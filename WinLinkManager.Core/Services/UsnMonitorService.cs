using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using WinLinkManager.Core.Native;

namespace WinLinkManager.Core.Services;

/// <summary>
/// USN 日志监控服务：通过 USN 日志轮询 + FileSystemWatcher 双重机制检测文件变更
/// </summary>
public class UsnMonitorService : IUsnMonitorService, IDisposable
{
    private readonly ILogger<UsnMonitorService> _logger;
    private readonly IIndexService _indexService;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private readonly List<FileSystemWatcher> _watchers = new(); // FileSystemWatcher 回退监控
    private long _lastUsn;        // 上次读取的 USN 序号
    private long _usnJournalId;   // 当前卷的 USN 日志 ID
    private bool _usnReady;       // USN journal 是否已就绪
    private ConcurrentDictionary<string, byte> _pendingChanges = new(); // 去重收集的待处理变更
    private const int DebounceMs = 500;  // 变更事件防抖间隔

    /// <summary>文件系统变更事件</summary>
    public event EventHandler<FsChangeEventArgs>? ChangeDetected;

    /// <summary>是否正处于回退模式（FileSystemWatcher）</summary>
    public bool IsFallbackMode { get; private set; }
    /// <summary>监控是否正在运行</summary>
    public bool IsRunning { get; private set; }

    public UsnMonitorService(
        IIndexService indexService,
        ILogger<UsnMonitorService> logger)
    {
        _indexService = indexService;
        _logger = logger;
    }

    /// <summary>启动监控：初始化 USN journal，启动轮询，启动 FileSystemWatcher 辅助</summary>
    public async Task StartAsync(CancellationToken ct)
    {
        if (IsRunning) return;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        IsRunning = true;

        var scanDirs = await _indexService.GetScanDirectoriesAsync();
        var activePaths = scanDirs.Where(d => !d.IsExcluded && Directory.Exists(d.Path)).Select(d => d.Path).ToList();
        if (activePaths.Count == 0) { _logger.LogWarning("没有有效的扫描目录，跳过文件监控"); return; }

        try
        {
            if (TryInitUsnJournal())
            {
                _logger.LogInformation("USN journal 可用，启动 USN 轮询监控");
                _monitorTask = Task.Factory.StartNew(
                    () => UsnPollingLoopAsync(_cts.Token),
                    _cts.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default).Unwrap();
            }
            else throw new InvalidOperationException("USN journal 不可用");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "USN journal 不可用，回退到 FileSystemWatcher");
            IsFallbackMode = true;
        }

        StartWatchers(activePaths);
        _logger.LogInformation("文件监控已启动，扫描目录: {Count} 个", activePaths.Count);
    }

    /// <summary>停止监控：取消轮询、清理 watcher、等待任务完成</summary>
    public async Task StopAsync()
    {
        if (!IsRunning) return;
        _cts?.Cancel();
        foreach (var fsw in _watchers) { fsw.EnableRaisingEvents = false; fsw.Dispose(); }
        _watchers.Clear();
        if (_monitorTask != null) { try { await _monitorTask; } catch (OperationCanceledException) { } }
        IsRunning = false;
        _logger.LogInformation("文件监控已停止");
    }

    /// <summary>尝试初始化 USN journal，读取当前 LastUsn 和 JournalId</summary>
    private bool TryInitUsnJournal()
    {
        try
        {
            var outputBuffer = Marshal.AllocHGlobal(64);
            try
            {
                using var probeHandle = NtfsNative.CreateFileW(@"\\.\C:", NtfsNative.GENERIC_READ,
                    NtfsNative.FILE_SHARE_READ | NtfsNative.FILE_SHARE_WRITE, IntPtr.Zero,
                    NtfsNative.OPEN_EXISTING, NtfsNative.FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
                if (probeHandle.IsInvalid) return false;
                if (!NtfsNative.DeviceIoControl(probeHandle, NtfsNative.FSCTL_QUERY_USN_JOURNAL,
                    IntPtr.Zero, 0, outputBuffer, 64, out _, IntPtr.Zero)) return false;
                _usnJournalId = Marshal.ReadInt64(outputBuffer, 0);   // UsnJournalID
                _lastUsn = Marshal.ReadInt64(outputBuffer, 16);       // NextUsn
                _usnReady = true;
                return true;
            }
            finally { Marshal.FreeHGlobal(outputBuffer); }
        }
        catch { return false; }
    }

    /// <summary>USN 轮询主循环：阻塞式读取 USN journal 的变更记录</summary>
    private async Task UsnPollingLoopAsync(CancellationToken ct)
    {
        _logger.LogInformation("USN 阻塞监听已启动 (NextUsn={LastUsn})", _lastUsn);

        using var volumeHandle = NtfsNative.CreateFileW(@"\\.\C:", NtfsNative.GENERIC_READ,
            NtfsNative.FILE_SHARE_READ | NtfsNative.FILE_SHARE_WRITE, IntPtr.Zero,
            NtfsNative.OPEN_EXISTING, NtfsNative.FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
        if (volumeHandle.IsInvalid)
        {
            _logger.LogWarning("无法打开卷句柄进行 USN 轮询");
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            if (!_usnReady) { await Task.Delay(1000, ct); continue; }

            try
            {
                if (CheckUsnJournalBlocking(volumeHandle, ct))
                    FireEvent(FsChangeType.Modified, "");
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "USN 读取异常，停止 USN 监控");
                break;
            }
        }
    }

    /// <summary>
    /// 阻塞式 USN 读取。DeviceIoControl 在内核态等待直到有数据或超时，
    /// 期间线程挂起，CPU 占用为 0。只读一次记录后立即返回。
    /// </summary>
    private bool CheckUsnJournalBlocking(SafeFileHandle volumeHandle, CancellationToken ct)
    {
        // 构造 READ_USN_JOURNAL_DATA_V0 输入结构（40 字节）
        var inputBuf = Marshal.AllocHGlobal(40);
        try
        {
            Marshal.WriteInt64(inputBuf, 0, _lastUsn + 1);     // StartUsn: 从此序号后开始读取
            Marshal.WriteInt32(inputBuf, 8, unchecked((int)0xFFFFFFFF));  // ReasonMask: 监听所有变更原因
            Marshal.WriteInt32(inputBuf, 12, 1);                // ReturnOnlyOnClose: 只返回关闭时的记录
            Marshal.WriteInt64(inputBuf, 16, 2L);               // Timeout: 2 秒内核等待
            Marshal.WriteInt64(inputBuf, 24, 0L);               // BytesToWaitFor: 0（有数据即返回）
            Marshal.WriteInt64(inputBuf, 32, _usnJournalId);    // UsnJournalID
        }
        catch { Marshal.FreeHGlobal(inputBuf); return false; }

        var outputBuf = Marshal.AllocHGlobal(65536);
        var foundReparse = false;

        try
        {
            if (!NtfsNative.DeviceIoControl(volumeHandle, NtfsNative.FSCTL_READ_USN_JOURNAL,
                    inputBuf, 40, outputBuf, 65536, out var bytesReturned, IntPtr.Zero))
            {
                var error = Marshal.GetLastWin32Error();
                // ERROR_HANDLE_EOF (38) = 超时无数据（正常），返回 false
                if (error == 38) return false;
                _logger.LogWarning("FSCTL_READ_USN_JOURNAL 失败: error={Error}", error);
                return false;
            }

            if (bytesReturned < 8) return false;

            // 遍历记录，查找重解析点变更（排除 CLOSE 事件以避免重复）
            var recordEnd = bytesReturned - 8L; // 最后 8 字节是 NextUsn
            var offset = 0u;

            while (offset + 60 <= recordEnd)
            {
                var recordLen = Marshal.ReadInt32(outputBuf, (int)offset);
                if (recordLen <= 0 || offset + (uint)recordLen > recordEnd) break;

                var attrs = Marshal.ReadInt32(outputBuf, (int)offset + 32);
                var reason = Marshal.ReadInt32(outputBuf, (int)offset + 24);

                // 只关注重解析点（符号链接/交接点）的非关闭事件
                if ((attrs & (int)NtfsNative.FILE_ATTRIBUTE_REPARSE_POINT) != 0)
                {
                    if (reason != unchecked((int)NtfsNative.USN_REASON_CLOSE))
                        foundReparse = true;
                }

                offset += (uint)recordLen;
            }

            // 更新 LastUsn 为下一轮起始位置
            _lastUsn = Marshal.ReadInt64(outputBuf, (int)recordEnd);
        }
        finally
        {
            Marshal.FreeHGlobal(inputBuf);
            Marshal.FreeHGlobal(outputBuf);
        }

        return foundReparse;
    }

    /// <summary>在指定路径上启动 FileSystemWatcher 作为辅助监控</summary>
    private void StartWatchers(List<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                var fsw = new FileSystemWatcher
                {
                    Path = path, IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Attributes,
                    InternalBufferSize = 65536
                };
                fsw.Created += OnWatcherEvent;
                fsw.Deleted += OnWatcherEvent;
                fsw.Changed += OnWatcherEvent;
                fsw.Renamed += OnWatcherRenamed;
                fsw.Error += OnWatcherError;
                fsw.EnableRaisingEvents = true;
                _watchers.Add(fsw);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "无法在 {Path} 上启动 FSW", path); }
        }
    }

    /// <summary>FSW 事件处理：仅关注重解析点，加入防抖队列</summary>
    private void OnWatcherEvent(object sender, FileSystemEventArgs e)
    {
        bool isReparse;
        try { var attr = File.GetAttributes(e.FullPath); isReparse = attr.HasFlag(FileAttributes.ReparsePoint); }
        catch { isReparse = e.ChangeType == WatcherChangeTypes.Deleted; }
        if (!isReparse) return;
        var type = e.ChangeType switch
        {
            WatcherChangeTypes.Created => FsChangeType.Created,
            WatcherChangeTypes.Deleted => FsChangeType.Deleted,
            _ => FsChangeType.Modified
        };
        _pendingChanges[e.FullPath] = 1;
        DebounceFire(type, e.FullPath);
    }

    /// <summary>FSW 重命名事件：同时记录新旧路径</summary>
    private void OnWatcherRenamed(object sender, RenamedEventArgs e)
    {
        bool isReparse;
        try { var attr = File.GetAttributes(e.FullPath); isReparse = attr.HasFlag(FileAttributes.ReparsePoint); }
        catch { isReparse = false; }
        if (!isReparse) return;
        _pendingChanges[e.FullPath] = 1;
        _pendingChanges[e.OldFullPath] = 1;
        DebounceFire(FsChangeType.Modified, e.FullPath);
    }

    /// <summary>FSW 错误日志</summary>
    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "FSW 错误");
    }

    /// <summary>防抖延迟触发事件，合并短时间内的多次变更</summary>
    private CancellationTokenSource? _debounceCts;
    private void DebounceFire(FsChangeType type, string path)
    {
        lock (this)
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DebounceMs, token);
                    var snapshot = Interlocked.Exchange(ref _pendingChanges, new ConcurrentDictionary<string, byte>());
                    if (snapshot.Count > 0)
                        FireEvent(type, path, snapshot.Keys.ToList());
                }
                catch (TaskCanceledException) { }
            }, token);
        }
    }

    /// <summary>触发变更事件通知订阅者</summary>
    private void FireEvent(FsChangeType type, string path, List<string>? affectedPaths = null)
    {
        try
        {
            ChangeDetected?.Invoke(this, new FsChangeEventArgs
            {
                ChangeType = type,
                Path = path,
                AffectedPaths = affectedPaths ?? new List<string> { path }
            });
        }
        catch (Exception ex) { _logger.LogWarning(ex, "事件处理器异常"); }
    }

    /// <summary>释放资源：停止所有 watcher 并取消后台任务</summary>
    public void Dispose()
    {
        foreach (var fsw in _watchers) { fsw.EnableRaisingEvents = false; fsw.Dispose(); }
        _watchers.Clear();
        _cts?.Cancel(); _cts?.Dispose();
        _debounceCts?.Dispose();
    }
}

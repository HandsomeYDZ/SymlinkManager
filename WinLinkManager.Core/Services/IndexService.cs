using System.Collections.Concurrent;
using WinLinkManager.Core.Data;
using WinLinkManager.Core.Models;

namespace WinLinkManager.Core.Services;

/// <summary>
/// 链接索引服务，以内存缓存加速访问并通过批量持久化减少数据库写入
/// </summary>
public class IndexService : IIndexService
{
    private readonly AppDbContext _db;
    private readonly ConcurrentDictionary<string, LinkEntry> _cache = new();
    private ConcurrentDictionary<string, byte> _dirtyKeys = new(); // 跟踪已修改但未持久化的条目

    public IndexService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>从数据库加载所有链接到缓存</summary>
    public async Task InitializeAsync()
    {
        var links = await _db.LoadAllLinksAsync();
        foreach (var link in links)
        {
            _cache[link.LinkPath] = link;
        }
    }

    /// <summary>检查缓存中是否已有索引数据</summary>
    public Task<bool> HasExistingIndexAsync()
    {
        return Task.FromResult(_cache.Count > 0);
    }

    /// <summary>插入或更新缓存条目并标记为脏数据</summary>
    public Task UpsertAsync(LinkEntry entry)
    {
        _cache[entry.LinkPath] = entry;
        _dirtyKeys.TryAdd(entry.LinkPath, 0);
        return Task.CompletedTask;
    }

    /// <summary>获取所有缓存的链接条目</summary>
    public Task<List<LinkEntry>> GetAllAsync()
    {
        return Task.FromResult(_cache.Values.ToList());
    }

    /// <summary>获取扫描目录配置</summary>
    public Task<List<ScanDirectoryConfig>> GetScanDirectoriesAsync()
    {
        return _db.LoadScanDirectoriesAsync();
    }

    /// <summary>保存扫描目录配置</summary>
    public Task SaveScanDirectoriesAsync(List<ScanDirectoryConfig> dirs)
    {
        return _db.SaveScanDirectoriesAsync(dirs);
    }

    /// <summary>从缓存和数据库中删除指定链接</summary>
    public async Task DeleteAsync(string linkPath)
    {
        _cache.TryRemove(linkPath, out _);
        _dirtyKeys.TryRemove(linkPath, out _);
        await _db.DeleteLinkAsync(linkPath);
    }

    /// <summary>清空缓存和数据库中的全部索引</summary>
    public async Task RebuildIndexAsync()
    {
        _cache.Clear();
        _dirtyKeys.Clear();
        await _db.ClearIndexAsync();
    }

    /// <summary>启动后台持久化循环，每 30 秒将脏数据批量写入数据库</summary>
    public CancellationTokenSource StartBatchPersist(CancellationToken ct)
    {
        Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(30000, ct);
                    // 原子交换获取当前脏数据快照
                    var snapshot = Interlocked.Exchange(ref _dirtyKeys, new ConcurrentDictionary<string, byte>());
                    if (snapshot.Count > 0)
                    {
                        var entries = snapshot.Keys.Select(k => _cache[k]).ToList();
                        await _db.BatchUpsertLinksAsync(entries);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // 正常关闭，不做处理
            }
            finally
            {
                // 关闭前刷入剩余脏数据
                var snapshot = Interlocked.Exchange(ref _dirtyKeys, new ConcurrentDictionary<string, byte>());
                if (snapshot.Count > 0)
                {
                    var entries = snapshot.Keys.Select(k => _cache[k]).ToList();
                    await _db.BatchUpsertLinksAsync(entries);
                }
            }
        }, ct);

        return new CancellationTokenSource();
    }
}

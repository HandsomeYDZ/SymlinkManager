using WinLinkManager.Core.Models;

namespace WinLinkManager.Core.Services;

/// <summary>
/// 扫描器服务接口：遍历文件系统发现所有符号链接
/// </summary>
public interface IScannerService
{
    /// <summary>执行全量扫描，异步流式返回发现的链接条目</summary>
    IAsyncEnumerable<LinkEntry> FullScanAsync(
        List<ScanDirectoryConfig> scanDirs,
        IProgress<ScanProgress>? progress,
        CancellationToken ct);
}

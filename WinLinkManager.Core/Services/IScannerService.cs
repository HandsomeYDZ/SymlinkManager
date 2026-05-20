using WinLinkManager.Core.Models;

namespace WinLinkManager.Core.Services;

public interface IScannerService
{
    IAsyncEnumerable<SymlinkEntry> FullScanAsync(
        List<ScanDirectoryConfig> scanDirs,
        IProgress<ScanProgress>? progress,
        CancellationToken ct);
}

using WinLinkManager.Core.Models;

namespace WinLinkManager.Core.Services;

/// <summary>
/// 文件系统变更类型
/// </summary>
public enum FsChangeType
{
    Created,
    Deleted,
    Modified
}

/// <summary>
/// 文件系统变更事件参数
/// </summary>
public class FsChangeEventArgs : EventArgs
{
    public FsChangeType ChangeType { get; init; }
    public string Path { get; init; } = string.Empty;
    public string? OldPath { get; init; }
    public IReadOnlyCollection<string> AffectedPaths { get; init; } = Array.Empty<string>();
}

/// <summary>
/// USN 日志监控服务接口，检测文件系统实时变更
/// </summary>
public interface IUsnMonitorService
{
    event EventHandler<FsChangeEventArgs>? ChangeDetected;
    Task StartAsync(CancellationToken ct);
    Task StopAsync();
    bool IsFallbackMode { get; }
}

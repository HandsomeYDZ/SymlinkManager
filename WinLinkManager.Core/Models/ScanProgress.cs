namespace WinLinkManager.Core.Models;

/// <summary>
/// 扫描进度报告：已扫描数、发现链接数、当前目录和完成标记
/// </summary>
public class ScanProgress
{
    public long TotalScanned { get; set; }
    public long LinksFound { get; set; }
    public string? CurrentDirectory { get; set; }
    public bool IsComplete { get; set; }
}

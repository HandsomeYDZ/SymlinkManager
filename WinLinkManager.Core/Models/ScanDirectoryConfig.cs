namespace WinLinkManager.Core.Models;

/// <summary>
/// 扫描目录配置：包含路径、排除标记和添加时间
/// </summary>
public class ScanDirectoryConfig
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public bool IsExcluded { get; set; }
    public string AddedTime { get; set; } = string.Empty;
}

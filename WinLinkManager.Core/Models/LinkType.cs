namespace WinLinkManager.Core.Models;

/// <summary>
/// 符号链接类型：文件链接、目录链接或交接点
/// </summary>
public enum LinkType
{
    FileLink = 0,
    DirectoryLink = 1,
    Junction = 2
}

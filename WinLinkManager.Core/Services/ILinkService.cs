using WinLinkManager.Core.Models;

namespace WinLinkManager.Core.Services;

/// <summary>
/// 符号链接操作服务接口：创建、删除、类型检测与转换
/// </summary>
public interface ILinkService
{
    /// <summary>创建符号链接（文件/目录/Junction）</summary>
    bool CreateLink(string linkPath, string targetPath, LinkType type);
    /// <summary>删除指定路径的符号链接</summary>
    void DeleteLink(string linkPath, LinkType type);
    /// <summary>转换链接类型（如目录链接转交接点）</summary>
    ConvertResult ConvertType(string linkPath, LinkType currentType, LinkType newType, string newTarget);
    /// <summary>检测路径上的链接类型</summary>
    LinkType DetectType(string linkPath);
    /// <summary>检查路径是否存在</summary>
    bool Exists(string linkPath);
}

/// <summary>
/// 类型转换操作结果
/// </summary>
public class ConvertResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

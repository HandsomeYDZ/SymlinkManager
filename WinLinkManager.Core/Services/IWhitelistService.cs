namespace WinLinkManager.Core.Services;

/// <summary>
/// 白名单服务接口：管理白名单路径的增删查
/// </summary>
public interface IWhitelistService
{
    /// <summary>自动添加（由程序逻辑自动识别）</summary>
    Task AddAutoAsync(string path);
    /// <summary>手动添加（用户主动操作）</summary>
    Task AddManualAsync(string path);
    /// <summary>从白名单中移除</summary>
    Task RemoveAsync(string path);
    /// <summary>检查路径是否在白名单中</summary>
    Task<bool> IsInWhitelistAsync(string path);
    /// <summary>获取所有白名单路径</summary>
    Task<List<string>> GetAllPathsAsync();
}

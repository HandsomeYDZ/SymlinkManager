using WinLinkManager.Core.Data;

namespace WinLinkManager.Core.Services;

/// <summary>
/// 白名单服务实现，委托 AppDbContext 执行持久化操作
/// </summary>
public class WhitelistService : IWhitelistService
{
    private readonly AppDbContext _dbContext;
    private readonly IIndexService _indexService;

    public WhitelistService(AppDbContext dbContext, IIndexService indexService)
    {
        _dbContext = dbContext;
        _indexService = indexService;
    }

    /// <summary>自动添加（来源标记为 auto）</summary>
    public async Task AddAutoAsync(string path)
        => await _dbContext.AddWhitelistAsync(path, "auto");

    /// <summary>手动添加（来源标记为 manual）</summary>
    public async Task AddManualAsync(string path)
        => await _dbContext.AddWhitelistAsync(path, "manual");

    /// <summary>从白名单中移除</summary>
    public async Task RemoveAsync(string path)
        => await _dbContext.RemoveWhitelistAsync(path);

    /// <summary>检查路径是否在白名单中（忽略大小写）</summary>
    public async Task<bool> IsInWhitelistAsync(string path)
    {
        var paths = await _dbContext.GetAllWhitelistPathsAsync();
        return paths.Contains(path, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>获取所有白名单路径</summary>
    public async Task<List<string>> GetAllPathsAsync()
        => await _dbContext.GetAllWhitelistPathsAsync();
}

using WinLinkManager.Core.Models;

namespace WinLinkManager.Core.Services;

/// <summary>
/// 链接索引服务接口，管理内存缓存与持久化同步
/// </summary>
public interface IIndexService
{
    /// <summary>从数据库加载所有链接到内存缓存</summary>
    Task InitializeAsync();
    /// <summary>检查是否已有索引数据</summary>
    Task<bool> HasExistingIndexAsync();
    /// <summary>插入或更新链接到缓存并标记脏数据</summary>
    Task UpsertAsync(LinkEntry entry);
    /// <summary>获取所有缓存的链接条目</summary>
    Task<List<LinkEntry>> GetAllAsync();
    /// <summary>获取扫描目录配置</summary>
    Task<List<ScanDirectoryConfig>> GetScanDirectoriesAsync();
    /// <summary>保存扫描目录配置</summary>
    Task SaveScanDirectoriesAsync(List<ScanDirectoryConfig> dirs);
    /// <summary>清空索引并重建</summary>
    Task RebuildIndexAsync();
    /// <summary>从索引和数据库中删除指定链接</summary>
    Task DeleteAsync(string linkPath);
    /// <summary>启动后台批量持久化任务，每 30 秒将脏数据写入数据库</summary>
    CancellationTokenSource StartBatchPersist(CancellationToken ct);
}

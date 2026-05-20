using Microsoft.Data.Sqlite;
using WinLinkManager.Core.Models;

namespace WinLinkManager.Core.Data;

/// <summary>
/// SQLite 数据库上下文，处理所有表 CRUD 和初始化
/// </summary>
public class AppDbContext
{
    private readonly string _connectionString;

    public AppDbContext(string connectionString = "Data Source=winlink-manager.db")
    {
        _connectionString = connectionString;
    }

    /// <summary>创建新的 SQLite 连接</summary>
    public SqliteConnection CreateConnection() => new(_connectionString);

    /// <summary>初始化数据库，创建所有表和默认数据</summary>
    public async Task InitializeAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        // PRAGMA 必须在其他命令之前单独执行
        using var pragmaCmd = conn.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA journal_mode=WAL;";
        await pragmaCmd.ExecuteNonQueryAsync();

        // Create each table separately (SqliteCommand only executes first statement)
        await ExecuteNonQueryAsync(conn, @"CREATE TABLE IF NOT EXISTS ScanIndex (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            LinkPath TEXT NOT NULL UNIQUE,
            LinkName TEXT NOT NULL,
            TargetPath TEXT NOT NULL,
            LinkType INTEGER NOT NULL,
            CreationTime TEXT NOT NULL,
            Status INTEGER NOT NULL DEFAULT 0,
            InWhitelist INTEGER NOT NULL DEFAULT 0,
            LastSeenTime TEXT NOT NULL
        )");

        await ExecuteNonQueryAsync(conn, @"CREATE TABLE IF NOT EXISTS Whitelist (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Path TEXT NOT NULL UNIQUE,
            AddedTime TEXT NOT NULL,
            Source TEXT NOT NULL
        )");

        await ExecuteNonQueryAsync(conn, @"CREATE TABLE IF NOT EXISTS ScanDirectories (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Path TEXT NOT NULL UNIQUE,
            IsExcluded INTEGER NOT NULL DEFAULT 0,
            AddedTime TEXT NOT NULL
        )");

        await ExecuteNonQueryAsync(conn, @"CREATE TABLE IF NOT EXISTS AppConfig (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ConfigKey TEXT NOT NULL UNIQUE,
            ConfigValue TEXT NOT NULL
        )");

        // Insert default system exclusions if table is empty
        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM ScanDirectories";
        var count = (long)(await countCmd.ExecuteScalarAsync())!;
        if (count == 0)
        {
            await ExecuteNonQueryAsync(conn, @"INSERT INTO ScanDirectories (Path, IsExcluded, AddedTime) VALUES
                ('C:\Windows', 1, datetime('now'))");
            await ExecuteNonQueryAsync(conn, @"INSERT INTO ScanDirectories (Path, IsExcluded, AddedTime) VALUES
                ('C:\Program Files', 1, datetime('now'))");
            await ExecuteNonQueryAsync(conn, @"INSERT INTO ScanDirectories (Path, IsExcluded, AddedTime) VALUES
                ('C:\Program Files (x86)', 1, datetime('now'))");

            await InsertDefaultFixedDriveRootsAsync(conn);
        }
        else
        {
            using var includedCountCmd = conn.CreateCommand();
            includedCountCmd.CommandText = "SELECT COUNT(*) FROM ScanDirectories WHERE IsExcluded = 0";
            var includedCount = (long)(await includedCountCmd.ExecuteScalarAsync())!;
            if (includedCount == 0)
            {
                await InsertDefaultFixedDriveRootsAsync(conn);
            }
        }
    }

    /// <summary>执行非查询 SQL 辅助方法</summary>
    private static async Task ExecuteNonQueryAsync(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>插入所有固定硬盘根目录作为默认扫描路径</summary>
    private static async Task InsertDefaultFixedDriveRootsAsync(SqliteConnection conn)
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType == DriveType.Fixed && drive.IsReady)
            {
                using var insertCmd = conn.CreateCommand();
                insertCmd.CommandText = @"INSERT INTO ScanDirectories (Path, IsExcluded, AddedTime) VALUES (@path, 0, datetime('now'))";
                insertCmd.Parameters.AddWithValue("@path", drive.Name);
                await insertCmd.ExecuteNonQueryAsync();
            }
        }
    }

    // === ScanIndex CRUD ===
    /// <summary>加载所有链接索引，左连白名单表以获取完整白名单状态</summary>
    public async Task<List<LinkEntry>> LoadAllLinksAsync()
    {
        var links = new List<LinkEntry>();
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT i.LinkPath, i.LinkName, i.TargetPath, i.LinkType, i.CreationTime, i.Status, i.InWhitelist, i.LastSeenTime, w.Path
            FROM ScanIndex i
            LEFT JOIN Whitelist w ON i.LinkPath = w.Path";
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var inWhitelistFromIndex = reader.GetInt32(6) == 1;
            var inWhitelistFromTable = !reader.IsDBNull(8);

            links.Add(new LinkEntry
            {
                LinkPath = reader.GetString(0),
                LinkName = reader.GetString(1),
                TargetPath = reader.GetString(2),
                LinkType = (LinkType)reader.GetInt32(3),
                CreationTime = reader.GetString(4),
                Status = (LinkStatus)reader.GetInt32(5),
                InWhitelist = inWhitelistFromIndex || inWhitelistFromTable,
                LastSeenTime = reader.GetString(7)
            });
        }
        return links;
    }

    /// <summary>插入或更新单条链接记录（按 LinkPath 冲突合并）</summary>
    public async Task UpsertLinkAsync(LinkEntry entry)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ScanIndex (LinkPath, LinkName, TargetPath, LinkType, CreationTime, Status, InWhitelist, LastSeenTime)
            VALUES (@path, @name, @target, @type, @ctime, @status, @wl, @seen)
            ON CONFLICT(LinkPath) DO UPDATE SET
                LinkName=@name, TargetPath=@target, LinkType=@type,
                Status=@status, InWhitelist=@wl, LastSeenTime=@seen;
        ";
        cmd.Parameters.AddWithValue("@path", entry.LinkPath);
        cmd.Parameters.AddWithValue("@name", entry.LinkName);
        cmd.Parameters.AddWithValue("@target", entry.TargetPath);
        cmd.Parameters.AddWithValue("@type", (int)entry.LinkType);
        cmd.Parameters.AddWithValue("@ctime", entry.CreationTime);
        cmd.Parameters.AddWithValue("@status", (int)entry.Status);
        cmd.Parameters.AddWithValue("@wl", entry.InWhitelist ? 1 : 0);
        cmd.Parameters.AddWithValue("@seen", entry.LastSeenTime);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>批量插入或更新链接记录，在事务中执行以提高性能</summary>
    public async Task BatchUpsertLinksAsync(IEnumerable<LinkEntry> entries)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        foreach (var entry in entries)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO ScanIndex (LinkPath, LinkName, TargetPath, LinkType, CreationTime, Status, InWhitelist, LastSeenTime)
                VALUES (@path, @name, @target, @type, @ctime, @status, @wl, @seen)
                ON CONFLICT(LinkPath) DO UPDATE SET
                    LinkName=@name, TargetPath=@target, LinkType=@type,
                    Status=@status, InWhitelist=@wl, LastSeenTime=@seen;
            ";
            cmd.Parameters.AddWithValue("@path", entry.LinkPath);
            cmd.Parameters.AddWithValue("@name", entry.LinkName);
            cmd.Parameters.AddWithValue("@target", entry.TargetPath);
            cmd.Parameters.AddWithValue("@type", (int)entry.LinkType);
            cmd.Parameters.AddWithValue("@ctime", entry.CreationTime);
            cmd.Parameters.AddWithValue("@status", (int)entry.Status);
            cmd.Parameters.AddWithValue("@wl", entry.InWhitelist ? 1 : 0);
            cmd.Parameters.AddWithValue("@seen", entry.LastSeenTime);
            await cmd.ExecuteNonQueryAsync();
        }

        tx.Commit();
    }

    /// <summary>删除指定路径的链接记录</summary>
    public async Task DeleteLinkAsync(string linkPath)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ScanIndex WHERE LinkPath = @path";
        cmd.Parameters.AddWithValue("@path", linkPath);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>清空整个链接索引表</summary>
    public async Task ClearIndexAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ScanIndex";
        await cmd.ExecuteNonQueryAsync();
    }

    // === Whitelist CRUD ===
    /// <summary>添加白名单项并同步更新 LinkIndex 的白名单标记</summary>
    public async Task AddWhitelistAsync(string path, string source)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT OR REPLACE INTO Whitelist (Path, AddedTime, Source) VALUES (@path, datetime('now'), @source)";
        cmd.Parameters.AddWithValue("@path", path);
        cmd.Parameters.AddWithValue("@source", source);
        await cmd.ExecuteNonQueryAsync();

        using var updateCmd = conn.CreateCommand();
        updateCmd.CommandText = "UPDATE ScanIndex SET InWhitelist = 1 WHERE LinkPath = @path";
        updateCmd.Parameters.AddWithValue("@path", path);
        await updateCmd.ExecuteNonQueryAsync();
    }

    /// <summary>移除白名单项并同步清除 LinkIndex 的白名单标记</summary>
    public async Task RemoveWhitelistAsync(string path)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Whitelist WHERE Path = @path";
        cmd.Parameters.AddWithValue("@path", path);
        await cmd.ExecuteNonQueryAsync();

        using var updateCmd = conn.CreateCommand();
        updateCmd.CommandText = "UPDATE ScanIndex SET InWhitelist = 0 WHERE LinkPath = @path";
        updateCmd.Parameters.AddWithValue("@path", path);
        await updateCmd.ExecuteNonQueryAsync();
    }

    /// <summary>获取所有白名单路径列表</summary>
    public async Task<List<string>> GetAllWhitelistPathsAsync()
    {
        var paths = new List<string>();
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Path FROM Whitelist";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            paths.Add(reader.GetString(0));
        return paths;
    }

    // === ScanDirectories CRUD ===
    /// <summary>加载所有扫描目录配置</summary>
    public async Task<List<ScanDirectoryConfig>> LoadScanDirectoriesAsync()
    {
        var dirs = new List<ScanDirectoryConfig>();
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Path, IsExcluded, AddedTime FROM ScanDirectories";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            dirs.Add(new ScanDirectoryConfig
            {
                Id = reader.GetInt32(0),
                Path = reader.GetString(1),
                IsExcluded = reader.GetInt32(2) == 1,
                AddedTime = reader.GetString(3)
            });
        }
        return dirs;
    }

    /// <summary>全量保存扫描目录配置（先清空再插入，事务中执行）</summary>
    public async Task SaveScanDirectoriesAsync(List<ScanDirectoryConfig> dirs)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        using var clearCmd = conn.CreateCommand();
        clearCmd.Transaction = tx;
        clearCmd.CommandText = "DELETE FROM ScanDirectories";
        await clearCmd.ExecuteNonQueryAsync();

        foreach (var d in dirs)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO ScanDirectories (Path, IsExcluded, AddedTime) VALUES (@path, @excl, @time)";
            cmd.Parameters.AddWithValue("@path", d.Path);
            cmd.Parameters.AddWithValue("@excl", d.IsExcluded ? 1 : 0);
            cmd.Parameters.AddWithValue("@time", d.AddedTime);
            await cmd.ExecuteNonQueryAsync();
        }

        tx.Commit();
    }
}

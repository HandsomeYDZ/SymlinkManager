# WinLinkManager — Windows 符号链接管理器

Windows 平台 NTFS 符号链接 / 交接点（Junction）的全生命周期管理工具。
支持全盘扫描、实时监控、健康检测、白名单过滤。

## 功能

- **全盘扫描** — 基于 NTFS MFT（主文件表）的高速扫描，秒级发现系统中所有符号链接和交接点
- **实时监控** — 通过 USN Journal（更新序列号日志） + FileSystemWatcher 双重机制，实时感知链接的增删改
- **链接管理** — 创建、编辑、删除链接，支持文件符号链接、目录符号链接(/D)、交接点(/J) 三种类型
- **类型转换** — 目录符号链接与交接点之间安全互转，含自动备份/回滚机制
- **健康检测** — 自动检测链接目标是否存在，标记"有效"或"失效"状态
- **白名单机制** — 手动或自动标记常用链接，支持按白名单筛选列表
- **搜索过滤** — 按链接名称、路径、目标路径实时搜索
- **索引持久化** — SQLite 存储索引数据，启动即用，无需重复扫描

## 技术栈

- **语言 / 框架：** C# 12 / WPF / .NET Framework 4.8
- **架构模式：** MVVM（Model-View-ViewModel）
- **数据库：** SQLite（Microsoft.Data.Sqlite）
- **依赖注入：** Microsoft.Extensions.DependencyInjection
- **日志：** Serilog（文件滚动日志，保留 30 天）
- **底层 API：** NTFS USN Journal、FSCTL 重解析点操作、MFT 枚举
- **配置：** JSON 配置文件（%LocalAppData%\WinLinkManager\config\）

## 项目结构

```
WinLinkManager.App/     # WPF 应用层 — View、ViewModel、Converter
WinLinkManager.Core/    # 核心类库 — Model、Service、Native P/Invoke、Data
```

## 运行要求

- Windows 10+（需要 NTFS 文件系统）
- **管理员权限**（读取 MFT / 操作重解析点必需）
- .NET Framework 4.8 Runtime

## 本地运行

```bash
dotnet run --project WinLinkManager.App
```

## 发布打包

```bash
dotnet publish WinLinkManager.App -c Release -o publish
```

输出目录：`publish\`，运行 `WinLinkManager.App.exe`（需以管理员身份运行）。

## 数据目录

```
%LocalAppData%\WinLinkManager\
├── config\          # 配置文件
│   └── app.config
├── logs\            # 运行日志（按日滚动）
└── winlink-manager.db   # SQLite 索引数据库
```

## License

MIT

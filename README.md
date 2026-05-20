# Windows 链接管理器 (NTFS WinLink Manager)

> **⚠️ 声明：本项目由 AI 全权生成，鄙人不会维护不提供支持。仅供参考。**

---

## 功能

一个 Windows NTFS 符号链接和交接点（Junction）的 GUI 管理工具，用于替代 `mklink` 命令行的繁琐操作。

- **列表浏览** — 表格展示所有链接的名称、路径、目标、类型、创建时间、失效状态
- **实时搜索** — 按名称/路径即时过滤
- **创建链接** — 支持三种类型：文件符号链接、目录符号链接(/D)、交接点(/J)
- **类型转换** — /D ↔ /J 一键互转，带预览确认和备份回滚
- **白名单** — 软件创建的链接自动加入白名单，支持右键手动添加/移除；底部 Tab 切换"全部"/"白名单"视图

## 技术栈

- C# / .NET Framework 4.8 / WPF
- NTFS USN (FSCTL_ENUM_USN_DATA)
- SQLite 嵌入式数据库

## 使用

### 直接运行（开发环境）

```bash
dotnet run --project WinLinkManager.App
```

### 下载使用（无需安装任何运行时）

从 [Releases](../../releases) 下载 `WinLinkManager.zip`（约 3.6MB），解压后右键 `WinLinkManager.App.exe` → **以管理员身份运行**。

> **无需安装 .NET 运行时** — Windows 10/11 自带 .NET Framework 4.8/4.8.1，解压即用。

### 自行打包

```bash
dotnet publish WinLinkManager.App -c Release -o publish
```

输出：`publish\` 目录（约 8MB），可以打包为 zip 分发。

> **需要管理员权限** — 扫描 NTFS 卷必需，启动时如果不在管理员终端会弹出提权对话框。

## 数据存储

所有数据（数据库、配置、日志）存储在：

```
%LocalAppData%\WinLinkManager\
```

## License

MIT — 我不会维护，随便用。

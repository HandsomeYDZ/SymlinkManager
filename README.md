# Windows 符号链接管理器 (NTFS Symlink Manager)

> **⚠️ 声明：本项目由 AI 全权生成，作者不会维护、不接受 PR、不提供支持。仅供参考。**

---

## 功能

一个 Windows NTFS 符号链接和交接点（Junction）的 GUI 管理工具，用于替代 `mklink` 命令行的繁琐操作。

- **全盘扫描** — 基于 NTFS USN (MFT) 索引高速扫描所有本地磁盘上的符号链接和交接点
- **列表浏览** — 表格展示所有链接的名称、路径、目标、类型、创建时间、失效状态
- **实时搜索** — 按名称/路径即时过滤
- **创建链接** — 支持三种类型：文件符号链接、目录符号链接(/D)、交接点(/J)
- **类型转换** — /D ↔ /J 一键互转，带预览确认和备份回滚
- **失效检测** — 自动标记目标路径不存在的链接（删除线 + 灰色）
- **白名单系统** — 软件创建的链接自动加入白名单，支持右键手动添加/移除；底部 Tab 切换"全部"/"白名单"视图
- **增量更新** — USN Journal 监控文件系统变更，索引保持实时同步
- **设置** — 图形化管理扫描目录（添加/移除/排除），可自定义数据库和配置路径

## 技术栈

- C# / .NET 10 / WPF
- NTFS USN (FSCTL_ENUM_USN_DATA) 直读扫描
- SQLite 嵌入式数据库
- 单文件发布（Self-contained，无需安装 .NET 运行时）

## 使用

### 直接运行（开发环境）

```bash
dotnet run --project SymlinkManager.App
```

### 独立 EXE（无需 .NET 运行时）

从 [Releases](../../releases) 下载 `SymlinkManager.App.exe`，右键 → **以管理员身份运行**。

### 自行打包

```bash
dotnet publish SymlinkManager.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

输出：`publish\SymlinkManager.App.exe` (~142MB)

> **需要管理员权限** — 扫描 NTFS 卷必需，启动时如果不在管理员终端会弹出提权对话框。

## 数据存储

所有数据（数据库、配置、日志）存储在：

```
%LocalAppData%\SymlinkManager\
```

## 截图

_（运行后自行截取）_

## License

MIT — 但反正没人维护，随便用。

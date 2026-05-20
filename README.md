# WinLinkManager (NTFS Link Manager)

> 说明：本项目为实验性质工具，仅作参考使用。

## 功能

- 浏览 NTFS 符号链接 / Junction 列表
- 按名称和路径搜索
- 创建链接（文件符号链接、目录符号链接、Junction）
- 链接类型转换（`/D` 与 `/J`）
- 白名单管理

## 技术栈

- C# / WPF / .NET Framework 4.8
- SQLite（Microsoft.Data.Sqlite）
- NTFS USN

## 本地运行

```bash
dotnet run --project WinLinkManager.App
```

## 手动打包（Portable 多文件）

```bash
dotnet publish WinLinkManager.App -c Release -o publish
```

输出目录：`publish\`  
分发方式：将 `publish\` 目录整体压缩成 zip 后分发。  
运行方式：右键 `WinLinkManager.App.exe`，选择“以管理员身份运行”。

## GitHub Actions 打包

仓库内置工作流：`.github/workflows/publish-release.yml`

- `workflow_dispatch`：手动触发
  - 可选输入 `version`（如 `v1.0.1`）
  - 可选输入 `create_release`（是否创建/更新 GitHub Release）
- `push tags`：推送 `v*` 标签时自动触发

工作流结果：

- 产出一个 portable zip（Artifact）
- 在触发条件满足时，将同一个 zip 上传到 GitHub Release

## 数据目录

程序数据默认存储于：

```text
%LocalAppData%\WinLinkManager\
```

## License

MIT

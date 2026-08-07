# Codex 对话迁移工具

[English](README.md)

这是一个本地运行的 Windows 工具，可把 Codex 项目、主对话和子代理对话一起备份、迁移、查看、恢复或删除。

> 本项目是非官方社区工具，与 OpenAI 没有关联，也未获得 OpenAI 背书。Codex 的本地存储格式可能变化，正式导入前请先检查，并为重要数据另留一份备份。

## 主要功能

- 可一次选择多个项目，将项目目录、主对话和子代理对话一起备份为一个 `.codexproject`。
- 可跨项目勾选主对话，仅备份为 `.codexchat`。
- 新电脑上可指定项目保存位置；对话仍自动导入 C 盘的 Codex 数据目录。
- 对话可“按原始编号智能合并”，也可“作为新对话复制”并生成全新 Thread ID，互不共用文件。
- 主对话和子代理对话分开查看，并显示项目、对话的大小和实际路径。
- 主对话与子代理页都支持全选、再次点击全不选、删除所选。
- 可移入软件回收站、从回收站恢复或永久删除；主对话删除时还可单独选择是否处理对应项目目录。
- 顶栏可立即切换中文/English，并记住选择。

## 文件后缀

| 后缀 | 内容 | 用途 |
| --- | --- | --- |
| `.codexchat` | 只含所选对话 | 只迁移或归档对话 |
| `.codexproject` | 一个或多个项目目录，以及对应主对话、子代理对话 | 整体迁移项目工作环境 |
| `.codexpack` / `.codexbundle` | 旧版本兼容格式 | 只用于导入旧备份 |

`.cct-bak` 不是正式备份。它只是导入事务的临时安全快照，成功或回滚后会自动清理。

## 最简单的使用方法

1. 从 Releases 下载 `CodexConversationMigrator-Windows-v3.0.0.zip`，并核对 SHA-256。
2. 把 ZIP 完整解压到同一个文件夹，不要拆开 EXE、XAML 和 `cct.exe`。
3. 双击 `Start.cmd` 或 `CodexConversationMigrator.exe`。
4. 在原电脑创建 `.codexproject` 或 `.codexchat`。
5. 在新电脑先点“检查（不写入）”；检查通过后完全退出 Codex，再正式导入。
6. 重新打开 Codex，并打开刚还原的项目目录，对应对话应显示在该项目下。

希望两份对话互不影响时，选“作为新对话复制”；希望以后继续导入同一来源并合并时，选“按原始编号合并”。

## 源码编译

需要 Windows 10/11、.NET SDK 8.x、.NET Framework 4.8 Targeting Pack，以及 PowerShell 5.1 或更高版本。

```powershell
.\Get-Cct.ps1
.\build.ps1
.\test.ps1 -NoBuild
.\package.ps1 -Version 3.0.0
```

只有开发脚本 `Get-Cct.ps1` 会在缺少组件时下载固定版本的 `cct` v1.2.0，并在使用前校验压缩包和 EXE 的 SHA-256；桌面软件本身不会联网下载依赖。

## 隐私与安全

- 软件本地运行，没有遥测，也没有云同步。
- 备份文件可能包含提示词、源码、命令输出、本机路径和密钥，请按敏感文件保管。
- 永久删除无法从本工具恢复。
- 导入会更新 Codex 会话文件、本地任务索引和桌面项目归属；工具会制作安全备份并验证写入，但重要数据仍建议另行备份。
- 不要把真实的备份包、JSONL 会话或 Codex 数据库上传到公开 Issue。

更多说明见 [PRIVACY.md](PRIVACY.md)、[SECURITY.md](SECURITY.md) 和 [备份格式说明](docs/BACKUP_FORMATS.md)。

## 第三方组件

发布包包含 [ahmojo/codex-claude-transfer](https://github.com/ahmojo/codex-claude-transfer) 的 `cct` v1.2.0，按 MIT 许可证使用，详见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

## 许可证

本项目使用 [MIT License](LICENSE)。

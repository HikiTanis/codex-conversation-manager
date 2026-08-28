# Codex Conversation Migrator v3.0.0

Version 3.0.0 is the first public release of the expanded local Codex conversation and project manager. It combines inspection, storage management, deletion, project-path reassociation, backup, restore, and cross-computer migration in one portable Windows application.

## 简体中文摘要

- 统一管理 Codex 项目、主对话和子代理对话，可查看时间、大小、Thread ID 与实际路径。
- 项目改名或移动后，可以重新关联原对话；也可只迁移对话，或把多个项目与全部关联对话打包迁移。
- 支持按原始编号合并与生成全新编号的独立复制，适合跨电脑往返迁移。
- 支持软件回收站、恢复、永久删除、项目目录处理、孤立子代理和旧侧边栏残留修复。
- 导入会保留并验证 Codex 的 `legacy` / `paginated` 历史模式，并在写入索引前拒绝缺少连续 `ordinal` 或 `turn_context` 的分页记录。
- 支持简体中文与英文即时切换，并统一了主要窗口、弹窗、标题栏和任务栏图标。

## Problems addressed

- **Renamed or relocated projects:** remap existing conversations to a project's new folder and update local Codex visibility data.
- **Opaque storage usage:** show project totals and the latest update time, file size, and actual path of each main and subagent conversation.
- **Large subagent histories:** manage subagents separately and remove selected old records when their stored execution context is no longer needed.
- **Flexible migration:** migrate conversations to a project copied by the user, or package project files and all linked conversations together.
- **Round-trip continuity:** continue work on a second computer, migrate back, and merge later content by original lineage within the destination project.
- **Routine cleanup:** inspect, trash, restore, or permanently delete unwanted conversations, with optional related-project processing for main conversations.

## Highlights

- Back up selected main conversations across projects as `.codexchat`.
- Back up multiple project folders, main conversations, and subagent conversations as `.codexproject`.
- Restore projects to user-selected locations while conversations remain in the normal local Codex data directory.
- Choose smart merge by original ID or independent copy with fresh Thread IDs and separate files.
- Preserve and verify each imported conversation's Codex `legacy` or `paginated` history mode; reject structurally incomplete paginated records before indexing.
- Skip invalid same-source/destination cwd mappings instead of failing an otherwise valid import.
- View main and subagent conversations separately with consistent selection and deletion controls.
- Use an app trash for recoverable deletion or permanently purge confirmed-unneeded records to reclaim storage.
- Route supported conversation deletion through Codex's official `thread/delete` interface before local cleanup; a refusal preserves the original conversation.
- Resolve parent-child relationships before deletion, stage separately recoverable descendant copies, and disclose the complete permanent-deletion impact.
- Repair exact, evidence-backed older sidebar remnants and keep orphaned subagents visible for review.
- Keep rollout files, the SQLite thread index, Codex history mode, desktop project state, and sidebar state synchronized during import, deletion, and restoration.
- Use redesigned dialogs, consistent controls, corrected title-bar buttons, a proper taskbar icon, and immediate Simplified Chinese/English switching.
- Import legacy `.codexpack` and `.codexbundle` files while creating new backups with clearer extensions.

## Download and verification

Release assets:

- `CodexConversationMigrator-Windows-v3.0.0.zip`
- `SHA256SUMS.txt`

The package is portable and requires 64-bit Windows 10/11 plus the .NET Framework 4.8 runtime. The verified `cct.exe` dependency is included. Inspection, backup, and import do not require a separately installed CLI. The full feature set, including conversation deletion and legacy-sidebar repair, requires Codex CLI 0.148.0 or later; the latest release is recommended. Run `codex --version` before using those operations. The compatibility baseline was exercised with isolated data on `0.148.0` and newer `0.150.0` builds; versions older than `0.148.0` are unsupported.

Verify the ZIP before extracting it:

```powershell
$zip = '.\CodexConversationMigrator-Windows-v3.0.0.zip'
$expected = ((Get-Content .\SHA256SUMS.txt -Raw).Trim() -split '\s+')[0]
$actual = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
if ($actual -ne $expected) { throw 'SHA-256 mismatch. Do not run this package.' }
$actual
```

The application EXE and bundled `cct.exe` are currently not Authenticode-signed, so Windows SmartScreen may show an unknown-publisher warning. Run the package only after verifying its hash and confirming that it came from the intended GitHub Release.

## Upgrade and safety notes

- Extract v3.0.0 into a new folder rather than mixing it with an older package.
- Exit Codex completely before formal import, deletion, or restoration.
- Formal `.codexchat` and `.codexproject` files are user-managed and are not deleted when their source conversations are removed.
- Moving a conversation to app trash keeps a recoverable copy inside the Codex data directory; permanently purge it to reclaim that storage.
- Project packages preserve ordinary files, empty directories, and last-write timestamps, but skip directory junctions and symbolic links and do not transfer NTFS permissions or alternate data streams.
- Paginated full-history support remains dependent on the destination Codex version. Keep the source package until imported conversations have been opened and verified. The current [Codex app-server documentation](https://developers.openai.com/codex/app-server) marks full-history reading and resumption for paginated records as not yet supported.
- Backup packages and app-trash records are not encrypted and may contain complete conversation, project, path, environment, and secret data.

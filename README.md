# Codex Conversation Migrator

[简体中文](README.zh-CN.md)

A local-first Windows desktop tool for backing up, migrating, inspecting, restoring, and deleting Codex projects together with their main and subagent conversations.

> This is an unofficial community project. It is not affiliated with or endorsed by OpenAI. Codex's local storage formats may change, so inspect a backup before importing it and keep an independent copy of important data.

## What it does

- Backs up multiple projects and all linked conversations into one `.codexproject` file.
- Backs up selected main conversations across projects into a `.codexchat` file.
- Restores project files to a folder you choose while importing conversations into the local Codex data folder on drive C.
- Supports either smart merge by source lineage or a fully independent copy with new Thread IDs.
- Separates main conversations from subagent conversations and shows file sizes and paths.
- Deletes selected conversations to the app trash or permanently, with an optional related-project action for main conversations.
- Restores or permanently purges conversations from the app trash.
- Switches immediately between Simplified Chinese and English, and remembers the choice.

## Backup formats

| Extension | Contents | Normal use |
| --- | --- | --- |
| `.codexchat` | Selected conversations only | Move or archive conversations without project files |
| `.codexproject` | One or more project folders plus linked main and subagent conversations | Move a complete working context to another computer |
| `.codexpack` / `.codexbundle` | Legacy import compatibility | Import older backups only |

`.cct-bak` is not a user backup format. It is a temporary transaction snapshot and is cleaned up after commit or rollback.

## Quick start

1. Download `CodexConversationMigrator-Windows-v3.0.0.zip` from Releases and verify its SHA-256 value.
2. Extract the whole ZIP to one folder. Keep the EXE, XAML, and `cct.exe` together.
3. Run `Start.cmd` or `CodexConversationMigrator.exe`.
4. Create a `.codexproject` or `.codexchat` backup.
5. On the destination computer, inspect the package first, exit Codex completely, and then import it.
6. Reopen Codex and open the restored project folder. Its imported conversations should appear under that project.

For a separate copy that can be deleted independently, select **Copy as new conversations**. For later incremental imports of the same source conversation, select **Merge by original ID**.

## Build from source

Requirements:

- Windows 10 or Windows 11
- .NET SDK 8.x
- .NET Framework 4.8 targeting pack
- PowerShell 5.1 or newer

```powershell
.\Get-Cct.ps1
.\build.ps1
.\test.ps1 -NoBuild
.\package.ps1 -Version 3.0.0
```

`Get-Cct.ps1` downloads the pinned upstream `cct` v1.2.0 Windows release only when it is missing, and verifies both the archive and extracted executable with SHA-256 before use. The desktop application itself does not download dependencies.

## Privacy and safety

- The application runs locally and has no telemetry or cloud-sync feature.
- Backup packages can contain prompts, source code, command output, local paths, and secrets. Treat them as sensitive files.
- Permanent deletion cannot be undone by this application.
- Import can update Codex session files, the local thread index, and desktop project association data. The app creates safety backups and validates writes, but you should still keep an external backup.
- Never attach a real `.codexchat`, `.codexproject`, `.codexpack`, `.codexbundle`, `.jsonl`, or Codex database to a public issue.

See [PRIVACY.md](PRIVACY.md), [SECURITY.md](SECURITY.md), and [docs/BACKUP_FORMATS.md](docs/BACKUP_FORMATS.md).

## Third-party component

Release packages include `cct` v1.2.0 from [ahmojo/codex-claude-transfer](https://github.com/ahmojo/codex-claude-transfer), used under the MIT License. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## License

Codex Conversation Migrator is available under the [MIT License](LICENSE).

# Codex Conversation Migrator

**Local-first management, backup, and migration for Codex conversations and projects on Windows**

[简体中文](README.zh-CN.md)

Codex Conversation Migrator is a Windows desktop application for managing Codex projects, main conversations, and subagent conversations. It handles project-path changes, storage inspection and cleanup, backup and restore, and migration between computers.

It does more than copy conversation files. The application preserves conversation lineage, remaps project paths during import, updates local task-index and desktop-project associations, and helps Codex display imported conversations under the intended project.

> This is an unofficial community project. It is not affiliated with or endorsed by OpenAI. Codex's local storage formats may change; inspect a backup before importing it and keep an independent copy of important data.

## Problems it addresses

| Scenario | Common problem | How the application helps |
| --- | --- | --- |
| A project is renamed or moved on the same computer | Existing conversations still reference the old working directory and may no longer appear under the renamed or relocated project | Remaps conversations to the new project folder and updates the local task index and desktop project association |
| Historical conversations are difficult to inspect | It is hard to see the real path, latest update time, or storage size of each project, main conversation, and subagent conversation | Groups records by project, separates main and subagent conversations, and shows time, size, and path for each record |
| Large numbers of subagents consume disk space | Subagents may retain full execution context, tool results, and terminal output and can accumulate on drive C | Supports selection, select-all, and batch deletion for subagent records, with recoverable and permanent options |
| Work must continue on another computer | Project files and Codex conversations are stored separately, so manually copying a project does not necessarily reconnect its conversations | Migrates conversations to an already copied project, or packages projects and all linked conversations together |
| Work moves back and forth between computers | Conversations continued on the second computer need to merge back without creating unmanaged duplicates | Uses original lineage plus the destination project to identify and merge later conversation content |

## Core capabilities

### Conversation and project management

- Shows each project folder, total project-file size, and file count.
- Manages main and subagent conversations separately.
- Shows each conversation's latest update time, session-file size, Thread ID, and actual path.
- Opens main or subagent conversation content in a read-only viewer.
- Provides the same select-all, clear-all, and delete-selected workflow in both views.

### Project rename, relocation, and reassociation

After a project is renamed or moved on the same computer, its conversations can be backed up as `.codexchat` and imported against the new project folder. The application rewrites recorded working-directory information and performs targeted updates to the Codex task index and desktop project association so the conversations can appear under the new location.

### Conversation-only or complete project migration

- **Conversations only:** copy the project yourself, then use `.codexchat` to import selected main conversations and associate them with the destination folder.
- **Projects and conversations together:** use `.codexproject` to package one or more project folders, empty directories, main conversations, and subagent conversations in one file.
- **Round-trip migration and merge:** move from computer A to B, continue working on B, then back up again and return to A. Merge by original ID identifies the same lineage within the destination project and merges later content.
- **Independent copies:** Copy as new conversations assigns fresh Thread IDs and separate session files. Deleting one copy does not delete another.

### Inspection, trash, and deletion

- Moves selected conversations to the app trash for later restoration or permanent deletion.
- Permanently deletes unneeded main or subagent conversations.
- Optionally processes the related project folder when deleting a main conversation.
- Moves a project folder to the Windows Recycle Bin or permanently deletes it after explicit confirmation.

> **Freeing space on drive C:** the app trash is stored inside the Codex data directory. Moving records there protects against accidental deletion but does not materially reclaim space on that drive. Permanently purge confirmed-unneeded records from the app trash to release their storage.

## Download and run (end users)

The release ZIP is portable: there is no installer, and end users do not need the .NET SDK, targeting pack, or any repository PowerShell script. A 64-bit Windows 10/11 system with the .NET Framework 4.8 runtime is required.

1. Download `CodexConversationMigrator-Windows-v3.0.0.zip` and `SHA256SUMS.txt` from Releases.
2. Extract the entire ZIP into a new folder. Do not run the application from inside the ZIP, and keep the EXE, XAML, and `cct.exe` together.
3. Double-click `Start.cmd`; `CodexConversationMigrator.exe` can also be started directly.
4. When upgrading, extract the new version into a separate folder instead of mixing it with files from an older release.

## Basic usage

### Create a backup

1. Open **Back up conversations** and choose **Projects + conversations** or **Conversations only**.
2. Select the projects or main conversations to include. Selections can span multiple projects.
3. Choose the destination folder and create the `.codexproject` or `.codexchat` package.

### Restore or migrate

1. Open **Import backup** and select the backup package.
2. For `.codexchat`, select each project's actual folder. For `.codexproject`, select where project folders should be restored.
3. Use **Merge by original ID** to continue the same lineage, or **Copy as new conversations** for independent files and Thread IDs.
4. Run **Inspect first (no writes)**. After it passes, exit Codex completely and start the import.
5. Reopen Codex and the destination project, then confirm that the conversations appear under that project.

### Inspect and clean up

1. Select a project, then switch between **Main conversations** and **Subagent conversations**.
2. Review the latest update time, size, Thread ID, path, or read-only conversation content.
3. Select individual records or use Select all, then choose **Delete selected**.
4. Use the app trash when recovery may be needed; permanently purge confirmed-unneeded records when storage must be reclaimed.

## Typical workflows

### 1. Rename or move a project on the same computer

1. Select the conversations to retain and create a `.codexchat` backup.
2. On the import page, choose the project's new folder and keep conversation-to-project linking enabled.
3. Run the read-only inspection. If it passes, exit Codex completely and perform the import.
4. Reopen Codex and the relocated project, then confirm that its conversations appear under the project.

### 2. Copy the project manually and migrate conversations only

1. Create a `.codexchat` backup on the source computer.
2. Copy the project folder and `.codexchat` file to the destination computer.
3. Import the backup and select the project's actual destination folder.

### 3. Move projects and conversations in one package

1. Select one or more projects and create a `.codexproject` backup.
2. Copy that single package to the destination computer.
3. Select the restoration location, inspect the package, and then restore project files and linked conversations.

### 4. Continue on another computer and merge back

1. Create a new backup for the same project on the second computer.
2. Return the backup to the first computer and select the original project folder.
3. Choose Merge by original ID. Matching is limited to the destination project, and later conversation content is merged into the same lineage.

## Backup formats

| Extension | Contents | Recommended use |
| --- | --- | --- |
| `.codexchat` | Selected main conversations without project files | Manually copied projects, renamed or relocated projects, and conversation-only archives |
| `.codexproject` | One or more project folders plus linked main and subagent conversations | Complete project-workspace migration |
| `.codexpack` / `.codexbundle` | Legacy compatibility formats | Importing older backups only |

## Build from source (developers)

Requirements:

- Windows 10 or Windows 11
- .NET SDK 8.x
- .NET Framework 4.8 targeting pack
- PowerShell 5.1 or newer

From the repository root, one command downloads any missing pinned component, builds the application, runs the tests, and creates the release ZIP:

```powershell
.\package.ps1 -Version 3.0.0
```

`package.ps1` calls the build and test scripts automatically. If `cct.exe` is missing, the build invokes `Get-Cct.ps1`, downloads the pinned upstream `cct` v1.2.0 release, and verifies both the archive and executable with SHA-256. The finished ZIP is written to `release/` together with `SHA256SUMS.txt`. The desktop application itself does not download build dependencies.

## Privacy and safety

- The application runs locally and has no telemetry or cloud-sync feature.
- Backup packages can contain prompts, source code, command output, local paths, and secrets. Treat them as sensitive files.
- Permanent deletion cannot be undone by this application. Confirm conversation and project paths before proceeding.
- Import updates Codex session files, the local task index, and desktop project association data. The application creates safety backups and validates writes, but important data should still have an independent backup.
- Never attach a real backup package, session JSONL, or Codex database to a public issue.

See [PRIVACY.md](PRIVACY.md), [SECURITY.md](SECURITY.md), and [docs/BACKUP_FORMATS.md](docs/BACKUP_FORMATS.md).

## Third-party component and license

Release packages include `cct` v1.2.0 from [ahmojo/codex-claude-transfer](https://github.com/ahmojo/codex-claude-transfer), used under the MIT License. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Codex Conversation Migrator is available under the [MIT License](LICENSE).

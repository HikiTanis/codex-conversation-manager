# Codex Conversation Migrator

**Local-first management, backup, and migration for Codex conversations and projects on Windows**

[简体中文](README.zh-CN.md)

Codex Conversation Migrator is a Windows desktop application for managing Codex projects, main conversations, and subagent conversations. It handles project-path changes, storage inspection and cleanup, backup and restore, and migration between computers.

It does more than copy conversation files. The application preserves conversation lineage, remaps project paths during import, updates local task-index and desktop-project associations, and helps Codex display imported conversations under the intended project.

> This is an unofficial community project. It is not affiliated with or endorsed by OpenAI. Codex's local storage formats may change; inspect a backup before importing it and keep an independent copy of important data.

## Choose the right workflow

| Goal | Use | Import identity mode |
| --- | --- | --- |
| Rename or move a project on the same computer | Create a `.codexchat` backup, then map it to the new folder | **Merge by original ID** |
| Copy the project folder yourself to another computer | Move the folder and a `.codexchat` backup separately | Merge to continue the same lineage, or copy for an independent duplicate |
| Move project files and conversations together | Create one `.codexproject` containing one or more projects | Merge or independent copy |
| Keep a completely separate conversation copy | Use either formal backup type | **Copy as new conversations** |
| Review or reclaim local storage | Use the Main conversations, Subagents, and app-trash views | Move to app trash first, or permanently delete after review |

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
- **Projects and conversations together:** use `.codexproject` to package one or more project folders, ordinary files, empty directories, main conversations, and subagent conversations in one file. Directory junctions, symbolic links, NTFS permissions, and alternate data streams are not transferred.
- **Round-trip migration and merge:** move from computer A to B, continue working on B, then back up again and return to A. Merge by original ID identifies the same lineage within the destination project and merges later content.
- **Independent copies:** Copy as new conversations assigns fresh Thread IDs and separate session files. Deleting one copy does not delete another.
- **Codex history modes:** import preserves and verifies each conversation's `legacy` or `paginated` mode; paginated records are also checked for continuous `ordinal` values and `turn_context`. Full paginated-history support still depends on the destination Codex version, so keep the source backup and open imported tasks to verify them. The current [Codex app-server documentation](https://developers.openai.com/codex/app-server) marks paginated history as experimental and says full-history reading and resumption are not yet supported.

### Inspection, trash, and deletion

- Moves selected conversations to the app trash for later restoration or permanent deletion.
- Calls Codex's official `thread/delete` interface before touching local conversation data, then updates the session file, local task indexes, desktop project state, and matching sidebar records only after official deletion succeeds. If the official interface is unavailable, the original conversation is preserved.
- Codex deletes spawned descendant threads together with their parent. The application resolves the full parent-child graph first: app-trash deletion stages a separate recoverable copy for every affected descendant, while permanent deletion shows the complete impact count before confirmation.
- Re-registers the thread index and desktop project association when a conversation is restored from the app trash.
- Permanently deletes unneeded main or subagent conversations.
- Optionally processes the related project folder when deleting a main conversation.
- Moves a project folder to the Windows Recycle Bin or permanently deletes it after explicit confirmation.
- Repairs both index orphans and older partial deletions where the file and current index row are already gone but Codex still shows the task. Legacy candidates must be confirmed by both a recent Codex `rollout_not_found` log and the latest pre-deletion index backup before they are offered for review.
- Shows surviving descendants whose parent conversation is gone in an **Orphaned subagents** project instead of hiding the project. During stale-parent repair, safe records are handled first; the application then switches to the correct project, opens the Subagents view, searches the exact Thread ID, and selects the blocking descendant. Move it to app trash and refresh again to finish the parent cleanup.
- Repairs already-deleted tasks that still appear in the Codex desktop sidebar or a cached task list. Cleanup runs only when Codex is fully closed and only for exact, previously confirmed deleted Thread IDs; it does not clear sign-in data, valid tasks, or project files.

> **Freeing space on drive C:** the app trash is stored inside the Codex data directory. Moving records there protects against accidental deletion but does not materially reclaim space on that drive. Permanently purge confirmed-unneeded records from the app trash to release their storage.

> **Before a formal import, deletion, or restoration:** exit Codex completely so the running desktop app cannot overwrite updated session, index, or sidebar state.

## Download and run (end users)

The release ZIP is portable: there is no installer, and end users do not need the .NET SDK, targeting pack, or repository PowerShell scripts. A 64-bit Windows 10/11 system with the [.NET Framework 4.8 runtime](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48) is required.

Inspection, backup, and import work directly from the release package because the verified `cct.exe` dependency is included. **The full feature set, including conversation deletion and legacy-sidebar repair, requires Codex CLI 0.148.0 or later; the latest release is recommended.** This minimum does not apply to inspection, backup, or import. The application checks common npm installation paths, `PATH`, its own directory, and versioned Codex/VS Code extension runtimes, then prefers the newer discovered version. Run `codex --version` in PowerShell first. If it reports a version older than `0.148.0`, or if the command is unavailable, update or install the CLI using the [official Codex CLI documentation](https://developers.openai.com/codex/cli), or install the npm package:

```powershell
npm install -g @openai/codex
codex --version
```

The compatibility baseline was exercised with isolated data on `0.148.0` and newer `0.150.0` builds; versions older than `0.148.0` are unsupported. If a compatible CLI cannot be found or official deletion is rejected, the application stops and preserves the original conversation.

1. Download `CodexConversationMigrator-Windows-v3.0.0.zip` and `SHA256SUMS.txt` from Releases.
2. Verify the ZIP before extracting it:

   ```powershell
   $zip = '.\CodexConversationMigrator-Windows-v3.0.0.zip'
   $expected = ((Get-Content .\SHA256SUMS.txt -Raw).Trim() -split '\s+')[0]
   $actual = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
   if ($actual -ne $expected) { throw 'SHA-256 mismatch. Do not run this package.' }
   $actual
   ```

3. Extract the entire ZIP into a new folder. Do not run the application from inside the ZIP, and keep the EXE, XAML, and `cct.exe` together.
4. Double-click `Start.cmd`; `CodexConversationMigrator.exe` can also be started directly.
5. When upgrading, extract the new version into a separate folder instead of mixing it with files from an older release.

The application EXE and bundled `cct.exe` are currently not Authenticode-signed, so Windows SmartScreen may show an unknown-publisher warning. Verify the downloaded ZIP against `SHA256SUMS.txt` and run it only if you trust the release source. A matching hash detects a changed download; it does not replace publisher identity verification.

## Basic usage

### Create a backup

1. Open **Manage and Back Up** and choose **Projects + conversations** or **Conversations only**.
2. Select the projects or main conversations to include. Selections can span multiple projects.
3. Choose the destination folder and create the `.codexproject` or `.codexchat` package.

### Restore or migrate

1. Open **Import and Restore** and select the backup package.
2. For `.codexchat`, select each project's actual folder. For `.codexproject`, select where project folders should be restored.
3. Use **Merge by original ID** to continue the same lineage, or **Copy as new conversations** for independent files and Thread IDs.
4. Run **Inspect first (no writes)**. After it passes, exit Codex completely and start the import.
5. Reopen Codex and the destination project, then confirm that the conversations appear under that project.

### Inspect and clean up

1. Select a project, then switch between **Main conversations** and **Subagent conversations**.
2. Review the latest update time, size, Thread ID, path, or read-only conversation content.
3. Select individual records or use Select all, then choose **Delete selected**. If a main conversation has spawned descendants, the confirmation shows the additional impact count.
4. Exit Codex before confirming deletion or restoration. If an older version left a broken sidebar item, open it once in Codex so the failure is recorded, exit Codex completely, then open this application and click **Refresh / Repair**. Review the title and Thread ID before confirming repair. If the conversation is already gone but its sidebar item remains, Refresh completes the matching sidebar cleanup before reporting that Codex can be reopened.
5. Use the app trash when recovery may be needed; permanently purge confirmed-unneeded records when storage must be reclaimed.

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

`VERSION` is the authoritative release version. From the repository root, one command validates that version, downloads any missing pinned component, builds the application, runs the functional and render tests, removes older local release ZIPs, and creates the current package:

```powershell
.\package.ps1
```

Passing `-Version` is optional and acts as an assertion; it must match `VERSION`:

```powershell
.\package.ps1 -Version 3.0.0
```

`package.ps1` invokes the build, test, and release-verification scripts automatically. If `cct.exe` is missing, the build invokes `Get-Cct.ps1`, downloads pinned upstream `cct` v1.2.0, and verifies both the archive and executable with SHA-256. The deterministic ZIP and `SHA256SUMS.txt` are written to `release/`. The desktop application itself does not download build dependencies.

See [docs/RELEASING.md](docs/RELEASING.md) for the maintainer checklist.

## Privacy and safety

- The application runs locally and has no telemetry, cloud-sync, account, or automatic-update feature.
- Formal backups and app-trash entries are not encrypted. They can contain prompts, source code, command output, local paths, environment details, and secrets; treat them as sensitive files.
- Project packages contain ordinary files and empty directories but skip directory junctions and symbolic links. NTFS permissions and alternate data streams are not transferred.
- Permanent deletion cannot be undone by this application. Confirm conversation and project paths before proceeding.
- Import updates Codex session files, the local task index, and desktop project-association data. The application validates writes, but important data should still have an independent backup.
- Never attach a real backup package, session JSONL, Codex database, or unredacted screenshot to a public issue.

See [PRIVACY.md](PRIVACY.md), [SECURITY.md](SECURITY.md), [backup-format details](docs/BACKUP_FORMATS.md), [v3.0.0 release notes](docs/RELEASE_NOTES_v3.0.0.md), [CHANGELOG.md](CHANGELOG.md), and [SUPPORT.md](SUPPORT.md).

## Third-party component and license

Release packages include `cct` v1.2.0 from [ahmojo/codex-claude-transfer](https://github.com/ahmojo/codex-claude-transfer), used under the MIT License. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Codex Conversation Migrator is available under the [MIT License](LICENSE).

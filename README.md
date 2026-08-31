# Codex Conversation Migrator

**Manage, back up, and migrate local Codex conversations and projects on Windows.**

[简体中文](README.zh-CN.md) · Current release: v1.0.0

The application keeps Codex projects, main conversations, and subagent conversations organized in one place. It reconnects conversations after a project is renamed or moved, explains local storage use, migrates work between computers, and helps avoid or repair stale sidebar entries during cleanup.

> This is an unofficial community project. It is not affiliated with or endorsed by OpenAI. Codex local-storage formats may change; keep an independent backup of important data.

## Problems it solves

- **A project moved and its conversations disappeared:** reassociate existing conversations with a renamed, relocated, or copied project folder.
- **Local storage is hard to understand:** separate main and subagent conversations by project, with project totals and each conversation's time, size, Thread ID, and actual path.
- **Subagents have accumulated:** search, select, select all, and batch-delete main or subagent conversations; after selecting every main conversation in a project, the same operation can also process its folder.
- **Projects and conversations are difficult to move together:** transfer selected conversations alone, or package one or more projects with all linked conversations.
- **Round trips create duplicates or shared files:** merge by original identity, or create fresh Thread IDs and fully independent session files.

## Core capabilities

| Area | What it does |
| --- | --- |
| Conversation and project management | Shows each project folder, file count, and total size; separates main and subagent conversations; fully displays long conversations and opens at the latest message; the user-message rail supports hover previews, click/drag navigation, and a viewer that follows the main window size |
| Backup and migration | Creates `.codexchat` from selected main conversations across projects, or `.codexproject` from one or more projects with linked main and subagent conversations |
| Project reassociation | Maps a conversation's recorded project path to a new folder and updates the local task index and desktop-project association |
| Two import identities | **Smart merge** continues the same lineage; **Independent copy** creates fresh Thread IDs and session files, so deleting one copy cannot delete the other |
| Cleanup and recovery | Batch-moves records to app trash, restores them, or permanently deletes them; selecting every main conversation in a project enables optional project-folder handling and evidence-backed stale-sidebar repair |
| Chinese and English UI | Switches between Simplified Chinese and English inside the application |

Inspection, backup, dry-run validation, and import are built into the application. They require neither a separate transfer utility nor Codex CLI. **Only conversation deletion (app trash or permanent deletion) and stale-sidebar repair require Codex CLI 0.148.0 or later; the latest version is recommended.** Sending or deleting a project folder through Windows does not itself require the CLI.

## Download and run

End-user requirements:

- 64-bit Windows 10 or Windows 11;
- [.NET Framework 4.8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48);
- for deletion or sidebar repair only, a compatible locally available [Codex CLI](https://developers.openai.com/codex/cli) 0.148.0 or later. Run `codex --version` to confirm it; the application can also discover compatible runtimes bundled with Codex Desktop or the VS Code extension. Version 0.148.0 is this project's compatibility baseline, not an official OpenAI minimum.

The application is portable and does not need an installer:

1. Download `CodexConversationMigrator-Windows-v1.0.0.zip` and `SHA256SUMS.txt` from GitHub Releases.
2. Compare the release checksum:

   ```powershell
   $zip = '.\CodexConversationMigrator-Windows-v1.0.0.zip'
   (Get-FileHash $zip -Algorithm SHA256).Hash
   Get-Content .\SHA256SUMS.txt
   ```

   The two SHA-256 values must match.
3. Extract the complete ZIP into a new folder. Do not run it from inside the archive or mix files from different versions.
4. Double-click `Start.cmd`, or run `CodexConversationMigrator.exe`.

The executable is not currently Authenticode-signed, so Windows may show an unknown-publisher warning. Download only from a release source you trust and verify the SHA-256 first.

## Three common workflows

### 1. Rename or move a project on the same computer

1. Create a `.codexchat` for the conversations associated with the original project. If the folder has already moved, you can still scan and back up the old conversations while their session files remain; do not delete them first.
2. After renaming or moving the project, import the backup and select the new project folder.
3. Run the read-only inspection. If it passes, exit Codex completely and import with **Smart merge**.
4. Reopen Codex and the relocated project, then confirm that its conversations appear under the project.

### 2. Copy a project yourself and migrate conversations only

1. Select main conversations across one or more projects and create a `.codexchat` on the source computer.
2. Copy the project folders and the backup file separately to the destination computer.
3. Import the backup and map every source project to its actual destination folder. Use **Smart merge** to continue the same lineage, or **Independent copy** for a separate copy.

### 3. Move projects and conversations together

1. Select one or more projects and create a `.codexproject`.
2. On the destination computer, choose where to restore each project, inspect the package, and then import the project files and all linked conversations.
3. To bring later work back to the first computer, create another backup and use **Smart merge**. Matching is limited to the selected destination project.

> Exit Codex completely before import, deletion, restoration, or sidebar repair so the running client cannot overwrite local state. Keep the source package until every imported conversation has been opened and verified in the destination Codex client.

## Backup formats

| Extension | Contents | Use it for |
| --- | --- | --- |
| `.codexchat` | Selected main conversations across projects; no project files or subagents | Renamed or moved projects, manually copied projects, and conversation-only archives or transfers |
| `.codexproject` | One or more project folders plus all linked main and subagent conversations | Complete project-and-conversation backup or migration |
| `.codexpack` / `.codexbundle` | Backups from older releases | Legacy import only; new packages are not created in these formats |

App trash is not a formal backup. It lives at `<CODEX_HOME>\conversation-migrator-trash` and exists for recovery from accidental deletion. Moving a session there usually does not reclaim space on drive C; permanently purge confirmed-unneeded entries to release that space.

See `docs/BACKUP_FORMATS.md` in the source repository for package structure, conflict handling, validation, and resource limits.

## Compatibility and limitations

- `.jsonl.zst` sessions currently expose index time, size, and path only. Their content cannot be previewed, included in a formal backup, or imported; keep the original compressed file.
- `.codexproject` preserves ordinary files, empty directories, and modification times. It does not transfer junctions, symbolic links, NTFS permissions, or alternate data streams.
- Project restore can require an empty destination, keep existing same-name files, or create a recovery ZIP before overwriting. Review the destination and available disk space first.
- Import validates paths, Thread IDs, checksums, archive structure, and resource boundaries, and uses transaction snapshots for rollback. It cannot guarantee compatibility with an unknown future Codex storage format.
- For `paginated` history, import stops when complete-history, turn-pagination, or resume conditions cannot be validated safely. Still open every imported conversation in the destination Codex client to verify it.

## Privacy and safety

- The application runs locally and has no telemetry, cloud sync, account system, or automatic updater.
- Formal packages, recovery ZIPs, and app-trash entries are not encrypted. They may contain prompts, responses, source code, command output, local paths, and secrets; handle them as sensitive files.
- Permanent deletion cannot be undone by this application. Verify the full path before deleting a project folder.
- Formal packages remain user-managed files. Deleting a source conversation does not delete an existing `.codexchat` or `.codexproject`.
- Never upload a real backup, session JSONL, Codex database, or unredacted screenshot to a public issue.

For the complete privacy and security boundaries, see `docs/PRIVACY.md` and `.github/SECURITY.md` in the source repository.

## Development and documentation

Source builds require Windows 10/11, .NET SDK 8.x, and PowerShell 5.1 or later. A separate .NET Framework 4.8 Targeting Pack is not required. From the repository root, run:

```powershell
.\scripts\package.ps1
```

The script restores pinned dependencies, builds the application, runs Chinese and English functional and UI tests, validates the release candidate, and writes the ZIP plus `SHA256SUMS.txt` to `release/`. `VERSION` is the single source of truth for the release version.

The source repository also contains `docs/releases/v1.0.0.md` (release notes), `CHANGELOG.md` (version history), `docs/RELEASING.md` (maintainer workflow), `.github/SUPPORT.md` (issue reporting), and `.github/CONTRIBUTING.md` (contributor guide).

Licensed under the [MIT License](LICENSE).

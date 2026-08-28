# Privacy

Codex Conversation Migrator is local-first and has no telemetry, analytics, advertising, account system, cloud upload, automatic update, or background synchronization feature.

## Local data the application can read

Depending on the selected operation, the application can read:

- Codex session JSONL files and their metadata;
- the local Codex SQLite task index and subagent relationships;
- Codex desktop project/task state and exact sidebar cache records;
- local Codex error logs used to confirm an older partial-deletion candidate;
- project files selected for a `.codexproject` backup.

The application invokes the bundled `cct.exe` for defined local inspection, export, dry-run, and import operations. Conversation deletion and supported legacy-sidebar repair invoke an independently installed Codex CLI through its local app-server protocol. These executables are not used to submit prompts or start model work.

`Get-Cct.ps1` is a developer helper that downloads one pinned third-party build from GitHub when the dependency is missing and verifies its archive and EXE hashes. The desktop application does not perform that download.

## Local data the application can write

- Formal `.codexchat` and `.codexproject` packages are written only to a folder selected by the user.
- App-trash records are stored inside the active Codex data directory and remain there until restored or permanently purged.
- Import and restoration can update session files, task indexes, and desktop project-association data.
- Overwrite-mode project restoration can create a recovery ZIP inside the active Codex data directory.
- A selected project folder can be sent to the Windows Recycle Bin or permanently deleted only after explicit confirmation.

## Sensitive content

Formal backups, recovery ZIPs, and app-trash entries are not encrypted. They may contain complete prompts, responses, tool output, local paths, project source files, environment details, credentials, and secrets. Anyone who can read one of these files may be able to read that content.

Store packages securely, encrypt them before using an untrusted transfer channel, and permanently remove copies that are no longer needed. Never attach a real Codex home, backup package, session JSONL, SQLite database, project payload, access token, or unredacted screenshot to a public issue.

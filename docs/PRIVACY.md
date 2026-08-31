# Privacy

Codex Conversation Migrator is local-first and has no telemetry, analytics, advertising, account system, cloud upload, automatic update, or background synchronization feature.

## Local data the application can read

Depending on the selected operation, the application can read:

- Codex session JSONL files and their metadata;
- the local Codex SQLite task index and subagent relationships;
- Codex desktop project/task state and exact sidebar cache records;
- local Codex error logs used to confirm an older partial-deletion candidate;
- project files selected for a `.codexproject` backup.

Inspection, backup, dry-run, and import are performed by the application's built-in native engine. The release package does not bundle or download a third-party migration executable. Conversation deletion and supported legacy-sidebar repair use a compatible locally available Codex CLI executable through its app-server protocol. Codex CLI 0.148.0 or later is this project's supported compatibility baseline, and the latest release is recommended; the application can also discover compatible runtimes bundled with Codex Desktop or the VS Code extension. The CLI is not required for inspection, backup, or import, and the application does not use it to submit prompts or start model work.

## Local data the application can write

- Formal `.codexchat` and `.codexproject` packages are written only to a folder selected by the user.
- App-trash records are stored inside the active Codex data directory and remain there until restored or permanently purged.
- Import and restoration can update session files, task indexes, and desktop project-association data.
- Before risky index or desktop-state writes, local safety copies can be stored under the active Codex data directory. They may contain task titles, Thread IDs, project paths, and index metadata; they are not formal conversation backups or app-trash records.
- Overwrite-mode project restoration can create a recovery ZIP inside the active Codex data directory.
- A selected project folder can be sent to the Windows Recycle Bin or permanently deleted only after explicit confirmation.

Compressed `.jsonl.zst` sessions are currently read only at the index-metadata level. Their conversation contents are not previewed, formally backed up, or imported.

## Sensitive content

Formal backups, recovery ZIPs, and app-trash entries are not encrypted. They may contain complete prompts, responses, tool output, local paths, project source files, environment details, credentials, and secrets. Anyone who can read one of these files may be able to read that content.

Store packages securely, encrypt them before using an untrusted transfer channel, and permanently remove copies that are no longer needed. Never attach a real Codex home, backup package, session JSONL, SQLite database, project payload, access token, or unredacted screenshot to a public issue.

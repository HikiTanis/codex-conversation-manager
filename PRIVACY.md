# Privacy

Codex Conversation Migrator is local-first.

- The desktop application contains no telemetry, analytics, advertising, account system, cloud upload, or automatic update service.
- It reads local Codex session files and index data only to show, back up, migrate, restore, or delete the items selected by the user.
- It writes formal backup packages only to a user-selected backup folder.
- `Get-Cct.ps1` is a developer helper that downloads a pinned third-party build from GitHub when missing. The desktop application does not perform that download.

Backup packages and app-trash entries may contain complete prompts, responses, tool output, local paths, project source files, environment details, and secrets. Anyone who can read a package may be able to read that content. Store it securely, encrypt it before using an untrusted transfer channel, and permanently remove copies you no longer need.

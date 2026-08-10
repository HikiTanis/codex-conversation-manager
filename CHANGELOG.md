# Changelog

All notable changes are documented here. The project follows semantic versioning for public releases.

## [3.0.0] - 2026-08-10

### Added

- Unified local management for Codex projects, main conversations, and subagent conversations.
- Project-path reassociation for projects renamed or moved on the same computer.
- Per-project storage totals and per-conversation latest update time, file size, Thread ID, and actual path.
- Separate main and subagent views with consistent select-all, clear-all, and delete-selected controls.
- Read-only viewing of main and subagent conversation content.
- Multi-project `.codexproject` backups containing project folders, empty directories, main conversations, and subagent conversations.
- Cross-project `.codexchat` backups containing selected main conversations without project files.
- Smart merge by original lineage within the destination project, supporting later imports and round-trip migration between computers.
- Independent-copy mode with fresh Thread IDs and separate conversation files.
- Project restoration to user-selected folders while conversations remain in the local Codex data directory.
- App trash, restoration, permanent conversation deletion, and optional related-project processing.
- Project-folder actions using the Windows Recycle Bin or explicit permanent deletion.
- Immediate Simplified Chinese and English switching with a remembered preference.
- Reproducible build, test, packaging, release checksums, CI, and public-project documentation.

### Changed since 2.2.0

- Repositioned the application from a conversation mover to a conversation-and-project management, backup, and migration tool.
- Reorganized the interface around clear backup, restore, inspection, and management workflows.
- Replaced ambiguous new backup files with `.codexchat` and `.codexproject`; legacy formats remain import-compatible.
- Improved import responsiveness, same-path mapping, targeted Codex index registration, and desktop project association.
- Ensured independently copied conversations use separate files and IDs, so deleting one copy does not remove another.
- Unified destructive-action dialogs and made recoverable versus permanent operations explicit.
- Separated subagent records from main conversations so large historical execution contexts can be reviewed and cleaned selectively.

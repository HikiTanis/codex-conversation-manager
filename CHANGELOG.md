# Changelog

All notable changes are documented here. The project follows semantic versioning for public releases.

## [3.0.0] - 2026-08-07

### Added

- Immediate Simplified Chinese and English switching with a remembered preference.
- Multi-project `.codexproject` backup containing project folders, main conversations, and subagent conversations.
- Conversation-only `.codexchat` backup across projects.
- Smart merge by source lineage and independent copy with fresh Thread IDs.
- Project restoration to user-selected folders while conversations remain in the local Codex data folder.
- Separate main and subagent views with per-project and per-conversation sizes and paths.
- Consistent select-all, clear-all, and delete-selected controls for both conversation types.
- App trash, restore, permanent deletion, and optional related-project processing.
- Localized custom dialogs, title bars, taskbar icon, and consistent control styling.
- Reproducible build, test, packaging, release checksums, CI, and public project documentation.

### Changed since 2.2.0

- Reorganized the workflow around two clear tasks: create a backup and restore a backup.
- Replaced ambiguous new backups with `.codexchat` and `.codexproject`; legacy formats remain import-only.
- Improved import responsiveness, same-path mapping handling, targeted Codex index registration, and desktop project association.
- Ensured independently copied conversations use separate files and IDs, so deleting one copy does not remove another.
- Unified destructive-action controls and made permanent actions explicit.

### Removed

- `.cct-bak` as a user-visible backup mode. It remains only as an automatically cleaned transaction snapshot.

# Changelog

All notable changes are documented here. The project follows semantic versioning for public releases.

## [Unreleased]

No unreleased changes are documented yet.

## [3.0.0] - 2026-08-28

First public release of the expanded conversation-and-project management application.

### Added

- Unified local management for Codex projects, main conversations, and subagent conversations.
- Project-path reassociation for projects renamed or moved on the same computer.
- Per-project storage totals and per-conversation latest update time, file size, Thread ID, and actual path.
- Separate main and subagent views with consistent select-all, clear-all, and delete-selected controls.
- Read-only viewing of main and subagent conversation content.
- Multi-project `.codexproject` backups containing ordinary project files, empty directories, main conversations, and subagent conversations.
- Cross-project `.codexchat` backups containing selected main conversations without project files.
- Smart merge by original lineage within the destination project, supporting later imports and round-trip migration between computers.
- Independent-copy mode with fresh Thread IDs and separate conversation files.
- Project restoration to user-selected folders while conversations remain in the local Codex data directory.
- App trash, restoration, permanent conversation deletion, and optional related-project processing.
- Project-folder actions using the Windows Recycle Bin or explicit permanent deletion.
- Immediate Simplified Chinese and English switching with a remembered preference.
- Transactional conversation deletion across rollout files, the SQLite thread index, subagent edges, and Codex desktop thread state.
- Thread-index and desktop-project re-registration when restoring a conversation from the app trash.
- Detection and guided cleanup of orphaned subagents and older partial-deletion sidebar records.
- A single authoritative `VERSION` file, deterministic release ZIP creation, version/package verification, checksums, CI, and tag-driven GitHub Releases.
- Automated Chinese and English functional, window-chrome, selection, import-layout, backup-layout, and dialog-theme render tests.

### Changed since 2.2.0

- Repositioned the application from a conversation mover to a conversation-and-project management, backup, and migration tool.
- Reorganized the interface around backup, restore, inspection, and management workflows.
- Replaced ambiguous new backup files with `.codexchat` and `.codexproject`; legacy formats remain import-compatible.
- Improved import responsiveness, same-path handling, targeted Codex index registration, desktop project association, and failure rollback for both current and legacy package flows.
- Ensured independently copied conversations use separate files and IDs, so deleting one copy does not remove another.
- Unified destructive-action dialogs and made recoverable versus permanent operations explicit.
- Separated subagent records from main conversations so large historical execution contexts can be reviewed and cleaned selectively.
- Routed supported deletion and stale-sidebar repair through Codex's official `thread/delete` interface before local cleanup.
- Preserved and verified Codex `legacy` and `paginated` history modes during import, including continuous ordinal and turn-context checks for paginated records.

### Fixed

- Same-source and destination project mappings no longer pass an invalid identical `--map-cwd` argument.
- Corrected `history_mode` during targeted index updates and reject structurally incomplete paginated histories before indexing; full-history support remains dependent on the destination Codex version.
- Independently copied conversations no longer share Thread IDs or session files with their source.
- Main-conversation deletion now discloses and safely handles descendant subagents before Codex applies its cascade.
- Previously deleted conversations can be removed from stale desktop sidebar caches using exact, evidence-backed Thread IDs.
- Window title bars, dialog controls, taskbar icon behavior, and clipped import controls were made consistent.

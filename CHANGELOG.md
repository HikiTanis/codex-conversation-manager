# Changelog

All notable changes are documented here. The project follows semantic versioning for public releases.

## [Unreleased]

No unreleased changes are documented yet.

## [1.0.2] - 2026-09-01

### Fixed

- Made the GitHub Release workflow detect an absent release without treating the expected lookup result as a fatal PowerShell error.
- Made reruns safely edit an existing release and replace its assets, while first-time runs create the release normally.

## [1.0.1] - 2026-09-01

### Changed

- Renamed the product to **Codex Conversation Manager** and the Chinese display name to **Codex 对话与项目管理器**, reflecting its conversation and project management scope.
- Renamed the solution, source project, executable, portable ZIP, and GitHub-facing release artifacts consistently.
- Preserved access to existing app trash, transaction metadata, lineage markers, and the saved language preference from the former application data directory.

## [1.0.0] - 2026-08-31

### Initial release

- Unified local management for Codex projects, main conversations, and subagent conversations, including time, size, Thread ID, actual path, and per-project storage totals.
- Project-path reassociation after a project is renamed, moved, or copied.
- Multi-conversation `.codexchat` backups and multi-project `.codexproject` backups containing project files plus linked conversations.
- Smart merge by original lineage and independent-copy import with fresh Thread IDs and separate conversation files.
- Project restoration to user-selected folders while conversations remain in the destination Codex data directory.
- App trash, restoration, permanent deletion, optional related-project handling, and evidence-backed stale-sidebar repair.
- Simplified Chinese and English interfaces with consistent main/subagent selection and deletion controls.

### Changed

- Replaced the external conversation-transfer runtime with a built-in native engine for session inspection, formal backup, and import.
- Reduced the portable release package and removed the build-time download and runtime bundling of a separate transfer executable.
- Kept Codex CLI integration only for conversation deletion and stale-sidebar repair. Those operations require Codex CLI 0.148.0 or later; the latest release is recommended.
- Standardized new formal backups on `.codexchat` and `.codexproject`; `.codexpack` and `.codexbundle` remain legacy import-only formats.
- Updated source builds to restore pinned .NET Framework 4.8 reference assemblies from NuGet, so developers need Windows, .NET SDK 8.x, and PowerShell 5.1 or newer but not a separately installed Targeting Pack.
- Renamed current import safety snapshots and maintenance paths to application-owned native transaction terminology while retaining one-version cleanup compatibility for orphaned legacy snapshots.

### Improved

- Backups now read every rollout from one stable temporary snapshot, validate embedded IDs, hashes, and duplicate sources, and replace an existing output package only after successful completion.
- Imports validate the manifest, checksums, source working directory, archive paths, expanded size, compression ratio, duplicate entries, and available temporary storage before modifying Codex data.
- Nested project payloads now use the same bounded validation and counted extraction model, and project-file overwrites use atomic replacement.
- Smart merge now compares normalized metadata and lineage within the destination project, avoiding false divergence caused only by rewritten working-directory JSON formatting.
- Independent-copy import preserves parent-subagent relationships while assigning fresh Thread IDs and separate session files.
- Session discovery now combines regular and archived session files with a defensive SQLite index overlay, ignores reparse points, and refuses mismatched path-index records.
- Scan, backup, and import work run off the UI thread to keep the desktop interface responsive during large operations.

### Fixed

- Normalized legacy rollout filenames whose filename GUID did not match the embedded Thread ID, preventing Codex's “rollout path does not match thread id” error after import.
- Prevented bundle metadata from authorizing a source-project mapping when the actual embedded conversation working directory does not match it.
- Preserved valid conversation previews when optional metadata payloads are missing and recognized subagent sources and parent relations consistently.
- Rejected unsupported compressed rollouts instead of producing a formal backup that could not be restored.

### Known limitation

- `.jsonl.zst` sessions can currently be listed from index metadata, including path and size, but compressed content cannot yet be previewed, formally backed up, or imported.

## Internal pre-release milestone

Earlier internal development builds were not public releases. Their completed work is included in the 1.0.0 entry above.

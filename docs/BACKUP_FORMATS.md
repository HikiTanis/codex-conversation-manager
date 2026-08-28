# Backup formats and import behavior

The application distinguishes user-created backup packages from the app trash. They serve different purposes and should not be managed as interchangeable backups.

## Storage types at a glance

| Type | User-managed | Project files | Main conversations | Subagent conversations | Purpose |
| --- | --- | --- | --- | --- | --- |
| `.codexchat` | Yes | No | Selected records | No | Conversation-only migration or archive |
| `.codexproject` | Yes | Yes | All linked records in selected projects | Yes | Complete project-workspace migration |
| App trash | Through the application | No | Selected/affected records | Selected/affected records | Recoverable local removal |

Moving a conversation to the app trash is not the same as creating a formal backup package, and it does not materially reclaim space from the drive containing the Codex data directory. Permanently purging a confirmed-unneeded trash entry releases its stored session data.

## Formal package container

`.codexchat` and `.codexproject` are ZIP containers with a `manifest.json`. They also contain one or more internal `.codexbundle` files; `.codexproject` additionally contains one or more `project-files.zip` payloads.

The current writer uses these manifest schema values:

| Package shape | Current schema |
| --- | --- |
| One-project `.codexchat` | 2 |
| One-project `.codexproject` | 3 |
| Multi-project `.codexchat` or `.codexproject` | 5 |

The schema number is an internal compatibility marker, not an invitation to edit a package manually. Import also supports earlier manifests when their required fields can be validated.

The manifest records source-project identity, bundle ownership, session lineage, titles, timestamps, subagent status, project payload statistics, and the SHA-256 of each project-file payload. During inspection and import, the application verifies declared paths, bundle/project ownership, duplicate paths, destination boundaries, payload hashes and statistics, and archive structure before writing project files.

## `.codexchat`

A conversation-only package contains selected **main conversations** and package metadata, but no project-directory payload or subagent conversations. Typical uses include:

- copying a project folder manually and migrating only its conversations;
- reconnecting conversations after a project is renamed or moved on the same computer;
- archiving selected main conversations without duplicating project files.

Selections can span multiple projects. During import, each source project is mapped to its actual destination folder. Conversation files are placed in the current user's normal Codex data directory.

## `.codexproject`

A complete project package contains one or more project-directory payloads, empty-directory metadata, all linked main and subagent conversations, checksums, and source-lineage metadata.

Project payloads preserve ordinary file contents, directory structure, empty directories, and last-write timestamps. Directory junctions, symbolic links, and other reparse points are skipped. NTFS access-control lists, alternate data streams, and other filesystem-specific metadata are not transferred.

During restore, each project is mapped to a destination folder selected by the user, while conversation files are imported into the current user's normal Codex data directory.

## Project-path reassociation

Import can rewrite recorded working-directory paths and perform targeted updates to the local Codex task index and desktop project association. This allows conversations to be associated with a copied, renamed, or relocated project instead of remaining tied to the source path.

An identical source and destination path is treated as an intentional no-op mapping; the invalid identical `--map-cwd` argument is not sent.

## Conversation identity modes

### Merge by original ID

Matching is scoped to the destination project plus the source conversation's original ID. The first import receives a local Thread ID and retains source-lineage metadata. Later imports can locate the same lineage and merge new content without sharing a session file with an unrelated project.

This supports round-trip work: migrate from computer A to B, continue on B, create another backup, and merge later content back into the corresponding project on A.

### Copy as new conversations

Every import creates fresh Thread IDs and independent conversation files. When a package contains subagents, main/subagent parent-child IDs are rewritten together. Deleting one copy does not delete another copy.

### Codex history modes

Each imported session keeps its recorded `legacy` or `paginated` history mode. For paginated sessions, import also requires valid JSONL, integer `ordinal` values starting at zero without gaps, and `turn_context` when history records are present. The targeted index write verifies the stored mode together with the session path and project association. Unknown future modes and structurally incomplete paginated sessions fail before a successful import is reported.

Full paginated-history reading and resumption remain dependent on the destination Codex version. Keep the source package until imported conversations have been opened and verified in Codex. The current [Codex app-server documentation](https://developers.openai.com/codex/app-server) describes this capability as experimental and not yet available for full-history reads or resume.

## Legacy inputs

`.codexpack` and raw `.codexbundle` files remain supported for import compatibility. New user-created backups use `.codexchat` or `.codexproject` so their purpose is visible from the extension.

## Project-file conflict modes

- **Require an empty destination**: safest default; restoration stops if the folder is not empty.
- **Keep existing files**: adds missing files and leaves same-name destination files unchanged.
- **Overwrite with backup**: creates a recovery ZIP before overwriting same-name files.

Archive paths, destination boundaries, drive roots, protected folders, free space, duplicate paths, Windows-reserved names, and reparse points are validated before project files are written.

## Confidentiality

Formal packages and app-trash records are not encrypted. They may contain complete prompts, responses, source code, command output, local paths, environment details, credentials, and secrets. Inspect and transfer them as sensitive files, and never attach a real package to a public issue.

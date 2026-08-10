# Backup formats and import behavior

The application distinguishes user-created backup packages from the app trash. They serve different purposes and should not be managed as interchangeable backups.

## Storage types at a glance

| Type | User-managed | Purpose |
| --- | --- | --- |
| `.codexchat` | Yes | Selected main conversations without project files |
| `.codexproject` | Yes | One or more project folders plus linked main and subagent conversations |
| App trash | Through the application | Recoverable removal of conversations; stored inside the Codex data directory |

Moving a conversation to the app trash is not the same as creating a formal backup package, and it does not materially reclaim space from the drive containing the Codex data directory. Permanently purging a confirmed-unneeded trash entry releases its stored session data.

## Formal backup packages

### `.codexchat`

Contains selected main conversations and package metadata, but no project-directory payload. Typical uses include:

- copying a project folder manually and migrating only its conversations;
- reconnecting conversations after a project is renamed or moved on the same computer;
- archiving selected conversations without duplicating project files.

During import, each source project can be mapped to its actual destination folder. Conversation files remain in the current user's normal Codex data directory.

### `.codexproject`

Contains one or more project-directory payloads, empty-directory metadata, main conversations, subagent conversations, checksums, and source-lineage metadata. It is intended for complete working-context migration when project files and conversations should travel together in one package.

During restore, each project is mapped to a destination folder selected by the user, while conversation files are imported into the current user's normal Codex data directory.

## Project-path reassociation

Import can rewrite recorded working-directory paths and perform targeted updates to the local Codex task index and desktop project association. This allows conversations to be associated with a project that was copied, renamed, or relocated instead of remaining tied to the source path.

## Conversation identity modes

### Merge by original ID

Matching is scoped to the destination project plus the source conversation's original ID. The first import receives a local Thread ID and retains source-lineage metadata. Later imports can locate the same lineage and merge new content without sharing a session file with an unrelated project.

This mode supports round-trip work: migrate from computer A to B, continue on B, create another backup, and merge the later content back into the corresponding project on A.

### Copy as new conversations

Every import creates fresh Thread IDs and independent conversation files. Main/subagent parent-child IDs are rewritten together. Deleting one copy does not delete another copy.

## Legacy inputs

`.codexpack` and raw `.codexbundle` files remain supported for import compatibility. New user-created backups use `.codexchat` or `.codexproject` so their contents are clear from the extension.

## Project-file conflict modes

- **Require an empty destination**: safest default; restoration stops if the folder is not empty.
- **Keep existing files**: adds missing files and leaves same-name destination files unchanged.
- **Overwrite with backup**: creates a recovery backup before overwriting same-name files.

Archive paths, payload statistics, SHA-256 values, destination boundaries, drive roots, protected folders, and reparse points are validated before project files are written.

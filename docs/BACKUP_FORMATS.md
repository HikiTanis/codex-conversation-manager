# Backup formats and import behavior

## Formal backup files

### `.codexchat`

Contains selected conversations and package metadata, but no project directory payload. It is intended for conversation-only backup and migration.

### `.codexproject`

Contains one or more project directory payloads, empty-directory metadata, main conversations, subagent conversations, checksums, and source-lineage metadata. During restore, each project is mapped to a destination folder selected by the user, while conversation files are imported into the current user's Codex data folder.

## Legacy inputs

`.codexpack` and raw `.codexbundle` files remain supported for import compatibility. New backups use `.codexchat` or `.codexproject` so their contents are clear from the extension.

## Temporary files

`.cct-bak` files are transaction snapshots created during an import that may replace a local file. They are not formal user backups:

- on success, the transaction commits and removes them;
- on failure, the transaction restores from them and then removes them;
- legacy leftovers are moved into a recoverable app-managed location instead of remaining scattered in `sessions`.

## Conversation identity modes

### Merge by original ID

Matching is scoped to the destination project plus the source conversation's original ID. The first import receives a local Thread ID and retains source-lineage metadata; later imports can find and merge the same source without sharing files with an unrelated project.

### Copy as new conversations

Every import creates fresh Thread IDs and independent conversation files. Main/subagent parent-child IDs are rewritten together. Deleting one copy does not delete the other copy.

## Project-file conflict modes

- **Require an empty destination**: safest default; restoration stops if the folder is not empty.
- **Keep existing files**: adds missing files and leaves same-name destination files unchanged.
- **Overwrite with backup**: creates a recovery backup before overwriting same-name files.

Archive paths, payload statistics, SHA-256 values, destination boundaries, drive roots, protected folders, and reparse points are validated before project files are written.

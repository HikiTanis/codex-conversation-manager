# Codex Conversation Migrator v3.0.0

Version 3.0.0 expands the original conversation-transfer utility into a local Codex conversation and project manager. It combines inspection, storage management, deletion, project-path reassociation, backup, restore, and cross-computer migration in one Windows application.

## Problems addressed

- **Renamed or relocated projects:** remap existing conversations to a project's new folder and update local Codex visibility data.
- **Opaque storage usage:** show project totals and the latest update time, file size, and actual path of each main and subagent conversation.
- **Large subagent histories:** manage subagents separately and remove selected old records when their stored execution context is no longer needed.
- **Flexible migration:** migrate conversations to a project copied by the user, or package project files and all linked conversations together.
- **Round-trip continuity:** continue work on a second computer, migrate back, and merge later content by original lineage within the destination project.
- **Routine cleanup:** inspect, trash, restore, or permanently delete unwanted conversations, with optional related-project processing for main conversations.

## Highlights

- Back up selected conversations across projects as `.codexchat`.
- Back up multiple project folders, main conversations, and subagent conversations as `.codexproject`.
- Restore projects to user-selected locations while conversations remain in the normal local Codex data directory.
- Choose smart merge by original ID or independent copy with fresh Thread IDs and separate files.
- View main and subagent conversations separately with consistent selection and deletion controls.
- Use an app trash for recoverable deletion or permanently purge confirmed-unneeded records to reclaim storage.
- Route conversation deletion through Codex's official `thread/delete` interface before removing local data; a refusal preserves the original conversation.
- Invalidate a matching Codex desktop task-list cache after confirmed deletion, including a catch-up path for repairs completed by an older build. Cache cleanup requires Codex to be fully closed and does not remove sign-in data, valid tasks, or project files.
- Resolve parent-child relationships from rollout metadata and the Codex thread index before deletion. Because parent deletion cascades to spawned descendants, recoverable deletion stages each affected conversation separately in the app trash and permanent deletion discloses the full impact count.
- Keep rollout files, the SQLite thread index, and Codex desktop thread state synchronized during deletion and restoration.
- Detect both index orphans and legacy partial deletions. Older candidates are cross-checked against Codex `rollout_not_found` logs and the latest pre-deletion index backup before user review.
- Keep subagent-only projects visible as **Orphaned subagents**. When a stale parent cannot be removed safely, show the descendant's friendly role, exact Thread ID, parent ID, project path, and JSONL path, then automatically select it for recoverable deletion.
- Use redesigned dialogs, consistent controls, corrected title-bar buttons, and a proper taskbar icon.
- Switch between Simplified Chinese and English without restarting.
- Import legacy `.codexpack` and `.codexbundle` files while creating new backups with clearer extensions.

## Upgrade note

Extract v3.0.0 into a new folder rather than mixing it with an older package. Formal `.codexchat` and `.codexproject` backups are user-managed files and are not deleted when their source conversations are removed from the application.

Exit Codex completely before deleting or restoring conversations, because a running desktop process can overwrite local sidebar state. Conversation deletion and legacy-sidebar repair require an independently callable Codex CLI (`codex.exe`); run `codex --version` to verify it is available. If an older build already left a sidebar entry whose rollout file is missing, open that item once so Codex records the failure, exit Codex, click Refresh in the manager, review the detected title and Thread ID, and confirm cleanup. If the rollout and index are already gone but the entry remains visible, Refresh invalidates the exact matching desktop task-list cache and reports when Codex can be reopened.

Moving a conversation to the app trash preserves a copy inside the Codex data directory. To reclaim that disk space, permanently purge the record from the app trash after confirming it is no longer needed.

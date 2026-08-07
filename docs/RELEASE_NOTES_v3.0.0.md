# Codex Conversation Migrator v3.0.0

This release turns the original conversation mover into a complete local project-and-conversation migration tool.

Highlights:

- Back up multiple projects with all linked main and subagent conversations.
- Choose any backup destination folder.
- Restore project files to selected folders while keeping Codex conversations in the normal local Codex data location.
- Choose smart merge by original lineage or independent copy with fresh IDs.
- Manage main and subagent conversations separately, including sizes, paths, selection, trash, restore, permanent deletion, and optional related-project deletion.
- Use a redesigned, consistent interface with corrected title-bar controls and taskbar icon.
- Switch between Simplified Chinese and English without restarting.
- Use the new `.codexchat` and `.codexproject` extensions; legacy formats remain import-compatible.

Upgrade note: extract v3.0.0 into a new folder rather than mixing it with an older package. Formal backup files are not removed when a conversation is deleted from the app.

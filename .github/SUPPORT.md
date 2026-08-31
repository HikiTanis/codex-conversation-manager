# Support

The portable package supports 64-bit Windows 10/11 with the .NET Framework 4.8 runtime. Inspection, backup, and import use the built-in native engine and require no third-party migration executable. Conversation deletion and legacy-sidebar repair require a compatible locally available Codex CLI executable; Codex CLI 0.148.0 or later is the supported project baseline, and the latest release is recommended.

Compressed `.jsonl.zst` sessions currently expose index metadata only. Content preview, formal backup, and import are not supported for those records.

Before opening an issue:

1. Confirm you are using the latest release.
2. For an import problem, run the read-only inspection first. For deletion or sidebar repair, fully exit Codex and record whether refresh/repair changes the result.
3. Record the app version, Windows version, Codex version, relevant backup extension, and the exact error text.
4. Reproduce with synthetic data when possible.

Never post a real backup package, session file, Codex database, username, private path, prompt, project source file, or secret. Redact screenshots before attaching them.

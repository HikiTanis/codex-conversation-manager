# Contributing

Contributions are welcome. Keep changes focused, preserve the local-first behavior, and avoid using real Codex history in tests or screenshots.

## Development workflow

1. Use 64-bit Windows 10/11 with .NET SDK 8.x and PowerShell 5.1 or later. The project restores the pinned `Microsoft.NETFramework.ReferenceAssemblies.net48` NuGet package, so a separately installed .NET Framework 4.8 Targeting Pack is not required.
2. Fork the repository and create a short-lived branch.
3. From the repository root, run the complete build, test, and packaging workflow:

   ```powershell
   .\scripts\package.ps1
   ```

   This command reads the authoritative `VERSION` file, restores pinned build packages, builds the application, runs all automated tests, and creates the release ZIP and checksum under `release/`. Inspection, backup, and import are implemented by the built-in native engine; no third-party migration executable is downloaded or packaged.

   GitHub-hosted Windows runners call the same packaging script with `-SkipUiTests` because they do not provide the interactive desktop required by the native-window hit-test and WPF screenshot regressions. Run the command above without that switch on a normal Windows desktop before release; the bilingual functional self-tests still run in CI.
4. Describe behavior changes, safety implications, and manual checks in the pull request.

## Rules for fixtures and bug reports

- Use synthetic Codex homes and invented Thread IDs.
- Do not commit or attach real conversation bundles, JSONL files, SQLite databases, usernames, absolute user paths, prompts, source code, or secrets.
- Add both Chinese and English text for any new user-facing message.
- Keep permanent deletion behind a clear confirmation and preserve existing path-boundary checks.
- Do not silently change backup formats. Document schema changes and maintain import compatibility when practical.
- Keep `.codexchat` and `.codexproject` as the formal output formats. `.codexpack` and `.codexbundle` are legacy import-only formats.
- Preserve the current `.jsonl.zst` boundary unless the implementation and tests are updated together: index metadata may be shown, but content preview, formal backup, and import are unsupported.

By contributing, you agree that your contribution is licensed under this repository's MIT License.

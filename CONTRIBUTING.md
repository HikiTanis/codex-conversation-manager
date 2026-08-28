# Contributing

Contributions are welcome. Keep changes focused, preserve the local-first behavior, and avoid using real Codex history in tests or screenshots.

## Development workflow

1. Fork the repository and create a short-lived branch.
2. From the repository root, run the complete build, test, and packaging workflow:

   ```powershell
   .\package.ps1
   ```

   This command reads the authoritative `VERSION` file, fetches and verifies the pinned `cct` dependency when missing, builds the application, runs all automated tests, and creates the release ZIP and checksum under `release/`.
3. Describe behavior changes, safety implications, and manual checks in the pull request.

## Rules for fixtures and bug reports

- Use synthetic Codex homes and invented Thread IDs.
- Do not commit or attach real conversation bundles, JSONL files, SQLite databases, usernames, absolute user paths, prompts, source code, or secrets.
- Add both Chinese and English text for any new user-facing message.
- Keep permanent deletion behind a clear confirmation and preserve existing path-boundary checks.
- Do not silently change backup formats. Document schema changes and maintain import compatibility when practical.

By contributing, you agree that your contribution is licensed under this repository's MIT License.

# Security policy

## Supported version

Security fixes are provided for the latest 3.x release.

## Reporting a vulnerability

Use GitHub's private security advisory feature when the repository is published. Do not open a public issue for path-traversal, arbitrary-file-write, unsafe-deletion, archive-validation, or local-index-corruption findings until a fix is available.

Include a minimal synthetic reproduction. Never send a real Codex home, backup package, session JSONL, SQLite database, access token, or proprietary project.

## Security boundary

The application processes local archives and can write to the selected project folder and the current user's Codex data. It validates archive paths, rejects dangerous project roots and reparse-point escapes, verifies payload hashes, backs up index state before writes, and limits deletion to resolved targets. These controls reduce risk but do not make untrusted backup packages safe to open without inspection.

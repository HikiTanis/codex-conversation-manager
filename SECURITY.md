# Security policy

## Supported versions

| Version | Supported |
| --- | --- |
| Latest 3.x release | Yes |
| Older 3.x releases | Upgrade first |
| 2.x and earlier | No |

Codex local storage formats can change independently of this project. Reproduce a problem on the latest release before reporting it.

## Reporting a vulnerability

Use the repository's GitHub **Private vulnerability reporting** or private security-advisory feature. Do not open a public issue for path-traversal, arbitrary-file-write, unsafe-deletion, archive-validation, local-index-corruption, or release-supply-chain findings until a fix is available.

Include a minimal synthetic reproduction and affected version. Never send a real Codex home, backup package, session JSONL, SQLite database, access token, private path, or proprietary project.

## Security boundary

The application processes local archives and can write to a selected project folder and the current user's Codex data. It validates archive and manifest paths, rejects dangerous project roots and reparse-point escapes, checks project-payload hashes and statistics, backs up relevant state before risky writes, limits deletion to resolved targets, and stops when the official Codex deletion protocol refuses an operation.

These controls reduce risk but do not make an untrusted backup package safe. Use **Inspect first (no writes)**, review the source of the package, keep independent backups, and fully exit Codex before formal import, deletion, or restoration.

## Release authenticity

The Windows application and bundled `cct.exe` are currently not Authenticode-signed. Official release assets include `SHA256SUMS.txt`; verify the downloaded ZIP as shown in the README before extraction. A matching hash detects an altered download but does not by itself prove publisher identity.

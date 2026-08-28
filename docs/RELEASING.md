# Releasing

This checklist is for maintainers preparing a public GitHub release. Run it from a clean checkout on Windows.

## 1. Prepare the version

`VERSION` is the authoritative version for packaging, CI, and generated assembly metadata. It must contain one stable semantic version such as `3.0.0`.

For a new release:

1. Update `VERSION`.
2. Update the visible version label and Codex app-server client version when `Verify-Release.ps1` identifies them.
3. Add `docs/RELEASE_NOTES_v<version>.md`.
4. Move completed entries from `[Unreleased]` into a dated version section in `CHANGELOG.md`.
5. Update version-specific download names in both README files.

Run the source-level consistency check:

```powershell
$version = (Get-Content .\VERSION -Raw).Trim()
.\Verify-Release.ps1 -Version $version
```

Do not continue until it passes.

## 2. Build, test, and package

The complete local release command is:

```powershell
.\package.ps1
```

It performs the following work:

- verifies source and documentation version consistency;
- fetches and verifies pinned `cct.exe` only when missing;
- creates a Release build;
- runs Chinese and English functional, window-chrome, and render tests in a temporary synthetic Codex home;
- removes older versioned ZIPs from `release/`;
- creates a deterministic ZIP with complete linked end-user documentation;
- writes `release/SHA256SUMS.txt`;
- verifies the ZIP file list, Markdown links, EXE version, hash, and absence of an absolute PDB path.

Review `artifacts/test/`; every self-test and chrome report must pass, every expected PNG must be present, and no `*.error.txt` file may exist.

## 3. Inspect the release assets

Confirm that `release/` contains exactly:

- `CodexConversationMigrator-Windows-v<version>.zip`
- `SHA256SUMS.txt`
- the tracked `.gitkeep`

Verify the checksum independently:

```powershell
$version = (Get-Content .\VERSION -Raw).Trim()
$zip = ".\release\CodexConversationMigrator-Windows-v$version.zip"
$expected = ((Get-Content .\release\SHA256SUMS.txt -Raw).Trim() -split '\s+')[0]
$actual = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
if ($actual -ne $expected) { throw 'Release checksum mismatch.' }
```

Open the extracted package on a clean test account when practical. Check startup, language switching, backup inspection, and a synthetic import. Never use real private conversations or project data in release evidence.

The binaries are currently not Authenticode-signed. Keep the unsigned-package and SHA-256 verification notice in both README files and the release notes until signing is introduced.

## 4. Commit and tag

Before tagging:

```powershell
git status --short
git diff --check
```

Commit all intended source and documentation changes. Confirm that the Git author email is safe to publish.

Create an annotated tag only after the final commit exists:

```powershell
$version = (Get-Content .\VERSION -Raw).Trim()
git tag -a "v$version" -m "Codex Conversation Migrator v$version"
```

If an unpublished local tag already points to an older preparatory commit, remove and recreate it before any push. Never move or replace a tag that has already been published.

## 5. Push and verify GitHub Release

After configuring the intended `origin` remote:

```powershell
$version = (Get-Content .\VERSION -Raw).Trim()
git push origin main
git push origin "v$version"
```

The tag workflow rejects a tag that does not exactly match `VERSION`, rebuilds and retests from the tagged commit, and uses `docs/RELEASE_NOTES_v<version>.md` as the GitHub Release body.

After the workflow finishes, verify:

- the workflow is green;
- the release title and notes are correct;
- the ZIP and `SHA256SUMS.txt` assets are present;
- the downloaded asset hash matches;
- the README download instructions refer to the published version.

If any check fails, fix it in a new commit and release a new version. Do not silently replace public release assets or move a published tag.

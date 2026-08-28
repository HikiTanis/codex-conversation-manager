[CmdletBinding()]
param(
    [string]$Version,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$canonicalVersion = [IO.File]::ReadAllText((Join-Path $repoRoot 'VERSION')).Trim()
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $canonicalVersion
}
if (-not [string]::Equals($Version, $canonicalVersion, [StringComparison]::Ordinal)) {
    throw "Requested version $Version does not match VERSION $canonicalVersion."
}

& (Join-Path $repoRoot 'Verify-Release.ps1') -Version $Version

$releaseRoot = Join-Path $repoRoot 'release'
$buildRoot = Join-Path $repoRoot 'src\bin\Release\net48'
$zipPath = Join-Path $releaseRoot "CodexConversationMigrator-Windows-v$Version.zip"
$stage = Join-Path $releaseRoot ('.stage-' + [Guid]::NewGuid().ToString('N'))

function New-DeterministicZip {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceRoot,
        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $fixedTimestamp = [DateTimeOffset]::Parse('2000-01-01T00:00:00Z')
    $archive = [IO.Compression.ZipFile]::Open($DestinationPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $sourcePrefix = [IO.Path]::GetFullPath($SourceRoot).TrimEnd('\') + '\'
        $files = Get-ChildItem -LiteralPath $SourceRoot -File -Recurse | Sort-Object {
            $_.FullName.Substring($sourcePrefix.Length).Replace('\', '/')
        }
        foreach ($file in $files) {
            $relativePath = $file.FullName.Substring($sourcePrefix.Length).Replace('\', '/')
            $entry = $archive.CreateEntry($relativePath, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $fixedTimestamp
            $input = [IO.File]::OpenRead($file.FullName)
            $output = $entry.Open()
            try {
                $input.CopyTo($output)
            }
            finally {
                $output.Dispose()
                $input.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

try {
    New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
    Get-ChildItem -LiteralPath $releaseRoot -Filter 'CodexConversationMigrator-Windows-v*.zip' -File |
        Remove-Item -Force
    $existingHashFile = Join-Path $releaseRoot 'SHA256SUMS.txt'
    if (Test-Path -LiteralPath $existingHashFile) {
        Remove-Item -LiteralPath $existingHashFile -Force
    }

    & (Join-Path $repoRoot 'build.ps1') -Configuration Release
    if (-not $SkipTests) {
        & (Join-Path $repoRoot 'test.ps1') -NoBuild
    }

    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    $files = @(
        'CodexConversationMigrator.exe',
        'CodexConversationMigrator.exe.config',
        'CodexConversationMigrator.xaml',
        'cct.exe'
    )
    foreach ($file in $files) {
        $source = Join-Path $buildRoot $file
        if (-not (Test-Path -LiteralPath $source)) {
            throw "Required release file is missing: $source"
        }
        Copy-Item -LiteralPath $source -Destination (Join-Path $stage $file)
    }

    Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging\Start.cmd') -Destination (Join-Path $stage 'Start.cmd')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'VERSION') -Destination (Join-Path $stage 'VERSION')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination (Join-Path $stage 'README.md')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'README.zh-CN.md') -Destination (Join-Path $stage 'README.zh-CN.md')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'CHANGELOG.md') -Destination (Join-Path $stage 'CHANGELOG.md')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'PRIVACY.md') -Destination (Join-Path $stage 'PRIVACY.md')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'SECURITY.md') -Destination (Join-Path $stage 'SECURITY.md')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'SUPPORT.md') -Destination (Join-Path $stage 'SUPPORT.md')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $stage 'LICENSE')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $stage 'THIRD_PARTY_NOTICES.md')
    $stageDocs = Join-Path $stage 'docs'
    New-Item -ItemType Directory -Path $stageDocs -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\BACKUP_FORMATS.md') -Destination (Join-Path $stageDocs 'BACKUP_FORMATS.md')
    Copy-Item -LiteralPath (Join-Path $repoRoot "docs\RELEASE_NOTES_v$Version.md") -Destination (Join-Path $stageDocs "RELEASE_NOTES_v$Version.md")
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\RELEASING.md') -Destination (Join-Path $stageDocs 'RELEASING.md')

    $stageThirdParty = Join-Path $stage 'third_party\cct'
    New-Item -ItemType Directory -Path $stageThirdParty -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot 'third_party\cct\LICENSE') -Destination (Join-Path $stageThirdParty 'LICENSE')

    New-DeterministicZip -SourceRoot $stage -DestinationPath $zipPath

    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    $hashFile = Join-Path $releaseRoot 'SHA256SUMS.txt'
    [IO.File]::WriteAllText($hashFile, "$zipHash  $([IO.Path]::GetFileName($zipPath))$([Environment]::NewLine)", [Text.UTF8Encoding]::new($false))

    & (Join-Path $repoRoot 'Verify-Release.ps1') -Version $Version -PackagePath $zipPath

    Write-Host "Created $zipPath"
    Write-Host "SHA-256 $zipHash"
}
finally {
    if (Test-Path -LiteralPath $stage) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
}

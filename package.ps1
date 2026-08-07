[CmdletBinding()]
param(
    [string]$Version = '3.0.0',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseRoot = Join-Path $repoRoot 'release'
$buildRoot = Join-Path $repoRoot 'src\bin\Release\net48'
$zipPath = Join-Path $releaseRoot "CodexConversationMigrator-Windows-v$Version.zip"
$stage = Join-Path $releaseRoot ('.stage-' + [Guid]::NewGuid().ToString('N'))

try {
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
    Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination (Join-Path $stage 'README.md')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'README.zh-CN.md') -Destination (Join-Path $stage 'README.zh-CN.md')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $stage 'LICENSE')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $stage 'THIRD_PARTY_NOTICES.md')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'third_party\cct\LICENSE') -Destination (Join-Path $stage 'LICENSE-cct.txt')

    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zipPath -CompressionLevel Optimal

    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    $hashFile = Join-Path $releaseRoot 'SHA256SUMS.txt'
    [IO.File]::WriteAllText($hashFile, "$zipHash  $([IO.Path]::GetFileName($zipPath))`r`n", [Text.UTF8Encoding]::new($false))
    Write-Host "Created $zipPath"
    Write-Host "SHA-256 $zipHash"
}
finally {
    if (Test-Path -LiteralPath $stage) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
}

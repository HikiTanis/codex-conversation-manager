[CmdletBinding()]
param(
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$app = Join-Path $repoRoot 'src\bin\Release\net48\CodexConversationMigrator.exe'
$artifactRoot = Join-Path $repoRoot 'artifacts\test'
$temporaryCodexHome = Join-Path ([IO.Path]::GetTempPath()) ('codex-migrator-test-home-' + [Guid]::NewGuid().ToString('N'))
$previousCodexHome = $env:CODEX_HOME

function Invoke-MigratorTest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $app
    $startInfo.Arguments = $Arguments
    $startInfo.WorkingDirectory = Split-Path -Parent $app
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $process = [Diagnostics.Process]::Start($startInfo)
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "$Name failed with exit code $($process.ExitCode)."
    }
    Write-Host "$Name passed."
}

try {
    if (-not $NoBuild) {
        & (Join-Path $repoRoot 'build.ps1') -Configuration Release
    }
    if (-not (Test-Path -LiteralPath $app)) {
        throw "Built application was not found: $app"
    }

    New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $temporaryCodexHome -Force | Out-Null
    $env:CODEX_HOME = $temporaryCodexHome

    $zhReport = Join-Path $artifactRoot 'selftest-zh-CN.txt'
    $enReport = Join-Path $artifactRoot 'selftest-en-US.txt'
    $zhChrome = Join-Path $artifactRoot 'chrome-zh-CN.txt'
    $enChrome = Join-Path $artifactRoot 'chrome-en-US.txt'

    Invoke-MigratorTest "--self-test --language zh-CN --report `"$zhReport`"" 'Chinese self-test'
    Invoke-MigratorTest "--self-test --language en-US --report `"$enReport`"" 'English self-test'
    Invoke-MigratorTest "--chrome-test `"$zhChrome`" --language zh-CN" 'Chinese window chrome test'
    Invoke-MigratorTest "--chrome-test `"$enChrome`" --language en-US" 'English window chrome test'
}
finally {
    $env:CODEX_HOME = $previousCodexHome
    if (Test-Path -LiteralPath $temporaryCodexHome) {
        Remove-Item -LiteralPath $temporaryCodexHome -Recurse -Force
    }
}

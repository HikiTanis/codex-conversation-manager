[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $repoRoot 'src\CodexConversationMigrator.csproj'
$cct = Join-Path $repoRoot 'third_party\cct\cct.exe'
$expectedCctHash = '4BF87C706151F9B8C73357A20FD9FF9FA47139773ACB1FFED5BC24D2D47BCEC5'

if (-not (Test-Path -LiteralPath $cct)) {
    & (Join-Path $repoRoot 'Get-Cct.ps1')
}

$actualCctHash = (Get-FileHash -LiteralPath $cct -Algorithm SHA256).Hash
if ($actualCctHash -ne $expectedCctHash) {
    throw "cct.exe failed SHA-256 verification: $actualCctHash"
}

dotnet build $project --configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

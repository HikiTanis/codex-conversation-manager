[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$destination = Join-Path $repoRoot 'third_party\cct\cct.exe'
$expectedExeHash = '4BF87C706151F9B8C73357A20FD9FF9FA47139773ACB1FFED5BC24D2D47BCEC5'
$expectedArchiveHash = '390CFCFB8A26075EAE3FD2D6C00E5859C8AC1234F6637CE2BDF8B08BDA9AADC'
$downloadUrl = 'https://github.com/ahmojo/codex-claude-transfer/releases/download/v1.2.0/cct_v1.2.0_windows_amd64.tar.gz'

if (Test-Path -LiteralPath $destination) {
    $actual = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    if ($actual -eq $expectedExeHash) {
        Write-Host 'cct.exe v1.2.0 is present and verified.'
        return
    }
    if (-not $Force) {
        throw "Existing cct.exe has an unexpected SHA-256: $actual. Re-run with -Force to replace only this file."
    }
}

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('codex-migrator-cct-' + [Guid]::NewGuid().ToString('N'))
$archive = Join-Path $temporaryRoot 'cct.tar.gz'
$extract = Join-Path $temporaryRoot 'extract'

try {
    New-Item -ItemType Directory -Path $extract -Force | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri $downloadUrl -OutFile $archive

    $archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    if ($archiveHash -ne $expectedArchiveHash) {
        throw "Downloaded cct archive failed SHA-256 verification: $archiveHash"
    }

    & tar.exe -xzf $archive -C $extract
    if ($LASTEXITCODE -ne 0) {
        throw "tar.exe failed with exit code $LASTEXITCODE."
    }

    $downloadedExe = Get-ChildItem -LiteralPath $extract -Filter 'cct.exe' -File -Recurse | Select-Object -First 1
    if ($null -eq $downloadedExe) {
        throw 'The verified cct archive did not contain cct.exe.'
    }

    $exeHash = (Get-FileHash -LiteralPath $downloadedExe.FullName -Algorithm SHA256).Hash
    if ($exeHash -ne $expectedExeHash) {
        throw "Extracted cct.exe failed SHA-256 verification: $exeHash"
    }

    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $downloadedExe.FullName -Destination $destination -Force
    Write-Host "Installed verified cct.exe v1.2.0 to $destination"
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}

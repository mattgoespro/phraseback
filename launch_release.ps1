$ErrorActionPreference = 'Stop'
$release = Join-Path $PSScriptRoot 'dist\Phraseback\Phraseback.exe'
if (-not (Test-Path -LiteralPath $release)) {
    throw 'Canonical release is missing. Run ./build.ps1 -Task Release first.'
}
Start-Process -FilePath $release -WorkingDirectory (Split-Path -Parent $release)

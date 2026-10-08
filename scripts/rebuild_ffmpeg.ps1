# Cache the reproducibly built PNG/GIF encoder together with verified source materials.
param([string]$CacheDirectory, [switch]$Offline, [string]$BuildDirectory)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$pin = Get-Content -LiteralPath (Join-Path $repo 'packaging/ffmpeg.lock.json') -Raw | ConvertFrom-Json
if (-not $CacheDirectory) { $CacheDirectory = Join-Path $repo '.tmp/rebuild/dependencies/ffmpeg-minimal' }
$cache = [IO.Path]::GetFullPath($CacheDirectory)
function Verified([string]$path, [string]$sha) {
    (Test-Path -LiteralPath $path -PathType Leaf) -and ((Get-FileHash -LiteralPath $path).Hash -eq $sha)
}
function VerifiedKit([string]$root) {
    foreach ($file in $pin.source_files) {
        if (-not (Verified (Join-Path $root $file.path) $file.sha256)) { return $false }
    }
    return $true
}
$binary = Join-Path $cache 'ffmpeg.exe'
$kit = Join-Path $cache 'corresponding-source'
if ((Verified $binary $pin.binary_sha256) -and (VerifiedKit $kit)) { return $binary }
if (-not $BuildDirectory) {
    if ($Offline) { throw 'Verified FFmpeg binary and source kit are unavailable offline.' }
    $BuildDirectory = & (Join-Path $PSScriptRoot 'rebuild_ffmpeg_source.ps1')
}
$builtBinary = Join-Path $BuildDirectory 'ffmpeg-7.1/ffmpeg.exe'
$builtKit = Join-Path $BuildDirectory 'corresponding-source'
if (-not (Verified $builtBinary $pin.binary_sha256) -or -not (VerifiedKit $builtKit)) {
    throw 'Built encoder or corresponding-source materials do not match the pinned release.'
}
# Publish only pinned files. Partial copies cannot pass verification on the next run.
New-Item -ItemType Directory -Force $kit | Out-Null
foreach ($file in $pin.source_files) {
    $destination = Join-Path $kit $file.path
    New-Item -ItemType Directory -Force (Split-Path $destination -Parent) | Out-Null
    Copy-Item -LiteralPath (Join-Path $builtKit $file.path) -Destination $destination -Force
}
Copy-Item -LiteralPath $builtBinary -Destination $binary -Force
if (-not (Verified $binary $pin.binary_sha256) -or -not (VerifiedKit $kit)) { throw 'Encoder cache publication failed verification.' }
return $binary

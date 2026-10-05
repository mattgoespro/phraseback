# Fast failure-path checks. Successful compilation is an explicit separate check.
param([string]$BuildDirectory)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$build = Join-Path $PSScriptRoot 'rebuild_ffmpeg_source.ps1'
$scratch = Join-Path $repo ('.tmp/rebuild/ffmpeg-source-check-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $scratch | Out-Null
function ExpectFailure([scriptblock]$action, [string]$pattern) {
    $observed = $false
    try { & $action | Out-Null }
    catch { $observed = $_.Exception.Message -like $pattern }
    if (-not $observed) { throw "Expected rejection: $pattern" }
}
$badCompiler = Join-Path $scratch 'invalid.zip'
[IO.File]::WriteAllText($badCompiler, 'synthetic corrupt compiler')
ExpectFailure { & $build -CompilerArchive $badCompiler -Offline } '*Compiler archive missing or checksum mismatch*'
ExpectFailure { & $build -SourceCache $scratch -Offline } '*Verified source unavailable offline*'
$pin = Get-Content (Join-Path $repo 'app/packaging/ffmpeg-source.lock.json') -Raw | ConvertFrom-Json
$badSource = Join-Path $scratch ([IO.Path]::GetFileName(([Uri]$pin.ffmpeg.url).AbsolutePath))
[IO.File]::WriteAllText($badSource, 'synthetic corrupt source')
ExpectFailure { & $build -SourceCache $scratch -Offline } '*Verified source unavailable offline*'
if (@(Get-ChildItem $scratch -Filter '*.download').Count -ne 0) { throw 'Offline validation created a download.' }
Write-Output 'Source build rejects unverified compiler, missing sources and corrupt sources offline.'
if ($BuildDirectory) {
    $provenance = Get-Content (Join-Path $BuildDirectory 'provenance.json') -Raw | ConvertFrom-Json
    $kit = Join-Path $BuildDirectory 'corresponding-source'
    $retainedPin = Join-Path $kit 'ffmpeg-source.lock.json'
    $retained = Get-Content $retainedPin -Raw | ConvertFrom-Json
    foreach ($source in @($retained.ffmpeg, $retained.zlib)) {
        $archive = Join-Path $kit ([IO.Path]::GetFileName(([Uri]$source.url).AbsolutePath))
        if ((Get-FileHash $archive).Hash -ne $source.sha256) { throw 'Retained source hash mismatch.' }
    }
    $checks = @{
        'ffmpeg-7.1/ffmpeg.exe' = $provenance.binary_sha256
        'corresponding-source/build_minimal_ffmpeg.sh' = $provenance.recipe_sha256
        'corresponding-source/ffmpeg-source.lock.json' = $provenance.source_manifest_sha256
    }
    foreach ($relative in $checks.Keys) {
        if ((Get-FileHash (Join-Path $BuildDirectory $relative)).Hash -ne $checks[$relative]) {
            throw "Build provenance mismatch: $relative"
        }
    }
    foreach ($notice in @('LLVM-MinGW-LICENSE.TXT', 'COPYING.LGPLv2.1',
        'FFmpeg-LICENSE.md', 'zlib-license-and-header.h', 'FFMPEG-SOURCE-BUILD.md',
        'mingw-runtime-notices/COPYING.MinGW-w64-runtime.txt',
        'mingw-runtime-notices/COPYING.MinGW-w64.txt', 'mingw-runtime-notices/COPYING')) {
        if ((Get-Item (Join-Path $kit $notice)).Length -eq 0) { throw "Empty notice: $notice" }
    }
    if ($provenance.redistribution_approved -ne $false) { throw 'Candidate must not approve its own redistribution.' }
    Write-Output 'Retained source archives, recipe, binary provenance and required source-kit notices verified.'
}

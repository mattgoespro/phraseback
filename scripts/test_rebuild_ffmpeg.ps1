$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$bootstrap = Join-Path $PSScriptRoot 'rebuild_ffmpeg.ps1'
$pin = Get-Content -LiteralPath (Join-Path $repo 'packaging/ffmpeg.lock.json') -Raw | ConvertFrom-Json
$verified = & $bootstrap
$scratch = Join-Path $repo ('.tmp/rebuild/ffmpeg-check-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
function Assert([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function MustFailOffline {
    $failed = $false
    try { & $bootstrap -CacheDirectory $scratch -Offline | Out-Null }
    catch { $failed = $_.Exception.Message -like '*unavailable offline*' }
    Assert $failed 'Missing/corrupt dependency must fail closed without networking.'
}
MustFailOffline
$fixture = Join-Path $scratch 'verified-build'
New-Item -ItemType Directory -Force (Join-Path $fixture 'ffmpeg-7.1') | Out-Null
Copy-Item -LiteralPath $verified -Destination (Join-Path $fixture 'ffmpeg-7.1/ffmpeg.exe')
Copy-Item -LiteralPath (Join-Path (Split-Path $verified -Parent) 'corresponding-source') -Destination $fixture -Recurse
$extracted = & $bootstrap -CacheDirectory $scratch -Offline -BuildDirectory $fixture
Assert ((Get-FileHash $extracted).Hash -eq $pin.binary_sha256) 'Verified import changed the executable.'
Assert ((& $bootstrap -CacheDirectory $scratch -Offline) -eq $extracted) 'Warm offline cache failed.'
[IO.File]::WriteAllText($extracted, 'deliberately corrupt synthetic cache')
MustFailOffline
$repaired = & $bootstrap -CacheDirectory $scratch -Offline -BuildDirectory $fixture
Assert ((Get-FileHash $repaired).Hash -eq $pin.binary_sha256) 'Corrupt binary was not repaired from the verified build.'
[IO.File]::WriteAllText((Join-Path $scratch 'corresponding-source/COPYING.LGPLv2.1'), 'corrupt synthetic notice')
MustFailOffline
& $bootstrap -CacheDirectory $scratch -Offline -BuildDirectory $fixture | Out-Null
[IO.File]::WriteAllText((Join-Path $scratch 'corresponding-source/ffmpeg-7.1.tar.xz'), 'corrupt synthetic source')
MustFailOffline
$rejected = $false
[IO.File]::WriteAllText((Join-Path $fixture 'corresponding-source/zlib-1.3.2.tar.gz'), 'corrupt build source')
try { & $bootstrap -CacheDirectory $scratch -Offline -BuildDirectory $fixture | Out-Null }
catch { $rejected = $_.Exception.Message -like '*do not match*' }
Assert $rejected 'An unverified build must not repair the cache.'
Assert (@(Get-ChildItem $scratch -Filter '*.download').Count -eq 0) 'Bootstrap left a partial download.'
Write-Output 'FFmpeg bootstrap: verified import, warm offline reuse, repair, and binary/source/notice corruption rejection passed.'
$global:LASTEXITCODE = 0 # Expected native failures above are successful rejection tests.

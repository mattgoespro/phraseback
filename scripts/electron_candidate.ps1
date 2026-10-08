param([string]$EnginePath = '', [string]$FfmpegPath = '')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$app = Join-Path $repo 'packages/ui'
$source = Join-Path $repo ('.tmp/electron/package-source-' + [Guid]::NewGuid().ToString('N'))
$out = Join-Path $repo ('.tmp/electron/package-' + [Guid]::NewGuid().ToString('N'))
if (-not $EnginePath) {
    $cargo = Join-Path $env:USERPROFILE '.cargo/bin/cargo.exe'
    if (-not (Test-Path -LiteralPath $cargo)) { throw 'Pinned Rust toolchain is unavailable' }
    $env:CARGO_TARGET_DIR = Join-Path $repo '.tmp/rebuild/target'
    & $cargo '+1.98.1-x86_64-pc-windows-msvc' build --manifest-path (Join-Path $repo 'packages/engine/Cargo.toml') --locked --release --target x86_64-pc-windows-msvc
    if ($LASTEXITCODE -ne 0) { throw 'MSVC release engine build failed' }
    $EnginePath = Join-Path $env:CARGO_TARGET_DIR 'x86_64-pc-windows-msvc/release/phraseback-engine.exe'
}
if (-not $FfmpegPath) { $FfmpegPath = Join-Path $repo '.tmp/rebuild/dependencies/ffmpeg-minimal/ffmpeg.exe' }
$EnginePath = (Resolve-Path -LiteralPath $EnginePath).Path
$FfmpegPath = (Resolve-Path -LiteralPath $FfmpegPath).Path
foreach ($required in @($EnginePath,$FfmpegPath)) { if (-not (Test-Path -LiteralPath $required)) { throw "Missing package input: $required" } }
$ffmpegPin = Get-Content -LiteralPath (Join-Path $repo 'packaging/ffmpeg.lock.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $FfmpegPath).Hash -ne $ffmpegPin.binary_sha256) { throw 'FFmpeg binary does not match the pinned source kit' }
$sourceKit = Join-Path (Split-Path $FfmpegPath -Parent) 'corresponding-source'
foreach ($file in $ffmpegPin.source_files) {
    $sourceFile = Join-Path $sourceKit $file.path
    if (-not (Test-Path -LiteralPath $sourceFile) -or (Get-FileHash -LiteralPath $sourceFile).Hash -ne $file.sha256) { throw "FFmpeg source kit mismatch: $($file.path)" }
}
Push-Location $app
$previousTestEngine = [Environment]::GetEnvironmentVariable('PHRASEBACK_TEST_ENGINE')
try {
    & (Join-Path $repo 'scripts/electron_fixture.ps1') | Out-Null
    $env:PHRASEBACK_TEST_ENGINE = $EnginePath
    & npm run check; if ($LASTEXITCODE -ne 0) { throw 'TypeScript check failed' }
    & npm test; if ($LASTEXITCODE -ne 0) { throw 'Electron tests failed' }
    & npm run build; if ($LASTEXITCODE -ne 0) { throw 'Electron build failed' }
    New-Item -ItemType Directory -Path $source -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $app 'dist') -Destination (Join-Path $source 'dist') -Recurse
    @{ name='phraseback'; productName='Phraseback'; version='0.2.0'; main='dist/main/main.js'; private=$true } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $source 'package.json') -Encoding utf8
    $packager = Join-Path $app 'node_modules/.bin/electron-packager.cmd'
    $electronVersion = (Get-Content -LiteralPath (Join-Path $app 'node_modules/electron/package.json') -Raw | ConvertFrom-Json).version
    & $packager $source Phraseback --platform=win32 --arch=x64 "--electron-version=$electronVersion" "--out=$out" --asar --overwrite --executable-name=Phraseback
    if ($LASTEXITCODE -ne 0) { throw 'Electron packaging failed' }
    $payload = Join-Path $out 'Phraseback-win32-x64'
    Copy-Item -LiteralPath $EnginePath -Destination (Join-Path $payload 'Phraseback.Engine.exe')
    Copy-Item -LiteralPath $FfmpegPath -Destination (Join-Path $payload 'ffmpeg.exe')
    Copy-Item -LiteralPath $sourceKit -Destination (Join-Path $payload 'ffmpeg-source') -Recurse
    Copy-Item -LiteralPath (Join-Path $repo 'packaging/FFMPEG-SOURCE-BUILD.md') -Destination (Join-Path $payload 'ffmpeg-source/BUILD.md')
    Copy-Item -LiteralPath (Join-Path $repo 'packaging/ffmpeg.lock.json') -Destination $payload
    Copy-Item -LiteralPath (Join-Path $repo 'packaging/THIRD-PARTY-NOTICES-ELECTRON.md') -Destination (Join-Path $payload 'THIRD-PARTY-NOTICES.md')
    $licenseDir = Join-Path $payload 'licenses/electron'
    New-Item -ItemType Directory -Path $licenseDir -Force | Out-Null
    foreach ($name in @('LICENSE','LICENSES.chromium.html')) { Copy-Item -LiteralPath (Join-Path $app "node_modules/electron/dist/$name") -Destination $licenseDir }
    & node (Join-Path $app 'scripts/collect-notices.mjs') $payload
    if ($LASTEXITCODE -ne 0) { throw 'npm notice inventory failed' }
    & (Join-Path $repo 'scripts/electron_rust_notices.ps1') -Payload $payload
    if ($LASTEXITCODE -ne 0) { throw 'Rust notice inventory failed' }
    $unwind = Join-Path (Split-Path $EnginePath -Parent) 'libunwind.dll'
    if (Test-Path -LiteralPath $unwind) { Copy-Item -LiteralPath $unwind -Destination $payload }
    Copy-Item -LiteralPath (Join-Path $repo 'packages/engine/contracts/model-presets.json') -Destination $payload
    $inventory = @(Get-ChildItem -LiteralPath $payload -File -Recurse | ForEach-Object {
        @{ path = [IO.Path]::GetRelativePath($payload,$_.FullName); bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    @{ candidate = $true; redistribution_approved = $false; ui = 'electron'; engine_sha256 = (Get-FileHash -LiteralPath $EnginePath -Algorithm SHA256).Hash.ToLowerInvariant(); engine_input = $EnginePath; files = $inventory } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $out 'build-provenance.json') -Encoding utf8
    $iscc = Join-Path $repo '.tmp/tooling/inno-6.7.3/ISCC.exe'
    $innoPin = Get-Content -LiteralPath (Join-Path $repo 'packaging/inno.lock.json') -Raw | ConvertFrom-Json
    if (-not (Test-Path -LiteralPath $iscc) -or (Get-FileHash -LiteralPath $iscc).Hash -ne $innoPin.compiler_sha256) { throw 'Pinned Inno Setup compiler unavailable or changed' }
    & $iscc /Qp "/DPayload=$payload" "/DOutput=$out" (Join-Path $repo 'packaging/Phraseback.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Electron installer build failed' }
    $installerPath = Join-Path $out 'Phraseback-Setup-0.2.0-win-x64.exe'
    @{ candidate = $true; redistribution_approved = $false; ui = 'electron'; installer = @{ name = [IO.Path]::GetFileName($installerPath); bytes = (Get-Item -LiteralPath $installerPath).Length; sha256 = (Get-FileHash -LiteralPath $installerPath).Hash.ToLowerInvariant() }; payload_inventory = 'build-provenance.json' } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $out 'package-summary.json') -Encoding utf8
    @{ candidate = $out; summary_sha256 = (Get-FileHash -LiteralPath (Join-Path $out 'package-summary.json')).Hash.ToLowerInvariant() } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $repo '.tmp/electron/latest-candidate.json') -Encoding utf8
    Write-Output "Electron candidate: $out"
} finally {
    [Environment]::SetEnvironmentVariable('PHRASEBACK_TEST_ENGINE', $previousTestEngine)
    Pop-Location
}

param([ValidateSet('Build','Check','Test','Prototype')][string]$Task = 'Build')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Split-Path $PSScriptRoot -Parent)).Path
$cargo = Join-Path $env:USERPROFILE '.cargo/bin/cargo.exe'
if (-not (Test-Path -LiteralPath $cargo)) { throw 'Pinned Rust toolchain is unavailable.' }
$env:CARGO_TARGET_DIR = Join-Path $repo '.tmp/rebuild/target'
$manifest = Join-Path $repo 'app/Cargo.toml'
$toolchain = '+1.98.1-x86_64-pc-windows-msvc'
$target = 'x86_64-pc-windows-msvc'
function Invoke-Cargo([string[]]$Arguments) {
    & $cargo $toolchain @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Rust $($Arguments[0]) failed with exit code $LASTEXITCODE" }
}
if ($Task -eq 'Check') {
    Invoke-Cargo -Arguments @('fmt','--manifest-path',$manifest,'--all','--check')
    Invoke-Cargo -Arguments @('clippy','--manifest-path',$manifest,'--locked','--target',$target,'--all-targets','--','-D','warnings')
    return
}
Invoke-Cargo -Arguments @('build','--manifest-path',$manifest,'--locked','--target',$target)
$payload = Join-Path $repo '.tmp/rebuild/prototype-payload'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $env:CARGO_TARGET_DIR "$target/debug/phraseback-engine.exe") -Destination (Join-Path $payload 'Phraseback.Engine.exe') -Force
if ($Task -eq 'Test') { Invoke-Cargo -Arguments @('test','--manifest-path',$manifest,'--locked','--target',$target) }
if ($Task -in @('Test','Prototype')) {
    & (Join-Path $PSScriptRoot 'electron_fixture.ps1') | Out-Null
}
if ($Task -eq 'Prototype') {
    $encoder = & (Join-Path $PSScriptRoot 'rebuild_ffmpeg.ps1')
    Copy-Item -LiteralPath $encoder -Destination (Join-Path $payload 'ffmpeg.exe') -Force
}

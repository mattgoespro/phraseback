param([switch]$AllowDevelopmentToolchain)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Set-Location $repo
$dotnet = Join-Path $env:LOCALAPPDATA 'FlowRecorderDev/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$cargo = Join-Path $env:USERPROFILE '.cargo/bin/cargo.exe'
$env:PATH = "$(Split-Path $cargo -Parent);$env:PATH"
$env:CARGO_TARGET_DIR = Join-Path $repo '.tmp/rebuild/target'
$target = 'x86_64-pc-windows-msvc'
$toolchain = '+1.98.1-x86_64-pc-windows-msvc'
if ($AllowDevelopmentToolchain) {
    $target = 'x86_64-pc-windows-gnullvm'
    $toolchain = '+1.98.1-x86_64-pc-windows-gnullvm'
    $compiler = Join-Path $repo '.tmp/tooling/llvm-mingw-20260908-ucrt-x86_64/bin'
    $env:PATH = "$compiler;$env:PATH"
    $env:CARGO_TARGET_X86_64_PC_WINDOWS_GNULLVM_LINKER = Join-Path $compiler 'x86_64-w64-mingw32-clang.exe'
}
function Checked([string]$executable, [string[]]$arguments) {
    & $executable @arguments
    if ($LASTEXITCODE -ne 0) { throw "$executable failed ($LASTEXITCODE)" }
}
$stage = Join-Path $repo ('.tmp/rebuild/package-' + [Guid]::NewGuid().ToString('N'))
$payload = Join-Path $stage 'Phraseback'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
Checked $cargo @($toolchain,'build','--manifest-path','app/Cargo.toml','--locked','--release','--target',$target)
Checked $dotnet @('publish','app/shell/Phraseback.App/Phraseback.App.csproj','-c','Release','-r','win-x64','--self-contained','true','-p:RestoreLockedMode=true','-p:PublishTrimmed=false','-o',$payload)
Copy-Item -LiteralPath (Join-Path $env:CARGO_TARGET_DIR "$target/release/phraseback-engine.exe") -Destination (Join-Path $payload 'Phraseback.Engine.exe')
if ($AllowDevelopmentToolchain) { Copy-Item -LiteralPath (Join-Path $compiler 'libunwind.dll') -Destination $payload }
$encoder = & (Join-Path $PSScriptRoot 'rebuild_ffmpeg.ps1')
Copy-Item -LiteralPath $encoder -Destination $payload
Copy-Item -LiteralPath (Join-Path (Split-Path $encoder -Parent) 'corresponding-source') -Destination (Join-Path $payload 'ffmpeg-source') -Recurse
Copy-Item -LiteralPath (Join-Path $repo 'app/packaging/FFMPEG-SOURCE-BUILD.md') -Destination (Join-Path $payload 'ffmpeg-source/BUILD.md')
Copy-Item -LiteralPath (Join-Path $repo 'app/packaging/ffmpeg.lock.json') -Destination $payload
Copy-Item -LiteralPath (Join-Path $repo 'app/packaging/THIRD-PARTY-NOTICES.md') -Destination $payload
Copy-Item -LiteralPath (Join-Path $repo 'app/contracts/model-presets.json') -Destination $payload
if (-not $AllowDevelopmentToolchain) {
    & (Join-Path $PSScriptRoot 'rebuild_runtime_imports.ps1') -Payload $payload -Report (Join-Path $stage 'runtime-imports.json')
}
& (Join-Path $PSScriptRoot 'rebuild_notices.ps1') -Payload $payload -Target $target -Toolchain $toolchain.TrimStart('+')
$inventory = Get-ChildItem -LiteralPath $payload -File -Recurse | ForEach-Object {
    @{ path = [IO.Path]::GetRelativePath($payload,$_.FullName); bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
}
@{ candidate = $true; redistribution_approved = $false; toolchain = $toolchain; target = $target; files = @($inventory) } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $stage 'build-provenance.json') -Encoding utf8
& $cargo $toolchain metadata --manifest-path app/Cargo.toml --locked --format-version 1 |
    Set-Content -LiteralPath (Join-Path $stage 'cargo-metadata.json') -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Dependency inventory failed' }
$iscc = Join-Path $repo '.tmp/tooling/inno-6.7.3/ISCC.exe'
if (-not (Test-Path -LiteralPath $iscc)) { throw "Portable candidate is at $payload. Install pinned Inno Setup 6.7.3 to build its installer." }
$innoPin = Get-Content -LiteralPath (Join-Path $repo 'app/packaging/inno.lock.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $iscc -Algorithm SHA256).Hash -ne $innoPin.compiler_sha256) {
    throw 'Installer compiler checksum mismatch; restore the pinned Inno Setup installation.'
}
Checked $iscc @('/Qp',"/DPayload=$payload","/DOutput=$stage",(Join-Path $repo 'app/packaging/Phraseback.iss'))
Copy-Item -LiteralPath (Join-Path $repo 'app/packaging/inno.lock.json') -Destination $stage
$installerPath = Join-Path $stage 'Phraseback-Setup-0.2.0-win-x64.exe'
@{
    candidate = $true
    redistribution_approved = $false
    source_revision = $env:GITHUB_SHA
    installer = @{ name = [IO.Path]::GetFileName($installerPath); bytes = (Get-Item -LiteralPath $installerPath).Length; sha256 = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant() }
    compiler = @{ version = $innoPin.version; sha256 = $innoPin.compiler_sha256 }
    payload_inventory = 'build-provenance.json'
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $stage 'package-summary.json') -Encoding utf8
@{ payload = $payload; candidate = $true; shared_data_enabled = $true; playground_isolated = $true } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $repo '.tmp/rebuild/playground-payload.json') -Encoding utf8
Write-Output "Candidate only: $stage"
Write-Output 'Staged payload is ready. Use ./build.ps1 -Task Release to verify and publish dist/Phraseback.'

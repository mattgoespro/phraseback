param([ValidateSet('Build', 'Test', 'Prototype', 'Check')][string]$Task = 'Build', [switch]$UseDevelopmentToolchain)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Set-Location $repo
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$dotnet = Join-Path $env:LOCALAPPDATA 'FlowRecorderDev/dotnet/dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$cargo = Join-Path $env:USERPROFILE '.cargo/bin/cargo.exe'
if (-not (Test-Path $cargo)) { $cargo = (Get-Command cargo -ErrorAction Stop).Source }
$env:PATH = "$(Split-Path $cargo -Parent);$env:PATH"
$env:CARGO_TARGET_DIR = Join-Path $repo '.tmp/rebuild/target'
$testTemp = Join-Path $repo '.tmp/rebuild/tool-temp'
New-Item -ItemType Directory -Force $testTemp | Out-Null
$env:TEMP = $testTemp
$env:TMP = $testTemp
$compiler = Join-Path $repo '.tmp/tooling/llvm-mingw-20260908-ucrt-x86_64/bin'
$rust = @('+1.98.1-x86_64-pc-windows-msvc', '--manifest-path', 'app/Cargo.toml')
$target = 'x86_64-pc-windows-msvc'
$vswhere = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Microsoft Visual Studio/Installer/vswhere.exe'
$hasMsvc = $false
if (Test-Path -LiteralPath $vswhere) {
    $hasMsvc = [bool](& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 Microsoft.VisualStudio.Component.Windows11SDK.26100 -property installationPath)
}
if ($UseDevelopmentToolchain -or -not $hasMsvc) {
    if (-not (Test-Path -LiteralPath $compiler)) { throw 'Install MSVC x64 and Windows SDK, or bootstrap the documented development compiler' }
    # Local prototype fallback only; release acceptance still requires MSVC.
    $env:PATH = "$compiler;$env:PATH"
    $env:CARGO_TARGET_X86_64_PC_WINDOWS_GNULLVM_LINKER = Join-Path $compiler 'x86_64-w64-mingw32-clang.exe'
    $rust[0] = '+1.98.1-x86_64-pc-windows-gnullvm'
    $target = 'x86_64-pc-windows-gnullvm'
}
function Invoke-Checked([string]$exe, [string[]]$arguments) {
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "$exe failed with exit code $LASTEXITCODE" }
}
$toolchain = $rust[0]
$manifest = @('--manifest-path', 'app/Cargo.toml')
if ($Task -eq 'Check') {
    Invoke-Checked $dotnet @('run', '--project', 'app/tools/Phraseback.Tools/Phraseback.Tools.csproj', '-p:RestoreLockedMode=true')
    Invoke-Checked $cargo (@($toolchain, 'fmt') + $manifest + @('--all', '--check'))
    Invoke-Checked $cargo (@($toolchain, 'clippy') + $manifest + @('--locked', '--target', $target, '--all-targets', '--', '-D', 'warnings'))
    exit
}
Invoke-Checked $cargo (@($toolchain, 'build') + $manifest + @('--locked', '--target', $target))
Invoke-Checked $dotnet @('build', 'app/shell/Phraseback.App/Phraseback.App.csproj', '-p:RestoreLockedMode=true')
$payload = Join-Path $repo '.tmp/rebuild/prototype-payload'
New-Item -ItemType Directory -Force $payload | Out-Null
$env:FLOW_REBUILD_ENGINE = Join-Path $payload 'Phraseback.Engine.exe'
Copy-Item -LiteralPath (Join-Path $env:CARGO_TARGET_DIR "$target/debug/phraseback-engine.exe") -Destination $env:FLOW_REBUILD_ENGINE -Force
if ($target -eq 'x86_64-pc-windows-gnullvm') {
    Copy-Item -LiteralPath (Join-Path $compiler 'libunwind.dll') -Destination $payload -Force
}
$encoder = & (Join-Path $PSScriptRoot 'rebuild_ffmpeg.ps1')
Copy-Item -LiteralPath $encoder -Destination (Join-Path $payload 'ffmpeg.exe') -Force
$env:FLOW_REBUILD_TEST_ROOT = Join-Path $repo '.tmp/rebuild/csharp-tests'
if ($Task -eq 'Test') {
    & (Join-Path $PSScriptRoot 'test_rebuild_ffmpeg.ps1')
    Invoke-Checked $dotnet @('run', '--project', 'app/tools/Phraseback.Tools/Phraseback.Tools.csproj', '-p:RestoreLockedMode=true')
    Invoke-Checked $cargo (@($toolchain, 'test') + $manifest + @('--locked', '--target', $target))
    Invoke-Checked $dotnet @('test', 'app/shell/Phraseback.Tests/Phraseback.Tests.csproj', '-p:RestoreLockedMode=true', '--logger', 'trx', '--results-directory', '.tmp/rebuild/reports')
}
if ($Task -eq 'Prototype') {
    $fixture = Join-Path $repo '.tmp/rebuild/prototype-data'
    Invoke-Checked $dotnet @('run', '--project', 'app/tools/Phraseback.Tools/Phraseback.Tools.csproj', '-p:RestoreLockedMode=true', '--', '--fixture-root', $fixture)
    $app = Join-Path $repo '.tmp/rebuild/dotnet/bin/Phraseback.App/Debug/net10.0/Phraseback.exe'
    $env:DOTNET_ROOT = Split-Path $dotnet -Parent
    Start-Process -FilePath $app -ArgumentList @('--engine', ('"' + $env:FLOW_REBUILD_ENGINE + '"'), '--data-root', ('"' + $fixture + '"')) -WindowStyle Hidden
}

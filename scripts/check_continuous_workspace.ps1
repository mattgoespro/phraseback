param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $repo
if (-not $SkipBuild) { & (Join-Path $repo 'build.ps1') -Task Build }
$checkRoot = Join-Path $repo ('.tmp/rebuild/workflow-' + [Guid]::NewGuid().ToString('N'))
$dotnet = Join-Path $env:LOCALAPPDATA 'FlowRecorderDev/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet).Source }
& $dotnet run --project app/tools/Phraseback.Tools -- --fixture-root $checkRoot
if ($LASTEXITCODE -ne 0) { throw 'Failed to install isolated fixture' }
$env:DOTNET_ROOT = Split-Path $dotnet -Parent
$checkApp = Join-Path $repo '.tmp/rebuild/dotnet/bin/Phraseback.App/Debug/net10.0/Phraseback.exe'
$checkEngine = Join-Path $repo '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe'
$checkProcess = Start-Process -FilePath $checkApp -ArgumentList @('--engine', ('"' + $checkEngine + '"'), '--data-root', ('"' + $checkRoot + '"'), '--workflow-check') -WindowStyle Hidden -PassThru
if (-not $checkProcess.WaitForExit(30000)) {
    $checkProcess.Kill($true)
    [void]$checkProcess.WaitForExit(5000)
    throw "Native workflow check exceeded 30 seconds; isolated evidence remains at $checkRoot"
}
$reportPath = Join-Path $checkRoot 'workflow-check/report.json'
if (-not (Test-Path -LiteralPath $reportPath)) { throw "No workflow report at $reportPath" }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
Get-Content -LiteralPath $reportPath
if (-not $report.passed) { throw 'Native workflow check failed' }

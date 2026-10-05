param([switch]$SkipBuild, [string]$PackagedPayload, [ValidateRange(5,20)][int]$Seconds = 5, [switch]$FullDisplay, [ValidateSet('button','shortcut')][string]$StopControl, [switch]$UnmarkedDataRoot, [ValidateSet('colors','detailed')][string]$Workload = 'colors')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Set-Location $repo
if (-not $SkipBuild -and -not $PackagedPayload) { & "$PSScriptRoot/rebuild.ps1" -Task Build }
$nativeRoot = Join-Path $repo ('.tmp/rebuild/native-check-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $nativeRoot | Out-Null
New-Item -ItemType Directory -Path (Join-Path $nativeRoot 'sessions') | Out-Null
if (-not $UnmarkedDataRoot) { New-Item -ItemType File -Path (Join-Path $nativeRoot '.flow-recorder-development') | Out-Null }
$env:DOTNET_ROOT = Join-Path $env:LOCALAPPDATA 'FlowRecorderDev/dotnet'
$nativeApp = Join-Path $repo '.tmp/rebuild/dotnet/bin/Phraseback.App/Debug/net10.0/Phraseback.exe'
$nativeEngine = Join-Path $repo '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe'
if ($PackagedPayload) {
    $payload = (Resolve-Path -LiteralPath $PackagedPayload).Path
    $nativeApp = Join-Path $payload 'Phraseback.exe'
    $nativeEngine = Join-Path $payload 'Phraseback.Engine.exe'
    if (-not (Test-Path -LiteralPath (Join-Path $payload 'coreclr.dll'))) { throw 'Expected a self-contained payload with its private runtime' }
    # Proves app-local runtime startup on this host, not clean-machine acceptance.
    $env:DOTNET_ROOT = Join-Path $nativeRoot 'deliberately-absent-dotnet'
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'
}
$nativeArguments = @('--engine',('"' + $nativeEngine + '"'),'--data-root',('"' + $nativeRoot + '"'),'--native-check','--native-check-seconds',$Seconds)
if ($FullDisplay) { $nativeArguments += '--native-check-full-display' }
$nativeArguments += @('--native-check-workload', $Workload)
if ($StopControl) { $nativeArguments += @('--native-check-stop', $StopControl) }
$testedBinaries = @(
    @{role='application_host';path=$nativeApp},
    @{role='shell_assembly';path=(Join-Path (Split-Path $nativeApp -Parent) 'Phraseback.dll')},
    @{role='engine';path=$nativeEngine}
) | ForEach-Object {
    @{role=$_.role;sha256=(Get-FileHash -LiteralPath $_.path -Algorithm SHA256).Hash.ToLowerInvariant()}
}
@{ cpu = @(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors); gpu = @(Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion); memory_bytes = (Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory; os = (Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,BuildNumber); packaged = [bool]$PackagedPayload; binaries = @($testedBinaries) } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $nativeRoot 'machine.json') -Encoding utf8
$nativeProcess = Start-Process -FilePath $nativeApp -ArgumentList $nativeArguments -WindowStyle Hidden -PassThru
Write-Output $nativeRoot
if (-not $nativeProcess.WaitForExit(30000)) {
    # This handle owns only the isolated test process, never an existing user app.
    # Bound the entire visible check, including countdown, stop and review work.
    $nativeProcess.Kill($true)
    [void]$nativeProcess.WaitForExit(5000)
    throw 'Native check exceeded the 30-second safety deadline and its test process tree was stopped. Isolated evidence is retained.'
}
$report = Join-Path $nativeRoot 'native-check.json'
if (-not (Test-Path -LiteralPath $report)) { throw 'Native check did not write a report' }
Get-Content -LiteralPath $report
if (-not (Get-Content -LiteralPath $report -Raw | ConvertFrom-Json).passed) { throw 'Native check failed; evidence retained in its isolated folder' }

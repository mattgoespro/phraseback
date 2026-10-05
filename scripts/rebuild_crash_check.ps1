param([Parameter(Mandatory)][string]$Payload)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$payloadPath = (Resolve-Path -LiteralPath $Payload).Path
$application = Join-Path $payloadPath 'Phraseback.exe'
$root = Join-Path $repo ('.tmp/rebuild/crash-check-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $root 'sessions') -Force | Out-Null
New-Item -ItemType File -Path (Join-Path $root '.flow-recorder-development') | Out-Null
$children = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
function Start-CheckedPreview {
    $appProcess = Start-Process -FilePath $application -ArgumentList @('--data-root', ('"' + $root + '"')) -WindowStyle Hidden -PassThru
    $children.Add($appProcess)
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while ([DateTime]::UtcNow -lt $deadline) {
        $engineInfo = Get-CimInstance Win32_Process -Filter "ParentProcessId = $($appProcess.Id)" | Where-Object Name -eq 'Phraseback.Engine.exe' | Select-Object -First 1
        if ($engineInfo) {
            $lock = Join-Path $root 'application.lock'
            if ((Test-Path -LiteralPath $lock) -and (Get-Content -LiteralPath $lock -TotalCount 1) -eq [string]$engineInfo.ProcessId) {
                $engineProcess = [Diagnostics.Process]::GetProcessById($engineInfo.ProcessId)
                $children.Add($engineProcess)
                return @{ app = $appProcess; engine = $engineProcess }
            }
        }
        if ($appProcess.HasExited) { throw 'The test shell exited before the engine acquired its data lock.' }
        Start-Sleep -Milliseconds 100
    }
    throw 'Engine did not acquire its isolated lock before the deadline.'
}
try {
    $first = Start-CheckedPreview
    $first.app.Kill() # Deliberately kill only this test shell, not its process tree.
    if (-not $first.app.WaitForExit(5000)) { throw 'Test shell did not stop.' }
    if (-not $first.engine.WaitForExit(5000)) { throw 'The shell job did not clean up its engine.' }
    $second = Start-CheckedPreview
    if (-not $second.app.CloseMainWindow()) { throw 'Restarted shell has no closeable window.' }
    if (-not $second.app.WaitForExit(8000)) { throw 'Normal close did not finish.' }
    if (-not $second.engine.WaitForExit(5000)) { throw 'Restarted engine did not stop.' }
    @{ passed = $true; shell_crash_killed_engine = $true; lock_reacquired = $true; normal_shutdown = $true } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'crash-check.json') -Encoding utf8
    Write-Output $root
} finally {
    foreach ($child in $children) {
        if (-not $child.HasExited) { $child.Kill($true); $child.WaitForExit(5000) | Out-Null }
        $child.Dispose()
    }
}

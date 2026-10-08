param([Parameter(Mandatory = $true)][string]$Root)
$processes = @(Get-CimInstance Win32_Process -Filter "Name = 'Phraseback.Engine.exe'" |
    Where-Object { $_.CommandLine -like "*$Root*" })
if ($processes.Count -ne 1) { throw "Expected one engine for the isolated root; found $($processes.Count)" }
$processes[0].ProcessId

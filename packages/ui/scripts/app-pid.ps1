param([Parameter(Mandatory = $true)][string]$Root)
$processes = @(Get-CimInstance Win32_Process -Filter "Name = 'Phraseback.exe'" |
    Where-Object { $_.CommandLine -like "*$Root*" -and $_.CommandLine -notlike '*--type=*' })
if ($processes.Count -ne 1) { throw "Expected one app main process for the isolated root; found $($processes.Count)" }
$processes[0].ProcessId

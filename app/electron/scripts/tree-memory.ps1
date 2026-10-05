param([Parameter(Mandatory = $true)][string]$Root, [string]$Name = 'Phraseback.exe')
$all = @(Get-CimInstance Win32_Process)
$main = @($all | Where-Object { $_.Name -eq $Name -and $_.CommandLine -like "*$Root*" -and $_.CommandLine -notlike '*--type=*' })
if ($main.Count -ne 1) { throw "Expected one isolated app main process; found $($main.Count)" }
$ids = [System.Collections.Generic.HashSet[int]]::new()
[void]$ids.Add([int]$main[0].ProcessId)
do {
    $count = $ids.Count
    foreach ($child in $all) { if ($ids.Contains([int]$child.ParentProcessId)) { [void]$ids.Add([int]$child.ProcessId) } }
} while ($ids.Count -gt $count)
$processes = @($ids | ForEach-Object { Get-Process -Id $_ -ErrorAction SilentlyContinue })
@{ process_count = $processes.Count; working_set_bytes = ($processes | Measure-Object WorkingSet64 -Sum).Sum; private_bytes = ($processes | Measure-Object PrivateMemorySize64 -Sum).Sum } | ConvertTo-Json -Compress

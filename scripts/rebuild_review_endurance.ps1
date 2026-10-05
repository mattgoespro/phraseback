# Real CPU Skia decode, no visible window or desktop capture.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Set-Location $repo
$dotnet = Join-Path $env:LOCALAPPDATA 'FlowRecorderDev/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
& "$PSScriptRoot/rebuild.ps1" -Task Build
$root = Join-Path $repo ('.tmp/rebuild/review-endurance-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $root | Out-Null
$previousEnvironment = @{}
foreach ($name in @('FLOW_REVIEW_ENDURANCE','FLOW_REVIEW_ENDURANCE_OUTPUT','FLOW_REBUILD_TEST_ROOT','FLOW_REBUILD_ENGINE')) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$env:FLOW_REVIEW_ENDURANCE = '1'
$env:FLOW_REVIEW_ENDURANCE_OUTPUT = $root
$env:FLOW_REBUILD_TEST_ROOT = Join-Path $root 'fixtures'
$env:FLOW_REBUILD_ENGINE = Join-Path $repo '.tmp/rebuild/prototype-payload/Phraseback.Engine.exe'
try {
    & $dotnet test app/shell/Phraseback.Tests/Phraseback.Tests.csproj -p:RestoreLockedMode=true --filter FullyQualifiedName~ReviewEnduranceTests --blame-hang-timeout 4m --logger trx --results-directory $root
    if ($LASTEXITCODE -ne 0) { throw "Review endurance failed; evidence: $root" }
    @{ configuration='Debug'; renderer='Skia CPU, headless platform'; cpu=@(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores); os=(Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version); memory_bytes=(Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'machine.json') -Encoding utf8
    $report = Get-Content (Join-Path $root 'review-endurance.json') -Raw | ConvertFrom-Json
    $report | Select-Object passed,seeks,frame_count,growth_bytes,allowed_growth_bytes | ConvertTo-Json
    Write-Output "Evidence: $root"
}
finally {
    foreach ($name in $previousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
}

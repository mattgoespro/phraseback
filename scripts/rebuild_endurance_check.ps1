# Non-interactive, synthetic CPU pipeline check. Never captures the desktop.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Set-Location $repo
$cargo = Join-Path $env:USERPROFILE '.cargo/bin/cargo.exe'
$env:CARGO_TARGET_DIR = Join-Path $repo '.tmp/rebuild/target'
$root = Join-Path $repo ('.tmp/rebuild/endurance-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
New-Item -ItemType File -Path (Join-Path $root '.flow-recorder-development') | Out-Null
$env:FLOW_ENDURANCE_ROOT = $root
$buildLines = & $cargo '+1.98.1-x86_64-pc-windows-msvc' test --manifest-path packages/engine/Cargo.toml --locked --release --target x86_64-pc-windows-msvc -p flow-engine --no-run --message-format=json
if ($LASTEXITCODE -ne 0) { throw 'Endurance test build failed' }
$artifacts = @($buildLines | ForEach-Object { $_ | ConvertFrom-Json } | Where-Object { $_.reason -eq 'compiler-artifact' -and $_.target.name -eq 'phraseback-engine' -and $_.profile.test -and $_.executable })
if ($artifacts.Count -ne 1) { throw 'Expected exactly one engine test executable' }
$stdout = Join-Path $root 'test.stdout.log'
$stderr = Join-Path $root 'test.stderr.log'
$process = Start-Process -FilePath $artifacts[0].executable -ArgumentList @('--exact','recording::tests::synthetic_capture_endurance','--ignored','--nocapture') -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
$samples = [Collections.Generic.List[object]]::new()
$clock = [Diagnostics.Stopwatch]::StartNew()
$timedOut = $false
while (-not $process.WaitForExit(250)) {
    $process.Refresh()
    $samples.Add(@{ elapsed_ms=$clock.Elapsed.TotalMilliseconds; private_bytes=$process.PrivateMemorySize64; working_set_bytes=$process.WorkingSet64 })
    if ($clock.Elapsed.TotalSeconds -gt 330) {
        $timedOut = $true
        $process.Kill($true)
        [void]$process.WaitForExit(5000)
        break
    }
}
$samples | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'memory-samples.json') -Encoding utf8
@{ cpu=@(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores); memory_bytes=(Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory; os=(Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version); configuration='release'; native_capture=$false } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'machine.json') -Encoding utf8
Write-Output "Evidence: $root"
if ($timedOut) { throw 'Synthetic endurance exceeded its deadline; only its owned process tree was stopped' }
if ($process.ExitCode -ne 0) { Get-Content $stdout; Get-Content $stderr; throw 'Synthetic pipeline test failed' }
$result = Get-Content -LiteralPath (Join-Path $root 'endurance-result.json') -Raw | ConvertFrom-Json
if (-not $result.passed -or $samples.Count -lt 12) { throw 'Insufficient successful endurance evidence' }
function Percentile95($values) {
    $sorted = @($values | Sort-Object)
    return $sorted[[Math]::Max(0, [int][Math]::Ceiling($sorted.Count * .95) - 1)]
}
$third = [int][Math]::Floor($samples.Count / 3)
$middle = Percentile95 ($samples | Select-Object -Skip $third -First $third | ForEach-Object private_bytes)
$last = Percentile95 ($samples | Select-Object -Skip ($third * 2) | ForEach-Object private_bytes)
# Operational regression threshold, not a claim that encoder/GPU allocations have
# been individually attributed. Preserve raw private and working-set measurements.
$growth = $last - $middle
$report = @{ passed=($growth -le 32MB); samples=$samples.Count; middle_private_p95=$middle; final_private_p95=$last; growth_bytes=$growth; allowed_growth_bytes=32MB; peak_private_bytes=($samples.private_bytes | Measure-Object -Maximum).Maximum; frames=$result.frames; limitations=$result.limitations }
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'memory-result.json') -Encoding utf8
$report | ConvertTo-Json
if (-not $report.passed) { throw 'CPU process memory did not plateau within the 32 MiB post-warmup growth threshold' }

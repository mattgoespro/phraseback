# Non-interactive encoder-only allocation check. No desktop capture or user data.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Set-Location $repo
$cargo = Join-Path $env:USERPROFILE '.cargo/bin/cargo.exe'
$root = Join-Path $repo ('.tmp/rebuild/encoder-memory-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
New-Item -ItemType File -Path (Join-Path $root '.flow-recorder-development') | Out-Null
$oldRoot = $env:FLOW_ENCODER_MEMORY_ROOT
$oldTarget = $env:CARGO_TARGET_DIR
try {
    $env:FLOW_ENCODER_MEMORY_ROOT = $root
    $env:CARGO_TARGET_DIR = Join-Path $repo '.tmp/rebuild/target'
    & $cargo '+1.98.1-x86_64-pc-windows-msvc' test --manifest-path app/Cargo.toml --locked --release --target x86_64-pc-windows-msvc -p flow-engine recording::tests::encoder_heap_is_bounded_on_noisy_full_display_frames -- --exact --ignored
    if ($LASTEXITCODE -ne 0) { throw 'Encoder heap check failed' }
    $report = Get-Content -LiteralPath (Join-Path $root 'encoder-memory.json') -Raw | ConvertFrom-Json
    if (-not $report.passed -or $report.results.Count -ne 18) { throw 'Incomplete encoder evidence' }
    @{ configuration='release'; cpu=@(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores); memory_bytes=(Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory; native_capture=$false } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'machine.json') -Encoding utf8
    "Evidence: $root"
    $report.results | Select-Object width,height,noisy,repeat,@{n='peak_heap_bytes';e={$_.rust_heap.peak_bytes}},elapsed_ms
} finally {
    $env:FLOW_ENCODER_MEMORY_ROOT = $oldRoot
    $env:CARGO_TARGET_DIR = $oldTarget
}

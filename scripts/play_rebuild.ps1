param([string]$Payload)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (-not $Payload) {
    $pointer = Join-Path $repo '.tmp/rebuild/playground-payload.json'
    if (-not (Test-Path -LiteralPath $pointer)) { throw 'No packaged preview exists yet. Run scripts/rebuild_package.ps1 first.' }
    $Payload = (Get-Content -LiteralPath $pointer -Raw | ConvertFrom-Json).payload
}
$payloadPath = (Resolve-Path -LiteralPath $Payload).Path
foreach ($file in @('Phraseback.exe','Phraseback.Engine.exe','coreclr.dll','ffmpeg.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $payloadPath $file))) { throw "Preview payload is incomplete: $file" }
}
# This stable playground is never reset by the fixture generator or package builder.
# The development marker is deliberately not added to the user's normal data root.
$data = Join-Path $repo '.tmp/rebuild/playground-data'
New-Item -ItemType Directory -Force -Path (Join-Path $data 'sessions') | Out-Null
New-Item -ItemType File -Force -Path (Join-Path $data '.flow-recorder-development') | Out-Null
Start-Process -FilePath (Join-Path $payloadPath 'Phraseback.exe') -ArgumentList @('--data-root', ('"' + $data + '"')) -WorkingDirectory $payloadPath -WindowStyle Hidden
Write-Output "Preview launched. Recordings and model downloads stay in $data"

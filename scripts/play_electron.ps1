param([string]$Payload = '', [string]$DataRoot = '')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Split-Path $PSScriptRoot -Parent)).Path
if (-not $Payload) { $Payload = Join-Path $repo 'dist/Phraseback' }
$payloadPath = (Resolve-Path -LiteralPath $Payload).Path
foreach ($file in @('Phraseback.exe','Phraseback.Engine.exe','ffmpeg.exe','resources/app.asar')) {
    if (-not (Test-Path -LiteralPath (Join-Path $payloadPath $file) -PathType Leaf)) { throw "Electron playground payload is incomplete: $file" }
}
if (-not $DataRoot) { $DataRoot = Join-Path $repo '.tmp/rebuild/playground-data' }
$data = [IO.Path]::GetFullPath($DataRoot)
$temporaryRoot = [IO.Path]::GetFullPath((Join-Path $repo '.tmp')) + [IO.Path]::DirectorySeparatorChar
if (-not $data.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Playground data must remain under .tmp.' }
$marker = Join-Path $data '.flow-recorder-development'
if ((Test-Path -LiteralPath $data) -and -not (Test-Path -LiteralPath $marker)) { throw "Existing playground root has no development marker: $data" }
New-Item -ItemType Directory -Force -Path (Join-Path $data 'sessions') | Out-Null
if (-not (Test-Path -LiteralPath $marker)) { New-Item -ItemType File -Path $marker | Out-Null }
$process = Start-Process -FilePath (Join-Path $payloadPath 'Phraseback.exe') -ArgumentList @('--data-root', ('"' + $data + '"')) -WorkingDirectory $payloadPath -PassThru
Write-Output "Electron playground started (PID $($process.Id)): $data"

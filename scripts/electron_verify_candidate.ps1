param([Parameter(Mandatory=$true)][string]$Candidate)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Split-Path $PSScriptRoot -Parent)).Path
$candidatePath = (Resolve-Path -LiteralPath $Candidate).Path
$allowed = [IO.Path]::GetFullPath((Join-Path $repo '.tmp/electron')) + [IO.Path]::DirectorySeparatorChar
if (-not $candidatePath.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Candidate must be inside the repository .tmp/electron directory.' }
$summaryPath = Join-Path $candidatePath 'package-summary.json'
$provenancePath = Join-Path $candidatePath 'build-provenance.json'
$summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
$provenance = Get-Content -LiteralPath $provenancePath -Raw | ConvertFrom-Json
if ($summary.ui -ne 'electron' -or $provenance.ui -ne 'electron' -or $summary.candidate -ne $true -or $provenance.candidate -ne $true) { throw 'Candidate metadata does not identify an Electron candidate.' }
$payload = Join-Path $candidatePath 'Phraseback-win32-x64'
if (-not (Test-Path -LiteralPath $payload -PathType Container)) { throw 'Candidate payload is missing.' }
$inventory = @($provenance.files)
if ($inventory.Count -eq 0) { throw 'Candidate inventory is empty.' }
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $inventory) {
    $file = [IO.Path]::GetFullPath((Join-Path $payload $entry.path))
    if (-not $file.StartsWith($payload + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or -not $seen.Add($file)) { throw "Invalid or duplicate inventory path: $($entry.path)" }
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing payload file: $($entry.path)" }
    if ((Get-Item -LiteralPath $file).Length -ne $entry.bytes -or (Get-FileHash -LiteralPath $file).Hash -ne $entry.sha256) { throw "Payload changed: $($entry.path)" }
}
$actual = @(Get-ChildItem -LiteralPath $payload -File -Recurse)
if ($actual.Count -ne $inventory.Count) { throw 'Payload contains files outside its inventory.' }
foreach ($required in @('Phraseback.exe','Phraseback.Engine.exe','ffmpeg.exe','resources/app.asar','THIRD-PARTY-NOTICES.md','licenses/electron/LICENSE','licenses/cargo/components.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $payload $required) -PathType Leaf)) { throw "Required packaged component missing: $required" }
}
$installer = [IO.Path]::GetFullPath((Join-Path $candidatePath $summary.installer.name))
if (-not $installer.StartsWith($candidatePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Installer name escaped candidate directory.' }
if (-not (Test-Path -LiteralPath $installer -PathType Leaf) -or (Get-Item -LiteralPath $installer).Length -ne $summary.installer.bytes -or (Get-FileHash -LiteralPath $installer).Hash -ne $summary.installer.sha256) { throw 'Installer changed or is missing.' }
[ordered]@{
    candidate = $candidatePath
    summary_sha256 = (Get-FileHash -LiteralPath $summaryPath).Hash.ToLowerInvariant()
    installer_sha256 = $summary.installer.sha256
    engine_sha256 = $provenance.engine_sha256
    payload_files = $inventory.Count
    verified = $true
} | ConvertTo-Json -Compress

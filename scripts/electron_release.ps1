param([Parameter(Mandatory=$true)][string]$Candidate, [Parameter(Mandatory=$true)][string]$AcceptanceFile)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Split-Path $PSScriptRoot -Parent)).Path
$verification = & (Join-Path $PSScriptRoot 'electron_verify_candidate.ps1') -Candidate $Candidate | ConvertFrom-Json
$candidatePath = $verification.candidate
$acceptancePath = (Resolve-Path -LiteralPath $AcceptanceFile).Path
$acceptance = Get-Content -LiteralPath $acceptancePath -Raw | ConvertFrom-Json
$requiredChecks = @('rust_and_electron_tests','packaged_native','installer_rollback','visual_review','notices')
if ($acceptance.phase -eq 'migration_cutover') { $requiredChecks += 'avalonia_regression' }
elseif ($acceptance.phase -ne 'electron_update') { throw 'Acceptance phase must be migration_cutover or electron_update.' }
if ($acceptance.candidate_summary_sha256 -ne $verification.summary_sha256 -or $acceptance.redistribution_approved -ne $true) { throw 'Acceptance does not approve this exact candidate and its redistribution.' }
foreach ($check in $requiredChecks) { if (@($acceptance.passed_checks) -notcontains $check) { throw "Acceptance is missing: $check" } }
$dist = [IO.Path]::GetFullPath((Join-Path $repo 'dist'))
$backupRoot = [IO.Path]::GetFullPath((Join-Path $repo 'reference-backups'))
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$canonical = Join-Path $dist 'Phraseback'
$incoming = Join-Path $dist ('incoming-electron-' + [Guid]::NewGuid().ToString('N'))
$incomingInstaller = Join-Path $dist ('.incoming-installer-' + [Guid]::NewGuid().ToString('N') + '.exe')
$rollback = Join-Path $backupRoot ('previous-release-' + [Guid]::NewGuid().ToString('N'))
if (-not $incoming.StartsWith($dist + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    -not $rollback.StartsWith($backupRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Release paths escaped the workspace.' }
$summary = Get-Content -LiteralPath (Join-Path $candidatePath 'package-summary.json') -Raw | ConvertFrom-Json
$installerName = $summary.installer.name
$publishedInstaller = Join-Path $dist $installerName
$metadata = @('build-provenance.json','package-summary.json','release-acceptance.json','runtime-imports.json')
if ((Test-Path -LiteralPath $canonical) -and (Test-Path -LiteralPath (Join-Path $dist 'package-summary.json')) -and
    (Get-FileHash -LiteralPath (Join-Path $dist 'package-summary.json')).Hash -eq $verification.summary_sha256) {
    foreach ($entry in (Get-Content -LiteralPath (Join-Path $candidatePath 'build-provenance.json') -Raw | ConvertFrom-Json).files) {
        if ((Get-FileHash -LiteralPath (Join-Path $canonical $entry.path)).Hash -ne $entry.sha256) { throw "Published payload changed: $($entry.path)" }
    }
    if ((Get-FileHash -LiteralPath $publishedInstaller).Hash -ne $summary.installer.sha256) { throw 'Published installer changed.' }
    if ((Get-FileHash -LiteralPath (Join-Path $dist 'release-acceptance.json')).Hash -ne
        (Get-FileHash -LiteralPath $acceptancePath).Hash) { throw 'Published acceptance differs from the requested acceptance.' }
    Write-Output "Electron release already published and verified: $canonical"
    return
}
Copy-Item -LiteralPath (Join-Path $candidatePath 'Phraseback-win32-x64') -Destination $incoming -Recurse
Copy-Item -LiteralPath (Join-Path $candidatePath $installerName) -Destination $incomingInstaller
foreach ($entry in (Get-Content -LiteralPath (Join-Path $candidatePath 'build-provenance.json') -Raw | ConvertFrom-Json).files) {
    if ((Get-FileHash -LiteralPath (Join-Path $incoming $entry.path)).Hash -ne $entry.sha256) { throw "Staged payload changed: $($entry.path)" }
}
if ((Get-FileHash -LiteralPath $incomingInstaller).Hash -ne $summary.installer.sha256) { throw 'Staged installer changed.' }
New-Item -ItemType Directory -Path $rollback -Force | Out-Null
$oldCanonical = Join-Path $rollback 'Phraseback'
$oldInstaller = Join-Path $rollback $installerName
$newCanonicalPublished = $false
$newInstallerPublished = $false
$newMetadata = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
try {
    if (Test-Path -LiteralPath $canonical) { Move-Item -LiteralPath $canonical -Destination $oldCanonical }
    if (Test-Path -LiteralPath $publishedInstaller) { Move-Item -LiteralPath $publishedInstaller -Destination $oldInstaller }
    foreach ($name in $metadata) {
        $current = Join-Path $dist $name
        if (Test-Path -LiteralPath $current) { Move-Item -LiteralPath $current -Destination (Join-Path $rollback $name) }
    }
    Move-Item -LiteralPath $incoming -Destination $canonical
    $newCanonicalPublished = $true
    Move-Item -LiteralPath $incomingInstaller -Destination $publishedInstaller
    $newInstallerPublished = $true
    [void]$newMetadata.Add('build-provenance.json')
    Copy-Item -LiteralPath (Join-Path $candidatePath 'build-provenance.json') -Destination $dist
    [void]$newMetadata.Add('package-summary.json')
    Copy-Item -LiteralPath (Join-Path $candidatePath 'package-summary.json') -Destination $dist
    [void]$newMetadata.Add('release-acceptance.json')
    Copy-Item -LiteralPath $acceptancePath -Destination (Join-Path $dist 'release-acceptance.json')
    foreach ($entry in (Get-Content -LiteralPath (Join-Path $dist 'build-provenance.json') -Raw | ConvertFrom-Json).files) {
        if ((Get-FileHash -LiteralPath (Join-Path $canonical $entry.path)).Hash -ne $entry.sha256) { throw "Published payload changed: $($entry.path)" }
    }
    if ((Get-FileHash -LiteralPath $publishedInstaller).Hash -ne $summary.installer.sha256) { throw 'Published installer changed.' }
} catch {
    if ($newCanonicalPublished -and (Test-Path -LiteralPath $canonical)) { Move-Item -LiteralPath $canonical -Destination (Join-Path $rollback 'failed-Phraseback') }
    if ($newInstallerPublished -and (Test-Path -LiteralPath $publishedInstaller)) { Move-Item -LiteralPath $publishedInstaller -Destination (Join-Path $rollback 'failed-installer.exe') }
    foreach ($name in $metadata) {
        $current = Join-Path $dist $name
        if ($newMetadata.Contains($name) -and (Test-Path -LiteralPath $current)) { Move-Item -LiteralPath $current -Destination (Join-Path $rollback ('failed-' + $name)) }
        $old = Join-Path $rollback $name
        if (Test-Path -LiteralPath $old) { Move-Item -LiteralPath $old -Destination $current }
    }
    if (Test-Path -LiteralPath $oldCanonical) { Move-Item -LiteralPath $oldCanonical -Destination $canonical }
    if (Test-Path -LiteralPath $oldInstaller) { Move-Item -LiteralPath $oldInstaller -Destination $publishedInstaller }
    throw
}
Write-Output "Electron release published: $canonical"
Write-Output "Previous release retained: $rollback"

param([Parameter(Mandatory=$true)][string]$Candidate, [string]$PreviousCandidate, [switch]$SkipNativeCapture)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Split-Path $PSScriptRoot -Parent)).Path
$package = (Resolve-Path -LiteralPath $Candidate).Path
$electronRoot = [IO.Path]::GetFullPath((Join-Path $repo '.tmp/electron')) + [IO.Path]::DirectorySeparatorChar
$allowed = $electronRoot
if (-not $package.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Only an isolated development candidate may be checked' }
$installer = Join-Path $package 'Phraseback-Setup-0.2.0-win-x64.exe'
if (-not (Test-Path -LiteralPath $installer)) { throw 'Candidate installer is missing' }
$previousPackage = $null
if ($PreviousCandidate) {
    $previousPackage = (Resolve-Path -LiteralPath $PreviousCandidate).Path
    if (-not $previousPackage.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Previous candidate must be isolated too' }
    if ($previousPackage -eq $package) { throw 'Upgrade check needs two distinct candidates' }
    if (-not (Test-Path -LiteralPath (Join-Path $previousPackage 'Phraseback-Setup-0.2.0-win-x64.exe'))) { throw 'Previous installer missing' }
}
$registry = 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/{AA7BD096-84EA-43DC-92CB-4E8E85F82401}_is1'
if (Test-Path -LiteralPath $registry) { throw 'An existing installation owns this application identity; use a clean test machine' }
$checkRoot = Join-Path $allowed ('installer-check-' + [Guid]::NewGuid().ToString('N'))
$installation = [IO.Path]::GetFullPath((Join-Path $checkRoot 'installed'))
if (-not $installation.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Installation destination escaped the disposable workspace' }
New-Item -ItemType Directory -Path $checkRoot | Out-Null
$preservedData = Join-Path $checkRoot 'data'
New-Item -ItemType Directory -Path $preservedData | Out-Null
# Synthetic data only: never point an installer acceptance run at the user's library.
$env:FLOW_RECORDER_DATA = $preservedData
if ((Get-Content -LiteralPath (Join-Path $package 'package-summary.json') -Raw | ConvertFrom-Json).ui -ne 'electron') { throw 'Only Electron installers are supported' }
& {
    $fixtureSource = Join-Path $repo 'packages/engine/fixtures/studio'
    $fixtureTarget = Join-Path $preservedData 'sessions/synthetic-settings'
    $manifest = Get-Content -LiteralPath (Join-Path $fixtureSource 'manifest.json') -Raw | ConvertFrom-Json -AsHashtable
    foreach ($entry in $manifest.GetEnumerator()) {
        $sourceFile = [IO.Path]::GetFullPath((Join-Path $fixtureSource $entry.Key))
        if (-not $sourceFile.StartsWith($fixtureSource + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            (Get-FileHash -LiteralPath $sourceFile).Hash -ne $entry.Value) { throw "Synthetic fixture checksum mismatch: $($entry.Key)" }
        $targetFile = [IO.Path]::GetFullPath((Join-Path $fixtureTarget $entry.Key))
        if (-not $targetFile.StartsWith($fixtureTarget + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Synthetic fixture path escaped destination' }
        New-Item -ItemType Directory -Path (Split-Path $targetFile -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $sourceFile -Destination $targetFile
    }
    [IO.File]::WriteAllText((Join-Path $preservedData '.flow-recorder-development'), 'isolated installer fixture')
}
$modelFixture = Join-Path $preservedData 'models/installer-preservation-fixture.bin'
New-Item -ItemType Directory -Path (Split-Path $modelFixture -Parent) -Force | Out-Null
[IO.File]::WriteAllBytes($modelFixture, [byte[]](0,1,2,3,255)) # Synthetic bytes, not model weights.
$dataHashes = @(Get-ChildItem -LiteralPath $preservedData -File -Recurse | ForEach-Object {
    [ordered]@{path=[IO.Path]::GetRelativePath($preservedData,$_.FullName);sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
})
function AssertDataPreserved {
    $current = @(Get-ChildItem -LiteralPath $preservedData -File -Recurse)
    if ($current.Count -ne $dataHashes.Count) { throw 'Installer changed synthetic data inventory' }
    foreach ($entry in $dataHashes) {
        if ((Get-FileHash -LiteralPath (Join-Path $preservedData $entry.path)).Hash -ne $entry.sha256) { throw "Installer changed synthetic data: $($entry.path)" }
    }
}
function AssertInstalledPayload([string]$sourcePackage) {
    $inventory = Get-Content -LiteralPath (Join-Path $sourcePackage 'build-provenance.json') -Raw | ConvertFrom-Json
    foreach ($entry in $inventory.files) {
        if ((Get-FileHash -LiteralPath (Join-Path $installation $entry.path)).Hash -ne $entry.sha256) { throw "Installed bytes differ: $($entry.path)" }
    }
    $registered = (Get-ItemProperty -LiteralPath $registry).InstallLocation
    if ([IO.Path]::GetFullPath($registered).TrimEnd('\') -ne $installation.TrimEnd('\')) { throw 'Installer registered a different destination' }
    AssertDataPreserved
}
function CheckedProcess([string]$exe, [string[]]$arguments) {
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(60000)) { throw "Installer exceeded its deadline. Inspect the process before retrying: $($process.Id)" }
    if ($process.ExitCode -ne 0) { throw "Installer returned $($process.ExitCode)" }
}
$installArgs = @('/CURRENTUSER','/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/NOICONS',('/DIR="' + $installation + '"'))
if ($previousPackage) {
    CheckedProcess (Join-Path $previousPackage 'Phraseback-Setup-0.2.0-win-x64.exe') ($installArgs + ('/LOG="' + (Join-Path $checkRoot 'previous-install.log') + '"'))
    AssertInstalledPayload $previousPackage
}
CheckedProcess $installer ($installArgs + ('/LOG="' + (Join-Path $checkRoot 'install.log') + '"'))
AssertInstalledPayload $package
if (-not $SkipNativeCapture) {
    Push-Location (Join-Path $repo 'packages/ui')
    try {
        & node scripts/native-smoke.mjs --packaged (Join-Path $installation 'Phraseback.exe')
        if ($LASTEXITCODE -ne 0) { throw 'Installed Electron smoke failed' }
    } finally { Pop-Location }
}
if ($previousPackage) {
    CheckedProcess (Join-Path $previousPackage 'Phraseback-Setup-0.2.0-win-x64.exe') ($installArgs + ('/LOG="' + (Join-Path $checkRoot 'rollback.log') + '"'))
    AssertInstalledPayload $previousPackage
    CheckedProcess $installer ($installArgs + ('/LOG="' + (Join-Path $checkRoot 'restore-current.log') + '"'))
    AssertInstalledPayload $package
}
# Same-version reinstall exercises replacement ownership, not a different-version upgrade.
CheckedProcess $installer ($installArgs + ('/LOG="' + (Join-Path $checkRoot 'reinstall.log') + '"'))
AssertInstalledPayload $package
$uninstaller = Join-Path $installation 'unins000.exe'
if (-not (Test-Path -LiteralPath $uninstaller)) { throw 'Installed uninstaller missing' }
CheckedProcess $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="' + (Join-Path $checkRoot 'uninstall.log') + '"'))
if (Test-Path -LiteralPath (Join-Path $installation 'Phraseback.exe')) { throw 'Installed application remained after uninstall' }
if (Test-Path -LiteralPath $registry) { throw 'Uninstall registration remained after uninstall' }
AssertDataPreserved
[ordered]@{passed=$true;candidate=$package;previous_candidate=$previousPackage;binary_upgrade_rollback=[bool]$previousPackage;native_capture=(-not $SkipNativeCapture);preserved_files=$dataHashes;scope='Local installer replacement/rollback; both candidates use version 0.2.0. No claim of different-version application data migration.'} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $checkRoot 'installer-check.json') -Encoding utf8
Write-Output "Passed local install, payload verification, reinstall, uninstall and synthetic-data preservation: $checkRoot"
if ($previousPackage) { Write-Output 'Different-build upgrade, rollback and forward restoration verified. Both candidates identify as 0.2.0; this is not a cross-version schema migration test.' }

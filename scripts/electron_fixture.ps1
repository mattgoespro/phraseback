param([string]$Root = '')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Split-Path $PSScriptRoot -Parent)).Path
if (-not $Root) { $Root = Join-Path $repo '.tmp/electron/prototype-data' }
$rootPath = [IO.Path]::GetFullPath($Root)
$temporaryRoot = [IO.Path]::GetFullPath((Join-Path $repo '.tmp')) + [IO.Path]::DirectorySeparatorChar
if (-not $rootPath.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'The Electron fixture must stay under the repository .tmp directory.' }
$source = [IO.Path]::GetFullPath((Join-Path $repo 'packages/engine/fixtures/studio'))
$manifest = Get-Content -LiteralPath (Join-Path $source 'manifest.json') -Raw | ConvertFrom-Json -AsHashtable
$marker = Join-Path $rootPath '.flow-recorder-development'
if (Test-Path -LiteralPath $rootPath) {
    if (-not (Test-Path -LiteralPath $marker)) { throw "Existing fixture has no development marker: $rootPath" }
    $existingSession = Join-Path $rootPath 'sessions/synthetic-settings'
    foreach ($entry in $manifest.GetEnumerator()) {
        $existingFile = [IO.Path]::GetFullPath((Join-Path $existingSession $entry.Key))
        if (-not $existingFile.StartsWith($existingSession + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $existingFile -PathType Leaf) -or (Get-FileHash -LiteralPath $existingFile).Hash -ne $entry.Value) { throw "Existing fixture checksum mismatch: $($entry.Key)" }
    }
    Write-Output $rootPath
    return
}
$stage = $rootPath + '.incoming-' + [Guid]::NewGuid().ToString('N')
if (-not $stage.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture staging path escaped the repository .tmp directory.' }
New-Item -ItemType Directory -Path (Split-Path $stage -Parent) -Force | Out-Null
New-Item -ItemType Directory -Path $stage | Out-Null
try {
    $session = Join-Path $stage 'sessions/synthetic-settings'
    foreach ($entry in $manifest.GetEnumerator()) {
        $sourceFile = [IO.Path]::GetFullPath((Join-Path $source $entry.Key))
        $targetFile = [IO.Path]::GetFullPath((Join-Path $session $entry.Key))
        if (-not $sourceFile.StartsWith($source + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            -not $targetFile.StartsWith($session + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture path escaped its directory.' }
        if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf) -or (Get-FileHash -LiteralPath $sourceFile).Hash -ne $entry.Value) { throw "Fixture checksum mismatch: $($entry.Key)" }
        New-Item -ItemType Directory -Path (Split-Path $targetFile -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $sourceFile -Destination $targetFile
    }
    [IO.File]::WriteAllText((Join-Path $stage '.flow-recorder-development'), 'isolated Electron fixture')
    Move-Item -LiteralPath $stage -Destination $rootPath
} finally {
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
}
Write-Output $rootPath

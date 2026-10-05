param([Parameter(Mandatory)][string]$Payload, [Parameter(Mandatory)][string]$Report)
$ErrorActionPreference = 'Stop'
$payloadPath = (Resolve-Path -LiteralPath $Payload).Path
$vswhere = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Microsoft Visual Studio/Installer/vswhere.exe'
$installation = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if ($LASTEXITCODE -ne 0 -or -not $installation) { throw 'MSVC tools required for native import validation.' }
$compilerVersion = (Get-Content -LiteralPath (Join-Path $installation 'VC/Auxiliary/Build/Microsoft.VCToolsVersion.default.txt') -Raw).Trim()
$dumpbin = Join-Path $installation "VC/Tools/MSVC/$compilerVersion/bin/Hostx64/x64/dumpbin.exe"
$records = [Collections.Generic.List[object]]::new()
$violations = [Collections.Generic.List[string]]::new()
# /dependents includes ordinary and delay-loaded imports. Managed-only DLLs
# legitimately have none. This does not enumerate runtime LoadLibrary calls.
foreach ($file in Get-ChildItem -LiteralPath $payloadPath -File -Recurse | Where-Object Extension -in @('.exe','.dll') | Sort-Object FullName) {
    $dump = & $dumpbin /nologo /dependents $file.FullName
    if ($LASTEXITCODE -ne 0) { throw "Cannot inspect native imports: $($file.Name)" }
    $imports = @($dump | ForEach-Object {
        if ($_ -match '^\s+([A-Za-z0-9_.-]+\.dll)\s*$') { $Matches[1].ToLowerInvariant() }
    } | Sort-Object -Unique)
    $relative = [IO.Path]::GetRelativePath($payloadPath, $file.FullName)
    foreach ($dependency in $imports) {
        if ($dependency -match '^(vcruntime|msvcp|msvcr|concrt|vcomp)\d.*\.dll$') {
            $violations.Add("${relative}: $dependency")
        }
    }
    $records.Add([ordered]@{path=$relative;sha256=(Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant();imports=$imports})
}
if ($records.Count -eq 0) { throw 'No executable payload to inspect.' }
if ($violations.Count -ne 0) { throw ('External Visual C++ runtime dependency: ' + ($violations -join '; ')) }
[ordered]@{
    scope='PE normal/delay imports; not dynamic LoadLibrary resolution or clean-machine acceptance'
    external_visual_cpp_runtime=$false
    binaries=$records.ToArray()
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Report -Encoding utf8
Write-Output "Native import check passed for $($records.Count) binaries; no external Visual C++ runtime imports."

param([Parameter(Mandatory)][string]$Payload)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$payloadPath = (Resolve-Path -LiteralPath $Payload).Path
$cargo = Join-Path $env:USERPROFILE '.cargo/bin/cargo.exe'
$rustc = Join-Path $env:USERPROFILE '.cargo/bin/rustc.exe'
$toolchain = '+1.98.1-x86_64-pc-windows-msvc'
$metadataText = & $cargo $toolchain metadata --manifest-path (Join-Path $repo 'packages/engine/Cargo.toml') --locked --format-version 1 --filter-platform x86_64-pc-windows-msvc
if ($LASTEXITCODE -ne 0) { throw 'Cannot inventory Cargo dependencies' }
$metadata = $metadataText | ConvertFrom-Json
$sysroot = & $rustc $toolchain --print sysroot
if ($LASTEXITCODE -ne 0) { throw 'Cannot locate Rust standard library notices' }
$output = Join-Path $payloadPath 'licenses/cargo'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$items = [Collections.Generic.List[object]]::new()
function CopyNotice([string]$source, [string]$relative) {
    $destination = Join-Path $output $relative
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination
    return @{ path = ('licenses/cargo/' + $relative.Replace('\','/')); sha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() }
}
foreach ($package in $metadata.packages | Where-Object source | Sort-Object name,version) {
    $directory = Split-Path $package.manifest_path -Parent
    $files = @(Get-ChildItem -LiteralPath $directory -File -Recurse | Where-Object { $_.Name -match '^(licen[sc]e|copying|notice|copyright)([._-]|$)' -and $_.Extension -notin @('.rs','.js','.ts','.exe','.dll') })
    if ($package.license_file) { $files += Get-Item -LiteralPath (Join-Path $directory $package.license_file) }
    $files = @($files | Sort-Object FullName -Unique)
    if ($files.Count -eq 0) { throw "Missing Cargo license text: $($package.name)/$($package.version)" }
    $records = @($files | ForEach-Object { CopyNotice $_.FullName ("$($package.name)-$($package.version)/" + [IO.Path]::GetRelativePath($directory,$_.FullName)) })
    $items.Add(@{ name = $package.name; version = $package.version; declared_license = $package.license; source = $package.source; files = $records })
}
$stdlib = Join-Path $sysroot 'share/doc/rust/COPYRIGHT-library.html'
if (-not (Test-Path -LiteralPath $stdlib)) { throw 'Missing Rust standard library copyright text' }
$items.Add(@{ name = 'Rust standard library'; version = $toolchain.TrimStart('+'); files = @((CopyNotice $stdlib 'runtime/COPYRIGHT-library.html')) })
@{ scope = 'Conservative resolved Cargo packages for the Windows MSVC target'; components = @($items.ToArray()) } |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'components.json') -Encoding utf8
Write-Output "Collected Rust notices for $($items.Count) components"

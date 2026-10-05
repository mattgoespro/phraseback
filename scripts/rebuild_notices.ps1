# Conservative inventory: includes resolved build/platform dependencies as well as runtime packages.
# Presence of notices is evidence, not an automatic redistribution approval.
param([Parameter(Mandatory)][string]$Payload, [string]$Target = 'x86_64-pc-windows-msvc', [string]$Toolchain = '1.98.1-x86_64-pc-windows-msvc')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$payloadPath = (Resolve-Path -LiteralPath $Payload).Path
$output = Join-Path $payloadPath 'licenses'
if (Test-Path -LiteralPath $output) { throw 'Notices output already exists; use a fresh candidate payload.' }
$assetsPath = Join-Path $repo '.tmp/rebuild/dotnet/obj/Phraseback.App/project.assets.json'
$assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -AsHashtable
$deps = Get-Content -LiteralPath (Join-Path $payloadPath 'Phraseback.deps.json') -Raw | ConvertFrom-Json -AsHashtable
$sourceRoot = Join-Path $repo 'app/packaging/licenses'
$sources = Get-Content -LiteralPath (Join-Path $sourceRoot 'sources.json') -Raw | ConvertFrom-Json -AsHashtable
$cargo = Join-Path $env:USERPROFILE '.cargo/bin/cargo.exe'
$rustc = Join-Path $env:USERPROFILE '.cargo/bin/rustc.exe'
$metadataText = & $cargo "+$Toolchain" metadata --manifest-path (Join-Path $repo 'app/Cargo.toml') --locked --format-version 1 --filter-platform $Target
if ($LASTEXITCODE -ne 0) { throw 'Cargo notice inventory failed.' }
$metadata = $metadataText | ConvertFrom-Json -AsHashtable
$sysroot = & $rustc "+$Toolchain" --print sysroot
if ($LASTEXITCODE -ne 0) { throw 'Cannot locate Rust runtime notices.' }
$items = [Collections.Generic.List[object]]::new()
$ffmpegPin = Get-Content -LiteralPath (Join-Path $repo 'app/packaging/ffmpeg.lock.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath (Join-Path $payloadPath 'ffmpeg.exe')).Hash -ne $ffmpegPin.binary_sha256) { throw 'FFmpeg binary provenance mismatch.' }
foreach ($file in $ffmpegPin.source_files) {
    if ((Get-FileHash -LiteralPath (Join-Path $payloadPath ('ffmpeg-source/' + $file.path))).Hash -ne $file.sha256) { throw "FFmpeg source-kit mismatch: $($file.path)" }
}
$textPattern = '^(licen[sc]e|copying|notice|copyright|third[-_]party[-_]notices)([._-]|$)'
function NoticeFiles([string]$directory) {
    return @(Get-ChildItem -LiteralPath $directory -File -Recurse | Where-Object { $_.Name -match $textPattern -and $_.Extension -notin @('.rs','.cs','.dll','.so','.exe','.a','.lib','.py','.js','.ts') } | Sort-Object FullName)
}
function CopyNotice([string]$source, [string]$relative) {
    $destination = Join-Path $output $relative
    New-Item -ItemType Directory -Force (Split-Path $destination -Parent) | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination
    return [ordered]@{path=$relative.Replace('\','/'); sha256=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()}
}
function PackageRoot([string]$relative) {
    foreach ($base in $assets.packageFolders.Keys) {
        $candidate = Join-Path $base $relative
        if (Test-Path -LiteralPath $candidate -PathType Container) { return $candidate }
    }
    throw "Restored package not found: $relative"
}
# Validate checked-in supplements before publishing any output.
foreach ($file in $sources.files.GetEnumerator()) {
    $path = Join-Path $sourceRoot ('upstream/' + $file.Key)
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Value.sha256) { throw "Notice checksum mismatch: $($file.Key)" }
}
# Connect the shipped native bytes to the exact restored package whose notices
# are collected below. Native runtime-pack paths are flattened in the deps file.
$nativeArtifacts = [Collections.Generic.List[object]]::new()
foreach ($targetEntry in $deps.targets.Values) {
    foreach ($package in $targetEntry.GetEnumerator()) {
        if (-not $package.Value.native) { continue }
        $packageKey = $package.Key -replace '^runtimepack\.', ''
        $nativeName,$nativeVersion = $packageKey.Split('/')
        $nativeRoot = PackageRoot ($packageKey.ToLowerInvariant())
        foreach ($relative in $package.Value.native.Keys | Sort-Object) {
            if ([IO.Path]::GetExtension($relative) -notin @('.dll','.exe')) { continue }
            $fileName = [IO.Path]::GetFileName($relative)
            $source = Join-Path $nativeRoot $relative
            if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
                $source = Join-Path $nativeRoot ('runtimes/win-x64/native/' + $fileName)
            }
            $shipped = Join-Path $payloadPath $fileName
            if (-not (Test-Path -LiteralPath $shipped -PathType Leaf)) { throw "Native payload missing: $fileName" }
            $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
            if ((Get-FileHash -LiteralPath $shipped -Algorithm SHA256).Hash -ne $sourceHash) {
                throw "Native package provenance mismatch: $fileName"
            }
            $nativeArtifacts.Add([ordered]@{path=$fileName;sha256=$sourceHash;component=$nativeName;version=$nativeVersion;package_path=[IO.Path]::GetRelativePath($nativeRoot,$source).Replace('\','/')})
        }
    }
}
New-Item -ItemType Directory -Path $output | Out-Null
foreach ($p in $metadata.packages | Where-Object source | Sort-Object name,version) {
    $directory = Split-Path $p.manifest_path -Parent
    $files = @(NoticeFiles $directory)
    if ($p.license_file) {
        $declared = Get-Item -LiteralPath (Join-Path $directory $p.license_file)
        $files = @($files + $declared | Sort-Object FullName -Unique)
    }
    if ($files.Count -eq 0) { throw "Missing Cargo license text: $($p.name)/$($p.version)" }
    $records = @($files | ForEach-Object { CopyNotice $_.FullName "cargo/$($p.name)-$($p.version)/$([IO.Path]::GetRelativePath($directory,$_.FullName))" })
    $items.Add([ordered]@{ecosystem='cargo';name=$p.name;version=$p.version;license=$p.license;source=$p.source;repository=$p.repository;files=$records})
}
foreach ($pair in $assets.libraries.GetEnumerator() | Sort-Object Key) {
    if ($pair.Value.type -ne 'package') { continue }
    $name,$version = $pair.Key.Split('/')
    $directory = PackageRoot $pair.Value.path
    [xml]$spec = Get-Content -LiteralPath (Get-ChildItem -LiteralPath $directory -Filter '*.nuspec' | Select-Object -First 1).FullName -Raw
    $records = [Collections.Generic.List[object]]::new()
    foreach ($file in @(NoticeFiles $directory)) { $records.Add((CopyNotice $file.FullName "nuget/$name-$version/$([IO.Path]::GetRelativePath($directory,$file.FullName))")) }
    foreach ($supplement in $sources.nuget_supplements | Where-Object { $_.packages -contains $name -and $_.version -eq $version }) {
        if ($spec.package.metadata.repository.commit -ne $supplement.commit) { throw "NuGet source commit changed: $name/$version" }
        foreach ($file in $supplement.files) {
            $record = CopyNotice (Join-Path $sourceRoot "upstream/$file") "nuget/$name-$version/upstream/$file"
            $record['source_url'] = $sources.files[$file].url
            $records.Add($record)
        }
    }
    if ($records.Count -eq 0) { throw "Missing NuGet license text: $name/$version" }
    $items.Add([ordered]@{ecosystem='nuget';name=$name;version=$version;license=$spec.package.metadata.license.InnerText;package_sha512=$pair.Value.sha512;repository=$spec.package.metadata.repository.url;commit=$spec.package.metadata.repository.commit;files=@($records.ToArray())})
}
# Runtime packs are in the self-contained .deps file, not necessarily assets.libraries.
foreach ($key in $deps.libraries.Keys | Where-Object { $_ -like 'runtimepack.*' } | Sort-Object) {
    $name,$version = $key.Substring('runtimepack.'.Length).Split('/')
    $directory = PackageRoot ($name.ToLowerInvariant() + '/' + $version)
    $files = @(NoticeFiles $directory)
    if ($files.Count -eq 0) { throw "Missing .NET runtime notices: $key" }
    $records = @($files | ForEach-Object { CopyNotice $_.FullName "runtime/$name-$version/$([IO.Path]::GetRelativePath($directory,$_.FullName))" })
    $items.Add([ordered]@{ecosystem='dotnet-runtime';name=$name;version=$version;files=$records})
}
$libraryNotices = Join-Path $sysroot 'share/doc/rust/COPYRIGHT-library.html'
$items.Add([ordered]@{ecosystem='rust-runtime';name='Rust standard library';version=$Toolchain;files=@((CopyNotice $libraryNotices 'runtime/rust/COPYRIGHT-library.html'))})
Copy-Item -LiteralPath (Join-Path $sourceRoot 'sources.json') -Destination $output
Copy-Item -LiteralPath (Join-Path $sourceRoot 'NATIVE-REVIEW.md') -Destination $output
$items.ToArray() | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $output 'components.json') -Encoding utf8
$nativeArtifacts.ToArray() | Sort-Object path | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'native-artifacts.json') -Encoding utf8
[ordered]@{
    target=$Target; redistribution_approved=$false; component_count=$items.Count
    inventory_scope='Conservative resolved Cargo/NuGet package set, including build-only and other-platform packages; plus shipped runtime packs.'
    ffmpeg_source_materials='Verified pinned PNG/GIF encoder sources, recipe and runtime notices in ffmpeg-source/'
    native_notice_review='NATIVE-REVIEW.md; scoped engineering coverage for documented pins, not legal certification'
    pending=@('Release acceptance and final redistribution review')
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'status.json') -Encoding utf8
Write-Output "Collected notices for $($items.Count) components at $output; redistribution approval remains false."

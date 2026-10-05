param([Parameter(Mandatory)][string]$Payload)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$scratch = Join-Path $repo ('.tmp/rebuild/notices-test-' + [Guid]::NewGuid().ToString('N'))
$collector = Join-Path $PSScriptRoot 'rebuild_notices.ps1'
$hashSets = @()
function CopyNativePayload([string]$destination) {
    $deps = Get-Content -LiteralPath (Join-Path $Payload 'Phraseback.deps.json') -Raw | ConvertFrom-Json -AsHashtable
    foreach ($target in $deps.targets.Values) {
        foreach ($package in $target.Values) {
            if (-not $package.native) { continue }
            foreach ($relative in $package.native.Keys) {
                if ([IO.Path]::GetExtension($relative) -in @('.dll','.exe')) {
                    Copy-Item -LiteralPath (Join-Path $Payload ([IO.Path]::GetFileName($relative))) -Destination $destination
                }
            }
        }
    }
}
foreach ($name in @('first','second')) {
    $candidate = Join-Path $scratch $name
    New-Item -ItemType Directory -Path $candidate -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $Payload 'Phraseback.deps.json') -Destination $candidate
    Copy-Item -LiteralPath (Join-Path $Payload 'ffmpeg.exe') -Destination $candidate
    Copy-Item -LiteralPath (Join-Path $Payload 'ffmpeg-source') -Destination $candidate -Recurse
    CopyNativePayload $candidate
    & $collector -Payload $candidate
    $root = Join-Path $candidate 'licenses'
    $components = Get-Content -LiteralPath (Join-Path $root 'components.json') -Raw | ConvertFrom-Json
    if ($components.Count -lt 1) { throw 'Empty license inventory.' }
    foreach ($component in $components) {
        if ($component.files.Count -lt 1) { throw "Component lacks license texts: $($component.name)" }
        foreach ($file in $component.files) {
            if ((Get-FileHash -LiteralPath (Join-Path $root $file.path)).Hash -ne $file.sha256) { throw 'License output does not match inventory hash.' }
        }
    }
    foreach ($nameRequired in @('Rust standard library','Microsoft.NETCore.App.Runtime.win-x64','Avalonia.Fonts.Inter','Avalonia.Angle.Windows.Natives','SkiaSharp.NativeAssets.Win32','HarfBuzzSharp.NativeAssets.Win32','windows-capture')) {
        if ($components.name -notcontains $nameRequired) { throw "Required component missing: $nameRequired" }
    }
    $nativeArtifacts = Get-Content -LiteralPath (Join-Path $root 'native-artifacts.json') -Raw | ConvertFrom-Json
    foreach ($required in @('av_libglesv2.dll','libSkiaSharp.dll','libHarfBuzzSharp.dll','coreclr.dll','hostfxr.dll')) {
        if ($nativeArtifacts.path -notcontains $required) { throw "Native provenance omitted: $required" }
    }
    foreach ($artifact in $nativeArtifacts) {
        $component = @($components | Where-Object { $_.name -eq $artifact.component -and ($_.version -eq $artifact.version -or $_.ecosystem -eq 'dotnet-runtime') })
        if ($component.Count -ne 1) { throw "Native artifact has no unique notice owner: $($artifact.path)" }
        if ((Get-FileHash -LiteralPath (Join-Path $candidate $artifact.path)).Hash -ne $artifact.sha256) { throw 'Native artifact inventory hash mismatch' }
    }
    foreach ($vendorName in @('SkiaSharp.NativeAssets.Win32','HarfBuzzSharp.NativeAssets.Win32')) {
        $vendor = $components | Where-Object name -eq $vendorName
        $notice = $vendor.files | Where-Object path -match '/THIRD-PARTY-NOTICES.txt$'
        if (-not $notice) { throw "Vendor third-party notices omitted: $vendorName" }
        $noticeText = Get-Content -LiteralPath (Join-Path $root $notice.path) -Raw
        foreach ($section in @('HarfBuzz','skia','libpng','freetype','ICU','libjpeg-turbo','libwebp','zlib')) {
            if ($noticeText -notmatch "(?m)^# $([regex]::Escape($section))\r?$") { throw "Vendor notice section omitted: $section" }
        }
    }
    $inter = $components | Where-Object name -eq 'Avalonia.Fonts.Inter'
    if (-not ($inter.files.path -match 'Inter-LICENSE.txt$')) { throw 'Inter font license omitted.' }
    $angle = $components | Where-Object name -eq 'Avalonia.Angle.Windows.Natives'
    foreach ($dependency in @('xxhash','abseil','astcenc','rapidjson','zlib','libcxx','libcxxabi','ceval','Chromium','Bison')) {
        if (-not ($angle.files.path -match "ANGLE-$dependency-LICENSE")) { throw "ANGLE dependency notice omitted: $dependency" }
    }
    $parserNotice = $angle.files | Where-Object path -match 'ANGLE-Bison-NOTICE.txt$'
    if (-not $parserNotice) { throw 'Bison parser exception notice omitted.' }
    $parserText = Get-Content -LiteralPath (Join-Path $root $parserNotice.path) -Raw
    if ($parserText -notmatch 'special exception' -or $parserText -notmatch 'terms of your choice') { throw 'Bison parser exception text lost.' }
    $khronos = $angle.files | Where-Object path -match 'ANGLE-Khronos-NOTICE.txt$'
    $khronosApache = $angle.files | Where-Object path -match 'ANGLE-Khronos-Apache-LICENSE.txt$'
    if (-not $khronos -or -not $khronosApache) { throw 'Khronos header notices or Apache terms omitted.' }
    $khronosText = Get-Content -LiteralPath (Join-Path $root $khronos.path) -Raw
    foreach ($header in @('EGL/egl.h','EGL/eglext.h','EGL/eglplatform.h','GLES/egl.h','GLES/gl.h','GLES/glext.h','GLES/glplatform.h','GLES2/gl2.h','GLES2/gl2ext.h','GLES2/gl2platform.h','GLES3/gl3.h','GLES3/gl31.h','GLES3/gl32.h','GLES3/gl3platform.h','KHR/khrplatform.h')) {
        if ($khronosText -notmatch "(?m)^include/$([regex]::Escape($header))\r?$") { throw "Khronos attribution omitted: $header" }
    }
    foreach ($terms in @('Permission is hereby granted','SPDX-License-Identifier: MIT','SPDX-License-Identifier: Apache-2.0','Copyright 2013-2026')) {
        if (-not $khronosText.Contains($terms)) { throw "Khronos terms/attribution lost: $terms" }
    }
    $status = Get-Content -LiteralPath (Join-Path $root 'status.json') -Raw | ConvertFrom-Json
    if (-not (Test-Path -LiteralPath (Join-Path $root 'NATIVE-REVIEW.md')) -or -not $status.native_notice_review) { throw 'Native review scope omitted.' }
    if ($status.redistribution_approved -or $status.pending.Count -eq 0) { throw 'Unfinished redistribution gates were incorrectly closed.' }
    $hashSet = @(Get-ChildItem -LiteralPath $root -File -Recurse | ForEach-Object { [IO.Path]::GetRelativePath($root,$_.FullName) + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash } | Sort-Object)
    $hashSets += ,$hashSet
    $refused = $false
    try { & $collector -Payload $candidate | Out-Null } catch { $refused = $_.Exception.Message -like '*already exists*' }
    if (-not $refused) { throw 'Collector must refuse to overwrite an existing notice bundle.' }
}
if (Compare-Object $hashSets[0] $hashSets[1]) { throw 'Notice collection was not deterministic.' }
$corrupt = Join-Path $scratch 'mismatched-native'
New-Item -ItemType Directory -Path $corrupt | Out-Null
foreach ($file in @('Phraseback.deps.json','ffmpeg.exe')) { Copy-Item -LiteralPath (Join-Path $Payload $file) -Destination $corrupt }
Copy-Item -LiteralPath (Join-Path $Payload 'ffmpeg-source') -Destination $corrupt -Recurse
CopyNativePayload $corrupt
[IO.File]::WriteAllBytes((Join-Path $corrupt 'libSkiaSharp.dll'), [byte[]](0,1,2,3))
$rejected = $false
try { & $collector -Payload $corrupt | Out-Null } catch { $rejected = $_.Exception.Message -like 'Native package provenance mismatch:*' }
if (-not $rejected -or (Test-Path -LiteralPath (Join-Path $corrupt 'licenses'))) { throw 'Mismatched native bytes must fail before notice publication.' }
Write-Output 'Notices checks passed: component/vendor coverage, file hashes, native package provenance and tamper rejection, pinned font license, deterministic output, overwrite refusal, and explicit open redistribution gates.'

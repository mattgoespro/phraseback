# Build a candidate from verified source; never changes the shipping encoder pin.
param(
    [string]$CompilerArchive,
    [string]$SourceCache,
    [switch]$Offline
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$pinPath = Join-Path $repo 'app/packaging/ffmpeg-source.lock.json'
$pin = Get-Content -LiteralPath $pinPath -Raw | ConvertFrom-Json
if (-not $SourceCache) { $SourceCache = Join-Path $repo '.tmp/rebuild/ffmpeg-source-inputs' }
if (-not $CompilerArchive) { $CompilerArchive = Join-Path $repo '.tmp/tooling/llvm-mingw.zip' }
function Verified([string]$path, [string]$hash) {
    (Test-Path -LiteralPath $path -PathType Leaf) -and
        ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $hash)
}
if (-not (Test-Path -LiteralPath $CompilerArchive) -and -not $Offline) {
    New-Item -ItemType Directory -Force (Split-Path $CompilerArchive -Parent) | Out-Null
    $download = $CompilerArchive + '.' + [Guid]::NewGuid().ToString('N') + '.download'
    try {
        Invoke-WebRequest -Uri $pin.compiler.url -OutFile $download
        if (-not (Verified $download $pin.compiler.sha256)) { throw 'Compiler download checksum mismatch.' }
        Move-Item -LiteralPath $download -Destination $CompilerArchive
    }
    finally { if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download } }
}
if (-not (Verified $CompilerArchive $pin.compiler.sha256)) {
    throw "Compiler archive missing or checksum mismatch. Obtain $($pin.compiler.url)"
}
$bash = Join-Path $env:ProgramFiles 'Git/bin/bash.exe'
if (-not (Test-Path -LiteralPath $bash)) { throw 'Git for Windows Bash is required.' }
New-Item -ItemType Directory -Force $SourceCache | Out-Null
$archives = @()
foreach ($source in @($pin.ffmpeg, $pin.zlib)) {
    $archive = Join-Path $SourceCache ([IO.Path]::GetFileName(([Uri]$source.url).AbsolutePath))
    if (-not (Verified $archive $source.sha256)) {
        if ($Offline) { throw "Verified source unavailable offline: $archive" }
        $temporary = Join-Path $SourceCache ([Guid]::NewGuid().ToString('N') + '.download')
        try {
            Invoke-WebRequest -Uri $source.url -OutFile $temporary
            if (-not (Verified $temporary $source.sha256)) { throw 'Source checksum mismatch.' }
            Move-Item -LiteralPath $temporary -Destination $archive -Force
        }
        finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
    }
    $archives += (Resolve-Path -LiteralPath $archive).Path
}
$stage = Join-Path $repo ('.tmp/rebuild/ffmpeg-source-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $stage | Out-Null
# Each build starts with fresh trees from archives whose hashes were verified above.
foreach ($archive in @($CompilerArchive) + $archives) {
    & tar -xf $archive -C $stage
    if ($LASTEXITCODE -ne 0) { throw "Extraction failed: $archive" }
}
$recipe = Join-Path $stage 'build_minimal_ffmpeg.sh'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'build_minimal_ffmpeg.sh') -Destination $recipe
$previousPath = $env:PATH
try {
    $env:PATH = (Join-Path $stage "$($pin.compiler.version)/bin") + ';' + $previousPath
    Push-Location $stage
    try {
        & $bash ./build_minimal_ffmpeg.sh *> (Join-Path $stage 'build.log')
        if ($LASTEXITCODE -ne 0) { throw "Encoder build failed; inspect $stage/build.log" }
    }
    finally { Pop-Location }
}
finally { $env:PATH = $previousPath }
$binary = Join-Path $stage 'ffmpeg-7.1/ffmpeg.exe'
$kit = Join-Path $stage 'corresponding-source'
New-Item -ItemType Directory $kit | Out-Null
foreach ($archive in $archives) { Copy-Item -LiteralPath $archive -Destination $kit }
Copy-Item -LiteralPath $pinPath,$recipe -Destination $kit
Copy-Item -LiteralPath (Join-Path $stage "$($pin.compiler.version)/LICENSE.TXT") -Destination (Join-Path $kit 'LLVM-MinGW-LICENSE.TXT')
$runtimeNotices = Join-Path $kit 'mingw-runtime-notices'
New-Item -ItemType Directory $runtimeNotices | Out-Null
Get-ChildItem -LiteralPath (Join-Path $stage "$($pin.compiler.version)/x86_64-w64-mingw32/share/mingw32") -File -Filter 'COPYING*' |
    Copy-Item -Destination $runtimeNotices
Copy-Item -LiteralPath (Join-Path $stage 'ffmpeg-7.1/COPYING.LGPLv2.1') -Destination $kit
Copy-Item -LiteralPath (Join-Path $stage 'ffmpeg-7.1/LICENSE.md') -Destination (Join-Path $kit 'FFmpeg-LICENSE.md')
Copy-Item -LiteralPath (Join-Path $stage 'zlib-1.3.2/zlib.h') -Destination (Join-Path $kit 'zlib-license-and-header.h')
Copy-Item -LiteralPath (Join-Path $repo 'app/packaging/FFMPEG-SOURCE-BUILD.md') -Destination $kit
[ordered]@{
    candidate = $true
    redistribution_approved = $false
    binary_sha256 = (Get-FileHash -LiteralPath $binary).Hash.ToLowerInvariant()
    compiler_archive_sha256 = $pin.compiler.sha256
    recipe_sha256 = (Get-FileHash -LiteralPath $recipe).Hash.ToLowerInvariant()
    source_manifest_sha256 = (Get-FileHash -LiteralPath $pinPath).Hash.ToLowerInvariant()
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'provenance.json') -Encoding utf8
Write-Output $stage

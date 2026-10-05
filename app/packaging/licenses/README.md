# Notice provenance

`sources.json` pins texts omitted from restored NuGet packages to their declared
upstream commits. The collector checks their SHA-256 hashes before copying them.
Other texts come from restored Cargo/NuGet package contents and the pinned Rust runtime.
Git attributes retain the exact downloaded bytes.

Avalonia's embedded Inter-Regular.ttf identifies `Version 3.019;git-0a5106e0b` in
its name table. The corresponding Inter license is pinned to the resolved full commit
`0a5106e0bde18df09374066bf3a7998e3546307d`. The font inspected from Avalonia's own pinned
source commit has SHA-256 `41ab0f707a2bfab8133ccdfcdab52282f5f79e5751f43a264805451c7bb95fb8`.

`scripts/rebuild_notices.ps1` runs during candidate packaging. It fails if a resolved
component has no collected license text, or a supplement's checksum/source commit
changes. The output manifest includes each copied file's hash. The inventory is
deliberately conservative and includes build-only and other-platform packages.

The collector also hashes the 18 native executables/libraries declared in the
Windows payload's `.deps.json` against their restored NuGet/runtime-pack originals.
`native-artifacts.json` records the owning package, version, relative source path
and hash. A missing or mismatched native file fails before notice publication.
This manifest covers declared native dependencies, not the generated app host,
Rust engine or separately verified FFmpeg executable.

MSVC engine builds statically link the C runtime through the target-specific
root `.cargo/config.toml`. Candidate packaging inspects normal and delay-loaded
imports of every shipped EXE/DLL and rejects external Visual C++ runtime imports.
The package-stage `runtime-imports.json` records inspected binary hashes and
imports. This does not inspect dynamic LoadLibrary calls and does not substitute
for a clean Windows installation test. Windows system DLLs remain OS dependencies.

SkiaSharp.NativeAssets.Win32 3.119.4 and HarfBuzzSharp.NativeAssets.Win32 8.3.1.3
each ship LICENSE.txt and a vendor THIRD-PARTY-NOTICES.txt. Both are retained
unchanged. The vendor bundle includes Skia, HarfBuzz, PNG, FreeType, ICU, JPEG,
WebP, zlib and further sections; tests require the principal section markers and
the native-binary-to-notice-owner mapping. This verifies vendor notice delivery,
not an independent audit of every statically linked source file.

FFmpeg's pinned source/build/runtime-notice kit is now verified and bundled.
The scoped native-notice engineering review is recorded in NATIVE-REVIEW.md.
It closes the coverage review for the documented pins, not overall release
acceptance or an independent legal certification. The package and notices status
retain `redistribution_approved: false` while the candidate is unreleased.

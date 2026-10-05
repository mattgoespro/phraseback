# Windows native notice coverage review

Reviewed 2026-09-25 for the pinned MSVC win-x64 candidate. This is an engineering
review of notice delivery and provenance, not a legal opinion or a full source
audit of vendor binaries. Package release acceptance is a separate decision.

## Shipping native dependencies

`native-artifacts.json` maps 18 payload binaries to restored package bytes:

- Avalonia.Angle.Windows.Natives 2.1.27548.20260419: `av_libglesv2.dll`.
- SkiaSharp.NativeAssets.Win32 3.119.4: `libSkiaSharp.dll`.
- HarfBuzzSharp.NativeAssets.Win32 8.3.1.3: `libHarfBuzzSharp.dll`.
- Microsoft.NETCore.App.Runtime.win-x64 10.0.12: 15 runtime binaries.

The collector preserves the native vendors' LICENSE and THIRD-PARTY-NOTICES
files. Skia/HarfBuzz notices include their PNG, FreeType, ICU, JPEG, WebP and zlib
attributions. The .NET runtime carries its vendor license and third-party notice
bundle. Binary-to-package hashes and copied-notice hashes are checked separately.
This review relies on those vendors' notice inventories; it does not assert that
every source file embedded in their binaries has been independently examined.

## ANGLE supplement coverage

Source commit: `1c89805903c1482166356d3b950d474973180e61`, matching the NuGet package
repository metadata. Review inputs at that commit:

- `.github/workflows/build.yml`: Windows x64 libGLESv2 build; D3D9, Vulkan, GL,
  Metal, WGPU, SwiftShader and null backends explicitly disabled.
- `BUILD.gn`, `gni/angle.gni`, `src/libGLESv2.gni`, `src/compiler.gni`, `DEPS`:
  native library/source dependencies and platform conditions.
- `src/common/spirv/BUILD.gn`: the unconditional `angle_spirv_headers` target
  contains ANGLE's own `spirv_types.h`; third-party SPIR-V libraries are in the
  additional backend targets, not that target alone.
- `util/BUILD.gn`: StackWalker is a separate utility target, not grounds for
  treating every vendored Windows utility as part of the shipped libGLESv2.

The retained supplement set covers xxhash, ceval, astcenc, RapidJSON, Abseil,
Chromium zlib/compression utilities, libc++, libc++abi and Chromium attribution.
Generated-parser Bison terms include the parser exception, not just GPL text.
The 15 EGL/GLES/KHR public headers retain their individual notices, with Apache
terms where applicable. `sources.json` records the exact source URLs, revisions
and hashes for these texts. The collector rejects changed supplement bytes or
NuGet repository commits. Tests require each supplement family and header notice.

Backend-specific Vulkan/Metal/WGPU dependencies, Linux X11 helpers, samples and
test tools are not inferred to ship merely because they appear in the source
tree. The review is limited to the documented vendor Windows build. Revisit it
when the native package, source commit, backend configuration or payload changes.

## Other bundled native code

The Rust dependency inventory preserves nested notices too: ring's Apache/ISC,
BoringSSL, once_cell and fiat texts, and unicode-ident's additional Unicode text
are retained rather than reduced to one top-level license. Rust standard-library
notices are included. Static MSVC linkage removes an external runtime prerequisite;
it does not turn Windows system libraries into bundled components.

FFmpeg remains a separate executable. Its pinned source archives, rebuild recipe,
FFmpeg/zlib terms and LLVM/MinGW runtime notices are shipped in `ffmpeg-source`.
Its provenance is checked separately from NuGet native dependencies. Model weights
and llama.cpp downloads are not part of the application payload.

## Outcome and repeatable evidence

No missing notice family was identified in this scoped review. The engineering
native-notice coverage item is closed for these pins. Keep vendor attribution,
checksums and source materials in every distributed payload. This does not declare
the overall candidate released or approve unrelated future binaries.

Run `scripts/test_rebuild_notices.ps1 -Payload <candidate>/Phraseback` to verify
delivery, native ownership, hashes, deterministic collection and corruption
rejection. `scripts/rebuild_runtime_imports.ps1` separately verifies ordinary and
delay-loaded imports; it is not a dynamic LoadLibrary or source-license audit.

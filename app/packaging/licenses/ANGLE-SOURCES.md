# ANGLE notice evidence

Package: Avalonia.Angle.Windows.Natives 2.1.27548.20260419.
Its NuGet repository metadata identifies AvaloniaUI/angle commit
`1c89805903c1482166356d3b950d474973180e61`. The collector checks this commit before
adding the supplemental texts; each retained file is hash-pinned in sources.json.

Inspected upstream files at that exact commit:

- `.github/workflows/build.yml`: Windows x64 release build; Vulkan, desktop GL,
  null, D3D9, Metal, WebGPU and SwiftShader backends explicitly disabled.
- `BUILD.gn`: common xxHash/Abseil, compression, ASTC image utilities and optional
  RapidJSON references. `src/libGLESv2.gni` also names the vendored ceval header.
- `gni/angle.gni`: standalone desktop ASTC and available RapidJSON defaults.
- `DEPS`: exact dependency revisions and Chromium revision used for source lookup.

Collected notices cover xxHash, ceval, Abseil, ASTC encoder, RapidJSON, Chromium's
zlib fork, Chromium-authored compression utilities, libc++ and libc++abi. libc++
and libc++abi are retained conservatively; this is not a claim that every object
from those libraries is linked. The package already supplies ANGLE's own license.
Files are UTF-8/LF with a final newline; hashes identify the retained text bytes.

The generated `glslang_tab_autogen.cpp` parser's opening copyright, terms, special
exception and attribution comments are retained in ANGLE-Bison-NOTICE.txt. The
matching GPLv3 text comes from this revision's parser-skeleton license file.
Both are collected together and the exception's presence is regression-tested.

The recursive source tree at this commit was inspected for additional license
files and public header notices. Fifteen Khronos-attributed headers under
include/EGL, GLES, GLES2, GLES3 and KHR have separate MIT-style or Apache-2.0
terms. Their original opening comment blocks, including copyright dates, are
retained in ANGLE-Khronos-NOTICE.txt with exact per-header source URLs. The full
Apache-2.0 text is bundled alongside it (identical to the already pinned Abseil
license text). All fifteen header attributions are required by packaging tests.

The source configuration disables OpenCL by default; the vendor release disables
Vulkan, which also disables the Vulkan-only overlay. The overlay font license,
Vulkan shader helper licenses, X11-only libXNVCtrl and the optional Mesa build
are not evidence of additional enabled Windows D3D11 dependencies. The inspected
generated lexer files identify the ANGLE BSD license, unlike the Bison parser's
explicit exception handled above. These observations do not replace a complete
resolved build-graph audit.

This closes the identified missing-text cases, not the full transitive dependency
audit. Enabled third-party source/header coverage and
other native vendor notices still need review before final redistribution approval.
No rendering backend, dependency version or binary was changed.

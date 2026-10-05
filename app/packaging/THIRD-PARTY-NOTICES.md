# Third-party components

This development candidate is not approved for redistribution.

The bundled `licenses/components.json` records collected license/notice texts and
their SHA-256 hashes. It conservatively includes resolved build-only and non-Windows
dependencies as well as runtime packages. `licenses/status.json` records outstanding
redistribution checks; collecting notices does not itself close those checks.

- .NET 10: MIT, https://github.com/dotnet/runtime
- Avalonia 12.1.2: MIT, https://github.com/AvaloniaUI/Avalonia
- Inter font: SIL Open Font License, https://github.com/rsms/inter
- Rust dependencies: exact versions and package sources are recorded in Cargo.lock
  and the package's cargo-metadata.json. Package license texts and Rust standard-library
  notices are collected under `licenses/`. .NET runtime notices, native NuGet notices,
  Avalonia's upstream attributions, and the exact Inter 3.019 font license are included.
- FFmpeg 7.1: minimal PNG/GIF build, LGPL version 2.1 or later, with zlib 1.3.2
  under the zlib license. Built from unmodified upstream archives using pinned
  LLVM-MinGW; no GPL codecs or network protocols are enabled. Executable SHA-256:
  dab18c1afc480374a252e67a0529d098e9b6af61bbf14f5d393f8b019b796f6f.
  The `ffmpeg-source/` folder contains the exact FFmpeg/zlib source archives,
  build recipe, source/compiler pins, FFmpeg/zlib licenses and LLVM/MinGW runtime
  notices. `ffmpeg.lock.json` records their verified hashes. Build instructions
  are in `ffmpeg-source/BUILD.md`. Users may replace the separate ffmpeg.exe;
  it is not linked into the Rust engine or Avalonia shell.
- Development LLVM-MinGW builds additionally contain libunwind.dll from
  llvm-mingw 20260908. Its required notices must accompany any distributed fallback
  build. The planned Windows release uses MSVC instead.

Qwen3-VL models and llama.cpp are downloaded only on request, are not bundled in
the application payload, and retain the exact asset manifests in model-presets.json.
llama.cpp runtime archives retain their upstream license files when extracted.

Inno Setup 6.7.3 is a build tool, not an application dependency. Its installer was
verified with Windows Authenticode (Pyrsys B.V.) and SHA-256
9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732.
Review the tool's licensing terms before commercial use: https://jrsoftware.org/isinfo.php

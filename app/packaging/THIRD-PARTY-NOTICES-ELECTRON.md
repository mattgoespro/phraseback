# Phraseback Electron third-party components

The `licenses/` directory contains component inventories and copied license
texts for this package. The release acceptance record identifies the exact
package approved for redistribution.

- Electron and Chromium: bundled runtime notices are in
  `licenses/electron/LICENSE` and `licenses/electron/LICENSES.chromium.html`.
- JavaScript packages: `licenses/npm/components.json` records installed package
  versions, declared licenses, copied license texts, and SHA-256 hashes. It
  conservatively includes build-only packages. Entries without bundled text
  must be classified before release; build-only packages are not shipped as
  runtime code merely because they appear in the lockfile.
- Rust engine and standard library: `licenses/cargo/components.json` records
  the resolved Windows MSVC Cargo inventory and copied texts. The engine is
  built from the pinned `app/Cargo.lock` and Rust 1.98.1 toolchain.
- FFmpeg 7.1: the separate minimal PNG/GIF encoder, its exact source archives,
  build recipe, license texts, and pins are in `ffmpeg-source/` and
  `ffmpeg.lock.json`. It is not linked into Electron or the Rust engine.
- Local model and llama.cpp assets are downloaded only when the user chooses
  installation. They are not part of this application payload.
- Inno Setup 6.7.3 is an installer build tool, not a runtime dependency. Its
  pinned compiler checksum is in `app/packaging/inno.lock.json` in source.

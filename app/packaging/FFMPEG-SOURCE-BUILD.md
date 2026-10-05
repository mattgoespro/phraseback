# Minimal PNG/GIF encoder

`ffmpeg.lock.json` pins the source-built encoder and every required source-kit
file. `scripts/rebuild_ffmpeg.ps1` verifies both before returning a cached binary,
or builds from pinned archives on a cold online cache. The application remains a
release candidate; successful compilation does not approve redistribution.

The existing export uses PNG decoding, PNG palette generation and GIF encoding.
The candidate retains FFmpeg 7.1 but removes unrelated codecs, network protocols
and external multimedia libraries. Its only explicitly enabled external library
is zlib. No application export algorithm or format changes are intended.

## Build

The checked entry point is `scripts/rebuild_ffmpeg_source.ps1 -Offline` after the
archives below are cached. Without `-Offline`, missing source/compiler archives can
be downloaded and hash-checked. The compiler archive defaults to
`.tmp/tooling/llvm-mingw.zip`; its pinned hash matches the upstream release asset
digest. Every invocation extracts fresh source and compiler trees under `.tmp/`,
builds, and retains a source kit plus provenance. It does not update the launcher
or encoder pin. Fast rejection checks: `scripts/test_rebuild_ffmpeg_source.ps1`.
Pass `-BuildDirectory <completed-build>` to that test to validate retained archive
hashes, executable/recipe provenance and required source-kit notices. The source
kit includes upstream archives unchanged, FFmpeg and zlib license material, LLVM
and MinGW runtime notices. This is not an assertion of legal approval.

1. Download the two archives in `ffmpeg-source.lock.json` and verify their SHA-256
   hashes before extraction. Keep both original archives for source distribution.
2. Extract into a fresh repository-local `.tmp/` directory so it contains
   `ffmpeg-7.1/` and `zlib-1.3.2/`. Do not reuse a source tree configured with other
   codecs or dependencies.
3. Add LLVM-MinGW `20260908-ucrt-x86_64/bin` to PATH. Run
   `scripts/build_minimal_ffmpeg.sh` using Git Bash, with that extracted parent as
   the working directory. The recipe uses the compiler's `mingw32-make`, four
   compilation workers, and local compiler temporary files.
4. The output is `ffmpeg-7.1/ffmpeg.exe`. Keep the build log, `ffbuild/config.log`,
   `ffbuild/config.mak`, compiler identity and executable hash as provenance.

## Verification and distribution

- Test with the actual packaged engine in a separate `.tmp/` payload, not by
  replacing an executable used by the normal launcher.
- Preserve export timing, long holds, original PNG copies, Unicode paths,
  cancellation and transactional publication.
- Inspect imported DLLs and exercise the candidate without compiler directories
  on PATH. A successful development-machine launch is not clean-machine proof.
- Preserve the pinned compiler distribution identity when updating the recipe.
- Packaging includes matching source archives, build recipe and applicable license
  materials, including compiler runtime notices, under `ffmpeg-source/`.
- Two fresh offline source/compiler builds matched the initial candidate byte for
  byte. This is local repeatability evidence, not a universal cross-host guarantee.

Existing portable candidates retain their original encoders for rollback. No
existing recording or original screenshot is rewritten by this change. Original
PNGs remain authoritative; GIFs are derived, palette-limited exports.

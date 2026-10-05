# Electron migration acceptance — 2026-10-04

The active Windows release is Electron with the existing Rust engine and
version-1 project format. The exact candidate was
`.tmp/electron/package-290c9f0fc2e54c6c849dec1d727d3f46`;
its `package-summary.json` SHA-256 is
`cb8de1309dbcd2adb803025b188a01c777891d63399c9540762ad91c9e81bc8d`.
The 567-file payload and installer passed inventory verification after
publication to `dist/Phraseback`. The accepted installer SHA-256 is
`7200e9f13cc0331ec3d7759c52945f85c486a6c8c10d602cfcafa97b1f884cf5`.
The signed-off machine-readable gate is `dist/release-acceptance.json`.

## Checks on the exact package

- Rust tests: 69 passed, 2 opt-in tests ignored. Electron tests: 21 passed.
  The one-time Avalonia migration regression passed 101 tests with 2 skipped.
- Native packaged flows passed for Library/open/recovery, Review/curation,
  organization and cancellation with concurrent edits, prompt consistency,
  display/region capture, exclusion, global hotkey, floating Stop, shortcut
  conflicts, export and cancellation, preferences, reconnect, normal/forced
  shutdown, model verification/controls, saved generation, cancellation, and
  cached install/removal. All used isolated roots under `.tmp/`.
- On Windows 11, Intel i7-14650HX, Intel UHD Graphics, and a native
  2560 × 1600 SDR display at 125% scaling, five full-display changing-surface
  runs measured 8.03–8.16 samples/s (target ≥7.6). Stop-to-Review p95 was
  343 ms (target ≤1000). CPU capture pool peaked at 49,152,000 bytes against
  its 128 MiB budget; observed GPU local usage peaked at 34,136,064 bytes.
- Uncached and cached seek p95 were 25.2 and 8.6 ms; renderer dispatch p95
  under seeking load was 1.1 ms. After 800 seeks, post-warmup private-memory
  growth was 6,189,056 bytes. Reports are under `.tmp/electron/capture-Olxl1h/`.
- Review, Library, capture, Settings, prompt, navigation/actions/recording
  menus, keyboard focus, loaded/empty states, and a reconnect error state were
  rendered and visually checked
  at 1280 × 800 and 1024 × 700 without horizontal overflow.
- Installer install, different-build replacement, rollback, forward restore,
  reinstall, and uninstall preserved synthetic recording and model hashes.
  Both compared builds identify as 0.2.0; this is not a cross-version data
  migration test.
- Notice inventory: Electron/Chromium texts, 169 npm entries, 170 Rust
  components, and pinned FFmpeg source kit. Six npm entries without local
  license files are build-only and absent from the packaged runtime bundle.
  The scoped review is `app/packaging/ELECTRON-NOTICE-REVIEW.md`.

One earlier candidate produced a single 5.93 samples/s outlier with Windows
capture callback pauses. Its next four runs and a fresh five-run repeat met the
target. The published candidate then passed its own five-run set without a
missed sampling interval. A fresh multi-gigabyte model download was not run;
the engine's mock-server tests cover download failure and resume behavior.

## Cutover and rollback

`dist/Phraseback` and `dist/Phraseback-Setup-0.2.0-win-x64.exe` now contain the
accepted Electron release. The previous Avalonia portable folder, installer,
and metadata are retained at
`reference-backups/previous-release-58138b27d796432083268287e30ded98/`.
The canonical launcher and `Play Phraseback.cmd` use Electron. Default
`build.ps1` Check, Test, Build, Prototype, and accepted Release paths no longer
invoke C# or NuGet. Avalonia source and its historical scripts remain for
rollback; no user library, recordings, or models were used for validation.

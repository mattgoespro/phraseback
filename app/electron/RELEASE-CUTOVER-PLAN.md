# Electron release cutover

Status: completed 2026-10-04. The exact candidate, exit-gate evidence, and
rollback location are recorded in `RELEASE-ACCEPTANCE.md`. The phases below
are retained as the migration contract.

The Rust engine remains the only authority for version-1 projects, original PNGs,
journal ordering, exclusive data-root locking, and durable writes. All test data
lives under `.tmp/`; no acceptance test opens the user's library. The previous
Avalonia portable folder and installer are retained under `reference-backups/`.

## Phase 1 — Behavior inventory and parity

- Map each Avalonia flow and engine command to an Electron control and observable
  result: Library/recovery, display and region setup, countdown and stopping,
  Review/playback/curation/manual edits, organization, prompt template, model
  install/verify/remove and generation, export, window preferences, and close.
- Exercise those flows on isolated version-1 fixtures. Test interrupted open,
  recording, generation, and export, then reopen and verify original evidence,
  drafts, and completed work survive.
- Review screenshots at 1280×800 and 1024×700 against `DESIGN.md`, including
  menus, settings, empty/error/loading states, keyboard paths, and focus.
- Exit gate: every behavior has a source pointer, Electron pointer, a passing
  check, or a documented user-approved change. No missing release behavior.

## Phase 2 — Engine contract and lifecycle

- Exercise the real Rust engine through the TypeScript transport: split and
  malformed frames, incompatible protocol, notification order, stale revisions,
  failed durable saves, cancellation, reconnect, and shutdown.
- Verify `operation_saved` acknowledgements are never treated as disposable
  progress and that completion cannot overtake persistence. Verify the Electron
  main process bounds pending work and cleans up the child on normal and forced
  close without releasing the data-root lock early.
- Exit gate: contract and lifecycle tests pass on isolated data; no UI path
  silently drops a draft or treats an unsaved result as complete.

## Phase 3 — Native Windows acceptance

- Recheck SDR full-display and region capture, content exclusion, countdown,
  floating controls, real `Ctrl+Shift+F9`, shortcut conflict, and display scaling
  with a native run no longer than 30 seconds per disruptive test.
- Measure the `app/SCOPE.md` targets on the reference display: effective sample
  rate, stop-to-review p95, cached and uncached seek p95, UI dispatch latency,
  capture/decode memory plateau, cancellation, and cleanup. Record hardware,
  display mode, fixture, sample sizes, and raw measurements.
- Exit gate: every in-scope target passes or an explicitly approved scope
  amendment records the changed target. Keep headless and native evidence apart.

## Phase 4 — Candidate packaging and installer

- Pin and verify the Electron, Node, Rust, FFmpeg, and Inno inputs; collect
  Electron/Chromium, JavaScript dependency, Rust, and FFmpeg notices/source.
- Package the latest tested source with the Rust engine and assets, inventory
  every payload file, and smoke-test the portable folder on isolated data.
- Test packaged capture and child cleanup. Install, replace, reinstall, roll
  back, restore forward, and uninstall with the existing Inno identity. Verify
  synthetic recordings and model assets retain identical hashes throughout.
- Exit gate: candidate inventory and installer checks pass on the exact build
  that is proposed for release. No claim of a cross-version migration from
  same-version replacement tests.

## Phase 5 — Release switch

- Run `Check` and `Test` across Rust and the new shell, plus final native and
  packaged acceptance. Publish the verified Electron portable folder and
  installer to `dist/` atomically, retaining the previous Avalonia payload as a
  recoverable rollback snapshot.
- Change `run.ps1`, `build.ps1` defaults and launch shortcuts to Electron only
  after verifying the new canonical payload's hashes and startup.
- Exit gate: normal launch, new installation, upgrade and rollback paths use the
  verified Electron release; the real user library has not been used for tests.

## Phase 6 — Legacy UI deprovision

- Remove Avalonia from active build, test, packaging, launch, and documentation
  paths. Retain source and verified rollback artifacts in a clearly labeled
  archive until release acceptance is recorded; never delete user recordings,
  models, or original PNGs.
- Verify a fresh `Build`, `Check`, `Test`, `Prototype`, and `Release` use Electron
  and no longer require the Avalonia runtime or NuGet restore.
- Exit gate: Electron has full proven feature parity and is the only active UI.
  The Rust engine, data format, installer identity, and rollback material remain.

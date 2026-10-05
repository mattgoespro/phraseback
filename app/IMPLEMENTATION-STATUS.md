# Replacement completion tracker

## October 4 Electron cutover

The active release is Rust + Electron. The accepted package, native and
installer evidence, and Avalonia rollback location are in
`electron/RELEASE-ACCEPTANCE.md`. `build.ps1`, `run.ps1`, and the playground
shortcut now use Electron. Entries below record the earlier Rust/Avalonia
replacement and are historical, not current build instructions.

Updated 2026-09-25. The instruction is to finish the replacement, then remove Python.
Python source and obsolete tooling have been retired into the verified rollback area.
The active implementation now lives under `app/`; original models/recordings are untouched.
Canonical release publication and post-move validation are complete.
This file tracks implementation and proof. The user-approved resolution amendment is
in SCOPE.md: validate the native 2560 × 1600 display and smaller regions.
Current scope takes precedence over historical entries below.

Remaining delivery is sequenced in [DELIVERY-PHASES.md](DELIVERY-PHASES.md). Phase 1
startup/recovery and shared command contracts are implemented. Phase 2 review features,
window preferences and bounded instrumentation are implemented. Metadata persistence fault
tests are expanded. Release builds now support normal data roots after the
compatibility audit. Final release and cutover verification are complete.

## Implemented and exercised

### September 30 Phraseback rebrand

- Renamed the desktop UI, .NET projects/namespaces, engine executable, portable
  package, installer and playground launcher to Phraseback. Current launchers use
  `dist/Phraseback/Phraseback.exe`; the installer retains its application identity
  for existing installations and replaces obsolete Start menu shortcuts.
- New libraries default to LOCALAPPDATA/Phraseback. Existing LOCALAPPDATA/FlowRecorder
  libraries are opened in place. PHRASEBACK_DATA takes precedence over the retained
  FLOW_RECORDER_DATA override; explicit --data-root takes precedence over both.
  Debug builds protect both normal library locations. No recordings or models are moved.
- Check, Build, focused startup/branding tests and the full Test suite passed:
  62 Rust tests and 89 C# tests, with two opt-in tests skipped in each suite.
- Release completed through build.ps1 with payload and installer hash verification.
  A short isolated packaged launch verified the native Phraseback window title,
  executable product/description metadata and graceful shutdown. The new installer
  compiled successfully; it was not installed into the user's Windows environment.
- Older entries below retain original artifact paths and technical identifiers as
  historical evidence. Compatibility paths, development toolchain locations,
  protocol schema IDs and development markers retain their original identifiers.

### September 25 migration completed and canonical build verified

- Active Rust/Avalonia source is now `app/`. Python `src/`, `test/`, helper scripts,
  PyInstaller/configuration files, old environment and obsolete guidance were moved
  out of the active project into `reference-backups/python-reference-20260925-211203-e8b599bf/retired-tree/`.
  The byte-verified ZIP and passing restored-test report remain beside it. No user
  recording, installed model or unrelated folder was modified. Recovery is possible.
- Root `build.ps1` owns Build/Check/Test/Prototype/Release entry points. `run.ps1`
  and `launch_release.ps1` launch the canonical self-contained Rust/Avalonia app.
  Python transition jobs were removed from CI; schema generation and active tests
  remain Rust/C#. PowerShell scripts parse; CI YAML and absence of Python jobs checked.
- Post-move Check passes shared contracts, formatting and Clippy. Test passes 61 Rust
  tests (two opt-in skips) and 85 C# tests (two opt-in skips). C# report:
  `.tmp/rebuild/reports/Matt_MATT-LAPTOP_2026-09-25_21_21_23_net10.0.trx`.
- Release built from moved source: `package-6db430cce3fe458e90ff841a751785e9`.
  Published `dist/FlowRecorder` plus `dist/FlowRecorder-Setup-0.2.0-win-x64.exe`.
  All 622 canonical payload files and installer SHA-256 match provenance. 222 native
  binaries have no external Visual C++ runtime imports; 197-component notices checks pass.
- Canonical native recording/review passes:
  `.tmp/rebuild/native-check-234719a4d529419f975c7b3b9b3061bd/`: 40 samples,
  37 accepted + 3 duplicates, zero missed, 5.011 seconds; 311.60 ms stop-to-review,
  1.28/51.40 ms cached/uncached seek p95. Owned process closes inside 30 seconds.
- Latest installer installation, upgrade, rollback, forward restoration, reinstall,
  uninstall and synthetic-data preservation pass:
  `.tmp/rebuild/installer-check-a26abdb44ea24323b978a6cd72be5c48/`.
- Actual root launcher with isolated FLOW_RECORDER_DATA starts and closes both shell
  and engine successfully: `.tmp/rebuild/launcher-check-258421c66f4b4d019f31d73ec8982305/`.
  Package is unsigned and local-use delivery; no public redistribution approval is claimed.
  This entry supersedes earlier pending-cutover statements below.

### September 25 refreshed rollback snapshot verified

- Latest pre-cutover snapshot: `reference-backups/python-reference-20260925-211203-e8b599bf/`.
  All 222 archived source/configuration files match the fresh extraction byte-for-byte,
  including the C# model-check replacement and optional-portable CI amendment.
- ZIP SHA-256: `2ba76c49695d741dfb19631708857090b9548d1000ee8bc244731283eac10268`.
  Restored Python/compatibility tests: 136 passed, one skip in 38.24 seconds;
  `restored-tests.xml` and `verification.json` are retained beside the ZIP.
  Uses the existing Python environment and compiled compatible Rust engine, not
  a fresh dependency installation. Pytest/conftest select the restored `src` tree.
- No source, launcher, installed model or recording was removed or replaced.
  This supersedes the earlier source snapshot for rollback preparation, not its
  historical evidence. Final cutover still awaits the outstanding acceptance decision.

### September 25 CI retirement audit and optional portable checks

- The authored CI matrix previously made Windows packaging depend on Linux/macOS
  jobs. Normal runs now select Windows only; manual `run_portable` explicitly adds
  the other platforms. YAML parsing and matrix/default/dependency assertions pass
  locally. No remote workflow was run or repository uploaded.
- Python remains only in the explicit local `Reference` task, CI compatibility/
  Qt-lock/reference checks and legacy helper/application sources. The ordinary
  replacement build/test/contract-generation/package path is Python-independent.
  Preserve the tested legacy-reader and Qt interoperability evidence in the final
  source archive before retiring those transition-only CI steps. Shared schemas,
  golden fixtures, Rust tests and C# regression tests remain active after cutover.

### September 25 Python-independent real-model check

- Added `scripts/rebuild_model_check.ps1` and `RealModelTests.cs`, reusing the
  existing C# engine client, embedded studio fixture and pinned model manifest.
  Requires explicit source/engine paths and preset selection. Assets are copied
  into a unique isolated directory after size/SHA-256 validation; source hashes
  are checked again after inference. No downloads or model installation changes.
- The check covers forced single-step generation, healthy warm state, preservation
  of unrelated manual steps, durable manual edit/reopen and explicit release.
  Reports retain the engine hash and mark network isolation as unverified. A
  local inference pass must not be relabeled disconnected-network acceptance.
- Asset-copy regression passes (corruption, traversal and overwrite rejection).
  Real Vulkan run passes against the current packaged engine:
  `.tmp/rebuild/model-check-78598e09268447af849d93f3f896f321/`.
  Real CPU run also passes:
  `.tmp/rebuild/model-check-b20a9ff9d0e640d4ae644aa608330833/`.
  Both preserve source asset hashes and release their model processes. The old
  Python helper can now retire with the reference tooling; production app code is unchanged.
- Full C# suite: 85 passed, two opt-in skips (real inference and review endurance);
  report `.tmp/rebuild/reports/Matt_MATT-LAPTOP_2026-09-25_21_01_54_net10.0.trx`.
  The separately opted-in CPU and Vulkan runs above each passed without skips.

### September 25 current packaged build repeated native performance passes

- Two five-second detailed synthetic full-display captures at 2560 x 1600 passed
  on candidate `package-71bd49cdbc874fab87167d4098a61617`. Machine reports contain
  matching host, shell and engine SHA-256 fingerprints. Both processes exited
  within their 30-second deadlines; no recorder processes remained afterwards.
- Each run sampled 40 opportunities, with zero missed opportunities: 36 saved +
  4 duplicate states, then 37 saved + 3 duplicate states. Saved-frame counts match
  accepted evidence. Effective cadence was 7.91 and 7.96 samples/second.
- Stop-to-review: 317.25 and 299.59 ms. Cached seek p95: 1.27 and 2.39 ms;
  uncached seek p95: 75.28 and 66.78 ms; dispatch p95: 0.674 and 0.377 ms.
  Neither run failed a seek. CPU capture pool: 49,152,000 bytes; sampled local
  GPU peak: 34,136,064 bytes. Preview cache plus thumbnails stayed below 128 MiB.
- Evidence roots under `.tmp/rebuild/`:
  `native-check-8299b278fd554d52a2d38aef8def2ee3` and
  `native-check-ebd803b4209f4c038173ff3376d72e1d`.
  This closes short repeated current-build timing/cadence validation for this
  synthetic workload. It is not long-duration native, hardware-interruption,
  assistive-technology or disconnected-model workflow proof.

### September 25 historical performance evidence audited

- Reviewed six successful native full-display runs at 2560 x 1600. Their recorded
  stop-to-review times are 309–325 ms; available cached seek p95 values are
  1.22–1.68 ms and uncached seek p95 values are 54–63 ms. Raw reports and capture
  metrics are indexed in `.tmp/rebuild/historical-native-performance.json`.
- These runs span different builds and workloads. Older reports lack executable
  fingerprints, so they are historical evidence, not repeated-run acceptance of
  the current candidate. Do not average their percentiles into a release result.
- Future native reports now record SHA-256 fingerprints for the application host,
  shell assembly and engine alongside machine metadata. The script parses and
  all three current-candidate hashes survive a JSON round trip. No capture was
  started for this audit; application behavior and the 30-second deadline are unchanged.

### September 25 headless keyboard review workflow verified

- Added real-control keyboard checks at 1024 x 700 and 1280 x 800, using the
  production MainWindow content/event handlers and an actual isolated Rust engine.
  Tab traverses recording/moment rows, timeline, four description inputs and the
  visible review action buttons without a focus trap in that traversal.
- Text is entered through Avalonia's headless text-input path. The test confirms
  an unsaved draft exists, presses Down in the moment list, and verifies the next
  evidence is selected only after the previous text is persisted as manual and
  the unsaved draft clears. No direct view-model edit substitutes for text entry.
- Focused tests pass at both sizes. Full C# suite: 84 passed, one opt-in endurance
  skip. Report `.tmp/rebuild/reports/Matt_MATT-LAPTOP_2026-09-25_20_47_05_net10.0.trx`.
  This is component keyboard routing/save evidence, not native Narrator, modal
  keyboard-flow or hardware-interruption acceptance. No shipping code changed and
  no new package was necessary; current playground remains the previous candidate.

### September 25 native row accessibility names corrected

- Native packaged inspection at 1024 x 700 found library and moment rows exposing
  record ToString output, including internal IDs/Owner and complete step fields.
  Added names on the existing ListBoxItem containers: recording title, and moment
  number/title/time/status. Lists themselves now announce Recordings/Selected moments.
  Visual layout and selection ownership are unchanged.
- Real automation-peer regression failed against the old names and passes with
  the fix. Native reinspection confirms `A calmer workspace` and
  `1. Open workspace settings. 00:00.000. Reviewed`, rather than internal objects.
  Minimum-size native render was inspected. The broader keyboard/Narrator audit
  is not closed: input targeting returned `coordinate input geometry is unavailable`
  and, after refreshed geometry, `foreground window did not report a process id`.
  Both isolated UI checks retained 30-second process deadlines; no recorder
  processes remained. No desktop recording or user data was used.
- Checks pass: 61 Rust (two opt-in skips), 82 C# (one opt-in skip), 137 reference/
  compatibility tests, contracts, formatting and Clippy. Python reference visual
  script exits successfully. C# report:
  `.tmp/rebuild/reports/Matt_MATT-LAPTOP_2026-09-25_20_41_30_net10.0.trx`.
- Portable/installer candidate `package-71bd49cdbc874fab87167d4098a61617` under
  `.tmp/rebuild/`; 622 payload hashes and installer hash verified. Playground-selected.
  Synthetic UI root: `.tmp/rebuild/keyboard-check-d3fc826dbc0e4b888cafa21b204db09b/`.
  Python and canonical output remain intact.

### September 25 source rollback archive verified

- Created non-destructive source snapshot under
  `reference-backups/python-reference-20260925-203423-f534bb37/`, outside disposable
  `.tmp` storage. Includes 219 source/configuration/test files, Python dependency
  pins and contemporary rebuild integration context. Excludes generated builds,
  caches, interpreter/toolchains, user recordings and installed model weights.
- ZIP SHA-256: `4ce640c4cc740a699cc2a296ee3aee8ae0008ff5729d66d8b955e2f6caa58d2e`.
  Extracted to a new `.tmp/rebuild/reference-backup-.../restored` folder; every
  archived source file matched its original hash. Manifests and restore instructions
  are retained beside the archive. No working source was removed or overwritten.
- First restored pytest run found the excluded empty `.tmp` parent missing.
  Added that restore setup step, without altering Python application code. Rerun
  from the extracted source: 136 passed, one live-cursor-dependent skip, zero failures.
  Uses the existing Python environment and compatible built Rust engine; does not
  claim fresh dependency installation. `restored-tests.xml` and verification.json
  retain the result. Reconcile later source changes against the snapshot before
  any final deletion; this is not automatic cutover authorization.

### September 25 scoped native-notice review completed

- Recorded `packaging/licenses/NATIVE-REVIEW.md`: 18 native artifacts mapped to
  four vendor packages; native vendor notice inventories; pinned ANGLE Windows
  D3D11 build/source graph and supplement/header coverage; Rust nested notices;
  separately verified FFmpeg source materials. No missing notice family was found
  within that scope. This is engineering coverage, not a vendor-wide source audit
  or legal certification; overall release acceptance remains separate.
- The collector now ships the review with its scope and no longer reports the
  completed native coverage review as pending. Existing version/commit/hash checks
  remain. Dependency/backend changes require revisiting the documented review.
- Notice tests pass for 197 components: deterministic collection, file hashes,
  native-binary ownership and corruption rejection, pinned font/parser/header
  notices, overwrite refusal and retention of explicit overall release status.
  Evidence: `.tmp/rebuild/notices-test-7dc09ac33fc845cd8defa64e1f5b3363/`.
- Updated portable/installer candidate:
  `.tmp/rebuild/package-84276fc505db4cffaf8da28735c48888/`; 622 payload files and
  installer hashes verified. Application host/shell, engine and FFmpeg bytes match
  the previously tested candidate; this is a notice-only payload change.
  Playground selects it. Canonical output and Python remain unchanged.

### September 25 local installed-build upgrade and rollback

- Extended the existing installer check with an optional previous candidate and
  explicit no-capture mode. Uses the real embedded synthetic project (metadata,
  frame journal and PNGs) plus synthetic model bytes, not an empty preservation
  marker. Checks all original data hashes after every installer operation.
- Passed earlier candidate `package-3aa2876a9ad049039b18b4b26b6d5d91` to corrected
  candidate `package-8b8fffa6e8aa49c1a3fc580dd5107fd6`, rollback to the earlier
  candidate, forward restoration, reinstall and uninstall. Every installed payload
  file matched its candidate provenance. Registration pointed only to the disposable
  workspace and was removed by uninstall, along with the installed application.
- Both actual candidates have product version 0.2.0; this is different-build
  replacement and rollback evidence, not cross-version application-schema migration.
  No native capture was run; no existing installation, user recordings or models
  were touched. Synthetic data remains available for inspection.
- Evidence: `.tmp/rebuild/installer-check-38925175ac974500b7e5833a568b3d87/`
  contains operation logs and `installer-check.json` with preserved-file hashes.

### September 25 user acceptance and scope amendment

- User confirms the floating Stop button and countdown cancellation function properly.
  Both interaction checks are closed by user verification, not by a new automated run.
- Clean-Windows and Linux/macOS acceptance checks are no longer completion or
  Python-retirement prerequisites, by explicit user instruction. Do not reintroduce
  their unavailability as a blocker. Historical reports are retained unchanged and
  do not override this amendment; optional CI is not required for cutover.
- Remaining local checks, dependency review, recoverable reference backup and
  canonical cutover still apply. This amendment alone does not delete Python or
  assert that every remaining requirement has passed.

### September 23 floating-control retry: still unverified

- The native Computer Use tool became callable again. Retried the packaged
  production Stop-button path using the detailed synthetic scene and the existing
  30-second process deadline, against candidate `package-8b8fffa6e8aa49c1a3fc580dd5107fd6`.
- The tool returned the main Phraseback window and synthetic scene, but not
  the floating recording-controls window. Main-window accessibility did not expose
  its Stop button either. No input was sent and no handle was reconstructed.
- Report: `.tmp/rebuild/native-check-ed7b64773bc94334b42bee0e69d5bf9d/native-check.json`;
  `passed: false`, expected stop `button`, observed stop null, operation timed out.
  The outer safety deadline terminated the owned test process tree. A subsequent
  process check found no FlowRecorder or FlowRecorder.Engine process.
- This is an automation-targeting blocker, not evidence that the Stop handler
  passed or failed. Do not repeat this same retry without a changed capability or
  a user-operated check. Production countdown cancellation remains unverified too.

### September 23 portable engine runtime dependency fixed

- Native import audit found `VCRUNTIME140.dll` in the previously packaged Rust
  engine, without that DLL in the portable payload. This was a clean-machine
  startup risk hidden by this development machine's installed runtime.
- Target-specific Cargo configuration now statically links the MSVC C runtime.
  Linux/macOS and the GNU development target are unchanged. Packaging scans every
  EXE/DLL's normal and delay-loaded imports, rejects external Visual C++ runtime
  dependencies, and records hashes/imports in `runtime-imports.json`; CI retains
  the report. The old candidate fails this check; the new candidate passes for
  all 222 executable files. Dynamic LoadLibrary dependencies and actual clean
  Windows acceptance are not proven by this inspection.
- Full verification passes: 61 Rust (two opt-in skips), 81 C# (one opt-in skip),
  137 reference/compatibility tests, contracts, formatting and Clippy; all four
  release startup tests also pass. Notice provenance/tamper/determinism checks
  pass. No new desktop capture or real user-data access was needed.
- Portable/installer candidate:
  `.tmp/rebuild/package-8b8fffa6e8aa49c1a3fc580dd5107fd6/`.
  All 621 payload hashes and installer hash verified; playground-selected.
  Native interaction, clean-machine/cross-platform acceptance and final notice
  review remain open. Canonical output and Python remain unchanged.

### September 23 structured native capture workload

- Added opt-in `-Workload detailed` to the native check. The application-owned
  opaque fixture updates scrolling-style rows, selection, bars, progress text and
  a brief error status on a steady dark background. It is not an interactive
  product screen or a trace of another application. Default color-fixture behavior
  is unchanged, and reports identify the workload. The 30-second process cap stays.
- Deterministic scene-state regression passes. Full checks: 61 Rust (two opt-in
  skips), 81 C# (one opt-in skip), 137 reference/compatibility tests, contracts,
  formatting and Clippy. Portable/installer candidate:
  `.tmp/rebuild/package-3aa2876a9ad049039b18b4b26b6d5d91/`.
  All 621 payload hashes and installer hash verified; playground-selected.
- Packaged detailed native run passed at 2560 x 1600: 5.045 s, 40 samples, 36 unique
  frames, four duplicates, zero missed opportunities. Stop-to-review 319.8756 ms,
  cached seek p95 1.2703 ms, uncached 55.7591 ms, dispatch p95 0.2596 ms. First/last
  original PNGs were visually inspected, including the error-to-saved status change.
  No recorder processes remained. Evidence:
  `.tmp/rebuild/native-check-28f277654869427a866be6657b5554c1/`.
- Native desktop-control tooling is not callable in this session; the supplied
  computer-use skill requires that tool, not a custom input-injection workaround.
  Therefore production Stop-button/countdown interaction and accessibility were
  not exercised. Structured synthetic evidence does not close those gates or
  clean-machine/cross-platform release acceptance. Python remains intact.

### September 23 measured capture-adapter GPU usage

- Capture obtains IDXGIAdapter3 from its actual D3D11 device, not by guessing which
  display adapter Windows selected. While sampled staging is mapped it queries
  node-0 local/nonlocal process usage and budgets. Retains 256 recent timed samples,
  all-time observed peaks and failed-query counts. Unavailable counters remain null
  and never cause capture failure. This is engine-process adapter usage, not system
  GPU totals, per-texture residency or unsampled peaks; see CAPTURE-MEMORY.md.
- The bounded-history/null-failure regression passes. Full verification: 61 Rust
  tests (two opt-in skips), 80 C# (one opt-in skip), 137 reference/compatibility tests,
  contracts, formatting and Clippy. Portable/installer candidate:
  `.tmp/rebuild/package-f797ec128aa542708907643638ba0a53/`.
  All 621 payload hashes and installer hash verified; playground-selected.
- Native 2560 x 1600 check passed over 5.018 seconds: 40 samples, 38 unique frames,
  two duplicates, zero missed opportunities, 319.0108 ms stop-to-review. Intel UHD
  node 0 supplied 40 GPU samples with zero failed queries; local peak 34,136,064
  bytes, nonlocal peak 0. CPU pool, mapped staging and logical WGC payload remain
  separate. Evidence: `.tmp/rebuild/native-check-ef10981e68304e21817a33a59bfbec63/`.
  No recorder processes remained. This closes separate memory-accounting
  implementation/short-run verification, not long-duration native stability,
  interactive Stop/countdown, accessibility or clean-machine release acceptance.
  Python remains intact.

### September 23 encoder heap, staging counters and backup contention

- Added test-only thread-local Rust allocation accounting around the actual PNG
  encoder. Eighteen release cases cover flat/noisy input at 640 x 480, 2560 x 100
  and 2560 x 1600, three repetitions each. Peak encoder heap is 96,256 bytes per
  worker at width 2560, with zero live bytes at scope end. Input pixels, output
  storage, allocator overhead, stack and foreign/GPU allocations are excluded.
  The probe is absent from shipping builds. Evidence:
  `.tmp/rebuild/encoder-memory-ca0ebec55c544ec39cc831237157f151/`.
- Capture metrics now separate observed mapped staging pitch/size from nominal
  single-WGC-surface RGBA payload and the existing CPU pool reservation. Unknown
  native measurements are null for fake capture. Mapped staging is released as
  soon as pixels have been copied. These numbers do not measure GPU residency.
- The first compatibility run failed an edit with generic Windows Access denied
  (136 passed, one failed); no completed legacy backup existed in that fixture.
  Twenty isolated reruns passed, so they did not identify the original external
  lock holder. A deterministic staged-backup reader lock then reproduced code 5
  at directory publication. Added the same bounded 150 ms Windows retry there,
  with temporary/permanent-lock regressions and content-free backup-stage errors.
  Original metadata is unchanged on failure; a permanently locked temporary
  backup may remain until external handles close. No live owner's data lock is
  bypassed and non-Windows publication remains unchanged.
- Final checks pass: 60 Rust tests (two separate opt-in skips), 80 C# (one opt-in
  skip), 137 reference/compatibility tests, contracts, formatting and Clippy.
  Optional CI encoder accounting retains JSON results; workflow syntax was parsed
  locally, not run hosted.
- Portable/installer candidate `.tmp/rebuild/package-7534af95bcec435697a725604882afc3/`
  is playground-selected; 621 payload hashes and installer hash verified. Native
  five-second 2560 x 1600 check passed: 41 samples, 34 unique frames, seven duplicates,
  zero missed opportunities, 312.0573 ms stop-to-review. Counters separately show
  49,152,000-byte CPU pool reservation, 16,384,000-byte mapped staging peak (10,240-byte
  row pitch), and 16,384,000-byte nominal WGC surface payload. No recorder processes
  remained. Evidence: `.tmp/rebuild/native-check-eadfc4bb9fc64d1e9b5a6b1cd8f65a78/`.
  This is not a GPU-residency measurement or production Stop-button acceptance.
  Python remains intact.

### September 23 Khronos header notices

- Inspected ANGLE's exact source tree and enabled-backend settings. Found separate
  Khronos copyright/license comments in fifteen public EGL/GLES/KHR headers that
  were not covered by the collected ANGLE attribution text alone.
- Retained all fifteen original comment blocks with pinned-revision source URLs,
  including their MIT-style permissions and Apache-2.0 identifiers/attributions.
  Added the complete Apache-2.0 terms alongside them. Both supplement files are
  hash-pinned and tied to the existing NuGet source commit.
- All fifteen blocks were compared with the downloaded original headers. Notice
  collection passed deterministic output, dependency/hash checks, native tamper
  rejection and new required-header/terms assertions. Evidence:
  `.tmp/rebuild/notices-test-e9eb221bef6b474ba511aafe58e9b1e9/`.
- Candidate `.tmp/rebuild/package-07c72a4a2a4d46059bf7403dd343d977/` includes the
  supplements. All runtime executables/libraries remain byte-identical to the prior
  candidate. Portable and installer builds completed; all 621 payload hashes and
  installer hash verified. The playground selects this candidate. No new runtime
  tests are claimed for this notice-only slice. Full resolved-dependency review and release acceptance
  remain open; Python and canonical release remain unchanged.

### September 23 native dependency provenance and notice coverage

- Packaging now verifies all 18 Windows native dependencies declared by the
  published `.deps.json` against the exact restored NuGet/runtime-pack bytes.
  Missing or substituted native files fail before notices are published. The
  new `licenses/native-artifacts.json` maps each binary to its package/version,
  source-relative path and hash; component notices remain separately hashed.
- Confirmed the restored SkiaSharp 3.119.4 and HarfBuzzSharp 8.3.1.3 Windows
  packages supply both their license and vendor third-party notice bundles.
  Regression checks require their principal dependency sections and a unique
  notice owner for each declared native artifact. This is vendor notice/provenance
  verification, not an independent review of every statically linked source file.
- Deterministic collection, coverage, checksums, refusal to overwrite, and deliberate
  native-byte tampering tests passed twice. Latest evidence:
  `.tmp/rebuild/notices-test-0ff3d31d00bd49458288c76b4069db4f/`.
- Refreshed payload is `.tmp/rebuild/package-63905e5d94654817a0a1bc6bcaa75ef7/`.
  Its existing 618 files are byte-identical to the prior native-tested candidate;
  the 619th file is the provenance manifest. No runtime code changed in this slice.
  Portable and installer builds completed; all 619 payload hashes and installer
  hash verified, and the playground points to this candidate. ANGLE source/header coverage and final
  release acceptance remain open; no Python deletion or canonical promotion.

### September 23 Windows atomic-save contention hardening

- Reproduced PersistError Windows code 5 deterministically by holding the destination
  open without FILE_SHARE_DELETE. The short-lock test fails with the prior one-shot
  publication and passes after bounded retry. This reproduces the failure mechanism;
  it does not identify the external process responsible for earlier intermittent runs.
- Windows metadata publication now reuses the already-flushed staging file and retries
  access/sharing/lock violations with 10/20/40/80 ms delays (150 ms total sleep).
  It never deletes the original or changes destination permissions. Other errors
  return immediately; persistent contention still returns a visible save failure.
  Non-Windows publication is unchanged.
- A lasting native reader lock test verifies bounded failure, unchanged original bytes,
  staging cleanup, and successful retry after release. Twenty repetitions of all eleven
  project tests passed. Full checks passed: 56 Rust, 80 C# (one opt-in skip), 137
  reference/compatibility tests, shared contracts, formatting and Clippy.
- Refreshed portable/installer candidate and selected playground:
  `.tmp/rebuild/package-77b236da4bbc42749aff54b64d83300b/`.
  All 618 payload hashes plus installer hash verified. Native unmarked-isolated-root
  full-display check passed: 2560 x 1600, 5.041 s, 40 samples, 34 accepted unique
  frames, six duplicates, zero missed opportunities; stop-to-review 324.4235 ms,
  cached seek p95 1.6731 ms, uncached 60.7577 ms. Review render inspected and no
  recorder processes remained. Evidence:
  `.tmp/rebuild/native-check-f76fe160d220445db5d27e9b1d82a969/`.
  This validates the updated streaming PNG path on a short uniform-color native
  workload, not high-entropy allocation totals, production Stop-button interaction,
  repeated performance percentiles or clean-machine acceptance. Python stays intact.

### September 23 bounded PNG output and recurring save failure

- Audited the pinned capture/PNG allocation owners in CAPTURE-MEMORY.md. The
  previous fast PNG path retained complete compressed images outside pool_bytes.
  Recording now streams 64 KiB output chunks with the same fast compressor,
  explicitly finishing before publication. Existing source images are untouched.
- A noisy-image regression failed on the old implementation and passes on streaming,
  with exact RGBA round-trip and bounded writes. Short/excess input and signature/
  image-payload write errors are rejected. All four focused recording tests pass.
- Release synthetic endurance passed 4,800 frames: 153 memory samples, 36,036,608-byte
  peak private memory, middle-to-final p95 growth 110,592 bytes. Evidence:
  `.tmp/rebuild/endurance-8c046b9dfa274f0492897b570b871a4e/`.
  Uniform-color CPU evidence does not prove noisy-frame allocation or native cadence.
- First full run passed 53 Rust, 80 C# (one opt-in skip), 137 reference tests and
  contract/format/Clippy checks. After adding the input/write-failure test, the final
  full run failed in the existing metadata fault test at project.rs:532: the normal
  retry save returned PersistError Windows code 5 (Access denied). This recurs from
  a prior run and is NOT resolved by the encoder work. Focused recording tests and
  code checks pass, but the final full suite is not green.
- Next priority: diagnose the recurring atomic metadata replacement failure. Then
  complete encoder allocation/native performance checks before refreshing the
  packaged playground. Current selected package and Python remain unchanged.

### September 23 workspace copy and refreshed candidate

- Removed stale prototype wording from the production window title, studio header
  and recording library. The privacy note now accurately says capture and analysis
  run locally. The separate interaction preview explicitly says it does not change
  recordings or settings, without claiming an isolated library. Layout is unchanged.
- Two headless logical-tree regression tests cover the production and preview copy.
  Verification passed: 52 Rust and 80 C# tests (one separate opt-in endurance skip),
  plus contract, formatting and Clippy checks. No rendered interaction was tested
  in this copy-only slice; reference tests were not rerun.
- Portable/installer candidate:
  `.tmp/rebuild/package-d5c0ea545082487b8d181b40bc0fbd76/`.
  Playground launcher selects this payload. All 618 payload file hashes and the
  installer hash were verified against the generated inventories.
- User confirms no other test environments or CI repository are available.
  Clean-Windows and Linux/macOS execution remain unverified, not waived; no remote
  hosting or upload is authorized. Python and canonical release remain unchanged.

### September 23 recording-start failure cleanup

- Added a regression through MainWindow.RunRecordingAsync using the real engine
  and a deliberately occupied Win32 stop shortcut. It failed before the change:
  unconditional capture-visibility restoration masked the shortcut-conflict error
  on the headless window handle, although exclusion had never been established.
- Recording cleanup now restores studio visibility only after successful exclusion.
  The regression passes twice through the failure path: original conflict message
  preserved, workspace enabled/connected/not busy, source PNG and project bytes
  unchanged, no extra recording folder, shortcut reacquirable, and preparation
  usable again. The window is never shown and capture never starts.
- This exercises the production early-failure path, not a rendered countdown or
  button press. Those interactive acceptance checks remain open.
- Verification passed: 52 Rust, 78 C# (one separate opt-in endurance skip), 137
  reference/compatibility tests, contract/format/Clippy checks. Portable/installer:
  `.tmp/rebuild/package-e236b6d7d5b94a1f86494763546c8141/` (playground selected).
- Packaged direct-workspace native smoke check passed with 40 frames over 5.021 s,
  289.8409 ms stop-to-review and no recorder processes remaining. Render inspected;
  evidence `.tmp/rebuild/native-check-6f5807c5df4749e3bca990f6443cd307/`.
  This harness does not press the production Stop button or cancel its countdown.
- Render inspection found stale prototype/library-isolation copy in the shared
  workspace despite release startup supporting normal data. Update that wording
  before cutover; do not imply real user data is inaccessible in a release build.

### September 23 real CPU preview-decode endurance

- Added opt-in `scripts/rebuild_review_endurance.ps1`, using the actual Skia drawing
  backend with a headless window platform, the real engine and Workspace seek path.
  Ordinary tests keep their lightweight renderer; this test skips unless explicitly
  enabled. No visible window, screen capture, keyboard input or user data is involved.
- Each run creates 64 synthetic 1440 x 900 PNGs and performs 1,200 seeks with a
  coprime stride that exceeds cache capacity. Both runs passed with 1,175 evictions,
  zero failed seeks, a 129,600,000-byte cache peak and zero cache bytes after close.
  The cache peak stays below its 125 MiB allocation, leaving the separate thumbnail
  reservation within the combined 128 MiB decoded-image budget.
- Sixty process-private/working-set samples were retained per run, without forced
  garbage collection. Middle-to-final-third private-memory p95 growth was
  -3,932,160 bytes and +2,174,976 bytes, within the 32 MiB regression threshold.
  Runs took approximately 10 and 9 seconds. Evidence:
  `.tmp/rebuild/review-endurance-e8a3c9b4a8684c32a6f05567f4f60a39/` and
  `.tmp/rebuild/review-endurance-9578ba633d2c4df9a4b2f273f99f3386/`.
  The second also records machine/configuration metadata.
- This is Debug-build synthetic CPU decode/cache evidence, not rendered-window,
  GPU memory, production seek-latency or long-duration native acceptance. Runtime
  implementation and the current portable build were not changed by this slice.
- Ordinary verification passed 52 Rust and 77 C# tests, with the new opt-in test
  explicitly skipped in the ordinary run and passed separately twice. Contract,
  formatting, Clippy and workflow YAML checks passed. The manual CI endurance input
  now includes this check and retains JSON/TRX evidence; hosted execution is unverified.

### September 23 stop-shortcut startup and cleanup

- Added real Win32 registration tests: competing registration reports the intended
  Ctrl+Shift+F9 conflict, disposing a failed contender does not steal the owner,
  and the shortcut can be acquired after its owner is disposed. No keyboard input
  or desktop capture is involved. Tests are serialized against other collections.
- Hardened RecordingProtection's startup/disposal race. A durable disposal flag
  handles closing before the worker message queue exists; late workers exit, and
  repeated Dispose calls cannot post to a stale/reused thread ID. Queued stop
  callbacks also check disposal before invoking the recording action.
- Forty immediate-disposal/reacquisition cycles now observe each startup result,
  allowing intentional cancellation but not a hidden registration error. Focused
  tests passed in 76 ms, versus about 20 seconds for the initial weaker tests on
  the original implementation. This is lifecycle evidence, not a UI-stop proof.
- Sequential verification passed: 52 Rust, 77 C#, 137 reference/compatibility tests,
  contract checks, formatting and Clippy. First full attempt hit Access denied in
  the existing metadata-save test; a retry collided with the concurrently running
  reference engine. Both are recorded rather than hidden; the later sequential
  run passed. No metadata-persistence code was changed to suppress the first error.
- Floating-button, countdown UI and assistive-technology checks remain open.
- Twenty isolated reruns of the metadata-save test passed; retained logs are at
  `.tmp/rebuild/save-recheck-170fc06e5fd642bb9e48c057243543fb/`. This does not explain
  or claim to fix the earlier intermittent Windows denial.
- Portable/installer build passed at
  `.tmp/rebuild/package-62df4c08004b48348797cbf0f72b3c96/`; all 618 payload hashes
  verified. The playground launcher selects this candidate with the cleanup fix.

### September 23 parser notices and bounded control-test retry

- Added ANGLE's generated Bison 3.8.2 parser copyright/terms/exception/attribution
  comments plus the GPLv3 text from the same pinned repository revision. Both are
  hashed supplements attached only to the matching ANGLE package commit. Notice
  tests require the exception alongside the license; deterministic collection passes.
  Portable/installer build passed at
  `.tmp/rebuild/package-ecd9ef2344b1424bbcd8611f3bba30b4/`; all 618 payload hashes
  verified and both parser files are present in the packaged ANGLE inventory.
  The playground launcher selects this candidate.
- Retried the real floating Stop button with the packaged production recording
  flow and isolated synthetic evidence. Computer Use returned the synthetic and
  main windows but no floating controls; main-window inspection returned
  `no screenshot targets found`. No button input was sent or observed.
- The report correctly records failure, not a pass. The 30-second launcher deadline
  stopped the owned test tree; no recorder/engine processes remained afterward.
  Evidence: `.tmp/rebuild/native-check-82c09f40004142b7a99d7bce7d462b4a/`.
  This is a test-access limitation, not proof of a broken or working Stop button.
  Requested a short manual check from the user; the native acceptance item stays open.

### September 23 ANGLE supplemental notice coverage

- Inspected the exact ANGLE NuGet repository commit, Windows build workflow,
  build graph/defaults and dependency revision manifest. Added nine missing-text
  supplements: xxHash, ceval, Abseil, ASTC encoder, RapidJSON, Chromium zlib,
  Chromium utility code, libc++ and libc++abi. All source URLs are revision-pinned.
- The existing notice collector now requires the matching ANGLE package commit
  and verifies retained text hashes before adding these materials. Tests require
  all nine supplements; explicit LF attributes preserve hashes on Windows checkout.
- Scope and remaining review are in `packaging/licenses/ANGLE-SOURCES.md`. This is
  concrete collection progress, not a claim of complete native transitive coverage
  or legal approval. Rendering behavior and native binaries are unchanged.
- Deterministic notice collection tests passed twice with the nine supplements
  required; the ANGLE inventory now contains ten files including its main license.
  Verified all 616 portable payload hashes at
  `.tmp/rebuild/package-6bbc4eeb3f1b4a00bd65473e8afc17d0/`.
- Follow-up evidence: `src/compiler/translator/glslang_tab_autogen.cpp` at the
  pinned ANGLE revision embeds a Bison 3.8.2 parser notice and special exception.
  This was inspected but not yet added to the supplement bundle; the review stays open.

### September 23 minimal encoder integrated into packaging

- Replaced the rebuild's Gyan/imageio encoder acquisition with the independently
  repeatable FFmpeg 7.1 PNG/GIF build. No Python executes during acquisition.
  The binary plus 13 source/recipe/license files are individually hash-pinned;
  cached use requires all of them to match. A cold online build downloads verified
  sources/compiler and builds; offline use fails closed on incomplete material.
- Portable/installer payloads now include `ffmpeg-source/` with unmodified upstream
  FFmpeg/zlib archives, exact recipe/compiler pins, and FFmpeg/zlib/LLVM/MinGW notices.
  Notice collection validates these against the pinned binary before producing its
  inventory. The FFmpeg source-material collection gate is closed; native vendor
  transitive-notice review and final redistribution acceptance remain open.
- Added bootstrap tests for verified import, offline reuse, binary repair, corrupt
  binary/source/notice rejection and rejection of an unverified repair source.
  The Windows CI definition caches only the verified encoder/source kit under
  manifest/recipe keys. Hash-pinned repository inputs have explicit LF attributes.
  Workflow syntax was checked locally; hosted CI has not been run.
- New portable/installer candidate:
  `.tmp/rebuild/package-2614380235964f0697fb3874633c1082/`.
  `Play Phraseback.cmd` selects it. Its exact packaged engine/encoder passed all
  13 operation tests, including GIF pixel/timing/long-hold/Unicode-path checks.
- Validation: 137 reference/compatibility tests, 52 Rust tests (one opt-in endurance
  check ignored), 75 C# tests; contract/format/Clippy checks and deterministic notice
  tests passed. Canonical dist, Python and user data remain unchanged.

### September 23 verified offline source builds

- Added `scripts/rebuild_ffmpeg_source.ps1`: verifies source and compiler archives,
  extracts fresh trees in .tmp, builds without Python, and retains sources, recipe,
  build logs and hash provenance. It does not replace the shipping encoder.
- Pinned LLVM-MinGW's archive SHA-256 against the upstream GitHub release asset
  digest. Offline rejection checks pass for corrupt compiler, missing sources and
  corrupt sources. Missing/unverified inputs never reach extraction or compilation.
- A fresh extracted compiler/source build produced exactly the same executable
  SHA-256 as the previous candidate (`dab18c1a...9b796f6f`). All 13 operation tests
  passed with this independently built binary and the fixed engine. Evidence:
  `.tmp/rebuild/ffmpeg-source-71b0bd924e244942b980b0549e4c69d7/`.
- The complete source-kit collector adds FFmpeg/zlib license texts and LLVM/MinGW
  runtime notices. Its validator detects the missing notices in the earlier kit.
  A second fresh offline wrapper build passed the complete kit validator and again
  produced the identical encoder hash. Complete-kit evidence:
  `.tmp/rebuild/ffmpeg-source-ca97ae4e4ce04bd78c18f9da88d845b2/`.
  Integration into shipping acquisition/notices and remaining distribution review
  are still open; the current app uses the unchanged pinned Gyan encoder.

### September 23 GIF colour regression and source-built encoder candidate

- Strengthened real-engine export tests to inspect decoded GIF colours/dimensions
  as well as timing, original PNG copies, Unicode/apostrophe paths and preservation
  of existing destinations. Both 1.5-second and 125.125-second fixtures failed with
  the original encoder: colours introduced by the last screenshot were missing.
- Confirmed the cause in FFmpeg 7.1's palettegen implementation: diff statistics
  count the previous frame's changed pixels. Export now uses full-frame palette
  statistics, including the final evidence frame. Both fixtures pass with the
  existing pinned encoder and the independently compiled candidate.
- Added a candidate-only minimal FFmpeg build recipe and source-input manifest.
  FFmpeg 7.1 plus zlib 1.3.2 build with LLVM-MinGW, PNG/GIF and file input only;
  the completed binary is 2,296,832 bytes. Source SHA-256 identities are pinned.
  Build log: `.tmp/rebuild/ffmpeg-minimal/candidate-build.log`; candidate SHA-256:
  `dab18c1afc480374a252e67a0529d098e9b6af61bbf14f5d393f8b019b796f6f`.
- The candidate passed all 13 operation tests in a separate payload without adding
  compiler directories to PATH. It is not yet selected by packaging. Compiler
  archive pinning, complete source/license delivery, independent reproducibility
  and clean-machine checks remain open; redistribution remains unapproved.
- Full reference/compatibility suite: 137 passed. Rust: 52 passed, one opt-in
  endurance check ignored. C#: 75 passed. Contract checks, formatting and Clippy
  passed. No user recordings/models, Python implementation or canonical release
  were changed.
- Portable and installer rebuilt at
  `.tmp/rebuild/package-19d28a3f84764da79eee6de575aa9b66/`; the playground launcher
  now selects this payload. Both new GIF regression cases pass against its exact
  packaged engine with the unchanged shipping FFmpeg binary.

### September 23 normal release startup enabled

- Lifted the release-only development-folder restriction after the passing legacy
  locking, journal interruption, editing round-trip and fault/lifecycle checks.
  The shell resolves LOCALAPPDATA/FlowRecorder or FLOW_RECORDER_DATA as before.
  Release engine startup creates a fresh root only after a valid compatible hello,
  acquires exclusive ownership, then initializes its sessions directory. Protocol
  mismatch/malformed hello creates no root. Original evidence/backup rules are unchanged.
- Debug shell/engine still require marked development roots, and the debug engine
  refuses the normal LOCALAPPDATA location even if a marker was mistakenly added.
  All validation used synthetic .tmp roots, including a simulated LOCALAPPDATA;
  the user's actual library and installed models were not opened or changed.
- Four real-process startup tests pass in both Debug and Release. They cover fresh
  unmarked initialization, empty-library listing, invalid handshakes, normal-folder
  debug refusal, retained model sentinel bytes, and pipe-disconnect lock cleanup.
  Release-mode startup tests were added to the Windows packaging CI job.
- New portable/installer candidate:
  `.tmp/rebuild/package-243b64ca10d14830997df705f2211807/`.
  Its native unmarked-root check passed (36 unique frames, 5.037 s recording,
  306.0667 ms stop-to-review). Evidence:
  `.tmp/rebuild/native-check-19154d8f7f5241239c74151db11ab366/`.
  The actual floating-button/hardware/accessibility gates remain separate.
- All 64 compatibility/Qt-lock/operation tests passed against this packaged engine,
  including real FFmpeg export. Normal checks passed 52 Rust plus 75 C# tests and
  contract/fmt/Clippy. Full reference: 135 passed, one previously observed Python
  thumbnail callback failure on a deleted QListWidget; not reported as fully green.
- Playground launches still explicitly select `.tmp/rebuild/playground-data`.
  Direct release executable launches can now open normal data, so use the playground
  launcher for candidate testing. Canonical output/launcher and Python remain intact.

### September 23 synthetic CPU recording endurance

- Added an opt-in release-mode test and `scripts/rebuild_endurance_check.ps1`.
  Each run feeds 4,800 uniform-color 2560 x 1600 frames through the real encoder,
  ordered PNG publication, flushed journal, finalization and reopen path. It
  verifies all source paths, timestamps and accepted/journal/project frame counts.
  No desktop capture, window or user data is involved; the fake producer is unpaced.
- Two runs passed, each preserving all 4,800 frames. Capture-pool reservation was
  49,152,000 bytes. Process-private memory peaks were 36,593,664 / 36,659,200 bytes;
  middle-to-final-third p95 growth was 110,592 / 405,504 bytes, below the explicit
  32 MiB post-warmup regression threshold. Raw private/working-set samples and
  machine metadata are retained (134 / 119 samples at approximately 250 ms).
- Evidence: `.tmp/rebuild/endurance-a1523e278ad94869a4c56fb860802412/` and
  `.tmp/rebuild/endurance-12c4df7e6e2c46e5b124c3c05aaef7d2/`.
  First run completed in 35.567 s; these are accelerated frame-count tests, not
  ten-minute wall-clock/native recordings. They do not prove high-entropy cadence,
  WGC/GPU/cursor behavior, separate encoder allocations or review-cache stability.
- Normal suite remains 48 Rust tests passed, with this one endurance test explicitly
  ignored in ordinary runs and exercised separately twice; all 75 C# tests passed.
  Contract/fmt/Clippy and workflow YAML checks passed. CI exposes the non-interactive
  endurance check as an optional manual input, retaining JSON/logs only.
- Full Python/reference regression also passed: 136 tests in 39.94 s.

### September 23 packaging CI preparation

- Added a Windows packaging CI job dependent on the three-platform test matrix
  and reference job. It builds the optimized self-contained folder and installer,
  checks encoder acquisition and notices, and retains provenance reports only.
  Binary uploads remain disabled while redistribution/source gates are open.
- Pinned the existing Inno 6.7.3 installer and compiler SHA-256 identities. The
  installer was verified locally with a valid Pyrsys B.V. signature, matching the
  publisher listed on the official Inno download page. CI verifies the download
  hash/signature; local packaging now refuses a mismatched compiler executable.
- Added installer/compiler hashes and optional CI source revision to
  `package-summary.json`, alongside the existing payload inventory.
- Local packaging passed with the compiler hash check. Current candidate:
  `.tmp/rebuild/package-9ab416577b9e401b9c55d9c76ad5fe4d/`; the playground pointer
  selects it, while the canonical launcher/output remain unchanged. Verified the
  installer hash and all 593 payload inventory hashes against the built files.
- All 75 C# client/headless tests passed against the optimized packaged engine
  from the preceding equivalent payload (`package-65868e21bc06463c88f3aa19b3eb755d`).
  License collector tests passed component/hash coverage, deterministic output,
  pinned font attribution and overwrite refusal. This is not installed-app UI QA.
- Workflow YAML parsed successfully with duplicate-key checking; triggers and job
  dependency references validated locally. Hosted CI execution is still unverified.
- User confirmed no additional test machines or CI repository are available.
  No remote was created, no source or binary was uploaded, and clean Windows /
  Linux / macOS execution remains unverified. This is not a waiver of those gates.

### September 23 editing compatibility audit

- Added a complete Python-created → Rust-edited → Python-saved → Rust-reopened
  scenario covering add/remove/replace, manual descriptions, review transitions,
  context invalidation, Unicode text, original creation/error metadata, frame timing,
  byte-identical source PNGs/journal, and immutable one-time metadata backups.
- The negative test exposed a real side effect: replacing a moment with an already
  selected frame failed validation but still published `.rust-selection-edited`,
  suppressing future automatic suggestions. The engine now rejects duplicate choices
  before writing the marker. Metadata, backup state and revision remain unchanged.
- The new regression failed before the fix and passed afterward. A matching C#
  real-engine regression retains coverage independently of Python retirement.
- Focused legacy compatibility, Qt/Rust locking and operation checks: 64 passed.
  Full replacement suite: 48 Rust and 75 C# passed; contract/fmt/Clippy passed.
  Full reference run: 135 passed, one failed with a Python thumbnail callback
  accessing an already-deleted QListWidget during the model-setup success test.
  This is retained separately from the passing migration-specific checks; the
  reference suite is not reported wholly green and Python source is unchanged.
  Focused rerun of all three model-setup cases passed; this does not erase the
  full-suite callback failure or establish its root cause.
  Real-data startup remains guarded pending the final compatibility/cutover audit;
  no existing recordings, installed models or canonical launchers were modified.

### September 23 deterministic native cursor coverage

- Split live pointer acquisition from the existing Rust compositor without changing
  its production path. Native Windows tests create a private 16x16 monochrome cursor
  with an off-centre (3,5) hotspot; they never install it as the user's pointer.
- Pixel-exact tests passed for hotspot placement at negative monitor origins, all
  four clipping edges and corners, fully outside cursors, and hidden cursors.
  White, transparent and inverted monochrome pixels also pass channel-exact checks
  with and without clipping. Tests exercise the real GetIconInfo/DrawIconEx/GDI
  allocation and cleanup path, not a fake rasterizer. The focused capture suite
  increased from one to five tests.
- A read-only check of the live pointer returned CURSORINFO.flags=0 and no cursor
  handle. The unchanged Python live-cursor test consequently skipped on this run;
  this does not resolve or turn its earlier two failures into passes. Colored,
  animated, user-selected cursor shapes and mixed-DPI live coverage remain separate.
- Final verification: all 48 Rust and 74 C# tests passed; contract/fmt/Clippy checks
  passed. A subsequent full Python/reference run passed all 134 tests in 40.75 s,
  including the live cursor check. The earlier failures remain evidence of live
  cursor-state sensitivity, not a diagnosed/fixed production defect. No Python
  source or test assertions were changed. No desktop capture was used for this slice.

### September 23 production recording-control check

- Added `rebuild_native_check.ps1 -StopControl button|shortcut`. This uses the
  production preparation/countdown/outline/controls path, not the harness's direct
  capture calls. Reports require the matching actual stop callback, restored studio
  interactivity, saved frames, reopened review and successful cached/uncached seeks.
- Real Computer Use Ctrl+Shift+F9 input passed in a debug build: 54 unique frames,
  7.728-second synthetic region recording, stop-to-review 302.884 ms. Evidence:
  `.tmp/rebuild/native-check-48d429a536144ca3be4440a85cfa860a/native-check.json`.
  Expected and observed stop source both `shortcut`; the process exited successfully.
- Floating-button attempt did not pass: the automation window inventory did not
  expose the tool window. The 20-second interaction timeout reported failure and
  the launcher enforced its 30-second process-tree deadline. Evidence:
  `.tmp/rebuild/native-check-1908c00a06be484d8e9c36d1291c3580/`.
  This is not evidence that the button is broken, nor a passing button check.
- Failed checks retain their opaque synthetic surface until engine disposal;
  timeout cleanup now requests cancellation first. No unrelated desktop surface
  is intentionally uncovered while capture could still be active.
- Contract/fmt/Clippy checks passed. The reference rerun passed 133 tests but failed
  `test_windows_cursor_is_composited_with_actual_hotspot`; a focused rerun also
  failed (no pixel difference after composing the current live cursor). This is
  retained as unresolved native cursor evidence; no skip or assertion was weakened.
- Replacement tests passed 44 Rust and 74 C# again after the cleanup refinement.
  Packaged control verification, actual floating-button input, countdown cancellation
  and other native/release gates remain open. No canonical output was replaced.

### September 23 native capture/review acceptance slice

- Tightened the native-check launcher to a 30-second whole-process deadline, with
  forced cleanup of only its owned test process tree. Capture requests are limited
  to 5–20 seconds to reserve countdown/stop time. Normal user recordings are unchanged.
- Packaged 790 x 490 synthetic region passed: 38 unique frames in 5.026 s,
  stop-to-review 269.8918 ms. Evidence:
  `.tmp/rebuild/native-check-44512b98185b481d97f9eb8b9240c8a0/`.
- Two packaged 2560 x 1600 synthetic runs passed: 40 samples each, zero missed
  opportunities, 37/38 unique PNGs plus 3/2 exact duplicates; all accepted frames
  were present after reopening. Durations 5.046/5.053 s (about 7.93/7.92 samples/s),
  stop-to-review 310.5946/309.1534 ms, organizing continuing independently.
  Evidence: `.tmp/rebuild/native-check-3dcf5c5e50ab445d91de0bd501cea337/`
  and `.tmp/rebuild/native-check-10244861f4a541e5b7cd32dcf5e8f412/`.
- Corrected a measurement gap: forward/backward seeking could yield zero cached
  samples as originals were replaced with derivatives. The harness now warms a
  small stable working set and fails if no cached seek is exercised. Final run:
  20 cached seeks p95 1.2158 ms, 41 uncached seeks p95 54.3526 ms, dispatch p95
  0.4853 ms. CPU capture pool 49,152,000 bytes; preview peak 129,600,000 bytes plus
  103,680 thumbnail bytes, within the respective 128 MiB budgets. No failed seeks.
- Reviewed the application-only rendered workspace. No matching shell or engine
  process remained after each run. Machine metadata/raw samples are retained.
  Final candidate: `.tmp/rebuild/package-9c77221d88554b8397dd6ed7eb797ebf/`.
  Replacement tests passed 44 Rust/74 C#; reference tests passed all 134.
- These are short changing-color fixture results, not realistic/high-entropy trace
  benchmarks, sustained memory/GPU/encoder accounting, or complete accessibility and
  hardware-interruption acceptance. The harness invokes workspace capture/stop directly:
  actual floating Stop-button/hotkey interaction remains a separate open check.

### September 23 migration — packaged license inventory

- Candidate packaging now collects 359 license/notice files for 197 resolved
  Cargo/NuGet/runtime components. This is a conservative set including build-only
  and other-platform dependencies, not a claim that all 197 ship executable code.
- Missing NuGet texts are pinned to declared source commits with checked-in SHA-256
  hashes. The Inter font's own name table identifies 3.019/git-0a5106e0b; its matching
  upstream font license is included. Rust standard-library and .NET runtime notices,
  Avalonia upstream attributions, Skia and HarfBuzz native notices are included.
- The collector fails on absent license texts or changed supplement hashes/commits.
  Its tests verify component coverage, every output hash, deterministic output,
  overwrite refusal, font attribution, and explicit unresolved release gates.
- Portable and installer candidate with the notice bundle:
  `.tmp/rebuild/package-47f9baf362a0468d8aa8fc18b39969aa/`.
  The playground launcher points there. Redistribution approval remains false:
  FFmpeg corresponding source/linked-library materials and native vendor transitive
  notice review (including ANGLE) remain open; collection is not legal certification.
- A regression run exposed an intermittent Windows access-denied failure during
  synthetic fixture directory publication. Bounded access/sharing-error retries now
  stop after 750 ms total delay, never replace a destination, and have fault tests.
  The subsequent replacement suite passed 44 Rust and 74 C# tests; all 134 legacy
  reference tests passed. Contract/fmt/Clippy checks also passed.

### September 23 migration — Python-free prototype setup and normal test path

- Froze the five original synthetic screenshots plus version-1 project/journal as
  checked-in, hash-pinned assets. All seven files match a fresh legacy generator run
  byte-for-byte; Git attributes prevent metadata line-ending conversion on other OSes.
- The .NET tool embeds these assets and installs them beneath a dedicated repository
  `.tmp` subdirectory. It validates hashes, stages before directory publication,
  rejects linked/unsafe destinations and unmarked nonempty data, and never overwrites
  an existing or incomplete recording. No Python, Pillow, or installed fonts are needed.
- Seven regression cases cover engine opening of the five original review states,
  unchanged source bytes, preservation of edits, incomplete-project refusal and
  invalid destination rejection. The replacement suite passed 44 Rust and 72 C# tests.
- `Build`, `Check`, `Test`, `Prototype`, and candidate packaging now have no Python
  runtime dependency. `Reference` is an explicit additional transition-only command
  for the original Python suite and old/new interoperability checks. Those checks
  remain required until retirement; they were not silently removed from acceptance.
- The separate reference run passed 133 tests; one live cursor-hotspot test skipped
  because Windows reported the cursor hidden. This is recorded as unverified for
  this run, not a passing native cursor check. The managed contract/fmt/Clippy check passed.
- Build-tool instructions are in `app/tools/README.md`. Source deletion, real-data
  enablement, native/release acceptance and repository cutover remain pending.

### September 23 migration — .NET contract generation

- Added the dependency-free `FlowRecorder.Tools` .NET CLI. It generates and checks
  the Rust/C# envelopes and all command payloads, validates golden data against the
  schema subset, and checks the engine command surface for omissions/additions.
- Before switching tools, all four outputs matched the existing Python-generated
  files exactly. After the switch, only the two envelope generator-credit comments
  changed; the retained Python checker also passed against the new outputs.
- Build `Check` and `Test` tasks and the CI contract step now use .NET. Six regression
  tests cover checked-in output parity, drift repair, required/unknown/type-invalid
  golden fields, missing golden coverage, unlisted engine commands, and no writes
  when validation fails. All 65 C#, 44 Rust, and 134 Python tests passed; fmt/Clippy passed.
- Remaining Python tooling: synthetic prototype fixture creation and the legacy
  reference/compatibility suites. The old generator is retained only as transition
  evidence until Python retirement. This tooling change does not alter the IPC protocol.

### September 23 migration — independent encoder acquisition

- Build and package scripts no longer source FFmpeg from the legacy `.venv` or
  require a previous prototype build. PowerShell directly acquires the unchanged
  7.1 executable from its pinned upstream archive, verifying archive and binary hashes.
  The ZIP-format wheel is a download container only; no Python code is executed.
- `app/packaging/ffmpeg.lock.json` records provenance and is included in packages.
  `scripts/test_rebuild_ffmpeg.ps1` verifies empty offline-cache rejection, offline
  extraction, corrupt-binary repair, warm-cache reuse, and corrupt-archive rejection.
- This removes encoder acquisition's Python dependency, not the remaining Python
  contract/fixture tooling or legacy compatibility test dependency. Corresponding
  source and license aggregation remain release gates; redistribution is still disabled.
- Verification: `rebuild.ps1 -Task Test` passed 44 Rust, 59 C#, and 134 Python tests
  plus the new PowerShell encoder checks. `rebuild_package.ps1` produced both the
  portable candidate and installer using independent acquisition:
  `.tmp/rebuild/package-44efb33ac9f6469885cfd98d793e7565/`.
  The playground launcher now selects that candidate; canonical output is unchanged.
- Environment check: this host runs Windows 11 Home Single Language; WSL is not
  installed, Windows Sandbox was not found, and the repository has no Git remote.
  No Linux/macOS execution or clean-machine evidence can be inferred from local builds.

### September 22 fix — recording outline blocking desktop input

- Replaced the full-area transparent Avalonia indicator with a shell-owned native
  layered, input-transparent, non-activating window. A hollow Win32 region removes
  the interior from hit testing independently of compositor transparency. The
  remaining border is capture-excluded and uses physical-pixel coordinates.
- Unexpected native outline closure requests recording stop; intentional teardown
  does not. Cleanup always attempts to close both controls and outline and restore
  studio interactivity. Recording controls no longer request activation on show.
- A stop requested while `start_capture` is awaiting its operation ID is honored
  as soon as the ID arrives. Queued close callbacks are ignored after teardown.
- Automated native-HWND regressions cover layered/transparent/no-activate styles,
  hollow geometry (including negative origins and tiny regions), display affinity,
  external close notification, and idempotent disposal. Full suite: 44 Rust,
  59 C#, 134 Python passed. Contract/fmt/Clippy checks and legacy visual renders passed.
- Added a DEBUG-only two-process input fixture (`--indicator-input-check <report-root>`).
  It uses no capture engine and closes within 30 seconds. Its separate outline process
  crosses an EDGE button while leaving CENTER in the hole. After the interrupted
  attempt, the resumed check passed physical pointer clicks to both CENTER and EDGE
  while the separate outline process was live. The fixture then auto-closed and both
  processes exited. Evidence: `.tmp/rebuild/indicator-input-auto/input-check.json`
  (centerClicks=1, edgeClicks=1, overlayLive=true; distinct fixture/outline PIDs).
  The fixture now starts its outline automatically to fit the observation/action
  sequence within the timeout. End-to-end recording/stop interaction remains a
  separate unverified check; this test did not capture desktop content.
- Refreshed portable/installer candidate:
  `.tmp/rebuild/package-951d8cad642b4e2f9d798e3a2a350d2a/`.
  `Play Phraseback.cmd` selects this build. Existing recordings, Python, and
  canonical `dist/FlowRecorder` were not changed.

### September 22 review fix — responsive interrupted-recording recovery

- The shell prepares every project open using a cancellable engine worker, then
  publishes its prepared snapshot. PNG recovery no longer occupies the command loop
  or inherits a ten-second RPC deadline. Individual commands retain their deadlines.
- Recovery checks cancellation between journal records and PNG rows and before saving.
  Cancellation during validation leaves authoritative metadata and source PNGs unchanged.
  Failed/cancelled preparation preserves the previously opened project and revision.
- An unopenable newest recording no longer disconnects startup: the library remains
  available. Existing unsaved-draft conflict protection remains enforced on reconnect.
- Regression coverage: cancellation within PNG decoding, live-engine responsiveness,
  cancellation/retry, and unavailable newest-project isolation. Full suite: 44 Rust,
  55 C#, 134 Python tests passed. An earlier Python run hit a legacy Qt deleted-widget
  teardown error; the full rerun passed without Python application changes.
- Real C# client with 4,800 synthetic 1440 x 900 journal frames: debug recovery completed
  in 112.061 seconds, with 1,413 progress updates and 1,048 command probes (maximum
  2.950 ms), without disconnecting. Token cancellation also retained the live engine.
  Probe: `.tmp/recovery-client-check/Check.csproj`; data:
  `.tmp/review-recovery-043e17d71d2347db84c172d03193e511/`.
- Optimized candidate engine with a repeated-source synthetic journal: 4,800 frames
  recovered in 8.491 seconds, 108 progress updates, 78 probes (maximum 2.079 ms).
  Evidence: `.tmp/recovery-fixed-5c2f434187f24466a12590390aded76a/`.
  This validates recovery/IPC, not native capture cadence or clean-machine acceptance.
- Final refreshed portable/installer candidate:
  `.tmp/rebuild/package-9919c3e444e744b7b6ffc032e2c58fce/`.
  Its engine passed cancellation and all 4,800-frame recovery checks in 7.737 seconds,
  with 73 command probes (maximum 3.252 ms). Evidence:
  `.tmp/recovery-fixed-f5fd246462de436d84f8dad369f62241/`.
  `Play Phraseback.cmd` now selects this candidate; the already-running preview,
  canonical distribution and real application data were left untouched.

### Existing replacement features

- Version-1 reading, one-time metadata backups, atomic writes, valid-prefix recovery,
  damaged/missing evidence handling, manual-text protection and Qt lock interoperability.
- Revision-checked 128-item metadata pages; UI add/remove/replace selection, context/title
  editing, 350 ms description saves, original-pixel view and a 125 MiB preview LRU
  with a separate 3 MiB reserve for realized-row thumbnails.
  Paged snapshots no longer serialize the complete frame/step arrays before clearing them.
- Bounded background preview derivatives, adjacent-frame prefetch, stale-seek rejection,
  displayed-image protection during speculative eviction, and original-image fallback
  when disposable preview data cannot be read. Original evidence is never rewritten.
- Foreground worker operations with identifiers, cancellation, incremental durable
  generated steps, stale-result guards and pipe-disconnect shutdown. Negotiated IPC push
  notifications replace UI polling; progress coalesces but durable saves do not. Tests
  cover more acknowledgements than the worker queue capacity and terminal ordering.
- Idle disconnect detection and explicit engine reconnect, including failed startup.
  Reconnect retains drafts and restores the recording/selection/preview using a new
  engine session. Conflicting evidence or descriptions block replay; the user can
  copy a retained draft before explicitly discarding it. Missing recordings and live
  lock owners do not cause draft loss. Sibling-engine/default/override path resolution
  is implemented, but both shell and engine retain the development-data guard.
- Shared generated request/response payloads for all 33 commands, with matching
  Rust/C# golden tests, required/null/unknown-field checks and boundary validation.
  Malformed commands cannot mutate state; malformed shutdown no longer terminates
  the engine. Invalid hello fields fail before locking. Worker-specific operation
  detail objects remain explicitly open within the generated operation envelope.
- Metadata-save failure injection at prepublication stages, partial serialization,
  blocked-journal backup cleanup/retry and recovery backup failure/retry tests.
  Full-storage errors are injected; no real disk was filled or quota altered.
- Background organizing no longer delays the recording task's return to review.
  Analysis writes only disposable suggestions; selection publication is a separate
  revision-checked command. The shell declines publication over a pending draft.
  Background notifications have their own coalesced channel, independent of foreground
  saves/completion. Opening another project or starting expensive foreground work
  cancels analysis; changed/cancelled results cannot replace current choices.
- Library pagination returns at most 64 summaries per page. A single read-only worker
  builds an ordered catalog using streaming summary parsing, without constructing
  frame arrays or validating PNG paths. The shell exposes Load more; reconnect and
  operation completion can open a recording outside the first page. Legacy commands
  remain available alongside the five new generated command contracts.
- WGC/D3D11 monitor/physical-region capture, explicit cursor composition, cursor-free
  descriptors, pooled bounded buffers, parallel PNG encoders and ordered journal commits.
- Actual recording setup/region overlay, countdown, exclusion checks, global Ctrl+Shift+F9,
  floating controls and region indicator. The original shortcut is preserved.
- Settling/grouping logic (375/750 ms), retained original frames, grouped alternatives
  and organizing that protects manual text and deliberately changed selections.
- Exact four Qwen3-VL manifests, copied-asset verification, resumable downloads, repair,
  explicit CPU/Vulkan startup, owned authenticated loopback runtime, proxy bypass,
  generation cache namespace, structured-output checks and warm/release lifecycle.
  Removal requires a UI confirmation. Dead warm runtimes are reaped; no-work generation
  retains an otherwise healthy warm runtime. Small evidence is no longer upscaled for
  inference; this processing change has a separate v2 fingerprint namespace.
- GIF/PNG/Markdown export, safe relative image references, literal Markdown content,
  held-state timing, unique transactional staging and owned FFmpeg processes.
- Native synthetic region check and CPU/Vulkan inference checks on isolated data.

## Evidence from this continuation

- `rebuild.ps1 -Task Check` passed after the final notification/preview/lifecycle edits.
- Latest MSVC build and test run (2026-09-21): **29 Rust passed**, **46 C# passed**,
  **127 Python/reference passed**, with no skips. The debug shell/engine build succeeded without warnings.
  The earlier Python run had 111 passes and one live-cursor failure. That test passed
  on the latest run; this does not establish comprehensive cursor-shape/hotspot coverage.
- Twelve new startup/recovery tests cover idle crashes, unsaved-draft retention,
  lost-acknowledgement state, changed descriptions/context/selections, missing projects,
  lock contention and failed-startup retries. Headless tests verify recovery controls
  fit and accept keyboard focus at 1024 × 700 and 1280 × 800. This is not native
  accessibility or all-operation crash acceptance. Latest C# report:
  `.tmp/rebuild/reports/Matt_MATT-LAPTOP_2026-09-21_20_17_33_net10.0.trx`.
  `scripts/visual_check.py` also rendered the unchanged Python reference screens.
- Latest contract/persistence test report:
  `.tmp/rebuild/reports/Matt_MATT-LAPTOP_2026-09-21_20_35_12_net10.0.trx` and
  `.tmp/rebuild/reports/python.xml`. `rebuild.ps1 -Task Check` passed schema/source
  consistency, formatting and Clippy. An earlier run skipped the live-cursor test
  because Windows hid the cursor; it passed in the latest run. No new native capture,
  real model inference, release packaging or clean-machine validation is claimed.
- The five focused Rust-engine operation tests passed, including 300-frame paging,
  all manifest values matching Python, manual-choice preservation, missing-model
  failure persistence, and actual FFmpeg timing/Unicode-path/original-PNG export.
- Rust fake capture produced 50 ordered PNG/journal records and reopened successfully.
- Rust local HTTP tests cover resumed/restarted downloads, corruption, retained partial
  bytes and cancellation under 300 ms. No model assets are downloaded by ordinary tests.
- Native studio, model settings and recording setup inspected. Short native check:
  40 samples, 39 unique PNGs, one exact duplicate, zero missed opportunities; 5.009 s,
  790 × 490 physical pixels on a 2560 × 1600 display; pooled buffers 4,645,200 bytes.
  Stop through organizing: 951.8 ms. This is a debug, short-region check, not full-workload acceptance.
  Evidence: `.tmp/rebuild/native-check-adb0eb09d3414acbbd188493250b9376/`.
- Real Qwen 2B CPU and Vulkan inference completed using copied original assets:
  107.23 s / 42.08 s including verification/startup/inference. These are not separate
  startup/inference measurements and not language-speed comparisons. Structured output,
  durable save, manual edit/reopen and explicit release passed. Output quality still
  requires human review; the model inferred a click on the synthetic validation screen.
  Evidence: `.tmp/rebuild/model-check-6808c257d73d43eabe349b92061bf5e3/model-check.json`.
- Inno Setup 6.7.3 was installed per-user under `.tmp/tooling/` after valid Pyrsys B.V.
  Authenticode verification. On 2026-09-21 Visual Studio Build Tools 18.10.1, MSVC x64
  and Windows SDK installed unattended, with no reboot required. The Microsoft-signed
  bootstrapper is retained under `.tmp/tooling/`. Exact versions and provenance are in
  [tooling/README.md](tooling/README.md). Build scripts now prefer the pinned MSVC toolchain.
- Optimized GNU/LLVM self-contained portable and installer candidates built successfully.
  Candidate: `.tmp/rebuild/package-b4e4a8030cce4132bcca9d6a764e121e/`.
  Local per-user install, installed native recording, same-version reinstall, uninstall,
  and preservation of synthetic data passed with SDK discovery disabled. Installed native
  check: 39 frames / 5.071 seconds; stop-through-organizing 317.2 ms. This is a short-region
  sample, not p95/full-workload acceptance. Evidence: `.tmp/rebuild/installer-check-b16f667e213a44ee8d603468a081aff4/`
  and `.tmp/rebuild/native-check-f9adfc68af7a4dc2bd6b53e1306af69c/`.
  The temporary installation was uninstalled; its source candidate is retained. The
  canonical output is unchanged, and no candidate is approved for redistribution.
  Subsequent source-only fixes (speculative cache pinning, corrupt-derivative fallback,
  nullable protocol results) passed the latest suites but are not in that candidate.
  The later startup/recovery changes are likewise not included in that old candidate.

## Phase 2 evidence (2026-09-21)

- The final `rebuild.ps1 -Task Test` run passed 29 Rust, 46 C# and 127 Python tests.
  C# report: `.tmp/rebuild/reports/Matt_MATT-LAPTOP_2026-09-21_20_59_02_net10.0.trx`.
  `rebuild.ps1 -Task Check` passed shared schemas, formatting and Clippy.
  A final preview-resynchronization guard (for a seek invalidated during suggestion
  publication) also passed all 46 C# tests; report:
  `.tmp/rebuild/reports/Matt_MATT-LAPTOP_2026-09-21_21_01_59_net10.0.trx`.
- New tests cover background analysis without publication, revision/cancellation/open
  invalidation, manual-draft retention during a 4,000-frame synthetic review, independent
  foreground/background notification delivery, shutdown/reacquired ownership, pagination
  through 140/141-recording libraries and reconnect beyond the first page. A blocked
  derived cache still allows analysis and explicit selection publication without changing PNGs.
- One earlier full Python run aborted during Qt background-thread cleanup. The subsequent
  full runs passed; the intermittent reference failure is recorded, not claimed fixed.
  A new library test initially omitted `state: ready`, inadvertently exercising recovery;
  its fixture was corrected and the evidence-path rejection check then passed.
- Native app-owned synthetic check passed: 38 unique frames over 5.064 seconds in a
  790 × 490 physical-pixel region. Stop-to-review was 190.1585 ms, with organizing still
  pending. This is one debug sample, not p95 or ten-minute acceptance. No matching app or
  engine process remained after the check. Evidence:
  `.tmp/rebuild/native-check-461be0d4a6ea41b39b89ee18ed0dede7/native-check.json`.
- Python reference visual scripts passed (`visual_check.py`, `visual_performance_check.py`).
  The 20-run reference-only `benchmark_performance.py` completed under
  `.tmp/rebuild/phase2-reference-benchmark/`. Its historical/updated Python stage timings
  are not a Rust comparison, native cadence test or model inference benchmark.

## Phase 2 review-resources evidence (2026-09-22)

- `scripts/rebuild.ps1 -Task Test`: 31 Rust, 54 C# and 127 Python tests passed, no skips.
  C# report: `.tmp/rebuild/reports/Matt_MATT-LAPTOP_2026-09-22_18_20_29_net10.0.trx`.
  MSVC debug build has no warnings/errors; `-Task Check` passes contracts, formatting
  and Clippy. Shared schema generation now covers 35 commands, including two preferences
  commands; preference writes do not change the active project revision.
- Tests cover preference restart/invalid saves/corrupt fallback/publication failure,
  unchanged legacy metadata, thumbnail recycling/detachment/cancellation/bounded ownership,
  corrupt-derivative fallback, bounded percentile histories, dispatch-probe shutdown and
  seek cache classification. The reference visual scripts also pass; these Python renders
  are distinct from the new Avalonia render below.
- Final native check: 36 unique images over 5.039 seconds, 790 x 490 physical region on
  the 2560 x 1600 display. Capture counters report 40 samples, four duplicates, no missed
  opportunities and 4,645,200 pooled CPU bytes. Stop-to-review was 191.7166 ms while organizing remained pending.
  Twenty cached seeks had p95 1.479 ms; 21 uncached seeks had p95 5.9381 ms. Thirty-eight
  dispatch probes had p95 0.5529 ms. These are short debug-run measurements, not full-workload
  acceptance or a Python-versus-Rust benchmark. Deduplicated PNG count is not sampling cadence.
  Decoded preview pixels peaked at 34,064,800 bytes; two thumbnails occupied 104,400 bytes.
  Working set is reported separately and is not expected to equal the decoded cache size.
- Report and raw bounded samples:
  `.tmp/rebuild/native-check-bc0db09c3c1445118c09fc1f9ced0eee/native-check.json`.
  `review.png` in that directory renders only the application-owned synthetic recording.
  The final UI render was visually inspected: thumbnails, preview and inspector
  are visible without overlap at 1280 x 800. Headless layout tests also cover 1024 x 700;
  native accessibility and minimum-size interaction acceptance are still required.
  No matching shell or engine process remained after the final native check.

## Phase 3 first hardening slice (2026-09-22)

- Fixed cancellation under a saturated save-acknowledgement queue. Normal backpressure
  remains bounded and non-coalescing; cancellation can stop a blocked producer without
  waiting for the forced-exit deadline. Persistence still precedes acknowledgement.
- Closed the warm-runtime expiry gap between periodic cleanup ticks by checking its age
  before reuse. Cancelled work is rejected before acquiring/starting a runtime or encoder.
- Added isolated fake HTTP and supervised-child tests for response validation/limits,
  authentication, connection loss, stalled inference cancellation, healthy reuse and
  expired/dead runtime rejection. A generation-path cancellation test proves an earlier
  generated description survives, manual text remains protected, the interrupted step stays
  pending, model state returns to unloaded and original PNG bytes are unchanged.
- Injected export failures/cancellation clean staging and concat manifests while retaining
  existing export bytes, including Unicode/apostrophe paths. Six new compatibility fixtures
  test interrupted journal/image boundaries and idempotent recovery with metadata backups.
- No installed models or real recordings were used. Python reference visual scripts pass;
  no Avalonia layout change or new native capture validation is claimed for this slice.
- Final `rebuild.ps1 -Task Test`: 37 Rust and 54 C# tests passed; Python reported
  132 passed and one skipped (`test_windows_cursor_is_composited_with_actual_hotspot`,
  because the Windows cursor was hidden). The focused compatibility suite passed all
  30 tests. `-Task Check` passed generated contracts, formatting and Clippy after moving
  the operation tests below production items. Final C# report:
  `.tmp/rebuild/reports/Matt_MATT-LAPTOP_2026-09-22_18_46_13_net10.0.trx`.
- Remaining Phase 3 coverage is itemized in DELIVERY-PHASES.md. The real-data guard and
  release gates remain active; these tests do not justify Python deletion.

## Phase 3 completion and playable candidate (2026-09-22)

- Added automatic warm-expiry, preset/removal, explicit cache bypass/manual-protection,
  active runtime death and capture/finalization fault coverage. Runtime health is polled
  while inference is in flight, so a dead process cannot leave a stalled HTTP request
  occupying generation until its long request timeout.
- Removed the Windows child-startup race: spawn suspended, assign the cleanup job, then
  resume the primary thread. This follows the documented
  [CREATE_SUSPENDED contract](https://learn.microsoft.com/en-us/windows/win32/procthread/process-creation-flags).
  Synthetic parent/grandchild tests prove cleanup on normal drop and abrupt owner death.
- Final suite: 43 Rust, 54 C# and 133 Python tests pass, no skips. Check passes formatting,
  Clippy and generated contracts. Report:
  `.tmp/rebuild/reports/Matt_MATT-LAPTOP_2026-09-22_19_12_52_net10.0.trx`.
  A repeated run exposed inherited nonblocking mode on fake-server accepted sockets;
  explicitly setting them to blocking fixed that harness race. All six inference tests
  subsequently passed five consecutive parallel runs. Reference visual scripts pass.
- Optimized MSVC/.NET self-contained candidate:
  `.tmp/rebuild/package-2df726063ea9461fb329a2a826f7c75b/FlowRecorder/`.
  The same payload produces `FlowRecorder-Setup-0.2.0-win-x64.exe` beside it.
  `Play Phraseback.cmd` launches this candidate using stable, isolated playground data;
  see TRY-THE-APP.md. The candidate does not need developer runtimes to start.
- Packaged shell crash/restart/normal-close test passed:
  `.tmp/rebuild/crash-check-89025c173f614cd1be254fe5c9190d1c/crash-check.json`.
- Packaged synthetic recording passed: 39 frames over 5.012 seconds, 790 x 490 region,
  197.2083 ms stop-to-review. Report/render/machine details:
  `.tmp/rebuild/native-check-a54b4862c0724e80949b7c28ba32e964/`.
  This short run does not establish native-display cadence or memory plateau acceptance.
- Local per-user install, installed recording, same-version reinstall, uninstall and
  synthetic-data preservation passed, with SDK discovery disabled during native launch:
  `.tmp/rebuild/installer-check-548b7adbfc7d4dc7bc6cbc6d9bdb68cc/`.
  Temporary installation was removed. This is not a clean machine or cross-version rollback test.
- Phase 4 remains open for short native-display and hardware/accessibility checks.
  HDR conversion is out of scope under the September 22 amendment. Phase 5
  remains open for notices/source materials, independent FFmpeg sourcing, portable CI,
  clean-machine/cross-version checks and canonical cutover. Python must not be deleted yet.

## Pre-cutover checklist (historical; closed or deferred above)

Current checklist, reconciled September 25; older milestone entries above are history.

- Audit retained Python-only validation utilities and CI references for retirement;
  the opt-in real-model check now has a verified C# replacement. Normal Build/Test/Check and packaging
  already run without Python; Reference intentionally remains transition-only.
- Reconcile the source rollback archive with final changes, switch the canonical
  payload/launcher/build commands, remove the Python implementation and obsolete tooling,
  and reorganize source ownership/documentation around Rust and Avalonia. Verify the
  final layout's build, tests, packaged startup and artifact hashes after moving files.

Completed evidence must not be reopened solely because a historical checklist says otherwise:
shared-data startup and lock compatibility, journal fault/recovery tests, generation lifecycle,
user-confirmed floating Stop/countdown cancellation, component keyboard navigation at both
window sizes, repeated current-build short native performance, separate memory accounting
and synthetic endurance, scoped native dependency notices/source materials, and local
installer replacement/rollback/data preservation are recorded in dated entries above.

Clean-Windows and Linux/macOS execution are not release or Python-retirement gates.
4K and HDR are outside scope. Native test processes retain the 30-second safety deadline;
normal app recordings have no automatic cutoff. No canonical files have been replaced yet.

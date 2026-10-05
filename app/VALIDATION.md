# Rebuild validation record

**Latest evidence:** [IMPLEMENTATION-STATUS.md](IMPLEMENTATION-STATUS.md) records the
subsequent capture/model/export implementation, native synthetic check, real CPU/Vulkan
inference and latest test results. Results below are the earlier milestone-2 baseline,
not the current feature boundary or latest aggregate result.
The current workload is defined by [SCOPE.md](SCOPE.md): the user's native display
and smaller regions. Historical raw reports retain their original wording.

## Current native fixture options

`scripts/rebuild_native_check.ps1 -Workload detailed -Seconds 5 -FullDisplay`
uses an opaque application-owned synthetic workspace instead of the default
flat-color fixture. It changes rows, selection, progress text/bars and a brief
validation message on a steady dark background. The same capture, persistence,
review-seek and GPU/memory diagnostics run, with the whole-process 30-second
deadline unchanged. Pass `-PackagedPayload <candidate>/Phraseback` to test a
self-contained candidate and `-UnmarkedDataRoot` to exercise release startup in
the newly created isolated directory.

This is a structured synthetic workload, not an actual third-party application
trace or an accessibility/interactive-control acceptance test. Its decorative
workspace is deliberately noninteractive; the existing global stop shortcut is
registered by the harness. Raw evidence stays under the generated `.tmp/rebuild`
directory. Reports identify the selected workload so results are not conflated.

## Earlier milestone-2 validation

Date: 2026-09-20. Scope: the isolated Windows rebuild, not a replacement release.
The user cleared the initial design checkpoint and requested continued implementation.
Milestone 2 is in progress. No real recording/model data, canonical launcher, or
`dist/FlowRecorder` payload was changed. One compatibility-preserving Python runtime
fix adds bounded retries for transient Windows export publication failures.

## Build and automated results

The local build uses .NET SDK 10.0.401, Avalonia 12.1.2 and Rust 1.98.1 with the
development LLVM-MinGW fallback documented in `README.md`. This is a debug build;
it does not validate the intended MSVC release toolchain or performance targets.

- `./scripts/rebuild.ps1 -Task Check`: passed shared envelope/schema consistency,
  Rust formatting and Clippy with warnings denied.
- `./scripts/rebuild.ps1 -Task Test`: shell build passed with zero warnings and
  zero errors; **9 Rust tests, 20 C# tests and 107 Python tests passed**, with no
  skipped tests in the latest run. The command returned exit code 0.
- **35 focused interoperability tests passed**: 15 legacy/persistence tests and
  20 Windows Qt-lock tests. They cover
  Python-to-Rust-to-Python annotations and image hashes, incompatible protocol
  rejection before locking, optional legacy fields, failed backups/atomic saves,
  truncated journals, damaged PNGs, missing evidence, invalid replacement indices,
  and Qt/Rust launch order, simultaneous startup, crashes and stale-lock safety.
- C# tests include real-engine durable acknowledgements and stale revisions,
  retained drafts on failed saves, decoded evidence, save/review/reopen behavior,
  golden envelopes, layouts at 1280 × 800 and 1024 × 700, and simulated design states.
  A new regression proves missing evidence clears the old preview without losing
  manual annotations or preventing editing.
- `test/test_export.py`: **8 passed** in the focused run and both additional runs.
  The new publication tests cover transient locks, permanent denial, cancellation
  and an already-existing destination.
- `./.venv/Scripts/python.exe scripts/visual_check.py`: passed, exit code 0.
  The harness now waits for the existing asynchronous Qt shutdown before Python
  exits; its earlier teardown crashed after producing the two offscreen images.

Machine-readable C# and Python reports are under `.tmp/rebuild/reports/`. Python
test scratch files are under `.tmp/pytest/`; builds are not under pytest's base.

### Earlier reference failures and current disposition

Earlier full runs were not green:

- `test_windows_cursor_is_composited_with_actual_hotspot`: the composited image
  matched the background despite Windows reporting a visible cursor. A targeted
  rerun also failed; another run skipped because Windows hid the cursor. It passed
  in the latest full run. No cursor code was changed, and comprehensive native
  cursor-shape/hotspot validation remains outstanding.
- The existing export tests intermittently received
  `PermissionError: [WinError 5] Access is denied` when renaming the completed
  export staging directory. The Python reference now retries only Windows errors
  5/32/33, at most ten attempts with 50 ms cancellable waits. It does not retry
  other errors or replace an existing destination. Permanent errors still surface.
  The particular external handle/process causing the transient denial was not
  identified; no Windows security or filesystem permission settings were changed.

The recorded passing runs establish the current automated baseline, not native
or release acceptance. Export formats, timing, staging and cancellation behavior
remain covered by the original tests; source recordings were not modified.

## Earlier native prototype checks

The development executable was launched against `.tmp/rebuild/prototype-data`.
During native inspection:

- The charcoal workspace rendered with restrained blue controls, local fonts,
  source evidence, selected moments, playback controls and the description inspector.
- Selecting a moment and using the down-arrow changed the evidence, time and
  description together. Accessibility exposed labeled description fields and controls.
- A manual description appeared in the saved fixture and remained visible on
  subsequent inspection. Automated real-engine tests additionally verify reopen.
- The interaction-preview window displayed the generating state and its cancel
  action changed to the explicit cancelled state with completed progress retained.
  This is a simulation, not model generation or cancellation validation.
- Normal native close completed; a subsequent process check found no remaining
  prototype shell or engine processes before rebuilding.

The final automation recovery encountered an input-geometry error. Fresh window
selection restored accessibility inspection and normal keyboard close. A later
screenshot did not match the target window, so it was not accepted as visual
evidence. Earlier native checks and the final headless tests are distinct evidence.
No claim is made for a complete keyboard audit, Narrator, mixed DPI, or packaging.
The later missing-preview change was verified with headless interaction coverage;
the native checks below were not rerun for that behavior.

## Reference parity map and remaining proof

- **Durable projects and recovery:** `test/test_project.py` is the Python reference.
  Rust implements a development subset, with backups, atomic metadata replacement,
  valid-prefix journals, path checks and manual-text preservation. Optional legacy
  defaults, missing/corrupt images, failed replacement and Windows Qt locking now
  have passing tests. Recovery preserves existing notes/identifiers where evidence
  survives and refuses to discard annotated moments whose evidence is missing.
  Complete fault injection, large-library paging and full editing remain milestone
  2 work. Shared real-data access stays disabled.
- **Capture and cursor:** `test/test_capture.py`, `test/test_cursor.py` and
  `scripts/native_capture_check.py` define the reference. No Rust WGC backend,
  overlays, exclusion readiness, global stop control or SDR/HDR pipeline exists yet.
- **Selection and review:** `test/test_performance.py`, `test/test_ui.py` and the
  Python selection implementation are the reference. The prototype has real
  playback/edit/save behavior but only simulated grouping. Settling, alternatives,
  full editing commands, virtualized paging and bounded caches remain incomplete.
- **Models and generation:** `test/test_model.py`, generation-related UI tests and
  the existing verification scripts are the reference. No Rust model installer,
  llama.cpp supervisor, inference, warm lifecycle or model migration is implemented.
- **Export:** `test/test_export.py` is the reference, including bounded Windows
  publication retries. Rust export and bundled FFmpeg remain unimplemented.
- **Packaging:** the Python build remains canonical. Self-contained Avalonia
  publication, MSVC release builds, Inno Setup, clean-machine testing and rollback
  acceptance remain unverified and are not shipped by this prototype.

The GitHub Actions matrix is authored but has not run remotely. Linux/macOS
compilation and test results are therefore not claimed. No ten-minute native-display capture,
memory plateau, seek-latency, stop-to-review, real inference, or release benchmark
result is available for the rebuild.

## User design checkpoint

Run `./scripts/rebuild.ps1 -Task Prototype` after closing any previous prototype.
Review the main workspace and **Prototype states**, including grouped alternatives,
save failure, cancellation, recording setup and settings. Description edits in
the main workspace are real and persist only in the synthetic development fixture;
actions in the design-state window are simulations.

The initial checkpoint is cleared for further implementation; no additional design
decision is blocking milestone 2. The remaining milestones in `README.md` are not
complete, and final interaction/accessibility acceptance remains required.

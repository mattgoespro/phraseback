# Remaining delivery phases

September 25 cutover: source is now under
`app/`, Python is in the rollback area, and final post-move build/publication verification
is complete. Canonical payload, installer and launcher checks pass; see the final
IMPLEMENTATION-STATUS.md entry. Earlier phase entries are historical; SCOPE.md governs acceptance.

This execution order implements the remaining work from the accepted rebuild plan.
It does not replace the original milestones. The user-approved resolution and
September 25 acceptance amendments in SCOPE.md apply throughout. Python stays available until
the replacement passes acceptance and a recoverable reference backup is verified.

## Phase 1 — Startup, connection recovery and contracts

Implemented in the first phase slice:

- Detect an idle engine disconnect without waiting for the next user command.
- Offer explicit reconnect, including after an initial startup failure.
- Dispose the old transport before starting another engine and acquiring its lock.
- Recover the current recording, selection and preview with a new session/revision.
- Retain unsaved descriptions in the shell. Do not automatically replay them after
  reconnect. An explicit save uses the recovered revision.
- Refuse draft replay if context, frame references, selected evidence or the saved
  description changed. An exact committed draft is accepted after a lost acknowledgement.
- Provide a selectable draft and explicit discard confirmation for conflicts or
  missing recordings. Failed reconnects do not discard drafts or steal live locks.
- Resolve a sibling engine executable and the FLOW_RECORDER_DATA override, while
  preserving explicit development arguments. Normal user data remains guarded.

Implemented in the contract/persistence slice:

- Shared method-specific request/response schemas and generated contracts are now
  complete for the original 28 commands (Phase 2 adds five). Both runtimes exercise common golden examples and
  reject malformed payloads. Worker-specific operation-detail objects remain an
  explicitly open object within the typed operation contract.
- Metadata persistence fault coverage now includes injected full-storage/access failures
  before publication, partial serialization, a blocked journal during backup and
  recovery backup failures. These tests preserve bytes, revision and retry behavior.

Shared-data gate update (September 23):

- Capture-journal interruption, Qt lock interoperability, full editing round trips,
  cancellation/draft recovery and Windows child cleanup now have passing coverage
  documented in IMPLEMENTATION-STATUS.md. Release startup supports normal roots;
  debug startup remains isolated. Fresh-root and invalid-handshake behavior is tested
  against both build modes without using the real library. Final cutover stays gated
  by native and release acceptance below.

The contract/persistence implementation slice passes 29 Rust, 42 C# and 121 Python
tests on 2026-09-21. Injected full-storage errors are not a real volume-exhaustion test.
The remaining data-access and lifecycle gates stay open while Phase 2 work proceeds.

## Phase 2 — Responsive review and bounded library work

Implemented:

- Recording returns to interactive review without waiting for full organizing.
  Background results require explicit operation/project/revision-checked publication;
  unsaved drafts, edited selections, cancellation and project switches take precedence.
- Foreground and background notifications have separate bounded/coalesced channels.
  The workspace stays editable during analysis, with separate cancellation and a
  layout-stable status line. Derived-cache failure does not block review.
- The shell loads 64 library summaries at a time through a Load more action. A single
  read-only worker indexes metadata without constructing frame arrays or validating
  PNG paths. Catalog snapshots preserve order and reject stale pagination cursors.
  Reconnect can recover a project that is not on the first library page.

Implemented in the review-resources slice (2026-09-22):

- Selected-moment thumbnails load asynchronously only for realized rows. Recycling,
  cancellation and shutdown release their pixels; stale project results are rejected.
  One thumbnail decoder and at most 32 retained/pending thumbnail reservations bound work.
  Corrupt derivatives fall back to originals without rewriting evidence.
- The decoded review budget is split into a 125 MiB preview LRU and a 3 MiB thumbnail
  reserve. Cache eviction/peak counters and thumbnail ownership tests cover these limits;
  transient decoder allocations and original-pixel dialogs are not cache allocations.
- Window size and maximized state persist through engine-owned, atomic writes to
  `ui-preferences-rust-v1.json`, outside legacy project metadata. Restored dimensions fit
  the current display; no screen position or recording region is remembered. Invalid
  dimensions are rejected, corrupt preferences fall back, and failed saves remain visible.
- Bounded timing windows distinguish cached/uncached seeks and decode duration. A single
  outstanding background-to-UI probe measures dispatch latency, not timer intervals.
  Content-free raw timing samples, memory samples, cache counters and existing native
  capture counters are retained by the synthetic check. Memory history is limited to
  1,024 samples at approximately one-second intervals; timing histories retain 1,024 each.

The review-resources slice passes 31 Rust, 54 C# and 127 Python tests. A native synthetic
check renders the updated Avalonia workspace and exercises repeated seeking. Repeated
native measurements, GPU/encoder allocation accounting, accessibility and clean-machine
acceptance were left for Phases 4 and 5. The scope amendment below replaces disruptive
ten-minute runs with short native tests and non-disruptive synthetic endurance.

The native check now reports stop-to-review separately from background organizing.
Its short synthetic-region result does not establish sustained native stability.

## Phase 3 — Failure and lifecycle hardening

Implemented in the first hardening slice (2026-09-22):

- Save acknowledgements retain bounded backpressure but no longer block cancellation
  when their queue is full. Already persisted progress remains available on reopen;
  terminal status retains the last durable result if cancellation prevents enqueueing.
- A warm runtime is checked for expiry at acquisition, not only by the periodic reaper.
  Tests exercise healthy reuse, expired/dead runtime rejection and explicit release
  using isolated supervised test children, without installed model assets.
- Loopback fake-server tests cover bearer authentication, valid/invalid JSON, oversized
  responses, connection loss and cancellation of a stalled inference request. A complete
  generation-path test persists one description before cancellation of the next request,
  preserving manual text, pending evidence and original PNG bytes.
- Export cancellation and encoder-failure tests verify transactional staging cleanup,
  manifest cleanup and unchanged existing destinations. A cancelled export does not launch
  an encoder. The injected encoder tests do not replace the existing real-FFmpeg tests.
- Six interruption fixtures cover PNG publication without journal commit, truncated
  frame/duration records, missing/corrupt images and out-of-order timestamps. Rust recovery
  preserves the valid prefix, original bytes/backups and manual text, reopens idempotently,
  and remains readable by Python.

Implemented in the remaining automated hardening slice (2026-09-22):

- Timer-driven warm expiry, preset changes, isolated model removal, targeted cache use,
  forced cache bypass and manual replacement/protection now have lifecycle coverage.
- Active inference also polls child health. If the runtime dies while an HTTP connection
  stalls, the failed moment is persisted promptly instead of waiting for a ten-minute timeout.
- Windows runtime/encoder children start suspended, join their kill-on-close job, then
  resume. Tests verify descendant cleanup after normal drop and abrupt owner termination.
- Packaged shell-crash testing verifies that its job kills the engine, the next launch
  reacquires the same data root, and normal close subsequently cleans up both processes.
- Actual fake-capture pipelines now exercise injected encoder exhaustion, partial/failed
  journal writes and failure immediately before final metadata publication. Workers stop,
  the journal prefix recovers, and existing PNG bytes remain unchanged.

Phase 3's automated fault/lifecycle work is implemented. Injected errors are not claims
of real power-loss or volume-exhaustion testing. Native acceptance and release cutover
remain gated by the following phases and the outstanding shared-data acceptance checks.

## Phase 4 — Native Windows acceptance

September 25: the user verified the floating Stop button and countdown cancellation.
These checks are closed; do not retry them solely because the automation tool could
not target the floating controls. Other local checks below remain distinct.

September 23 memory-accounting update: encoder heap is measured separately with
flat/noisy synthetic cases; native capture records mapped staging sizes and sampled
DXGI local/nonlocal engine-process GPU usage. Bounded histories and short native
checks pass; see CAPTURE-MEMORY.md for evidence and measurement limits. This closes
the separate allocation-accounting implementation, not the remaining native
interaction, hardware-interruption, accessibility or realistic-workload checks.

The native launcher now supports explicit 5–20 second recordings and an opt-in full-display
synthetic surface, with machine metadata and raw bounded timing/memory samples. Short
portable/installed runs pass. Full-display runs require an uninterrupted display session;
Device-interruption and assistive-technology acceptance are not yet complete.
The launcher enforces a 30-second whole-process deadline. September 23 packaged native
2560 x 1600 runs passed capture/stop/reopen/review with zero missed opportunities;
see IMPLEMENTATION-STATUS.md for exact samples and remaining verification limits.
The September 22 scope amendment removes HDR and ten-minute disruptive native runs.
Use at most 20 seconds of capture for fullscreen checks, reserving time for setup/stop.

- Retain explicit SDR-only validation, then complete native cursor/DPI/hotkey/display
  interruption, sleep/lock and accessibility checks.
- Measure short native-display runs, cadence, stop-to-review and seek percentiles and
  UI responsiveness. Use non-disruptive synthetic endurance checks for memory plateaus,
  without claiming native long-duration proof. No 4K or HDR requirement applies.

## Phase 5 — Release and cutover

September 25: local installation, different-build upgrade/rollback, forward
restoration, reinstall and uninstall pass with all installed payload hashes and
synthetic recording/model bytes verified. Both candidates are version 0.2.0;
this proves installer replacement/rollback, not different-version schema changes.
See IMPLEMENTATION-STATUS.md for retained evidence.

An optimized MSVC self-contained portable candidate and installer are available for local
playground use through `Play Phraseback.cmd`; see TRY-THE-APP.md. This interim artifact
does not mean Phase 5 is complete or bypass Phase 4. Existing data and canonical output remain guarded.

- Produce the optimized MSVC portable/installer payload, complete dependency notices
  and FFmpeg corresponding-source materials, and remove remaining Python build tooling.
  Encoder acquisition is now independent of Python, with pinned archive/binary hashes
  and offline/corruption tests (`scripts/rebuild_ffmpeg.ps1`, September 23).
  The minimal source-built PNG/GIF encoder and its verified source/license kit are
  now bundled; the September 25 scoped native vendor notice review is complete.
  See packaging/licenses/NATIVE-REVIEW.md and the latest
  IMPLEMENTATION-STATUS entry for exact artifact and test evidence.
- Retain packaging/artifact CI and optional portable jobs. Clean-Windows and
  Linux/macOS execution are not acceptance gates, per the September 25 user amendment.
- Validate local Windows startup, disconnected workflow, upgrade, uninstall and rollback.
- Only after acceptance: replace the canonical payload/launcher, verify a recoverable
  Python reference backup, then remove the Python implementation.

Builds, headless tests, native tests, model tests and clean-machine release tests are
separate evidence. A passing check in one category does not close another category.

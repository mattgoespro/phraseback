# Windows-first replacement scope

## Electron UI migration amendment (2026-10-03)

The user requested a Windows Electron UI to replace Avalonia while retaining the
Rust engine, version-1 projects, local-only operation, portable folder, and Inno
installer identity. Electron was built and tested beside Avalonia, then became
the active release on 2026-10-04 after feature, native Windows, performance,
and packaged acceptance. The previous Avalonia release remains available in
`reference-backups/` for rollback. The approved compact dark Review design and
later UI corrections are the visual baseline. No project-data or engine rewrite
was authorized. See `electron/RELEASE-ACCEPTANCE.md`.

The older statement below that names Avalonia as the agreed direction records the
previous release scope; this amendment supersedes that UI choice. Existing capture
and performance acceptance limits still apply unless explicitly changed.

## Historical Avalonia direction (superseded by the Electron amendment)

Updated 2026-09-25 following the user's explicit scope changes. This amendment
supersedes resolution, HDR, native-test-duration and external-environment acceptance
requirements in the original implementation plan.
The Rust engine, C# Avalonia shell, six delivery milestones, compatibility contracts,
offline workflow, portable package and per-user installer remain the agreed direction.

## Product vision and supported workload

Build a responsive, local-only recording and review studio for the user's actual
Windows desktop. Record one display or a rectangular region, preserve original
evidence, curate selected moments, generate local descriptions, and review before export.

The reference display is the user's native 2560 × 1600 display observed during native
validation. Validate short recordings at that native resolution and representative
smaller regions, with an 8 FPS sampling target. Native test recordings must not exceed
30 seconds. Prefer shorter runs to allow for countdown and finalization; do not schedule
ten-minute desktop-covering tests. Record the actual display mode and reference hardware
with each benchmark. Normal user recordings have no new duration cutoff. Short-run
results do not establish long-duration stability; use non-disruptive synthetic endurance
checks where possible and clearly label their limitations.

This release supports SDR/sRGB capture only. HDR capture, tone mapping, HDR test hardware
and HDR acceptance are out of scope and must not block completion. Keep the existing
color-space check: when HDR is active, explain that the user must turn it off for the
chosen display before recording. Do not silently save incorrectly interpreted colors.

4K support, dedicated optimization, test hardware, benchmarks and release acceptance
are out of scope. No such requirement may block completion or Python retirement.
Do not add an artificial resolution restriction to the capture implementation merely
to enforce this scope; any unvalidated larger mode is outside the supported workload.

## Performance acceptance

- At least 7.6 effective samples per second on the short changing-content fixture
  at the reference display's native resolution. Count samples separately from unique PNGs.
- Stop to interactive review: p95 no greater than one second. Full suggestion analysis
  may continue separately; it must not delay initial evidence access.
- Cached seeks: p95 no greater than 100 ms. Uncached preview seeks: p95 no greater than
  250 ms on the reference SSD.
- No capture, decode, hashing, model or export work on the UI thread. Measure dispatch
  latency under load, not just timer intervals in a short native smoke check.
- Keep the 128 MiB full-resolution CPU capture-buffer budget and separate 128 MiB
  decoded-image budget. Measure GPU and encoder allocations separately and verify a plateau.
- Preserve bounded cancellation, graceful shutdown deadlines and forced child cleanup.

## Current release gates

On September 25 the user confirmed that the floating Stop button and countdown
cancellation function properly. Those interaction checks are accepted on the basis
of user verification; earlier automation timeouts remain historical evidence only.

The user also explicitly removed clean-Windows and Linux/macOS acceptance checks
from migration completion and Python-retirement requirements. Do not raise their
unavailability as a blocker again or request those environments. Existing optional
portable CI jobs may remain, but their execution is not a cutover prerequisite.
Do not claim testing that was not performed. macOS and Linux capture remain future adapters.

The migration is complete. Data compatibility, recovery, local installation,
upgrade, uninstall and rollback evidence is recorded in IMPLEMENTATION-STATUS.md.
The retired Python reference remains recoverable from its verified backup.

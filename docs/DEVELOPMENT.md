# Development and release

Run commands from the repository root in PowerShell 7. Use isolated data under
`.tmp/`; preserve normal libraries, recordings and model assets. Supported
workloads, performance limits and deferred checks are in [SCOPE.md](SCOPE.md).

## Setup

Use Rust 1.98.1 with rustfmt and Clippy, Node 24.20.0/npm 11.19.0, MSVC x64 and
the Windows SDK. Import `tooling/windows-build-tools.vsconfig` when installing
Visual Studio Build Tools. Cargo pins the engine dependencies; the UI lockfile
pins its dependencies. Installer builds verify `packaging/inno.lock.json` and
the compiler at `.tmp/tooling/inno-6.7.3/ISCC.exe`.

```powershell
npm ci --prefix packages/ui
./build.ps1 -Task Check
./build.ps1 -Task Test
./build.ps1 -Task Build
./build.ps1 -Task Prototype
```

`Prototype` installs a checksum-pinned synthetic walkthrough. Existing fixture
data is verified, never reset automatically. Debug engines require a marked
development root; release builds support the normal data-root rules.

## Engine contracts

Schemas and golden messages are in `packages/engine/contracts/`. After schema
changes, run `npm run generate:contracts --prefix packages/ui`, then Check and
Test. Generation verifies Rust and TypeScript outputs, golden command coverage
and the engine dispatch surface.

- IPC uses four-byte little-endian lengths followed by UTF-8 JSON, with an
  8 MiB maximum. Stdout is protocol-only; stderr excludes user content.
- A compatible `hello` precedes data-root locking or writes. Requests are
  sequential; replies match request IDs and the engine session UUID.
- Required nullable fields must be present; unknown fields and null evidence
  elements are rejected. Version-1 optional fields retain their defaults.
- Coalesced progress never replaces durable `operation_saved` acknowledgements;
  drain those acknowledgements before terminal state. Bound all queues.
- Edits require recording identity and revision. Failed persistence preserves
  the in-memory revision. Original PNGs remain authoritative and unchanged.
- Open/recovery runs through cancellable `prepare_open`; publish only a completed
  prepared snapshot. Background organization writes only through the separate
  revision-checked `apply_organization` command.
- Preview paths reference saved evidence. A missing original cannot resolve to
  an orphaned cached image. Capture, inference and export remain outside the
  renderer interaction path.
- Preserve Qt-compatible `application.lock` ownership: exclusive create,
  conservative stale-owner reclamation, reparse-point rejection and normal
  release of matching metadata. Retain `.rust-development.lock` compatibility.

Capture accounting separates the reusable CPU image pool, decoded previews,
encoder heap and sampled engine GPU usage. `pool_bytes` is not total process
memory. Streaming PNG output uses a 64 KiB chunk buffer, which does not bound
all compressor allocations. Opt-in accounting lives in
`scripts/rebuild_encoder_memory.ps1`; synthetic endurance uses
`scripts/rebuild_endurance_check.ps1`.

## UI baseline

Review has one dominant evidence canvas, with compact playback and moments below
it. Use a near-black canvas, lighter neutral grey header, restrained violet accent
and charcoal elevated controls. Define boundaries through spacing and elevation;
use borders sparingly. The recording breadcrumb is the picker, with an aligned
chevron inside its label. Center the Play label. Keep secondary actions in menus
or collapsible details; omit the permanent Moments heading/toolbar, Edit moment
button, reviewed-count label and green glows. Menus fit their contents and use
grey hover states. Inspect primary flows at 1280 × 800 and 1024 × 700, including
scrolling menus, empty/error states and keyboard focus.

## Packaged validation

Finish implementation and the source suites before making a candidate. Run native
checks sequentially and stop on failure. Each disruptive native test process must
finish within 30 seconds. Keep source tests, native UI, real inference and packaged
results separate; rebuilding creates a new candidate requiring fresh acceptance.

```powershell
./scripts/electron_candidate.ps1
$candidate = (Get-Content .tmp/electron/latest-candidate.json -Raw | ConvertFrom-Json).candidate
./scripts/electron_verify_candidate.ps1 -Candidate $candidate
$exe = Join-Path $candidate 'Phraseback-win32-x64/Phraseback.exe'
```

Native scripts live in `packages/ui/scripts/` and create isolated fixtures.
Run them from `packages/ui`, passing `--packaged $exe` unless noted below.

| Coverage | Scripts and additional arguments |
| --- | --- |
| Screens, edits, prompts | `native-layout`, `native-smoke`, `native-curation`, `native-prompt-consistency` |
| Organization | `native-organize`, `native-organization-lifecycle`; repeat the latter with `--edit-during` |
| Capture | `native-capture --performance` five times; separately use `--exclusion`, `--hotkey`, `--floating`; also run `native-region` |
| Hotkey conflicts | `native-hotkey-conflict` takes `$exe` as its positional argument |
| Review/load performance | `native-review-performance`, `native-review-endurance`, `native-open-cancel`, with `--root $captureRoot` |
| Recovery | `native-interrupted-capture`, `native-recovery-conflict`; repeat the latter with `--missing` |
| Export | `native-export`, `native-export-cancel --root $captureRoot` |
| Lifecycle | `native-preferences`, `native-reconnect`, `native-close`, `native-forced-close` |

Names above have the `.mjs` extension. For example:

```powershell
Push-Location packages/ui
try {
  node scripts/native-smoke.mjs --packaged $exe
  if ($LASTEXITCODE -ne 0) { throw 'Packaged smoke failed' }
  $captures = 1..5 | ForEach-Object {
    $result = node scripts/native-capture.mjs --packaged $exe --performance | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Packaged capture failed' }
    $result
  }
  $captureRoot = $captures[0].root
} finally { Pop-Location }
```

Compare capture/seek/memory reports with SCOPE.md. Inspect screenshots under
`.tmp/electron/layout-*.png` against the UI baseline above. The exclusion test
must show the window covering its marker while the saved PNG retains the marker
beneath. Short native recordings do not establish long-duration stability.

For real-model checks, set `PHRASEBACK_MODEL_FIXTURE` to verified assets already
staged under `.tmp/`. Run `native-model-verify.mjs --packaged $exe`, take its JSON
`root` as `$modelRoot`, then run `native-model-controls`, `native-generate --root
$modelRoot`, `native-generation-cancel`, `native-model-cancel`, and
`native-model-lifecycle --root $modelRoot`, all with `--packaged $exe`.
Rust mock-server tests cover download failures; fixture reuse does not establish
a fresh multi-gigabyte native download.

## Installer and release

The installer test refuses to overwrite an existing installed Phraseback identity.
Use a different verified candidate under `.tmp/electron/` for `$previous`:

```powershell
./scripts/rebuild_installer_check.ps1 -Candidate $candidate -PreviousCandidate $previous
```

It verifies replacement, rollback, forward restoration, reinstall, uninstall and
synthetic-data hashes. Same-version replacement does not prove cross-version
project migration.

Review inventories in `licenses/npm/`, `licenses/cargo/`, `licenses/electron/`
and the pinned FFmpeg source kit. Packaging conservatively inventories build
dependencies too. The accepted 2026-10-04 package classified missing local texts
for extract-zip, esbuild platform binaries, Rollup platform binaries and err-code
as build-only/unshipped. Recheck that classification for each new candidate.
Shipped notices and encoder source instructions remain in `packaging/`.

Acceptance must bind the exact candidate summary hash to
`rust_and_electron_tests`, `packaged_native`, `installer_rollback`, `visual_review`
and `notices`, using phase `electron_update` and `redistribution_approved: true`.
Candidate generation does not approve its own release.

```powershell
./build.ps1 -Task Release -Candidate $candidate -AcceptanceFile .tmp/electron/release-acceptance.json
```

Release verifies hashes, retains the previous Electron release under
`reference-backups/` and is idempotent for an already published candidate.
`dist/release-acceptance.json`, `dist/package-summary.json` and
`dist/build-provenance.json` identify the accepted bytes. Keep detailed test
reports under `.tmp/` instead of adding per-run documentation to source folders.

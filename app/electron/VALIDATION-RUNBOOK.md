# Electron release validation

Run these checks only after implementation changes are finished. Every native script
uses an isolated root under `.tmp/`; leave the normal Phraseback library closed.
Run commands in the foreground, one at a time. Stop at the first failure and keep
its output and `.tmp` evidence for diagnosis. The exact candidate that passes is
the one eligible for release; rebuilding it afterwards creates a new candidate.

## 1. Source and contract suites

Use PowerShell 7 from the repository root:

```powershell
./build.ps1 -Task Check
./build.ps1 -Task Test
```

These commands build/check the pinned Rust engine and run TypeScript contracts;
`Test` also runs the Rust suite. The one-time Avalonia regression gate was run
before the 2026-10-04 cutover and is recorded in `RELEASE-ACCEPTANCE.md`.

## 2. Exact packaged candidate

```powershell
./scripts/electron_candidate.ps1
$candidate = (Get-Content .tmp/electron/latest-candidate.json -Raw | ConvertFrom-Json).candidate
./scripts/electron_verify_candidate.ps1 -Candidate $candidate
$exe = Join-Path $candidate 'Phraseback-win32-x64/Phraseback.exe'
```

Keep `$candidate` and `$exe` in the same PowerShell session. The candidate
command compiles a pinned MSVC release engine, checks the Electron source and
real-engine contract, and makes the portable folder and installer. The verifier
checks every payload file and the installer against their inventory. Inspect
`app/packaging/ELECTRON-NOTICE-REVIEW.md` against this exact candidate before
approving redistribution; its initial metadata deliberately says
`redistribution_approved=false`.

## 3. Native Windows and interaction checks

These tests control the real desktop. The capture tests cover it briefly; each
recording stays below the 30-second disruptive-test limit. Keep the reference
display in SDR at its native 2560 × 1600 mode, and record the current scaling
value. From the repository root:

```powershell
Push-Location app/electron
try {
  function Invoke-Native([string]$Script, [string[]]$Arguments) {
    & node $Script @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Script failed with exit code $LASTEXITCODE" }
  }
  Invoke-Native -Script scripts/native-layout.mjs -Arguments @('--packaged',$exe)
  Invoke-Native -Script scripts/native-smoke.mjs -Arguments @('--packaged',$exe)
  Invoke-Native -Script scripts/native-curation.mjs -Arguments @('--packaged',$exe)
  Invoke-Native -Script scripts/native-organize.mjs -Arguments @('--packaged',$exe)
  Invoke-Native -Script scripts/native-organization-lifecycle.mjs -Arguments @('--packaged',$exe)
  Invoke-Native -Script scripts/native-organization-lifecycle.mjs -Arguments @('--packaged',$exe,'--edit-during')
  Invoke-Native -Script scripts/native-prompt-consistency.mjs -Arguments @('--packaged',$exe)
  $captures = 1..5 | ForEach-Object {
    $result = node scripts/native-capture.mjs --packaged $exe --performance | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Packaged capture performance failed' }
    $result
  }
  $captureRoot = $captures[0].root
  Invoke-Native -Script scripts/native-capture.mjs -Arguments @('--packaged',$exe,'--exclusion')
  Invoke-Native -Script scripts/native-capture.mjs -Arguments @('--packaged',$exe,'--hotkey')
  Invoke-Native -Script scripts/native-capture.mjs -Arguments @('--packaged',$exe,'--floating')
  Invoke-Native -Script scripts/native-hotkey-conflict.mjs -Arguments @($exe)
  Invoke-Native -Script scripts/native-region.mjs -Arguments @('--packaged',$exe)
  Invoke-Native -Script scripts/native-review-performance.mjs -Arguments @('--packaged',$exe,'--root',$captureRoot)
  Invoke-Native -Script scripts/native-review-endurance.mjs -Arguments @('--packaged',$exe,'--root',$captureRoot)
  Invoke-Native -Script scripts/native-open-cancel.mjs -Arguments @('--packaged',$exe,'--root',$captureRoot)
  Invoke-Native -Script scripts/native-interrupted-capture.mjs -Arguments @('--packaged',$exe)
  Invoke-Native -Script scripts/native-recovery-conflict.mjs -Arguments @('--packaged',$exe)
  Invoke-Native -Script scripts/native-recovery-conflict.mjs -Arguments @('--packaged',$exe,'--missing')
  Invoke-Native -Script scripts/native-export.mjs -Arguments @('--packaged',$exe)
  Invoke-Native -Script scripts/native-export-cancel.mjs -Arguments @('--packaged',$exe,'--root',$captureRoot)
  Invoke-Native -Script scripts/native-preferences.mjs -Arguments @('--packaged',$exe)
  Invoke-Native -Script scripts/native-reconnect.mjs -Arguments @('--packaged',$exe)
  Invoke-Native -Script scripts/native-close.mjs -Arguments @('--packaged',$exe)
  Invoke-Native -Script scripts/native-forced-close.mjs -Arguments @('--packaged',$exe)
} finally { Pop-Location }
$captures | Select-Object effective_samples_per_second,stop_to_review_ms,pool_bytes,gpu_local_peak_bytes,root
```

Check every capture is at least 7.6 effective samples/s; the five-run p95
Stop-to-Review time is at most 1000 ms. The review report under `$captureRoot`
must show uncached seek p95 at most 250 ms, cached seek p95 at most 100 ms,
and a bounded private-memory plateau after warmup. Inspect each capture's
`pool_bytes` against the 128 MiB CPU capture-buffer budget, and keep the
decoded-image and GPU measurements separate. Check the ten screenshots
under `.tmp/electron/layout-*.png` against `DESIGN.md`, including menus and
focus, at 1280 × 800 and 1024 × 700. Confirm the exclusion test observed the
window covering its marker while the captured PNG retained the marker beneath.

For local-model checks, use only verified model assets already staged under
`.tmp/`. Set `PHRASEBACK_MODEL_FIXTURE` to that isolated model directory if the
historical fixture path is unavailable. Do not point it at the normal library.
Then run the packaged model checks, keeping the same `$exe` and `Invoke-Native`
function from the native block:

```powershell
Push-Location app/electron
try {
  $modelCheck = node scripts/native-model-verify.mjs --packaged $exe | ConvertFrom-Json
  if ($LASTEXITCODE -ne 0) { throw 'Packaged model verification failed' }
  $modelRoot = $modelCheck.root
  Invoke-Native -Script scripts/native-model-controls.mjs -Arguments @('--packaged',$exe)
  Invoke-Native -Script scripts/native-generate.mjs -Arguments @('--packaged',$exe,'--root',$modelRoot)
  Invoke-Native -Script scripts/native-generation-cancel.mjs -Arguments @('--packaged',$exe)
  Invoke-Native -Script scripts/native-model-cancel.mjs -Arguments @('--packaged',$exe)
  Invoke-Native -Script scripts/native-model-lifecycle.mjs -Arguments @('--packaged',$exe,'--root',$modelRoot)
} finally { Pop-Location }
```

The fixture-based mock-server Rust tests cover fresh download failures; this
does not claim a fresh multi-gigabyte native download.

## 4. Installer replacement and rollback

Use a clean test installation identity. The test refuses to overwrite an
existing installed Phraseback. Supply a different, previously verified
candidate from `.tmp/electron/` so replacement, rollback, and forward restore
are all exercised:

```powershell
$previous = 'C:\Users\Matt\Desktop\Code\Desktop\phraseback\.tmp\electron\package-28abab7fc32b4a249a66ef9abfcf8a80'
./scripts/rebuild_installer_check.ps1 -Candidate $candidate -PreviousCandidate $previous
```

The installer check preserves hashes of synthetic recordings and model fixture
bytes across install, replace, rollback, reinstall, and uninstall. Both packages
use app version 0.2.0, so this proves different-build replacement, not a
cross-version project-data migration.

## 5. Release gate

Record an acceptance file binding the exact `package-summary.json` hash to
the passed Rust/Electron suite, packaged native checks, installer rollback,
visual review, and final notices review. The migration cutover used the
`migration_cutover` phase and additionally required an Avalonia regression.
Future releases use `electron_update`. Publish the accepted bytes with
`./build.ps1 -Task Release -Candidate $candidate -AcceptanceFile .tmp/electron/release-acceptance.json`.
The command retains the prior release under `reference-backups/` and is
idempotent for a candidate already published with that acceptance hash.

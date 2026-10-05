# Phraseback

Windows-first, local-only screen recording, screenshot review, optional local AI
descriptions, and GIF/PNG/Markdown export. The Rust engine owns capture,
persistence, inference, and export. Electron/React/TypeScript owns the desktop UI.

## Run

Run `./run.ps1` to open the verified Electron portable app at
`dist/Phraseback/Phraseback.exe`. The per-user installer is
`dist/Phraseback-Setup-0.2.0-win-x64.exe`.

New libraries use `%LOCALAPPDATA%/Phraseback`. Existing
`%LOCALAPPDATA%/FlowRecorder` libraries remain in place and are opened
automatically, preserving recordings and models. `PHRASEBACK_DATA` overrides the
location; `FLOW_RECORDER_DATA` remains supported for existing launch setups.
Explicit `--data-root` takes precedence. Version-1 recordings and original PNGs
are unchanged. Do not run two writers against the same data root.

## Develop and verify

The active build needs pinned Rust 1.98.1, Node dependencies, MSVC x64, and the
Windows SDK. Installer builds need the pinned Inno Setup compiler. See
`app/tooling/README.md`.

```powershell
./build.ps1 -Task Check
./build.ps1 -Task Test
./build.ps1 -Task Build
./build.ps1 -Task Prototype
```

`Prototype` opens the Electron app on a verified fixture under `.tmp/`. Native
and packaged checks use isolated data; never test against the normal library.
See `app/electron/VALIDATION-RUNBOOK.md`.

To prepare a release, run `./scripts/electron_candidate.ps1`, validate the exact
candidate, and record acceptance. Then publish it with:

```powershell
$candidate = (Get-Content .tmp/electron/latest-candidate.json -Raw | ConvertFrom-Json).candidate
./build.ps1 -Task Release -Candidate $candidate -AcceptanceFile .tmp/electron/release-acceptance.json
```

Release verifies the payload and installer hashes. The previous portable app,
installer, and metadata are retained under `reference-backups/previous-release-*`.
The accepted Electron migration is recorded in
`app/electron/RELEASE-ACCEPTANCE.md`.

## Source layout

- `app/crates/`: Rust domain, engine, and Windows capture adapter.
- `app/electron/`: active desktop UI, engine client, and native checks.
- `app/contracts/`, `app/fixtures/`: shared schemas and test evidence.
- `app/packaging/`: installer, pins, notices, and FFmpeg source materials.
- `app/shell/`: retained Avalonia rollback source; not part of active builds.
- `scripts/`: build and release validation scripts.

Retired Python source remains in the verified archive at
`../../Archives/phraseback-retired-20260927/reference-backups/`.

## Scope

Windows 11 x64, SDR display/region capture, target 8 FPS. 4K and HDR are
outside the agreed scope. The release is unsigned; public signing and an
automatic updater are not included. See `app/SCOPE.md` for acceptance limits.

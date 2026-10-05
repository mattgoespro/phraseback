# Electron migration status

The migration is complete as of 2026-10-04. Electron is the only active UI
for the portable app, installer, launcher, playground shortcut, and default
build tasks. Rust remains the authoritative engine; version-1 projects and
original PNGs were not migrated or rewritten.

The exact candidate, verification results, known limits, and Avalonia rollback
location are in `RELEASE-ACCEPTANCE.md`. `FEATURE-PARITY.md` maps the migrated
flows. `RELEASE-CUTOVER-PLAN.md` retains the phase gates and
`VALIDATION-RUNBOOK.md` describes repeatable Electron checks.

The C# Avalonia source and historical build scripts remain available for
rollback. They are not called by `build.ps1`, `run.ps1`, or the installer
candidate pipeline. All migration tests used isolated data under `.tmp/`.

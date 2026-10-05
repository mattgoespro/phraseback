# Development tools

Normal Rust/Avalonia development needs the pinned Rust and .NET toolchains plus
PowerShell, not Python. Run these commands from the repository root:

```powershell
./scripts/rebuild.ps1 -Task Build
./scripts/rebuild.ps1 -Task Check
./scripts/rebuild.ps1 -Task Test
./scripts/rebuild.ps1 -Task Prototype
```

`Prototype` installs the frozen synthetic fixture without replacing an existing
recording, then launches the development shell. All disposable data remains under
`.tmp`. It does not access normal user recordings or installed model assets.

`Phraseback.Tools` is a .NET console application with no external package dependencies:

```powershell
dotnet run --project app/tools/Phraseback.Tools
dotnet run --project app/tools/Phraseback.Tools -- --write
dotnet run --project app/tools/Phraseback.Tools -- --fixture-root .tmp/rebuild/prototype-data
```

The default command verifies shared schemas and generated Rust/C# contracts.
`--write` regenerates contracts only after schema/golden validation succeeds.
`--fixture-root` installs hash-verified synthetic evidence in a dedicated `.tmp`
subdirectory; it refuses unmarked nonempty directories and never overwrites a project.

While the migration is in progress, run this **additional** legacy gate after
compatibility or cross-module changes:

```powershell
./scripts/rebuild.ps1 -Task Reference
```

Only this transition suite needs the old Python environment. It runs the original
application tests and real old/new interoperability checks with the compiled engine.
It has not been waived or replaced by the new .NET fixture tests.

## Candidate packaging and CI

`./scripts/rebuild_package.ps1` builds the self-contained Windows portable folder
and per-user installer beneath `.tmp/rebuild/package-*`. It does not promote them
to `dist/Phraseback`. Inno Setup 6.7.3 must be installed at
`.tmp/tooling/inno-6.7.3`; the compiler hash is checked against
`app/packaging/inno.lock.json` before execution. The official download and its
installer hash are recorded in that manifest as well.

`build-provenance.json` inventories payload files; `package-summary.json` records
the installer hash, compiler identity and CI source revision when available.
Candidate and redistribution flags remain explicit. Missing source/notice or
native/clean-machine acceptance is not overridden by successful compilation.

The CI workflow has portable Windows/Linux/macOS checks and a dependent Windows
packaging job. Only provenance and notice inventories are uploaded by that job
while redistribution gates remain open, not executable artifacts. The pinned
Inno installer is installed only on the disposable Windows runner. This workflow
is authored but cannot be claimed as run until an authorized remote runner exists.

## Synthetic recording endurance

`./scripts/rebuild_endurance_check.ps1` runs an opt-in release-mode Rust test with
4,800 synthetic 2560 × 1600 frames through the real encoder/journal pipeline. It
opens no capture session or window and leaves the desktop usable. It verifies
frame/journal counts, timestamps, finalization and reopening, and samples the test
process's private memory and working set every 250 ms. Evidence is retained under
its unique `.tmp/rebuild/endurance-*` directory.

The post-warmup regression threshold is at most 32 MiB growth between middle-third
and final-third private-memory p95. Raw samples and machine metadata are retained;
this threshold does not replace the separate 128 MiB capture/review budgets.
The five-minute worker deadline and 330-second owned-process fallback apply only
to this non-interactive synthetic test. This does not establish native cadence,
GPU allocation accounting, high-entropy performance, or review-cache stability.

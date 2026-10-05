# Application source

The active app is the Rust engine in `crates/` and the Electron UI in
`electron/`. The C# Avalonia shell in `shell/` is retained for rollback only.
Start with the repository-root README for build, launch, and release commands.

Shared protocol schemas live in `contracts/`. Electron contract types and
runtime validators are generated and checked by
`electron/scripts/generate-contracts.mjs`; the Rust engine remains the sole
authoritative writer of version-1 projects.

For isolated native and packaged checks, follow
`electron/VALIDATION-RUNBOOK.md`. Its capture, model, and installer fixtures
stay under the repository `.tmp/` directory. The migration acceptance and
rollback location are recorded in `electron/RELEASE-ACCEPTANCE.md`.

`SCOPE.md` contains the current acceptance limits. Earlier entries in
`IMPLEMENTATION-STATUS.md` and `DELIVERY-PHASES.md` describe historical
states, not active build commands.

# Phraseback repository guidance

The working application is Rust in `packages/engine/` and
Electron/React/TypeScript in `packages/ui/`. Retired Avalonia and Python
implementations are not part of this repository.

- Engine owns authoritative writes. Preserve version-1 projects, original PNGs,
  journal ordering, Qt-compatible exclusive locking and manual descriptions.
- UI owns presentation and its Windows window/hotkey integration. Never block its
  dispatcher with capture, decode, hashing, inference or export work.
- Use `./build.ps1 -Task Check`, `Test`, `Build` and `Release`; `Prototype` uses isolated data.
- Keep temporary builds/test data under `.tmp/`. Canonical portable output is
  `dist/Phraseback`; launcher is `./run.ps1`. Never test against the real user library.
- Read `docs/SCOPE.md` for explicit scope/acceptance amendments. Do not reintroduce
  user-deferred environment, accessibility, display-interruption or offline checks as gates.
- Native disruptive test processes must finish within 30 seconds. Normal recordings are uncapped.
- Run focused regression tests, then the full Rust/Electron suite for cross-component changes.
  Keep native UI, inference and packaged evidence distinct from headless tests.
- Retain local-only processing; no audio/OCR/cloud/import/updater expansion without request.
- Preserve user changes and current release backups. Do not delete recordings or model assets.

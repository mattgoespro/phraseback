# Electron candidate notice review

Candidate inspected: `.tmp/electron/package-290c9f0fc2e54c6c849dec1d727d3f46`
(`package-summary.json` SHA-256 `cb8de1309dbcd2adb803025b188a01c777891d63399c9540762ad91c9e81bc8d`).
It contains the pinned MSVC release Rust engine. This is an engineering inventory
review, not a legal certification or permission to redistribute.

The payload includes Electron/Chromium license texts, copied texts for 169
installed npm packages, copied texts for 170 Windows-target Rust components,
and the pinned FFmpeg source kit. `app.asar` has 11 entries: compiled main,
preload, renderer HTML/CSS/JS and `package.json`; it has no `node_modules`.

Six entries in the conservative npm inventory lack a license file in their
installed package directory:

| Package | Dependency path | Runtime disposition |
| --- | --- | --- |
| `@electron-internal/extract-zip@1.0.5` | Electron download / Packager | Build-time extraction only |
| `@esbuild/win32-x64@0.25.12` | esbuild CLI | Build-time bundler binary |
| `@esbuild/win32-x64@0.28.2` | Vite → esbuild | Build-time bundler binary |
| `@rollup/rollup-win32-x64-gnu@4.64.0` | Rollup optional platform package | Not used in Windows runtime |
| `@rollup/rollup-win32-x64-msvc@4.64.0` | Vite → Rollup | Build-time bundler binary |
| `err-code@2.0.3` | Packager → notarize → promise-retry | Build-time packaging dependency |

The paths were inspected with `npm explain`; none of these package directories
is in `app.asar`, and their package markers are absent from the compiled
runtime bundles. Their missing local text does not represent a missing text
for shipped package source. The broad npm inventory retains the entries so
the classification is visible rather than silently dropping them.

The exact candidate inventory verifier passed. Packaging reran the npm and
Rust notice collectors; the pinned FFmpeg binary and source-kit hashes passed.
Electron/Chromium texts, the runtime JavaScript bundle, Rust texts, and the
separate FFmpeg source kit were checked in the payload. This closes the scoped
engineering notice review for this candidate. Its immutable build provenance
still records `redistribution_approved=false`; the separate release acceptance
record is the approval for the exact candidate hash above.

# Desktop UI

Follow root AGENTS.md. For UI changes, read the UI baseline in
`../../docs/DEVELOPMENT.md`. Keep privileged file access, the engine
process, Windows integration and media permissions in Electron main. The renderer
uses the typed preload API; it has no Node access and cannot write project files.

Shared schemas live in `../engine/contracts/`. Run `npm run generate:contracts`
after schema changes; `npm run check` verifies Rust and TypeScript outputs.
Use the repository-root build tasks and isolated-data native checks described in
`../../docs/DEVELOPMENT.md`.

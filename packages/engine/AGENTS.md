# Engine source

Follow root AGENTS.md. Rust lives here; the Electron UI is in `../ui/`.
Engine is the sole authoritative writer. IPC is four-byte little-endian length-prefixed
UTF-8 JSON, maximum 8 MiB. stdout is protocol-only; stderr is content-free diagnostics.
Shared schemas and golden examples drive Rust and TypeScript contracts; use
`../ui/scripts/generate-contracts.mjs`. Keep platform APIs in Electron main
and out of the renderer and portable Rust domain layer.
Use isolated `.tmp` data for development. Release builds support normal user data roots;
debug builds require marked development roots. Preserve that distinction.

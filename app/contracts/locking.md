# Windows data-root ownership

The engine acquires ownership after a compatible handshake and before project
loading, recovery, or authoritative writes. Failure returns `data_root_unavailable`.
The shell retains the existing actionable startup-error path.

Windows uses the same `application.lock` pathname as the Python application:

- Atomic create-new, read/write access, read sharing only; the handle stays open.
- UTF-8 lines contain PID, executable stem, hostname, then empty optional machine
  and boot identifiers. Qt accepts these optional fields being empty.
- A stale local PID or executable mismatch permits reclamation. Unknown process
  access is conservative. Otherwise the reference's 30-second absolute timestamp
  age applies, including malformed or future-dated metadata.
- Reclamation opens the exact existing file with read/delete access, checks it,
  and marks that handle for deletion. A live Qt/Rust owner's sharing mode prevents
  this open, regardless of misleading timestamps or metadata. Reparse points are
  rejected rather than followed. There is no check-then-delete-by-path race.
- Normal release closes the owner handle and removes only matching metadata,
  retrying briefly if readers block deletion. A crash leaves a recoverable file.

These semantics were checked against the primary
[Qt 6.11.2 Windows implementation](https://github.com/qt/qtbase/blob/v6.11.2/src/corelib/io/qlockfile_win.cpp)
and [metadata/stale-lock implementation](https://github.com/qt/qtbase/blob/v6.11.2/src/corelib/io/qlockfile.cpp).
The Rust implementation does not copy Qt code. It uses Windows file APIs through
`windows-sys` and Rust-owned handles. Unlike Qt, it does not compare machine/boot
IDs when reclaiming another application's lock: hostname/PID or age is required.
That can conservatively delay reclamation after a hostname change. This is a
Windows/local-filesystem contract, not a claim of network or Unix lock compatibility.

The additional `.rust-development.lock` remains held for compatibility with earlier
prototype builds. Removing that protection while old binaries can run would allow
two engine versions to overlap.

## Verified matrix

`test/test_rebuild_locking.py` runs against actual Qt 6.11.2 child processes:

- Qt-first and Rust-first launch, readable Rust owner metadata and normal release.
- Forced stale removal attempts cannot steal a live Rust owner.
- Qt and Rust crashes, each recovered by Qt and Rust successors.
- Fresh malformed metadata is preserved; old/future-dated metadata is reclaimable.
- Local versus foreign-host dead-owner metadata.
- Eight simultaneous startup races, each with exactly one owner.
- Unicode/apostrophe data-root paths and a lock symlink that must not delete its target.

All fixtures are disposable. Real application data is still gated independently
of these tests. The CI Windows job now includes this matrix; remote execution is
not claimed until the workflow actually runs.

# Windows build toolchain

Installation verified on 2026-09-21 using Microsoft's unattended installer.
Visual Studio Installer reports the instance complete, launchable and not requiring
a reboot. The C++ workload, x64/x86 compiler tools and Windows SDK component are present.

## Installed versions and provenance

- Visual Studio Build Tools 2026: **18.10.1**, installation version **18.10.12210.168**.
- Installation directory: `C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools`.
- MSVC toolset directory: **14.51.36231**; x64 `cl.exe` file version **19.51.36257.0**.
- Windows SDK directory: **10.0.26100.0**; x64 `rc.exe` file version **10.0.26100.8249**.
- Rust MSVC toolchain: **1.98.1-x86_64-pc-windows-msvc**, including rustfmt and Clippy.
- Existing .NET SDK **10.0.401** and Avalonia **12.1.2** remain installed for
  rollback source; the active Electron build does not require them.

The installer is retained at `.tmp/tooling/vs_BuildTools.exe` (5,695,128 bytes).
Its Authenticode signature was **Valid**, signed by **Microsoft Corporation**, before execution.
Its SHA-256 is `160f5e9c319e3408867cae9de83f5d8803bdf7c34fcc8463e8fc28286e49d99e`.

The fixed installer came from Microsoft's [release history](https://learn.microsoft.com/en-us/visualstudio/releases/2026/release-history):
[18.10.1 Build Tools bootstrapper](https://download.visualstudio.microsoft.com/download/pr/7437128c-6580-48ab-9c69-f7452be2ee7f/160f5e9c319e3408867cae9de83f5d8803bdf7c34fcc8463e8fc28286e49d99e/vs_BuildTools.exe).
This is an online bootstrapper, not a complete offline layout; it downloads the selected components.

## Requested components

`windows-build-tools.vsconfig` records the required workload and component IDs:

- `Microsoft.VisualStudio.Workload.VCTools` — Desktop development with C++.
- `Microsoft.VisualStudio.Component.VC.Tools.x86.x64` — MSVC x64/x86 tools.
- `Microsoft.VisualStudio.Component.Windows11SDK.26100` — Windows 11 SDK.

The successful bootstrapper invocation used these three `--add` entries and
`--includeRecommended --quiet --wait --norestart`. No security policies were changed,
no restart was performed, and no unrelated workload was requested. An interactive
installation can import the same configuration file if unattended setup is unavailable.
See Microsoft's [installer parameters](https://learn.microsoft.com/en-us/visualstudio/install/use-command-line-parameters-to-install-visual-studio?view=visualstudio).

## Build selection

`build.ps1` uses `scripts/electron_engine.ps1` with the pinned MSVC Rust
toolchain, then checks or builds the Electron UI. The release candidate path
also verifies the pinned FFmpeg and Inno inputs. `scripts/rebuild.ps1` retains
the old Avalonia pipeline for rollback only; it is not called by `build.ps1`.

This installation does not itself prove release acceptance. The source tests, optimized
package, native capture and clean-machine checks remain separately reported.

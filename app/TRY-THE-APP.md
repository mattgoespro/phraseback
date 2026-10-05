# Try the Windows rebuild

Double-click **Play Phraseback.cmd** in the repository root. It launches the latest
self-contained, optimized MSVC preview recorded in `.tmp/rebuild/playground-payload.json`.
The app does not need an installed Python, .NET SDK, Rust toolchain or FFmpeg to run.

For the completed local replacement, use `./run.ps1` or the executable in
`dist/Phraseback`. The Python implementation has been retired with a verified
rollback archive. The playground remains available for isolated experiments.

## First run

1. Select **New recording**, choose a display or region, and start the countdown.
2. Stop with the floating controls or **Ctrl+Shift+F9**.
3. Review selected moments, choose alternatives, and edit descriptions. Changes save automatically.
4. For local AI descriptions, open **Settings** and install a preset. This isolated
   playground starts without your existing model installations; downloads require networking.
   Model inference remains local after installation. GPU failure does not silently select CPU.
5. Review generated text before exporting the GIF, selected PNGs and Markdown.

## Where your test data goes

Recordings, preferences and any downloaded models stay in `.tmp/rebuild/playground-data`.
The launcher does not reset this folder. Builds and the synthetic fixture generator do not
reset it either. **Back up any recordings you want to keep before deleting `.tmp`.**
This playground launcher deliberately does not open `%LOCALAPPDATA%/Phraseback`
or an existing `%LOCALAPPDATA%/FlowRecorder` library.
Release executables support that normal location (or `PHRASEBACK_DATA`, with
`FLOW_RECORDER_DATA` retained for compatibility) when
launched directly, with exclusive ownership and one-time legacy metadata backups.
For candidate testing, continue using **Play Phraseback.cmd** so your existing
library stays untouched. Debug executables still require an isolated marked root;
do not put a development marker in the normal data folder.

## Current limitations

- Windows 11 x64, SDR capture only. Turn HDR off for the chosen display in Windows
  Settings before recording. HDR capture and tone mapping are outside release scope.
- Short repeated native-display performance passes. Clean-Windows
  and Linux/macOS execution are not release gates under the September 25 amendment.
- The unsigned installer and canonical portable build are ready for local use.
  Scoped notices/source checks and final cutover verification are complete.
- No automatic update, audio, OCR, video import or cloud analysis is included.

## Repeatable checks

Use `scripts/rebuild_crash_check.ps1 -Payload <payload>` for isolated shell-crash/job cleanup.
Use `scripts/rebuild_native_check.ps1 -PackagedPayload <payload>` for the short synthetic check.
Only when the display is free, add `-Seconds 20 -FullDisplay` for a short full-display
changing-color run, leaving room for countdown and finalization within the user's 30-second
disruption budget. The harness rejects recording durations over 30 seconds; do not use
the maximum for fullscreen runs because setup and stopping take additional time.
This covers the display with an app-owned test surface, closed before analysis. Capture counters,
machine information, bounded timing/memory samples and an app render are kept beneath `.tmp/rebuild`.
Repeated short runs have passed.

See [IMPLEMENTATION-STATUS.md](IMPLEMENTATION-STATUS.md) for current evidence and release gates.

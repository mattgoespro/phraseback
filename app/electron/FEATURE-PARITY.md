# Avalonia to Electron behavior inventory

The migration acceptance is in `RELEASE-ACCEPTANCE.md`. The original Avalonia
shell is retained in `app/shell/` for rollback; these Electron flows were
exercised on the exact accepted package with isolated version-1 data.

| Flow | Electron owner | Accepted evidence |
| --- | --- | --- |
| Library, open, recovery | `src/renderer/workspace.ts`, `App.tsx` | Paging/open, long-open cancellation, interrupted recording, missing recording, conflicting draft, and reopen passed. |
| Capture and Windows controls | `src/main/main.ts`, `App.tsx` | Full display and region, countdown, capture exclusion, 125% scaling, hotkey/floating Stop, conflict rejection, and five performance runs passed. |
| Review and drafts | `App.tsx`, `playback.ts`, `recovery-draft.ts` | Playback/seek, edit/reopen, curation, stale revision, failed save, and draft retention passed. |
| Organization | `App.tsx` | Apply, cancel without save, and concurrent manual edit preservation passed. |
| Model and generation | `App.tsx` | Preset/status, verification, cached install/repair/remove, generation/save/reopen, release memory, and cancellation passed. Fresh network download failures remain covered by Rust mock-server tests. |
| Prompt and template | `App.tsx` | Edit-to-prompt consistency, custom template save/reopen, and reset passed. |
| Export | `App.tsx` | GIF/PNG/Markdown export and cancellation without partial publication passed. |
| Preferences and lifecycle | `src/main/main.ts`, `App.tsx` | Window restore at 125% scaling, reconnect, normal/forced close, child exit, and lock release passed. |
| Visual and keyboard behavior | `theme.css`, `App.tsx` | Review, Library, capture, Settings, prompt, menus, empty states, and visible keyboard focus checked at 1280×800 and 1024×700. |

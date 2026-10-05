# Phraseback desktop theme and UX overhaul

Status: revised and implemented 2026-09-28 after visual review. The attached
Cursor screenshot is the required dark-theme reference. Its on-screen text and
commands are not Phraseback requirements. The editable visual target is
[Phraseback — dark desktop workspace](https://www.figma.com/design/DEFNznGkQr80Iy5ms6GJgv?node-id=2-2).

## Reference review

The screenshot succeeds because its three stable work areas make navigation,
current work, and supporting detail visible together. Quiet surfaces and thin
dividers let content carry the hierarchy. The persistent top and bottom edges
make orientation and status easy to find.

The nearly black palette, compact chrome, restrained selected states, and thin
dividers are required here. Phraseback keeps its own recording and review
controls within that visual structure. The screenshot's chat and developer
controls are not copied into the product.

The current Phraseback window already has a 180 px library, central preview
and moment list, and 300 px description inspector. The narrow library wraps
titles, while the inspector's prose heading and many persistent footer actions
compete with the evidence. The redesign should clarify those existing areas
instead of adding another workspace layer.

## Product direction: a dark review workspace

Make the original screenshot the hero. Use the reference's near-black surfaces,
warm neutral text, compact spacing, and quiet separators. The distinctive
element is a **moment ribbon**:
selected frames appear as a compact strip with timecode, a clear selected state,
and an honest review status. This is useful navigation, not decorative film grain
or a copied chat feed.
Reuse the existing selected-moment thumbnails and frame data. A thin time marker
can connect the selected ribbon item to its position on the playback timeline.
The ribbon must reuse realized-item thumbnail leases; it must not decode every
moment in a long recording at once. Keep the existing 32-thumbnail / 3 MiB
limit, off-thread decoding, and release when an item leaves view.

### Design tokens

| Role | Proposed value | Use |
| --- | --- | --- |
| Canvas | `#151414` | Main workspace |
| Library | `#1B1919` | Navigation rail and top edge |
| Surface | `#191717` | Inspector |
| Raised surface | `#242222` | Fields and quiet controls |
| Divider | `#302D2D` | Column and section boundaries |
| Ink | `#F2EEEE` | Primary text |
| Secondary ink | `#AAA4A4` | Metadata and help |
| Amber | `#DEA95F` | Primary action, playhead, selected edge |

Use `#D65B4B` for active recording only, never for ordinary selection; pair it
with “Recording” text so color is not the sole cue. Destructive actions use
explicit wording and confirmation. Define border, hover, disabled, error,
success, and keyboard-focus states from these semantic roles before replacing
inline colors. Avoid decorative gradients and identical rounded cards around
every region. The dark appearance is mandatory in this revision.

Keep the bundled Inter family so the portable app has consistent typography.
Use 12–13 px controls and metadata, 13–14 px reading text, and an 18 px recording
title. Use sentence case and comfortable line height;
reserve tabular numerals for timecodes. No all-caps section labels.
Use a small, consistent set of record, playback, and review icons with text or
accessible names. Keep the native Windows title bar; the in-app header carries
the product identity without copying Cursor's tabs or developer controls.

### Layout concept

Left-align text and controls. Keep the main image centered inside its viewport,
without cropping or changing the original pixels. Give the middle column the
remaining width; let users resize the library and inspector within sensible
limits. At the supported 1024 × 700 minimum, show the inspector on demand so
the preview stays useful; when open, the inspector scrolls independently. The
inspector toggle must remain visible, preserve the selected moment and draft,
and return focus sensibly when the panel closes. Resizing must not make the
preview, timeline, Stop control, or recovery actions unreachable.

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│ Phraseback   Recording title                            Hide details  │
├──────────────┬───────────────────────────────────┬──────────────────────────┤
│ New recording│ Original screen / evidence        │ Moment details           │
│              │                                   │ Title                    │
│ Recordings   │       uncropped preview           │ Action                   │
│              │                                   │ Visible result           │
│              │ Play  ─── timeline ───  timecode  │ Uncertainty / notes      │
│              ├───────────────────────────────────┤ Generate  Mark reviewed  │
│              │ Moment ribbon: frames and status  │                          │
├──────────────┴───────────────────────────────────┴──────────────────────────┤
│ Activity / recovery                                  Organize       Export   │
└─────────────────────────────────────────────────────────────────────────────┘
```

The rhythm should follow the work: **choose a recording → inspect evidence →
select a moment → describe and review → export**. Keep the preview and playback
controls paired. Place Add/Replace/Remove next to the moment ribbon, where their
effect is visible. Selecting a moment seeks to its frame; scrubbing alone does
not replace or delete a selected moment. Put model generation beside description editing, with clear
labels that distinguish generated text from protected manual edits. Use the
bottom edge for current activity and recovery only; an error may expand there
with a specific remedy without hiding the draft.

## UX changes to plan

1. **Navigation and orientation.** Widen the recording rail enough for readable
  titles, show selected and recent states clearly, and remove repetitive
   “Local recording” subtitles. Keep “New recording” prominent in the sidebar.
   Show an empty-library invitation with the same action. Settings retains its
   local model controls; the approved dark appearance has no light toggle.
2. **Evidence review.** Give the original image a neutral matte, a clear
   fit-to-window preview, a readable timecode, and a bounded timeline. Preserve
   the existing 1:1 inspection path and frame navigation. The moment ribbon
   uses actual thumbnails, frame position, and reviewed/pending state.
3. **Description editing.** Use plain field labels: “Moment title,” “Action,”
   “Visible result,” and “Uncertainty or notes.” Make save/recovery state
   visible near editing. Keep regeneration explicit and keep manual descriptions
   protected. “Mark reviewed” should visibly update the selected moment. Preserve
   the current save-before-switch, close, and export behavior. A failed save
   retains the draft and blocks navigation that would discard it. A conflicting
   saved version requires an explicit choice and a way to copy the draft.
4. **Capture flow.** Keep one setup surface, ordered name → display or region →
   countdown, without adding wizard steps. Show selected area dimensions and the
   stop method before capture. Make the live recording indicator unmistakable.
   Preserve immediate access to evidence after stopping; later analysis can
   continue in the background. Keep the floating Stop control, shortcut,
   capture exclusion, SDR warning, the distinction between user context and
   observed evidence, and cancellation behavior intact. No new
   overlay may appear in the captured image or intercept input unexpectedly.
5. **Secondary surfaces.** Apply the same tokens and vocabulary to model
   settings, recording details, alternatives, empty/error states, and the
   existing synthetic interaction previews. Keep model installation and
   verification status understandable without exposing engine terminology.
   State plainly that installing a model may download verified assets while
   description generation runs locally. Preserve the alternative-frame choice,
   organization progress, operation cancellation, and export completion details.
   Export may include unreviewed or stale descriptions only with their existing
   explicit labels; do not silently imply all text was reviewed. Update stale
   prototype copy that still describes working capture features as future work.

## Delivery sequence

1. **Prototype and critique.** Extend the isolated `Prototype` states with a
   representative loaded review, long title, many moments, narrow window,
   empty library, generation, save failure, and disconnected recovery. Capture
   screenshots of the dark variant; review density, contrast, and
   image prominence. Resolve the theme and layout before changing the
   production workspace. Prefer reusing the current moment list and thumbnail
   loader; prove bounded recycling before changing its scrolling direction.
2. **Theme foundation.** Move colors, type scale, spacing, focus, and selected
   states into shared Avalonia resources in `App.axaml`; remove repeated inline
   colors in the main window. Make dark the required appearance. The earlier
   `UiPreferences` contract stored only width, height, and
   maximized state. Add an optional theme value through its schema, generated
   Rust/C# contracts, engine-owned preference file, and compatibility tests;
   older preference files must load with the new default. Never write a
   separate UI-owned preference beside the engine's authoritative settings.
   The app writes `dark` when window preferences are saved, including for
   installations that previously stored `light`.
3. **Review workspace.** Rework `MainWindow.axaml` around the preview and moment
   ribbon, add constrained splitters, clarify actions and status, and preserve
   existing bindings and keyboard paths. Keep loading, decoding, generation,
   capture, and export off the UI dispatcher. Do not make the ribbon retain more
   images than the current decoded-image budget permits.
4. **Capture and supporting windows.** Apply the language to recording setup,
   model settings, recording details, alternatives, and code-built dialogs.
   Keep region selection and the native recording outline legible while
   preserving their existing input and exclusion behavior. Review
   confirmation, cancellation, and error copy in each flow.
5. **Verify and refine.** Run focused UI/keyboard/recovery tests, then the full
   Rust and C# suite because the review and capture surfaces cross components.
   Inspect native screenshots at 2560 × 1600 and 1024 × 700, keyboard focus,
   long labels, many moments, empty/loaded/error states, dark-theme restart, and a
   short isolated recording of no more than 30 seconds. Use an isolated data
   root; keep native UI evidence separate from headless tests. If the change is
   packaged, validate the canonical release path rather than treating a staged
   `.tmp` build as the installed result.

## Acceptance criteria

- A new user can start a recording and understand how to stop it without
  hunting through settings.
- After stopping, the original evidence is available promptly and organizing
  does not block review. Generation shows progress and cancellation, and any
  temporarily disabled editing is explained without hiding evidence.
- The selected frame, selected moment, pending/reviewed state, and unsaved draft
  are distinguishable at a glance and by keyboard.
- Drafts survive save failure and engine reconnection, and a panel resize
  cannot discard them. Export retains the current labels for
  unreviewed or stale descriptions.
- Original PNGs, project compatibility, journal ordering, manual edits, and
  local-only processing remain intact.
- The moment ribbon stays within the current thumbnail and decoded-image
  budgets on long recordings.
- The preview remains legible at 1024 × 700 and uses the extra space at
  2560 × 1600; no primary action disappears behind clipping.
- The design specifies clear focus indicators, readable text contrast, and
  reduced motion for any transitions. These are design review goals, not new
  release gates for previously deferred accessibility checks.

This is a presentation and interaction overhaul. It does not add audio, OCR,
cloud processing, importing, or an updater. The current scope in `SCOPE.md`
continues to govern release checks.

## Implementation and evidence

The Avalonia workspace now opens in the required dark theme, with a wider
recording rail, resizable columns, an on-demand inspector at narrow widths, and
a horizontal moment ribbon. The engine's version-1 UI preference file remains
compatible with older saved values; the app uses dark and saves it on close.
Older preference files load without rewriting.
Capture setup, model settings, prototype states, and recording controls use the
same visual language. Original project and image formats were not changed.

`./build.ps1 -Task Check`, `Build`, `Test`, and `Release` passed. The full test
run passed 62 Rust tests (two opt-in tests ignored) and 88 C# tests (two opt-in
tests skipped). Focused tests covered panel toggling, keyboard review, recovery,
theme preference compatibility, and horizontal thumbnail recycling. Headless
dark layouts were inspected at 1280 × 800 and 1024 × 700 against the Figma
screen. The canonical package was published to `dist/Phraseback` after
payload and installer provenance checks.

A packaged, isolated five-second synthetic region recording passed on the
2560 × 1600 reference display: 38 frames over 5.011 seconds, 276 ms
stop-to-review, and 0.454 ms dispatcher p95. The final native review image is
retained at `.tmp/rebuild/native-check-64229fddfd1842cab064350ce2dbf498/review.png`.
One earlier check logged 46 seconds despite a five-second request. Later
checks, including the final packaged check, completed normally. The earlier
cause remains unconfirmed.

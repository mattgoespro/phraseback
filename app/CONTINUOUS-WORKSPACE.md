# Continuous workspace and prompt formatting

Phraseback starts on Ready to record. Use the sidebar to start a recording, choose
an existing recording, or open Settings. Stopping opens review; descriptions still
require an explicit Generate action. Get prompt expands the prompt beside the same
timeline. Continue reviewing restores the selected moment and description editor.
During recording, the evidence surface shows the latest saved frame, refreshed at
most twice per second. This preview does not change capture sampling or originals.

The Task field is the user's requested outcome. Context remains user-provided
background, separate from observed evidence. A blank task is valid and the default
prompt omits its section. Task edits do not invalidate generated descriptions.

## Templates

Settings → Prompt format provides syntax highlighting, autocomplete, a token guide,
and a preview using the open recording. Type `{{` to show completions; Enter or Tab
inserts the selected completion. The Insert token controls also work without typing.
Moment and conditional blocks insert both markers, with the caret between them.

| Syntax | Meaning |
| --- | --- |
| `{{field}}` | Insert a field |
| `{{#moments}} … {{/moments}}` | Repeat over selected moments in timeline order |
| `{{#if field}} … {{/if}}` | Include content when the field is nonempty |

Document fields: `title`, `task`, `context`, `duration`, `recording_path`.

Moment fields: `number`, `title`, `time`, `action`, `result`, `uncertainty`,
`review_note`, `screenshot_path`. Inside moments, `title` is the moment title;
document-only fields remain available. Conditions can nest. Unknown fields,
unfinished/mismatched blocks, and nested moment loops remain literal. Interpolated
values are escaped as literal Markdown and never interpreted as another template.

Save commits the global format used by the workspace, Copy and `transcript.md`.
Reset changes the editor draft to the shipped default; Save commits that reset.
Unsaved drafts survive navigation within the current app session. Copy and export
continue using the saved format. The default retains missing/stale/unreviewed
notices; custom templates may omit them. Recording failure notes remain visible.

Local preview paths point to original evidence. Export paths point to the exported
GIF and screenshots, keeping the package portable. Copying Markdown does not attach
images to another application.

## Ownership and compatibility

The Rust engine owns metadata and template persistence. The optional `task` field
defaults to empty in version-1 projects. `edit_project` callers that omit task leave
it unchanged. Template settings live separately from window preferences in
`prompt-template-rust-v1.json`; missing/corrupt settings use the default without
rewriting the file on load. Save uses atomic replacement.

`prompt_template`, `save_prompt_template`, `reset_prompt_template` and `render_prompt`
are defined in the shared command schema. Rendering validates recording identity and
revision, and accepts an optional template override for the settings preview.
Templates are limited to 256 KiB and rendered output to 6 MiB. Rendering runs in the
engine; highlighting scans run off the Avalonia dispatcher. Late preview responses
are discarded.
Capture previews use coalesced operation progress references published only after
the PNG and journal entry are saved. A single background decoder bounds the preview
resolution and discards results from a stopped or superseded recording.

## Validation

Run `./build.ps1 -Task Check`, `Test`, and `Build`.
`./scripts/check_continuous_workspace.ps1` installs an isolated synthetic fixture,
checks native navigation and bounds, and saves screenshots at both supported window
sizes under `.tmp/rebuild/workflow-*/workflow-check`. Its process has a 30-second
deadline. This checks native UI rendering, not model inference or capture.
The existing native recording check covers capture separately.

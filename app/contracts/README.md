# Private protocol, version 1

Each message is a four-byte unsigned little-endian byte count, followed by UTF-8 JSON.
Reject lengths of zero or greater than 8 MiB before allocating the payload.
Stdout is reserved for these messages; diagnostics use stderr and exclude user content.

`protocol.schema.json` is the source of truth for the envelope shapes. Rust and C#
definitions carry generated-file headers and are checked mechanically against this
schema by `dotnet run --project app/tools/Phraseback.Tools`. Change the schema and both
generated representations together. Both runtimes also deserialize the same golden
request, success, and failure examples in their tests.

`commands.schema.json` defines request and response payloads for every command.
Its `x-methods` map selects the request/response definitions; `metadata_page` selects
the generated frame or step page using the request's `kind`. Operation status is
nullable only when no operation exists. Worker-specific `OperationStatus.result`
objects remain an explicit extension point; operation identity/state and the
surrounding command result are generated, not inferred from those optional details.

Run `dotnet run --project app/tools/Phraseback.Tools -- --write` to regenerate both languages,
then run the normal checks/tests. The default invocation verifies generated files,
valid golden examples against the schema subset, golden command coverage and the
engine dispatch surface. C# display helpers remain separate partial records.

Prompt formatting adds `prompt_template`, `save_prompt_template`,
`reset_prompt_template`, and `render_prompt`. The renderer validates recording ID and
revision and accepts an optional draft template. The optional project/task edit field
defaults to empty on old projects; omitting it from `edit_project` preserves its value.
Capture progress may include `capture_preview`, `capture_width`, and `capture_height`
in the worker-specific result extension. These references are coalesced, refer only to
saved frames, and are not authoritative save acknowledgements.

The engine validates command fields before dispatch and returns `invalid_params`
without changing project state for malformed requests. Invalid hello parameters are
rejected before acquiring the data lock. The shell validates outgoing commands and
incoming results using the same generated types. A malformed local command does not
poison the transport; an invalid remote response does. Required nullable fields must
still be present, and nested evidence arrays cannot contain null elements. Existing
v1 optional parameters and their defaults remain supported; extra unknown fields are
rejected. No project-file schema or authoritative-data migration is involved.

Commands remain sequential; responses and asynchronous notifications share the framed
stream. Request IDs identify replies; the session UUID changes each time the engine starts. The first request
must be `hello` with a compatible major version. No lock or writable project is
opened before a successful version check. A broken controlling pipe exits the engine.

The shell opts into notifications with `hello.params.notifications = true` (minor 1).
Legacy test clients can omit this and poll `operation_status`. `operation_status`
notifications coalesce progress; `operation_saved` notifications never coalesce durable
step acknowledgements. Both carry the session, operation identity and project revision.
The engine drains durable saves before terminal state. An independent shell reader
routes responses, bounds pending save acknowledgements and coalesces progress. A slow
or disconnected shell cannot cause unlimited capture or notification queues.

Implemented commands: `hello`, `capabilities`, `screens`, `list_projects`,
`open_project`, `metadata_page`, `frame`, `edit_step`, `review_step`, `add_step`, `remove_step`,
`replace_step`, `edit_project`, `organize`, `alternatives`, `prepare_capture`, `start_capture`,
`operation_status`, `cancel_operation`, `model_status`, `model_select`, `model_install`,
`model_verify`, `model_remove`, `model_release`, `generate`, `export`, `shutdown`.
Phase 2 adds `library_page`, `organize_background`, `organization_status`,
`cancel_organization` and `apply_organization`.
All edits require the currently opened
`recording_id` and `revision`. Success means the metadata replacement completed;
failed persistence leaves the in-memory revision unchanged. Source images are
referenced by validated local paths, never embedded in the protocol.

Capture/model/export work runs on a cancellable foreground worker. Optional
`frame.params.preview_width` requests a bounded derivative; if unavailable, the original
path is returned while a bounded background worker prepares the cache. A missing source
never resolves to an orphaned cached image. Original pixels are requested by omitting
that parameter. Source PNGs are never rewritten.

`organize_background` starts a single analysis worker without occupying the foreground
operation. It never writes project metadata. Its coalesced `organization_status`
notifications use an independent shell queue, so they cannot replace foreground
completion or persistence acknowledgements. Polling `organization_status` is available
to non-notification clients; `cancel_organization` addresses the analysis by ID.
Opening another recording or starting capture/generation/export cancels analysis.

`apply_organization` is a separate authoritative write requiring operation ID, recording
ID and the original project revision. Cancelled, edited or reopened projects reject
obsolete results. The shell additionally refuses automatic application while an editor
draft is pending or another interaction owns the workspace. This matters because an
unsaved shell draft is not yet reflected in the engine revision. The legacy foreground
`organize` command remains supported. A failed disposable-cache write does not prevent
analysis or review; source PNGs remain unchanged.

Frame/step metadata is paged in batches of up to 128. `library_page` returns at most 64
summary rows and a catalog ID/next offset. A single read-only worker streams metadata
summaries and counts steps without loading frame arrays, decoding PNGs or validating
every evidence path. The first response may have `indexing: true`; retry with its
catalog ID, without `refresh`, until the sorted snapshot is ready. Indexing does not
block the engine command loop. Rows sort by descending creation date and recording ID.
`refresh: true` rebuilds a completed catalog; obsolete cursors fail explicitly. Evidence
validation still occurs on open/access. The legacy `list_projects` command is unchanged.
Real application data remains disabled pending the remaining acceptance gates.

Protocol minor 2 adds `prepare_open`, an `opening` foreground operation that loads and validates the
project and, if interrupted, recovers its journal on a worker. Poll `operation_status`
or consume notifications, then call `open_project` after completion to publish the
prepared snapshot. This keeps each RPC bounded while recovery may take longer than
the shell's ten-second transport deadline. `cancel_operation` works during recovery;
checks occur between records and PNG rows and before the atomic metadata save.
Cancelled validation never publishes a truncated prefix. Once the atomic save has
committed, cancellation cannot undo the recovered metadata.

The shell uses preparation for every open. Direct legacy `open_project` calls still
open ready projects, but interrupted projects now return an actionable error rather
than blocking the protocol loop. Failed/cancelled preparation preserves the previously
opened snapshot and revision. The shell keeps its library connected when the initial
recording cannot open; unsaved reconnect drafts retain the existing conflict checks.

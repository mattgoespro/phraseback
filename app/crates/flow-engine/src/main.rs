use flow_core::{
    project::{Project, Result, Step},
    protocol::{self, Error, Response},
};
#[cfg(test)]
mod allocation_probe;
mod child;
mod data_lock;
mod export;
mod generation;
mod library;
mod model;
mod operation;
mod organize;
mod preferences;
mod preview;
mod recording;
use serde_json::{Value, json};
use std::{
    fs, io,
    path::{Path, PathBuf},
};

struct Engine {
    prepared_open: std::sync::Arc<std::sync::Mutex<Option<(String, Project)>>>,
    root: PathBuf,
    opened: Option<(String, Project)>,
    revision: u64,
    operation: Option<operation::Operation>,
    organization: Option<operation::Operation>,
    catalog: Option<library::Catalog>,
    operation_applied: bool,
    prepared_capture: Option<(
        String,
        flow_capture_windows::Screen,
        flow_capture_windows::Area,
        String,
        String,
    )>,
    models: model::Models,
    previews: preview::Previews,
}

impl Engine {
    fn directory(&self, id: &str) -> Result<PathBuf> {
        if id.is_empty() || id.contains(['/', '\\', ':']) || id.starts_with('.') {
            return Err("Invalid recording identifier".into());
        }
        let sessions = self.root.join("sessions").canonicalize()?;
        if !sessions.starts_with(&self.root) {
            return Err("Sessions directory is outside data root".into());
        }
        let directory = sessions.join(id).canonicalize()?;
        if !directory.starts_with(&sessions) {
            return Err("Recording is outside data root".into());
        }
        Ok(directory)
    }
    fn snapshot(&self, paged: bool) -> Value {
        self.opened.as_ref().map_or(
            Value::Null,
            |(id, project)| {
                let metadata = if paged {
                    json!({"version":project.version,"title":project.title,"context":project.context,"task":project.task,
                        "created":project.created,"frames":[],"steps":[],"duration_ms":project.duration_ms,
                        "width":project.width,"height":project.height,"error":project.error,"state":project.state})
                } else { json!(project) };
                json!({"recording_id":id,"revision":self.revision,"project":metadata,"frame_count":project.frames.len(),"step_count":project.steps.len()})
            },
        )
    }
    fn dispatch(&mut self, method: &str, params: &Value) -> Result<Value> {
        if let Some(operation) = &self.operation {
            let status = operation.status();
            if status.finished && !self.operation_applied {
                if status.kind != "opening" {
                    self.opened = None;
                    self.revision += 1;
                }
                self.operation_applied = true;
            }
            if !status.finished
                && ![
                    "operation_status",
                    "cancel_operation",
                    "shutdown",
                    "capabilities",
                    "frame",
                    "metadata_page",
                    "model_status",
                    "organization_status",
                    "cancel_organization",
                    "library_page",
                    "prompt_template",
                    "render_prompt",
                    "ui_preferences",
                    "save_ui_preferences",
                ]
                .contains(&method)
            {
                return Err(
                    "An operation is active; cancel or wait before editing or changing recordings"
                        .into(),
                );
            }
        }
        if [
            "prepare_open",
            "open_project",
            "prepare_capture",
            "generate",
            "export",
            "organize",
            "model_install",
            "model_verify",
            "model_remove",
            "shutdown",
        ]
        .contains(&method)
            && let Some(background) = &self.organization
        {
            background.cancel();
        }
        match method {
            "prepare_open" => {
                let id = text(params, "recording_id")?.to_owned();
                let directory = self.directory(&id)?;
                let prepared = self.prepared_open.clone();
                *prepared.lock().unwrap() = None;
                self.operation = Some(operation::Operation::start(
                    "opening",
                    Some(id.clone()),
                    self.revision,
                    move |work| {
                        work.check()?;
                        let mut project = Project::load(&directory)?;
                        let mut reported = None;
                        project.recover_checked(&directory, |count| {
                            work.check()?;
                            if reported != Some(count) {
                                work.update(
                                    "opening",
                                    0,
                                    &format!("Recovering recording · {count} frames checked…"),
                                );
                                reported = Some(count);
                            }
                            Ok(())
                        })?;
                        work.check()?;
                        *prepared.lock().unwrap() = Some((id, project));
                        Ok(json!({"ready":true}))
                    },
                ));
                self.operation_applied = false;
                Ok(json!(self.operation.as_ref().unwrap().status()))
            }
            "prompt_template" => Ok(serde_json::to_value(preferences::load_template(
                &self.root,
            )?)?),
            "save_prompt_template" => Ok(serde_json::to_value(preferences::save_template(
                &self.root,
                text(params, "template")?.into(),
            )?)?),
            "reset_prompt_template" => Ok(serde_json::to_value(preferences::save_template(
                &self.root,
                flow_core::prompt::DEFAULT_TEMPLATE.into(),
            )?)?),
            "render_prompt" => {
                let (id, project) = self.opened.as_ref().ok_or("No recording is open")?;
                if params["revision"].as_u64() != Some(self.revision)
                    || text(params, "recording_id")? != id
                {
                    return Err("Stale project revision".into());
                }
                let template = match params["template"].as_str() {
                    Some(value) => value.to_owned(),
                    None => preferences::load_template(&self.root)?.template,
                };
                let directory = self.directory(id)?;
                let markdown = flow_core::prompt::render(project, &directory, &template, false)?;
                Ok(json!({"recording_id":id,"revision":self.revision,"markdown":markdown}))
            }
            "ui_preferences" => Ok(serde_json::to_value(preferences::load(&self.root)?)?),
            "save_ui_preferences" => Ok(serde_json::to_value(preferences::save(
                &self.root,
                serde_json::from_value(params.clone())?,
            )?)?),
            "library_page" => library::page(&mut self.catalog, &self.root, params),
            "organize_background" => {
                if self
                    .organization
                    .as_ref()
                    .is_some_and(|op| !op.status().finished)
                {
                    return Err("Background organizing is still stopping".into());
                }
                let (id, project) = self.opened.as_ref().ok_or("No recording is open")?;
                if params["revision"].as_u64() != Some(self.revision)
                    || text(params, "recording_id")? != id
                {
                    return Err("Stale project revision".into());
                }
                let directory = self.directory(id)?;
                let project = project.clone();
                self.organization = Some(operation::Operation::start(
                    "organizing",
                    Some(id.clone()),
                    self.revision,
                    move |work| {
                        let suggestions = organize::analyze(&project, &directory, &work)?;
                        work.check()?;
                        Ok(json!({"suggestions":suggestions}))
                    },
                ));
                Ok(serde_json::to_value(
                    self.organization.as_ref().unwrap().status(),
                )?)
            }
            "organization_status" => Ok(self
                .organization
                .as_ref()
                .map(|op| json!(op.status()))
                .unwrap_or(Value::Null)),
            "cancel_organization" => {
                let background = self
                    .organization
                    .as_ref()
                    .ok_or("No background organizing is active")?;
                if text(params, "id")? != background.status().id {
                    return Err("Organization identity changed".into());
                }
                background.cancel();
                Ok(json!({"cancelling":true}))
            }
            "apply_organization" => {
                let background = self
                    .organization
                    .as_ref()
                    .ok_or("No background suggestions are available")?;
                let status = background.status();
                let (id, project) = self.opened.as_ref().ok_or("No recording is open")?;
                if text(params, "id")? != status.id
                    || text(params, "recording_id")? != id
                    || status.recording_id.as_deref() != Some(id)
                    || status.revision != self.revision
                    || params["revision"].as_u64() != Some(self.revision)
                    || background
                        .work
                        .cancel
                        .load(std::sync::atomic::Ordering::Relaxed)
                {
                    return Err("Stale organizing result; current choices are retained".into());
                }
                if !status.finished || status.state != "completed" {
                    return Err("Suggestions are not ready".into());
                }
                let suggestions = serde_json::from_value(
                    status.result.ok_or("Missing suggestions")?["suggestions"].clone(),
                )?;
                let mut candidate = project.clone();
                organize::apply(&mut candidate, &self.directory(id)?, suggestions)?;
                self.opened = Some((id.clone(), candidate));
                self.revision += 1;
                Ok(self.snapshot(params["paged"].as_bool().unwrap_or(false)))
            }
            "capabilities" => Ok(
                json!({"prototype":true,"shared_data":false,"capture":cfg!(windows),"generation":cfg!(windows),"export":true,"editing":true,
                    "source_enumeration":cfg!(windows),"system_selection":false,"region_cropping":cfg!(windows),"cursor_modes":["explicit"],"control_exclusion":cfg!(windows)}),
            ),
            "screens" => Ok(serde_json::to_value(flow_capture_windows::screens()?)?),
            "model_status" => Ok(self.models.inspect()),
            "model_remove" => {
                if params["confirmed"].as_bool() != Some(true) {
                    return Err("Confirm model removal first".into());
                }
                self.models.release();
                let models = self.models.clone();
                let preset = models.selected();
                self.operation = Some(operation::Operation::start(
                    "removing_model",
                    None,
                    self.revision,
                    move |work| model::remove(&models.root, &preset, &work),
                ));
                self.operation_applied = false;
                Ok(serde_json::to_value(
                    self.operation.as_ref().unwrap().status(),
                )?)
            }
            "model_select" => {
                self.models.select(text(params, "preset")?)?;
                Ok(self.models.inspect())
            }
            "model_release" => {
                self.models.release();
                Ok(self.models.inspect())
            }
            "model_install" | "model_verify" => {
                self.models.release();
                let models = self.models.clone();
                let preset = models.selected();
                let install = method == "model_install";
                self.operation = Some(operation::Operation::start(
                    if install { "installing" } else { "verifying" },
                    None,
                    self.revision,
                    move |work| {
                        if install {
                            model::install(&models.root, &preset, &work)
                        } else {
                            model::verify(&models.root, &preset, &work)?;
                            Ok(json!({"verified":true}))
                        }
                    },
                ));
                self.operation_applied = false;
                Ok(serde_json::to_value(
                    self.operation.as_ref().unwrap().status(),
                )?)
            }
            "generate" => {
                let (id, project) = self.opened.as_ref().ok_or("No recording is open")?;
                if params["revision"].as_u64() != Some(self.revision)
                    || text(params, "recording_id")? != id
                {
                    return Err("Stale project revision".into());
                }
                let directory = self.directory(id)?;
                let project = project.clone();
                let models = self.models.clone();
                let target = params["step_id"].as_str().map(str::to_owned);
                let force = params["force"].as_bool().unwrap_or(false);
                let replace_manual = params["replace_manual"].as_bool().unwrap_or(false);
                if replace_manual && !force {
                    return Err(
                        "Replacing manual descriptions requires explicit regeneration".into(),
                    );
                }
                self.operation = Some(operation::Operation::start(
                    "generating",
                    Some(id.clone()),
                    self.revision,
                    move |work| {
                        generation::generate(
                            models,
                            project,
                            &directory,
                            target,
                            force,
                            replace_manual,
                            work,
                        )
                    },
                ));
                self.operation_applied = false;
                Ok(serde_json::to_value(
                    self.operation.as_ref().unwrap().status(),
                )?)
            }
            "export" => {
                let (id, project) = self.opened.as_ref().ok_or("No recording is open")?;
                if params["revision"].as_u64() != Some(self.revision)
                    || text(params, "recording_id")? != id
                {
                    return Err("Stale project revision".into());
                }
                let directory = self.directory(id)?;
                let parent = PathBuf::from(text(params, "parent")?);
                let ffmpeg = export::bundled_encoder()?;
                let template = preferences::load_template(&self.root)?.template;
                let project = project.clone();
                self.operation = Some(operation::Operation::start(
                    "exporting",
                    Some(id.clone()),
                    self.revision,
                    move |work| {
                        export::run_export_template(
                            project, &directory, &parent, &ffmpeg, work, &template,
                        )
                    },
                ));
                self.operation_applied = false;
                Ok(serde_json::to_value(
                    self.operation.as_ref().unwrap().status(),
                )?)
            }
            "organize" => {
                let (id, project) = self.opened.as_ref().ok_or("No recording is open")?;
                if params["revision"].as_u64() != Some(self.revision)
                    || text(params, "recording_id")? != id
                {
                    return Err("Stale project revision".into());
                }
                let directory = self.directory(id)?;
                let project = project.clone();
                self.operation = Some(operation::Operation::start(
                    "organizing",
                    Some(id.clone()),
                    self.revision,
                    move |work| organize::run(project, &directory, work),
                ));
                self.operation_applied = false;
                Ok(serde_json::to_value(
                    self.operation.as_ref().unwrap().status(),
                )?)
            }
            "alternatives" => {
                let (id, project) = self.opened.as_ref().ok_or("No recording is open")?;
                if text(params, "recording_id")? != id {
                    return Err("Recording changed".into());
                }
                let path = self.directory(id)?.join("derived-rust-v1/suggestions.json");
                let data: Value = fs::read(path)
                    .ok()
                    .and_then(|b| serde_json::from_slice(&b).ok())
                    .unwrap_or(Value::Null);
                if data["frames"] == serde_json::to_value(&project.frames)? {
                    Ok(data["suggestions"].clone())
                } else {
                    Ok(json!({"selected":[],"intervals":[]}))
                }
            }
            "prepare_capture" => {
                self.models.release();
                let screen: flow_capture_windows::Screen =
                    serde_json::from_value(params["screen"].clone())?;
                let area: flow_capture_windows::Area =
                    serde_json::from_value(params["area"].clone())?;
                area.validate(&screen)?;
                if !flow_capture_windows::screens()?.contains(&screen) {
                    return Err("The selected display changed".into());
                }
                let token = uuid::Uuid::new_v4().to_string();
                self.prepared_capture = Some((
                    token.clone(),
                    screen,
                    area,
                    text(params, "title")?.into(),
                    text(params, "context")?.into(),
                ));
                Ok(json!({"token":token}))
            }
            "start_capture" => {
                if params["exclusion_ready"].as_bool() != Some(true)
                    || params["shortcut_ready"].as_bool() != Some(true)
                {
                    return Err("Recording controls are not ready".into());
                }
                let prepared = self
                    .prepared_capture
                    .as_ref()
                    .ok_or("Capture must be prepared first")?;
                if text(params, "token")? != prepared.0 {
                    return Err("Capture preparation expired".into());
                }
                let (_, screen, area, title, context) = self.prepared_capture.take().unwrap();
                let root = self.root.clone();
                self.operation = Some(operation::Operation::start(
                    "preparing_capture",
                    None,
                    self.revision,
                    move |work| recording::record(&root, &title, &context, screen, area, work),
                ));
                self.operation_applied = false;
                Ok(serde_json::to_value(
                    self.operation.as_ref().unwrap().status(),
                )?)
            }
            "operation_status" => Ok(self
                .operation
                .as_ref()
                .map(|op| serde_json::to_value(op.status()).unwrap())
                .unwrap_or(Value::Null)),
            "cancel_operation" => {
                let op = self.operation.as_ref().ok_or("No operation is active")?;
                if text(params, "id")? != op.status().id {
                    return Err("Operation identity changed".into());
                }
                op.cancel();
                Ok(json!({"cancelling":true}))
            }
            "list_projects" => {
                let mut projects = Vec::new();
                for entry in fs::read_dir(self.root.join("sessions"))? {
                    let entry = entry?;
                    if entry.file_type()?.is_dir()
                        && let Ok(project) = Project::load(&entry.path())
                    {
                        projects.push(json!({"id":entry.file_name().to_string_lossy(),"title":project.title,"created":project.created,"duration_ms":project.duration_ms,"steps":project.steps.len()}));
                    }
                }
                projects.sort_by(|a, b| b["created"].as_str().cmp(&a["created"].as_str()));
                Ok(json!({"items":projects}))
            }
            "open_project" => {
                let id = text(params, "recording_id")?;
                let directory = self.directory(id)?;
                let prepared = self.prepared_open.lock().unwrap().take();
                let project = match prepared {
                    Some((prepared_id, project)) if prepared_id == id => project,
                    _ => {
                        let project = Project::load(&directory)?;
                        if project.state == "recording" {
                            return Err("Recovery required: prepare_open must complete before opening this recording".into());
                        }
                        project
                    }
                };
                self.opened = Some((id.into(), project));
                self.revision += 1;
                Ok(self.snapshot(params["paged"].as_bool().unwrap_or(false)))
            }
            "metadata_page" => {
                let (id, project) = self.opened.as_ref().ok_or("No recording is open")?;
                if params["revision"].as_u64() != Some(self.revision)
                    || text(params, "recording_id")? != id
                {
                    return Err("Stale project revision; reopen before editing".into());
                }
                let offset = params["offset"].as_u64().ok_or("Missing offset")? as usize;
                let limit = params["limit"].as_u64().unwrap_or(128).clamp(1, 128) as usize;
                let (items, total) = match text(params, "kind")? {
                    "frames" => (
                        serde_json::to_value(
                            project
                                .frames
                                .iter()
                                .skip(offset)
                                .take(limit)
                                .collect::<Vec<_>>(),
                        )?,
                        project.frames.len(),
                    ),
                    "steps" => (
                        serde_json::to_value(
                            project
                                .steps
                                .iter()
                                .skip(offset)
                                .take(limit)
                                .collect::<Vec<_>>(),
                        )?,
                        project.steps.len(),
                    ),
                    _ => return Err("Unknown metadata kind".into()),
                };
                if offset > total {
                    return Err("Metadata offset is out of range".into());
                }
                let next = offset.saturating_add(limit);
                Ok(
                    json!({"items":items,"revision":self.revision,"next_offset": if next < total { Some(next) } else { None }}),
                )
            }
            "frame" => {
                let (id, project) = self.opened.as_ref().ok_or("No recording is open")?;
                if text(params, "recording_id")? != id {
                    return Err("Recording changed".into());
                }
                let index = params["index"].as_u64().ok_or("Missing frame index")? as usize;
                let directory = self.directory(id)?;
                let original = project.image_path(&directory, index)?;
                let path = if let Some(width) = params["preview_width"].as_u64() {
                    self.previews
                        .reference(&directory, original, width.clamp(64, 1440) as u32)
                } else {
                    original
                };
                Ok(json!({"path":path,"recording_id":id,"index":index,"revision":self.revision}))
            }
            "edit_step" | "review_step" | "add_step" | "remove_step" | "replace_step"
            | "edit_project" => {
                if params["revision"].as_u64() != Some(self.revision) {
                    return Err("Stale project revision; reopen before editing".into());
                }
                let (id, original) = self.opened.as_ref().ok_or("No recording is open")?;
                if text(params, "recording_id")? != id {
                    return Err("Recording changed".into());
                }
                let directory = self.directory(id)?;
                let mut project = original.clone();
                let dependencies = project.dependencies();
                if method == "edit_project" {
                    let title = text(params, "title")?.trim();
                    project.title = if title.is_empty() {
                        "Desktop workflow"
                    } else {
                        title
                    }
                    .into();
                    let context = text(params, "context")?;
                    if project.context != context {
                        for step in &mut project.steps {
                            if !step.action.is_empty() || !step.result.is_empty() {
                                step.status = "stale".into();
                                step.reviewed = false;
                            }
                        }
                    }
                    project.context = context.into();
                    if let Some(task) = params["task"].as_str() {
                        project.task = task.into();
                    }
                } else if method == "add_step" {
                    let frame = params["frame"].as_u64().ok_or("Missing frame")? as usize;
                    if frame >= project.frames.len() {
                        return Err("Frame is out of range".into());
                    }
                    if !project.steps.iter().any(|s| s.frame == frame) {
                        project.steps.push(Step::new(frame));
                    }
                } else {
                    let step_id = text(params, "step_id")?;
                    let position = project
                        .steps
                        .iter()
                        .position(|s| s.id == step_id)
                        .ok_or("Step no longer exists")?;
                    if method == "remove_step" {
                        project.steps.remove(position);
                    } else {
                        if method == "replace_step"
                            && project.steps.iter().enumerate().any(|(index, step)| {
                                index != position
                                    && Some(step.frame as u64) == params["frame"].as_u64()
                            })
                        {
                            return Err("This frame is already selected".into());
                        }
                        let step = &mut project.steps[position];
                        match method {
                            "edit_step" => {
                                step.title = text(params, "title")?.into();
                                step.action = text(params, "action")?.into();
                                step.result = text(params, "result")?.into();
                                step.uncertainty = text(params, "uncertainty")?.into();
                                step.status = "edited".into();
                                step.manual = true;
                                step.reviewed = false;
                            }
                            "review_step" => {
                                step.reviewed = params["reviewed"]
                                    .as_bool()
                                    .ok_or("Missing reviewed flag")?;
                                if step.reviewed && step.status == "stale" {
                                    step.status =
                                        if step.manual { "edited" } else { "generated" }.into();
                                }
                            }
                            "replace_step" => {
                                let frame =
                                    params["frame"].as_u64().ok_or("Missing frame")? as usize;
                                if frame >= project.frames.len() {
                                    return Err("Frame is out of range".into());
                                }
                                step.frame = frame;
                            }
                            _ => unreachable!(),
                        }
                    }
                }
                project.steps.sort_by_key(|s| s.frame);
                if ["add_step", "remove_step", "replace_step"].contains(&method) {
                    project.invalidate_changed(&dependencies);
                    flow_core::project::atomic_json(
                        &directory.join(".rust-selection-edited"),
                        &json!({"modified":true}),
                    )?;
                }
                project.save(&directory)?;
                self.opened = Some((id.clone(), project));
                self.revision += 1;
                Ok(self.snapshot(params["paged"].as_bool().unwrap_or(false)))
            }
            "shutdown" => Ok(json!({"stopped":true})),
            _ => Err("Unknown or unavailable prototype command".into()),
        }
    }
}

fn text<'a>(value: &'a Value, key: &str) -> Result<&'a str> {
    value[key]
        .as_str()
        .ok_or_else(|| format!("Missing text field: {key}").into())
}

fn main() {
    if run().is_err() {
        eprintln!(
            "Engine stopped: protocol, data-root or I/O failure. No recording content logged."
        );
        std::process::exit(1);
    }
}
fn run() -> Result<()> {
    let args: Vec<_> = std::env::args().collect();
    let index = args
        .iter()
        .position(|s| s == "--data-root")
        .ok_or("Explicit data root required")?;
    let requested_root = Path::new(args.get(index + 1).ok_or("Missing data root")?);
    let mut input = io::stdin().lock();
    let mut output = io::stdout().lock();
    let session = uuid::Uuid::new_v4().to_string();
    let first = protocol::read_message(&mut input)?.ok_or("Handshake required")?;
    if first.protocol != protocol::MAJOR || first.method != "hello" {
        protocol::write_message(
            &mut output,
            &Response {
                protocol: protocol::MAJOR,
                id: first.id,
                session,
                result: None,
                error: Some(Error {
                    code: "protocol_mismatch".into(),
                    message: "Compatible hello handshake required".into(),
                }),
            },
        )?;
        return Ok(());
    }
    if protocol::commands::validate_request("hello", &first.params).is_err() {
        protocol::write_message(
            &mut output,
            &Response {
                protocol: protocol::MAJOR,
                id: first.id,
                session,
                result: None,
                error: Some(Error {
                    code: "invalid_params".into(),
                    message: "Invalid hello parameters".into(),
                }),
            },
        )?;
        return Ok(());
    }
    let notifications = first.params["notifications"].as_bool().unwrap_or(false);
    let (root, _lock) = match acquire_data_root(requested_root) {
        Ok(owned) => owned,
        Err(_) => {
            protocol::write_message(&mut output, &Response {
                protocol: protocol::MAJOR, id: first.id, session,
                result: None, error: Some(Error {
                    code: "data_root_unavailable".into(),
                    message: "This data folder is already open or cannot be locked. Close its other owner and retry; do not delete a live lock.".into(),
                }),
            })?;
            return Ok(());
        }
    };
    protocol::write_message(
        &mut output,
        &Response {
            protocol: protocol::MAJOR,
            id: first.id,
            session: session.clone(),
            result: Some(
                json!({"engine_version":"0.2.0-prototype","protocol_minor":2,"notifications":notifications}),
            ),
            error: None,
        },
    )?;
    let mut engine = Engine {
        prepared_open: Default::default(),
        models: model::Models::new(root.clone()),
        previews: preview::Previews::new(),
        root,
        opened: None,
        revision: 0,
        operation: None,
        organization: None,
        catalog: None,
        operation_applied: true,
        prepared_capture: None,
    };
    drop(input);
    let (requests, receiver) = std::sync::mpsc::sync_channel(16);
    std::thread::spawn(move || {
        let mut input = io::stdin().lock();
        loop {
            let request = protocol::read_message(&mut input);
            let finished = !matches!(request, Ok(Some(_)));
            if requests.send(request).is_err() || finished {
                break;
            }
        }
    });
    let mut last_status = Value::Null;
    let mut last_organization = Value::Null;
    loop {
        if let Some(background) = &engine.organization {
            let current = json!(background.status());
            if notifications && current != last_organization {
                protocol::write_message(
                    &mut output,
                    &protocol::Notification {
                        protocol: protocol::MAJOR,
                        session: session.clone(),
                        event: "organization_status".into(),
                        data: current.clone(),
                    },
                )?;
                last_organization = current;
            }
        }
        if let Some(operation) = &engine.operation {
            // Read terminal state first, then drain every save that precedes it.
            // This prevents a final notification overtaking a durable-step acknowledgement.
            let current = serde_json::to_value(operation.status())?;
            for status in operation.saved_notifications() {
                if notifications {
                    protocol::write_message(
                        &mut output,
                        &protocol::Notification {
                            protocol: protocol::MAJOR,
                            session: session.clone(),
                            event: "operation_saved".into(),
                            data: serde_json::to_value(status)?,
                        },
                    )?;
                }
            }
            if notifications && current != last_status {
                protocol::write_message(
                    &mut output,
                    &protocol::Notification {
                        protocol: protocol::MAJOR,
                        session: session.clone(),
                        event: "operation_status".into(),
                        data: current.clone(),
                    },
                )?;
                last_status = current;
            }
        }
        let request = match receiver.recv_timeout(std::time::Duration::from_millis(100)) {
            Ok(Ok(Some(request))) => request,
            Ok(Ok(None)) | Err(std::sync::mpsc::RecvTimeoutError::Disconnected) => break,
            Ok(Err(error)) => return Err(error.into()),
            Err(std::sync::mpsc::RecvTimeoutError::Timeout) => continue,
        };
        let invalid_params =
            protocol::commands::validate_request(&request.method, &request.params).is_err();
        let result = if request.protocol != protocol::MAJOR {
            Err("Protocol version mismatch".into())
        } else if invalid_params {
            Err("Invalid command parameters".into())
        } else {
            engine.dispatch(&request.method, &request.params)
        };
        let response = match result {
            Ok(value) => Response {
                protocol: protocol::MAJOR,
                id: request.id,
                session: session.clone(),
                result: Some(value),
                error: None,
            },
            Err(error) => Response {
                protocol: protocol::MAJOR,
                id: request.id,
                session: session.clone(),
                result: None,
                error: Some(Error {
                    code: if request.protocol != protocol::MAJOR {
                        "protocol_mismatch"
                    } else if invalid_params {
                        "invalid_params"
                    } else if error.to_string().starts_with("Stale") {
                        "stale_revision"
                    } else {
                        "operation_failed"
                    }
                    .into(),
                    message: error.to_string(),
                }),
            },
        };
        protocol::write_message(&mut output, &response)?;
        if request.method == "shutdown" && response.error.is_none() {
            break;
        }
    }
    Ok(())
}

fn acquire_data_root(requested: &Path) -> Result<(PathBuf, data_lock::DataLock)> {
    // Never call before a validated hello. Debug builds stay isolated even when
    // somebody accidentally launches the executable without the development script.
    #[cfg(debug_assertions)]
    {
        let root = requested.canonicalize()?;
        if !root.join(".flow-recorder-development").is_file() {
            return Err("Debug builds require a marked development root".into());
        }
        if let Ok(local) = std::env::var("LOCALAPPDATA")
            && ["Phraseback", "FlowRecorder"].iter().any(|name| {
                root == Path::new(&local)
                    .join(name)
                    .canonicalize()
                    .unwrap_or_default()
            })
        {
            return Err("Debug builds cannot open normal application data".into());
        }
    }
    #[cfg(not(debug_assertions))]
    fs::create_dir_all(requested)?;
    let root = requested.canonicalize()?;
    let lock = data_lock::DataLock::acquire(&root)?;
    fs::create_dir_all(root.join("sessions"))?;
    Ok((root, lock))
}

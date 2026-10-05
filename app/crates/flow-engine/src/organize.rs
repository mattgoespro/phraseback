use crate::operation::Work;
use flow_core::{
    project::{Project, Result, Step, atomic_json},
    selection::{Descriptor, Suggestions, from_runs},
};
use serde_json::{Value, json};
use std::{fs, path::Path};

pub fn suggestions(project: &Project, directory: &Path, work: &Work) -> Result<Suggestions> {
    if project.frames.is_empty() {
        return Ok(from_runs(&[], 0));
    }
    let load = |index: usize| -> Result<Descriptor> {
        let cached = directory
            .join("derived-rust-v1")
            .join(format!("{index:07}.descriptor"));
        if let Ok(bytes) = fs::read(&cached)
            && let Ok(value) = Descriptor::decode(&bytes)
        {
            return Ok(value);
        }
        let image = image::open(project.image_path(directory, index)?)?.to_rgba8();
        let bytes = flow_capture_windows::descriptor(image.as_raw(), image.width(), image.height());
        // Legacy fallback includes its recorded cursor. Never alter source pixels.
        Descriptor::decode(&bytes)
    };
    let mut anchor = load(0)?;
    let mut start = 0;
    let mut runs = Vec::new();
    for i in 1..project.frames.len() {
        work.check()?;
        let current = load(i)?;
        if anchor.difference(&current) > 1.0 {
            runs.push((
                start,
                i - 1,
                project.frames[i]
                    .time_ms
                    .saturating_sub(project.frames[start].time_ms),
            ));
            start = i;
            anchor = current;
        }
        if i % 32 == 0 {
            work.update(
                "organizing",
                (i * 100 / project.frames.len()) as u32,
                "Organizing screenshots…",
            );
        }
    }
    runs.push((
        start,
        project.frames.len() - 1,
        project
            .duration_ms
            .saturating_sub(project.frames[start].time_ms),
    ));
    Ok(from_runs(&runs, project.frames.len()))
}
pub fn analyze(project: &Project, directory: &Path, work: &Work) -> Result<Suggestions> {
    let result = suggestions(project, directory, work)?;
    work.check()?;
    let cache = directory.join("derived-rust-v1");
    // Disposable cache failure must not block review or publish source changes.
    let _ = fs::create_dir_all(&cache).and_then(|_| {
        atomic_json(
            &cache.join("suggestions.json"),
            &json!({"frames":project.frames,"suggestions":result}),
        )
        .map_err(std::io::Error::other)
    });
    Ok(result)
}

pub fn apply(project: &mut Project, directory: &Path, result: Suggestions) -> Result<Value> {
    // A separately persisted marker survives cache deletion. Reorganizing must not
    // undo a screenshot choice, including a deliberate removal, made by the user.
    let preserve = directory.join(".rust-selection-edited").exists()
        || project
            .steps
            .iter()
            .any(|s| s.manual || !s.action.is_empty() || !s.result.is_empty());
    if !preserve {
        let previous = project.dependencies();
        project.steps = result
            .selected
            .iter()
            .map(|&frame| {
                project
                    .steps
                    .iter()
                    .find(|s| s.frame == frame)
                    .cloned()
                    .unwrap_or_else(|| Step::new(frame))
            })
            .collect();
        project.invalidate_changed(&previous);
        project.save(directory)?;
    }
    Ok(json!({"suggestions":result,"choices_preserved":preserve}))
}

pub fn run(mut project: Project, directory: &Path, work: Work) -> Result<Value> {
    let result = analyze(&project, directory, &work)?;
    work.check()?;
    apply(&mut project, directory, result)
}

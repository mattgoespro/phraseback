use serde::{Deserialize, Serialize};
use std::{
    collections::HashMap,
    fs::{self, OpenOptions},
    io::{BufReader, Write},
    path::{Component, Path, PathBuf},
};
use tempfile::NamedTempFile;

pub type Result<T> = std::result::Result<T, Box<dyn std::error::Error + Send + Sync>>;

#[derive(Clone, Debug, PartialEq)]
pub struct EvidenceRevision {
    context: String,
    initial: bool,
    frames: Vec<(&'static str, Frame)>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Frame {
    pub file: String,
    pub time_ms: u64,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Step {
    pub frame: usize,
    #[serde(default = "new_id")]
    pub id: String,
    #[serde(default = "step_title")]
    pub title: String,
    #[serde(default)]
    pub action: String,
    #[serde(default)]
    pub result: String,
    #[serde(default = "pending")]
    pub status: String,
    #[serde(default)]
    pub manual: bool,
    #[serde(default)]
    pub reviewed: bool,
    #[serde(default)]
    pub uncertainty: String,
}

fn new_id() -> String {
    uuid::Uuid::new_v4().simple().to_string()
}
fn step_title() -> String {
    "Untitled step".into()
}
fn pending() -> String {
    "pending".into()
}
fn version_one() -> u32 {
    1
}
fn project_title() -> String {
    "Desktop workflow".into()
}
fn recording() -> String {
    "recording".into()
}
fn created_now() -> String {
    chrono::Local::now().format("%Y-%m-%dT%H:%M:%S").to_string()
}

impl Step {
    pub fn new(frame: usize) -> Self {
        Self {
            frame,
            id: uuid::Uuid::new_v4().simple().to_string(),
            title: "Untitled step".into(),
            action: String::new(),
            result: String::new(),
            status: "pending".into(),
            manual: false,
            reviewed: false,
            uncertainty: String::new(),
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Project {
    #[serde(default = "version_one")]
    pub version: u32,
    #[serde(default = "project_title")]
    pub title: String,
    #[serde(default)]
    pub context: String,
    #[serde(default)]
    pub task: String,
    #[serde(default = "created_now")]
    pub created: String,
    #[serde(default)]
    pub frames: Vec<Frame>,
    #[serde(default)]
    pub steps: Vec<Step>,
    #[serde(default)]
    pub duration_ms: u64,
    #[serde(default)]
    pub width: u32,
    #[serde(default)]
    pub height: u32,
    #[serde(default)]
    pub error: String,
    #[serde(default = "recording")]
    pub state: String,
}

impl Project {
    pub fn load(directory: &Path) -> Result<Self> {
        let project: Self = serde_json::from_slice(&fs::read(directory.join("project.json"))?)?;
        project.validate(directory)?;
        Ok(project)
    }

    pub fn validate(&self, directory: &Path) -> Result<()> {
        if self.version != 1 {
            return Err("Unsupported recording version".into());
        }
        if self.frames.windows(2).any(|f| f[0].time_ms > f[1].time_ms) {
            return Err("Frame timestamps are out of order".into());
        }
        let mut ids = std::collections::HashSet::new();
        let mut selected = std::collections::HashSet::new();
        for step in &self.steps {
            if step.id.is_empty()
                || step.frame >= self.frames.len()
                || !ids.insert(&step.id)
                || !selected.insert(step.frame)
            {
                return Err("Invalid or duplicate selected moment".into());
            }
        }
        if self.state != "recording" {
            for i in 0..self.frames.len() {
                match self.image_path(directory, i) {
                    // Missing evidence must not hide a project or erase its annotations.
                    // Preview/export access still reports the missing file explicitly.
                    Err(error)
                        if error
                            .downcast_ref::<std::io::Error>()
                            .is_some_and(|e| e.kind() == std::io::ErrorKind::NotFound) => {}
                    result => {
                        result?;
                    }
                }
            }
        }
        Ok(())
    }

    pub fn image_path(&self, directory: &Path, index: usize) -> Result<PathBuf> {
        let frame = self.frames.get(index).ok_or("Frame is out of range")?;
        let relative = Path::new(&frame.file);
        if relative
            .components()
            .any(|c| !matches!(c, Component::Normal(_)))
            || !frame.file.starts_with("frames/")
            || frame.file.contains(['\\', ':'])
        {
            return Err("Frame path is outside this recording".into());
        }
        let base = directory.join("frames").canonicalize()?;
        if !base.starts_with(directory.canonicalize()?) {
            return Err("Frame directory is outside this recording".into());
        }
        let path = directory.join(relative).canonicalize()?;
        if !path.starts_with(base) || !path.is_file() {
            return Err("Frame path is outside this recording".into());
        }
        Ok(path)
    }

    pub fn save(&self, directory: &Path) -> Result<()> {
        self.validate(directory)?;
        backup(directory)?;
        atomic_json(&directory.join("project.json"), self)
    }

    pub fn recover(&mut self, directory: &Path) -> Result<bool> {
        self.recover_checked(directory, |_| Ok(()))
    }

    /// Check cancellation throughout validation; never publish a partial recovery.
    pub fn recover_checked(
        &mut self,
        directory: &Path,
        mut checkpoint: impl FnMut(usize) -> Result<()>,
    ) -> Result<bool> {
        checkpoint(0)?;
        if self.state != "recording" {
            return Ok(false);
        }
        let mut candidate = self.clone();
        candidate.frames.clear();
        let mut recovered_dimensions = None;
        match fs::read(directory.join("frames.jsonl")) {
            Ok(journal) => {
                for line in journal
                    .split(|byte| *byte == b'\n')
                    .filter(|line| !line.is_empty())
                {
                    checkpoint(candidate.frames.len())?;
                    let Ok(item) = serde_json::from_slice::<serde_json::Value>(line) else {
                        break;
                    };
                    if let Some(duration) = item.get("duration_ms").and_then(|v| v.as_u64()) {
                        candidate.duration_ms = candidate.duration_ms.max(duration);
                    } else if let Ok(frame) = serde_json::from_value::<Frame>(item) {
                        if candidate
                            .frames
                            .last()
                            .is_some_and(|previous| previous.time_ms > frame.time_ms)
                        {
                            break;
                        }
                        candidate.frames.push(frame);
                        let count = candidate.frames.len();
                        let dimensions = candidate
                            .image_path(directory, count - 1)
                            .and_then(|path| png_dimensions(&path, &mut || checkpoint(count)));
                        // Cancellation is not corrupt evidence: propagate it instead of
                        // silently publishing a shorter recovered prefix.
                        checkpoint(count)?;
                        match dimensions {
                            Ok(size)
                                if recovered_dimensions.is_none()
                                    || recovered_dimensions == Some(size) =>
                            {
                                recovered_dimensions = Some(size);
                            }
                            _ => {
                                candidate.frames.pop();
                                break;
                            }
                        }
                    } else {
                        break;
                    }
                }
            }
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => {}
            Err(error) => return Err(error.into()),
        }
        candidate.steps.clear();
        let before = self.dependencies();
        for step in &self.steps {
            let original = &self.frames[step.frame].file;
            if let Some(index) = candidate.frames.iter().position(|f| &f.file == original) {
                let mut retained = step.clone();
                retained.frame = index;
                candidate.steps.push(retained);
            } else if step.manual
                || step.reviewed
                || !step.action.is_empty()
                || !step.result.is_empty()
                || !step.uncertainty.is_empty()
                || step.title != step_title()
            {
                return Err("Recovery would detach an annotated moment from its evidence; original metadata and images were retained".into());
            }
        }
        candidate.steps.sort_by_key(|step| step.frame);
        candidate.invalidate_changed(&before);
        if !candidate.frames.is_empty() {
            candidate.duration_ms = candidate
                .duration_ms
                .max(candidate.frames.last().unwrap().time_ms.saturating_add(125));
            if candidate.steps.is_empty() {
                candidate.steps.push(Step::new(0));
                if candidate.frames.len() > 1 {
                    candidate.steps.push(Step::new(candidate.frames.len() - 1));
                }
            }
            (candidate.width, candidate.height) = recovered_dimensions.unwrap();
        }
        candidate.state = "ready".into();
        if !candidate.error.is_empty() {
            candidate.error.push('\n');
        }
        candidate.error.push_str("Recording was interrupted. Available frames have been recovered; original files were retained.");
        checkpoint(candidate.frames.len())?;
        candidate.save(directory)?;
        *self = candidate;
        Ok(true)
    }

    pub fn dependencies(&self) -> HashMap<String, EvidenceRevision> {
        self.steps
            .iter()
            .enumerate()
            .map(|(i, s)| {
                let mut frames = Vec::new();
                if i > 0 {
                    frames.push((
                        "previous_selected",
                        self.frames[self.steps[i - 1].frame].clone(),
                    ));
                }
                if s.frame > 0 && (i == 0 || self.steps[i - 1].frame != s.frame - 1) {
                    frames.push(("immediately_before", self.frames[s.frame - 1].clone()));
                }
                frames.push(("current", self.frames[s.frame].clone()));
                (
                    s.id.clone(),
                    EvidenceRevision {
                        context: self.context.chars().take(1000).collect(),
                        initial: s.frame == 0,
                        frames,
                    },
                )
            })
            .collect()
    }

    pub fn invalidate_changed(&mut self, before: &HashMap<String, EvidenceRevision>) {
        let after = self.dependencies();
        for step in &mut self.steps {
            if before.get(&step.id) != after.get(&step.id)
                && (!step.action.is_empty() || !step.result.is_empty())
            {
                step.status = "stale".into();
                step.reviewed = false;
            }
        }
    }
}

fn png_dimensions(path: &Path, checkpoint: &mut impl FnMut() -> Result<()>) -> Result<(u32, u32)> {
    let mut decoder = png::Decoder::new(BufReader::new(fs::File::open(path)?));
    decoder.set_limits(png::Limits {
        bytes: 128 * 1024 * 1024,
    });
    let mut reader = decoder.read_info()?;
    let size = (reader.info().width, reader.info().height);
    if size.0 == 0 || size.1 == 0 || size.0 as u64 * size.1 as u64 > 64 * 1024 * 1024 {
        return Err("Recovery image dimensions exceed the safety limit".into());
    }
    while reader.next_row()?.is_some() {
        checkpoint()?;
    }
    reader.finish()?;
    Ok(size)
}

pub fn atomic_json(path: &Path, value: &impl Serialize) -> Result<()> {
    atomic_json_checked(path, value, |_| Ok(()))
}

#[derive(Clone, Copy, PartialEq, Debug)]
enum SaveStage {
    Created,
    Written,
    Synced,
}

// A private checkpoint seam permits deterministic storage-failure tests without
// shipping an environment variable or protocol command that can inject faults.
fn atomic_json_checked(
    path: &Path,
    value: &impl Serialize,
    mut checkpoint: impl FnMut(SaveStage) -> std::io::Result<()>,
) -> Result<()> {
    let mut temporary = NamedTempFile::new_in(path.parent().ok_or("Missing parent")?)?;
    checkpoint(SaveStage::Created)?;
    serde_json::to_writer_pretty(&mut temporary, value)?;
    temporary.write_all(b"\n")?;
    checkpoint(SaveStage::Written)?;
    temporary.as_file().sync_all()?;
    checkpoint(SaveStage::Synced)?;
    persist_metadata(temporary, path)?;
    Ok(())
}

#[cfg(not(windows))]
fn persist_metadata(temporary: NamedTempFile, path: &Path) -> Result<()> {
    temporary.persist(path)?;
    Ok(())
}

#[cfg(windows)]
fn persist_metadata(mut temporary: NamedTempFile, path: &Path) -> Result<()> {
    // Readers without FILE_SHARE_DELETE can briefly prevent atomic replacement.
    // Reuse the already flushed file; never delete the destination or rewrite
    // permissions. Permanent denial is still returned after at most 150 ms sleep.
    let mut delays = [10, 20, 40, 80].into_iter();
    loop {
        match temporary.persist(path) {
            Ok(_) => return Ok(()),
            Err(error) => {
                if !matches!(error.error.raw_os_error(), Some(5 | 32 | 33)) {
                    return Err(error.into());
                }
                let Some(delay) = delays.next() else {
                    return Err(error.into());
                };
                temporary = error.file;
                std::thread::sleep(std::time::Duration::from_millis(delay));
            }
        }
    }
}

fn backup(directory: &Path) -> Result<()> {
    backup_checked(directory, |_| {})
}

fn backup_checked(directory: &Path, before_publish: impl FnOnce(&Path)) -> Result<()> {
    let destination = directory.join("legacy-backup-rust-v1");
    if destination.join("complete.json").is_file() {
        return Ok(());
    }
    if destination.exists() {
        return Err("Incomplete legacy backup; original metadata has not been modified".into());
    }
    let stage = tempfile::Builder::new()
        .prefix(".backup-")
        .tempdir_in(directory)
        .map_err(|error| format!("Cannot create legacy backup staging directory: {error}"))?;
    for name in ["project.json", "frames.jsonl"] {
        let source = directory.join(name);
        if source.exists() {
            let target = stage.path().join(name);
            fs::copy(source, &target)
                .map_err(|error| format!("Cannot copy {name} into legacy backup: {error}"))?;
            OpenOptions::new()
                .write(true)
                .open(target)
                .map_err(|error| format!("Cannot open {name} for legacy backup flush: {error}"))?
                .sync_all()
                .map_err(|error| format!("Cannot flush {name} in legacy backup: {error}"))?;
        }
    }
    atomic_json(
        &stage.path().join("complete.json"),
        &serde_json::json!({"format":1}),
    )
    .map_err(|error| format!("Cannot write legacy backup completion marker: {error}"))?;
    before_publish(stage.path());
    publish_backup(stage.path(), &destination)
        .map_err(|error| format!("Cannot publish legacy backup directory: {error}"))?;
    Ok(())
}

fn publish_backup(source: &Path, destination: &Path) -> std::io::Result<()> {
    #[cfg(windows)]
    {
        let mut delays = [10, 20, 40, 80].into_iter();
        loop {
            match fs::rename(source, destination) {
                Ok(()) => return Ok(()),
                Err(error) => {
                    if !matches!(error.raw_os_error(), Some(5 | 32 | 33)) {
                        return Err(error);
                    }
                    let Some(delay) = delays.next() else {
                        return Err(error);
                    };
                    std::thread::sleep(std::time::Duration::from_millis(delay));
                }
            }
        }
    }
    #[cfg(not(windows))]
    fs::rename(source, destination)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[cfg(windows)]
    #[test]
    fn backup_publication_survives_short_windows_reader_lock() {
        use std::os::windows::fs::OpenOptionsExt;
        let (dir, _) = fixture();
        let original = fs::read(dir.path().join("project.json")).unwrap();
        let mut release = None;
        let result = backup_checked(dir.path(), |stage| {
            let reader = OpenOptions::new()
                .read(true)
                .share_mode(1)
                .open(stage.join("project.json"))
                .unwrap();
            release = Some(std::thread::spawn(move || {
                std::thread::sleep(std::time::Duration::from_millis(45));
                drop(reader);
            }));
        });
        release.unwrap().join().unwrap();
        result.unwrap();
        assert_eq!(fs::read(dir.path().join("project.json")).unwrap(), original);
        assert_eq!(
            fs::read(dir.path().join("legacy-backup-rust-v1/project.json")).unwrap(),
            original
        );
    }
    #[cfg(windows)]
    #[test]
    fn backup_publication_permanent_lock_never_changes_original() {
        use std::os::windows::fs::OpenOptionsExt;
        let (dir, _) = fixture();
        let original = fs::read(dir.path().join("project.json")).unwrap();
        let mut held = None;
        let started = std::time::Instant::now();
        let result = backup_checked(dir.path(), |stage| {
            held = Some(
                OpenOptions::new()
                    .read(true)
                    .share_mode(1)
                    .open(stage.join("project.json"))
                    .unwrap(),
            );
        });
        assert!(
            result
                .unwrap_err()
                .to_string()
                .starts_with("Cannot publish legacy backup directory:")
        );
        assert!(started.elapsed() < std::time::Duration::from_secs(2));
        assert_eq!(fs::read(dir.path().join("project.json")).unwrap(), original);
        assert!(!dir.path().join("legacy-backup-rust-v1").exists());
        drop(held);
        backup(dir.path()).unwrap();
        assert_eq!(
            fs::read(dir.path().join("legacy-backup-rust-v1/project.json")).unwrap(),
            original
        );
    }
    #[test]
    fn cancelling_recovery_inside_png_validation_never_publishes_partial_metadata() {
        let dir = tempfile::tempdir().unwrap();
        fs::create_dir(dir.path().join("frames")).unwrap();
        let path = dir.path().join("frames/0000000.png");
        let mut encoder = png::Encoder::new(fs::File::create(&path).unwrap(), 2, 100);
        encoder.set_color(png::ColorType::Rgba);
        encoder.set_depth(png::BitDepth::Eight);
        encoder
            .write_header()
            .unwrap()
            .write_image_data(&vec![0; 800])
            .unwrap();
        let mut project: Project =
            serde_json::from_value(serde_json::json!({"state":"recording"})).unwrap();
        atomic_json(&dir.path().join("project.json"), &project).unwrap();
        fs::write(
            dir.path().join("frames.jsonl"),
            b"{\"file\":\"frames/0000000.png\",\"time_ms\":0}\n",
        )
        .unwrap();
        let before = fs::read(dir.path().join("project.json")).unwrap();
        let image = fs::read(&path).unwrap();
        let mut checks = 0;
        assert!(
            project
                .recover_checked(dir.path(), |_| {
                    checks += 1;
                    if checks >= 10 {
                        Err("cancelled".into())
                    } else {
                        Ok(())
                    }
                })
                .is_err()
        );
        assert_eq!(project.state, "recording");
        assert_eq!(fs::read(dir.path().join("project.json")).unwrap(), before);
        assert_eq!(fs::read(&path).unwrap(), image);
        assert!(!dir.path().join("legacy-backup-rust-v1").exists());
        assert!(project.recover(dir.path()).unwrap());
        assert_eq!(project.frames.len(), 1);
    }
    fn fixture() -> (tempfile::TempDir, Project) {
        let directory = tempfile::tempdir().unwrap();
        fs::create_dir(directory.path().join("frames")).unwrap();
        let mut frames = Vec::new();
        for i in 0..4 {
            let file = format!("frames/{i:07}.png");
            fs::write(directory.path().join(&file), b"fixture").unwrap();
            frames.push(Frame {
                file,
                time_ms: i as u64 * 125,
            });
        }
        let project = Project {
            version: 1,
            title: "Test".into(),
            context: String::new(),
            task: String::new(),
            created: "2026-09-20T10:00:00".into(),
            frames,
            steps: vec![Step::new(0), Step::new(3)],
            duration_ms: 500,
            width: 640,
            height: 400,
            error: String::new(),
            state: "ready".into(),
        };
        atomic_json(&directory.path().join("project.json"), &project).unwrap();
        (directory, project)
    }
    #[test]
    fn backup_is_once_and_original_frames_are_untouched() {
        let (dir, mut p) = fixture();
        let original = fs::read(dir.path().join("project.json")).unwrap();
        p.title = "Changed".into();
        p.save(dir.path()).unwrap();
        p.title = "Again".into();
        p.save(dir.path()).unwrap();
        assert_eq!(
            fs::read(dir.path().join("legacy-backup-rust-v1/project.json")).unwrap(),
            original
        );
        assert_eq!(Project::load(dir.path()).unwrap().title, "Again");
        assert_eq!(
            fs::read(p.image_path(dir.path(), 0).unwrap()).unwrap(),
            b"fixture"
        );
    }
    #[test]
    fn failed_backup_does_not_publish_mutation() {
        let (dir, mut p) = fixture();
        fs::write(dir.path().join("legacy-backup-rust-v1"), "blocked").unwrap();
        p.title = "Unsaved".into();
        assert!(p.save(dir.path()).is_err());
        assert_eq!(Project::load(dir.path()).unwrap().title, "Test");
    }

    #[test]
    fn injected_storage_failures_never_publish_or_leave_temporary_files() {
        for stage in [SaveStage::Created, SaveStage::Written, SaveStage::Synced] {
            for kind in [
                std::io::ErrorKind::StorageFull,
                std::io::ErrorKind::PermissionDenied,
            ] {
                let (dir, mut project) = fixture();
                let path = dir.path().join("project.json");
                let original = fs::read(&path).unwrap();
                let files_before = fs::read_dir(dir.path()).unwrap().count();
                project.title = "Must not be published".into();
                assert!(
                    atomic_json_checked(&path, &project, |reached| {
                        if reached == stage {
                            Err(std::io::Error::from(kind))
                        } else {
                            Ok(())
                        }
                    })
                    .is_err(),
                    "{stage:?}: {kind:?}"
                );
                assert_eq!(fs::read(&path).unwrap(), original);
                assert_eq!(fs::read_dir(dir.path()).unwrap().count(), files_before);
                project.save(dir.path()).unwrap();
                assert_eq!(Project::load(dir.path()).unwrap().title, project.title);
                assert_eq!(
                    fs::read(dir.path().join("legacy-backup-rust-v1/project.json")).unwrap(),
                    original
                );
            }
        }
    }

    #[cfg(windows)]
    #[test]
    fn atomic_save_survives_short_windows_reader_lock() {
        use std::os::windows::fs::OpenOptionsExt;
        let (dir, mut project) = fixture();
        let path = dir.path().join("project.json");
        let reader = std::fs::OpenOptions::new()
            .read(true)
            .share_mode(1)
            .open(&path)
            .unwrap();
        project.title = "Saved after reader closes".into();
        // Keep the target open without FILE_SHARE_DELETE, as external Windows
        // readers can do. Acquisition happens before save, not on a racing thread.
        let release = std::thread::spawn(move || {
            std::thread::sleep(std::time::Duration::from_millis(45));
            drop(reader);
        });
        let result = atomic_json(&path, &project);
        release.join().unwrap();
        result.unwrap();
        assert_eq!(Project::load(dir.path()).unwrap().title, project.title);
    }

    #[cfg(windows)]
    #[test]
    fn permanent_windows_reader_lock_preserves_original_and_cleans_staging() {
        use std::os::windows::fs::OpenOptionsExt;
        let (dir, mut project) = fixture();
        let path = dir.path().join("project.json");
        let original = fs::read(&path).unwrap();
        let files_before = fs::read_dir(dir.path()).unwrap().count();
        let reader = std::fs::OpenOptions::new()
            .read(true)
            .share_mode(1)
            .open(&path)
            .unwrap();
        project.title = "Not saved".into();
        let started = std::time::Instant::now();
        assert!(atomic_json(&path, &project).is_err());
        assert!(started.elapsed() < std::time::Duration::from_secs(2));
        assert_eq!(fs::read(&path).unwrap(), original);
        assert_eq!(fs::read_dir(dir.path()).unwrap().count(), files_before);
        drop(reader);
        atomic_json(&path, &project).unwrap();
        assert_eq!(Project::load(dir.path()).unwrap().title, project.title);
    }

    #[test]
    fn partial_serialization_failure_preserves_previous_document() {
        struct Partial;
        impl Serialize for Partial {
            fn serialize<S: serde::Serializer>(
                &self,
                serializer: S,
            ) -> std::result::Result<S::Ok, S::Error> {
                use serde::ser::SerializeStruct;
                let mut output = serializer.serialize_struct("Partial", 2)?;
                output.serialize_field("written", "partial bytes")?;
                Err(serde::ser::Error::custom("injected serialization failure"))
            }
        }
        let (dir, _) = fixture();
        let path = dir.path().join("project.json");
        let original = fs::read(&path).unwrap();
        assert!(atomic_json(&path, &Partial).is_err());
        assert_eq!(fs::read(&path).unwrap(), original);
        assert_eq!(fs::read_dir(dir.path()).unwrap().count(), 2);
    }
    #[test]
    fn rejects_traversal_and_out_of_range_steps() {
        let (dir, mut p) = fixture();
        p.frames[0].file = "frames/../../secret.png".into();
        assert!(p.validate(dir.path()).is_err());
        p.steps[0].frame = 100;
        assert!(p.validate(dir.path()).is_err());
    }
    #[test]
    fn adding_evidence_invalidates_successor_without_erasing_text() {
        let (_, mut p) = fixture();
        p.steps[1].action = "Manual wording".into();
        p.steps[1].manual = true;
        p.steps[1].reviewed = true;
        let before = p.dependencies();
        p.steps.insert(1, Step::new(2));
        p.invalidate_changed(&before);
        assert_eq!(p.steps[2].status, "stale");
        assert!(p.steps[2].manual);
        assert!(!p.steps[2].reviewed);
        assert_eq!(p.steps[2].action, "Manual wording");
    }

    #[test]
    fn evidence_role_changes_invalidate_even_when_frame_indices_match() {
        let (_, mut p) = fixture();
        p.steps.remove(0);
        p.steps[0].action = "Existing annotation".into();
        let before = p.dependencies();
        p.steps.insert(0, Step::new(2));
        p.invalidate_changed(&before);
        assert_eq!(p.steps[1].status, "stale");
    }

    #[test]
    fn evidence_timestamp_changes_invalidate_without_erasing_manual_text() {
        let (_, mut p) = fixture();
        p.steps[0].result = "Manual result".into();
        p.steps[0].manual = true;
        let before = p.dependencies();
        p.frames[0].time_ms = 50;
        p.invalidate_changed(&before);
        assert_eq!(p.steps[0].status, "stale");
        assert_eq!(p.steps[0].result, "Manual result");
        assert!(p.steps[0].manual);
    }
}

use flow_core::{project::Result, protocol::commands::LibraryEntry};
use serde::{
    Deserialize, Deserializer,
    de::{IgnoredAny, SeqAccess, Visitor},
};
use serde_json::{Value, json};
use std::{
    fmt, fs,
    io::BufReader,
    path::{Path, PathBuf},
    sync::{
        Arc,
        atomic::{AtomicBool, Ordering},
        mpsc::{self, Receiver, TryRecvError},
    },
};

#[derive(Default)]
struct Count(usize);
impl<'de> Deserialize<'de> for Count {
    fn deserialize<D: Deserializer<'de>>(deserializer: D) -> std::result::Result<Self, D::Error> {
        struct Counter;
        impl<'de> Visitor<'de> for Counter {
            type Value = Count;
            fn expecting(&self, formatter: &mut fmt::Formatter) -> fmt::Result {
                formatter.write_str("a step array")
            }
            fn visit_seq<A: SeqAccess<'de>>(
                self,
                mut sequence: A,
            ) -> std::result::Result<Count, A::Error> {
                let mut count = 0;
                while sequence.next_element::<IgnoredAny>()?.is_some() {
                    count += 1;
                }
                Ok(Count(count))
            }
        }
        deserializer.deserialize_seq(Counter)
    }
}
#[derive(Deserialize)]
struct Summary {
    #[serde(default = "version")]
    version: u32,
    #[serde(default = "title")]
    title: String,
    #[serde(default)]
    created: String,
    #[serde(default)]
    duration_ms: i64,
    #[serde(default)]
    steps: Count,
}
fn version() -> u32 {
    1
}
fn title() -> String {
    "Desktop workflow".into()
}

fn scan(root: &Path, cancel: &AtomicBool) -> Result<Vec<LibraryEntry>> {
    let sessions = root.join("sessions").canonicalize()?;
    if !sessions.starts_with(root) {
        return Err("Sessions directory is outside data root".into());
    }
    let mut items = Vec::new();
    for entry in fs::read_dir(&sessions)? {
        if cancel.load(Ordering::Relaxed) {
            return Err("Library refresh cancelled".into());
        }
        let entry = entry?;
        if !entry.file_type()?.is_dir() {
            continue;
        }
        let directory = entry.path().canonicalize()?;
        if !directory.starts_with(&sessions) {
            continue;
        }
        // Deserialize only summary fields. Unknown frame data is streamed past;
        // no frame array, image decode or evidence-path validation occurs here.
        let Ok(metadata) = directory.join("project.json").canonicalize() else {
            continue;
        };
        if !metadata.starts_with(&directory) {
            continue;
        }
        let Ok(file) = fs::File::open(metadata) else {
            continue;
        };
        let Ok(summary) = serde_json::from_reader::<_, Summary>(BufReader::new(file)) else {
            continue;
        };
        if summary.version != 1 {
            continue;
        }
        items.push(LibraryEntry {
            id: entry.file_name().to_string_lossy().into_owned(),
            title: summary.title,
            created: summary.created,
            duration_ms: summary.duration_ms,
            steps: i32::try_from(summary.steps.0)?,
        });
    }
    items.sort_by(|a, b| b.created.cmp(&a.created).then_with(|| b.id.cmp(&a.id)));
    Ok(items)
}

pub struct Catalog {
    id: String,
    receiver: Receiver<Result<Vec<LibraryEntry>>>,
    items: Option<Vec<LibraryEntry>>,
    error: Option<String>,
    cancel: Arc<AtomicBool>,
}
impl Catalog {
    fn start(root: PathBuf) -> Self {
        let cancel = Arc::new(AtomicBool::new(false));
        let worker_cancel = cancel.clone();
        let (sender, receiver) = mpsc::sync_channel(1);
        // One read-only catalog worker. Refresh never replaces a running worker.
        std::thread::spawn(move || {
            let _ = sender.send(scan(&root, &worker_cancel));
        });
        Self {
            id: uuid::Uuid::new_v4().to_string(),
            receiver,
            items: None,
            error: None,
            cancel,
        }
    }
    fn poll(&mut self) {
        if self.items.is_some() || self.error.is_some() {
            return;
        }
        match self.receiver.try_recv() {
            Ok(Ok(items)) => self.items = Some(items),
            Ok(Err(_)) | Err(TryRecvError::Disconnected) => {
                self.error = Some("Recording library could not be indexed".into())
            }
            Err(TryRecvError::Empty) => {}
        }
    }
}
impl Drop for Catalog {
    fn drop(&mut self) {
        self.cancel.store(true, Ordering::Relaxed);
    }
}

pub fn page(slot: &mut Option<Catalog>, root: &Path, params: &Value) -> Result<Value> {
    if let Some(catalog) = slot.as_mut() {
        catalog.poll();
    }
    let refresh = params["refresh"].as_bool().unwrap_or(false);
    if slot.is_none()
        || refresh
            && slot
                .as_ref()
                .is_some_and(|c| c.items.is_some() || c.error.is_some())
    {
        *slot = Some(Catalog::start(root.to_path_buf()));
    }
    let catalog = slot.as_mut().unwrap();
    if let Some(cursor) = params["catalog_id"].as_str()
        && cursor != catalog.id
    {
        return Err("Stale library cursor; refresh the library".into());
    }
    if let Some(error) = &catalog.error {
        return Err(error.clone().into());
    }
    let offset = params["offset"].as_u64().unwrap_or(0);
    let limit = params["limit"].as_u64().unwrap_or(64).clamp(1, 64) as usize;
    let Some(items) = &catalog.items else {
        return Ok(json!({"items":[],"catalog_id":catalog.id,"next_offset":null,"indexing":true}));
    };
    if offset > items.len() as u64 {
        return Err("Library offset is out of range".into());
    }
    let offset = offset as usize;
    let next = offset.saturating_add(limit);
    Ok(
        json!({"items":items.iter().skip(offset).take(limit).collect::<Vec<_>>(),
        "catalog_id":catalog.id,"next_offset":if next < items.len() {Some(next)} else {None},"indexing":false}),
    )
}

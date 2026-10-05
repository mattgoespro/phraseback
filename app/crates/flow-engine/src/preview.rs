//! Disposable previews. A full queue or failed cache write always falls back to evidence.
use flow_core::project::Result;
use image::ImageFormat;
use sha2::{Digest, Sha256};
use std::{
    fs,
    path::{Path, PathBuf},
    sync::mpsc::{self, SyncSender},
    thread::{self, JoinHandle},
    time::UNIX_EPOCH,
};

struct Job {
    source: PathBuf,
    destination: PathBuf,
    width: u32,
}

pub struct Previews {
    sender: Option<SyncSender<Job>>,
    worker: Option<JoinHandle<()>>,
}

impl Previews {
    pub fn new() -> Self {
        let (sender, receiver) = mpsc::sync_channel::<Job>(4);
        let worker = thread::spawn(move || {
            while let Ok(job) = receiver.recv() {
                // Cache failure must not turn a readable source into unavailable evidence.
                let _ = render(&job);
            }
        });
        Self {
            sender: Some(sender),
            worker: Some(worker),
        }
    }

    pub fn reference(&self, directory: &Path, source: PathBuf, width: u32) -> PathBuf {
        let result = (|| -> Result<PathBuf> {
            let directory = directory.canonicalize()?;
            let cache = directory.join("previews-rust-v1");
            if !cache.exists() {
                fs::create_dir(&cache)?;
            }
            let cache = cache.canonicalize()?;
            if !cache.starts_with(&directory) {
                return Err("Preview directory escapes recording".into());
            }
            let metadata = fs::metadata(&source)?;
            let modified = metadata.modified()?.duration_since(UNIX_EPOCH)?.as_nanos();
            let width = width.clamp(64, 1440);
            let key = format!("{}:{}:{modified}:{width}", source.display(), metadata.len());
            let name = format!("{:x}.png", Sha256::digest(key.as_bytes()));
            let destination = cache.join(name);
            if destination.is_file() {
                let path = destination.canonicalize()?;
                if path.starts_with(&cache) {
                    return Ok(path);
                }
                return Err("Preview file escapes cache".into());
            }
            if let Some(sender) = &self.sender {
                // Speculative work never blocks protocol handling or grows without bound.
                let _ = sender.try_send(Job {
                    source: source.clone(),
                    destination,
                    width,
                });
            }
            Ok(source.clone())
        })();
        result.unwrap_or(source)
    }
}

fn render(job: &Job) -> Result<()> {
    if job.destination.exists() {
        return Ok(());
    }
    let image = image::open(&job.source)?;
    let image = if image.width() > job.width || image.height() > 1440 {
        image.thumbnail(job.width, 1440)
    } else {
        image
    };
    let mut file =
        tempfile::NamedTempFile::new_in(job.destination.parent().ok_or("Missing preview parent")?)?;
    image.write_to(file.as_file_mut(), ImageFormat::Png)?;
    // A disposable cache needs atomic publication, not a journal or durability flush.
    file.persist_noclobber(&job.destination)
        .map_err(|error| error.error)?;
    Ok(())
}

impl Drop for Previews {
    fn drop(&mut self) {
        self.sender.take();
        if let Some(worker) = self.worker.take() {
            let deadline = std::time::Instant::now() + std::time::Duration::from_secs(5);
            while !worker.is_finished() && std::time::Instant::now() < deadline {
                thread::sleep(std::time::Duration::from_millis(10));
            }
            if worker.is_finished() {
                let _ = worker.join();
            } else {
                std::process::exit(2);
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn derived_preview_is_bounded_and_does_not_rewrite_evidence() {
        let directory = tempfile::tempdir().unwrap();
        let source = directory.path().join("original.png");
        image::RgbImage::new(2000, 1000).save(&source).unwrap();
        let original = fs::read(&source).unwrap();
        let destination = directory.path().join("preview.png");
        render(&Job {
            source: source.clone(),
            destination: destination.clone(),
            width: 640,
        })
        .unwrap();
        assert_eq!(image::image_dimensions(destination).unwrap(), (640, 320));
        assert_eq!(fs::read(source).unwrap(), original);
    }
    #[test]
    fn unusable_cache_falls_back_to_source_without_modification() {
        let directory = tempfile::tempdir().unwrap();
        let source = directory.path().join("original.png");
        fs::write(&source, "immutable source").unwrap();
        fs::write(
            directory.path().join("previews-rust-v1"),
            "unusable cache directory",
        )
        .unwrap();
        let previews = Previews::new();
        assert_eq!(
            previews.reference(directory.path(), source.clone(), 1440),
            source
        );
        assert_eq!(fs::read_to_string(source).unwrap(), "immutable source");
    }
}

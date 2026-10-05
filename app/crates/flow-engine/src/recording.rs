use crate::operation::Work;
use flow_capture_windows::{Area, CaptureContext, Metrics, RawFrame, Screen, Session};
use flow_core::project::{Frame, Project, Result, Step, atomic_json};
use serde_json::{Value, json};
use std::{
    collections::BTreeMap,
    fs::{self, OpenOptions},
    io::Write,
    path::Path,
    sync::{Arc, Mutex, atomic::Ordering, mpsc},
    thread,
    time::{Duration, Instant},
};

pub fn record(
    root: &Path,
    title: &str,
    context: &str,
    screen: Screen,
    area: Area,
    work: Work,
) -> Result<Value> {
    record_with_backend(root, title, context, screen, area, work, Backend::Native)
}
enum Backend {
    Native,
    #[cfg(test)]
    Fake(usize, Option<Fault>),
}
#[cfg(test)]
#[derive(Clone, Copy, PartialEq)]
enum Fault {
    Encoding,
    Journal,
    Finalization,
}
enum Capture {
    Native(Session),
    #[cfg(test)]
    Fake(thread::JoinHandle<()>, Arc<std::sync::atomic::AtomicBool>),
}
impl Capture {
    fn start(context: CaptureContext, backend: Backend) -> Result<Self> {
        match backend {
            Backend::Native => Ok(Self::Native(Session::start(context)?)),
            #[cfg(test)]
            Backend::Fake(count, _) => {
                let stop = Arc::new(std::sync::atomic::AtomicBool::new(false));
                let cancel = stop.clone();
                let handle = thread::spawn(move || {
                    for sequence in 0..count {
                        let mut pixels = loop {
                            if cancel.load(Ordering::Relaxed) {
                                return;
                            }
                            match context
                                .free
                                .lock()
                                .unwrap()
                                .recv_timeout(Duration::from_millis(10))
                            {
                                Ok(pixels) => break pixels,
                                Err(mpsc::RecvTimeoutError::Timeout) => continue,
                                Err(_) => return,
                            }
                        };
                        pixels.fill(sequence as u8);
                        let descriptor = flow_capture_windows::descriptor(
                            &pixels,
                            context.area.width,
                            context.area.height,
                        );
                        if context
                            .sender
                            .send(RawFrame {
                                sequence: sequence as u64,
                                time_ms: sequence as u64 * 125,
                                pixels,
                                descriptor,
                            })
                            .is_err()
                        {
                            break;
                        }
                        let mut metrics = context.metrics.lock().unwrap();
                        metrics.samples += 1;
                        metrics.accepted += 1;
                    }
                });
                Ok(Self::Fake(handle, stop))
            }
        }
    }
    fn is_finished(&self) -> bool {
        match self {
            Self::Native(session) => session.is_finished(),
            #[cfg(test)]
            Self::Fake(thread, _) => thread.is_finished(),
        }
    }
    fn stop(self) -> Result<()> {
        match self {
            Self::Native(session) => session.stop(),
            #[cfg(test)]
            Self::Fake(thread, stop) => {
                stop.store(true, Ordering::Relaxed);
                thread
                    .join()
                    .map_err(|_| "Fake capture thread failed".into())
            }
        }
    }
}
fn record_with_backend(
    root: &Path,
    title: &str,
    context: &str,
    screen: Screen,
    area: Area,
    work: Work,
    backend: Backend,
) -> Result<Value> {
    #[cfg(test)]
    let fault = match &backend {
        Backend::Fake(_, fault) => *fault,
        _ => None,
    };
    work.check()?;
    area.validate(&screen)?;
    if fs2::available_space(root)? < 256 * 1024 * 1024 {
        return Err("Storage reserve is below 256 MiB".into());
    }
    let id = format!(
        "{}-{}",
        chrono::Local::now().format("%Y%m%d-%H%M%S"),
        &uuid::Uuid::new_v4().simple().to_string()[..8]
    );
    let directory = root.join("sessions").join(&id);
    fs::create_dir_all(directory.join("frames"))?;
    let mut project: Project = serde_json::from_value(
        json!({"title":title,"context":context,"width":area.width,"height":area.height}),
    )?;
    atomic_json(&directory.join("project.json"), &project)?;
    work.recording(&id);
    let mut journal = OpenOptions::new()
        .create_new(true)
        .write(true)
        .open(directory.join("frames.jsonl"))?;
    let bytes = area.width as usize * area.height as usize * 4;
    let (free_tx, free_rx) = mpsc::sync_channel(2);
    for _ in 0..2 {
        free_tx.send(vec![0; bytes])?;
    }
    let (capture_tx, capture_rx) = mpsc::sync_channel::<RawFrame>(2);
    let input = Arc::new(Mutex::new(capture_rx));
    let (encoded_tx, encoded_rx) = mpsc::sync_channel::<Result<RawFrame>>(2);
    let mut encoders = Vec::new();
    for _ in 0..2 {
        let input = input.clone();
        let output = encoded_tx.clone();
        let frames = directory.join("frames");
        let area = area.clone();
        encoders.push(thread::spawn(move || {
            loop {
                let received = input.lock().unwrap().recv();
                let Ok(raw) = received else {
                    break;
                };
                let result = (|| -> Result<RawFrame> {
                    #[cfg(test)]
                    if fault == Some(Fault::Encoding) && raw.sequence == 1 {
                        return Err(std::io::Error::from(std::io::ErrorKind::StorageFull).into());
                    }
                    let mut temporary = tempfile::NamedTempFile::new_in(&frames)?;
                    encode_frame(&mut temporary, area.width, area.height, &raw.pixels)?;
                    temporary.as_file().sync_all()?;
                    temporary.persist(frames.join(format!("{:07}.png", raw.sequence)))?;
                    Ok(raw)
                })();
                let failed = result.is_err();
                if output.send(result).is_err() || failed {
                    break;
                }
            }
        }));
    }
    drop(encoded_tx);
    let metrics = Arc::new(Mutex::new(Metrics {
        pool_bytes: (bytes * 3) as u64,
        ..Default::default()
    }));
    let started = Instant::now();
    let native = matches!(backend, Backend::Native);
    let mut session = match Capture::start(
        CaptureContext {
            screen: screen.clone(),
            area,
            sender: capture_tx,
            free: Arc::new(Mutex::new(free_rx)),
            recycle: free_tx.clone(),
            metrics: metrics.clone(),
        },
        backend,
    ) {
        Ok(session) => Some(session),
        Err(error) => {
            project.error = error.to_string();
            None
        }
    };
    work.update("recording", 0, "Recording · Ctrl+Shift+F9 to stop");
    let mut pending = BTreeMap::new();
    let mut capture_end = None;
    let mut last_check = Instant::now();
    let mut last_preview = Instant::now();
    loop {
        if session.is_some() && (work.cancel.load(Ordering::Relaxed) || !project.error.is_empty()) {
            work.update("finalizing", 0, "Saving accepted frames…");
            capture_end = Some(started.elapsed().as_millis() as u64);
            if let Err(error) = session.take().unwrap().stop() {
                project.error = error.to_string();
            }
        }
        if last_check.elapsed() >= Duration::from_secs(1) && session.is_some() {
            last_check = Instant::now();
            if fs2::available_space(&directory).unwrap_or(0) < 256 * 1024 * 1024 {
                project.error = "Storage reserve reached; recording stopped".into();
            }
            if native
                && !flow_capture_windows::screens()
                    .unwrap_or_default()
                    .contains(&screen)
            {
                project.error = "Display disconnected or changed resolution".into();
            }
            if session.as_ref().is_some_and(Capture::is_finished) {
                project.error = "Native capture ended; available frames are retained".into();
            }
            if let Err(error) = write_journal(
                &mut journal,
                &json!({"duration_ms":started.elapsed().as_millis() as u64}),
            ) {
                project.error = error.to_string();
            }
            let stats = metrics.lock().unwrap().clone();
            work.update(
                "recording",
                0,
                &format!(
                    "Recording · {} samples · {} saved · {} delayed",
                    stats.samples,
                    project.frames.len(),
                    stats.missed
                ),
            );
        }
        match encoded_rx.recv_timeout(Duration::from_millis(50)) {
            Ok(Ok(raw)) => {
                pending.insert(raw.sequence, raw);
            }
            Ok(Err(error)) => {
                project.error = error.to_string();
            }
            Err(mpsc::RecvTimeoutError::Timeout) => continue,
            Err(mpsc::RecvTimeoutError::Disconnected) => break,
        }
        while let Some(raw) = pending.remove(&(project.frames.len() as u64)) {
            #[cfg(test)]
            if fault == Some(Fault::Journal) && raw.sequence == 1 {
                journal.write_all(b"{\"file\":")?;
                journal.sync_all()?;
                journal = std::fs::File::open(directory.join("frames.jsonl"))?;
            }
            let frame = Frame {
                file: format!("frames/{:07}.png", raw.sequence),
                time_ms: raw.time_ms,
            };
            if let Err(error) = write_journal(&mut journal, &serde_json::to_value(&frame)?) {
                project.error = error.to_string();
                break;
            }
            // Disposable cursor-free index. Failure cannot prevent evidence persistence.
            let cache = directory.join("derived-rust-v1");
            let _ = fs::create_dir_all(&cache);
            let _ = fs::write(
                cache.join(format!("{:07}.descriptor", raw.sequence)),
                &raw.descriptor,
            );
            project.frames.push(frame);
            if project.frames.len() == 1 || last_preview.elapsed() >= Duration::from_millis(500) {
                // The PNG and its journal record are durable before the UI sees this reference.
                let path = directory.join(&project.frames.last().unwrap().file);
                work.capture_preview(&path, project.width, project.height);
                last_preview = Instant::now();
            }
            let _ = free_tx.try_send(raw.pixels);
        }
    }
    if let Some(session) = session {
        let _ = session.stop();
    }
    for encoder in encoders {
        let _ = encoder.join();
    }
    project.duration_ms = capture_end
        .unwrap_or(started.elapsed().as_millis() as u64)
        .max(project.frames.last().map_or(0, |f| f.time_ms) + 10);
    let _ = write_journal(&mut journal, &json!({"duration_ms":project.duration_ms}));
    #[cfg(test)]
    if fault == Some(Fault::Finalization) {
        return Err(std::io::Error::from(std::io::ErrorKind::StorageFull).into());
    }
    project.state = "ready".into();
    if !project.frames.is_empty() {
        project.steps.push(Step::new(0));
        if project.frames.len() > 1 {
            project.steps.push(Step::new(project.frames.len() - 1));
        }
    }
    project.save(&directory)?;
    let stats = metrics.lock().unwrap().clone();
    let _ = atomic_json(&directory.join("capture-metrics.json"), &stats);
    Ok(
        json!({"recording_id":id,"frames":project.frames.len(),"duration_ms":project.duration_ms,"metrics":stats,"warning":project.error}),
    )
}

const PNG_CHUNK_BYTES: usize = 64 * 1024;

fn encode_frame(output: impl Write, width: u32, height: u32, pixels: &[u8]) -> Result<()> {
    let mut encoder = png::Encoder::new(output, width, height);
    encoder.set_color(png::ColorType::Rgba);
    encoder.set_depth(png::BitDepth::Eight);
    encoder.set_compression(png::Compression::Fast);
    let mut writer = encoder.write_header()?;
    // Keep compressed output bounded instead of retaining a full encoded image
    // per worker. Finish explicitly so disk/compression failures precede publish.
    {
        let mut stream = writer.stream_writer_with_size(PNG_CHUNK_BYTES)?;
        stream.write_all(pixels)?;
        stream.finish()?;
    }
    writer.finish()?;
    Ok(())
}

fn write_journal(file: &mut std::fs::File, value: &Value) -> Result<()> {
    serde_json::to_writer(&mut *file, value)?;
    file.write_all(b"\n")?;
    file.flush()?;
    file.sync_all()?;
    Ok(())
}

#[cfg(test)]
mod tests {
    #[test]
    fn live_preview_references_only_a_published_frame() {
        let root = tempfile::tempdir().unwrap();
        let path = root.path().join("preview.png");
        let worker_path = path.clone();
        let op = crate::operation::Operation::start("preparing_capture", None, 1, move |work| {
            std::fs::write(&worker_path, b"published image")?;
            work.capture_preview(&worker_path, 128, 96);
            while work.check().is_ok() {
                std::thread::sleep(std::time::Duration::from_millis(1));
            }
            work.check()?;
            Ok(serde_json::json!({}))
        });
        let deadline = std::time::Instant::now() + std::time::Duration::from_secs(2);
        while op.status().result.is_none() {
            assert!(std::time::Instant::now() < deadline);
            std::thread::sleep(std::time::Duration::from_millis(1));
        }
        let preview = op.status().result.unwrap();
        assert_eq!(preview["capture_preview"].as_str(), path.to_str());
        assert!(path.is_file());
        assert_eq!(preview["capture_width"], 128);
        op.cancel();
    }
    use super::*;

    #[test]
    #[ignore = "Opt-in encoder heap accounting; run scripts/rebuild_encoder_memory.ps1"]
    fn encoder_heap_is_bounded_on_noisy_full_display_frames() {
        let root = std::path::PathBuf::from(
            std::env::var_os("FLOW_ENCODER_MEMORY_ROOT").expect("Isolated output root required"),
        );
        assert!(root.join(".flow-recorder-development").is_file());
        let mut results = Vec::new();
        for (width, height) in [(640, 480), (2560, 100), (2560, 1600)] {
            for noisy in [false, true] {
                let mut state = 0x12345678u32;
                let pixels: Vec<u8> = (0..width * height * 4)
                    .map(|_| {
                        state ^= state << 13;
                        state ^= state >> 17;
                        state ^= state << 5;
                        if noisy { state as u8 } else { 128 }
                    })
                    .collect();
                for repeat in 0..3 {
                    let started = Instant::now();
                    let stats = crate::allocation_probe::measure(|| {
                        encode_frame(std::io::sink(), width, height, &pixels).unwrap();
                    });
                    assert_eq!(
                        stats.live_bytes, 0,
                        "Encoder allocations escaped measurement scope"
                    );
                    assert!(stats.peak_bytes > 0);
                    assert!(
                        stats.peak_bytes <= 256 * 1024,
                        "Unbounded encoder heap: {stats:?}"
                    );
                    results.push(json!({"width":width,"height":height,"noisy":noisy,"repeat":repeat,"elapsed_ms":started.elapsed().as_secs_f64()*1000.0,"rust_heap":stats}));
                }
            }
        }
        atomic_json(&root.join("encoder-memory.json"), &json!({"passed":true,"results":results,"per_worker_heap_limit_bytes":256*1024,"workers":2,"limitations":"Rust requested heap bytes during encoding only. Input pixels allocated before scope; output discarded. Excludes OS/allocator overhead, stack, disk I/O, capture staging and GPU allocations. Timings include test allocator accounting."})).unwrap();
    }

    #[test]
    fn noisy_png_round_trips_with_bounded_output_chunks() {
        struct BoundedOutput(Vec<u8>);
        impl Write for BoundedOutput {
            fn write(&mut self, bytes: &[u8]) -> std::io::Result<usize> {
                assert!(
                    bytes.len() <= PNG_CHUNK_BYTES,
                    "whole-image output allocation"
                );
                self.0.extend_from_slice(bytes);
                Ok(bytes.len())
            }
            fn flush(&mut self) -> std::io::Result<()> {
                Ok(())
            }
        }
        let mut state = 0x12345678u32;
        let pixels: Vec<u8> = (0..640 * 480 * 4)
            .map(|_| {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                state as u8
            })
            .collect();
        let mut output = BoundedOutput(Vec::new());
        encode_frame(&mut output, 640, 480, &pixels).unwrap();
        assert!(output.0.len() > PNG_CHUNK_BYTES * 10);
        let decoded = image::load_from_memory(&output.0).unwrap().to_rgba8();
        assert_eq!(decoded.dimensions(), (640, 480));
        assert_eq!(decoded.as_raw(), &pixels);
    }

    #[test]
    fn streaming_png_reports_output_and_incomplete_input_errors() {
        struct FailedOutput(usize);
        impl Write for FailedOutput {
            fn write(&mut self, bytes: &[u8]) -> std::io::Result<usize> {
                if self.0 == 0 {
                    return Err(std::io::ErrorKind::StorageFull.into());
                }
                let written = bytes.len().min(self.0);
                self.0 -= written;
                Ok(written)
            }
            fn flush(&mut self) -> std::io::Result<()> {
                Ok(())
            }
        }
        // Fail both the signature and the image payload after a complete IHDR.
        for budget in [0, 33] {
            assert!(encode_frame(FailedOutput(budget), 16, 16, &[0; 1024]).is_err());
        }
        assert!(encode_frame(std::io::sink(), 16, 16, &[0; 1023]).is_err());
        assert!(encode_frame(std::io::sink(), 16, 16, &[0; 1025]).is_err());
    }

    #[test]
    #[ignore = "Opt-in synthetic endurance; run scripts/rebuild_endurance_check.ps1"]
    fn synthetic_capture_endurance() {
        let root =
            std::env::var_os("FLOW_ENDURANCE_ROOT").expect("Isolated endurance root required");
        let root = std::path::PathBuf::from(root);
        assert!(root.join(".flow-recorder-development").is_file());
        assert!(
            !root.join("sessions").exists(),
            "Use a fresh evidence directory"
        );
        let path = root.clone();
        let operation = crate::operation::Operation::start("recording", None, 0, move |work| {
            record_with_backend(
                &path,
                "Synthetic endurance",
                "No desktop capture",
                Screen {
                    id: "fake".into(),
                    name: "fake".into(),
                    left: 0,
                    top: 0,
                    width: 2560,
                    height: 1600,
                },
                Area {
                    left: 0,
                    top: 0,
                    width: 2560,
                    height: 1600,
                },
                work,
                Backend::Fake(4800, None),
            )
        });
        let started = Instant::now();
        while !operation.status().finished {
            if started.elapsed() > Duration::from_secs(300) {
                operation.cancel();
                panic!("Synthetic endurance exceeded five minutes");
            }
            thread::sleep(Duration::from_millis(50));
        }
        let status = operation.status();
        assert_eq!(status.state, "completed", "{:?}", status.error);
        let directory = root.join("sessions").join(status.recording_id.unwrap());
        let project = Project::load(&directory).unwrap();
        assert_eq!(project.frames.len(), 4800);
        assert_eq!(project.state, "ready");
        assert!(project.error.is_empty(), "{}", project.error);
        for (index, frame) in project.frames.iter().enumerate() {
            assert_eq!(frame.time_ms, index as u64 * 125);
            assert_eq!(frame.file, format!("frames/{index:07}.png"));
            assert!(project.image_path(&directory, index).unwrap().is_file());
        }
        let metrics: Value =
            serde_json::from_slice(&fs::read(directory.join("capture-metrics.json")).unwrap())
                .unwrap();
        assert_eq!(metrics["accepted"], 4800);
        assert_eq!(metrics["pool_bytes"], 2560 * 1600 * 4 * 3);
        let journal = fs::read_to_string(directory.join("frames.jsonl")).unwrap();
        assert_eq!(
            journal
                .lines()
                .filter(|line| line.contains("\"file\""))
                .count(),
            4800
        );
        atomic_json(&root.join("endurance-result.json"), &json!({
            "passed":true, "frames":4800, "width":2560, "height":1600,
            "elapsed_seconds":started.elapsed().as_secs_f64(), "metrics":metrics,
            "limitations":"Synthetic uniform-color frames, unpaced producer. No WGC, cursor, GPU, native cadence or review-cache claim."
        })).unwrap();
    }

    #[test]
    fn fake_capture_drains_in_order_and_can_be_reopened() {
        let root = tempfile::tempdir().unwrap();
        let path = root.path().to_owned();
        let op = crate::operation::Operation::start("recording", None, 0, move |work| {
            record_with_backend(
                &path,
                "Synthetic capture",
                "",
                Screen {
                    id: "fake".into(),
                    name: "fake".into(),
                    left: 0,
                    top: 0,
                    width: 128,
                    height: 96,
                },
                Area {
                    left: 0,
                    top: 0,
                    width: 128,
                    height: 96,
                },
                work,
                Backend::Fake(50, None),
            )
        });
        let deadline = Instant::now() + Duration::from_secs(20);
        while !op.status().finished {
            assert!(Instant::now() < deadline);
            thread::sleep(Duration::from_millis(10));
        }
        let status = op.status();
        assert_eq!(status.state, "completed", "{:?}", status.error);
        let directory = root
            .path()
            .join("sessions")
            .join(status.recording_id.unwrap());
        let project = Project::load(&directory).unwrap();
        assert_eq!(project.frames.len(), 50);
        assert_eq!(project.state, "ready");
        assert!(
            project
                .frames
                .windows(2)
                .all(|p| p[0].time_ms < p[1].time_ms)
        );
        for (index, _) in project.frames.iter().enumerate() {
            let image = image::open(project.image_path(&directory, index).unwrap())
                .unwrap()
                .to_rgba8();
            assert_eq!(image.get_pixel(0, 0).0[0], index as u8);
        }
        let metrics: Value =
            serde_json::from_slice(&fs::read(directory.join("capture-metrics.json")).unwrap())
                .unwrap();
        assert_eq!(metrics["accepted"], 50);
        assert_eq!(metrics["pool_bytes"], 128 * 96 * 4 * 3);
        assert!(metrics["wgc_surface_payload_bytes"].is_null());
        assert!(metrics["staging_mapped_peak_bytes"].is_null());
        let journal = fs::read_to_string(directory.join("frames.jsonl")).unwrap();
        assert_eq!(
            journal.lines().filter(|l| l.contains("\"file\"")).count(),
            50
        );
    }

    #[test]
    fn capture_failures_stop_workers_and_preserve_recoverable_prefix() {
        for fault in [Fault::Encoding, Fault::Journal, Fault::Finalization] {
            let root = tempfile::tempdir().unwrap();
            let path = root.path().to_owned();
            let operation = crate::operation::Operation::start("recording", None, 0, move |work| {
                record_with_backend(
                    &path,
                    "Fault fixture",
                    "",
                    Screen {
                        id: "fake".into(),
                        name: "fake".into(),
                        left: 0,
                        top: 0,
                        width: 128,
                        height: 96,
                    },
                    Area {
                        left: 0,
                        top: 0,
                        width: 128,
                        height: 96,
                    },
                    work,
                    Backend::Fake(20, Some(fault)),
                )
            });
            let deadline = Instant::now() + Duration::from_secs(5);
            while !operation.status().finished {
                assert!(Instant::now() < deadline);
                thread::sleep(Duration::from_millis(10));
            }
            let status = operation.status();
            let directory = root
                .path()
                .join("sessions")
                .join(status.recording_id.unwrap());
            let before: Vec<_> = fs::read_dir(directory.join("frames"))
                .unwrap()
                .map(|entry| {
                    let path = entry.unwrap().path();
                    let bytes = fs::read(&path).unwrap();
                    (path, bytes)
                })
                .collect();
            let mut project = Project::load(&directory).unwrap();
            if fault == Fault::Finalization {
                assert_eq!(status.state, "failed");
                assert_eq!(project.state, "recording");
                assert!(project.recover(&directory).unwrap());
                assert_eq!(project.frames.len(), 20);
            } else {
                assert_eq!(status.state, "completed");
                assert!(!project.error.is_empty());
                assert_eq!(project.frames.len(), 1);
            }
            for (path, bytes) in before {
                assert_eq!(fs::read(path).unwrap(), bytes);
            }
            assert_eq!(project.state, "ready");
            assert!(!project.recover(&directory).unwrap());
        }
    }
}

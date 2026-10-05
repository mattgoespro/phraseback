use crate::{child::OwnedChild, operation::Work};
use flow_core::project::{Project, Result};
use serde_json::{Value, json};
use std::{
    fs,
    io::Write,
    path::{Path, PathBuf},
    process::{Command, Stdio},
    thread,
    time::Duration,
};

#[cfg(test)]
use flow_core::prompt::literal;
fn safe_name(title: &str) -> String {
    let mut name: String = title
        .chars()
        .filter(|c| c.is_alphanumeric() || ['_', ' ', '-'].contains(c))
        .collect::<String>()
        .trim()
        .chars()
        .take(60)
        .collect();
    if name.is_empty() {
        name = "Desktop workflow".into();
    }
    let upper = name.to_uppercase();
    if ["CON", "PRN", "AUX", "NUL"].contains(&upper.as_str())
        || (1..=9).any(|i| upper == format!("COM{i}") || upper == format!("LPT{i}"))
    {
        name = format!("Workflow {name}");
    }
    name
}
fn run(command: &mut Command, work: &Work) -> Result<()> {
    work.check()?;
    // No screenshot paths, descriptions, tokens or prompts in diagnostic logs.
    let mut process = OwnedChild::spawn(command.stdout(Stdio::null()).stderr(Stdio::null()))?;
    loop {
        work.check()?;
        if let Some(status) = process.process.try_wait()? {
            return if status.success() {
                Ok(())
            } else {
                Err(
                    "GIF encoder failed; original recording and existing exports are unchanged"
                        .into(),
                )
            };
        }
        thread::sleep(Duration::from_millis(50));
    }
}
fn encoder_path(path: &Path) -> PathBuf {
    // Rust canonical paths use the Win32 verbatim prefix. FFmpeg's URL parser
    // interprets that prefix as a protocol; give it an ordinary absolute path.
    #[cfg(windows)]
    {
        let value = path.to_string_lossy();
        if let Some(unc) = value.strip_prefix(r"\\?\UNC\") {
            return PathBuf::from(format!(r"\\{unc}"));
        }
        if let Some(local) = value.strip_prefix(r"\\?\") {
            return PathBuf::from(local);
        }
    }
    path.to_owned()
}
pub fn concat(project: &Project) -> Result<(String, u64)> {
    if project.frames.is_empty() {
        return Err("This recording has no recoverable frames".into());
    }
    let mut lines = String::from("ffconcat version 1.0\n");
    let mut final_hold = 10;
    for (i, frame) in project.frames.iter().enumerate() {
        let digits = frame
            .file
            .strip_prefix("frames/")
            .and_then(|s| s.strip_suffix(".png"))
            .ok_or("Unexpected recording frame filename")?;
        if digits.len() != 7 || !digits.bytes().all(|b| b.is_ascii_digit()) {
            return Err("Unexpected recording frame filename".into());
        }
        let end = project
            .frames
            .get(i + 1)
            .map_or(project.duration_ms, |f| f.time_ms);
        let mut remaining = end.saturating_sub(frame.time_ms).max(10);
        while remaining > 0 {
            final_hold = remaining.min(60000);
            lines.push_str(&format!(
                "file '{}'\noption framerate 1000\nduration {:.3}\n",
                frame.file,
                final_hold as f64 / 1000.0
            ));
            remaining -= final_hold;
        }
    }
    Ok((lines, ((final_hold as f64 / 10.0).round() as u64).max(1)))
}
pub fn run_export_template(
    project: Project,
    directory: &Path,
    parent: &Path,
    ffmpeg: &Path,
    work: Work,
    template: &str,
) -> Result<Value> {
    run_export_with_encoder(project, directory, parent, ffmpeg, work, template, run)
}

fn run_export_with_encoder(
    project: Project,
    directory: &Path,
    parent: &Path,
    ffmpeg: &Path,
    work: Work,
    template: &str,
    mut encode: impl FnMut(&mut Command, &Work) -> Result<()>,
) -> Result<Value> {
    work.check()?;
    if !ffmpeg.is_file() {
        return Err(
            "The bundled GIF encoder is missing; repair the application installation".into(),
        );
    }
    fs::create_dir_all(parent)?;
    let parent = parent.canonicalize()?;
    if fs2::available_space(&parent)? < 100 * 1024 * 1024 {
        return Err("Insufficient free space for export".into());
    }
    for index in 0..project.frames.len() {
        work.check()?;
        project.image_path(directory, index)?;
    }
    let stage = tempfile::Builder::new()
        .prefix(".flow-export-")
        .tempdir_in(&parent)?;
    let (lines, delay) = concat(&project)?;
    let mut manifest = tempfile::Builder::new()
        .prefix(".flow-export-")
        .suffix(".ffconcat")
        .tempfile_in(directory)?;
    manifest.write_all(lines.as_bytes())?;
    manifest.flush()?;
    let palette = stage.path().join("palette.png");
    let gif = stage.path().join("recording.gif");
    let common = || {
        let mut command = Command::new(ffmpeg);
        command
            .args([
                "-hide_banner",
                "-loglevel",
                "error",
                "-y",
                "-f",
                "concat",
                "-safe",
                "0",
                "-i",
            ])
            .arg(encoder_path(manifest.path()));
        command
    };
    work.update("exporting", 15, "Building GIF colour palette…");
    encode(
        common()
            .args([
                "-vf",
                // FFmpeg 7.1's diff mode counts the previous frame's changed
                // pixels and never adds new colours from the final frame.
                // Include every evidence frame in the global palette instead.
                "palettegen=stats_mode=full",
                "-frames:v",
                "1",
                "-update",
                "1",
            ])
            .arg(encoder_path(&palette)),
        &work,
    )?;
    work.update("exporting", 45, "Encoding full-resolution GIF…");
    encode(
        common()
            .arg("-i")
            .arg(encoder_path(&palette))
            .args([
                "-lavfi",
                "paletteuse=dither=sierra2_4a",
                "-fps_mode",
                "vfr",
                "-loop",
                "0",
                "-final_delay",
            ])
            .arg(delay.to_string())
            .arg(encoder_path(&gif)),
        &work,
    )?;
    fs::remove_file(palette)?;
    fs::create_dir(stage.path().join("screenshots"))?;
    let mut ordered: Vec<_> = project.steps.iter().collect();
    ordered.sort_by_key(|step| step.frame);
    for (index, step) in ordered.iter().enumerate() {
        work.check()?;
        let relative = format!("screenshots/{:03}.png", index + 1);
        fs::copy(
            project.image_path(directory, step.frame)?,
            stage.path().join(relative),
        )?;
    }
    let lines = flow_core::prompt::render(&project, directory, template, true)?;
    fs::write(stage.path().join("transcript.md"), lines)?;
    work.check()?;
    let name = safe_name(&project.title);
    let mut destination = parent.join(&name);
    let mut suffix = 2;
    while destination.exists() {
        destination = parent.join(format!("{name} ({suffix})"));
        suffix += 1;
    }
    // Rename never replaces an existing nonempty export. Windows also rejects an
    // existing empty destination; portable release publication is validated on Windows.
    for attempt in 0..10 {
        work.check()?;
        match fs::rename(stage.path(), &destination) {
            Ok(()) => return Ok(json!({"destination":destination})),
            Err(error)
                if cfg!(windows)
                    && matches!(error.raw_os_error(), Some(5 | 32 | 33))
                    && attempt < 9
                    && !destination.exists() =>
            {
                thread::sleep(Duration::from_millis(50))
            }
            Err(error) => return Err(error.into()),
        }
    }
    unreachable!()
}
pub fn bundled_encoder() -> Result<PathBuf> {
    Ok(std::env::current_exe()?
        .parent()
        .ok_or("Engine location unavailable")?
        .join("ffmpeg.exe"))
}
#[cfg(test)]
mod tests {
    use super::*;
    use crate::operation::Operation;
    use std::{sync::atomic::Ordering, time::Instant};

    #[test]
    fn cancellation_and_encoder_failure_never_publish_partial_exports() {
        for cancel in [false, true] {
            let root = tempfile::tempdir().unwrap();
            let recording = root.path().join("recording");
            fs::create_dir_all(recording.join("frames")).unwrap();
            image::RgbaImage::new(2, 2)
                .save(recording.join("frames/0000000.png"))
                .unwrap();
            let original = fs::read(recording.join("frames/0000000.png")).unwrap();
            let parent = root.path().join("Unicode é and apostrophe's exports");
            fs::create_dir_all(parent.join("Existing")).unwrap();
            fs::write(parent.join("Existing/keep.txt"), b"unchanged").unwrap();
            let project: Project = serde_json::from_value(json!({"title":"Existing",
                "frames":[{"file":"frames/0000000.png","time_ms":0}],"duration_ms":125,
                "steps":[{"frame":0,"id":"manual","action":"Protected text","manual":true}]}))
            .unwrap();
            let worker_directory = recording.clone();
            let worker_parent = parent.clone();
            let operation = Operation::start("exporting", None, 1, move |work| {
                let mut calls = 0;
                run_export_with_encoder(
                    project,
                    &worker_directory,
                    &worker_parent,
                    &std::env::current_exe()?,
                    work,
                    flow_core::prompt::DEFAULT_TEMPLATE,
                    |command, work| {
                        calls += 1;
                        fs::write(
                            Path::new(command.get_args().last().unwrap()),
                            b"fake encoded data",
                        )?;
                        if calls == 2 {
                            if cancel {
                                work.cancel.store(true, Ordering::Relaxed);
                            } else {
                                return Err("Injected encoder failure".into());
                            }
                        }
                        Ok(())
                    },
                )
            });
            let deadline = Instant::now() + Duration::from_secs(5);
            while !operation.status().finished {
                assert!(Instant::now() < deadline);
                thread::sleep(Duration::from_millis(10));
            }
            assert_eq!(
                operation.status().state,
                if cancel { "cancelled" } else { "failed" }
            );
            assert_eq!(
                fs::read(parent.join("Existing/keep.txt")).unwrap(),
                b"unchanged"
            );
            assert_eq!(
                fs::read_dir(&parent).unwrap().count(),
                1,
                "Staging directory leaked"
            );
            assert_eq!(
                fs::read_dir(&recording).unwrap().count(),
                1,
                "Concat manifest leaked"
            );
            assert_eq!(
                fs::read(recording.join("frames/0000000.png")).unwrap(),
                original
            );
        }
    }
    #[test]
    fn markdown_is_literal_and_names_are_safe() {
        assert_eq!(literal("<img>[x](a)*"), "&lt;img&gt;\\[x\\]\\(a\\)\\*");
        assert_eq!(safe_name("CON"), "Workflow CON");
        assert_eq!(safe_name("../"), "Desktop workflow");
    }
    #[test]
    fn held_states_split_without_losing_time() {
        let project: Project = serde_json::from_value(
            json!({"frames":[{"file":"frames/0000000.png","time_ms":0}],"duration_ms":125125}),
        )
        .unwrap();
        let (text, delay) = concat(&project).unwrap();
        assert_eq!(text.matches("file '").count(), 3);
        assert!(text.contains("duration 5.125"));
        assert_eq!(delay, 513);
    }
}

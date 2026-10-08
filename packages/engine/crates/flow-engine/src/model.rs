use crate::{child::OwnedChild, operation::Work};
use flow_core::project::{Result, atomic_json};
use serde::{Deserialize, Serialize};
use serde_json::{Value, json};
use sha2::{Digest, Sha256};
use std::{
    fs::{self, File, OpenOptions},
    io::{Read, Write},
    net::TcpListener,
    path::{Path, PathBuf},
    process::{Command, Stdio},
    sync::{Arc, Mutex},
    thread,
    time::{Duration, Instant},
};

#[derive(Clone, Serialize, Deserialize)]
pub struct Asset {
    pub name: String,
    pub url: String,
    pub size: u64,
    pub sha256: String,
}
#[derive(Clone, Serialize, Deserialize)]
pub struct Preset {
    pub id: String,
    pub title: String,
    pub model_id: String,
    pub revision: String,
    pub gpu: bool,
    pub ram_gb: u64,
    pub suggested_vram_gb: u64,
    pub assets: Vec<Asset>,
}
pub fn presets() -> Vec<Preset> {
    serde_json::from_str(include_str!("../../../contracts/model-presets.json"))
        .expect("pinned manifests")
}
pub fn preset(id: &str) -> Result<Preset> {
    presets()
        .into_iter()
        .find(|p| p.id == id)
        .ok_or_else(|| "Unknown model preset".into())
}
impl Preset {
    pub fn directory(&self, root: &Path) -> PathBuf {
        if self.model_id == "qwen2" {
            root.join("model")
        } else {
            root.join("models").join(&self.model_id)
        }
    }
    pub fn runtime(&self, root: &Path) -> PathBuf {
        self.directory(root).join(if self.gpu {
            "runtime-vulkan"
        } else {
            "runtime"
        })
    }
}
pub struct Runtime {
    pub child: OwnedChild,
    pub port: u16,
    pub token: String,
    pub preset: String,
    pub used: Instant,
}
#[derive(Clone)]
pub struct Models {
    pub root: PathBuf,
    pub warm: Arc<Mutex<Option<Runtime>>>,
    pub state: Arc<Mutex<String>>,
}
impl Models {
    pub fn new(root: PathBuf) -> Self {
        let model = Self {
            root,
            warm: Arc::new(Mutex::new(None)),
            state: Arc::new(Mutex::new("unloaded".into())),
        };
        let weak = Arc::downgrade(&model.warm);
        let state = Arc::downgrade(&model.state);
        thread::spawn(move || {
            loop {
                thread::sleep(Duration::from_secs(1));
                let Some(warm) = weak.upgrade() else {
                    break;
                };
                let Ok(mut runtime) = warm.try_lock() else {
                    continue;
                };
                if runtime.as_mut().is_some_and(|r| {
                    r.used.elapsed() >= Duration::from_secs(60)
                        || !matches!(r.child.process.try_wait(), Ok(None))
                }) {
                    runtime.take();
                    if let Some(state) = state.upgrade() {
                        *state.lock().unwrap() = "unloaded".into();
                    }
                }
            }
        });
        model
    }
    pub fn selected(&self) -> Preset {
        fs::read(self.root.join("model-settings.json"))
            .ok()
            .and_then(|b| serde_json::from_slice::<Value>(&b).ok())
            .and_then(|v| v["preset"].as_str().and_then(|id| preset(id).ok()))
            .unwrap_or_else(|| presets().remove(0))
    }
    pub fn inspect(&self) -> Value {
        let preset = self.selected();
        let directory = preset.directory(&self.root);
        json!({"presets":presets(),"selected":preset.id,"state":*self.state.lock().unwrap(),
            "assets_present":preset.assets.iter().all(|a|fs::metadata(directory.join(&a.name)).is_ok_and(|m|m.len()==a.size)),
            "verified":directory.join(format!("verified-rust-{}.json",preset.id)).is_file()})
    }
    pub fn release(&self) {
        *self.state.lock().unwrap() = "stopping".into();
        self.warm.lock().unwrap().take();
        *self.state.lock().unwrap() = "unloaded".into();
    }
    pub fn select(&self, id: &str) -> Result<()> {
        let preset = preset(id)?;
        self.release();
        atomic_json(
            &self.root.join("model-settings.json"),
            &json!({"preset":preset.id}),
        )
    }
    pub fn retain(&self, runtime: Runtime) {
        *self.warm.lock().unwrap() = Some(runtime);
        *self.state.lock().unwrap() = "warm".into();
    }
    pub fn take_or_start(&self, preset: &Preset, work: &Work) -> Result<Runtime> {
        work.check()?;
        let existing = self.warm.lock().unwrap().take();
        if let Some(mut runtime) = existing
            && runtime.preset == preset.id
            && runtime.used.elapsed() < Duration::from_secs(60)
            && runtime.child.process.try_wait()?.is_none()
        {
            *self.state.lock().unwrap() = "processing".into();
            return Ok(runtime);
        }
        *self.state.lock().unwrap() = "starting".into();
        let result = start(&self.root, preset, work);
        *self.state.lock().unwrap() = if result.is_ok() {
            "processing"
        } else {
            "unloaded"
        }
        .into();
        result
    }
}
pub fn digest(path: &Path, work: &Work) -> Result<String> {
    let mut file = File::open(path)?;
    let mut digest = Sha256::new();
    let mut buffer = vec![0; 4 * 1024 * 1024];
    loop {
        work.check()?;
        let size = file.read(&mut buffer)?;
        if size == 0 {
            break;
        }
        digest.update(&buffer[..size]);
    }
    Ok(format!("{:x}", digest.finalize()))
}
pub fn contained(root: &Path, path: &Path) -> Result<()> {
    let root = root.canonicalize()?;
    let mut ancestor = path;
    while !ancestor.exists() {
        ancestor = ancestor.parent().ok_or("Invalid model path")?;
    }
    if !ancestor.canonicalize()?.starts_with(root) {
        return Err("Model path is outside the application data folder".into());
    }
    if path.exists() && fs::symlink_metadata(path)?.file_type().is_symlink() {
        return Err("Model path must not be a symbolic link".into());
    }
    Ok(())
}
pub fn verify(root: &Path, preset: &Preset, work: &Work) -> Result<()> {
    let directory = preset.directory(root);
    contained(root, &directory)?;
    for (index, asset) in preset.assets.iter().enumerate() {
        work.update(
            "verifying",
            (index * 100 / preset.assets.len()) as u32,
            "Verifying local model assets…",
        );
        let path = directory.join(&asset.name);
        contained(root, &path)?;
        if fs::metadata(&path)?.len() != asset.size || digest(&path, work)? != asset.sha256 {
            return Err(format!(
                "Model asset failed verification: {}. Repair in Settings.",
                asset.name
            )
            .into());
        }
    }
    extract(root, preset, work)?;
    atomic_json(
        &directory.join(format!("verified-rust-{}.json", preset.id)),
        &json!({"preset":preset.id,"assets":preset.assets,"runtime":"b10985"}),
    )
}
pub fn runtime() -> Result<tokio::runtime::Runtime> {
    Ok(tokio::runtime::Builder::new_current_thread()
        .enable_all()
        .build()?)
}
pub async fn cancellable<T>(
    work: &Work,
    future: impl std::future::Future<Output = Result<T>>,
) -> Result<T> {
    tokio::pin!(future);
    loop {
        tokio::select! { result=&mut future=>return result, _=tokio::time::sleep(Duration::from_millis(50))=>work.check()? }
    }
}
async fn download(asset: &Asset, directory: &Path, work: &Work) -> Result<()> {
    let client = reqwest::Client::builder()
        .https_only(true)
        .connect_timeout(Duration::from_secs(20))
        .read_timeout(Duration::from_secs(20))
        .build()?;
    download_with_client(&client, asset, directory, work).await
}
async fn download_with_client(
    client: &reqwest::Client,
    asset: &Asset,
    directory: &Path,
    work: &Work,
) -> Result<()> {
    let target = directory.join(&asset.name);
    if fs::metadata(&target).is_ok_and(|m| m.len() == asset.size)
        && digest(&target, work)? == asset.sha256
    {
        return Ok(());
    }
    let partial = directory.join(format!("{}.part", asset.name));
    let mut offset = fs::metadata(&partial).map_or(0, |m| m.len());
    if offset > asset.size {
        fs::remove_file(&partial)?;
        offset = 0;
    }
    if offset != asset.size {
        if fs2::available_space(directory)? < asset.size - offset + 256 * 1024 * 1024 {
            return Err("Insufficient free space for a verified model download".into());
        }
        let mut request = client
            .get(&asset.url)
            .header("User-Agent", "Phraseback/0.2");
        if offset > 0 {
            request = request.header("Range", format!("bytes={offset}-"));
        }
        let mut response = cancellable(work, async {
            Ok(request.send().await?.error_for_status()?)
        })
        .await?;
        if response.status() == reqwest::StatusCode::PARTIAL_CONTENT {
            if !response
                .headers()
                .get("Content-Range")
                .and_then(|v| v.to_str().ok())
                .is_some_and(|v| v.starts_with(&format!("bytes {offset}-")))
            {
                return Err("Download range did not match the partial file".into());
            }
        } else {
            offset = 0;
        }
        let mut file = OpenOptions::new()
            .create(true)
            .write(true)
            .append(offset > 0)
            .truncate(offset == 0)
            .open(&partial)?;
        while let Some(chunk) = cancellable(work, async { Ok(response.chunk().await?) }).await? {
            if offset + chunk.len() as u64 > asset.size {
                return Err("Download exceeded the pinned asset size".into());
            }
            file.write_all(&chunk)?;
            offset += chunk.len() as u64;
            work.update(
                "installing",
                (offset * 100 / asset.size) as u32,
                &format!(
                    "Downloading {} · {} / {} MiB",
                    asset.name,
                    offset / 1048576,
                    asset.size / 1048576
                ),
            );
        }
        file.sync_all()?;
    }
    if fs::metadata(&partial)?.len() != asset.size || digest(&partial, work)? != asset.sha256 {
        fs::remove_file(partial)?;
        return Err("Model checksum verification failed; retry to download a fresh copy".into());
    }
    work.check()?;
    // Never replace a legacy asset until its replacement is verified.
    let temporary = tempfile::NamedTempFile::new_in(directory)?;
    fs::rename(&partial, temporary.path())?;
    temporary.persist(target)?;
    Ok(())
}
fn extract(root: &Path, preset: &Preset, work: &Work) -> Result<()> {
    let directory = preset.directory(root);
    let destination = preset.runtime(root);
    contained(root, &destination)?;
    let stage = tempfile::Builder::new()
        .prefix(".runtime-")
        .tempdir_in(&directory)?;
    let mut archive = zip::ZipArchive::new(File::open(directory.join(&preset.assets[2].name))?)?;
    let mut names = std::collections::HashSet::new();
    let mut total = 0;
    for index in 0..archive.len() {
        work.check()?;
        let mut entry = archive.by_index(index)?;
        if entry.is_dir() {
            continue;
        }
        let safe = entry.enclosed_name().ok_or("Unsafe runtime archive path")?;
        let name = safe
            .file_name()
            .ok_or("Invalid runtime filename")?
            .to_owned();
        total += entry.size();
        if total > 512 * 1024 * 1024 || !names.insert(name.clone()) {
            return Err("Unexpected runtime archive contents".into());
        }
        let mut output = File::create(stage.path().join(name))?;
        std::io::copy(&mut entry, &mut output)?;
        output.sync_all()?;
    }
    if !stage.path().join("llama-server.exe").is_file() {
        return Err("Verified runtime is missing llama-server.exe".into());
    }
    if destination.exists() {
        // Existing runtime binaries are disposable, but retain them until the new
        // directory is published. This operation owns all paths below directory.
        let backup = directory.join(format!(".runtime-old-{}", uuid::Uuid::new_v4()));
        fs::rename(&destination, &backup)?;
        if let Err(error) = fs::rename(stage.path(), &destination) {
            let _ = fs::rename(&backup, &destination);
            return Err(error.into());
        }
        let _ = fs::remove_dir_all(backup);
    } else {
        fs::rename(stage.path(), destination)?;
    }
    Ok(())
}
fn environment(command: &mut Command) {
    for (key, _) in std::env::vars_os() {
        if ["LLAMA_", "HF_", "GGML_"]
            .iter()
            .any(|prefix| key.to_string_lossy().starts_with(prefix))
        {
            command.env_remove(key);
        }
    }
}
pub fn devices(output: &str) -> Vec<(String, String, u64)> {
    output
        .lines()
        .filter_map(|line| {
            let (id, rest) = line.trim().split_once(':')?;
            if !id
                .strip_prefix("Vulkan")?
                .bytes()
                .all(|b| b.is_ascii_digit())
            {
                return None;
            }
            let (name, memory) = rest.rsplit_once('(')?;
            let size = memory.split_once(" MiB")?.0.trim().parse().ok()?;
            Some((id.into(), name.trim().into(), size))
        })
        .collect()
}
fn gpu_device(root: &Path, preset: &Preset, work: &Work) -> Result<String> {
    let mut log = tempfile::NamedTempFile::new_in(preset.directory(root))?;
    let mut command = Command::new(preset.runtime(root).join("llama-server.exe"));
    command
        .arg("--list-devices")
        .stdout(log.reopen()?)
        .stderr(Stdio::null());
    environment(&mut command);
    let mut process = OwnedChild::spawn(&mut command)?;
    let started = Instant::now();
    loop {
        work.check()?;
        if let Some(exit) = process.process.try_wait()? {
            if !exit.success() {
                return Err("Vulkan device probe failed; no CPU fallback was used".into());
            }
            break;
        }
        if started.elapsed() > Duration::from_secs(20) {
            return Err("Vulkan device probe timed out".into());
        }
        thread::sleep(Duration::from_millis(50));
    }
    let mut output = String::new();
    log.read_to_string(&mut output)?;
    devices(&output).into_iter().max_by_key(|(_,name,memory)| {let name=name.to_lowercase();(["nvidia","radeon rx","radeon pro","arc(","arc "].iter().any(|tag|name.contains(tag)),*memory)}).map(|d|d.0).ok_or_else(||"No compatible Vulkan GPU found. Select CPU explicitly or update the driver. No CPU fallback was used.".into())
}
pub fn install(root: &Path, preset: &Preset, work: &Work) -> Result<Value> {
    hardware(preset)?;
    let directory = preset.directory(root);
    contained(root, &directory)?;
    fs::create_dir_all(&directory)?;
    let missing = preset
        .assets
        .iter()
        .map(|a| {
            a.size
                .saturating_sub(fs::metadata(directory.join(&a.name)).map_or(0, |m| m.len()))
        })
        .sum::<u64>();
    if fs2::available_space(&directory)? < missing + 512 * 1024 * 1024 {
        return Err("Insufficient disk space to install this preset".into());
    }
    let rt = runtime()?;
    for asset in [&preset.assets[2], &preset.assets[0], &preset.assets[1]] {
        contained(root, &directory.join(&asset.name))?;
        contained(root, &directory.join(format!("{}.part", asset.name)))?;
        rt.block_on(download(asset, &directory, work))?;
        if asset.name == preset.assets[2].name {
            extract(root, preset, work)?;
            if preset.gpu {
                gpu_device(root, preset, work)?;
            }
        }
    }
    verify(root, preset, work)?;
    let _runtime = start(root, preset, work)?;
    Ok(json!({"installed":true,"preset":preset.id}))
}
fn start(root: &Path, preset: &Preset, work: &Work) -> Result<Runtime> {
    hardware(preset)?;
    verify(root, preset, work)?;
    let listener = TcpListener::bind("127.0.0.1:0")?;
    let port = listener.local_addr()?.port();
    drop(listener);
    let token = format!(
        "{}{}",
        uuid::Uuid::new_v4().simple(),
        uuid::Uuid::new_v4().simple()
    );
    let directory = preset.directory(root);
    let mut command = Command::new(preset.runtime(root).join("llama-server.exe"));
    command
        .arg("-m")
        .arg(directory.join(&preset.assets[0].name))
        .arg("--mmproj")
        .arg(directory.join(&preset.assets[1].name))
        .args(["--host", "127.0.0.1", "--port"])
        .arg(port.to_string())
        .args([
            "--api-key",
            &token,
            "--no-webui",
            "--offline",
            "-c",
            "8192",
            "-ngl",
            if preset.gpu { "all" } else { "0" },
            "-t",
        ])
        .arg(
            thread::available_parallelism()
                .map_or(4, |p| p.get())
                .saturating_sub(2)
                .clamp(1, 8)
                .to_string(),
        )
        .args(["--parallel", "1"])
        .stdout(Stdio::null())
        .stderr(Stdio::null());
    if preset.gpu {
        command.args([
            "--device",
            &gpu_device(root, preset, work)?,
            "--split-mode",
            "none",
            "--fit",
            "off",
        ]);
    }
    environment(&mut command);
    let child = OwnedChild::spawn(&mut command)?;
    let mut runtime_child = Runtime {
        child,
        port,
        token,
        preset: preset.id.clone(),
        used: Instant::now(),
    };
    let started: Result<()> = runtime()?.block_on(async {
        let client=reqwest::Client::builder().no_proxy().timeout(Duration::from_secs(1)).build()?;
        let started=Instant::now();
        while started.elapsed()<Duration::from_secs(180) {
            work.check()?;
            if runtime_child.child.process.try_wait()?.is_some(){return Err("Local runtime stopped during startup. Free memory or choose a smaller preset; no CPU fallback was used.".into());}
            if client.get(format!("http://127.0.0.1:{port}/health")).bearer_auth(&runtime_child.token).send().await.is_ok_and(|r|r.status().is_success()) {return Ok(());}
            tokio::time::sleep(Duration::from_millis(200)).await;
        } Err("Local model startup exceeded three minutes".into())
    });
    started?;
    Ok(runtime_child)
}
fn hardware(preset: &Preset) -> Result<()> {
    #[cfg(all(windows, target_arch = "x86_64"))]
    {
        use windows_sys::Win32::System::SystemInformation::{GlobalMemoryStatusEx, MEMORYSTATUSEX};
        if !std::is_x86_feature_detected!("avx2") {
            return Err("This local runtime requires AVX2; recording remains available".into());
        }
        let mut memory = MEMORYSTATUSEX {
            dwLength: std::mem::size_of::<MEMORYSTATUSEX>() as u32,
            ..Default::default()
        };
        if unsafe { GlobalMemoryStatusEx(&mut memory) } == 0 {
            return Err(std::io::Error::last_os_error().into());
        }
        if memory.ullTotalPhys < (preset.ram_gb * 1024 - 512) * 1024 * 1024 {
            return Err(format!("This preset requires {} GB installed RAM", preset.ram_gb).into());
        }
        Ok(())
    }
    #[cfg(not(all(windows, target_arch = "x86_64")))]
    {
        let _ = preset;
        Err("Local inference currently requires Windows 11 x64".into())
    }
}

pub fn remove(root: &Path, preset: &Preset, work: &Work) -> Result<Value> {
    let directory = preset.directory(root);
    contained(root, &directory)?;
    work.check()?;
    if directory.exists() {
        fs::remove_dir_all(directory)?;
    }
    Ok(json!({"removed":preset.id}))
}

#[cfg(test)]
pub(crate) mod tests {
    use super::*;
    use crate::operation::Operation;
    fn wait(operation: &Operation) {
        let deadline = Instant::now() + Duration::from_secs(10);
        while !operation.status().finished {
            assert!(Instant::now() < deadline);
            thread::sleep(Duration::from_millis(10));
        }
    }
    #[test]
    fn runtime_child_fixture() {
        if std::env::var_os("FLOW_RECORDER_TEST_CHILD").is_some() {
            thread::sleep(Duration::from_secs(30));
        }
    }

    pub(crate) fn fake_runtime(preset: &Preset, age: Duration) -> Runtime {
        let mut command = Command::new(std::env::current_exe().unwrap());
        command
            .args([
                "--exact",
                "model::tests::runtime_child_fixture",
                "--nocapture",
            ])
            .env("FLOW_RECORDER_TEST_CHILD", "1")
            .stdout(Stdio::null())
            .stderr(Stdio::null());
        Runtime {
            child: OwnedChild::spawn(&mut command).unwrap(),
            port: 1,
            token: "test-only".into(),
            preset: preset.id.clone(),
            used: Instant::now() - age,
        }
    }

    #[test]
    fn timer_releases_expired_runtime_and_preset_change_releases_warm_runtime() {
        let root = tempfile::tempdir().unwrap();
        let models = Models::new(root.path().to_owned());
        models.retain(fake_runtime(&models.selected(), Duration::from_secs(61)));
        let deadline = Instant::now() + Duration::from_secs(4);
        while models.warm.lock().unwrap().is_some() {
            assert!(Instant::now() < deadline);
            thread::sleep(Duration::from_millis(20));
        }
        assert_eq!(*models.state.lock().unwrap(), "unloaded");
        models.retain(fake_runtime(&models.selected(), Duration::ZERO));
        let next = presets().remove(1);
        models.select(&next.id).unwrap();
        assert!(models.warm.lock().unwrap().is_none());
        assert_eq!(models.selected().id, next.id);
        let directory = next.directory(root.path());
        fs::create_dir_all(&directory).unwrap();
        fs::write(directory.join("synthetic-asset"), b"test-only").unwrap();
        let worker_root = root.path().to_owned();
        let operation = Operation::start("removing", None, 1, move |work| {
            remove(&worker_root, &next, &work)
        });
        wait(&operation);
        assert_eq!(operation.status().state, "completed");
        assert!(!directory.exists());
    }

    #[test]
    fn healthy_warm_runtime_is_reused_but_expired_or_dead_runtime_is_not() {
        for state in ["healthy", "expired", "dead"] {
            let root = tempfile::tempdir().unwrap();
            let models = Models::new(root.path().to_owned());
            let preset = models.selected();
            let mut runtime = fake_runtime(
                &preset,
                if state == "expired" {
                    Duration::from_secs(61)
                } else {
                    Duration::ZERO
                },
            );
            let pid = runtime.child.process.id();
            if state == "dead" {
                runtime.child.process.kill().unwrap();
                runtime.child.process.wait().unwrap();
            }
            models.retain(runtime);
            let worker_models = models.clone();
            let operation = Operation::start("generating", None, 1, move |work| {
                let runtime = worker_models.take_or_start(&preset, &work)?;
                assert_eq!(runtime.child.process.id(), pid);
                worker_models.retain(runtime);
                Ok(Value::Null)
            });
            wait(&operation);
            assert_eq!(
                operation.status().state,
                if state == "healthy" {
                    "completed"
                } else {
                    "failed"
                }
            );
            // Missing fixture weights make a fresh launch fail. Success would
            // incorrectly mean that a stale/dead runtime was reused.
            models.release();
            assert!(models.warm.lock().unwrap().is_none());
            assert_eq!(*models.state.lock().unwrap(), "unloaded");
        }
    }
    #[test]
    fn downloads_resume_restart_and_never_publish_corruption() {
        for mode in ["resume", "restart", "corrupt"] {
            let directory = tempfile::tempdir().unwrap();
            let listener = TcpListener::bind("127.0.0.1:0").unwrap();
            let url = format!("http://{}/asset", listener.local_addr().unwrap());
            fs::write(directory.path().join("weights.part"), b"abc").unwrap();
            let server = thread::spawn(move || {
                let (mut stream, _) = listener.accept().unwrap();
                stream
                    .set_read_timeout(Some(Duration::from_secs(3)))
                    .unwrap();
                let mut request = Vec::new();
                let mut byte = [0];
                while !request.ends_with(b"\r\n\r\n") {
                    stream.read_exact(&mut byte).unwrap();
                    request.push(byte[0]);
                }
                assert!(
                    String::from_utf8_lossy(&request)
                        .to_lowercase()
                        .contains("range: bytes=3-")
                );
                let reply = if mode == "resume" {
                    "HTTP/1.1 206 Partial Content\r\nContent-Length: 3\r\nContent-Range: bytes 3-5/6\r\n\r\ndef"
                } else if mode == "restart" {
                    "HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\nabcdef"
                } else {
                    "HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\nxxxxxx"
                };
                stream.write_all(reply.as_bytes()).unwrap();
            });
            let asset = Asset {
                name: "weights".into(),
                url,
                size: 6,
                sha256: format!("{:x}", Sha256::digest(b"abcdef")),
            };
            let path = directory.path().to_owned();
            let op = Operation::start("installing", None, 0, move |work| {
                runtime()?.block_on(download_with_client(
                    &reqwest::Client::builder().no_proxy().build()?,
                    &asset,
                    &path,
                    &work,
                ))?;
                Ok(json!({}))
            });
            wait(&op);
            server.join().unwrap();
            if mode == "corrupt" {
                assert_eq!(op.status().state, "failed");
                assert!(!directory.path().join("weights").exists());
                assert!(!directory.path().join("weights.part").exists());
            } else {
                assert_eq!(op.status().state, "completed", "{:?}", op.status().error);
                assert_eq!(
                    fs::read(directory.path().join("weights")).unwrap(),
                    b"abcdef"
                );
            }
        }
    }
    #[test]
    fn cancelled_download_keeps_partial_bytes_and_returns_promptly() {
        let directory = tempfile::tempdir().unwrap();
        let listener = TcpListener::bind("127.0.0.1:0").unwrap();
        let url = format!("http://{}/asset", listener.local_addr().unwrap());
        let server = thread::spawn(move || {
            let (mut stream, _) = listener.accept().unwrap();
            let mut request = [0; 2048];
            let _ = stream.read(&mut request);
            stream
                .write_all(b"HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\nabc")
                .unwrap();
            thread::sleep(Duration::from_millis(500));
        });
        let path = directory.path().to_owned();
        let partial = path.join("weights.part");
        let op = Operation::start("installing", None, 0, move |work| {
            runtime()?.block_on(download_with_client(
                &reqwest::Client::builder().no_proxy().build()?,
                &Asset {
                    name: "weights".into(),
                    url,
                    size: 6,
                    sha256: format!("{:x}", Sha256::digest(b"abcdef")),
                },
                &path,
                &work,
            ))?;
            Ok(json!({}))
        });
        let deadline = Instant::now() + Duration::from_secs(3);
        while fs::metadata(&partial).map_or(0, |m| m.len()) < 3 {
            assert!(Instant::now() < deadline);
            thread::sleep(Duration::from_millis(5));
        }
        let start = Instant::now();
        op.cancel();
        wait(&op);
        assert!(start.elapsed() < Duration::from_millis(300));
        assert_eq!(op.status().state, "cancelled");
        assert_eq!(fs::read(partial).unwrap(), b"abc");
        server.join().unwrap();
    }
    #[test]
    fn device_parser_excludes_cpu_and_preserves_identifiers() {
        let d = devices(
            "CPU: Host (10000 MiB, free)\n Vulkan0: Intel GPU (4000 MiB, free)\nVulkan1: NVIDIA RTX (8000 MiB, free)",
        );
        assert_eq!(d.len(), 2);
        assert_eq!(d[1].0, "Vulkan1");
    }
    #[test]
    fn all_presets_are_pinned_and_cpu_gpu_share_original_weights() {
        let p = presets();
        assert_eq!(p.len(), 4);
        assert_eq!(p[0].assets[0].sha256, p[1].assets[0].sha256);
        for p in p {
            for a in p.assets {
                assert_eq!(a.sha256.len(), 64);
                assert!(a.url.starts_with("https://"));
            }
        }
    }
}

use serde_json::{Value, json};
use std::{
    fs,
    io::Write,
    path::Path,
    process::{Command, Stdio},
    time::{Duration, Instant},
};

fn fixture() -> tempfile::TempDir {
    let root = Path::new(env!("CARGO_MANIFEST_DIR"))
        .ancestors()
        .nth(3)
        .unwrap()
        .join(".tmp/rebuild/startup-tests");
    fs::create_dir_all(&root).unwrap();
    tempfile::tempdir_in(root).unwrap()
}

fn handshake(root: &Path, local_data: &Path, protocol: u32, params: Value) -> Value {
    let mut child = Command::new(env!("CARGO_BIN_EXE_phraseback-engine"))
        .args(["--data-root", root.to_str().unwrap()])
        .env("LOCALAPPDATA", local_data)
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();
    let body =
        serde_json::to_vec(&json!({"protocol":protocol,"id":1,"method":"hello","params":params}))
            .unwrap();
    let mut input = child.stdin.take().unwrap();
    input.write_all(&(body.len() as u32).to_le_bytes()).unwrap();
    input.write_all(&body).unwrap();
    let list =
        serde_json::to_vec(&json!({"protocol":1,"id":2,"method":"list_projects","params":{}}))
            .unwrap();
    input.write_all(&(list.len() as u32).to_le_bytes()).unwrap();
    input.write_all(&list).unwrap();
    drop(input); // Exercise controlling-shell disconnect as well as startup.
    let deadline = Instant::now() + Duration::from_secs(10);
    while child.try_wait().unwrap().is_none() {
        if Instant::now() >= deadline {
            child.kill().unwrap();
            child.wait().unwrap();
            panic!("Startup/disconnect exceeded ten seconds");
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    let output = child.wait_with_output().unwrap();
    assert!(output.status.success(), "{:?}", output.stderr);
    assert!(output.stdout.len() >= 4);
    let length = u32::from_le_bytes(output.stdout[..4].try_into().unwrap()) as usize;
    let first: Value = serde_json::from_slice(&output.stdout[4..4 + length]).unwrap();
    if first["error"].is_null() {
        let remainder = &output.stdout[4 + length..];
        assert!(remainder.len() >= 4);
        let size = u32::from_le_bytes(remainder[..4].try_into().unwrap()) as usize;
        assert_eq!(size + 4, remainder.len());
        let listed: Value = serde_json::from_slice(&remainder[4..]).unwrap();
        assert!(listed["error"].is_null(), "{listed}");
        assert_eq!(listed["result"]["items"], json!([]));
    } else {
        assert_eq!(length + 4, output.stdout.len());
    }
    first
}

#[test]
fn invalid_handshakes_never_create_a_fresh_data_root() {
    let temp = fixture();
    for (protocol, params, code) in [
        (999, json!({}), "protocol_mismatch"),
        (1, json!({"notifications":"yes"}), "invalid_params"),
    ] {
        let root = temp.path().join("not-created");
        assert_eq!(
            handshake(&root, temp.path(), protocol, params)["error"]["code"],
            code
        );
        assert!(!root.exists());
    }
}

#[test]
fn fresh_root_is_created_only_by_release_build_after_hello() {
    let temp = fixture();
    let root = temp.path().join("café's fresh data");
    let response = handshake(&root, temp.path(), 1, json!({}));
    if cfg!(debug_assertions) {
        assert_eq!(response["error"]["code"], "data_root_unavailable");
        assert!(!root.exists());
    } else {
        assert!(response["error"].is_null(), "{response}");
        assert!(root.is_dir());
        assert!(!root.join("application.lock").exists());
        assert!(!root.join(".flow-recorder-development").exists());
    }
}

#[test]
fn normal_location_remains_untouched_on_hello_and_is_debug_protected() {
    check_normal_location("Phraseback");
    check_normal_location("FlowRecorder");
}

fn check_normal_location(name: &str) {
    let temp = fixture();
    let root = temp.path().join(name);
    fs::create_dir(&root).unwrap();
    // Even a misplaced development marker must not let debug builds open it.
    fs::write(root.join(".flow-recorder-development"), b"").unwrap();
    fs::create_dir(root.join("models")).unwrap();
    fs::write(root.join("models/keep.bin"), b"synthetic model sentinel").unwrap();
    let response = handshake(&root, temp.path(), 1, json!({}));
    if cfg!(debug_assertions) {
        assert_eq!(response["error"]["code"], "data_root_unavailable");
        assert!(!root.join(".rust-development.lock").exists());
    } else {
        assert!(response["error"].is_null(), "{response}");
    }
    assert_eq!(
        fs::read(root.join("models/keep.bin")).unwrap(),
        b"synthetic model sentinel"
    );
    assert!(!root.join("application.lock").exists());
    assert_eq!(root.join("sessions").exists(), !cfg!(debug_assertions));
}

#[test]
fn marked_development_root_works_in_both_builds() {
    let temp = fixture();
    fs::write(temp.path().join(".flow-recorder-development"), b"").unwrap();
    let response = handshake(temp.path(), temp.path(), 1, json!({}));
    assert!(response["error"].is_null(), "{response}");
    assert!(!temp.path().join("application.lock").exists());
}

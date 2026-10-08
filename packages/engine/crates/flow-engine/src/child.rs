use flow_core::project::Result;
use std::process::{Child, Command, Stdio};

pub struct OwnedChild {
    pub process: Child,
    #[cfg(windows)]
    _job: Job,
}
impl OwnedChild {
    pub fn spawn(command: &mut Command) -> Result<Self> {
        command.stdin(Stdio::null());
        #[cfg(windows)]
        {
            use std::os::windows::process::CommandExt;
            // Keep the primary thread suspended until job assignment succeeds.
            // Otherwise a fast runtime could spawn descendants outside its job.
            command.creation_flags(0x08000004);
        }
        let process = command.spawn()?;
        #[cfg(windows)]
        let mut process = process;
        #[cfg(windows)]
        let job = match Job::attach(&process) {
            Ok(job) => job,
            Err(error) => {
                let _ = process.kill();
                let _ = process.wait();
                return Err(error);
            }
        };
        let owned = Self {
            process,
            #[cfg(windows)]
            _job: job,
        };
        #[cfg(windows)]
        resume_primary_thread(owned.process.id())?;
        Ok(owned)
    }
}
#[cfg(windows)]
fn resume_primary_thread(pid: u32) -> Result<()> {
    use windows_sys::Win32::{
        Foundation::*,
        System::{Diagnostics::ToolHelp::*, Threading::*},
    };
    struct Handle(HANDLE);
    impl Drop for Handle {
        fn drop(&mut self) {
            unsafe {
                CloseHandle(self.0);
            }
        }
    }
    // A newly created suspended process has exactly one thread. All handles are
    // closed on failure; OwnedChild then kills the still-suspended process.
    unsafe {
        let snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
        if snapshot == INVALID_HANDLE_VALUE {
            return Err(std::io::Error::last_os_error().into());
        }
        let snapshot = Handle(snapshot);
        let mut entry = THREADENTRY32 {
            dwSize: std::mem::size_of::<THREADENTRY32>() as u32,
            ..std::mem::zeroed()
        };
        let mut found = Thread32First(snapshot.0, &mut entry);
        while found != 0 {
            if entry.th32OwnerProcessID == pid {
                let thread = OpenThread(THREAD_SUSPEND_RESUME, 0, entry.th32ThreadID);
                if thread.is_null() {
                    return Err(std::io::Error::last_os_error().into());
                }
                let thread = Handle(thread);
                if ResumeThread(thread.0) == u32::MAX {
                    return Err(std::io::Error::last_os_error().into());
                }
                return Ok(());
            }
            found = Thread32Next(snapshot.0, &mut entry);
        }
    }
    Err("The supervised child primary thread was not found".into())
}
impl Drop for OwnedChild {
    fn drop(&mut self) {
        if !matches!(self.process.try_wait(), Ok(Some(_))) {
            let _ = self.process.kill();
        }
        let _ = self.process.wait();
    }
}
#[cfg(windows)]
struct Job(windows_sys::Win32::Foundation::HANDLE);
#[cfg(windows)]
unsafe impl Send for Job {}
#[cfg(windows)]
impl Job {
    fn attach(process: &Child) -> Result<Self> {
        use std::os::windows::io::AsRawHandle;
        use windows_sys::Win32::System::JobObjects::*;
        // The handle is owned by Job; every failure closes it and kills the child.
        unsafe {
            let handle = CreateJobObjectW(std::ptr::null(), std::ptr::null());
            if handle.is_null() {
                return Err(std::io::Error::last_os_error().into());
            }
            let job = Self(handle);
            let mut limits: JOBOBJECT_EXTENDED_LIMIT_INFORMATION = std::mem::zeroed();
            limits.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            if SetInformationJobObject(
                handle,
                JobObjectExtendedLimitInformation,
                &limits as *const _ as _,
                std::mem::size_of_val(&limits) as u32,
            ) == 0
                || AssignProcessToJobObject(handle, process.as_raw_handle()) == 0
            {
                return Err(std::io::Error::last_os_error().into());
            }
            Ok(job)
        }
    }
}
#[cfg(windows)]
impl Drop for Job {
    fn drop(&mut self) {
        unsafe {
            windows_sys::Win32::Foundation::CloseHandle(self.0);
        }
    }
}

#[cfg(all(test, windows))]
mod tests {
    use super::*;
    use std::{
        fs,
        os::windows::{
            io::{AsRawHandle, FromRawHandle, OwnedHandle},
            process::CommandExt,
        },
        thread,
        time::{Duration, Instant},
    };
    use windows_sys::Win32::System::Threading::{OpenProcess, WaitForSingleObject};

    fn fixture_command(role: &str, report: &std::path::Path) -> Command {
        let mut command = Command::new(std::env::current_exe().unwrap());
        command
            .args(["--exact", "child::tests::tree_fixture", "--nocapture"])
            .env("FLOW_TEST_TREE_ROLE", role)
            .env("FLOW_TEST_TREE_REPORT", report)
            .stdout(Stdio::null())
            .stderr(Stdio::null())
            .creation_flags(0x08000000);
        command
    }
    #[test]
    fn tree_fixture() {
        let Ok(role) = std::env::var("FLOW_TEST_TREE_ROLE") else {
            return;
        };
        let report = std::path::PathBuf::from(std::env::var_os("FLOW_TEST_TREE_REPORT").unwrap());
        if role == "owner" {
            let _child = OwnedChild::spawn(&mut fixture_command("parent", &report)).unwrap();
            thread::sleep(Duration::from_secs(30));
        } else if role == "parent" {
            let mut child = fixture_command("leaf", &report).spawn().unwrap();
            fs::write(report, format!("{} {}", std::process::id(), child.id())).unwrap();
            thread::sleep(Duration::from_secs(30));
            let _ = child.kill();
            let _ = child.wait();
        } else {
            thread::sleep(Duration::from_secs(30));
        }
    }
    fn handles(report: &std::path::Path) -> Vec<OwnedHandle> {
        let deadline = Instant::now() + Duration::from_secs(5);
        let ids = loop {
            if let Ok(text) = fs::read_to_string(report) {
                let ids: Vec<u32> = text
                    .split_whitespace()
                    .filter_map(|value| value.parse().ok())
                    .collect();
                if ids.len() == 2 {
                    break ids;
                }
            }
            assert!(Instant::now() < deadline);
            thread::sleep(Duration::from_millis(10));
        };
        ids.into_iter()
            .map(|pid| unsafe {
                let handle = OpenProcess(0x00100000, 0, pid);
                assert!(!handle.is_null());
                let handle = OwnedHandle::from_raw_handle(handle);
                assert_eq!(WaitForSingleObject(handle.as_raw_handle(), 0), 258);
                handle
            })
            .collect()
    }
    #[test]
    fn cleanup_kills_descendants_on_drop_and_abrupt_owner_death() {
        for abrupt in [false, true] {
            let root = tempfile::tempdir().unwrap();
            let report = root.path().join("pids.txt");
            if abrupt {
                let mut owner = fixture_command("owner", &report).spawn().unwrap();
                let descendants = handles(&report);
                owner.kill().unwrap();
                owner.wait().unwrap();
                for process in descendants {
                    assert_eq!(
                        unsafe { WaitForSingleObject(process.as_raw_handle(), 3000) },
                        0
                    );
                }
            } else {
                let child = OwnedChild::spawn(&mut fixture_command("parent", &report)).unwrap();
                let descendants = handles(&report);
                drop(child);
                for process in descendants {
                    assert_eq!(
                        unsafe { WaitForSingleObject(process.as_raw_handle(), 3000) },
                        0
                    );
                }
            }
        }
    }
}

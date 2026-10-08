//! Data-root ownership. Windows interoperates with the reference QLockFile.
//! The old prototype lock remains held so previously built engines cannot overlap.
use fs2::FileExt;
use std::{
    fs::{File, OpenOptions},
    io,
    path::Path,
};

pub struct DataLock {
    #[cfg(windows)]
    _application: windows::ApplicationLock,
    _prototype: File,
}

impl DataLock {
    pub fn acquire(root: &Path) -> io::Result<Self> {
        let prototype = OpenOptions::new()
            .read(true)
            .write(true)
            .create(true)
            .truncate(false)
            .open(root.join(".rust-development.lock"))?;
        prototype.try_lock_exclusive()?;
        Ok(Self {
            #[cfg(windows)]
            _application: windows::ApplicationLock::acquire(root.join("application.lock"))?,
            _prototype: prototype,
        })
    }
}

#[cfg(windows)]
mod windows {
    use std::{
        fs::{File, OpenOptions},
        io::{self, Read, Write},
        os::windows::{
            fs::{MetadataExt, OpenOptionsExt},
            io::{AsRawHandle, FromRawHandle, OwnedHandle},
        },
        path::{Path, PathBuf},
        time::{Duration, Instant, SystemTime},
    };
    use windows_sys::Win32::{
        Foundation::{ERROR_INVALID_PARAMETER, GENERIC_READ},
        Storage::FileSystem::{
            DELETE, FILE_ATTRIBUTE_REPARSE_POINT, FILE_DISPOSITION_INFO,
            FILE_FLAG_OPEN_REPARSE_POINT, FILE_SHARE_READ, FileDispositionInfo,
            SetFileInformationByHandle,
        },
        System::Threading::{
            GetExitCodeProcess, OpenProcess, PROCESS_QUERY_LIMITED_INFORMATION,
            QueryFullProcessImageNameW,
        },
    };

    pub struct ApplicationLock {
        file: Option<File>,
        path: PathBuf,
        contents: Vec<u8>,
    }

    fn busy() -> io::Error {
        io::Error::new(
            io::ErrorKind::WouldBlock,
            "This data folder is already open, or its lock cannot safely be reclaimed",
        )
    }

    impl ApplicationLock {
        pub fn acquire(path: PathBuf) -> io::Result<Self> {
            let name = std::env::current_exe()?
                .file_stem()
                .ok_or_else(busy)?
                .to_string_lossy()
                .into_owned();
            let host = std::env::var("COMPUTERNAME").unwrap_or_default();
            // Qt's five UTF-8 lines: PID, executable stem, hostname, optional machine/boot IDs.
            // Empty optional IDs retain compatibility with the three-line legacy format.
            let contents = format!("{}\n{name}\n{host}\n\n\n", std::process::id()).into_bytes();
            for _ in 0..3 {
                match OpenOptions::new()
                    .read(true)
                    .write(true)
                    .create_new(true)
                    .share_mode(FILE_SHARE_READ)
                    .open(&path)
                {
                    Ok(file) => {
                        let mut owned = Self {
                            file: Some(file),
                            path,
                            contents,
                        };
                        let file = owned.file.as_mut().unwrap();
                        file.write_all(&owned.contents)?;
                        file.sync_all()?;
                        return Ok(owned);
                    }
                    Err(error) if matches!(error.raw_os_error(), Some(5 | 32 | 80 | 183)) => {
                        if !remove_if(&path, apparently_stale)? {
                            return Err(busy());
                        }
                    }
                    Err(error) => return Err(error),
                }
            }
            Err(busy())
        }
    }

    impl Drop for ApplicationLock {
        fn drop(&mut self) {
            // Qt readers may briefly prevent reopening for deletion. Never delete by path
            // after a stale check: the handle below pins the exact file being checked.
            drop(self.file.take());
            let deadline = Instant::now() + Duration::from_millis(500);
            loop {
                match remove_if(&self.path, |_, bytes| Ok(bytes == self.contents)) {
                    Ok(true) => break,
                    _ if Instant::now() < deadline => std::thread::sleep(Duration::from_millis(5)),
                    _ => {
                        eprintln!("Data lock cleanup deferred; no recording content logged.");
                        break;
                    }
                }
            }
        }
    }

    fn remove_if(
        path: &Path,
        predicate: impl FnOnce(&File, &[u8]) -> io::Result<bool>,
    ) -> io::Result<bool> {
        // A live Qt/Rust writer denies DELETE and WRITE sharing. This open must fail
        // even if its timestamp or metadata makes that live lock look stale.
        let mut file = match OpenOptions::new()
            .access_mode(GENERIC_READ | DELETE)
            .share_mode(FILE_SHARE_READ)
            .custom_flags(FILE_FLAG_OPEN_REPARSE_POINT)
            .open(path)
        {
            Ok(file) => file,
            Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(true),
            Err(error) if matches!(error.raw_os_error(), Some(5 | 32)) => return Ok(false),
            Err(error) => return Err(error),
        };
        if file.metadata()?.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT != 0 {
            return Err(io::Error::new(
                io::ErrorKind::InvalidData,
                "Lock path must not be a reparse point",
            ));
        }
        let mut bytes = Vec::new();
        (&mut file).take(16 * 1024).read_to_end(&mut bytes)?;
        if !predicate(&file, &bytes)? {
            return Ok(false);
        }
        let disposition = FILE_DISPOSITION_INFO { DeleteFile: true };
        // SAFETY: file owns a valid handle; disposition remains valid for this synchronous call.
        if unsafe {
            SetFileInformationByHandle(
                file.as_raw_handle(),
                FileDispositionInfo,
                (&disposition as *const FILE_DISPOSITION_INFO).cast(),
                std::mem::size_of_val(&disposition) as u32,
            )
        } == 0
        {
            return Err(io::Error::last_os_error());
        }
        Ok(true) // Closing this handle deletes that same file, not a replacement at its path.
    }

    fn apparently_stale(file: &File, bytes: &[u8]) -> io::Result<bool> {
        if let Ok(text) = std::str::from_utf8(bytes) {
            let mut lines = text.lines();
            let pid = lines
                .next()
                .and_then(|s| s.parse::<u32>().ok())
                .filter(|p| *p > 0);
            let app = lines.next();
            let host = lines.next();
            if let (Some(pid), Some(app), Some(host)) = (pid, app, host)
                && (host.is_empty() || host == std::env::var("COMPUTERNAME").unwrap_or_default())
                && process_is_gone(pid, app)
            {
                return Ok(true);
            }
        }
        let modified = file.metadata()?.modified()?;
        let now = SystemTime::now();
        let age = now
            .duration_since(modified)
            .unwrap_or_else(|_| modified.duration_since(now).unwrap_or_default());
        Ok(age > Duration::from_secs(30))
    }

    fn process_is_gone(pid: u32, app: &str) -> bool {
        // Access denied is inconclusive, not proof that a process died.
        // SAFETY: no pointer arguments; a non-null returned handle is transferred to OwnedHandle.
        let raw = unsafe { OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, 0, pid) };
        if raw.is_null() {
            return io::Error::last_os_error().raw_os_error()
                == Some(ERROR_INVALID_PARAMETER as i32);
        }
        let process = unsafe { OwnedHandle::from_raw_handle(raw) };
        let mut exit = 0;
        // SAFETY: process owns the handle and exit is a valid output pointer.
        if unsafe { GetExitCodeProcess(process.as_raw_handle(), &mut exit) } == 0 {
            return false;
        }
        if exit != 259 {
            return true;
        }
        let mut buffer = vec![0u16; 32768];
        let mut length = buffer.len() as u32;
        // SAFETY: buffer capacity matches the supplied mutable length; the handle is live.
        if unsafe {
            QueryFullProcessImageNameW(process.as_raw_handle(), 0, buffer.as_mut_ptr(), &mut length)
        } == 0
        {
            return false;
        }
        let executable = String::from_utf16_lossy(&buffer[..length as usize]);
        Path::new(&executable)
            .file_stem()
            .is_some_and(|name| name.to_string_lossy() != app)
    }
}

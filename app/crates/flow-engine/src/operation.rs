use flow_core::project::Result;
use serde::Serialize;
use serde_json::Value;
use std::{
    sync::{
        Arc, Mutex,
        atomic::{AtomicBool, Ordering},
        mpsc::{self, Receiver, SyncSender},
    },
    thread::{self, JoinHandle},
    time::{Duration, Instant},
};

#[derive(Clone, Serialize)]
pub struct Status {
    pub id: String,
    pub kind: String,
    pub state: String,
    pub progress: u32,
    pub message: String,
    pub recording_id: Option<String>,
    pub revision: u64,
    pub finished: bool,
    pub error: Option<String>,
    pub result: Option<Value>,
}
#[derive(Clone)]
pub struct Work {
    pub cancel: Arc<AtomicBool>,
    pub status: Arc<Mutex<Status>>,
    saved: SyncSender<Status>,
}
impl Work {
    pub fn check(&self) -> Result<()> {
        if self.cancel.load(Ordering::Relaxed) {
            Err("Operation cancelled; saved progress is retained".into())
        } else {
            Ok(())
        }
    }
    pub fn update(&self, state: &str, progress: u32, message: &str) {
        let mut status = self.status.lock().unwrap();
        status.state = state.into();
        status.progress = progress.min(100);
        status.message = message.into();
    }
    pub fn recording(&self, id: &str) {
        self.status.lock().unwrap().recording_id = Some(id.into());
    }
    pub fn capture_preview(&self, path: &std::path::Path, width: u32, height: u32) {
        // Disposable presentation reference; coalesced progress, never a save acknowledgement.
        self.status.lock().unwrap().result = Some(serde_json::json!({
            "capture_preview": path, "capture_width": width, "capture_height": height
        }));
    }
    pub fn saved(&self, result: Value) -> Result<()> {
        let mut status = {
            let mut status = self.status.lock().unwrap();
            status.result = Some(result);
            status.clone()
        };
        // Save acknowledgements are never coalesced. Bounded backpressure retains
        // durability ordering even when the controlling shell is temporarily slow.
        loop {
            self.check()?;
            match self.saved.try_send(status) {
                Ok(()) => return Ok(()),
                Err(mpsc::TrySendError::Full(pending)) => {
                    status = pending;
                    thread::sleep(Duration::from_millis(10));
                }
                Err(mpsc::TrySendError::Disconnected(_)) => {
                    return Err("Controlling shell disconnected".into());
                }
            }
        }
    }
}

pub struct Operation {
    pub work: Work,
    thread: Option<JoinHandle<()>>,
    saved: Receiver<Status>,
}
impl Operation {
    pub fn start(
        kind: &str,
        recording_id: Option<String>,
        revision: u64,
        run: impl FnOnce(Work) -> Result<Value> + Send + 'static,
    ) -> Self {
        let (saved, notifications) = mpsc::sync_channel(8);
        let work = Work {
            saved,
            cancel: Arc::new(AtomicBool::new(false)),
            status: Arc::new(Mutex::new(Status {
                id: uuid::Uuid::new_v4().to_string(),
                kind: kind.into(),
                state: kind.into(),
                progress: 0,
                message: "Starting…".into(),
                recording_id,
                revision,
                finished: false,
                error: None,
                result: None,
            })),
        };
        let worker = work.clone();
        let thread = thread::spawn(move || {
            let result =
                std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| run(worker.clone())));
            let mut status = worker.status.lock().unwrap();
            status.finished = true;
            match result {
                Ok(Ok(value)) => {
                    status.result = Some(value);
                    status.state = "completed".into();
                    status.progress = 100;
                    status.message = "Complete".into();
                }
                Ok(Err(error)) => {
                    status.error = Some(error.to_string());
                    status.state = if worker.cancel.load(Ordering::Relaxed) {
                        "cancelled"
                    } else {
                        "failed"
                    }
                    .into();
                }
                Err(_) => {
                    status.error =
                        Some("Worker failed unexpectedly; reopen to recover saved progress".into());
                    status.state = "failed".into();
                }
            }
        });
        Self {
            work,
            thread: Some(thread),
            saved: notifications,
        }
    }
    pub fn status(&self) -> Status {
        self.work.status.lock().unwrap().clone()
    }
    pub fn cancel(&self) {
        self.work.cancel.store(true, Ordering::Relaxed);
    }
    pub fn saved_notifications(&self) -> impl Iterator<Item = Status> + '_ {
        self.saved.try_iter()
    }
}
impl Drop for Operation {
    fn drop(&mut self) {
        self.cancel();
        if let Some(thread) = self.thread.take() {
            let deadline = Instant::now() + Duration::from_secs(5);
            while !thread.is_finished() && Instant::now() < deadline {
                thread::sleep(Duration::from_millis(10));
            }
            if thread.is_finished() {
                let _ = thread.join();
            } else {
                // Exit while the data-root lock is still held. Never release ownership
                // while a stuck worker could continue writing authoritative data.
                eprintln!("Worker shutdown deadline exceeded; process cleanup required.");
                std::process::exit(2);
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    #[test]
    fn cancellation_unblocks_full_durable_notification_queue() {
        let (ready, waiting) = mpsc::channel();
        let operation = Operation::start("generating", None, 1, move |work| {
            for index in 0..8 {
                work.saved(json!({"saved":index}))?;
            }
            ready.send(()).unwrap();
            work.saved(json!({"saved":8}))?;
            Ok(Value::Null)
        });
        waiting.recv_timeout(Duration::from_secs(2)).unwrap();
        operation.cancel();
        let deadline = Instant::now() + Duration::from_secs(2);
        while !operation.status().finished {
            assert!(
                Instant::now() < deadline,
                "Cancellation blocked by saved notifications"
            );
            thread::sleep(Duration::from_millis(10));
        }
        assert_eq!(operation.status().state, "cancelled");
        assert_eq!(operation.saved_notifications().count(), 8);
        // Cancellation can prevent an acknowledgement from being enqueued, but
        // the last durable result remains discoverable in terminal status.
        assert_eq!(operation.status().result.unwrap()["saved"], 8);
    }
}

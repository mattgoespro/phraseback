//! Test-only, thread-local accounting of Rust allocator requests. Not process RSS,
//! allocator overhead, stack storage, foreign allocations, or GPU residency.
//! Measured closures must create AND destroy their allocations within the scope;
//! callers assert a zero balance to catch violations of that constraint.
use std::{
    alloc::{GlobalAlloc, Layout, System},
    cell::Cell,
};

#[derive(Clone, Copy, Default, Debug, serde::Serialize)]
pub struct Stats {
    pub live_bytes: i64,
    pub peak_bytes: i64,
    pub allocated_bytes: u64,
    pub allocations: u64,
}

thread_local! {
    static STATE: Cell<Option<Stats>> = const { Cell::new(None) };
}

fn record(allocated: usize, freed: usize) {
    let _ = STATE.try_with(|state| {
        if let Some(mut stats) = state.get() {
            stats.live_bytes += allocated as i64 - freed as i64;
            stats.peak_bytes = stats.peak_bytes.max(stats.live_bytes);
            stats.allocated_bytes += allocated as u64;
            stats.allocations += u64::from(allocated != 0);
            state.set(Some(stats));
        }
    });
}

struct Probe;
#[global_allocator]
static ALLOCATOR: Probe = Probe;

// All allocation operations delegate unchanged to System. Accounting uses only
// const-initialized thread-local Cells and cannot allocate or unwind.
unsafe impl GlobalAlloc for Probe {
    unsafe fn alloc(&self, layout: Layout) -> *mut u8 {
        let ptr = unsafe { System.alloc(layout) };
        if !ptr.is_null() {
            record(layout.size(), 0);
        }
        ptr
    }
    unsafe fn alloc_zeroed(&self, layout: Layout) -> *mut u8 {
        let ptr = unsafe { System.alloc_zeroed(layout) };
        if !ptr.is_null() {
            record(layout.size(), 0);
        }
        ptr
    }
    unsafe fn dealloc(&self, ptr: *mut u8, layout: Layout) {
        record(0, layout.size());
        unsafe { System.dealloc(ptr, layout) };
    }
    unsafe fn realloc(&self, ptr: *mut u8, layout: Layout, size: usize) -> *mut u8 {
        let result = unsafe { System.realloc(ptr, layout, size) };
        if !result.is_null() {
            record(size, layout.size());
        }
        result
    }
}

pub fn measure(action: impl FnOnce()) -> Stats {
    struct Reset;
    impl Drop for Reset {
        fn drop(&mut self) {
            STATE.with(|state| state.set(None));
        }
    }
    STATE.with(|state| {
        assert!(state.get().is_none());
        state.set(Some(Stats::default()));
    });
    let _reset = Reset;
    action();
    STATE.with(|state| state.get().unwrap())
}

#[test]
fn probe_counts_and_releases_owned_allocations() {
    let stats = measure(|| {
        let mut bytes = Vec::with_capacity(1024);
        bytes.resize(1024, 1u8);
        std::hint::black_box(&bytes);
        bytes.reserve_exact(1024);
        std::hint::black_box(&bytes);
    });
    assert_eq!(stats.live_bytes, 0);
    assert_eq!(stats.peak_bytes, 2048);
    assert_eq!(stats.allocated_bytes, 3072);
}

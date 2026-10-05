use serde::{Deserialize, Serialize};
use std::sync::{
    Arc, Mutex,
    mpsc::{Receiver, SyncSender},
};

pub type Result<T> = std::result::Result<T, Box<dyn std::error::Error + Send + Sync>>;
#[derive(Clone, Debug, Serialize, Deserialize, PartialEq)]
pub struct Screen {
    pub id: String,
    pub name: String,
    pub left: i32,
    pub top: i32,
    pub width: u32,
    pub height: u32,
}
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct Area {
    pub left: i32,
    pub top: i32,
    pub width: u32,
    pub height: u32,
}
impl Area {
    pub fn validate(&self, screen: &Screen) -> Result<()> {
        if self.width == 0
            || self.height == 0
            || self.left < screen.left
            || self.top < screen.top
            || self.left as i64 + self.width as i64 > screen.left as i64 + screen.width as i64
            || self.top as i64 + self.height as i64 > screen.top as i64 + screen.height as i64
            || self.width as u64 * self.height as u64 * 4 > 128 * 1024 * 1024 / 3
        {
            return Err(
                "Select a non-empty region inside one display, within the capture buffer budget"
                    .into(),
            );
        }
        Ok(())
    }
}
pub struct RawFrame {
    pub sequence: u64,
    pub time_ms: u64,
    pub pixels: Vec<u8>,
    pub descriptor: Vec<u8>,
}
#[derive(Default, Clone, Debug, Serialize)]
pub struct Metrics {
    pub samples: u64,
    pub accepted: u64,
    pub duplicates: u64,
    pub missed: u64,
    pub pool_bytes: u64,
    /// Logical RGBA payload of the single WGC surface, NOT measured VRAM usage.
    pub wgc_surface_payload_bytes: Option<u64>,
    /// Peak readable mapped staging bytes, including observed row padding.
    pub staging_mapped_peak_bytes: Option<u64>,
    pub staging_row_pitch_peak_bytes: Option<u64>,
    pub gpu_memory: Option<GpuMemoryMetrics>,
}

/// Sampled DXGI process usage on capture adapter node 0. Not system-wide usage,
/// nor exact individual-resource allocation/residency or unsampled peak usage.
#[derive(Default, Clone, Debug, Serialize)]
pub struct GpuMemoryMetrics {
    pub adapter_name: String,
    pub node_index: u32,
    pub samples: u64,
    pub failed_queries: u64,
    pub local_peak_bytes: Option<u64>,
    pub nonlocal_peak_bytes: Option<u64>,
    pub recent: std::collections::VecDeque<GpuMemorySample>,
}
#[derive(Clone, Debug, Serialize)]
pub struct GpuMemorySample {
    pub elapsed_ms: u64,
    pub local_usage_bytes: Option<u64>,
    pub nonlocal_usage_bytes: Option<u64>,
    pub local_budget_bytes: Option<u64>,
    pub nonlocal_budget_bytes: Option<u64>,
}
impl GpuMemoryMetrics {
    pub fn observe(&mut self, sample: GpuMemorySample) {
        self.samples += 1;
        for (usage, peak) in [
            (sample.local_usage_bytes, &mut self.local_peak_bytes),
            (sample.nonlocal_usage_bytes, &mut self.nonlocal_peak_bytes),
        ] {
            if let Some(usage) = usage {
                *peak = Some(peak.unwrap_or(0).max(usage));
            } else {
                self.failed_queries += 1;
            }
        }
        if self.recent.len() == 256 {
            self.recent.pop_front();
        }
        self.recent.push_back(sample);
    }
}
impl Metrics {
    pub fn observe_staging(&mut self, row_pitch: u32, height: u32) {
        let pitch = u64::from(row_pitch);
        self.staging_row_pitch_peak_bytes =
            Some(self.staging_row_pitch_peak_bytes.unwrap_or(0).max(pitch));
        self.staging_mapped_peak_bytes = Some(
            self.staging_mapped_peak_bytes
                .unwrap_or(0)
                .max(pitch * u64::from(height)),
        );
    }
}
#[derive(Clone)]
pub struct CaptureContext {
    pub screen: Screen,
    pub area: Area,
    pub sender: SyncSender<RawFrame>,
    pub free: Arc<Mutex<Receiver<Vec<u8>>>>,
    pub recycle: SyncSender<Vec<u8>>,
    pub metrics: Arc<Mutex<Metrics>>,
}

/// Cursor-free descriptor, retained separately from cursor-inclusive source evidence.
pub fn descriptor(pixels: &[u8], width: u32, height: u32) -> Vec<u8> {
    let scale = (320.0 / width.max(height) as f64).min(1.0);
    let w = ((width as f64 * scale).round() as u32).max(1);
    let h = ((height as f64 * scale).round() as u32).max(1);
    let mut data = Vec::with_capacity(8 + w as usize * h as usize * 3);
    data.extend_from_slice(&w.to_le_bytes());
    data.extend_from_slice(&h.to_le_bytes());
    for y in 0..h {
        for x in 0..w {
            let offset = (((y as u64 * height as u64 / h as u64) * width as u64
                + x as u64 * width as u64 / w as u64)
                * 4) as usize;
            data.extend_from_slice(&pixels[offset..offset + 3]);
        }
    }
    data
}

#[cfg(windows)]
mod native;
#[cfg(windows)]
pub use native::{Session, screens};
#[cfg(not(windows))]
pub fn screens() -> Result<Vec<Screen>> {
    Ok(Vec::new())
}
#[cfg(not(windows))]
pub struct Session;
#[cfg(not(windows))]
impl Session {
    pub fn start(_: CaptureContext) -> Result<Self> {
        Err("Capture is supported on Windows only".into())
    }
    pub fn stop(self) -> Result<()> {
        Ok(())
    }
    pub fn is_finished(&self) -> bool {
        true
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn gpu_samples_are_bounded_and_failed_queries_are_not_zero_usage() {
        let mut metrics = GpuMemoryMetrics::default();
        for index in 0..300 {
            metrics.observe(GpuMemorySample {
                elapsed_ms: index,
                local_usage_bytes: (index != 299).then_some(300 - index),
                nonlocal_usage_bytes: None,
                local_budget_bytes: Some(1000),
                nonlocal_budget_bytes: None,
            });
        }
        assert_eq!(metrics.samples, 300);
        assert_eq!(metrics.failed_queries, 301);
        assert_eq!(metrics.local_peak_bytes, Some(300));
        assert_eq!(metrics.nonlocal_peak_bytes, None);
        assert_eq!(metrics.recent.len(), 256);
        assert_eq!(metrics.recent.front().unwrap().elapsed_ms, 44);
        assert_eq!(metrics.recent.back().unwrap().local_usage_bytes, None);
    }
    #[test]
    fn staging_accounting_includes_padding_and_retains_peak() {
        let mut metrics = Metrics::default();
        assert_eq!(metrics.staging_mapped_peak_bytes, None);
        assert_eq!(metrics.wgc_surface_payload_bytes, None);
        metrics.observe_staging(512, 20);
        metrics.observe_staging(256, 10);
        assert_eq!(metrics.staging_mapped_peak_bytes, Some(10_240));
        assert_eq!(metrics.staging_row_pitch_peak_bytes, Some(512));
        assert_eq!(metrics.pool_bytes, 0);
    }
    #[test]
    fn region_uses_physical_pixels_and_negative_origins() {
        let screen = Screen {
            id: "test".into(),
            name: "test".into(),
            left: -2560,
            top: -100,
            width: 2560,
            height: 1600,
        };
        assert!(
            Area {
                left: -2560,
                top: -100,
                width: 2560,
                height: 1600
            }
            .validate(&screen)
            .is_ok()
        );
        assert!(
            Area {
                left: -2561,
                top: -100,
                width: 1,
                height: 1
            }
            .validate(&screen)
            .is_err()
        );
        assert!(
            Area {
                left: -2560,
                top: -100,
                width: u32::MAX,
                height: u32::MAX
            }
            .validate(&screen)
            .is_err()
        );
    }
}

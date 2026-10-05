use super::*;
use std::{
    mem::size_of,
    time::{Duration, Instant},
};
use windows::{
    Win32::Graphics::Dxgi::{
        DXGI_MEMORY_SEGMENT_GROUP, DXGI_MEMORY_SEGMENT_GROUP_LOCAL,
        DXGI_MEMORY_SEGMENT_GROUP_NON_LOCAL, DXGI_QUERY_VIDEO_MEMORY_INFO, IDXGIAdapter3,
        IDXGIDevice,
    },
    core::Interface,
};
use windows_capture::{
    capture::{CaptureControl, Context, GraphicsCaptureApiHandler},
    frame::Frame,
    graphics_capture_api::InternalCaptureControl,
    monitor::Monitor,
    settings::{
        ColorFormat, CursorCaptureSettings, DirtyRegionSettings, DrawBorderSettings,
        MinimumUpdateIntervalSettings, SecondaryWindowSettings, Settings,
    },
};
use windows_sys::Win32::{
    Graphics::Gdi::*,
    UI::{HiDpi::*, WindowsAndMessaging::*},
};

pub fn screens() -> Result<Vec<Screen>> {
    // The engine's capture coordinates are always physical pixels.
    unsafe {
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    }
    Monitor::enumerate()?
        .into_iter()
        .map(|monitor| {
            let mut info = MONITORINFO {
                cbSize: size_of::<MONITORINFO>() as u32,
                ..Default::default()
            };
            if unsafe { GetMonitorInfoW(monitor.as_raw_hmonitor(), &mut info) } == 0 {
                return Err(std::io::Error::last_os_error().into());
            }
            Ok(Screen {
                id: monitor.device_name()?,
                name: monitor.name()?,
                left: info.rcMonitor.left,
                top: info.rcMonitor.top,
                width: (info.rcMonitor.right - info.rcMonitor.left) as u32,
                height: (info.rcMonitor.bottom - info.rcMonitor.top) as u32,
            })
        })
        .collect()
}

pub struct Session(CaptureControl<Handler, Box<dyn std::error::Error + Send + Sync>>);
impl Session {
    pub fn start(context: CaptureContext) -> Result<Self> {
        context.area.validate(&context.screen)?;
        if !screens()?.contains(&context.screen) {
            return Err("The selected display changed or disconnected".into());
        }
        let monitor = Monitor::enumerate()?
            .into_iter()
            .find(|m| m.device_name().ok().as_deref() == Some(&context.screen.id))
            .ok_or("The selected display is unavailable")?;
        require_sdr(&monitor)?;
        let settings = Settings::new(
            monitor,
            CursorCaptureSettings::WithoutCursor,
            DrawBorderSettings::Default,
            SecondaryWindowSettings::Default,
            MinimumUpdateIntervalSettings::Default,
            DirtyRegionSettings::Default,
            ColorFormat::Rgba8,
            context,
        );
        Ok(Self(Handler::start_free_threaded(settings)?))
    }
    pub fn stop(self) -> Result<()> {
        self.0.stop().map_err(|e| e.to_string().into())
    }
    pub fn is_finished(&self) -> bool {
        self.0.is_finished()
    }
}

fn require_sdr(monitor: &Monitor) -> Result<()> {
    use windows::{
        Win32::Graphics::Dxgi::{
            Common::DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709, CreateDXGIFactory1, IDXGIFactory1,
            IDXGIOutput6,
        },
        core::Interface,
    };
    let factory: IDXGIFactory1 = unsafe { CreateDXGIFactory1()? };
    let mut adapter_index = 0;
    while let Ok(adapter) = unsafe { factory.EnumAdapters1(adapter_index) } {
        let mut output_index = 0;
        while let Ok(output) = unsafe { adapter.EnumOutputs(output_index) } {
            let desc = unsafe { output.GetDesc()? };
            if desc.Monitor.0 == monitor.as_raw_hmonitor() {
                let output: IDXGIOutput6 = output.cast()?;
                if unsafe { output.GetDesc1()? }.ColorSpace
                    != DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709
                {
                    return Err("This release supports SDR capture only. Turn off HDR for this display in Windows Settings and use an SDR/sRGB color mode before recording.".into());
                }
                return Ok(());
            }
            output_index += 1;
        }
        adapter_index += 1;
    }
    Err("The display color space could not be verified; recording was not started".into())
}

struct Handler {
    context: CaptureContext,
    started: Instant,
    next: Duration,
    previous: Vec<u8>,
    sequence: u64,
    gpu_adapter: Option<IDXGIAdapter3>,
}
impl GraphicsCaptureApiHandler for Handler {
    type Flags = CaptureContext;
    type Error = Box<dyn std::error::Error + Send + Sync>;
    fn new(context: Context<Self::Flags>) -> Result<Self> {
        // Diagnostics are optional: unsupported counters must not block capture.
        let gpu_adapter = context
            .device
            .cast::<IDXGIDevice>()
            .ok()
            .and_then(|device| unsafe { device.GetAdapter().ok() })
            .and_then(|adapter| adapter.cast::<IDXGIAdapter3>().ok());
        if let Some(adapter) = &gpu_adapter {
            let name = unsafe { adapter.GetDesc() }
                .ok()
                .map(|desc| {
                    let end = desc
                        .Description
                        .iter()
                        .position(|c| *c == 0)
                        .unwrap_or(desc.Description.len());
                    String::from_utf16_lossy(&desc.Description[..end])
                })
                .unwrap_or_else(|| "Unknown capture adapter".into());
            context.flags.metrics.lock().unwrap().gpu_memory = Some(GpuMemoryMetrics {
                adapter_name: name,
                ..Default::default()
            });
        }
        context
            .flags
            .metrics
            .lock()
            .unwrap()
            .wgc_surface_payload_bytes = Some(
            u64::from(context.flags.screen.width) * u64::from(context.flags.screen.height) * 4,
        );
        Ok(Self {
            context: context.flags,
            started: Instant::now(),
            next: Duration::ZERO,
            previous: Vec::new(),
            sequence: 0,
            gpu_adapter,
        })
    }
    fn on_frame_arrived(&mut self, frame: &mut Frame, _: InternalCaptureControl) -> Result<()> {
        let elapsed = self.started.elapsed();
        if elapsed < self.next {
            return Ok(());
        }
        let missed = elapsed.saturating_sub(self.next).as_millis() / 125;
        self.next = Duration::from_millis(((elapsed.as_millis() / 125 + 1) * 125) as u64);
        if frame.width() != self.context.screen.width
            || frame.height() != self.context.screen.height
        {
            return Err("Display resolution changed; recording stopped".into());
        }
        {
            let mut metrics = self.context.metrics.lock().unwrap();
            metrics.samples += 1;
            metrics.missed += missed as u64;
        }
        let Ok(mut pixels) = self.context.free.lock().unwrap().try_recv() else {
            self.context.metrics.lock().unwrap().missed += 1;
            return Ok(());
        };
        let area = &self.context.area;
        let x = (area.left - self.context.screen.left) as u32;
        let y = (area.top - self.context.screen.top) as u32;
        let mut buffer = frame.buffer_crop(x, y, x + area.width, y + area.height)?;
        let stride = buffer.row_pitch() as usize;
        let source = buffer.as_raw_buffer();
        let row_bytes = area.width as usize * 4;
        for row in 0..area.height as usize {
            pixels[row * row_bytes..(row + 1) * row_bytes]
                .copy_from_slice(&source[row * stride..row * stride + row_bytes]);
        }
        self.context
            .metrics
            .lock()
            .unwrap()
            .observe_staging(buffer.row_pitch(), area.height);
        // Observe while the staging texture is still alive, at the accepted
        // sampling cadence. This is process usage, not a texture-size estimate.
        if let Some(adapter) = &self.gpu_adapter {
            let query = |segment: DXGI_MEMORY_SEGMENT_GROUP| {
                let mut info = DXGI_QUERY_VIDEO_MEMORY_INFO::default();
                unsafe { adapter.QueryVideoMemoryInfo(0, segment, &mut info) }
                    .ok()
                    .map(|()| info)
            };
            let local = query(DXGI_MEMORY_SEGMENT_GROUP_LOCAL);
            let nonlocal = query(DXGI_MEMORY_SEGMENT_GROUP_NON_LOCAL);
            if let Some(metrics) = self.context.metrics.lock().unwrap().gpu_memory.as_mut() {
                metrics.observe(GpuMemorySample {
                    elapsed_ms: elapsed.as_millis() as u64,
                    local_usage_bytes: local.as_ref().map(|v| v.CurrentUsage),
                    nonlocal_usage_bytes: nonlocal.as_ref().map(|v| v.CurrentUsage),
                    local_budget_bytes: local.as_ref().map(|v| v.Budget),
                    nonlocal_budget_bytes: nonlocal.as_ref().map(|v| v.Budget),
                });
            }
        }
        // Pixels are now owned by the pooled vector. Release the mapped texture
        // before descriptor and cursor work; it is not a pooled CPU allocation.
        drop(buffer);
        let descriptor = super::descriptor(&pixels, area.width, area.height);
        composite_cursor(&mut pixels, area)?;
        if pixels == self.previous {
            self.context.metrics.lock().unwrap().duplicates += 1;
            let _ = self.context.recycle.try_send(pixels);
            return Ok(());
        }
        self.previous.clone_from(&pixels);
        let item = RawFrame {
            sequence: self.sequence,
            time_ms: if self.sequence == 0 {
                0
            } else {
                elapsed.as_millis() as u64
            },
            pixels,
            descriptor,
        };
        self.context.sender.try_send(item).map_err(
            |_| "Capture queue unavailable; accepted evidence was not silently discarded",
        )?;
        self.sequence += 1;
        self.context.metrics.lock().unwrap().accepted += 1;
        Ok(())
    }
}

fn composite_cursor(pixels: &mut [u8], area: &Area) -> Result<()> {
    let mut cursor = CURSORINFO {
        cbSize: size_of::<CURSORINFO>() as u32,
        ..Default::default()
    };
    if unsafe { GetCursorInfo(&mut cursor) } == 0 {
        return Err(std::io::Error::last_os_error().into());
    }
    composite_cursor_state(pixels, area, &cursor)
}

// Separate acquisition from rendering so native shape/hotspot tests do not
// depend on, move, or replace the user's live pointer.
fn composite_cursor_state(pixels: &mut [u8], area: &Area, cursor: &CURSORINFO) -> Result<()> {
    if cursor.flags & CURSOR_SHOWING == 0 {
        return Ok(());
    }
    let mut icon = ICONINFO::default();
    if unsafe { GetIconInfo(cursor.hCursor, &mut icon) } == 0 {
        return Err(std::io::Error::last_os_error().into());
    }
    struct Icons(ICONINFO);
    impl Drop for Icons {
        fn drop(&mut self) {
            unsafe {
                if !self.0.hbmColor.is_null() {
                    DeleteObject(self.0.hbmColor);
                }
                if !self.0.hbmMask.is_null() {
                    DeleteObject(self.0.hbmMask);
                }
            }
        }
    }
    let icons = Icons(icon);
    let mut shape = BITMAP::default();
    let colored = !icons.0.hbmColor.is_null();
    if unsafe {
        GetObjectW(
            if colored {
                icons.0.hbmColor
            } else {
                icons.0.hbmMask
            },
            size_of::<BITMAP>() as i32,
            (&mut shape as *mut BITMAP).cast(),
        )
    } == 0
    {
        return Err("Unable to read cursor shape".into());
    }
    let width = shape.bmWidth;
    let height = if colored {
        shape.bmHeight
    } else {
        shape.bmHeight / 2
    };
    if width <= 0 || height <= 0 || width > 512 || height > 512 {
        return Err("Unsupported cursor dimensions".into());
    }
    let x = cursor.ptScreenPos.x - area.left - icons.0.xHotspot as i32;
    let y = cursor.ptScreenPos.y - area.top - icons.0.yHotspot as i32;
    let left = x.max(0);
    let top = y.max(0);
    let right = (x + width).min(area.width as i32);
    let bottom = (y + height).min(area.height as i32);
    if left >= right || top >= bottom {
        return Ok(());
    }
    let w = right - left;
    let h = bottom - top;
    let dc = unsafe { CreateCompatibleDC(std::ptr::null_mut()) };
    if dc.is_null() {
        return Err("Cannot create cursor compositor".into());
    }
    let info = BITMAPINFO {
        bmiHeader: BITMAPINFOHEADER {
            biSize: size_of::<BITMAPINFOHEADER>() as u32,
            biWidth: w,
            biHeight: -h,
            biPlanes: 1,
            biBitCount: 32,
            ..Default::default()
        },
        ..Default::default()
    };
    let mut bits = std::ptr::null_mut();
    let bitmap = unsafe {
        CreateDIBSection(
            dc,
            &info,
            DIB_RGB_COLORS,
            &mut bits,
            std::ptr::null_mut(),
            0,
        )
    };
    if bitmap.is_null() {
        unsafe {
            DeleteDC(dc);
        }
        return Err("Cannot allocate cursor compositor".into());
    }
    let old = unsafe { SelectObject(dc, bitmap) };
    let patch =
        unsafe { std::slice::from_raw_parts_mut(bits.cast::<u8>(), w as usize * h as usize * 4) };
    for row in 0..h as usize {
        for col in 0..w as usize {
            let source = (((top as usize + row) * area.width as usize) + left as usize + col) * 4;
            let target = (row * w as usize + col) * 4;
            patch[target..target + 4].copy_from_slice(&[
                pixels[source + 2],
                pixels[source + 1],
                pixels[source],
                255,
            ]);
        }
    }
    let drawn = unsafe {
        DrawIconEx(
            dc,
            x - left,
            y - top,
            cursor.hCursor,
            0,
            0,
            0,
            std::ptr::null_mut(),
            DI_NORMAL,
        )
    };
    unsafe {
        GdiFlush();
    }
    for row in 0..h as usize {
        for col in 0..w as usize {
            let target = (((top as usize + row) * area.width as usize) + left as usize + col) * 4;
            let source = (row * w as usize + col) * 4;
            pixels[target..target + 4].copy_from_slice(&[
                patch[source + 2],
                patch[source + 1],
                patch[source],
                255,
            ]);
        }
    }
    unsafe {
        SelectObject(dc, old);
        DeleteObject(bitmap);
        DeleteDC(dc);
    }
    if drawn == 0 {
        return Err("Cannot composite the Windows cursor".into());
    }
    Ok(())
}

#[cfg(test)]
mod cursor_tests {
    use super::*;
    use windows_sys::Win32::Foundation::POINT;

    struct TestCursor(HCURSOR);
    impl Drop for TestCursor {
        fn drop(&mut self) {
            unsafe {
                DestroyCursor(self.0);
            }
        }
    }

    fn shape(and_mask: u8, xor_mask: u8) -> TestCursor {
        // 16x16 monochrome square; hotspot deliberately off-centre.
        let and_plane = [and_mask; 32];
        let xor_plane = [xor_mask; 32];
        let handle = unsafe {
            CreateCursor(
                std::ptr::null_mut(),
                3,
                5,
                16,
                16,
                and_plane.as_ptr().cast(),
                xor_plane.as_ptr().cast(),
            )
        };
        assert!(!handle.is_null(), "{}", std::io::Error::last_os_error());
        TestCursor(handle)
    }

    fn verify(position: (i32, i32), showing: bool) {
        verify_pattern(position, showing, 0, 0);
    }

    fn verify_pattern(position: (i32, i32), showing: bool, and_mask: u8, xor_mask: u8) {
        let cursor = shape(and_mask, xor_mask);
        let area = Area {
            left: -300,
            top: -200,
            width: 40,
            height: 32,
        };
        let mut pixels = [113u8, 130, 147, 255].repeat(40 * 32);
        let state = CURSORINFO {
            cbSize: size_of::<CURSORINFO>() as u32,
            flags: if showing { CURSOR_SHOWING } else { 0 },
            hCursor: cursor.0,
            ptScreenPos: POINT {
                x: area.left + position.0,
                y: area.top + position.1,
            },
        };
        composite_cursor_state(&mut pixels, &area, &state).unwrap();
        for y in 0..32i32 {
            for x in 0..40i32 {
                let covered = showing
                    && (position.0 - 3..position.0 + 13).contains(&x)
                    && (position.1 - 5..position.1 + 11).contains(&y);
                let expected = if covered {
                    [
                        (113 & and_mask) ^ xor_mask,
                        (130 & and_mask) ^ xor_mask,
                        (147 & and_mask) ^ xor_mask,
                        255,
                    ]
                } else {
                    [113, 130, 147, 255]
                };
                let offset = (y as usize * 40 + x as usize) * 4;
                assert_eq!(&pixels[offset..offset + 4], &expected, "pixel {x},{y}");
            }
        }
    }

    #[test]
    fn native_cursor_respects_hotspot_and_negative_monitor_origin() {
        verify((12, 14), true);
    }

    #[test]
    fn native_cursor_clips_each_edge_without_changing_other_pixels() {
        for position in [(0, 12), (39, 12), (12, 0), (12, 31), (0, 0), (39, 31)] {
            verify(position, true);
        }
    }

    #[test]
    fn hidden_and_outside_cursors_leave_source_unchanged() {
        verify((12, 14), false);
        for position in [(-32, 12), (64, 12), (12, -32), (12, 64)] {
            verify(position, true);
        }
    }

    #[test]
    fn native_monochrome_white_transparency_and_inversion_preserve_channels() {
        for (and_mask, xor_mask) in [(0, 255), (255, 0), (255, 255)] {
            verify_pattern((12, 14), true, and_mask, xor_mask);
            verify_pattern((0, 0), true, and_mask, xor_mask);
        }
    }
}

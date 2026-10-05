# Capture allocation audit

Source inspected September 23, 2026 against Cargo.lock: windows-capture 2.0.1,
png 0.18.0. This is ownership evidence, not a measured total GPU-memory budget.

## Known allocations

- `flow-engine/src/recording.rs`: two reusable RGBA vectors circulate through the
  capture/encoding/commit queues. `flow-capture-windows/src/native.rs` additionally
  retains the previous cursor-inclusive image for exact deduplication. The existing
  `pool_bytes` counter reserves three images; it is not total process memory.
- windows-capture `graphics_capture_api.rs`: the WGC frame pool is created with
  one full-monitor surface. Logical RGBA payload is monitor width × height × 4;
  this does not establish driver allocation size, residency or hidden copies.
- windows-capture `Frame::buffer_crop` creates a staging texture per sampled
  crop, then maps it. Its row pitch may exceed width × 4. This texture is outside
  the reusable CPU-vector budget. Mapping is RAII-owned by FrameBuffer.
- The cursor compositor creates a clipped GDI bitmap plus shape/mask handles.
  Descriptors, journal metadata and compression state are also outside the pool.

## PNG output correction

Previously png `Writer::write_image_data` accumulated compressed output in a Vec.
Its fast-compression fallback can also construct a stored-only output while the
first compressed buffer is still live. Two concurrent encoding workers can each
incur these allocations. Uniform-color endurance does not stress that case.

Recording now uses png `stream_writer_with_size(65536)` and explicitly finishes
both stream and PNG writer before flushing, publishing and journaling the file.
The pinned implementation uses three row buffers, a bounded chunk buffer and the
same Fdeflate fast compressor. It no longer retains full-image compressed output.
The 64 KiB figure bounds the output chunk, NOT total compressor memory.

A deterministic noisy-image regression failed on the old whole-image writer and
passes on streaming: each output write is at most 64 KiB and decoded RGBA pixels
match exactly. Additional tests reject incomplete/excess input and storage failures
at the signature and after IHDR. Existing pipeline failure/recovery tests still
apply. Source images from existing recordings are never rewritten.

## Measured accounting and verification limits

- Encoder-only Rust heap is now measured by the opt-in
  `scripts/rebuild_encoder_memory.ps1`. A test-only allocator delegates unchanged
  to System and counts requested sizes on the encoding thread. Input vectors are
  created outside the scope, output goes to a sink, and the encoder must destroy
  all scoped allocations before measurement ends. Ordinary shipping binaries do
  not contain the probe. Eighteen release cases cover three sizes, flat/noisy
  content and three repetitions. At width 2560, peak requested heap was 96,256
  bytes regardless of height (100 or 1600) or image entropy; each scope ended at
  zero live bytes. This excludes allocator overhead, stack, OS and disk I/O.
  Evidence: `.tmp/rebuild/encoder-memory-ca0ebec55c544ec39cc831237157f151/`.
- Native metrics now separately report `staging_mapped_peak_bytes` (observed row
  pitch times height), `staging_row_pitch_peak_bytes`, and
  `wgc_surface_payload_bytes` (nominal one-surface RGBA content). Fake capture
  reports null for unavailable native measurements. The map is released after
  copying pixels, before descriptor/cursor processing.
  Native evidence: `.tmp/rebuild/native-check-eadfc4bb9fc64d1e9b5a6b1cd8f65a78/`
  measured a 10,240-byte row pitch and 16,384,000-byte staging peak at 2560 x 1600,
  separately from the 49,152,000-byte pool reservation and equal-size nominal
  WGC surface payload. All 41 sampling opportunities were serviced in the short run.
- Native capture now queries IDXGIAdapter3::QueryVideoMemoryInfo for node 0 on
  the actual capture device's adapter, while staging is mapped. Local and nonlocal
  process usage and budgets are sampled separately from logical texture payloads.
  A bounded 256-sample history retains timing, with all-time observed peaks and
  failed-query counts. Missing adapter/query support does not stop recording;
  unavailable readings are null, not zero.
  Microsoft defines CurrentUsage as application video-memory usage:
  https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_4/ns-dxgi1_4-dxgi_query_video_memory_info
  These are sampled engine-process figures on adapter node 0, not individual
  texture sizes, system-wide GPU usage, the shell's GPU usage or unsampled peaks.
  Detailed per-resource residency attribution would require a native profiler.
  Short native validation returned 40 successful samples on Intel UHD node 0:
  local process peak 34,136,064 bytes and nonlocal peak 0, with no failed queries.
  Evidence: `.tmp/rebuild/native-check-ef10981e68304e21817a33a59bfbec63/`.
  This fulfills separate GPU-usage accounting at process/adapter granularity;
  per-resource residency analysis is not claimed. Long-duration native stability
  cannot be inferred from this short recording.
- Repeat short native cadence and stop/review tests for the streaming encoder,
  within the 30-second whole-process cap. No new performance claim is made yet.
- The updated packaged playground passed a five-second native 2560 x 1600
  changing-color check: 40 samples, zero missed opportunities, 34 unique PNGs.
  Evidence: `.tmp/rebuild/native-check-f76fe160d220445db5d27e9b1d82a969/`.
  This is a native smoke result, not noisy-frame allocation or repeated cadence proof.

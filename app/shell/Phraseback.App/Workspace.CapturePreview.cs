using Avalonia.Media.Imaging;
using Phraseback.Client;

namespace Phraseback.App;

public sealed partial class Workspace
{
    private Bitmap? capturePreview;
    private bool captureDecoding;
    private long capturePreviewVersion;
    private string? capturePreviewPath;
    public int CapturePreviewCount { get; private set; }
    public Bitmap? CapturePreview => capturePreview;
    public bool HasCapturePreview => capturePreview is not null;

    private void ClearCapturePreview()
    {
        ++capturePreviewVersion; capturePreviewPath = null;
        var previous = capturePreview; capturePreview = null;
        Raise(nameof(CapturePreview)); Raise(nameof(HasCapturePreview)); previous?.Dispose();
    }
    private async Task LoadCapturePreviewAsync(OperationStatus operation)
    {
        if (operation.Kind != "preparing_capture" || operation.Finished || captureDecoding || disposed
            || operation.Result is not { } result || !result.TryGetProperty("capture_preview", out var reference)) return;
        var path = reference.GetString();
        if (path is null || capturePreviewPath == path) return;
        var width = result.GetProperty("capture_width").GetInt64(); var height = result.GetProperty("capture_height").GetInt64();
        if (width <= 0 || height <= 0) return;
        // One in-flight decoder, at most 4 MiB per image; the prior image is released on replacement.
        var previewWidth = (int)Math.Clamp(Math.Min(width, Math.Min(1024, 1024 * width / height)), 1, 1024);
        captureDecoding = true; var version = capturePreviewVersion;
        try
        {
            var bitmap = await Task.Run(() => { using var stream = File.OpenRead(path); return Bitmap.DecodeToWidth(stream, previewWidth); });
            if (disposed || version != capturePreviewVersion || !Busy || operationId != operation.Id) { bitmap.Dispose(); return; }
            var previous = capturePreview; capturePreview = bitmap; capturePreviewPath = path; CapturePreviewCount++;
            Raise(nameof(CapturePreview)); Raise(nameof(HasCapturePreview)); previous?.Dispose();
        }
        catch (IOException) { /* A disposable preview failure cannot stop evidence capture. */ }
        catch (ArgumentException) { }
        finally { captureDecoding = false; }
    }
}

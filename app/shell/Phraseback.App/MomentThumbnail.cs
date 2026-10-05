using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Phraseback.App;

/// <summary>A realized row owns its image. Recycling cancels work and releases pixels.</summary>
public sealed class MomentThumbnail : Image
{
    private CancellationTokenSource? pending;
    private ThumbnailLease? lease;
    private bool attached;
    public Task Loading { get; private set; } = Task.CompletedTask;
    public MomentThumbnail()
    {
        Width = 80; Height = 45; Stretch = Stretch.Uniform;
        DataContextChanged += (_, _) => Refresh();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    { base.OnAttachedToVisualTree(e); attached = true; Refresh(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { attached = false; Refresh(); base.OnDetachedFromVisualTree(e); }
    private void Refresh()
    {
        pending?.Cancel(); pending?.Dispose(); pending = null;
        Source = null; lease?.Dispose(); lease = null;
        if (!attached || DataContext is not Moment { Owner: { } owner } moment) return;
        pending = new CancellationTokenSource();
        Loading = LoadAsync(owner, moment, pending.Token);
    }
    private async Task LoadAsync(Workspace owner, Moment moment, CancellationToken cancellation)
    {
        try
        {
            var loaded = await owner.LoadThumbnailAsync(moment, cancellation);
            if (cancellation.IsCancellationRequested || !attached) { loaded?.Dispose(); return; }
            lease = loaded; Source = loaded?.Image;
        }
        catch (Exception) { /* Missing thumbnails must not block original evidence or editing. */ }
    }
}

public sealed class ThumbnailLease : IDisposable
{
    public Bitmap Image { get; }
    private Action<ThumbnailLease>? release;
    internal ThumbnailLease(Bitmap image, Action<ThumbnailLease> release) { Image = image; this.release = release; }
    public void Dispose()
    {
        var callback = release; release = null;
        if (callback is null) return;
        Image.Dispose(); callback(this);
    }
}

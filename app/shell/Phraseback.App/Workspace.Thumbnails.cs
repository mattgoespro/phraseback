using Avalonia.Media.Imaging;
using Phraseback.Client;

namespace Phraseback.App;

public sealed partial class Workspace
{
    // Reserve 3 MiB of the 128 MiB decoded-review budget for realized thumbnails.
    // At most 32 images (160 x 90 x 4 bytes each), with one decoder in flight.
    private readonly SemaphoreSlim thumbnailDecoder = new(1);
    private readonly HashSet<ThumbnailLease> thumbnails = [];
    private int thumbnailReservations;
    public int ThumbnailCount => thumbnails.Count;
    public long ThumbnailBytes => thumbnails.Sum(value => (long)value.Image.PixelSize.Width * value.Image.PixelSize.Height * 4);

    public async Task<ThumbnailLease?> LoadThumbnailAsync(Moment moment, CancellationToken cancellation = default)
    {
        var project = snapshot;
        var client = engine;
        if (disposed || !connected || client is null || project is null || moment.RecordingId != project.RecordingId
            || !Moments.Contains(moment) || thumbnailReservations >= 32) return null;
        thumbnailReservations++;
        var retained = false;
        try
        {
            await thumbnailDecoder.WaitAsync(cancellation);
            try
            {
                if (disposed || cancellation.IsCancellationRequested || snapshot != project) return null;
                // Do not cancel a transmitted protocol command when a row is recycled.
                var reference = await client.RequestAsync<ImageReference>("frame", new { recording_id = project.RecordingId, index = moment.Step.Frame, preview_width = 160 });
                if (disposed || cancellation.IsCancellationRequested || snapshot != project || reference.Revision != project.Revision
                    || reference.RecordingId != project.RecordingId || reference.Index != moment.Step.Frame) return null;
                Task<Bitmap> DecodeThumbnailAsync(string path) => Task.Run(() =>
                {
                    using var stream = File.OpenRead(path);
                    return project.Project.Height * 160L > project.Project.Width * 90L
                        ? Bitmap.DecodeToHeight(stream, 90) : Bitmap.DecodeToWidth(stream, 160);
                });
                Bitmap image;
                try { image = await DecodeThumbnailAsync(reference.Path); }
                catch
                {
                    if (disposed || cancellation.IsCancellationRequested || snapshot != project) return null;
                    reference = await client.RequestAsync<ImageReference>("frame", new { recording_id = project.RecordingId, index = moment.Step.Frame });
                    if (disposed || cancellation.IsCancellationRequested || snapshot != project || reference.Revision != project.Revision) return null;
                    image = await DecodeThumbnailAsync(reference.Path);
                }
                if (disposed || cancellation.IsCancellationRequested || snapshot != project || !Moments.Contains(moment)
                    || image.PixelSize.Width > 160 || image.PixelSize.Height > 90)
                { image.Dispose(); return null; }
                var lease = new ThumbnailLease(image, value => { thumbnails.Remove(value); thumbnailReservations--; });
                thumbnails.Add(lease); retained = true;
                return lease;
            }
            finally { thumbnailDecoder.Release(); }
        }
        finally { if (!retained) thumbnailReservations--; }
    }
}

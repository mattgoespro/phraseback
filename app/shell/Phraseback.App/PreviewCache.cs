using Avalonia.Media.Imaging;

namespace Phraseback.App;

/// <summary>UI-thread-owned decoded-image LRU with a configurable pixel-buffer budget.</summary>
public sealed class PreviewCache(long budget = 128L * 1024 * 1024) : IDisposable
{
    private readonly Dictionary<string, LinkedListNode<(string Key, Bitmap Image, long Bytes)>> entries = [];
    private readonly LinkedList<(string Key, Bitmap Image, long Bytes)> recent = [];
    public long Bytes { get; private set; }
    public long PeakBytes { get; private set; }
    public long Evictions { get; private set; }
    public int Count => entries.Count;
    private void Touch(Bitmap? image)
    {
        if (image is null) return;
        var node = recent.First;
        while (node is not null)
        {
            if (ReferenceEquals(node.Value.Image, image)) { recent.Remove(node); recent.AddFirst(node); return; }
            node = node.Next;
        }
    }
    public bool TryPutPrefetched(string key, Bitmap image, Bitmap? displayed)
    {
        var bytes = checked((long)image.PixelSize.Width * image.PixelSize.Height * 4);
        var pinned = displayed is null ? 0 : checked((long)displayed.PixelSize.Width * displayed.PixelSize.Height * 4);
        if (bytes + pinned > budget || entries.ContainsKey(key)) return false;
        Touch(displayed);
        Put(key, image);
        return true;
    }
    public Bitmap? Get(string key)
    {
        if (!entries.TryGetValue(key, out var node)) return null;
        recent.Remove(node); recent.AddFirst(node); return node.Value.Image;
    }
    public void Put(string key, Bitmap bitmap)
    {
        var bytes = checked((long)bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4);
        if (bytes > budget) throw new InvalidOperationException("Preview exceeds the decoded image budget.");
        if (entries.ContainsKey(key)) throw new InvalidOperationException("Preview is already cached.");
        while (Bytes + bytes > budget && recent.Last is { } oldest)
        {
            entries.Remove(oldest.Value.Key); recent.RemoveLast(); Bytes -= oldest.Value.Bytes;
            oldest.Value.Image.Dispose();
            Evictions++;
        }
        entries.Add(key, recent.AddFirst((key, bitmap, bytes))); Bytes += bytes;
        PeakBytes = Math.Max(PeakBytes, Bytes);
    }
    public void Dispose()
    {
        foreach (var entry in recent) entry.Image.Dispose();
        entries.Clear(); recent.Clear(); Bytes = 0;
    }
}

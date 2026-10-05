using Phraseback.Client;

namespace Phraseback.App;

public sealed partial class Workspace
{
    private string? catalogId;
    private int? libraryOffset;
    private long libraryVersion;
    private bool libraryLoading;
    public bool HasMoreRecordings => libraryOffset is not null;
    public bool CanLoadMore => HasMoreRecordings && connected && !disposed && !libraryLoading;

    private async Task<LibraryPage> ReadLibraryPageAsync(EngineClient client, int offset, string? cursor, bool refresh)
    {
        while (!disposed)
        {
            var page = await client.RequestAsync<LibraryPage>("library_page", new { offset, limit = 64, catalog_id = cursor, refresh });
            if (!page.Indexing) return page;
            cursor = page.CatalogId; refresh = false;
            await Task.Delay(75);
        }
        throw new OperationCanceledException("Workspace closed during library indexing.");
    }

    private void ApplyLibraryPage(LibraryPage page, bool replace)
    {
        catalogId = page.CatalogId; libraryOffset = page.NextOffset;
        if (replace) Library.Clear();
        foreach (var entry in page.Items)
            if (!Library.Any(existing => existing.Id == entry.Id))
                Library.Add(snapshot?.RecordingId == entry.Id
                    ? entry with { Title = snapshot.Project.Title, DurationMs = snapshot.Project.DurationMs, Steps = snapshot.Project.Steps.Length }
                    : entry);
        Raise(nameof(HasMoreRecordings)); Raise(nameof(CanLoadMore));
    }

    public Task LoadMoreRecordingsAsync() => LoadLibraryAsync(false);
    public Task RefreshLibraryAsync() => LoadLibraryAsync(true);
    private async Task LoadLibraryAsync(bool refresh)
    {
        if (!connected || engine is null || disposed || (!refresh && (!CanLoadMore || libraryOffset is null))) return;
        var client = engine;
        var version = ++libraryVersion;
        libraryLoading = true; Raise(nameof(CanLoadMore));
        try
        {
            var page = await ReadLibraryPageAsync(client, refresh ? 0 : libraryOffset!.Value, refresh ? null : catalogId, refresh);
            if (disposed || version != libraryVersion || !ReferenceEquals(client, engine)) return;
            ApplyLibraryPage(page, refresh);
        }
        catch (Exception ex) { if (!disposed && version == libraryVersion && ReferenceEquals(client, engine)) Error = $"Library unavailable: {ex.Message}"; }
        finally { if (version == libraryVersion) { libraryLoading = false; Raise(nameof(CanLoadMore)); } }
    }
}

using Phraseback.Client;

namespace Phraseback.App;

public sealed partial class Workspace
{
    public Task<PromptTemplate> LoadPromptTemplateAsync() => engine is null
        ? throw new InvalidOperationException("Engine unavailable")
        : engine.RequestAsync<PromptTemplate>("prompt_template", new EmptyRequest());

    public Task<PromptTemplate> SavePromptTemplateAsync(string template) => engine is null
        ? throw new InvalidOperationException("Engine unavailable")
        : engine.RequestAsync<PromptTemplate>("save_prompt_template", new SavePromptTemplateRequest(template));

    public async Task<RenderedPrompt?> RenderPromptAsync(string? template = null, bool flush = false)
    {
        if (!connected || engine is null || snapshot is null || disposed) return null;
        if (flush && !await FlushAsync()) return null;
        var expected = snapshot;
        var reply = await engine.RequestAsync<RenderedPrompt>("render_prompt", new RenderPromptRequest(expected.RecordingId, (ulong)expected.Revision, template));
        return snapshot?.RecordingId == reply.RecordingId && (ulong)snapshot.Revision == reply.Revision ? reply : null;
    }
}

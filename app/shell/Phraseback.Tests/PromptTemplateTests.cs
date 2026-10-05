using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Phraseback.App;
using Phraseback.Client;
using Xunit;

namespace Phraseback.Tests;

public sealed class PromptTemplateTests
{
    [AvaloniaFact]
    public async Task TemplateDraftSurvivesNavigationAndLatestPreviewWins()
    {
        using var fixture = new Fixture();
        await using var workspace = new Workspace();
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        var original = await workspace.LoadPromptTemplateAsync();
        var view = new PromptSettingsView(workspace, () => { });
        await view.LoadAsync();
        var host = new Window { Width = 1024, Height = 700, Content = view };
        host.Show();
        try
        {
            view.Editor.Text = "Older preview";
            view.Editor.Text = "Latest preview";
            await Task.Delay(400);
            Assert.Equal("Latest preview", view.PreviewText);
            host.Content = new TextBlock { Text = "Other workspace" };
            host.Content = view; await view.LoadAsync();
            Assert.Equal("Latest preview", view.Editor.Text);
            Assert.Equal(original.Template, (await workspace.LoadPromptTemplateAsync()).Template);
        }
        finally { host.Close(); }
    }
    [Theory]
    [InlineData("moments", "{{#moments}}\n\n{{/moments}}", 13)]
    [InlineData("if task", "{{#if task}}\n\n{{/if}}", 13)]
    [InlineData("title", "{{title}}", 9)]
    public void CompletionInsertsPairsAndPositionsCaret(string choice, string expected, int caret)
    {
        var edit = TemplateCompletion.Insert("{{", 2, 2, choice);
        Assert.Equal(expected, edit.Text);
        Assert.Equal(caret, edit.Caret);
    }

    [Fact]
    public void CompletionWorksInAnEmptyEditorAndReplacesAnExistingToken()
    {
        Assert.Equal(("{{task}}", 8), TemplateCompletion.Insert("", 0, 0, "task"));
        Assert.Equal(("A {{task}} Z", 10), TemplateCompletion.Insert("A {{title}} Z", 6, 6, "task"));
    }

    [Fact]
    public async Task CopyRendererAndExportUseTheSameSavedTemplate()
    {
        using var fixture = new Fixture();
        await using var workspace = new Workspace();
        await workspace.ConnectAsync(Fixture.Engine, fixture.Root, null);
        Assert.True(await workspace.EditProjectAsync("Fixture", "", "Fix workflow"));
        await workspace.SavePromptTemplateAsync("{{title}}: {{task}} {{#moments}}{{review_note}}{{/moments}}");
        var prompt = await workspace.RenderPromptAsync(flush: true);
        var exports = Path.Combine(fixture.Root, "exports"); Directory.CreateDirectory(exports);
        await workspace.ExportAsync(exports);
        Assert.Empty(workspace.Error);
        Assert.NotNull(workspace.ExportDestination);
        Assert.Equal(prompt!.Markdown, File.ReadAllText(Path.Combine(workspace.ExportDestination!, "transcript.md")));
        Assert.True(File.Exists(Path.Combine(workspace.ExportDestination!, "screenshots", "001.png")));
    }

    [Fact]
    public async Task TaskSaveDoesNotInvalidateDescriptionsAndTemplateIsShared()
    {
        using var fixture = new Fixture();
        await using var client = await EngineClient.StartAsync(Fixture.Engine, fixture.Root);
        var snapshot = await client.RequestSnapshotAsync("open_project", new { recording_id = "sample" }, TestContext.Current.CancellationToken);
        snapshot = await client.RequestSnapshotAsync("edit_step", new { recording_id = snapshot.RecordingId, revision = snapshot.Revision, step_id = snapshot.Project.Steps[0].Id, title = "Step", action = "Click", result = "Open", uncertainty = "" }, TestContext.Current.CancellationToken);
        snapshot = await client.RequestSnapshotAsync("review_step", new { recording_id = snapshot.RecordingId, revision = snapshot.Revision, step_id = snapshot.Project.Steps[0].Id, reviewed = true }, TestContext.Current.CancellationToken);
        var original = snapshot.Project.Steps[0];
        snapshot = await client.RequestSnapshotAsync("edit_project", new { recording_id = snapshot.RecordingId, revision = snapshot.Revision, title = snapshot.Project.Title, context = snapshot.Project.Context, task = "Fix workflow" }, TestContext.Current.CancellationToken);
        Assert.Equal(original, snapshot.Project.Steps[0]);
        Assert.Equal("Fix workflow", snapshot.Project.Task);
        await client.RequestAsync<JsonElement>("save_prompt_template", new { template = "{{task}}: {{#moments}}{{action}}{{/moments}}" }, TestContext.Current.CancellationToken);
        var prompt = await client.RequestAsync<JsonElement>("render_prompt", new { recording_id = snapshot.RecordingId, revision = snapshot.Revision }, TestContext.Current.CancellationToken);
        Assert.Equal("Fix workflow: Click", prompt.GetProperty("markdown").GetString());
        await Assert.ThrowsAsync<EngineException>(() => client.RequestAsync<JsonElement>("render_prompt", new { recording_id = snapshot.RecordingId, revision = snapshot.Revision - 1 }, TestContext.Current.CancellationToken));
        await client.RequestAsync<JsonElement>("reset_prompt_template", new EmptyRequest(), TestContext.Current.CancellationToken);
        prompt = await client.RequestAsync<JsonElement>("render_prompt", new { recording_id = snapshot.RecordingId, revision = snapshot.Revision }, TestContext.Current.CancellationToken);
        Assert.Contains("## Task", prompt.GetProperty("markdown").GetString());
    }
}

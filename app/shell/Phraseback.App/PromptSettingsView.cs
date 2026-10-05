using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Phraseback.App;

internal sealed class PromptSettingsView : UserControl
{
    private readonly Workspace workspace;
    internal readonly TemplateEditor Editor = new();
    private readonly TextBox preview = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private string? saved, defaults;
    private CancellationTokenSource? debounce;
    private long previewVersion;
    private bool loading;
    internal string? PreviewText => preview.Text;
    public PromptSettingsView(Workspace workspace, Action savedChanged)
    {
        this.workspace = workspace;
        var save = new Button { Content = "Save", Classes = { "accent" } };
        var reset = new Button { Content = "Reset to default" };
        var tokens = new ComboBox { ItemsSource = TemplateCompletion.Fields.Concat(["moments", "if…"]).ToArray(), SelectedIndex = 0, Width = 160 };
        var insert = new Button { Content = "Insert token" };
        var conditions = new ComboBox { ItemsSource = TemplateCompletion.Fields, SelectedIndex = 0, Width = 140, IsVisible = false };
        tokens.SelectionChanged += (_, _) => conditions.IsVisible = tokens.SelectedItem as string == "if…";
        insert.Click += (_, _) => { if (tokens.SelectedItem is string token) Editor.Insert(token == "if…" ? "if " + conditions.SelectedItem : token); };
        reset.Click += (_, _) => { if (defaults is not null) Editor.Text = defaults; };
        save.Click += async (_, _) =>
        {
            save.IsEnabled = false;
            try { var reply = await workspace.SavePromptTemplateAsync(Editor.Text); saved = reply.Template; status.Text = Editor.Text == saved ? "Saved · preview, Copy and export use this template." : "Unsaved draft · retained while navigating this session"; savedChanged(); }
            catch (Exception ex) { status.Text = "Not saved: " + ex.Message; }
            finally { save.IsEnabled = true; }
        };
        Editor.Edited += () =>
        {
            if (loading) return;
            status.Text = Editor.Text == saved ? "Saved template" : "Unsaved draft · retained while navigating this session";
            QueuePreview();
        };
        var toolbar = new WrapPanel { Children = { tokens, conditions, insert, save, reset } };
        foreach (var child in toolbar.Children) child.Margin = new Thickness(0, 0, 8, 5);
        var left = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), RowSpacing = 10 };
        left.Children.Add(new TextBlock { Text = "Prompt format", FontSize = 24, FontWeight = FontWeight.SemiBold });
        Grid.SetRow(toolbar, 1); left.Children.Add(toolbar); Grid.SetRow(Editor, 2); left.Children.Add(Editor); Grid.SetRow(status, 3); left.Children.Add(status);
        var right = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 10 };
        right.Children.Add(new TextBlock { Text = "Live preview", FontSize = 18, FontWeight = FontWeight.SemiBold });
        Grid.SetRow(preview, 1); right.Children.Add(preview);
        var guide = new TextBlock { Text = "{{token}} inserts a value.\n{{#moments}} … {{/moments}} repeats selected moments.\n{{#if field}} … {{/if}} includes nonempty fields.\n\nDocument: title, task, context, duration, recording_path.\nMoment: number, title, time, action, result, uncertainty, review_note, screenshot_path.\n\nInside moments, title is the moment title. Unknown or unfinished syntax stays literal. Type {{ for autocomplete; Enter or Tab inserts. Block markers are paired automatically.", TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        var guideScroll = new ScrollViewer { Content = guide, MaxHeight = 180 };
        var guideDisclosure = new Expander { Header = "Token guide", Content = guideScroll, IsExpanded = true, HorizontalAlignment = HorizontalAlignment.Stretch };
        Grid.SetRow(guideDisclosure, 2); right.Children.Add(guideDisclosure);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("1.2*,20,*"), Margin = new Thickness(28, 0, 28, 24) };
        grid.Children.Add(left); Grid.SetColumn(right, 2); grid.Children.Add(right); Content = grid;
        AttachedToVisualTree += (_, _) => QueuePreview();
        DetachedFromVisualTree += (_, _) => { debounce?.Cancel(); ++previewVersion; };
    }
    public async Task LoadAsync()
    {
        if (saved is not null) return;
        var reply = await workspace.LoadPromptTemplateAsync(); saved = reply.Template; defaults = reply.DefaultTemplate;
        loading = true; Editor.Text = saved; loading = false; status.Text = "Saved template"; QueuePreview();
    }
    private void QueuePreview()
    {
        debounce?.Cancel(); debounce?.Dispose(); debounce = new CancellationTokenSource();
        _ = PreviewAsync(debounce.Token, ++previewVersion);
    }
    private async Task PreviewAsync(CancellationToken cancellation, long version)
    {
        try
        {
            await Task.Delay(220, cancellation);
            var result = await workspace.RenderPromptAsync(Editor.Text);
            if (version != previewVersion || cancellation.IsCancellationRequested) return;
            preview.Text = result?.Markdown ?? "Record or open a recording to preview this template with your evidence.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (version == previewVersion) preview.Text = "Preview unavailable: " + ex.Message; }
    }
}

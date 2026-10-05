using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Phraseback.App;

internal static class TemplateCompletion
{
    public static readonly string[] Fields = ["title", "task", "context", "duration", "recording_path", "number", "time", "action", "result", "uncertainty", "review_note", "screenshot_path"];
    public static (string Text, int Caret) Insert(string text, int start, int end, string choice)
    {
        start = Math.Clamp(start, 0, text.Length); end = Math.Clamp(end, start, text.Length);
        var opening = text.LastIndexOf("{{", Math.Max(0, start - 1), StringComparison.Ordinal);
        if (opening >= 0 && !text[opening..start].Contains("}}", StringComparison.Ordinal))
        {
            start = opening;
            var close = text.IndexOf("}}", end, StringComparison.Ordinal);
            if (close >= end && !text[end..close].Contains("{{", StringComparison.Ordinal) && !text[end..close].Contains('\n')) end = close + 2;
        }
        if (text.AsSpan(end).StartsWith("}}")) end += 2;
        var insert = choice == "moments" ? "{{#moments}}\n\n{{/moments}}"
            : choice.StartsWith("if ", StringComparison.Ordinal) ? "{{#" + choice + "}}\n\n{{/if}}" : "{{" + choice + "}}";
        var caret = insert.Contains('\n') ? insert.IndexOf('\n') + 1 : insert.Length;
        return (text[..start] + insert + text[end..], start + caret);
    }
    public static bool Recognized(string value) => Fields.Contains(value) || value is "#moments" or "/moments" or "/if"
        || value.StartsWith("#if ", StringComparison.Ordinal) && Fields.Contains(value[4..].Trim());
}

/// <summary>Native text editing, selection and caret with a synchronized coloured text layer.</summary>
internal sealed class TemplateEditor : UserControl
{
    internal readonly TextBox Input = new() { AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap,
        FontFamily = new FontFamily("Cascadia Mono,Consolas"), FontSize = 13, LineHeight = 21, Padding = new Thickness(10),
        Background = Brushes.Transparent, Foreground = Brushes.Transparent, SelectionForegroundBrush = Brushes.Transparent,
        CaretBrush = new SolidColorBrush(Color.Parse("#F1EDF5")), BorderThickness = new Thickness(0), MinHeight = 260 };
    private readonly TextBlock colors = new() { IsHitTestVisible = false, FontFamily = new FontFamily("Cascadia Mono,Consolas"),
        FontSize = 13, LineHeight = 21, Padding = new Thickness(10), TextWrapping = TextWrapping.NoWrap };
    private readonly ListBox suggestions = new() { MaxHeight = 120, IsVisible = false };
    private long version;
    public event Action? Edited;
    public string Text { get => Input.Text ?? ""; set => Input.Text = value; }
    public TemplateEditor()
    {
        Avalonia.Automation.AutomationProperties.SetName(Input, "Prompt template editor");
        Avalonia.Automation.AutomationProperties.SetName(suggestions, "Template autocomplete");
        ScrollViewer.SetHorizontalScrollBarVisibility(Input, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(Input, ScrollBarVisibility.Disabled);
        var layers = new Grid(); layers.Children.Add(Input); layers.Children.Add(colors);
        var scroll = new ScrollViewer { Content = layers, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 260 };
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        grid.Children.Add(scroll); Grid.SetRow(suggestions, 1); grid.Children.Add(suggestions);
        var surface = new Border { CornerRadius = new CornerRadius(7), Child = grid };
        surface.Bind(Border.BackgroundProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("FieldBrush")); Content = surface;
        Input.TextChanged += (_, _) => { Edited?.Invoke(); _ = HighlightAsync(); UpdateSuggestions(); };
        Input.PropertyChanged += (_, e) => { if (e.Property == TextBox.CaretIndexProperty) UpdateSuggestions(); };
        suggestions.DoubleTapped += (_, _) => AcceptSuggestion();
        Input.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (!suggestions.IsVisible) return;
            if (e.Key == Key.Escape) { suggestions.IsVisible = false; e.Handled = true; }
            else if (e.Key is Key.Enter or Key.Tab) { AcceptSuggestion(); e.Handled = true; }
            else if (e.Key is Key.Down or Key.Up) { suggestions.SelectedIndex = Math.Clamp(suggestions.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, suggestions.ItemCount - 1); e.Handled = true; }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }
    public void Insert(string choice)
    {
        var edit = TemplateCompletion.Insert(Text, Math.Min(Input.SelectionStart, Input.SelectionEnd), Math.Max(Input.SelectionStart, Input.SelectionEnd), choice);
        Text = edit.Text; Input.CaretIndex = edit.Caret; Input.SelectionStart = Input.SelectionEnd = edit.Caret;
        suggestions.IsVisible = false; Input.Focus();
    }
    private void AcceptSuggestion() { if (suggestions.SelectedItem is string choice) Insert(choice); }
    private void UpdateSuggestions()
    {
        var caret = Math.Clamp(Input.CaretIndex, 0, Text.Length);
        var prefix = Text[..caret]; var opening = prefix.LastIndexOf("{{", StringComparison.Ordinal);
        if (opening < 0 || prefix[opening..].Contains("}}", StringComparison.Ordinal)) { suggestions.IsVisible = false; return; }
        var query = prefix[(opening + 2)..].Trim().TrimStart('#');
        if (query.Contains('\n')) { suggestions.IsVisible = false; return; }
        var choices = query.StartsWith("if", StringComparison.Ordinal)
            ? TemplateCompletion.Fields.Select(f => "if " + f).Where(f => f.StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToArray()
            : TemplateCompletion.Fields.Concat(["moments"]).Concat(TemplateCompletion.Fields.Select(f => "if " + f)).Where(f => f.StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        suggestions.ItemsSource = choices; suggestions.SelectedIndex = choices.Length > 0 ? 0 : -1; suggestions.IsVisible = choices.Length > 0;
    }
    private async Task HighlightAsync()
    {
        var request = ++version; var text = Text;
        try
        {
            var spans = await Task.Run(() => Regex.Matches(text, @"\{\{([^{}]*)\}\}", RegexOptions.None, TimeSpan.FromSeconds(1))
                .Where(m => TemplateCompletion.Recognized(m.Groups[1].Value.Trim())).Select(m => (m.Index, m.Length)).ToArray());
            if (request != version) return;
            colors.Inlines!.Clear(); var cursor = 0;
            foreach (var (start, length) in spans)
            {
                colors.Inlines.Add(new Run(text[cursor..start]));
                colors.Inlines.Add(new Run(text.Substring(start, length)) { Foreground = new SolidColorBrush(Color.Parse("#8FB1D9")) });
                cursor = start + length;
            }
            colors.Inlines.Add(new Run(text[cursor..]));
        }
        catch (RegexMatchTimeoutException) { if (request == version) colors.Text = text; }
    }
}

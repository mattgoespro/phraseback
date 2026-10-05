using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Phraseback.Client;

namespace Phraseback.App.Windows;

internal sealed record RecordingSetup(CaptureScreen Screen, CaptureArea Area, string Title, string Context);

internal sealed class RecordingSetupView : UserControl
{
    public event Action<RecordingSetup>? StartRequested;
    public RecordingSetupView(CaptureScreen[] screens)
    {

        var name = new TextBox { Text = "Desktop workflow" };
        var context = new TextBox { PlaceholderText = "Optional context, not observed evidence", AcceptsReturn = true, MinHeight = 90, TextWrapping = TextWrapping.Wrap };
        var displays = new ComboBox { ItemsSource = screens, SelectedIndex = screens.Length > 0 ? 0 : -1, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        var dimensions = new TextBlock();
        CaptureArea? area = null;
        void Reset() { if (displays.SelectedItem is CaptureScreen s) { area = new(s.Left, s.Top, s.Width, s.Height); dimensions.Text = $"Full display · {s.Width} × {s.Height} physical pixels"; } }
        displays.SelectionChanged += (_, _) => Reset(); Reset();
        var region = new Button { Content = "Choose rectangle…", IsEnabled = screens.Length > 0 };
        region.Click += async (_, _) =>
        {
            if (displays.SelectedItem is not CaptureScreen screen) return;
            var overlay = new RegionSelectionWindow(screen);
            var selected = await overlay.ShowDialog<CaptureArea?>((Window)TopLevel.GetTopLevel(this)!);
            if (selected is not null) { area = selected; dimensions.Text = $"Rectangle · {area.Width} × {area.Height} at {area.Left}, {area.Top}"; }
        };
        var full = new Button { Content = "Use full display" }; full.Click += (_, _) => Reset();
        var start = new Button { Content = "Start recording", IsEnabled = screens.Length > 0,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        start.Classes.Add("accent");
        start.Click += (_, _) => { if (displays.SelectedItem is CaptureScreen screen && area is not null) StartRequested?.Invoke(new RecordingSetup(screen, area, name.Text ?? "", context.Text ?? "")); };
        Avalonia.Automation.AutomationProperties.SetName(name, "Recording title");
        Avalonia.Automation.AutomationProperties.SetName(context, "Recording context");
        Avalonia.Automation.AutomationProperties.SetName(displays, "Display to record");
        var details = new Expander { Header = "Recording details", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Content = new StackPanel { Spacing = 8, Children = { new TextBlock { Text = "Recording title" }, name,
                new TextBlock { Text = "Context (your notes, not observed evidence)" }, context } } };
        var capture = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        capture.Children.Add(displays); Grid.SetColumn(region, 1); capture.Children.Add(region);
        start.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center; start.MinWidth = 180;
        var body = new StackPanel { MaxWidth = 650, Margin = new Thickness(32), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Spacing = 18, Children = {
                new TextBlock { Text = "Show the workflow you want help with.", FontSize = 26, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = "Record your screen, review the moments, then copy the prompt.", TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = "Capture source", FontSize = 12 }, capture,
                new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 12, Children = { dimensions, full } },
                details,
                new TextBlock { Text = screens.Length == 0 ? "No supported displays found." : "A 3-second countdown gives you time to switch windows. Stop with Ctrl+Shift+F9 or the floating Stop button.", FontSize = 12, TextWrapping = TextWrapping.Wrap },
                start
            } };
        var surface = new Border { Child = new ScrollViewer { Content = body }, CornerRadius = new CornerRadius(7) };
        surface.Bind(Border.BackgroundProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("PreviewBrush"));
        var heading = new TextBlock { Text = "Start a recording", FontSize = 24, FontWeight = FontWeight.SemiBold };
        var note = new TextBlock { Text = "Record → Review → Get prompt\nScreenshots and descriptions stay on this computer.", FontSize = 12 };
        var page = new Grid { Margin = new Thickness(24, 24, 24, 20), RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 22 };
        page.Children.Add(heading); Grid.SetRow(surface, 1); page.Children.Add(surface); Grid.SetRow(note, 2); page.Children.Add(note); Content = page;
    }
}

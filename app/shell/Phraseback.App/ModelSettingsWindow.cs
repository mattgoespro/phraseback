using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Phraseback.Client;

namespace Phraseback.App;

internal sealed class ModelSettingsView : UserControl
{
    private readonly Workspace workspace;
    private readonly ComboBox presets = new() { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button install = new() { Content = "Install / repair" };
    private readonly Button verify = new() { Content = "Verify existing files" };
    private readonly Button release = new() { Content = "Release model memory" };
    private readonly Button cancel = new() { Content = "Cancel operation" };
    private readonly Button remove = new() { Content = "Remove model files…" };
    private bool loading;
    public ModelSettingsView(Workspace workspace)
    {
        this.workspace = workspace;
        Avalonia.Automation.AutomationProperties.SetName(presets, "Local model preset");
        Content = new ScrollViewer { Content = new StackPanel { Margin = new Thickness(28), Spacing = 16, Children = {
            new TextBlock { Text = "Local model", FontSize = 22, FontWeight = FontWeight.SemiBold }, presets, status,
            new WrapPanel { Children = { install, verify, release, cancel, remove } },
            new TextBlock { Text = "Installing may download verified model files. Description generation runs on this computer. GPU failure never switches to CPU automatically.", TextWrapping = TextWrapping.Wrap }
        } } };
        AttachedToVisualTree += async (_, _) => await Refresh();
        presets.SelectionChanged += async (_, _) => { if (!loading && presets.SelectedItem is ModelPreset preset) { try { await workspace.SelectModelAsync(preset.Id); await Refresh(); } catch (Exception ex) { status.Text = ex.Message; } } };
        install.Click += async (_, _) => await Run(false);
        verify.Click += async (_, _) => await Run(true);
        release.Click += async (_, _) => { try { await workspace.ReleaseModelAsync(); await Refresh(); } catch (Exception ex) { status.Text = ex.Message; } };
        cancel.Click += async (_, _) => await workspace.CancelOperationAsync();
        remove.Click += async (_, _) =>
        {
            var dialog = new Window { Title = "Remove local model", Width = 480, Height = 240, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var confirm = new Button { Content = "Remove model files" }; confirm.Click += (_, _) => dialog.Close(true);
            var keep = new Button { Content = "Keep model" }; keep.Click += (_, _) => dialog.Close(false);
            dialog.Content = new StackPanel { Margin = new Thickness(24), Spacing = 18, Children = {
                new TextBlock { Text = "Remove this model's downloaded files? CPU and GPU 2B presets share their weights. Recordings and descriptions will not be removed.", TextWrapping = TextWrapping.Wrap },
                new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 12, Children = { keep, confirm } } } };
            if (await dialog.ShowDialog<bool>((Window)TopLevel.GetTopLevel(this)!))
            {
                EnableActions(false);
                try { await workspace.RemoveModelAsync(); if (!workspace.HasError) await Refresh(); }
                finally { EnableActions(true); }
            }
        };
        AttachedToVisualTree += (_, _) => workspace.PropertyChanged += Changed;
        DetachedFromVisualTree += (_, _) => workspace.PropertyChanged -= Changed;
    }
    private void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Workspace.Activity) or nameof(Workspace.Error)) status.Text = workspace.HasError ? workspace.Error : workspace.Activity;
    }
    private async Task Run(bool verifyOnly)
    {
        EnableActions(false);
        try { await workspace.InstallModelAsync(verifyOnly); if (!workspace.HasError) await Refresh(); }
        finally { EnableActions(true); }
    }
    private void EnableActions(bool enabled) => presets.IsEnabled = install.IsEnabled = verify.IsEnabled = release.IsEnabled = remove.IsEnabled = enabled;
    private async Task Refresh()
    {
        try
        {
            var model = await workspace.ModelStatusAsync(); loading = true;
            presets.ItemsSource = model.Presets; presets.SelectedItem = model.Presets.First(p => p.Id == model.Selected); loading = false;
            var selected = (ModelPreset)presets.SelectedItem;
            status.Text = $"{model.State} · {(model.Verified ? "Previously verified; checked again before use" : model.AssetsPresent ? "Existing assets found — verification required" : "Model not installed")}\n{selected.Assets.Sum(a => a.Size) / 1073741824.0:F2} GiB assets · {selected.RamGb} GB RAM recommended";
        }
        catch (Exception ex) { status.Text = ex.Message; loading = false; }
    }
}

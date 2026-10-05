#if DEBUG
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.Text.Json;

namespace Phraseback.App.Windows;

/// <summary>Opt-in native rendering and navigation check using an isolated fixture.</summary>
internal static class NativeWorkflowCheck
{
    public static async Task Run(MainWindow owner, Workspace workspace, string root)
    {
        var output = Path.Combine(root, "workflow-check"); Directory.CreateDirectory(output);
        var screenshots = new List<string>();
        try
        {
            foreach (var size in new[] { new Size(1280, 800), new Size(1024, 700) })
            {
                owner.WindowState = WindowState.Normal; owner.Width = size.Width; owner.Height = size.Height;
                owner.ShowStage(WorkspaceStage.Ready);
                await Save("ready", size);
                if (workspace.Library.FirstOrDefault() is not { } recording) throw new IOException("An isolated recording fixture is required.");
                if (!await workspace.OpenAsync(recording)) throw new IOException(workspace.Error);
                owner.ShowStage(WorkspaceStage.Review);
                await owner.RefreshPromptAsync();
                await Save("review", size);
                var task = owner.FindControl<TextBox>("TaskInput")!;
                task.Text = "Help me simplify the settings workflow";
                owner.FindControl<Button>("GetPromptButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(400);
                if (!owner.FindControl<Button>("ContinueReviewButton")!.IsVisible) throw new IOException("Get prompt did not open results.");
                await Save("prompt", size);
                var selected = workspace.Selected?.Step.Id;
                owner.FindControl<Button>("ContinueReviewButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (workspace.Selected?.Step.Id != selected) throw new IOException("Navigation changed the selected moment.");
                owner.FindControl<Button>("InspectorToggle")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Save("descriptions", size);
                owner.FindControl<Button>("InspectorToggle")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                owner.ShowStage(WorkspaceStage.Settings); await owner.ShowSettingsAsync();
                await Save("settings", size);
            }
            File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new { passed = true, screenshots, scope = "Native UI rendering and navigation, no inference or capture" }));
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new { passed = false, error = ex.ToString(), screenshots })); }
        finally { owner.Close(); }

        async Task Save(string name, Size size)
        {
            await Task.Delay(180); await Dispatcher.UIThread.InvokeAsync(owner.UpdateLayout, DispatcherPriority.Render);
            var content = (Control)owner.Content!;
            foreach (var controlName in name switch {
                "review" or "prompt" => new[] { "EvidencePreviewPanel", "PromptPanel", "MomentList" },
                "descriptions" => new[] { "EvidencePreviewPanel", "DescriptionInspector", "MomentList" },
                _ => Array.Empty<string>() })
            {
                var control = owner.FindControl<Control>(controlName)!;
                var origin = control.TranslatePoint(default, content)!.Value;
                if (control.Bounds.Width <= 0 || control.Bounds.Height <= 0 || origin.X < 0 || origin.Y < 0 || origin.X + control.Bounds.Width > content.Bounds.Width + 1 || origin.Y + control.Bounds.Height > content.Bounds.Height + 1)
                    throw new IOException($"{controlName} is outside the {size.Width} x {size.Height} workspace: {origin}, {control.Bounds}");
            }
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)content.Bounds.Width, (int)content.Bounds.Height), new Vector(96, 96));
            bitmap.Render(content);
            var path = Path.Combine(output, $"{name}-{size.Width}x{size.Height}.png"); bitmap.Save(path, PngBitmapEncoderOptions.Default); screenshots.Add(path);
        }
    }
}
#endif

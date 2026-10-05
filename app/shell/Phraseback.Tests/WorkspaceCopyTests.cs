using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Phraseback.App;
using Xunit;

namespace Phraseback.Tests;

public sealed class WorkspaceCopyTests
{
    [AvaloniaFact]
    public void StudioDoesNotPromiseThatNormalRecordingsAreInaccessible()
    {
        var window = new MainWindow();
        var text = window.GetLogicalDescendants().OfType<TextBlock>().Select(block => block.Text ?? "").ToArray();
        Assert.Equal("Phraseback", window.Title);
        Assert.Contains("No recording open", text);
        Assert.Contains("Recordings", text);
        Assert.DoesNotContain(text, value => value.Contains("prototype", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Development fixtures", StringComparison.Ordinal)
            || value.Contains("DESIGN PREVIEW", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void InteractionPreviewDescribesItsControlsNotTheActualDataLocation()
    {
        var window = new PrototypeWindow("Settings");
        var text = window.GetLogicalDescendants().OfType<TextBlock>().Select(block => block.Text ?? "").ToArray();
        Assert.Contains("This interaction preview does not change recordings or settings.", text);
        Assert.DoesNotContain(text, value => value.Contains("isolated development directory", StringComparison.Ordinal));
    }
}

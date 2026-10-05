using Avalonia.Headless.XUnit;
using Phraseback.App.Windows;
using Xunit;

namespace Phraseback.Tests;

public sealed class DetailedCaptureSurfaceTests
{
    [AvaloniaFact]
    public void DetailedWorkloadAdvancesRowsProgressAndBriefErrorDeterministically()
    {
        var surface = new DetailedCaptureSurface();
        Assert.Equal(0, surface.FirstRow);
        Assert.False(surface.WarningVisible);
        for (var frame = 1; frame <= 100; frame++)
        {
            surface.Advance();
            Assert.Equal(frame / 3 % 80, surface.FirstRow);
            Assert.Equal(frame % 100, surface.Progress);
            Assert.Equal(frame % 50 is >= 25 and <= 27, surface.WarningVisible);
        }
    }
}

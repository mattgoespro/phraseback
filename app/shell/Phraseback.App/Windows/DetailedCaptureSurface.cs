using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Phraseback.App.Windows;

/// <summary>Opt-in synthetic capture workload, not an interactive product view.</summary>
internal sealed class DetailedCaptureSurface : Control
{
    private static readonly IBrush BackgroundBrush = Brush.Parse("#191D24");
    private static readonly IBrush PanelBrush = Brush.Parse("#242B35");
    private static readonly IBrush AccentBrush = Brush.Parse("#527AB0");
    private static readonly IBrush TextBrush = Brush.Parse("#E4E9F0");
    private static readonly IBrush MutedBrush = Brush.Parse("#ABB9CA");
    private int frame;
    internal int FirstRow => frame / 3 % 80;
    internal bool WarningVisible => frame % 50 is >= 25 and <= 27;
    internal int Progress => frame % 100;

    public void Advance()
    {
        frame++;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width < 100 || height < 100) return;
        context.DrawRectangle(BackgroundBrush, null, new Rect(Bounds.Size));
        using var clip = context.PushClip(new Rect(Bounds.Size));
        var left = Math.Min(170, width * .18);
        var inspector = Math.Min(250, width * .25);
        var bodyWidth = Math.Max(40, width - left - inspector - 40);
        context.DrawRectangle(PanelBrush, null, new Rect(0, 0, width, 68));
        Text(context, "SYNTHETIC WORKSPACE · no personal content", 18, 12, 18);
        Text(context, "Automatic short test · Ctrl+Shift+F9 stops capture", 18, 39, 12, MutedBrush);
        Text(context, "RECORDINGS", 16, 89, 13, MutedBrush);
        for (var index = 0; index < 8; index++)
        {
            var y = 122 + index * 43;
            if (index == FirstRow % 8) context.DrawRectangle(AccentBrush, null, new Rect(8, y - 5, left - 16, 34));
            Text(context, $"Example {index + 1:00}", 16, y, 13);
        }
        var bodyX = left + 14;
        Text(context, "Activity / scrolling evidence", bodyX, 88, 16);
        for (var index = 0; index < Math.Min(30, (int)((height - 174) / 27)); index++)
        {
            var row = FirstRow + index;
            var y = 122 + index * 27;
            context.DrawRectangle(PanelBrush, null, new Rect(bodyX, y, bodyWidth, 24));
            Text(context, $"{row + 1:000}   local-task-{row % 12:00}    /    evidence item {row * 7:000}", bodyX + 8, y + 3, 12);
            context.DrawRectangle(AccentBrush, null, new Rect(bodyX + bodyWidth * .78, y + 8, Math.Max(2, bodyWidth * .18 * ((row + frame) % 11) / 10), 8));
        }
        var detailX = width - inspector + 8;
        Text(context, "DETAILS", detailX, 89, 13, MutedBrush);
        Text(context, $"Selected item {FirstRow + 1:000}", detailX, 122, 14);
        Text(context, $"Progress: {Progress}%", detailX, 157, 14);
        context.DrawRectangle(AccentBrush, null, new Rect(detailX, 189, Math.Max(2, (inspector - 24) * Progress / 100), 10));
        Text(context, WarningVisible ? "Temporary validation error" : "Local changes saved", detailX, 223, 12, WarningVisible ? Brushes.Orange : TextBrush);
        Text(context, "Generated test content only", 18, height - 30, 12, MutedBrush);
    }

    private static void Text(DrawingContext context, string text, double x, double y, double size, IBrush? brush = null) =>
        context.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            Typeface.Default, size, brush ?? TextBrush), new Point(x, y));
}

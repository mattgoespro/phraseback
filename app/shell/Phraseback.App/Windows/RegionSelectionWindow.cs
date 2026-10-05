using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Phraseback.Client;

namespace Phraseback.App.Windows;

internal sealed class RegionSelectionWindow : Window
{
    private readonly CaptureScreen screen;
    private readonly Canvas canvas = new();
    private readonly Border selection = new() { BorderBrush = new SolidColorBrush(Color.FromRgb(222, 169, 95)), BorderThickness = new Thickness(2), Background = new SolidColorBrush(Color.FromArgb(60, 222, 169, 95)) };
    private Point? origin;
    public RegionSelectionWindow(CaptureScreen screen)
    {
        this.screen = screen;
        WindowDecorations = WindowDecorations.None; CanResize = false; Topmost = true;
        ShowInTaskbar = false; Background = new SolidColorBrush(Color.FromArgb(65, 0, 0, 0));
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Position = new PixelPoint(screen.Left, screen.Top);
        var scale = Screens.ScreenFromPoint(Position)?.Scaling ?? 1;
        Width = screen.Width / scale; Height = screen.Height / scale;
        canvas.Children.Add(selection);
        canvas.Children.Add(new TextBlock { Text = "Drag a rectangle to record · Esc to cancel", Foreground = Brushes.White, Background = Brushes.Black, Padding = new Thickness(12), Margin = new Thickness(20) });
        Content = canvas;
        PointerPressed += (_, e) => { origin = e.GetPosition(canvas); e.Pointer.Capture(canvas); };
        PointerMoved += (_, e) => { if (origin is { } start) Draw(start, e.GetPosition(canvas)); };
        PointerReleased += (_, e) =>
        {
            if (origin is not { } start) return;
            var end = e.GetPosition(canvas); e.Pointer.Capture(null);
            var a = this.PointToScreen(start); var b = this.PointToScreen(end);
            var left = Math.Clamp(Math.Min(a.X, b.X), screen.Left, screen.Left + screen.Width);
            var top = Math.Clamp(Math.Min(a.Y, b.Y), screen.Top, screen.Top + screen.Height);
            var right = Math.Clamp(Math.Max(a.X, b.X), screen.Left, screen.Left + screen.Width);
            var bottom = Math.Clamp(Math.Max(a.Y, b.Y), screen.Top, screen.Top + screen.Height);
            if (right > left && bottom > top) Close(new CaptureArea(left, top, right - left, bottom - top));
            else origin = null;
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
    private void Draw(Point a, Point b)
    {
        Canvas.SetLeft(selection, Math.Min(a.X, b.X)); Canvas.SetTop(selection, Math.Min(a.Y, b.Y));
        selection.Width = Math.Abs(a.X - b.X); selection.Height = Math.Abs(a.Y - b.Y);
    }
}

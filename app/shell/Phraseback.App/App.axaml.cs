using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Phraseback.App;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
#if DEBUG
            desktop.MainWindow = Windows.IndicatorInputCheck.Create(desktop.Args ?? []) ?? new MainWindow(desktop.Args ?? []);
#else
            desktop.MainWindow = new MainWindow(desktop.Args ?? []);
#endif
        base.OnFrameworkInitializationCompleted();
    }
}

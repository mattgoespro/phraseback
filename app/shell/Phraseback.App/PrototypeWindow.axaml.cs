using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Phraseback.App;

public sealed partial class PrototypeWindow : Window
{
    public PrototypeStates States { get; } = new();
    public PrototypeWindow() { InitializeComponent(); DataContext = States; }
    public PrototypeWindow(string state) : this() => States.SelectedCase = state;
    public async Task LoadEvidenceAsync(Workspace workspace)
    {
        var previews = await workspace.LoadAlternativesAsync();
        foreach (var preview in previews) States.Alternatives.Add(preview);
        States.Alternative = States.Alternatives.FirstOrDefault();
    }
    private void ActionClick(object? sender, RoutedEventArgs e) => States.Act();
    private void BackClick(object? sender, RoutedEventArgs e) => Close();
    protected override void OnClosed(EventArgs e)
    {
        foreach (var preview in States.Alternatives) preview.Image.Dispose();
        base.OnClosed(e);
    }
}

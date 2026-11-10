using Avalonia;
using Avalonia.Markup.Xaml;

namespace ChestTracker.Ui;

public sealed class UiApp : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}

using Avalonia.Controls;

namespace ChestTracker.Ui;

/// <summary>The app window: overview, matches and settings.</summary>
public sealed class MainWindow : Window
{
    private readonly IAppHost _app;
    private MainView _view = null!;

    public MainWindow(IAppHost app, Page page)
    {
        _app = app;
        Title = "chest-tracker";
        Icon = UiHost.AppIcon();
        Width = 980;
        Height = 660;
        MinWidth = 860;
        MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetView(page);
    }

    public void Show(Page page) => _view.Show(page);

    /// <summary>Rebuilds every text (language changed), staying on the same page.</summary>
    public void Reload() => SetView(_view.Current);

    private void SetView(Page page)
    {
        _view = new MainView(_app, page);
        _view.LanguageChanged += Reload;
        Content = _view;
    }
}

using Avalonia.Controls;
using Avalonia.Threading;

namespace ChestTracker.Ui;

public enum Page { Overview, Matches, Settings }

/// <summary>Side navigation and the current page.</summary>
public sealed partial class MainView : UserControl
{
    private readonly IAppHost _app;

    public MainView(IAppHost app, Page page)
    {
        _app = app;
        InitializeComponent();
        NavOverview.Click += (_, _) => Show(Page.Overview);
        NavMatches.Click += (_, _) => Show(Page.Matches);
        NavSettings.Click += (_, _) => Show(Page.Settings);
        AttachedToVisualTree += (_, _) => _app.Changed += UpdateStatus;
        DetachedFromVisualTree += (_, _) => _app.Changed -= UpdateStatus;
        UpdateStatus();
        Show(page);
    }

    public Page Current { get; private set; }

    /// <summary>The language changed on the settings page: the window rebuilds this view.</summary>
    public event Action? LanguageChanged;

    public void Show(Page page)
    {
        Current = page;
        NavOverview.Classes.Set("on", page == Page.Overview);
        NavMatches.Classes.Set("on", page == Page.Matches);
        NavSettings.Classes.Set("on", page == Page.Settings);
        PageHost.Content = page switch
        {
            Page.Matches => new MatchesPage(_app),
            Page.Settings => SettingsPage(),
            _ => OverviewPage(),
        };
    }

    private OverviewPage OverviewPage()
    {
        var overview = new OverviewPage(_app);
        overview.SeeAll += () => Show(Page.Matches);
        return overview;
    }

    private SettingsPage SettingsPage()
    {
        var settings = new SettingsPage(_app);
        // After the event that changed it has finished: the page is about to be replaced.
        settings.LanguageChanged += () => Dispatcher.UIThread.Post(() => LanguageChanged?.Invoke());
        return settings;
    }

    private void UpdateStatus()
    {
        StatusText.Text = _app.Status;
    }
}

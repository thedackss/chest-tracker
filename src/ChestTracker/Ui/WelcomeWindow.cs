using Avalonia.Controls;

namespace ChestTracker.Ui;

/// <summary>First launch window. Reads the replays already on the PC meanwhile (see UiHost.RunWelcome).</summary>
public sealed class WelcomeWindow : Window
{
    /// <summary>What the user entered and how far the first scan got (kept when the language changes).</summary>
    public sealed class State
    {
        public int Done, Total;
        public bool ScanDone;
        /// <summary>Quest chests found in the replays.</summary>
        public int Tracked;
        public int Quest;
        public bool QuestEdited;
        public bool Autostart = true;
    }

    private readonly Settings _settings;
    private WelcomeView _view = null!;

    public WelcomeWindow(Settings settings)
    {
        _settings = settings;
        Title = "chest-tracker";
        Icon = UiHost.AppIcon();
        Width = 560;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetView();
    }

    public State Progress { get; } = new();

    /// <summary>True once "Start tracking" was clicked (closed any other way: quit).</summary>
    public bool Accepted { get; private set; }

    public void Update() => _view.Update();

    private void SetView()
    {
        _view = new WelcomeView(Progress);
        // After the selection event has finished: the view is replaced.
        _view.LanguageChanged += () => Avalonia.Threading.Dispatcher.UIThread.Post(SetView);
        _view.Start += () =>
        {
            _settings.Language = Text.Language;
            _settings.StartingChests = Math.Max(0, (Progress.QuestEdited ? Progress.Quest : Progress.Tracked) - Progress.Tracked);
            _settings.StartWithFortnite = Progress.Autostart;
            Accepted = true;
            Close();
        };
        Content = _view;
    }
}

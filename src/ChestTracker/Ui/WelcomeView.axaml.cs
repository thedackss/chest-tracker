using Avalonia.Controls;
using L = ChestTracker.Text;

namespace ChestTracker.Ui;

/// <summary>First launch: turn on replays, where the quest is now, start with Windows.</summary>
public sealed partial class WelcomeView : UserControl
{
    private readonly WelcomeWindow.State _state;
    private bool _loading = true;

    public WelcomeView(WelcomeWindow.State state)
    {
        _state = state;
        InitializeComponent();
        LanguageBox.SelectedIndex = Math.Max(0, Array.IndexOf(L.Languages, L.Language));
        QuestBox.NumberFormat = L.Culture.NumberFormat;
        AutostartSwitch.IsChecked = state.Autostart;
        Update();

        QuestBox.ValueChanged += (_, _) =>
        {
            if (_loading) return;
            _state.QuestEdited = true;
            _state.Quest = (int)(QuestBox.Value ?? 0);
            Update();
        };
        AutostartSwitch.IsCheckedChanged += (_, _) => _state.Autostart = AutostartSwitch.IsChecked == true;
        LanguageBox.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            L.Set(L.Languages[Math.Max(0, LanguageBox.SelectedIndex)]);
            LanguageChanged?.Invoke();
        };
        StartButton.Click += (_, _) => Start?.Invoke();
        _loading = false;
    }

    public event Action? LanguageChanged;
    public event Action? Start;

    /// <summary>Shows the scan progress or result.</summary>
    public void Update()
    {
        _loading = true;
        if (!_state.QuestEdited) QuestBox.Value = _state.Tracked;
        _loading = false;
        StartButton.IsEnabled = _state.ScanDone;

        if (!_state.ScanDone)
        {
            QuestInfo.Text = _state.Total > 0
                ? L.T($"Reading your replays… {_state.Done}/{_state.Total}", $"Leyendo tus repeticiones… {_state.Done}/{_state.Total}")
                : L.T("Reading your replays…", "Leyendo tus repeticiones…");
            return;
        }
        var start = (_state.QuestEdited ? _state.Quest : _state.Tracked) - _state.Tracked;
        QuestInfo.Classes.Set("bad", start < 0);
        QuestInfo.Text = _state.Tracked == 0 && !_state.QuestEdited
            ? L.T("No replays yet. Type the number the game shows.", "Aún no hay repeticiones. Escribe el número que muestra el juego.")
            : start >= 0
                ? L.T($"{L.N(_state.Tracked)} from replays + {L.N(start)} from before", $"{L.N(_state.Tracked)} de repeticiones + {L.N(start)} de antes")
                : L.T($"Lower than the {L.N(_state.Tracked)} already in your replays", $"Menos que los {L.N(_state.Tracked)} que ya hay en tus repeticiones");
    }
}

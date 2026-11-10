using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using L = ChestTracker.Text;

namespace ChestTracker.Ui;

/// <summary>Settings. Every change is saved and applied at once (typed values after a short pause).</summary>
public sealed partial class SettingsPage : UserControl
{
    private readonly IAppHost _app;
    private readonly Settings _settings;
    private readonly DispatcherTimer _typing = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private bool _loading = true;

    public SettingsPage(IAppHost app)
    {
        _app = app;
        _settings = app.Settings;
        InitializeComponent();

        LanguageBox.SelectedIndex = Math.Max(0, Array.IndexOf(L.Languages, L.Language));
        (_settings.Theme switch { "dark" => ThemeDark, "light" => ThemeLight, _ => ThemeSystem }).IsChecked = true;
        AutostartSwitch.IsChecked = _settings.StartWithFortnite;
        QuestBox.NumberFormat = L.Culture.NumberFormat;
        QuestBox.Value = _settings.StartingChests + QuestFromReplays;
        FolderBox.Text = _settings.DemosFolder;
        InGameSwitch.IsChecked = _settings.OverlayOnlyInGame;
        SizeSlider.Value = _settings.OverlaySize;
        AboutText.Text = "chest-tracker " + Version;
        UpdateQuestInfo();
        UpdateFolderInfo();
        UpdateOverlay();

        LanguageBox.SelectionChanged += (_, _) => Change(() =>
        {
            _settings.Language = L.Languages[Math.Max(0, LanguageBox.SelectedIndex)];
            LanguageChanged?.Invoke();
        });
        foreach (var (button, theme) in new[] { (ThemeSystem, "system"), (ThemeDark, "dark"), (ThemeLight, "light") })
        {
            button.IsCheckedChanged += (_, _) =>
            {
                if (button.IsChecked != true) return;
                Change(() =>
                {
                    _settings.Theme = theme;
                    UiHost.ApplyTheme(theme);
                });
            };
        }
        AutostartSwitch.IsCheckedChanged += (_, _) => Change(() => _settings.StartWithFortnite = AutostartSwitch.IsChecked == true);
        OverlaySwitch.IsCheckedChanged += (_, _) =>
        {
            if (!_loading && OverlaySwitch.IsChecked != _settings.ShowOverlay) _app.ToggleOverlay();
        };
        InGameSwitch.IsCheckedChanged += (_, _) => Change(() => _settings.OverlayOnlyInGame = InGameSwitch.IsChecked == true);
        QuestSwitch.IsCheckedChanged += (_, _) => Change(() => _settings.OverlayShowQuest = QuestSwitch.IsChecked == true);
        LastSwitch.IsCheckedChanged += (_, _) => Change(() => _settings.OverlayShowLast = LastSwitch.IsChecked == true);
        SessionSwitch.IsCheckedChanged += (_, _) => Change(() => _settings.OverlayShowSession = SessionSwitch.IsChecked == true);
        TreasuresSwitch.IsCheckedChanged += (_, _) => Change(() => _settings.OverlayShowTreasures = TreasuresSwitch.IsChecked == true);
        ObsSwitch.IsChecked = _settings.ObsOverlay;
        ObsSwitch.IsCheckedChanged += (_, _) =>
        {
            Change(() => _settings.ObsOverlay = ObsSwitch.IsChecked == true);
            UpdateObs();
        };
        ObsCopyButton.Click += async (_, _) =>
        {
            if (_app.ObsUrl is { } url && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(url);
                ObsCopyButton.Content = L.T("Copied", "Copiado");
            }
        };
        ObsOpenButton.Click += (_, _) =>
        {
            if (_app.ObsUrl is { } url)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
            }
        };
        UpdateObs();
        SizeSlider.ValueChanged += (_, _) => Change(() => _settings.OverlaySize = (int)SizeSlider.Value);
        LockButton.Click += (_, _) => _app.ToggleLock();
        DataFolderButton.Click += (_, _) => _app.OpenDataFolder();
        BrowseButton.Click += async (_, _) => await BrowseAsync();

        // Typed values: applied after a short pause, and when leaving the page.
        QuestBox.ValueChanged += (_, _) => Typed();
        FolderBox.TextChanged += (_, _) => Typed();
        _typing.Tick += (_, _) => ApplyTyped();
        AttachedToVisualTree += (_, _) => _app.Changed += OnAppChanged;
        DetachedFromVisualTree += (_, _) =>
        {
            _app.Changed -= OnAppChanged;
            if (_typing.IsEnabled) ApplyTyped();
        };
        _loading = false;
    }

    /// <summary>Raised after the language setting changed and was saved.</summary>
    public event Action? LanguageChanged;

    private int QuestFromReplays => _app.Matches.Sum(m => m.QuestChests);

    private static string Version =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(SettingsPage).Assembly.GetName().Version?.ToString(3) ?? "";

    private void Change(Action apply)
    {
        if (_loading) return;
        apply();
        _app.SettingsChanged();
        UpdateOverlay();
    }

    private void Typed()
    {
        if (_loading) return;
        UpdateQuestInfo();
        _typing.Stop();
        _typing.Start();
    }

    private void ApplyTyped()
    {
        _typing.Stop();
        var start = Math.Max(0, (int)(QuestBox.Value ?? 0) - QuestFromReplays);
        var folder = string.IsNullOrWhiteSpace(FolderBox.Text) ? Settings.DefaultDemosFolder : FolderBox.Text.Trim();
        if (start == _settings.StartingChests && folder == _settings.DemosFolder) return;
        _settings.StartingChests = start;
        _settings.DemosFolder = folder;
        _app.SettingsChanged();
        UpdateFolderInfo();
    }

    private void OnAppChanged()
    {
        // New match counted, or the overlay toggled by its shortcut.
        _loading = true;
        if (!_typing.IsEnabled) QuestBox.Value = _settings.StartingChests + QuestFromReplays;
        _loading = false;
        UpdateQuestInfo();
        UpdateOverlay();
    }

    private void UpdateQuestInfo()
    {
        var tracked = QuestFromReplays;
        var value = (int)(QuestBox.Value ?? 0);
        var start = value - tracked;
        QuestInfo.Text = start < 0
            ? L.T($"Lower than the {L.N(tracked)} already counted from replays", $"Menos que los {L.N(tracked)} ya contados de repeticiones")
            : L.T($"{L.N(tracked)} from replays + {L.N(start)} from before tracking", $"{L.N(tracked)} de repeticiones + {L.N(start)} de antes del registro")
              + (value >= Totals.QuestGoal
                  ? L.T($"\nThe in-game quest stops at {L.N(Totals.QuestGoal)}; this keeps counting.", $"\nLa misión del juego se detiene en {L.N(Totals.QuestGoal)}; esto sigue contando.")
                  : "");
    }

    private void UpdateObs()
    {
        var url = _app.ObsUrl;
        ObsPanel.IsVisible = _settings.ObsOverlay;
        ObsUrlBox.Text = url ?? "";
        ObsCopyButton.IsEnabled = ObsOpenButton.IsEnabled = url != null;
        ObsInfo.Classes.Set("bad", url == null);
        ObsInfo.Text = url == null
            ? L.T("Could not start: no free port. Details in errors.log (data folder).", "No se pudo iniciar: no hay un puerto libre. Detalles en errors.log (carpeta de datos).")
            : L.T("In OBS: Sources → + → Browser, paste the address, width 500, height 300. It shows the rows chosen above and updates by itself. "
                  + "Add ?align=left or ?scale=1.5 to the address to change it.",
                  "En OBS: Fuentes → + → Navegador, pega la dirección, ancho 500, alto 300. Muestra las filas elegidas arriba y se actualiza solo. "
                  + "Agrega ?align=left o ?scale=1.5 a la dirección para cambiarlo.");
    }

    private void UpdateFolderInfo()
    {
        var folder = _settings.DemosFolder;
        var ok = Directory.Exists(folder);
        var count = ok ? Directory.EnumerateFiles(folder, "*.replay").Count() : 0;
        FolderInfo.Classes.Set("bad", !ok);
        FolderInfo.Text = ok
            ? L.T($"{L.N(count)} replays in this folder", $"{L.N(count)} repeticiones en esta carpeta")
            : L.T("Folder not found. Turn on replays in Fortnite: Settings → Game → Replays.",
                  "No se encuentra la carpeta. Activa las repeticiones en Fortnite: Ajustes → Juego → Repeticiones.");
    }

    private void UpdateOverlay()
    {
        _loading = true;
        OverlaySwitch.IsChecked = _settings.ShowOverlay;
        QuestSwitch.IsChecked = _settings.OverlayShowQuest; // may have switched off by itself (quest complete)
        LastSwitch.IsChecked = _settings.OverlayShowLast;
        SessionSwitch.IsChecked = _settings.OverlayShowSession;
        TreasuresSwitch.IsChecked = _settings.OverlayShowTreasures;
        _loading = false;
        SizeText.Text = $"{_settings.OverlaySize} %";
        LockButton.Content = _app.OverlayLocked
            ? L.T("Unlock to move   Ctrl+Shift+L", "Desfijar para mover   Ctrl+Shift+L")
            : L.T("Lock in place   Ctrl+Shift+L", "Fijar aquí   Ctrl+Shift+L");
        using var png = new MemoryStream(_app.OverlayPng(_settings.OverlaySize / 100f));
        var old = OverlayImage.Source as IDisposable;
        OverlayImage.Source = new Bitmap(png);
        old?.Dispose();
    }

    private async Task BrowseAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;
        var start = Directory.Exists(FolderBox.Text) ? await storage.TryGetFolderFromPathAsync(FolderBox.Text!) : null;
        var picked = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = L.T("Replays folder", "Carpeta de repeticiones"),
            SuggestedStartLocation = start,
        });
        if (picked.Count > 0 && picked[0].TryGetLocalPath() is string path)
        {
            FolderBox.Text = path;
            ApplyTyped();
        }
    }
}

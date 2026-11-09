namespace ChestTracker;

/// <summary>The app: tray icon, background scans, overlay, report and shortcuts.</summary>
public sealed class TrayApp : ApplicationContext
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan FinishingInterval = TimeSpan.FromSeconds(2);

    private readonly Store _store;
    private readonly Settings _settings;
    private readonly Tracker _tracker;
    private readonly DateTime _sessionStartUtc = DateTime.UtcNow;
    private readonly SynchronizationContext _ui;

    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _header = new() { Enabled = false };
    private readonly ToolStripMenuItem _overlayItem = new();
    private readonly ToolStripMenuItem _lockItem = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 2000 };
    private readonly OverlayForm _overlay = new();
    private readonly Hotkeys _hotkeys = new();

    private bool _scanning;
    private DateTime _lastScanUtc = DateTime.MinValue;
    private bool _fortniteRunning;
    private SettingsForm? _settingsForm;

    public TrayApp(Store store, Settings settings, Tracker tracker)
    {
        _store = store;
        _settings = settings;
        _tracker = tracker;
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _tray = new NotifyIcon { Icon = ChestArt.AppIcon(), Visible = true, ContextMenuStrip = BuildMenu() };
        _tray.DoubleClick += (_, _) => OpenReport();

        _overlay.Moved += p =>
        {
            _settings.OverlayX = p.X;
            _settings.OverlayY = p.Y;
            SaveSettings();
        };
        _hotkeys.Register(Keys.O, ToggleOverlay);
        _hotkeys.Register(Keys.L, ToggleLock);

        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Refresh();
        Report.Write(_store, _settings, _tracker.MatchesSnapshot(), Totals());
        Tick();
    }

    private Totals Totals() => _tracker.GetTotals(_sessionStartUtc);

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        _overlayItem.Click += (_, _) => ToggleOverlay();
        _lockItem.Click += (_, _) => ToggleLock();
        menu.Items.Add(_header);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_overlayItem);
        menu.Items.Add(_lockItem);
        menu.Items.Add(Text.T("Open report", "Abrir informe"), null, (_, _) => OpenReport());
        menu.Items.Add(Text.T("Settings…", "Ajustes…"), null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Text.T("Quit", "Salir"), null, (_, _) => ExitThread());
        return menu;
    }

    private void Tick()
    {
        _fortniteRunning = Fortnite.IsRunning();
        var interval = _tracker.Finishing ? FinishingInterval : ScanInterval;
        if (!_scanning && DateTime.UtcNow - _lastScanUtc >= interval)
        {
            _scanning = true;
            Task.Run(() =>
            {
                try
                {
                    return _tracker.Scan();
                }
                catch (Exception e)
                {
                    _store.Log("Scan failed", e);
                    return ScanResult.Empty;
                }
            }).ContinueWith(t => _ui.Post(_ => OnScanned(t.Result), null));
        }
        UpdateOverlay();
    }

    private void OnScanned(ScanResult result)
    {
        _scanning = false;
        _lastScanUtc = DateTime.UtcNow;
        if (result.Added.Count > 0)
        {
            var totals = Totals();
            Report.Write(_store, _settings, _tracker.MatchesSnapshot(), totals);
            if (result.Added.Count == 1)
            {
                var m = result.Added[0];
                Notify(Text.T($"Match counted: +{m.ChestsTotal} chests", $"Partida contada: +{m.ChestsTotal} cofres"),
                    Text.T($"Quest +{m.QuestChests} · total {Text.N(totals.Real)}", $"Misión +{m.QuestChests} · total {Text.N(totals.Real)}"));
            }
            else
            {
                Notify(Text.T($"{result.Added.Count} matches counted", $"{result.Added.Count} partidas contadas"),
                    Text.T($"Total {Text.N(totals.Real)} · quest {Text.N(totals.Quest)}", $"Total {Text.N(totals.Real)} · misión {Text.N(totals.Quest)}"));
            }
        }
        if (result.Errors.Count > 0)
        {
            Notify(Text.T("A replay could not be read", "No se pudo leer una repetición"), string.Join("\n", result.Errors.Take(2)));
        }
        Refresh();
    }

    private void Notify(string title, string text)
    {
        _tray.BalloonTipTitle = title;
        _tray.BalloonTipText = text.Length > 0 ? text : " ";
        _tray.ShowBalloonTip(5000);
    }

    /// <summary>Tray tooltip, menu and overlay figures.</summary>
    private void Refresh()
    {
        var t = Totals();
        var summary = Text.T($"{Text.N(t.Real)} chests · quest {Text.N(t.Quest)}", $"{Text.N(t.Real)} cofres · misión {Text.N(t.Quest)}");
        _header.Text = summary;
        var tip = "chest-tracker · " + summary;
        _tray.Text = tip.Length > 63 ? tip[..63] : tip;
        _overlayItem.Text = Text.T("Show overlay\tCtrl+Shift+O", "Mostrar overlay\tCtrl+Shift+O");
        _overlayItem.Checked = _settings.ShowOverlay;
        _lockItem.Text = Text.T("Lock overlay\tCtrl+Shift+L", "Fijar overlay\tCtrl+Shift+L");
        _lockItem.Checked = _settings.OverlayLocked;
        UpdateOverlay();
    }

    private void UpdateOverlay()
    {
        var visible = _settings.ShowOverlay && (!_settings.OverlayOnlyInGame || _fortniteRunning || !_settings.OverlayLocked);
        if (!visible)
        {
            if (_overlay.Visible) _overlay.Hide();
            return;
        }
        _overlay.UpdateState(new OverlayState(Totals(), _tracker.MatchInProgress, _settings.OverlayLocked), _settings.OverlaySize);
        if (!_overlay.Visible)
        {
            _overlay.Location = OverlayPosition();
            _overlay.Show();
        }
    }

    /// <summary>The saved position if it is still on a screen, else right side of the main screen, under the minimap.</summary>
    private Point OverlayPosition()
    {
        if (_settings.OverlayX is int x && _settings.OverlayY is int y
            && Screen.AllScreens.Any(s => s.WorkingArea.Contains(x + 20, y + 20)))
        {
            return new Point(x, y);
        }
        var area = Screen.PrimaryScreen!.WorkingArea;
        return new Point(area.Right - _overlay.Width - (int)(area.Width * 0.01), area.Top + (int)(area.Height * 0.30));
    }

    private void ToggleOverlay()
    {
        _settings.ShowOverlay = !_settings.ShowOverlay;
        SaveSettings();
        Refresh();
    }

    private void ToggleLock()
    {
        _settings.OverlayLocked = !_settings.OverlayLocked;
        if (!_settings.ShowOverlay && !_settings.OverlayLocked)
        {
            _settings.ShowOverlay = true; // unlocking means "I want to move it"
        }
        SaveSettings();
        Refresh();
    }

    private void OpenReport()
    {
        Report.Write(_store, _settings, _tracker.MatchesSnapshot(), Totals());
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_store.ReportPath) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception e)
        {
            _store.Log("Could not open the report", e);
        }
    }

    public void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }
        var questFromReplays = _tracker.MatchesSnapshot().Sum(m => m.QuestChests);
        _settingsForm = new SettingsForm(_settings, questFromReplays, welcome: false);
        _settingsForm.FormClosed += (_, _) =>
        {
            if (_settingsForm.DialogResult == DialogResult.OK)
            {
                ApplySettings();
            }
            _settingsForm = null;
        };
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void ApplySettings()
    {
        var language = Text.Language;
        Text.Set(_settings.Language);
        SaveSettings();
        Autostart.Apply(_settings.StartWithFortnite);
        if (language != Text.Language)
        {
            _tray.ContextMenuStrip = BuildMenu();
        }
        _lastScanUtc = DateTime.MinValue; // replays folder may have changed
        Report.Write(_store, _settings, _tracker.MatchesSnapshot(), Totals());
        Refresh();
    }

    private void SaveSettings()
    {
        try
        {
            _store.SaveSettings(_settings);
        }
        catch (Exception e)
        {
            _store.Log("Could not save settings", e);
        }
    }

    /// <summary>Another launch of the exe: show the settings of this one.</summary>
    public void OnSecondInstance() => _ui.Post(_ => ShowSettings(), null);

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _hotkeys.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _overlay.Close();
        base.ExitThreadCore();
    }
}

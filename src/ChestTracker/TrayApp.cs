using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ChestTracker.Ui;

namespace ChestTracker;

/// <summary>The app: tray icon, background scans, overlay, shortcuts. Opens the windows (Ui) on demand.</summary>
public sealed class TrayApp : ApplicationContext, IAppHost
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan FinishingInterval = TimeSpan.FromSeconds(2);

    private readonly Store _store;
    private readonly Settings _settings;
    private readonly Tracker _tracker;
    private readonly DateTime _sessionStartUtc = DateTime.UtcNow;
    private readonly SynchronizationContext _ui;

    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 2000 };
    private readonly OverlayForm _overlay = new();
    private readonly Hotkeys _hotkeys = new();

    private bool _scanning;
    private DateTime _lastScanUtc = DateTime.MinValue;
    private bool _fortniteRunning;
    private bool _matchInProgress;
    private string _demosFolder;
    private DateTime _balloonClickedUtc;
    private ObsServer? _obs;

    public TrayApp(Store store, Settings settings, Tracker tracker)
    {
        _store = store;
        _settings = settings;
        _tracker = tracker;
        _demosFolder = settings.DemosFolder;
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _tray = new NotifyIcon { Icon = ChestArt.AppIcon(), Visible = true };
        // Left or right click: the panel. MouseUp, not MouseClick: in the Windows 11 hidden icons popup the button
        // press doesn't always reach the icon, and MouseClick then never fires.
        _tray.MouseUp += (_, e) =>
        {
            // A click on one of our notifications can arrive as a click on the icon too: that one opens the window.
            if (e.Button is MouseButtons.Left or MouseButtons.Right && DateTime.UtcNow - _balloonClickedUtc > TimeSpan.FromSeconds(1))
            {
                UiHost.ToggleFlyout(this, new Avalonia.PixelPoint(Cursor.Position.X, Cursor.Position.Y));
            }
        };
        _tray.BalloonTipClicked += (_, _) =>
        {
            _balloonClickedUtc = DateTime.UtcNow;
            UiHost.ShowMain(this, Page.Overview);
        };
        _tray.DoubleClick += (_, _) => UiHost.ShowMain(this, Page.Overview);

        // Get the panel ready so the first click is quick: a few seconds after start (not while the game runs),
        // or as soon as the mouse is over the icon.
        _tray.MouseMove += (_, _) => UiHost.WarmUp(this);
        var warmUp = new System.Windows.Forms.Timer { Interval = 4000 };
        warmUp.Tick += (_, _) =>
        {
            warmUp.Dispose();
            if (!_fortniteRunning) UiHost.WarmUp(this);
        };
        warmUp.Start();

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
        ApplyObs();
        Refresh();
        Report.Write(_store, _settings, _tracker.MatchesSnapshot(), Totals);
        Tick();
    }

    // ---- IAppHost (the windows) ----

    public Settings Settings => _settings;
    public Totals Totals => _tracker.GetTotals(_sessionStartUtc);
    public List<MatchRecord> Matches => _tracker.MatchesSnapshot();
    public bool OverlayLocked => _settings.OverlayLocked;
    public event Action? Changed;

    public string Status =>
        _tracker.MatchInProgress ? Text.T("Match in progress\nCounted when it ends", "Partida en curso\nSe cuenta al terminar")
        : _fortniteRunning ? Text.T("Fortnite is running\nWatching replays", "Fortnite está abierto\nVigilando repeticiones")
        : Text.T("Watching replays", "Vigilando repeticiones")
          + (Totals.Last is { } last ? "\n" + Text.T("Last match: ", "Última partida: ") + Stats.ShortWhen(last) : "");

    public void SettingsChanged()
    {
        Text.Set(_settings.Language);
        SaveSettings();
        if (_store.IsDefaultFolder)
        {
            Autostart.Apply(_settings.StartWithFortnite);
        }
        if (_settings.DemosFolder != _demosFolder)
        {
            _demosFolder = _settings.DemosFolder;
            _lastScanUtc = DateTime.MinValue; // scan the new folder now
        }
        ApplyObs();
        Report.Write(_store, _settings, _tracker.MatchesSnapshot(), Totals);
        Refresh();
    }

    public void ToggleOverlay()
    {
        _settings.ShowOverlay = !_settings.ShowOverlay;
        SaveSettings();
        Refresh();
    }

    public void ToggleLock()
    {
        _settings.OverlayLocked = !_settings.OverlayLocked;
        if (!_settings.ShowOverlay && !_settings.OverlayLocked)
        {
            _settings.ShowOverlay = true; // unlocking means "I want to move it"
        }
        SaveSettings();
        Refresh();
    }

    public void ExportReport()
    {
        Report.Write(_store, _settings, _tracker.MatchesSnapshot(), Totals);
        Open(_store.ReportPath);
    }

    public void OpenDataFolder() => Open(_store.Folder);

    public void Quit() => ExitThread();

    public byte[] OverlayPng(float scale) => OverlayPng(OverlayNow, scale);

    public string? ObsUrl => _obs?.Url;

    /// <summary>Starts or stops the OBS overlay server to match the setting.</summary>
    private void ApplyObs()
    {
        if (_settings.ObsOverlay && _obs == null)
        {
            _obs = ObsServer.Start(_settings.ObsPort);
            if (_obs == null) _store.Log($"OBS overlay: no free port from {_settings.ObsPort}");
            UpdateObs();
        }
        else if (!_settings.ObsOverlay && _obs != null)
        {
            _obs.Dispose();
            _obs = null;
        }
    }

    /// <summary>The figures the OBS page shows: the same rows as the overlay.</summary>
    private void UpdateObs()
    {
        if (_obs == null) return;
        var t = Totals;
        var show = OverlayRows.From(_settings);
        var rows = new List<object>();
        if (show.Quest) rows.Add(new { label = Text.T("Quest", "Misión"), value = Text.N(t.Quest) });
        if (show.Last) rows.Add(new { label = Text.T("Last", "Última"), value = t.Last != null ? Text.N(t.Last.ChestsTotal) : "—" });
        if (show.Session) rows.Add(new { label = Text.T("Session", "Sesión"), value = Text.N(t.Session) });
        if (show.Treasures) rows.Add(new { label = "★", value = Text.N(t.Treasures), star = true });
        _obs.Update(System.Text.Json.JsonSerializer.Serialize(new { total = Text.N(t.Real), inMatch = _tracker.MatchInProgress, rows }));
    }

    private OverlayState OverlayNow =>
        new(Totals, _tracker.MatchInProgress, _settings.OverlayLocked, OverlayRows.From(_settings));

    public static byte[] OverlayPng(OverlayState state, float scale)
    {
        using var bmp = OverlayForm.Draw(state, scale);
        using var png = new MemoryStream();
        bmp.Save(png, ImageFormat.Png);
        return png.ToArray();
    }

    // ---- tray ----

    private void Tick()
    {
        var running = Fortnite.IsRunning();
        if (running != _fortniteRunning || _tracker.MatchInProgress != _matchInProgress)
        {
            _fortniteRunning = running;
            _matchInProgress = _tracker.MatchInProgress;
            UpdateObs(); // gold total while in a match
            Changed?.Invoke(); // status line, overlay preview
        }
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
            var totals = Totals;
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
        if (result.Added.Count > 0)
        {
            Refresh();
        }
    }

    private void Notify(string title, string text)
    {
        _tray.BalloonTipTitle = title;
        _tray.BalloonTipText = text.Length > 0 ? text : " ";
        _tray.ShowBalloonTip(5000);
    }

    /// <summary>Tray tooltip, overlay and open windows.</summary>
    private void Refresh()
    {
        var t = Totals;
        var summary = Text.T($"{Text.N(t.Real)} chests · quest {Text.N(t.Quest)}", $"{Text.N(t.Real)} cofres · misión {Text.N(t.Quest)}");
        var tip = "chest-tracker · " + summary;
        _tray.Text = tip.Length > 63 ? tip[..63] : tip;
        // Quest complete in game: switch its overlay row off, once (it can be switched back on in Settings).
        if (t.QuestComplete != _settings.QuestCompleteHandled)
        {
            _settings.QuestCompleteHandled = t.QuestComplete;
            if (t.QuestComplete) _settings.OverlayShowQuest = false;
            SaveSettings();
        }
        UpdateOverlay();
        UpdateObs();
        Changed?.Invoke();
    }

    private void UpdateOverlay()
    {
        var visible = _settings.ShowOverlay && (!_settings.OverlayOnlyInGame || _fortniteRunning || !_settings.OverlayLocked);
        if (!visible)
        {
            if (_overlay.IsOnScreen) _overlay.HideQuietly();
            return;
        }
        _overlay.UpdateState(OverlayNow, _settings.OverlaySize);
        if (!_overlay.IsOnScreen)
        {
            _overlay.Location = OverlayPosition();
            _overlay.ShowQuietly();
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

    private void Open(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception e)
        {
            _store.Log($"Could not open {path}", e);
        }
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

    /// <summary>Another launch of the exe: show this one's window.</summary>
    public void OnSecondInstance() => _ui.Post(_ => UiHost.ShowMain(this, Page.Overview), null);

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _hotkeys.Dispose();
        _obs?.Dispose();
        UiHost.CloseAll();
        _tray.Visible = false;
        _tray.Dispose();
        _overlay.Close();
        base.ExitThreadCore();
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using L = ChestTracker.Text;

namespace ChestTracker.Ui;

/// <summary>Small panel shown when the tray icon is clicked: today's figures and quick toggles. Closes when it loses focus.</summary>
public sealed partial class TrayFlyout : Window
{
    private readonly IAppHost _app;

    public TrayFlyout(IAppHost app)
    {
        _app = app;
        InitializeComponent();
        OpenButton.Click += (_, _) => Then(() => UiHost.ShowMain(_app, Page.Overview));
        SettingsButton.Click += (_, _) => Then(() => UiHost.ShowMain(_app, Page.Settings));
        QuitButton.Click += (_, _) => Then(_app.Quit);
        OverlayButton.Click += (_, _) => _app.ToggleOverlay();
        LockButton.Click += (_, _) => _app.ToggleLock();
        Deactivated += (_, _) => Dismiss();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Dismiss();
        };
        _app.Changed += Refresh;
        Opened += (_, _) => RoundCorners();
        Closed += (_, _) =>
        {
            IsClosed = true;
            _app.Changed -= Refresh;
        };
        Refresh();
    }

    /// <summary>Windows 11 rounds the corners and draws a thin border, like its own flyouts (ignored on Windows 10).</summary>
    private void RoundCorners()
    {
        if (TryGetPlatformHandle()?.Handle is not { } hwnd) return;
        var round = 2; // DWMWCP_ROUND
        DwmSetWindowAttribute(hwnd, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref round, sizeof(int));
        if (this.TryFindResource("CtLine", ActualThemeVariant, out var line) && line is Avalonia.Media.ISolidColorBrush brush)
        {
            var c = brush.Color;
            var colorRef = c.R | c.G << 8 | c.B << 16; // COLORREF: 0x00BBGGRR
            DwmSetWindowAttribute(hwnd, 34 /* DWMWA_BORDER_COLOR */, ref colorRef, sizeof(int));
        }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public bool IsClosed { get; private set; }

    public void Dismiss()
    {
        if (!IsClosed) Close();
    }

    /// <summary>Closes the flyout, then does <paramref name="action"/>.</summary>
    private void Then(Action action)
    {
        Dismiss();
        action();
    }

    private void Refresh()
    {
        var t = _app.Totals;
        var today = Stats.Today(_app.Matches);
        QuestText.Text = L.N(t.Quest);
        SubText.Text = t.QuestComplete
            ? L.T($"opened · {L.N(t.Real)} total", $"abiertos · {L.N(t.Real)} en total")
            : L.T($"quest · {L.N(t.Real)} total", $"misión · {L.N(t.Real)} en total");
        LastText.Text = t.Last != null ? "+" + L.N(t.Last.ChestsTotal) : "—";
        TodayText.Text = L.N(today.Sum(m => m.ChestsTotal));
        TreasuresText.Text = "★ " + L.N(t.Treasures);
        OverlaySwitch.IsChecked = _app.Settings.ShowOverlay;
        LockSwitch.IsChecked = _app.OverlayLocked;
    }

    /// <summary>
    /// Puts the flyout next to the tray: by the cursor when it is on the taskbar, else (hidden icons popup, keyboard)
    /// in the corner by the clock. Always inside the work area of that screen.
    /// </summary>
    public void PlaceNear(PixelPoint cursor)
    {
        var screen = Screens.ScreenFromPoint(cursor) ?? Screens.Primary;
        if (screen == null) return;
        var area = screen.WorkingArea;
        var size = PixelSize.FromSize(ClientSize, screen.Scaling);
        const int margin = 12;
        int x, y;
        if (area.Contains(cursor))
        {
            x = area.Right - size.Width - margin;
            y = area.Y > screen.Bounds.Y ? area.Y + margin : area.Bottom - size.Height - margin; // taskbar on top?
        }
        else
        {
            x = cursor.X - size.Width / 2;
            y = cursor.Y > area.Y + area.Height / 2 ? area.Bottom - size.Height - margin : area.Y + margin;
        }
        x = Math.Clamp(x, area.X + margin, area.Right - size.Width - margin);
        Position = new PixelPoint(x, y);
    }
}

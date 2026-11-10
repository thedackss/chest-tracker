using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;

namespace ChestTracker.Ui;

/// <summary>
/// Starts Avalonia the first time a window is needed, on the WinForms UI thread: its windows then run inside the
/// tray app's message loop. Nothing of it is loaded while the app only sits in the tray.
/// </summary>
public static class UiHost
{
    private static bool _started;
    private static MainWindow? _main;
    private static TrayFlyout? _flyout;
    private static DateTime _flyoutClosedUtc;

    public static void EnsureStarted(string? theme)
    {
        if (_started) return;
        AppBuilder.Configure<UiApp>()
            .UseWin32()
            .UseSkia()
            .UseHarfBuzz()
            // Software rendering: the windows are simple and mostly still, and no GPU device is created next to the game.
            .With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } })
            .SetupWithoutStarting();
        _started = true;
        ApplyTheme(theme);
    }

    /// <summary>
    /// Makes the first tray click as quick as the next ones: starts Avalonia and draws the panel once, off screen,
    /// then gives the memory back. The first open otherwise spends ~0.5 s starting up (mostly compiling code).
    /// </summary>
    public static void WarmUp(IAppHost app)
    {
        if (_started) return;
        EnsureStarted(app.Settings.Theme);
        var panel = new TrayFlyout(app)
        {
            ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = new PixelPoint(-30000, -30000),
        };
        panel.Opened += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            panel.Dismiss();
            Dispatcher.UIThread.Post(ReleaseMemory, DispatcherPriority.Background);
        }, DispatcherPriority.Background);
        panel.Show();
    }

    /// <summary>"dark", "light", or anything else to follow Windows.</summary>
    public static void ApplyTheme(string? theme)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = theme switch
            {
                "dark" => ThemeVariant.Dark,
                "light" => ThemeVariant.Light,
                _ => ThemeVariant.Default,
            };
        }
    }

    public static WindowIcon AppIcon()
    {
        using var stream = typeof(UiHost).Assembly.GetManifestResourceStream("app.ico")!;
        return new WindowIcon(stream);
    }

    public static void ShowMain(IAppHost app, Page page)
    {
        EnsureStarted(app.Settings.Theme);
        if (_main == null)
        {
            _main = new MainWindow(app, page);
            _main.Closed += (_, _) =>
            {
                _main = null;
                Dispatcher.UIThread.Post(ReleaseMemory, DispatcherPriority.Background);
            };
            _main.Show();
        }
        else
        {
            _main.Show(page);
            if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        }
        _main.Activate();
    }

    /// <summary>Tray icon clicked: opens the flyout by the cursor, or closes it.</summary>
    public static void ToggleFlyout(IAppHost app, PixelPoint cursor)
    {
        EnsureStarted(app.Settings.Theme);
        if (_flyout is { IsClosed: false })
        {
            _flyout.Dismiss();
            return;
        }
        // The click on the tray icon took the focus away and closed it a moment ago: that click meant "close".
        if (DateTime.UtcNow - _flyoutClosedUtc < TimeSpan.FromMilliseconds(400)) return;

        var flyout = new TrayFlyout(app) { Opacity = 0 };
        flyout.Closed += (_, _) => _flyoutClosedUtc = DateTime.UtcNow;
        flyout.Opened += (_, _) =>
        {
            flyout.PlaceNear(cursor);
            flyout.Opacity = 1;
            flyout.Activate();
        };
        _flyout = flyout;
        flyout.Show();
    }

    /// <summary>
    /// The window was closed (usually before playing): give back what it used, so the tray app is as light as before
    /// it was opened.
    /// </summary>
    private static void ReleaseMemory()
    {
        if (_main != null || _flyout is { IsClosed: false }) return;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        SetProcessWorkingSetSize(self.Handle, -1, -1);
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, nint min, nint max);

    /// <summary>The language changed: rebuild the open windows' text.</summary>
    public static void Reload()
    {
        _flyout?.Dismiss();
        _main?.Reload();
    }

    public static void CloseAll()
    {
        _flyout?.Dismiss();
        _main?.Close();
    }

    /// <summary>
    /// First launch: shows the welcome window while the replays already on the PC are read, until it is closed.
    /// Returns false if it was closed without "Start tracking".
    /// </summary>
    public static bool RunWelcome(Store store, Settings settings, Tracker tracker)
    {
        EnsureStarted(settings.Theme);
        var window = new WelcomeWindow(settings);
        var state = window.Progress;
        using var done = new CancellationTokenSource();
        window.Closed += (_, _) => done.Cancel();

        Task.Run(() =>
        {
            try
            {
                tracker.Scan(ignoreQuietPeriod: true, (n, total, _) => Dispatcher.UIThread.Post(() =>
                {
                    state.Done = n;
                    state.Total = total;
                    window.Update();
                }));
            }
            catch (Exception e)
            {
                // The welcome screen still works with what was read.
                store.Log("First scan failed", e);
            }
        }).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
        {
            state.ScanDone = true;
            state.Tracked = tracker.MatchesSnapshot().Sum(m => m.QuestChests);
            window.Update();
        }));

        window.Show();
        Dispatcher.UIThread.MainLoop(done.Token);
        return window.Accepted;
    }

    /// <summary>--preview: every window in both themes as PNG files.</summary>
    public static void SavePreviews(IAppHost app, string folder)
    {
        EnsureStarted("dark");
        foreach (var theme in new[] { "dark", "light" })
        {
            ApplyTheme(theme);
            foreach (var page in Enum.GetValues<Page>())
            {
                Snap(new MainWindow(app, page), Path.Combine(folder, $"{theme}-{page.ToString().ToLowerInvariant()}.png"));
            }
            var welcome = new WelcomeWindow(app.Settings);
            welcome.Progress.ScanDone = true;
            welcome.Progress.Tracked = app.Matches.Sum(m => m.QuestChests);
            welcome.Update();
            Snap(welcome, Path.Combine(folder, $"{theme}-welcome.png"));
            Snap(new TrayFlyout(app), Path.Combine(folder, $"{theme}-flyout.png"));
        }
        ApplyTheme(app.Settings.Theme);
    }

    private static void Snap(Window window, string path)
    {
        window.ShowInTaskbar = false;
        window.ShowActivated = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Position = new PixelPoint(-30000, -30000);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var size = PixelSize.FromSize(window.ClientSize, 1);
        using (var bitmap = new RenderTargetBitmap(size, new Vector(96, 96)))
        {
            bitmap.Render(window);
            bitmap.Save(path, PngBitmapEncoderOptions.Default);
        }
        window.Close();
    }
}

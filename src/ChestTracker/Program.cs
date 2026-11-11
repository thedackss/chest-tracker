using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ChestTracker;

public static class Program
{
    private const string MutexName = @"Local\chest-tracker";
    private const string ShowEventName = @"Local\chest-tracker-show";

    // ChestTracker.exe [--background] [--data <folder>] [--preview <folder>]
    //   --background  started with Windows: no welcome screen, just the tray
    //   --data        another data folder (testing)
    //   --preview     saves overlay, chest and window screenshots (dark and light) to <folder>, then quits
    //   --make-icon   writes the app icon (.ico) to <file>, then quits (tools/make-icon.ps1)
    [STAThread]
    public static int Main(string[] args)
    {
        string? dataDir = null, previewDir = null, iconFile = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--data" when i + 1 < args.Length: dataDir = args[++i]; break;
                case "--preview" when i + 1 < args.Length: previewDir = args[++i]; break;
                case "--make-icon" when i + 1 < args.Length: iconFile = args[++i]; break;
            }
        }
        if (iconFile != null)
        {
            ChestArt.WriteIcon(iconFile);
            return 0;
        }

        ApplicationConfiguration.Initialize();
        var store = new Store(dataDir);
        var firstRun = !store.HasSettings;
        var settings = store.LoadSettings();
        Text.Set(settings.Language);

        if (previewDir != null)
        {
            Preview.Save(store, settings, previewDir);
            return 0;
        }

        // One instance per data folder (string.GetHashCode differs between processes: use a real hash).
        var suffix = dataDir != null
            ? "-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(dataDir).ToLowerInvariant())))[..16]
            : "";
        using var mutex = new Mutex(true, MutexName + suffix, out var first);
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName + suffix);
        if (!first)
        {
            // The running one opens its window. Only the foreground process (this one) may let it come to the front.
            AllowSetForegroundWindow(-1 /* ASFW_ANY */);
            showEvent.Set();
            return 0;
        }

        // An error in a window or a tray click goes to errors.log; the tray app keeps counting (no .NET error dialog).
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => store.Log("Unexpected error", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception x) store.Log("Crash", x);
        };
        Ui.UiHost.Log = store.Log;

        var tracker = new Tracker(store, settings, store.LoadData());
        if (firstRun)
        {
            // Reads the replays already on the PC meanwhile, so the quest value can be turned into a start.
            if (!Ui.UiHost.RunWelcome(store, settings, tracker))
            {
                return 0;
            }
            Text.Set(settings.Language);
        }
        store.SaveSettings(settings);
        if (store.IsDefaultFolder)
        {
            Autostart.Apply(settings.StartWithFortnite);
        }

        var app = new TrayApp(store, settings, tracker);
        var waiter = new Thread(() =>
        {
            while (showEvent.WaitOne())
            {
                app.OnSecondInstance();
            }
        }) { IsBackground = true };
        waiter.Start();
        Application.Run(app);
        return 0;
    }

    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(int processId);
}

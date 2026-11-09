namespace ChestTracker;

public static class Program
{
    private const string MutexName = @"Local\chest-tracker";
    private const string ShowEventName = @"Local\chest-tracker-show";

    // ChestTracker.exe [--background] [--data <folder>] [--preview <folder>]
    //   --background  started with Windows: no welcome screen, just the tray
    //   --data        another data folder (testing)
    //   --preview     saves overlay and settings screenshots to <folder>, then quits
    [STAThread]
    public static int Main(string[] args)
    {
        string? dataDir = null, previewDir = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--data" when i + 1 < args.Length: dataDir = args[++i]; break;
                case "--preview" when i + 1 < args.Length: previewDir = args[++i]; break;
            }
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
            showEvent.Set(); // the running one opens its settings
            return 0;
        }

        var tracker = new Tracker(store, settings, store.LoadData());
        if (firstRun)
        {
            // Count the replays already on the PC first, so the welcome screen can turn the quest value into a start.
            ScanWithProgress(tracker);
            using var welcome = new SettingsForm(settings, tracker.MatchesSnapshot().Sum(m => m.QuestChests), welcome: true);
            if (welcome.ShowDialog() != DialogResult.OK)
            {
                return 0;
            }
            Text.Set(settings.Language);
        }
        store.SaveSettings(settings);
        Autostart.Apply(settings.StartWithFortnite);

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

    /// <summary>First launch: a small window while the replays already on disk are analyzed.</summary>
    private static void ScanWithProgress(Tracker tracker)
    {
        if (tracker.UnprocessedFiles().Count == 0)
        {
            return;
        }
        using var form = new Form
        {
            Text = "chest-tracker",
            Icon = ChestArt.AppIcon(),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ControlBox = false,
            StartPosition = FormStartPosition.CenterScreen,
            ClientSize = new Size(380, 90),
            Font = new Font("Segoe UI", 9.5f),
        };
        var label = new Label { Text = Text.T("Reading your replays…", "Leyendo tus repeticiones…"), Location = new Point(16, 16), AutoSize = true };
        var bar = new ProgressBar { Location = new Point(16, 46), Width = 348, Style = ProgressBarStyle.Continuous };
        form.Controls.Add(label);
        form.Controls.Add(bar);
        form.Shown += async (_, _) =>
        {
            await Task.Run(() => tracker.Scan(ignoreQuietPeriod: true, (done, total, name) => form.BeginInvoke(() =>
            {
                bar.Maximum = Math.Max(1, total);
                bar.Value = Math.Min(done, bar.Maximum);
                label.Text = Text.T($"Reading your replays… {done}/{total}", $"Leyendo tus repeticiones… {done}/{total}");
            })));
            form.Close();
        };
        form.ShowDialog();
    }
}

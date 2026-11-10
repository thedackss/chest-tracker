using System.Drawing;
using System.Drawing.Imaging;
using ChestTracker.Ui;

namespace ChestTracker;

/// <summary>--preview: renders the overlay states, the chest and every window (dark and light) to PNG files.</summary>
public static class Preview
{
    public static void Save(Store store, Settings settings, string folder)
    {
        Directory.CreateDirectory(folder);
        var data = store.LoadData();
        if (data.Matches.Count == 0)
        {
            new Tracker(store, settings, data).Scan(ignoreQuietPeriod: true);
        }
        var sessionStart = data.Matches.Count > 0 ? data.Matches.Max(m => m.When).AddMinutes(-90) : DateTime.UtcNow;
        var totals = Totals.From(data, settings, sessionStart);

        foreach (var (name, state) in new[]
        {
            ("overlay-locked", new OverlayState(totals, InMatch: false, Locked: true)),
            ("overlay-in-match", new OverlayState(totals, InMatch: true, Locked: true)),
            ("overlay-unlocked", new OverlayState(totals, InMatch: false, Locked: false)),
        })
        {
            using var bmp = OverlayForm.Draw(state, 1.25f);
            // Over a mid-grey "game" background so the outline can be judged.
            using var shot = new Bitmap(bmp.Width + 40, bmp.Height + 40);
            using (var g = Graphics.FromImage(shot))
            {
                g.Clear(Color.FromArgb(70, 110, 140));
                g.DrawImage(bmp, 20, 20);
            }
            shot.Save(Path.Combine(folder, name + ".png"), ImageFormat.Png);
        }

        // The chest at icon sizes, on dark and light.
        int[] chestSizes = { 16, 24, 32, 48, 64, 128 };
        using (var strip = new Bitmap(chestSizes.Sum() + 10 * (chestSizes.Length + 1), 2 * (128 + 20)))
        using (var g = Graphics.FromImage(strip))
        {
            g.Clear(Color.FromArgb(15, 17, 23));
            using (var light = new SolidBrush(Color.FromArgb(245, 246, 248)))
            {
                g.FillRectangle(light, 0, 148, strip.Width, 148);
            }
            var x = 10;
            foreach (var s in chestSizes)
            {
                using var chest = ChestArt.Render(s);
                g.DrawImageUnscaled(chest, x, 138 - s);
                g.DrawImageUnscaled(chest, x, 286 - s);
                x += s + 10;
            }
            strip.Save(Path.Combine(folder, "chest.png"), ImageFormat.Png);
        }

        UiHost.SavePreviews(new PreviewHost(settings, data, sessionStart), folder);
        File.WriteAllText(Path.Combine(folder, "report.html"), Report.Build(settings, data.Matches, totals));
    }

    /// <summary>The windows' view of the app, from the data folder: changes are not saved.</summary>
    private sealed class PreviewHost(Settings settings, TrackerData data, DateTime sessionStartUtc) : IAppHost
    {
        public Settings Settings => settings;
        public Totals Totals => Totals.From(data, settings, sessionStartUtc);
        public List<MatchRecord> Matches => data.Matches.ToList();
        public string Status => Text.T("Watching replays", "Vigilando repeticiones");
        public bool OverlayLocked => true;
        public event Action? Changed { add { } remove { } }
        public void SettingsChanged() { }
        public void ToggleOverlay() { }
        public void ToggleLock() { }
        public void ExportReport() { }
        public void OpenDataFolder() { }
        public void Quit() { }
        public string? ObsUrl => settings.ObsOverlay ? $"http://localhost:{settings.ObsPort}/overlay" : null;
        public byte[] OverlayPng(float scale) => TrayApp.OverlayPng(new OverlayState(Totals, false, true, OverlayRows.From(settings)), scale);
    }
}

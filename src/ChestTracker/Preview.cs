using System.Drawing.Imaging;

namespace ChestTracker;

/// <summary>--preview: renders the overlay states and the settings window to PNG files (for checking the UI).</summary>
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
        var totals = Totals.From(data, settings, data.Matches.Count > 0 ? data.Matches[^1].When.AddMinutes(-90) : DateTime.UtcNow);

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

        foreach (var welcome in new[] { true, false })
        {
            using var form = new SettingsForm(settings, data.Matches.Sum(m => m.QuestChests), welcome)
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-5000, -5000),
                ShowInTaskbar = false,
            };
            form.Show();
            Application.DoEvents();
            using var bmp = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.Size));
            bmp.Save(Path.Combine(folder, welcome ? "welcome.png" : "settings.png"), ImageFormat.Png);
            form.Close();
        }

        File.WriteAllText(Path.Combine(folder, "report.html"), Report.Build(settings, data.Matches, totals));
    }
}

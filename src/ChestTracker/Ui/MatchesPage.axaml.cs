using Avalonia.Controls;
using L = ChestTracker.Text;

namespace ChestTracker.Ui;

/// <summary>One line of the matches table, already formatted.</summary>
public sealed record MatchRow(string When, string Mode, string Place, string Total, string Quest, string Broken, string Buried, string Treasures)
{
    public static MatchRow From(MatchRecord m) => new(
        Pretty.When(m),
        Pretty.Playlist(m.Playlist),
        m.Placement is int p && p > 0 ? $"#{p}" : "—",
        L.N(m.ChestsTotal),
        L.N(m.QuestChests),
        L.N(m.ChestsBroken),
        L.N(m.Chests.Count(c => c.Kind == ChestKinds.Buried)),
        string.Join(", ", m.TreasureFinds.Select(f => "★ " + f.DisplayName)));
}

/// <summary>Every match, newest first. Export writes the HTML report.</summary>
public sealed partial class MatchesPage : UserControl
{
    private readonly IAppHost _app;

    public MatchesPage(IAppHost app)
    {
        _app = app;
        InitializeComponent();
        ExportButton.Click += (_, _) => _app.ExportReport();
        OpenFolderButton.Click += (_, _) => _app.OpenDataFolder();
        AttachedToVisualTree += (_, _) => _app.Changed += Refresh;
        DetachedFromVisualTree += (_, _) => _app.Changed -= Refresh;
        Refresh();
    }

    private void Refresh()
    {
        var matches = _app.Matches.OrderByDescending(m => m.When).ToList();
        Rows.ItemsSource = matches.Select(MatchRow.From).ToList();
        Empty.IsVisible = matches.Count == 0;
        var start = _app.Settings.StartingChests;
        Summary.Text = L.T($"{L.Matches(matches.Count)} · {L.Chests(matches.Sum(m => m.ChestsTotal))}", $"{L.Matches(matches.Count)} · {L.Chests(matches.Sum(m => m.ChestsTotal))}")
            + (start > 0 ? L.T($" · +{L.N(start)} from before tracking", $" · +{L.N(start)} de antes del registro") : "");
    }
}

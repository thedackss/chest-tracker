using Avalonia.Controls;
using Avalonia.Layout;
using L = ChestTracker.Text;

namespace ChestTracker.Ui;

/// <summary>Quest count, totals, chests per day and the last matches. Updates live after each match.</summary>
public sealed partial class OverviewPage : UserControl
{
    private const int RecentCount = 4;
    private readonly IAppHost _app;

    public OverviewPage(IAppHost app)
    {
        _app = app;
        InitializeComponent();
        SeeAllButton.Click += (_, _) => SeeAll?.Invoke();
        foreach (var range in new[] { Range7, Range30, RangeAll })
        {
            range.IsCheckedChanged += (_, _) => UpdateChart(_app.Matches);
        }
        AttachedToVisualTree += (_, _) => _app.Changed += Refresh;
        DetachedFromVisualTree += (_, _) => _app.Changed -= Refresh;
        Refresh();
    }

    public event Action? SeeAll;

    private void Refresh()
    {
        var t = _app.Totals;
        var matches = _app.Matches;
        var today = Stats.Today(matches);
        var tracked = matches.Sum(m => m.ChestsTotal);

        QuestText.Text = L.N(t.Quest);
        QuestLabel.Text = Stats.QuestLabel(t);
        TotalText.Text = L.N(t.Real);
        TodayLabel.Text = L.T($"Today · {L.Matches(today.Count)}", $"Hoy · {L.Matches(today.Count)}");
        TodayText.Text = L.N(today.Sum(m => m.ChestsTotal));
        LastText.Text = t.Last != null ? "+" + L.N(t.Last.ChestsTotal) : "—";

        MatchesText.Text = L.N(matches.Count);
        AverageText.Text = matches.Count > 0 ? ((double)tracked / matches.Count).ToString("0.0", L.Culture) : "—";
        BestText.Text = matches.Count > 0 ? L.N(matches.Max(m => m.ChestsTotal)) : "—";
        TreasuresText.Text = "★ " + L.N(t.Treasures);

        UpdateChart(matches);
        UpdateRecent(matches);
    }

    private void UpdateChart(List<MatchRecord> matches)
    {
        int? days = Range7.IsChecked == true ? 7 : Range30.IsChecked == true ? 30 : null;
        Chart.Bars = Stats.ChestsPerDay(matches, days);
    }

    private void UpdateRecent(List<MatchRecord> matches)
    {
        RecentList.Children.Clear();
        var recent = matches.OrderByDescending(m => m.When).Take(RecentCount).ToList();
        if (recent.Count == 0)
        {
            RecentList.Children.Add(new TextBlock
            {
                Classes = { "muted" },
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(0, 6, 0, 0),
                Text = L.T("No match yet. Play a match with replays turned on.", "Aún no hay partidas. Juega una con las repeticiones activadas."),
            });
            return;
        }
        for (var i = 0; i < recent.Count; i++)
        {
            var m = recent[i];
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 10, Margin = new Avalonia.Thickness(0, 7) };
            var left = new StackPanel();
            left.Children.Add(new TextBlock { Text = Stats.ShortWhen(m), FontWeight = Avalonia.Media.FontWeight.Medium });
            left.Children.Add(new TextBlock { Text = Stats.ModeAndPlace(m), Classes = { "small" }, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
            row.Children.Add(left);
            if (m.TreasureFinds.Count > 0)
            {
                var star = new TextBlock { Text = "★" + m.TreasureFinds.Count, Classes = { "small", "star" }, VerticalAlignment = VerticalAlignment.Center };
                ToolTip.SetTip(star, string.Join("\n", m.TreasureFinds.Select(f => f.DisplayName)));
                Grid.SetColumn(star, 1);
                row.Children.Add(star);
            }
            var count = new TextBlock
            {
                Text = L.N(m.ChestsTotal),
                Classes = { "num", "gold" },
                FontSize = 20,
                MinWidth = 34,
                TextAlignment = Avalonia.Media.TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(count, 2);
            row.Children.Add(count);
            if (i > 0) RecentList.Children.Add(new Border { Classes = { "sep" } });
            RecentList.Children.Add(row);
        }
    }
}

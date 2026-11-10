using L = ChestTracker.Text;

namespace ChestTracker.Ui;

/// <summary>Figures the windows show, computed from the match history.</summary>
public static class Stats
{
    public static List<MatchRecord> Today(List<MatchRecord> matches) =>
        matches.Where(m => m.When.ToLocalTime().Date == DateTime.Now.Date).ToList();

    /// <summary>"Squads · No Build · #3".</summary>
    public static string ModeAndPlace(MatchRecord m) =>
        Pretty.Playlist(m.Playlist) + (m.Placement is int p && p > 0 ? $" · #{p}" : "");

    /// <summary>"Today 21:40", "Yesterday 23:05", else the date.</summary>
    public static string ShortWhen(MatchRecord m)
    {
        var local = m.When.ToLocalTime();
        var time = local.ToString("HH:mm", L.Culture);
        var days = (DateTime.Now.Date - local.Date).Days;
        return days switch
        {
            0 => L.T($"Today {time}", $"Hoy {time}"),
            1 => L.T($"Yesterday {time}", $"Ayer {time}"),
            _ => local.ToString("ddd d MMM, HH:mm", L.Culture),
        };
    }

    /// <summary>Chests per day for the last <paramref name="days"/> days, or since the first match (per week past 60 days).</summary>
    public static List<Bar> ChestsPerDay(List<MatchRecord> matches, int? days)
    {
        var today = DateTime.Now.Date;
        var byDay = matches.GroupBy(m => m.When.ToLocalTime().Date)
            .ToDictionary(g => g.Key, g => (Chests: g.Sum(m => m.ChestsTotal), Matches: g.Count()));
        var first = days is int d ? today.AddDays(1 - d) : byDay.Keys.DefaultIfEmpty(today).Min();
        var span = (today - first).Days + 1;

        if (days == null && span > 60)
        {
            // Weeks starting on Monday.
            var start = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
            var weeks = (today - start).Days / 7 + 1;
            var every = Math.Max(1, weeks / 6);
            return Enumerable.Range(0, weeks).Select(i =>
            {
                var from = start.AddDays(7 * i);
                var chests = byDay.Where(kv => kv.Key >= from && kv.Key < from.AddDays(7)).Sum(kv => kv.Value.Chests);
                var label = (weeks - 1 - i) % every == 0 ? from.ToString("d MMM", L.Culture) : "";
                return new Bar(chests, label, L.T($"Week of {from.ToString("d MMM", L.Culture)}: {L.Chests(chests)}",
                    $"Semana del {from.ToString("d MMM", L.Culture)}: {L.Chests(chests)}"));
            }).ToList();
        }

        var labelEvery = span <= 7 ? 1 : span <= 31 ? 7 : Math.Max(7, span / 6);
        return Enumerable.Range(0, span).Select(i =>
        {
            var day = first.AddDays(i);
            var (chests, count) = byDay.TryGetValue(day, out var v) ? v : (0, 0);
            var fromEnd = span - 1 - i;
            var label = fromEnd % labelEvery != 0 ? ""
                : fromEnd == 0 ? L.T("today", "hoy")
                : span <= 7 ? day.ToString("ddd", L.Culture)
                : day.ToString("d MMM", L.Culture);
            var tip = $"{day.ToString("ddd d MMM", L.Culture)}: {L.Chests(chests)}" + (count > 0 ? $" · {L.Matches(count)}" : "");
            return new Bar(chests, label, tip);
        }).ToList();
    }
}

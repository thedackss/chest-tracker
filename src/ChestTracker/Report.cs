using System.Net;
using System.Text;

namespace ChestTracker;

/// <summary>Writes report.html: totals, chests per day and every match. Self-contained (no downloads).</summary>
public static class Report
{
    public static void Write(Store store, Settings settings, List<MatchRecord> matches, Totals totals)
    {
        try
        {
            Store.WriteAtomic(store.ReportPath, Build(settings, matches, totals));
        }
        catch (IOException)
        {
            // Open in a browser that locks it: next time.
        }
    }

    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    public static string Build(Settings settings, List<MatchRecord> matches, Totals t)
    {
        var sb = new StringBuilder();
        var ordered = matches.OrderByDescending(m => m.When).ToList();
        var player = ordered.Select(m => m.PlayerName).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "";
        var tracked = matches.Sum(m => m.ChestsTotal);
        var avg = matches.Count > 0 ? (double)tracked / matches.Count : 0;
        var best = matches.Count > 0 ? matches.Max(m => m.ChestsTotal) : 0;
        var broken = matches.Sum(m => m.ChestsBroken);
        var buried = matches.Sum(m => m.Chests.Count(c => c.Kind == ChestKinds.Buried));

        string Tile(string value, string label, string? sub = null, bool hero = false) =>
            $"<div class=\"tile{(hero ? " hero" : "")}\"><div class=\"v\">{value}</div><div class=\"l\">{label}</div>{(sub != null ? $"<div class=\"s\">{sub}</div>" : "")}</div>";

        sb.Append($"""
            <!doctype html><html lang="{Text.Language}"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <meta http-equiv="refresh" content="30">
            <title>chest-tracker</title><style>{Css}</style></head><body><main>
            <header><h1>chest-tracker</h1><span class="muted">{H(player)} · {Text.T("updated", "actualizado")} {DateTime.Now.ToString("g", Text.Culture)}</span></header>
            <section class="tiles">
            {Tile(Text.N(t.Real), Text.T("chests in total", "cofres en total"), Text.T("opened, dug up or broken", "abiertos, desenterrados o rotos"), hero: true)}
            {Tile(Text.N(t.Quest), Text.T("quest count", "conteo de misión"), Text.T("opened only, like the in-game quest", "solo abiertos, como la misión"), hero: true)}
            {Tile(Text.N(matches.Count), Text.T("matches tracked", "partidas registradas"), settings.StartingChests > 0 ? Text.T($"+{Text.N(settings.StartingChests)} from before tracking", $"+{Text.N(settings.StartingChests)} de antes del registro") : null)}
            {Tile(avg.ToString("0.0", Text.Culture), Text.T("chests per match", "cofres por partida"))}
            {Tile(Text.N(best), Text.T("best match", "mejor partida"))}
            {Tile(Text.N(broken), Text.T("broken", "rotos"), Text.T($"{Text.N(buried)} buried dug up", $"{Text.N(buried)} enterrados"))}
            {Tile("★ " + Text.N(t.Treasures), Text.T("treasures", "tesoros"))}
            </section>
            """);

        // Chests per day, last 30 days.
        var today = DateTime.Now.Date;
        var days = Enumerable.Range(0, 30).Select(i => today.AddDays(i - 29)).ToList();
        var perDay = days.Select(d => matches.Where(m => m.When.ToLocalTime().Date == d).Sum(m => m.ChestsTotal)).ToList();
        var max = Math.Max(1, perDay.Max());
        sb.Append($"<section><h2>{Text.T("Chests per day", "Cofres por día")}</h2><div class=\"card bars\">");
        for (var i = 0; i < days.Count; i++)
        {
            var pct = perDay[i] * 100 / max;
            sb.Append($"<div class=\"bar\" title=\"{days[i].ToString("d MMM", Text.Culture)}: {perDay[i]}\"><span style=\"height:{Math.Max(pct, perDay[i] > 0 ? 3 : 0)}%\"></span><i>{(i % 5 == 4 || i == 29 ? days[i].ToString("d/M", Text.Culture) : "")}</i></div>");
        }
        sb.Append("</div></section>");

        // Matches.
        sb.Append($"""
            <section><h2>{Text.T("Matches", "Partidas")}</h2><div class="card"><table>
            <tr><th>{Text.T("When", "Cuándo")}</th><th>{Text.T("Mode", "Modo")}</th><th>{Text.T("Place", "Puesto")}</th>
            <th class="n">{Text.T("Total", "Total")}</th><th class="n">{Text.T("Quest", "Misión")}</th><th class="n">{Text.T("Broken", "Rotos")}</th><th class="n">{Text.T("Buried", "Enterrados")}</th><th>{Text.T("Treasures", "Tesoros")}</th></tr>
            """);
        foreach (var m in ordered)
        {
            var treasures = string.Join(", ", m.TreasureFinds.Select(f => "★ " + H(f.DisplayName)));
            sb.Append($"<tr><td>{H(Pretty.When(m))}</td><td>{H(Pretty.Playlist(m.Playlist))}</td><td>{H(Pretty.Placement(m))}</td>"
                + $"<td class=\"n b\">{m.ChestsTotal}</td><td class=\"n\">{m.QuestChests}</td><td class=\"n\">{m.ChestsBroken}</td>"
                + $"<td class=\"n\">{m.Chests.Count(c => c.Kind == ChestKinds.Buried)}</td><td class=\"t\">{treasures}</td></tr>");
        }
        if (ordered.Count == 0)
        {
            sb.Append($"<tr><td colspan=\"8\" class=\"muted\">{Text.T("No match yet. Play a match with replays turned on.", "Aún no hay partidas. Juega una con las repeticiones activadas.")}</td></tr>");
        }
        sb.Append("</table></div></section></main></body></html>");
        return sb.ToString();
    }

    private const string Css = """
        :root{color-scheme:dark;--bg:#0f1117;--card:#181b24;--line:#262a36;--text:#e8e8ef;--muted:#8a8fa3;--gold:#ffcd46}
        *{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:15px/1.45 "Segoe UI",system-ui,sans-serif}
        main{max-width:1100px;margin:0 auto;padding:24px 16px 48px}
        header{display:flex;flex-wrap:wrap;align-items:baseline;justify-content:space-between;gap:8px;margin-bottom:20px}
        h1{margin:0;font-size:26px;color:var(--gold)}h2{font-size:16px;margin:28px 0 10px;color:var(--muted);text-transform:uppercase;letter-spacing:.06em}
        .muted{color:var(--muted)}
        .tiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(135px,1fr));gap:12px}
        .tile{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:14px 16px}
        .tile .v{font-size:28px;font-weight:700;font-variant-numeric:tabular-nums}.tile.hero .v{color:var(--gold);font-size:34px}
        .tile .l{color:var(--text)}.tile .s{color:var(--muted);font-size:13px}
        .card{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:12px;overflow-x:auto}
        .bars{display:flex;align-items:flex-end;gap:4px;height:180px;padding-bottom:22px}
        .bar{flex:1;height:100%;display:flex;flex-direction:column;justify-content:flex-end;position:relative}
        .bar span{display:block;background:var(--gold);border-radius:4px 4px 0 0;min-height:0}
        .bar i{position:absolute;bottom:-20px;left:50%;transform:translateX(-50%);font-style:normal;font-size:11px;color:var(--muted);white-space:nowrap}
        table{width:100%;border-collapse:collapse;font-variant-numeric:tabular-nums}
        th,td{padding:7px 10px;border-bottom:1px solid var(--line);text-align:left;white-space:nowrap}
        th{color:var(--muted);font-weight:600;font-size:13px}td.n,th.n{text-align:right}td.b{color:var(--gold);font-weight:700}td.t{white-space:normal;color:#d6beff}
        tr:last-child td{border-bottom:0}
        """;
}

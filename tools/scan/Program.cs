using ChestTracker;

// chest-scan <replay or folder>... [--start N]
// Analyzes replays without touching any saved history and prints what was counted per match.
var paths = new List<string>();
var start = 0;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--start" && i + 1 < args.Length)
    {
        start = int.Parse(args[++i]);
    }
    else
    {
        paths.Add(args[i]);
    }
}
if (paths.Count == 0)
{
    Console.Error.WriteLine("usage: chest-scan <replay or folder>... [--start N]");
    return 2;
}

var files = paths
    .SelectMany(p => Directory.Exists(p) ? Directory.GetFiles(p, "*.replay") : new[] { p })
    .Select(p => new FileInfo(p))
    .OrderBy(f => f.LastWriteTimeUtc)
    .ToList();

Console.WriteLine($"{"replay",-42} {"player",-12} {"total",5} {"quest",5} {"broken",6} {"buried",6} {"treas",5}");
int total = 0, quest = 0, failed = 0;
foreach (var file in files)
{
    try
    {
        var m = Tracker.Analyze(file.FullName, TimeSpan.FromMinutes(3));
        total += m.ChestsTotal;
        quest += m.QuestChests;
        var buried = m.Chests.Count(c => c.Kind == ChestKinds.Buried);
        var treasures = string.Join(", ", m.TreasureFinds.Select(t => t.DisplayName));
        Console.WriteLine($"{file.Name,-42} {m.PlayerName,-12} {m.ChestsTotal,5} {m.QuestChests,5} {m.ChestsBroken,6} {buried,6} {m.TreasureFinds.Count,5}  {treasures}");
    }
    catch (Exception e)
    {
        failed++;
        Console.WriteLine($"{file.Name,-42} ERROR {e.Message}");
    }
}
Console.WriteLine($"{files.Count - failed} matches  total={start + total}  quest={start + quest}{(start != 0 ? $"  (start {start})" : "")}");
return failed == 0 ? 0 : 1;

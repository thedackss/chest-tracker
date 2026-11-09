namespace ChestTracker;

public sealed class ChestRecord
{
    public float TimeSeconds { get; set; }
    /// <summary>A <see cref="ChestKinds"/> id.</summary>
    public string Kind { get; set; } = "";
    public string Blueprint { get; set; } = "";
    /// <summary>Broken instead of opened (floor or wall under it destroyed, explosion...).</summary>
    public bool Destroyed { get; set; }
    public List<string> Items { get; set; } = new();
}

public sealed class TreasureFind
{
    /// <summary>Prize id ("DelilahPrize:dpid_item4"), unknown-treasure key, or item blueprint.</summary>
    public string Item { get; set; } = "";
    public float? ObtainedAtSeconds { get; set; }
    /// <summary>Detected from the golden treasure chest itself (prize known for sure).</summary>
    public bool FromGoldenChest { get; set; }
    /// <summary>A <see cref="ChestKinds"/> id ("" when unknown).</summary>
    public string SourceKind { get; set; } = "";
    public bool SourceDestroyed { get; set; }
    /// <summary>Team treasure: the teammate whose chest gave it ("" when out of sight).</summary>
    public string? Teammate { get; set; }

    public string DisplayName => TreasureNames.Friendly(Item);
}

public sealed class MatchRecord
{
    public string SessionId { get; set; } = "";
    public string ReplayFile { get; set; } = "";
    public DateTime AnalyzedAtUtc { get; set; }
    public DateTime? MatchStartUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public string? Playlist { get; set; }
    public string? GameVersion { get; set; }
    public string? PlayerName { get; set; }
    public string? EpicId { get; set; }
    public int? Placement { get; set; }
    public int? TotalPlayers { get; set; }
    public int? Eliminations { get; set; }
    public int AnalyzerVersion { get; set; }
    public bool IsBattleRoyale { get; set; } = true;
    public List<ChestRecord> Chests { get; set; } = new();
    /// <summary>Non-chest interactions by kind (ammo boxes, loot cannons...).</summary>
    public Dictionary<string, int> OtherInteractions { get; set; } = new();
    public List<TreasureFind> TreasureFinds { get; set; } = new();
    /// <summary>Treasures found by teammates (shared with the team). Never in the player's own totals.</summary>
    public List<TreasureFind> TeamTreasureFinds { get; set; } = new();

    public DateTime When => MatchStartUtc ?? AnalyzedAtUtc;

    /// <summary>Every chest the player opened, dug up or broke.</summary>
    public int ChestsTotal => Chests.Count;

    /// <summary>Chests opened or dug up, not broken: what the in-game "Open treasure chests" quest counts.</summary>
    public int QuestChests => Chests.Count(c => !c.Destroyed);

    public int ChestsBroken => Chests.Count(c => c.Destroyed);
}

public sealed class Settings
{
    public string? Language { get; set; }
    public string DemosFolder { get; set; } = DefaultDemosFolder;

    /// <summary>Chests opened before tracking started (e.g. the in-game quest value), added to both totals.</summary>
    public int StartingChests { get; set; }

    /// <summary>Starts with Windows, waits in the tray and shows the overlay while Fortnite runs.</summary>
    public bool StartWithFortnite { get; set; }

    public bool ShowOverlay { get; set; } = true;
    /// <summary>Hide the locked overlay while Fortnite is not running (unlocked it always shows, to be placed).</summary>
    public bool OverlayOnlyInGame { get; set; } = true;
    public bool OverlayLocked { get; set; }
    public int? OverlayX { get; set; }
    public int? OverlayY { get; set; }
    /// <summary>Overlay scale in percent.</summary>
    public int OverlaySize { get; set; } = 100;

    public static string DefaultDemosFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteGame", "Saved", "Demos");
}

public sealed class TrackerData
{
    public List<MatchRecord> Matches { get; set; } = new();

    /// <summary>Key = file name|size|last write ticks. Value = session id, "skipped", "incomplete" or "error: ...".</summary>
    public Dictionary<string, string> ProcessedFiles { get; set; } = new();
}

/// <summary>Totals shown everywhere (overlay, tray, report, scan tool).</summary>
public sealed record Totals(int Real, int Quest, int Matches, int Treasures, int Session, int SessionMatches, MatchRecord? Last)
{
    public static Totals From(TrackerData data, Settings settings, DateTime sessionStartUtc)
    {
        var session = data.Matches.Where(m => m.AnalyzedAtUtc >= sessionStartUtc || m.When >= sessionStartUtc).ToList();
        return new Totals(
            settings.StartingChests + data.Matches.Sum(m => m.ChestsTotal),
            settings.StartingChests + data.Matches.Sum(m => m.QuestChests),
            data.Matches.Count,
            data.Matches.Sum(m => m.TreasureFinds.Count),
            session.Sum(m => m.ChestsTotal),
            session.Count,
            data.Matches.MaxBy(m => m.When));
    }
}

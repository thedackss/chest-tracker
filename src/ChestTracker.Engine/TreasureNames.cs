namespace ChestTracker;

/// <summary>Display names of treasure prizes and items. Adapted from Fortnite-ChestCounter (MIT).</summary>
public static class TreasureNames
{
    /// <summary>Key prefix of a treasure whose prize id is not in the replay.</summary>
    public const string UnknownPrefix = "Unknown:";

    /// <summary>Raw item stored for a treasure seen only through its prize handler.</summary>
    public const string HandlerItem = "GA_TreasureHandler";

    private static string? KnownPrize(string id) => id.ToLowerInvariant() switch
    {
        "delilahprize:dpid_item1" => Text.T("A$AP Rocky solid gold gauntlets (pickaxe) ?", "Guanteletes de oro macizo de A$AP Rocky (pico) ?"),
        "delilahprize:dpid_item2" => Text.T("Plunder in Paradise (loading screen)", "Plunder in Paradise (pantalla de carga)"),
        "delilahprize:dpid_item3" => Text.T("Mercedes-AMG G 63 (vehicle) ?", "Mercedes-AMG G 63 (vehículo) ?"),
        "delilahprize:dpid_item4" => Text.T("No Trespassing (music track)", "No Trespassing (pista musical)"),
        _ => null,
    };

    /// <summary>Display name of a prize id ("DelilahPrize:dpid_item4"), an unknown-treasure key or an item blueprint.</summary>
    public static string Friendly(string item)
    {
        if (item.Contains(':'))
        {
            if (KnownPrize(item) is { } known)
            {
                return known;
            }
            if (item.StartsWith(UnknownPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // One of the treasure hunt gizmos; which one is not in the replay.
                return Text.T("Treasure gizmo", "Artilugio de tesoro");
            }
            return Text.T("Golden chest reward", "Recompensa de cofre dorado");
        }

        var i = item.ToLowerInvariant();
        if (i == HandlerItem.ToLowerInvariant()) return Text.T("Treasure gizmo", "Artilugio de tesoro");
        if (i.Contains("flameceleb")) return Text.T("Golden firework", "Fuego artificial dorado");
        if (i.Contains("relicextractor")) return Text.T("Portable extractor", "Extractor portátil");
        if (i.Contains("relicfinder")) return Text.T("Sprite locator", "Localizador de espíritus");
        if (i.Contains("luckylocator")) return Text.T("Lucky locator", "Localizador de la suerte");
        if (i.Contains("smugglerextractionkey") || i.Contains("extractionaccelerat")) return Text.T("Extraction accelerator", "Acelerador de extracción");
        return item;
    }
}

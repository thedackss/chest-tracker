namespace ChestTracker;

/// <summary>
/// Classifies container blueprints. Adapted from Fortnite-ChestCounter (MIT, see THIRD_PARTY_NOTICES.md).
/// The ids are stored in the history, so they never change; <see cref="Name"/> gives the display name.
/// </summary>
public static class ChestKinds
{
    // Chests (counted in the totals)
    public const string Normal = "chest";
    public const string Rare = "rare-chest";
    public const string Buried = "buried-chest";
    /// <summary>Rare sprite chest behind the locked vault doors: B_TieredChest_Relics_C and its variants.</summary>
    public const string Sprite = "sprite-chest";
    public const string Poster = "poster-chest";
    public const string Other = "other-chest";

    // Other interactions (tracked apart, never in the chest totals)
    public const string Cannon = "loot-cannon";
    public const string AmmoBox = "ammo-box";
    public const string FoodBox = "food-box";
    public const string OtherContainer = "other-container";

    private static readonly HashSet<string> ChestSet = new() { Normal, Rare, Buried, Sprite, Poster, Other };

    public static bool IsChest(string kind) => ChestSet.Contains(kind);

    public static string Classify(string blueprint)
    {
        var b = blueprint.ToLowerInvariant();
        if (b.Contains("lootcannon") || b.Contains("cannon")) return Cannon;
        if (b.Contains("ammo")) return AmmoBox;
        if (b.Contains("foodbox") || b.Contains("food_box")) return FoodBox;
        if (b.Contains("rarechest") || b.Contains("legendary") || b.Contains("rare_chest")) return Rare;
        if (b.Contains("buried")) return Buried;
        if (b.Contains("spriteposter") || b.Contains("booty")) return Poster;
        if (b.Contains("relic") || b.Contains("extraction")) return Sprite;
        if (b.Contains("tiered_chest") || b.Contains("normalchest") || b.Contains("standardchest")) return Normal;
        // Coolers, barrels, etc. are not chests.
        return b.Contains("chest") ? Other : OtherContainer;
    }

    public static string Name(string kind) => kind switch
    {
        Normal => Text.T("Chest", "Cofre"),
        Rare => Text.T("Rare chest", "Cofre raro"),
        Buried => Text.T("Buried chest", "Cofre enterrado"),
        Sprite => Text.T("Sprite chest (rare)", "Cofre de espíritu (raro)"),
        Poster => Text.T("Sprite poster chest", "Cofre de carteles de espíritu"),
        Other => Text.T("Other chest", "Otro cofre"),
        Cannon => Text.T("Loot cannon", "Cañón de botín"),
        AmmoBox => Text.T("Ammo box", "Caja de munición"),
        FoodBox => Text.T("Food box", "Caja de comida"),
        OtherContainer => Text.T("Other container", "Otro contenedor"),
        _ => Text.T("Unknown", "Desconocido"),
    };
}

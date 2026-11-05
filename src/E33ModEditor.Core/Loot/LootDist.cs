namespace E33ModEditor.Core.Loot;

/// <summary>Chest / drop bucket counts for vanilla and tiered randomizer rolls.</summary>
public static class LootDist
{
    public const string Potions = "potions";
    public const string Pictos = "pictos";
    public const string Lumina = "lumina";
    public const string Weapons = "weapons";
    public const string Chroma = "chroma";
    public const string Catalysts = "catalysts";
    public const string Protected = "protected";
    public const string Other = "other";

    public static readonly string[] Buckets =
        { Potions, Pictos, Lumina, Weapons, Chroma, Catalysts, Protected, Other };

    public static string Bucket(LootCatalog catalog, string id)
    {
        if (string.IsNullOrEmpty(id)) return Other;
        if (catalog.IsProtected(id)) return Protected;
        if (id is "Consumable_LuminaPoint" or "Generic_LuminaPoint" or "Generic_5xLuminaPoint")
            return Lumina;
        if (id.StartsWith("Consumable_Health_", StringComparison.Ordinal)
            || id.StartsWith("Consumable_Energy_", StringComparison.Ordinal)
            || id.StartsWith("Consumable_Revive_", StringComparison.Ordinal))
            return Potions;
        var type = catalog.TypeOf(id);
        return type switch
        {
            "Pictos" => Pictos,
            "Weapon" => Weapons,
            "Gold" => Chroma,
            "Upgrade Material" or "Upgrade roll" or "Shard" => Catalysts,
            "Inventory" when id.Contains("Lumina", StringComparison.Ordinal) => Lumina,
            _ => Other,
        };
    }

    public sealed class Cell
    {
        public double Expected { get; set; }
        public int Slots { get; set; }
    }

    public static Dictionary<(string Source, int Tier, string Bucket), Cell> Vanilla(LootCatalog catalog)
    {
        var d = NewGrid();
        foreach (var check in catalog.Checks)
        {
            if (check.Source != "Chest" && check.Source != LootCatalog.PaintCageSource
                && check.Source != "Enemy") continue;
            foreach (var slot in check.Slots)
            {
                var b = slot.LootTableRef ? Catalysts : Bucket(catalog, slot.ItemId);
                Add(d, check.Source, check.Tier, b, slot.Quantity * slot.Chance / 100.0, 1);
            }
        }
        return d;
    }

    public static Dictionary<(string Source, int Tier, string Bucket), Cell> Randomized(
        LootCatalog catalog, RandomizerKit.Settings s, Random rng)
    {
        var d = NewGrid();
        foreach (var check in catalog.Checks.Where(c => c.Source == "Chest"))
        {
            foreach (var slot in check.Slots)
            {
                if (slot.LootTableRef)
                {
                    Add(d, "Chest", check.Tier, Catalysts, 1, 1);
                    continue;
                }
                var id = slot.ItemId;
                if (catalog.IsProtected(id) || catalog.BrokenItems.Contains(id)
                    || catalog.TypeOf(id) is "Upgrade roll" or "Gold" or "Shard" or "Music Record"
                    || id is "Consumable_LuminaPoint" or "Consumable_Respec")
                {
                    Add(d, "Chest", check.Tier, Bucket(catalog, id), slot.Quantity * slot.Chance / 100.0, 1);
                    continue;
                }
                // New chest model: E[rolls]=1.4 (70/20/10); per roll pictos 25%,
                // weapon 3%, lumina 36%, chroma 36%. Tint/shape quotas below are
                // carved out of lumina+chroma by overriding drops.
                const double rolls = 1.4;
                Add(d, "Chest", check.Tier, Pictos, 0.25 * rolls, 1);
                Add(d, "Chest", check.Tier, Weapons, 0.03 * rolls, 1);
                Add(d, "Chest", check.Tier, Lumina, 0.36 * rolls, 1);
                Add(d, "Chest", check.Tier, Chroma, 0.36 * rolls, 1);
            }
        }

        // Paint Cages: one guaranteed tier-appropriate gear item per row. The
        // coin flip between pictos and weapon is modelled as 50/50.
        foreach (var check in catalog.Checks.Where(c => c.Source == LootCatalog.PaintCageSource))
        {
            Add(d, LootCatalog.PaintCageSource, check.Tier, Pictos, 0.5, 1);
            Add(d, LootCatalog.PaintCageSource, check.Tier, Weapons, 0.5, 1);
        }

        // Capacity shards (Healing / Energy / Revive tint shards) + Chroma
        // Elixir Shards, and the potency shapes, all placed by the randomizer.
        for (int si = 0; si < 3; si++)
        {
            for (int i = 0; i < s.TintsPerType; i++)
                Add(d, "Chest", 1 + ((si + i) % 4), Catalysts, 1, 1);
        }
        for (int i = 0; i < s.ChromaElixirShards; i++)
            Add(d, "Chest", 1 + (i % 4), Catalysts, 1, 1);
        for (int i = 0; i < s.ShapesPerType * 3; i++)
            Add(d, "Chest", 1 + (i % 4), Other, 1, 1);

        bool chalierCounted = false;
        foreach (var check in catalog.Checks.Where(c => c.Source == "Enemy"))
        {
            catalog.Enemies.TryGetValue(check.Id, out var enemy);
            var arch = enemy?.Archetype ?? "Regular";
            // Mirror the realizer: the row's own IsBoss flag promotes rows the
            // archetype column mislabels (the Elite Dualliste solos, François)
            // to the boss recipe, so the model matches what ships. White-nevron
            // fights are always bossed (RandomizerKit.WhiteNevronBoss).
            if (RandomizerKit.WhiteNevronBoss.Contains(check.Id))
                arch = "Boss";
            else if (arch is not ("Boss" or "Alpha" or "SuperBoss" or "Petank" or "Merchant")
                && check.IsBoss && RandomizerKit.IsRealBossRow(check.Id, enemy, arch))
                arch = "Boss";
            if (arch == "Merchant") continue;
            var tier = check.Tier;
            foreach (var slot in check.Slots)
            {
                if (catalog.IsProtected(slot.ItemId))
                    Add(d, "Enemy", tier, Protected, slot.Quantity * slot.Chance / 100.0, 1);
            }

            // The Chalier trio shares one payout (only one variant is fought),
            // so the boss package is modeled once even though all three rows
            // keep their own protected slots above.
            if (RandomizerKit.ChalierTrio.Contains(check.Id))
            {
                if (chalierCounted) continue;
                chalierCounted = true;
            }

            var overworld = check.Id.StartsWith("WM_", StringComparison.Ordinal)
                || check.DisplayName.Contains("(World Map)", StringComparison.OrdinalIgnoreCase);
            if (arch == "Petank")
                Add(d, "Enemy", tier, Lumina, Math.Max(1, s.PetankLumina), 1);
            else if (arch is "SuperBoss")
            {
                // Fixed rule: 2 weapons + 2 pictos, 66% third picto, 33% fourth.
                Add(d, "Enemy", tier, Pictos, 2.99, 4);
                Add(d, "Enemy", tier, Weapons, 2, 2);
                // Catalysts: T4 1×L5 + 3–5×L4; T5 low 2×L5 + 6–10×L4;
                // T5 high 3×L5 + 11–25×L4 (band by level). Expected ~mid-band.
                Add(d, "Enemy", tier, Catalysts, tier >= 5 ? 15.5 : (tier >= 4 ? 5.0 : 4.0), tier >= 4 ? 2 : 1);
                Add(d, "Enemy", tier, Lumina, 5, 1);
            }
            else if (arch is "Boss" or "Alpha")
            {
                // Fixed rule: 1 weapon + 1 picto, 66% second picto, 33% third.
                Add(d, "Enemy", tier, Pictos, 1.99, 3);
                Add(d, "Enemy", tier, Weapons, 1, 1);
                Add(d, "Enemy", tier, Catalysts, tier >= 5 ? 15.5 : (tier >= 4 ? 5.0 : 4.0), tier >= 4 ? 2 : 1);
                // The chromatic Petank replaces the standard boss 5 lumina
                // with its signature 1000, rather than stacking both.
                Add(d, "Enemy", tier, Lumina,
                    check.Id == "WM_Petank_Alpha" ? 1000 : 5, 1);
            }
            else if (overworld)
            {
                Add(d, "Enemy", tier, Pictos, 0.5, 1);
                Add(d, "Enemy", tier, Weapons, 0.5, 1);
                // Field economy: 30% at uniform 1–3 catalysts (capped L4), plus
                // the lower-tier rolls below, and 1 lumina at 4%/tier.
                AddFieldCatalysts(d, tier);
                Add(d, "Enemy", tier, Lumina, 0.04 * tier, 1);
            }
            else if (arch == "Elite")
            {
                Add(d, "Enemy", tier, Pictos, s.ElitePictosChance / 100.0, 1);
                AddFieldCatalysts(d, tier);
                Add(d, "Enemy", tier, Lumina, 0.04 * tier, 1);
            }
            else
            {
                Add(d, "Enemy", tier, Pictos, s.RegularPictosChance / 100.0, 1);
                AddFieldCatalysts(d, tier);
                Add(d, "Enemy", tier, Lumina, 0.04 * tier, 1);
            }

            // Rare endgame Perfect Catalyst on top of the recipe. arch is already
            // promoted to "Boss" above for IsBoss rows, so this mirrors
            // RandomizerKit.QualifiesForT5PerfectCatalyst exactly.
            if (s.T5PerfectCatalystChance > 0
                && RandomizerKit.QualifiesForT5PerfectCatalyst(
                    check.Id, arch, check.AreaLevel, check.Tier, arch is "Boss", overworld))
                Add(d, "Enemy", tier, Catalysts, s.T5PerfectCatalystChance / 100.0, 1);
        }
        return d;
    }

    /// <summary>
    /// Expected catalyst quantity for a non-boss field drop, mirroring
    /// <c>RandomizerKit.AddFieldDrops</c>: the row's own catalyst (L4 cap) plus
    /// x1 one tier below at 50% and x1 two tiers below at 30%. T5 pays its L4 at
    /// 60% / 2–6, so its expected base is 2.4 rather than the usual 0.6.
    /// </summary>
    private static void AddFieldCatalysts(
        Dictionary<(string, int, string), Cell> d, int tier)
    {
        var t = Math.Clamp(tier, 1, 5);
        var catalystTier = Math.Min(t, 4);
        var baseChance = t >= 5 ? 0.60 : 0.30;
        var baseQty = t >= 5 ? 4.0 : 2.0;
        Add(d, "Enemy", t, Catalysts, baseChance * baseQty, 1);
        if (catalystTier >= 2)
            Add(d, "Enemy", t, Catalysts, RandomizerKit.CatalystOneTierBelowChance / 100.0, 1);
        if (catalystTier >= 3)
            Add(d, "Enemy", t, Catalysts, RandomizerKit.CatalystTwoTiersBelowChance / 100.0, 1);
    }

    public static string Format(
        Dictionary<(string Source, int Tier, string Bucket), Cell> data, string title)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(title);
        foreach (var source in new[] { "Chest", LootCatalog.PaintCageSource, "Enemy" })
        {
            sb.AppendLine();
            sb.AppendLine($"{source,-12} " + string.Join(" ", Buckets.Select(b => $"{b,10}")));
            for (int t = 1; t <= 5; t++)
            {
                sb.Append($"T{t}     ");
                foreach (var b in Buckets)
                {
                    data.TryGetValue((source, t, b), out var cell);
                    var v = cell?.Expected ?? 0;
                    sb.Append($" {v,10:0.#}");
                }
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    private static Dictionary<(string, int, string), Cell> NewGrid() =>
        new();

    private static void Add(
        Dictionary<(string, int, string), Cell> d,
        string source, int tier, string bucket, double expected, int slots)
    {
        var key = (source, Math.Clamp(tier, 1, 5), bucket);
        if (!d.TryGetValue(key, out var cell))
            d[key] = cell = new Cell();
        cell.Expected += expected;
        cell.Slots += slots;
    }

}

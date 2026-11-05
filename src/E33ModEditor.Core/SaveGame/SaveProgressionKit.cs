using E33ModEditor.Core.Loot;
using Newtonsoft.Json.Linq;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;

namespace E33ModEditor.Core.SaveGame;

/// <summary>
/// Rebuilds a save's pictos/weapon ownership to match a randomized pack's
/// reachable sources for a chosen story point. Placements are read back from
/// the staged pack (<c>work/mod</c>) — chests, enemy drops, merchant stock and
/// the white-nevron dialogue grants — then filtered by an area scope, and the
/// resulting set is written into the save (inventory + passive/weapon
/// progressions). Everything that is not a pictos or weapon is left untouched.
/// </summary>
public static class SaveProgressionKit
{
    public sealed record PlannedItem(
        string Id, string Type, int Tier, int Level,
        string Source, string Area, int AreaLevel);

    public sealed record Plan(
        IReadOnlyList<PlannedItem> Pictos,
        IReadOnlyList<PlannedItem> Weapons,
        IReadOnlyDictionary<string, int> CountsBySource);

    // ───────────────────── scope ─────────────────────

    /// <summary>
    /// Areas considered reachable up to and including Monoco's Station, plus the
    /// optional content the user has explicitly cleared (Dark Shores chests,
    /// Crushing Cavern, Abbest Cave, the Bourgeon fights, Yellow Harvest, the
    /// world map). Matched against the catalog's area display name so chests and
    /// enemies of the same place agree.
    /// </summary>
    public static readonly string[] InScopeAreas =
    {
        // Story 1-9
        "Lumiere", "The Manor", "Manor", "Camp", "Mini levels", "World Map",
        "Spring Meadows",
        "Flying Waters",
        "Ancient Sanctuary",
        "Gestral Village",
        "Esquie's Nest",
        "Stone Wave Cliffs",
        "Stone Wave Cliffs Cave",
        "Forgotten Battlefield",
        "Stone Quarry",
        "Monoco's Station",
        // Early optionals + user-cleared extras
        "Yellow Harvest",
        "Dark Shores",
        "Crushing Cavern",
        "Abbest Cave",
        "The Small Bourgeon",
    };

    /// <summary>Merchant table stems whose stock is in scope.</summary>
    public static readonly HashSet<string> InScopeMerchants = new(StringComparer.OrdinalIgnoreCase)
    {
        "DT_Merchant_GoblusLair",
        "DT_Merchant_GestralVillage1", "DT_Merchant_GestralVillage2",
        "DT_Merchant_SeaCliff",
        "DT_Merchant_ForgottenBattlefield",
        "DT_Merchant_GrandisStation",
        "DT_Merchant_YellowForest",
        "DT_Merchant_WM_1", "DT_Merchant_WM_2", "DT_Merchant_WM_4", "DT_Merchant_WM_5",
        "DT_Merchant_WM_6", "DT_Merchant_WM_7", "DT_Merchant_WM_8", "DT_Merchant_WM_10",
        "DT_Merchant_WM_11", "DT_Merchant_WM_12",
    };

    /// <summary>Starting weapons for the recruited cast (Verso is in the party;
    /// Monoco has not joined yet, so Monocaro is excluded).</summary>
    public static readonly string[] StartingWeapons =
    {
        "Noahram", "Lunerim", "Maellum", "Scieleson", "Verleso",
    };

    public static bool InScope(string area, int areaLevel)
    {
        if (string.IsNullOrEmpty(area)) return false;
        if (area.Contains("Act 3", StringComparison.OrdinalIgnoreCase)) return false;
        if (area.Contains("Cut Content", StringComparison.OrdinalIgnoreCase)) return false;
        if (area.StartsWith("DLC", StringComparison.OrdinalIgnoreCase)) return false;
        if (area.Contains("Generic", StringComparison.OrdinalIgnoreCase)) return false;
        if (area.Contains("Placeholder", StringComparison.OrdinalIgnoreCase)) return false;
        // "Lumiere" is both the Act 1 prologue (level 1) and Act 3 (level 99).
        if (area.Equals("Lumiere", StringComparison.OrdinalIgnoreCase) && areaLevel > 25) return false;
        foreach (var a in InScopeAreas)
            if (area.Equals(a, StringComparison.OrdinalIgnoreCase)
                || area.StartsWith(a + " ", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>Chest scope: the reachable areas (Dark Shores chests are in).</summary>
    public static bool InScopeChest(string area, int areaLevel) => InScope(area, areaLevel);

    /// <summary>
    /// Enemy scope: same areas, but only T1 + low-T2 fights (the user defeated
    /// "all open world enemies t1 low t2" plus side bosses through Forgotten
    /// Battlefield). Dark Shores was chests-only, and the Camp Monoco duel is a
    /// later quest, so both are excluded here.
    /// </summary>
    public static bool InScopeEnemy(string area, int areaLevel, int tier)
    {
        if (!InScope(area, areaLevel)) return false;
        if (area.Equals("Dark Shores", StringComparison.OrdinalIgnoreCase)) return false;
        if (area.Equals("Camp", StringComparison.OrdinalIgnoreCase)) return false;
        if (tier > 2) return false;
        if (area.Equals("World Map", StringComparison.OrdinalIgnoreCase) && areaLevel > 30) return false;
        return true;
    }

    public static int TierTop(int tier) => Math.Clamp(tier, 1, 5) switch
    {
        1 => 8,
        2 => 13,
        3 => 20,
        4 => 26,
        _ => 33,
    };

    // ───────────────────── plan ─────────────────────

    public static Plan Build(string modDir, LootCatalog catalog, int maxTier = 5, Action<string>? log = null)
    {
        EnsureDialogueSiblings(modDir);
        maxTier = Math.Clamp(maxTier, 1, 5);
        var planned = new Dictionary<string, PlannedItem>(StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        void Add(PlannedItem item)
        {
            if (item.Tier > maxTier) return;
            counts[item.Source] = counts.GetValueOrDefault(item.Source) + 1;
            planned.TryAdd(item.Id, item);
        }

        // Chests + enemy drops: walk the catalog's checks (area/tier metadata)
        // and read the staged row of the same id for what the seed actually put there.
        var chestsAsset = Find(modDir, "DT_ChestsContent.uasset");
        var enemiesAsset = Find(modDir, "DT_jRPG_Enemies.uasset");
        var chests = chestsAsset is null ? null : new DataTableEditor(chestsAsset);
        var enemies = enemiesAsset is null ? null : new DataTableEditor(enemiesAsset);

        foreach (var check in catalog.Checks)
        {
            var inScope = check.Source == "Enemy"
                ? InScopeEnemy(check.Area, check.AreaLevel, check.Tier)
                : InScopeChest(check.Area, check.AreaLevel);
            if (!inScope) continue;
            var ids = check.Source switch
            {
                "Chest" or LootCatalog.PaintCageSource when chests is not null => ChestItems(chests, check.Id),
                "Enemy" when enemies is not null => EnemyItems(enemies, check.Id),
                _ => Enumerable.Empty<string>(),
            };
            foreach (var id in ids)
                if (Classify(catalog, id) is { } item)
                    Add(item with { Source = $"{check.Source}:{check.Area}", Area = check.Area, AreaLevel = check.AreaLevel });
        }

        // Merchants: read the staged table per in-scope stem.
        foreach (var path in Directory.EnumerateFiles(modDir, "DT_Merchant_*.uasset", SearchOption.AllDirectories))
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            if (!InScopeMerchants.Contains(stem)) continue;
            var ed = new DataTableEditor(path);
            foreach (var id in MerchantItems(ed))
                if (Classify(catalog, id) is { } item)
                    Add(item with { Source = "Merchant:" + stem });
        }

        // White-nevron dialogue grants (all of them, per the user's instruction).
        foreach (var path in DialogueAssets(modDir))
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            foreach (var prop in new[] { "Reward", "RewardOnCompletion", "Rewards" })
                foreach (var id in DialogueRewardKit.ReadRewardIds(path, prop))
                    if (Classify(catalog, id) is { } item)
                        Add(item with { Source = "WhiteNevron:" + stem });
        }

        // Starting weapons are never placed by the seed.
        foreach (var id in StartingWeapons)
            if (Classify(catalog, id) is { } item)
                Add(item with { Source = "Starting" });

        var all = planned.Values.ToList();
        log?.Invoke($"[stage] planned {all.Count(i => i.Type == "Pictos")} pictos, " +
                    $"{all.Count(i => i.Type == "Weapon")} weapons from {planned.Count} distinct ids");
        return new Plan(
            all.Where(i => i.Type == "Pictos").OrderBy(i => i.Id, StringComparer.Ordinal).ToList(),
            all.Where(i => i.Type == "Weapon").OrderBy(i => i.Id, StringComparer.Ordinal).ToList(),
            counts);
    }

    /// <summary>Pictos/weapons currently in the save that the plan does not allow.</summary>
    public static (List<string> Pictos, List<string> Weapons) Disallowed(GvasFile file, LootCatalog catalog, Plan plan)
    {
        var allowed = new HashSet<string>(plan.Pictos.Concat(plan.Weapons).Select(i => i.Id), StringComparer.OrdinalIgnoreCase);
        var pictos = new List<string>();
        var weapons = new List<string>();
        foreach (var id in CurrentGear(file))
        {
            if (allowed.Contains(id)) continue;
            switch (Classify(catalog, id)?.Type)
            {
                case "Pictos": pictos.Add(id); break;
                case "Weapon": weapons.Add(id); break;
            }
        }
        return (pictos.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                weapons.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    /// <summary>Every pictos/weapon id the save currently references.</summary>
    private static IEnumerable<string> CurrentGear(GvasFile file)
    {
        foreach (var id in SaveGameEditor.InventoryIds(file, out _)) yield return id;
        foreach (var item in SaveGameEditor.PassiveProgressions(file).Items)
            if (IdOf(item) is { Length: > 0 } pid) yield return pid;
        foreach (var item in SaveGameEditor.Progressions(file).Items)
            if (IdOf(item) is { Length: > 0 } wid) yield return wid;

        static string IdOf(TypedValue item) =>
            item.Payload is GvasPayload.Struct st
                ? st.Fields?.FirstOrDefault(f =>
                        f.Name.StartsWith("PassiveEffectName", StringComparison.Ordinal) ||
                        f.Name.StartsWith("DefinitionID", StringComparison.Ordinal))
                    ?.Value.Payload as string ?? ""
                : "";
    }

    // ───────────────────── apply ─────────────────────

    public static void Apply(GvasFile file, LootCatalog catalog, Plan plan, Action<string>? log = null)
    {
        foreach (var item in plan.Pictos)
        {
            SaveGameEditor.SetPictosFound(file, item.Id, true);
            SaveGameEditor.SetPictosMastered(file, item.Id, true);
            SaveGameEditor.SetPictosLevel(file, item.Id, item.Level);
        }
        foreach (var item in plan.Weapons)
        {
            SaveGameEditor.SetItemOwned(file, item.Id, true);
            SaveGameEditor.SetItemLevel(file, item.Id, item.Level);
        }

        var (pictos, weapons) = Disallowed(file, catalog, plan);

        // A character must always keep a weapon in its equipment slot. Removing
        // the equipped weapon outright deleted the FEquipmentSlot entry and left
        // the character unable to equip anything. Instead re-point the slot to an
        // allowed weapon of the same character (starting weapon preferred); if
        // none exists, keep the equipped weapon so the slot stays valid.
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in SaveGameEdits.Characters(file))
        {
            foreach (var slot in SaveGameEdits.EquipmentOf(file, c.Id))
            {
                if (!slot.ItemType.Equals(SaveGameEdits.ItemTypeWeapon, StringComparison.OrdinalIgnoreCase)) continue;
                if (!weapons.Contains(slot.ItemId, StringComparer.OrdinalIgnoreCase)) continue;
                if (PreferredWeapon(catalog, plan, c.Id) is { } replacement)
                    SaveGameEdits.SetEquipment(file, c.Id, slot.ItemType, slot.SlotIndex, replacement);
                else
                    keep.Add(slot.ItemId);
            }
        }

        foreach (var id in pictos) SaveGameEditor.SetPictosFound(file, id, false);
        foreach (var id in weapons)
        {
            if (keep.Contains(id)) continue;
            SaveGameEditor.SetPictosFound(file, id, false); // inventory + progressions + pictos loadout
        }
        log?.Invoke($"[stage] granted {plan.Pictos.Count} pictos + {plan.Weapons.Count} weapons; " +
                    $"removed {pictos.Count} pictos + {weapons.Count - keep.Count} weapons" +
                    (keep.Count > 0 ? $" (kept {keep.Count} equipped)" : ""));
    }

    /// <summary>Starting weapon per character; also the safe fallback when an
    /// equipped weapon is removed.</summary>
    private static readonly Dictionary<string, string> StartingWeaponByCharacter = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Gustave"] = "Noahram", ["Frey"] = "Noahram",
        ["Lune"] = "Lunerim", ["Maelle"] = "Maellum",
        ["Sciel"] = "Scieleson", ["Verso"] = "Verleso", ["Monoco"] = "Monocaro",
    };

    private static string? PreferredWeapon(LootCatalog catalog, Plan plan, string charId)
    {
        if (StartingWeaponByCharacter.TryGetValue(charId, out var starting)
            && plan.Weapons.Any(w => w.Id.Equals(starting, StringComparison.OrdinalIgnoreCase)))
            return starting;
        foreach (var w in plan.Weapons)
        {
            var owner = DialogueRewardKit.WeaponCharacter(catalog, w.Id);
            if (owner.Equals(charId, StringComparison.OrdinalIgnoreCase)
                || (charId.Equals("Frey", StringComparison.OrdinalIgnoreCase)
                    && owner.Equals("Gustave", StringComparison.OrdinalIgnoreCase)))
                return w.Id;
        }
        return null;
    }

    // ───────────────────── reading staged assets ─────────────────────

    private static PlannedItem? Classify(LootCatalog catalog, string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        // Tier-list membership is the authoritative pool classification (the
        // randomizer only ever places tiered items), and it covers custom
        // pictos that are absent from item_data.json.
        for (var t = 1; t <= 5; t++)
        {
            if (catalog.Tiers.TryGetValue($"Pictos_Tier{t}", out var p) && p.Contains(id))
                return new PlannedItem(id, "Pictos", t, TierTop(t), "", "", 0);
            if (catalog.Tiers.TryGetValue($"Weapon_Tier{t}", out var w) && w.Contains(id))
                return new PlannedItem(id, "Weapon", t, TierTop(t), "", "", 0);
        }
        return null;
    }

    private static IEnumerable<string> ChestItems(DataTableEditor ed, string rowId)
    {
        if (Row(ed, rowId) is not { } fields) yield break;
        if (Named(fields, "LootTableSections") is not ArrayPropertyData secs) yield break;
        foreach (var sec in secs.Value.OfType<StructPropertyData>())
        {
            if (Named(sec.Value, "LootEntries") is not ArrayPropertyData ents) continue;
            foreach (var ent in ents.Value.OfType<StructPropertyData>())
                if (Named(ent.Value, "ItemID") is NamePropertyData nm && nm.Value is not null)
                    yield return nm.Value.ToString();
        }
    }

    private static IEnumerable<string> EnemyItems(DataTableEditor ed, string rowId)
    {
        if (Row(ed, rowId) is not { } fields) yield break;
        if (Named(fields, "PossibleLoot") is not ArrayPropertyData loot) yield break;
        foreach (var ent in loot.Value.OfType<StructPropertyData>())
            if (Named(ent.Value, "Item") is StructPropertyData item
                && Named(item.Value, "RowName") is NamePropertyData nm && nm.Value is not null)
                yield return nm.Value.ToString();
    }

    private static IEnumerable<string> MerchantItems(DataTableEditor ed)
    {
        foreach (var row in ed.Table.Table.Data)
        {
            if (Named(row.Value, "ItemRowName") is NamePropertyData nm && nm.Value is not null)
                yield return nm.Value.ToString();
            else if (row.Name is not null)
                yield return DataTableEditor.CleanName(row.Name);
        }
    }

    private static IEnumerable<string> DialogueAssets(string modDir) =>
        Directory.EnumerateFiles(modDir, "BP_Dialog*.uasset", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(modDir, "BP_Dialogue*.uasset", SearchOption.AllDirectories))
            .Distinct(StringComparer.Ordinal);

    /// <summary>
    /// The randomizer deletes the sibling enum assets a dialogue BP needs for
    /// offline parsing (the game resolves them itself). Restore them from the
    /// dialogue cache so the CDO can be read; the next rebuild removes them
    /// again, so they never ship.
    /// </summary>
    private static void EnsureDialogueSiblings(string modDir)
    {
        var repoRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(modDir)));
        if (repoRoot is null) return;
        var cache = Path.Combine(repoRoot, "work", "dialogue_rewards");
        if (!Directory.Exists(cache)) return;
        foreach (var path in DialogueAssets(modDir))
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            foreach (var sib in DialogueRewardKit.SiblingsOf(stem))
            {
                var dir = Path.GetDirectoryName(path)!;
                if (File.Exists(Path.Combine(dir, sib + ".uasset"))) continue;
                var src = Directory.EnumerateFiles(cache, sib + ".uasset", SearchOption.AllDirectories).FirstOrDefault();
                if (src is null) continue;
                foreach (var ext in new[] { ".uasset", ".uexp" })
                {
                    var s = Path.ChangeExtension(src, ext);
                    if (File.Exists(s)) File.Copy(s, Path.Combine(dir, sib + ext), overwrite: true);
                }
            }
        }
    }

    private static List<PropertyData>? Row(DataTableEditor ed, string rowId)
    {
        foreach (var row in ed.Table.Table.Data)
            if (DataTableEditor.CleanName(row.Name) == rowId) return row.Value;
        return null;
    }

    private static PropertyData? Named(IList<PropertyData> data, string name)
    {
        foreach (var p in data)
            if (DataTableEditor.CleanName(p.Name) == name) return p;
        return null;
    }

    private static string? Find(string root, string fileName)
    {
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).FirstOrDefault();
    }

    // ───────────────────── helpers for the CLI ─────────────────────

    /// <summary>Loads the vanilla chest/enemy tables the catalog needs for area metadata.</summary>
    public static LootCatalog CatalogWithChecks(string repoRoot)
    {
        var catalog = new LootCatalog();
        catalog.RegisterExtraPictos(
            PictosCreateKit.PainterConversions.Select(p => (p.Id, p.DisplayName))
                .Concat(PictosCreateKit.Powers.Select(p => (p.Id, p.DisplayName)))
                .Concat(PictosCreateKit.HitBurn.Select(p => (p.Id, p.DisplayName)))
                .Concat(PictosMagnitudeKit.Variants.Select(p => (p.Id, p.DisplayName)))
                .Concat(BombPictosKit.CreatedPictos)
                .Concat(ReviveDebuffKit.CreatedPictos));
        var chestsPath = Path.Combine(repoRoot, "work", "chests.json");
        var enemiesPath = Path.Combine(repoRoot, "work", "enemies.json");
        if (File.Exists(chestsPath) && File.Exists(enemiesPath))
            catalog.Rebuild(
                JObject.Parse(File.ReadAllText(chestsPath)),
                JObject.Parse(File.ReadAllText(enemiesPath)),
                Array.Empty<(string, JObject)>());
        return catalog;
    }
}

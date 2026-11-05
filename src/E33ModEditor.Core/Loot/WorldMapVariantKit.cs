using Newtonsoft.Json.Linq;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;

namespace E33ModEditor.Core.Loot;

/// <summary>
/// World-map encounters reference the same <c>DT_jRPG_Enemies</c> rows as their
/// native biomes (RE_Veilleur, MM_Pelerin, DS_Noire, …). Loot lives on the row,
/// so a T4+ native would otherwise leak its tier into the optional world fight
/// (which is exactly why The Reacher / Frozen Hearts rows were handing out the
/// curated fight tier in their own area). This clones each borrowed T4+ row into
/// a world-only variant pinned to the fight's tier and repoints
/// <c>DT_WorldMap_Encounters</c> at the variant, leaving the native fight's own
/// tier untouched. Most variants pin to T3 (<see cref="WorldLevel"/>); rows in
/// <see cref="T4Variants"/> pin to T4 (<see cref="WorldLevelHigh"/>) because
/// their world fight sits in a higher band.
/// </summary>
public static class WorldMapVariantKit
{
    public const string EncountersTable = "DT_WorldMap_Encounters";
    public const string VariantPrefix = "WMV_";
    /// <summary>Table level for a world variant: <c>TierFromLevel(40)</c> = T3.</summary>
    public const int WorldLevel = 40;
    /// <summary>Table level for a higher-band world variant:
    /// <c>TierFromLevel(60)</c> = T4 (bottom of the band).</summary>
    public const int WorldLevelHigh = 60;

    /// <summary>Native T4+ rows that world-map encounters borrow.</summary>
    public static readonly string[] DoubleDipped =
    {
        "RE_Veilleur", "RE_Echassier", "RE_OrphelinNail_Big", "RE_OrphelinSpoon_Big",
        "RE_OrphelinHammer_Big",
        "MM_Pelerin", "MM_Danseuse", "MM_Stalact", "MM_Braseleur",
        "DS_Noire",
        // Crimson Forest (T4): borrowed by WM_ChevaliereC_Glaise_OrphelinSpoonBig
        // and WM_Noire_ChevaliereC_OrphelinHammerBig.
        "YF_Chevaliere_C",
        // NOTE: boss/chromatic-alpha rows borrowed by world fights (GO_Demineur_ALPHA,
        // SM_Boss_Eveque_Ice_ALPHA, MF_Moissonneuse_ALPHA, …) are handled by
        // BossVenueKit instead, which pins each to the encounter's own level so
        // the venue pays its real band (a fixed world tier would be wrong for the
        // level-50/68/80+ solo fights).
    };

    /// <summary>Borrowed natives whose world-map fight sits in the T4 band, so
    /// their world variant pins to <see cref="WorldLevelHigh"/> instead of
    /// <see cref="WorldLevel"/>.</summary>
    public static readonly HashSet<string> T4Variants = new(StringComparer.OrdinalIgnoreCase);

    public static string VariantOf(string row) => VariantPrefix + row;

    /// <summary>
    /// Stages <c>DT_WorldMap_Encounters</c>, clones the borrowed rows in the
    /// staged <c>DT_jRPG_Enemies</c>, and repoints the encounters. Returns the
    /// number of encounter enemy slots repointed.
    /// </summary>
    public static int Build(string modDir)
    {
        var enemiesPath = PictosCreateKit.FindAsset(modDir, "DT_jRPG_Enemies.uasset");
        if (enemiesPath is null) return 0;
        var encountersPath = PictosCreateKit.FindAsset(modDir, EncountersTable + ".uasset")
            ?? StageEncounters(modDir);
        if (encountersPath is null) return 0;

        var enemies = new DataTableEditor(enemiesPath);
        var encounters = new DataTableEditor(encountersPath);

        // 1. Clone each borrowed native row once, pinned to the world tier. The
        //    clone keeps its donor's actor class / display, so the fight looks
        //    identical; only the loot/stat tier changes. The encounter reference
        //    may differ in case from the DT row (UE names match
        //    case-insensitively, e.g. GO_Demineur_Alpha vs GO_Demineur_ALPHA),
        //    so the lookup is case-insensitive too.
        var variants = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in DoubleDipped.Distinct(StringComparer.Ordinal))
        {
            if (!enemies.HasRow(row))
            {
                Console.Error.WriteLine($"[worldmap-variants] no such enemy row '{row}'; skipped");
                continue;
            }
            var variant = VariantOf(row);
            if (!enemies.HasRow(variant))
                enemies.CloneRow(row, variant);
            var worldLevel = T4Variants.Contains(row) ? WorldLevelHigh : WorldLevel;
            enemies.Apply(new JObject
            {
                [variant] = new JObject
                {
                    ["EnemyHardcodedName"] = variant,
                    ["Level"] = worldLevel,
                },
            }.ToString());
            variants[row] = variant;
        }
        enemies.Save(enemiesPath);

        // 2. Repoint every encounter that borrows one of those rows.
        var edits = new JObject();
        foreach (var row in encounters.Table.Table.Data)
        {
            if (Named(row.Value, "Enemies") is not MapPropertyData map) continue;
            var rowEdits = new JObject();
            int i = 0;
            foreach (var kv in map.Value)
            {
                var key = IntKey(kv.Key, i++);
                if (kv.Value is StructPropertyData sv
                    && Named(sv.Value, "RowName") is NamePropertyData nm
                    && nm.Value?.ToString() is { } id
                    && variants.TryGetValue(id, out var variant))
                    rowEdits[$"Enemies.{key}.RowName"] = variant;
            }
            if (rowEdits.Count > 0)
                edits[DataTableEditor.CleanName(row.Name)] = rowEdits;
        }
        var changed = edits.Count == 0 ? 0 : encounters.Apply(edits.ToString());
        encounters.Save(encountersPath);
        Console.WriteLine(
            $"[worldmap-variants] {variants.Count} variant row(s); repointed {changed} encounter slot(s)");
        return changed;
    }

    private static string? StageEncounters(string modDir)
    {
        var src = FileCopy.FindTablesAsset(EncountersTable + ".uasset");
        if (src is null) return null;
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        var dest = idx >= 0
            ? Path.Combine(modDir, src[idx..])
            : Path.Combine(modDir, Path.GetFileName(src));
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    /// <summary>Map keys are ints rendered as "[i]" (mirrors DataTableEditor.KeyToString).</summary>
    private static string IntKey(PropertyData key, int fallback) => key switch
    {
        IntPropertyData i => $"[{i.Value}]",
        Int64PropertyData i => $"[{i.Value}]",
        UInt32PropertyData u => $"[{u.Value}]",
        _ => $"[{fallback}]",
    };

    private static PropertyData? Named(List<PropertyData> props, string name) =>
        props.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == name);
}

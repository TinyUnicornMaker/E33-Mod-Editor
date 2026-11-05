using Newtonsoft.Json.Linq;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;

namespace E33ModEditor.Core.Loot;

/// <summary>
/// The four endgame (T5) areas reuse low-tier <c>DT_jRPG_Enemies</c> rows.
/// Flying Manor is a gauntlet built almost entirely from borrowed early-game
/// rows (Spring Meadows, Flying Waters, Stone Wave Cliffs, …); Renoir's Drafts
/// borrows a Visages row and Act 3 Lumière borrows Visages / Sirene / The Reacher
/// rows. Loot lives on the enemy row, so a borrowed row keeps dropping its
/// native low-tier loot (and can never roll the rare Perfect Catalyst) even when
/// fought at T5.
///
/// This clones each borrowed row into a T5-only variant (prefix <c>T5_</c>),
/// pins it to <see cref="EndgameLevel"/> and repoints only the endgame
/// encounters at the variant. The native row keeps its own tier in its own area,
/// so the T5 loot cannot leak down into earlier content.
///
/// Same shape as <see cref="WorldMapVariantKit"/>, but for the campaign areas
/// (that kit handles the optional world-map fights).
/// </summary>
public static class EndgameVariantKit
{
    public const string EncountersTable = "DT_jRPG_Encounters";
    public const string EnemiesTable = "DT_jRPG_Enemies";
    public const string VariantPrefix = "T5_";
    /// <summary>Table level for an endgame variant — <c>TierFromLevel(99)</c> = T5.</summary>
    public const int EndgameLevel = 99;

    /// <summary>
    /// Encounters that take place in one of the four endgame areas. Flying Manor
    /// (<c>CFH_</c>) is a gauntlet of borrowed low-tier rows with no native T5
    /// enemy in most fights, so it can only be matched by name; the rest are the
    /// native prefixes of Verso's Drafts (<c>VD_</c>), Renoir's Drafts
    /// (<c>RD_</c>) and Act 3 Lumière (<c>L_</c>/<c>LU_</c> Aberration / Creation /
    /// Chroma / citizens).
    /// </summary>
    public static readonly string[] EndgameEncounterPrefixes =
    {
        "CFH_",
        "VD_",
        "RD_",
        "L_Aberration", "L_Creation", "L_Chroma", "L_LumiereCitizen", "L_Boss_Curator",
        "LU_Aberration", "LU_Echassier",
    };

    private static readonly HashSet<string> BossArchetypes =
        new(StringComparer.OrdinalIgnoreCase) { "Boss", "Alpha", "SuperBoss", "Merchant", "Petank" };

    public static string VariantOf(string row) => VariantPrefix + row;

    public static bool IsEndgameEncounter(string name) =>
        EndgameEncounterPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));

    /// <summary>
    /// Clones every low-tier row borrowed by an endgame encounter into a T5
    /// variant, repoints the encounters and registers the variants in
    /// <paramref name="catalog"/> so the randomizer rolls them as real T5 rows.
    /// Requires the staged <c>DT_jRPG_Enemies</c> in <paramref name="modDir"/>.
    /// Returns the number of encounter slots repointed.
    /// </summary>
    public static int Build(string modDir, LootCatalog catalog)
    {
        var enemiesPath = PictosCreateKit.FindAsset(modDir, EnemiesTable + ".uasset");
        if (enemiesPath is null) return 0;
        var encountersPath = PictosCreateKit.FindAsset(modDir, EncountersTable + ".uasset")
            ?? StageEncounters(modDir);
        if (encountersPath is null) return 0;

        var enemies = new DataTableEditor(enemiesPath);
        var encounters = new DataTableEditor(encountersPath);

        // Actual DT row names (case-insensitive): an encounter may reference a row
        // with different casing than the table (e.g. GO_Demineur_Alpha).
        var rowByCi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in enemies.RowNames) rowByCi.TryAdd(r, r);

        // 1. Discover the borrowed low-tier rows the endgame encounters reference.
        var toClone = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in encounters.Table.Table.Data)
        {
            if (!IsEndgameEncounter(DataTableEditor.CleanName(row.Name))) continue;
            if (Named(row.Value, "Enemies") is not MapPropertyData map) continue;
            foreach (var kv in map.Value)
            {
                if (kv.Value is not StructPropertyData sv) continue;
                if (Named(sv.Value, "RowName") is not NamePropertyData nm) continue;
                var id = nm.Value?.ToString();
                if (string.IsNullOrEmpty(id) || !rowByCi.TryGetValue(id, out var baseRow)) continue;
                if (NeedsT5Variant(baseRow, catalog)) toClone[baseRow] = VariantOf(baseRow);
            }
        }

        if (toClone.Count == 0)
        {
            Console.WriteLine("[endgame-variants] no borrowed low-tier rows in endgame encounters");
            return 0;
        }

        // 2. Clone each borrowed row once, pinned to T5.
        foreach (var (baseRow, variant) in toClone)
        {
            if (!enemies.HasRow(variant))
                enemies.CloneRow(baseRow, variant);
            enemies.Apply(new JObject
            {
                [variant] = new JObject
                {
                    ["EnemyHardcodedName"] = variant,
                    ["Level"] = EndgameLevel,
                },
            }.ToString());
            catalog.RegisterEnemy(variant, new LootCatalog.EnemyInfo
            {
                // Deliberately name-agnostic: a "(Frozen Hearts)" display would
                // re-trigger the biome remap and pin the clone back to T4.
                DisplayName = baseRow,
                Archetype = catalog.Enemies.TryGetValue(baseRow, out var bi) ? bi.Archetype : "Regular",
                Level = EndgameLevel,
                CutContent = false,
            });
        }
        enemies.Save(enemiesPath);

        // 3. Repoint the endgame encounters at the variants.
        var edits = new JObject();
        foreach (var row in encounters.Table.Table.Data)
        {
            var encName = DataTableEditor.CleanName(row.Name);
            if (!IsEndgameEncounter(encName)) continue;
            if (Named(row.Value, "Enemies") is not MapPropertyData map) continue;
            var rowEdits = new JObject();
            int i = 0;
            foreach (var kv in map.Value)
            {
                var key = IntKey(kv.Key, i++);
                if (kv.Value is StructPropertyData sv
                    && Named(sv.Value, "RowName") is NamePropertyData nm
                    && nm.Value?.ToString() is { } id
                    && rowByCi.TryGetValue(id, out var baseRow)
                    && toClone.TryGetValue(baseRow, out var variant))
                    rowEdits[$"Enemies.{key}.RowName"] = variant;
            }
            if (rowEdits.Count > 0)
                edits[encName] = rowEdits;
        }
        var changed = edits.Count == 0 ? 0 : encounters.Apply(edits.ToString());
        encounters.Save(encountersPath);

        Console.WriteLine(
            $"[endgame-variants] {toClone.Count} T5 variant row(s); repointed {changed} endgame encounter slot(s)");
        return changed;
    }

    /// <summary>True when a borrowed row is a real, non-boss, non-cut row whose
    /// native fight level is below the endgame threshold (so it needs a T5 clone).</summary>
    private static bool NeedsT5Variant(string row, LootCatalog catalog)
    {
        if (!catalog.Enemies.TryGetValue(row, out var info)) return false;
        if (info.CutContent) return false;
        if (BossArchetypes.Contains(info.Archetype)) return false;
        return LootCatalog.ResolveEnemyLevel(0, info) < RandomizerKit.PerfectCatalystMinLevel;
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

    private static PropertyData? Named(IList<PropertyData> props, string name) =>
        props.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == name);
}

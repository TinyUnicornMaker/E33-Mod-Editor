using Newtonsoft.Json.Linq;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;

namespace E33ModEditor.Core.Loot;

/// <summary>
/// Loot lives on the enemy row, and a boss/chromatic-alpha row is frequently
/// reused by an optional world-map encounter at a different fight level
/// (e.g. <c>SM_Portier_ALPHA</c> is native T5 but <c>WM_Portier_Alpha</c> is a
/// level-68 fight). Because the reward band is read from the row, the reused
/// boss would pay its native package in a venue of a different band.
///
/// This clones each such boss row into a venue-pinned variant (<c>BV_</c>
/// prefix) at the encounter's own <c>EncounterLevelOverride</c> and repoints
/// that encounter, so the fight pays the band of the area it is actually in.
/// The native row is untouched (its own fight keeps its own band).
///
/// Runs after <see cref="WorldMapVariantKit"/> (which handles the regular rows
/// borrowed by world fights) and <see cref="EndgameVariantKit"/>.
/// </summary>
public static class BossVenueKit
{
    public const string VariantPrefix = "BV_";

    private static readonly string[] EncounterTables =
        { "DT_jRPG_Encounters", "DT_jRPG_Encounters_CleaTower", "DT_WorldMap_Encounters" };

    private static readonly HashSet<string> BossArchetypes =
        new(StringComparer.OrdinalIgnoreCase) { "Boss", "Alpha", "SuperBoss" };

    public static string VariantOf(string row, int level) => $"{VariantPrefix}{row}_{level}";

    /// <summary>
    /// Clones boss rows reused by an override-bearing encounter into a
    /// venue-pinned variant and repoints the encounter. Returns the number of
    /// encounter slots repointed.
    /// </summary>
    public static int Build(string modDir, LootCatalog catalog)
    {
        var enemiesPath = PictosCreateKit.FindAsset(modDir, "DT_jRPG_Enemies.uasset");
        if (enemiesPath is null) return 0;

        var enemies = new DataTableEditor(enemiesPath);
        var rowByCi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in enemies.RowNames) rowByCi.TryAdd(r, r);

        // One variant per (row, venue level).
        var variants = new Dictionary<(string Row, int Level), string>();
        var editsByTable = new Dictionary<string, JObject>(StringComparer.Ordinal);

        foreach (var table in EncounterTables)
        {
            var path = PictosCreateKit.FindAsset(modDir, table + ".uasset") ?? Stage(modDir, table);
            if (path is null) continue;
            var encounters = new DataTableEditor(path);
            var edits = new JObject();
            foreach (var row in encounters.Table.Table.Data)
            {
                var over = Named(row.Value, "EncounterLevelOverride") is IntPropertyData ip ? ip.Value : 0;
                if (over <= 0) continue; // inherit = the row's own (native) band
                if (Named(row.Value, "Enemies") is not MapPropertyData map) continue;
                var rowEdits = new JObject();
                int i = 0;
                foreach (var kv in map.Value)
                {
                    var key = IntKey(kv.Key, i++);
                    if (kv.Value is not StructPropertyData sv) continue;
                    if (Named(sv.Value, "RowName") is not NamePropertyData nm) continue;
                    var id = nm.Value?.ToString();
                    if (string.IsNullOrEmpty(id) || !rowByCi.TryGetValue(id, out var rowName)) continue;
                    // Echassier's level-48 story fight already uses the native row,
                    // whose 900M pin is derived at 48. Cloning it here would lose
                    // the id-keyed pin and the Act 3 loot package; the level pin
                    // in StoryLevelKit is enough to keep the venue at L48.
                    if (rowName.Equals("LU_EchassierAlpha", StringComparison.OrdinalIgnoreCase)
                        && over == 48)
                        continue;
                    if (!IsBoss(rowName, enemies, catalog)) continue;

                    var native = NativeQty(rowName, enemies, catalog);
                    var venue = VenueQty(over);
                    if (native == venue) continue;

                    var vkey = (rowName, over);
                    if (!variants.TryGetValue(vkey, out var variant))
                    {
                        variant = VariantOf(rowName, over);
                        variants[vkey] = variant;
                    }
                    rowEdits[$"Enemies.{key}.RowName"] = variant;
                }
                if (rowEdits.Count > 0)
                    edits[DataTableEditor.CleanName(row.Name)] = rowEdits;
            }

            // Clone the variants into the enemy table, pinned to the venue level.
            foreach (var ((rowName, level), variant) in variants)
            {
                if (enemies.HasRow(variant)) continue;
                enemies.CloneRow(rowName, variant);
                enemies.Apply(new JObject
                {
                    [variant] = new JObject
                    {
                        ["EnemyHardcodedName"] = variant,
                        ["Level"] = level,
                    },
                }.ToString());
                catalog.RegisterEnemy(variant, new LootCatalog.EnemyInfo
                {
                    // Name-agnostic so a "(Frozen Hearts)" / "(World Map)" display
                    // cannot re-trigger a biome remap or the overworld branch.
                    DisplayName = rowName,
                    Archetype = catalog.Enemies.TryGetValue(rowName, out var bi) ? bi.Archetype : "Boss",
                    Level = level,
                    CutContent = false,
                });
            }

            var changed = edits.Count == 0 ? 0 : encounters.Apply(edits.ToString());
            if (changed > 0) encounters.Save(path);
            if (changed > 0)
                Console.WriteLine($"[boss-venues] {table}: repointed {changed} venue slot(s)");
            editsByTable[table] = edits;
        }

        if (variants.Count > 0)
        {
            enemies.Save(enemiesPath);
            Console.WriteLine($"[boss-venues] {variants.Count} venue variant row(s)");
        }
        return editsByTable.Values.Sum(e => e.Count);
    }

    private static bool IsBoss(string row, DataTableEditor enemies, LootCatalog catalog)
    {
        if (catalog.Enemies.TryGetValue(row, out var info) && BossArchetypes.Contains(info.Archetype))
            return true;
        // Fall back to the staged row's own IsBoss flag.
        try
        {
            var r = enemies.Table.Table.Data.FirstOrDefault(x =>
                DataTableEditor.CleanName(x.Name).Equals(row, StringComparison.OrdinalIgnoreCase));
            return r is not null && Named(r.Value, "IsBoss") is BoolPropertyData b && b.Value;
        }
        catch { return false; }
    }

    /// <summary>L5 quantity the boss recipe would pay for the row's native band.</summary>
    private static int NativeQty(string row, DataTableEditor enemies, LootCatalog catalog)
    {
        int level, tier;
        if (catalog.Enemies.TryGetValue(row, out var info))
        {
            level = LootCatalog.ResolveEnemyLevel(RowLevel(enemies, row), info);
            tier = catalog.AreaTierForEnemy(row, info, LootCatalog.TierFromLevel(level), isBossRow: true);
        }
        else
        {
            // Runtime variant (e.g. a WMV_ clone): use the level it was pinned to.
            level = RowLevel(enemies, row);
            tier = LootCatalog.TierFromLevel(level);
        }
        return RecipeQty(tier, level);
    }

    private static int RowLevel(DataTableEditor enemies, string row)
    {
        try
        {
            var r = enemies.Table.Table.Data.FirstOrDefault(x =>
                DataTableEditor.CleanName(x.Name).Equals(row, StringComparison.OrdinalIgnoreCase));
            return r is not null && Named(r.Value, "Level") is IntPropertyData lvl ? lvl.Value : 0;
        }
        catch { return 0; }
    }

    /// <summary>L5 quantity the recipe pays at a venue with the given override level.</summary>
    private static int VenueQty(int level) => RecipeQty(LootCatalog.TierFromLevel(level), level);

    private static int RecipeQty(int tier, int level)
    {
        if (tier >= 5) return level >= 90 ? 21 : 15;
        if (tier == 4) return level >= 70 ? 10 : 5;
        return 0; // T1–T3 pay their own tier, never L5
    }

    private static string? Stage(string modDir, string table)
    {
        var src = FileCopy.FindTablesAsset(table + ".uasset");
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

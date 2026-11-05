using E33ModEditor.Core.Loot;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;

namespace E33ModEditor.Core;

/// <summary>
/// Optional late-pack buff for ordinary (non-boss) enemies. The multipliers are
/// applied to the staged <c>EnemyScaling</c> values, i.e. on top of whatever
/// the vanilla table or the randomizer (MonsterStats) produced.
///
/// Per tier (T1..T5) the defaults are:
///   HP     +30% / +50% / +200% / +500% / +1000%
///   Speed  +20% / +30% / +40% / +50% / +100%
///   Attack +10% / +20% / +50% / +100% / +200%  (Physical and Magical)
///
/// Boss/alpha/super-boss archetypes, real boss rows, merchants and cut content
/// are left untouched. Elites count as regular enemies (they are not bosses).
///
/// ⚠ This kit must run <b>after</b> <see cref="RandomizerKit.Apply"/> in both
/// the GUI pack and <see cref="Packer.FullPackKit"/>: the randomizer writes
/// absolute band factors into EnemyScaling, so running this first would be
/// overwritten.
/// </summary>
public static class RegularEnemyBuffKit
{
    public const string EnemiesTable = "DT_jRPG_Enemies";

    public static readonly double[] DefaultHpBonus =
        { 0.30, 0.50, 2.00, 5.00, 10.00 };
    public static readonly double[] DefaultSpeedBonus =
        { 0.20, 0.30, 0.40, 0.50, 1.00 };
    public static readonly double[] DefaultAttackBonus =
        { 0.10, 0.20, 0.50, 1.00, 2.00 };

    /// <summary>Applies the per-tier buff. Returns the number of scaling fields changed.</summary>
    public static int Apply(string modDir, LootCatalog? catalog,
        double[]? hpBonus = null, double[]? speedBonus = null, double[]? attackBonus = null)
    {
        var hp = Normalize(hpBonus, DefaultHpBonus, nameof(hpBonus));
        var speed = Normalize(speedBonus, DefaultSpeedBonus, nameof(speedBonus));
        var attack = Normalize(attackBonus, DefaultAttackBonus, nameof(attackBonus));

        var path = EnsureEnemiesTable(modDir)
            ?? throw new FileNotFoundException(EnemiesTable + ".uasset not found (donor extract missing?)");

        var ed = new DataTableEditor(path);
        int changed = 0;
        foreach (var row in ed.Table.Table.Data)
        {
            var id = DataTableEditor.CleanName(row.Name);
            LootCatalog.EnemyInfo? enemy = null;
            catalog?.Enemies.TryGetValue(id, out enemy);
            if (!IsRegularRow(row, id, enemy)) continue;

            var tableLevel = Named(row.Value, "Level") is IntPropertyData lvl ? lvl.Value : 0;
            var level = LootCatalog.ResolveEnemyLevel(tableLevel, enemy);
            var rowTier = LootCatalog.TierFromLevel(level);
            // Tier selection mirrors RandomizeMonsterStats: the table's IsBoss
            // flag keeps a row's own tier above its area band (tutorial and
            // Petank rows set it too). We still buff such rows — only real boss
            // rows are skipped above.
            var bossFlag = Named(row.Value, "IsBoss") is BoolPropertyData b && b.Value;
            var tier = catalog?.AreaTierForEnemy(id, enemy, rowTier, bossFlag)
                ?? rowTier;
            tier = Math.Clamp(tier, 1, 5) - 1;

            if (Named(row.Value, "EnemyScaling") is not StructPropertyData scaling) continue;
            foreach (var field in scaling.Value)
            {
                var name = DataTableEditor.CleanName(field.Name);
                var factor = name switch
                {
                    "HP" => 1 + hp[tier],
                    "Speed" => 1 + speed[tier],
                    "PhysicalAttack" or "MagicalAttack" => 1 + attack[tier],
                    _ => 1,
                };
                if (factor == 1) continue;
                if (ScaleNumber(field, factor)) changed++;
            }
        }

        if (changed > 0)
        {
            try { ed.Save(path); }
            catch (FormatException ex)
            {
                throw new FormatException(Path.GetFileName(path) + ": " + ex.Message, ex);
            }
            Console.WriteLine($"[regular-enemy-buff] {changed} EnemyScaling field(s) raised");
        }
        return changed;
    }

    /// <summary>
    /// Regular = not cut content, not a boss archetype and not a real boss row.
    /// The archetype column alone is not enough: the table's IsBoss flag is set
    /// on tutorial fights and Petank lumina enemies too, so
    /// <see cref="RandomizerKit.IsRealBossRow"/> provides the same real-boss
    /// filter the randomizer uses for loot/stat guarantees.
    /// </summary>
    private static bool IsRegularRow(StructPropertyData row, string id,
        LootCatalog.EnemyInfo? enemy)
    {
        if (enemy is { CutContent: true }) return false;
        var arch = enemy?.Archetype ?? "Regular";
        if (arch is "Boss" or "Alpha" or "SuperBoss" or "Merchant") return false;
        if (Named(row.Value, "IsBoss") is BoolPropertyData boss && boss.Value
            && RandomizerKit.IsRealBossRow(id, enemy, arch))
            return false;
        return true;
    }

    private static double[] Normalize(double[]? requested, double[] fallback, string name)
    {
        var values = requested ?? fallback;
        if (values.Length != 5)
            throw new ArgumentException($"{name} must have five entries (one per tier)", name);
        return values;
    }

    private static bool ScaleNumber(PropertyData field, double factor)
    {
        switch (field)
        {
            case FloatPropertyData f:
            {
                var value = (float)(f.Value * factor);
                if (Math.Abs(value - f.Value) < 0.000001) return false;
                f.Value = value;
                return true;
            }
            case DoublePropertyData d:
            {
                var value = d.Value * factor;
                if (Math.Abs(value - d.Value) < 0.0000001) return false;
                d.Value = value;
                return true;
            }
            case IntPropertyData i:
            {
                var value = Math.Max(0, (int)Math.Round(i.Value * factor));
                if (value == i.Value) return false;
                i.Value = value;
                return true;
            }
            default:
                return false;
        }
    }

    private static PropertyData? Named(IList<PropertyData> data, string name) =>
        data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == name);

    private static string? EnsureEnemiesTable(string modDir)
    {
        var existing = GameplayTweaksKit.FindAsset(modDir, EnemiesTable + ".uasset");
        if (existing is not null) return existing;
        var src = FileCopy.FindTablesAsset(EnemiesTable + ".uasset");
        if (src is null) return null;
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return null;
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }
}

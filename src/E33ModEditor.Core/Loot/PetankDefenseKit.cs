using Newtonsoft.Json.Linq;

namespace E33ModEditor.Core.Loot;

/// <summary>
/// Gives the Petank archetype a real defense stat that scales with the actual
/// encounter level.
///
/// A Petank row does not carry a fight level of its own: the same
/// <c>Petank_Blue</c> (etc.) row is used by encounters at several levels, and
/// the runtime picks the level row from the archetype's scaling table
/// (<c>BP_DataAsset_Archetype_Petank</c> →
/// <c>DT_EnemyArchetype_Petank[_Easy|_Hard]</c>). Deriving a defense from a
/// single curated level would therefore be wrong for most fights.
///
/// This pass instead:
///   1. stages neutral 1.0 defense bases on the archetype tables;
///   2. sets every Petank archetype level row's
///      <c>PhysicalDefense</c>/<c>MagicalDefense</c> to that row's attack power;
///   3. sets each Petank-archetype enemy row's defense scaling to its attack
///      scaling, so <c>archetypeDefense(level) × rowDefenseScaling</c> equals
///      <c>archetypeAttack(level) × rowAttackScaling</c> — the effective attack
///      power at whatever level the encounter actually uses.
///
/// Rows that merely have "Petank" in the name but use a different archetype
/// (the Chromatic Alpha and the Regular summons) are deliberately left alone;
/// they keep their own archetype/defense rules.
/// </summary>
public static class PetankDefenseKit
{
    public const string EnemiesTable = "DT_jRPG_Enemies";
    private const string PetankArchetype = "Petank";

    public static int Apply(string modDir)
    {
        // Defense bases must be 1.0 for the archetype rows to be effective.
        // Idempotent when the randomizer already staged them.
        int n = EnemyDefenseKit.StageArchetypeDefense(modDir);

        // 1. Petank archetype: defense at every level = attack at that level.
        foreach (var table in new[]
                 {
                     "DT_EnemyArchetype_Petank.uasset",
                     "DT_EnemyArchetype_Petank_Easy.uasset",
                     "DT_EnemyArchetype_Petank_Hard.uasset",
                 })
        {
            var path = FileCopy.FindUnder(modDir, table);
            if (path is null) continue;

            var ed = new DataTableEditor(path);
            var edits = new JObject();
            int levels = 0;
            foreach (var name in ed.RowNames)
            {
                if (!name.StartsWith("Level_", StringComparison.Ordinal)) continue;
                if (!ed.TryGetRow(name, out var row)) continue;
                var dump = ed.DumpRow(row);
                var attack = Math.Max(
                    dump.Value<double?>("PhysicalAttack") ?? 0,
                    dump.Value<double?>("MagicalAttack") ?? 0);
                if (attack <= 0) continue;
                edits[name] = new JObject
                {
                    ["PhysicalDefense"] = attack,
                    ["MagicalDefense"] = attack,
                };
                levels++;
            }
            if (levels == 0) continue;
            n += ed.Apply(edits.ToString());
            ed.Save(path);
            Console.WriteLine($"[petank-defense] {Path.GetFileNameWithoutExtension(table)}: {levels} level row(s) DEF = ATK");
        }

        // 2. Petank-archetype enemy rows: let the archetype defense through.
        var enemies = FileCopy.FindUnder(modDir, EnemiesTable + ".uasset");
        if (enemies is null)
        {
            var src = FileCopy.FindTablesAsset(EnemiesTable + ".uasset")
                ?? throw new FileNotFoundException($"[petank-defense] {EnemiesTable}.uasset not found");
            var rel = RelativeSandfall(src);
            enemies = Path.Combine(modDir, rel);
            FileCopy.AssetPair(src, enemies);
        }

        var enemyEd = new DataTableEditor(enemies);
        var rowEdits = new JObject();
        foreach (var name in enemyEd.RowNames)
        {
            if (!name.Contains("Petank", StringComparison.OrdinalIgnoreCase)) continue;
            if (!enemyEd.TryGetRow(name, out var row)) continue;

            var dump = enemyEd.DumpRow(row);
            if (EnemyEffectiveStats.NormalizeArchetype(
                    dump.Value<string>("EnemyArchetype") ?? "") != PetankArchetype)
                continue;
            if (dump["EnemyScaling"] is not JObject scaling) continue;

            // Petank archetype attack is physical (MagicalAttack is 0 in every
            // level row), so the physical scaling is the row's attack-power
            // multiplier. Using max(physical, magical) here would inflate the
            // defense by the unused magical scaling (e.g. Yellow 1.47 vs 1.45).
            var attackScale = scaling.Value<double?>("PhysicalAttack") ?? 0;
            if (attackScale <= 0) continue;

            rowEdits[name] = new JObject
            {
                ["EnemyScaling.PhysicalDefense"] = attackScale,
                ["EnemyScaling.MagicalDefense"] = attackScale,
            };
        }
        if (rowEdits.Count > 0)
        {
            n += enemyEd.Apply(rowEdits.ToString());
            enemyEd.Save(enemies);
            Console.WriteLine($"[petank-defense] {rowEdits.Count} Petank row(s) defense scaling = attack scaling");
        }
        return n;
    }

    private static string RelativeSandfall(string path)
    {
        var idx = path.IndexOf("Sandfall" + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        if (idx < 0) idx = path.IndexOf("Sandfall/", StringComparison.Ordinal);
        if (idx < 0) throw new InvalidDataException(path + " is not under Sandfall/");
        return path[idx..];
    }
}

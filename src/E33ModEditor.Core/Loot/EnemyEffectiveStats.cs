using Newtonsoft.Json.Linq;

namespace E33ModEditor.Core.Loot;

/// <summary>
/// Effective in-game enemy stats.
///
/// <c>DT_jRPG_Enemies.EnemyStats</c> is NOT the operative value at runtime for
/// most rows: 154/434 vanilla rows carry placeholder <c>HP 1.0 / ATK 0.0</c>
/// (e.g. Boss_Simon, Visages/Sirene/Lumiere content). The battle code
/// (<c>FL_EnemyScalingHelpers.ComputeCharacterStats</c>) builds each fighter's
/// <c>CharacterBaseStats</c> as
/// <c>ArchetypeDT[EncounterLevel] x EnemyScaling</c> and never reads
/// <c>EnemyStats</c>. The Monsters grid previously showed the raw table, so
/// placeholders displayed as 1 HP / 0 ATK while the fight used thousands or
/// millions (Boss L99 archetype HP x scaling).
///
/// This helper loads the Normal-difficulty archetype tables
/// (<c>DT_EnemyArchetype_Regular|Strong|Weak|Elite|Elusive|Boss|Alpha|Petank</c>)
/// and computes <c>archetype(fightLevel) x scaling</c> for HP, attacks and
/// speed — the stats the archetype system provides. Defence / magic-defence /
/// accuracy / mastery are 0 in every archetype row at every level, so those
/// keep showing the raw table value.
/// </summary>
public static class EnemyEffectiveStats
{
    public const string EffHpPath = "_EffectiveHP";
    public const string EffAttackPath = "_EffectiveAttack";
    public const string EffSpeedPath = "_EffectiveSpeed";

    private static readonly object Gate = new();
    private static Dictionary<string, Dictionary<int, Dictionary<string, double>>>? _cache;

    /// <summary>Base archetype names keyed by any DT/object spelling.</summary>
    public static string NormalizeArchetype(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Regular";
        var s = raw;
        // "DT_EnemyArchetype_Boss_Hard" -> "Boss"; "BP_DataAsset_Archetype_Alpha" -> "Alpha".
        foreach (var prefix in new[] { "DT_EnemyArchetype_", "BP_DataAsset_Archetype_", "BP_DataAsset_" })
            if (s.StartsWith(prefix, StringComparison.Ordinal))
                s = s[prefix.Length..];
        foreach (var suffix in new[] { "_Hard", "_Easy" })
            if (s.EndsWith(suffix, StringComparison.Ordinal))
                s = s[..^suffix.Length];
        // Enemy_data.json uses "SuperBoss" for a few rows; curves match Boss.
        if (s.Equals("SuperBoss", StringComparison.OrdinalIgnoreCase)) return "Boss";
        if (s.Equals("OPBoss", StringComparison.OrdinalIgnoreCase)) return "Boss";
        foreach (var known in new[] { "Regular", "Strong", "Weak", "Elite", "Elusive", "Boss", "Alpha", "Petank" })
            if (s.Equals(known, StringComparison.OrdinalIgnoreCase)) return known;
        return "Regular";
    }

    private static Dictionary<string, Dictionary<int, Dictionary<string, double>>> Curves()
    {
        lock (Gate)
        {
            if (_cache != null) return _cache;
            _cache = Load();
            return _cache;
        }
    }

    /// <summary>
    /// Runtime archetype for a staged row: the <c>EnemyArchetype</c> data asset
    /// wins (the game reads it first), with the DT column only as fallback.
    /// <c>HardOnly_Boss</c> maps to <c>Boss_Hard</c> and
    /// <c>HardOnly_OPBoss</c> to <c>OPBoss_Hard</c>; the normal names map to
    /// their Normal tables.
    /// </summary>
    public static string RuntimeArchetype(JObject row, string? catalogArchetype = null)
    {
        var da = row.Value<string>("EnemyArchetype");
        var dt = row.Value<string>("EnemyArchetypeDT");
        var raw = !string.IsNullOrWhiteSpace(da) && !da.Equals("None", StringComparison.OrdinalIgnoreCase)
            ? da
            : dt;
        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("None", StringComparison.OrdinalIgnoreCase))
            raw = catalogArchetype;
        return RuntimeArchetypeName(raw);
    }

    /// <summary>String form of <see cref="RuntimeArchetype"/> (also used when a
    /// caller passes a DT/BP spelling directly to <see cref="ArchetypeStat"/>).</summary>
    public static string RuntimeArchetypeName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Regular";

        foreach (var prefix in new[] { "DT_EnemyArchetype_", "BP_DataAsset_Archetype_", "BP_DataAsset_" })
            if (raw.StartsWith(prefix, StringComparison.Ordinal))
                raw = raw[prefix.Length..];

        // Strip difficulty suffixes first so OPBoss_Easy/Boss_Easy canonicalise
        // to their base names instead of falling through to Regular.
        if (raw.EndsWith("_Easy", StringComparison.OrdinalIgnoreCase)) raw = raw[..^5];
        if (raw.Equals("HardOnly_Boss", StringComparison.OrdinalIgnoreCase)) return "Boss_Hard";
        if (raw.Equals("HardOnly_OPBoss", StringComparison.OrdinalIgnoreCase)) return "OPBoss_Hard";
        if (raw.Equals("OPBoss", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("SuperBoss", StringComparison.OrdinalIgnoreCase)) return "Boss";
        if (raw.EndsWith("_NoAchievement", StringComparison.OrdinalIgnoreCase))
            raw = raw[..^"_NoAchievement".Length];
        if (raw.EndsWith("_Hard", StringComparison.OrdinalIgnoreCase)) return raw;

        foreach (var known in new[] { "Regular", "Strong", "Weak", "Elite", "Elusive", "Boss", "Alpha", "Petank" })
            if (raw.Equals(known, StringComparison.OrdinalIgnoreCase)) return known;
        return "Regular";
    }

    /// <summary>For tests: drop the cached archetype curves.</summary>
    public static void Reset() { lock (Gate) _cache = null; }

    private static Dictionary<string, Dictionary<int, Dictionary<string, double>>> Load()
    {
        var result = new Dictionary<string, Dictionary<int, Dictionary<string, double>>>(StringComparer.OrdinalIgnoreCase);
        var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "archetypes");
        try
        {
            if (!HasAll(dir))
                new Retoc().ToLegacy(dir, "DT_EnemyArchetype_");
        }
        catch { /* extraction needs game paks + no ~mods shadowing; fall back to EnemyStatCurves */ }
        foreach (var arch in new[] { "Regular", "Strong", "Weak", "Elite", "Elusive", "Boss", "Alpha", "Petank",
                     "Regular_Hard", "Strong_Hard", "Weak_Hard", "Elite_Hard", "Elusive_Hard",
                     "Boss_Hard", "OPBoss_Hard", "Alpha_Hard", "Petank_Hard" })
        {
            var table = $"DT_EnemyArchetype_{arch}";
            var file = FileCopy.FindUnder(dir, table + ".uasset")
                ?? FileCopy.FindTablesAsset(table + ".uasset");
            if (file is null) continue;
            try
            {
                var ed = new DataTableEditor(file);
                var dump = JObject.Parse(ed.Dump());
                var levels = new Dictionary<int, Dictionary<string, double>>();
                foreach (var prop in dump.Properties())
                {
                    var row = prop.Value as JObject;
                    if (row is null) continue;
                    var lvl = row.Value<int?>("Level") ?? 0;
                    if (lvl < 1 || lvl > 99) continue;
                    levels[lvl] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["HP"] = row.Value<double?>("HP") ?? 0,
                        ["PhysicalAttack"] = row.Value<double?>("PhysicalAttack") ?? 0,
                        ["MagicalAttack"] = row.Value<double?>("MagicalAttack") ?? 0,
                        ["Speed"] = row.Value<double?>("Speed") ?? 0,
                        ["CriticalChance"] = row.Value<double?>("CriticalChance") ?? 0,
                        ["Experience"] = row.Value<double?>("Experience") ?? 0,
                        ["Chroma"] = row.Value<double?>("Chroma") ?? 0,
                    };
                }
                if (levels.Count > 0) result[arch] = levels;
            }
            catch { /* one broken table must not block the grid */ }
        }
        if (!result.ContainsKey("Boss_Hard") || !result.ContainsKey("OPBoss_Hard"))
            Console.Error.WriteLine("[enemy-effective] WARNING: Hard/OPBoss archetype tables are unavailable; Hard/HardOnly rows will fall back to the Normal curves.");
        return result;
    }

    private static bool HasAll(string dir)
    {
        foreach (var arch in new[] { "Regular", "Boss", "Alpha", "Boss_Hard", "OPBoss_Hard" })
            if (FileCopy.FindUnder(dir, $"DT_EnemyArchetype_{arch}.uasset") is null
                && FileCopy.FindTablesAsset($"DT_EnemyArchetype_{arch}.uasset") is null)
                return false;
        return true;
    }

    private static double ArchetypeValue(string archetype, int level, string field)
    {
        level = Math.Clamp(level, 1, 99);
        var curves = Curves();
        if (curves.TryGetValue(archetype, out var levels)
            && levels.TryGetValue(level, out var row)
            && row.TryGetValue(field, out var v))
            return v;
        // Fallback to the hardcoded Regular curves (same source tables).
        bool bossAtk = archetype is "Boss" or "Alpha"
            or "Boss_Hard" or "OPBoss_Hard" or "Alpha_Hard";
        return field switch
        {
            "HP" => EnemyStatCurves.Value(level, "HP", bossAtk) * HpMultiple(archetype),
            "PhysicalAttack" or "MagicalAttack" => EnemyStatCurves.Value(level, field, bossAtk),
            "Speed" => EnemyStatCurves.Value(level, "Speed", bossAtk) * SpeedMultiple(archetype),
            "Experience" => EnemyStatCurves.Value(level, "Experience", bossAtk) * XpMultiple(archetype),
            "Chroma" => EnemyStatCurves.Value(level, "Chroma", bossAtk) * ChromaMultiple(archetype),
            _ => 0,
        };
    }

    /// <summary>
    /// Base archetype HP at a level (Normal difficulty): the value the runtime
    /// multiplies by <c>EnemyScaling.HP</c>. Used to convert an absolute HP
    /// target into the scaling a row must carry.
    /// </summary>
    public static double ArchetypeHp(string? archetype, int level) =>
        ArchetypeValue(ResolveArchetypeKey(archetype), level, "HP");

    /// <summary>Any archetype stat value at a level (HP / PhysicalAttack /
    /// MagicalAttack / Speed / …). Used by the absolute stat pins.</summary>
    public static double ArchetypeStat(string? archetype, int level, string field) =>
        ArchetypeValue(ResolveArchetypeKey(archetype), level, field);

    /// <summary>
    /// Keeps canonical runtime keys such as <c>Boss_Hard</c>/<c>OPBoss_Hard</c>
    /// intact (NormalizeArchetype would strip <c>_Hard</c>) while still
    /// accepting DT/BP spellings and the catalog's plain names.
    /// </summary>
    private static string ResolveArchetypeKey(string? archetype)
    {
        if (string.IsNullOrWhiteSpace(archetype)) return "Regular";
        if (Curves().ContainsKey(archetype)) return archetype;
        var runtime = RuntimeArchetypeName(archetype);
        if (Curves().ContainsKey(runtime)) return runtime;
        return NormalizeArchetype(archetype);
    }

    // Vanilla Normal-table level-1 ratios (DT_EnemyArchetype_* L1 vs Regular L1):
    // Boss/Alpha HP 1560/120 = 13, Elite 720/120 = 6, Strong/Petank 240/120 = 2,
    // Weak/Elusive 84/120 = 0.7. Speed Boss/Alpha/Elite 300/200 = 1.5.
    // XP/Chroma: Boss 10x, Alpha 7.5x, Elite 4x, Strong 1.5x, Weak 0.5x, Petank chroma 20x.
    private static double HpMultiple(string arch) => arch switch
    {
        "Boss" or "Alpha" or "Boss_Hard" or "OPBoss_Hard" or "Alpha_Hard" => 13,
        "Elite" or "Elite_Hard" => 6,
        "Strong" or "Petank" or "Strong_Hard" or "Petank_Hard" => 2,
        "Weak" or "Elusive" or "Weak_Hard" or "Elusive_Hard" => 0.7,
        _ => 1,
    };
    private static double SpeedMultiple(string arch) => arch switch
    {
        "Boss" or "Alpha" or "Elite" or "Boss_Hard" or "OPBoss_Hard" or "Alpha_Hard" or "Elite_Hard" => 1.5,
        _ => 1,
    };
    private static double XpMultiple(string arch) => arch switch
    {
        "Boss" or "Boss_Hard" or "OPBoss_Hard" => 10,
        "Alpha" or "Alpha_Hard" => 7.5,
        "Elite" or "Elite_Hard" => 4,
        "Strong" or "Strong_Hard" => 1.5,
        "Weak" or "Weak_Hard" => 0.5,
        _ => 1,
    };
    private static double ChromaMultiple(string arch) => arch switch
    {
        "Petank" or "Petank_Hard" => 20,
        "Boss" or "Boss_Hard" or "OPBoss_Hard" => 10,
        "Alpha" or "Alpha_Hard" => 7.5,
        "Elite" or "Elite_Hard" => 4,
        "Strong" or "Strong_Hard" => 1.5,
        "Weak" or "Weak_Hard" => 0.5,
        _ => 1,
    };

    private static double Scaling(JObject baseline, string field)
    {
        var tok = baseline["EnemyScaling"]?[field];
        if (tok is null) return 1;
        try { return (double)tok; } catch { return 1; }
    }

    private static int TableLevel(JObject baseline) =>
        baseline.Value<int?>("Level") is { } l and > 0 ? l : 1;

    private static string RowArchetype(string rowName, JObject baseline, Func<string, string?>? archetypeByRow) =>
        RuntimeArchetype(baseline, archetypeByRow?.Invoke(rowName));

    /// <summary>
    /// Effective HP/attack/speed for one enemy baseline at its own table level:
    /// <c>archetype(level) x scaling</c>, rounded like the runtime
    /// (<c>Round(archetype x scaling)</c>). Returns false when curves are
    /// unavailable (caller keeps the raw table value).
    /// </summary>
    public static bool TryCompute(
        string rowName, JObject baseline, Func<string, string?>? archetypeByRow,
        out double hp, out double attack, out double speed)
    {
        hp = attack = speed = 0;
        try
        {
            var arch = RowArchetype(rowName, baseline, archetypeByRow);
            var level = LootCatalog.EncounterLevelForEnemy(rowName, TableLevel(baseline));
            hp = Math.Round(ArchetypeValue(arch, level, "HP") * Scaling(baseline, "HP"));
            var patk = Math.Round(ArchetypeValue(arch, level, "PhysicalAttack") * Scaling(baseline, "PhysicalAttack"));
            var matk = Math.Round(ArchetypeValue(arch, level, "MagicalAttack") * Scaling(baseline, "MagicalAttack"));
            // Archetype magic-attack is 0 in every vanilla row, so this is
            // physical in practice; Max keeps it correct if curves gain values.
            attack = Math.Max(patk, matk);
            speed = Math.Round(ArchetypeValue(arch, level, "Speed") * Scaling(baseline, "Speed"));
            return true;
        }
        catch { return false; }
    }

    /// <summary>True when the raw table stats are placeholders (HP 1 + ATK 0).</summary>
    public static bool IsPlaceholder(JObject baseline)
    {
        var stats = baseline["EnemyStats"] as JObject;
        if (stats is null) return false;
        static double Num(JObject o, string k) { try { return (double)(o[k] ?? 0); } catch { return 0; } }
        return Math.Abs(Num(stats, "E_jRPG_StatType::NewEnumerator1") - 1.0) < 0.0001
            && Math.Abs(Num(stats, "E_jRPG_StatType::NewEnumerator3")) < 0.0001;
    }
}

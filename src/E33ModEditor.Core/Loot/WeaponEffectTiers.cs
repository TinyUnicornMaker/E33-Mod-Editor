using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.Core.Loot;

/// <summary>
/// Weapon-passive T1–T5 plus DoNotUse. Stock is seeded from the lowest
/// vanilla weapon item-tier that uses each effect.
/// </summary>
public sealed class WeaponEffectTiers
{
    public static readonly string[] Columns = { "Tier1", "Tier2", "Tier3", "Tier4", "Tier5", "DoNotUse" };

    public Dictionary<string, List<string>> Data { get; private set; } = NewEmpty();

    public static string OverlayPath =>
        Path.Combine(EditorPaths.RepoRoot, "work", "weapon_effect_tiers.json");

    public static string StockSourcePath =>
        Path.Combine(EditorPaths.RepoRoot, "src", "E33ModEditor.Core", "Loot", "Data", "weapon_effect_tiers.json");

    public static WeaponEffectTiers Load()
    {
        var t = new WeaponEffectTiers();
        t.ResetToStock();
        if (File.Exists(OverlayPath))
        {
            try { t.ReplaceFromJson(File.ReadAllText(OverlayPath)); }
            catch { /* keep stock */ }
        }
        return t;
    }

    public void ResetToStock()
    {
        Data = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(ReadStock())
               ?? NewEmpty();
        EnsureColumns();
    }

    public void ReplaceFromJson(string json)
    {
        var parsed = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);
        if (parsed is null) return;
        Data = parsed;
        EnsureColumns();
    }

    public string ToJson()
    {
        EnsureColumns();
        return JsonConvert.SerializeObject(Data, Formatting.Indented);
    }

    public void Save()
    {
        var json = ToJson();
        Directory.CreateDirectory(Path.GetDirectoryName(OverlayPath)!);
        File.WriteAllText(OverlayPath, json);
        try
        {
            var dir = Path.GetDirectoryName(StockSourcePath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                File.WriteAllText(StockSourcePath, json);
        }
        catch { /* packed install */ }
    }

    public List<string> Column(string column)
    {
        if (!Data.TryGetValue(column, out var list))
        {
            list = new List<string>();
            Data[column] = list;
        }
        return list;
    }

    public int? TierOf(string id)
    {
        for (int t = 1; t <= 5; t++)
            if (Column("Tier" + t).Contains(id)) return t;
        return null;
    }

    public void Move(string id, string destColumn)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!Columns.Contains(destColumn) && destColumn != "Unplaced") return;
        foreach (var col in Columns)
            Column(col).RemoveAll(x => x == id);
        if (destColumn != "Unplaced")
            Column(destColumn).Add(id);
    }

    public void MoveAllToDoNotUse()
    {
        var dest = Column("DoNotUse");
        foreach (var col in Columns)
        {
            if (col == "DoNotUse") continue;
            dest.AddRange(Column(col));
            Column(col).Clear();
        }
    }

    public IReadOnlyList<string> Unplaced(IEnumerable<string> knownIds)
    {
        var placed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var col in Columns)
            foreach (var id in Column(col))
                placed.Add(id);
        return knownIds.Where(id => !placed.Contains(id)).Distinct(StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<string> Pool(int tier)
    {
        var t = Math.Clamp(tier, 1, 5);
        return Column("Tier" + t);
    }

    /// <summary>Weighted effect-tier given the weapon's item tier.</summary>
    public static int PickEffectTier(int weaponTier, Random rng)
    {
        var roll = rng.NextDouble();
        return Math.Clamp(weaponTier, 1, 5) switch
        {
            1 => 1,
            2 => roll < 0.70 ? 2 : 1,
            3 => roll < 0.70 ? 3 : roll < 0.90 ? 2 : 1,
            4 => roll < 0.60 ? 4 : roll < 0.80 ? 3 : roll < 0.95 ? 2 : 1,
            _ => roll < 0.60 ? 5 : roll < 0.90 ? 4 : 3,
        };
    }

    /// <summary>
    /// Slots a weapon of <paramref name="weaponTier"/> can hold. The passive
    /// slots unlock at the tier ends 33/66/111/222/333, so each catalyst band
    /// (T1 1–33, T2 34–66, T3 67–111, T4 112–222, T5 223–333) starts just past
    /// the previous tier's unlock and a tier fills exactly <c>tier</c> slots:
    /// 1/2/3/4/5.
    /// </summary>
    public static int SlotCount(int weaponTier) => Math.Clamp(weaponTier, 1, 5);

    /// <summary>
    /// Stain-echo family: vanilla Lightning-on-Fire plus the 6×6 WepFx_Echo matrix.
    /// Two of these on one weapon can explode stain generation.
    /// </summary>
    public static bool IsStainEcho(string? id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (id.StartsWith("WepFx_Echo", StringComparison.OrdinalIgnoreCase)) return true;
        if (id.Equals("Stains_LightningOnFire", StringComparison.OrdinalIgnoreCase)) return true;
        return WeaponEffectsCreateKit.TryGet(id, out var spec)
               && spec.Family == WeaponEffectsCreateKit.FamilyStainEcho;
    }

    /// <summary>Exclusive groups: at most one member per weapon.</summary>
    public static string? ExclusiveGroup(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (IsStainEcho(id)) return "stain-echo";
        // Status finishers: deal bonus damage to Slowed/Powerless/Defenceless
        // and consume the status. One per weapon total (base and Skills
        // variants share the group) — stacking them splits the same payoff.
        if (id.StartsWith("WepFx_SlowConsume", StringComparison.Ordinal)
            || id.StartsWith("WepFx_PowerlessConsume", StringComparison.Ordinal)
            || id.StartsWith("WepFx_DefenselessConsume", StringComparison.Ordinal))
            return "status-consume";
        return null;
    }

    public static bool IsAutoDeath(string? id) =>
        !string.IsNullOrEmpty(id) && id.Equals("AutoDeath", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Character-mechanic effects that do nothing on Gustave. The prefix checks
    /// below cover most families; these are the non-prefixed stragglers —
    /// Lune stains, Sciel sun/moon charges + foretell, Monoco masks, Verso ranks.
    /// Without them the Gustave "generic T1–T3" rule leaks dead passives onto him.
    /// </summary>
    private static readonly string[] SystemEffectIds =
    {
        "PerfectionOnDamage", "DamageOnPerfectionRank",
        "SimonimOvercharge", "InstantReplayOnOverchargedHealSkills",
        "Mask_APCost-1OnCaster", "DoubleFreeAimDamageAlmighty",
        "BaseAttackToHeavy", "ReducedDamageOnHeavy",
        "FourthHitForetell", "SunAPDiscount", "ChargesBalance", "RandomChargeTurnStart",
        "StartD", "PerfectDefenseB",
    };

    public static bool IsCharacterSystemEffect(string? id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (id.StartsWith("Stains_", StringComparison.OrdinalIgnoreCase)) return true;
        if (id.StartsWith("Perfection_", StringComparison.OrdinalIgnoreCase)) return true;
        if (id.StartsWith("Masks_", StringComparison.OrdinalIgnoreCase)) return true;
        if (id.StartsWith("Stance_", StringComparison.OrdinalIgnoreCase)) return true;
        if (id.StartsWith("Foretell_", StringComparison.OrdinalIgnoreCase)) return true;
        if (SystemEffectIds.Contains(id, StringComparer.OrdinalIgnoreCase)) return true;
        return WeaponEffectsCreateKit.TryGet(id, out var spec)
               && !string.IsNullOrEmpty(spec.Character);
    }

    public static bool IsGustave(string? character) =>
        !string.IsNullOrEmpty(character)
        && character.Equals("Gustave", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Character eligibility incl. the Gustave exception (T1–T3 generic only,
    /// never AutoDeath). Mirrors RandomizerKit's historical Permitted/Eligible
    /// checks so the roller, wildcard and sweeps cannot drift apart.
    /// </summary>
    public bool Allows(WeaponEffectCharacters chars, string character, string id)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(character)) return false;
        if (IsGustave(character))
        {
            if (IsAutoDeath(id)) return false;
            if (chars.Allows(id, character)) return true;
            if (IsCharacterSystemEffect(id)) return false;
            return TierOf(id) is >= 1 and <= 3;
        }
        return chars.Allows(id, character);
    }

    /// <summary>
    /// Pick one unused effect: roll a tier, uniform among character-allowed
    /// ids in that tier that do not share an exclusive group already on the
    /// weapon (stain-echo is one-per-weapon — keep re-rolling the mix). If
    /// those attempts miss, walk T5→T1. Gustave: T1–T3 mix only, no AutoDeath,
    /// generic (non-system) T1–T3 effects are eligible even if his chip is off.
    /// <paramref name="allow"/> adds the optional stain element gate.
    /// Null if nothing eligible.
    /// </summary>
    public string? PickEffect(int weaponTier, string character, ISet<string> used,
        WeaponEffectCharacters chars, Random rng, Func<string, bool>? allow = null)
    {
        var blocked = BlockedGroups(used);
        var gustave = IsGustave(character);
        var mixTier = gustave ? 3 : weaponTier;
        for (int i = 0; i < 24; i++)
        {
            var t = PickEffectTier(mixTier, rng);
            if (gustave && t > 3) t = 3;
            var pool = Eligible(t, character, used, chars, blocked, allow);
            if (pool.Count > 0)
                return pool[rng.Next(pool.Count)];
        }
        // Fallback walk: high-to-low, capped at the weapon's own tier so a T4
        // weapon never grabs a T5 effect when the rolled tiers come up empty.
        var walkFrom = gustave ? 3 : Math.Clamp(weaponTier, 1, 5);
        for (int t = walkFrom; t >= 1; t--)
        {
            var pool = Eligible(t, character, used, chars, blocked, allow);
            if (pool.Count > 0)
                return pool[rng.Next(pool.Count)];
        }
        return null;
    }

    private List<string> Eligible(int tier, string character, ISet<string> used,
        WeaponEffectCharacters chars, HashSet<string> blocked, Func<string, bool>? allow)
    {
        return Pool(tier)
            .Where(id =>
            {
                if (used.Contains(id)) return false;
                if (ExclusiveGroup(id) is { } g && blocked.Contains(g)) return false;
                if (!Allows(chars, character, id)) return false;
                if (allow is not null && !allow(id)) return false;
                return true;
            })
            .ToList();
    }

    public static HashSet<string> BlockedGroups(ISet<string> used)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in used)
        {
            var g = ExclusiveGroup(id);
            if (g is not null) set.Add(g);
        }
        return set;
    }

    private void EnsureColumns()
    {
        foreach (var col in Columns)
            if (!Data.ContainsKey(col))
                Data[col] = new List<string>();
    }

    private static Dictionary<string, List<string>> NewEmpty()
    {
        var d = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var col in Columns) d[col] = new List<string>();
        return d;
    }

    private static string ReadStock()
    {
        var asm = typeof(WeaponEffectTiers).Assembly;
        var name = asm.GetManifestResourceNames()
            .First(n => n.EndsWith("weapon_effect_tiers.json", StringComparison.OrdinalIgnoreCase));
        using var s = asm.GetManifestResourceStream(name)!;
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.Core.Loot;

/// <summary>
/// Which playable characters may roll each weapon passive.
/// Stock is seeded from vanilla loadouts plus system-prefix overrides.
/// </summary>
public sealed class WeaponEffectCharacters
{
    public static readonly string[] Roster =
        { "Gustave", "Lune", "Maelle", "Sciel", "Verso", "Monoco" };

    public Dictionary<string, HashSet<string>> Allowed { get; private set; } =
        new(StringComparer.Ordinal);

    public static string OverlayPath =>
        Path.Combine(EditorPaths.RepoRoot, "work", "weapon_effect_characters.json");

    public static string StockSourcePath =>
        Path.Combine(EditorPaths.RepoRoot, "src", "E33ModEditor.Core", "Loot", "Data", "weapon_effect_characters.json");

    public static WeaponEffectCharacters Load()
    {
        var c = new WeaponEffectCharacters();
        c.ResetToStock();
        if (File.Exists(OverlayPath))
        {
            try { c.ReplaceFromJson(File.ReadAllText(OverlayPath)); }
            catch { /* keep stock */ }
        }
        return c;
    }

    public void ResetToStock() => ReplaceFromJson(ReadStock());

    /// <summary>
    /// Gustave: never AutoDeath; every generic (non-system) T1–T3 effect is allowed.
    /// Call after tiers are loaded so Reset-to-stock cannot drop the exception.
    /// </summary>
    public void ApplyGustaveRules(WeaponEffectTiers tiers)
    {
        const string g = "Gustave";
        if (Allowed.TryGetValue("AutoDeath", out var auto))
            auto.Remove(g);
        for (int t = 1; t <= 3; t++)
        {
            foreach (var id in tiers.Pool(t))
            {
                if (WeaponEffectTiers.IsAutoDeath(id)) continue;
                if (WeaponEffectTiers.IsCharacterSystemEffect(id)) continue;
                Ensure(id);
                Allowed[id].Add(g);
            }
        }
    }

    public void ReplaceFromJson(string json)
    {
        var parsed = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json)
                     ?? new Dictionary<string, List<string>>();
        Allowed = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (id, list) in parsed)
            Allowed[id] = new HashSet<string>(list ?? new List<string>(), StringComparer.Ordinal);
    }

    public string ToJson()
    {
        var o = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var id in Allowed.Keys.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            o[id] = Roster.Where(ch => Allows(id, ch)).ToList();
        return JsonConvert.SerializeObject(o, Formatting.Indented);
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

    public bool Allows(string effectId, string character)
    {
        if (string.IsNullOrEmpty(effectId) || string.IsNullOrEmpty(character)) return false;
        if (!Allowed.TryGetValue(effectId, out var set)) return false;
        return set.Contains(character);
    }

    public void Toggle(string effectId, string character)
    {
        if (string.IsNullOrEmpty(effectId) || !Roster.Contains(character)) return;
        if (!Allowed.TryGetValue(effectId, out var set))
            Allowed[effectId] = set = new HashSet<string>(StringComparer.Ordinal);
        if (!set.Add(character))
            set.Remove(character);
    }

    public void Ensure(string effectId, IEnumerable<string>? initial = null)
    {
        if (Allowed.ContainsKey(effectId)) return;
        Allowed[effectId] = initial is null
            ? new HashSet<string>(Roster, StringComparer.Ordinal)
            : new HashSet<string>(initial, StringComparer.Ordinal);
    }

    public IReadOnlyList<string> AllIds() =>
        Allowed.Keys.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Owner from a DT_jRPG_Items row (DA name, then subtype enum).</summary>
    public static string CharacterFromItem(string? pictosData, string? subtype)
    {
        var p = pictosData ?? "";
        if (p.Contains("_Lune_", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("_Lune", StringComparison.OrdinalIgnoreCase)) return "Lune";
        if (p.Contains("_Maelle_", StringComparison.OrdinalIgnoreCase)) return "Maelle";
        if (p.Contains("_Sciel_", StringComparison.OrdinalIgnoreCase)) return "Sciel";
        if (p.Contains("_Monoco_", StringComparison.OrdinalIgnoreCase)) return "Monoco";
        if (p.Contains("_Verso_", StringComparison.OrdinalIgnoreCase)) return "Verso";
        if (p.Contains("_Gustave_", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("_Noah_", StringComparison.OrdinalIgnoreCase)) return "Gustave";
        return subtype switch
        {
            "NewEnumerator0" => "Lune",
            "NewEnumerator1" => "Monoco",
            "NewEnumerator2" => "Sciel",
            "NewEnumerator14" => "Maelle",
            "NewEnumerator16" => "Gustave",
            "NewEnumerator21" => "Verso",
            _ => "",
        };
    }

    private static string ReadStock()
    {
        var asm = typeof(WeaponEffectCharacters).Assembly;
        var name = asm.GetManifestResourceNames()
            .First(n => n.EndsWith("weapon_effect_characters.json", StringComparison.OrdinalIgnoreCase));
        using var s = asm.GetManifestResourceStream(name)!;
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}

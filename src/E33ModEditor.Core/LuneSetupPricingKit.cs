using Newtonsoft.Json.Linq;

namespace E33ModEditor.Core;

/// <summary>
/// Lune setup-burden pricing pass. Scales per-hit <c>BaseDamage</c> (plus
/// <c>FirstHit</c> for the three ramping moves) so each move's overcharged
/// total equals clamp(AP) x burden, where burden = turnMult x apMult and
/// turnMult/apMult follow the tier-top (7/14/21) formula. Overcharge
/// multipliers are deliberately untouched, so every "Nx more damage"
/// tooltip line stays truthful; the matching per-hit numbers in the
/// descriptions are updated alongside the rows.
/// Ramps (FireRage/InfernalRage/SanguinaryFlames) scale BaseDamage +
/// FirstHit by one factor each, preserving the ramp shape; the AoE tax is
/// baked into those factors. Random-spread moves (Thunderfall,
/// CloudSplitter) are priced as single-target. Econ/zone moves, gradients,
/// Mayhem, Chiaroscuro, GravityMill and ElementalTrick are excluded.
/// Runs after <see cref="SkillDescriptionsKit"/> (its output provides the
/// find-fragments below) and after <see cref="SkillOverhaulKit"/> (whose
/// catalog carries these rows at vanilla values), and only when the Lune
/// overhaul is enabled.
/// </summary>
public static class LuneSetupPricingKit
{
    private const string BalTable = "DT_Balancing_MainCharacterSkills";
    private const string StTable = "ST_MainCharacters_Skills";

    internal sealed record RowEdit(string Row, double Value);
    internal sealed record MoveText(string Move, string Find, string Repl);

    // Base = target / (hits x overcharge); overcharge rows unchanged.
    internal static readonly RowEdit[] RowEdits =
    {
        new("Earthquake_BaseDamage", 0.4965),
        new("Electrify_BaseDamage", 0.1167),
        new("Thunderfall_BaseDamage", 0.2063),
        new("Wildfire_BaseDamage", 0.72),
        new("Immolation_BaseDamage", 0.4965),
        new("IceGust_BaseDamage", 0.986),
        new("ShockingWave_BaseDamage", 1.06),
        new("CrustalCrush_BaseDamage", 0.248),
        new("LightningDance_BaseDamage", 0.1767),
        new("LumiereZenithale_BaseDamage", 0.3392),
        new("FrostPiercer_BaseDamage", 1.823),
        new("Combustion_BaseDamage", 4.273),
        new("CripplingTsunami_BaseDamage", 0.6637),
        new("EarthSpike_BaseDamage", 0.4429),
        new("Hell_BaseDamage", 0.5315),
        new("CrushingTsunami_BaseDamage", 1.7177),
        new("CursedEarth_BaseDamage", 2.2575),
        new("LevinDance_BaseDamage", 0.2798),
        new("Brimstone_BaseDamage", 2.0775),
        new("ElementalGenesis_BaseDamage", 0.6125),
        new("ElementalCataclysm_BaseDamage", 2.9688),
        new("Ultima_BaseDamage", 7.889),
        new("UltimaNoir_BaseDamage", 16.032),
        // Ramps: uniform scale of BaseDamage + FirstHit (shape preserved).
        new("FireRage_BaseDamage", 1.0205),
        new("FireRage_FirstHit", 0.1791),
        new("InfernalRage_BaseDamage", 1.0436),
        new("InfernalRage_FirstHit", 0.4718),
        new("SanguinaryFlames_BaseDamage", 4.2247),
        new("SanguinaryFlames_FirstHit", 1.9203),
    };

    // Per-hit numbers in Long/Short descriptions. Fragments include the
    // keyword tag so e.g. 1.00x never matches inside 10.00x. The Sanguinary
    // base find carries the "Deals " prefix so the 2.00x overcharge line
    // (lowercase "deal") is not touched.
    internal static readonly MoveText[] TextEdits =
    {
        new("Earthquake", ">0.69x</", ">0.50x</"),
        new("Electrify", ">0.08x</", ">0.12x</"),
        new("Thunderfall", ">0.19x</", ">0.21x</"),
        new("Wildfire", ">0.66x</", ">0.72x</"),
        new("Immolation", ">0.46x</", ">0.50x</"),
        new("IceGust", ">0.92x</", ">0.99x</"),
        new("ShockingWave", ">0.99x</", ">1.06x</"),
        // LuneSkillsKit bakes the overcharge line as a computed total (0.99x2.0);
        // repoint it at the multiplier row (2.0, unchanged) for the new total.
        new("ShockingWave", ": deal <keyword id=\"DamageMultiplier\">1.98x</> more damage",
            ": deal <keyword id=\"DamageMultiplier\">2.00x</> more damage"),
        new("CrustalCrush", ">0.23x</", ">0.25x</"),
        new("LightningDance", ">0.12x</", ">0.18x</"),
        new("LumiereZenithale", ">0.21x</", ">0.34x</"),
        new("FrostPiercer", ">1.00x</", ">1.82x</"),
        new("Combustion", ">2.99x</", ">4.27x</"),
        new("CripplingTsunami", ">0.50x</", ">0.66x</"),
        new("EarthSpike", ">0.25x</", ">0.44x</"),
        new("Hell", ">0.45x</", ">0.53x</"),
        new("CrushingTsunami", ">1.45x</", ">1.72x</"),
        new("CursedEarth", ">1.93x</", ">2.26x</"),
        new("LevinDance", ">0.17x</", ">0.28x</"),
        new("Brimstone", ">1.65x</", ">2.08x</"),
        new("ElementalGenesis", ">0.45x</", ">0.61x</"),
        new("ElementalCataclysm", ">2.41x</", ">2.97x</"),
        new("Ultima", ">5.34x</", ">7.89x</"),
        new("UltimaNoir", ">6.41x</", ">16.03x</"),
        new("FireRage", ">0.33x</", ">0.36x</"),
        new("FireRage", ">1.98x</", ">2.15x</"),
        new("InfernalRage", "Deals <keyword id=\"DamageMultiplier\">1.32x</>",
            "Deals <keyword id=\"DamageMultiplier\">0.94x</>"),
        new("InfernalRage", ">6.16x</", ">4.40x</"),
        new("SanguinaryFlames", "Deals <keyword id=\"DamageMultiplier\">2.00x</>",
            "Deals <keyword id=\"DamageMultiplier\">3.84x</>"),
        new("SanguinaryFlames", ">9.28x</", ">17.82x</"),
    };

    public static int Apply(string modDir)
    {
        int n = PatchRows(modDir);
        n += PatchTexts(modDir);
        return n;
    }

    private static int PatchRows(string modDir)
    {
        var dest = EnsureModCopy(BalTable, modDir);
        if (dest is null) return 0;
        var rows = new JObject();
        foreach (var r in RowEdits)
            rows[r.Row] = new JObject { ["FloatValue"] = r.Value };
        var ed = new DataTableEditor(dest);
        int n = ed.Apply(rows.ToString(Newtonsoft.Json.Formatting.None));
        ed.Save(dest);
        return n;
    }

    private static int PatchTexts(string modDir)
    {
        var dest = EnsureModCopy(StTable, modDir);
        if (dest is null) return 0;
        var entries = DataTableEditor.OpenStringTable(dest);
        var outEntries = new Dictionary<string, string>();
        foreach (var t in TextEdits)
        {
            foreach (var key in new[] { $"Lune_{t.Move}_Description_Long", $"Lune_{t.Move}_Description_Short" })
            {
                if (!entries.TryGetValue(key, out var text)) continue;
                if (!text.Contains(t.Find, StringComparison.Ordinal)) continue;
                outEntries[key] = text.Replace(t.Find, t.Repl, StringComparison.Ordinal);
                entries[key] = outEntries[key];
            }
        }
        if (outEntries.Count == 0) return 0;
        DataTableEditor.AddStringTableEntries(dest, outEntries);
        return outEntries.Count;
    }

    /// <summary>Checks every row value and tooltip fragment. Prints failures, returns false if any.</summary>
    public static bool Verify(string modDir)
    {
        bool ok = true;
        var balPath = FileCopy.FindUnder(modDir, BalTable + ".uasset");
        if (balPath is null) { Console.WriteLine("[LuneSetupPricing] balancing table missing"); return false; }
        var dump = JObject.Parse(new DataTableEditor(balPath).Dump());
        foreach (var r in RowEdits)
        {
            var got = dump[r.Row]?["FloatValue"]?.ToObject<double>();
            if (got is null || Math.Abs(got.Value - r.Value) > 1e-4)
            {
                Console.WriteLine($"[LuneSetupPricing] FAIL row {r.Row} = {got} (expect {r.Value})");
                ok = false;
            }
        }
        var stPath = FileCopy.FindUnder(modDir, StTable + ".uasset");
        if (stPath is null) { Console.WriteLine("[LuneSetupPricing] string table missing"); return false; }
        var entries = DataTableEditor.OpenStringTable(stPath);
        foreach (var t in TextEdits)
        {
            foreach (var key in new[] { $"Lune_{t.Move}_Description_Long", $"Lune_{t.Move}_Description_Short" })
            {
                if (!entries.TryGetValue(key, out var text))
                {
                    Console.WriteLine($"[LuneSetupPricing] FAIL text {key} missing");
                    ok = false;
                }
                else if (!text.Contains(t.Repl, StringComparison.Ordinal) || text.Contains(t.Find, StringComparison.Ordinal))
                {
                    Console.WriteLine($"[LuneSetupPricing] FAIL text {key} missing '{t.Repl}'");
                    ok = false;
                }
            }
        }
        if (ok) Console.WriteLine("[LuneSetupPricing] verify OK");
        return ok;
    }

    private static string? EnsureModCopy(string stem, string modDir)
    {
        var existing = Directory.EnumerateFiles(modDir, stem + ".uasset", SearchOption.AllDirectories).FirstOrDefault();
        if (existing is not null) return existing;
        var src = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), stem + ".uasset")
            ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables", "skills"), stem + ".uasset");
        if (src is null) return null;
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return null;
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }
}

using Newtonsoft.Json.Linq;

namespace E33ModEditor.Core.Loot;

/// <summary>
/// Enemy defense (the third boss stat): staging + the defense-max curve.
/// Effective defense follows the same formula as every other stat
/// (<c>archetype[fight level] × EnemyScaling</c>, read by
/// <c>BP_BattleDamages.ApplyDefense</c> as A²/(A+2D)), but every vanilla
/// archetype base is 0, so the randomizer's band rolls on the scaling fields
/// are silent no-ops until bases go nonzero. This kit stages neutral 1.0
/// PhysicalDefense/MagicalDefense bases on every archetype table (all
/// difficulties) so all differentiation lives in per-row scaling, exactly
/// where the boss-curve pins write it. Non-curve rows keep vanilla 0.0
/// scaling and therefore keep 0 defense.
/// </summary>
public static class EnemyDefenseKit
{
    /// <summary>Staged archetype defense base: neutral, so scaling == effective.</summary>
    public const double StagedDefenseBase = 1.0;

    /// <summary>Share of MaxBaseAttack anchoring the top of the defense curve.</summary>
    public const double DefenseTopShare = 0.66;

    /// <summary>Pinned top anchor (L333): 0.66 × the confirmed Max Base AP.</summary>
    public const double TopDefense = 57728.0;

    /// <summary>
    /// Curve multiplier on top of the 66% base for the pre-reshape floor
    /// (L61 and below): 10× at the T4 floor, 100× at the L99 top pin. Above
    /// <see cref="CurveStartLevel"/> the reshape is S-shaped and no longer uses
    /// this linear ramp — see <see cref="MaxDefense"/>.
    /// </summary>
    public static double CurveMult(int fightLevel)
    {
        if (fightLevel <= 61) return 10.0;
        if (fightLevel >= 99) return 100.0;
        return 10.0 + 90.0 * (fightLevel - 61) / (99 - 61);
    }

    // --- S-shaped defense reshape -------------------------------------------
    // Waypoints, each a share of the 1× base top pin (before CurveTopStretch):
    //   L60 floor ── fast rise ── L70 = 50% ── gradual climb ── L80 = 60% ── rapid run ── L99 = 100%×stretch
    // Every join is a smoothstep, so the curve leaves and lands flat at each
    // waypoint (accelerate → ease through the middle → accelerate again).
    /// <summary>Level where the reshape leaves the flat floor and starts rising.</summary>
    public const int CurveStartLevel = 60;
    /// <summary>Level the first fast rise lands on (<see cref="CurveMidShare"/> of the base pin).</summary>
    public const int CurveMidLevel = 70;
    /// <summary>Share of the base top pin reached at <see cref="CurveMidLevel"/>.</summary>
    public const double CurveMidShare = 0.50;
    /// <summary>Level the gradual middle climb lands on.</summary>
    public const int CurvePlateauLevel = 80;
    /// <summary>Share of the base top pin reached at <see cref="CurvePlateauLevel"/>.</summary>
    public const double CurvePlateauShare = 0.60;
    /// <summary>Level of the final top pin.</summary>
    public const int CurveTopLevel = 99;

    /// <summary>
    /// Stretch applied to the endgame tail (L80 → L99): the top pin is
    /// multiplied by this, so max defense is <c>CurveTopStretch × BaseTopPin</c>.
    /// The L70/L80 waypoints stay anchored to <see cref="BaseTopPin"/>, so the
    /// 60→80 shape is unchanged and only the final climb is stretched.
    /// </summary>
    public const double CurveTopStretch = 3.0;

    /// <summary>Reference top pin (1×): 100× the 66% anchor = 5,772,800.</summary>
    public static double BaseTopPin => CurveMult(CurveTopLevel) * TopDefense;

    /// <summary>Stretched top pin at L99: <see cref="CurveTopStretch"/> × base.</summary>
    public static double TopDefensePin => CurveTopStretch * BaseTopPin;

    private static double SmoothStep(double t) => t * t * (3 - 2 * t);
    private static double Mix(double a, double b, double t) => a + (b - a) * t;

    /// <summary>Fallback weapon powers if the equipment overlay is unreadable.</summary>
    private static readonly Dictionary<int, double> FallbackPower = new()
    {
        [222] = 3228.0, [250] = 4265.1272411475575, [300] = 7014.563985004483, [333] = 13483.615176944248,
    };

    private static List<double>? _attack;
    private static readonly object Gate = new();

    private static List<double> AttackCurve()
    {
        lock (Gate)
        {
            if (_attack != null) return _attack;
            _attack = new List<double>();
            try
            {
                var path = Path.Combine(EditorPaths.RepoRoot, "work", "weapon_equipment_scaling.json");
                var o = JObject.Parse(File.ReadAllText(path));
                if (o["Attack"] is JArray arr)
                    foreach (var t in arr)
                        _attack.Add(t.ToObject<double>());
            }
            catch { }
            if (_attack.Count < 334)
            {
                _attack = new List<double>(new double[334]);
                var keys = FallbackPower.Keys.OrderBy(k => k).ToList();
                for (int i = 0; i < 334; i++)
                {
                    int lo = keys.LastOrDefault(k => k <= i, keys[0]);
                    int hi = keys.FirstOrDefault(k => k >= i, keys[^1]);
                    double plo = FallbackPower[lo], phi = FallbackPower[hi];
                    _attack[i] = hi == lo ? plo : plo + (phi - plo) * (i - lo) / (hi - lo);
                }
            }
            return _attack;
        }
    }

    /// <summary>Weapon power at a (possibly fractional) weapon level.</summary>
    public static double WeaponPower(double wl)
    {
        var curve = AttackCurve();
        wl = Math.Clamp(wl, 0, curve.Count - 1);
        int lo = (int)Math.Floor(wl), hi = Math.Min(lo + 1, curve.Count - 1);
        return curve[lo] + (curve[hi] - curve[lo]) * (wl - lo);
    }

    /// <summary>
    /// Reference weapon level for a fight level, piecewise-linear through the
    /// anchors (61→222, 70→250, 81→300, 99→333), first segment extended below.
    /// </summary>
    public static double ReferenceWeaponLevel(int fightLevel)
    {
        (int F, int W)[] anchors = { (61, 222), (70, 250), (81, 300), (99, 333) };
        if (fightLevel <= anchors[0].F)
        {
            var slope = (double)(anchors[1].W - anchors[0].W) / (anchors[1].F - anchors[0].F);
            return anchors[0].W + slope * (fightLevel - anchors[0].F);
        }
        for (int i = 0; i < anchors.Length - 1; i++)
        {
            if (fightLevel <= anchors[i + 1].F)
            {
                var (f0, w0) = anchors[i];
                var (f1, w1) = anchors[i + 1];
                return w0 + (double)(w1 - w0) * (fightLevel - f0) / (f1 - f0);
            }
        }
        return anchors[^1].W;
    }

    /// <summary>Extreme endgame defense multiplier at the top of T5.</summary>
    public const double ExtremeTopMultiplier = 30.0;

    /// <summary>
    /// Additional defense multiplier for the alternate T4–T5 boss curve.
    /// It is 1× at the T4 floor and ramps smoothly to 30× at the top of T5.
    /// </summary>
    public static double ExtremeDefenseScale(int fightLevel)
    {
        if (fightLevel <= CurveStartLevel) return 1.0;
        if (fightLevel >= CurveTopLevel) return ExtremeTopMultiplier;
        var t = (fightLevel - CurveStartLevel) / (double)(CurveTopLevel - CurveStartLevel);
        return Mix(1.0, ExtremeTopMultiplier, SmoothStep(t));
    }

    /// <summary>
    /// Defense-max curve. Below <see cref="CurveStartLevel"/> it keeps the old
    /// flat floor (10× × 66% × reference weapon power). From L60 to the L99
    /// top pin the reshape is S-shaped, smoothstepped between three waypoints:
    /// a fast rise to <see cref="CurveMidShare"/> (50%) of the pin at L70, a
    /// gradual climb to <see cref="CurvePlateauShare"/> (60%) at L80, then a
    /// rapid run to the pin at L99. The extreme variant refits this curve on
    /// top of the same T4/T5 anchors.
    /// </summary>
    public static double MaxDefense(int fightLevel, bool extreme = false)
    {
        var regular = MaxDefenseRegular(fightLevel);
        return extreme ? regular * ExtremeDefenseScale(fightLevel) : regular;
    }

    private static double MaxDefenseRegular(int fightLevel)
    {
        if (fightLevel <= CurveStartLevel) return FloorDefense(fightLevel);
        if (fightLevel >= CurveTopLevel) return TopDefensePin;

        double baseTop = BaseTopPin;
        if (fightLevel <= CurveMidLevel)
            return Mix(FloorDefense(CurveStartLevel), CurveMidShare * baseTop,
                SmoothStep((fightLevel - CurveStartLevel) / (double)(CurveMidLevel - CurveStartLevel)));
        if (fightLevel <= CurvePlateauLevel)
            return Mix(CurveMidShare * baseTop, CurvePlateauShare * baseTop,
                SmoothStep((fightLevel - CurveMidLevel) / (double)(CurvePlateauLevel - CurveMidLevel)));
        return Mix(CurvePlateauShare * baseTop, TopDefensePin,
            SmoothStep((fightLevel - CurvePlateauLevel) / (double)(CurveTopLevel - CurvePlateauLevel)));
    }

    /// <summary>Pre-reshape floor: 10× × 66% × reference weapon power (L60 and below).</summary>
    private static double FloorDefense(int fightLevel) =>
        CurveMult(fightLevel) * DefenseTopShare * WeaponPower(ReferenceWeaponLevel(fightLevel));

    /// <summary>
    /// Stages neutral 1.0 PhysicalDefense/MagicalDefense bases on every
    /// DT_EnemyArchetype_* table (all archetypes, all difficulties) so the
    /// per-row curve pins land exactly. Returns the number of values written.
    /// </summary>
    public static int StageArchetypeDefense(string modDir)
    {
        var srcs = new List<string>();
        try
        {
            var root = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "archetypes");
            if (Directory.Exists(root))
                srcs.AddRange(Directory.EnumerateFiles(root, "DT_EnemyArchetype_*.uasset",
                    SearchOption.AllDirectories));
        }
        catch { }
        int n = 0;
        foreach (var src in srcs)
        {
            var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
            var dest = idx >= 0
                ? Path.Combine(modDir, src[idx..])
                : Path.Combine(modDir, Path.GetFileName(src));
            FileCopy.AssetPair(src, dest);
            var ed = new DataTableEditor(dest);
            var edits = new JObject();
            foreach (var name in ed.RowNames)
            {
                if (!name.StartsWith("Level_", StringComparison.Ordinal)) continue;
                edits[name] = new JObject
                {
                    ["PhysicalDefense"] = StagedDefenseBase,
                    ["MagicalDefense"] = StagedDefenseBase,
                };
            }
            n += ed.Apply(edits.ToString());
            ed.Save(dest);
        }
        if (n > 0) Console.WriteLine($"[enemy-defense] {srcs.Count} archetype table(s), {n} defense base(s) set to 1.0");
        return n;
    }
}

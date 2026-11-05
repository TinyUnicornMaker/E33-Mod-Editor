using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace E33ModEditor.Core;

/// <summary>
/// Sciel move kit — the whole Sciel overhaul as one unit, gated by the Skill
/// Overhaul "Character.Sciel" switch. No new moves (all 26 exist); the kit is
/// rebalance + DA costs + two BP patches + descriptions.
///
/// Damage rule: Sciel base-damage total = 0.75 × clamp(AP) — three-quarters of
/// the curve per point. Per-hit row = total ÷ hits (Sealed Fate
/// 5–7 hits → ÷6; Spectral Sweep 2–6 hits → ÷4). Gradient finishers are exempt:
/// Shadow Bringer ×20, Doom ×40, End Slice flat 90 + 5%/foretell-consumed.
///
/// BP patches (staged clones, no statement surgery):
/// - AllSet: TurnDuration IntConst 3 → 1 (buffs last 1 turn).
/// - Doom: Powerless/Defenseless/Slow ObjectConst → Greater variants.
/// - Plentiful Harvest: fixed Physical → Earth (DA enum + MakeDamageBuilder byte).
/// Dark Cleansing has no APCost prop (free utility) — the kit inserts one.
/// Every Sciel DA carries BP_SkillDataComponent_ForetellMechanic (unparsable
/// schema); DA edits go through ApplyToProperties so that export is untouched.
/// </summary>
public static class ScielSkillsKit
{
    public const string BalTable = "DT_Balancing_MainCharacterSkills";
    public const string StringTable = "ST_MainCharacters_Skills";

    /// <summary>AP clamp — same curve as the other kits.</summary>
    public static double Clamp(double ap) => SkillOverhaulKit.Clamp(ap);

    /// <summary>Sciel rule: 0.75 × the curve value.</summary>
    public static double ScielTotal(double ap) => 0.75 * Clamp(ap);

    /// <summary>Per-hit base row for an N-hit move: (0.75×clamp) ÷ hits.</summary>
    public static double PerHit(double ap, int hits) => ScielTotal(ap) / hits;

    private static UAsset Open(string p)
    {
        var map = DataTableEditor.Mappings;
        return map != null ? new UAsset(p, EngineVersion.VER_UE5_4, map) : new UAsset(p, EngineVersion.VER_UE5_4);
    }

    /// <summary>Map a work/tables source path to the same relative path under the mod dir.</summary>
    private static string StageDest(string srcUasset, string modDir)
    {
        var full = srcUasset.Replace('\\', '/');
        const string marker = "/Content/";
        var idx = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) throw new InvalidDataException("not under Content/: " + srcUasset);
        var rel = full[(idx + marker.Length)..];
        return Path.Combine(modDir, "Sandfall", "Content", rel);
    }

    private static string FindTables(string stem)
        => FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), stem + ".uasset")
           ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "extracted"), stem + ".uasset")
           ?? FileCopy.FindTablesAsset(stem + ".uasset")
           ?? throw new FileNotFoundException(stem + ".uasset under work/tables");

    private static void Stage(string src, string modDir, out string dest)
    {
        dest = StageDest(src, modDir);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);
    }

    public static int Apply(string modDir)
    {
        int n = 0;
        n += Rebalance(modDir);
        n += PatchDataAssets(modDir);
        n += PatchBuffBPs(modDir);
        n += PatchNamesAndDescriptions(modDir);
        Console.WriteLine($"[Sciel] staged {n} asset edit(s)");
        return n;
    }

    // ──────────────────────────────────────────────────────────────────────
    // A. Balancing-row rebalance (0.75 × clamp, per-hit)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>A balancing-row write: row name → FloatValue.</summary>
    private sealed record RowEdit(string Row, double Value);

    private static readonly RowEdit[] RebalanceRows =
    {
        // Focused Foretell — 1 AP, 1 hit, Physical. 0.2475. Foretell 2 → 1 (2-if-empty → 3 total).
        new("FocusedForetell_BaseDamage", PerHit(1, 1)),
        new("FocusedForetell_BaseForetell", 1.0),
        new("FocusedForetell_ForetellIfTarfet0", 2.0),
        // Twilight Slash (GrimPrediction) — 2 AP, 1 hit, Dark. 0.495. Per-foretell 25% → 3%.
        new("GrimPrediction_Basedamage", PerHit(2, 1)),
        new("GrimPrediction_DamageIncreasePerForetell", 0.03),
        // Rush (SpeedUp) — duration 3 → 2 turns.
        new("SpeedUp_SpeedDuration", 2.0),
        // Firing Shadow — 3 AP, 3 hits, Dark. 0.7425 ÷ 3 = 0.2475. Eaten-foretell ×3 → ×1.5.
        new("FiringShadow_BaseDamage", PerHit(3, 3)),
        new("FiringShadow_DamageMultiplierPerForetell", 0.5),
        // Plentiful Harvest — 4 AP, 2 hits, now Earth. 0.99 ÷ 2 = 0.495. Earth break 1.3.
        new("PlentifulHarvest_BaseDamage", PerHit(4, 2)),
        new("PlentifulHarvest_BreakBarMultiplier", 1.3),
        // Marking Card (ShadowTargeting) — 4 AP, 2 hits, Dark. 0.99 ÷ 2 = 0.495.
        new("ShadowTargeting_BaseDamage", PerHit(4, 2)),
        // Bad Omen — 5 AP, 2 hits, Dark. 1.2375 ÷ 2 = 0.61875.
        new("BadOmen_BaseDamage", PerHit(5, 2)),
        // Harvest — 3 AP, 1 hit, weapon. 0.7425. No base heal; 10% per foretell.
        new("Harvest_BaseDamage", PerHit(3, 1)),
        new("Harvest_BaseHeal", 0.0),
        new("Harvest_HealPercentPerForetell", 0.10),
        // Delaying Slash (Postponed) — 6 AP, 2 hits, weapon. 1.485 ÷ 2 = 0.7425. Per-foretell 30% → 5%.
        new("Postponed_BaseDamage", PerHit(6, 2)),
        new("Postponed_DamageIncreasePerForetell", 0.05),
        // Searing Bond — 8 AP, 1 hit, Dark. 1.98.
        new("SearingBond_BaseDamage", PerHit(8, 1)),
        // Spectral Sweep — 11 AP, 2–6 hits, weapon. 3.6362 ÷ 4 (avg) = 0.909.
        new("SpectralSweep_BaseDamage", PerHit(11, 4)),
        // Final Path — 13 AP, 1 hit, Dark. 5.8763.
        new("FinalPath_BaseDamage", PerHit(13, 1)),
        // Phantom Blade (ShadowDrop) — 5 AP, 1 hit, Dark. 1.2375. Per-foretell 35% → 5%.
        new("ShadowDrop_BaseDamage", PerHit(5, 1)),
        new("ShadowDrop_DamageIncreasePerForetell", 0.05),
        // Card Weaver — 1 AP, 1 hit, Physical. 0.2475.
        new("CardWeaver_BaseDamage", PerHit(1, 1)),
        // Dark Wave — 7 AP, 3 hits, Dark. 1.7325 ÷ 3 = 0.5775. Per-foretell 25% → 5%.
        new("DarkWave_BaseDamage", PerHit(7, 3)),
        new("DarkWave_DamageIncreasePerForetell", 0.05),
        // Intervention — ally AP gain 4 → 3.
        new("Intervention_APGain", 3.0),
        // Sealed Fate — 8 AP, 5–7 hits, weapon. 1.98 ÷ 6 (avg) = 0.33. Consume-mult ×3 → ×2.
        new("SealedFate_BaseDamage", PerHit(8, 6)),
        new("SealedFate_DamageMultiplierOnForetellConsumed", 2.0),
        // Grim Harvest (DarkAbsorption) — 9 AP, 1 hit, Dark. 2.2275. No base heal (5%/foretell kept).
        new("DarkAbsorption_BaseDamage", PerHit(9, 1)),
        new("DarkAbsorption_BaseHeal", 0.0),
        // Twilight Dance — 11 AP, 4 hits, Dark. 3.6362 ÷ 4 = 0.909. Per-foretell 25% → 10%.
        new("TwilightDance_BaseDamage", PerHit(11, 4)),
        new("TwilightDance_DamageIncreasePerForetell", 0.10),
        // Our Sacrifice — 14 AP, 1 hit, Dark, all enemies. 7.4703. Stolen cap ×4 → ×2; per-foretell 30% → 10%.
        new("OurSacrifice_BaseDamage", PerHit(14, 1)),
        new("OurSacrifice_DamageIncreasePerForetell", 0.10),
        new("OurSacrifice_StolenHPMultiplier", 0.5),
        // Gradient finishers (exempt from the curve; user values are TOTALS).
        // Shadow Bringer — 2 charges, 10 hits. 20 total = 2.0 per hit.
        new("ShadowBringer_BaseDamage", 2.0),
        // Doom — 4 charges, 3 hits. 40 total = 40/3 per hit.
        new("Doom_BaseDamage", 40.0 / 3),
        // End Slice — 9 charges, 1 hit. Flat 90; per-foretell-consumed 20% → 5%.
        new("EndSlice_BaseDamage", 90.0),
        new("EndSlice_DamageIncreasePerForetell", 0.05),
    };

    private static int Rebalance(string modDir)
    {
        var balSrc = FindTables(BalTable);
        Stage(balSrc, modDir, out var balDest);

        var rows = new JObject();
        foreach (var e in RebalanceRows)
            rows[e.Row] = new JObject { ["FloatValue"] = e.Value };
        var ed = new DataTableEditor(balDest);
        int n = ed.Apply(rows.ToString(Newtonsoft.Json.Formatting.None));
        ed.Save(balDest);
        Console.WriteLine($"[Sciel] rebalanced {RebalanceRows.Length} balancing row(s)");
        return n;
    }

    // ──────────────────────────────────────────────────────────────────────
    // B. DA property edits (AP / charge costs) + Plentiful Harvest Earth retheme
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>DA APCost edits (gradient SkillType moves read APCost as charges).</summary>
    private static readonly (string DaStem, string ApCost)[] DaEdits =
    {
        ("DA_Skill_Sciel_new2_Foretelling2", "1"),   // Focused Foretell 2 → 1
        ("DA_Skill_Sciel_DarkCleansing", "2"),       // Dark Cleansing free → 2 (prop inserted)
        ("DA_Skill_Sciel_SpeedUp", "2"),             // Rush 3 → 2
        ("DA_Skill_Sciel_AllSet", "4"),              // All Set 6 → 4
        ("DA_Skill_Sciel_new14_ShadowTargeting", "4"), // Marking Card 3 → 4
        ("DA_Skill_Sciel_BadOmen", "5"),             // Bad Omen 3 → 5
        ("DA_Skill_Sciel_new18_Postponed", "6"),     // Delaying Slash 5 → 6
        ("DA_Skill_Sciel_FortunesFury", "9"),        // Fortune's Fury 5 → 9
        ("DA_Skill_Sciel_SearingBond", "8"),         // Searing Bond 4 → 8
        ("DA_Skill_Sciel_SpectralSweep", "11"),      // Spectral Sweep 7 → 11
        ("DA_Skill_Sciel_FinalPath", "13"),          // Final Path 9 → 13
        ("DA_Skill_Sciel_Intervention", "3"),        // Intervention 5 → 3
        ("DA_Skill_Sciel_SealedFate", "8"),          // Sealed Fate 4 → 8
        ("DA_Skill_Sciel_new7_DarkAbsorption", "9"), // Grim Harvest 5 → 9
        ("DA_Skill_Sciel_TwilightDance", "11"),      // Twilight Dance 9 → 11
        ("DA_Skill_Sciel_OurSacrifice", "14"),       // Our Sacrifice 4 → 14
        ("DA_Skill_Sciel_CardWeaver", "1"),          // Card Weaver 3 → 1
        ("DA_Skill_Sciel_DarkWave", "7"),            // Dark Wave 6 → 7
        ("DA_Skill_Sciel_ShadowBringer", "2"),       // Shadow Bringer 1 → 2 charges
        ("DA_Skill_Sciel_Doom", "4"),                // Doom 2 → 4 charges
        ("DA_Skill_Sciel_EndSlice", "9"),            // End Slice 3 → 9 charges
    };

    private static int PatchDataAssets(string modDir)
    {
        int n = 0;
        foreach (var (stem, apCost) in DaEdits)
        {
            var src = FindTables(stem);
            Stage(src, modDir, out var dest);
            var ed = DataTableEditor.OpenPropertyAsset(dest);
            try
            {
                n += ed.ApplyToProperties($"{{\"APCost\":{apCost}}}");
            }
            catch (KeyNotFoundException)
            {
                // Dark Cleansing ships with no APCost prop (free utility) — insert one.
                n += InsertDaInt(dest, "APCost", int.Parse(apCost));
                ed = DataTableEditor.OpenPropertyAsset(dest);
            }
            ed.Save(dest);
            Console.WriteLine($"[Sciel] {stem} APCost -> {apCost}");
        }
        n += RethemeDaElement("DA_Skill_Sciel_PlentifulHarvest", "Earth", modDir);
        n += RethemeBpElement("BP_Battle_SkillScript_Sciel_New_PlentifulHarvest", "Earth", modDir);
        return n;
    }

    /// <summary>Inserts a missing int prop into a skill DA (Dark Cleansing has no
    /// APCost). Shared helper on <see cref="DataTableEditor"/>.</summary>
    private static int InsertDaInt(string dest, string propName, int value)
    {
        DataTableEditor.EnsureIntProperty(dest, propName, value);
        Console.WriteLine($"[Sciel] {Path.GetFileNameWithoutExtension(dest)} {propName} inserted -> {value}");
        return 1;
    }

    /// <summary>Sets a skill DA's BaseElementalType enum (copies the Maelle pattern).
    /// Plentiful Harvest ships with no BaseElementalType (Physical default), so the
    /// enum prop is inserted after TargetingType using Doom's prop as a template.</summary>
    private static int RethemeDaElement(string daStem, string element, string modDir)
    {
        var src = FindTables(daStem);
        Stage(src, modDir, out var dest);
        var map = DataTableEditor.Mappings;
        var da = map != null ? new UAsset(dest, EngineVersion.VER_UE5_4, map) : new UAsset(dest, EngineVersion.VER_UE5_4);
        var en = FName.FromString(da, "EAttackType::" + StainsMechanicKit.Enumerator(element));
        int n = 0;
        foreach (var ex in da.Exports.OfType<NormalExport>())
            foreach (var p in ex.Data)
            {
                if (p.Name.ToString() == "BaseElementalType" && p is EnumPropertyData bpe && !bpe.Value.Equals(en))
                {
                    bpe.Value = en;
                    n++;
                }
            }
        if (n == 0)
        {
            // No BaseElementalType prop (Physical default) — insert one.
            var tplDa = Open(FindTables("DA_Skill_Sciel_Doom"));
            var tpl = tplDa.Exports.OfType<NormalExport>()
                .SelectMany(e => e.Data)
                .OfType<EnumPropertyData>()
                .First(p => p.Name.ToString() == "BaseElementalType");
            var ex = da.Exports.OfType<NormalExport>()
                .First(e => e.Data.Any(p => p.Name.ToString() == "TargetingType"));
            var prop = new EnumPropertyData(FName.FromString(da, "BaseElementalType"))
            {
                EnumType = tpl.EnumType,
                InnerType = tpl.InnerType,
                Value = en,
                PropertyTypeName = tpl.PropertyTypeName,
            };
            int idx = ex.Data.FindIndex(p => p.Name.ToString() == "TargetingType");
            ex.Data.Insert(idx + 1, prop);
            n = 1;
        }
        da.Write(dest);
        Console.WriteLine($"[Sciel] {daStem} BaseElementalType -> {element}");
        return n;
    }

    /// <summary>Retargets a skill-script BP's MakeDamageBuilder element byte
    /// (Maelle pattern: first ByteConst param is the element slot).</summary>
    private static int RethemeBpElement(string bpStem, string element, string modDir)
    {
        var src = FindTables(bpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        int ordinal = StainsMechanicKit.Ordinal(element);
        int n = 0;
        foreach (var fn in bp.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            bool changed = false;
            foreach (var s in fn.ScriptBytecode)
            {
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (changed || e is not EX_VirtualFunction vf || vf.VirtualFunctionName.ToString() != "MakeDamageBuilder") return;
                    foreach (var p in vf.Parameters)
                        if (p is EX_ByteConst bc) // first ByteConst = the element slot
                        {
                            bc.Value = (byte)ordinal;
                            changed = true;
                            return;
                        }
                });
            }
            if (changed)
            {
                MayhemFixKit.RecalcContextSkips(bp, fn);
                n++;
            }
        }
        if (n > 0) bp.Write(dest);
        Console.WriteLine($"[Sciel] {bpStem} MakeDamageBuilder element -> {element} ({n} fn(s))");
        return n;
    }

    // ──────────────────────────────────────────────────────────────────────
    // C. Skill-script BP patches (staged clones, same-size edits only)
    // ──────────────────────────────────────────────────────────────────────

    private static int PatchBuffBPs(string modDir)
    {
        int n = 0;
        n += PatchAllSetDuration(modDir);
        n += PatchDoomGreater(modDir);
        return n;
    }

    /// <summary>All Set: buff TurnDuration IntConst 3 → 1 (buffs last 1 turn).
    /// Only patches IntConsts sharing a statement with NameConst TurnDuration.</summary>
    private static int PatchAllSetDuration(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_Sciel_AllSet";
        var src = FindTables(bpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        var fn = bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (fn?.ScriptBytecode is null) return 0;
        int n = 0;
        foreach (var s in fn.ScriptBytecode)
        {
            bool hasDuration = false;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_NameConst nc && nc.Value.ToString() == "TurnDuration") hasDuration = true;
            });
            if (!hasDuration) continue;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_IntConst ic && ic.Value == 3) { ic.Value = 1; n++; }
            });
        }
        if (n > 0) bp.Write(dest);
        Console.WriteLine($"[Sciel] {bpStem} TurnDuration 3 -> 1 ({n} site(s))");
        return n > 0 ? 1 : 0;
    }

    /// <summary>Doom: swap Powerless/Defenseless/Slow buff classes for their
    /// Greater variants (same-size ObjectConst import swap, no statement surgery).</summary>
    private static int PatchDoomGreater(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_Sciel_Doom";
        var src = FindTables(bpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        var fn = bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (fn?.ScriptBytecode is null) return 0;
        var swaps = new Dictionary<string, (string Cls, string Pkg)>(StringComparer.Ordinal)
        {
            ["BP_BattleBuff_Powerless_C"] = ("BP_BattleBuff_GreaterPowerless_C", "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_GreaterPowerless"),
            ["BP_BattleBuff_Defenseless_C"] = ("BP_BattleBuff_GreaterDefenseless_C", "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_GreaterDefenseless"),
            ["BP_BattleBuff_Slow_C"] = ("BP_BattleBuff_GreaterSlow_C", "/Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_GreaterSlow"),
        };
        int n = 0;
        foreach (var s in fn.ScriptBytecode)
        {
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is not EX_ObjectConst oc || !oc.Value.IsImport()) return;
                var name = oc.Value.ToImport(bp).ObjectName.ToString();
                if (!swaps.TryGetValue(name, out var cf)) return;
                oc.Value = LuneSkillsKit.EnsureObjectImport(bp, cf.Pkg, cf.Cls, cf.Cls, "/Script/Engine");
                n++;
            });
        }
        if (n > 0) bp.Write(dest);
        Console.WriteLine($"[Sciel] {bpStem} buffs -> Greater variants ({n} swap(s))");
        return n > 0 ? 1 : 0;
    }

    // ──────────────────────────────────────────────────────────────────────
    // D. Names + descriptions (vanilla voice, per-hit gold numbers)
    // ──────────────────────────────────────────────────────────────────────

    private static string Gold(double v) => SkillDescriptionsKit.Gold(v);
    private const string Dark = "<keyword id=\"Element_Dark\">Dark</>";
    private const string Physical = "<keyword id=\"Element_Physical\">Physical</>";
    private const string Earth = "<keyword id=\"Element_Earth\">Earth</>";
    private const string Foretell = "<keyword id=\"Foretell\">Foretell</>";
    private const string AP = "<keyword id=\"APShard\">AP</>";
    private const string Heal = "<keyword id=\"Heal\">Heals</>";
    private const string Break = "<keyword id=\"Break\">Break</>";
    private const string Mark = "<keyword id=\"StatusEffect_Mark\">Mark</>";
    private const string Burn = "<keyword id=\"StatusEffect_Burn\">Burning</>";
    private const string Twilight = "<keyword id=\"Foretell_Twilight\">Twilight</>";
    private const string Crit = "<keyword id=\"CriticalHit\">Critical Hits</>";

    /// <summary>String-table base key → (display name, Long, Short).</summary>
    private static readonly (string Key, string Name, string Long, string Short)[] StringEdits =
    {
        ("Sciel_FocusedForetell", "Focused Foretell",
            $"Deals {Gold(PerHit(1, 1))} single target {Physical} damage. 1 hit.\nApplies 1 {Foretell}.\nApplies 2 additional {Foretell} if the target has 0 {Foretell} (3 total).",
            $"{Gold(PerHit(1, 1))} {Physical} damage. 1 hit.\nApplies 1 {Foretell}. If no {Foretell}, applies 3."),
        ("Sciel_GrimPrediction", "Twilight Slash",
            $"Deals {Gold(PerHit(2, 1))} single target {Dark} damage. 1 hit.\nConsumes all {Foretell} to deal 3% more damage per {Foretell}.",
            $"{Gold(PerHit(2, 1))} {Dark} damage. 1 hit.\nConsumes {Foretell} to deal 3% more damage each."),
        ("Sciel_DarkCleansing", "Dark Cleansing",
            $"Cleanses an ally of their Status Effects and propagates the target's buff to all allies.",
            $"Cleanses an ally of their Status Effects and propagates the target's buff to all allies."),
        ("Sciel_SpeedUp", "Rush",
            $"Applies <keyword id=\"Buff_Rush\">Rush</> to 1-5 allies, increasing their speed for 2 turns.",
            $"Applies <keyword id=\"Buff_Rush\">Rush</> to 1-5 allies, increasing their speed for 2 turns."),
        ("Sciel_FiringShadow", "Firing Shadow",
            $"Deals {Gold(PerHit(3, 3))} {Dark} damage to all enemies. 3 hits.\nConsumes 1 {Foretell} per hit to deal 50% more damage on that hit.",
            $"{Gold(PerHit(3, 3))} {Dark} damage to all enemies. 3 hits.\nConsumes 1 {Foretell} per hit to deal 50% more damage."),
        ("Sciel_PlentifulHarvest", "Plentiful Harvest",
            $"Deals {Gold(PerHit(4, 2))} single target {Earth} damage. 2 hits.\nConsumes all {Foretell} on a target and gives 1 {AP} to a party member for each {Foretell} consumed.",
            $"{Gold(PerHit(4, 2))} {Earth} damage. 2 hits.\nConsumes all {Foretell} to give {AP} to party members."),
        ("Sciel_AllSet", "All Set",
            $"Applies <keyword id=\"Buff_Shell\">Shell</>, <keyword id=\"Buff_Powerful\">Powerful</>, and <keyword id=\"Buff_Rush\">Rush</> to all allies for 1 turn.",
            $"Applies <keyword id=\"Buff_Shell\">Shell</>, <keyword id=\"Buff_Powerful\">Powerful</>, and <keyword id=\"Buff_Rush\">Rush</> to all allies for 1 turn."),
        ("Sciel_ShadowTargeting", "Marking Card",
            $"Deals {Gold(PerHit(4, 2))} single target {Dark} damage. 2 hits.\nApplies {Mark} to the target.\nApplies 3 {Foretell}.",
            $"{Gold(PerHit(4, 2))} {Dark} damage. 2 hits.\nApplies 3 {Foretell} and {Mark}."),
        ("Sciel_BadOmen", "Bad Omen",
            $"Deals {Gold(PerHit(5, 2))} {Dark} damage to all enemies. 2 hits.\nApplies 2 {Foretell} per hit.",
            $"{Gold(PerHit(5, 2))} {Dark} damage to all enemies. 2 hits.\nApplies 2 {Foretell} per hit."),
        ("Sciel_Harvest", "Harvest",
            $"Deals {Gold(PerHit(3, 1))} single target damage. 1 hit.\nUses weapon's element.\nConsumes all {Foretell} to {Heal} self for 10% Health per {Foretell} consumed.",
            $"{{DynamicElement}} damage. 1 hit.\nConsumes all {Foretell} to heal self 10% per {Foretell} consumed."),
        ("Sciel_Postponed", "Delaying Slash",
            $"Deals {Gold(PerHit(6, 2))} single target damage. 2 hits.\nUses weapon's element.\nConsumes {Foretell} to deal 5% more damage per {Foretell} and delay the target's turn.",
            $"{{DynamicElement}} damage. 2 hits.\nConsumes {Foretell} to deal 5% more damage each and delays the target."),
        ("Sciel_FortuneSFury", "Fortune's Fury",
            $"Targeted ally deals double damage for 1 turn.",
            $"Targeted ally deals double damage for 1 turn."),
        ("Sciel_SearingBond", "Searing Bond",
            $"Deals {Gold(PerHit(8, 1))} single target {Dark} damage. 1 hit.\nApplies 5 {Foretell}.\nAlso deals damage and applies {Foretell} to every other {Burn} enemy.",
            $"{Gold(PerHit(8, 1))} {Dark} damage. 1 hit.\nApplies damage and 5 {Foretell} to the target and all {Burn} enemies."),
        ("Sciel_SpectralSweep", "Spectral Sweep",
            $"Deals {Gold(PerHit(11, 4))} single target damage. 2 to 6 hits.\nUses weapon's element.\nApplies 1 {Foretell} per hit.\n{Crit} apply an additional {Foretell}.",
            $"{{DynamicElement}} damage. 2 to 6 hits.\nApplies 1 {Foretell} per hit.\n{Crit} apply an additional {Foretell}."),
        ("Sciel_FinalPath", "Final Path",
            $"Deals {Gold(PerHit(13, 1))} single target {Dark} damage and applies 10 {Foretell}. 1 hit.\nCan {Break}.",
            $"{Gold(PerHit(13, 1))} {Dark} damage and applies 10 {Foretell}. 1 hit.\nCan {Break}."),
        ("Sciel_ShadowDrop", "Phantom Blade",
            $"Deals {Gold(PerHit(5, 1))} single target {Dark} damage. 1 hit.\nConsumes all {Foretell} to deal 5% more damage per {Foretell}.\nCan {Break}.",
            $"{Gold(PerHit(5, 1))} {Dark} damage. 1 hit.\nConsumes {Foretell} to deal 5% more damage each.\nCan {Break}."),
        ("Sciel_CardWeaver", "Card Weaver",
            $"Deals {Gold(PerHit(1, 1))} single target {Physical} damage. 1 hit.\nPropagates the target's {Foretell} to all enemies.\nPlays a second turn.",
            $"{Gold(PerHit(1, 1))} {Physical} damage.\nPropagates the target's {Foretell} to all enemies.\nPlays a second turn."),
        ("Sciel_DarkWave", "Dark Wave",
            $"Deals {Gold(PerHit(7, 3))} {Dark} damage to all enemies. 3 hits.\nConsumes all {Foretell} to deal 5% more damage per {Foretell}.",
            $"{Gold(PerHit(7, 3))} {Dark} damage to all enemies. 3 hits.\nConsumes all {Foretell} to deal 5% more damage each."),
        ("Sciel_Intervention", "Intervention",
            $"Targeted ally plays immediately and gains 3 {AP}.",
            $"Targeted ally plays immediately and gains 3 {AP}."),
        ("Sciel_SealedFate", "Sealed Fate",
            $"Deals {Gold(PerHit(8, 6))} single target damage. 5-7 hits.\nUses weapon's element.\nEach hit can consume 1 {Foretell} to deal 100% more damage.\n{Crit} don't remove the {Foretell} but still gets the damage increase.",
            $"{{DynamicElement}} damage. 5-7 hits.\nConsumes {Foretell} to deal 100% more damage.\n{Crit} don't remove {Foretell}."),
        ("Sciel_DarkAbsorption", "Grim Harvest",
            $"Deals {Gold(PerHit(9, 1))} single target {Dark} damage. 1 hit.\nConsumes {Foretell} to {Heal} all allies by 5% Health per {Foretell} consumed.",
            $"{Gold(PerHit(9, 1))} {Dark} damage. 1 hit.\nConsumes {Foretell} to heal all allies 5% per {Foretell} consumed."),
        ("Sciel_TwilightDance", "Twilight Dance",
            $"Deals {Gold(PerHit(11, 4))} single target {Dark} damage. 4 hits.\nDuring {Twilight}, extends {Twilight} duration by 1 turn.\nConsumes all {Foretell} to deal 10% more damage per {Foretell}.",
            $"{Gold(PerHit(11, 4))} {Dark} damage. 4 hits.\nDuring {Twilight}, extends {Twilight} duration by 1 turn.\nConsumes all {Foretell} to deal 10% more damage each."),
        ("Sciel_OurSacrifice", "Our Sacrifice",
            $"Deals {Gold(PerHit(14, 1))} {Dark} damage to all enemies. 1 hit.\nAbsorbs allies' Health and enemies' {Foretell} to deal more damage (up to double damage, +10% per {Foretell}).",
            $"{Gold(PerHit(14, 1))} {Dark} damage to all enemies. 1 hit.\nAbsorbs allies' Health and {Foretell} to deal more damage (max double, +10% each)."),
        ("Sciel_ShadowBringer", "Shadow Bringer",
            $"Deals {Gold(2.0)} single target {Dark} damage to random enemies. 10 hits.\nApplies 1 {Foretell} per hit.",
            $"{Gold(2.0)} {Dark} damage to random enemies. 10 hits.\nApplies 1 {Foretell} per hit."),
        ("Sciel_Doom", "Doom",
            $"Deals {Gold(40.0 / 3)} single target {Dark} damage. 3 hits.\nApplies <keyword id=\"Buff_Powerless\">Greater Powerless</>, <keyword id=\"Buff_Defenseless\">Greater Defenceless</>, and <keyword id=\"Buff_Slow\">Greater Slow</> for 3 turns.\nCan {Break}.",
            $"{Gold(40.0 / 3)} {Dark} damage. 3 hits.\nApplies Greater Powerless, Greater Defenceless and Greater Slow for 3 turns.\nCan {Break}."),
        ("Sciel_EndSlice", "End Slice",
            $"Deals {Gold(90.0)} single target {Physical} damage. 1 hit.\nDeal 5% more damage for each {Foretell} consumed since the beginning of the battle.",
            $"{Gold(90.0)} {Physical} damage. 1 hit.\nDeal 5% more damage for each {Foretell} consumed this battle."),
    };

    /// <summary>Editor preview: the modded Long description for one Sciel
    /// string-table base key (e.g. "Sciel_SealedFate"). Returns false when no
    /// kit entry rewrites that key (caller falls back to vanilla text).</summary>
    public static bool TryPreviewDescription(string baseKey, out string longText)
    {
        foreach (var (key, _, longT, _) in StringEdits)
        {
            if (!string.Equals(key, baseKey, StringComparison.Ordinal)) continue;
            longText = longT;
            return true;
        }
        longText = "";
        return false;
    }

    private static int PatchNamesAndDescriptions(string modDir)
    {
        var stSrc = FindTables(StringTable);
        Stage(stSrc, modDir, out var stDest);
        var entries = new Dictionary<string, string>();
        foreach (var (key, name, longText, shortText) in StringEdits)
        {
            entries[key + "_Name"] = name;
            entries[key + "_Description_Long"] = longText;
            entries[key + "_Description_Short"] = shortText;
        }
        DataTableEditor.AddStringTableEntries(stDest, entries);
        Console.WriteLine($"[Sciel] staged {entries.Count} string-table entries");
        return entries.Count;
    }
}

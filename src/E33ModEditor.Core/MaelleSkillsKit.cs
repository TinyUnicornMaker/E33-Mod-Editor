using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace E33ModEditor.Core;

/// <summary>
/// Maelle move kit — the whole Maelle overhaul as one unit, gated by the Skill
/// Overhaul "Character.Maelle" switch (legacy work/maelle_overhaul.json is inert). Three elemental paths (Fire / Lightning / Void), new stance rules
/// (CombatBalanceKit + StanceEffectsKit), renamed/rethemed moves, and per-move
/// rebalances scaled to 0.75 × the Lune AP-clamp curve.
///
/// Damage rule: Maelle base-damage total = 0.75 × clamp(AP); per-hit row =
/// 0.75×clamp ÷ hits. Conditional bonuses (Marked, per-Burn, per-hit-received,
/// stance-conditional) stay at their row values except where the design says
/// otherwise — each is an independent controllable balancing row.
///
/// Weapon-element moves (DynamicElementOverride) cannot take a fixed element
/// byte without crashing (repo-proven): FencersFlurry, MomentumStrike,
/// OffensiveSwitch, SwordBallet stay weapon-element.
///
/// LevelSequences are never written/packed (repo hard rule) — retheme FX via
/// BP-level retargets only where the donor's sequence is not involved.
/// </summary>
public static class MaelleSkillsKit
{
    public const string BalTable = "DT_Balancing_MainCharacterSkills";
    public const string StringTable = "ST_MainCharacters_Skills";
    public const string GraphStem = "DA_SkillGraph_Maelle";

    /// <summary>AP clamp — same curve as the Lune/Gustave kits.</summary>
    public static double Clamp(double ap) => SkillOverhaulKit.Clamp(ap);

    /// <summary>Maelle rule: 0.75 × the Lune curve value.</summary>
    public static double MaelleTotal(double ap) => 0.75 * Clamp(ap);

    /// <summary>Per-hit base row for an N-hit move: (0.75×clamp) ÷ hits.</summary>
    public static double PerHit(double ap, int hits) => MaelleTotal(ap) / hits;

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
           ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "extract_maelle"), stem + ".uasset")
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
        n += CreateNewMoves(modDir);
        n += PatchGradientMoves(modDir);
        n += PatchThermalArcBurnCap(modDir);
        n += PatchBurningCanvasReadCap(modDir);
        n += PatchDeathBargain(modDir);
        n += PatchFlameEater(modDir);
        n += PatchVirtuoseDmg(modDir);
        n += PatchRainOfFire(modDir);
        n += PatchNamesAndDescriptions(modDir);
        Console.WriteLine($"[Maelle] staged {n} asset edit(s)");
        return n;
    }

    // ──────────────────────────────────────────────────────────────────────
    // A. Balancing-row rebalance (0.75 × clamp)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>A balancing-row write: row name → FloatValue.</summary>
    private sealed record RowEdit(string Row, double Value);

    /// <summary>
    /// Maelle rebalance rows. Base total = 0.75 × clamp(AP) per move; the row is
    /// per-hit (total ÷ hits). Conditional rows (Marked / per-Burn / per-hit)
    /// are tuned per the design; "keep as-is" moves (Égide, Mezzo Forte) are absent.
    /// </summary>
    private static readonly RowEdit[] RebalanceRows =
    {
        // ── Lightning path ────────────────────────────────────────────────
        // Offensive Switch — 1 AP, 1 hit, weapon. 0.75×clamp(1)=0.2475. Defenceless 1 turn.
        new("OffensiveSwitch_BaseDamage", PerHit(1, 1)),
        // Thermal Arc (Combustion/InvigoratingFire) — 4 AP, 2 hits. 0.75×clamp(4)=0.99 ÷ 2 = 0.495.
        // Burn-consume: base × (1 + DamageIncreasePerBurn × consumed). Consume path: +5% per Burn.
        new("InvigoratingFire_BaseDamage", PerHit(4, 2)),
        new("InvigoratingFire_DamageIncreasePerBurn", 0.05),
        // Electric Fury (Fleuret Fury) — 9 AP, 3 hits, Lightning. 2.2275 ÷ 3 = 0.7425.
        new("FleuretFury_BaseDamage", PerHit(9, 3)),
        // Breaking Rules — 5 AP, 2 hits, Lightning. 1.2375 ÷ 2 = 0.61875.
        new("BreakingRules_BaseDamage", PerHit(5, 2)),
        // Payback — 5 AP, 1 hit, Lightning. 1.2375.
        new("Payback_BaseDamage", PerHit(5, 1)),
        // Gustave's Homage — 13 AP, 8 hits, Lightning. 5.8763 ÷ 8 = 0.7345.
        // Marked ×1.5 + LastHit ×2 kept.
        new("GustaveSMemoire_BaseDamage", PerHit(13, 8)),
        // Guard Down / Guard Up — durations 3 → 2 turns.
        new("GuardDown_DefenslessDuration", 2.0),
        new("GuardUp_ShellDuration", 2.0),
        // Fencer's Flurry — 4 AP, 1 hit AoE, weapon. 0.75×clamp(4)=0.99. Defenceless 1 turn kept.
        new("FencersFlurry_BaseDamage", PerHit(4, 1)),

        // ── Fire path ─────────────────────────────────────────────────────
        // Percée — 1 AP, 1 hit. 0.2475. Marked ×2 → ×1.5. Virtuose ×1.5 handled in BP.
        new("Percee_BaseDamage", PerHit(1, 1)),
        new("Percee_MarkedDamageMultiplier", 1.5),
        // Momentum Strike — 7 AP, 1 hit, weapon. 1.7325. Marked ×2 → ×1.5.
        new("MomentumStrike_BaseDamage", PerHit(7, 1)),
        new("MomentumStrike_MarkedDamageMultiplier", 1.5),
        // Virtuous Flames (Swift Stride/FireRise) — 3 AP, 1 hit, Fire. 0.7425.
        new("FireRise_BaseDamage", PerHit(3, 1)),
        // Pyrolyse — 9 AP, 3 hits, Fire. 2.2275 ÷ 3 = 0.7425.
        new("Pyrolyse_BaseDamage", PerHit(9, 3)),
        // Sword Ballet — 9 AP, 5 hits, weapon. 2.2275 ÷ 5 = 0.4455.
        new("SwordBallet_BaseDamage", PerHit(9, 5)),
        // Revenge — 5 AP, 1 hit, Fire. 1.2375. DamageIncreasePerHit 1.5 → 0.5 (+50%/hit).
        new("Revenge_BaseDamage", PerHit(5, 1)),
        new("Revenge_DamageIncreasePerHit", 0.5),
        // Spark — 2 AP, 1 hit, Fire. 0.495.
        new("Spark_BaseDamage", PerHit(2, 1)),
        // Degagement — 2 AP, 1 hit, Fire. Weakness duration 2 → 5 turns.
        new("Degagement_BaseDamage", PerHit(2, 1)),
        new("Degagement_WeaknessDuration", 5.0),

        // ── Void path ─────────────────────────────────────────────────────
        // Phantom Strike — 12 AP, 4 hits, Void. 4.6225 ÷ 4 = 1.1556. Gradient 0.35 → 0.5.
        new("PhantomStrike_BaseDamage", PerHit(12, 4)),
        new("PhantomStrike_GradientChargeAmount", 0.5),
        // Burning Canvas — 11 AP, 5 hits, Void. 3.6362 ÷ 5 = 0.7272. Per-burn 0.1 → 0.01 (+1%/Burn).
        new("BurningCanvas_BaseDamage", PerHit(11, 5)),
        new("BurningCanvas_DamageIncreasePerBurnStack", 0.01),
        // Stendhal — 15 AP, 1 hit, Void. 9.4966.
        new("Stendhal_BaseDamage", PerHit(15, 1)),

        // ── Death's Bargain (Last Chance) — 8 AP. Refills all AP: Maelle's full bar
        // is 21 (the vanilla 9 = a generic full refill, but Maelle's max is 21 per the
        // user's balance); HP 1 kept.
        new("LastChance_APGain", 21.0),
        new("LastChance_NewHpAmount", 1.0),
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
        Console.WriteLine($"[Maelle] rebalanced {RebalanceRows.Length} balancing row(s)");
        return n;
    }

    // ──────────────────────────────────────────────────────────────────────
    // B. DA property edits (AP cost) + element retheme (DA + skill-script BP)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>DA property edits. Property values are JSON scalars (APCost int).</summary>
    private static readonly (string DaStem, string Prop, string JsonValue)[] DaEdits =
    {
        // Lightning path AP/retune
        ("DA_Skill_Maelle_NEW19_InvigoratingFire", "APCost", "4"), // Thermal Arc (4 AP stays 4)
        ("DA_Skill_Maelle_FleuretFury", "APCost", "9"),        // Electric Fury
        ("DA_Skill_Maelle_BreakingRules", "APCost", "5"),      // Breaking Rules
        ("DA_Skill_Maelle_NEW13_Payback", "APCost", "5"),      // Payback
        ("DA_Skill_Maelle_GustaveSMemoire", "APCost", "13"),   // Gustave's Homage
        // Fire path AP/retune
        ("DA_Skill_Maelle_NEW14_Percee", "APCost", "1"),       // Percée
        ("DA_Skill_Maelle_NEW18_Spark", "APCost", "2"),        // Spark
        // Void path AP/retune
        ("DA_Skill_Maelle_PhantomStrike", "APCost", "12"),     // Phantom Strike
        ("DA_Skill_Maelle_BurningCanvas", "APCost", "11"),     // Burning Canvas
        ("DA_Skill_Maelle_Stendhal", "APCost", "15"),          // Stendhal
        ("DA_Skill_Maelle_LastChance", "APCost", "8"),         // Death's Bargain
    };

    /// <summary>
    /// Element retheme: the move's fixed-element damage becomes the target element.
    /// Weapon-element moves (DynamicElementOverride / GetBaseDamageType) are excluded
    /// — they cannot take a fixed byte without crashing. Percée is fixed-Physical
    /// (MakeDamageBuilder byte 1) so it CAN become Fire; Momentum is true weapon.
    /// DA stem → target element display name.
    /// Applied as: DA BaseElementalType enum + the BP's MakeDamageBuilder byte.
    /// </summary>
    private static readonly (string DaStem, string BpStem, string Element)[] ElementRethemes =
    {
        // Lightning path
        ("DA_Skill_Maelle_NEW19_InvigoratingFire", "BP_Battle_SkillScript_New19_InvigoratingFire", "Lightning"), // Thermal Arc
        ("DA_Skill_Maelle_FleuretFury",       "BP_Battle_SkillScript_FleuretFury",       "Lightning"), // Electric Fury
        ("DA_Skill_Maelle_BreakingRules",     "BP_Battle_SkillScript_BreakingRules",     "Lightning"), // Breaking Rules
        ("DA_Skill_Maelle_NEW13_Payback",     "BP_Battle_SkillScript_New13_Payback",     "Lightning"), // Payback
        // Fire path
        ("DA_Skill_Maelle_NEW4_FireRise",     "BP_Battle_SkillScript_New4_FireRise",     "Fire"),      // Flame Eater (Swift Stride)
        ("DA_Skill_Maelle_NEW14_Percee",      "BP_Battle_SkillScript_New14_Percee",      "Fire"),      // Percée
        // Gradient — Golgra's Combo (Virtuose Strike) is Lightning
        ("DA_Skill_Maelle_VirtuoseStrike",    "BP_Battle_SkillScript_VirtuoseStrike",    "Lightning"), // Golgra's Combo
    };

    private static int PatchDataAssets(string modDir)
    {
        int n = 0;
        foreach (var (stem, prop, jsonValue) in DaEdits)
        {
            var src = FindTables(stem);
            Stage(src, modDir, out var dest);
            var ed = DataTableEditor.OpenPropertyAsset(dest);
            n += ed.ApplyToProperties($"{{\"{prop}\":{jsonValue}}}");
            ed.Save(dest);
            Console.WriteLine($"[Maelle] {stem} {prop} -> {jsonValue}");
        }
        foreach (var (daStem, bpStem, element) in ElementRethemes)
        {
            n += RethemeDaElement(daStem, element, modDir);
            n += RethemeBpElement(bpStem, element, modDir);
        }
        return n;
    }

    /// <summary>Sets a skill DA's BaseElementalType enum to the element's EAttackType enumerator.</summary>
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
        da.Write(dest);
        Console.WriteLine($"[Maelle] {daStem} BaseElementalType -> {element}");
        return n;
    }

    /// <summary>Retargets a skill-script BP's MakeDamageBuilder element byte (proven Lune/SuperSpark
    /// pattern). The element slot is the FIRST ByteConst parameter of MakeDamageBuilder (right after the
    /// target stats context) — e.g. Physical=1, Fire=2, Lightning=4, Void=8. Later ByteConsts are
    /// targeting / source markers and are left alone.</summary>
    private static int RethemeBpElement(string bpStem, string element, string modDir)
    {
        var src = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), bpStem + ".uasset")
            ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "extract_maelle"), bpStem + ".uasset")
            ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "extracted"), bpStem + ".uasset");
        if (src is null) { Console.WriteLine($"[Maelle] {bpStem} not found; element retheme skipped"); return 0; }
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
        Console.WriteLine($"[Maelle] {bpStem} MakeDamageBuilder element -> {element} ({n} fn(s))");
        return n;
    }

    /// <summary>In-place MakeDamageBuilder element retheme on an already-open BP
    /// (used for brand-new clones that do not exist under work/tables yet). The element
    /// slot is the FIRST ByteConst parameter of MakeDamageBuilder (right after the target
    /// stats context) — e.g. Fire=2, Lightning=4, Void=8. Later ByteConsts are targeting /
    /// source markers and are left alone.</summary>
    private static int RethemeBpInPlace(UAsset bp, string element)
    {
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
        if (n > 0) Console.WriteLine($"[Maelle] {Path.GetFileNameWithoutExtension(bp.FilePath)} MakeDamageBuilder element -> {element} ({n} fn(s))");
        return n;
    }

    // ──────────────────────────────────────────────────────────────────────
    // C. Gradient moves (charge cost + damage rows)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Gradient moves: DA APCost (charge cost) + balancing damage row.
    /// The damage row is per-hit; the requested "xN" is the move's TOTAL, so for
    /// multi-hit gradient moves the per-hit row = total ÷ hits (Golgra's Combo is
    /// 5 hits → 30/5 = 6). Gommage (1 hit) stays 100.</summary>
    private static readonly (string DaStem, string Row, int Cost, double Damage)[] GradientMoves =
    {
        // Golgra's Combo (Virtuose Strike) — 3 charges, 5 hits, total ×30 → per-hit 6, Lightning.
        ("DA_Skill_Maelle_VirtuoseStrike", "VirtuoseStrike_BaseDamage", 3, 6.0),
        // Phoenix Flame — 5 charges, revive 100%, burn 100.
        ("DA_Skill_Maelle_PhoenixFlame", "", 5, 0),
        // Gommage — 10 charges, 1 hit, total ×100.
        ("DA_Skill_Maelle_Gommage", "Gommage_BaseDamage", 10, 100.0),
    };

    private static int PatchGradientMoves(string modDir)
    {
        int n = 0;
        var balSrc = FindTables(BalTable);
        Stage(balSrc, modDir, out var balDest);
        var ed = new DataTableEditor(balDest);
        var rows = new JObject();

        foreach (var (stem, row, cost, damage) in GradientMoves)
        {
            var src = FindTables(stem);
            Stage(src, modDir, out var dest);
            var daEd = DataTableEditor.OpenPropertyAsset(dest);
            n += daEd.ApplyToProperties($"{{\"APCost\":{cost}}}");
            daEd.Save(dest);
            Console.WriteLine($"[Maelle] {stem} APCost (charges) -> {cost}");
            if (row.Length > 0)
                rows[row] = new JObject { ["FloatValue"] = damage };
        }
        // Phoenix Flame special rows (revive 100%, burn 100) applied in balancing table.
        rows["PhoenixFlame_BurnAmount"] = new JObject { ["FloatValue"] = 100.0 };
        rows["PhoenixFlame_BaseRevivePercentHealth"] = new JObject { ["FloatValue"] = 1.0 };
        rows["PhoenixFlame_RevivePercentHealthPerQTE"] = new JObject { ["FloatValue"] = 0.0 };
        n += ed.Apply(rows.ToString(Newtonsoft.Json.Formatting.None));
        ed.Save(balDest);
        return n;
    }

    // ──────────────────────────────────────────────────────────────────────
    // D. Thermal Arc (Combustion) — raise burn-consume cap 10 → 10000
    // ──────────────────────────────────────────────────────────────────────

    private const string ThermalArcBpStem = "BP_Battle_SkillScript_New19_InvigoratingFire";

    /// <summary>
    /// Thermal Arc (renamed Combustion) consumes up to N Burn for increased damage.
    /// The cap is the literal IntConst 10 inside BurnToConsume = Min(burnCount, 10)
    /// in the per-target consume loop. Raising it 10 → 10000 is a same-size scalar
    /// change (no offset shift). The description ("Consumes up to 10000 Burn") is set
    /// in PatchNamesAndDescriptions.
    /// </summary>
    private static int PatchThermalArcBurnCap(string modDir)
    {
        var src = FindTables(ThermalArcBpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        int n = 0;
        foreach (var fn in bp.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            bool changed = false;
            foreach (var s in fn.ScriptBytecode)
            {
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (changed || e is not EX_FinalFunction ff) return;
                    if (!ff.StackNode.IsImport()) return;
                    var impName = bp.Imports[Math.Abs(ff.StackNode.Index) - 1].ObjectName.ToString();
                    if (impName != "Min") return;
                    // Cap is the IntConst 10 parameter of Min(burnCount, 10) that feeds BurnToConsume.
                    foreach (var p in ff.Parameters)
                        if (p is EX_IntConst ic && ic.Value == 10)
                        {
                            ic.Value = 10000;
                            changed = true;
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
        Console.WriteLine($"[Maelle] {ThermalArcBpStem}: burn-consume cap 10 -> 10000 ({n} fn(s))");
        return n;
    }

    // ──────────────────────────────────────────────────────────────────────
    // D2. Burning Canvas family — uncap the per-burn read (Clamp 0..100 → 0..10000)
    // ──────────────────────────────────────────────────────────────────────

    private const string BurningCanvasBpStem = "BP_Battle_SkillScript_BurningCanvas";

    /// <summary>
    /// Burning Canvas (and its LActeFinalIncendiaire clone) scale damage by the target
    /// current Burn stacks: mult = 1 + DamageIncreasePerBurnStack x Clamp(stacks, 0, 100).
    /// Raising the clamp literal 100 → 10000 makes the read effectively uncapped
    /// (same-size IntConst rewrite, no offset shift).
    /// </summary>
    private static int PatchBurnReadClampBp(string bpDest, string what)
    {
        var bp = Open(bpDest);
        var fn = bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (fn?.ScriptBytecode is null) return 0;
        int n = 0;
        foreach (var st in fn.ScriptBytecode)
        {
            bool isClampLet = false;
            MayhemFixKit.Walk(new[] { st }, e =>
            {
                if (isClampLet || e is not EX_Let let) return;
                if (let.Variable is not EX_LocalVariable lv) return;
                if (PtrName(lv.Variable) != "CallFunc_Clamp_ReturnValue") return;
                if (let.Expression is not EX_CallMath cm || !cm.StackNode.IsImport()) return;
                if (cm.StackNode.ToImport(bp).ObjectName.ToString() != "Clamp") return;
                if (cm.Parameters.Length != 3) return;
                bool readsBurnCount = false;
                MayhemFixKit.Walk(new[] { cm.Parameters[0] }, x =>
                {
                    if (x is EX_LocalVariable ilv && PtrName(ilv.Variable) == "CallFunc_GetActiveBuffCounts_ReturnValue")
                        readsBurnCount = true;
                });
                if (!readsBurnCount) return;
                if (cm.Parameters[2] is EX_IntConst ic && ic.Value == 100) { ic.Value = 10000; isClampLet = true; }
            });
            if (isClampLet) n++;
        }
        if (n != 1)
        {
            Console.WriteLine($"[Maelle] {what}: burn-read clamp x{n} (expect 1); skipped");
            return 0;
        }
        MayhemFixKit.RecalcContextSkips(bp, fn);
        bp.Write(bpDest);
        Console.WriteLine($"[Maelle] {what}: burn-read clamp 100 -> 10000");
        return n;
    }

    private static int PatchBurningCanvasReadCap(string modDir)
    {
        var src = FindTables(BurningCanvasBpStem);
        Stage(src, modDir, out var dest);
        return PatchBurnReadClampBp(dest, BurningCanvasBpStem);
    }

    // ──────────────────────────────────────────────────────────────────────
    /// <summary>
    /// LActe Final applies 5 Burn per hit (BurnAppliedPerHit row = 5.0), but the Burning
    /// Canvas donor kismet spawns the Burn buff with the default single stack and never
    /// reads that row. Insert a row read plus ChangeStackCount(spawned, FTrunc(row))
    /// between the per-hit SpawnObject and ActivateBuff, mirroring Combustion.
    /// </summary>
    private static int PatchLActeFinal(string bpDest, MaelleNewMove m)
    {
        int n = PatchBurnReadClampBp(bpDest, m.BpStem);
        n += PatchLActeBurnStacks(bpDest, m.NameId + "_BurnAppliedPerHit");
        return n;
    }

    private static int PatchLActeBurnStacks(string bpDest, string rowName)
    {
        var bp = Open(bpDest);
        var fn = bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode!.ToList();

        int rowIdx = -1;
        string? outRow = null;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool isRowRead = false, isBurnStackRow = false;
            string? outName = null;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_Context ctx && ctx.ContextExpression is EX_FinalFunction ff
                    && ff.StackNode.IsImport() && bp.Imports[Math.Abs(ff.StackNode.Index) - 1].ObjectName.ToString() == "GetDataTableRowFromName")
                    isRowRead = true;
                if (e is EX_NameConst nc && nc.Value.ToString().EndsWith("DamageIncreasePerBurnStack", StringComparison.Ordinal))
                    isBurnStackRow = true;
                if (e is EX_LocalVariable lv && PtrName(lv.Variable).StartsWith("CallFunc_GetDataTableRowFromName_OutRow", StringComparison.Ordinal))
                    outName = PtrName(lv.Variable);
            });
            if (isRowRead && isBurnStackRow && outName is not null) { rowIdx = i; outRow = outName; }
        }
        if (rowIdx < 0 || outRow is null)
            throw new InvalidDataException("[Maelle] LActeFinal: burn-stack row read not found");

        KismetExpression? extractor = null;
        for (int i = rowIdx + 1; i < stmts.Count && extractor is null; i++)
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (extractor is not null || e is not EX_StructMemberContext smc) return;
                MayhemFixKit.Walk(new[] { smc }, x =>
                {
                    if (extractor is not null) return;
                    if (x is EX_LocalVariable ilv && PtrName(ilv.Variable) == outRow)
                        extractor = (KismetExpression)MayhemFixKit.CloneKismet(smc);
                });
            });
        if (extractor is null)
            throw new InvalidDataException("[Maelle] LActeFinal: burn-stack FloatValue extractor not found");

        int spawnIdx = -1, actIdx = -1;
        KismetExpression? spawnedLocal = null;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hasSpawn = false, hasBurn = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (hasSpawn && hasBurn) return;
                if (e is EX_LetObj lo && lo.AssignmentExpression is EX_FinalFunction ff
                    && ff.StackNode.IsImport() && bp.Imports[Math.Abs(ff.StackNode.Index) - 1].ObjectName.ToString() == "SpawnObject") hasSpawn = true;
                if (e is EX_ObjectConst oc && oc.Value.IsImport()
                    && bp.Imports[Math.Abs(oc.Value.Index) - 1].ObjectName.ToString() == "BP_BattleBuff_Burn_C") hasBurn = true;
            });
            if (hasSpawn && hasBurn && spawnIdx < 0)
            {
                spawnIdx = i;
                if (stmts[i] is EX_LetObj letObj && letObj.VariableExpression is EX_LocalVariable glv)
                    spawnedLocal = (KismetExpression)MayhemFixKit.CloneKismet(glv);
            }
            if (spawnIdx >= 0 && actIdx < 0 && i > spawnIdx
                && stmts[i] is EX_Context actCtx && actCtx.ContextExpression is EX_LocalVirtualFunction avf
                && avf.VirtualFunctionName.ToString() == "ActivateBuff")
                actIdx = i;
        }
        if (spawnIdx < 0 || actIdx < 0 || spawnedLocal is null)
            throw new InvalidDataException("[Maelle] LActeFinal: burn SpawnObject/ActivateBuff not found");

        var truncFn = LuneSkillsKit.EnsureKismetMathFn(bp, "FTrunc");

        var rowRead = (KismetExpression)MayhemFixKit.CloneKismet(stmts[rowIdx]);
        MayhemFixKit.Walk(new[] { rowRead }, e =>
        {
            if (e is EX_NameConst nc && nc.Value.ToString().EndsWith("DamageIncreasePerBurnStack", StringComparison.Ordinal))
                nc.Value = FName.FromString(bp, rowName);
        });

        var setStacks = new EX_Context
        {
            ObjectExpression = spawnedLocal,
            ContextExpression = new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(bp, "ChangeStackCount"),
                Parameters = new KismetExpression[]
                {
                    new EX_CallMath
                    {
                        StackNode = truncFn,
                        Parameters = new KismetExpression[] { extractor },
                    },
                },
            },
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
        };

        uint insOff = 0;
        for (int i = 0; i < actIdx; i++) insOff += (uint)stmts[i].GetSize(bp);
        uint total = (uint)rowRead.GetSize(bp) + (uint)setStacks.GetSize(bp);
        stmts.InsertRange(actIdx, new[] { rowRead, setStacks });
        fn.ScriptBytecode = stmts.ToArray();
        MayhemFixKit.ShiftAbsolute(bp, fn, insOff, total, null);
        MayhemFixKit.RecalcContextSkips(bp, fn);
        bp.Write(bpDest);
        Console.WriteLine($"[Maelle] LActeFinal: burn apply now reads {rowName} (ChangeStackCount per hit)");
        return 2;
    }

    // E. Death's Bargain (Last Chance) — self-Curse (SpawnObject+ActivateBuff)
    // ──────────────────────────────────────────────────────────────────────

    private const string LastChanceBpStem = "BP_Battle_SkillScript_LastChance";

    /// <summary>
    /// Death's Bargain (renamed Last Chance): 8 AP (DA), self HP → 1, refills AP,
    /// switches Virtuose (all vanilla kismet) — plus a new self-Curse.
    ///
    /// CRASH/BIND FIX HISTORY (2026-09-05):
    ///  (1) hand-built 1-arg ApplyBuff(class) — malformed, crashed the VM on use;
    ///  (2) cross-asset FProperty local copy — corrupted the BP;
    ///  (3) bare full-shape ApplyBuff + same-asset out-local — no crash, but the Curse
    ///      did NOT bind to Maelle (ApplyBuff works for plain buffs like Blight, but
    ///      every BP that applies Cursed to a player uses SpawnObject+ActivateBuff —
    ///      Cursed's custom ActivateBuff/OnCharacterDealtDamages hooks only register
    ///      through that path).
    /// FINAL: SpawnObject(BP_BattleBuff_Cursed_C, selfStats) then
    /// Context(selfStats).ActivateBuff(spawned, ByteConst 4, selfStats, success),
    /// cloned from the game's own AutoCurse/ReviveWithCurse/OffensiveSwitch shape,
    /// with both locals same-asset cloned from LastChance's own FProperties.
    /// </summary>
    private static int PatchDeathBargain(string modDir)
    {
        // Self-Curse via AutoCurse's shape: SpawnObject(Cursed_C, selfStats) then
        // selfStats.ActivateBuff(spawned, ByteConst 4, selfStats, success).
        // Synthesized CurrentCharacter/AC_jRPG_CharacterStats FFieldPaths are
        // no-ops in this game (same bug as stance Berserk / Lune GainAP) — clone
        // the InstVar pointers off LastChance's own working GainAP context.
        var src = FindTables(LastChanceBpStem);
        var dest = StageDest(src, modDir);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        var bp = Open(dest);
        var fn = bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (fn?.ScriptBytecode is null) return 0;

        EX_Context? gainCtx = null;
        int gainApIdx = -1;
        for (int i = 0; i < fn.ScriptBytecode.Length; i++)
        {
            MayhemFixKit.Walk(new[] { fn.ScriptBytecode[i] }, e =>
            {
                if (gainCtx is not null) return;
                if (e is EX_Context ctx && ctx.ContextExpression is EX_VirtualFunction vf
                    && vf.VirtualFunctionName.ToString() == "GainAP")
                    gainCtx = ctx;
            });
            if (gainCtx is not null) { gainApIdx = i; break; }
        }
        if (gainCtx?.ObjectExpression is null || gainApIdx < 0) return 0;
        var statsTpl = gainCtx.ObjectExpression; // CurrentCharacter.AC_jRPG_CharacterStats
        var rvPtr = gainCtx.RValuePointer ?? new KismetPropertyPointer(new FFieldPath());

        FProperty? objProp = (fn.LoadedProperties ?? Array.Empty<FProperty>())
            .FirstOrDefault(p => p is FObjectProperty && p.Name.ToString() == "CallFunc_GetCurrentCharacter_CurrentCharacter");
        FProperty? boolProp = (fn.LoadedProperties ?? Array.Empty<FProperty>())
            .FirstOrDefault(p => p is FBoolProperty && p.Name.ToString() == "K2Node_SwitchInteger_CmpSuccess");
        if (objProp is null || boolProp is null) return 0;

        var props = (fn.LoadedProperties ?? Array.Empty<FProperty>()).ToList();
        var ownerIdx = FPackageIndex.FromExport(bp.Exports.IndexOf(fn));
        var spawnProp = (FProperty)ShallowClone(objProp);
        spawnProp.Name = FName.FromString(bp, "CallFunc_SpawnObject_ReturnValue");
        props.Add(spawnProp);
        var successProp = (FProperty)ShallowClone(boolProp);
        successProp.Name = FName.FromString(bp, "CallFunc_ActivateBuff_Success");
        props.Add(successProp);
        fn.LoadedProperties = props.ToArray();

        EX_LocalVariable Local(string name) => new()
        {
            Variable = new KismetPropertyPointer(new FFieldPath(new[] { new FName(bp, name) }, ownerIdx)),
        };
        var spawnLocal = Local("CallFunc_SpawnObject_ReturnValue");
        var successLocal = Local("CallFunc_ActivateBuff_Success");
        KismetExpression Stats() => MayhemFixKit.CloneKismet(statsTpl);

        var spawnFn = EnsureEngineFunction(bp, "GameplayStatics", "SpawnObject");
        var setIntFn = EnsureEngineFunction(bp, "KismetSystemLibrary", "SetIntPropertyByName");
        const string cursedPkg = "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Cursed";
        var cursedIdx = LuneSkillsKit.EnsureObjectImport(bp, cursedPkg,
            "BP_BattleBuff_Cursed_C", "BP_BattleBuff_Cursed_C", "/Script/Engine");
        LuneSkillsKit.EnsureObjectImport(bp, cursedPkg,
            "Default__BP_BattleBuff_Cursed_C", "BP_BattleBuff_Cursed_C", cursedPkg);

        var spawn = new EX_LetObj
        {
            VariableExpression = spawnLocal,
            AssignmentExpression = new EX_FinalFunction
            {
                StackNode = spawnFn,
                Parameters = new KismetExpression[]
                {
                    new EX_ObjectConst { Value = cursedIdx },
                    Stats(),
                },
            },
        };
        var setDuration = new EX_FinalFunction
        {
            StackNode = setIntFn,
            Parameters = new KismetExpression[]
            {
                spawnLocal,
                new EX_NameConst { Value = FName.FromString(bp, "TurnDuration") },
                new EX_IntConst { Value = 4 },
            },
        };
        var activate = new EX_Context
        {
            ObjectExpression = Stats(),
            ContextExpression = new EX_VirtualFunction
            {
                VirtualFunctionName = FName.FromString(bp, "ActivateBuff"),
                Parameters = new KismetExpression[]
                {
                    spawnLocal,
                    new EX_ByteConst { Value = 4 },
                    Stats(),
                    successLocal,
                },
            },
            RValuePointer = rvPtr,
        };

        var stmts = fn.ScriptBytecode.ToList();
        int insertIdx = gainApIdx + 1;
        uint insertOffset = 0;
        for (int i = 0; i < insertIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
        uint delta = (uint)spawn.GetSize(bp) + (uint)setDuration.GetSize(bp) + (uint)activate.GetSize(bp);
        MayhemFixKit.ShiftAbsolute(bp, fn, insertOffset, delta, null);
        stmts.Insert(insertIdx, activate);
        stmts.Insert(insertIdx, setDuration);
        stmts.Insert(insertIdx, spawn);
        fn.ScriptBytecode = stmts.ToArray();
        MayhemFixKit.RecalcContextSkips(bp, fn);
        bp.Write(dest);
        Console.WriteLine("[Maelle] Death's Bargain: self-Curse cloned from GainAP stats context after GainAP");
        return 1;
    }

    private static FPackageIndex EnsureEngineFunction(UAsset asset, string library, string functionName)
    {
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (imp.ObjectName.ToString() != functionName) continue;
            if (!imp.OuterIndex.IsImport()) continue;
            var outer = asset.Imports[Math.Abs(imp.OuterIndex.Index) - 1].ObjectName.ToString();
            if (outer == library) return FPackageIndex.FromImport(i);
        }
        FPackageIndex? enginePkg = null, lib = null;
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (imp.ObjectName.ToString() == "/Script/Engine" && imp.ClassName.ToString() == "Package")
                enginePkg = FPackageIndex.FromImport(i);
            if (imp.ObjectName.ToString() == library)
                lib = FPackageIndex.FromImport(i);
        }
        enginePkg ??= asset.AddImport(new Import("/Script/CoreUObject", "Package",
            FPackageIndex.FromRawIndex(0), "/Script/Engine", false, asset));
        lib ??= asset.AddImport(new Import("/Script/CoreUObject", library, enginePkg, library, false, asset));
        return asset.AddImport(new Import("/Script/CoreUObject", "Function", lib, functionName, false, asset));
    }

    // ──────────────────────────────────────────────────────────────────────
    // E. Virtuous Flames (Swift Stride / FireRise) — strip the AP regain
    // ──────────────────────────────────────────────────────────────────────

    private const string FireRiseBpStem = "BP_Battle_SkillScript_New4_FireRise";

    /// <summary>
    /// Virtuous Flames (renamed Swift Stride): 3 AP Fire, switches to Virtuose if the
    /// target is Burning (kept), no AP regain (removed). The vanilla BP deals and
    /// then GainAP(Local_APGain). Removing the GainAP statement shifts all later
    /// absolute offsets (ShiftAbsoluteSigned).
    /// </summary>
    private static int PatchFlameEater(string modDir)
    {
        var src = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), FireRiseBpStem + ".uasset")
            ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "extract_maelle"), FireRiseBpStem + ".uasset")
            ?? throw new FileNotFoundException(FireRiseBpStem + ".uasset");
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        var fn = bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();

        // Find the statement whose virtual call is GainAP.
        int gainIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool has = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (has) return;
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "GainAP") has = true;
            });
            if (has) { gainIdx = i; break; }
        }
        if (gainIdx < 0) return 0; // already stripped / no AP gain

        // Remove the statement and shift everything after it back by its size.
        var removed = stmts[gainIdx];
        uint removedOffset = 0;
        for (int i = 0; i < gainIdx; i++) removedOffset += (uint)stmts[i].GetSize(bp);
        uint removedSize = (uint)removed.GetSize(bp);
        LuneSkillsKit.ShiftAbsoluteSigned(bp, fn, removedOffset + removedSize, removedSize);
        stmts.RemoveAt(gainIdx);
        fn.ScriptBytecode = stmts.ToArray();
        MayhemFixKit.RecalcContextSkips(bp, fn);
        bp.Write(dest);
        Console.WriteLine("[Maelle] Flame Eater: GainAP statement removed (AP regain stripped)");
        return 1;
    }

    // ──────────────────────────────────────────────────────────────────────
    // F2. NEW Maelle moves (donor clones: Lightning + Fire paths)
    //     Lightning: Lumière Above, Revers Fatal, Déviation Absolue,
    //     For Those Who Come After. Fire: Vengeance Ardente, Alicia's Nightmare,
    //     Blade Waltz, L'Acte Final Incendiaire.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Spec for a NEW Maelle move cloned from an existing Maelle skill.</summary>
    public sealed record MaelleNewMove(
        string DaStem,          // new DA stem, e.g. "DA_Skill_Maelle_LumiereAbove"
        string BpStem,          // new BP stem, e.g. "BP_Battle_SkillScript_LumiereAbove"
        string NameId,          // DA NameID + ST key midfix, e.g. "LumiereAbove"
        string DonorDa,         // donor DA stem, e.g. "DA_Skill_Maelle_NEW9_RainOfFire"
        string DonorBp,         // donor BP stem, e.g. "BP_Battle_SkillScript_New9_RainOfFire"
        string DonorStem,       // donor row/stem prefix, e.g. "RainOfFire"
        int ApCost,
        string DisplayName,
        string Description,
        string ShortDescription,
        double BaseDamage,      // per-hit row value = PerHit(ApCost, hits)
        int Hits,               // donor sequence hit count (for BreakBar + doc)
        string? Element = "Lightning",
        (string From, string To)[]? ExtraPairs = null, // FX/asset retargets
        FVector2D? Position = null); // floating graph position (no prereq edge)

    /// <summary>The four new Maelle moves (donor clones, floating graph nodes).</summary>
    public static readonly MaelleNewMove[] NewMoves =
    {
        // Lumière Above: Rain of Fire clone — Lightning barrage, no Burn,
        // switches Defensive, ×2 in Virtuose. Single target: it inherits the
        // donor's TargetingType (NewEnumerator1, same as Payback/Pyrolyse).
        new(
            "DA_Skill_Maelle_LumiereAbove", "BP_Battle_SkillScript_LumiereAbove", "LumiereAbove",
            "DA_Skill_Maelle_NEW9_RainOfFire", "BP_Battle_SkillScript_New9_RainOfFire", "RainOfFire",
            15, "Lumière Above",
            "Deals <keyword id=\"DamageMultiplier\">2.53x</> single target <keyword id=\"Element_Lightning\">Lightning</> damage per hit. 2 hits.\n" +
            "Switches to <keyword id=\"Stance_Defensive\">Defensive Stance</>.\n" +
            "<keyword id=\"Stance_Virtuose\">Virtuose Stance</>: deal <keyword id=\"DamageMultiplier\">2.00x</> more damage.",
            "<keyword id=\"DamageMultiplier\">2.53x</> <keyword id=\"Element_Lightning\">Lightning</> damage per hit. 2 hits.\n" +
            "Switches to <keyword id=\"Stance_Defensive\">Defensive Stance</>.\n" +
            "<keyword id=\"Stance_Virtuose\">Virtuose Stance</>: deal 2.00x more damage.",
            2.5324, 2, "Lightning",
            Position: new FVector2D(125f, 1225f)),

        // Revers Fatal: Thermal Arc (Combustion) clone — Lightning nuke, consumes
        // up to 10000 Burn for damage, no Burn apply; Virtuose-gated Greater Slow +
        // Greater Defenceless. Thermal Arc = 4 AP, 2 hits, burn-consume.
        new(
            "DA_Skill_Maelle_ReversFatal", "BP_Battle_SkillScript_ReversFatal", "ReversFatal",
            "DA_Skill_Maelle_NEW19_InvigoratingFire", "BP_Battle_SkillScript_New19_InvigoratingFire", "InvigoratingFire",
            17, "Revers Fatal",
            "Deals <keyword id=\"DamageMultiplier\">4.09x</> single target <keyword id=\"Element_Lightning\">Lightning</> damage per hit. 2 hits.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> up to 10000 <keyword id=\"StatusEffect_Burn\">Burn</>: deal <keyword id=\"DamageMultiplier\">5%</> more damage per <keyword id=\"StatusEffect_Burn\">Burn</> consumed.\n" +
            "<keyword id=\"Stance_Virtuose\">Virtuose Stance</>: also inflicts <keyword id=\"Buff_Slow\">Greater Slow</> and <keyword id=\"Buff_Defenseless\">Greater Defenceless</> for 3 turns.",
            "<keyword id=\"DamageMultiplier\">4.09x</> <keyword id=\"Element_Lightning\">Lightning</> damage per hit. 2 hits.\n" +
            "Consumes <keyword id=\"StatusEffect_Burn\">Burn</>: deal 5% more damage per <keyword id=\"StatusEffect_Burn\">Burn</> consumed.\n" +
            "<keyword id=\"Stance_Virtuose\">Virtuose Stance</>: inflicts <keyword id=\"Buff_Slow\">Greater Slow</> and <keyword id=\"Buff_Defenseless\">Greater Defenceless</> for 3 turns.",
            4.0926, 2, "Lightning",
            Position: new FVector2D(250f, 1225f)),

        // Déviation Absolue: Payback clone — 19 AP Lightning counter, −2 AP per
        // hit parried last turn. Payback = 5 AP(vanilla 9), 1 hit.
        new(
            "DA_Skill_Maelle_DeviationAbsolue", "BP_Battle_SkillScript_DeviationAbsolue", "DeviationAbsolue",
            "DA_Skill_Maelle_NEW13_Payback", "BP_Battle_SkillScript_New13_Payback", "Payback",
            19, "Déviation Absolue",
            "Deals <keyword id=\"DamageMultiplier\">13.23x</> single target <keyword id=\"Element_Lightning\">Lightning</> damage. 1 hit.\n" +
            "Reduced <keyword id=\"APShard\">AP</> cost by 2 for each attack parried since last turn.\n" +
            "Can <keyword id=\"Break\">Break</>",
            "<keyword id=\"DamageMultiplier\">13.23x</> <keyword id=\"Element_Lightning\">Lightning</> damage. 1 hit.\n" +
            "Reduced <keyword id=\"APShard\">AP</> cost by 2 for each attack parried last turn.\n" +
            "Can <keyword id=\"Break\">Break</>",
            13.2278, 1, "Lightning",
            Position: new FVector2D(375f, 1225f)),

        // For Those Who Come After: Gustave's Homage clone — 8-hit Lightning,
        // keeps Marked ×1.5 + LastHit ×2. Homage = 13 AP(vanilla 8), 8 hits.
        new(
            "DA_Skill_Maelle_ForThoseWhoComeAfter", "BP_Battle_SkillScript_ForThoseWhoComeAfter", "ForThoseWhoComeAfter",
            "DA_Skill_Maelle_GustaveSMemoire", "BP_Battle_SkillScript_GustaveSMemoire", "GustaveSMemoire",
            21, "For Those Who Come After",
            "Deals <keyword id=\"DamageMultiplier\">2.67x</> single target <keyword id=\"Element_Lightning\">Lightning</> damage per hit. 8 hits.\n" +
            "The final hit deals <keyword id=\"DamageMultiplier\">2.00x</> damage.\n" +
            "Increased damage to <keyword id=\"StatusEffect_Mark\">Marked</> targets.\n" +
            "Doesn't remove Mark.",
            "<keyword id=\"DamageMultiplier\">2.67x</> <keyword id=\"Element_Lightning\">Lightning</> damage per hit. 8 hits.\n" +
            "The final hit deals <keyword id=\"DamageMultiplier\">2.00x</> damage.\n" +
            "Increased damage to <keyword id=\"StatusEffect_Mark\">Marked</> targets.\n" +
            "Doesn't remove Mark.",
            2.6721, 8, "Lightning",
            Position: new FVector2D(500f, 1225f)),

        // Vengeance Ardente: Revenge clone — 16 AP Fire counter, +50%/hit received.
        // Revenge = 5 AP, 1 hit, Fire, switches Offensive on cast.
        new(
            "DA_Skill_Maelle_VengeanceArdente", "BP_Battle_SkillScript_VengeanceArdente", "VengeanceArdente",
            "DA_Skill_Maelle_Revenge", "BP_Battle_SkillScript_Revenge", "Revenge",
            16, "Vengeance Ardente",
            "Deals <keyword id=\"DamageMultiplier\">6.44x</> single target <keyword id=\"Element_Fire\">Fire</> damage. 1 hit.\n" +
            "Damage increased <keyword id=\"DamageMultiplier\">+50%</> for each hit received since the previous turn.\n" +
            "Can <keyword id=\"Break\">Break</>",
            "<keyword id=\"DamageMultiplier\">6.44x</> <keyword id=\"Element_Fire\">Fire</> damage. 1 hit.\n" +
            "Damage increased +50% for each hit received since the previous turn.\n" +
            "Can <keyword id=\"Break\">Break</>",
            6.4387, 1, "Fire",
            Position: new FVector2D(125f, 1325f)),

        // Alicia's Nightmare: Pyrolyse clone — 21 AP Fire, 30 Burn/hit (+20 in
        // Offensive). Pyrolyse = 9 AP, 3 hits, Fire, overcharge/stance burn bonus.
        new(
            "DA_Skill_Maelle_AliciasNightmare", "BP_Battle_SkillScript_AliciasNightmare", "AliciasNightmare",
            "DA_Skill_Maelle_Pyrolyse", "BP_Battle_SkillScript_Pyrolyse", "Pyrolyse",
            21, "Alicia's Nightmare",
            "Deals <keyword id=\"DamageMultiplier\">7.13x</> single target <keyword id=\"Element_Fire\">Fire</> damage per hit. 3 hits.\n" +
            "Applies 30 <keyword id=\"StatusEffect_Burn\">Burn</> per hit.\n" +
            "<keyword id=\"Stance_Offensive\">Offensive Stance</>: Applies 20 more <keyword id=\"StatusEffect_Burn\">Burn</> per hit.",
            "<keyword id=\"DamageMultiplier\">7.13x</> <keyword id=\"Element_Fire\">Fire</> damage per hit. 3 hits.\n" +
            "Applies 30 <keyword id=\"StatusEffect_Burn\">Burn</> per hit.\n" +
            "<keyword id=\"Stance_Offensive\">Offensive Stance</>: Applies 20 more <keyword id=\"StatusEffect_Burn\">Burn</> per hit.",
            7.1257, 3, "Fire",
            Position: new FVector2D(250f, 1325f)),

        // Blade Waltz: Sword Ballet clone — 18 AP weapon-element, crits ×2.
        // Sword Ballet = 9 AP, 5 hits, weapon element (DynamicElementOverride —
        // cannot take a fixed element byte). Switches Offensive on cast.
        new(
            "DA_Skill_Maelle_BladeWaltz", "BP_Battle_SkillScript_BladeWaltz", "BladeWaltz",
            "DA_Skill_Maelle_SwordBallet", "BP_Battle_SkillScript_SwordBallet", "SwordBallet",
            18, "Blade Waltz",
            "Deals <keyword id=\"DamageMultiplier\">2.08x</> single target damage per hit. 5 hits.\n" +
            "Uses weapon's element.\n" +
            "<keyword id=\"CriticalHit\">Critical Hits</> deal double damage.",
            "<keyword id=\"DamageMultiplier\">2.08x</> {{DynamicElement}} damage per hit. 5 hits.\n" +
            "<keyword id=\"CriticalHit\">Critical Hits</> deal double damage.",
            2.0811, 5, null,
            Position: new FVector2D(375f, 1325f)),

        // L'Acte Final Incendiaire: Burning Canvas clone — 20 AP, retheme Fire,
        // applies 5 Burn/hit, +1% damage per Burn on target (non-consuming read).
        // Burning Canvas = 5 AP, 5 hits, Void; forces Offensive on cast.
        new(
            "DA_Skill_Maelle_LActeFinalIncendiaire", "BP_Battle_SkillScript_LActeFinalIncendiaire", "LActeFinalIncendiaire",
            "DA_Skill_Maelle_BurningCanvas", "BP_Battle_SkillScript_BurningCanvas", "BurningCanvas",
            20, "L'Acte Final Incendiaire",
            "Deals <keyword id=\"DamageMultiplier\">3.36x</> single target <keyword id=\"Element_Fire\">Fire</> damage per hit. 5 hits.\n" +
            "Applies 5 <keyword id=\"StatusEffect_Burn\">Burn</> per hit. Deal <keyword id=\"DamageMultiplier\">1%</> more damage per <keyword id=\"StatusEffect_Burn\">Burn</> on the target.",
            "<keyword id=\"DamageMultiplier\">3.36x</> <keyword id=\"Element_Fire\">Fire</> damage per hit. 5 hits.\n" +
            "Applies 5 <keyword id=\"StatusEffect_Burn\">Burn</> per hit. Deal 1% more damage per <keyword id=\"StatusEffect_Burn\">Burn</> on the target.",
            3.3632, 5, "Fire",
            Position: new FVector2D(500f, 1325f)),
    };

    private static int CreateNewMoves(string modDir)
    {
        int n = 0;
        foreach (var m in NewMoves)
            n += CreateMaelleClone(modDir, m);
        return n;
    }

    private static int CreateMaelleClone(string modDir, MaelleNewMove m)
    {
        // Sources. Donors are Maelle's own (same-character, no cross-char imports).
        var donorDa = FindTables(m.DonorDa);
        var donorBp = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), m.DonorBp + ".uasset")
            ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "extract_maelle"), m.DonorBp + ".uasset")
            ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "extracted"), m.DonorBp + ".uasset");
        var balSrc = FindTables(BalTable);
        var stSrc = FindTables(StringTable);
        var graphSrc = FindTables(GraphStem);
        var iconSrc = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), "DT_SkillIcons.uasset")
            ?? FileCopy.FindTablesAsset("DT_SkillIcons.uasset");
        if (donorDa is null || donorBp is null || balSrc is null || stSrc is null || graphSrc is null || iconSrc is null)
        {
            Console.WriteLine($"[Maelle] new-move donor missing for {m.NameId}: DA={donorDa != null} BP={donorBp != null} bal={balSrc != null} st={stSrc != null} graph={graphSrc != null} icon={iconSrc != null}");
            return 0;
        }

        var daDest = StageDest(donorDa, modDir);
        daDest = ReplaceStem(daDest, m.DonorDa, m.DaStem);
        var bpDest = StageDest(donorBp, modDir);
        bpDest = ReplaceStem(bpDest, m.DonorBp, m.BpStem);
        var balDest = StageDest(balSrc, modDir);
        var stDest = StageDest(stSrc, modDir);
        var gDest = StageDest(graphSrc, modDir);
        var iconDest = StageDest(iconSrc, modDir).Replace("/Datatables/", "/DataTables/");
        foreach (var d in new[] { daDest, bpDest, balDest, stDest, gDest, iconDest })
            Directory.CreateDirectory(Path.GetDirectoryName(d)!);

        // String table rows (ST keys are the vanilla-voice convention).
        if (!File.Exists(stDest)) FileCopy.AssetPair(stSrc, stDest);
        DataTableEditor.AddStringTableEntries(stDest, new Dictionary<string, string>
        {
            ["Maelle_" + m.NameId + "_Name"] = m.DisplayName,
            ["Maelle_" + m.NameId + "_Description_Long"] = m.Description,
            ["Maelle_" + m.NameId + "_Description_Short"] = m.ShortDescription,
        });

        // 1. DA clone.
        FileCopy.AssetPair(donorDa, daDest);
        var da = Open(daDest);
        LuneSkillsKit.RetargetPairs(da, daDest, (m.DonorDa, m.DaStem), (m.DonorStem, m.NameId));
        foreach (var ex in da.Exports.OfType<NormalExport>())
            foreach (var p in ex.Data)
            {
                if (p.Name.ToString().Equals("SkillScript", StringComparison.OrdinalIgnoreCase) && p is SoftObjectPropertyData soft)
                {
                    var pkg = "/Game/Gameplay/Battle/Skills/Content/Maelle/" + m.BpStem;
                    soft.Value = new FSoftObjectPath(new FName(da, pkg), new FName(da, m.BpStem + "_C"), null);
                    if (da.SoftObjectPathList is { Count: > 0 } list) list.Add(soft.Value);
                }
                else if (p.Name.ToString() == "Name" && p is TextPropertyData tname)
                    tname.Value = new FString("Maelle_" + m.NameId + "_Name");
                else if (p.Name.ToString() == "Description" && p is TextPropertyData tdesc)
                    tdesc.Value = new FString("Maelle_" + m.NameId + "_Description_Long");
                else if (p.Name.ToString() == "ShortDescription" && p is TextPropertyData tshort)
                    tshort.Value = new FString("Maelle_" + m.NameId + "_Description_Short");
                else if (p.Name.ToString() == "APCost" && p is IntPropertyData ap)
                    ap.Value = m.ApCost;
                else if (p.Name.ToString() == "NameID" && p is NamePropertyData nid)
                    nid.Value = FName.FromString(da, m.NameId);
                else if (p.Name.ToString() == "BaseElementalType" && p is EnumPropertyData bpe && m.Element is { } el2)
                    bpe.Value = FName.FromString(da, "EAttackType::" + StainsMechanicKit.Enumerator(el2));
            }
        da.FolderName = new FString("/Game/Gameplay/SkillTree/Content/Maelle/Skills/" + m.DaStem);
        da.Write(daDest);

        // 2. BP clone + surgical retargets (rows only; keep SEQ/FX refs intact).
        FileCopy.AssetPair(donorBp, bpDest);
        var bp = Open(bpDest);
        var pairs = new List<(string From, string To)>
        {
            (m.DonorBp, m.BpStem),
            (m.DonorStem + "_", m.NameId + "_"), // balancing-row prefix
        };
        if (m.ExtraPairs is not null) pairs.AddRange(m.ExtraPairs);
        LuneSkillsKit.RetargetPairs(bp, bpDest, pairs.ToArray());
        bp.FolderName = new FString("/Game/Gameplay/Battle/Skills/Content/Maelle/" + m.BpStem);
        // In-place element retheme (Lightning byte) — do it before Write so the clone
        // never needs a second pass (the retheme-by-stem helpers would not find the
        // brand-new clone under work/tables).
        if (m.Element is { } el)
            RethemeBpInPlace(bp, el);
        bp.Write(bpDest);

        // 3. Balancing rows: clone donor damage rows to the new prefix, set values.
        if (!File.Exists(balDest)) FileCopy.AssetPair(balSrc, balDest);
        var bal = new DataTableEditor(balDest);
        foreach (var suffix in DamageRowSuffixes(m))
        {
            var donorRow = m.DonorStem + "_" + suffix;
            var newRow = m.NameId + "_" + suffix;
            if (!bal.HasRow(donorRow)) continue;
            bal.CloneRow(donorRow, newRow);
            var val = NewMoveRowValue(m, suffix); // null = keep the cloned donor value
            if (val is double v)
                bal.Apply($"{{\"{newRow}\":{{\"FloatValue\":{v.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}}}");
        }
        bal.Save(balDest);

        // 4. Icon row: clone the donor's icon row to the new NameId.
        if (!File.Exists(iconDest)) FileCopy.AssetPair(iconSrc, iconDest);
        var icon = new DataTableEditor(iconDest);
        string donorIconRow = DonorIconRow(m);
        if (icon.HasRow(donorIconRow))
        {
            icon.CloneRow(donorIconRow, m.NameId);
            icon.Save(iconDest);
        }
        else
            Console.WriteLine($"[Maelle] icon donor row '{donorIconRow}' not found; new skill may render hidden");

        // 5. Element retheme is in-place on the clones (steps 1-2 above).

        // 6. Per-move kismet edits — operate on the clone dest directly.
        int edits = ApplyNewMoveEdits(bpDest, m);

        // 7. Floating graph node (no prereq edge).
        if (!File.Exists(gDest)) FileCopy.AssetPair(graphSrc, gDest);
        AppendFloatingGraphNode(gDest, m);

        Console.WriteLine($"[Maelle] {m.NameId} ({m.ApCost} AP, {m.Hits} hit(s)) staged");
        return 7 + edits;
    }

    /// <summary>Replace the trailing donor stem in a staged dest path with the new stem.</summary>
    private static string ReplaceStem(string stagedPath, string donorStem, string newStem)
    {
        var file = Path.GetFileNameWithoutExtension(stagedPath);
        return stagedPath.Replace(file, file.Replace(donorStem, newStem, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    private static string[] DamageRowSuffixes(MaelleNewMove m) => m.NameId switch
    {
        "LumiereAbove" => new[] { "BaseDamage", "BreakBarMultiplier" }, // no Burn rows on a Lightning clone
        "ReversFatal" => new[] { "BaseDamage", "BreakBarMultiplier", "DamageIncreasePerBurn" },
        "DeviationAbsolue" => new[] { "BaseDamage", "BreakBarMultiplier" },
        "ForThoseWhoComeAfter" => new[] { "BaseDamage", "BreakBarMultiplier", "LastHitMultiplier", "MarkedDamageMultiplier" },
        // Fire path
        "VengeanceArdente" => new[] { "BaseDamage", "BreakBarMultiplier", "DamageIncreasePerHit" },
        "AliciasNightmare" => new[] { "BaseDamage", "BreakBarMultiplier", "BurnPerHit", "BurnOnOvercharge" },
        "BladeWaltz" => new[] { "BaseDamage", "BreakBarMultiplier", "CriticalDamageMultiplier" },
        "LActeFinalIncendiaire" => new[] { "BaseDamage", "BreakBarMultiplier", "BurnAppliedPerHit", "DamageIncreasePerBurnStack" },
        _ => new[] { "BaseDamage", "BreakBarMultiplier" },
    };

    private static string DonorIconRow(MaelleNewMove m) => m.DonorStem switch
    {
        "RainOfFire" => "RainOfFire",
        "InvigoratingFire" => "InvigoratingFire",
        "Payback" => "Payback",
        "GustaveSMemoire" => "GustavesMemoire", // the DA stem is GustaveSMemoire but the icon row is "GustavesMemoire"
        _ => m.DonorStem,
    };

    /// <summary>Tuned value for a cloned row (null = keep the donor's cloned value).
    /// The BaseDamage row always takes <paramref name="m"/>.BaseDamage (the per-hit value).</summary>
    private static double? NewMoveRowValue(MaelleNewMove m, string suffix) => suffix switch
    {
        "BaseDamage" => m.BaseDamage,
        _ => m.NameId switch
        {
            "LActeFinalIncendiaire" => suffix switch
            {
                "BurnAppliedPerHit" => 5.0,
                "DamageIncreasePerBurnStack" => 0.01, // global rule: +1% per Burn
                _ => null,
            },
            "AliciasNightmare" => suffix switch
            {
                "BurnPerHit" => 30.0,
                "BurnOnOvercharge" => 20.0, // Offensive-stance bonus (Pyrolyse's +2 scaled up)
                _ => null,
            },
            _ => null,
        },
    };

    /// <summary>
    /// Appends a graph node with NO prerequisite edge (a floating node the user
    /// connects later in the Graph Editor). Node + edges are separate arrays in
    /// DA_SkillGraph_Maelle, so a node without an edge is representable. Cost is
    /// the DA APCost (mirrors the kit convention: graph UnlockCost = DA APCost).
    /// </summary>
    private static void AppendFloatingGraphNode(string gDest, MaelleNewMove m)
    {
        var g = Open(gDest);
        var gx = g.Exports.OfType<NormalExport>().First();
        var nodes = (ArrayPropertyData)gx.Data[0];
        bool already = false;
        Import templateImp = null;
        for (int i = 0; i < nodes.Value.Length; i++)
        {
            var nodeStruct = (StructPropertyData)nodes.Value[i];
            var su = (StructPropertyData)nodeStruct.Value[0];
            var skillPtr = ((ObjectPropertyData)su.Value[0]).Value;
            if (skillPtr is null || skillPtr.IsNull() || !skillPtr.IsImport()) continue;
            var impName = g.Imports[Math.Abs(skillPtr.Index) - 1].ObjectName.ToString();
            if (impName == m.DaStem) { already = true; break; }
            templateImp ??= g.Imports[Math.Abs(skillPtr.Index) - 1];
        }
        if (already) return;
        if (templateImp is null) throw new InvalidDataException("no template import in " + GraphStem);
        var daPkgPath = "/Game/Gameplay/SkillTree/Content/Maelle/Skills/" + m.DaStem;
        var newDaIdx = LuneSkillsKit.EnsureObjectImport(g, daPkgPath, m.DaStem,
            templateImp.ClassName.ToString(), templateImp.ClassPackage.ToString());

        var node = (StructPropertyData)nodes.Value[0].Clone();
        node.Value[0] = (StructPropertyData)node.Value[0].Clone();
        node.Value[1] = (StructPropertyData)node.Value[1].Clone();
        var suN = (StructPropertyData)node.Value[0];
        ((ObjectPropertyData)suN.Value[0]).Value = newDaIdx;
        ((IntPropertyData)suN.Value[1]).Value = m.ApCost;
        ((BoolPropertyData)suN.Value[2]).Value = false;
        var req = (StructPropertyData)suN.Value[3];
        ((ObjectPropertyData)req.Value[0]).Value = null;
        ((NamePropertyData)req.Value[1]).Value = FName.FromString(g, "None");
        ((BoolPropertyData)suN.Value[4]).Value = false;
        var pos = m.Position ?? FloatingPosition(g, nodes);
        var posWrap = (StructPropertyData)node.Value[1];
        ((Vector2DPropertyData)posWrap.Value[0]).Value = pos;
        nodes.Value = nodes.Value.Append(node).ToArray();
        g.Write(gDest);
        Console.WriteLine($"[Maelle] graph floating node: {m.DaStem} @ ({pos.X:0.#}, {pos.Y:0.#})");
    }

    /// <summary>Pick a free lattice spot below/right of the existing tree so floating
    /// nodes never overlap: scan the current node positions for a gap on the grid.</summary>
    private static FVector2D FloatingPosition(UAsset g, ArrayPropertyData nodes)
    {
        double x = 125, y = 1225; // well below the vanilla tree (max ~925)
        var taken = new HashSet<(double X, double Y)>();
        foreach (var n in nodes.Value)
        {
            var ns = (StructPropertyData)n;
            var posWrap = (StructPropertyData)ns.Value[1];
            var v = ((Vector2DPropertyData)posWrap.Value[0]).Value;
            taken.Add((Math.Round(v.X, 1), Math.Round(v.Y, 1)));
        }
        while (taken.Contains((x, y))) x += 125;
        return new FVector2D((float)x, (float)y);
    }

    /// <summary>Per-move kismet edits on the staged clone (dispatch by NameId).</summary>
    private static int ApplyNewMoveEdits(string bpDest, MaelleNewMove m) => m.NameId switch
    {
        "LumiereAbove" => EditLumiereAbove(bpDest, m),
        "ReversFatal" => EditReversFatal(bpDest, m),
        "DeviationAbsolue" => EditDeviationAbsolue(bpDest, m),
        "LActeFinalIncendiaire" => PatchLActeFinal(bpDest, m),
        _ => 0,
    };

    // ── Lumière Above (RainOfFire clone) ─────────────────────────────────
    // 1. Strip the per-hit Burn apply block (SpawnObject(Burn) → ActivateBuff).
    // 2. Switch forced Offensive → Defensive (SwitchStance ByteConst 1 → 2).
    // 3. Virtuose ×2: remapped Percée EqualEqual(CurrentStance, 3) after THIS
    //    BP's GetComponentByClass and before SwitchStance (the switch is Defensive,
    //    so a post-switch read never sees Virtuose). AddFinalDamageMultiplier(2.0)
    //    before DealDamages gated on that captured bool. Do not insert Percée's
    //    GetComponentByClass LetObj — unrebased clone indices crashed on use.
    private static int EditLumiereAbove(string bpDest, MaelleNewMove m)
    {
        var bp = Open(bpDest);
        var fn = bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();
        int n = 0;

        // 1. Burn-block deletion: SpawnObject(BP_BattleBuff_Burn_C) statement
        //    through the ActivateBuff statement (both inside the per-target loop).
        int burnStart = -1, burnEnd = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hasSpawn = false, hasBuff = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_LetObj lo && lo.AssignmentExpression is EX_FinalFunction ff
                    && FnImportName(ff, bp) == "SpawnObject") hasSpawn = true;
                if (e is EX_ObjectConst oc && oc.Value.IsImport()
                    && bp.Imports[Math.Abs(oc.Value.Index) - 1].ObjectName.ToString() == "BP_BattleBuff_Burn_C") hasBuff = true;
            });
            if (hasSpawn && hasBuff) { burnStart = i; break; }
        }
        if (burnStart < 0)
        {
            Console.WriteLine("[Maelle] LumiereAbove: burn SpawnObject not found; burn strip skipped");
        }
        else
        {
            // The block runs until the ActivateBuff statement (a Context whose call
            // is ActivateBuff, right before the per-target EX_PopExecutionFlow).
            for (int i = burnStart; i < stmts.Count; i++)
            {
                bool hasAct = false;
                MayhemFixKit.Walk(new[] { stmts[i] }, e =>
                {
                    if (e is EX_Context ctx && ctx.ContextExpression is EX_VirtualFunction vf
                        && vf.VirtualFunctionName.ToString() == "ActivateBuff") hasAct = true;
                });
                if (hasAct) { burnEnd = i; break; }
            }
            if (burnEnd < 0) burnEnd = burnStart; // fallback: remove only the spawn
            uint removedSize = 0, removedOffset = 0;
            for (int i = 0; i < burnStart; i++) removedOffset += (uint)stmts[i].GetSize(bp);
            for (int i = burnStart; i <= burnEnd; i++) removedSize += (uint)stmts[i].GetSize(bp);
            LuneSkillsKit.ShiftAbsoluteSigned(bp, fn, removedOffset, removedSize);
            stmts.RemoveRange(burnStart, burnEnd - burnStart + 1);
            fn.ScriptBytecode = stmts.ToArray();
            MayhemFixKit.RecalcContextSkips(bp, fn);
            n++;
            Console.WriteLine($"[Maelle] LumiereAbove: stripped burn block ({burnEnd - burnStart + 1} stmts)");
        }

        // 2. Offensive → Defensive stance byte (SwitchStance ByteConst 1 → 2).
        bool stanceDone = false;
        foreach (var s in fn.ScriptBytecode)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (stanceDone || e is not EX_Context ctx) return;
                if (ctx.ContextExpression is not EX_VirtualFunction vf || vf.VirtualFunctionName.ToString() != "SwitchStance") return;
                foreach (var p in vf.Parameters)
                    if (p is EX_ByteConst bc && bc.Value == 1) { bc.Value = 2; stanceDone = true; }
            });
        if (stanceDone)
        {
            MayhemFixKit.RecalcContextSkips(bp, fn);
            n++;
            Console.WriteLine("[Maelle] LumiereAbove: stance switch Offensive -> Defensive");
        }

        // 3. Virtuose ×2 from THIS asset's GetComponentByClass + a remapped
        //    CurrentStance InstVar. Snapshot BEFORE SwitchStance, and only
        //    SwitchStance on the last hit so both sequence notifies keep Virtuose.
        stmts = fn.ScriptBytecode.ToList();
        var virtLet = BuildInAssetVirtuoseLet(bp, fn);
        int compIdx = IndexOfGetComponentLet(bp, fn);
        if (virtLet is null || compIdx < 0)
        {
            Console.WriteLine("[Maelle] LumiereAbove: in-asset Virtuose gate unavailable; x2 splice skipped");
        }
        else
        {
            InsertStmts(bp, fn, ref stmts, compIdx + 1, virtLet);
            n++;
            Console.WriteLine("[Maelle] LumiereAbove: Virtuose snapshot after GetComponentByClass");

            int switchIdx = IndexOfSwitchStance(fn);
            var isLast = FindIsLastActionEffectLocal(fn);
            if (switchIdx >= 0 && isLast is not null)
            {
                uint swStart = 0;
                for (int i = 0; i < switchIdx; i++) swStart += (uint)stmts[i].GetSize(bp);
                var skipSwitch = new EX_JumpIfNot
                {
                    BooleanExpression = MayhemFixKit.CloneKismet(isLast),
                    CodeOffset = 0,
                };
                skipSwitch.CodeOffset = swStart + (uint)skipSwitch.GetSize(bp) + (uint)stmts[switchIdx].GetSize(bp);
                InsertStmts(bp, fn, ref stmts, switchIdx, skipSwitch);
                n++;
                Console.WriteLine("[Maelle] LumiereAbove: SwitchStance delayed to last hit");
            }

            int dealIdx = IndexOfDealDamages(fn);
            EX_Context? dealCtx = null;
            if (dealIdx >= 0)
                MayhemFixKit.Walk(new[] { stmts[dealIdx] }, e =>
                {
                    if (dealCtx is null && e is EX_Context ctx && ctx.ContextExpression is EX_VirtualFunction vf
                        && vf.VirtualFunctionName.ToString() == "DealDamages")
                        dealCtx = ctx;
                });
            if (dealIdx < 0 || dealCtx is null)
            {
                Console.WriteLine("[Maelle] LumiereAbove: DealDamages not found; x2 splice skipped");
            }
            else
            {
                var addFinal = (EX_Context)MayhemFixKit.CloneKismet(dealCtx);
                addFinal.ContextExpression = new EX_VirtualFunction
                {
                    VirtualFunctionName = FName.FromString(bp, "AddFinalDamageMultiplier"),
                    Parameters = new KismetExpression[] { new EX_DoubleConst { Value = 2.0 } },
                };
                addFinal.RValuePointer = new KismetPropertyPointer(new FFieldPath());
                var jump2 = new EX_JumpIfNot
                {
                    CodeOffset = 0,
                    BooleanExpression = MayhemFixKit.CloneKismet(virtLet.VariableExpression),
                };
                uint insertOffset = 0;
                for (int i = 0; i < dealIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
                jump2.CodeOffset = insertOffset + (uint)jump2.GetSize(bp) + (uint)addFinal.GetSize(bp);
                InsertStmts(bp, fn, ref stmts, dealIdx, jump2, addFinal);
                n++;
                Console.WriteLine("[Maelle] LumiereAbove: Virtuose x2 splice before DealDamages");
            }
        }
        if (n > 0) bp.Write(bpDest);
        return n;
    }

    /// <summary>
    /// Re-derives every EX_PushExecutionFlow resume address from the statement flow:
    /// a Push resumes at the statement right after the matching Pop/PopIfNot (or at the
    /// function's final EX_Return when the Pop is the implicit end-of-function pop the
    /// writer emits after the last top-level statement). Statement indices are mapped to
    /// byte offsets, so every resume target lands exactly on a statement start.
    /// </summary>
    private static void RepairPushFlow(UAsset bp, FunctionExport fn)
    {
        if (fn.ScriptBytecode is null) return;
        var stmts = fn.ScriptBytecode.ToList();
        // Byte offset of each statement start.
        var starts = new List<uint>();
        uint pos = 0;
        foreach (var s in stmts)
        {
            starts.Add(pos);
            pos += (uint)s.GetSize(bp);
        }
        // Simulate the push/pop nesting. A PopExecutionFlowIfNot pops only when its
        // condition is true and otherwise continues; but for resume-address purposes the
        // Push's saved resume is the statement after the pop that closes it.
        var stack = new Stack<int>(); // statement indices of unmatched pushes
        var resumeAt = new Dictionary<int, int>(); // push stmt index -> resume stmt index
        for (int i = 0; i < stmts.Count; i++)
        {
            var s = stmts[i];
            if (s is EX_PushExecutionFlow) stack.Push(i);
            else if (s is EX_PopExecutionFlow or EX_PopExecutionFlowIfNot)
            {
                if (stack.Count > 0)
                {
                    var pushIdx = stack.Pop();
                    if (!resumeAt.ContainsKey(pushIdx)) resumeAt[pushIdx] = i + 1;
                }
            }
        }
        // A push left on the stack resumes at the function's final EX_Return statement.
        while (stack.Count > 0)
        {
            var pushIdx = stack.Pop();
            if (resumeAt.ContainsKey(pushIdx)) continue;
            int retIdx = stmts.Count - 1;
            for (int i = stmts.Count - 1; i >= 0; i--)
                if (stmts[i] is EX_Return) { retIdx = i; break; }
            resumeAt[pushIdx] = retIdx;
        }
        // Rewrite each push's resume address to the start of its resume statement.
        for (int i = 0; i < stmts.Count; i++)
        {
            if (stmts[i] is not EX_PushExecutionFlow pf) continue;
            if (!resumeAt.TryGetValue(i, out var targetStmt)) continue;
            uint target = starts[Math.Min(targetStmt, starts.Count - 1)];
            pf.PushingAddress = target;
        }
    }

    // ── Revers Fatal (Thermal Arc / InvigoratingFire clone) ──────────────
    // 1. Remove the forced Offensive SwitchStance (so the move does not yank
    //    Maelle out of Virtuose before the gate runs).
    // 2. Raise the BurnToConsume cap (the Min(burnCount, N) literal) 10 → 10000.
    // 3. Virtuose-gated Greater Slow + Greater Defenceless apply, spliced right
    //    before the burn-consume / damage region.
    private static int EditReversFatal(string bpDest, MaelleNewMove m)
    {
        var bp = Open(bpDest);
        var fn = bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();
        int n = 0;

        // Snapshot the Virtuose LetBool WHILE SwitchStance still exists so
        // BuildInAssetVirtuoseLet can clone that Context as the CurrentStance
        // read shell (synthesizing the Context after the strip is a VM hazard).
        var virtLet = BuildInAssetVirtuoseLet(bp, fn);

        // 1. Strip the forced SwitchStance statement entirely (RainOfFire-family
        //    donors force Offensive on cast; Revers Fatal must not).
        int stanceIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool has = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (has) return;
                if (e is EX_Context ctx && ctx.ContextExpression is EX_VirtualFunction vf
                    && vf.VirtualFunctionName.ToString() == "SwitchStance") has = true;
            });
            if (has) { stanceIdx = i; break; }
        }
        if (stanceIdx >= 0)
        {
            uint removedOffset = 0;
            for (int i = 0; i < stanceIdx; i++) removedOffset += (uint)stmts[i].GetSize(bp);
            uint removedSize = (uint)stmts[stanceIdx].GetSize(bp);
            // Shift while SwitchStance is still in fn.ScriptBytecode so every jump/push
            // that pointed AFTER it moves back by removedSize. RemapFlowOffsetsByOrdinal
            // is wrong for deletions (ordinal N after a delete is old N+1) — that made
            // the QTE Pop resume skip the second-loop counter init and freeze on hit 1.
            LuneSkillsKit.ShiftAbsoluteSigned(bp, fn, removedOffset, removedSize);
            stmts.RemoveAt(stanceIdx);
            fn.ScriptBytecode = stmts.ToArray();
            MayhemFixKit.RecalcContextSkips(bp, fn);
            n++;
            Console.WriteLine("[Maelle] ReversFatal: forced SwitchStance removed");
        }

        // 2. BurnToConsume cap: BurnToConsume = Min(GetActiveBuffCounts(Burn), 10)
        //    lives in ExecuteUbergraph (the per-target consume loop), not
        //    OnActionEffect. OnActionEffect only reads the InstVar. Walk every
        //    function the way PatchThermalArcBurnCap does.
        int cap = 0;
        foreach (var capFn in bp.Exports.OfType<FunctionExport>())
        {
            if (capFn.ScriptBytecode is null) continue;
            bool changed = false;
            foreach (var s in capFn.ScriptBytecode)
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (e is not EX_FinalFunction ff || FnImportName(ff, bp) != "Min") return;
                    foreach (var p in ff.Parameters)
                        if (p is EX_IntConst ic && ic.Value == 10)
                        { ic.Value = 10000; changed = true; cap = 10000; }
                });
            if (changed) MayhemFixKit.RecalcContextSkips(bp, capFn);
        }
        if (cap > 0)
        {
            n++;
            Console.WriteLine("[Maelle] ReversFatal: BurnToConsume cap 10 -> 10000");
        }
        else
            Console.WriteLine("[Maelle] ReversFatal: Min(burn, 10) not found; cap unchanged");

        // 3. Virtuose-gated Greater Slow + Greater Defenceless.
        n += SpliceVirtuoseStatuses(bp, fn, virtLet);

        if (n > 0) bp.Write(bpDest);
        return n;
    }

    // ── Déviation Absolue (Payback clone) ─────────────────────────────────
    // GetSkillCostOverride currently computes Clamp(base − parries, 0, 9).
    // Vanilla Payback is a 9 AP skill, so the Clamp max of 9 is the skill's own
    // cap. Déviation Absolue is 19 AP: leaving max=9 hides every −2 step until
    // 2·parries ≥ 10 (cost stuck at 9). The design wants −2 AP per parry:
    // Subtract(base, Multiply(parries, 2)) and Clamp(..., 0, APCost).
    private static int EditDeviationAbsolue(string bpDest, MaelleNewMove m)
    {
        var bp = Open(bpDest);
        var fn = bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "GetSkillCostOverride");
        if (fn?.ScriptBytecode is null) return 0;

        // Find the Subtract(base, parries) call + the statement that contains it.
        var stmts = fn.ScriptBytecode.ToList();
        int subStmt = -1;
        EX_FinalFunction? sub = null;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool done = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (done || sub is not null) return;
                if (e is EX_FinalFunction ff && FnImportName(ff, bp) == "Subtract_IntInt"
                    && ff.Parameters.Length >= 2 && ff.Parameters[1] is EX_InstanceVariable)
                { sub = ff; done = true; }
            });
            if (sub is not null) { subStmt = i; break; }
        }
        if (sub is null)
        {
            Console.WriteLine("[Maelle] DeviationAbsolue: Subtract(base, parries) not found");
            return 0;
        }

        // Replace the parries operand with Multiply_IntInt(parries, 2). Measure the
        // statement's start + size BEFORE mutating so later flow offsets can be shifted
        // by the delta (the stored offsets still reflect the pre-mutation layout).
        uint oldStart = 0;
        for (int i = 0; i < subStmt; i++) oldStart += (uint)stmts[i].GetSize(bp);
        uint oldSize = (uint)stmts[subStmt].GetSize(bp);
        var parries = (KismetExpression)MayhemFixKit.CloneKismet(sub.Parameters[1]);
        var multFn = EnsureEngineFunction(bp, "KismetMathLibrary", "Multiply_IntInt");
        var mult = new EX_FinalFunction
        {
            StackNode = multFn,
            Parameters = new KismetExpression[] { parries, new EX_IntConst { Value = 2 } },
        };
        sub.Parameters[1] = mult;
        uint newSize = (uint)stmts[subStmt].GetSize(bp);

        // Sizes of surrounding statements shift, so advance every flow offset that pointed
        // at-or-after this statement by the delta (ShiftAbsolute semantics).
        RecomputeFlowOffsets(bp, fn, oldStart, (int)(newSize - oldSize));

        // Raise the vanilla Payback Clamp max (9) to this skill's AP cost. Same-size
        // IntConst rewrite — no further flow shift.
        int clampMax = 0;
        foreach (var s in fn.ScriptBytecode)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (clampMax != 0 || e is not EX_FinalFunction cf) return;
                if (FnImportName(cf, bp) != "Clamp" || cf.Parameters.Length < 3) return;
                if (cf.Parameters[2] is EX_IntConst max && max.Value == 9)
                { max.Value = m.ApCost; clampMax = m.ApCost; }
            });
        if (clampMax == 0)
            Console.WriteLine("[Maelle] DeviationAbsolue: Clamp max 9 not found; left unchanged");

        MayhemFixKit.RecalcContextSkips(bp, fn);
        bp.Write(bpDest);
        Console.WriteLine($"[Maelle] DeviationAbsolue: cost override now Clamp(base - 2x parries, 0, {m.ApCost})");
        return 1;
    }

    /// <summary>True when the final function's import name matches (reads the BP import).</summary>
    private static string FnImportName(EX_FinalFunction ff, UAsset bp)
    {
        if (ff.StackNode is { } sn && sn.IsImport())
            return bp.Imports[Math.Abs(sn.Index) - 1].ObjectName.ToString();
        return "";
    }

    /// <summary>
    /// After an in-place expression mutation changes a statement's serialized size,
    /// every absolute flow offset (jump/push target) that pointed at-or-after the
    /// mutation must advance by the same delta. Statement ORDER is unchanged. Call this
    /// with the mutation statement's OLD start offset (pre-mutation layout) and the size
    /// delta; the stored offsets still reflect the pre-mutation layout, so ShiftAbsolute
    /// advances every target at-or-after the mutation point correctly.
    /// </summary>
    private static void RecomputeFlowOffsets(UAsset bp, FunctionExport fn, uint oldStmtStart, int delta)
    {
        if (delta == 0) return;
        if (delta > 0) MayhemFixKit.ShiftAbsolute(bp, fn, oldStmtStart, (uint)delta, null);
        else LuneSkillsKit.ShiftAbsoluteSigned(bp, fn, oldStmtStart, (uint)(-delta));
    }

    private static void InsertStmts(UAsset bp, FunctionExport fn, ref List<KismetExpression> stmts,
        int idx, params KismetExpression[] extra)
    {
        stmts = fn.ScriptBytecode!.ToList();
        uint insertOffset = 0;
        for (int i = 0; i < idx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
        uint blockSize = (uint)extra.Sum(b => (long)b.GetSize(bp));
        ShiftOffsetsAtOrAfter(bp, fn, insertOffset, blockSize);
        stmts.InsertRange(idx, extra);
        fn.ScriptBytecode = stmts.ToArray();
        MayhemFixKit.RecalcContextSkips(bp, fn);
    }

    private static int IndexOfGetComponentLet(UAsset bp, FunctionExport fn)
    {
        var stmts = fn.ScriptBytecode;
        if (stmts is null) return -1;
        for (int i = 0; i < stmts.Length; i++)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_FinalFunction ff && FnImportName(ff, bp) == "GetComponentByClass") hit = true;
            });
            if (hit) return i;
        }
        return -1;
    }

    private static bool IsStanceComponentLet(UAsset bp, FunctionExport fn, int idx)
    {
        if (fn.ScriptBytecode is null || idx < 0 || idx >= fn.ScriptBytecode.Length) return false;
        bool isStance = false;
        MayhemFixKit.Walk(new[] { fn.ScriptBytecode[idx] }, e =>
        {
            if (isStance || e is not EX_ObjectConst oc || !oc.Value.IsImport()) return;
            int imp = Math.Abs(oc.Value.Index) - 1;
            if (imp >= 0 && imp < bp.Imports.Count
                && bp.Imports[imp].ObjectName.ToString() == "BP_StanceMechanicComponent_C")
                isStance = true;
        });
        return isStance;
    }

    private static int IndexOfSwitchStance(FunctionExport fn)
    {
        var stmts = fn.ScriptBytecode;
        if (stmts is null) return -1;
        for (int i = 0; i < stmts.Length; i++)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "SwitchStance") hit = true;
            });
            if (hit) return i;
        }
        return -1;
    }

    private static int IndexOfDealDamages(FunctionExport fn)
    {
        var stmts = fn.ScriptBytecode;
        if (stmts is null) return -1;
        for (int i = 0; i < stmts.Length; i++)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "DealDamages") hit = true;
            });
            if (hit) return i;
        }
        return -1;
    }

    private static EX_LetObj? FindGetComponentLet(UAsset bp, FunctionExport fn)
    {
        int i = IndexOfGetComponentLet(bp, fn);
        if (i < 0 || fn.ScriptBytecode is null) return null;
        return fn.ScriptBytecode[i] as EX_LetObj;
    }

    private static KismetExpression? FindIsLastActionEffectLocal(FunctionExport fn)
    {
        if (fn.ScriptBytecode is null) return null;
        KismetExpression? found = null;
        foreach (var s in fn.ScriptBytecode)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (found is not null || e is not EX_LetBool lb) return;
                if (lb.VariableExpression is EX_LocalVariable lv
                    && PtrName(lv.Variable) == "CallFunc_IsLastActionEffectOfSkill_ReturnValue")
                    found = lb.VariableExpression;
            });
        return found;
    }

    private static FPackageIndex EnsureEqualEqualByteByte(UAsset asset)
    {
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (imp.ObjectName.ToString() != "EqualEqual_ByteByte") continue;
            if (imp.OuterIndex.IsImport()
                && asset.Imports[Math.Abs(imp.OuterIndex.Index) - 1].ObjectName.ToString() == "KismetMathLibrary")
                return FPackageIndex.FromImport(i);
        }
        int tmpl = -1;
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (!imp.OuterIndex.IsImport()) continue;
            if (asset.Imports[Math.Abs(imp.OuterIndex.Index) - 1].ObjectName.ToString() != "KismetMathLibrary") continue;
            if (imp.ClassName.ToString() != "Function") continue;
            tmpl = i;
            break;
        }
        if (tmpl < 0) return EnsureEngineFunction(asset, "KismetMathLibrary", "EqualEqual_ByteByte");
        var t = asset.Imports[tmpl];
        return asset.AddImport(new Import(
            t.ClassPackage.ToString(), t.ClassName.ToString(), t.OuterIndex,
            "EqualEqual_ByteByte", false, asset));
    }

    private static EX_InstanceVariable? CloneCurrentStanceInstVar(UAsset dest)
    {
        var perCe = FindTables("BP_Battle_SkillScript_New14_Percee");
        if (perCe is null) return null;
        var pc = Open(perCe);
        EX_InstanceVariable? found = null;
        foreach (var cand in pc.Exports.OfType<FunctionExport>())
        {
            if (cand.ScriptBytecode is null) continue;
            MayhemFixKit.Walk(cand.ScriptBytecode, e =>
            {
                if (found is null && e is EX_InstanceVariable iv && PtrName(iv.Variable) == "CurrentStance")
                    found = iv;
            });
            if (found is not null) break;
        }
        if (found is null) return null;
        var clone = (EX_InstanceVariable)MayhemFixKit.CloneKismet(found);
        FPackageIndex owner = FPackageIndex.FromRawIndex(0);
        for (int k = 0; k < dest.Imports.Count; k++)
            if (dest.Imports[k].ObjectName.ToString() == "BP_StanceMechanicComponent_C")
            { owner = FPackageIndex.FromImport(k); break; }
        if (clone.Variable?.New is { } np)
        {
            var path = (np.Path ?? Array.Empty<FName>())
                .Select(n => FName.FromString(dest, n.ToString())).ToArray();
            clone.Variable = new KismetPropertyPointer(new FFieldPath(path, owner));
        }
        return clone;
    }

    private static EX_LetBool? BuildInAssetVirtuoseLet(UAsset bp, FunctionExport fn,
        string localName = "CallFunc_EqualEqual_ByteByte_ReturnValue")
    {
        var getComp = FindGetComponentLet(bp, fn);
        if (getComp?.VariableExpression is not EX_LocalVariable compLocal)
        {
            Console.WriteLine("[Maelle] no in-asset GetComponentByClass local; Virtuose gate skipped");
            return null;
        }
        var stanceIv = CloneCurrentStanceInstVar(bp);
        if (stanceIv is null)
        {
            Console.WriteLine("[Maelle] CurrentStance InstVar unavailable; Virtuose gate skipped");
            return null;
        }

        EX_Context? switchCtx = null;
        if (fn.ScriptBytecode is not null)
            foreach (var s in fn.ScriptBytecode)
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (switchCtx is null && e is EX_Context ctx && ctx.ContextExpression is EX_VirtualFunction vf
                        && vf.VirtualFunctionName.ToString() == "SwitchStance")
                        switchCtx = ctx;
                });

        EX_Context readStance;
        if (switchCtx is not null)
        {
            readStance = (EX_Context)MayhemFixKit.CloneKismet(switchCtx);
            readStance.ObjectExpression = (EX_LocalVariable)MayhemFixKit.CloneKismet(compLocal);
            readStance.ContextExpression = stanceIv;
            // SwitchStance's RValuePointer is the out-stance local; a property read
            // must point at CurrentStance or the VM follows the wrong field.
            readStance.RValuePointer = stanceIv.Variable ?? new KismetPropertyPointer(new FFieldPath());
        }
        else
        {
            readStance = new EX_Context
            {
                ObjectExpression = (EX_LocalVariable)MayhemFixKit.CloneKismet(compLocal),
                ContextExpression = stanceIv,
                RValuePointer = stanceIv.Variable ?? new KismetPropertyPointer(new FFieldPath()),
            };
        }

        var destOwner = FPackageIndex.FromExport(bp.Exports.IndexOf(fn));
        var have = new HashSet<string>(
            (fn.LoadedProperties ?? Array.Empty<FProperty>()).Select(p => p.Name.ToString()),
            StringComparer.Ordinal);
        var boolTpl = (fn.LoadedProperties ?? Array.Empty<FProperty>()).FirstOrDefault(p => p is FBoolProperty);
        if (boolTpl is not null && !have.Contains(localName))
        {
            var prop = (FProperty)ShallowClone(boolTpl);
            prop.Name = FName.FromString(bp, localName);
            fn.LoadedProperties = (fn.LoadedProperties ?? Array.Empty<FProperty>()).Concat(new[] { prop }).ToArray();
        }
        var boolLocal = new EX_LocalVariable
        {
            Variable = new KismetPropertyPointer(new FFieldPath(
                new[] { FName.FromString(bp, localName) }, destOwner)),
        };
        return new EX_LetBool
        {
            VariableExpression = boolLocal,
            AssignmentExpression = new EX_FinalFunction
            {
                StackNode = EnsureEqualEqualByteByte(bp),
                Parameters = new KismetExpression[] { readStance, new EX_ByteConst { Value = 3 } },
            },
        };
    }

    /// <summary>
    /// Clones Percée's proven Virtuose gate into <paramref name="fn"/> of <paramref name="bp"/>:
    ///   [LetObj comp = Context(CurrentCharacter).GetComponentByClass(StanceMechanic)]
    ///   [LetBool isVirtuose = EqualEqual_ByteByte(Context(comp).CurrentStance, ByteConst 3)]
    /// All cross-BP hazards are handled: FNames are interned in the dest name map,
    /// FinalFunction/ObjectConst import indices are remapped by name (EqualEqual_ByteByte
    /// via EnsureKismetMathFn), locals are retargeted to this function's export, missing
    /// local FProperties are ShallowCloned from this BP's OWN same-type properties, and
    /// InstVar ResolvedOwners are remapped to the dest import of the same class (CurrentStance
    /// → StanceMechanic, CurrentCharacter → BP_Battle_SkillScript_C — never the dest class
    /// export). Returns the two cloned statements (the caller gates its own behavior on the LetBool).
    /// </summary>
    private static (EX_LetObj Comp, EX_LetBool Eq)? ClonePerceeVirtuoseGate(UAsset bp, FunctionExport fn)
    {
        var destComp = FindGetComponentLet(bp, fn);
        var destEq = BuildInAssetVirtuoseLet(bp, fn);
        if (destComp is not null && destEq is not null)
            return ((EX_LetObj)MayhemFixKit.CloneKismet(destComp), destEq);

        var perCe = FindTables("BP_Battle_SkillScript_New14_Percee");
        if (perCe is null) { Console.WriteLine("[Maelle] Percee donor missing; Virtuose gate skipped"); return null; }
        var pc = Open(perCe);
        var pcFn = pc.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (pcFn?.ScriptBytecode is null) { Console.WriteLine("[Maelle] Percee kismet missing; Virtuose gate skipped"); return null; }

        // Find Percée's [LetObj GetComponentByClass][LetBool EqualEqual ... ByteConst 3] pair.
        EX_LetObj? gateCompTpl = null;
        EX_LetBool? gateEqTpl = null;
        var pcStmts = pcFn.ScriptBytecode.ToList();
        for (int i = 0; i < pcStmts.Count - 1; i++)
        {
            EX_LetObj? lo = null;
            bool writesComp = false;
            MayhemFixKit.Walk(new[] { pcStmts[i] }, e =>
            {
                if (e is EX_LetObj l && l.AssignmentExpression is { } asg)
                {
                    bool isComp = false;
                    MayhemFixKit.Walk(new[] { asg }, e2 =>
                    {
                        if (isComp) return;
                        if (e2 is EX_FinalFunction ff && FnImportName(ff, pc) == "GetComponentByClass") isComp = true;
                    });
                    if (isComp) lo = l;
                }
                if (e is EX_LocalVariable lv && PtrName(lv.Variable) == "CallFunc_GetComponentByClass_ReturnValue") writesComp = true;
            });
            if (lo is null || !writesComp) continue;
            EX_LetBool? eq = null;
            bool isGate = false;
            MayhemFixKit.Walk(new[] { pcStmts[i + 1] }, e =>
            {
                if (e is EX_LetBool lb && lb.AssignmentExpression is EX_FinalFunction ff
                    && FnImportName(ff, pc) == "EqualEqual_ByteByte") eq = lb;
                if (e is EX_ByteConst b && b.Value == 3) isGate = true;
            });
            if (eq is not null && isGate) { gateCompTpl = lo; gateEqTpl = eq; break; }
        }
        if (gateCompTpl is null || gateEqTpl is null) { Console.WriteLine("[Maelle] Percee Virtuose gate not found"); return null; }

        // Local handling: NEVER copy FProperty instances across BPs (repo-proven corruption).
        // Missing locals are ShallowCloned from THIS BP's own same-type FProperties.
        var destOwner = FPackageIndex.FromExport(bp.Exports.IndexOf(fn));
        var classExport = bp.Exports.OfType<ClassExport>().FirstOrDefault();
        var classOwner = FPackageIndex.FromExport(classExport is not null
            ? bp.Exports.IndexOf(classExport)
            : bp.Exports.IndexOf(fn));
        var have = new HashSet<string>(
            (fn.LoadedProperties ?? Array.Empty<FProperty>()).Select(p => p.Name.ToString()),
            StringComparer.Ordinal);
        var boolTpl = (fn.LoadedProperties ?? Array.Empty<FProperty>()).FirstOrDefault(p => p is FBoolProperty);
        var objTpl = (fn.LoadedProperties ?? Array.Empty<FProperty>()).FirstOrDefault(p => p is FObjectProperty);
        void EnsureLocal(string name, FProperty? template)
        {
            if (have.Contains(name) || template is null) return;
            var prop = (FProperty)ShallowClone(template);
            prop.Name = FName.FromString(bp, name);
            fn.LoadedProperties = (fn.LoadedProperties ?? Array.Empty<FProperty>()).Concat(new[] { prop }).ToArray();
            have.Add(name);
        }
        EnsureLocal("CallFunc_GetComponentByClass_ReturnValue", objTpl);
        EnsureLocal("CallFunc_EqualEqual_ByteByte_ReturnValue", boolTpl);

        KismetExpression Retarget(KismetExpression src)
        {
            var clone = MayhemFixKit.CloneKismet(src);
            var seen = new HashSet<KismetExpression>();
            MayhemFixKit.Walk(new[] { clone }, e =>
            {
                if (!seen.Add(e)) return;
                if (e is EX_FinalFunction ff && ff.StackNode is { } sn && sn.IsImport())
                    ff.StackNode = MapImport(pc, bp, sn);
                if (e is EX_ObjectConst oc && oc.Value.IsImport())
                    oc.Value = MapImport(pc, bp, oc.Value);
                if (e is EX_VirtualFunction vf)
                    vf.VirtualFunctionName = FName.FromString(bp, vf.VirtualFunctionName.ToString());
                if (e is EX_NameConst nc)
                    nc.Value = FName.FromString(bp, nc.Value.ToString());
                if (e is EX_LocalVariable lv) RebasePtrFromSource(pc, bp, lv.Variable, destOwner, classOwner);
                if (e is EX_LocalOutVariable ov) RebasePtrFromSource(pc, bp, ov.Variable, destOwner, classOwner);
                if (e is EX_InstanceVariable iv) RebasePtrFromSource(pc, bp, iv.Variable, destOwner, classOwner);
                if (e is EX_Context ctx && ctx.RValuePointer is { } rvp)
                    RebasePtrFromSource(pc, bp, rvp, destOwner, classOwner);
            });
            return clone;
        }
        return ((EX_LetObj)Retarget(gateCompTpl), (EX_LetBool)Retarget(gateEqTpl));
    }

    /// <summary>
    /// Revers Fatal: splice a Virtuose-gated Greater Slow + Greater Defenceless
    /// application into the per-target loop, just before the burn-consume.
    ///
    /// STRATEGY (proven shapes only):
    ///  • The Virtuose gate is Percée's proven LetObj(GetComponentByClass) +
    ///    LetBool(EqualEqual_ByteByte(Context(comp).CurrentStance, 3)) pair, cloned via
    ///    <see cref="ClonePerceeVirtuoseGate"/> (all cross-BP hazards handled there).
    ///  • The status applies are synthesized bare-ApplyBuff statements (GuardDown's proven
    ///    shape) targeting THIS BP's own per-target stats context, with a ShallowCloned
    ///    same-BP out-local.
    ///
    /// If any clone source is missing the method bails with a log (no partial write).
    /// </summary>
    private static int SpliceVirtuoseStatuses(UAsset bp, FunctionExport fn, EX_LetBool? virtLet)
    {
        var stmts = fn.ScriptBytecode.ToList();

        // The per-target burn-consume statement (TryRemoveBuffStacks) marks the spot:
        // insert the status block before it (inside the per-target push).
        int consumeIdx = -1;
        EX_Context? consumeCtx = null;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool has = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (has) return;
                if (e is EX_Context ctx && ctx.ContextExpression is EX_VirtualFunction vf
                    && vf.VirtualFunctionName.ToString() == "TryRemoveBuffStacks") { has = true; consumeCtx = ctx; }
            });
            if (has) { consumeIdx = i; break; }
        }
        if (consumeIdx < 0 || consumeCtx is null) { Console.WriteLine("[Maelle] ReversFatal: burn-consume not found; statuses skipped"); return 0; }

        virtLet ??= BuildInAssetVirtuoseLet(bp, fn);
        if (virtLet is null) return 0;

        // TryRemoveBuffStacks is called on stats.BattleBuffComponent. GuardDown's
        // ApplyBuff takes the stats object (Context(Array_Get).AC_jRPG_CharacterStats).
        // Passing BattleBuffComponent as that argument crashes the VM on use.
        var targetStats = consumeCtx.ObjectExpression;
        if (targetStats is EX_Context buffCtx
            && buffCtx.ContextExpression is EX_InstanceVariable biv
            && PtrName(biv.Variable) == "BattleBuffComponent"
            && buffCtx.ObjectExpression is { } stats)
            targetStats = stats;

        var destOwner = FPackageIndex.FromExport(bp.Exports.IndexOf(fn));
        var have = new HashSet<string>(
            (fn.LoadedProperties ?? Array.Empty<FProperty>()).Select(p => p.Name.ToString()),
            StringComparer.Ordinal);
        var objTpl = (fn.LoadedProperties ?? Array.Empty<FProperty>()).FirstOrDefault(p => p is FObjectProperty);
        if (objTpl is not null && !have.Contains("CallFunc_ApplyBuff_CreatedBuffInstance"))
        {
            var prop = (FProperty)ShallowClone(objTpl);
            prop.Name = FName.FromString(bp, "CallFunc_ApplyBuff_CreatedBuffInstance");
            fn.LoadedProperties = (fn.LoadedProperties ?? Array.Empty<FProperty>()).Concat(new[] { prop }).ToArray();
        }
        // Out-local pointer: clone a same-BP object local (never new FFieldPath).
        EX_LocalVariable? objLocalTpl = null;
        var getComp = FindGetComponentLet(bp, fn);
        if (getComp?.VariableExpression is EX_LocalVariable glv) objLocalTpl = glv;
        if (objLocalTpl is null)
            MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
            {
                if (objLocalTpl is null && e is EX_LocalVariable lv && PtrName(lv.Variable).Length > 0)
                    objLocalTpl = lv;
            });
        if (objLocalTpl is null) { Console.WriteLine("[Maelle] ReversFatal: no dest object local for ApplyBuff out"); return 0; }
        var outLocal = (EX_LocalVariable)MayhemFixKit.CloneKismet(objLocalTpl);
        if (outLocal.Variable?.New is { } np)
            outLocal.Variable = new KismetPropertyPointer(new FFieldPath(
                new[] { FName.FromString(bp, "CallFunc_ApplyBuff_CreatedBuffInstance") }, destOwner));

        var classFor = new (string Cls, string Pkg)[]
        {
            ("BP_BattleBuff_GreaterSlow_C", "/Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_GreaterSlow"),
            ("BP_BattleBuff_GreaterDefenseless_C", "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_GreaterDefenseless"),
        };
        int turns = 3;
        var applies = new List<KismetExpression>();
        foreach (var (cls, pkg) in classFor)
        {
            var imp = LuneSkillsKit.EnsureObjectImport(bp, pkg, cls, cls, "/Script/Engine");
            // GuardDown uses EX_LocalVirtualFunction ApplyBuff (parent-class UFunction).
            applies.Add(new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(bp, "ApplyBuff"),
                Parameters = new KismetExpression[]
                {
                    new EX_ObjectConst { Value = imp },
                    (KismetExpression)MayhemFixKit.CloneKismet(targetStats),
                    new EX_IntConst { Value = turns },
                    new EX_NoObject(),
                    new EX_ByteConst { Value = 4 },
                    (EX_LocalVariable)MayhemFixKit.CloneKismet(outLocal),
                },
            });
        }
        var jump = new EX_JumpIfNot
        {
            CodeOffset = 0,
            BooleanExpression = MayhemFixKit.CloneKismet(virtLet.VariableExpression),
        };
        var block = new List<KismetExpression> { virtLet, jump };
        block.AddRange(applies);

        uint insertOffset = 0;
        for (int i = 0; i < consumeIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
        uint blockSize = (uint)block.Sum(b => (long)b.GetSize(bp));
        jump.CodeOffset = insertOffset + blockSize;
        ShiftOffsetsAtOrAfter(bp, fn, insertOffset, blockSize);
        stmts.InsertRange(consumeIdx, block);
        fn.ScriptBytecode = stmts.ToArray();
        MayhemFixKit.RecalcContextSkips(bp, fn);
        Console.WriteLine("[Maelle] ReversFatal: Virtuose-gated Greater Slow + Greater Defenceless spliced");
        return 1;
    }

    /// <summary>
    /// Remaps every flow-offset target after a statement deletion/insertion, using the
    /// pre-edit statement-start map. A target that pointed at the start of statement N
    /// (pre-edit) is rewritten to statement N's CURRENT start. Statement ORDER must be
    /// unchanged (deletions remove whole statements but never reorder). Targets that
    /// fell strictly inside a statement are clamped to that statement's start.
    /// </summary>
    private static void RemapFlowOffsetsByOrdinal(UAsset bp, FunctionExport fn, List<uint> preStarts)
    {
        // Current (post-edit) statement starts, in order.
        var curStarts = new List<uint>();
        uint pos = 0;
        foreach (var s in fn.ScriptBytecode!)
        {
            curStarts.Add(pos);
            pos += (uint)s.GetSize(bp);
        }
        // Map a pre-edit absolute offset to a statement ordinal (the last start <= target).
        int OrdinalOf(uint target)
        {
            int idx = preStarts.Count - 1;
            for (int i = 0; i < preStarts.Count; i++)
                if (preStarts[i] > target) { idx = i - 1; break; }
            return Math.Max(0, idx);
        }
        uint p = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(bp, ref p, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j: j.CodeOffset = curStarts[Math.Min(OrdinalOf(j.CodeOffset), curStarts.Count - 1)]; break;
                    case EX_JumpIfNot jn: jn.CodeOffset = curStarts[Math.Min(OrdinalOf(jn.CodeOffset), curStarts.Count - 1)]; break;
                    case EX_PushExecutionFlow pf: pf.PushingAddress = curStarts[Math.Min(OrdinalOf(pf.PushingAddress), curStarts.Count - 1)]; break;
                    case EX_SwitchValue sv:
                        sv.EndGotoOffset = curStarts[Math.Min(OrdinalOf(sv.EndGotoOffset), curStarts.Count - 1)];
                        for (int i = 0; i < sv.Cases.Length; i++)
                            sv.Cases[i].NextOffset = curStarts[Math.Min(OrdinalOf(sv.Cases[i].NextOffset), curStarts.Count - 1)];
                        break;
                }
            });
    }

    /// <summary>Like MayhemFixKit.ShiftAbsolute but shifts targets that equal the
    /// insertion point too (a jump that pointed at the first shifted statement).</summary>
    private static void ShiftOffsetsAtOrAfter(UAsset bp, FunctionExport fn, uint insertionPoint, uint delta)
    {
        if (delta == 0) return;
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(bp, ref pos, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j when j.CodeOffset >= insertionPoint: j.CodeOffset += delta; break;
                    case EX_JumpIfNot jn when jn.CodeOffset >= insertionPoint: jn.CodeOffset += delta; break;
                    case EX_PushExecutionFlow pf when pf.PushingAddress >= insertionPoint: pf.PushingAddress += delta; break;
                    case EX_SwitchValue sv:
                        if (sv.EndGotoOffset >= insertionPoint) sv.EndGotoOffset += delta;
                        for (int i = 0; i < sv.Cases.Length; i++)
                            if (sv.Cases[i].NextOffset >= insertionPoint) sv.Cases[i].NextOffset += delta;
                        break;
                }
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // F. Names + descriptions (string table)
    // ──────────────────────────────────────────────────────────────────────

    private static string Dmg(double v) => v.ToString("0.00") + "x";
    private static string Gold(double v) => $"<keyword id=\"DamageMultiplier\">{Dmg(v)}</>";

    /// <summary>Per-hit gold multiplier for an N-hit move: 0.75×clamp(AP) ÷ hits.</summary>
    private static string GoldPerHit(double ap, int hits) => Gold(PerHit(ap, hits));

    /// <summary>Rain of Fire: 5 AP, 2 hits. Its skill script hardcodes the per-hit
    /// multiplier (<c>Local_DamageMultiplier</c>) instead of reading its
    /// <c>RainOfFire_BaseDamage</c> row, which is therefore dead data — so rescaling
    /// the move means rewriting that DoubleConst, not a balancing row.</summary>
    private const int RainOfFireApCost = 5;
    private const int RainOfFireHits = 2;
    /// <summary>The script's vanilla hardcoded value, matched when patching.</summary>
    private const double RainOfFireVanillaPerHit = 1.25;
    /// <summary>Overhaul 5 AP budget over 2 hits: 0.75×clamp(5) ÷ 2 = 0.61875.</summary>
    private static readonly double RainOfFirePerHit =
        Math.Round(PerHit(RainOfFireApCost, RainOfFireHits), 4);

    /// <summary>Display + description entries. Key prefix = the string-table move key
    /// (Maelle_&lt;NameId&gt;). Multi-hit moves quote the PER-HIT gold multiplier
    /// (the balancing row value — each hit's floating damage); single-hit moves quote
    /// the total (identical to per-hit). Conditional lines state the exact rate
    /// (+% per Burn / +% per hit received / −AP per parry). Rain of Fire is the one
    /// exception: it quotes its script constant, not its row (see
    /// <see cref="RainOfFirePerHit"/>).</summary>
    private static readonly (string Key, string Name, string Long, string Short)[] StringEdits =
    {
        // ── Lightning path ────────────────────────────────────────────────
        ("Maelle_InvigoratingFire", "Thermal Arc",
            $"Deals {GoldPerHit(4, 2)} single target <keyword id=\"Element_Lightning\">Lightning</> damage per hit. 2 hits.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> up to 10000 <keyword id=\"StatusEffect_Burn\">Burn</>: deal <keyword id=\"DamageMultiplier\">5%</> more damage per <keyword id=\"StatusEffect_Burn\">Burn</> consumed.",
            $"{GoldPerHit(4, 2)} <keyword id=\"Element_Lightning\">Lightning</> damage per hit. 2 hits.\n" +
            "Consumes <keyword id=\"StatusEffect_Burn\">Burn</>: deal 5% more damage per <keyword id=\"StatusEffect_Burn\">Burn</> consumed."),
        ("Maelle_FleuretFury", "Electric Fury",
            $"Deals {GoldPerHit(9, 3)} single target <keyword id=\"Element_Lightning\">Lightning</> damage per hit. 3 hits.\n" +
            "If in <keyword id=\"Stance_Virtuose\">Virtuose Stance</>, stay in <keyword id=\"Stance_Virtuose\">Virtuose Stance</>\n" +
            "Can <keyword id=\"Break\">Break</>",
            $"{GoldPerHit(9, 3)} <keyword id=\"Element_Lightning\">Lightning</> damage per hit. 3 hits.\n" +
            "If in <keyword id=\"Stance_Virtuose\">Virtuose Stance</>, stay in <keyword id=\"Stance_Virtuose\">Virtuose Stance</>.\n" +
            "Can <keyword id=\"Break\">Break</>"),
        ("Maelle_BreakingRules", "Breaking Rules",
            $"Deals {GoldPerHit(5, 2)} single target <keyword id=\"Element_Lightning\">Lightning</> damage per hit. 2 hits.\n" +
            "Destroys all target's <keyword id=\"Shield\">Shields</> and gains 1 <keyword id=\"APShard\">AP</> per Shield destroyed.\n" +
            "If target is <keyword id=\"Buff_Defenseless\">Defenceless</>, play a second turn.",
            $"{GoldPerHit(5, 2)} <keyword id=\"Element_Lightning\">Lightning</> damage per hit. 2 hits.\n" +
            "Destroys <keyword id=\"Shield\">Shields</> and gains 1 <keyword id=\"APShard\">AP</> per Shield.\n" +
            "If target is <keyword id=\"Buff_Defenseless\">Defenceless</>, play a second turn."),
        ("Maelle_Payback", "Payback",
            $"Deals {Gold(PerHit(5, 1))} single target <keyword id=\"Element_Lightning\">Lightning</> damage. 1 hit.\n" +
            "Costs 1 less <keyword id=\"APShard\">AP</> for each attack parried since last turn.\n" +
            "Can <keyword id=\"Break\">Break</>",
            $"{Gold(PerHit(5, 1))} <keyword id=\"Element_Lightning\">Lightning</> damage. 1 hit.\n" +
            "Costs 1 less <keyword id=\"APShard\">AP</> for each attack parried since last turn.\n" +
            "Can <keyword id=\"Break\">Break</>"),
        ("Maelle_GustaveSMemoire", "Gustave's Homage",
            $"Deals {GoldPerHit(13, 8)} single target <keyword id=\"Element_Lightning\">Lightning</> damage per hit. 8 hits.\n" +
            "Increased damage to <keyword id=\"StatusEffect_Mark\">Marked</> targets.\n" +
            "Doesn't remove Mark.",
            $"{GoldPerHit(13, 8)} <keyword id=\"Element_Lightning\">Lightning</> damage per hit. 8 hits.\n" +
            "Increased damage to <keyword id=\"StatusEffect_Mark\">Marked</> targets.\n" +
            "Doesn't remove Mark."),
        ("Maelle_OffensiveSwitch", "Offensive Switch",
            $"Deals {Gold(MaelleTotal(1))} single target damage and applies <keyword id=\"Buff_Defenseless\">Defenceless</> for 1 turn. 1 hit.\n" +
            "Uses weapon's element.",
            $"{Gold(MaelleTotal(1))} {{DynamicElement}} damage. Applies <keyword id=\"Buff_Defenseless\">Defenceless</> for 1 turn. 1 hit."),
        ("Maelle_GuardDown", "Guard Down",
            "Applies <keyword id=\"Buff_Defenseless\">Defenceless</> to all enemies for 2 turns.",
            "Applies <keyword id=\"Buff_Defenseless\">Defenceless</> to all enemies for 2 turns."),
        ("Maelle_GuardUp", "Guard Up",
            "Applies <keyword id=\"Buff_Shell\">Shell</>, reducing damage taken, to up to 5 allies for 2 turns.",
            "Applies <keyword id=\"Buff_Shell\">Shell</>, reducing damage taken, to up to 5 allies for 2 turns."),
        ("Maelle_FencersFlurry", "Fencer's Flurry",
            $"Deals {Gold(PerHit(4, 1))} damage to all enemies. 1 hit.\n" +
            "Uses weapon's element.\n" +
            "Applies <keyword id=\"Buff_Defenseless\">Defenceless</> for 1 turn.",
            $"{Gold(PerHit(4, 1))} {{DynamicElement}} damage to all enemies. 1 hit.\n" +
            "Applies <keyword id=\"Buff_Defenseless\">Defenceless</> for 1 turn."),

        // ── Fire path ─────────────────────────────────────────────────────
        ("Maelle_Percee", "Percée",
            $"Deals {Gold(MaelleTotal(1))} single target <keyword id=\"Element_Fire\">Fire</> damage. 1 hit.\n" +
            "Increased damage to <keyword id=\"StatusEffect_Mark\">Marked</> targets.\n" +
            $"<keyword id=\"Stance_Virtuose\">Virtuose Stance</>: deal {Gold(VirtuoseTestMultiplier)} more damage.",
            $"{Gold(MaelleTotal(1))} <keyword id=\"Element_Fire\">Fire</> damage. 1 hit.\n" +
            "Increased damage to <keyword id=\"StatusEffect_Mark\">Marked</> targets.\n" +
            $"<keyword id=\"Stance_Virtuose\">Virtuose Stance</>: deal {Gold(VirtuoseTestMultiplier)} more damage."),
        ("Maelle_MomentumStrike", "Momentum Strike",
            $"Deals {Gold(MaelleTotal(7))} single target damage. 1 hit.\n" +
            "Uses weapon's element.\n" +
            "Increased damage to <keyword id=\"StatusEffect_Mark\">Marked</> targets.\n" +
            $"<keyword id=\"Stance_Virtuose\">Virtuose Stance</>: deal {Gold(VirtuoseTestMultiplier)} more damage.",
            $"{Gold(MaelleTotal(7))} {{DynamicElement}} damage. 1 hit.\n" +
            "Increased damage to <keyword id=\"StatusEffect_Mark\">Marked</> targets.\n" +
            $"<keyword id=\"Stance_Virtuose\">Virtuose Stance</>: deal {Gold(VirtuoseTestMultiplier)} more damage."),
        ("Maelle_FireRise", "Virtuous Flames",
            $"Deals {Gold(MaelleTotal(3))} single target <keyword id=\"Element_Fire\">Fire</> damage. 1 hit.\n" +
            "Switches to <keyword id=\"Stance_Virtuose\">Virtuose Stance</> if target is <keyword id=\"StatusEffect_Burn\">Burning</>",
            $"{Gold(MaelleTotal(3))} <keyword id=\"Element_Fire\">Fire</> damage. 1 hit.\n" +
            "Switches to <keyword id=\"Stance_Virtuose\">Virtuose Stance</> if target is <keyword id=\"StatusEffect_Burn\">Burning</>"),
        ("Maelle_Pyrolyse", "Pyrolyse",
            $"Deals {GoldPerHit(9, 3)} single target <keyword id=\"Element_Fire\">Fire</> damage per hit. 3 hits.\n" +
            "Applies 5 <keyword id=\"StatusEffect_Burn\">Burn</> per hit.\n" +
            "<keyword id=\"Stance_Offensive\">Offensive Stance</>: Applies 2 more <keyword id=\"StatusEffect_Burn\">Burn</> per hit.",
            $"{GoldPerHit(9, 3)} <keyword id=\"Element_Fire\">Fire</> damage per hit. 3 hits.\n" +
            "Applies 5 <keyword id=\"StatusEffect_Burn\">Burn</> per hit.\n" +
            "<keyword id=\"Stance_Offensive\">Offensive Stance</>: Applies 2 more <keyword id=\"StatusEffect_Burn\">Burn</> per hit."),
        ("Maelle_SwordBallet", "Sword Ballet",
            $"Deals {GoldPerHit(9, 5)} single target damage per hit. 5 hits.\n" +
            "Uses weapon's element.\n" +
            "<keyword id=\"CriticalHit\">Critical Hits</> deal double damage.",
            $"{GoldPerHit(9, 5)} {{DynamicElement}} damage per hit. 5 hits.\n" +
            "<keyword id=\"CriticalHit\">Critical Hits</> deal double damage."),
        ("Maelle_Revenge", "Revenge",
            $"Deals {Gold(MaelleTotal(5))} single target <keyword id=\"Element_Fire\">Fire</> damage. 1 hit.\n" +
            "Damage increased <keyword id=\"DamageMultiplier\">+50%</> for each hit received since the previous turn.\n" +
            "Can <keyword id=\"Break\">Break</>",
            $"{Gold(MaelleTotal(5))} <keyword id=\"Element_Fire\">Fire</> damage. 1 hit.\n" +
            "Damage increased +50% for each hit received since the previous turn.\n" +
            "Can <keyword id=\"Break\">Break</>"),
        ("Maelle_Spark", "Spark",
            $"Deals {Gold(MaelleTotal(2))} single target <keyword id=\"Element_Fire\">Fire</> damage. 1 hit.\n" +
            "Applies 3 <keyword id=\"StatusEffect_Burn\">Burn</>.\n" +
            "<keyword id=\"Stance_Offensive\">Offensive Stance</>: Applies 2 more <keyword id=\"StatusEffect_Burn\">Burn</>",
            $"{Gold(MaelleTotal(2))} <keyword id=\"Element_Fire\">Fire</> damage. 1 hit.\n" +
            "Applies 3 <keyword id=\"StatusEffect_Burn\">Burn</>.\n" +
            "<keyword id=\"Stance_Offensive\">Offensive Stance</>: Applies 2 more <keyword id=\"StatusEffect_Burn\">Burn</>"),
        ("Maelle_Degagement", "Degagement",
            $"Deals {Gold(MaelleTotal(2))} single target <keyword id=\"Element_Fire\">Fire</> damage. 1 hit.\n" +
            "Target becomes weak to <keyword id=\"Element_Fire\">Fire</> damage for 5 turns.",
            $"{Gold(MaelleTotal(2))} <keyword id=\"Element_Fire\">Fire</> damage. 1 hit.\n" +
            "Target becomes weak to <keyword id=\"Element_Fire\">Fire</> damage for 5 turns."),
        // Rain of Fire — rescaled onto the overhaul curve by PatchRainOfFire. Its
        // script hardcodes the per-hit multiplier instead of reading the dead
        // RainOfFire_BaseDamage row (vanilla 0.8), so the DoubleConst carries the value.
        ("Maelle_RainOfFire", "Rain of Fire",
            $"Deals {Gold(RainOfFirePerHit)} single target <keyword id=\"Element_Fire\">Fire</> damage per hit. 2 hits.\n" +
            "Applies 3 <keyword id=\"StatusEffect_Burn\">Burn</> per hit.\n" +
            "<keyword id=\"Stance_Defensive\">Defensive Stance</>: Applies 2 more <keyword id=\"StatusEffect_Burn\">Burn</> per hit.",
            $"{Gold(RainOfFirePerHit)} <keyword id=\"Element_Fire\">Fire</> damage per hit. 2 hits.\n" +
            "Applies 3 <keyword id=\"StatusEffect_Burn\">Burn</> per hit.\n" +
            "<keyword id=\"Stance_Defensive\">Defensive Stance</>: Applies 2 more <keyword id=\"StatusEffect_Burn\">Burn</> per hit."),

        // ── Void path ─────────────────────────────────────────────────────
        ("Maelle_PhantomStrike", "Phantom Strike",
            $"Deals {GoldPerHit(12, 4)} <keyword id=\"Element_Void\">Void</> damage per hit to all enemies. 4 hits.\n" +
            "Also gives +50% of a Gradient Charge.",
            $"{GoldPerHit(12, 4)} <keyword id=\"Element_Void\">Void</> damage per hit to all enemies. 4 hits.\n" +
            "Also gives +50% of a Gradient Charge."),
        ("Maelle_BurningCanvas", "Burning Canvas",
            $"Deals {GoldPerHit(11, 5)} single target <keyword id=\"Element_Void\">Void</> damage per hit. 5 hits.\n" +
            "Applies 1 <keyword id=\"StatusEffect_Burn\">Burn</> per hit. Deal <keyword id=\"DamageMultiplier\">1%</> more damage per <keyword id=\"StatusEffect_Burn\">Burn</> on the target (does not consume).",
            $"{GoldPerHit(11, 5)} <keyword id=\"Element_Void\">Void</> damage per hit. 5 hits.\n" +
            "Applies 1 <keyword id=\"StatusEffect_Burn\">Burn</> per hit. Deal 1% more damage per <keyword id=\"StatusEffect_Burn\">Burn</> on the target (does not consume)."),
        ("Maelle_Stendhal", "Stendhal",
            $"Deals {Gold(MaelleTotal(15))} single target <keyword id=\"Element_Void\">Void</> damage. 1 hit.\n" +
            "Removes self-<keyword id=\"Shield\">Shields</> and self applies <keyword id=\"Buff_Defenseless\">Defenceless</> for 5 turns.",
            $"{Gold(MaelleTotal(15))} <keyword id=\"Element_Void\">Void</> damage. 1 hit.\n" +
            "Removes self-<keyword id=\"Shield\">Shields</> and self applies <keyword id=\"Buff_Defenseless\">Defenceless</> for 5 turns."),
        ("Maelle_LastChance", "Death's Bargain",
            "Reduces self-Health to 1 but refills all <keyword id=\"APShard\">AP</>.\n" +
            "Applies Curse to self for 4 turns.\n" +
            "Switches to <keyword id=\"Stance_Virtuose\">Virtuose Stance</>",
            "Reduces self-Health to 1 but refills all <keyword id=\"APShard\">AP</>.\n" +
            "Applies Curse to self for 4 turns.\n" +
            "Switches to <keyword id=\"Stance_Virtuose\">Virtuose Stance</>"),
        ("Maelle_MezzoForte", "Mezzo Forte",
            "Reapplies current stance and gives 2-4 <keyword id=\"APShard\">AP</>",
            "Reapplies current stance and gives 2-4 <keyword id=\"APShard\">AP</>"),

        // ── Gradient skills ───────────────────────────────────────────────
        ("Maelle_VirtuoseStrike", "Golgra's Combo",
            $"Deals {Gold(6.0)} single target <keyword id=\"Element_Lightning\">Lightning</> damage per hit. 5 hits.",
            $"{Gold(6.0)} <keyword id=\"Element_Lightning\">Lightning</> damage per hit. 5 hits."),
        ("Maelle_PhoenixFlame", "Phoenix Flame",
            "Applies 100 <keyword id=\"StatusEffect_Burn\">Burn</> to all enemies and revives all allies with 100% Health.",
            "Applies 100 <keyword id=\"StatusEffect_Burn\">Burn</> to all enemies and revives all allies with 100% Health."),
        ("Maelle_Gommage", "Gommage",
            $"Deals {Gold(100.0)} <keyword id=\"Element_Void\">Void</> damage. 1 hit.\n" +
            "Kills weak targets.",
            $"{Gold(100.0)} <keyword id=\"Element_Void\">Void</> damage. 1 hit.\n" +
            "Kills weak targets."),
    };

    /// <summary>Editor preview: the modded Long description for one Maelle
    /// string-table base key (e.g. "Maelle_Payback"). Returns false when no kit
    /// entry rewrites that key (caller falls back to the vanilla text).</summary>
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
        Console.WriteLine($"[Maelle] staged {entries.Count} string-table entries");
        return entries.Count;
    }

    // ──────────────────────────────────────────────────────────────────────
    // F2. Rain of Fire — rescale the script's hardcoded per-hit multiplier
    // ──────────────────────────────────────────────────────────────────────

    private const string RainOfFireBpStem = "BP_Battle_SkillScript_New9_RainOfFire";

    /// <summary>
    /// Rescales Rain of Fire onto the overhaul's 5 AP budget. Every other Maelle
    /// move reads its <c>&lt;Name&gt;_BaseDamage</c> balancing row as the per-hit
    /// multiplier, so <see cref="RebalanceRows"/> is enough for them. Rain of Fire
    /// never reads its row (vanilla left it at 0.8 — dead data) and instead
    /// hardcodes <c>Local_DamageMultiplier</c>, so the DoubleConst has to be
    /// rewritten directly. The description quotes the same
    /// <see cref="RainOfFirePerHit"/> value, so the two cannot drift.
    /// </summary>
    private static int PatchRainOfFire(string modDir)
    {
        var src = FindTables(RainOfFireBpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);

        int n = 0;
        foreach (var fn in bp.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            MayhemFixKit.Walk(fn.ScriptBytecode, e =>
            {
                if (e is EX_DoubleConst dc && Math.Abs(dc.Value - RainOfFireVanillaPerHit) < 0.0001)
                {
                    dc.Value = RainOfFirePerHit;
                    n++;
                }
            });
        }
        if (n > 0) bp.Write(dest);
        Console.WriteLine(
            $"[Maelle] Rain of Fire: per-hit {RainOfFireVanillaPerHit} -> {RainOfFirePerHit:0.##} ({n} constant(s))");
        return n;
    }

    // ──────────────────────────────────────────────────────────────────────
    // G. Virtuose stance ×1.5 on Percée / Momentum (per-move damage gate)
    // ──────────────────────────────────────────────────────────────────────

    private const string PerceeBpStem = "BP_Battle_SkillScript_New14_Percee";
    private const string MomentumBpStem = "BP_Battle_SkillScript_MomentumStrike";

    /// <summary>Moves that gain bonus damage while in Virtuose stance.</summary>
    private static readonly (string DaStem, string BpStem)[] VirtuoseDmgMoves =
    {
        ("DA_Skill_Maelle_NEW14_Percee", PerceeBpStem),
        ("DA_Skill_Maelle_MomentumStrike", MomentumBpStem),
    };

    /// <summary>
    /// In-Virtuose damage multiplier for Percée / Momentum (×1.5 = +50%).
    /// Was temporarily 100.0 for playtesting the stance-gated splice; reverted.
    /// </summary>
    private const double VirtuoseTestMultiplier = 1.5;

    /// <summary>
    /// Splices a Virtuose stance snapshot and a gated damage multiplier into Percée /
    /// Momentum. The snapshot MUST be built from THIS asset's own OnActionEffect locals
    /// and taken immediately after its GetComponentByClass, BEFORE the move's
    /// SwitchStance(2) (Defensive) call — both moves switch stance before damage, so a
    /// post-switch read never sees Virtuose.
    ///
    /// The old implementation cloned the CurrentStance==3 statements out of
    /// GetSkillCostOverride (Momentum's only stance gate) and pasted them into
    /// OnActionEffect. Their local-variable FFieldPaths stayed owned by
    /// GetSkillCostOverride, so the VM wrote through the wrong property offsets into
    /// OnActionEffect's frame (Momentum's target-loop counter) and crashed on cast.
    ///
    /// Shape:
    ///   LetBool isVirtuose = Context(comp).CurrentStance == ByteConst 3   [after GetComponentByClass]
    ///   JumpIfNot isVirtuose → skip the add                              [before DealDamages]
    ///   Context(MakeDamageBuilder).AddFinalDamageMultiplier(1.5)
    /// </summary>
    private static int PatchVirtuoseDmg(string modDir)
    {
        int n = 0;
        foreach (var (daStem, bpStem) in VirtuoseDmgMoves)
        {
            var src = FindTables(bpStem);
            Stage(src, modDir, out var dest);
            var bp = Open(dest);
            var fn = bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
            if (fn?.ScriptBytecode is null) continue;
            if (HasFinalMultBeforeDeal(bp, fn)) continue;

            // 1. Build the gate with OnActionEffect's OWN locals. Use a dedicated
            //    bool local: Percée already uses CallFunc_EqualEqual_ByteByte_ReturnValue
            //    later in its target loop, and reusing it would make the gate read a
            //    stale value on a second iteration. The helper reuses the file's
            //    existing GetComponentByClass local, declares the bool on this function
            //    when missing, and rebases CurrentStance to the StanceMechanic import.
            const string VirtuoseLocal = "Virtuose_IsVirtuose";
            var virtLet = BuildInAssetVirtuoseLet(bp, fn, VirtuoseLocal);
            int compIdx = IndexOfGetComponentLet(bp, fn);
            int switchIdx = IndexOfSwitchStance(fn);
            if (virtLet is null || compIdx < 0 || !IsStanceComponentLet(bp, fn, compIdx)
                || (switchIdx >= 0 && compIdx > switchIdx))
            {
                Console.WriteLine($"[Maelle] {bpStem}: in-asset Virtuose gate shape unavailable; x{VirtuoseTestMultiplier:0.##} splice skipped");
                continue;
            }

            // 2. Snapshot the stance BEFORE SwitchStance(2) (Defensive).
            var stmts = fn.ScriptBytecode.ToList();
            InsertStmts(bp, fn, ref stmts, compIdx + 1, virtLet);

            // 3. The builder local is DealDamages' Context object expression.
            int dealIdx = IndexOfDealDamages(fn);
            EX_Context? dealCtx = null;
            if (dealIdx >= 0)
                MayhemFixKit.Walk(new[] { stmts[dealIdx] }, e =>
                {
                    if (dealCtx is null && e is EX_Context ctx
                        && ctx.ContextExpression is EX_VirtualFunction vf
                        && vf.VirtualFunctionName.ToString() == "DealDamages")
                        dealCtx = ctx;
                });
            if (dealIdx < 0 || dealCtx?.ObjectExpression is not EX_LocalVariable builderLocal)
            {
                Console.WriteLine($"[Maelle] {bpStem}: DealDamages/builder not found; Virtuose x{VirtuoseTestMultiplier:0.##} splice skipped");
                continue;
            }

            var addFinal = new EX_Context
            {
                ObjectExpression = (EX_LocalVariable)MayhemFixKit.CloneKismet(builderLocal),
                ContextExpression = new EX_VirtualFunction
                {
                    VirtualFunctionName = FName.FromString(bp, "AddFinalDamageMultiplier"),
                    Parameters = new KismetExpression[] { new EX_DoubleConst { Value = VirtuoseTestMultiplier } },
                },
                RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            };
            var gate = new EX_JumpIfNot
            {
                CodeOffset = 0,
                BooleanExpression = MayhemFixKit.CloneKismet(virtLet.VariableExpression),
            };

            uint insertOffset = 0;
            for (int i = 0; i < dealIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
            uint blockSize = (uint)(gate.GetSize(bp) + addFinal.GetSize(bp));
            gate.CodeOffset = insertOffset + blockSize;
            InsertStmts(bp, fn, ref stmts, dealIdx, gate, addFinal);
            AssertLocalOwners(bp, fn);

            bp.Write(dest);
            Console.WriteLine($"[Maelle] {bpStem}: Virtuose x{VirtuoseTestMultiplier:0.##} snapshot before SwitchStance + damage splice before DealDamages");
            n++;
        }
        return n;
    }

    /// <summary>
    /// Every local-variable FFieldPath in a function must resolve to that same
    /// function export. Cross-function locals (e.g. a statement cloned out of
    /// GetSkillCostOverride into OnActionEffect) pass kismet-check but make the VM
    /// read/write the wrong frame offsets at runtime.
    /// </summary>
    private static void AssertLocalOwners(UAsset bp, FunctionExport fn)
    {
        if (fn.ScriptBytecode is null) return;
        int own = bp.Exports.IndexOf(fn) + 1;
        MayhemFixKit.Walk(fn.ScriptBytecode, e =>
        {
            KismetPropertyPointer? ptr = e switch
            {
                EX_LocalVariable lv => lv.Variable,
                EX_LocalOutVariable ov => ov.Variable,
                _ => null,
            };
            if (ptr?.New is not { Path.Length: > 0 } fp) return;
            if (fp.ResolvedOwner?.Index == own) return;
            throw new InvalidDataException(
                $"[Maelle] {fn.ObjectName}: local {string.Join(".", fp.Path.Select(n => n.ToString()))} " +
                $"owned by export {fp.ResolvedOwner?.Index} (expected {own})");
        });
    }

    /// <summary>True when our stance-gated VirtuoseTestMultiplier AddFinalDamageMultiplier already precedes DealDamages.</summary>
    private static bool HasFinalMultBeforeDeal(UAsset bp, FunctionExport fn)
    {
        if (fn.ScriptBytecode is null) return false;
        var stmts = fn.ScriptBytecode.ToList();
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hasDeal = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "DealDamages") hasDeal = true;
            });
            if (!hasDeal) continue;
            // Scan back a few statements for an AddFinalDamageMultiplier(VirtuoseTestMultiplier).
            for (int j = Math.Max(0, i - 4); j < i; j++)
            {
                bool hasFinal = false;
                MayhemFixKit.Walk(new[] { stmts[j] }, e =>
                {
                    if (hasFinal) return;
                    if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "AddFinalDamageMultiplier")
                        foreach (var p in vf.Parameters)
                            if (p is EX_DoubleConst dc && Math.Abs(dc.Value - VirtuoseTestMultiplier) < 0.001) hasFinal = true;
                });
                if (hasFinal) return true;
            }
        }
        return false;
    }

    /// <summary>Remap a source-asset import index onto the dest asset by object name.
    /// Kismet math functions that the dest is missing are cloned as KismetMathLibrary
    /// siblings (EqualEqual_ByteByte must not be faked as an Engine class).</summary>
    private static FPackageIndex MapImport(UAsset src, UAsset dest, FPackageIndex idx)
    {
        if (idx is null || idx.IsNull() || !idx.IsImport()) return idx;
        var name = src.Imports[Math.Abs(idx.Index) - 1].ObjectName.ToString();
        for (int i = 0; i < dest.Imports.Count; i++)
            if (dest.Imports[i].ObjectName.ToString() == name)
                return FPackageIndex.FromImport(i);
        if (name.Contains("ByteByte", StringComparison.Ordinal) || name.StartsWith("Multiply_", StringComparison.Ordinal)
            || name.StartsWith("EqualEqual_", StringComparison.Ordinal) || name.StartsWith("NotEqual_", StringComparison.Ordinal))
            return name == "EqualEqual_ByteByte" ? EnsureEqualEqualByteByte(dest) : EnsureEngineFunction(dest, "KismetMathLibrary", name);
        Console.WriteLine($"[Maelle] dest missing import '{name}'; leaving source index {idx.Index}");
        return idx;
    }

    private static void RebasePtrFromSource(UAsset src, UAsset dest, KismetPropertyPointer? p,
        FPackageIndex destFnOwner, FPackageIndex destClassOwner)
    {
        if (p?.New is not { } np) return;
        var names = (np.Path ?? Array.Empty<FName>())
            .Select(n => FName.FromString(dest, n.ToString())).ToArray();
        FPackageIndex owner = destFnOwner;
        if (names.Length > 0 && names[0].ToString() == "CurrentStance")
        {
            for (int k = 0; k < dest.Imports.Count; k++)
                if (dest.Imports[k].ObjectName.ToString() == "BP_StanceMechanicComponent_C")
                { p.New = new FFieldPath(names, FPackageIndex.FromImport(k)); return; }
        }
        var so = np.ResolvedOwner;
        if (so is { } s && !s.IsNull())
        {
            if (s.IsImport())
                owner = MapImport(src, dest, s);
            else if (s.IsExport())
            {
                var srcExp = src.Exports[s.Index - 1];
                owner = srcExp is FunctionExport ? destFnOwner : destClassOwner;
            }
        }
        p.New = new FFieldPath(names, owner);
    }

    private static string PtrName(KismetPropertyPointer? p) =>
        p?.New?.Path?.FirstOrDefault()?.ToString() ?? "";

    private static object ShallowClone(object o)
    {
        var m = o.GetType().GetMethod("MemberwiseClone",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return m.Invoke(o, null)!;
    }

    private static bool HasObjectConst(UAsset asset, FunctionExport fn, string objectName)
    {
        bool hit = false;
        if (fn.ScriptBytecode is null) return false;
        MayhemFixKit.Walk(fn.ScriptBytecode, e =>
        {
            if (e is EX_ObjectConst oc && oc.Value.IsImport()
                && asset.Imports[Math.Abs(oc.Value.Index) - 1].ObjectName.ToString() == objectName)
                hit = true;
        });
        return hit;
    }
}

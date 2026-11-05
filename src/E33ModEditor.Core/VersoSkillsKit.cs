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
/// Verso first-pass respec — all vanilla moves at 0.75 × the Lune AP-clamp
/// curve (Speed Burst and Defiant/Blood Strike at 1.5 ×; Steeled Strike keeps
/// its baked-in delay triple; Zanmoto keeps full curve), per-hit gold numbers
/// Lune-style, vanilla voice descs.
///
/// Rank-gate architecture (repo-proven 2026-09-11): SkillState.IsOvercharged
/// resolves through an opaque runtime path with NO per-skill data anywhere
/// (not in DAs, graph nodes, balancing rows, or tree assets), so every rank
/// gate in a staged BP is replaced with an EXPLICIT check — CurrentRank from
/// BP_PerfectionMechanicComponent, compared via the component's own
/// GetRankValue + GreaterEqual_IntInt (the exact IsOverchargeActive shape).
/// Rank bytes (E_Perfection_Ranks, forward order): None=0 D=1 C=2 B=3 A=4 S=5.
///
/// Gustave safety: Verso shares plain balancing rows with Gustave's copies
/// (FromFire, MarkingShot, StrikeStorm, Powerful). Those four moves get
/// Verso-suffixed row clones + retargeted staged Verso BPs; every other move
/// edits its Verso-exclusive rows in place. ST keys are Verso_* (Gustave uses
/// Verso_*_Gustave) — no collision. DA_Skill_Combo1 and plain rows are never
/// written (Assault Zero is a full custom with its own rows).
/// </summary>
public static partial class VersoSkillsKit
{
    public const string BalTable = "DT_Balancing_MainCharacterSkills";
    public const string StringTable = "ST_MainCharacters_Skills";
    public const string GraphStem = "DA_SkillGraph_Verso";

    /// <summary>AP clamp — same curve as every other kit.</summary>
    public static double Clamp(double ap) => SkillOverhaulKit.Clamp(ap);

    /// <summary>Verso rule: 0.75 × the Lune curve value.</summary>
    public static double VersoTotal(double ap) => 0.75 * Clamp(ap);

    /// <summary>Per-hit base row for an N-hit move: (0.75×clamp) ÷ hits.</summary>
    public static double PerHit(double ap, int hits) => VersoTotal(ap) / hits;

    internal static string Dmg(double v) => v.ToString("0.00") + "x";
    internal static string Gold(double v) => $"<keyword id=\"DamageMultiplier\">{Dmg(v)}</>";

    // E_Perfection_Ranks byte values (forward order, SetSpecificRank-proven:
    // Overcharge sets A=4, Perfect Break sets S=5; Leadership blocks at D=1).
    public const byte RankNone = 0, RankD = 1, RankC = 2, RankB = 3, RankA = 4, RankS = 5;

    // ── description keywords (game string-table ids) ──
    private const string Perfection = "<keyword id=\"Perfection\">Perfection</>";
    private const string AP = "<keyword id=\"APShard\">AP</>";
    private const string Physical = "<keyword id=\"Element_Physical\">Physical</>";
    private const string Light = "<keyword id=\"Element_Light\">Light</>";
    private const string Fire = "<keyword id=\"Element_Fire\">Fire</>";
    private const string Lightning = "<keyword id=\"Element_Lightning\">Lightning</>";
    private const string Break = "<keyword id=\"Break\">Break</>";
    private const string Stun = "<keyword id=\"StatusEffect_Stunned\">Stun</>";
    private const string Stunned = "<keyword id=\"StatusEffect_Stunned\">Stunned</>";
    private const string Mark = "<keyword id=\"StatusEffect_Mark\">Mark</>";
    private const string Burn = "<keyword id=\"StatusEffect_Burn\">Burns</>";
    private const string Heal = "<keyword id=\"Heal\">Heals</>";
    private const string Recovers = "<keyword id=\"Heal\">Recovers</>";
    private const string Crits = "<keyword id=\"CriticalHit\">Critical Hits</>";
    private const string Slow = "<keyword id=\"Buff_Slow\">Slow</>";
    private const string Berserk = "<keyword id=\"Debuff_Berserk\">Berserk</>";
    private const string Powerful = "<keyword id=\"Buff_Powerful\">Powerful</>";
    private static string Rank(char r) => $"<img id=\"Rank_{r}\"/>";

    /// <summary>One respec'd move: string-table key, display name, AP cost.</summary>
    private sealed record Spec(string Key, string Name, int Ap);

    private static readonly Spec[] Specs =
    {
        new("Verso_AssaultZero", "Assault Zero", 1),
        new("Verso_Powerful", "Powerful", 2),
        new("Verso_ParadigmShift", "Paradigm Shift", 4),
        new("Verso_Leadership", "Leadership", 5),
        new("Verso_QuickStrike", "Quick Strike", 3),
        new("Verso_Followup", "Follow Up", 4),
        new("Verso_AscendingAssault", "Ascending Assault", 5),
        new("Verso_RadiantSlash", "Radiant Slash", 6),
        new("Verso_LightHolder", "Light Holder", 3),
        new("Verso_PerfectBreak", "Perfect Break", 8),
        new("Verso_BerserkSlash", "Berserk Slash", 9),
        new("Verso_Overcharge", "Overload", 10),
        new("Verso_PhantomStars", "Phantom Stars", 11),
        new("Verso_FromFire", "From Fire", 2),
        new("Verso_MarkingShot", "Marking Shot", 2),
        new("Verso_PerfectRecovery", "Perfect Recovery", 3),
        new("Verso_Purification", "Purification", 4),
        new("Verso_Blitz", "Blitz", 7),
        new("Verso_StrikeStorm", "Strike Storm", 9),
        new("Verso_SpeedBurst", "Speed Burst", 6),
        new("Verso_DefiantStrike", "Blood Strike", 10),
        new("Verso_EndBringer", "End Bringer", 13),
        new("Verso_SteeledStrike", "Steeled Strike", 15),
    };

    // Per-hit damage rows (0.75×clamp ÷ hits; Speed Burst + Blood Strike 1.5×).
    // Blitz plays twice = 1 budgeted hit (second play is free).
    private static double PH(int ap, int hits) => PerHit(ap, hits);
    private static double PH15(int ap, int hits) => 1.5 * Clamp(ap) / hits;

    public static int Apply(string modDir)
    {
        int n = 0;
        n += Rebalance(modDir);
        n += CloneSharedRows(modDir);
        n += PatchDataAssets(modDir);
        n += PatchElements(modDir);
        n += PatchNamesAndDescriptions(modDir);
        n += SwapRankGates(modDir);
        n += PatchPayoffs(modDir);
        n += CreateAssaultZero(modDir);
        n += PatchGraph(modDir);
        n += ApplyNewMoves(modDir);
        Console.WriteLine($"[Verso] staged {n} asset edit(s)");
        return n;
    }

    // ──────────────────────────────────────────────────────────────────
    // A. Balancing rows (Verso-exclusive rows edited in place)
    // ──────────────────────────────────────────────────────────────────

    private sealed record RowEdit(string Row, double Value);

    private static readonly RowEdit[] RebalanceRows =
    {
        // Paradigm Shift — 4 AP, 3 hits. 0.75×clamp(4)=0.99 ÷ 3 = 0.33.
        // Rank B: +3 AP (was C: +1).
        new("ParadigmShift_BaseDamage", PH(4, 3)),
        new("ParadigmShift_OverchargeAPGain", 3.0),
        // Leadership — 5 AP support. Rank B: +3 AP (was C: +1). Base kept.
        new("Leadership_OverchargeApGain", 3.0),
        // Quick Strike — 3 AP, 1 hit. 0.75×clamp(3)=0.7425.
        // D-perfection bonus dropped: grant row zeroed (branch left intact).
        new("QuickStrike_BaseDamage", PH(3, 1)),
        new("QuickStrike_PerfectionBonus", 0.0),
        // Follow Up — 4 AP, 1 hit, Fire. 0.99. 10%/shot ×21, S +5%/shot.
        new("Followup_BaseDamage", PH(4, 1)),
        new("Followup_DamageIncreasePerShot", 0.10),
        new("Followup_MaxFreeAimAmount", 21.0),
        // Ascending Assault — 5 AP, 1 hit. 1.2375. 10%/cast, capped at 21 casts.
        new("AscendingAssault_BaseDamage", PH(5, 1)),
        new("AscendingAssault_DamageIncreasePerCast", 0.10),
        new("AscendingAssault_MaxCastCount", 21.0),
        // Radiant Slash — 6 AP AoE, 1 hit. 1.485. Rank B +50% (mult kept).
        new("RadiantSlash_BaseDamage", PH(6, 1)),
        // Light Holder — 3 AP, 5 hits. 0.75×clamp(3)=0.7425 ÷ 5 = 0.1485.
        // Rank A: ×3 damage (replaces +2 AP).
        new("LightHolder_BaseDamage", PH(3, 5)),
        // Perfect Break — 8 AP, 1 hit. 1.98. B-cost dropped, S-on-Break kept.
        new("PerfectBreak_BaseDamage", PH(8, 1)),
        // Berserk Slash — 9 AP, 3 hits. Exactly 0.75xCurve(9)/3 = 0.7425.
        // Missing-HP bonus capped at 2× in BP; Rank S +50% (mult kept).
        new("BerserkSlash_BaseDamage", PH(9, 3)),
        // Phantom Stars — 11 AP, 5 hits. 3.6362 ÷ 5 = 0.7272.
        // Rank S: Slow 5 (replaces 5-AP cost).
        new("PhantomStars_BaseDamage", PH(11, 5)),
        // Marking Shot is shared with Gustave → cloned (see CloneSharedRows).
        // Perfect Recovery — 30% base, C +20% (was 50% → 100%).
        new("PerfectRecovery_BaseHeal", 0.30),
        new("PerfectRecovery_OverchargeHealMultiplier", 1.6667),
        // Purification — 4 AP, 2 hits. 0.99 ÷ 2 = 0.495. Rank B +50%.
        new("Purification_BaseDamage", PH(4, 2)),
        new("Purification_OverchargeDamageMultiplier", 1.5),
        // Blitz — 7 AP, 1 hit (second play free). 1.7325. B +50% kept.
        new("Blitz_BaseDamage", PH(7, 1)),
        // Speed Burst — 6 AP, 5 hits at 1.5× curve. 2.97 ÷ 5 = 0.594.
        // Rank B: −50% (was C: ×2.0).
        new("SpeedBurst_BaseDamage", 0.59),
        new("SpeedBurst_OverchargeDamageMultiplier", 0.5),
        // Blood Strike (Defiant) — 10 AP, 2 hits at 1.5×. 5.715 ÷ 2 = 2.86.
        // No HP cost by default; Rank A absorbs 50% of Max HP for ×2.
        new("DefiantStrike_BaseDamage", 2.86),
        new("DefiantStrike_HpPercentLoss", 0.50),
        new("DefiantStrike_OverchargeDamageMultiplier", 2.0),
        // End Bringer — 13 AP, 6 hits. 5.8763 ÷ 6 = 0.9794.
        // Stun bonus 3× → 2×; reapply moved A → S.
        new("EndBringer_BaseDamage", PH(13, 6)),
        new("EndBringer_StunDamageMultiplier", 2.0),
        // Steeled Strike — 15 AP, 13 hits, delayed a full turn (interruptible).
        // The delay is paid for with a baked-in triple: 0.49 x3 = 1.47 per hit
        // (19.11 total). Rank S ×10 on top (×30 total).
        new("SteeledStrike_BaseDamage", 1.47),
        new("SteeledStrike_OverchargeDamageMultiplier", 10.0),
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
        Console.WriteLine($"[Verso] rebalanced {RebalanceRows.Length} balancing row(s)");
        return n;
    }

    // ──────────────────────────────────────────────────────────────────
    // B. Shared-row clones (Gustave safety: FromFire, MarkingShot,
    //    StrikeStorm, Powerful) + staged Verso BP retargets
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Plain row → Verso-exclusive clone + new value.</summary>
    private sealed record SharedClone(string Donor, string To, double Value);

    private static readonly SharedClone[] SharedClones =
    {
        // From Fire — 2 AP, 3 hits. 0.495 ÷ 3 = 0.165.
        // B damage bonus dropped (mult pinned 1.0); heal 20%, B +20%.
        new("FromFire_BaseDamage", "FromFire_Verso_BaseDamage", PH(2, 3)),
        new("FromFire_BreakBarMultiplier", "FromFire_Verso_BreakBarMultiplier", 0.5),
        new("FromFire_OverchargeDamageMultiplier", "FromFire_Verso_OverchargeDamageMultiplier", 1.0),
        new("FromFire_HealPercentage", "FromFire_Verso_HealPercentage", 0.20),
        // Marking Shot — 2 AP, 1 hit. 0.495. Rank C +25% (was ×2.0).
        new("MarkingShot_BaseDamage", "MarkingShot_Verso_BaseDamage", PH(2, 1)),
        new("MarkingShot_BreakBarMultiplier", "MarkingShot_Verso_BreakBarMultiplier", 1.0),
        new("MarkingShot_OverchargeDamageMultiplier", "MarkingShot_Verso_OverchargeDamageMultiplier", 1.25),
        // Strike Storm — 9 AP, 5 hits. 2.2275 ÷ 5 = 0.4455. Rank A +50%.
        new("StrikeStorm_BaseDamage", "StrikeStorm_Verso_BaseDamage", PH(9, 5)),
        new("StrikeStorm_BreakBarMultiplier", "StrikeStorm_Verso_BreakBarMultiplier", 0.3),
        new("StrikeStorm_OverchargeDamageMultiplier", "StrikeStorm_Verso_OverchargeDamageMultiplier", 1.5),
        // Powerful — 2 AP. Durations 3→2 base, 5→4 at B (was A: 5).
        new("Powerful_BasePowerfuLDuration", "Powerful_Verso_BasePowerfuLDuration", 2.0),
        new("Powerful_IncreasedPowerfulDuration", "Powerful_Verso_IncreasedPowerfulDuration", 4.0),
        new("Powerful_PerfectionPerQte", "Powerful_Verso_PerfectionPerQte", 1.0),
    };

    /// <summary>Staged Verso BP → (donor row → clone row) retargets.</summary>
    private static readonly (string BpStem, string Donor, string Clone)[] SharedRetargets =
    {
        ("BP_Battle_SkillScript_Verso_FromFire", "FromFire_BaseDamage", "FromFire_Verso_BaseDamage"),
        ("BP_Battle_SkillScript_Verso_FromFire", "FromFire_BreakBarMultiplier", "FromFire_Verso_BreakBarMultiplier"),
        ("BP_Battle_SkillScript_Verso_FromFire", "FromFire_OverchargeDamageMultiplier", "FromFire_Verso_OverchargeDamageMultiplier"),
        ("BP_Battle_SkillScript_Verso_FromFire", "FromFire_HealPercentage", "FromFire_Verso_HealPercentage"),
        ("BP_Battle_SkillScript_MarkingShot", "MarkingShot_BaseDamage", "MarkingShot_Verso_BaseDamage"),
        ("BP_Battle_SkillScript_MarkingShot", "MarkingShot_BreakBarMultiplier", "MarkingShot_Verso_BreakBarMultiplier"),
        ("BP_Battle_SkillScript_MarkingShot", "MarkingShot_OverchargeDamageMultiplier", "MarkingShot_Verso_OverchargeDamageMultiplier"),
        ("BP_Battle_SkillScript_Verso_StrikeStorm", "StrikeStorm_BaseDamage", "StrikeStorm_Verso_BaseDamage"),
        ("BP_Battle_SkillScript_Verso_StrikeStorm", "StrikeStorm_BreakBarMultiplier", "StrikeStorm_Verso_BreakBarMultiplier"),
        ("BP_Battle_SkillScript_Verso_StrikeStorm", "StrikeStorm_OverchargeDamageMultiplier", "StrikeStorm_Verso_OverchargeDamageMultiplier"),
        ("BP_Battle_SkillScript_Powerful", "Powerful_BasePowerfuLDuration", "Powerful_Verso_BasePowerfuLDuration"),
        ("BP_Battle_SkillScript_Powerful", "Powerful_IncreasedPowerfulDuration", "Powerful_Verso_IncreasedPowerfulDuration"),
        ("BP_Battle_SkillScript_Powerful", "Powerful_PerfectionPerQte", "Powerful_Verso_PerfectionPerQte"),
    };

    private static int CloneSharedRows(string modDir)
    {
        var balSrc = FindTables(BalTable);
        Stage(balSrc, modDir, out var balDest);
        var ed = new DataTableEditor(balDest);
        int n = 0;
        foreach (var c in SharedClones)
        {
            ed.CloneRow(c.Donor, c.To);
            n += ed.Apply($"{{\"{c.To}\":{{\"FloatValue\":{c.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}}}}}");
        }
        ed.Save(balDest);
        foreach (var (bpStem, donor, clone) in SharedRetargets)
            n += RetargetBpRow(bpStem, donor, clone, modDir);
        Console.WriteLine($"[Verso] cloned {SharedClones.Length} shared row(s) + {SharedRetargets.Length} retarget(s)");
        return n;
    }

    /// <summary>Retargets one balancing-row read inside a staged BP (Gustave
    /// pattern: only the staged Verso copy is touched).</summary>
    private static int RetargetBpRow(string bpStem, string donor, string clone, string modDir)
    {
        var src = FindBp(bpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        int n = 0;
        foreach (var fn in bp.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            bool changed = false;
            foreach (var s in fn.ScriptBytecode)
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (changed || e is not EX_NameConst nc || nc.Value.ToString() != donor) return;
                    nc.Value = FName.FromString(bp, clone);
                    changed = true;
                });
            if (changed)
            {
                MayhemFixKit.RecalcContextSkips(bp, fn);
                n++;
            }
        }
        if (n > 0) bp.Write(dest);
        return n;
    }

    // ──────────────────────────────────────────────────────────────────
    // C. DA costs (+ Blood Strike rename is ST-only; NameID kept stable)
    // ──────────────────────────────────────────────────────────────────

    private static readonly (string DaStem, string Prop, string JsonValue)[] DaEdits =
    {
        ("DA_Skill_Powerful", "APCost", "2"),
        ("DA_Skill_Verso_NEW_ParadigmShift", "APCost", "4"),
        ("DA_Skill_Verso_NEW_Leadership", "APCost", "5"),
        ("DA_Skill_Verso_NEW_QuickStrike", "APCost", "3"),
        ("DA_Skill_Verso_NEW_Followup", "APCost", "4"),
        ("DA_Skill_Verso_AscendingAssault", "APCost", "5"),
        ("DA_Skill_Verso_RadiantSlash", "APCost", "6"),
        ("DA_Skill_NEW_4", "APCost", "3"),
        ("DA_Skill_Verso_PerfectBreak", "APCost", "8"),
        ("DA_Skill_Verso_BerserkSlash", "APCost", "9"),
        ("DA_Skill_Verso_NEW_Overcharge", "APCost", "10"),
        ("DA_Skill_Verso_NEW_PhantomStars", "APCost", "11"),
        ("DA_Skill_Verso_NEW_FromFire", "APCost", "2"),
        // Marking Shot 2 (unchanged), Perfect Recovery 3 (unchanged).
        ("DA_Skill_Verso_NEW_Purification", "APCost", "4"),
        ("DA_Skill_Blitz", "APCost", "7"),
        ("DA_Skill_Verso_Strikestorm", "APCost", "9"),
        ("DA_Skill_Verso_DefiantStrike", "APCost", "10"),
        ("DA_Skill_Verso_Endbringer", "APCost", "13"),
        ("DA_Skill_Verso_SteeledStrike", "APCost", "15"),
        // Speed Burst 6 (unchanged).
    };

    private static int PatchDataAssets(string modDir)
    {
        int n = 0;
        foreach (var (stem, prop, jsonValue) in DaEdits)
        {
            var src = FindDa(stem);
            Stage(src, modDir, out var dest);
            var ed = DataTableEditor.OpenPropertyAsset(dest);
            n += ed.ApplyToProperties($"{{\"{prop}\":{jsonValue}}}");
            ed.Save(dest);
        }
        Console.WriteLine($"[Verso] patched {DaEdits.Length} DA cost(s)");
        return n;
    }

    // ──────────────────────────────────────────────────────────────────
    // D. Element rethemes (DA enum + BP first-ByteConst; Maelle pattern).
    //    Follow Up Light(7) → Fire(2); Speed Burst Light(7) → Lightning(4).
    // ──────────────────────────────────────────────────────────────────

    private static readonly (string DaStem, string BpStem, string Element)[] ElementRethemes =
    {
        ("DA_Skill_Verso_NEW_Followup", "BP_Battle_SkillScript_Verso_Followup", "Fire"),
        ("DA_Skill_Verso_NEW_Speedburst", "BP_Battle_SkillScript_Verso_SpeedBurst", "Lightning"),
    };

    private static int PatchElements(string modDir)
    {
        int n = 0;
        foreach (var (daStem, bpStem, element) in ElementRethemes)
        {
            n += RethemeDaElement(daStem, element, modDir);
            n += RethemeBpElement(bpStem, element, modDir);
        }
        return n;
    }

    private static int RethemeDaElement(string daStem, string element, string modDir)
    {
        var src = FindDa(daStem);
        Stage(src, modDir, out var dest);
        var da = Open(dest);
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
        Console.WriteLine($"[Verso] {daStem} BaseElementalType -> {element}");
        return n;
    }

    private static int RethemeBpElement(string bpStem, string element, string modDir)
    {
        var src = FindBp(bpStem);
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
                    if (changed || e is not EX_LocalVirtualFunction vf || vf.VirtualFunctionName.ToString() != "MakeDamageBuilder") return;
                    foreach (var p in vf.Parameters)
                        if (p is EX_ByteConst bc)
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
        Console.WriteLine($"[Verso] {bpStem} MakeDamageBuilder element -> {element} ({n} fn(s))");
        return n;
    }

    // ──────────────────────────────────────────────────────────────────
    // E. Names + descriptions (ST_MainCharacters_Skills; Lune-style gold
    //    per-hit first lines, vanilla voice, rank-icon lines)
    // ──────────────────────────────────────────────────────────────────

    private sealed record StringEdit(string Key, string Name, string Long, string Short);

    private static readonly StringEdit[] StringEdits =
    {
        new("Verso_AssaultZero", "Assault Zero",
            $"Deals {Gold(PH(1, 5))} single target damage. 5 hits.\nUses weapon's element.\n{Crits} generate 1 additional {Perfection}.\n{Rank('C')}: deal 100% more damage.",
            $"Deals {Gold(PH(1, 5))} damage. 5 hits.\nUses weapon's element.\n{Crits} generate 1 additional {Perfection}.\n{Rank('C')}: deal 100% more damage."),
        new("Verso_Powerful", "Powerful",
            $"Applies {Powerful} to 1-5 allies for 2 turns, increasing the damage they deal.\nGives 0-2 {Perfection}.\n{Rank('B')}: Duration increased to 4 turns.",
            $"Applies {Powerful} to 1-5 allies for 2 turns.\nGives 0-2 {Perfection}.\n{Rank('B')}: Duration increased to 4 turns."),
        new("Verso_ParadigmShift", "Paradigm Shift",
            $"Deals {Gold(PH(4, 3))} single target {Physical} damage and gives 1-3 {AP} back. 3 hits.\n{Rank('B')}: +3 {AP}.",
            $"Deals {Gold(PH(4, 3))} {Physical} damage and gives 1-3 {AP} back. 3 hits.\n{Rank('B')}: +3 {AP}."),
        new("Verso_Leadership", "Leadership",
            $"Reduces Verso's Rank.\nGives 2-4 {AP} to other allies.\n{Rank('B')}: +3 more {AP} to other allies.",
            $"Reduces Verso's Rank. Gives 2-4 {AP} to other allies.\n{Rank('B')}: +3 more {AP} to other allies."),
        new("Verso_QuickStrike", "Quick Strike",
            $"Deals {Gold(PH(3, 1))} single target {Physical} damage. 1 hit.",
            $"Deals {Gold(PH(3, 1))} {Physical} damage. 1 hit."),
        new("Verso_Followup", "Follow Up",
            $"Deals {Gold(PH(4, 1))} single target {Fire} damage. 1 hit.\nDeal 10% more damage for each Free Aim shot this turn, up to 21.\n{Rank('S')}: deal 5% more damage per shot.",
            $"Deals {Gold(PH(4, 1))} {Fire} damage. 1 hit.\nDeal 10% more damage for each Free Aim shot this turn, up to 21.\n{Rank('S')}: deal 5% more damage per shot."),
        new("Verso_AscendingAssault", "Ascending Assault",
            $"Deals {Gold(PH(5, 1))} single target damage. 1 hit.\nUses weapon's element.\nDeal 10% more damage at each cast, up to 21 casts.\n{Rank('S')}: deal 5% more damage per cast.",
            $"Deals {Gold(PH(5, 1))} damage. 1 hit.\nUses weapon's element.\nDeal 10% more damage at each cast, up to 21 casts.\n{Rank('S')}: deal 5% more damage per cast."),
        new("Verso_RadiantSlash", "Radiant Slash",
            $"Deals {Gold(PH(6, 1))} {Light} damage to all enemies. 1 hit.\nCan {Break}.\n{Rank('B')}: deal 50% more damage.",
            $"Deals {Gold(PH(6, 1))} {Light} damage to all enemies. 1 hit.\nCan {Break}.\n{Rank('B')}: deal 50% more damage."),
        new("Verso_LightHolder", "Light Holder",
            $"Deals {Gold(PH(3, 5))} single target {Light} damage. 5 hits.\nGains 1 Rank on completion.\n{Rank('A')}: deal 200% more damage.",
            $"Deals {Gold(PH(3, 5))} {Light} damage. 5 hits. Gains 1 Rank on completion.\n{Rank('A')}: deal 200% more damage."),
        new("Verso_PerfectBreak", "Perfect Break",
            $"Deals {Gold(PH(8, 1))} single target {Light} damage. 1 hit.\nCan {Break}.\n{Break} sets Rank to {Rank('S')}.",
            $"Deals {Gold(PH(8, 1))} {Light} damage. 1 hit.\nCan {Break}.\n{Break} sets Rank to {Rank('S')}."),
        new("Verso_BerserkSlash", "Berserk Slash",
            $"Deals {Gold(PH(9, 3))} single target {Physical} damage. 3 hits.\nDeal more damage for each missing Health, up to +100%.\n{Rank('S')}: deal 50% more damage.",
            $"Deals {Gold(PH(9, 3))} {Physical} damage. 3 hits.\nDeal more damage for each missing Health, up to +100%.\n{Rank('S')}: deal 50% more damage."),
        new("Verso_Overcharge", "Overload",
            $"Increases Rank to {Rank('S')} but sets self-Health to 1.\nApplies 1 {Berserk} to self for 5 turns.",
            $"Increases Rank to {Rank('S')} but sets self-Health to 1.\nApplies 1 {Berserk} to self for 5 turns."),
        new("Verso_PhantomStars", "Phantom Stars",
            $"Deals {Gold(PH(11, 5))} {Light} damage to all enemies. 5 hits.\nCan {Break}.\n{Rank('S')}: Inflicts {Slow} for 5 turns.",
            $"Deals {Gold(PH(11, 5))} {Light} damage to all enemies. 5 hits.\nCan {Break}.\n{Rank('S')}: Inflicts {Slow} for 5 turns."),
        new("Verso_FromFire", "From Fire",
            $"Deals {Gold(PH(2, 3))} single target damage. 3 hits.\nUses weapon's element.\n{Heal} self for 20% Health if the target {Burn}.\n{Rank('B')}: {Heal} self for an additional 20%.",
            $"Deals {Gold(PH(2, 3))} damage. 3 hits.\nUses weapon's element.\n{Heal} self for 20% Health if the target {Burn}.\n{Rank('B')}: {Heal} self for an additional 20%."),
        new("Verso_MarkingShot", "Marking Shot",
            $"Deals {Gold(PH(2, 1))} single target damage. 1 hit.\nUses weapon's element.\nApplies {Mark}.\n{Rank('C')}: deal 25% more damage.",
            $"Deals {Gold(PH(2, 1))} damage. 1 hit.\nUses weapon's element.\nApplies {Mark}.\n{Rank('C')}: deal 25% more damage."),
        new("Verso_PerfectRecovery", "Perfect Recovery",
            $"{Recovers} 30% Health and dispels Status Effects.\nGives 0-2 {Perfection}.\n{Rank('C')}: {Recovers} an additional 20%.",
            $"{Recovers} 30% Health and dispels Status Effects.\nGives 0-2 {Perfection}.\n{Rank('C')}: {Recovers} an additional 20%."),
        new("Verso_Purification", "Purification",
            $"Deals {Gold(PH(4, 2))} single target {Light} damage. 2 hits.\nDispels Verso's status effects.\n{Rank('B')}: deal 50% more damage.",
            $"Deals {Gold(PH(4, 2))} {Light} damage. 2 hits.\nDispels Verso's status effects.\n{Rank('B')}: deal 50% more damage."),
        new("Verso_Blitz", "Blitz",
            $"Deals {Gold(PH(7, 1))} single target {Physical} damage. 1 hit.\nPlays twice. Kills non-boss enemies with less than 10% Health.\n{Rank('B')}: deal 50% more damage.",
            $"Deals {Gold(PH(7, 1))} {Physical} damage. 1 hit.\nPlays twice. Kills non-boss enemies with less than 10% Health.\n{Rank('B')}: deal 50% more damage."),
        new("Verso_StrikeStorm", "Strike Storm",
            $"Deals {Gold(PH(9, 5))} single target damage. 5 hits.\nUses weapon's element.\n{Crits} generate 2 additional {Perfection}.\n{Rank('A')}: deal 50% more damage.",
            $"Deals {Gold(PH(9, 5))} damage. 5 hits.\nUses weapon's element.\n{Crits} generate 2 additional {Perfection}.\n{Rank('A')}: deal 50% more damage."),
        new("Verso_SpeedBurst", "Speed Burst",
            $"Deals {Gold(0.59)} single target {Lightning} damage. 5 hits.\nDamage increased by the Speed difference with the target.\n{Rank('B')}: Damage reduced by 50%.",
            $"Deals {Gold(0.59)} {Lightning} damage. 5 hits.\nDamage increased by the Speed difference with the target.\n{Rank('B')}: Damage reduced by 50%."),
        new("Verso_DefiantStrike", "Blood Strike",
            $"Deals {Gold(2.86)} single target {Physical} damage that applies {Mark}. 2 hits.\n{Rank('A')}: Absorbs 50% of maximum Health: deal 100% more damage. Requires 50% of maximum Health.",
            $"Deals {Gold(2.86)} {Physical} damage that applies {Mark}. 2 hits.\n{Rank('A')}: Absorbs 50% of maximum Health: deal 100% more damage. Requires 50% of maximum Health."),
        new("Verso_EndBringer", "End Bringer",
            $"Deals {Gold(PH(13, 6))} single target {Physical} damage. 6 hits.\nDeal 100% more damage if the target is {Stunned}.\n{Rank('S')}: Can reapply {Stun}.",
            $"Deals {Gold(PH(13, 6))} {Physical} damage. 6 hits.\nDeal 100% more damage if the target is {Stunned}.\n{Rank('S')}: Can reapply {Stun}."),
        new("Verso_SteeledStrike", "Steeled Strike",
            $"After 1 turn, deals {Gold(1.47)} single target {Physical} damage. 13 hits.\nInterrupted if any damage taken.\n{Rank('S')}: ×10 damage (×30 total).",
            $"After 1 turn, deals {Gold(1.47)} {Physical} damage. 13 hits.\nInterrupted if any damage taken.\n{Rank('S')}: ×10 damage (×30 total)."),
    };

    /// <summary>Editor preview: the modded Long description for one
    /// string-table base key. Wired into the graph editor tooltips.</summary>
    public static bool TryPreviewDescription(string baseKey, out string longText)
    {
        foreach (var s in StringEdits)
        {
            if (!string.Equals(s.Key, baseKey, StringComparison.Ordinal)) continue;
            longText = s.Long;
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
        foreach (var s in StringEdits)
        {
            if (s.Key == "Verso_AssaultZero") continue; // created with the custom skill
            entries[s.Key + "_Name"] = s.Name;
            entries[s.Key + "_Description_Long"] = s.Long;
            entries[s.Key + "_Description_Short"] = s.Short;
        }
        DataTableEditor.AddStringTableEntries(stDest, entries);
        Console.WriteLine($"[Verso] staged {entries.Count} string-table entries");
        return entries.Count;
    }

    // ── asset helpers ──

    private static UAsset Open(string p)
    {
        var map = DataTableEditor.Mappings;
        return map != null ? new UAsset(p, EngineVersion.VER_UE5_4, map) : new UAsset(p, EngineVersion.VER_UE5_4);
    }

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
            ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "probe-verso"), stem + ".uasset")
            ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "probe-skill"), stem + ".uasset")
            ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "extracted"), stem + ".uasset")
            ?? FileCopy.FindTablesAsset(stem + ".uasset")
            ?? throw new FileNotFoundException(stem + ".uasset");

    private static string FindDa(string stem) => FindTables(stem);
    private static string FindBp(string stem) => FindTables(stem);

    private static void Stage(string src, string modDir, out string dest)
    {
        dest = StageDest(src, modDir);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);
    }

    private static string PtrName(KismetPropertyPointer? p)
    {
        if (p?.New?.Path is { Length: > 0 } path)
            return string.Join(".", path.Select(n => n.ToString()));
        return p?.Old?.Index.ToString() ?? "?";
    }

    private static bool CallsVirtual(KismetExpression s, string name)
    {
        bool hit = false;
        MayhemFixKit.Walk(new[] { s }, e =>
        {
            if (hit) return;
            if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == name) hit = true;
        });
        return hit;
    }

    private static bool HasFinalCall(UAsset bp, KismetExpression s, string fnName)
    {
        bool hit = false;
        MayhemFixKit.Walk(new[] { s }, e =>
        {
            if (hit) return;
            if (e is EX_FinalFunction ff && ff.StackNode.IsImport()
                && ff.StackNode.ToImport(bp).ObjectName.ToString() == fnName) hit = true;
        });
        return hit;
    }

// __CHUNK2__

    // ──────────────────────────────────────────────────────────────────
    // F. Rank-gate swap: every SkillState.IsOvercharged read in a staged
    //    BP becomes an explicit CurrentRank >= threshold check
    //    (GetRankValue + GreaterEqual_IntInt = the IsOverchargeActive shape).
    // ──────────────────────────────────────────────────────────────────

    private const string CompPkg = "/Game/Gameplay/Battle/Perfection/BP_PerfectionMechanicComponent";
    private const string CompCls = "BP_PerfectionMechanicComponent_C";

    private static readonly (string BpStem, byte Threshold)[] GateSwaps =
    {
        ("BP_Battle_SkillScript_Powerful", RankB),
        ("BP_Battle_SkillScript_Verso_ParadigmShift", RankB),
        ("BP_Battle_SkillScript_Verso_Leadership", RankB),
        ("BP_Battle_SkillScript_Verso_RadiantSlash", RankB),
        ("BP_Battle_SkillScript_Verso_BerserkSlash", RankS),
        ("BP_Battle_SkillScript_Verso_FromFire", RankB),
        ("BP_Battle_SkillScript_MarkingShot", RankC),
        ("BP_Battle_SkillScript_PerfectRecovery", RankC),
        ("BP_Battle_SkillScript_NEW_15", RankB),
        ("BP_Battle_SkillScript_Blitz", RankB),
        ("BP_Battle_SkillScript_Verso_StrikeStorm", RankA),
        ("BP_Battle_SkillScript_Verso_SpeedBurst", RankB),
        ("BP_Battle_SkillScript_Verso_DefiantStrike", RankA),
        ("BP_Battle_SkillScript_Verso_EndBringer", RankS),
        ("BP_Battle_SkillScript_Verso_SteeledStrike", RankS),
    };

    /// <summary>Is this the SkillState.IsOvercharged member read?</summary>
    private static bool IsOverchargedRead(KismetExpression e)
    {
        if (e is not EX_Context ctx) return false;
        return ctx.ObjectExpression is EX_InstanceVariable oiv && PtrName(oiv.Variable) == "SkillState"
            && ctx.ContextExpression is EX_InstanceVariable civ && PtrName(civ.Variable) == "IsOvercharged";
    }

    /// <summary>Reflection mapper over kismet trees (mirrors MayhemFixKit.Walk
    /// traversal: fields + properties, single + arrays).</summary>
    private static KismetExpression MapExpr(KismetExpression root, Func<KismetExpression, KismetExpression?> swap)
    {
        var r = swap(root);
        if (r is not null) return r;
        const System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        foreach (var f in root.GetType().GetFields(bf))
        {
            var v = f.GetValue(root);
            if (v is KismetExpression ke) f.SetValue(root, MapExpr(ke, swap));
            else if (v is KismetExpression[] arr)
                f.SetValue(root, arr.Select(x => x is null ? null : MapExpr(x, swap)).ToArray()!);
        }
        foreach (var p in root.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            if (p.GetIndexParameters().Length > 0 || p.Name is "Token" or "Inst" or "RawValue" or "Tag") continue;
            if (!p.CanRead || !p.CanWrite) continue;
            object? v;
            try { v = p.GetValue(root); } catch { continue; }
            if (v is KismetExpression ke) p.SetValue(root, MapExpr(ke, swap));
            else if (v is KismetExpression[] arr)
                p.SetValue(root, arr.Select(x => x is null ? null : MapExpr(x, swap)).ToArray()!);
        }
        return root;
    }

    private static EX_InstanceVariable CloneMember(UAsset bp, List<KismetExpression> stmts, string name, string what)
    {
        EX_InstanceVariable? hit = null;
        int n = 0;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_InstanceVariable iv && PtrName(iv.Variable) == name) { hit ??= iv; n++; }
            });
        if (hit is null)
            throw new InvalidDataException($"{what}: no in-asset {name} read to clone");
        return (EX_InstanceVariable)MayhemFixKit.CloneKismet(hit);
    }

    /// <summary>An explicit rank gate: a comp-fetch prelude statement plus a
    /// factory for the boolean check expression. The check calls NO virtual
    /// functions except GetComponentByClass (dispatched on the character,
    /// crash-log-proven safe); the rank bytes compare directly with
    /// GreaterEqual_ByteByte (E_Perfection_Ranks order None0 D1 C2 B3 A4 S5
    /// is SetSpecificRank-proven). Every context mirrors a vanilla shape:
    /// LetObj store (Leadership #1), nested object fetch (component
    /// GetOwner pattern), member read with member RValue (Defiant #37 /
    /// component #8).</summary>
    private sealed record RankGate(KismetExpression Prelude, Func<KismetExpression> Check);

    private static EX_LocalVariable EnsureGateLocal(UAsset bp, FunctionExport fn, string what)
    {
        const string name = "CallFunc_GetComponentByClass_VersoGate";
        var destOwner = FPackageIndex.FromExport(bp.Exports.IndexOf(fn));
        var props = (fn.LoadedProperties ?? Array.Empty<UAssetAPI.FieldTypes.FProperty>()).ToList();
        if (!props.Any(p => p.Name.ToString() == name))
        {
            // Function locals live in the FProperty hierarchy (FObjectProperty),
            // not the asset PropertyData one — match by type name.
            var tpl = props.FirstOrDefault(p => p.GetType().Name == "FObjectProperty")
                ?? bp.Exports.OfType<FunctionExport>()
                    .SelectMany(f => f.LoadedProperties ?? Array.Empty<UAssetAPI.FieldTypes.FProperty>())
                    .FirstOrDefault(p => p.GetType().Name == "FObjectProperty")
                ?? throw new InvalidDataException($"{what}: no object local template for gate");
            var prop = (UAssetAPI.FieldTypes.FProperty)tpl.GetType().GetMethod("MemberwiseClone",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(tpl, null)!;
            prop.Name = FName.FromString(bp, name);
            props.Add(prop);
            fn.LoadedProperties = props.ToArray();
        }
        return new EX_LocalVariable
        {
            Variable = new KismetPropertyPointer(new FFieldPath(
                new[] { FName.FromString(bp, name) }, destOwner)),
        };
    }

    private static RankGate BuildRankGate(UAsset bp, FunctionExport? fn, List<KismetExpression> stmts,
        byte threshold, string what)
    {
        var compCls = LuneSkillsKit.EnsureObjectImport(bp, CompPkg, CompCls, "BlueprintGeneratedClass", "/Script/Engine");
        var geFn = LuneSkillsKit.EnsureKismetMathFn(bp, "GreaterEqual_ByteByte");
        var charTpl = CloneMember(bp, stmts, "CurrentCharacter", what);
        EX_LocalVariable? gateLocal = null;
        FPackageIndex? gateOwner = null;
        string gateName = "CallFunc_GetComponentByClass_VersoGate";
        if (fn is not null)
        {
            gateLocal = EnsureGateLocal(bp, fn, what);
            gateOwner = FPackageIndex.FromExport(bp.Exports.IndexOf(fn));
        }
        KismetPropertyPointer GatePtr() => gateOwner is null
            ? new KismetPropertyPointer(new FFieldPath())
            : new KismetPropertyPointer(new FFieldPath(
                new[] { FName.FromString(bp, gateName) }, gateOwner));
        KismetPropertyPointer RankPtr() => new(new FFieldPath(
            new[] { FName.FromString(bp, "CurrentRank") }, compCls));
        KismetExpression CompAcq() => new EX_Context
        {
            ObjectExpression = (KismetExpression)MayhemFixKit.CloneKismet(charTpl),
            ContextExpression = new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(bp, "GetComponentByClass"),
                Parameters = new KismetExpression[] { new EX_ObjectConst { Value = compCls } },
            },
            RValuePointer = GatePtr(),
        };
        KismetExpression Check() => new EX_CallMath
        {
            StackNode = geFn,
            Parameters = new KismetExpression[]
            {
                new EX_Context
                {
                    // Read the component the Prelude already stored, exactly like the
                    // vanilla IsOverchargeActive / Leadership shape. Chaining a fresh
                    // GetComponentByClass call here instead aborts the VM, killing the
                    // whole OnActionEffect (zero damage) for every rank-gated move.
                    ObjectExpression = gateLocal is null
                        ? CompAcq()
                        : (KismetExpression)MayhemFixKit.CloneKismet(gateLocal),
                    ContextExpression = new EX_InstanceVariable { Variable = RankPtr() },
                    RValuePointer = RankPtr(),
                },
                new EX_ByteConst { Value = threshold },
            },
        };
        KismetExpression Prelude() => gateLocal is null
            ? CompAcq()
            : new EX_LetObj
            {
                VariableExpression = gateLocal,
                AssignmentExpression = CompAcq(),
            };
        return new RankGate(Prelude(), Check);
    }

    private static int SwapRankGates(string modDir)
    {
        int n = 0;
        foreach (var (bpStem, threshold) in GateSwaps)
            n += SwapOneBp(bpStem, threshold, modDir);
        return n;
    }

    private static int SwapOneBp(string bpStem, byte threshold, string modDir)
    {
        // Staged clone first (Assault Zero does not exist in the tables), else vanilla.
        var src = FileCopy.FindUnder(modDir, bpStem + ".uasset") ?? FindBp(bpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        var fn = bp.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (fn?.ScriptBytecode is null)
            throw new InvalidDataException($"[Verso] {bpStem} has no OnActionEffect");
        var stmts = fn.ScriptBytecode.ToList();
        int hits = 0;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e => { if (IsOverchargedRead(e)) hit = true; });
            if (!hit) continue;
            var gate = BuildRankGate(bp, fn, stmts, threshold, bpStem);
            uint off = 0;
            for (int j = 0; j < i; j++) off += stmts[j].GetSize(bp);
            uint oldSize = stmts[i].GetSize(bp);
            var replaced = MapExpr(stmts[i], e => IsOverchargedRead(e) ? gate.Check() : null);
            uint newOne = replaced.GetSize(bp);
            uint preSize = gate.Prelude.GetSize(bp);
            var block = new List<KismetExpression> { gate.Prelude, replaced };
            stmts.RemoveAt(i);
            stmts.InsertRange(i, block);
            fn.ScriptBytecode = stmts.ToArray();
            long delta = (long)preSize + (long)newOne - (long)oldSize;
            if (delta > 0) MayhemFixKit.ShiftAbsolute(bp, fn, off, (uint)delta, null);
            else if (delta < 0) ShiftShrink(bp, fn, off, (uint)(-delta));
            MayhemFixKit.RecalcContextSkips(bp, fn);
            hits++;
            i++; // skip past the inserted prelude on rescan
        }
        if (hits == 0)
            throw new InvalidDataException($"[Verso] {bpStem}: no IsOvercharged read found");
        bp.Write(dest);
        Console.WriteLine($"[Verso] {bpStem}: {hits} rank gate(s) -> explicit >= {threshold}");
        return hits;
    }

    /// <summary>Negative-direction offset fixup (ShiftAbsolute only grows).</summary>
    private static void ShiftShrink(UAsset asset, FunctionExport fn, uint insertionPoint, uint delta)
    {
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref pos, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j when j.CodeOffset > insertionPoint:
                        j.CodeOffset -= delta; break;
                    case EX_JumpIfNot jn when jn.CodeOffset > insertionPoint:
                        jn.CodeOffset -= delta; break;
                    case EX_PushExecutionFlow pf when pf.PushingAddress > insertionPoint:
                        pf.PushingAddress -= delta; break;
                    case EX_SwitchValue sv:
                        if (sv.EndGotoOffset > insertionPoint) sv.EndGotoOffset -= delta;
                        for (int i = 0; i < sv.Cases.Length; i++)
                            if (sv.Cases[i].NextOffset > insertionPoint) sv.Cases[i].NextOffset -= delta;
                        break;
                }
            });
    }

// __CHUNK3__

    // ──────────────────────────────────────────────────────────────────
    // G. Payoff edits (conditional bonuses, cost-override removals,
    //    Light Holder AP-branch deletion, Overload rank + Berserk)
    // ──────────────────────────────────────────────────────────────────

    private static List<int> FindStmts(List<KismetExpression> stmts, Func<KismetExpression, bool> pred)
    {
        var idx = new List<int>();
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e => { if (!hit && pred(e)) hit = true; });
            if (hit) idx.Add(i);
        }
        return idx;
    }

    private static EX_LocalVariable CloneLocalRead(List<KismetExpression> stmts, string name, string what)
    {
        EX_LocalVariable? hit = null;
        int n = 0;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_LocalVariable lv && PtrName(lv.Variable) == name) { hit ??= lv; n++; }
            });
        if (hit is null)
            throw new InvalidDataException($"{what}: no local read '{name}'");
        return (EX_LocalVariable)MayhemFixKit.CloneKismet(hit);
    }

    private static KismetExpression CloneVarRead(List<KismetExpression> stmts, string name, string what)
    {
        KismetExpression? hit = null;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (hit is not null) return;
                if (e is EX_LocalVariable lv && PtrName(lv.Variable) == name) hit = lv;
                else if (e is EX_InstanceVariable iv && PtrName(iv.Variable) == name) hit = iv;
            });
        if (hit is null)
            throw new InvalidDataException($"{what}: no variable read '{name}'");
        return (KismetExpression)MayhemFixKit.CloneKismet(hit);
    }

    private static FPackageIndex CloneNamedCallImport(UAsset bp, List<KismetExpression> stmts, string callName, string what)
    {
        FPackageIndex? hit = null;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (hit is not null) return;
                if (e is EX_CallMath cm && cm.StackNode.IsImport()
                    && cm.StackNode.ToImport(bp).ObjectName.ToString() == callName) hit = cm.StackNode;
            });
        if (hit is null)
            throw new InvalidDataException($"{what}: no CallMath import '{callName}'");
        return hit;
    }

    /// <summary>Gated splice (Lune layout): [prelude] [JumpIfNot(cond) → end]
    /// + body, inserted after the anchor statement.</summary>
    private static void SpliceGated(UAsset bp, FunctionExport fn, int anchorIdx, KismetExpression cond,
        List<KismetExpression> body, string what, List<KismetExpression> prelude = null)
    {
        var stmts = fn.ScriptBytecode!.ToList();
        var jump = new EX_JumpIfNot { CodeOffset = 0, BooleanExpression = cond };
        uint jumpSize = (uint)jump.GetSize(bp);
        uint bodySize = 0;
        foreach (var s in body) bodySize += s.GetSize(bp);
        uint preSize = 0;
        if (prelude != null) foreach (var s in prelude) preSize += s.GetSize(bp);
        uint total = preSize + jumpSize + bodySize;
        uint insOff = 0;
        for (int i = 0; i <= anchorIdx; i++) insOff += stmts[i].GetSize(bp);
        var block = new List<KismetExpression>();
        if (prelude != null) block.AddRange(prelude);
        block.Add(jump);
        block.AddRange(body);
        stmts.InsertRange(anchorIdx + 1, block);
        fn.ScriptBytecode = stmts.ToArray();
        MayhemFixKit.ShiftAbsolute(bp, fn, insOff, total, null);
        jump.CodeOffset = insOff + total;
        MayhemFixKit.RecalcContextSkips(bp, fn);
        Console.WriteLine($"[Verso] {what}: gated splice +{total}B @ stmt {anchorIdx}");
    }

    /// <summary>Replaces one expression (predicate must match exactly once in
    /// the whole function) and shift-fixes the size delta.</summary>
    private static void ReplaceOne(UAsset bp, FunctionExport fn, Func<KismetExpression, bool> pred,
        Func<KismetExpression, KismetExpression> build, string what)
    {
        var stmts = fn.ScriptBytecode!.ToList();
        int found = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e => { if (!hit && pred(e)) hit = true; });
            if (hit)
            {
                if (found >= 0) throw new InvalidDataException($"{what}: multiple matches");
                found = i;
            }
        }
        if (found < 0) throw new InvalidDataException($"{what}: no match");
        uint off = 0;
        for (int j = 0; j < found; j++) off += stmts[j].GetSize(bp);
        uint oldSize = stmts[found].GetSize(bp);
        stmts[found] = MapExpr(stmts[found], e => pred(e) ? build(e) : null);
        uint newSize = stmts[found].GetSize(bp);
        fn.ScriptBytecode = stmts.ToArray();
        if (newSize > oldSize)
            MayhemFixKit.ShiftAbsolute(bp, fn, off, newSize - oldSize, null);
        else if (newSize < oldSize)
            ShiftShrink(bp, fn, off, oldSize - newSize);
        MayhemFixKit.RecalcContextSkips(bp, fn);
    }

    private static FunctionExport OnAction(UAsset bp, string bpStem)
        => bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect")
            ?? throw new InvalidDataException($"[Verso] {bpStem} has no OnActionEffect");

    private static int PatchPayoffs(string modDir)
    {
        int n = 0;
        n += NeutralizeCostOverrides(modDir);
        n += SuppressRankBadges(modDir);
        n += PatchBerserkCap(modDir);
        n += PatchLightHolder(modDir);
        n += PatchOverload(modDir);
        n += PatchFromFireHeal(modDir);
        n += PatchRecoveryBonus(modDir);
        n += PatchDefiantExtra(modDir);
        n += PatchFollowupBonus(modDir);
        n += PatchAscendingBonus(modDir);
        n += PatchPhantomSlow(modDir);
        return n;
    }

    // ── dropped cost discounts: Override=false (NewValue ignored) ──

    private static readonly (string BpStem, string Base)[] CostOverrides =
    {
        ("BP_Battle_SkillScript_Verso_Followup", "4"),
        ("BP_Battle_SkillScript_Verso_AscendingAssault", "5"),
        ("BP_Battle_SkillScript_Verso_PhantomStars", "11"),
        ("BP_Battle_SkillScript_Verso_PerfectBreak", "8"),
    };

    private static int NeutralizeCostOverrides(string modDir)
    {
        int n = 0;
        foreach (var (bpStem, _) in CostOverrides)
        {
            var src = FindBp(bpStem);
            Stage(src, modDir, out var dest);
            var bp = Open(dest);
            var fn = bp.Exports.OfType<FunctionExport>()
                .FirstOrDefault(f => f.ObjectName.ToString() == "GetSkillCostOverride")
                ?? throw new InvalidDataException($"[Verso] {bpStem} has no GetSkillCostOverride");
            var stmts = fn.ScriptBytecode!.ToList();
            int flips = 0;
            for (int i = 0; i < stmts.Count; i++)
            {
                bool hit = false;
                MayhemFixKit.Walk(new[] { stmts[i] }, e =>
                {
                    if (hit) return;
                    if (e is not EX_LetBool lb) return;
                    string target = lb.VariableExpression is EX_LocalVariable lv
                        ? PtrName(lv.Variable)
                        : lb.VariableExpression is EX_LocalOutVariable lov
                            ? PtrName(lov.Variable) : "";
                    if (target.Contains("Override", StringComparison.OrdinalIgnoreCase)) hit = true;
                });
                if (!hit) continue;
                uint off = 0;
                for (int j = 0; j < i; j++) off += stmts[j].GetSize(bp);
                uint oldSize = stmts[i].GetSize(bp);
                stmts[i] = MapExpr(stmts[i], e =>
                {
                    if (e is not EX_LetBool lb) return null;
                    string target = lb.VariableExpression is EX_LocalVariable lv
                        ? PtrName(lv.Variable)
                        : lb.VariableExpression is EX_LocalOutVariable lov
                            ? PtrName(lov.Variable) : "";
                    if (!target.Contains("Override", StringComparison.OrdinalIgnoreCase)) return null;
                    lb.AssignmentExpression = new EX_False();
                    flips++;
                    return lb;
                });
                uint newSize = stmts[i].GetSize(bp);
                fn.ScriptBytecode = stmts.ToArray();
                if (newSize > oldSize)
                    MayhemFixKit.ShiftAbsolute(bp, fn, off, newSize - oldSize, null);
                else if (newSize < oldSize)
                    ShiftShrink(bp, fn, off, oldSize - newSize);
                MayhemFixKit.RecalcContextSkips(bp, fn);
            }
            if (flips != 1)
                throw new InvalidDataException($"[Verso] {bpStem}: Override writer x{flips} (expect 1)");
            bp.Write(dest);
            Console.WriteLine($"[Verso] {bpStem}: cost override neutralized");
            n++;
        }
        return n;
    }

    // ── Stale rank badges off ──
    // The top-right texture badge (ModifySkillButton/ModifySkillTooltip on the
    // shared Perfection component) reads the instance OverchargeMinimumRank,
    // which resolves natively per skill with NO writable data anywhere (not
    // in DAs, graph nodes, rows, or tree assets — exhaustively verified).
    // After the respec those letters are wrong (Perfect Break still shows B
    // with no B gate left; Berserk Slash shows C with an S bonus). Forcing
    // the nonzero gate false selects the empty brush for every Verso skill;
    // the (correct, data-driven) description icons carry the real thresholds.
    // Only Verso skills use this component — nothing else is affected.

    private const string PerfectionComp = "BP_SkillDataComponent_PerfectionMechanic";

    private static int SuppressRankBadges(string modDir)
    {
        var src = FindTables(PerfectionComp);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        int n = 0;
        foreach (var fnName in new[] { "ModifySkillButton", "ModifySkillTooltip" })
        {
            var fn = bp.Exports.OfType<FunctionExport>()
                .FirstOrDefault(f => f.ObjectName.ToString() == fnName);
            if (fn?.ScriptBytecode is null) continue;
            int found = 0;
            foreach (var s in fn.ScriptBytecode)
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (e is EX_LetBool lb && lb.VariableExpression is EX_LocalVariable lv
                        && PtrName(lv.Variable) == "CallFunc_NotEqual_ByteByte_ReturnValue") found++;
                });
            if (found != 1)
                throw new InvalidDataException($"[Verso] {fnName}: badge gate x{found} (expect 1)");
            ReplaceOne(bp, fn,
                e => e is EX_LetBool lb && lb.VariableExpression is EX_LocalVariable lv2
                    && PtrName(lv2.Variable) == "CallFunc_NotEqual_ByteByte_ReturnValue",
                e =>
                {
                    ((EX_LetBool)e).AssignmentExpression = new EX_False();
                    return e;
                }, "badge gate");
            n++;
            Console.WriteLine($"[Verso] {fnName}: rank badge suppressed");
        }
        if (n != 2) throw new InvalidDataException($"[Verso] badge fns x{n} (expect 2)");
        bp.Write(dest);
        return n;
    }

    // ── Steeled Strike: untouched. Damage is Base x Hits x RankMult
    // (row 10.0 at S, Temp 1.0 otherwise); the triple lives in the row
    // (1.47 = 3x0.49) to pay for the 2-turn delay — no separate
    // "triple damage" sentence, the number already says it. ──

    // ── Berserk Slash: missing-HP mult capped at 2× via FClamp ──

    private static int PatchBerserkCap(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_Verso_BerserkSlash";
        var src = FindBp(bpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        var fn = OnAction(bp, bpStem);
        var clampFn = LuneSkillsKit.EnsureKismetMathFn(bp, "FClamp");
        bool done = false;
        ReplaceOne(bp, fn,
            e => e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "AddFinalDamageMultiplier",
            e =>
            {
                var vf = (EX_LocalVirtualFunction)e;
                if (vf.Parameters.Length < 1)
                    throw new InvalidDataException("[Verso] BerserkSlash: AddFinalDamageMultiplier arity");
                var amount = vf.Parameters[0];
                vf.Parameters[0] = new EX_CallMath
                {
                    StackNode = clampFn,
                    Parameters = new KismetExpression[]
                    {
                        amount,
                        new EX_DoubleConst { Value = 1.0 },
                        new EX_DoubleConst { Value = 2.0 },
                    },
                };
                done = true;
                return vf;
            }, "BerserkSlash cap");
        if (!done) throw new InvalidDataException("[Verso] BerserkSlash: no AddFinalDamageMultiplier");
        bp.Write(dest);
        Console.WriteLine($"[Verso] {bpStem}: missing-HP mult capped at 2x");
        return 1;
    }

    // ── Light Holder: delete +2AP branch (#7-10 shape), add per-hit ×3 ──

    private static int PatchLightHolder(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_NEW_4";
        var src = FindBp(bpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        var fn = OnAction(bp, bpStem);
        var stmts = fn.ScriptBytecode!.ToList();

        // 1. Delete the gated AP body: PopIfNot(AND) + row + FTrunc + GainAP.
        int andIdx = -1, popIdx = -1, gainIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool isAnd = false, isPop = false, isGain = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_CallMath cm && cm.StackNode.IsImport()
                    && cm.StackNode.ToImport(bp).ObjectName.ToString() == "BooleanAND") isAnd = true;
                if (e is EX_JumpIfNot || e is EX_PopExecutionFlowIfNot) isPop = true;
                if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "GainAP") isGain = true;
            });
            if (isAnd && andIdx < 0)
            {
                // AND producer LetBool (not the branch): the branch PopIfNot follows it.
                if (stmts[i] is EX_LetBool) andIdx = i;
            }
            if (isPop && andIdx >= 0 && popIdx < 0 && i > andIdx) popIdx = i;
            if (isGain && popIdx >= 0 && gainIdx < 0) gainIdx = i;
        }
        if (andIdx < 0 || popIdx < 0 || gainIdx < 0 || gainIdx != popIdx + 3)
            throw new InvalidDataException($"[Verso] {bpStem}: AP branch shape drifted ({andIdx},{popIdx},{gainIdx})");
        bool rowOk = false, truncOk = false, gainOk = false;
        MayhemFixKit.Walk(new[] { stmts[popIdx + 1] }, e => { if (e is EX_FinalFunction ff && ff.StackNode.IsImport() && ff.StackNode.ToImport(bp).ObjectName.ToString() == "GetDataTableRowFromName") rowOk = true; });
        MayhemFixKit.Walk(new[] { stmts[popIdx + 2] }, e => { if (e is EX_CallMath cm && cm.StackNode.IsImport() && cm.StackNode.ToImport(bp).ObjectName.ToString() == "FTrunc") truncOk = true; });
        MayhemFixKit.Walk(new[] { stmts[popIdx + 3] }, e => { if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "GainAP") gainOk = true; });
        if (!rowOk || !truncOk || !gainOk)
            throw new InvalidDataException($"[Verso] {bpStem}: AP branch body drifted");
        uint delOff = 0;
        for (int i = 0; i < popIdx; i++) delOff += stmts[i].GetSize(bp);
        uint delSize = 0;
        for (int i = popIdx; i <= gainIdx; i++) delSize += stmts[i].GetSize(bp);
        stmts.RemoveRange(popIdx, gainIdx - popIdx + 1);
        fn.ScriptBytecode = stmts.ToArray();
        ShiftShrink(bp, fn, delOff, delSize);
        MayhemFixKit.RecalcContextSkips(bp, fn);
        Console.WriteLine($"[Verso] {bpStem}: +2AP branch deleted");

        // 2. Per-hit ×3 at A: extra ReceiveDamage(2× damage) after the main one.
        stmts = fn.ScriptBytecode!.ToList();
        int recvIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
            if (CallsVirtual(stmts[i], "ReceiveDamage")) { recvIdx = i; break; }
        if (recvIdx < 0) throw new InvalidDataException($"[Verso] {bpStem}: no ReceiveDamage");
        var recvClone = (KismetExpression)MayhemFixKit.CloneKismet(stmts[recvIdx]);
        KismetExpression? amountOp = null;
        MayhemFixKit.Walk(new[] { recvClone }, e =>
        {
            if (amountOp is not null) return;
            if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "ReceiveDamage"
                && vf.Parameters.Length > 0) amountOp = vf.Parameters[0];
        });
        if (amountOp is null) throw new InvalidDataException($"[Verso] {bpStem}: ReceiveDamage amount missing");
        var multImport = CloneNamedCallImport(bp, stmts, "Multiply_DoubleDouble", bpStem);
        var doubled = new EX_CallMath
        {
            StackNode = multImport,
            Parameters = new KismetExpression[]
            {
                (KismetExpression)MayhemFixKit.CloneKismet(amountOp),
                new EX_DoubleConst { Value = 2.0 },
            },
        };
        MayhemFixKit.Walk(new[] { recvClone }, e =>
        {
            if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "ReceiveDamage"
                && vf.Parameters.Length > 0) vf.Parameters[0] = doubled;
        });
        var gate = BuildRankGate(bp, fn, stmts, RankA, bpStem);
        SpliceGated(bp, fn, recvIdx, gate.Check(), new List<KismetExpression> { recvClone }, $"{bpStem} x3", new List<KismetExpression> { gate.Prelude });
        bp.Write(dest);
        return 2;
    }

    // ── Overload: rank → S, AP refill removed, self Berserk 5 turns ──

    private const string BerserkPkg = "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Berserk";
    private const string BerserkCls = "BP_BattleBuff_Berserk_C";

    private static int PatchOverload(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_Verso_Overcharge";
        var src = FindBp(bpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        var fn = OnAction(bp, bpStem);

        // 0. Remove the AP refill: the single GainAP LVF statement goes whole.
        // Jump targets shift down (RemoveSlowTail pattern); skips recomputed.
        var cur = fn.ScriptBytecode!.ToList();
        int gainIdx = -1, gainN = 0;
        for (int i = 0; i < cur.Count; i++)
        {
            bool has = false;
            MayhemFixKit.Walk(new[] { cur[i] }, e =>
            {
                if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "GainAP") has = true;
            });
            if (has) { gainIdx = i; gainN++; }
        }
        if (gainN != 1)
            throw new InvalidDataException($"[Verso] {bpStem}: GainAP x{gainN} (expect 1)");
        uint rmOffset = 0;
        for (int i = 0; i < gainIdx; i++) rmOffset += (uint)cur[i].GetSize(bp);
        uint rmSize = (uint)cur[gainIdx].GetSize(bp);
        LuneSkillsKit.ShiftAbsoluteSigned(bp, fn, rmOffset, rmSize);
        cur.RemoveAt(gainIdx);
        fn.ScriptBytecode = cur.ToArray();
        MayhemFixKit.RecalcContextSkips(bp, fn);

        var stmts = fn.ScriptBytecode!.ToList();

        // 1. SetSpecificRank(4,·) → (5,·).
        int rankSets = 0;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "SetSpecificRank"
                    && vf.Parameters.Length > 0 && vf.Parameters[0] is EX_ByteConst bc && bc.Value == 4)
                {
                    bc.Value = 5;
                    rankSets++;
                }
            });
        if (rankSets != 1)
            throw new InvalidDataException($"[Verso] {bpStem}: SetSpecificRank(4) x{rankSets} (expect 1)");

        // 2. Self Berserk (1 stack, 5 turns) before the single Return.
        // The function declares no object/bool locals, so reuse
        // two dead temps (liveness-asserted): the comp local (last read #8)
        // and the IsLast bool (last read #2); the block sits before Return.
        int retIdx = -1, retN = 0;
        for (int i = 0; i < stmts.Count; i++)
            if (stmts[i] is EX_Return) { retIdx = i; retN++; }
        if (retIdx < 0 || retN != 1)
            throw new InvalidDataException($"[Verso] {bpStem}: Return x{retN} (expect 1)");
        int LastRead(string name)
        {
            int last = -1;
            for (int i = 0; i < stmts.Count; i++)
                MayhemFixKit.Walk(new[] { stmts[i] }, e =>
                {
                    if (e is EX_LocalVariable lv && PtrName(lv.Variable) == name) last = i;
                });
            return last;
        }
        if (LastRead("CallFunc_GetComponentByClass_ReturnValue") >= retIdx
            || LastRead("CallFunc_IsLastActionEffectOfSkill_ReturnValue") >= retIdx)
            throw new InvalidDataException($"[Verso] {bpStem}: scratch locals still live");
        var spawnLocal = CloneLocalRead(stmts, "CallFunc_GetComponentByClass_ReturnValue", bpStem);
        var okLocal = CloneLocalRead(stmts, "CallFunc_IsLastActionEffectOfSkill_ReturnValue", bpStem);
        EX_Context? selfStatsTpl = null;
        foreach (var s in stmts)
        {
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (selfStatsTpl is not null) return;
                if (e is EX_Context cx && cx.ObjectExpression is EX_InstanceVariable
                    && cx.ContextExpression is EX_InstanceVariable civ && PtrName(civ.Variable) == "AC_jRPG_CharacterStats")
                {
                    bool call = false;
                    MayhemFixKit.Walk(new[] { cx }, x => { if (x is EX_LocalVirtualFunction || x is EX_FinalFunction) call = true; });
                    if (!call) selfStatsTpl = cx;
                }
            });
            if (selfStatsTpl is not null) break;
        }
        if (selfStatsTpl is null)
            throw new InvalidDataException($"[Verso] {bpStem}: no self stats read");

        var spawnFn = EnsureEngineFunctionLocal(bp, "GameplayStatics", "SpawnObject");
        var setIntFn = EnsureEngineFunctionLocal(bp, "KismetSystemLibrary", "SetIntPropertyByName");
        var berserkIdx = LuneSkillsKit.EnsureObjectImport(bp, BerserkPkg, BerserkCls, BerserkCls, "/Script/Engine");
        var selfStats = (KismetExpression)MayhemFixKit.CloneKismet(selfStatsTpl);
        var spawn = new EX_LetObj
        {
            VariableExpression = spawnLocal,
            AssignmentExpression = new EX_FinalFunction
            {
                StackNode = spawnFn,
                Parameters = new KismetExpression[]
                {
                    new EX_ObjectConst { Value = berserkIdx },
                    (KismetExpression)MayhemFixKit.CloneKismet(selfStats),
                },
            },
        };
        var setDuration = new EX_FinalFunction
        {
            StackNode = setIntFn,
            Parameters = new KismetExpression[]
            {
                (KismetExpression)MayhemFixKit.CloneKismet(spawnLocal),
                new EX_NameConst { Value = FName.FromString(bp, "TurnDuration") },
                new EX_IntConst { Value = 5 },
            },
        };
        var activate = new EX_Context
        {
            ObjectExpression = (KismetExpression)MayhemFixKit.CloneKismet(selfStats),
            ContextExpression = new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(bp, "ActivateBuff"),
                Parameters = new KismetExpression[]
                {
                    (KismetExpression)MayhemFixKit.CloneKismet(spawnLocal),
                    new EX_ByteConst { Value = 4 },
                    (KismetExpression)MayhemFixKit.CloneKismet(selfStats),
                    (KismetExpression)MayhemFixKit.CloneKismet(okLocal),
                },
            },
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
        };
        var block = new List<KismetExpression> { spawn, setDuration, activate };
        uint insOff = 0;
        for (int i = 0; i < retIdx; i++) insOff += stmts[i].GetSize(bp);
        uint ssize = 0;
        foreach (var s in block) ssize += s.GetSize(bp);
        stmts.InsertRange(retIdx, block);
        fn.ScriptBytecode = stmts.ToArray();
        ShiftSignedLocal(bp, fn, insOff, (int)ssize);
        MayhemFixKit.RecalcContextSkips(bp, fn);
        bp.Write(dest);
        Console.WriteLine($"[Verso] {bpStem}: rank -> S, AP refill removed, self Berserk (5 turns)");
        return 3;
    }

    private static FPackageIndex EnsureEngineFunctionLocal(UAsset bp, string outerClass, string fnName)
    {
        for (int i = 0; i < bp.Imports.Count; i++)
        {
            var imp = bp.Imports[i];
            if (imp.ObjectName.ToString() == fnName) return FPackageIndex.FromImport(i);
        }
        FPackageIndex classIdx = null;
        for (int i = 0; i < bp.Imports.Count; i++)
        {
            var imp = bp.Imports[i];
            if (imp.ObjectName.ToString() == outerClass && imp.ClassName.ToString() == "Class")
                classIdx = FPackageIndex.FromImport(i);
        }
        if (classIdx is null)
        {
            FPackageIndex pkgIdx = null;
            for (int i = 0; i < bp.Imports.Count; i++)
            {
                var imp = bp.Imports[i];
                if (imp.ClassName.ToString() == "Package"
                    && imp.ObjectName.ToString() == "/Script/Engine")
                    pkgIdx = FPackageIndex.FromImport(i);
            }
            pkgIdx ??= bp.AddImport(new Import("/Script/CoreUObject", "Package",
                FPackageIndex.FromRawIndex(0), "/Script/Engine", false, bp));
            classIdx = bp.AddImport(new Import("/Script/CoreUObject", "Class",
                pkgIdx, outerClass, false, bp));
        }
        return bp.AddImport(new Import("/Script/CoreUObject", "Object",
            classIdx, fnName, false, bp));
    }

    private static void ShiftSignedLocal(UAsset asset, FunctionExport fn, uint insertionPoint, int delta)
    {
        if (delta >= 0)
        {
            MayhemFixKit.ShiftAbsolute(asset, fn, insertionPoint, (uint)delta, null);
            return;
        }
        ShiftShrink(asset, fn, insertionPoint, (uint)(-delta));
    }

    // ── From Fire: +20% heal at B (extra RecoverHP inside the burn latch) ──

    private static int PatchFromFireHeal(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_Verso_FromFire";
        var src = FindBp(bpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        var fn = OnAction(bp, bpStem);
        var stmts = fn.ScriptBytecode!.ToList();

        // Anchor: the RecoverHP statement (burn-gated block).
        int recvIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
            if (CallsVirtual(stmts[i], "RecoverHP")) { recvIdx = i; break; }
        if (recvIdx < 0) throw new InvalidDataException($"[Verso] {bpStem}: no RecoverHP");

        // Extra 20%: MaxHP × 0.20 through a fresh multiply.
        var maxHp = CloneLocalRead(stmts, "CallFunc_GetMaxHP_MaxHP", bpStem);
        var multImport = CloneNamedCallImport(bp, stmts, "Multiply_DoubleDouble", bpStem);
        var extra = new EX_CallMath
        {
            StackNode = multImport,
            Parameters = new KismetExpression[]
            {
                maxHp,
                new EX_DoubleConst { Value = 0.20 },
            },
        };
        var recvClone = (KismetExpression)MayhemFixKit.CloneKismet(stmts[recvIdx]);
        bool amountSwapped = false;
        MayhemFixKit.Walk(new[] { recvClone }, e =>
        {
            if (amountSwapped || e is not EX_LocalVirtualFunction vf || vf.VirtualFunctionName.ToString() != "RecoverHP") return;
            if (vf.Parameters.Length < 1)
                throw new InvalidDataException($"[Verso] {bpStem}: RecoverHP arity");
            vf.Parameters[0] = extra;
            amountSwapped = true;
        });
        if (!amountSwapped) throw new InvalidDataException($"[Verso] {bpStem}: RecoverHP amount missing");
        var gate = BuildRankGate(bp, fn, stmts, RankB, bpStem);
        SpliceGated(bp, fn, recvIdx, gate.Check(), new List<KismetExpression> { recvClone }, $"{bpStem} heal+20", new List<KismetExpression> { gate.Prelude });
        bp.Write(dest);
        return 1;
    }

    // ── Perfect Recovery: +20% heal at C (extra RecoverHP) ──

    private static int PatchRecoveryBonus(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_PerfectRecovery";
        var src = FindBp(bpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        var fn = OnAction(bp, bpStem);
        var stmts = fn.ScriptBytecode!.ToList();
        int recvIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
            if (CallsVirtual(stmts[i], "RecoverHP")) { recvIdx = i; break; }
        if (recvIdx < 0) throw new InvalidDataException($"[Verso] {bpStem}: no RecoverHP");
        var maxHp = CloneLocalRead(stmts, "CallFunc_GetMaxHP_MaxHP", bpStem);
        var multImport = CloneNamedCallImport(bp, stmts, "Multiply_DoubleDouble", bpStem);
        var extra = new EX_CallMath
        {
            StackNode = multImport,
            Parameters = new KismetExpression[]
            {
                maxHp,
                new EX_DoubleConst { Value = 0.20 },
            },
        };
        var recvClone = (KismetExpression)MayhemFixKit.CloneKismet(stmts[recvIdx]);
        bool amountSwapped = false;
        MayhemFixKit.Walk(new[] { recvClone }, e =>
        {
            if (amountSwapped || e is not EX_LocalVirtualFunction vf || vf.VirtualFunctionName.ToString() != "RecoverHP") return;
            if (vf.Parameters.Length < 1)
                throw new InvalidDataException($"[Verso] {bpStem}: RecoverHP arity");
            vf.Parameters[0] = extra;
            amountSwapped = true;
        });
        if (!amountSwapped) throw new InvalidDataException($"[Verso] {bpStem}: RecoverHP amount missing");
        var gate = BuildRankGate(bp, fn, stmts, RankC, bpStem);
        SpliceGated(bp, fn, recvIdx, gate.Check(), new List<KismetExpression> { recvClone }, $"{bpStem} heal+20", new List<KismetExpression> { gate.Prelude });
        bp.Write(dest);
        return 1;
    }

    // ── Blood Strike: conditional absorb at Rank A ──
    // No HP cost by default. At Rank A+ the skill tries to absorb 50% of Max
    // HP: Temp_bool (which already feeds the damage switch, now A-gated) is
    // extended to rank>=A AND affordable, where affordable = current HP >=
    // half of Max HP. One shared condition drives both the ×2 damage and the
    // HP-cost pipeline gate, so no bonus without payment and vice versa.

    private static int PatchDefiantExtra(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_Verso_DefiantStrike";
        var src = FindBp(bpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        var fn = OnAction(bp, bpStem);
        var stmts = fn.ScriptBytecode!.ToList();

        // -1. Base the loss on Max HP: the loss Multiply (the one reading the
        // HpPercentLoss row) multiplies CurrentHP -> swap to the MaxHP local.
        // The Subtract keeps CurrentHP: newHP = cur - pct*max.
        ReplaceOne(bp, fn,
            e => e is EX_Let let && let.Expression is EX_CallMath cm && IsMultCall(bp, cm)
                && HasLocalRead(cm, "CallFunc_GetDataTableRowFromName_OutRow_1")
                && HasMemberRead(cm, "CurrentHP"),
            e =>
            {
                var cm = (EX_CallMath)((EX_Let)e).Expression;
                var maxLocal = CloneLocalRead(fn.ScriptBytecode!.ToList(), "CallFunc_GetMaxHP_MaxHP", bpStem);
                for (int i = 0; i < cm.Parameters.Length; i++)
                {
                    bool isCur = false;
                    MayhemFixKit.Walk(new[] { cm.Parameters[i] }, x =>
                    {
                        if (x is EX_Context cx && cx.ContextExpression is EX_InstanceVariable iv
                            && PtrName(iv.Variable) == "CurrentHP") isCur = true;
                    });
                    if (isCur) cm.Parameters[i] = maxLocal;
                }
                return e;
            }, "DefiantStrike max-HP base");
        stmts = fn.ScriptBytecode!.ToList();

        // 0. The gate swap already turned the damage Temp_bool into
        // rank>=A. Find that LetBool (exactly one Temp_bool writer).
        int boolIdx = -1, boolN = 0;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_LetBool lb && lb.VariableExpression is EX_LocalVariable lv
                    && PtrName(lv.Variable) == "Temp_bool_Variable") hit = true;
            });
            if (hit) { boolIdx = i; boolN++; }
        }
        if (boolN != 1)
            throw new InvalidDataException($"[Verso] {bpStem}: Temp_bool writer x{boolN} (expect 1)");

        // 1. Fresh MaxHP read right before the gate: clone the HP region's
        // GetMaxHP statement (the loop-stale local can't be trusted here).
        int maxIdx = -1, maxN = 0;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "GetMaxHP") hit = true;
            });
            if (hit) { maxIdx = i; maxN++; }
        }
        if (maxN != 1)
            throw new InvalidDataException($"[Verso] {bpStem}: GetMaxHP x{maxN} (expect 1)");
        var maxRead = (KismetExpression)MayhemFixKit.CloneKismet(stmts[maxIdx]);
        uint insOff = 0;
        for (int i = 0; i < boolIdx; i++) insOff += stmts[i].GetSize(bp);
        uint maxSize = maxRead.GetSize(bp);
        stmts.Insert(boolIdx, maxRead);
        fn.ScriptBytecode = stmts.ToArray();
        ShiftSignedLocal(bp, fn, insOff, (int)maxSize);
        MayhemFixKit.RecalcContextSkips(bp, fn);
        boolIdx++;
        stmts = fn.ScriptBytecode!.ToList();

        // 2. Temp_bool = rank>=A AND affordable, where affordable = current
        // HP >= half of Max HP (proven imports only: Less_DoubleDouble +
        // Not_PreBool under KismetMathLibrary, in-BP Multiply).
        var andFn = LuneSkillsKit.EnsureKismetMathFn(bp, "BooleanAND");
        var lessFn = LuneSkillsKit.EnsureKismetMathFn(bp, "Less_DoubleDouble");
        var notFn = LuneSkillsKit.EnsureKismetMathFn(bp, "Not_PreBool");
        var mulFn = CloneNamedCallImport(bp, stmts, "Multiply_DoubleDouble", bpStem);
        var maxLocal = CloneLocalRead(stmts, "CallFunc_GetMaxHP_MaxHP", bpStem);
        KismetExpression? curCtx = null;
        foreach (var s in stmts)
        {
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (curCtx is not null) return;
                if (e is EX_Let let && let.Expression is EX_CallMath cm
                    && cm.StackNode.IsImport()
                    && cm.StackNode.ToImport(bp).ObjectName.ToString().Contains("Subtract"))
                {
                    foreach (var p in cm.Parameters)
                        MayhemFixKit.Walk(new[] { p }, x =>
                        {
                            if (x is EX_Context cx && cx.ContextExpression is EX_InstanceVariable iv
                                && PtrName(iv.Variable) == "CurrentHP")
                                curCtx = (KismetExpression)MayhemFixKit.CloneKismet(cx);
                        });
                }
            });
            if (curCtx is not null) break;
        }
        if (curCtx is null)
            throw new InvalidDataException($"[Verso] {bpStem}: no CurrentHP read");
        var affordable = new EX_CallMath
        {
            StackNode = notFn,
            Parameters = new KismetExpression[]
            {
                new EX_CallMath
                {
                    StackNode = lessFn,
                    Parameters = new KismetExpression[]
                    {
                        curCtx,
                        new EX_CallMath
                        {
                            StackNode = mulFn,
                            Parameters = new KismetExpression[]
                            {
                                (KismetExpression)MayhemFixKit.CloneKismet(maxLocal),
                                new EX_DoubleConst { Value = 0.5 },
                            },
                        },
                    },
                },
            },
        };
        ReplaceOne(bp, fn,
            e => e is EX_LetBool lb && lb.VariableExpression is EX_LocalVariable lv
                && PtrName(lv.Variable) == "Temp_bool_Variable",
            e =>
            {
                var lb = (EX_LetBool)e;
                var rankCheck = lb.AssignmentExpression;
                lb.AssignmentExpression = new EX_CallMath
                {
                    StackNode = andFn,
                    Parameters = new KismetExpression[] { rankCheck, affordable },
                };
                return e;
            }, "DefiantStrike absorb condition");
        stmts = fn.ScriptBytecode!.ToList();

        // 3. Gate the HP-cost pipeline (5 Lets + SetHP) on the same Temp_bool:
        // skip the whole region when the absorb didn't happen.
        int setIdx = -1, setN = 0;
        for (int i = 0; i < stmts.Count; i++)
            if (CallsVirtual(stmts[i], "SetHP")) { setIdx = i; setN++; }
        if (setN != 1)
            throw new InvalidDataException($"[Verso] {bpStem}: SetHP x{setN} (expect 1)");
        int first = setIdx;
        for (int i = setIdx - 1; i >= 0 && first > setIdx - 6; i--)
        {
            if (stmts[i] is EX_Let) first = i;
            else break;
        }
        if (setIdx - first != 5)
            throw new InvalidDataException($"[Verso] {bpStem}: HP pipeline x{setIdx - first} (expect 5 Lets + SetHP)");
        uint regionOff = 0;
        for (int i = 0; i < first; i++) regionOff += stmts[i].GetSize(bp);
        uint regionSize = 0;
        for (int i = first; i <= setIdx; i++) regionSize += stmts[i].GetSize(bp);
        var skip = new EX_JumpIfNot
        {
            CodeOffset = 0,
            BooleanExpression = CloneLocalRead(stmts, "Temp_bool_Variable", bpStem),
        };
        uint jumpSize = skip.GetSize(bp);
        stmts.Insert(first, skip);
        fn.ScriptBytecode = stmts.ToArray();
        ShiftSignedLocal(bp, fn, regionOff, (int)jumpSize);
        skip.CodeOffset = regionOff + jumpSize + regionSize;
        MayhemFixKit.RecalcContextSkips(bp, fn);

        bp.Write(dest);
        Console.WriteLine($"[Verso] {bpStem}: A-gated absorb (50% of max) + damage x2");
        return 3;
    }

    private static bool IsMultCall(UAsset bp, EX_CallMath cm)
        => cm.StackNode.IsImport() && cm.StackNode.ToImport(bp).ObjectName.ToString().Contains("Multiply");

    private static bool HasLocalRead(KismetExpression root, string name)
    {
        bool hit = false;
        MayhemFixKit.Walk(new[] { root }, e =>
        {
            if (e is EX_LocalVariable lv && PtrName(lv.Variable) == name) hit = true;
        });
        return hit;
    }

    private static bool HasMemberRead(KismetExpression root, string name)
    {
        bool hit = false;
        MayhemFixKit.Walk(new[] { root }, e =>
        {
            if (e is EX_InstanceVariable iv && PtrName(iv.Variable) == name) hit = true;
        });
        return hit;
    }

    // ── Follow Up: S adds +5%/shot (exact division over the live mult) ──

    private static int PatchFollowupBonus(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_Verso_Followup";
        var src = FindBp(bpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        var fn = OnAction(bp, bpStem);
        var stmts = fn.ScriptBytecode!.ToList();

        var m = CloneVarRead(stmts, "FreeAimDamageMultiplier", bpStem);
        var subFn = LuneSkillsKit.EnsureKismetMathFn(bp, "Subtract_DoubleDouble");
        var mulFn = CloneNamedCallImport(bp, stmts, "Multiply_DoubleDouble", bpStem);
        var addFn = CloneNamedCallImport(bp, stmts, "Add_DoubleDouble", bpStem);
        var divFn = LuneSkillsKit.EnsureKismetMathFn(bp, "SafeDivide");
        KismetExpression D(double v) => new EX_DoubleConst { Value = v };
        var amount = new EX_CallMath
        {
            StackNode = divFn,
            Parameters = new KismetExpression[]
            {
                new EX_CallMath
                {
                    StackNode = addFn,
                    Parameters = new KismetExpression[]
                    {
                        D(1.0),
                        new EX_CallMath
                        {
                            StackNode = mulFn,
                            Parameters = new KismetExpression[]
                            {
                                new EX_CallMath
                                {
                                    StackNode = subFn,
                                    Parameters = new KismetExpression[]
                                    {
                                        (KismetExpression)MayhemFixKit.CloneKismet(m),
                                        D(1.0),
                                    },
                                },
                                D(1.5),
                            },
                        },
                    },
                },
                (KismetExpression)MayhemFixKit.CloneKismet(m),
            },
        };
        var builder = CloneLocalRead(stmts, "CallFunc_MakeDamageBuilder_ReturnValue", bpStem);
        var reason = CloneLocalRead(stmts, "CallFunc_Conv_NameToString_ReturnValue", bpStem);
        var addMult = new EX_Context
        {
            ObjectExpression = builder,
            ContextExpression = new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(bp, "AddFinalDamageMultiplier"),
                Parameters = new KismetExpression[] { amount, reason },
            },
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
        };
        int dealIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
            if (CallsVirtual(stmts[i], "DealDamages")) { dealIdx = i; break; }
        if (dealIdx < 0) throw new InvalidDataException($"[Verso] {bpStem}: no DealDamages");
        var gate = BuildRankGate(bp, fn, stmts, RankS, bpStem);
        SpliceGated(bp, fn, dealIdx - 1, gate.Check(), new List<KismetExpression> { addMult }, $"{bpStem} S+5/shot", new List<KismetExpression> { gate.Prelude });
        bp.Write(dest);
        return 1;
    }

    // ── Ascending Assault: S adds +5%/cast ──

    private static int PatchAscendingBonus(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_Verso_AscendingAssault";
        var src = FindBp(bpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        var fn = OnAction(bp, bpStem);
        var stmts = fn.ScriptBytecode!.ToList();

        var castD = CloneLocalRead(stmts, "CallFunc_Conv_IntToDouble_ReturnValue", bpStem);
        var mulFn = CloneNamedCallImport(bp, stmts, "Multiply_DoubleDouble", bpStem);
        var addFn = CloneNamedCallImport(bp, stmts, "Add_DoubleDouble", bpStem);
        KismetExpression D(double v) => new EX_DoubleConst { Value = v };
        var amount = new EX_CallMath
        {
            StackNode = addFn,
            Parameters = new KismetExpression[]
            {
                D(1.0),
                new EX_CallMath
                {
                    StackNode = mulFn,
                    Parameters = new KismetExpression[] { castD, D(0.05) },
                },
            },
        };
        var builder = CloneLocalRead(stmts, "CallFunc_MakeDamageBuilder_ReturnValue", bpStem);
        var reason = CloneLocalRead(stmts, "CallFunc_Conv_NameToString_ReturnValue", bpStem);
        var addMult = new EX_Context
        {
            ObjectExpression = builder,
            ContextExpression = new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(bp, "AddFinalDamageMultiplier"),
                Parameters = new KismetExpression[] { amount, reason },
            },
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
        };
        int dealIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
            if (CallsVirtual(stmts[i], "DealDamages")) { dealIdx = i; break; }
        if (dealIdx < 0) throw new InvalidDataException($"[Verso] {bpStem}: no DealDamages");
        var gate = BuildRankGate(bp, fn, stmts, RankS, bpStem);
        SpliceGated(bp, fn, dealIdx - 1, gate.Check(), new List<KismetExpression> { addMult }, $"{bpStem} S+5/cast", new List<KismetExpression> { gate.Prelude });
        bp.Write(dest);
        return 1;
    }

    // ── Phantom Stars: S inflicts Slow 5 (fresh ApplyBuff LVF) ──

    private const string SlowPkg = "/Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_Slow";
    private const string SlowCls = "BP_BattleBuff_Slow_C";

    private static int PatchPhantomSlow(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_Verso_PhantomStars";
        var src = FindBp(bpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        var fn = OnAction(bp, bpStem);
        var stmts = fn.ScriptBytecode!.ToList();

        var target = CloneLocalRead(stmts, "CallFunc_Array_Get_Item", bpStem);
        var targetStats = new EX_Context
        {
            ObjectExpression = target,
            ContextExpression = CloneMember(bp, stmts, "AC_jRPG_CharacterStats", bpStem),
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
        };
        var hasHit = CloneLocalRead(stmts, "CallFunc_DealDamages_HasHit", bpStem);
        var andImport = CloneNamedCallImport(bp, stmts, "BooleanAND", bpStem);
        var slowIdx = LuneSkillsKit.EnsureObjectImport(bp, SlowPkg, SlowCls, SlowCls, "/Script/Engine");
        // Out-local: the damage builder is dead after DealDamages (the slow
        // hook sits post-hit), so reuse it as scratch (no new local needed).
        var createdLocal = CloneLocalRead(stmts, "CallFunc_MakeDamageBuilder_ReturnValue", bpStem);
        var gate = BuildRankGate(bp, fn, stmts, RankS, bpStem);
        var cond = new EX_CallMath
        {
            StackNode = andImport,
            Parameters = new KismetExpression[] { gate.Check(), hasHit },
        };
        var apply = new EX_LocalVirtualFunction
        {
            VirtualFunctionName = FName.FromString(bp, "ApplyBuff"),
            Parameters = new KismetExpression[]
            {
                new EX_ObjectConst { Value = slowIdx },
                targetStats,
                new EX_IntConst { Value = 5 },
                new EX_NoObject(),
                new EX_ByteConst { Value = 4 },
                createdLocal,
            },
        };
        // Anchor: the TryPerformBreakStun statement (post-hit hook point).
        int anchor = -1;
        for (int i = 0; i < stmts.Count; i++)
            if (CallsVirtual(stmts[i], "TryPerformBreakStun")) { anchor = i; break; }
        if (anchor < 0) throw new InvalidDataException($"[Verso] {bpStem}: no TryPerformBreakStun");
        SpliceGated(bp, fn, anchor, cond, new List<KismetExpression> { apply }, $"{bpStem} S:Slow", new List<KismetExpression> { gate.Prelude });
        bp.Write(dest);
        return 1;
    }

// __CHUNK4__

    // ──────────────────────────────────────────────────────────────────
    // H. Assault Zero — new custom starter (Combo1 donor), ex-Combo1 slot
    // ──────────────────────────────────────────────────────────────────

    private const string AZDa = "DA_Skill_Verso_AssaultZero";
    private const string AZBp = "BP_Battle_SkillScript_Verso_AssaultZero";

    private static int CreateAssaultZero(string modDir)
    {
        int n = 0;
        // 1. Rows (Combo1 values rescaled; plain Combo1 rows untouched).
        var balSrc = FindTables(BalTable);
        Stage(balSrc, modDir, out var balDest);
        var bal = new DataTableEditor(balDest);
        bal.CloneRow("Combo1_BaseDamage", "AssaultZero_BaseDamage");
        bal.CloneRow("Combo1_BreakBarMultiplier", "AssaultZero_BreakBarMultiplier");
        bal.CloneRow("Combo1_OverchargeDamageMultiplier", "AssaultZero_OverchargeDamageMultiplier");
        n += bal.Apply("{\"AssaultZero_BaseDamage\":{\"FloatValue\":" + PH(1, 5).ToString(System.Globalization.CultureInfo.InvariantCulture) + "},"
            + "\"AssaultZero_BreakBarMultiplier\":{\"FloatValue\":0.3},"
            + "\"AssaultZero_OverchargeDamageMultiplier\":{\"FloatValue\":2.0}}");
        bal.Save(balDest);

        // 2. DA clone.
        var daSrc = FindDa("DA_Skill_Combo1");
        var daDest = StageDest(daSrc, modDir).Replace("DA_Skill_Combo1", AZDa, StringComparison.Ordinal);
        Directory.CreateDirectory(Path.GetDirectoryName(daDest)!);
        if (!File.Exists(daDest)) FileCopy.AssetPair(daSrc, daDest);
        var da = Open(daDest);
        LuneSkillsKit.RetargetPairs(da, daDest,
            ("DA_Skill_Combo1", AZDa),
            ("BP_Battle_SkillScript_Combo1", AZBp));
        foreach (var ex in da.Exports.OfType<NormalExport>())
            foreach (var p in ex.Data)
            {
                if (p is NamePropertyData np && np.Name.ToString() == "NameID")
                    np.Value = FName.FromString(da, "AssaultZero");
                if (p is TextPropertyData tp && tp.TableId?.Value?.Value is { Length: > 0 })
                {
                    var pn = p.Name.ToString();
                    var suffix = pn == "Name" ? "Name"
                        : pn == "Description" ? "Description_Long"
                        : pn == "ShortDescription" ? "Description_Short" : null;
                    if (suffix != null)
                        tp.Value = new FString("Verso_AssaultZero_" + suffix);
                }
            }
        da.FolderName = new FString("/Game/Gameplay/SkillTree/Content/NoahVerso/DA_Skills/" + AZDa);
        da.Write(daDest);
        var daEd = DataTableEditor.OpenPropertyAsset(daDest);
        n += daEd.ApplyToProperties("{\"APCost\":1}");
        daEd.Save(daDest);
        n++;

        // 3. BP clone + row retargets.
        var bpSrc = FindBp("BP_Battle_SkillScript_Combo1");
        var bpDest = StageDest(bpSrc, modDir).Replace("BP_Battle_SkillScript_Combo1", AZBp, StringComparison.Ordinal);
        Directory.CreateDirectory(Path.GetDirectoryName(bpDest)!);
        if (!File.Exists(bpDest)) FileCopy.AssetPair(bpSrc, bpDest);
        var bp = Open(bpDest);
        LuneSkillsKit.RetargetPairs(bp, bpDest,
            ("BP_Battle_SkillScript_Combo1", AZBp),
            ("DA_Skill_Combo1", AZDa),
            ("Combo1_BaseDamage", "AssaultZero_BaseDamage"),
            ("Combo1_BreakBarMultiplier", "AssaultZero_BreakBarMultiplier"),
            ("Combo1_OverchargeDamageMultiplier", "AssaultZero_OverchargeDamageMultiplier"));
        bp.FolderName = new FString("/Game/Gameplay/Battle/Skills/Content/Verso/" + AZBp);
        bp.Write(bpDest);
        n++;

        // 4. Explicit Rank C gate on the clone.
        n += SwapOneBp(AZBp, RankC, modDir);

        // 5. String table (Name overwrites the dangling vanilla key).
        var stSrc = FindTables(StringTable);
        Stage(stSrc, modDir, out var stDest);
        foreach (var s in StringEdits)
        {
            if (s.Key != "Verso_AssaultZero") continue;
            DataTableEditor.AddStringTableEntries(stDest, new Dictionary<string, string>
            {
                [s.Key + "_Name"] = s.Name,
                [s.Key + "_Description_Long"] = s.Long,
                [s.Key + "_Description_Short"] = s.Short,
            });
            n += 3;
        }
        Console.WriteLine("[Verso] Assault Zero custom staged (DA+BP+rows+ST)");
        return n;
    }

    // ──────────────────────────────────────────────────────────────────
    // I. Graph: node0 Combo1 → AssaultZero (starter kept, edge repointed)
    // ──────────────────────────────────────────────────────────────────

    private static int PatchGraph(string modDir)
    {
        var graphSrc = FileCopy.FindTablesAsset(GraphStem + ".uasset")
            ?? throw new FileNotFoundException(GraphStem + " not under work/tables");
        var idx = graphSrc.IndexOf("Sandfall", StringComparison.Ordinal);
        var dest = Path.Combine(modDir, graphSrc[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!File.Exists(dest)) FileCopy.AssetPair(graphSrc, dest);
        var g = Open(dest);
        var gx = g.Exports.OfType<NormalExport>().FirstOrDefault()
            ?? throw new InvalidDataException("[Verso] graph has no export");
        var nodes = gx.Data[0] as ArrayPropertyData
            ?? throw new InvalidDataException("[Verso] graph nodes missing");
        var edges = gx.Data[1] as ArrayPropertyData
            ?? throw new InvalidDataException("[Verso] graph edges missing");

        string? NodeStem(StructPropertyData nodeStruct)
        {
            if (nodeStruct.Value is not { Count: >= 1 } || nodeStruct.Value[0] is not StructPropertyData su) return null;
            if (su.Value is not { Count: >= 1 } || su.Value[0] is not ObjectPropertyData op) return null;
            var pi = op.Value;
            if (pi is null || !pi.IsImport()) return null;
            return g.Imports[Math.Abs(pi.Index) - 1].ObjectName.ToString();
        }

        int node0 = -1;
        for (int i = 0; i < nodes.Value.Length; i++)
        {
            if (nodes.Value[i] is StructPropertyData ns && NodeStem(ns) == "DA_Skill_Combo1")
            {
                if (node0 >= 0) throw new InvalidDataException("[Verso] graph: Combo1 twice");
                node0 = i;
            }
        }
        if (node0 < 0) throw new InvalidDataException("[Verso] graph: Combo1 node missing");
        var nodeStruct = (StructPropertyData)nodes.Value[node0];
        var su = (StructPropertyData)nodeStruct.Value[0];
        var skillOp = (ObjectPropertyData)su.Value[0];
        var comboImport = skillPtr(skillOp);

        // Ensure the AssaultZero DA import (mirror the Combo1 record shape).
        var comboImp = g.Imports[Math.Abs(comboImport.Index) - 1];
        var pkgPath = "/Game/Gameplay/SkillTree/Content/NoahVerso/DA_Skills/" + AZDa;
        FPackageIndex pkgIdx = null;
        for (int i = 0; i < g.Imports.Count; i++)
        {
            var imp = g.Imports[i];
            if (imp.ObjectName.ToString() == pkgPath && imp.ClassName.ToString() == "Package")
                pkgIdx = FPackageIndex.FromImport(i);
        }
        pkgIdx ??= g.AddImport(new Import("/Script/CoreUObject", "Package",
            FPackageIndex.FromRawIndex(0), pkgPath, false, g));
        // DA imports use the bare stem (no _C suffix): obj=DA_Skill_Combo1,
        // class BP_DataAsset_Skill_C, outer the DA package. A _C-suffixed
        // import dangles (node shows no description, skill never unlocks).
        FPackageIndex azIdx = null;
        for (int i = 0; i < g.Imports.Count; i++)
        {
            var imp = g.Imports[i];
            if (imp.ObjectName.ToString() == AZDa) azIdx = FPackageIndex.FromImport(i);
        }
        azIdx ??= g.AddImport(new Import(
            comboImp.ClassPackage.ToString(), comboImp.ClassName.ToString(), pkgIdx,
            AZDa, false, g));
        skillOp.Value = azIdx;

        // Repoint Combo1 edges → AssaultZero (vanilla: MarkingShot→Combo1).
        int repointed = 0;
        foreach (var e in edges.Value)
        {
            if (e is not StructPropertyData edgeStruct) continue;
            for (int i = 0; i < 2 && i < edgeStruct.Value.Count; i++)
            {
                if (edgeStruct.Value[i] is not ObjectPropertyData op || op.Value is null) continue;
                if (op.Value.IsImport() && op.Value.Index == comboImport.Index)
                {
                    op.Value = azIdx;
                    repointed++;
                }
            }
        }
        if (repointed != 1)
            throw new InvalidDataException($"[Verso] graph: Combo1 edges x{repointed} (expect 1)");
        g.Write(dest);
        Console.WriteLine($"[Verso] graph: node{node0} Combo1 -> AssaultZero (starter kept, {repointed} edge repointed)");
        return 2;

        FPackageIndex skillPtr(ObjectPropertyData op) => op.Value
            ?? throw new InvalidDataException("[Verso] graph: null skill ptr");
    }
}
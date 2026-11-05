using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace E33ModEditor.Core;

/// <summary>
/// Gustave ("Noah") move kit: rebalances Gustave's 8 vanilla skills to the AP
/// clamp curve, cleans his skill grid (DA_SkillGraph_Noah) of the 14 cut-content
/// "Gustave_SkillTreeUnlock" hidden nodes, and adds new moves built on his own
/// donor family:
///   • Zoom Zoom    — Powerful clone that applies Rush (the Speed buff) instead
///                    of Powerful to 1-5 allies.
///   • Hell Storm   — Strike Storm clone, 12 AP, Fire element, no charge gain.
///   • Cinder Swarm — Lumière Assault (Combo1) clone, 6 AP, Fire element, keeps
///                    the donor's crit→charge behavior.
/// Plus the requested edits: Recovery heals 40% (was 50%), Shatter costs 8 AP
/// (was 5), Overcharge (UnleashCharge) uses a backloaded per-charge-level damage
/// table (0-10) with a 1.5x break multiplier per level, max = clamp(AP) at 10.
///
/// Wired into rebuild-mod after the Lune kits. All assets are Gustave/Noah-local
/// (no cross-character retargets); LevelSequences are never written (repo hard
/// rule) — every clone keeps its donor's vanilla sequence.
/// </summary>
public static class GustaveSkillsKit
{
    public const string BalTable = "DT_Balancing_MainCharacterSkills";
    public const string IconTable = "DT_SkillIcons";
    public const string StringTable = "ST_MainCharacters_Skills";
    public const string GraphStem = "DA_SkillGraph_Noah";

    /// <summary>The modded graph's hidden nodes: 14 cut-content skills gated by
    /// Gustave_SkillTreeUnlock (an IsCutContent unlock item). Removing a node must
    /// also remove every edge touching it. The DA stems are exact import names in
    /// the graph's Nodes[].SkillUnlock.Skill ObjectProperty.</summary>
    private static readonly string[] HiddenNodeDaStems =
    {
        "DA_Skill_Blitz",
        "DA_Skill_Combo1",                 // shared Verso/Gustave copy (node 9) — NOT DA_Skill_Gustave_Combo1
        "DA_Skill_LightHolder",
        "DA_Skill_Verso_NEW_PhantomStars",
        "DA_Skill_Verso_AscendingAssault",
        "DA_Skill_Verso_NEW_Leadership",
        "DA_Skill_Verso_NEW_FromFire",
        "DA_Skill_Verso_NEW_Followup",
        "DA_Skill_Verso_DefiantStrike",
        "DA_Skill_Verso_NEW_QuickStrike",
        "DA_Skill_Verso_NEW_ParadigmShift",
        "DA_Skill_Verso_NEW_Purification",
        "DA_Skill_Verso_SteeledStrike",
        "DA_Skill_Verso_RadiantStrike",
    };

    /// <summary>AP clamp — same curve as the Lune kit.</summary>
    public static double Clamp(double ap) => SkillOverhaulKit.Clamp(ap);

    private static UAsset Open(string p)
    {
        var map = DataTableEditor.Mappings;
        return map != null ? new UAsset(p, EngineVersion.VER_UE5_4, map) : new UAsset(p, EngineVersion.VER_UE5_4);
    }

    /// <summary>Map a work/tables source path to the same relative path under the mod dir, replacing a stem.</summary>
    private static string StageDest(string srcUasset, string newStem, string modDir)
    {
        var full = srcUasset.Replace('\\', '/');
        const string marker = "/Content/";
        var idx = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) throw new InvalidDataException("not under Content/: " + srcUasset);
        var rel = full[(idx + marker.Length)..];
        rel = rel.Replace(Path.GetFileNameWithoutExtension(srcUasset), newStem, StringComparison.Ordinal);
        return Path.Combine(modDir, "Sandfall", "Content", rel);
    }

    // ------------------------------------------------------------------
    // A. Grid cleanup — DA_SkillGraph_Noah
    // ------------------------------------------------------------------

    /// <summary>
    /// Removes the 14 hidden Gustave_SkillTreeUnlock nodes (+ every edge touching
    /// them) from DA_SkillGraph_Noah, leaving Gustave's 8 visible skills. Idempotent:
    /// a graph that already lacks the hidden nodes is left untouched.
    /// </summary>
    public static int CleanGustaveGraph(string modDir)
    {
        var graphSrc = FileCopy.FindTablesAsset(GraphStem + ".uasset");
        if (graphSrc is null) { Console.WriteLine("[Gustave] " + GraphStem + " not under work/tables"); return 0; }
        var dest = StageDest(graphSrc, GraphStem, modDir);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!File.Exists(dest)) FileCopy.AssetPair(graphSrc, dest);
        var g = Open(dest);
        var gx = g.Exports.OfType<NormalExport>().FirstOrDefault();
        if (gx is null || gx.Data is not { Count: >= 2 }) return 0;
        var nodes = gx.Data[0] as ArrayPropertyData;
        var edges = gx.Data[1] as ArrayPropertyData;
        if (nodes is null || edges is null) return 0;

        var hidden = new HashSet<string>(HiddenNodeDaStems, StringComparer.Ordinal);
        int removed = 0;
        var kept = new List<PropertyData>();
        var hiddenImports = new HashSet<int>(); // raw import indices of removed nodes

        foreach (var n in nodes.Value)
        {
            if (n is not StructPropertyData nodeStruct) { kept.Add(n); continue; }
            string? daStem = NodeDaStem(g, nodeStruct);
            if (daStem is not null && hidden.Contains(daStem))
            {
                // record the import raw index so edges to it can be dropped
                var skillPtr = ((ObjectPropertyData)((StructPropertyData)nodeStruct.Value[0]).Value[0]).Value;
                if (skillPtr is { } sp && sp.IsImport())
                    hiddenImports.Add(Math.Abs(sp.Index));
                removed++;
                continue;
            }
            kept.Add(n);
        }
        if (removed == 0)
        {
            Console.WriteLine("[Gustave] graph already clean (" + GraphStem + ")");
            return 0;
        }
        nodes.Value = kept.ToArray();

        // Drop edges touching a removed node: compare by the edge's ObjectProperty
        // import raw index against the recorded hidden imports, AND by name for safety.
        int removedEdges = 0;
        var keptEdges = new List<PropertyData>();
        foreach (var e in edges.Value)
        {
            if (e is not StructPropertyData edgeStruct) { keptEdges.Add(e); continue; }
            bool touches = false;
            for (int i = 0; i < 2 && i < edgeStruct.Value.Count; i++)
            {
                if (edgeStruct.Value[i] is not ObjectPropertyData op || op.Value is null) continue;
                if (op.Value.IsImport() && hiddenImports.Contains(Math.Abs(op.Value.Index))) { touches = true; break; }
                if (op.Value.IsImport())
                {
                    var impName = g.Imports[Math.Abs(op.Value.Index) - 1].ObjectName.ToString();
                    if (hidden.Contains(impName)) { touches = true; break; }
                }
            }
            if (touches) { removedEdges++; continue; }
            keptEdges.Add(e);
        }
        edges.Value = keptEdges.ToArray();
        g.Write(dest);
        Console.WriteLine($"[Gustave] graph cleaned: removed {removed} hidden node(s) + {removedEdges} edge(s) from {GraphStem}");
        return removed + removedEdges;
    }

    private static string? NodeDaStem(UAsset g, StructPropertyData nodeStruct)
    {
        if (nodeStruct.Value is not { Count: >= 1 } || nodeStruct.Value[0] is not StructPropertyData su) return null;
        if (su.Value is not { Count: >= 1 } || su.Value[0] is not ObjectPropertyData op) return null;
        var idx = op.Value;
        if (idx is null || !idx.IsImport()) return null;
        return g.Imports[Math.Abs(idx.Index) - 1].ObjectName.ToString();
    }

    // ------------------------------------------------------------------
    // Entry point (wired into rebuild-mod)
    // ------------------------------------------------------------------

    public static int Apply(string modDir)
    {
        int n = 0;
        n += CleanGustaveGraph(modDir);
        n += Rebalance(modDir);
        n += RetargetGustaveRows(modDir);
        n += PatchOvercharge(modDir);
        n += CreateNewMoves(modDir);
        n += PatchBurnSynergy(modDir);
        n += PatchDescriptions(modDir);
        Console.WriteLine($"[Gustave] staged {n} asset edit(s)");
        return n;
    }

    /// <summary>All Gustave string-table description rewrites (ST entry key →
    /// text), factored out of <see cref="PatchDescriptions"/> so the graph
    /// editor can preview the exact pack-time text without staging a pack.
    /// Pure static data (Lune-style gold multipliers via
    /// <see cref="SkillDescriptionsKit"/>).</summary>
    public static IReadOnlyDictionary<string, string> DescriptionEdits()
    {
        var edits = new Dictionary<string, string>();
        void Set(string moveKey, string longText, string shortText)
        {
            edits[moveKey + "_Description_Long"] = longText;
            edits[moveKey + "_Description_Short"] = shortText;
        }

        // Lumière Assault (Combo1) — 3 AP, 5 hits, 0.198 per hit = 0.99 total = clamp(3). Crits build +1 charge.
        // Dynamic element (DynamicElementOverride, like its CinderSwarm clone), so the Long
        // carries the "Uses weapon's element." line to match Lumière Flurry.
        Set("Verso_Combo1_Gustave",
            $"Deals {SkillDescriptionsKit.Gold(0.198)} single target damage per hit. 5 hits.\nUses weapon's element.\n<keyword id=\"CriticalHit\">Critical Hits</> generate 1 additional <keyword id=\"Gustave_Charges\">Charge</>",
            $"{SkillDescriptionsKit.Gold(0.198)} {{DynamicElement}} damage per hit. 5 hits.\n<keyword id=\"CriticalHit\">Critical Hits</> generate 1 additional <keyword id=\"Gustave_Charges\">Charge</>");

        // From Fire — 4 AP, 3 hits, 0.44 per hit = 1.32 total = clamp(4). Dynamic element; heals
        // self 20% if the target is Burning (restored vanilla text).
        Set("Verso_FromFire_Gustave",
            $"Deals {SkillDescriptionsKit.Gold(0.44)} single target damage per hit. 3 hits.\nUses weapon's element.\n<keyword id=\"Heal\">Heals</> self by 20% if the target <keyword id=\"StatusEffect_Burn\">Burns</>",
            $"{SkillDescriptionsKit.Gold(0.44)} {{DynamicElement}} damage per hit. 3 hits.\n<keyword id=\"Heal\">Heals</> self by 20% Health if the target <keyword id=\"StatusEffect_Burn\">Burns</>");

        // Marking Shot — 2 AP, 1 hit, fixed Lightning, applies Mark. Base 0.33; ×2 OC = 0.66 = clamp(2).
        Set("Verso_MarkingShot_Gustave",
            $"Deals {SkillDescriptionsKit.Gold(0.33)} single target <keyword id=\"Element_Lightning\">Lightning</> damage and applies <keyword id=\"StatusEffect_Mark\">Mark</>. 1 hit.",
            $"{SkillDescriptionsKit.Gold(0.33)} <keyword id=\"Element_Lightning\">Lightning</> damage and applies <keyword id=\"StatusEffect_Mark\">Mark</>. 1 hit.");

        // Shatter (PerfectBreak) — 8 AP, 1 hit AoE Lightning, base total 2.64 = clamp(8). Break -> full charge.
        Set("Verso_PerfectBreak_Gustave",
            $"Deals {SkillDescriptionsKit.Gold(2.64)} <keyword id=\"Element_Lightning\">Lightning</> damage to all enemies. 1 hit.\nIf a target is <keyword id=\"Break\">Broken</> by the hit, Overcharge is fully <keyword id=\"Gustave_Charges\">Charged</>",
            $"{SkillDescriptionsKit.Gold(2.64)} <keyword id=\"Element_Lightning\">Lightning</> to all enemies. 1 hit.\nIf a target is <keyword id=\"Break\">Broken</> by the hit, Overcharge is fully <keyword id=\"Gustave_Charges\">Charged</>");

        // Recovery (PerfectRecovery) — heal 40% (was 50%), dispels, 0-2 charges.
        Set("Verso_PerfectRecovery_Gustave",
            "<keyword id=\"Heal\">Recovers</> 40% Health and dispels Status Effects.\nGives 0-2 <keyword id=\"Gustave_Charges\">Charges</>",
            "<keyword id=\"Heal\">Recovers</> 40% Health and dispels Status Effects.\nGives 0-2 <keyword id=\"Gustave_Charges\">Charges</>");

        // Powerful — buff, no damage multiplier (kept verbatim apart from keyword).
        Set("Verso_Powerful_Gustave",
            "Applies <keyword id=\"Buff_Powerful\">Powerful</> to 1-5 allies, increasing the damage they deal for 3 turns.\nGives 0 to 2 <keyword id=\"Gustave_Charges\">Charges</>",
            "Applies <keyword id=\"Buff_Powerful\">Powerful</> to 1-5 allies for 3 turns.\nGives 0 to 2 <keyword id=\"Gustave_Charges\">Charges</>");

        // Strike Storm — 7 AP, 6 hits, 0.385 per hit = 2.31 total = clamp(7). Crits build +2 charges.
        Set("Verso_StrikeStorm_Gustave",
            $"Deals {SkillDescriptionsKit.Gold(0.385)} single target damage per hit. 6 hits.\nUses weapon's element.\n<keyword id=\"CriticalHit\">Critical Hits</> generate 2 additional <keyword id=\"Gustave_Charges\">Charges</>",
            $"{SkillDescriptionsKit.Gold(0.385)} {{DynamicElement}} damage per hit. 6 hits.\nUses weapon's element.\n<keyword id=\"CriticalHit\">Critical Hits</> generate 2 additional <keyword id=\"Gustave_Charges\">Charges</>");

        // Overcharge (UnleashCharge) — 8 AP, charge-scaled finisher. 0.99 at 0 Charges,
        // 3.96 at 10 (1.5× clamp(8)). Description states a range + exponential wording,
        // no per-charge values. Both trailing effects are still live in the BP — it reads
        // UnleashCharge_BreakBarMultiplier (1.5, so it Breaks) and drains the gauge via
        // ChangeCharge(-GetCurrentChargeCount) — so both are stated; the earlier rewrite
        // dropped them from the vanilla text.
        Set("Verso_UnleashCharge",
            $"Deals <keyword id=\"Element_Lightning\">Lightning</> damage to a single enemy, scaling exponentially with <keyword id=\"Gustave_Charges\">Charges</> ({SkillDescriptionsKit.Gold(0.99)} to {SkillDescriptionsKit.Gold(3.96)}). 1 hit.\n" +
            "Can <keyword id=\"Break\">Break</>.\n" +
            "Resets <keyword id=\"Gustave_Charges\">Charges</>.",
            $"<keyword id=\"Element_Lightning\">Lightning</> damage scaling exponentially with <keyword id=\"Gustave_Charges\">Charges</> ({SkillDescriptionsKit.Gold(0.99)} to {SkillDescriptionsKit.Gold(3.96)}). 1 hit.\n" +
            "Can <keyword id=\"Break\">Break</>.\n" +
            "Resets <keyword id=\"Gustave_Charges\">Charges</>.");

        // New moves:
        // Zoom Zoom — 3 AP, Rush buff (Speed) to 1-5 allies, 0-2 charges.
        Set("Verso_ZoomZoom_Gustave",
            "Applies <keyword id=\"Buff_Rush\">Rush</> to 1-5 allies, increasing their speed for 3 turns.\nGives 0 to 2 <keyword id=\"Gustave_Charges\">Charges</>",
            "Applies <keyword id=\"Buff_Rush\">Rush</> to 1-5 allies for 3 turns.\nGives 0 to 2 <keyword id=\"Gustave_Charges\">Charges</>");

        // Hell Storm — 12 AP, 6 hits, 1.0272 per hit = 6.16 total = clamp(12).
        // No charge gain (stripped): shares Strike Storm's sequence but the BP
        // ChangeCharge delta is zeroed, so crits build nothing.
        Set("Verso_HellStorm_Gustave",
            $"Deals {SkillDescriptionsKit.Gold(1.0272)} single target damage per hit. 6 hits.\nUses weapon's element.",
            $"{SkillDescriptionsKit.Gold(1.0272)} {{DynamicElement}} damage per hit. 6 hits.\nUses weapon's element.");

        // Cinder Swarm — 6 AP, 5 hits, 0.396 per hit = 1.98 total = clamp(6). Crits build +1 charge. Dynamic element.
        Set("Verso_CinderSwarm_Gustave",
            $"Deals {SkillDescriptionsKit.Gold(0.396)} single target damage per hit. 5 hits.\nUses weapon's element.\n<keyword id=\"CriticalHit\">Critical Hits</> generate 1 additional <keyword id=\"Gustave_Charges\">Charge</>",
            $"{SkillDescriptionsKit.Gold(0.396)} {{DynamicElement}} damage per hit. 5 hits.\n<keyword id=\"CriticalHit\">Critical Hits</> generate 1 additional <keyword id=\"Gustave_Charges\">Charge</>");

        return edits;
    }

    /// <summary>String-table description rewrites for every Gustave move — vanilla voice with
    /// Lune-style gold damage multipliers (<c>DamageMultiplier</c> keyword, staged by
    /// SkillDescriptionsKit). Long/Short pairs use the same numbers; the Short form is terser.</summary>
    private static int PatchDescriptions(string modDir)
    {
        var stSrc = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), StringTable + ".uasset");
        if (stSrc is null) return 0;
        var stDest = StageDest(stSrc, StringTable, modDir);
        Directory.CreateDirectory(Path.GetDirectoryName(stDest)!);
        if (!File.Exists(stDest)) FileCopy.AssetPair(stSrc, stDest);

        var edits = new Dictionary<string, string>(DescriptionEdits());

        DataTableEditor.AddStringTableEntries(stDest, edits);
        Console.WriteLine($"[Gustave] description rewrites applied ({edits.Count} entries)");
        return edits.Count;
    }

    // ------------------------------------------------------------------
    // B. Rebalance all 8 existing moves to the curve (balancing rows + DA AP)
    // ------------------------------------------------------------------

    /// <summary>A balancing-row write: row name → FloatValue.</summary>
    private sealed record RowEdit(string Row, double Value);

    /// <summary>Row edits for the Gustave rebalance. Each damaging move's total =
    /// clamp(AP), written per-hit into that move's Gustave-specific row; the
    /// staged Gustave BPs are retargeted onto those rows by
    /// <see cref="RetargetGustaveRows"/> (Verso's moves keep reading the shared
    /// plain rows, so Verso is untouched). Recovery heals 40%; Overcharge rows
    /// are set by <see cref="PatchOvercharge"/>.</summary>
    private static readonly RowEdit[] RebalanceRows =
    {
        // Combo1 (Lumière Assault) — 3 AP, 5 hits, single target. 5 × 0.198 = 0.99 = clamp(3).
        new("Combo1_Gustave_BaseDamage", 0.198),
        // From Fire — 4 AP, 3 hits. 3 × 0.44 = 1.32 = clamp(4). Heal stays 20%.
        new("FromFire_Gustave_BaseDamage", 0.44),
        // Marking Shot — 2 AP, 1 hit, Lightning, applies Mark; overcharge ×2.
        // Base 0.33 (50% of clamp(2)), ×2 = 0.66 = clamp(2).
        new("MarkingShot_Gustave_BaseDamage", 0.33),
        new("MarkingShot_Gustave_OverchargeDamageMultiplier", 2.0),
        // Shatter (PerfectBreak) — 8 AP, 1 hit AoE Lightning. 2.64 = clamp(8).
        new("PerfectBreak_Gustave_BaseDamage", 2.64),
        // Recovery (PerfectRecovery) — heal 50% → 40%.
        new("PerfectRecovery_Gustave_BaseHeal", 0.4),
        // Strike Storm — 7 AP, 6 hits, uniform 0.385 per hit = 2.31 = clamp(7).
        // The BP has no LastHit read (every hit uses BaseDamage), so there is no
        // backloaded final hit — all 6 hits deal the same base.
        new("StrikeStorm_Gustave_BaseDamage", 0.385),
        // Overcharge rows are applied separately by PatchOvercharge (see OverchargeRows).
    };

    /// <summary>DA property edits (AP cost). Key = DA stem (without folder).</summary>
    private static readonly (string DaStem, string Prop, double Value)[] DaEdits =
    {
        // Shatter: 5 → 8 AP.
        ("DA_Skill_Gustave_PerfectBreak", "APCost", 8),
        // Overcharge (UnleashCharge): 4 → 8 AP — it is a charge-spending finisher, so it
        // sits above a plain 8-AP hit (full-charge total = 1.5× clamp(8), see OverchargeRows).
        ("DA_Skill_Gustave_UnleashCharge", "APCost", 8),
    };

    private static int Rebalance(string modDir)
    {
        // Balancing rows.
        var balSrc = FileCopy.FindTablesAsset(BalTable + ".uasset");
        if (balSrc is null) { Console.WriteLine("[Gustave] " + BalTable + " not under work/tables"); return 0; }
        var balDest = StageDest(balSrc, BalTable, modDir);
        Directory.CreateDirectory(Path.GetDirectoryName(balDest)!);
        if (!File.Exists(balDest)) FileCopy.AssetPair(balSrc, balDest);

        int n = 0;
        var rows = new Newtonsoft.Json.Linq.JObject();
        foreach (var e in RebalanceRows)
            rows[e.Row] = new Newtonsoft.Json.Linq.JObject { ["FloatValue"] = e.Value };
        var ed = new DataTableEditor(balDest);
        n += ed.Apply(rows.ToString(Newtonsoft.Json.Formatting.None));
        ed.Save(balDest);
        Console.WriteLine($"[Gustave] rebalanced {RebalanceRows.Length} balancing row(s)");

        // DA AP edits.
        foreach (var (stem, prop, value) in DaEdits)
        {
            var src = FileCopy.FindTablesAsset(stem + ".uasset");
            if (src is null) { Console.WriteLine("[Gustave] " + stem + " not found"); continue; }
            var dest = StageDest(src, stem, modDir);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);
            var daEd = DataTableEditor.OpenPropertyAsset(dest);
            n += daEd.ApplyToProperties($"{{\"{prop}\":{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");
            daEd.Save(dest);
            Console.WriteLine($"[Gustave] {stem} {prop} -> {value}");
        }
        return n;
    }

    // ------------------------------------------------------------------
    // B2. Point Gustave's staged BPs at his character-specific rows.
    // ------------------------------------------------------------------

    /// <summary>
    /// The vanilla Gustave BPs read the shared plain balancing rows
    /// (Combo1_BaseDamage, …) — the same rows Verso's versions of these moves
    /// read. The rebalance writes Gustave-specific &lt;Move&gt;_Gustave_ rows
    /// instead, so without a retarget the rebalance would be dead (and writing
    /// the plain rows would silently rebalance Verso too). This stages
    /// Gustave's four multi-hit BPs into the mod and retargets exactly one row
    /// each — BaseDamage only; BreakBar/Overcharge/Heal rows keep their shared
    /// vanilla values, which already match the Gustave variants. Verso's BPs
    /// are never staged, so Verso is untouched. Idempotent: on an already
    /// retargeted copy the pairs no longer match and nothing changes.
    /// </summary>
    private static readonly (string BpStem, string FromRow, string ToRow)[] GustaveRowRetargets =
    {
        ("BP_Battle_SkillScript_Gustave_Combo1", "Combo1_BaseDamage", "Combo1_Gustave_BaseDamage"),
        ("BP_Battle_SkillScript_Gustave_FromFire", "FromFire_BaseDamage", "FromFire_Gustave_BaseDamage"),
        ("BP_Battle_SkillScript_Gustave_MarkingShot", "MarkingShot_BaseDamage", "MarkingShot_Gustave_BaseDamage"),
        ("BP_Battle_SkillScript_Gustave_StrikeStorm", "StrikeStorm_BaseDamage", "StrikeStorm_Gustave_BaseDamage"),
    };

    private static int RetargetGustaveRows(string modDir)
    {
        int n = 0;
        foreach (var (bpStem, fromRow, toRow) in GustaveRowRetargets)
        {
            var src = FileCopy.FindTablesAsset(bpStem + ".uasset");
            if (src is null) { Console.WriteLine("[Gustave] " + bpStem + " not under work/tables"); continue; }
            var dest = StageDest(src, bpStem, modDir);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);
            var bp = Open(dest);
            RetargetPairs(bp, dest, (fromRow, toRow));
            bp.Write(dest);
            n++;
            Console.WriteLine($"[Gustave] {bpStem}: {fromRow} -> {toRow}");
        }
        return n;
    }

    // ------------------------------------------------------------------
    // C/E/F. New moves: Zoom Zoom, Hell Storm, Cinder Swarm
    // ------------------------------------------------------------------

    /// <summary>Spec for a new Gustave move cloned from an existing Gustave skill.</summary>
    public sealed record NewMove(
        string DaStem,          // e.g. "DA_Skill_Gustave_ZoomZoom"
        string BpStem,          // e.g. "BP_Battle_SkillScript_Gustave_ZoomZoom"
        string NameId,          // DA NameID, e.g. "ZoomZoom_Gustave" (display key)
        string DonorDa,         // e.g. "DA_Skill_Gustave_Powerful"
        string DonorBp,         // e.g. "BP_Battle_SkillScript_Gustave_Powerful"
        string DonorStem,       // BP retarget stem, e.g. "Gustave_Powerful"
        int ApCost,
        string StKeyPrefix,     // "Verso_ZoomZoom_Gustave"
        string DisplayName,
        string Description,
        string ShortDescription,
        string PrereqNode,      // graph prerequisite DA stem (visible Gustave node), e.g. "DA_Skill_Gustave_Powerful"
        FVector2D Position,
        double BaseDamage,
        string? Element = null,     // "Fire"/"Lightning": set DA BaseElementalType + MakeDamageBuilder byte
        (string From, string To)[]? ExtraPairs = null,  // FX/asset retargets
        string? BuffFrom = null,    // Zoom Zoom: swap applied buff class stem
        string? BuffTo = null,
        bool StripChargeGain = false,  // Hell Storm: remove crit->ChangeCharge statements
        bool ConsumeAllCharges = false, // Exploding Shot: drain the full charge gauge on cast
        bool FireAndPowerless = false,  // Exploding Shot: element byte -> Fire, Marked -> Powerless
        (string DonorRow, string NewRow, double? Value)[]? RowOverrides = null); // extra balancing rows

    /// <summary>New Gustave moves the mod adds to the Noah graph.</summary>
    public static readonly NewMove[] NewMoves =
    {
        // Zoom Zoom: Powerful clone — applies Rush (Speed buff) instead of Powerful
        // to 1-5 allies, still grants 0-2 charges. AP 3 (matches Powerful).
        new(
            "DA_Skill_Gustave_ZoomZoom", "BP_Battle_SkillScript_Gustave_ZoomZoom", "ZoomZoom_Gustave",
            "DA_Skill_Gustave_Powerful", "BP_Battle_SkillScript_Gustave_Powerful", "Gustave_Powerful",
            3, "Verso_ZoomZoom_Gustave", "Zoom Zoom",
            "Applies <keyword id=\"Buff_Rush\">Rush</> to 1-5 allies for 3 turns.\nGives 0 to 2 <keyword id=\"Gustave_Charges\">Charges</>",
            "Applies <keyword id=\"Buff_Rush\">Rush</> to 1-5 allies for 3 turns.\nGives 0 to 2 <keyword id=\"Gustave_Charges\">Charges</>",
            "DA_Skill_Gustave_Powerful", new FVector2D(475f, 425f),
            0.0,
            ExtraPairs: new[]
            {
                ("FX_BuffPowerful", "FX_BuffRush"),
            },
            BuffFrom: "BP_BattleBuff_Powerful_125", BuffTo: "BP_BattleBuff_Speed",
            RowOverrides: new[]
            {
                ("Powerful_PerfectionPerQte", "ZoomZoom_PerfectionPerQte", (double?)1.0),
            }),
        // Hell Storm: Strike Storm clone — 12 AP, 6 hits. 1.0272 per hit = 6.16
        // = clamp(12). No charge gain (BP ChangeCharge zeroed).
        new(
            "DA_Skill_Gustave_HellStorm", "BP_Battle_SkillScript_Gustave_HellStorm", "HellStorm_Gustave",
            "DA_Skill_Gustave_Strikestorm", "BP_Battle_SkillScript_Gustave_StrikeStorm", "Gustave_StrikeStorm",
            12, "Verso_HellStorm_Gustave", "Hell Storm",
            "Deals <keyword id=\"DamageMultiplier\">1.03x</> single target damage per hit. 6 hits.\nUses weapon's element.",
            "<keyword id=\"DamageMultiplier\">1.03x</> {DynamicElement} damage per hit. 6 hits.\nUses weapon's element.",
            "DA_Skill_Gustave_Strikestorm", new FVector2D(850f, 625f),
            0.0,
            StripChargeGain: true,
            RowOverrides: new[]
            {
                ("StrikeStorm_Gustave_BaseDamage", "HellStorm_BaseDamage", (double?)1.0272),
                ("StrikeStorm_Gustave_BreakBarMultiplier", "HellStorm_BreakBarMultiplier", null),
                ("StrikeStorm_OverchargeDamageMultiplier", "HellStorm_OverchargeDamageMultiplier", (double?)1.0),
            }),
        // Lumière Flurry (Cinder Swarm): Lumière Assault (Combo1) clone — 6 AP, 5 hits,
        // weapon element, keeps charge gain. Display name "Lumière Flurry".
        new(
            "DA_Skill_Gustave_CinderSwarm", "BP_Battle_SkillScript_Gustave_CinderSwarm", "CinderSwarm_Gustave",
            "DA_Skill_Gustave_Combo1", "BP_Battle_SkillScript_Gustave_Combo1", "Gustave_Combo1",
            6, "Verso_CinderSwarm_Gustave", "Lumière Flurry",
            "Deals <keyword id=\"DamageMultiplier\">0.40x</> single target damage per hit. 5 hits.\nUses weapon's element.\n" +
            "<keyword id=\"CriticalHit\">Critical Hits</> generate 1 additional <keyword id=\"Gustave_Charges\">Charge</>",
            "<keyword id=\"DamageMultiplier\">0.40x</> {DynamicElement} damage per hit. 5 hits.\n" +
            "<keyword id=\"CriticalHit\">Critical Hits</> generate 1 additional <keyword id=\"Gustave_Charges\">Charge</>",
            "DA_Skill_Gustave_Combo1", new FVector2D(475f, 325f),
            0.0,
            RowOverrides: new[]
            {
                ("Combo1_Gustave_BaseDamage", "CinderSwarm_BaseDamage", (double?)0.396),
                ("Combo1_Gustave_BreakBarMultiplier", "CinderSwarm_BreakBarMultiplier", null),
                ("Combo1_OverchargeDamageMultiplier", "CinderSwarm_OverchargeDamageMultiplier", (double?)1.0),
            }),
        // Exploding Shot: Marking Shot clone — 4 AP, Fire element, applies Powerless
        // for 5 turns (duration inherited verbatim from the Marking Shot donor
        // ApplyBuff literal; FireAndPowerlessSwap only retargets element + class).
        new(
            "DA_Skill_Gustave_ExplodingShot", "BP_Battle_SkillScript_Gustave_ExplodingShot", "ExplodingShot_Gustave",
            "DA_Skill_Gustave_MarkingShot", "BP_Battle_SkillScript_Gustave_MarkingShot", "Gustave_MarkingShot",
            4, "Verso_ExplodingShot_Gustave", "Exploding Shot",
            "Deals <keyword id=\"DamageMultiplier\">1.32x</> <keyword id=\"Element_Fire\">Fire</> damage and applies <keyword id=\"Buff_Powerless\">Powerless</> for 5 turns. 1 hit.",
            "<keyword id=\"DamageMultiplier\">1.32x</> <keyword id=\"Element_Fire\">Fire</> damage and applies <keyword id=\"Buff_Powerless\">Powerless</> for 5 turns. 1 hit.",
            "DA_Skill_Gustave_MarkingShot", new FVector2D(850f, 425f),
            0.0,
            FireAndPowerless: true,
            Element: "Fire",   // DA display field (tooltip icon); the BP byte is the damage element
            RowOverrides: new[]
            {
                ("MarkingShot_Gustave_BaseDamage", "ExplodingShot_BaseDamage", (double?)1.32),
                ("MarkingShot_Gustave_BreakBarMultiplier", "ExplodingShot_BreakBarMultiplier", null),
                ("MarkingShot_OverchargeDamageMultiplier", "ExplodingShot_OverchargeDamageMultiplier", (double?)1.0),
            }),
    };

    private static int CreateNewMoves(string modDir)
    {
        int n = 0;
        foreach (var m in NewMoves)
            n += CreateClone(modDir, m);
        return n;
    }

    private static int CreateClone(string modDir, NewMove m)
    {
        var donorDa = FileCopy.FindTablesAsset(m.DonorDa + ".uasset");
        var donorBp = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), m.DonorBp + ".uasset");
        if (donorDa is null || donorBp is null)
        {
            Console.WriteLine($"[Gustave] clone donor missing for {m.NameId}: DA={donorDa != null} BP={donorBp != null}");
            return 0;
        }
        var balSrc = FileCopy.FindTablesAsset(BalTable + ".uasset");
        var iconSrc = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), IconTable + ".uasset");
        var stSrc = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), StringTable + ".uasset");
        var graphSrc = FileCopy.FindTablesAsset(GraphStem + ".uasset");
        if (balSrc is null || iconSrc is null || stSrc is null || graphSrc is null)
        {
            Console.WriteLine($"[Gustave] shared asset missing for {m.NameId}");
            return 0;
        }

        var daDest = StageDest(donorDa, m.DaStem, modDir);
        var bpDest = StageDest(donorBp, m.BpStem, modDir);
        var balDest = StageDest(balSrc, BalTable, modDir);
        var iconDest = StageDest(iconSrc, IconTable, modDir).Replace("/Datatables/", "/DataTables/");
        var stDest = StageDest(stSrc, StringTable, modDir);
        var gDest = StageDest(graphSrc, GraphStem, modDir);
        foreach (var d in new[] { daDest, bpDest, balDest, iconDest, stDest, gDest })
            Directory.CreateDirectory(Path.GetDirectoryName(d)!);

        // String table entries.
        if (!File.Exists(stDest)) FileCopy.AssetPair(stSrc, stDest);
        DataTableEditor.AddStringTableEntries(stDest, new Dictionary<string, string>
        {
            [m.StKeyPrefix + "_Name"] = m.DisplayName,
            [m.StKeyPrefix + "_Description_Long"] = m.Description,
            [m.StKeyPrefix + "_Description_Short"] = m.ShortDescription,
        });

        // 1. DA clone.
        FileCopy.AssetPair(donorDa, daDest);
        var da = Open(daDest);
        RetargetPairs(da, daDest, (m.DonorDa, m.DaStem), (m.DonorStem, m.NameId));
        foreach (var ex in da.Exports.OfType<NormalExport>())
            foreach (var p in ex.Data)
            {
                if (p.Name.ToString().Equals("SkillScript", StringComparison.OrdinalIgnoreCase) && p is SoftObjectPropertyData soft)
                {
                    var pkg = "/Game/Gameplay/Battle/Skills/Content/Gustave/" + m.BpStem;
                    soft.Value = new FSoftObjectPath(new FName(da, pkg), new FName(da, m.BpStem + "_C"), null);
                    if (da.SoftObjectPathList is { Count: > 0 } list) list.Add(soft.Value);
                }
                else if (p.Name.ToString() == "Name" && p is TextPropertyData tname)
                    tname.Value = new FString(m.StKeyPrefix + "_Name");
                else if (p.Name.ToString() == "Description" && p is TextPropertyData tdesc)
                    tdesc.Value = new FString(m.StKeyPrefix + "_Description_Long");
                else if (p.Name.ToString() == "ShortDescription" && p is TextPropertyData tshort)
                    tshort.Value = new FString(m.StKeyPrefix + "_Description_Short");
                else if (p.Name.ToString() == "APCost" && p is IntPropertyData ap)
                    ap.Value = m.ApCost;
                else if (p.Name.ToString() == "BaseElementalType" && p is EnumPropertyData bpe && m.Element is { } el)
                    bpe.Value = FName.FromString(da, "EAttackType::" + StainsMechanicKit.Enumerator(el));
                else if (p.Name.ToString() == "NameID" && p is NamePropertyData nid)
                    nid.Value = FName.FromString(da, m.NameId);
            }
        da.FolderName = new FString("/Game/Gameplay/SkillTree/Content/Gustave/" + m.DaStem);
        da.Write(daDest);

        // 2. BP clone + surgical retargets.
        FileCopy.AssetPair(donorBp, bpDest);
        var bp = Open(bpDest);
        var pairs = new List<(string From, string To)>
        {
            (m.DonorBp, m.BpStem),
            (m.DonorStem, m.NameId),
        };
        if (m.ExtraPairs is not null) pairs.AddRange(m.ExtraPairs);
        // Row retargets: the donor BP references <DonorMove>_* rows; retarget to <NewMove>_*.
        pairs.Add((DonorRowPrefix(m), m.NameId.Replace("_Gustave", "") + "_"));
        RetargetPairs(bp, bpDest, pairs.ToArray());
        bp.FolderName = new FString("/Game/Gameplay/Battle/Skills/Content/Gustave/" + m.BpStem);

        // NOTE: the element is NOT forced on the BP. Cinder Swarm / Hell Storm are cloned
        // from element-DYNAMIC donors (Combo1 / Strike Storm). Four distinct Fire-forcing
        // strategies — (1) replace the MakeDamageBuilder element param, (2) + flow repair,
        // (3) + dead-statement deletion, (4) replace ONLY the GetBaseDamageType assignment
        // RHS leaving the builder byte-identical — all pass kismet-check and round-trip
        // byte-stable, and all crash the move in-game at runtime. Conclusion (2026-09-04):
        // UAssetAPI cannot re-serialize a hand-modified OnActionEffect for this BP family
        // in a way the game VM accepts; only untouched functions survive. The clones
        // inherit the weapon element like their donors; descriptions say "Uses weapon's element".

        // Buff swap (Zoom Zoom): BP_BattleBuff_Powerful_125 -> BP_BattleBuff_Speed.
        if (m.BuffFrom is { } bf && m.BuffTo is { } bt)
            SwapBuffClass(bp, bf, bt);

        // Exploding Shot: element byte -> Fire, Marked -> Powerless.
        if (m.FireAndPowerless)
            FireAndPowerlessSwap(bp);

        // Charge-strip (Hell Storm): remove crit->ChangeCharge statements.
        if (m.StripChargeGain)
            StripChargeGainStatements(bp);

        // Consume all charges (Exploding Shot) — DISABLED: the drain-block clone from
        // Overcharge corrupted the function (10 bad kismet targets; the block-range
        // detection grabbed 48 unrelated statements). Exploding Shot ships as a clean
        // Marking Shot clone for now; charge consumption needs a precise same-BP edit.
        // if (m.ConsumeAllCharges)
        //     DrainAllCharges(bp);

        bp.Write(bpDest);

        // 3. Balancing rows: clone donor rows then set values.
        if (!File.Exists(balDest)) FileCopy.AssetPair(balSrc, balDest);
        var bal = new DataTableEditor(balDest);
        foreach (var (donorRow, newRow, val) in m.RowOverrides ?? Array.Empty<(string, string, double?)>())
        {
            if (bal.HasRow(donorRow))
            {
                bal.CloneRow(donorRow, newRow);
                if (val is double v)
                    bal.Apply($"{{\"{newRow}\":{{\"FloatValue\":{v.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}}}");
            }
        }
        bal.Save(balDest);

        // 4. Icon row: clone donor's icon row to the new NameId.
        if (!File.Exists(iconDest)) FileCopy.AssetPair(iconSrc, iconDest);
        var icon = new DataTableEditor(iconDest);
        string donorIconRow = IconRowFor(m);
        if (icon.HasRow(donorIconRow))
        {
            icon.CloneRow(donorIconRow, m.NameId);
            icon.Save(iconDest);
        }
        else
        {
            Console.WriteLine($"[Gustave] icon donor row '{donorIconRow}' not found; new skill may render hidden");
        }

        // 5. Graph node (append linked from the prerequisite).
        if (!File.Exists(gDest)) FileCopy.AssetPair(graphSrc, gDest);
        AppendGraphNode(gDest, m);

        Console.WriteLine($"[Gustave] {m.NameId} ({m.ApCost} AP) staged");
        return 6;
    }

    private static string DonorRowPrefix(NewMove m) => m.DonorStem switch
    {
        "Gustave_Powerful" => "Powerful_",
        "Gustave_StrikeStorm" => "StrikeStorm_",
        "Gustave_Combo1" => "Combo1_",
        "Gustave_FromFire" => "FromFire_",
        "Gustave_MarkingShot" => "MarkingShot_",
        _ => m.DonorStem + "_",
    };

    private static string IconRowFor(NewMove m) => m.DonorStem switch
    {
        "Gustave_Powerful" => "Powerful_Gustave",
        "Gustave_StrikeStorm" => "StrikeStorm_Gustave",
        "Gustave_Combo1" => "Combo1_Gustave",
        "Gustave_FromFire" => "FromFire_Gustave",
        "Gustave_MarkingShot" => "MarkingShot_Gustave",
        _ => m.DonorStem,
    };

    /// <summary>Surgical name retarget across the whole name map (mirror of LuneSkillsKit.RetargetPairs).</summary>
    internal static void RetargetPairs(UAsset asset, string dest, params (string From, string To)[] pairs)
    {
        var names = asset.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s)) continue;
            // Every clone keeps its donor's vanilla LevelSequence (repo hard rule —
            // sequences are never written). A blanket substring rename would rewrite
            // e.g. SEQ_SkillMaelle_BurningCanvas_V2 into a non-existent sequence and
            // leave the move waiting forever on a missing asset.
            // Shared buff classes are likewise never cloned: a prefix retarget once
            // rewrote BP_Buff_EndSliceForetellConsumedCount into a ghost buff on a
            // Sciel custom DA, silently dropping its battle-start counter.
            if (s.Contains("/Game/Gameplay/EpicBattleSequence/", StringComparison.Ordinal)
                || s.StartsWith("SEQ_", StringComparison.Ordinal)
                || s.Contains("BP_Buff_", StringComparison.Ordinal))
                continue;
            foreach (var (from, to) in pairs)
            {
                if (s.Contains(from, StringComparison.Ordinal))
                {
                    asset.SetNameReference(i, new FString(s.Replace(from, to, StringComparison.Ordinal)));
                    break;
                }
            }
        }
    }

    private static string LocalVarName(KismetPropertyPointer p)
    {
        var path = p.New?.Path;
        if (path is { Length: > 0 }) return string.Join(".", path.Select(x => x?.ToString()));
        if (p.Old is not null) return p.Old.ToString();
        return p.ToString();
    }

    /// <summary>
    /// Point the clone's applied-buff references at a different buff class. The clone's
    /// broad row retarget renames the donor's buff import (e.g. BP_BattleBuff_Powerful_125
    /// becomes BP_BattleBuff_&lt;NewMove&gt;_125, which does not exist as an asset), so this
    /// rewrites those imports to the target buff's real package + class (e.g. the Rush /
    /// Speed buff at /Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_Speed) and re-points
    /// the bytecode ObjectConsts that used them.
    /// </summary>
    private static void SwapBuffClass(UAsset bp, string fromStem, string toStem)
    {
        string toPkg = toStem switch
        {
            "BP_BattleBuff_Speed" => "/Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_Speed",
            _ => "/Game/Gameplay/Buffs/ParentClasses/" + toStem,
        };
        // 1. Find the donor-buff imports (the clone may have renamed *_125 -> <move>_125).
        var oldIdxs = new List<int>();
        for (int i = 0; i < bp.Imports.Count; i++)
        {
            var nm = bp.Imports[i].ObjectName.ToString();
            if (nm.StartsWith(fromStem, StringComparison.Ordinal)
                || (nm.Contains("BattleBuff_") && nm.Contains("_125")))
                oldIdxs.Add(i);
        }
        // 2. Ensure the target buff class + its package import exist.
        FPackageIndex? toPkgIdx = null;
        int toClassIdx = -1;
        for (int i = 0; i < bp.Imports.Count; i++)
        {
            if (bp.Imports[i].ObjectName.ToString() == toPkg && bp.Imports[i].ClassName.ToString() == "Package")
                toPkgIdx = FPackageIndex.FromImport(i);
            if (bp.Imports[i].ObjectName.ToString() == toStem + "_C")
                toClassIdx = i;
        }
        toPkgIdx ??= bp.AddImport(new Import("/Script/CoreUObject", "Package", FPackageIndex.FromRawIndex(0), toPkg, false, bp));
        if (toClassIdx < 0)
            toClassIdx = bp.AddImport(new Import("/Script/Engine", "BlueprintGeneratedClass", toPkgIdx, toStem + "_C", false, bp)).Index;

        // 3. Rewrite the old buff imports to the target class (reuse the slot).
        foreach (var i in oldIdxs)
        {
            var imp = bp.Imports[i];
            if (imp.ObjectName.ToString().EndsWith("_C", StringComparison.Ordinal) && imp.ClassName.ToString() == "BlueprintGeneratedClass")
            {
                imp.ObjectName = new FName(bp, toStem + "_C");
                imp.OuterIndex = toPkgIdx;
            }
            // package-level import (outer of Default__): leave, harmless
        }

        // 4. Re-point bytecode ObjectConsts that referenced any old buff import to the target.
        int n = 0;
        foreach (var fn in bp.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            foreach (var s in fn.ScriptBytecode)
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (n > 0) return;
                    if (e is EX_ObjectConst oc && oc.Value is { } oi && oi.IsImport())
                    {
                        var imp = oc.Value.ToImport(bp);
                        if (imp.ObjectName.ToString().Contains("BattleBuff", StringComparison.Ordinal)
                            && imp.ObjectName.ToString().Contains("_125", StringComparison.Ordinal))
                        { oc.Value = FPackageIndex.FromImport(toClassIdx); n++; }
                    }
                });
        }
        if (n > 0) Console.WriteLine($"[Gustave] buff swap: {fromStem} -> {toStem} ({n} ObjectConst(s) re-pointed)");
    }

    /// <summary>
    /// Remove the "if (HasHit &amp;&amp; IsCriticalHit) ChangeCharge(+N)" statements from a
    /// cloned skill BP (Hell Storm does not build charges). The gate is
    /// EX_LetBool(BooleanAND(HasHit, IsCriticalHit)) followed by EX_PopExecutionFlowIfNot
    /// and the ChangeCharge block. We neutralize it by removing the ChangeCharge call's
    /// effect (rewrite the charge delta to 0) rather than deleting statements — safer than
    /// statement surgery (no offset shifting).
    /// </summary>
    private static void StripChargeGainStatements(UAsset bp)
    {
        int n = 0;
        foreach (var fn in bp.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            foreach (var s in fn.ScriptBytecode)
            {
                // ChangeCharge is a VirtualFunction on the charge component; its delta is
                // the parameter after the function (usually an IntConst or a local).
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (e is EX_Context ctx && ctx.ContextExpression is EX_VirtualFunction vf
                        && vf.VirtualFunctionName.ToString() == "ChangeCharge")
                    {
                        foreach (var p in vf.Parameters)
                            if (p is EX_IntConst ic && ic.Value != 0) { ic.Value = 0; n++; return; }
                    }
                });
            }
        }
        if (n > 0) Console.WriteLine($"[Gustave] charge gain stripped ({n} ChangeCharge delta(s) -> 0)");
    }

    /// <summary>
    /// Exploding Shot: consume the ENTIRE charge gauge on cast. Clones the proven drain
    /// block from Overcharge (UnleashCharge) — the same Charge component family, so the
    /// GetComponentByClass(Charge) context + ChangeCharge(-count) shape is identical:
    ///   LetObj comp = CurrentCharacter.GetComponentByClass(Charge_Component_C)
    ///   Let count  = comp.GetCurrentChargeCount()
    ///   Let neg    = count × -1
    ///   comp.ChangeCharge(neg)
    /// The statements are injected at the top of OnActionEffect (before damage).
    /// </summary>
    private static void DrainAllCharges(UAsset bp)
    {
        // Find the donor Overcharge BP (same-family source of the drain statements).
        var ocSrc = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), "BP_Battle_SkillScript_Gustave_UnleashCharge.uasset");
        if (ocSrc is null) { Console.WriteLine("[Gustave] drain: Overcharge donor not found"); return; }
        var oc = Open(ocSrc);
        var ocFn = oc.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (ocFn?.ScriptBytecode is null) { Console.WriteLine("[Gustave] drain: no Overcharge OnActionEffect"); return; }

        // Find Overcharge's drain: the ChangeCharge whose delta is a local named
        // CallFunc_Multiply_IntInt_ReturnValue (the drain = count × -1), not a bare
        // IntConst (a charge GAIN like Strike Storm's +2). Scan top-down; the drain is the
        // LAST ChangeCharge in OnActionEffect.
        int changeIdx = -1;
        var ocStmts = ocFn.ScriptBytecode;
        for (int i = ocStmts.Length - 1; i >= 0 && changeIdx < 0; i--)
        {
            bool isDrain = false;
            MayhemFixKit.Walk(new[] { ocStmts[i] }, e =>
            {
                if (e is EX_Context ctx && ctx.ContextExpression is EX_VirtualFunction vf
                    && vf.VirtualFunctionName.ToString() == "ChangeCharge")
                    foreach (var p in vf.Parameters)
                        if (p is EX_LocalVariable lv && LocalVarName(lv.Variable).Contains("CallFunc_Multiply_IntInt_ReturnValue", StringComparison.Ordinal)) isDrain = true;
            });
            if (isDrain) changeIdx = i;
        }
        if (changeIdx < 0)
        {
            Console.WriteLine("[Gustave] drain: Overcharge drain ChangeCharge not found");
            return;
        }
        // Back up: capture statements from the last LetObj(GetComponentByClass(Charge))
        // before changeIdx through changeIdx (the drain run: LetObj, GetCurrentChargeCount,
        // Multiply(count,-1), ChangeCharge).
        int drainStart = changeIdx;
        while (drainStart > 0)
        {
            bool isLetObj = false;
            MayhemFixKit.Walk(new[] { ocStmts[drainStart] }, e =>
            {
                if (e is EX_LetObj lo && lo.AssignmentExpression is EX_FinalFunction ff
                    && FnName(ff, oc) == "GetComponentByClass") isLetObj = true;
            });
            if (isLetObj) break;
            drainStart--;
        }
        int drainEnd = changeIdx;

        // Clone the drain statements into Exploding Shot's OnActionEffect. Map the Charge
        // component import (must exist in Exploding Shot's clone; add if missing via the
        // donor's import identity).
        var bpFn = bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (bpFn?.ScriptBytecode is null) return;

        // Ensure the Charge component class import exists in Exploding Shot.
        int chargeCls = -1;
        for (int i = 0; i < bp.Imports.Count; i++)
            if (bp.Imports[i].ObjectName.ToString() == "BP_UniqueMechanic_Charge_Component_C") chargeCls = i;
        if (chargeCls < 0)
        {
            // Add the package + class imports for the shared Charge component with known paths.
            const string pkgPath = "/Game/Gameplay/Battle/UniqueMechanics/Charge/BP_UniqueMechanic_Charge_Component";
            FPackageIndex pkgIdx = FPackageIndex.FromRawIndex(0);
            for (int i = 0; i < bp.Imports.Count; i++)
                if (bp.Imports[i].ObjectName.ToString() == pkgPath && bp.Imports[i].ClassName.ToString() == "Package")
                { pkgIdx = FPackageIndex.FromImport(i); break; }
            if (pkgIdx.IsNull())
            {
                var pkgIdx2 = bp.AddImport(new Import("/Script/CoreUObject", "Package", FPackageIndex.FromRawIndex(0), pkgPath, false, bp));
                pkgIdx = pkgIdx2;
            }
            var clsIdx = bp.AddImport(new Import("/Script/Engine", "BlueprintGeneratedClass", pkgIdx,
                "BP_UniqueMechanic_Charge_Component_C", false, bp));
            chargeCls = Math.Abs(clsIdx.Index);
            Console.WriteLine($"[Gustave] drain: added charge import at {chargeCls}");
        }

        // Clone statements drainStart..drainEnd and re-point the Charge ObjectConst to the
        // Exploding Shot import index.
        if (chargeCls < 0) { Console.WriteLine("[Gustave] drain: charge component import could not be created"); return; }
        var block = new List<KismetExpression>();
        for (int i = drainStart; i <= drainEnd; i++)
        {
            var clone = MayhemFixKit.CloneKismet(ocStmts[i]);
            MayhemFixKit.Walk(new[] { clone }, e =>
            {
                if (e is EX_ObjectConst oc2 && oc2.Value.IsImport()
                    && oc2.Value.ToImport(oc).ObjectName.ToString().Contains("Charge_Component", StringComparison.Ordinal))
                    oc2.Value = FPackageIndex.FromImport(chargeCls);
            });
            block.Add(clone);
        }

        // Insert the drain at the TOP of OnActionEffect (index 0), shifting all offsets.
        uint blockSize = (uint)block.Sum(b => (long)b.GetSize(bp));
        MayhemFixKit.ShiftAbsolute(bp, bpFn, 0, blockSize, null);
        var list = bpFn.ScriptBytecode.ToList();
        list.InsertRange(0, block);
        bpFn.ScriptBytecode = list.ToArray();
        MayhemFixKit.RecalcContextSkips(bp, bpFn);
        Console.WriteLine($"[Gustave] ExplodingShot: charge-drain block ({block.Count} stmts) injected at OnActionEffect start");
    }

    /// <summary>
    /// Exploding Shot: make the Marking Shot clone deal FIRE and apply POWERLESS instead of
    /// Mark. Both edits are same-BP and size-preserving (the proven-safe class):
    ///   1. MakeDamageBuilder element byte 4 (Lightning) -> 2 (Fire) — the first ByteConst
    ///      in the builder's params, mirroring LuneSkillsKit.PatchDamageElement.
    ///   2. The ApplyBuff(Marked) buff-class ObjectConst -> Powerless (import retarget).
    /// </summary>
    private static void FireAndPowerlessSwap(UAsset bp)
    {
        int n = 0;
        // 1. Element byte -> Fire (2).
        foreach (var fn in bp.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            foreach (var s in fn.ScriptBytecode)
            {
                EX_VirtualFunction? mdb = null;
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (mdb is null && e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "MakeDamageBuilder") mdb = vf;
                });
                if (mdb is null) continue;
                foreach (var p in mdb.Parameters)
                    if (p is EX_ByteConst bc) { bc.Value = 2; n++; break; }
            }
        }
        if (n > 0) Console.WriteLine($"[Gustave] ExplodingShot: element -> Fire (byte 2, {n} builder)");

        // 2. Marked buff -> Powerless.
        int swapped = 0;
        var pwr = EnsureObjectImport(bp, "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Powerless",
            "BP_BattleBuff_Powerless_C", "BP_BattleBuff_Powerless_C", "/Script/Engine");
        foreach (var fn in bp.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            foreach (var s in fn.ScriptBytecode)
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (e is EX_ObjectConst oc && oc.Value is { } oi && oi.IsImport())
                    {
                        var imp = oi.ToImport(bp);
                        if (imp.ObjectName.ToString().Contains("BP_BattleBuff_Marked", StringComparison.Ordinal))
                        { oc.Value = pwr; swapped++; }
                    }
                });
        }
        if (swapped > 0) Console.WriteLine($"[Gustave] ExplodingShot: Marked -> Powerless ({swapped} ObjectConst(s))");
    }

    /// <summary>Append a graph node + edge to the Noah graph, linked from the prerequisite node.</summary>
    private static void AppendGraphNode(string gDest, NewMove m)
    {
        var g = Open(gDest);
        var gx = g.Exports.OfType<NormalExport>().First();
        var nodes = (ArrayPropertyData)gx.Data[0];
        var edges = (ArrayPropertyData)gx.Data[1];
        FPackageIndex prereqIdx = null;
        Import templateImp = null;
        bool already = false;
        for (int i = 0; i < nodes.Value.Length; i++)
        {
            var nodeStruct = (StructPropertyData)nodes.Value[i];
            var su = (StructPropertyData)nodeStruct.Value[0];
            var skillPtr = ((ObjectPropertyData)su.Value[0]).Value;
            if (skillPtr is null || skillPtr.IsNull() || !skillPtr.IsImport()) continue;
            var impName = g.Imports[Math.Abs(skillPtr.Index) - 1].ObjectName.ToString();
            if (impName == m.DaStem) already = true;
            if (impName == m.PrereqNode)
            {
                prereqIdx = skillPtr;
                templateImp = g.Imports[Math.Abs(skillPtr.Index) - 1];
            }
        }
        if (already) return;
        if (prereqIdx is null)
            throw new InvalidDataException($"Gustave graph prerequisite {m.PrereqNode} not found");
        var daPkgPath = "/Game/Gameplay/SkillTree/Content/Gustave/" + m.DaStem;
        var newDaIdx = EnsureObjectImport(g, daPkgPath, m.DaStem,
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
        var posWrap = (StructPropertyData)node.Value[1];
        ((Vector2DPropertyData)posWrap.Value[0]).Value = m.Position;
        nodes.Value = nodes.Value.Append(node).ToArray();

        var edge = (StructPropertyData)edges.Value[0].Clone();
        ((ObjectPropertyData)edge.Value[0]).Value = prereqIdx;
        ((ObjectPropertyData)edge.Value[1]).Value = newDaIdx;
        edges.Value = edges.Value.Append(edge).ToArray();
        g.Write(gDest);
        Console.WriteLine($"[Gustave] graph node appended: {m.DaStem} from {m.PrereqNode}");
    }

    internal static FPackageIndex EnsureObjectImport(UAsset asset, string packagePath, string objectName,
        string className, string classPackage)
    {
        FPackageIndex pkgIndex = null;
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (imp.ObjectName.ToString() == packagePath && imp.ClassName.ToString() == "Package")
                pkgIndex = FPackageIndex.FromImport(i);
            if (imp.ObjectName.ToString() == objectName && imp.ClassName.ToString() == className)
                return FPackageIndex.FromImport(i);
        }
        pkgIndex ??= asset.AddImport(new Import("/Script/CoreUObject", "Package",
            FPackageIndex.FromRawIndex(0), packagePath, false, asset));
        return asset.AddImport(new Import(classPackage, className, pkgIndex, objectName, false, asset));
    }

    // ------------------------------------------------------------------
    // D. Overcharge (UnleashCharge) — 8 AP charge finisher, 1.5x curve at full charge
    // ------------------------------------------------------------------

    /// <summary>
    /// Overcharge's BP computes <c>dmg = Base × QTE × (1 + DamageIncreasePerCharge×count)
    /// × (IsFullyCharged ? DamageMultiplierFullCharges : 1)</c> and IsFullyCharged is
    /// true only at 10/10. Overcharge is an 8 AP charge-spending finisher (DA APCost 4→8),
    /// so its full-charge total sits at 1.5× the clamp — the same "resource nuke above the
    /// clamp" convention as Lune's Dark/4-elemental rules:
    ///   • clamp(8) = 2.64; 1.5× = 3.96 at 10/10 charges.
    ///   • Base = 0.99 (25% of the max — firing at 0 charges is weak, as a finisher should be).
    ///   • perCharge = 0.06 → a gentle ramp below full (0.99 at 0 → ~1.5 at 9).
    ///   • FullCharges = 2.5 → 0.99 × (1 + 0.06×10) × 2.5 = 3.96 = 1.5 × clamp(8).
    ///   • BreakBarMultiplier = 1.5 → break = 1.5× the hit damage (the user's "1.5× curve
    ///     break": at full charge, break = 1.5 × 3.96 = 5.94 = 2.25 × clamp(8)).
    /// The per-tier sequences (VeryWeak/Medium/Strong/VeryStrong) still drive presentation.
    /// </summary>
    private static readonly RowEdit[] OverchargeRows =
    {
        new("UnleashCharge_BaseDamage", 0.99),
        new("UnleashCharge_DamageIncreasePerCharge", 0.06),
        new("UnleashCharge_DamageMultiplierFullCharges", 2.5),
        new("UnleashCharge_BreakBarMultiplier", 1.5),
    };

    private static int PatchOvercharge(string modDir)
    {
        // Overcharge damage is handled entirely by the row values above (the BP formula
        // already multiplies by DamageMultiplierFullCharges only when fully charged).
        // The rows are applied by Rebalance(); stage a fresh BP copy so the stale
        // archive one (previously deleted by GameplayTweaksKit) is not shipped.
        var balSrc = FileCopy.FindTablesAsset(BalTable + ".uasset");
        if (balSrc is null) return 0;
        var balDest = StageDest(balSrc, BalTable, modDir);
        Directory.CreateDirectory(Path.GetDirectoryName(balDest)!);
        if (!File.Exists(balDest)) FileCopy.AssetPair(balSrc, balDest);
        var rows = new Newtonsoft.Json.Linq.JObject();
        foreach (var e in OverchargeRows)
            rows[e.Row] = new Newtonsoft.Json.Linq.JObject { ["FloatValue"] = e.Value };
        var ed = new DataTableEditor(balDest);
        int n = ed.Apply(rows.ToString(Newtonsoft.Json.Formatting.None));
        ed.Save(balDest);

        const string bpStem = "BP_Battle_SkillScript_Gustave_UnleashCharge";
        var bpSrc = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), bpStem + ".uasset");
        if (bpSrc is null) { Console.WriteLine("[Gustave] Overcharge BP not found"); return n; }
        var bpDest2 = StageDest(bpSrc, bpStem, modDir);
        Directory.CreateDirectory(Path.GetDirectoryName(bpDest2)!);
        if (!File.Exists(bpDest2)) FileCopy.AssetPair(bpSrc, bpDest2);
        Console.WriteLine($"[Gustave] Overcharge rebalanced (1.5x clamp(8) at full charge); fresh BP staged");
        return n;
    }

    // ------------------------------------------------------------------
    // E. Burn synergy — Cinder Swarm (burn-stacker) + Strike Storm (detonator)
    // ------------------------------------------------------------------

    private static int PatchBurnSynergy(string modDir)
    {
        int n = 0;
        n += PatchCinderSwarmBurn(modDir);
        n += PatchStrikeStormDetonate(modDir);
        return n;
    }

    /// <summary>
    /// Cinder Swarm burn-stacker — DISABLED. The transplant (cloning Immolation's burn
    /// block with import re-pointing + flattening the marked branch) corrupted the
    /// function (garbage names, stale switch offsets, 6 bad kismet targets) and would
    /// crash in-game. Cross-BP statement transplant proved too fragile on this family.
    /// Cinder Swarm remains a working weapon-element move (crits build charges).
    /// </summary>
    private static int PatchCinderSwarmBurn(string modDir)
    {
        return 0;
    }


    private static void RetargetNames(KismetExpression e, UAsset bp, params (string From, string To)[] pairs)
    {
        MayhemFixKit.Walk(new[] { e }, x =>
        {
            if (x is EX_NameConst nc)
                foreach (var (from, to) in pairs)
                    if (nc.Value.ToString() == from) nc.Value = FName.FromString(bp, to);
        });
    }

    private static string FnName(EX_FinalFunction ff, UAsset a) =>
        ff.StackNode is { } sn && sn.IsImport() ? sn.ToImport(a).ObjectName.ToString() : "?";

    /// <summary>
    /// Strike Storm burn-detonator: when the single target has ≥5 Burn, the move deals ×2
    /// total and consumes 5 Burn. The ×2 is applied by doubling the damage value fed to
    /// MakeDamageBuilder; the consume drops the target's Burn stack by 5.
    /// </summary>
    private static int PatchStrikeStormDetonate(string modDir)
    {
        return 0;
    }
}

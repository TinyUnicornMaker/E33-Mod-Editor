using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
/// Sciel custom moves — 7 brand-new skills cloned from donors, chained west
/// from Our Sacrifice. Always on (no gate).
///
/// Damage rule: same 0.75 × clamp(AP) curve as the base kit. Per-hit row =
/// total ÷ hits. Gradient-style totals do not apply here; every number below
/// is a curve value.
///
/// Clone pipeline per move (lune-wildfire precedent): donor DA → new stem,
/// donor battle BP → new stem (name-map renames only), balancing rows cloned
/// + set, DT_SkillIcons row cloned, DA APCost set, ST strings added, graph
/// node + edge appended to DA_SkillGraph_Sciel.
///
/// Behavior deltas beyond the donor (Black Death self-absorb, Last Chance
/// death, False Start slow, Cursed Frenzy single-target, Soul Transplant
/// revive gate) are kismet spikes applied after the clone; see the
/// Phase-2 methods below.
/// </summary>
public static class ScielCustomKit
{
    public const string BalTable = ScielSkillsKit.BalTable;
    public const string StringTable = ScielSkillsKit.StringTable;
    private const string GraphStem = "DA_SkillGraph_Sciel";
    private const string IconTable = "DT_SkillIcons";

    public static double PerHit(double ap, int hits) => ScielSkillsKit.PerHit(ap, hits);

    private static string Gold(double v) => SkillDescriptionsKit.Gold(v);
    private const string Dark = "<keyword id=\"Element_Dark\">Dark</>";
    private const string Foretell = "<keyword id=\"Foretell\">Foretell</>";
    private const string AP = "<keyword id=\"APShard\">AP</>";
    private const string Break = "<keyword id=\"Break\">Break</>";
    private const string GreaterSlow = "<keyword id=\"Buff_Slow\">Greater Slow</>";
    private const string Powerful = "<keyword id=\"Buff_Powerful\">Powerful</>";
    private const string Rush = "<keyword id=\"Buff_Rush\">Rush</>";
    private const string Burn = "<keyword id=\"StatusEffect_Burn\">Burn</>";
    private const string Cursed = "<keyword id=\"Debuff_Cursed\">Cursed</>";
    private const string CriticalHits = "<keyword id=\"CriticalHit\">Critical Hits</>";
    private const string Twilight = "<keyword id=\"Foretell_Twilight\">Twilight</>";
    private const string Berserk = "<keyword id=\"Debuff_Berserk\">Berserk</>";
    private const string KeywordStyleTable = "DT_RichTextDecorator_KeywordStyles";

    /// <summary>One custom move: clone sources + new identity + numbers.</summary>
    private sealed record CustomMove(
        string Key, string Name, string DonorDa, string DonorBp, string DonorIcon,
        string DonorRowPrefix, string IconKey, int ApCost, int SpCost,
        double PosX, double PosY, string PrereqDa,
        (string DonorRow, string NewRow, double? Value)[] BalRows,
        string Long, string Short);

    private static readonly CustomMove[] Moves =
    {
        new("BlackDeath", "Black Death",
            "DA_Skill_Sciel_OurSacrifice", "BP_Battle_SkillScript_Sciel_OurSacrifice", "OurSacrifice",
            "OurSacrifice", "BlackDeath", 7, 5, 350.0, 875.0, "DA_Skill_Sciel_OurSacrifice",
            new (string, string, double?)[]
            {
                ("OurSacrifice_BaseDamage", "BlackDeath_BaseDamage", PerHit(7, 1)),
                ("OurSacrifice_BreakBarMultiplier", "BlackDeath_BreakBarMultiplier", null),
                ("OurSacrifice_DamageIncreasePerForetell", "BlackDeath_DamageIncreasePerForetell", 0.05),
                ("OurSacrifice_StolenHPMultiplier", "BlackDeath_StolenHPMultiplier", 1.5),
            },
            $"Deals {Gold(PerHit(7, 1))} {Dark} damage to all enemies. 1 hit.\nAbsorbs Sciel's Health: deal up to 50% more damage.\nConsumes all {Foretell} to deal 5% more damage per {Foretell}.",
            $"{Gold(PerHit(7, 1))} {Dark} damage to all enemies. 1 hit.\nAbsorbs Sciel's Health (max +50%).\nConsumes {Foretell} to deal 5% more damage each."),
        new("LastChance", "Last Chance",
            "DA_Skill_Sciel_Intervention", "BP_Battle_SkillScript_Sciel_Intervention", "Intervention",
            "Intervention", "ScielLastChance", 4, 6, 225.0, 875.0, "DA_Skill_Sciel_BlackDeath",
            new (string, string, double?)[]
            {
                ("Intervention_APGain", "LastChance_APGain", 5.0),
            },
            $"Targeted ally plays immediately and gains 5 {AP}.\nApplies {Powerful}, {Rush} and {Berserk} for 1 turn.\nInflicts {Cursed} for 2 turns.",
            $"Targeted ally plays immediately and gains 5 {AP}.\nApplies {Powerful}, {Rush} and {Berserk} for 1 turn.\nInflicts {Cursed} for 2 turns."),
        new("FalseStart", "False Start",
            "DA_Skill_Sciel_new18_Postponed", "BP_Battle_SkillScript_Sciel_New18_Postoned", "Postponed",
            "Postponed", "FalseStart", 11, 7, 100.0, 875.0, "DA_Skill_Sciel_LastChance",
            new (string, string, double?)[]
            {
                ("Postponed_BaseDamage", "FalseStart_BaseDamage", PerHit(11, 2)),
                ("Postponed_BreakBarMultiplier", "FalseStart_BreakBarMultiplier", null),
                ("Postponed_DamageIncreasePerForetell", "FalseStart_DamageIncreasePerForetell", 0.05),
                ("Postponed_InitiativeReductionPerForetell", "FalseStart_InitiativeReductionPerForetell", null),
            },
            $"Deals {Gold(PerHit(11, 2))} single target damage. 2 hits.\nUses weapon's element.\nConsumes {Foretell} to deal 5% more damage per {Foretell}, delays the target's turn and inflicts {GreaterSlow} for 3 turns.",
            $"{{DynamicElement}} damage. 2 hits.\nConsumes {Foretell} to deal 5% more damage each, delays the target and inflicts {GreaterSlow} for 3 turns."),
        new("SpectersEdge", "Specter's Edge",
            "DA_Skill_Sciel_new4_ShadowDrop", "BP_Battle_SkillScript_Sciel_New4_ShadowDrop", "ShadowDrop",
            "ShadowDrop", "SpectersEdge", 12, 8, 100.0, 725.0, "DA_Skill_Sciel_FalseStart",
            new (string, string, double?)[]
            {
                ("ShadowDrop_BaseDamage", "SpectersEdge_BaseDamage", PerHit(12, 1)),
                ("ShadowDrop_BreakBarMultiplier", "SpectersEdge_BreakBarMultiplier", null),
                ("ShadowDrop_DamageIncreasePerForetell", "SpectersEdge_DamageIncreasePerForetell", 0.10),
            },
            $"Deals {Gold(PerHit(12, 1))} single target {Dark} damage. 1 hit.\nConsumes all {Foretell} to deal 10% more damage per {Foretell}.\nCan {Break}.",
            $"{Gold(PerHit(12, 1))} {Dark} damage. 1 hit.\nConsumes {Foretell} to deal 10% more damage each.\nCan {Break}."),
        new("TripleHex", "Triple Hex",
            "DA_Skill_Sciel_AllSet", "BP_Battle_SkillScript_Sciel_AllSet", "AllSet",
            "AllSet", "TripleHex", 2, 9, 225.0, 725.0, "DA_Skill_Sciel_SpectersEdge",
            Array.Empty<(string, string, double?)>(),
            $"Inflicts 1 {Burn} and 1 {Berserk} on all allies for 3 turns and inflicts {Cursed} for 3 turns.",
            $"Inflicts 1 {Burn} and 1 {Berserk} on all allies for 3 turns and inflicts {Cursed} for 3 turns."),
        new("CursedFrenzy", "Cursed Frenzy",
            "DA_Skill_Sciel_AllSet", "BP_Battle_SkillScript_Sciel_AllSet", "AllSet",
            "AllSet", "CursedFrenzy", 10, 10, 350.0, 725.0, "DA_Skill_Sciel_TripleHex",
            Array.Empty<(string, string, double?)>(),
            $"Inflicts {Cursed} for 5 turns on one ally and inflicts 5 {Berserk} and 5 {Burn} for 5 turns.",
            $"Inflicts {Cursed} for 5 turns on one ally and inflicts 5 {Berserk} and 5 {Burn} for 5 turns."),
        new("SoulTransplant", "Soul Transplant",
            "DA_Skill_Sciel_Intervention", "BP_Battle_SkillScript_Sciel_Intervention", "Intervention",
            "Intervention", "SoulTransplant", 5, 5, 475.0, 725.0, "DA_Skill_Sciel_CursedFrenzy",
            new (string, string, double?)[]
            {
                ("Intervention_APGain", "SoulTransplant_ReviveHP", 1.0),
            },
            $"Revives dead allies at 1 Health and inflicts {Cursed} on them for 3 turns.",
            $"Revives dead allies at 1 Health, inflicting {Cursed} for 3 turns."),
        new("UltimaPath", "Ultima Path",
            "DA_Skill_Sciel_FinalPath", "BP_Battle_SkillScript_Sciel_New_FinalPath", "FinalPath",
            "FinalPath", "UltimaPath", 21, 14, 600.0, 725.0, "DA_Skill_Sciel_SoulTransplant",
            new (string, string, double?)[]
            {
                ("FinalPath_BaseDamage", "UltimaPath_BaseDamage", PerHit(21, 1)),
                ("FinalPath_BaseForetell", "UltimaPath_BaseForetell", 10.0),
                ("FinalPath_BreakBarMultiplier", "UltimaPath_BreakBarMultiplier", null),
            },
            $"Deals {Gold(PerHit(21, 1))} single target {Dark} damage. 1 hit.\nApplies 20 {Foretell}.\nCan {Break}.\nDuring {Twilight}, extends {Twilight} duration by 1 turn.",
            $"{Gold(PerHit(21, 1))} {Dark} damage. 1 hit.\nApplies 20 {Foretell}.\nCan {Break}.\nExtends {Twilight} duration by 1 turn."),
        new("UltimasBargain", "Ultima's Bargain",
            "DA_Skill_Sciel_OurSacrifice", "BP_Battle_SkillScript_Sciel_OurSacrifice", "OurSacrifice",
            "OurSacrifice", "UltimasBargain", 20, 14, 725.0, 725.0, "DA_Skill_Sciel_UltimaPath",
            new (string, string, double?)[]
            {
                ("OurSacrifice_BaseDamage", "UltimasBargain_BaseDamage", PerHit(20, 1)),
                ("OurSacrifice_BreakBarMultiplier", "UltimasBargain_BreakBarMultiplier", null),
                ("OurSacrifice_DamageIncreasePerForetell", "UltimasBargain_DamageIncreasePerForetell", 0.10),
                ("OurSacrifice_StolenHPMultiplier", "UltimasBargain_StolenHPMultiplier", 5.0),
            },
            $"Deals {Gold(PerHit(20, 1))} {Dark} damage to all enemies. 1 hit.\nConsumes Sciel's life force: deal up to 5x damage.\nInflicts {Cursed} on self for 1 turn.\nConsumes all {Foretell} to deal 10% more damage per {Foretell}.",
            $"{Gold(PerHit(20, 1))} {Dark} damage to all enemies. 1 hit.\nSciel's life force consumed (up to 5x).\nInflicts {Cursed} for 1 turn.\nKills Sciel."),
        new("SecondLife", "Second Life",
            "DA_Skill_Sciel_Intervention", "BP_Battle_SkillScript_Sciel_Intervention", "Intervention",
            "Intervention", "SecondLife", 17, 12, 600.0, 575.0, "DA_Skill_Sciel_SoulTransplant",
            Array.Empty<(string, string, double?)>(),
            $"Applies Aureole to a living ally, reviving them if they die.\nInflicts {Cursed} for 7 turns.",
            $"Applies Aureole to a living ally.\nInflicts {Cursed} for 7 turns."),
        new("PhantomSever", "Phantom Sever",
            "DA_Skill_Sciel_SpectralSweep", "BP_Battle_SkillScript_Sciel_New_SpectralSweep", "SpectralSweep",
            "SpectralSweep", "PhantomSever", 15, 12, 725.0, 575.0, "DA_Skill_Sciel_SecondLife",
            new (string, string, double?)[]
            {
                ("SpectralSweep_BaseDamage", "PhantomSever_BaseDamage", PerHit(15, 4)),
                ("SpectralSweep_ForetellPerHit", "PhantomSever_ForetellPerHit", 1.0),
                ("SpectralSweep_ForetellPerCrit", "PhantomSever_ForetellPerCrit", 2.0),
                ("SpectralSweep_ForetellChancePerHitLow", "PhantomSever_ForetellChancePerHitLow", null),
                ("SpectralSweep_ForetellChancePerHitMedium", "PhantomSever_ForetellChancePerHitMedium", null),
                ("SpectralSweep_ForetellChancePerHitHigh", "PhantomSever_ForetellChancePerHitHigh", null),
                ("SpectralSweep_BreakBarMultiplier", "PhantomSever_BreakBarMultiplier", null),
            },
            $"Deals {Gold(PerHit(15, 4))} single target {Dark} damage per hit. 2-6 hits.\nApplies 1 {Foretell} per hit.\n{CriticalHits} inflict 2 additional {Foretell}.",
            $"{Gold(PerHit(15, 4))} {Dark} damage per hit. 2-6 hits.\n{CriticalHits} inflict 2 {Foretell}."),
        new("Corruption", "Corruption",
            "DA_Skill_Sciel_EndSlice", "BP_Battle_SkillScript_Sciel_EndSlice", "EndSlice",
            "EndSlice", "Corruption", 21, 14, 850.0, 575.0, "DA_Skill_Sciel_PhantomSever",
            new (string, string, double?)[]
            {
                ("EndSlice_BaseDamage", "Corruption_BaseDamage", PerHit(21, 1)),
                ("EndSlice_DamageIncreasePerForetell", "Corruption_DamageIncreasePerForetell", 0.05),
                ("EndSlice_BreakBarMultiplier", "Corruption_BreakBarMultiplier", null),
            },
            $"Deals {Gold(PerHit(21, 1))} single target damage. 1 hit.\nUses weapon's element.\nDeal 5% more damage per {Foretell} consumed this battle.",
            $"{Gold(PerHit(21, 1))} damage. 1 hit.\nDeal 5% more damage per {Foretell} consumed this battle."),
    };

    private static UAsset Open(string p)
    {
        var map = DataTableEditor.Mappings;
        return map != null ? new UAsset(p, EngineVersion.VER_UE5_4, map) : new UAsset(p, EngineVersion.VER_UE5_4);
    }

    private static string FindAsset(string stem)
        => FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), stem + ".uasset")
           ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "extracted"), stem + ".uasset")
           ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "probe-custom"), stem + ".uasset")
           ?? FileCopy.FindTablesAsset(stem + ".uasset")
           ?? throw new FileNotFoundException(stem + ".uasset not found");

    private static string StageDest(string srcUasset, string modDir, string? newStem = null)
    {
        var full = srcUasset.Replace('\\', '/');
        const string marker = "/Content/";
        var idx = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) throw new InvalidDataException("not under Content/: " + srcUasset);
        var rel = full[(idx + marker.Length)..];
        if (newStem != null)
            rel = rel.Replace(Path.GetFileNameWithoutExtension(srcUasset), newStem, StringComparison.Ordinal);
        return Path.Combine(modDir, "Sandfall", "Content", rel);
    }

    private static void Stage(string src, string modDir, out string dest, string? newStem = null)
    {
        dest = StageDest(src, modDir, newStem);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);
    }

    public static int Apply(string modDir)
    {
        int n = 0;
        n += CloneMoves(modDir);
        n += PatchTripleHex(modDir);
        n += PatchFalseStart(modDir);
        n += PatchBlackDeath(modDir, "BP_Battle_SkillScript_Sciel_BlackDeath");
        n += PatchLastChance(modDir);
        n += PatchUltimaPathDoubleForetell(modDir);
        n += PatchUltimaTwilight(modDir);
        n += PatchUltimasBargain(modDir);
        n += PatchSoulTransplant(modDir, "BP_Battle_SkillScript_Sciel_SoulTransplant", "SoulTransplant_ReviveHP", 3, "SoulTransplant", addRevive: true, addAureole: false);
        n += PatchSoulTransplantTargeting(modDir, "DA_Skill_Sciel_SoulTransplant");
        // Second Life: live-ally curse + Aureole, NO revive — keeps the
        // donor's native live-ally targeting (no Rebirth transplant) and no
        // resurrect splice. (A stale SecondLife_ReviveHP row may linger in
        // staged bal tables; nothing reads it.)
        n += PatchSoulTransplant(modDir, "BP_Battle_SkillScript_Sciel_SecondLife", "SecondLife_ReviveHP", 7, "SecondLife", addRevive: false, addAureole: true);
        n += PatchCorruptionSkillType(modDir);
        n += PatchCorruptionBuffRef(modDir);
        n += PatchCursedFrenzy(modDir);
        n += PatchCursedFrenzyTargeting(modDir);
        n += PatchKeywordStyles(modDir);
        n += PatchNamesAndDescriptions(modDir);
        n += PatchIcons(modDir);
        n += PatchGraph(modDir);
        Console.WriteLine($"[ScielCustom] staged {n} asset edit(s)");
        return n;
    }

    /// <summary>Editor baseline registration: the 7 customs as chained nodes
    /// (DA stem, prereq DA stem, position, display name, AP cost).</summary>
    public static IEnumerable<(string DaStem, string PrereqDaStem, double X, double Y, string Name, int ApCost)> AuthoredAdds()
    {
        foreach (var m in Moves)
            yield return ("DA_Skill_Sciel_" + m.Key, m.PrereqDa, m.PosX, m.PosY, m.Name, m.ApCost);
    }

    /// <summary>Editor preview: modded Long description for a custom ST base key.</summary>
    public static bool TryPreviewDescription(string baseKey, out string longText)
    {
        foreach (var m in Moves)
        {
            if (!string.Equals("Sciel_" + m.Key, baseKey, StringComparison.Ordinal)) continue;
            longText = m.Long;
            return true;
        }
        longText = "";
        return false;
    }

    // ── 1. DA + BP clones, costs, balancing rows ─────────────────────────────

    private static int CloneMoves(string modDir)
    {
        int n = 0;
        var balSrc = FindAsset(BalTable);
        Stage(balSrc, modDir, out var balDest);
        var bal = new DataTableEditor(balDest);

        foreach (var m in Moves)
        {
            var daStem = "DA_Skill_Sciel_" + m.Key;
            var bpStem = "BP_Battle_SkillScript_Sciel_" + m.Key;

            var daSrc = FindAsset(m.DonorDa);
            Stage(daSrc, modDir, out var daDest, daStem);
            var da = Open(daDest);
            // Full-stem pairs first (RetargetPairs stops at the first match
            // per name): the SkillScript path must resolve to the new BP stem,
            // not a substring-renamed ghost (e.g. New4_SpectersEdge).
            var rowPrefix = m.Key == "LastChance" ? "ScielLastChance" : m.Key;
            LuneSkillsKit.RetargetPairs(da, daDest,
                (m.DonorDa, daStem),
                (m.DonorBp, bpStem),
                (m.DonorRowPrefix, rowPrefix));
            // NameID must match the icon-table row; LastChance uses the
            // collision-free ScielLastChance key.
            foreach (var ex in da.Exports.OfType<NormalExport>())
                foreach (var p in ex.Data)
                {
                    if (p is NamePropertyData np
                        && np.Name.ToString() == "NameID")
                        np.Value = FName.FromString(da, m.IconKey);
                    // ST keys are FStrings in the text history (not name-map
                    // FNames), so RetargetPairs misses them: point Name /
                    // Description / ShortDescription at the new move's keys.
                    if (p is TextPropertyData tp && tp.TableId?.Value?.Value is { Length: > 0 })
                    {
                        var pn = p.Name.ToString();
                        var suffix = pn == "Name" ? "Name"
                            : pn == "Description" ? "Description_Long"
                            : pn == "ShortDescription" ? "Description_Short" : null;
                        if (suffix != null)
                            tp.Value = new FString("Sciel_" + m.Key + "_" + suffix);
                    }
                }
            // FolderName must be the clone's own path (keeps the donor's
            // otherwise — the game then fails to resolve the new asset).
            da.FolderName = new FString("/Game/Gameplay/SkillTree/Content/Sciel/Skills/" + daStem);
            da.Write(daDest);
            var daEd = DataTableEditor.OpenPropertyAsset(daDest);
            try
            {
                n += daEd.ApplyToProperties("{\"APCost\":" + m.ApCost + "}");
            }
            catch (KeyNotFoundException)
            {
                DataTableEditor.EnsureIntProperty(daDest, "APCost", m.ApCost);
                n++;
                daEd = DataTableEditor.OpenPropertyAsset(daDest);
            }
            daEd.Save(daDest);
            n++;

            var bpSrc = FindAsset(m.DonorBp);
            Stage(bpSrc, modDir, out var bpDest, bpStem);
            var bp = Open(bpDest);
            if (m.Key == "LastChance")
            {
                // "LastChance" collides with an existing icon row: internal
                // prefix is ScielLastChance, but the BP stem keeps LastChance.
                // Full-stem pair first: RetargetPairs stops at the first match
                // per name, so the BP path must win over the short prefix.
                LuneSkillsKit.RetargetPairs(bp, bpDest,
                    (m.DonorBp, bpStem),
                    ("Intervention_APGain", "ScielLastChance_APGain"),
                    (m.DonorRowPrefix, "ScielLastChance"));
            }
            else
            {
                LuneSkillsKit.RetargetPairs(bp, bpDest,
                    (m.DonorBp, bpStem),
                    (m.DonorRowPrefix, m.Key));
            }
            // FolderName must be the clone's own path (donor path breaks
            // in-game resolution of the new script).
            bp.FolderName = new FString("/Game/Gameplay/Battle/Skills/Content/Sciel/" + bpStem);
            bp.Write(bpDest);
            n++;

            var rows = new JObject();
            foreach (var (donorRow, newRow, val) in m.BalRows)
            {
                var targetRow = newRow.Replace(m.Key, rowPrefix, StringComparison.Ordinal);
                bal.CloneRow(donorRow, targetRow);
                if (val is { } v)
                    rows[targetRow] = new JObject { ["FloatValue"] = v };
            }
            if (rows.Count > 0)
            {
                n += bal.Apply(rows.ToString(Newtonsoft.Json.Formatting.None));
            }
            Console.WriteLine($"[ScielCustom] cloned {m.Key} (DA+BP, {m.ApCost} AP, {m.BalRows.Length} bal rows)");
        }
        bal.Save(balDest);
        return n;
    }

    // ── 2. Triple Hex buff swaps (proven ObjectConst + TurnDuration techniques) ─

    private static int PatchTripleHex(string modDir)
    {
        var bpStem = "BP_Battle_SkillScript_Sciel_TripleHex";
        var dest = FindStaged(modDir, bpStem);
        var bp = Open(dest);
        var fn = bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (fn?.ScriptBytecode is null) return 0;
        var swaps = new Dictionary<string, (string Cls, string Pkg)>(StringComparer.Ordinal)
        {
            ["BP_BattleBuff_Speed_C"] = ("BP_BattleBuff_Burn_C", "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Burn"),
            ["BP_BattleBuff_Defense15_C"] = ("BP_BattleBuff_Cursed_C", "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Cursed"),
            ["BP_BattleBuff_Powerful_125_C"] = ("BP_BattleBuff_Berserk_C", "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Berserk"),
        };
        int n = 0, nDur = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in fn.ScriptBytecode)
        {
            // Buff-class swap across every statement (the class const need not
            // share a statement with TurnDuration).
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_ObjectConst oc && oc.Value.IsImport())
                {
                    var name = oc.Value.ToImport(bp).ObjectName.ToString();
                    seen.Add(name);
                    if (swaps.TryGetValue(name, out var cf))
                    {
                        oc.Value = LuneSkillsKit.EnsureObjectImport(bp, cf.Pkg, cf.Cls, cf.Cls, "/Script/Engine");
                        n++;
                    }
                }
            });
            bool hasDuration = false;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_NameConst nc && nc.Value.ToString() == "TurnDuration") hasDuration = true;
            });
            if (!hasDuration) continue;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_IntConst ic) { ic.Value = 3; nDur++; }
            });
        }
        if (n > 0) bp.Write(dest);
        Console.WriteLine($"[ScielCustom] TripleHex buff swaps x{n}, TurnDuration -> 3 (x{nDur})");
        Console.WriteLine("[ScielCustom] TripleHex buff imports seen: " + string.Join(",", seen.Where(s => s.Contains("Buff"))));
        return n > 0 ? 1 : 0;
    }

    private static string FindStaged(string modDir, string stem)
    {
        var hit = Directory.GetFiles(modDir, stem + ".uasset", SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new FileNotFoundException("staged " + stem + " not found");
        return hit;
    }

    // ── 2f. Soul Transplant: spend-10 revive ─────────────────────────────────
    // Donor Grim Harvest deals damage (kept) + heals allies (NOP'd: spec says
    // revive INSTEAD of healing). Appended end-block phases:
    //   sum loop: sum += GetForetellCountOnCharacter(enemy) per enemy.
    //   gate: Less(sum,10) + NOT + JumpIfNot -> end.
    //   consume loop: ForetellToConsume = count; ConsumeForetell (verbatim).
    //   dead fetch: manager = GetBattleManager [PhoenixFlame #34 clone];
    //     deadArr = manager.DeadPlayerCharacters [PhoenixFlame #35 clone,
    //     owner re-homed to the mirrored manager class].
    //   dead loop: revive each (amount = MaxHP x 0.5 row) + Cursed on revived.
    // Rules: same-asset clones; Fill() positional sets on source-verified
    // members; FNames re-interned; imports mirrored with full outer chains;
    // temps native dead slots (fail-loud harvest + distinctness asserts).

    private static KismetPropertyPointer STPtr(List<KismetExpression> stmts, string exact, string what, bool member = false)
    {
        foreach (var s in stmts)
        {
            KismetPropertyPointer? hit = null;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (hit is not null) return;
                if (!member && e is EX_LocalVariable lv && PtrName(lv.Variable) == exact) hit = lv.Variable;
                if (member && e is EX_InstanceVariable iv && PtrName(iv.Variable) == exact) hit = iv.Variable;
                if (!member && e is EX_Let l && l.Value is { } v && PtrName(v) == exact) hit = v;
            });
            if (hit is not null) return hit;
        }
        throw new InvalidDataException($"SoulTransplant: {what} pointer '{exact}' not found");
    }

    private static KismetExpression STClone(List<KismetExpression> stmts, Func<KismetExpression, bool> pred, string what)
    {
        foreach (var s in stmts)
            if (pred(s)) return (KismetExpression)MayhemFixKit.CloneKismet(s);
        throw new InvalidDataException($"SoulTransplant: template '{what}' not found");
    }

    // ── 2g. Soul Transplant v2: Intervention donor + Rebirth revive splice ────
    // Donor: Sciel's Intervention (targeted-ally loop, VFX/sound all native;
    // its AP gift + force-next are stripped: pure revive + curse).
    // Spliced per target right after the target fetch:
    //   Resurrect(flat HP baked from the SoulTransplant_ReviveHP row,
    //     caster stats) [Rebirth #35 shape]
    //   Doom curse spawn, CursedFrenzy-style TurnDuration set (3), Doom
    //   curse apply on the revived ally.
    // No foretell cost, no damage, no AP gift: revive + curse, always.

    private static int PatchSoulTransplant(string modDir, string bpStem,
        string reviveRow, int curseTurns, string tag, bool addRevive, bool addAureole)
    {
        var dest = FindStaged(modDir, bpStem);
        var bp = Open(dest);
        var fn = bp.Exports.OfType<FunctionExport>().First(f => f.ObjectName.ToString() == "OnActionEffect");
        var stmts = fn.ScriptBytecode!.ToList();

        // Migration: drop the legacy ApplyBuff-LVF Aureole (SecondLife v1).
        // The LVF call executed but never took hold (ApplyBuff-LVF is proven
        // only self-side by AngelsEyes; every working cross-character buff
        // uses SpawnObject/ActivateBuff). v2 re-grants Aureole via the
        // native triplet below; the orphaned out-local stays (benign).
        int lvfIdx = -1, lvfN = 0;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hit = false;
            if (stmts[i] is EX_LocalVirtualFunction lvf && lvf.VirtualFunctionName.ToString() == "ApplyBuff")
                MayhemFixKit.Walk(new[] { stmts[i] }, e =>
                {
                    if (e is EX_ObjectConst oc && oc.Value.IsImport()
                        && oc.Value.ToImport(bp).ObjectName.ToString() == "BP_LU_aureole_C") hit = true;
                });
            if (hit) { lvfIdx = i; lvfN++; }
        }
        if (lvfN > 1) throw new InvalidDataException($"SoulTransplant: legacy Aureole LVF x{lvfN} (expect <=1)");
        if (lvfN == 1)
        {
            uint lvfOff = 0;
            for (int i = 0; i < lvfIdx; i++) lvfOff += stmts[i].GetSize(bp);
            uint lvfSize = stmts[lvfIdx].GetSize(bp);
            stmts.RemoveAt(lvfIdx);
            fn.ScriptBytecode = stmts.ToArray();
            ShiftSigned(bp, fn, lvfOff, -(int)lvfSize);
            MayhemFixKit.RecalcContextSkips(bp, fn);
            Console.WriteLine($"[ScielCustom] {tag}: legacy Aureole LVF removed");
        }

        // Snapshot slot for a native char.AC read (populated pre-strip).
        EX_Context? statsReadTpl = null;

        bool HasCall(KismetExpression s, string fnName)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_CallMath cm && cm.StackNode.IsImport()
                    && cm.StackNode.ToImport(bp).ObjectName.ToString() == fnName) hit = true;
                if (e is EX_FinalFunction ff && !(ff is EX_CallMath) && ff.StackNode is { } sn && sn.IsImport()
                    && sn.ToImport(bp).ObjectName.ToString() == fnName) hit = true;
                if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == fnName) hit = true;
            });
            return hit;
        }

        // Canonical import constructor (TripleHex-proven): rebuild records
        // from the source's STRINGS instead of copying package records.
        // PACKAGE-OUTERED imports only (buff classes etc.). Class-outered
        // FUNCTION imports (KismetMathLibrary, GameplayStatics) MUST be
        // sibling-cloned (EnsureSiblingFn / EnsureKismetMathFn): flattening
        // the outer to the package builds an unresolvable record and the
        // game CTDs the moment the call executes (v2 NOT-gate, v3/v4
        // multiply + curse-spawn).
        UAssetAPI.UnrealTypes.FPackageIndex EnsureLike(UAsset from, UAsset to, UAssetAPI.UnrealTypes.FPackageIndex idx, string role)
        {
            var imp = idx.ToImport(from);
            string obj = imp.ObjectName.ToString(), cls = imp.ClassName.ToString();
            string outerPkg = "/Script/CoreUObject", outerObj = "null";
            if (imp.OuterIndex is { } oo && oo.IsImport())
            {
                var oimp = oo.ToImport(from);
                outerObj = oimp.ObjectName.ToString();
                outerPkg = oimp.ClassPackage.ToString() == "Package" ? outerObj
                    : oimp.ClassPackage.ToString();
            }
            Console.WriteLine($"[ScielCustom] ST EnsureLike {role}: obj={obj} cls={cls} outer={outerObj} classPkg={imp.ClassPackage}");
            return LuneSkillsKit.EnsureObjectImport(to, outerPkg, obj, cls, imp.ClassPackage.ToString());
        }

        // Sibling import cloner for class-outered FUNCTION imports: copies a
        // same-outer record already present in the destination (full chain
        // intact) under the new function name. Returns an existing match.
        UAssetAPI.UnrealTypes.FPackageIndex EnsureSiblingFn(UAsset from,
            UAssetAPI.UnrealTypes.FPackageIndex fromIdx, string fnName, string role)
        {
            var fromImp = fromIdx.ToImport(from);
            var fromOuter = fromImp.OuterIndex is { } oo && oo.IsImport() ? oo.ToImport(from) : null;
            if (fromOuter is null) throw new InvalidDataException($"SoulTransplant: {role} source outer missing");
            string wantOuter = fromOuter.ObjectName.ToString();
            for (int i = 0; i < bp.Imports.Count; i++)
            {
                var imp = bp.Imports[i];
                if (imp.ObjectName.ToString() == fnName
                    && imp.OuterIndex is { } oi && oi.IsImport()
                    && oi.ToImport(bp).ObjectName.ToString() == wantOuter)
                    return UAssetAPI.UnrealTypes.FPackageIndex.FromImport(i);
            }
            for (int i = 0; i < bp.Imports.Count; i++)
            {
                var sib = bp.Imports[i];
                if (sib.OuterIndex is { } oi && oi.IsImport()
                    && oi.ToImport(bp).ObjectName.ToString() == wantOuter)
                {
                    var created = bp.AddImport(new Import(
                        sib.ClassPackage.ToString(),
                        sib.ClassName.ToString(),
                        sib.OuterIndex,
                        fnName,
                        false,
                        bp));
                    Console.WriteLine($"[ScielCustom] ST sibling {role}: {fnName} under {wantOuter}");
                    return created;
                }
            }
            throw new InvalidDataException($"SoulTransplant: no {wantOuter} sibling for {role}");
        }

        KismetExpression StatsFor(KismetPropertyPointer charPtr)
        {
            // Pure `char.AC` read: an EX_Context whose object is a bare
            // character local, carrying the AC member, containing NO call.
            EX_Context? tpl = null;
            foreach (var s in stmts)
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (tpl is not null) return;
                    if (e is EX_Context cx && cx.ObjectExpression is EX_LocalVariable && IsPureStatsRead(cx))
                        tpl = cx;
                });
            // Post-strip fallback: the gift strip deletes the last native
            // template, so a pre-strip snapshot (statsReadTpl) stands in.
            EX_Context src = tpl ?? statsReadTpl
                ?? throw new InvalidDataException("SoulTransplant: statsCtx template not found");
            var c = (EX_Context)MayhemFixKit.CloneKismet(src);
            if (c.ObjectExpression is EX_LocalVariable olv) olv.Variable = charPtr;
            else throw new InvalidDataException("SoulTransplant: stats element not found");
            return c;
        }
        bool IsPureStatsRead(EX_Context cx)
        {
            bool ac = false, call = false;
            MayhemFixKit.Walk(new[] { cx }, x =>
            {
                if (x is EX_InstanceVariable iv && PtrName(iv.Variable) == "AC_jRPG_CharacterStats") ac = true;
                if (x is EX_LocalVirtualFunction || x is EX_FinalFunction) call = true;
            });
            return ac && !call;
        }
        KismetExpression CasterStatsRead()
        {
            // CurrentCharacter.AC read, cloned from the queue-cleanup arg
            // (same asset: no import re-homing needed).
            EX_Context? tpl = null;
            foreach (var s in stmts)
            {
                bool isCleanup = false;
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString().Contains("TryRemoveQueued")) isCleanup = true;
                });
                if (!isCleanup) continue;
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (tpl is not null) return;
                    if (e is EX_Context cx && cx.ObjectExpression is EX_InstanceVariable) tpl = cx;
                });
            }
            if (tpl is null) throw new InvalidDataException("SoulTransplant: caster stats template not found");
            return (KismetExpression)MayhemFixKit.CloneKismet(tpl);
        }
        KismetExpression MakeCurseSpawn(KismetPropertyPointer spawnT,
            string buffCls = "BP_BattleBuff_Cursed_C",
            string buffPkg = "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Cursed")
        {
            var doomSrc = FindAsset("BP_Battle_SkillScript_Sciel_Doom");
            var doom = Open(doomSrc);
            KismetExpression? tpl = null;
            foreach (var s in doom.Exports.OfType<FunctionExport>()
                .First(f => f.ObjectName.ToString() == "OnActionEffect").ScriptBytecode!)
            {
                bool spawn = false;
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (e is EX_ObjectConst oc && oc.Value.IsImport()
                        && oc.Value.ToImport(doom).ObjectName.ToString() == "BP_BattleBuff_Slow_C") spawn = true;
                });
                if (s is EX_LetObj && spawn) { tpl = s; break; }
            }
            if (tpl is null) throw new InvalidDataException("SoulTransplant: Doom spawn template not found");
            var c = (KismetExpression)MayhemFixKit.CloneKismet(tpl);
            UAssetAPI.UnrealTypes.FPackageIndex? spawnSn = null;
            string? spawnFn = null;
            MayhemFixKit.Walk(new[] { c }, e =>
            {
                if (e is EX_CallMath cm && cm.StackNode.IsImport())
                { spawnSn = cm.StackNode; spawnFn = cm.StackNode.ToImport(doom).ObjectName.ToString(); }
                else if (e is EX_FinalFunction ff && ff.StackNode is { } sn && sn.IsImport())
                { spawnSn = sn; spawnFn = sn.ToImport(doom).ObjectName.ToString(); }
            });
            if (spawnSn is null || spawnFn is null) throw new InvalidDataException("SoulTransplant: spawn call missing");
            var spawnImport = EnsureSiblingFn(doom, spawnSn, spawnFn, "spawn");
            MayhemFixKit.Walk(new[] { c }, e =>
            {
                if (e is EX_CallMath cm && cm.StackNode.IsImport())
                    cm.StackNode = spawnImport;
                else if (e is EX_FinalFunction ff && ff.StackNode is { } sn && sn.IsImport())
                    ff.StackNode = spawnImport;
                if (e is EX_ObjectConst oc && oc.Value.IsImport())
                    oc.Value = LuneSkillsKit.EnsureObjectImport(bp,
                        buffPkg, buffCls, buffCls, "/Script/Engine");
                if (e is EX_LocalVariable lv) lv.Variable = spawnT;
            });
            return c;
        }
        KismetExpression MakeCurseApply(KismetPropertyPointer itemT, KismetPropertyPointer casterT, KismetPropertyPointer spawnT, KismetPropertyPointer boolT)
        {
            var doomSrc = FindAsset("BP_Battle_SkillScript_Sciel_Doom");
            var doom = Open(doomSrc);
            KismetExpression? tpl = null;
            foreach (var s in doom.Exports.OfType<FunctionExport>()
                .First(f => f.ObjectName.ToString() == "OnActionEffect").ScriptBytecode!)
            {
                bool act = false;
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "ActivateBuff") act = true;
                });
                if (s is EX_Context && act) { tpl = s; break; }
            }
            if (tpl is null) throw new InvalidDataException("SoulTransplant: Doom apply template not found");
            var c = (KismetExpression)MayhemFixKit.CloneKismet(tpl);
            MayhemFixKit.Walk(new[] { c }, e =>
            {
                if (e is EX_LocalVirtualFunction vf)
                    vf.VirtualFunctionName = FName.FromString(bp, vf.VirtualFunctionName.ToString());
                if (e is EX_ObjectConst oc && oc.Value.IsImport())
                    oc.Value = EnsureLike(doom, bp, oc.Value, "apply-objconst");
            });
            // Receiver + explicit buff target onto the revived ally.
            var inners = new List<EX_Context>();
            MayhemFixKit.Walk(new[] { c }, e =>
            {
                if (e is EX_Context cx && cx.ObjectExpression is EX_LocalVariable && IsPureStatsRead(cx))
                    inners.Add(cx);
            });
            if (inners.Count == 0) throw new InvalidDataException("SoulTransplant: apply stats target missing");
            foreach (var inner in inners)
                if (!ReplaceNode(c, inner, StatsFor(itemT)))
                    throw new InvalidDataException("SoulTransplant: apply stats replace failed");
            // Receiver: Doom's TargetStats temp is a Doom-map pointer, which
            // resolves to garbage in our name map (staged as LV "name").
            // Cross-asset cloned pointers are NEVER valid here: rebuild the
            // read natively on the revived ally.
            if (c is EX_Context rootc && rootc.ObjectExpression is EX_LocalVariable)
                rootc.ObjectExpression = StatsFor(itemT);
            else throw new InvalidDataException("SoulTransplant: apply receiver shape drifted");
            // Doom's 3rd ActivateBuff arg is the CASTER's stats (buff
            // credit): Sciel, Doom-faithful.
            bool nestDone = false;
            MayhemFixKit.Walk(new[] { c }, e =>
            {
                if (!nestDone && e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "ActivateBuff")
                {
                    if (vf.Parameters.Length != 4)
                        throw new InvalidDataException($"SoulTransplant: ActivateBuff arity x{vf.Parameters.Length} (expect 4)");
                    if (vf.Parameters[2] is not EX_Context)
                        throw new InvalidDataException("SoulTransplant: ActivateBuff arg2 not a stats read");
                    vf.Parameters[2] = StatsFor(casterT);
                    nestDone = true;
                }
            });
            if (!nestDone) throw new InvalidDataException("SoulTransplant: nested stats replace failed");
            bool instDone = false, boolDone = false;
            MayhemFixKit.Walk(new[] { c }, e =>
            {
                if (e is EX_LocalVariable lv && PtrName(lv.Variable).Contains("SpawnObject")) { lv.Variable = spawnT; instDone = true; }
            });
            MayhemFixKit.Walk(new[] { c }, e =>
            {
                if (!boolDone && e is EX_LocalVariable lv && PtrName(lv.Variable).Contains("Success")) { lv.Variable = boolT; boolDone = true; }
            });
            if (!instDone || !boolDone)
                throw new InvalidDataException("SoulTransplant: apply remap incomplete");
            return c;
        }
        double ReadBalFloat(string dir, string row)
        {
            var balPath = FileCopy.FindUnder(dir, "DT_Balancing_MainCharacterSkills.uasset")
                ?? throw new FileNotFoundException("SoulTransplant: staged bal table missing");
            var bal = new DataTableEditor(balPath);
            if (!bal.TryGetRow(row, out var r)) throw new InvalidDataException("SoulTransplant: bal row missing: " + row);
            foreach (var leaf in bal.FlattenRow(r))
                if (leaf.Path.EndsWith("FloatValue", StringComparison.Ordinal))
                    return double.Parse(leaf.Value.ToString(), System.Globalization.CultureInfo.InvariantCulture);
            throw new InvalidDataException("SoulTransplant: FloatValue missing in " + row);
        }

        bool hasRes = false, hasAureole = false;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "Resurrect") hasRes = true;
                if (e is EX_ObjectConst oc && oc.Value.IsImport()
                    && oc.Value.ToImport(bp).ObjectName.ToString() == "BP_LU_aureole_C") hasAureole = true;
            });
        if (addRevive && hasRes) { Console.WriteLine($"[ScielCustom] {tag} revive already present"); return 0; }
        if (!addRevive && addAureole && hasAureole) { Console.WriteLine($"[ScielCustom] {tag} Aureole already present"); return 0; }

        // Snapshot a native char.AC read BEFORE the gift strip deletes the
        // last one (#12's receiver): StatsFor falls back to this clone.
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (statsReadTpl is not null) return;
                if (e is EX_Context cx && cx.ObjectExpression is EX_LocalVariable && IsPureStatsRead(cx))
                    statsReadTpl = cx;
            });
        if (statsReadTpl is null) throw new InvalidDataException("SoulTransplant: statsCtx snapshot missing");

        // ---- harvest: native slots (fail-loud) ----
        // The s-block runs inside the loop after the target fetch and reuses
        // only locals the loop never touches again.
        var itemT = STPtr(stmts, "CallFunc_Array_Get_Item", "item");
        var gateT = STPtr(stmts, "CallFunc_Less_IntInt_ReturnValue", "gate");
        var casterT = STPtr(stmts, "CallFunc_GetCurrentCharacter_CurrentCharacter", "caster");
        var spawnT = STPtr(stmts, "CallFunc_SpawnSystemAtLocation_ReturnValue_1", "spawn");
        KismetPropertyPointer? emptyRv = null;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_Context cx && cx.RValuePointer is { } rv && PtrName(rv) == "?") emptyRv ??= rv;
            });
        if (emptyRv is null) throw new InvalidDataException("SoulTransplant: empty RValue template not found");
        var distinct = new object?[] { (object?)itemT, gateT, casterT, spawnT }
            .Where(o => o is not null).Distinct().Count();
        if (distinct != 4) throw new InvalidDataException("SoulTransplant: temp slots alias each other");

        // Flat revive HP, baked from the staged row at build time. A live
        // % of MaxHP is NOT expressible here: the only MaxHP accessor is
        // the void GetMaxHP (out-param), and a nested void call yields no
        // usable value as an argument (v3/v4 revived at 0 HP). The donor
        // owns no double temp to catch the out-param, and adding function
        // locals is out of scope. (0 HP itself works fine, by the way:
        // future meat-shield move material - revive at 0 that soaks a hit.)
        double reviveHP = addRevive ? ReadBalFloat(modDir, reviveRow) : 0.0;
        if (addRevive)
        {
            if (reviveHP <= 0.0) throw new InvalidDataException($"{tag}: revive HP {reviveHP} must be positive");
            Console.WriteLine($"[ScielCustom] {tag} revive HP baked: {reviveHP}");
        }

        // ---- strip the AP gift (Force, AP row read, FTrunc, GainAP) ----
        // The loop's target fetch (GetTargetedCharacters + Array_Get) stays:
        // the revive/curse block anchors on the first Array_Get below.
        bool HasForceExact(KismetExpression s)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "Force Play Again") hit = true;
            });
            return hit;
        }
        var delTargets = new List<(string what, KismetExpression node)>();
        void CollectDel(string what, Func<KismetExpression, bool> pred)
        {
            KismetExpression? hit = null;
            int n = 0;
            foreach (var s in stmts) if (pred(s)) { hit = s; n++; }
            if (hit is null || n != 1) throw new InvalidDataException($"SoulTransplant: delete target {what} x{n} (expect 1)");
            delTargets.Add((what, hit));
        }
        CollectDel("Force", s => s is EX_Context && HasForceExact(s));
        CollectDel("APRow", s => s is EX_Context && HasCall(s, "GetDataTableRowFromName"));
        CollectDel("FTrunc", s => s is EX_Let && HasCall(s, "FTrunc"));
        CollectDel("GainAP", s => s is EX_Context && HasCall(s, "GainAP"));
        // Insert position: where GainAP was (minus earlier deletions).
        var preIdx = delTargets.ToDictionary(d => d.what, d => stmts.IndexOf(d.node));
        int insertIdx = preIdx["GainAP"] - preIdx.Values.Count(v => v < preIdx["GainAP"]);
        foreach (var idx in delTargets.Select(d => stmts.IndexOf(d.node)).OrderByDescending(i => i))
        {
            uint delOff = 0;
            for (int i = 0; i < idx; i++) delOff += stmts[i].GetSize(bp);
            uint delSize = stmts[idx].GetSize(bp);
            stmts.RemoveAt(idx);
            fn.ScriptBytecode = stmts.ToArray();
            ShiftSigned(bp, fn, delOff, -(int)delSize);
        }
        MayhemFixKit.RecalcContextSkips(bp, fn);
        Console.WriteLine($"[ScielCustom] {tag} AP gift stripped (x4 stmts)");

        // ---- s-block: revive (optional) + curse (insert after the first Array_Get) ----
        stmts = fn.ScriptBytecode!.ToList();
        var casterStats = CasterStatsRead();
        var sblock = new List<KismetExpression>();
        var s0pre = (KismetExpression)MayhemFixKit.CloneKismet(casterStats);
        if (addRevive)
        {
            if (reviveHP <= 0.0) throw new InvalidDataException($"{tag}: revive HP {reviveHP} must be positive");
            sblock.Add(new EX_Context
            {
                ObjectExpression = StatsFor(itemT),
                Offset = 54,
                RValuePointer = emptyRv,
                ContextExpression = new EX_LocalVirtualFunction
                {
                    VirtualFunctionName = FName.FromString(bp, "Resurrect"),
                    Parameters = new KismetExpression[]
                    {
                        new EX_DoubleConst { Value = reviveHP },
                        s0pre,
                    },
                },
            });
        }
        var s2 = MakeCurseSpawn(spawnT);
        var cfSrc = FindStaged(modDir, "BP_Battle_SkillScript_Sciel_CursedFrenzy");
        var cf = Open(cfSrc);
        KismetExpression? turnsTpl = null;
        foreach (var s in cf.Exports.OfType<FunctionExport>()
            .First(f => f.ObjectName.ToString() == "OnActionEffect").ScriptBytecode!)
            if (s is EX_CallMath tcm && tcm.StackNode.IsImport()
                && tcm.StackNode.ToImport(cf).ObjectName.ToString() == "SetIntPropertyByName") turnsTpl = s;
        if (turnsTpl is null) throw new InvalidDataException("SoulTransplant: CursedFrenzy TurnDuration template not found");
        KismetExpression MakeDuration(int turns)
        {
            var d = (KismetExpression)MayhemFixKit.CloneKismet(turnsTpl);
            if (d is EX_CallMath xcm && xcm.StackNode.IsImport())
                xcm.StackNode = EnsureSiblingFn(cf, xcm.StackNode, "SetIntPropertyByName", "turns");
            else throw new InvalidDataException("SoulTransplant: TurnDuration call shape drifted");
            int dInts = 0, dNames = 0, dLVs = 0;
            MayhemFixKit.Walk(new[] { d }, e =>
            {
                if (e is EX_LocalVariable xlv) { xlv.Variable = spawnT; dLVs++; }
                if (e is EX_IntConst xic) { xic.Value = turns; dInts++; }
                if (e is EX_NameConst xnc)
                {
                    if (xnc.Value.ToString() != "TurnDuration") throw new InvalidDataException("SoulTransplant: TurnDuration name drifted");
                    xnc.Value = FName.FromString(bp, "TurnDuration");
                    dNames++;
                }
            });
            if (dInts != 1 || dNames != 1 || dLVs != 1) throw new InvalidDataException($"SoulTransplant: TurnDuration remap ints x{dInts} names x{dNames} lvs x{dLVs}");
            return d;
        }
        var sX = MakeDuration(curseTurns);
        var s3 = MakeCurseApply(itemT, casterT, spawnT, gateT);

        // The apply credits Sciel via casterT, but the donor only populates
        // that local post-loop: fetch it here (verbatim clone, zero surgery)
        // or the instigator arg is null and the buff silently fails.
        KismetExpression? casterFetchTpl = null;
        int casterFetchN = 0;
        foreach (var s in stmts)
            if (s is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "GetCurrentCharacter") { casterFetchTpl = s; casterFetchN++; }
        if (casterFetchTpl is null || casterFetchN != 1) throw new InvalidDataException($"SoulTransplant: caster fetch template x{casterFetchN} (expect 1)");
        var s0 = (KismetExpression)MayhemFixKit.CloneKismet(casterFetchTpl);

        sblock.Insert(0, s0);
        sblock.Add(s2);
        sblock.Add(sX);
        sblock.Add(s3);
        if (addAureole)
        {
            // Native-chain Aureole (Doom-spawn shape with the aureole class,
            // ~battle-long duration, generic ally apply): the LVF transplant
            // executed but never took hold cross-character.
            sblock.Add(MakeCurseSpawn(spawnT, "BP_LU_aureole_C", "/Game/Gameplay/Buffs/UniqueBuffs/BP_LU_aureole"));
            sblock.Add(MakeDuration(99));
            sblock.Add(MakeCurseApply(itemT, casterT, spawnT, gateT));
        }
        if (insertIdx < 0 || insertIdx > stmts.Count) throw new InvalidDataException($"SoulTransplant: insert index {insertIdx} out of range");
        uint insOff = 0;
        for (int i = 0; i < insertIdx; i++) insOff += stmts[i].GetSize(bp);
        uint ssize = 0;
        foreach (var s in sblock) ssize += s.GetSize(bp);
        stmts.InsertRange(insertIdx, sblock);
        fn.ScriptBytecode = stmts.ToArray();
        ShiftSigned(bp, fn, insOff, (int)ssize);
        MayhemFixKit.RecalcContextSkips(bp, fn);


        bp.Write(dest);
        Console.WriteLine($"[ScielCustom] {tag}: Intervention donor + curse splice staged (revive {(addRevive ? "on" : "off")}, Aureole {(addAureole ? "on" : "off")})");
        return 1;
    }

    // ── 2h. Soul Transplant DA targeting: Luna's revive targeting ────────────
    // Transplant the dead-ally TargetingType enum (NewEnumerator12) from
    // Lune's Rebirth DA (same technique as CursedFrenzy's 2e).

    private static int PatchSoulTransplantTargeting(string modDir, string daStem)
    {
        var donor = Open(FindAsset("DA_Skill_Lune_Rebirth"));
        EnumPropertyData? srcProp = null;
        foreach (var ex in donor.Exports.OfType<NormalExport>())
            foreach (var p in ex.Data)
                if (p is EnumPropertyData e && e.Name.ToString() == "TargetingType") srcProp = e;
        if (srcProp is null) throw new InvalidDataException("Rebirth TargetingType not found");
        var daDest = FindStaged(modDir, daStem);
        var da = Open(daDest);
        bool done = false;
        foreach (var ex in da.Exports.OfType<NormalExport>())
            for (int i = 0; i < ex.Data.Count; i++)
                if (ex.Data[i] is EnumPropertyData)
                {
                    var d = (EnumPropertyData)ex.Data[i];
                    if (d.Name.ToString() != "TargetingType") continue;
                    ex.Data[i] = new EnumPropertyData
                    {
                        Name = FName.FromString(da, "TargetingType"),
                        EnumType = FName.FromString(da, srcProp.EnumType.ToString()),
                        Value = FName.FromString(da, srcProp.Value.ToString()),
                    };
                    done = true;
                }
        if (!done) throw new InvalidDataException("SoulTransplant TargetingType not found");
        da.Write(daDest);
        Console.WriteLine($"[ScielCustom] {daStem} DA targeting -> dead allies (Rebirth)");
        return 1;
    }

    // ── 2e. Corruption: regular skill, not gradient ─────────────────────────
    // The EndSlice donor is a gradient attack (ESkillType NewEnumerator2,
    // like Doom and Shadow Bringer; regulars are NewEnumerator1). Inheriting
    // the type puts Corruption in the gradient slot, auto-available from the
    // start with charge costs. Flip it to a regular unlockable skill.

    private static int PatchCorruptionSkillType(string modDir)
    {
        var dest = FindStaged(modDir, "DA_Skill_Sciel_Corruption");
        var da = Open(dest);
        bool done = false;
        foreach (var ex in da.Exports.OfType<NormalExport>())
            foreach (var p in ex.Data)
                if (p is EnumPropertyData e && e.Name.ToString() == "SkillType")
                {
                    if (e.Value.ToString() != "NewEnumerator2")
                        throw new InvalidDataException($"Corruption: SkillType {e.Value} (expect NewEnumerator2)");
                    e.Value = FName.FromString(da, "NewEnumerator1");
                    done = true;
                }
        if (!done) throw new InvalidDataException("Corruption: SkillType not found");
        da.Write(dest);
        Console.WriteLine("[ScielCustom] Corruption SkillType -> regular (NewEnumerator1)");
        return 1;
    }

    // ── 2f. Corruption: restore the vanilla foretell-counter buff ref ────────
    // EndSlice scales off battle-long foretell consumption ("Damage is
    // increased for each Foretell consumed since the beginning of the
    // battle"): ApplyBuffOnBattleStart installs BP_Buff_EndSlice-
    // ForetellConsumedCount and the generic sync feeds ForetellConsumedAmount.
    // CloneMoves' blind ("EndSlice"->"Corruption") substring retarget rewrote
    // the shared buff into ghost BP_Buff_CorruptionForetellConsumedCount
    // (dangling BuffClass -> no counter -> multiplier stuck at x1). Point the
    // 3 name entries (package, _C class, Default__) back at vanilla.

    private static int PatchCorruptionBuffRef(string modDir)
    {
        const string ghost = "CorruptionForetellConsumedCount";
        const string real = "EndSliceForetellConsumedCount";
        int total = 0;
        foreach (var stem in new[] { "DA_Skill_Sciel_Corruption", "BP_Battle_SkillScript_Sciel_Corruption" })
        {
            var dest = FindStaged(modDir, stem);
            var asset = Open(dest);
            var names = asset.GetNameMapIndexList();
            int n = 0;
            for (int i = 0; i < names.Count; i++)
            {
                var s = names[i]?.Value;
                if (string.IsNullOrEmpty(s) || !s.Contains(ghost, StringComparison.Ordinal)) continue;
                asset.SetNameReference(i, new FString(s.Replace(ghost, real, StringComparison.Ordinal)));
                n++;
            }
            if (n == 0) { Console.WriteLine($"[ScielCustom] {stem} buff ref already vanilla"); continue; }
            if (n != 3) throw new InvalidDataException($"{stem}: buff ghost refs x{n} (expect 3)");
            asset.Write(dest);
            total++;
        }
        if (total == 0) return 0;
        Console.WriteLine("[ScielCustom] Corruption BuffClass -> vanilla EndSlice counter buff (DA+BP)");
        return total;
    }

    // ── 2d. Black Death: self-only Our Sacrifice ─────────────────────────────
    // Our Sacrifice absorbs every ALLY except the caster (#15 skips self via
    // NotEqual) and drains each to 1 HP (SetHP ... DoubleConst 1). Black Death
    // renames the function to Equal (skip everyone EXCEPT Sciel): the import
    // record stays 100% native (rename, never AddImport — created function
    // records are crash suspects). Everything else follows — self at full HP
    // yields fraction ~1 × StolenHPMultiplier 1.5, SetHP 1 lands on Sciel,
    // per-foretell/base rows already set at clone time.

    private static int PatchBlackDeath(string modDir, string bpStem)
    {
        // Black Death: self-only Our Sacrifice via a NOT-gate. Our Sacrifice
        // absorbs every ALLY except the caster (#15 NotEqual -> skip self) and
        // drains each to 1 HP (SetHP ... DoubleConst 1). Calling a *renamed*
        // equality function crashed in game despite verifying clean, so the
        // inversion is a native Not_PreBool applied to #15's own result temp:
        // temp = NOT temp. Zero new imports, zero renames, one inserted
        // statement (signed-shifted). Everything else follows — self at full
        // HP yields fraction ~1 x StolenHPMultiplier 1.5, SetHP 1 lands on
        // Sciel, per-foretell/base rows already set at clone time.
        // (Ultima's Bargain reuses this verbatim on its own OurSacrifice
        // clone, then drops the drain to 0 HP for the kill.)
        var dest = FindStaged(modDir, bpStem);
        var bp = Open(dest);
        var fn = bp.Exports.OfType<FunctionExport>().First(f => f.ObjectName.ToString() == "OnActionEffect");
        var stmts = fn.ScriptBytecode!.ToList();

        // Template: native #4 (LetBool dest=NotTemp, CallMath(Not_PreBool, arg)).
        int tplIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool isLetBool = stmts[i] is EX_LetBool, hasNot = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_CallMath cm && cm.StackNode.IsImport()
                    && cm.StackNode.ToImport(bp).ObjectName.ToString() == "Not_PreBool") hasNot = true;
            });
            if (isLetBool && hasNot) { tplIdx = i; break; }
        }
        if (tplIdx < 0) throw new InvalidDataException("BlackDeath Not_PreBool template not found");
        // Target: #15 (LetBool dest=filterTemp, CallMath(NotEqual_ObjectObject)).
        int tgtIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool isLetBool = stmts[i] is EX_LetBool, hasNe = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_CallMath cm && cm.StackNode.IsImport()
                    && cm.StackNode.ToImport(bp).ObjectName.ToString() == "NotEqual_ObjectObject") hasNe = true;
            });
            if (isLetBool && hasNe) { tgtIdx = i; break; }
        }
        if (tgtIdx < 0) throw new InvalidDataException("BlackDeath filter statement not found");
        // The LetBool destination is the FIRST local child of the statement.
        KismetPropertyPointer? filterTemp = null;
        foreach (var lv in LocalsOf(stmts[tgtIdx]))
        { filterTemp = lv.Variable; break; }
        if (filterTemp is null) throw new InvalidDataException("BlackDeath filter temp not found");

        var gate = (EX_LetBool)MayhemFixKit.CloneKismet(stmts[tplIdx]);
        var gateLocals = LocalsOf(gate);
        if (gateLocals.Count != 2) throw new InvalidDataException($"BlackDeath template locals x{gateLocals.Count} (expect 2)");
        foreach (var lv in gateLocals) lv.Variable = filterTemp;
        uint gOff = 0;
        for (int i = 0; i <= tgtIdx; i++) gOff += stmts[i].GetSize(bp);
        uint gOld = 0;
        var gNew = gate.GetSize(bp);
        stmts.Insert(tgtIdx + 1, gate);
        fn.ScriptBytecode = stmts.ToArray();
        ShiftSigned(bp, fn, gOff, (int)gNew - (int)gOld);
        MayhemFixKit.RecalcContextSkips(bp, fn);
        bp.Write(dest);
        Console.WriteLine($"[ScielCustom] {bpStem}: filter inverted via NOT-gate (self only)");
        return 1;
    }

    // ── 2d0. Last Chance: ally singles + shared curse ────────────────────────
    // One strong turn for the targeted ally (immediate turn + 5 AP native):
    // Powerful / Rush / Berserk (1 turn each; neither Powerful nor Rush
    // stacks, so singles) plus Cursed for 2 turns on the same ally
    // (1-turn Cursed is an insta-kill; 2 turns ticks normally).
    // 4 native triplets, zero flow ops, two borrowed scratch locals.
    // v1's self-nuke and v2's 16-triplet stack block are migrated out.

    private static int PatchLastChance(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_Sciel_LastChance";
        var dest = FindStaged(modDir, bpStem);
        var bp = Open(dest);
        var fn = bp.Exports.OfType<FunctionExport>().First(f => f.ObjectName.ToString() == "OnActionEffect");
        var stmts = fn.ScriptBytecode!.ToList();

        int mdbN = 0, dealN = 0, spawnN = 0;
        foreach (var s in stmts) MayhemFixKit.Walk(new[] { s }, e =>
        {
            if (e is EX_CallMath cm && cm.StackNode.IsImport()
                && cm.StackNode.ToImport(bp).ObjectName.ToString() == "SpawnObject") spawnN++;
            if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "MakeDamageBuilder") mdbN++;
            if (e is EX_LocalVirtualFunction vf2 && vf2.VirtualFunctionName.ToString() == "DealDamages") dealN++;
        });
        if (mdbN > 1 || dealN > 1) throw new InvalidDataException($"LastChance: legacy nuke x{mdbN}/{dealN} (expect <=1 each)");
        bool legacyNuke = mdbN == 1 && dealN == 1;
        if (legacyNuke && spawnN != 3) throw new InvalidDataException($"LastChance: legacy spawns x{spawnN} (expect 3)");
        // v1 (11-stmt nuke block) and v2 (48-stmt stack block) both migrate;
        // v3 (4 triplets) no-ops. Fresh donors have no spawns at all.
        bool legacy = legacyNuke || spawnN == 16;
        if (!legacy && spawnN == 4) { Console.WriteLine("[ScielCustom] LastChance buffs already present"); return 0; }
        if (!legacy && spawnN != 0) throw new InvalidDataException($"LastChance: unexpected spawns x{spawnN} (expect 0, 4 or 16)");

        bool HasCall(KismetExpression s, string fnName)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_CallMath cm && cm.StackNode.IsImport()
                    && cm.StackNode.ToImport(bp).ObjectName.ToString() == fnName) hit = true;
                if (e is EX_FinalFunction ff && !(ff is EX_CallMath) && ff.StackNode is { } sn && sn.IsImport()
                    && sn.ToImport(bp).ObjectName.ToString() == fnName) hit = true;
                if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == fnName) hit = true;
            });
            return hit;
        }
        UAssetAPI.UnrealTypes.FPackageIndex EnsureLike(UAsset from, UAsset to, UAssetAPI.UnrealTypes.FPackageIndex idx, string role)
        {
            var imp = idx.ToImport(from);
            string obj = imp.ObjectName.ToString(), cls = imp.ClassName.ToString();
            string outerPkg = "/Script/CoreUObject", outerObj = "null";
            if (imp.OuterIndex is { } oo && oo.IsImport())
            {
                var oimp = oo.ToImport(from);
                outerObj = oimp.ObjectName.ToString();
                outerPkg = oimp.ClassPackage.ToString() == "Package" ? outerObj
                    : oimp.ClassPackage.ToString();
            }
            Console.WriteLine($"[ScielCustom] LC EnsureLike {role}: obj={obj} cls={cls} outer={outerObj}");
            return LuneSkillsKit.EnsureObjectImport(to, outerPkg, obj, cls, imp.ClassPackage.ToString());
        }
        UAssetAPI.UnrealTypes.FPackageIndex EnsureSiblingFn(UAsset from,
            UAssetAPI.UnrealTypes.FPackageIndex fromIdx, string fnName, string role)
        {
            var fromImp = fromIdx.ToImport(from);
            var fromOuter = fromImp.OuterIndex is { } oo && oo.IsImport() ? oo.ToImport(from) : null;
            if (fromOuter is null) throw new InvalidDataException($"LastChance: {role} source outer missing");
            string wantOuter = fromOuter.ObjectName.ToString();
            for (int i = 0; i < bp.Imports.Count; i++)
            {
                var imp = bp.Imports[i];
                if (imp.ObjectName.ToString() == fnName
                    && imp.OuterIndex is { } oi && oi.IsImport()
                    && oi.ToImport(bp).ObjectName.ToString() == wantOuter)
                    return UAssetAPI.UnrealTypes.FPackageIndex.FromImport(i);
            }
            for (int i = 0; i < bp.Imports.Count; i++)
            {
                var sib = bp.Imports[i];
                if (sib.OuterIndex is { } oi && oi.IsImport()
                    && oi.ToImport(bp).ObjectName.ToString() == wantOuter)
                {
                    var created = bp.AddImport(new Import(
                        sib.ClassPackage.ToString(),
                        sib.ClassName.ToString(),
                        sib.OuterIndex,
                        fnName,
                        false,
                        bp));
                    Console.WriteLine($"[ScielCustom] LC sibling {role}: {fnName} under {wantOuter}");
                    return created;
                }
            }
            throw new InvalidDataException($"LastChance: no {wantOuter} sibling for {role}");
        }
        KismetExpression StatsFor(KismetPropertyPointer charPtr)
        {
            EX_Context? tpl = null;
            foreach (var s in stmts)
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (tpl is not null) return;
                    if (e is EX_Context cx && cx.ObjectExpression is EX_LocalVariable && IsPureStatsRead(cx))
                        tpl = cx;
                });
            if (tpl is null) throw new InvalidDataException("LastChance: statsCtx template not found");
            var c = (EX_Context)MayhemFixKit.CloneKismet(tpl);
            if (c.ObjectExpression is EX_LocalVariable olv) olv.Variable = charPtr;
            else throw new InvalidDataException("LastChance: stats element not found");
            return c;
        }
        bool IsPureStatsRead(EX_Context cx)
        {
            bool ac = false, call = false;
            MayhemFixKit.Walk(new[] { cx }, x =>
            {
                if (x is EX_InstanceVariable iv && PtrName(iv.Variable) == "AC_jRPG_CharacterStats") ac = true;
                if (x is EX_LocalVirtualFunction || x is EX_FinalFunction) call = true;
            });
            return ac && !call;
        }
        KismetExpression CasterStatsRead()
        {
            EX_Context? tpl = null;
            foreach (var s in stmts)
            {
                bool isCleanup = false;
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString().Contains("TryRemoveQueued")) isCleanup = true;
                });
                if (!isCleanup) continue;
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (tpl is not null) return;
                    if (e is EX_Context cx && cx.ObjectExpression is EX_InstanceVariable) tpl = cx;
                });
            }
            if (tpl is null) throw new InvalidDataException("LastChance: caster stats template not found");
            return (KismetExpression)MayhemFixKit.CloneKismet(tpl);
        }
        int LvCount(string name)
        {
            int n = 0;
            foreach (var s in stmts) MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_LocalVariable lv && PtrName(lv.Variable) == name) n++;
            });
            return n;
        }

        var itemT = STPtr(stmts, "CallFunc_Array_Get_Item", "item");
        var borrowObjA = STPtr(stmts, "CallFunc_GetComponentByClass_ReturnValue", "spawn sink");
        var borrowBool = STPtr(stmts, "CallFunc_IsLastActionEffectOfSkill_ReturnValue", "success sink");
        if (LvCount("CallFunc_GetComponentByClass_ReturnValue") != 2)
            throw new InvalidDataException("LastChance: spawn sink live (expect set+read x2)");
        if (LvCount("CallFunc_IsLastActionEffectOfSkill_ReturnValue") != 2)
            throw new InvalidDataException("LastChance: bool sink live (expect set+read x2)");

        var doom = Open(FindAsset("BP_Battle_SkillScript_Sciel_Doom"));
        var doomStmts = doom.Exports.OfType<FunctionExport>()
            .First(f => f.ObjectName.ToString() == "OnActionEffect").ScriptBytecode!.ToList();
        KismetExpression DoomTpl(Func<KismetExpression, bool> pred, string what, bool takeFirst = false)
        {
            KismetExpression? hit = null;
            int n = 0;
            foreach (var s in doomStmts) if (pred(s)) { hit ??= s; n++; }
            if (hit is null || (!takeFirst && n != 1))
                throw new InvalidDataException($"LastChance: Doom template {what} x{n} (expect {(takeFirst ? "1+" : "1")})");
            return (KismetExpression)MayhemFixKit.CloneKismet(hit);
        }
        var cf = Open(FindStaged(modDir, "BP_Battle_SkillScript_Sciel_CursedFrenzy"));
        var cfStmts = cf.Exports.OfType<FunctionExport>()
            .First(f => f.ObjectName.ToString() == "OnActionEffect").ScriptBytecode!.ToList();
        KismetExpression DurTpl()
        {
            KismetExpression? hit = null;
            int n = 0;
            foreach (var s in cfStmts)
                if (s is EX_CallMath tcm && tcm.StackNode.IsImport()
                    && tcm.StackNode.ToImport(cf).ObjectName.ToString() == "SetIntPropertyByName") { hit = s; n++; }
            if (hit is null || n != 1) throw new InvalidDataException($"LastChance: duration template x{n} (expect 1)");
            var c = (KismetExpression)MayhemFixKit.CloneKismet(hit);
            if (c is EX_CallMath xcm && xcm.StackNode.IsImport())
                xcm.StackNode = EnsureSiblingFn(cf, xcm.StackNode, "SetIntPropertyByName", "duration");
            else throw new InvalidDataException("LastChance: duration call shape drifted");
            return c;
        }

        List<KismetExpression> MakeAllyBuff(string buffCls, string buffPkg, int turns)
        {
            var out3 = new List<KismetExpression>();
            // spawn (Doom Slow-spawn shape, class remapped)
            var sp = DoomTpl(s =>
            {
                if (s is not EX_LetObj) return false;
                bool hit = false;
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (e is EX_ObjectConst oc && oc.Value.IsImport()
                        && oc.Value.ToImport(doom).ObjectName.ToString() == "BP_BattleBuff_Slow_C") hit = true;
                });
                return hit;
            }, "spawn");
            UAssetAPI.UnrealTypes.FPackageIndex? spawnSn = null;
            MayhemFixKit.Walk(new[] { sp }, e =>
            {
                if (e is EX_CallMath cm && cm.StackNode.IsImport()) spawnSn = cm.StackNode;
                else if (e is EX_FinalFunction ff && ff.StackNode is { } sn && sn.IsImport()) spawnSn = sn;
            });
            if (spawnSn is null) throw new InvalidDataException("LastChance: spawn call missing");
            string spawnFn = spawnSn.ToImport(doom).ObjectName.ToString();
            var spawnImport = EnsureSiblingFn(doom, spawnSn, spawnFn, "spawn");
            MayhemFixKit.Walk(new[] { sp }, e =>
            {
                if (e is EX_CallMath cm && cm.StackNode.IsImport()) cm.StackNode = spawnImport;
                else if (e is EX_FinalFunction ff && ff.StackNode is { } sn && sn.IsImport()) ff.StackNode = spawnImport;
                if (e is EX_ObjectConst oc && oc.Value.IsImport())
                    oc.Value = LuneSkillsKit.EnsureObjectImport(bp, buffPkg, buffCls, buffCls, "/Script/Engine");
                if (e is EX_LocalVariable lv) lv.Variable = borrowObjA;
            });
            out3.Add(sp);
            // duration
            var du = DurTpl();
            int dInts = 0, dNames = 0, dLVs = 0;
            MayhemFixKit.Walk(new[] { du }, e =>
            {
                if (e is EX_LocalVariable xlv) { xlv.Variable = borrowObjA; dLVs++; }
                if (e is EX_IntConst xic) { xic.Value = turns; dInts++; }
                if (e is EX_NameConst xnc)
                {
                    if (xnc.Value.ToString() != "TurnDuration") throw new InvalidDataException("LastChance: TurnDuration name drifted");
                    xnc.Value = FName.FromString(bp, "TurnDuration");
                    dNames++;
                }
            });
            if (dInts != 1 || dNames != 1 || dLVs != 1)
                throw new InvalidDataException($"LastChance: duration remap ints x{dInts} names x{dNames} lvs x{dLVs}");
            out3.Add(du);
            // apply (Doom ActivateBuff shape — Doom owns 3 identical-shape
            // pairs; first is fine, receiver/instigator/spawn/success all
            // remapped below. Ally receiver, Sciel credit)
            var ap = DoomTpl(s => s is EX_Context && HasCall(s, "ActivateBuff"), "apply", takeFirst: true);
            MayhemFixKit.Walk(new[] { ap }, e =>
            {
                if (e is EX_LocalVirtualFunction vf)
                    vf.VirtualFunctionName = FName.FromString(bp, vf.VirtualFunctionName.ToString());
                if (e is EX_ObjectConst oc && oc.Value.IsImport())
                    oc.Value = EnsureLike(doom, bp, oc.Value, "apply-objconst");
            });
            var inners = new List<EX_Context>();
            MayhemFixKit.Walk(new[] { ap }, e =>
            {
                if (e is EX_Context cx && cx.ObjectExpression is EX_LocalVariable && IsPureStatsRead(cx))
                    inners.Add(cx);
            });
            if (inners.Count == 0) throw new InvalidDataException("LastChance: apply stats target missing");
            foreach (var inner in inners)
                if (!ReplaceNode(ap, inner, StatsFor(itemT)))
                    throw new InvalidDataException("LastChance: apply stats replace failed");
            if (ap is EX_Context rootc && rootc.ObjectExpression is EX_LocalVariable)
                rootc.ObjectExpression = StatsFor(itemT);
            else throw new InvalidDataException("LastChance: apply receiver shape drifted");
            bool nestDone = false;
            MayhemFixKit.Walk(new[] { ap }, e =>
            {
                if (!nestDone && e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "ActivateBuff")
                {
                    if (vf.Parameters.Length != 4)
                        throw new InvalidDataException($"LastChance: ActivateBuff arity x{vf.Parameters.Length} (expect 4)");
                    if (vf.Parameters[2] is not EX_Context)
                        throw new InvalidDataException("LastChance: ActivateBuff arg2 not a stats read");
                    vf.Parameters[2] = CasterStatsRead();
                    nestDone = true;
                }
            });
            if (!nestDone) throw new InvalidDataException("LastChance: nested stats replace failed");
            bool instDone = false, boolDone = false;
            MayhemFixKit.Walk(new[] { ap }, e =>
            {
                if (e is EX_LocalVariable lv && PtrName(lv.Variable).Contains("SpawnObject")) { lv.Variable = borrowObjA; instDone = true; }
            });
            MayhemFixKit.Walk(new[] { ap }, e =>
            {
                if (!boolDone && e is EX_LocalVariable lv && PtrName(lv.Variable).Contains("Success")) { lv.Variable = borrowBool; boolDone = true; }
            });
            if (!instDone || !boolDone) throw new InvalidDataException("LastChance: apply remap incomplete");
            out3.Add(ap);
            return out3;
        }

        // 1x Powerful / Rush / Berserk (1 turn) + Cursed (1 turn), all on the
        // targeted ally (unrolled triplets, sequential scratch).
        var block = new List<KismetExpression>();
        block.AddRange(MakeAllyBuff("BP_BattleBuff_Powerful_125_C", "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Powerful_125", 1));
        block.AddRange(MakeAllyBuff("BP_BattleBuff_Speed_C", "/Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_Speed", 1));
        block.AddRange(MakeAllyBuff("BP_BattleBuff_Berserk_C", "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Berserk", 1));
        block.AddRange(MakeAllyBuff("BP_BattleBuff_Cursed_C", "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Cursed", 2));

        // anchor: end of loop body (Moon charges), before its PopExecutionFlow.
        int moonIdx = -1, moonN = 0;
        for (int i = 0; i < stmts.Count; i++)
            if (CallsVirtual(stmts[i], "AddMoonCharges")) { moonIdx = i; moonN++; }
        if (moonN != 1) throw new InvalidDataException($"LastChance: moon anchor x{moonN} (expect 1)");
        if (legacy)
        {
            // v1 block (11: 3 triplets + builder + deal) or v2 block (48:
            // 16 triplets) sits exactly (moonIdx, popIdx): validate, remove.
            int popIdx = moonIdx + 1;
            while (popIdx < stmts.Count && stmts[popIdx] is not EX_PopExecutionFlow) popIdx++;
            if (popIdx >= stmts.Count) throw new InvalidDataException("LastChance: legacy loop-body end missing");
            int legacyN = popIdx - moonIdx - 1;
            if (legacyN != 11 && legacyN != 48) throw new InvalidDataException($"LastChance: legacy block x{legacyN} (expect 11 or 48)");
            int lSpawn = 0, lDur = 0, lAct = 0, lB = 0, lD = 0;
            for (int i = moonIdx + 1; i < popIdx; i++) MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_CallMath cm && cm.StackNode.IsImport()
                    && cm.StackNode.ToImport(bp).ObjectName.ToString() == "SpawnObject") lSpawn++;
                if (e is EX_CallMath sm && sm.StackNode.IsImport()
                    && sm.StackNode.ToImport(bp).ObjectName.ToString() == "SetIntPropertyByName") lDur++;
                if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "ActivateBuff") lAct++;
                if (e is EX_LocalVirtualFunction vb && vb.VirtualFunctionName.ToString() == "MakeDamageBuilder") lB++;
                if (e is EX_LocalVirtualFunction vd && vd.VirtualFunctionName.ToString() == "DealDamages") lD++;
            });
            bool v1 = legacyN == 11 && lSpawn == 3 && lDur == 3 && lAct == 3 && lB == 1 && lD == 1;
            bool v2 = legacyN == 48 && lSpawn == 16 && lDur == 16 && lAct == 16 && lB == 0 && lD == 0;
            if (!v1 && !v2)
                throw new InvalidDataException($"LastChance: legacy kinds spawn x{lSpawn} dur x{lDur} act x{lAct} bld x{lB} deal x{lD}");
            uint delOff = 0;
            for (int i = 0; i <= moonIdx; i++) delOff += stmts[i].GetSize(bp);
            uint delSize = 0;
            for (int i = moonIdx + 1; i < popIdx; i++) delSize += stmts[i].GetSize(bp);
            stmts.RemoveRange(moonIdx + 1, legacyN);
            fn.ScriptBytecode = stmts.ToArray();
            ShiftSigned(bp, fn, delOff, -(int)delSize);
            MayhemFixKit.RecalcContextSkips(bp, fn);
            Console.WriteLine("[ScielCustom] LastChance: legacy nuke block removed");
        }
        if (moonIdx + 1 >= stmts.Count || stmts[moonIdx + 1] is not EX_PopExecutionFlow)
            throw new InvalidDataException("LastChance: loop-body end drifted");
        uint insOff = 0;
        for (int i = 0; i <= moonIdx; i++) insOff += stmts[i].GetSize(bp);
        uint ssize = 0;
        foreach (var s in block) ssize += s.GetSize(bp);
        stmts.InsertRange(moonIdx + 1, block);
        fn.ScriptBytecode = stmts.ToArray();
        ShiftSigned(bp, fn, insOff, (int)ssize);
        MayhemFixKit.RecalcContextSkips(bp, fn);
        bp.Write(dest);
        Console.WriteLine("[ScielCustom] LastChance: Powerful/Rush/Berserk (1 turn) + Cursed (2) staged");
        return 1;
    }

    // ── 2d1. Ultima Path: 20 foretell as 2x10 capped applies ───────────────
    // ApplyForetell caps at 10 real stacks per call: a single 20-apply lands
    // 10 real + 10 UI ghost (consumers drain the 10 real, the ghost 10 sticks
    // forever, grants nothing). Row is 10; the donor's apply statement is
    // cloned verbatim (same target, same count) so 10+10 lands fully real.
    // Same-asset clone: zero imports, zero renames, one inserted statement.

    private static int PatchUltimaPathDoubleForetell(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_Sciel_UltimaPath";
        var dest = FindStaged(modDir, bpStem);
        var bp = Open(dest);
        var fn = bp.Exports.OfType<FunctionExport>().First(f => f.ObjectName.ToString() == "OnActionEffect");
        var stmts = fn.ScriptBytecode!.ToList();
        int applyIdx = -1, applyN = 0;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "ApplyForetell") hit = true;
            });
            if (hit) { applyIdx = i; applyN++; }
        }
        if (applyN == 2) { Console.WriteLine("[ScielCustom] UltimaPath double-apply already present"); return 0; }
        if (applyN != 1) throw new InvalidDataException($"UltimaPath: ApplyForetell statements x{applyN} (expect 1)");
        var dup = (KismetExpression)MayhemFixKit.CloneKismet(stmts[applyIdx]);
        uint insOff = 0;
        for (int i = 0; i <= applyIdx; i++) insOff += stmts[i].GetSize(bp);
        uint ssize = (uint)dup.GetSize(bp);
        stmts.Insert(applyIdx + 1, dup);
        fn.ScriptBytecode = stmts.ToArray();
        ShiftSigned(bp, fn, insOff, (int)ssize);
        MayhemFixKit.RecalcContextSkips(bp, fn);
        bp.Write(dest);
        Console.WriteLine("[ScielCustom] UltimaPath: foretell apply duplicated (10+10)");
        return 1;
    }

    // ── 2d1b. Ultima Path: Twilight +1 (TwilightDance transplant) ────────────
    // TwilightDance extends Twilight via the Foretell component natively:
    //   component.TryChangeActiveTwilightModeDuration(1)
    // The LVF is a TRY (safe no-op outside Twilight), so no IsInTwilightMode
    // gate is spliced: Ultima Path is single-target (one loop iteration),
    // making the unconditional call observationally identical to the gated
    // vanilla sequence, with zero flow ops, zero new locals, zero imports
    // (receiver reuses the live component local, IntConst 1 kept).

    private static int PatchUltimaTwilight(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_Sciel_UltimaPath";
        var dest = FindStaged(modDir, bpStem);
        var bp = Open(dest);
        var fn = bp.Exports.OfType<FunctionExport>().First(f => f.ObjectName.ToString() == "OnActionEffect");
        var stmts = fn.ScriptBytecode!.ToList();

        int prior = 0;
        foreach (var s in stmts) MayhemFixKit.Walk(new[] { s }, e =>
        {
            if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "TryChangeActiveTwilightModeDuration") prior++;
        });
        if (prior > 0) { Console.WriteLine("[ScielCustom] UltimaPath Twilight extend already present"); return 0; }

        var twdSrc = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "probe-twilight"),
                "BP_Battle_SkillScript_Sciel_TwilightDance.uasset")
            ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "extracted"),
                "BP_Battle_SkillScript_Sciel_TwilightDance.uasset")
            ?? throw new FileNotFoundException("TwilightDance script not found (probe-twilight/extracted)");
        var twd = Open(twdSrc);
        KismetExpression? tpl = null;
        int tplN = 0;
        foreach (var s in twd.Exports.OfType<FunctionExport>()
            .First(f => f.ObjectName.ToString() == "OnActionEffect").ScriptBytecode!)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "TryChangeActiveTwilightModeDuration") hit = true;
            });
            if (hit && s is EX_Context) { tpl = s; tplN++; }
        }
        if (tpl is null || tplN != 1) throw new InvalidDataException($"UltimaPath: Twilight extend template x{tplN} (expect 1)");
        var ext = (EX_Context)MayhemFixKit.CloneKismet(tpl);
        // Shape lock: receiver LV + one IntConst(1), nothing else remappable.
        int lvN = 0, intN = 0, objN = 0, callN = 0;
        MayhemFixKit.Walk(new[] { ext }, e =>
        {
            if (e is EX_LocalVariable) lvN++;
            if (e is EX_IntConst ic) { if (ic.Value != 1) throw new InvalidDataException("UltimaPath: extend amount drifted (expect 1)"); intN++; }
            if (e is EX_ObjectConst) objN++;
            if (e is EX_CallMath || e is EX_FinalFunction) callN++;
        });
        if (lvN != 1 || intN != 1 || objN != 0 || callN != 0)
            throw new InvalidDataException($"UltimaPath: extend shape lvs x{lvN} ints x{intN} objs x{objN} calls x{callN}");
        var compT = STPtr(stmts, "CallFunc_GetComponentByClass_ReturnValue", "foretell component");
        if (ext.ObjectExpression is EX_LocalVariable rlv) rlv.Variable = compT;
        else throw new InvalidDataException("UltimaPath: extend receiver shape drifted");
        if (ext.ContextExpression is EX_LocalVirtualFunction xvf
            && xvf.VirtualFunctionName.ToString() == "TryChangeActiveTwilightModeDuration")
            xvf.VirtualFunctionName = FName.FromString(bp, "TryChangeActiveTwilightModeDuration");
        else throw new InvalidDataException("UltimaPath: extend call shape drifted");

        // anchor: end of loop body (break-stun), before its PopExecutionFlow.
        int anchorIdx = -1, anchorN = 0;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "TryPerformBreakStun") hit = true;
            });
            if (hit) { anchorIdx = i; anchorN++; }
        }
        if (anchorN != 1) throw new InvalidDataException($"UltimaPath: break anchor x{anchorN} (expect 1)");
        if (anchorIdx + 1 >= stmts.Count || stmts[anchorIdx + 1] is not EX_PopExecutionFlow)
            throw new InvalidDataException("UltimaPath: loop-body end drifted");
        uint insOff = 0;
        for (int i = 0; i <= anchorIdx; i++) insOff += stmts[i].GetSize(bp);
        uint ssize = (uint)ext.GetSize(bp);
        stmts.Insert(anchorIdx + 1, ext);
        fn.ScriptBytecode = stmts.ToArray();
        ShiftSigned(bp, fn, insOff, (int)ssize);
        MayhemFixKit.RecalcContextSkips(bp, fn);
        bp.Write(dest);
        Console.WriteLine("[ScielCustom] UltimaPath: Twilight extend (+1) staged");
        return 1;
    }

    // ── 2d2. Ultima's Bargain: self-only absorb + self-Curse kill ──────────
    // NOT-gate inversion shared with Black Death (own OurSacrifice clone).
    // The drain stays at 1 HP (BlackDeath-proven: damage keeps working; a
    // 0-HP caster zeroes the strike). The kill comes from a self-applied
    // Cursed (1 turn) spliced before the function's Return: Cursed kills on
    // expiry through the status pipeline, which SetHP(0) never triggers
    // (it leaves Sciel immortal at 0 HP).

    private static int PatchUltimasBargain(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_Sciel_UltimasBargain";
        int n = PatchBlackDeath(modDir, bpStem);
        n += PatchBargainSelfCurse(modDir);
        return n;
    }

    // Canonical engine-function import constructor (mirrors
    // MaelleSkillsKit.EnsureEngineFunction, whose Death's Bargain self-Curse
    // works in-game): /Script/Engine package -> library -> Function. The
    // flattened-fake-Engine shape CTDs on link; this exact chain resolves.
    private static UAssetAPI.UnrealTypes.FPackageIndex EnsureEngineFunction(
        UAsset asset, string library, string functionName)
    {
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (imp.ObjectName.ToString() != functionName) continue;
            if (!imp.OuterIndex.IsImport()) continue;
            var outer = asset.Imports[Math.Abs(imp.OuterIndex.Index) - 1].ObjectName.ToString();
            if (outer == library) return UAssetAPI.UnrealTypes.FPackageIndex.FromImport(i);
        }
        UAssetAPI.UnrealTypes.FPackageIndex? enginePkg = null, lib = null;
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (imp.ObjectName.ToString() == "/Script/Engine" && imp.ClassName.ToString() == "Package")
                enginePkg = UAssetAPI.UnrealTypes.FPackageIndex.FromImport(i);
            if (imp.ObjectName.ToString() == library)
                lib = UAssetAPI.UnrealTypes.FPackageIndex.FromImport(i);
        }
        enginePkg ??= asset.AddImport(new Import("/Script/CoreUObject", "Package",
            UAssetAPI.UnrealTypes.FPackageIndex.FromRawIndex(0), "/Script/Engine", false, asset));
        lib ??= asset.AddImport(new Import("/Script/CoreUObject", library, enginePkg, library, false, asset));
        return asset.AddImport(new Import("/Script/CoreUObject", "Function", lib, functionName, false, asset));
    }

    private static int PatchBargainSelfCurse(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_Sciel_UltimasBargain";
        var dest = FindStaged(modDir, bpStem);
        var bp = Open(dest);
        var fn = bp.Exports.OfType<FunctionExport>().First(f => f.ObjectName.ToString() == "OnActionEffect");
        var stmts = fn.ScriptBytecode!.ToList();

        // Idempotence: the Cursed class const marks a prior splice.
        bool has = false;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_ObjectConst oc && oc.Value.IsImport()
                    && oc.Value.ToImport(bp).ObjectName.ToString() == "BP_BattleBuff_Cursed_C") has = true;
            });
        if (has) { Console.WriteLine("[ScielCustom] UltimasBargain self-Curse already present"); return 0; }

        // Fresh caster fetch (verbatim clone of the donor's own): the loop
        // temporaries are unreliable this late, and a null instigator would
        // silently void the buff (SoulTransplant v7 lesson).
        KismetExpression? fetchTpl = null;
        foreach (var s in stmts)
            if (s is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "GetCurrentCharacter")
            { fetchTpl = s; break; }
        if (fetchTpl is null) throw new InvalidDataException("UltimasBargain: caster fetch template not found");
        var fetch = (KismetExpression)MayhemFixKit.CloneKismet(fetchTpl);
        KismetPropertyPointer? casterT = null;
        MayhemFixKit.Walk(new[] { fetch }, e =>
        {
            if (casterT is null && e is EX_LocalVariable lv) casterT = lv.Variable;
        });
        if (casterT is null) throw new InvalidDataException("UltimasBargain: caster temp not found");

        // Native caster.AC read (same-asset template; cross-asset cloned
        // pointers resolve to garbage — SoulTransplant v9 lesson).
        bool IsPureStatsReadLocal(EX_Context cx)
        {
            bool ac = false, call = false;
            MayhemFixKit.Walk(new[] { cx }, x =>
            {
                if (x is EX_InstanceVariable iv && PtrName(iv.Variable) == "AC_jRPG_CharacterStats") ac = true;
                if (x is EX_LocalVirtualFunction || x is EX_FinalFunction) call = true;
            });
            return ac && !call;
        }
        EX_Context? acTpl = null;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (acTpl is not null) return;
                if (e is EX_Context cx && cx.ObjectExpression is EX_LocalVariable && IsPureStatsReadLocal(cx)) acTpl = cx;
            });
        if (acTpl is null) throw new InvalidDataException("UltimasBargain: native stats-read template not found");
        KismetExpression StatsForCaster()
        {
            var c = (EX_Context)MayhemFixKit.CloneKismet(acTpl);
            if (c.ObjectExpression is EX_LocalVariable olv) olv.Variable = casterT;
            else throw new InvalidDataException("UltimasBargain: stats element not found");
            return c;
        }

        // Out-locals (Maelle-proven: clone same-kind FProperties, rename,
        // append; never a new FFieldPath).
        var destOwner = FPackageIndex.FromExport(bp.Exports.IndexOf(fn));
        var props = (fn.LoadedProperties ?? Array.Empty<FProperty>()).ToList();
        EX_LocalVariable Local(string propName, Func<FProperty, bool> kind)
        {
            if (!props.Any(p => p.Name.ToString() == propName))
            {
                var tpl = props.FirstOrDefault(kind)
                    ?? throw new InvalidDataException($"UltimasBargain: no {propName} template property");
                var prop = (FProperty)tpl.GetType().GetMethod("MemberwiseClone",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(tpl, null)!;
                prop.Name = FName.FromString(bp, propName);
                props.Add(prop);
            }
            return new EX_LocalVariable
            {
                Variable = new KismetPropertyPointer(new FFieldPath(
                    new[] { FName.FromString(bp, propName) }, destOwner)),
            };
        }
        var spawnLocal = Local("CallFunc_SpawnObject_ReturnValue", p => p is FObjectProperty);
        var successLocal = Local("CallFunc_ActivateBuff_Success", p => p.GetType().Name.Contains("Bool"));
        fn.LoadedProperties = props.ToArray();

        // Self-Curse, Maelle Death's Bargain shape (her history: bare
        // ApplyBuff does NOT bind Cursed to a player — only
        // SpawnObject+ActivateBuff registers its hooks). TurnDuration 1:
        // expires at once, killing Sciel through the status pipeline.
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
                    StatsForCaster(),
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
                new EX_IntConst { Value = 1 },
            },
        };
        var activate = new EX_Context
        {
            ObjectExpression = StatsForCaster(),
            ContextExpression = new EX_VirtualFunction
            {
                VirtualFunctionName = FName.FromString(bp, "ActivateBuff"),
                Parameters = new KismetExpression[]
                {
                    (KismetExpression)MayhemFixKit.CloneKismet(spawnLocal),
                    new EX_ByteConst { Value = 4 },
                    StatsForCaster(),
                    (KismetExpression)MayhemFixKit.CloneKismet(successLocal),
                },
            },
            RValuePointer = acTpl.RValuePointer ?? new KismetPropertyPointer(new FFieldPath()),
        };

        // Exit path: before the single Return (after it = dead code).
        int retIdx = -1, retN = 0;
        for (int i = 0; i < stmts.Count; i++)
            if (stmts[i] is EX_Return) { retIdx = i; retN++; }
        if (retIdx < 0 || retN != 1)
            throw new InvalidDataException($"UltimasBargain: Return x{retN} (expect 1)");
        var block = new List<KismetExpression> { fetch, spawn, setDuration, activate };
        uint insOff = 0;
        for (int i = 0; i < retIdx; i++) insOff += stmts[i].GetSize(bp);
        uint ssize = 0;
        foreach (var s in block) ssize += s.GetSize(bp);
        stmts.InsertRange(retIdx, block);
        fn.ScriptBytecode = stmts.ToArray();
        ShiftSigned(bp, fn, insOff, (int)ssize);
        MayhemFixKit.RecalcContextSkips(bp, fn);
        bp.Write(dest);
        Console.WriteLine("[ScielCustom] UltimasBargain: self-Curse (1 turn) spliced before Return");
        return 1;
    }
    // * Buffs Speed/Defense15/Powerful_125 → Berserk/Cursed/Burn (ObjectConst
    //   swaps, TripleHex-proven) + TurnDuration → 5.
    // * Buff array 3 → 11 entries [Burn, Berserk, Cursed + Burn×4, Berserk×4]:
    //   the inner loop iterates Array_Length, so Cursed lands ×1, Burn and
    //   Berserk ×5 by repeat application.
    // * Hero source Context(GetAllAliveHeroes) → bare GetTargetedCharacters
    //   call (Fortune's Fury shape); heroes temp renamed to match.
    // Size changes (array growth, call shrink) go through signed shifts.

    private static int PatchCursedFrenzy(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_Sciel_CursedFrenzy";
        var dest = FindStaged(modDir, bpStem);
        var bp = Open(dest);
        LuneSkillsKit.RetargetPairs(bp, dest,
            ("CallFunc_GetAllAliveHeroes_Target_Heroes", "CallFunc_GetTargetedCharacters_TargetedCharacters"));

        var fn = bp.Exports.OfType<FunctionExport>().First(f => f.ObjectName.ToString() == "OnActionEffect");
        var stmts = fn.ScriptBytecode!.ToList();

        var swaps = new Dictionary<string, (string Cls, string Pkg)>(StringComparer.Ordinal)
        {
            ["BP_BattleBuff_Speed_C"] = ("BP_BattleBuff_Berserk_C", "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Berserk"),
            ["BP_BattleBuff_Defense15_C"] = ("BP_BattleBuff_Cursed_C", "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Cursed"),
            ["BP_BattleBuff_Powerful_125_C"] = ("BP_BattleBuff_Burn_C", "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Burn"),
        };
        int nSwap = 0;
        foreach (var s in stmts)
        {
            bool hasDuration = false;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_NameConst nc && nc.Value.ToString() == "TurnDuration") hasDuration = true;
                if (e is EX_ObjectConst oc && oc.Value.IsImport())
                {
                    var nm = oc.Value.ToImport(bp).ObjectName.ToString();
                    if (swaps.TryGetValue(nm, out var cf))
                    {
                        oc.Value = LuneSkillsKit.EnsureObjectImport(bp, cf.Pkg, cf.Cls, cf.Cls, "/Script/Engine");
                        nSwap++;
                    }
                }
            });
            if (!hasDuration) continue;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_IntConst ic) { ic.Value = 5; }
            });
        }
        if (nSwap != 3) throw new InvalidDataException($"CursedFrenzy buff swaps x{nSwap} (expect 3)");

        // Array expansion first (it sits at statement ~#1; shift everything after).
        var setArr = stmts.OfType<EX_SetArray>()
            .FirstOrDefault(a => a.Elements.Any(e => e is EX_ObjectConst));
        if (setArr is null) throw new InvalidDataException("CursedFrenzy buff array not found");
        if (setArr.Elements.Length != 3) throw new InvalidDataException($"CursedFrenzy buff array x{setArr.Elements.Length} (expect 3)");
        var extra = new List<KismetExpression>();
        for (int i = 0; i < 4; i++)
            extra.Add(new EX_ObjectConst
            {
                Value = LuneSkillsKit.EnsureObjectImport(bp,
                    "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Burn",
                    "BP_BattleBuff_Burn_C", "BP_BattleBuff_Burn_C", "/Script/Engine"),
            });
        for (int i = 0; i < 4; i++)
            extra.Add(new EX_ObjectConst
            {
                Value = LuneSkillsKit.EnsureObjectImport(bp,
                    "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Berserk",
                    "BP_BattleBuff_Berserk_C", "BP_BattleBuff_Berserk_C", "/Script/Engine"),
            });
        int arrIdx = stmts.FindIndex(s => ReferenceEquals(s, setArr));
        uint arrOff = 0;
        for (int i = 0; i < arrIdx; i++) arrOff += stmts[i].GetSize(bp);
        uint arrOld = setArr.GetSize(bp);
        setArr.Elements = setArr.Elements.Concat(extra).ToArray();
        int arrDelta = (int)setArr.GetSize(bp) - (int)arrOld;
        ShiftSigned(bp, fn, arrOff, arrDelta);
        MayhemFixKit.RecalcContextSkips(bp, fn);

        // Hero source: replace the whole GetAllAliveHeroes context statement.
        stmts = fn.ScriptBytecode!.ToList();
        int heroIdx = stmts.FindIndex(s => CallsVirtual(s, "GetAllAliveHeroes"));
        if (heroIdx < 0) throw new InvalidDataException("CursedFrenzy hero source not found");
        KismetPropertyPointer? heroesPtr = null;
        foreach (var s in stmts)
        {
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_LocalVariable lv && PtrName(lv.Variable) == "CallFunc_GetTargetedCharacters_TargetedCharacters")
                    heroesPtr ??= lv.Variable;
            });
            if (heroesPtr is not null) break;
        }
        if (heroesPtr is null) throw new InvalidDataException("CursedFrenzy renamed heroes temp not found");
        var getTargeted = new EX_LocalVirtualFunction
        {
            VirtualFunctionName = FName.FromString(bp, "GetTargetedCharacters"),
            Parameters = new KismetExpression[] { new EX_LocalVariable { Variable = heroesPtr } },
        };
        uint heroOff = 0;
        for (int i = 0; i < heroIdx; i++) heroOff += stmts[i].GetSize(bp);
        int heroDelta = (int)getTargeted.GetSize(bp) - (int)stmts[heroIdx].GetSize(bp);
        stmts[heroIdx] = getTargeted;
        fn.ScriptBytecode = stmts.ToArray();
        ShiftSigned(bp, fn, heroOff, heroDelta);
        MayhemFixKit.RecalcContextSkips(bp, fn);

        bp.Write(dest);
        Console.WriteLine("[ScielCustom] CursedFrenzy: buffs swapped, durations 5, array x11, single-target source");
        return 1;
    }

    // ── 2e. Cursed Frenzy DA targeting: transplant the single-ally
    // TargetingType enum property (NewEnumerator2) from DarkCleansing's DA.

    private static int PatchCursedFrenzyTargeting(string modDir)
    {
        var donor = Open(FindAsset("DA_Skill_Sciel_DarkCleansing"));
        EnumPropertyData? srcProp = null;
        foreach (var ex in donor.Exports.OfType<NormalExport>())
            foreach (var p in ex.Data)
                if (p is EnumPropertyData e && e.Name.ToString() == "TargetingType") srcProp = e;
        if (srcProp is null) throw new InvalidDataException("DarkCleansing TargetingType not found");
        var daDest = FindStaged(modDir, "DA_Skill_Sciel_CursedFrenzy");
        var da = Open(daDest);
        bool done = false;
        foreach (var ex in da.Exports.OfType<NormalExport>())
            for (int i = 0; i < ex.Data.Count; i++)
                if (ex.Data[i] is EnumPropertyData)
                {
                    var d = (EnumPropertyData)ex.Data[i];
                    if (d.Name.ToString() != "TargetingType") continue;
                    ex.Data[i] = new EnumPropertyData
                    {
                        Name = FName.FromString(da, "TargetingType"),
                        EnumType = FName.FromString(da, srcProp.EnumType.ToString()),
                        Value = FName.FromString(da, srcProp.Value.ToString()),
                    };
                    done = true;
                }
        if (!done) throw new InvalidDataException("CursedFrenzy TargetingType not found");
        da.Write(daDest);
        Console.WriteLine("[ScielCustom] CursedFrenzy DA targeting -> single ally");
        return 1;
    }

    private static string PtrName(UAssetAPI.Kismet.Bytecode.KismetPropertyPointer? p)
    {
        if (p?.New?.Path is { Length: > 0 } path)
            return string.Join(".", path.Select(n => n.ToString()));
        return p?.Old?.Index.ToString() ?? "?";
    }

    private static List<EX_LocalVariable> LocalsOf(KismetExpression s)
    {
        var list = new List<EX_LocalVariable>();
        MayhemFixKit.Walk(new[] { s }, e =>
        {
            if (e is EX_LocalVariable lv) list.Add(lv);
        });
        return list;
    }

    private static bool CallsVirtual(KismetExpression s, string name)
    {
        bool hit = false;
        MayhemFixKit.Walk(new[] { s }, e =>
        {
            if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == name) hit = true;
        });
        return hit;
    }

    private static bool SpawnsBuff(KismetExpression s, UAsset asset, string buffClass)
    {
        bool hit = false;
        MayhemFixKit.Walk(new[] { s }, e =>
        {
            if (e is EX_ObjectConst oc && oc.Value.IsImport()
                && oc.Value.ToImport(asset).ObjectName.ToString() == buffClass) hit = true;
        });
        return hit;
    }

    // ── 2c. False Start: transplant Doom's debuff block (SpawnObject →
    // ActivateBuff) with Greater Slow, v1 shape: GetCurrentCharacter proved
    // to resolve to SELF (slowed Sciel), so the char comes from a plain copy
    // of the delay path's own Array_Get_Item enemy temp instead. The buff
    // instance reuses False Start's dead-after-damage MakeDamageBuilder local
    // (both UObjects; nothing executes after the insertion point except
    // Return, so no live local is clobbered). Appends before EX_Return +
    // ShiftAbsolute. No explicit TurnDuration (Greater Slow default proved
    // working when this shape first landed).

    /// <summary>Mirrors a Doom-asset import (with its full outer chain) into
    /// FalseStart's table. A flat package-outer copy is WRONG here: function
    /// imports hang off CLASS imports (SpawnObject off GameplayStatics), and
    /// a package-outered record fails loader resolution (battle-load crash).
    /// Matches existing records by (name, class, outer-name) first.</summary>
    /// <summary>Signed jump/switch shift: statement replacement can SHRINK code,
    /// so the delta must be signed.</summary>
    private static void ShiftSigned(UAsset asset, FunctionExport fn, uint insertionPoint, int delta)
    {
        if (delta == 0) return;
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref pos, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j when j.CodeOffset > insertionPoint:
                        j.CodeOffset = (uint)((long)j.CodeOffset + delta); break;
                    case EX_JumpIfNot jn when jn.CodeOffset > insertionPoint:
                        jn.CodeOffset = (uint)((long)jn.CodeOffset + delta); break;
                    case EX_PushExecutionFlow pf when pf.PushingAddress > insertionPoint:
                        pf.PushingAddress = (uint)((long)pf.PushingAddress + delta); break;
                    case EX_SkipOffsetConst sc when sc.Value > insertionPoint:
                        sc.Value = (uint)((long)sc.Value + delta); break;
                    case EX_SwitchValue sv:
                        if (sv.EndGotoOffset > insertionPoint) sv.EndGotoOffset = (uint)((long)sv.EndGotoOffset + delta);
                        for (int i = 0; i < sv.Cases.Length; i++)
                            if (sv.Cases[i].NextOffset > insertionPoint)
                                sv.Cases[i].NextOffset = (uint)((long)sv.Cases[i].NextOffset + delta);
                        break;
                }
            });
    }

    /// <summary>Replaces the FIRST occurrence (by reference) of target in the
    /// tree with replacement. Field, property, and array-element slots.</summary>
    private static bool ReplaceNode(object root, KismetExpression target, KismetExpression replacement)
    {
        const System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        bool found = false;
        void Recurse(object node)
        {
            if (found || node is null) return;
            var t = node.GetType();
            foreach (var f in t.GetFields(bf))
            {
                if (found) return;
                object? v;
                try { v = f.GetValue(node); } catch { continue; }
                if (v is null) continue;
                if (ReferenceEquals(v, target)) { f.SetValue(node, replacement); found = true; return; }
                if (v is KismetExpression ke) Recurse(ke);
                else if (v is System.Collections.IList list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (ReferenceEquals(list[i], target)) { list[i] = replacement; found = true; return; }
                        if (list[i] is KismetExpression ike) Recurse(ike);
                    }
                }
            }
            foreach (var p in t.GetProperties(bf))
            {
                if (found) return;
                if (p.GetIndexParameters().Length > 0) continue;
                if (!p.CanWrite) continue;
                object? v;
                try { v = p.GetValue(node); } catch { continue; }
                if (v is null) continue;
                if (ReferenceEquals(v, target)) { p.SetValue(node, replacement); found = true; return; }
                if (v is KismetExpression ke) Recurse(ke);
            }
        }
        Recurse(root);
        return found;
    }

    private static FPackageIndex MirrorImport(UAsset from, UAsset to, FPackageIndex idx)
    {
        var imp = idx.ToImport(from);
        FPackageIndex newOuter = FPackageIndex.FromRawIndex(0);
        string outerName = "null";
        if (imp.OuterIndex is { } o && o.IsImport())
        {
            newOuter = MirrorImport(from, to, o);
            outerName = o.ToImport(from).ObjectName.ToString();
        }
        string name = imp.ObjectName.ToString(), cls = imp.ClassName.ToString();
        for (int i = 0; i < to.Imports.Count; i++)
        {
            var e = to.Imports[i];
            if (e.ObjectName.ToString() != name || e.ClassName.ToString() != cls) continue;
            string eOuter = "null";
            if (e.OuterIndex is { } eo && eo.IsImport() && eo.Index >= -to.Imports.Count)
                eOuter = eo.ToImport(to).ObjectName.ToString();
            if (eOuter == outerName) return FPackageIndex.FromImport(i);
        }
        var created = new Import(imp.ClassPackage.ToString(), cls, newOuter, name, false, to);
        to.AddImport(created);
        return FPackageIndex.FromImport(to.Imports.Count - 1);
    }

    private static int PatchFalseStart(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_Sciel_FalseStart";
        var dest = FindStaged(modDir, bpStem);
        var bp = Open(dest);
        var fn = bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();

        bool hasSlow = stmts.Any(s => SpawnsBuff(s, bp, "BP_BattleBuff_GreaterSlow_C"));
        if (hasSlow) { Console.WriteLine("[ScielCustom] FalseStart slow already present"); return 0; }

        // Doom template: SpawnObject(Slow) / GetCurrentCharacter / ActivateBuff.
        var doomSrc = FindAsset("BP_Battle_SkillScript_Sciel_Doom");
        Console.WriteLine($"[ScielCustom] FalseStart doomSrc={doomSrc}");
        var doom = Open(doomSrc);
        var doomFn = doom.Exports.OfType<FunctionExport>()
            .First(f => f.ObjectName.ToString() == "OnActionEffect");
        var ds = doomFn.ScriptBytecode!.ToList();
        int dSpawn = -1;
        for (int i = 0; i < ds.Count - 2; i++)
        {
            if (ds[i] is EX_LetObj && SpawnsBuff(ds[i], doom, "BP_BattleBuff_Slow_C")
                && ds[i + 1] is EX_LocalVirtualFunction vf1 && vf1.VirtualFunctionName.ToString() == "GetCurrentCharacter"
                && CallsVirtual(ds[i + 2], "ActivateBuff"))
            { dSpawn = i; break; }
        }
        if (dSpawn < 0) throw new InvalidDataException("Doom slow block not found");

        // Harvest FalseStart pointers (all native to this BP).
        KismetPropertyPointer? fsBuilder = null, fsChar = null, fsStatsMember = null, fsBool = null, fsEmptyRValue = null;
        foreach (var s in stmts)
        {
            if (s is EX_LetObj && CallsVirtual(s, "MakeDamageBuilder"))
            {
                var first = LocalsOf(s).FirstOrDefault();
                if (first is not null && PtrName(first.Variable).Contains("MakeDamageBuilder"))
                    fsBuilder = first.Variable;
            }
            if (s is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "GetCurrentCharacter")
            {
                var lv = LocalsOf(s).FirstOrDefault(l => PtrName(l.Variable).Contains("GetCurrentCharacter"));
                if (lv is not null) fsChar = lv.Variable;
            }
            // Any LetBool destination: declared-bool, dead by function end.
            if (s is EX_LetBool && fsBool is null)
            {
                var first = LocalsOf(s).FirstOrDefault();
                if (first is not null) fsBool = first.Variable;
            }
            // A native "empty" context RValue (serializes cleanly in this asset).
            if (fsEmptyRValue is null)
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (e is EX_Context cx && cx.RValuePointer is { } rv && PtrName(rv) == "?")
                        fsEmptyRValue = rv;
                });
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_InstanceVariable iv && PtrName(iv.Variable) == "AC_jRPG_CharacterStats")
                    fsStatsMember ??= iv.Variable;
            });
        }
        if (fsBuilder is null || fsChar is null || fsStatsMember is null || fsBool is null || fsEmptyRValue is null)
            throw new InvalidDataException(
                $"FalseStart harvest failed (builder={PtrName(fsBuilder)} char={PtrName(fsChar)} stats={PtrName(fsStatsMember)} bool={PtrName(fsBool)} empty={PtrName(fsEmptyRValue)})");

        // Template stats-read RHS from Doom #25 (Local_TargetStats = <target>.AC_jRPG_CharacterStats).
        var d25 = ds.FirstOrDefault(s => s is EX_LetObj
            && LocalsOf(s).Any(l => PtrName(l.Variable).Contains("Local_TargetStats")));
        if (d25 is null) throw new InvalidDataException("Doom Local_TargetStats assignment not found");
        EX_Context? statsCtxTpl = null;
        MayhemFixKit.Walk(new[] { d25 }, e =>
        {
            if (e is EX_Context c && statsCtxTpl is null) statsCtxTpl = c;
        });
        if (statsCtxTpl is null) throw new InvalidDataException("Doom stats-read context not found");
        var statsRead = (EX_Context)MayhemFixKit.CloneKismet(statsCtxTpl);
        MayhemFixKit.Walk(new[] { statsRead }, e =>
        {
            if (e is EX_LocalVariable lv && PtrName(lv.Variable).Contains("Array_Get"))
                lv.Variable = fsChar;
            if (e is EX_InstanceVariable iv && PtrName(iv.Variable) == "AC_jRPG_CharacterStats")
                iv.Variable = fsStatsMember;
        });
        // Buff class: Greater Slow (the v1 self-slowing build proved this
        // class + default duration works; the explicit TurnDuration mirror
        // and Burn diagnostic are dropped as poison suspects).
        var slowImp = LuneSkillsKit.EnsureObjectImport(bp,
            "/Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_GreaterSlow",
            "BP_BattleBuff_GreaterSlow_C", "BP_BattleBuff_GreaterSlow_C", "/Script/Engine");
        // N60 replacement: charLocal = delay path's enemy element. The tail's
        // own Array_Get populates CallFunc_Array_Get_Item with the enemy
        // (RemoveInitiative provably consumes it), so copy that slot — no
        // array re-fetch, no member reads, no new imports.
        KismetPropertyPointer? fsEnemy = null;
        foreach (var s in stmts)
        {
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (fsEnemy is null && e is EX_LocalVariable lv
                    && PtrName(lv.Variable) == "CallFunc_Array_Get_Item")
                    fsEnemy = lv.Variable;
            });
            if (fsEnemy is not null) break;
        }
        if (fsEnemy is null) throw new InvalidDataException("FalseStart: Array_Get_Item temp not found");
        var nCopy = new EX_LetObj
        {
            VariableExpression = new EX_LocalVariable { Variable = fsChar },
            AssignmentExpression = new EX_LocalVariable { Variable = fsEnemy },
        };

        // Doom N59 (spawn) + N61 (apply) clones. Doom N60 (GetCurrentCharacter)
        // is superseded by nCopy above.
        var nSpawn = (KismetExpression)MayhemFixKit.CloneKismet(ds[dSpawn]);
        var nApply = (KismetExpression)MayhemFixKit.CloneKismet(ds[dSpawn + 2]);
        // Cross-asset clone sanitation (Doom clones ONLY — nCopy is built
        // from same-asset parts and must NOT be mirrored): FName structs and
        // FPackageIndex values are map-local. Re-intern every name by string
        // and mirror every import into FalseStart's table (else ActivateBuff
        // reads as ArrayProperty, SpawnObject as Conv_NameToString, etc.).
        MayhemFixKit.Walk(new[] { nSpawn, nApply }, e =>
        {
            if (e is EX_LocalVirtualFunction vf)
                vf.VirtualFunctionName = FName.FromString(bp, vf.VirtualFunctionName.ToString());
            if (e is EX_CallMath cm && cm.StackNode.IsImport())
                cm.StackNode = MirrorImport(doom, bp, cm.StackNode);
            // NOTE: else-if — EX_CallMath extends EX_FinalFunction; a second
            // match would re-remap the already-fixed index against the wrong
            // table (that shipped SpringArm for SpawnObject).
            else if (e is EX_FinalFunction ff && ff.StackNode is { } fsn && fsn.IsImport())
                ff.StackNode = MirrorImport(doom, bp, fsn);
            if (e is EX_ObjectConst oc && oc.Value.IsImport())
            {
                var onm = oc.Value.ToImport(doom).ObjectName.ToString();
                if (onm == "BP_BattleBuff_Slow_C")
                    oc.Value = slowImp;
                else
                    oc.Value = MirrorImport(doom, bp, oc.Value);
            }
            if (e is EX_InstanceVariable iv)
            {
                if (PtrName(iv.Variable) == "AC_jRPG_CharacterStats") iv.Variable = fsStatsMember;
                else throw new InvalidDataException($"FalseStart transplant: unexpected member '{PtrName(iv.Variable)}'");
            }
        });

        // RValue owners: Doom's empties ("?") take the native empty shape;
        // Doom's AC member designators take the NATIVE member pointer.
        // (Blanketing everything empty was tried — it lobotomizes the
        // stats-read, whose RValue designates the member to read: the buff
        // then applies to null and silently fizzles.)
        var emptyRv = fsEmptyRValue;
        MayhemFixKit.Walk(new[] { nSpawn, nCopy, nApply, statsRead }, e =>
        {
            if (e is EX_Context cx && cx.RValuePointer is { } rv)
            {
                var nm = PtrName(rv);
                if (nm == "?") cx.RValuePointer = emptyRv;
                else if (nm == "AC_jRPG_CharacterStats") cx.RValuePointer = fsStatsMember;
                else throw new InvalidDataException($"FalseStart transplant: unexpected RValue '{nm}'");
            }
        });
        MayhemFixKit.Walk(new[] { nSpawn, nApply }, e =>
        {
            if (e is not EX_LocalVariable lv || lv.Variable is null) return;
            var nm = PtrName(lv.Variable);
            if (nm.Contains("SpawnObject_ReturnValue")) lv.Variable = fsBuilder;
            else if (nm.Contains("GetCurrentCharacter_CurrentCharacter")) lv.Variable = fsChar;
            else if (nm.Contains("ActivateBuff_Success")) lv.Variable = fsBool;
            else if (nm is "?" or "" || nm.Contains("Local_TargetStats")) { } // target: swapped below
            else throw new InvalidDataException($"FalseStart transplant: unexpected local '{nm}'");
        });

        bool targetSwapped = false;
        MayhemFixKit.Walk(new[] { nApply }, e =>
        {
            if (e is EX_Context ctx && ctx.ObjectExpression is EX_LocalVariable olv
                && PtrName(olv.Variable).Contains("Local_TargetStats"))
            {
                ctx.ObjectExpression = statsRead;
                targetSwapped = true;
            }
        });
        if (!targetSwapped) throw new InvalidDataException("FalseStart transplant: ActivateBuff target not found");

        // Insert before EX_Return; fix absolute jumps + context skips.
        int retIdx = stmts.FindIndex(s => s is EX_Return);
        if (retIdx < 0) throw new InvalidDataException("FalseStart has no EX_Return");
        uint insertOffset = 0;
        for (int i = 0; i < retIdx; i++) insertOffset += stmts[i].GetSize(bp);
        var block = new KismetExpression[] { nSpawn, nCopy, nApply };
        Console.WriteLine($"[ScielCustom] FalseStart transplant: doomBlock@{dSpawn} -> insert@{retIdx} (3 stmts)");
        uint delta = 0;
        foreach (var b in block) delta += b.GetSize(bp);
        stmts.InsertRange(retIdx, block);
        fn.ScriptBytecode = stmts.ToArray();
        ShiftSigned(bp, fn, insertOffset, (int)delta);
        MayhemFixKit.RecalcContextSkips(bp, fn);
        bp.Write(dest);
        Console.WriteLine("[ScielCustom] FalseStart: Greater Slow transplant appended (3 stmts)");
        return 1;
    }

    // ── 2b. Custom debuff keyword styles (Buff_* styles carry icons, which
    // would show the wrong symbols — clone iconless Element_Ice and recolor,
    // same recipe as the Gold DamageMultiplier). Grey Cursed, crimson
    // Berserk. (Purple Blight lives in SkillDescriptionsKit — shared with
    // Lune Ultima Noir — so it ships even with the Sciel gate off.) ───

    private static int PatchKeywordStyles(string modDir)
    {
        var dest = Directory.GetFiles(modDir, KeywordStyleTable + ".uasset", SearchOption.AllDirectories).FirstOrDefault();
        if (dest is null)
        {
            var src = FindAsset(KeywordStyleTable);
            dest = StageDest(src, modDir);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            FileCopy.AssetPair(src, dest);
        }
        var ed = new DataTableEditor(dest);
        ed.CloneRow("Element_Ice", "Debuff_Cursed");
        ed.CloneRow("Element_Ice", "Debuff_Berserk");
        ed.Apply("{\"Debuff_Cursed\":{\"TextColor.SpecifiedColor.SpecifiedColor\":\"(0.5, 0.5, 0.52, 1)\"}," +
                 "\"Debuff_Berserk\":{\"TextColor.SpecifiedColor.SpecifiedColor\":\"(0.8, 0.1, 0.15, 1)\"}}");
        ed.Save(dest);
        Console.WriteLine("[ScielCustom] Debuff_Cursed (grey) / Debuff_Berserk (crimson), no icons");
        return 1;
    }

    // ── 3. ST strings ───────────────────────────────────────────────────────

    private static int PatchNamesAndDescriptions(string modDir)
    {
        var stSrc = FindAsset(StringTable);
        Stage(stSrc, modDir, out var stDest);
        var entries = new Dictionary<string, string>();
        foreach (var m in Moves)
        {
            var key = "Sciel_" + m.Key;
            entries[key + "_Name"] = m.Name;
            entries[key + "_Description_Long"] = m.Long;
            entries[key + "_Description_Short"] = m.Short;
        }
        DataTableEditor.AddStringTableEntries(stDest, entries);
        Console.WriteLine($"[ScielCustom] staged {entries.Count} string-table entries");
        return 1;
    }

    // ── 4. Icons ────────────────────────────────────────────────────────────

    private static int PatchIcons(string modDir)
    {
        var iconSrc = FindAsset(IconTable);
        // The work/tables extract writes "Datatables" but the game mounts
        // "DataTables" — a wrong-case path ships but never loads (no icon
        // row). Normalize to the game's casing (Lune precedent).
        var iconDest = StageDest(iconSrc, modDir).Replace("/Resources/Datatables/", "/Resources/DataTables/");
        Directory.CreateDirectory(Path.GetDirectoryName(iconDest)!);
        if (!File.Exists(iconDest)) FileCopy.AssetPair(iconSrc, iconDest);
        var ed = new DataTableEditor(iconDest);
        foreach (var m in Moves)
            ed.CloneRow(m.DonorIcon, m.IconKey);
        ed.Save(iconDest);
        Console.WriteLine($"[ScielCustom] cloned {Moves.Length} icon rows");
        return 1;
    }

    // ── 5. Graph nodes + edges (DA asset; editor JSON handled by script) ─────

    private static int PatchGraph(string modDir)
    {
        var graphSrc = FindAsset(GraphStem);
        Stage(graphSrc, modDir, out var gDest);
        var g = Open(gDest);
        var gx = g.Exports.OfType<NormalExport>().First();
        var nodes = (ArrayPropertyData)gx.Data[0];
        var edges = (ArrayPropertyData)gx.Data[1];

        FPackageIndex? osImp = null;
        Import? templateImp = null;
        StructPropertyData? template = null;
        for (int i = 0; i < nodes.Value.Length; i++)
        {
            var nodeStruct = (StructPropertyData)nodes.Value[i];
            var su = (StructPropertyData)nodeStruct.Value[0];
            var skillPtr = ((ObjectPropertyData)su.Value[0]).Value;
            if (skillPtr is null || !skillPtr.IsImport()) continue;
            var impName = g.Imports[Math.Abs(skillPtr.Index) - 1].ObjectName.ToString();
            if (impName == "DA_Skill_Sciel_OurSacrifice")
            {
                osImp = skillPtr;
                templateImp = g.Imports[Math.Abs(skillPtr.Index) - 1];
                template = nodeStruct;
            }
        }
        if (osImp is null || templateImp is null || template is null)
            throw new InvalidDataException("OurSacrifice node not found in Sciel graph");

        foreach (var m in Moves)
        {
            var daStem = "DA_Skill_Sciel_" + m.Key;
            var daPkgPath = "/Game/Gameplay/SkillTree/Content/Sciel/Skills/" + daStem;
            var newDaIdx = LuneSkillsKit.EnsureObjectImport(g, daPkgPath, daStem,
                templateImp.ClassName.ToString(), templateImp.ClassPackage.ToString());

            var node = (StructPropertyData)template.Clone();
            node.Value[0] = (StructPropertyData)((StructPropertyData)node.Value[0]).Clone();
            node.Value[1] = (StructPropertyData)((StructPropertyData)node.Value[1]).Clone();
            var suN = (StructPropertyData)node.Value[0];
            ((ObjectPropertyData)suN.Value[0]).Value = newDaIdx;
            ((IntPropertyData)suN.Value[1]).Value = m.SpCost;
            ((BoolPropertyData)suN.Value[2]).Value = false;
            var req = (StructPropertyData)suN.Value[3];
            ((ObjectPropertyData)req.Value[0]).Value = null;
            ((NamePropertyData)req.Value[1]).Value = FName.FromString(g, "None");
            ((BoolPropertyData)suN.Value[4]).Value = false;
            var posWrap = (StructPropertyData)node.Value[1];
            ((Vector2DPropertyData)posWrap.Value[0]).Value = new FVector2D(m.PosX, m.PosY);
            nodes.Value = nodes.Value.Append(node).ToArray();

            var prereqImp = FindSkillImport(g, nodes, m.PrereqDa);
            var edge = (StructPropertyData)((StructPropertyData)edges.Value[0]).Clone();
            ((ObjectPropertyData)edge.Value[0]).Value = prereqImp;
            ((ObjectPropertyData)edge.Value[1]).Value = newDaIdx;
            edges.Value = edges.Value.Append(edge).ToArray();
        }
        g.Write(gDest);
        Console.WriteLine($"[ScielCustom] appended {Moves.Length} graph nodes + edges");
        return 1;
    }

    private static FPackageIndex FindSkillImport(UAsset g, ArrayPropertyData nodes, string daStem)
    {
        foreach (var nv in nodes.Value)
        {
            var su = (StructPropertyData)((StructPropertyData)nv).Value[0];
            var ptr = ((ObjectPropertyData)su.Value[0]).Value;
            if (ptr is null || !ptr.IsImport()) continue;
            if (g.Imports[Math.Abs(ptr.Index) - 1].ObjectName.ToString() == daStem)
                return ptr;
        }
        throw new KeyNotFoundException("graph node for " + daStem + " not found");
    }
}

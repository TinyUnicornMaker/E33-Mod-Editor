using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Monoco move kit — rebalance + DA AP costs + Sanctuary Greater Shell +
/// descriptions. Gated by Skill Overhaul "Character.Monoco". No graph
/// UnlockCost writes (feet / gradient items unlock skills).
///
/// Damage: base total = 0.75 × Clamp(AP), split across hits. Matching mask
/// (and Almighty-as-wildcard) applies OverchargeDamageMultiplier = 3.
/// Skills whose preferred mask is Almighty itself use ×5 so they sit
/// above curve when upgraded. Duallist's extra BonusDamageIfCorrectMask
/// is flattened to 1 so it does not stack on top of that ×5.
///
/// Gradients are charge costs on DA APCost, not the 0.75× curve:
/// Mighty Strike 3 / ×30, Sanctuary 5 / 5 shields + Greater Shell 5t,
/// Break Point 8 / ×80.
/// </summary>
public static class MonocoSkillsKit
{
    public const string BalTable = "DT_Balancing_MainCharacterSkills";
    public const string StringTable = "ST_MainCharacters_Skills";

    public static double Clamp(double ap) => SkillOverhaulKit.Clamp(ap);
    public static double BaseTotal(double ap) => 0.75 * Clamp(ap);
    public static double PerHit(double ap, int hits) => hits <= 0 ? 0 : BaseTotal(ap) / hits;
    /// <summary>Correct-mask multiplier. Almighty-preferred skills are ×5 (above curve); all others ×3.</summary>
    public static double MaskMult(string maskId) =>
        string.Equals(maskId, "Almighty", StringComparison.Ordinal) ? 5.0 : 3.0;

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
        n += PatchSanctuary(modDir);
        n += PatchNamesAndDescriptions(modDir);
        Console.WriteLine($"[Monoco] staged {n} asset edit(s)");
        return n;
    }

    private static string Gold(double v) => SkillDescriptionsKit.Gold(v);
    private const string Physical = "<keyword id=\"Element_Physical\">Physical</>";
    private const string Ice = "<keyword id=\"Element_Ice\">Ice</>";
    private const string Fire = "<keyword id=\"Element_Fire\">Fire</>";
    private const string Dark = "<keyword id=\"Element_Dark\">Dark</>";
    private const string Light = "<keyword id=\"Element_Light\">Light</>";
    private const string Earth = "<keyword id=\"Element_Earth\">Earth</>";
    private const string Lightning = "<keyword id=\"Element_Lightning\">Lightning</>";
    private const string Void = "<keyword id=\"Element_Void\">Void</>";
    private const string AP = "<keyword id=\"APShard\">AP</>";
    private const string Break = "<keyword id=\"Break\">Break</>";
    private const string Breaks = "<keyword id=\"Break\">Breaks</>";
    private const string BreakDmg = "<keyword id=\"BreakDamage\">Break damage</>";
    private const string Mark = "<keyword id=\"StatusEffect_Mark\">Mark</>";
    private const string Marked = "<keyword id=\"StatusEffect_Mark\">Marked</>";
    private const string Burn = "<keyword id=\"StatusEffect_Burn\">Burn</>";
    private const string Burning = "<keyword id=\"StatusEffect_Burn\">Burning</>";
    private const string Slow = "<keyword id=\"Buff_Slow\">Slow</>";
    private const string Rush = "<keyword id=\"Buff_Rush\">Rush</>";
    private const string Shell = "<keyword id=\"Buff_Shell\">Shell</>";
    private const string Regen = "<keyword id=\"Buff_Regen\">Regen</>";
    private const string Powerful = "<keyword id=\"Buff_Powerful\">Powerful</>";
    private const string Powerless = "<keyword id=\"Buff_Powerless\">Powerless</>";
    private const string Defenseless = "<keyword id=\"Buff_Defenseless\">Defenceless</>";
    private const string Shield = "<keyword id=\"Shield\">Shields</>";
    private const string ShieldOne = "<keyword id=\"Shield\">Shield</>";
    private const string Heal = "<keyword id=\"Heal\">Heals</>";
    private const string Recovers = "<keyword id=\"Heal\">Recovers</>";
    private const string Stun = "<keyword id=\"StatusEffect_Stunned\">Stunned</>";
    private const string Crits = "<keyword id=\"CriticalHit\">Critical Hits</>";
    private const string Almighty = "<keyword id=\"MonocoMask_Almighty\">Almighty Mask</>";

    private static string Mask(string id) => id switch
    {
        "Agile" => "<keyword id=\"MonocoMask_Agile_Left\">Agile Mask</>",
        "Heavy" => "<keyword id=\"MonocoMask_Heavy_Left\">Heavy Mask</>",
        "Caster" => "<keyword id=\"MonocoMask_Caster_Left\">Caster Mask</>",
        "Balanced" => "<keyword id=\"MonocoMask_Balanced_Left\">Balanced Mask</>",
        "Almighty" => "<keyword id=\"MonocoMask_Almighty_Left\">Almighty Mask</>",
        _ => id,
    };

    private enum Aim { Single, All, Random }

    private sealed record Spec(
        string Da, string Bal, string St, int Ap, int Hits,
        Aim Target, string Elem, string MaskId, bool OcDamage,
        string ExtraLong = "", string ExtraShort = "", string MaskExtra = "");

    // Foot-dropper AP from earliest Monoco-available fight (loot T1–T5).
    private static readonly Spec[] Specs =
    {
        new("DA_Skill_Transfo_AbbestMelee", "AbbestMelee", "Monoco_AbbestMelee",
            1, 1, Aim.Single, Physical, "Agile", true,
            ExtraLong: "Monoco plays a second turn.",
            ExtraShort: "Monoco plays a second turn.",
            MaskExtra: $"{Mask("Agile")}: Costs 0 {AP}. Deal {Gold(MaskMult("Agile"))} more damage."),
        new("DA_Skill_Transfo_Lancelier", "LancelierEstoc", "Monoco_LancelierEstoc",
            1, 1, Aim.Single, Ice, "Agile", true,
            ExtraLong: $"Applies {Slow} for 3 turns.", ExtraShort: $"Applies {Slow} for 3 turns."),
        new("DA_Skill_Transfo_ChalierRelentlessSword", "ChalierRelentlessSword", "Monoco_ChalierRelentlessSword",
            2, 6, Aim.Single, Physical, "Balanced", true,
            ExtraLong: "Interrupted if failed.", ExtraShort: "Interrupted if failed."),
        new("DA_Skill_Transfo_DemineurThunderStrike", "DemineurThunderStrike", "Monoco_DemineurThunderStrike",
            2, 1, Aim.Single, Lightning, "Caster", false,
            ExtraLong: $"Deals extra {BreakDmg}.", ExtraShort: $"Deals extra {BreakDmg}.",
            MaskExtra: $"{Mask("Caster")}: Increased {BreakDmg}."),
        new("DA_Skill_Transfo_BrulerAnchorsmash", "BrulerAnchorSmash", "Monoco_BrulerAnchorSmash",
            2, 3, Aim.Single, Physical, "Caster", true,
            ExtraLong: $"Can {Break}.", ExtraShort: $"Can {Break}."),
        new("DA_Skill_Transfo_CrulerShield", "CrulerShield", "Monoco_CrulerShield",
            2, 0, Aim.Single, "", "Heavy", false),
        new("DA_Skill_Transfo_LusterCombo", "LusterCombo", "Monoco_LusterCombo",
            2, 3, Aim.Single, Physical, "Agile", true,
            ExtraLong: $"Applies {Rush} to self for 3 turns.", ExtraShort: $"Applies {Rush} to self for 3 turns."),
        new("DA_Skill_Transfo_StalactMeleeCombo", "StalactCombo", "Monoco_StalactCombo",
            3, 4, Aim.Single, Ice, "Heavy", true,
            ExtraLong: $"High {BreakDmg}.", ExtraShort: $"High {BreakDmg}."),
        new("DA_Skill_Transfo_PotatoBagMageThunderThrows", "PotatobagMageThunderThrows", "Monoco_PotatobagMageThunderThrows",
            3, 3, Aim.Random, Lightning, "Caster", true,
            ExtraLong: $"{Crits} trigger an additional hit.", ExtraShort: $"{Crits} trigger an additional hit."),
        new("DA_Skill_Transfo_PotatobagRangerThunderEstoc", "PotatobagRangerThunderEstoc", "Monoco_PotatobagRangerThunderEstoc",
            3, 1, Aim.Single, Lightning, "Balanced", true,
            ExtraLong: $"Deals more damage if the target is {Stun}.", ExtraShort: $"More damage if the target is {Stun}."),
        new("DA_Skill_Transfo_PotatobagTankSlam", "PotatobagTankSlam", "Monoco_PotatobagTankSlam",
            3, 1, Aim.All, Physical, "Heavy", true,
            ExtraLong: $"Increased damage to {Marked} targets.", ExtraShort: $"<img id=\"StatusEffect_Mark\"/>: Increased damage."),
        new("DA_Skill_Transfo_PotatobagBoss_FireShots", "PotatobagBossFireShots", "Monoco_PotatobagBossFireShots",
            3, 3, Aim.All, Fire, "Almighty", true,
            ExtraLong: $"Applies 3 {Burn} per hit.", ExtraShort: $"Applies 3 {Burn} per hit."),
        new("DA_Skill_Transfo_GaultCombo", "GaultCombo", "Monoco_GaultCombo",
            4, 4, Aim.Single, Physical, "Balanced", true,
            ExtraLong: $"Applies {Mark}.", ExtraShort: $"Applies {Mark}."),
        new("DA_Skill_Transfo_PotierBuff", "PotierBuff", "Monoco_PotierBuff",
            4, 0, Aim.All, "", "Caster", false),
        new("DA_Skill_Transfo_JarCombo", "JarCombo", "Monoco_JarCombo",
            4, 4, Aim.All, Physical, "Heavy", true),
        new("DA_Skill_Transfo_PelerinFreshAir", "PelerinFreshAir", "Monoco_PelerinFreshAir",
            5, 0, Aim.All, "", "Caster", false),
        new("DA_Skill_Transfo_HexgaCombo", "HexgaCombo", "Monoco_HexgaCombo",
            5, 2, Aim.Single, Earth, "Heavy", true,
            ExtraLong: $"Applies {Defenseless} for 3 turns.", ExtraShort: $"Applies {Defenseless} for 3 turns.",
            MaskExtra: $"{Mask("Heavy")}: deal {Gold(MaskMult("Heavy"))} more damage. {Defenseless} lasts 5 turns."),
        new("DA_Skill_Transfo_RocherHammering", "RocherHammering", "Monoco_RocherHammering",
            5, 4, Aim.Single, Physical, "Heavy", true,
            ExtraLong: $"Can {Break}.", ExtraShort: $"Can {Break}."),
        new("DA_Skill_Transfo_HeavyCultistBloodSword", "CultistBloodSword", "Monoco_CultistBloodSword",
            5, 3, Aim.All, Dark, "Heavy", true,
            ExtraLong: "Sacrifices 90% Health to increase damage.", ExtraShort: "Sacrifices 90% Health to increase damage."),
        new("DA_Skill_Transfo_FlyingCultistSlash", "CultistFlyingSlashes", "Monoco_CultistFlyingSlashes",
            5, 3, Aim.Single, Dark, "Agile", true,
            ExtraLong: "Deals more damage the less Health Monoco has.", ExtraShort: "Deals more damage the less Health Monoco has."),
        new("DA_Skill_Transfo_RamasseurBonk", "RamasseurBonk", "Monoco_RamasseurBonk",
            6, 1, Aim.Single, Dark, "Agile", false,
            ExtraLong: $"Can {Break}.", ExtraShort: $"Can {Break}.",
            MaskExtra: $"{Mask("Agile")}: fills 20% of the target's Break Bar."),
        new("DA_Skill_Transfo_TroubadourBuff", "TroubadourBuff", "Monoco_TroubadourBuff",
            6, 0, Aim.All, "", "Caster", false),
        new("DA_Skill_Transfo_ChevaliereCAC", "ChevaliereCaC", "Monoco_ChevaliereCaC",
            6, 6, Aim.Single, Physical, "Agile", true,
            ExtraLong: $"Pierces {ShieldOne}. Damage increased for each {ShieldOne} on the target.",
            ExtraShort: $"Pierces {ShieldOne}. Damage increased per {ShieldOne}."),
        new("DA_Skill_Transfo_ChevaliereBAoECombo", "ChevaliereBAoECombo", "Monoco_ChevaliereBAoECombo",
            6, 3, Aim.All, Physical, "Heavy", true,
            ExtraLong: $"{Crits} deal {Gold(2.5)}.", ExtraShort: $"{Crits} deal {Gold(2.5)}."),
        new("DA_Skill_Transfo_PortierCrashingDown", "PortierSmash", "Monoco_PortierSmash",
            7, 1, Aim.All, Physical, "Heavy", true,
            ExtraLong: $"Can {Break}.", ExtraShort: $"Can {Break}."),
        new("DA_Skill_Transfo_MoissonneuseVendange", "MoissonneuseVendange", "Monoco_MoissonneuseVendange",
            7, 3, Aim.Single, Physical, "Balanced", true,
            ExtraLong: $"Can {Break}.", ExtraShort: $"Can {Break}."),
        new("DA_Skill_Transfo_ContorsionnisteAngryBlast", "ContorsionnisteAngryBlast", "Monoco_ContorsionnisteAngryBlast",
            7, 1, Aim.All, Physical, "Balanced", true,
            ExtraLong: $"{Heal} all allies by 10% Health for each enemy hit.",
            ExtraShort: $"{Heal} all allies by 10% Health for each enemy hit."),
        new("DA_Skill_Transfo_BoucheclierShield", "BoucheclierShield", "Monoco_BoucheclierShield",
            7, 0, Aim.All, "", "Heavy", false),
        new("DA_Skill_Transfo_ChapelierAxeSlash", "ChapelierAxeSlash", "Monoco_ChapelierAxeSlash",
            7, 3, Aim.All, Physical, "Agile", true,
            ExtraLong: $"Applies {Mark}.", ExtraShort: $"Applies {Mark}."),
        new("DA_Skill_Transfo_SaplingAbsorption", "SaplingAbsorption", "Monoco_SaplingAbsorption",
            8, 3, Aim.Single, Dark, "Caster", true,
            ExtraLong: $"{Recovers} 5% Health on hit.", ExtraShort: $"{Recovers} 5% Health on hit.",
            MaskExtra: $"{Mask("Caster")}: deal {Gold(MaskMult("Caster"))} more damage and double {Heal}."),
        new("DA_Skill_Transfo_GlaiseEarthquakes", "GlaiseEarthquake", "Monoco_GlaiseEarthquake",
            8, 3, Aim.All, Earth, "Heavy", false,
            ExtraLong: $"Applies {Powerful} to self for 3 turns.", ExtraShort: $"Applies {Powerful} to self for 3 turns.",
            MaskExtra: $"{Mask("Heavy")}: applies {Powerful} to all allies for 3 turns."),
        new("DA_Skill_Transfo_BalletCharm", "BalletCharm", "Monoco_BalletCharm",
            8, 1, Aim.Single, Light, "Caster", true,
            ExtraLong: $"Applies {Powerless} for 3 turns.", ExtraShort: $"Applies {Powerless} for 3 turns."),
        new("DA_Skill_Transfo_BenisseurMortar", "BenisseurMortar", "Monoco_BenisseurMortar",
            8, 3, Aim.Single, Ice, "Caster", true,
            ExtraLong: $"Change to {Almighty} if the target is {Marked}.",
            ExtraShort: $"<img id=\"StatusEffect_Mark\"/>: Change to {Almighty}."),
        new("DA_Skill_Transfo_ClairEnfeeble", "ClairEnfeeble", "Monoco_ClairEnfeeble",
            9, 1, Aim.All, Light, "Balanced", true,
            ExtraLong: $"Applies {Powerless} for 3 turns.", ExtraShort: $"Applies {Powerless} for 3 turns."),
        new("DA_Skill_Transfo_ObscurCombo", "ObscurCombo", "Monoco_ObscurCombo",
            9, 5, Aim.Single, Dark, "Heavy", true,
            ExtraLong: $"Deals more damage against {Powerless} targets.", ExtraShort: $"More damage against {Powerless} targets."),
        new("DA_Skill_Transfo_EvequeSpear", "EvequeSpear", "Monoco_EvequeSpear",
            10, 1, Aim.Single, Earth, "Heavy", true,
            ExtraLong: $"Applies {Powerless} for 3 turns.", ExtraShort: $"Applies {Powerless} for 3 turns."),
        new("DA_Skill_Transfo_GrosseTeteWrecking", "GrosseTeteWrecking", "Monoco_GrosseTeteWrecking",
            11, 5, Aim.Single, Physical, "Heavy", true,
            ExtraLong: $"Applies {Defenseless} for 3 turns.", ExtraShort: $"Applies {Defenseless} for 3 turns."),
        new("DA_Skill_Transfo_BraseleurHammerSmash", "BraseleurHammerSmash", "Monoco_BraseleurHammerSmash",
            12, 2, Aim.Single, Fire, "Balanced", true,
            ExtraLong: $"Applies 3 {Burn}.", ExtraShort: $"Applies 3 {Burn}."),
        new("DA_Skill_Transfo_DanseuseWingDance", "DanseuseWingDance", "Monoco_DanseuseWingDance",
            12, 3, Aim.Single, Fire, "Balanced", true,
            ExtraLong: $"Deals more damage against {Burning} targets.", ExtraShort: $"More damage against {Burning} targets."),
        new("DA_Skill_Transfo_ChevaliereCAoECombo", "ChevaliereFrozenCombo", "Monoco_ChevaliereFrozenCombo",
            14, 3, Aim.All, Ice, "Balanced", true,
            ExtraLong: $"Applies {Slow} for 3 turns.", ExtraShort: $"Applies {Slow} for 3 turns."),
        new("DA_Skill_Transfo_OrphelinBuff", "OrphelinPowerful", "Monoco_OrphelinPowerful",
            3, 0, Aim.All, "", "Caster", false),
        new("DA_Skill_Transfo_EchassierCombo", "EchassierCombo", "Monoco_EchassierCombo",
            16, 2, Aim.Single, Physical, "Agile", true,
            ExtraLong: $"Second hit applies {Mark}.", ExtraShort: $"Applies {Mark}."),
        new("DA_Skill_Transfo_AberrationBurningLight", "AberrationBurningLight", "Monoco_AberrationBurningLight",
            20, 2, Aim.All, Light, "Agile", false,
            ExtraLong: $"Applies 4 {Burn} per hit.", ExtraShort: $"Applies 4 {Burn} per hit.",
            MaskExtra: $"{Mask("Agile")}: Applies 2 more {Burn} per hit."),
        new("DA_Skill_Transfo_LampmasterSwordOfLight", "LampmasterSwordOfLight", "Monoco_LampmasterSwordOfLight",
            20, 1, Aim.All, Light, "Almighty", true,
            ExtraLong: "Increased damage at each cast.", ExtraShort: "Increased damage at each cast."),
        new("DA_Skill_Transfo_DuallistStormBlood", "DuallistStormBlood", "Monoco_DuallistStormBlood",
            21, 4, Aim.Single, Physical, "Almighty", true,
            ExtraLong: $"Can {Break}.", ExtraShort: $"Can {Break}."),
        new("DA_Skill_Transfo_CreationFromTheVoid", "CreationFromTheVoid", "Monoco_CreationFromTheVoid",
            21, 3, Aim.Random, Void, "Caster", true,
            ExtraLong: "More damage if the same target is hit multiple times.",
            ExtraShort: "More damage if the same target is hit multiple times."),
    };

    private sealed record RowEdit(string Row, double Value);

    private static IEnumerable<RowEdit> DamageRows()
    {
        foreach (var s in Specs)
        {
            if (s.Hits > 0)
                yield return new RowEdit(s.Bal + "_BaseDamage", PerHit(s.Ap, s.Hits));
            if (s.OcDamage)
                yield return new RowEdit(s.Bal + "_OverchargeDamageMultiplier", MaskMult(s.MaskId));
        }
        yield return new RowEdit("DuallistStormBlood_BonusDamageIfCorrectMask", 1.0);
        yield return new RowEdit("MightyStrike_BaseDamage", 15.0);
        yield return new RowEdit("Sanctuary_ShieldAmount", 5.0);
        yield return new RowEdit("BreakPoint_BaseDamage", 80.0);
    }

    private static int Rebalance(string modDir)
    {
        var balSrc = FindTables(BalTable);
        Stage(balSrc, modDir, out var balDest);
        var rows = new JObject();
        var edits = DamageRows().ToArray();
        foreach (var e in edits)
            rows[e.Row] = new JObject { ["FloatValue"] = e.Value };
        var ed = new DataTableEditor(balDest);
        int n = ed.Apply(rows.ToString(Newtonsoft.Json.Formatting.None));
        ed.Save(balDest);
        Console.WriteLine($"[Monoco] rebalanced {edits.Length} balancing row(s)");
        return n;
    }

    private static int PatchDataAssets(string modDir)
    {
        int n = 0;
        var das = Specs.Select(s => (s.Da, s.Ap))
            .Concat(new[]
            {
                ("DA_Skill_MightyStrike", 3),
                ("DA_Skill_Sanctuary", 5),
                ("DA_Skill_BreakPoint", 8),
            });
        foreach (var (stem, ap) in das)
        {
            var src = FindTables(stem);
            Stage(src, modDir, out var dest);
            var ed = DataTableEditor.OpenPropertyAsset(dest);
            n += ed.ApplyToProperties("{\"APCost\":" + ap + "}");
            ed.Save(dest);
            Console.WriteLine($"[Monoco] {stem} APCost -> {ap}");
        }
        return n;
    }

    /// <summary>Sanctuary: Regen 3t → Greater Shell 5t. Shield amount is a balancing row.</summary>
    private static int PatchSanctuary(string modDir)
    {
        const string bpStem = "BP_Battle_SkillScript_Monoco_Sanctuary";
        var src = FindTables(bpStem);
        Stage(src, modDir, out var dest);
        var bp = Open(dest);
        var fn = bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (fn?.ScriptBytecode is null) return 0;
        int n = 0;
        foreach (var s in fn.ScriptBytecode)
        {
            bool regen = false;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_ObjectConst oc && oc.Value.IsImport()
                    && oc.Value.ToImport(bp).ObjectName.ToString() == "BP_BattleBuff_Regen_C")
                    regen = true;
            });
            if (!regen) continue;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_ObjectConst oc && oc.Value.IsImport()
                    && oc.Value.ToImport(bp).ObjectName.ToString() == "BP_BattleBuff_Regen_C")
                {
                    oc.Value = LuneSkillsKit.EnsureObjectImport(bp,
                        "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_GreaterShell",
                        "BP_BattleBuff_GreaterShell_C", "BP_BattleBuff_GreaterShell_C", "/Script/Engine");
                    n++;
                }
                if (e is EX_IntConst ic && ic.Value == 3)
                {
                    ic.Value = 5;
                    n++;
                }
            });
        }
        if (n > 0) bp.Write(dest);
        Console.WriteLine($"[Monoco] {bpStem} Regen 3t -> Greater Shell 5t ({n} site(s))");
        return n > 0 ? 1 : 0;
    }

    private static string DamageLine(Spec s, bool shortForm)
    {
        if (s.Hits <= 0) return "";
        var ph = Gold(PerHit(s.Ap, s.Hits));
        var hit = s.Hits == 1 ? "1 hit" : $"{s.Hits} hits";
        var where = s.Target switch
        {
            Aim.All => $" {s.Elem} damage to all enemies",
            Aim.Random => $" {s.Elem} damage to random targets",
            _ => $" single target {s.Elem} damage",
        };
        return shortForm
            ? $"{ph}{where}. {hit}."
            : $"Deals {ph}{where}. {hit}.";
    }

    private static string MaskLine(Spec s)
    {
        if (!string.IsNullOrEmpty(s.MaskExtra)) return s.MaskExtra;
        if (s.Hits > 0 && s.OcDamage)
            return $"{Mask(s.MaskId)}: deal {Gold(MaskMult(s.MaskId))} more damage.";
        return "";
    }

    private static (string Long, string Short) UtilityText(Spec s) => s.St switch
    {
        "Monoco_CrulerShield" => (
            $"Applies 1-2 {Shield} to the target.\n{Mask("Heavy")}: Gives 2 {AP} to the target.",
            $"Applies 1 to 3 {Shield} to the target.\n{Mask("Heavy")}: Gives 2 {AP}."),
        "Monoco_PotierBuff" => (
            $"Gives 1-3 {AP} to all allies.\n{Mask("Caster")}: Gives 1 additional {AP}.",
            $"Gives 1-3 {AP} to all allies.\n{Mask("Caster")}: Gives 1 additional {AP}."),
        "Monoco_PelerinFreshAir" => (
            $"Applies {Regen} to all allies for 3 turns.\n{Mask("Caster")}: also {Heal} 40% Health.",
            $"Applies {Regen} to all allies for 3 turns.\n{Mask("Caster")}: also {Heal} 40% Health."),
        "Monoco_TroubadourBuff" => (
            $"Applies a random buff to 1 to 5 allies for 3 turns.\n{Mask("Caster")}: applies a second random buff for 3 turns.",
            $"Applies a random buff to 1 to 5 allies for 3 turns.\n{Mask("Caster")}: applies a second random buff for 3 turns."),
        "Monoco_BoucheclierShield" => (
            $"Applies {Shell} to 1-5 allies for 3 turns.\n{Mask("Heavy")}: Also applies 1 {ShieldOne}.",
            $"Applies {Shell} to 1-5 allies for 3 turns.\n{Mask("Heavy")}: Also applies 1 {ShieldOne}."),
        "Monoco_OrphelinPowerful" => (
            $"Applies {Powerful} to 1-5 allies for 3 turns.\n{Mask("Caster")}: Also gives 3 {AP} to targets.",
            $"Applies {Powerful} to 1-5 allies for 3 turns.\n{Mask("Caster")}: Also gives 3 {AP} to targets."),
        _ => ("", ""),
    };

    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.Ordinal)
    {
        ["Monoco_AbbestMelee"] = "Abbest Wind",
        ["Monoco_AberrationBurningLight"] = "Aberration Light",
        ["Monoco_BalletCharm"] = "Ballet Charm",
        ["Monoco_BenisseurMortar"] = "Bénisseur Mortar",
        ["Monoco_BoucheclierShield"] = "Boucheclier Fortify",
        ["Monoco_BraseleurHammerSmash"] = "Braseleur Smash",
        ["Monoco_BrulerAnchorSmash"] = "Bruler Bash",
        ["Monoco_ChalierRelentlessSword"] = "Chalier Combo",
        ["Monoco_ChapelierAxeSlash"] = "Chapelier Slash",
        ["Monoco_ChevaliereBAoECombo"] = "Chevalière Thrusts",
        ["Monoco_ChevaliereCaC"] = "Chevalière Piercing",
        ["Monoco_ChevaliereFrozenCombo"] = "Chevalière Ice",
        ["Monoco_ClairEnfeeble"] = "Clair Enfeeble",
        ["Monoco_ContorsionnisteAngryBlast"] = "Contorsionniste Blast",
        ["Monoco_CreationFromTheVoid"] = "Création Void",
        ["Monoco_CrulerShield"] = "Cruler Barrier",
        ["Monoco_CultistBloodSword"] = "Cultist Blood",
        ["Monoco_CultistFlyingSlashes"] = "Cultist Slashes",
        ["Monoco_DanseuseWingDance"] = "Danseuse Waltz",
        ["Monoco_DemineurThunderStrike"] = "Démineur Thunder",
        ["Monoco_DuallistStormBlood"] = "Duallist Storm",
        ["Monoco_EchassierCombo"] = "Échassier Stabs",
        ["Monoco_EvequeSpear"] = "Évêque Spear",
        ["Monoco_GaultCombo"] = "Gault Fury",
        ["Monoco_GlaiseEarthquake"] = "Glaise Earthquakes",
        ["Monoco_GrosseTeteWrecking"] = "Grosse Tête Whack",
        ["Monoco_HexgaCombo"] = "Hexga Crush",
        ["Monoco_JarCombo"] = "Jar Lampstorm",
        ["Monoco_LampmasterSwordOfLight"] = "Lampmaster Light",
        ["Monoco_LancelierEstoc"] = "Lancelier Impale",
        ["Monoco_LusterCombo"] = "Luster Slices",
        ["Monoco_MoissonneuseVendange"] = "Moissonneuse Vendange",
        ["Monoco_ObscurCombo"] = "Obscur Sword",
        ["Monoco_OrphelinPowerful"] = "Orphelin Cheers",
        ["Monoco_PelerinFreshAir"] = "Pèlerin Heal",
        ["Monoco_PortierSmash"] = "Portier Crash",
        ["Monoco_PotatobagBossFireShots"] = "Sakapatate Fire",
        ["Monoco_PotatobagMageThunderThrows"] = "Sakapatate Explosion",
        ["Monoco_PotatobagRangerThunderEstoc"] = "Sakapatate Estoc",
        ["Monoco_PotatobagTankSlam"] = "Sakapatate Slam",
        ["Monoco_PotierBuff"] = "Potier Energy",
        ["Monoco_RamasseurBonk"] = "Ramasseur Bonk",
        ["Monoco_RocherHammering"] = "Rocher Hammering",
        ["Monoco_SaplingAbsorption"] = "Sapling Absorption",
        ["Monoco_StalactCombo"] = "Stalact Punches",
        ["Monoco_TroubadourBuff"] = "Troubadour Trumpet",
        ["Monoco_MightyStrike"] = "Mighty Strike",
        ["Monoco_Sanctuary"] = "Sanctuary",
        ["Monoco_BreakPoint"] = "Break Point",
    };

    private static IEnumerable<(string Key, string Name, string Long, string Short)> AllStringEdits()
    {
        foreach (var s in Specs)
        {
            DisplayNames.TryGetValue(s.St, out var name);
            name ??= s.St;
            string longT, shortT;
            if (s.Hits <= 0)
            {
                (longT, shortT) = UtilityText(s);
            }
            else
            {
                var partsL = new List<string> { DamageLine(s, false) };
                var partsS = new List<string> { DamageLine(s, true) };
                if (!string.IsNullOrEmpty(s.ExtraLong)) partsL.Add(s.ExtraLong);
                if (!string.IsNullOrEmpty(s.ExtraShort)) partsS.Add(s.ExtraShort);
                var mask = MaskLine(s);
                if (!string.IsNullOrEmpty(mask)) { partsL.Add(mask); partsS.Add(mask); }
                longT = string.Join("\n", partsL);
                shortT = string.Join("\n", partsS);
            }
            yield return (s.St, name, longT, shortT);
        }

        yield return ("Monoco_MightyStrike", "Mighty Strike",
            $"Deals {Gold(15.0)} single target damage. 2 hits.\nUses weapon's element.\nDeals double damage if the target is {Stun}.\nGoes to {Almighty}.",
            $"{Gold(15.0)} {{DynamicElement}} damage. 2 hits.\nDeals double damage if the target is {Stun}.\nGoes to {Almighty}.");
        yield return ("Monoco_Sanctuary", "Sanctuary",
            $"Gives 5 {Shield} and applies Greater Shell to all allies for 5 turns.",
            $"Gives 5 {Shield} and applies Greater Shell to all allies for 5 turns.");
        yield return ("Monoco_BreakPoint", "Break Point",
            $"Deals {Gold(80.0)} single target damage. 1 hit.\nUses weapon's element.\nFills the target's Break Bar and {Breaks} it.",
            $"{Gold(80.0)} {{DynamicElement}} damage. 1 hit.\nFills the target's Break Bar and {Breaks} it.");
    }

    public static bool TryPreviewDescription(string baseKey, out string longText)
    {
        foreach (var (key, _, longT, _) in AllStringEdits())
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
        foreach (var (key, name, longText, shortText) in AllStringEdits())
        {
            entries[key + "_Name"] = name;
            entries[key + "_Description_Long"] = longText;
            entries[key + "_Description_Short"] = shortText;
        }
        DataTableEditor.AddStringTableEntries(stDest, entries);
        Console.WriteLine($"[Monoco] staged {entries.Count} string-table entries");
        return entries.Count;
    }
}

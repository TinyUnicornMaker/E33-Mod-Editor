using E33ModEditor.Core;
using E33ModEditor.Core.Loot;
using E33ModEditor.Core.SaveGame;
using E33ModEditor.Core.Spoiler;
using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;

// Commands:
//   find    <assetNameSubstring>              → containers containing the asset
//   extract <assetNameSubstring> [--out DIR]  → unpack the smallest matching container
//   dump    <uasset> [--out FILE]             → rows as JSON
//   flatten <uasset> <rowName>                → typed scalar leaves (as GUI sees them)
//   apply   <uasset> <edits.json>             → edit rows in place
//   pack    <dir> --name <ModName_P>          → build mod container into ~mods
//   spoiler-build [seed] [--out FILE]        → single-file spoiler for the seed
//   save-dump       <sav> [--out FILE.json]   → GVAS save as JSON
//   save-write      <json> <sav>              → JSON back into a .sav
//   save-roundtrip  <sav> [--write FILE]      → verify byte-identical re-serialization
//   save-pictos-add <sav> --id NAME [--level N] [--master] [--no-backup]
//   loot-audit                  → verify tier-list ids resolve to base rows or current mod ids
//   maelle-blonde               → retint Maelle-only hair MIs to dark golden blonde (work/mod_hair)

if (args.Length == 0) { PrintUsage(); return 1; }

try
{
    return args[0].ToLowerInvariant() switch
    {
        "find" => CmdFind(args),
        "extract" => CmdExtract(args),
        "extract-suspend" => CmdExtractSuspend(args),
        "findassets" => CmdFindAssets(args),
        "dump" => CmdDump(args),
        "flatten" => CmdFlatten(args),
        "apply" => CmdApply(args),
        "pack" => CmdPack(args),
        "maelle-stage" => CmdMaelleStage(args),
        "maelle-blonde" => CmdMaelleBlonde(args),
        "sciel-stage" => CmdScielStage(args),
        "sciel-custom-stage" => CmdScielCustomStage(args),
        "verso-stage" => CmdVersoStage(args),
        "dumpasset" => CmdDumpAsset(args),
        "dumpexport" => CmdDumpExport(args),
        "dumpcdo" => CmdDumpCdo(args),
        "rtwrite" => CmdRtWrite(args),
        "kisdump" => CmdKismetDump(args),
        "kistree" => CmdKismetTree(args),
        "st-sizes" => CmdStSizes(args),
        "fn-locals" => CmdFnLocals(args),
        "graphdump" => CmdGraphDump(args),
        "fx-smoke" => CmdFxSmoke(args),
        "fx-roll" => CmdFxRoll(args),
        "rando-smoke" => CmdRandoSmoke(args),
        "loot-dist" => CmdLootDist(args),
        "loot-audit" => CmdLootAudit(args),
        "enum-guard-smoke" => CmdEnumGuardSmoke(args),
        "tweaks-smoke" => CmdTweaksSmoke(args),
        "popup-cap-smoke" => CmdPopupCapSmoke(args),
        "balance-smoke" => CmdBalanceSmoke(args),
        "pictos-smoke" => CmdPictosSmoke(args),
        "picto-tags" => CmdPictoTags(args),
        "skill-overhaul-smoke" => CmdSkillOverhaulSmoke(args),
        "skill-graph-smoke" => CmdSkillGraphSmoke(args),
        "skill-desc-smoke" => CmdSkillDescSmoke(args),
        "dumpkismet" => CmdDumpKismet(args),
        "fn-list" => CmdFnList(args),
        "uberjumps" => CmdUberJumps(args),
        "entry-audit" => CmdEntryAudit(args),
        "find-fn" => CmdFindFn(args),
        "find-const" => CmdFindConst(args),
        "find-var" => CmdFindVar(args),
        "find-setarray" => CmdFindSetArray(args),
        "usage" => CmdUsage(args),
        "exports" => CmdExports(args),
        "names" => CmdNames(args),
        "mappings" => CmdMappings(args),
        "enums" => CmdEnums(args),
        "st" => CmdStringTable(args),
        "wfx-diag" => CmdWfxDiag(args),
        "wfx-inject" => CmdWfxInject(args),
        "roundtrip" => CmdRoundtrip(args),
        "cloneprobe" => CmdCloneProbe(args),
        "lune-wildfire" => CmdLuneWildfire(args),
        "lune-heal-smoke" => CmdLuneHealSmoke(args),
        "superspark" => CmdSuperSpark(args),
        "rebuild-mod" => CmdRebuildMod(args),
        "loc-languages" => CmdLocLanguages(args),
        "loc-catalog" => CmdLocCatalog(args),
        "loc-apply" => CmdLocApply(args),
        "loc-validate" => CmdLocValidate(args),
        "loc-smoke" => CmdLocSmoke(args),
        "spoiler-build" => CmdSpoilerBuild(args),
        "firstperson" => CmdFirstPerson(args),
        "firstperson-smoke" => CmdFirstPersonSmoke(args),
        "secondperson-pack" => CmdSecondPersonPack(args),
        "build-weaponoverlevel" => CmdBuildWeaponOverlevel(args),
        "weapon-overlevel-smoke" => CmdWeaponOverlevelSmoke(args),
        "bulk-smoke" => CmdBulkSmoke(args),
        "drop-smoke" => CmdDropSmoke(args),
        "dumpimports" => CmdDumpImports(args),
        "texture-info" => CmdTextureInfo(args),
        "texture-export" => CmdTextureExport(args),
        "dump-stains" => CmdDumpStains(args),
        "pak-colors" => CmdPakColors(args),
        "pak-owners" => CmdPakOwners(args),
        "graph-diag" => CmdGraphDiag(args),
        "kismet-check" => CmdKismetCheck(args),
        "save-dump" => CmdSaveDump(args),
        "save-write" => CmdSaveWrite(args),
        "save-roundtrip" => CmdSaveRoundtrip(args),
        "save-pictos-add" => CmdSavePictosAdd(args),
        "save-pictos-swap" => CmdSavePictosSwap(args),
        "save-summary" => CmdSaveSummary(args),
        "save-story" => CmdSaveStory(args),
        "save-stage-rebuild" => CmdSaveStageRebuild(args),
        "compat-detect" => CmdCompatDetect(args),
        "compat-patch" => CmdCompatPatch(args),
        "compat-smoke" => CmdCompatSmoke(args),
        "party5" => CmdParty5(args),
        "party5-smoke" => CmdParty5Smoke(args),
        "battle-groups" => CmdBattleGroups(args),
        "battle-groups-smoke" => CmdBattleGroupsSmoke(args),
        "random-battles" => CmdRandomBattles(args),
        "random-battles-smoke" => CmdRandomBattlesSmoke(args),
        "row-damage-smoke" => CmdRowDamageSmoke(args),
        _ => Unknown(args[0]),
    };
}
catch (Exception e)
{
    Console.Error.WriteLine($"error: {e}");
    return 2;
}

static int CmdFlatten(string[] args)
{
    var editor = new DataTableEditor(Path.GetFullPath(args[1]));
    var row = editor.Table.Table.Data.FirstOrDefault(r =>
        E33ModEditor.Core.DataTableEditor.StripSuffix(r.Name.ToString()) == args[2])
        ?? throw new KeyNotFoundException($"row '{args[2]}' not found");
    foreach (var leaf in editor.FlattenRow(row))
        Console.WriteLine($"{leaf.Kind,-10} {leaf.Path,-70} {leaf.Value}");
    return 0;
}

static int CmdDumpAsset(string[] args)
{
    var editor = DataTableEditor.OpenPropertyAsset(Path.GetFullPath(args[1]));
    foreach (var leaf in editor.FlattenProperties())
        Console.WriteLine($"{leaf.Kind,-10} {leaf.Path,-50} {leaf.Value}");
    return 0;
}

static int CmdDumpExport(string[] args)
{
    // dumpasset opens only the first NormalExport; Blueprints keep per-object
    // data (e.g. CurveFloat keys, SCS templates) on the other exports.
    var path = Path.GetFullPath(args[1]);
    var want = args.Length > 2 ? args[2] : "";
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
        : new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    foreach (var exp in u.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>())
    {
        var nm = exp.ObjectName.ToString();
        if (!string.IsNullOrEmpty(want) && nm != want) continue;
        DumpPropList(exp.Data, nm, 0);
    }
    return 0;
}

static void DumpPropList(List<UAssetAPI.PropertyTypes.Objects.PropertyData> list, string prefix, int depth)
{
    if (depth > 6) return;
    foreach (var p in list)
    {
        var path = prefix.Length == 0 ? DataTableEditor.CleanName(p.Name) : $"{prefix}.{DataTableEditor.CleanName(p.Name)}";
        switch (p)
        {
            case UAssetAPI.PropertyTypes.Structs.StructPropertyData st:
                DumpPropList(st.Value, path, depth + 1);
                break;
            case UAssetAPI.PropertyTypes.Objects.ArrayPropertyData arr when arr.Value != null:
                for (int i = 0; i < arr.Value.Length; i++)
                {
                    var e = arr.Value[i];
                    if (e is UAssetAPI.PropertyTypes.Structs.StructPropertyData se)
                        DumpPropList(se.Value, $"{path}.{i}", depth + 1);
                    else
                        Console.WriteLine($"{"",-10} {$"{path}.{i}",-50} {e}");
                }
                break;
            default:
                Console.WriteLine($"{p.GetType().Name,-10} {path,-50} {p}");
                break;
        }
    }
}

static int CmdFxSmoke(string[] args)
{
    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_fxsmoke");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);
    WeaponEffectsCreateKit.Reload();
    Console.WriteLine("specs " + WeaponEffectsCreateKit.Specs.Count);
    var fails = new List<string>();
    void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "ok   " : "FAIL ") + what);
        if (!ok) fails.Add(what);
    }

    // Walk a staged BP's bytecode for int/float constants (for rework checks).
    IEnumerable<UAssetAPI.Kismet.Bytecode.KismetExpression> BpExprs(string assetName)
    {
        var pth = WeaponEffectsCreateKit.FindAsset(dir, assetName);
        if (pth is null) yield break;
        var mp = DataTableEditor.Mappings;
        var u = mp != null
            ? new UAssetAPI.UAsset(pth, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, mp)
            : new UAssetAPI.UAsset(pth, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
        foreach (var fn in u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            var all = new List<UAssetAPI.Kismet.Bytecode.KismetExpression>();
            E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode, all.Add);
            foreach (var e in all) yield return e;
        }
    }
    int IntConstCount(string bp, int v) => BpExprs(bp)
        .Count(e => e is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ic && ic.Value == v);
    int FloatConstCount(string bp, double v) => BpExprs(bp).Count(e =>
        (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst dc && Math.Abs(dc.Value - v) < 1e-6)
        || (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_FloatConst fc && Math.Abs(fc.Value - v) < 1e-5));

    // The DamageReason comparison on a consume clone: "EqualEqual_ByteByte:1"
    // means skills-only (SkillCast), "NotEqual_ByteByte:4" means the old
    // every-non-base-attack gate that also ate Burn/status DOT ticks.
    string ConsumeReason(string assetName)
    {
        var pth = WeaponEffectsCreateKit.FindAsset(dir, assetName);
        if (pth is null) return "missing";
        var mp = DataTableEditor.Mappings;
        var u = mp != null
            ? new UAssetAPI.UAsset(pth, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, mp)
            : new UAssetAPI.UAsset(pth, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
        foreach (var fn in u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            string? found = null;
            E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode, e =>
            {
                if (e is not UAssetAPI.Kismet.Bytecode.Expressions.EX_FinalFunction ff
                    || ff.Parameters.Length != 2
                    || ff.Parameters[1] is not UAssetAPI.Kismet.Bytecode.Expressions.EX_ByteConst bc)
                    return;
                var name = ff.StackNode.IsImport()
                    ? ff.StackNode.ToImport(u)?.ObjectName?.ToString() ?? ""
                    : "";
                if (name is "EqualEqual_ByteByte" or "NotEqual_ByteByte")
                    found = name + ":" + bc.Value;
            });
            if (found is not null) return found;
        }
        return "none";
    }

    var n = WeaponEffectsCreateKit.Inject(dir);
    Console.WriteLine("injected " + n);

    // Consume clones must gate on SkillCast (1), never "!= 4": Burn/status DOT
    // ticks are DamageReason 2 and would otherwise eat the stack they just applied.
    Check(ConsumeReason("BP_LU_WepFx_BurnConsume4Skills.uasset") == "EqualEqual_ByteByte:1",
        "Skill Consume Burn checks DamageReason==1 (skill), not !=4");
    Check(ConsumeReason("BP_LU_WepFx_SlowConsumeSkills.uasset") == "EqualEqual_ByteByte:1",
        "Status Consume (Skills) checks DamageReason==1 (skill), not !=4");
    Check(ConsumeReason("BP_LU_WepFx_SlowConsume.uasset") == "EqualEqual_ByteByte:4",
        "Status Consume (base attack) still checks DamageReason==4");

    var bal = WeaponEffectsCreateKit.FindAsset(dir, "DT_Balancing_Luminas.uasset")
        ?? throw new FileNotFoundException("mod balancing");
    var vanilla = Path.Combine(EditorPaths.RepoRoot,
        "work/tables/DT_Balancing_Luminas/Sandfall/Content/Gameplay/Battle/ScalingSystem/BalancingTables/Content/DT_Balancing_Luminas.uasset");
    Check(FloatValue(vanilla, "AttackBurn_BurnDamageMultiplier") == "2", "vanilla AttackBurn multiplier untouched");
    Check(FloatValue(bal, "WepFx_AttackBurn5_BurnDamageMultiplier") == "5", "WepFx_AttackBurn5 balancing = 5");
    Check(FloatValue(vanilla, "Stains_FireStart_StainsAmount") == "1", "vanilla FireStart amount untouched");
    Check(FloatValue(bal, "WepFx_FireStart2_StainsAmount") == "2", "WepFx_FireStart2 balancing = 2");
    Check(FloatValue(bal, "APOnCounter_APAmount") == "5", "vanilla APOnCounter reworked to 5 AP");
    Check(FloatValue(vanilla, "APOnCounter_APAmount") == "2", "pristine APOnCounter untouched (2)");
    Check(FloatValue(bal, "WepFx_CounterBurn10_BurnDamageMultiplier") == "1",
        "WepFx_CounterBurn10 Burn damage = 1x (normal)");
    // Burn reworks.
    Check(FloatValue(bal, "BurnDamage_BurnDamage_DamageMultiplier") == "0.25", "BurnDamage I = 25%");
    Check(FloatValue(bal, "WepFx_BurnDmg2_BurnDamage_DamageMultiplier") == "0.5", "BurnDmg II = 50%");
    Check(FloatValue(bal, "WepFx_BurnDmg3_BurnDamage_DamageMultiplier") == "1", "BurnDmg III = 100%");
    Check(FloatValue(bal, "WepFx_BurnDmg4_BurnDamage_DamageMultiplier") == "2", "BurnDmg IV = 200%");
    Check(FloatValue(bal, "WepFx_BurnDmg5_BurnDamage_DamageMultiplier") == "5", "BurnDmg V = 500%");
    Check(FloatValue(bal, "LightDMGBurnOnCrit_BurnStack") == "5", "LightDMGBurnOnCrit = 5 Burn");
    Check(IntConstCount("BP_LU_LightDMGBurnOnCrit.uasset", 5) > 0,
        "LightDMGBurnOnCrit applies 5 real Burn stacks (ChangeStackCount)");
    Check(IntConstCount("BP_LU_Foretell_ApplyBurnEveryXForetell.uasset", 10) > 0,
        "Foretell_ApplyBurnEveryXForetell applies 10 real Burn stacks (ChangeStackCount)");
    Check(FloatValue(bal, "Stains_BurnOnCrit_StackToApply") == "5", "BurnOnCrit = 5 Burn (stacks)");
    Check(FloatValue(bal, "CritAppliesBurn_BurnDamageMultiplier") == "5",
        "CritAppliesBurn = 5x Burn damage");
    Check(FloatValue(bal, "MarkIsBurn_BurnStack") == "100", "MarkIsBurn = 100 Burn");
    Check(FloatValue(bal, "Stance_BurnInVirtuose_StackToApply") == "50", "BurnInVirtuose = 50 Burn/hit");
    Check(FloatValue(bal, "Foretell_ApplyBurnEveryXForetell_BurnDamageMultiplier") == "10"
          && FloatValue(bal, "Foretell_ApplyBurnEveryXForetell_BurnStackInterval") == "1",
        "Foretell_ApplyBurnEveryXForetell = 10 Burn per Foretell");
    Check(FloatValue(bal, "Perfection_BurnOnBaseAtkPerRank_BurnAmountOnRankS") == "250",
        "Perfection_BurnOnBaseAtkPerRank = 50 Burn/rank (S=250)");
    Check(IntConstCount("BP_LU_AttackBurn.uasset", 5) > 0
          && IntConstCount("BP_LU_AttackBurn.uasset", 2) == 0, "AttackBurn applies 5 Burn (const 2->5)");
    // Element Skill Damage ladder -> 100%.
    Check(FloatValue(bal, "SkillsFireDMG+_DamageIncrease") == "1"
          && FloatValue(bal, "SkillsPhysicalDMG+_DamageIncrease") == "1"
          && FloatValue(bal, "SkillsLightDMG+_DamageIncrease") == "1"
          && FloatValue(bal, "SkillsVoidDMG+_DamageIncrease") == "1",
        "element Skills*DMG+ = 100%");
    Check(FloatValue(bal, "Stance_HealOnSwitch_HealPercentOnStanceSwitch") == "0.2",
        "Stance_HealOnSwitch = 20%");
    Check(IntConstCount("BP_LU_APOnShield.uasset", 5) > 0
          && IntConstCount("BP_LU_APOnShield.uasset", 1) == 0, "APOnShield = 5 AP (const 1->5)");
    Check(FloatValue(bal, "WepFx_AltCrit1_DamageIncrease") == "0.5"
          && FloatValue(bal, "WepFx_AltCrit5_DamageIncrease") == "6", "AltCrit ladder 50->600%");
    Check(FloatValue(bal, "WepFx_MacabrePower1_DamageIncrease") == "0.5"
          && FloatValue(bal, "WepFx_MacabrePower3_DamageIncrease") == "2", "MacabrePower ladder 50/100/200%");
    Check(FloatValue(bal, "WepFx_FirstLife1_DamageIncrease") == "1"
          && FloatValue(bal, "WepFx_FirstLife3_DamageIncrease") == "3", "FirstLife ladder 100/200/300%");
    Check(FloatValue(bal, "Stains_IncreaseDMGPerLightStain_DamageIncrease") == "0.5",
        "IncreaseDMGPerLightStain = 50%/stain");
    Check(FloatValue(bal, "Stains_ElementalCyclingDMG+_DamageIncreasePerDifferentElementalDamage") == "0.5",
        "ElementalCyclingDMG+ = 50%/element");
    Check(FloatValue(bal, "Stains_OverchargeDMG+_MaxMultiplier") == "4.2",
        "OverchargeDMG+ cap = 21 stacks");
    Check(FloatValue(bal, "Masks_DMG+OverchargedSkills_DamageIncrease") == "1",
        "Masks_DMG+OverchargedSkills = 100%");
    Check(E33ModEditor.Core.Loot.WeaponEffectTiers.Load().TierOf("NoOverchargeSkillBuff") == 1,
        "NoOverchargeSkillBuff moved to Tier1");
    Check(IntConstCount("BP_LU_WepFx_AttackBurn5.uasset", 10) > 0, "WepFx_AttackBurn5 applies 10 Burn");
    Check(IntConstCount("BP_LU_WepFx_AttackBurn10.uasset", 25) > 0, "WepFx_AttackBurn10 applies 25 Burn");
    Check(FloatConstCount("BP_LU_LightDamageBurn.uasset", 1.0) > 0
          && FloatConstCount("BP_LU_LightDamageBurn.uasset", 0.2) == 0, "LightDamageBurn = 100% chance");

    var pePath = WeaponEffectsCreateKit.FindAsset(dir, "DT_PassiveEffects.uasset");
    var pe = pePath is null ? null : new DataTableEditor(pePath);
    Check(pe != null && WeaponEffectsCreateKit.Specs.All(s => pe.HasRow(s.Id)),
        $"all {WeaponEffectsCreateKit.Specs.Count} passive rows present");
    if (pe != null && pe.TryGetRow("WepFx_EchoFireIce", out var echoRow))
    {
        var buffs = echoRow.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "BattleBuffs")
            as UAssetAPI.PropertyTypes.Objects.ArrayPropertyData;
        var target = buffs is { Value.Length: > 0 } ? buffs.Value[0]?.ToString() ?? "" : "";
        Check(target.Contains("/Created/BP_LU_WepFx_EchoFireIce", StringComparison.OrdinalIgnoreCase),
            "echo row BattleBuffs -> Created package (" + target + ")");
    }
    else Check(false, "WepFx_EchoFireIce row present");

    var stPath = WeaponEffectsCreateKit.FindAsset(dir, "ST_PassiveEffects.uasset");
    var st = stPath is null ? null : DataTableEditor.OpenStringTable(stPath);
    Check(st != null && st.ContainsKey("PASSIVE_WepFx_EchoFireIce_Name")
        && st.ContainsKey("PASSIVE_WepFx_EchoFireIce_Description"),
        "string table entries for WepFx_EchoFireIce");
    if (st != null)
    {
        Check(st.TryGetValue("PASSIVE_CounterDefenseless_Description", out var cd)
              && cd.Contains("Apply Defenceless on Counterattack"), "CounterDefenseless tooltip aligned");
        Check(st.TryGetValue("PASSIVE_APOnCounter_Description", out var ap)
              && ap.Contains("Gain 5 AP on Counterattack"), "APOnCounter tooltip = Gain 5 AP");
        Check(st.TryGetValue("PASSIVE_CounterBurn_Description", out var cbt)
              && cbt.Contains("Apply 50 Burn"), "CounterBurn tooltip = 50 Burn");
        Check(st.TryGetValue("PASSIVE_WepFx_CounterBurn10_Description", out var wb)
              && wb.Contains("200 Burn"), "WepFx_CounterBurn10 tooltip = 200 Burn");
        Check(st.TryGetValue("PASSIVE_BurnDamage_Description", out var bd) && bd.Contains("25% increased Burn damage"),
            "BurnDamage I tooltip = 25%");
        Check(st.TryGetValue("PASSIVE_AttackBurn_Description", out var ab) && ab.Contains("applies 5 Burn"),
            "AttackBurn tooltip = 5 Burn");
        Check(st.TryGetValue("PASSIVE_MarkIsBurn_Description", out var mi) && mi.Contains("100 Burn"),
            "MarkIsBurn tooltip = 100 Burn");
        Check(st.TryGetValue("PASSIVE_WepFx_BurnDmg5_Description", out var b5) && b5.Contains("500%"),
            "WepFx_BurnDmg5 tooltip = 500%");
        Check(st.TryGetValue("PASSIVE_LightDamageBurn_Description", out var ld) && ld.Contains("Applies Burn"),
            "LightDamageBurn tooltip = 100%");
        Check(st.TryGetValue("PASSIVE_AugmentedAttack_Weapon_Description", out var aa)
              && aa.Contains("50% increased Base Attack"), "AugmentedAttack tooltip = 50%");
        Check(st.TryGetValue("PASSIVE_AugmentedAim_Weapon_Description", out var am)
              && am.Contains("50% increased Free Aim"), "AugmentedAim tooltip = 50%");
    }

    // Vanilla BP rework: CounterBurn's ChangeStackCount(5) becomes 50.
    var counterBurnBp = WeaponEffectsCreateKit.FindAsset(dir, "BP_LU_CounterBurn.uasset");
    Check(counterBurnBp is not null, "vanilla BP_LU_CounterBurn staged for the 50-stack rework");
    if (counterBurnBp is not null)
    {
        var cbMap = DataTableEditor.Mappings;
        var cbU = cbMap != null
            ? new UAssetAPI.UAsset(counterBurnBp, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, cbMap)
            : new UAssetAPI.UAsset(counterBurnBp, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
        int fifty = 0, five = 0;
        foreach (var fn in cbU.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode, e =>
            {
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ic)
                {
                    if (ic.Value == 50) fifty++;
                    if (ic.Value == 5) five++;
                }
            });
        }
        Check(fifty > 0 && five == 0, $"CounterBurn applies 50 Burn (i50={fifty}, i5={five})");
    }

    // The clone keeps its own stack patch: 200 stacks, normal Burn multiplier (checked above).
    var cb10 = WeaponEffectsCreateKit.FindAsset(dir, "BP_LU_WepFx_CounterBurn10.uasset");
    Check(cb10 is not null, "WepFx_CounterBurn10 clone exists");
    if (cb10 is not null)
    {
        var cbMap10 = DataTableEditor.Mappings;
        var cbU10 = cbMap10 != null
            ? new UAssetAPI.UAsset(cb10, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, cbMap10)
            : new UAssetAPI.UAsset(cb10, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
        int two00 = 0, five10 = 0;
        foreach (var fn in cbU10.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode, e =>
            {
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ic)
                {
                    if (ic.Value == 200) two00++;
                    if (ic.Value == 5) five10++;
                }
            });
        }
        Check(two00 > 0 && five10 == 0, $"WepFx_CounterBurn10 applies 200 Burn (i200={two00}, i5={five10})");
    }

    var echoAsset = WeaponEffectsCreateKit.FindAsset(dir, "BP_LU_WepFx_EchoFireIce.uasset");
    Check(echoAsset is not null, "echo clone exists");
    if (echoAsset is not null)
    {
        var m = ReadEchoMap(echoAsset);
        Check(m.entries == 1, $"echo TypeToTypeMap has exactly 1 entry (got {m.entries})");
        Check(m.key == "EAttackType::NewEnumerator0", "echo key = EAttackType::NewEnumerator0 (Fire) got " + m.key);
        Check(m.value == "EAttackType::NewEnumerator1", "echo value = EAttackType::NewEnumerator1 (Ice) got " + m.value);
        Check(m.listener.EndsWith("::NewEnumerator0", StringComparison.Ordinal) || m.listener == "NewEnumerator0",
            "echo InitialStainTypeToListen = EAttackType::NewEnumerator0 got " + m.listener);
        Check(m.bareNames == 0, $"no bare 'NewEnumerator*' names leaked (got {m.bareNames})");
    }

    var start = WeaponEffectsCreateKit.FindAsset(dir, "BP_LU_WepFx_FireStart2.uasset");
    Check(start is not null, "stain-start clone exists");

    // Condition table integrity: every tagged effect is internally consistent
    // and every declared conflict tag is actually carried by an effect.
    var cond = E33ModEditor.Core.Loot.WeaponEffectConditions.Load();
    var condTiers = E33ModEditor.Core.Loot.WeaponEffectTiers.Load();
    var condIds = E33ModEditor.Core.Loot.WeaponEffectTiers.Columns
        .Where(c => c != "DoNotUse")
        .SelectMany(c => condTiers.Column(c))
        .Distinct(StringComparer.Ordinal).ToList();
    var badSelf = condIds.Where(id => !cond.SelfConsistent(id)).ToList();
    Check(badSelf.Count == 0, $"condition tags are self-consistent (bad: {string.Join(",", badSelf)})");
    var tagged = condIds.Where(id => cond.TagsOf(id).Count > 0).ToList();
    Check(tagged.Count > 0, $"condition table tags effects ({tagged.Count})");
    var unused = cond.ConditionTags
        .Where(t => !tagged.Any(id => cond.TagsOf(id).Contains(t, StringComparer.Ordinal))).ToList();
    Check(unused.Count == 0, $"every conflict tag is used (unused: {string.Join(",", unused)})");

    if (fails.Count > 0)
    {
        Console.WriteLine($"fx-smoke FAILED ({fails.Count})");
        return 1;
    }
    Console.WriteLine("fx-smoke PASSED");
    return 0;

    static string? FloatValue(string table, string row)
    {
        try
        {
            var ed = new DataTableEditor(table);
            if (!ed.TryGetRow(row, out var r)) return null;
            return ed.FlattenRow(r).FirstOrDefault(l => l.Path == "FloatValue")?.Value?.ToString();
        }
        catch { return "<parse error>"; }
    }
}

static EchoMapState ReadEchoMap(string uasset)
{
    var map = DataTableEditor.Mappings;
    var a = map != null
        ? new UAssetAPI.UAsset(uasset, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
        : new UAssetAPI.UAsset(uasset, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    var cdo = a.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>()
        .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
    var mp = cdo?.Data.OfType<UAssetAPI.PropertyTypes.Objects.MapPropertyData>()
        .FirstOrDefault(x => DataTableEditor.CleanName(x.Name) == "TypeToTypeMap");
    string KeyOf(UAssetAPI.PropertyTypes.Objects.PropertyData p) => p switch
    {
        UAssetAPI.PropertyTypes.Objects.EnumPropertyData e => e.Value?.Value?.Value ?? "",
        UAssetAPI.PropertyTypes.Objects.BytePropertyData b => b.EnumValue?.Value?.Value ?? "",
        _ => "<" + p.GetType().Name + ">",
    };
    var listenerProp = cdo?.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "InitialStainTypeToListen");
    return new EchoMapState(
        mp?.Value.Count ?? -1,
        mp is { Value.Count: > 0 } ? KeyOf(mp.Value.First().Key) : "",
        mp is { Value.Count: > 0 } ? KeyOf(mp.Value.First().Value) : "",
        listenerProp is null ? "<absent>" : KeyOf(listenerProp),
        a.GetNameMapIndexList().Count(nm => (nm.Value ?? "").StartsWith("NewEnumerator", StringComparison.Ordinal)));
}

static int CmdCloneProbe(string[] args)
{
    var src = Path.GetFullPath(args[1]);
    var dest = Path.GetFullPath(args[2]);
    var mode = args.Length > 3 ? args[3] : "b";   // n=none r=retarget s=stem b=both
    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
    FileCopy.AssetPair(src, dest);
    var map = DataTableEditor.Mappings;
    var a = map != null
        ? new UAssetAPI.UAsset(dest, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
        : new UAssetAPI.UAsset(dest, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, null);
    var newStem = Path.GetFileNameWithoutExtension(dest);
    var srcStem = Path.GetFileNameWithoutExtension(src);
    if (mode is "r" or "b")
        typeof(E33ModEditor.Core.WeaponEffectsCreateKit)
            .GetMethod("RetargetPackage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object?[] { a, dest, srcStem, newStem });
    if (mode is "s" or "b")
    {
        var names = a.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s)) continue;
            if (s.Contains(srcStem, StringComparison.Ordinal))
                a.SetNameReference(i, new UAssetAPI.UnrealTypes.FString(s.Replace(srcStem, newStem, StringComparison.Ordinal)));
        }
    }
    a.Write(dest);
    Console.WriteLine($"cloneprobe[{mode}] wrote {Path.GetFileName(dest)}");
    return 0;
}

static int CmdRoundtrip(string[] args)
{
    var p = Path.GetFullPath(args[1]);
    var map = DataTableEditor.Mappings;
    var a = map != null
        ? new UAssetAPI.UAsset(p, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
        : new UAssetAPI.UAsset(p, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    a.Write(p);
    Console.WriteLine("roundtripped " + Path.GetFileName(p));
    return 0;
}

static int CmdWfxInject(string[] args)
{
    var dir = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(dir);
    WeaponEffectsCreateKit.Reload();
    Console.WriteLine("specs " + WeaponEffectsCreateKit.Specs.Count);
    var n = WeaponEffectsCreateKit.Inject(dir, copyTables: args.Length < 3 || args[2] != "--copy-tables");
    Console.WriteLine("injected " + n);
    return 0;
}

static int CmdWfxDiag(string[] args)
{
    var staging = Path.Combine(EditorPaths.RepoRoot, "work", "mod");
    var dir = EditorPaths.TempSubdir("wfx_diag");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);

    foreach (var f in new[] { "DT_PassiveEffects", "DT_Balancing_Luminas", "ST_PassiveEffects" })
    {
        var src = WeaponEffectsCreateKit.FindAsset(staging, f + ".uasset");
        if (src is null) { Console.WriteLine("staging missing " + f); continue; }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        FileCopy.Overwrite(src, Path.Combine(dir, src[idx..]));
        var exp = Path.ChangeExtension(src, ".uexp");
        if (File.Exists(exp)) FileCopy.Overwrite(exp, Path.Combine(dir, exp[idx..]));
    }
    Console.WriteLine("seeded scratch from staging (copyTables:false path)");

    WeaponEffectsCreateKit.Reload();
    Console.WriteLine("specs " + WeaponEffectsCreateKit.Specs.Count);
    try
    {
        var n = WeaponEffectsCreateKit.Inject(dir, copyTables: false);
        Console.WriteLine("injected " + n);
    }
    catch (Exception e)
    {
        Console.WriteLine($"INJECT THREW {e.GetType().Name}: {e.Message}");
        foreach (var l in (e.StackTrace ?? "").Split('\n').Take(6))
            Console.WriteLine("  " + l.Trim());
    }

    var pe = WeaponEffectsCreateKit.FindAsset(dir, "DT_PassiveEffects.uasset");
    if (pe != null)
    {
        try
        {
            var ed = new DataTableEditor(pe);
            int wfx = ed.RowNames.Count(r => r.StartsWith("WepFx_", StringComparison.Ordinal));
            Console.WriteLine($"passive rows total={ed.RowNames.Count} wepfx={wfx}");
            if (ed.TryGetRow("WepFx_EchoFireFire", out var row))
                foreach (var f in row.Value)
                    if (DataTableEditor.CleanName(f.Name) == "BattleBuffs"
                        && f is UAssetAPI.PropertyTypes.Objects.ArrayPropertyData arr
                        && arr.Value.Length > 0)
                        Console.WriteLine("echo BattleBuffs[0]=" + arr.Value[0]);
        }
        catch (Exception e) { Console.WriteLine("post-parse passive failed: " + e.Message); }
    }
    else Console.WriteLine("scratch has no DT_PassiveEffects");

    DumpEchoMap(dir, "BP_LU_WepFx_EchoFireFire");
    return 0;
}

static void DumpEchoMap(string dir, string stem)
{
    var p = WeaponEffectsCreateKit.FindAsset(dir, stem + ".uasset");
    if (p is null) { Console.WriteLine("missing " + stem); return; }
    var a = new UAssetAPI.UAsset(p, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, DataTableEditor.Mappings);
    var cdo = a.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>()
        .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
    var mp = cdo?.Data.OfType<UAssetAPI.PropertyTypes.Objects.MapPropertyData>()
        .FirstOrDefault(x => DataTableEditor.CleanName(x.Name) == "TypeToTypeMap");
    if (mp is null) { Console.WriteLine(stem + ": no TypeToTypeMap prop"); return; }
    Console.WriteLine($"{stem} TypeToTypeMap entries={mp.Value.Count}");
    foreach (var kv in mp.Value)
        Console.WriteLine($"   {FmtProp(kv.Key)} -> {FmtProp(kv.Value)}");
    int bare = a.GetNameMapIndexList().Count(n => (n.Value ?? "").StartsWith("NewEnumerator", StringComparison.Ordinal));
    Console.WriteLine("   bare 'NewEnumerator*' names in namemap: " + bare);
}

static string FmtProp(UAssetAPI.PropertyTypes.Objects.PropertyData p) => p switch
{
    UAssetAPI.PropertyTypes.Objects.BytePropertyData b => $"byte[{b.EnumValue}]",
    UAssetAPI.PropertyTypes.Objects.EnumPropertyData e => $"enum[{e.EnumType}]{e.Value}",
    _ => p.GetType().Name,
};

static int CmdRandoSmoke(string[] args)
{
    // Full catalog (tiers + areas), mirroring the real rebuild: the chest
    // staging below must exercise the same area-share machinery that ships,
    // not tier-1 area-less rows.
    int randoFails = 0;
    void Chk(bool ok, string what)
    {
        Console.WriteLine((ok ? "ok:   " : "FAIL: ") + what);
        if (!ok) randoFails++;
    }

    var catalog = NewLootCatalogWithExtras();
    catalog.Rebuild(
        Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(EditorPaths.RepoRoot, "work", "chests.json"))),
        Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(EditorPaths.RepoRoot, "work", "enemies.json"))),
        Array.Empty<(string, Newtonsoft.Json.Linq.JObject)>());
    Console.WriteLine("protected " + catalog.ProtectedItems.Count);
    // Star Simon's six haircuts are both DLC Items and Cosmetic. TypeOf()
    // reports the first category (DLC Items), so they must be in the protected
    // set by id or the drop randomizer replaces them with gear.
    var simonFaces = new[]
    {
        "FaceGustave_Simon", "FaceVerso_Simon", "FaceMaelle_Simon",
        "FaceLune_Simon", "FaceSciel_Simon", "FaceMonoco_Simon",
    };
    var unprotectedFaces = simonFaces.Where(id => !catalog.IsProtected(id)).ToList();
    if (unprotectedFaces.Count > 0)
    {
        Console.Error.WriteLine(
            "rando-smoke FAILED: Simon cosmetics not protected: " + string.Join(",", unprotectedFaces));
        return 1;
    }
    // The Mask Keeper phase 2 is the second half of the Visages island Axon
    // fight, not cut content: its catalog display name is what the spoiler
    // parses for the area tag, so it must say (Visages), not (Cut Content).
    var maskKeeper = catalog.Enemies.GetValueOrDefault("MF_Axon_MaskKeeper_VisagesPhase2");
    if (maskKeeper is null
        || !maskKeeper.DisplayName.Contains("(Visages)", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine(
            "rando-smoke FAILED: Mask Keeper phase 2 must carry the (Visages) area tag");
        return 1;
    }
    var sampleKeys = catalog.ProtectedItems.Where(id =>
        id.StartsWith("Quest_", StringComparison.Ordinal) || catalog.TypeOf(id) == "Key Item").Take(5);
    Console.WriteLine("key-sample " + string.Join(",", sampleKeys));
    for (int t = 1; t <= 5; t++)
        Console.WriteLine($"elements T{t} " + string.Join(",", E33ModEditor.Core.Loot.RandomizerKit.WeaponElementPool(t)));

    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_randosmoke");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);
    // Mirror the pack order: tutorials are disabled before the randomizer, so
    // the Monoco's Station Stalact/Pelerin repoints must survive the roll.
    E33ModEditor.Core.TutorialDisableKit.Apply(dir);
    var s = new E33ModEditor.Core.Loot.RandomizerKit.Settings
    {
        Seed = 42,
        Chests = false,
        EnemyDrops = false,
        Merchants = false,
        WeaponPassives = false,
        MonsterStats = true,
        WeaponStats = true,
    };
    var n = E33ModEditor.Core.Loot.RandomizerKit.Apply(dir, s, catalog, null);
    Console.WriteLine("changed " + n);
    {
        var smokeEne = FileCopy.FindUnder(dir, "DT_jRPG_Enemies.uasset")
            ?? throw new FileNotFoundException("mod smoke enemies");
        var smokeEd = new DataTableEditor(smokeEne);
        double Xp(string id)
        {
            if (!smokeEd.TryGetRow(id, out var row)) return 0;
            return smokeEd.DumpRow(row)["EnemyScaling"]?["Experience"]?.ToObject<double>() ?? 0;
        }
        var cloneXp = Xp("MM_Danseuse_CloneAlpha");
        var mainXp = Xp("MM_Danseuse_Alpha");
        Chk(mainXp > 0 && cloneXp > 0 && cloneXp <= mainXp * 0.15 + 1,
            $"Danseuse clone pays 10% XP (clone {cloneXp:0} vs main {mainXp:0})");
        var abbestVenueXp = Xp("BV_SM_Abbest_Alpha_69");
        var abbestNativeXp = Xp("SM_Abbest_Alpha");
        Chk(abbestVenueXp > 0 && abbestNativeXp > 0 && abbestVenueXp < abbestNativeXp * 0.5,
            $"Abbest Tower venue no longer pays native XP (venue {abbestVenueXp:0} vs native {abbestNativeXp:0})");
    }
    if (!E33ModEditor.Core.TutorialDisableKit.VerifyActorSwaps(dir))
    {
        Console.Error.WriteLine(
            "rando-smoke FAILED: tutorial Stalact/Pelerin actor swap or encounter row is wrong");
        return 1;
    }
    Console.WriteLine("tutorial actor swap: Stalact/Pelerin keep tutorial rows + plain actors OK");

    // Passive pack: stage the fifth slot, run the archetype roller end-to-end
    // and sweep the packed table (character rules + Lune melee-element gate).
    var passiveDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_randosmoke_passives");
    if (Directory.Exists(passiveDir)) Directory.Delete(passiveDir, true);
    Directory.CreateDirectory(passiveDir);
    var slotRows = E33ModEditor.Core.WeaponPassiveSlotKit.Apply(passiveDir);
    var ps = new E33ModEditor.Core.Loot.RandomizerKit.Settings
    {
        Seed = 42,
        Chests = false,
        EnemyDrops = false,
        Merchants = false,
        WeaponPassives = true,
        MonsterStats = false,
        WeaponStats = true,
    };
    var pn = E33ModEditor.Core.Loot.RandomizerKit.Apply(passiveDir, ps, catalog, null);
    var sweep = E33ModEditor.Core.Loot.RandomizerKit.SweepWeaponEligibility(passiveDir);
    Console.WriteLine($"passive pack slot-rows={slotRows} changed={pn} sweep-problems={sweep.Count}");
    if (sweep.Count > 0)
    {
        foreach (var p in sweep.Take(10)) Console.Error.WriteLine("  sweep: " + p);
        Console.Error.WriteLine("rando-smoke FAILED: randomizer pack violates weapon eligibility");
        return 1;
    }

    // Chests: the engine rolls one entry per SECTION, so a multi-drop chest has
    // to be several single-entry sections. Guard that shape — a regression here
    // silently collapses every multi-drop chest into one weighted pick.
    var chestDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_randosmoke_chests");
    if (Directory.Exists(chestDir)) Directory.Delete(chestDir, true);
    Directory.CreateDirectory(chestDir);
    // Stage the created pictos into the chest tree so the gear-banned area ban
    // is exercised against them too: a created picto such as Blighted Power III
    // grants Defense yet is absent from the vanilla-only DefensePictos list.
    DataTableEditor.ResetMappings();
    PictosCreateKit.CopyCreateTables(chestDir);
    PictosCreateKit.InjectPainterConversions(chestDir);
    PictosCreateKit.InjectStatusAffinities(chestDir);
    PictosMagnitudeKit.Inject(chestDir, copyTables: false);
    CutPictosKit.Apply(chestDir);
    PictoTagKit.Apply(chestDir);
    var cs = new E33ModEditor.Core.Loot.RandomizerKit.Settings
    {
        Seed = 42,
        Chests = true,
        EnemyDrops = false,
        Merchants = false,
        WeaponPassives = false,
        MonsterStats = false,
        WeaponStats = false,
    };
    E33ModEditor.Core.Loot.RandomizerKit.Apply(chestDir, cs, catalog, null);
    var chestPath = FileCopy.FindUnder(chestDir, "DT_ChestsContent.uasset")
        ?? throw new FileNotFoundException("mod chests");
    var chestJson = Newtonsoft.Json.Linq.JObject.Parse(new DataTableEditor(chestPath).Dump());
    int chests = 0, multiDrop = 0, badSection = 0, emptyChest = 0;
    foreach (var row in chestJson.Properties())
    {
        chests++;
        var secs = row.Value["LootTableSections"] as Newtonsoft.Json.Linq.JArray
            ?? new Newtonsoft.Json.Linq.JArray();
        if (secs.Count == 0) emptyChest++;
        else if (secs.Count > 1) multiDrop++;
        foreach (var sec in secs)
            if (((sec["LootEntries"] as Newtonsoft.Json.Linq.JArray)?.Count ?? 0) != 1)
                badSection++;
    }
    Console.WriteLine(
        $"chests={chests} multi-drop={multiDrop} sectionsWithoutOneEntry={badSection} empty={emptyChest}");
    if (badSection > 0 || emptyChest > 0 || multiDrop == 0)
    {
        Console.Error.WriteLine(
            "rando-smoke FAILED: chests must be single-entry sections (one drop each) and include multi-drop chests");
        return 1;
    }

    // Paint Cages are their own randomizer group and must always contain one
    // tier-appropriate pictos or weapon — never a shard, catalyst or chroma.
    {
        var cageIds = E33ModEditor.Core.Loot.LootCatalog.PaintCageChests;
        int missingRows = 0, badLoot = 0, badTier = 0;
        foreach (var id in cageIds)
        {
            var check = catalog.Checks.FirstOrDefault(c => c.Id == id
                && (c.Source == "Chest" || c.Source == E33ModEditor.Core.Loot.LootCatalog.PaintCageSource));
            if (check is null || check.Source != E33ModEditor.Core.Loot.LootCatalog.PaintCageSource)
            {
                missingRows++;
                Console.Error.WriteLine($"rando-smoke FAILED: {id} not classified as a Paint Cage");
                continue;
            }
            var row = chestJson[id] as Newtonsoft.Json.Linq.JObject;
            var secs = row?["LootTableSections"] as Newtonsoft.Json.Linq.JArray;
            var ents = secs is { Count: 1 }
                ? secs[0]?["LootEntries"] as Newtonsoft.Json.Linq.JArray
                : null;
            var item = ents is { Count: 1 } ? ents[0]?["ItemID"]?.ToString() ?? "" : "";
            bool InPoolAnyTier(string id, string type) =>
                Enumerable.Range(1, 5).Any(t => catalog.PoolFor(type, t).Contains(id));
            var isPictos = InPoolAnyTier(item, "Pictos");
            var isWeapon = InPoolAnyTier(item, "Weapon");
            if (row is null || secs is not { Count: 1 } || ents is not { Count: 1 }
                || (!isPictos && !isWeapon))
            {
                badLoot++;
                Console.Error.WriteLine($"rando-smoke FAILED: {id} does not hold exactly one pictos/weapon (got {item})");
                continue;
            }
            if (!catalog.PoolFor("Pictos", check.Tier).Contains(item)
                && !catalog.PoolFor("Weapon", check.Tier).Contains(item))
            {
                badTier++;
                Console.Error.WriteLine($"rando-smoke FAILED: {id} holds {item} outside T{check.Tier}");
            }
        }
        Chk(cageIds.Count == 21, $"Paint Cage catalog has 21 rows (got {cageIds.Count})");
        Chk(missingRows == 0 && badLoot == 0 && badTier == 0,
            "every Paint Cage rolls exactly one tier-appropriate pictos or weapon");
    }

    // The Paint Cages toggle is independent of Chests: turning Chests off must
    // still randomize only the cages, and turning Paint Cages off must leave
    // their vanilla rewards alone.
    {
        var vanillaChests = Newtonsoft.Json.Linq.JObject.Parse(
            File.ReadAllText(Path.Combine(EditorPaths.RepoRoot, "work", "chests.json")));
        Newtonsoft.Json.Linq.JToken? DumpChest(string dir, string rowId)
        {
            var path = FileCopy.FindUnder(dir, "DT_ChestsContent.uasset");
            if (path is null) return null;
            var json = Newtonsoft.Json.Linq.JObject.Parse(new DataTableEditor(path).Dump());
            return json[rowId];
        }

        var pcOnlyDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_randosmoke_paintcages");
        if (Directory.Exists(pcOnlyDir)) Directory.Delete(pcOnlyDir, true);
        Directory.CreateDirectory(pcOnlyDir);
        var pcSettings = new E33ModEditor.Core.Loot.RandomizerKit.Settings
        {
            Seed = 77, Chests = false, PaintCages = true, EnemyDrops = false,
            Merchants = false, WeaponPassives = false, MonsterStats = false, WeaponStats = false,
        };
        E33ModEditor.Core.Loot.RandomizerKit.Apply(pcOnlyDir, pcSettings, catalog, null);
        var pcRow = DumpChest(pcOnlyDir, "Chest_SeaCliff_5");
        var pcNormal = DumpChest(pcOnlyDir, "Chest_SpringMeadows_1");
        Chk(pcRow is not null && !Newtonsoft.Json.Linq.JToken.DeepEquals(
                pcRow, vanillaChests["Chest_SeaCliff_5"]),
            "Paint Cages alone randomize their reward rows");
        Chk(pcNormal is not null && Newtonsoft.Json.Linq.JToken.DeepEquals(
                pcNormal, vanillaChests["Chest_SpringMeadows_1"]),
            "Paint Cages alone leave normal chest rows vanilla");

        var chestOnlyDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_randosmoke_chestsonly");
        if (Directory.Exists(chestOnlyDir)) Directory.Delete(chestOnlyDir, true);
        Directory.CreateDirectory(chestOnlyDir);
        var coSettings = new E33ModEditor.Core.Loot.RandomizerKit.Settings
        {
            Seed = 77, Chests = true, PaintCages = false, EnemyDrops = false,
            Merchants = false, WeaponPassives = false, MonsterStats = false, WeaponStats = false,
        };
        E33ModEditor.Core.Loot.RandomizerKit.Apply(chestOnlyDir, coSettings, catalog, null);
        var coRow = DumpChest(chestOnlyDir, "Chest_SeaCliff_5");
        Chk(coRow is not null && Newtonsoft.Json.Linq.JToken.DeepEquals(
                coRow, vanillaChests["Chest_SeaCliff_5"]),
            "Chests alone leave Paint Cage rows vanilla");
    }

    // Sequence-break guard: Dark Shores / Frozen Hearts are reachable
    // without combat, so their chests must never yield weapons, catalysts,
    // or defense-carrying pictos (other pictos, chroma, lumina, tints,
    // shapes are fine).
    int bannedDrops = 0;
    // The empty/partial chest-only staging above refreshes DefensePictos from a
    // tree without DT_PictosDefinitions, which falls back to the vanilla table.
    // Re-derive from the full chest staging (created pictos included) before the
    // drift guard and the gear-banned chest check.
    E33ModEditor.Core.Loot.RandomizerKit.DefensePictos.Clear();
    E33ModEditor.Core.Loot.RandomizerKit.DefensePictos.UnionWith(
        E33ModEditor.Core.Loot.RandomizerKit.DefensePictosFrom(chestDir));
    var defensePictos = E33ModEditor.Core.Loot.RandomizerKit.DefensePictos;
    foreach (var row in chestJson.Properties())
    {
        if (E33ModEditor.Core.Loot.LootCatalog.PaintCageChests.Contains(row.Name))
            continue; // Paint Cages are a gear guarantee, not a reachable-without-combat chest
        if (row.Name.IndexOf("darkshores", StringComparison.OrdinalIgnoreCase) < 0
            && row.Name.IndexOf("frozenhearts", StringComparison.OrdinalIgnoreCase) < 0)
            continue;
        var secs2 = row.Value["LootTableSections"] as Newtonsoft.Json.Linq.JArray
            ?? new Newtonsoft.Json.Linq.JArray();
        foreach (var sec in secs2)
        {
            var ents = sec["LootEntries"] as Newtonsoft.Json.Linq.JArray
                ?? new Newtonsoft.Json.Linq.JArray();
            foreach (var ent in ents)
            {
                var item = ent["ItemID"]?.ToString() ?? "";
                if (item.StartsWith("UpgradeMaterial_", StringComparison.Ordinal)
                    || string.Equals(catalog.TypeOf(item), "Weapon", StringComparison.OrdinalIgnoreCase)
                    || defensePictos.Contains(item))
                {
                    Console.Error.WriteLine($"rando-smoke FAILED: gear-banned chest {row.Name} yields {item}");
                    bannedDrops++;
                }
            }
        }
    }
    Console.WriteLine($"gear-banned area chest violations={bannedDrops}");
    if (bannedDrops > 0) return 1;

    // Only packed items may be stocked: every chest entry must resolve to a row
    // in the staged DT_jRPG_Items. A created id whose kit did not run (e.g. a
    // magnitude variant with its toggle off) would otherwise be handed out as a
    // phantom entry that renders as a placeholder in-game.
    {
        var packed = E33ModEditor.Core.Loot.RandomizerKit.PackedItemIds(chestDir);
        int phantom = 0;
        foreach (var row in chestJson.Properties())
        {
            var secsP = row.Value["LootTableSections"] as Newtonsoft.Json.Linq.JArray
                ?? new Newtonsoft.Json.Linq.JArray();
            foreach (var sec in secsP)
            {
                var ents = sec["LootEntries"] as Newtonsoft.Json.Linq.JArray
                    ?? new Newtonsoft.Json.Linq.JArray();
                foreach (var ent in ents)
                {
                    var item = ent["ItemID"]?.ToString() ?? "";
                    if (item.Length == 0) continue;
                    // Pools only ever draw pictos/weapons, and those live in
                    // DT_jRPG_Items; chroma/consumables/quest items are not.
                    var type = catalog.TypeOf(item);
                    if (type is not ("Pictos" or "Weapon")) continue;
                    if (packed.Contains(item)) continue;
                    Console.Error.WriteLine($"rando-smoke FAILED: chest {row.Name} stocks unpacked {type} {item}");
                    phantom++;
                }
            }
        }
        Console.WriteLine($"phantom (unpacked) chest drops={phantom} packed={packed.Count}");
        if (phantom > 0) return 1;
        if (!packed.Contains("FirstOffensive2"))
        {
            Console.Error.WriteLine("rando-smoke FAILED: staged created picto FirstOffensive2 missing from DT_jRPG_Items");
            return 1;
        }
    }

    // Drift guard: the ban set must equal the derivation from the PACKED table
    // (now mod-aware, so created pictos are covered) — not just the vanilla copy.
    {
        var tableSet = E33ModEditor.Core.Loot.RandomizerKit.DefensePictosFrom(chestDir);
        var missing = tableSet.Where(i => !defensePictos.Contains(i)).ToList();
        var extra = defensePictos.Where(i => !tableSet.Contains(i)).ToList();
        Console.WriteLine($"defense set: kit={defensePictos.Count} table={tableSet.Count} " +
                          $"missing=[{string.Join(",", missing)}] extra=[{string.Join(",", extra)}]");
        if (missing.Count > 0 || extra.Count > 0) return 1;

        // Created defenders must be covered; offense-only powers must not be.
        foreach (var id in new[] { "BlightedPower3", "BlightedPower2", "BerserkPower1", "BerserkPower2" })
            if (!defensePictos.Contains(id))
            {
                Console.Error.WriteLine($"rando-smoke FAILED: created defender {id} missing from the chest-ban set");
                return 1;
            }
        foreach (var id in new[] { "CursedPower2", "CursedPower3", "BlightedPower1", "BerserkPower3" })
            if (defensePictos.Contains(id))
            {
                Console.Error.WriteLine($"rando-smoke FAILED: offense-only {id} wrongly banned");
                return 1;
            }
        Console.WriteLine("created defense pictos covered by the gear-banned chest ban");

        // Revived pictos that grant Defence must be chest-banned in Dark Shores /
        // Frozen Hearts too (the ban set is derived from the packed table).
        foreach (var id in new[] { "AntiDizzy", "AntiCurse", "AntiExhaust", "ReviveWithShell",
                                   "DefenseRiserDodge" })
            if (!defensePictos.Contains(id))
            {
                Console.Error.WriteLine($"rando-smoke FAILED: revived defender {id} not in the chest-ban set");
                return 1;
            }

        // Curated world-map encounter tiers: these fights mix enemy regions, so
        // every listed row must resolve to the curated fight tier instead of its
        // own region tier (otherwise one fight hands out several tiers).
        foreach (var (id, want) in E33ModEditor.Core.Loot.LootCatalog.EncounterTierByEnemy)
        {
            if (!catalog.Enemies.TryGetValue(id, out var info))
            {
                Console.Error.WriteLine($"rando-smoke FAILED: curated encounter row {id} is not a known enemy");
                return 1;
            }
            var rowTier = E33ModEditor.Core.Loot.LootCatalog.TierFromLevel(
                E33ModEditor.Core.Loot.LootCatalog.ResolveEnemyLevel(0, info));
            // Biome rows (Frozen Hearts / Act 3) keep their native remap tier and
            // intentionally ignore the curated mixed-region fight tier, so the
            // native area still hands out its own tier (T4 Frozen Hearts).
            var biome = E33ModEditor.Core.Loot.LootCatalog.IsFrozenHearts(info)
                || E33ModEditor.Core.Loot.LootCatalog.IsAct3Lumiere(info);
            var expected = biome ? rowTier : want;
            var got = catalog.AreaTierForEnemy(id, info, rowTier);
            if (got != expected)
            {
                Console.Error.WriteLine(biome
                    ? $"rando-smoke FAILED: biome row {id} resolves to T{got}, expected its remap T{expected}"
                    : $"rando-smoke FAILED: {id} resolves to T{got}, curated T{expected}");
                return 1;
            }
        }
        Console.WriteLine(
            $"curated world-map encounter tiers applied ({E33ModEditor.Core.Loot.LootCatalog.EncounterTierByEnemy.Count} enemy rows across {E33ModEditor.Core.Loot.LootCatalog.EncounterTiers.Count} fights)");
        foreach (var c in E33ModEditor.Core.Loot.LootCatalog.EncounterTierConflicts)
        {
            var detail = string.Join(", ", c.Votes.OrderBy(v => v.Tier)
                .Select(v => $"T{v.Tier} in {v.Encounter}"));
            Console.WriteLine($"  note: {c.Enemy} shared by curated fights ({detail}) — row tier T{c.Tier}");
        }
    }

    // Sticky-share floor: area shares are owner-only until the area's own
    // rows are done, so Dark Shores + Frozen Hearts (late in table order)
    // must see at least one pictos across their 39 chests — seed-42 staged
    // zero before the fix despite shares of ~7/~24.
    int banPictos = 0;
    foreach (var row in chestJson.Properties())
    {
        if (row.Name.IndexOf("darkshores", StringComparison.OrdinalIgnoreCase) < 0
            && row.Name.IndexOf("frozenhearts", StringComparison.OrdinalIgnoreCase) < 0)
            continue;
        var secs3 = row.Value["LootTableSections"] as Newtonsoft.Json.Linq.JArray
            ?? new Newtonsoft.Json.Linq.JArray();
        foreach (var sec in secs3)
        {
            var ents = sec["LootEntries"] as Newtonsoft.Json.Linq.JArray
                ?? new Newtonsoft.Json.Linq.JArray();
            foreach (var ent in ents)
                if (string.Equals(catalog.TypeOf(ent["ItemID"]?.ToString() ?? ""),
                    "Pictos", StringComparison.OrdinalIgnoreCase))
                    banPictos++;
        }
    }
    Console.WriteLine($"gear-banned area pictos={banPictos}");
    if (banPictos < 1)
    {
        Console.Error.WriteLine(
            "rando-smoke FAILED: Dark Shores / Frozen Hearts chests yielded zero pictos (sticky shares broken)");
        return 1;
    }

    var wep = FileCopy.FindUnder(dir, "DT_WeaponDefinitions.uasset")
        ?? throw new FileNotFoundException("mod weapons");
    var ene = FileCopy.FindUnder(dir, "DT_jRPG_Enemies.uasset")
        ?? throw new FileNotFoundException("mod enemies");
    var wepEd = new DataTableEditor(wep);
    var eneEd = new DataTableEditor(ene);

    void DumpWeapon(string id)
    {
        if (!wepEd.TryGetRow(id, out var row))
        {
            Console.WriteLine("MISSING " + id);
            return;
        }
        var d = wepEd.DumpRow(row);
        Console.WriteLine($"{id} atk={d["BaseStatsMultipliers"]?["E_jRPG_StatType::NewEnumerator3"]} aim={d["BaseStatsMultipliers"]?["E_jRPG_StatType::NewEnumerator13"]} dmg={d["BaseDamageType"]} free={d["FreeAimDamageType"]} chroma={d["ChromaValueMultiplier"]}");
    }
    void DumpEnemy(string id)
    {
        if (!eneEd.TryGetRow(id, out var row))
        {
            Console.WriteLine("MISSING " + id);
            return;
        }
        var d = eneEd.DumpRow(row);
        var sc = d["EnemyScaling"];
        Console.WriteLine($"{id} hp={sc?["HP"]} atk={sc?["PhysicalAttack"]} spd={sc?["Speed"]} xp={sc?["Experience"]} chroma={sc?["Chroma"]} isBoss={d["IsBoss"]}");
    }

    DumpWeapon("Noahram");
    DumpWeapon("Maellum");
    DumpWeapon("Lunerim");
    DumpEnemy("SM_Lancelier");
    DumpEnemy("SM_Boss_Eveque");
    DumpEnemy("OL_MirrorRenoir_FirstFight");
    DumpEnemy("MM_MirrorRenoir");
    DumpEnemy("MO_Boss_Paintress");
    DumpEnemy("L_Creation");
    DumpEnemy("L_Boss_Curator");
    DumpEnemy("L_LumiereCitizen_Fleuret_Female");
    DumpEnemy("MM_Gargant");
    DumpEnemy("MM_Danseuse_Clone");

    double ScaleOf(string id, string field)
    {
        if (!eneEd.TryGetRow(id, out var row)) return double.NaN;
        var sc = eneEd.DumpRow(row)["EnemyScaling"];
        var t = sc?[field];
        return t is null or { Type: Newtonsoft.Json.Linq.JTokenType.Null } ? double.NaN : t.ToObject<double>();
    }
    int LevelOf(string id)
    {
        if (!eneEd.TryGetRow(id, out var row)) return -1;
        var t = eneEd.DumpRow(row)["Level"];
        return t is null ? -1 : t.ToObject<int>();
    }
    bool Near(double got, double want, double tol = 0.02) =>
        !double.IsNaN(got) && Math.Abs(got - want) <= Math.Max(tol, Math.Abs(want) * 0.02);

    Chk(Near(ScaleOf("OL_MirrorRenoir_FirstFight", "HP"), 21), "Old Lumiere Renoir HP ×21");
    Chk(Near(ScaleOf("OL_MirrorRenoir_FirstFight", "Experience"), 21), "Old Lumiere Renoir XP ×21");
    Chk(Near(ScaleOf("OL_MirrorRenoir_FirstFight", "Chroma"), 21), "Old Lumiere Renoir Chroma ×21");
    Chk(Near(ScaleOf("MM_MirrorRenoir", "HP"), 12), "Monolith Renoir HP ×12");
    Chk(Near(ScaleOf("MO_Boss_Paintress", "HP"), 33_333_333.0 / 197_808.0),
        "Paintress HP pins to 33,333,333 effective (archetype[43] x scaling)");
    Chk(Near(ScaleOf("MO_Boss_Paintress", "Experience"), (33_333_333.0 / 197_808.0) / 1.3),
        "Paintress XP follows the HP factor");
    Chk(Near(ScaleOf("MO_Boss_Paintress", "Chroma"), (33_333_333.0 / 197_808.0) / 1.3),
        "Paintress Chroma follows the HP factor");
    Chk(Near(ScaleOf("MO_Boss_Paintress", "PhysicalDefense"), 975),
        "Paintress physical defense pins to 975");
    Chk(Near(ScaleOf("MO_Boss_Paintress", "MagicalDefense"), 975),
        "Paintress magical defense pins to 975");
    // Outgoing-damage multiplier: both story bosses triple their attack scaling
    // (band x vanilla x 3). Renoir T4 band 1.75-2.05 x vanilla 1.0; Paintress
    // T3 band 1.55-1.70 x vanilla PATK 1.2 / Mag 1.0.
    {
        var atk = ScaleOf("MM_MirrorRenoir", "PhysicalAttack");
        Chk(atk >= 5.25 - 0.05 && atk <= 6.15 + 0.05,
            $"Monolith Renoir damage tripled (got {atk:0.###})");
        var patk = ScaleOf("MO_Boss_Paintress", "PhysicalAttack");
        Chk(patk >= 3.72 - 0.05 && patk <= 4.08 + 0.05,
            $"Paintress physical damage doubled (got {patk:0.###})");
        var matk = ScaleOf("MO_Boss_Paintress", "MagicalAttack");
        Chk(matk >= 3.10 - 0.05 && matk <= 3.40 + 0.05,
            $"Paintress magic damage doubled (got {matk:0.###})");
    }
    // Act 3 finale trio: absolute effective pins (archetype[99] x staged scaling).
    // Respecced against the current L99 curves: specialist stat = 1.2× the max
    // curve, the other two = 0.66× (ATK 60k, SPD 15k, DEF 17,318,400).
    // Creation (defense specialist): 1.3B HP / 39.6k ATK / 9.9k SPD / 20.78M DEF.
    Chk(Near(ScaleOf("L_Creation", "HP"), 1_300_000_000.0 / 1_854_480.0), "Creation HP pins to 1.3B");
    Chk(Near(ScaleOf("L_Creation", "PhysicalAttack"), 39_600.0 / 8_280.0), "Creation attack pins to 39.6k");
    Chk(Near(ScaleOf("L_Creation", "Speed"), 9_900.0 / 3_248.0), "Creation speed pins to 9.9k");
    Chk(Near(ScaleOf("L_Creation", "PhysicalDefense"), 20_782_080.0), "Creation defense pins to 20.78M");
    Chk(Near(ScaleOf("L_Creation", "MagicalDefense"), 20_782_080.0), "Creation defense is element-agnostic");
    Chk(Near(ScaleOf("L_Creation", "Experience"), ScaleOf("L_Creation", "HP") / 2.0),
        "Creation XP follows the HP-relative factor");
    Chk(Near(ScaleOf("L_Creation", "Chroma"), ScaleOf("L_Creation", "HP") / 2.0),
        "Creation Chroma follows the HP-relative factor");
    // Renoir (attack specialist): 2B HP / 72k ATK / 9.9k SPD / 11.43M DEF.
    Chk(Near(ScaleOf("L_Boss_Curator", "HP"), 2_000_000_000.0 / 4_018_040.0), "Act 3 Renoir HP pins to 2B");
    Chk(Near(ScaleOf("L_Boss_Curator", "PhysicalAttack"), 72_000.0 / 8_280.0), "Act 3 Renoir attack pins to 72k");
    Chk(Near(ScaleOf("L_Boss_Curator", "Speed"), 9_900.0 / 3_248.0), "Act 3 Renoir speed pins to 9.9k");
    Chk(Near(ScaleOf("L_Boss_Curator", "PhysicalDefense"), 11_430_144.0), "Act 3 Renoir defense pins to 11.43M");
    Chk(Near(ScaleOf("L_Boss_Curator", "MagicalDefense"), 11_430_144.0), "Act 3 Renoir defense is element-agnostic");
    Chk(Near(ScaleOf("L_Boss_Curator", "Experience"), ScaleOf("L_Boss_Curator", "HP") / 1.5),
        "Act 3 Renoir XP follows the HP-relative factor");
    Chk(Near(ScaleOf("L_Boss_Curator", "Chroma"), ScaleOf("L_Boss_Curator", "HP") / 1.5),
        "Act 3 Renoir Chroma follows the HP-relative factor");
    // Echassier (speed specialist): 900M HP / 39.6k ATK / 18k SPD / 11.43M DEF.
    // ATK/SPD/DEF scale to the level-99 curves (HP pin stays derived at the
    // true spawn level 48).
    Chk(Near(ScaleOf("LU_EchassierAlpha", "HP"), 900_000_000.0 / 272_454.0), "Echassier HP pins to 900M");
    Chk(Near(ScaleOf("LU_EchassierAlpha", "PhysicalAttack"), 39_600.0 / 3_058.0), "Echassier attack pins to 39.6k");
    Chk(Near(ScaleOf("LU_EchassierAlpha", "Speed"), 18_000.0 / 1_451.0), "Echassier speed pins to 18k");
    Chk(Near(ScaleOf("LU_EchassierAlpha", "PhysicalDefense"), 11_430_144.0), "Echassier defense pins to 11.43M");
    Chk(Near(ScaleOf("LU_EchassierAlpha", "MagicalDefense"), 11_430_144.0), "Echassier defense is element-agnostic");
    Chk(Near(ScaleOf("LU_EchassierAlpha", "Experience"), ScaleOf("LU_EchassierAlpha", "HP")),
        "Echassier XP follows the HP-relative factor");
    Chk(Near(ScaleOf("LU_EchassierAlpha", "Chroma"), ScaleOf("LU_EchassierAlpha", "HP")),
        "Echassier Chroma follows the HP-relative factor");
    Chk(LevelOf("LU_EchassierAlpha") == 48, "Echassier DT Level matches its spawn level");
    Chk(LevelOf("L_Boss_Curator") == 46, "Act 3 Renoir DT Level unchanged");
    Chk(E33ModEditor.Core.Loot.StoryLevelKit.Overrides.TryGetValue("TowerBattle_32", out var towerLevel)
        && towerLevel == 99, "Creation Tower floor is pinned to L99");
    Chk(E33ModEditor.Core.Loot.StoryLevelKit.Overrides.TryGetValue("LU_EchassierAlpha*1", out var echassierLevel)
        && echassierLevel == 48, "Echassier encounter is pinned to its L48 spawn level");
    Chk(LevelOf("QUEST_BertrandBigHands") == 15, "arena Bertrand table Level 60→15");
    Chk(LevelOf("QUEST_DominiqueGiantFeet") == 15, "arena Dominique table Level 60→15");
    Chk(LevelOf("QUEST_JulienTinyHead") == 15, "arena Julien table Level 60→15");
    Chk(LevelOf("QUEST_MatthieuTheColossus") == 15, "arena Matthieu table Level 60→15");
    Chk(LevelOf("GV_GestralScielTuto") == 15, "arena Sciel tuto table Level 60→15");
    Chk(LevelOf("GV_Sciel_Arena") == 15, "arena Sciel table Level 60→15");
    Chk(LevelOf("GA_GestralChad") == 15, "arena Chad table Level 35→15");
    Chk(LevelOf("GA_GestralSumo") == 65, "dark Sumo table Level 35→65");
    Chk(LevelOf("GA_GestralSkinny") == 65, "dark Skinny table Level 35→65");
    Chk(LevelOf("GA_GestralBuff") == 65, "dark Buff table Level 35→65");
    Chk(LevelOf("GA_GestralTall") == 65, "dark Tall table Level 35→65");
    Chk(LevelOf("GA_GestralBaby") == 65, "dark Baby table Level 35→65");
    Chk(LevelOf("GV_Golgra") == 80, "village Golgra stays 80");
    Chk(LevelOf("QUEST_Golgra_DarkArena") == 81, "dark Golgra table Level 48→81");
    Chk(LevelOf("Quest_GestralSumo9999") == 100, "Ono Puncho table Level 0→100");
    {
        // Ono Puncho is the 9,999-damage quest target: speed and both defenses
        // are absolute 500 pins, while HP/attack keep the T5 boss curve.
        var onoSpeed = ScaleOf("Quest_GestralSumo9999", "Speed")
            * E33ModEditor.Core.Loot.EnemyEffectiveStats.ArchetypeStat("Regular", 100, "Speed");
        Chk(Near(onoSpeed, 500, 5), $"Ono Puncho effective speed pins to 500 (got {onoSpeed:0.###})");
        Chk(Near(ScaleOf("Quest_GestralSumo9999", "PhysicalDefense"), 500, 5),
            "Ono Puncho physical defense pins to 500");
        Chk(Near(ScaleOf("Quest_GestralSumo9999", "MagicalDefense"), 500, 5),
            "Ono Puncho magical defense pins to 500");
    }
    {
        var hp = ScaleOf("L_LumiereCitizen_Fleuret_Female", "HP");
        var xp = ScaleOf("L_LumiereCitizen_Fleuret_Female", "Experience");
        var chr = ScaleOf("L_LumiereCitizen_Fleuret_Female", "Chroma");
        var hpLift = 309080.0 / 20958.0;
        Chk(hp >= hpLift * 1.5 - 0.05 && hp <= hpLift * 1.8 + 0.05,
            "Act 3 citizen HP is L99 bake × T5 band");
        Chk(Near(xp, 247500.0 / 18300.0), "Act 3 citizen XP is L99 bake (not banded)");
        Chk(Near(chr, 2522.0 / 980.0), "Act 3 citizen chroma is L99 bake");
    }
    {
        var hp = ScaleOf("MM_Gargant", "HP");
        var xp = ScaleOf("MM_Gargant", "Experience");
        var t4Boss = E33ModEditor.Core.Loot.RandomizerKit.DefaultBossHp[3];
        // T4/T5 bosses now take the HP curve (level 61) instead of the bake × band.
        var wantHp = E33ModEditor.Core.Loot.RandomizerKit.CurveHp(61)
            / E33ModEditor.Core.Loot.EnemyEffectiveStats.ArchetypeHp("Boss", 61);
        Chk(Near(hp, wantHp), "Frozen Hearts Gargant HP uses the boss curve (L61)");
        var wantXp = E33ModEditor.Core.Loot.RandomizerKit.RewardLevels(61) * 33_333_333.0
            / E33ModEditor.Core.Loot.EnemyEffectiveStats.ArchetypeStat("Boss", 61, "Experience");
        Chk(Near(xp, wantXp), "Frozen Hearts Gargant XP follows the level-linked reward rule");
    }
    Chk(Near(ScaleOf("MM_Danseuse_Clone", "Experience"), 0), "Danseuse clone XP stays 0");
    Chk(Near(ScaleOf("SM_Boss_Eveque", "Experience"), 1.5), "Eveque XP follows T1 boss formula");
    // White-haired Man (scripted story fight) is frozen out of the randomizer:
    // stats stay vanilla. (His fixed 1× Grandiose drop is stamped by the
    // EnemyDrops pass, which this smoke disables; verified via staged dump.)
    Chk(Near(ScaleOf("SC_MirrorRenoir_GustaveEnd", "HP"), 500), "White-haired Man stats stay vanilla");

    // Sirene boss: 30% above the Visages Axon's ×21 → ×27.3 on HP/XP/Chroma,
    // plus the flat +11% on the rolled T3 combat bands (attack band 1.55-1.70
    // × 1.11 = 1.72-1.89).
    Chk(Near(ScaleOf("SI_Axon_Sirene", "HP"), 1.2 * 27.3), "Sirene boss HP ×27.3 on vanilla 1.2");
    Chk(Near(ScaleOf("SI_Axon_Sirene", "Experience"), 27.3), "Sirene boss XP ×27.3");
    Chk(Near(ScaleOf("SI_Axon_Sirene", "Chroma"), 27.3), "Sirene boss Chroma ×27.3");
    {
        var atk = ScaleOf("SI_Axon_Sirene", "PhysicalAttack");
        var spd = ScaleOf("SI_Axon_Sirene", "Speed");
        Chk(atk >= 1.72 - 0.01 && atk <= 1.89 + 0.01, "Sirene boss attack is T3 attack band ×1.11");
        // Speed takes the T3 stat band (1.10-1.25) × the +11% Sirene bonus and
        // then the universal boss ×2 → 2.44-2.78.
        Chk(spd >= 1.10 * 1.11 * 2.0 - 0.01 && spd <= 1.25 * 1.11 * 2.0 + 0.01,
            "Sirene boss speed is T3 stat band ×1.11 × boss ×2");
    }

    // Dualliste family: the story fight is LR with two full bars at absolute
    // HP 8.0 (~404.6K each, ~809K total). The separate P1/P2 presentation rows
    // use the same 8.0 per-bar pin with flat vanilla ×12 XP/Chroma; the R/L
    // elite solos keep the ×8 base with the normal T2 boss factor on top and
    // payouts keeping the ×8 rate.
    Chk(Near(ScaleOf("FB_Dualliste_Phase1", "HP"), 8.0), "Dualliste P1 HP absolute 8.0 (story phase 1)");
    Chk(Near(ScaleOf("FB_Dualliste_Phase2", "HP"), 8.0), "Dualliste P2 HP absolute 8.0 (story phase 2)");
    // ×8 base × T2 boss factor (2.5).
    Chk(Near(ScaleOf("FB_DuallisteR", "HP"), 20.0), "Dualliste R HP ×8 × T2 boss factor");
    Chk(Near(ScaleOf("FB_DuallisteL", "HP"), 20.0), "Dualliste L HP ×8 × T2 boss factor");
    Chk(Near(ScaleOf("FB_DuallisteLR", "HP"), 8.0), "Dualliste LR HP absolute 8.0 per bar (two-bar story fight ~809K)");
    Chk(Near(ScaleOf("FB_DuallisteLR", "HP") * 2, 16.0), "Dualliste LR two-phase total absolute 16.0 (~809K)");
    foreach (var duel in new[] { "FB_DuallisteR", "FB_DuallisteL", "FB_DuallisteLR", "FB_Dualliste_Phase1", "FB_Dualliste_Phase2" })
    {
        var atk = ScaleOf(duel, "PhysicalAttack");
        if (duel is "FB_Dualliste_Phase1" or "FB_Dualliste_Phase2")
            Chk(atk >= 1.34 && atk <= 1.51, $"Dualliste {duel} attacks keep T2 band");
        else
            Chk(Near(atk, 5.0), $"Dualliste {duel} attacks flat ×5");
        if (duel is "FB_Dualliste_Phase1")
        {
            // Story phase payout: flat vanilla ×12 battle rewards.
            Chk(Near(ScaleOf(duel, "Experience"), 0.4 * 12), $"Dualliste {duel} XP flat vanilla ×12");
            Chk(Near(ScaleOf(duel, "Chroma"), 12.0), $"Dualliste {duel} Chroma flat vanilla ×12");
        }
        else if (duel is "FB_Dualliste_Phase2")
        {
            Chk(Near(ScaleOf(duel, "Experience"), 12.0), $"Dualliste {duel} XP flat vanilla ×12");
            Chk(Near(ScaleOf(duel, "Chroma"), 12.0), $"Dualliste {duel} Chroma flat vanilla ×12");
        }
        else Chk(Near(ScaleOf(duel, "Experience"), 1 + 8.0 / 3), $"Dualliste {duel} XP follows ×8 rate");
    }

    // ── gear spread: each tier's pictos split across areas by drop points ──
    var spread = NewLootCatalogWithExtras();
    spread.Rebuild(
        Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(EditorPaths.RepoRoot, "work", "chests.json"))),
        Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(EditorPaths.RepoRoot, "work", "enemies.json"))),
        Array.Empty<(string, Newtonsoft.Json.Linq.JObject)>());
    var spreadPlacement = new E33ModEditor.Core.Loot.RandomizerKit.LootPlacement();
    spreadPlacement.AllocateByArea(spread, new Random(12345));
    {
        var points = new Dictionary<(int Tier, string Area), int>();
        foreach (var c in spread.Checks)
        {
            if (c.Source == "Merchant" || c.AreaKey.Length == 0) continue;
            var k = (Math.Clamp(c.Tier, 1, 5), c.AreaKey);
            points[k] = points.GetValueOrDefault(k) + 1;
        }
        int tiers = 0, areas = 0, bad = 0;
        for (int t = 1; t <= 5; t++)
        {
            var tierAreas = points.Where(kv => kv.Key.Tier == t).ToList();
            if (tierAreas.Count == 0) continue;
            tiers++;
            var totalPoints = tierAreas.Sum(kv => kv.Value);
            var totalAlloc = tierAreas.Sum(kv =>
                spreadPlacement.Allocation.GetValueOrDefault(("Pictos", t, kv.Key.Area)));
            if (totalAlloc == 0) { bad++; Console.WriteLine($"SPREAD-EMPTY T{t}"); continue; }
            var k2 = (double)totalAlloc / totalPoints;
            foreach (var (key, w) in tierAreas)
            {
                var got = spreadPlacement.Allocation.GetValueOrDefault(("Pictos", t, key.Area));
                areas++;
                if (Math.Abs(got - k2 * w) >= 1.0)
                {
                    bad++;
                    Console.WriteLine($"SPREAD-DEVIATION T{t} {key.Area} got={got} points={w} want={k2 * w:0.00}");
                }
            }
        }
        Console.WriteLine($"spread: tiers={tiers} areas={areas} deviations={bad}");
        Chk(tiers > 0 && bad == 0,
            "loot spread: each tier's pictos allocated across areas in proportion to drop points");
    }

    // ── the table's IsBoss flag counts as a boss, scoped to real fights ──
    {
        var eneTable = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), "DT_jRPG_Enemies.uasset");
        if (eneTable is null) Console.WriteLine("boss-scope: vanilla enemies table not found");
        else
        {
            var reserved = E33ModEditor.Core.Loot.RandomizerKit.ReserveBossGear(
                eneTable, spread, new Random(7), new E33ModEditor.Core.Loot.RandomizerKit.LootPlacement());
            Console.WriteLine($"boss-scope: reserved rows={reserved.Count}");
            Chk(reserved.ContainsKey("QUEST_FrancoisDuel"), "IsBoss Strong row (Francois duel) is reserved");
            Chk(!reserved.ContainsKey("L_MaelleTutorial"), "IsBoss tutorial regular is not a boss");
            Chk(!reserved.ContainsKey("Petank_Red"), "Petank rows are not reserved (own drop branch)");
        }
    }

    // Arena / chromatic variants share a DT Level with the template (or an
    // inflated boss number). Loot tiers must follow the per-fight level.
    {
        int TierOf(string id)
        {
            var c = spread.Checks.FirstOrDefault(x => x.Id == id);
            return c?.Tier ?? -1;
        }
        Chk(TierOf("GV_Golgra") == 1, "Gestral Village Golgra is T1 (not table level 80 / T5)");
        Chk(TierOf("QUEST_MatthieuTheColossus") == 1, "Gestral Village arena boss is T1 (not table level 60)");
        Chk(TierOf("QUEST_BertrandBigHands") == 1, "Gestral Village Bertrand is T1");
        Chk(TierOf("GA_GestralChad") == 1, "Gestral Village warrior is T1");
        Chk(TierOf("QUEST_Golgra_DarkArena") == 4, "Dark Gestral Arena Golgra stays T4");
        Chk(TierOf("SM_Portier_ALPHA") == 5, "Endless Tower chromatic Portier stays T5");
        Chk(TierOf("YF_Chevaliere_A_Alpha") == 5, "Endless Tower chromatic Ceramic Chevaliere stays T5");
        Chk(TierOf("L_LumiereCitizen_Fleuret_Female") == 5, "Act 3 Lumiere citizen is T5");
        Chk(TierOf("L_Boss_Curator") == 5, "Act 3 Renoir is T5 loot");
        Chk(TierOf("L_Creation") == 5, "Creation is T5 loot");
        Chk(TierOf("MM_Gargant") == 4, "Frozen Hearts Gargant stays T4 (lv 77)");
        Chk(TierOf("MM_Pelerin") == 4, "Frozen Hearts Pelerin stays T4 (lv 74)");
        Chk(spread.Checks.FirstOrDefault(x => x.Id == "Chest_Lumiere_1")?.Tier == 5,
            "Act 3 Lumiere chests are T5");
        Chk(spread.Checks.FirstOrDefault(x => x.Id == "Chest_FrozenHearts_1")?.Tier == 4,
            "Frozen Hearts chests stay T4");
        Chk(spread.AreaTierForEnemy("DS_Noire", spread.Enemies.GetValueOrDefault("DS_Noire"), 3) == 4,
            "Dark Shores Noire keeps T4 (released from the T2 world-map fight)");
        Chk(spread.AreaTierForEnemy("RE_Veilleur", spread.Enemies.GetValueOrDefault("RE_Veilleur"), 3) == 4,
            "The Reacher Veilleur keeps T4 (released from the T2 world-map fight)");
        Chk(spread.AreaTierForEnemy("RE_Echassier", spread.Enemies.GetValueOrDefault("RE_Echassier"), 3) == 4,
            "The Reacher Echassier keeps T4");
        Chk(E33ModEditor.Core.Loot.LootCatalog.MerchantTiers["DT_Merchant_Lumiere"] == 5,
            "Cribappa shop is T5");
        Chk(E33ModEditor.Core.Loot.LootCatalog.MerchantTiers["DT_Merchant_MonocosMountain"] == 4,
            "Melosh shop is T4");
        Chk(spread.AreaKeyFor("GA_GestralSumo") == "darkgestralarena", "Dark arena Sumo buckets to darkgestralarena");
        Chk(spread.AreaKeyFor("QUEST_MatthieuTheColossus") == "gestralvillage", "Village Matthieu buckets to gestralvillage");
        Chk(spread.AreaKeyFor("GV_Sciel_Arena") == "gestralvillage", "Sciel arena retargets from chromazone to gestralvillage");
        Chk(spread.AreaTierForEnemy("GA_GestralSumo", spread.Enemies.GetValueOrDefault("GA_GestralSumo"), 4, true) == 4,
            "Dark arena Sumo keeps T4 by area");
        Chk(spread.AreaTierForEnemy("GA_GestralChad", spread.Enemies.GetValueOrDefault("GA_GestralChad"), 1) == 1,
            "Village Chad drops T1 by area");
        Chk(spread.AreaTierForEnemy("Quest_GestralSumo9999", spread.Enemies.GetValueOrDefault("Quest_GestralSumo9999"), 5) == 5,
            "Ono Puncho keeps T5 override in village");
        Chk(spread.AreaTierForEnemy("SC_CultistFlying_ALPHA", spread.Enemies.GetValueOrDefault("SC_CultistFlying_ALPHA"), 2, true) == 3,
            "Chromatic Reaper Cultist pinned to T3 (world map)");
        Chk(spread.AreaTierForEnemy("MF_Chapelier_MaskSadness", spread.Enemies.GetValueOrDefault("MF_Chapelier_MaskSadness"), 2, true) == 3,
            "Visages mask trio pinned to T3");
        Chk(spread.AreaTierForEnemy("MF_Axon_MaskKeeper_VisagesPhase2", spread.Enemies.GetValueOrDefault("MF_Axon_MaskKeeper_VisagesPhase2"), 4, true) == 3,
            "Visages Mask Keeper pinned to T3");
        Chk(!(spread.Enemies.GetValueOrDefault("MF_Axon_MaskKeeper_VisagesPhase2")?.CutContent ?? false),
            "Mask Keeper row is not cut content (makes it eligible for stats + drops)");
        // The Visages island's chromatic BOSSES are fought in T4/T5 endgame areas,
        // so they stay unpinned and take the boss curve at their own tier (the
        // T3 pin above covers the island mobs only). Without this they keep the
        // old T3 treatment and never reach the curve.
        // Grosse Tete (world map) is the MF_ exception going the other way:
        // tagged L55 (mid-T3 by row tier — the T3/T4 boundary is 60) but pinned
        // The native MF_ row stays T4, but the world-map venue clone is T3:
        // dodge-only farming must not pay a T4 package. NOTE: pass the catalog
        // row tier here, never a hardcoded one — areaLevel 0 short-circuits
        // AreaTierForEnemy straight to rowTier.
        {
            var tete = spread.Checks.FirstOrDefault(x => x.Id == "MF_GrossTete");
            Chk(tete is not null
                && spread.AreaTierForEnemy("MF_GrossTete", spread.Enemies.GetValueOrDefault("MF_GrossTete"), tete.Tier, tete.IsBoss) == 4,
                "Native Grosse Tete keeps T4 (boss curve)");
            spread.RegisterEnemy("BV_MF_GrossTete_50", new E33ModEditor.Core.Loot.LootCatalog.EnemyInfo
            {
                DisplayName = "MF_GrossTete",
                Archetype = "Elite",
                Level = 50,
            });
            Chk(spread.AreaTierForEnemy("BV_MF_GrossTete_50",
                    spread.Enemies.GetValueOrDefault("BV_MF_GrossTete_50"),
                    E33ModEditor.Core.Loot.LootCatalog.TierFromLevel(50), true) == 3,
                "World-map Grosse Tete venue clone is T3");
        }
        Chk(spread.AreaTierForEnemy("MF_Boucheclier_ALPHA", spread.Enemies.GetValueOrDefault("MF_Boucheclier_ALPHA"), 4, true) == 4,
            "Isle of the Eyes chromatic Boucheclier keeps T4 (boss curve)");
        Chk(spread.AreaTierForEnemy("MF_Chapelier_ALPHA", spread.Enemies.GetValueOrDefault("MF_Chapelier_ALPHA"), 4, true) == 4,
            "The Crows chromatic Chapelier keeps T4 (boss curve)");
        Chk(spread.AreaTierForEnemy("MF_Moissonneuse_ALPHA", spread.Enemies.GetValueOrDefault("MF_Moissonneuse_ALPHA"), 5, true) == 5,
            "Endless Tower chromatic Moissonneuse keeps T5 (boss curve)");
        // ...and the curve gate needs a boss row, so the archetype has to stay Alpha.
        foreach (var id in new[] { "MF_Boucheclier_ALPHA", "MF_Chapelier_ALPHA", "MF_Moissonneuse_ALPHA" })
        {
            var arch = spread.Enemies.GetValueOrDefault(id)?.Archetype ?? "";
            Chk(arch == "Alpha", $"{id} is still an Alpha archetype (curve gate needs a boss row)");
        }
        Chk(E33ModEditor.Core.Loot.LootCatalog.MerchantTiers["DT_Merchant_Visages"] == 3,
            "Blooraga (Visages) shop is T3");
        // Sirene arena is T3: chests come off the lifted area level, monsters
        // off the explicit AreaTierOverrides row list (the SI_ rows bucket to
        // several actor folders, so this also covers the boss and the rows
        // released from the curated world-map encounters).
        Chk(TierOf("Chest_Sirene_1") == 3, "Sirene chests are T3");
        Chk(spread.AreaTierForEnemy("SI_Axon_Sirene", spread.Enemies.GetValueOrDefault("SI_Axon_Sirene"), 2, true) == 3,
            "Sirene boss pinned to T3");
        Chk(spread.AreaTierForEnemy("SI_Ballet", spread.Enemies.GetValueOrDefault("SI_Ballet"), 2) == 3,
            "Sirene Ballet pinned to T3");
        Chk(spread.AreaTierForEnemy("SI_Benisseur", spread.Enemies.GetValueOrDefault("SI_Benisseur"), 2) == 3,
            "Sirene Benisseur pinned to T3 (released from curated encounters)");
        Chk(spread.AreaTierForEnemy("SI_Glissando", spread.Enemies.GetValueOrDefault("SI_Glissando"), 2) == 3,
            "Sirene Glissando pinned to T3");
    }

    // ── T4/T5 area bands: chests, shops and region enemies stay on tier ──
    // Guards the retuned areas against the actor-folder fallback handing a
    // region row (or a shop) a lower/higher band. Chests key off the area
    // bucket; shops are mapped explicitly (their stems don't encode the area);
    // region rows living in a shared actor folder are pinned in
    // AreaTierOverrides. Any new drift here is a regression.
    {
        var chestAreas = new (string Name, string Key, int Tier)[]
        {
            ("Red Woods", "smalllevelredwoods", 4),
            ("Sacred River", "smalllevelsacredriver", 4),
            ("Sky Island", "smalllevelfloatingisland", 4),
            ("The Chosen Path", "smalllevelchosenpath", 4),
            ("The Reacher", "smalllevelreacher", 4),
            ("Isle of the Eyes", "smalllevelvisages", 4),
            ("The Crows", "smalllevelrocktrailing", 4),
            ("Crimson Forest", "crimsonforest", 4),
            ("Dark Shores", "darkshores", 4),
            ("Endless Night Sanctuary", "twilightsanctuary", 4),
            ("Frozen Hearts", "frozenhearts", 4),
            ("Flying Manor", "flyingmanor", 5),
            ("Verso's Drafts", "sidelevelversosdraft", 5),
            ("Renoir's Drafts", "renoirsdraft", 5),
        };
        foreach (var (name, key, tier) in chestAreas)
        {
            var rows = spread.Checks.Where(c => c.Source == "Chest" && c.AreaKey == key).ToList();
            Chk(rows.Count > 0 && rows.All(c => c.Tier == tier),
                $"{name} chests are T{tier} ({rows.Count} rows)");
        }

        var shopAreas = new (string Stem, string Where, int Tier)[]
        {
            ("DT_Merchant_Reacher", "The Reacher", 4),
            ("DT_Merchant_GestralVillage3", "Sacred River", 4),
            ("DT_Merchant_TwilightSanctuary", "Endless Night Sanctuary", 4),
            ("DT_Merchant_FH_Custo_Danseuse", "Frozen Hearts", 4),
            ("DT_Merchant_MonocosMountain", "Frozen Hearts", 4),
            ("DT_Merchant_CleaIsland", "Flying Manor", 5),
            ("DT_Merchant_Optional3", "Renoir's Drafts", 5),
            ("DT_Merchant_VersosDraft", "Verso's Drafts", 5),
            ("DT_Merchant_Lumiere", "Lumiere - Act 3", 5),
        };
        foreach (var (stem, where, tier) in shopAreas)
            Chk(E33ModEditor.Core.Loot.LootCatalog.MerchantTiers.GetValueOrDefault(stem, 0) == tier,
                $"{stem} shop matches its {where} T{tier} band");

        var pinned = new (string Id, int Tier, string Where)[]
        {
            ("FC_ChalierHammer", 4, "Flying Cemetery"),
            ("FC_ChalierSpear", 4, "Flying Cemetery"),
            ("FC_ChalierSword", 4, "Flying Cemetery"),
            ("RD_Aberration", 5, "Renoir's Drafts"),
            ("RD_Ballet", 5, "Renoir's Drafts"),
            ("RD_Benisseur", 5, "Renoir's Drafts"),
            ("RD_Echassier", 5, "Renoir's Drafts"),
            ("RD_OrphelinHammer", 5, "Renoir's Drafts"),
            ("RD_OrphelinSpoon", 5, "Renoir's Drafts"),
            ("RD_Veilleur", 5, "Renoir's Drafts"),
            ("TS_PotatoBag_Mage_Twilight", 4, "Endless Night Sanctuary"),
            ("TS_PotatoBag_Ranger_Twilight", 4, "Endless Night Sanctuary"),
            ("TS_PotatoBag_Tank_Twilight", 4, "Endless Night Sanctuary"),
            ("YF_Chevaliere_C", 4, "Crimson Forest"),
            ("YF_Chevaliere_C_Alpha", 4, "Crimson Forest"),
        };
        foreach (var (id, tier, where) in pinned)
        {
            var c = spread.Checks.FirstOrDefault(x => x.Id == id);
            Chk(c is not null
                && spread.AreaTierForEnemy(id, spread.Enemies.GetValueOrDefault(id), c.Tier, c.IsBoss) == tier,
                $"{id} keeps its {where} T{tier} band");
        }

        // Crimson Forest is a T4 area: its Gold Chevaliere row was released from
        // the curated world-map fights (WorldMapVariantKit carries them instead)
        // so the AreaTierOverrides pin above is what the tier check sees.
        Chk(!E33ModEditor.Core.Loot.LootCatalog.EncounterTierByEnemy.ContainsKey("YF_Chevaliere_C"),
            "Crimson Forest Chevaliere released from curated world-map fights");
        var cfChests = spread.Checks.Where(c => c.Source == "Chest" && c.AreaKey == "crimsonforest").ToList();
        Chk(cfChests.Count > 0 && cfChests.All(c => c.AreaLevel >= 60),
            $"Crimson Forest chests lifted to the T4 band (got {cfChests.Count} rows)");
    }

    // ── shops: guaranteed pictos + weapons, never chroma, clothes untouched ──
    {
        var mDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_randosmoke_shop");
        if (Directory.Exists(mDir)) Directory.Delete(mDir, true);
        Directory.CreateDirectory(mDir);
        var ms = new E33ModEditor.Core.Loot.RandomizerKit.Settings
        {
            Seed = 4242, Chests = false, EnemyDrops = false, Merchants = true,
            WeaponPassives = false, MonsterStats = false, WeaponStats = false,
        };
        E33ModEditor.Core.Loot.RandomizerKit.Apply(mDir, ms, spread, null);

        int shops = 0, noWeapon = 0, noPictos = 0, chroma = 0, clothesChecked = 0, clothesDirty = 0;
        int outOfRange = 0, badPrice = 0, badLevel = 0;
        int missingCatalyst = 0, badCatalystQty = 0, badCatalystTier = 0, t5Catalyst = 0;
        var gearSizes = new SortedDictionary<int, int>();
        foreach (var f in Directory.EnumerateFiles(mDir, "DT_Merchant_*.uasset", SearchOption.AllDirectories))
        {
            var ids = new DataTableEditor(f).Table.Table.Data
                .Select(r => DataTableEditor.CleanName(r.Name)).ToList();
            if (ids.Count == 0) continue;
            var isClothes = ids.All(i => spread.IsProtected(i)
                || spread.Describe(i).Type == "CharacterCustomization");
            if (isClothes)
            {
                clothesChecked++;
                var vanillaPath = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), Path.GetFileName(f));
                if (vanillaPath is not null)
                {
                    var vids = new DataTableEditor(vanillaPath).Table.Table.Data
                        .Select(r => DataTableEditor.CleanName(r.Name)).ToList();
                    if (!vids.SequenceEqual(ids)) clothesDirty++;
                }
                continue;
            }
            shops++;
            var shopJson = Newtonsoft.Json.Linq.JObject.Parse(new DataTableEditor(f).Dump());
            foreach (var prop in shopJson.Properties())
            {
                // Kept (protected) rows preserve their own sale terms; every
                // rebuilt row must sell at base price and natural level —
                // the row-0 template's terms must never leak into new stock
                // (Kasumi's 5000-chroma skin priced her whole shop at 5000).
                if (spread.IsProtected(prop.Name)) continue;
                if (prop.Value["PriceOverride"]?.ToString() is string p && p != "-1")
                {
                    Console.Error.WriteLine($"rando-smoke FAILED: {Path.GetFileName(f)}:{prop.Name} PriceOverride={p}");
                    badPrice++;
                }
                if (prop.Value["LevelOverride"]?.ToString() is string l && l != "-1")
                {
                    Console.Error.WriteLine($"rando-smoke FAILED: {Path.GetFileName(f)}:{prop.Name} LevelOverride={l}");
                    badLevel++;
                }
            }
            var gear = ids.Count(i => spread.Describe(i).Type is "Pictos" or "Weapon");
            gearSizes[gear] = gearSizes.GetValueOrDefault(gear) + 1;
            if (gear < E33ModEditor.Core.Loot.RandomizerKit.MinShopGear
                || gear > E33ModEditor.Core.Loot.RandomizerKit.MaxShopGear) outOfRange++;
            if (!ids.Any(i => spread.Describe(i).Type == "Weapon")) noWeapon++;
            if (!ids.Any(i => spread.Describe(i).Type == "Pictos")) noPictos++;
            if (ids.Any(i => i.StartsWith("ChromaPack_", StringComparison.Ordinal))) chroma++;

            // Catalysts: the shop tier's own catalyst is a x33 convenience,
            // every lower catalyst is bulk stock (x999). T5 shops never sell
            // L5, so their L1–L4 are all below the tier and get the bulk count.
            var stem = Path.GetFileNameWithoutExtension(f);
            var shopTier = E33ModEditor.Core.Loot.LootCatalog.MerchantTiers.GetValueOrDefault(stem, 2);
            for (int t = 1; t <= Math.Min(shopTier, 4); t++)
            {
                var id = $"UpgradeMaterial_Level{t}";
                var want = t < shopTier
                    ? E33ModEditor.Core.Loot.RandomizerKit.LowerCatalystShopQuantity
                    : E33ModEditor.Core.Loot.RandomizerKit.CurrentCatalystShopQuantity;
                var cat = shopJson[id];
                if (cat is null)
                {
                    missingCatalyst++;
                    Console.Error.WriteLine($"rando-smoke FAILED: {stem} missing {id}");
                    continue;
                }
                var got = cat["Quantity"]?.ToObject<int>() ?? -1;
                if (got != want)
                {
                    badCatalystQty++;
                    Console.Error.WriteLine($"rando-smoke FAILED: {stem}:{id} qty={got}, want {want}");
                }
            }
            if (shopJson["UpgradeMaterial_Level5"] is not null)
            {
                t5Catalyst++;
                Console.Error.WriteLine($"rando-smoke FAILED: {stem} sells the restricted L5 catalyst");
            }
            var highestSold = ids
                .Where(i => i.StartsWith("UpgradeMaterial_Level", StringComparison.Ordinal))
                .Select(i => int.TryParse(i["UpgradeMaterial_Level".Length..], out var lv) ? lv : 0)
                .DefaultIfEmpty(0).Max();
            if (highestSold > Math.Min(shopTier, 4))
            {
                badCatalystTier++;
                Console.Error.WriteLine(
                    $"rando-smoke FAILED: {stem} sells L{highestSold}, above its T{shopTier} cap");
            }
        }
        Console.WriteLine($"shops={shops} noWeapon={noWeapon} noPictos={noPictos} chroma={chroma} " +
                          $"clothesChecked={clothesChecked} clothesDirty={clothesDirty} outOfRange={outOfRange} " +
                          $"badPrice={badPrice} badLevel={badLevel} missingCatalyst={missingCatalyst} " +
                          $"badCatalystQty={badCatalystQty} badCatalystTier={badCatalystTier} t5Catalyst={t5Catalyst}");
        Console.WriteLine("  shop gear sizes: " + string.Join(", ",
            gearSizes.Select(kv => $"{kv.Key} gear x{kv.Value}")));
        Chk(shops > 0 && outOfRange == 0, "every gear shop stocks between 4 and 12 gear items");
        Chk(shops > 0 && noWeapon == 0, "every gear shop stocks at least one weapon");
        Chk(shops > 0 && noPictos == 0, "every gear shop stocks at least one pictos");
        Chk(chroma == 0, "no shop sells chroma currency");
        Chk(clothesChecked > 0 && clothesDirty == 0, "clothes shops keep their default stock");
        Chk(shops > 0 && badPrice == 0, "rebuilt shop rows sell at base price (no template PriceOverride leak)");
        Chk(shops > 0 && badLevel == 0, "rebuilt shop rows sell at natural level (no template LevelOverride leak)");
        Chk(shops > 0 && missingCatalyst == 0 && badCatalystQty == 0,
            "shops stock current-tier catalyst x33 and every lower catalyst x999");
        Chk(shops > 0 && badCatalystTier == 0 && t5Catalyst == 0,
            "no shop sells above its tier, and T5 shops never sell L5");
    }

    // ── enemy drops: Dualliste rows each pay the standard T2 boss package ──
    {
        var dDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_randosmoke_drops");
        if (Directory.Exists(dDir)) Directory.Delete(dDir, true);
        Directory.CreateDirectory(dDir);
        var ds = new E33ModEditor.Core.Loot.RandomizerKit.Settings
        {
            Seed = 42, Chests = false, EnemyDrops = true, Merchants = false,
            WeaponPassives = false, MonsterStats = false, WeaponStats = false,
        };
        E33ModEditor.Core.Loot.RandomizerKit.Apply(dDir, ds, spread, null);
        var dEne = FileCopy.FindUnder(dDir, "DT_jRPG_Enemies.uasset")
            ?? throw new FileNotFoundException("mod drop enemies");
        var dEneEd = new DataTableEditor(dEne);
        Newtonsoft.Json.Linq.JArray LootOf(string id)
        {
            if (!dEneEd.TryGetRow(id, out var row)) return new Newtonsoft.Json.Linq.JArray();
            return dEneEd.DumpRow(row)["PossibleLoot"] as Newtonsoft.Json.Linq.JArray
                ?? new Newtonsoft.Json.Linq.JArray();
        }
        // Pool membership, not TypeOf(): a tier-listed cut picto (SpeedsterDodge,
        // CursedPower, …) reads as its "Cut Content Items" category, not "Pictos".
        bool InPool(string id, string type) =>
            Enumerable.Range(1, 5).Any(t => spread.PoolFor(type, t).Contains(id));
        int LuminaTotal(string id) => LootOf(id)
            .Where(e => (e["Item"]?["RowName"]?.ToString() ?? "") == "Consumable_LuminaPoint")
            .Sum(e => e["Quantity"]?.ToObject<int>() ?? 0);
        Chk(LuminaTotal("Petank_Red") == 50,
            $"regular Petank drops exactly 50 lumina (got {LuminaTotal("Petank_Red")})");
        Chk(LuminaTotal("WM_Petank_Alpha") == 1000,
            $"chromatic Petank drops exactly 1000 lumina (got {LuminaTotal("WM_Petank_Alpha")})");
        foreach (var duel in new[] { "FB_DuallisteR", "FB_DuallisteL", "FB_DuallisteLR", "FB_Dualliste_Phase1", "FB_Dualliste_Phase2" })
        {
            var loot = LootOf(duel);
            Chk(loot.Any(e => InPool(e["Item"]?["RowName"]?.ToString() ?? "", "Weapon")),
                $"Dualliste {duel} stages its guaranteed weapon");
            Chk(loot.Any(e => InPool(e["Item"]?["RowName"]?.ToString() ?? "", "Pictos")),
                $"Dualliste {duel} stages pictos rewards");
            Chk(loot.Any(e => e["Item"]?["RowName"]?.ToString() == "UpgradeMaterial_Level2"),
                $"Dualliste {duel} stages T2 catalysts");
            var lumina = loot.FirstOrDefault(e =>
                e["Item"]?["RowName"]?.ToString() == "Consumable_LuminaPoint");
            Chk(lumina is not null && lumina["Quantity"]?.ToObject<int>() == 5,
                $"Dualliste {duel} stages 5 lumina");
        }

        Chk(LootOf("Quest_GestralSumo9999").Count == 0,
            "Ono Puncho carries no battle drops (undefeatable quest fight)");

        // Star Simon's haircuts are vanilla cosmetics; the randomizer must
        // preserve them and add its package on top rather than replace them.
        var simonLoot = LootOf("Boss_Simon_ALPHA");
        foreach (var face in simonFaces)
            Chk(simonLoot.Any(e => e["Item"]?["RowName"]?.ToString() == face),
                $"Star Simon keeps cosmetic {face}");

        // Respawned Danseuse clones must not pay catalysts or lumina.
        var respawningCloneLoot = LootOf("MM_Danseuse_CloneAlpha");
        Chk(!respawningCloneLoot.Any(e =>
                (e["Item"]?["RowName"]?.ToString() ?? "").StartsWith("UpgradeMaterial_", StringComparison.Ordinal)
                || e["Item"]?["RowName"]?.ToString() == "Consumable_LuminaPoint"),
            "respawned Danseuse clone pays no catalyst or lumina");

        // T5 areas cap regulars at L4, so they pay that L4 catalyst at double the
        // rate (60% / 2–6) versus T4 (30% / 1–3).
        {
            Newtonsoft.Json.Linq.JObject? CatOf(string id) =>
                LootOf(id).FirstOrDefault(e => (e["Item"]?["RowName"]?.ToString() ?? "")
                    .StartsWith("UpgradeMaterial_", StringComparison.Ordinal)) as Newtonsoft.Json.Linq.JObject;
            var t5 = CatOf("L_LumiereCitizen_Fleuret_Female");
            Chk(t5 is not null
                && t5["Item"]!["RowName"]!.ToString() == "UpgradeMaterial_Level4"
                && t5["LootPercentageChance"]!.ToObject<double>() == 60
                && t5["Quantity"]!.ToObject<int>() is >= 2 and <= 6,
                "T5 field catalyst pays L4 at 60% / 2-6");
            var t4 = CatOf("MM_Pelerin");
            Chk(t4 is not null
                && t4["Item"]!["RowName"]!.ToString() == "UpgradeMaterial_Level4"
                && t4["Quantity"]!.ToObject<int>() is >= 1 and <= 3,
                "T4 field catalyst pays L4 at 30% / 1-3");
        }

        // Lower-tier catalysts: every non-boss field drop also rolls x1 one tier
        // down at 50% and x1 two tiers down at 30%, never below L1. T5's own
        // field catalyst is the capped L4, so its relatives are L3 (50%) and
        // L2 (30%). T1 rows have nothing below them.
        {
            (string Item, int Qty, double Chance)[] CatsOf(string id) => LootOf(id)
                .Where(e => (e["Item"]?["RowName"]?.ToString() ?? "")
                    .StartsWith("UpgradeMaterial_", StringComparison.Ordinal))
                .Select(e => (
                    e["Item"]!["RowName"]!.ToString(),
                    e["Quantity"]?.ToObject<int>() ?? 0,
                    e["LootPercentageChance"]?.ToObject<double>() ?? 0))
                .ToArray();
            void ChkCat(string id, string item, int qty, double chance) =>
                Chk(CatsOf(id).Any(c => c.Item == item && c.Qty == qty
                        && Math.Abs(c.Chance - chance) < 1e-9),
                    $"{id} rolls {item} x{qty} @ {chance}%");

            ChkCat("MF_Mime", "UpgradeMaterial_Level2", 1, 50);
            ChkCat("MF_Mime", "UpgradeMaterial_Level1", 1, 30);
            ChkCat("MM_Pelerin", "UpgradeMaterial_Level3", 1, 50);
            ChkCat("MM_Pelerin", "UpgradeMaterial_Level2", 1, 30);
            ChkCat("L_LumiereCitizen_Fleuret_Female", "UpgradeMaterial_Level3", 1, 50);
            ChkCat("L_LumiereCitizen_Fleuret_Female", "UpgradeMaterial_Level2", 1, 30);
            Chk(CatsOf("SM_Lancelier").All(c => c.Item == "UpgradeMaterial_Level1"),
                "T1 field drops have no lower-tier catalysts");
        }

        // Act 3 Lumiere is the endgame final area: loot must roll at pictos 33.
        // Loot level is (EncounterLevel + LootContextLevelOffset)/3 (zone 48 +
        // vanilla 0-3 = 16-17). Pin every Act 3 drop's offset to 51 (48+51=99).
        {
            E33ModEditor.Core.Loot.RandomizerKit.PinAct3LootLevels(dDir, spread);
            var dEneAct3 = new DataTableEditor(FileCopy.FindUnder(dDir, "DT_jRPG_Enemies.uasset")!);
            bool Act3Ok(string id)
            {
                if (!dEneAct3.TryGetRow(id, out var row)) return false;
                var loot = row.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "PossibleLoot")
                    as UAssetAPI.PropertyTypes.Objects.ArrayPropertyData;
                if (loot is null || loot.Value.Length == 0) return true;
                foreach (var ent in loot.Value.OfType<UAssetAPI.PropertyTypes.Structs.StructPropertyData>())
                {
                    var off = ent.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "LootContextLevelOffset")
                        as UAssetAPI.PropertyTypes.Objects.IntPropertyData;
                    if (off is null || off.Value != 51) return false;
                }
                return true;
            }
            Chk(Act3Ok("LU_EchassierAlpha"), "Act 3 Chromatic Echassier drops at offset 51 (pictos 33)");
            Chk(Act3Ok("L_Creation"), "Act 3 Creation drops at offset 51 (pictos 33)");
            Chk(Act3Ok("L_Boss_Curator"), "Act 3 Renoir drops at offset 51 (pictos 33)");
            Chk(Act3Ok("L_LumiereCitizen_Fleuret_Female"), "Act 3 citizens drop at offset 51 (pictos 33)");
        }

        // Endgame clone variants: a borrowed low-tier row fought in a T5 area is
        // cloned to T5_* and the encounter repointed, so it rolls full T5 loot +
        // the catalyst while the native row keeps its own tier (no leak down).
        {
            Chk(dEneEd.HasRow("T5_MM_Danseuse"), "Flying Manor borrows MM_Danseuse -> T5_MM_Danseuse clone");
            Chk(dEneEd.HasRow("T5_SM_Portier"), "Flying Manor borrows SM_Portier -> T5_SM_Portier clone");
            Chk(dEneEd.HasRow("T5_MF_Boucheclier"), "Renoir's Drafts borrows MF_Boucheclier -> T5 variant");
            double CloneChance(string id) => LootOf(id)
                .Where(e => (e["Item"]?["RowName"]?.ToString() ?? "") == "UpgradeMaterial_Level5")
                .Select(e => e["LootPercentageChance"]?.ToObject<double>() ?? 0)
                .DefaultIfEmpty(0).Max();
            Chk(CloneChance("T5_MM_Danseuse") == 3.125, "T5_MM_Danseuse rolls the 3.125% Perfect Catalyst");
            Chk(CloneChance("T5_SM_Portier") == 3.125, "T5_SM_Portier rolls the 3.125% Perfect Catalyst");
            Chk(CloneChance("MM_Danseuse") != 3.125,
                "native MM_Danseuse (Frozen Hearts T4) does not leak the catalyst");
            Chk(CloneChance("SM_Portier") != 3.125,
                "native SM_Portier (Spring Meadows T1) does not leak the catalyst");
            var encPath = FileCopy.FindUnder(dDir, "DT_jRPG_Encounters.uasset");
            var encJson = encPath is null
                ? null
                : Newtonsoft.Json.Linq.JObject.Parse(new DataTableEditor(encPath).Dump());
            var encRow = encJson?["CFH_Gargant*1_Danseuse*2"]?["Enemies"] as Newtonsoft.Json.Linq.JObject;
            var refs = encRow?.Properties()
                .Select(p => p.Value?["RowName"]?.ToString()).Where(s => s is not null).ToList()
                ?? new List<string?>();
            Chk(refs.Any(r => r == "T5_MM_Danseuse"),
                "CFH_Gargant*1_Danseuse*2 now spawns T5_MM_Danseuse");
            Chk(!refs.Any(r => r == "MM_Danseuse"),
                "…and no longer the native MM_Danseuse");
        }

        // Rare endgame Perfect Catalyst: a T5-area regular/elite (level 80+, the
        // whole T5 band — Flying Manor, Verso's Drafts, Renoir's Drafts, Lumière)
        // additionally rolls one L5 at 3.125% (≈ 1/32). Boss/alpha recipes
        // already carry L5 at 100%, so that chance is the discriminator; lower
        // tiers, bosses, named quest fights and world-map variants never get it.
        {
            double ChanceOf(string id, string item) => LootOf(id)
                .Where(e => (e["Item"]?["RowName"]?.ToString() ?? "") == item)
                .Select(e => e["LootPercentageChance"]?.ToObject<double>() ?? 0)
                .DefaultIfEmpty(0).Max();
            foreach (var id in new[]
                     {
                         "L_LumiereCitizen_Fleuret_Female", "L_LumiereCitizen_Mage_Female",
                         "RD_Echassier", "RD_Moissonneuse", "RD_Veilleur",
                         "RD_Aberration", "L_Aberration",
                         // The two T5 endgame areas below 90 (Flying Manor 83,
                         // Verso's Drafts 86) are covered now too.
                         "CFH_Mime", "VD_Licorne", "VD_Barbasucette",
                         "VD_Franctale", "VD_Machinapieds", "VD_Mime",
                     })
                Chk(ChanceOf(id, "UpgradeMaterial_Level5") == 3.125,
                    $"{id} rolls the rare 3.125% Perfect Catalyst");
            foreach (var id in new[]
                     {
                         "SM_Lancelier", "MM_Pelerin", "RD_Creation",
                         "GO_Demineur_ALPHA", "WMV_GO_Demineur_ALPHA",
                         // Named quest fights in an endgame area stay excluded.
                         "Quest_FeintFight", "VD_Quest_GestralSeesaw",
                     })
                Chk(ChanceOf(id, "UpgradeMaterial_Level5") != 3.125,
                    $"{id} does not roll the rare Perfect Catalyst");
        }

        // Respawning farm guard: the Chromatic Danseuse clone re-summons mid-fight,
        // so its package must never contain catalysts or lumina. The main alpha
        // keeps the normal boss recipe (5×L5 + 3–5×L4).
        {
            var cloneLoot = LootOf("MM_Danseuse_CloneAlpha");
            Chk(!cloneLoot.Any(e =>
                    (e["Item"]?["RowName"]?.ToString() ?? "").StartsWith("UpgradeMaterial_", StringComparison.Ordinal)
                    || e["Item"]?["RowName"]?.ToString() == "Consumable_LuminaPoint"),
                "Danseuse clone drops no catalyst or lumina (respawn farm guard)");
            var mainLoot = LootOf("MM_Danseuse_Alpha");
            Chk(mainLoot.Any(e => (e["Item"]?["RowName"]?.ToString() ?? "")
                    == "UpgradeMaterial_Level5"),
                "Danseuse main alpha keeps its Level5 catalyst");

            // Grosse Tete can be killed by dodging only, so the surviving
            // variants cap the Perfect Catalyst payload at 3. The native MF_
            // row keeps its T4 band; CFH keeps its T5 band.
            foreach (var tete in new[] { "MF_GrossTete", "CFH_GrossTete" })
            {
                var teteLoot = LootOf(tete);
                var teteL5 = teteLoot.FirstOrDefault(e =>
                    (e["Item"]?["RowName"]?.ToString() ?? "") == "UpgradeMaterial_Level5");
                Chk(teteL5 is not null
                    && teteL5["Quantity"]?.ToObject<int>()
                        == E33ModEditor.Core.Loot.RandomizerKit.GrosseTetePerfectCatalystQty,
                    $"{tete} caps Perfect Catalysts at 3");
            }
            // The world-map venue clone is T3: no Perfect Catalyst, T3 catalyst.
            var wmTete = LootOf("BV_MF_GrossTete_45");
            Chk(!wmTete.Any(e => (e["Item"]?["RowName"]?.ToString() ?? "")
                    == "UpgradeMaterial_Level5"),
                "World-map Grosse Tete variant pays no Perfect Catalyst (T3)");
            Chk(wmTete.Any(e => (e["Item"]?["RowName"]?.ToString() ?? "")
                    == "UpgradeMaterial_Level3"),
                "World-map Grosse Tete variant pays a T3 catalyst");
        }

        // World-map split: the borrowed T4+ natives are cloned to T3 world
        // variants (T4 for the high-band solo chromatics, see T4Variants) and
        // DT_WorldMap_Encounters is repointed at them, so the native fight keeps
        // its tier while the optional world fight pays the world tier.
        {
            var variants = dEneEd.RowNames
                .Where(n => n.StartsWith("WMV_", StringComparison.Ordinal)).ToList();
            Chk(variants.Count == E33ModEditor.Core.Loot.WorldMapVariantKit.DoubleDipped.Length,
                $"world-map variants staged (got {variants.Count})");
            var encPath = FileCopy.FindUnder(dDir, "DT_WorldMap_Encounters.uasset");
            Chk(encPath is not null, "world-map encounter table staged");
            if (encPath is not null)
            {
                var dump = new DataTableEditor(encPath).Dump();
                var repointed = System.Text.RegularExpressions.Regex.Matches(dump, "\"WMV_").Count;
                Chk(repointed >= 20, $"world-map encounters repointed at variants (got {repointed})");
                // The solo world chromatics borrow T5 natives; each world fight
                // must point at a venue-pinned clone, not the native row.
                foreach (var (enc, variant) in new[]
                         {
                             ("WM_Demineur_Alpha", "BV_GO_Demineur_ALPHA_68"),
                             ("WM_EvequeIce_Alpha", "BV_SM_Boss_Eveque_Ice_ALPHA_50"),
                             ("WM_MoissoneuseAlpha", "BV_MF_Moissonneuse_ALPHA_50"),
                         })
                    Chk(dump.Contains($"\"{variant}\""),
                        $"{enc} repointed at {variant} (venue-pinned)");
            }

            // The high-band solo chromatics: the native row keeps its T5 package
            // while the venue clone pays the band of the world fight it is in.
            foreach (var (native, l5, variant, venueL5) in new[]
                     {
                         ("GO_Demineur_ALPHA", 21, "BV_GO_Demineur_ALPHA_68", 5),
                         ("SM_Boss_Eveque_Ice_ALPHA", 15, "BV_SM_Boss_Eveque_Ice_ALPHA_50", 0),
                         ("MF_Moissonneuse_ALPHA", 21, "BV_MF_Moissonneuse_ALPHA_50", 0),
                     })
            {
                var loot = LootOf(native);
                var l5line = loot.FirstOrDefault(e =>
                    (e["Item"]?["RowName"]?.ToString() ?? "") == "UpgradeMaterial_Level5");
                Chk(l5line is not null && l5line["Quantity"]?.ToObject<int>() == l5,
                    $"{native} keeps its T5 package ({l5}×L5)");
                var v = LootOf(variant);
                var vline = v.FirstOrDefault(e =>
                    (e["Item"]?["RowName"]?.ToString() ?? "") == "UpgradeMaterial_Level5");
                var got = vline?["Quantity"]?.ToObject<int>() ?? 0;
                Chk(got == venueL5,
                    $"{variant} pays the venue band ({venueL5}×L5, got {got})");
            }
        }

        // World-variant stats: the T4 clones are chromatic alphas, so they take
        // the absolute T4 boss curve at their world tier like any other alpha —
        // an order of magnitude above the tier ladder (curve factors run in the
        // hundreds; the ladder would be donor scaling × 25 at most).
        {
            var statEne = FileCopy.FindUnder(
                    Path.Combine(EditorPaths.RepoRoot, "work", "mod_randosmoke"),
                    "DT_jRPG_Enemies.uasset")
                ?? throw new FileNotFoundException("mod stat enemies");
            var statEd = new DataTableEditor(statEne);
            double StagedHp(string id) => StagedScaling(id, "HP");
            double StagedScaling(string id, string field)
            {
                if (!statEd.TryGetRow(id, out var row)) return double.NaN;
                var scaling = row.Value.FirstOrDefault(p =>
                        DataTableEditor.CleanName(p.Name) == "EnemyScaling")
                    as UAssetAPI.PropertyTypes.Structs.StructPropertyData;
                var f = scaling?.Value.FirstOrDefault(p =>
                    DataTableEditor.CleanName(p.Name) == field);
                return f switch
                {
                    UAssetAPI.PropertyTypes.Objects.DoublePropertyData d => d.Value,
                    UAssetAPI.PropertyTypes.Objects.FloatPropertyData x => x.Value,
                    UAssetAPI.PropertyTypes.Objects.IntPropertyData i => i.Value,
                    _ => double.NaN,
                };
            }
            int StagedLevel(string id)
            {
                if (!statEd.TryGetRow(id, out var row)) return -1;
                var lvl = row.Value.FirstOrDefault(p =>
                    DataTableEditor.CleanName(p.Name) == "Level");
                return lvl switch
                {
                    UAssetAPI.PropertyTypes.Objects.IntPropertyData i => i.Value,
                    _ => -1,
                };
            }
            // BossVenueKit pins each reused boss to its world fight's own level;
            // the clone then takes that level's boss curve (rather than a fixed
            // world tier, which would be wrong for the level-50/68 solo fights).
            foreach (var (variant, level) in new[]
                     {
                         ("BV_GO_Demineur_ALPHA_68", 68),
                         ("BV_SM_Boss_Eveque_Ice_ALPHA_50", 50),
                         ("BV_MF_Moissonneuse_ALPHA_50", 50),
                     })
            {
                Chk(StagedLevel(variant) == level,
                    $"{variant} pinned to venue level {level}");
                var got = StagedHp(variant);
                Chk(!double.IsNaN(got) && got > 1.0,
                    $"{variant} takes a boss HP pin (got {got:0.##})");
            }

            // Flying curve rows (IsFlying flag) stage a third of the curve HP:
            // 0.33 × CurveHp(L) / Alpha[L] (vanilla cancels out of the pin).
            // Loot, XP and ATK/SPD/DEF are untouched — HP only. Flying ladder
            // bosses take the same cut (vanilla × ladder × 0.33).
            foreach (var (id, want) in new[]
                     {
                         ("GO_Demineur_ALPHA", 0.33 * 900000000.0 / 4018040.0),
                         ("MF_Chapelier_ALPHA", 0.33 * 328209579.0 / 821808.0),
                         ("MF_Chapelier_MaskSadness", 0.3 * 10.0 * 0.33),
                         ("SC_CultistFlying_ALPHA", 0.5 * 10.0 * 0.33),
                         ("SI_Ballet_ALPHA_Charm", 0.35 * 10.0 * 0.33),
                         ("SI_Ballet_ALPHA_Melee", 0.35 * 10.0 * 0.33),
                         ("SI_Ballet_ALPHA_Range", 0.35 * 10.0 * 0.33),
                     })
            {
                var got = StagedHp(id);
                Chk(!double.IsNaN(got) && Math.Abs(got - want) < 1.0,
                    $"{id} stages third HP (flying, scaling ~{want:0.##}, got {got:0.##})");
            }

            // Grosse Tete lift: CurveHp(55) / Elite[55] ≈ 1017.14 (vanilla cancels
            // out of the pin, same as above). The old T3 ladder staged 200.
            const double wantTete = 210431394.86149445 / 206886.0;
            var gotTete = StagedHp("MF_GrossTete");
            Chk(!double.IsNaN(gotTete) && Math.Abs(gotTete - wantTete) < 1.0,
                $"Grosse Tete takes the T4 boss curve (HP scaling ~{wantTete:0.##}, got {gotTete:0.##})");

            // Third-stat defense pins: P.DEF and M.DEF stage identically at
            // resolved-defShare × DefMax(level) (staged archetype base 1.0, so
            // staged == target however vanilla scaled — including rows like
            // Duolliste_P2 whose vanilla scaling is nonzero). Expected values
            // go through the real pool resolver — hand-rolled arithmetic already
            // got one wrong (alpha shares differ per row and scale-down bites).
            // A ladder row would stage donor × 25 at most.
            foreach (var (id, level, atkCfg, defCfg) in new[]
                     {
                         ("MF_Boucheclier_ALPHA", 61, 0.70, 0.50),
                         ("MM_Gargant", 61, 0.64, 0.60),
                         ("SM_Boss_Eveque_Ice_ALPHA", 83, 0.72, 0.40),
                         ("Duolliste_P2", 99, 1.00, 0.20),
                     })
            {
                var (_, _, dShare) = E33ModEditor.Core.Loot.RandomizerKit.ResolveShares(atkCfg, defCfg);
                var want = dShare * E33ModEditor.Core.Loot.EnemyDefenseKit.MaxDefense(level);
                var gotP = StagedScaling(id, "PhysicalDefense");
                var gotM = StagedScaling(id, "MagicalDefense");
                Chk(!double.IsNaN(gotP) && Math.Abs(gotP - want) < Math.Max(1.0, want * 0.01),
                    $"{id} stages its defense pin (P.DEF ~{want:0.##}, got {gotP:0.##})");
                Chk(!double.IsNaN(gotM) && Math.Abs(gotM - want) < Math.Max(1.0, want * 0.01),
                    $"{id} stages P.DEF and M.DEF identically (M.DEF {gotM:0.##})");
            }

            // Staged archetype defense bases are uniformly 1.0 (the pin math
            // divides by this; vanilla 0.0 could never pin against).
            {
                var archDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_randosmoke");
                foreach (var arch in new[] { "Alpha", "Boss", "Regular" })
                {
                    var ap = FileCopy.FindUnder(archDir, $"DT_EnemyArchetype_{arch}.uasset");
                    Chk(ap is not null, $"staged {arch} archetype table present");
                    if (ap is null) continue;
                    var aed = new DataTableEditor(ap);
                    foreach (var lvl in new[] { "Level_60", "Level_99" })
                    {
                        if (!aed.TryGetRow(lvl, out var arow)) { Chk(false, $"{arch} {lvl} staged"); continue; }
                        foreach (var f in new[] { "PhysicalDefense", "MagicalDefense" })
                        {
                            var v = arow.Value.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == f);
                            var bv = v switch
                            {
                                UAssetAPI.PropertyTypes.Objects.DoublePropertyData d => d.Value,
                                UAssetAPI.PropertyTypes.Objects.FloatPropertyData x => x.Value,
                                UAssetAPI.PropertyTypes.Objects.IntPropertyData i => (double)i.Value,
                                _ => double.NaN,
                            };
                            Chk(!double.IsNaN(bv) && Math.Abs(bv - 1.0) < 1e-9,
                                $"staged {arch} {lvl} {f} == 1.0");
                        }
                    }
                }
            }

            // The defense chain reads the weapon overlay: guard its anchor
            // values so a silent overlay retime fails loudly instead of
            // drifting every defense pin.
            {
                var o = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(
                    Path.Combine(EditorPaths.RepoRoot, "work", "weapon_equipment_scaling.json")));
                var atk = o["Attack"] as Newtonsoft.Json.Linq.JArray;
                Chk(atk is not null && atk.Count >= 334, "weapon overlay has 334 levels");
                if (atk is not null && atk.Count >= 334)
                {
                    foreach (var (lvl, want) in new[] { (222, 3228.0), (250, 4265.1272411475575), (300, 7014.563985004483), (333, 13483.615176944248) })
                        Chk(Math.Abs(atk[lvl]!.ToObject<double>() - want) < 1e-6,
                            $"weapon overlay power at {lvl} == {want}");
                }
            }
        }

        // FB_DuallisteLR carries a hand-authored T3 package on top of its boss
        // recipe: exactly one Verso-only T3 weapon, two distinct T3 pictos and
        // one T3 catalyst, and keeps its vanilla loot context offset.
        {
            int PoolTier(string id)
            {
                for (int t = 1; t <= 5; t++)
                    if (spread.PoolFor("Pictos", t).Contains(id) || spread.PoolFor("Weapon", t).Contains(id))
                        return t;
                return 0;
            }

            var owners = E33ModEditor.Core.Loot.RandomizerKit.WeaponOwners();
            var loot = LootOf("FB_DuallisteLR");
            var ids = loot.Select(e => e["Item"]?["RowName"]?.ToString() ?? "").ToList();

            var versoWeapons = ids.Where(id =>
                PoolTier(id) == 3
                && owners.TryGetValue(id, out var o)
                && o.Equals("Verso", StringComparison.OrdinalIgnoreCase)).ToList();
            Chk(versoWeapons.Count == 1,
                $"Dualliste LR override: exactly one T3 Verso weapon (got {string.Join(",", versoWeapons)})");

            var t3pictos = ids.Where(id => spread.PoolFor("Pictos", 3).Contains(id)).ToList();
            Chk(t3pictos.Count == 2 && t3pictos.Distinct(StringComparer.Ordinal).Count() == 2,
                $"Dualliste LR override: two distinct T3 pictos (got {string.Join(",", t3pictos)})");

            var t3cat = loot.FirstOrDefault(e => e["Item"]?["RowName"]?.ToString() == "UpgradeMaterial_Level3");
            Chk(t3cat is not null && t3cat["Quantity"]?.ToObject<int>() == 1,
                "Dualliste LR override: one UpgradeMaterial_Level3 at quantity 1");

            Chk(ids.Contains("Consumable_LuminaPoint"),
                "Dualliste LR override is additive to the boss package (lumina present)");

            var offsets = loot.Select(e => e["LootContextLevelOffset"]?.ToObject<int>())
                .Where(o => o is not null).Select(o => o!.Value).Distinct().ToList();
            Chk(offsets.Count == 1 && offsets[0] == 3,
                $"Dualliste LR keeps its vanilla LootContextLevelOffset=3 (got {string.Join(",", offsets)})");
        }

        // Old Lumiere Renoir's package REPLACES his boss recipe: three distinct
        // T3 pictos, two distinct T3 weapons, 21 T3 catalysts and 99 lumina, with
        // no T4/T5 catalyst left over from the archetype recipe.
        {
            var loot = LootOf("OL_MirrorRenoir_FirstFight");
            var ids = loot.Select(e => e["Item"]?["RowName"]?.ToString() ?? "").ToList();
            Console.WriteLine("OL_MirrorRenoir_FirstFight drops: " +
                string.Join(", ", loot.Select(e => $"{e["Item"]?["RowName"]}×{e["Quantity"]}")));

            var t3Pictos = ids.Where(id => spread.PoolFor("Pictos", 3).Contains(id)).ToList();
            Chk(t3Pictos.Count == 3 && t3Pictos.Distinct(StringComparer.Ordinal).Count() == 3,
                $"Old Lumiere Renoir drops three distinct T3 pictos (got {string.Join(",", t3Pictos)})");

            var t3Weapons = ids.Where(id => spread.PoolFor("Weapon", 3).Contains(id)).ToList();
            Chk(t3Weapons.Count == 2 && t3Weapons.Distinct(StringComparer.Ordinal).Count() == 2,
                $"Old Lumiere Renoir drops two distinct T3 weapons (got {string.Join(",", t3Weapons)})");

            var cat = loot.FirstOrDefault(e =>
                e["Item"]?["RowName"]?.ToString() == "UpgradeMaterial_Level3");
            Chk(cat is not null && cat["Quantity"]?.ToObject<int>() == 21,
                "Old Lumiere Renoir drops 21 T3 catalysts");

            var lum = loot.FirstOrDefault(e =>
                e["Item"]?["RowName"]?.ToString() == "Consumable_LuminaPoint");
            Chk(lum is not null && lum["Quantity"]?.ToObject<int>() == 99,
                "Old Lumiere Renoir drops 99 lumina");

            Chk(!ids.Any(id => id is "UpgradeMaterial_Level4" or "UpgradeMaterial_Level5"),
                "Old Lumiere Renoir boss recipe suppressed (no T4/T5 catalyst)");
        }

        // Sirene boss package REPLACES her boss recipe: three distinct T3
        // pictos, two distinct T3 weapons, 3-5 T3 catalysts and 5 lumina.
        {
            var loot = LootOf("SI_Axon_Sirene");
            var ids = loot.Select(e => e["Item"]?["RowName"]?.ToString() ?? "").ToList();
            Console.WriteLine("SI_Axon_Sirene drops: " +
                string.Join(", ", loot.Select(e => $"{e["Item"]?["RowName"]}×{e["Quantity"]}")));

            var t3Pictos = ids.Where(id => spread.PoolFor("Pictos", 3).Contains(id)).ToList();
            Chk(t3Pictos.Count == 3 && t3Pictos.Distinct(StringComparer.Ordinal).Count() == 3,
                $"Sirene boss drops three distinct T3 pictos (got {string.Join(",", t3Pictos)})");

            var t3Weapons = ids.Where(id => spread.PoolFor("Weapon", 3).Contains(id)).ToList();
            Chk(t3Weapons.Count == 2 && t3Weapons.Distinct(StringComparer.Ordinal).Count() == 2,
                $"Sirene boss drops two distinct T3 weapons (got {string.Join(",", t3Weapons)})");

            var cat = loot.FirstOrDefault(e =>
                e["Item"]?["RowName"]?.ToString() == "UpgradeMaterial_Level3");
            var catQty = cat?["Quantity"]?.ToObject<int>() ?? -1;
            Chk(catQty >= 3 && catQty <= 5, $"Sirene boss drops 3-5 T3 catalysts (got {catQty})");

            var lum = loot.FirstOrDefault(e =>
                e["Item"]?["RowName"]?.ToString() == "Consumable_LuminaPoint");
            Chk(lum is not null && lum["Quantity"]?.ToObject<int>() == 5,
                "Sirene boss drops 5 lumina (the other Axon amount)");

            Chk(!ids.Any(id => id is "UpgradeMaterial_Level4" or "UpgradeMaterial_Level5"),
                "Sirene boss recipe suppressed (no T4/T5 catalyst)");
        }

        // Paintress + Painted Renoir packages REPLACE their boss recipes: one
        // random T4 weapon, three distinct T4 pictos and 33 Grandiose
        // (UpgradeMaterial_Level4) Chroma Catalysts.
        foreach (var (boss, label) in new[]
                 {
                     ("MO_Boss_Paintress", "The Paintress"),
                     ("MM_MirrorRenoir", "Painted Renoir"),
                 })
        {
            var loot = LootOf(boss);
            var ids = loot.Select(e => e["Item"]?["RowName"]?.ToString() ?? "").ToList();
            Console.WriteLine($"{boss} drops: " +
                string.Join(", ", loot.Select(e => $"{e["Item"]?["RowName"]}×{e["Quantity"]}")));

            var t4Pictos = ids.Where(id => spread.PoolFor("Pictos", 4).Contains(id)).ToList();
            Chk(t4Pictos.Count == 3 && t4Pictos.Distinct(StringComparer.Ordinal).Count() == 3,
                $"{label} drops three distinct T4 pictos (got {string.Join(",", t4Pictos)})");

            var t4Weapons = ids.Where(id => spread.PoolFor("Weapon", 4).Contains(id)).ToList();
            Chk(t4Weapons.Count == 1 && t4Weapons.Distinct(StringComparer.Ordinal).Count() == 1,
                $"{label} drops one T4 weapon (got {string.Join(",", t4Weapons)})");

            var cat = loot.FirstOrDefault(e =>
                e["Item"]?["RowName"]?.ToString() == "UpgradeMaterial_Level4");
            Chk(cat is not null && cat["Quantity"]?.ToObject<int>() == 33,
                $"{label} drops 33 T4 catalysts (got {cat?["Quantity"]})");

            Chk(!ids.Any(id => id is "UpgradeMaterial_Level3" or "UpgradeMaterial_Level5"),
                $"{label} boss recipe suppressed (no off-tier catalyst)");
        }

        // RedWoods fountain (the "Bénisseur"): every white-nevron grant must
        // actually be staged, and the fountain's Chroma-donation reward must be
        // a T4 picto that never carries a Defense stat boost.
        {
            foreach (var (stem, label) in new[]
                     {
                         ("BP_Dialogue_TroubadourCantPlay", "troubadour"),
                         ("BP_Dialog_SpiritPortier", "portier"),
                         ("BP_Dialog_Goblu_DemineurMissingMine", "demineur"),
                         ("BP_Dialogue_SleepingBenisseur", "fountain"),
                         ("BP_Dialog_JarNeedLight", "jar"),
                     })
            {
                Chk(FileCopy.FindUnder(dDir, stem + ".uasset") is not null,
                    $"white-nevron {label} reward staged ({stem})");
            }

            var ben = FileCopy.FindUnder(dDir, "BP_Dialogue_SleepingBenisseur.uasset");
            if (ben is not null)
            {
                var rewards = E33ModEditor.Core.Loot.DialogueRewardKit.ReadRewardIds(ben, "RewardOnCompletion")
                    .Concat(E33ModEditor.Core.Loot.DialogueRewardKit.ReadRewardIds(ben, "Reward"))
                    .ToList();
                var reward = rewards.FirstOrDefault(r =>
                    Enumerable.Range(1, 5).Any(t => spread.PoolFor("Pictos", t).Contains(r)));
                var t4 = reward is not null && spread.PoolFor("Pictos", 4).Contains(reward);
                var noDef = reward is not null
                    && !E33ModEditor.Core.Loot.RandomizerKit.DefensePictos.Contains(reward);
                Chk(t4 && noDef, $"fountain reward {reward} is T4 and non-Defense");
            }
        }
    }

    // ── tier coherence + consumables: chests on, bosses reserve first ──
    {
        var cDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_randosmoke_chests");
        if (Directory.Exists(cDir)) Directory.Delete(cDir, true);
        Directory.CreateDirectory(cDir);
        var chestSettings = new E33ModEditor.Core.Loot.RandomizerKit.Settings
        {
            Seed = 42, Chests = true, EnemyDrops = true, Merchants = false,
            WeaponPassives = false, MonsterStats = false, WeaponStats = false,
        };
        E33ModEditor.Core.Loot.RandomizerKit.Apply(cDir, chestSettings, spread, null);

        // Boss > Chests > Regular plus tier-locked boss gear: every picto or
        // weapon on a boss row must share the tier of that row's catalyst.
        var cEne = FileCopy.FindUnder(cDir, "DT_jRPG_Enemies.uasset")
            ?? throw new FileNotFoundException("mod chest enemies");
        var cEneEd = new DataTableEditor(cEne);
        Newtonsoft.Json.Linq.JArray CLoot(string id)
        {
            if (!cEneEd.TryGetRow(id, out var row)) return new Newtonsoft.Json.Linq.JArray();
            return cEneEd.DumpRow(row)["PossibleLoot"] as Newtonsoft.Json.Linq.JArray
                ?? new Newtonsoft.Json.Linq.JArray();
        }
        int TierOfItem(string id)
        {
            // Read the tier from the pool lists, not Describe(): generated
            // magnitude variants (BurnOnHit5, Immaculate5, …) are registered
            // with a placeholder PictosTier, but sit in the correct pool.
            for (int t = 1; t <= 5; t++)
                if (spread.PoolFor("Pictos", t).Contains(id) || spread.PoolFor("Weapon", t).Contains(id))
                    return t;
            return 0;
        }
        foreach (var duel in new[] { "FB_DuallisteR", "FB_DuallisteL", "FB_DuallisteLR",
            "FB_Dualliste_Phase1", "FB_Dualliste_Phase2", "CFH_Dualliste", "FB_Dualliste_CleasTower" })
        {
            var loot = CLoot(duel);
            // A row can carry more than one catalyst tier now: an override
            // package may sit alongside the archetype tier (FB_DuallisteLR is
            // T2 boss + T3 package). Every gear item must match one of the
            // catalyst tiers actually present on the row.
            var catTiers = loot
                .Select(e => e["Item"]?["RowName"]?.ToString() ?? "")
                .Where(n => n.StartsWith("UpgradeMaterial_Level", StringComparison.Ordinal))
                .Select(n => int.TryParse(n.Replace("UpgradeMaterial_Level", ""), out var ct) ? ct : 0)
                .Where(t => t > 0)
                .ToHashSet();
            if (catTiers.Count == 0) { Chk(false, $"Dualliste {duel} has a tier catalyst"); continue; }
            var gear = loot
                .Select(e => e["Item"]?["RowName"]?.ToString() ?? "")
                .Where(id => TierOfItem(id) > 0)
                .Select(id => (Id: id, Tier: TierOfItem(id)))
                .ToList();
            var off = gear.Where(g => !catTiers.Contains(g.Tier)).ToList();
            var tiers = string.Join("/T", catTiers.OrderBy(t => t));
            Chk(off.Count == 0, $"Dualliste {duel} gear tier matches catalyst T{tiers}"
                + (off.Count == 0 ? "" : " (off: " + string.Join(", ", off.Select(o => $"{o.Id}=T{o.Tier}")) + ")"));
        }

        // Consumables: capacity shards (9/9/9 + 2 elixir) and potency shapes
        // (2 each) placed; the tint items themselves are never dropped.
        var cChest = FileCopy.FindUnder(cDir, "DT_ChestsContent.uasset")
            ?? throw new FileNotFoundException("mod chest chests");
        var cChestEd = new DataTableEditor(cChest);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in cChestEd.Table.Table.Data)
        {
            if (row.Value.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "LootTableSections")
                is not UAssetAPI.PropertyTypes.Objects.ArrayPropertyData secs) continue;
            foreach (var sec in secs.Value.OfType<UAssetAPI.PropertyTypes.Structs.StructPropertyData>())
            {
                if (sec.Value.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "LootEntries")
                    is not UAssetAPI.PropertyTypes.Objects.ArrayPropertyData ents) continue;
                foreach (var ent in ents.Value.OfType<UAssetAPI.PropertyTypes.Structs.StructPropertyData>())
                {
                    var nm = ent.Value.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "ItemID")
                        as UAssetAPI.PropertyTypes.Objects.NamePropertyData;
                    var id = nm?.Value?.ToString() ?? "";
                    if (id.Length > 0) counts[id] = counts.GetValueOrDefault(id) + 1;
                }
            }
        }
        int Cnt(string id) => counts.GetValueOrDefault(id);
        Chk(Cnt("HealingTint_Shard") == 9, $"Healing Tint Shards = 9 (got {Cnt("HealingTint_Shard")})");
        Chk(Cnt("EnergyTint_Shard") == 9, $"Energy Tint Shards = 9 (got {Cnt("EnergyTint_Shard")})");
        Chk(Cnt("ReviveTint_Shard") == 9, $"Revive Tint Shards = 9 (got {Cnt("ReviveTint_Shard")})");
        Chk(Cnt("PartyHealShard") == 2, $"Chroma Elixir Shards = 2 (got {Cnt("PartyHealShard")})");
        Chk(Cnt("ConsumableUpgradeMaterial_Health") == 2, $"Shape of Health = 2 (got {Cnt("ConsumableUpgradeMaterial_Health")})");
        Chk(Cnt("ConsumableUpgradeMaterial_AP") == 2, $"Shape of Energy = 2 (got {Cnt("ConsumableUpgradeMaterial_AP")})");
        Chk(Cnt("ConsumableUpgradeMaterial_Revive") == 2, $"Shape of Life = 2 (got {Cnt("ConsumableUpgradeMaterial_Revive")})");
        var tintItems = counts
            .Where(kv => kv.Key.StartsWith("Consumable_Health_Level", StringComparison.Ordinal)
                      || kv.Key.StartsWith("Consumable_Energy_Level", StringComparison.Ordinal)
                      || kv.Key.StartsWith("Consumable_Revive_Level", StringComparison.Ordinal))
            .Sum(kv => kv.Value);
        Chk(tintItems == 0, $"no tint items placed (got {tintItems})");
    }

    // ── dialogue reward patches: single-site grant swaps on scratch copies ──
    {
        var gDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_randosmoke_dialogue");
        if (Directory.Exists(gDir)) Directory.Delete(gDir, true);
        Directory.CreateDirectory(gDir);
        string StageBp(string stem, string sub)
        {
            _ = sub; // sources resolve through the shared dialogue cache now
            var src = E33ModEditor.Core.Loot.DialogueRewardKit.EnsureLocal(stem);
            var dest = Path.Combine(gDir, stem + ".uasset");
            FileCopy.AssetPair(src, dest);
            foreach (var sib in E33ModEditor.Core.Loot.DialogueRewardKit.SiblingsOf(stem))
            {
                var sibSrc = Path.Combine(Path.GetDirectoryName(src)!, sib + ".uasset");
                if (File.Exists(sibSrc))
                    FileCopy.AssetPair(sibSrc, Path.Combine(gDir, sib + ".uasset"));
            }
            return dest;
        }
        var tro = StageBp("BP_Dialogue_TroubadourCantPlay",
            Path.Combine("Narrative", "Dialogs", "LevelsDialogs", "BonusLevels", "StoneQuarry"));
        var por = StageBp("BP_Dialog_SpiritPortier",
            Path.Combine("Narrative", "Dialogs", "LevelsDialogs", "BonusLevels", "DoorMaze"));
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.ReadRewardId(tro) == "HealingParry", "troubadour vanilla grant reads HealingParry");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.ReadRewardId(por) == "ProtectingHeal", "portier vanilla grant reads ProtectingHeal");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.PatchRewardId(tro, "HealingParry", "AutoShell") == 1, "troubadour grant is a single site");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.PatchRewardId(por, "ProtectingHeal", "Teamwork2") == 1, "portier grant is a single site");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.ReadRewardId(tro) == "AutoShell", "troubadour grant rewrites to AutoShell");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.ReadRewardId(por) == "Teamwork2", "portier grant rewrites to Teamwork2");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.PatchLootContext(tro, 65) == 0, "troubadour has no encounter-level context site");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.PatchLootContext(por, 65) == 1, "portier context trims to a single site");
        string? keeper = null;
        try
        {
            keeper = StageBp("BP_Dialogue_HiddenArena_Keeper",
                Path.Combine("Narrative", "Dialogs", "LevelsDialogs", "BonusLevels", "HiddenGestralArena"));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dialogue-smoke] keeper not staged: " + ex.Message);
        }
        if (keeper is not null)
        {
            var kplace = new E33ModEditor.Core.Loot.RandomizerKit.LootPlacement();
            var kgot = E33ModEditor.Core.Loot.DialogueRewardKit.PatchRewardArray(
                keeper, "Rewards", 1, spread, new Random(7), kplace);
            Chk(kgot.Count == 5, "keeper has 5 swappable array grants");
            Chk(kgot.Select(p => p.Original).OrderBy(x => x).SequenceEqual(
                new[] { "LastStandCritical", "LastStandPowerful", "LastStandShell", "LastStandSpeed", "SoloFighter" }),
                "keeper vanilla grants are the LastStand set + SoloFighter");
            Chk(kgot.All(p => spread.PoolFor("Pictos", 1).Contains(p.Replacement)),
                "keeper replacements are exact T1 pictos");
            Chk(E33ModEditor.Core.Loot.DialogueRewardKit.PatchLootContext(keeper, 8) >= 1,
                "keeper context sites trimmable");
        }
        string StageBp2(string stem, string sub)
        {
            _ = sub;
            var src = E33ModEditor.Core.Loot.DialogueRewardKit.EnsureLocal(stem);
            var dest = Path.Combine(gDir, stem + ".uasset");
            FileCopy.AssetPair(src, dest);
            return dest;
        }
        var dem = StageBp2("BP_Dialog_Goblu_DemineurMissingMine",
            Path.Combine("Narrative", "Dialogs", "LevelsDialogs", "Goblu"));
        var ben = StageBp2("BP_Dialogue_SleepingBenisseur",
            Path.Combine("Narrative", "Dialogs", "LevelsDialogs", "BonusLevels", "RedWoods"));
        var bla = StageBp2("BP_Dialogue_JudgeOfMercy",
            Path.Combine("Narrative", "Dialogs", "LevelsDialogs", "BonusLevels", "JudgeOfMercy"));
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.ReadRewardId(dem) == "Deminerim", "demineur vanilla grant reads Deminerim");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.ReadRewardIds(ben, "RewardOnCompletion").Contains("Recovery"), "benisseur vanilla grant reads Recovery");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.PatchRewardId(dem, "Deminerim", "Lanceram") == 1, "demineur grant is a single site");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.PatchRewardId(ben, "Recovery", "BurningShots") == 1, "benisseur grant is a single site");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.ReadRewardId(dem) == "Lanceram", "demineur grant rewrites to Lanceram");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.ReadRewardIds(ben, "RewardOnCompletion").Contains("BurningShots"), "benisseur grant rewrites to BurningShots");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.PatchIntConst(bla, 100, 1000) == 1, "blanche pacifist lumina is a single IntConst 100");
        var jar = StageBp2("BP_Dialog_JarNeedLight",
            Path.Combine("Narrative", "Dialogs", "LevelsDialogs", "SpringMeadows"));
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.ReadRewardId(jar) == "HealingTint_Shard", "jar vanilla grant reads HealingTint_Shard");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.PatchRewardId(jar, "HealingTint_Shard", "SoloFighter") == 1, "jar grant is a single site");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.ReadRewardId(jar) == "SoloFighter", "jar grant rewrites to SoloFighter");
        var gol = StageBp2("BP_Dialogue_GV_Golgra",
            Path.Combine("Narrative", "Dialogs", "LevelsDialogs", "GestralVillage"));
        var golEntries = E33ModEditor.Core.Loot.DialogueRewardKit.ReadMapRewardEntries(gol, "Reward_GolgraMap");
        Chk(golEntries.Count == 6, $"golgra map has 6 character entries ({golEntries.Count})");
        Chk(golEntries.Select(p => p.Key).OrderBy(x => x).SequenceEqual(
            new[] { "Lune", "Maelle", "Monoco", "Noah", "Sciel", "Verso" }),
            "golgra map keys are the six characters (Noah=Gustave)");
        Chk(golEntries.FirstOrDefault(p => p.Key == "Maelle").Value == "Medalum", "golgra vanilla Maelle grant is Medalum");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.PatchMapRewardKey(gol, "Reward_GolgraMap", "Maelle", "Jarum") == 1, "golgra Maelle grant is a single site");
        var golAfter = E33ModEditor.Core.Loot.DialogueRewardKit.ReadMapRewardEntries(gol, "Reward_GolgraMap");
        Chk(golAfter.FirstOrDefault(p => p.Key == "Maelle").Value == "Jarum", "golgra Maelle grant rewrites to Jarum");
        Chk(golAfter.Where(p => p.Key != "Maelle").All(p => golEntries.Any(q => q.Key == p.Key && q.Value == p.Value)), "golgra other keys untouched by the Maelle patch");
        {
            var oplace = new E33ModEditor.Core.Loot.RandomizerKit.LootPlacement();
            foreach (var (key, owner) in new[] { ("Noah", "Gustave"), ("Verso", "Verso"), ("Lune", "Lune"), ("Maelle", "Maelle"), ("Sciel", "Sciel"), ("Monoco", "Monoco") })
            {
                var opick = E33ModEditor.Core.Loot.DialogueRewardKit.TakeOwnedExactTier(spread, oplace, owner, 3, new Random(333333));
                Chk(opick is not null && E33ModEditor.Core.Loot.DialogueRewardKit.WeaponCharacter(spread, opick) == owner, $"golgra[{key}] owner draw is a {owner} weapon ({opick})");
                Chk(opick is not null && spread.PoolFor("Weapon", 3).Contains(opick), $"golgra[{key}] owner draw is exact T3 ({opick})");
            }
            // Guaranteed story gifts must never fall back to the vanilla T5
            // Medalum just because the owner's T3 pool was already placed:
            // repeat an owner T3 instead.
            var exhaust = new E33ModEditor.Core.Loot.RandomizerKit.LootPlacement();
            var maelleT3 = spread.PoolFor("Weapon", 3)
                .Where(id => E33ModEditor.Core.Loot.DialogueRewardKit.WeaponCharacter(spread, id) == "Maelle")
                .ToList();
            foreach (var id in maelleT3) exhaust.Placed.Add(id);
            var rep = E33ModEditor.Core.Loot.DialogueRewardKit.TakeOwnedExactTierOrRepeat(
                spread, exhaust, "Maelle", 3, new Random(333333));
            Chk(rep is not null && maelleT3.Contains(rep),
                $"golgra Maelle exhausted pool repeats a T3 Maelle weapon ({rep})");
            Chk(E33ModEditor.Core.Loot.DialogueRewardKit.TakeOwnedExactTier(
                    spread, exhaust, "Maelle", 3, new Random(333333)) is null,
                "unique-only Maelle draw reports dry once the tier is placed");
        }
        // The tournament registrar's Rewards array is the payout after the
        // last arena fight (Sciel), independent of Golgra's map. Its CDO is a
        // RawExport, so the kit locates the six 16-byte S_ItemStack entries by
        // the vanilla FName sequence and swaps the RowNames in place.
        {
            var reg = StageBp2("BP_Dialogue_GV_ArenaRegistrar",
                Path.Combine("Narrative", "Dialogs", "LevelsDialogs", "GestralVillage"));
            var regSlots = new (string Original, string Owner)[]
            {
                ("Sakaram", "Gustave"),
                ("Trebuchim", "Lune"),
                ("Medalum", "Maelle"),
                ("Hevason", "Sciel"),
                ("Sakaram", "Verso"),
                ("Sidaro", "Monoco"),
            };
            var regPlace = new E33ModEditor.Core.Loot.RandomizerKit.LootPlacement();
            var regGot = E33ModEditor.Core.Loot.DialogueRewardKit.PatchOwnerRewardArray(
                reg, "Rewards", regSlots, 3, spread, regPlace, new Random(333333), null, out var regStart);
            Chk(regGot.Count == 6, $"registrar Rewards has 6 owner slots ({regGot.Count})");
            Chk(regGot.FirstOrDefault(p => p.Original == "Medalum").Replacement != "Medalum",
                "registrar vanilla Medalum slot is replaced");
            var regNames = E33ModEditor.Core.Loot.DialogueRewardKit.ReadOwnerRewardArrayAt(reg, regStart, 6);
            Chk(regNames.Count == 6, $"registrar Rewards reads back 6 names ({regNames.Count})");
            Chk(regNames.All(id => spread.PoolFor("Weapon", 3).Contains(id)),
                "registrar Rewards rewrites to exact T3 weapons");
            for (int i = 0; i < regSlots.Length && i < regNames.Count; i++)
                Chk(E33ModEditor.Core.Loot.DialogueRewardKit.WeaponCharacter(spread, regNames[i]) == regSlots[i].Owner,
                    $"registrar slot {i} stays {regSlots[i].Owner} ({regNames[i]})");
        }
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.WeaponCharacter(spread, "Reacheso_1") == "Verso", "Reacheso_1 parses as Verso weapon");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.WeaponCharacter(spread, "Deminerim") == "Lune", "Deminerim parses as Lune weapon");
        Chk(E33ModEditor.Core.Loot.DialogueRewardKit.WeaponCharacter(spread, "SoloFighter") == "", "pictos parse as no character");
        Chk(!E33ModEditor.Core.Loot.RandomizerKit.WeaponAllowedAtTier(spread, "Weapon", "Reacheso_1", 1), "Verso weapon gated out at T1");
        Chk(!E33ModEditor.Core.Loot.RandomizerKit.WeaponAllowedAtTier(spread, "Weapon", "Reacheso_1", 2), "Verso weapon gated out at T2");
        Chk(E33ModEditor.Core.Loot.RandomizerKit.WeaponAllowedAtTier(spread, "Weapon", "Reacheso_1", 3), "Verso weapon allowed at T3");
        Chk(E33ModEditor.Core.Loot.RandomizerKit.WeaponAllowedAtTier(spread, "Weapon", "Demonam", 1), "Gustave weapon allowed at T1");
        Chk(E33ModEditor.Core.Loot.RandomizerKit.WeaponAllowedAtTier(spread, "Weapon", "Deminerim", 1), "Lune weapon allowed at T1");
        Chk(E33ModEditor.Core.Loot.RandomizerKit.WeaponAllowedAtTier(spread, "Pictos", "SoloFighter", 1), "pictos always pass the roster gate");
    }

    // ── GUI ⇄ CLI parity ───────────────────────────────────────────────────
    // The GUI keeps its own copy of the pack sequence, so the two can drift.
    // 1) The ladder that actually ships (work/randomizer.json, read by
    //    rebuild-mod) must match DefaultBossHp — otherwise the same defaults
    //    pack different boss HP depending on which entry point runs.
    var randoJson = Path.Combine(EditorPaths.RepoRoot, "work", "randomizer.json");
    if (File.Exists(randoJson))
    {
        var ship = (Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(randoJson))["BossHp"]
            as Newtonsoft.Json.Linq.JArray)?.Select(t => t.ToObject<double>()).ToArray()
            ?? Array.Empty<double>();
        var want = E33ModEditor.Core.Loot.RandomizerKit.DefaultBossHp;
        Chk(ship.Length == want.Length && !ship.Where((v, i) => Math.Abs(v - want[i]) > 1e-6).Any(),
            $"shipping BossHp ladder matches DefaultBossHp ({string.Join("/", want)})");

        // The rare endgame Perfect Catalyst ships on (default 3.125%); a config that
        // predates the field leaves it at the default, so this stays > 0.
        var rare = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(randoJson))
            .Value<double?>("T5PerfectCatalystChance");
        Chk(rare is > 0, $"shipping config keeps the rare Perfect Catalyst on ({rare ?? 0}%)");
    }
    Chk(new E33ModEditor.Core.Loot.RandomizerKit.Settings().T5PerfectCatalystChance > 0,
        "a default RandomizerKit.Settings enables the rare Perfect Catalyst (CLI pack default)");

    // GUI packing is no longer supported; CLI rebuild-mod and the standalone
    // generator are the only pack-build paths.

    return randoFails == 0 ? 0 : 1;
}

static int CmdLootDist(string[] args)
{
    var runs = 20;
    if (args.Length > 1 && int.TryParse(args[1], out var n) && n > 0)
        runs = Math.Min(n, 100);
    var catalog = new E33ModEditor.Core.Loot.LootCatalog();
    var chestsPath = Path.Combine(EditorPaths.RepoRoot, "work", "chests.json");
    var enemiesPath = Path.Combine(EditorPaths.RepoRoot, "work", "enemies.json");
    if (!File.Exists(chestsPath) || !File.Exists(enemiesPath))
        throw new FileNotFoundException("work/chests.json and work/enemies.json are required.");
    catalog.Rebuild(
        Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(chestsPath)),
        Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(enemiesPath)),
        Array.Empty<(string, Newtonsoft.Json.Linq.JObject)>());
    Console.WriteLine($"checks chests={catalog.Checks.Count(c => c.Source == "Chest")} paint cages={catalog.Checks.Count(c => c.Source == E33ModEditor.Core.Loot.LootCatalog.PaintCageSource)} enemies={catalog.Checks.Count(c => c.Source == "Enemy")}");
    Console.Write(E33ModEditor.Core.Loot.LootDist.Format(
        E33ModEditor.Core.Loot.LootDist.Vanilla(catalog), "VANILLA (expected quantity × chance)"));

    var s = new E33ModEditor.Core.Loot.RandomizerKit.Settings();
    var acc = E33ModEditor.Core.Loot.LootDist.Randomized(catalog, s, new Random(1));
    foreach (var key in acc.Keys.ToList())
        acc[key] = new E33ModEditor.Core.Loot.LootDist.Cell();
    for (int i = 0; i < runs; i++)
    {
        var one = E33ModEditor.Core.Loot.LootDist.Randomized(catalog, s, new Random(1000 + i));
        foreach (var (k, cell) in one)
        {
            if (!acc.TryGetValue(k, out var a))
                acc[k] = a = new E33ModEditor.Core.Loot.LootDist.Cell();
            a.Expected += cell.Expected / runs;
            a.Slots += cell.Slots;
        }
    }
    Console.Write(E33ModEditor.Core.Loot.LootDist.Format(acc,
        $"RANDOMIZER mean over {runs} seeds (chests rolled; drops = archetype recipe)"));

    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_lootdist");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);
    var live = new E33ModEditor.Core.Loot.RandomizerKit.Settings
    {
        Seed = 42,
        Chests = true,
        EnemyDrops = true,
        Merchants = false,
        WeaponPassives = false,
        MonsterStats = false,
        WeaponStats = false,
    };
    E33ModEditor.Core.Loot.RandomizerKit.Apply(dir, live, catalog, null);
    var chestAsset = FileCopy.FindUnder(dir, "DT_ChestsContent.uasset");
    if (chestAsset != null)
    {
        var ed = new DataTableEditor(chestAsset);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in ed.Table.Table.Data)
        {
            if (row.Value.FirstOrDefault(p =>
                    E33ModEditor.Core.DataTableEditor.CleanName(p.Name) == "LootTableSections")
                is not UAssetAPI.PropertyTypes.Objects.ArrayPropertyData secs) continue;
            foreach (var sec in secs.Value.OfType<UAssetAPI.PropertyTypes.Structs.StructPropertyData>())
            {
                if (sec.Value.FirstOrDefault(p =>
                        E33ModEditor.Core.DataTableEditor.CleanName(p.Name) == "LootEntries")
                    is not UAssetAPI.PropertyTypes.Objects.ArrayPropertyData ents) continue;
                foreach (var ent in ents.Value.OfType<UAssetAPI.PropertyTypes.Structs.StructPropertyData>())
                {
                    var nm = ent.Value.FirstOrDefault(p =>
                        E33ModEditor.Core.DataTableEditor.CleanName(p.Name) == "ItemID")
                        as UAssetAPI.PropertyTypes.Objects.NamePropertyData;
                    var id = nm?.Value?.ToString() ?? "";
                    if (id.Length == 0) continue;
                    counts[id] = counts.GetValueOrDefault(id) + 1;
                }
            }
        }
        void Show(string id) => Console.WriteLine($"  {id} {counts.GetValueOrDefault(id)}");
        Console.WriteLine("live chest counts (seed 42):");
        Show("HealingTint_Shard");
        Show("EnergyTint_Shard");
        Show("ReviveTint_Shard");
        Show("PartyHealShard");
        Show("ConsumableUpgradeMaterial_Health");
        Show("ConsumableUpgradeMaterial_AP");
        Show("ConsumableUpgradeMaterial_Revive");
        Show("UpgradeMaterial_Level1");
        Show("UpgradeMaterial_Level2");
        Show("UpgradeMaterial_Level3");
        Show("UpgradeMaterial_Level4");
        Show("UpgradeMaterial_Level5");
        Console.WriteLine("  tint shards " +
            (counts.GetValueOrDefault("HealingTint_Shard")
             + counts.GetValueOrDefault("EnergyTint_Shard")
             + counts.GetValueOrDefault("ReviveTint_Shard")));
        Console.WriteLine("  tint items (should be 0) " +
            (counts.GetValueOrDefault("Consumable_Health_Level0")
             + counts.GetValueOrDefault("Consumable_Health_Level1")
             + counts.GetValueOrDefault("Consumable_Health_Level2")
             + counts.GetValueOrDefault("Consumable_Energy_Level0")
             + counts.GetValueOrDefault("Consumable_Energy_Level1")
             + counts.GetValueOrDefault("Consumable_Energy_Level2")
             + counts.GetValueOrDefault("Consumable_Revive_Level0")
             + counts.GetValueOrDefault("Consumable_Revive_Level1")
             + counts.GetValueOrDefault("Consumable_Revive_Level2")));
    }
    return 0;
}

static int CmdBalanceSmoke(string[] args)
{
    int fails = 0;
    void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "ok   " : "FAIL ") + what);
        if (!ok) fails++;
    }

    var defaults = new GameplayTweaksKit.Settings();
    Check(!defaults.BuffRegularEnemies, "regular-enemy buff is off by default");
    Check(!defaults.ReduceLuminaDrops, "lumina adjustment is off by default");
    Check(Math.Abs(defaults.LuminaDropMultiplier - 1.0) < 0.0001, "lumina multiplier default is 1.0");

    var roundTrip = new GameplayTweaksKit.Settings();
    roundTrip.FromJson(defaults.ToJson());
    Check(roundTrip.RegularEnemyHpBonus.SequenceEqual(RegularEnemyBuffKit.DefaultHpBonus)
          && roundTrip.RegularEnemySpeedBonus.SequenceEqual(RegularEnemyBuffKit.DefaultSpeedBonus)
          && roundTrip.RegularEnemyAttackBonus.SequenceEqual(RegularEnemyBuffKit.DefaultAttackBonus),
        "per-tier bonus arrays round-trip");

    var enabled = new GameplayTweaksKit.Settings();
    enabled.FromJson(JObject.Parse(
        """{"BuffRegularEnemies":true,"ReduceLuminaDrops":true,"LuminaDropMultiplier":0.5}"""));
    Check(enabled.BuffRegularEnemies && enabled.ReduceLuminaDrops,
        "enabled toggles load from gameplay_tweaks JSON");
    Check(enabled.ToJson().Value<bool?>("BuffRegularEnemies") == true,
        "enabled toggles are written back to gameplay_tweaks JSON");

    var catalog = NewLootCatalogWithExtras();
    catalog.Rebuild(
        JObject.Parse(File.ReadAllText(Path.Combine(EditorPaths.RepoRoot, "work", "chests.json"))),
        JObject.Parse(File.ReadAllText(Path.Combine(EditorPaths.RepoRoot, "work", "enemies.json"))),
        Array.Empty<(string, JObject)>());

    static JObject DumpTable(string stem)
    {
        var path = FileCopy.FindTablesAsset(stem + ".uasset")
            ?? throw new FileNotFoundException(stem + ".uasset not found under work/tables");
        return JObject.Parse(new DataTableEditor(path).Dump());
    }

    static JObject DumpStaged(string dir, string stem)
    {
        var path = FileCopy.FindUnder(dir, stem + ".uasset")
            ?? throw new FileNotFoundException(stem + ".uasset not staged in " + dir);
        return JObject.Parse(new DataTableEditor(path).Dump());
    }

    var vanillaEnemies = DumpTable("DT_jRPG_Enemies");
    int RowTier(string id, JObject row)
    {
        catalog.Enemies.TryGetValue(id, out var enemy);
        var level = E33ModEditor.Core.Loot.LootCatalog.ResolveEnemyLevel(
            row.Value<int?>("Level") ?? 0, enemy);
        var rowTier = E33ModEditor.Core.Loot.LootCatalog.TierFromLevel(level);
        return catalog.AreaTierForEnemy(id, enemy, rowTier, isBossRow: false);
    }

    bool IsRegular(JObject row, string id)
    {
        catalog.Enemies.TryGetValue(id, out var enemy);
        if (enemy is { CutContent: true }) return false;
        var arch = enemy?.Archetype ?? "Regular";
        if (arch is "Boss" or "Alpha" or "SuperBoss" or "Merchant") return false;
        if ((row.Value<bool?>("IsBoss") ?? false)
            && E33ModEditor.Core.Loot.RandomizerKit.IsRealBossRow(id, enemy, arch))
            return false;
        return row["EnemyScaling"] is JObject scaling
            && (scaling.Value<double?>("HP") ?? 0) > 0
            && (scaling.Value<double?>("Speed") ?? 0) > 0
            && (scaling.Value<double?>("PhysicalAttack") ?? 0) > 0;
    }

    string? FindRegular(int tier) => vanillaEnemies.Properties()
        .FirstOrDefault(p => IsRegular((JObject)p.Value, p.Name) && RowTier(p.Name, (JObject)p.Value) == tier)
        ?.Name;

    string? FindBoss() => vanillaEnemies.Properties()
        .FirstOrDefault(p =>
        {
            var arch = catalog.Enemies.GetValueOrDefault(p.Name)?.Archetype ?? "";
            return arch is "Boss" or "Alpha" or "SuperBoss"
                && p.Value["EnemyScaling"] is JObject
                && (p.Value["EnemyScaling"]?.Value<double?>("HP") ?? 0) > 0;
        })?.Name;

    var buffDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_balancesmoke");
    if (Directory.Exists(buffDir)) Directory.Delete(buffDir, true);
    Directory.CreateDirectory(buffDir);
    DataTableEditor.ResetMappings();
    var buffChanges = RegularEnemyBuffKit.Apply(buffDir, catalog);
    Check(buffChanges > 0, $"enemy buff changed {buffChanges} scaling field(s)");
    var buffedEnemies = DumpStaged(buffDir, "DT_jRPG_Enemies");

    void VerifyFactors(string? id, int tier, double hp, double speed, double attack, string label)
    {
        if (id is null)
        {
            Check(false, label + ": no suitable row found");
            return;
        }
        var before = (JObject)vanillaEnemies[id]!["EnemyScaling"]!;
        var after = (JObject)buffedEnemies[id]!["EnemyScaling"]!;
        bool hpOk = Math.Abs((after.Value<double?>("HP") ?? 0)
            - (before.Value<double?>("HP") ?? 0) * (1 + hp)) < 0.001;
        bool speedOk = Math.Abs((after.Value<double?>("Speed") ?? 0)
            - (before.Value<double?>("Speed") ?? 0) * (1 + speed)) < 0.001;
        bool atkOk = Math.Abs((after.Value<double?>("PhysicalAttack") ?? 0)
            - (before.Value<double?>("PhysicalAttack") ?? 0) * (1 + attack)) < 0.001
            && Math.Abs((after.Value<double?>("MagicalAttack") ?? 0)
            - (before.Value<double?>("MagicalAttack") ?? 0) * (1 + attack)) < 0.001;
        Check(hpOk && speedOk && atkOk, $"{label} {id}: HP/Speed/ATK factors applied");
    }

    VerifyFactors(FindRegular(1), 1,
        RegularEnemyBuffKit.DefaultHpBonus[0], RegularEnemyBuffKit.DefaultSpeedBonus[0],
        RegularEnemyBuffKit.DefaultAttackBonus[0], "T1 regular");
    VerifyFactors(FindRegular(5), 5,
        RegularEnemyBuffKit.DefaultHpBonus[4], RegularEnemyBuffKit.DefaultSpeedBonus[4],
        RegularEnemyBuffKit.DefaultAttackBonus[4], "T5 regular");

    var bossId = FindBoss();
    if (bossId is null)
    {
        Check(false, "boss row: no suitable row found");
    }
    else
    {
        var before = (JObject)vanillaEnemies[bossId]!["EnemyScaling"]!;
        var after = (JObject)buffedEnemies[bossId]!["EnemyScaling"]!;
        Check(before.ToString() == after.ToString(), $"boss {bossId}: EnemyScaling untouched");
    }

    // ── lumina reduction ──
    var lumDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_luminasmoke");
    if (Directory.Exists(lumDir)) Directory.Delete(lumDir, true);
    Directory.CreateDirectory(lumDir);
    DataTableEditor.ResetMappings();
    var lumChanges = E33ModEditor.Core.Loot.LuminaDropKit.Apply(lumDir, catalog, 0.5);
    Check(lumChanges > 0, $"lumina reduction changed {lumChanges} source(s)");
    var lumEnemies = DumpStaged(lumDir, "DT_jRPG_Enemies");

    static JObject? LuminaDrop(JObject table, string rowId)
    {
        if (table[rowId]?["PossibleLoot"] is not JArray loot) return null;
        return loot.OfType<JObject>().FirstOrDefault(e =>
            e["Item"]?["RowName"]?.ToString() == E33ModEditor.Core.Loot.LuminaDropKit.LuminaItem);
    }

    var onePointBefore = LuminaDrop(vanillaEnemies, "RD_Benisseur");
    var onePointAfter = LuminaDrop(lumEnemies, "RD_Benisseur");
    Check(onePointBefore?.Value<double?>("LootPercentageChance") == 20,
        "RD_Benisseur starts at a 20% lumina chance");
    Check(onePointAfter?.Value<double?>("LootPercentageChance") == 10,
        "RD_Benisseur one-point lumina chance halved to 10%");

    var stackBefore = LuminaDrop(vanillaEnemies, "QUEST_WeaponlessChalier");
    var stackAfter = LuminaDrop(lumEnemies, "QUEST_WeaponlessChalier");
    Check(stackBefore?.Value<int?>("Quantity") == 3 && stackAfter?.Value<int?>("Quantity") == 1,
        "3-stack quest lumina reduced to 1 (integer floor)");
    var bossLumBefore = LuminaDrop(vanillaEnemies, "Boss_Simon_ALPHA");
    var bossLumAfter = LuminaDrop(lumEnemies, "Boss_Simon_ALPHA");
    Check(bossLumBefore?.Value<int?>("Quantity") == 20 && bossLumAfter?.Value<int?>("Quantity") == 10,
        "20-stack superboss lumina halved to 10");

    var merchant = DumpStaged(lumDir, "DT_Merchant_Lumiere");
    Check(merchant["Consumable_LuminaPoint"]?.Value<int?>("Quantity") == 1,
        "merchant lumina stock reduced (vanilla 3 -> 1)");

    var vanillaChests = DumpTable("DT_ChestsContent");
    var lumChests = DumpStaged(lumDir, "DT_ChestsContent");
    static bool PureSingleLumina(JObject row)
    {
        if (row["LootTableSections"] is not JArray secs || secs.Count != 1) return false;
        if (secs[0]?["LootEntries"] is not JArray entries || entries.Count != 1) return false;
        return entries[0]?["ItemID"]?.ToString() == E33ModEditor.Core.Loot.LuminaDropKit.LuminaItem
            && (entries[0]?["Quantity"]?.Value<int?>() ?? 1) == 1;
    }

    var pureRows = vanillaChests.Properties()
        .Where(p => PureSingleLumina((JObject)p.Value)).Select(p => p.Name).ToList();
    int stayedLumina = 0, swappedChroma = 0, empty = 0;
    foreach (var id in pureRows)
    {
        var sections = lumChests[id]?["LootTableSections"] as JArray;
        var entries = sections is { Count: 1 } ? sections[0]?["LootEntries"] as JArray : null;
        if (entries is not { Count: > 0 })
        {
            empty++;
            continue;
        }
        var item = entries[0]?["ItemID"]?.ToString() ?? "";
        if (item == E33ModEditor.Core.Loot.LuminaDropKit.LuminaItem) stayedLumina++;
        else if (item.StartsWith("ChromaPack_", StringComparison.Ordinal)) swappedChroma++;
    }
    Check(pureRows.Count > 0, $"found {pureRows.Count} single-point pure-lumina chest rows");
    Check(stayedLumina == (pureRows.Count + 1) / 2 && swappedChroma == pureRows.Count / 2,
        $"pure-lumina chests: {stayedLumina} kept lumina, {swappedChroma} swapped to chroma");
    Check(empty == 0, "no reduced chest left without a drop");

    Check(!Directory.EnumerateFiles(lumDir, "BP_Dialogue_JudgeOfMercy.uasset",
            SearchOption.AllDirectories).Any(),
        "dialogue lumina (Blanche) is never staged by the reduction");

    // ── lumina increase ──
    var lumUpDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_luminasmoke_up");
    if (Directory.Exists(lumUpDir)) Directory.Delete(lumUpDir, true);
    Directory.CreateDirectory(lumUpDir);
    DataTableEditor.ResetMappings();
    var lumUpChanges = E33ModEditor.Core.Loot.LuminaDropKit.Apply(lumUpDir, catalog, 2.0);
    Check(lumUpChanges > 0, $"lumina increase changed {lumUpChanges} source(s)");
    var lumUpEnemies = DumpStaged(lumUpDir, "DT_jRPG_Enemies");
    Check(LuminaDrop(lumUpEnemies, "RD_Benisseur")?.Value<double?>("LootPercentageChance") == 40,
        "RD_Benisseur one-point lumina chance doubled to 40%");
    Check(LuminaDrop(lumUpEnemies, "QUEST_WeaponlessChalier")?.Value<int?>("Quantity") == 6,
        "3-stack quest lumina doubled to 6");
    Check(LuminaDrop(lumUpEnemies, "Boss_Simon_ALPHA")?.Value<int?>("Quantity") == 40,
        "20-stack superboss lumina doubled to 40");
    var upMerchant = DumpStaged(lumUpDir, "DT_Merchant_Lumiere");
    Check(upMerchant["Consumable_LuminaPoint"]?.Value<int?>("Quantity") == 6,
        "merchant lumina stock doubled (vanilla 3 -> 6)");
    var upChests = DumpStaged(lumUpDir, "DT_ChestsContent");
    int upChroma = 0;
    foreach (var id in pureRows)
    {
        var sections = upChests[id]?["LootTableSections"] as JArray;
        var entries = sections is { Count: 1 } ? sections[0]?["LootEntries"] as JArray : null;
        if (entries is { Count: > 0 }
            && (entries[0]?["ItemID"]?.ToString() ?? "").StartsWith("ChromaPack_", StringComparison.Ordinal))
            upChroma++;
    }
    Check(upChroma == 0, "increasing pure-lumina chests never swaps in chroma");
    Check(!Directory.EnumerateFiles(lumUpDir, "BP_Dialogue_JudgeOfMercy.uasset",
            SearchOption.AllDirectories).Any(),
        "dialogue lumina (Blanche) is never staged by the increase");

    Console.WriteLine(fails == 0 ? "balance-smoke OK" : $"balance-smoke FAILED ({fails})");
    return fails == 0 ? 0 : 1;
}

static int CmdPopupCapSmoke(string[] args)
{
    int cap = args.Length > 1 && !args[1].StartsWith('-')
        ? int.Parse(args[1]) : PictoPopupCapKit.DefaultCap;
    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_popupcap");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);

    int fails = 0;
    void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "ok   " : "FAIL ") + what);
        if (!ok) fails++;
    }

    PictoPopupCapKit.Apply(dir, cap);
    var path = FileCopy.FindUnder(dir, PictoPopupCapKit.AssetStem + ".uasset");
    Check(path is not null, "widget staged");
    if (path is null) { Console.WriteLine("popup-cap-smoke FAILED"); return 1; }
    Check(PictoPopupCapKit.Verify(path, cap), $"guard verifies at cap {cap}");

    // Re-applying with a different cap must update the literal, not insert twice.
    int cap2 = Math.Min(cap + 1, PictoPopupCapKit.MaxCap);
    PictoPopupCapKit.Apply(dir, cap2);
    path = FileCopy.FindUnder(dir, PictoPopupCapKit.AssetStem + ".uasset")!;
    Check(PictoPopupCapKit.Verify(path, cap2), $"guard verifies after cap update to {cap2}");

    // 0 = vanilla: the widget must disappear from the pack.
    if (args.Contains("--keep")) Console.WriteLine("ok   kept staged widget for inspection");
    else
    {
        PictoPopupCapKit.Apply(dir, 0);
        Check(FileCopy.FindUnder(dir, PictoPopupCapKit.AssetStem + ".uasset") is null,
            "cap 0 removes the widget");
    }

    Console.WriteLine(fails == 0 ? "popup-cap-smoke OK" : $"popup-cap-smoke FAILED ({fails})");
    return fails == 0 ? 0 : 1;
}

static int CmdTweaksSmoke(string[] args)
{
    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_tweakssmoke");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);
    if (!new GameplayTweaksKit.Settings().DisableTutorials)
    {
        Console.Error.WriteLine("tweaks-smoke FAILED: DisableTutorials is not the default");
        return 1;
    }
    var s = new GameplayTweaksKit.Settings
    {
        LevelCap = 250,
        AttributePointsPerLevel = 2,
        LuminaPerLevel = 2,
        DodgeWindow = 0.165,
        ParryWindow = 0.1125,
        TightenDefenseWindows = true,
        DisableTutorials = true,
        AttributeCap = 200,
        MaxConsecutiveTurns = 3,
        AutoPictoMastery = true,
    };
    var n = GameplayTweaksKit.Apply(dir, s);
    Console.WriteLine("applied " + n);
    var fl = GameplayTweaksKit.FindAsset(dir, "FL_jRPG_CustomFunctionLibrary.uasset")
        ?? throw new FileNotFoundException("FL library");
    var bp = GameplayTweaksKit.FindAsset(dir, "BP_CharacterData.uasset")
        ?? throw new FileNotFoundException("BP_CharacterData");
    Console.WriteLine("GetMaximumLevel:");
    DumpFnInts(fl, "GetMaximumLevel");
    Console.WriteLine("GetMaxLuminaPoints:");
    DumpFnInts(bp, "GetMaxLuminaPoints");
    Console.WriteLine("GetMaxLuminaPointsDetails:");
    DumpFnInts(bp, "GetMaxLuminaPointsDetails");
    Console.WriteLine("SetLevel IntConsts:");
    DumpFnInts(bp, "SetLevel");

    // Attribute point cap: all five scaling tables extended to Points_200, and
    // the attributes-wheel clamp lifted off 99.
    foreach (var attr in new[] { "Agility", "Defense", "Luck", "Vitality", "Strength" })
    {
        var t = GameplayTweaksKit.FindAsset(dir, "DT_AttributeStatScaling_" + attr + ".uasset");
        if (t is null)
        {
            Console.Error.WriteLine($"tweaks-smoke FAILED: DT_AttributeStatScaling_{attr} not staged");
            return 1;
        }
        var ed = new DataTableEditor(t);
        // The table is extended to max(AttributeCap, LevelCap x pointsPerLevel).
        int want = s.AttributeTableCap;
        Console.WriteLine($"{attr}: Points_99={ed.HasRow("Points_99")} Points_100={ed.HasRow("Points_100")} Points_200={ed.HasRow("Points_200")} Points_{want}={ed.HasRow("Points_" + want)} Points_{want + 1}={ed.HasRow("Points_" + (want + 1))}");
        if (!ed.HasRow("Points_100") || !ed.HasRow("Points_200")
            || !ed.HasRow("Points_" + want) || ed.HasRow("Points_" + (want + 1)))
        {
            Console.Error.WriteLine($"tweaks-smoke FAILED: DT_AttributeStatScaling_{attr} row range wrong (want 0..{want})");
            return 1;
        }
        // Curve shape: 1-80 is a straight line, then the per-point gain accelerates.
        var curve = Newtonsoft.Json.Linq.JObject.Parse(ed.Dump());
        double Val(int n)
        {
            var stats = curve["Points_" + n]?["Stats"] as Newtonsoft.Json.Linq.JObject;
            var first = stats?.Properties().FirstOrDefault();
            return first is null ? double.NaN : first.Value.ToObject<double>();
        }
        double stepEarly = Val(80) - Val(60);
        double stepMid = Val(100) - Val(80);
        double stepLate = Val(200) - Val(180);
        bool monotone = true;
        for (int p = 1; p <= 200; p++)
            if (!(Val(p) > Val(p - 1))) { monotone = false; break; }
        Console.WriteLine(
            $"  {attr} curve: 60->80={stepEarly:0.##} 80->100={stepMid:0.##} 180->200={stepLate:0.##} " +
            $"v80={Val(80):0.#} v99={Val(99):0.#} v200={Val(200):0.#}");
        if (!monotone || Math.Abs((Val(40) - Val(20)) - stepEarly) > 0.05
            || !(stepLate > stepEarly))
        {
            Console.Error.WriteLine(
                $"tweaks-smoke FAILED: DT_AttributeStatScaling_{attr} curve is not linear-then-accelerating");
            return 1;
        }
    }
    var wheel = GameplayTweaksKit.FindAsset(dir, "WBP_Raw_Attributes_Display.uasset");
    if (wheel is null)
    {
        Console.Error.WriteLine("tweaks-smoke FAILED: attributes widget not staged for the cap patch");
        return 1;
    }
    DumpFnInts(wheel, "SetAllowedMaxValue");
    DumpFnInts(wheel, "SetAllowedMinValue");

    // Windows now patch the difficulty table (Expedition + Expert rows), not
    // the battle-base CDO. Verify the rows landed and no CDO-patched base
    // (retired patch artifact) is staged.
    var dt = GameplayTweaksKit.FindAsset(dir, "DT_Balancing_BattleDifficultySettings.uasset")
        ?? throw new FileNotFoundException("difficulty settings table");
    var rows = Newtonsoft.Json.Linq.JObject.Parse(new DataTableEditor(dt).Dump());
    double Row(string name) => rows[name]?["FloatValue"] is Newtonsoft.Json.Linq.JValue v ? (double)v.Value : double.NaN;
    Console.WriteLine($"Dodge Normal/Hard = {Row("ImmuneDuration_Dodge_Normal")}/{Row("ImmuneDuration_Dodge_Hard")} (expect {s.DodgeWindow}/{s.DodgeWindow})");
    Console.WriteLine($"Parry Normal/Hard = {Row("ImmuneDuration_Parry_Normal")}/{Row("ImmuneDuration_Parry_Hard")} (expect {s.ParryWindow}/{s.ParryWindow})");
    Console.WriteLine($"Dodge/Parry Easy untouched = {Row("ImmuneDuration_Dodge_Easy")}/{Row("ImmuneDuration_Parry_Easy")} (expect 0.5/0.35)");
    if (Math.Abs(Row("ImmuneDuration_Dodge_Normal") - s.DodgeWindow) > 0.0001
        || Math.Abs(Row("ImmuneDuration_Dodge_Hard") - s.DodgeWindow) > 0.0001
        || Math.Abs(Row("ImmuneDuration_Parry_Normal") - s.ParryWindow) > 0.0001
        || Math.Abs(Row("ImmuneDuration_Parry_Hard") - s.ParryWindow) > 0.0001
        || Math.Abs(Row("ImmuneDuration_Dodge_Easy") - 0.5) > 0.0001
        || Math.Abs(Row("ImmuneDuration_Parry_Easy") - 0.35) > 0.0001)
    {
        Console.Error.WriteLine("tweaks-smoke FAILED: difficulty table rows wrong");
        return 1;
    }

    // Tutorial disable: the tutorial system must return false and the narrative
    // encounter rows must point at their no-tutorial enemy variants.
    var tutorialSystem = GameplayTweaksKit.FindAsset(dir, TutorialDisableKit.TutorialSystemAsset + ".uasset");
    if (tutorialSystem is null || !TutorialDisableKit.VerifyTutorialSystem(tutorialSystem))
    {
        Console.Error.WriteLine("tweaks-smoke FAILED: tutorial system was not force-disabled");
        return 1;
    }
    var encounters = GameplayTweaksKit.FindAsset(dir, TutorialDisableKit.EncountersTable + ".uasset");
    if (encounters is null || !TutorialDisableKit.VerifyEncounters(encounters, disabled: true))
    {
        Console.Error.WriteLine("tweaks-smoke FAILED: tutorial encounters still reference tutorial enemies");
        return 1;
    }
    Console.WriteLine("tutorial disable: tutorial system false + no-tutorial encounters OK");

    // Auto Picto Mastery: the controller must carry the built-in
    // Sandfall.LearnAnyLuminaAfterOneBattle CVar with its default flipped on.
    var controller = GameplayTweaksKit.FindAsset(dir, AutoPictoMasteryKit.ControllerAsset + ".uasset");
    if (controller is null || !AutoPictoMasteryKit.Verify(controller))
    {
        Console.Error.WriteLine("tweaks-smoke FAILED: AutoPictoMastery CVar default was not enabled");
        return 1;
    }
    Console.WriteLine("auto picto mastery: LearnAnyLuminaAfterOneBattle default = true");

    // The retired CDO window patch leaves no battle base staged. The base-attack lumina
    // and Cheater are the only guarded Force Play Again sources; gradient attacks and
    // Thermal Transfer stay unguarded and are bounded by the cap below.

    // Consecutive turns: Force Play Again keeps vanilla's notPrevent term but replaces
    // the dedup-buff presence test with a count of the buffs stacked this window plus a
    // per-action forced-queue test, so bonus turns are capped at MaxConsecutiveTurns
    // instead of being unbounded.
    var cbs = GameplayTweaksKit.FindAsset(dir, "AC_jRPG_CharacterBattleStats.uasset");
    if (cbs is null)
    {
        Console.Error.WriteLine("tweaks-smoke FAILED: AC_jRPG_CharacterBattleStats not staged for the consecutive-turns patch");
        return 1;
    }
    if (!GameplayTweaksKit.VerifyTurnsPatch(cbs, s.MaxConsecutiveTurns))
    {
        Console.Error.WriteLine("tweaks-smoke FAILED: Force Play Again cap patch malformed");
        return 1;
    }
    // The dedup buff must stack, or GetActiveBuffCounts never leaves 1 and the cap is moot.
    var turnBuff = GameplayTweaksKit.FindAsset(dir, "BP_BattleBuff_RemoveItselfAtAnotherCharacterTurn.uasset");
    if (turnBuff is null || !GameplayTweaksKit.VerifyTurnBuffStack(turnBuff))
    {
        Console.Error.WriteLine("tweaks-smoke FAILED: turn buff StackingType not StackNew");
        return 1;
    }
    Console.WriteLine("consecutive turns: Force Play Again capped by count + per-action queue");

    // Gradient charges: the gauge BP carries the raised cap, and the battle HUD
    // widget (which has its own copy of the max and hard-codes the per-charge
    // layout for three charges) must be rebuilt for MaxCharges+1 states.
    var gaugeBp = GameplayTweaksKit.FindAsset(dir, "BP_GradientGaugeSystem.uasset");
    var gaugeWidget = GameplayTweaksKit.FindAsset(dir, "WBP_GradientChargeGauge.uasset");
    if (gaugeBp is null || gaugeWidget is null)
    {
        Console.Error.WriteLine("tweaks-smoke FAILED: gradient gauge assets not staged");
        return 1;
    }
    if (!CdoIntEquals(gaugeBp, "MaxCharges", s.MaxGradientCharges))
    {
        Console.Error.WriteLine("tweaks-smoke FAILED: BP_GradientGaugeSystem.MaxCharges wrong");
        return 1;
    }
    if (!CheckGradientWidget(gaugeWidget, s.MaxGradientCharges + 1))
    {
        Console.Error.WriteLine("tweaks-smoke FAILED: WBP_GradientChargeGauge not rebuilt for the raised cap");
        return 1;
    }
    Console.WriteLine($"gradient gauge: HUD widget rebuilt for {s.MaxGradientCharges + 1} states");

    // Picto/passive proc cards: the battle HUD container must carry the guard.
    var popup = GameplayTweaksKit.FindAsset(dir, PictoPopupCapKit.AssetStem + ".uasset");
    if (popup is null || !PictoPopupCapKit.Verify(popup, s.PictoPopupCap))
    {
        Console.Error.WriteLine("tweaks-smoke FAILED: WBP_PassivePopupContainer cap guard missing or malformed");
        return 1;
    }
    Console.WriteLine($"popup cap: passive/picto cards capped at {s.PictoPopupCap}");

    // Revert parity: a fully-vanilla settings object must delete the attribute
    // tables and the patched widget, and HasLeftoverAssets must still see them so
    // the GUI does not bail out with "no changes detected" and ship them stale.
    var name = new[] { "DT_AttributeStatScaling_Agility", "DT_AttributeStatScaling_Defense",
                       "DT_AttributeStatScaling_Luck", "DT_AttributeStatScaling_Vitality",
                       "DT_AttributeStatScaling_Strength", "WBP_Raw_Attributes_Display",
                       "WBP_GradientChargeGauge", "BP_BattleBuff_RemoveItselfAtAnotherCharacterTurn",
                       "BP_LU_AttackPlayAgain", "BP_LU_Cheater", "BP_jRPG_Character_Battle_Base",
                       "BP_Battle_SkillScript_ThermalTransfer",
                       "DT_Balancing_BattleDifficultySettings",
                       "BP_TutorialSystemComponent",
                       AutoPictoMasteryKit.ControllerAsset,
                       PictoPopupCapKit.AssetStem };
    if (Array.TrueForAll(name, x => GameplayTweaksKit.FindAsset(dir, x + ".uasset") != null)
        && !GameplayTweaksKit.HasLeftoverAssets(dir))
    {
        Console.Error.WriteLine("tweaks-smoke FAILED: HasLeftoverAssets does not see the attribute-cap assets (revert would ship them stale)");
        return 1;
    }
    var vanilla = new GameplayTweaksKit.Settings
    {
        LevelCap = GameplayTweaksKit.VanillaLevelCap,
        LuminaPerLevel = GameplayTweaksKit.VanillaLuminaPerLevel,
        AttributePointsPerLevel = GameplayTweaksKit.VanillaAttributePointsPerLevel,
        MaxGradientCharges = GameplayTweaksKit.VanillaMaxGradientCharges,
        BurnMultiplier = GameplayTweaksKit.VanillaBurnMultiplier,
        BurnPlayerScale = GameplayTweaksKit.VanillaBurnPlayerScale,
        CritRateDamageScaling = false,
        CritDamageIncrease = GameplayTweaksKit.VanillaCritDamageIncrease,
        DisableTutorials = false,
        MaskKeeperFullMoveset = false,
        SireneFullMoveset = false,
        PictoPopupCap = GameplayTweaksKit.VanillaPictoPopupCap,
    };
    if (!vanilla.IsVanilla)
    {
        Console.Error.WriteLine("tweaks-smoke FAILED: the revert settings object is not vanilla (fix the defaults used here)");
        return 1;
    }
    GameplayTweaksKit.Apply(dir, vanilla);
    foreach (var stem in name)
        if (GameplayTweaksKit.FindAsset(dir, stem + ".uasset") is { } leftover)
        {
            Console.Error.WriteLine($"tweaks-smoke FAILED: {stem} not removed on revert to vanilla");
            return 1;
        }
    Console.WriteLine("revert to vanilla removed the attribute-cap assets");

    // Crit damage: BP_BattleDamages is staged with the base-addend rewrite and/or the
    // crit-rate splice, verified, and removed again on revert.
    GameplayTweaksKit.Settings CritOnly(bool scaling, float addend) => new()
    {
        LevelCap = GameplayTweaksKit.VanillaLevelCap,
        LuminaPerLevel = GameplayTweaksKit.VanillaLuminaPerLevel,
        AttributePointsPerLevel = GameplayTweaksKit.VanillaAttributePointsPerLevel,
        MaxGradientCharges = GameplayTweaksKit.VanillaMaxGradientCharges,
        BurnMultiplier = GameplayTweaksKit.VanillaBurnMultiplier,
        BurnPlayerScale = GameplayTweaksKit.VanillaBurnPlayerScale,
        CritRateDamageScaling = scaling,
        CritDamageIncrease = addend,
        DisableTutorials = false,
        MaskKeeperFullMoveset = false,
        SireneFullMoveset = false,
        PictoPopupCap = GameplayTweaksKit.VanillaPictoPopupCap,
    };

    void CheckCrit(string label, GameplayTweaksKit.Settings settings)
    {
        var cd = Path.Combine(EditorPaths.RepoRoot, "work", "mod_critsmoke");
        if (Directory.Exists(cd)) Directory.Delete(cd, true);
        Directory.CreateDirectory(cd);
        if (settings.IsVanilla)
            throw new InvalidDataException(label + ": settings do not leave the vanilla state");
        GameplayTweaksKit.Apply(cd, settings);
        var bp = GameplayTweaksKit.FindAsset(cd, "BP_BattleDamages.uasset")
            ?? throw new InvalidDataException(label + ": BP_BattleDamages not staged");
        if (!CritDamageKit.Verify(bp, settings.CritDamageIncrease, settings.CritRateDamageScaling))
            throw new InvalidDataException(label + ": BP_BattleDamages edit missing or malformed");
        if (!GameplayTweaksKit.HasLeftoverAssets(cd))
            throw new InvalidDataException(label + ": HasLeftoverAssets does not see BP_BattleDamages");
        var st = GameplayTweaksKit.FindAsset(cd, "ST_UI_GameMenu.uasset")
            ?? throw new InvalidDataException(label + ": ST_UI_GameMenu not staged");
        var want = GameplayTweaksKit.DescribeCritRate(settings);
        if (!E33ModEditor.Core.DataTableEditor.OpenStringTable(st)
                .TryGetValue("ST_GM_STAT_CRIT_RATE_DESC", out var got) || got != want)
            throw new InvalidDataException($"{label}: crit stat text = '{got}', want '{want}'");
        Console.WriteLine($"crit damage [{label}]: BP_BattleDamages verified; attribute text = \"{got}\"");
    }

    try
    {
        CheckCrit("scaling only", CritOnly(scaling: true, GameplayTweaksKit.VanillaCritDamageIncrease));
        CheckCrit("addend only", CritOnly(scaling: false, addend: 1.0f));
        CheckCrit("addend + scaling", CritOnly(scaling: true, addend: 1.0f));
    }
    catch (InvalidDataException ex)
    {
        Console.Error.WriteLine("tweaks-smoke FAILED: " + ex.Message);
        return 1;
    }

    GameplayTweaksKit.Apply(Path.Combine(EditorPaths.RepoRoot, "work", "mod_critsmoke"), vanilla);
    if (GameplayTweaksKit.FindAsset(Path.Combine(EditorPaths.RepoRoot, "work", "mod_critsmoke"), "BP_BattleDamages.uasset") is not null)
    {
        Console.Error.WriteLine("tweaks-smoke FAILED: BP_BattleDamages not removed on revert to vanilla");
        return 1;
    }
    Console.WriteLine("revert to vanilla removed BP_BattleDamages");
    return 0;

    static bool CdoIntEquals(string uasset, string field, int want)
    {
        var map = DataTableEditor.Mappings;
        var u = map != null
            ? new UAssetAPI.UAsset(uasset, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
            : new UAssetAPI.UAsset(uasset, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
        var cdo = u.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
        return cdo?.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == field)
            is UAssetAPI.PropertyTypes.Objects.IntPropertyData ip && ip.Value == want;
    }

    // The HUD widget must carry the raised max, per-charge fill arrays sized to the
    // state count, and N-case colour/scale switches.
    static bool CheckGradientWidget(string uasset, int states)
    {
        var map = DataTableEditor.Mappings;
        var u = map != null
            ? new UAssetAPI.UAsset(uasset, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
            : new UAssetAPI.UAsset(uasset, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
        var cdo = u.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
        if (cdo is null) return false;
        var mc = cdo.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "MaxCharges")
            as UAssetAPI.PropertyTypes.Objects.IntPropertyData;
        var pt = cdo.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "PercentageTarget")
            as UAssetAPI.PropertyTypes.Objects.ArrayPropertyData;
        var cp = cdo.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "CurrentPercentage")
            as UAssetAPI.PropertyTypes.Objects.ArrayPropertyData;
        int colourCases = SwitchCaseCount(u, "GetChargeColor");
        int scaleCases = SwitchCaseCount(u, "SetCharges");
        string fillReadSource = FillReadSource(u);
        bool fillWrite = SetProgressWritesScalar(u);
        string initTarget = SetTextTarget(u, "Initialize");
        string setTarget = SetTextTarget(u, "SetCharges");
        string pattern = PatternText(u, "Initialize");
        string initArg = CallArgName(u, "Initialize", "Conv_IntToInt64");
        string setArg = CallArgName(u, "SetCharges", "Conv_IntToText");
        string slot0 = FirstSlotContent(u, "HorizontalBox_0");
        int widthCalls = VirtualCallCount(u, "Initialize", "SetTextMinDesiredWidth")
                       + VirtualCallCount(u, "Initialize", "SetTextMaxDesiredWidth");
        int justifyCalls = VirtualCallCount(u, "Initialize", "SetJustification");
        string slotPadding = SlotPadding(u, "GridSlot_3");
        Console.WriteLine($"  widget: MaxCharges={mc?.Value} PercentageTarget={pt?.Value?.Length} CurrentPercentage={cp?.Value?.Length} " +
                          $"colourCases={colourCases} scaleCases={scaleCases} " +
                          $"(want {states - 1}/{states}/{states}/{states}/{states})");
        Console.WriteLine($"  widget fill/text: SetProgressWritesScalar={fillWrite} fillSource={fillReadSource} " +
                          $"Initialize.SetText={initTarget} SetCharges.SetText={setTarget} " +
                          $"pattern=\"{pattern}\" initArg={initArg} setArg={setArg} widthCalls={widthCalls} " +
                          $"justifyCalls={justifyCalls} GridSlot_3.padding={slotPadding} HorizontalBox_0[0]={slot0}");
        return mc?.Value == states - 1
            && pt?.Value?.Length == states
            && cp?.Value?.Length == states
            && colourCases == states
            && scaleCases == states
            && fillWrite && fillReadSource == "PreviewProgress"
            && initTarget == "MaxValueText"
            && setTarget == "CurrentValueText"
            && pattern == "/{MaxCharges}"
            && initArg == "MaxCharges"
            && setArg == "Current Charges"
            && slot0 == "Overlay_5"
            && widthCalls == 4
            && justifyCalls == 2;
    }

    static string SlotPadding(UAssetAPI.UAsset u, string slotName)
    {
        var exp = u.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString() == slotName);
        if (exp?.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "Padding")
            is not UAssetAPI.PropertyTypes.Structs.StructPropertyData sp || sp.Value is null) return "?";
        return string.Join(",", sp.Value.OfType<UAssetAPI.PropertyTypes.Objects.FloatPropertyData>()
            .Select(x => x.Value.ToString("0.#")));
    }

    static int VirtualCallCount(UAssetAPI.UAsset u, string fnName, string callee)
    {
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == fnName);
        if (fn?.ScriptBytecode is null) return -1;
        int n = 0;
        E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_VirtualFunction vf
                && vf.VirtualFunctionName.ToString() == callee) n++;
        });
        return n;
    }

    static string PatternText(UAssetAPI.UAsset u, string fnName)
    {
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == fnName);
        if (fn?.ScriptBytecode is null) return "";
        string found = "";
        E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode, e =>
        {
            if (found.Length > 0) return;
            if (e is not UAssetAPI.Kismet.Bytecode.Expressions.EX_TextConst tc) return;
            if (tc.Value.TextLiteralType != UAssetAPI.Kismet.Bytecode.EBlueprintTextLiteralType.InvariantText) return;
            if (tc.Value.InvariantLiteralString is UAssetAPI.Kismet.Bytecode.Expressions.EX_StringConst s
                && s.Value.Length > 0) found = s.Value;
        });
        return found;
    }

    static string CallArgName(UAssetAPI.UAsset u, string fnName, string callee)
    {
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == fnName);
        if (fn?.ScriptBytecode is null) return "";
        string found = "";
        E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode, e =>
        {
            if (e is not UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm) return;
            if (cm.StackNode is null || !cm.StackNode.IsImport()) return;
            if (cm.StackNode.ToImport(u).ObjectName.ToString() != callee) return;
            if (cm.Parameters is not { Length: > 0 }) return;
            if (cm.Parameters[0] is UAssetAPI.Kismet.Bytecode.Expressions.EX_InstanceVariable iv)
                found = iv.Variable?.New?.Path?[0].ToString() ?? "";
        });
        return found;
    }

    static string FillReadSource(UAssetAPI.UAsset u)
    {
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "SetGaugeLerpAlpha");
        if (fn?.ScriptBytecode is null) return "";
        string src = "";
        E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode, e =>
        {
            if (e is not UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm) return;
            if (cm.StackNode is null || !cm.StackNode.IsImport()) return;
            if (cm.StackNode.ToImport(u).ObjectName.ToString() != "Lerp") return;
            if (cm.Parameters is not { Length: 3 }) return;
            src = cm.Parameters[0] switch
            {
                UAssetAPI.Kismet.Bytecode.Expressions.EX_InstanceVariable iv =>
                    iv.Variable?.New?.Path?[0].ToString() ?? "",
                UAssetAPI.Kismet.Bytecode.Expressions.EX_ArrayGetByRef => "array",
                _ => "?",
            };
        });
        return src;
    }

    static bool SetProgressWritesScalar(UAssetAPI.UAsset u)
    {
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "SetProgress");
        if (fn?.ScriptBytecode is null) return false;
        bool ok = false;
        E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode, e =>
        {
            if (e is not UAssetAPI.Kismet.Bytecode.Expressions.EX_Let l) return;
            if (l.Variable is not UAssetAPI.Kismet.Bytecode.Expressions.EX_InstanceVariable iv) return;
            if (iv.Variable?.New?.Path?[0].ToString() != "PreviewProgress") return;
            if (l.Expression is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable lv
                && lv.Variable?.New?.Path?[0].ToString() == "TargetProgress") ok = true;
        });
        return ok;
    }

    static string SetTextTarget(UAssetAPI.UAsset u, string fnName)
    {
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == fnName);
        if (fn?.ScriptBytecode is null) return "";
        string found = "";
        E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode, e =>
        {
            if (e is not UAssetAPI.Kismet.Bytecode.Expressions.EX_Context ctx) return;
            if (ctx.ContextExpression is not UAssetAPI.Kismet.Bytecode.Expressions.EX_VirtualFunction vf) return;
            if (vf.VirtualFunctionName.ToString() != "Set Text") return;
            if (ctx.ObjectExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_InstanceVariable iv)
                found = iv.Variable?.New?.Path?[0].ToString() ?? "";
        });
        return found;
    }

    static string FirstSlotContent(UAssetAPI.UAsset u, string panelName)
    {
        var exp = u.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString() == panelName);
        if (exp?.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "Slots")
            is not UAssetAPI.PropertyTypes.Objects.ArrayPropertyData a
            || a.Value is null || a.Value.Length == 0) return "";
        if (a.Value[0] is not UAssetAPI.PropertyTypes.Objects.ObjectPropertyData o
            || !o.Value.IsExport()) return "";
        var slot = o.Value.ToExport(u) as UAssetAPI.ExportTypes.NormalExport;
        if (slot?.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "Content")
            is not UAssetAPI.PropertyTypes.Objects.ObjectPropertyData content
            || !content.Value.IsExport()) return "";
        return content.Value.ToExport(u)?.ObjectName.ToString() ?? "";
    }

    static int SwitchCaseCount(UAssetAPI.UAsset u, string fnName)
    {
        foreach (var fn in u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>())
        {
            if (fn.ObjectName.ToString() != fnName || fn.ScriptBytecode is null) continue;
            int n = -1;
            E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode, e =>
            {
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_SwitchValue sv) n = sv.Cases.Length;
            });
            return n;
        }
        return -1;
    }

    static void DumpFnInts(string uasset, string fnName)
    {
        var map = DataTableEditor.Mappings;
        var u = map != null
            ? new UAssetAPI.UAsset(uasset, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
            : new UAssetAPI.UAsset(uasset, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
        foreach (var fn in u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>())
        {
            if (fn.ObjectName.ToString() != fnName || fn.ScriptBytecode is null) continue;
            void Walk(IEnumerable<UAssetAPI.Kismet.Bytecode.KismetExpression> exprs)
            {
                foreach (var e in exprs)
                {
                    if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst c)
                        Console.WriteLine("  IntConst " + c.Value);
                    foreach (var f in e.GetType().GetFields())
                    {
                        var v = f.GetValue(e);
                        if (v is UAssetAPI.Kismet.Bytecode.KismetExpression ke) Walk(new[] { ke });
                        else if (v is UAssetAPI.Kismet.Bytecode.KismetExpression[] arr) Walk(arr);
                    }
                    foreach (var p in e.GetType().GetProperties())
                    {
                        if (p.GetIndexParameters().Length > 0) continue;
                        var v = p.GetValue(e);
                        if (v is UAssetAPI.Kismet.Bytecode.KismetExpression ke) Walk(new[] { ke });
                        else if (v is UAssetAPI.Kismet.Bytecode.KismetExpression[] arr) Walk(arr);
                    }
                }
            }
            Walk(fn.ScriptBytecode);
        }
    }
}

/// <summary>
/// Verifies the Skill Overhaul apply pipeline: an enabled change lands in the
/// mod table, then disabling it reverts the cell to vanilla on the next pack.
/// Runs a synthetic round-trip plus the real Lune catalog (all-defaults).
/// </summary>
static int CmdSkillDescSmoke(string[] args)
{
    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_descsmoke");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);
    DataTableEditor.ResetMappings();
    int fails = 0;
    void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "ok   " : "FAIL ") + what);
        if (!ok) fails++;
    }

    int n = SkillDescriptionsKit.Apply(dir);
    Check(n == 50, $"staged {n} changes (expect 50: 48 string entries + 2 keyword styles)");

    // Keyword style row: gold, no icon.
    var dt = Directory.EnumerateFiles(dir, "DT_RichTextDecorator_KeywordStyles.uasset", SearchOption.AllDirectories).First();
    var dtJson = Newtonsoft.Json.Linq.JObject.Parse(new DataTableEditor(dt).Dump());
    var dmg = dtJson["DamageMultiplier"] as Newtonsoft.Json.Linq.JObject;
    Check(dmg != null, "DamageMultiplier keyword row added");
    if (dmg != null)
    {
        var col = dmg["TextColor"]?["SpecifiedColor"]?["SpecifiedColor"]?.ToString() ?? "";
        Check(col.Contains("0.693872") && col.Contains("0.527115") && col.Contains("0.23074"), $"gold color = {col}");
        Check((dmg["Icon"]?.ToString() ?? "") == "", "no icon on DamageMultiplier");
    }
    var blight = dtJson["Debuff_Blight"] as Newtonsoft.Json.Linq.JObject;
    Check(blight != null, "Debuff_Blight keyword row added");
    if (blight != null)
    {
        var col = blight["TextColor"]?["SpecifiedColor"]?["SpecifiedColor"]?.ToString() ?? "";
        Check(col.Contains("0.55") && col.Contains("0.8"), $"blight purple color = {col}");
        Check((blight["Icon"]?.ToString() ?? "") == "", "no icon on Debuff_Blight");
    }

    // String entries: base value + overcharge effect, vanilla info preserved.
    var st = Directory.EnumerateFiles(dir, "ST_MainCharacters_Skills.uasset", SearchOption.AllDirectories).First();
    var table = DataTableEditor.OpenStringTable(st);
    string Get(string key) => table.TryGetValue(key, out var v) ? v : "";
    string imm = Get("Lune_Immolation_Description_Long");
    Check(imm.Contains("Deals <keyword id=\"DamageMultiplier\">0.46x</> single target"), "Immolation base 0.46x (was 'low')");
    Check(imm.Contains(": deal <keyword id=\"DamageMultiplier\">1.43x</> more damage"), "Immolation overcharge 1.43x");
    Check(imm.Contains("Applies 2 more <keyword id=\"StatusEffect_Burn\">Burns</>"), "Immolation Marked line kept");
    var may = Get("Lune_Mayhem_Description_Long");
    Check(may.Contains("Deals <keyword id=\"DamageMultiplier\">0.11x</> to <keyword id=\"DamageMultiplier\">2.61x</> elemental damage in total"), "Mayhem range 0.11x -> 2.61x total, no dark");
    Check(may.Contains("doubled for each <img id=\"Stain_Dark\"/> consumed"), "Mayhem dark doubling");
    Check(may.IndexOf("Stain_Dark") == may.LastIndexOf("Stain_Dark"), "Mayhem dark line appears exactly once");
    Check(Get("Lune_SkyBreak_Description_Short").Contains("<keyword id=\"DamageMultiplier\">70.00x</>"), "Sky Break 70.00x (was 'extreme')");
    var gen = Get("Lune_ElementalGenesis_Description_Long");
    Check(gen.Contains("Deals <keyword id=\"DamageMultiplier\">0.45x</> damage per hit"), "Genesis base 0.45x per hit");
    Check(gen.Contains("Can only be cast with <img id=\"Stain_Lightning\"/><img id=\"Stain_Earth\"/><img id=\"Stain_Fire\"/><img id=\"Stain_Ice\"/>"), "Genesis stain requirement kept");
    var ele = Get("Lune_Electrify_Description_Long");
    Check(ele.Contains("Deals <keyword id=\"DamageMultiplier\">0.08x</>"), "Electrify base 0.08x per hit");
    Check(!ele.Contains("to generate one Light Stain"), "Electrify light-stain line removed (fire->Light CreateStainByType removed)");
    var sc = Get("Lune_StormCaller_Description_Long");
    Check(sc.Contains("receive <keyword id=\"DamageMultiplier\">0.06x</>") && sc.Contains("and <keyword id=\"DamageMultiplier\">0.06x</>"), "Storm Caller 0.06x both events (clamp(3)/16)");
    Check(sc.Contains("for double thunder strikes on turn end"), "Storm Caller double-strike effect kept");
    var tq = Get("Lune_Terraquake_Description_Long");
    Check(tq.Contains("Duration: 1 turn.") && tq.Contains("to increase the Duration to 3 turns."), "Terraquake durations 1 -> 3 turns");
    Check(Get("Lune_Typhoon_Description_Long").Contains("Heals</> allies by 10% Health"), "Typhoon heal 10%");
    Check(Get("Lune_Hell_Description_Long").Contains(": deal <keyword id=\"DamageMultiplier\">3.33x</> more damage"), "Hell overcharge 3.33x");
    Check(Get("Lune_Tremor_Description_Long").Contains("Removes all enemies' <keyword id=\"Shield\">Shields</>"), "Tremor shield line kept");

    // Earth moves: break damage = 1.3 × base total shown in the description.
    Check(Get("Lune_Earthquake_Description_Long").Contains("Earth</> damage and <keyword id=\"DamageMultiplier\">0.90x</> <keyword id=\"BreakDamage\">break damage</> to all enemies"), "Earthquake break 0.90x");
    Check(Get("Lune_Earthquake_Description_Short").Contains("Earth</> damage and <keyword id=\"DamageMultiplier\">0.90x</> <keyword id=\"BreakDamage\">break damage</> to all enemies"), "Earthquake break 0.90x (Short)");
    Check(Get("Lune_EarthSpike_Description_Long").Contains("Earth</> damage per hit and <keyword id=\"DamageMultiplier\">0.65x</> <keyword id=\"BreakDamage\">break damage</>. 2 hits"), "EarthSpike break 0.65x");
    Check(Get("Lune_Terraquake_Description_Long").Contains("damage and <keyword id=\"DamageMultiplier\">1.95x</> <keyword id=\"BreakDamage\">break damage</> to all enemies every turn"), "Terraquake break 1.95x");
    Check(Get("Lune_CrustalCrush_Description_Long").Contains("<keyword id=\"Element_Earth\">Earth</> damage per hit and <keyword id=\"DamageMultiplier\">1.50x</> <keyword id=\"BreakDamage\">break damage</> 5 hits"), "CrustalCrush break 1.50x");

    // Healing moves: ranges updated, no damage multipliers, vanilla lines kept.
    Check(Get("Lune_HealingLight_Description_Long").Contains("<img id=\"Stain_Earth\"/>: Costs 0"), "HealingLight 1 earth overcharge");
    Check(Get("Lune_Rebirth_Description_Long").Contains("with 1-9% Health and 2 additional"), "Rebirth 1-9% + 2 AP");
    Check(Get("Lune_Revitalization_Description_Long").Contains("by 10-20% Health"), "Revitalization 10-20%");
    Check(Get("Lune_Revitalization_Description_Long").Contains("1-5 allies"), "Revitalization targets up to 5 allies");
    Check(Get("Lune_Revitalization_Description_Long").Contains("<keyword id=\"Buff_Regen\">Regen</>"), "Revitalization Regen line kept");
    Check(!Get("Lune_HealingLight_Description_Long").Contains("DamageMultiplier"), "HealingLight has no damage multiplier");

    Console.WriteLine(fails == 0 ? "ok   skill-desc-smoke" : $"FAIL skill-desc-smoke ({fails} checks)");
    return fails == 0 ? 0 : 1;
}

static int CmdSkillOverhaulSmoke(string[] args)
{
    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_ohsmoke");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);
    DataTableEditor.ResetMappings();
    int fails = 0;
    void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "ok   " : "FAIL ") + what);
        if (!ok) fails++;
    }

    float Cell(string rowName)
    {
        var bal = GameplayTweaksKit.FindAsset(dir, "DT_Balancing_MainCharacterSkills.uasset")
            ?? throw new FileNotFoundException("staged balancing table");
        var ed = new DataTableEditor(bal);
        if (!ed.TryGetRow(rowName, out var row)) throw new KeyNotFoundException(rowName);
        var p = row.Value.First(x => DataTableEditor.CleanName(x.Name) == "FloatValue");
        return p switch
        {
            UAssetAPI.PropertyTypes.Objects.FloatPropertyData f => f.Value,
            UAssetAPI.PropertyTypes.Objects.DoublePropertyData d => (float)d.Value,
            _ => float.NaN,
        };
    }

    // ── option persistence (Light-stain wildcard) ──
    var option = new SkillOverhaulKit.Settings();
    Check(!option.LightStainWildcard, "Light-stain wildcard defaults off");
    option.LightStainWildcard = true;
    var reloaded = new SkillOverhaulKit.Settings();
    reloaded.FromJson(option.ToJson());
    Check(reloaded.LightStainWildcard, "Light-stain wildcard survives settings JSON");

    // ── synthetic enable/disable round-trip ──
    var spec = new SkillOverhaulKit.ChangeSpec(
        Character: "Lune",
        MoveId: "Immolation",
        Field: "BurnFix",
        Display: "Burn stacks applied",
        Note: "smoke",
        Table: SkillOverhaulKit.SkillBalTable,
        Row: "Immolation_BaseBurnToApply",
        Path: "FloatValue",
        Vanilla: 3.0,
        New: 7.5,
        DefaultEnabled: false);
    var synthetic = new[] { spec };
    var s = new SkillOverhaulKit.Settings();

    s.SetEnabled(spec, true);
    int n1 = SkillOverhaulKit.Apply(dir, s, synthetic);
    var v1 = Cell(spec.Row);
    s.SetEnabled(spec, false);
    int n2 = SkillOverhaulKit.Apply(dir, s, synthetic);
    var v2 = Cell(spec.Row);
    Check(v1 == 7.5f && v2 == 3f, $"synthetic enable/disable ({v1} → {v2})");
    if (fails > 0) return 1;

    // ── real Lune catalog: enabled lands, disabled reverts to vanilla ──
    var real = new SkillOverhaulKit.Settings(); // defaults: all enabled
    int n3 = SkillOverhaulKit.Apply(dir, real);
    Check(Cell("Electrify_BaseDamage") == 0.077f, $"Electrify_BaseDamage = {Cell("Electrify_BaseDamage")} (expect 0.077)");
    Check(Cell("Hell_BaseDamage") == 0.4455f, $"Hell_BaseDamage = {Cell("Hell_BaseDamage")} (expect 0.4455)");
    Check(Cell("Terraquake_BaseDuration") == 1.0f, $"Terraquake_BaseDuration = {Cell("Terraquake_BaseDuration")} (expect 1)");
    Check(Cell("Typhoon_OverchargeDuration") == 3.0f, $"Typhoon_OverchargeDuration = {Cell("Typhoon_OverchargeDuration")} (expect 3)");
    Check(Cell("Mayhem_BaseDamage") == 0.33f, $"Mayhem_BaseDamage = {Cell("Mayhem_BaseDamage")} (expect 0.33)");
    Check(Cell("Mayhem_ZeroStainDamageMultiplier") == 0.66f, $"Mayhem_ZeroStainDamageMultiplier = {Cell("Mayhem_ZeroStainDamageMultiplier")} (expect 0.66)");
    Check(Cell("Mayhem_OneStainDamageMultiplier") == 0.33f, $"Mayhem_OneStainDamageMultiplier = {Cell("Mayhem_OneStainDamageMultiplier")} (expect 0.33)");
    Check(Cell("Mayhem_TwoStainsDamageMultiplier") == 0.66f, $"Mayhem_TwoStainsDamageMultiplier = {Cell("Mayhem_TwoStainsDamageMultiplier")} (expect 0.66)");
    Check(Cell("Mayhem_ThreeStainsDamageMultiplier") == 1.32f, $"Mayhem_ThreeStainsDamageMultiplier = {Cell("Mayhem_ThreeStainsDamageMultiplier")} (expect 1.32)");
    Check(Cell("Mayhem_FourStainsDamageMultiplier") == 1.98f, $"Mayhem_FourStainsDamageMultiplier = {Cell("Mayhem_FourStainsDamageMultiplier")} (expect 1.98)");
    Check(Cell("ElementalGenesis_BaseDamage") == 0.4455f, $"ElementalGenesis_BaseDamage = {Cell("ElementalGenesis_BaseDamage")} (expect 0.4455)");
    Check(Cell("StormCaller_BaseDamageOnHit") == 0.0619f, $"StormCaller_BaseDamageOnHit = {Cell("StormCaller_BaseDamageOnHit")} (expect 0.0619)");
    Check(Cell("StormCaller_BaseDamage") == 0.0619f, $"StormCaller_BaseDamage = {Cell("StormCaller_BaseDamage")} (expect 0.0619)");
    Check(Cell("Earthquake_BreakBarMultiplier") == 1.3f, $"Earthquake_BreakBarMultiplier = {Cell("Earthquake_BreakBarMultiplier")} (expect 1.3)");
    Check(Cell("EarthSpike_BreakBarMultiplier") == 1.3f, $"EarthSpike_BreakBarMultiplier = {Cell("EarthSpike_BreakBarMultiplier")} (expect 1.3)");
    Check(Cell("Terraquake_BreakBarMultiplier") == 1.3f, $"Terraquake_BreakBarMultiplier = {Cell("Terraquake_BreakBarMultiplier")} (expect 1.3)");
    Check(Cell("CrustalCrush_BreakBarBaseMultiplier") == 1.3f, $"CrustalCrush_BreakBarBaseMultiplier = {Cell("CrustalCrush_BreakBarBaseMultiplier")} (expect 1.3)");
    Check(Cell("HealingLight_BaseHeal") == 0.1f, $"HealingLight_BaseHeal = {Cell("HealingLight_BaseHeal")} (expect 0.1)");
    Check(Cell("Rebirth_BaseHealValue") == 0.01f, $"Rebirth_BaseHealValue = {Cell("Rebirth_BaseHealValue")} (expect 0.01)");
    Check(Cell("Rebirth_HealAmountPerQte") == 0.02f, $"Rebirth_HealAmountPerQte = {Cell("Rebirth_HealAmountPerQte")} (expect 0.02)");
    Check(Cell("Revitalization_BaseHealPercent") == 0.1f, $"Revitalization_BaseHealPercent = {Cell("Revitalization_BaseHealPercent")} (expect 0.1)");
    Check(Cell("Revitalization_HealIncreasePerQte") == 0.05f, $"Revitalization_HealIncreasePerQte = {Cell("Revitalization_HealIncreasePerQte")} (expect 0.05)");
    int ApCost(string stem)
    {
        var da = Directory.EnumerateFiles(dir, stem + ".uasset", SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new FileNotFoundException(stem);
        var ed = DataTableEditor.OpenPropertyAsset(da);
        return ed.FlattenProperties().First(l => l.Path == "APCost").Value.ToObject<int>();
    }
    Check(ApCost("DA_Skill_Lune_HealingLight") == 1, $"HealingLight APCost = {ApCost("DA_Skill_Lune_HealingLight")} (expect 1)");
    Check(ApCost("DA_Skill_Lune_Rebirth") == 9, $"Rebirth APCost = {ApCost("DA_Skill_Lune_Rebirth")} (expect 9)");
    Check(Cell("Typhoon_HealPercent") == 0.1f, $"Typhoon_HealPercent = {Cell("Typhoon_HealPercent")} (expect 0.1)");
    int EarthOvercharge(string stem)
    {
        var da = Directory.EnumerateFiles(dir, stem + ".uasset", SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new FileNotFoundException(stem);
        var maps = StainsMechanicKit.ReadFile(da);
        return maps.OverchargeRequired
            .Where(kv => StainsMechanicKit.Display(kv.Key) == "Earth")
            .Select(kv => kv.Value)
            .FirstOrDefault();
    }
    Check(EarthOvercharge("DA_Skill_Lune_HealingLight") == 1, $"HealingLight overcharge Earth = {EarthOvercharge("DA_Skill_Lune_HealingLight")} (expect 1)");
    // ── Sciel catalog: 0.5x curve cells + costs (incl. the inserted DarkCleansing APCost) ──
    Check(Cell("GrimPrediction_Basedamage") == (float)ScielSkillsKit.PerHit(2, 1), $"TwilightSlash base = {Cell("GrimPrediction_Basedamage")} (expect {(float)ScielSkillsKit.PerHit(2, 1)})");
    Check(Cell("FocusedForetell_BaseForetell") == 1.0f, $"FocusedForetell foretell = {Cell("FocusedForetell_BaseForetell")} (expect 1)");
    Check(Cell("FiringShadow_DamageMultiplierPerForetell") == 0.5f, $"FiringShadow eaten mult = {Cell("FiringShadow_DamageMultiplierPerForetell")} (expect 0.5)");
    Check(Cell("Harvest_HealPercentPerForetell") == 0.1f, $"Harvest heal/foretell = {Cell("Harvest_HealPercentPerForetell")} (expect 0.1)");
    Check(Cell("Harvest_BaseHeal") == 0.0f, $"Harvest base heal = {Cell("Harvest_BaseHeal")} (expect 0)");
    Check(Cell("SealedFate_DamageMultiplierOnForetellConsumed") == 2.0f, $"SealedFate consume mult = {Cell("SealedFate_DamageMultiplierOnForetellConsumed")} (expect 2)");
    Check(Cell("OurSacrifice_StolenHPMultiplier") == 0.5f, $"OurSacrifice stolen mult = {Cell("OurSacrifice_StolenHPMultiplier")} (expect 0.5)");
    Check(Cell("ShadowBringer_BaseDamage") == 2.0f, $"ShadowBringer base = {Cell("ShadowBringer_BaseDamage")} (expect 2, 20 total over 10 hits)");
    Check(Math.Abs(Cell("Doom_BaseDamage") - (float)(40.0 / 3)) < 1e-4, $"Doom base = {Cell("Doom_BaseDamage")} (expect 40/3, 40 total over 3 hits)");
    Check(Cell("EndSlice_BaseDamage") == 90.0f, $"EndSlice base = {Cell("EndSlice_BaseDamage")} (expect 90)");
    Check(Cell("EndSlice_DamageIncreasePerForetell") == 0.05f, $"EndSlice per-foretell = {Cell("EndSlice_DamageIncreasePerForetell")} (expect 0.05)");
    Check(ApCost("DA_Skill_Sciel_OurSacrifice") == 14, $"OurSacrifice APCost = {ApCost("DA_Skill_Sciel_OurSacrifice")} (expect 14)");
    Check(ApCost("DA_Skill_Sciel_DarkCleansing") == 2, $"DarkCleansing APCost = {ApCost("DA_Skill_Sciel_DarkCleansing")} (expect 2, inserted prop)");
    Check(ApCost("DA_Skill_Sciel_EndSlice") == 9, $"EndSlice charges = {ApCost("DA_Skill_Sciel_EndSlice")} (expect 9)");
    Check(n3 == SkillOverhaulKit.Specs.Length, $"catalog applied {n3}/{SkillOverhaulKit.Specs.Length} changes (raw patches don't count)");

    MonocoSkillsKit.Apply(dir);
    Check(ApCost("DA_Skill_Transfo_AbbestMelee") == 1, $"Abbest APCost = {ApCost("DA_Skill_Transfo_AbbestMelee")} (expect 1)");
    Check(ApCost("DA_Skill_Transfo_ChalierRelentlessSword") == 2, $"Chalier APCost = {ApCost("DA_Skill_Transfo_ChalierRelentlessSword")} (expect 2)");
    Check(ApCost("DA_Skill_Transfo_DuallistStormBlood") == 21, $"Duallist APCost = {ApCost("DA_Skill_Transfo_DuallistStormBlood")} (expect 21)");
    Check(ApCost("DA_Skill_Transfo_CreationFromTheVoid") == 21, $"Creation APCost = {ApCost("DA_Skill_Transfo_CreationFromTheVoid")} (expect 21)");
    Check(ApCost("DA_Skill_MightyStrike") == 3, $"MightyStrike charges = {ApCost("DA_Skill_MightyStrike")} (expect 3)");
    Check(ApCost("DA_Skill_Sanctuary") == 5, $"Sanctuary charges = {ApCost("DA_Skill_Sanctuary")} (expect 5)");
    Check(ApCost("DA_Skill_BreakPoint") == 8, $"BreakPoint charges = {ApCost("DA_Skill_BreakPoint")} (expect 8)");
    Check(Math.Abs(Cell("ChalierRelentlessSword_BaseDamage") - (float)MonocoSkillsKit.PerHit(2, 6)) < 1e-5,
        $"Chalier base = {Cell("ChalierRelentlessSword_BaseDamage")} (expect {MonocoSkillsKit.PerHit(2, 6)})");
    Check(Cell("DuallistStormBlood_OverchargeDamageMultiplier") == 5f, $"Duallist OC mult = {Cell("DuallistStormBlood_OverchargeDamageMultiplier")} (expect 5)");
    Check(Cell("PotatobagBossFireShots_OverchargeDamageMultiplier") == 5f, $"Sakapatate Fire OC mult = {Cell("PotatobagBossFireShots_OverchargeDamageMultiplier")} (expect 5)");
    Check(Cell("LampmasterSwordOfLight_OverchargeDamageMultiplier") == 5f, $"Lampmaster OC mult = {Cell("LampmasterSwordOfLight_OverchargeDamageMultiplier")} (expect 5)");
    Check(Cell("ChalierRelentlessSword_OverchargeDamageMultiplier") == 3f, $"Chalier OC mult = {Cell("ChalierRelentlessSword_OverchargeDamageMultiplier")} (expect 3)");
    Check(Cell("DuallistStormBlood_BonusDamageIfCorrectMask") == 1f, $"Duallist correct-mask bonus = {Cell("DuallistStormBlood_BonusDamageIfCorrectMask")} (expect 1)");
    Check(Cell("MightyStrike_BaseDamage") == 15f, $"MightyStrike base = {Cell("MightyStrike_BaseDamage")} (expect 15)");
    Check(Cell("Sanctuary_ShieldAmount") == 5f, $"Sanctuary shields = {Cell("Sanctuary_ShieldAmount")} (expect 5)");
    Check(Cell("BreakPoint_BaseDamage") == 80f, $"BreakPoint base = {Cell("BreakPoint_BaseDamage")} (expect 80)");
    var stMon = Directory.EnumerateFiles(dir, "ST_MainCharacters_Skills.uasset", SearchOption.AllDirectories).First();
    var monTable = DataTableEditor.OpenStringTable(stMon);
    string MonGet(string key) => monTable.TryGetValue(key, out var v) ? v : "";
    Check(MonGet("Monoco_ChalierRelentlessSword_Description_Long").Contains("DamageMultiplier"), "Chalier desc uses DamageMultiplier");
    Check(!MonGet("Monoco_ChalierRelentlessSword_Description_Long").Contains("high single target"), "Chalier desc dropped 'high'");
    Check(MonGet("Monoco_AbbestMelee_Description_Long").Contains("plays a second turn")
        || MonGet("Monoco_AbbestMelee_Description_Long").Contains("Monoco plays a second turn"), "Abbest extra turn is Monoco's");
    Check(MonGet("Monoco_Sanctuary_Description_Long").Contains("Greater Shell"), "Sanctuary Greater Shell");
    Check(MonGet("Monoco_MightyStrike_Description_Long").Contains("15.00x"), "Mighty Strike 15.00x per hit");
    Check(MonGet("Monoco_BreakPoint_Description_Long").Contains("80.00x"), "Break Point 80.00x");
    // Mask lines state the multiplier, not the resulting absolute value (0.5x base ×3 = 0.56x).
    Check(MonGet("Monoco_StalactCombo_Description_Long").Contains("3.00x")
        && MonGet("Monoco_StalactCombo_Description_Long").Contains("more damage")
        && !MonGet("Monoco_StalactCombo_Description_Long").Contains("0.56x"), "Stalact mask line = ×3 multiplier, not 0.56x");
    foreach (var k in new[] { "Monoco_AbbestMelee", "Monoco_HexgaCombo", "Monoco_SaplingAbsorption" })
        Check(MonGet(k + "_Description_Long").Contains("3.00x") && MonGet(k + "_Description_Long").Contains("more damage"),
            $"{k} mask line = ×3 multiplier");
    Check(MonGet("Monoco_DuallistStormBlood_Description_Long").Contains("5.00x")
        && MonGet("Monoco_DuallistStormBlood_Description_Long").Contains("more damage"), "Duallist (Almighty) mask line = ×5 multiplier");

    foreach (var sp in SkillOverhaulKit.Specs) real.SetEnabled(sp, false);
    int n4 = SkillOverhaulKit.Apply(dir, real);
    Check(Cell("Electrify_BaseDamage") == 0.2f, $"revert Electrify_BaseDamage = {Cell("Electrify_BaseDamage")} (expect 0.2)");
    Check(Cell("Hell_BaseDamage") == 5.0f, $"revert Hell_BaseDamage = {Cell("Hell_BaseDamage")} (expect 5)");
    Check(Cell("Mayhem_ZeroStainDamageMultiplier") == 1.0f, $"revert Mayhem_ZeroStainDamageMultiplier = {Cell("Mayhem_ZeroStainDamageMultiplier")} (expect 1.0)");
    Check(Cell("Mayhem_OneStainDamageMultiplier") == 1.15f, $"revert Mayhem_OneStainDamageMultiplier = {Cell("Mayhem_OneStainDamageMultiplier")} (expect 1.15)");
    Check(Cell("Mayhem_TwoStainsDamageMultiplier") == 1.5f, $"revert Mayhem_TwoStainsDamageMultiplier = {Cell("Mayhem_TwoStainsDamageMultiplier")} (expect 1.5)");
    Check(Cell("Mayhem_ThreeStainsDamageMultiplier") == 2.0f, $"revert Mayhem_ThreeStainsDamageMultiplier = {Cell("Mayhem_ThreeStainsDamageMultiplier")} (expect 2.0)");
    Check(Cell("Mayhem_FourStainsDamageMultiplier") == 3.0f, $"revert Mayhem_FourStainsDamageMultiplier = {Cell("Mayhem_FourStainsDamageMultiplier")} (expect 3.0)");
    Check(Cell("ElementalGenesis_BaseDamage") == 3.0f, $"revert ElementalGenesis_BaseDamage = {Cell("ElementalGenesis_BaseDamage")} (expect 3)");
    Check(Cell("StormCaller_BaseDamageOnHit") == 0.2f, $"revert StormCaller_BaseDamageOnHit = {Cell("StormCaller_BaseDamageOnHit")} (expect 0.2)");
    Check(Cell("Earthquake_BreakBarMultiplier") == 1.0f, $"revert Earthquake_BreakBarMultiplier = {Cell("Earthquake_BreakBarMultiplier")} (expect 1.0)");
    Check(Cell("CrustalCrush_BreakBarBaseMultiplier") == 2.0f, $"revert CrustalCrush_BreakBarBaseMultiplier = {Cell("CrustalCrush_BreakBarBaseMultiplier")} (expect 2.0)");
    Check(Cell("HealingLight_BaseHeal") == 0.3f, $"revert HealingLight_BaseHeal = {Cell("HealingLight_BaseHeal")} (expect 0.3)");
    Check(Cell("Rebirth_BaseHealValue") == 0.3f, $"revert Rebirth_BaseHealValue = {Cell("Rebirth_BaseHealValue")} (expect 0.3)");
    Check(ApCost("DA_Skill_Lune_HealingLight") == 3 && ApCost("DA_Skill_Lune_Rebirth") == 5, "revert HealingLight APCost 3 / Rebirth APCost 5");
    Check(Cell("Doom_BaseDamage") == 2.5f, $"revert Doom_BaseDamage = {Cell("Doom_BaseDamage")} (expect 2.5)");
    Check(Cell("EndSlice_BaseDamage") == 10.0f, $"revert EndSlice_BaseDamage = {Cell("EndSlice_BaseDamage")} (expect 10)");
    Check(Cell("OurSacrifice_StolenHPMultiplier") == 1.5f, $"revert OurSacrifice stolen mult = {Cell("OurSacrifice_StolenHPMultiplier")} (expect 1.5)");
    Check(ApCost("DA_Skill_Sciel_OurSacrifice") == 4, $"revert OurSacrifice APCost = {ApCost("DA_Skill_Sciel_OurSacrifice")} (expect 4)");
    Check(Cell("Typhoon_HealPercent") == 0.2f, $"revert Typhoon_HealPercent = {Cell("Typhoon_HealPercent")} (expect 0.2)");
    Check(EarthOvercharge("DA_Skill_Lune_HealingLight") == 1, "HealingLight overcharge Earth stays 1 (always-on stain map)");

    Console.WriteLine(fails == 0 ? "ok   skill-overhaul-smoke" : $"FAIL skill-overhaul-smoke ({fails} checks)");
    if (fails > 0) return 1;
    Directory.Delete(dir, true);
    return 0;
}

/// <summary>
/// Lune heal/stain smoke: stages LuneSkillsKit + LuneApocalypseKit into a
/// throwaway dir and asserts the no-Light-substitution patches and the Angel's
/// Gift single-pay grant shape:
/// - staged holder HasStainQuantities / FindStainsObjectsByQuantities: no byte 7
/// - staged Genesis BP ValidateSkillAvailability: no byte 7
/// - no staged BP has byte 7 in ValidateSkillAvailability
/// - Angel's Gift: 20 loop GainAP, 0 self GainAP (4 stain bursts x 5 party slots)
/// - Dark Power unchanged: 4 self GainAP, 0 loop
/// - clone stain maps: AngelsGift Lightx1/empty, DarkPower Darkx1/empty,
///   VivifyingLight inherits vanilla Earthx2 (matches its 2-Earth desc)
/// </summary>
static int CmdLuneHealSmoke(string[] args)
{
    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_healsmoke");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);
    DataTableEditor.ResetMappings();
    int fails = 0;
    void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "ok   " : "FAIL ") + what);
        if (!ok) fails++;
    }

    LuneSkillsKit.Apply(dir);
    LuneApocalypseKit.Apply(dir);

    UAsset Open(string p)
    {
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAsset(p, EngineVersion.VER_UE5_4, map)
            : new UAsset(p, EngineVersion.VER_UE5_4);
    }
    int ByteConst(string bpPath, string fnName, byte value)
    {
        var bp = Open(bpPath);
        var fn = bp.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == fnName);
        if (fn?.ScriptBytecode is null) return -1;
        int n = 0;
        MayhemFixKit.Walk(fn.ScriptBytecode, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_ByteConst bc && bc.Value == value) n++;
        });
        return n;
    }
    int ByteSevens(string bpPath, string fnName) => ByteConst(bpPath, fnName, 7);
    (int Self, int Loop, int Total) GainApShape(string bpPath)
    {
        var bp = Open(bpPath);
        var fn = bp.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect")
            ?? throw new InvalidDataException("no OnActionEffect in " + bpPath);
        int self = 0, loop = 0, total = 0;
        foreach (var s in fn.ScriptBytecode!)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_Context ctx
                    && ctx.ContextExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_VirtualFunction vf
                    && vf.VirtualFunctionName.ToString() == "GainAP"
                    && ctx.ObjectExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_Context inner)
                {
                    total++;
                    if (inner.ObjectExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_InstanceVariable) self++;
                    else if (inner.ObjectExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable) loop++;
                }
            });
        return (self, loop, total);
    }
    string FindStaged(string fileName) =>
        Directory.EnumerateFiles(dir, fileName, SearchOption.AllDirectories).FirstOrDefault()
        ?? throw new FileNotFoundException(fileName + " not staged");

    var holder = FindStaged("BP_StainHolderComponent.uasset");
    Check(ByteSevens(holder, "HasStainQuantities") == 0, "holder HasStainQuantities has no Light byte");
    Check(ByteSevens(holder, "FindStainsObjectsByQuantities") == 0, "holder FindStainsObjectsByQuantities has no Light byte (consume fallback dead)");
    // Byte 9 (never-held) is the required retarget: byte 0 would read the
    // empty-slot count and keep substitution alive (always-overcharged bug).
    Check(ByteConst(holder, "HasStainQuantities", 9) == 1, "holder HasStainQuantities lookup points at byte 9");
    Check(ByteConst(holder, "FindStainsObjectsByQuantities", 9) == 2, "holder FindStainsObjectsByQuantities fallback points at byte 9 x2");
    var genesis = FindStaged("BP_Battle_SkillScript_ElementalGenesis.uasset");
    Check(ByteSevens(genesis, "ValidateSkillAvailability") == 0, "Genesis ValidateSkillAvailability has no Light byte");
    Check(ByteConst(genesis, "ValidateSkillAvailability", 9) == 1, "Genesis ValidateSkillAvailability lookup points at byte 9");
    var cataclysm = FindStaged("BP_Battle_SkillScript_ElementalCataclysm.uasset");
    Check(ByteSevens(cataclysm, "ValidateSkillAvailability") == 0, "Cataclysm ValidateSkillAvailability has no Light byte");
    Check(ByteConst(cataclysm, "ValidateSkillAvailability", 9) == 1, "Cataclysm ValidateSkillAvailability lookup points at byte 9");
    int leakyValidates = 0;
    foreach (var bp in Directory.EnumerateFiles(dir, "BP_*.uasset", SearchOption.AllDirectories))
    {
        int n;
        try { n = ByteSevens(bp, "ValidateSkillAvailability"); }
        catch { continue; }
        if (n > 0) { Console.WriteLine($"FAIL staged {Path.GetFileName(bp)} Validate byte7 x{n}"); leakyValidates++; }
    }
    Check(leakyValidates == 0, "no staged BP ValidateSkillAvailability allows Light substitution");

    var gift = GainApShape(FindStaged("BP_Battle_SkillScript_AngelsGift.uasset"));
    // 4 stain bursts x 5 party targets (Party of Five; the old 4 skipped the
    // fifth member, and the original 3 skipped the fourth).
    Check(gift is { Self: 0, Loop: 20, Total: 20 }, $"Angel's Gift grants: self={gift.Self} loop={gift.Loop} total={gift.Total} (expect 0/20/20)");
    var dark = GainApShape(FindStaged("BP_Battle_SkillScript_DarkPower.uasset"));
    Check(dark is { Self: 4, Loop: 0, Total: 4 }, $"Dark Power grants unchanged: self={dark.Self} loop={dark.Loop} total={dark.Total} (expect 4/0/4)");

    string Maps(string stem)
    {
        var da = FindStaged(stem + ".uasset");
        var maps = StainsMechanicKit.ReadFile(da);
        string Fmt(IReadOnlyDictionary<string, int> m) =>
            m.Count == 0 ? "(empty)" : string.Join("+", m.Select(kv => StainsMechanicKit.Display(kv.Key) + "x" + kv.Value));
        return "consume=" + Fmt(maps.OverchargeRequired) + " produce=" + Fmt(maps.Produced);
    }
    Check(Maps("DA_Skill_Lune_AngelsGift") == "consume=Lightx1 produce=(empty)", "AngelsGift maps: " + Maps("DA_Skill_Lune_AngelsGift"));
    Check(Maps("DA_Skill_Lune_DarkPower") == "consume=Darkx1 produce=(empty)", "DarkPower maps: " + Maps("DA_Skill_Lune_DarkPower"));
    Check(Maps("DA_Skill_Lune_VivifyingLight").StartsWith("consume=Earthx2"), "VivifyingLight inherits vanilla Earthx2: " + Maps("DA_Skill_Lune_VivifyingLight"));

    // Wildcard ON: no strict holder is staged (vanilla Light substitution comes
    // back), but Genesis/Cataclysm keep their per-skill exact-element patches.
    var wildDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_healsmoke_wild");
    if (Directory.Exists(wildDir)) Directory.Delete(wildDir, true);
    Directory.CreateDirectory(wildDir);
    LuneSkillsKit.Apply(wildDir, lightStainWildcard: true);
    string? FindIn(string root, string fileName) =>
        Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).FirstOrDefault();
    Check(FindIn(wildDir, "BP_StainHolderComponent.uasset") is null,
        "wildcard ON: strict stain holder not staged (vanilla Light substitution)");
    var wildGenesis = FindIn(wildDir, "BP_Battle_SkillScript_ElementalGenesis.uasset");
    Check(wildGenesis is not null && ByteSevens(wildGenesis, "ValidateSkillAvailability") == 0,
        "wildcard ON: Genesis still requires the exact four elements");
    var wildCataclysm = FindIn(wildDir, "BP_Battle_SkillScript_ElementalCataclysm.uasset");
    Check(wildCataclysm is not null && ByteSevens(wildCataclysm, "ValidateSkillAvailability") == 0,
        "wildcard ON: Cataclysm still requires the exact four elements");

    // A reused mod tree can still hold the strict holder from an earlier pack;
    // toggling wildcard on must delete it so vanilla substitution returns.
    var reuseDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_healsmoke_reuse");
    if (Directory.Exists(reuseDir)) Directory.Delete(reuseDir, true);
    Directory.CreateDirectory(reuseDir);
    var staleRel = Path.GetRelativePath(dir, holder);
    var staleDest = Path.Combine(reuseDir, staleRel);
    Directory.CreateDirectory(Path.GetDirectoryName(staleDest)!);
    File.Copy(holder, staleDest);
    var staleExp = Path.ChangeExtension(holder, ".uexp");
    if (File.Exists(staleExp)) File.Copy(staleExp, Path.ChangeExtension(staleDest, ".uexp"));
    LuneSkillsKit.Apply(reuseDir, lightStainWildcard: true);
    Check(FindIn(reuseDir, "BP_StainHolderComponent.uasset") is null,
        "wildcard ON: stale strict holder removed from a reused pack");

    if (fails == 0)
    {
        Directory.Delete(wildDir, true);
        Directory.Delete(reuseDir, true);
    }
    Console.WriteLine(fails == 0 ? "ok   lune-heal-smoke" : $"FAIL lune-heal-smoke ({fails} checks)");
    if (fails > 0) return 1;
    Directory.Delete(dir, true);
    return 0;
}

/// <summary>
/// Skill Graph Editor writer smoke: stages a vanilla DA_SkillGraph into a
/// throwaway dir, applies a moved-node + relink layout, re-reads the staged
/// copy and asserts the positions and edge endpoints match.
/// </summary>
static int CmdSkillGraphSmoke(string[] args)
{
    DataTableEditor.ResetMappings();
    var vanilla = SkillGraphKit.FindCharacterGraphVanilla("DA_SkillGraph_Lune");
    if (vanilla is null)
    {
        Console.WriteLine("FAIL skill-graph-smoke (no DA_SkillGraph_Lune under work/tables/graphs)");
        return 1;
    }

    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_graphsmoke");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);
    int fails = 0;
    void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "ok   " : "FAIL ") + what);
        if (!ok) fails++;
    }

    var before = SkillGraphKit.Load(vanilla);
    if (before.Nodes.Count == 0)
    {
        Console.WriteLine("FAIL skill-graph-smoke (graph loaded 0 nodes)");
        return 1;
    }
    Check(before.Links.Count > 0, $"vanilla Lune graph: {before.Nodes.Count} nodes, {before.Links.Count} links");

    // Move the first two skills to new coords, keeping the full vanilla link
    // set (a real "shape" always captures every link on the tree).
    var a = before.Nodes[0];
    var b = before.Nodes.FirstOrDefault(n => n.Stem != a.Stem);
    if (b is null)
    {
        Console.WriteLine("FAIL skill-graph-smoke (need ≥ 2 nodes)");
        return 1;
    }
    var layout = new SkillGraphKit.GraphLayout();
    // Off-grid targets on purpose: the writer snaps every position onto the
    // shared 125×100 lattice (x ≡ 100 mod 125, y ≡ 125 mod 100), so the smoke
    // asserts the snapped result rather than the raw request.
    var wantAx = SkillGraphKit.SnapGraphX(a.X + 75);
    var wantAy = SkillGraphKit.SnapGraphY(a.Y + 60);
    var wantBx = SkillGraphKit.SnapGraphX(b.X - 30);
    var wantBy = SkillGraphKit.SnapGraphY(b.Y - 45);
    layout.Positions.Add((a.Skill, a.X + 75, a.Y + 60));
    layout.Positions.Add((b.Skill, b.X - 30, b.Y - 45));
    foreach (var (from, to) in before.Links)
        layout.Links.Add((before.Nodes[from].Stem, before.Nodes[to].Stem));

    int written = SkillGraphKit.WriteStagedGraph(dir, "DA_SkillGraph_Lune", layout);
    Check(written == 1, $"write staged graph returned 1 (got {written})");

    var stagedPath = System.IO.Path.Combine(dir, "Sandfall", "Content", "Gameplay", "SkillTree", "Content", "Lune", "DA_SkillGraph_Lune.uasset");
    if (!File.Exists(stagedPath))
    {
        Console.WriteLine("FAIL skill-graph-smoke (staged graph missing at " + stagedPath + ")");
        return 1;
    }
    var after = SkillGraphKit.Load(stagedPath);
    var afterA = after.Nodes.FirstOrDefault(n => n.Stem == a.Stem);
    var afterB = after.Nodes.FirstOrDefault(n => n.Stem == b.Stem);
    Check(afterA is not null && Math.Abs(afterA.X - wantAx) < 0.01 && Math.Abs(afterA.Y - wantAy) < 0.01,
        $"node {a.Stem} snapped to ({afterA?.X:0},{afterA?.Y:0}) expected ({wantAx:0},{wantAy:0})");
    Check(afterB is not null && Math.Abs(afterB.X - wantBx) < 0.01 && Math.Abs(afterB.Y - wantBy) < 0.01,
        $"node {b.Stem} snapped to ({afterB?.X:0},{afterB?.Y:0}) expected ({wantBx:0},{wantBy:0})");
    var afterPairs = after.Links.Select(l => (after.Nodes[l.From].Stem, after.Nodes[l.To].Stem)).ToList();
    var layoutPairs = layout.Links.ToList();
    Check(afterPairs.SequenceEqual(layoutPairs) && after.Links.Count == before.Links.Count,
        $"full link set rebuilt ({before.Links.Count} links)");
    Check(after.Nodes.Count == before.Nodes.Count, "node set unchanged (no skill added or removed)");

    // The uniform lattice snaps any requested coordinate, but leaves already
    // on-grid values untouched (idempotent on the Lune-phase grid).
    Check(SkillGraphKit.SnapGraphX(100) == 100 && SkillGraphKit.SnapGraphY(125) == 125,
        "snap keeps on-grid (100,125) unchanged");
    Check(SkillGraphKit.SnapGraphX(225) == 225 && SkillGraphKit.SnapGraphY(225) == 225,
        "snap keeps on-grid (225,225) unchanged");
    Check(SkillGraphKit.SnapGraphX(162.5) == 162.5 && SkillGraphKit.SnapGraphY(175) == 175,
        "snap keeps sub-grid (162.5,175) unchanged");
    Check(SkillGraphKit.SnapGraphX(413) == 412.5, $"off-grid x 413 snaps to sub-column 412.5 (got {SkillGraphKit.SnapGraphX(413)})");
    Check(SkillGraphKit.SnapGraphY(475) == 475, $"off-grid y 475 snaps to major row 475 (got {SkillGraphKit.SnapGraphY(475)})");

    // The writer syncs each node's UnlockCost to the DA APCost (Rebirth DA = 9).
    var stagedRebirth = after.Nodes.FirstOrDefault(n => n.Skill == "DA_Skill_Lune_Rebirth");
    Check(stagedRebirth is not null && stagedRebirth.UnlockCost == 9,
        $"staged graph Rebirth UnlockCost synced to 9 (DA APCost) — got {stagedRebirth?.UnlockCost}");

    // Re-apply the same layout → the graph already matches, write is a no-op? No:
    // WriteStagedGraph re-writes whenever a layout is supplied; assert idempotence
    // by comparing the re-read of a second identical write.
    int again = SkillGraphKit.WriteStagedGraph(dir, "DA_SkillGraph_Lune", layout);
    var after2 = SkillGraphKit.Load(stagedPath);
    Check(again == 1 && after2.Nodes.Count == after.Nodes.Count
        && after2.Nodes.Select(n => (n.Skill, n.X, n.Y)).SequenceEqual(after.Nodes.Select(n => (n.Skill, n.X, n.Y))),
        "second identical write stays stable");

    // Vanilla layout with shape=vanilla → no writes.
    var settings = new SkillGraphKit.Settings();
    Check(!settings.IsActive, "empty settings are inactive (nothing packs)");

    // The pack applies the Active slot, which is distinct from the viewed Shape.
    var activeSettings = new SkillGraphKit.Settings();
    var lune = activeSettings.For("Lune");
    lune.Shape = "custom";               // viewing custom
    lune.Active = "New";                 // but a preset named "New" is packed
    lune.Presets["New"] = new SkillGraphKit.GraphLayout();
    lune.Custom.Positions.Add(("Lune_Wildfire", 350.0, 425.0));
    lune.Presets["New"].Positions.Add(("Lune_Wildfire", 999.0, 999.0));
    Check(activeSettings.IsActive, "recorded Active (preset) makes settings active");
    var json = activeSettings.ToJson();
    var roundTripped = new SkillGraphKit.Settings();
    roundTripped.FromJson(json);
    Check(roundTripped.For("Lune").PackShape() == "New" && roundTripped.For("Lune").Shape == "custom",
        "Active survives JSON round-trip while Shape stays the viewed one");
    // Legacy file: no "active" key → Active defaults to Shape (old pack behaviour).
    var legacy = new SkillGraphKit.Settings();
    legacy.FromJson(Newtonsoft.Json.Linq.JObject.Parse(
        "{\"Lune\":{\"shape\":\"New\",\"custom\":{\"positions\":[],\"links\":[]},\"presets\":{\"New\":{\"positions\":[],\"links\":[]}}}}"));
    Check(legacy.For("Lune").PackShape() == "New",
        $"legacy settings (no active key) pack the viewed shape — got {legacy.For("Lune").PackShape()}");

    // Editor baseline = vanilla + kit-authored new skills at authored positions.
    var baseline = SkillGraphKit.LoadEditorBaseline("DA_SkillGraph_Lune");
    var ultima = baseline.Nodes.FirstOrDefault(n => n.Skill == "DA_Skill_Lune_Ultima");
    var darkPower = baseline.Nodes.FirstOrDefault(n => n.Skill == "DA_Skill_Lune_DarkPower");
    Check(ultima is not null && Math.Abs(ultima.X - 225.0) < 0.01 && Math.Abs(ultima.Y - 625.0) < 0.01
        && ultima.UnlockCost == 21,
        $"baseline includes Ultima at authored (225,625) 21 AP (got {(ultima is null ? "missing" : $"({ultima.X:0},{ultima.Y:0}) {ultima.UnlockCost} AP")})");
    Check(darkPower is not null && Math.Abs(darkPower.X - 475.0) < 0.01 && Math.Abs(darkPower.Y - 825.0) < 0.01,
        $"baseline includes DarkPower at authored (475,825) (got {(darkPower is null ? "missing" : $"({darkPower.X:0},{darkPower.Y:0})")})");
    Check(baseline.Nodes.Count > before.Nodes.Count,
        $"baseline has more nodes than vanilla ({baseline.Nodes.Count} vs {before.Nodes.Count})");
    var authored = SkillGraphKit.AuthoredAddsByGraph()["DA_SkillGraph_Lune"];
    var missing = authored.Where(a => !baseline.Nodes.Any(n => n.Skill == a.DaStem)).Select(a => a.DaStem).ToList();
    Check(missing.Count == 0, $"every authored Lune add landed in the baseline (missing: {string.Join(", ", missing)})");

    // Display costs resolve to the DA APCost (the tuned value), not the stale graph UnlockCost.
    int BaselineCost(string stem) =>
        baseline.Nodes.First(n => n.Skill == stem).DisplayCost;
    Check(BaselineCost("DA_Skill_Lune_Rebirth") == 9,
        $"Rebirth displays 9 AP (DA APCost) — got {BaselineCost("DA_Skill_Lune_Rebirth")}");
    Check(BaselineCost("DA_Skill_Lune_HealingLight") == 1,
        $"HealingLight displays 1 AP (DA APCost) — got {BaselineCost("DA_Skill_Lune_HealingLight")}");
    Check(BaselineCost("DA_Skill_Lune_BlitzOvercharge") == 9,
        $"BlitzOvercharge displays 9 AP — got {BaselineCost("DA_Skill_Lune_BlitzOvercharge")}");
    Check(BaselineCost("DA_Skill_Lune_Ultima") == 21,
        $"Ultima displays 21 AP — got {BaselineCost("DA_Skill_Lune_Ultima")}");

    // Element overrides for moves whose element lives in the script/description.
    Check(SkillGraphKit.ElementOverride("DA_Skill_Lune_BlitzOvercharge") == "Lightning",
        "BlitzOvercharge element override = Lightning");
    Check(SkillGraphKit.ElementOverride("DA_Skill_Lune_ElementalGenesis") == "Multi",
        "ElementalGenesis element override = Multi");
    Check(SkillGraphKit.ElementOverride("DA_Skill_Gustave_HellStorm") == "Weapon",
        "HellStorm element override = Weapon");

    // Gustave baseline: the mod-staged Noah graph (when present) is authoritative —
    // it keeps the visible Gustave nodes, drops the hidden Verso/cut-content ones,
    // and carries the 4 new Gustave moves.
    var noahMod = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "mod"), "DA_SkillGraph_Noah.uasset");
    if (noahMod is not null)
    {
        var noah = SkillGraphKit.LoadEditorBaseline("DA_SkillGraph_Noah");
        Check(noah.Nodes.Any(n => n.Skill == "DA_Skill_Gustave_HellStorm")
            && noah.Nodes.Any(n => n.Skill == "DA_Skill_Gustave_CinderSwarm")
            && noah.Nodes.Any(n => n.Skill == "DA_Skill_Gustave_ZoomZoom")
            && noah.Nodes.Any(n => n.Skill == "DA_Skill_Gustave_ExplodingShot"),
            $"Noah baseline has all 4 new Gustave moves ({noah.Nodes.Count} nodes)");
        Check(!noah.Nodes.Any(n => n.Skill is "DA_Skill_Combo1" or "DA_Skill_Blitz" or "DA_Skill_LightHolder"),
            "Noah baseline drops the hidden Verso/cut-content nodes");
    }
    else
    {
        Console.WriteLine("note  no mod-staged Noah graph — skipping Noah baseline check");
    }

    // Maelle baseline: the eight donor-clone moves appear as FLOATING nodes (no
    // prerequisite link) whenever the Maelle kit is on and staged the mod graph.
    var maelleMod = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "mod"), "DA_SkillGraph_Maelle.uasset");
    if (maelleMod is not null)
    {
        var maelle = SkillGraphKit.LoadEditorBaseline("DA_SkillGraph_Maelle");
        foreach (var stem in new[]
        {
            "DA_Skill_Maelle_LumiereAbove", "DA_Skill_Maelle_ReversFatal",
            "DA_Skill_Maelle_DeviationAbsolue", "DA_Skill_Maelle_ForThoseWhoComeAfter",
            "DA_Skill_Maelle_VengeanceArdente", "DA_Skill_Maelle_AliciasNightmare",
            "DA_Skill_Maelle_BladeWaltz", "DA_Skill_Maelle_LActeFinalIncendiaire",
        })
            Check(maelle.Nodes.Any(n => n.Skill == stem), $"Maelle baseline has {stem} ({maelle.Nodes.Count} nodes)");
    }
    else
    {
        Console.WriteLine("note  no mod-staged Maelle graph — skipping Maelle baseline check");
    }

    var monVanilla = SkillGraphKit.FindCharacterGraphVanilla("DA_SkillGraph_Monoco");
    Check(monVanilla is not null, "Monoco graph extract present");
    if (monVanilla is not null)
    {
        var monG = SkillGraphKit.Load(monVanilla);
        Check(monG.Nodes.Count >= 40, $"Monoco graph has {monG.Nodes.Count} nodes");
        Check(monG.Nodes.All(n => n.UnlockCost == 0),
            "all Monoco graph UnlockCost values are 0 (feet / gradient items)");
    }

    Console.WriteLine(fails == 0 ? "ok   skill-graph-smoke" : $"FAIL skill-graph-smoke ({fails} checks)");
    if (fails > 0) return 1;
    Directory.Delete(dir, true);
    return 0;
}

static int CmdFxRoll(string[] args)
{
    var count = 20;
    if (args.Length > 1 && int.TryParse(args[1], out var n) && n > 0)
        count = Math.Min(n, 200);
    var catalog = new E33ModEditor.Core.Loot.LootCatalog();
    var owners = E33ModEditor.Core.Loot.RandomizerKit.WeaponOwners();
    Console.WriteLine($"weapons-with-owner {owners.Count}");
    PrintPools(catalog, owners);

    var echoDup = 0;
    var emptyCore = 0;
    var charMismatch = 0;
    var idDup = 0;
    var startStack = 0;
    var gustaveAuto = 0;
    var gustaveHigh = 0;
    var t1Slots = 0;
    var t1Weapons = 0;
    var t2Slots = 0;
    var t2Weapons = 0;
    var unused = new HashSet<string>(StringComparer.Ordinal);
    foreach (var col in E33ModEditor.Core.Loot.WeaponEffectTiers.Columns)
        if (col != "DoNotUse")
            foreach (var id in catalog.EffectTiers.Column(col))
                unused.Add(id);

    var echoHits = 0;
    var totalSlots = 0;
    var echoByChar = new Dictionary<string, int>(StringComparer.Ordinal);
    var slotByChar = new Dictionary<string, int>(StringComparer.Ordinal);
    var weaponsByChar = new Dictionary<string, int>(StringComparer.Ordinal);
    var effectHits = new Dictionary<string, int>(StringComparer.Ordinal);
    var extraFilled = 0;
    var extraTriedWeapons = 0;
    var tierOff = 0; // effect tier lower than weapon mix would prefer — just count T1 on T5
    var t5gotT1 = 0;
    var t5gotT3 = 0;
    var neverRolledWeapons = 0;
    var elementMismatch = 0;
    var conditionConflicts = 0;
    var floorMissing = 0;
    var earlyNeedsUnsatisfied = 0;
    var statusConsumeStacks = 0;
    var cond = catalog.EffectConditions;
    var synShieldPay = 0;
    var synShieldSpend = 0;
    var synShell = 0;
    var synCounterShield = 0;
    var spendWeapons = 0;
    var spendGenWeapons = 0;
    var synTrig = new Dictionary<string, (int Total, int Sat)>(StringComparer.Ordinal);
    var synTrigHigh = new Dictionary<string, (int Total, int Sat)>(StringComparer.Ordinal);
    var archSlots = 0;
    var primaryHit = 0;
    var primarySlotHit = 0;
    var primaryCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    var luneWeapons = 0;
    var luneWithStain = 0;
    var luneStainSlots = 0;
    var t4Tier = new int[6];
    var t5Tier = new int[6];

    for (int run = 1; run <= count; run++)
    {
        var seed = 1000 + run;
        var loadouts = E33ModEditor.Core.Loot.RandomizerKit.RollWeaponPassives(
            catalog, owners, new Random(seed));
        var runEchoDup = 0;
        var runEmpty = 0;
        var runMismatch = 0;
        var runConflicts = 0;
        foreach (var w in loadouts)
        {
            unused.RemoveWhere(id =>
                id == w.Extra || id == w.Level2 || id == w.Level3 || id == w.Level4 || id == w.Level5);
            extraTriedWeapons++;
            if (w.Extra.Length > 0) extraFilled++;
            if (w.WeaponTier == 1) { t1Weapons++; t1Slots += w.Filled().Count(); }
            if (w.WeaponTier == 2) { t2Weapons++; t2Slots += w.Filled().Count(); }
            if (E33ModEditor.Core.Loot.WeaponEffectTiers.IsGustave(w.Character))
            {
                foreach (var id in w.Filled())
                {
                    if (E33ModEditor.Core.Loot.WeaponEffectTiers.IsAutoDeath(id))
                        gustaveAuto++;
                    var et = catalog.EffectTiers.TierOf(id);
                    if (et is > 3) gustaveHigh++;
                }
            }

            var filled = w.Filled().ToList();
            var tagSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in filled)
            {
                if (cond.Conflicts(tagSet, id)) { conditionConflicts++; runConflicts++; }
                tagSet.UnionWith(cond.TagsOf(id));
            }
            if (w.WeaponTier >= 3 && !filled.Any(cond.IsUnconditional)) floorMissing++;
            if (w.WeaponTier <= 3 && filled.Any(id => !cond.EnablerSatisfied(id, tagSet)))
                earlyNeedsUnsatisfied++;
            if (tagSet.Contains("shield-gen") && tagSet.Contains("shield-payoff")) synShieldPay++;
            if (tagSet.Contains("shield-gen") && tagSet.Contains("shield-spend")) synShieldSpend++;
            if (tagSet.Contains("shield-spend"))
            {
                spendWeapons++;
                if (tagSet.Contains("shield-gen") || tagSet.Contains("shell-gen")) spendGenWeapons++;
            }
            var highTier = w.WeaponTier >= 4;
            foreach (var (when, prefer, _, _) in cond.Synergies)
            {
                if (!tagSet.Contains(when)) continue;
                var sat = prefer.Count > 0 && prefer.Any(t => tagSet.Contains(t));
                var cur = synTrig.TryGetValue(when, out var v) ? v : (Total: 0, Sat: 0);
                synTrig[when] = (cur.Total + 1, cur.Sat + (sat ? 1 : 0));
                if (highTier)
                {
                    var h = synTrigHigh.TryGetValue(when, out var hv) ? hv : (Total: 0, Sat: 0);
                    synTrigHigh[when] = (h.Total + 1, h.Sat + (sat ? 1 : 0));
                }
            }
            if (tagSet.Contains("shell-gen") && tagSet.Contains("shell-need")) synShell++;
            if (tagSet.Contains("counter") && tagSet.Contains("shield-payoff")) synCounterShield++;
            totalSlots += filled.Count;
            weaponsByChar[w.Character] = weaponsByChar.GetValueOrDefault(w.Character) + 1;
            slotByChar[w.Character] = slotByChar.GetValueOrDefault(w.Character) + filled.Count;

            var meleeName = E33ModEditor.Core.StainsMechanicKit.ElementNames
                .TryGetValue(w.MeleeElement, out var mn) ? mn : "";
            var poolArch = new HashSet<string>(StringComparer.Ordinal) { w.PrimaryArchetype };
            foreach (var l in catalog.EffectArchetypes.Links(w.PrimaryArchetype)) poolArch.Add(l);
            if (w.PrimaryArchetype.Length > 0)
                primaryCounts[w.PrimaryArchetype] = primaryCounts.GetValueOrDefault(w.PrimaryArchetype) + 1;
            if (w.Character == "Lune")
            {
                luneWeapons++;
                var stainSlots = filled.Count(id => catalog.EffectArchetypes.ElementOf(id) != null);
                luneStainSlots += stainSlots;
                if (stainSlots > 0) luneWithStain++;
            }
            if (filled.Count(id => E33ModEditor.Core.Loot.WeaponEffectTiers.ExclusiveGroup(id)
                                   == "status-consume") > 1)
                statusConsumeStacks++;

            // Free slot always; one more per tier (T1..T5 -> 1/2/3/4/5).
            if (w.Extra.Length == 0
                || (w.WeaponTier >= 2 && w.Level2.Length == 0)
                || (w.WeaponTier < 2 && w.Level2.Length > 0)
                || (w.WeaponTier >= 3 && w.Level3.Length == 0)
                || (w.WeaponTier < 3 && w.Level3.Length > 0)
                || (w.WeaponTier >= 4 && w.Level4.Length == 0)
                || (w.WeaponTier < 4 && w.Level4.Length > 0)
                || (w.WeaponTier >= 5 && w.Level5.Length == 0)
                || (w.WeaponTier < 5 && w.Level5.Length > 0))
            {
                emptyCore++;
                runEmpty++;
            }

            var set = new HashSet<string>(StringComparer.Ordinal);
            var echoes = 0;
            var starts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in filled)
            {
                if (!set.Add(id)) { idDup++; }
                effectHits[id] = effectHits.GetValueOrDefault(id) + 1;
                if (!catalog.EffectCharacters.Allows(id, w.Character))
                {
                    charMismatch++;
                    runMismatch++;
                }
                if (E33ModEditor.Core.Loot.WeaponEffectTiers.IsStainEcho(id))
                {
                    echoes++;
                    echoHits++;
                    echoByChar[w.Character] = echoByChar.GetValueOrDefault(w.Character) + 1;
                }
                var startKey = StainStartKey(id);
                if (startKey is not null && !starts.Add(startKey))
                    startStack++;
                var et = catalog.EffectTiers.TierOf(id);
                if (w.WeaponTier == 5 && et == 1) t5gotT1++;
                if (w.WeaponTier == 5 && et == 3) t5gotT3++;
                if (et is int etv)
                {
                    if (w.WeaponTier == 4 && etv <= 5) t4Tier[etv]++;
                    if (w.WeaponTier == 5 && etv <= 5) t5Tier[etv]++;
                }
                if (et is int etv2 && etv2 < w.WeaponTier && w.WeaponTier - etv2 >= 3)
                    tierOff++;

                var element = catalog.EffectArchetypes.ElementOf(id)
                              ?? (cond.ElementGateEnabled ? cond.ElementOf(id) : null);
                if (!string.IsNullOrEmpty(element) && !string.IsNullOrEmpty(meleeName)
                    && !element.Equals(meleeName, StringComparison.OrdinalIgnoreCase))
                {
                    elementMismatch++;
                    runMismatch++;
                }
                // Extra is the wildcard; the other slots must sit in the
                // primary archetype or one of its siblings.
                if (id != w.Extra)
                {
                    archSlots++;
                    var primaryArch = catalog.EffectArchetypes.Get(w.PrimaryArchetype);
                    if (primaryArch is not null
                        && primaryArch.Members.Contains(id, StringComparer.Ordinal))
                        primarySlotHit++;
                    if (catalog.EffectArchetypes.All.Any(a => poolArch.Contains(a.Id)
                            && a.Members.Contains(id, StringComparer.Ordinal)))
                        primaryHit++;
                }
            }
            if (echoes > 1)
            {
                echoDup++;
                runEchoDup++;
            }
        }
        if (loadouts.Count == 0) neverRolledWeapons++;
        Console.WriteLine(
            $"run {run,2} seed {seed} weapons {loadouts.Count} echoDup {runEchoDup} emptyCore {runEmpty} mismatch {runMismatch} conflicts {runConflicts}");
    }

    Console.WriteLine();
    Console.WriteLine("=== totals across " + count + " generations ===");
    Console.WriteLine($"stain-echo doubles (must be 0): {echoDup}");
    Console.WriteLine($"duplicate ids on one weapon: {idDup}");
    Console.WriteLine($"character mismatches: {charMismatch}");
    Console.WriteLine($"slot-count violations: {emptyCore}");
    Console.WriteLine($"Gustave AutoDeath (must be 0): {gustaveAuto}");
    Console.WriteLine($"Gustave T4/T5 effects (must be 0): {gustaveHigh}");
    Console.WriteLine($"T1 slots/weapon: {(t1Weapons == 0 ? "n/a" : (t1Slots / (double)t1Weapons).ToString("0.00"))}");
    Console.WriteLine($"T2 slots/weapon: {(t2Weapons == 0 ? "n/a" : (t2Slots / (double)t2Weapons).ToString("0.00"))}");
    Console.WriteLine($"same-element stain-start stacks: {startStack}");
    Console.WriteLine($"T5 weapons rolling a T1 effect: {t5gotT1}");
    Console.WriteLine($"T5 weapons rolling a T3 effect (mix has no T3): {t5gotT3}");
    Console.WriteLine($"effect 3+ tiers below weapon: {tierOff}");
    static string MixShare(int[] a, int lo, int hi, string label)
    {
        var total = 0;
        for (int i = lo; i <= hi; i++) total += a[i];
        if (total == 0) return label + " n/a";
        var parts = new List<string>();
        for (int i = hi; i >= lo; i--)
            if (a[i] > 0) parts.Add($"{i}:{100.0 * a[i] / total:0}%");
        return label + " " + string.Join(" ", parts);
    }
    Console.WriteLine(MixShare(t4Tier, 1, 5, "T4 weapon effect tiers (target T4 60 / T3 20 / T2 15 / T1 5):"));
    Console.WriteLine(MixShare(t5Tier, 1, 5, "T5 weapon effect tiers (target T5 60 / T4 30 / T3 10):"));
    Console.WriteLine($"element mismatches (must be 0): {elementMismatch}");
    Console.WriteLine($"condition conflicts on one weapon (must be 0): {conditionConflicts}");
    Console.WriteLine($"T3+ weapons missing an unconditional floor (must be 0): {floorMissing}");
    Console.WriteLine($"T1-T3 payoffs without an enabler (must be 0): {earlyNeedsUnsatisfied}");
    Console.WriteLine($"status-consume stacks (must be 0): {statusConsumeStacks}");
    Console.WriteLine($"synergy weapons — shield gen+payoff {synShieldPay}, shield gen+spend {synShieldSpend}, "
                      + $"shell gen+payoff {synShell}, counter+shield-payoff {synCounterShield}");
    Console.WriteLine($"spender weapons {spendWeapons}; of those with a generator {spendGenWeapons} "
                      + $"({Pct(spendGenWeapons, spendWeapons)})");
    Console.WriteLine("synergy satisfaction, T4/T5 weapons (trigger → also holding a partner):");
    foreach (var kv in synTrigHigh.OrderByDescending(k => k.Value.Total).ThenBy(k => k.Key, StringComparer.Ordinal))
        Console.WriteLine($"  {kv.Key,-22} {kv.Value.Sat,6}/{kv.Value.Total,-6} "
                          + $"({100.0 * kv.Value.Sat / kv.Value.Total:0.0}%)");
    Console.WriteLine($"archetype cohesion {primaryHit}/{archSlots} ({Pct(primaryHit, archSlots)})");
    Console.WriteLine($"archetype slots taken from the primary {primarySlotHit}/{archSlots} "
                      + $"({Pct(primarySlotHit, archSlots)})");
    Console.WriteLine($"Lune weapons with >=1 stain effect {luneWithStain}/{luneWeapons} "
                      + $"({Pct(luneWithStain, luneWeapons)}), stain slots {luneStainSlots}");
    Console.WriteLine("primary archetype share (top 12):");
    foreach (var kv in primaryCounts.OrderByDescending(kv => kv.Value).Take(12))
        Console.WriteLine($"  {kv.Value,5}  {kv.Key}");
    var stainIds = catalog.EffectArchetypes.All
        .Where(a => !string.IsNullOrEmpty(a.Element))
        .SelectMany(a => a.Members)
        .Distinct(StringComparer.Ordinal)
        .Select(id => (Id: id, Element: catalog.EffectArchetypes.ElementOf(id) ?? "",
                       Hits: effectHits.GetValueOrDefault(id)))
        .Concat(cond.ElementGateEnabled
            ? cond.ElementEffects.Keys.Select(id => (Id: id, Element: cond.ElementOf(id) ?? "",
                                                     Hits: effectHits.GetValueOrDefault(id)))
            : Enumerable.Empty<(string Id, string Element, int Hits)>())
        .DistinctBy(x => x.Id)
        .OrderBy(x => x.Hits).ThenBy(x => x.Id, StringComparer.Ordinal)
        .ToList();
    Console.WriteLine($"element-tagged effects: {stainIds.Count}, never rolled: "
                      + stainIds.Count(x => x.Hits == 0));
    Console.WriteLine("  coldest element effects:");
    foreach (var x in stainIds.Take(10))
        Console.WriteLine($"    {x.Hits,5}  [{x.Element,-9}] {x.Id}");
    Console.WriteLine("  hottest element effects:");
    foreach (var x in stainIds.TakeLast(5))
        Console.WriteLine($"    {x.Hits,5}  [{x.Element,-9}] {x.Id}");
    Console.WriteLine($"extra fill {extraFilled}/{extraTriedWeapons} ({Pct(extraFilled, extraTriedWeapons)})");
    Console.WriteLine($"stain-echo slots {echoHits}/{totalSlots} ({Pct(echoHits, totalSlots)})");
    Console.WriteLine("echo share by character:");
    foreach (var ch in E33ModEditor.Core.Loot.WeaponEffectCharacters.Roster)
    {
        var slots = slotByChar.GetValueOrDefault(ch);
        var e = echoByChar.GetValueOrDefault(ch);
        var w = weaponsByChar.GetValueOrDefault(ch);
        Console.WriteLine($"  {ch,-8} weapons/run ~{w / (double)count:0.0}  echo {e}/{slots} ({Pct(e, slots)})");
    }

    Console.WriteLine();
    Console.WriteLine("never rolled (placed in T1–T5, 0 hits): " + unused.Count);
    foreach (var id in unused.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).Take(40))
        Console.WriteLine("  " + id);
    if (unused.Count > 40) Console.WriteLine("  …");

    Console.WriteLine();
    Console.WriteLine("hottest effects (top 15):");
    foreach (var kv in effectHits.OrderByDescending(kv => kv.Value).Take(15))
        Console.WriteLine($"  {kv.Value,5}  {kv.Key}  tier={catalog.EffectTiers.TierOf(kv.Key)}");

    Console.WriteLine();
    Console.WriteLine("coldest placed effects (bottom 15 with hits):");
    foreach (var kv in effectHits.OrderBy(kv => kv.Value).Take(15))
        Console.WriteLine($"  {kv.Value,5}  {kv.Key}  tier={catalog.EffectTiers.TierOf(kv.Key)}");

    return echoDup == 0 && charMismatch == 0 && idDup == 0
           && gustaveAuto == 0 && gustaveHigh == 0 && emptyCore == 0
           && elementMismatch == 0 && conditionConflicts == 0
           && statusConsumeStacks == 0 ? 0 : 2;

    static string Pct(int n, int d) => d == 0 ? "n/a" : $"{100.0 * n / d:0.0}%";

    static string? StainStartKey(string id)
    {
        if (id.Contains("Start", StringComparison.OrdinalIgnoreCase)
            && (id.StartsWith("Stains_", StringComparison.Ordinal)
                || id.StartsWith("WepFx_", StringComparison.Ordinal)))
        {
            foreach (var el in new[] { "Fire", "Ice", "Lightning", "Earth", "Light", "Dark" })
                if (id.Contains(el, StringComparison.OrdinalIgnoreCase))
                    return el;
        }
        return null;
    }

    static void PrintPools(E33ModEditor.Core.Loot.LootCatalog catalog,
        IReadOnlyDictionary<string, string> owners)
    {
        Console.WriteLine("eligible pool sizes (character × effect-tier):");
        Console.WriteLine($"  {"",-10} {"T1",6} {"T2",6} {"T3",6} {"T4",6} {"T5",6} {"echo",6}");
        foreach (var ch in E33ModEditor.Core.Loot.WeaponEffectCharacters.Roster)
        {
            var line = $"  {ch,-10}";
            var echo = 0;
            for (int t = 1; t <= 5; t++)
            {
                var n = catalog.EffectTiers.Pool(t).Count(id => catalog.EffectCharacters.Allows(id, ch));
                var e = catalog.EffectTiers.Pool(t).Count(id =>
                    catalog.EffectCharacters.Allows(id, ch)
                    && E33ModEditor.Core.Loot.WeaponEffectTiers.IsStainEcho(id));
                echo += e;
                line += $" {n,6}";
            }
            Console.WriteLine(line + $" {echo,6}");
        }

        var wep = new Dictionary<string, int[]>(StringComparer.Ordinal);
        foreach (var ch in E33ModEditor.Core.Loot.WeaponEffectCharacters.Roster)
            wep[ch] = new int[6];
        int noOwner = 0;
        for (int t = 1; t <= 5; t++)
        {
            foreach (var id in catalog.ItemTiers.Column(E33ModEditor.Core.Loot.ItemTiers.Weapon, "Tier" + t))
            {
                if (!owners.TryGetValue(id, out var ch) || ch.Length == 0) { noOwner++; continue; }
                wep[ch][t]++;
            }
        }
        Console.WriteLine($"weapons missing owner {noOwner}");
        Console.WriteLine("weapon counts by character × item-tier:");
        Console.WriteLine($"  {"",-10} {"T1",6} {"T2",6} {"T3",6} {"T4",6} {"T5",6}");
        foreach (var ch in E33ModEditor.Core.Loot.WeaponEffectCharacters.Roster)
        {
            var a = wep[ch];
            Console.WriteLine($"  {ch,-10} {a[1],6} {a[2],6} {a[3],6} {a[4],6} {a[5],6}");
        }
    }
}

/// <summary>Compact kismet probe (read-only): one line per top-level
/// statement in the named function (default OnActionEffect) listing called
/// functions, object consts, and int/float/name/string consts.</summary>
static int CmdKismetDump(string[] args)
{
    var path = Path.GetFullPath(args[1]);
    var fnName = args.Length > 2 ? args[2] : "OnActionEffect";
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
        : new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
        .FirstOrDefault(f => f.ObjectName.ToString() == fnName);
    if (fn?.ScriptBytecode is null) { Console.WriteLine("no function " + fnName); return 1; }
    int i = 0;
    uint koff = 0;
    foreach (var s in fn.ScriptBytecode)
    {
        var calls = new List<string>();
        var objs = new List<string>();
        var consts = new List<string>();
        MayhemFixKit.Walk(new[] { s }, e =>
        {
            switch (e)
            {
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_FinalFunction ff when ff.StackNode is { } sn && sn.IsImport():
                    calls.Add(sn.ToImport(u).ObjectName.ToString());
                    break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_ObjectConst oc when oc.Value.IsImport():
                    objs.Add(oc.Value.ToImport(u).ObjectName.ToString());
                    break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ic:
                    consts.Add("i" + ic.Value);
                    break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_FloatConst fc:
                    consts.Add("f" + fc.Value);
                    break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_NameConst nc:
                    consts.Add("n" + nc.Value.ToString());
                    break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_StringConst sc:
                    consts.Add("s" + sc.Value);
                    break;
            }
        });
        Console.WriteLine($"#{i++} @{koff} [{s.GetType().Name}] calls=[{string.Join(",", calls.Distinct())}] objs=[{string.Join(",", objs.Distinct())}] consts=[{string.Join(",", consts.Distinct())}]");
        koff += s.GetSize(u);
    }
    return 0;
}

    /// <summary>TEMP PROBE (remove after LastChance debug): function locals.</summary>
    static int CmdFnLocals(string[] args)
    {
        var path = Path.GetFullPath(args[1]);
        var fnName = args.Length > 2 ? args[2] : "OnActionEffect";
        var map = DataTableEditor.Mappings;
        var u = map != null
            ? new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
            : new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == fnName);
        if (fn is null) { Console.WriteLine("no function " + fnName); return 1; }
        Console.WriteLine($"FunctionFlags={fn.FunctionFlags}");
        foreach (var p in fn.LoadedProperties ?? Array.Empty<UAssetAPI.FieldTypes.FProperty>())
        {
            Console.WriteLine($"{p.GetType().Name} type={p.SerializedType} {p.Name} size={p.ElementSize} flags={p.PropertyFlags}");
            if (p is UAssetAPI.FieldTypes.FBoolProperty b)
                Console.WriteLine($"  BOOL FieldSize={b.FieldSize} ByteOffset={b.ByteOffset} ByteMask={b.ByteMask} FieldMask={b.FieldMask}");
        }
        return 0;
    }

    /// <summary>TEMP PROBE (remove after SoulTransplant debug): per-statement
/// GetSize vs true serialized size for a function's bytecode.</summary>
static int CmdStSizes(string[] args)
{
    var path = Path.GetFullPath(args[1]);
    var fnName = args.Length > 2 ? args[2] : "OnActionEffect";
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
        : new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
        .FirstOrDefault(f => f.ObjectName.ToString() == fnName);
    if (fn?.ScriptBytecode is null) { Console.WriteLine("no function " + fnName); return 1; }
    int i = 0;
    foreach (var s in fn.ScriptBytecode)
    {
        uint claimed = s.GetSize(u);
        int actual;
        using (var ms = new MemoryStream())
        {
            var w = new UAssetAPI.AssetBinaryWriter(ms, u);
            actual = UAssetAPI.Kismet.Bytecode.ExpressionSerializer.WriteExpression(s, w);
        }
        string flag = claimed == actual ? "ok" : "MISMATCH";
        Console.WriteLine($"#{i++} [{s.GetType().Name}] claimed={claimed} actual={actual} {flag}");
    }
    return 0;
}

/// <summary>Skill-graph dump (read-only): node index → skill import, SP
/// cost, position; then edges as From → To skill names.</summary>
static int CmdGraphDump(string[] args)
{
    var path = Path.GetFullPath(args[1]);
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
        : new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    var gx = u.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>().First();
    var nodes = (UAssetAPI.PropertyTypes.Objects.ArrayPropertyData)gx.Data[0];
    var edges = (UAssetAPI.PropertyTypes.Objects.ArrayPropertyData)gx.Data[1];
    string SkillName(UAssetAPI.UnrealTypes.FPackageIndex ptr)
    {
        if (ptr is null || !ptr.IsImport()) return "<null>";
        return u.Imports[Math.Abs(ptr.Index) - 1].ObjectName.ToString();
    }
    for (int i = 0; i < nodes.Value.Length; i++)
    {
        try
        {
            var ns = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)nodes.Value[i];
            var su = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)ns.Value[0];
            var skillProp = su.Value[0] as UAssetAPI.PropertyTypes.Objects.ObjectPropertyData;
            var skill = skillProp is null ? "?" : SkillName(skillProp.Value);
            var costProp = su.Value[1] as UAssetAPI.PropertyTypes.Objects.IntPropertyData;
            var pos = ns.Value[1] as UAssetAPI.PropertyTypes.Structs.StructPropertyData;
            var v = pos?.Value[0] as UAssetAPI.PropertyTypes.Structs.Vector2DPropertyData;
            var req = su.Value[3] as UAssetAPI.PropertyTypes.Structs.StructPropertyData;
            var reqObjProp = req?.Value[0] as UAssetAPI.PropertyTypes.Objects.ObjectPropertyData;
            var reqNameProp = (req?.Value.Count > 1 ? req.Value[1] : null) as UAssetAPI.PropertyTypes.Objects.NamePropertyData;
            var reqStr = reqObjProp is null ? "?" : SkillName(reqObjProp.Value);
            var reqNameStr = reqNameProp?.Value is null ? "<null>" : reqNameProp.Value.ToString();
            Console.WriteLine($"node#{i} {skill} sp={costProp?.Value} @({v?.Value.X},{v?.Value.Y}) req=[{reqStr}|{reqNameStr}]");
        }
        catch (Exception ex) { Console.WriteLine($"node#{i} <shape: {ex.GetType().Name} {ex.Message} :: {ex.StackTrace?.Split('\n')[0]}>"); }
    }
    for (int i = 0; i < edges.Value.Length; i++)
    {
        var es = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)edges.Value[i];
        var from = SkillName(((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)es.Value[0]).Value);
        var to = SkillName(((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)es.Value[1]).Value);
        Console.WriteLine($"edge#{i} {from} -> {to}");
    }
    return 0;
}

/// <summary>Reflection kismet tree (read-only): dumps one statement's full
/// expression tree with scalar fields. Usage: kistree &lt;uasset&gt; [fn]
/// [stmtIndex]. Cap output with head.</summary>
static int CmdKismetTree(string[] args)
{
    var path = Path.GetFullPath(args[1]);
    var fnName = args.Length > 2 ? args[2] : "OnActionEffect";
    int want = args.Length > 3 ? int.Parse(args[3]) : -1;
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
        : new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
        .FirstOrDefault(f => f.ObjectName.ToString() == fnName);
    if (fn?.ScriptBytecode is null) { Console.WriteLine("no function " + fnName); return 1; }
    int i = 0;
    uint off = 0;
    var offsets = new List<uint>();
    foreach (var s in fn.ScriptBytecode) { offsets.Add(off); off += (uint)s.GetSize(u); }
    foreach (var s in fn.ScriptBytecode)
    {
        if (want < 0 || want == i) { Console.WriteLine($"@@ stmt#{i} off={offsets[i]}"); KismetTreePrint(u, s, 0, i); }
        i++;
    }
    return 0;
}

static void KismetTreePrint(UAssetAPI.UAsset u, object node, int depth, int stmt)
{
    if (node is null || depth > 10) return;
    var pad = new string(' ', depth * 2);
    var t = node.GetType();
    if (t.Name == "KismetPropertyPointer")
    {
        Console.WriteLine($"{pad}#{stmt} PTR {FmtPtr(node as UAssetAPI.Kismet.Bytecode.KismetPropertyPointer)}");
        return;
    }
    var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
    if (node is UAssetAPI.Kismet.Bytecode.KismetExpression)
    {
        var scalars = new List<string>();
        foreach (var m in t.GetProperties(flags).Cast<System.Reflection.MemberInfo>().Concat(t.GetFields(flags)))
        {
            if (m is System.Reflection.PropertyInfo p && p.GetIndexParameters().Length > 0) continue;
            object v;
            try { v = m is System.Reflection.PropertyInfo pp ? pp.GetValue(node) : ((System.Reflection.FieldInfo)m).GetValue(node); } catch { continue; }
            if (v is null) continue;
            var name = m.Name;
            if (v is UAssetAPI.Kismet.Bytecode.KismetExpression || (v is System.Collections.IEnumerable && v is not string)) continue;
            if (v.GetType().IsPrimitive || v.GetType().IsEnum || v is string || v is decimal)
                scalars.Add($"{name}={v}");
            else if (v is UAssetAPI.UnrealTypes.FName fn)
                scalars.Add($"{name}=n{fn}");
            else if (v is UAssetAPI.UnrealTypes.FString fs)
                scalars.Add($"{name}=s{fs.Value}");
            else if (v is UAssetAPI.UnrealTypes.FPackageIndex pi)
                scalars.Add($"{name}={(pi.IsImport() ? "imp:" + pi.ToImport(u).ObjectName.ToString() + "[raw" + pi.Index + "]" : pi.IsExport() ? "exp" : "null")}");
            else scalars.Add($"{name}:{v.GetType().Name}");
        }
        Console.WriteLine($"{pad}#{stmt} {t.Name} {string.Join(" ", scalars)}");
        foreach (var m in t.GetProperties(flags).Cast<System.Reflection.MemberInfo>().Concat(t.GetFields(flags)))
        {
            if (m is System.Reflection.PropertyInfo p && p.GetIndexParameters().Length > 0) continue;
            object v;
            try { v = m is System.Reflection.PropertyInfo pp ? pp.GetValue(node) : ((System.Reflection.FieldInfo)m).GetValue(node); } catch { continue; }
            if (v is UAssetAPI.Kismet.Bytecode.KismetExpression)
                KismetTreePrint(u, v, depth + 1, stmt);
            else if (v is UAssetAPI.Kismet.Bytecode.KismetPropertyPointer kp)
            {
                Console.WriteLine($"{new string(' ', (depth + 1) * 2)}#{stmt} PTR {FmtPtr(kp)}");
            }
            else if (v is System.Collections.IEnumerable en && v is not string)
                foreach (var item in en)
                    if (item is UAssetAPI.Kismet.Bytecode.KismetExpression)
                        KismetTreePrint(u, item, depth + 1, stmt);
        }
    }
}

/// <summary>Debug: load an asset and write it back elsewhere untouched, to
/// test writer round-trip fidelity.</summary>
static int CmdRtWrite(string[] args)
{
    var src = Path.GetFullPath(args[1]);
    var dest = Path.GetFullPath(args[2]);
    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAssetAPI.UAsset(src, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
        : new UAssetAPI.UAsset(src, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    File.Copy(src, dest, true);
    var uexpSrc = Path.ChangeExtension(src, ".uexp");
    if (File.Exists(uexpSrc)) File.Copy(uexpSrc, Path.ChangeExtension(dest, ".uexp"), true);
    u.Write(dest);
    return 0;
}

static IEnumerable<string> CdoLeaves(UAssetAPI.UAsset u, UAssetAPI.PropertyTypes.Objects.PropertyData p)
{
    switch (p)
    {
        case UAssetAPI.PropertyTypes.Structs.VectorPropertyData v:
            yield return $"vec=({v.Value.X},{v.Value.Y},{v.Value.Z})"; break;
        case UAssetAPI.PropertyTypes.Structs.RotatorPropertyData r:
            yield return $"rot=(p{r.Value.Pitch},y{r.Value.Yaw},r{r.Value.Roll})"; break;
        case UAssetAPI.PropertyTypes.Objects.NamePropertyData nm:
            yield return "name=" + nm.Value; break;
        case UAssetAPI.PropertyTypes.Objects.IntPropertyData i:
            yield return "int=" + i.Value; break;
        case UAssetAPI.PropertyTypes.Objects.BoolPropertyData b:
            yield return "bool=" + b.Value; break;
        case UAssetAPI.PropertyTypes.Objects.EnumPropertyData e:
            yield return "enum=" + e.Value; break;
        case UAssetAPI.PropertyTypes.Structs.StructPropertyData s when s.Value != null:
            foreach (var c in s.Value) foreach (var l in CdoLeaves(u, c)) yield return l;
            break;
        case UAssetAPI.PropertyTypes.Objects.ArrayPropertyData a when a.Value != null:
            yield return "[" + a.Value.Length + "]";
            foreach (var c in a.Value) foreach (var l in CdoLeaves(u, c)) yield return l;
            break;
        case UAssetAPI.PropertyTypes.Objects.ObjectPropertyData o:
            yield return "obj=" + FmtIdx(u, o.Value); break;
        default: yield return p.GetType().Name; break;
    }
}

static int CmdDumpCdo(string[] args)
{
    var path = Path.GetFullPath(args[1]);
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
        : new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    foreach (var exp in u.Exports)
    {
        var ne = exp as UAssetAPI.ExportTypes.NormalExport;
        Console.WriteLine($"-- {exp.ObjectName} class={FmtIdx(u, exp.ClassIndex)} ({(ne?.Data.Count ?? 0)} props) size={exp.SerialSize} export={exp.GetType().Name} --");
        if (ne is null) continue;
        if (exp is UAssetAPI.ExportTypes.EnumExport en)
        {
            foreach (var (name, value) in en.Enum.Names)
                Console.WriteLine($"  enum {name} = {value}");
            Console.WriteLine($"  CppForm={en.Enum.CppForm}");
        }
        foreach (var p in ne.Data)
        {
            var n = p.Name?.ToString() ?? "";
            var v = p switch
            {
                UAssetAPI.PropertyTypes.Objects.BytePropertyData b =>
                    $"byte {b.EnumType} = {b.EnumValue} ({b.Value})",
                UAssetAPI.PropertyTypes.Objects.EnumPropertyData e =>
                    $"enum {e.EnumType} = {e.Value}",
                UAssetAPI.PropertyTypes.Objects.IntPropertyData i => $"int {i.Value}",
                UAssetAPI.PropertyTypes.Objects.Int64PropertyData l => $"i64 {l.Value}",
                UAssetAPI.PropertyTypes.Objects.FloatPropertyData f => $"float {f.Value}",
                UAssetAPI.PropertyTypes.Objects.DoublePropertyData dd => $"double {dd.Value}",
                UAssetAPI.PropertyTypes.Objects.BoolPropertyData bo => $"bool {bo.Value}",
                UAssetAPI.PropertyTypes.Objects.NamePropertyData nm => $"name {nm.Value}",
                UAssetAPI.PropertyTypes.Objects.StrPropertyData s => $"str {s.Value}",
                UAssetAPI.PropertyTypes.Objects.TextPropertyData t =>
                    $"text hist={t.HistoryType} val={t.Value} src={t.CultureInvariantString} table={t.TableId}",
                UAssetAPI.PropertyTypes.Objects.MapPropertyData m =>
                    "map " + string.Join(", ", m.Value.Select(kv => $"{Fmt(kv.Key)}=>{Fmt(kv.Value)}")),
                UAssetAPI.PropertyTypes.Objects.ObjectPropertyData o =>
                    "obj " + FmtIdx(u, o.Value),
                UAssetAPI.PropertyTypes.Structs.StructPropertyData s2 =>
                    "struct " + string.Join(", ", CdoLeaves(u, s2)),
                UAssetAPI.PropertyTypes.Objects.ArrayPropertyData a =>
                    "array[" + (a.Value?.Length ?? 0) + "] " + string.Join(" | ", (a.Value ?? []).SelectMany(x => CdoLeaves(u, x))),
                _ => p.GetType().Name,
            };
            Console.WriteLine($"  {n}: {v}");
        }
    }
    Console.WriteLine("name map (enum/stain/stance/rank/buff):");    foreach (var n in u.GetNameMapIndexList())
    {
        var s = n?.Value ?? "";
        if (s.Contains("Stain", StringComparison.OrdinalIgnoreCase)
            || s.Contains("Stance", StringComparison.OrdinalIgnoreCase)
            || s.Contains("Enumerator", StringComparison.Ordinal)
            || s.Contains("Rank", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("E_", StringComparison.Ordinal))
            Console.WriteLine("  " + s);
    }
    return 0;
}

static int CmdPictoTags(string[] args)
{
    var modDir = args.Length > 1 && !args[1].StartsWith("--", StringComparison.Ordinal)
        ? Path.GetFullPath(args[1])
        : Path.Combine(EditorPaths.RepoRoot, "work", "mod");
    if (!Directory.Exists(modDir))
    {
        Console.Error.WriteLine($"picto-tags: mod dir not found: {modDir}");
        return 1;
    }

    var changed = PictoTagKit.Apply(modDir);
    Console.WriteLine($"picto-tags: {changed} row(s) retagged in {modDir}");
    return 0;
}

static int CmdPictosSmoke(string[] args)
{
    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_pictosmoke");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);
    DataTableEditor.ResetMappings(); // pack runs start from pristine schemas

    var fails = new List<string>();
    void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "ok   " : "FAIL ") + what);
        if (!ok) fails.Add(what);
    }
    static string Leaf(string s) { var i = s.LastIndexOf("::") + 1; return i > 0 ? s[i..] : s; }
    static UAssetAPI.UAsset Open(string p)
    {
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAssetAPI.UAsset(p, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
            : new UAssetAPI.UAsset(p, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    }

    PictosCreateKit.CopyCreateTables(dir); // CLI/standalone build copies these before both kits
    var painters = PictosCreateKit.InjectPainterConversions(dir);
    Console.WriteLine("painters injected " + painters);
    var powers = PictosCreateKit.InjectStatusAffinities(dir);
    Console.WriteLine("powers injected " + powers);
    var hitburn = PictosCreateKit.InjectHitBurn(dir);
    Console.WriteLine("hitburn injected " + hitburn);
    var variants = PictosMagnitudeKit.Inject(dir, copyTables: false);
    var special = SpecialPictosKit.Apply(dir);
    Console.WriteLine("special pictos injected " + special);
    CutPictosKit.Apply(dir);
    BombPictosKit.Apply(dir);
    ReviveDebuffKit.Apply(dir);
    CleanseTweakKit.Apply(dir);
    PictoTagKit.Apply(dir);
    WeaponEffectsCreateKit.Reload();
    var wep = WeaponEffectsCreateKit.Inject(dir, copyTables: false);
    Console.WriteLine("weapon effects injected " + wep);

    // 0) class identity. Every created BP's generated class must match its own
    //    stem, because the passive rows reference "<stem>_C". A rename applied
    //    twice (an id containing the donor's short name, e.g. CursedPower3 off
    //    BP_LU_CursedPower) yields BP_LU_CursedPower33_C instead, the row's
    //    class is unresolvable, and the game crashes at battle load on equip.
    var classNames = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    foreach (var bp in Directory.EnumerateFiles(dir, "BP_LU_*.uasset", SearchOption.AllDirectories))
    {
        var stem = Path.GetFileNameWithoutExtension(bp);
        classNames[stem] = Open(bp).GetNameMapIndexList()
            .Select(n => n?.Value ?? "").Where(s => s.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }
    foreach (var (stem, names) in classNames)
        Check(names.Contains(stem + "_C"), stem + " defines its own class");

    static IEnumerable<(string Row, string Stem, string Class)> PassiveBuffRefs(string pePath)
    {
        var json = Newtonsoft.Json.Linq.JObject.Parse(new DataTableEditor(pePath).Dump());
        foreach (var row in json.Properties())
        {
            if (row.Value["BattleBuffs"] is not Newtonsoft.Json.Linq.JArray buffs) continue;
            foreach (var e in buffs)
            {
                var m = System.Text.RegularExpressions.Regex.Match(
                    e.ToString(), @"^\(([^,]+),\s*([^,]+),");
                if (m.Success)
                    yield return (row.Name, Path.GetFileName(m.Groups[1].Value.Trim()),
                        m.Groups[2].Value.Trim());
            }
        }
    }

    var peSmoke = PictosCreateKit.FindAsset(dir, "DT_PassiveEffects.uasset")!;
    int refs = 0;
    foreach (var (row, stem, klass) in PassiveBuffRefs(peSmoke))
    {
        if (!classNames.TryGetValue(stem, out var names)) continue; // vanilla package
        refs++;
        Check(names.Contains(klass), $"passive row {row} resolves {klass}");
    }
    Console.WriteLine($"passive rows referencing a created BP: {refs}");

    // 0a) categories/tags. Every catalog rule that made it into the pack must
    //     match exactly; every mod-owned picto row (new or changed) must carry
    //     at least one tag. This is the regression guard for the donor-tag bugs
    //     (Super Sonic inheriting Burn, Critical Chain inheriting Parry, etc.).
    {
        static HashSet<string> CategoriesOf(Newtonsoft.Json.Linq.JObject? row)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (row?["Categories"] is not Newtonsoft.Json.Linq.JArray arr) return set;
            foreach (var item in arr)
            {
                var s = item?.ToString() ?? "";
                var i = s.LastIndexOf("::", StringComparison.Ordinal);
                set.Add(i >= 0 ? s[(i + 2)..] : s);
            }
            return set;
        }

        var peJson = Newtonsoft.Json.Linq.JObject.Parse(new DataTableEditor(peSmoke).Dump());
        var catalogIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in PictoTagCatalog.Rules)
        {
            Check(catalogIds.Add(rule.Id), $"catalog id {rule.Id} is unique");
            Check(rule.Tags.All(PictoTagCatalog.AllTags.Contains),
                $"{rule.Id} uses known tags");
            Check(rule.Legacy is null || rule.Legacy.All(PictoTagCatalog.AllTags.Contains),
                $"{rule.Id} legacy tags are known");
        }
        foreach (var (tag, label) in PictoTagCatalog.NewTags)
            Check(PictoTagCatalog.Rules.Any(r => r.Tags.Contains(tag)),
                $"new {label} tag is used by at least one picto");

        int rulesChecked = 0;
        foreach (var rule in PictoTagCatalog.Rules)
        {
            if (peJson[rule.Id] is not Newtonsoft.Json.Linq.JObject row) continue;
            rulesChecked++;
            var cats = CategoriesOf(row);
            Check(cats.SetEquals(rule.Tags),
                $"{rule.Id} categories [{string.Join(",", cats)}] == [{string.Join(",", rule.Tags)}]");
        }
        Console.WriteLine($"picto category rules checked: {rulesChecked}");

        var vanillaPe = PictosCreateKit.FindAsset(
            Path.Combine(EditorPaths.RepoRoot, "work", "tables"),
            "DT_PassiveEffects.uasset");
        if (vanillaPe is not null)
        {
            var vanillaJson = Newtonsoft.Json.Linq.JObject.Parse(
                new DataTableEditor(vanillaPe).Dump());
            int custom = 0;
            foreach (var prop in peJson.Properties())
            {
                if (prop.Name.StartsWith("WepFx_", StringComparison.Ordinal)) continue;
                if (prop.Name.EndsWith("_Weapon", StringComparison.Ordinal)) continue;
                if (prop.Value?["Name"]?.ToString() == "Weapon") continue;
                bool ours = vanillaJson[prop.Name] is null
                    || !Newtonsoft.Json.Linq.JToken.DeepEquals(vanillaJson[prop.Name], prop.Value);
                if (!ours) continue;
                custom++;
                var cats = CategoriesOf((Newtonsoft.Json.Linq.JObject)prop.Value!);
                Check(cats.Count > 0, $"{prop.Name} has at least one category");
            }
            Console.WriteLine($"mod-owned picto rows checked: {custom}");
        }

        var enumPath = Path.Combine(dir, PictoTagKit.EnumRel + ".uasset");
        Check(File.Exists(enumPath), "extended E_PassiveEffectCategory staged");
        if (File.Exists(enumPath))
        {
            var ee = Open(enumPath).Exports.OfType<UAssetAPI.ExportTypes.EnumExport>()
                .FirstOrDefault();
            foreach (var (tag, label) in PictoTagCatalog.NewTags)
            {
                var full = PictoTagKit.EnumStem + "::" + tag;
                Check(ee is not null && ee.Enum.Names.Any(n => n.Item1.ToString() == full),
                    $"category enum defines {label} ({tag})");
            }
        }

        var ssCats = CategoriesOf(peJson[SpecialPictosKit.SuperSonicId] as Newtonsoft.Json.Linq.JObject);
        Check(ssCats.Contains(PictoTagCatalog.Speed)
            && !ssCats.Contains(PictoTagCatalog.Burn),
            "Super Sonic is Speed/Buff, not Burn");
        var ccCats = CategoriesOf(peJson["CriticalChain"] as Newtonsoft.Json.Linq.JObject);
        Check(ccCats.Contains(PictoTagCatalog.Critical)
            && !ccCats.Contains(PictoTagCatalog.Parry),
            "Critical Chain is Critical, not Parry");
        var rwpCats = CategoriesOf(peJson["ReviveWithPowerless"] as Newtonsoft.Json.Linq.JObject);
        Check(rwpCats.Contains(PictoTagCatalog.Debuff)
            && !rwpCats.Contains(PictoTagCatalog.Burn),
            "Revive with Powerless is Debuff, not Burn");
        var painterCats = CategoriesOf(peJson["PainterConv_Fire"] as Newtonsoft.Json.Linq.JObject);
        Check(painterCats.Contains(PictoTagCatalog.Element),
            "Painter conversion carries the Element tag");
        var lifestealCats = CategoriesOf(peJson["AttackLifesteal"] as Newtonsoft.Json.Linq.JObject);
        Check(lifestealCats.Contains(PictoTagCatalog.Lifesteal),
            "Attack Lifesteal carries the Lifesteal tag");
        var ruptureCats = CategoriesOf(peJson[SpecialPictosKit.RuptureId] as Newtonsoft.Json.Linq.JObject);
        Check(ruptureCats.Contains(PictoTagCatalog.Luck),
            "Rupture Shot carries the Luck tag");
    }

    // 0b) the two special unique pictos. Alicia's Suffering clones the cut
    //     burn-damage passive onto its own balancing key; Layered Canvas lists
    //     seven turn-start status clones in one passive. Both cost 100 lumina.
    {
        var balSpecial = Newtonsoft.Json.Linq.JObject.Parse(new DataTableEditor(
            PictosCreateKit.FindAsset(dir, "DT_Balancing_Luminas.uasset")!).Dump());
        var peSpecial = Newtonsoft.Json.Linq.JObject.Parse(new DataTableEditor(peSmoke).Dump());
        var aliciaBp = PictosCreateKit.FindAsset(dir, "BP_LU_" + SpecialPictosKit.AliciaId + ".uasset");
        Check(aliciaBp is not null, "Alicia's Suffering BP written");
        if (aliciaBp is not null)
        {
            var aliciaText = Text(Path.ChangeExtension(aliciaBp, ".uexp")) + Text(aliciaBp);
            Check(aliciaText.Contains(SpecialPictosKit.AliciaId + "_BurnDamage_DamageMultiplier", StringComparison.Ordinal),
                "Alicia's Suffering carries its own BurnDamage key");
            Check(!aliciaText.Contains("BurnDamage_BurnDamage_DamageMultiplier", StringComparison.Ordinal)
                || aliciaText.Contains(SpecialPictosKit.AliciaId + "_BurnDamage_DamageMultiplier", StringComparison.Ordinal),
                "Alicia's Suffering key is renamed");
        }
        Check((float?)balSpecial[SpecialPictosKit.AliciaId + "_BurnDamage_DamageMultiplier"]?["FloatValue"] == 10f,
            "Alicia's Suffering burn multiplier is 10.0 (+1000%)");
        Check((int?)peSpecial[SpecialPictosKit.AliciaId]?["LuminaCost"] == 100,
            "Alicia's Suffering costs 100 lumina");
        Check((int?)peSpecial[SpecialPictosKit.LayeredId]?["LuminaCost"] == 100,
            "Layered Canvas costs 100 lumina");
        var canvas = peSpecial[SpecialPictosKit.LayeredId];
        Check(canvas?["BattleBuffs"] is Newtonsoft.Json.Linq.JArray a && a.Count == 7,
            "Layered Canvas carries 7 status BattleBuffs");
        foreach (var key in new[] { "Burn", "Berserk", "Blight", "Regen", "Powerful", "Shell", "Rush" })
        {
            var stem = SpecialPictosKit.LayeredId + "_" + key;
            Check(PictosCreateKit.FindAsset(dir, "BP_LU_" + stem + ".uasset") is not null,
                $"Layered Canvas {key} BP written");
            Check((float?)balSpecial[stem + "_Duration"]?["FloatValue"] == 1f,
                $"Layered Canvas {key} lasts 1 turn");
        }

        // Super Sonic: Speed only at x3 curve, plus a custom +100% status
        // refreshed every turn start (cloned from vanilla Rush).
        Check((int?)peSpecial[SpecialPictosKit.SuperSonicId]?["LuminaCost"] == 100,
            "Super Sonic costs 100 lumina");
        var ssBuffs = peSpecial[SpecialPictosKit.SuperSonicId]?["BattleBuffs"];
        Check(ssBuffs is Newtonsoft.Json.Linq.JArray sub && sub.Count == 1
            && sub[0].ToString().Contains("BP_LU_" + SpecialPictosKit.SuperSonicId, StringComparison.Ordinal),
            "Super Sonic applies its turn-start applier");
        Check((float?)balSpecial[SpecialPictosKit.SuperSonicId + "_Duration"]?["FloatValue"] == 1f,
            "Super Sonic refreshes each turn");
        var ssDef = Newtonsoft.Json.Linq.JObject.Parse(new DataTableEditor(
            PictosCreateKit.FindAsset(dir, "DT_PictosDefinitions.uasset")!).Dump());
        Check((float?)ssDef[SpecialPictosKit.SuperSonicId]?["BaseStatMultipliers"]
                ?["E_jRPG_StatType::NewEnumerator7"] == 3.0f,
            "Super Sonic grants Speed x3.0");
        var ssApplier = PictosCreateKit.FindAsset(dir, "BP_LU_" + SpecialPictosKit.SuperSonicId + ".uasset");
        Check(ssApplier is not null, "Super Sonic applier BP written");
        if (ssApplier is not null)
        {
            var ssText = Text(Path.ChangeExtension(ssApplier, ".uexp")) + Text(ssApplier);
            Check(ssText.Contains("BP_LU_" + SpecialPictosKit.SuperSonicId + "_Buff", StringComparison.Ordinal)
                && !ssText.Contains("BP_BattleBuff_Burn", StringComparison.Ordinal),
                "Super Sonic applier targets the custom status, not Burn");
        }
        var ssBuff = PictosCreateKit.FindAsset(dir,
            "BP_LU_" + SpecialPictosKit.SuperSonicId + "_Buff.uasset");
        Check(ssBuff is not null, "Super Sonic status buff written");
        if (ssBuff is not null)
        {
            var ba = Open(ssBuff);
            double? mult = null;
            foreach (var exp in ba.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>())
                foreach (var p in exp.Data)
                    if (p is UAssetAPI.PropertyTypes.Objects.ArrayPropertyData arr
                        && DataTableEditor.CleanName(p.Name) == "CharacterStatMultipliers")
                        foreach (var el in arr.Value.OfType<UAssetAPI.PropertyTypes.Structs.StructPropertyData>())
                            foreach (var f in el.Value)
                                if (f is UAssetAPI.PropertyTypes.Objects.DoublePropertyData dd
                                    && DataTableEditor.CleanName(f.Name) == "Multiplier")
                                    mult = dd.Value;
            Check(mult == SpecialPictosKit.SuperSonicStatusMultiplier,
                "Super Sonic status Speed multiplier is 2.0 (+100%)");
        }

        // Rupture Shot: Free-Aim Inverted application, 100 lumina, chance raised.
        Check((int?)peSpecial[SpecialPictosKit.RuptureId]?["LuminaCost"] == 100,
            "Rupture Shot costs 100 lumina");
        var rsBuffs = peSpecial[SpecialPictosKit.RuptureId]?["BattleBuffs"];
        Check(rsBuffs is Newtonsoft.Json.Linq.JArray rsb && rsb.Count == 1
            && rsb[0].ToString().Contains("BP_LU_" + SpecialPictosKit.RuptureId, StringComparison.Ordinal),
            "Rupture Shot applies its own passive");
        Check((float?)balSpecial[SpecialPictosKit.RuptureId + "_ChancePercentage"]?["FloatValue"] == 50f,
            "Rupture Shot chance is 50%");
        Check((float?)balSpecial[SpecialPictosKit.RuptureId + "_BuffToApplyDuration"]?["FloatValue"] == 5f,
            "Rupture Shot Inverted lasts 5 turns");
        var rsDef = Newtonsoft.Json.Linq.JObject.Parse(new DataTableEditor(
            PictosCreateKit.FindAsset(dir, "DT_PictosDefinitions.uasset")!).Dump());
        Check((float?)rsDef[SpecialPictosKit.RuptureId]?["BaseStatMultipliers"]
                ?["E_jRPG_StatType::NewEnumerator7"] == 1.5f
            && (float?)rsDef[SpecialPictosKit.RuptureId]?["BaseStatMultipliers"]
                ?["E_jRPG_StatType::NewEnumerator6"] == 0.5f,
            "Rupture Shot grants Speed 1.5 / Crit 0.5");
        var rsBp = PictosCreateKit.FindAsset(dir, "BP_LU_" + SpecialPictosKit.RuptureId + ".uasset");
        Check(rsBp is not null, "Rupture Shot passive BP written");
        if (rsBp is not null)
        {
            var rsText = Text(Path.ChangeExtension(rsBp, ".uexp")) + Text(rsBp);
            Check(rsText.Contains("BP_BattleBuff_Inverted", StringComparison.Ordinal)
                && rsText.Contains(SpecialPictosKit.RuptureId + "_ChancePercentage", StringComparison.Ordinal)
                && !rsText.Contains("FreeAimInvertedShot", StringComparison.Ordinal),
                "Rupture Shot targets Inverted and renames its donor keys");
            var ra = Open(rsBp);
            double? chance = null;
            foreach (var exp in ra.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>())
                foreach (var p in exp.Data)
                    if (DataTableEditor.CleanName(p.Name) == "ChancePercentage"
                        && p is UAssetAPI.PropertyTypes.Objects.DoublePropertyData cd)
                        chance = cd.Value;
            Check(chance == SpecialPictosKit.RuptureChance,
                "Rupture Shot CDO chance is 50");
        }

        // Uno Puncho: 21 lumina, direct x10 on the first hit and x0 afterwards,
        // plus the fixed Ono Puncho dialogue grant.
        Check((int?)peSpecial[SpecialPictosKit.UnoPunchoId]?["LuminaCost"]
                == SpecialPictosKit.UnoPunchoLuminaCost,
            "Uno Puncho costs 21 lumina");
        Check((float?)balSpecial[SpecialPictosKit.UnoPunchoId + "_DamageIncrease"]?["FloatValue"]
                == SpecialPictosKit.UnoPunchoDamageIncrease,
            "Uno Puncho first-hit multiplier is 10x");
        Check((float?)balSpecial[SpecialPictosKit.UnoPunchoId + "_DamageDecrease"]?["FloatValue"]
                == SpecialPictosKit.UnoPunchoDamageDecrease,
            "Uno Puncho later-hit multiplier is 0x");
        var unoBp = PictosCreateKit.FindAsset(dir,
            "BP_LU_" + SpecialPictosKit.UnoPunchoId + ".uasset");
        Check(unoBp is not null, "Uno Puncho passive BP written");
        if (unoBp is not null)
        {
            var ua = Open(unoBp);
            bool marksSpent = false;
            bool directMultiplier = false;
            foreach (var fn in ua.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>())
                foreach (var st in fn.ScriptBytecode ?? Array.Empty<UAssetAPI.Kismet.Bytecode.KismetExpression>())
                    MayhemFixKit.Walk(new[] { st }, e =>
                    {
                        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LetBool lb
                            && lb.VariableExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_InstanceVariable iv
                            && PartyFiveKit.FieldName(iv.Variable) == "HasDied"
                            && lb.AssignmentExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_True)
                            marksSpent = true;
                        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_Let let
                            && PartyFiveKit.FieldName(let.Value)?.StartsWith("Type_17_", StringComparison.Ordinal) == true
                            && let.Expression is UAssetAPI.Kismet.Bytecode.Expressions.EX_ByteConst bc
                            && bc.Value == 2)
                            directMultiplier = true;
                    });
            Check(marksSpent, "Uno Puncho marks its state after the first hit");
            Check(directMultiplier,
                "Uno Puncho uses direct-multiplier modifiers (0x cannot become healing)");
            var unoText = Text(Path.ChangeExtension(unoBp, ".uexp")) + Text(unoBp);
            Check(unoText.Contains(SpecialPictosKit.UnoPunchoId + "_DamageIncrease", StringComparison.Ordinal)
                && !unoText.Contains("FirstLife_DamageIncrease", StringComparison.Ordinal),
                "Uno Puncho carries its own balancing keys");
        }
        // The reward is a data-only CDO row swap: the dialogue keeps exactly its
        // one vanilla AddItemToInventory call, but that call now grants
        // UnoPuncho instead of the Potatoe Bag. No second call is inserted.
        var onoDialogue = PictosCreateKit.FindAsset(dir, "BP_Dialogue_GV_OnoPuncho.uasset");
        Check(onoDialogue is not null, "Ono Puncho dialogue staged with replaced reward");
        if (onoDialogue is not null)
        {
            int grants = 0;
            bool hasPictoInBytecode = false;
            foreach (var fn in Open(onoDialogue).Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>())
                MayhemFixKit.Walk(fn.ScriptBytecode ?? Array.Empty<UAssetAPI.Kismet.Bytecode.KismetExpression>(), e =>
                {
                    if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf
                        && vf.VirtualFunctionName.ToString() == "AddItemToInventory")
                        grants++;
                    if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_NameConst nc
                        && nc.Value.ToString() == SpecialPictosKit.UnoPunchoId)
                        hasPictoInBytecode = true;
                });
            var reward = E33ModEditor.Core.Loot.DialogueRewardKit.ReadRewardId(onoDialogue);
            Check(grants == 1, "Ono Puncho dialogue keeps exactly one vanilla inventory call");
            Check(!hasPictoInBytecode, "Ono Puncho dialogue does not inject the picto into bytecode");
            Check(reward == SpecialPictosKit.UnoPunchoId,
                $"Ono Puncho dialogue Reward row is {reward ?? "<missing>"}");
        }
    }

    // 1) every painter clone converts to its intended element.
    //    Sources are opened first so dependency schemas stay resolvable for
    //    the renamed clones (same mechanism the kits rely on).
    foreach (var spec in PictosCreateKit.PainterConversions)
    {
        var clone = PictosCreateKit.FindAsset(dir, "BP_LU_" + spec.Id + ".uasset");
        if (clone is null) { Check(false, spec.Id + " BP written"); continue; }
        var srcName = spec.SourceBp == "clone" ? "BP_LU_SkillsConversionToVoid" : spec.SourceBp;
        _ = Open(FileCopy.FindTablesAsset(srcName + ".uasset")!);
        var a = Open(clone);
        var elem = a.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>()
            .SelectMany(e => e.Data).OfType<UAssetAPI.PropertyTypes.Objects.EnumPropertyData>()
            .FirstOrDefault(p => p.Name.ToString() == "NewElement");
        Check(elem is not null && Leaf(elem.Value.ToString() ?? "") == Leaf(spec.ElementEnum),
            $"{spec.Id} NewElement -> {Leaf(spec.ElementEnum)}");
    }

    // 3) power clones carry their own balancing key and the right buff import
    string Text(string p)
    {
        using var stream = File.OpenRead(p);
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    }
    foreach (var spec in PictosCreateKit.Powers)
    {
        var clone = PictosCreateKit.FindAsset(dir, "BP_LU_" + spec.Id + ".uasset");
        if (clone is null) { Check(false, spec.Id + " BP written"); continue; }
        var text = Text(Path.ChangeExtension(clone, ".uexp")) + Text(clone);
        Check(text.Contains(spec.BalancingKey, StringComparison.Ordinal)
            && text.Contains(spec.BuffClass, StringComparison.Ordinal),
            $"{spec.Id} key + {spec.BuffClass}");
    }

    // 4) status-swap clones retarget imports; no source-buff strings remain
    (string Id, string MustHave, string MustNotHave)[] swaps =
    {
        ("AutoBerserk", "BP_BattleBuff_Berserk", "BP_BattleBuff_Speed"),
        ("AutoBlight", "BP_BattleBuff_Blight", "BP_BattleBuff_Speed"),
        ("SosCurse", "BP_BattleBuff_Cursed", "BP_BattleBuff_Powerful_125"),
        ("SosBerserk", "BP_BattleBuff_Berserk", "BP_BattleBuff_Powerful_125"),
        ("SosBlight", "BP_BattleBuff_Blight", "BP_BattleBuff_Powerful_125"),
        ("ReviveWithCurse", "BP_BattleBuff_Cursed", "BP_BattleBuff_Regen"),
        ("ReviveWithBerserk", "BP_BattleBuff_Berserk", "BP_BattleBuff_Regen"),
        ("ReviveWithBlight", "BP_BattleBuff_Blight", "BP_BattleBuff_Regen"),
        ("CursingDeath", "BP_BattleBuff_Cursed", "BP_BattleBuff_Defense15"),
        ("BerserkingDeath", "BP_BattleBuff_Berserk", "BP_BattleBuff_Defense15"),
        ("BlightingDeath", "BP_BattleBuff_Blight", "BP_BattleBuff_Defense15"),
    };
    foreach (var (id, must, mustNot) in swaps)
    {
        var clone = PictosCreateKit.FindAsset(dir, "BP_LU_" + id + ".uasset");
        if (clone is null) { Check(false, id + " BP written"); continue; }
        var text = Text(clone);
        Check(text.Contains(must, StringComparison.Ordinal) && !text.Contains(mustNot, StringComparison.Ordinal),
            $"{id} -> {must} (source buff removed)");
    }

    // status never applies. SOS triggers carry it on their own StatusToApply
    // (the trigger inherits the apply-on-low-HP logic from the shared
    // BP_LU_SosStatusEffect sub-buff).
    var swapTargetPaths = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["SosCurse"] = "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Cursed",
        ["SosBerserk"] = "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Berserk",
        ["SosBlight"] = "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Blight",
        ["ReviveWithCurse"] = "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Cursed",
        ["ReviveWithBerserk"] = "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Berserk",
        ["ReviveWithBlight"] = "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Blight",
        ["CursingDeath"] = "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Cursed",
        ["BerserkingDeath"] = "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Berserk",
        ["BlightingDeath"] = "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Blight",
    };
    foreach (var (id, targetPath) in swapTargetPaths)
    {
        var clone = PictosCreateKit.FindAsset(dir, "BP_LU_" + id + ".uasset");
        if (clone is null) continue;
        var text = Text(clone);
        Check(text.Contains(targetPath, StringComparison.Ordinal),
            $"{id} buff import outer -> {targetPath}");
        // BP_BattleBuff_Powerful_125 (the "_125" is the FName Number, not a
        // string). A name-map sweep can't match it; if the import retarget
        // drops, the clone references BP_BattleBuff_<Status>_125, which does
        // not exist in the game and hangs the engine on battle load. Assert
        // no such residual survives.
        Check(!text.Contains("_125_C", StringComparison.Ordinal),
            $"{id} no residual _125 status class");
        // SOS triggers must KEEP inheriting the apply-on-low-HP logic from the
        // shared vanilla sub-buff (BP_LU_SosStatusEffect_C). Re-parenting the
        // trigger to BP_BattleBuffInstance used to orphan that logic and left
        // a dangling Default__BP_LU_SosStatusEffect import at the renamed
        // path, which froze the game on battle load. Assert the SuperStruct
        // still resolves to the vanilla sub-buff and its CDO keeps
        // StatusToApply (pointing at the target status).
        if (id.StartsWith("Sos", StringComparison.Ordinal))
        {
            // Prime the source sub-buff and the source trigger (same class
            // chain) so the clone's CDO parses with its StatusToApply property.
            var subSrc = FileCopy.FindTablesAsset("BP_LU_SosStatusEffect.uasset");
            if (subSrc is not null) _ = Open(subSrc);
            var sosSrc = FileCopy.FindTablesAsset("BP_LU_SosPower.uasset");
            if (sosSrc is not null) _ = Open(sosSrc);
            var a = Open(clone);
            var ce = a.Exports.OfType<UAssetAPI.ExportTypes.ClassExport>().FirstOrDefault();
            bool inheritsSubBuff = ce is not null && ce.SuperStruct.IsImport()
                && (a.Imports[-ce.SuperStruct.Index - 1].ObjectName?.ToString() ?? "")
                    == "BP_LU_SosStatusEffect_C"
                && (a.Imports[-ce.SuperStruct.Index - 1].OuterIndex.IsImport()
                    ? a.Imports[-a.Imports[-ce.SuperStruct.Index - 1].OuterIndex.Index - 1].ObjectName?.ToString()
                    : "") == "/Game/Gameplay/Lumina/Passives/SosPictos/BP_LU_SosStatusEffect";
            Check(inheritsSubBuff, $"{id} inherits vanilla SosStatusEffect sub-buff");
            // The CDO's StatusToApply is the applied-status class reference.
            // In this CLI process the shared mapping cache is poisoned by the
            // many prior Open() calls, so the CDO may not re-parse its
            // properties; when it does, assert it points at the target class.
            var cdo = a.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>().FirstOrDefault();
            if (cdo is not null && cdo.Data.Count > 0)
            {
                var st = cdo.Data.OfType<UAssetAPI.PropertyTypes.Objects.ObjectPropertyData>()
                    .FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "StatusToApply");
                bool statusRight = false;
                if (st is not null && st.Value.IsImport())
                {
                    var imp = a.Imports[-st.Value.Index - 1];
                    statusRight = (imp.ObjectName?.ToString() ?? "") == Path.GetFileName(targetPath) + "_C"
                        && imp.OuterIndex.IsImport()
                        && (a.Imports[-imp.OuterIndex.Index - 1].ObjectName?.ToString() ?? "") == targetPath;
                }
                Check(statusRight, $"{id} CDO StatusToApply -> {Path.GetFileName(targetPath)}_C @ {targetPath}");
            }
        }
    }

    // 5) weapon self-status ladders generated with per-tier keys
    foreach (var status in new[] { "Blight", "Berserk" })
    for (int tier = 1; tier <= 3; tier++)
    {
        var id = $"WepFx_Self{status}{tier}";
        Check(WeaponEffectsCreateKit.TryGet(id, out _), id + " spec exists");
    }

    // 3) the fire clone's payload must differ from the void source it was cloned from
    var fireUexp = Path.ChangeExtension(PictosCreateKit.FindAsset(dir, "BP_LU_PainterConv_Fire.uasset"), ".uexp");
    var voidUexp = Path.ChangeExtension(FileCopy.FindTablesAsset("BP_LU_SkillsConversionToVoid.uasset"), ".uexp");
    Check(!File.ReadAllBytes(fireUexp!).SequenceEqual(File.ReadAllBytes(voidUexp!)),
        "PainterConv_Fire payload differs from void source");

    // 4) balancing: reworked vanilla values, new keys, dead Auto keys absent
    var balPath = PictosCreateKit.FindAsset(dir, "DT_Balancing_Luminas.uasset")
        ?? throw new FileNotFoundException("DT_Balancing_Luminas");
    var bal = new DataTableEditor(balPath);
    Check(FloatRow(bal, "StunBoost_DamageIncrease") == "0.1", "vanilla StunBoost lowered to 0.1 (tier I)");
    Check(FloatRow(bal, "CursedPower_DamageIncrease") == "0.05", "vanilla CursedPower lowered to 0.05 (tier I)");
    Check(FloatRow(bal, "GlassCanon2_DamageMultiplier") == "1.25", "GlassCanon2_DamageMultiplier = 1.25");
    Check(FloatRow(bal, "GlassCanon3_DamageReceivedMultiplier") == "2", "GlassCanon3_DamageReceivedMultiplier = 2");
    Check(FloatRow(bal, "BurnDurationIncrease3_DurationIncrease") == "4", "BurnDurationIncrease3_DurationIncrease = 4");
    Check(FloatRow(bal, "DoubleBurn2_BurnMultiplier") == "3", "DoubleBurn2_BurnMultiplier = 3");
    Check(FloatRow(bal, "ConsumingAttack2_MaxBurnConsumed") == "250", "ConsumingAttack2_MaxBurnConsumed = 250");
    Check(FloatRow(bal, "StunBoost5_DamageIncrease") == "1.5", "StunBoost5_DamageIncrease = 1.5");
    Check(FloatRow(bal, "BerserkPower3_DamageIncrease") == "0.2", "BerserkPower3_DamageIncrease = 0.2");
    Check(FloatRow(bal, "BlightedPower1_DamageIncrease") == "0.05", "BlightedPower1_DamageIncrease = 0.05");
    Check(FloatRow(bal, "WepFx_SelfBlight3_DamageIncreasePerBlightStack") == "0.3",
        "WepFx_SelfBlight3 per-stack = 0.3");
    Check(FloatRow(bal, "WepFx_Immaculate5_DamageIncrease") == "2", "WepFx_Immaculate5 = 2.0");
    Check(FloatRow(bal, "WepFx_StunBoost5_DamageIncrease") == "3", "WepFx_StunBoost5 = 3.0");
    Check(FloatRow(bal, "WepFx_CounterDmg2_DamageMultiplier") == "3",
        "WepFx_CounterDmg2 = 3.0 (x3, +200%)");
    Check(FloatRow(bal, "WepFx_CounterDmg3_DamageMultiplier") == "4",
        "WepFx_CounterDmg3 = 4.0 (x4, +300%)");
    Check(FloatRow(bal, "WepFx_FrancoisShell1_DamageMultiplier") == "1", "WepFx_FrancoisShell1 dealt 1.0");
    Check(FloatRow(bal, "WepFx_FrancoisShell3_DamageReceivedMultiplier") == "0.7", "WepFx_FrancoisShell3 taken 0.7");
    // Parry Specialist: the BP builds serialized FDamageModifier type 2, which
    // is a direct multiplier. 2.0 is the true x2 ("double"); 1.0 would be x1.
    Check(FloatRow(bal, "ParrySpecialist_ParryWindowDecrease") == "-0.1",
        "Parry Specialist I window = -10%");
    Check(FloatRow(bal, "ParrySpecialist_CounterDamageMultiplier") == "2",
        "Parry Specialist I counter = 2.0 (x2)");
    Check(FloatRow(bal, "ParrySpecialist2_CounterDamageMultiplier") == "2",
        "Parry Specialist II counter = 2.0 (x2)");
    Check(FloatRow(bal, "ParrySpecialist3_CounterDamageMultiplier") == "2",
        "Parry Specialist III counter = 2.0 (x2)");
    Check(WeaponEffectsCreateKit.TryGet("WepFx_Break2", out _), "existing WepFx_Break2 kept");
    Check(!WeaponEffectsCreateKit.TryGet("WepFx_DeathsDividend1", out _), "no DeathsDividend weapon clone");

    // 4b) Draining Cleanse rework: the AP price is data-driven (the BP gates and
    //     spends DispelOnAPConsume_APConsumed), so the balancing float, the
    //     passive lumina cost and the shared hover key must all move together.
    Check(FloatRow(bal, "DispelOnAPConsume_APConsumed") == "10", "Draining Cleanse AP cost 10");
    {
        var peCleanse = new DataTableEditor(PictosCreateKit.FindAsset(dir, "DT_PassiveEffects.uasset")!);
        if (peCleanse.TryGetRow("DispelOnAPConsume", out var dcRow))
        {
            var lc = dcRow.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "LuminaCost")
                as UAssetAPI.PropertyTypes.Objects.IntPropertyData;
            Check(lc?.Value == 21, "Draining Cleanse lumina cost 21");
        }
        else Check(false, "DispelOnAPConsume passive row present");
        var stCleanse = PictosCreateKit.FindAsset(dir, "ST_PassiveEffects.uasset");
        Check(stCleanse is not null
              && DataTableEditor.OpenStringTable(stCleanse).TryGetValue(
                  "PASSIVE_DispelOnAPConsume_Description", out var dcDesc)
              && dcDesc.Contains("10 AP", StringComparison.Ordinal),
            "Draining Cleanse description says 10 AP");
    }

    // 5) table rows reference the generated content
    var itemsPath = PictosCreateKit.FindAsset(dir, "DT_jRPG_Items.uasset")
        ?? throw new FileNotFoundException("DT_jRPG_Items");
    var items = new DataTableEditor(itemsPath);
    Check(items.HasRow("PainterConv_Fire"), "item row PainterConv_Fire");
    Check(items.HasRow("BerserkPower1") && items.HasRow("BlightedPower3"), "item rows for powers");
    Check(items.HasRow("AutoBerserk") && items.HasRow("SosCurse"), "item rows for auto/sos swaps");
    Check(items.HasRow("ReviveWithCurse") && items.HasRow("CursingDeath"), "item rows for revive/death swaps");
    var iconPath = PictosCreateKit.FindAsset(dir, "DT_PictoIcons.uasset");
    Check(iconPath is not null, "DT_PictoIcons staged");
    Check(iconPath is not null && iconPath.Replace('\\', '/').Contains("/Resources/DataTables/PictoIcons/", StringComparison.Ordinal),
        "DT_PictoIcons under DataTables (not Datatables)");
    if (iconPath is not null)
    {
        var icons = new DataTableEditor(iconPath);
        Check(icons.HasRow("BurnOnHit1") && icons.HasRow("BurnOnHit5"), "PictoIcons BurnOnHit I–V");
        Check(icons.HasRow("PainterConv_Fire"), "PictoIcons PainterConv_Fire");
        Check(icons.HasRow("BerserkPower1"), "PictoIcons BerserkPower1");
        Check(icons.HasRow("BestDefense2") && icons.HasRow("Infinite"), "PictoIcons BestDefense2 + Infinite");
        Check(icons.HasRow("BurningDodge") && icons.HasRow("Painter"), "PictoIcons vanilla donors kept");
    }
    var pePath = PictosCreateKit.FindAsset(dir, "DT_PassiveEffects.uasset")
        ?? throw new FileNotFoundException("DT_PassiveEffects");
    var pe = new DataTableEditor(pePath);
    foreach (var (parryId, parryCost) in new[]
             { ("ParrySpecialist", 5), ("ParrySpecialist2", 10), ("ParrySpecialist3", 20) })
    {
        if (pe.TryGetRow(parryId, out var parryRow))
        {
            var lc = parryRow.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "LuminaCost")
                as UAssetAPI.PropertyTypes.Objects.IntPropertyData;
            Check(lc?.Value == parryCost, $"{parryId} lumina cost {parryCost}");
        }
        else Check(false, parryId + " passive row present");
    }
    foreach (var (costId, cost) in new[]
             {
                 ("ParryHelper", 5), ("ParryHelper2", 10), ("ParryHelper3", 15),
                 ("LongerDodge", 5), ("Dodger2", 10), ("Dodger3", 15),
                 ("AutoCurse", 10), ("AutoBerserk", 10), ("AutoBlight", 10),
                 ("OverPowered", 33),
                 ("BurnOnHit1", 1), ("BurnOnHit2", 3), ("BurnOnHit3", 5),
                 ("BurnOnHit4", 10), ("BurnOnHit5", 15),
             })
        CheckPassiveCost(pe, costId, cost);
    // Random Defense: the base and all four clones must carry the narrowed
    // 50..50 roll + 0.75 reciprocal scale that makes the existing RandomBool
    // select exactly 0.5 or 1.5. Low/High rows stay 0.5/1.5.
    foreach (var rdId in new[] { "RandomDefense", "RandomDefense2", "RandomDefense3", "RandomDefense4", "RandomDefense5" })
    {
        var rdPath = PictosCreateKit.FindAsset(dir, "BP_LU_" + rdId + ".uasset");
        if (rdPath is null) { Check(false, rdId + " BP present"); continue; }
        var rdAsset = Open(rdPath);
        var rdFn = rdAsset.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "ProcessReceivedRawDamages");
        bool has50 = false, has075 = false, has100 = false, has1 = false;
        if (rdFn?.ScriptBytecode is not null)
            MayhemFixKit.Walk(rdFn.ScriptBytecode, e =>
            {
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ic)
                {
                    if (ic.Value == 50) has50 = true;
                    if (ic.Value == 100) has100 = true;
                }
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst dc)
                {
                    if (Math.Abs(dc.Value - 0.75) < 1e-6) has075 = true;
                    if (Math.Abs(dc.Value - 1.0) < 1e-6) has1 = true;
                }
            });
        Check(has50 && has075 && !has100 && !has1,
            $"{rdId} random pick = 0.5/1.5 (50+0.75, no 100/1)");
    }
    Check(pe.HasRow("PainterConv_Fire") && pe.HasRow("ReviveWithBlight"), "passive rows for created pictos");
    if (pe.TryGetRow("PainterConv_Fire", out var pfRow))
    {
        var buffs = pfRow.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "BattleBuffs")
            as UAssetAPI.PropertyTypes.Objects.ArrayPropertyData;
        var target = buffs is { Value.Length: > 0 } ? buffs.Value[0]?.ToString() ?? "" : "";
        Check(target.Contains("/Game/Gameplay/Lumina/Passives/BP_LU_PainterConv_Fire", StringComparison.Ordinal),
            "PainterConv_Fire BattleBuffs -> clone package (" + target + ")");
    }

    string[] newIds =
    {
        "DoubleAP2", "BestDefense5", "PowerOfPain5", "FirstOffensive5", "AP+1TurnStart3",
        "EmpoweringJumps5", "BreakDamageOnCrit5", "BurningDeath5", "EnergyDeath5",
        "RandomDefense5", "ConfidentFighter5", "BreakingCounter5", "HealingParry3",
        "ManOfParry3", "ReinforcementParade3", "LuckyAim3", "Stand5", "GradientOnCrit2",
        "Infinite", "DeathsDividend",
    };
    foreach (var id in newIds)
    {
        Check(items.HasRow(id), "item row " + id);
        Check(pe.HasRow(id), "passive row " + id);
        Check(PictosCreateKit.FindAsset(dir, "BP_LU_" + id + ".uasset") != null, "BP_LU_" + id);
        Check(PictosCreateKit.FindAsset(dir, "DA_GearPictos_" + id + ".uasset") != null, "DA_GearPictos_" + id);
    }

    // Case-casing guard: vanilla DA_GearPictos_firstOffensive uses a lowercase
    // FolderName/object while the file/spec historically used uppercase
    // (VD_Machinapieds_Alpha dropped FirstOffensive5 as Combo Attack II +
    // TheOne text). Clones must not retain the donor stem in any casing.
    foreach (var tier in new[] { "FirstOffensive2", "FirstOffensive3", "FirstOffensive4", "FirstOffensive5" })
    {
        var daPath = PictosCreateKit.FindAsset(dir, "DA_GearPictos_" + tier + ".uasset");
        Check(daPath is not null, "DA_GearPictos_" + tier + " staged (case guard)");
        if (daPath is null) continue;
        var da = Open(daPath);
        var folder = da.FolderName.Value ?? "";
        Check(folder.EndsWith("DA_GearPictos_" + tier, StringComparison.Ordinal),
            $"{tier} FolderName ends with own stem ({folder})");
        var names = da.GetNameMapIndexList().Select(n => n?.Value ?? "").ToHashSet(StringComparer.Ordinal);
        Check(names.Contains("DA_GearPictos_" + tier),
            $"{tier} name map contains own stem");
        Check(!names.Contains("DA_GearPictos_firstOffensive"),
            $"{tier} no lowercase donor stem remains");
    }

    // Created item rows must reference their Data Asset with the REAL class
    // package (vanilla rows use it). "/Script/Engine" is a fake Engine class:
    // the Pictos_Data hard ref then fails to link, the tooltip widget cannot
    // bind the DA, and the picto's hover shows the previously selected item's
    // text while the passive effect (soft BattleBuffs ref) still works.
    {
        var itemsAsset = Open(itemsPath);
        int badClassPkg = 0, createdRefs = 0;
        foreach (var imp in itemsAsset.Imports)
        {
            if (imp.ClassName.ToString() != "BP_DataAsset_Item_Pictos_C") continue;
            createdRefs++;
            if (imp.ClassPackage.ToString() != "/Game/Gameplay/Inventory/BP_DataAsset_Item_Pictos")
                badClassPkg++;
        }
        Check(createdRefs > 0 && badClassPkg == 0,
            $"every Pictos_Data import uses the real class package (refs={createdRefs}, bad={badClassPkg})");
    }

    // Lucky Aim retune: every tier rolls 15% for the bonus shot (the vanilla
    // 30% const is retuned on the base BP and on both clones), costs are
    // 1 / 7 / 21, and the description text must agree with the const.
    static bool HasConst(UAssetAPI.UAsset a, string fn, double v)
    {
        foreach (var exp in a.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>())
        {
            if (exp.ObjectName.Value.Value != fn) continue;
            foreach (var bc in exp.ScriptBytecode)
                if (HasConstRec(bc, v)) return true;
        }
        return false;
    }
    static bool HasConstRec(UAssetAPI.Kismet.Bytecode.KismetExpression e, double v)
    {
        // Mirrors PictosMagnitudeKit.PatchConstRec: children hang off both
        // properties and fields, and call arguments sit in array fields.
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst dc && Math.Abs(dc.Value - v) < 1e-6)
            return true;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_FloatConst fc && Math.Abs(fc.Value - v) < 1e-6f)
            return true;
        var t = e.GetType();
        foreach (var p in t.GetProperties())
        {
            if (p.PropertyType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression)
                && p.GetValue(e) is UAssetAPI.Kismet.Bytecode.KismetExpression one
                && one != e && HasConstRec(one, v)) return true;
            if (p.PropertyType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression[])
                && p.GetValue(e) is UAssetAPI.Kismet.Bytecode.KismetExpression[] pa
                && pa.Any(c => c is not null && HasConstRec(c, v))) return true;
        }
        foreach (var f in t.GetFields())
        {
            if (f.FieldType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression)
                && f.GetValue(e) is UAssetAPI.Kismet.Bytecode.KismetExpression one
                && one != e && HasConstRec(one, v)) return true;
            if (f.FieldType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression[])
                && f.GetValue(e) is UAssetAPI.Kismet.Bytecode.KismetExpression[] fa
                && fa.Any(c => c is not null && HasConstRec(c, v))) return true;
        }
        return false;
    }

    foreach (var (id, cost) in new[] { ("LuckyAim", 1), ("LuckyAim2", 7), ("LuckyAim3", 21) })
    {
        var costOk = pe.TryGetRow(id, out var luckyRow)
            && (int?)pe.DumpRow(luckyRow)["LuminaCost"] == cost;
        Check(costOk, $"Lucky Aim cost {id} = {cost}");
    }
    foreach (var bpName in new[] { "BP_LU_APFreeAim_50Recover", "BP_LU_LuckyAim2", "BP_LU_LuckyAim3" })
    {
        var bpPath = PictosCreateKit.FindAsset(dir, bpName + ".uasset");
        if (bpPath is null) { Check(false, bpName + " staged"); continue; }
        var bpAsset = Open(bpPath);
        Check(HasConst(bpAsset, "ProcessAPChange", 15) && !HasConst(bpAsset, "ProcessAPChange", 30),
            bpName + " rolls 15% bonus shot (30% retuned)");
    }
    foreach (var table in new[] { "DT_PassiveEffects", "DT_jRPG_Items" })
    {
        var tf = PictosCreateKit.FindAsset(dir, table + ".uexp");
        var text = tf is null ? "" : System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(tf));
        Check(text.Contains("15% chance to recover 1 AP on Free Aim shot."),
            table + " Lucky Aim description says 15%");
        Check(!text.Contains("30% chance to recover"), table + " has no stale 30% Lucky Aim text");
    }

    // ── Visages Mask Keeper: keep the full mask moveset after the trio ──
    // The boss decides which masks are alive from three persistent flags; the
    // kit re-points those reads at a flag of our own that nothing can set.
    {
        MaskKeeperKit.Apply(dir, true);
        var mkBp = PictosCreateKit.FindAsset(dir, MaskKeeperKit.BpStem + ".uasset");
        Check(mkBp is not null, "Mask Keeper boss BP staged");
        if (mkBp is not null)
        {
            var imports = string.Join("|", Open(mkBp).Imports.Select(i => i.ObjectName.Value.Value));
            Check(imports.Contains(MaskKeeperKit.FlagStem, StringComparison.Ordinal),
                "Mask Keeper reads the mod's never-set mask flag");
            foreach (var f in MaskKeeperKit.DefeatedFlags)
                Check(!imports.Contains(f, StringComparison.Ordinal), "Mask Keeper no longer reads " + f);
        }
        var mkFlag = PictosCreateKit.FindAsset(dir, MaskKeeperKit.FlagStem + ".uasset");
        Check(mkFlag is not null, "Mask Keeper flag asset staged");
        Check(mkFlag is not null
              && !MaskKeeperKit.CarriesVanillaGuid(Path.ChangeExtension(mkFlag, ".uexp")),
            "Mask Keeper flag has its own GUID (no save can hand it the defeated state)");

        // The Gameplay Tweaks toggle is a full opt-out: off must stage nothing.
        var offDir = Path.Combine(EditorPaths.RepoRoot, "work", "tmp", "maskkeeper-off");
        if (Directory.Exists(offDir)) Directory.Delete(offDir, true);
        Directory.CreateDirectory(offDir);
        MaskKeeperKit.Apply(offDir, false);
        Check(PictosCreateKit.FindAsset(offDir, MaskKeeperKit.BpStem + ".uasset") is null
              && PictosCreateKit.FindAsset(offDir, MaskKeeperKit.FlagStem + ".uasset") is null,
            "Mask Keeper tweak off stages nothing");

        // Regression: GameplayTweaksKit used to re-copy vanilla ST_PassiveEffects
        // when it wrote the Cheater line (consecutive-turns tweak), reverting every
        // string the picto/weapon kits had staged there. Run that tweak in the same
        // tree and assert the magnitude text survived — the string table is what the
        // picto tooltips read, so checking only the loc-key tables hides this.
        GameplayTweaksKit.Apply(dir, new GameplayTweaksKit.Settings
        {
            MaxConsecutiveTurns = 3,
            DisableTutorials = false,
        });
        var stPath = PictosCreateKit.FindAsset(dir, "ST_PassiveEffects.uasset");
        var stRows = new Dictionary<string, string>(StringComparer.Ordinal);
        if (stPath is not null)
            foreach (var (k, v) in DataTableEditor.OpenStringTable(stPath)) stRows[k] = v;
        Check(stRows.TryGetValue("PASSIVE_FirstOffensive_Name", out var foName) && foName == "First Offensive I",
            "First Offensive I rename survives the gameplay tweaks (string table)");
        Check(stRows.TryGetValue("PASSIVE_FirstOffensive_Description", out var foDesc) && foDesc.Contains("10% more damage"),
            "First Offensive I description survives the gameplay tweaks");
        Check(stRows.TryGetValue("PASSIVE_FirstOffensive5_Name", out var fo5) && fo5 == "First Offensive V",
            "First Offensive V row present in the string table");
        Check(stRows.TryGetValue("PASSIVE_FirstOffensive5_Description", out var fo5Desc)
              && fo5Desc.Contains("200% more damage"),
            "First Offensive V description survives the gameplay tweaks (string table)");
        // Serialization guard: Dump()/DumpRow() must render the inline FText,
        // not "null" (ToJson had no TextPropertyData case; ToString() returns
        // "null" for HistoryType.None, the form every created row uses).
        Check(pe.TryGetRow("FirstOffensive5", out var fo5Row)
              && pe.DumpRow(fo5Row)["Description"]?.ToString()
                  == "First hit dealt and taken deals 200% more damage.",
            "First Offensive V description renders through DumpRow (not \"null\")");
        Check(!stRows.TryGetValue("PASSIVE_LuckyAim_Description", out var la) || !la.Contains("30%"),
            "Lucky Aim string is not the stale 30% vanilla text");
        Check(stRows.TryGetValue("PASSIVE_Cheater_Description", out var ch) && ch.Contains("three times"),
            "gameplay tweak still writes its own Cheater string");
    }

    // Death Bomb I–V ladder. Tier I is the vanilla picto (row + BP patched in
    // place); II–V are clones of it with their own balancing row. The damage
    // element is deliberately left as the donor's runtime read — forcing a
    // literal CTDs on battle load, so these bombs deal the wielder's element.
    {
        BombPictosKit.Apply(dir);
        var peBomb = new DataTableEditor(PictosCreateKit.FindAsset(dir, "DT_PassiveEffects.uasset")!);
        var defBomb = new DataTableEditor(PictosCreateKit.FindAsset(dir, "DT_PictosDefinitions.uasset")!);
        var balBomb = new DataTableEditor(PictosCreateKit.FindAsset(dir, "DT_Balancing_Luminas.uasset")!);
        var bombCatalog = new E33ModEditor.Core.Loot.LootCatalog();
        var ladder = new (string Id, float Mult, int Lumina, bool Clone)[]
        {
            ("DeathBombPhysical", 0.5f, 1, false), ("DeathBomb2", 1f, 5, true),
            ("DeathBomb3", 2f, 10, true), ("DeathBomb4", 5f, 25, true), ("DeathBomb5", 10f, 50, true),
        };
        foreach (var (bombId, mult, lumina, isClone) in ladder)
        {
            Check(peBomb.HasRow(bombId), $"{bombId} passive row staged");
            if (peBomb.TryGetRow(bombId, out var pRow))
                Check(peBomb.DumpRow(pRow)["LuminaCost"]?.ToString() == lumina.ToString(),
                    $"{bombId} Lumina cost {lumina}");
            if (defBomb.TryGetRow(bombId, out var dRow))
            {
                var multipliers = defBomb.DumpRow(dRow)["BaseStatMultipliers"] as Newtonsoft.Json.Linq.JObject;
                Check(multipliers is not null
                      && multipliers.Properties().Any(p => Convert.ToDouble(p.Value.ToString()) > 0),
                    $"{bombId} definition has real multipliers");
            }
            if (peBomb.TryGetRow(bombId, out var buffRow))
            {
                var buffs = peBomb.DumpRow(buffRow)["BattleBuffs"]?.ToString() ?? "";
                Check(isClone
                        ? buffs.Contains("/BP_LU_" + bombId + ",")
                        : buffs.Contains("DeathBomb/BP_LU_DeathBomb,") && !buffs.Contains("BP_LU_DeathBombPhysical"),
                    isClone
                        ? $"{bombId} passive points at its own blueprint"
                        : $"{bombId} passive keeps the vanilla DeathBomb blueprint (a missing BP freezes battle load)");
            }
            var key = bombId + "_FinalDamageMultiplier";
            Check(balBomb.HasRow(key), $"{bombId} balancing row staged");
            if (balBomb.TryGetRow(key, out var bRow))
                Check(Math.Abs(Convert.ToDouble(balBomb.DumpRow(bRow)["FloatValue"].ToString()) - mult) < 0.001,
                    $"{bombId} damage multiplier {mult}");
            if (isClone)
            {
                var bombBp = PictosCreateKit.FindAsset(dir, "BP_LU_" + bombId + ".uasset");
                Check(bombBp is not null, $"{bombId} BP staged");
                Check(bombBp is not null && BombPictosKit.ElementByteOf(bombBp) == -1,
                    $"{bombId} element argument left dynamic (forced literal CTDs on battle load)");
                Check(PictosCreateKit.FindAsset(dir, "DA_GearPictos_" + bombId + ".uasset") is not null,
                    $"{bombId} DA staged");
                // Identity: DA lives in Gear/ (donor location), not NewLumina/;
                // wrong package + donor ItemID is what hovered DeathBomb5 as
                // Combo Attack II + TheOne on LU_EchassierAlpha.
                var itemBombPath = PictosCreateKit.FindAsset(dir, "DT_jRPG_Items.uasset")!;
                var itemBombAsset = Open(itemBombPath);
                var wrongPkg = "/Game/Gameplay/Pictos/Gear/NewLumina/DA_GearPictos_" + bombId;
                var rightPkg = "/Game/Gameplay/Pictos/Gear/DA_GearPictos_" + bombId;
                Check(!itemBombAsset.Imports.Any(i => i.ObjectName.ToString() == wrongPkg),
                    $"{bombId} item has no stale NewLumina DA package");
                Check(itemBombAsset.Imports.Any(i => i.ObjectName.ToString() == rightPkg),
                    $"{bombId} item points at Gear/ DA package");
                if (defBomb.TryGetRow(bombId, out var idRow))
                {
                    var iid = idRow.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "ItemID")
                        as UAssetAPI.PropertyTypes.Objects.NamePropertyData;
                    Check(iid?.Value.ToString() == bombId,
                        $"{bombId} definition ItemID is itself ({iid?.Value})");
                }
                else Check(false, $"{bombId} definition row present for ItemID");
            }
            Check(!bombCatalog.BrokenItems.Contains(bombId), $"{bombId} not broken-listed");
        }
    }
    // Revive-triggered enemy debuffs: clone of the enemy-affecting passive with its
    // trigger retargeted to the revive dispatcher (OnResurected on the same owner
    // class) and its status import swapped to the target buff.
    {
        ReviveDebuffKit.Apply(dir);
        var peRev = new DataTableEditor(PictosCreateKit.FindAsset(dir, "DT_PassiveEffects.uasset")!);
        var itemRev = new DataTableEditor(PictosCreateKit.FindAsset(dir, "DT_jRPG_Items.uasset")!);
        var defRev = new DataTableEditor(PictosCreateKit.FindAsset(dir, "DT_PictosDefinitions.uasset")!);
        foreach (var (reviveId, _) in ReviveDebuffKit.CreatedPictos)
        {
            Check(peRev.HasRow(reviveId), $"{reviveId} passive row staged");
            if (peRev.TryGetRow(reviveId, out var rRow))
                Check(peRev.DumpRow(rRow)["LuminaCost"]?.ToString() == "5", $"{reviveId} Lumina cost 5");
            var revBp = PictosCreateKit.FindAsset(dir, "BP_LU_" + reviveId + ".uasset");
            Check(revBp is not null, $"{reviveId} BP staged");
            Check(revBp is not null && ReviveDebuffKit.HasReviveTrigger(revBp),
                $"{reviveId} binds the revive dispatcher, not the death one");
            var revDa = PictosCreateKit.FindAsset(dir, "DA_GearPictos_" + reviveId + ".uasset");
            Check(revDa is not null, $"{reviveId} DA staged");
            // Item identity: the clone must not resolve as its BurningDeath donor
            // (that is what made the drop show up in-game as Burning Death I).
            if (itemRev.TryGetRow(reviveId, out var iRow))
            {
                var idump = itemRev.DumpRow(iRow);
                Check(idump["Item_HardcodedName"]?.ToString() == reviveId,
                    $"{reviveId} item hardcoded name is itself");
                Check(idump["Pictos_Data"]?.ToString()?.Contains("DA_GearPictos_" + reviveId) == true,
                    $"{reviveId} item points at its own gear data");
            }
            else Check(false, $"{reviveId} item row staged");
            if (defRev.TryGetRow(reviveId, out var dRow))
                Check(defRev.DumpRow(dRow)["ItemID"]?.ToString() == reviveId,
                    $"{reviveId} definition ItemID is itself");
        }
    }
    Check(FloatRow(bal, "BestDefense_DamageMultiplier") == "1.1", "BestDefense I multiplier 1.1");
    Check(FloatRow(bal, "EnergyDeath_GainApAmount") == "1", "EnergyDeath I AP = 1");
    for (int rdTier = 1; rdTier <= 5; rdTier++)
    {
        var rd = rdTier == 1 ? "RandomDefense" : "RandomDefense" + rdTier;
        Check(FloatRow(bal, rd + "_LowMultiplier") == "0.5", $"{rd} low 0.5");
        Check(FloatRow(bal, rd + "_HighMultiplier") == "1.5", $"{rd} high 1.5");
    }
    Check(FloatRow(bal, "BreakDamageOnCrit_BreakDamageIncrease") == "1.1", "Critical Break I 1.1");
    Check(FloatRow(bal, "BurningDeath5_BurnStackAmount") == "50", "BurningDeath5 stacks 50");
    Check(FloatRow(bal, "Infinite_NewMaxHpValue") is "3333333" or "3.333333E+06",
        "Infinite max HP 3333333 (" + FloatRow(bal, "Infinite_NewMaxHpValue") + ")");
    Check(FloatRow(bal, "DeathsDividend_DamageIncrease") == "0.1", "DeathsDividend 0.1 per revive");
    Check(pe.HasRow("CriticalBreak"), "Critical Stun row CriticalBreak untouched");
    int Cost(string id)
    {
        if (!pe.TryGetRow(id, out var r)) return -1;
        var c = r.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "LuminaCost")
            as UAssetAPI.PropertyTypes.Objects.IntPropertyData;
        return c?.Value ?? -1;
    }
    Check(Cost("DoubleAP") == 40 && Cost("DoubleAP2") == 60, "Energy Master costs 40/60");
    Check(Cost("Infinite") == 10 && Cost("DeathsDividend") == 15, "Infinite 10 / Death's Dividend 15");
    var cc2 = PictosCreateKit.FindAsset(dir, "BP_LU_GradientOnCrit2.uasset");
    if (cc2 != null)
    {
        var a = Open(cc2);
        bool sawLatchTrue = false, sawLatchFalse = false;
        foreach (var fn in a.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>())
        {
            if (fn.ObjectName.ToString() != "ProcessDealtRawDamages" || fn.ScriptBytecode is null) continue;
            void Walk(UAssetAPI.Kismet.Bytecode.KismetExpression e)
            {
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LetBase let
                    && let.VariableExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_InstanceVariable iv)
                {
                    var path = iv.Variable?.New?.Path;
                    var name = path is { Length: > 0 } ? string.Join(".", path.Select(n => n.ToString())) : "";
                    if (name.Contains("CanGainGradientCharges", StringComparison.Ordinal))
                    {
                        if (let.AssignmentExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_True) sawLatchTrue = true;
                        if (let.AssignmentExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_False) sawLatchFalse = true;
                    }
                }
                foreach (var pr in e.GetType().GetProperties())
                {
                    if (pr.PropertyType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression)
                        && pr.GetValue(e) is UAssetAPI.Kismet.Bytecode.KismetExpression child && child != e)
                        Walk(child);
                    else if (pr.PropertyType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression[])
                             && pr.GetValue(e) is UAssetAPI.Kismet.Bytecode.KismetExpression[] arr)
                        foreach (var c in arr) if (c != null) Walk(c);
                }
            }
            foreach (var bc in fn.ScriptBytecode) Walk(bc);
        }
        Check(sawLatchFalse && !sawLatchTrue, "Charging Critical II CanGainGradientCharges latches until turn start");
    }

    // Re-pin now that every exclusive picto asset (including Super Sonic) is
    // staged — mirrors FullPackKit, which pins after all picto kits.
    PictosMagnitudeKit.PinExclusiveDrops(dir);
    var enePath = PictosCreateKit.FindAsset(dir, "DT_jRPG_Enemies.uasset");
    Check(enePath != null, "Infinite pin copied DT_jRPG_Enemies");
    if (enePath != null)
    {
        var ene = new DataTableEditor(enePath);
        Check(ene.TryGetRow("L_Boss_Curator", out var curator), "Curator row present");
        if (ene.TryGetRow("L_Boss_Curator", out curator))
        {
            var loot = curator.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "PossibleLoot")
                as UAssetAPI.PropertyTypes.Objects.ArrayPropertyData;
            var ids = new List<string>();
            if (loot != null)
                foreach (var ent in loot.Value.OfType<UAssetAPI.PropertyTypes.Structs.StructPropertyData>())
                {
                    var item = ent.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "Item")
                        as UAssetAPI.PropertyTypes.Structs.StructPropertyData;
                    var nm = item?.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "RowName")
                        as UAssetAPI.PropertyTypes.Objects.NamePropertyData;
                    if (nm?.Value != null) ids.Add(nm.Value.ToString());
                }
            Check(ids.Contains("Infinite"), "Curator PossibleLoot includes Infinite (" + string.Join(",", ids) + ")");
            var mult = curator.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "LootChanceMultiplier");
            var mv = mult is UAssetAPI.PropertyTypes.Objects.DoublePropertyData dd ? dd.Value
                : mult is UAssetAPI.PropertyTypes.Objects.FloatPropertyData ff ? ff.Value : -1;
            Check(mv >= 1, "Curator LootChanceMultiplier >= 1 (" + mv + ")");
        }
        List<string> LootIds(UAssetAPI.PropertyTypes.Structs.StructPropertyData row)
        {
            var list = new List<string>();
            if (row.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "PossibleLoot")
                is not UAssetAPI.PropertyTypes.Objects.ArrayPropertyData loot) return list;
            foreach (var ent in loot.Value.OfType<UAssetAPI.PropertyTypes.Structs.StructPropertyData>())
            {
                var item = ent.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "Item")
                    as UAssetAPI.PropertyTypes.Structs.StructPropertyData;
                var nm = item?.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "RowName")
                    as UAssetAPI.PropertyTypes.Objects.NamePropertyData;
                if (nm?.Value != null) list.Add(nm.Value.ToString());
            }
            return list;
        }
        if (ene.TryGetRow("MM_MirrorRenoir", out var renoir))
            Check(LootIds(renoir).Contains("OverPowered"), "Monolith Renoir drops Ultima Power");
        if (ene.TryGetRow("MO_Boss_Paintress", out var paintress))
            Check(!LootIds(paintress).Contains("OverPowered"), "Paintress no longer drops Ultima Power");
        if (ene.TryGetRow(SpecialPictosKit.SuperSonicEnemy, out var echassier))
            Check(LootIds(echassier).Contains(SpecialPictosKit.SuperSonicId),
                "Chromatic Echassier drops Super Sonic");
        else
            Check(false, "Chromatic Echassier row present");
        if (ene.TryGetRow(SpecialPictosKit.RuptureEnemy, out var creation))
            Check(LootIds(creation).Contains(SpecialPictosKit.RuptureId),
                "Création drops Rupture Shot");
        else
            Check(false, "Création row present");
        int other = 0;
        foreach (var row in ene.Table.Table.Data)
        {
            var id = DataTableEditor.CleanName(row.Name);
            foreach (var drop in LootIds(row))
            {
                if (string.Equals(drop, "Infinite", StringComparison.Ordinal) && id != "L_Boss_Curator") other++;
                if (string.Equals(drop, "OverPowered", StringComparison.Ordinal) && id != "MM_MirrorRenoir") other++;
                if (string.Equals(drop, SpecialPictosKit.SuperSonicId, StringComparison.Ordinal)
                    && id != SpecialPictosKit.SuperSonicEnemy) other++;
                if (string.Equals(drop, SpecialPictosKit.RuptureId, StringComparison.Ordinal)
                    && id != SpecialPictosKit.RuptureEnemy) other++;
            }
        }
        Check(other == 0, "exclusive pictos not on any other enemy (" + other + ")");
    }

    // ── revived cut-content pictos: non-zero stat rows + written text ──
    CutPictosKit.Apply(dir);
    {
        var cutDefDump = Newtonsoft.Json.Linq.JObject.Parse(
            new DataTableEditor(PictosCreateKit.FindAsset(dir, "DT_PictosDefinitions.uasset")!).Dump());
        var cutItemsPath = PictosCreateKit.FindAsset(dir, "DT_jRPG_Items.uasset")!;
        var cutItemText = Text(cutItemsPath) + Text(Path.ChangeExtension(cutItemsPath, ".uexp"));
        var cutPePath = PictosCreateKit.FindAsset(dir, "DT_PassiveEffects.uasset")!;
        var cutPeText = Text(cutPePath) + Text(Path.ChangeExtension(cutPePath, ".uexp"));
        foreach (var s in CutPictosKit.Specs)
        {
            var mults = cutDefDump[s.Id]?["BaseStatMultipliers"] as Newtonsoft.Json.Linq.JObject;
            Check(mults is not null && mults.Properties().Any(p => p.Value.ToObject<double>() > 0),
                $"{s.Id} has non-zero stat multipliers");
            Check(cutItemText.Contains(s.DisplayName, StringComparison.Ordinal)
                  && cutItemText.Contains(s.Description, StringComparison.Ordinal),
                $"{s.Id} item name + description written");
            Check(cutPeText.Contains(s.Description, StringComparison.Ordinal),
                $"{s.Id} passive description written");
        }
    }

    Console.WriteLine(fails.Count == 0 ? "pictos-smoke PASS" : $"pictos-smoke {fails.Count} FAILURES");
    return fails.Count == 0 ? 0 : 1;

    void CheckPassiveCost(DataTableEditor table, string id, int expected)
    {
        if (!table.TryGetRow(id, out var row))
        {
            Check(false, id + " passive row present");
            return;
        }
        var lc = row.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "LuminaCost")
            as UAssetAPI.PropertyTypes.Objects.IntPropertyData;
        Check(lc?.Value == expected, $"{id} lumina cost {expected}");
    }

    static string? FloatRow(DataTableEditor ed, string row)
    {
        try
        {
            if (!ed.TryGetRow(row, out var r)) return null;
            return ed.FlattenRow(r).FirstOrDefault(l => l.Path == "FloatValue")?.Value?.ToString();
        }
        catch { return "<parse error>"; }
    }
}

/// <summary>Lists functions containing an EX_SetArray of N integer constants.
/// Usage: find-setarray &lt;uasset&gt; &lt;n&gt;</summary>
static int CmdFindSetArray(string[] args)
{
    var path = Path.GetFullPath(args[1]);
    int want = int.Parse(args[2]);
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAsset(path, EngineVersion.VER_UE5_4, map)
        : new UAsset(path, EngineVersion.VER_UE5_4);
    foreach (var fn in u.Exports.OfType<FunctionExport>())
    {
        if (fn.ScriptBytecode is null) continue;
        int n = 0;
        MayhemFixKit.Walk(fn.ScriptBytecode, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_SetArray sa
                && sa.Elements is { Length: > 0 }
                && sa.Elements.All(x => x is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst)
                && sa.Elements.Length == want)
                n++;
        });
        if (n > 0) Console.WriteLine($"{fn.ObjectName} x{n}");
    }
    return 0;
}

/// <summary>Lists functions that reference a named variable/field.
/// Usage: find-var &lt;uasset&gt; &lt;name&gt;</summary>
static int CmdFindVar(string[] args)
{
    var path = Path.GetFullPath(args[1]);
    var needle = args[2];
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAsset(path, EngineVersion.VER_UE5_4, map)
        : new UAsset(path, EngineVersion.VER_UE5_4);
    foreach (var fn in u.Exports.OfType<FunctionExport>())
    {
        if (fn.ScriptBytecode is null) continue;
        int n = 0;
        MayhemFixKit.Walk(fn.ScriptBytecode, e =>
        {
            switch (e)
            {
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_InstanceVariable iv
                    when PartyFiveKit.FieldName(iv.Variable) == needle: n++; break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable lv
                    when PartyFiveKit.FieldName(lv.Variable) == needle: n++; break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalOutVariable lov
                    when PartyFiveKit.FieldName(lov.Variable) == needle: n++; break;
            }
        });
        if (n > 0) Console.WriteLine($"{fn.ObjectName} x{n}");
    }
    return 0;
}

/// <summary>Finds every function containing a literal int/byte constant.
/// Usage: find-const &lt;uasset&gt; &lt;value&gt;</summary>
static int CmdFindConst(string[] args)
{
    var path = Path.GetFullPath(args[1]);
    bool isFloat = args[2].Contains('.');
    double fval = isFloat ? double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 0;
    int value = isFloat ? 0 : int.Parse(args[2]);
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAsset(path, EngineVersion.VER_UE5_4, map)
        : new UAsset(path, EngineVersion.VER_UE5_4);
    foreach (var fn in u.Exports.OfType<FunctionExport>())
    {
        if (fn.ScriptBytecode is null) continue;
        int n = 0;
        MayhemFixKit.Walk(fn.ScriptBytecode, e =>
        {
            switch (e)
            {
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst c when !isFloat && c.Value == value: n++; break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConstByte c2 when !isFloat && c2.Value == value: n++; break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_ByteConst c3 when !isFloat && c3.Value == value: n++; break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_FloatConst fc when isFloat && Math.Abs(fc.Value - fval) < 0.001: n++; break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst dc when isFloat && Math.Abs(dc.Value - fval) < 0.001: n++; break;
            }
        });
        if (n > 0) Console.WriteLine($"{fn.ObjectName} x{n}");
    }
    return 0;
}

/// <summary>Lists every EX_LocalFinalFunction ubergraph stub in a Blueprint:
/// owning function, StackNode, and the IntConst entry offset it passes.
/// Usage: uberjumps &lt;uasset&gt;</summary>
static int CmdUberJumps(string[] args)
{
    var path = Path.GetFullPath(args[1]);
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAsset(path, EngineVersion.VER_UE5_4, map)
        : new UAsset(path, EngineVersion.VER_UE5_4);
    string NM(FPackageIndex idx)
    {
        if (idx is null || idx.IsNull()) return "-";
        if (idx.IsExport()) return u.Exports[idx.Index - 1].ObjectName.ToString();
        return idx.IsImport() && Math.Abs(idx.Index) - 1 < u.Imports.Count
            ? "imp:" + u.Imports[Math.Abs(idx.Index) - 1].ObjectName.ToString() : "?";
    }
    foreach (var fn in u.Exports.OfType<FunctionExport>())
    {
        if (fn.ScriptBytecode is null) continue;
        MayhemFixKit.Walk(fn.ScriptBytecode, e =>
        {
            if (e is not UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalFinalFunction lf) return;
            var arg = lf.Parameters?.Length == 1 ? lf.Parameters[0] as UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst : null;
            Console.WriteLine($"{fn.ObjectName,-55} StackNode={NM(lf.StackNode),-45} off={arg?.Value.ToString() ?? "?"}");
        });
    }
    return 0;
}

/// <summary>
/// Audits every Blueprint under a directory for custom-event entry points that
/// no longer land on a statement start / inside the UberGraph. This is the
/// failure mode that crashed Lune's Meteor after the SkyBreak statement shrank:
/// OnExecuteSkill still called ExecuteUbergraph(601) while the graph ended at 557.
/// Usage: entry-audit [dir] (default work/mod)
/// </summary>
static int CmdEntryAudit(string[] args)
{
    var root = args.Length > 1 ? Path.GetFullPath(args[1])
        : Path.Combine(EditorPaths.RepoRoot, "work", "mod");
    if (!Directory.Exists(root)) { Console.Error.WriteLine("entry-audit: missing " + root); return 2; }
    var map = DataTableEditor.Mappings;
    int assets = 0, entries = 0, bad = 0;

    foreach (var path in Directory.EnumerateFiles(root, "*.uasset", SearchOption.AllDirectories))
    {
        UAsset u;
        try
        {
            u = map != null ? new UAsset(path, EngineVersion.VER_UE5_4, map)
                            : new UAsset(path, EngineVersion.VER_UE5_4);
        }
        catch { continue; }
        assets++;

        string StackName(FPackageIndex idx)
        {
            if (idx is null || idx.IsNull()) return "-";
            if (idx.IsImport() && Math.Abs(idx.Index) - 1 < u.Imports.Count)
                return u.Imports[Math.Abs(idx.Index) - 1].ObjectName.ToString();
            if (idx.IsExport()) return u.Exports[idx.Index - 1].ObjectName.ToString();
            return "?";
        }

        var ubers = u.Exports.OfType<FunctionExport>()
            .Where(f => f.ScriptBytecode is not null
                && f.ObjectName.ToString().StartsWith("ExecuteUbergraph", StringComparison.Ordinal))
            .ToList();
        foreach (var uber in ubers)
        {
            var stmts = uber.ScriptBytecode!;
            var starts = new HashSet<uint>(PartyFiveKit.StatementStarts(u, stmts));
            uint total = 0;
            foreach (var s in stmts) total += (uint)s.GetSize(u);
            var uberName = uber.ObjectName.ToString();

            foreach (var fn in u.Exports.OfType<FunctionExport>())
            {
                if (ReferenceEquals(fn, uber) || fn.ScriptBytecode is null) continue;
                MayhemFixKit.Walk(fn.ScriptBytecode, e =>
                {
                    if (e is not UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalFinalFunction lf) return;
                    if (StackName(lf.StackNode) != uberName) return;
                    if (lf.Parameters is not { Length: 1 }
                        || lf.Parameters[0] is not UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ic) return;
                    entries++;
                    bool inside = ic.Value >= 0 && (uint)ic.Value < total;
                    bool onStart = inside && starts.Contains((uint)ic.Value);
                    if (onStart) return;
                    bad++;
                    Console.WriteLine(
                        $"BAD {Path.GetRelativePath(root, path)} :: {fn.ObjectName} -> {uberName} " +
                        $"off={ic.Value} total={total} inside={inside} onStart={onStart}");
                });
            }
        }
    }

    Console.WriteLine($"entry-audit: {assets} asset(s), {entries} entry point(s), {bad} bad");
    return bad == 0 ? 0 : 1;
}

/// <summary>Recursively scans a directory of .uasset files and prints the ones
/// that define a FunctionExport with the given name. Usage:
/// find-fn &lt;dir&gt; &lt;fnName&gt;</summary>
static int CmdFindFn(string[] args)
{
    var dir = Path.GetFullPath(args[1]);
    var needle = args[2];
    foreach (var path in Directory.EnumerateFiles(dir, "*.uasset", SearchOption.AllDirectories))
    {
        try
        {
            var map = DataTableEditor.Mappings;
            var u = map != null
                ? new UAsset(path, EngineVersion.VER_UE5_4, map)
                : new UAsset(path, EngineVersion.VER_UE5_4);
            foreach (var fn in u.Exports.OfType<FunctionExport>())
            {
                if (fn.ObjectName.ToString().Contains(needle, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(Path.GetRelativePath(dir, path) + " :: " + fn.ObjectName);
                    break;
                }
            }
        }
        catch { /* unparseable asset: skip */ }
    }
    return 0;
}

/// <summary>Finds every statement containing a call to the named function and
/// prints the enclosing function + full expression trees. Usage:
/// usage &lt;uasset&gt; &lt;callName&gt;</summary>
static int CmdUsage(string[] args)
{
    var path = Path.GetFullPath(args[1]);
    var needle = args[2];
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAsset(path, EngineVersion.VER_UE5_4, map)
        : new UAsset(path, EngineVersion.VER_UE5_4);
    foreach (var fn in u.Exports.OfType<FunctionExport>())
    {
        if (fn.ScriptBytecode is null) continue;
        int i = 0;
        foreach (var s in fn.ScriptBytecode)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                switch (e)
                {
                    case UAssetAPI.Kismet.Bytecode.Expressions.EX_FinalFunction ff
                        when ff.StackNode is { } sn && sn.IsImport()
                             && sn.ToImport(u).ObjectName.ToString() == needle:
                    case UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm
                        when cm.StackNode is { } sn2 && sn2.IsImport()
                             && sn2.ToImport(u).ObjectName.ToString() == needle:
                    case UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction lvf
                        when lvf.VirtualFunctionName.ToString() == needle:
                    case UAssetAPI.Kismet.Bytecode.Expressions.EX_VirtualFunction vf
                        when vf.VirtualFunctionName.ToString() == needle:
                    case UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalFinalFunction lff
                        when lff.StackNode is { } sn3 && sn3.IsImport()
                             && sn3.ToImport(u).ObjectName.ToString() == needle:
                        hit = true;
                        break;
                }
            });
            if (hit)
            {
                Console.WriteLine($"== {fn.ObjectName} stmt#{i} ==");
                KismetTreePrint(u, s, 0, i);
            }
            i++;
        }
    }
    return 0;
}

/// <summary>Lists a Blueprint's function exports (name + bytecode size).
/// Usage: fn-list &lt;uasset&gt; [nameFilter]</summary>
static int CmdFnList(string[] args)
{
    var path = Path.GetFullPath(args[1]);
    var filter = args.Length > 2 ? args[2] : null;
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAsset(path, EngineVersion.VER_UE5_4, map)
        : new UAsset(path, EngineVersion.VER_UE5_4);
    int n = 0;
    foreach (var fn in u.Exports.OfType<FunctionExport>())
    {
        var name = fn.ObjectName.ToString();
        if (filter is not null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
        Console.WriteLine($"{name} bytes={fn.ScriptBytecode?.Length ?? 0}");
        n++;
    }
    Console.WriteLine($"-- {n} function(s)");
    return 0;
}

/// <summary>Lists every export: index, type, object name, class, outer.
/// Usage: exports &lt;uasset&gt; [nameFilter]</summary>
static int CmdExports(string[] args)
{
    var path = Path.GetFullPath(args[1]);
    var filter = args.Length > 2 ? args[2] : null;
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAsset(path, EngineVersion.VER_UE5_4, map)
        : new UAsset(path, EngineVersion.VER_UE5_4);
    for (int i = 0; i < u.Exports.Count; i++)
    {
        var ex = u.Exports[i];
        var name = ex.ObjectName.ToString();
        if (filter is not null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
        string ClassName(FPackageIndex idx)
        {
            if (idx is null || idx.IsNull()) return "-";
            if (idx.IsImport() && Math.Abs(idx.Index) - 1 < u.Imports.Count)
                return u.Imports[Math.Abs(idx.Index) - 1].ObjectName.ToString();
            return "export#" + idx.Index;
        }
        string OuterName(FPackageIndex idx)
        {
            if (idx is null || idx.IsNull()) return "-";
            if (idx.IsImport() && Math.Abs(idx.Index) - 1 < u.Imports.Count)
                return u.Imports[Math.Abs(idx.Index) - 1].ObjectName.ToString();
            return "export#" + idx.Index;
        }
        Console.WriteLine($"#{i} {ex.GetType().Name} {name} class={ClassName(ex.ClassIndex)} outer={OuterName(ex.OuterIndex)}");
    }
    return 0;
}

/// <summary>Dumps a UAsset name map (newline-separated, filtered).
/// Usage: names &lt;uasset&gt; [substring]</summary>
static int CmdNames(string[] args)
{
    var path = Path.GetFullPath(args[1]);
    var filter = args.Length > 2 ? args[2] : null;
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAsset(path, EngineVersion.VER_UE5_4, map)
        : new UAsset(path, EngineVersion.VER_UE5_4);
    foreach (var n in u.GetNameMapIndexList())
    {
        var s = n.ToString();
        if (filter is not null && !s.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
        Console.WriteLine(s);
    }
    return 0;
}

static int CmdDumpKismet(string[] args)
{
    var path = Path.GetFullPath(args[1]);
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
        : new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    var filter = args.Length > 2 ? args[2] : null;
    foreach (var fn in u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>())
    {
        if (fn.ScriptBytecode is null) continue;
        var name = fn.ObjectName.ToString();
        if (filter != null && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
            continue;
        Console.WriteLine($"-- {fn.ObjectName} --");
        Walk(fn.ScriptBytecode, e =>
        {
            switch (e)
            {
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst c:
                    Console.WriteLine($"  IntConst {c.Value}"); break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConstByte b:
                    Console.WriteLine($"  IntConstByte {b.Value}"); break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_ByteConst by:
                    Console.WriteLine($"  ByteConst {by.Value}"); break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_FloatConst f:
                    Console.WriteLine($"  FloatConst {f.Value}"); break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst dc:
                    Console.WriteLine($"  DoubleConst {dc.Value}"); break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_NameConst n:
                    Console.WriteLine($"  NameConst {n.Value}"); break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_TextConst t:
                {
                    var st = t.Value;
                    string body = st.TextLiteralType switch
                    {
                        UAssetAPI.Kismet.Bytecode.EBlueprintTextLiteralType.InvariantText =>
                            (st.InvariantLiteralString as UAssetAPI.Kismet.Bytecode.Expressions.EX_StringConst)?.Value ?? "?",
                        UAssetAPI.Kismet.Bytecode.EBlueprintTextLiteralType.LiteralString =>
                            (st.LiteralString as UAssetAPI.Kismet.Bytecode.Expressions.EX_StringConst)?.Value ?? "?",
                        _ => "",
                    };
                    Console.WriteLine($"  TextConst [{st.TextLiteralType}] \"{body}\""); break;
                }
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable lv:
                    Console.WriteLine($"  LocalVar {FmtPtr(lv.Variable)}"); break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_InstanceVariable iv:
                    Console.WriteLine($"  InstVar {FmtPtr(iv.Variable)}"); break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalOutVariable ov:
                    Console.WriteLine($"  OutVar {FmtPtr(ov.Variable)}"); break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_Context ctx:
                    Console.WriteLine($"  Context {ctx.ObjectExpression?.GetType().Name}"); break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_VirtualFunction vf:
                    Console.WriteLine($"  VirtFn {vf.VirtualFunctionName}"); break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm:
                    Console.WriteLine($"  CallMath {cm.StackNode}"); break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_FinalFunction ff:
                    Console.WriteLine($"  FinalFn {ff.StackNode}"); break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_ObjectConst oc:
                    Console.WriteLine($"  ObjectConst {FmtIdx(u, oc.Value)}"); break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_Let l:
                    Console.WriteLine($"  Let"); break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_LetValueOnPersistentFrame lp:
                    Console.WriteLine($"  LetPersist {lp.DestinationProperty}"); break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_SwitchValue sw:
                    Console.WriteLine($"  SwitchValue cases={sw.Cases?.Length ?? 0} index={sw.IndexTerm?.GetType().Name}");
                    if (sw.IndexTerm is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable lv2)
                        Console.WriteLine($"    index LocalVar {FmtPtr(lv2.Variable)}");
                    if (sw.IndexTerm is UAssetAPI.Kismet.Bytecode.Expressions.EX_InstanceVariable iv2)
                        Console.WriteLine($"    index InstVar {FmtPtr(iv2.Variable)}");
                    foreach (var c in sw.Cases ?? Array.Empty<UAssetAPI.Kismet.Bytecode.Expressions.FKismetSwitchCase>())
                    {
                        var idx = c.CaseIndexValueTerm switch
                        {
                            UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ic => ic.Value.ToString(),
                            UAssetAPI.Kismet.Bytecode.Expressions.EX_ByteConst bc => "b" + bc.Value,
                            UAssetAPI.Kismet.Bytecode.Expressions.EX_True => "true",
                            UAssetAPI.Kismet.Bytecode.Expressions.EX_False => "false",
                            _ => c.CaseIndexValueTerm?.GetType().Name ?? "?",
                        };
                        var term = c.CaseTerm switch
                        {
                            UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable lv3 => "L:" + FmtPtr(lv3.Variable),
                            UAssetAPI.Kismet.Bytecode.Expressions.EX_ObjectConst oc2 => "O:" + FmtIdx(u, oc2.Value),
                            _ => c.CaseTerm?.GetType().Name ?? "?",
                        };
                        Console.WriteLine($"    case {idx} -> {term}");
                    }
                    Console.WriteLine($"    default {sw.DefaultTerm?.GetType().Name} end={sw.EndGotoOffset}");
                    break;
                default:
                    Console.WriteLine($"  {e.GetType().Name}");
                    break;
            }
        });
    }
    return 0;

    static void Walk(IEnumerable<UAssetAPI.Kismet.Bytecode.KismetExpression> exprs,
        Action<UAssetAPI.Kismet.Bytecode.KismetExpression> visit)
    {
        if (exprs is null) return;
        foreach (var e in exprs)
        {
            if (e is null) continue;
            visit(e);
            foreach (var f in e.GetType().GetFields())
            {
                var v = f.GetValue(e);
                if (v is UAssetAPI.Kismet.Bytecode.KismetExpression ke) Walk(new[] { ke }, visit);
                else if (v is UAssetAPI.Kismet.Bytecode.KismetExpression[] arr) Walk(arr, visit);
            }
            foreach (var p in e.GetType().GetProperties())
            {
                if (p.GetIndexParameters().Length > 0) continue;
                var v = p.GetValue(e);
                if (v is UAssetAPI.Kismet.Bytecode.KismetExpression ke) Walk(new[] { ke }, visit);
                else if (v is UAssetAPI.Kismet.Bytecode.KismetExpression[] arr) Walk(arr, visit);
            }
        }
    }
}

static string FmtIdx(UAssetAPI.UAsset u, UAssetAPI.UnrealTypes.FPackageIndex idx)
{
    if (idx is null || idx.IsNull()) return "null";
    try
    {
        if (idx.IsImport())
        {
            var imp = idx.ToImport(u);
            return (imp?.ObjectName?.ToString() ?? "?") + " @" + idx.Index;
        }
        if (idx.IsExport())
            return (idx.ToExport(u)?.ObjectName?.ToString() ?? "?") + " @" + idx.Index;
    }
    catch { /* ignore */ }
    return idx.Index.ToString();
}

static string FmtPtr(UAssetAPI.Kismet.Bytecode.KismetPropertyPointer? p)
{
    if (p?.New?.Path is { Length: > 0 } path)
        return string.Join(".", path.Select(n => n.ToString()));
    return p?.Old?.Index.ToString() ?? "?";
}

static string Fmt(UAssetAPI.PropertyTypes.Objects.PropertyData p) => p switch
{
    UAssetAPI.PropertyTypes.Objects.BytePropertyData b => $"{b.EnumValue}({b.Value})",
    UAssetAPI.PropertyTypes.Objects.EnumPropertyData e => e.Value?.ToString() ?? "",
    UAssetAPI.PropertyTypes.Objects.IntPropertyData i => i.Value.ToString(),
    UAssetAPI.PropertyTypes.Objects.FloatPropertyData f => f.Value.ToString(),
    UAssetAPI.PropertyTypes.Objects.NamePropertyData n => n.Value?.ToString() ?? "",
    _ => p.GetType().Name,
};

static int CmdMappings(string[] args)
{
    var mapPath = args.Length > 1 ? args[1] : EditorPaths.Mappings;
    var m = new UAssetAPI.Unversioned.Usmap(mapPath);
    Console.WriteLine($"mappings loaded: {mapPath}");
    var schemas = m.Schemas;
    Console.WriteLine($"total schemas: {schemas.Count}");
    var jrpg = schemas.Keys.Where(k => k.Contains("jRPG", StringComparison.OrdinalIgnoreCase)).ToList();
    Console.WriteLine($"jRPG schemas: {jrpg.Count}");
    foreach (var k in jrpg.Take(15)) Console.WriteLine("  " + k);
    Console.WriteLine(schemas.ContainsKey("S_jRPG_Encounter")
        ? "S_jRPG_Encounter PRESENT"
        : "S_jRPG_Encounter MISSING");
    return 0;
}

static int CmdEnums(string[] args)
{
    var pattern = args.Length > 1 ? args[1] : "";
    var map = E33ModEditor.Core.DataTableEditor.Mappings
        ?? throw new InvalidOperationException("no mappings configured");
    foreach (var (name, e) in map.EnumMap.OrderBy(k => k.Key,
                 StringComparer.OrdinalIgnoreCase))
    {
        if (pattern.Length > 0 && !name.Contains(pattern, StringComparison.OrdinalIgnoreCase)) continue;
        var vals = e.Values.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
        Console.WriteLine($"{name} [{vals.Count}]");
        for (int i = 0; i < vals.Count; i++) Console.WriteLine($"   {i}: {vals[i]}");
    }
    return 0;
}

static int CmdStringTable(string[] args)
{
    var filter = args.Length > 2 ? args[2] : "";
    foreach (var (k, v) in E33ModEditor.Core.DataTableEditor.OpenStringTable(Path.GetFullPath(args[1])))
        if (filter.Length == 0 ||
            k.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            v.Contains(filter, StringComparison.OrdinalIgnoreCase))
            Console.WriteLine($"{k}\t{v}");
    return 0;
}

static int CmdExtractSuspend(string[] args)
{
    // Extracts one or more assets into work/tables/<filter> while the game's
    // ~mods directory is moved aside (and always restored), mirroring the
    // donor-extraction flow. Used for assets that mod containers would shadow.
    using (E33ModEditor.Core.Packer.DonorExtract.SuspendMods())
    {
        foreach (var filter in args.Skip(1))
        {
            var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", filter.TrimEnd('_'));
            Directory.CreateDirectory(dir);
            Console.WriteLine("[extract-suspend] " + filter);
            new Retoc().ToLegacy(dir, filter);
        }
    }
    Console.WriteLine("[extract-suspend] done");
    return 0;
}

static int CmdFind(string[] args)
{
    using var index = new GameIndex();
    foreach (var hit in index.FindContainersFor(args[1]))
        Console.WriteLine(Path.Combine(EditorPaths.PaksDir, hit));
    return 0;
}

static int CmdFindAssets(string[] args)
{
    using var index = new GameIndex();
    var needle = args.Length > 1 ? args[1] : "";
    var matches = index.AllFiles
        .Where(k => k.Contains(needle, StringComparison.OrdinalIgnoreCase))
        .OrderBy(k => k)
        .ToList();
    foreach (var m in matches) Console.WriteLine(m);
    Console.WriteLine($"-- {matches.Count} assets");
    return 0;
}

static int CmdExtract(string[] args)
{
    string asset = args[1];
    string outDir = GetOption(args, "--out") ?? Path.Combine(Directory.GetCurrentDirectory(), "extracted");
    using var index = new GameIndex();
    var hits = index.FindContainersFor(asset);
    if (hits.Count == 0) { Console.Error.WriteLine($"no container contains '{asset}'"); return 2; }
    new Retoc().ToLegacy(outDir, Path.GetFileNameWithoutExtension(asset));
    var match = Directory.EnumerateFiles(outDir, $"{Path.GetFileNameWithoutExtension(asset)}*", SearchOption.AllDirectories).ToList();
    foreach (var f in match) Console.WriteLine(f);
    return match.Count > 0 ? 0 : 2;
}

static int CmdDump(string[] args)
{
    var editor = new DataTableEditor(Path.GetFullPath(args[1]));
    var json = editor.Dump();
    var outFile = GetOption(args, "--out");
    if (outFile is not null) { File.WriteAllText(outFile, json); Console.WriteLine($"wrote {outFile}"); }
    else Console.WriteLine(json);
    return 0;
}

static int CmdApply(string[] args)
{
    var editor = new DataTableEditor(Path.GetFullPath(args[1]));
    int changes = editor.Apply(File.ReadAllText(args[2]));
    editor.Save(Path.GetFullPath(args[1]));
    Console.WriteLine($"{changes} field(s) updated in {args[1]}");
    return 0;
}

static int CmdPack(string[] args)
{
    string dir = Path.GetFullPath(args[1]);
    string name = GetOption(args, "--name") ?? E33ModEditor.Core.Packer.FullPackKit.ModNameFor(CurrentSeed());
    foreach (var p in new Retoc().PackCombined(dir, name))
        Console.WriteLine(p);
    return 0;
}

static int CurrentSeed()
{
    try
    {
        var randoPath = Path.Combine(EditorPaths.RepoRoot, "work", "randomizer.json");
        if (File.Exists(randoPath))
        {
            var o = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(randoPath));
            var s = o.Value<int?>("Seed");
            if (s.HasValue) return s.Value;
        }
    }
    catch { /* fall through to default */ }
    return 333333;
}

// ───────────────────────── save editing ─────────────────────────

static int CmdSaveDump(string[] args)
{
    var file = GvasReader.ReadFile(Path.GetFullPath(args[1]));
    var json = SaveGameJson.ToJObject(file).ToString(Newtonsoft.Json.Formatting.Indented);
    var outFile = GetOption(args, "--out");
    if (outFile is not null) { File.WriteAllText(outFile, json); Console.WriteLine($"wrote {outFile}"); }
    else Console.WriteLine(json);
    return 0;
}

static int CmdSaveWrite(string[] args)
{
    var json = File.ReadAllText(Path.GetFullPath(args[1]));
    var file = SaveGameJson.FromJObject(Newtonsoft.Json.Linq.JObject.Parse(json));
    GvasWriter.WriteFile(Path.GetFullPath(args[2]), file);
    Console.WriteLine($"wrote {args[2]}");
    return 0;
}

static int CmdSaveRoundtrip(string[] args)
{
    var path = Path.GetFullPath(args[1]);
    var original = File.ReadAllBytes(path);
    var file = GvasReader.ReadFile(path);
    using var ms = new MemoryStream();
    new GvasWriter(ms).Write(file);
    var rewritten = ms.ToArray();

    if (GetOption(args, "--write") is { } outPath) File.WriteAllBytes(outPath, rewritten);
    if (!rewritten.AsSpan().SequenceEqual(original))
    {
        Console.Error.WriteLine($"round-trip MISMATCH: {original.Length} → {rewritten.Length} bytes");
        for (int i = 0; i < Math.Min(original.Length, rewritten.Length); i++)
            if (original[i] != rewritten[i]) { Console.Error.WriteLine($"first diff at offset {i}: {original[i]:X2} vs {rewritten[i]:X2}"); break; }
        return 1;
    }
    Console.WriteLine($"round-trip OK: {rewritten.Length} bytes, {file.Properties.Count} root properties");
    return 0;
}

static int CmdSavePictosAdd(string[] args)
{
    string? id = null;
    int level = 1;
    bool master = false, backup = true;
    for (int i = 2; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--id": id = args[++i]; break;
            case "--level": level = int.Parse(args[++i]); break;
            case "--master": master = true; break;
            case "--no-backup": backup = false; break;
        }
    }
    if (id is null) { Console.Error.WriteLine("--id is required"); return 2; }

    var knownIds = new HashSet<string>(NewLootCatalogWithExtras().Items.Keys, StringComparer.Ordinal);
    knownIds.UnionWith(BaseItemRowIds());
    if (!knownIds.Contains(id))
        Console.WriteLine($"warning: '{id}' is not in the current item tables or mod kits — the game may not resolve it (run loot-audit).");

    var path = Path.GetFullPath(args[1]);
    var file = GvasReader.ReadFile(path);

    SaveGameEditor.SetPictosFound(file, id, true);
    if (master) SaveGameEditor.SetPictosMastered(file, id, true);
    SaveGameEditor.SetPictosLevel(file, id, level);
    Console.WriteLine($"{id}: found{(master ? " + mastered" : "")} at level {level}");

    GvasWriter.WriteFile(path, file);
    Console.WriteLine("saved " + path);
    return 0;
}

static int CmdSavePictosSwap(string[] args)
{
    string? from = null, to = null;
    bool backup = true;
    for (int i = 2; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--from": from = args[++i]; break;
            case "--to": to = args[++i]; break;
            case "--no-backup": backup = false; break;
        }
    }
    if (from is null || to is null) { Console.Error.WriteLine("--from and --to are required"); return 2; }

    var knownIds = new HashSet<string>(NewLootCatalogWithExtras().Items.Keys, StringComparer.Ordinal);
    knownIds.UnionWith(BaseItemRowIds());
    if (!knownIds.Contains(to))
        Console.WriteLine($"warning: '{to}' is not in the current item tables or mod kits — the game may not resolve it (run loot-audit).");

    var path = Path.GetFullPath(args[1]);
    if (backup) File.Copy(path, path + ".bak", overwrite: true);
    var file = GvasReader.ReadFile(path);

    var swap = SaveGameEditor.SwapPictos(file, from, to);
    Console.WriteLine($"{swap.From} -> {swap.To}: level {swap.Level}, mastered={swap.Mastered}, equipped=[{string.Join(",", swap.EquippedBy)}]");

    GvasWriter.WriteFile(path, file);
    Console.WriteLine("saved " + path);
    return 0;
}

static int CmdSaveSummary(string[] args)
{
    var file = GvasReader.ReadFile(Path.GetFullPath(args[1]));
    var s = SaveScan.Summarize(file);
    Console.WriteLine($"story   {s.StoryPoint}");
    Console.WriteLine($"location {s.Location}");
    Console.WriteLine($"party   Lv {s.PartyLevel}");
    Console.WriteLine($"played  {s.TimePlayed}");
    return 0;
}

static int CmdSaveStory(string[] args)
{
    string? baseSave = null, outSave = null, point = null;
    var charLevel = 0;
    var weaponLevel = 66;
    var pictosLevel = 10;
    var gear = true;
    var master = true;

    for (var i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--list":
                foreach (var c in StoryPointKit.Chapters)
                    Console.WriteLine($"{c.Index,2}  {c.Key,-24} {c.Name,-26} {c.LevelAsset} @ {c.SpawnTag}");
                return 0;
            case "--point": point = args[++i]; break;
            case "--char-level": charLevel = int.Parse(args[++i]); break;
            case "--weapon-level": weaponLevel = int.Parse(args[++i]); break;
            case "--pictos-level": pictosLevel = int.Parse(args[++i]); break;
            case "--no-gear": gear = false; break;
            case "--no-master": master = false; break;
            default:
                if (args[i].StartsWith("--")) break;
                if (baseSave is null) baseSave = args[i];
                else if (outSave is null) outSave = args[i];
                break;
        }
    }

    if (baseSave is null || point is null)
    {
        Console.Error.WriteLine("usage: save-story <base.sav> [out.sav] --point <index|key> " +
                                "[--char-level N] [--weapon-level N] [--pictos-level N] " +
                                "[--no-gear] [--no-master] [--list]");
        return 2;
    }
    var chapter = StoryPointKit.Find(point);
    if (chapter is null)
    {
        Console.Error.WriteLine($"unknown story point '{point}' (use --list)");
        return 2;
    }

    var path = Path.GetFullPath(baseSave);
    var file = GvasReader.ReadFile(path);
    StoryPointKit.Apply(file, chapter, Console.WriteLine);
    if (charLevel > 0) StoryPointKit.SetCharacterLevels(file, charLevel, log: Console.WriteLine);
    if (gear) StoryPointKit.GrantAllGear(file, weaponLevel, pictosLevel, master, Console.WriteLine);

    var target = outSave is not null
        ? Path.GetFullPath(outSave)
        : Path.Combine(Path.GetDirectoryName(path) ?? ".",
            Path.GetFileNameWithoutExtension(path) + $"_{chapter.Key}.sav");
    GvasWriter.WriteFile(target, file);
    Console.WriteLine("wrote " + target);
    return 0;
}

static int CmdSaveStageRebuild(string[] args)
{
    bool write = false, backup = true, listSources = false, dialogue = false;
    int maxTier = 5;
    string? sav = null;
    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--write": write = true; break;
            case "--dry-run": write = false; break;
            case "--no-backup": backup = false; break;
            case "--list-sources": listSources = true; break;
            case "--dialogue": dialogue = true; break;
            case "--max-tier": maxTier = int.Parse(args[++i]); break;
            default: if (!args[i].StartsWith("--")) sav = args[i]; break;
        }
    }

    if (dialogue)
    {
        var md = Path.Combine(EditorPaths.RepoRoot, "work", "mod");
        var bps = Directory.EnumerateFiles(md, "BP_Dialog*.uasset", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(md, "BP_Dialogue*.uasset", SearchOption.AllDirectories))
            .Distinct(StringComparer.Ordinal);
        foreach (var p in bps)
        {
            Console.WriteLine(Path.GetFileNameWithoutExtension(p));
            foreach (var prop in new[] { "Reward", "RewardOnCompletion", "Rewards" })
                foreach (var id in E33ModEditor.Core.Loot.DialogueRewardKit.ReadRewardIds(p, prop))
                    Console.WriteLine($"   {prop}: {id}");
        }
        return 0;
    }

    var catalog = E33ModEditor.Core.SaveGame.SaveProgressionKit.CatalogWithChecks(EditorPaths.RepoRoot);

    if (listSources)
    {
        foreach (var c in catalog.Checks.OrderBy(c => c.Source).ThenBy(c => c.Area).ThenBy(c => c.Id))
        {
            var inScope = c.Source == "Enemy"
                ? E33ModEditor.Core.SaveGame.SaveProgressionKit.InScopeEnemy(c.Area, c.AreaLevel, c.Tier)
                : E33ModEditor.Core.SaveGame.SaveProgressionKit.InScopeChest(c.Area, c.AreaLevel);
            Console.WriteLine($"{(inScope ? "IN " : "out")} {c.Source,-7} {c.Id,-42} area={c.Area,-26} key={c.AreaKey,-26} T{c.Tier} L{c.AreaLevel}{(c.IsBoss ? " BOSS" : "")}");
        }
        return 0;
    }

    if (sav is null)
    {
        Console.Error.WriteLine("usage: save-stage-rebuild <save.sav> [--write] [--list-sources] [--no-backup]");
        return 2;
    }

    var modDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod");
    var plan = E33ModEditor.Core.SaveGame.SaveProgressionKit.Build(modDir, catalog, maxTier, m => Console.WriteLine(m));

    Console.WriteLine($"== allowed pictos ({plan.Pictos.Count}) ==");
    foreach (var p in plan.Pictos) Console.WriteLine($"  {p.Id,-34} T{p.Tier} L{p.Level,-2}  {p.Source}");
    Console.WriteLine($"== allowed weapons ({plan.Weapons.Count}) ==");
    foreach (var p in plan.Weapons) Console.WriteLine($"  {p.Id,-34} T{p.Tier} L{p.Level,-2}  {p.Source}");

    var path = Path.GetFullPath(sav);
    var file = GvasReader.ReadFile(path);
    var (badP, badW) = E33ModEditor.Core.SaveGame.SaveProgressionKit.Disallowed(file, catalog, plan);
    Console.WriteLine($"== would remove {badP.Count} pictos, {badW.Count} weapons ==");
    foreach (var id in badP) Console.WriteLine("  -p " + id);
    foreach (var id in badW) Console.WriteLine("  -w " + id);

    if (!write)
    {
        Console.WriteLine("[dry-run] nothing written — re-run with --write to apply.");
        return 0;
    }

    if (backup)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Copy(path, path + $".bak_{stamp}", overwrite: true);
        Console.WriteLine("backup: " + path + $".bak_{stamp}");
    }
    E33ModEditor.Core.SaveGame.SaveProgressionKit.Apply(file, catalog, plan, m => Console.WriteLine(m));
    GvasWriter.WriteFile(path, file);
    Console.WriteLine("wrote " + path);
    return 0;
}

/// <summary>
/// Guards the enum-extraction filter. Retoc's -f is an unanchored file-name
/// substring, so a short/generic enum name would dump tens of thousands of
/// assets into work/enums (that once cost 32 GB). Proves a real enum name
/// matches one asset and that a broad name would be refused.
/// </summary>
static int CmdEnumGuardSmoke(string[] args)
{
    var retoc = new Retoc();
    string[] names = { "E_jRPG_StatType", "EAttackType", "ECharacterAttribute", "ESkillType" };
    var bad = new List<string>();
    foreach (var n in names)
    {
        var c = retoc.CountMatchingAssets(n);
        Console.WriteLine($"  '{n}' -> {c} asset(s) (want 1, cap {DataTableEditor.MaxEnumExtractMatches})");
        if (c != 1) bad.Add(n);
    }
    var broad = retoc.CountMatchingAssets("E");
    Console.WriteLine($"  broad 'E' -> {broad} assets (must exceed cap {DataTableEditor.MaxEnumExtractMatches})");
    if (bad.Count > 0)
    {
        Console.Error.WriteLine("enum-guard-smoke FAILED: real enum name(s) did not match exactly one asset: "
            + string.Join(", ", bad));
        return 1;
    }
    if (broad <= DataTableEditor.MaxEnumExtractMatches)
    {
        Console.Error.WriteLine($"enum-guard-smoke FAILED: expected the broad filter to exceed the cap, got {broad}");
        return 1;
    }

    // End-to-end: the real entry point must refuse the broad name and write
    // NOTHING. Before the guard this dumped ~40,800 assets (32 GB) into work/enums.
    var enumDir = Path.Combine(EditorPaths.RepoRoot, "work", "enums");
    int Before() => Directory.Exists(enumDir) ? Directory.GetFiles(enumDir, "*", SearchOption.AllDirectories).Length : 0;
    var before = Before();
    var refused = DataTableEditor.EnumDisplayNames("E");
    var after = Before();
    Console.WriteLine($"  EnumDisplayNames(\"E\") -> {(refused is null ? "null (refused)" : "UNEXPECTED names")}; work/enums {before} -> {after} files");
    if (refused is not null)
    {
        Console.Error.WriteLine("enum-guard-smoke FAILED: a broad enum name was not refused");
        return 1;
    }
    if (after != before)
    {
        Console.Error.WriteLine($"enum-guard-smoke FAILED: broad enum name wrote {after - before} files into work/enums");
        return 1;
    }
    Console.WriteLine("enum-guard-smoke PASS");
    return 0;
}

static E33ModEditor.Core.Loot.LootCatalog NewLootCatalogWithExtras()
{
    var catalog = new E33ModEditor.Core.Loot.LootCatalog();
    catalog.RegisterExtraPictos(
        E33ModEditor.Core.PictosCreateKit.PainterConversions.Select(p => (p.Id, p.DisplayName))
            .Concat(E33ModEditor.Core.PictosCreateKit.Powers.Select(p => (p.Id, p.DisplayName)))
            .Concat(E33ModEditor.Core.PictosCreateKit.HitBurn.Select(p => (p.Id, p.DisplayName)))
            .Concat(E33ModEditor.Core.PictosMagnitudeKit.Variants.Select(p => (p.Id, p.DisplayName)))
            .Concat(BombPictosKit.CreatedPictos)
                .Concat(ReviveDebuffKit.CreatedPictos));
    return catalog;
}

/// <summary>Row ids of the base-game item dump (work/items.json), when present.</summary>
static HashSet<string> BaseItemRowIds()
{
    var set = new HashSet<string>(StringComparer.Ordinal);
    var path = Path.Combine(EditorPaths.RepoRoot, "work", "items.json");
    if (!File.Exists(path)) return set;
    try
    {
        var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
        foreach (var prop in root.Properties()) set.Add(prop.Name);
    }
    catch { /* treat unreadable dump as absent */ }
    return set;
}

/// <summary>
/// Verifies every id in the tier lists resolves to a base-game row or a
/// current mod id. Dead ids (retired/never-created content) would otherwise
/// ship silently as broken chest/shop/drop entries once dragged into T1-T5.
/// </summary>
static int CmdLootAudit(string[] args)
{
    var catalog = NewLootCatalogWithExtras();
    var known = new HashSet<string>(catalog.Items.Keys, StringComparer.Ordinal);
    known.UnionWith(BaseItemRowIds());
    var dead = new List<string>();
    foreach (var category in E33ModEditor.Core.Loot.ItemTiers.Categories)
    {
        foreach (var column in E33ModEditor.Core.Loot.ItemTiers.Columns)
        {
            foreach (var id in catalog.ItemTiers.Column(category, column))
            {
                if (!known.Contains(id)) dead.Add($"{category}.{column}: {id}");
            }
        }
    }
    if (dead.Count == 0)
    {
        Console.WriteLine("loot-audit clean: every tier id resolves to a base row or current mod id.");
        return 0;
    }
    foreach (var d in dead) Console.WriteLine("DEAD " + d);
    Console.Error.WriteLine($"{dead.Count} dead id(s) in item_tiers.json — remove them or the randomizer can ship broken drops.");
    return 1;
}

static string? GetOption(string[] args, string name)
{
    var idx = Array.IndexOf(args, name);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}

static int Unknown(string arg)
{
    Console.Error.WriteLine($"unknown command '{arg}' (try 'help')");
    return 1;
}

/// <summary>
/// Turns Maelle's Spark into Super Spark in the packed ExpeditionInfinity_<seed>_P tree:
/// Void, 3 hits, 10 Burn, Offensive 10 Blight, Void Niagara.
/// </summary>
static int CmdSuperSpark(string[] args)
{
    // Test-only command: Maelle Spark → Super Spark. Requires explicit --apply
    // so it never runs as part of normal rebuild-mod / standalone builds.
    // See docs/super-spark.md and docs/HowToCreateAndEditMoves.md.
    if (!args.Contains("--apply", StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine("superspark is a parked test edit (docs/super-spark.md).");
        Console.WriteLine("Pass --apply to inject Maelle Spark → Super Spark and pack ExpeditionInfinity_<seed>_P.");
        return 0;
    }
    var modDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod");
    SuperSparkKit.Inject(modDir);
    var sparkName = E33ModEditor.Core.Packer.FullPackKit.ModNameFor(CurrentSeed());
    var packed = new Retoc().PackCombined(modDir, sparkName);
    Console.WriteLine($"[pack] {sparkName} installed:");
    foreach (var f in packed) Console.WriteLine("       " + f);
    return 0;
}

/// <summary>
/// Stages the Maelle overhaul kit into a throwaway scratch dir (work/mod_maelle)
/// so the produced assets can be inspected (dump/kismet-check) before they are
/// wired into the real pack. Prints the staged edit count.
/// </summary>
static int CmdMaelleBlonde(string[] args)
{
    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_hair");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);
    DataTableEditor.ResetMappings(); // pristine schemas, same as pack runs

    var n = MaelleHairKit.Apply(dir);
    Console.WriteLine($"[maelle-blonde] retinted {n} hair vector param(s) into {dir}");
    Console.WriteLine("[maelle-blonde] next: pack work/mod_hair --name TheGoldenOne_P");
    return 0;
}

static int CmdMaelleStage(string[] args)
{
    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_maelle");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);
    DataTableEditor.ResetMappings(); // pristine schemas, same as pack runs

    // The kit reads source assets from work/tables + work/extract_maelle; make
    // sure the shared extracted stance family + skill BPs exist first.
    var n = MaelleSkillsKit.Apply(dir);
    Console.WriteLine($"[maelle-stage] staged {n} edit(s) into {dir}");
    return 0;
}

/// <summary>
/// Stages the Sciel overhaul kit into a throwaway scratch dir (work/mod_sciel)
/// so the produced assets can be inspected (dump/kismet-check) before they are
/// wired into the real pack. Prints the staged edit count.
/// </summary>
static int CmdScielStage(string[] args)
{
    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_sciel");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);
    DataTableEditor.ResetMappings(); // pristine schemas, same as pack runs

    var n = ScielSkillsKit.Apply(dir);
    Console.WriteLine($"[sciel-stage] staged {n} edit(s) into {dir}");
    return 0;
}

static int CmdScielCustomStage(string[] args)
{
    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_custom");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);
    DataTableEditor.ResetMappings(); // pristine schemas, same as pack runs

    var n = ScielSkillsKit.Apply(dir);
    n += ScielCustomKit.Apply(dir);
    Console.WriteLine($"[sciel-custom-stage] staged {n} edit(s) into {dir}");
    VerifyScielCustom(dir);
    return 0;
}

static int CmdVersoStage(string[] args)
{
    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_verso");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);
    DataTableEditor.ResetMappings(); // pristine schemas, same as pack runs

    var n = VersoSkillsKit.Apply(dir);
    Console.WriteLine($"[verso-stage] staged {n} edit(s) into {dir}");
    VerifyVerso(dir);
    return 0;
}

/// <summary>Re-opens the Verso stage output and asserts the respec contract:
/// balancing rows (direct + Verso clones + AssaultZero), DA costs, ST keys,
/// explicit rank gates (no live IsOvercharged reads in swapped BPs),
/// neutralized cost overrides, graph node swap, and untouched Gustave rows.</summary>
static void VerifyVerso(string dir)
{
    int checks = 0;
    void Check(bool ok, string msg)
    {
        checks++;
        if (!ok) throw new InvalidDataException("[verso-verify] FAIL: " + msg);
        Console.WriteLine("[verso-verify] ok: " + msg);
    }

    var bal = Path.Combine(dir, "Sandfall", "Content", "Gameplay", "Battle",
        "ScalingSystem", "BalancingTables", "Content", "DT_Balancing_MainCharacterSkills.uasset");
    var balDump = new DataTableEditor(bal);
    static double RowFloat(DataTableEditor b, string row)
    {
        if (!b.TryGetRow(row, out var r) || r is null) return double.NaN;
        foreach (var f in r.Value)
        {
            var n = E33ModEditor.Core.DataTableEditor.CleanName(f.Name);
            if (n is "FloatValue" or "Value")
            {
                if (double.TryParse(f.ToString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var v)) return v;
            }
        }
        return double.NaN;
    }
    // Direct rows with exact values.
    foreach (var (row, want) in new[] {
        ("ParadigmShift_BaseDamage", 0.33), ("ParadigmShift_OverchargeAPGain", 3.0),
        ("Leadership_OverchargeApGain", 3.0),
        ("QuickStrike_BaseDamage", 0.7425), ("QuickStrike_PerfectionBonus", 0.0),
        ("Followup_BaseDamage", 0.99), ("Followup_DamageIncreasePerShot", 0.10),
        ("Followup_MaxFreeAimAmount", 21.0),
        ("AscendingAssault_BaseDamage", 1.2375), ("AscendingAssault_DamageIncreasePerCast", 0.10),
        ("AscendingAssault_MaxCastCount", 21.0),
        ("RadiantSlash_BaseDamage", 1.485),
        ("LightHolder_BaseDamage", 0.1485),
        ("PerfectBreak_BaseDamage", 1.98),
        ("BerserkSlash_BaseDamage", 0.7425),
        ("PhantomStars_BaseDamage", 0.7272),
        ("PerfectRecovery_BaseHeal", 0.30), ("PerfectRecovery_OverchargeHealMultiplier", 1.6667),
        ("Purification_BaseDamage", 0.495), ("Purification_OverchargeDamageMultiplier", 1.5),
        ("Blitz_BaseDamage", 1.7325),
        ("SpeedBurst_BaseDamage", 0.59), ("SpeedBurst_OverchargeDamageMultiplier", 0.5),
        ("DefiantStrike_BaseDamage", 2.86), ("DefiantStrike_HpPercentLoss", 0.50),
        ("DefiantStrike_OverchargeDamageMultiplier", 2.0),
        ("EndBringer_BaseDamage", 0.9794), ("EndBringer_StunDamageMultiplier", 2.0),
        ("SteeledStrike_BaseDamage", 1.47), ("SteeledStrike_OverchargeDamageMultiplier", 10.0),
        ("FromFire_Verso_BaseDamage", 0.165), ("FromFire_Verso_OverchargeDamageMultiplier", 1.0),
        ("FromFire_Verso_HealPercentage", 0.20),
        ("MarkingShot_Verso_BaseDamage", 0.495), ("MarkingShot_Verso_OverchargeDamageMultiplier", 1.25),
        ("StrikeStorm_Verso_BaseDamage", 0.4455), ("StrikeStorm_Verso_OverchargeDamageMultiplier", 1.5),
        ("Powerful_Verso_BasePowerfuLDuration", 2.0), ("Powerful_Verso_IncreasedPowerfulDuration", 4.0),
        ("AssaultZero_BaseDamage", 0.0495), ("AssaultZero_OverchargeDamageMultiplier", 2.0),
        ("Zanmoto_BaseDamage", 8.4457), ("Repainted_NewHpValue", 33.0),
        ("RedBushido_BaseDamage", 6.5034), ("RedBushido_DamageIncreasePerPercentHP", 0.02),
        ("VoidedStars_BaseDamage", 3.0694), ("Blitzer_BaseDamage", 4.9604),
    })
    {
        var got = RowFloat(balDump, row);
        Check(!double.IsNaN(got) && Math.Abs(got - want) < 0.0005, $"{row}={got} (expect {want})");
    }
    // Shared plain rows keep vanilla values (Gustave safety).
    foreach (var (row, want) in new[] {
        ("FromFire_BaseDamage", 0.625), ("MarkingShot_BaseDamage", 1.0),
        ("StrikeStorm_BaseDamage", 0.7), ("Combo1_BaseDamage", 0.25),
    })
    {
        var got = RowFloat(balDump, row);
        Check(!double.IsNaN(got) && Math.Abs(got - want) < 0.0005, $"{row} untouched={got}");
    }

    // DA costs.
    var daCosts = new Dictionary<string, int>
    {
        ["DA_Skill_Powerful"] = 2, ["DA_Skill_Verso_NEW_ParadigmShift"] = 4,
        ["DA_Skill_Verso_NEW_Leadership"] = 5, ["DA_Skill_Verso_NEW_QuickStrike"] = 3,
        ["DA_Skill_Verso_NEW_Followup"] = 4, ["DA_Skill_Verso_AscendingAssault"] = 5,
        ["DA_Skill_Verso_RadiantSlash"] = 6, ["DA_Skill_NEW_4"] = 3,
        ["DA_Skill_Verso_PerfectBreak"] = 8, ["DA_Skill_Verso_BerserkSlash"] = 9,
        ["DA_Skill_Verso_NEW_Overcharge"] = 10, ["DA_Skill_Verso_NEW_PhantomStars"] = 11,
        ["DA_Skill_Verso_NEW_FromFire"] = 2, ["DA_Skill_Verso_NEW_Purification"] = 4,
        ["DA_Skill_Blitz"] = 7, ["DA_Skill_Verso_Strikestorm"] = 9,
        ["DA_Skill_Verso_DefiantStrike"] = 10, ["DA_Skill_Verso_Endbringer"] = 13,
        ["DA_Skill_Verso_SteeledStrike"] = 15, ["DA_Skill_Verso_AssaultZero"] = 1,
        ["DA_Skill_Verso_Zanmoto"] = 24, ["DA_Skill_Verso_Repainted"] = 17,
        ["DA_Skill_Verso_RedBushido"] = 18, ["DA_Skill_Verso_VoidedStars"] = 17,
        ["DA_Skill_Verso_Blitzer"] = 19,
    };
    foreach (var (stem, want) in daCosts)
    {
        var p = FileCopy.FindUnder(dir, stem + ".uasset");
        Check(p != null, stem + " staged");
        if (p is null) continue;
        var leaves = DataTableEditor.OpenPropertyAsset(p).FlattenProperties();
        var hit = leaves.FirstOrDefault(l => l.Path == "APCost");
        Check(hit != null && hit.Value?.ToString() == want.ToString(), $"{stem} APCost={hit?.Value} (expect {want})");
    }

    // ST keys (Long present for every move incl. AssaultZero + Blood Strike name).
    var st = FileCopy.FindUnder(dir, "ST_MainCharacters_Skills.uasset");
    Check(st != null, "string table staged");
    if (st != null)
    {
        var table = DataTableEditor.OpenStringTable(st);
        foreach (var k in new[] { "Verso_ParadigmShift", "Verso_Leadership", "Verso_QuickStrike",
            "Verso_Followup", "Verso_AscendingAssault", "Verso_RadiantSlash", "Verso_LightHolder",
            "Verso_PerfectBreak", "Verso_BerserkSlash", "Verso_Overcharge", "Verso_PhantomStars",
            "Verso_FromFire", "Verso_MarkingShot", "Verso_PerfectRecovery", "Verso_Purification",
            "Verso_Blitz", "Verso_StrikeStorm", "Verso_SpeedBurst", "Verso_DefiantStrike",
            "Verso_EndBringer", "Verso_SteeledStrike", "Verso_AssaultZero", "Verso_Powerful" })
            Check(table.ContainsKey(k + "_Description_Long"), "ST " + k + "_Long");
        Check(table.TryGetValue("Verso_DefiantStrike_Name", out var bn) && bn == "Blood Strike",
            "Blood Strike rename");
        Check(table.TryGetValue("Verso_SpeedBurst_Description_Long", out var sb) && sb.Contains("Rank_B"),
            "SpeedBurst rank letter present");
        Check(table.TryGetValue("Verso_PhantomStars_Description_Long", out var ps) && ps.Contains("Buff_Slow"),
            "PhantomStars Slow keyword = Buff_Slow");
        Check(table.TryGetValue("Verso_Overcharge_Description_Long", out var oc) && !oc.Contains("refill"),
            "Overload desc drops AP refill");
        Check(table.TryGetValue("Verso_SteeledStrike_Description_Long", out var ss)
            && !ss.Contains("triple", StringComparison.OrdinalIgnoreCase) && ss.Contains("×30"),
            "SteeledStrike triple in the number");
        Check(table.TryGetValue("Verso_DefiantStrike_Description_Long", out var bs)
            && bs.Contains("Rank_A") && bs.Contains("Absorbs 50% of maximum Health") && bs.Contains("100% more damage"),
            "BloodStrike absorb desc");
        Check(table.TryGetValue("Verso_EndBringer_Description_Long", out var eb) && eb.Contains("100% more damage"),
            "EndBringer +100% desc");
        Check(table.TryGetValue("Verso_BerserkSlash_Description_Long", out var bk) && bk.Contains("+100%"),
            "BerserkSlash +100% desc");
        Check(table.TryGetValue("Verso_Zanmoto_Description_Long", out var zn)
            && zn.Contains("After 2 turns") && zn.Contains("Ethereal swords") && zn.Contains("Rank_S"),
            "Zanmoto desc");
        Check(table.TryGetValue("Verso_Repainted_Description_Long", out var rp)
            && rp.Contains("Rank_S") && rp.Contains("12") && rp.Contains("Cursed")
            && rp.Contains("100") && rp.Contains("33"),
            "Repainted desc");
        Check(table.TryGetValue("Verso_RedBushido_Description_Long", out var rb)
            && rb.Contains("+100%") && rb.Contains("Rank_S"),
            "RedBushido desc");
        Check(table.TryGetValue("Verso_VoidedStars_Description_Long", out var vs)
            && vs.Contains("Element_Void") && vs.Contains("+200%") && !vs.Contains("Slow"),
            "VoidedStars desc");
        Check(table.TryGetValue("Verso_Blitzer_Description_Long", out var bz)
            && bz.Contains("500 Speed") && bz.Contains("Element_Lightning"),
            "Blitzer desc");
    }

    // Explicit gates: swapped BPs carry GetRankValue + GreaterEqual and no
    // live SkillState.IsOvercharged reads in OnActionEffect.
    var gmap = DataTableEditor.Mappings;
    UAssetAPI.UAsset OpenBp(string stem)
    {
        var p = FileCopy.FindUnder(dir, stem + ".uasset")
            ?? throw new InvalidDataException("[verso-verify] missing " + stem);
        return gmap != null
            ? new UAssetAPI.UAsset(p, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, gmap)
            : new UAssetAPI.UAsset(p, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    }
    bool HasIsOvercharged(UAssetAPI.UAsset u)
    {
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .First(f => f.ObjectName.ToString() == "OnActionEffect");
        bool hit = false;
        E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (hit) return;
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_Context ctx
                && ctx.ObjectExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_InstanceVariable oiv
                && oiv.Variable?.New?.Path?.Length > 0
                && oiv.Variable.New.Path[^1].ToString() == "SkillState"
                && ctx.ContextExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_InstanceVariable civ
                && civ.Variable?.New?.Path?.Length > 0
                && civ.Variable.New.Path[^1].ToString() == "IsOvercharged") hit = true;
        });
        return hit;
    }
    bool HasGateCall(UAssetAPI.UAsset u, string lvf)
    {
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .First(f => f.ObjectName.ToString() == "OnActionEffect");
        bool math = false, comp = false;
        E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm && cm.StackNode.IsImport()
                && cm.StackNode.ToImport(u).ObjectName.ToString() == "GreaterEqual_ByteByte") math = true;
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf
                && vf.VirtualFunctionName.ToString() == lvf) comp = true;
        });
        return math && comp;
    }
    foreach (var bpStem in new[] { "BP_Battle_SkillScript_Powerful", "BP_Battle_SkillScript_Verso_ParadigmShift",
        "BP_Battle_SkillScript_Verso_Leadership", "BP_Battle_SkillScript_Verso_RadiantSlash",
        "BP_Battle_SkillScript_Verso_BerserkSlash", "BP_Battle_SkillScript_Verso_FromFire",
        "BP_Battle_SkillScript_MarkingShot", "BP_Battle_SkillScript_PerfectRecovery",
        "BP_Battle_SkillScript_NEW_15", "BP_Battle_SkillScript_Blitz",
        "BP_Battle_SkillScript_Verso_StrikeStorm", "BP_Battle_SkillScript_Verso_SpeedBurst",
        "BP_Battle_SkillScript_Verso_DefiantStrike", "BP_Battle_SkillScript_Verso_EndBringer",
        "BP_Battle_SkillScript_Verso_SteeledStrike", "BP_Battle_SkillScript_Verso_AssaultZero" })
    {
        var u = OpenBp(bpStem);
        Check(HasGateCall(u, "GetComponentByClass"), bpStem + " explicit rank check");
        Check(!HasIsOvercharged(u), bpStem + " no live IsOvercharged read");
    }
    // Cost overrides neutralized (Override writer assigns False).
    foreach (var bpStem in new[] { "BP_Battle_SkillScript_Verso_Followup",
        "BP_Battle_SkillScript_Verso_AscendingAssault", "BP_Battle_SkillScript_Verso_PhantomStars",
        "BP_Battle_SkillScript_Verso_PerfectBreak" })
    {
        var u = OpenBp(bpStem);
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .First(f => f.ObjectName.ToString() == "GetSkillCostOverride");
        int writers = 0;
        bool isFalse = false;
        E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (e is not UAssetAPI.Kismet.Bytecode.Expressions.EX_LetBool lb) return;
            string target = lb.VariableExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable lv
                ? lv.Variable?.New?.Path?.Length > 0 ? lv.Variable.New.Path[^1].ToString() : ""
                : lb.VariableExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalOutVariable lov
                    && lov.Variable?.New?.Path?.Length > 0 ? lov.Variable.New.Path[^1].ToString() : "";
            if (!target.Contains("Override", StringComparison.OrdinalIgnoreCase)) return;
            writers++;
            if (lb.AssignmentExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_False) isFalse = true;
        });
        Check(writers == 1 && isFalse, bpStem + " cost override = false");
    }
    // Overload: AP refill gone, rank S set, self Berserk applied.
    {
        var u = OpenBp("BP_Battle_SkillScript_Verso_Overcharge");
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .First(f => f.ObjectName.ToString() == "OnActionEffect");
        bool gain = false, rankS = false, berserk = false;
        E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf)
            {
                var nm = vf.VirtualFunctionName.ToString();
                if (nm == "GainAP") gain = true;
                if (nm == "SetSpecificRank" && vf.Parameters.Length > 0
                    && vf.Parameters[0] is UAssetAPI.Kismet.Bytecode.Expressions.EX_ByteConst bc && bc.Value == 5)
                    rankS = true;
            }
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_ObjectConst eoc && eoc.Value.IsImport()
                && eoc.Value.ToImport(u).ObjectName.ToString() == "BP_BattleBuff_Berserk_C") berserk = true;
        });
        Check(!gain, "Overload no AP refill");
        Check(rankS, "Overload rank -> S");
        Check(berserk, "Overload self Berserk");
    }
    // Steeled Strike: vanilla Base x Hits x RankMult (Temp 1.0, no hidden mult).
    {
        var u = OpenBp("BP_Battle_SkillScript_Verso_SteeledStrike");
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .First(f => f.ObjectName.ToString() == "OnActionEffect");
        int tempAssign = 0;
        bool tempOne = false;
        E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_Let let
                && let.Variable is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable lv
                && lv.Variable?.New?.Path?.Length > 0
                && lv.Variable.New.Path[^1].ToString() == "Temp_real_Variable"
                && let.Expression is UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst dc)
            { tempAssign++; if (Math.Abs(dc.Value - 1.0) < 1e-9) tempOne = true; }
        });
        Check(tempAssign == 1 && tempOne, "SteeledStrike Temp = 1.0 (no hidden mult)");
    }
    // Blood Strike: A-gated conditional absorb. One Temp_bool (rank>=A AND
    // affordable) feeds the damage switch and skips the HP pipeline.
    {
        var u = OpenBp("BP_Battle_SkillScript_Verso_DefiantStrike");
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .First(f => f.ObjectName.ToString() == "OnActionEffect");
        bool curInLoss = false;
        int maxReads = 0, threshA = 0;
        bool hasAnd = false, tempSkip = false;
        foreach (var s in fn.ScriptBytecode!)
            E33ModEditor.Core.MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable mlv
                    && mlv.Variable?.New?.Path?.Length > 0
                    && mlv.Variable.New.Path[^1].ToString() == "CallFunc_GetMaxHP_MaxHP") maxReads++;
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cmA && cmA.StackNode.IsImport()
                    && cmA.StackNode.ToImport(u).ObjectName.ToString() == "BooleanAND") hasAnd = true;
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_JumpIfNot jn
                    && jn.BooleanExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable jlv
                    && jlv.Variable?.New?.Path?.Length > 0
                    && jlv.Variable.New.Path[^1].ToString() == "Temp_bool_Variable") tempSkip = true;
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LetBool lb
                    && lb.VariableExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable tlv
                    && tlv.Variable?.New?.Path?.Length > 0
                    && tlv.Variable.New.Path[^1].ToString() == "Temp_bool_Variable")
                    E33ModEditor.Core.MayhemFixKit.Walk(new[] { lb.AssignmentExpression }, x =>
                    {
                        if (x is UAssetAPI.Kismet.Bytecode.Expressions.EX_ByteConst bc && bc.Value == 4) threshA++;
                    });
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_Let let
                    && let.Expression is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm
                    && cm.StackNode.IsImport()
                    && cm.StackNode.ToImport(u).ObjectName.ToString().Contains("Multiply"))
                {
                    E33ModEditor.Core.MayhemFixKit.Walk(new[] { cm }, x =>
                    {
                        if (x is UAssetAPI.Kismet.Bytecode.Expressions.EX_InstanceVariable iv
                            && iv.Variable?.New?.Path?.Length > 0
                            && iv.Variable.New.Path[^1].ToString() == "CurrentHP") curInLoss = true;
                    });
                }
            });
        Check(!curInLoss, "BloodStrike loss never reads CurrentHP");
        Check(maxReads >= 3, $"BloodStrike MaxHP reads x{maxReads}");
        Check(threshA == 1, "BloodStrike gate threshold A");
        Check(hasAnd, "BloodStrike combined rank+affordable condition");
        Check(tempSkip, "BloodStrike HP pipeline gated on Temp_bool");
    }
    // Rank badges suppressed on the shared Perfection component.
    {
        var u = OpenBp("BP_SkillDataComponent_PerfectionMechanic");
        foreach (var fnName in new[] { "ModifySkillButton", "ModifySkillTooltip" })
        {
            var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
                .First(f => f.ObjectName.ToString() == fnName);
            int writers = 0;
            bool isFalse = false;
            E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
            {
                if (e is not UAssetAPI.Kismet.Bytecode.Expressions.EX_LetBool lb) return;
                string target = lb.VariableExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable lv
                    ? lv.Variable?.New?.Path?.Length > 0 ? lv.Variable.New.Path[^1].ToString() : "" : "";
                if (target != "CallFunc_NotEqual_ByteByte_ReturnValue") return;
                writers++;
                if (lb.AssignmentExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_False) isFalse = true;
            });
            Check(writers == 1 && isFalse, fnName + " badge gate = false");
        }
    }
    // Graph: node0 is AssaultZero, Combo1 gone, node import resolves.
    var gpath = FileCopy.FindUnder(dir, "DA_SkillGraph_Verso.uasset");
    Check(gpath != null, "Verso graph staged");
    if (gpath != null)
    {
        var gg = gmap != null
            ? new UAssetAPI.UAsset(gpath, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, gmap)
            : new UAssetAPI.UAsset(gpath, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
        var gx = gg.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>().First();
        var nodes = (UAssetAPI.PropertyTypes.Objects.ArrayPropertyData)gx.Data[0];
        var first = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)nodes.Value[0];
        var su = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)first.Value[0];
        var ptr = ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)su.Value[0]).Value;
        Check(ptr != null && ptr.IsImport(), "node0 skill is an import");
        var impName = ptr != null && ptr.IsImport()
            ? gg.Imports[Math.Abs(ptr.Index) - 1].ObjectName.ToString() : "";
        Check(impName == "DA_Skill_Verso_AssaultZero", "node0 = AssaultZero (got " + impName + ")");
        var imp = ptr != null && ptr.IsImport() ? gg.Imports[Math.Abs(ptr.Index) - 1] : null;
        var outer = imp?.OuterIndex is { } oi && oi.IsImport() ? gg.Imports[Math.Abs(oi.Index) - 1] : null;
        var pkgFile = outer != null
            ? FileCopy.FindUnder(dir, outer.ObjectName.ToString().Split('/').Last() + ".uasset") : null;
        Check(pkgFile != null, "node0 import package staged");
        bool comboLeft = false;
        foreach (var nv in nodes.Value)
        {
            var s2 = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)((UAssetAPI.PropertyTypes.Structs.StructPropertyData)nv).Value[0];
            var p2 = ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)s2.Value[0]).Value;
            if (p2 != null && p2.IsImport()
                && gg.Imports[Math.Abs(p2.Index) - 1].ObjectName.ToString() == "DA_Skill_Combo1") comboLeft = true;
        }
        Check(!comboLeft, "no Combo1 node remains");
        // New moves: 5 nodes (cost = AP, x >= 975) + Steeled chain edges.
        var wantNodes = new Dictionary<string, (int Ap, double X)>
        {
            ["DA_Skill_Verso_Zanmoto"] = (24, 975), ["DA_Skill_Verso_Repainted"] = (17, 1100),
            ["DA_Skill_Verso_RedBushido"] = (18, 1225), ["DA_Skill_Verso_VoidedStars"] = (17, 1350),
            ["DA_Skill_Verso_Blitzer"] = (19, 1475),
        };
        var nodeIdxByStem = new Dictionary<string, int>();
        foreach (var nv in nodes.Value)
        {
            var s2 = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)((UAssetAPI.PropertyTypes.Structs.StructPropertyData)nv).Value[0];
            var p2 = ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)s2.Value[0]).Value;
            if (p2 is null || !p2.IsImport()) continue;
            var nm = gg.Imports[Math.Abs(p2.Index) - 1].ObjectName.ToString();
            nodeIdxByStem[nm] = Array.IndexOf(nodes.Value, nv);
            if (wantNodes.TryGetValue(nm, out var want))
            {
                var cost = ((UAssetAPI.PropertyTypes.Objects.IntPropertyData)s2.Value[1]).Value;
                Check(cost == want.Ap, $"graph {nm} UnlockCost={cost}");
                var posWrap = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)
                    ((UAssetAPI.PropertyTypes.Structs.StructPropertyData)nv).Value[1];
                var pos = ((UAssetAPI.PropertyTypes.Structs.Vector2DPropertyData)posWrap.Value[0]).Value;
                Check(Math.Abs(pos.X - want.X) < 1 && Math.Abs(pos.Y - 525) < 1,
                    $"graph {nm} pos=({pos.X},{pos.Y})");
            }
        }
        foreach (var stem in wantNodes.Keys)
            Check(nodeIdxByStem.ContainsKey(stem), "graph node " + stem);
        var edges = (UAssetAPI.PropertyTypes.Objects.ArrayPropertyData)gx.Data[1];
        int chainEdges = 0;
        var wantLinks = new HashSet<(string, string)>
        {
            ("DA_Skill_Verso_SteeledStrike", "DA_Skill_Verso_Zanmoto"),
            ("DA_Skill_Verso_Zanmoto", "DA_Skill_Verso_Repainted"),
            ("DA_Skill_Verso_Repainted", "DA_Skill_Verso_RedBushido"),
            ("DA_Skill_Verso_RedBushido", "DA_Skill_Verso_VoidedStars"),
            ("DA_Skill_Verso_VoidedStars", "DA_Skill_Verso_Blitzer"),
        };
        string StemOf(UAssetAPI.UnrealTypes.FPackageIndex? pi) =>
            pi != null && pi.IsImport() ? gg.Imports[Math.Abs(pi.Index) - 1].ObjectName.ToString() : "";
        foreach (var ev in edges.Value)
        {
            var es = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)ev;
            var f = StemOf(((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)es.Value[0]).Value);
            var t = StemOf(((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)es.Value[1]).Value);
            if (wantLinks.Contains((f, t))) chainEdges++;
        }
        Check(chainEdges == 5, $"graph chain edges x{chainEdges}");
    }
    // New moves: gates + per-move kismet contract.
    foreach (var bpStem in new[] { "BP_Battle_SkillScript_Verso_Zanmoto",
        "BP_Battle_SkillScript_Verso_RedBushido", "BP_Battle_SkillScript_Verso_Blitzer" })
    {
        var u = OpenBp(bpStem);
        Check(HasGateCall(u, "GetComponentByClass"), bpStem + " explicit rank check");
        Check(!HasIsOvercharged(u), bpStem + " no live IsOvercharged read");
    }
    foreach (var bpStem in new[] { "BP_Battle_SkillScript_Verso_Repainted",
        "BP_Battle_SkillScript_Verso_VoidedStars" })
    {
        var u = OpenBp(bpStem);
        Check(!HasIsOvercharged(u), bpStem + " no live IsOvercharged read");
    }
    // Zanmoto: count->2, uber re-force jump, 2 Light sword builders, FX.
    {
        var u = OpenBp("BP_Battle_SkillScript_Verso_Zanmoto");
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .First(f => f.ObjectName.ToString() == "OnActionEffect");
        bool count2 = false;
        int lightBuilders = 0;
        bool fx = false;
        int timeOffsets = 0;
        E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LetBool lb)
            {
                bool ne = false, fc = false, two = false;
                E33ModEditor.Core.MayhemFixKit.Walk(new[] { lb }, x =>
                {
                    if (x is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm && cm.StackNode.IsImport()
                        && cm.StackNode.ToImport(u).ObjectName.ToString() == "NotEqual_IntInt") ne = true;
                    if (x is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable lv
                        && lv.Variable?.New?.Path?.Length > 0
                        && lv.Variable.New.Path[^1].ToString() == "CallFunc_GetForcedSkillExecutionCount_ReturnValue") fc = true;
                    if (x is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ic && ic.Value == 2) two = true;
                });
                if (ne && fc && two) count2 = true;
            }
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf
                && vf.VirtualFunctionName.ToString() == "MakeDamageBuilder")
            {
                foreach (var p in vf.Parameters)
                {
                    if (p is UAssetAPI.Kismet.Bytecode.Expressions.EX_ByteConst bc)
                    {
                        if (bc.Value == 7) lightBuilders++;
                        break;
                    }
                }
            }
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_ObjectConst eoc && eoc.Value.IsImport()
                && eoc.Value.ToImport(u).ObjectName.ToString() == "BP_FX_FollowingSwordVerso_C") fx = true;
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_NameConst nc
                && nc.Value.ToString() == "TimeOffset") timeOffsets++;
        });
        Check(count2, "Zanmoto damage branch at count 2");
        Check(lightBuilders == 2, $"Zanmoto sword builders x{lightBuilders}");
        Check(fx, "Zanmoto sword FX import presence");
        Check(timeOffsets == 2, $"Zanmoto TimeOffsets x{timeOffsets}");
        // Spawn matches the vanilla skill-script shape: EX_Self world context,
        // EX_Context member calls, ShouldFollow off, Deactivate for teardown.
        {
            bool ctxOk = false, followOk = false;
            bool k2Flat = false, tfFlat = false, nestedFinal = false;
            bool weaponAnchor = false, lifeSpan = false;
            E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
            {
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_NameConst nc
                    && nc.Value.ToString() == "TargetToFollow") followOk = true;
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction wvf
                    && wvf.VirtualFunctionName.ToString() == "GetWeaponActor") weaponAnchor = true;
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction svf
                    && svf.VirtualFunctionName.ToString() == "SetLifeSpan"
                    && svf.Parameters.Length == 1
                    && svf.Parameters[0] is UAssetAPI.Kismet.Bytecode.Expressions.EX_FloatConst) lifeSpan = true;
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm && cm.StackNode.IsImport()
                    && cm.StackNode.ToImport(u).ObjectName.ToString() == "BeginDeferredActorSpawnFromClass"
                    && cm.Parameters.Length > 0
                    && cm.Parameters[0] is UAssetAPI.Kismet.Bytecode.Expressions.EX_Self) ctxOk = true;
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_Let let)
                {
                    // Member call: wrapped in EX_Context (object) like vanilla.
                    var locExpr = let.Expression is UAssetAPI.Kismet.Bytecode.Expressions.EX_Context lctx
                        ? lctx.ContextExpression : let.Expression;
                    if (locExpr is UAssetAPI.Kismet.Bytecode.Expressions.EX_FinalFunction ff
                        && ff.StackNode.IsImport()
                        && ff.StackNode.ToImport(u).ObjectName.ToString() == "K2_GetActorLocation")
                        k2Flat = true;
                    if (let.Expression is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath lcm
                        && lcm.StackNode.IsImport()
                        && lcm.StackNode.ToImport(u).ObjectName.ToString() == "MakeTransform")
                        tfFlat = true;
                }
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath outer)
                {
                    E33ModEditor.Core.MayhemFixKit.Walk(outer.Parameters, x =>
                    {
                        if (x is UAssetAPI.Kismet.Bytecode.Expressions.EX_FinalFunction) nestedFinal = true;
                    });
                }
            });
            // Locals registered on the function.
            var ufn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
                .First(f => f.ObjectName.ToString() == "OnActionEffect");
            var propNames = (ufn.LoadedProperties ?? System.Array.Empty<UAssetAPI.FieldTypes.FProperty>())
                .Select(p => p.Name.ToString()).ToHashSet();
            var structs = (ufn.LoadedProperties ?? System.Array.Empty<UAssetAPI.FieldTypes.FProperty>())
                .OfType<UAssetAPI.FieldTypes.FStructProperty>()
                .Where(p => p.Name.ToString() == "ZanSwordLoc" || p.Name.ToString() == "ZanSwordTf")
                .ToList();
            var structSizes = structs.ToDictionary(p => p.Name.ToString(), p => p.ElementSize);
            var tfStruct = structs.FirstOrDefault(p => p.Name.ToString() == "ZanSwordTf")?.Struct;
            bool tfIsTransform = tfStruct is { } si && si.IsImport()
                && si.ToImport(u).ObjectName.ToString() == "Transform";
            Check(ctxOk, "Zanmoto spawn world context = Self");
            Check(followOk, "Zanmoto follow anchor -> weapon mesh");
            Check(weaponAnchor, "Zanmoto anchor traces wielder's weapon");
            Check(lifeSpan, "Zanmoto sword lifetime 1.5s");
            Check(k2Flat, "Zanmoto location read is flat");
            Check(tfFlat, "Zanmoto transform is flat");
            Check(!nestedFinal, "Zanmoto no nested engine calls");
            Check(propNames.Contains("ZanSwordLoc") && propNames.Contains("ZanSwordTf"),
                "Zanmoto struct scratch locals");
            // loc = FVector (24), tf = FTransform (96, 16B-aligned). A Vector-sized
            // tf local corrupted the VM stack -> S-rank CTD.
            Check(structSizes.Count == 2 && structSizes["ZanSwordLoc"] == 24
                && structSizes["ZanSwordTf"] == 96 && tfIsTransform,
                $"Zanmoto struct scratch (loc=Vector/24, tf=Transform/96; got {string.Join(",", structSizes)} tfTransform={tfIsTransform})");
        }
        var uber = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .First(f => f.ObjectName.ToString().StartsWith("ExecuteUbergraph_"));
        bool reforce = false, uberCount2 = false;
        E33ModEditor.Core.MayhemFixKit.Walk(uber.ScriptBytecode!, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_Jump j && j.CodeOffset == 486) reforce = true;
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LetBool lb)
            {
                bool ne = false, two = false;
                E33ModEditor.Core.MayhemFixKit.Walk(new[] { lb }, x =>
                {
                    if (x is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm && cm.StackNode.IsImport()
                        && cm.StackNode.ToImport(u).ObjectName.ToString() == "NotEqual_IntInt") ne = true;
                    if (x is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ic && ic.Value == 2) two = true;
                });
                if (ne && two) uberCount2 = true;
            }
        });
        Check(reforce, "Zanmoto middle turn re-forces");
        Check(uberCount2, "Zanmoto uber damage branch at count 2");
    }
    // Repainted: no AP gain, rank S, buff battery, curse, burn stacks.
    {
        var u = OpenBp("BP_Battle_SkillScript_Verso_Repainted");
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .First(f => f.ObjectName.ToString() == "OnActionEffect");
        bool gain = false, rankS = false, curse = false;
        var buffs = new HashSet<string>();
        var stackCounts = new HashSet<int>();
        E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf)
            {
                var nm = vf.VirtualFunctionName.ToString();
                if (nm == "GainAP") gain = true;
                if (nm == "SetSpecificRank" && vf.Parameters.Length > 0
                    && vf.Parameters[0] is UAssetAPI.Kismet.Bytecode.Expressions.EX_ByteConst bc && bc.Value == 5)
                    rankS = true;
                if (nm == "SetCurseDuration") curse = true;
                if (nm == "ChangeStackCount" && vf.Parameters.Length > 0
                    && vf.Parameters[0] is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ic)
                    stackCounts.Add(ic.Value);
            }
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_ObjectConst eoc && eoc.Value.IsImport())
            {
                var onm = eoc.Value.ToImport(u).ObjectName.ToString();
                if (onm.StartsWith("BP_BattleBuff_")) buffs.Add(onm);
            }
        });
        Check(!gain, "Repainted no AP gain");
        Check(rankS, "Repainted rank -> S");
        Check(buffs.SetEquals(new[] { "BP_BattleBuff_Berserk_C", "BP_BattleBuff_GreaterSpeed_C",
            "BP_BattleBuff_GreaterShell_C", "BP_BattleBuff_GreaterPowerful_C",
            "BP_BattleBuff_Cursed_C", "BP_BattleBuff_Burn_C" }),
            "Repainted buff battery x6 (got " + string.Join(",", buffs) + ")");
        Check(stackCounts.SetEquals(new[] { 12, 100 }), "Repainted stacks 12 + 100");
        Check(curse, "Repainted curse duration set");
        // Battery runs once: JumpIfNot(IsLast) -> Return precedes it.
        {
            bool once = false;
            foreach (var s in fn.ScriptBytecode!)
            {
                if (s is UAssetAPI.Kismet.Bytecode.Expressions.EX_JumpIfNot jn
                    && jn.BooleanExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable lv
                    && lv.Variable?.New?.Path?.Length > 0
                    && lv.Variable.New.Path[^1].ToString() == "CallFunc_IsLastActionEffectOfSkill_ReturnValue")
                {
                    once = true;
                    break;
                }
            }
            Check(once, "Repainted battery once-gated");
        }
    }
    // Red Bushido: capped HP bonus.
    {
        var u = OpenBp("BP_Battle_SkillScript_Verso_RedBushido");
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .First(f => f.ObjectName.ToString() == "OnActionEffect");
        bool capped = false;
        E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf
                && vf.VirtualFunctionName.ToString() == "AddFinalDamageMultiplier"
                && vf.Parameters.Length > 0
                && vf.Parameters[0] is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath) capped = true;
        });
        Check(capped, "RedBushido HP bonus capped");
    }
    // Voided Stars: Void byte, no statuses, break x3, stun kept.
    {
        var u = OpenBp("BP_Battle_SkillScript_Verso_VoidedStars");
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .First(f => f.ObjectName.ToString() == "OnActionEffect");
        bool voidByte = false, buff = false, stun = false, triple = false;
        E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf)
            {
                var nm = vf.VirtualFunctionName.ToString();
                if (nm == "MakeDamageBuilder")
                {
                    foreach (var p in vf.Parameters)
                    {
                        if (p is UAssetAPI.Kismet.Bytecode.Expressions.EX_ByteConst bc)
                        {
                            if (bc.Value == 8) voidByte = true;
                            break;
                        }
                    }
                }
                if (nm == "ApplyBuff") buff = true;
                if (nm == "TryPerformBreakStun") stun = true;
            }
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm && cm.StackNode.IsImport()
                && cm.StackNode.ToImport(u).ObjectName.ToString() == "Multiply_DoubleDouble")
            {
                bool three = false;
                E33ModEditor.Core.MayhemFixKit.Walk(cm.Parameters, x =>
                {
                    if (x is UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst dc
                        && Math.Abs(dc.Value - 3.0) < 1e-9) three = true;
                });
                if (three) triple = true;
            }
        });
        Check(voidByte, "VoidedStars Void damage byte");
        Check(!buff, "VoidedStars no statuses");
        Check(triple, "VoidedStars break x3");
        Check(stun, "VoidedStars break-stun kept");
        // Cloned S-cost discount neutralized (same as the vanilla four).
        {
            var ofn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
                .FirstOrDefault(f => f.ObjectName.ToString() == "GetSkillCostOverride");
            bool neutral = false;
            if (ofn?.ScriptBytecode != null)
            {
                E33ModEditor.Core.MayhemFixKit.Walk(ofn.ScriptBytecode!, e =>
                {
                    if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LetBool lb)
                    {
                        string target = lb.VariableExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable lv
                            && lv.Variable?.New?.Path?.Length > 0 ? lv.Variable.New.Path[^1].ToString()
                            : lb.VariableExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalOutVariable lov
                                && lov.Variable?.New?.Path?.Length > 0 ? lov.Variable.New.Path[^1].ToString() : "";
                        if (target.Contains("Override", System.StringComparison.OrdinalIgnoreCase)
                            && lb.AssignmentExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_False)
                            neutral = true;
                    }
                });
            }
            Check(neutral, "VoidedStars cost override = false");
        }
        // Branch targets + placement: [prelude][jumpS][sBuilder]
        // [jumpOver][base builder][deal]. The block must sit BEFORE the
        // base builder (fall-through hits the prelude, never the builder).
        {
            var fns = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
                .First(f => f.ObjectName.ToString() == "OnActionEffect");
            uint off = 0;
            uint preludeOff = 0, sBuilderOff = 0, baseBuilderOff = 0, dealOff = 0;
            uint jumpSTarget = 0, jumpOverTarget = 0;
            foreach (var s in fns.ScriptBytecode!)
            {
                if (s is UAssetAPI.Kismet.Bytecode.Expressions.EX_LetObj)
                {
                    bool hasBuilder = false, hasTriple = false, hasComp = false;
                    E33ModEditor.Core.MayhemFixKit.Walk(new[] { s }, x =>
                    {
                        if (x is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf)
                        {
                            var nm = vf.VirtualFunctionName.ToString();
                            if (nm == "MakeDamageBuilder") hasBuilder = true;
                            if (nm == "GetComponentByClass") hasComp = true;
                        }
                        if (x is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm && cm.StackNode.IsImport()
                            && cm.StackNode.ToImport(u).ObjectName.ToString() == "Multiply_DoubleDouble")
                        {
                            E33ModEditor.Core.MayhemFixKit.Walk(cm.Parameters, y =>
                            {
                                if (y is UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst dc
                                    && Math.Abs(dc.Value - 3.0) < 1e-9) hasTriple = true;
                            });
                        }
                    });
                    if (hasComp && preludeOff == 0) preludeOff = off;
                    if (hasBuilder && hasTriple) sBuilderOff = off;
                    if (hasBuilder && !hasTriple) baseBuilderOff = off;
                }
                if (s is UAssetAPI.Kismet.Bytecode.Expressions.EX_Context)
                {
                    bool deal = false;
                    E33ModEditor.Core.MayhemFixKit.Walk(new[] { s }, x =>
                    {
                        if (x is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf
                            && vf.VirtualFunctionName.ToString() == "DealDamages") deal = true;
                    });
                    if (deal && dealOff == 0) dealOff = off;
                }
                if (s is UAssetAPI.Kismet.Bytecode.Expressions.EX_JumpIfNot jn)
                {
                    bool ge = false;
                    E33ModEditor.Core.MayhemFixKit.Walk(new[] { jn.BooleanExpression }, x =>
                    {
                        if (x is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm && cm.StackNode.IsImport()
                            && cm.StackNode.ToImport(u).ObjectName.ToString() == "GreaterEqual_ByteByte") ge = true;
                    });
                    if (ge) jumpSTarget = jn.CodeOffset;
                }
                if (s is UAssetAPI.Kismet.Bytecode.Expressions.EX_Jump j
                    && sBuilderOff > 0 && off > sBuilderOff && dealOff == 0) jumpOverTarget = j.CodeOffset;
                off += s.GetSize(u);
            }
            Check(preludeOff > 0 && preludeOff < baseBuilderOff,
                $"VoidedStars branch before builder ({preludeOff} vs {baseBuilderOff})");
            Check(baseBuilderOff > 0 && jumpSTarget == baseBuilderOff,
                $"VoidedStars non-S -> base builder ({jumpSTarget} vs {baseBuilderOff})");
            Check(dealOff > 0 && jumpOverTarget == dealOff,
                $"VoidedStars S-path -> deal ({jumpOverTarget} vs {dealOff})");
        }
    }
    // Blitzer: Lightning byte, 2 builders, speed loop shapes.
    {
        var u = OpenBp("BP_Battle_SkillScript_Verso_Blitzer");
        var fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .First(f => f.ObjectName.ToString() == "OnActionEffect");
        bool lightByte = false, speed = false, trunc = false, greater = false, sub = false;
        int builders = 0;
        E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf)
            {
                var nm = vf.VirtualFunctionName.ToString();
                if (nm == "MakeDamageBuilder")
                {
                    builders++;
                    foreach (var p in vf.Parameters)
                    {
                        if (p is UAssetAPI.Kismet.Bytecode.Expressions.EX_ByteConst bc)
                        {
                            if (bc.Value == 4) lightByte = true;
                            break;
                        }
                    }
                }
                if (nm == "GetCurrentSpeed") speed = true;
            }
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm && cm.StackNode.IsImport())
            {
                var onm = cm.StackNode.ToImport(u).ObjectName.ToString();
                if (onm == "FTrunc") trunc = true;
                if (onm == "Greater_IntInt") greater = true;
                if (onm == "Subtract_DoubleDouble") sub = true;
            }
        });
        Check(lightByte, "Blitzer Lightning damage byte");
        Check(builders == 2, $"Blitzer builders x{builders}");
        Check(speed && trunc && greater && sub, "Blitzer strike-loop shapes");
        // Loop targets: rank/skip/cond jumps -> first stmt past the block,
        // back jump -> the Less_IntInt cond.
        {
            var fns = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
                .First(f => f.ObjectName.ToString() == "OnActionEffect");
            uint off = 0;
            uint condOff = 0, afterBlockOff = 0;
            uint rankT = 0, skipT = 0, condT = 0, backT = 0;
            bool inBlock = false, sawBack = false;
            foreach (var s in fns.ScriptBytecode!)
            {
                if (s is UAssetAPI.Kismet.Bytecode.Expressions.EX_JumpIfNot jn)
                {
                    string math = "";
                    E33ModEditor.Core.MayhemFixKit.Walk(new[] { jn.BooleanExpression }, x =>
                    {
                        if (x is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm && cm.StackNode.IsImport())
                            math = cm.StackNode.ToImport(u).ObjectName.ToString();
                    });
                    if (math == "GreaterEqual_ByteByte") { rankT = jn.CodeOffset; inBlock = true; }
                    else if (math == "Greater_IntInt" && inBlock) skipT = jn.CodeOffset;
                    else if (math == "Less_IntInt" && inBlock) { condT = jn.CodeOffset; condOff = off; }
                }
                if (s is UAssetAPI.Kismet.Bytecode.Expressions.EX_Jump j && inBlock && !sawBack)
                {
                    backT = j.CodeOffset;
                    sawBack = true;
                }
                off += s.GetSize(u);
                if (sawBack && afterBlockOff == 0 && s is UAssetAPI.Kismet.Bytecode.Expressions.EX_Jump)
                    afterBlockOff = off;
            }
            Check(rankT > 0 && rankT == afterBlockOff, $"Blitzer rank jump -> end ({rankT} vs {afterBlockOff})");
            Check(skipT == afterBlockOff, $"Blitzer skip -> end ({skipT} vs {afterBlockOff})");
            Check(condT == afterBlockOff, $"Blitzer cond -> end ({condT} vs {afterBlockOff})");
            Check(backT == condOff, $"Blitzer loop back -> cond ({backT} vs {condOff})");
        }
    }
    Console.WriteLine($"[verso-verify] {checks} checks passed");
}

/// <summary>Re-opens the customKit stage output and asserts the clone contract:
/// new balancing rows, DA SkillScript targets, ST keys, icon rows, Triple Hex
/// buff swaps, and the 7 appended graph nodes + edges.</summary>
static void VerifyScielCustom(string dir)
{
    int checks = 0;
    void Check(bool ok, string msg)
    {
        checks++;
        if (!ok) throw new InvalidDataException("[sciel-custom-verify] FAIL: " + msg);
        Console.WriteLine("[sciel-custom-verify] ok: " + msg);
    }

    var bal = Path.Combine(dir, "Sandfall", "Content", "Gameplay", "Battle",
        "ScalingSystem", "BalancingTables", "Content", "DT_Balancing_MainCharacterSkills.uasset");
    var balDump = new DataTableEditor(bal);
    Check(balDump.TryGetRow("SpectersEdge_BaseDamage", out _), "SpectersEdge_BaseDamage row present");
    Check(balDump.TryGetRow("BlackDeath_BaseDamage", out _), "BlackDeath_BaseDamage row present");
    Check(balDump.TryGetRow("ScielLastChance_APGain", out _), "ScielLastChance_APGain row present");
    Check(balDump.TryGetRow("FalseStart_BaseDamage", out _), "FalseStart_BaseDamage row present");
    Check(balDump.TryGetRow("SoulTransplant_ReviveHP", out _), "SoulTransplant_ReviveHP row present");

    foreach (var stem in new[] { "DA_Skill_Sciel_BlackDeath", "DA_Skill_Sciel_LastChance", "DA_Skill_Sciel_FalseStart", "DA_Skill_Sciel_SpectersEdge", "DA_Skill_Sciel_TripleHex", "DA_Skill_Sciel_CursedFrenzy", "DA_Skill_Sciel_SoulTransplant" })
        Check(FileCopy.FindUnder(dir, stem + ".uasset") != null, stem + " staged");
    foreach (var stem in new[] { "BP_Battle_SkillScript_Sciel_BlackDeath", "BP_Battle_SkillScript_Sciel_TripleHex", "BP_Battle_SkillScript_Sciel_SpectersEdge" })
        Check(FileCopy.FindUnder(dir, stem + ".uasset") != null, stem + " staged");

    var st = FileCopy.FindUnder(dir, "ST_MainCharacters_Skills.uasset");
    Check(st != null, "string table staged");

    var gpath = FileCopy.FindUnder(dir, "DA_SkillGraph_Sciel.uasset");
    Check(gpath != null, "Sciel graph staged");
    var gmap = DataTableEditor.Mappings;
    var g = gmap != null
        ? new UAssetAPI.UAsset(gpath, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, gmap)
        : new UAssetAPI.UAsset(gpath, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    var gx = g.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>().First();
    var gnodes = (UAssetAPI.PropertyTypes.Objects.ArrayPropertyData)gx.Data[0];
    var gedges = (UAssetAPI.PropertyTypes.Objects.ArrayPropertyData)gx.Data[1];
    var skills = new HashSet<string>();
    foreach (var nv in gnodes.Value)
    {
        var su = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)((UAssetAPI.PropertyTypes.Structs.StructPropertyData)nv).Value[0];
        var ptr = ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)su.Value[0]).Value;
        if (ptr is null || !ptr.IsImport()) continue;
        skills.Add(g.Imports[Math.Abs(ptr.Index) - 1].ObjectName.ToString());
    }
    foreach (var key in new[] { "BlackDeath", "LastChance", "FalseStart", "SpectersEdge", "TripleHex", "CursedFrenzy", "SoulTransplant" })
        Check(skills.Contains("DA_Skill_Sciel_" + key), "graph node DA_Skill_Sciel_" + key);
    Console.WriteLine($"[sciel-custom-verify] graph nodes={gnodes.Value.Length} edges={gedges.Value.Length}");
    var baseline = SkillGraphKit.LoadEditorBaseline("DA_SkillGraph_Sciel");
    Check(baseline.Nodes.Count == 38, $"editor baseline nodes={baseline.Nodes.Count} (expect 26+12)");
    var bskills = new HashSet<string>(baseline.Nodes.Select(n => n.Skill), StringComparer.Ordinal);
    Check(bskills.Contains("DA_Skill_Sciel_SoulTransplant"), "baseline contains SoulTransplant");
    Check(bskills.Contains("DA_Skill_Sciel_BlackDeath"), "baseline contains BlackDeath");

    // FalseStart transplant: GreaterSlow spawn + GetCurrentCharacter + ActivateBuff.
    var fsBp = FileCopy.FindUnder(dir, "BP_Battle_SkillScript_Sciel_FalseStart.uasset");
    Check(fsBp != null, "FalseStart BP staged");
    var fsMap = DataTableEditor.Mappings;
    var fsU = fsMap != null
        ? new UAssetAPI.UAsset(fsBp, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, fsMap)
        : new UAssetAPI.UAsset(fsBp, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    var fsFn = fsU.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
        .First(f => f.ObjectName.ToString() == "OnActionEffect");
    bool fsSpawn = false, fsSlow = false, fsGetChar = false, fsActivate = false, fsDur = false;
    E33ModEditor.Core.MayhemFixKit.Walk(fsFn.ScriptBytecode!, e =>
    {
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm && cm.StackNode.IsImport()
            && cm.StackNode.ToImport(fsU).ObjectName.ToString() == "SpawnObject") fsSpawn = true;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_ObjectConst oc && oc.Value.IsImport()
            && (oc.Value.ToImport(fsU).ObjectName.ToString() == "BP_BattleBuff_GreaterSlow_C"
                || oc.Value.ToImport(fsU).ObjectName.ToString() == "BP_BattleBuff_Burn_C")) fsSlow = true;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf)
        {
            if (vf.VirtualFunctionName.ToString() == "GetCurrentCharacter") fsGetChar = true;
            if (vf.VirtualFunctionName.ToString() == "ActivateBuff") fsActivate = true;
        }
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_NameConst nc
            && nc.Value.ToString() == "TurnDuration") fsDur = true;
    });
    Check(fsSpawn, "FalseStart calls SpawnObject");
    Check(fsSlow, "FalseStart references GreaterSlow");
    Check(fsGetChar, "FalseStart calls GetCurrentCharacter");
    Check(fsActivate, "FalseStart calls ActivateBuff");

    // Cursed Frenzy: 11-entry buff array, single-target source, 5-turn durations.
    var cfBp = FileCopy.FindUnder(dir, "BP_Battle_SkillScript_Sciel_CursedFrenzy.uasset");
    Check(cfBp != null, "CursedFrenzy BP staged");
    var cfMap = DataTableEditor.Mappings;
    var cfU = cfMap != null
        ? new UAssetAPI.UAsset(cfBp, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, cfMap)
        : new UAssetAPI.UAsset(cfBp, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    var cfFn = cfU.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
        .First(f => f.ObjectName.ToString() == "OnActionEffect");
    int cfArrayLen = -1, cfDur5 = 0;
    bool cfGetTargeted = false, cfGetAll = false;
    bool cfBurn = false, cfBerserk = false, cfCursed = false;
    E33ModEditor.Core.MayhemFixKit.Walk(cfFn.ScriptBytecode!, e =>
    {
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_SetArray sa
            && sa.Elements.Any(x => x is UAssetAPI.Kismet.Bytecode.Expressions.EX_ObjectConst))
            cfArrayLen = sa.Elements.Length;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf)
        {
            if (vf.VirtualFunctionName.ToString() == "GetTargetedCharacters") cfGetTargeted = true;
            if (vf.VirtualFunctionName.ToString() == "GetAllAliveHeroes") cfGetAll = true;
        }
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_ObjectConst oc && oc.Value.IsImport())
        {
            var onm = oc.Value.ToImport(cfU).ObjectName.ToString();
            if (onm == "BP_BattleBuff_Burn_C") cfBurn = true;
            if (onm == "BP_BattleBuff_Berserk_C") cfBerserk = true;
            if (onm == "BP_BattleBuff_Cursed_C") cfCursed = true;
        }
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ic && ic.Value == 5) cfDur5++;
    });
    Check(cfArrayLen == 11, $"CursedFrenzy buff array x{cfArrayLen} (expect 11)");
    Check(cfGetTargeted, "CursedFrenzy fetches targeted characters");
    Check(!cfGetAll, "CursedFrenzy no longer fetches all heroes");
    Check(cfBurn && cfBerserk && cfCursed, "CursedFrenzy Burn/Berserk/Cursed imports referenced");
    Check(cfDur5 > 0, $"CursedFrenzy 5-turn durations x{cfDur5}");

    // Black Death: self-only filter (Equal, no NotEqual).
    var bdBp = FileCopy.FindUnder(dir, "BP_Battle_SkillScript_Sciel_BlackDeath.uasset");
    Check(bdBp != null, "BlackDeath BP staged");
    var bdMap = DataTableEditor.Mappings;
    var bdU = bdMap != null
        ? new UAssetAPI.UAsset(bdBp, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, bdMap)
        : new UAssetAPI.UAsset(bdBp, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    var bdFn = bdU.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
        .First(f => f.ObjectName.ToString() == "OnActionEffect");
    bool bdEqual = false, bdNotEqual = false;
    E33ModEditor.Core.MayhemFixKit.Walk(bdFn.ScriptBytecode!, e =>
    {
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm && cm.StackNode.IsImport())
        {
            var nm = cm.StackNode.ToImport(bdU).ObjectName.ToString();
            if (nm == "Equal_ObjectObject") bdEqual = true;
            if (nm == "NotEqual_ObjectObject") bdNotEqual = true;
        }
    });
    // DIAGNOSTIC 2026-09-09d resolved: pure clone casts clean, so the
    // Equal-rename was guilty; the inversion is now a native NOT-gate.
    // Expects: NotEqual still called (filter kept) + 2 Not_PreBool calls
    // (native #4 + the gate).
    int bdNot = 0;
    E33ModEditor.Core.MayhemFixKit.Walk(bdFn.ScriptBytecode!, e =>
    {
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm && cm.StackNode.IsImport())
        {
            var nm = cm.StackNode.ToImport(bdU).ObjectName.ToString();
            if (nm == "Equal_ObjectObject") bdEqual = true;
            if (nm == "NotEqual_ObjectObject") bdNotEqual = true;
            if (nm == "Not_PreBool") bdNot++;
        }
    });
    Check(!bdEqual, "BlackDeath calls no renamed equality function");
    Check(bdNotEqual, "BlackDeath keeps native NotEqual filter");
    Check(bdNot == 2, $"BlackDeath NOT-gate present (Not_PreBool x{bdNot})");
    // No TurnDuration in the v1-shape block (Greater Slow default duration
    // proved working when the transplant first landed slow on self).

    // Last Chance: single Powerful/Rush/Berserk (1 turn) + Cursed (2),
    // 5 AP gift, 4 AP cost, NO self-nuke (DealDamages can't touch friendlies).
    var lcBp = FileCopy.FindUnder(dir, "BP_Battle_SkillScript_Sciel_LastChance.uasset");
    Check(lcBp != null, "LastChance BP staged");
    var lcMap = DataTableEditor.Mappings;
    var lcU = lcMap != null
        ? new UAssetAPI.UAsset(lcBp, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, lcMap)
        : new UAssetAPI.UAsset(lcBp, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    var lcFn = lcU.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
        .First(f => f.ObjectName.ToString() == "OnActionEffect");
    int lcSpawn = 0, lcActivate = 0, lcDur1 = 0, lcDur2 = 0;
    bool lcPower = false, lcSpeed = false, lcBerserk = false, lcCursed = false;
    bool lcBuilder = false, lcDeal = false;
    E33ModEditor.Core.MayhemFixKit.Walk(lcFn.ScriptBytecode!, e =>
    {
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm && cm.StackNode.IsImport()
            && cm.StackNode.ToImport(lcU).ObjectName.ToString() == "SpawnObject") lcSpawn++;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf)
        {
            if (vf.VirtualFunctionName.ToString() == "ActivateBuff") lcActivate++;
            if (vf.VirtualFunctionName.ToString() == "MakeDamageBuilder") lcBuilder = true;
            if (vf.VirtualFunctionName.ToString() == "DealDamages") lcDeal = true;
        }
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_ObjectConst oc && oc.Value.IsImport())
        {
            var onm = oc.Value.ToImport(lcU).ObjectName.ToString();
            if (onm == "BP_BattleBuff_Powerful_125_C") lcPower = true;
            if (onm == "BP_BattleBuff_Speed_C") lcSpeed = true;
            if (onm == "BP_BattleBuff_Berserk_C") lcBerserk = true;
            if (onm == "BP_BattleBuff_Cursed_C") lcCursed = true;
        }
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ic && ic.Value == 1) lcDur1++;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ic2 && ic2.Value == 2) lcDur2++;
    });
    Check(lcSpawn == 4, $"LastChance buff spawns x{lcSpawn} (expect 4)");
    Check(lcActivate == 4, $"LastChance buff activates x{lcActivate} (expect 4)");
    Check(lcPower && lcSpeed && lcBerserk && lcCursed, "LastChance Powerful/Rush/Berserk/Cursed imports referenced");
    Check(lcDur2 == 1, $"LastChance curse duration x{lcDur2} (expect 1)");
    Check(lcDur1 == 5, $"LastChance 1-turn durations x{lcDur1} (expect 2 donor + 3 buffs)");
    Check(!lcBuilder, "LastChance has no damage builder");
    Check(!lcDeal, "LastChance deals no damage");
    Check(WaveApCost("DA_Skill_Sciel_LastChance") == 4, "LastChance APCost = 4");
    Check(Math.Abs(BalCell("ScielLastChance_APGain") - 5f) < 0.001f, "LastChance AP gift = 5");

    // Soul Transplant: revive block (Resurrect + Cursed + manager fetch, flat-HP baked, no damage, no foretell cost).
    var stBp = FileCopy.FindUnder(dir, "BP_Battle_SkillScript_Sciel_SoulTransplant.uasset");
    Check(stBp != null, "SoulTransplant BP staged");
    var stMap = DataTableEditor.Mappings;
    var stU = stMap != null
        ? new UAssetAPI.UAsset(stBp, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, stMap)
        : new UAssetAPI.UAsset(stBp, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    var stFn = stU.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
        .First(f => f.ObjectName.ToString() == "OnActionEffect");
    bool stRes = false, stAct = false, stMgr = false, stPct = false, stCurse = false, stGate = false;
    E33ModEditor.Core.MayhemFixKit.Walk(stFn.ScriptBytecode!, e =>
    {
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf)
        {
            if (vf.VirtualFunctionName.ToString() == "Resurrect") stRes = true;
            if (vf.VirtualFunctionName.ToString() == "ActivateBuff") stAct = true;
            if (vf.VirtualFunctionName.ToString() == "GetBattleManager") stMgr = true;
        }
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst dc && dc.Value == 1.0) stPct = true;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_ObjectConst oc && oc.Value.IsImport()
            && oc.Value.ToImport(stU).ObjectName.ToString() == "BP_BattleBuff_Cursed_C") stCurse = true;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_JumpIfNot) stGate = true;
    });
    Check(stRes, "SoulTransplant calls Resurrect");
    Check(stAct, "SoulTransplant calls ActivateBuff");
    Check(stMgr, "SoulTransplant fetches BattleManager");
    Check(stPct, "SoulTransplant revive HP baked (1)");
    Check(stCurse, "SoulTransplant references Cursed");
    // Link-chain: class-outered function imports must keep their outer
    // (flattened outers CTD on execute). SpawnObject -> GameplayStatics.
    bool stSpawnOk = false;
    E33ModEditor.Core.MayhemFixKit.Walk(stFn.ScriptBytecode!, e =>
    {
        UAssetAPI.UnrealTypes.FPackageIndex? sn = null;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm) sn = cm.StackNode;
        else if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_FinalFunction ff && ff.StackNode is { } s2) sn = s2;
        if (sn is null || !sn.IsImport()) return;
        var nm = sn.ToImport(stU).ObjectName.ToString();
        var outer = sn.ToImport(stU).OuterIndex;
        var outerNm = outer is { } oi && oi.IsImport() ? oi.ToImport(stU).ObjectName.ToString() : null;
        if (nm == "SpawnObject" && outerNm == "GameplayStatics") stSpawnOk = true;
    });
    Check(stSpawnOk, "SoulTransplant SpawnObject links via GameplayStatics");
    // Curse receiver must be a stats READ, not a bare temp: cross-asset
    // cloned local pointers resolve to garbage in our name map.
    bool stRecvOk = false;
    E33ModEditor.Core.MayhemFixKit.Walk(stFn.ScriptBytecode!, e =>
    {
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_Context cx)
        {
            bool hasAct = false;
            E33ModEditor.Core.MayhemFixKit.Walk(new[] { cx }, x =>
            {
                if (x is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf
                    && vf.VirtualFunctionName.ToString() == "ActivateBuff") hasAct = true;
            });
            if (hasAct && cx.ObjectExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_Context) stRecvOk = true;
        }
    });
    Check(stRecvOk, "SoulTransplant curse receiver is a stats read");
    // ── Wave-2 customs (Ultima Path / Bargain / Second Life / Phantom Sever / Corruption) ──
    float BalCell(string rowName)
    {
        if (!balDump.TryGetRow(rowName, out var row)) throw new KeyNotFoundException(rowName);
        foreach (var leaf in balDump.FlattenRow(row))
            if (leaf.Path.EndsWith("FloatValue", StringComparison.Ordinal))
                return float.Parse(leaf.Value.ToString(), System.Globalization.CultureInfo.InvariantCulture);
        throw new KeyNotFoundException(rowName + ".FloatValue");
    }
    UAssetAPI.UAsset? WaveAsset(string bpStem, out UAssetAPI.ExportTypes.FunctionExport? fn)
    {
        fn = null;
        var p = FileCopy.FindUnder(dir, bpStem + ".uasset");
        Check(p != null, $"{bpStem} staged");
        if (p is null) return null;
        var m = DataTableEditor.Mappings;
        var u = m != null
            ? new UAssetAPI.UAsset(p, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, m)
            : new UAssetAPI.UAsset(p, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
        fn = u.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>()
            .First(f => f.ObjectName.ToString() == "OnActionEffect");
        return u;
    }
    UAssetAPI.ExportTypes.FunctionExport? WaveFn(string bpStem)
        => WaveAsset(bpStem, out var fn) is null ? null : fn;
    int WaveApCost(string daStem)
    {
        var da = Directory.EnumerateFiles(dir, daStem + ".uasset", SearchOption.AllDirectories).FirstOrDefault();
        Check(da != null, $"{daStem} staged");
        if (da is null) return -1;
        var ed = DataTableEditor.OpenPropertyAsset(da);
        return ed.FlattenProperties().First(l => l.Path == "APCost").Value.ToObject<int>();
    }
    // Ultima Path: foretell row 10 applied twice (2x10 capped applies), 21 AP.
    Check(Math.Abs(BalCell("UltimaPath_BaseForetell") - 10f) < 0.001f, "UltimaPath foretell row = 10");
    var upFn = WaveFn("BP_Battle_SkillScript_Sciel_UltimaPath");
    int upForetell = 0;
    if (upFn != null) foreach (var s in upFn.ScriptBytecode!)
        E33ModEditor.Core.MayhemFixKit.Walk(new[] { s }, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf
                && vf.VirtualFunctionName.ToString() == "ApplyForetell") upForetell++;
        });
    Check(upForetell == 2, $"UltimaPath applies foretell twice (got {upForetell})");
    bool upTwi = false;
    if (upFn != null) E33ModEditor.Core.MayhemFixKit.Walk(upFn.ScriptBytecode!, e =>
    {
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf
            && vf.VirtualFunctionName.ToString() == "TryChangeActiveTwilightModeDuration") upTwi = true;
    });
    Check(upTwi, "UltimaPath extends Twilight duration");
    Check(WaveApCost("DA_Skill_Sciel_UltimaPath") == 21, "UltimaPath APCost = 21");
    // Bargain: StolenHP x5, drain at 1 HP (damage safety), Maelle-style
    // self-Curse kill (spawn + TurnDuration 1 + ActivateBuff), 20 AP.
    Check(Math.Abs(BalCell("UltimasBargain_StolenHPMultiplier") - 5f) < 0.001f, "Bargain StolenHP x5");
    var ubU = WaveAsset("BP_Battle_SkillScript_Sciel_UltimasBargain", out var ubFn);
    bool ubDrain1 = false, ubSpawn = false, ubAct = false, ubCurseImp = false, ubT1 = false;
    if (ubFn != null) foreach (var s in ubFn.ScriptBytecode!)
    {
        bool setHP = false, one = false;
        E33ModEditor.Core.MayhemFixKit.Walk(new[] { s }, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf
                && vf.VirtualFunctionName.ToString().Contains("SetHP")) setHP = true;
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst dc && dc.Value == 1.0) one = true;
        });
        if (setHP && one) ubDrain1 = true;
    }
    if (ubFn != null) E33ModEditor.Core.MayhemFixKit.Walk(ubFn.ScriptBytecode!, e =>
    {
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_FinalFunction ff && ff.StackNode is { } sn && sn.IsImport()
            && ubU != null && sn.ToImport(ubU).ObjectName.ToString() == "SpawnObject") ubSpawn = true;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_VirtualFunction vf
            && vf.VirtualFunctionName.ToString() == "ActivateBuff") ubAct = true;
    });
    if (ubFn != null) foreach (var s in ubFn.ScriptBytecode!)
    {
        bool setInt = false, tname = false, tone = false;
        E33ModEditor.Core.MayhemFixKit.Walk(new[] { s }, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_FinalFunction ff && ff.StackNode is { } sn2 && sn2.IsImport()
                && ubU != null && sn2.ToImport(ubU).ObjectName.ToString() == "SetIntPropertyByName") setInt = true;
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_NameConst nc
                && nc.Value.ToString() == "TurnDuration") tname = true;
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ic && ic.Value == 1) tone = true;
        });
        if (setInt && tname && tone) ubT1 = true;
    }
    if (ubU != null) foreach (var imp in ubU.Imports)
    {
        if (imp.ObjectName.ToString() == "BP_BattleBuff_Cursed_C") ubCurseImp = true;
        if (imp.ObjectName.ToString() == "SpawnObject" && imp.OuterIndex is { } oi && oi.IsImport()
            && oi.ToImport(ubU).ObjectName.ToString() == "GameplayStatics") ubSpawn = true;
    }
    Check(ubDrain1, "Bargain drain at 1 HP");
    Check(ubSpawn, "Bargain spawns via GameplayStatics");
    Check(ubAct, "Bargain calls ActivateBuff (self-Curse)");
    Check(ubCurseImp, "Bargain imports BP_BattleBuff_Cursed_C");
    Check(ubT1, "Bargain TurnDuration = 1");
    Check(WaveApCost("DA_Skill_Sciel_UltimasBargain") == 20, "Bargain APCost = 20");
    // Second Life: Aureole via the native chain (spawn/99-turn/activate),
    // NO ApplyBuff LVF (executed but never took cross-character), curse 7,
    // NO revive, live-ally targeting, 17 AP.
    var slU = WaveAsset("BP_Battle_SkillScript_Sciel_SecondLife", out var slFn);
    bool slAurCls = false, slAct = false, slT99 = false, slT7 = false, slAurImp = false, slRes = false, slLvf = false;
    if (slFn != null && slU != null) E33ModEditor.Core.MayhemFixKit.Walk(slFn.ScriptBytecode!, e =>
    {
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf
            && vf.VirtualFunctionName.ToString() == "ApplyBuff") slLvf = true;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vr
            && vr.VirtualFunctionName.ToString() == "Resurrect") slRes = true;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction va
            && va.VirtualFunctionName.ToString() == "ActivateBuff") slAct = true;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_ObjectConst oc && oc.Value.IsImport()
            && oc.Value.ToImport(slU).ObjectName.ToString() == "BP_LU_aureole_C") slAurCls = true;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ic && ic.Value == 7) slT7 = true;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ic99 && ic99.Value == 99) slT99 = true;
    });
    if (slU != null) foreach (var imp in slU.Imports)
        if (imp.ObjectName.ToString() == "BP_LU_aureole_C") slAurImp = true;
    Check(!slLvf, "SecondLife no longer uses ApplyBuff LVF");
    Check(slAurCls, "SecondLife spawns BP_LU_aureole_C");
    Check(slAct, "SecondLife calls ActivateBuff");
    Check(slT99, "SecondLife Aureole TurnDuration = 99");
    Check(slAurImp, "SecondLife imports BP_LU_aureole_C");
    Check(slT7, "SecondLife TurnDuration = 7");
    Check(!slRes, "SecondLife has no Resurrect");
    Check(WaveApCost("DA_Skill_Sciel_SecondLife") == 17, "SecondLife APCost = 17");
    var slDa = Directory.EnumerateFiles(dir, "DA_Skill_Sciel_SecondLife.uasset", SearchOption.AllDirectories).FirstOrDefault();
    Check(slDa != null, "SecondLife DA staged");
    if (slDa != null)
    {
        var slDaEd = DataTableEditor.OpenPropertyAsset(slDa);
        var slTgt = slDaEd.FlattenProperties().First(l => l.Path == "TargetingType").Value.ToString();
        Check(slTgt == "NewEnumerator2", $"SecondLife targets living allies (got {slTgt})");
    }
    // Phantom Sever: crit gate + per-crit foretell + rows, 15 AP.
    Check(Math.Abs(BalCell("PhantomSever_ForetellPerCrit") - 2f) < 0.001f, "PhantomSever per-crit foretell = 2");
    var psFn = WaveFn("BP_Battle_SkillScript_Sciel_PhantomSever");
    bool psCrit = false, psApply = false;
    if (psFn != null) E33ModEditor.Core.MayhemFixKit.Walk(psFn.ScriptBytecode!, e =>
    {
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_InstanceVariable iv
            && iv.Variable?.New?.Path?.Any(n => n.ToString() == "IsCriticalHit") == true) psCrit = true;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf
            && vf.VirtualFunctionName.ToString() == "ApplyForetell") psApply = true;
    });
    Check(psCrit, "PhantomSever reads IsCriticalHit");
    Check(psApply, "PhantomSever calls ApplyForetell");
    Check(WaveApCost("DA_Skill_Sciel_PhantomSever") == 15, "PhantomSever APCost = 15");
    // Corruption: moon charges inherited, foretell scaling row, 21 AP.
    Check(Math.Abs(BalCell("Corruption_DamageIncreasePerForetell") - 0.05f) < 0.001f, "Corruption per-foretell = 0.05");
    var coFn = WaveFn("BP_Battle_SkillScript_Sciel_Corruption");
    bool coMoon = false;
    if (coFn != null) E33ModEditor.Core.MayhemFixKit.Walk(coFn.ScriptBytecode!, e =>
    {
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf
            && vf.VirtualFunctionName.ToString() == "AddMoonCharges") coMoon = true;
    });
    Check(coMoon, "Corruption adds Moon charges");
    Check(WaveApCost("DA_Skill_Sciel_Corruption") == 21, "Corruption APCost = 21");
    var coDa = Directory.EnumerateFiles(dir, "DA_Skill_Sciel_Corruption.uasset", SearchOption.AllDirectories).FirstOrDefault();
    Check(coDa != null, "Corruption DA staged");
    if (coDa != null)
    {
        var coEd = DataTableEditor.OpenPropertyAsset(coDa);
        var coType = coEd.FlattenProperties().First(l => l.Path == "SkillType").Value.ToString();
        Check(coType == "NewEnumerator1", $"Corruption SkillType = regular (got {coType})");
    }
    // Corruption's battle-long scaling needs the vanilla counter buff: the
    // ("EndSlice"->"Corruption") clone retarget once ghosted it into
    // BP_Buff_CorruptionForetellConsumedCount (dangling BuffClass -> x1 stuck).
    foreach (var stem in new[] { "DA_Skill_Sciel_Corruption", "BP_Battle_SkillScript_Sciel_Corruption" })
    {
        var p = Directory.EnumerateFiles(dir, stem + ".uasset", SearchOption.AllDirectories).FirstOrDefault();
        Check(p != null, $"{stem} staged for buff-ref check");
        if (p != null)
        {
            var raw = System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(p));
            Check(raw.Contains("EndSliceForetellConsumedCount"), $"{stem} keeps vanilla counter-buff ref");
            Check(!raw.Contains("CorruptionForetellConsumedCount"), $"{stem} has no ghost buff ref");
        }
    }
    var stDaPath = FileCopy.FindUnder(dir, "DA_Skill_Sciel_SoulTransplant.uasset")!;
    var stDaMap = DataTableEditor.Mappings;
    var stDa = stDaMap != null
        ? new UAsset(stDaPath, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, stDaMap)
        : new UAsset(stDaPath, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    bool stDeadTarget = false;
    foreach (var ex in stDa.Exports.OfType<NormalExport>())
        foreach (var p in ex.Data)
            if (p is UAssetAPI.PropertyTypes.Objects.EnumPropertyData e
                && e.Name.ToString() == "TargetingType"
                && e.Value.ToString() == "NewEnumerator12") stDeadTarget = true;
    Check(stDeadTarget, "SoulTransplant targets dead allies (NewEnumerator12)");
    // (gate-jump assert omitted: donor has native JumpIfNots)
    Console.WriteLine($"[sciel-custom-verify] {checks} checks passed");
}

/// <summary>
/// Rebuilds work/mod from vanilla tables with every module the GUI pack
/// applies (pictos, weapon effects, gameplay tweaks, randomizer),
/// reading the same settings the GUI persists (work/gameplay_tweaks.json,
/// work/randomizer.json), then packs Export/ExpeditionInfinity_<seed>_P into ~mods. This is
/// the CLI equivalent of the GUI's "Apply &amp; Pack" and lets a repack pick up
/// code fixes without launching the app.
/// </summary>
static int CmdBuildWeaponOverlevel(string[] args)
{
    var written = E33ModEditor.Core.WeaponOverlevelingKit.Build();
    Console.WriteLine($"[weapon-overlevel] {written.Count} file(s) installed to ~mods as {E33ModEditor.Core.WeaponOverlevelingKit.ModName}");
    return 0;
}

/// <summary>Stages the standalone weapon over-levelling mod into a scratch dir
/// and runs its Verify() — no game files, no ~mods install.</summary>
static int CmdWeaponOverlevelSmoke(string[] args)
{
    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "standalone", "weapon_overlevel", "smoke");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);
    int n = E33ModEditor.Core.WeaponOverlevelingKit.Apply(dir);
    Console.WriteLine($"[weapon-overlevel-smoke] applied {n} edit(s)");
    E33ModEditor.Core.WeaponOverlevelingKit.Verify(dir);
    Console.WriteLine("[weapon-overlevel-smoke] PASSED");
    return 0;
}

/// <summary>Bulk-upgrade splice smoke: copies the vanilla Camp weapon-upgrade
/// widget into a scratch dir, runs WeaponBulkUpgradeKit.Apply + Verify, then
/// re-applies to prove idempotence. No game files, no ~mods install.</summary>
static int CmdBulkSmoke(string[] args)
{
    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_bulksmoke");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);
    // Optional arg: widget under test (defaults to the vanilla tables copy, so
    // the splice mechanics are tested; pass the overleveled work/mod copy to
    // prove pack-order compatibility after WeaponOverLevelKit.Apply).
    var src = args.Length > 1 ? Path.GetFullPath(args[1])
        : FileCopy.FindTablesAsset("WBP_BS_WeaponUpgrade.uasset")
        ?? throw new FileNotFoundException("WBP_BS_WeaponUpgrade.uasset not in work/tables");
    FileCopy.AssetPair(src, Path.Combine(dir, "WBP_BS_WeaponUpgrade.uasset"));
    int n = E33ModEditor.Core.WeaponBulkUpgradeKit.Apply(dir);
    Console.WriteLine($"[bulk-smoke] applied {n}");
    if (n != 1) { Console.Error.WriteLine("[bulk-smoke] FAILED: want exactly 1 (widget asset)"); return 1; }
    E33ModEditor.Core.WeaponBulkUpgradeKit.Verify(dir);
    int n2 = E33ModEditor.Core.WeaponBulkUpgradeKit.Apply(dir);
    Console.WriteLine($"[bulk-smoke] re-apply returned {n2}");
    if (n2 != 0) { Console.Error.WriteLine("[bulk-smoke] FAILED: re-apply is not idempotent"); return 1; }
    E33ModEditor.Core.WeaponBulkUpgradeKit.Verify(dir);
    Console.WriteLine("[bulk-smoke] PASSED");
    return 0;
}

/// <summary>Weapon-only drop-level smoke: stages a scratch copy, applies the
/// MaxDropLevel 222 scale to both gear paths and the BattleManager reward
/// pre-filter, verifies, then re-applies to prove idempotence. No game files,
/// no ~mods install.</summary>
static int CmdDropSmoke(string[] args)
{
    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_dropsmoke");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);

    var settings = new E33ModEditor.Core.WeaponDropLevelKit.Settings
    {
        Enabled = true,
        MaxDropLevel = 222,
    };
    int n = E33ModEditor.Core.WeaponDropLevelKit.Apply(dir, settings);
    Console.WriteLine($"[drop-smoke] applied {n}");
    if (n != 3)
    {
        Console.Error.WriteLine("[drop-smoke] FAILED: want 3 edits (GenerateItem, ChangeLevelWithLootContext, BattleManager pre-filter)");
        return 1;
    }
    E33ModEditor.Core.WeaponDropLevelKit.Verify(dir, settings);

    int n2 = E33ModEditor.Core.WeaponDropLevelKit.Apply(dir, settings);
    Console.WriteLine($"[drop-smoke] re-apply returned {n2}");
    if (n2 != 0)
    {
        Console.Error.WriteLine("[drop-smoke] FAILED: re-apply is not idempotent");
        return 1;
    }
    E33ModEditor.Core.WeaponDropLevelKit.Verify(dir, settings);

    // MaxDropLevel 33 must be a true no-op: no assets staged at all.
    var vanillaDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_dropsmoke_vanilla");
    if (Directory.Exists(vanillaDir)) Directory.Delete(vanillaDir, true);
    Directory.CreateDirectory(vanillaDir);
    var vanilla = new E33ModEditor.Core.WeaponDropLevelKit.Settings
    {
        Enabled = true,
        MaxDropLevel = 33,
    };
    int n3 = E33ModEditor.Core.WeaponDropLevelKit.Apply(vanillaDir, vanilla);
    if (n3 != 0 || Directory.EnumerateFiles(vanillaDir, "*", SearchOption.AllDirectories).Any())
    {
        Console.Error.WriteLine("[drop-smoke] FAILED: MaxDropLevel 33 staged assets");
        return 1;
    }
    Console.WriteLine("[drop-smoke] PASSED");
    return 0;
}

static int CmdLocLanguages(string[] args)
{
    foreach (var l in E33ModEditor.Core.Localization.ModLocalization.Supported)
        Console.WriteLine($"{l.Code,-3} {l.Culture,-7} {l.DisplayName}");
    return 0;
}

static int CmdLocCatalog(string[] args)
{
    var outDir = Path.Combine(EditorPaths.RepoRoot, "work", "localization", "generated");
    for (var i = 1; i < args.Length; i++)
        if (args[i] is "--out" or "-o" && i + 1 < args.Length) outDir = args[++i];
    E33ModEditor.Core.Localization.LocalizationKit.BuildCatalogs(
        Path.Combine(EditorPaths.RepoRoot, "work", "mod"), outDir, Console.WriteLine);
    return 0;
}

static int CmdLocApply(string[] args)
{
    var language = E33ModEditor.Core.Localization.LocalizationKit.LoadLanguage();
    for (var i = 1; i < args.Length; i++)
        if (args[i] is "--language" or "-l")
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--language needs a value (en, fr, pl, ru)");
                return 2;
            }
            var found = E33ModEditor.Core.Localization.ModLocalization.Find(args[++i]);
            if (found is null)
            {
                Console.Error.WriteLine("unsupported language; use en, fr, pl or ru");
                return 2;
            }
            language = found.Code;
        }
    if (E33ModEditor.Core.Localization.ModLocalization.Find(language)?.Code == "en")
    {
        Console.WriteLine("English is the source language; no locres is written.");
        return 0;
    }
    try
    {
        var path = E33ModEditor.Core.Localization.LocalizationKit.Apply(
            Path.Combine(EditorPaths.RepoRoot, "work", "mod"), language, Console.WriteLine);
        Console.WriteLine(path);
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static int CmdLocValidate(string[] args)
{
    var language = E33ModEditor.Core.Localization.LocalizationKit.LoadLanguage();
    for (var i = 1; i < args.Length; i++)
        if (args[i] is "--language" or "-l")
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--language needs a value (en, fr, pl, ru)");
                return 2;
            }
            var found = E33ModEditor.Core.Localization.ModLocalization.Find(args[++i]);
            if (found is null)
            {
                Console.Error.WriteLine("unsupported language; use en, fr, pl or ru");
                return 2;
            }
            language = found.Code;
        }
    E33ModEditor.Core.Localization.ModLocalization.SetLanguage(language);
    var issues = E33ModEditor.Core.Localization.LocalizationKit.Validate(language);
    if (issues.Count == 0)
    {
        Console.WriteLine($"{language}: no structural issues");
        return 0;
    }
    Console.WriteLine($"{language}: {issues.Count} issue(s)");
    foreach (var issue in issues) Console.WriteLine("  " + issue);
    return issues.Count == 0 ? 0 : 1;
}

static int CmdLocSmoke(string[] args)
{
    var failures = 0;
    Console.WriteLine($"repak: {(File.Exists(Path.Combine(EditorPaths.ToolsDir, "repak", "repak")) ? "ok" : "MISSING")}");
    if (!File.Exists(Path.Combine(EditorPaths.ToolsDir, "repak", "repak"))) failures++;
    foreach (var language in E33ModEditor.Core.Localization.ModLocalization.Supported)
    {
        E33ModEditor.Core.Localization.ModLocalization.ResetCache();
        E33ModEditor.Core.Localization.ModLocalization.SetLanguage(language.Code);
        var en = E33ModEditor.Core.Localization.ModLocalization.TableValues("en");
        var tr = E33ModEditor.Core.Localization.ModLocalization.TableValues(language.Code);
        var missing = en.Keys.Count(k => !tr.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v));
        var issues = E33ModEditor.Core.Localization.LocalizationKit.Validate(language.Code);
        var bad = issues.Count(i => !i.EndsWith(": unchanged", StringComparison.Ordinal));
        Console.WriteLine($"{language.Code,-3} {language.Culture,-7} entries={en.Count} missing={missing} structural={bad}");
        if (missing > 0 || bad > 0) failures++;
    }
    Console.WriteLine(failures == 0 ? "loc-smoke: ok" : $"loc-smoke: {failures} failure(s)");
    return failures == 0 ? 0 : 1;
}

static int CmdRebuildMod(string[] args)
{
    var language = E33ModEditor.Core.Localization.LocalizationKit.LoadLanguage();
    string? revision = null;
    for (var i = 1; i < args.Length; i++)
    {
        if (args[i] is "--language" or "-l")
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--language needs a value (en, fr, pl, ru)");
                return 2;
            }
            var requested = E33ModEditor.Core.Localization.ModLocalization.Find(args[++i]);
            if (requested is null)
            {
                Console.Error.WriteLine("unsupported language; use en, fr, pl or ru");
                return 2;
            }
            language = requested.Code;
        }
        else if (args[i] is "--revision" or "-r")
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--revision needs a value (e.g. _v1.3)");
                return 2;
            }
            revision = args[++i];
        }
    }
    E33ModEditor.Core.Localization.LocalizationKit.SaveLanguage(language);
    Console.WriteLine($"[rebuild-mod] language: {E33ModEditor.Core.Localization.LocalizationKit.CultureFor(language)}");

    var tweaks = new GameplayTweaksKit.Settings();
    var tweaksPath = Path.Combine(EditorPaths.RepoRoot, "work", "gameplay_tweaks.json");
    if (File.Exists(tweaksPath))
        tweaks.FromJson(Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(tweaksPath)));

    var rando = new E33ModEditor.Core.Loot.RandomizerKit.Settings();
    var randoPath = Path.Combine(EditorPaths.RepoRoot, "work", "randomizer.json");
    if (File.Exists(randoPath))
        Newtonsoft.Json.JsonConvert.PopulateObject(File.ReadAllText(randoPath), rando);
    // The rare endgame Perfect Catalyst is on by default (Settings initializer);
    // a stale work/randomizer.json that predates the field leaves it at 1.
    Console.WriteLine($"[rebuild-mod] randomizer: seed {rando.Seed}, enemy drops {rando.EnemyDrops}, " +
        $"rare Perfect catalyst {rando.T5PerfectCatalystChance}% (T5 Lv{E33ModEditor.Core.Loot.RandomizerKit.PerfectCatalystMinLevel}+ regulars)");

    var compat = new CompatibilityKit.Settings();
    var compatPath = Path.Combine(EditorPaths.RepoRoot, "work", "compatibility.json");
    if (File.Exists(compatPath))
        compat.FromJson(Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(compatPath)));

    var result = E33ModEditor.Core.Packer.FullPackKit.RebuildAndPack(new()
    {
        Tweaks = tweaks,
        FirstPerson = FirstPersonCameraKit.Settings.Load(),
        PartyFive = PartyFiveKit.Settings.Load(),
        Randomizer = rando,
        Compatibility = compat,
        Language = language,
        PackRevisionOverride = revision,
        Log = msg => Console.WriteLine("[rebuild-mod] " + msg),
    });
    Console.WriteLine("[rebuild-mod] staged " + result.Edits + $" asset edits; {E33ModEditor.Core.Packer.FullPackKit.ModNameFor(result.Seed, revision)} installed (seed {result.Seed}):");
    foreach (var f in result.PackedFiles) Console.WriteLine("       " + f);
    return 0;
}

/// <summary>Build the single-file spoiler codex from the current work/mod stage
/// (or the vanilla tables when work/mod has no staged copies). Used to test
/// the generator without repacking; Apply &amp; Pack / rebuild-mod do this
/// automatically for the seed they just packed.</summary>
static int CmdSpoilerBuild(string[] args)
{
    var seed = args.Length > 1 && int.TryParse(args[1], out var explicitSeed)
        ? explicitSeed
        : CurrentSeed();
    string? output = null;
    var modDir = Path.Combine(EditorPaths.RepoRoot, "work", "mod");
    string? edition = null, editionNote = null, revision = null;
    for (var i = 1; i < args.Length; i++)
    {
        if (args[i] == "--out" && i + 1 < args.Length)
            output = Path.GetFullPath(args[++i]);
        else if (args[i] == "--mod-dir" && i + 1 < args.Length)
            modDir = Path.GetFullPath(args[++i]);
        else if (args[i] == "--edition" && i + 1 < args.Length)
            edition = args[++i];
        else if (args[i] == "--edition-note" && i + 1 < args.Length)
            editionNote = args[++i];
        else if (args[i] == "--revision" && i + 1 < args.Length)
            revision = args[++i];
    }

    var catalog = new E33ModEditor.Core.Loot.LootCatalog();
    var chestsPath = FileCopy.FindTablesAsset("DT_ChestsContent.uasset")
        ?? throw new FileNotFoundException("DT_ChestsContent.uasset");
    var enemiesPath = FileCopy.FindTablesAsset("DT_jRPG_Enemies.uasset")
        ?? throw new FileNotFoundException("DT_jRPG_Enemies.uasset");
    var merchants = new List<(string, JObject)>();
    foreach (var file in Directory.EnumerateFiles(
                 Path.Combine(EditorPaths.RepoRoot, "work", "tables"),
                 "DT_Merchant_*.uasset", SearchOption.AllDirectories))
    {
        try { merchants.Add((file, JObject.Parse(new DataTableEditor(file).Dump()))); }
        catch { /* skip broken extract */ }
    }
    catalog.Rebuild(
        JObject.Parse(new DataTableEditor(chestsPath).Dump()),
        JObject.Parse(new DataTableEditor(enemiesPath).Dump()),
        merchants);

    var path = SpoilerCodexKit.Build(
        modDir,
        seed,
        catalog,
        Console.WriteLine,
        output,
        packRevision: revision,
        edition: edition,
        editionNote: editionNote);
    Console.WriteLine($"[spoiler-build] seed {seed}: {path}");
    return 0;
}

static int CmdFirstPerson(string[] args)
{
    var s = FirstPersonCameraKit.Settings.Load();
    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i].ToLowerInvariant())
        {
            case "--on": s.Enabled = true; break;
            case "--off": s.Enabled = false; break;
            case "--arm" when i + 1 < args.Length:
                s.ArmLength = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--eye" when i + 1 < args.Length:
                s.EyeLift = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--pivot-drop" when i + 1 < args.Length:
                s.PivotDrop = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--forward" when i + 1 < args.Length:
                s.ForwardOffset = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--fov" when i + 1 < args.Length:
                s.FieldOfView = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--aim-fov" when i + 1 < args.Length:
                s.AimFieldOfView = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--aim-side" when i + 1 < args.Length:
                s.AimSideOffset = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--maelle-aim-side" when i + 1 < args.Length:
                s.MaelleAimSide = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--maelle-aim-forward" when i + 1 < args.Length:
                s.MaelleAimForward = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--monoco-aim-forward" when i + 1 < args.Length:
                s.MonocoAimForward = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--monoco-aim-lift" when i + 1 < args.Length:
                s.MonocoAimLift = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--lune-aim-lift" when i + 1 < args.Length:
                s.LuneAimLift = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--sciel-aim-forward" when i + 1 < args.Length:
                s.ScielAimForward = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--sciel-aim-lift" when i + 1 < args.Length:
                s.ScielAimLift = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--verso-aim-lift" when i + 1 < args.Length:
                s.VersoAimLift = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--aim-fp": s.AimFirstPerson = true; break;
            case "--no-aim-fp": s.AimFirstPerson = false; break;
            case "--monoco-forward" when i + 1 < args.Length:
                s.MonocoExploreForward = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--monoco-arm" when i + 1 < args.Length:
                s.MonocoExploreArm = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--monoco-side" when i + 1 < args.Length:
                s.MonocoExploreSide = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--explore-side" when i + 1 < args.Length:
                s.ExploreSideOffset = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--preset" when i + 1 < args.Length:
            {
                var preset = args[++i].ToLowerInvariant();
                if (preset == "overshoulder")
                {
                    // Classic over-the-right-shoulder: short boom, head-height
                    // pivot, lens right of the spine, lag back on, vanilla FOV.
                    s.ArmLength = 130f;
                    s.PivotDrop = 14f;
                    s.ForwardOffset = 50f;
                    s.ExploreSideOffset = 35f;
                    s.FieldOfView = 85f;
                    s.DisableLag = false;
                }
                else if (preset is "firstperson" or "fp")
                {
                    s.ArmLength = 0f;
                    s.PivotDrop = 28f;
                    s.ForwardOffset = 38f;
                    s.ExploreSideOffset = 0f;
                    s.FieldOfView = 90f;
                    s.DisableLag = true;
                }
                else { Console.WriteLine("unknown preset: " + preset); return 1; }
                break;
            }
            case "--verso-forward" when i + 1 < args.Length:
                s.VersoExploreForward = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--climb-arm" when i + 1 < args.Length:
                s.ClimbArmLength = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--climb-scale" when i + 1 < args.Length:
                s.ClimbArmScale = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--worldmap-aperture" when i + 1 < args.Length:
                s.WorldMapAperture = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--dof": s.DepthOfField = true; break;
            case "--no-dof": s.DepthOfField = false; break;
            case "--dof-aperture" when i + 1 < args.Length:
                s.DofAperture = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--dof-blur" when i + 1 < args.Length:
                s.DofBlurAmount = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--dof-radius" when i + 1 < args.Length:
                s.DofBlurRadius = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--dof-focus" when i + 1 < args.Length:
                s.DofFocusDistance = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--lag": s.DisableLag = false; break;
            case "--no-lag": s.DisableLag = true; break;
            default: Console.WriteLine("unknown arg: " + args[i]); return 1;
        }
    }
    s.FromJson(s.ToJson()); // clamp
    s.Save();
    Console.WriteLine("[firstperson] " + s.ToJson().ToString(Newtonsoft.Json.Formatting.None));
    Console.WriteLine(s.Enabled
        ? "enabled — run rebuild-mod (or the standalone generator) to fold it into ExpeditionInfinity_<seed>_P."
        : "disabled — run rebuild-mod to revert to the vanilla third-person camera.");
    return 0;
}

static int CmdFirstPersonSmoke(string[] args)
{
    var s = FirstPersonCameraKit.Settings.Load();
    s.Enabled = true;
    // Passes 1–2 cover the base camera; the DoF extension is exercised in pass 3.
    s.DepthOfField = false;
    int fails = 0;
    void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "ok   " : "FAIL ") + what);
        if (!ok) fails++;
    }

    // Pass 1: explore-only (default) — aim configs and the table stay vanilla.
    {
        s.AimFirstPerson = false;
        var dir = EditorPaths.TempSubdir("fp_smoke");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        int n;
        try { n = FirstPersonCameraKit.Apply(dir, s); }
        catch (Exception e) { Console.WriteLine("FAIL apply threw: " + e); return 1; }
        Check(n >= FirstPersonCameraKit.Stems.Length * 3, $"explore-only staged {n} edits");
        var exploreStems = FirstPersonCameraKit.Stems
            .Concat(FirstPersonCameraKit.CreatedExploreStems.Select(c => c.Stem)).ToArray();
        foreach (var stem in exploreStems)
        {
            var (fov, forward, side, lift, boom) = FirstPersonCameraKit.Resolve(stem, s);
            CheckConfig(dir, stem, s, fov, forward, side, null, boom, Check);
        }
        Check(FileCopy.FindUnder(dir, "DA_CameraConfig_FreeAim_Default.uasset") is null,
            "aim configs not staged when AimFirstPerson=false");
        // Table IS staged (Verso explore rewire) but aim rows stay vanilla.
        {
            var stagedTable = FileCopy.FindUnder(dir, "DT_jRPG_WorldCharacters.uasset");
            Check(stagedTable is not null, "DT_jRPG_WorldCharacters staged");
            if (stagedTable is not null)
            {
                Check(FirstPersonCameraKit.ReadWorldCharacterAim(stagedTable, "Verso", "CameraConfig") == "DA_CameraConfig_Verso",
                    "Verso row CameraConfig -> DA_CameraConfig_Verso");
                Check(FirstPersonCameraKit.ReadWorldCharacterAim(stagedTable, "Sciel") == "DA_CameraConfig_FreeAim_Lune",
                    "Sciel aim row still vanilla");
            }
        }
        // Traversal-restore hardening: staged pawns carry preset values in
        // every restore source (main-camera SCS template + Save snapshot).
        {
            var stagedPawn = FileCopy.FindUnder(dir, "BP_jRPG_Character_World.uasset");
            Check(stagedPawn is not null, "BP_jRPG_Character_World staged");
            if (stagedPawn is not null)
            {
                var st = FirstPersonCameraKit.ReadPawnCameraState(stagedPawn);
                var wantArm = s.ArmLength.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                Check(st.GetValueOrDefault("ScsArm") == wantArm, $"pawn smart-arm SCS arm={wantArm} (got {st.GetValueOrDefault("ScsArm")})");
                var wantSockTpl = $"({s.ForwardOffset}, {s.ExploreSideOffset}, 0)";
                Check(st.GetValueOrDefault("ScsSocket") == wantSockTpl, $"pawn smart-arm SCS socket={wantSockTpl} (got {st.GetValueOrDefault("ScsSocket")})");
                Check(st.GetValueOrDefault("PivotArm") == "0", $"crouch pivot arm still 0 (got {st.GetValueOrDefault("PivotArm")})");
                var consts = FirstPersonCameraKit.SaveSnapshotConsts(stagedPawn);
                Check(consts.Contains("double:" + wantArm), $"snapshot arm const {wantArm} (got [{string.Join(", ", consts)}])");
                var wantSock = $"vector:({s.ForwardOffset}, {s.ExploreSideOffset}, 0)";
                Check(consts.Contains(wantSock), $"snapshot socket const {wantSock}");
                Check(FirstPersonCameraKit.SaveSnapshotMentions(stagedPawn, "MaxWalkSpeedCache"),
                    "walk-speed snapshot untouched");
                Check(FirstPersonCameraKit.SaveSnapshotMentions(stagedPawn, "OriginalCameraLocation"),
                    "camera-location snapshot untouched");
            }
            // The world-map pawn overrides the smart-arm SCS (1500cm boom,
            // (0,300,142) socket) and inherits all camera logic; it needs the
            // same preset baked in.
            var stagedMapPawn = FileCopy.FindUnder(dir, "BP_jRPG_Character_WorldMap_pawn.uasset");
            Check(stagedMapPawn is not null, "BP_jRPG_Character_WorldMap_pawn staged");
            if (stagedMapPawn is not null)
            {
                var stm = FirstPersonCameraKit.ReadPawnCameraState(stagedMapPawn);
                var wantArm = s.ArmLength.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                Check(stm.GetValueOrDefault("ScsArm") == wantArm, $"worldmap smart-arm SCS arm={wantArm} (got {stm.GetValueOrDefault("ScsArm")})");
                var wantSockTpl = $"({s.ForwardOffset}, {s.ExploreSideOffset}, 0)";
                Check(stm.GetValueOrDefault("ScsSocket") == wantSockTpl, $"worldmap smart-arm SCS socket={wantSockTpl} (got {stm.GetValueOrDefault("ScsSocket")})");
                var wantAperture = s.WorldMapAperture.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                var gotAperture = FirstPersonCameraKit.ReadWorldMapAperture(stagedMapPawn);
                Check(gotAperture == wantAperture, $"worldmap DoF aperture={wantAperture} (got {gotAperture})");
            }
            // Overworld DoF off (extension off): both overworld lenses park the
            // aperture and carry no DoF override, so nothing — including a fight
            // reset — can bring the diorama blur back.
            foreach (var pawn in FirstPersonCameraKit.OverworldDofPawns)
            {
                var p = FileCopy.FindUnder(dir, pawn + ".uasset");
                Check(p is not null, pawn + " staged (overworld dof off)");
                if (p is null) continue;
                var wantAp = s.WorldMapAperture.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                Check(FirstPersonCameraKit.ReadCineAperture(p) == wantAp,
                    $"{pawn} overworld aperture={wantAp} (got {FirstPersonCameraKit.ReadCineAperture(p)})");
                Check(FirstPersonCameraKit.ReadCineDofFlag(p, "bOverride_DepthOfFieldFstop") == "0",
                    $"{pawn} DoF fstop override cleared");
                Check(FirstPersonCameraKit.ReadCineDofFlag(p, "bOverride_DepthOfFieldFocalDistance") == "0",
                    $"{pawn} DoF focal override cleared");
            }
        }
        // Climb boom: ClimbArmLength=0 keeps the explore framing during climbs.
        {
            var climb = FileCopy.FindUnder(dir, "BP_ClimbingComponent.uasset");
            Check(climb is not null, "BP_ClimbingComponent staged");
            if (climb is not null)
            {
                var wantClimb = s.ClimbArm.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                var got = FirstPersonCameraKit.ReadClimbArm(climb);
                Check(got == wantClimb, $"climb boom={wantClimb} (got {got})");
            }
        }
        // Monoco explore lens further forward than the rest.
        var monoco = FileCopy.FindUnder(dir, "DA_CameraConfig_Monoco.uasset");
        if (monoco is not null)
        {
            var vals = FirstPersonCameraKit.ReadValues(monoco);
            Check(vals.GetValueOrDefault("SocketOffset", "").StartsWith($"({s.MonocoExploreForward},",
                    StringComparison.Ordinal),
                $"Monoco explore socket forward {s.MonocoExploreForward} (got {vals.GetValueOrDefault("SocketOffset")})");
        }
    }

    // Pass 2: full aim path (opt-in) — clones stage and rows rewire.
    {
        s.AimFirstPerson = true;
        var dir = EditorPaths.TempSubdir("fp_smoke_aim");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        int n;
        try { n = FirstPersonCameraKit.Apply(dir, s); }
        catch (Exception e) { Console.WriteLine("FAIL apply threw: " + e); return 1; }
        var allStems = FirstPersonCameraKit.Stems.Concat(FirstPersonCameraKit.AimStems)
            .Concat(FirstPersonCameraKit.CreatedAimStems.Select(c => c.Stem))
            .Concat(FirstPersonCameraKit.CreatedExploreStems.Select(c => c.Stem)).ToArray();
        Check(n >= allStems.Length * 3, $"aim path staged {n} edits");
        foreach (var stem in allStems)
        {
            var (fov, forward, side, lift, boom) = FirstPersonCameraKit.Resolve(stem, s);
            // Expected pivot = vanilla base + EyeLift - PivotDrop + lift.
            double? vanillaBase = stem switch
            {
                "DA_CameraConfig_FreeAim_Monoco" => 152.09,
                "DA_CameraConfig_FreeAim_Lune" => 127.54,
                "DA_CameraConfig_FreeAim_Maelle" => 127.54,
                "DA_CameraConfig_FreeAim_Default" => 147.54,
                "DA_CameraConfig_FreeAim_Sciel" => 127.54,
                "DA_CameraConfig_FreeAim_Verso" => 147.54,
                "DA_CameraConfig_Verso" => 142.09,
                _ => null,
            };
            string? wantPivot = vanillaBase is null ? null :
                (vanillaBase.Value + s.EyeLift - s.PivotDrop + lift)
                .ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            CheckConfig(dir, stem, s, fov, forward, side, wantPivot, boom, Check);
        }
        var stagedTable = FileCopy.FindUnder(dir, "DT_jRPG_WorldCharacters.uasset");
        Check(stagedTable is not null, "DT_jRPG_WorldCharacters staged");
        if (stagedTable is not null)
        {
            Check(FirstPersonCameraKit.ReadWorldCharacterAim(stagedTable, "Sciel") == "DA_CameraConfig_FreeAim_Sciel",
                "Sciel row -> FreeAim_Sciel");
            Check(FirstPersonCameraKit.ReadWorldCharacterAim(stagedTable, "Verso") == "DA_CameraConfig_FreeAim_Verso",
                "Verso row -> FreeAim_Verso");
            Check(FirstPersonCameraKit.ReadWorldCharacterAim(stagedTable, "Lune") == "DA_CameraConfig_FreeAim_Lune",
                "Lune row still -> FreeAim_Lune");
        }
    }
    // Pass 3: DoF extension (opt-in) — both exploration lenses carry the
    // moderate aperture and the dungeon lens refocuses so the subject is sharp.
    {
        s.DepthOfField = true;
        var dir = EditorPaths.TempSubdir("fp_smoke_dof");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        try { FirstPersonCameraKit.Apply(dir, s); }
        catch (Exception e) { Console.WriteLine("FAIL dof apply threw: " + e); return 1; }
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var wantAperture = s.DofAperture.ToString("0.###", inv);
        var wantFocus = FirstPersonCameraKit.DofFocus(s).ToString("0.###", inv);
        foreach (var pawn in FirstPersonCameraKit.DofPawns)
        {
            var p = FileCopy.FindUnder(dir, pawn + ".uasset");
            Check(p is not null, pawn + " staged (dof)");
            if (p is null) continue;
            var got = FirstPersonCameraKit.ReadCineAperture(p);
            Check(got == wantAperture, $"{pawn} DoF aperture={wantAperture} (got {got})");
            var gotFstop = FirstPersonCameraKit.ReadCineDof(p, "DepthOfFieldFstop");
            Check(gotFstop == wantAperture, $"{pawn} PP DepthOfFieldFstop={wantAperture} (got {gotFstop})");
            var gotFocal = FirstPersonCameraKit.ReadCineDof(p, "DepthOfFieldFocalDistance");
            Check(gotFocal == wantFocus, $"{pawn} PP DepthOfFieldFocalDistance={wantFocus} (got {gotFocal})");
        }
        var world = FileCopy.FindUnder(dir, "BP_jRPG_Character_World.uasset");
        if (world is not null)
        {
            var gotFocus = FirstPersonCameraKit.ReadCineFocus(world);
            Check(gotFocus == wantFocus, $"dungeon DoF focus={wantFocus} (got {gotFocus})");
        }
    }
    // Vanilla sources untouched: arm length still 280/310.
    var vanilla = FileCopy.FindTablesAsset("DA_CameraConfig_Default.uasset");
    if (vanilla is not null)
    {
        var vals = FirstPersonCameraKit.ReadValues(vanilla);
        Check(vals.GetValueOrDefault("TargetArmLength") == "280", "vanilla Default untouched (arm=280)");
    }
    Console.WriteLine(fails == 0 ? "firstperson-smoke PASSED" : $"firstperson-smoke FAILED ({fails})");
    return fails == 0 ? 0 : 1;
}

/// <summary>
/// LEGACY / DEBUG ONLY. Builds a standalone camera pack (the camera is now a
/// submodule of ExpeditionInfinity_<seed>_P — use <c>rebuild-mod</c> or the GUI Apply &amp; Pack).
/// Wipes the staging dir, applies the kit, verifies every restore source, then
/// packs to Export/ and installs to ~mods. Kept for isolating camera changes
/// without a full rebuild; do not ship the standalone pack alongside the main one.
/// </summary>
static int CmdSecondPersonPack(string[] args)
{
    var s = FirstPersonCameraKit.Settings.Load();
    s.Enabled = true;
    var dir = Path.GetFullPath(GetOption(args, "--out")
        ?? Path.Combine(EditorPaths.RepoRoot, "work", "mod_fp"));
    var name = GetOption(args, "--name") ?? "SecondPersonCamera_P";
    bool install = !args.Contains("--no-install");

    Console.WriteLine("[secondperson-pack] LEGACY DEBUG PATH: the camera is a submodule of ExpeditionInfinity_<seed>_P now — " +
                      "use rebuild-mod (or the standalone generator). A standalone camera pack will shadow the main pack.");

    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);
    int n;
    try { n = FirstPersonCameraKit.Apply(dir, s); }
    catch (Exception e) { Console.WriteLine("FAIL apply threw: " + e); return 1; }

    int fails = 0;
    void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "ok   " : "FAIL ") + what);
        if (!ok) fails++;
    }
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var wantArm = s.ArmLength.ToString("0.###", inv);
    var wantSock = $"({s.ForwardOffset}, {s.ExploreSideOffset}, 0)";

    foreach (var stem in new[] { "BP_jRPG_Character_World", "BP_jRPG_Character_WorldMap_pawn" })
    {
        var p = FileCopy.FindUnder(dir, stem + ".uasset");
        Check(p is not null, stem + " staged");
        if (p is null) continue;
        var st = FirstPersonCameraKit.ReadPawnCameraState(p);
        Check(st.GetValueOrDefault("ScsArm") == wantArm,
            $"{stem} smart-arm boom={wantArm} (got {st.GetValueOrDefault("ScsArm")})");
        Check(st.GetValueOrDefault("ScsSocket") == wantSock,
            $"{stem} smart-arm socket={wantSock} (got {st.GetValueOrDefault("ScsSocket")})");
    }
    var climb = FileCopy.FindUnder(dir, "BP_ClimbingComponent.uasset");
    Check(climb is not null, "BP_ClimbingComponent staged");
    if (climb is not null)
    {
        var wantClimb = s.ClimbArm.ToString("0.###", inv);
        var got = FirstPersonCameraKit.ReadClimbArm(climb);
        Check(got == wantClimb, $"climb boom={wantClimb} (got {got})");
    }
    var mapPawn = FileCopy.FindUnder(dir, "BP_jRPG_Character_WorldMap_pawn.uasset");
    if (mapPawn is not null)
    {
        var wantAperture = (s.DepthOfField ? s.DofAperture : s.WorldMapAperture)
            .ToString("0.###", inv);
        var got = FirstPersonCameraKit.ReadCineAperture(mapPawn);
        Check(got == wantAperture, $"worldmap DoF aperture={wantAperture} (got {got})");
        if (s.DepthOfField)
        {
            var worldPawn = FileCopy.FindUnder(dir, "BP_jRPG_Character_World.uasset");
            if (worldPawn is not null)
                Check(FirstPersonCameraKit.ReadCineAperture(worldPawn) == wantAperture,
                    $"dungeon DoF aperture={wantAperture} (got {FirstPersonCameraKit.ReadCineAperture(worldPawn)})");
        }
    }
    var def = FileCopy.FindUnder(dir, "DA_CameraConfig_Default.uasset");
    Check(def is not null, "DA_CameraConfig_Default staged");
    if (def is not null)
    {
        var vals = FirstPersonCameraKit.ReadValues(def);
        Check(vals.GetValueOrDefault("TargetArmLength") == wantArm,
            $"explore boom={wantArm} (got {vals.GetValueOrDefault("TargetArmLength")})");
        Check(vals.GetValueOrDefault("SocketOffset") == wantSock,
            $"explore socket={wantSock} (got {vals.GetValueOrDefault("SocketOffset")})");
    }
    Check(FileCopy.FindUnder(dir, "DT_jRPG_WorldCharacters.uasset") is not null,
        "DT_jRPG_WorldCharacters staged");
    if (fails > 0)
    {
        Console.WriteLine($"secondperson-pack FAILED ({fails}) — nothing packed");
        return 1;
    }

    foreach (var ext in new[] { ".pak", ".utoc", ".ucas" })
    {
        var stale = Path.Combine(EditorPaths.ExportDir, name + ext);
        if (File.Exists(stale)) File.Delete(stale);
    }
    new Retoc().PackToZen(dir, Path.Combine(EditorPaths.ExportDir, name + ".utoc"));
    Console.WriteLine($"secondperson-pack: {n} edit(s) staged in {dir}");
    foreach (var ext in new[] { ".pak", ".utoc", ".ucas" })
    {
        var src = Path.Combine(EditorPaths.ExportDir, name + ext);
        if (!File.Exists(src)) continue;
        Console.WriteLine("  " + src);
        if (install)
        {
            Directory.CreateDirectory(EditorPaths.ModsDir);
            File.Copy(src, Path.Combine(EditorPaths.ModsDir, name + ext), true);
        }
    }
    Console.WriteLine(install
        ? $"installed {name} to {EditorPaths.ModsDir}"
        : $"packed {name} to {EditorPaths.ExportDir} (not installed)");
    return 0;
}

static void CheckConfig(string dir, string stem, FirstPersonCameraKit.Settings s, float wantFov, float wantForward, float wantSide, string? wantPivot, float wantArm,
    Action<bool, string> Check)
{
    var staged = FileCopy.FindUnder(dir, stem + ".uasset");
    Check(staged is not null, stem + " staged");
    if (staged is null) return;
    var vals = FirstPersonCameraKit.ReadValues(staged);
    var wantArmStr = wantArm.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    Check(vals.GetValueOrDefault("TargetArmLength") == wantArmStr, $"{stem} arm={wantArmStr} (got {vals.GetValueOrDefault("TargetArmLength")})");
    var wantSock = $"({wantForward}, {wantSide}, 0)";
    Check(vals.GetValueOrDefault("SocketOffset", "") == wantSock,
        $"{stem} socket={wantSock} (got {vals.GetValueOrDefault("SocketOffset")})");
    if (vals.TryGetValue("EnableCameraLag", out var lag))
        Check(lag == (s.DisableLag ? "False" : "True"), $"{stem} lag {(s.DisableLag ? "off" : "on")}");
    Check(vals.GetValueOrDefault("FieldOfView") == wantFov.ToString("0",
            System.Globalization.CultureInfo.InvariantCulture),
        $"{stem} fov={wantFov} (got {vals.GetValueOrDefault("FieldOfView")})");
    if (wantPivot is not null)
    {
        var got = vals.GetValueOrDefault("RelativeLocation", "");
        Check(got.EndsWith(", " + wantPivot + ")", StringComparison.Ordinal),
            $"{stem} pivotZ={wantPivot} (got {got})");
    }
    Console.WriteLine($"       {stem} pivot={vals.GetValueOrDefault("RelativeLocation")}");
}

static int CmdCompatDetect(string[] args)
{
    var hit = CompatibilityKit.Detect();
    if (hit is null)
    {
        Console.WriteLine("[compat] Bigger Party Mod not found in " + EditorPaths.ModsDir);
        return 1;
    }
    Console.WriteLine("[compat] found: " + hit);
    foreach (var p in new Retoc().ListPaths(hit))
        Console.WriteLine("   " + p);
    return 0;
}

static int? CdoIntProp(string path, string field)
{
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAsset(path, EngineVersion.VER_UE5_4, map)
        : new UAsset(path, EngineVersion.VER_UE5_4);
    foreach (var ex in u.Exports.OfType<NormalExport>())
    {
        if (!ex.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal)) continue;
        foreach (var p in ex.Data)
            if (p is UAssetAPI.PropertyTypes.Objects.IntPropertyData i
                && DataTableEditor.CleanName(p.Name) == field) return i.Value;
    }
    return null;
}

static int CmdCompatSmoke(string[] args)
{
    int checks = 0, fails = 0;
    void Check(bool ok, string msg)
    {
        checks++;
        Console.WriteLine((ok ? "ok   " : "FAIL ") + msg);
        if (!ok) fails++;
    }

    var realPaks = EditorPaths.PaksDir;
    var realMod = CompatibilityKit.Detect();
    if (realMod is null)
    {
        Console.WriteLine("[compat-smoke] skip: Bigger Party Mod not installed");
        return 0;
    }

    var root = EditorPaths.TempSubdir("compat-smoke");
    if (Directory.Exists(root)) Directory.Delete(root, true);
    Directory.CreateDirectory(root);
    var modsDir = Path.Combine(root, "~mods");
    Directory.CreateDirectory(modsDir);
    foreach (var g in new[] { "global.utoc", "global.ucas" })
        File.Copy(Path.Combine(realPaks, g), Path.Combine(root, g), true);
    var stem = Path.GetFileNameWithoutExtension(realMod);
    foreach (var ext in new[] { ".utoc", ".ucas", ".pak" })
        File.Copy(Path.Combine(realPaks, "~mods", stem + ext), Path.Combine(modsDir, stem + ext), true);

    var tweaks = new GameplayTweaksKit.Settings();
    var tweaksPath = Path.Combine(EditorPaths.RepoRoot, "work", "gameplay_tweaks.json");
    if (File.Exists(tweaksPath))
        tweaks.FromJson(Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(tweaksPath)));

    EditorPaths.PaksDir = root;
    try
    {
        var hit = CompatibilityKit.Detect();
        Check(hit is not null, "detects Bigger Party Mod in isolated ~mods");
        Check(hit is not null && CompatibilityKit.IsValidMod(hit), "mod signature recognised");

        var fake = Path.Combine(root, "fakeMod");
        foreach (var a in CompatibilityKit.PatchAssets)
        {
            var p = Path.Combine(fake, "Sandfall", "Content", a);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, "x");
            File.WriteAllText(Path.ChangeExtension(p, ".uexp"), "x");
        }
        Check(CompatibilityKit.StripConflicts(fake) == 6, "StripConflicts removes 3 assets + .uexp");

        var edits = CompatibilityKit.Apply(hit!, tweaks, null);
        Check(edits >= 2, $"patched the mod in place ({edits} value edits)");

        // Re-extract the patched copy and verify the values survived the repack.
        var src = Path.Combine(root, "verify-src");
        var ex = Path.Combine(root, "verify-out");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(ex);
        foreach (var g in new[] { "global.utoc", "global.ucas" })
            File.Copy(Path.Combine(root, g), Path.Combine(src, g), true);
        foreach (var ext in new[] { ".utoc", ".ucas", ".pak" })
            File.Copy(Path.Combine(modsDir, stem + ext), Path.Combine(src, stem + ext), true);
        new Retoc().ToLegacyFrom(src, ex);

        var cm = Path.Combine(ex, "Sandfall/Content/jRPGTemplate/Blueprints/Components/AC_jRPG_CharactersManager.uasset");
        var fl = Path.Combine(ex, "Sandfall/Content/jRPGTemplate/Blueprints/Basics/FL_jRPG_CustomFunctionLibrary.uasset");
        Check(CdoIntProp(cm, "MaxCharactersAmountInParty") == 4, "mod's party size 4 preserved");
        Check(CdoIntProp(cm, "CharacterMaxLevel") == tweaks.LevelCap, $"CharacterMaxLevel = {tweaks.LevelCap}");
        var flU = new UAsset(fl, EngineVersion.VER_UE5_4, DataTableEditor.Mappings);
        int maxLevel = -1;
        foreach (var fn in flU.Exports.OfType<FunctionExport>())
        {
            if (fn.ObjectName.ToString() != "GetMaximumLevel" || fn.ScriptBytecode is null) continue;
            E33ModEditor.Core.MayhemFixKit.Walk(fn.ScriptBytecode,
                e => { if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst c) maxLevel = c.Value; });
        }
        Check(maxLevel == tweaks.LevelCap, $"FL GetMaximumLevel = {tweaks.LevelCap}");

        // Structural guard: patching must not re-serialize the container's zen
        // headers. A legacy round-trip (to-zen) rewrites the import map / export
        // indices and the game rejects the re-indexed blueprints with an access
        // violation at launch, so an unedited asset's header must stay identical
        // to the pristine original.
        var pristineDir = Path.Combine(EditorPaths.RepoRoot, "work", "pak-backup", "compat", stem + "-pristine");
        var pristineUtoc = Path.Combine(pristineDir, stem + ".utoc");
        Check(File.Exists(pristineUtoc), "pristine original kept for comparison");
        if (File.Exists(pristineUtoc))
        {
            var src0 = Path.Combine(root, "verify-orig-src");
            var ex0 = Path.Combine(root, "verify-orig");
            Directory.CreateDirectory(src0);
            Directory.CreateDirectory(ex0);
            foreach (var g in new[] { "global.utoc", "global.ucas" })
                File.Copy(Path.Combine(root, g), Path.Combine(src0, g), true);
            foreach (var ext in new[] { ".utoc", ".ucas", ".pak" })
                File.Copy(Path.Combine(pristineDir, stem + ext), Path.Combine(src0, stem + ext), true);
            new Retoc().ToLegacyFrom(src0, ex0);

            var rel = "Sandfall/Content/jRPGTemplate/Blueprints/Basics/BP_jRPG_Character_World";
            Check(File.ReadAllBytes(Path.Combine(ex0, rel + ".uasset"))
                    .AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(ex, rel + ".uasset"))),
                "unedited asset header preserved byte-for-byte (no zen re-indexing)");
        }
    }
    finally
    {
        EditorPaths.PaksDir = realPaks;
        try { Directory.Delete(root, true); } catch { /* best effort */ }
    }

    Console.WriteLine(fails == 0
        ? $"[compat-smoke] {checks} checks passed"
        : $"[compat-smoke] {fails}/{checks} FAILED");
    return fails == 0 ? 0 : 1;
}

static int CmdParty5(string[] args)
{
    var dir = GetOption(args, "--out") ?? Path.Combine(EditorPaths.RepoRoot, "work", "mod_party5");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);

    // Respect the user's saved formation/party size. The bisect flags below are
    // throwaway overrides: they never write work/party_five.json, so a debug
    // build cannot silently ship the no-spawn / no-formation variant later.
    var kit = PartyFiveKit.Settings.Load();
    kit.Enabled = true;
    bool bisect = args.Contains("--no-formation") || args.Contains("--no-spawn") ||
                  args.Contains("--no-resetdebug");
    if (args.Contains("--no-formation")) kit.UseFormationOffsets = false;
    if (args.Contains("--no-spawn")) kit.UseSpawnPatch = false;
    if (args.Contains("--no-resetdebug")) kit.UseResetDebugPrint = false;
    int n = PartyFiveKit.Apply(dir, kit, m => Console.WriteLine(m));
    PartyFiveKit.Verify(dir, kit);
    if (!bisect)
    {
        kit.Save();
        Console.WriteLine($"party5: {n} edit(s) staged in {dir} and verified; settings saved to work/party_five.json " +
                          "(rebuild-mod / the standalone generator folds it into ExpeditionInfinity_<seed>_P)");
    }
    else
    {
        Console.WriteLine($"party5: {n} edit(s) staged in {dir} and verified (bisect flags; settings NOT saved)");
    }

    var pack = GetOption(args, "--pack");
    if (pack is not null)
    {
        Console.WriteLine($"[party5] LEGACY DEBUG PATH: packing standalone {pack}.* — the party is a submodule of " +
                          "ExpeditionInfinity_<seed>_P now; do not ship this alongside the main pack.");
        var exportUtoc = Path.Combine(EditorPaths.ExportDir, pack + ".utoc");
        foreach (var ext in new[] { ".pak", ".utoc", ".ucas" })
        {
            var stale = Path.Combine(EditorPaths.ExportDir, pack + ext);
            if (File.Exists(stale)) File.Delete(stale);
        }
        new Retoc().PackToZen(dir, exportUtoc);
        Console.WriteLine($"packed {pack}.* into {EditorPaths.ExportDir}");
        if (args.Contains("--install"))
        {
            foreach (var ext in new[] { ".pak", ".utoc", ".ucas" })
            {
                var src = Path.Combine(EditorPaths.ExportDir, pack + ext);
                if (File.Exists(src)) File.Copy(src, Path.Combine(EditorPaths.ModsDir, pack + ext), true);
            }
            Console.WriteLine($"installed to {EditorPaths.ModsDir}");
        }
        Console.WriteLine("NOTE: move 0000_Party_Of_Four_P (or any other party-size mod) out of ~mods — " +
                          "its BP_BattleMap/Character_World expect its own BattleManager and would conflict.");
    }
    return 0;
}

static int CmdRowDamageSmoke(string[] args)
{
    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_rowdamagesmoke");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);

    int checks = 0, fails = 0;
    void Check(bool ok, string msg)
    {
        checks++;
        Console.WriteLine((ok ? "ok   " : "FAIL ") + msg);
        if (!ok) fails++;
    }

    var party = new PartyFiveKit.Settings { Enabled = true };
    var settings = new RowDamageKit.Settings();
    var back = RowDamageKit.BackSpots(party);
    Check(back.SequenceEqual(new[] { 1, 3, 4 }),
        $"default formation back spots = [{string.Join(",", back)}] (want 1,3,4)");

    // Rows must follow the offsets, not a hard-coded 0/1/2/3 order.
    var custom = new PartyFiveKit.Settings { Enabled = true };
    var customFormation = custom.Formation.ToArray();
    customFormation[0] = (-200, customFormation[0].Right);
    customFormation[1] = (+200, customFormation[1].Right);
    custom.Formation = customFormation;
    var customBack = RowDamageKit.BackSpots(custom);
    Check(customBack.Contains(0) && !customBack.Contains(1),
        $"custom formation back spots = [{string.Join(",", customBack)}]");

    int n = RowDamageKit.Apply(dir, party, settings, m => Console.WriteLine(m));
    Check(n >= 1, $"applied {n} edit(s)");

    var path = FileCopy.FindUnder(dir, "AC_jRPG_CharacterBattleStats.uasset");
    Check(path is not null, "staged AC_jRPG_CharacterBattleStats");
    if (path is not null)
    {
        Check(RowDamageKit.Verify(path, back, settings), "row damage patch verifies");

        var map = DataTableEditor.Mappings;
        var asset = map is not null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(f => f.ObjectName.ToString() == RowDamageKit.FunctionName);
        int adds = 0, selects = 0, convs = 0, spots = 0, enemies = 0;
        foreach (var st in fn.ScriptBytecode!)
            WalkExprs(new[] { st }, e =>
            {
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVirtualFunction vf
                    && vf.VirtualFunctionName.ToString() == "AddDamageModifier") adds++;
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm
                    && cm.StackNode is not null && cm.StackNode.IsImport()
                    && cm.StackNode.ToImport(asset).ObjectName.ToString() == "SelectInt") selects++;
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cv
                    && cv.StackNode is not null && cv.StackNode.IsImport()
                    && cv.StackNode.ToImport(asset).ObjectName.ToString() == "Conv_IntToDouble") convs++;
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_InstanceVariable iv)
                {
                    var nm = iv.Variable?.New?.Path?.FirstOrDefault()?.ToString();
                    if (nm == "BattleSpotIndex") spots++;
                    if (nm == "Enemy?") enemies++;
                }
            });
        Check(adds >= 2, $"AddDamageModifier calls = {adds}");
        Check(selects >= 8, $"SelectInt calls = {selects}");
        Check(convs >= 4, $"Conv_IntToDouble calls = {convs}");
        Check(spots >= 2, $"BattleSpotIndex reads = {spots}");
        Check(enemies >= 2, $"Enemy? reads = {enemies}");
    }

    // Composition: the stats component is also staged by GameplayTweaksKit for
    // the consecutive-turns cap. Row damage must patch the same copy and leave
    // the turns edit intact.
    try
    {
        var compose = Path.Combine(EditorPaths.RepoRoot, "work", "mod_rowdamagecompose");
        if (Directory.Exists(compose)) Directory.Delete(compose, true);
        Directory.CreateDirectory(compose);

        var tweaks = new GameplayTweaksKit.Settings { MaxConsecutiveTurns = 3 };
        GameplayTweaksKit.Apply(compose, tweaks);
        int n2 = RowDamageKit.Apply(compose, party, settings, m => Console.WriteLine(m));
        Check(n2 >= 1, $"compose: applied {n2} edit(s)");

        var cpath = FileCopy.FindUnder(compose, "AC_jRPG_CharacterBattleStats.uasset");
        Check(cpath is not null, "compose: staged AC_jRPG_CharacterBattleStats");
        if (cpath is not null)
        {
            Check(RowDamageKit.Verify(cpath, back, settings), "compose: row damage verifies");
            Check(GameplayTweaksKit.VerifyTurnsPatch(cpath, 3),
                "compose: consecutive-turns patch survives row damage");
        }
    }
    catch (Exception ex)
    {
        Check(false, "compose: " + ex.Message);
    }

    Console.WriteLine(fails == 0
        ? $"[row-damage-smoke] {checks} checks passed"
        : $"[row-damage-smoke] {fails}/{checks} FAILED");
    return fails == 0 ? 0 : 1;
}

static int CmdParty5Smoke(string[] args)
{
    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "mod_party5smoke");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);

    int checks = 0, fails = 0;
    void Check(bool ok, string msg)
    {
        checks++;
        Console.WriteLine((ok ? "ok   " : "FAIL ") + msg);
        if (!ok) fails++;
    }

    var kit = new PartyFiveKit.Settings();
    kit.Enabled = true;
    int n = PartyFiveKit.Apply(dir, kit, m => Console.WriteLine(m));
    Check(n >= 4, $"applied {n} edit(s)");

    try
    {
        var cm = FileCopy.FindUnder(dir, "AC_jRPG_CharactersManager.uasset")!;
        var cmAsset = DataTableEditor.Mappings is { } m1
            ? new UAsset(cm, EngineVersion.VER_UE5_4, m1)
            : new UAsset(cm, EngineVersion.VER_UE5_4);
        var cdo = cmAsset.Exports.OfType<NormalExport>()
            .First(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
        var cap = cdo.Data.OfType<UAssetAPI.PropertyTypes.Objects.IntPropertyData>()
            .First(p => DataTableEditor.CleanName(p.Name) == "MaxCharactersAmountInParty");
        Check(cap.Value == 5, "CharactersManager cap = 5");

        var fl = FileCopy.FindUnder(dir, "FL_jRPG_CustomFunctionLibrary.uasset")!;
        var flAsset = DataTableEditor.Mappings is { } m2
            ? new UAsset(fl, EngineVersion.VER_UE5_4, m2)
            : new UAsset(fl, EngineVersion.VER_UE5_4);
        var remap = flAsset.Exports.OfType<FunctionExport>()
            .First(f => f.ObjectName.ToString() == "RemapPartyIndexToBattleSpotIndex");
        var arrays = remap.ScriptBytecode!
            .OfType<UAssetAPI.Kismet.Bytecode.Expressions.EX_SetArray>()
            .Select(s => s.Elements.OfType<UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst>()
                .Select(e => e.Value).ToArray())
            .ToList();
        Check(arrays.Count == 2 && arrays.All(a => a.SequenceEqual(new[] { 2, 0, 3, 1, 4 })),
            "remap table = [2,0,3,1,4] in both branches");

        // Requested layout, computed from the vanilla anchors + per-spot offsets:
        // front 1/2 left-to-right, back 3/5/4 left-to-right, no character closer
        // than the ~200-unit placement floor.
        var anchors = new (double X, double Y, double Yaw)[]
        {
            (-230.06, 7.97, -12.95),
            (-223.11, 264.81, -32.74),
            (-211.20, -274.30, 9.33),
        };
        var world = new (double X, double Y)[kit.PartySize];
        var remapForLayout = PartyFiveKit.RemapTable();
        for (int member = 0; member < kit.PartySize; member++)
        {
            int spot = remapForLayout[member];
            int anchor = spot <= 2 ? spot : spot == 3 ? 2 : 0;
            var (fwd, rightOffset) = kit.Formation[spot];
            double yaw = anchors[anchor].Yaw * Math.PI / 180.0;
            world[member] = (
                anchors[anchor].X + fwd * Math.Cos(yaw) - rightOffset * Math.Sin(yaw),
                anchors[anchor].Y + fwd * Math.Sin(yaw) + rightOffset * Math.Cos(yaw));
        }
        Check(world[0].Y < world[1].Y && Math.Abs(world[0].X - world[1].X) < 2,
            $"front row is 1 left / 2 right (x={world[0].X:0.0}/{world[1].X:0.0}, y={world[0].Y:0.0}/{world[1].Y:0.0})");
        Check(world[2].Y < world[4].Y && world[4].Y < world[3].Y,
            $"back row is 3/5/4 left-to-right (y={world[2].Y:0.0}/{world[4].Y:0.0}/{world[3].Y:0.0})");
        Check(world[0].X > world[2].X && world[1].X > world[3].X && world[1].X > world[4].X,
            $"front row is closer to the enemies (+X) than the back row (x={world[0].X:0.0},{world[1].X:0.0} vs {world[2].X:0.0},{world[4].X:0.0},{world[3].X:0.0})");
        double minGap = double.MaxValue;
        for (int a = 0; a < world.Length; a++)
            for (int b = a + 1; b < world.Length; b++)
                minGap = Math.Min(minGap, Math.Sqrt(
                    Math.Pow(world[a].X - world[b].X, 2) + Math.Pow(world[a].Y - world[b].Y, 2)));
        Check(minGap >= 200, $"all five spawn positions >= 200 units apart (min {minGap:0.0})");

        var bm = FileCopy.FindUnder(dir, "AC_jRPG_BattleManager.uasset")!;
        var bmAsset = DataTableEditor.Mappings is { } m3
            ? new UAsset(bm, EngineVersion.VER_UE5_4, m3)
            : new UAsset(bm, EngineVersion.VER_UE5_4);
        var spawn = bmAsset.Exports.OfType<FunctionExport>()
            .First(f => f.ObjectName.ToString() == "GetProperBattleSpawnPointTransform");
        var starts = new HashSet<uint>(PartyFiveKit.StatementStarts(bmAsset, spawn.ScriptBytecode!));
        uint totalSize = 0;
        foreach (var s in spawn.ScriptBytecode!) totalSize += (uint)s.GetSize(bmAsset);
        int bad = 0;
        uint pos = 0;
        foreach (var st in spawn.ScriptBytecode!)
            st.Visit(bmAsset, ref pos, (e, _) =>
            {
                switch (e)
                {
                    case UAssetAPI.Kismet.Bytecode.Expressions.EX_Jump j when !starts.Contains(j.CodeOffset): bad++; break;
                    case UAssetAPI.Kismet.Bytecode.Expressions.EX_JumpIfNot jn when !starts.Contains(jn.CodeOffset): bad++; break;
                    case UAssetAPI.Kismet.Bytecode.Expressions.EX_PushExecutionFlow pf when !starts.Contains(pf.PushingAddress): bad++; break;
                    case UAssetAPI.Kismet.Bytecode.Expressions.EX_SwitchValue sv:
                        if (sv.EndGotoOffset == 0 || sv.EndGotoOffset > totalSize) bad++;
                        foreach (var c in sv.Cases) if (c.NextOffset == 0 || c.NextOffset > totalSize) bad++;
                        break;
                }
            });
        Check(bad == 0, $"every jump target in the spawn function lands on a statement start ({bad} bad)");

        bool alias = false, select = false, rotate = false, right = false, add = false;
        int aliasAnds = 0;
        MayhemFixKit.Walk(spawn.ScriptBytecode!, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LetBool lb
                && PartyFiveKit.FieldName((lb.VariableExpression as UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable)?.Variable)
                   == "CallFunc_EqualEqual_IntInt_ReturnValue"
                && lb.AssignmentExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath o
                && o.StackNode.IsImport()
                && o.StackNode.ToImport(bmAsset).ObjectName.ToString() == "BooleanOR")
            {
                alias = true;
                MayhemFixKit.Walk(new UAssetAPI.Kismet.Bytecode.KismetExpression[] { o },
                    n => { if (n is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath a
                        && a.StackNode.IsImport()
                        && a.StackNode.ToImport(bmAsset).ObjectName.ToString() == "BooleanAND") aliasAnds++; });
            }
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath c && c.StackNode.IsImport())
            {
                var name = c.StackNode.ToImport(bmAsset).ObjectName.ToString();
                if (name == "SelectFloat") select = true;
                if (name == "Multiply_VectorFloat") add = true;
                if (name == "GetForwardVector") rotate = true;
                if (name == "GetRightVector") right = true;
            }
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_Context ctx
                && ctx.ContextExpression is UAssetAPI.Kismet.Bytecode.Expressions.EX_FinalFunction ffn
                && ffn.StackNode.IsImport())
            {
                var nm2 = ffn.StackNode.ToImport(bmAsset).ObjectName.ToString();
                if (nm2 == "GetForwardVector") rotate = true;
                if (nm2 == "GetRightVector") right = true;
            }
        });
        Check(alias && aliasAnds >= 3, $"slot-3 + slot-4 aliases wired into the spot match ({aliasAnds} AND node(s))");
        Check(select && rotate && right && add, "formation offset expression (SelectFloat + GetForwardVector/GetRightVector + Multiply_VectorFloat) present");

        var menu = FileCopy.FindUnder(dir, "WBP_GM_TeamOverview3D_Page.uasset")!;
        var menuAsset = DataTableEditor.Mappings is { } m6
            ? new UAsset(menu, EngineVersion.VER_UE5_4, m6)
            : new UAsset(menu, EngineVersion.VER_UE5_4);
        var uber = menuAsset.Exports.OfType<FunctionExport>()
            .First(f => f.ObjectName.ToString() == "ExecuteUbergraph_WBP_GM_TeamOverview3D_Page");
        int menuGates = 0;
        MayhemFixKit.Walk(uber.ScriptBytecode!, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath c && c.Parameters is { Length: 2 }
                && c.StackNode.IsImport())
            {
                var nm = c.StackNode.ToImport(menuAsset).ObjectName.ToString();
                if (nm is "Less_IntInt" or "GreaterEqual_IntInt"
                    && c.Parameters[1] is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ci
                    && ci.Value == 5)
                    menuGates++;
            }
        });
        Check(menuGates == 2, $"team-menu add/enable gates lifted to 5 ({menuGates}/2)");

        var cbb = FileCopy.FindUnder(dir, "BP_jRPG_Character_Battle_Base.uasset")!;
        var cbbAsset = DataTableEditor.Mappings is { } m7
            ? new UAsset(cbb, EngineVersion.VER_UE5_4, m7)
            : new UAsset(cbb, EngineVersion.VER_UE5_4);
        var orderFn = cbbAsset.Exports.OfType<FunctionExport>()
            .First(f => f.ObjectName.ToString() == "GetBattleSpotIndexLeftToRight_(WhenFacingCharacters)");
        var orderArrays = orderFn.ScriptBytecode!
            .OfType<UAssetAPI.Kismet.Bytecode.Expressions.EX_SetArray>()
            .Select(sa => sa.Elements.OfType<UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst>()
                .Select(e => e.Value).ToArray()).ToList();
        Check(orderArrays.Any(a => a.SequenceEqual(new[] { 1, 0, 2, 2, 1 })),
            "player left-to-right spot table = [1,0,2,2,1] (spot-3 mirrors left, spot-4 middle)");
        // The extra order values must stay inside the 0..2 range covered by
        // ClampAngleForLookLeftRight's yaw-limit switch; a value of 3 would hit
        // both switch defaults (0.0) and lock that character's free-aim yaw.
        // (Only the player table is checked — the enemy table [1,2,0,3]
        // legitimately contains a 3.)
        var playerTable = orderArrays.FirstOrDefault(a => a.Length == 5 && a[0] == 1 && a[1] == 0 && a[2] == 2);
        Check(playerTable is not null && playerTable.All(v => v >= 0 && v <= 2),
            "all player spot-order values inside the yaw-clamp switch range 0..2");

        // Overworld followers are capped by the number of follow points, and the
        // count that matters is the one on the world character's own component
        // template -- a template property overrides the component class CDO.
        var wc = FileCopy.FindUnder(dir, "BP_jRPG_Character_World.uasset");
        if (wc is not null && DataTableEditor.Mappings is { } m8)
        {
            var wcAsset = new UAsset(wc, EngineVersion.VER_UE5_4, m8);
            var pts = wcAsset.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>()
                .First(e => e.ObjectName.ToString() == "BP_AICompanion_TargetPointsComponent_GEN_VARIABLE")
                .Data.OfType<UAssetAPI.PropertyTypes.Objects.ArrayPropertyData>()
                .First(p => DataTableEditor.CleanName(p.Name) == "TargetPoints");
            Check(pts.Value.Length == 4,
                $"world-character follow points = {pts.Value.Length} (near flanks + balanced outer flanks)");
        }
        else
        {
            Check(false, "staged BP_jRPG_Character_World not found");
        }

        // The component class CDO is patched too; the template above wins at
        // runtime, but keep both in sync so a future template removal does not
        // silently fall back to two points.
        var tpComp = FileCopy.FindUnder(dir, "BP_AICompanion_TargetPointsComponent.uasset");
        if (tpComp is not null && DataTableEditor.Mappings is { } m9)
        {
            var tpAsset = new UAsset(tpComp, EngineVersion.VER_UE5_4, m9);
            var tpCdo = tpAsset.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>()
                .First(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
            var tpPts = tpCdo.Data.OfType<UAssetAPI.PropertyTypes.Objects.ArrayPropertyData>()
                .First(p => DataTableEditor.CleanName(p.Name) == "TargetPoints");
            Check(tpPts.Value.Length == 4,
                $"class-default follow points = {tpPts.Value.Length} (near flanks + balanced outer flanks)");
        }
        else
        {
            Check(false, "staged BP_AICompanion_TargetPointsComponent not found");
        }

        // Re-open the written assets through the kit's own verifier.
        PartyFiveKit.Verify(dir, kit);
        Check(true, "party5 Verify() passes");

        // The written asset must be stable: write again and compare bytes.
        var before = File.ReadAllBytes(bm);
        if (DataTableEditor.Mappings is { } m4)
        {
            var again = new UAsset(bm, EngineVersion.VER_UE5_4, m4);
            again.Write(bm);
        }
        Check(before.AsSpan().SequenceEqual(File.ReadAllBytes(bm)), "BattleManager re-serialization is byte-stable");

        // Every other function's bytecode must be byte-identical to vanilla:
        // only the spawn function may change.
        string? Vanilla(string asset)
        {
            foreach (var root in new[]
                     {
                         Path.Combine(EditorPaths.RepoRoot, "work", "tables", "party_five"),
                         Path.Combine(EditorPaths.RepoRoot, "work", "party5", "vanilla"),
                     })
            {
                var hit = FileCopy.FindUnder(root, asset);
                if (hit is not null) return hit;
            }
            return null;
        }
        byte[] Code(UAsset a, FunctionExport f)
        {
            using var ms = new MemoryStream();
            var w = new UAssetAPI.AssetBinaryWriter(ms, a);
            foreach (var st in f.ScriptBytecode ?? Array.Empty<UAssetAPI.Kismet.Bytecode.KismetExpression>())
                UAssetAPI.Kismet.Bytecode.ExpressionSerializer.WriteExpression(st, w);
            return ms.ToArray();
        }
        var vanBmPath = Vanilla("AC_jRPG_BattleManager.uasset");
        if (vanBmPath is not null && DataTableEditor.Mappings is { } m5)
        {
            var vanBm = new UAsset(vanBmPath, EngineVersion.VER_UE5_4, m5);
            var spawnV = vanBm.Exports.OfType<FunctionExport>()
                .First(f => f.ObjectName.ToString() == "GetProperBattleSpawnPointTransform");
            int changed = 0;
            foreach (var vf in vanBm.Exports.OfType<FunctionExport>())
            {
                if (vf.ScriptBytecode is null) continue;
                var pf = bmAsset.Exports.OfType<FunctionExport>()
                    .FirstOrDefault(f => f.ObjectName.ToString() == vf.ObjectName.ToString());
                if (pf?.ScriptBytecode is null) continue;
                if (!Code(vanBm, vf).AsSpan().SequenceEqual(Code(bmAsset, pf)))
                {
                    var nm3 = vf.ObjectName.ToString();
                    if (nm3 is not ("GetProperBattleSpawnPointTransform" or "Spawn Player Character")) changed++;
                }
            }
            Check(changed == 0, $"only the two spawn functions changed ({changed} other function(s) differ)");

            // Switch offsets are intra-statement: their distance from the
            // containing statement's start must be identical to vanilla.
            List<string> SwitchRels(UAsset a, FunctionExport f)
            {
                var starts = PartyFiveKit.StatementStarts(a, f.ScriptBytecode!);
                var rel = new List<string>();
                for (int i = 0; i < f.ScriptBytecode!.Length; i++)
                {
                    uint start = starts[i];
                    MayhemFixKit.Walk(new[] { f.ScriptBytecode![i] }, e =>
                    {
                        if (e is not UAssetAPI.Kismet.Bytecode.Expressions.EX_SwitchValue sv) return;
                        rel.Add("end:" + (sv.EndGotoOffset - start));
                        for (int c = 0; c < sv.Cases.Length; c++)
                            rel.Add($"case{c}:" + (sv.Cases[c].NextOffset - start));
                    });
                }
                return rel;
            }
            var vanRels = SwitchRels(vanBm, spawnV);
            var patRels = SwitchRels(bmAsset, spawn);
            Check(vanRels.SequenceEqual(patRels),
                $"switch offsets keep their in-statement position ({vanRels.Count} switch offset(s))");
        }
        else
        {
            Check(false, "vanilla BattleManager not found for the per-function comparison");
        }

        // ── the on-screen home-snap debug print ────
        var cmPath = FileCopy.FindUnder(dir, "AC_jRPG_CombatMovement.uasset");
        if (cmPath is not null && DataTableEditor.Mappings is { } mvMappings)
        {
            var cmvAsset = new UAsset(cmPath, EngineVersion.VER_UE5_4, mvMappings);
            var moveFn = cmvAsset.Exports.OfType<FunctionExport>()
                .FirstOrDefault(f => f.ObjectName.ToString() == "MoveToInitialLocation");
            Check(moveFn?.ScriptBytecode is not null, "MoveToInitialLocation found");

            if (moveFn?.ScriptBytecode is not null)
            {
                // The insert must sit BEFORE the ubergraph jump. That jump is the
                // only thing that can desynchronise if the print were to disturb
                // the latent-action parameter layout, and its target must be
                // untouched (the ubergraph itself is not edited).
                string? StackOf(UAssetAPI.Kismet.Bytecode.KismetExpression e)
                    => e is UAssetAPI.Kismet.Bytecode.Expressions.EX_CallMath cm
                        && cm.StackNode is not null && cm.StackNode.IsImport()
                        ? cmvAsset.Imports[Math.Abs(cm.StackNode.Index) - 1].ObjectName.ToString() : null;
                int jumpIdx = Array.FindIndex(moveFn.ScriptBytecode,
                    s => s is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalFinalFunction);
                Check(jumpIdx == 6
                        && moveFn.ScriptBytecode[6] is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalFinalFunction lff
                        && ((UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst)lff.Parameters[0]).Value == 9003,
                    "prints inserted before the ubergraph jump, which still targets 9003");
                Check(StackOf(moveFn.ScriptBytecode[4]) == "PrintString"
                        && StackOf(moveFn.ScriptBytecode[5]) == "PrintString",
                    "two debug prints present and adjacent");
                Check(!MayhemFixKit.WalkHas(moveFn.ScriptBytecode[4],
                        e => e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable)
                        && !MayhemFixKit.WalkHas(moveFn.ScriptBytecode[5],
                        e => e is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable),
                    "no foreign local-variable reference survives in either print");

                // The latent-action parameter writes before the insert must be
                // byte-identical to vanilla, and the jump/Return/EndOfScript tail
                // after it must be too: the print must be purely additive.
                var vanCmPath = Vanilla("AC_jRPG_CombatMovement.uasset");
                if (vanCmPath is not null)
                {
                    var vanCm = new UAsset(vanCmPath, EngineVersion.VER_UE5_4, mvMappings);
                    var vanMove = vanCm.Exports.OfType<FunctionExport>()
                        .First(f => f.ObjectName.ToString() == "MoveToInitialLocation");
                    byte[] Stmt(UAsset a, UAssetAPI.Kismet.Bytecode.KismetExpression st)
                    {
                        using var ms = new MemoryStream();
                        var w = new UAssetAPI.AssetBinaryWriter(ms, a);
                        UAssetAPI.Kismet.Bytecode.ExpressionSerializer.WriteExpression(st, w);
                        return ms.ToArray();
                    }
                    // Vanilla's stub is <latent writes> + jump + Return + EndOfScript,
                    // so its parameter writes are everything before that tail.
                    int vanJump = vanMove.ScriptBytecode!.Length - 3;
                    Check(jumpIdx == vanJump + 2,
                        "two statements inserted between the latent writes and the jump");
                    bool PrefixSame()
                    {
                        for (int i = 0; i < vanJump; i++)
                        {
                            var a = Stmt(vanCm, vanMove.ScriptBytecode![i]);
                            var b = Stmt(cmvAsset, moveFn.ScriptBytecode[i]);
                            if (!a.AsSpan().SequenceEqual(b.AsSpan())) return false;
                        }
                        return true;
                    }
                    var patTail = moveFn.ScriptBytecode.Skip(moveFn.ScriptBytecode.Length - 3)
                        .Select(s => Stmt(cmvAsset, s)).ToArray();
                    var vanTail = vanMove.ScriptBytecode!.Skip(vanMove.ScriptBytecode.Length - 3)
                        .Select(s => Stmt(vanCm, s)).ToArray();
                    bool tailSame = patTail.Length == vanTail.Length
                        && patTail.Select((b, i) => b.AsSpan().SequenceEqual(vanTail[i].AsSpan())).All(x => x);
                    Check(PrefixSame(), "latent-action parameter writes unchanged");
                    Check(tailSame, "ubergraph jump/Return/EndOfScript tail unchanged");

                    // The ubergraph itself must be byte-identical to vanilla: it
                    // is untouched, so no absolute offset in the package moved.
                    var vanUber = vanCm.Exports.OfType<FunctionExport>()
                        .FirstOrDefault(f => f.ObjectName.ToString().StartsWith("ExecuteUbergraph", StringComparison.Ordinal));
                    var patUber = cmvAsset.Exports.OfType<FunctionExport>()
                        .FirstOrDefault(f => f.ObjectName.ToString().StartsWith("ExecuteUbergraph", StringComparison.Ordinal));
                    if (vanUber?.ScriptBytecode is not null && patUber?.ScriptBytecode is not null)
                        Check(Code(vanCm, vanUber).AsSpan().SequenceEqual(Code(cmvAsset, patUber)),
                            "CombatMovement ubergraph byte-identical to vanilla (no offset moved)");

                    // Every other function in the asset must be untouched too.
                    int others = 0;
                    foreach (var vf in vanCm.Exports.OfType<FunctionExport>())
                    {
                        if (vf.ScriptBytecode is null) continue;
                        if (vf.ObjectName.ToString() == "MoveToInitialLocation") continue;
                        var pf = cmvAsset.Exports.OfType<FunctionExport>()
                            .FirstOrDefault(f => f.ObjectName.ToString() == vf.ObjectName.ToString());
                        if (pf?.ScriptBytecode is null) continue;
                        if (!Code(vanCm, vf).AsSpan().SequenceEqual(Code(cmvAsset, pf))) others++;
                    }
                    Check(others == 0,
                        $"only MoveToInitialLocation changed ({others} other function(s) differ)");
                }

                // Byte-stable round trip of the written asset.
                var cmBefore = File.ReadAllBytes(cmPath);
                var cmAgain = new UAsset(cmPath, EngineVersion.VER_UE5_4, mvMappings);
                cmAgain.Write(cmPath);
                Check(cmBefore.AsSpan().SequenceEqual(File.ReadAllBytes(cmPath)),
                    "CombatMovement re-serialization is byte-stable");
            }
        }
        else
        {
            Check(false, "staged CombatMovement not found");
        }
    }
    catch (Exception ex)
    {
        Check(false, "verification threw: " + ex.Message);
    }

    Console.WriteLine(fails == 0
        ? $"[party5-smoke] {checks} checks passed"
        : $"[party5-smoke] {fails}/{checks} FAILED");
    return fails == 0 ? 0 : 1;
}

static int CmdBattleGroups(string[] args)
{
    var modDir = Path.GetFullPath(GetOption(args, "--mod-dir")
        ?? Path.Combine(EditorPaths.RepoRoot, "work", "mod"));
    Directory.CreateDirectory(modDir);
    int max = int.TryParse(GetOption(args, "--max"), out var m) ? m : 5;
    double chance = double.TryParse(GetOption(args, "--chance"),
        System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out var c) ? c : 50;
    int seed = int.TryParse(GetOption(args, "--seed"), out var sd)
        ? sd : Environment.TickCount % 99_999_999;

    var s = new RandomizerKit.Settings
    {
        BattleGroups = true,
        MaxEnemiesPerBattle = max,
        ExtraEnemiesChance = chance,
        BattleGroupsAreaPrefix = GetOption(args, "--area") ?? "",
    };
    var catalog = new LootCatalog();
    int field = BattleGroupKit.ApplyCapacity(modDir, max, Console.WriteLine);
    int groups = BattleGroupKit.RandomizeEncounters(modDir, catalog, new Random(seed), s, Console.WriteLine);
    BattleGroupKit.VerifyCapacity(modDir, max, Console.WriteLine);
    BattleGroupKit.VerifyEncounterGroups(modDir, Console.WriteLine);
    Console.WriteLine($"[battle-groups] {field} field asset(s), {groups} encounter row(s) changed (seed {seed})");
    return 0;
}

static int CmdBattleGroupsSmoke(string[] args)
{
    var dir = Path.Combine(EditorPaths.TempSubdir("battle_groups_smoke"), "mod");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);

    int checks = 0, fails = 0;
    void Check(bool ok, string msg)
    {
        checks++;
        Console.WriteLine((ok ? "ok   " : "FAIL ") + msg);
        if (!ok) fails++;
    }

    const int capacity = 6;
    var catalog = new LootCatalog();
    var settings = new RandomizerKit.Settings
    {
        BattleGroups = true,
        MaxEnemiesPerBattle = capacity,
        ExtraEnemiesChance = 100,
        ShuffleBattleRosters = true,
    };

    try
    {
        int field = BattleGroupKit.ApplyCapacity(dir, capacity, m => Console.WriteLine(m));
        Check(field >= 3, $"field capacity patch applied to {field} asset(s)");
        BattleGroupKit.VerifyCapacity(dir, capacity, m => Console.WriteLine(m));
        Check(true, $"capacity verifier passes at {capacity} slots");

        // Re-applying must not duplicate aliases/offsets.
        BattleGroupKit.ApplyCapacity(dir, capacity, m => Console.WriteLine(m));
        BattleGroupKit.VerifyCapacity(dir, capacity, m => Console.WriteLine(m));
        Check(true, "capacity patch is idempotent");

        // The battle assets are shared with Party of Five. Apply the party over
        // this staged tree and make sure both sets of edits survive.
        var party = new PartyFiveKit.Settings { Enabled = true };
        PartyFiveKit.Apply(dir, party, m => Console.WriteLine(m));
        PartyFiveKit.Verify(dir, party);
        BattleGroupKit.VerifyCapacity(dir, capacity, m => Console.WriteLine(m));
        Check(true, "composes with Party of Five (both patches survive)");

        int groups = BattleGroupKit.RandomizeEncounters(dir, catalog, new Random(20261028), settings,
            m => Console.WriteLine(m));
        Check(groups > 0, $"randomized {groups} encounter row(s)");
        BattleGroupKit.VerifyEncounterGroups(dir, m => Console.WriteLine(m));
        Check(true, "encounter groups have contiguous keys and at least one >3");

        var enc = FileCopy.FindUnder(dir, "DT_jRPG_Encounters.uasset");
        Check(enc is not null, "DT_jRPG_Encounters staged");
        if (enc is not null)
        {
            var ed = new DataTableEditor(enc);
            int big = 0, overCap = 0;
            foreach (var row in ed.Table.Table.Data)
            {
                if (row.Value.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "Enemies")
                    is not UAssetAPI.PropertyTypes.Objects.MapPropertyData emap) continue;
                if (emap.Value.Count > 3) big++;
                if (emap.Value.Count > capacity) overCap++;
            }
            Check(big > 0, $"{big} group(s) with more than three enemies");
            Check(overCap == 0, $"no group exceeds the configured cap ({overCap} over)");
        }

        var fl = FileCopy.FindUnder(dir, "FL_jRPG_CustomFunctionLibrary.uasset")!;
        var flAsset = DataTableEditor.Mappings is { } map
            ? new UAsset(fl, EngineVersion.VER_UE5_4, map)
            : new UAsset(fl, EngineVersion.VER_UE5_4);
        var indexFn = flAsset.Exports.OfType<FunctionExport>()
            .First(f => f.ObjectName.ToString() == "GetEnemyIndexesLeftToRight");
        var table = indexFn.ScriptBytecode!
            .OfType<UAssetAPI.Kismet.Bytecode.Expressions.EX_SetArray>()
            .First().Elements.OfType<UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst>()
            .Select(e => e.Value).ToArray();
        Check(table.SequenceEqual(new[] { 2, 0, 1, 3, 4, 5 }),
            $"enemy order table = [{string.Join(",", table)}]");

        // The real entry point: RandomizerKit must run field capacity + encounter
        // groups even when every other randomizer source is off.
        var onlyGroupsDir = Path.Combine(EditorPaths.TempSubdir("battle_groups_smoke"), "randomizer-only");
        if (Directory.Exists(onlyGroupsDir)) Directory.Delete(onlyGroupsDir, true);
        Directory.CreateDirectory(onlyGroupsDir);
        var onlyGroups = new RandomizerKit.Settings
        {
            Chests = false, PaintCages = false, EnemyDrops = false, Merchants = false,
            WeaponPassives = false, MonsterStats = false, WeaponStats = false,
            BattleGroups = true, MaxEnemiesPerBattle = capacity, ExtraEnemiesChance = 100,
            ShuffleBattleRosters = true, Seed = 20261028,
        };
        int randoEdits = RandomizerKit.Apply(onlyGroupsDir, onlyGroups, catalog, null);
        BattleGroupKit.VerifyCapacity(onlyGroupsDir, capacity, m => Console.WriteLine(m));
        BattleGroupKit.VerifyEncounterGroups(onlyGroupsDir, m => Console.WriteLine(m), catalog);
        Check(randoEdits > 0, $"RandomizerKit alone applies battle groups ({randoEdits} edit(s))");
    }
    catch (Exception ex)
    {
        Check(false, "verification threw: " + ex.Message);
    }

    Console.WriteLine(fails == 0
        ? $"[battle-groups-smoke] {checks} checks passed"
        : $"[battle-groups-smoke] {fails}/{checks} FAILED");
    return fails == 0 ? 0 : 1;
}

static int CmdRandomBattles(string[] args)
{
    var modDir = Path.GetFullPath(GetOption(args, "--mod-dir")
        ?? Path.Combine(EditorPaths.RepoRoot, "work", "mod"));
    Directory.CreateDirectory(modDir);
    int max = int.TryParse(GetOption(args, "--max"), out var m) ? m : 5;
    int variants = int.TryParse(GetOption(args, "--variants"), out var v) ? v : 3;
    double chance = double.TryParse(GetOption(args, "--chance"),
        System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out var c) ? c : 50;
    int seed = int.TryParse(GetOption(args, "--seed"), out var sd)
        ? sd : Environment.TickCount % 99_999_999;

    var s = new RandomizerKit.Settings
    {
        RandomBattles = true,
        MaxEnemiesPerBattle = max,
        BattleVariants = variants,
        ExtraEnemiesChance = chance,
        BattleGroupsAreaPrefix = GetOption(args, "--area") ?? "",
    };
    var catalog = new LootCatalog();
    int field = BattleGroupKit.ApplyCapacity(modDir, max, Console.WriteLine);
    int edits = BattleGroupKit.BuildRuntimeBattles(modDir, catalog, new Random(seed), s, Console.WriteLine);
    BattleGroupKit.VerifyRuntimeBattles(modDir, Console.WriteLine);
    Console.WriteLine($"[random-battles] {field} field asset(s), {edits} runtime edit(s) (seed {seed})");
    return 0;
}

static int CmdRandomBattlesSmoke(string[] args)
{
    var dir = Path.Combine(EditorPaths.TempSubdir("random_battles_smoke"), "mod");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    Directory.CreateDirectory(dir);

    int checks = 0, fails = 0;
    void Check(bool ok, string msg)
    {
        checks++;
        Console.WriteLine((ok ? "ok   " : "FAIL ") + msg);
        if (!ok) fails++;
    }

    var catalog = new LootCatalog();
    var settings = new RandomizerKit.Settings
    {
        RandomBattles = true,
        MaxEnemiesPerBattle = 5,
        BattleVariants = 3,
        ExtraEnemiesChance = 100,
        ShuffleBattleRosters = true,
    };

    try
    {
        int field = BattleGroupKit.ApplyCapacity(dir, settings.MaxEnemiesPerBattle,
            m => Console.WriteLine(m));
        Check(field >= 3, $"field capacity applied to {field} asset(s)");

        int edits = BattleGroupKit.BuildRuntimeBattles(dir, catalog, new Random(20261029), settings,
            m => Console.WriteLine(m));
        Check(edits > 0, $"runtime variants + world enemy patch applied ({edits} edit(s))");
        BattleGroupKit.VerifyRuntimeBattles(dir, m => Console.WriteLine(m));
        Check(true, "runtime battle verifier passes");

        // Re-applying must not append duplicate handles or duplicate the
        // StartBattle locals.
        int again = BattleGroupKit.BuildRuntimeBattles(dir, catalog, new Random(20261029), settings,
            m => Console.WriteLine(m));
        Check(again == 0, $"second application is a no-op ({again} edit(s))");
        BattleGroupKit.VerifyRuntimeBattles(dir, m => Console.WriteLine(m));
        Check(true, "runtime battle verifier still passes after re-apply");

        var enc = FileCopy.FindUnder(dir, "DT_jRPG_Encounters.uasset");
        Check(enc is not null, "DT_jRPG_Encounters staged");
        if (enc is not null)
        {
            var ed = new DataTableEditor(enc);
            int variants = ed.Table.Table.Data.Count(r =>
                DataTableEditor.CleanName(r.Name).StartsWith(BattleGroupKit.VariantPrefix, StringComparison.Ordinal));
            Check(variants > 0, $"{variants} runtime variant row(s) created");
        }

        var gault = FileCopy.FindUnder(dir, "BP_jRPG_EnemyWorld_Gault.uasset");
        Check(gault is not null, "sample world enemy BP staged");
        if (gault is not null)
        {
            var asset = DataTableEditor.Mappings is { } map
                ? new UAsset(gault, EngineVersion.VER_UE5_4, map)
                : new UAsset(gault, EngineVersion.VER_UE5_4);
            var cdo = asset.Exports.OfType<NormalExport>()
                .First(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
            var arr = cdo.Data.OfType<UAssetAPI.PropertyTypes.Objects.ArrayPropertyData>()
                .First(p => DataTableEditor.CleanName(p.Name) == "PossibleEncounters");
            int n = arr.Value.Length;
            bool hasVariant = arr.Value.OfType<UAssetAPI.PropertyTypes.Structs.StructPropertyData>()
                .Any(s => s.Value.OfType<UAssetAPI.PropertyTypes.Objects.NamePropertyData>()
                    .Any(p => p.Value?.ToString().StartsWith(BattleGroupKit.VariantPrefix, StringComparison.Ordinal) == true));
            Check(n >= 4, $"Gault PossibleEncounters grew to {n} (1 vanilla + 3 variants)");
            Check(hasVariant, "Gault PossibleEncounters contains a RAND_ variant");
        }

        var baseBp = FileCopy.FindUnder(dir, "BP_jRPG_Enemy_World_Base_Seamless.uasset");
        Check(baseBp is not null, "world enemy base staged");
        if (baseBp is not null)
        {
            var asset = DataTableEditor.Mappings is { } map
                ? new UAsset(baseBp, EngineVersion.VER_UE5_4, map)
                : new UAsset(baseBp, EngineVersion.VER_UE5_4);
            var fn = asset.Exports.OfType<FunctionExport>()
                .First(f => f.ObjectName.ToString() == "StartBattle");
            bool local = (fn.LoadedProperties ?? Array.Empty<UAssetAPI.FieldTypes.FProperty>())
                .Any(p => p.Name.ToString() == "RAND_ArrayRandom_OutItem");
            bool arrayRandom = false;
            MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
            {
                if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_FinalFunction ff
                    && ff.StackNode.IsImport()
                    && ff.StackNode.ToImport(asset).ObjectName.ToString() == "Array_Random"
                    && ff.Parameters.Length > 0
                    && ff.Parameters[0] is UAssetAPI.Kismet.Bytecode.Expressions.EX_InstanceVariable iv
                    && PartyFiveKit.FieldName(iv.Variable) == "PossibleEncounters")
                    arrayRandom = true;
            });
            Check(local, "StartBattle has the RAND_ArrayRandom_OutItem local");
            Check(arrayRandom, "StartBattle re-rolls PossibleEncounters");
            var starts = new HashSet<uint>(PartyFiveKit.StatementStarts(asset, fn.ScriptBytecode!));
            uint total = 0;
            foreach (var st in fn.ScriptBytecode!) total += (uint)st.GetSize(asset);
            int bad = 0;
            uint pos = 0;
            foreach (var st in fn.ScriptBytecode!)
                st.Visit(asset, ref pos, (e, _) =>
                {
                    switch (e)
                    {
                        case UAssetAPI.Kismet.Bytecode.Expressions.EX_Jump j when !starts.Contains(j.CodeOffset): bad++; break;
                        case UAssetAPI.Kismet.Bytecode.Expressions.EX_JumpIfNot jn when !starts.Contains(jn.CodeOffset): bad++; break;
                        case UAssetAPI.Kismet.Bytecode.Expressions.EX_PushExecutionFlow pf when !starts.Contains(pf.PushingAddress): bad++; break;
                        case UAssetAPI.Kismet.Bytecode.Expressions.EX_SwitchValue sv:
                            if (sv.EndGotoOffset == 0 || sv.EndGotoOffset > total) bad++;
                            foreach (var c in sv.Cases) if (c.NextOffset == 0 || c.NextOffset > total) bad++;
                            break;
                    }
                });
            Check(bad == 0, $"StartBattle jumps valid ({bad} bad)");
        }
    }
    catch (Exception ex)
    {
        Check(false, "verification threw: " + ex.Message);
    }

    Console.WriteLine(fails == 0
        ? $"[random-battles-smoke] {checks} checks passed"
        : $"[random-battles-smoke] {fails}/{checks} FAILED");
    return fails == 0 ? 0 : 1;
}

static int CmdCompatPatch(string[] args)
{
    var utoc = GetOption(args, "--utoc") ?? CompatibilityKit.Detect();
    if (utoc is null) { Console.Error.WriteLine("error: Bigger Party Mod not found (pass --utoc)"); return 1; }
    var tweaks = new GameplayTweaksKit.Settings();
    var tweaksPath = Path.Combine(EditorPaths.RepoRoot, "work", "gameplay_tweaks.json");
    if (File.Exists(tweaksPath))
        tweaks.FromJson(Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(tweaksPath)));
    var edits = CompatibilityKit.Apply(utoc, tweaks, Console.WriteLine);
    Console.WriteLine($"[compat] done, {edits} edit(s)");
    return 0;
}

/// <summary>
/// Hand-rolled probe: clones Fencers Flurry into a brand-new Maelle skill
/// "Charming Shot" that applies BP_BattleBuff_Charmed instead of Defenseless,
/// wires it into DA_SkillGraph_Maelle, adds its balancing rows, packs E33Probe_P.
/// </summary>
static int CmdSkillProbe(string[] args)
{
    const string DaDonor = "DA_Skill_Maelle_FencersFlurry";
    const string BpDonor = "BP_Battle_SkillScript_FencersFlurry";
    const string DaNew = "DA_Skill_Maelle_CharmingShot";
    const string BpNew = "BP_Battle_SkillScript_CharmingShot";

    var repo = EditorPaths.RepoRoot;
    var probeDir = Path.Combine(repo, "work", "probe-skill");
    var stage = Path.Combine(repo, "work", "probe-charm", "mod");
    if (Directory.Exists(stage)) Directory.Delete(stage, true);
    Directory.CreateDirectory(stage);

    var map = DataTableEditor.Mappings;
    UAssetAPI.UAsset Open(string p) => map != null
        ? new UAssetAPI.UAsset(p, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
        : new UAssetAPI.UAsset(p, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);

    string StageDest(string srcUasset, string newStem)
    {
        var full = srcUasset.Replace('\\', '/');
        const string marker = "/Content/";
        var idx = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) throw new InvalidDataException("not under Content/: " + srcUasset);
        var rel = full[(idx + marker.Length)..];
        rel = rel.Replace(Path.GetFileNameWithoutExtension(srcUasset), newStem, StringComparison.Ordinal);
        return Path.Combine(stage, "Sandfall", "Content", rel);
    }

    // ---------- sources ----------
    var donorDa = FileCopy.FindUnder(probeDir, DaDonor + ".uasset")
        ?? throw new FileNotFoundException(DaDonor + ".uasset (run: extract DA_Skill_Maelle --out work/probe-skill)");
    var donorBp = FileCopy.FindUnder(probeDir, BpDonor + ".uasset")
        ?? throw new FileNotFoundException(BpDonor + ".uasset (run: extract BP_Battle_SkillScript_FencersFlurry --out work/probe-skill)");
    var graphSrc = FileCopy.FindTablesAsset("DA_SkillGraph_Maelle.uasset")
        ?? throw new FileNotFoundException("DA_SkillGraph_Maelle.uasset in work/tables/graphs");
    var balMod = Path.Combine(repo, "work", "mod", "Sandfall", "Content", "Gameplay", "Battle",
        "ScalingSystem", "BalancingTables", "Content", "DT_Balancing_MainCharacterSkills.uasset");
    var balSrc = File.Exists(balMod) ? balMod
        : FileCopy.FindTablesAsset("DT_Balancing_MainCharacterSkills.uasset")
        ?? throw new FileNotFoundException("DT_Balancing_MainCharacterSkills.uasset");

    var daDest = StageDest(donorDa, DaNew);
    var bpDest = StageDest(donorBp, BpNew);
    var gDest = StageDest(graphSrc, "DA_SkillGraph_Maelle");
    var bDest = StageDest(balSrc, "DT_Balancing_MainCharacterSkills");
    Directory.CreateDirectory(Path.GetDirectoryName(daDest)!);
    Directory.CreateDirectory(Path.GetDirectoryName(bpDest)!);
    Directory.CreateDirectory(Path.GetDirectoryName(gDest)!);
    Directory.CreateDirectory(Path.GetDirectoryName(bDest)!);

    // ---------- 1. skill data asset ----------
    FileCopy.AssetPair(donorDa, daDest);
    var da = Open(daDest);
    Retarget(da, daDest, "FencersFlurry", "CharmingShot");
    foreach (var ex in da.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>())
        foreach (var p in ex.Data)
        {
            if (p.Name.ToString().Equals("SkillScript", StringComparison.OrdinalIgnoreCase) && p is UAssetAPI.PropertyTypes.Objects.SoftObjectPropertyData soft)
            {
                var pkg = "/Game/Gameplay/Battle/Skills/Content/Maelle/" + BpNew;
                soft.Value = new UAssetAPI.PropertyTypes.Objects.FSoftObjectPath(
                    new UAssetAPI.UnrealTypes.FName(da, pkg),
                    new UAssetAPI.UnrealTypes.FName(da, BpNew + "_C"), null);
                if (da.SoftObjectPathList is { Count: > 0 } list) list.Add(soft.Value);
            }
            else if (p.Name.ToString() == "Name" && p is UAssetAPI.PropertyTypes.Objects.TextPropertyData tname)
                SetInvariant(tname, "Charming Shot");
            else if (p.Name.ToString() == "Description" && p is UAssetAPI.PropertyTypes.Objects.TextPropertyData tdesc)
                SetInvariant(tdesc, "Deal physical damage and Charm the target.");
            else if (p.Name.ToString() == "ShortDescription" && p is UAssetAPI.PropertyTypes.Objects.TextPropertyData tshort)
                SetInvariant(tshort, "Deal damage and Charm.");
            else if (p.Name.ToString() == "TargetingType" && p is UAssetAPI.PropertyTypes.Objects.BytePropertyData tb)
                tb.EnumValue = new UAssetAPI.UnrealTypes.FName(da, "NewEnumerator1"); // single-enemy
            else if (p.Name.ToString() == "TargetingType" && p is UAssetAPI.PropertyTypes.Objects.EnumPropertyData te)
                te.Value = new UAssetAPI.UnrealTypes.FName(da, "NewEnumerator1"); // single-enemy
        }
    da.Write(daDest);
    Console.WriteLine($"[da]   {DaNew} written");

    // ---------- 2. skill script blueprint ----------
    FileCopy.AssetPair(donorBp, bpDest);
    var bp = Open(bpDest);
    // SURGICAL renames only. The script imports external assets whose names
    // embed the donor stem (SEQ_SkillMaelle_FencersFlurry, VFX, ...) — a
    // blanket sweep rewrites those into nonexistent packages and the battle
    // freezes waiting for a sequence that will never load.
    RetargetPairs(bp, bpDest,
        ("BP_Battle_SkillScript_FencersFlurry", BpNew),
        ("FencersFlurry_BaseDamage", "CharmingShot_BaseDamage"),
        ("FencersFlurry_BreakBarMultiplier", "CharmingShot_BreakBarMultiplier"),
        ("FencersFlurry_DefenselessDuration", "CharmingShot_CharmDuration"));

    int oldImp = -1;
    for (int i = 0; i < bp.Imports.Count; i++)
        if (bp.Imports[i].ObjectName.ToString() == "BP_BattleBuff_Defenseless_C") { oldImp = i; break; }
    if (oldImp < 0) throw new InvalidDataException("donor BP has no BP_BattleBuff_Defenseless_C import");
    var srcImp = bp.Imports[oldImp];
    var buffClass = GetOption(args, "--buff") ?? "BP_BattleBuff_Charmed_C";
    int oldRaw = UAssetAPI.UnrealTypes.FPackageIndex.FromImport(oldImp).Index;
    var buffObj = buffClass.EndsWith("_C", StringComparison.Ordinal) ? buffClass[..^2] : buffClass;
    var charmedPkg = "/Game/Gameplay/Buffs/GenericBuff/" + buffObj;
    var charmedIdx = EnsureObjectImport(bp, charmedPkg, buffClass,
        srcImp.ClassName.ToString(), srcImp.ClassPackage.ToString());
    Console.WriteLine($"[bp]   buff class: {buffClass}");
    int patched = 0;
    foreach (var fn in bp.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>())
    {
        if (fn.ScriptBytecode is null) continue;
        WalkExprs(fn.ScriptBytecode, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_ObjectConst oc && oc.Value.Index == oldRaw)
            {
                oc.Value = charmedIdx;
                patched++;
            }
        });
    }
    // defensive: any tagged-property object refs to the old class import
    foreach (var ex in bp.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>())
        PatchObjectRefs(ex.Data, oldRaw, charmedIdx.Index);
    bp.Write(bpDest);
    Console.WriteLine($"[bp]   {BpNew} written ({patched} ApplyBuff class refs retargeted to Charmed)");

    // ---------- 3. balancing rows ----------
    FileCopy.AssetPair(balSrc, bDest);
    var balEd = new DataTableEditor(bDest);
    (string donor, string row, float val)[] rows =
    {
        ("FencersFlurry_BaseDamage", "CharmingShot_BaseDamage", 2.0f),
        ("FencersFlurry_BreakBarMultiplier", "CharmingShot_BreakBarMultiplier", 1.0f),
        ("FencersFlurry_DefenselessDuration", "CharmingShot_CharmDuration", 2.0f),
    };
    foreach (var (donorRow, newRow, val) in rows)
    {
        balEd.CloneRow(donorRow, newRow);
        if (!balEd.TryGetRow(newRow, out var row))
            throw new KeyNotFoundException($"row '{newRow}' missing after clone");
        foreach (var child in row.Value)
        {
            switch (child)
            {
                case UAssetAPI.PropertyTypes.Objects.FloatPropertyData f: f.Value = val; break;
                case UAssetAPI.PropertyTypes.Objects.DoublePropertyData d: d.Value = val; break;
            }
        }
    }
    balEd.Save(bDest);
    Console.WriteLine("[bal]  CharmingShot_BaseDamage=2.0 BreakBarMultiplier=1.0 CharmDuration=2.0");

    // ---------- 4. graph node ----------
    // --replace <DA_Stem>: repoint an EXISTING connected node at the new skill
    // (randomizer-proven SetNode technique). Default mode APPENDS a new node;
    // appending renders in-game but the tree currently treats it as hidden.
    string replaceStem = null;
    for (int i = 1; i + 1 < args.Length; i++)
        if (args[i] == "--replace") replaceStem = args[i + 1];

    FileCopy.AssetPair(graphSrc, gDest);
    var g = Open(gDest);
    var gx = g.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>().First();
    var nodes = (UAssetAPI.PropertyTypes.Objects.ArrayPropertyData)gx.Data[0];
    var edges = (UAssetAPI.PropertyTypes.Objects.ArrayPropertyData)gx.Data[1];

    UAssetAPI.UnrealTypes.FPackageIndex fleuretImp = null;
    int replaceNode = -1;
    UAssetAPI.UnrealTypes.FPackageIndex replaceImp = null;
    for (int i = 0; i < nodes.Value.Length; i++)
    {
        var nodeStruct = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)nodes.Value[i];
        var su = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)nodeStruct.Value[0];
        var skillPtr = ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)su.Value[0]).Value;
        var impName = g.Imports[Math.Abs(skillPtr.Index) - 1].ObjectName.ToString();
        if (impName == "DA_Skill_Maelle_FleuretFury")
            fleuretImp = skillPtr;
        if (replaceStem != null && impName == replaceStem)
        {
            replaceNode = i;
            replaceImp = skillPtr;
        }
    }
    if (fleuretImp is null && replaceStem is null)
        throw new InvalidDataException("Fleuret Fury node not found in Maelle graph");
    if (replaceStem != null && replaceNode < 0)
        throw new InvalidDataException($"--replace node '{replaceStem}' not found in Maelle graph");
    var fleuretImport = g.Imports[Math.Abs(fleuretImp.Index) - 1];
    var templateImp = g.Imports[Math.Abs((replaceNode >= 0
        ? ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)((UAssetAPI.PropertyTypes.Structs.StructPropertyData)((UAssetAPI.PropertyTypes.Structs.StructPropertyData)nodes.Value[replaceNode]).Value[0]).Value[0]).Value
        : fleuretImp).Index) - 1];
    // vanilla convention: each skill DA is imported under a Package whose
    // ObjectName is the FULL asset path (see FleuretFury import [42])
    var daPkgPath = "/Game/Gameplay/SkillTree/Content/Maelle/Skills/" + DaNew;
    var newDaIdx = EnsureObjectImport(g, daPkgPath, DaNew,
        templateImp.ClassName.ToString(), templateImp.ClassPackage.ToString());

    if (replaceStem != null)
    {
        // keep the host node's cost/position — swap its skill AND every edge
        // endpoint that referenced the old skill (randomizer ToAsset semantics)
        var hostStruct = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)nodes.Value[replaceNode];
        var suH = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)hostStruct.Value[0];
        ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)suH.Value[0]).Value = newDaIdx;
        int swapped = 0;
        foreach (var e in edges.Value)
        {
            var es = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)e;
            for (int s = 0; s < 2; s++)
            {
                var ep = (UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)es.Value[s];
                if (ep.Value is { } v && v.Index == replaceImp.Index)
                {
                    ep.Value = newDaIdx;
                    swapped++;
                }
            }
        }
        Console.WriteLine($"[graph] node #{replaceNode} ({replaceStem}) now hosts {DaNew}; {swapped} edge endpoints repointed");
    }
    else
    {
    // template from a PLAIN node (node 0), not the trailing gradient node —
    // gradient nodes carry item-locked semantics we must not inherit
    var node = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)nodes.Value[0].Clone();
    node.Value[0] = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)node.Value[0].Clone();
    node.Value[1] = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)node.Value[1].Clone();
    var suN = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)node.Value[0];
    ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)suN.Value[0]).Value = newDaIdx;
    ((UAssetAPI.PropertyTypes.Objects.IntPropertyData)suN.Value[1]).Value = 2;
    ((UAssetAPI.PropertyTypes.Objects.BoolPropertyData)suN.Value[2]).Value = false;
    var req = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)suN.Value[3];
    ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)req.Value[0]).Value = null;
    ((UAssetAPI.PropertyTypes.Objects.NamePropertyData)req.Value[1]).Value =
        UAssetAPI.UnrealTypes.FName.FromString(g, "None");
    ((UAssetAPI.PropertyTypes.Objects.BoolPropertyData)suN.Value[4]).Value = false;
    var posWrap = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)node.Value[1];
    ((UAssetAPI.PropertyTypes.Structs.Vector2DPropertyData)posWrap.Value[0]).Value =
        new UAssetAPI.UnrealTypes.FVector2D(925.0, 525.0);
    nodes.Value = nodes.Value.Append(node).ToArray();

    var edge = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)edges.Value[0].Clone();
    // vanilla direction: FromNode = prerequisite, ToNode = dependent
    ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)edge.Value[0]).Value = fleuretImp;
    ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)edge.Value[1]).Value = newDaIdx;
    edges.Value = edges.Value.Append(edge).ToArray();
    Console.WriteLine($"[graph] appended node #{nodes.Value.Length - 1} = {DaNew} (cost 2, linked to FleuretFury), {edges.Value.Length} edges");
    }
    g.Write(gDest);

    // ---------- 5. verify staged output ----------
    VerifyProbe(daDest, bpDest, bDest, gDest, Open, buffClass);

    // ---------- 6. pack ----------
    var packed = new Retoc().PackCombined(stage, "E33Probe_P");
    Console.WriteLine("[pack] E33Probe_P installed:");
    foreach (var f in packed) Console.WriteLine("       " + f);
    return 0;
}

static void SetInvariant(UAssetAPI.PropertyTypes.Objects.TextPropertyData t, string display)
{
    t.HistoryType = UAssetAPI.PropertyTypes.Objects.TextHistoryType.None;
    t.Flags = UAssetAPI.PropertyTypes.Objects.ETextFlag.CultureInvariant;
    t.Namespace = null;
    t.Value = new UAssetAPI.UnrealTypes.FString(display);
    t.CultureInvariantString = new UAssetAPI.UnrealTypes.FString(display);
    t.TableId = null;
}

static void RenameNameRef(UAssetAPI.UAsset asset, string from, string to)
{
    var idx = asset.SearchNameReference(UAssetAPI.UnrealTypes.FString.FromString(from));
    if (idx >= 0) asset.SetNameReference(idx, UAssetAPI.UnrealTypes.FString.FromString(to));
}
/// <summary>
/// Retargets the package to its destination path and applies ONLY the given
/// name-map replacements. Unlike a blanket stem sweep this leaves external
/// references (sequences, VFX, audio) that happen to embed the donor stem
/// untouched — those packages still exist and must keep resolving.
/// </summary>
static void RetargetPairs(UAssetAPI.UAsset asset, string destUasset, params (string from, string to)[] pairs)
{
    var full = destUasset.Replace('\\', '/');
    const string marker = "/Content/";
    var mIdx = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
    asset.FolderName = UAssetAPI.UnrealTypes.FString.FromString("/Game/" + full[(mIdx + marker.Length)..][..^".uasset".Length]);
    var names = asset.GetNameMapIndexList();
    for (int i = 0; i < names.Count; i++)
    {
        var s = names[i]?.Value;
        if (string.IsNullOrEmpty(s)) continue;
        var next = s;
        foreach (var (from, to) in pairs)
            next = next.Replace(from, to, StringComparison.Ordinal);
        if (next != s) asset.SetNameReference(i, UAssetAPI.UnrealTypes.FString.FromString(next));
    }
}

static void Retarget(UAssetAPI.UAsset asset, string destUasset, string srcStem, string newStem)
{
    var full = destUasset.Replace('\\', '/');
    const string marker = "/Content/";
    var mIdx = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
    var newPackage = "/Game/" + full[(mIdx + marker.Length)..][..^".uasset".Length];
    var oldPackage = asset.FolderName?.Value;
    asset.FolderName = UAssetAPI.UnrealTypes.FString.FromString(newPackage);
    var names = asset.GetNameMapIndexList();
    for (int i = 0; i < names.Count; i++)
    {
        var s = names[i]?.Value;
        if (string.IsNullOrEmpty(s)) continue;
        var next = s;
        if (!string.IsNullOrEmpty(oldPackage))
            next = next.Replace(oldPackage, newPackage, StringComparison.Ordinal);
        if (!next.Contains(newStem, StringComparison.Ordinal))
            next = next.Replace(srcStem, newStem, StringComparison.Ordinal);
        if (next != s) asset.SetNameReference(i, UAssetAPI.UnrealTypes.FString.FromString(next));
    }
}

static UAssetAPI.UnrealTypes.FPackageIndex EnsureObjectImport(
    UAssetAPI.UAsset asset, string packagePath, string objectName, string className, string classPackage)
{
    UAssetAPI.UnrealTypes.FPackageIndex pkgIndex = null;
    for (int i = 0; i < asset.Imports.Count; i++)
    {
        var imp = asset.Imports[i];
        if (imp.ObjectName.ToString() == packagePath && imp.ClassName.ToString() == "Package")
            pkgIndex = UAssetAPI.UnrealTypes.FPackageIndex.FromImport(i);
        if (imp.ObjectName.ToString() == objectName && imp.ClassName.ToString() == className)
            return UAssetAPI.UnrealTypes.FPackageIndex.FromImport(i);
    }
    pkgIndex ??= asset.AddImport(new UAssetAPI.Import("/Script/CoreUObject", "Package",
        UAssetAPI.UnrealTypes.FPackageIndex.FromRawIndex(0), packagePath, false, asset));
    return asset.AddImport(new UAssetAPI.Import(classPackage, className, pkgIndex, objectName, false, asset));
}

/// <summary>
/// Expands Lune's skill tree: clones DA_Skill_Lune_Wildfire into the new skill
/// "TestMoveNewFire" (unique DA + skill-script BP + balancing rows with 50x
/// base damage, plus its own DT_SkillIcons row so the tree renders an icon),
/// APPENDS a linked, non-secret node to DA_SkillGraph_Lune (Wildfire is the
/// prerequisite), stages everything into work/mod and packs ExpeditionInfinity_<seed>_P.
/// </summary>
static int CmdLuneWildfire(string[] args)
{
    const string DaNew = "DA_Skill_Lune_TestMoveNewFire";
    const string BpNew = "BP_Battle_SkillScript_TestMoveNewFire";

    var repo = EditorPaths.RepoRoot;
    var probeDir = Path.Combine(repo, "work", "probe-wildfire");
    var modDir = Path.Combine(repo, "work", "mod");
    if (!Directory.Exists(modDir)) Directory.CreateDirectory(modDir);

    var map = DataTableEditor.Mappings;
    UAssetAPI.UAsset Open(string p) => map != null
        ? new UAssetAPI.UAsset(p, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
        : new UAssetAPI.UAsset(p, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);

    string StageDest(string srcUasset, string newStem)
    {
        var full = srcUasset.Replace('\\', '/');
        const string marker = "/Content/";
        var idx = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) throw new InvalidDataException("not under Content/: " + srcUasset);
        var rel = full[(idx + marker.Length)..];
        rel = rel.Replace(Path.GetFileNameWithoutExtension(srcUasset), newStem, StringComparison.Ordinal);
        return Path.Combine(modDir, "Sandfall", "Content", rel);
    }

    // ---------- sources (vanilla tables + the ACTIVE mod balancing table) ----------
    var donorDa = FileCopy.FindUnder(Path.Combine(repo, "work", "tables"),
        "DA_Skill_Lune_Wildfire.uasset")
        ?? throw new FileNotFoundException("DA_Skill_Lune_Wildfire.uasset under work/tables");
    var donorBp = FileCopy.FindUnder(probeDir, "BP_Battle_SkillScript_Wildfire.uasset")
        ?? throw new FileNotFoundException("BP_Battle_SkillScript_Wildfire.uasset (run: extract BP_Battle_SkillScript_Wildfire --out work/probe-wildfire)");
    var graphSrc = FileCopy.FindTablesAsset("DA_SkillGraph_Lune.uasset")
        ?? throw new FileNotFoundException("DA_SkillGraph_Lune.uasset in work/tables/graphs");
    var iconSrc = FileCopy.FindUnder(probeDir, "DT_SkillIcons.uasset")
        ?? throw new FileNotFoundException("DT_SkillIcons.uasset (run: extract DT_SkillIcons --out work/probe-wildfire)");
    // Active table first (carries gameplay-tweak rows); vanilla only as fallback.
    var balMod = Path.Combine(modDir, "Sandfall", "Content", "Gameplay", "Battle",
        "ScalingSystem", "BalancingTables", "Content", "DT_Balancing_MainCharacterSkills.uasset");
    var balSrc = File.Exists(balMod) ? balMod
        : FileCopy.FindTablesAsset("DT_Balancing_MainCharacterSkills.uasset")
        ?? throw new FileNotFoundException("DT_Balancing_MainCharacterSkills.uasset");

    var daDest = StageDest(donorDa, DaNew);
    var bpDest = StageDest(donorBp, BpNew);
    var gDest = StageDest(graphSrc, "DA_SkillGraph_Lune");
    var bDest = StageDest(balSrc, "DT_Balancing_MainCharacterSkills");
    var iDest = StageDest(iconSrc, "DT_SkillIcons");
    foreach (var d in new[] { daDest, bpDest, gDest, bDest, iDest })
        Directory.CreateDirectory(Path.GetDirectoryName(d)!);

    // ---------- 1. skill data asset ----------
    FileCopy.AssetPair(donorDa, daDest);
    var da = Open(daDest);
    Retarget(da, daDest, "Wildfire", "TestMoveNewFire");
    foreach (var ex in da.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>())
        foreach (var p in ex.Data)
        {
            if (p.Name.ToString().Equals("SkillScript", StringComparison.OrdinalIgnoreCase) && p is UAssetAPI.PropertyTypes.Objects.SoftObjectPropertyData soft)
            {
                var pkg = "/Game/Gameplay/Battle/Skills/Content/Lune/" + BpNew;
                soft.Value = new UAssetAPI.PropertyTypes.Objects.FSoftObjectPath(
                    new UAssetAPI.UnrealTypes.FName(da, pkg),
                    new UAssetAPI.UnrealTypes.FName(da, BpNew + "_C"), null);
                if (da.SoftObjectPathList is { Count: > 0 } list) list.Add(soft.Value);
            }
            else if (p.Name.ToString() == "Name" && p is UAssetAPI.PropertyTypes.Objects.TextPropertyData tname)
                SetInvariant(tname, "TestMoveNewFire");
            else if (p.Name.ToString() == "Description" && p is UAssetAPI.PropertyTypes.Objects.TextPropertyData tdesc)
                SetInvariant(tdesc, "Wildfire clone: 50x base damage. Test move for tree expansion.");
            else if (p.Name.ToString() == "ShortDescription" && p is UAssetAPI.PropertyTypes.Objects.TextPropertyData tshort)
                SetInvariant(tshort, "50x Wildfire damage.");
        }
    da.Write(daDest);
    Console.WriteLine($"[da]   {DaNew} written");

    // ---------- 2. skill script blueprint (surgical renames only) ----------
    FileCopy.AssetPair(donorBp, bpDest);
    var bp = Open(bpDest);
    RetargetPairs(bp, bpDest,
        ("BP_Battle_SkillScript_Wildfire", BpNew),
        ("Wildfire_BaseDamage", "TestMoveNewFire_BaseDamage"),
        ("Wildfire_BreakBarMultiplier", "TestMoveNewFire_BreakBarMultiplier"),
        ("Wildfire_BurnOnHit", "TestMoveNewFire_BurnOnHit"),
        ("Wildfire_OverchargeDamageMultiplier", "TestMoveNewFire_OverchargeDamageMultiplier"),
        ("Wildfire_SelfDamageMultiplier", "TestMoveNewFire_SelfDamageMultiplier"));
    bp.Write(bpDest);
    Console.WriteLine($"[bp]   {BpNew} written (SEQ_LuneSkill_WildFire import kept)");

    // ---------- 3. balancing rows (clone from the ACTIVE table; base damage x50) ----------
    FileCopy.AssetPair(balSrc, bDest);
    var balEd = new DataTableEditor(bDest);
    (string donor, string row, float? val)[] rows =
    {
        ("Wildfire_BaseDamage", "TestMoveNewFire_BaseDamage", 62.5f), // 50 * vanilla 1.25
        ("Wildfire_BreakBarMultiplier", "TestMoveNewFire_BreakBarMultiplier", null),
        ("Wildfire_BurnOnHit", "TestMoveNewFire_BurnOnHit", null),
        ("Wildfire_OverchargeDamageMultiplier", "TestMoveNewFire_OverchargeDamageMultiplier", null),
        ("Wildfire_SelfDamageMultiplier", "TestMoveNewFire_SelfDamageMultiplier", null),
    };
    foreach (var (donorRow, newRow, val) in rows)
    {
        balEd.CloneRow(donorRow, newRow);
        if (!balEd.TryGetRow(newRow, out var row))
            throw new KeyNotFoundException($"row '{newRow}' missing after clone");
        if (val is not { } v) continue;
        foreach (var child in row.Value)
        {
            switch (child)
            {
                case UAssetAPI.PropertyTypes.Objects.FloatPropertyData f: f.Value = v; break;
                case UAssetAPI.PropertyTypes.Objects.DoublePropertyData d: d.Value = v; break;
            }
        }
    }
    balEd.Save(bDest);
    Console.WriteLine("[bal]  TestMoveNewFire_BaseDamage=62.5 (50x vanilla 1.25); 4 aux rows cloned unchanged");

    // ---------- 4. skill icon row (the tree hides skills with no DT_SkillIcons row) ----------
    FileCopy.AssetPair(iconSrc, iDest);
    var iconEd = new DataTableEditor(iDest);
    iconEd.CloneRow("Wildfire", "TestMoveNewFire");
    if (!iconEd.TryGetRow("TestMoveNewFire", out var iconRow))
        throw new KeyNotFoundException("icon row missing after clone");
    iconEd.Save(iDest);
    Console.WriteLine("[icon] DT_SkillIcons row TestMoveNewFire -> " +
        string.Join("; ", iconRow.Value.Select(c => c.ToString())));

    // ---------- 5. graph node (APPEND, linked from Wildfire, visible) ----------
    FileCopy.AssetPair(graphSrc, gDest);
    var g = Open(gDest);
    var gx = g.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>().First();
    var nodes = (UAssetAPI.PropertyTypes.Objects.ArrayPropertyData)gx.Data[0];
    var edges = (UAssetAPI.PropertyTypes.Objects.ArrayPropertyData)gx.Data[1];

    UAssetAPI.UnrealTypes.FPackageIndex wildfireImp = null;
    UAssetAPI.Import templateImp = null;
    for (int i = 0; i < nodes.Value.Length; i++)
    {
        var nodeStruct = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)nodes.Value[i];
        var su = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)nodeStruct.Value[0];
        var skillPtr = ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)su.Value[0]).Value;
        var impName = g.Imports[Math.Abs(skillPtr.Index) - 1].ObjectName.ToString();
        if (impName == "DA_Skill_Lune_Wildfire")
        {
            wildfireImp = skillPtr;
            templateImp = g.Imports[Math.Abs(skillPtr.Index) - 1];
        }
    }
    if (wildfireImp is null)
        throw new InvalidDataException("Wildfire node not found in Lune graph");
    var daPkgPath = "/Game/Gameplay/SkillTree/Content/Lune/Skills/" + DaNew;
    var newDaIdx = EnsureObjectImport(g, daPkgPath, DaNew,
        templateImp.ClassName.ToString(), templateImp.ClassPackage.ToString());

    // template from a PLAIN node (Wildfire = node 0), not a gradient node
    var node = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)nodes.Value[0].Clone();
    node.Value[0] = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)node.Value[0].Clone();
    node.Value[1] = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)node.Value[1].Clone();
    var suN = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)node.Value[0];
    ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)suN.Value[0]).Value = newDaIdx;
    ((UAssetAPI.PropertyTypes.Objects.IntPropertyData)suN.Value[1]).Value = 2;
    ((UAssetAPI.PropertyTypes.Objects.BoolPropertyData)suN.Value[2]).Value = false;
    var req = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)suN.Value[3];
    ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)req.Value[0]).Value = null;
    ((UAssetAPI.PropertyTypes.Objects.NamePropertyData)req.Value[1]).Value =
        UAssetAPI.UnrealTypes.FName.FromString(g, "None");
    ((UAssetAPI.PropertyTypes.Objects.BoolPropertyData)suN.Value[4]).Value = false;
    var posWrap = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)node.Value[1];
    ((UAssetAPI.PropertyTypes.Structs.Vector2DPropertyData)posWrap.Value[0]).Value =
        new UAssetAPI.UnrealTypes.FVector2D(350.0, 575.0);
    nodes.Value = nodes.Value.Append(node).ToArray();

    var edge = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)edges.Value[0].Clone();
    // vanilla direction: FromNode = prerequisite, ToNode = dependent
    ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)edge.Value[0]).Value = wildfireImp;
    ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)edge.Value[1]).Value = newDaIdx;
    edges.Value = edges.Value.Append(edge).ToArray();
    Console.WriteLine($"[graph] appended node #{nodes.Value.Length - 1} = {DaNew} (cost 2 @ (350,575), linked Wildfire -> {DaNew}, IsSecret=false), {edges.Value.Length} edges");
    g.Write(gDest);

    // ---------- 6. verify staged output ----------
    VerifyLuneWildfire(daDest, bpDest, bDest, gDest, iDest, Open);

    // ---------- 7. pack (single seed-suffixed ExpeditionInfinity keeps the balancing table consistent) ----------
    var wildName = E33ModEditor.Core.Packer.FullPackKit.ModNameFor(CurrentSeed());
    var packed = new Retoc().PackCombined(modDir, wildName);
    Console.WriteLine($"[pack] {wildName} installed:");
    foreach (var f in packed) Console.WriteLine("       " + f);
    return 0;
}

/// <summary>
/// Re-opens the five staged assets and asserts the TestMoveNewFire contract:
/// DA name/NameID/SkillScript, BP has no leftover Wildfire_* NameConsts and
/// still imports the vanilla sequence, balancing row values, appended graph
/// node with prerequisite edge, and the DT_SkillIcons row.
/// </summary>
static void VerifyLuneWildfire(string daDest, string bpDest, string bDest, string gDest, string iDest,
    Func<string, UAssetAPI.UAsset> open)
{
    var da = open(daDest);
    int daOk = 0;
    foreach (var ex in da.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>())
        foreach (var p in ex.Data)
        {
            if (p.Name.ToString() == "Name" && p is UAssetAPI.PropertyTypes.Objects.TextPropertyData tname)
            {
                if (tname.CultureInvariantString?.Value != "TestMoveNewFire")
                    throw new InvalidDataException("DA Name != TestMoveNewFire: " + tname.CultureInvariantString?.Value);
                daOk++;
            }
            else if (p.Name.ToString() == "NameID" && p is UAssetAPI.PropertyTypes.Objects.NamePropertyData nid)
            {
                if (nid.Value.ToString() != "TestMoveNewFire")
                    throw new InvalidDataException("DA NameID != TestMoveNewFire: " + nid.Value);
                daOk++;
            }
            else if (p.Name.ToString() == "SkillScript" && p is UAssetAPI.PropertyTypes.Objects.SoftObjectPropertyData soft)
            {
                if (!soft.Value.AssetPath.PackageName.ToString().EndsWith("BP_Battle_SkillScript_TestMoveNewFire", StringComparison.Ordinal))
                    throw new InvalidDataException("DA SkillScript not retargeted: " + soft.Value.AssetPath.PackageName);
                daOk++;
            }
        }
    if (daOk < 3) throw new InvalidDataException($"DA checks passed={daOk}, expected 3");

    var bp = open(bpDest);
    int leftover = 0, seqRefs = 0;
    foreach (var fn in bp.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>())
    {
        if (fn.ScriptBytecode is null) continue;
        WalkExprs(fn.ScriptBytecode, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_NameConst nc &&
                nc.Value.ToString().StartsWith("Wildfire_", StringComparison.Ordinal)) leftover++;
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_ObjectConst oc &&
                oc.Value is { } v && !v.IsNull() && v.IsImport() &&
                bp.Imports[Math.Abs(v.Index) - 1].ObjectName.ToString().Contains("SEQ_LuneSkill_WildFire")) seqRefs++;
        });
    }
    if (leftover > 0) throw new InvalidDataException($"{leftover} Wildfire_* NameConst left unrenamed");
    if (seqRefs < 1) throw new InvalidDataException("BP lost its sequence import (SEQ_LuneSkill_WildFire)");

    var bal = new DataTableEditor(bDest);
    foreach (var (want, expect) in new[] { ("TestMoveNewFire_BaseDamage", 62.5f), ("TestMoveNewFire_BreakBarMultiplier", 1.0f), ("TestMoveNewFire_BurnOnHit", 3.0f), ("TestMoveNewFire_OverchargeDamageMultiplier", 2.0f), ("TestMoveNewFire_SelfDamageMultiplier", 1.0f) })
    {
        if (!bal.TryGetRow(want, out var row))
            throw new KeyNotFoundException($"row missing after round-trip: {want}");
        float got = row.Value.Select(c => c switch
        {
            UAssetAPI.PropertyTypes.Objects.FloatPropertyData f => f.Value,
            UAssetAPI.PropertyTypes.Objects.DoublePropertyData d => (float)d.Value,
            _ => float.NaN,
        }).FirstOrDefault(v => !float.IsNaN(v));
        if (Math.Abs(got - expect) > 0.001f)
            throw new InvalidDataException($"row '{want}' = {got}, expected {expect}");
    }

    var icon = new DataTableEditor(iDest);
    if (!icon.TryGetRow("TestMoveNewFire", out var iconRow))
        throw new KeyNotFoundException("DT_SkillIcons row TestMoveNewFire missing");
    if (iconRow.Value.Count == 0)
        throw new InvalidDataException("icon row has no fields");

    var g = open(gDest);
    var gx = g.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>().First();
    var nodes = (UAssetAPI.PropertyTypes.Objects.ArrayPropertyData)gx.Data[0];
    int newNode = -1;
    for (int i = 0; i < nodes.Value.Length; i++)
    {
        var ns = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)nodes.Value[i];
        var su = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)ns.Value[0];
        var ptr = ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)su.Value[0]).Value;
        if (g.Imports[Math.Abs(ptr.Index) - 1].ObjectName.ToString() == "DA_Skill_Lune_TestMoveNewFire")
            newNode = i;
    }
    if (newNode < 0)
        throw new InvalidDataException("no node imports the new skill");
    if (newNode != nodes.Value.Length - 1)
        throw new InvalidDataException("new skill not the appended node");
    var edges = (UAssetAPI.PropertyTypes.Objects.ArrayPropertyData)gx.Data[1];
    var lastEdge = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)edges.Value[^1];
    string NameOf(UAssetAPI.UnrealTypes.FPackageIndex p) =>
        g.Imports[Math.Abs(p.Index) - 1].ObjectName.ToString();
    if (NameOf(((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)lastEdge.Value[0]).Value) != "DA_Skill_Lune_Wildfire" ||
        NameOf(((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)lastEdge.Value[1]).Value) != "DA_Skill_Lune_TestMoveNewFire")
        throw new InvalidDataException("edge direction wrong: FromNode must be Wildfire, ToNode TestMoveNewFire");
    Console.WriteLine("[verify] DA/BP/balancing/graph/icons re-open clean; node linked Wildfire -> TestMoveNewFire");
}
/// <summary>
/// Game's-eye view: mounts Paks recursively (~mods included) via CUE4Parse,
/// loads the Maelle skill graph from the winning container, and reports how
/// every imported package resolved — null means the game cannot find it.
/// </summary>
/// <summary>Verifies every jump/flow target in a BP's functions lands on a statement start.</summary>
static int CmdKismetCheck(string[] args)
{
    var path = Path.GetFullPath(args[1]);
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
        : new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    int badTotal = 0;
    foreach (var fn in u.Exports.OfType<FunctionExport>())
    {
        if (fn.ScriptBytecode is null) continue;
        // statement start offsets
        var starts = new List<uint>();
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode)
        {
            starts.Add(pos);
            pos += (uint)st.GetSize(u);
        }
        var startsSet = new HashSet<uint>(starts);
        var bad = new List<string>();
        void Check(uint target, string kind)
        {
            if (!startsSet.Contains(target))
                bad.Add($"{kind}->{target} (starts: {string.Join(",", starts.Where(s => s <= target).TakeLast(2))})");
        }
        uint p2 = 0;
        foreach (var st in fn.ScriptBytecode)
        {
            st.Visit(u, ref p2, (e, _) =>
            {
                switch (e)
                {
                    case UAssetAPI.Kismet.Bytecode.Expressions.EX_Jump j: Check(j.CodeOffset, "Jump"); break;
                    case UAssetAPI.Kismet.Bytecode.Expressions.EX_JumpIfNot jn: Check(jn.CodeOffset, "JumpIfNot"); break;
                    case UAssetAPI.Kismet.Bytecode.Expressions.EX_PushExecutionFlow pf: Check(pf.PushingAddress, "PushFlow"); break;
                    case UAssetAPI.Kismet.Bytecode.Expressions.EX_SwitchValue sv:
                        Check(sv.EndGotoOffset, "SwitchEnd");
                        foreach (var c in sv.Cases ?? Array.Empty<UAssetAPI.Kismet.Bytecode.Expressions.FKismetSwitchCase>())
                            Check(c.NextOffset, "SwitchCase");
                        break;
                }
            });
        }
        if (bad.Count > 0)
        {
            Console.WriteLine($"{fn.ObjectName}: {bad.Count} BAD targets");
            foreach (var b in bad.Take(8)) Console.WriteLine("    " + b);
            badTotal += bad.Count;
        }
        if (args.Length > 2 && args[2] == "mdb" && fn.ObjectName.ToString() == "OnActionEffect")
        {
            foreach (var st in fn.ScriptBytecode)
            {
                uint pp = 0;
                st.Visit(u, ref pp, (e, _) =>
                {
                    if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "MakeDamageBuilder")
                    {
                        for (int i = 0; i < vf.Parameters.Length; i++)
                        {
                            var p2 = vf.Parameters[i];
                            var detail = p2 switch
                            {
                                UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable lv => "LocalVar",
                                UAssetAPI.Kismet.Bytecode.Expressions.EX_ByteConst bc => "ByteConst=" + bc.Value,
                                UAssetAPI.Kismet.Bytecode.Expressions.EX_FinalFunction ff => "FinalFn(" + (ff.StackNode.IsImport() ? ff.StackNode.ToImport(u).ObjectName.ToString() : "?") + ")",
                                _ => p2.GetType().Name,
                            };
                            Console.WriteLine($"  mdb[{i}] = {detail}");
                        }
                    }
                });
            }
        }
        if (args.Length > 2 && args[2] == "jumps" && fn.ObjectName.ToString() == "OnActionEffect")
        {
            var stStarts = new List<uint>();
            uint pj = 0;
            foreach (var st in fn.ScriptBytecode)
            {
                stStarts.Add(pj);
                pj += (uint)st.GetSize(u);
            }
            int Ord(uint target)
            {
                for (int i = 0; i < stStarts.Count; i++)
                    if (stStarts[i] == target) return i;
                return -1;
            }
            int idx = 0;
            var pushStack = new Stack<int>();
            var pushTargets = new Dictionary<int, int>();
            uint p4 = 0;
            foreach (var st in fn.ScriptBytecode)
            {
                if (st is UAssetAPI.Kismet.Bytecode.Expressions.EX_PushExecutionFlow pf2)
                {
                    pushTargets[idx] = Ord(pf2.PushingAddress);
                    pushStack.Push(idx);
                }
                if (st is UAssetAPI.Kismet.Bytecode.Expressions.EX_PopExecutionFlow || st is UAssetAPI.Kismet.Bytecode.Expressions.EX_PopExecutionFlowIfNot)
                {
                    if (pushStack.Count > 0) pushStack.Pop();
                }
                var desc = st switch
                {
                    UAssetAPI.Kismet.Bytecode.Expressions.EX_Jump j => $"EX_Jump -> stmt {Ord(j.CodeOffset)}",
                    UAssetAPI.Kismet.Bytecode.Expressions.EX_JumpIfNot jn => $"EX_JumpIfNot -> stmt {Ord(jn.CodeOffset)}",
                    UAssetAPI.Kismet.Bytecode.Expressions.EX_PopExecutionFlowIfNot => "EX_PopExecutionFlowIfNot (bail -> push target)",
                    UAssetAPI.Kismet.Bytecode.Expressions.EX_PushExecutionFlow => "EX_PushExecutionFlow",
                    _ => null,
                };
                if (desc != null) Console.WriteLine($"  [{idx}] {desc}");
                else if (args.Length > 3 && args[3] == "full") Console.WriteLine($"  [{idx}] {st.GetType().Name}");
                idx++;
                st.Visit(u, ref p4, (e, _) => { });
            }
            foreach (var kv in pushTargets)
                Console.WriteLine($"  push @stmt {kv.Key} resumes at stmt {kv.Value}");
        }
    }
    Console.WriteLine(badTotal == 0 ? "OK: all jump targets land on statement starts" : $"{badTotal} bad jump targets");
    return badTotal == 0 ? 0 : 1;
}

/// <summary>
/// Mounts an arbitrary IoStore trio directory (e.g. a downloaded Nexus mod)
/// and prints MaterialInstance scalar/vector params for assets matching the
/// filter (default "MI_"). Read-only; used to lift color recipes from
/// reference mods.
/// </summary>
static int CmdPakColors(string[] args)
{
    if (args.Length < 2) { Console.WriteLine("usage: pak-colors <pakDir|trioPath> [filter]"); return 1; }
    var dir = Path.GetFullPath(args[1]);
    if (File.Exists(dir)) dir = Path.GetDirectoryName(dir)!;
    var filter = args.Length > 2 ? args[2] : "MI_";
    var provider = new CUE4Parse.FileProvider.DefaultFileProvider(
        dir, SearchOption.TopDirectoryOnly, true,
        new CUE4Parse.UE4.Versions.VersionContainer(CUE4Parse.UE4.Versions.EGame.GAME_UE5_4));
    provider.Initialize();
    provider.SubmitKey(new CUE4Parse.UE4.Objects.Core.Misc.FGuid(),
        new CUE4Parse.Encryption.Aes.FAesKey("0x0000000000000000000000000000000000000000000000000000000000000000"));
    try { provider.MappingsContainer = new CUE4Parse.MappingsProvider.Usmap.FileUsmapTypeMappingsProvider(EditorPaths.Mappings); }
    catch (Exception ex) { Console.WriteLine($"[pak-colors] mappings not loaded: {ex.Message}"); }
    var keys = provider.Files.Keys
        .Where(k => k.Contains(filter, StringComparison.OrdinalIgnoreCase) && k.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
        .OrderBy(k => k).ToList();
    Console.WriteLine($"[pak-colors] {keys.Count} asset(s) matching '{filter}' in {dir}");
    foreach (var key in keys)
    {
        if (!provider.TryLoadPackage(key, out var pkg) || pkg is null)
        {
            Console.WriteLine($"  {key}: LOAD FAILED");
            continue;
        }
        var mi = pkg.GetExports().OfType<CUE4Parse.UE4.Assets.Exports.Material.UMaterialInstanceConstant>().FirstOrDefault();
        if (mi is null)
        {
            var refs = pkg.NameMap
                .Select(n => n.Name ?? "")
                .Where(n => n.Contains("MI_", StringComparison.Ordinal) || (n.Contains("Hair", StringComparison.OrdinalIgnoreCase) && n.Length < 80))
                .Distinct(StringComparer.Ordinal).OrderBy(n => n).ToList();
            Console.WriteLine($"  {key}: (no MaterialInstance export; hair refs: {(refs.Count == 0 ? "none" : string.Join(", ", refs))})");
            continue;
        }
        Console.WriteLine($"  {key}  parent={mi.Parent?.Name ?? "?"}");
        foreach (var s in mi.ScalarParameterValues)
            Console.WriteLine($"    scalar {s.Name} = {s.ParameterValue}");
        foreach (var v in mi.VectorParameterValues)
            Console.WriteLine($"    vector {v.Name} = {v.ParameterValue}");
    }
    return 0;
}

/// <summary>
/// For each asset matching the filter, lists every mounted container that
/// provides it (base chunks + mods). Used to diagnose mod conflicts and
/// load-order issues: the game resolves an asset from the highest-priority
/// container that carries it.
/// </summary>
static int CmdPakOwners(string[] args)
{
    if (args.Length < 3) { Console.WriteLine("usage: pak-owners <pakDir> <filter>"); return 1; }
    var dir = Path.GetFullPath(args[1]);
    if (File.Exists(dir)) dir = Path.GetDirectoryName(dir)!;
    var filter = args[2];
    var provider = new CUE4Parse.FileProvider.DefaultFileProvider(
        dir, SearchOption.AllDirectories, true,
        new CUE4Parse.UE4.Versions.VersionContainer(CUE4Parse.UE4.Versions.EGame.GAME_UE5_4));
    provider.Initialize();
    provider.SubmitKey(new CUE4Parse.UE4.Objects.Core.Misc.FGuid(),
        new CUE4Parse.Encryption.Aes.FAesKey("0x0000000000000000000000000000000000000000000000000000000000000000"));
    try { provider.MappingsContainer = new CUE4Parse.MappingsProvider.Usmap.FileUsmapTypeMappingsProvider(EditorPaths.Mappings); }
    catch (Exception ex) { Console.WriteLine($"[pak-owners] mappings not loaded: {ex.Message}"); }
    var keys = provider.Files.Keys
        .Where(k => k.Contains(filter, StringComparison.OrdinalIgnoreCase) && k.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
        .OrderBy(k => k).ToList();
    Console.WriteLine($"[pak-owners] {keys.Count} asset(s) matching '{filter}'");
    foreach (var key in keys)
    {
        var owners = new List<string>();
        foreach (var reader in provider.MountedVfs)
        {
            if (reader.Files.ContainsKey(key)) owners.Add(reader.Name);
        }
        Console.WriteLine($"  {key}\n    <= {string.Join(", ", owners)}");
    }
    return 0;
}

static int CmdGraphDiag(string[] args)
{
    var provider = new CUE4Parse.FileProvider.DefaultFileProvider(
        EditorPaths.PaksDir, SearchOption.AllDirectories, true,
        new CUE4Parse.UE4.Versions.VersionContainer(CUE4Parse.UE4.Versions.EGame.GAME_UE5_4));
    provider.MappingsContainer = new CUE4Parse.MappingsProvider.Usmap.FileUsmapTypeMappingsProvider(EditorPaths.Mappings);
    provider.Initialize();
    provider.SubmitKey(new CUE4Parse.UE4.Objects.Core.Misc.FGuid(),
        new CUE4Parse.Encryption.Aes.FAesKey("0x0000000000000000000000000000000000000000000000000000000000000000"));

    foreach (var path in new[]
    {
        "/Game/Gameplay/SkillTree/Content/Maelle/DA_SkillGraph_Maelle.uasset",
    })
    {
        var pkg = provider.LoadPackage(path);
        if (pkg is not CUE4Parse.UE4.Assets.IoPackage io)
        {
            Console.WriteLine($"[graph-diag] {path}: NOT LOADED ({pkg?.GetType().Name ?? "null"})");
            continue;
    }
        Console.WriteLine($"[graph-diag] {path}");
        Console.WriteLine($"             imports={io.ImportedPackages.Value.Length} importMap={io.ImportMap.Length}");
        var bad = 0;
        for (int i = 0; i < io.ImportedPackages.Value.Length; i++)
        {
            var imp = io.ImportedPackages.Value[i];
            var name = imp?.Name ?? "?";
            if (imp == null || name.Contains("Charming", StringComparison.OrdinalIgnoreCase))
                Console.WriteLine($"             import[{i}] = {(imp == null ? "<UNRESOLVED>" : name)}");
            if (imp == null) bad++;
        }
        if (bad > 0) Console.WriteLine($"             {bad} UNRESOLVED imported packages");
    }
    return 0;
}

static void WalkExprs(IEnumerable<UAssetAPI.Kismet.Bytecode.KismetExpression> exprs,
    Action<UAssetAPI.Kismet.Bytecode.KismetExpression> visit)
{
    if (exprs is null) return;
    foreach (var e in exprs)
    {
        if (e is null) continue;
        visit(e);
        foreach (var f in e.GetType().GetFields())
        {
            var v = f.GetValue(e);
            if (v is UAssetAPI.Kismet.Bytecode.KismetExpression ke) WalkExprs(new[] { ke }, visit);
            else if (v is UAssetAPI.Kismet.Bytecode.KismetExpression[] arr) WalkExprs(arr, visit);
        }
        foreach (var p in e.GetType().GetProperties())
        {
            if (p.GetIndexParameters().Length > 0) continue;
            var v = p.GetValue(e);
            if (v is UAssetAPI.Kismet.Bytecode.KismetExpression ke) WalkExprs(new[] { ke }, visit);
            else if (v is UAssetAPI.Kismet.Bytecode.KismetExpression[] arr) WalkExprs(arr, visit);
        }
    }
}

static void PatchObjectRefs(IEnumerable<UAssetAPI.PropertyTypes.Objects.PropertyData> props, int oldRaw, int newRaw)
{
    foreach (var p in props)
    {
        if (p is UAssetAPI.PropertyTypes.Objects.ObjectPropertyData o &&
            o.Value is { } idx && idx.Index == oldRaw)
            o.Value = UAssetAPI.UnrealTypes.FPackageIndex.FromRawIndex(newRaw);
        switch (p)
        {
            case UAssetAPI.PropertyTypes.Structs.StructPropertyData st: PatchObjectRefs(st.Value, oldRaw, newRaw); break;
            case UAssetAPI.PropertyTypes.Objects.ArrayPropertyData arr: PatchObjectRefs(arr.Value, oldRaw, newRaw); break;
        }
    }
}

static void VerifyProbe(string daDest, string bpDest, string bDest, string gDest,
    Func<string, UAssetAPI.UAsset> open, string buffClass)
{
    var da = open(daDest);
    if (!da.FolderName.Value.EndsWith(Path.GetFileNameWithoutExtension(daDest), StringComparison.Ordinal))
        throw new InvalidDataException("DA FolderName not retargeted: " + da.FolderName.Value);

    var bp = open(bpDest);
    bool hasBuff = bp.Imports.Any(i => i.ObjectName.ToString() == buffClass);
    if (!hasBuff) throw new InvalidDataException($"BP missing {buffClass} import after patch");
    int leftover = 0;
    foreach (var fn in bp.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>())
    {
        if (fn.ScriptBytecode is null) continue;
        WalkExprs(fn.ScriptBytecode, e =>
        {
            if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_NameConst nc &&
                nc.Value.ToString().StartsWith("FencersFlurry_", StringComparison.Ordinal)) leftover++;
        });
    }
    if (leftover > 0) throw new InvalidDataException($"{leftover} FencersFlurry_* NameConst left unrenamed");

    var bal = new DataTableEditor(bDest);
    foreach (var (want, expect) in new[] { ("CharmingShot_BaseDamage", 2.0f), ("CharmingShot_BreakBarMultiplier", 1.0f), ("CharmingShot_CharmDuration", 2.0f) })
    {
        if (!bal.TryGetRow(want, out var row))
            throw new KeyNotFoundException($"row missing after round-trip: {want}");
        float got = row.Value.Select(c => c switch
        {
            UAssetAPI.PropertyTypes.Objects.FloatPropertyData f => f.Value,
            UAssetAPI.PropertyTypes.Objects.DoublePropertyData d => (float)d.Value,
            _ => float.NaN,
        }).FirstOrDefault(v => !float.IsNaN(v));
        if (Math.Abs(got - expect) > 0.001f)
            throw new InvalidDataException($"row '{want}' = {got}, expected {expect}");
    }

    var g = open(gDest);
    var gxV = g.Exports.OfType<UAssetAPI.ExportTypes.NormalExport>().First();
    var nodes = (UAssetAPI.PropertyTypes.Objects.ArrayPropertyData)gxV.Data[0];
    int charmNode = -1;
    for (int i = 0; i < nodes.Value.Length; i++)
    {
        var ns = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)nodes.Value[i];
        var su = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)ns.Value[0];
        var ptr = ((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)su.Value[0]).Value;
        if (g.Imports[Math.Abs(ptr.Index) - 1].ObjectName.ToString() == "DA_Skill_Maelle_CharmingShot")
            charmNode = i;
    }
    if (charmNode < 0)
        throw new InvalidDataException("no node imports the new skill");
    if (charmNode == nodes.Value.Length - 1)
    {
        // append mode: also check the trailing edge's direction
        var edgesV = (UAssetAPI.PropertyTypes.Objects.ArrayPropertyData)gxV.Data[1];
        var lastEdge = (UAssetAPI.PropertyTypes.Structs.StructPropertyData)((UAssetAPI.PropertyTypes.Structs.StructPropertyData)edgesV.Value[^1]).Clone();
        string NameOf(UAssetAPI.UnrealTypes.FPackageIndex p) =>
            g.Imports[Math.Abs(p.Index) - 1].ObjectName.ToString();
        if (NameOf(((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)lastEdge.Value[0]).Value) != "DA_Skill_Maelle_FleuretFury" ||
            NameOf(((UAssetAPI.PropertyTypes.Objects.ObjectPropertyData)lastEdge.Value[1]).Value) != "DA_Skill_Maelle_CharmingShot")
            throw new InvalidDataException("edge direction wrong: FromNode must be the prerequisite (FleuretFury), ToNode the new skill");
    }
    Console.WriteLine("[verify] staged assets re-open clean: DA/BP/balancing/graph all consistent");
}

static int CmdDumpStains(string[] args)
{
    IEnumerable<string> files;
    if (args.Length > 1)
    {
        var p = Path.GetFullPath(args[1]);
        files = Directory.Exists(p)
            ? Directory.EnumerateFiles(p, "DA_Skill_*.uasset", SearchOption.AllDirectories)
            : new[] { p };
    }
    else
    {
        var root = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "skills");
        files = Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "DA_Skill_Lune_*.uasset", SearchOption.AllDirectories)
            : Array.Empty<string>();
    }

    static string Fmt(IReadOnlyDictionary<string, int> m)
    {
        if (m.Count == 0) return "(none)";
        return string.Join(", ", m.Select(kv =>
        {
            var name = StainsMechanicKit.Display(kv.Key);
            var icons = string.Join("+", Enumerable.Repeat(name, Math.Max(1, kv.Value)));
            return $"{icons} ({kv.Key}×{kv.Value})";
        }));
    }

    int n = 0;
    foreach (var f in files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
    {
        var maps = StainsMechanicKit.ReadFile(f);
        var hasMech = StainsMechanicKit.FindMechanic(StainsMechanicKit.Open(f)) is not null;
        if (!hasMech && maps.OverchargeRequired.Count == 0 && maps.Produced.Count == 0)
            continue;
        Console.WriteLine(Path.GetFileNameWithoutExtension(f));
        Console.WriteLine("  consume  " + Fmt(maps.OverchargeRequired));
        Console.WriteLine("  generate " + Fmt(maps.Produced));
        n++;
    }
    Console.WriteLine($"-- {n} skill(s) with StainsMechanic");
    return 0;
}

static int CmdTextureInfo(string[] args)
{
    var filter = args.Length > 1 ? args[1] : "UI/Resources/Textures/GameMenu/Backgrounds";
    var max = 200;
    for (var i = 2; i < args.Length; i++)
        if (int.TryParse(args[i], out var n)) max = n;
    foreach (var t in TextureExportKit.ListTextures(filter, max))
        Console.WriteLine($"{t.Format,-14} {t.SizeX,5}x{t.SizeY,-5} mips={t.MipCount} bytes={t.FirstMipBytes,10}  {t.Path}");
    return 0;
}

static int CmdTextureExport(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("usage: texture-export <NameOrPath[,Name2...]|@listfile> [--out DIR] [--max N]");
        return 2;
    }
    var names = new List<string>();
    string? outDir = null;
    var max = 2048;
    for (var i = 1; i < args.Length; i++)
    {
        if (args[i] == "--out" && i + 1 < args.Length) { outDir = Path.GetFullPath(args[++i]); }
        else if (args[i] == "--max" && i + 1 < args.Length && int.TryParse(args[++i], out var m)) { max = m; }
        else if (args[i].StartsWith("@"))
        {
            var listFile = Path.GetFullPath(args[i][1..]);
            names.AddRange(File.ReadAllLines(listFile).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")));
        }
        else if (!args[i].StartsWith("--"))
        {
            names.AddRange(args[i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
    }
    outDir ??= Path.Combine(EditorPaths.RepoRoot, "work", "guide_textures");
    var results = TextureExportKit.ExportTextures(names, outDir, max);
    Console.WriteLine($"-- {results.Count}/{names.Count} exported to {outDir}");
    return results.Count == names.Count ? 0 : 1;
}

static int CmdDumpImports(string[] args)
{
    var path = Path.GetFullPath(args[1]);
    var map = DataTableEditor.Mappings;
    var u = map != null
        ? new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
        : new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
    string? filter = args.Length > 2 ? args[2] : null;
    for (int i = 0; i < u.Imports.Count; i++)
    {
        var imp = u.Imports[i];
        var outer = imp.OuterIndex is { } oi && !oi.IsNull() && oi.IsImport()
            ? u.Imports[Math.Abs(oi.Index) - 1].ObjectName.ToString()
            : "null";
        var line = $"[{i}] raw={UAssetAPI.UnrealTypes.FPackageIndex.FromImport(i).Index} pkg={imp.ClassPackage} cls={imp.ClassName} obj={imp.ObjectName} outer={imp.OuterIndex?.Index}";
        if (filter == null || line.Contains(filter, StringComparison.OrdinalIgnoreCase))
            Console.WriteLine(line);
    }
    Console.WriteLine($"-- {u.Imports.Count} imports");
    return 0;
}

 static void PrintUsage()
{
    Console.WriteLine("""
        E33 Mod Editor CLI
          find    <assetNameSubstring>               list game containers containing asset
          extract <assetNameSubstring> [--out DIR]   unpack smallest containing container
          extract-suspend <asset> [...]              extract assets while ~mods is moved aside
                                                     (always restored; for assets mods shadow)
          dump    <uasset> [--out FILE]              dump DataTable rows to JSON
          flatten <uasset> <rowName>                 typed scalar leaves of one row
          apply   <uasset> <edits.json>              apply row edits in place
          pack    <dir> --name <ModName_P>           pack legacy dir into ~mods container
          dumpasset <uasset>                         flatten a property-only asset
          texture-info <filter> [max]               list UI textures (format/size) via CUE4Parse
          texture-export <names> [--out DIR] [--max N]  export UI textures to PNG (comma list or @file)
          dump-stains [uasset|dir]                   StainsMechanic maps (consume / generate) per skill DA
          fx-roll [count]                            simulate weapon-passive rolls (default 20)
          rando-smoke                                seed-42 monster/weapon stat + element rolls
          loot-dist [runs]                           chest/drop bucket counts vanilla vs randomizer
          tweaks-smoke                               level-cap + lumina-per-level kismet check
          popup-cap-smoke [N] [--keep]               battle proc-card cap guard (WBP_PassivePopupContainer)
          balance-smoke                              regular-enemy buff + lumina-drop adjustment checks
          bulk-smoke [uasset]                        weapon bulk-upgrade splice + verify (default: vanilla
                                                       tables copy; pass the overleveled work/mod copy
                                                       for pack-order compatibility)
          drop-smoke                                 weapon-only drop-level splice + verify (MaxDropLevel 222)
          pictos-smoke                               painter element + auto-duration clone verification
          picto-tags [modDir]                        repair/apply picto categories and stage the
                                                     extended E_PassiveEffectCategory enum (default work/mod)
          skill-overhaul-smoke                       enable/disable apply + revert round-trip
          mappings [usmap]                           schema summary for the usmap
          enums [filter]                             enum names/values from the usmap
          st <uasset> [filter]                       string table entries
          lune-wildfire                               clone Lune Wildfire -> "TestMoveNewFire" (50x damage, icon row,
                                                      appended linked node on her tree), pack ExpeditionInfinity_<seed>_P
          lune-heal-smoke                             stage Lune kits to scratch dir, verify strict/no-Light and
                                                      wildcard-On holder modes, Genesis/Cataclysm exceptions,
                                                      + Angel's Gift single-pay grant shape
          superspark                                  Spark → Super Spark (Void, Fleuret Fury 3-hit anim, 10 Burn / Offensive 10 Blight)
          save-write      <json> <sav>               JSON back into a .sav
          save-roundtrip  <sav> [--write FILE]       verify byte-identical re-serialization
          save-summary    <sav>                      story point, location, party level, playtime
          save-pictos-add <sav> --id NAME [--level N] [--master] [--no-backup]
          save-pictos-swap <sav> --from A --to B [--no-backup]
          save-story <sav> [out.sav] --point <index|key> [--char-level N]
                     [--weapon-level N] [--pictos-level N] [--no-gear] [--no-master] [--list]
                                 rewind a donor save to a main-story checkpoint and
                                 optionally grant endgame gear for testing
          save-stage-rebuild <sav> [--write] [--list-sources] [--max-tier N] [--no-backup]
                                 reconstruct pictos/weapons from the staged randomized
                                 pack up to Monoco's Station (dry-run unless --write)
          party5 [--out DIR] [--pack NAME_P] [--install]
                                 build the 5-member party mod (two-row
                                 formation: 1/2 front, 3/5/4 back) into
                                 work/mod_party5, verify it, and optionally pack it to
                                 Export/ (--install also copies it into the game's ~mods).
                                 Uses work/party_five.json's saved formation (party size fixed at 5).
                                 --no-formation omits the offset math (keeps the slot-3/4
                                 aliases); --no-spawn omits the BattleManager/Character_Battle_Base
                                 patch entirely — both are crash-bisect variants.
          party5-smoke           patch verification for the party-of-five kit (no game files)
          battle-groups [--mod-dir DIR] [--max N] [--chance PCT] [--area PREFIX] [--seed N]
                                 patch battle assets for 4–8 enemy slots and rewrite
                                 encounter rosters (experimental; default work/mod)
          battle-groups-smoke    field-capacity + roster + Party-of-Five composition check
          random-battles [--mod-dir DIR] [--max N] [--variants N] [--chance PCT] [--area PREFIX] [--seed N]
                                 pre-generate variant rosters and patch world enemies to
                                 re-roll the encounter on every battle start (experimental)
          random-battles-smoke   runtime variants + PossibleEncounters + StartBattle check
          row-damage-smoke       stage + verify the back-row damage patch (no game files)
          firstperson [flags]    camera preset tuning (--preset overshoulder|firstperson,
                                 --arm/--forward/--side.../--monoco-arm/--monoco-side/
                                 --climb-arm/--climb-scale/--worldmap-aperture,
                                 see firstperson.json)
          firstperson-smoke      staged camera verification (DAs, pawns, climb boom, table)
          secondperson-pack [--out DIR] [--name NAME_P] [--no-install]
                                 build the standalone second-person camera mod from scratch:
                                 wipes DIR (default work/mod_fp), applies + verifies all four
                                 restore sources, packs Export/NAME_P.{pak,utoc,ucas} and
                                 installs to ~mods (--no-install skips the copy).
          rebuild-mod [--language en|fr|pl|ru]
                                 full pack; the language bakes a translated
                                 Game.locres (en = no override, the default)
          loc-languages                list pack languages and cultures
          loc-catalog [--out DIR]      compare work/mod with vanilla and write
                                       en/fr/pl/ru catalogs + review report
          loc-apply --language fr      merge the selected catalog into the
                                       staged work/mod Game.locres
          loc-validate --language fr   coverage + markup/number/newline checks
          loc-smoke                    verify all shipped catalogs + repak presence
        """);
}

sealed record EchoMapState(int entries, string key, string value, string listener, int bareNames);

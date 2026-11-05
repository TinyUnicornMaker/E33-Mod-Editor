using E33ModEditor.Core.Localization;
using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Generates extra pictos into a packed mod tree. Does not touch shops or loot.
/// </summary>
public static class PictosCreateKit
{
    public const string TogglePainter = "PainterConversion";
    public const string ToggleStatusAffinity = "StatusAffinity";

    public sealed record ConversionSpec(
        string Id,
        string DisplayName,
        string Description,
        string ElementEnum,
        string SourceBp);

    /// <summary>
    /// Damage bonus while the WEARER has the status. Cloned from vanilla
    /// Cursed Power, whose graph gates on the source character's own buff
    /// component (ProcessDealtRawDamages → HasBuffOfClass → AddDamageModifier),
    /// so separate tiers each add their own modifier and compound
    /// multiplicatively when worn together.
    /// </summary>
    public sealed record PowerSpec(
        string Id,
        string DisplayName,
        string Description,
        string BuffClass,
        string BuffPath,
        float Value)
    {
        public string BalancingKey => Id + "_DamageIncrease";
    }

    /// <summary>One Painter-style conversion pictos per damage element.</summary>
    public static readonly ConversionSpec[] PainterConversions =
    {
        new("PainterConv_Fire", "Painter's Fire",
            "Convert all Skill damage to Fire damage.", "NewEnumerator0", "clone"),
        new("PainterConv_Ice", "Painter's Ice",
            "Convert all Skill damage to Ice damage.", "NewEnumerator1", "clone"),
        new("PainterConv_Lightning", "Painter's Lightning",
            "Convert all Skill damage to Lightning damage.", "NewEnumerator2", "clone"),
        new("PainterConv_Earth", "Painter's Earth",
            "Convert all Skill damage to Earth damage.", "NewEnumerator3", "clone"),
        new("PainterConv_Dark", "Painter's Dark",
            "Convert all Skill damage to Dark damage.", "NewEnumerator4",
            "BP_LU_SkillsConversionToDark"),
        new("PainterConv_Light", "Painter's Light",
            "Convert all Skill damage to Light damage.", "NewEnumerator5", "clone"),
        new("PainterConv_Physical", "Painter's Physical",
            "Convert all Skill damage to Physical damage.", "NewEnumerator6",
            "BP_LU_SkillsConversionToPhysical"),
        new("PainterConv_Void", "Painter's Void",
            "Convert all Skill damage to Void damage.", "NewEnumerator7",
            "BP_LU_SkillsConversionToVoid"),
    };
    /// <summary>
    /// The three power archetypes, three tiers each. Vanilla Cursed Power is
    /// reworked into tier I (value lowered, renamed) — no vanilla Berserk or
    /// Blight power exists, so those get full ladders.
    /// </summary>
    public static readonly PowerSpec[] Powers =
    {
        Pow("CursedPower2", "Cursed Power II", "Cursed", "Cursed", 0.10f),
        Pow("CursedPower3", "Cursed Power III", "Cursed", "Cursed", 0.20f),
        Pow("BerserkPower1", "Berserk Power I", "Berserked", "Berserk", 0.05f),
        Pow("BerserkPower2", "Berserk Power II", "Berserked", "Berserk", 0.10f),
        Pow("BerserkPower3", "Berserk Power III", "Berserked", "Berserk", 0.20f),
        Pow("BlightedPower1", "Blighted Power I", "Blighted", "Blight", 0.05f),
        Pow("BlightedPower2", "Blighted Power II", "Blighted", "Blight", 0.10f),
        Pow("BlightedPower3", "Blighted Power III", "Blighted", "Blight", 0.20f),
    };

    public sealed record HitBurnSpec(string Id, string DisplayName, string Description, float Chance);
    private static readonly Dictionary<string, int> HitBurnLuminaCosts = new(StringComparer.Ordinal)
    {
        ["BurnOnHit1"] = 1, ["BurnOnHit2"] = 3, ["BurnOnHit3"] = 5,
        ["BurnOnHit4"] = 10, ["BurnOnHit5"] = 15,
    };
    public static readonly HitBurnSpec[] HitBurn = {
        new("BurnOnHit1", "Burning Skin I", "Gain 1 Burn on dodge (20%).", 0.20f),
        new("BurnOnHit2", "Burning Skin II", "Gain 1 Burn on dodge (40%).", 0.40f),
        new("BurnOnHit3", "Burning Skin III", "Gain 1 Burn on dodge (60%).", 0.60f),
        new("BurnOnHit4", "Burning Skin IV", "Gain 1 Burn on dodge (80%).", 0.80f),
        new("BurnOnHit5", "Burning Skin V", "Gain 1 Burn on dodge (100%).", 1.00f),
    };

    public sealed record StainPictosSpec(string Id, string DisplayName, string Description, string ElementEnum, int Amount, string SourceBp);
    public static readonly StainPictosSpec[] StainPictos = {
        new("StainPictos_Fire2", "Fire Stains II", "Start battle with 2 Fire Stains.", "NewEnumerator0", 2, "BP_LU_Stains_FireStart"),
        new("StainPictos_Fire4", "Fire Stains IV", "Start battle with 4 Fire Stains.", "NewEnumerator0", 4, "BP_LU_Stains_FireStart"),
        new("StainPictos_Ice2", "Ice Stains II", "Start battle with 2 Ice Stains.", "NewEnumerator1", 2, "BP_LU_Stains_IceStart"),
        new("StainPictos_Ice4", "Ice Stains IV", "Start battle with 4 Ice Stains.", "NewEnumerator1", 4, "BP_LU_Stains_IceStart"),
        new("StainPictos_Lightning2", "Lightning Stains II", "Start battle with 2 Lightning Stains.", "NewEnumerator2", 2, "BP_LU_Stains_LightningStart"),
        new("StainPictos_Lightning4", "Lightning Stains IV", "Start battle with 4 Lightning Stains.", "NewEnumerator2", 4, "BP_LU_Stains_LightningStart"),
        new("StainPictos_Earth2", "Earth Stains II", "Start battle with 2 Earth Stains.", "NewEnumerator3", 2, "BP_LU_Stains_EarthStart"),
        new("StainPictos_Earth4", "Earth Stains IV", "Start battle with 4 Earth Stains.", "NewEnumerator3", 4, "BP_LU_Stains_EarthStart"),
    };



    private static readonly Dictionary<string, int> PowerLuminaCosts = new(StringComparer.Ordinal)
    {
        ["CursedPower"] = 5, ["CursedPower2"] = 10, ["CursedPower3"] = 15,
        ["BerserkPower1"] = 5, ["BerserkPower2"] = 10, ["BerserkPower3"] = 15,
        ["BlightedPower1"] = 5, ["BlightedPower2"] = 10, ["BlightedPower3"] = 15,
    };

    private const string GenericBuff = "/Game/Gameplay/Buffs/GenericBuff";

    private static PowerSpec Pow(string id, string display, string statusWord, string buff, float value) =>
        new(id, display, $"Deal {value * 100:0}% more damage while {statusWord}.",
            "BP_BattleBuff_" + buff, GenericBuff + "/BP_BattleBuff_" + buff, value);

    public static bool HasGeneratedAssets(string modDir) =>
        FindAsset(modDir, "BP_LU_PainterConv_Fire.uasset") != null
        || FindAsset(modDir, "BP_LU_BerserkPower1.uasset") != null;
    public static void CopyCreateTables(string modDir)
    {
        foreach (var table in new[]
                 {
                     // Runtime item lookup uses CompositeDataTable ParentTables.
                     // Extra rows on the composite itself are wiped on load — DT_jRPG_Items
                     // is the parent that actually carries pictos/gear.
                     "DT_jRPG_Items", "DT_jRPG_Items_Composite", "DT_PassiveEffects",
                     "DT_PictosDefinitions", "DT_Balancing_Luminas",
                 })
        {
            try { CopyTableIntoMod(table, modDir); }
            catch { /* optional depending on preload */ }
        }
        StagePictoIcons(modDir);
        EnsureExtracted("ST_PassiveEffects");
        var st = FileCopy.FindTablesAsset("ST_PassiveEffects.uasset");
        if (st != null)
        {
            var idx = st.IndexOf("Sandfall", StringComparison.Ordinal);
            if (idx >= 0)
                FileCopy.AssetPair(st, Path.Combine(modDir, st[idx..]));
        }
    }

    /// <summary>
    /// <c>WBP_PictoIcon.LoadPictoIcon</c> looks up <c>DT_PictoIcons</c> by pictos
    /// id. A miss skips <c>LoadPassiveEffectLearnState</c>, so the widget keeps
    /// its CDO — gold frame, all four learn pips filled — even at 0/4 battles.
    /// The extract writes <c>Datatables</c>; the game mounts <c>DataTables</c>.
    /// </summary>
    internal static string StagePictoIcons(string modDir)
    {
        EnsureExtracted("DT_PictoIcons");
        var src = FileCopy.FindTablesAsset("DT_PictoIcons.uasset")
            ?? throw new FileNotFoundException("DT_PictoIcons.uasset");
        var dest = Path.Combine(modDir,
            "Sandfall/Content/UI/Resources/DataTables/PictoIcons/DT_PictoIcons.uasset");
        if (!File.Exists(dest))
            FileCopy.AssetPair(src, dest);
        return dest;
    }

    /// <summary>Clone a vanilla <c>DT_PictoIcons</c> row under each new pictos id.</summary>
    internal static void ClonePictoIconRows(string modDir, IEnumerable<(string Donor, string Id)> rows)
    {
        var path = StagePictoIcons(modDir);
        var ed = new DataTableEditor(path);
        foreach (var (donor, id) in rows)
        {
            if (string.IsNullOrEmpty(id) || ed.HasRow(id)) continue;
            if (string.IsNullOrEmpty(donor) || !ed.HasRow(donor)) continue;
            ed.CloneRow(donor, id);
        }
        ed.Save(path);
    }

    public static int InjectPainterConversions(string modDir)
    {
        var repo = EditorPaths.RepoRoot;
        // Conversion Blueprints are unversioned: their CDO only parses when the
        // sibling packages (the conversion family shares a schema chain) sit on
        // disk under one Content root, so UAssetAPI can pull their schemas.
        // Opened from an isolated copy, the CDO falls back to raw bytes and the
        // NewElement patch below would be silently skipped (every clone then
        // keeps the void payload it was copied from).
        var bpRoot = EnsureExtractedShared("LuminaPassives",
            "BP_LU_SkillsConversionToVoid", "BP_LU_SkillsConversionToDark",
            "BP_LU_SkillsConversionToPhysical");
        EnsureExtracted("DA_GearPictos_Painter");
        EnsureExtracted("ST_PassiveEffects");

        var painterDa = FindAsset(Path.Combine(repo, "work", "tables"), "DA_GearPictos_Painter.uasset")
            ?? throw new FileNotFoundException("DA_GearPictos_Painter.uasset");

        int added = 0;
        var strings = new Dictionary<string, string>();

        foreach (var spec in PainterConversions)
        {
            var bpStem = "BP_LU_" + spec.Id;
            var daStem = "DA_GearPictos_" + spec.Id;
            var bpDest = Path.Combine(modDir, "Sandfall/Content/Gameplay/Lumina/Passives", bpStem + ".uasset");
            var daDest = Path.Combine(modDir, "Sandfall/Content/Gameplay/Pictos/Gear/NewLumina", daStem + ".uasset");

            var srcName = spec.SourceBp == "clone" ? "BP_LU_SkillsConversionToVoid" : spec.SourceBp;
            var srcBp = FindAsset(bpRoot, srcName + ".uasset")
                ?? throw new FileNotFoundException(srcName + ".uasset");
            CloneConversionBlueprint(srcBp, bpDest, bpStem, spec.ElementEnum, spec.Id);

            CloneGearData(painterDa, daDest, daStem, spec.Id);
            strings[$"PASSIVE_{spec.Id}_Name"] = spec.DisplayName;
            strings[$"PASSIVE_{spec.Id}_Description"] = spec.Description;
            added++;
        }

        InjectItemRows(modDir);
        InjectPassiveRows(modDir);
        InjectDefinitionRows(modDir);
        InjectStrings(modDir, strings);
        ClonePictoIconRows(modDir, PainterConversions.Select(s => ("Painter", s.Id)));
        return added;
    }

    public static int InjectStatusAffinities(string modDir)
    {
        var repo = EditorPaths.RepoRoot;
        EnsureExtracted("BP_LU_CursedPower");
        EnsureExtracted("DA_GearPictos_InvertedAffinity");
        EnsureExtracted("ST_PassiveEffects");

        var cursedBp = FindAsset(Path.Combine(repo, "work", "tables"), "BP_LU_CursedPower.uasset")
            ?? throw new FileNotFoundException("BP_LU_CursedPower.uasset");
        var gearDa = FindAsset(Path.Combine(repo, "work", "tables"), "DA_GearPictos_InvertedAffinity.uasset")
            ?? throw new FileNotFoundException("DA_GearPictos_InvertedAffinity.uasset");

        int added = 0;
        var strings = new Dictionary<string, string>();

        foreach (var spec in Powers)
        {
            var bpStem = "BP_LU_" + spec.Id;
            var daStem = "DA_GearPictos_" + spec.Id;
            var bpDest = Path.Combine(modDir,
                "Sandfall/Content/Gameplay/Lumina/Passives/OnDamageDealt", bpStem + ".uasset");
            var daDest = Path.Combine(modDir,
                "Sandfall/Content/Gameplay/Pictos/Gear/NewLumina", daStem + ".uasset");
            ClonePowerBlueprint(cursedBp, bpDest, bpStem,
                new[] { "BP_BattleBuff_Cursed" },
                new[] { "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Cursed" },
                spec.BuffClass, spec.BuffPath);
            CloneGearData(gearDa, daDest, daStem, spec.Id);
            strings[$"PASSIVE_{spec.Id}_Name"] = spec.DisplayName;
            strings[$"PASSIVE_{spec.Id}_Description"] = spec.Description;
            added++;
        }

        ReworkVanillaCursedPower(modDir);
        InjectStatusItemRows(modDir);
        InjectStatusPassiveRows(modDir);
        InjectStatusDefinitionRows(modDir);
        InjectStatusBalancing(modDir);
        InjectStrings(modDir, strings);
        ClonePictoIconRows(modDir, Powers.Select(s => ("CursedPower", s.Id)));
        return added;
    }
    public static int InjectHitBurn(string modDir)
    {
        var repo = EditorPaths.RepoRoot;
        var bpRoot = EnsureExtractedShared("StatusOnDodge", "BP_LU_DodgeBurn", "BP_LU_DodgeCanInflictStatus", "Enum_StatusToApplyOnDodge", "BP_BattleBuff_Burn", "AC_jRPG_BattleManager");
        EnsureExtracted("DA_GearPictos_Painter");
        var srcBurnBp = FindAsset(bpRoot, "BP_LU_DodgeBurn.uasset")
            ?? FindAsset(Path.Combine(repo, "work", "tables"), "BP_LU_DodgeBurn.uasset")
            ?? throw new FileNotFoundException("BP_LU_DodgeBurn.uasset");
        var srcParentBp = FindAsset(bpRoot, "BP_LU_DodgeCanInflictStatus.uasset")
            ?? FindAsset(Path.Combine(repo, "work", "tables", "StatusOnDodge"), "BP_LU_DodgeCanInflictStatus.uasset")
            ?? throw new FileNotFoundException("BP_LU_DodgeCanInflictStatus.uasset");
        var gearDa = FindAsset(Path.Combine(repo, "work", "tables"), "DA_GearPictos_Painter.uasset")
            ?? FileCopy.FindTablesAsset("DA_GearPictos_Painter.uasset")
            ?? throw new FileNotFoundException("DA_GearPictos_Painter.uasset");
        int added = 0;
        var strings = new Dictionary<string, string>();
        foreach (var spec in HitBurn)
        {
            var parentStem = "BP_LU_DodgeCanInflictStatus_" + spec.Id;
            var parentDest = Path.Combine(modDir, "Sandfall/Content/Gameplay/Lumina/Passives/OnDodge/StatusOnDodge", parentStem + ".uasset");
            CloneBurnParent(srcParentBp, parentDest, parentStem, spec);
            var bpStem = "BP_LU_" + spec.Id;
            var daStem = "DA_GearPictos_" + spec.Id;
            var bpDest = Path.Combine(modDir, "Sandfall/Content/Gameplay/Lumina/Passives/OnDodge/StatusOnDodge", bpStem + ".uasset");
            var daDest = Path.Combine(modDir, "Sandfall/Content/Gameplay/Pictos/Gear/NewLumina", daStem + ".uasset");
            Directory.CreateDirectory(Path.GetDirectoryName(bpDest)!);
            Directory.CreateDirectory(Path.GetDirectoryName(daDest)!);
            CloneHitBurnBlueprint(srcBurnBp, bpDest, bpStem, spec, parentStem);
            CloneGearData(gearDa, daDest, daStem, spec.Id);
            strings[$"PASSIVE_{spec.Id}_Name"] = spec.DisplayName;
            strings[$"PASSIVE_{spec.Id}_Description"] = spec.Description;
            added++;
        }
        InjectHitBurnItemRows(modDir);
        InjectHitBurnPassiveRows(modDir);
        InjectHitBurnDefinitionRows(modDir);
        InjectStrings(modDir, strings);
        ClonePictoIconRows(modDir, HitBurn.Select(s => ("BurningDodge", s.Id)));
        return added;
    }

    public static int InjectStainPictos(string modDir)
    {
        var repo = EditorPaths.RepoRoot;
        var shared = EnsureExtractedShared("StainPictos", "BP_LU_Stains_FireStart", "BP_LU_Stains_IceStart", "BP_LU_Stains_LightningStart", "BP_LU_Stains_EarthStart", "BP_LU_Stains_OnBattleStart1");
        EnsureExtracted("DA_GearPictos_Painter");
        var gearDa = FindAsset(Path.Combine(repo, "work", "tables"), "DA_GearPictos_Painter.uasset")
            ?? FileCopy.FindTablesAsset("DA_GearPictos_Painter.uasset")
            ?? throw new FileNotFoundException("DA_GearPictos_Painter.uasset");
        // Prime parent so child CDOs parse.
        _ = OpenAsset(FindAsset(shared, "BP_LU_Stains_OnBattleStart1.uasset") ?? throw new FileNotFoundException("OnBattleStart1"));
        int added = 0;
        var strings = new Dictionary<string, string>();
        foreach (var spec in StainPictos)
        {
            var src = FindAsset(shared, spec.SourceBp + ".uasset") ?? FindAsset(Path.Combine(repo, "work", "tables"), spec.SourceBp + ".uasset") ?? throw new FileNotFoundException(spec.SourceBp);
            var bpStem = "BP_LU_" + spec.Id;
            var daStem = "DA_GearPictos_" + spec.Id;
            var bpDest = Path.Combine(modDir, "Sandfall/Content/Gameplay/Lumina/Passives", bpStem + ".uasset");
            var daDest = Path.Combine(modDir, "Sandfall/Content/Gameplay/Pictos/Gear/NewLumina", daStem + ".uasset");
            Directory.CreateDirectory(Path.GetDirectoryName(bpDest)!);
            Directory.CreateDirectory(Path.GetDirectoryName(daDest)!);
            CloneStainPictosBlueprint(src, bpDest, bpStem, spec);
            CloneGearData(gearDa, daDest, daStem, spec.Id);
            strings[$"PASSIVE_{spec.Id}_Name"] = spec.DisplayName;
            strings[$"PASSIVE_{spec.Id}_Description"] = spec.Description;
            added++;
        }
        InjectStainPictosItemRows(modDir);
        InjectStainPictosPassiveRows(modDir);
        InjectStainPictosDefinitionRows(modDir);
        InjectStrings(modDir, strings);
        ClonePictoIconRows(modDir, StainPictos.Select(s => ("Painter", s.Id)));
        return added;
    }

    private static void InjectStainPictosItemRows(string modDir)
    {
        foreach (var table in ItemTables)
        {
            var path = FindAsset(modDir, table + ".uasset") ?? CopyTableIntoMod(table, modDir);
            var ed = new DataTableEditor(path);
            var stats = new JObject();
            foreach (var spec in StainPictos)
            {
                ed.CloneRow("Painter", spec.Id);
                RewriteStainPictosItem(ed, spec);
                stats[spec.Id] = ItemStatEdit(new StatSpread(0.90f, 0.90f, 0, 0, 1.20f, 110, 35, 0, 0));
            }
            ed.Apply(stats.ToString());
            ed.Save(path);
        }
    }

    private static void RewriteStainPictosItem(DataTableEditor ed, StainPictosSpec spec)
    {
        if (!ed.TryGetRow(spec.Id, out var row)) return;
        foreach (var f in row.Value)
        {
            var n = DataTableEditor.CleanName(f.Name);
            if (n == "Item_HardcodedName" && f is NamePropertyData nm)
                nm.Value = new FName(ed.Asset, spec.Id);
            else if (n == "Item_DisplayName" && f is TextPropertyData t)
                SetLocText(t, $"PASSIVE_{spec.Id}_Name", spec.DisplayName);
            else if (n == "ItemDescription" && f is TextPropertyData d)
                SetLocText(d, $"PASSIVE_{spec.Id}_Description", spec.Description);
            else if (n == "Pictos_Data" && f is ObjectPropertyData o)
                o.Value = EnsureObjectImport(ed.Asset,
                    "/Game/Gameplay/Pictos/Gear/NewLumina/DA_GearPictos_" + spec.Id,
                    "DA_GearPictos_" + spec.Id,
                    "BP_DataAsset_Item_Pictos_C",
                    // ClassPackage must be the package that DEFINES the class
                    // (vanilla rows use it). "/Script/Engine" here is a fake
                    // Engine class: the Data Asset hard ref fails to link, so
                    // the pictos tooltip (which binds the DA) goes stale while
                    // the passive effect (soft BattleBuffs ref) still works.
                    "/Game/Gameplay/Inventory/BP_DataAsset_Item_Pictos");
        }
    }

    private static void InjectStainPictosPassiveRows(string modDir)
    {
        var path = FindAsset(modDir, "DT_PassiveEffects.uasset") ?? CopyTableIntoMod("DT_PassiveEffects", modDir);
        var ed = new DataTableEditor(path);
        foreach (var spec in StainPictos)
        {
            ed.CloneRow("Painter", spec.Id);
            if (!ed.TryGetRow(spec.Id, out var row)) continue;
            foreach (var f in row.Value.ToList())
            {
                var n = DataTableEditor.CleanName(f.Name);
                if (n == "Name" && f is TextPropertyData t)
                    SetLocText(t, $"PASSIVE_{spec.Id}_Name", spec.DisplayName);
                else if (n == "Description" && f is TextPropertyData d)
                    SetLocText(d, $"PASSIVE_{spec.Id}_Description", spec.Description);
                else if (n == "LuminaCost" && f is IntPropertyData c)
                    c.Value = spec.Amount == 2 ? 5 : 10;
                else if (n == "BattleBuffs" && f is ArrayPropertyData arr && arr.Value.Length > 0 && arr.Value[0] is SoftObjectPropertyData)
                {
                    var pkg = "/Game/Gameplay/Lumina/Passives/BP_LU_" + spec.Id;
                    var cls = "BP_LU_" + spec.Id + "_C";
                    var pathRef = new FSoftObjectPath(new FName(ed.Asset, pkg), new FName(ed.Asset, cls), null);
                    var elem = (SoftObjectPropertyData)arr.Value[0].Clone();
                    elem.Value = pathRef;
                    var neu = (ArrayPropertyData)arr.Clone();
                    neu.Value = new PropertyData[] { elem };
                    var idx = row.Value.IndexOf(f);
                    if (idx >= 0) row.Value[idx] = neu;
                    if (ed.Asset.SoftObjectPathList is { Count: > 0 } list)
                        list.Add(pathRef);
                }
            }
        }
        ed.Save(path);
    }

    private static void InjectStainPictosDefinitionRows(string modDir)
    {
        var path = FindAsset(modDir, "DT_PictosDefinitions.uasset") ?? CopyTableIntoMod("DT_PictosDefinitions", modDir);
        var ed = new DataTableEditor(path);
        var stats = new JObject();
        foreach (var spec in StainPictos)
        {
            ed.CloneRow("Painter", spec.Id);
            if (!ed.TryGetRow(spec.Id, out var row)) continue;
            foreach (var f in row.Value)
                if (DataTableEditor.CleanName(f.Name) == "ItemID" && f is NamePropertyData nm)
                    nm.Value = new FName(ed.Asset, spec.Id);
            stats[spec.Id] = DefStatEdit(new StatSpread(0.90f, 0.90f, 0, 0, 1.20f, 110, 35, 0, 0));
        }
        ed.Apply(stats.ToString());
        ed.Save(path);
    }

    private static void CloneStainPictosBlueprint(string srcUasset, string destUasset, string newStem, StainPictosSpec spec)
    {
        var asset = OpenAsset(srcUasset);
        var srcStem = Path.GetFileNameWithoutExtension(srcUasset);
        RetargetPackage(asset, destUasset, srcStem, newStem);
        // Patch StainsAmount on the new pictos CDO (child of OnBattleStart1).
        foreach (var export in asset.Exports.OfType<NormalExport>())
        {
            if (!export.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal)) continue;
            foreach (var p in export.Data)
            {
                if (DataTableEditor.CleanName(p.Name) == "StainsAmount" && p is IntPropertyData ip)
                    ip.Value = spec.Amount;
                if (DataTableEditor.CleanName(p.Name) == "StainsType" && p is EnumPropertyData ep)
                    ep.Value = new FName(asset, spec.ElementEnum);
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(destUasset)!);
        asset.Write(destUasset);
    }

    private static void InjectHitBurnItemRows(string modDir)
    {
        foreach (var table in ItemTables)
        {
            var path = FindAsset(modDir, table + ".uasset") ?? CopyTableIntoMod(table, modDir);
            var ed = new DataTableEditor(path);
            var stats = new JObject();
            foreach (var spec in HitBurn)
            {
                ed.CloneRow("BurningDodge", spec.Id);
                RewriteClonedItem(ed, spec.Id);
                stats[spec.Id] = ItemStatEdit(new StatSpread(0.90f, 0.90f, 0, 0, 1.20f, 110, 35, 0, 0));
            }
            ed.Apply(stats.ToString());
            ed.Save(path);
        }
    }
    private static void InjectHitBurnPassiveRows(string modDir)
    {
        var path = FindAsset(modDir, "DT_PassiveEffects.uasset") ?? CopyTableIntoMod("DT_PassiveEffects", modDir);
        var ed = new DataTableEditor(path);
        foreach (var spec in HitBurn)
        {
            ed.CloneRow("BurningDodge", spec.Id);
            if (!ed.TryGetRow(spec.Id, out var row)) continue;
            foreach (var f in row.Value.ToList())
            {
                var n = DataTableEditor.CleanName(f.Name);
                if (n == "Name" && f is TextPropertyData t)
                    SetLocText(t, $"PASSIVE_{spec.Id}_Name", spec.DisplayName);
                else if (n == "Description" && f is TextPropertyData d)
                    SetLocText(d, $"PASSIVE_{spec.Id}_Description", spec.Description);
                else if (n == "LuminaCost" && f is IntPropertyData c)
                {
                    if (HitBurnLuminaCosts.TryGetValue(spec.Id, out var cost)) c.Value = cost;
                }
                else if (n == "BattleBuffs" && f is ArrayPropertyData arr && arr.Value.Length > 0 && arr.Value[0] is SoftObjectPropertyData)
                {
                    var pkg = "/Game/Gameplay/Lumina/Passives/OnDodge/StatusOnDodge/BP_LU_" + spec.Id;
                    var cls = "BP_LU_" + spec.Id + "_C";
                    var pathRef = new FSoftObjectPath(new FName(ed.Asset, pkg), new FName(ed.Asset, cls), null);
                    var elem = (SoftObjectPropertyData)arr.Value[0].Clone();
                    elem.Value = pathRef;
                    var neu = (ArrayPropertyData)arr.Clone();
                    neu.Value = new PropertyData[] { elem };
                    var idx = row.Value.IndexOf(f);
                    if (idx >= 0) row.Value[idx] = neu;
                    if (ed.Asset.SoftObjectPathList is { Count: > 0 } list)
                        list.Add(pathRef);
                }
            }

        }
        ed.Save(path);
    }

    private static void InjectHitBurnDefinitionRows(string modDir)
    {
        var path = FindAsset(modDir, "DT_PictosDefinitions.uasset") ?? CopyTableIntoMod("DT_PictosDefinitions", modDir);
        var ed = new DataTableEditor(path);
        var stats = new JObject();
        foreach (var spec in HitBurn)
        {
            ed.CloneRow("BurningDodge", spec.Id);
            if (!ed.TryGetRow(spec.Id, out var row)) continue;
            foreach (var f in row.Value)
                if (DataTableEditor.CleanName(f.Name) == "ItemID" && f is NamePropertyData nm)
                    nm.Value = new FName(ed.Asset, spec.Id);
            stats[spec.Id] = DefStatEdit(new StatSpread(0.90f, 0.90f, 0, 0, 1.20f, 110, 35, 0, 0));
        }
        ed.Apply(stats.ToString());
        ed.Save(path);
    }



    private static void InjectItemRows(string modDir)
    {
        foreach (var table in ItemTables)
        {
            var path = FindAsset(modDir, table + ".uasset")
                ?? CopyTableIntoMod(table, modDir);
            var ed = new DataTableEditor(path);
            var stats = new JObject();
            for (int i = 0; i < PainterConversions.Length; i++)
            {
                var spec = PainterConversions[i];
                Console.Error.WriteLine("[pictos-create] item " + spec.Id);
                ed.CloneRow("Painter", spec.Id);
                RewriteClonedItem(ed, spec);
                stats[spec.Id] = ItemStatEdit(PainterSpread(i));
            }
            ed.Apply(stats.ToString());
            Console.Error.WriteLine("[pictos-create] saving " + table + "…");
            ed.Save(path);
        }
    }

    /// <summary>
    /// DT_jRPG_Items is a real DataTable (parent of the composite). The composite
    /// is still cloned so editor dumps stay consistent, but the game rebuilds it
    /// from ParentTables and discards extra composite rows.
    /// </summary>
    private static readonly string[] ItemTables = { "DT_jRPG_Items", "DT_jRPG_Items_Composite" };

    private const string StatHp = "E_jRPG_StatType::NewEnumerator1";
    private const string StatDef = "E_jRPG_StatType::NewEnumerator4";
    private const string StatCrit = "E_jRPG_StatType::NewEnumerator6";
    private const string StatSpd = "E_jRPG_StatType::NewEnumerator7";

    /// <summary>Vanilla-style pictos stats: two of HP/Def/Speed/Crit plus hover numbers.</summary>
    private readonly record struct StatSpread(
        float Hp, float Def, float Spd, float Crit, float Chroma,
        int ItemHp, int ItemDef, int ItemSpd, int ItemCrit);

    private static StatSpread PainterSpread(int i) => i switch
    {
        0 => new(0, 0, 0.95f, 0.95f, 1.50f, 0, 0, 48, 16),   // Fire
        1 => new(1.00f, 1.00f, 0, 0, 1.20f, 120, 40, 0, 0),  // Ice
        2 => new(0, 0, 1.43f, 0.48f, 1.50f, 0, 0, 72, 8),    // Lightning
        3 => new(0.95f, 0.95f, 0, 0, 1.75f, 110, 48, 0, 0),  // Earth
        4 => new(1.00f, 0, 0, 1.00f, 1.20f, 124, 0, 0, 16),  // Dark
        5 => new(1.43f, 0, 0.48f, 0, 1.50f, 130, 0, 24, 0),  // Light
        6 => new(0, 0.95f, 0.95f, 0, 1.25f, 0, 36, 48, 0),   // Physical
        _ => new(0, 0, 1.00f, 1.00f, 1.00f, 0, 0, 51, 16),   // Void
    };

    private static readonly StatSpread[] AffinitySpreads =
    {
        new(1.00f, 0, 0, 1.00f, 1.20f, 124, 0, 0, 16),
        new(0, 0, 1.00f, 1.00f, 1.00f, 0, 0, 51, 16),
        new(1.00f, 1.00f, 0, 0, 1.20f, 120, 40, 0, 0),
        new(0, 1.00f, 1.00f, 0, 1.20f, 0, 40, 48, 0),
        new(0.95f, 0, 0, 0.95f, 1.50f, 100, 0, 0, 16),
        new(0, 0, 1.43f, 0.48f, 1.50f, 0, 0, 72, 8),
        new(1.43f, 0.48f, 0, 0, 1.50f, 130, 20, 0, 0),
        new(0.95f, 0.95f, 0, 0, 1.75f, 110, 48, 0, 0),
        new(0, 0.95f, 0.95f, 0, 1.25f, 0, 36, 48, 0),
        new(1.50f, 0, 0.50f, 0, 1.10f, 140, 0, 24, 0),
    };

    private static StatSpread AffinitySpread(int i) => AffinitySpreads[i % AffinitySpreads.Length];

    /// <summary>
    /// Hover text must not be a StringTableEntry/Base loc key. New keys are
    /// missing from the cooked locres, so lookup falls through to the pictos
    /// widget CDO (Accelerating Heal). Culture-invariant FText is what the
    /// working E33 randomizer uses for custom item names.
    /// </summary>
    private static void SetLocText(TextPropertyData t, string key, string display)
    {
        display = ModLocalization.Key(key, display);
        t.HistoryType = TextHistoryType.None;
        t.Flags = ETextFlag.CultureInvariant;
        t.Namespace = null;
        t.Value = new FString(display);
        t.CultureInvariantString = new FString(display);
        t.TableId = null;
    }

    /// <summary>
    /// Sets a row's culture-invariant display text, adding the field when the
    /// vanilla row never serialized one (several vanilla rows carry no
    /// Item_DisplayName / ItemDescription and rely on cooked locres).
    /// </summary>
    internal static void SetRowText(DataTableEditor ed, StructPropertyData row,
        string fieldName, string key, string value)
    {
        if (row.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == fieldName)
            is TextPropertyData existing)
        {
            SetLocText(existing, key, value);
            return;
        }
        value = ModLocalization.Key(key, value);
        var text = new TextPropertyData(new FName(ed.Asset, fieldName))
        {
            Flags = ETextFlag.CultureInvariant,
            HistoryType = TextHistoryType.None,
            Namespace = null,
            Value = new FString(value),
            CultureInvariantString = new FString(value),
            TableId = null,
        };
        row.Value.Add(text);
    }

    private static JObject ItemStatEdit(StatSpread s)
    {
        var o = new JObject();
        if (s.ItemHp > 0) o[StatHp] = s.ItemHp;
        if (s.ItemDef > 0) o[StatDef] = s.ItemDef;
        if (s.ItemSpd > 0) o[StatSpd] = s.ItemSpd;
        if (s.ItemCrit > 0) o[StatCrit] = s.ItemCrit;
        return new JObject { ["Pictos_ItemStats"] = o };
    }

    private static JObject DefStatEdit(StatSpread s)
    {
        var o = new JObject();
        if (s.Hp > 0) o[StatHp] = s.Hp;
        if (s.Def > 0) o[StatDef] = s.Def;
        if (s.Spd > 0) o[StatSpd] = s.Spd;
        if (s.Crit > 0) o[StatCrit] = s.Crit;
        return new JObject
        {
            ["BaseStatMultipliers"] = o,
            ["ChromaValueMultiplier"] = s.Chroma,
        };
    }

    private static void RewriteClonedItem(DataTableEditor ed, ConversionSpec spec)
    {
        if (!ed.TryGetRow(spec.Id, out var row)) return;
        foreach (var f in row.Value)
        {
            var n = DataTableEditor.CleanName(f.Name);
            if (n == "Item_HardcodedName" && f is NamePropertyData nm)
                nm.Value = new FName(ed.Asset, spec.Id);
            else if (n == "Item_DisplayName" && f is TextPropertyData t)
                SetLocText(t, $"PASSIVE_{spec.Id}_Name", spec.DisplayName);
            else if (n == "ItemDescription" && f is TextPropertyData d)
                SetLocText(d, $"PASSIVE_{spec.Id}_Description", spec.Description);
            else if (n == "Pictos_Data" && f is ObjectPropertyData o)
                o.Value = EnsureObjectImport(ed.Asset,
                    "/Game/Gameplay/Pictos/Gear/NewLumina/DA_GearPictos_" + spec.Id,
                    "DA_GearPictos_" + spec.Id,
                    "BP_DataAsset_Item_Pictos_C",
                    // ClassPackage must be the package that DEFINES the class
                    // (vanilla rows use it). "/Script/Engine" here is a fake
                    // Engine class: the Data Asset hard ref fails to link, so
                    // the pictos tooltip (which binds the DA) goes stale while
                    // the passive effect (soft BattleBuffs ref) still works.
                    "/Game/Gameplay/Inventory/BP_DataAsset_Item_Pictos");
        }
    }
    private static void RewriteClonedItem(DataTableEditor ed, HitBurnSpec spec)
    {
        if (!ed.TryGetRow(spec.Id, out var row)) return;
        foreach (var f in row.Value)
        {
            var n = DataTableEditor.CleanName(f.Name);
            if (n == "Item_HardcodedName" && f is NamePropertyData nm)
                nm.Value = new FName(ed.Asset, spec.Id);
            else if (n == "Item_DisplayName" && f is TextPropertyData t)
                SetLocText(t, $"PASSIVE_{spec.Id}_Name", spec.DisplayName);
            else if (n == "ItemDescription" && f is TextPropertyData d)
                SetLocText(d, $"PASSIVE_{spec.Id}_Description", spec.Description);
            else if (n == "Pictos_Data" && f is ObjectPropertyData o)
                o.Value = EnsureObjectImport(ed.Asset,
                    "/Game/Gameplay/Pictos/Gear/NewLumina/DA_GearPictos_" + spec.Id,
                    "DA_GearPictos_" + spec.Id,
                    "BP_DataAsset_Item_Pictos_C",
                    // ClassPackage must be the package that DEFINES the class
                    // (vanilla rows use it). "/Script/Engine" here is a fake
                    // Engine class: the Data Asset hard ref fails to link, so
                    // the pictos tooltip (which binds the DA) goes stale while
                    // the passive effect (soft BattleBuffs ref) still works.
                    "/Game/Gameplay/Inventory/BP_DataAsset_Item_Pictos");
        }
    }
    internal static void RewriteClonedItem(DataTableEditor ed, string id)
    {
        var spec = HitBurn.FirstOrDefault(s => s.Id == id);
        if (spec != null) { RewriteClonedItem(ed, spec); return; }
        // Generic clone: the row name is the id and the gear asset is
        // DA_GearPictos_<id>. Without this the clone keeps the donor's
        // Item_HardcodedName + Pictos_Data and resolves as the donor in-game
        // (this is why the revive-bomb clones dropped as Burning Death I).
        if (!ed.TryGetRow(id, out var row)) return;
        foreach (var f in row.Value)
        {
            var n = DataTableEditor.CleanName(f.Name);
            if (n == "Item_HardcodedName" && f is NamePropertyData nm)
                nm.Value = new FName(ed.Asset, id);
            else if (n == "Pictos_Data" && f is ObjectPropertyData o)
                o.Value = EnsureObjectImport(ed.Asset,
                    "/Game/Gameplay/Pictos/Gear/NewLumina/DA_GearPictos_" + id,
                    "DA_GearPictos_" + id,
                    "BP_DataAsset_Item_Pictos_C",
                    // ClassPackage must be the package that DEFINES the class
                    // (vanilla rows use it). "/Script/Engine" here is a fake
                    // Engine class: the Data Asset hard ref fails to link, so
                    // the pictos tooltip (which binds the DA) goes stale while
                    // the passive effect (soft BattleBuffs ref) still works.
                    "/Game/Gameplay/Inventory/BP_DataAsset_Item_Pictos");
        }
    }

    private static void InjectPassiveRows(string modDir)
    {
        var path = FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? CopyTableIntoMod("DT_PassiveEffects", modDir);
        var ed = new DataTableEditor(path);
        foreach (var spec in PainterConversions)
        {
            ed.CloneRow("Painter", spec.Id);
            if (!ed.TryGetRow(spec.Id, out var row)) continue;
            foreach (var f in row.Value.ToList())
            {
                var n = DataTableEditor.CleanName(f.Name);
                if (n == "Name" && f is TextPropertyData t)
                    SetLocText(t, $"PASSIVE_{spec.Id}_Name", spec.DisplayName);
                else if (n == "Description" && f is TextPropertyData d)
                    SetLocText(d, $"PASSIVE_{spec.Id}_Description", spec.Description);
                else if (n == "BattleBuffs" && f is ArrayPropertyData arr && arr.Value.Length > 0
                         && arr.Value[0] is SoftObjectPropertyData)
                {
                    var pkg = "/Game/Gameplay/Lumina/Passives/BP_LU_" + spec.Id;
                    var cls = "BP_LU_" + spec.Id + "_C";
                    var pathRef = new FSoftObjectPath(new FName(ed.Asset, pkg), new FName(ed.Asset, cls), null);
                    var elem = (SoftObjectPropertyData)arr.Value[0].Clone();
                    elem.Value = pathRef;
                    var neu = (ArrayPropertyData)arr.Clone();
                    neu.Value = new PropertyData[] { elem };
                    var idx = row.Value.IndexOf(f);
                    if (idx >= 0) row.Value[idx] = neu;
                    if (ed.Asset.SoftObjectPathList is { Count: > 0 } list)
                        list.Add(pathRef);
                }
            }
        }
        ed.Save(path);
    }

    private static void InjectDefinitionRows(string modDir)
    {
        var path = FindAsset(modDir, "DT_PictosDefinitions.uasset")
            ?? CopyTableIntoMod("DT_PictosDefinitions", modDir);
        var ed = new DataTableEditor(path);
        var stats = new JObject();
        for (int i = 0; i < PainterConversions.Length; i++)
        {
            var spec = PainterConversions[i];
            ed.CloneRow("Painter", spec.Id);
            if (!ed.TryGetRow(spec.Id, out var row)) continue;
            foreach (var f in row.Value)
                if (DataTableEditor.CleanName(f.Name) == "ItemID" && f is NamePropertyData nm)
                    nm.Value = new FName(ed.Asset, spec.Id);
            stats[spec.Id] = DefStatEdit(PainterSpread(i));
        }
        ed.Apply(stats.ToString());
        ed.Save(path);
    }

    private static void InjectStatusItemRows(string modDir)
    {
        foreach (var table in ItemTables)
        {
            var path = FindAsset(modDir, table + ".uasset")
                ?? CopyTableIntoMod(table, modDir);
            var ed = new DataTableEditor(path);
            var stats = new JObject();
            for (int i = 0; i < Powers.Length; i++)
            {
                var spec = Powers[i];
                ed.CloneRow("CursedPower", spec.Id);
                if (!ed.TryGetRow(spec.Id, out var row)) continue;
                foreach (var f in row.Value)
                {
                    var n = DataTableEditor.CleanName(f.Name);
                    if (n == "Item_HardcodedName" && f is NamePropertyData nm)
                        nm.Value = new FName(ed.Asset, spec.Id);
                    else if (n == "Item_DisplayName" && f is TextPropertyData t)
                        SetLocText(t, $"PASSIVE_{spec.Id}_Name", spec.DisplayName);
                    else if (n == "ItemDescription" && f is TextPropertyData d)
                        SetLocText(d, $"PASSIVE_{spec.Id}_Description", spec.Description);
                    else if (n == "Pictos_Data" && f is ObjectPropertyData o)
                        o.Value = EnsureObjectImport(ed.Asset,
                            "/Game/Gameplay/Pictos/Gear/NewLumina/DA_GearPictos_" + spec.Id,
                            "DA_GearPictos_" + spec.Id,
                            "BP_DataAsset_Item_Pictos_C",
                            "/Game/Gameplay/Inventory/BP_DataAsset_Item_Pictos");
                }
                stats[spec.Id] = ItemStatEdit(AffinitySpread(i));
            }
            ed.Apply(stats.ToString());
            ed.Save(path);
        }
    }

    private static void InjectStatusPassiveRows(string modDir)
    {
        var path = FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? CopyTableIntoMod("DT_PassiveEffects", modDir);
        var ed = new DataTableEditor(path);
        foreach (var spec in Powers)
        {
            ed.CloneRow("CursedPower", spec.Id);
            if (!ed.TryGetRow(spec.Id, out var row)) continue;
            foreach (var f in row.Value.ToList())
            {
                var n = DataTableEditor.CleanName(f.Name);
                if (n == "Name" && f is TextPropertyData t)
                    SetLocText(t, $"PASSIVE_{spec.Id}_Name", spec.DisplayName);
                else if (n == "Description" && f is TextPropertyData d)
                    SetLocText(d, $"PASSIVE_{spec.Id}_Description", spec.Description);
                else if (n == "LuminaCost" && f is IntPropertyData c)
                {
                    if (PowerLuminaCosts.TryGetValue(spec.Id, out var cost)) c.Value = cost;
                }
                else if (n == "BattleBuffs" && f is ArrayPropertyData arr && arr.Value.Length > 0
                         && arr.Value[0] is SoftObjectPropertyData)
                {
                    var pkg = "/Game/Gameplay/Lumina/Passives/OnDamageDealt/BP_LU_" + spec.Id;
                    var cls = "BP_LU_" + spec.Id + "_C";
                    var pathRef = new FSoftObjectPath(new FName(ed.Asset, pkg), new FName(ed.Asset, cls), null);
                    var elem = (SoftObjectPropertyData)arr.Value[0].Clone();
                    elem.Value = pathRef;
                    var neu = (ArrayPropertyData)arr.Clone();
                    neu.Value = new PropertyData[] { elem };
                    var idx = row.Value.IndexOf(f);
                    if (idx >= 0) row.Value[idx] = neu;
                    if (ed.Asset.SoftObjectPathList is { Count: > 0 } list)
                        list.Add(pathRef);
                }
            }
        }
        ed.Save(path);
    }

    private static void InjectStatusDefinitionRows(string modDir)
    {
        var path = FindAsset(modDir, "DT_PictosDefinitions.uasset")
            ?? CopyTableIntoMod("DT_PictosDefinitions", modDir);
        var ed = new DataTableEditor(path);
        var stats = new JObject();
        for (int i = 0; i < Powers.Length; i++)
        {
            var spec = Powers[i];
            ed.CloneRow("CursedPower", spec.Id);
            if (!ed.TryGetRow(spec.Id, out var row)) continue;
            foreach (var f in row.Value)
                if (DataTableEditor.CleanName(f.Name) == "ItemID" && f is NamePropertyData nm)
                    nm.Value = new FName(ed.Asset, spec.Id);
            stats[spec.Id] = DefStatEdit(AffinitySpread(i));
        }
        ed.Apply(stats.ToString());
        ed.Save(path);
    }

    private static void InjectStatusBalancing(string modDir)
    {
        var path = FindAsset(modDir, "DT_Balancing_Luminas.uasset")
            ?? CopyTableIntoMod("DT_Balancing_Luminas", modDir);
        var ed = new DataTableEditor(path);
        var edits = new Newtonsoft.Json.Linq.JObject();
        foreach (var spec in Powers)
        {
            ed.CloneRow("CursedPower_DamageIncrease", spec.BalancingKey);
            edits[spec.BalancingKey] = new Newtonsoft.Json.Linq.JObject
            {
                ["FloatValue"] = spec.Value,
            };
        }
        // Vanilla Cursed Power becomes tier I of the ladder.
        edits["CursedPower_DamageIncrease"] = new Newtonsoft.Json.Linq.JObject
        {
            ["FloatValue"] = 0.05f,
        };
        ed.Apply(edits.ToString());
        ed.Save(path);
    }

    private static void InjectStrings(string modDir, Dictionary<string, string> entries)
    {
        var path = FindAsset(modDir, "ST_PassiveEffects.uasset");
        if (path is null)
        {
            var src = FileCopy.FindTablesAsset("ST_PassiveEffects.uasset")
                ?? throw new FileNotFoundException("ST_PassiveEffects.uasset");
            var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
            var dest = idx >= 0 ? Path.Combine(modDir, src[idx..]) : Path.Combine(modDir, Path.GetFileName(src));
            FileCopy.AssetPair(src, dest);
            path = dest;
        }
        DataTableEditor.AddStringTableEntries(path, entries);
    }

    /// <summary>
    /// Opens the SOURCE Blueprint from the shared extraction root (where its
    /// dependency schemas resolve), retargets it to the destination package in
    /// memory, patches the CDO's NewElement enum, and writes the pair out.
    /// Copy-then-edit cannot work here: an isolated copy fails unversioned CDO
    /// parsing, and the element value would never be patched in.
    /// </summary>
    private static void CloneConversionBlueprint(string srcUasset, string destUasset, string newStem,
        string elementEnum, string displayId)
    {
        var asset = OpenAsset(srcUasset);
        var srcStem = Path.GetFileNameWithoutExtension(srcUasset);
        var srcShort = srcStem.StartsWith("BP_LU_", StringComparison.Ordinal)
            ? srcStem["BP_LU_".Length..] : srcStem;
        RetargetPackage(asset, destUasset, srcStem, newStem);
        var names = asset.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s)) continue;
            if (s.Contains(srcShort, StringComparison.Ordinal))
                asset.SetNameReference(i, FString.FromString(s.Replace(srcShort, displayId, StringComparison.Ordinal)));
        }
        var patched = false;
        foreach (var export in asset.Exports.OfType<NormalExport>())
        {
            foreach (var p in export.Data)
            {
                if (p is EnumPropertyData e && e.Name.ToString() == "NewElement")
                {
                    e.Value = new FName(asset, elementEnum);
                    patched = true;
                }
            }
        }
        if (!patched)
            throw new InvalidDataException(
                "NewElement not found on " + Path.GetFileName(srcUasset) +
                " CDO — dependency schemas were not resolvable; extraction root is incomplete.");
        Directory.CreateDirectory(Path.GetDirectoryName(destUasset)!);
        asset.Write(destUasset);
    }

    /// <summary>
    /// Clones vanilla Cursed Power (self-status damage gate) retargeting its
    /// buff import to the target status. Name-map-only edits: the payload is
    /// untouched, so an unversioned parse failure on the copied pair is
    /// harmless — raw bytes are preserved verbatim.
    /// </summary>
    private static void ClonePowerBlueprint(string srcUasset, string destUasset, string newStem,
        string[] fromClasses, string[] fromPaths, string toClass, string toPath)
    {
        CopyPair(srcUasset, destUasset);
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(destUasset, EngineVersion.VER_UE5_4, map)
            : new UAsset(destUasset, EngineVersion.VER_UE5_4);
        const string srcStem = "BP_LU_CursedPower";
        const string srcShort = "CursedPower";
        RetargetPackage(asset, destUasset, srcStem, newStem);
        var names = asset.GetNameMapIndexList();
        // RetargetPackage has already swapped the full stem, so a name that now
        // carries the new short name must not be swapped again: an id containing
        // the donor's short name (CursedPower2/3) doubled it into
        // BP_LU_CursedPower33_C — a class the passive row never resolves, so the
        // game crashed at battle load when the picto was equipped.
        var newShort = newStem["BP_LU_".Length..];
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s)) continue;
            var next = s.Contains(newShort, StringComparison.Ordinal)
                ? s
                : s.Replace(srcShort, newShort, StringComparison.Ordinal);
            foreach (var fc in fromClasses)
                if (next.Contains(fc, StringComparison.Ordinal))
                    next = next.Replace(fc, toClass, StringComparison.Ordinal);
            foreach (var fp in fromPaths)
                if (next.Contains(fp, StringComparison.Ordinal))
                    next = next.Replace(fp, toPath, StringComparison.Ordinal);
            if (next != s) asset.SetNameReference(i, FString.FromString(next));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(destUasset)!);
        asset.Write(destUasset);
    }

    internal static void CloneGearData(string srcUasset, string destUasset, string newStem, string rowName)
    {
        CopyPair(srcUasset, destUasset);
        var ed = DataTableEditor.OpenPropertyAsset(destUasset);
        var srcStem = Path.GetFileNameWithoutExtension(srcUasset);
        RetargetPackage(ed.Asset, destUasset, srcStem, newStem);
        foreach (var export in ed.Asset.Exports.OfType<NormalExport>())
        {
            foreach (var p in export.Data)
                SetPassiveRowName(p, rowName, ed.Asset);
        }
        ed.Asset.Write(destUasset);
    }
    private static void CloneBurnParent(string srcUasset, string destUasset, string newStem, HitBurnSpec spec)
    {
        CopyPair(srcUasset, destUasset);
        var map = DataTableEditor.Mappings;
        var asset = map != null ? new UAsset(destUasset, EngineVersion.VER_UE5_4, map) : new UAsset(destUasset, EngineVersion.VER_UE5_4);
        const string srcStem = "BP_LU_DodgeCanInflictStatus";
        RetargetPackage(asset, destUasset, srcStem, newStem);
        PatchBurnParentKismet(asset, spec.Chance);
        Directory.CreateDirectory(Path.GetDirectoryName(destUasset)!);
        asset.Write(destUasset);
    }
    private static void PatchBurnParentKismet(UAsset asset, float chance)
    {
        foreach (var exp in asset.Exports.OfType<FunctionExport>())
        {
            if (!exp.ObjectName.Value.Value.Contains("ExecuteUbergraph")) continue;
            uint off = 0;
            foreach (var bc in exp.ScriptBytecode)
                PatchBurnKismetRec(asset, bc, ref off, chance);
        }
    }
    private static void PatchBurnKismetRec(UAsset asset, KismetExpression expr, ref uint offset, float chance)
    {
        uint cur = offset;
        if (expr is EX_DoubleConst dc && cur == 0x016E)
            dc.Value = chance;
        if (expr is EX_LocalVariable lv && cur >= 0x02F0 && cur <= 0x0300)
        {
            var pn = lv.Variable?.New?.Path;
            if (pn != null && pn.Length > 0 && pn[0].ToString() == "K2Node_CustomEvent_Enemy")
            {
                pn[0] = new FName(asset, "K2Node_CustomEvent_Character");
            }
        }
        var t = expr.GetType();
        foreach (var pr in t.GetProperties())
        {
            if (pr.PropertyType == typeof(KismetExpression))
            {
                var val = pr.GetValue(expr) as KismetExpression;
                if (val != null && val != expr) PatchBurnKismetRec(asset, val, ref offset, chance);
            }
            else if (pr.PropertyType == typeof(KismetExpression[]))
            {
                var arr = pr.GetValue(expr) as KismetExpression[];
                if (arr != null) foreach (var child in arr) if (child != null) PatchBurnKismetRec(asset, child, ref offset, chance);
            }
        }
        foreach (var f in t.GetFields())
        {
            if (f.FieldType == typeof(KismetExpression))
            {
                var val = f.GetValue(expr) as KismetExpression;
                if (val != null) PatchBurnKismetRec(asset, val, ref offset, chance);
            }
            else if (f.FieldType == typeof(KismetExpression[]))
            {
                var arr = f.GetValue(expr) as KismetExpression[];
                if (arr != null) foreach (var child in arr) if (child != null) PatchBurnKismetRec(asset, child, ref offset, chance);
            }
        }
        uint dummy = cur;
        try { expr.Visit(asset, ref dummy, (c, o) => { }); } catch { }
        offset = dummy;
    }
    private static void CloneHitBurnBlueprint(string srcUasset, string destUasset, string newStem, HitBurnSpec spec, string parentStem)
    {
        CopyPair(srcUasset, destUasset);
        var map = DataTableEditor.Mappings;
        var asset = map != null ? new UAsset(destUasset, EngineVersion.VER_UE5_4, map) : new UAsset(destUasset, EngineVersion.VER_UE5_4);
        const string srcStem = "BP_LU_DodgeBurn";
        RetargetPackage(asset, destUasset, srcStem, newStem);
        var names = asset.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s)) continue;
            if (s.Contains("DodgeBurn", StringComparison.Ordinal))
            {
                var next = s.Replace("DodgeBurn", newStem["BP_LU_".Length..], StringComparison.Ordinal);
                if (next != s) asset.SetNameReference(i, FString.FromString(next));
            }
            if (s.Contains("BP_LU_DodgeCanInflictStatus", StringComparison.Ordinal))
            {
                var next = s.Replace("BP_LU_DodgeCanInflictStatus", parentStem, StringComparison.Ordinal);
                if (next != s) asset.SetNameReference(i, FString.FromString(next));
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(destUasset)!);
        asset.Write(destUasset);
    }



    /// <summary>
    /// IoStore package ID is hashed from UAsset.FolderName (UE5 package name),
    /// not the filename. Clones that keep the source FolderName collide and
    /// retoc keeps a single chunk — the game then fails to load the DA and the
    /// hover widget shows its CDO default (Accelerating Heal, no stats).
    /// </summary>
    internal static void RetargetPackage(UAsset asset, string destUasset, string srcStem, string newStem)
    {
        var newPackage = GamePackagePath(destUasset);
        var oldPackage = asset.FolderName?.Value;
        asset.FolderName = FString.FromString(newPackage);
        var names = asset.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s)) continue;
            var next = s;
            if (!string.IsNullOrEmpty(oldPackage))
                next = next.Replace(oldPackage, newPackage, StringComparison.Ordinal);
            // srcStem is a prefix of newStem (DA_GearPictos_Painter vs
            // DA_GearPictos_PainterConv_Fire). Skip if the package path
            // replace already inserted newStem.
            if (!next.Contains(newStem, StringComparison.Ordinal))
            {
                var replaced = next.Replace(srcStem, newStem, StringComparison.Ordinal);
                if (replaced == next && next.Contains(srcStem, StringComparison.OrdinalIgnoreCase))
                {
                    // Vanilla casing can differ from the filename
                    // (DA_GearPictos_firstOffensive vs DA_GearPictos_FirstOffensive).
                    replaced = System.Text.RegularExpressions.Regex.Replace(
                        next,
                        System.Text.RegularExpressions.Regex.Escape(srcStem),
                        m => newStem,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
                next = replaced;
            }
            if (next != s) asset.SetNameReference(i, FString.FromString(next));
        }
    }

    private static string GamePackagePath(string destUasset)
    {
        var full = destUasset.Replace('\\', '/');
        const string marker = "/Content/";
        var idx = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            throw new InvalidDataException("cloned asset is not under Content/: " + destUasset);
        var rel = full[(idx + marker.Length)..];
        if (rel.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            rel = rel[..^".uasset".Length];
        return "/Game/" + rel;
    }

    private static void SetPassiveRowName(PropertyData p, string rowName, UAsset asset)
    {
        if (p is NamePropertyData n && n.Name.ToString() is "RowName" or "Name")
            n.Value = new FName(asset, rowName);
        if (p is StructPropertyData st)
            foreach (var c in st.Value) SetPassiveRowName(c, rowName, asset);
        if (p is ArrayPropertyData arr)
            foreach (var c in arr.Value) SetPassiveRowName(c, rowName, asset);
    }

    private static FPackageIndex EnsureObjectImport(UAsset asset, string packagePath, string objectName,
        string className, string classPackage)
    {
        FPackageIndex? pkgIndex = null;
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

    private static string CopyTableIntoMod(string table, string modDir)
    {
        var srcDir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", table);
        if (!Directory.Exists(srcDir))
            new Retoc().ToLegacy(srcDir, table);
        CopyTree(srcDir, modDir);
        return FindAsset(modDir, table + ".uasset")
            ?? throw new FileNotFoundException(table + ".uasset missing after copy");
    }

    internal static void EnsureExtracted(string filter)
    {
        if (FileCopy.FindTablesAsset(filter + ".uasset") != null)
            return;
        var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", filter);
        Directory.CreateDirectory(dir);
        new Retoc().ToLegacy(dir, filter);
    }

    /// <summary>
    /// Extracts every filter into ONE shared legacy root (work/tables/&lt;rootName&gt;).
    /// Unversioned Blueprint CDOs only parse when the packages their schema
    /// chain walks sit on disk under the same Content root; UAssetAPI pulls
    /// those schemas from siblings on first open and caches them in the shared
    /// mappings for the rest of the process.
    /// </summary>
    internal static string EnsureExtractedShared(string rootName, params string[] filters)
    {
        var root = Path.Combine(EditorPaths.RepoRoot, "work", "tables", rootName);
        Directory.CreateDirectory(root);
        foreach (var filter in filters)
        {
            if (FindAsset(root, filter + ".uasset") != null) continue;
            // DonorExtract stages each filter under work/tables/<filter>. When that
            // already happened (the packer runs EnsureAll first) copy the staged
            // tree in instead of re-extracting — retoc refuses while ~mods holds a
            // live pack, which would otherwise break a fresh packer run.
            var staged = FileCopy.FindTablesAsset(filter + ".uasset");
            if (staged != null && CopyStagedInto(root, staged)) continue;
            new Retoc().ToLegacy(root, filter);
        }
        return root;
    }

    /// <summary>
    /// Copies the whole <c>work/tables/&lt;filter&gt;</c> tree that produced
    /// <paramref name="stagedAsset"/> into <paramref name="root"/>, preserving the
    /// Sandfall/Content layout. Returns false (so the caller re-extracts) when the
    /// asset is not under work/tables or the copy did not land it in root.
    /// </summary>
    private static bool CopyStagedInto(string root, string stagedAsset)
    {
        var tables = Path.Combine(EditorPaths.RepoRoot, "work", "tables");
        var full = Path.GetFullPath(stagedAsset);
        var rel = Path.GetRelativePath(Path.GetFullPath(tables), full);
        if (rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel)) return false;
        var parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Length < 2) return false;
        CopyTree(Path.Combine(tables, parts[0]), root);
        return FindAsset(root, Path.GetFileName(stagedAsset)) != null;
    }

    internal static UAsset OpenAsset(string path)
    {
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
    }

    internal static void CopyPair(string srcUasset, string destUasset) =>
        FileCopy.AssetPair(srcUasset, destUasset);

    private static void CopyTree(string src, string dst)
    {
        if (!Directory.Exists(src)) return;
        foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
        {
            var rel = dir[src.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (rel.StartsWith("Sandfall", StringComparison.Ordinal))
                Directory.CreateDirectory(Path.Combine(dst, rel));
        }
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var idx = file.IndexOf("Sandfall", StringComparison.Ordinal);
            if (idx < 0) continue;
            var dest = Path.Combine(dst, file[idx..]);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            FileCopy.Overwrite(file, dest);
        }
    }

    public static void RemovePainterConversions(string modDir)
    {
        DeleteGenerated(modDir, PainterConversions.Select(s => s.Id));
    }

    public static void RemoveStatusAffinities(string modDir)
    {
        DeleteGenerated(modDir, Powers.Select(s => s.Id));
    }

    public static void RemoveHitBurn(string modDir)
    {
        DeleteGenerated(modDir, HitBurn.Select(s => s.Id));
        // Per-tier parents introduced for correct chance (20/40/60/80/100) and self-target.
        DeleteGenerated(modDir, HitBurn.Select(s => "DodgeCanInflictStatus_" + s.Id));
    }

    public static void RemoveStainPictos(string modDir) =>
        DeleteGenerated(modDir, StainPictos.Select(s => s.Id));



    /// <summary>
    /// Vanilla Cursed Power becomes tier I: renamed and its hover text aligned
    /// with the created ladder (the balancing value is set in
    /// InjectStatusBalancing).
    /// </summary>
    private static void ReworkVanillaCursedPower(string modDir)
    {
        var itemsPath = FindAsset(modDir, "DT_jRPG_Items.uasset")
            ?? throw new FileNotFoundException("DT_jRPG_Items");
        var items = new DataTableEditor(itemsPath);
        if (items.TryGetRow("CursedPower", out var itemRow))
        {
            SetRowText(items, itemRow, "Item_DisplayName", "PASSIVE_CursedPower_Name", "Cursed Power I");
            SetRowText(items, itemRow, "ItemDescription", "PASSIVE_CursedPower_Description",
                "Deal 5% more damage while Cursed.");
            items.Save(itemsPath);
        }
        var pePath = FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? throw new FileNotFoundException("DT_PassiveEffects");
        var pe = new DataTableEditor(pePath);
        if (pe.TryGetRow("CursedPower", out var peRow))
        {
            SetRowText(pe, peRow, "Name", "PASSIVE_CursedPower_Name", "Cursed Power I");
            SetRowText(pe, peRow, "Description", "PASSIVE_CursedPower_Description",
                "Deal 5% more damage while Cursed.");
            foreach (var f in peRow.Value)
                if (DataTableEditor.CleanName(f.Name) == "LuminaCost" && f is IntPropertyData c)
                    c.Value = 5;
            pe.Save(pePath);
        }
    }

    internal static void DeleteGenerated(string modDir, IEnumerable<string> ids)
    {
        if (!Directory.Exists(modDir)) return;
        foreach (var id in ids)
        {
            foreach (var name in new[]
                     {
                         "BP_LU_" + id + ".uasset", "BP_LU_" + id + ".uexp",
                         "DA_GearPictos_" + id + ".uasset", "DA_GearPictos_" + id + ".uexp",
                     })
            {
                var hit = FindAsset(modDir, name);
                if (hit != null) File.Delete(hit);
            }
        }
    }

    public static string? FindAsset(string root, string fileName)
    {
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).FirstOrDefault();
    }
}

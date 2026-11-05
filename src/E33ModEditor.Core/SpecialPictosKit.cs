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
/// Five hand-authored unique pictos:
///  - <b>Alicia's Suffering</b> — +1000% burn damage (clones the cut
///    <c>BP_LU_BurnDamage</c>, whose balancing value is an additive burn-damage
///    modifier). Drops only from <c>RC_Alicia</c> (The Reacher).
///  - <b>Layered Canvas</b> — at the start of every turn the wearer gains
///    1 stack of Burn / Berserk / Blight / Regen / Powerful / Shell / Rush for
///    1 turn. Built from seven clones of the turn-start
///    <c>BP_LU_SelfBurnButDmg+</c> (one per status), all listed in the passive's
///    <c>BattleBuffs</c> array. Drops only from <c>CFH_Boss_Clea</c>.
///  - <b>Super Sonic</b> — grants Speed only at 3x the shared picto curve, and
///    refreshes a custom <b>Super Sonic</b> status (+100% Speed) on the wearer
///    every turn start, so it lasts the whole battle. The status is a clone of
///    the vanilla Rush buff <c>BP_BattleBuff_Speed</c> (Speed x1.33) with the
///    CDO's <c>CharacterStatMultipliers</c> multiplier raised to 2.0; the
///    turn-start applier is a clone of <c>BP_LU_SelfBurnButDmg+</c> retargeted
///    from Burn to that buff. Drops only from <c>LU_EchassierAlpha</c>
///    (Act 3 Lumière Chromatic Echassier).
///  - <b>Rupture Shot</b> — Free Aim shots can inflict the vanilla <b>Inverted</b>
///    status (the target can't be healed and healing damages it instead) at a
///    raised chance. Clones the vanilla Free-Aim Inverted passive; drops only
///    from <c>L_Creation</c> (Act 3 Lumière Création).
///  - <b>Uno Puncho</b> — the first hit deals 10x damage; every hit after deals
///    no damage. Built from <c>BP_LU_FirstLife</c> (a permanent, stateful
///    damage modifier) by changing its modifier type to a direct multiplier,
///    setting the "after death" row to 0×, and marking the buff as spent after
///    the first hit. It is not a random drop: it is an extra Ono Puncho
///    dialogue reward for the 9,999-damage objective. Costs 21 lumina.
///
/// Follows the PictosCreateKit recipe: unique /Game package per DA and BP,
/// item row on DT_jRPG_Items, passive + definition + icon + balancing rows.
/// </summary>
public static class SpecialPictosKit
{
    public const string AliciaId = "AliciasSuffering";
    public const string LayeredId = "LayeredCanvas";
    public const string SuperSonicId = "SuperSonic";
    public const string RuptureId = "RuptureShot";
    public const string UnoPunchoId = "UnoPuncho";

    /// <summary>Guaranteed-drop fight for each picto (see PinExclusiveDrops).</summary>
    public const string AliciaEnemy = "RC_Alicia";
    public const string LayeredEnemy = "CFH_Boss_Clea";
    public const string SuperSonicEnemy = "LU_EchassierAlpha";
    public const string RuptureEnemy = "L_Creation";

    /// <summary>Lumina cost for all specials (vanilla's priciest is 40).</summary>
    public const int LuminaCost = 100;

    /// <summary>Uno Puncho's cheap, one-shot gimmick price.</summary>
    public const int UnoPunchoLuminaCost = 21;

    /// <summary>First-hit direct multiplier: BaseDamage × 10.</summary>
    public const float UnoPunchoDamageIncrease = 10.0f;

    /// <summary>
    /// After the first hit: a direct multiplier of 0. Type-2 multipliers cannot
    /// go negative, so this is a true no-damage hit rather than the old huge
    /// flat penalty (which produced negative damage and healed the target when
    /// the clamp was inactive).
    /// </summary>
    public const float UnoPunchoDamageDecrease = 0.0f;

    /// <summary>Super Sonic picto stat: Speed only, 3x the shared picto curve.</summary>
    public const float SuperSonicSpeed = 3.0f;

    /// <summary>Super Sonic status: Rush's Speed multiplier raised 1.33 -> 2.0 (+100%).</summary>
    public const double SuperSonicStatusMultiplier = 2.0d;

    /// <summary>Rupture Shot: Free-Aim chance to inflict Inverted (vanilla is 33%).</summary>
    public const float RuptureChance = 50.0f;

    /// <summary>Rupture Shot: turns the Inverted status lasts when applied.</summary>
    public const int RuptureDuration = 5;

    /// <summary>+1000% burn damage (the vanilla BurnDamage row is +50%).</summary>
    public const float BurnDamageMultiplier = 10.0f;

    private const string GearDir = "Sandfall/Content/Gameplay/Pictos/Gear/NewLumina";

    /// <summary>
    /// The same folder as <see cref="GearDir"/> but as a mounted game path. The
    /// extracted tree prefixes everything with "Sandfall/Content"; the /Game
    /// mount point does not, so the two forms must not be interchanged when
    /// building object imports (doing so points at /Game/Sandfall/... — a package
    /// that does not exist, leaving the item's Pictos_Data unresolved).
    /// </summary>
    private const string GearGameDir = "Gameplay/Pictos/Gear/NewLumina";

    private const string PassiveDir = "Sandfall/Content/Gameplay/Lumina/Passives";
    private const string DonorDa = "DA_GearPictos_DoubleBurn";
    private const string DonorItem = "DoubleBurn";
    private const string BurnDamageBp = "BP_LU_BurnDamage";
    private const string DonorTurnBp = "BP_LU_SelfBurnButDmg+";

    private static readonly string[] ItemTables = { "DT_jRPG_Items", "DT_jRPG_Items_Composite" };

    private const string StatHp = "E_jRPG_StatType::NewEnumerator1";
    private const string StatDef = "E_jRPG_StatType::NewEnumerator4";
    private const string StatCrit = "E_jRPG_StatType::NewEnumerator6";
    private const string StatSpd = "E_jRPG_StatType::NewEnumerator7";

    /// <summary>
    /// One Layered Canvas status. <c>FromCls/FromPath</c> is the donor's Burn
    /// reference; <c>Cls/Path</c> is the target status buff. Replacing longest
    /// string first keeps the embedded class name inside the package path intact.
    /// </summary>
    private sealed record StatusSpec(
        string Key, string Display, string Cls, string Path, string BuffProperty);

    private static readonly StatusSpec[] Statuses =
    {
        new("Burn", "Burn", "BP_BattleBuff_Burn",
            "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Burn", "BurnMultiplier"),
        new("Berserk", "Berserk", "BP_BattleBuff_Berserk",
            "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Berserk", "BurnMultiplier"),
        new("Blight", "Blight", "BP_BattleBuff_Blight",
            "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Blight", "BurnMultiplier"),
        new("Regen", "Regen", "BP_BattleBuff_Regen",
            "/Game/Gameplay/Buffs/UniqueBuffs/BP_BattleBuff_Regen", "BurnMultiplier"),
        new("Powerful", "Powerful", "BP_BattleBuff_Powerful_125",
            "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Powerful_125", "BurnMultiplier"),
        new("Shell", "Shell", "BP_BattleBuff_Defense15",
            "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Defense15", "BurnMultiplier"),
        new("Rush", "Rush", "BP_BattleBuff_Speed",
            "/Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_Speed", "BurnMultiplier"),
    };

    private const string DonorFromCls = "BP_BattleBuff_Burn";
    private const string DonorFromPath = "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Burn";

    // ── Super Sonic status ──
    // Vanilla Rush buff (Speed x1.33) is cloned for the custom status; the
    // turn-start applier donor is the same BP_LU_SelfBurnButDmg+ clone source
    // Layered Canvas uses, retargeted from Burn to the custom buff.
    private const string SpeedBp = "BP_BattleBuff_Speed";
    private const string SpeedPath = "/Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_Speed";
    private const string ParentBuffDir = "Sandfall/Content/Gameplay/Buffs/ParentClasses";
    private const string SuperSonicBuffStem = "BP_LU_" + SuperSonicId + "_Buff";

    // ── Rupture Shot status ──
    // Vanilla "Free Aim Inverted Shot" is the complete, CDO-parseable member of
    // the pair (its twin BP_LU_InvertShot drops its CDO as raw bytes): it applies
    // BP_BattleBuff_Inverted on Free Aim, with ChancePercentage / BuffToApply /
    // BuffToApplyTurn Duration on its own CDO and matching balancing keys.
    private const string RuptureBpDonor = "BP_LU_FreeAimInvertedShot";
    private const string RuptureBpParent = "BP_LU_FreeAimShotEffectParent";
    private const string RuptureDaDonor = "DA_GearPictos_FreeAimInvertedShot";
    private const string RuptureRowDonor = "FreeAimInvertedShot";

    // ── Uno Puncho ──
    // FirstLife is a permanent buff that already selects one of two balancing
    // rows through its `HasDied` instance bool. The first hit uses the
    // "increase" row; setting the state after that hit makes every later hit
    // use the "decrease" row.
    private const string UnoPunchoBpDonor = "BP_LU_FirstLife";
    private const string UnoPunchoDaDonor = "DA_GearPictos_Immaculate";
    private const string UnoPunchoRowDonor = "FirstLife";
    private const string OnoPunchoDialogue = "BP_Dialogue_GV_OnoPuncho";
    private const string OnoPunchoOriginalReward = "SkinMaelle_Potatoe_Bag";

    /// <summary>True when the special pictos live in the packed tree (revert detection).</summary>
    public static bool HasGeneratedAssets(string modDir) =>
        PictosCreateKit.FindAsset(modDir, "BP_LU_" + AliciaId + ".uasset") is not null
        || PictosCreateKit.FindAsset(modDir, "BP_LU_" + LayeredId + "_Burn.uasset") is not null
        || PictosCreateKit.FindAsset(modDir, "BP_LU_" + SuperSonicId + ".uasset") is not null
        || PictosCreateKit.FindAsset(modDir, "BP_LU_" + RuptureId + ".uasset") is not null
        || PictosCreateKit.FindAsset(modDir, "BP_LU_" + UnoPunchoId + ".uasset") is not null;

    public static int Apply(string modDir)
    {
        // The donors already live in work/tables (the same extraction the other
        // picto kits use). FindTablesAsset keeps us on the existing copy rather
        // than triggering a fresh extraction that ~mods would block.
        var turnBp = FileCopy.FindTablesAsset(DonorTurnBp + ".uasset")
            ?? throw new FileNotFoundException(DonorTurnBp);
        var burnBp = FileCopy.FindTablesAsset(BurnDamageBp + ".uasset")
            ?? throw new FileNotFoundException(BurnDamageBp);
        var gearDa = FileCopy.FindTablesAsset(DonorDa + ".uasset")
            ?? throw new FileNotFoundException(DonorDa);
        // Super Sonic needs the vanilla Rush buff (BP_BattleBuff_Speed). It is in
        // donor_filters.txt so the packer stages it; EnsureExtracted covers a
        // dev checkout that has not run DonorExtract yet.
        PictosCreateKit.EnsureExtracted(SpeedBp);
        var speedBp = FileCopy.FindTablesAsset(SpeedBp + ".uasset")
            ?? throw new FileNotFoundException(SpeedBp);
        // Rupture Shot clones the Free-Aim Inverted passive; its CDO only parses
        // with the schema parent on disk, so pull both into one shared donor root.
        var ruptureRoot = PictosCreateKit.EnsureExtractedShared("RuptureShotDonor",
            RuptureBpDonor, RuptureBpParent);
        var ruptureBp = PictosCreateKit.FindAsset(ruptureRoot, RuptureBpDonor + ".uasset")
            ?? throw new FileNotFoundException(RuptureBpDonor);
        PictosCreateKit.EnsureExtracted(RuptureDaDonor);
        var ruptureDa = FileCopy.FindTablesAsset(RuptureDaDonor + ".uasset")
            ?? throw new FileNotFoundException(RuptureDaDonor);
        // Uno Puncho reuses FirstLife's stateful two-row damage modifier and
        // Immaculate's structurally-identical gear DA (FirstLife's own DA is
        // not extractable while a mod is installed).
        PictosCreateKit.EnsureExtracted(UnoPunchoBpDonor);
        PictosCreateKit.EnsureExtracted(UnoPunchoDaDonor);
        var unoBp = FileCopy.FindTablesAsset(UnoPunchoBpDonor + ".uasset")
            ?? throw new FileNotFoundException(UnoPunchoBpDonor);
        var unoDa = FileCopy.FindTablesAsset(UnoPunchoDaDonor + ".uasset")
            ?? throw new FileNotFoundException(UnoPunchoDaDonor);
        PictosCreateKit.EnsureExtracted("ST_PassiveEffects");

        int added = 0;
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);

        added += InjectAliciasSuffering(modDir, burnBp, gearDa, strings);
        added += InjectLayeredCanvas(modDir, turnBp, gearDa, strings);
        added += InjectSuperSonic(modDir, speedBp, turnBp, gearDa, strings);
        added += InjectRuptureShot(modDir, ruptureBp, ruptureDa, strings);
        added += InjectUnoPuncho(modDir, unoBp, unoDa, strings);

        InjectItemRows(modDir, strings);
        InjectPassiveRows(modDir);
        InjectDefinitionRows(modDir);
        InjectSuperSonicRows(modDir, strings);
        InjectRuptureRows(modDir, strings);
        InjectUnoPunchoRows(modDir, strings);
        InjectBalancing(modDir);
        PictosCreateKit.ClonePictoIconRows(modDir, new[]
        {
            (DonorItem, AliciaId), (DonorItem, LayeredId), (DonorItem, SuperSonicId),
            (RuptureRowDonor, RuptureId), (UnoPunchoRowDonor, UnoPunchoId),
        });
        var st = PictosCreateKit.FindAsset(modDir, "ST_PassiveEffects.uasset");
        if (st != null) DataTableEditor.AddStringTableEntries(st, strings);

        // Replace the dialogue's vanilla Potatoe Bag reward with the new
        // picto. This is a data-only CDO row swap: the dialogue's bytecode
        // (and its single AddItemToInventory call) stays vanilla, so the
        // completed-quest dialogue cannot crash on a second inventory add.
        E33ModEditor.Core.Loot.DialogueRewardKit.ReplaceFixedReward(
            modDir, OnoPunchoDialogue, OnoPunchoOriginalReward, UnoPunchoId);
        return added;
    }

    public static void Remove(string modDir)
    {
        foreach (var id in new[] { AliciaId, LayeredId, SuperSonicId, RuptureId, UnoPunchoId })
        {
            PictosCreateKit.DeleteGenerated(modDir, new[] { id });
            PictosCreateKit.DeleteGenerated(modDir,
                Statuses.Select(s => LayeredId + "_" + s.Key));
        }
        // The custom Super Sonic status buff (BP_LU_SuperSonic_Buff).
        PictosCreateKit.DeleteGenerated(modDir, new[] { SuperSonicId + "_Buff" });
    }

    // ───────────────────── Alicia's Suffering ─────────────────────

    private static int InjectAliciasSuffering(string modDir, string burnBp, string gearDa,
        Dictionary<string, string> strings)
    {
        var bpDest = Path.Combine(modDir, PassiveDir, "BP_LU_" + AliciaId + ".uasset");
        CloneBlueprint(burnBp, bpDest, "BP_LU_" + AliciaId, new[]
        {
            ("BurnDamage_BurnDamage_DamageMultiplier", AliciaId + "_BurnDamage_DamageMultiplier"),
        });
        CloneGearAsset(gearDa, modDir, "DA_GearPictos_" + AliciaId, AliciaId);
        strings[$"PASSIVE_{AliciaId}_Name"] = "Alicia's Suffering";
        strings[$"PASSIVE_{AliciaId}_Description"] =
            "Raises all Burn damage the wearer causes by 1000%.";
        return 1;
    }

    // ───────────────────── Layered Canvas ─────────────────────

    private static int InjectLayeredCanvas(string modDir, string turnBp, string gearDa,
        Dictionary<string, string> strings)
    {
        foreach (var st in Statuses)
        {
            var bpStem = "BP_LU_" + LayeredId + "_" + st.Key;
            var bpDest = Path.Combine(modDir, PassiveDir, bpStem + ".uasset");
            var replace = new List<(string From, string To)>
            {
                // Balancing keys (and the CDO's own Name) keep the donor token
                // without the BP_LU_ prefix, so they need the bare stem swap.
                (DonorTurnBp["BP_LU_".Length..], LayeredId + "_" + st.Key),
                (DonorFromCls, st.Cls),
                (DonorFromPath, st.Path),
            };
            CloneBlueprint(turnBp, bpDest, bpStem, replace);
        }
        CloneGearAsset(gearDa, modDir, "DA_GearPictos_" + LayeredId, LayeredId);
        strings[$"PASSIVE_{LayeredId}_Name"] = "Layered Canvas";
        strings[$"PASSIVE_{LayeredId}_Description"] =
            "Gain 1 Burn, Berserk, Blight, Regen, Powerful, Shell and Rush for 1 turn at the start of each turn.";
        return Statuses.Length;
    }

    // ───────────────────── Super Sonic ─────────────────────

    /// <summary>
    /// Super Sonic: grants Speed only at 3x the shared picto curve, and
    /// refreshes a custom <b>Super Sonic</b> status (Rush with Speed x2.0) on the
    /// wearer every turn start. The status is a clone of <c>BP_BattleBuff_Speed</c>;
    /// the applier clones <c>BP_LU_SelfBurnButDmg+</c> (the Layered Canvas
    /// turn-start donor) with Burn retargeted to that buff.
    /// </summary>
    private static int InjectSuperSonic(string modDir, string speedBp, string turnBp,
        string gearDa, Dictionary<string, string> strings)
    {
        CloneSuperSonicBuff(modDir, speedBp);

        var buffPath = "/Game/Gameplay/Buffs/ParentClasses/" + SuperSonicBuffStem;
        var bpDest = Path.Combine(modDir, PassiveDir, "BP_LU_" + SuperSonicId + ".uasset");
        CloneBlueprint(turnBp, bpDest, "BP_LU_" + SuperSonicId, new[]
        {
            // Bare donor token first so the balancing keys / CDO names are renamed
            // (longest-first ordering in CloneBlueprint keeps the Burn path intact).
            (DonorTurnBp["BP_LU_".Length..], SuperSonicId),
            (DonorFromCls, SuperSonicBuffStem),
            (DonorFromPath, buffPath),
        });

        CloneGearAsset(gearDa, modDir, "DA_GearPictos_" + SuperSonicId, SuperSonicId);
        strings[$"PASSIVE_{SuperSonicId}_Name"] = "Super Sonic";
        strings[$"PASSIVE_{SuperSonicId}_Description"] =
            "Gain Super Sonic (+100% Speed) at the start of every turn.";
        return 1;
    }

    /// <summary>
    /// Clones the vanilla Rush buff (<c>BP_BattleBuff_Speed</c>, Speed x1.33) into
    /// the custom Super Sonic status and raises its CDO
    /// <c>CharacterStatMultipliers</c> multiplier to 2.0 (+100% Speed). The class
    /// and outer Package are both retargeted by RetargetPackage, so the game
    /// resolves the clone as a distinct status.
    /// </summary>
    private static void CloneSuperSonicBuff(string modDir, string speedBp)
    {
        var dest = Path.Combine(modDir, ParentBuffDir, SuperSonicBuffStem + ".uasset");
        PictosCreateKit.CopyPair(speedBp, dest);
        var asset = PictosCreateKit.OpenAsset(dest);
        PictosCreateKit.RetargetPackage(asset, dest, SpeedBp, SuperSonicBuffStem);

        bool patched = false;
        foreach (var export in asset.Exports.OfType<NormalExport>())
        {
            foreach (var p in export.Data)
            {
                var n = DataTableEditor.CleanName(p.Name);
                if (n == "CharacterStatMultipliers" && p is ArrayPropertyData arr)
                {
                    foreach (var el in arr.Value.OfType<StructPropertyData>())
                        foreach (var f in el.Value)
                            if (f is DoublePropertyData d
                                && DataTableEditor.CleanName(f.Name) == "Multiplier")
                            {
                                d.Value = SuperSonicStatusMultiplier;
                                patched = true;
                            }
                }
                else if (n == "Name" && p is TextPropertyData name)
                    SetInlineText(name, "BUFF_SuperSonic_Name", "Super Sonic");
                else if (n == "Description" && p is TextPropertyData desc)
                    SetInlineText(desc, "BUFF_SuperSonic_Description", "Speed increased by 100%.");
            }
        }
        if (!patched)
            throw new InvalidDataException(SpeedBp +
                " CDO has no CharacterStatMultipliers multiplier to raise (unparseable CDO?)");
        asset.Write(dest);
    }

    /// <summary>Item / passive / definition / balancing rows for Super Sonic.</summary>
    private static void InjectSuperSonicRows(string modDir, Dictionary<string, string> strings)
    {
        var spread = new StatSpread(0f, 0f, SuperSonicSpeed, 0f, 1.50f, 0, 0, 220, 0);
        var edits = new JObject { [SuperSonicId] = ItemStatEdit(spread) };
        foreach (var table in ItemTables)
        {
            var path = PictosCreateKit.FindAsset(modDir, table + ".uasset")
                ?? throw new FileNotFoundException(table);
            var ed = new DataTableEditor(path);
            ed.CloneRow(DonorItem, SuperSonicId);
            RewriteItem(ed, SuperSonicId, strings);
            ed.Apply(edits.ToString());
            ed.Save(path);
        }

        var pePath = PictosCreateKit.FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? throw new FileNotFoundException("DT_PassiveEffects");
        var pe = new DataTableEditor(pePath);
        pe.CloneRow(DonorItem, SuperSonicId);
        if (pe.TryGetRow(SuperSonicId, out var peRow))
        {
            PictosCreateKit.SetRowText(pe, peRow, "Name",
                $"PASSIVE_{SuperSonicId}_Name", strings[$"PASSIVE_{SuperSonicId}_Name"]);
            PictosCreateKit.SetRowText(pe, peRow, "Description",
                $"PASSIVE_{SuperSonicId}_Description", strings[$"PASSIVE_{SuperSonicId}_Description"]);
            foreach (var f in peRow.Value)
                if (DataTableEditor.CleanName(f.Name) == "LuminaCost" && f is IntPropertyData c)
                    c.Value = LuminaCost;
            SetBattleBuffs(pe, peRow, new[]
            {
                ($"/Game/Gameplay/Lumina/Passives/BP_LU_{SuperSonicId}",
                    $"BP_LU_{SuperSonicId}_C"),
            });
        }
        pe.Save(pePath);

        var defPath = PictosCreateKit.FindAsset(modDir, "DT_PictosDefinitions.uasset")
            ?? throw new FileNotFoundException("DT_PictosDefinitions");
        var def = new DataTableEditor(defPath);
        def.CloneRow(DonorItem, SuperSonicId);
        if (def.TryGetRow(SuperSonicId, out var defRow))
            foreach (var f in defRow.Value)
                if (DataTableEditor.CleanName(f.Name) == "ItemID" && f is NamePropertyData nm)
                    nm.Value = new FName(def.Asset, SuperSonicId);
        def.Apply(new JObject { [SuperSonicId] = DefStatEdit(spread) }.ToString());
        def.Save(defPath);

        // The turn-start applier's Burn riders are inert for a stat buff: no
        // self-burn and no damage-per-stack, and the status lasts 1 turn so it is
        // re-applied every turn start (effectively permanent while worn).
        var balPath = PictosCreateKit.FindAsset(modDir, "DT_Balancing_Luminas.uasset")
            ?? throw new FileNotFoundException("DT_Balancing_Luminas");
        var bal = new DataTableEditor(balPath);
        var balEdits = new JObject();
        var donorKey = DonorTurnBp["BP_LU_".Length..];
        AddClone(bal, balEdits, SuperSonicId + "_BurnSelfDamageMultiplier",
            donorKey + "_BurnSelfDamageMultiplier", 0f);
        AddClone(bal, balEdits, SuperSonicId + "_DamageIncreasePerBurnStack",
            donorKey + "_DamageIncreasePerBurnStack", 0f);
        AddClone(bal, balEdits, SuperSonicId + "_Duration", donorKey + "_Duration", 1f);
        bal.Apply(balEdits.ToString());
        bal.Save(balPath);
    }

    // ───────────────────── Rupture Shot ─────────────────────

    /// <summary>
    /// Rupture Shot: Free Aim shots can inflict the vanilla <b>Inverted</b> status
    /// (the target can't be healed and healing damages it instead). Clones the
    /// vanilla Free-Aim Inverted passive — the complete, CDO-parseable member of
    /// the InvertShot pair — and raises the application chance.
    /// </summary>
    private static int InjectRuptureShot(string modDir, string ruptureBp, string ruptureDa,
        Dictionary<string, string> strings)
    {
        var bpDest = Path.Combine(modDir, PassiveDir, "BP_LU_" + RuptureId + ".uasset");
        CloneRuptureShotBp(ruptureBp, bpDest);
        CloneGearAsset(ruptureDa, modDir, "DA_GearPictos_" + RuptureId, RuptureId);
        strings[$"PASSIVE_{RuptureId}_Name"] = "Rupture Shot";
        strings[$"PASSIVE_{RuptureId}_Description"] =
            "Free Aim shots can inflict Inverted on the target.";
        return 1;
    }

    /// <summary>
    /// Opens the donor from the shared donor root (its schema parent sits beside it
    /// there, so the unversioned CDO parses), retargets the package/class, renames
    /// the balancing keys, and raises the CDO chance. Writing from the opened
    /// SOURCE — not a pre-copied dest — is the repo's rule for these CDOs.
    /// </summary>
    private static void CloneRuptureShotBp(string srcUasset, string destUasset)
    {
        var asset = PictosCreateKit.OpenAsset(srcUasset);
        PictosCreateKit.RetargetPackage(asset, destUasset, RuptureBpDonor, "BP_LU_" + RuptureId);
        var names = asset.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s)) continue;
            // Balancing keys keep the bare donor token (FreeAimInvertedShot_*),
            // which RetargetPackage's BP_LU_ stem swap does not touch.
            var next = s.Replace(RuptureRowDonor, RuptureId, StringComparison.Ordinal);
            if (next != s) asset.SetNameReference(i, FString.FromString(next));
        }

        bool patched = false;
        foreach (var export in asset.Exports.OfType<NormalExport>())
            foreach (var p in export.Data)
            {
                var n = DataTableEditor.CleanName(p.Name);
                if (n == "ChancePercentage" && p is DoublePropertyData d)
                {
                    d.Value = RuptureChance;
                    patched = true;
                }
                else if (n == "Name" && p is TextPropertyData t)
                    SetInlineText(t, "BUFF_RuptureShot_Name", "Rupture Shot");
            }
        if (!patched)
            throw new InvalidDataException(RuptureBpDonor +
                " CDO did not parse; ChancePercentage not set (missing schema parent?)");
        Directory.CreateDirectory(Path.GetDirectoryName(destUasset)!);
        asset.Write(destUasset);
    }

    /// <summary>Item / passive / definition / balancing rows for Rupture Shot.</summary>
    private static void InjectRuptureRows(string modDir, Dictionary<string, string> strings)
    {
        // Premium two-stat spread (Speed + Crit), chroma 1.5.
        var spread = new StatSpread(0f, 0f, 1.50f, 0.50f, 1.50f, 0, 0, 75, 8);
        var edits = new JObject { [RuptureId] = ItemStatEdit(spread) };
        foreach (var table in ItemTables)
        {
            var path = PictosCreateKit.FindAsset(modDir, table + ".uasset")
                ?? throw new FileNotFoundException(table);
            var ed = new DataTableEditor(path);
            ed.CloneRow(RuptureRowDonor, RuptureId);
            RewriteItem(ed, RuptureId, strings);
            ed.Apply(edits.ToString());
            ed.Save(path);
        }

        var pePath = PictosCreateKit.FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? throw new FileNotFoundException("DT_PassiveEffects");
        var pe = new DataTableEditor(pePath);
        pe.CloneRow(RuptureRowDonor, RuptureId);
        if (pe.TryGetRow(RuptureId, out var peRow))
        {
            PictosCreateKit.SetRowText(pe, peRow, "Name",
                $"PASSIVE_{RuptureId}_Name", strings[$"PASSIVE_{RuptureId}_Name"]);
            PictosCreateKit.SetRowText(pe, peRow, "Description",
                $"PASSIVE_{RuptureId}_Description", strings[$"PASSIVE_{RuptureId}_Description"]);
            foreach (var f in peRow.Value)
                if (DataTableEditor.CleanName(f.Name) == "LuminaCost" && f is IntPropertyData c)
                    c.Value = LuminaCost;
            SetBattleBuffs(pe, peRow, new[]
            {
                ($"/Game/Gameplay/Lumina/Passives/BP_LU_{RuptureId}",
                    $"BP_LU_{RuptureId}_C"),
            });
        }
        pe.Save(pePath);

        var defPath = PictosCreateKit.FindAsset(modDir, "DT_PictosDefinitions.uasset")
            ?? throw new FileNotFoundException("DT_PictosDefinitions");
        var def = new DataTableEditor(defPath);
        def.CloneRow(RuptureRowDonor, RuptureId);
        if (def.TryGetRow(RuptureId, out var defRow))
            foreach (var f in defRow.Value)
                if (DataTableEditor.CleanName(f.Name) == "ItemID" && f is NamePropertyData nm)
                    nm.Value = new FName(def.Asset, RuptureId);
        def.Apply(new JObject { [RuptureId] = DefStatEdit(spread) }.ToString());
        def.Save(defPath);

        var balPath = PictosCreateKit.FindAsset(modDir, "DT_Balancing_Luminas.uasset")
            ?? throw new FileNotFoundException("DT_Balancing_Luminas");
        var bal = new DataTableEditor(balPath);
        var balEdits = new JObject();
        AddClone(bal, balEdits, RuptureId + "_ChancePercentage",
            RuptureRowDonor + "_ChancePercentage", RuptureChance);
        AddClone(bal, balEdits, RuptureId + "_BuffToApplyDuration",
            RuptureRowDonor + "_BuffToApplyDuration", RuptureDuration);
        bal.Apply(balEdits.ToString());
        bal.Save(balPath);
    }

    // ───────────────────── Uno Puncho ─────────────────────

    /// <summary>
    /// The first hit is ×10, every later hit is ×0. FirstLife already selects
    /// between a "before death" and an "after death" balancing row with its
    /// <c>HasDied</c> bool, so the clone needs to (a) rename that state's rows,
    /// (b) put 10.0 / 0.0 in them, (c) switch its <c>FDamageModifier</c> from
    /// flat additive (type 3) to direct multiplier (type 2), and (d) set
    /// <c>HasDied</c> after the first damage event. A 0.0 direct multiplier
    /// cannot go negative, so subsequent hits deal exactly zero instead of
    /// healing through a negative flat sum.
    /// </summary>
    private static int InjectUnoPuncho(string modDir, string firstLifeBp, string gearDa,
        Dictionary<string, string> strings)
    {
        var bpDest = Path.Combine(modDir, PassiveDir, "BP_LU_" + UnoPunchoId + ".uasset");
        CloneUnoPunchoBp(firstLifeBp, bpDest);
        CloneGearAsset(gearDa, modDir, "DA_GearPictos_" + UnoPunchoId, UnoPunchoId);
        strings[$"PASSIVE_{UnoPunchoId}_Name"] = "Uno Puncho";
        strings[$"PASSIVE_{UnoPunchoId}_Description"] =
            "First hit deals 10x damage. All following hits deal no damage.";
        return 1;
    }

    private static void CloneUnoPunchoBp(string srcUasset, string destUasset)
    {
        PictosCreateKit.CopyPair(srcUasset, destUasset);
        var asset = PictosCreateKit.OpenAsset(destUasset);
        var newStem = "BP_LU_" + UnoPunchoId;
        PictosCreateKit.RetargetPackage(asset, destUasset, UnoPunchoBpDonor, newStem);

        // Retarget the bare donor token as well: this covers the two balancing
        // keys and the CDO's own Name. `RetargetPackage` only handles the BP stem.
        var names = asset.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s)) continue;
            var next = s.Replace(UnoPunchoRowDonor, UnoPunchoId, StringComparison.Ordinal);
            if (next != s) asset.SetNameReference(i, FString.FromString(next));
        }

        foreach (var export in asset.Exports.OfType<NormalExport>())
            foreach (var p in export.Data)
                if (DataTableEditor.CleanName(p.Name) == "Name" && p is TextPropertyData name)
                    SetInlineText(name, "BUFF_UnoPuncho_Name", "Uno Puncho");

        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "ProcessDealtRawDamages")
            ?? throw new InvalidDataException(UnoPunchoBpDonor + " has no ProcessDealtRawDamages");
        var stmts = fn.ScriptBytecode?.ToList()
            ?? throw new InvalidDataException(UnoPunchoBpDonor + " has no bytecode");

        // FirstLife builds an additive FDamageModifier (serialized type 3).
        // Uno Puncho needs a direct multiplier (serialized type 2) so the
        // after-first-hit value can be exactly 0x. A flat negative value would
        // be applied as negative damage and heal the target whenever the game's
        // optional 9,999 clamp is not active.
        int typePatches = 0;
        foreach (var st in stmts)
            MayhemFixKit.Walk(new[] { st }, e =>
            {
                if (e is EX_Let let
                    && let.Value is { } ptr
                    && PartyFiveKit.FieldName(ptr)?.StartsWith("Type_17_", StringComparison.Ordinal) == true
                    && let.Expression is EX_ByteConst bc)
                {
                    bc.Value = 2; // FDamageModifier direct multiplier
                    typePatches++;
                }
            });
        if (typePatches != 1)
            throw new InvalidDataException(UnoPunchoBpDonor +
                $" expected 1 FDamageModifier Type byte, patched {typePatches}");

        int addIdx = -1;
        for (int i = 0; i < stmts.Count && addIdx < 0; i++)
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (addIdx < 0 && e is EX_LocalVirtualFunction vf
                    && vf.VirtualFunctionName.ToString() == "AddDamageModifier")
                    addIdx = i;
            });
        if (addIdx < 0)
            throw new InvalidDataException(UnoPunchoBpDonor + " has no AddDamageModifier statement");

        EX_InstanceVariable? hasDied = null;
        foreach (var st in stmts)
            MayhemFixKit.Walk(new[] { st }, e =>
            {
                if (hasDied is null && e is EX_InstanceVariable iv
                    && PartyFiveKit.FieldName(iv.Variable) == "HasDied")
                    hasDied = iv;
            });
        if (hasDied is null)
            throw new InvalidDataException(UnoPunchoBpDonor + " has no HasDied state");

        var spent = new EX_LetBool
        {
            VariableExpression = (EX_InstanceVariable)MayhemFixKit.CloneKismet(hasDied),
            AssignmentExpression = new EX_True(),
        };

        uint point = 0;
        for (int i = 0; i <= addIdx; i++) point += (uint)stmts[i].GetSize(asset);
        uint delta = (uint)spent.GetSize(asset);
        ShiftOffsetsAtOrAfter(asset, fn, point, delta);
        stmts.Insert(addIdx + 1, spent);
        fn.ScriptBytecode = stmts.ToArray();
        MayhemFixKit.RecalcContextSkips(asset, fn);
        ValidateJumpTargets(asset, fn, "Uno Puncho / ProcessDealtRawDamages");
        asset.Write(destUasset);
    }

    private static void InjectUnoPunchoRows(string modDir, Dictionary<string, string> strings)
    {
        // Vanilla-friendly two-stat spread: Speed + Crit, like Augmented First
        // Strike / Rupture Shot, so the one-shot is easier to land.
        var spread = new StatSpread(0f, 0f, 1.50f, 0.50f, 1.50f, 0, 0, 72, 8);
        var edits = new JObject { [UnoPunchoId] = ItemStatEdit(spread) };
        foreach (var table in ItemTables)
        {
            var path = PictosCreateKit.FindAsset(modDir, table + ".uasset")
                ?? throw new FileNotFoundException(table);
            var ed = new DataTableEditor(path);
            ed.CloneRow(UnoPunchoRowDonor, UnoPunchoId);
            RewriteItem(ed, UnoPunchoId, strings);
            ed.Apply(edits.ToString());
            ed.Save(path);
        }

        var pePath = PictosCreateKit.FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? throw new FileNotFoundException("DT_PassiveEffects");
        var pe = new DataTableEditor(pePath);
        pe.CloneRow(UnoPunchoRowDonor, UnoPunchoId);
        if (pe.TryGetRow(UnoPunchoId, out var peRow))
        {
            PictosCreateKit.SetRowText(pe, peRow, "Name",
                $"PASSIVE_{UnoPunchoId}_Name", strings[$"PASSIVE_{UnoPunchoId}_Name"]);
            PictosCreateKit.SetRowText(pe, peRow, "Description",
                $"PASSIVE_{UnoPunchoId}_Description", strings[$"PASSIVE_{UnoPunchoId}_Description"]);
            foreach (var f in peRow.Value)
                if (DataTableEditor.CleanName(f.Name) == "LuminaCost" && f is IntPropertyData c)
                    c.Value = UnoPunchoLuminaCost;
            SetBattleBuffs(pe, peRow, new[]
            {
                ($"/Game/Gameplay/Lumina/Passives/BP_LU_{UnoPunchoId}",
                    $"BP_LU_{UnoPunchoId}_C"),
            });
        }
        pe.Save(pePath);

        var defPath = PictosCreateKit.FindAsset(modDir, "DT_PictosDefinitions.uasset")
            ?? throw new FileNotFoundException("DT_PictosDefinitions");
        var def = new DataTableEditor(defPath);
        def.CloneRow(UnoPunchoRowDonor, UnoPunchoId);
        if (def.TryGetRow(UnoPunchoId, out var defRow))
            foreach (var f in defRow.Value)
                if (DataTableEditor.CleanName(f.Name) == "ItemID" && f is NamePropertyData nm)
                    nm.Value = new FName(def.Asset, UnoPunchoId);
        def.Apply(new JObject { [UnoPunchoId] = DefStatEdit(spread) }.ToString());
        def.Save(defPath);

        var balPath = PictosCreateKit.FindAsset(modDir, "DT_Balancing_Luminas.uasset")
            ?? throw new FileNotFoundException("DT_Balancing_Luminas");
        var bal = new DataTableEditor(balPath);
        var balEdits = new JObject();
        AddClone(bal, balEdits, UnoPunchoId + "_DamageIncrease",
            UnoPunchoRowDonor + "_DamageIncrease", UnoPunchoDamageIncrease);
        AddClone(bal, balEdits, UnoPunchoId + "_DamageDecrease",
            UnoPunchoRowDonor + "_DamageDecrease", UnoPunchoDamageDecrease);
        bal.Apply(balEdits.ToString());
        bal.Save(balPath);
    }

    /// <summary>At-or-after offsets shift (an insertion exactly on an existing
    /// jump target must move that target past the inserted statement).</summary>
    private static void ShiftOffsetsAtOrAfter(UAsset asset, FunctionExport fn, uint point, uint delta)
    {
        if (delta == 0) return;
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref pos, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j when j.CodeOffset >= point: j.CodeOffset += delta; break;
                    case EX_JumpIfNot jn when jn.CodeOffset >= point: jn.CodeOffset += delta; break;
                    case EX_PushExecutionFlow pf when pf.PushingAddress >= point: pf.PushingAddress += delta; break;
                    // EX_SwitchValue's EndGotoOffset/Case NextOffset are relative
                    // to the switch expression, so an insertion outside it does
                    // not move them.
                }
            });
    }

    private static void ValidateJumpTargets(UAsset asset, FunctionExport fn, string tag)
    {
        var starts = new HashSet<uint>();
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
        {
            starts.Add(pos);
            pos += (uint)st.GetSize(asset);
        }
        var bad = new List<string>();
        uint p = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref p, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j when !starts.Contains(j.CodeOffset): bad.Add($"Jump->{j.CodeOffset}"); break;
                    case EX_JumpIfNot jn when !starts.Contains(jn.CodeOffset): bad.Add($"JumpIfNot->{jn.CodeOffset}"); break;
                    case EX_PushExecutionFlow pf when !starts.Contains(pf.PushingAddress): bad.Add($"PushFlow->{pf.PushingAddress}"); break;
                    // Switch cases use expression-relative offsets, not statement
                    // starts.
                }
            });
        if (bad.Count > 0)
            throw new InvalidDataException($"{tag} has {bad.Count} dangling jump target(s): {string.Join(", ", bad)}");
    }

    private static void CloneGearAsset(string src, string modDir, string newStem, string rowName)
    {
        var dest = Path.Combine(modDir, GearDir, newStem + ".uasset");
        PictosCreateKit.CopyPair(src, dest);
        var ed = DataTableEditor.OpenPropertyAsset(dest);
        var donorStem = Path.GetFileNameWithoutExtension(src);
        RetargetPackage(ed.Asset, dest, donorStem, newStem);
        foreach (var export in ed.Asset.Exports.OfType<NormalExport>())
            foreach (var p in export.Data)
                RewriteRowNames(p, ed.Asset, rowName);
        ed.Asset.Write(dest);
    }

    // ───────────────────── table rows ─────────────────────

    private static void InjectItemRows(string modDir, Dictionary<string, string> strings)
    {
        foreach (var table in ItemTables)
        {
            var path = PictosCreateKit.FindAsset(modDir, table + ".uasset")
                ?? throw new FileNotFoundException(table);
            var ed = new DataTableEditor(path);
            var stats = new JObject();
            foreach (var id in new[] { AliciaId, LayeredId })
            {
                ed.CloneRow(DonorItem, id);
                RewriteItem(ed, id, strings);
                stats[id] = ItemStatEdit(id == AliciaId
                    ? new StatSpread(1.00f, 0f, 0f, 1.00f, 1.50f, 124, 0, 0, 16)
                    : new StatSpread(0f, 0f, 1.43f, 0.48f, 1.50f, 0, 0, 72, 8));
            }
            ed.Apply(stats.ToString());
            ed.Save(path);
        }
    }

    private static void RewriteItem(DataTableEditor ed, string id,
        Dictionary<string, string> strings)
    {
        if (!ed.TryGetRow(id, out var row)) return;
        PictosCreateKit.SetRowText(ed, row, "Item_DisplayName",
            $"PASSIVE_{id}_Name", strings[$"PASSIVE_{id}_Name"]);
        PictosCreateKit.SetRowText(ed, row, "ItemDescription",
            $"PASSIVE_{id}_Description", strings[$"PASSIVE_{id}_Description"]);
        foreach (var f in row.Value)
        {
            var n = DataTableEditor.CleanName(f.Name);
            if (n == "Item_HardcodedName" && f is NamePropertyData nm)
                nm.Value = new FName(ed.Asset, id);
            else if (n == "Pictos_Data" && f is ObjectPropertyData o)
                o.Value = EnsureObjectImport(ed.Asset, GearGameDir, "DA_GearPictos_" + id);
        }
    }

    private static void InjectPassiveRows(string modDir)
    {
        var path = PictosCreateKit.FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? throw new FileNotFoundException("DT_PassiveEffects");
        var ed = new DataTableEditor(path);
        foreach (var id in new[] { AliciaId, LayeredId })
        {
            ed.CloneRow(DonorItem, id);
            if (!ed.TryGetRow(id, out var row)) continue;
            PictosCreateKit.SetRowText(ed, row, "Name", $"PASSIVE_{id}_Name",
                id == AliciaId ? "Alicia's Suffering" : "Layered Canvas");
            PictosCreateKit.SetRowText(ed, row, "Description", $"PASSIVE_{id}_Description",
                id == AliciaId
                    ? "Raises all Burn damage the wearer causes by 1000%."
                    : "Gain 1 Burn, Berserk, Blight, Regen, Powerful, Shell and Rush for 1 turn at the start of each turn.");
            foreach (var f in row.Value.ToList())
                if (DataTableEditor.CleanName(f.Name) == "LuminaCost" && f is IntPropertyData c)
                    c.Value = LuminaCost;

            var buffs = id == AliciaId
                ? new List<(string Pkg, string Cls)>
                {
                    ($"/Game/Gameplay/Lumina/Passives/BP_LU_{AliciaId}", $"BP_LU_{AliciaId}_C"),
                }
                : Statuses.Select(s =>
                {
                    var stem = $"{LayeredId}_{s.Key}";
                    return ($"/Game/Gameplay/Lumina/Passives/BP_LU_{stem}", $"BP_LU_{stem}_C");
                }).ToList();
            SetBattleBuffs(ed, row, buffs);
        }
        ed.Save(path);
    }

    private static void InjectDefinitionRows(string modDir)
    {
        var path = PictosCreateKit.FindAsset(modDir, "DT_PictosDefinitions.uasset")
            ?? throw new FileNotFoundException("DT_PictosDefinitions");
        var ed = new DataTableEditor(path);
        var stats = new JObject();
        foreach (var id in new[] { AliciaId, LayeredId })
        {
            ed.CloneRow(DonorItem, id);
            if (!ed.TryGetRow(id, out var row)) continue;
            foreach (var f in row.Value)
                if (DataTableEditor.CleanName(f.Name) == "ItemID" && f is NamePropertyData nm)
                    nm.Value = new FName(ed.Asset, id);
            stats[id] = DefStatEdit(id == AliciaId
                ? new StatSpread(1.00f, 0f, 0f, 1.00f, 1.50f, 124, 0, 0, 16)
                : new StatSpread(0f, 0f, 1.43f, 0.48f, 1.50f, 0, 0, 72, 8));
        }
        ed.Apply(stats.ToString());
        ed.Save(path);
    }

    private static void InjectBalancing(string modDir)
    {
        var path = PictosCreateKit.FindAsset(modDir, "DT_Balancing_Luminas.uasset")
            ?? throw new FileNotFoundException("DT_Balancing_Luminas");
        var ed = new DataTableEditor(path);
        var edits = new JObject();
        // Alicia's Suffering: the vanilla cut BurnDamage row is an additive
        // burn-damage modifier of 0.5 (+50%); 10.0 = +1000%.
        if (!ed.HasRow(AliciaId + "_BurnDamage_DamageMultiplier"))
            ed.CloneRow(BurnDamageBp["BP_LU_".Length..] + "_BurnDamage_DamageMultiplier",
                AliciaId + "_BurnDamage_DamageMultiplier");
        edits[AliciaId + "_BurnDamage_DamageMultiplier"] = new JObject { ["FloatValue"] = BurnDamageMultiplier };

        // Layered Canvas: 1-turn statuses, no donor damage rider.
        foreach (var st in Statuses)
        {
            var key = $"{LayeredId}_{st.Key}";
            AddClone(ed, edits, key + "_BurnSelfDamageMultiplier",
                DonorTurnBp["BP_LU_".Length..] + "_BurnSelfDamageMultiplier", st.Key == "Burn" ? 1.0f : 0f);
            AddClone(ed, edits, key + "_DamageIncreasePerBurnStack",
                DonorTurnBp["BP_LU_".Length..] + "_DamageIncreasePerBurnStack", 0f);
            AddClone(ed, edits, key + "_Duration",
                DonorTurnBp["BP_LU_".Length..] + "_Duration", 1f);
        }
        ed.Apply(edits.ToString());
        ed.Save(path);
    }

    private static void AddClone(DataTableEditor ed, JObject edits,
        string key, string cloneFrom, float value)
    {
        if (!ed.HasRow(key)) ed.CloneRow(cloneFrom, key);
        edits[key] = new JObject { ["FloatValue"] = value };
    }

    // ───────────────────── helpers ─────────────────────

    private readonly record struct StatSpread(
        float Hp, float Def, float Spd, float Crit, float Chroma,
        int ItemHp, int ItemDef, int ItemSpd, int ItemCrit);

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

    /// <summary>
    /// Rewrites an existing buff FText to inline culture-invariant text. Vanilla
    /// buff names/descriptions are StringTableEntry into ST_Buffs_StatusEffects;
    /// new keys are absent from the cooked locres, so the clone must carry its
    /// literal string instead (same rule as picto hover text).
    /// </summary>
    private static void SetInlineText(TextPropertyData t, string key, string value)
    {
        value = ModLocalization.Inline(key, value);
        t.HistoryType = TextHistoryType.None;
        t.Flags = ETextFlag.CultureInvariant;
        t.Namespace = null;
        t.Value = new FString(value);
        t.CultureInvariantString = new FString(value);
        t.TableId = null;
    }

    private static void SetBattleBuffs(DataTableEditor ed, StructPropertyData row,
        IReadOnlyList<(string Pkg, string Cls)> buffs)
    {
        var field = row.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "BattleBuffs")
            as ArrayPropertyData;
        if (field?.Value is null || field.Value.Length == 0
            || field.Value[0] is not SoftObjectPropertyData template) return;
        var list = new List<PropertyData>();
        foreach (var (pkg, cls) in buffs)
        {
            var pathRef = new FSoftObjectPath(new FName(ed.Asset, pkg), new FName(ed.Asset, cls), null);
            var elem = (SoftObjectPropertyData)template.Clone();
            elem.Value = pathRef;
            list.Add(elem);
            if (ed.Asset.SoftObjectPathList is { Count: > 0 } soft) soft.Add(pathRef);
        }
        var neu = (ArrayPropertyData)field.Clone();
        neu.Value = list.ToArray();
        var idx = row.Value.IndexOf(field);
        if (idx >= 0) row.Value[idx] = neu;
    }

    private static void CloneBlueprint(string srcUasset, string destUasset, string newStem,
        IEnumerable<(string From, string To)> replacements)
    {
        PictosCreateKit.CopyPair(srcUasset, destUasset);
        var asset = PictosCreateKit.OpenAsset(destUasset);
        RetargetPackage(asset, destUasset, Path.GetFileNameWithoutExtension(srcUasset), newStem);
        var ordered = replacements
            .Where(r => !string.IsNullOrEmpty(r.From) && r.From != r.To)
            .OrderByDescending(r => r.From.Length)
            .ToList();
        var names = asset.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s)) continue;
            var next = s;
            foreach (var (from, to) in ordered)
                if (next.Contains(from, StringComparison.Ordinal))
                    next = next.Replace(from, to, StringComparison.Ordinal);
            if (next != s) asset.SetNameReference(i, FString.FromString(next));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(destUasset)!);
        asset.Write(destUasset);
    }

    private static void RetargetPackage(UAsset asset, string destUasset, string srcStem, string newStem)
    {
        var full = destUasset.Replace('\\', '/');
        var idx = full.IndexOf("/Content/", StringComparison.OrdinalIgnoreCase);
        var rel = full[(idx + "/Content/".Length)..];
        if (rel.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)) rel = rel[..^".uasset".Length];
        var newPackage = "/Game/" + rel;
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
            if (!next.Contains(newStem, StringComparison.Ordinal))
                next = next.Replace(srcStem, newStem, StringComparison.Ordinal);
            if (next != s) asset.SetNameReference(i, FString.FromString(next));
        }
    }

    private static void RewriteRowNames(PropertyData p, UAsset asset, string rowName)
    {
        if (p is NamePropertyData n && DataTableEditor.CleanName(n.Name) is "RowName" or "PassiveEffectRowName" or "ItemID")
        {
            n.Value = new FName(asset, rowName);
            return;
        }
        switch (p)
        {
            case StructPropertyData s:
                foreach (var c in s.Value) RewriteRowNames(c, asset, rowName);
                break;
            case ArrayPropertyData a when a.Value != null:
                foreach (var c in a.Value) RewriteRowNames(c, asset, rowName);
                break;
        }
    }

    private static FPackageIndex EnsureObjectImport(UAsset asset, string folder, string objectName)
    {
        var packagePath = "/Game/" + folder + "/" + objectName;
        FPackageIndex? pkgIndex = null;
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (imp.ObjectName.ToString() == packagePath && imp.ClassName.ToString() == "Package")
                pkgIndex = FPackageIndex.FromImport(i);
            if (imp.ObjectName.ToString() == objectName && imp.ClassName.ToString() == "BP_DataAsset_Item_Pictos_C")
                return FPackageIndex.FromImport(i);
        }
        pkgIndex ??= asset.AddImport(new Import("/Script/CoreUObject", "Package",
            FPackageIndex.FromRawIndex(0), packagePath, false, asset));
        // ClassPackage must be the package that DEFINES the class, matching
        // vanilla rows; "/Script/Engine" is a fake Engine class and leaves the
        // Pictos_Data hard ref unresolved.
        return asset.AddImport(new Import("/Game/Gameplay/Inventory/BP_DataAsset_Item_Pictos",
            "BP_DataAsset_Item_Pictos_C", pkgIndex, objectName, false, asset));
    }
}

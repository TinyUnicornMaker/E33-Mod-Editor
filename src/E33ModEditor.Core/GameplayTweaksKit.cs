using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Unversioned;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetAPI.UnrealTypes.EngineEnums;

namespace E33ModEditor.Core;

/// <summary>
/// Packs gameplay-rule tweaks into the mod tree. Vanilla work tables stay untouched.
/// </summary>
public static class GameplayTweaksKit
{
    public const int VanillaLevelCap = 99;
    public const int ExpandedLevelCap = 999;
    public const int DefaultLevelCap = 250;
    public const int VanillaMaxAp = 9;
    public const int VanillaConsecutiveTurns = 2;
    public const float VanillaMaxForetell = 20f;
    public const float VanillaCritDamageIncrease = 0.5f;
    public const int VanillaMaxShields = 9;
    public const int VanillaLuminaPerLevel = 1;
    public const int VanillaAttributePointsPerLevel = 3;
    public const int VanillaSkillPointsPerLevel = 1;
    public const int ExtraLevelXp = 33333333;
    public const int VanillaMaxGradientCharges = 3;
    public const int DefaultMaxGradientCharges = 10;
    public const double VanillaBurnMultiplier = 1.0;
    public const double DefaultBurnMultiplier = 0.66;
    public const double VanillaBurnPlayerScale = 0.2;
    public const double DefaultBurnPlayerScale = 0.01;
    public const int VanillaBurnDuration = 3;
    public const double VanillaBurnTailOff = 2.0;
    public const double VanillaDodgeWindow = 0.22;
    public const double VanillaParryWindow = 0.15;
    public const int VanillaAttributeCap = 99;
    public const int MaxAttributeCap = 500;
    public const int MinPictoPopupCap = PictoPopupCapKit.MinCap;
    public const int DefaultPictoPopupCap = PictoPopupCapKit.DefaultCap;

    public const string StatHp = "E_jRPG_StatType::NewEnumerator1";
    public const string StatAp = "E_jRPG_StatType::NewEnumerator2";
    public const string StatAttack = "E_jRPG_StatType::NewEnumerator3";
    public const string StatDefense = "E_jRPG_StatType::NewEnumerator4";
    public const string StatSpeed = "E_jRPG_StatType::NewEnumerator7";
    public const string StatCrit = "E_jRPG_StatType::NewEnumerator6";
    public sealed class Settings
    {
        public int LevelCap { get; set; } = DefaultLevelCap;
        public int ExtraXpPerLevel { get; set; } = ExtraLevelXp;
        public int AttributePointsPerLevel { get; set; } = 2;
        public int SkillPointsPerLevel { get; set; } = VanillaSkillPointsPerLevel;
        public double HpMult { get; set; } = 1;
        public double AttackMult { get; set; } = 1;
        public double SpeedMult { get; set; } = 1;
        public double CritMult { get; set; } = 1;
        public double DefenseMult { get; set; } = 1;
        public int MaxAP { get; set; } = VanillaMaxAp;
        public int MaxConsecutiveTurns { get; set; } = VanillaConsecutiveTurns;
        public bool Permadeath { get; set; }
        public float MaxForetell { get; set; } = VanillaMaxForetell;
        public float CritDamageIncrease { get; set; } = VanillaCritDamageIncrease;

        /// <summary>
        /// Scale the crit damage multiplier with the attacker's crit rate:
        /// <c>1.5 × max(1, critRate/100)</c> (bytecode splice in BP_BattleDamages;
        /// see <see cref="CritRateDamageKit"/>). On by default (Expedition Infinity);
        /// off is vanilla.
        /// </summary>
        public bool CritRateDamageScaling { get; set; } = true;
        public int MaxShields { get; set; } = VanillaMaxShields;
        public int LuminaPerLevel { get; set; } = 2;
        public int MaxGradientCharges { get; set; } = DefaultMaxGradientCharges;
        public double BurnMultiplier { get; set; } = DefaultBurnMultiplier;
        public double BurnPlayerScale { get; set; } = DefaultBurnPlayerScale;
        public int BurnDuration { get; set; } = VanillaBurnDuration;
        public double BurnTailOff { get; set; } = VanillaBurnTailOff;
        public double DodgeWindow { get; set; } = VanillaDodgeWindow;
        public double ParryWindow { get; set; } = VanillaParryWindow;

        /// <summary>Whether the tuned dodge/parry windows are applied at all.
        /// Off means the table is not staged and both windows are vanilla; the
        /// raw <see cref="DodgeWindow"/> / <see cref="ParryWindow"/> values stay
        /// in the JSON so toggling back restores the last custom preset.</summary>
        public bool TightenDefenseWindows { get; set; }

        /// <summary>Force the tutorial system off and use the no-tutorial
        /// narrative encounters. See <see cref="TutorialDisableKit"/>.</summary>
        public bool DisableTutorials { get; set; } = true;

        public int AttributeCap { get; set; } = VanillaAttributeCap;

        /// <summary>
        /// Visages Mask Keeper keeps the full mask moveset (mask turn-actions,
        /// bound mask actors, Skill_SummonMasks) even after all three mask
        /// sub-bosses are defeated. See <see cref="MaskKeeperKit"/>. On by default
        /// (Expedition Infinity); off is vanilla.
        /// </summary>
        public bool MaskKeeperFullMoveset { get; set; } = true;

        /// <summary>
        /// Sirène (Axon heart) keeps her full Tisseur-synergy moveset
        /// (Skill_TisseurHeal) even after the Tisseur battle is won. See
        /// <see cref="SireneKit"/>. On by default (Expedition Infinity);
        /// off is vanilla.
        /// </summary>
        public bool SireneFullMoveset { get; set; } = true;

        /// <summary>
        /// Use the game's built-in <c>Sandfall.LearnAnyLuminaAfterOneBattle</c>
        /// switch so a picto masters on its first post-battle progress tick
        /// instead of after <c>LuminaUnlockStepsCount</c> (3–4) battles. See
        /// <see cref="AutoPictoMasteryKit"/>. Off is vanilla.
        /// </summary>
        public bool AutoPictoMastery { get; set; }

        /// <summary>
        /// Masters a picto as soon as it enters the inventory by hooking
        /// <c>BP_DataAsset_Item.OnAddedToInventory</c> (the funnel used by
        /// chests, drops, shops and quest rewards). See
        /// <see cref="PictoInstantMasteryKit"/>. Off is vanilla; the one-battle
        /// <see cref="AutoPictoMastery"/> switch is independent.
        /// </summary>
        public bool InstantPictoMastery { get; set; }

        /// <summary>
        /// Caps the number of simultaneous passive / picto proc cards on the
        /// battle HUD. Vanilla is unlimited. When <see cref="LimitPictoPopups"/>
        /// is on, 0 hides every proc card and <see cref="DefaultPictoPopupCap"/>
        /// (5) keeps the screen readable when a party wears 20–30 pictos whose
        /// turn-start effects all proc at once. See
        /// <see cref="PictoPopupCapKit"/>.
        /// </summary>
        public bool LimitPictoPopups { get; set; } = true;
        public int PictoPopupCap { get; set; } = DefaultPictoPopupCap;

        /// <summary>
        /// Raise non-boss enemies' HP / Speed / Attacks per tier on top of the
        /// staged EnemyScaling. See <see cref="RegularEnemyBuffKit"/>. Off is
        /// vanilla. The bonus arrays are kept in the JSON so presets can be
        /// tuned without a code change; the GUI only exposes the switch.
        /// </summary>
        public bool BuffRegularEnemies { get; set; }
        public double[] RegularEnemyHpBonus { get; set; } =
            (double[])RegularEnemyBuffKit.DefaultHpBonus.Clone();
        public double[] RegularEnemySpeedBonus { get; set; } =
            (double[])RegularEnemyBuffKit.DefaultSpeedBonus.Clone();
        public double[] RegularEnemyAttackBonus { get; set; } =
            (double[])RegularEnemyBuffKit.DefaultAttackBonus.Clone();

        /// <summary>
        /// Scale every regular (chest / enemy / merchant) source of
        /// <c>Consumable_LuminaPoint</c>. Dialogue rewards are never touched.
        /// See <see cref="Loot.LuminaDropKit"/>. Off is vanilla.
        /// </summary>
        public bool ReduceLuminaDrops { get; set; }
        public double LuminaDropMultiplier { get; set; } = Loot.LuminaDropKit.DefaultMultiplier;

        public bool ExpandedLevels => LevelCap > VanillaLevelCap;
        public int AttributeTableCap => Math.Max(
            Math.Max(VanillaAttributeCap, AttributeCap),
            LevelCap * Math.Max(1, AttributePointsPerLevel));

        public bool IsVanilla =>
            LevelCap == VanillaLevelCap
            && ExtraXpPerLevel == ExtraLevelXp
            && AttributePointsPerLevel == VanillaAttributePointsPerLevel
            && SkillPointsPerLevel == VanillaSkillPointsPerLevel
            && Near(HpMult, 1) && Near(AttackMult, 1) && Near(SpeedMult, 1)
            && Near(CritMult, 1) && Near(DefenseMult, 1)
            && MaxAP == VanillaMaxAp
            && MaxConsecutiveTurns == VanillaConsecutiveTurns
            && !Permadeath
            && Near(MaxForetell, VanillaMaxForetell)
            && Near(CritDamageIncrease, VanillaCritDamageIncrease)
            && !CritRateDamageScaling
            && MaxShields == VanillaMaxShields
            && LuminaPerLevel == VanillaLuminaPerLevel
            && MaxGradientCharges == VanillaMaxGradientCharges
            && Near(BurnMultiplier, VanillaBurnMultiplier)
            && Near(BurnPlayerScale, VanillaBurnPlayerScale)
            && BurnDuration == VanillaBurnDuration
            && Near(BurnTailOff, VanillaBurnTailOff)
            && (!TightenDefenseWindows
                || (Near(DodgeWindow, VanillaDodgeWindow) && Near(ParryWindow, VanillaParryWindow)))
            && !DisableTutorials
            && AttributeCap == VanillaAttributeCap
            && !MaskKeeperFullMoveset
            && !SireneFullMoveset
            && !AutoPictoMastery
            && !InstantPictoMastery
            && !LimitPictoPopups
            && !BuffRegularEnemies
            && !ReduceLuminaDrops;

        public bool NeedsStatTables =>
            ExpandedLevels
            || !Near(HpMult, 1) || !Near(AttackMult, 1) || !Near(SpeedMult, 1)
            || !Near(CritMult, 1) || !Near(DefenseMult, 1);

        public bool NeedsAp => MaxAP != VanillaMaxAp;
        public bool NeedsTurns => MaxConsecutiveTurns != VanillaConsecutiveTurns;
        public bool NeedsForetell => !Near(MaxForetell, VanillaMaxForetell);

        /// <summary>Crit damage is a bytecode edit to BP_BattleDamages (the addend rewrites the
        /// baked base constant; the crit-rate scaling wraps it) — never a table change.</summary>
        public bool NeedsCritDamage =>
            CritRateDamageScaling || !Near(CritDamageIncrease, VanillaCritDamageIncrease);
        public bool NeedsShields => MaxShields != VanillaMaxShields;
        public bool NeedsLumina => LuminaPerLevel != VanillaLuminaPerLevel;
        public bool NeedsAttributes => AttributePointsPerLevel != VanillaAttributePointsPerLevel;
        public bool NeedsSkillPoints => SkillPointsPerLevel != VanillaSkillPointsPerLevel;
        public bool NeedsSkillBalancing => NeedsForetell;
        public bool NeedsGradient => MaxGradientCharges != VanillaMaxGradientCharges;
        public bool NeedsBurn => !Near(BurnMultiplier, VanillaBurnMultiplier) || !Near(BurnPlayerScale, VanillaBurnPlayerScale) || BurnDuration != VanillaBurnDuration || !Near(BurnTailOff, VanillaBurnTailOff);
        public bool NeedsDefenseWindows => TightenDefenseWindows
            && (!Near(DodgeWindow, VanillaDodgeWindow) || !Near(ParryWindow, VanillaParryWindow));
        public bool NeedsTutorialDisable => DisableTutorials;
        public bool NeedsAutoPictoMastery => AutoPictoMastery;
        public bool NeedsInstantPictoMastery => InstantPictoMastery;
        public bool NeedsPictoPopupCap => LimitPictoPopups;
        public bool NeedsAttributeCap => AttributeCap > VanillaAttributeCap;
        public Newtonsoft.Json.Linq.JObject ToJson() => new()
        {
            ["LevelCap"] = LevelCap,
            ["ExpandedLevels"] = ExpandedLevels,
            ["ExtraXpPerLevel"] = ExtraXpPerLevel,
            ["AttributePointsPerLevel"] = AttributePointsPerLevel,
            ["SkillPointsPerLevel"] = SkillPointsPerLevel,
            ["HpMult"] = HpMult,
            ["AttackMult"] = AttackMult,
            ["SpeedMult"] = SpeedMult,
            ["CritMult"] = CritMult,
            ["DefenseMult"] = DefenseMult,
            ["MaxAP"] = MaxAP,
            ["MaxConsecutiveTurns"] = MaxConsecutiveTurns,
            ["Permadeath"] = Permadeath,
            ["MaxForetell"] = MaxForetell,
            ["CritDamageIncrease"] = CritDamageIncrease,
            ["CritRateDamageScaling"] = CritRateDamageScaling,
            ["MaxShields"] = MaxShields,
            ["LuminaPerLevel"] = LuminaPerLevel,
            ["MaxGradientCharges"] = MaxGradientCharges,
            ["BurnMultiplier"] = BurnMultiplier,
            ["BurnPlayerScale"] = BurnPlayerScale,
            ["BurnDuration"] = BurnDuration,
            ["BurnTailOff"] = BurnTailOff,
            ["DodgeWindow"] = DodgeWindow,
            ["ParryWindow"] = ParryWindow,
            ["TightenDefenseWindows"] = TightenDefenseWindows,
            ["DisableTutorials"] = DisableTutorials,
            ["AttributeCap"] = AttributeCap,
            ["MaskKeeperFullMoveset"] = MaskKeeperFullMoveset,
            ["SireneFullMoveset"] = SireneFullMoveset,
            ["AutoPictoMastery"] = AutoPictoMastery,
            ["InstantPictoMastery"] = InstantPictoMastery,
            ["LimitPictoPopups"] = LimitPictoPopups,
            ["PictoPopupCap"] = PictoPopupCap,
            ["BuffRegularEnemies"] = BuffRegularEnemies,
            ["RegularEnemyHpBonus"] = new Newtonsoft.Json.Linq.JArray(RegularEnemyHpBonus),
            ["RegularEnemySpeedBonus"] = new Newtonsoft.Json.Linq.JArray(RegularEnemySpeedBonus),
            ["RegularEnemyAttackBonus"] = new Newtonsoft.Json.Linq.JArray(RegularEnemyAttackBonus),
            ["ReduceLuminaDrops"] = ReduceLuminaDrops,
            ["LuminaDropMultiplier"] = LuminaDropMultiplier,
        };

        public void FromJson(Newtonsoft.Json.Linq.JObject o)
        {
            if (o.Count == 0) return;
            AttributeCap = Math.Clamp(
                o.Value<int?>("AttributeCap") ?? VanillaAttributeCap,
                VanillaAttributeCap, MaxAttributeCap);
            if (o["LevelCap"] != null)
                LevelCap = Math.Clamp(o.Value<int?>("LevelCap") ?? DefaultLevelCap, 1, ExpandedLevelCap);
            else if (o.Value<bool?>("ExpandedLevels") == true)
                LevelCap = DefaultLevelCap;
            else if (o["ExpandedLevels"] != null && o.Value<bool?>("ExpandedLevels") == false)
                LevelCap = VanillaLevelCap;
            ExtraXpPerLevel = o.Value<int?>("ExtraXpPerLevel") ?? ExtraLevelXp;
            AttributePointsPerLevel = o.Value<int?>("AttributePointsPerLevel") ?? 2;
            SkillPointsPerLevel = o.Value<int?>("SkillPointsPerLevel") ?? VanillaSkillPointsPerLevel;
            HpMult = o.Value<double?>("HpMult") ?? 1;
            AttackMult = o.Value<double?>("AttackMult") ?? 1;
            SpeedMult = o.Value<double?>("SpeedMult") ?? 1;
            CritMult = o.Value<double?>("CritMult") ?? 1;
            DefenseMult = o.Value<double?>("DefenseMult") ?? 1;
            MaxAP = o.Value<int?>("MaxAP") ?? VanillaMaxAp;
            MaxConsecutiveTurns = o.Value<int?>("MaxConsecutiveTurns") ?? VanillaConsecutiveTurns;
            Permadeath = o.Value<bool?>("Permadeath") ?? false;
            MaxForetell = o.Value<float?>("MaxForetell") ?? VanillaMaxForetell;
            CritDamageIncrease = o.Value<float?>("CritDamageIncrease") ?? VanillaCritDamageIncrease;
            CritRateDamageScaling = o.Value<bool?>("CritRateDamageScaling") ?? true;
            MaxShields = o.Value<int?>("MaxShields") ?? VanillaMaxShields;
            LuminaPerLevel = o.Value<int?>("LuminaPerLevel") ?? 2;
            MaxGradientCharges = Math.Clamp(o.Value<int?>("MaxGradientCharges") ?? DefaultMaxGradientCharges, 1, 99);
            BurnMultiplier = o.Value<double?>("BurnMultiplier") ?? VanillaBurnMultiplier;
            BurnPlayerScale = o.Value<double?>("BurnPlayerScale") ?? DefaultBurnPlayerScale;
            BurnDuration = Math.Clamp(o.Value<int?>("BurnDuration") ?? VanillaBurnDuration, 1, 20);
            BurnTailOff = o.Value<double?>("BurnTailOff") ?? VanillaBurnTailOff;
            BurnTailOff = Math.Clamp(BurnTailOff, 0, 10);
            DodgeWindow = o.Value<double?>("DodgeWindow") ?? VanillaDodgeWindow;
            ParryWindow = o.Value<double?>("ParryWindow") ?? VanillaParryWindow;
            // Migration: the pre-toggle configs only had the two numbers. Infer
            // the switch from them so existing projects keep their tight pack.
            TightenDefenseWindows = o.Value<bool?>("TightenDefenseWindows")
                ?? (!Near(DodgeWindow, VanillaDodgeWindow) || !Near(ParryWindow, VanillaParryWindow));
            DisableTutorials = o.Value<bool?>("DisableTutorials") ?? true;
            MaskKeeperFullMoveset = o.Value<bool?>("MaskKeeperFullMoveset") ?? true;
            SireneFullMoveset = o.Value<bool?>("SireneFullMoveset") ?? true;
            AutoPictoMastery = o.Value<bool?>("AutoPictoMastery") ?? false;
            InstantPictoMastery = o.Value<bool?>("InstantPictoMastery") ?? false;
            PictoPopupCap = Math.Clamp(
                o.Value<int?>("PictoPopupCap") ?? DefaultPictoPopupCap,
                MinPictoPopupCap, PictoPopupCapKit.MaxCap);
            // Older configs had no toggle; 0 there meant vanilla.
            LimitPictoPopups = o.Value<bool?>("LimitPictoPopups") ?? (PictoPopupCap > 0);
            BuffRegularEnemies = o.Value<bool?>("BuffRegularEnemies") ?? false;
            RegularEnemyHpBonus = BonusArray(o["RegularEnemyHpBonus"], RegularEnemyBuffKit.DefaultHpBonus);
            RegularEnemySpeedBonus = BonusArray(o["RegularEnemySpeedBonus"], RegularEnemyBuffKit.DefaultSpeedBonus);
            RegularEnemyAttackBonus = BonusArray(o["RegularEnemyAttackBonus"], RegularEnemyBuffKit.DefaultAttackBonus);
            ReduceLuminaDrops = o.Value<bool?>("ReduceLuminaDrops") ?? false;
            // Configs written before the multiplier was adjustable only had the
            // switch; keep their 50% reduction instead of silently using 1.0.
            var defaultMultiplier = ReduceLuminaDrops ? 0.5 : Loot.LuminaDropKit.DefaultMultiplier;
            LuminaDropMultiplier = Math.Clamp(
                o.Value<double?>("LuminaDropMultiplier") ?? defaultMultiplier, 0.05, 5.0);
            // Legacy keys from removed experiments – ignored, allow migration.
            o.Remove("ParryLock");
            o.Remove("OverchargeMax");
        }


        private static double[] BonusArray(Newtonsoft.Json.Linq.JToken? token, double[] fallback)
        {
            if (token is not Newtonsoft.Json.Linq.JArray array || array.Count != 5)
                return (double[])fallback.Clone();
            return array.Select(v => (double?)v ?? 0).ToArray();
        }

        private static bool Near(double a, double b) => Math.Abs(a - b) < 0.0001;
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 0.0001;

    public static bool HasLeftoverAssets(string modDir) =>
        FindAsset(modDir, "DT_jRPG_Levels.uasset") != null
        || FindAsset(modDir, "AC_jRPG_CharactersManager.uasset") != null
        || FindAsset(modDir, "BP_CharacterData.uasset") != null
        || FindAsset(modDir, "DT_CharactersLevelScaling.uasset") != null
        || FindAsset(modDir, "DT_Balancing_BattleDifficultySettings.uasset") != null
        || FindAsset(modDir, "BP_TutorialSystemComponent.uasset") != null
        || FindAsset(modDir, AutoPictoMasteryKit.ControllerAsset + ".uasset") != null
        || FindAsset(modDir, PictoInstantMasteryKit.AssetStem + ".uasset") != null
        || FindAsset(modDir, PictoPopupCapKit.AssetStem + ".uasset") != null
        || FindAsset(modDir, "DT_Balancing_MainCharacterSkills.uasset") != null
        || FindAsset(modDir, "FL_jRPG_CustomFunctionLibrary.uasset") != null
        || FindAsset(modDir, "BP_CharacterSkillGraphSystem.uasset") != null
        || FindAsset(modDir, "BP_GradientGaugeSystem.uasset") != null
        || FindAsset(modDir, "WBP_GradientChargeGauge.uasset") != null
        || FindAsset(modDir, "BP_BattleBuff_Burn.uasset") != null
        || FindAsset(modDir, "BP_BattleBuff_BurnAggregator.uasset") != null
        || FindAsset(modDir, "BP_BattleDamages.uasset") != null
        || FindAsset(modDir, "WBP_Raw_Attributes_Display.uasset") != null
        || FindAsset(modDir, "AC_jRPG_CharacterBattleStats.uasset") != null
        || FindAsset(modDir, "BP_BattleBuff_RemoveItselfAtAnotherCharacterTurn.uasset") != null
        || FindAsset(modDir, "DT_AttributeStatScaling_Agility.uasset") != null
        || FindAsset(modDir, "DT_AttributeStatScaling_Defense.uasset") != null
        || FindAsset(modDir, "DT_AttributeStatScaling_Luck.uasset") != null
        || FindAsset(modDir, "DT_AttributeStatScaling_Vitality.uasset") != null
        || FindAsset(modDir, "DT_AttributeStatScaling_Strength.uasset") != null;

    private static readonly HashSet<string> CopiedThisApply = new(StringComparer.Ordinal);

    public static int Apply(string modDir, Settings s)
    {
        CopiedThisApply.Clear();
        int n = 0;
        if (s.IsVanilla)
        {
            if (FindAsset(modDir, "DT_jRPG_Items_Composite.uasset") != null)
                UnhideReviveItems(modDir);
            RemoveOwnedAssets(modDir);
            TutorialDisableKit.RestoreEncounters(modDir);
            DeleteStaleWindowBase(modDir);
            return 0;
        }

        if (!s.ExpandedLevels)
            DeletePair(modDir, "FL_jRPG_CustomFunctionLibrary.uasset");

        if (s.ExpandedLevels || s.NeedsStatTables)
        {
            n += ApplyLevelCap(modDir, s);
            n += ApplyXpTable(modDir, s);
            n += ApplyLevelStatTable(modDir, s);
            n += ApplyStatCurves(modDir, s);
        }

        // Attribute tables are also needed when only the attribute point cap
        // changed (no level-cap expansion), so they gate separately.
        if (s.ExpandedLevels || s.NeedsStatTables || s.NeedsAttributeCap)
            n += ApplyAttributeTables(modDir, s);

        // The wheel clamps assignment at 99 in bytecode, so the tables alone are
        // not enough when the cap is raised.
        if (s.NeedsAttributeCap)
            n += ApplyAttributeCapUi(modDir, s);
        else
            DeletePair(modDir, "WBP_Raw_Attributes_Display.uasset");

        if (s.NeedsAp)
            n += ApplyMaxAp(modDir, s);

        if (s.NeedsLumina || s.NeedsAttributes)
            n += ApplyLuminaAndAttributes(modDir, s);
        else if (!s.NeedsAp)
            DeletePair(modDir, "BP_CharacterData.uasset");

        if (s.NeedsSkillPoints)
            n += ApplySkillPointsPerLevel(modDir, s);
        else
            DeletePair(modDir, "BP_CharacterSkillGraphSystem.uasset");

        if (s.NeedsTurns)
            n += ApplyConsecutiveTurns(modDir, s);

        // NOTE: the Charge-mechanic + Gustave charge-BP deletions that used to live
        // here were removed — the Gustave move kit (GustaveSkillsKit) rebalances and
        // stages Gustave's charge moves (Overcharge / Strike Storm / Lumière Assault)
        // which depend on BP_UniqueMechanic_Charge_Component and the UnleashCharge /
        // PerfectBreak skill scripts. Deleting them breaks the charge gauge.

        if (s.NeedsSkillBalancing)
            n += ApplySkillBalancing(modDir, s);

        if (s.NeedsShields)
            n += ApplyMaxShields(modDir, s);

        if (s.NeedsGradient)
            n += ApplyMaxGradientCharges(modDir, s);
        else
        {
            DeletePair(modDir, "BP_GradientGaugeSystem.uasset");
            DeletePair(modDir, "WBP_GradientChargeGauge.uasset");
        }

        if (s.NeedsBurn)
            n += ApplyBurnTweaks(modDir, s);
        else
        {
            DeletePair(modDir, "BP_BattleBuff_Burn.uasset");
            DeletePair(modDir, "BP_BattleBuff_BurnAggregator.uasset");
        }

        if (s.NeedsDefenseWindows)
            n += ApplyDefenseWindows(modDir, s);
        else
            DeletePair(modDir, "DT_Balancing_BattleDifficultySettings.uasset");

        // Tutorials are force-disabled rather than tuned: the tutorial system
        // returns false and the narrative encounters point at their plain
        // no-tutorial enemy variants.
        if (s.NeedsTutorialDisable)
            n += TutorialDisableKit.Apply(modDir);
        else
        {
            DeletePair(modDir, TutorialDisableKit.TutorialSystemAsset + ".uasset");
            TutorialDisableKit.RestoreEncounters(modDir);
        }

        // Picto mastery uses the game's own LearnAnyLuminaAfterOneBattle CVar,
        // registered from BP_jRPG_Controller_World's construction script.
        if (s.NeedsAutoPictoMastery)
            n += AutoPictoMasteryKit.Apply(modDir);
        else
            DeletePair(modDir, AutoPictoMasteryKit.ControllerAsset + ".uasset");

        // Instant mastery is the acquisition hook. It is independent of the
        // one-battle CVar above and does not need it.
        if (s.NeedsInstantPictoMastery)
            n += PictoInstantMasteryKit.Apply(modDir);
        else
            DeletePair(modDir, PictoInstantMasteryKit.AssetStem + ".uasset");

        // Battle HUD proc cards. 0 is vanilla (widget not staged); the cap is
        // enforced in WBP_PassivePopupContainer.AddPassiveEffectNotification.
        n += PictoPopupCapKit.Apply(modDir, s.NeedsPictoPopupCap ? s.PictoPopupCap : (int?)null);

        // Crit damage — a kismet edit to BP_BattleDamages (base addend + optional crit-rate
        // scaling), not a table change.
        if (s.NeedsCritDamage)
        {
            n += CritDamageKit.Apply(modDir, s.CritDamageIncrease, s.CritRateDamageScaling);
            n += ApplyCritStatText(modDir, s);
        }
        else
            DeletePair(modDir, "BP_BattleDamages.uasset");
        // Remove a battle-base staged by the old CDO window patch (retired
        // 2026-09-02: CDO immune durations are overwritten by the difficulty
        // table at spawn, and the ParryLockDuration shrink caused edge-of-window
        // parry crashes). The windows now live in the table above, so a base in
        // the pack can only be this stale artifact — delete it so it cannot
        // ship its bad ParryLockDuration.
        DeleteStaleWindowBase(modDir);

        if (s.Permadeath)
            n += ApplyPermadeath(modDir, true);
        else
        {
            // AC_jRPG_CharacterBattleStats also carries the consecutive-turns
            // patch (and shields), so only drop it when nothing needs it.
            if (!s.NeedsShields && !s.NeedsTurns)
                DeletePair(modDir, "AC_jRPG_CharacterBattleStats.uasset");
            if (FindAsset(modDir, "DT_jRPG_Items_Composite.uasset") != null)
                UnhideReviveItems(modDir);
        }

        return n;
    }


    private static int ApplySkillBalancing(string modDir, Settings s)
    {
        var path = CopyIntoMod("DT_Balancing_MainCharacterSkills", modDir);
        var ed = new DataTableEditor(path);
        var edits = new Newtonsoft.Json.Linq.JObject();
        if (s.NeedsForetell)
            edits["Generic_MaxForetell"] = new Newtonsoft.Json.Linq.JObject { ["FloatValue"] = s.MaxForetell };
        int n = 0;
        if (edits.Count > 0) n = ed.Apply(edits.ToString());
        ed.Save(path);
        return n;
    }

    private static int ApplyMaxShields(string modDir, Settings s)
    {
        var path = CopyIntoMod("AC_jRPG_CharacterBattleStats", modDir);
        return PatchIntConstIn(path, VanillaMaxShields, s.MaxShields, "SetCurrentShieldPoints");
    }

    private static int ApplyMaxGradientCharges(string modDir, Settings s)
    {
        var path = CopyIntoMod("BP_GradientGaugeSystem", modDir);
        int n = 0;
        if (SetCdoInt(path, "MaxCharges", s.MaxGradientCharges)) n++;
        // Fallback: patch any hardcoded IntConst 3 in the bytecode (defensive).
        else n += PatchIntConstIn(path, VanillaMaxGradientCharges, s.MaxGradientCharges);

        // The battle HUD widget carries its own copy of the maximum and hard-codes
        // the per-charge fill/colour/scale layout for three charges; rebuild it.
        var widget = CopyIntoMod("WBP_GradientChargeGauge", modDir);
        n += GradientGaugeUiKit.Apply(widget, s.MaxGradientCharges);
        return n;
    }

    private static int ApplyBurnTweaks(string modDir, Settings s)
    {
        int n = 0;
        // Vanilla: Burn.BurnMultiplier=1.0, Aggregator.BurnDamageMultiplier=0.5, playerScale 0.2 inside ComputeBurnDamageTotal,
        // TurnDuration=3 on both. The two DoubleConst 2.0 in Kismet implement b^2/(k·Def+Atk) tail-off.
        const double vanillaAggMult = 0.5;
        if (!Near(s.BurnMultiplier, VanillaBurnMultiplier))
        {
            // Global burn damage multiplier – scale the aggregator's BurnDamageMultiplier linearly.
            // Keeps per-stack BurnMultiplier at 1.0 so that BurnMultiplier=2.0 means 2× damage (not 4×).
            var aggPath = CopyIntoMod("BP_BattleBuff_BurnAggregator", modDir);
            double newAgg = vanillaAggMult * s.BurnMultiplier;
            if (SetCdoDouble(aggPath, "BurnDamageMultiplier", newAgg)) n++;
        }
        // Player-team scalar (DoubleConst 0.2 vs 1.0 branch in Compute Burn Damage Total)
        if (!Near(s.BurnPlayerScale, VanillaBurnPlayerScale))
        {
            var aggPath = CopyIntoMod("BP_BattleBuff_BurnAggregator", modDir);
            n += PatchDoubleConstIn(aggPath, VanillaBurnPlayerScale, s.BurnPlayerScale, "Compute Burn Damage Total");
        }
        // Duration on both CDOs
        if (s.BurnDuration != VanillaBurnDuration)
        {
            var burnPath = CopyIntoMod("BP_BattleBuff_Burn", modDir);
            if (SetCdoInt(burnPath, "TurnDuration", s.BurnDuration)) n++;
            var aggPath = CopyIntoMod("BP_BattleBuff_BurnAggregator", modDir);
            if (SetCdoInt(aggPath, "TurnDuration", s.BurnDuration)) n++;
        }
        // Tail-off factor k in b^2/(k·Def+Atk) – two DoubleConst 2.0 in ComputeBurnDamageTotal
        if (!Near(s.BurnTailOff, VanillaBurnTailOff))
        {
            var aggPath = CopyIntoMod("BP_BattleBuff_BurnAggregator", modDir);
            n += PatchDoubleConstIn(aggPath, VanillaBurnTailOff, s.BurnTailOff, "Compute Burn Damage Total");
        }
        return n;
    }

    /// <summary>
    /// Base dodge/parry windows via DT_Balancing_BattleDifficultySettings.
    /// The game overwrites each battle character's immune durations from this
    /// table at spawn (BP_jRPG_Character_Battle_Base.ApplyDifficultySettings:
    /// rows ImmuneDuration_{Dodge,Parry}_{Easy,Normal,Hard} keyed by current
    /// difficulty) — CDO defaults are NOT the operative values in battle.
    /// Window formula at runtime: base × (1 + charMod) × (1 + globalMod); the
    /// helper pictos add +22% each, so base = 0.75 × vanilla and all three
    /// helpers land at ≈1.25 × vanilla (0.75 × 1.667 additive).
    /// Story/Easy keeps its more lenient vanilla rows; the tuned rows are
    /// written to Normal (Expedition) and Hard (Expert), which share timings
    /// in vanilla. ParryLockDuration is deliberately NOT touched — vanilla 1.2
    /// is what gates re-parry; shrinking it while the difficulty table keeps
    /// the full 0.15 s window let a second parry re-enter the native state
    /// machine near the window edge (crash).
    /// </summary>
    private static int ApplyDefenseWindows(string modDir, Settings s)
    {
        var path = CopyIntoMod("DT_Balancing_BattleDifficultySettings", modDir);
        var ed = new DataTableEditor(path);
        var edits = new Newtonsoft.Json.Linq.JObject
        {
            ["ImmuneDuration_Dodge_Normal"] = new Newtonsoft.Json.Linq.JObject { ["FloatValue"] = s.DodgeWindow },
            ["ImmuneDuration_Dodge_Hard"] = new Newtonsoft.Json.Linq.JObject { ["FloatValue"] = s.DodgeWindow },
            ["ImmuneDuration_Parry_Normal"] = new Newtonsoft.Json.Linq.JObject { ["FloatValue"] = s.ParryWindow },
            ["ImmuneDuration_Parry_Hard"] = new Newtonsoft.Json.Linq.JObject { ["FloatValue"] = s.ParryWindow },
        };
        int n = ed.Apply(edits.ToString());
        ed.Save(path);
        return n;
    }

    private static bool SetCdoDouble(string uassetPath, string field, double value)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map) : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
        var cdo = asset.Exports.OfType<NormalExport>().FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
        if (cdo is null) return false;
        foreach (var p in cdo.Data)
        {
            if (DataTableEditor.CleanName(p.Name) != field) continue;
            if (p is DoublePropertyData d)
            {
                if (Math.Abs(d.Value - value) < 0.0001) return false;
                d.Value = value;
                WriteAsset(asset, uassetPath);
                return true;
            }
            if (p is FloatPropertyData f)
            {
                if (Math.Abs(f.Value - value) < 0.0001) return false;
                f.Value = (float)value;
                WriteAsset(asset, uassetPath);
                return true;
            }
        }
        return false;
    }

    private static int PatchDoubleConstIn(string uasset, double from, double to, string? fnName = null)
    {
        if (Near(from, to)) return 0;
        var map = DataTableEditor.Mappings;
        var asset = map != null ? new UAsset(uasset, EngineVersion.VER_UE5_4, map) : new UAsset(uasset, EngineVersion.VER_UE5_4);
        int n = 0;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            if (fnName != null && fn.ObjectName.ToString() != fnName) continue;
            WalkKismet(fn.ScriptBytecode, e =>
            {
                if (e is EX_DoubleConst dc && Math.Abs(dc.Value - from) < 0.0001)
                {
                    dc.Value = to;
                    n++;
                }
                else if (e is EX_FloatConst fc && Math.Abs(fc.Value - from) < 0.0001)
                {
                    fc.Value = (float)to;
                    n++;
                }
            });
        }
        if (n > 0) WriteAsset(asset, uasset);
        return n;
    }

    private static int PatchIntConstIn(string uasset, int from, int to, string? fnName = null)
    {
        if (from == to) return 0;
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(uasset, EngineVersion.VER_UE5_4, map)
            : new UAsset(uasset, EngineVersion.VER_UE5_4);
        int n = 0;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            if (fnName != null && fn.ObjectName.ToString() != fnName) continue;
            WalkKismet(fn.ScriptBytecode, e =>
            {
                if (e is EX_IntConst c && c.Value == from)
                {
                    c.Value = to;
                    n++;
                }
                else if (e is EX_ByteConst b && b.Value == from && to is >= 0 and <= 255)
                {
                    b.Value = (byte)to;
                    n++;
                }
            });
        }
        if (n > 0) WriteAsset(asset, uasset);
        return n;
    }

    private static int ApplyLevelCap(string modDir, Settings s)
    {
        int n = 0;
        var path = CopyIntoMod("AC_jRPG_CharactersManager", modDir);
        if (SetCdoInt(path, "CharacterMaxLevel", s.LevelCap)) n++;
        if (s.ExpandedLevels)
        {
            var fl = CopyIntoMod("FL_jRPG_CustomFunctionLibrary", modDir);
            n += PatchIntConstIn(fl, VanillaLevelCap, s.LevelCap, "GetMaximumLevel");
        }
        return n;
    }

    /// <summary>
    /// Applies the tweaks that live in the assets the Bigger Party Mod also
    /// ships — the level cap (CharactersManager + FL) — onto a directory already
    /// holding that mod's extracted copies. <see cref="CompatibilityKit"/> uses
    /// this to patch the third-party mod in place instead of shadowing it with a
    /// vanilla copy.
    ///
    /// Consecutive turns are NOT here: the cap lives in our own
    /// <c>AC_jRPG_CharacterBattleStats</c> (not one of the mod's assets), so
    /// <see cref="Apply"/> already patches it for both modes.
    /// </summary>
    public static int ApplyCompatEdits(string dir, Settings s)
    {
        int n = 0;
        var cm = FindAsset(dir, "AC_jRPG_CharactersManager.uasset");
        if (cm is not null && SetCdoInt(cm, "CharacterMaxLevel", s.LevelCap)) n++;
        if (s.ExpandedLevels)
        {
            var fl = FindAsset(dir, "FL_jRPG_CustomFunctionLibrary.uasset");
            if (fl is not null) n += PatchIntConstIn(fl, VanillaLevelCap, s.LevelCap, "GetMaximumLevel");
        }
        return n;
    }

    private static int ApplyLuminaAndAttributes(string modDir, Settings s)
    {
        var path = CopyIntoMod("BP_CharacterData", modDir);
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);

        FPackageIndex? mulNode = null;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ObjectName.ToString() != "SetLevel" || fn.ScriptBytecode is null) continue;
            WalkKismet(fn.ScriptBytecode, e =>
            {
                if (mulNode != null) return;
                if (e is EX_FinalFunction ff && ff.Parameters is { Length: 2 }
                    && ff.Parameters[1] is EX_IntConst c && c.Value != 0)
                    mulNode = ff.StackNode;
            });
        }

        int n = 0;
        if (s.NeedsLumina)
        {
            if (mulNode is null)
                throw new InvalidDataException("BP_CharacterData.SetLevel has no Multiply_IntInt to clone.");
            foreach (var fn in asset.Exports.OfType<FunctionExport>())
            {
                var name = fn.ObjectName.ToString();
                if (fn.ScriptBytecode is null) continue;
                if (name == "GetMaxLuminaPoints")
                    n += WrapLuminaAdd(fn.ScriptBytecode, mulNode, s.LuminaPerLevel);
                else if (name == "GetMaxLuminaPointsDetails")
                    n += WrapLuminaLet(fn.ScriptBytecode, mulNode, s.LuminaPerLevel);
            }
        }

        if (s.NeedsAttributes)
        {
            foreach (var fn in asset.Exports.OfType<FunctionExport>())
            {
                if (fn.ObjectName.ToString() != "SetLevel" || fn.ScriptBytecode is null) continue;
                WalkKismet(fn.ScriptBytecode, e =>
                {
                    if (e is EX_IntConst c && c.Value == VanillaAttributePointsPerLevel)
                    {
                        c.Value = s.AttributePointsPerLevel;
                        n++;
                    }
                });
            }
        }

        if (n > 0) WriteAsset(asset, path);
        return n;
    }

    private static int ApplySkillPointsPerLevel(string modDir, Settings s)
    {
        if (s.SkillPointsPerLevel == VanillaSkillPointsPerLevel) return 0;
        var path = CopyIntoMod("BP_CharacterSkillGraphSystem", modDir);
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);

        FPackageIndex? addNode = null;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ObjectName.ToString() != "GetTotalSkillPoints" || fn.ScriptBytecode is null) continue;
            WalkKismet(fn.ScriptBytecode, e =>
            {
                if (e is EX_FinalFunction ff && ff.Parameters is { Length: 2 })
                    addNode = ff.StackNode;
            });
        }
        if (addNode is null)
            throw new InvalidDataException("BP_CharacterSkillGraphSystem has no Add_IntInt to clone.");

        int n = 0;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ObjectName.ToString() != "OnOwnerLevelSet" || fn.ScriptBytecode is null) continue;
            WalkKismet(fn.ScriptBytecode, e =>
            {
                if (e is not EX_Let let) return;
                if (let.Variable is not EX_InstanceVariable iv) return;
                if (FieldName(iv.Variable) != "SkillPointsFromLeveling") return;
                if (let.Expression is EX_CallMath) return;
                let.Expression = RepeatAdd(addNode, let.Expression, s.SkillPointsPerLevel);
                n++;
            });
        }
        if (n > 0) WriteAsset(asset, path);
        return n;
    }

    private static KismetExpression RepeatAdd(FPackageIndex addNode, KismetExpression inner, int times)
    {
        KismetExpression acc = inner;
        for (int i = 1; i < times; i++)
        {
            acc = new EX_CallMath
            {
                StackNode = addNode,
                Parameters = new[] { acc, inner },
            };
        }
        return acc;
    }

    private static int WrapLuminaAdd(KismetExpression[] bytecode, FPackageIndex mulNode, int factor)
    {
        int n = 0;
        WalkKismet(bytecode, e =>
        {
            if (e is not EX_FinalFunction ff || ff.Parameters is not { Length: 2 }) return;
            if (ff.Parameters[0] is not EX_InstanceVariable iv
                || FieldName(iv.Variable) != "LuminaMaxPointsFromLevel")
                return;
            if (ff.Parameters[1] is not EX_InstanceVariable) return; // Add(level, consumables), not Multiply
            ff.Parameters[0] = Mul(mulNode, ff.Parameters[0], factor);
            n++;
        });
        return n;
    }

    private static int WrapLuminaLet(KismetExpression[] bytecode, FPackageIndex mulNode, int factor)
    {
        int n = 0;
        WalkKismet(bytecode, e =>
        {
            if (e is not EX_Let let) return;
            if (let.Expression is EX_CallMath) return;
            if (let.Expression is EX_InstanceVariable iv
                && FieldName(iv.Variable) == "LuminaMaxPointsFromLevel")
            {
                let.Expression = Mul(mulNode, let.Expression, factor);
                n++;
            }
        });
        return n;
    }

    private static EX_CallMath Mul(FPackageIndex mulNode, KismetExpression inner, int factor) =>
        new()
        {
            StackNode = mulNode,
            Parameters = new KismetExpression[] { inner, new EX_IntConst { Value = factor } },
        };

    private static string FieldName(KismetPropertyPointer? p)
    {
        var path = p?.New?.Path;
        if (path is { Length: > 0 })
            return path[0].ToString() ?? "";
        return "";
    }

    private static int ApplyXpTable(string modDir, Settings s)
    {
        var path = CopyIntoMod("DT_jRPG_Levels", modDir);
        var ed = new DataTableEditor(path);
        int n = 0;
        if (s.ExpandedLevels)
        {
            if (!ed.TryGetRow("99", out var src))
                throw new InvalidDataException("DT_jRPG_Levels is missing row 99.");
            for (int lvl = VanillaLevelCap + 1; lvl <= s.LevelCap; lvl++)
            {
                var name = lvl.ToString();
                if (ed.HasRow(name)) continue;
                var clone = (StructPropertyData)src.Clone();
                clone.Name = new FName(ed.Asset, name);
                SetInt(clone, "ExperienceNeededToReachThisLevel", s.ExtraXpPerLevel);
                ed.Table.Table.Data.Add(clone);
                n++;
            }
        }
        ed.Save(path);
        return n;
    }

    private static int ApplyLevelStatTable(string modDir, Settings s)
    {
        var path = CopyIntoMod("DT_CharactersLevelScaling", modDir);
        var ed = new DataTableEditor(path);
        int n = ScaleMapRows(ed, "Level_", VanillaLevelCap, s, includeLevelField: true);

        if (s.ExpandedLevels)
        {
            if (!ed.TryGetRow("Level_99", out var src99) || !ed.TryGetRow("Level_90", out var src90))
                throw new InvalidDataException("DT_CharactersLevelScaling is missing Level_90/99.");
            var v99 = ReadStatMap(src99);
            var v90 = ReadStatMap(src90);
            for (int lvl = VanillaLevelCap + 1; lvl <= s.LevelCap; lvl++)
            {
                var name = "Level_" + lvl;
                if (ed.HasRow(name)) continue;
                var clone = (StructPropertyData)src99.Clone();
                clone.Name = new FName(ed.Asset, name);
                SetInt(clone, "Level", lvl);
                foreach (var key in v99.Keys.ToList())
                {
                    var slope = (v99[key] - v90.GetValueOrDefault(key)) / 9.0;
                    SetStatMap(clone, key, v99[key] + slope * (lvl - VanillaLevelCap));
                }
                ed.Table.Table.Data.Add(clone);
                n++;
            }
        }

        ed.Save(path);
        return n;
    }

    private static int ApplyStatCurves(string modDir, Settings s)
    {
        int n = 0;
        n += ExtendCurve(modDir, "Curve_StatsPerLevel_HP", s.HpMult, s);
        n += ExtendCurve(modDir, "Curve_StatsPerLevel_PAttack", s.AttackMult, s);
        n += ExtendCurve(modDir, "Curve_StatsPerLevel_Speed", s.SpeedMult, s);
        n += ExtendCurve(modDir, "Curve_StatsPerLevel_CritRate", s.CritMult, s);
        n += ExtendCurve(modDir, "Curve_StatsPerLevel_Defense", s.DefenseMult, s);
        return n;
    }

    private static int ExtendCurve(string modDir, string asset, double mult, Settings s)
    {
        var path = CopyIntoMod(asset, modDir);
        var map = DataTableEditor.Mappings;
        var u = map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
        var exp = u.Exports.OfType<NormalExport>().FirstOrDefault()
            ?? throw new InvalidDataException(asset + " has no curve export.");
        var curve = exp.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "FloatCurve") as StructPropertyData
            ?? throw new InvalidDataException(asset + " has no FloatCurve.");
        var keys = curve.Value.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "Keys") as ArrayPropertyData
            ?? throw new InvalidDataException(asset + " has no Keys.");

        int n = 0;
        static RichCurveKeyPropertyData? KeyOf(PropertyData elem) =>
            elem as RichCurveKeyPropertyData
            ?? (elem as StructPropertyData)?.Value.OfType<RichCurveKeyPropertyData>().FirstOrDefault();

        foreach (var elem in keys.Value)
        {
            var k = KeyOf(elem);
            if (k is null) continue;
            if (Math.Abs(mult - 1) > 0.0001)
            {
                var v = k.Value;
                v.Value = (float)(v.Value * mult);
                k.Value = v;
                n++;
            }
        }

        if (s.ExpandedLevels && keys.Value.Length >= 2)
        {
            var last = KeyOf(keys.Value[^1]);
            var prev = KeyOf(keys.Value[^2]);
            if (last is not null && prev is not null && last.Value.Time < s.LevelCap - 0.01f)
            {
                var dt = last.Value.Time - prev.Value.Time;
                var slope = dt > 0.001f ? (last.Value.Value - prev.Value.Value) / dt : 0;
                var neu = (PropertyData)keys.Value[^1].Clone();
                var nk = KeyOf(neu)!;
                var kv = last.Value;
                kv.Time = s.LevelCap;
                kv.Value = last.Value.Value + slope * (s.LevelCap - last.Value.Time);
                kv.InterpMode = ERichCurveInterpMode.RCIM_Linear;
                kv.TangentMode = ERichCurveTangentMode.RCTM_Auto;
                nk.Value = kv;
                var list = keys.Value.ToList();
                list.Add(neu);
                keys.Value = list.ToArray();
                n++;
            }
        }

        WriteAsset(u, path);
        return n;
    }

    private static int ApplyAttributeTables(string modDir, Settings s)
    {
        int n = 0;
        foreach (var table in new[]
                 {
                     "DT_AttributeStatScaling_Agility",
                     "DT_AttributeStatScaling_Defense",
                     "DT_AttributeStatScaling_Luck",
                     "DT_AttributeStatScaling_Vitality",
                     "DT_AttributeStatScaling_Strength",
                 })
        {
            var path = CopyIntoMod(table, modDir);
            var ed = new DataTableEditor(path);
            n += ScaleMapRows(ed, "Points_", 99, s, includeLevelField: false);
            if (s.ExpandedLevels || s.NeedsAttributeCap)
                n += ApplyAttributeCurve(ed, s);
            ed.Save(path);
        }
        return n;
    }

    /// <summary>Points where the straight opening segment ends and acceleration begins.</summary>
    public const int AttributeCurveKnee = 80;
    /// <summary>Point value whose scaling is the acceleration target.</summary>
    public const int AttributeCurveTarget = 200;
    /// <summary>Vanilla <c>Points_99</c> scaling is multiplied by this at <see cref="AttributeCurveTarget"/>.</summary>
    public const double AttributeCurveTargetMultiple = 3.0;

    /// <summary>
    /// Rewrites one attribute scaling table's point curve:
    /// <list type="bullet">
    /// <item>1..80 — a straight line from zero landing on the vanilla <c>Points_80</c>
    /// value (vanilla's uneven early gains become a flat per-point rate).</item>
    /// <item>81..200 — accelerates (quadratic, continuous in value and slope at 80)
    /// until <c>Points_200</c> reaches <see cref="AttributeCurveTargetMultiple"/> ×
    /// the vanilla <c>Points_99</c> value.</item>
    /// <item>201.. — continues at the 200 slope so the table stays monotone.</item>
    /// </list>
    /// Rows 0 and 80 keep their vanilla values.
    /// </summary>
    private static int ApplyAttributeCurve(DataTableEditor ed, Settings s)
    {
        if (!ed.TryGetRow("Points_" + AttributeCurveKnee, out var kneeRow)) return 0;
        if (!ed.TryGetRow("Points_99", out var src99)) return 0;
        var atKnee = ReadStatMap(kneeRow);
        var v99 = ReadStatMap(src99);
        if (atKnee.Count == 0 || v99.Count == 0) return 0;

        int max = Math.Max(AttributeCurveTarget, s.AttributeTableCap);
        double span = AttributeCurveTarget - AttributeCurveKnee;
        int changed = 0;

        // The staged table is vanilla (Points_0..Points_99), so materialise the rest.
        for (int p = 0; p <= max; p++)
        {
            var name = "Points_" + p;
            if (ed.HasRow(name)) continue;
            var clone = (StructPropertyData)src99.Clone();
            clone.Name = new FName(ed.Asset, name);
            ed.Table.Table.Data.Add(clone);
        }

        foreach (var key in v99.Keys.ToList())
        {
            double knee = atKnee.GetValueOrDefault(key);
            double slope = knee / AttributeCurveKnee;
            double target = v99.GetValueOrDefault(key) * AttributeCurveTargetMultiple;
            // Quadratic term so the curve lands exactly on the target at 200.
            double accel = (target - knee - slope * span) / (span * span);
            double endSlope = slope + 2 * accel * span;

            for (int p = 0; p <= max; p++)
            {
                var name = "Points_" + p;
                if (!ed.TryGetRow(name, out var row)) continue;
                double v = p <= AttributeCurveKnee
                    ? slope * p
                    : p <= AttributeCurveTarget
                        ? knee + slope * (p - AttributeCurveKnee) + accel * (p - AttributeCurveKnee) * (p - AttributeCurveKnee)
                        : target + endSlope * (p - AttributeCurveTarget);
                SetStatMap(row, key, v);
                changed++;
            }
        }
        return changed;
    }

    /// <summary>
    /// Attribute-assignment UI clamp. WBP_Raw_Attributes_Display.SetAllowedMaxValue
    /// and SetAllowedMinValue both do Clamp(arg, 0, 99) in bytecode, so extending the
    /// scaling tables alone is not enough — the attributes wheel would still refuse to
    /// assign past 99. Patch those two constants up to the configured cap.
    /// </summary>
    private static int ApplyAttributeCapUi(string modDir, Settings s)
    {
        var path = CopyIntoMod("WBP_Raw_Attributes_Display", modDir);
        int n = PatchIntConstIn(path, VanillaAttributeCap, s.AttributeCap, "SetAllowedMaxValue");
        n += PatchIntConstIn(path, VanillaAttributeCap, s.AttributeCap, "SetAllowedMinValue");
        return n;
    }

    private static int ApplyMaxAp(string modDir, Settings s)
    {
        int n = 0;
        var data = CopyIntoMod("BP_CharacterData", modDir);
        if (SetCdoInt(data, "MaxActionPoints", s.MaxAP)) n++;

        var defs = CopyIntoMod("DT_jRPG_CharacterDefinitions", modDir);
        var ed = new DataTableEditor(defs);
        var json = new Newtonsoft.Json.Linq.JObject();
        foreach (var name in ed.RowNames)
        {
            json[name] = new Newtonsoft.Json.Linq.JObject
            {
                ["CharacterStats." + StatAp] = s.MaxAP,
            };
        }
        n += ed.Apply(json.ToString());
        ed.Save(defs);
        return n;
    }

    /// <summary>Rewrites the Attribute-screen "Critical Rate" description so it
    /// matches the real crit formula (<see cref="CritDamageKit"/>). That text is a
    /// static ST_UI_GameMenu entry the bytecode edit does not touch, so without
    /// this it keeps claiming a flat "50% more damage".</summary>
    private static int ApplyCritStatText(string modDir, Settings s)
    {
        var st = FindAsset(modDir, "ST_UI_GameMenu.uasset")
            ?? CopyIntoMod("ST_UI_GameMenu", modDir);
        DataTableEditor.AddStringTableEntries(st, new Dictionary<string, string>
        {
            ["ST_GM_STAT_CRIT_RATE_DESC"] = DescribeCritRate(s),
        });
        return 1;
    }

    /// <summary>Attribute-screen "Critical Rate" description. With crit-rate
    /// scaling on the crit multiplier is <c>1.5 × max(1, CritRate/100)</c> —
    /// multiplicative — so the text says the damage scales with Critical Rate.
    /// Without scaling it just names the flat addend. Generated from the settings
    /// so it tracks the configuration.</summary>
    public static string DescribeCritRate(Settings s)
    {
        if (s.CritRateDamageScaling)
            return "Critical hits deal more damage, scaling with Critical Rate.";
        int basePct = (int)Math.Round(s.CritDamageIncrease * 100);
        return $"Critical hits deal {basePct}% more damage.";
    }

    private static int ApplyConsecutiveTurns(string modDir, Settings s)
    {
        if (s.MaxConsecutiveTurns <= VanillaConsecutiveTurns) return 0;
        int n = MakeTurnBuffStack(modDir);
        n += AllowChainedPlayAgain(CopyIntoMod("AC_jRPG_CharacterBattleStats", modDir), s.MaxConsecutiveTurns);
        // Per-source dedup (WrapBuffSource) is disabled: editing the source BPs' bytecode
        // crashed the VM in-game. The cap alone bounds bonus turns; a single source can take
        // them, so the Cheater text describes "three times in a row" instead.
        // Edit the table an earlier kit already staged (magnitude picto names/
        // descriptions, created weapon-effect text, cut-picto removals all land
        // in ST_PassiveEffects). CopyIntoMod starts from the VANILLA extract, so
        // using it here wholesale reverted every one of those strings to vanilla
        // — e.g. First Offensive I–V lost its renames and "deals 10% more damage"
        // text, and Lucky Aim went back to the stale 30%.
        var st = FindAsset(modDir, "ST_PassiveEffects.uasset")
            ?? CopyIntoMod("ST_PassiveEffects", modDir);
        DataTableEditor.AddStringTableEntries(st, new Dictionary<string, string>
        {
            ["PASSIVE_Cheater_Description"] = "Always play three times in a row.",
        });
        n++;
        return n;
    }

    private const string TurnBuff = "BP_BattleBuff_RemoveItselfAtAnotherCharacterTurn";
    private const string BuffStackingEnum = "E_jRPG_BuffStackingType";
    private const string BuffStackNew = "NewEnumerator1";

    /// <summary>
    /// The dedup buff is the counter for how many bonus turns a character has taken in the
    /// current window. It inherits <c>StackingType = ReplaceExisting</c> from
    /// <c>BP_BattleBuffInstance</c> (E_jRPG_BuffStackingType NewEnumerator2, numeric 1), and
    /// <c>AddBuff</c>'s ReplaceExisting branch removes the old wrapper before adding a new
    /// one, so the stack count never exceeds 1. Override it to NewEnumerator1 (StackNew,
    /// numeric 0) so AddBuff takes the stack branch and GetActiveBuffCounts counts every
    /// granted bonus turn. The buff is still cleared when another character's turn starts.
    /// </summary>
    private static int MakeTurnBuffStack(string modDir)
    {
        var path = CopyIntoMod(TurnBuff, modDir);
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
        if (cdo is null) return 0;

        if (cdo.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "StackingType")
            is EnumPropertyData existing)
        {
            if (existing.Value?.ToString() == BuffStackNew) return 0;
            existing.Value = FName.FromString(asset, BuffStackNew);
            existing.EnumType = FName.FromString(asset, BuffStackingEnum);
            WriteAsset(asset, path);
            if (!VerifyTurnBuffStack(path))
                throw new InvalidDataException($"{TurnBuff}: StackingType override did not survive the write");
            Console.WriteLine("  [turns] turn buff StackingType -> StackNew (existing override)");
            return 1;
        }

        // The CDO does not override StackingType, so it inherits ReplaceExisting. Add the
        // override, cloning the CDO's own enum property so the property-tag shape (and its
        // complete type name) stays valid, then retarget it at the stacking enum.
        var tpl = cdo.Data.OfType<EnumPropertyData>().FirstOrDefault();
        if (tpl is null) return 0;
        var prop = (EnumPropertyData)ShallowClone(tpl);
        prop.Name = FName.FromString(asset, "StackingType");
        prop.Value = FName.FromString(asset, BuffStackNew);
        prop.EnumType = FName.FromString(asset, BuffStackingEnum);
        prop.InnerType = FName.FromString(asset, "ByteProperty");
        if (prop.PropertyTypeName?.Nodes is { Count: > 1 } nodes)
        {
            var node = nodes[1];
            node.Name = FName.FromString(asset, BuffStackingEnum);
            nodes[1] = node;
        }
        cdo.Data.Add(prop);
        WriteAsset(asset, path);
        if (!VerifyTurnBuffStack(path))
            throw new InvalidDataException($"{TurnBuff}: StackingType override did not survive the write");
        Console.WriteLine("  [turns] turn buff StackingType -> StackNew (override added)");
        return 1;
    }

    /// <summary>Re-reads the saved buff and requires StackingType = StackNew.</summary>
    public static bool VerifyTurnBuffStack(string uassetPath)
    {
        try
        {
            var map = DataTableEditor.Mappings;
            var a = map != null
                ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
                : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
            var cdo = a.Exports.OfType<NormalExport>()
                .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
            var st = cdo?.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "StackingType")
                as EnumPropertyData;
            var value = st?.Value?.ToString() ?? "";
            var type = st?.EnumType?.ToString() ?? "";
            bool ok = value.EndsWith(BuffStackNew, StringComparison.Ordinal)
                      && type.Contains(BuffStackingEnum, StringComparison.Ordinal);
            Console.WriteLine($"  [verify] turn buff StackingType={value} enum={type} ({ok})");
            return ok;
        }
        catch (Exception ex)
        {
            Console.WriteLine("  [verify] turn buff failed: " + ex.Message);
            return false;
        }
    }

    private static object ShallowClone(object o)
    {
        var m = o.GetType().GetMethod("MemberwiseClone",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return m.Invoke(o, null)!;
    }

    /// <summary>
    /// Adds a function local (cloned from a same-type property already in this asset) and
    /// returns the property pointer to reference it. Matches the repo's rule that missing
    /// locals are shallow-cloned from the same blueprint, never copied across blueprints.
    /// </summary>
    private static KismetPropertyPointer EnsureLocal(UAsset asset, FunctionExport fn, string name,
        Func<FProperty, bool> matches)
    {
        var props = (fn.LoadedProperties ?? Array.Empty<FProperty>()).ToList();
        if (!props.Any(p => p.Name.ToString() == name))
        {
            var tpl = FindTemplateLocal(asset, matches)
                ?? throw new InvalidDataException($"no same-type local template for {name} in {fn.ObjectName}");
            var prop = (FProperty)ShallowClone(tpl);
            prop.Name = FName.FromString(asset, name);
            props.Add(prop);
            fn.LoadedProperties = props.ToArray();
        }
        return new KismetPropertyPointer(new FFieldPath(
            new[] { FName.FromString(asset, name) }, FPackageIndex.FromExport(asset.Exports.IndexOf(fn))));
    }

    private static FProperty? FindTemplateLocal(UAsset asset, Func<FProperty, bool> matches)
    {
        foreach (var f in asset.Exports.OfType<FunctionExport>())
            foreach (var p in f.LoadedProperties ?? Array.Empty<FProperty>())
                if (matches(p)) return p;
        return null;
    }

    // ── per-source turn dedup ────────────────────────────────────────────────
    // The cap alone lets one source grant a turn on several turns in a row. Each
    // source carries its own "used this window" flag in its own StackCount (see
    // WrapBuffSource); the flag resets when the marker buff is gone, which is the
    // window boundary. No class member is added, so save state is untouched.

    private const string MarkerBuffPkg = "/Game/Gameplay/Buffs/UniqueBuffs/BP_BattleBuff_RemoveItselfAtAnotherCharacterTurn";
    private const string MarkerBuffClass = "BP_BattleBuff_RemoveItselfAtAnotherCharacterTurn_C";
    private const string StatsPkg = "/Game/jRPGTemplate/Blueprints/Components/AC_jRPG_CharacterBattleStats";
    private const int SourceStackUnused = 1;   // base BP_BattleBuffInstance CDO default
    private const int SourceStackUsed = 2;

    /// <summary>
    /// Gives a buff source (the weapon lumina, Cheater) its own "used this window" flag in
    /// its own <c>StackCount</c>. The base buff CDO ships StackCount = 1, so 1 = unused and
    /// 2 = used (0 would imply "no stacks"). The flag is cleared when the marker buff is
    /// gone, which is exactly the window boundary (the marker removes itself when another
    /// character's turn starts). Nothing here changes a class's serialized layout, so save
    /// state is untouched.
    /// </summary>
    private static int WrapBuffSource(string modDir, string assetName)
    {
        var path = CopyIntoMod(assetName, modDir);
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);

        int wrapped = 0;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            var stmts = fn.ScriptBytecode.ToList();
            int idx = stmts.FindIndex(s =>
            {
                bool hit = false;
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (e is EX_Context c && c.ContextExpression is EX_VirtualFunction vf
                        && vf.VirtualFunctionName.ToString() == "Force Play Again") hit = true;
                });
                return hit;
            });
            if (idx < 0 || stmts[idx] is not EX_Context callCtx) continue;

            var statsRef = callCtx.ObjectExpression;   // the stats component (already materialised)
            var bcLocal = TryEnsureLocal(asset, fn, "E33TurnBuffComp", p => p is FObjectProperty);
            var flagLocal = TryEnsureLocal(asset, fn, "E33TurnMarkerUp", p => p is FBoolProperty);
            var unusedLocal = TryEnsureLocal(asset, fn, "E33TurnStackUnused", p => p is FBoolProperty);
            var usedLocal = EnsureIntLocal(asset, fn, "E33TurnUsedStack");
            if (bcLocal is null || flagLocal is null || unusedLocal is null)
            {
                Console.WriteLine($"  [turns] {assetName}.{fn.ObjectName}: no local template for the buff-source guard; left unguarded");
                continue;
            }

            EX_LocalVariable L(KismetPropertyPointer ptr) => new() { Variable = ptr };

            // DIAG PROBE: E33_TURNS_GUARD_LOCALS_ONLY=1 adds the new locals but leaves the
            // bytecode untouched, to tell whether the loader rejects the added properties or
            // the rewritten bytecode.
            if (Environment.GetEnvironmentVariable("E33_TURNS_GUARD_LOCALS_ONLY") == "1")
            {
                WriteAsset(asset, path);
                Console.WriteLine($"  [turns] {assetName}.{fn.ObjectName}: PROBE locals-only (bytecode untouched)");
                wrapped++;
                continue;
            }

            // bc = stats.BattleBuffComponent  (cross-class read; EnsureObjectImport reuses the
            // asset's existing BlueprintGeneratedClass import for the stats class).
            var bcPtr = new KismetPropertyPointer(new FFieldPath(
                new[] { FName.FromString(asset, "BattleBuffComponent") },
                LuneSkillsKit.EnsureObjectImport(asset, StatsPkg, "AC_jRPG_CharacterBattleStats_C",
                    "BlueprintGeneratedClass", "/Script/Engine")));
            var bcRead = new EX_Context
            {
                ObjectExpression = MayhemFixKit.CloneKismet(statsRef),
                RValuePointer = bcLocal,
                ContextExpression = new EX_InstanceVariable { Variable = bcPtr },
            };
            bcRead.Offset = ContextSkip(asset, bcRead);

            // flag = bc.HasBuffOfClass(MarkerClass)
            var markerClass = LuneSkillsKit.EnsureObjectImport(asset, MarkerBuffPkg, MarkerBuffClass,
                "BlueprintGeneratedClass", "/Script/Engine");
            var hasCall = new EX_Context
            {
                ObjectExpression = L(bcLocal),
                RValuePointer = flagLocal,
                ContextExpression = new EX_LocalVirtualFunction
                {
                    VirtualFunctionName = FName.FromString(asset, "HasBuffOfClass"),
                    Parameters = new KismetExpression[] { new EX_ObjectConst { Value = markerClass } },
                },
            };
            hasCall.Offset = ContextSkip(asset, hasCall);

            var notBool = EnsureEngineFunction(asset, "KismetMathLibrary", "Not_PreBool");
            var eq = EnsureEngineFunction(asset, "KismetMathLibrary", "EqualEqual_IntInt");
            var setStack = EnsureEngineFunction(asset, "KismetSystemLibrary", "SetIntPropertyByName");
            KismetExpression SetStack(int v) => new EX_CallMath
            {
                StackNode = setStack,
                Parameters = new KismetExpression[]
                {
                    new EX_Self(),
                    new EX_NameConst { Value = FName.FromString(asset, "StackCount") },
                    new EX_IntConst { Value = v },
                },
            };

            var inserted = new List<KismetExpression>();
            // Pre-clear the out param so a skipped call still reads "not applied".
            if (FindLocal(fn, "CallFunc_Force_Play_Again_HasAppliedForcePlayAgain") is { } applied)
                inserted.Add(new EX_LetBool
                {
                    VariableExpression = MayhemFixKit.CloneKismet(applied),
                    AssignmentExpression = new EX_False(),
                });
            inserted.Add(new EX_LetObj { VariableExpression = L(bcLocal), AssignmentExpression = bcRead });
            inserted.Add(new EX_LetBool { VariableExpression = L(flagLocal), AssignmentExpression = hasCall });
            inserted.Add(new EX_LetBool
            {
                VariableExpression = L(flagLocal),
                AssignmentExpression = new EX_CallMath
                {
                    StackNode = notBool,
                    Parameters = new KismetExpression[] { L(flagLocal) },
                },
            });
            var skipReset = new EX_JumpIfNot { CodeOffset = 0, BooleanExpression = L(flagLocal) };
            inserted.Add(skipReset);
            inserted.Add(SetStack(SourceStackUnused));
            // used = self.GetStackCount()  (value-returning call wrapped in EX_Context,
            // matching how the game compiles GetSourceCharacter / GetActiveBuffCounts).
            var getStack = new EX_Context
            {
                ObjectExpression = new EX_Self(),
                RValuePointer = usedLocal,
                ContextExpression = new EX_LocalVirtualFunction
                {
                    VirtualFunctionName = FName.FromString(asset, "GetStackCount"),
                    Parameters = Array.Empty<KismetExpression>(),
                },
            };
            getStack.Offset = ContextSkip(asset, getStack);
            inserted.Add(new EX_Let { Value = usedLocal, Variable = L(usedLocal), Expression = getStack });
            inserted.Add(new EX_LetBool
            {
                VariableExpression = L(unusedLocal),
                AssignmentExpression = new EX_CallMath
                {
                    StackNode = eq,
                    Parameters = new KismetExpression[] { L(usedLocal), new EX_IntConst { Value = SourceStackUnused } },
                },
            });
            var skipCall = new EX_JumpIfNot { CodeOffset = 0, BooleanExpression = L(unusedLocal) };
            inserted.Add(skipCall);
            inserted.Add(SetStack(SourceStackUsed));

            uint insertOff = 0;
            for (int i = 0; i < idx; i++) insertOff += (uint)stmts[i].GetSize(asset);
            uint added = (uint)inserted.Sum(s => (long)s.GetSize(asset));
            uint callSize = (uint)callCtx.GetSize(asset);
            uint uberSizeOld = 0;
            foreach (var s in stmts) uberSizeOld += (uint)s.GetSize(asset);

            stmts.InsertRange(idx, inserted);
            fn.ScriptBytecode = stmts.ToArray();
            MayhemFixKit.ShiftAbsolute(asset, fn, insertOff, added, null);

            // A custom-event ubergraph's EntryPoint is an absolute offset into it and the stub
            // functions pass it as an IntConst (ApplyOnTurnEnd=64, OnCharacterTurnStart=160).
            // Inserting shifts every EntryPoint at/after the insertion, so retarget those
            // literals here — the same fix RemoveVoiceFromDirector applies to the removal case.
            // Without it the next event dispatch (OnCharacterTurnStart, i.e. battle start) lands
            // mid-statement and the VM crashes.
            if (fn.ObjectName.ToString().StartsWith("ExecuteUbergraph", StringComparison.Ordinal))
            {
                foreach (var other in asset.Exports.OfType<FunctionExport>())
                {
                    if (ReferenceEquals(other, fn) || other.ScriptBytecode is null) continue;
                    MayhemFixKit.Walk(other.ScriptBytecode, e =>
                    {
                        if (e is EX_IntConst ic && ic.Value >= (int)insertOff && ic.Value < (int)uberSizeOld)
                            ic.Value += (int)added;
                    });
                }
            }

            // Both targets are in the post-insert layout, so set them after the shift.
            int skipResetIdx = inserted.IndexOf(skipReset);
            uint resetTarget = insertOff;
            for (int i = 0; i < skipResetIdx + 2; i++) resetTarget += (uint)inserted[i].GetSize(asset);
            skipReset.CodeOffset = resetTarget;                       // → the GetStackCount statement
            skipCall.CodeOffset = insertOff + added + callSize;       // → just after the call
            wrapped++;
        }
        if (wrapped == 0) return 0;
        WriteAsset(asset, path);
        Console.WriteLine($"  [turns] {assetName}: {wrapped} buff source(s) given their own per-window flag");
        return wrapped;
    }

    private static KismetExpression? FindLocal(FunctionExport fn, string name)
    {
        KismetExpression? found = null;
        MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (found is null && e is EX_LocalVariable lv && FieldName(lv.Variable) == name) found = lv;
        });
        return found;
    }

    /// <summary>
    /// Int local for the source guard. Clones a same-type property when the asset has one,
    /// otherwise builds a plain IntProperty (the weapon-lumina passive declares no int).
    /// </summary>
    private static KismetPropertyPointer EnsureIntLocal(UAsset asset, FunctionExport fn, string name)
    {
        var props = (fn.LoadedProperties ?? Array.Empty<FProperty>()).ToList();
        if (!props.Any(p => p.Name.ToString() == name))
        {
            // Prefer a non-parameter template: cloning a parameter keeps CPF_Parm and
            // turns the new local into an extra parameter, which breaks the override
            // signature of functions like ProcessDealtRawDamages at load time.
            var tpl = props.FirstOrDefault(p => IsIntLike(p) && !IsParm(p))
                ?? FindTemplateLocal(asset, p => IsIntLike(p) && !IsParm(p))
                ?? props.FirstOrDefault(IsIntLike)
                ?? FindTemplateLocal(asset, IsIntLike);
            FProperty prop = tpl is not null
                ? (FProperty)ShallowClone(tpl)
                : new FGenericProperty
                {
                    SerializedType = FName.FromString(asset, "IntProperty"),
                    ElementSize = 4,
                };
            prop.Name = FName.FromString(asset, name);
            prop.PropertyFlags = EPropertyFlags.CPF_None;
            prop.RepIndex = 0;
            props.Add(prop);
            fn.LoadedProperties = props.ToArray();
        }
        return new KismetPropertyPointer(new FFieldPath(
            new[] { FName.FromString(asset, name) }, FPackageIndex.FromExport(asset.Exports.IndexOf(fn))));
    }

    private static bool IsParm(FProperty p) => (p.PropertyFlags & EPropertyFlags.CPF_Parm) != 0;

    private static bool IsIntLike(FProperty p) =>
        p is not FBoolProperty && p is not FObjectProperty
        && p.ElementSize == 4 && p.Name.ToString().Contains("int", StringComparison.OrdinalIgnoreCase);

    /// <summary>Like <see cref="EnsureLocal"/> but returns null instead of throwing.</summary>
    private static KismetPropertyPointer? TryEnsureLocal(UAsset asset, FunctionExport fn, string name,
        Func<FProperty, bool> matches)
    {
        var props = (fn.LoadedProperties ?? Array.Empty<FProperty>()).ToList();
        if (!props.Any(p => p.Name.ToString() == name))
        {
            // A function local must not be flagged as a parameter; cloning the function's
            // own parameters (as the naive FirstOrDefault does) would extend the override
            // signature and the game's loader rejects the asset at battle start.
            var tpl = props.FirstOrDefault(p => matches(p) && !IsParm(p))
                ?? FindTemplateLocal(asset, p => matches(p) && !IsParm(p))
                ?? props.FirstOrDefault(matches)
                ?? FindTemplateLocal(asset, matches);
            if (tpl is null) return null;
            var prop = (FProperty)ShallowClone(tpl);
            prop.Name = FName.FromString(asset, name);
            prop.PropertyFlags = EPropertyFlags.CPF_None;
            prop.RepIndex = 0;
            props.Add(prop);
            fn.LoadedProperties = props.ToArray();
        }
        return new KismetPropertyPointer(new FFieldPath(
            new[] { FName.FromString(asset, name) }, FPackageIndex.FromExport(asset.Exports.IndexOf(fn))));
    }

    internal static FPackageIndex EnsureEngineFunction(UAsset asset, string library, string functionName)
    {
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (imp.ObjectName.ToString() != functionName) continue;
            if (!imp.OuterIndex.IsImport()) continue;
            var outer = asset.Imports[Math.Abs(imp.OuterIndex.Index) - 1].ObjectName.ToString();
            if (outer == library) return FPackageIndex.FromImport(i);
        }
        int tmpl = -1;
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (!imp.OuterIndex.IsImport()) continue;
            if (asset.Imports[Math.Abs(imp.OuterIndex.Index) - 1].ObjectName.ToString() != library) continue;
            if (imp.ClassName.ToString() != "Function") continue;
            tmpl = i;
            break;
        }
        if (tmpl >= 0)
        {
            var src = asset.Imports[tmpl];
            return asset.AddImport(new Import(src.ClassPackage.ToString(), "Function",
                src.OuterIndex, functionName, false, asset));
        }
        FPackageIndex? enginePkg = null, lib = null;
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (imp.ObjectName.ToString() == "/Script/Engine" && imp.ClassName.ToString() == "Package")
                enginePkg = FPackageIndex.FromImport(i);
            if (imp.ObjectName.ToString() == library) lib = FPackageIndex.FromImport(i);
        }
        enginePkg ??= asset.AddImport(new Import("/Script/CoreUObject", "Package",
            FPackageIndex.FromRawIndex(0), "/Script/Engine", false, asset));
        lib ??= asset.AddImport(new Import("/Script/CoreUObject", library, enginePkg, library, false, asset));
        return asset.AddImport(new Import("/Script/CoreUObject", "Function", lib, functionName, false, asset));
    }

    /// <summary>
    /// Caps how many turns a character can take in a row.
    ///
    /// Vanilla's limiter is a boolean gate: <c>Force Play Again</c> only queues a bonus
    /// turn while <c>BP_BattleBuff_RemoveItselfAtAnotherCharacterTurn</c> is absent, and
    /// every caller passes <c>PreventChainedPlayAgain = true</c>. The buff is spawned by
    /// the first successful force and removed when a *different* character's turn starts,
    /// so vanilla grants exactly one bonus turn per window (2 in a row). The old patch
    /// deleted that check (forced <c>notPrevent = true</c>), which made the queue
    /// unconditional: a multi-hit attack queued one bonus turn per hit
    /// (<c>BP_LU_AttackPlayAgain.ProcessDealtRawDamages</c> runs per damage instance) and
    /// any self-renewing source looped until another character acts.
    ///
    /// Keep vanilla's <c>notPrevent</c> term and replace the buff *presence* test with a
    /// *count* (see <see cref="MakeTurnBuffStack"/> for why the buff must stack):
    ///   count         = BattleBuffComponent.GetActiveBuffCounts(RemoveItself…)   [int local]
    ///   underCap      = count &lt; N-1
    ///   queue         = BattleManagerReference.GetForcedNextCharacters()         [array local]
    ///   alreadyQueued = queue.Contains(self)
    ///   cond          = (underCap AND !alreadyQueued) OR notPrevent
    /// Every call is a statement whose result lands in a type-matched local, matching how
    /// the game's own compiler emits these graphs (nested calls and mismatched
    /// RValuePointers do not survive the VM). The forced-queue entry is consumed by
    /// <c>StartCharacterTurn</c>, so "already queued" means "already granted a bonus turn
    /// this action" — a 3-hit combo counts once. The spawned buff's TurnDuration is raised
    /// so the counter cannot expire mid-chain (<c>HasExpired</c> is <c>TurnDuration == 0</c>).
    /// </summary>
    private static int AllowChainedPlayAgain(string path, int maxTurns)
    {
        int cap = Math.Max(1, maxTurns - 1);
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(e => e.ObjectName.ToString() == "Force Play Again");
        if (fn?.ScriptBytecode is null) return 0;

        var stmts = fn.ScriptBytecode.ToList();

        // Vanilla shapes: hasBuff = BattleBuffComponent.HasBuffOfClass(RemoveItself…),
        // notHasBuff = !hasBuff, cond = OR(notHasBuff, notPrevent), duration = 3.
        EX_LetBool? hasBuff = null, notHasBuff = null, cond = null;
        EX_Let? duration = null;
        foreach (var s in stmts)
        {
            if (s is EX_LetBool lb)
            {
                if (hasBuff is null)
                {
                    if (lb.AssignmentExpression is EX_Context hc
                        && hc.ContextExpression is EX_LocalVirtualFunction hv
                        && hv.VirtualFunctionName.ToString() == "HasBuffOfClass") hasBuff = lb;
                }
                else if (notHasBuff is null)
                {
                    if (lb.AssignmentExpression is EX_CallMath nc && IsImport(asset, nc, "Not_PreBool")
                        && nc.Parameters is { Length: 1 } && nc.Parameters[0] is EX_LocalVariable nl
                        && hasBuff.VariableExpression is EX_LocalVariable hl
                        && FieldName(nl.Variable) == FieldName(hl.Variable)) notHasBuff = lb;
                }
                else if (cond is null)
                {
                    if (lb.AssignmentExpression is EX_CallMath oc && IsImport(asset, oc, "BooleanOR")
                        && oc.Parameters is { Length: 2 }) cond = lb;
                }
            }
            else if (duration is null && s is EX_Let let
                && let.Expression is EX_CallMath ml && IsImport(asset, ml, "MakeLiteralInt")
                && ml.Parameters is { Length: 1 } && ml.Parameters[0] is EX_IntConst ic && ic.Value == 3)
            {
                duration = let;
            }
        }
        if (hasBuff is null || notHasBuff is null || cond is null || duration is null) return 0;
        if (hasBuff.VariableExpression is not EX_LocalVariable underCapVar
            || notHasBuff.VariableExpression is not EX_LocalVariable queuedVar
            || hasBuff.AssignmentExpression is not EX_Context countCtx
            || countCtx.ContextExpression is not EX_LocalVirtualFunction countCall
            || cond.AssignmentExpression is not EX_CallMath orCall
            || orCall.Parameters is not { Length: 2 }) return 0;
        var managerIv = FindInstanceVarRef(fn, "BattleManagerReference");
        if (managerIv is null) return 0;

        // The three statements must be contiguous so one offset shift covers them.
        int i0 = stmts.IndexOf(hasBuff), i1 = stmts.IndexOf(notHasBuff), i2 = stmts.IndexOf(cond);
        if (i1 != i0 + 1 || i2 != i0 + 2) return 0;
        uint regionStart = 0;
        for (int i = 0; i < i0; i++) regionStart += (uint)stmts[i].GetSize(asset);
        uint oldSize = (uint)(stmts[i0].GetSize(asset) + stmts[i1].GetSize(asset) + stmts[i2].GetSize(asset));

        // Locals: the count rides the existing int local (CallFunc_MakeLiteralInt_ReturnValue,
        // untouched until the TurnDuration literal further down); the queue needs an array
        // local, which this function does not have, so add one (same-asset clone).
        if (duration.Variable is not EX_LocalVariable countVar) return 0;
        var queuePtr = EnsureLocal(asset, fn, "ForcedNextCharactersQueue",
            p => p is FArrayProperty a && a.Inner is FObjectProperty);

        // 1. count = BattleBuffComponent.GetActiveBuffCounts(RemoveItself…)
        countCall.VirtualFunctionName = FName.FromString(asset, "GetActiveBuffCounts");
        countCtx.RValuePointer = countVar.Variable;
        countCtx.Offset = ContextSkip(asset, countCtx);
        var countStmt = (EX_Let)MayhemFixKit.CloneKismet(duration);
        countStmt.Expression = countCtx;

        // 2. underCap = count < cap
        var underCapStmt = (EX_LetBool)MayhemFixKit.CloneKismet(hasBuff);
        underCapStmt.AssignmentExpression = new EX_CallMath
        {
            StackNode = ImportRef(asset, "Less_IntInt"),
            Parameters = new KismetExpression[]
            {
                new EX_LocalVariable { Variable = countVar.Variable },
                new EX_IntConst { Value = cap },
            },
        };

        // 3. queue = BattleManagerReference.GetForcedNextCharacters()
        var queueStmt = (EX_Let)MayhemFixKit.CloneKismet(duration);
        queueStmt.Variable = new EX_LocalVariable { Variable = queuePtr };
        queueStmt.Value = queuePtr;
        var queueCall = new EX_Context
        {
            ObjectExpression = MayhemFixKit.CloneKismet(managerIv),
            RValuePointer = queuePtr,
            ContextExpression = new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(asset, "GetForcedNextCharacters"),
                Parameters = Array.Empty<KismetExpression>(),
            },
        };
        queueCall.Offset = ContextSkip(asset, queueCall);
        queueStmt.Expression = queueCall;

        // 4. alreadyQueued = queue.Contains(self)
        var alreadyStmt = (EX_LetBool)MayhemFixKit.CloneKismet(notHasBuff);
        var containsCtx = new EX_Context
        {
            ObjectExpression = new EX_ObjectConst { Value = ImportRef(asset, "Default__KismetArrayLibrary") },
            RValuePointer = queuedVar.Variable,
            ContextExpression = new EX_FinalFunction
            {
                StackNode = ImportRef(asset, "Array_Contains"),
                Parameters = new KismetExpression[]
                {
                    new EX_LocalVariable { Variable = queuePtr },
                    new EX_Self(),
                },
            },
        };
        containsCtx.Offset = ContextSkip(asset, containsCtx);
        alreadyStmt.AssignmentExpression = containsCtx;

        // 5. cond = OR(AND(underCap, !alreadyQueued), notPrevent)
        orCall.Parameters = new KismetExpression[]
        {
            new EX_CallMath
            {
                StackNode = ImportRef(asset, "BooleanAND"),
                Parameters = new KismetExpression[]
                {
                    new EX_LocalVariable { Variable = underCapVar.Variable },
                    new EX_CallMath
                    {
                        StackNode = ImportRef(asset, "Not_PreBool"),
                        Parameters = new KismetExpression[] { new EX_LocalVariable { Variable = queuedVar.Variable } },
                    },
                },
            },
            orCall.Parameters[1],
        };

        // 6. Keep the dedup buff alive for the whole chain: the window counter lives in its
        //    stack count, and the wrapper expires at TurnDuration == 0.
        if (duration.Expression is EX_CallMath dl && dl.Parameters is { Length: 1 }
            && dl.Parameters[0] is EX_IntConst dur) dur.Value = 999;

        var replacement = new KismetExpression[] { countStmt, underCapStmt, queueStmt, alreadyStmt, cond };
        stmts.RemoveRange(i0, 3);
        stmts.InsertRange(i0, replacement);
        fn.ScriptBytecode = stmts.ToArray();

        long delta = replacement.Sum(s => (long)s.GetSize(asset)) - oldSize;
        ShiftTargetsAtOrAfter(asset, fn, regionStart + oldSize, delta);
        if (!CheckTurnsPatch(asset, fn, maxTurns))
        {
            Console.Error.WriteLine("  [turns] cap patch failed its own layout check; leaving Force Play Again vanilla");
            return 0;
        }
        WriteAsset(asset, path);
        Console.WriteLine($"  [turns] Force Play Again: bonus turns capped at {cap} per window, one per action ({delta:+#;-#;0}b)");
        if (!VerifyTurnsPatch(path, maxTurns))
            throw new InvalidDataException("AC_jRPG_CharacterBattleStats: Force Play Again cap patch did not survive the write");
        return 1;
    }

    private static bool IsImport(UAsset asset, EX_FinalFunction fn, string name) =>
        fn.StackNode is { } sn && sn.IsImport() && sn.ToImport(asset).ObjectName.ToString() == name;

    private static FPackageIndex ImportRef(UAsset asset, string objectName)
    {
        for (int i = 0; i < asset.Imports.Count; i++)
            if (asset.Imports[i].ObjectName.ToString() == objectName) return FPackageIndex.FromImport(i);
        throw new InvalidDataException($"AC_jRPG_CharacterBattleStats: import not found: {objectName}");
    }

    private static EX_InstanceVariable? FindInstanceVarRef(FunctionExport fn, string name)
    {
        EX_InstanceVariable? found = null;
        MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (found is null && e is EX_InstanceVariable iv && FieldName(iv.Variable) == name) found = iv;
        });
        return found;
    }

    /// <summary>The EX_Context skip length: the size of its context expression.</summary>
    private static uint ContextSkip(UAsset asset, EX_Context ctx) =>
        ctx.ContextExpression is null ? 0 : (uint)ctx.ContextExpression.GetSize(asset);

    /// <summary>
    /// Moves absolute targets that point exactly at <paramref name="at"/> by <paramref name="delta"/>
    /// (used after an insertion, where <c>ShiftAbsolute</c>'s strict boundary misses them).
    /// </summary>
    private static void ShiftTargetsAt(UAsset asset, FunctionExport fn, uint at, long delta)
    {
        if (delta == 0) return;
        uint pos = 0;
        foreach (var stmt in fn.ScriptBytecode!)
            stmt.Visit(asset, ref pos, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j when j.CodeOffset == at:
                        j.CodeOffset = (uint)(j.CodeOffset + delta); break;
                    case EX_JumpIfNot jn when jn.CodeOffset == at:
                        jn.CodeOffset = (uint)(jn.CodeOffset + delta); break;
                    case EX_PushExecutionFlow pf when pf.PushingAddress == at:
                        pf.PushingAddress = (uint)(pf.PushingAddress + delta); break;
                    case EX_SwitchValue sv:
                        if (sv.EndGotoOffset == at) sv.EndGotoOffset = (uint)(sv.EndGotoOffset + delta);
                        for (int i = 0; i < sv.Cases.Length; i++)
                            if (sv.Cases[i].NextOffset == at)
                                sv.Cases[i].NextOffset = (uint)(sv.Cases[i].NextOffset + delta);
                        break;
                }
            });
    }

    /// <summary>
    /// Moves every absolute bytecode target (jump / push-execution-flow / switch) at or
    /// beyond <paramref name="threshold"/> by <paramref name="delta"/> (which may be
    /// negative) after the statements starting there changed length.
    /// </summary>
    private static void ShiftTargetsAtOrAfter(UAsset asset, FunctionExport fn, uint threshold, long delta)
    {
        if (delta == 0) return;
        uint pos = 0;
        foreach (var stmt in fn.ScriptBytecode!)
            stmt.Visit(asset, ref pos, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j when j.CodeOffset >= threshold:
                        j.CodeOffset = (uint)(j.CodeOffset + delta); break;
                    case EX_JumpIfNot jn when jn.CodeOffset >= threshold:
                        jn.CodeOffset = (uint)(jn.CodeOffset + delta); break;
                    case EX_PushExecutionFlow pf when pf.PushingAddress >= threshold:
                        pf.PushingAddress = (uint)(pf.PushingAddress + delta); break;
                    case EX_SwitchValue sv:
                        if (sv.EndGotoOffset >= threshold) sv.EndGotoOffset = (uint)(sv.EndGotoOffset + delta);
                        for (int i = 0; i < sv.Cases.Length; i++)
                            if (sv.Cases[i].NextOffset >= threshold)
                                sv.Cases[i].NextOffset = (uint)(sv.Cases[i].NextOffset + delta);
                        break;
                }
            });
    }

    /// <summary>
    /// Re-reads a saved component and requires the cap patch to be present and its
    /// absolute targets to still land on statement starts.
    /// </summary>
    public static bool VerifyTurnsPatch(string uassetPath, int maxTurns)
    {
        var map = DataTableEditor.Mappings;
        var a = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
        var fn = a.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "Force Play Again");
        if (fn?.ScriptBytecode is null) return false;
        return CheckTurnsPatch(a, fn, maxTurns);
    }

    /// <summary>
    /// Shape + target check for the cap patch: the count/queue condition, the cap
    /// constant, the retained vanilla notPrevent term, the raised buff duration, and
    /// every absolute target on a statement start.
    /// </summary>
    private static bool CheckTurnsPatch(UAsset a, FunctionExport fn, int maxTurns)
    {
        int cap = Math.Max(1, maxTurns - 1);
        bool count = false, queue = false, contains = false, and = false, notPrevent = false;
        bool containsReadsQueue = false;
        int capConsts = 0, dur999 = 0;
        bool hasQueueLocal = (fn.LoadedProperties ?? Array.Empty<FProperty>())
            .Any(p => p.Name.ToString() == "ForcedNextCharactersQueue" && p is FArrayProperty);
        MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (e is EX_LocalVirtualFunction vf)
            {
                var n = vf.VirtualFunctionName.ToString();
                if (n == "GetActiveBuffCounts") count = true;
                if (n == "GetForcedNextCharacters") queue = true;
            }
            if (e is EX_FinalFunction ff && ff.StackNode is { } sn && sn.IsImport())
            {
                var n = sn.ToImport(a).ObjectName.ToString();
                if (n == "Array_Contains")
                {
                    contains = true;
                    if (ff.Parameters is { Length: 2 }
                        && ff.Parameters[0] is EX_LocalVariable av
                        && FieldName(av.Variable) == "ForcedNextCharactersQueue") containsReadsQueue = true;
                }
                if (n == "MakeLiteralInt" && ff.Parameters is { Length: 1 }
                    && ff.Parameters[0] is EX_IntConst mi && mi.Value == 999) dur999++;
            }
            if (e is EX_CallMath cm && cm.StackNode is { } msn && msn.IsImport())
            {
                var n = msn.ToImport(a).ObjectName.ToString();
                if (n == "BooleanAND") and = true;
                if (n == "Not_PreBool" && cm.Parameters is { Length: 1 }
                    && cm.Parameters[0] is EX_LocalVariable lv
                    && FieldName(lv.Variable) == "PreventChainedPlayAgain") notPrevent = true;
                if (n == "Less_IntInt" && cm.Parameters is { Length: 2 }
                    && cm.Parameters[1] is EX_IntConst ic && ic.Value == cap) capConsts++;
            }
        });

        var starts = new HashSet<uint>();
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!) { starts.Add(pos); pos += (uint)st.GetSize(a); }
        starts.Add(pos);
        int bad = 0;
        uint p = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(a, ref p, (e, _) =>
            {
                if (e is EX_Jump j && !starts.Contains(j.CodeOffset)) bad++;
                else if (e is EX_JumpIfNot jn && !starts.Contains(jn.CodeOffset)) bad++;
                else if (e is EX_PushExecutionFlow pf && !starts.Contains(pf.PushingAddress)) bad++;
            });

        bool ok = count && queue && contains && containsReadsQueue && and && notPrevent
                  && hasQueueLocal && capConsts == 1 && dur999 == 1 && bad == 0;
        Console.WriteLine($"  [verify] Force Play Again: count={count} queue={queue} contains={contains} readsQueue={containsReadsQueue} local={hasQueueLocal} and={and} notPrevent={notPrevent} cap={capConsts}(={cap}) dur999={dur999} badTargets={bad}");
        return ok;
    }

    private static void WriteAsset(UAsset asset, string path)
    {
        try { asset.Write(path); }
        catch (FormatException ex)
        {
            throw new FormatException(Path.GetFileName(path) + ": " + ex.Message, ex);
        }
    }

    private static int ApplyPermadeath(string modDir, bool on)
    {
        int n = 0;
        var stats = CopyIntoMod("AC_jRPG_CharacterBattleStats", modDir);
        if (SetCdoName(stats, "Check_CanResurrect", on ? "CanResurrectDisabled" : "CanResurrect"))
            n++;

        // Revive items live on DT_Items_Consumables (a composite parent). Rows
        // written only onto DT_jRPG_Items_Composite are discarded on load.
        foreach (var table in new[] { "DT_Items_Consumables", "DT_jRPG_Items", "DT_jRPG_Items_Composite" })
        {
            try
            {
                var itemsPath = FindAsset(modDir, table + ".uasset") ?? CopyIntoMod(table, modDir);
                n += SetReviveItemHidden(itemsPath, on);
            }
            catch { /* table optional depending on extract */ }
        }
        return n;
    }

    private static int UnhideReviveItems(string modDir)
    {
        int n = 0;
        foreach (var table in new[] { "DT_Items_Consumables", "DT_jRPG_Items", "DT_jRPG_Items_Composite" })
        {
            var itemsPath = FindAsset(modDir, table + ".uasset");
            if (itemsPath is not null) n += SetReviveItemHidden(itemsPath, false);
        }
        return n;
    }

    private static int SetReviveItemHidden(string itemsPath, bool hidden)
    {
        var ed = new DataTableEditor(itemsPath);
        var json = new Newtonsoft.Json.Linq.JObject();
        foreach (var name in ed.RowNames)
        {
            if (!name.StartsWith("Consumable_Revive", StringComparison.OrdinalIgnoreCase)
                && name is not "ConsumableUpgradeMaterial_Revive")
                continue;
            json[name] = new Newtonsoft.Json.Linq.JObject
            {
                ["HideInInventory"] = hidden,
                ["HideInLootPopup"] = hidden,
            };
        }
        if (json.Count == 0) return 0;
        var n = ed.Apply(json.ToString());
        ed.Save(itemsPath);
        return n;
    }

    private static void DeletePair(string modDir, string fileName)
    {
        var hit = FindAsset(modDir, fileName);
        if (hit is null) return;
        File.Delete(hit);
        var uexp = Path.ChangeExtension(hit, ".uexp");
        if (File.Exists(uexp)) File.Delete(uexp);
    }

    /// <summary>
    /// Removes a BP_jRPG_Character_Battle_Base left in the pack by the retired
    /// CDO window patch. The old patch is recognizable by its tightened CDO
    /// (DodgeImmuneDuration/ParryImmuneDuration below vanilla, or a shrunk
    /// ParryLockDuration). A vanilla-base copy staged by a future kit is left
    /// untouched.
    /// </summary>
    private static void DeleteStaleWindowBase(string modDir)
    {
        var path = FindAsset(modDir, "BP_jRPG_Character_Battle_Base.uasset");
        if (path is null) return;
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
        if (cdo is null) return;
        bool stale = false;
        foreach (var p in cdo.Data)
        {
            if (p is not DoublePropertyData d) continue;
            switch (DataTableEditor.CleanName(p.Name))
            {
                case "DodgeImmuneDuration": if (d.Value < VanillaDodgeWindow - 0.0001) stale = true; break;
                case "ParryImmuneDuration": if (d.Value < VanillaParryWindow - 0.0001) stale = true; break;
                case "ParryLockDuration": if (d.Value < 1.2 - 0.0001) stale = true; break;
            }
        }
        if (!stale) return;
        File.Delete(path);
        var uexp = Path.ChangeExtension(path, ".uexp");
        if (File.Exists(uexp)) File.Delete(uexp);
        Console.WriteLine("[tweaks] removed stale CDO window-patch battle base");
    }

    private static int ScaleMapRows(DataTableEditor ed, string prefix, int maxVanilla, Settings s, bool includeLevelField)
    {
        int n = 0;
        var mults = new Dictionary<string, double>
        {
            [StatHp] = s.HpMult,
            [StatAttack] = s.AttackMult,
            [StatSpeed] = s.SpeedMult,
            [StatCrit] = s.CritMult,
            [StatDefense] = s.DefenseMult,
        };
        if (mults.Values.All(v => Math.Abs(v - 1) < 0.0001)) return 0;

        for (int i = 0; i <= maxVanilla; i++)
        {
            var name = prefix + i;
            if (!ed.TryGetRow(name, out var row)) continue;
            var map = row.Value.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "Stats") as MapPropertyData;
            if (map is null) continue;
            foreach (var (k, v) in map.Value.ToList())
            {
                var key = MapKeyName(k);
                if (!mults.TryGetValue(key, out var m) || Math.Abs(m - 1) < 0.0001) continue;
                if (v is FloatPropertyData f) { f.Value = (float)(f.Value * m); n++; }
                else if (v is DoublePropertyData d) { d.Value *= m; n++; }
            }
            _ = includeLevelField;
        }
        return n;
    }

    private static Dictionary<string, double> ReadStatMap(StructPropertyData row)
    {
        var result = new Dictionary<string, double>();
        if (row.Value.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "Stats") is not MapPropertyData map)
            return result;
        foreach (var (k, v) in map.Value)
        {
            result[MapKeyName(k)] = v switch
            {
                FloatPropertyData f => f.Value,
                DoublePropertyData d => d.Value,
                IntPropertyData i => i.Value,
                _ => 0,
            };
        }
        return result;
    }

    private static void SetStatMap(StructPropertyData row, string key, double value)
    {
        if (row.Value.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "Stats") is not MapPropertyData map)
            return;
        foreach (var (k, v) in map.Value)
        {
            if (MapKeyName(k) != key) continue;
            switch (v)
            {
                case FloatPropertyData f: f.Value = (float)value; break;
                case DoublePropertyData d: d.Value = value; break;
                case IntPropertyData i: i.Value = (int)Math.Round(value); break;
            }
        }
    }

    private static string MapKeyName(PropertyData k) => k switch
    {
        NamePropertyData n => n.Value?.ToString() ?? "",
        EnumPropertyData e => e.Value?.ToString() ?? "",
        BytePropertyData b => b.EnumValue?.ToString() ?? b.Value.ToString(),
        _ => k.ToString() ?? "",
    };

    private static void SetInt(StructPropertyData row, string field, int value)
    {
        foreach (var p in row.Value)
        {
            if (DataTableEditor.CleanName(p.Name) != field) continue;
            if (p is IntPropertyData i) i.Value = value;
            else if (p is Int64PropertyData l) l.Value = value;
        }
    }

    internal static bool SetCdoInt(string uassetPath, string field, int value)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
        if (cdo is null) return false;
        foreach (var p in cdo.Data)
        {
            if (DataTableEditor.CleanName(p.Name) != field) continue;
            if (p is IntPropertyData i)
            {
                if (i.Value == value) { WriteAsset(asset, uassetPath); return true; }
                i.Value = value;
                WriteAsset(asset, uassetPath);
                return true;
            }
        }
        return false;
    }

    private static bool SetCdoName(string uassetPath, string field, string value)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
        if (cdo is null) return false;
        foreach (var p in cdo.Data)
        {
            if (DataTableEditor.CleanName(p.Name) != field) continue;
            if (p is NamePropertyData n)
            {
                n.Value = new FName(asset, value);
                WriteAsset(asset, uassetPath);
                return true;
            }
        }
        return false;
    }

    private static void WalkKismet(IEnumerable<KismetExpression> exprs, Action<KismetExpression> visit)
    {
        foreach (var e in exprs)
        {
            visit(e);
            switch (e)
            {
                case EX_Context ctx:
                    if (ctx.ObjectExpression != null) WalkKismet(new[] { ctx.ObjectExpression }, visit);
                    if (ctx.ContextExpression != null) WalkKismet(new[] { ctx.ContextExpression }, visit);
                    break;
                case EX_Let let:
                    if (let.Variable != null) WalkKismet(new[] { let.Variable }, visit);
                    if (let.Expression != null) WalkKismet(new[] { let.Expression }, visit);
                    break;
                case EX_LetObj lo:
                    if (lo.VariableExpression != null) WalkKismet(new[] { lo.VariableExpression }, visit);
                    if (lo.AssignmentExpression != null) WalkKismet(new[] { lo.AssignmentExpression }, visit);
                    break;
                case EX_LetBool lb:
                    if (lb.VariableExpression != null) WalkKismet(new[] { lb.VariableExpression }, visit);
                    if (lb.AssignmentExpression != null) WalkKismet(new[] { lb.AssignmentExpression }, visit);
                    break;
                case EX_VirtualFunction vf when vf.Parameters is { Length: > 0 }:
                    WalkKismet(vf.Parameters, visit);
                    break;
                case EX_FinalFunction ff when ff.Parameters is { Length: > 0 }:
                    WalkKismet(ff.Parameters, visit);
                    break;
                case EX_CallMath cm when cm.Parameters is { Length: > 0 }:
                    WalkKismet(cm.Parameters, visit);
                    break;
                case EX_JumpIfNot jn when jn.BooleanExpression != null:
                    WalkKismet(new[] { jn.BooleanExpression }, visit);
                    break;
                case EX_SwitchValue sw:
                    if (sw.IndexTerm != null) WalkKismet(new[] { sw.IndexTerm }, visit);
                    if (sw.DefaultTerm != null) WalkKismet(new[] { sw.DefaultTerm }, visit);
                    if (sw.Cases != null)
                    {
                        foreach (var c in sw.Cases)
                        {
                            if (c.CaseIndexValueTerm != null)
                                WalkKismet(new[] { c.CaseIndexValueTerm }, visit);
                            if (c.CaseTerm != null)
                                WalkKismet(new[] { c.CaseTerm }, visit);
                        }
                    }
                    break;
            }
        }
    }

    private static void RemoveOwnedAssets(string modDir)
    {
        if (!Directory.Exists(modDir)) return;
        foreach (var name in new[]
                 {
                     "DT_jRPG_Levels.uasset", "DT_CharactersLevelScaling.uasset",
                     "AC_jRPG_CharactersManager.uasset", "BP_CharacterData.uasset",
                     "DT_jRPG_CharacterDefinitions.uasset",
                     "Curve_StatsPerLevel_HP.uasset", "Curve_StatsPerLevel_PAttack.uasset",
                     "Curve_StatsPerLevel_Speed.uasset", "Curve_StatsPerLevel_CritRate.uasset",
                     "Curve_StatsPerLevel_Defense.uasset",
                     "DT_AttributeStatScaling_Vitality.uasset", "DT_AttributeStatScaling_Strength.uasset",
                     "DT_AttributeStatScaling_Agility.uasset", "DT_AttributeStatScaling_Defense.uasset",
                     "DT_AttributeStatScaling_Luck.uasset",
                     "AC_jRPG_BattleManager.uasset", "AC_jRPG_CharacterBattleStats.uasset",
                     "BP_BattleBuff_RemoveItselfAtAnotherCharacterTurn.uasset",
                     "BP_LU_AttackPlayAgain.uasset", "BP_LU_Cheater.uasset",
                     "BP_jRPG_Character_Battle_Base.uasset",
                     "BP_Battle_SkillScript_ThermalTransfer.uasset",
                      "BP_UniqueMechanic_Charge_Component.uasset", "WBP_UniqueMechanic_Charge.uasset",
                      "WBP_PortraitUniqueMechanic_Overcharge.uasset",
                      "WBP_Raw_Attributes_Display.uasset",
                     "BP_Battle_SkillScript_Gustave_UnleashCharge.uasset",
                     "BP_Battle_SkillScript_Gustave_PerfectBreak.uasset",
                     "DT_Balancing_BattleDifficultySettings.uasset",
                     "BP_TutorialSystemComponent.uasset",
                     AutoPictoMasteryKit.ControllerAsset + ".uasset",
                     PictoInstantMasteryKit.AssetStem + ".uasset",
                     PictoPopupCapKit.AssetStem + ".uasset",
                     "DT_Balancing_MainCharacterSkills.uasset",
                     "FL_jRPG_CustomFunctionLibrary.uasset", "BP_CharacterSkillGraphSystem.uasset",
                     "BP_GradientGaugeSystem.uasset",
                     "WBP_GradientChargeGauge.uasset",
                     "BP_BattleBuff_Burn.uasset", "BP_BattleBuff_BurnAggregator.uasset",
                     "BP_BattleDamages.uasset",
                 })
        {
            var hit = FindAsset(modDir, name);
            if (hit is null) continue;
            File.Delete(hit);
            var uexp = Path.ChangeExtension(hit, ".uexp");
            if (File.Exists(uexp)) File.Delete(uexp);
        }
    }

    private static string CopyIntoMod(string asset, string modDir)
    {
        var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "gameplay_tweaks");
        var src = FindAsset(dir, asset + ".uasset")
            ?? FindAsset(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), asset + ".uasset");
        if (src is null)
        {
            Directory.CreateDirectory(dir);
            // Vanilla extraction must not be shadowed by installed mod containers.
            using (E33ModEditor.Core.Packer.DonorExtract.SuspendMods())
                new Retoc().ToLegacy(dir, asset);
            src = FindAsset(dir, asset + ".uasset")
                ?? throw new FileNotFoundException(asset + ".uasset");
        }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) throw new InvalidDataException(asset + " is not under Sandfall/");
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!CopiedThisApply.Add(dest))
            return dest;
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    public static string? FindAsset(string root, string fileName)
    {
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).FirstOrDefault();
    }
}

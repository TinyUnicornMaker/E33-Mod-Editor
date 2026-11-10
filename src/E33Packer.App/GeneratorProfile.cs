using E33ModEditor.Core;
using E33ModEditor.Core.Loot;

namespace E33Packer.App;

/// <summary>
/// The complete set of user choices that, together with the seed, determine a
/// generated pack. The profile is saved next to every build so a seed can be
/// reproduced exactly.
/// </summary>
public sealed class GeneratorProfile
{
    public const int CurrentFormat = 3;
    public int Format { get; set; } = CurrentFormat;
    public string Name { get; set; } = "Expedition Infinity";
    public int Seed { get; set; } = 123456;
    public string Language { get; set; } = "en";

    public RandomizerKit.Settings Randomizer { get; set; } = new();
    public BalanceOptions Balance { get; set; } = new();
    public QoLOptions QoL { get; set; } = new();
}

/// <summary>
/// Curated balance surface. The core AP/turn/burn/crit values are deliberately
/// fixed in <see cref="ToTweaks"/> so the seed generator cannot break the
/// overhaul's damage curve.
/// </summary>
public sealed class BalanceOptions
{
    // Enemy & economy
    public bool BuffRegularEnemies { get; set; } = true;
    public double[] RegularEnemyHpBonus { get; set; } =
        (double[])RegularEnemyBuffKit.DefaultHpBonus.Clone();
    public double[] RegularEnemySpeedBonus { get; set; } =
        (double[])RegularEnemyBuffKit.DefaultSpeedBonus.Clone();
    public double[] RegularEnemyAttackBonus { get; set; } =
        (double[])RegularEnemyBuffKit.DefaultAttackBonus.Clone();
    public bool ReduceLuminaDrops { get; set; }
    public double LuminaDropMultiplier { get; set; } = 1.0;

    // Progression
    public int LevelCap { get; set; } = 333;
    public int ExtraXpPerLevel { get; set; } = 33_333_333;
    public int AttributePointsPerLevel { get; set; } = 2;
    public int SkillPointsPerLevel { get; set; } = 1;
    public int LuminaPerLevel { get; set; } = 2;
    public int AttributeCap { get; set; } = 333;

    // Player power
    public double HpMult { get; set; } = 1.0;
    public double AttackMult { get; set; } = 1.0;
    public double SpeedMult { get; set; } = 1.0;
    public double CritMult { get; set; } = 1.0;
    public double DefenseMult { get; set; } = 1.0;

    // Difficulty
    // Kept for profile compatibility only. Permadeath is not a completed
    // feature, so the generator always writes it off.
    public bool Permadeath { get; set; } = false;
    public bool TightenDefenseWindows { get; set; } = true;
    public double DodgeWindow { get; set; } = 0.165;
    public double ParryWindow { get; set; } = 0.1125;

    // Weapon progression
    public bool WeaponDropLevelEnabled { get; set; } = true;
    public int MaxDropLevel { get; set; } = 222;

    public GameplayTweaksKit.Settings ToTweaks(QoLOptions qol)
    {
        var mastery = (qol.PictoMastery ?? "none").Trim().ToLowerInvariant();
        return new GameplayTweaksKit.Settings
        {
            LevelCap = Math.Clamp(LevelCap, 99, 999),
            ExtraXpPerLevel = Math.Clamp(ExtraXpPerLevel, 1, 99_999_999),
            AttributePointsPerLevel = Math.Clamp(AttributePointsPerLevel, 0, 20),
            SkillPointsPerLevel = Math.Clamp(SkillPointsPerLevel, 1, 10),
            LuminaPerLevel = Math.Clamp(LuminaPerLevel, 1, 10),
            AttributeCap = Math.Clamp(AttributeCap, 99, 500),
            HpMult = Math.Clamp(HpMult, 0.1, 10),
            AttackMult = Math.Clamp(AttackMult, 0.1, 10),
            SpeedMult = Math.Clamp(SpeedMult, 0.1, 10),
            CritMult = Math.Clamp(CritMult, 0.1, 10),
            DefenseMult = Math.Clamp(DefenseMult, 0.1, 10),

            // Core invariants: the overhaul's movesets and damage curve assume
            // these. They are intentionally not exposed in the generator UI.
            MaxAP = 21,
            MaxConsecutiveTurns = 3,
            MaxForetell = 30f,
            MaxShields = 5,
            MaxGradientCharges = 10,
            CritDamageIncrease = 1.5f,
            CritRateDamageScaling = true,
            BurnMultiplier = 0.075,
            BurnPlayerScale = 0.01,
            BurnDuration = 3,
            BurnTailOff = 3.0,

            // Not a finished feature; never enable it from the generator.
            Permadeath = false,
            TightenDefenseWindows = TightenDefenseWindows,
            DodgeWindow = DodgeWindow,
            ParryWindow = ParryWindow,
            // Tutorials are always skipped in the generator.
            DisableTutorials = true,
            MaskKeeperFullMoveset = qol.MaskKeeperFullMoveset,
            SireneFullMoveset = qol.SireneFullMoveset,
            AutoPictoMastery = mastery == "onebattle",
            InstantPictoMastery = mastery == "instant",
            LimitPictoPopups = qol.LimitBattleProcCards,
            PictoPopupCap = Math.Clamp(qol.PictoPopupCap, PictoPopupCapKit.MinCap,
                PictoPopupCapKit.MaxCap),
            BuffRegularEnemies = BuffRegularEnemies,
            RegularEnemyHpBonus = ClampBonusArray(RegularEnemyHpBonus, RegularEnemyBuffKit.DefaultHpBonus),
            RegularEnemySpeedBonus = ClampBonusArray(RegularEnemySpeedBonus, RegularEnemyBuffKit.DefaultSpeedBonus),
            RegularEnemyAttackBonus = ClampBonusArray(RegularEnemyAttackBonus, RegularEnemyBuffKit.DefaultAttackBonus),
            ReduceLuminaDrops = ReduceLuminaDrops,
            LuminaDropMultiplier = Math.Clamp(LuminaDropMultiplier, 0.1, 5.0),
        };
    }

    private static double[] ClampBonusArray(double[]? values, double[] fallback) =>
        values is { Length: 5 }
            ? values.Select(v => double.IsFinite(v) ? Math.Clamp(v, -0.95, 30) : 0).ToArray()
            : (double[])fallback.Clone();

    public WeaponDropLevelKit.Settings ToWeaponDropLevel() => new()
    {
        Enabled = WeaponDropLevelEnabled,
        MaxDropLevel = Math.Clamp(MaxDropLevel, 33, 333),
    };
}

public sealed class QoLOptions
{
    /// <summary>"instant" or "none". Off is the Expedition Infinity default.</summary>
    public string PictoMastery { get; set; } = "none";
    /// <summary>When off the vanilla unlimited battle-proc stack is used.</summary>
    public bool LimitBattleProcCards { get; set; } = true;
    public int PictoPopupCap { get; set; } = PictoPopupCapKit.DefaultCap;
    /// <summary>Vanilla light-stain substitution is off in the standard 1.3
    /// preset; Easy Mode can opt back in from the Quality of Life tab.</summary>
    public bool LightStainWildcard { get; set; }
    public bool SkipTutorials { get; set; } = true;
    public bool BulkUpgradeEnabled { get; set; } = true;
    public int BulkUpgradeLevels { get; set; } = 10;
    public bool BulkTapPlusOne { get; set; }
    public bool MaskKeeperFullMoveset { get; set; } = true;
    public bool SireneFullMoveset { get; set; } = true;

    public SkillOverhaulKit.Settings ToSkillOverhaul()
    {
        var s = new SkillOverhaulKit.Settings();
        // Substitution is not exposed by the generator; keep the core strict.
        s.LightStainWildcard = false;
        return s;
    }

    public WeaponBulkUpgradeKit.Settings ToWeaponBulkUpgrade() => new()
    {
        Enabled = BulkUpgradeEnabled,
        Levels = Math.Clamp(BulkUpgradeLevels, 1, WeaponBulkUpgradeKit.BulkLevels),
        // The tap-for-one-level path is not reliable; the generator does not
        // expose it and always leaves it off.
        TapPlusOne = false,
    };
}

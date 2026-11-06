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
/// Magnitude variants of vanilla pictos.
/// Unique FolderName / no shops or drops.
/// </summary>
public static class PictosMagnitudeKit
{
    public const string Toggle = "PictosMagnitude";

    public enum SwapKind { BalancingSwap, BuffSwap, ReviveBase }

    public sealed record Spec(
        string Id,
        string DisplayName,
        string Description,
        string SourceId,
        string SourceBp,
        string SourceDa,
        IReadOnlyList<(string Key, string CloneFrom, float Value)> Balancing,
        SwapKind Kind = SwapKind.BalancingSwap,
        /// <summary>Status key from <see cref="Statuses"/>: buff-import strings swapped to it.</summary>
        string? SwapToStatus = null,
        string[]? SwapFromClass = null,
        string[]? SwapFromPath = null,
        /// <summary>Legacy: battle-start duration override, unused by current variants.</summary>
        float? Turns = null)
    {
        /// <summary>
        /// Table-row id override. Assets (BP_LU_&lt;Id&gt;, DA_GearPictos_&lt;Id&gt;)
        /// and balancing keys keep using <see cref="Id"/>, but the item /
        /// passive / definition rows are written under this id instead. Used to
        /// repoint a vanilla row at a generated asset (Dead Energy II merging
        /// into the cut-content BootyHunter row).
        /// </summary>
        public string RowId => RowIdOverride ?? Id;

        /// <summary>Backing field for <see cref="RowId"/>; set via `with`.</summary>
        public string? RowIdOverride { get; init; }
    }

    private static readonly string[] ReviveFromClasses =
        { "BP_BattleBuff_Regen", "BP_BattleBuff_Powerful_125", "BP_BattleBuff_Defense15", "BP_BattleBuff_Speed", "BP_BattleBuff_Precision" };
    private static readonly string[] ReviveFromPaths =
        {
            "/Game/Gameplay/Buffs/UniqueBuffs/BP_BattleBuff_Regen",
            "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Powerful_125",
            "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Defense15",
            "/Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_Speed",
            "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Precision",
        };

    public static readonly Spec[] Variants =
    {
        new("BurnDurationIncrease2", "Longer Burn II", "Burn duration is increased by 3.",
            "BurnDurationIncrease", "BP_LU_BurnDurationIncrease", "DA_GearPictos_BurnDurationIncrease",
            new[] { ("BurnDurationIncrease2_DurationIncrease", "BurnDurationIncrease_DurationIncrease", 3f) }),
        new("BurnDurationIncrease3", "Longer Burn III", "Burn duration is increased by 4.",
            "BurnDurationIncrease", "BP_LU_BurnDurationIncrease", "DA_GearPictos_BurnDurationIncrease",
            new[] { ("BurnDurationIncrease3_DurationIncrease", "BurnDurationIncrease_DurationIncrease", 4f) }),
        new("DoubleBurn2", "Triple Burn", "Every Burn applied is tripled.",
            "DoubleBurn", "BP_LU_DoubleBurn", "DA_GearPictos_DoubleBurn",
            new[] { ("DoubleBurn2_BurnMultiplier", "DoubleBurn_BurnMultiplier", 3f) }),
        Stun(2, 0.20f), Stun(3, 0.50f), Stun(4, 1.00f), Stun(5, 1.50f),
        StatusSwap("AutoBerserk", "Auto Berserk", "AutoCurse",
            "DA_GearPictos_AutoCurse", "BP_LU_AutoRush",
            new[] { "BP_BattleBuff_Speed" },
            new[] { "/Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_Speed" },
            "Berserk", "Self apply Berserk on battle start."),
        StatusSwap("AutoBlight", "Auto Blight", "AutoCurse",
            "DA_GearPictos_AutoCurse", "BP_LU_AutoRush",
            new[] { "BP_BattleBuff_Speed" },
            new[] { "/Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_Speed" },
            "Blight", "Self apply Blight on battle start."),
        StatusSwap("SosCurse", "SOS Curse", "SosPower",
            "DA_GearPictos_SosPower", "BP_LU_SosPower",
            new[] { "BP_BattleBuff_Powerful_125" },
            new[] { "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Powerful_125" },
            "Cursed", "Apply Curse when falling below 50% health."),
        StatusSwap("SosBerserk", "SOS Berserk", "SosPower",
            "DA_GearPictos_SosPower", "BP_LU_SosPower",
            new[] { "BP_BattleBuff_Powerful_125" },
            new[] { "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Powerful_125" },
            "Berserked", "Apply Berserk when falling below 50% health."),
        StatusSwap("SosBlight", "SOS Blight", "SosPower",
            "DA_GearPictos_SosPower", "BP_LU_SosPower",
            new[] { "BP_BattleBuff_Powerful_125" },
            new[] { "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Powerful_125" },
            "Blighted", "Apply Blight when falling below 50% health."),
        StatusSwap("ReviveWithCurse", "Cursing Revive", "ReviveWithPower",
            "DA_GearPictos_ReviveWithPower", "BP_LU_ReviveWithStatusOnAllie",
            ReviveFromClasses, ReviveFromPaths,
            "Cursed", "Apply Curse for 3 turns when revived.",
            SwapKind.ReviveBase),
        StatusSwap("ReviveWithBerserk", "Berserked Revive", "ReviveWithPower",
            "DA_GearPictos_ReviveWithPower", "BP_LU_ReviveWithStatusOnAllie",
            ReviveFromClasses, ReviveFromPaths,
            "Berserked", "Apply Berserk for 3 turns when revived.",
            SwapKind.ReviveBase),
        StatusSwap("ReviveWithBlight", "Blighted Revive", "ReviveWithPower",
            "DA_GearPictos_ReviveWithPower", "BP_LU_ReviveWithStatusOnAllie",
            ReviveFromClasses, ReviveFromPaths,
            "Blighted", "Apply Blight for 3 turns when revived.",
            SwapKind.ReviveBase),
        StatusSwap("CursingDeath", "Cursing Death", "ProtectingDeath",
            "DA_GearPictos_ProtectingDeath", "BP_LU_ProtectiveDeath",
            new[] { "BP_BattleBuff_Defense15" },
            new[] { "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Defense15" },
            "Cursed", "On death, allies gain Curse."),
        StatusSwap("BerserkingDeath", "Berserking Death", "ProtectingDeath",
            "DA_GearPictos_ProtectingDeath", "BP_LU_ProtectiveDeath",
            new[] { "BP_BattleBuff_Defense15" },
            new[] { "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Defense15" },
            "Berserked", "On death, allies gain Berserk."),
        StatusSwap("BlightingDeath", "Blighting Death", "ProtectingDeath",
            "DA_GearPictos_ProtectingDeath", "BP_LU_ProtectiveDeath",
            new[] { "BP_BattleBuff_Defense15" },
            new[] { "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Defense15" },
            "Blighted", "On death, allies gain Blight."),
        new("GlassCanon2", "Glass Cannon II", "Deal 25% more damage, but take 50% more damage.",
            "GlassCanon", "BP_LU_GlassCanon", "DA_GearPictos_GlassCanon",
            new[]
            {
                ("GlassCanon2_DamageMultiplier", "GlassCanon_DamageMultiplier", 1.25f),
                ("GlassCanon2_DamageReceivedMultiplier", "GlassCanon_DamageReceivedMultiplier", 1.5f),
            }),
        new("GlassCanon3", "Glass Cannon III", "Deal 25% more damage, but take 100% more damage.",
            "GlassCanon", "BP_LU_GlassCanon", "DA_GearPictos_GlassCanon",
            new[]
            {
                ("GlassCanon3_DamageMultiplier", "GlassCanon_DamageMultiplier", 1.25f),
                ("GlassCanon3_DamageReceivedMultiplier", "GlassCanon_DamageReceivedMultiplier", 2.0f),
            }),
        new("ConsumingAttack2", "Consuming Attack II", "Base attack consumes up to 250 Burns to deal 5% more damage per Burn consumed.",
            "ConsumingAttack", "BP_LU_ConsumingAttack", "DA_GearPictos_ConsumingAttack",
            new[]
            {
                ("ConsumingAttack2_MaxBurnConsumed", "ConsumingAttack_MaxBurnConsumed", 250f),
                ("ConsumingAttack2_DamageMultiplierPerBurn", "ConsumingAttack_DamageMultiplierPerBurn", 0.05f),
            }),
        new("Roulette2", "Roulette II", "Every hit has a 50% chance to deal either 50% or 150% of its damage.",
            "Roulette", "BP_LU_Roulette", "DA_GearPictos_Roulette",
            new[]
            {
                ("Roulette2_LowValue", "Roulette_LowValue", 0.5f),
                ("Roulette2_HighValue", "Roulette_HighValue", 1.5f),
            }),
        new("Roulette3", "Roulette III", "Every hit has a 50% chance to deal either 50% or 150% of its damage.",
            "Roulette", "BP_LU_Roulette", "DA_GearPictos_Roulette",
            new[]
            {
                ("Roulette3_LowValue", "Roulette_LowValue", 0.5f),
                ("Roulette3_HighValue", "Roulette_HighValue", 1.5f),
            }),
        new("Roulette4", "Roulette IV", "Every hit has a 50% chance to deal either 50% or 150% of its damage.",
            "Roulette", "BP_LU_Roulette", "DA_GearPictos_Roulette",
            new[]
            {
                ("Roulette4_LowValue", "Roulette_LowValue", 0.5f),
                ("Roulette4_HighValue", "Roulette_HighValue", 1.5f),
            }),
        new("Roulette5", "Roulette V", "Every hit has a 50% chance to deal either 50% or 150% of its damage.",
            "Roulette", "BP_LU_Roulette", "DA_GearPictos_Roulette",
            new[]
            {
                ("Roulette5_LowValue", "Roulette_LowValue", 0.5f),
                ("Roulette5_HighValue", "Roulette_HighValue", 1.5f),
            }),
        // --- New magnitude variants requested ---
        // Augmented Attack ladder: I +10% / II +25% / III +50%, all written in the
        // vanilla "N% increased Base Attack damage." voice.
        new("AugmentedAttack2", "Augmented Attack II", "25% increased Base Attack damage.",
            "AugmentedAttack", "BP_LU_BaseAttack+25", "DA_GearPictos_AugmentedAttack",
            new[] { ("AugmentedAttack2_DamageIncrease", "AugmentedAttack_DamageIncrease", 0.25f) }),
        new("AugmentedAttack3", "Augmented Attack III", "50% increased Base Attack damage.",
            "AugmentedAttack", "BP_LU_BaseAttack+25", "DA_GearPictos_AugmentedAttack",
            new[] { ("AugmentedAttack3_DamageIncrease", "AugmentedAttack_DamageIncrease", 0.5f) }),
        // Augmented Aim: flat +10% free-aim damage on every tier (I-V).
        AugmentedAim(2, 0.10f), AugmentedAim(3, 0.10f),
        AugmentedAim(4, 0.10f), AugmentedAim(5, 0.10f),
        new("SoloFighter2", "Solo Fighter II", "Deal 50% more damage while fighting alone.",
            "SoloFighter", "BP_LU_SoloFighter", "DA_GearPictos_SoloFighter",
            new[] { ("SoloFighter2_DamageMultiplier", "SoloFighter_DamageMultiplier", 1.5f) }),
        new("SoloFighter3", "Solo Fighter III", "Deal 50% more damage while fighting alone.",
            "SoloFighter", "BP_LU_SoloFighter", "DA_GearPictos_SoloFighter",
            new[] { ("SoloFighter3_DamageMultiplier", "SoloFighter_DamageMultiplier", 1.5f) }),
        new("Teamwork2", "Teamwork II", "Deal 10% more damage while all allies are alive and you are not alone.",
            "Teamwork", "BP_LU_Teamwork", "DA_GearPictos_Teamwork",
            Array.Empty<(string Key, string CloneFrom, float Value)>()),
        new("Teamwork3", "Teamwork III", "Deal 15% more damage while all allies are alive and you are not alone.",
            "Teamwork", "BP_LU_Teamwork", "DA_GearPictos_Teamwork",
            Array.Empty<(string Key, string CloneFrom, float Value)>()),
        // Break Damage: II/III are value-identical clones of Breaker (+25% each,
        // stacked multiplicatively) but use the vanilla "N% increased Break
        // damage." voice instead of the old "Deal 30% more..." placeholder text.
        new("BreakDamage2", "Break Damage II", "25% increased Break damage.",
            "Breaker", "BP_LU_Breaker", "DA_GearPictos_Breaker",
            Array.Empty<(string Key, string CloneFrom, float Value)>()),
        new("BreakDamage3", "Break Damage III", "25% increased Break damage.",
            "Breaker", "BP_LU_Breaker", "DA_GearPictos_Breaker",
            Array.Empty<(string Key, string CloneFrom, float Value)>()),
        // DeadEnergy: vanilla DeadEnergy (I, +3 AP via BP_LU_DeadEnergy reading
        // BootyHunter_ApAmount) and a cut-content BootyHunter picto whose rows
        // reference a BP_LU_BootyHunter that was never shipped (it displays as
        // "Dead Energy II" in vanilla ST but does nothing when equipped).
        // DeadEnergyII merges into the BootyHunter row: the generated
        // BP_LU_DeadEnergyII + DA_GearPictos_DeadEnergyII (+5 AP) assets are
        // created, and the BootyHunter item/passive/definition rows are
        // rewritten to reference them — so old saves owning BootyHunter get a
        // working +5 AP "Dead Energy II", and there is one canonical tier II.
        new("DeadEnergyII", "Dead Energy II", "Gain 5 AP on killing an enemy.",
            "DeadEnergy", "BP_LU_DeadEnergy", "DA_GearPictos_DeadEnergy",
            new[] { ("DeadEnergyII_ApAmount", "BootyHunter_ApAmount", 5f) })
        { RowIdOverride = "BootyHunter" },
        // Dodge Helper window: LongerDodge is the window pictos (+22%). Cloned as Dodge Helper II/III for the requested window expansion.
        new("Dodger2", "Dodge Helper II", "Dodge window increased by 22%.",
            "LongerDodge", "BP_LU_LongerDodge", "DA_GearPictos_LongerDodge",
            new[] { ("Dodger2_DodgeWindowIncrease", "LongerDodge_DodgeWindowIncrease", 0.22f) }),
        new("Dodger3", "Dodge Helper III", "Dodge window increased by 22%.",
            "LongerDodge", "BP_LU_LongerDodge", "DA_GearPictos_LongerDodge",
            new[] { ("Dodger3_DodgeWindowIncrease", "LongerDodge_DodgeWindowIncrease", 0.22f) }),
        // Serialized FDamageModifier type 2 (NewEnumerator3) is a direct
        // multiplier: 2.0 = x2. The additive type is serialized 3
        // (NewEnumerator2), used by Stun Boost and Immaculate.
        new("ParrySpecialist2", "Parry Specialist II", "Parry window reduced by 10%. Counterattacks deal double damage.",
            "ParrySpecialist", "BP_LU_ParrySpecialist", "DA_GearPictos_ParrySpecialist",
            new[] { ("ParrySpecialist2_ParryWindowDecrease", "ParrySpecialist_ParryWindowDecrease", -0.10f), ("ParrySpecialist2_CounterDamageMultiplier", "ParrySpecialist_CounterDamageMultiplier", 2.0f) }),
        new("ParrySpecialist3", "Parry Specialist III", "Parry window reduced by 10%. Counterattacks deal double damage.",
            "ParrySpecialist", "BP_LU_ParrySpecialist", "DA_GearPictos_ParrySpecialist",
            new[] { ("ParrySpecialist3_ParryWindowDecrease", "ParrySpecialist_ParryWindowDecrease", -0.10f), ("ParrySpecialist3_CounterDamageMultiplier", "ParrySpecialist_CounterDamageMultiplier", 2.0f) }),
        // Parry Helper window expansion: ParryHelper is the window pictos (+22%).
        // Cloned as Parry Helper II/III for the requested window expansion (mirrors
        // the Dodge Helper II/III ladder).
        new("ParryHelper2", "Parry Helper II", "Parry window increased by 22%.",
            "ParryHelper", "BP_LU_ParryHelper", "DA_GearPictos_ParryHelper",
            new[] { ("ParryHelper2_ParryWindowIncrease", "ParryHelper_ParryWindowIncrease", 0.22f) }),
        new("ParryHelper3", "Parry Helper III", "Parry window increased by 22%.",
            "ParryHelper", "BP_LU_ParryHelper", "DA_GearPictos_ParryHelper",
            new[] { ("ParryHelper3_ParryWindowIncrease", "ParryHelper_ParryWindowIncrease", 0.22f) }),
        // François's Shell (renamed from Titanium Shell): inverse Glass Cannon.
        // Reduces damage dealt AND damage received (Glass Canon BP has separate
        // dealt/received handlers; the clone retargets both rows to sub-1.0 values).
        new("TitaniumShell", "François's Shell I", "Take 10% less damage, but deal 10% less damage.",
            "GlassCanon", "BP_LU_GlassCanon", "DA_GearPictos_GlassCanon",
            new[]
            {
                ("TitaniumShell_DamageMultiplier", "GlassCanon_DamageMultiplier", 0.9f),
                ("TitaniumShell_DamageReceivedMultiplier", "GlassCanon_DamageReceivedMultiplier", 0.9f),
            }),
        new("TitaniumShell2", "François's Shell II", "Take 10% less damage, but deal 20% less damage.",
            "GlassCanon", "BP_LU_GlassCanon", "DA_GearPictos_GlassCanon",
            new[]
            {
                ("TitaniumShell2_DamageMultiplier", "GlassCanon_DamageMultiplier", 0.8f),
                ("TitaniumShell2_DamageReceivedMultiplier", "GlassCanon_DamageReceivedMultiplier", 0.9f),
            }),
        new("TitaniumShell3", "François's Shell III", "Take 10% less damage, but deal 30% less damage.",
            "GlassCanon", "BP_LU_GlassCanon", "DA_GearPictos_GlassCanon",
            new[]
            {
                ("TitaniumShell3_DamageMultiplier", "GlassCanon_DamageMultiplier", 0.7f),
                ("TitaniumShell3_DamageReceivedMultiplier", "GlassCanon_DamageReceivedMultiplier", 0.9f),
            }),
        // --- New families (requested 2026-09): the vanilla row is reworked into
        // tier I ("<Display> I") by ReworkNewVanillaBases + the tier-I balancing
        // edits in InjectBalancing; these specs add tiers II+ as clones. ---
        Immaculate(2, 0.20f), Immaculate(3, 0.30f), Immaculate(4, 0.50f), Immaculate(5, 1.00f),
        AltCrit(2, 0.20f), AltCrit(3, 0.30f), AltCrit(4, 0.40f), AltCrit(5, 0.50f),
        new("APDiscount2", "AP Discount II", "Skills cost 2 less AP.",
            "APDiscount", "BP_LU_APDiscount", "DA_GearPictos_Immaculate",
            new[] { ("APDiscount2_APReduction", "APDiscount_APReduction", -2f) }),
        FirstLife(2, 0.10f, 0.10f), FirstLife(3, 0.21f, 0.21f),
        AugmentedFirstStrike(2, 0.10f), AugmentedFirstStrike(3, 0.20f),
        AugmentedFirstStrike(4, 0.30f), AugmentedFirstStrike(5, 0.50f),
        // Const/companion families: no balancing row on the donor for Confident
        // (0.5 received multiplier is a DoubleConst in the BP), row-driven but
        // two-asset for Clea's Death (companion _Buff clone), row-driven for
        // Attack Lifesteal (AttackLifesteal_DamagePercentageToHeal).
        Confident(2, 0.90f), Confident(3, 0.85f), Confident(4, 0.80f), Confident(5, 0.75f),
        CleasDeath(2, 0.10f), CleasDeath(3, 0.15f),
        AttackLifesteal(2, 0.10f), AttackLifesteal(3, 0.20f),
        // Gradient Overcharge: vanilla I is rename-only (3 charges, 200% more);
        // II 5 charges for 400% more, III 10 charges for 1000% more. The donor
        // BP reads two balancing rows per tier (charges consumed, multiplier).
        GradientOvercharge(2, 5f, 5.0f), GradientOvercharge(3, 10f, 11.0f),
        // --- 2026-09 ladders ---
        new("DoubleAP2", "Energy Master II", "Every AP gain is increased by 2.",
            "DoubleAP", "BP_LU_APx2", "DA_GearPictos_DoubleAP",
            new[] { ("DoubleAP2_BonusApAmount", "DoubleAP_BonusApAmount", 2f) }),
        BestDefense(2, 1.25f), BestDefense(3, 1.50f), BestDefense(4, 2.00f), BestDefense(5, 3.00f),
        SameEffect("PowerOfPain2", "Energising Pain II", PainDesc, "PowerOfPain", "BP_LU_PowerOfPain", "DA_GearPictos_PowerOfPain"),
        SameEffect("PowerOfPain3", "Energising Pain III", PainDesc, "PowerOfPain", "BP_LU_PowerOfPain", "DA_GearPictos_PowerOfPain"),
        SameEffect("PowerOfPain4", "Energising Pain IV", PainDesc, "PowerOfPain", "BP_LU_PowerOfPain", "DA_GearPictos_PowerOfPain"),
        SameEffect("PowerOfPain5", "Energising Pain V", PainDesc, "PowerOfPain", "BP_LU_PowerOfPain", "DA_GearPictos_PowerOfPain"),
        FirstOffensive(2, 1.25f), FirstOffensive(3, 1.50f), FirstOffensive(4, 2.00f), FirstOffensive(5, 3.00f),
        SameEffect("AP+1TurnStart2", "Energising Turn II", TurnDesc, "AP+1TurnStart", "BP_LU_AP+1TurnStart", "DA_GearPictos_AP+1TurnStart"),
        SameEffect("AP+1TurnStart3", "Energising Turn III", TurnDesc, "AP+1TurnStart", "BP_LU_AP+1TurnStart", "DA_GearPictos_AP+1TurnStart"),
        EmpoweringJumps(2, 0.25f), EmpoweringJumps(3, 0.50f), EmpoweringJumps(4, 1.00f), EmpoweringJumps(5, 2.00f),
        CritBreak(2, 1.20f), CritBreak(3, 1.30f), CritBreak(4, 1.40f), CritBreak(5, 1.50f),
        BurningDeath(2, 5f), BurningDeath(3, 10f), BurningDeath(4, 20f), BurningDeath(5, 50f),
        EnergyDeath(2, 2f), EnergyDeath(3, 3f), EnergyDeath(4, 4f), EnergyDeath(5, 5f),
        RandomDefense(2), RandomDefense(3), RandomDefense(4), RandomDefense(5),
        ConfidentFighter(2, 0.10f), ConfidentFighter(3, 0.20f), ConfidentFighter(4, 0.50f), ConfidentFighter(5, 1.00f),
        BreakingCounter(2, 1.25f), BreakingCounter(3, 1.50f), BreakingCounter(4, 2.00f), BreakingCounter(5, 3.00f),
        HealingParry(2, 0.05f), HealingParry(3, 0.10f),
        SameEffect("ManOfParry2", "Perilous Parry II", PerilousDesc, "ManOfParry", "BP_LU_ParryApX2DamageReceivedX2", "DA_GearPictos_ManOfParry"),
        SameEffect("ManOfParry3", "Perilous Parry III", PerilousDesc, "ManOfParry", "BP_LU_ParryApX2DamageReceivedX2", "DA_GearPictos_ManOfParry"),
        EmpoweringParry(2, 0.10f), EmpoweringParry(3, 0.15f),
        SameEffect("LuckyAim2", "Lucky Aim II", LuckyDesc, "LuckyAim", "BP_LU_APFreeAim_50Recover", "DA_GearPictos_LuckyAim"),
        SameEffect("LuckyAim3", "Lucky Aim III", LuckyDesc, "LuckyAim", "BP_LU_APFreeAim_50Recover", "DA_GearPictos_LuckyAim"),
        FullStrength(2, 1.10f), FullStrength(3, 1.20f), FullStrength(4, 1.30f), FullStrength(5, 1.50f),
        new("GradientOnCrit2", "Charging Critical II", "20% of a Gradient Charge on Critical hit. Once per turn.",
            "GradientOnCrit", "BP_LU_GradientOnCrit", "DA_GearPictos_GradientOnCrit",
            new[] { ("GradientOnCrit2_GradientChargeOnCrit", "GradientOnCrit_GradientChargeOnCrit", 0.2f) }),
        new("Infinite", "Infinite", "Max Health is set to 3,333,333.",
            "TheOne", "BP_LU_MaxHealth1", "DA_GearPictos_TheOne",
            new[]
            {
                ("Infinite_NewHPValue", "TheOne_NewHPValue", 3333333f),
                ("Infinite_NewMaxHpValue", "TheOne_NewMaxHpValue", 3333333f),
            }),
        new("DeathsDividend", "Death's Dividend", "Deal 10% more damage for each time you have been revived this battle.",
            "Immaculate", "BP_LU_DamageBonusOnRevive", "DA_GearPictos_Immaculate",
            new[] { ("DeathsDividend_DamageIncrease", "DamageBonusOnRevive_DamageIncrease", 0.10f) }),
        CritChain(1, 0.01f),
        CritChain(2, 0.02f),
        CritChain(3, 0.03f),
    };
    // Lumina point costs as requested: Stun I 3 / II 5 / III 7 / IV 10 / V 15,
    // Glass Cannon I 10 / II 20 / III 30, Triple Burn 50, Consuming Attack II 25,
    // Roulette I 5 / II 10 / III 15 / IV 20 / V 25,
    // Augmented Attack 1/5/20, Augmented Aim 1/3/5/10/21, Solo Fighter 1/6/11, Teamwork 5/10/15,
    // Break Damage 10/20/30, Dead Energy 2/7, Dodge Helper 5/10/15, Parry Helper 5/10/15,
    // Parry Specialist 5/10/20, Full Strength I–V 5/10/20/30/50, Auto statuses 10,
    // Dodge Specialist 1, Ultima Power 33.
    private static readonly Dictionary<string, int> LuminaCosts = new(StringComparer.Ordinal)
    {
        ["StunBoost"] = 3, ["StunBoost2"] = 5, ["StunBoost3"] = 7, ["StunBoost4"] = 10, ["StunBoost5"] = 15,
        ["GlassCanon"] = 10, ["GlassCanon2"] = 20, ["GlassCanon3"] = 30,
        ["DoubleBurn2"] = 50,
        ["ConsumingAttack"] = 10, ["ConsumingAttack2"] = 25,
        ["Roulette"] = 5, ["Roulette2"] = 10, ["Roulette3"] = 15, ["Roulette4"] = 20, ["Roulette5"] = 25,
        ["AugmentedAttack"] = 1, ["AugmentedAttack2"] = 5, ["AugmentedAttack3"] = 20,
        ["AugmentedAim"] = 1, ["AugmentedAim2"] = 3, ["AugmentedAim3"] = 5, ["AugmentedAim4"] = 10, ["AugmentedAim5"] = 21,
        ["SoloFighter2"] = 6, ["SoloFighter3"] = 11,
        ["Teamwork2"] = 10, ["Teamwork3"] = 15,
        ["AutoCurse"] = 10, ["AutoBerserk"] = 10, ["AutoBlight"] = 10,
        ["OverPowered"] = 33,
        ["Breaker"] = 10, ["BreakDamage2"] = 20, ["BreakDamage3"] = 30,
        ["DeadEnergyII"] = 7,
        ["LongerDodge"] = 5, ["Dodger2"] = 10, ["Dodger3"] = 15,
        // Dodge Specialist is a tier-I defensive passive (25% smaller dodge window,
        // 50% chance to gain 1 AP on dodge); 15 lumina was overpriced for that trade.
        ["DodgeSpecialist"] = 1,
        ["ParrySpecialist2"] = 10, ["ParrySpecialist3"] = 20,
        ["ParryHelper"] = 5, ["ParryHelper2"] = 10, ["ParryHelper3"] = 15,
        ["TitaniumShell"] = 10, ["TitaniumShell2"] = 20, ["TitaniumShell3"] = 30,
        ["Immaculate"] = 10, ["Immaculate2"] = 15, ["Immaculate3"] = 20, ["Immaculate4"] = 30, ["Immaculate5"] = 40,
        ["AlternatingCritical"] = 5, ["AlternatingCritical2"] = 10, ["AlternatingCritical3"] = 15, ["AlternatingCritical4"] = 20, ["AlternatingCritical5"] = 25,
        ["APDiscount"] = 10, ["APDiscount2"] = 20,
        ["FirstLife"] = 10, ["FirstLife2"] = 15, ["FirstLife3"] = 25,
        ["Augmented1stStrike"] = 10, ["Augmented1stStrike2"] = 15, ["Augmented1stStrike3"] = 20, ["Augmented1stStrike4"] = 30, ["Augmented1stStrike5"] = 40,
        ["Confident"] = 10, ["Confident2"] = 15, ["Confident3"] = 20, ["Confident4"] = 25, ["Confident5"] = 30,
        ["CleasDeath"] = 10, ["CleasDeath2"] = 15, ["CleasDeath3"] = 20,
        ["AttackLifesteal"] = 10, ["AttackLifesteal2"] = 15, ["AttackLifesteal3"] = 20,
        ["GradientOvercharge"] = 10, ["GradientOvercharge2"] = 30, ["GradientOvercharge3"] = 50,
        ["DoubleAP"] = 40, ["DoubleAP2"] = 60,
        ["BestDefense"] = 5, ["BestDefense2"] = 10, ["BestDefense3"] = 20, ["BestDefense4"] = 35, ["BestDefense5"] = 50,
        ["PowerOfPain"] = 10, ["PowerOfPain2"] = 20, ["PowerOfPain3"] = 30, ["PowerOfPain4"] = 40, ["PowerOfPain5"] = 50,
        ["FirstOffensive"] = 5, ["FirstOffensive2"] = 10, ["FirstOffensive3"] = 20, ["FirstOffensive4"] = 35, ["FirstOffensive5"] = 50,
        ["AP+1TurnStart"] = 20, ["AP+1TurnStart2"] = 30, ["AP+1TurnStart3"] = 40,
        ["EmpoweringJumps"] = 5, ["EmpoweringJumps2"] = 10, ["EmpoweringJumps3"] = 20, ["EmpoweringJumps4"] = 35, ["EmpoweringJumps5"] = 50,
        ["BreakDamageOnCrit"] = 5, ["BreakDamageOnCrit2"] = 10, ["BreakDamageOnCrit3"] = 15, ["BreakDamageOnCrit4"] = 20, ["BreakDamageOnCrit5"] = 25,
        ["BurningDeath"] = 5, ["BurningDeath2"] = 10, ["BurningDeath3"] = 15, ["BurningDeath4"] = 25, ["BurningDeath5"] = 40,
        ["EnergyDeath"] = 5, ["EnergyDeath2"] = 10, ["EnergyDeath3"] = 15, ["EnergyDeath4"] = 20, ["EnergyDeath5"] = 25,
        ["RandomDefense"] = 5, ["RandomDefense2"] = 10, ["RandomDefense3"] = 15, ["RandomDefense4"] = 20, ["RandomDefense5"] = 25,
        ["ConfidentFighter"] = 5, ["ConfidentFighter2"] = 10, ["ConfidentFighter3"] = 20, ["ConfidentFighter4"] = 50, ["ConfidentFighter5"] = 100,
        ["BreakingCounter"] = 3, ["BreakingCounter2"] = 8, ["BreakingCounter3"] = 15, ["BreakingCounter4"] = 25, ["BreakingCounter5"] = 40,
        ["HealingParry"] = 5, ["HealingParry2"] = 10, ["HealingParry3"] = 20,
        ["ManOfParry"] = 5, ["ManOfParry2"] = 10, ["ManOfParry3"] = 15,
        ["ReinforcementParade"] = 5, ["ReinforcementParade2"] = 10, ["ReinforcementParade3"] = 15,
        ["LuckyAim"] = 1, ["LuckyAim2"] = 7, ["LuckyAim3"] = 21,
        ["Stand"] = 5, ["Stand2"] = 10, ["Stand3"] = 20, ["Stand4"] = 30, ["Stand5"] = 50,
        ["GradientOnCrit"] = 10, ["GradientOnCrit2"] = 20,
        ["Infinite"] = 10,
        ["DeathsDividend"] = 15,
        ["CriticalChain"] = 10, ["CriticalChain2"] = 20, ["CriticalChain3"] = 40,
    };

    private const string PainDesc = "No longer gain AP on Parry. +1 AP on getting hit.";
    private const string TurnDesc = "+1 AP on turn start.";
    private const string PerilousDesc = "Incoming damage is doubled, but +1 AP on Parry.";
    /// <summary>Lucky Aim's bonus-shot chance: vanilla 30%, retuned to 15% on every tier.</summary>
    private const double LuckyAimVanillaChance = 30;
    private const double LuckyAimChance = 15;
    private const string LuckyDesc = "15% chance to recover 1 AP on Free Aim shot.";
    /// <summary>
    /// Stun Boost uses serialized FDamageModifier type 3 (additive), so wearing
    /// several tiers sums their percentages. The vanilla entry is reworked into
    /// tier I at inject time.
    /// </summary>
    private static Spec Stun(int n, float value) =>
        new("StunBoost" + n, "Stun Boost " + Roman(n),
            $"Deal {value * 100:0}% more damage to Stunned targets.",
            "StunBoost", "BP_LU_StunBoost", "DA_GearPictos_StunBoost",
            new[] { ($"StunBoost{n}_DamageIncrease", "StunBoost_DamageIncrease", value) });

    /// <summary>"+X% increased damage until a hit is received" (Immaculate ladder).</summary>
    private static Spec Immaculate(int tier, float inc) =>
        new("Immaculate" + tier, "Immaculate " + Roman(tier),
            $"{inc * 100:0}% increased damage until a hit is received.",
            "Immaculate", "BP_LU_Immaculate", "DA_GearPictos_Immaculate",
            new[] { ($"Immaculate{tier}_DamageIncrease", "Immaculate_DamageIncrease", inc) });

    /// <summary>"On Critical hit, X% increased damage of the next non-Critical hit".</summary>
    private static Spec AltCrit(int tier, float inc) =>
        new("AlternatingCritical" + tier, "Alternating Critical " + Roman(tier),
            $"On Critical hit, {inc * 100:0}% increased damage of the next non-Critical hit.",
            "AlternatingCritical", "BP_LU_AlternatingCritical", "DA_GearPictos_AlternatingCritical",
            new[] { ($"AlternatingCritical{tier}_DamageIncrease", "AlternatingCritical_DamageIncrease", inc) });

    /// <summary>"X% increased damage until death. Y% decreased damage on death."
    /// The vanilla gear DA for FirstLife is not extractable while the mod is
    /// installed, so the DA clone template is a structurally-identical pictos
    /// gear DA; the clone's row/passive links are rewritten to the new id.</summary>
    private static Spec FirstLife(int tier, float inc, float dec) =>
        new("FirstLife" + tier, "First Life " + Roman(tier),
            $"{inc * 100:0}% increased damage until death. {Math.Abs(dec) * 100:0}% decreased damage on death. (Once)",
            "FirstLife", "BP_LU_FirstLife", "DA_GearPictos_Immaculate",
            new[]
            {
                ($"FirstLife{tier}_DamageIncrease", "FirstLife_DamageIncrease", inc),
                ($"FirstLife{tier}_DamageDecrease", "FirstLife_DamageDecrease", -Math.Abs(dec)),
            });

    /// <summary>"X% increased damage on the first hit. Once per battle."</summary>
    private static Spec AugmentedFirstStrike(int tier, float inc) =>
        new("Augmented1stStrike" + tier, "Augmented First Strike " + Roman(tier),
            $"{inc * 100:0}% increased damage on the first hit. Once per battle.",
            "Augmented1stStrike", "BP_LU_1stAttackBoost", "DA_GearPictos_Augmented1stStrike",
            new[] { ($"Augmented1stStrike{tier}_DamageIncrease", "Augmented1stStrike_DamageIncrease", inc) });

    /// <summary>"Free aim shots deal X% more damage." Flat +10% on every tier.</summary>
    private static Spec AugmentedAim(int tier, float inc) =>
        new("AugmentedAim" + tier, "Augmented Aim " + Roman(tier),
            $"Free aim shots deal {inc * 100:0}% more damage.",
            "AugmentedAim", "BP_LU_FreeAimDamage+25", "DA_GearPictos_AugmentedAim",
            new[] { ($"AugmentedAim{tier}_DamageIncrease", "AugmentedAim_DamageIncrease", inc) });

    /// <summary>"Take X% less damage, but can't be Healed." The donor BP keeps a
    /// 0.5 received-damage DoubleConst in ProcessReceivedRawDamages (no
    /// balancing row); clones are patched to their tier multiplier, and the
    /// vanilla row's staged BP copy is patched to 0.95 for tier I.</summary>
    private static Spec Confident(int tier, float receivedMultiplier) =>
        new("Confident" + tier, "Confident " + Roman(tier),
            $"Take {(1.0f - receivedMultiplier) * 100:0}% less damage, but can't be Healed.",
            "Confident", "BP_LU_CantHealButDamage50percentLess", "DA_GearPictos_Confident",
            Array.Empty<(string Key, string CloneFrom, float Value)>());

    /// <summary>"On death, allies gain X% increased damage until they die."
    /// The effect BP has a companion BP_LU_CleasDeath_Buff that reads the
    /// balancing row; Inject clones both (see CloneCleasBuff). The vanilla gear
    /// DA is not extractable while the mod is installed, so the DA template is
    /// a structurally-identical pictos gear DA (clone links are rewritten).</summary>
    private static Spec CleasDeath(int tier, float inc) =>
        new("CleasDeath" + tier, "Macabre Power " + Roman(tier),
            $"On death, allies gain {inc * 100:0}% increased damage until they die.",
            "CleasDeath", "BP_LU_CleasDeath", "DA_GearPictos_Immaculate",
            new[] { ($"CleasDeath{tier}_DamageIncrease", "CleasDeath_DamageIncrease", inc) });

    /// <summary>"Recover X% Health on Base Attack." (row AttackLifesteal_DamagePercentageToHeal)</summary>
    private static Spec AttackLifesteal(int tier, float pct) =>
        new("AttackLifesteal" + tier, "Attack Lifesteal " + Roman(tier),
            $"Recover {pct * 100:0}% Health on Base Attack.",
            "AttackLifesteal", "BP_LU_BaseAttackLifesteal", "DA_GearPictos_AttackLifesteal",
            new[] { ($"AttackLifesteal{tier}_DamagePercentageToHeal", "AttackLifesteal_DamagePercentageToHeal", pct) });

    /// <summary>"On turn start, consume N Gradient Charges (if able) to deal X% more
    /// damage this turn." The donor reads a charges-consumed row and a damage
    /// multiplier row (vanilla: 3 charges, x3 = "200% more"); tiers II/III each
    /// get their own pair. The vanilla gear DA is not locally extractable, so the
    /// DA clone template is a structurally-identical pictos gear DA.</summary>
    private static Spec GradientOvercharge(int tier, float charges, float multiplier) =>
        new("GradientOvercharge" + tier, "Gradient Overcharge " + Roman(tier),
            $"On turn start, consume {charges:0} Gradient Charges (if able) to deal {(multiplier - 1) * 100:0}% more damage this turn.",
            "GradientOvercharge", "BP_LU_GradientOvercharge", "DA_GearPictos_Immaculate",
            new[]
            {
                ($"GradientOvercharge{tier}_GradientConsumedAmount", "GradientOvercharge_GradientConsumedAmount", charges),
                ($"GradientOvercharge{tier}_DamageMultiplier", "GradientOvercharge_DamageMultiplier", multiplier),
            });

    private static Spec SameEffect(string id, string display, string description, string sourceId, string bp, string da) =>
        new(id, display, description, sourceId, bp, da,
            Array.Empty<(string Key, string CloneFrom, float Value)>());

    private static Spec BestDefense(int tier, float mult) =>
        new("BestDefense" + tier, "The Best Defense " + Roman(tier),
            $"Can't Dodge or Parry. Deal {(mult - 1) * 100:0}% more damage.",
            "BestDefense", "BP_LU_BestDefense", "DA_GearPictos_BestDefense",
            new[] { ($"BestDefense{tier}_DamageMultiplier", "BestDefense_DamageMultiplier", mult) });

    private static Spec FirstOffensive(int tier, float mult) =>
        new("FirstOffensive" + tier, "First Offensive " + Roman(tier),
            $"First hit dealt and taken deals {(mult - 1) * 100:0}% more damage.",
            "FirstOffensive", "BP_LU_FirstOffensive", "DA_GearPictos_firstOffensive",
            new[]
            {
                ($"FirstOffensive{tier}_DamageMultiplierDealtValue", "FirstOffensive_DamageMultiplierDealtValue", mult),
                ($"FirstOffensive{tier}_DamageMultiplierValue", "FirstOffensive_DamageMultiplierValue", mult),
            });

    private static Spec EmpoweringJumps(int tier, float perJump) =>
        new("EmpoweringJumps" + tier, "Empowering Jumps " + Roman(tier),
            $"Counters deal {perJump * 100:0}% more damage for each successful Jump Counter this turn.",
            "EmpoweringJumps", "BP_LU_EmpoweringJumps", "DA_GearPictos_EmpoweringJumps",
            new[] { ($"EmpoweringJumps{tier}_DamageMultiplier", "EmpoweringJumps_DamageMultiplier", perJump) });

    private static Spec CritBreak(int tier, float mult) =>
        new("BreakDamageOnCrit" + tier, "Critical Break " + Roman(tier),
            $"{(mult - 1) * 100:0}% increased Break damage on Critical Hits.",
            "BreakDamageOnCrit", "BP_LU_BreakDamageOnCrit", "DA_GearPictos_BreakDamageOnCrit",
            new[] { ($"BreakDamageOnCrit{tier}_BreakDamageIncrease", "BreakDamageOnCrit_BreakDamageIncrease", mult) });

    private static Spec BurningDeath(int tier, float stacks) =>
        new("BurningDeath" + tier, "Burning Death " + Roman(tier),
            $"Apply {stacks:0} Burn to all enemies on death.",
            "BurningDeath", "BP_LU_BurningDeath", "DA_GearPictos_BurningDeath",
            new[] { ($"BurningDeath{tier}_BurnStackAmount", "BurningDeath_BurnStackAmount", stacks) });

    private static Spec EnergyDeath(int tier, float ap) =>
        new("EnergyDeath" + tier, "Energising Death " + Roman(tier),
            $"+{ap:0} AP to all allies if this character dies.",
            "EnergyDeath", "BP_LU_EnergyDeath", "DA_GearPictos_EnergyDeath",
            new[] { ($"EnergyDeath{tier}_GainApAmount", "EnergyDeath_GainApAmount", ap) });

    private static Spec RandomDefense(int tier) =>
        new("RandomDefense" + tier, "Random Defense " + Roman(tier),
            "Damage taken is randomly multiplied by 50% or 150%.",
            "RandomDefense", "BP_LU_RandomDefense", "DA_GearPictos_RandomDefense",
            new[]
            {
                ($"RandomDefense{tier}_LowMultiplier", "RandomDefense_LowMultiplier", 0.5f),
                ($"RandomDefense{tier}_HighMultiplier", "RandomDefense_HighMultiplier", 1.5f),
            });

    private static Spec ConfidentFighter(int tier, float inc) =>
        new("ConfidentFighter" + tier, "Confident Fighter " + Roman(tier),
            $"{inc * 100:0}% increased damage, but can't be Healed.",
            "ConfidentFighter", "BP_LU_ConfidentFighter", "DA_GearPictos_ConfidentFighter",
            new[] { ($"ConfidentFighter{tier}_DamageIncrease", "ConfidentFighter_DamageIncrease", inc) });

    private static Spec BreakingCounter(int tier, float mult) =>
        new("BreakingCounter" + tier, "Breaking Counter " + Roman(tier),
            $"{(mult - 1) * 100:0}% increased Break damage on Counterattack.",
            "BreakingCounter", "BP_LU_BreakingCounter", "DA_GearPictos_BreakingCounter",
            Array.Empty<(string Key, string CloneFrom, float Value)>());

    private static Spec HealingParry(int tier, float pct) =>
        new("HealingParry" + tier, "Healing Parry " + Roman(tier),
            $"Recover {pct * 100:0}% Health on Parry.",
            "HealingParry", "BP_LU_HealOnParry", "DA_GearPictos_HealingParry",
            Array.Empty<(string Key, string CloneFrom, float Value)>());

    private static Spec EmpoweringParry(int tier, float inc) =>
        new("ReinforcementParade" + tier, "Empowering Parry " + Roman(tier),
            $"Each successful Parry increases damage by {inc * 100:0}% until the end of the following turn. Taking any damage removes this buff.",
            "ReinforcementParade", "BP_LU_ReinforcementParade", "DA_GearPictos_Immaculate",
            new[] { ($"ReinforcementParade{tier}_DamageIncreasePerParry", "ReinforcementParade_DamageIncreasePerParry", inc) });

    /// <summary>Consecutive-Critical-Hit chain cap (stacks).</summary>
    public const int CriticalChainMaxStacks = 100;

    /// <summary>"Each consecutive Critical Hit increases damage by X%. Resets on
    /// non-Critical hit." Cloned from BP_LU_ReinforcementParade (int counter ×
    /// per-event balancing key) with the parry/turn-end/hit-taken paths neutered
    /// and a crit-gated counter update inserted into ProcessDealtRawDamages
    /// (see PatchCriticalChain). The counter is clamped at
    /// <see cref="CriticalChainMaxStacks"/>.</summary>
    private static Spec CritChain(int tier, float perCrit)
    {
        var id = tier == 1 ? "CriticalChain" : "CriticalChain" + tier;
        return new(id, "Critical Chain " + Roman(tier),
            $"Each consecutive Critical Hit increases damage by {perCrit * 100:0}%. Can stack up to {CriticalChainMaxStacks} times. Resets on non-Critical hit.",
            "ReinforcementParade", "BP_LU_ReinforcementParade", "DA_GearPictos_Immaculate",
            new[] { (id + "_DamageIncreasePerCrit", "ReinforcementParade_DamageIncreasePerParry", perCrit) });
    }

    private static Spec FullStrength(int tier, float mult) =>
        new("Stand" + tier, "Full Strength " + Roman(tier),
            $"{(mult - 1) * 100:0}% increased damage on full Health.",
            "Stand", "BP_LU_Stand", "DA_GearPictos_Stand",
            Array.Empty<(string Key, string CloneFrom, float Value)>());

    private static Spec StatusSwap(string id, string display, string sourceId, string da, string bp,
        string[] fromClasses, string[] fromPaths, string statusWord, string description,
        SwapKind kind = SwapKind.BuffSwap) =>
        new(id, display, description, sourceId, bp, da,
            Array.Empty<(string Key, string CloneFrom, float Value)>(),
            kind, statusWord, fromClasses, fromPaths);

    /// <summary>
    /// No balancing rows: no Blueprint reads the *_BuffDuration keys (they are
    /// dead rows in DT_Balancing_Luminas). BP_BattleBuff_AutoApplyBuff copies
    /// its child's BuffTurnDuration onto the spawned buff; children that do not
    /// override it inherit the parent CDO default of 3 turns. Duration variants
    /// therefore patch BuffTurnDuration on the clone's CDO instead.
    /// </summary>
    private static Spec Auto(string src, string buff, string da, string bp, int tier, float turns) =>
        new(src + tier, $"Auto {buff} " + Roman(tier),
            $"Apply {buff} for {turns:0} turns on battle start.",
            src, bp, da,
            Array.Empty<(string Key, string CloneFrom, float Value)>(),
            Turns: turns);

    private static string Roman(int n) => n switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII",
        _ => n.ToString(),
    };

    public static bool HasGeneratedAssets(string modDir) =>
        PictosCreateKit.FindAsset(modDir, "BP_LU_GlassCanon2.uasset") != null;

    /// <summary>
    /// copyTables resets DT_jRPG_Items / DT_PassiveEffects / … to vanilla before
    /// cloning. Pass false when the caller already ran
    /// PictosCreateKit.CopyCreateTables this pack — otherwise every row written
    /// by the painter / status-affinity injects is wiped.
    /// </summary>
    public static int Inject(string modDir, bool copyTables = true)
    {
        if (copyTables)
            PictosCreateKit.CopyCreateTables(modDir);
        int added = 0;
        // Buff-swap clones open their SOURCE from a shared extraction root:
        // unversioned CDOs only parse when the schema-chain parents (Auto
        // apply-buff, SOS base, revive base) sit on disk under one Content
        // root. Isolated copies fall back to raw bytes and the swap would be
        // skipped.
        var swapKinds = Variants.Where(v => v.Kind is SwapKind.BuffSwap or SwapKind.ReviveBase).ToList();
        string? swapRoot = null;
        if (swapKinds.Count > 0)
            swapRoot = PictosCreateKit.EnsureExtractedShared("TriggerFamilies",
                swapKinds.Select(v => v.SourceBp)
                    .Concat(new[] { "BP_BattleBuff_AutoApplyBuff", "BP_LU_SosStatusEffect", "BP_LU_ReviveWithStatusOnAllie" })
                    .Distinct().ToArray());
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var spec in Variants)
        {
            EnsureExtracted(spec.SourceDa);
            var bpDest = Path.Combine(modDir, "Sandfall/Content/Gameplay/Lumina/Passives", "BP_LU_" + spec.Id + ".uasset");
            var daDest = Path.Combine(modDir, "Sandfall/Content/Gameplay/Pictos/Gear/NewLumina", "DA_GearPictos_" + spec.Id + ".uasset");
            if (spec.Kind is SwapKind.BuffSwap or SwapKind.ReviveBase)
            {
                var srcBp = PictosCreateKit.FindAsset(swapRoot!, spec.SourceBp + ".uasset")
                    ?? throw new FileNotFoundException(spec.SourceBp);
                Console.WriteLine($"[swap] {spec.Id} status={spec.SwapToStatus} classes=[{string.Join(",", spec.SwapFromClass ?? Array.Empty<string>())}]");
                var tgt = StatusTarget(spec.SwapToStatus!);
                CloneStatusSwapBlueprint(srcBp, bpDest, "BP_LU_" + spec.Id,
                    spec.SwapFromClass!, spec.SwapFromPath!, tgt);
            }
            else
            {
                EnsureExtracted(spec.SourceBp);
                var srcBp = FileCopy.FindTablesAsset(spec.SourceBp + ".uasset")
                    ?? throw new FileNotFoundException(spec.SourceBp);
                var replace = spec.Balancing.Select(b => (b.CloneFrom, b.Key)).ToList();
                CloneBlueprint(srcBp, bpDest, "BP_LU_" + spec.Id, replace);
                // Every clone inherits the donor CDO's display Name (a
                // StringTableEntry at the source row). The name-map sweep never
                // touches it (no BP_LU_ stem), so proc banners would show the
                // donor's name — e.g. "Gradient Overcharge I" on a III proc.
                RetargetCloneDisplayName(bpDest, spec);
                if (spec.Id.StartsWith("Teamwork", StringComparison.Ordinal))
                {
                    double mult = spec.Id switch { "Teamwork2" => 1.10, "Teamwork3" => 1.15, _ => 1.05 };
                    // Only patch the clones; vanilla rework is handled separately after the loop
                    if (spec.Id is "Teamwork2" or "Teamwork3")
                        PatchTeamworkDamage(bpDest, mult);
                }
            }
            // Per-family post-clone surgery on the cloned BP.
            if (spec.Id.Length > 9 && spec.Id.StartsWith("Confident", StringComparison.Ordinal)
                && spec.Id[9] is >= '2' and <= '5' && spec.Id.Length == 10)
                PatchConstValue(bpDest, "ProcessReceivedRawDamages", 0.5, ConfidentReceived(spec.Id));
            if (spec.Id.StartsWith("CleasDeath", StringComparison.Ordinal))
                CloneCleasBuff(modDir, spec);
            if (spec.Id.StartsWith("BreakingCounter", StringComparison.Ordinal) && spec.Id != "BreakingCounter")
                PatchConstValue(bpDest, "ProcessDealtRawDamages", 1.5, BreakingCounterMult(spec.Id));
            if (spec.Id.StartsWith("Stand", StringComparison.Ordinal) && spec.Id != "Stand")
                PatchConstValue(bpDest, "ProcessDealtRawDamages", 1.25, FullStrengthMult(spec.Id));
            if (spec.Id.StartsWith("RandomDefense", StringComparison.Ordinal) && spec.Id != "RandomDefense")
                PatchRandomDefenseBranch(bpDest);
            if (spec.Id is "HealingParry2" or "HealingParry3")
                PatchConstValue(bpDest, null, 0.03, spec.Id == "HealingParry2" ? 0.05 : 0.10);
            // Gradient Overcharge II/III: the vanilla graph hard-codes the charge
            // count (gate + ConsumeCharge) and never reads the balancing row, so
            // the clone must patch those two IntConsts directly. The proc banner
            // is also shared across tiers, so give each clone its own inline text
            // naming its multiplier (x5 / x11).
            if (spec.Id.StartsWith("GradientOvercharge", StringComparison.Ordinal)
                && spec.Id.Length > "GradientOvercharge".Length)
            {
                var chargeEntry = spec.Balancing.First(
                    b => b.Key.EndsWith("_GradientConsumedAmount", StringComparison.Ordinal));
                var multEntry = spec.Balancing.First(
                    b => b.Key.EndsWith("_DamageMultiplier", StringComparison.Ordinal));
                PatchGradientOverchargeCharges(bpDest, (int)chargeEntry.Value);
                PatchGradientOverchargeTooltipText(bpDest, GradientTooltipLine(multEntry.Value));
            }
            if (spec.Id.StartsWith("CriticalChain", StringComparison.Ordinal))
                PatchCriticalChain(bpDest, CriticalChainMaxStacks);
            // Lucky Aim: the tiers are clones of the same passive, so the
            // bonus-shot roll (RandomFloatInRange(0,100) < 30) is retuned on
            // each clone; the vanilla base is patched in ReworkLuckyAimBase.
            if (spec.Id is "LuckyAim2" or "LuckyAim3")
                PatchConstValue(bpDest, "ProcessAPChange", LuckyAimVanillaChance, LuckyAimChance);
            var srcDa = FileCopy.FindTablesAsset(spec.SourceDa + ".uasset")
                ?? throw new FileNotFoundException(spec.SourceDa);
            CloneGear(srcDa, daDest, "DA_GearPictos_" + spec.Id, spec.RowId);
            // ST entries follow the ROW that exists in the tables. For a
            // repointed variant (RowIdOverride) the visible row keeps its own
            // PASSIVE_<RowId>_* keys; the Id-keyed row was removed.
            var stKey = spec.Id == spec.RowId ? spec.Id : spec.RowId;
            strings[$"PASSIVE_{stKey}_Name"] = spec.DisplayName;
            strings[$"PASSIVE_{stKey}_Description"] = spec.Description;
            added++;
        }
        ReworkVanillaStunBoost(modDir);
        ReworkVanillaGlassCannon(modDir);
        ReworkVanillaRoulette(modDir);
        ReworkVanillaTeamwork(modDir);
        ReworkVanillaParrySpecialist(modDir);
        ReworkNewVanillaBases(modDir);
        ReworkConfidentBase(modDir);
        ReworkBreakingCounterBase(modDir);
        ReworkStandBase(modDir);
        ReworkRandomDefenseBase(modDir);
        ReworkLuckyAimBase(modDir);
        // Rename every ladder's tier-I base to "<Display> I" (must run after the
        // ReworkVanilla* methods above, which still write the plain names).
        ApplyBaseTierRenames(modDir, strings);
        // Vanilla (tier I) rows are reworked to inline FText, but the pictos
        // tooltip reads ST_PassiveEffects directly, so the tier-I keys must be
        // rewritten there too (II/III already get theirs via the loop above).
        strings["PASSIVE_ParrySpecialist_Name"] = "Parry Specialist I";
        strings["PASSIVE_ParrySpecialist_Description"] = "Parry window reduced by 10%. Counterattacks deal double damage.";
        // ReworkVanillaDeadEnergy(modDir); // (not yet implemented; left out so build still passes)
        InjectItemRows(modDir);
        FixRankedWeaponPictosData(modDir);
        InjectPassiveRows(modDir);
        InjectDefinitionRows(modDir);
        InjectBalancing(modDir);
        ReworkGradientOverchargeTooltip(modDir);
        PictosCreateKit.ClonePictoIconRows(modDir, Variants.Select(s => (s.SourceId, s.RowId)));
        var st = PictosCreateKit.FindAsset(modDir, "ST_PassiveEffects.uasset");
        if (st != null) DataTableEditor.AddStringTableEntries(st, strings);
        PinExclusiveDrops(modDir);
        return added;
    }

    public const string InfiniteId = "Infinite";
    public const string InfiniteDropEnemy = "L_Boss_Curator";
    /// <summary>Vanilla Painted Power row; displayed as Ultima Power.</summary>
    public const string UltimaPowerId = "OverPowered";
    public const string UltimaPowerDropEnemy = "MM_MirrorRenoir";

    public static int PinInfiniteLoot(string modDir) => PinExclusiveDrops(modDir);

    /// <summary>
    /// Pins exclusive pictos onto one fight each and strips them from every
    /// other enemy, chest and shop. Infinite → Act 3 Renoir (only when its
    /// assets exist). Ultima Power (OverPowered) → Act 2 Monolith Renoir.
    /// </summary>
    public static int PinExclusiveDrops(string modDir)
    {
        var pins = new List<(string Item, string Enemy)>();
        bool infiniteOn = PictosCreateKit.FindAsset(modDir, "BP_LU_" + InfiniteId + ".uasset") is not null
            || PictosCreateKit.FindAsset(modDir, "DA_GearPictos_" + InfiniteId + ".uasset") is not null;
        if (infiniteOn) pins.Add((InfiniteId, InfiniteDropEnemy));
        pins.Add((UltimaPowerId, UltimaPowerDropEnemy));
        // Special pictos: Alicia's Suffering → Alicia (The Reacher),
        // Layered Canvas → Clea (Flying Manor). Added only when staged.
        if (PictosCreateKit.FindAsset(modDir, "BP_LU_" + SpecialPictosKit.AliciaId + ".uasset") is not null)
            pins.Add((SpecialPictosKit.AliciaId, SpecialPictosKit.AliciaEnemy));
        if (PictosCreateKit.FindAsset(modDir, "BP_LU_" + SpecialPictosKit.LayeredId + "_Burn.uasset") is not null)
            pins.Add((SpecialPictosKit.LayeredId, SpecialPictosKit.LayeredEnemy));
        // Super Sonic → Act 3 Lumière Chromatic Echassier (LU_EchassierAlpha).
        if (PictosCreateKit.FindAsset(modDir, "BP_LU_" + SpecialPictosKit.SuperSonicId + ".uasset") is not null)
            pins.Add((SpecialPictosKit.SuperSonicId, SpecialPictosKit.SuperSonicEnemy));
        // Rupture Shot → Act 3 Lumière Création (L_Creation).
        if (PictosCreateKit.FindAsset(modDir, "BP_LU_" + SpecialPictosKit.RuptureId + ".uasset") is not null)
            pins.Add((SpecialPictosKit.RuptureId, SpecialPictosKit.RuptureEnemy));
        var exclusive = new HashSet<string>(pins.Select(p => p.Item), StringComparer.OrdinalIgnoreCase);
        var byEnemy = pins.ToDictionary(p => p.Enemy, p => p.Item, StringComparer.OrdinalIgnoreCase);

        EnsureExtracted("DT_jRPG_Enemies");
        var src = FileCopy.FindTablesAsset("DT_jRPG_Enemies.uasset")
            ?? throw new FileNotFoundException("DT_jRPG_Enemies");
        var dest = PictosCreateKit.FindAsset(modDir, "DT_jRPG_Enemies.uasset");
        if (dest is null)
        {
            var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
            dest = idx >= 0 ? Path.Combine(modDir, src[idx..]) : Path.Combine(modDir, "DT_jRPG_Enemies.uasset");
            FileCopy.AssetPair(src, dest);
        }
        var ed = new DataTableEditor(dest);
        StructPropertyData? dummy = null;
        foreach (var row in ed.Table.Table.Data)
        {
            if (Named(row.Value, "PossibleLoot") is ArrayPropertyData arr && arr.Value.Length > 0
                && arr.Value[0] is StructPropertyData st)
            {
                dummy = st;
                break;
            }
        }
        if (dummy is null) return 0;
        int changed = 0;
        foreach (var row in ed.Table.Table.Data)
        {
            var id = DataTableEditor.CleanName(row.Name);
            if (Named(row.Value, "PossibleLoot") is not ArrayPropertyData loot) continue;
            var kept = loot.Value.OfType<StructPropertyData>()
                .Where(e =>
                {
                    var item = DropItemId(e);
                    return item is null || !exclusive.Contains(item);
                })
                .Cast<PropertyData>().ToList();
            bool dirty = kept.Count != loot.Value.Length;
            if (byEnemy.TryGetValue(id, out var pin))
            {
                if (Named(row.Value, "LootChanceMultiplier") is DoublePropertyData d && d.Value < 1)
                { d.Value = 1; dirty = true; }
                if (Named(row.Value, "LootChanceMultiplier") is FloatPropertyData f && f.Value < 1)
                { f.Value = 1; dirty = true; }
                if (!kept.OfType<StructPropertyData>().Any(e =>
                        string.Equals(DropItemId(e), pin, StringComparison.OrdinalIgnoreCase)))
                {
                    kept.Add(CloneDrop(ed.Asset, dummy, pin));
                    dirty = true;
                }
            }
            if (!dirty) continue;
            var neu = (ArrayPropertyData)loot.Clone();
            neu.Value = kept.ToArray();
            var at = row.Value.IndexOf(loot);
            if (at >= 0) row.Value[at] = neu;
            changed++;
        }
        if (changed > 0) ed.Save(dest);
        changed += StripIdsFromChests(modDir, exclusive);
        changed += StripIdsFromMerchants(modDir, exclusive);
        return changed;
    }

    private static StructPropertyData CloneDrop(UAsset asset, StructPropertyData dummy, string itemId)
    {
        asset.AddNameReference(FString.FromString(itemId));
        var drop = (StructPropertyData)dummy.Clone();
        if (Named(drop.Value, "Item") is StructPropertyData item
            && Named(item.Value, "RowName") is NamePropertyData nm)
            nm.Value = new FName(asset, itemId);
        if (Named(drop.Value, "Quantity") is IntPropertyData q) q.Value = 1;
        if (Named(drop.Value, "LootPercentageChance") is DoublePropertyData d) d.Value = 100;
        else if (Named(drop.Value, "LootPercentageChance") is FloatPropertyData f) f.Value = 100;
        return drop;
    }

    private static string? DropItemId(StructPropertyData drop)
    {
        if (Named(drop.Value, "Item") is not StructPropertyData item) return null;
        return Named(item.Value, "RowName") is NamePropertyData nm ? nm.Value?.ToString() : null;
    }

    private static int StripIdsFromChests(string modDir, HashSet<string> ids)
    {
        var path = PictosCreateKit.FindAsset(modDir, "DT_ChestsContent.uasset");
        if (path is null) return 0;
        var ed = new DataTableEditor(path);
        int changed = 0;
        foreach (var row in ed.Table.Table.Data)
        {
            if (Named(row.Value, "LootTableSections") is not ArrayPropertyData secs) continue;
            foreach (var sec in secs.Value.OfType<StructPropertyData>())
            {
                if (Named(sec.Value, "LootEntries") is not ArrayPropertyData ents) continue;
                var kept = ents.Value.Where(e =>
                {
                    if (e is not StructPropertyData st) return true;
                    var id = Named(st.Value, "ItemID") is NamePropertyData nm ? nm.Value?.ToString() : "";
                    return id is null || !ids.Contains(id);
                }).ToArray();
                if (kept.Length == ents.Value.Length) continue;
                var neu = (ArrayPropertyData)ents.Clone();
                neu.Value = kept;
                var at = sec.Value.IndexOf(ents);
                if (at >= 0) sec.Value[at] = neu;
                changed++;
            }
        }
        if (changed > 0) ed.Save(path);
        return changed;
    }

    private static int StripIdsFromMerchants(string modDir, HashSet<string> ids)
    {
        if (!Directory.Exists(modDir)) return 0;
        int changed = 0;
        foreach (var path in Directory.EnumerateFiles(modDir, "DT_Merchant_*.uasset", SearchOption.AllDirectories))
        {
            var ed = new DataTableEditor(path);
            var n = ed.Table.Table.Data.RemoveAll(r => ids.Contains(DataTableEditor.CleanName(r.Name)));
            if (n <= 0) continue;
            ed.Save(path);
            changed += n;
        }
        return changed;
    }

    private static PropertyData? Named(IList<PropertyData> fields, string name) =>
        fields.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == name);

    public static void Remove(string modDir)
    {
        if (!Directory.Exists(modDir)) return;
        foreach (var spec in Variants)
        {
            foreach (var name in new[]
                     {
                         "BP_LU_" + spec.Id + ".uasset", "BP_LU_" + spec.Id + ".uexp",
                         "DA_GearPictos_" + spec.Id + ".uasset", "DA_GearPictos_" + spec.Id + ".uexp",
                     })
            {
                var hit = PictosCreateKit.FindAsset(modDir, name);
                if (hit != null) File.Delete(hit);
            }
        }
    }
    private static void FixRankedWeaponPictosData(string modDir)
    {
        // Ranked weapons (Reach*/Siren* _1/_2) ship vanilla Pictos_Data whose
        // import is an FName-NUMBERED quirk, e.g. raw name
        // "DA_WeaponPictos_Maelle_AS_Reachum" with number 2: it RENDERS as
        // "..._Reachum_2" (so every string comparison passes) but FName-exact
        // resolution fails because the true asset is ("..._Reachum_2", 0).
        // Only rank-suffixed DAs exist in the pak (verified via findassets).
        // Merchants resolve the weapon through that reference and render "?"
        // when it dangles, so mint true number-0 imports and repoint each
        // staged row. (A soft-reference branch is kept for rows that
        // deserialize as SoftObjectPropertyData instead.)
        foreach (var table in new[] { "DT_jRPG_Items", "DT_jRPG_Items_Composite" })
        {
            var path = PictosCreateKit.FindAsset(modDir, table + ".uasset");
            if (path == null) continue;
            var ed = new DataTableEditor(path);
            bool dirty = false;
            foreach (var row in ed.Table.Table.Data)
            {
                var id = DataTableEditor.CleanName(row.Name);
                if (!RankedWeaponRank.TryGetValue(id, out var rank)) continue;
                if (!ed.TryGetRow(id, out var target)) continue;
                var pic = target.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == "Pictos_Data");
                if (pic is SoftObjectPropertyData soft)
                {
                    var cur = soft.Value.ToString();
                    if (!cur.EndsWith("_" + rank, StringComparison.Ordinal))
                    {
                        var sp = soft.Value;
                        if (SuffixSoftName(ed.Asset, ref sp.AssetPath.AssetName, cur, rank)
                            || SuffixSoftName(ed.Asset, ref sp.AssetPath.PackageName, cur, rank))
                        {
                            soft.Value = sp;
                            dirty = true;
                        }
                    }
                    continue;
                }
                if (pic is not ObjectPropertyData o || !o.Value.IsImport()) continue;
                var imp = ed.Asset.Imports[Math.Abs(o.Value.Index) - 1];
                var rendered = imp.ObjectName.ToString();
                var raw = imp.ObjectName.Value.Value;
                if (rendered == raw) continue; // genuine number-0 name
                if (!rendered.EndsWith("_" + rank, StringComparison.Ordinal)) continue;
                if (!imp.OuterIndex.IsImport()) continue;
                var pkgImp = ed.Asset.Imports[Math.Abs(imp.OuterIndex.Index) - 1];
                if (pkgImp.ClassName.ToString() != "Package") continue;
                var pkgRendered = pkgImp.ObjectName.ToString();
                var className = imp.ClassName.ToString();
                var classPkg = "/Script/Engine";
                FPackageIndex? foundPkg = null;
                FPackageIndex? foundObj = null;
                string? ciPkg = null;
                for (int i = 0; i < ed.Asset.Imports.Count; i++)
                {
                    var c = ed.Asset.Imports[i];
                    // Compare RAW names: ToString() lies for numbered FNames.
                    if (c.ClassName.ToString() == "Package"
                        && c.ObjectName.Value.Value == pkgRendered)
                        foundPkg ??= FPackageIndex.FromImport(i);
                    if (c.ObjectName.Value.Value == rendered
                        && c.ClassName.ToString() == className)
                        foundObj ??= FPackageIndex.FromImport(i);
                    if (c.ObjectName.ToString() == className && ciPkg == null
                        && c.OuterIndex.IsImport())
                        ciPkg = ed.Asset.Imports[Math.Abs(c.OuterIndex.Index) - 1].ObjectName.ToString();
                }
                if (ciPkg != null) classPkg = ciPkg;
                foundPkg ??= ed.Asset.AddImport(new Import("/Script/CoreUObject", "Package",
                    FPackageIndex.FromRawIndex(0), pkgRendered, false, ed.Asset));
                foundObj ??= ed.Asset.AddImport(new Import(classPkg, className,
                    foundPkg, rendered, false, ed.Asset));
                o.Value = foundObj;
                dirty = true;
            }
            if (dirty) ed.Save(path);
        }
    }

    private static bool SuffixSoftName(UAsset asset, ref FName field, string cur, string rank)
    {
        var s = field.Value.Value;
        if (s == cur || s.EndsWith("/" + cur, StringComparison.Ordinal)
            || s.EndsWith("." + cur, StringComparison.Ordinal)
            || s.EndsWith(":" + cur, StringComparison.Ordinal))
        {
            field = new FName(asset, s + "_" + rank);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Ranked weapons whose vanilla item identity is family-level
    /// (HardcodedName/DefinitionID carry the bare family while the row id is
    /// rank-suffixed). Merchants render these as "?" even with valid names,
    /// prices and pictos references, so shops must never stock them. Chests,
    /// drops and boss guarantees reference them through
    /// <see cref="LootReferenceFor"/> (the bare family name), which is what
    /// vanilla loot does and what actually resolves and grants in game.
    /// </summary>
    internal static bool IsQuirkedRankedWeapon(string id) => RankedWeaponRank.ContainsKey(id);

    /// <summary>
    /// Loot reference for an item id: ranked weapons are renamed to their bare
    /// family (Reachum_2 → Reachum). Vanilla loot always names a weapon by its
    /// family — the rank-suffixed rows carry family-level
    /// <c>Item_HardcodedName</c> and a <c>Pictos_Data</c> reference that does
    /// not survive packing, so a suffixed loot entry renders "?" in the loot
    /// popup and never lands in inventory. Every other id passes through.
    /// </summary>
    internal static string LootReferenceFor(string id) =>
        RankedWeaponRank.TryGetValue(id, out var rank)
            ? id[..^(rank.Length + 1)]
            : id;

    private static readonly Dictionary<string, string> RankedWeaponRank = new(StringComparer.Ordinal)
    {
        ["Reacharo_1"] = "1", ["Reacharo_2"] = "2",
        ["Reacherim_1"] = "1", ["Reacherim_2"] = "2",
        ["Reacheron_1"] = "1", ["Reacheron_2"] = "2",
        ["Reacheso_1"] = "1", ["Reacheso_2"] = "2",
        ["Reachum_1"] = "1", ["Reachum_2"] = "2",
        ["Sirenaro_1"] = "1", ["Sirenaro_2"] = "2",
        ["Sirenim_1"] = "1", ["Sirenim_2"] = "2",
        ["Sirenon_1"] = "1", ["Sirenon_2"] = "2",
        ["Sirenum_1"] = "1", ["Sirenum_2"] = "2",
        ["Sireso_1"] = "1", ["Sireso_2"] = "2",
    };

    private static void InjectItemRows(string modDir)
    {
        foreach (var table in new[] { "DT_jRPG_Items", "DT_jRPG_Items_Composite" })
        {
            var path = PictosCreateKit.FindAsset(modDir, table + ".uasset")
                ?? throw new FileNotFoundException(table);
            var ed = new DataTableEditor(path);
            foreach (var spec in Variants)
            {
                var rowId = spec.RowId;
                if (spec.Id != rowId && ed.HasRow(spec.Id)) ed.Table.Table.Data.RemoveAll(r => DataTableEditor.CleanName(r.Name) == spec.Id);
                if (!ed.HasRow(rowId)) ed.CloneRow(spec.SourceId, rowId);
                RewriteItem(ed, spec);
            }
            ed.Save(path);
        }
    }

    private static void RewriteItem(DataTableEditor ed, Spec spec)
    {
        if (!ed.TryGetRow(spec.RowId, out var row)) return;
        foreach (var f in row.Value)
        {
            var n = DataTableEditor.CleanName(f.Name);
            if (n == "Item_HardcodedName" && f is NamePropertyData nm)
                nm.Value = new FName(ed.Asset, spec.RowId);
            else if (n == "Item_DisplayName" && f is TextPropertyData t)
                SetLoc(t, $"PASSIVE_{spec.RowId}_Name", spec.DisplayName);
            else if (n == "ItemDescription" && f is TextPropertyData d)
                SetLoc(d, $"PASSIVE_{spec.RowId}_Description", spec.Description);
            else if (n == "Pictos_Data" && f is ObjectPropertyData o)
                o.Value = EnsureObjectImport(ed.Asset,
                    "/Game/Gameplay/Pictos/Gear/NewLumina/DA_GearPictos_" + spec.Id,
                    "DA_GearPictos_" + spec.Id,
                    "BP_DataAsset_Item_Pictos_C",
                    // ClassPackage must be the package that DEFINES the class;
                    // "/Script/Engine" is a fake Engine class and leaves the
                    // Pictos_Data hard ref unresolved (tooltip goes stale).
                    "/Game/Gameplay/Inventory/BP_DataAsset_Item_Pictos");
        }
    }

    private static void InjectPassiveRows(string modDir)
    {
        var path = PictosCreateKit.FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? throw new FileNotFoundException("DT_PassiveEffects");
        var ed = new DataTableEditor(path);
        foreach (var spec in Variants)
        {
            var rowId = spec.RowId;
            if (spec.Id != rowId && ed.HasRow(spec.Id)) ed.Table.Table.Data.RemoveAll(r => DataTableEditor.CleanName(r.Name) == spec.Id);
            if (!ed.HasRow(rowId)) ed.CloneRow(spec.SourceId, rowId);
            if (!ed.TryGetRow(rowId, out var row)) continue;
            foreach (var f in row.Value.ToList())
            {
                var n = DataTableEditor.CleanName(f.Name);
                if (n == "Name" && f is TextPropertyData t)
                    SetLoc(t, $"PASSIVE_{spec.RowId}_Name", spec.DisplayName);
                else if (n == "Description" && f is TextPropertyData d)
                    SetLoc(d, $"PASSIVE_{spec.RowId}_Description", spec.Description);
                else if (n == "LuminaCost" && f is IntPropertyData c)
                {
                    if (LuminaCosts.TryGetValue(spec.Id, out var cost)) c.Value = cost;
                }
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
                    // E33 tables ship an EMPTY-but-non-null SoftObjectPathList;
                    // adding an entry flips UAssetAPI's writer into index
                    // serialization and every untouched inline path then fails
                    // lookup ("Failed to find AssetPath"). Only register when
                    // the table actually uses the list.
                    if (ed.Asset.SoftObjectPathList is { Count: > 0 } pathList)
                        pathList.Add(pathRef);
                }
            }
        }
        ed.Save(path);
    }

    private static void InjectDefinitionRows(string modDir)
    {
        var path = PictosCreateKit.FindAsset(modDir, "DT_PictosDefinitions.uasset")
            ?? throw new FileNotFoundException("DT_PictosDefinitions");
        var ed = new DataTableEditor(path);
        foreach (var spec in Variants)
        {
            var rowId = spec.RowId;
            if (spec.Id != rowId && ed.HasRow(spec.Id)) ed.Table.Table.Data.RemoveAll(r => DataTableEditor.CleanName(r.Name) == spec.Id);
            if (!ed.HasRow(rowId)) ed.CloneRow(spec.SourceId, rowId);
            if (!ed.TryGetRow(rowId, out var row)) continue;
            foreach (var f in row.Value)
                if (DataTableEditor.CleanName(f.Name) == "ItemID" && f is NamePropertyData nm)
                    nm.Value = new FName(ed.Asset, rowId);
        }
        ed.Save(path);
    }

    private static void InjectBalancing(string modDir)
    {
        var path = PictosCreateKit.FindAsset(modDir, "DT_Balancing_Luminas.uasset")
            ?? throw new FileNotFoundException("DT_Balancing_Luminas");
        var ed = new DataTableEditor(path);
        var edits = new JObject();
        foreach (var spec in Variants)
        {
            foreach (var b in spec.Balancing)
            {
                if (!ed.HasRow(b.Key)) ed.CloneRow(b.CloneFrom, b.Key);
                edits[b.Key] = new JObject { ["FloatValue"] = b.Value };
            }
        }

        // Vanilla Stun Boost becomes tier I of the new ladder.
        edits["StunBoost_DamageIncrease"] = new JObject { ["FloatValue"] = 0.10f };
        // Roulette: all three levels 50%/150% (0.5x / 1.5x) as requested.
        edits["Roulette_HighValue"] = new JObject { ["FloatValue"] = 1.5f };
        edits["Roulette_LowValue"] = new JObject { ["FloatValue"] = 0.5f };
        // Parry Specialist (vanilla, reworked into tier I of the Parry Specialist
        // ladder): -10% parry window and x2 counter damage. The BP builds an
        // FDamageModifier with serialized type 2 (direct multiplier), so 2.0 is
        // the true x2; 1.0 would remove the counter bonus entirely.
        edits["ParrySpecialist_ParryWindowDecrease"] = new JObject { ["FloatValue"] = -0.10f };
        edits["ParrySpecialist_CounterDamageMultiplier"] = new JObject { ["FloatValue"] = 2.0f };
        // Helper tier-I vanilla rows retuned to the ladder value (+22% each, so
        // base 0.75x vanilla + all three helpers lands at ~1.25x vanilla).
        edits["LongerDodge_DodgeWindowIncrease"] = new JObject { ["FloatValue"] = 0.22f };
        edits["ParryHelper_ParryWindowIncrease"] = new JObject { ["FloatValue"] = 0.22f };
        // New families (2026-09): vanilla tier-I rows retuned to their ladder values.
        edits["Immaculate_DamageIncrease"] = new JObject { ["FloatValue"] = 0.10f };
        edits["AlternatingCritical_DamageIncrease"] = new JObject { ["FloatValue"] = 0.10f };
        edits["FirstLife_DamageIncrease"] = new JObject { ["FloatValue"] = 0.05f };
        edits["FirstLife_DamageDecrease"] = new JObject { ["FloatValue"] = -0.05f };
        edits["Augmented1stStrike_DamageIncrease"] = new JObject { ["FloatValue"] = 0.05f };
        // Augmented Attack: tier-I vanilla row retuned to the +10% ladder value.
        edits["AugmentedAttack_DamageIncrease"] = new JObject { ["FloatValue"] = 0.10f };
        // Augmented Aim: tier-I vanilla row retuned to the flat +10% ladder value.
        edits["AugmentedAim_DamageIncrease"] = new JObject { ["FloatValue"] = 0.10f };
        edits["CleasDeath_DamageIncrease"] = new JObject { ["FloatValue"] = 0.05f };
        edits["AttackLifesteal_DamagePercentageToHeal"] = new JObject { ["FloatValue"] = 0.05f };
        edits["BestDefense_DamageMultiplier"] = new JObject { ["FloatValue"] = 1.10f };
        edits["FirstOffensive_DamageMultiplierDealtValue"] = new JObject { ["FloatValue"] = 1.10f };
        edits["FirstOffensive_DamageMultiplierValue"] = new JObject { ["FloatValue"] = 1.10f };
        edits["EmpoweringJumps_DamageMultiplier"] = new JObject { ["FloatValue"] = 0.10f };
        edits["BreakDamageOnCrit_BreakDamageIncrease"] = new JObject { ["FloatValue"] = 1.10f };
        edits["EnergyDeath_GainApAmount"] = new JObject { ["FloatValue"] = 1f };
        edits["RandomDefense_HighMultiplier"] = new JObject { ["FloatValue"] = 1.5f };
        edits["ConfidentFighter_DamageIncrease"] = new JObject { ["FloatValue"] = 0.05f };
        if (edits.Count > 0) ed.Apply(edits.ToString());
        ed.Save(path);
    }

    // ── Gradient Overcharge battle tooltip ─────────────────────────────────
    // The proc tooltip key BATTLETOOLTIP_GradientOvercharge is shared by every
    // tier (the clone sweep retargets the BP stem and the DamageMultiplier row,
    // never the StringTable key), and its vanilla text claims "deals double
    // damage" — wrong for the reworked ladder (I x3, II x5, III x11).
    //
    // Tier I is the unstaged vanilla BP and reads this key, so rewrite the
    // existing entry to its multiplier. Tiers II/III are cloned, so each one
    // gets its own inline literal (PatchGradientOverchargeTooltipText) naming
    // its multiplier; that avoids both a shared key and any new ST key, which
    // are not guaranteed to resolve in the cooked locres.
    private const string GradientTooltipKey = "BATTLETOOLTIP_GradientOvercharge";

    /// <summary>The proc banner text, e.g. "{skill}: {char} deals 1000% more damage this turn."</summary>
    private static string GradientTooltipLineEnglish(double multiplier) =>
        $"{{skill}}: {{char}} deals {(multiplier - 1) * 100:0}% more damage this turn.";

    private static string GradientTooltipLine(double multiplier)
    {
        var percent = (multiplier - 1) * 100;
        var template = ModLocalization.Inline(
            "BATTLETOOLTIP_GradientOvercharge_Template",
            "{skill}: {char} deals {value}% more damage this turn.");
        return template.Replace("{value}", percent.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static int ReworkGradientOverchargeTooltip(string modDir)
    {
        EnsureExtracted("ST_BattleTooltips");
        var src = FileCopy.FindTablesAsset("ST_BattleTooltips.uasset");
        if (src is null)
        {
            Console.WriteLine("[GradientOvercharge] ST_BattleTooltips not found; tier-I battle tooltip left vanilla");
            return 0;
        }
        var dest = PictosCreateKit.FindAsset(modDir, "ST_BattleTooltips.uasset");
        if (dest is null)
        {
            var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
            dest = idx >= 0 ? Path.Combine(modDir, src[idx..]) : Path.Combine(modDir, Path.GetFileName(src));
            FileCopy.AssetPair(src, dest);
        }
        var text = GradientTooltipLineEnglish(3.0); // vanilla tier-I multiplier; locres translates it
        DataTableEditor.AddStringTableEntries(dest, new Dictionary<string, string>
        {
            [GradientTooltipKey] = text,
        });
        Console.WriteLine($"[GradientOvercharge] tier-I battle tooltip -> \"{text}\"");
        return 1;
    }

    /// <summary>
    /// Opens the source Blueprint from the shared TriggerFamilies root (schema
    /// parents alongside, so the CDO parses), retargets it in memory, and
    /// swaps every buff-import string to the target status. The sources spawn
    /// buffs via SandfallBlueprintHelpers using imported class objects — no
    /// cast nodes — so class FName + package path replacements retarget them
    /// completely. The revive base carries five per-vanilla-variant imports;
    /// all of them collapse onto the one target status.
    /// </summary>
    private static void CloneStatusSwapBlueprint(string srcBp, string destUasset, string newStem,
        string[] fromClasses, string[] fromPaths, (string Cls, string Path) target)
    {
        var asset = PictosCreateKit.OpenAsset(srcBp);
        Retarget(asset, destUasset, Path.GetFileNameWithoutExtension(srcBp), newStem);
        // Rewrite the outer Package FName for every matching class import
        // BEFORE the name-map sweep mutates the class FNames. The sweep
        // replaces e.g. BP_BattleBuff_Speed with BP_BattleBuff_Berserk
        // everywhere in the name map; after that, BP_BattleBuff_Speed_C is
        // gone and we can no longer match imports against fromClasses. Doing
        // the import retarget first keeps the original class FNames intact
        RetargetBuffImports(asset, fromClasses, fromPaths, target.Cls, target.Path);
        // Replace longest strings first. The fromClass is a substring of the
        // fromPath (BP_BattleBuff_Powerful_125 ⊂ …/BP_BattleBuff_Powerful_125),
        // so replacing the shorter class first would corrupt the path (it would
        // rewrite the tail and leave BP_BattleBuff_<Status>_125, a class that
        // does not exist). Paths first, longest-first, keeps both intact.
        var ordered = fromPaths.Select(p => (From: p, To: target.Path))
            .Concat(fromClasses.Select(c => (From: c, To: target.Cls)))
            .Where(r => !string.Equals(r.From, r.To, StringComparison.Ordinal))
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

    /// <summary>
    /// Rewrites the outer Package FName for every import whose ObjectName
    /// matches one of <paramref name="fromClasses"/> + "_C" and whose outer
    /// matches the corresponding fromPath.
    /// The status stat-buff variants (e.g. BP_BattleBuff_Powerful_125) store
    /// the "_125" as the FName <see cref="FName.Number"/> suffix, not in the
    /// Value string, so a name-map string sweep can never match it. We set
    /// the ObjectName Value AND clear Number here, which is the only reliable
    /// way to re-point an import at another package/class.
    /// </summary>
    private static void RetargetBuffImports(UAsset asset, string[] fromClasses, string[] fromPaths,
        string toClass, string toPath)
    {
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            var className = imp.ObjectName?.ToString() ?? "";
            string? matchedFromClass = null;
            foreach (var fc in fromClasses)
                if (className == fc + "_C") { matchedFromClass = fc; break; }
            if (matchedFromClass is null) continue;
            if (!imp.OuterIndex.IsImport()) continue;
            var outer = imp.OuterIndex.ToImport(asset);
            if (outer is null) continue;
            var outerName = outer.ObjectName?.ToString() ?? "";
            bool outerMatches = false;
            foreach (var fp in fromPaths)
                if (outerName == fp) { outerMatches = true; break; }
            if (!outerMatches) continue;
            imp.ObjectName.Value = new FString(toClass + "_C");
            imp.ObjectName.Number = 0;
            outer.ObjectName.Value = new FString(toPath);
            outer.ObjectName.Number = 0;
        }
    }

    private static readonly Dictionary<string, (string Cls, string Path)> StatusTargets =
        new(StringComparer.Ordinal)
        {
            ["Cursed"] = ("BP_BattleBuff_Cursed", "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Cursed"),
            ["Berserked"] = ("BP_BattleBuff_Berserk", "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Berserk"),
            ["Blighted"] = ("BP_BattleBuff_Blight", "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Blight"),
            // singular aliases: the Auto* variants pass unprefixed status words
            ["Curse"] = ("BP_BattleBuff_Cursed", "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Cursed"),
            ["Berserk"] = ("BP_BattleBuff_Berserk", "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Berserk"),
            ["Blight"] = ("BP_BattleBuff_Blight", "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Blight"),
        };

    private static (string Cls, string Path) StatusTarget(string statusWord) =>
        StatusTargets.TryGetValue(statusWord, out var hit)
            ? hit
            : throw new KeyNotFoundException("no buff mapping for status " + statusWord);

    /// <summary>
    /// Tier-I base rows of picto ladders are renamed "<Display> I" so the base
    /// reads consistently with the II/III created tiers. Each base is the
    /// vanilla row whose II/III variants clone from it; the vanilla row's FText
    /// is converted to culture-invariant inline text (the tooltip reads the row
    /// directly) and the matching ST_PassiveEffects keys are overridden too.
    /// </summary>
    private static readonly (string Row, string Name, string? Description)[] BaseTierRenames =
    {
        ("AugmentedAttack", "Augmented Attack I", "10% increased Base Attack damage."),
        ("AugmentedAim", "Augmented Aim I", "Free aim shots deal 10% more damage."),
        ("GlassCanon", "Glass Cannon I", null),
        ("Roulette", "Roulette I", "Every hit has a 50% chance to deal either 50% or 150% of its damage."),
        ("SoloFighter", "Solo Fighter I", null),
        ("Teamwork", "Teamwork I", "Deal 5% more damage while all allies are alive and you are not alone."),
        ("CursedPower", "Cursed Power I", "Deal 5% more damage while Cursed."),
        ("Breaker", "Break Damage I", null),
        ("ConsumingAttack", "Consuming Attack I", null),
        ("BurnDurationIncrease", "Longer Burn I", null),
        ("LongerDodge", "Dodge Helper I", "Dodge window increased by 22%."),
        ("ParryHelper", "Parry Helper I", "Parry window increased by 22%."),
        ("StunBoost", "Stun Boost I", "Deal 10% more damage to Stunned targets."),
        // New families (2026-09): vanilla rows reworked into tier I. Rows whose
        // tier-I value was retuned carry the matching description; rename-only
        // bases pass null and keep their vanilla text.
        ("Immaculate", "Immaculate I", "10% increased damage until a hit is received."),
        ("AlternatingCritical", "Alternating Critical I", "On Critical hit, 10% increased damage of the next non-Critical hit."),
        ("APDiscount", "AP Discount I", null),
        ("FirstLife", "First Life I", "5% increased damage until death. 5% decreased damage on death. (Once)"),
        ("Augmented1stStrike", "Augmented First Strike I", "5% increased damage on the first hit. Once per battle."),
        // Rename-only bases whose tier-II+ ladder needs donor assets that are
        // not extractable while the mod is installed (At Death's Door, Beneficial
        // Contamination); tier-I keeps the vanilla effect exactly.
        ("LastStand", "At Death's Door I", null),
        ("BeneficialContamination", "Beneficial Contamination I", null),
        ("GradientOvercharge", "Gradient Overcharge I", null),
        ("CleasDeath", "Macabre Power I", "On death, allies gain 5% increased damage until they die."),
        ("AttackLifesteal", "Attack Lifesteal I", "Recover 5% Health on Base Attack."),
        ("Confident", "Confident I", "Take 5% less damage, but can't be Healed."),
        ("DoubleAP", "Energy Master I", "Every AP gain is increased by 1."),
        ("BestDefense", "The Best Defense I", "Can't Dodge or Parry. Deal 10% more damage."),
        ("PowerOfPain", "Energising Pain I", PainDesc),
        ("FirstOffensive", "First Offensive I", "First hit dealt and taken deals 10% more damage."),
        ("AP+1TurnStart", "Energising Turn I", TurnDesc),
        ("EmpoweringJumps", "Empowering Jumps I", "Counters deal 10% more damage for each successful Jump Counter this turn."),
        ("BreakDamageOnCrit", "Critical Break I", "10% increased Break damage on Critical Hits."),
        ("BurningDeath", "Burning Death I", "Apply 3 Burn to all enemies on death."),
        ("EnergyDeath", "Energising Death I", "+1 AP to all allies if this character dies."),
        ("RandomDefense", "Random Defense I", "Damage taken is randomly multiplied by 50% or 150%."),
        ("ConfidentFighter", "Confident Fighter I", "5% increased damage, but can't be Healed."),
        ("BreakingCounter", "Breaking Counter I", "10% increased Break damage on Counterattack."),
        ("HealingParry", "Healing Parry I", "Recover 3% Health on Parry."),
        ("ManOfParry", "Perilous Parry I", PerilousDesc),
        ("ReinforcementParade", "Empowering Parry I", "Each successful Parry increases damage by 5% until the end of the following turn. Taking any damage removes this buff."),
        ("LuckyAim", "Lucky Aim I", LuckyDesc),
        ("Stand", "Full Strength I", "5% increased damage on full Health."),
        ("GradientOnCrit", "Charging Critical I", "+20% of a Gradient Charge on Critical hit. Once per turn."),
    };

    private static void ApplyBaseTierRenames(string modDir, Dictionary<string, string> strings)
    {
        var itemsPath = PictosCreateKit.FindAsset(modDir, "DT_jRPG_Items.uasset")
            ?? throw new FileNotFoundException("DT_jRPG_Items");
        var pePath = PictosCreateKit.FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? throw new FileNotFoundException("DT_PassiveEffects");
        foreach (var (rowId, name, desc) in BaseTierRenames)
        {
            var items = new DataTableEditor(itemsPath);
            if (items.TryGetRow(rowId, out var itemRow))
            {
                PictosCreateKit.SetRowText(items, itemRow, "Item_DisplayName",
                    "PASSIVE_" + rowId + "_Name", name);
                if (desc != null)
                    PictosCreateKit.SetRowText(items, itemRow, "ItemDescription",
                        "PASSIVE_" + rowId + "_Description", desc);
                items.Save(itemsPath);
            }
            var pe = new DataTableEditor(pePath);
            if (pe.TryGetRow(rowId, out var peRow))
            {
                PictosCreateKit.SetRowText(pe, peRow, "Name", "PASSIVE_" + rowId + "_Name", name);
                if (desc != null)
                    PictosCreateKit.SetRowText(pe, peRow, "Description",
                        "PASSIVE_" + rowId + "_Description", desc);
                pe.Save(pePath);
            }
            strings["PASSIVE_" + rowId + "_Name"] = name;
            if (desc != null) strings["PASSIVE_" + rowId + "_Description"] = desc;
        }
    }

    /// <summary>
    /// Applies the tier-I Lumina costs (from <see cref="LuminaCosts"/>) to the
    /// new-family VANILLA base rows, before ApplyBaseTierRenames renames them.
    /// Clone rows pick their costs up in InjectPassiveRows instead.
    /// </summary>
    private static void ReworkNewVanillaBases(string modDir)
    {
        var pePath = PictosCreateKit.FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? throw new FileNotFoundException("DT_PassiveEffects");
        var pe = new DataTableEditor(pePath);
        bool dirty = false;
        foreach (var (id, cost) in LuminaCosts)
        {
            if (!pe.HasRow(id)) continue; // clones land in the table later (InjectPassiveRows)
            if (!pe.TryGetRow(id, out var peRow)) continue;
            foreach (var f in peRow.Value)
            {
                if (DataTableEditor.CleanName(f.Name) == "LuminaCost" && f is IntPropertyData c && c.Value != cost)
                {
                    c.Value = cost;
                    dirty = true;
                }
            }
        }
        if (dirty) pe.Save(pePath);
    }

    private static void ReworkVanillaStunBoost(string modDir)
    {
        const string name = "Stun Boost I";
        const string desc = "Deal 10% more damage to Stunned targets.";
        var itemsPath = PictosCreateKit.FindAsset(modDir, "DT_jRPG_Items.uasset")
            ?? throw new FileNotFoundException("DT_jRPG_Items");
        var items = new DataTableEditor(itemsPath);
        if (items.TryGetRow("StunBoost", out var itemRow))
        {
            PictosCreateKit.SetRowText(items, itemRow, "Item_DisplayName",
                "PASSIVE_StunBoost_Name", name);
            PictosCreateKit.SetRowText(items, itemRow, "ItemDescription",
                "PASSIVE_StunBoost_Description", desc);
            items.Save(itemsPath);
        }
        var pePath = PictosCreateKit.FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? throw new FileNotFoundException("DT_PassiveEffects");
        var pe = new DataTableEditor(pePath);
        if (pe.TryGetRow("StunBoost", out var peRow))
        {
            PictosCreateKit.SetRowText(pe, peRow, "Name", "PASSIVE_StunBoost_Name", name);
            PictosCreateKit.SetRowText(pe, peRow, "Description", "PASSIVE_StunBoost_Description", desc);
            foreach (var f in peRow.Value)
                if (DataTableEditor.CleanName(f.Name) == "LuminaCost" && f is IntPropertyData c)
                    c.Value = 10;
            pe.Save(pePath);
        }
    }

    /// <summary>
    /// Vanilla Parry Specialist becomes tier I of the Parry Specialist ladder:
    /// renamed to "Parry Specialist I", its balancing rows aligned to the
    /// created tiers (-10% window, x2 counter) in InjectBalancing, and the
    /// tier-I Lumina cost set to 5.
    /// </summary>
    private static void ReworkVanillaParrySpecialist(string modDir)
    {
        const string name = "Parry Specialist I";
        const string desc = "Parry window reduced by 10%. Counterattacks deal double damage.";
        var itemsPath = PictosCreateKit.FindAsset(modDir, "DT_jRPG_Items.uasset")
            ?? throw new FileNotFoundException("DT_jRPG_Items");
        var items = new DataTableEditor(itemsPath);
        if (items.TryGetRow("ParrySpecialist", out var itemRow))
        {
            PictosCreateKit.SetRowText(items, itemRow, "Item_DisplayName",
                "PASSIVE_ParrySpecialist_Name", name);
            PictosCreateKit.SetRowText(items, itemRow, "ItemDescription",
                "PASSIVE_ParrySpecialist_Description", desc);
            items.Save(itemsPath);
        }
        var pePath = PictosCreateKit.FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? throw new FileNotFoundException("DT_PassiveEffects");
        var pe = new DataTableEditor(pePath);
        if (pe.TryGetRow("ParrySpecialist", out var peRow))
        {
            PictosCreateKit.SetRowText(pe, peRow, "Name", "PASSIVE_ParrySpecialist_Name", name);
            PictosCreateKit.SetRowText(pe, peRow, "Description", "PASSIVE_ParrySpecialist_Description", desc);
            foreach (var f in peRow.Value)
                if (DataTableEditor.CleanName(f.Name) == "LuminaCost" && f is IntPropertyData c)
                    c.Value = 5;
            pe.Save(pePath);
        }
    }

    private static void ReworkVanillaGlassCannon(string modDir)
    {
        const string name = "Glass Cannon";
        // Fix spelling: vanilla asset is GlassCanon (one n) but display should be Glass Cannon
        var itemsPath = PictosCreateKit.FindAsset(modDir, "DT_jRPG_Items.uasset")
            ?? throw new FileNotFoundException("DT_jRPG_Items");
        var items = new DataTableEditor(itemsPath);
        if (items.TryGetRow("GlassCanon", out var itemRow))
        {
            PictosCreateKit.SetRowText(items, itemRow, "Item_DisplayName",
                "PASSIVE_GlassCanon_Name", name);
            // Description stays vanilla but ensure correct spelling if needed
            items.Save(itemsPath);
        }
        var pePath = PictosCreateKit.FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? throw new FileNotFoundException("DT_PassiveEffects");
        var pe = new DataTableEditor(pePath);
        if (pe.TryGetRow("GlassCanon", out var peRow))
        {
            PictosCreateKit.SetRowText(pe, peRow, "Name", "PASSIVE_GlassCanon_Name", name);
            // Ensure LuminaCost 10 for tier I
            foreach (var f in peRow.Value)
                if (DataTableEditor.CleanName(f.Name) == "LuminaCost" && f is IntPropertyData c)
                    c.Value = 10;
            pe.Save(pePath);
        }
    }

    private static void ReworkVanillaRoulette(string modDir)
    {
        const string desc = "Every hit has a 50% chance to deal either 50% or 150% of its damage.";
        var itemsPath = PictosCreateKit.FindAsset(modDir, "DT_jRPG_Items.uasset")
            ?? throw new FileNotFoundException("DT_jRPG_Items");
        var items = new DataTableEditor(itemsPath);
        if (items.TryGetRow("Roulette", out var itemRow))
        {
            PictosCreateKit.SetRowText(items, itemRow, "ItemDescription",
                "PASSIVE_Roulette_Description", desc);
            items.Save(itemsPath);
        }
        var pePath = PictosCreateKit.FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? throw new FileNotFoundException("DT_PassiveEffects");
        var pe = new DataTableEditor(pePath);
        if (pe.TryGetRow("Roulette", out var peRow))
        {
            PictosCreateKit.SetRowText(pe, peRow, "Description", "PASSIVE_Roulette_Description", desc);
            foreach (var f in peRow.Value)
                if (DataTableEditor.CleanName(f.Name) == "LuminaCost" && f is IntPropertyData c)
                    c.Value = 5;
            pe.Save(pePath);
        }
    }

    private static void ReworkVanillaTeamwork(string modDir)
    {
        const string desc = "Deal 5% more damage while all allies are alive and you are not alone.";
        var itemsPath = PictosCreateKit.FindAsset(modDir, "DT_jRPG_Items.uasset")
            ?? throw new FileNotFoundException("DT_jRPG_Items");
        var items = new DataTableEditor(itemsPath);
        if (items.TryGetRow("Teamwork", out var itemRow))
        {
            PictosCreateKit.SetRowText(items, itemRow, "ItemDescription",
                "PASSIVE_Teamwork_Description", desc);
            items.Save(itemsPath);
        }
        var pePath = PictosCreateKit.FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? throw new FileNotFoundException("DT_PassiveEffects");
        var pe = new DataTableEditor(pePath);
        if (pe.TryGetRow("Teamwork", out var peRow))
        {
            PictosCreateKit.SetRowText(pe, peRow, "Description", "PASSIVE_Teamwork_Description", desc);
            foreach (var f in peRow.Value)
                if (DataTableEditor.CleanName(f.Name) == "LuminaCost" && f is IntPropertyData c)
                    c.Value = 5;
            pe.Save(pePath);
        }
        var srcBp = FileCopy.FindTablesAsset("BP_LU_Teamwork.uasset")
            ?? throw new FileNotFoundException("BP_LU_Teamwork");
        var dest = Path.Combine(modDir, "Sandfall/Content/Gameplay/Lumina/Passives/OnDamageDealt/BP_LU_Teamwork.uasset");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(srcBp, dest);
        var srcUexp = Path.ChangeExtension(srcBp, ".uexp");
        var destUexp = Path.ChangeExtension(dest, ".uexp");
        if (File.Exists(srcUexp)) File.Copy(srcUexp, destUexp, true);
        PatchTeamworkDamage(dest, 1.05);
    }

    /// <summary>
    /// Confident tier-I is the vanilla row retuned from "50% less damage" to 5%
    /// (received multiplier 0.5 -> 0.95). The value is a DoubleConst in the BP
    /// (no balancing row), so the vanilla BP is staged as a mod override with
    /// the constant patched — the ReworkVanillaTeamwork pattern.
    /// </summary>
    private static void ReworkConfidentBase(string modDir)
    {
        var srcBp = FileCopy.FindTablesAsset("BP_LU_CantHealButDamage50percentLess.uasset")
            ?? throw new FileNotFoundException("BP_LU_CantHealButDamage50percentLess");
        var dest = Path.Combine(modDir, "Sandfall/Content/Gameplay/Lumina/Passives/OnDamageDealt/BP_LU_CantHealButDamage50percentLess.uasset");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(srcBp, dest);
        var srcUexp = Path.ChangeExtension(srcBp, ".uexp");
        var destUexp = Path.ChangeExtension(dest, ".uexp");
        if (File.Exists(srcUexp)) File.Copy(srcUexp, destUexp, true);
        PatchConstValue(dest, "ProcessReceivedRawDamages", 0.5, 0.95);
    }

    private static void ReworkBreakingCounterBase(string modDir)
    {
        var srcBp = FileCopy.FindTablesAsset("BP_LU_BreakingCounter.uasset")
            ?? throw new FileNotFoundException("BP_LU_BreakingCounter");
        var dest = Path.Combine(modDir, "Sandfall/Content/Gameplay/Lumina/Passives/BP_LU_BreakingCounter.uasset");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(srcBp, dest);
        PatchConstValue(dest, "ProcessDealtRawDamages", 1.5, 1.10);
    }

    private static void ReworkStandBase(string modDir)
    {
        var srcBp = FileCopy.FindTablesAsset("BP_LU_Stand.uasset")
            ?? throw new FileNotFoundException("BP_LU_Stand");
        var dest = Path.Combine(modDir, "Sandfall/Content/Gameplay/Lumina/Passives/OnDamageDealt/BP_LU_Stand.uasset");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(srcBp, dest);
        PatchConstValue(dest, "ProcessDealtRawDamages", 1.25, 1.05);
    }

    /// <summary>
    /// Vanilla Random Defense rolls a continuous Low..1 range and its reciprocal,
    /// ignoring HighMultiplier. The tier rows are fixed at Low=0.5/High=1.5, so
    /// narrow the roll to Low (max 50) and scale the reciprocal branch to High
    /// (0.75 / 0.5 = 1.5). The existing RandomBool then selects exactly 0.5 or 1.5.
    /// Keep Low at 0.5 and High at 1.5; the smoke test asserts both rows.
    /// </summary>
    private static void ReworkRandomDefenseBase(string modDir)
    {
        var srcBp = FileCopy.FindTablesAsset("BP_LU_RandomDefense.uasset")
            ?? throw new FileNotFoundException("BP_LU_RandomDefense");
        var dest = Path.Combine(modDir, "Sandfall/Content/Gameplay/Lumina/Passives/BP_LU_RandomDefense.uasset");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(srcBp, dest);
        PatchRandomDefenseBranch(dest);
    }

    private static void PatchRandomDefenseBranch(string bpDest)
    {
        PatchIntConstValue(bpDest, "ProcessReceivedRawDamages", 100, 50);
        PatchConstValue(bpDest, "ProcessReceivedRawDamages", 1.0, 0.75);
    }

    /// <summary>
    /// Lucky Aim I is the vanilla AP-recovery passive itself — the II/III tiers
    /// are clones of it — so the base has to be staged and patched too (30% → 15%).
    /// </summary>
    private static void ReworkLuckyAimBase(string modDir)
    {
        var srcBp = FileCopy.FindTablesAsset("BP_LU_APFreeAim_50Recover.uasset")
            ?? throw new FileNotFoundException("BP_LU_APFreeAim_50Recover");
        var dest = Path.Combine(modDir,
            "Sandfall/Content/Gameplay/Lumina/Passives/OnApChange/BP_LU_APFreeAim_50Recover.uasset");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(srcBp, dest);
        PatchConstValue(dest, "ProcessAPChange", LuckyAimVanillaChance, LuckyAimChance);
    }

    private static double BreakingCounterMult(string id) => id switch
    {
        "BreakingCounter2" => 1.25, "BreakingCounter3" => 1.50,
        "BreakingCounter4" => 2.00, "BreakingCounter5" => 3.00,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id),
    };

    private static double FullStrengthMult(string id) => id switch
    {
        "Stand2" => 1.10, "Stand3" => 1.20, "Stand4" => 1.30, "Stand5" => 1.50,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id),
    };

    private static double ConfidentReceived(string id) => id switch
    {
        "Confident2" => 0.90, "Confident3" => 0.85, "Confident4" => 0.80, "Confident5" => 0.75,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id),
    };

    /// <summary>
    /// Macabre Power (Clea's Death) spans two BPs: BP_LU_CleasDeath spawns a
    /// companion BP_LU_CleasDeath_Buff that reads the balancing row. The main
    /// clone's retargeted name map already points at BP_LU_&lt;Id&gt;_Buff, so
    /// Inject must also produce that asset with the per-tier balancing-key
    /// rename.
    /// </summary>
    private static void CloneCleasBuff(string modDir, Spec spec)
    {
        var src = FileCopy.FindTablesAsset("BP_LU_CleasDeath_Buff.uasset")
            ?? throw new FileNotFoundException("BP_LU_CleasDeath_Buff");
        var dest = Path.Combine(modDir, "Sandfall/Content/Gameplay/Lumina/Passives", "BP_LU_" + spec.Id + "_Buff.uasset");
        CloneBlueprint(src, dest, "BP_LU_" + spec.Id + "_Buff",
            new[] { ("CleasDeath_DamageIncrease", spec.Balancing[0].Key) });
    }

    private static void PatchTeamworkDamage(string destUasset, double newMultiplier)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null ? new UAsset(destUasset, EngineVersion.VER_UE5_4, map) : new UAsset(destUasset, EngineVersion.VER_UE5_4);
        bool patched = false;
        foreach (var exp in asset.Exports.OfType<UAssetAPI.ExportTypes.FunctionExport>())
        {
            if (exp.ObjectName.Value.Value != "ProcessDealtRawDamages") continue;
            uint off = 0;
            foreach (var bc in exp.ScriptBytecode)
                patched |= PatchTeamworkRec(asset, bc, ref off, newMultiplier);
        }
        if (patched) asset.Write(destUasset);
    }

    private static bool PatchTeamworkRec(UAsset asset, UAssetAPI.Kismet.Bytecode.KismetExpression expr, ref uint offset, double newVal)
    {
        uint cur = offset;
        bool did = false;
        if (expr is UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst dc && Math.Abs(dc.Value - 1.1) < 0.001)
        {
            dc.Value = newVal;
            did = true;
        }
        if (expr is UAssetAPI.Kismet.Bytecode.Expressions.EX_FloatConst fc && Math.Abs(fc.Value - 1.1f) < 0.001f)
        {
            fc.Value = (float)newVal;
            did = true;
        }
        var t = expr.GetType();
        foreach (var pr in t.GetProperties())
        {
            if (pr.PropertyType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression))
            {
                var val = pr.GetValue(expr) as UAssetAPI.Kismet.Bytecode.KismetExpression;
                if (val != null && val != expr) did |= PatchTeamworkRec(asset, val, ref offset, newVal);
            }
            else if (pr.PropertyType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression[]))
            {
                var arr = pr.GetValue(expr) as UAssetAPI.Kismet.Bytecode.KismetExpression[];
                if (arr != null) foreach (var child in arr) if (child != null) did |= PatchTeamworkRec(asset, child, ref offset, newVal);
            }
        }
        foreach (var f in t.GetFields())
        {
            if (f.FieldType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression))
            {
                var val = f.GetValue(expr) as UAssetAPI.Kismet.Bytecode.KismetExpression;
                if (val != null) did |= PatchTeamworkRec(asset, val, ref offset, newVal);
            }
            else if (f.FieldType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression[]))
            {
                var arr = f.GetValue(expr) as UAssetAPI.Kismet.Bytecode.KismetExpression[];
                if (arr != null) foreach (var child in arr) if (child != null) did |= PatchTeamworkRec(asset, child, ref offset, newVal);
            }
        }
        uint dummy = cur;
        try { expr.Visit(asset, ref dummy, (c, o) => { }); } catch { }
        offset = dummy;
        return did;
    }

    /// <summary>
    /// Replaces every Float/DoubleConst equal to <paramref name="from"/> inside
    /// the named function of a Blueprint with <paramref name="to"/> (used to
    /// retune const-based picto passives such as Confident's 0.5 received
    /// multiplier). No-op when the value is absent so re-injects stay idempotent.
    /// </summary>
    private static void PatchConstValue(string destUasset, string? functionName, double from, double to)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null ? new UAsset(destUasset, EngineVersion.VER_UE5_4, map) : new UAsset(destUasset, EngineVersion.VER_UE5_4);
        bool patched = false;
        foreach (var exp in asset.Exports.OfType<FunctionExport>())
        {
            if (functionName != null && exp.ObjectName.Value.Value != functionName) continue;
            foreach (var bc in exp.ScriptBytecode)
                patched |= PatchConstRec(bc, from, to);
        }
        if (patched) asset.Write(destUasset);
    }

    private static void PatchIntConstValue(string destUasset, string? functionName, int from, int to)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null ? new UAsset(destUasset, EngineVersion.VER_UE5_4, map) : new UAsset(destUasset, EngineVersion.VER_UE5_4);
        bool patched = false;
        foreach (var exp in asset.Exports.OfType<FunctionExport>())
        {
            if (functionName != null && exp.ObjectName.Value.Value != functionName) continue;
            if (exp.ScriptBytecode is null) continue;
            foreach (var bc in exp.ScriptBytecode)
                patched |= PatchIntConstRec(bc, from, to);
        }
        if (patched) asset.Write(destUasset);
    }

    public static bool PatchIntConstRec(UAssetAPI.Kismet.Bytecode.KismetExpression expr, int from, int to)
    {
        bool did = false;
        if (expr is UAssetAPI.Kismet.Bytecode.Expressions.EX_IntConst ic && ic.Value == from)
        {
            ic.Value = to;
            did = true;
        }
        var t = expr.GetType();
        foreach (var pr in t.GetProperties())
        {
            if (pr.PropertyType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression))
            {
                var val = pr.GetValue(expr) as UAssetAPI.Kismet.Bytecode.KismetExpression;
                if (val != null && val != expr) did |= PatchIntConstRec(val, from, to);
            }
            else if (pr.PropertyType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression[]))
            {
                var arr = pr.GetValue(expr) as UAssetAPI.Kismet.Bytecode.KismetExpression[];
                if (arr != null) foreach (var child in arr) if (child != null) did |= PatchIntConstRec(child, from, to);
            }
        }
        foreach (var f in t.GetFields())
        {
            if (f.FieldType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression))
            {
                var val = f.GetValue(expr) as UAssetAPI.Kismet.Bytecode.KismetExpression;
                if (val != null) did |= PatchIntConstRec(val, from, to);
            }
            else if (f.FieldType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression[]))
            {
                var arr = f.GetValue(expr) as UAssetAPI.Kismet.Bytecode.KismetExpression[];
                if (arr != null) foreach (var child in arr) if (child != null) did |= PatchIntConstRec(child, from, to);
            }
        }
        return did;
    }

    /// <summary>
    /// Repoints a cloned BP's CDO display <c>Name</c> from the donor's passive
    /// row (<c>PASSIVE_&lt;SourceId&gt;_Name</c>) to the clone's own row key.
    /// <c>CloneBlueprint</c>'s name-map sweep only rewrites the BP stem, so
    /// without this every clone's proc banner shows the donor's name. Only
    /// touches the exact stale reference; clones whose donor Name is already
    /// correct (or not a row reference) are left alone.
    /// </summary>
    private static void RetargetCloneDisplayName(string destUasset, Spec spec)
    {
        var from = "PASSIVE_" + spec.SourceId + "_Name";
        var to = "PASSIVE_" + spec.RowId + "_Name";
        if (from == to) return;
        var map = DataTableEditor.Mappings;
        var asset = map != null ? new UAsset(destUasset, EngineVersion.VER_UE5_4, map) : new UAsset(destUasset, EngineVersion.VER_UE5_4);
        bool patched = false;
        foreach (var ne in asset.Exports.OfType<NormalExport>())
        {
            foreach (var p in ne.Data)
            {
                if (p is TextPropertyData t
                    && DataTableEditor.CleanName(p.Name) == "Name"
                    && t.HistoryType == TextHistoryType.StringTableEntry
                    && t.Value?.Value == from)
                {
                    t.Value = new FString(to);
                    patched = true;
                }
            }
        }
        if (patched)
        {
            asset.Write(destUasset);
            Console.WriteLine($"[{spec.Id}] display Name -> {to}");
        }
    }

    /// <summary>
    /// Gradient Overcharge: the vanilla <c>BP_LU_GradientOvercharge</c> hard-codes
    /// the amount it takes, in two places in the ubergraph — the
    /// "GetAvailableCharges &gt;= 3" gate and the <c>ConsumeCharge(3)</c> argument.
    /// It never reads the <c>*_GradientConsumedAmount</c> balancing row, so cloning
    /// II/III only renamed the DamageMultiplier key and every tier still consumed 3.
    /// Rewrite both IntConsts to the tier's charge count (IntConst is fixed-width,
    /// so bytecode offsets are unchanged). Assert exactly two so a graph change
    /// fails loudly instead of silently under-consuming.
    /// </summary>
    private static void PatchGradientOverchargeCharges(string destUasset, int charges)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null ? new UAsset(destUasset, EngineVersion.VER_UE5_4, map) : new UAsset(destUasset, EngineVersion.VER_UE5_4);
        int patched = 0;
        foreach (var exp in asset.Exports.OfType<FunctionExport>())
        {
            if (!exp.ObjectName.Value.Value.StartsWith("ExecuteUbergraph", StringComparison.Ordinal)) continue;
            MayhemFixKit.Walk(exp.ScriptBytecode ?? Array.Empty<KismetExpression>(), e =>
            {
                if (e is EX_IntConst ic && ic.Value == 3)
                {
                    ic.Value = charges;
                    patched++;
                }
            });
        }
        if (patched != 2)
            throw new InvalidDataException(
                $"GradientOvercharge: expected 2 charge IntConsts, patched {patched} (charges {charges})");
        asset.Write(destUasset);
    }

    /// <summary>
    /// Gradient Overcharge II/III proc banner. The Format pattern is a
    /// <c>StringTableEntry</c> pointing at the shared tier key; replace it with an
    /// inline invariant literal so the clone can name its own multiplier. The
    /// replacement shrinks the statement (StringTableEntry carries an object
    /// pointer + table id + key), so shift the ubergraph's absolute offsets and
    /// re-point the event-entry offsets — the same fixup <see cref="NeuterGates"/>
    /// uses. Leaving a stale target jumps into the middle of a statement and
    /// crashes the VM at turn start.
    /// </summary>
    private static void PatchGradientOverchargeTooltipText(string destUasset, string text)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null ? new UAsset(destUasset, EngineVersion.VER_UE5_4, map) : new UAsset(destUasset, EngineVersion.VER_UE5_4);
        var uber = asset.Exports.OfType<FunctionExport>().FirstOrDefault(
            f => f.ObjectName.Value.Value.StartsWith("ExecuteUbergraph", StringComparison.Ordinal));
        if (uber?.ScriptBytecode is null)
            throw new InvalidDataException("GradientOvercharge: ubergraph not found");

        var stmts = uber.ScriptBytecode;
        var startsBefore = StatementStarts(asset, uber);
        int idx = -1;
        EX_TextConst? tc = null;
        for (int i = 0; i < stmts.Length && tc is null; i++)
        {
            int captured = i;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (tc is null && e is EX_TextConst t
                    && t.Value.TextLiteralType == EBlueprintTextLiteralType.StringTableEntry
                    && (t.Value.StringTableKey as EX_StringConst)?.Value == GradientTooltipKey)
                {
                    tc = t;
                    idx = captured;
                }
            });
        }
        if (tc is null || idx < 0)
            throw new InvalidDataException($"GradientOvercharge: tooltip TextConst '{GradientTooltipKey}' not found");

        uint stmtStart = startsBefore[idx];
        uint oldSize = (uint)stmts[idx].GetSize(asset);
        tc.Value.TextLiteralType = EBlueprintTextLiteralType.InvariantText;
        tc.Value.InvariantLiteralString = new EX_StringConst { Value = text };
        tc.Value.StringTableAsset = null;
        tc.Value.StringTableId = null;
        tc.Value.StringTableKey = null;
        uint newSize = (uint)stmts[idx].GetSize(asset);
        if (newSize < oldSize)
            LuneSkillsKit.ShiftAbsoluteSigned(asset, uber, stmtStart + newSize, oldSize - newSize);
        else if (newSize > oldSize)
            ShiftOffsetsAtOrAfter(asset, uber, stmtStart + oldSize, newSize - oldSize);

        MayhemFixKit.RecalcContextSkips(asset, uber);
        var startsAfter = StatementStarts(asset, uber);
        RepointUbergraphEntries(asset, uber, startsBefore, startsAfter, "GradientOvercharge");
        ValidateJumpTargets(asset, uber, "GradientOvercharge / ExecuteUbergraph");
        asset.Write(destUasset);
        Console.WriteLine($"[GradientOvercharge] tooltip pattern -> \"{text}\" ({oldSize} -> {newSize} bytes)");
    }

    /// <summary>
    /// Critical Chain: rewires the Empowering Parry counter into a crit chain.
    /// The donor's bonus math (count × per-event balancing key) is kept as-is;
    /// three paths are neutered so the ONLY state change is the new crit gate:
    /// ubergraph parry-increment and turn-end reset (both gated on their own
    /// IsBuffTarget locals) and the ProcessReceivedRawDamages hit-taken reset
    /// each become an unconditional jump along their existing skip path by
    /// swapping the gate condition to False — no offsets change. Then
    /// ProcessDealtRawDamages gains an appended block before the parent call:
    /// if BattleDamages.IsCriticalHit (same Context shape as
    /// BP_LU_AlternatingCritical) count++ + Notify, else count = 0. The current
    /// hit therefore scales with the chain built by previous hits. No new
    /// function locals: the increment Lets the instance counter from an inline
    /// Add_IntInt, and every pointer/import is cloned from the same asset.
    /// </summary>
    internal static void PatchCriticalChain(string destUasset, int maxStacks)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null ? new UAsset(destUasset, EngineVersion.VER_UE5_4, map) : new UAsset(destUasset, EngineVersion.VER_UE5_4);

        FunctionExport Fn(string name) =>
            asset.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.Value.Value == name)
            ?? throw new InvalidDataException("CriticalChain: " + name + " not found");

        var uber = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.Value.Value.StartsWith("ExecuteUbergraph", StringComparison.Ordinal))
            ?? throw new InvalidDataException("CriticalChain: ubergraph not found");
        // Event blocks are entered by a hard-coded offset (EX_ComputedJump), so
        // record the layout before the gates shrink and repoint the callers after.
        var uberStartsBefore = StatementStarts(asset, uber);
        int neutered = NeuterGates(asset, uber,
            lv => PartyFiveKit.FieldName(lv.Variable) is "CallFunc_IsBuffTarget_IsTarget_1" or "CallFunc_IsBuffTarget_IsTarget_2");
        if (neutered != 2)
            throw new InvalidDataException($"CriticalChain: expected 2 ubergraph gates, neutered {neutered}");

        var recv = Fn("ProcessReceivedRawDamages");
        int rNeutered = NeuterGates(asset, recv,
            lv => PartyFiveKit.FieldName(lv.Variable) == "CallFunc_IsBuffTarget_IsTarget");
        if (rNeutered != 1)
            throw new InvalidDataException($"CriticalChain: expected 1 received gate, neutered {rNeutered}");

        var fn = Fn("ProcessDealtRawDamages");
        var stmts = fn.ScriptBytecode!.ToList();
        int parentIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (!hit && e is EX_LocalFinalFunction lf && lf.StackNode.IsImport()
                    && asset.Imports[Math.Abs(lf.StackNode.Index) - 1].ObjectName.ToString() == "ProcessDealtRawDamages")
                    hit = true;
            });
            if (hit) { parentIdx = i; break; }
        }
        if (parentIdx < 0)
            throw new InvalidDataException("CriticalChain: parent ProcessDealtRawDamages call not found");

        if (stmts[0] is not EX_Context dealtCtx || dealtCtx.ObjectExpression is not EX_LocalOutVariable dealtOut
            || PartyFiveKit.FieldName(dealtOut.Variable) != "BattleDamages")
            throw new InvalidDataException("CriticalChain: stmt#0 BattleDamages context not found");
        KismetPropertyPointer? countPtrTpl = null;
        foreach (var st in stmts)
            MayhemFixKit.Walk(new[] { st }, e =>
            {
                if (countPtrTpl is null && e is EX_InstanceVariable iv
                    && PartyFiveKit.FieldName(iv.Variable) == "ParrySuccessCount")
                    countPtrTpl = iv.Variable;
            });
        EX_CallMath? addTpl = null;
        EX_CallMath? greaterTpl = null;
        foreach (var u in asset.Exports.OfType<FunctionExport>())
            foreach (var st in u.ScriptBytecode ?? Array.Empty<KismetExpression>())
                MayhemFixKit.Walk(new[] { st }, e =>
                {
                    if (e is not EX_CallMath cm || !cm.StackNode.IsImport()) return;
                    var nm = asset.Imports[Math.Abs(cm.StackNode.Index) - 1].ObjectName.ToString();
                    if (addTpl is null && nm == "Add_IntInt") addTpl = cm;
                    if (greaterTpl is null && nm == "Greater_IntInt") greaterTpl = cm;
                });
        if (countPtrTpl is null)
            throw new InvalidDataException("CriticalChain: ParrySuccessCount pointer not found");
        if (addTpl is null)
            throw new InvalidDataException("CriticalChain: Add_IntInt import not found");
        if (greaterTpl is null)
            throw new InvalidDataException("CriticalChain: Greater_IntInt import not found");
        int dmgImp = -1;
        for (int i = 0; i < asset.Imports.Count; i++)
            if (asset.Imports[i].ObjectName.ToString() == "BP_BattleDamages_C") { dmgImp = i; break; }
        if (dmgImp < 0)
            throw new InvalidDataException("CriticalChain: BP_BattleDamages_C import not found");
        var dmgOwner = FPackageIndex.FromImport(dmgImp);
        EX_Context? notifyTpl = null;
        foreach (var st in stmts)
            MayhemFixKit.Walk(new[] { st }, e =>
            {
                if (notifyTpl is null && e is EX_Context c && c.ContextExpression is EX_LocalVirtualFunction vf
                    && vf.VirtualFunctionName.ToString() == "NotifyLuminaPassiveEffectBuff")
                    notifyTpl = c;
            });
        if (notifyTpl is null)
            throw new InvalidDataException("CriticalChain: Notify template not found");

        KismetPropertyPointer ClonePtr(KismetPropertyPointer src)
        {
            if (src.New is null)
                throw new InvalidDataException("CriticalChain: legacy Old-style pointer");
            var path = (src.New.Path ?? Array.Empty<FName>()).Select(n => FName.FromString(asset, n.ToString())).ToArray();
            return new KismetPropertyPointer(new FFieldPath(path, src.New.ResolvedOwner));
        }

        var critCtx = new EX_Context
        {
            ObjectExpression = new EX_LocalOutVariable { Variable = ClonePtr(dealtOut.Variable!) },
            Offset = 0,
            PropertyType = 0,
            RValuePointer = new KismetPropertyPointer(new FFieldPath(
                new[] { FName.FromString(asset, "IsCriticalHit") }, dmgOwner)),
            ContextExpression = new EX_InstanceVariable
            {
                Variable = new KismetPropertyPointer(new FFieldPath(
                    new[] { FName.FromString(asset, "IsCriticalHit") }, dmgOwner)),
            },
        };
        var incLet = new EX_Let
        {
            Value = ClonePtr(countPtrTpl),
            Variable = new EX_InstanceVariable { Variable = ClonePtr(countPtrTpl) },
            Expression = new EX_CallMath
            {
                StackNode = addTpl.StackNode,
                Parameters = new KismetExpression[]
                {
                    new EX_InstanceVariable { Variable = ClonePtr(countPtrTpl) },
                    new EX_IntConst { Value = 1 },
                },
            },
        };
        var resetLet = new EX_Let
        {
            Value = ClonePtr(countPtrTpl),
            Variable = new EX_InstanceVariable { Variable = ClonePtr(countPtrTpl) },
            Expression = new EX_IntConst { Value = 0 },
        };
        // Clamp the chain counter: if count > maxStacks then count = maxStacks.
        var capGate = new EX_JumpIfNot
        {
            CodeOffset = 0,
            BooleanExpression = new EX_CallMath
            {
                StackNode = greaterTpl.StackNode,
                Parameters = new KismetExpression[]
                {
                    new EX_InstanceVariable { Variable = ClonePtr(countPtrTpl) },
                    new EX_IntConst { Value = maxStacks },
                },
            },
        };
        var capLet = new EX_Let
        {
            Value = ClonePtr(countPtrTpl),
            Variable = new EX_InstanceVariable { Variable = ClonePtr(countPtrTpl) },
            Expression = new EX_IntConst { Value = maxStacks },
        };
        // The owner's IsBuffTarget local, so the block can ignore hits by others.
        KismetPropertyPointer? isTargetPtrTpl = null;
        foreach (var st in stmts)
            MayhemFixKit.Walk(new[] { st }, e =>
            {
                if (isTargetPtrTpl is null && e is EX_LocalVariable lv
                    && PartyFiveKit.FieldName(lv.Variable) == "CallFunc_IsBuffTarget_IsTarget")
                    isTargetPtrTpl = lv.Variable;
            });
        if (isTargetPtrTpl is null)
            throw new InvalidDataException("CriticalChain: IsBuffTarget local not found");

        // Insert the counter update right before the count>0 read so the bonus
        // gate below sees the fresh count (a crit therefore scales the same hit).
        int insertIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool isCountGate = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (!isCountGate && e is EX_CallMath cm && cm.StackNode.IsImport()
                    && asset.Imports[Math.Abs(cm.StackNode.Index) - 1].ObjectName.ToString() == "Greater_IntInt")
                    isCountGate = true;
            });
            if (isCountGate) { insertIdx = i; break; }
        }
        if (insertIdx < 0)
            throw new InvalidDataException("CriticalChain: count>0 gate not found");

        var gate0 = new EX_JumpIfNot
        {
            CodeOffset = 0,
            BooleanExpression = new EX_LocalVariable { Variable = ClonePtr(isTargetPtrTpl) },
        };
        var gate = new EX_JumpIfNot { CodeOffset = 0, BooleanExpression = critCtx };
        var skipReset = new EX_Jump { CodeOffset = 0 };
        var block = new List<KismetExpression>
        {
            gate0, gate, incLet, capGate, capLet, (KismetExpression)MayhemFixKit.CloneKismet(notifyTpl),
            skipReset, resetLet,
        };

        uint base_ = 0;
        for (int i = 0; i < insertIdx; i++) base_ += (uint)stmts[i].GetSize(asset);
        uint parentOff = 0;
        for (int i = 0; i < parentIdx; i++) parentOff += (uint)stmts[i].GetSize(asset);
        uint[] sizes = block.Select(b => (uint)b.GetSize(asset)).ToArray();
        uint delta = 0;
        foreach (var s in sizes) delta += s;
        // block: 0 gate0, 1 gate, 2 incLet, 3 capGate, 4 capLet, 5 notify, 6 skipReset, 7 resetLet
        gate0.CodeOffset = parentOff + delta;                                  // not the owner -> parent call
        gate.CodeOffset = base_ + sizes[0] + sizes[1] + sizes[2] + sizes[3] + sizes[4] + sizes[5] + sizes[6];
        capGate.CodeOffset = base_ + sizes[0] + sizes[1] + sizes[2] + sizes[3] + sizes[4];
        skipReset.CodeOffset = base_ + delta;                                  // crit handled -> continue

        ShiftOffsetsAtOrAfter(asset, fn, base_, delta);
        stmts.InsertRange(insertIdx, block);
        fn.ScriptBytecode = stmts.ToArray();
        MayhemFixKit.RecalcContextSkips(asset, fn);
        var uberStartsAfter = StatementStarts(asset, uber);
        RepointUbergraphEntries(asset, uber, uberStartsBefore, uberStartsAfter);
        ValidateJumpTargets(asset, uber, "ExecuteUbergraph");
        ValidateJumpTargets(asset, recv, "ProcessReceivedRawDamages");
        ValidateJumpTargets(asset, fn, "ProcessDealtRawDamages");
        asset.Write(destUasset);
        Console.WriteLine($"[critchain] counter update inserted at {base_} (+{delta}b)");
    }

    /// <summary>For each top-level <c>EX_JumpIfNot</c> whose boolean reads the
    /// matching local, replaces the read with <c>False</c> (forcing the branch
    /// down its skip path) and shifts every jump target past the statement to
    /// absorb the size change. A bare <c>EX_False</c> is smaller than the
    /// local-variable read it replaces, so without the fixup the following
    /// bytecode moves and the jump targets dangle — a VM crash on battle entry.</summary>
    private static int NeuterGates(UAsset asset, FunctionExport fn, Func<EX_LocalVariable, bool> match)
    {
        int n = 0;
        var stmts = fn.ScriptBytecode!;
        for (int i = 0; i < stmts.Length; i++)
        {
            if (stmts[i] is not EX_JumpIfNot jn || jn.BooleanExpression is not EX_LocalVariable lv) continue;
            if (!match(lv)) continue;
            uint start = 0;
            for (int j = 0; j < i; j++) start += (uint)stmts[j].GetSize(asset);
            uint oldSize = (uint)stmts[i].GetSize(asset);
            jn.BooleanExpression = new EX_False();
            uint newSize = (uint)stmts[i].GetSize(asset);
            if (newSize < oldSize)
                LuneSkillsKit.ShiftAbsoluteSigned(asset, fn, start + newSize, oldSize - newSize);
            else if (newSize > oldSize)
                ShiftOffsetsAtOrAfter(asset, fn, start + oldSize, newSize - oldSize);
            n++;
        }
        return n;
    }

    /// <summary>Fails the build if any jump/push/switch target in the function
    /// does not land exactly on a statement start. A dangling target runs the VM
    /// off the end of a statement and crashes (Critical Chain did this on battle
    /// entry before the size fixups were added).</summary>
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
                    case EX_SwitchValue sv:
                        if (!starts.Contains(sv.EndGotoOffset)) bad.Add($"SwitchEnd->{sv.EndGotoOffset}");
                        if (sv.Cases != null)
                            for (int i = 0; i < sv.Cases.Length; i++)
                                if (!starts.Contains(sv.Cases[i].NextOffset)) bad.Add($"SwitchCase->{sv.Cases[i].NextOffset}");
                        break;
                }
            });
        if (bad.Count > 0)
            throw new InvalidDataException(
                $"CriticalChain: {tag} has {bad.Count} dangling jump target(s): {string.Join(", ", bad)}");
    }

    /// <summary>Byte offset of every top-level statement in the function.</summary>
    private static List<uint> StatementStarts(UAsset asset, FunctionExport fn)
    {
        var list = new List<uint>();
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
        {
            list.Add(pos);
            pos += (uint)st.GetSize(asset);
        }
        return list;
    }

    /// <summary>The ubergraph is entered through <c>EX_ComputedJump</c> with a
    /// hard-coded offset passed by each caller (ActivateBuff / OnParrySuccess /
    /// ApplyOnTurnEnd). Shrinking a gate moves the event blocks, so re-point every
    /// caller's entry constant at the block's new offset — a stale one jumps into
    /// the middle of a statement and crashes the VM (Critical Chain crashed at
    /// turn end exactly this way).</summary>
    private static void RepointUbergraphEntries(
        UAsset asset, FunctionExport uber, List<uint> before, List<uint> after, string tag = "CriticalChain")
    {
        int uberExp = asset.Exports.IndexOf(uber);
        var map = new Dictionary<uint, uint>();
        for (int i = 0; i < before.Count && i < after.Count; i++) map[before[i]] = after[i];
        var startsAfter = new HashSet<uint>(after);
        int repointed = 0;
        foreach (var f in asset.Exports.OfType<FunctionExport>())
        {
            if (ReferenceEquals(f, uber)) continue;
            foreach (var st in f.ScriptBytecode ?? Array.Empty<KismetExpression>())
                MayhemFixKit.Walk(new[] { st }, e =>
                {
                    if (e is not EX_FinalFunction ff || ff.StackNode.IsImport()) return;
                    if (Math.Abs(ff.StackNode.Index) - 1 != uberExp) return;
                    for (int i = 0; i < ff.Parameters.Length; i++)
                        if (ff.Parameters[i] is EX_IntConst ic)
                        {
                            if (map.TryGetValue((uint)ic.Value, out var neu)) { ic.Value = (int)neu; repointed++; }
                            if (!startsAfter.Contains((uint)ic.Value))
                                throw new InvalidDataException(
                                    $"{tag}: ubergraph entry {ic.Value} is not a statement start");
                        }
                });
        }
        Console.WriteLine($"[{tag}] ubergraph entry offsets repointed: {repointed}");
    }

    /// <summary>Jump-target fixup with at-or-after semantics (an insert exactly
    /// at an existing jump's target must shift that target too).</summary>
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
                    case EX_SwitchValue sv:
                        if (sv.EndGotoOffset >= point) sv.EndGotoOffset += delta;
                        for (int i = 0; i < sv.Cases.Length; i++)
                            if (sv.Cases[i].NextOffset >= point) sv.Cases[i].NextOffset += delta;
                        break;
                }
            });
    }

    private static bool PatchConstRec(UAssetAPI.Kismet.Bytecode.KismetExpression expr, double from, double to)
    {
        bool did = false;
        if (expr is UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst dc && Math.Abs(dc.Value - from) < 1e-6)
        {
            dc.Value = to;
            did = true;
        }
        if (expr is UAssetAPI.Kismet.Bytecode.Expressions.EX_FloatConst fc && Math.Abs(fc.Value - from) < 1e-6f)
        {
            fc.Value = (float)to;
            did = true;
        }
        var t = expr.GetType();
        foreach (var pr in t.GetProperties())
        {
            if (pr.PropertyType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression))
            {
                var val = pr.GetValue(expr) as UAssetAPI.Kismet.Bytecode.KismetExpression;
                if (val != null && val != expr) did |= PatchConstRec(val, from, to);
            }
            else if (pr.PropertyType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression[]))
            {
                var arr = pr.GetValue(expr) as UAssetAPI.Kismet.Bytecode.KismetExpression[];
                if (arr != null) foreach (var child in arr) if (child != null) did |= PatchConstRec(child, from, to);
            }
        }
        foreach (var f in t.GetFields())
        {
            if (f.FieldType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression))
            {
                var val = f.GetValue(expr) as UAssetAPI.Kismet.Bytecode.KismetExpression;
                if (val != null) did |= PatchConstRec(val, from, to);
            }
            else if (f.FieldType == typeof(UAssetAPI.Kismet.Bytecode.KismetExpression[]))
            {
                var arr = f.GetValue(expr) as UAssetAPI.Kismet.Bytecode.KismetExpression[];
                if (arr != null) foreach (var child in arr) if (child != null) did |= PatchConstRec(child, from, to);
            }
        }
        return did;
    }

    private static void CloneBlueprint(string src, string dest, string newStem,
        IReadOnlyList<(string From, string To)> replacements)
    {
        FileCopy.AssetPair(src, dest);
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(dest, EngineVersion.VER_UE5_4, map)
            : new UAsset(dest, EngineVersion.VER_UE5_4);
        Retarget(asset, dest, Path.GetFileNameWithoutExtension(src), newStem);
        var ordered = replacements.Where(r => r.From != r.To).OrderByDescending(r => r.From.Length).ToList();
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
        asset.Write(dest);
    }

    private static void CloneGear(string src, string dest, string newStem, string rowName)
    {
        FileCopy.AssetPair(src, dest);
        var ed = DataTableEditor.OpenPropertyAsset(dest);
        Retarget(ed.Asset, dest, Path.GetFileNameWithoutExtension(src), newStem);
        foreach (var export in ed.Asset.Exports.OfType<NormalExport>())
            foreach (var p in export.Data)
                RewriteRowNames(p, ed.Asset, rowName);
        ed.Asset.Write(dest);
    }

    /// <summary>
    /// Row handles are nested (DA: PassiveEffects[0].Row_2_…DataTableRowHandle.RowName),
    /// so a top-level-only sweep misses them — the clone would keep pointing its
    /// passive-effect row at the source id. Descend struct/array values.
    /// </summary>
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

    private static void Retarget(UAsset asset, string destUasset, string srcStem, string newStem)
    {
        var full = destUasset.Replace('\\', '/');
        var idx = full.IndexOf("/Content/", StringComparison.OrdinalIgnoreCase);
        var rel = full[(idx + "/Content/".Length)..];
        if (rel.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            rel = rel[..^".uasset".Length];
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

    private static void SetLoc(TextPropertyData t, string key, string display)
    {
        display = ModLocalization.Key(key, display);
        t.HistoryType = TextHistoryType.None;
        t.Flags = ETextFlag.CultureInvariant;
        t.Namespace = null;
        t.Value = new FString(display);
        t.CultureInvariantString = new FString(display);
        t.TableId = null;
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

    private static void EnsureExtracted(string filter)
    {
        if (FileCopy.FindTablesAsset(filter + ".uasset") != null) return;
        var extracted = Path.Combine(EditorPaths.RepoRoot, "extracted");
        if (Directory.Exists(extracted))
        {
            var hit = Directory.EnumerateFiles(extracted, filter + ".uasset", SearchOption.AllDirectories).FirstOrDefault();
            if (hit != null)
            {
                var relRoot = extracted.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var rel = Path.GetRelativePath(relRoot, Path.GetDirectoryName(hit)!);
                var destDir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", filter, rel);
                Directory.CreateDirectory(destDir);
                FileCopy.AssetPair(hit, Path.Combine(destDir, Path.GetFileName(hit)));
                return;
            }
        }
        var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", filter);
        Directory.CreateDirectory(dir);
        new Retoc().ToLegacy(dir, filter);
    }
}

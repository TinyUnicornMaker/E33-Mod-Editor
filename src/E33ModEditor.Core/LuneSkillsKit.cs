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
/// Creates NEW Lune skills by cloning an existing skill's Data Asset + skill-script
/// Blueprint + balancing rows + icon row, then appending a linked node to
/// DA_SkillGraph_Lune. The clone keeps the donor's stain maps unless
/// <see cref="NewSkill.OverchargeRequiredStains"/> / <see cref="NewSkill.ProducedStains"/>
/// are set (see <see cref="StainsMechanicKit"/>). Wired into rebuild-mod so the
/// new skills survive a full rebuild.
/// </summary>
public static class LuneSkillsKit
{
    /// <summary>Per-clone skill-script edits (applied to the cloned BP before it is written).</summary>
    public sealed record KismetEdits(
        bool OverchargeBonusHeal = false, // +30% MaxHP heal when overcharged (Restoration)
        string? RandomGenerate = null,    // "Light|Dark": replace GenerateStainsFromSkill with a 50/50 random branch
        string? DamageElement = null,     // MakeDamageBuilder element + DA BaseElementalType, e.g. "Light"
        bool CustomDarkConsume = false,   // replace TryConsumeOverchargeStainsFromSkill with a direct queue consume
        string? StatusOnOvercharge = null, // "Slow|Defenseless|Powerless": statuses applied only when overcharged (Shocking Wave)
        bool DarkerFire = false,          // darken ImpactFire colors + enlarge the fire FX (Infernal Rage)
        bool TripleDamage = false,        // 3 DealDamages per consume (Blitz), not a ×3 multiply
        bool LightningOnly = false,       // availability requires >=1 Lightning stain; tier key = consumed count (Blitz)
        bool GenerateLightPerConsume = false, // create 1 Light stain per consumed stain (Blitz)
        int QteFailStunTurns = 0,         // missed QTE interrupts: self-stun N turns + channel end (Fire Rage / Infernal Rage)
        bool AlreadyBurning = false,      // burn escalation keys on the target already having Burn, not Marked (Combustion)
        bool ConsumeGeneratesDark = false,  // overcharge creates 1 Dark explicitly instead of map-driven generation (Combustion)
        string? CustomConsume = null,       // "Earth|Earth|Dark": replace TryConsumeOverchargeStainsFromSkill with direct ConsumeStainByType calls
        bool NoLightSubstitution = false,   // disable the Light-stain substitution in ValidateSkillAvailability (Elemental Cataclysm)
        bool DirectNuke = false,            // remove Typhoon's zone buff spawn + ally heal (Gravity Mill)
        double OverchargeDamageMultiplier = 0, // block-level overcharge damage ×N for the direct hit (Gravity Mill)
        int DefenselessTurns = 0,           // ApplyBuff Defenseless for N turns when overcharged (Gravity Mill)
        int ExtraCritStrikes = 0,           // Lightning-family: chain N extra DealDamageAndFeedback per crit (Levin: 2)
        int BaseStrikes = 0,                // Maelstrom: per-turn strike count when NOT overcharged (row + buff param)
        int OverchargeStrikes = 0,          // Maelstrom: per-turn strike count when overcharged
        string? StatusesOnHit = null,       // "GreaterSlow|GreaterDefenseless": swap the donor's native status ApplyBuff
                                            // statement for these classes, gated on overcharge (Crushing Tsunami / Frost Piercer)
        int StatusesOnHitTurns = 0,         // turn count for StatusesOnHit applications (Crushing: 3, Frost: 7)
        bool RemoveNativeStatus = false,    // strip the donor's always-on status before gating (Frost: drop IceGust Slow)
        int GreaterPowerlessTurns = 0,      // Cloud Splitter: OC-gated Greater Powerless for N turns, synthesized
                                            // into DealDamageAndFeedback (Thunderfall has no native status to clone)
        string? ApGainStatuses = null,      // "Burn|Powerless|Slow|Defenseless": Soul Eater — +4 AP per status on the target
        bool ApGainAlways = false,          // Soul Eater: AP gain is unconditional (no overcharge gate)
        float ImpactScale = 1f,             // multiply the skill's spawned FX scale (Frost/Crushing "much larger", Serpenphare huge)
        bool StripForcePlayAgain = false,   // Soul Eater: remove the donor's overcharge "Force Play Again" second-turn
        bool PureZone = false,              // Serpenphare: Typhoon clone — remove the on-cast ally heal + tooltip, keep zone
        double? ZoneTickDamage = null,      // Serpenphare: per-turn zone tick damage row override (Typhoon_BaseDamage clone)
        int BaseZoneTurns = 0,              // Serpenphare: base zone duration (1)
        int OverchargeZoneTurns = 0);       // Serpenphare: overcharge zone duration (5)

    public sealed record NewSkill(
        string DaStem,          // e.g. "DA_Skill_Lune_VivifyingLight"
        string BpStem,          // e.g. "BP_Battle_SkillScript_VivifyingLight"
        string NameId,          // NameID on the DA, e.g. "VivifyingLight"
        string DonorDa,         // e.g. "DA_Skill_Lune_HealingLight"
        string DonorBp,         // e.g. "BP_Battle_SkillScript_HealingLight"
        string DonorStem,       // retarget stem inside the BP, e.g. "HealingLight"
        int ApCost,
        string DisplayName,
        string Description,
        string ShortDescription,
        (string DonorRow, string NewRow, double? Value)[] BalancingRows,
        string PrereqNameId,    // graph prerequisite node (NameID), e.g. "Revitalization"
        FVector2D Position,
        IReadOnlyDictionary<string, int>? OverchargeRequiredStains = null,
        IReadOnlyDictionary<string, int>? ProducedStains = null,
        KismetEdits? Kismet = null,
        (string From, string To)[]? ExtraPairs = null,
        string? SequenceStem = null,     // donor LevelSequence, e.g. "SEQ_SkillLune_IceGust"
        string? SequenceNewStem = null,  // clone stem, e.g. "SEQ_SkillLune_Chiaroscuro"
        bool RemoveVoice = false,        // delete the DirectorBP PlaySkillBattleLineInternal call
        (string From, string To)[]? SeqRetargets = null, // FX swaps inside the cloned sequence
        (string From, string To)[]? ExtraSequences = null, // extra sibling LevelSequences (e.g. Fire Rage's _Charge)
        (float R, float G, float B)? ChargeColor = null,  // recolor the FX_ElementalCharge aura keyframes
        string? MechanicStem = null,     // override StainsMechanic class (Chiaroscuro either-or widget)
        bool DropOverchargeOnBuff = false, // remove the donor's OverchargeOnBuff component (Combustion: stain-only overcharge)
        bool BiggerFlames = false,      // scale the cloned sequence's fire FX (User.Size x3, Lifetime x1.5 — Combustion)
        bool BiggerImpacts = false);    // scale the cloned sequence's elemental-impact transform Scale (Elemental Cataclysm)

    /// <summary>Skill clones that ship with the mod. The overcharge stain setup is inherited from the donor.</summary>
    public static readonly NewSkill[] Skills =
    {
        // Vivifying Light: Healing Light variant — 4 AP, heals 40-70% (base 0.4 + 7.5%/QTE),
        // overcharge still 2 Earth → 0 AP (donor's requirement, unchanged).
        new(
            "DA_Skill_Lune_VivifyingLight",
            "BP_Battle_SkillScript_VivifyingLight",
            "VivifyingLight",
            "DA_Skill_Lune_HealingLight",
            "BP_Battle_SkillScript_HealingLight",
            "HealingLight",
            4,
            "Vivifying Light",
            "<keyword id=\"Heal\">Heals</> the targeted ally by 40-70% Health and dispels Status Effects.\n" +
            "<keyword id=\"Consume_Earth\">Consumes</> <img id=\"Stain_Earth\"/><img id=\"Stain_Earth\"/>: Costs 0 <keyword id=\"APShard\">AP</>",
            "<keyword id=\"Heal\">Heals</> the targeted ally by 40-70% Health and dispels Status effects.\n" +
            "<keyword id=\"Consume_Earth\">Consumes</> <img id=\"Stain_Earth\"/><img id=\"Stain_Earth\"/>: Costs 0 <keyword id=\"APShard\">AP</>",
            new[]
            {
                ("HealingLight_BaseHeal", "VivifyingLight_BaseHeal", (double?)0.4),
                ("HealingLight_HealIncreaseForEachQTE", "VivifyingLight_HealIncreaseForEachQTE", (double?)0.075),
                ("HealingLight_OverchargeAPCost", "VivifyingLight_OverchargeAPCost", (double?)0.0),
            },
                        "Revitalization",
            // Top row (Y = 125, same as Revitalization): the chain extends to the
            // right of the tree's top node, clear of every existing node. (1550,700)
            // landed down-right and rendered behind/below other nodes in-game.
            new FVector2D(600.0f, 425.0f)),

        // Restoration: Revitalization variant — 8 AP, heals 40-70% (base 0.4 + 15%/QTE),
        // overcharge 3 Light → +30% Health (kismet insert; donor Regen stays on overcharge).
        new(
            "DA_Skill_Lune_Restoration",
            "BP_Battle_SkillScript_Restoration",
            "Restoration",
            "DA_Skill_Lune_Revitalization",
            "BP_Battle_SkillScript_Revitalization",
            "Revitalization",
            8,
            "Restoration",
            "<keyword id=\"Heal\">Heals</> 1-5 allies by 40-70% Health.\n" +
            "Consumes <img id=\"Stain_Light\"/><img id=\"Stain_Light\"/><img id=\"Stain_Light\"/> to also apply <keyword id=\"Buff_Regen\">Regen</> that heals on turn start for 3 turns, and heal an additional 30% Health.",
            "<keyword id=\"Heal\">Heals</> 1-5 allies by 40-70% Health.\n" +
            "Consumes <img id=\"Stain_Light\"/><img id=\"Stain_Light\"/><img id=\"Stain_Light\"/>: applies <keyword id=\"Buff_Regen\">Regen</> for 3 turns and heals an additional 30% Health.",
            new[]
            {
                ("Revitalization_BaseHealPercent", "Restoration_BaseHealPercent", (double?)0.4),
                ("Revitalization_HealIncreasePerQte", "Restoration_HealIncreasePerQte", (double?)0.15),
            },
            "VivifyingLight",
            new FVector2D(725.0f, 125.0f),
            OverchargeRequiredStains: new Dictionary<string, int> { ["Light"] = 3 },
            Kismet: new KismetEdits(OverchargeBonusHeal: true)),

        // Chiaroscuro: Ice Lance (IceGust) variant — 11 AP, 1 hit, base 3.030 so
        // ×2.0 overcharge = 6.06 = dark rule ×1.25 over clamp(11); overcharge
        // 1 Light + 1 Dark; random Light/Dark stain generation (kismet); Light FX.
        new(
            "DA_Skill_Lune_Chiaroscuro",
            "BP_Battle_SkillScript_Chiaroscuro",
            "Chiaroscuro",
            "DA_Skill_Lune_IceGust",
            "BP_Battle_SkillScript_IceGust",
            "IceGust",
            11,
            "Chiaroscuro",
            "Deals <keyword id=\"DamageMultiplier\">3.03x</> single target <keyword id=\"Element_Light\">Light</> damage that <keyword id=\"Buff_Slow\">Slows</> the target for 3 turns. 1 hit.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Light\"/><img id=\"Stain_Dark\"/>: deal <keyword id=\"DamageMultiplier\">2.00x</> more damage.",
            "Deals <keyword id=\"DamageMultiplier\">3.03x</> <keyword id=\"Element_Light\">Light</> damage. <keyword id=\"Buff_Slow\">Slows</> the target for 3 turns. 1 hit.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Light\"/><img id=\"Stain_Dark\"/>: deal <keyword id=\"DamageMultiplier\">2.00x</> more damage.",
            new[]
            {
                ("IceGust_BaseDamage", "Chiaroscuro_BaseDamage", (double?)3.0301),
                ("IceGust_OverchargeDamageMultiplier", "Chiaroscuro_OverchargeDamageMultiplier", (double?)2.0),
                ("IceGust_BreakBarMultiplier", "Chiaroscuro_BreakBarMultiplier", null),
            },
            "Restoration",
            new FVector2D(850.0f, 325.0f),
            OverchargeRequiredStains: new Dictionary<string, int> { ["Light"] = 1, ["Dark"] = 1 },
            // Widget reads ProducedStains for the top-right generate icons; the kismet
            // RandomGenerate branch still emits only one of the two at runtime.
            ProducedStains: new Dictionary<string, int> { ["Light"] = 1, ["Dark"] = 1 },
            Kismet: new KismetEdits(RandomGenerate: "Light|Dark", DamageElement: "Light", CustomDarkConsume: true),
            ExtraPairs: new[]
            {
                ("ElementalFX/Ice/FX_ExplosionIce", "ElementalFX/Light/FX_ExplosionLight"),
                ("FX_ExplosionIce", "FX_ExplosionLight"),
            },
            SequenceStem: "SEQ_SkillLune_IceGust",
            SequenceNewStem: "SEQ_SkillLune_Chiaroscuro",
            RemoveVoice: true,
            MechanicStem: StainUiKit.EitherOrMechanic),

        // Lumière Zénithale: Lightning Dance variant — 15 AP, 6 hits, base 0.211 so
        // 0.211 × 6 × 10.0 overcharge = 12.66 = clamp(15) (4 stains → 90% from
        // overcharge); overcharge 2 Light + 2 Lightning; generates 1 Light + 1 Dark; Light FX.
        new(
            "DA_Skill_Lune_LumiereZenithale",
            "BP_Battle_SkillScript_LumiereZenithale",
            "LumiereZenithale",
            "DA_Skill_Lune_LightningDance",
            "BP_Battle_SkillScript_LightningDance",
            "LightningDance",
            15,
            "Lumière Zénithale",
            "Deals <keyword id=\"DamageMultiplier\">0.21x</> single target <keyword id=\"Element_Light\">Light</> damage per hit. 6 hits.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Light\"/><img id=\"Stain_Light\"/><img id=\"Stain_Lightning\"/><img id=\"Stain_Lightning\"/>: deal <keyword id=\"DamageMultiplier\">10.00x</> more damage.",
            "Deals <keyword id=\"DamageMultiplier\">0.21x</> <keyword id=\"Element_Light\">Light</> damage per hit. 6 hits.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Light\"/><img id=\"Stain_Light\"/><img id=\"Stain_Lightning\"/><img id=\"Stain_Lightning\"/>: deal <keyword id=\"DamageMultiplier\">10.00x</> more damage.",
            new[]
            {
                ("LightningDance_BaseDamage", "LumiereZenithale_BaseDamage", (double?)0.211),
                ("LightningDance_OverchargeDamageMultiplier", "LumiereZenithale_OverchargeDamageMultiplier", (double?)10.0),
                ("LightningDance_BreakBarMultiplier", "LumiereZenithale_BreakBarMultiplier", null),
            },
            "Chiaroscuro",
            new FVector2D(350.0f, 625.0f),
            OverchargeRequiredStains: new Dictionary<string, int> { ["Light"] = 2, ["Lightning"] = 2 },
            ProducedStains: new Dictionary<string, int> { ["Light"] = 1, ["Dark"] = 1 },
            Kismet: new KismetEdits(DamageElement: "Light"),
            ExtraPairs: new[]
            {
                ("ElementalFX/Lightning/FX_ExplosionLightning", "ElementalFX/Light/FX_ExplosionLight"),
                ("FX_ExplosionLightning", "FX_ExplosionLight"),
            },
            SequenceStem: "SEQ_SkillLune_NEW_LightningDance",
            SequenceNewStem: "SEQ_SkillLune_LumiereZenithale",
            RemoveVoice: true,
            SeqRetargets: new[]
            {
                ("ElementalFX/Lightning/FX_ExplosionLightning", "ElementalFX/Light/FX_ExplosionLight"),
                ("FX_ExplosionLightning", "FX_ExplosionLight"),
                ("ElementalFX/Lightning/FX_LightningRework", "ElementalFX/Light/FX_WorldLightTrail"),
                ("FX_LightningRework", "FX_WorldLightTrail"),
                ("Trails/Trail_Paint_OG/FX_LightningTrail", "ElementalFX/Light/FX_WorldLightTrail"),
                ("FX_LightningTrail", "FX_WorldLightTrail"),
            }),

        // Shocking Wave: Crippling Tsunami variant — 6 AP, 1 hit, Lightning. Base does no
        // status; consuming 2 Lightning boosts damage (0.99 × 2.0 = 1.98 = clamp(6)) and
        // applies Slow + Defenseless + Powerless. Generates 1 Ice (donor generated Ice+Light).
        new(
            "DA_Skill_Lune_ShockingWave",
            "BP_Battle_SkillScript_ShockingWave",
            "ShockingWave",
            "DA_Skill_Lune_CripplingTsunami",
            "BP_Battle_SkillScript_CripplingTsunami",
            "CripplingTsunami",
            6,
            "Shocking Wave",
            "Deals <keyword id=\"DamageMultiplier\">0.99x</> single target <keyword id=\"Element_Lightning\">Lightning</> damage. 1 hit.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Lightning\"/><img id=\"Stain_Lightning\"/>: deal <keyword id=\"DamageMultiplier\">1.98x</> more damage and inflicts <keyword id=\"Buff_Slow\">Slow</>, <keyword id=\"Buff_Defenseless\">Defenceless</> and <keyword id=\"Buff_Powerless\">Powerless</> for 3 turns.",
            "Deals <keyword id=\"DamageMultiplier\">0.99x</> <keyword id=\"Element_Lightning\">Lightning</> damage. 1 hit.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Lightning\"/><img id=\"Stain_Lightning\"/>: deal <keyword id=\"DamageMultiplier\">1.98x</> more damage and inflicts <keyword id=\"Buff_Slow\">Slow</>, <keyword id=\"Buff_Defenseless\">Defenceless</> and <keyword id=\"Buff_Powerless\">Powerless</> for 3 turns.",
            new[]
            {
                ("CripplingTsunami_BaseDamage", "ShockingWave_BaseDamage", (double?)0.99),
                ("CripplingTsunami_OverchargeDamageMultiplier", "ShockingWave_OverchargeDamageMultiplier", (double?)2.0),
                ("CripplingTsunami_BreakBarMultiplier", "ShockingWave_BreakBarMultiplier", null),
                ("CripplingTsunami_SlowDuration", "ShockingWave_SlowDuration", null),
            },
            "CripplingTsunami",
            new FVector2D(850.0f, 625.0f),
            OverchargeRequiredStains: new Dictionary<string, int> { ["Lightning"] = 2 },
            ProducedStains: new Dictionary<string, int> { ["Ice"] = 1 },
            Kismet: new KismetEdits(DamageElement: "Lightning", StatusOnOvercharge: "Slow|Defenseless|Powerless"),
            ExtraPairs: new[]
            {
                ("FX_ExplosionIce", "FX_ExplosionLightning"),
            },
            SequenceStem: "SEQ_LuneSkill_30_CripplingTsunami",
            SequenceNewStem: "SEQ_LuneSkill_ShockingWave",
            RemoveVoice: true),

        // Infernal Rage: Fire Rage variant — 8 AP, darker red + larger fire FX. Consumes 3 Ice
        // for the ramp boost: first hit 0.66 × 2.0 = 1.32 = clamp(4); by turn 5 with overcharge
        // 1.46 × 2.109 × 2.0 = 6.16 = clamp(12), then tails off. Generates 2 Fire (donor Fire+Light).
        new(
            "DA_Skill_Lune_InfernalRage",
            "BP_Battle_SkillScript_InfernalRage",
            "InfernalRage",
            "DA_Skill_Lune_FireRage",
            "BP_Battle_SkillScript_FireRage",
            "FireRage",
            8,
            "Infernal Rage",
            "Deals <keyword id=\"DamageMultiplier\">1.32x</> <keyword id=\"Element_Fire\">Fire</> damage on the first turn, ramping up to <keyword id=\"DamageMultiplier\">6.16x</> by the fifth turn.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/>: deal <keyword id=\"DamageMultiplier\">2.00x</> more damage.\n" +
            "Stuns self for 2 turns if interrupted.",
            "Deals <keyword id=\"DamageMultiplier\">1.32x</> <keyword id=\"Element_Fire\">Fire</> damage on the first turn, ramping up to <keyword id=\"DamageMultiplier\">6.16x</> by the fifth turn.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/>: deal <keyword id=\"DamageMultiplier\">2.00x</> more damage.\n" +
            "Stuns self for 2 turns if interrupted.",
            new[]
            {
                ("FireRage_BaseDamage", "InfernalRage_BaseDamage", (double?)1.46),
                ("FireRage_FirstHit", "InfernalRage_FirstHit", (double?)0.66),
                ("FireRage_OverchargeDamageMultiplier", "InfernalRage_OverchargeDamageMultiplier", (double?)2.0),
                ("FireRage_DamageMultiplierPerTurn", "InfernalRage_DamageMultiplierPerTurn", (double?)1.5),
                ("FireRage_DamagePerTurnMultiplierDeceleration", "InfernalRage_DamagePerTurnMultiplierDeceleration", (double?)0.5),
                ("FireRage_MaxDamageMultiplierStack", "InfernalRage_MaxDamageMultiplierStack", (double?)2.5),
                ("FireRage_BreakBarMultiplier", "InfernalRage_BreakBarMultiplier", null),
            },
            "FireRage",
            new FVector2D(225.0f, 325.0f),
            OverchargeRequiredStains: new Dictionary<string, int> { ["Ice"] = 3 },
            ProducedStains: new Dictionary<string, int> { ["Fire"] = 2 },
            Kismet: new KismetEdits(DarkerFire: true, QteFailStunTurns: 2),
            ExtraPairs: new[]
            {
                // Point the taken-damage buff at the 2-turn clone (see PatchInfernalRageBuff).
                ("BP_Buff_FireRage_HasTakenAnyDamage", "BP_Buff_InfernalRage_HasTakenAnyDamage"),
            },
            SequenceStem: "SEQ_Lune_Skill_FireRage",
            SequenceNewStem: "SEQ_Lune_Skill_InfernalRage",
            ExtraSequences: new[]
            {
                ("SEQ_Lune_Skill_FireRage_Charge", "SEQ_Lune_Skill_InfernalRage_Charge"),
            },
            RemoveVoice: true),

        // Sanguinary Flames: Infernal Rage variant — 12 AP, the family's peak. Consumes 4 Ice
        // (Light substitutes, vanilla consume) for ×2.0. Same ramp shape (×1.5/turn, decel ×0.5,
        // cap 2.5) scaled 1.5× per AP vs Infernal Rage: first hit 1.0 × 2.0 = 2.00; turn 5
        // 2.2 × 2.109 × 2.0 = 9.28. Generates 3 Fire. Interrupt (missed QTE or taking damage)
        // stuns Lune 3 turns — QTE path via kismet, damage path via the cloned buff.
        new(
            "DA_Skill_Lune_SanguinaryFlames",
            "BP_Battle_SkillScript_SanguinaryFlames",
            "SanguinaryFlames",
            "DA_Skill_Lune_FireRage",
            "BP_Battle_SkillScript_FireRage",
            "FireRage",
            12,
            "Sanguinary Flames",
            "Deals <keyword id=\"DamageMultiplier\">2.00x</> <keyword id=\"Element_Fire\">Fire</> damage on the first turn, ramping up to <keyword id=\"DamageMultiplier\">9.28x</> by the fifth turn.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/>: deal <keyword id=\"DamageMultiplier\">2.00x</> more damage.\n" +
            "Stuns self for 3 turns if interrupted.",
            "Deals <keyword id=\"DamageMultiplier\">2.00x</> <keyword id=\"Element_Fire\">Fire</> damage on the first turn, ramping up to <keyword id=\"DamageMultiplier\">9.28x</> by the fifth turn.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/>: deal <keyword id=\"DamageMultiplier\">2.00x</> more damage.\n" +
            "Stuns self for 3 turns if interrupted.",
            new[]
            {
                ("FireRage_BaseDamage", "SanguinaryFlames_BaseDamage", (double?)2.2),
                ("FireRage_FirstHit", "SanguinaryFlames_FirstHit", (double?)1.0),
                ("FireRage_OverchargeDamageMultiplier", "SanguinaryFlames_OverchargeDamageMultiplier", (double?)2.0),
                ("FireRage_DamageMultiplierPerTurn", "SanguinaryFlames_DamageMultiplierPerTurn", (double?)1.5),
                ("FireRage_DamagePerTurnMultiplierDeceleration", "SanguinaryFlames_DamagePerTurnMultiplierDeceleration", (double?)0.5),
                ("FireRage_MaxDamageMultiplierStack", "SanguinaryFlames_MaxDamageMultiplierStack", (double?)2.5),
                ("FireRage_BreakBarMultiplier", "SanguinaryFlames_BreakBarMultiplier", null),
            },
            "InfernalRage",
            new FVector2D(325.0f, 325.0f),
            OverchargeRequiredStains: new Dictionary<string, int> { ["Ice"] = 4 },
            ProducedStains: new Dictionary<string, int> { ["Fire"] = 3 },
            Kismet: new KismetEdits(DarkerFire: true, QteFailStunTurns: 3),
            ExtraPairs: new[]
            {
                ("BP_Buff_FireRage_HasTakenAnyDamage", "BP_Buff_SanguinaryFlames_HasTakenAnyDamage"),
            },
            SequenceStem: "SEQ_Lune_Skill_FireRage",
            SequenceNewStem: "SEQ_Lune_Skill_SanguinaryFlames",
            ExtraSequences: new[]
            {
                ("SEQ_Lune_Skill_FireRage_Charge", "SEQ_Lune_Skill_SanguinaryFlames_Charge"),
            },
            RemoveVoice: true),

        // Combustion: Immolation variant — 14 AP, 1 hit. Overcharge consumes 3 Earth
        // (Light substitutes, vanilla consume) to generate 1 Dark and deal ×3.33:
        // base 2.988 × 3.33 = 9.96 = clamp(14) (3 stains → 70% from overcharge).
        // Applies 5 Burn, 15 if the target is already burning (kismet keys on Burn,
        // not the donor's Marked check). No default stain generation — the Dark is an
        // explicit overcharge consequence (CreateStainByType in the consume block), and
        // ProducedStains stays empty so the widget shows no permanent Dark icon.
        // DropOverchargeOnBuff removes the donor's any-buff overcharge (export + all
        // references) so the consume is the only gate. BiggerFlames scales the
        // cloned sequence's fire FX (User.Size ×3, Lifetime ×1.5).
        new(
            "DA_Skill_Lune_Combustion",
            "BP_Battle_SkillScript_Combustion",
            "Combustion",
            "DA_Skill_Lune_Immolation",
            "BP_Battle_SkillScript_Immolation",
            "Immolation",
            14,
            "Combustion",
            "Deals <keyword id=\"DamageMultiplier\">2.99x</> <keyword id=\"Element_Fire\">Fire</> damage and 5 <keyword id=\"StatusEffect_Burn\">Burn</>. 1 hit.\n" +
            "Applies 10 more <keyword id=\"StatusEffect_Burn\">Burns</> if the target is already burning.\n" +
            "<keyword id=\"Consume_Earth\">Consumes</> <img id=\"Stain_Earth\"/><img id=\"Stain_Earth\"/><img id=\"Stain_Earth\"/>: deal <keyword id=\"DamageMultiplier\">3.33x</> more damage and generates 1 <img id=\"Stain_Dark\"/>.",
            "Deals <keyword id=\"DamageMultiplier\">2.99x</> <keyword id=\"Element_Fire\">Fire</> damage and 5 <keyword id=\"StatusEffect_Burn\">Burn</>. 1 hit.\n" +
            "Applies 10 more <keyword id=\"StatusEffect_Burn\">Burns</> if the target is already burning.\n" +
            "<keyword id=\"Consume_Earth\">Consumes</> <img id=\"Stain_Earth\"/><img id=\"Stain_Earth\"/><img id=\"Stain_Earth\"/>: deal <keyword id=\"DamageMultiplier\">3.33x</> more damage and generates 1 <img id=\"Stain_Dark\"/>.",
            new[]
            {
                ("Immolation_BaseDamage", "Combustion_BaseDamage", (double?)2.988),
                ("Immolation_OverchargeDamageMultiplier", "Combustion_OverchargeDamageMultiplier", (double?)3.33),
                ("Immolation_BaseBurnToApply", "Combustion_BaseBurnToApply", (double?)5.0),
                ("Immolation_OverchargeBurnStacks", "Combustion_OverchargeBurnStacks", (double?)15.0),
                ("Immolation_BurnMultiplier", "Combustion_BurnMultiplier", null),
                ("Immolation_BreakBarMultiplier", "Combustion_BreakBarMultiplier", null),
            },
            "Immolation",
            new FVector2D(350.0f, 325.0f),
            OverchargeRequiredStains: new Dictionary<string, int> { ["Earth"] = 3 },
            ProducedStains: new Dictionary<string, int>(),
            Kismet: new KismetEdits(AlreadyBurning: true, ConsumeGeneratesDark: true),
            SequenceStem: "SEQ_LuneSkill_Immolation",
            SequenceNewStem: "SEQ_LuneSkill_Combustion",
            RemoveVoice: true,
            DropOverchargeOnBuff: true,
            BiggerFlames: true),

        // Blitz Overcharge: Mayhem variant — 9 AP. Requires >=1 Lightning stain; every consumed
        // stain is 3 real DealDamages (4 stains = 12 hits). Clamp(9) = 0.33×9 = 2.97.
        // Backloaded tiers 0.05/0.05/0.1/0.25/1.0: totals 3×0.2475×0.05 = 0.04,
        // 6×0.2475×0.1 = 0.15, 9×0.2475×0.25 = 0.56, 12×0.2475×1.0 = 2.97.
        // Generates 1 Light per consumed stain. Impacts are Lightning.
        new(
            "DA_Skill_Lune_BlitzOvercharge",
            "BP_Battle_SkillScript_BlitzOvercharge",
            "BlitzOvercharge",
            "DA_Skill_Lune_Mayhem",
            "BP_Battle_SkillScript_Mayhem",
            "Mayhem",
            9,
            "Blitz Overcharge",
            "Deals <keyword id=\"DamageMultiplier\">0.04x</> to <keyword id=\"DamageMultiplier\">2.97x</> <keyword id=\"Element_Lightning\">Lightning</> damage, striking 3 times per <img id=\"Stain_Lightning\"/> consumed. Consuming 4 <img id=\"Stain_Lightning\"/>: deal <keyword id=\"DamageMultiplier\">2.97x</> damage.\nGenerates 1 <img id=\"Stain_Light\"/> per stain consumed.",
            "Deals <keyword id=\"DamageMultiplier\">0.04x</> to <keyword id=\"DamageMultiplier\">2.97x</> <keyword id=\"Element_Lightning\">Lightning</> damage, striking 3 times per <img id=\"Stain_Lightning\"/>.\nGenerates 1 <img id=\"Stain_Light\"/> per stain consumed.",
            new[]
            {
                ("Mayhem_BaseDamage", "BlitzOvercharge_BaseDamage", (double?)0.2475),
                ("Mayhem_ZeroStainDamageMultiplier", "BlitzOvercharge_ZeroStainDamageMultiplier", (double?)0.05),
                ("Mayhem_OneStainDamageMultiplier", "BlitzOvercharge_OneStainDamageMultiplier", (double?)0.05),
                ("Mayhem_TwoStainsDamageMultiplier", "BlitzOvercharge_TwoStainsDamageMultiplier", (double?)0.1),
                ("Mayhem_ThreeStainsDamageMultiplier", "BlitzOvercharge_ThreeStainsDamageMultiplier", (double?)0.25),
                ("Mayhem_FourStainsDamageMultiplier", "BlitzOvercharge_FourStainsDamageMultiplier", (double?)1.0),
                ("Mayhem_BreakBarMultiplier", "BlitzOvercharge_BreakBarMultiplier", null),
            },
            "Mayhem",
            new FVector2D(350.0f, 525.0f),
            Kismet: new KismetEdits(TripleDamage: true, LightningOnly: true,
                GenerateLightPerConsume: true, NoLightSubstitution: true),
            SequenceStem: "SEQ_LuneSkill_Mayhem",
            SequenceNewStem: "SEQ_LuneSkill_BlitzOvercharge",
            ChargeColor: (1.0f, 0.932f, 0.389f),
            RemoveVoice: true),

        // Elemental Cataclysm: Elemental Genesis variant — 16 AP, the 4-element hard
        // cast requirement with NO Light substitution (ValidateSkillAvailability patched).
        // 8 hits: base 2.4145 × 8 = 19.32 = 4-elemental rule ×1.2 over clamp(16).
        new(
            "DA_Skill_Lune_ElementalCataclysm",
            "BP_Battle_SkillScript_ElementalCataclysm",
            "ElementalCataclysm",
            "DA_Skill_Lune_ElementalGenesis",
            "BP_Battle_SkillScript_ElementalGenesis",
            "ElementalGenesis",
            16,
            "Elemental Cataclysm",
            "Deals <keyword id=\"DamageMultiplier\">2.41x</> damage per hit to all enemies. 8 hits.\n" +
            "Each hit deals damage in a random element.\n" +
            "Can only be cast with <img id=\"Stain_Fire\"/><img id=\"Stain_Ice\"/><img id=\"Stain_Lightning\"/><img id=\"Stain_Earth\"/>.",
            "Deals <keyword id=\"DamageMultiplier\">2.41x</> damage per hit to all enemies. 8 hits.\n" +
            "Each hit deals damage in a random element.\n" +
            "Can only be cast with <img id=\"Stain_Fire\"/><img id=\"Stain_Ice\"/><img id=\"Stain_Lightning\"/><img id=\"Stain_Earth\"/>.",
            new[]
            {
                ("ElementalGenesis_BaseDamage", "ElementalCataclysm_BaseDamage", (double?)2.4145),
                ("ElementalGenesis_BreakBarMultiplier", "ElementalCataclysm_BreakBarMultiplier", null),
            },
            "ElementalGenesis",
            new FVector2D(600.0f, 825.0f),
            OverchargeRequiredStains: new Dictionary<string, int> { ["Fire"] = 1, ["Ice"] = 1, ["Lightning"] = 1, ["Earth"] = 1 },
            Kismet: new KismetEdits(NoLightSubstitution: true)),
            // Do NOT clone SEQ_SkillLune_ElementalGenesis. UAssetAPI cannot parse its
            // BP_FX_ElementalShot exports; Open+Write produces a sequence that crashes
            // the game at save load once the Lune graph pulls the DA in.
            // The Cataclysm BP keeps the donor's vanilla Genesis sequence reference.

        // Cursed Earth: Earthquake (Earth Rising) variant — 12 AP, 1 hit AoE. Overcharge
        // consumes 2 Earth + 1 Dark via direct ConsumeStainByType (no substitution, Dark
        // reliably eaten) for ×4.0: 1.926 × 4.0 = 7.70 = dark rule ×1.25 over clamp(12).
        // Earth break 1.3 × base = 2.50. Darkness impact FX.
        new(
            "DA_Skill_Lune_CursedEarth",
            "BP_Battle_SkillScript_CursedEarth",
            "CursedEarth",
            "DA_Skill_Lune_EarthQuake",
            "BP_Battle_SkillScript_Earthquake",
            "Earthquake",
            12,
            "Cursed Earth",
            "Deals <keyword id=\"DamageMultiplier\">1.93x</> <keyword id=\"Element_Earth\">Earth</> damage and <keyword id=\"DamageMultiplier\">2.50x</> <keyword id=\"BreakDamage\">break damage</> to all enemies. 1 hit.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Earth\"/><img id=\"Stain_Earth\"/><img id=\"Stain_Dark\"/>: deal <keyword id=\"DamageMultiplier\">4.00x</> more damage.",
            "Deals <keyword id=\"DamageMultiplier\">1.93x</> <keyword id=\"Element_Earth\">Earth</> damage and <keyword id=\"DamageMultiplier\">2.50x</> <keyword id=\"BreakDamage\">break damage</> to all enemies. 1 hit.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Earth\"/><img id=\"Stain_Earth\"/><img id=\"Stain_Dark\"/>: deal <keyword id=\"DamageMultiplier\">4.00x</> more damage.",
            new[]
            {
                ("Earthquake_BaseDamage", "CursedEarth_BaseDamage", (double?)1.926),
                ("Earthquake_OverchargeDamageMultiplier", "CursedEarth_OverchargeDamageMultiplier", (double?)4.0),
                ("Earthquake_BreakBarMultiplier", "CursedEarth_BreakBarMultiplier", (double?)1.3),
            },
            "EarthQuake",
            new FVector2D(850.0f, 525.0f),
            OverchargeRequiredStains: new Dictionary<string, int> { ["Earth"] = 2, ["Dark"] = 1 },
            Kismet: new KismetEdits(CustomConsume: "Earth|Earth|Dark"),
            ExtraPairs: new[]
            {
                ("ElementalFX/Earth/FX_ExplosionEarth", "ElementalFX/Dark/FX_ExplosionDark"),
                ("FX_ExplosionEarth", "FX_ExplosionDark"),
            },
            SequenceStem: "SEQ_SkillLune_EarthRising",
            SequenceNewStem: "SEQ_SkillLune_CursedEarth",
            RemoveVoice: true),

        // Gravity Mill: Typhoon variant — 18 AP, direct earth nuke (no zone, no heal).
        // Overcharge consumes 2 Earth + 2 Lightning for ×10.0 and Defenseless 3 turns:
        // 2.6013 × 10.0 = 26.01 = clamp(18) (4 stains → 90% from overcharge). Earth
        // break 1.3 × base = 3.38. Rock/grey FX.
        new(
            "DA_Skill_Lune_GravityMill",
            "BP_Battle_SkillScript_GravityMill",
            "GravityMill",
            "DA_Skill_Lune_Typhoon",
            "BP_Battle_SkillScript_Typhoon",
            "Typhoon",
            18,
            "Gravity Mill",
            "Deals <keyword id=\"DamageMultiplier\">2.60x</> <keyword id=\"Element_Earth\">Earth</> damage and <keyword id=\"DamageMultiplier\">3.38x</> <keyword id=\"BreakDamage\">break damage</> to all enemies. 1 hit.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Earth\"/><img id=\"Stain_Earth\"/><img id=\"Stain_Lightning\"/><img id=\"Stain_Lightning\"/>: deal <keyword id=\"DamageMultiplier\">10.00x</> more damage and inflicts <keyword id=\"Buff_Defenseless\">Defenceless</> for 3 turns.",
            "Deals <keyword id=\"DamageMultiplier\">2.60x</> <keyword id=\"Element_Earth\">Earth</> damage and <keyword id=\"DamageMultiplier\">3.38x</> <keyword id=\"BreakDamage\">break damage</> to all enemies. 1 hit.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Earth\"/><img id=\"Stain_Earth\"/><img id=\"Stain_Lightning\"/><img id=\"Stain_Lightning\"/>: deal <keyword id=\"DamageMultiplier\">10.00x</> more damage and inflicts <keyword id=\"Buff_Defenseless\">Defenceless</> for 3 turns.",
            new[]
            {
                ("Typhoon_InitialDamage", "GravityMill_InitialDamage", (double?)2.6013),
                ("Typhoon_BreakBarMultiplier", "GravityMill_BreakBarMultiplier", (double?)1.3),
                ("Typhoon_BaseDuration", "GravityMill_BaseDuration", null),
                ("Typhoon_OverchargeDuration", "GravityMill_OverchargeDuration", null),
                ("Typhoon_HealPercent", "GravityMill_HealPercent", (double?)0.0),
            },
            "Typhoon",
            new FVector2D(850.0f, 725.0f),
            OverchargeRequiredStains: new Dictionary<string, int> { ["Earth"] = 2, ["Lightning"] = 2 },
            ProducedStains: new Dictionary<string, int> { ["Earth"] = 1, ["Dark"] = 1 },
            Kismet: new KismetEdits(DamageElement: "Earth", CustomConsume: "Earth|Earth|Lightning|Lightning", DirectNuke: true, OverchargeDamageMultiplier: 10.0, DefenselessTurns: 3),
            ExtraPairs: new[]
            {
                ("FX_Test01Burst", "FX_ExplosionEarth"),
            },
            SequenceStem: "SEQ_LuneSkill_Typhoon",
            SequenceNewStem: "SEQ_LuneSkill_GravityMill",
            RemoveVoice: true,
            SeqRetargets: new[]
            {
                ("ElementalFX/Ice/FX_ExplosionIce", "ElementalFX/Earth/FX_ExplosionEarth"),
                ("FX_ExplosionIce", "FX_ExplosionEarth"),
                ("ElementalFX/Ice/FX_WorldIceTrail", "ElementalFX/Earth/FX_WorldEarthTrail"),
                ("FX_WorldIceTrail", "FX_WorldEarthTrail"),
                ("FX_TornadoPaint", "FX_WindFence"),
            }),

        // Maelstrom: Storm Caller variant — 10 AP Lightning storm zone. Consumes 2 Ice +
        // 1 Lightning to overcharge: the storm lasts 1 turn base (3 strikes) or 5 turns
        // overcharged (5 strikes/turn). Generates 1 Lightning. Zone move — no overcharge
        // damage multiplier (docs exception); every strike = MaelstromPerStrike
        // (clamp(10)/16 = 0.2384), shared single source with the rows below so the
        // description can never go stale when the value changes.
        // Own zone buff (BP_BattleBuff_Maelstrom, cloned): the buff's hardcoded
        // StormCaller_BaseDamage read is retargeted to Maelstrom_BaseDamage, and the
        // skill BP spawns the clone — Storm Caller keeps its own row.
        new(
            "DA_Skill_Lune_Maelstrom",
            "BP_Battle_SkillScript_Maelstrom",
            "Maelstrom",
            "DA_Skill_Lune_StormCaller",
            "BP_Battle_SkillScript_StormCaller",
            "StormCaller",
            10,
            "Maelstrom",
            "Calls a Lightning storm that strikes 3 times each turn. All enemies receive " + SkillDescriptionsKit.Gold(SkillOverhaulKit.MaelstromPerStrike) + " <keyword id=\"Element_Lightning\">Lightning</> damage per strike at the end of their turn, and " + SkillDescriptionsKit.Gold(SkillOverhaulKit.MaelstromPerStrike) + " <keyword id=\"Element_Lightning\">Lightning</> damage per strike when they receive damage.\nDuration: 3 turns.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/><img id=\"Stain_Lightning\"/>: the storm strikes 5 times each turn.",
            "Calls a Lightning storm (3 strikes each turn). All enemies receive " + SkillDescriptionsKit.Gold(SkillOverhaulKit.MaelstromPerStrike) + " <keyword id=\"Element_Lightning\">Lightning</> damage per strike on turn end and " + SkillDescriptionsKit.Gold(SkillOverhaulKit.MaelstromPerStrike) + " <keyword id=\"Element_Lightning\">Lightning</> damage per strike on damage received.\nDuration: 3 turns.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/><img id=\"Stain_Lightning\"/>: 5 strikes each turn.",
            new[]
            {
                ("StormCaller_BaseAmountOfHits", "Maelstrom_BaseAmountOfHits", (double?)3.0),
                ("StormCaller_OverchargeAmountOfHits", "Maelstrom_OverchargeAmountOfHits", (double?)5.0),
                ("StormCaller_BaseDamageOnHit", "Maelstrom_BaseDamageOnHit", (double?)SkillOverhaulKit.MaelstromPerStrike),
                ("StormCaller_BaseDamage", "Maelstrom_BaseDamage", (double?)SkillOverhaulKit.MaelstromPerStrike),
            },
            "StormCaller",
            new FVector2D(1350.0f, 125.0f),
            OverchargeRequiredStains: new Dictionary<string, int> { ["Ice"] = 2, ["Lightning"] = 1 },
            ProducedStains: new Dictionary<string, int> { ["Lightning"] = 1 },
            Kismet: new KismetEdits(BaseStrikes: 3, OverchargeStrikes: 5),
            ExtraPairs: new[]
            {
                ("BP_BattleBuff_StormCaller", "BP_BattleBuff_Maelstrom"),
            },
            // Maelstrom damage-tick: own cloned zone buff WITH the row retarget.
            // Hook survival CONFIRMED live: the renamed clone keeps the native
            // on-damage-tick hook (one Lightning strike per damage tick, confirmed
            // in-game). The Sept A/B read was wrong — its crash came from the
            // reactor-spawn splice (OC-gated Maelstrom3/5 spawn inside the
            // storm-buff target loop; nested GetTargetedCharacters with a shared
            // index var), which stays removed.
            SequenceStem: "SEQ_SkillLune_StormCaller",
            RemoveVoice: true),

        // Levin Dance: Lightning Dance variant — 13 AP. Consumes 2 Earth + 1 Lightning +
        // 1 Ice to overcharge. Standard 4-stain rule-3 ×10 overcharge (the ×1.3 curve-beater:
        // base 0.1698 × ×10 × 6 core hits = 10.19 = clamp(13)×1.3). Native Lightning Dance
        // already chains 1 extra strike on crit; PatchExtraCritStrikes makes it 2 (flavor —
        // the overcharge payout is the ×10, not guaranteed crits). Generates 1 Lightning + 1 Ice.
        new(
            "DA_Skill_Lune_LevinDance",
            "BP_Battle_SkillScript_LevinDance",
            "LevinDance",
            "DA_Skill_Lune_LightningDance",
            "BP_Battle_SkillScript_LightningDance",
            "LightningDance",
            13,
            "Levin Dance",
            "Deals <keyword id=\"DamageMultiplier\">0.17x</> <keyword id=\"Element_Lightning\">Lightning</> damage per strike. 6 strikes.\n" +
            "<keyword id=\"CriticalHit\">Critical Hits</> chain 2 extra <keyword id=\"Element_Lightning\">Lightning</> strikes.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Earth\"/><img id=\"Stain_Earth\"/><img id=\"Stain_Lightning\"/><img id=\"Stain_Ice\"/>: deal <keyword id=\"DamageMultiplier\">10.00x</> more damage.",
            "Deals <keyword id=\"DamageMultiplier\">0.17x</> <keyword id=\"Element_Lightning\">Lightning</> damage per strike. 6 strikes.\n" +
            "<keyword id=\"CriticalHit\">Critical Hits</> chain 2 extra <keyword id=\"Element_Lightning\">Lightning</> strikes.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Earth\"/><img id=\"Stain_Earth\"/><img id=\"Stain_Lightning\"/><img id=\"Stain_Ice\"/>: deal <keyword id=\"DamageMultiplier\">10.00x</> more damage.",
            new[]
            {
                ("LightningDance_BaseDamage", "LevinDance_BaseDamage", (double?)0.1698),
                ("LightningDance_OverchargeDamageMultiplier", "LevinDance_OverchargeDamageMultiplier", (double?)10.0),
                ("LightningDance_BreakBarMultiplier", "LevinDance_BreakBarMultiplier", null),
            },
            "LightningDance",
            new FVector2D(1475.0f, 125.0f),
            OverchargeRequiredStains: new Dictionary<string, int> { ["Earth"] = 2, ["Lightning"] = 1, ["Ice"] = 1 },
            ProducedStains: new Dictionary<string, int> { ["Lightning"] = 1, ["Ice"] = 1 },
            Kismet: new KismetEdits(ExtraCritStrikes: 2),
            SequenceStem: "SEQ_SkillLune_NEW_LightningDance",
            RemoveVoice: true),

        // Cloud Splitter: Thunderfall variant — 20 AP AoE Lightning barrage. Consumes
        // 3 Lightning + 1 Ice to overcharge: the barrage spreads 8–24 strikes and
        // inflicts Greater Powerless for 2 turns (synthesized OC-gated ApplyBuff in
        // DealDamageAndFeedback). Generates 1 Lightning. 24-hit OC
        // total = clamp(20) ≈ 42.04 (×10 overcharge on the native Thunderfall damage
        // flow); per-hit = 42.04 ÷ 24 = 1.7517. Base (8 hits) ≈ 14.
        new(
            "DA_Skill_Lune_CloudSplitter",
            "BP_Battle_SkillScript_CloudSplitter",
            "CloudSplitter",
            "DA_Skill_Lune_Thunderfall",
            "BP_Battle_SkillScript_Thunderfall",
            "Thunderfall",
            20,
            "Cloud Splitter",
            "Deals <keyword id=\"DamageMultiplier\">1.75x</> <keyword id=\"Element_Lightning\">Lightning</> damage per hit to all enemies. 8 to 24 hits.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Lightning\"/><img id=\"Stain_Lightning\"/><img id=\"Stain_Lightning\"/><img id=\"Stain_Ice\"/>: deal <keyword id=\"DamageMultiplier\">10.00x</> more damage and inflicts <keyword id=\"Buff_Powerless\">Greater Powerless</> for 2 turns.",
            "Deals <keyword id=\"DamageMultiplier\">1.75x</> <keyword id=\"Element_Lightning\">Lightning</> damage per hit to all enemies. 8 to 24 hits.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Lightning\"/><img id=\"Stain_Lightning\"/><img id=\"Stain_Lightning\"/><img id=\"Stain_Ice\"/>: deal <keyword id=\"DamageMultiplier\">10.00x</> more damage, <keyword id=\"Buff_Powerless\">Greater Powerless</> for 2 turns.",
            new[]
            {
                ("Thunderfall_BaseDamage", "CloudSplitter_BaseDamage", (double?)1.7517),
                ("Thunderfall_OverchargeDamageMultiplier", "CloudSplitter_OverchargeDamageMultiplier", (double?)10.0),
                ("Thunderfall_BreakBarMultiplier", "CloudSplitter_BreakBarMultiplier", null),
            },
            "Thunderfall",
            new FVector2D(1600.0f, 125.0f),
            OverchargeRequiredStains: new Dictionary<string, int> { ["Lightning"] = 3, ["Ice"] = 1 },
            ProducedStains: new Dictionary<string, int> { ["Lightning"] = 1 },
            Kismet: new KismetEdits(GreaterPowerlessTurns: 2),
            SequenceStem: "SEQ_LuneSkill_30_Thunderfall_V2",
            RemoveVoice: true),

        // Frost Piercer: Ice Lance (IceGust) variant — 14 AP, single-target Ice spike.
        // Overcharge 3 Ice + 1 Light (4 stains → 90% from overcharge): base 0.996 × ×10
        // = 9.96 = clamp(14). Generates 1 Ice + 2 Light. OC-gated Greater Slow +
        // Greater Defenceless for 7 turns (the donor's always-on 3-turn Slow is removed;
        // the Greater pair sits in its slot behind the overcharge gate). Much larger FX.
        new(
            "DA_Skill_Lune_FrostPiercer",
            "BP_Battle_SkillScript_FrostPiercer",
            "FrostPiercer",
            "DA_Skill_Lune_IceGust",
            "BP_Battle_SkillScript_IceGust",
            "IceGust",
            14,
            "Frost Piercer",
            "Deals <keyword id=\"DamageMultiplier\">1.00x</> single target <keyword id=\"Element_Ice\">Ice</> damage. 1 hit.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/><img id=\"Stain_Light\"/>: deal <keyword id=\"DamageMultiplier\">10.00x</> more damage and inflicts <keyword id=\"Buff_Slow\">Greater Slow</> and <keyword id=\"Buff_Defenseless\">Greater Defenceless</> for 7 turns.",
            "Deals <keyword id=\"DamageMultiplier\">1.00x</> <keyword id=\"Element_Ice\">Ice</> damage. 1 hit.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/><img id=\"Stain_Light\"/>: deal <keyword id=\"DamageMultiplier\">10.00x</> more damage, <keyword id=\"Buff_Slow\">Greater Slow</> + <keyword id=\"Buff_Defenseless\">Greater Defenceless</> for 7 turns.",
            new[]
            {
                ("IceGust_BaseDamage", "FrostPiercer_BaseDamage", (double?)0.996),
                ("IceGust_OverchargeDamageMultiplier", "FrostPiercer_OverchargeDamageMultiplier", (double?)10.0),
                ("IceGust_BreakBarMultiplier", "FrostPiercer_BreakBarMultiplier", null),
            },
            "Chiaroscuro",
            new FVector2D(950.0f, 125.0f),
            OverchargeRequiredStains: new Dictionary<string, int> { ["Ice"] = 3, ["Light"] = 1 },
            ProducedStains: new Dictionary<string, int> { ["Ice"] = 1, ["Light"] = 2 },
            Kismet: new KismetEdits(StatusesOnHit: "GreaterSlow|GreaterDefenseless", StatusesOnHitTurns: 7,
                RemoveNativeStatus: true, ImpactScale: 4f),
            SequenceStem: "SEQ_SkillLune_IceGust",
            RemoveVoice: true),

        // Crushing Tsunami: Crippling Tsunami variant — 11 AP, AoE Ice. Overcharge
        // 2 Earth + 1 Fire (3 stains → 70% from overcharge): base 1.4545 × ×3.33
        // = 4.85 = clamp(11). Generates 2 Ice + 1 Light. OC-gated Greater Slow +
        // Defenceless for 3 turns (donor duration); donor's always-on Slow is dropped.
        new(
            "DA_Skill_Lune_CrushingTsunami",
            "BP_Battle_SkillScript_CrushingTsunami",
            "CrushingTsunami",
            "DA_Skill_Lune_CripplingTsunami",
            "BP_Battle_SkillScript_CripplingTsunami",
            "CripplingTsunami",
            11,
            "Crushing Tsunami",
            "Deals <keyword id=\"DamageMultiplier\">1.45x</> <keyword id=\"Element_Ice\">Ice</> damage to all enemies. 1 hit.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Earth\"/><img id=\"Stain_Earth\"/><img id=\"Stain_Fire\"/>: deal <keyword id=\"DamageMultiplier\">3.33x</> more damage and inflicts <keyword id=\"Buff_Slow\">Greater Slow</> and <keyword id=\"Buff_Defenseless\">Greater Defenceless</> for 3 turns.",
            "Deals <keyword id=\"DamageMultiplier\">1.45x</> <keyword id=\"Element_Ice\">Ice</> damage to all enemies. 1 hit.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Earth\"/><img id=\"Stain_Earth\"/><img id=\"Stain_Fire\"/>: deal <keyword id=\"DamageMultiplier\">3.33x</> more damage, <keyword id=\"Buff_Slow\">Greater Slow</> + <keyword id=\"Buff_Defenseless\">Greater Defenceless</> for 3 turns.",
            new[]
            {
                ("CripplingTsunami_BaseDamage", "CrushingTsunami_BaseDamage", (double?)1.4545),
                ("CripplingTsunami_OverchargeDamageMultiplier", "CrushingTsunami_OverchargeDamageMultiplier", (double?)3.33),
                ("CripplingTsunami_BreakBarMultiplier", "CrushingTsunami_BreakBarMultiplier", null),
                ("CripplingTsunami_SlowDuration", "CrushingTsunami_SlowDuration", null),
            },
            "CripplingTsunami",
            new FVector2D(1100.0f, 425.0f),
            OverchargeRequiredStains: new Dictionary<string, int> { ["Earth"] = 2, ["Fire"] = 1 },
            ProducedStains: new Dictionary<string, int> { ["Ice"] = 2, ["Light"] = 1 },
            Kismet: new KismetEdits(StatusesOnHit: "GreaterSlow|GreaterDefenseless", StatusesOnHitTurns: 3,
                RemoveNativeStatus: true, ImpactScale: 4f),
            SequenceStem: "SEQ_LuneSkill_30_CripplingTsunami",
            RemoveVoice: true),

        // Soul Eater: Thermal Transfer variant — 3 AP, single-target Ice. No overcharge
        // stains / no damage multiplier: always-on AP battery. The donor's +4-AP-on-burning
        // loop is extended to +4 AP per status the target has among Burn / Powerless /
        // Slow / Defenceless (max 16 AP), consumed on the final hit. The donor's overcharge
        // "Force Play Again" second turn is stripped so Soul Eater never grants a bonus turn.
        new(
            "DA_Skill_Lune_SoulEater",
            "BP_Battle_SkillScript_SoulEater",
            "SoulEater",
            "DA_Skill_Lune_ThermalTransfer",
            "BP_Battle_SkillScript_ThermalTransfer",
            "ThermalTransfer",
            3,
            "Soul Eater",
            "Deals <keyword id=\"DamageMultiplier\">0.50x</> single target <keyword id=\"Element_Ice\">Ice</> damage per hit. 2 hits.\n" +
            "On the final hit, gains 4 <keyword id=\"APShard\">AP</> for each of <keyword id=\"StatusEffect_Burn\">Burn</>, <keyword id=\"Buff_Powerless\">Powerless</>, <keyword id=\"Buff_Slow\">Slow</> and <keyword id=\"Buff_Defenseless\">Defenceless</> the target has (max 16 <keyword id=\"APShard\">AP</>) and consumes those statuses.",
            "Deals <keyword id=\"DamageMultiplier\">0.50x</> <keyword id=\"Element_Ice\">Ice</> damage per hit. 2 hits.\n" +
            "On the final hit, gains 4 <keyword id=\"APShard\">AP</> per status on the target (max 16) and consumes them.",
            new[]
            {
                ("ThermalTransfer_BaseDamage", "SoulEater_BaseDamage", (double?)0.495),
                ("ThermalTransfer_BreakBarMultiplier", "SoulEater_BreakBarMultiplier", null),
                ("ThermalTransfer_APGain", "SoulEater_APGain", (double?)4.0),
            },
            "ThermalTransfer",
            new FVector2D(1350.0f, 425.0f),
            OverchargeRequiredStains: new Dictionary<string, int>(),
            ProducedStains: new Dictionary<string, int>(),
            Kismet: new KismetEdits(ApGainStatuses: "Burn|Powerless|Slow|Defenseless", ApGainAlways: true,
                StripForcePlayAgain: true),
            SequenceStem: "SEQ_SkillLune_ThermalTransfer",
            RemoveVoice: true),

        // Serpenphare's Wrath: Typhoon variant — 19 AP, Ice zone. On-cast hit + per-turn
        // zone ticks: 10.02 initial + 10.02/turn = clamp(19)/3.3 per turn (base 1 turn;
        // overcharge 2 Lightning + 2 Fire extends the zone to 5 turns → 5×10.02 = 50.1 +
        // 10.02 initial = 60.12 ≈ 1.8× clamp(19) over the full zone). Generates 4 Ice.
        // Pure damage: the Typhoon heal (on-cast + per-turn) is zeroed via the cloned
        // Serpenphare_HealPercent = 0 row. The zone buff (BP_BattleBuff_Serpenphare) is a
        // clone of BP_BattleBuff_Typhoon with the heal tick zeroed; its per-turn damage
        // reads Serpenphare_BaseDamage. Screen-filling FX via BP-spawn scale ×6.
        new(
            "DA_Skill_Lune_SerpenpharesWrath",
            "BP_Battle_SkillScript_SerpenpharesWrath",
            "SerpenpharesWrath",
            "DA_Skill_Lune_Typhoon",
            "BP_Battle_SkillScript_Typhoon",
            "Typhoon",
            19,
            "Serpenphare's Wrath",
            "Deals <keyword id=\"DamageMultiplier\">10.02x</> <keyword id=\"Element_Ice\">Ice</> damage to all enemies and summons a storm that deals <keyword id=\"DamageMultiplier\">10.02x</> each turn (1 turn).\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Lightning\"/><img id=\"Stain_Lightning\"/><img id=\"Stain_Fire\"/><img id=\"Stain_Fire\"/>: the storm lasts 5 turns.",
            "Deals <keyword id=\"DamageMultiplier\">10.02x</> <keyword id=\"Element_Ice\">Ice</> damage and summons a 10.02x storm each turn (1 turn).\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Lightning\"/><img id=\"Stain_Lightning\"/><img id=\"Stain_Fire\"/><img id=\"Stain_Fire\"/>: 5 turns.",
            new[]
            {
                ("Typhoon_InitialDamage", "SerpenpharesWrath_InitialDamage", (double?)10.02),
                ("Typhoon_BaseDamage", "SerpenpharesWrath_BaseDamage", (double?)10.02),
                ("Typhoon_BreakBarMultiplier", "SerpenpharesWrath_BreakBarMultiplier", (double?)1.0),
                ("Typhoon_BaseDuration", "SerpenpharesWrath_BaseDuration", (double?)1.0),
                ("Typhoon_OverchargeDuration", "SerpenpharesWrath_OverchargeDuration", (double?)5.0),
                ("Typhoon_HealPercent", "SerpenpharesWrath_HealPercent", (double?)0.0),
            },
            "Typhoon",
            new FVector2D(1600.0f, 425.0f),
            OverchargeRequiredStains: new Dictionary<string, int> { ["Lightning"] = 2, ["Fire"] = 2 },
            ProducedStains: new Dictionary<string, int> { ["Ice"] = 4 },
            Kismet: new KismetEdits(ImpactScale: 6f),
            ExtraPairs: new[]
            {
                ("BP_BattleBuff_Typhoon", "BP_BattleBuff_SerpenpharesWrath"),
                ("FX_Test01Burst", "FX_ExplosionIce"),
            },
            SequenceStem: "SEQ_LuneSkill_Typhoon",
            RemoveVoice: true),

        // Brimstone: Hell variant — 19 AP, the top-end Fire nuke. 2 hits. Consumes
        // 1 Ice + 1 Earth + 2 Fire (4 stains → 90% from overcharge): base 1.653 × 2 hits
        // = 3.31, ×10.0 overcharge = 33.07 = clamp(19). Applies 50 Burn per hit. Keeps
        // Hell's self-damage ratio (row cloned at Hell's value) so the self-hit scales
        // with the bigger hit. Keeps Hell's shared sequence (never written).
        new(
            "DA_Skill_Lune_Brimstone",
            "BP_Battle_SkillScript_Brimstone",
            "Brimstone",
            "DA_Skill_Lune_Hell",
            "BP_Battle_SkillScript_Hell",
            "Hell",
            19,
            "Brimstone",
            "Deals <keyword id=\"DamageMultiplier\">1.65x</> <keyword id=\"Element_Fire\">Fire</> damage per hit to all enemies. 2 hits.\n" +
            "Applies 50 <keyword id=\"StatusEffect_Burn\">Burn</> per hit.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Ice\"/><img id=\"Stain_Earth\"/><img id=\"Stain_Fire\"/><img id=\"Stain_Fire\"/>: deal <keyword id=\"DamageMultiplier\">10.00x</> more damage.",
            "Deals <keyword id=\"DamageMultiplier\">1.65x</> <keyword id=\"Element_Fire\">Fire</> damage per hit to all enemies. 2 hits.\n" +
            "Applies 50 <keyword id=\"StatusEffect_Burn\">Burn</> per hit.\n" +
            "<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Ice\"/><img id=\"Stain_Earth\"/><img id=\"Stain_Fire\"/><img id=\"Stain_Fire\"/>: deal <keyword id=\"DamageMultiplier\">10.00x</> more damage.",
            new[]
            {
                ("Hell_BaseDamage", "Brimstone_BaseDamage", (double?)1.653),
                ("Hell_OverchargeDamageMultiplier", "Brimstone_OverchargeDamageMultiplier", (double?)10.0),
                ("Hell_BurnPerHit", "Brimstone_BurnPerHit", (double?)50.0),
                ("Hell_BurnMultiplier", "Brimstone_BurnMultiplier", null),
                ("Hell_SelfDamageMultiplier", "Brimstone_SelfDamageMultiplier", null),
                ("Hell_BreakBarMultiplier", "Brimstone_BreakBarMultiplier", null),
            },
            "Hell",
            new FVector2D(1450.0f, 425.0f),
            OverchargeRequiredStains: new Dictionary<string, int> { ["Ice"] = 1, ["Earth"] = 1, ["Fire"] = 2 },
            ProducedStains: new Dictionary<string, int> { ["Fire"] = 2, ["Earth"] = 1 },
            RemoveVoice: true),
    };

    /// <summary>
    /// Stages the Lune overhaul. <paramref name="lightStainWildcard"/> restores
    /// the vanilla shared stain holder (Light substitutes for any missing
    /// element); false stages the no-substitution holder used since the wildcard
    /// removal. Elemental Genesis / Cataclysm are always made strict by their
    /// own per-skill Validation patches.
    /// </summary>
    public static int Apply(string modDir, bool lightStainWildcard = false)
    {
        int n = StainUiKit.Apply(modDir);
        n += lightStainWildcard
            ? RestoreVanillaLightSubstitution(modDir)
            : PatchStainHolderNoLightSubstitution(modDir);
        n += PatchVanillaGenesisValidate(modDir);
        n += CloneTakenDamageBuff(modDir, "BP_Buff_FireRage_HasTakenAnyDamage", "BP_Buff_InfernalRage_HasTakenAnyDamage", 2);
        n += CloneTakenDamageBuff(modDir, "BP_Buff_FireRage_HasTakenAnyDamage", "BP_Buff_SanguinaryFlames_HasTakenAnyDamage", 3);
        // Maelstrom's zone buff is a real clone again (row-retargeted, hook survival
        // decided by the live test protocol) — no shared vanilla buff.
        n += CloneStormZoneBuff(modDir);
        n += PatchVanillaFireRage(modDir);
        n += PatchVanillaElectrify(modDir);
        n += CloneSerpenphareBuff(modDir);
        // Electrify: stage the DA override so the game is guaranteed to route to the
        // patched skill BP (the fire→Light CreateStainByType is removed in the BP) and the
        // produce map stays 2 Lightning. Without the DA in the mod the game can fall back
        // to the base-pak asset chain.
        n += StageVanillaDA(modDir, "DA_Skill_Lune_Electrify",
            produced: new Dictionary<string, int> { ["Lightning"] = 2 },
            routeScriptTo: "BP_Battle_SkillScript_Electrify");
        // Strip Light from the produce maps the player no longer wants (Light is never to
        // be generated as a by-product of these elemental moves).
        n += StageVanillaDA(modDir, "DA_Skill_Lune_IceGust",        // Ice Lance
            produced: new Dictionary<string, int> { ["Ice"] = 1 });
        n += StageVanillaDA(modDir, "DA_Skill_Lune_Wildfire",
            produced: new Dictionary<string, int> { ["Fire"] = 1 });
        n += StageVanillaDA(modDir, "DA_Skill_Lune_EarthSpike",     // Rockslide
            produced: new Dictionary<string, int> { ["Earth"] = 1 });
        n += StageVanillaDA(modDir, "DA_Skill_Lune_CrustalCrush",
            produced: new Dictionary<string, int> { ["Earth"] = 2 });
        n += StageVanillaDA(modDir, "DA_Skill_Lune_Hell",
            produced: new Dictionary<string, int> { ["Fire"] = 2 });
        // Gravity Mill's produce map is set via its NewSkill ProducedStains (Create stages
        // its DA from the Typhoon donor); updated to Earth + Dark there.
        foreach (var sk in Skills)
            n += Create(modDir, sk);
        return n;
    }

    /// <summary>
    /// Wildcard ON: drop any staged no-Light copy of the shared stain holder so the
    /// vanilla component (with Light substitution) is used. A previous pack may have
    /// left the patched copy in a reused mod tree. Per-skill strict patches
    /// (Elemental Genesis / Cataclysm) are untouched.
    /// </summary>
    private static int RestoreVanillaLightSubstitution(string modDir)
    {
        const string stem = "BP_StainHolderComponent";
        if (!Directory.Exists(modDir)) return 0;
        int removed = 0;
        foreach (var file in Directory.EnumerateFiles(modDir, stem + ".uasset", SearchOption.AllDirectories))
        {
            File.Delete(file);
            var exp = Path.ChangeExtension(file, ".uexp");
            if (File.Exists(exp)) File.Delete(exp);
            removed++;
        }
        // Also clean an orphan .uexp left by an interrupted/partial pack.
        foreach (var exp in Directory.EnumerateFiles(modDir, stem + ".uexp", SearchOption.AllDirectories))
        {
            if (File.Exists(Path.ChangeExtension(exp, ".uasset"))) continue;
            File.Delete(exp);
            removed++;
        }
        if (removed > 0)
            System.Console.WriteLine($"[StainHolder] Light-stain wildcard ON: removed staged strict copy ({removed} file(s))");
        else
            System.Console.WriteLine("[StainHolder] Light-stain wildcard ON: vanilla substitution restored");
        return removed;
    }

    /// <summary>
    /// Removes the shared Light-stain substitution from the game's stain-availability
    /// check and consume fallback. BP_StainHolderComponent.HasStainQuantities lets a
    /// held Light (byte 7) cover ANY missing elemental requirement by subtracting the
    /// held Light count from the unmet total; FindStainsObjectsByQuantities (which
    /// backs the native TryConsumeOverchargeStainsFromSkill every skill-script consume
    /// uses) Array_Finds a Light in the stain queue and spends THAT index for any
    /// still-unmet requirement. The player wants Light to be consumed ONLY for
    /// Light-specific requirements (which the per-key loop already handles), never as
    /// a universal substitute. The patch stages a modified copy of the shared
    /// component into the mod with both Light lookups pointed at the never-held byte
    /// 9 (see <see cref="PatchLightLookupToNeverHeld"/> — byte 0 would read the
    /// empty-slot count, keeping substitution alive). Light-only requirements still
    /// work: the exact-type loop checks each required key (incl. Light) directly.
    /// </summary>
    private static int PatchStainHolderNoLightSubstitution(string modDir)
    {
        const string stem = "BP_StainHolderComponent";
        var src = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), stem + ".uasset");
        if (src is null)
        {
            var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", stem);
            Directory.CreateDirectory(dir);
            new Retoc().ToLegacy(dir, stem);
            src = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), stem + ".uasset");
        }
        if (src is null) { System.Console.WriteLine($"[StainHolder] {stem} not found in tables"); return 0; }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return 0;
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);
        var map = DataTableEditor.Mappings;
        var holder = map != null ? new UAsset(dest, EngineVersion.VER_UE5_4, map) : new UAsset(dest, EngineVersion.VER_UE5_4);

        int total = 0;
        var fn = holder.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "HasStainQuantities");
        if (fn?.ScriptBytecode is null) return 0;
        int patched = PatchLightLookupToNeverHeld(fn);
        System.Console.WriteLine(patched > 0
            ? $"[StainHolder] HasStainQuantities: Light-substitution disabled ({patched} lookup(s) -> byte 9)"
            : "[StainHolder] HasStainQuantities Light lookup not found (no patch)");
        if (patched > 0) total++;

        // Consume-time fallback: FindStainsObjectsByQuantities spends a Light for any
        // requirement left unmet by the exact-type match (2x ByteConst 7). Retarget to
        // the never-held byte 9 so only exactly-required stains are ever consumed
        // (byte 0 would match already-eaten slots, which are marked 0 in the queue).
        var find = holder.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "FindStainsObjectsByQuantities");
        if (find?.ScriptBytecode is null) return total;
        int patchedFind = PatchLightLookupToNeverHeld(find, includeZeroInit: false);
        System.Console.WriteLine(patchedFind > 0
            ? $"[StainHolder] FindStainsObjectsByQuantities: Light-consume fallback disabled ({patchedFind} lookup(s) -> byte 9)"
            : "[StainHolder] FindStainsObjectsByQuantities Light lookup not found (no patch)");
        if (patchedFind > 0) total++;

        holder.FolderName = new FString("/Game/Gameplay/Battle/Stains/" + stem);
        holder.Write(dest);
        return total;
    }

    /// <summary>
    /// Elemental Genesis still runs its vanilla skill BP (only the DA is staged), and
    /// that BP's ValidateSkillAvailability lets a held Light (byte 7) stand in for any
    /// of the four required elements (Fire/Ice/Lightning/Earth) — the same substitution
    /// <see cref="KismetEdits.NoLightSubstitution"/> removes for Elemental Cataclysm,
    /// whose damage math assumes a hard four-element requirement. Stages a mod override
    /// of the vanilla BP with the Light lookup retargeted to the never-held byte 9
    /// (see <see cref="PatchLightLookupToNeverHeld"/>), mirroring the Cataclysm patch.
    /// Light requirements elsewhere are untouched.
    /// </summary>
    private static int PatchVanillaGenesisValidate(string modDir)
    {
        const string stem = "BP_Battle_SkillScript_ElementalGenesis";
        EnsureExtracted(stem);
        var src = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), stem + ".uasset");
        if (src is null) { System.Console.WriteLine($"[Genesis] {stem} not found in tables"); return 0; }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return 0;
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);
        var map = DataTableEditor.Mappings;
        var bp = map != null ? new UAsset(dest, EngineVersion.VER_UE5_4, map) : new UAsset(dest, EngineVersion.VER_UE5_4);

        var validate = bp.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "ValidateSkillAvailability");
        if (validate?.ScriptBytecode is null)
        {
            System.Console.WriteLine("[Genesis] ValidateSkillAvailability not found (no patch)");
            return 0;
        }
        int patched = PatchLightLookupToNeverHeld(validate);
        if (patched == 0)
        {
            System.Console.WriteLine("[Genesis] ValidateSkillAvailability Light lookup not found (no patch)");
            return 0;
        }
        bp.FolderName = new FString("/Game/Gameplay/Battle/Skills/Content/Lune/" + stem);
        bp.Write(dest);
        System.Console.WriteLine($"[Genesis] ValidateSkillAvailability: Light-substitution disabled ({patched} lookup(s) -> byte 9)");
        return 1;
    }



    private static int Create(string modDir, NewSkill sk)
    {
        var map = DataTableEditor.Mappings;
        UAsset Open(string p) => map != null
            ? new UAsset(p, EngineVersion.VER_UE5_4, map)
            : new UAsset(p, EngineVersion.VER_UE5_4);

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

        var donorDa = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), sk.DonorDa + ".uasset")
            ?? throw new FileNotFoundException(sk.DonorDa + ".uasset under work/tables");
        EnsureExtracted(sk.DonorBp);
        EnsureExtracted("DT_SkillIcons");
        EnsureExtracted("DA_SkillGraph_Lune");
        var donorBp = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), sk.DonorBp + ".uasset")
            ?? throw new FileNotFoundException(sk.DonorBp + ".uasset under work/tables");
        var graphSrc = FileCopy.FindTablesAsset("DA_SkillGraph_Lune.uasset")
            ?? throw new FileNotFoundException("DA_SkillGraph_Lune.uasset");
        var iconSrc = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), "DT_SkillIcons.uasset")
            ?? throw new FileNotFoundException("DT_SkillIcons.uasset under work/tables");
        var balSrc = FileCopy.FindTablesAsset("DT_Balancing_MainCharacterSkills.uasset")
            ?? throw new FileNotFoundException("DT_Balancing_MainCharacterSkills.uasset");

        var daDest = StageDest(donorDa, sk.DaStem);
        var bpDest = StageDest(donorBp, sk.BpStem);
        var gDest = StageDest(graphSrc, "DA_SkillGraph_Lune");
        var bDest = StageDest(balSrc, "DT_Balancing_MainCharacterSkills");
        var iDest = StageDest(iconSrc, "DT_SkillIcons");
        // The work/tables extract writes "Datatables", the game mounts "DataTables" —
        // a wrong-case path ships but never loads, so the skill tree hides the skill
        // (no icon row). Normalize to the game's casing.
        iDest = iDest.Replace("/Resources/Datatables/", "/Resources/DataTables/");
        foreach (var d in new[] { daDest, bpDest, gDest, bDest, iDest })
            Directory.CreateDirectory(Path.GetDirectoryName(d)!);

        // String table: new Name/Long/Short entries for the skill. The table is shared
        // across skills — stage once, then keep appending (AddStringTableEntries merges).
        var stSrc = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), "ST_MainCharacters_Skills.uasset")
            ?? throw new FileNotFoundException("ST_MainCharacters_Skills.uasset");
        var stDest = StageDest(stSrc, "ST_MainCharacters_Skills");
        Directory.CreateDirectory(Path.GetDirectoryName(stDest)!);
        if (!File.Exists(stDest)) FileCopy.AssetPair(stSrc, stDest);
        var stKey = $"Lune_{sk.NameId}";
        DataTableEditor.AddStringTableEntries(stDest, new Dictionary<string, string>
        {
            [stKey + "_Name"] = sk.DisplayName,
            [stKey + "_Description_Long"] = sk.Description,
            [stKey + "_Description_Short"] = sk.ShortDescription,
        });

        // ---------- 1. DA: clone + retarget + overrides ----------
        FileCopy.AssetPair(donorDa, daDest);
        var da = Open(daDest);
        // Rename both the package/object stem (DonorDa, e.g. "EarthQuake") and the
        // NameID stem (DonorStem, e.g. "Earthquake"). They differ in case for some
        // vanilla skills, so a single stem retarget would leave the export name stale.
        RetargetPairs(da, daDest, (sk.DonorDa, sk.DaStem), (sk.DonorStem, sk.NameId));
        if (sk.MechanicStem is not null)
            RetargetPairs(da, daDest, (StainUiKit.VanillaMechanic, sk.MechanicStem));
        foreach (var ex in da.Exports.OfType<NormalExport>())
            foreach (var p in ex.Data)
            {
                if (p.Name.ToString().Equals("SkillScript", StringComparison.OrdinalIgnoreCase) && p is SoftObjectPropertyData soft)
                {
                    var pkg = "/Game/Gameplay/Battle/Skills/Content/Lune/" + sk.BpStem;
                    soft.Value = new FSoftObjectPath(new FName(da, pkg), new FName(da, sk.BpStem + "_C"), null);
                    if (da.SoftObjectPathList is { Count: > 0 } list) list.Add(soft.Value);
                }
                else if (p.Name.ToString() == "Name" && p is TextPropertyData tname)
                    tname.Value = new FString(stKey + "_Name");
                else if (p.Name.ToString() == "Description" && p is TextPropertyData tdesc)
                    tdesc.Value = new FString(stKey + "_Description_Long");
                else if (p.Name.ToString() == "ShortDescription" && p is TextPropertyData tshort)
                    tshort.Value = new FString(stKey + "_Description_Short");
                else if (p.Name.ToString() == "APCost" && p is IntPropertyData ap)
                    ap.Value = sk.ApCost;
                else if (p.Name.ToString() == "BaseElementalType" && p is EnumPropertyData bpe && sk.Kismet?.DamageElement is { } el)
                    bpe.Value = FName.FromString(da, "EAttackType::" + StainsMechanicKit.Enumerator(el));
            }
        // The package-name header must match the file path, or to-zen treats the
        // clone as a duplicate of the donor and silently drops it from the pak.
        da.FolderName = new FString("/Game/Gameplay/SkillTree/Content/Lune/Skills/" + sk.DaStem);
        if (sk.DropOverchargeOnBuff)
            DropOverchargeOnBuffComponent(da);
        da.Write(daDest);
        if (sk.OverchargeRequiredStains is not null || sk.ProducedStains is not null)
            StainsMechanicKit.WriteFile(daDest, sk.OverchargeRequiredStains, sk.ProducedStains);

        // ---------- 2. skill script BP: surgical renames ----------
        FileCopy.AssetPair(donorBp, bpDest);
        var bp = Open(bpDest);
        var pairs = new List<(string From, string To)> { (sk.DonorBp, sk.BpStem) };
        foreach (var (donorRow, newRow, _) in sk.BalancingRows)
            pairs.Add((donorRow, newRow));
        if (sk.ExtraPairs is not null) pairs.AddRange(sk.ExtraPairs);
        // Do not retarget the sequence name: the clone would have to be packed,
        // and UAssetAPI Write of LevelSequences crashes the game at new-game load.
        // The cloned BP keeps the donor's vanilla sequence.
        RetargetPairs(bp, bpDest, pairs.ToArray());
        if (sk.Kismet is { } ke)
        {
            int k = 0;
            k += PatchOverchargeBonusHeal(bp, ke);
            k += PatchRandomGenerate(bp, ke);
            k += PatchOverchargeConsume(bp, ke);
            k += PatchDamageElement(bp, ke);
            k += PatchStatusOnOvercharge(bp, ke);
            k += PatchDarkerFire(bp, ke);
            k += PatchQteFailInterrupt(bp, ke);
            k += PatchBlitz(bp, ke);
            k += PatchAlreadyBurning(bp, ke);
            k += PatchConsumeGeneratesDark(bp, ke);
            k += PatchNoLightSubstitution(bp, ke);
            k += PatchDirectNuke(bp, ke);
            k += PatchOverchargeDamageMultiplier(bp, ke);
            k += PatchDefenselessTurns(bp, ke);
            k += PatchExtraCritStrikes(bp, ke);
            k += PatchStatusesOnHit(bp, ke);
            k += PatchGreaterPowerless(bp, ke);
            k += PatchApGainStatuses(bp, ke);
            k += PatchStripForcePlayAgain(bp, ke);
            k += PatchImpactScale(bp, ke);
            if (k > 0)
                foreach (var fn in bp.Exports.OfType<FunctionExport>())
                    if (fn.ScriptBytecode is not null)
                        MayhemFixKit.RecalcContextSkips(bp, fn);
        }
        bp.FolderName = new FString("/Game/Gameplay/Battle/Skills/Content/Lune/" + sk.BpStem);
        bp.Write(bpDest);

        // LevelSequences are not cloned. UAssetAPI cannot parse possessables /
        // Niagara / hit-sound tracks; Open+Write of those files (and of the
        // shared FX_ElementalCharge Niagara system) produced a pack that crashed
        // on new game. The BP keeps the donor's vanilla sequence.

        // ---------- 3. balancing rows ----------
        if (!File.Exists(bDest)) FileCopy.AssetPair(balSrc, bDest);
        var balEd = new DataTableEditor(bDest);
        foreach (var (donorRow, newRow, val) in sk.BalancingRows)
        {
            balEd.CloneRow(donorRow, newRow);
            if (val is double v)
                balEd.Apply($"{{\"{newRow}\":{{\"FloatValue\":{v.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}}}");
        }
        // Maelstrom uses its OWN cloned zone buff (CloneStormZoneBuff): the buff's
        // hardcoded StormCaller_BaseDamage read is retargeted to Maelstrom_BaseDamage
        // (0.1525), and the skill BP spawns the clone — vanilla Storm Caller's own
        // buff volley is untouched.
        balEd.Save(bDest);

        // ---------- 4. icon row ----------
        if (!File.Exists(iDest)) FileCopy.AssetPair(iconSrc, iDest);
        var iconEd = new DataTableEditor(iDest);
        iconEd.CloneRow(sk.DonorStem, sk.NameId);
        if (!iconEd.TryGetRow(sk.NameId, out _)) throw new InvalidDataException("icon row missing after clone");
        iconEd.Save(iDest);

        // ---------- 5. graph node (appended, linked from the prerequisite) ----------
        if (!File.Exists(gDest)) FileCopy.AssetPair(graphSrc, gDest);
        var g = Open(gDest);
        var gx = g.Exports.OfType<NormalExport>().First();
        var nodes = (ArrayPropertyData)gx.Data[0];
        var edges = (ArrayPropertyData)gx.Data[1];
        FPackageIndex prereqIdx = null;
        Import templateImp = null;
        bool alreadyOnGraph = false;
        for (int i = 0; i < nodes.Value.Length; i++)
        {
            var nodeStruct = (StructPropertyData)nodes.Value[i];
            var su = (StructPropertyData)nodeStruct.Value[0];
            var skillPtr = ((ObjectPropertyData)su.Value[0]).Value;
            if (skillPtr is null || skillPtr.IsNull() || !skillPtr.IsImport()) continue;
            var impName = g.Imports[Math.Abs(skillPtr.Index) - 1].ObjectName.ToString();
            if (impName == sk.DaStem) alreadyOnGraph = true;
            if (impName == "DA_Skill_Lune_" + sk.PrereqNameId)
            {
                prereqIdx = skillPtr;
                templateImp = g.Imports[Math.Abs(skillPtr.Index) - 1];
            }
        }
        if (alreadyOnGraph)
            return 5;
        if (prereqIdx is null)
            throw new InvalidDataException($"prerequisite node {sk.PrereqNameId} not found in Lune graph");
        var daPkgPath = "/Game/Gameplay/SkillTree/Content/Lune/Skills/" + sk.DaStem;
        var newDaIdx = EnsureObjectImport(g, daPkgPath, sk.DaStem,
            templateImp.ClassName.ToString(), templateImp.ClassPackage.ToString());

        var node = (StructPropertyData)nodes.Value[0].Clone();
        node.Value[0] = (StructPropertyData)node.Value[0].Clone();
        node.Value[1] = (StructPropertyData)node.Value[1].Clone();
        var suN = (StructPropertyData)node.Value[0];
        ((ObjectPropertyData)suN.Value[0]).Value = newDaIdx;
        ((IntPropertyData)suN.Value[1]).Value = sk.ApCost;
        ((BoolPropertyData)suN.Value[2]).Value = false;
        var req = (StructPropertyData)suN.Value[3];
        ((ObjectPropertyData)req.Value[0]).Value = null;
        ((NamePropertyData)req.Value[1]).Value = FName.FromString(g, "None");
        ((BoolPropertyData)suN.Value[4]).Value = false;
        var posWrap = (StructPropertyData)node.Value[1];
        ((Vector2DPropertyData)posWrap.Value[0]).Value = sk.Position;
        nodes.Value = nodes.Value.Append(node).ToArray();

        var edge = (StructPropertyData)edges.Value[0].Clone();
        ((ObjectPropertyData)edge.Value[0]).Value = prereqIdx;
        ((ObjectPropertyData)edge.Value[1]).Value = newDaIdx;
        edges.Value = edges.Value.Append(edge).ToArray();
        g.Write(gDest);

        return 5; // DA + BP + balancing + icons + graph
    }

    internal static void EnsureExtracted(string filter)
    {
        if (FileCopy.FindTablesAsset(filter + ".uasset") != null) return;
        var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", filter);
        Directory.CreateDirectory(dir);
        new Retoc().ToLegacy(dir, filter);
    }

    private static void SetInvariant(TextPropertyData t, string text)
    {
        if (t.CultureInvariantString is null)
            t.CultureInvariantString = new FString(text);
        else
            t.CultureInvariantString.Value = text;
    }

    internal static bool SequenceHasRawExports(UAsset seq) =>
        seq.Exports.Any(e => e is RawExport);

    /// <summary>Retargets every "from" name (assets, rows, exports) to "to" in the package, mirroring WeaponEffectsCreateKit's surgical rename.</summary>
    internal static void RetargetPairs(UAsset asset, string dest, params (string From, string To)[] pairs)
    {
        var names = asset.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s)) continue;
            // Skill clones reuse the donor's LevelSequence (SEQ_SkillMaelle_*); those
            // references must keep pointing at the real vanilla sequence. A blanket
            // substring rename here would rewrite e.g. SEQ_SkillMaelle_BurningCanvas_V2
            // into a non-existent sequence (donor stem "BurningCanvas" is contained in
            // the sequence name), leaving the move waiting forever on a missing asset.
            // Same hazard for shared buff classes: Corruption's ("EndSlice"->"Corruption")
            // retarget rewrote BP_Buff_EndSliceForetellConsumedCount into a ghost buff,
            // silently dropping EndSlice's battle-start counter (damage stuck at x1).
            // Buffs are shared vanilla assets, never cloned: never rename them.
            if (s.Contains("/Game/Gameplay/EpicBattleSequence/", StringComparison.Ordinal)
                || s.StartsWith("SEQ_SkillMaelle_", StringComparison.Ordinal)
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

    internal static void RetargetPackage(UAsset asset, string dest, string fromStem, string toStem)
    {
        var names = asset.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s)) continue;
            if (s.Contains(fromStem, StringComparison.Ordinal))
                asset.SetNameReference(i, new FString(s.Replace(fromStem, toStem, StringComparison.Ordinal)));
        }
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

    /// <summary>
    /// Kismet math bytecode functions (EqualEqual_ByteByte, etc.) must be imported as
    /// siblings of NotEqual_ByteByte (ClassPackage=/Script/CoreUObject, outer=KismetMathLibrary).
    /// EnsureObjectImport("/Script/Engine", name, name) creates a fake Engine class and
    /// crashes the game when the Blueprint is linked (save load).
    /// </summary>
    internal static FPackageIndex EnsureKismetMathFn(UAsset asset, string functionName)
    {
        int sibling = -1;
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            var nm = imp.ObjectName.ToString();
            bool isMath = imp.OuterIndex is { } oi && !oi.IsNull() && oi.IsImport()
                && asset.Imports[Math.Abs(oi.Index) - 1].ObjectName.ToString() == "KismetMathLibrary";
            if (nm == functionName && isMath)
                return FPackageIndex.FromImport(i);
            if (isMath && sibling < 0) sibling = i;
            if (nm == "NotEqual_ByteByte") sibling = i;
        }
        if (sibling < 0)
            throw new InvalidDataException("no KismetMathLibrary function to clone for " + functionName);
        var tmpl = asset.Imports[sibling];
        return asset.AddImport(new Import(
            tmpl.ClassPackage.ToString(),
            tmpl.ClassName.ToString(),
            tmpl.OuterIndex,
            functionName,
            false,
            asset));
    }
    // ---------- per-skill kismet patches ----------

    internal static FunctionExport? OnActionEffect(UAsset bp) =>
        bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");

    private static string? PtrName(KismetPropertyPointer? p) =>
        p?.New?.Path?.FirstOrDefault()?.ToString() ?? p?.ToString();

    private static bool IsOverchargedCondition(EX_JumpIfNot jn)
    {
        // cond = Context(ContextExpression = InstanceVariable SkillState) -> InstanceVariable IsOvercharged
        if (jn.BooleanExpression is not EX_Context ctx) return false;
        return ctx.ObjectExpression is EX_InstanceVariable iv1 && PtrName(iv1.Variable) == "SkillState"
            && ctx.ContextExpression is EX_InstanceVariable iv2 && PtrName(iv2.Variable) == "IsOvercharged";
    }

    /// <summary>Restoration: after the main heal, add RecoverHP(MaxHP x 0.3) gated on overcharge.</summary>
    private static int PatchOverchargeBonusHeal(UAsset bp, KismetEdits ke)
    {
        if (!ke.OverchargeBonusHeal) return 0;
        var fn = OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();

        int healIdx = -1;
        for (int i = stmts.Count - 1; i >= 0; i--)
        {
            bool has = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e => { if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "RecoverHP") has = true; });
            if (has) { healIdx = i; break; }
        }
        if (healIdx < 0) return 0;

        EX_JumpIfNot? gate = null;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e => { if (e is EX_JumpIfNot jn && gate is null && IsOverchargedCondition(jn)) gate = jn; });
        if (gate is null) return 0;

        KismetExpression? maxHp = null;
        foreach (var s in stmts)
        {
            EX_Context? gctx = null;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (gctx is null && e is EX_Context c
                    && c.ContextExpression is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "GetMaxHP")
                    gctx = c;
            });
            if (gctx is not null) { maxHp = gctx; break; }
        }
        if (maxHp is null) return 0;

        FPackageIndex? multIdx = null;
        for (int i = 0; i < bp.Imports.Count; i++)
            if (bp.Imports[i].ObjectName.ToString() == "Multiply_DoubleDouble") { multIdx = FPackageIndex.FromImport(i); break; }
        if (multIdx is null) return 0;

        var bonus = MayhemFixKit.CloneKismet(stmts[healIdx]);
        EX_VirtualFunction? bonusCall = null;
        MayhemFixKit.Walk(new[] { bonus }, e => { if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "RecoverHP" && bonusCall is null) bonusCall = vf; });
        if (bonusCall is null || bonusCall.Parameters.Length < 1) return 0;
        bonusCall.Parameters[0] = new EX_FinalFunction
        {
            StackNode = multIdx,
            Parameters = new KismetExpression[]
            {
                MayhemFixKit.CloneKismet(maxHp),
                new EX_DoubleConst { Value = 0.3 },
            },
        };

        uint insertOffset = 0;
        for (int i = 0; i <= healIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
        var cond = MayhemFixKit.CloneKismet(gate.BooleanExpression);
        uint jumpSize = 1 + 4 + (uint)cond.GetSize(bp);
        uint bonusSize = (uint)bonus.GetSize(bp);
        var jump = new EX_JumpIfNot
        {
            CodeOffset = insertOffset + jumpSize + bonusSize,
            BooleanExpression = cond,
        };
        MayhemFixKit.ShiftAbsolute(bp, fn, insertOffset, jumpSize + bonusSize, null);
        var list = fn.ScriptBytecode.ToList();
        list.InsertRange(healIdx + 1, new KismetExpression[] { jump, bonus });
        fn.ScriptBytecode = list.ToArray();
        return 1;
    }

    /// <summary>Chiaroscuro: replace GenerateStainsFromSkill with a 50/50 random branch creating one of two stains.</summary>
    private static int PatchRandomGenerate(UAsset bp, KismetEdits ke)
    {
        if (ke.RandomGenerate is null) return 0;
        var els = ke.RandomGenerate.Split('|');
        if (els.Length != 2) return 0;
        var fn = OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();

        int genIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool has = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e => { if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "GenerateStainsFromSkill") has = true; });
            if (has) { genIdx = i; break; }
        }
        if (genIdx < 0) return 0;

        FPackageIndex? randIdx = null;
        for (int i = 0; i < bp.Imports.Count; i++)
            if (bp.Imports[i].ObjectName.ToString() == "RandomBool") { randIdx = FPackageIndex.FromImport(i); break; }
        if (randIdx is null)
        {
            FPackageIndex? mathIdx = null;
            for (int i = 0; i < bp.Imports.Count; i++)
                if (bp.Imports[i].ObjectName.ToString() == "KismetMathLibrary") { mathIdx = FPackageIndex.FromImport(i); break; }
            if (mathIdx is null)
            {
                FPackageIndex? engIdx = null;
                for (int i = 0; i < bp.Imports.Count; i++)
                    if (bp.Imports[i].ObjectName.ToString() == "/Script/Engine" && bp.Imports[i].ClassName.ToString() == "Package") { engIdx = FPackageIndex.FromImport(i); break; }
                engIdx ??= bp.AddImport(new Import("/Script/CoreUObject", "Package", FPackageIndex.FromRawIndex(0), "/Script/Engine", false, bp));
                mathIdx = bp.AddImport(new Import("/Script/CoreUObject", "KismetMathLibrary", engIdx, "KismetMathLibrary", false, bp));
            }
            randIdx = bp.AddImport(new Import("/Script/CoreUObject", "RandomBool", mathIdx, "RandomBool", false, bp));
        }
        var randCond = new EX_FinalFunction { StackNode = randIdx, Parameters = Array.Empty<KismetExpression>() };

        var lightStmt = MayhemFixKit.CloneKismet(stmts[genIdx]);
        var darkStmt = MayhemFixKit.CloneKismet(stmts[genIdx]);
        SetCreateStain(lightStmt, StainsMechanicKit.Ordinal(els[0]));
        SetCreateStain(darkStmt, StainsMechanicKit.Ordinal(els[1]));

        uint insertOffset = 0;
        for (int i = 0; i < genIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
        uint jumpSize = 1 + 4 + (uint)randCond.GetSize(bp);
        uint lightSize = (uint)lightStmt.GetSize(bp);
        uint darkSize = (uint)darkStmt.GetSize(bp);
        uint endJumpSize = 5; // EX_Jump token + 4-byte CodeOffset
        // Layout: [JumpIfNot(RandomBool) -> dark] [light] [Jump -> after] [dark].
        // true  -> light only (the end jump skips dark); false -> dark only.
        var jump = new EX_JumpIfNot
        {
            CodeOffset = insertOffset + jumpSize + lightSize + endJumpSize,
            BooleanExpression = randCond,
        };
        var endJump = new EX_Jump
        {
            CodeOffset = insertOffset + jumpSize + lightSize + endJumpSize + darkSize,
        };
        long delta = jumpSize + lightSize + endJumpSize + darkSize - (long)stmts[genIdx].GetSize(bp);
        if (delta > 0) MayhemFixKit.ShiftAbsolute(bp, fn, insertOffset, (uint)delta, null);
        var list = fn.ScriptBytecode.ToList();
        list.RemoveAt(genIdx);
        list.InsertRange(genIdx, new KismetExpression[] { jump, lightStmt, endJump, darkStmt });
        fn.ScriptBytecode = list.ToArray();
        return 1;
    }

    private static void SetCreateStain(KismetExpression stmt, int elementOrdinal)
    {
        MayhemFixKit.Walk(new[] { stmt }, e =>
        {
            if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "GenerateStainsFromSkill")
            {
                vf.VirtualFunctionName = FName.FromString(vf.VirtualFunctionName.Asset, "CreateStainByType");
                vf.Parameters = new KismetExpression[]
                {
                    new EX_ByteConst { Value = (byte)elementOrdinal },
                    new EX_True(),
                    new EX_ByteConst { Value = 1 },
                };
            }
        });
    }

    /// <summary>Chiaroscuro/Lumière: MakeDamageBuilder element byte -> the new element.</summary>
    private static int PatchDamageElement(UAsset bp, KismetEdits ke)
    {
        if (ke.DamageElement is null) return 0;
        int ordinal = StainsMechanicKit.Ordinal(ke.DamageElement);
        foreach (var fn in bp.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            foreach (var s in fn.ScriptBytecode)
            {
                EX_VirtualFunction? mdb = null;
                MayhemFixKit.Walk(new[] { s }, e => { if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "MakeDamageBuilder" && mdb is null) mdb = vf; });
                if (mdb is null) continue;
                foreach (var p in mdb.Parameters)
                    if (p is EX_ByteConst bc) { bc.Value = (byte)ordinal; return 1; }
            }
        }
        return 0;
    }

    // ---------- LevelSequence helpers ----------

    /// <summary>
    /// Deletes the DirectorBP statement that plays the skill voiceline
    /// (<c>PlaySkillBattleLineInternal</c>) and retargets every custom-event
    /// <c>EntryPoint</c> IntConst that jumped into the ubergraph past it.
    /// Deleting the call without shifting those offsets is what broke
    /// Chiaroscuro / Lumière after the voiceline patch — later sequence events
    /// landed in the middle of other statements.
    /// </summary>
    internal static int RemoveVoiceFromDirector(UAsset seq)
    {
        FunctionExport? uber = null;
        foreach (var fn in seq.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            if (fn.ObjectName.ToString().StartsWith("ExecuteUbergraph", StringComparison.Ordinal))
                uber = fn;
        }
        if (uber?.ScriptBytecode is null) return 0;

        int idx = -1;
        for (int i = 0; i < uber.ScriptBytecode.Length; i++)
        {
            bool has = false;
            MayhemFixKit.Walk(new[] { uber.ScriptBytecode[i] }, e =>
            {
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "PlaySkillBattleLineInternal") has = true;
            });
            if (has) { idx = i; break; }
        }
        if (idx < 0) return 0;

        uint rmOffset = 0;
        for (int i = 0; i < idx; i++) rmOffset += (uint)uber.ScriptBytecode[i].GetSize(seq);
        uint rmSize = (uint)uber.ScriptBytecode[idx].GetSize(seq);
        uint uberSize = rmOffset;
        for (int i = idx; i < uber.ScriptBytecode.Length; i++)
            uberSize += (uint)uber.ScriptBytecode[i].GetSize(seq);

        ShiftAbsoluteSigned(seq, uber, rmOffset, rmSize);
        foreach (var fn in seq.Exports.OfType<FunctionExport>())
        {
            if (ReferenceEquals(fn, uber) || fn.ScriptBytecode is null) continue;
            MayhemFixKit.Walk(fn.ScriptBytecode, e =>
            {
                if (e is not EX_IntConst ic) return;
                if (ic.Value < (int)rmOffset || ic.Value >= (int)uberSize) return;
                ic.Value = ic.Value >= (int)(rmOffset + rmSize)
                    ? ic.Value - (int)rmSize
                    : (int)rmOffset;
            });
        }
        var list = uber.ScriptBytecode.ToList();
        list.RemoveAt(idx);
        uber.ScriptBytecode = list.ToArray();
        MayhemFixKit.RecalcContextSkips(seq, uber);
        return 1;
    }

    internal static void ShiftAbsoluteSigned(UAsset asset, FunctionExport fn, uint removalPoint, uint removalSize)
    {
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref pos, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j when j.CodeOffset >= removalPoint + removalSize: j.CodeOffset -= removalSize; break;
                    case EX_JumpIfNot jn when jn.CodeOffset >= removalPoint + removalSize: jn.CodeOffset -= removalSize; break;
                    case EX_PushExecutionFlow pf when pf.PushingAddress >= removalPoint + removalSize: pf.PushingAddress -= removalSize; break;
                    case EX_SwitchValue sv:
                        if (sv.EndGotoOffset >= removalPoint + removalSize) sv.EndGotoOffset -= removalSize;
                        for (int i = 0; i < sv.Cases.Length; i++)
                            if (sv.Cases[i].NextOffset >= removalPoint + removalSize) sv.Cases[i].NextOffset -= removalSize;
                        break;
                }
            });
    }

    /// <summary>
    /// Chiaroscuro: replace native TryConsumeOverchargeStainsFromSkill with two
    /// holder.ConsumeStainByType calls (Light then Dark). The native consume
    /// path does not reliably eat Dark; the previous custom path scanned
    /// Self.ReverseStainQueue, a Mayhem-only InstVar that Ice Gust does not
    /// have, which crashed the VM at cast.
    /// </summary>
    private static int PatchOverchargeConsume(UAsset bp, KismetEdits ke)
    {
        var elements = ke.CustomConsume?.Split('|')
            ?? (ke.CustomDarkConsume ? new[] { "Light", "Dark" } : null);
        if (elements is null || elements.Length == 0) return 0;
        var fn = OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();

        int consumeIdx = -1;
        EX_LocalVariable? holder = null;
        for (int i = 0; i < stmts.Count; i++)
        {
            EX_VirtualFunction? tc = null;
            EX_LocalVariable? h = null;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "TryConsumeOverchargeStainsFromSkill" && tc is null) tc = vf;
                if (e is EX_LocalVariable lv && h is null)
                {
                    var nm = lv.Variable?.New?.Path?.FirstOrDefault()?.ToString() ?? "";
                    if (nm.StartsWith("CallFunc_GetComponentByClass_ReturnValue", StringComparison.Ordinal)) h = lv;
                }
            });
            if (tc is not null) { consumeIdx = i; holder = h; break; }
        }
        if (consumeIdx < 0 || holder is null) return 0;

        var emptyPtr = new KismetPropertyPointer(new FFieldPath());
        EX_Context Consume(int element) => new EX_Context
        {
            ObjectExpression = MayhemFixKit.CloneKismet(holder),
            ContextExpression = new EX_VirtualFunction
            {
                VirtualFunctionName = FName.FromString(bp, "ConsumeStainByType"),
                Parameters = new KismetExpression[]
                {
                    new EX_ByteConst { Value = (byte)element },
                    new EX_ByteConst { Value = 1 },
                },
            },
            RValuePointer = emptyPtr,
        };

        var replacements = elements.Select(el => Consume(StainsMechanicKit.Ordinal(el))).ToArray();
        uint insertOffset = 0;
        for (int i = 0; i < consumeIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
        long delta = replacements.Sum(r => (long)r.GetSize(bp)) - stmts[consumeIdx].GetSize(bp);
        if (delta > 0) MayhemFixKit.ShiftAbsolute(bp, fn, insertOffset, (uint)delta, null);
        else if (delta < 0) ShiftAbsoluteSigned(bp, fn, insertOffset, (uint)(-delta));
        var list = fn.ScriptBytecode.ToList();
        list.RemoveAt(consumeIdx);
        list.InsertRange(consumeIdx, replacements);
        fn.ScriptBytecode = list.ToArray();
        return 1;
    }

    /// <summary>
    /// Shocking Wave: the donor (Crippling Tsunami) applies Slow unconditionally on hit.
    /// Gate the Slow on overcharge and append Defenseless + Powerless applications, so the
    /// base move has no status and the 2-Lightning overcharge inflicts all three.
    /// </summary>
    private static int PatchStatusOnOvercharge(UAsset bp, KismetEdits ke)
    {
        if (ke.StatusOnOvercharge is null) return 0;
        var statuses = ke.StatusOnOvercharge.Split('|');
        if (statuses.Length == 0) return 0;
        var fn = OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();

        // Find the statement containing ApplyBuff(BP_BattleBuff_Slow_C).
        int slowIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool has = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (has) return;
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "ApplyBuff")
                {
                    foreach (var p in vf.Parameters)
                        MayhemFixKit.Walk(new[] { p }, e2 =>
                        {
                            if (e2 is EX_ObjectConst oc && oc.Value.IsImport() &&
                                oc.Value.ToImport(bp).ObjectName.ToString() == "BP_BattleBuff_Slow_C") has = true;
                        });
                }
            });
            if (has) { slowIdx = i; break; }
        }
        if (slowIdx < 0) return 0;

        // IsOvercharged condition: Context(SkillState) -> IsOvercharged.
        EX_JumpIfNot? gate = null;
        foreach (var s2 in stmts)
            MayhemFixKit.Walk(new[] { s2 }, e => { if (e is EX_JumpIfNot jn && gate is null && IsOverchargedCondition(jn)) gate = jn; });
        if (gate is null) return 0;

        // Buff class imports: BP_BattleBuff_Defenseless_C + BP_BattleBuff_Powerless_C.
        var buffImports = new List<FPackageIndex>();
        foreach (var status in statuses)
        {
            var (cls, pkg) = status switch
            {
                "Slow" => ("BP_BattleBuff_Slow_C", "/Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_Slow"),
                "Defenseless" => ("BP_BattleBuff_Defenseless_C", "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Defenseless"),
                "Powerless" => ("BP_BattleBuff_Powerless_C", "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Powerless"),
                _ => (null, null),
            };
            if (cls is null) continue;
            buffImports.Add(EnsureObjectImport(bp, pkg!, cls, cls, "/Script/Engine"));
        }
        if (buffImports.Count == 0) return 0;

        // The slow statement becomes the first status application; clone it per extra status.
        var stmts2 = new List<KismetExpression> { stmts[slowIdx] };
        for (int i = 1; i < buffImports.Count; i++)
        {
            var clone = MayhemFixKit.CloneKismet(stmts[slowIdx]);
            bool swapped = false;
            MayhemFixKit.Walk(new[] { clone }, e =>
            {
                if (swapped) return;
                if (e is EX_ObjectConst oc && oc.Value.IsImport() &&
                    oc.Value.ToImport(bp).ObjectName.ToString() == "BP_BattleBuff_Slow_C")
                {
                    oc.Value = buffImports[i];
                    swapped = true;
                }
            });
            if (!swapped) return 0;
            stmts2.Add(clone);
        }

        // Layout: [JumpIfNot(IsOvercharged) -> after] [Slow] [Defenseless] [Powerless].
        uint insertOffset = 0;
        for (int i = 0; i < slowIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
        uint jumpSize = 1 + 4 + (uint)gate.BooleanExpression.GetSize(bp);
        uint totalSize = jumpSize;
        foreach (var st in stmts2) totalSize += (uint)st.GetSize(bp);
        var jump = new EX_JumpIfNot
        {
            CodeOffset = insertOffset + totalSize,
            BooleanExpression = MayhemFixKit.CloneKismet(gate.BooleanExpression),
        };
        long delta = totalSize - (long)stmts[slowIdx].GetSize(bp);
        if (delta > 0) MayhemFixKit.ShiftAbsolute(bp, fn, insertOffset, (uint)delta, null);
        var list = fn.ScriptBytecode.ToList();
        list.RemoveAt(slowIdx);
        list.InsertRange(slowIdx, new[] { jump }.Concat(stmts2));
        fn.ScriptBytecode = list.ToArray();
        return 1;
    }

    /// <summary>
    /// Frost Piercer / Crushing Tsunami: swap the donor's native always-on status for a
    /// set of OC-gated statuses. The donor (IceGust / Crippling Tsunami) applies Slow on
    /// its hit unconditionally. <paramref name="ke.StatusesOnHit"/> lists the replacement
    /// buff classes (e.g. "GreaterSlow|GreaterDefenseless"); the native Slow statement is
    /// removed (<paramref name="ke.RemoveNativeStatus"/>) and its slot becomes
    /// [JumpIfNot(IsOvercharged) -> after][status1][status2] so the statuses land only on
    /// overcharge for <paramref name="ke.StatusesOnHitTurns"/> turns. All clones reuse the
    /// donor's own ApplyBuff statement (locals/contexts stay valid).
    /// </summary>
    private static int PatchStatusesOnHit(UAsset bp, KismetEdits ke)
    {
        if (ke.StatusesOnHit is null) return 0;
        var statuses = ke.StatusesOnHit.Split('|');
        if (statuses.Length == 0 || ke.StatusesOnHitTurns <= 0) return 0;
        var fn = OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();

        // Find the donor's native ApplyBuff(BP_BattleBuff_Slow_C) statement.
        int slowIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool has = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (has) return;
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "ApplyBuff")
                    foreach (var p in vf.Parameters)
                        MayhemFixKit.Walk(new[] { p }, e2 =>
                        {
                            if (e2 is EX_ObjectConst oc && oc.Value.IsImport() &&
                                oc.Value.ToImport(bp).ObjectName.ToString() == "BP_BattleBuff_Slow_C") has = true;
                        });
            });
            if (has) { slowIdx = i; break; }
        }
        if (slowIdx < 0) return 0;

        // IsOvercharged condition.
        EX_JumpIfNot? gate = null;
        foreach (var s2 in stmts)
            MayhemFixKit.Walk(new[] { s2 }, e => { if (e is EX_JumpIfNot jn && gate is null && IsOverchargedCondition(jn)) gate = jn; });
        if (gate is null) return 0;

        // Build the replacement status application statements (clone the donor's Slow
        // statement once per status, swapping the buff class + duration).
        var applies = new List<KismetExpression>();
        var classFor = new Dictionary<string, (string Cls, string Pkg)>(StringComparer.Ordinal)
        {
            ["GreaterSlow"] = ("BP_BattleBuff_GreaterSlow_C", "/Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_GreaterSlow"),
            ["Defenseless"] = ("BP_BattleBuff_Defenseless_C", "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Defenseless"),
            ["GreaterDefenseless"] = ("BP_BattleBuff_GreaterDefenseless_C", "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_GreaterDefenseless"),
            ["Powerless"] = ("BP_BattleBuff_Powerless_C", "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Powerless"),
            ["GreaterPowerless"] = ("BP_BattleBuff_GreaterPowerless_C", "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_GreaterPowerless"),
            ["Slow"] = ("BP_BattleBuff_Slow_C", "/Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_Slow"),
        };
        for (int si = 0; si < statuses.Length; si++)
        {
            if (!classFor.TryGetValue(statuses[si], out var cf)) return 0;
            var imp = EnsureObjectImport(bp, cf.Pkg, cf.Cls, cf.Cls, "/Script/Engine");
            // Always clone fresh — mutating the donor's original on si==0 would leave no
            // BP_BattleBuff_Slow_C to swap on later statuses.
            var clone = MayhemFixKit.CloneKismet(stmts[slowIdx]);
            bool swapped = false;
            MayhemFixKit.Walk(new[] { clone }, e =>
            {
                if (swapped) return;
                if (e is EX_ObjectConst oc && oc.Value.IsImport() &&
                    oc.Value.ToImport(bp).ObjectName.ToString() == "BP_BattleBuff_Slow_C")
                { oc.Value = imp; swapped = true; }
            });
            if (!swapped) return 0;
            applies.Add(clone);
        }

        // Rewrite the duration. The donor statement may read a hard IntConst (IceGust tail:
        // ApplyBuff(target, Slow_C, IntConst 3, ...)) OR a row-driven FTrunc local
        // (Crippling Tsunami: the duration local is filled by FTrunc(duration row) earlier).
        // For a hard IntConst, set it to the requested turns. For a row-driven value, the
        // cloned row is created by BalancingRows with the right value — but those rows are
        // created with `null` for Frost/Crushing (they clone CripplingTsunami_SlowDuration
        // at its vanilla 3), so we must ALSO set the IntConst/FTrunc source. Set every
        // top-level IntConst in the ApplyBuff params that is a pure duration literal.
        foreach (var ap in applies)
            MayhemFixKit.Walk(new[] { ap }, e =>
            {
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "ApplyBuff")
                    foreach (var p in vf.Parameters)
                        if (p is EX_IntConst ic) { ic.Value = ke.StatusesOnHitTurns; }
            });

        // Layout: [JumpIfNot(IsOvercharged) -> after] [status1] [status2].
        // RemoveNativeStatus: the native statement is replaced by applies (already the
        // first element). When false we'd keep the native Slow too — not used by these two.
        if (ke.RemoveNativeStatus)
        {
            uint insertOffset = 0;
            for (int i = 0; i < slowIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
            uint jumpSize = 1 + 4 + (uint)gate.BooleanExpression.GetSize(bp);
            uint totalSize = jumpSize;
            foreach (var st in applies) totalSize += (uint)st.GetSize(bp);
            var jump = new EX_JumpIfNot
            {
                CodeOffset = insertOffset + totalSize,
                BooleanExpression = MayhemFixKit.CloneKismet(gate.BooleanExpression),
            };
            long delta = totalSize - (long)stmts[slowIdx].GetSize(bp);
            if (delta > 0) MayhemFixKit.ShiftAbsolute(bp, fn, insertOffset, (uint)delta, null);
            else if (delta < 0) ShiftAbsoluteSigned(bp, fn, insertOffset, (uint)(-delta));
            var list2 = fn.ScriptBytecode.ToList();
            list2.RemoveAt(slowIdx);
            list2.InsertRange(slowIdx, new[] { jump }.Concat(applies));
            fn.ScriptBytecode = list2.ToArray();
        }
        System.Console.WriteLine($"[{statuses.Length}] statuses on hit: {ke.StatusesOnHit} for {ke.StatusesOnHitTurns} turns (OC-gated)");
        return 1;
    }

    /// <summary>
    /// Cloud Splitter: Thunderfall has no native status statement, so neither
    /// StatusOnOvercharge nor StatusesOnHit applies. Synthesize an overcharge-gated
    /// Greater Powerless application (N turns) inside the donor's own
    /// DealDamageAndFeedback, right after its DealDamages call (per struck target),
    /// mirroring Crippling Tsunami's ApplyBuff shape
    /// (buff, targetStats, duration, casterStats, 4, outBuff) with
    /// Thunderfall-native template clones. All templates are same-asset.
    /// </summary>
    private static int PatchGreaterPowerless(UAsset bp, KismetEdits ke)
    {
        if (ke.GreaterPowerlessTurns <= 0) return 0;
        const string buffCls = "BP_BattleBuff_GreaterPowerless_C";
        foreach (var fn0 in bp.Exports.OfType<FunctionExport>())
        {
            if (fn0.ScriptBytecode is null) continue;
            bool has = false;
            foreach (var s in fn0.ScriptBytecode)
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (e is EX_ObjectConst oc && oc.Value.IsImport() &&
                        oc.Value.ToImport(bp).ObjectName.ToString() == buffCls) has = true;
                });
            if (has) { Console.WriteLine("[Lune] CloudSplitter Greater Powerless already present"); return 0; }
        }
        var fn = bp.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "DealDamageAndFeedback")
            ?? throw new InvalidDataException("CloudSplitter: DealDamageAndFeedback not found");
        var stmts = fn.ScriptBytecode!.ToList();

        static string PropName(KismetPropertyPointer? p) =>
            p?.New?.Path?.FirstOrDefault()?.ToString() ?? "";
        static bool IsStatsRead(EX_Context cx)
        {
            bool ac = false, call = false;
            MayhemFixKit.Walk(new[] { cx }, x =>
            {
                if (x is EX_InstanceVariable iv && PropName(iv.Variable) == "AC_jRPG_CharacterStats") ac = true;
                if (x is EX_LocalVirtualFunction || x is EX_FinalFunction) call = true;
            });
            return ac && !call;
        }
        // Target stats: Context(LocalVar CurrentTarget){AC}. Caster stats:
        // Context(InstVar CurrentCharacter){AC}. Both native to this function.
        EX_Context? targetTpl = null, casterTpl = null;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_Context cx && cx.ObjectExpression is EX_LocalVariable lv
                    && PropName(lv.Variable) == "CurrentTarget" && IsStatsRead(cx) && targetTpl is null)
                    targetTpl = cx;
                if (e is EX_Context cx2 && cx2.ObjectExpression is EX_InstanceVariable iv2
                    && PropName(iv2.Variable) == "CurrentCharacter" && IsStatsRead(cx2) && casterTpl is null)
                    casterTpl = cx2;
            });
        if (targetTpl is null) throw new InvalidDataException("CloudSplitter: target stats template not found");
        if (casterTpl is null) throw new InvalidDataException("CloudSplitter: caster stats template not found");

        // Overcharge gate boolean from OnActionEffect's own gate (same asset).
        var onAction = bp.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect")
            ?? throw new InvalidDataException("CloudSplitter: OnActionEffect not found");
        EX_JumpIfNot? gate = null;
        foreach (var s2 in onAction.ScriptBytecode!)
            MayhemFixKit.Walk(new[] { s2 }, e => { if (e is EX_JumpIfNot jn && gate is null && IsOverchargedCondition(jn)) gate = jn; });
        if (gate is null) throw new InvalidDataException("CloudSplitter: overcharge gate not found");

        // Out-local: clone a same-function FObjectProperty, rename, append.
        var destOwner = FPackageIndex.FromExport(bp.Exports.IndexOf(fn));
        var props = (fn.LoadedProperties ?? Array.Empty<UAssetAPI.FieldTypes.FProperty>()).ToList();
        const string outName = "CallFunc_ApplyBuff_CreatedBuffInstance";
        if (!props.Any(p => p.Name.ToString() == outName))
        {
            var tpl = props.FirstOrDefault(p => p.GetType().Name == "FObjectProperty")
                ?? throw new InvalidDataException("CloudSplitter: no object local template");
            var prop = (UAssetAPI.FieldTypes.FProperty)tpl.GetType().GetMethod("MemberwiseClone",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(tpl, null)!;
            prop.Name = FName.FromString(bp, outName);
            props.Add(prop);
        }
        fn.LoadedProperties = props.ToArray();
        var outLocal = new EX_LocalVariable
        {
            Variable = new KismetPropertyPointer(new FFieldPath(
                new[] { FName.FromString(bp, outName) }, destOwner)),
        };

        var gpImport = EnsureObjectImport(bp,
            "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_GreaterPowerless",
            buffCls, buffCls, "/Script/Engine");
        var apply = new EX_VirtualFunction
        {
            VirtualFunctionName = FName.FromString(bp, "ApplyBuff"),
            Parameters = new KismetExpression[]
            {
                new EX_ObjectConst { Value = gpImport },
                (KismetExpression)MayhemFixKit.CloneKismet(targetTpl),
                new EX_IntConst { Value = ke.GreaterPowerlessTurns },
                (KismetExpression)MayhemFixKit.CloneKismet(casterTpl),
                new EX_ByteConst { Value = 4 },
                outLocal,
            },
        };

        int dmgIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "DealDamages") hit = true;
            });
            if (hit) { dmgIdx = i; break; }
        }
        if (dmgIdx < 0) throw new InvalidDataException("CloudSplitter: DealDamages not found");
        var jump = new EX_JumpIfNot
        {
            CodeOffset = 0, // fixed below
            BooleanExpression = (KismetExpression)MayhemFixKit.CloneKismet(gate.BooleanExpression),
        };
        uint insertOffset = 0;
        for (int i = 0; i <= dmgIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
        var block = new List<KismetExpression> { jump, apply };
        uint blockSize = (uint)block.Sum(b => (long)b.GetSize(bp));
        jump.CodeOffset = insertOffset + blockSize;
        MayhemFixKit.ShiftAbsolute(bp, fn, insertOffset, blockSize, null);
        stmts.InsertRange(dmgIdx + 1, block);
        fn.ScriptBytecode = stmts.ToArray();
        Console.WriteLine($"[Lune] CloudSplitter: OC-gated Greater Powerless x{ke.GreaterPowerlessTurns} spliced");
        return 1;
    }

    /// <summary>
    /// Soul Eater: replace the donor's single "burning target → +4 AP once" with
    /// "+4 AP per status the target holds" among <paramref name="ke.ApGainStatuses"/>
    /// (Burn/Powerless/Slow/Defenceless), up to 16 AP.
    ///
    /// Thermal Transfer's OnActionEffect, per targeted enemy (inside the target loop):
    ///   [CallFunc_HasBuffOfClass_ReturnValue = Context(Context(Context(
    ///       TargetedCharacters[Idx], AC_jRPG_CharacterStats), BattleBuffComponent)
    ///       .HasBuffOfClass(Burn))]
    ///   ... MakeDamageBuilder + DealDamages ...
    ///   [if AND(HasHit, Local_IsTargetBurning): ShouldApplyAPGain = true]
    /// and the actual AP gain lives in the overcharge region (gated on the flag), which
    /// only runs when overcharged.
    ///
    /// Soul Eater has NO overcharge and wants +4 PER status. We insert N straight-line
    /// units right after the donor's per-target HasBuffOfClass(Burn) statement (inside the
    /// loop, where the target context is live). Each unit:
    ///   [LetBool CallFunc_HasBuffOfClass_ReturnValue = HasBuffOfClass(target, statusX)]
    ///     (clone of the donor's burn check, buff class swapped)
    ///   [JumpIfNot(CallFunc_HasBuffOfClass_ReturnValue) → past the gain]
    ///   [GainAP(4)]  (the proven GainApOn builder — Dark Power / Angel's Gift shape)
    /// Each unit sets the shared return-value bool then immediately consumes it, so a
    /// target holding 3 statuses runs 3 gains = 12 AP. All statements are the donor's own
    /// clones or the proven GainApOn shape — no new locals. The donor's original
    /// overcharge-region gain is left unreachable (Soul Eater has no overcharge) so it
    /// never double-grants.
    /// </summary>
    private static int PatchApGainStatuses(UAsset bp, KismetEdits ke)
    {
        if (ke.ApGainStatuses is null) return 0;
        var statuses = ke.ApGainStatuses.Split('|');
        if (statuses.Length == 0) return 0;
        var fn = OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();

        // The donor's per-target burn-check statement: the top-level LetBool whose
        // expression is Context(...).HasBuffOfClass(BP_BattleBuff_Burn_C).
        int burnIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool has = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (has) return;
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "HasBuffOfClass")
                    foreach (var p in vf.Parameters)
                        MayhemFixKit.Walk(new[] { p }, e2 =>
                        {
                            if (e2 is EX_ObjectConst oc && oc.Value.IsImport() &&
                                oc.Value.ToImport(bp).ObjectName.ToString() == "BP_BattleBuff_Burn_C") has = true;
                        });
            });
            if (has) { burnIdx = i; break; }
        }
        if (burnIdx < 0) return 0;

        // Extract the return-value bool local the burn check writes.
        EX_LocalVariable? retLocal = null;
        MayhemFixKit.Walk(new[] { stmts[burnIdx] }, e =>
        {
            if (retLocal is null && e is EX_LetBool lb && lb.VariableExpression is EX_LocalVariable lv
                && PtrName(lv.Variable) == "CallFunc_HasBuffOfClass_ReturnValue")
                retLocal = lv;
        });
        if (retLocal is null) return 0;

        // The character-stats instance var (AC_jRPG_CharacterStats) used by GainApOn's
        // receiver — clone it from the burn check's own context chain.
        EX_InstanceVariable? statsIv = null;
        MayhemFixKit.Walk(new[] { stmts[burnIdx] }, e =>
        {
            if (statsIv is null && e is EX_InstanceVariable iv && PtrName(iv.Variable) == "AC_jRPG_CharacterStats")
                statsIv = iv;
        });
        if (statsIv is null) return 0;

        var classFor = new Dictionary<string, (string Cls, string Pkg)>(StringComparer.Ordinal)
        {
            ["Burn"] = ("BP_BattleBuff_Burn_C", "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Burn"),
            ["Powerless"] = ("BP_BattleBuff_Powerless_C", "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Powerless"),
            ["Slow"] = ("BP_BattleBuff_Slow_C", "/Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_Slow"),
            ["Defenseless"] = ("BP_BattleBuff_Defenseless_C", "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Defenseless"),
        };

        // Build N straight-line units: [burn-check clone (class swapped)] [gate] [GainAP].
        // The gate is a JumpIfNot on the return bool that skips the GainAP when the status
        // is absent. Offsets are filled after we know the total block layout.
        var block = new List<KismetExpression>();

        // Lune-self receiver for the gain: clone the donor's own `CurrentCharacter`
        // instance variable (used across the donor for self contexts) and wrap it in the
        // proven GainApOn Context(Context(CurrentCharacter, stats), GainAP) shape.
        EX_InstanceVariable? selfIv = null;
        foreach (var s in stmts)
        {
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (selfIv is null && e is EX_InstanceVariable iv && PtrName(iv.Variable) == "CurrentCharacter")
                    selfIv = iv;
            });
            if (selfIv is not null) break;
        }
        if (selfIv is null) return 0;
        var selfStats = new EX_Context
        {
            ObjectExpression = (EX_InstanceVariable)MayhemFixKit.CloneKismet(selfIv),
            ContextExpression = (EX_InstanceVariable)MayhemFixKit.CloneKismet(statsIv),
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
        };
        for (int si = 0; si < statuses.Length; si++)
        {
            if (!classFor.TryGetValue(statuses[si], out var cf)) return 0;
            var imp = EnsureObjectImport(bp, cf.Pkg, cf.Cls, cf.Cls, "/Script/Engine");

            // 1. Check: clone the donor's burn check, swap the buff class.
            var check = MayhemFixKit.CloneKismet(stmts[burnIdx]);
            bool swapped = false;
            MayhemFixKit.Walk(new[] { check }, e =>
            {
                if (swapped) return;
                if (e is EX_ObjectConst oc && oc.Value.IsImport() &&
                    oc.Value.ToImport(bp).ObjectName.ToString() == "BP_BattleBuff_Burn_C")
                { oc.Value = imp; swapped = true; }
            });
            if (!swapped) return 0;

            // 2. Gate on the check's return bool.
            var gate = new EX_JumpIfNot
            {
                CodeOffset = 0,
                BooleanExpression = new EX_LocalVariable { Variable = retLocal.Variable },
            };

            // 3. Gain: the proven GainApOn shape (Lune self) for 4 AP.
            var gain = new EX_Context
            {
                ObjectExpression = (EX_Context)MayhemFixKit.CloneKismet(selfStats),
                ContextExpression = new EX_VirtualFunction
                {
                    VirtualFunctionName = FName.FromString(bp, "GainAP"),
                    Parameters = new KismetExpression[]
                    {
                        new EX_IntConst { Value = 4 },
                        new EX_ByteConst { Value = 1 },
                    },
                },
                RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            };

            block.Add(check);
            block.Add(gate);
            block.Add(gain);
        }

        // The target's BattleBuffComponent receiver. In the donor's burn check the call is
        //   Context(Context(Context(target, stats), BattleBuffComponent)).HasBuffOfClass(...)
        // — the HasBuffOfClass context's ObjectExpression is the FULL buff-component context
        // (Context(Context(target, stats), BattleBuffComponent)). Clone that entire context
        // (not just its ObjectExpression, which would drop the BattleBuffComponent level).
        EX_Context? buffCompCtx = null;
        MayhemFixKit.Walk(new[] { stmts[burnIdx] }, e =>
        {
            if (buffCompCtx is null && e is EX_Context ctx
                && ctx.ContextExpression is EX_InstanceVariable iv
                && PtrName(iv.Variable) == "BattleBuffComponent")
                buffCompCtx = ctx;
        });
        if (buffCompCtx is null) return 0;
        var buffCompTarget = (EX_Context)MayhemFixKit.CloneKismet(buffCompCtx);

        // The donor's dead OC region writes CallFunc_FTrunc_ReturnValue (int, from
        // FTrunc(SoulEater_APGain)) but that region never runs (no overcharge), so the
        // declared local is free to reuse as TryRemoveBuffStacks' out-local.
        EX_LocalVariable? ftruncLocal = null;
        foreach (var s in stmts)
        {
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (ftruncLocal is null && e is EX_LocalVariable lv
                    && PtrName(lv.Variable) == "CallFunc_FTrunc_ReturnValue")
                    ftruncLocal = lv;
            });
            if (ftruncLocal is not null) break;
        }
        if (ftruncLocal is null) return 0;

        // Layout: [check0 gate0 gain0 consume0] [check1 gate1 gain1 consume1]
        //         [check2 gate2 gain2 consume2] [check3 gate3 gain3 consume3]
        // Each unit grants +4 AP if the target holds that status, then removes it with the
        // game's own per-class status consume (Lumina weapon "Consume" passives /
        // Invigorating Fire):
        //   Context(Context(Context(target, stats), BattleBuffComponent))
        //     .TryRemoveBuffStacks(BP_BattleBuff_<S>_C, <stacks>, <out local>)
        // Count 999 fully clears every stack of the status. RemoveAllBuffsOfType(2) was
        // rejected: byte 2 = StatusEffect category (Burn/Frozen/...) and does NOT clear the
        // StatsModifier-category stat debuffs (Powerless/Slow/Defenceless), which is why
        // only Burn was consumed in-game.
        var layout = new List<KismetExpression>();
        for (int si = 0; si < block.Count; si += 3)
        {
            layout.Add(block[si]);      // check
            layout.Add(block[si + 1]);  // gate
            layout.Add(block[si + 2]);  // gain
            if (!classFor.TryGetValue(statuses[si / 3], out var cf2)) return 0;
            var imp2 = EnsureObjectImport(bp, cf2.Pkg, cf2.Cls, cf2.Cls, "/Script/Engine");
            var consume = new EX_Context
            {
                ObjectExpression = (EX_Context)MayhemFixKit.CloneKismet(buffCompTarget),
                ContextExpression = new EX_VirtualFunction
                {
                    VirtualFunctionName = FName.FromString(bp, "TryRemoveBuffStacks"),
                    Parameters = new KismetExpression[]
                    {
                        new EX_ObjectConst { Value = imp2 },
                        new EX_IntConst { Value = 999 },
                        new EX_LocalVariable { Variable = ftruncLocal.Variable },
                    },
                },
                RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            };
            layout.Add(consume);
        }

        // The donor's burn check is statement burnIdx. We insert the layout immediately
        // AFTER it (inside the same loop body). The donor's burn check must remain the FIRST
        // statement so its CallFunc_Array_Get_Item index is live; our clones reuse the same
        // array-index local.
        uint insertOffset = 0;
        for (int i = 0; i <= burnIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
        uint blockSize = (uint)layout.Sum(b => (long)b.GetSize(bp));
        MayhemFixKit.ShiftAbsolute(bp, fn, insertOffset, blockSize, null);

        // Fill jump offsets (absolute from function start). A status gate that FAILS (status
        // absent) must fall to the NEXT unit's check so the remaining statuses are still
        // tested; the LAST unit's gate falls past the end of the layout (donor flow resumes).
        // Units are [c g gain consume] quads.
        uint pos = insertOffset;
        for (int bi = 0; bi < layout.Count; bi++)
        {
            if (layout[bi] is EX_JumpIfNot jn)
            {
                bool isLastGate = bi + 4 >= layout.Count;
                uint end = pos + (uint)layout[bi].GetSize(bp);
                if (isLastGate)
                {
                    for (int k = bi + 1; k < layout.Count; k++) end += (uint)layout[k].GetSize(bp);
                }
                else
                {
                    // Skip this gate's gain + consume (2 statements) to land on the next
                    // unit's check (4 statements ahead of the gate).
                    end += (uint)layout[bi + 1].GetSize(bp);
                    end += (uint)layout[bi + 2].GetSize(bp);
                }
                jn.CodeOffset = end;
            }
            pos += (uint)layout[bi].GetSize(bp);
        }

        var list = fn.ScriptBytecode.ToList();
        list.InsertRange(burnIdx + 1, layout);
        fn.ScriptBytecode = list.ToArray();
        MayhemFixKit.RecalcContextSkips(bp, fn);

        // Neutralize the donor's stale-flag AP gain. Thermal Transfer's own +AP-on-burn
        // mechanic is: the per-target bridge sets the instance flag ShouldApplyAPGain = true
        // when a burning target is hit, and the TOP of OnActionEffect (running on the NEXT
        // hit of this 2-hit move) grants GainAP(SoulEater_APGain) when the flag is set. Our
        // per-status units have fully replaced that mechanic, but the untouched bridge still
        // sets the flag on hit 1, so hit 2's top-level region fires an extra +4 AP. Flip the
        // bridge's assignment to false (same statement size, no offset fixups needed).
        bool bridgeFixed = false;
        foreach (var s in fn.ScriptBytecode)
        {
            if (s is EX_LetBool lb && lb.VariableExpression is EX_InstanceVariable iv
                && PtrName(iv.Variable) == "ShouldApplyAPGain"
                && lb.AssignmentExpression is EX_True)
            {
                lb.AssignmentExpression = new EX_False();
                bridgeFixed = true;
                break;
            }
        }
        if (!bridgeFixed) return 0;

        System.Console.WriteLine($"[SoulEater] per-status AP gain x{statuses.Length} (4 AP each) + per-class consume (donor stale-flag gain neutralized)");
        return 1;
    }

    /// <summary>
    /// Soul Eater: strip Thermal Transfer's overcharge "Force Play Again" (the second-turn
    /// grant) so Soul Eater is a plain single-turn action. The call is the
    /// Force Play Again virtual function inside the overcharge block; its top-level
    /// statement is removed.
    /// </summary>
    private static int PatchStripForcePlayAgain(UAsset bp, KismetEdits ke)
    {
        if (!ke.StripForcePlayAgain) return 0;
        var fn = OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();
        int idx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool has = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (has) return;
                if (e is EX_Context ctx && ctx.ContextExpression is EX_VirtualFunction vf &&
                    vf.VirtualFunctionName.ToString() == "Force Play Again") has = true;
            });
            if (has) { idx = i; break; }
        }
        if (idx < 0) return 0;
        uint rmOffset = 0;
        for (int i = 0; i < idx; i++) rmOffset += (uint)stmts[i].GetSize(bp);
        uint rmSize = (uint)stmts[idx].GetSize(bp);
        ShiftAbsoluteSigned(bp, fn, rmOffset, rmSize);
        var list = fn.ScriptBytecode.ToList();
        list.RemoveAt(idx);
        fn.ScriptBytecode = list.ToArray();
        System.Console.WriteLine("[SoulEater] Force Play Again (second turn) stripped");
        return 1;
    }

    /// <summary>
    /// Frost Piercer / Crushing Tsunami / Serpenphare: scale the skill's spawned impact FX
    /// (the SpawnSystemAtLocation spawn-scale VectorConst) by the given factor. Mirrors the
    /// LuneApocalypseKit PatchImpactFx scale path on the skill-script BP's OnActionEffect.
    /// </summary>
    private static int PatchImpactScale(UAsset bp, KismetEdits ke)
    {
        if (ke.ImpactScale <= 1f) return 0;
        var fn = OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return 0;
        int n = 0;
        foreach (var s in fn.ScriptBytecode)
        {
            if (s is not EX_LetObj letObj) continue;
            EX_FinalFunction? spawn = null;
            MayhemFixKit.Walk(new[] { letObj.AssignmentExpression }, e =>
            {
                if (spawn is null && e is EX_FinalFunction ff &&
                    ff.StackNode.IsImport() &&
                    ff.StackNode.ToImport(bp).ObjectName.ToString() == "SpawnSystemAtLocation")
                    spawn = ff;
            });
            if (spawn is null) continue;
            foreach (var p in spawn.Parameters)
                if (p is EX_VectorConst vc)
                {
                    vc.Value = new FVector(vc.Value.X * ke.ImpactScale, vc.Value.Y * ke.ImpactScale, vc.Value.Z * ke.ImpactScale);
                    n++;
                }
        }
        if (n > 0) System.Console.WriteLine($"[impact] spawn FX scale x{ke.ImpactScale} ({n} spawn(s))");
        return n > 0 ? 1 : 0;
    }

    /// <summary>
    /// Infernal Rage: darken ImpactFire's fire colors and enlarge every fire FX so the
    /// clone reads as a bigger, darker-red blast than Fire Rage.
    /// </summary>
    private static int PatchDarkerFire(UAsset bp, KismetEdits ke)
    {
        if (!ke.DarkerFire) return 0;
        var fn = bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "ImpactFire");
        if (fn?.ScriptBytecode is null) return 0;
        int n = 0;
        foreach (var s in fn.ScriptBytecode)
        {
            var setCalls = new List<EX_VirtualFunction>();
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() is "SetColorParameter" or "SetFloatParameter")
                    setCalls.Add(vf);
            });
            foreach (var vf in setCalls)
            {
                var name = vf.Parameters.Length > 0 && vf.Parameters[0] is EX_NameConst nc ? nc.Value.ToString() : "";
                if (vf.VirtualFunctionName.ToString() == "SetFloatParameter")
                {
                    if (vf.Parameters.Length < 2 || vf.Parameters[1] is not EX_FloatConst fc) continue;
                    switch (name)
                    {
                        case "Size" when fc.Value == 2: fc.Value = 4; n++; break;   // rock impact + explosion
                        case "Size" when fc.Value == 3.5f: fc.Value = 5; n++; break; // smoke puff
                        case "LittleMeshSize": fc.Value = 2; n++; break;
                        case "LifetimeMultiplier": fc.Value = 3; n++; break;
                    }
                }
                else if (vf.VirtualFunctionName.ToString() == "SetColorParameter" && vf.Parameters.Length >= 2 &&
                         vf.Parameters[1] is EX_StructConst sc)
                {
                    // FLinearColor = 4 floats (R, G, B, A); keep alpha.
                    var floats = new List<EX_FloatConst>();
                    foreach (var el in sc.Value)
                        if (el is EX_FloatConst fl) floats.Add(fl);
                    if (floats.Count < 3) continue;
                    switch (name)
                    {
                        case "PowerColor":   // (1, 0.025, 0) -> darker red
                            floats[0].Value = 0.55f; floats[1].Value = 0.012f; n++; break;
                        case "Minimum_Color":
                            floats[0].Value = 0.7f; floats[1].Value = 0.03f; n++; break;
                        case "Maximum_Color":
                            floats[0].Value = 0.05f; floats[1].Value = 0.0001f; n++; break;
                    }
                }
            }
        }
        return n > 0 ? 1 : 0;
    }

    /// <summary>
    /// Combustion: the donor (Immolation) overcharges when the target has any buff via
    /// its BP_SkillDataComponent_OverchargeOnBuff_C component; the skill must overcharge
    /// purely from consumed stains. Drop the component from SkillComponentList only.
    /// The orphaned subobject export stays UNTOUCHED (its own class + serialized data):
    /// removing it breaks the pack (to-zen: "Failed to sort exports in load order
    /// because of circular dependencies"), and re-classing it to another component
    /// (BP_SkillDataComponent_Base_C) crashes the game on load — the export's data
    /// does not deserialize against the swapped schema ("Failed to find a valid
    /// property for schema index 1 ... BP_SkillDataComponent_Base_C"), and the skill
    /// tree DA loads on save load. The orphan is unreferenced (SkillComponentList no
    /// longer lists it), so the game lazily loads it with its original class intact.
    /// </summary>
    private static void DropOverchargeOnBuffComponent(UAsset da)
    {
        foreach (var ex in da.Exports.OfType<NormalExport>())
        {
            var list = ex.Data?.FirstOrDefault(p => p.Name.ToString() == "SkillComponentList") as ArrayPropertyData;
            if (list is null) continue;
            list.Value = list.Value.Where(elem => !(elem is ObjectPropertyData op && op.Value?.IsExport() == true
                && op.Value.ToExport(da).ObjectName.ToString().Contains("OverchargeOnBuff", StringComparison.Ordinal))).ToArray();
        }
        System.Console.WriteLine("[Combustion] removed OverchargeOnBuff from component list (export kept intact)");
    }

    /// <summary>
    /// Combustion: vanilla Immolation applies its escalated burn when the target is
    /// Marked (HasBuffOfClass BP_BattleBuff_Marked_C); Combustion escalates when the
    /// target is ALREADY burning. Swap the HasBuffOfClass argument to the Burn buff —
    /// both are already imports in the donor (Burn is the spawned instance).
    /// </summary>
    private static int PatchAlreadyBurning(UAsset bp, KismetEdits ke)
    {
        if (!ke.AlreadyBurning) return 0;
        int n = 0;
        foreach (var fn in bp.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            foreach (var s in fn.ScriptBytecode)
            {
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (n > 0) return;
                    if (e is not EX_LocalVirtualFunction lvf) return;
                    if (lvf.VirtualFunctionName.ToString() != "HasBuffOfClass") return;
                    foreach (var p in lvf.Parameters)
                    {
                        if (p is not EX_ObjectConst oc) continue;
                        if (!oc.Value.IsImport()) continue;
                        if (oc.Value.ToImport(bp).ObjectName.ToString() != "BP_BattleBuff_Marked_C") continue;
                        int burnIdx = bp.Imports.FindIndex(imp => imp.ObjectName.ToString() == "BP_BattleBuff_Burn_C");
                        if (burnIdx < 0) return;
                        oc.Value = FPackageIndex.FromImport(burnIdx);
                        n++;
                        return;
                    }
                });
            }
        }
        if (n > 0) System.Console.WriteLine("[Combustion] burn escalation keys on an already-burning target");
        return n;
    }

    /// <summary>
    /// Combustion: replace the map-driven GenerateStainsFromSkill with an explicit
    /// CreateStainByType(Dark), and retarget the overcharge gate so it skips the WHOLE
    /// block. Vanilla Immolation's JumpIfNot(IsOvercharged) only skips the consume —
    /// its target is the generate's holder assignment, so the generate (and therefore
    /// the Dark) ran on every cast. Retargeting it to the statement after the create
    /// (the block's PopExecutionFlow) makes the Dark a strict consequence of the
    /// overcharge consume (3 Earth or Light substitutes, gated by the game's own
    /// stain-driven IsOvercharged). The replacement grows the statement, so every
    /// absolute code offset after it is shifted by the size delta (ShiftAbsolute).
    /// </summary>
    private static int PatchConsumeGeneratesDark(UAsset bp, KismetEdits ke)
    {
        if (!ke.ConsumeGeneratesDark) return 0;
        var fn = OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return 0;

        EX_JumpIfNot? gate = null;
        int genIdx = -1;
        for (int i = 0; i < fn.ScriptBytecode.Length; i++)
        {
            MayhemFixKit.Walk(new[] { fn.ScriptBytecode[i] }, e =>
            {
                if (gate is null && e is EX_JumpIfNot jn && IsOverchargedCondition(jn)) gate = jn;
                if (genIdx < 0 && e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "GenerateStainsFromSkill") genIdx = i;
            });
        }
        if (gate is null || genIdx < 0) return 0;

        uint insertOffset = 0;
        for (int j = 0; j < genIdx; j++) insertOffset += (uint)fn.ScriptBytecode[j].GetSize(bp);
        var st = fn.ScriptBytecode[genIdx];
        uint oldSize = (uint)st.GetSize(bp);
        SetCreateStain(st, StainsMechanicKit.Ordinal("Dark"));
        uint newSize = (uint)st.GetSize(bp);
        int delta = (int)newSize - (int)oldSize;
        if (delta != 0)
            MayhemFixKit.ShiftAbsolute(bp, fn, insertOffset, (uint)delta, null);

        // The create is the last statement of the overcharge block; the statement
        // right after it (the block's PopExecutionFlow) is where the gate must land.
        gate.CodeOffset = insertOffset + newSize;

        System.Console.WriteLine("[Combustion] overcharge consumes 3 Earth/Light -> 1 Dark (gate skips the whole block)");
        return 1;
    }


    /// <summary>
    /// Retargets a Light-stain lookup (ByteConst 7) at the never-held byte 9 so the
    /// lookup always misses. Byte 9 is used — never 0: empty stain-queue slots ARE
    /// byte 0 (GetFirstAvailableSlotIndex finds them with Equal(Array_Get, 0)), and
    /// "Get Active Stain Count By Type" counts all nine enumerator indices 0-8, so a
    /// lookup of 0 returns the EMPTY-slot count and substitution stays alive. Key 9 is
    /// absent from that map, so Map_Find yields 0 and the requirement check is strict.
    /// Fresh copies carry 7 (retargeted wherever they appear); already-patched copies
    /// carry the lookup init as 0 — when <paramref name="includeZeroInit"/> is set,
    /// the Temp_byte_Variable init Let is retargeted from 0 as well (other ByteConst
    /// 0s, e.g. Genesis's feedback-struct fields or Find's consumed-slot markers, are
    /// never touched: Find must keep marking eaten slots 0, so it passes false).
    /// </summary>
    private static int PatchLightLookupToNeverHeld(FunctionExport fn, bool includeZeroInit = true)
    {
        if (fn.ScriptBytecode is null) return 0;
        int n = 0;
        foreach (var s in fn.ScriptBytecode)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (includeZeroInit
                    && e is EX_Let let
                    && let.Variable is EX_LocalVariable lv
                    && PtrName(lv.Variable) == "Temp_byte_Variable"
                    && let.Expression is EX_ByteConst init
                    && init.Value == 0) { init.Value = 9; n++; }
                else if (e is EX_ByteConst bc && bc.Value == 7) { bc.Value = 9; n++; }
            });
        return n;
    }

    /// <summary>
    /// Elemental Cataclysm: the Genesis donor lets Light stains substitute for a missing
    /// required element in ValidateSkillAvailability (it tracks LightStainsAmount and
    /// spends it to fill gaps). Pointing that Light query (ByteConst 7) at the
    /// never-held byte 9 makes LightStainsAmount always 0, so any missing element
    /// blocks the cast — no Light substitution. Size-neutral (byte 7 → 9).
    /// </summary>
    private static int PatchNoLightSubstitution(UAsset bp, KismetEdits ke)
    {
        if (!ke.NoLightSubstitution) return 0;
        var validate = bp.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "ValidateSkillAvailability");
        if (validate?.ScriptBytecode is null) return 0;
        int n = PatchLightLookupToNeverHeld(validate);
        if (n > 0) System.Console.WriteLine("[Cataclysm] Light substitution disabled in ValidateSkillAvailability");
        return n > 0 ? 1 : 0;
    }

    /// <summary>
    /// Levin Dance: chain N extra lightning strikes whenever a strike crits. The donor
    /// (Lightning Dance / Thunderfall) already chains ONE extra DealDamageAndFeedback per
    /// crit via its `BattleDamages.IsCriticalHit` gate; this clones that extra-strike
    /// statement (the one right after the BooleanAND(IsCriticalHit) gate) (N−1) more times
    /// so a crit chains N total extra strikes. All locals/contexts are cloned from the
    /// donor's own existing extra-strike statement — no new (undeclared) locals.
    /// </summary>
    private static int PatchExtraCritStrikes(UAsset bp, KismetEdits ke)
    {
        if (ke.ExtraCritStrikes <= 1) return 0;
        var fn = OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();

        // The extra-strike statement is the DealDamageAndFeedback that appears AFTER a
        // BooleanAND FinalFunction whose params reference IsCriticalHit (the crit gate).
        // In both LightningDance/Thunderfall it is the 2nd DealDamageAndFeedback in the
        // function. Find the crit-gate statement first, then the first
        // DealDamageAndFeedback statement at/after it.
        int gateStmtIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool inGate = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (inGate) return;
                // BooleanAND is a KismetMathLibrary FinalFunction (import, "BooleanAND").
                if (e is EX_FinalFunction ff && ff.StackNode.IsImport() &&
                    ff.StackNode.ToImport(bp).ObjectName.ToString() == "BooleanAND")
                {
                    foreach (var p in ff.Parameters)
                        MayhemFixKit.Walk(new[] { p }, e2 =>
                        {
                            // IsCriticalHit appears as InstVar inside a context/struct on
                            // the BattleDamages local (e.g. Context(LocalVar)->InstVar).
                            if (e2 is EX_InstanceVariable iv && PtrName(iv.Variable) == "IsCriticalHit")
                                inGate = true;
                        });
                }
            });
            if (inGate) { gateStmtIdx = i; break; }
        }
        if (gateStmtIdx < 0) return 0;

        int extraDealIdx = -1;
        for (int i = gateStmtIdx + 1; i < stmts.Count; i++)
        {
            bool hasDeal = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "DealDamageAndFeedback") hasDeal = true;
            });
            if (hasDeal) { extraDealIdx = i; break; }
        }
        if (extraDealIdx < 0) return 0;

        // The extra-strike statement's whole top-level range is usually just that one
        // statement (DealDamageAndFeedback + its out-let), but be safe: include any
        // immediately-following statements that write the HasHit/BattleDamages out-locals
        // by matching the same statement that contains the VirtualFunction call.
        var copies = new List<KismetExpression>();
        for (int c = 0; c < ke.ExtraCritStrikes - 1; c++)
            copies.Add(MayhemFixKit.CloneKismet(stmts[extraDealIdx]));

        uint insertOffset = 0;
        for (int i = 0; i <= extraDealIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
        uint extra = 0;
        foreach (var c in copies) extra += (uint)c.GetSize(bp);
        MayhemFixKit.ShiftAbsolute(bp, fn, insertOffset, extra, null);
        var list = fn.ScriptBytecode.ToList();
        list.InsertRange(extraDealIdx + 1, copies);
        fn.ScriptBytecode = list.ToArray();
        System.Console.WriteLine($"[LevinDance] crit chains {ke.ExtraCritStrikes} extra strikes (cloned x{ke.ExtraCritStrikes - 1})");
        return 1;
    }


    /// <summary>
    /// Gravity Mill: turn Typhoon's zone buff spawn into dead code by flipping the
    /// CurrentActionEffectNumber == 0 gate to == -1 (never true). The ally-heal loop is
    /// neutralized separately via the cloned Typhoon_HealPercent row (0). Result: one
    /// direct earth hit, no per-turn zone, no heal.
    /// </summary>
    private static int PatchDirectNuke(UAsset bp, KismetEdits ke)
    {
        if (!ke.DirectNuke) return 0;
        var fn = OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return 0;
        int n = 0;
        foreach (var s in fn.ScriptBytecode)
        {
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (n > 0) return;
                if (e is not EX_FinalFunction ff || !ff.StackNode.IsImport()) return;
                if (ff.StackNode.ToImport(bp).ObjectName.ToString() != "EqualEqual_IntInt") return;
                bool isActionEffect = false;
                foreach (var p in ff.Parameters)
                    if (p is EX_Context c && c.ContextExpression is EX_InstanceVariable iv &&
                        PtrName(iv.Variable) == "CurrentActionEffectNumber")
                        isActionEffect = true;
                if (!isActionEffect) return;
                foreach (var p in ff.Parameters)
                    if (p is EX_IntConst ic && ic.Value == 0) { ic.Value = -1; n++; }
            });
            if (n > 0) break;
        }
        if (n > 0) System.Console.WriteLine("[GravityMill] zone buff spawn disabled (direct nuke)");
        return n > 0 ? 1 : 0;
    }

    /// <summary>
    /// Gravity Mill: apply the overcharge damage multiplier to Typhoon's direct hit.
    /// Typhoon's direct damage is one Let (Multiply = InitialDamage × QTE) with no
    /// overcharge branch; insert a JumpIfNot(IsOvercharged) gate + a second Let that
    /// multiplies the same damage local by the multiplier. The MakeDamageBuilder after it
    /// reads the updated local.
    /// </summary>
    private static int PatchOverchargeDamageMultiplier(UAsset bp, KismetEdits ke)
    {
        if (ke.OverchargeDamageMultiplier <= 0) return 0;
        var fn = OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();

        int mulIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            if (stmts[i] is EX_Let let && let.Variable is EX_LocalVariable lv &&
                PtrName(lv.Variable) == "CallFunc_Multiply_DoubleDouble_ReturnValue" &&
                let.Expression is EX_FinalFunction ff && ff.StackNode.IsImport() &&
                ff.StackNode.ToImport(bp).ObjectName.ToString() == "Multiply_DoubleDouble")
            { mulIdx = i; break; }
        }
        if (mulIdx < 0) return 0;

        EX_JumpIfNot? gate = null;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e => { if (e is EX_JumpIfNot jn && gate is null && IsOverchargedCondition(jn)) gate = jn; });
        if (gate is null) return 0;

        var mul2 = MayhemFixKit.CloneKismet(stmts[mulIdx]);
        var mulLet = (EX_Let)mul2;
        EX_FinalFunction? mulFn = null;
        MayhemFixKit.Walk(new[] { mul2 }, e =>
        {
            if (mulFn is null && e is EX_FinalFunction f && f.StackNode.IsImport() &&
                f.StackNode.ToImport(bp).ObjectName.ToString() == "Multiply_DoubleDouble")
                mulFn = f;
        });
        if (mulFn is null || mulFn.Parameters.Length < 1) return 0;
        mulFn.Parameters = new KismetExpression[]
        {
            MayhemFixKit.CloneKismet(mulLet.Variable),
            new EX_DoubleConst { Value = ke.OverchargeDamageMultiplier },
        };

        uint insertOffset = 0;
        for (int i = 0; i <= mulIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
        var cond = MayhemFixKit.CloneKismet(gate.BooleanExpression);
        uint jumpSize = 1 + 4 + (uint)cond.GetSize(bp);
        uint mulSize = (uint)mul2.GetSize(bp);
        var jump = new EX_JumpIfNot
        {
            CodeOffset = insertOffset + jumpSize + mulSize,
            BooleanExpression = cond,
        };
        MayhemFixKit.ShiftAbsolute(bp, fn, insertOffset, jumpSize + mulSize, null);
        var list = fn.ScriptBytecode.ToList();
        list.InsertRange(mulIdx + 1, new KismetExpression[] { jump, mul2 });
        fn.ScriptBytecode = list.ToArray();
        System.Console.WriteLine($"[GravityMill] overcharge damage ×{ke.OverchargeDamageMultiplier} added to direct hit");
        return 1;
    }

    /// <summary>
    /// Gravity Mill: ApplyBuff(BP_BattleBuff_Defenseless, target, N turns) gated on
    /// IsOvercharged, inserted right after the direct-hit DealDamages so every enemy hit
    /// is defencelessed for N turns. The target/caster contexts are cloned from the
    /// donor's own MakeDamageBuilder so every FFieldPath stays valid.
    /// </summary>
    private static int PatchDefenselessTurns(UAsset bp, KismetEdits ke)
    {
        if (ke.DefenselessTurns <= 0) return 0;
        var fn = OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();

        int dealIdx = -1;
        KismetExpression? target = null;
        KismetExpression? caster = null;
        KismetPropertyPointer? outPtr = null;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hasDeal = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "MakeDamageBuilder"
                    && vf.Parameters.Length >= 1 && target is null)
                    target = MayhemFixKit.CloneKismet(vf.Parameters[0]);
                if (e is EX_ObjToInterfaceCast cast && caster is null)
                    caster = MayhemFixKit.CloneKismet(cast.Target);
                if (e is EX_LocalVariable lv && outPtr is null)
                {
                    var nm = PtrName(lv.Variable);
                    if (nm == "CallFunc_SpawnObject_ReturnValue") outPtr = lv.Variable;
                }
                if (e is EX_VirtualFunction vf2 && vf2.VirtualFunctionName.ToString() == "DealDamages")
                    hasDeal = true;
            });
            if (hasDeal) dealIdx = i;
        }
        if (dealIdx < 0 || target is null || caster is null || outPtr is null) return 0;

        EX_JumpIfNot? gate = null;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e => { if (e is EX_JumpIfNot jn && gate is null && IsOverchargedCondition(jn)) gate = jn; });
        if (gate is null) return 0;

        var defenselessIdx = EnsureObjectImport(bp,
            "/Game/Gameplay/Buffs/StatsBuffs/BP_BattleBuff_Defenseless",
            "BP_BattleBuff_Defenseless_C", "BP_BattleBuff_Defenseless_C", "/Script/Engine");

        var apply = new EX_VirtualFunction
        {
            VirtualFunctionName = FName.FromString(bp, "ApplyBuff"),
            Parameters = new KismetExpression[]
            {
                new EX_ObjectConst { Value = defenselessIdx },
                target,
                new EX_IntConst { Value = ke.DefenselessTurns },
                caster,
                new EX_ByteConst { Value = 4 },
                new EX_LocalVariable { Variable = outPtr },
            },
        };

        uint insertOffset = 0;
        for (int i = 0; i <= dealIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
        var cond = MayhemFixKit.CloneKismet(gate.BooleanExpression);
        uint jumpSize = 1 + 4 + (uint)cond.GetSize(bp);
        uint applySize = (uint)apply.GetSize(bp);
        var jump = new EX_JumpIfNot
        {
            CodeOffset = insertOffset + jumpSize + applySize,
            BooleanExpression = cond,
        };
        MayhemFixKit.ShiftAbsolute(bp, fn, insertOffset, jumpSize + applySize, null);
        var list = fn.ScriptBytecode.ToList();
        list.InsertRange(dealIdx + 1, new KismetExpression[] { jump, apply });
        fn.ScriptBytecode = list.ToArray();
        System.Console.WriteLine($"[GravityMill] Defenseless {ke.DefenselessTurns} turn(s) on overcharge");
        return 1;
    }
    /// <summary>
    /// Combustion: much bigger flames. The Immolation sequence animates Niagara user
    /// parameters on FX_ExplosionFire / FX_WorldFireTrail via MovieSceneFloatSections:
    /// User.Size ramps 1 → 5000 and LifetimeMultiplier pulses 0 → 0.2 → 0 (per system).
    /// Scale the Size channels x3 (3 → 15000) and Lifetime x1.5 (0.3), leaving the
    /// Spawn Spacing (250) density channels untouched. Channels are identified by value
    /// magnitude (Size > 1000, Lifetime <= 1).
    /// </summary>
    internal static int PatchBiggerFlames(UAsset seq) => ScaleNiagaraUserParams(seq, 3f, 1.5f);

    internal static int ScaleNiagaraUserParams(UAsset seq, float sizeMult, float lifeMult)
    {
        int n = 0;
        foreach (var ex in seq.Exports)
        {
            var cls = ex.ClassIndex?.IsImport() == true ? ex.ClassIndex.ToImport(seq).ObjectName.ToString() : "";
            if (cls != "MovieSceneFloatSection" || ex is not NormalExport ne) continue;
            var prop = ne.Data.FirstOrDefault(p => p.Name.ToString() == "FloatCurve") as StructPropertyData;
            var channelProp = prop?.Value.FirstOrDefault(p => p.GetType().Name == "MovieSceneFloatChannelPropertyData");
            if (channelProp is null) continue;
            var pv = channelProp.GetType().GetProperty("Value");
            var chan = pv.GetValue(channelProp);
            var vals = (Array)chan.GetType().GetField("Values").GetValue(chan);
            float max = 0;
            foreach (var el in vals)
            {
                var f = el.GetType().GetField("Value");
                max = Math.Max(max, Math.Abs((float)f.GetValue(el)));
            }
            if (max == 0) continue;
            float mult = max > 1000 ? sizeMult : (max <= 1f ? lifeMult : 1f);
            if (mult == 1f) continue;
            for (int i = 0; i < vals.Length; i++)
            {
                var el = vals.GetValue(i);
                var f = el.GetType().GetField("Value");
                f.SetValue(el, (float)f.GetValue(el) * mult);
                vals.SetValue(el, i);
            }
            pv.SetValue(channelProp, chan);
            n++;
        }
        if (n > 0) System.Console.WriteLine($"[Combustion] bigger flames: scaled {n} FX channel(s)");
        return n;
    }

    /// <summary>
    /// Elemental Cataclysm: enlarge the per-hit elemental impacts. The Genesis sequence
    /// binds 8 BP_FX_ElementalShot actors; each binding has a MovieScene3DTransformSection
    /// whose Scale float-channel controls the impact size. Scale those channels ×1.5
    /// (keyframe values and default value). Translation/Rotation are left untouched.
    /// </summary>
    internal static int PatchBiggerImpacts(UAsset seq)
    {
        var shotSections = CollectSpawnableTransformSections(seq);
        int n = 0;
        for (int i = 0; i < seq.Exports.Count; i++)
        {
            var ex = seq.Exports[i];
            var cls = ex.ClassIndex?.IsImport() == true ? ex.ClassIndex.ToImport(seq).ObjectName.ToString() : "";
            if (cls != "MovieScene3DTransformSection" || ex is not NormalExport ne) continue;
            if (shotSections is not null && !shotSections.Contains(i)) continue;
            foreach (var prop in ne.Data)
                n += ScaleImpactChannel(prop, underScale: false);
        }
        if (n > 0) System.Console.WriteLine($"[Cataclysm] bigger impacts: scaled {n} scale channel(s)");
        else System.Console.WriteLine("[Cataclysm] bigger impacts: no scale channel found");
        return n;
    }

    private static HashSet<int>? CollectSpawnableTransformSections(UAsset seq)
    {
        static string? GuidOf(StructPropertyData s) =>
            s.Value.OfType<GuidPropertyData>().FirstOrDefault() is { } gp ? gp.Value.ToString() : null;

        NormalExport? movie = null;
        foreach (var ex in seq.Exports.OfType<NormalExport>())
            if (ex.ClassIndex?.IsImport() == true && ex.ClassIndex.ToImport(seq).ObjectName.ToString() == "MovieScene")
            { movie = ex; break; }
        if (movie is null) return null;

        // Spawnable Guids whose template is BP_FX_ElementalShot.
        var shotGuids = new HashSet<string>(StringComparer.Ordinal);
        var spawnables = movie.Data.OfType<ArrayPropertyData>().FirstOrDefault(p => p.Name.ToString() == "Spawnables");
        if (spawnables is not null)
            foreach (var se in spawnables.Value.OfType<StructPropertyData>())
            {
                string? templ = null, guid = null;
                foreach (var f in se.Value)
                {
                    var fn = f.Name?.ToString() ?? "";
                    if (fn == "ObjectTemplate" && f is ObjectPropertyData oo && oo.Value?.IsExport() == true)
                    {
                        var te = seq.Exports[oo.Value.Index - 1];
                        templ = te.ClassIndex?.IsImport() == true ? te.ClassIndex.ToImport(seq).ObjectName.ToString() : null;
                    }
                    else if (fn == "Guid" && f is StructPropertyData gs) guid = GuidOf(gs);
                }
                if (templ?.Contains("BP_FX_ElementalShot", StringComparison.Ordinal) == true && guid is not null)
                    shotGuids.Add(guid);
            }
        if (shotGuids.Count == 0) return null;

        // ObjectBinding Guid -> its MovieScene3DTransformTrack export index (1-based).
        var shotTrackIdx = new HashSet<int>();
        var bindings = movie.Data.OfType<ArrayPropertyData>().FirstOrDefault(p => p.Name.ToString() == "ObjectBindings");
        if (bindings is not null)
            foreach (var be in bindings.Value.OfType<StructPropertyData>())
            {
                string? guid = null;
                var trackIdx = new List<int>();
                foreach (var f in be.Value)
                {
                    var fn = f.Name?.ToString() ?? "";
                    if (fn == "ObjectGuid" && f is StructPropertyData gs) guid = GuidOf(gs);
                    else if (fn == "Tracks" && f is ArrayPropertyData ta)
                        foreach (var t in ta.Value.OfType<ObjectPropertyData>())
                            if (t.Value?.IsExport() == true)
                            {
                                var trExp = seq.Exports[t.Value.Index - 1];
                                if (trExp.ClassIndex?.IsImport() == true && trExp.ClassIndex.ToImport(seq).ObjectName.ToString() == "MovieScene3DTransformTrack")
                                    trackIdx.Add(t.Value.Index);
                            }
                }
                if (guid is not null && shotGuids.Contains(guid))
                    foreach (var ti in trackIdx) shotTrackIdx.Add(ti);
            }
        if (shotTrackIdx.Count == 0) return null;

        // Transform track Sections -> section export indices (0-based).
        var sectionIdx = new HashSet<int>();
        for (int i = 0; i < seq.Exports.Count; i++)
        {
            var ex = seq.Exports[i];
            if (ex.ClassIndex?.IsImport() != true) continue;
            if (ex.ClassIndex.ToImport(seq).ObjectName.ToString() != "MovieScene3DTransformTrack" || ex is not NormalExport tr) continue;
            if (!shotTrackIdx.Contains(i + 1)) continue;
            var imp = tr.Data.OfType<ArrayPropertyData>().FirstOrDefault(p => p.Name.ToString() == "Sections");
            if (imp is null) continue;
            foreach (var sec in imp.Value.OfType<ObjectPropertyData>())
                if (sec.Value?.IsExport() == true)
                    sectionIdx.Add(sec.Value.Index - 1);
        }
        return sectionIdx.Count > 0 ? sectionIdx : null;
    }

    private static int ScaleImpactChannel(PropertyData prop, bool underScale)
    {
        int n = 0;
        var name = prop.Name?.ToString() ?? "";
        bool isScale = underScale || name.Contains("Scale", StringComparison.OrdinalIgnoreCase);
        if (prop is StructPropertyData sp)
        {
            foreach (var child in sp.Value ?? new List<PropertyData>())
                n += ScaleImpactChannel(child, isScale);
            return n;
        }
        if (prop is ArrayPropertyData ap)
        {
            foreach (var child in ap.Value ?? Array.Empty<PropertyData>())
                n += ScaleImpactChannel(child, isScale);
            return n;
        }
        if (!isScale) return 0;
        if (prop is MovieSceneFloatChannelPropertyData mf)
        {
            var pv = mf.GetType().GetProperty("Value");
            var chan = pv?.GetValue(mf);
            if (chan is not null) { ScaleChannelObject(chan); pv!.SetValue(mf, chan); }
            return 1;
        }
        if (prop is MovieSceneDoubleChannelPropertyData md)
        {
            var pv = md.GetType().GetProperty("Value");
            var chan = pv?.GetValue(md);
            if (chan is not null) { ScaleChannelObject(chan); pv!.SetValue(md, chan); }
            return 1;
        }
        return 0;
    }

    private static void ScaleChannelObject(object chan)
    {
        var t = chan.GetType();
        var vals = (Array?)t.GetField("Values")?.GetValue(chan);
        if (vals is not null)
            for (int i = 0; i < vals.Length; i++)
            {
                var el = vals.GetValue(i);
                var f = el?.GetType().GetField("Value");
                if (f is null) continue;
                var v = f.GetValue(el);
                f.SetValue(el, v is float fv ? fv * 1.5f : (double)v! * 1.5);
                vals.SetValue(el, i);
            }
        var dv = t.GetField("DefaultValue");
        if (dv is not null)
        {
            var d = dv.GetValue(chan);
            dv.SetValue(chan, d is float df ? df * 1.5f : (double)d! * 1.5);
        }
    }


    /// <summary>
    /// The interrupt consequence for the Rage-family clones: the Fire Rage taken-damage
    /// buff (which applies BP_BattleBuff_Stun to Lune with TurnDuration 1 when she is hit
    /// mid-channel — the vanilla "Stuns self if interrupted") is cloned into
    /// &lt;toStem&gt; with the given TurnDuration, so the clone stuns Lune for N turns
    /// instead of 1. Each clone's skill-script retarget (ExtraPairs) points its buff
    /// references at the clone; Fire Rage keeps the vanilla 1-turn buff.
    /// </summary>
    /// <summary>
    /// Maelstrom: clone Storm Caller's unique zone buff (BP_BattleBuff_StormCaller →
    /// BP_BattleBuff_Maelstrom) and retarget the buff's hardcoded
    /// StormCaller_BaseDamage turn-end-volley read to Maelstrom_BaseDamage (0.1525).
    /// The skill BP's SpawnObject is re-pointed at the clone via Maelstrom's
    /// ExtraPairs, so Storm Caller keeps its own buff and row. Restored Sept recipe
    /// + row retarget; the native on-damage-tick hook survival is decided by the
    /// live test protocol (pure-rename clone first — the Sept A/B never isolated
    /// the splice crash from the hook claim).
    /// </summary>
    private static int CloneStormZoneBuff(string modDir)
    {
        const string fromStem = "BP_BattleBuff_StormCaller";
        const string toStem = "BP_BattleBuff_Maelstrom";
        EnsureExtracted(fromStem);
        var src = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), fromStem + ".uasset");
        if (src is null) { System.Console.WriteLine($"[{toStem}] {fromStem} not found in tables"); return 0; }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return 0;
        var dest = Path.Combine(modDir, src[idx..]).Replace(fromStem, toStem);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);
        var map = DataTableEditor.Mappings;
        var buff = map != null ? new UAsset(dest, EngineVersion.VER_UE5_4, map) : new UAsset(dest, EngineVersion.VER_UE5_4);
        // Rename the class + retarget the turn-end volley damage read to Maelstrom's row.
        RetargetPairs(buff, dest,
            (fromStem, toStem),
            ("StormCaller_BaseDamage", "Maelstrom_BaseDamage"));
        buff.FolderName = new FString("/Game/Gameplay/Buffs/UniqueBuffs/" + toStem);
        buff.Write(dest);
        System.Console.WriteLine($"[{toStem}] zone buff cloned + volley row retargeted");
        return 1;
    }

    private static int CloneTakenDamageBuff(string modDir, string fromStem, string toStem, int turnDuration)
    {
        EnsureExtracted(fromStem);
        var src = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), fromStem + ".uasset");
        if (src is null) { System.Console.WriteLine($"[{toStem}] {fromStem} not found in tables"); return 0; }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return 0;
        var dest = Path.Combine(modDir, src[idx..]).Replace(fromStem, toStem);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);
        var map = DataTableEditor.Mappings;
        var buff = map != null ? new UAsset(dest, EngineVersion.VER_UE5_4, map) : new UAsset(dest, EngineVersion.VER_UE5_4);
        RetargetPairs(buff, dest, (fromStem, toStem));
        int patched = 0;
        foreach (var fn in buff.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            foreach (var s in fn.ScriptBytecode)
            {
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (patched > 0) return;
                    if (e is not EX_FinalFunction ff) return;
                    bool hasTurn = false;
                    foreach (var p in ff.Parameters)
                        if (p is EX_NameConst nc && nc.Value.ToString() == "TurnDuration") hasTurn = true;
                    if (!hasTurn) return;
                    foreach (var p in ff.Parameters)
                        if (p is EX_IntConst ic && ic.Value == 1) { ic.Value = turnDuration; patched++; }
                });
            }
        }
        buff.FolderName = new FString("/Game/Gameplay/Buffs/UniqueBuffs/" + toStem);
        buff.Write(dest);
        System.Console.WriteLine(patched > 0
            ? $"[{toStem}] buff clone: TurnDuration 1 -> {turnDuration}"
            : $"[{toStem}] buff clone staged (TurnDuration marker not found)");
        return 1;
    }

    /// <summary>
    /// Serpenphare's Wrath: clone the Typhoon zone buff (BP_BattleBuff_Typhoon →
    /// BP_BattleBuff_SerpenpharesWrath) and retarget every Typhoon_* balancing-row read to
    /// the Serpenphare rows. The buff's per-turn heal reads Typhoon_HealPercent (cloned to
    /// SerpenpharesWrath_HealPercent = 0) so the pure-damage zone heals nothing, and its
    /// per-turn damage reads Typhoon_BaseDamage (cloned to SerpenpharesWrath_BaseDamage =
    /// 10.02). Retargeting the buff class name also makes the skill BP's SpawnObject (via
    /// the Create ExtraPairs) reference this clone.
    /// </summary>
    private static int CloneSerpenphareBuff(string modDir)
    {
        const string fromStem = "BP_BattleBuff_Typhoon";
        const string toStem = "BP_BattleBuff_SerpenpharesWrath";
        EnsureExtracted(fromStem);
        var src = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), fromStem + ".uasset");
        if (src is null) { System.Console.WriteLine($"[{toStem}] {fromStem} not found in tables"); return 0; }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return 0;
        var dest = Path.Combine(modDir, src[idx..]).Replace(fromStem, toStem);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);
        var map = DataTableEditor.Mappings;
        var buff = map != null ? new UAsset(dest, EngineVersion.VER_UE5_4, map) : new UAsset(dest, EngineVersion.VER_UE5_4);
        // Rename the class + every Typhoon_* balancing row the buff reads.
        RetargetPairs(buff, dest,
            ("BP_BattleBuff_Typhoon", toStem),
            ("Typhoon_InitialDamage", "SerpenpharesWrath_InitialDamage"),
            ("Typhoon_BaseDamage", "SerpenpharesWrath_BaseDamage"),
            ("Typhoon_BreakBarMultiplier", "SerpenpharesWrath_BreakBarMultiplier"),
            ("Typhoon_BaseDuration", "SerpenpharesWrath_BaseDuration"),
            ("Typhoon_OverchargeDuration", "SerpenpharesWrath_OverchargeDuration"),
            ("Typhoon_HealPercent", "SerpenpharesWrath_HealPercent"),
            // Per-turn tick FX: the Typhoon buff spawns FX_Test01Burst per enemy per turn.
            // Swap it for the Ice explosion and scale it up for the screen-filling zone.
            ("FX_Test01Burst", "FX_ExplosionIce"));

        // Scale the per-turn spawn system (the buff's SpawnSystemAtLocation VectorConst).
        foreach (var fne in buff.Exports.OfType<FunctionExport>())
        {
            if (fne.ScriptBytecode is null) continue;
            foreach (var s in fne.ScriptBytecode)
            {
                if (s is not EX_LetObj letObj) continue;
                EX_FinalFunction? spawn = null;
                MayhemFixKit.Walk(new[] { letObj.AssignmentExpression }, e =>
                {
                    if (spawn is null && e is EX_FinalFunction ff &&
                        ff.StackNode.IsImport() &&
                        ff.StackNode.ToImport(buff).ObjectName.ToString() == "SpawnSystemAtLocation")
                        spawn = ff;
                });
                if (spawn is null) continue;
                foreach (var p in spawn.Parameters)
                    if (p is EX_VectorConst vc)
                        vc.Value = new FVector(vc.Value.X * 6f, vc.Value.Y * 6f, vc.Value.Z * 6f);
            }
        }

        buff.FolderName = new FString("/Game/Gameplay/Buffs/UniqueBuffs/" + toStem);
        buff.Write(dest);
        System.Console.WriteLine($"[{toStem}] zone buff cloned + rows retargeted");
        return 1;
    }

    /// <summary>
    /// Fire Rage / Infernal Rage: a missed QTE interrupts the channel — Lune stuns herself
    /// (1 turn Fire Rage, 2 turns Infernal Rage) and the channel ends. The block is
    /// inserted right before the enemy loop in OnActionEffect: when the last QTE failed
    /// (FailCount &gt; 0) it runs instead of the damage. It applies the interrupt stun and
    /// removes the tracking buff (MarkBuffForRemoval); with the buff gone the ubergraph's
    /// existing cast-fail path ends the channel (ShowPlayerChoice) on the next evaluation —
    /// the same end flow the damage-interrupt uses. No lifecycle calls are made mid-action.
    /// All locals/contexts are cloned from existing statements so every FFieldPath owner
    /// stays valid; only SetIntPropertyByName needs a new import.
    /// </summary>
    private static int PatchQteFailInterrupt(UAsset bp, KismetEdits ke)
    {
        if (ke.QteFailStunTurns <= 0) return 0;
        var fn = OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();

        // Locals to reuse: the QTE FailCount and the spawned tracking-buff instance.
        EX_LocalVariable? failCount = null, buffLocal = null;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is not EX_LocalVariable lv) return;
                var nm = PtrName(lv.Variable);
                if (nm == "CallFunc_GetLastSkillComboInfo_FailCount" && failCount is null) failCount = lv;
                if (nm == "CallFunc_SpawnObject_ReturnValue" && buffLocal is null) buffLocal = lv;
            });
        if (failCount is null || buffLocal is null) return 0;

        // Template statements in OnActionEffect: the tracking-buff spawn (SpawnObject),
        // its SetPermanent call, its ActivateBuff, and the enemy-loop init (GetAliveEnemies).
        KismetExpression? spawnStmt = null, setPermanent = null, activateStmt = null;
        int loopInitIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool isSpawn = false, isPerm = false, isAct = false, isLoop = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_FinalFunction ff && ff.StackNode.IsImport() &&
                    ff.StackNode.ToImport(bp).ObjectName.ToString() == "SpawnObject") isSpawn = true;
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "SetPermanent") isPerm = true;
                if (e is EX_VirtualFunction vf2 && vf2.VirtualFunctionName.ToString() == "ActivateBuff") isAct = true;
                if (e is EX_VirtualFunction vf3 && vf3.VirtualFunctionName.ToString() == "GetAliveEnemies") isLoop = true;
            });
            if (isSpawn && spawnStmt is null) spawnStmt = stmts[i];
            if (isPerm && setPermanent is null) setPermanent = stmts[i];
            if (isAct && activateStmt is null) activateStmt = stmts[i];
            if (isLoop && loopInitIdx < 0) loopInitIdx = i;
        }
        if (spawnStmt is null || setPermanent is null || activateStmt is null || loopInitIdx < 0) return 0;

        // Buff class import (/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Stun).
        var stunIdx = EnsureObjectImport(bp, "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Stun",
            "BP_BattleBuff_Stun_C", "BP_BattleBuff_Stun_C", "/Script/Engine");

        // SetIntPropertyByName(instance, "TurnDuration", N): add the import under the
        // existing KismetSystemLibrary package import if not present.
        FPackageIndex? setIntIdx = null, kslIdx = null;
        for (int i = 0; i < bp.Imports.Count; i++)
        {
            if (bp.Imports[i].ObjectName.ToString() == "SetIntPropertyByName") setIntIdx = FPackageIndex.FromImport(i);
            if (bp.Imports[i].ObjectName.ToString() == "KismetSystemLibrary") kslIdx = FPackageIndex.FromImport(i);
        }
        if (setIntIdx is null)
        {
            if (kslIdx is null)
            {
                FPackageIndex? engIdx = null;
                for (int i = 0; i < bp.Imports.Count; i++)
                    if (bp.Imports[i].ObjectName.ToString() == "/Script/Engine" && bp.Imports[i].ClassName.ToString() == "Package") { engIdx = FPackageIndex.FromImport(i); break; }
                engIdx ??= bp.AddImport(new Import("/Script/CoreUObject", "Package", FPackageIndex.FromRawIndex(0), "/Script/Engine", false, bp));
                kslIdx = bp.AddImport(new Import("/Script/CoreUObject", "KismetSystemLibrary", engIdx, "KismetSystemLibrary", false, bp));
            }
            setIntIdx = bp.AddImport(new Import("/Script/CoreUObject", "SetIntPropertyByName", kslIdx, "SetIntPropertyByName", false, bp));
        }

        // Greater_IntInt(FailCount, 0) — already imported by the vanilla BP.
        FPackageIndex? greaterIdx = null;
        for (int i = 0; i < bp.Imports.Count; i++)
            if (bp.Imports[i].ObjectName.ToString() == "Greater_IntInt") { greaterIdx = FPackageIndex.FromImport(i); break; }
        if (greaterIdx is null) return 0;

        // 1. MarkBuffForRemoval on the tracking buff (SetPermanent clone, renamed).
        var markBuff = MayhemFixKit.CloneKismet(setPermanent);
        bool renamed = false;
        MayhemFixKit.Walk(new[] { markBuff }, e =>
        {
            if (renamed) return;
            if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "SetPermanent")
            {
                vf.VirtualFunctionName = FName.FromString(vf.VirtualFunctionName.Asset, "MarkBuffForRemoval");
                renamed = true;
            }
        });
        if (!renamed) return 0;

        // 2. SpawnObject(BP_BattleBuff_Stun_C, Self) -> the buff local (clone of the
        //    tracking-buff spawn, class swapped).
        var spawnStun = MayhemFixKit.CloneKismet(spawnStmt);
        bool swapped = false;
        MayhemFixKit.Walk(new[] { spawnStun }, e =>
        {
            if (swapped) return;
            if (e is EX_FinalFunction ff && ff.StackNode.IsImport() &&
                ff.StackNode.ToImport(bp).ObjectName.ToString() == "SpawnObject" && ff.Parameters.Length >= 2)
            {
                ff.Parameters[0] = new EX_ObjectConst { Value = stunIdx };
                swapped = true;
            }
        });
        if (!swapped) return 0;

        // 3. SetIntPropertyByName(stunInstance, "TurnDuration", N).
        var setDur = new EX_FinalFunction
        {
            StackNode = setIntIdx,
            Parameters = new KismetExpression[]
            {
                new EX_LocalVariable { Variable = buffLocal.Variable },
                new EX_NameConst { Value = FName.FromString(bp, "TurnDuration") },
                new EX_IntConst { Value = ke.QteFailStunTurns },
            },
        };

        // 4. ActivateBuff (clone of the tracking-buff activation; p0 = buff local, now
        //    the stun instance).
        var activate = MayhemFixKit.CloneKismet(activateStmt);

        // 5. Skip the damage when interrupted (jump to the trailing EX_Return).
        var gate = new EX_JumpIfNot
        {
            CodeOffset = 0, // set after shift
            BooleanExpression = new EX_FinalFunction
            {
                StackNode = greaterIdx,
                Parameters = new KismetExpression[]
                {
                    new EX_LocalVariable { Variable = failCount.Variable },
                    new EX_IntConst { Value = 0 },
                },
            },
        };
        var endJump = new EX_Jump { CodeOffset = 0 }; // set after shift

        var block = new KismetExpression[] { gate, markBuff, spawnStun, setDur, activate, endJump };

        // Insert before the enemy-loop init; on QTE fail the block runs instead of damage.
        uint insertOffset = 0;
        for (int i = 0; i < loopInitIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
        uint blockSize = 0;
        foreach (var b in block) blockSize += (uint)b.GetSize(bp);
        uint returnOffset = 0;
        foreach (var s in stmts)
        {
            if (s is EX_Return) break;
            returnOffset += (uint)s.GetSize(bp);
        }
        MayhemFixKit.ShiftAbsolute(bp, fn, insertOffset, blockSize, null);
        gate.CodeOffset = insertOffset + blockSize;    // -> loop init (skipped when failed)
        endJump.CodeOffset = returnOffset + blockSize; // -> trailing EX_Return
        var list = fn.ScriptBytecode.ToList();
        list.InsertRange(loopInitIdx, block);
        fn.ScriptBytecode = list.ToArray();
        System.Console.WriteLine($"[QteFailInterrupt] missed QTE -> self-stun {ke.QteFailStunTurns} turn(s) + channel end");
        return 1;
    }

    /// <summary>
    /// Fire Rage itself: stage the vanilla skill-script BP into the mod and apply the
    /// QTE-fail interrupt patch (1-turn self-stun, matching the vanilla interrupt), so the
    /// original move punishes a missed QTE like its Infernal Rage clone.
    /// </summary>
    private static int PatchVanillaFireRage(string modDir)
    {
        EnsureExtracted("BP_Battle_SkillScript_FireRage");
        var src = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), "BP_Battle_SkillScript_FireRage.uasset");
        if (src is null) { System.Console.WriteLine("[FireRage] BP_Battle_SkillScript_FireRage not found in tables"); return 0; }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return 0;
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);
        var map = DataTableEditor.Mappings;
        var bp = map != null ? new UAsset(dest, EngineVersion.VER_UE5_4, map) : new UAsset(dest, EngineVersion.VER_UE5_4);
        int k = PatchQteFailInterrupt(bp, new KismetEdits(QteFailStunTurns: 1));
        if (k > 0)
        {
            foreach (var fn in bp.Exports.OfType<FunctionExport>())
                if (fn.ScriptBytecode is not null)
                    MayhemFixKit.RecalcContextSkips(bp, fn);
            bp.Write(dest);
        }
        return k;
    }

    /// <summary>
    /// Stages a mod copy of a vanilla Lune skill DA and rewrites its StainsMechanic
    /// produced-stain map (and optionally the overcharge-consume map). This makes the
    /// whole asset mod-owned (like the Fire Rage / Storm Caller DA overrides) so the game
    /// is guaranteed to load the mod's version — including the SkillScript BP pointer that
    /// routes to any patched skill BP. Used to strip Light from the produce maps of
    /// Ice Lance / Wildfire / Rockslide / Crustal Crush / Hell and to fix Electrify so the
    /// game cannot fall back to the vanilla (fire→Light) skill BP.
    /// </summary>
    private static int StageVanillaDA(string modDir, string daStem,
        IReadOnlyDictionary<string, int>? produced = null,
        IReadOnlyDictionary<string, int>? consume = null,
        string? routeScriptTo = null)
    {
        EnsureExtracted(daStem);
        var src = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), daStem + ".uasset");
        if (src is null) { System.Console.WriteLine($"[{daStem}] DA not found in tables"); return 0; }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return 0;
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);
        var map = DataTableEditor.Mappings;
        var da = map != null ? new UAsset(dest, EngineVersion.VER_UE5_4, map) : new UAsset(dest, EngineVersion.VER_UE5_4);

        // Rewrite the SkillScript soft pointer so the DA routes to a patched BP if asked.
        if (routeScriptTo is not null)
        {
            foreach (var ex in da.Exports.OfType<NormalExport>())
                foreach (var p in ex.Data)
                {
                    if (p.Name.ToString().Equals("SkillScript", StringComparison.OrdinalIgnoreCase) && p is SoftObjectPropertyData soft)
                    {
                        var pkg = "/Game/Gameplay/Battle/Skills/Content/Lune/" + routeScriptTo;
                        soft.Value = new FSoftObjectPath(new FName(da, pkg), new FName(da, routeScriptTo + "_C"), null);
                        if (da.SoftObjectPathList is { Count: > 0 } list) list.Add(soft.Value);
                    }
                }
        }
        da.Write(dest);
        if (produced is not null || consume is not null)
            StainsMechanicKit.WriteFile(dest, consume, produced);
        System.Console.WriteLine($"[{daStem}] DA staged (produce={ProduceSummary(produced)}, consume={ProduceSummary(consume)}, script={routeScriptTo})");
        return 1;
    }

    private static string ProduceSummary(IReadOnlyDictionary<string, int>? m)
    {
        if (m is null) return "—";
        return string.Join("+", m.Select(kv => kv.Key + "×" + kv.Value));
    }

    /// <summary>
    /// Electrify: remove the overcharge fire→light conversion. Vanilla Electrify's
    /// OnActionEffect calls holder.CreateStainByType(Light=7, 1) inside the overcharge
    /// block, converting the consumed Fire stain into a Light stain. The player wants that
    /// conversion gone entirely (Electrify should only generate its 2 Lightning). This
    /// stages a patched copy of the vanilla BP with the Light CreateStainByType statement
    /// removed from OnActionEffect.
    /// </summary>
    private static int PatchVanillaElectrify(string modDir)
    {
        EnsureExtracted("BP_Battle_SkillScript_Electrify");
        var src = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), "BP_Battle_SkillScript_Electrify.uasset");
        if (src is null) { System.Console.WriteLine("[Electrify] BP not found in tables"); return 0; }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return 0;
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);
        var map = DataTableEditor.Mappings;
        var bp = map != null ? new UAsset(dest, EngineVersion.VER_UE5_4, map) : new UAsset(dest, EngineVersion.VER_UE5_4);
        var fn = OnActionEffect(bp);
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();

        // Find the top-level statement with CreateStainByType whose first param is Light (7).
        int rmIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (hit) return;
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "CreateStainByType"
                    && vf.Parameters.Length >= 1 && vf.Parameters[0] is EX_ByteConst bc && bc.Value == 7)
                    hit = true;
            });
            if (hit) { rmIdx = i; break; }
        }
        if (rmIdx < 0) { System.Console.WriteLine("[Electrify] Light CreateStainByType not found"); return 0; }

        uint rmOffset = 0;
        for (int i = 0; i < rmIdx; i++) rmOffset += (uint)stmts[i].GetSize(bp);
        uint rmSize = (uint)stmts[rmIdx].GetSize(bp);
        var list = fn.ScriptBytecode.ToList();
        list.RemoveAt(rmIdx);
        fn.ScriptBytecode = list.ToArray();
        ShiftAbsoluteSigned(bp, fn, rmOffset, rmSize);
        foreach (var f in bp.Exports.OfType<FunctionExport>())
            if (f.ScriptBytecode is not null)
                MayhemFixKit.RecalcContextSkips(bp, f);
        bp.Write(dest);
        System.Console.WriteLine("[Electrify] fire->light CreateStainByType removed");
        return 1;
    }

    /// <summary>
    /// Recolors the shared FX_ElementalCharge system's exposed color defaults (the charge
    /// aura around the player). The NiagaraUserRedirectionParameterStore's ParameterData
    /// holds the LinearColor defaults back to back; the first three full colors (the
    /// visible red/orange/white gradient) are repainted the given RGB.
    /// </summary>
    private static void PatchChargeSystemDefaults((float R, float G, float B) rgb)
    {
        // Stage the shared system if the mod does not carry it yet.
        EnsureExtracted("FX_ElementalCharge");
        var src = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), "FX_ElementalCharge.uasset");
        if (src is null) { System.Console.WriteLine("[ChargeColor] FX_ElementalCharge not found in tables"); return; }
        var full = src.Replace('\\', '/');
        const string marker = "/Content/";
        var cIdx = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (cIdx < 0) { System.Console.WriteLine("[ChargeColor] FX_ElementalCharge not under Content/"); return; }
        var rel = full[(cIdx + marker.Length)..];
        var path = Path.Combine(EditorPaths.RepoRoot, "work", "mod", "Sandfall", "Content", rel);
        if (!File.Exists(path)) FileCopy.AssetPair(src, path);
        var map = DataTableEditor.Mappings;
        var u = map != null
            ? new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4, map)
            : new UAssetAPI.UAsset(path, UAssetAPI.UnrealTypes.EngineVersion.VER_UE5_4);
        int patched = 0;
        foreach (var exp in u.Exports.OfType<NormalExport>())
        {
            if (exp.ObjectName.ToString() != "FX_ElementalCharge") continue;
            foreach (var pr in exp.Data)
            {
                if (pr.Name?.ToString() != "ExposedParameters" || pr is not StructPropertyData sp) continue;
                foreach (var f2 in sp.Value ?? new List<PropertyData>())
                {
                    if (f2.Name?.ToString() != "ParameterData" || f2 is not ArrayPropertyData ap) continue;
                    var bytes = ap.Value;
                    if (bytes is null || bytes.Length < 48) break;
                    var want = BitConverter.GetBytes(rgb.R).Concat(BitConverter.GetBytes(rgb.G))
                        .Concat(BitConverter.GetBytes(rgb.B)).Concat(BitConverter.GetBytes(1.0f)).ToArray();
                    for (int i = 0; i < 48; i++)
                    {
                        if (bytes[i] is BytePropertyData bd) { bd.Value = want[i % 16]; patched++; }
                    }
                }
            }
        }
        if (patched > 0)
        {
            u.Write(path);
            System.Console.WriteLine($"[ChargeColor] FX_ElementalCharge defaults painted ({rgb.R}, {rgb.G}, {rgb.B})");
        }
    }

    /// <summary>
    /// Recolors the FX_ElementalCharge aura keyframes in a cloned sequence's .uexp.
    /// The Mayhem/Blitz charge block is [frame 56][frame 60][R1,G1,B1,A1,R2,G2,B2,A2]:
    /// both keyframes are painted the given RGB (alpha stays 1).
    /// </summary>
    internal static void PatchChargeColor(string uexpPath, (float R, float G, float B) rgb)
    {
        var data = File.ReadAllBytes(uexpPath);
        var pat = new byte[8];
        Buffer.BlockCopy(BitConverter.GetBytes(56), 0, pat, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(60), 0, pat, 4, 4);
        int idx = -1;
        for (int i = 0; i + 8 <= data.Length; i++)
        {
            bool ok = true;
            for (int j = 0; j < 8; j++) if (data[i + j] != pat[j]) { ok = false; break; }
            if (ok) { idx = i; break; }
        }
        if (idx < 0) { System.Console.WriteLine("[ChargeColor] frame pair not found in " + Path.GetFileName(uexpPath)); return; }
        var vals = new[] { rgb.R, rgb.G, rgb.B, 1.0f, rgb.R, rgb.G, rgb.B, 1.0f };
        for (int i = 0; i < vals.Length; i++)
            Buffer.BlockCopy(BitConverter.GetBytes(vals[i]), 0, data, idx + 8 + i * 4, 4);
        File.WriteAllBytes(uexpPath, data);
        System.Console.WriteLine($"[ChargeColor] aura painted ({rgb.R}, {rgb.G}, {rgb.B}) in " + Path.GetFileName(uexpPath));
    }

    /// <summary>Re-bases statement offsets after an in-statement expression grew or shrank.</summary>
    private static void ResizeShift(UAsset bp, FunctionExport fn, List<KismetExpression> stmts,
        int stmtIdx, uint oldExprSize, uint newExprSize)
    {
        uint insertOffset = 0;
        for (int i = 0; i < stmtIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
        long delta = (long)newExprSize - oldExprSize;
        if (delta > 0) MayhemFixKit.ShiftAbsolute(bp, fn, insertOffset, (uint)delta, null);
        else if (delta < 0) ShiftAbsoluteSigned(bp, fn, insertOffset, (uint)(-delta));
    }

    /// <summary>
    /// Blitz Overcharge (Mayhem clone): tier key = actual consumed count (byte 6 -> 0),
    /// availability requires >=1 Lightning, 3 DealDamages per consumed stain,
    /// and 1 Light stain generated per consumed stain.
    /// </summary>
    private static int PatchBlitz(UAsset bp, KismetEdits ke)
    {
        if (!ke.TripleDamage && !ke.LightningOnly && !ke.GenerateLightPerConsume) return 0;
        int n = 0;
        var onAction = OnActionEffect(bp);
        var stmts = onAction?.ScriptBytecode?.ToList();
        if (stmts is null) return 0;

        // 1. Consume guard + tier key: the vanilla Mayhem consume loop reads
        //    ReverseStainQueue[0], skips when the top stain is Dark (`NotEqual(item, 6)`),
        //    and otherwise consumes whatever element is on top. For Blitz (Lightning-only)
        //    that must become "consume only when the top stain IS Lightning": rewrite the
        //    guard to `EqualEqual_ByteByte(item, 4)`. A blanket byte 6->0 swap is WRONG —
        //    it inverts the guard to "skip Lightning, consume anything else" (the bug
        //    where Dark stains get consumed).
        if (ke.LightningOnly)
        {
            var eqIdx = EnsureKismetMathFn(bp, "EqualEqual_ByteByte");
            foreach (var s in stmts)
            {
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    // The consume guard: NotEqual_ByteByte(item, 6) -> EqualEqual_ByteByte(item, 4).
                    if (e is EX_FinalFunction ff && ff.StackNode.IsImport() &&
                        ff.StackNode.ToImport(bp).ObjectName.ToString() == "NotEqual_ByteByte" &&
                        ff.Parameters.Length == 2 && ff.Parameters[1] is EX_ByteConst bc && bc.Value == 6)
                    {
                        ff.StackNode = eqIdx;
                        bc.Value = 4;
                        n++;
                    }
                    // The stain-count map lookup key: Temp_byte_Variable = 6 -> 0 (Empty).
                    // Tier = queueLen(4) - count(element) so the key MUST be Empty (0):
                    // 4 - emptyCount = actual stains consumed (MayhemFixKit's proven
                    // PatchByteConstAllowDark does the same 6->0). Rewriting it to 4
                    // (Lightning) gives tier = 4 - lightningCount, which INVERTS the
                    // scaling (4 Lightning -> tier 0 -> ZeroStain, no damage growth).
                    else if (e is EX_ByteConst bc2 && bc2.Value == 6)
                    {
                        bc2.Value = 0;
                        n++;
                    }
                });
            }
        }

        // 2. Availability: ValidateSkillAvailability loop check NotEqual(item, 6) ->
        //    EqualEqual(item, 4) so the move is usable only with >=1 Lightning stain.
        if (ke.LightningOnly)
        {
            var validate = bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "ValidateSkillAvailability");
            if (validate?.ScriptBytecode is not null)
            {
                var eqIdx = EnsureKismetMathFn(bp, "EqualEqual_ByteByte");
                bool patched = false;
                foreach (var s in validate.ScriptBytecode)
                {
                    MayhemFixKit.Walk(new[] { s }, e =>
                    {
                        if (patched) return;
                        if (e is EX_FinalFunction ff && ff.StackNode.IsImport() &&
                            ff.StackNode.ToImport(bp).ObjectName.ToString() == "NotEqual_ByteByte" &&
                            ff.Parameters.Length == 2 && ff.Parameters[1] is EX_ByteConst bc && bc.Value == 6)
                        {
                            ff.StackNode = eqIdx;
                            bc.Value = 4;
                            patched = true;
                        }
                    });
                    if (patched) { n++; break; }
                }
                if (!patched)
                {
                    // Fallback: first NotEqual_ByteByte with a byte 6 anywhere in Validate.
                    foreach (var s in validate.ScriptBytecode)
                    {
                        MayhemFixKit.Walk(new[] { s }, e =>
                        {
                            if (patched) return;
                            if (e is EX_FinalFunction ff && ff.StackNode.IsImport() &&
                                ff.StackNode.ToImport(bp).ObjectName.ToString() == "NotEqual_ByteByte" &&
                                ff.Parameters.Length == 2 && ff.Parameters[1] is EX_ByteConst bc2 && bc2.Value == 6)
                            {
                                ff.StackNode = eqIdx;
                                bc2.Value = 4;
                                patched = true;
                            }
                        });
                        if (patched) { n++; break; }
                    }
                }
            }
        }

        // 2b. Scan-consume (the MayhemFixKit shape that makes the vanilla Mayhem work):
        //     replace the tail-guard + tail-consume. The Blitz's queue is fixed-slot, so
        //     with fewer than 4 stains the tail slot can be Empty: the vanilla guard then
        //     bails (move activates but consumes nothing and deals nothing) and the tail
        //     consume indexes a dead slot. The scan finds the first non-Light, non-Empty
        //     stain at its true index and consumes it there, exactly like the working
        //     Mayhem. For Blitz (LightningOnly) the scan consumes ONLY Lightning — passing
        //     the element makes the scan skip everything that isn't Lightning, so Dark and
        //     Light stains stay in the queue. Must run BEFORE the element-forcing patch
        //     (3b), which then rewrites the scan's own element Let.
        if (ke.LightningOnly)
        {
            n += MayhemFixKit.PatchExcludeLight(bp, consumeOnlyElement: (byte)StainsMechanicKit.Ordinal("Lightning"));
            stmts = onAction!.ScriptBytecode.ToList();
        }

        // 3. Three actual hits per consumed stain: clone MakeDamageBuilder + DealDamages
        //    twice immediately after the original pair. A ×3 multiply is one hit (and
        //    previously wrapped the wrong arg). Clamp(9) is the SUM of the 12 hits.
        if (ke.TripleDamage)
        {
            int mdbIdx = -1, dealIdx = -1;
            for (int i = 0; i < stmts.Count; i++)
            {
                bool hasMdb = false, hasDeal = false;
                MayhemFixKit.Walk(new[] { stmts[i] }, e =>
                {
                    if (e is EX_VirtualFunction vf)
                    {
                        var nm = vf.VirtualFunctionName.ToString();
                        if (nm == "MakeDamageBuilder") hasMdb = true;
                        if (nm == "DealDamages") hasDeal = true;
                    }
                });
                if (mdbIdx < 0 && hasMdb) mdbIdx = i;
                if (mdbIdx >= 0 && hasDeal) { dealIdx = i; break; }
            }
            if (mdbIdx >= 0 && dealIdx >= mdbIdx)
            {
                var copies = new List<KismetExpression>();
                for (int c = 0; c < 2; c++)
                    for (int i = mdbIdx; i <= dealIdx; i++)
                        copies.Add(MayhemFixKit.CloneKismet(stmts[i]));
                uint insertOffset = 0;
                for (int i = 0; i <= dealIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
                uint extra = 0;
                foreach (var c in copies) extra += (uint)c.GetSize(bp);
                MayhemFixKit.ShiftAbsolute(bp, onAction!, insertOffset, extra, null);
                var list = onAction!.ScriptBytecode.ToList();
                list.InsertRange(dealIdx + 1, copies);
                onAction.ScriptBytecode = list.ToArray();
                stmts = onAction.ScriptBytecode.ToList();
                n++;
                System.Console.WriteLine($"[PatchBlitz] TripleDamage: cloned hit stmts [{mdbIdx}..{dealIdx}] x2 (3 hits/stain)");
            }
        }

        // 3b. Lightning-only rendering: the consume-element local becomes the constant
        //     Lightning (byte 4) so the guard, the damage element and the per-element FX
        //     switch all resolve to Lightning. Resize-shifted like the x3 patch.
        if (ke.LightningOnly)
        {
            int letIdx = -1;
            for (int i = 0; i < stmts.Count; i++)
            {
                // The queue-read Let (rvalue = the read result), not the ByteConst init.
                if (stmts[i] is EX_Let let && let.Variable is EX_LocalVariable lv &&
                    PtrName(lv.Variable) == "Local_StainConsumedElement" &&
                    let.Expression is not EX_ByteConst) { letIdx = i; break; }
            }
            if (letIdx >= 0)
            {
                var let = (EX_Let)stmts[letIdx];
                uint oldSize = (uint)let.Expression.GetSize(bp);
                let.Expression = new EX_ByteConst { Value = 4 };
                uint newSize = (uint)let.Expression.GetSize(bp);
                ResizeShift(bp, onAction!, stmts, letIdx, oldSize, newSize);
                n++;
            }
        }

        // 4. 1 Light (byte 7) per consumed stain: append CreateStainByType at the END of
        //    OnActionEffect (the proven Chiaroscuro position — the stain holder is mid-action
        //    otherwise), reusing the consume statement's context shape.
        if (ke.GenerateLightPerConsume)
        {
            int consumeIdx = -1;
            for (int i = 0; i < stmts.Count; i++)
            {
                bool has = false;
                MayhemFixKit.Walk(new[] { stmts[i] }, e =>
                {
                    if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "ConsumeStainAtIndex") has = true;
                });
                if (has) { consumeIdx = i; break; }
            }
            if (consumeIdx >= 0)
            {
                var gen = MayhemFixKit.CloneKismet(stmts[consumeIdx]);
                bool renamed = false;
                MayhemFixKit.Walk(new[] { gen }, e =>
                {
                    if (renamed) return;
                    if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "ConsumeStainAtIndex")
                    {
                        vf.VirtualFunctionName = FName.FromString(vf.VirtualFunctionName.Asset, "CreateStainByType");
                        vf.Parameters = new KismetExpression[]
                        {
                            new EX_ByteConst { Value = 7 },  // Light
                            new EX_True(),
                            new EX_ByteConst { Value = 1 },
                        };
                        renamed = true;
                    }
                });
                if (renamed)
                {
                    // Insert right AFTER the consume statement (the scan's consume):
                    // the holder local is set, the create runs once per consumed stain in
                    // the found path, and the function terminator stays intact. (Before the
                    // FX switch it landed in a case body the forced Lightning element never
                    // reaches; appending past EX_EndOfScript crashed the VM.)
                    int insertIdx = consumeIdx + 1;
                    uint insertOffset = 0;
                    for (int i = 0; i < insertIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
                    uint genSize = (uint)gen.GetSize(bp);
                    MayhemFixKit.ShiftAbsolute(bp, onAction!, insertOffset, genSize, null);
                    var list = onAction!.ScriptBytecode.ToList();
                    list.Insert(insertIdx, gen);
                    onAction.ScriptBytecode = list.ToArray();
                    n++;
                }
            }
        }
        // 5. FX switch: the first case check (element == 0) FALSE-jumps to the audio-only
        //    body, so the forced Lightning element never reaches the Thunder impact. Repoint
        //    that first switch jump at the Thunder case body (fetch + root + FX + jump).
        if (ke.LightningOnly)
        {
            stmts = onAction!.ScriptBytecode.ToList();
            int mdbIdx = -1;
            for (int i = 0; i < stmts.Count; i++)
            {
                bool has = false;
                MayhemFixKit.Walk(new[] { stmts[i] }, e =>
                {
                    if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "MakeDamageBuilder") has = true;
                });
                if (has) { mdbIdx = i; break; }
            }
            int jnIdx = -1;
            for (int i = mdbIdx + 1; i < stmts.Count; i++)
                if (stmts[i] is EX_JumpIfNot) { jnIdx = i; break; }
            int thunderIdx = -1;
            for (int i = 0; i < stmts.Count; i++)
            {
                bool has = false;
                MayhemFixKit.Walk(new[] { stmts[i] }, e =>
                {
                    if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "FX_ImpactThunder") has = true;
                });
                if (has) { thunderIdx = i; break; }
            }
            // The case body is [fetch][root-context][FX_ImpactThunder][jump -> audio]:
            // jump to the fetch so the root location is recomputed for the target.
            if (jnIdx >= 0 && thunderIdx >= 2)
            {
                int bodyStart = thunderIdx - 2;
                uint target = 0;
                for (int i = 0; i < bodyStart; i++) target += (uint)stmts[i].GetSize(bp);
                ((EX_JumpIfNot)stmts[jnIdx]).CodeOffset = target;
                n++;
                System.Console.WriteLine("[PatchBlitz] FX switch: first case jump -> Thunder body");
            }
        }
        return n > 0 ? 1 : 0;
    }
}

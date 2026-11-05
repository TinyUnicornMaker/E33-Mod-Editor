using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Skill Overhaul: a catalog of parameter changes to vanilla skills, toggled
/// per character and applied into the packed mod. The roster (which moves a
/// character has) is read live from the extracted skill Data Assets by the
/// editor; this kit only defines the changes themselves.
///
/// Pack semantics: every spec the module knows about (recorded in settings,
/// or default-enabled) pins its target cell — to <see cref="ChangeSpec.New"/>
/// when enabled, to <see cref="ChangeSpec.Vanilla"/> when disabled — so a
/// disabled change reverts on the next pack. The mod copy is reused when it
/// already exists, so Gameplay Tweaks / Mayhem rows on the same table are
/// preserved; this kit only ever touches its own cells.
/// </summary>
public static class SkillOverhaulKit
{
    public const string SkillBalTable = "DT_Balancing_MainCharacterSkills";

    /// <summary>
    /// Characters the overhaul covers, in card order: Gustave, Lune, Maelle,
    /// Sciel, Verso, Monoco.
    /// Specs cover edits to pre-existing vanilla cells (balancing rows, DA
    /// props); structural kit work (kismet, graphs, brand-new assets) is
    /// covered by the per-character gate (see <see cref="Settings"/>).
    /// </summary>
    public static readonly string[] SupportedCharacters = { "Gustave", "Lune", "Maelle", "Sciel", "Verso", "Monoco" };

    /// balancing rows. <see cref="Hits"/> and <see cref="Overcharge"/> are
    /// move facts used by the curve editor to plot max total damage
    /// (base × hits × overcharge) against the AP clamp.
    /// </summary>
    public sealed record ChangeSpec(
        string Character,
        string MoveId,      // skill NameID, e.g. "Immolation"
        string Field,       // short stable id, e.g. "BaseBurnToApply"
        string Display,     // "Burn stacks applied"
        string Note,        // user-facing explanation
        string Table,       // SkillBalTable or a DA_Skill_* stem
        string Row,         // balancing row name; unused for asset changes
        string Path,        // "FloatValue" or the asset property path, e.g. "APCost"
        double Vanilla,
        double New,
        bool DefaultEnabled = true,
        int Hits = 1,
        double Overcharge = 1.0)
    {
        public string Id => $"{Character}.{MoveId}.{Field}";
    }

    /// <summary>Clamp for an AP cost: linear 0.33·x to 9 AP, then 3·e^(0.24·(x−9)).</summary>
    public static double Clamp(double ap) => ap <= 9 ? 0.33 * ap : 3.0 * Math.Exp(0.24 * (ap - 9));

    /// <summary>Clamp series indexed by AP, 0..maxAp inclusive.</summary>
    public static double[] ClampSeries(int maxAp)
    {
        var arr = new double[maxAp + 1];
        for (int x = 0; x <= maxAp; x++) arr[x] = Clamp(x);
        return arr;
    }

    /// <summary>Storm-zone per-strike rule: every Lightning strike deals
    /// clamp(AP)/16 (rounded to 4). Storm Caller (3 AP) = 0.0619, Maelstrom
    /// (10 AP) = 0.2384. Single source of truth — specs, kit rows and descs
    /// all reference these so values and text can never drift apart.</summary>
    public static readonly double StormCallerPerStrike = Math.Round(Clamp(3) / 16.0, 4);
    public static readonly double MaelstromPerStrike = Math.Round(Clamp(10) / 16.0, 4);

    /// <summary>
    /// The full change catalog. Lune damage moves follow the move-guide rules:
    /// overcharged damage = the AP clamp, split by consumed-stain count (1 stain =
    /// 30% from overcharge, 2 = 50%, 3 = 70%, 4 = 90%; base = the remainder).
    /// Moves that consume Dark stains get +25% per Dark over the clamp (additive).
    /// Moves that require the four elementals (Genesis) sit at ×1.2 over the clamp.
    /// Exceptions: Mayhem/Blitz (tiered, left as-is), Fire Rage family (per-turn
    /// ramp), Terraquake/Typhoon (zone, no damage overcharge),
    /// Thermal Transfer (overcharge is +4 AP + second turn), Elemental Trick
    /// (no stains consumed). Storm Caller/Maelstrom are zone moves with their own
    /// rule: every Lightning strike = clamp(AP)/16 (StormCallerPerStrike /
    /// MaelstromPerStrike). Utility moves are no longer capped at 0.66× clamp.
    /// Earth moves get BreakBarMultiplier = 1.3 (break = 1.3 × base total; the
    /// game applies the multiplier). Healing moves: HealingLight 10-30% @ 1 AP,
    /// Rebirth 1-9% @ 9 AP, Revitalization 10-20%; gradient and other healing
    /// (Cure/TreeOfLife) untouched. Terraquake/Typhoon get base 1 / overcharge 3
    /// turns.
    /// </summary>
    public static readonly ChangeSpec[] Specs =
    {
        // ── Utility cap removed (rule 3: curve at OC, 30% from overcharge) ──
        new("Lune", "Electrify", "BaseDamage", "Base damage",
            "Utility (stain generator): 0.077 × 3 hits = 0.23 base (70% of clamp(1)); ×1.43 overcharge = 0.33 = clamp(1).",
            SkillBalTable, "Electrify_BaseDamage", "FloatValue", 0.2, 0.077, Hits: 3, Overcharge: 1.43),
        new("Lune", "Electrify", "OverchargeDamageMultiplier", "Overcharge damage",
            "1 stain → 30% from overcharge: ×1.43 (was 2.0).",
            SkillBalTable, "Electrify_OverchargeDamageMultiplier", "FloatValue", 2.0, 1.43),
        new("Lune", "Thermal Transfer", "BaseDamage", "Base damage",
            "Utility (+4 AP): no overcharge damage (OC is +4 AP + second turn); base unchanged 0.22 × 2 hits = 0.44.",
            SkillBalTable, "ThermalTransfer_BaseDamage", "FloatValue", 0.5, 0.22, Hits: 2),
        new("Lune", "Elemental Trick", "BaseDamage", "Base damage",
            "Utility (random stain): 0.1625 × 4 hits = 0.65; no stains consumed (no OC split).",
            SkillBalTable, "ElementalTrick_BaseDamage", "FloatValue", 0.3, 0.1625, Hits: 4),
        new("Lune", "Storm Caller", "APCost", "AP cost",
            "6 → 3.",
            "DA_Skill_Lune_StormCaller", "", "APCost", 6.0, 3.0),
        new("Lune", "Storm Caller", "BaseDamageOnHit", "Damage per hit",
            "StormCallerPerStrike (clamp(3)/16): 0.0619 × 2 overcharge hits = 0.12. Consumes 1 Fire (was 2), generates 1 Lightning (was Lightning+Light). Game reads BaseDamageOnHit, not BaseDamage.",
            SkillBalTable, "StormCaller_BaseDamageOnHit", "FloatValue", 0.2, StormCallerPerStrike, Hits: 2),

        // ── Damage moves: overcharged = curve (rule 3 split by stain count) ──
        new("Lune", "Immolation", "BaseDamage", "Base damage",
            "0.462 × 1 hit = 0.46 base (70% of clamp(2)); ×1.43 overcharge = 0.66 = clamp(2). Burn rows unchanged.",
            SkillBalTable, "Immolation_BaseDamage", "FloatValue", 0.7, 0.462, Overcharge: 1.43),
        new("Lune", "Immolation", "OverchargeDamageMultiplier", "Overcharge damage",
            "1 stain → 30% from overcharge: ×1.43 (was 1.5).",
            SkillBalTable, "Immolation_OverchargeDamageMultiplier", "FloatValue", 1.5, 1.43),
        new("Lune", "Earthquake", "BaseDamage", "Base damage",
            "0.693 × 1 hit = 0.69 base (70% of clamp(3)); ×1.43 overcharge = 0.99 = clamp(3).",
            SkillBalTable, "Earthquake_BaseDamage", "FloatValue", 1.2, 0.693, Overcharge: 1.43),
        new("Lune", "Earthquake", "OverchargeDamageMultiplier", "Overcharge damage",
            "1 stain → 30% from overcharge: ×1.43 (was 1.5).",
            SkillBalTable, "Earthquake_OverchargeDamageMultiplier", "FloatValue", 1.5, 1.43),
        new("Lune", "Elemental Genesis", "APCost", "AP cost",
            "4 → 9. The 4-stain hard requirement is unchanged.",
            "DA_Skill_Lune_ElementalGenesis", "", "APCost", 4.0, 9.0),
        new("Lune", "Elemental Genesis", "BaseDamage", "Base damage",
            "Always consumes 4 stains; 8 hits: 0.4455 × 8 = 3.56 = 4-elemental rule ×1.2 over clamp(9).",
            SkillBalTable, "ElementalGenesis_BaseDamage", "FloatValue", 3.0, 0.4455, Hits: 8),
        new("Lune", "Ice Gust", "BaseDamage", "Base damage",
            "0.924 × 1 hit = 0.92 base (70% of clamp(4)); ×1.43 overcharge = 1.32 = clamp(4).",
            SkillBalTable, "IceGust_BaseDamage", "FloatValue", 1.4, 0.924, Overcharge: 1.43),
        new("Lune", "Ice Gust", "OverchargeDamageMultiplier", "Overcharge damage",
            "1 stain → 30% from overcharge: ×1.43 (was 1.5).",
            SkillBalTable, "IceGust_OverchargeDamageMultiplier", "FloatValue", 1.5, 1.43),
        new("Lune", "Wildfire", "BaseDamage", "Base damage",
            "0.66 × 1 hit = 0.66 base (50% of clamp(4)); ×2.0 overcharge = 1.32 = clamp(4). Burn 3/hit unchanged.",
            SkillBalTable, "Wildfire_BaseDamage", "FloatValue", 1.25, 0.66, Overcharge: 2.0),
        new("Lune", "Crippling Tsunami", "BaseDamage", "Base damage",
            "0.495 × 1 hit = 0.50 base (30% of clamp(5)); ×3.33 overcharge = 1.65 = clamp(5). Slow unchanged.",
            SkillBalTable, "CripplingTsunami_BaseDamage", "FloatValue", 2.5, 0.495, Overcharge: 3.33),
        new("Lune", "Crippling Tsunami", "OverchargeDamageMultiplier", "Overcharge damage",
            "3 stains → 70% from overcharge: ×3.33 (was 5.0).",
            SkillBalTable, "CripplingTsunami_OverchargeDamageMultiplier", "FloatValue", 5.0, 3.33),
        new("Lune", "Earth Spike", "BaseDamage", "Base damage",
            "0.2475 × 2 hits = 0.50 base (30% of clamp(5)); ×3.33 overcharge = 1.65 = clamp(5).",
            SkillBalTable, "EarthSpike_BaseDamage", "FloatValue", 1.25, 0.2475, Hits: 2, Overcharge: 3.33),
        new("Lune", "Earth Spike", "OverchargeDamageMultiplier", "Overcharge damage",
            "3 stains → 70% from overcharge: ×3.33 (was 5.0).",
            SkillBalTable, "EarthSpike_OverchargeDamageMultiplier", "FloatValue", 5.0, 3.33),
        new("Lune", "Fire Rage", "APCost", "AP cost",
            "Ramp rework: 4 AP.",
            "DA_Skill_Lune_FireRage", "", "APCost", 5.0, 4.0),
        new("Lune", "Fire Rage", "BaseDamage", "Base damage",
            "Ramp base: 0.94 × ramp (turn 5 ×2.109) = 1.98 = clamp(6).",
            SkillBalTable, "FireRage_BaseDamage", "FloatValue", 2.5, 0.94, Overcharge: 2.0),
        new("Lune", "Fire Rage", "FirstHit", "First-hit multiplier",
            "Initial overcharged hit: 0.165 × ×2.0 overcharge = 0.33 = clamp(1).",
            SkillBalTable, "FireRage_FirstHit", "FloatValue", 1.0, 0.165),
        new("Lune", "Fire Rage", "PerTurnDeceleration", "Ramp deceleration",
            "Ramp ×1.5/turn decelerating ×0.5: turn 5 = ×2.109, then tails off toward ×2.718.",
            SkillBalTable, "FireRage_DamagePerTurnMultiplierDeceleration", "FloatValue", 0.85, 0.5),
        new("Lune", "Fire Rage", "MaxStack", "Ramp multiplier cap",
            "Safety cap (ramp converges to ~×2.55 total with base 0.94).",
            SkillBalTable, "FireRage_MaxDamageMultiplierStack", "FloatValue", 99.0, 2.5),
        new("Lune", "Thunderfall", "BaseDamage", "Base damage",
            "0.1925 × 6 hits = 1.16 base (70% of clamp(5)); ×1.43 overcharge = 1.65 = clamp(5).",
            SkillBalTable, "Thunderfall_BaseDamage", "FloatValue", 0.3, 0.1925, Hits: 6, Overcharge: 1.43),
        new("Lune", "Thunderfall", "OverchargeDamageMultiplier", "Overcharge damage",
            "1 stain → 30% from overcharge: ×1.43 (was 1.5).",
            SkillBalTable, "Thunderfall_OverchargeDamageMultiplier", "FloatValue", 1.5, 1.43),
        new("Lune", "Crustal Crush", "BaseDamage", "Base damage",
            "0.231 × 5 hits = 1.16 base (50% of clamp(7)); ×2.0 overcharge = 2.31 = clamp(7).",
            SkillBalTable, "CrustalCrush_BaseDamage", "FloatValue", 0.9, 0.231, Hits: 5, Overcharge: 2.0),
        new("Lune", "Crustal Crush", "OverchargeDamageMultiplier", "Overcharge damage",
            "2 stains → 50% from overcharge: ×2.0 (was 3.0).",
            SkillBalTable, "CrustalCrush_OverchargeDamageMultiplier", "FloatValue", 3.0, 2.0),
        new("Lune", "Lightning Dance", "BaseDamage", "Base damage",
            "0.1155 × 6 hits = 0.69 base (30% of clamp(7)); ×3.33 overcharge = 2.31 = clamp(7).",
            SkillBalTable, "LightningDance_BaseDamage", "FloatValue", 0.6, 0.1155, Hits: 6, Overcharge: 3.33),
        new("Lune", "Lightning Dance", "OverchargeDamageMultiplier", "Overcharge damage",
            "3 stains → 70% from overcharge: ×3.33 (was 5.0).",
            SkillBalTable, "LightningDance_OverchargeDamageMultiplier", "FloatValue", 5.0, 3.33),
        new("Lune", "Hell", "BaseDamage", "Base damage",
            "0.4455 × 2 hits = 0.89 base (30% of clamp(9)); ×3.33 overcharge = 2.97 = clamp(9). Burn 5/hit unchanged.",
            SkillBalTable, "Hell_BaseDamage", "FloatValue", 5.0, 0.4455, Hits: 2, Overcharge: 3.33),
        new("Lune", "Hell", "OverchargeDamageMultiplier", "Overcharge damage",
            "3 stains → 70% from overcharge: ×3.33 (was 3.0).",
            SkillBalTable, "Hell_OverchargeDamageMultiplier", "FloatValue", 3.0, 3.33),

        // ── Mayhem: base 0.33; per-stain tiers backloaded; dark ×2^dark (kismet) ──
        // total = hits(N) × base × tierMult(N) × 2^dark. Tiers 0.33/0.66/1.32/1.98 give
        // totals 0.11 / 0.44 / 1.31 / 2.61 — 83% of the damage from the 3rd+4th stain.
        // 4 dark = 2.61 × 2⁴ = 41.76. Tier = actual stains consumed (kismet fix), so the
        // One/Two/Three rows now actually apply.
        new("Lune", "Mayhem", "BaseDamage", "Base damage",
            "Per-hit base 0.33 (down from 0.99): total = hits × base × tier-mult × 2^dark.",
            SkillBalTable, "Mayhem_BaseDamage", "FloatValue", 0.6, 0.33),
        new("Lune", "Mayhem", "ZeroStain", "0 stains consumed",
            "0-stain floor 0.33 × 0.66 = 0.22 (unreachable; the skill needs ≥1 stain).",
            SkillBalTable, "Mayhem_ZeroStainDamageMultiplier", "FloatValue", 1.0, 0.66),
        new("Lune", "Mayhem", "OneStain", "1 stain consumed",
            "1 hit × 0.33 × 0.33 = 0.11.",
            SkillBalTable, "Mayhem_OneStainDamageMultiplier", "FloatValue", 1.15, 0.33),
        new("Lune", "Mayhem", "TwoStains", "2 stains consumed",
            "2 hits × 0.33 × 0.66 = 0.44.",
            SkillBalTable, "Mayhem_TwoStainsDamageMultiplier", "FloatValue", 1.5, 0.66),
        new("Lune", "Mayhem", "ThreeStains", "3 stains consumed",
            "3 hits × 0.33 × 1.32 = 1.31.",
            SkillBalTable, "Mayhem_ThreeStainsDamageMultiplier", "FloatValue", 2.0, 1.32),
        new("Lune", "Mayhem", "FourStains", "4 stains consumed",
            "4 hits × 0.33 × 1.98 = 2.61. Dark: ×2 per dark stain — 3 dark = 2.61×8 = 20.88, 4 dark = 2.61×16 = 41.76.",
            SkillBalTable, "Mayhem_FourStainsDamageMultiplier", "FloatValue", 3.0, 1.98),

        // ── Zone turns ──
        new("Lune", "Terraquake", "BaseDuration", "Base zone duration (turns)",
            "Base turns 3 → 1.",
            SkillBalTable, "Terraquake_BaseDuration", "FloatValue", 3.0, 1.0),
        new("Lune", "Terraquake", "OverchargeDuration", "Stain-empowered zone duration (turns)",
            "Stain-empowered turns 5 → 3.",
            SkillBalTable, "Terraquake_OverchargeDuration", "FloatValue", 5.0, 3.0),
        new("Lune", "Typhoon", "BaseDuration", "Base zone duration (turns)",
            "Base turns 3 → 1.",
            SkillBalTable, "Typhoon_BaseDuration", "FloatValue", 3.0, 1.0),
        new("Lune", "Typhoon", "OverchargeDuration", "Stain-empowered zone duration (turns)",
            "Stain-empowered turns 5 → 3.",
            SkillBalTable, "Typhoon_OverchargeDuration", "FloatValue", 5.0, 3.0),
        new("Lune", "Typhoon", "HealPercent", "Zone heal per turn",
            "Heals allies for 20% → 10% Health every turn.",
            SkillBalTable, "Typhoon_HealPercent", "FloatValue", 0.2, 0.1),

        // ── Earth break damage: the BreakBarMultiplier field is set to 1.3 flat; the
        // game applies it to the move's base, so break = 1.3 × base total. Tremor
        // (gradient) untouched. Descriptions show 1.3 × base total. ──
        new("Lune", "Earthquake", "BreakBarMultiplier", "Break damage",
            "Break-bar multiplier 1.0 → 1.3: break = 1.3 × 0.66 base = 0.86.",
            SkillBalTable, "Earthquake_BreakBarMultiplier", "FloatValue", 1.0, 1.3),
        new("Lune", "Earth Spike", "BreakBarMultiplier", "Break damage",
            "Break-bar multiplier 0.7 → 1.3: break = 1.3 × 0.33 base = 0.43.",
            SkillBalTable, "EarthSpike_BreakBarMultiplier", "FloatValue", 0.7, 1.3),
        new("Lune", "Terraquake", "BreakBarMultiplier", "Break damage",
            "Break-bar multiplier 1.0 → 1.3: break = 1.3 × 1.50 base = 1.95.",
            SkillBalTable, "Terraquake_BreakBarMultiplier", "FloatValue", 1.0, 1.3),
        new("Lune", "Crustal Crush", "BreakBarBaseMultiplier", "Break damage",
            "Break-bar multiplier 2.0 → 1.3: break = 1.3 × 0.77 base = 1.00.",
            SkillBalTable, "CrustalCrush_BreakBarBaseMultiplier", "FloatValue", 2.0, 1.3),

        // ── Healing moves ──
        new("Lune", "Healing Light", "BaseHeal", "Heal amount",
            "Base heal 30% → 10% (+5% per QTE → 10-30%). Still dispels statuses.",
            SkillBalTable, "HealingLight_BaseHeal", "FloatValue", 0.3, 0.1),
        new("Lune", "Healing Light", "APCost", "AP cost",
            "3 → 1.",
            "DA_Skill_Lune_HealingLight", "", "APCost", 3.0, 1.0),
        new("Lune", "Rebirth", "BaseHealValue", "Revive health",
            "Revive 30% → 1% base.",
            SkillBalTable, "Rebirth_BaseHealValue", "FloatValue", 0.3, 0.01),
        new("Lune", "Rebirth", "HealAmountPerQte", "Revive health per QTE",
            "+10% → +2% per QTE (1-9% total).",
            SkillBalTable, "Rebirth_HealAmountPerQte", "FloatValue", 0.1, 0.02),
        new("Lune", "Rebirth", "APCost", "AP cost",
            "5 → 9.",
            "DA_Skill_Lune_Rebirth", "", "APCost", 5.0, 9.0),
        new("Lune", "Revitalization", "BaseHealPercent", "Heal amount",
            "Base heal 40% → 10% (+5% per QTE → 10-20%).",
            SkillBalTable, "Revitalization_BaseHealPercent", "FloatValue", 0.4, 0.1),
        new("Lune", "Revitalization", "HealIncreasePerQte", "Heal per QTE",
            "+10% → +5% per QTE.",
            SkillBalTable, "Revitalization_HealIncreasePerQte", "FloatValue", 0.1, 0.05),

        // ── Storm Caller keeps its own BaseDamage row (Maelstrom has its own
        // cloned buff + Maelstrom_BaseDamage row): pin StormCallerPerStrike so
        // packs that still carry older values (vanilla 0.6, shared-row 0.3178)
        // converge. ──
        new("Lune", "Storm Caller", "BaseDamage", "Base damage",
            "StormCallerPerStrike (clamp(3)/16 = 0.0619) — Maelstrom's volley reads its own Maelstrom_BaseDamage (0.2384) via its cloned buff. The game reads BaseDamageOnHit for Storm Caller's overcharge hits.",
            SkillBalTable, "StormCaller_BaseDamage", "FloatValue", 0.6, StormCallerPerStrike, Hits: 1),

        // ── Gustave: rebalance rows are kit-created <Move>_Gustave_ rows, so
        // "Vanilla" here is the shared donor-row value (per-move off = the move
        // behaves as vanilla while the retarget stays in place) ──
        new("Gustave", "Combo1", "BaseDamage", "Base damage",
            "Lumière Assault: donor 0.25 → 0.198 per hit (5 hits = clamp).",
            SkillBalTable, "Combo1_Gustave_BaseDamage", "FloatValue", 0.25, 0.198, Hits: 5),
        new("Gustave", "FromFire", "BaseDamage", "Base damage",
            "From Fire: donor 0.625 → 0.44 per hit (3 hits = clamp).",
            SkillBalTable, "FromFire_Gustave_BaseDamage", "FloatValue", 0.625, 0.44, Hits: 3),
        new("Gustave", "MarkingShot", "BaseDamage", "Base damage",
            "Marking Shot: donor 1 → 0.33 (50% of clamp, ×2 overcharge).",
            SkillBalTable, "MarkingShot_Gustave_BaseDamage", "FloatValue", 1.0, 0.33, Hits: 1),
        new("Gustave", "Shatter", "BaseDamage", "Base damage",
            "Shatter: donor 5 → 2.64 = clamp(8).",
            SkillBalTable, "PerfectBreak_Gustave_BaseDamage", "FloatValue", 5.0, 2.64, Hits: 1),
        new("Gustave", "Recovery", "BaseHeal", "Heal amount",
            "Recovery: donor 50% → 40%.",
            SkillBalTable, "PerfectRecovery_Gustave_BaseHeal", "FloatValue", 0.5, 0.4, Hits: 1),
        new("Gustave", "StrikeStorm", "BaseDamage", "Base damage",
            "Strike Storm: donor 0.7 → uniform 0.385 per hit (6 hits = clamp).",
            SkillBalTable, "StrikeStorm_Gustave_BaseDamage", "FloatValue", 0.7, 0.385, Hits: 6),
        new("Gustave", "Shatter", "APCost", "AP cost",
            "5 → 8.",
            "DA_Skill_Gustave_PerfectBreak", "", "APCost", 5.0, 8.0),
        new("Gustave", "Overcharge", "APCost", "AP cost",
            "4 → 8 (charge-spending finisher).",
            "DA_Skill_Gustave_UnleashCharge", "", "APCost", 4.0, 8.0),
        new("Gustave", "Overcharge", "BaseDamage", "Base damage",
            "Overcharge at 0 charges: 1.5 → 0.99.",
            SkillBalTable, "UnleashCharge_BaseDamage", "FloatValue", 1.5, 0.99, Hits: 1),
        new("Gustave", "Overcharge", "DamageIncreasePerCharge", "Damage per charge",
            "Backloaded per-charge table: +20% → +6% per charge.",
            SkillBalTable, "UnleashCharge_DamageIncreasePerCharge", "FloatValue", 0.2, 0.06, Hits: 1),
        new("Gustave", "Overcharge", "DamageMultiplierFullCharges", "Full-charge multiplier",
            "×1.25 → ×2.5 at 10 charges.",
            SkillBalTable, "UnleashCharge_DamageMultiplierFullCharges", "FloatValue", 1.25, 2.5, Hits: 1),
        new("Gustave", "Overcharge", "BreakBarMultiplier", "Break per charge level",
            "×1 → ×1.5 break per charge level.",
            SkillBalTable, "UnleashCharge_BreakBarMultiplier", "FloatValue", 1.0, 1.5, Hits: 1),

        // ── Maelle: 0.75 × clamp rebalance (New calls MaelleSkillsKit.PerHit so
        // the specs stay in sync with the kit by construction) ──
        new("Maelle", "OffensiveSwitch", "BaseDamage", "Base damage",
            "Offensive Switch: 1 → 0.2475.",
            SkillBalTable, "OffensiveSwitch_BaseDamage", "FloatValue", 1.0, MaelleSkillsKit.PerHit(1, 1), Hits: 1),
        new("Maelle", "ThermalArc", "BaseDamage", "Base damage",
            "Thermal Arc: 0.8 → 0.495 per hit (2 hits).",
            SkillBalTable, "InvigoratingFire_BaseDamage", "FloatValue", 0.8, MaelleSkillsKit.PerHit(4, 2), Hits: 2),
        new("Maelle", "ThermalArc", "DamageIncreasePerBurn", "Damage per Burn",
            "Consume path +5% per Burn: 0.4 → 0.05.",
            SkillBalTable, "InvigoratingFire_DamageIncreasePerBurn", "FloatValue", 0.4, 0.05, Hits: 2),
        new("Maelle", "FleuretFury", "BaseDamage", "Base damage",
            "Electric Fury: 0.8 → 0.7425 per hit (3 hits).",
            SkillBalTable, "FleuretFury_BaseDamage", "FloatValue", 0.8, MaelleSkillsKit.PerHit(9, 3), Hits: 3),
        new("Maelle", "FleuretFury", "APCost", "AP cost",
            "6 → 9.",
            "DA_Skill_Maelle_FleuretFury", "", "APCost", 6.0, 9.0),
        new("Maelle", "BreakingRules", "BaseDamage", "Base damage",
            "Breaking Rules: 0.75 → 0.61875 per hit (2 hits).",
            SkillBalTable, "BreakingRules_BaseDamage", "FloatValue", 0.75, MaelleSkillsKit.PerHit(5, 2), Hits: 2),
        new("Maelle", "BreakingRules", "APCost", "AP cost",
            "3 → 5.",
            "DA_Skill_Maelle_BreakingRules", "", "APCost", 3.0, 5.0),
        new("Maelle", "Payback", "BaseDamage", "Base damage",
            "Payback: 5 → 1.2375.",
            SkillBalTable, "Payback_BaseDamage", "FloatValue", 5.0, MaelleSkillsKit.PerHit(5, 1), Hits: 1),
        new("Maelle", "Payback", "APCost", "AP cost",
            "9 → 5.",
            "DA_Skill_Maelle_NEW13_Payback", "", "APCost", 9.0, 5.0),
        new("Maelle", "GustavesHomage", "BaseDamage", "Base damage",
            "Gustave's Homage: 0.5 → per-hit share of 0.75×clamp(13) (8 hits).",
            SkillBalTable, "GustaveSMemoire_BaseDamage", "FloatValue", 0.5, MaelleSkillsKit.PerHit(13, 8), Hits: 8),
        new("Maelle", "GustavesHomage", "APCost", "AP cost",
            "8 → 13.",
            "DA_Skill_Maelle_GustaveSMemoire", "", "APCost", 8.0, 13.0),
        new("Maelle", "GuardDown", "DefenslessDuration", "Defenceless duration",
            "3 → 2 turns.",
            SkillBalTable, "GuardDown_DefenslessDuration", "FloatValue", 3.0, 2.0, Hits: 1),
        new("Maelle", "GuardUp", "ShellDuration", "Shell duration",
            "3 → 2 turns.",
            SkillBalTable, "GuardUp_ShellDuration", "FloatValue", 3.0, 2.0, Hits: 1),
        new("Maelle", "FencersFlurry", "BaseDamage", "Base damage",
            "Fencer's Flurry: 1.4 → 0.99 (curve rule, 1 hit AoE).",
            SkillBalTable, "FencersFlurry_BaseDamage", "FloatValue", 1.4, MaelleSkillsKit.PerHit(4, 1), Hits: 1),
        new("Maelle", "Percee", "BaseDamage", "Base damage",
            "Percée: 2.2 → 0.2475.",
            SkillBalTable, "Percee_BaseDamage", "FloatValue", 2.2, MaelleSkillsKit.PerHit(1, 1), Hits: 1),
        new("Maelle", "Percee", "MarkedDamageMultiplier", "Marked multiplier",
            "Marked ×2 → ×1.5.",
            SkillBalTable, "Percee_MarkedDamageMultiplier", "FloatValue", 2.0, 1.5, Hits: 1),
        new("Maelle", "Percee", "APCost", "AP cost",
            "5 → 1.",
            "DA_Skill_Maelle_NEW14_Percee", "", "APCost", 5.0, 1.0),
        new("Maelle", "MomentumStrike", "BaseDamage", "Base damage",
            "Momentum Strike: 3.5 → 1.7325.",
            SkillBalTable, "MomentumStrike_BaseDamage", "FloatValue", 3.5, MaelleSkillsKit.PerHit(7, 1), Hits: 1),
        new("Maelle", "MomentumStrike", "MarkedDamageMultiplier", "Marked multiplier",
            "Marked ×2 → ×1.5.",
            SkillBalTable, "MomentumStrike_MarkedDamageMultiplier", "FloatValue", 2.0, 1.5, Hits: 1),
        new("Maelle", "FlameEater", "BaseDamage", "Base damage",
            "Flame Eater: 1 → 0.7425.",
            SkillBalTable, "FireRise_BaseDamage", "FloatValue", 1.0, MaelleSkillsKit.PerHit(3, 1), Hits: 1),
        new("Maelle", "Pyrolyse", "BaseDamage", "Base damage",
            "Pyrolyse: 1.67 → 0.7425 per hit (3 hits).",
            SkillBalTable, "Pyrolyse_BaseDamage", "FloatValue", 1.6666667, MaelleSkillsKit.PerHit(9, 3), Hits: 3),
        new("Maelle", "SwordBallet", "BaseDamage", "Base damage",
            "Sword Ballet: 0.75 → per-hit share of 0.75×clamp(9) (5 hits).",
            SkillBalTable, "SwordBallet_BaseDamage", "FloatValue", 0.75, MaelleSkillsKit.PerHit(9, 5), Hits: 5),
        new("Maelle", "Revenge", "BaseDamage", "Base damage",
            "Revenge: 1.2 → 1.2375.",
            SkillBalTable, "Revenge_BaseDamage", "FloatValue", 1.2, MaelleSkillsKit.PerHit(5, 1), Hits: 1),
        new("Maelle", "Revenge", "DamageIncreasePerHit", "Damage per hit taken",
            "+150% → +50% per hit taken.",
            SkillBalTable, "Revenge_DamageIncreasePerHit", "FloatValue", 1.5, 0.5, Hits: 1),
        new("Maelle", "Spark", "BaseDamage", "Base damage",
            "Spark: 1 → 0.495.",
            SkillBalTable, "Spark_BaseDamage", "FloatValue", 1.0, MaelleSkillsKit.PerHit(2, 1), Hits: 1),
        new("Maelle", "Spark", "APCost", "AP cost",
            "3 → 2.",
            "DA_Skill_Maelle_NEW18_Spark", "", "APCost", 3.0, 2.0),
        new("Maelle", "Degagement", "BaseDamage", "Base damage",
            "Dégagement: 1 → 0.495.",
            SkillBalTable, "Degagement_BaseDamage", "FloatValue", 1.0, MaelleSkillsKit.PerHit(2, 1), Hits: 1),
        new("Maelle", "Degagement", "WeaknessDuration", "Weakness duration",
            "2 → 5 turns.",
            SkillBalTable, "Degagement_WeaknessDuration", "FloatValue", 2.0, 5.0, Hits: 1),
        new("Maelle", "PhantomStrike", "BaseDamage", "Base damage",
            "Phantom Strike: 0.7 → 1.1556 per hit (4 hits).",
            SkillBalTable, "PhantomStrike_BaseDamage", "FloatValue", 0.7, MaelleSkillsKit.PerHit(12, 4), Hits: 4),
        new("Maelle", "PhantomStrike", "GradientChargeAmount", "Gradient charge",
            "0.35 → 0.5.",
            SkillBalTable, "PhantomStrike_GradientChargeAmount", "FloatValue", 0.35, 0.5, Hits: 4),
        new("Maelle", "PhantomStrike", "APCost", "AP cost",
            "7 → 12.",
            "DA_Skill_Maelle_PhantomStrike", "", "APCost", 7.0, 12.0),
        new("Maelle", "BurningCanvas", "BaseDamage", "Base damage",
            "Burning Canvas: 0.4 → 0.7272 per hit (5 hits).",
            SkillBalTable, "BurningCanvas_BaseDamage", "FloatValue", 0.4, MaelleSkillsKit.PerHit(11, 5), Hits: 5),
        new("Maelle", "BurningCanvas", "DamageIncreasePerBurnStack", "Damage per Burn",
            "Global +1% per Burn rule: 0.1 → 0.01.",
            SkillBalTable, "BurningCanvas_DamageIncreasePerBurnStack", "FloatValue", 0.1, 0.01, Hits: 5),
        new("Maelle", "BurningCanvas", "APCost", "AP cost",
            "5 → 11.",
            "DA_Skill_Maelle_BurningCanvas", "", "APCost", 5.0, 11.0),
        new("Maelle", "Stendhal", "BaseDamage", "Base damage",
            "Stendhal: 15 → 9.4966 single-hit nuke.",
            SkillBalTable, "Stendhal_BaseDamage", "FloatValue", 15.0, MaelleSkillsKit.PerHit(15, 1), Hits: 1),
        new("Maelle", "Stendhal", "APCost", "AP cost",
            "8 → 15.",
            "DA_Skill_Maelle_Stendhal", "", "APCost", 8.0, 15.0),
        new("Maelle", "DeathsBargain", "APGain", "AP refill",
            "Refills Maelle's full 21-AP bar (vanilla generic 9).",
            SkillBalTable, "LastChance_APGain", "FloatValue", 9.0, 21.0, Hits: 1),
        new("Maelle", "DeathsBargain", "APCost", "AP cost",
            "1 → 8.",
            "DA_Skill_Maelle_LastChance", "", "APCost", 1.0, 8.0),
        // ── Maelle gradient moves ──
        new("Maelle", "VirtuoseStrike", "BaseDamage", "Base damage",
            "Golgra's Combo: 0.3 → 6 per hit (5 hits, 3 charges).",
            SkillBalTable, "VirtuoseStrike_BaseDamage", "FloatValue", 0.3, 6.0, Hits: 5),
        new("Maelle", "VirtuoseStrike", "APCost", "Charges",
            "1 → 3 charges.",
            "DA_Skill_Maelle_VirtuoseStrike", "", "APCost", 1.0, 3.0),
        new("Maelle", "PhoenixFlame", "BurnAmount", "Burn stacks",
            "10 → 100 Burn.",
            SkillBalTable, "PhoenixFlame_BurnAmount", "FloatValue", 10.0, 100.0, Hits: 1),
        new("Maelle", "PhoenixFlame", "BaseRevivePercentHealth", "Revive health",
            "50% → 100% revive.",
            SkillBalTable, "PhoenixFlame_BaseRevivePercentHealth", "FloatValue", 0.5, 1.0, Hits: 1),
        new("Maelle", "PhoenixFlame", "RevivePercentHealthPerQTE", "Revive per QTE",
            "+10% → +0% per QTE (flat 100%).",
            SkillBalTable, "PhoenixFlame_RevivePercentHealthPerQTE", "FloatValue", 0.1, 0.0, Hits: 1),
        new("Maelle", "PhoenixFlame", "APCost", "Charges",
            "2 → 5 charges.",
            "DA_Skill_Maelle_PhoenixFlame", "", "APCost", 2.0, 5.0),
        new("Maelle", "Gommage", "BaseDamage", "Base damage",
            "Gommage: 50 → 100.",
            SkillBalTable, "Gommage_BaseDamage", "FloatValue", 50.0, 100.0, Hits: 1),
        new("Maelle", "Gommage", "APCost", "Charges",
            "3 → 10 charges.",
            "DA_Skill_Maelle_Gommage", "", "APCost", 3.0, 10.0),
        // ── Maelle stance rule (CombatBalanceKit writes it; the other three
        // stance rows already equal vanilla) ──
        new("Maelle", "VirtuoseStance", "DMGDealtMultiplier", "Virtuose damage",
            "Virtuose dealt ×3 → ×2 (+1 Berserk/turn via the stance kit).",
            SkillBalTable, "Maelle_VirtuoseStance_DMGDealtMultiplier", "FloatValue", 3.0, 2.0, Hits: 1),
        // ── Sciel: 0.75 × clamp rebalance (New calls ScielSkillsKit.PerHit so
        // kit + specs can't drift; variable-hit moves divide by average hits) ──
        new("Sciel", "FocusedForetell", "BaseDamage", "Base damage",
            "Focused Foretell: 1 → per-hit share of 0.75×clamp(1).",
            SkillBalTable, "FocusedForetell_BaseDamage", "FloatValue", 1.0, ScielSkillsKit.PerHit(1, 1), Hits: 1),
        new("Sciel", "FocusedForetell", "BaseForetell", "Foretell applied",
            "Applies 1 (was 2); 2-if-empty for 3 total (was 3-if-empty for 5).",
            SkillBalTable, "FocusedForetell_BaseForetell", "FloatValue", 2.0, 1.0, Hits: 1),
        new("Sciel", "FocusedForetell", "ForetellIfEmpty", "Bonus Foretell on empty target",
            "3 → 2 additional (1 base + 2 = 3 total).",
            SkillBalTable, "FocusedForetell_ForetellIfTarfet0", "FloatValue", 3.0, 2.0, Hits: 1),
        new("Sciel", "FocusedForetell", "APCost", "AP cost",
            "2 → 1.",
            "DA_Skill_Sciel_new2_Foretelling2", "", "APCost", 2.0, 1.0),
        new("Sciel", "TwilightSlash", "BaseDamage", "Base damage",
            "Twilight Slash: 1 → 0.495.",
            SkillBalTable, "GrimPrediction_Basedamage", "FloatValue", 1.0, ScielSkillsKit.PerHit(2, 1), Hits: 1),
        new("Sciel", "TwilightSlash", "DamageIncreasePerForetell", "Damage per Foretell",
            "+25% → +3% per Foretell.",
            SkillBalTable, "GrimPrediction_DamageIncreasePerForetell", "FloatValue", 0.25, 0.03, Hits: 1),
        new("Sciel", "DarkCleansing", "APCost", "AP cost",
            "Free → 2 (DA ships with no APCost prop; the kit inserts one).",
            "DA_Skill_Sciel_DarkCleansing", "", "APCost", 0.0, 2.0),
        new("Sciel", "Rush", "SpeedDuration", "Rush duration",
            "3 → 2 turns.",
            SkillBalTable, "SpeedUp_SpeedDuration", "FloatValue", 3.0, 2.0, Hits: 1),
        new("Sciel", "Rush", "APCost", "AP cost",
            "3 → 2.",
            "DA_Skill_Sciel_SpeedUp", "", "APCost", 3.0, 2.0),
        new("Sciel", "FiringShadow", "BaseDamage", "Base damage",
            "Firing Shadow: 0.5 → 0.2475 per hit (3 hits).",
            SkillBalTable, "FiringShadow_BaseDamage", "FloatValue", 0.5, ScielSkillsKit.PerHit(3, 3), Hits: 3),
        new("Sciel", "FiringShadow", "DamageMultiplierPerForetell", "Bonus per eaten Foretell",
            "Eaten Foretell ×3 → ×1.5 per hit.",
            SkillBalTable, "FiringShadow_DamageMultiplierPerForetell", "FloatValue", 2.0, 0.5, Hits: 3),
        new("Sciel", "PlentifulHarvest", "BaseDamage", "Base damage",
            "Plentiful Harvest: 1 → 0.495 per hit (2 hits), now Earth.",
            SkillBalTable, "PlentifulHarvest_BaseDamage", "FloatValue", 1.0, ScielSkillsKit.PerHit(4, 2), Hits: 2),
        new("Sciel", "PlentifulHarvest", "BreakBarMultiplier", "Break multiplier",
            "Earth break rule: 0.7 → 1.3.",
            SkillBalTable, "PlentifulHarvest_BreakBarMultiplier", "FloatValue", 0.7, 1.3, Hits: 2),
        new("Sciel", "AllSet", "APCost", "AP cost",
            "6 → 4; buffs last 1 turn (kismet, kit-side).",
            "DA_Skill_Sciel_AllSet", "", "APCost", 6.0, 4.0),
        new("Sciel", "MarkingCard", "BaseDamage", "Base damage",
            "Marking Card: 1 → 0.495 per hit (2 hits).",
            SkillBalTable, "ShadowTargeting_BaseDamage", "FloatValue", 1.0, ScielSkillsKit.PerHit(4, 2), Hits: 2),
        new("Sciel", "MarkingCard", "APCost", "AP cost",
            "3 → 4.",
            "DA_Skill_Sciel_new14_ShadowTargeting", "", "APCost", 3.0, 4.0),
        new("Sciel", "BadOmen", "BaseDamage", "Base damage",
            "Bad Omen: 1 → 0.61875 per hit (2 hits).",
            SkillBalTable, "BadOmen_BaseDamage", "FloatValue", 1.0, ScielSkillsKit.PerHit(5, 2), Hits: 2),
        new("Sciel", "BadOmen", "APCost", "AP cost",
            "3 → 5.",
            "DA_Skill_Sciel_BadOmen", "", "APCost", 3.0, 5.0),
        new("Sciel", "Harvest", "BaseDamage", "Base damage",
            "Harvest: 2 → 0.7425.",
            SkillBalTable, "Harvest_BaseDamage", "FloatValue", 2.0, ScielSkillsKit.PerHit(3, 1), Hits: 1),
        new("Sciel", "Harvest", "BaseHeal", "Base heal",
            "No base heal (was 40%); 10% per Foretell.",
            SkillBalTable, "Harvest_BaseHeal", "FloatValue", 0.4, 0.0, Hits: 1),
        new("Sciel", "Harvest", "HealPercentPerForetell", "Heal per Foretell",
            "5% → 10% per Foretell.",
            SkillBalTable, "Harvest_HealPercentPerForetell", "FloatValue", 0.05, 0.10, Hits: 1),
        new("Sciel", "DelayingSlash", "BaseDamage", "Base damage",
            "Delaying Slash: 1.5 → 0.7425 per hit (2 hits).",
            SkillBalTable, "Postponed_BaseDamage", "FloatValue", 1.5, ScielSkillsKit.PerHit(6, 2), Hits: 2),
        new("Sciel", "DelayingSlash", "DamageIncreasePerForetell", "Damage per Foretell",
            "+30% → +5% per Foretell.",
            SkillBalTable, "Postponed_DamageIncreasePerForetell", "FloatValue", 0.3, 0.05, Hits: 2),
        new("Sciel", "DelayingSlash", "APCost", "AP cost",
            "5 → 6.",
            "DA_Skill_Sciel_new18_Postponed", "", "APCost", 5.0, 6.0),
        new("Sciel", "FortunesFury", "APCost", "AP cost",
            "5 → 9.",
            "DA_Skill_Sciel_FortunesFury", "", "APCost", 5.0, 9.0),
        new("Sciel", "SearingBond", "BaseDamage", "Base damage",
            "Searing Bond: 2.5 → 1.98.",
            SkillBalTable, "SearingBond_BaseDamage", "FloatValue", 2.5, ScielSkillsKit.PerHit(8, 1), Hits: 1),
        new("Sciel", "SearingBond", "APCost", "AP cost",
            "4 → 8.",
            "DA_Skill_Sciel_SearingBond", "", "APCost", 4.0, 8.0),
        new("Sciel", "SpectralSweep", "BaseDamage", "Base damage",
            "Spectral Sweep: 0.5 → 0.909 per hit (2–6 hits, avg 4).",
            SkillBalTable, "SpectralSweep_BaseDamage", "FloatValue", 0.5, ScielSkillsKit.PerHit(11, 4), Hits: 4),
        new("Sciel", "SpectralSweep", "APCost", "AP cost",
            "7 → 11.",
            "DA_Skill_Sciel_SpectralSweep", "", "APCost", 7.0, 11.0),
        new("Sciel", "FinalPath", "BaseDamage", "Base damage",
            "Final Path: 7.5 → 5.8763.",
            SkillBalTable, "FinalPath_BaseDamage", "FloatValue", 7.5, ScielSkillsKit.PerHit(13, 1), Hits: 1),
        new("Sciel", "FinalPath", "APCost", "AP cost",
            "9 → 13.",
            "DA_Skill_Sciel_FinalPath", "", "APCost", 9.0, 13.0),
        new("Sciel", "PhantomBlade", "BaseDamage", "Base damage",
            "Phantom Blade: 3 → 1.2375.",
            SkillBalTable, "ShadowDrop_BaseDamage", "FloatValue", 3.0, ScielSkillsKit.PerHit(5, 1), Hits: 1),
        new("Sciel", "PhantomBlade", "DamageIncreasePerForetell", "Damage per Foretell",
            "+35% → +5% per Foretell.",
            SkillBalTable, "ShadowDrop_DamageIncreasePerForetell", "FloatValue", 0.35, 0.05, Hits: 1),
        new("Sciel", "CardWeaver", "BaseDamage", "Base damage",
            "Card Weaver: 2 → 0.2475.",
            SkillBalTable, "CardWeaver_BaseDamage", "FloatValue", 2.0, ScielSkillsKit.PerHit(1, 1), Hits: 1),
        new("Sciel", "CardWeaver", "APCost", "AP cost",
            "3 → 1.",
            "DA_Skill_Sciel_CardWeaver", "", "APCost", 3.0, 1.0),
        new("Sciel", "DarkWave", "BaseDamage", "Base damage",
            "Dark Wave: 1 → 0.5775 per hit (3 hits).",
            SkillBalTable, "DarkWave_BaseDamage", "FloatValue", 1.0, ScielSkillsKit.PerHit(7, 3), Hits: 3),
        new("Sciel", "DarkWave", "DamageIncreasePerForetell", "Damage per Foretell",
            "+25% → +5% per Foretell.",
            SkillBalTable, "DarkWave_DamageIncreasePerForetell", "FloatValue", 0.25, 0.05, Hits: 3),
        new("Sciel", "DarkWave", "APCost", "AP cost",
            "6 → 7.",
            "DA_Skill_Sciel_DarkWave", "", "APCost", 6.0, 7.0),
        new("Sciel", "Intervention", "APGain", "AP granted",
            "Ally gains 4 → 3 AP.",
            SkillBalTable, "Intervention_APGain", "FloatValue", 4.0, 3.0, Hits: 1),
        new("Sciel", "Intervention", "APCost", "AP cost",
            "5 → 3.",
            "DA_Skill_Sciel_Intervention", "", "APCost", 5.0, 3.0),
        new("Sciel", "SealedFate", "BaseDamage", "Base damage",
            "Sealed Fate: 0.4 → 0.33 per hit (5–7 hits, avg 6).",
            SkillBalTable, "SealedFate_BaseDamage", "FloatValue", 0.4, ScielSkillsKit.PerHit(8, 6), Hits: 6),
        new("Sciel", "SealedFate", "DamageMultiplierOnForetellConsumed", "Consume multiplier",
            "Consumed Foretell ×3 → ×2.",
            SkillBalTable, "SealedFate_DamageMultiplierOnForetellConsumed", "FloatValue", 3.0, 2.0, Hits: 6),
        new("Sciel", "SealedFate", "APCost", "AP cost",
            "4 → 8.",
            "DA_Skill_Sciel_SealedFate", "", "APCost", 4.0, 8.0),
        new("Sciel", "GrimHarvest", "BaseDamage", "Base damage",
            "Grim Harvest: 3 → 2.2275.",
            SkillBalTable, "DarkAbsorption_BaseDamage", "FloatValue", 3.0, ScielSkillsKit.PerHit(9, 1), Hits: 1),
        new("Sciel", "GrimHarvest", "BaseHeal", "Base heal",
            "No base heal (was 30%); 5% per Foretell to all allies.",
            SkillBalTable, "DarkAbsorption_BaseHeal", "FloatValue", 0.3, 0.0, Hits: 1),
        new("Sciel", "GrimHarvest", "APCost", "AP cost",
            "5 → 9.",
            "DA_Skill_Sciel_new7_DarkAbsorption", "", "APCost", 5.0, 9.0),
        new("Sciel", "TwilightDance", "BaseDamage", "Base damage",
            "Twilight Dance: 1.5 → 0.909 per hit (4 hits).",
            SkillBalTable, "TwilightDance_BaseDamage", "FloatValue", 1.5, ScielSkillsKit.PerHit(11, 4), Hits: 4),
        new("Sciel", "TwilightDance", "DamageIncreasePerForetell", "Damage per Foretell",
            "+25% → +10% per Foretell.",
            SkillBalTable, "TwilightDance_DamageIncreasePerForetell", "FloatValue", 0.25, 0.10, Hits: 4),
        new("Sciel", "TwilightDance", "APCost", "AP cost",
            "9 → 11.",
            "DA_Skill_Sciel_TwilightDance", "", "APCost", 9.0, 11.0),
        new("Sciel", "OurSacrifice", "BaseDamage", "Base damage",
            "Our Sacrifice: 2 → 7.4703 (14 AP all-enemy nuke).",
            SkillBalTable, "OurSacrifice_BaseDamage", "FloatValue", 2.0, ScielSkillsKit.PerHit(14, 1), Hits: 1),
        new("Sciel", "OurSacrifice", "DamageIncreasePerForetell", "Damage per Foretell",
            "+30% → +10% per Foretell.",
            SkillBalTable, "OurSacrifice_DamageIncreasePerForetell", "FloatValue", 0.3, 0.10, Hits: 1),
        new("Sciel", "OurSacrifice", "StolenHPMultiplier", "Stolen-health multiplier",
            "Stolen-health bonus caps at ×2 (was ×4).",
            SkillBalTable, "OurSacrifice_StolenHPMultiplier", "FloatValue", 1.5, 0.5, Hits: 1),
        new("Sciel", "OurSacrifice", "APCost", "AP cost",
            "4 → 14.",
            "DA_Skill_Sciel_OurSacrifice", "", "APCost", 4.0, 14.0),
        // ── Sciel gradient finishers (exempt from the curve) ──
        new("Sciel", "ShadowBringer", "BaseDamage", "Base damage",
            "Shadow Bringer: 0.5 → 2 per hit (10 hits, 20 total).",
            SkillBalTable, "ShadowBringer_BaseDamage", "FloatValue", 0.5, 2.0, Hits: 10),
        new("Sciel", "ShadowBringer", "APCost", "Charges",
            "1 → 2 charges.",
            "DA_Skill_Sciel_ShadowBringer", "", "APCost", 1.0, 2.0),
        new("Sciel", "Doom", "BaseDamage", "Base damage",
            "Doom: 2.5 → 13.33 per hit (3 hits, 40 total); Greater debuffs (kit-side).",
            SkillBalTable, "Doom_BaseDamage", "FloatValue", 2.5, 13.333333333333334, Hits: 3),
        new("Sciel", "Doom", "APCost", "Charges",
            "2 → 4 charges.",
            "DA_Skill_Sciel_Doom", "", "APCost", 2.0, 4.0),
        new("Sciel", "EndSlice", "BaseDamage", "Base damage",
            "End Slice: 10 → flat 90 (battle-long Foretell counter kept).",
            SkillBalTable, "EndSlice_BaseDamage", "FloatValue", 10.0, 90.0, Hits: 1),
        new("Sciel", "EndSlice", "DamageIncreasePerForetell", "Damage per Foretell consumed",
            "+20% → +5% per Foretell consumed this battle.",
            SkillBalTable, "EndSlice_DamageIncreasePerForetell", "FloatValue", 0.2, 0.05, Hits: 1),
        new("Sciel", "EndSlice", "APCost", "Charges",
            "3 → 9 charges.",
            "DA_Skill_Sciel_EndSlice", "", "APCost", 3.0, 9.0),
    };

    /// <summary>
    /// Always-on stain-map edits (not toggleable). Healing Light overcharge
    /// is 1 Earth (vanilla 2). Applied via <see cref="StainsMechanicKit"/> so
    /// it stays valid after the StainsMechanic schema is grafted.
    /// </summary>
    public static readonly (string Asset, IReadOnlyDictionary<string, int> Overcharge)[] AlwaysOnStains =
    {
        ("DA_Skill_Lune_HealingLight", new Dictionary<string, int> { ["Earth"] = 1 }),
        ("DA_Skill_Lune_StormCaller", new Dictionary<string, int> { ["Fire"] = 1 }),
    };

    /// <summary>Always-on produced-stain edits (not toggleable). Storm Caller
    /// generates one Lightning stain (vanilla Lightning + Light); Fire Rage now
    /// generates only 1 Fire (vanilla Fire + Light) — GenerateStainsFromSkill reads
    /// this map, so it governs the runtime stains and the widget's generate icons.</summary>
    public static readonly (string Asset, IReadOnlyDictionary<string, int> Produced)[] AlwaysOnProducedStains =
    {
        ("DA_Skill_Lune_StormCaller", new Dictionary<string, int> { ["Lightning"] = 1 }),
        ("DA_Skill_Lune_FireRage", new Dictionary<string, int> { ["Fire"] = 1 }),
    };

    public sealed class Settings
    {
        private const string LightStainWildcardKey = "Option.LightStainWildcard";
        private readonly Dictionary<string, bool> _enabled = new(StringComparer.OrdinalIgnoreCase);

        public bool IsEnabled(ChangeSpec s) => _enabled.TryGetValue(s.Id, out var v) ? v : s.DefaultEnabled;
        public bool IsKnown(ChangeSpec s) => _enabled.ContainsKey(s.Id) || s.DefaultEnabled;

        /// <summary>Records the toggle; persists on the next save.</summary>
        public void SetEnabled(ChangeSpec s, bool on) => _enabled[s.Id] = on;

        /// <summary>
        /// Restore vanilla Light-stain substitution: a held Light covers any missing
        /// elemental requirement and is consumed in its place. Off (default) keeps
        /// today's strict requirements. Elemental Genesis and Elemental Cataclysm
        /// stay strict either way because their per-skill Validation is patched
        /// independently (see <see cref="LuneSkillsKit"/>). Mayhem still never
        /// consumes Light.
        /// </summary>
        public bool LightStainWildcard
        {
            get => _enabled.TryGetValue(LightStainWildcardKey, out var v) && v;
            set => _enabled[LightStainWildcardKey] = value;
        }

        private static string GateKey(string character) => "Character." + character;

        /// <summary>
        /// Per-character master gate. Absent = enabled: end users start with no
        /// settings file and get all three characters on. The legacy file flags
        /// (work/maelle_overhaul.json, work/lune_skills.json) are inert.
        /// </summary>
        public bool CharacterEnabled(string character)
            => !_enabled.TryGetValue(GateKey(character), out var v) || v;

        public void SetCharacterEnabled(string character, bool on)
            => _enabled[GateKey(character)] = on;

        public bool HasEnabled => Specs.Any(IsEnabled);
        public bool HasRecorded => _enabled.Count > 0;

        /// <summary>True when a pack must re-assert (enabled) or revert (recorded-disabled) changes.</summary>
        public bool IsActive => HasEnabled || HasRecorded;

        public JObject ToJson()
        {
            var o = new JObject();
            foreach (var (k, v) in _enabled) o[k] = v;
            return o;
        }

        public void FromJson(JObject? o)
        {
            _enabled.Clear();
            if (o is null) return;
            foreach (var p in o.Properties())
                if (p.Value.Type == JTokenType.Boolean)
                    _enabled[p.Name] = p.Value.Value<bool>();
        }
    }

    public static string SettingsPath => Path.Combine(EditorPaths.RepoRoot, "work", "skill_overhaul.json");

    public static Settings LoadSettings()
    {
        var s = new Settings();
        try
        {
            if (File.Exists(SettingsPath))
                s.FromJson(JObject.Parse(File.ReadAllText(SettingsPath)));
        }
        catch { /* optional */ }
        return s;
    }

    /// <summary>
    /// Applies every known spec to the packed mod tree. Targets are reused
    /// when the mod copy already exists; otherwise a fresh vanilla copy is
    /// staged from work/tables first. Enabled cells get <c>New</c>, disabled
    /// known cells get <c>Vanilla</c> so toggling off reverts.
    /// Specs whose character gate is off are skipped entirely — their revert
    /// is handled by <see cref="RevertCharacter"/>.
    /// </summary>
    public static int Apply(string modDir, Settings s, IReadOnlyList<ChangeSpec>? specs = null)
    {
        var catalog = (specs ?? Specs).Where(x => s.CharacterEnabled(x.Character)).ToArray();
        var balRows = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
        var assetEdits = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in catalog)
        {
            if (!s.IsKnown(spec)) continue;
            var value = new JValue(s.IsEnabled(spec) ? spec.New : spec.Vanilla);
            if (spec.Table == SkillBalTable)
            {
                if (!balRows.TryGetValue(spec.Row, out var f)) balRows[spec.Row] = f = new JObject();
                f[spec.Path] = value;
            }
            else
            {
                if (!assetEdits.TryGetValue(spec.Table, out var f)) assetEdits[spec.Table] = f = new JObject();
                f[spec.Path] = value;
            }
        }

        int n = 0;
        if (balRows.Count > 0)
        {
            var dest = EnsureModCopy(SkillBalTable, modDir);
            if (dest is not null)
            {
                var ed = new DataTableEditor(dest);
                n += ed.Apply(new JObject(balRows.Select(kv => new JProperty(kv.Key, kv.Value))).ToString(Formatting.None));
                ed.Save(dest);
            }
        }
        foreach (var (asset, edits) in assetEdits)
        {
            var dest = EnsureModCopy(asset, modDir);
            if (dest is null) continue;
            var ed = DataTableEditor.OpenPropertyAsset(dest);
            try
            {
                n += ed.ApplyToProperties(edits.ToString(Formatting.None));
            }
            catch (KeyNotFoundException)
            {
                // A DA can ship without the prop (Dark Cleansing has no APCost):
                // retry per-prop, inserting missing int props, then re-open.
                foreach (var p in edits.Properties())
                {
                    try
                    {
                        n += ed.ApplyToProperties(new JObject(p).ToString(Formatting.None));
                    }
                    catch (KeyNotFoundException)
                    {
                        if (p.Value is JValue v && v.Value<int?>() is int iv)
                        {
                            DataTableEditor.EnsureIntProperty(dest, p.Name, iv);
                            n++;
                            ed = DataTableEditor.OpenPropertyAsset(dest);
                        }
                        else throw;
                    }
                }
            }
            ed.Save(dest);
        }
        // The stain maps live on Lune DAs staged by the Lune kits; a gated-off
        // Lune reverts them by deleting those staged files (see RevertCharacter).
        if (s.CharacterEnabled("Lune"))
        {
            foreach (var (asset, overcharge) in AlwaysOnStains)
            {
                var dest = EnsureModCopy(asset, modDir);
                if (dest is null) continue;
                StainsMechanicKit.WriteFile(dest, overcharge, produced: null);
            }
            foreach (var (asset, produced) in AlwaysOnProducedStains)
            {
                var dest = EnsureModCopy(asset, modDir);
                if (dest is null) continue;
                StainsMechanicKit.WriteFile(dest, null, produced);
            }
        }
        return n;
    }

    private static string? EnsureModCopy(string stem, string modDir)
    {
        var existing = FindAsset(modDir, stem + ".uasset");
        if (existing is not null) return existing;
        var src = FileCopy.FindTablesAsset(stem + ".uasset");
        if (src is null) return null;
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return null;
        var dest = Path.Combine(modDir, src[idx..]);
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    private static string? FindAsset(string dir, string fileName)
    {
        if (!Directory.Exists(dir)) return null;
        return Directory.EnumerateFiles(dir, fileName, SearchOption.AllDirectories).FirstOrDefault();
    }

    /// <summary>
    /// Character-exclusive staged stems: kit-created files (no vanilla
    /// counterpart) plus mutated single-character files (vanilla BPs/DAs,
    /// skill graphs, UI/mechanic assets). Deleting the staged copy reverts to
    /// vanilla on the next pack, since the pack only packs what is staged.
    /// Shared tables (balancing, icons, strings) are NOT listed: their cells
    /// revert via spec Vanilla pins, and leftover added rows are unread by
    /// vanilla assets (documented, harmless).
    /// </summary>
    public static readonly Dictionary<string, string[]> ExclusiveStems =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Gustave"] = new[]
            {
                // New moves (DA + skill-script BP each).
                "DA_Skill_Gustave_ZoomZoom", "BP_Battle_SkillScript_Gustave_ZoomZoom",
                "DA_Skill_Gustave_HellStorm", "BP_Battle_SkillScript_Gustave_HellStorm",
                "DA_Skill_Gustave_CinderSwarm", "BP_Battle_SkillScript_Gustave_CinderSwarm",
                "DA_Skill_Gustave_ExplodingShot", "BP_Battle_SkillScript_Gustave_ExplodingShot",
                // Mutated: retargeted BPs, Overcharge BP, APCost DAs, skill graph.
                "BP_Battle_SkillScript_Gustave_Combo1", "BP_Battle_SkillScript_Gustave_FromFire",
                "BP_Battle_SkillScript_Gustave_MarkingShot", "BP_Battle_SkillScript_Gustave_StrikeStorm",
                "BP_Battle_SkillScript_Gustave_UnleashCharge",
                "DA_Skill_Gustave_PerfectBreak", "DA_Skill_Gustave_UnleashCharge",
                "DA_SkillGraph_Noah",
            },
            ["Lune"] = new[]
            {
                // New moves from the clone family + Apocalypse.
                "DA_Skill_Lune_VivifyingLight", "BP_Battle_SkillScript_VivifyingLight",
                "DA_Skill_Lune_Restoration", "BP_Battle_SkillScript_Restoration",
                "DA_Skill_Lune_Chiaroscuro", "BP_Battle_SkillScript_Chiaroscuro",
                "DA_Skill_Lune_LumiereZenithale", "BP_Battle_SkillScript_LumiereZenithale",
                "DA_Skill_Lune_ShockingWave", "BP_Battle_SkillScript_ShockingWave",
                "DA_Skill_Lune_InfernalRage", "BP_Battle_SkillScript_InfernalRage",
                "DA_Skill_Lune_SanguinaryFlames", "BP_Battle_SkillScript_SanguinaryFlames",
                "DA_Skill_Lune_Combustion", "BP_Battle_SkillScript_Combustion",
                "DA_Skill_Lune_BlitzOvercharge", "BP_Battle_SkillScript_BlitzOvercharge",
                "DA_Skill_Lune_ElementalCataclysm", "BP_Battle_SkillScript_ElementalCataclysm",
                "DA_Skill_Lune_CursedEarth", "BP_Battle_SkillScript_CursedEarth",
                "DA_Skill_Lune_GravityMill", "BP_Battle_SkillScript_GravityMill",
                "DA_Skill_Lune_Maelstrom", "BP_Battle_SkillScript_Maelstrom",
                "DA_Skill_Lune_LevinDance", "BP_Battle_SkillScript_LevinDance",
                "DA_Skill_Lune_CloudSplitter", "BP_Battle_SkillScript_CloudSplitter",
                "DA_Skill_Lune_FrostPiercer", "BP_Battle_SkillScript_FrostPiercer",
                "DA_Skill_Lune_CrushingTsunami", "BP_Battle_SkillScript_CrushingTsunami",
                "DA_Skill_Lune_SoulEater", "BP_Battle_SkillScript_SoulEater",
                "DA_Skill_Lune_SerpenpharesWrath", "BP_Battle_SkillScript_SerpenpharesWrath",
                "DA_Skill_Lune_Brimstone", "BP_Battle_SkillScript_Brimstone",
                "DA_Skill_Lune_DarkPower", "BP_Battle_SkillScript_DarkPower",
                "DA_Skill_Lune_AngelsGift", "BP_Battle_SkillScript_AngelsGift",
                "DA_Skill_Lune_Ultima", "BP_Battle_SkillScript_Ultima",
                "DA_Skill_Lune_UltimaNoir", "BP_Battle_SkillScript_UltimaNoir",
                // Cloned buffs + stain UI/mechanic.
                "BP_Buff_InfernalRage_HasTakenAnyDamage", "BP_Buff_SanguinaryFlames_HasTakenAnyDamage",
                "BP_BattleBuff_SerpenpharesWrath", "BP_BattleBuff_Maelstrom",
                "WBP_LuneGenerateSlash", "WBP_SkillButtonModifier_RequiredStainsEitherOr",
                "BP_SkillDataComponent_StainsMechanic_EitherOr",
                // Mutated: staged Lune DAs/BPs, skill graph, Mayhem BP, global
                // stain holder + stain icon (vanilla fallback covers everyone).
                "DA_Skill_Lune_Electrify", "DA_Skill_Lune_IceGust", "DA_Skill_Lune_Wildfire",
                "DA_Skill_Lune_EarthSpike", "DA_Skill_Lune_CrustalCrush", "DA_Skill_Lune_Hell",
                "DA_Skill_Lune_HealingLight", "DA_Skill_Lune_StormCaller", "DA_Skill_Lune_FireRage",
                "DA_Skill_Lune_Rebirth", "DA_Skill_Lune_ElementalGenesis",
                "BP_Battle_SkillScript_ElementalGenesis", "BP_Battle_SkillScript_FireRage",
                "BP_Battle_SkillScript_Electrify", "BP_Battle_SkillScript_Mayhem",
                "DA_SkillGraph_Lune",
                "BP_StainHolderComponent", "WBP_GenericElement_StainIconeOnly",
            },
            ["Maelle"] = new[]
            {
                // New moves (DA + skill-script BP each).
                "DA_Skill_Maelle_LumiereAbove", "BP_Battle_SkillScript_LumiereAbove",
                "DA_Skill_Maelle_ReversFatal", "BP_Battle_SkillScript_ReversFatal",
                "DA_Skill_Maelle_DeviationAbsolue", "BP_Battle_SkillScript_DeviationAbsolue",
                "DA_Skill_Maelle_ForThoseWhoComeAfter", "BP_Battle_SkillScript_ForThoseWhoComeAfter",
                "DA_Skill_Maelle_VengeanceArdente", "BP_Battle_SkillScript_VengeanceArdente",
                "DA_Skill_Maelle_AliciasNightmare", "BP_Battle_SkillScript_AliciasNightmare",
                "DA_Skill_Maelle_BladeWaltz", "BP_Battle_SkillScript_BladeWaltz",
                "DA_Skill_Maelle_LActeFinalIncendiaire", "BP_Battle_SkillScript_LActeFinalIncendiaire",
                // Stance-kit buff.
                "BP_BattleBuff_VirtuoseBerserk",
                // Mutated: APCost/retheme/gradient DAs, kismet BPs, skill graph,
                // stance mechanic component.
                "DA_Skill_Maelle_NEW19_InvigoratingFire", "DA_Skill_Maelle_FleuretFury",
                "DA_Skill_Maelle_BreakingRules", "DA_Skill_Maelle_NEW13_Payback",
                "DA_Skill_Maelle_GustaveSMemoire", "DA_Skill_Maelle_NEW14_Percee",
                "DA_Skill_Maelle_NEW18_Spark", "DA_Skill_Maelle_PhantomStrike",
                "DA_Skill_Maelle_BurningCanvas", "DA_Skill_Maelle_Stendhal",
                "DA_Skill_Maelle_LastChance", "DA_Skill_Maelle_NEW4_FireRise",
                "DA_Skill_Maelle_VirtuoseStrike", "DA_Skill_Maelle_PhoenixFlame",
                "DA_Skill_Maelle_Gommage",
                "BP_Battle_SkillScript_New19_InvigoratingFire", "BP_Battle_SkillScript_FleuretFury",
                "BP_Battle_SkillScript_BreakingRules", "BP_Battle_SkillScript_New13_Payback",
                "BP_Battle_SkillScript_New4_FireRise", "BP_Battle_SkillScript_New14_Percee",
                "BP_Battle_SkillScript_VirtuoseStrike", "BP_Battle_SkillScript_LastChance",
                "BP_Battle_SkillScript_MomentumStrike",
                "DA_SkillGraph_Maelle", "BP_StanceMechanicComponent",
            },
            ["Sciel"] = new[]
            {
                // Mutated: APCost/charge-cost DAs, Earth-rethemed DA, kismet BPs.
                "DA_Skill_Sciel_new2_Foretelling2", "DA_Skill_Sciel_DarkCleansing",
                "DA_Skill_Sciel_SpeedUp", "DA_Skill_Sciel_AllSet",
                "DA_Skill_Sciel_new14_ShadowTargeting", "DA_Skill_Sciel_BadOmen",
                "DA_Skill_Sciel_new18_Postponed", "DA_Skill_Sciel_FortunesFury",
                "DA_Skill_Sciel_SearingBond", "DA_Skill_Sciel_SpectralSweep",
                "DA_Skill_Sciel_FinalPath", "DA_Skill_Sciel_Intervention",
                "DA_Skill_Sciel_SealedFate", "DA_Skill_Sciel_new7_DarkAbsorption",
                "DA_Skill_Sciel_TwilightDance", "DA_Skill_Sciel_OurSacrifice",
                "DA_Skill_Sciel_CardWeaver", "DA_Skill_Sciel_DarkWave",
                "DA_Skill_Sciel_ShadowBringer", "DA_Skill_Sciel_Doom",
                "DA_Skill_Sciel_EndSlice", "DA_Skill_Sciel_PlentifulHarvest",
                "BP_Battle_SkillScript_Sciel_AllSet", "BP_Battle_SkillScript_Sciel_Doom",
                "BP_Battle_SkillScript_Sciel_New_PlentifulHarvest",
            },
        };

    /// <summary>
    /// Reverts one character: pins their spec cells to Vanilla, then deletes
    /// their exclusive staged files (vanilla fallback). Shared-table rows the
    /// kits created (e.g. Lune's 85 clone rows) and added icon/string rows are
    /// left in place — unread by vanilla assets once the BPs/DAs are gone.
    /// Returns (cellsPinned, filesDeleted).
    /// </summary>
    public static (int cellsPinned, int filesDeleted) RevertCharacter(string modDir, string character)
    {
        int pinned = 0, deleted = 0;
        var specs = Specs.Where(x => string.Equals(x.Character, character, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (specs.Length > 0)
        {
            var off = new Settings();
            foreach (var spec in specs) off.SetEnabled(spec, false);
            // Bypass the gate (this character is off by definition).
            pinned = Apply(modDir, off, specs);
        }
        if (ExclusiveStems.TryGetValue(character, out var stems) && Directory.Exists(modDir))
        {
            foreach (var stem in stems)
                foreach (var file in Directory.EnumerateFiles(modDir, stem + ".uasset", SearchOption.AllDirectories))
                {
                    File.Delete(file);
                    var exp = Path.ChangeExtension(file, ".uexp");
                    if (File.Exists(exp)) File.Delete(exp);
                    deleted++;
                }
        }
        return (pinned, deleted);
    }
}

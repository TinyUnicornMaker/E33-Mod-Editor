using System;
using System.Collections.Generic;

namespace E33ModEditor.Core;

/// <summary>
/// Authoritative category tags for every picto this mod creates, revives, or
/// meaningfully reworks. The game stores these in
/// <c>DT_PassiveEffects.Categories</c> (a <c>TArray&lt;E_PassiveEffectCategory&gt;</c>).
///
/// Existing enum values (0-22) are the vanilla taxonomy. The last four values
/// are appended by <see cref="PictoTagKit"/> to the shipped
/// <c>E_PassiveEffectCategory</c> asset:
///   23 Element, 24 Revive, 25 Lifesteal, 26 Luck.
/// </summary>
public static class PictoTagCatalog
{
    // Vanilla E_PassiveEffectCategory values.
    public const string FreeAim = "NewEnumerator2";
    public const string BaseAttack = "NewEnumerator3";
    public const string AP = "NewEnumerator4";
    public const string Heal = "NewEnumerator5";
    public const string Shield = "NewEnumerator6";
    public const string Critical = "NewEnumerator7";
    public const string Damage = "NewEnumerator8";
    public const string Burn = "NewEnumerator9";
    public const string Buff = "NewEnumerator10";
    public const string StatusEffects = "NewEnumerator11";
    public const string Parry = "NewEnumerator12";
    public const string Counter = "NewEnumerator13";
    public const string Death = "NewEnumerator14";
    public const string Solo = "NewEnumerator15";
    public const string Defense = "NewEnumerator16";
    public const string Speed = "NewEnumerator17";
    public const string Mark = "NewEnumerator18";
    public const string Debuff = "NewEnumerator19";
    public const string Break = "NewEnumerator20";
    public const string Items = "NewEnumerator21";
    public const string Gradient = "NewEnumerator22";

    // Values appended to the enum by PictoTagKit.
    public const string Element = "NewEnumerator23";
    public const string Revive = "NewEnumerator24";
    public const string Lifesteal = "NewEnumerator25";
    public const string Luck = "NewEnumerator26";

    /// <summary>New enum entries and their display names.</summary>
    public static readonly (string Tag, string Label)[] NewTags =
    {
        (Element, "Element"),
        (Revive, "Revive"),
        (Lifesteal, "Lifesteal"),
        (Luck, "Luck"),
    };

    /// <summary>
    /// Expected tags for one row. <paramref name="Legacy"/> is the set the
    /// donor/inherited row had before this catalog existed; it is safe to
    /// replace. Any other non-empty set is treated as a manual edit and left
    /// alone (with a warning).
    /// </summary>
    public sealed record Rule(string Id, string[] Tags, string[]? Legacy = null);

    private static string[] T(params string[] tags) => tags;

    public static readonly Rule[] Rules =
    {
        // ── Painter conversions ───────────────────────────────────────────
        // Element conversion; Damage is the broad fallback, Element is the
        // new specific tag.
        new("PainterConv_Fire", T(Damage, Element)),
        new("PainterConv_Ice", T(Damage, Element)),
        new("PainterConv_Lightning", T(Damage, Element)),
        new("PainterConv_Earth", T(Damage, Element)),
        new("PainterConv_Dark", T(Damage, Element)),
        new("PainterConv_Light", T(Damage, Element)),
        new("PainterConv_Physical", T(Damage, Element)),
        new("PainterConv_Void", T(Damage, Element)),

        // Stain pictos are removed from the current pack, but keep the rules
        // ready for when the kit is re-enabled.
        new("StainPictos_Fire2", T(Damage, Element)),
        new("StainPictos_Fire4", T(Damage, Element)),
        new("StainPictos_Ice2", T(Damage, Element)),
        new("StainPictos_Ice4", T(Damage, Element)),
        new("StainPictos_Lightning2", T(Damage, Element)),
        new("StainPictos_Lightning4", T(Damage, Element)),
        new("StainPictos_Earth2", T(Damage, Element)),
        new("StainPictos_Earth4", T(Damage, Element)),

        // ── Status powers ─────────────────────────────────────────────────
        new("CursedPower", T(Damage, StatusEffects)),
        new("CursedPower2", T(Damage, StatusEffects)),
        new("CursedPower3", T(Damage, StatusEffects)),
        new("BerserkPower1", T(Damage, StatusEffects)),
        new("BerserkPower2", T(Damage, StatusEffects)),
        new("BerserkPower3", T(Damage, StatusEffects)),
        new("BlightedPower1", T(Damage, StatusEffects)),
        new("BlightedPower2", T(Damage, StatusEffects)),
        new("BlightedPower3", T(Damage, StatusEffects)),

        // ── Burning Skin ──────────────────────────────────────────────────
        new("BurnOnHit1", T(Burn, StatusEffects)),
        new("BurnOnHit2", T(Burn, StatusEffects)),
        new("BurnOnHit3", T(Burn, StatusEffects)),
        new("BurnOnHit4", T(Burn, StatusEffects)),
        new("BurnOnHit5", T(Burn, StatusEffects)),

        // ── Dodge / parry helpers ─────────────────────────────────────────
        new("LongerDodge", T(Defense)),
        new("Dodger2", T(Defense)),
        new("Dodger3", T(Defense)),
        new("DodgeSpecialist", T(AP, Defense)),
        new("ParryHelper", T(Parry)),
        new("ParryHelper2", T(Parry)),
        new("ParryHelper3", T(Parry)),
        new("ParrySpecialist", T(Parry, Counter)),
        new("ParrySpecialist2", T(Parry, Counter)),
        new("ParrySpecialist3", T(Parry, Counter)),

        // ── Random / luck families ────────────────────────────────────────
        new("RandomDefense", T(Defense, Luck)),
        new("RandomDefense2", T(Defense, Luck)),
        new("RandomDefense3", T(Defense, Luck)),
        new("RandomDefense4", T(Defense, Luck)),
        new("RandomDefense5", T(Defense, Luck)),
        new("LuckyAim", T(FreeAim, AP, Luck)),
        new("LuckyAim2", T(FreeAim, AP, Luck)),
        new("LuckyAim3", T(FreeAim, AP, Luck)),
        new("Roulette", T(Damage, Luck), T(Damage)),
        new("Roulette2", T(Damage, Luck), T(Damage)),
        new("Roulette3", T(Damage, Luck), T(Damage)),
        new("Roulette4", T(Damage, Luck), T(Damage)),
        new("Roulette5", T(Damage, Luck), T(Damage)),
        new("GradientOnCrit", T(Gradient, Critical, Luck), T(Gradient, Critical)),
        new("GradientOnCrit2", T(Gradient, Critical, Luck), T(Gradient, Critical)),
        new("RuptureShot", T(FreeAim, StatusEffects, Debuff, Luck)),

        // ── Auto / SOS status starters ────────────────────────────────────
        new("AutoCurse", T(Buff, StatusEffects)),
        new("AutoBerserk", T(Buff, StatusEffects)),
        new("AutoBlight", T(Buff, StatusEffects)),
        new("SosCurse", T(Buff, StatusEffects), T(Damage, Buff)),
        new("SosBerserk", T(Buff, StatusEffects), T(Damage, Buff)),
        new("SosBlight", T(Buff, StatusEffects), T(Damage, Buff)),

        // ── Death / revive ────────────────────────────────────────────────
        // Cursing/Berserking/Blighting Death are status swaps of Protecting
        // Death: the Defense tag belongs to the donor, not the status.
        new("CursingDeath", T(Death, Buff, StatusEffects), T(Death, Buff, Defense)),
        new("BerserkingDeath", T(Death, Buff, StatusEffects), T(Death, Buff, Defense)),
        new("BlightingDeath", T(Death, Buff, StatusEffects), T(Death, Buff, Defense)),

        new("ReviveWithCurse", T(Revive, Death, Buff, StatusEffects), T(Death, Damage, Buff)),
        new("ReviveWithBerserk", T(Revive, Death, Buff, StatusEffects), T(Death, Damage, Buff)),
        new("ReviveWithBlight", T(Revive, Death, Buff, StatusEffects), T(Death, Damage, Buff)),
        new("ReviveWithPowerless", T(Revive, Death, Debuff, StatusEffects), T(Burn, Death, StatusEffects)),
        new("ReviveWithDefenceless", T(Revive, Death, Debuff, StatusEffects), T(Burn, Death, StatusEffects)),
        new("ReviveWithSlow", T(Revive, Death, Debuff, StatusEffects), T(Burn, Death, StatusEffects)),
        new("ReviveWithShell", T(Revive, Death, Defense, Buff)),
        new("ReviveWithRush", T(Revive, Death, Speed, Buff)),
        new("DeathsDividend", T(Damage, Revive, Death), T(Damage)),

        // ── Revived cut content ───────────────────────────────────────────
        new("AntiDizzy", T(StatusEffects, Defense)),
        new("AntiCurse", T(StatusEffects, Defense)),
        new("AntiExhaust", T(StatusEffects, Defense)),
        new("DefenseRiserDodge", T(Defense, Buff)),
        new("PowerRiserDodge", T(Damage, Buff, Defense)),
        new("CriticalHavoc", T(Critical, Damage)),
        new("EmpoweringDeath", T(Death, Buff, Damage)),

        // ── Special / hand-authored pictos ────────────────────────────────
        new("SuperSonic", T(Speed, Buff), T(Burn, StatusEffects)),
        new("LayeredCanvas", T(Burn, Buff, StatusEffects), T(Burn, StatusEffects)),
        new("CriticalChain", T(Damage, Critical), T(Damage, Parry)),
        new("CriticalChain2", T(Damage, Critical), T(Damage, Parry)),
        new("CriticalChain3", T(Damage, Critical), T(Damage, Parry)),
        new("Infinite", T(Defense)),
        new("UnoPuncho", T(Damage)),
        new("AliciasSuffering", T(Burn, StatusEffects)),

        // ── Damage / health trade-offs ────────────────────────────────────
        // Our François's Shell rows clone Glass Cannon, so the donor only
        // carried Damage; they also reduce damage taken.
        new("TitaniumShell", T(Damage, Defense), T(Damage)),
        new("TitaniumShell2", T(Damage, Defense), T(Damage)),
        new("TitaniumShell3", T(Damage, Defense), T(Damage)),

        // ── Lifesteal ─────────────────────────────────────────────────────
        new("AttackLifesteal", T(BaseAttack, Heal, Lifesteal), T(BaseAttack, Heal)),
        new("AttackLifesteal2", T(BaseAttack, Heal, Lifesteal), T(BaseAttack, Heal)),
        new("AttackLifesteal3", T(BaseAttack, Heal, Lifesteal), T(BaseAttack, Heal)),
    };

    /// <summary>All valid category tags, including the four new ones.</summary>
    public static readonly IReadOnlySet<string> AllTags = new HashSet<string>(StringComparer.Ordinal)
    {
        FreeAim, BaseAttack, AP, Heal, Shield, Critical, Damage, Burn, Buff,
        StatusEffects, Parry, Counter, Death, Solo, Defense, Speed, Mark,
        Debuff, Break, Items, Gradient, Element, Revive, Lifesteal, Luck,
    };
}

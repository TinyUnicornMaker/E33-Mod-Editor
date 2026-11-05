using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace E33ModEditor.Core;

/// <summary>
/// Patches Lune's skill descriptions in ST_MainCharacters_Skills without losing
/// the vanilla overcharge/stain info. The damage word in the first line ("low",
/// "medium", "high", ...) is replaced with the move's PER-HIT base damage as a
/// gold "N.NNx" (balancing BaseDamage rows are per-hit; multi-hit lines also
/// gain an explicit "per hit" / "per strike", matching the Maelle/Lune-kit
/// convention); overcharge lines that describe a damage bonus ("for increased
/// damage" / "greatly increased damage") get the exact overcharge multiplier
/// (": deal 1.50x more damage").
/// Everything else — the consumed-stain symbols, non-damage overcharge effects
/// (Light Stain generation, second turn, double strikes, duration extension),
/// crit lines, Break lines — is preserved verbatim. Terraquake/Typhoon
/// duration text is updated to the rebalanced values (base 1 / overcharge 3
/// turns). Gold is a new no-icon "DamageMultiplier" keyword style added to
/// DT_RichTextDecorator_KeywordStyles.
/// </summary>
public static class SkillDescriptionsKit
{
    private const string StTable = "ST_MainCharacters_Skills.uasset";
    private const string KeywordStyleTable = "DT_RichTextDecorator_KeywordStyles.uasset";

    // Gold = the game's Break/BreakDamage color.
    private const double GoldR = 0.693872, GoldG = 0.527115, GoldB = 0.23074;

    internal static string Dmg(double v) => v.ToString("0.00") + "x";
    internal static string Gold(double v) => $"<keyword id=\"DamageMultiplier\">{Dmg(v)}</>";

    /// <summary>Per move: entry key, first-line damage word(s) + per-hit base, overcharge damage multiplier (null = keep the vanilla overcharge text), extra verbatim replacements.</summary>
    private sealed record Move(string Key, string Word, double Base, double? Overcharge, string[]? Extra = null)
    {
        /// <summary>Replacement pairs applied verbatim to Long and Short.</summary>
        public (string Find, string Repl)[] Replacements()
        {
            var list = new List<(string, string)>();
            if (Word.Length > 0)
            {
                // First line: "Deals low ..." / leading "Low ..." / mid-sentence "deals high ..." -> per-hit base.
                list.Add(("Deals " + Word + " ", $"Deals {Gold(Base)} "));
                list.Add(("deals " + Word + " ", $"deals {Gold(Base)} "));
                list.Add((Word[..1].ToUpperInvariant() + Word[1..] + " ", Gold(Base) + " "));
                if (Word.Contains('|'))
                {
                    // Storm Caller: two words in one line.
                    foreach (var w in Word.Split('|'))
                    {
                        list.Add(("receive " + w + " ", $"receive {Gold(Base)} "));
                        list.Add(("and " + w + " ", $"and {Gold(Base)} "));
                    }
                }
            }
            if (Overcharge is double oc)
            {
                list.Add((" for greatly increased damage.", $": deal {Gold(oc)} more damage."));
                list.Add((": Greatly increased damage.", $": deal {Gold(oc)} more damage."));
                list.Add((" for increased damage.", $": deal {Gold(oc)} more damage."));
                list.Add((": Increased damage.", $": deal {Gold(oc)} more damage."));
                list.Add((" to deal increased damage.", $": deal {Gold(oc)} more damage."));
            }
            if (Extra is not null)
                foreach (var e in Extra)
                {
                    var sp = e.Split('|');
                    list.Add((sp[0], sp[1]));
                }
            return list.ToArray();
        }
    }

    private static readonly Move[] Moves =
    {
        new("Lune_CripplingTsunami_Description", "medium", 0.50, 3.33,
            new[]
            {
                "Applies <keyword id=\"Buff_Slow\">Slow</>\n|Applies <keyword id=\"Buff_Slow\">Slow</> for 3 turns.\n",
            }),
        // Earth moves: break damage = 1.3 × base total (BreakBarMultiplier row = 1.3).
        // Damage numbers below are per-hit; break numbers stay totals (as in vanilla).
        new("Lune_CrustalCrush_Description", "high", 0.231, 2.0,
            new[]
            {
                "<keyword id=\"Element_Earth\">Earth</> and <keyword id=\"BreakDamage\">Break damage</> 5 hits.|<keyword id=\"Element_Earth\">Earth</> damage per hit and <keyword id=\"DamageMultiplier\">1.50x</> <keyword id=\"BreakDamage\">break damage</> 5 hits.",
            }),
        new("Lune_EarthSpike_Description", "medium", 0.2475, 3.33,
            new[]
            {
                "Earth</> damage. 2 hits.|Earth</> damage per hit and <keyword id=\"DamageMultiplier\">0.65x</> <keyword id=\"BreakDamage\">break damage</>. 2 hits.",
            }),
        new("Lune_Earthquake_Description", "low", 0.69, 1.43,
            new[]
            {
                "damage to all enemies. 1 hit.|damage and <keyword id=\"DamageMultiplier\">0.90x</> <keyword id=\"BreakDamage\">break damage</> to all enemies. 1 hit.",
                "Damage to all enemies. 1 hit.|damage and <keyword id=\"DamageMultiplier\">0.90x</> <keyword id=\"BreakDamage\">break damage</> to all enemies. 1 hit.",
            }),
        new("Lune_Electrify_Description", "low", 0.077, 1.43,
            new[]
            {
                "Lightning</> damage. 3 hits.|Lightning</> damage per hit. 3 hits.",
                "\n<keyword id=\"Consume_Fire\">Consumes</> <img id=\"Stain_Fire\"/> to generate one Light Stain<img id=\"Stain_Light\"/>|",
            }),
        new("Lune_ElementalGenesis_Description", "extreme", 0.4455, null,
            new[]
            {
                "damage to all enemies. 8 hits.|damage per hit to all enemies. 8 hits.",
            }),
        new("Lune_ElementalTrick_Description", "low", 0.1625, null,
            new[]
            {
                "Earth</> damage. 4 hits.|Earth</> damage per hit. 4 hits.",
            }),
        new("Lune_FireRage_Description", "", 0, null,
            new[]
            {
                "Deals increasingly high <keyword id=\"Element_Fire\">Fire</> damage to all enemies every turn until Lune receives damage.|<keyword id=\"DamageMultiplier\">0.33x</> <keyword id=\"Element_Fire\">Fire</> damage on the first turn, ramping up to <keyword id=\"DamageMultiplier\">1.98x</> by the fifth turn.\n<keyword id=\"Consume_Generic\">Consumes</> <img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/>: deal <keyword id=\"DamageMultiplier\">2.00x</> more damage.",
                // Vanilla keeps a second "Consumes 2 Ice: Increased damage." line after the
                // self-stun line; drop it (the consume effect is already stated above) and
                // state the 1-turn interrupt self-stun in place of the vanilla tail.
                "\nStuns self if interrupted.\n<keyword id=\"Consume_Ice\">Consumes</> <img id=\"Stain_Ice\"/><img id=\"Stain_Ice\"/>: Increased damage.|\nStuns self for 1 turn if interrupted.",
            }),
        new("Lune_Hell_Description", "very high", 0.4455, 3.33,
            new[]
            {
                "Fire</> damage that applies 5|Fire</> damage per hit that applies 5",
            }),
        new("Lune_IceGust_Description", "medium", 0.92, 1.43,
            new[]
            {
                "damage that <keyword id=\"Buff_Slow\">Slows</> the target. 1 hit.|damage that <keyword id=\"Buff_Slow\">Slows</> the target for 3 turns. 1 hit.",
                "damage. <keyword id=\"Buff_Slow\">Slows</> the target. 1 hit.|damage. <keyword id=\"Buff_Slow\">Slows</> the target for 3 turns. 1 hit.",
            }),
        new("Lune_Immolation_Description", "low", 0.46, 1.43),
        new("Lune_LightningDance_Description", "very high", 0.1155, 3.33,
            new[]
            {
                "Lightning</> damage. 6 hits.|Lightning</> damage per hit. 6 hits.",
            }),
        // Storm Caller: every Lightning strike = StormCallerPerStrike
        // (clamp(3)/16 = 0.0619). Turn-end volley reads StormCaller_BaseDamage,
        // on-damage strikes and direct hits read BaseDamageOnHit — all pinned to
        // the same per-strike value. Consume is 1 Fire (AlwaysOnStains), duration
        // 3 turns. The 0.495/0.3178 pairs converge older packs (0.50x, 0.32x
        // texts) to the rule value.
        new("Lune_StormCaller_Description", "", 0, null,
            new[]
            {
                "receive medium |receive " + Gold(SkillOverhaulKit.StormCallerPerStrike) + " ",
                "and low |and " + Gold(SkillOverhaulKit.StormCallerPerStrike) + " ",
                "damage at the end of their turn,|damage per strike at the end of their turn,",
                "damage when they receive damage.|damage per strike when they receive damage.",
                "damage on turn end|damage per strike on turn end",
                "damage on damage received.|damage per strike on damage received.",
                "damage per strike on damage received.\n<keyword id=\"Consume_Fire\">Consumes</> <img id=\"Stain_Fire\"/><img id=\"Stain_Fire\"/>: Double thunder strikes.|damage per strike on damage received.\nDuration: 3 turns.\n<keyword id=\"Consume_Fire\">Consumes</> <img id=\"Stain_Fire\"/><img id=\"Stain_Fire\"/>: Double thunder strikes.",
                "receive " + Gold(0.3178) + " <keyword id=\"Element_Lightning\">Lightning</> damage per strike at the end of their turn,|receive " + Gold(SkillOverhaulKit.StormCallerPerStrike) + " <keyword id=\"Element_Lightning\">Lightning</> damage per strike at the end of their turn,",
                "receive " + Gold(0.495) + " <keyword id=\"Element_Lightning\">Lightning</> damage per strike at the end of their turn,|receive " + Gold(SkillOverhaulKit.StormCallerPerStrike) + " <keyword id=\"Element_Lightning\">Lightning</> damage per strike at the end of their turn,",
                "and " + Gold(0.495) + " <keyword id=\"Element_Lightning\">Lightning</> damage per strike when they receive damage.|and " + Gold(SkillOverhaulKit.StormCallerPerStrike) + " <keyword id=\"Element_Lightning\">Lightning</> damage per strike when they receive damage.",
                "<img id=\"Stain_Fire\"/><img id=\"Stain_Fire\"/> for double|<img id=\"Stain_Fire\"/> for double",
                "<img id=\"Stain_Fire\"/><img id=\"Stain_Fire\"/>: Double|<img id=\"Stain_Fire\"/>: Double",
            }),
        new("Lune_Terraquake_Description", "low", 1.50, null,
            new[]
            {
                "damage and <keyword id=\"BreakDamage\">Break damage</> to all enemies every turn.|damage and <keyword id=\"DamageMultiplier\">1.95x</> <keyword id=\"BreakDamage\">break damage</> to all enemies every turn.",
                "Duration: 3 turns.|Duration: 1 turn.",
                "to increase the Duration to 5 turns.|to increase the Duration to 3 turns.",
                "Duration increased from 3-5 turns.|Duration increased from 1-3 turns.",
            }),
        new("Lune_ThermalTransfer_Description", "low", 0.22, null,
            new[]
            {
                "Ice</> damage. 2 hits.|Ice</> damage per hit. 2 hits.",
            }),
        new("Lune_Thunderfall_Description", "medium", 0.1925, 1.43,
            new[]
            {
                "damage to random enemies. 2-6 hits.|damage per hit to random enemies. 2-6 hits.",
            }),
        new("Lune_Typhoon_Description", "high", 2.50, null,
            new[]
            {
                // Trailing context (newline / " every turn.") keeps the pair
                // non-prefix so re-packs skip it (idempotent).
                "Heals</> allies.\n|Heals</> allies by 10% Health.\n",
                "Heals</> allies every turn.|Heals</> allies by 10% Health every turn.",
                "Duration increased from 3 to 5 turns.|Duration increased from 1 to 3 turns.",
            }),
        new("Lune_Wildfire_Description", "medium", 0.66, 2.0,
            new[] { "Risky medium |Risky " + Gold(0.66) + " " }),
        new("Lune_Tremor_Description", "high", 30.00, null),
        new("Lune_SkyBreak_Description", "extreme", 70.00, null),
        // Healing moves: heal % ranges only (no damage words, no gold multipliers).
        new("Lune_HealingLight_Description", "", 0, null,
            new[]
            {
                "by 30-50% Health|by 10-30% Health",
                "<img id=\"Stain_Earth\"/> <img id=\"Stain_Earth\"/>|<img id=\"Stain_Earth\"/>",
                "<img id=\"Stain_Earth\"/><img id=\"Stain_Earth\"/>|<img id=\"Stain_Earth\"/>",
            }),
        new("Lune_Rebirth_Description", "", 0, null,
            new[] { "with 30-70% Health|with 1-9% Health" }),
        new("Lune_Revitalization_Description", "", 0, null,
            new[]
            {
                "by 40-60% Health|by 10-20% Health",
                "1-3 allies|1-5 allies",
                "Applies <keyword id=\"Buff_Regen\">Regen</>|Applies <keyword id=\"Buff_Regen\">Regen</> for 3 turns",
            }),
    };

    private static string ApplyReplacements(string text, Move m)
    {
        var r = text;
        // Collapse the historical Typhoon runaway ("by 10% Health" appended once per
        // pack by the non-idempotent heal replacement) before applying anything.
        r = System.Text.RegularExpressions.Regex.Replace(r, @"( by 10% Health){2,}", " by 10% Health");
        // Collapse doubled duration clauses from re-packs: append pairs whose
        // clause ends the string (e.g. Revitalization Short) are non-prefix by
        // construction, so a second pack would stack "for 3 turns for 3 turns".
        r = System.Text.RegularExpressions.Regex.Replace(r, @"( for \d+ turns?)( for \d+ turns?)+", "$1");
        foreach (var (find, repl) in m.Replacements())
        {
            if (!r.Contains(find, StringComparison.Ordinal)) continue; // idempotent: nothing to replace
            r = r.Replace(find, repl);
        }
        return PinBaseRate(r, m);
    }

    private static readonly System.Text.RegularExpressions.Regex BaseRateRx =
        new(@"<keyword id=""DamageMultiplier"">[\d.]+x</>", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Convergent repair: older packs wrote TOTAL damage into these per-hit
    /// descs (Genesis 3.56x, Trick 0.65x, Thunderfall 1.16x, ...), and plain
    /// find/replace can never correct a wrong number (the vanilla find is long gone
    /// from the mod copy, so re-packs silently keep the stale text). The first gold
    /// number in each of these entries is always the base rate, so pin it to the
    /// current per-hit value; already-correct entries are untouched. Moves with
    /// Base 0 (Storm Caller, Fire Rage, healing) are skipped.</summary>
    private static string PinBaseRate(string s, Move m)
    {
        if (m.Base <= 0) return s;
        return BaseRateRx.Replace(s, Gold(m.Base), 1);
    }

    /// <summary>Mayhem: consumes up to 4 non-Light stains (Light is never eaten,
    /// PatchExcludeLight), striking once per consumed stain with that stain element.
    /// Per-hit damage is BaseDamage (0.33) times the consumed-count tier
    /// (0.33/0.66/1.32/1.98 for 1-4 stains: 0.11x to 2.61x with no Dark), and every
    /// Dark consumed doubles the whole cast (x2^dark, InjectDarkCastFactor).</summary>
    private static string MayhemLong(string s) => s.Replace(
        "Consumes all Stains<img id=\"Stain_Generic\"/> to deal high elemental damage to the target.",
        "<keyword id=\"Consume_Generic\">Consumes</> up to 4 stains except <img id=\"Stain_Light\"/>: strikes once per consumed stain, each hit matching its element.\nDeals " + Gold(0.11) + " to " + Gold(2.61) + " elemental damage in total across all hits, doubled for each <img id=\"Stain_Dark\"/> consumed.");

    private static string MayhemShort(string s) => s.Replace(
        "<keyword id=\"Consume_Generic\">Consumes</> all Stains<img id=\"Stain_Generic\"/> to deal high elemental damage to the target.",
        "<keyword id=\"Consume_Generic\">Consumes</> up to 4 stains except <img id=\"Stain_Light\"/>: strikes once per consumed stain (" + Gold(0.11) + " to " + Gold(2.61) + " total, doubled for each <img id=\"Stain_Dark\"/> consumed).");

    /// <summary>Editor preview: the modded Long description for one string-table
    /// base key (e.g. "Lune_StormCaller_Description"), computed from the given
    /// vanilla text with the exact pack-time replacements. Returns false when
    /// no kit rewrites that key (caller falls back to the vanilla text).</summary>
    public static bool TryPreviewDescription(string baseKey, string vanillaLong, out string modded)
    {
        foreach (var m in Moves)
        {
            if (!string.Equals(m.Key, baseKey, StringComparison.Ordinal)) continue;
            modded = ApplyReplacements(vanillaLong, m);
            return true;
        }
        if (string.Equals(baseKey, "Lune_Mayhem_Description", StringComparison.Ordinal))
        {
            modded = MayhemLong(vanillaLong);
            return true;
        }
        modded = "";
        return false;
    }

    public static int Apply(string modDir)
    {
        int n = 0;
        var stDest = EnsureModCopy(StTable, modDir);
        if (stDest is null) return 0;

        // Gold multiplier keyword style (no icon): clone Element_Ice (iconless) and recolor.
        var dtDest = EnsureModCopy(KeywordStyleTable, modDir);
        if (dtDest is null) return 0;
        try
        {
            var ed = new DataTableEditor(dtDest);
            ed.CloneRow("Element_Ice", "DamageMultiplier");
            n += ed.Apply(
                $"{{\"DamageMultiplier\":{{\"TextColor.SpecifiedColor.SpecifiedColor\":" +
                $"\"({GoldR}, {GoldG}, {GoldB}, 1)\"}}}}");
            // Purple Blight keyword style (no icon), same recipe. Used by
            // Lune Ultima Noir ("10 Blight on self") and future Blight text.
            ed.CloneRow("Element_Ice", "Debuff_Blight");
            n += ed.Apply(
                "{\"Debuff_Blight\":{\"TextColor.SpecifiedColor.SpecifiedColor\":" +
                "\"(0.55, 0.2, 0.8, 1)\"}}");
            ed.Save(dtDest);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[SkillDescriptions] keyword style: {e.Message}");
            return n;
        }

        var table = DataTableEditor.OpenStringTable(stDest); // pristine copy in the mod dir
        var entries = new Dictionary<string, string>();
        foreach (var m in Moves)
        {
            if (!table.TryGetValue(m.Key + "_Long", out var longV) || !table.TryGetValue(m.Key + "_Short", out var shortV))
                continue;
            entries[m.Key + "_Long"] = ApplyReplacements(longV, m);
            entries[m.Key + "_Short"] = ApplyReplacements(shortV, m);
            n += 2;
        }
        // Mayhem: range = base × stain multiplier for 1..4 stains (0.11x → 2.61x),
        // no dark bonus in the range; the dark line stays separate. Idempotent:
        // the dark line is inserted once and duplicates from earlier runs collapse.
        if (table.TryGetValue("Lune_Mayhem_Description_Long", out var mayL) && table.TryGetValue("Lune_Mayhem_Description_Short", out var mayS))
        {
            entries["Lune_Mayhem_Description_Long"] = MayhemLong(mayL);
            entries["Lune_Mayhem_Description_Short"] = MayhemShort(mayS);
            n += 2;
        }
        DataTableEditor.AddStringTableEntries(stDest, entries);
        return n;
    }

    private static string? EnsureModCopy(string stem, string modDir)
    {
        var existing = Directory.EnumerateFiles(modDir, stem, SearchOption.AllDirectories).FirstOrDefault();
        if (existing is not null) return existing;
        var src = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), stem);
        if (src is null) return null;
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return null;
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }
}

using Newtonsoft.Json.Linq;
using UAssetAPI.PropertyTypes.Objects;

namespace E33ModEditor.Core;

/// <summary>
/// Revives the vanilla cut-content pictos the loot randomizer distributes that
/// ship with their Data Asset and effect Blueprint (so their effect works) but
/// with literal "null" name/description text, an empty hover stat map and an
/// all-zero <c>DT_PictosDefinitions</c> row — so they hover blank and grant no
/// stats. This kit fills only what is missing; the effect is left untouched.
///
/// Cut pictos whose Blueprint does no work are NOT revived — they are parked in
/// <c>DoNotUse</c> + <c>LootCatalog.BrokenItems</c> instead (Critical Cursed
/// Power, CritChanceOnWeak, Auto1Hp, AntiSilence, PostGradient, PerfectHeal,
/// ProtectingFire, PatientFighter, and the mis-wired Defense/Power Breaker Dodge
/// pair, which share one Blueprint).
/// </summary>
public static class CutPictosKit
{
    public sealed record Spec(
        string Id, string DisplayName, string Description, int LuminaCost,
        double Hp, double Def, double Spd, double Crit, double Chroma,
        int ItemHp, int ItemDef, int ItemSpd, int ItemCrit);

    /// <summary>
    /// Stat spreads follow the vanilla two-of-HP/Def/Spd/Crit convention
    /// (sum ≈1.95, chroma 1.2–1.5). Effects verified from the shipped BPs:
    /// Anti Dizzy/Curse/Exhaust = PreventBuff; ReviveWith* = on revive apply a
    /// status to all allies; *Dodge = on dodge apply/gain a status;
    /// CriticalHavoc = on enemy killed the next hit is a Critical Hit;
    /// EmpoweringDeath = on death apply Powerful to the party.
    /// Any spread that grants Defence is automatically banned from Dark Shores /
    /// Frozen Hearts chests (RandomizerKit derives that set from the packed table).
    /// </summary>
    public static readonly Spec[] Specs =
    {
        new("AntiDizzy", "Anti Dizzy", "Prevents Dizzy.", 5,
            1.00, 0.95, 0, 0, 1.50, 120, 40, 0, 0),
        new("CriticalHavoc", "Critical Havoc", "Defeating an enemy makes your next hit a Critical Hit.", 5,
            0, 0, 1.00, 0.95, 1.20, 0, 0, 48, 16),
        // Prevent-buff family (config-driven BP subclasses).
        new("AntiCurse", "Anti Curse", "Prevents Curse.", 5,
            1.00, 0.95, 0, 0, 1.20, 120, 40, 0, 0),
        new("AntiExhaust", "Anti Exhaust", "Prevents Exhaust.", 5,
            1.00, 0.95, 0, 0, 1.20, 120, 40, 0, 0),
        // Revive-with-status family (BP_LU_ReviveWithStatusOnAllie subclasses).
        new("ReviveWithShell", "Revive with Shell", "Apply Shell for 3 turns when revived.", 5,
            1.00, 0.95, 0, 0, 1.20, 120, 40, 0, 0),
        new("ReviveWithRush", "Revive with Rush", "Apply Rush for 3 turns when revived.", 5,
            1.00, 0, 0.95, 0, 1.20, 120, 0, 48, 0),
        // On-dodge status family (BP_LU_DodgeCanInflictStatus subclasses).
        // Defense Breaker / Power Breaker Dodge are NOT revived: both point at the
        // same BP_LU_DodgeDefensless, so "Power Breaker" applies Defenceless too
        // instead of lowering Power — parked in DoNotUse + BrokenItems.
        new("DefenseRiserDodge", "Defense Riser Dodge", "On dodge, gain Shell.", 5,
            0.95, 1.00, 0, 0, 1.50, 110, 48, 0, 0),
        new("PowerRiserDodge", "Power Riser Dodge", "On dodge, gain Powerful.", 5,
            0, 0, 1.00, 0.95, 1.20, 0, 0, 51, 16),
        // On-death buff to the whole party (BP_LU_EmpoweringDeath).
        new("EmpoweringDeath", "Empowering Death", "When you die, apply Powerful to all allies.", 5,
            0.95, 0, 0, 1.00, 1.50, 110, 0, 0, 16),
    };

    private const string StatHp = "E_jRPG_StatType::NewEnumerator1";
    private const string StatDef = "E_jRPG_StatType::NewEnumerator4";
    private const string StatCrit = "E_jRPG_StatType::NewEnumerator6";
    private const string StatSpd = "E_jRPG_StatType::NewEnumerator7";

    public static int Apply(string modDir)
    {
        PaintItemRows(modDir);
        PaintPassiveRows(modDir);
        PaintDefinitions(modDir);
        PaintStrings(modDir);
        Console.WriteLine($"[cut-pictos] revived {Specs.Length} cut picto(s)");
        return Specs.Length;
    }

    private static string Key(Spec s, string suffix) => $"PASSIVE_{s.Id}_{suffix}";

    private static void PaintItemRows(string modDir)
    {
        foreach (var table in new[] { "DT_jRPG_Items", "DT_jRPG_Items_Composite" })
        {
            var path = PictosCreateKit.FindAsset(modDir, table + ".uasset");
            if (path is null) continue;
            var ed = new DataTableEditor(path);
            var stats = new JObject();
            foreach (var s in Specs)
            {
                if (!ed.TryGetRow(s.Id, out var row)) continue;
                PictosCreateKit.SetRowText(ed, row, "Item_DisplayName", Key(s, "Name"), s.DisplayName);
                PictosCreateKit.SetRowText(ed, row, "ItemDescription", Key(s, "Description"), s.Description);
                var o = new JObject();
                if (s.ItemHp > 0) o[StatHp] = s.ItemHp;
                if (s.ItemDef > 0) o[StatDef] = s.ItemDef;
                if (s.ItemSpd > 0) o[StatSpd] = s.ItemSpd;
                if (s.ItemCrit > 0) o[StatCrit] = s.ItemCrit;
                stats[s.Id] = new JObject { ["Pictos_ItemStats"] = o };
            }
            ed.Apply(stats.ToString());
            ed.Save(path);
        }
    }

    private static void PaintPassiveRows(string modDir)
    {
        var path = PictosCreateKit.FindAsset(modDir, "DT_PassiveEffects.uasset")
            ?? throw new FileNotFoundException("DT_PassiveEffects");
        var ed = new DataTableEditor(path);
        foreach (var s in Specs)
        {
            if (!ed.TryGetRow(s.Id, out var row)) continue;
            PictosCreateKit.SetRowText(ed, row, "Name", Key(s, "Name"), s.DisplayName);
            PictosCreateKit.SetRowText(ed, row, "Description", Key(s, "Description"), s.Description);
            if (s.LuminaCost > 0)
                foreach (var f in row.Value)
                    if (DataTableEditor.CleanName(f.Name) == "LuminaCost" && f is IntPropertyData c)
                        c.Value = s.LuminaCost;
        }
        ed.Save(path);
    }

    private static void PaintDefinitions(string modDir)
    {
        var path = PictosCreateKit.FindAsset(modDir, "DT_PictosDefinitions.uasset")
            ?? throw new FileNotFoundException("DT_PictosDefinitions");
        var ed = new DataTableEditor(path);
        var edits = new JObject();
        foreach (var s in Specs)
        {
            var o = new JObject();
            if (s.Hp > 0) o[StatHp] = s.Hp;
            if (s.Def > 0) o[StatDef] = s.Def;
            if (s.Spd > 0) o[StatSpd] = s.Spd;
            if (s.Crit > 0) o[StatCrit] = s.Crit;
            if (o.Count == 0) throw new InvalidDataException($"{s.Id} has no stat multipliers");
            edits[s.Id] = new JObject
            {
                ["BaseStatMultipliers"] = o,
                ["ChromaValueMultiplier"] = s.Chroma,
            };
        }
        ed.Apply(edits.ToString());
        ed.Save(path);
    }

    private static void PaintStrings(string modDir)
    {
        var st = PictosCreateKit.FindAsset(modDir, "ST_PassiveEffects.uasset");
        if (st is null) return;
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var s in Specs)
        {
            entries[Key(s, "Name")] = s.DisplayName;
            entries[Key(s, "Description")] = s.Description;
        }
        DataTableEditor.AddStringTableEntries(st, entries);
    }
}

using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Weapon over-levelling (levels 1–<see cref="MaxLevel"/>) with the catalyst
/// bands and effect unlocks reworked:
///
/// <list type="bullet">
/// <item><c>DT_WeaponScalingTable</c> — vanilla 1–10 stretched over 1–33, 11–19 over 34–66,
/// 20–32 over 67–111, then compounding +1%/level 112–300 (tapered ~1.2%→0.9% across
/// 112–222) and +2%/level 301–333 (see <see cref="WeaponCurveKit"/>).</item>
/// <item><c>DT_EquipmentScalingTable_Weapons</c> — the same stretch/compounding on its own
/// vanilla base so the two tables stay in lockstep (read by
/// BP_ItemInstance_Gear_Weapon.RefreshCurrentGearStats).</item>
/// <item><c>DT_Equipment_ItemQuantityPerWeaponUpgrade</c> — one catalyst per level, tiered by
/// band: T1 1–33, T2 34–66, T3 67–111, T4 112–222, T5 223–333.</item>
/// <item>Cap constants <c>33 → 333</c> in FL_GameConstants.GetGearMaxUpgradeLevel,
/// BP_ItemUpgradeSystem.GetItemInstanceUpgradeCost and WBP_BS_WeaponUpgrade.</item>
/// <item>Weapon effect unlocks: retime FL_ItemUpgradeHelpers.GetWeaponQualityForUpgradeLevel to
/// Q0 ≤32 / Q1 33–65 / Q2 66–110 / Q3 111–221 / Q4 222–332 / Q5 333+ so the five
/// passive slots unlock at the tier ends 33/66/111/222/333. The shared quality
/// value also drives the attribute upgrades, so GetAttributeUpgradeRanks is
/// retimed {2,4,5} → {1,3,4} to keep those at 33/111/222.</item>
/// </list>
/// </summary>
public static class WeaponOverLevelKit
{
    public const int MaxLevel = 333;
    public const int VanillaMaxLevel = 33;

    public const string EqTable = "DT_EquipmentScalingTable_Weapons";
    public const string EqOverlay = "weapon_equipment_scaling.json";
    public const string UpgradeTable = "DT_Equipment_ItemQuantityPerWeaponUpgrade";
    public const string UpgradeOverlay = "weapon_upgrade_costs.json";
    public const string EqAttackKey = "E_jRPG_StatType::NewEnumerator3";

    /// <summary>
    /// Old → new upper bounds in GetWeaponQualityForUpgradeLevel. The bands become
    /// Q0 ≤32 / Q1 33–65 / Q2 66–110 / Q3 111–221 / Q4 222–332 / Q5 333+, so the
    /// five passive slots (mapped quality 1–5) unlock at 33/66/111/222/333.
    /// </summary>
    private static readonly Dictionary<int, int> QualityRetime = new()
    {
        [3] = 65, [9] = 110, [19] = 221, [32] = 332,
    };

    /// <summary>The <c>&gt;=</c> floor in GetWeaponQualityForUpgradeLevel (0 → 33).</summary>
    private const int QualityFloor = 33;

    public static int Apply(string modDir)
    {
        int n = 0;
        n += ApplyEquipmentScaling(modDir);
        n += ApplyUpgradeCosts(modDir);
        n += PatchCaps(modDir);
        n += PatchQualityRetime(modDir);
        n += PatchAttributeUpgradeRanks(modDir);
        n += PatchWeaponTooltip(modDir);
        n += UpdateCatalystDescriptions(modDir);
        return n;
    }

    /// <summary>Updates the five catalyst item descriptions to the new level bands.</summary>
    private static int UpdateCatalystDescriptions(string modDir)
    {
        if (FileCopy.FindTablesAsset("ST_Items.uasset") is null) return 0;
        var dest = CopyIntoMod("ST_Items", modDir);
        var entries = new Dictionary<string, string>
        {
            ["ITEM_UpgradeMaterial_Level1_Description"] = "Can be used at Camp to upgrade weapons up to level 33.",
            ["ITEM_UpgradeMaterial_Level2_Description"] = "Can be used at Camp to upgrade weapons up to level 66.",
            ["ITEM_UpgradeMaterial_Level3_Description"] = "Can be used at Camp to upgrade weapons up to level 111.",
            ["ITEM_UpgradeMaterial_Level4_Description"] = "Can be used at Camp to upgrade weapons up to level 222.",
            ["ITEM_UpgradeMaterial_Level5_Description"] = "Can be used at Camp to upgrade weapons up to level 333.",
        };
        DataTableEditor.AddStringTableEntries(dest, entries);
        Console.WriteLine($"[over-level] ST_Items: {entries.Count} catalyst description(s) retimed");
        return entries.Count;
    }

    /// <summary>
    /// WBP_WeaponTooltip: the effect slots showed hardcoded unlock levels (4/10/20)
    /// and the max-level VFX compared against 33. Retimes them to the new
    /// 33/66/111/222/333 ladder and the 333 cap.
    /// </summary>
    private static int PatchWeaponTooltip(string modDir)
    {
        if (FileCopy.FindTablesAsset("WBP_WeaponTooltip.uasset") is null) return 0;
        var dest = CopyIntoMod("WBP_WeaponTooltip", modDir);
        var asset = Open(dest);
        int n = 0;
        // Old display levels per quality (b0..b5) -> new. Quality 1..5 unlock the
        // five slots at the tier ends 33/66/111/222/333.
        int[] from = { 20, 10, 4, 0, 0, 33 };
        int[] to = { 222, 111, 66, 33, 0, 333 };
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            var name = fn.ObjectName.ToString();
            if (name == "LoadWeapon")
            {
                uint off = 0;
                foreach (var stmt in fn.ScriptBytecode)
                    stmt.Visit(asset, ref off, (expr, _) =>
                    {
                        if (expr is EX_IntConst ic && ic.Value == VanillaMaxLevel) { ic.Value = MaxLevel; n++; }
                    });
            }
            else if (name == "LoadWeaponLuminas")
            {
                var consts = new List<EX_IntConst>();
                uint off = 0;
                foreach (var stmt in fn.ScriptBytecode)
                    stmt.Visit(asset, ref off, (expr, _) => { if (expr is EX_IntConst ic) consts.Add(ic); });
                for (int i = 0; i + from.Length <= consts.Count; i++)
                {
                    bool match = true;
                    for (int j = 0; j < from.Length; j++)
                        if (consts[i + j].Value != from[j]) { match = false; break; }
                    if (!match) continue;
                    for (int j = 0; j < from.Length; j++) consts[i + j].Value = to[j];
                    n += from.Length;
                    break;
                }
            }
        }
        if (n > 0) asset.Write(dest);
        Console.WriteLine($"[over-level] WBP_WeaponTooltip: {n} value(s) retimed (effect levels 33/66/111/222/333)");
        return n > 0 ? 1 : 0;
    }

    private static string CopyIntoMod(string stem, string modDir)
    {
        var src = FileCopy.FindTablesAsset(stem + ".uasset")
            ?? throw new FileNotFoundException($"work/tables extraction missing for {stem}");
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        var rel = idx >= 0 ? src[idx..] : Path.Combine("Sandfall", "Content", stem + ".uasset");
        var dest = Path.Combine(modDir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    private static int ApplyEquipmentScaling(string modDir)
    {
        var overlay = Path.Combine(EditorPaths.RepoRoot, "work", EqOverlay);
        if (!File.Exists(overlay)) return 0;
        var o = JObject.Parse(File.ReadAllText(overlay));
        var attack = o["Attack"] as JArray;
        var chroma = o["ChromaValue"] as JArray;
        int levels = Math.Max(attack?.Count ?? 0, chroma?.Count ?? 0);
        if (levels == 0) return 0;

        var dest = CopyIntoMod(EqTable, modDir);
        var ed = new DataTableEditor(dest);
        for (int lvl = VanillaMaxLevel + 1; lvl < levels; lvl++)
            if (!ed.HasRow($"Level_{lvl}"))
                ed.CloneRow($"Level_{VanillaMaxLevel}", $"Level_{lvl}", numericName: true);

        var edits = new JObject();
        for (int lvl = 0; lvl < levels; lvl++)
        {
            if (edits[$"Level_{lvl}"] is not JObject row)
                edits[$"Level_{lvl}"] = row = new JObject();
            row["Level"] = lvl;
            if (attack is { } a && lvl < a.Count) row[$"Stats.{EqAttackKey}"] = a[lvl];
            if (chroma is { } c && lvl < c.Count) row["ChromaValue"] = c[lvl];
        }
        var applied = ed.Apply(edits.ToString());
        ed.Save(dest);
        Console.WriteLine($"[over-level] {EqTable}: {levels} level(s) written ({applied} values) from work/{EqOverlay}");
        return applied;
    }

    private static int ApplyUpgradeCosts(string modDir)
    {
        var overlay = Path.Combine(EditorPaths.RepoRoot, "work", UpgradeOverlay);
        if (!File.Exists(overlay)) return 0;
        var costs = JObject.Parse(File.ReadAllText(overlay));
        if (costs.Count == 0) return 0;

        var dest = CopyIntoMod(UpgradeTable, modDir);
        var ed = new DataTableEditor(dest);
        string template = VanillaMaxLevel.ToString();
        if (!ed.HasRow(template)) return 0;

        var edits = new JObject();
        foreach (var (lvl, spec) in costs)
        {
            if (!int.TryParse(lvl, out var level) || spec is not JObject s) continue;
            if (!ed.HasRow(lvl)) ed.CloneRow(template, lvl);
            var row = new JObject { ["WeaponLevel"] = level };
            if (s["Item"] is { } item) row["ItemQuantity.ItemRow.RowName"] = item;
            if (s["Qty"] is { } qty) row["ItemQuantity.ItemQuantity"] = qty;
            edits[lvl] = row;
        }
        var applied = ed.Apply(edits.ToString());
        ed.Save(dest);
        Console.WriteLine($"[over-level] {UpgradeTable}: {costs.Count} row(s) from work/{UpgradeOverlay}");
        return applied;
    }

    private static int PatchCaps(string modDir)
    {
        int n = 0;
        n += PatchCap(modDir, "FL_GameConstants", "GetGearMaxUpgradeLevel");
        n += PatchCap(modDir, "BP_ItemUpgradeSystem", "GetItemInstanceUpgradeCost");
        n += PatchCap(modDir, "WBP_BS_WeaponUpgrade", null);
        return n;
    }

    /// <summary>Rewrites every <c>EX_IntConst 33</c> in the named function (or all functions) to the new cap.</summary>
    private static int PatchCap(string modDir, string stem, string? function)
    {
        var dest = CopyIntoMod(stem, modDir);
        var asset = Open(dest);
        int hits = 0;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            if (function is not null && fn.ObjectName.ToString() != function) continue;
            uint off = 0;
            foreach (var stmt in fn.ScriptBytecode)
                stmt.Visit(asset, ref off, (expr, _) =>
                {
                    if (expr is EX_IntConst ic && ic.Value == VanillaMaxLevel) { ic.Value = MaxLevel; hits++; }
                });
        }
        if (hits > 0) asset.Write(dest);
        Console.WriteLine($"[over-level] {stem}{(function is null ? "" : "." + function)}: {hits} cap constant(s) -> {MaxLevel}");
        return hits > 0 ? 1 : 0;
    }

    /// <summary>Retimes GetWeaponQualityForUpgradeLevel so effect slots unlock at levels 33/66/111/222/333.</summary>
    private static int PatchQualityRetime(string modDir)
    {
        const string stem = "FL_ItemUpgradeHelpers";
        const string fn = "GetWeaponQualityForUpgradeLevel";
        var dest = CopyIntoMod(stem, modDir);
        var asset = Open(dest);
        var function = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == fn);
        if (function?.ScriptBytecode is null) return 0;

        int hits = 0;
        bool floorDone = false;
        uint off = 0;
        foreach (var stmt in function.ScriptBytecode)
            stmt.Visit(asset, ref off, (expr, _) =>
            {
                if (expr is not EX_IntConst ic) return;
                // The `>=` floor: levels below it fall through to quality 0.
                if (ic.Value == 0 && !floorDone) { ic.Value = QualityFloor; floorDone = true; hits++; }
                else if (QualityRetime.TryGetValue(ic.Value, out var to)) { ic.Value = to; hits++; }
            });
        if (hits > 0) asset.Write(dest);
        Console.WriteLine($"[over-level] {stem}.{fn}: {hits} quality value(s) retimed (effect unlocks at 33/66/111/222/333)");
        return hits > 0 ? 1 : 0;
    }

    /// <summary>
    /// The attribute-upgrade unlocks read the same quality value, so shifting the
    /// bands one tier up would move them from 33/111/223 to 66/222/333. Retime
    /// GetAttributeUpgradeRanks from {2,4,5} to {1,3,4} to keep them at 33/111/222.
    /// </summary>
    private static int PatchAttributeUpgradeRanks(string modDir)
    {
        const string stem = "BP_ItemInstance_Gear_Weapon";
        const string fn = "GetAttributeUpgradeRanks";
        if (FileCopy.FindTablesAsset(stem + ".uasset") is null) return 0;
        var dest = CopyIntoMod(stem, modDir);
        var asset = Open(dest);
        var function = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == fn);
        if (function?.ScriptBytecode is null) return 0;

        byte[] ranks = { 1, 3, 4 };
        int hits = 0;
        foreach (var stmt in function.ScriptBytecode)
        {
            if (stmt is not EX_SetArray setArray) continue;
            var bytes = setArray.Elements.OfType<EX_ByteConst>().ToList();
            if (bytes.Count != ranks.Length) continue;
            for (int i = 0; i < ranks.Length; i++) { bytes[i].Value = ranks[i]; hits++; }
            break;
        }
        if (hits > 0) asset.Write(dest);
        Console.WriteLine($"[over-level] {stem}.{fn}: {hits} attribute rank(s) retimed (33/111/222)");
        return hits > 0 ? 1 : 0;
    }

    private static UAsset Open(string path)
    {
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
    }
}

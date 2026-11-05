using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Standalone "Weapon Over-leveling" mod: vanilla plus weapons level 34–333,
/// plus a matching pictos over-level curve so loot past 33 stays on-stats.
///
/// The only gameplay changes are compounding attack per level past 33 on weapons
/// (+3%/level), compounding pictos stats per level past 33 (+1%/level to 300,
/// +2%/level after), with every weapon over-level costing one Perfect Chroma
/// Catalyst. It overrides as few assets as possible (eight files):
/// <list type="bullet">
/// <item><c>DT_WeaponScalingTable</c> — battle/upgrade curve (BP_ItemUpgradeSystem.ComputeWeaponInstanceData).</item>
/// <item><c>DT_EquipmentScalingTable_Weapons</c> — the weapon instance's runtime scaling.</item>
/// <item><c>DT_EquipmentScalingTable_Pictos</c> — the pictos instance's runtime scaling
/// (BP_ItemInstance_Gear.RefreshCurrentGearStats via the GetScalingDataTable override).
/// Without this, pictos loot past 33 (unclamped once GetGearMaxUpgradeLevel is lifted)
/// hits missing scaling rows and falls back to 0.</item>
/// <item><c>DT_Equipment_ItemQuantityPerWeaponUpgrade</c> — one Perfect Chroma Catalyst per over-level.</item>
/// <item><c>FL_GameConstants</c> — GetGearMaxUpgradeLevel 33 → 333.</item>
/// <item><c>BP_ItemUpgradeSystem</c> — GetItemInstanceUpgradeCost maxed check 33 → 333.</item>
/// <item><c>WBP_BS_WeaponUpgrade</c> — the Camp weapon-upgrade screen's own hard-coded
/// 33 cap. Without this the UI refuses to upgrade past 33 even with the
/// constants above lifted — this is why the mod looked half-working.</item>
/// <item><c>ST_Items</c> — the Perfect Chroma Catalyst description.</item>
/// </list>
/// Vanilla rows 0–33 are untouched; nothing else in the game is modified.
/// </summary>
public static class WeaponOverlevelingKit
{
    public const string ModName = "WeaponOverleveling_P";
    public const string Version = "1.1";
    public const int VanillaMaxLevel = 33;
    public const int MaxLevel = 333;
    public const double PerLevel = 1.03;
    public const string PerfectCatalyst = "UpgradeMaterial_Level5";
    public const string EqAttackKey = "E_jRPG_StatType::NewEnumerator3";

    /// <summary>Pictos over-level curve: +1%/level to <see cref="PictoKnee"/>, +2%/level after.</summary>
    public const string PictoTable = "DT_EquipmentScalingTable_Pictos";
    public const int PictoKnee = 300;
    public const double PictoRate1 = 1.01;
    public const double PictoRate2 = 1.02;
    public const string PictoHpKey = "E_jRPG_StatType::NewEnumerator1";
    public const string PictoDefKey = "E_jRPG_StatType::NewEnumerator4";
    public const string PictoCritKey = "E_jRPG_StatType::NewEnumerator6";
    public const string PictoSpdKey = "E_jRPG_StatType::NewEnumerator7";

    public static string VanillaDir =>
        Path.Combine(EditorPaths.RepoRoot, "work", "standalone", "weapon_overlevel", "vanilla");
    public static string StageDir =>
        Path.Combine(EditorPaths.RepoRoot, "work", "standalone", "weapon_overlevel", "mod");

    /// <summary>Builds the standalone mod's asset tree and returns the file count.</summary>
    public static int Apply(string modDir)
    {
        if (!Directory.Exists(VanillaDir))
            throw new DirectoryNotFoundException(
                $"Vanilla sources missing at {VanillaDir}. Re-extract them (game mods uninstalled).");

        int n = 0;
        n += ExtendWeaponCurve(modDir);
        n += ExtendEquipmentCurve(modDir);
        n += ExtendPictoCurve(modDir);
        n += ExtendUpgradeCosts(modDir);
        n += PatchCap(modDir, "FL_GameConstants", "GetGearMaxUpgradeLevel");
        n += PatchCap(modDir, "BP_ItemUpgradeSystem", "GetItemInstanceUpgradeCost");
        // The Camp upgrade screen hard-codes the 33 cap in its own widget; every
        // other over-levelling cap is cosmetic without this one.
        n += PatchCap(modDir, "WBP_BS_WeaponUpgrade", null);
        n += UpdateCatalystDescription(modDir);
        return n;
    }

    /// <summary>
    /// Re-opens the staged assets and asserts the over-level rows and caps are
    /// all present and correct. Throws with a precise reason on any gap so a
    /// half-working pack can never be installed silently.
    /// </summary>
    public static void Verify(string modDir)
    {
        // ── DT_WeaponScalingTable: rows 34..333 compound +3%/level ──
        var curve = Dump(modDir, "DT_WeaponScalingTable");
        double at33 = Num(curve, "Level_33", "PhysicalAttack");
        double mag33 = Num(curve, "Level_33", "MagicalAttack");
        double chroma33 = Num(curve, "Level_33", "ChromaValue");
        if (curve["Level_34"] is null)
            throw new InvalidDataException("DT_WeaponScalingTable: Level_34 missing");
        foreach (var lvl in new[] { 34, 100, 234, 333 })
        {
            CheckRow(curve, lvl, at33, mag33, chroma33);
        }

        // ── DT_EquipmentScalingTable_Weapons: same, on its own base ──
        var eq = Dump(modDir, "DT_EquipmentScalingTable_Weapons");
        double eqAt33 = Num(eq, "Level_33", "Stats", EqAttackKey);
        if (eqAt33 <= 0)
            throw new InvalidDataException("DT_EquipmentScalingTable_Weapons: Level_33 attack base is 0");
        double eqChroma33 = Num(eq, "Level_33", "ChromaValue");
        foreach (var lvl in new[] { 34, 100, 333 })
        {
            var row = eq[$"Level_{lvl}"] as JObject
                ?? throw new InvalidDataException($"DT_EquipmentScalingTable_Weapons: Level_{lvl} missing");
            double wantAtk = Round3(eqAt33 * Math.Pow(PerLevel, lvl - VanillaMaxLevel));
            double gotAtk = row["Stats"]?[EqAttackKey]?.ToObject<double>() ?? double.NaN;
            if (Math.Abs(gotAtk - wantAtk) > 0.01)
                throw new InvalidDataException($"equipment curve Level_{lvl} attack {gotAtk} != {wantAtk}");
            double wantChroma = Math.Round(eqChroma33 * Math.Pow(PerLevel, lvl - VanillaMaxLevel));
            double gotChroma = row["ChromaValue"]?.ToObject<double>() ?? double.NaN;
            if (Math.Abs(gotChroma - wantChroma) > 1)
                throw new InvalidDataException($"equipment curve Level_{lvl} chroma {gotChroma} != {wantChroma}");
        }

        // ── DT_EquipmentScalingTable_Pictos: +1%/level to 300, +2%/level after ──
        var pic = Dump(modDir, PictoTable);
        double picHp = Num(pic, "Level_33", "Stats", PictoHpKey);
        double picDef = Num(pic, "Level_33", "Stats", PictoDefKey);
        double picCrit = Num(pic, "Level_33", "Stats", PictoCritKey);
        double picSpd = Num(pic, "Level_33", "Stats", PictoSpdKey);
        double picChroma = Num(pic, "Level_33", "ChromaValue");
        if (pic["Level_34"] is null)
            throw new InvalidDataException($"{PictoTable}: Level_34 missing");
        foreach (var lvl in new[] { 34, 100, 300, 301, 333 })
        {
            CheckPictoRow(pic, lvl, picHp, picDef, picCrit, picSpd, picChroma);
        }

        // ── DT_Equipment_ItemQuantityPerWeaponUpgrade: one Perfect Catalyst per level ──
        var costs = Dump(modDir, "DT_Equipment_ItemQuantityPerWeaponUpgrade");
        foreach (var lvl in new[] { 34, 145, 333 })
        {
            var row = costs[lvl.ToString()] as JObject
                ?? throw new InvalidDataException($"upgrade cost row {lvl} missing");
            var item = row["ItemQuantity"]?["ItemRow"]?["RowName"]?.ToString();
            var qty = row["ItemQuantity"]?["ItemQuantity"]?.ToObject<int>() ?? 0;
            if (item != PerfectCatalyst || qty != 1)
                throw new InvalidDataException($"upgrade cost {lvl}: {item} x{qty}, want {PerfectCatalyst} x1");
        }
        if (costs["333"]?["WeaponLevel"]?.ToObject<int>() != MaxLevel)
            throw new InvalidDataException("upgrade cost row 333 has the wrong WeaponLevel");

        // ── Cap constants: the named functions, and every WBP function ──
        AssertCapped(modDir, "FL_GameConstants", "GetGearMaxUpgradeLevel");
        AssertCapped(modDir, "BP_ItemUpgradeSystem", "GetItemInstanceUpgradeCost");
        AssertCapped(modDir, "WBP_BS_WeaponUpgrade", null);

        Console.WriteLine("[weapon-overlevel] Verify() passed: rows 34-333 + all caps correct");
    }

    private static void CheckRow(JObject curve, int lvl, double at33, double mag33, double chroma33)
    {
        var row = curve[$"Level_{lvl}"] as JObject
            ?? throw new InvalidDataException($"DT_WeaponScalingTable: Level_{lvl} missing");
        if (row["Level"]?.ToObject<int>() != lvl)
            throw new InvalidDataException($"Level_{lvl}.Level is not {lvl}");
        double p = row["PhysicalAttack"]?.ToObject<double>() ?? double.NaN;
        double m = row["MagicalAttack"]?.ToObject<double>() ?? double.NaN;
        double c = row["ChromaValue"]?.ToObject<double>() ?? double.NaN;
        double wp = Round3(at33 * Math.Pow(PerLevel, lvl - VanillaMaxLevel));
        double wm = Round3(mag33 * Math.Pow(PerLevel, lvl - VanillaMaxLevel));
        double wc = Math.Round(chroma33 * Math.Pow(PerLevel, lvl - VanillaMaxLevel));
        if (Math.Abs(p - wp) > 0.01 || Math.Abs(m - wm) > 0.01)
            throw new InvalidDataException($"curve Level_{lvl} attack {p}/{m} != {wp}/{wm}");
        if (Math.Abs(c - wc) > 1)
            throw new InvalidDataException($"curve Level_{lvl} chroma {c} != {wc}");
    }

    private static void CheckPictoRow(JObject table, int lvl, double hp33, double def33, double crit33, double spd33, double chroma33)
    {
        var row = table[$"Level_{lvl}"] as JObject
            ?? throw new InvalidDataException($"{PictoTable}: Level_{lvl} missing");
        if (row["Level"]?.ToObject<int>() != lvl)
            throw new InvalidDataException($"{PictoTable} Level_{lvl}.Level is not {lvl}");
        double hp = row["Stats"]?[PictoHpKey]?.ToObject<double>() ?? double.NaN;
        double def = row["Stats"]?[PictoDefKey]?.ToObject<double>() ?? double.NaN;
        double crit = row["Stats"]?[PictoCritKey]?.ToObject<double>() ?? double.NaN;
        double spd = row["Stats"]?[PictoSpdKey]?.ToObject<double>() ?? double.NaN;
        double chroma = row["ChromaValue"]?.ToObject<double>() ?? double.NaN;
        if (Math.Abs(hp - Round3(OverValuePicto(hp33, lvl))) > 0.01
            || Math.Abs(def - Round3(OverValuePicto(def33, lvl))) > 0.01
            || Math.Abs(crit - Round3(OverValuePicto(crit33, lvl))) > 0.01
            || Math.Abs(spd - Round3(OverValuePicto(spd33, lvl))) > 0.01)
            throw new InvalidDataException($"{PictoTable} Level_{lvl} stats mismatch");
        if (Math.Abs(chroma - Math.Round(OverValuePicto(chroma33, lvl))) > 1)
            throw new InvalidDataException($"{PictoTable} Level_{lvl} chroma {chroma} mismatch");
    }

    /// <summary>Asserts the named function (or every function) has the 333 cap and no stale 33.</summary>
    private static void AssertCapped(string modDir, string stem, string? function)
    {
        var path = FindStaged(modDir, stem);
        var asset = Open(path);
        int caps = 0, stale = 0;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            if (function is not null && fn.ObjectName.ToString() != function) continue;
            uint off = 0;
            foreach (var stmt in fn.ScriptBytecode)
                stmt.Visit(asset, ref off, (expr, _) =>
                {
                    if (expr is not EX_IntConst ic) return;
                    if (ic.Value == MaxLevel) caps++;
                    else if (ic.Value == VanillaMaxLevel) stale++;
                });
        }
        if (caps == 0)
            throw new InvalidDataException($"{stem}{(function is null ? "" : "." + function)}: no {MaxLevel} cap found");
        if (stale > 0)
            throw new InvalidDataException($"{stem}{(function is null ? "" : "." + function)}: {stale} stale {VanillaMaxLevel} constant(s) remain");
    }

    /// <summary>Stages into <see cref="StageDir"/>, verifies, packs and installs, returning the written files.</summary>
    public static IReadOnlyList<string> Build()
    {
        if (Directory.Exists(StageDir)) Directory.Delete(StageDir, true);
        Directory.CreateDirectory(StageDir);
        int n = Apply(StageDir);
        Console.WriteLine($"[weapon-overlevel] v{Version}: {n} edit(s) staged in {StageDir}");
        Verify(StageDir);
        var written = new Retoc().PackCombined(StageDir, ModName);
        foreach (var f in written) Console.WriteLine("[weapon-overlevel] wrote " + f);
        return written;
    }

    private static string CopyVanilla(string modDir, string stem)
    {
        var src = Directory.EnumerateFiles(VanillaDir, stem + ".uasset", SearchOption.AllDirectories).FirstOrDefault();
        if (src is null)
        {
            // Not cached yet: extract the pristine asset (mods moved aside so a
            // third-party pack cannot shadow it) into the vanilla cache.
            Directory.CreateDirectory(VanillaDir);
            using (Packer.DonorExtract.SuspendMods())
                new Retoc().ToLegacy(VanillaDir, stem);
            src = Directory.EnumerateFiles(VanillaDir, stem + ".uasset", SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new FileNotFoundException($"{stem}.uasset not found under {VanillaDir} after extraction");
        }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        var dest = Path.Combine(modDir, idx >= 0 ? src[idx..] : Path.Combine("Sandfall", "Content", stem + ".uasset"));
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    /// <summary>Compounds a vanilla base value by <see cref="PerLevel"/> from level 34 up.</summary>
    private static double OverValue(double at33, int lvl) => at33 * Math.Pow(PerLevel, lvl - VanillaMaxLevel);

    /// <summary>Compounds a vanilla pictos base value by <see cref="PictoRate1"/>
    /// to <see cref="PictoKnee"/>, then <see cref="PictoRate2"/> after.</summary>
    private static double OverValuePicto(double at33, int lvl)
    {
        int lo = Math.Min(lvl, PictoKnee) - VanillaMaxLevel;
        int hi = Math.Max(0, lvl - PictoKnee);
        return at33 * Math.Pow(PictoRate1, lo) * Math.Pow(PictoRate2, hi);
    }

    private static int ExtendWeaponCurve(string modDir)
    {
        var dest = CopyVanilla(modDir, "DT_WeaponScalingTable");
        var ed = new DataTableEditor(dest);
        if (!ed.TryGetRow($"Level_{VanillaMaxLevel}", out var baseRow)) return 0;

        double atk = Float(baseRow, "PhysicalAttack");
        double mag = Float(baseRow, "MagicalAttack");
        double chroma = Float(baseRow, "ChromaValue");

        for (int lvl = VanillaMaxLevel + 1; lvl <= MaxLevel; lvl++)
        {
            if (!ed.HasRow($"Level_{lvl}"))
                ed.CloneRow($"Level_{VanillaMaxLevel}", $"Level_{lvl}", numericName: true);
            var edit = new JObject
            {
                ["Level"] = lvl,
                ["PhysicalAttack"] = Round3(OverValue(atk, lvl)),
                ["MagicalAttack"] = Round3(OverValue(mag, lvl)),
                ["ChromaValue"] = (int)Math.Round(OverValue(chroma, lvl)),
            };
            ed.Apply(new JObject { [$"Level_{lvl}"] = edit }.ToString());
        }
        ed.Save(dest);
        Console.WriteLine($"[weapon-overlevel] DT_WeaponScalingTable: levels 34-{MaxLevel} (+3%/level)");
        return MaxLevel - VanillaMaxLevel;
    }

    private static int ExtendEquipmentCurve(string modDir)
    {
        var dest = CopyVanilla(modDir, "DT_EquipmentScalingTable_Weapons");
        var ed = new DataTableEditor(dest);
        if (!ed.TryGetRow($"Level_{VanillaMaxLevel}", out var baseRow)) return 0;

        double atk = FloatMap(baseRow, "Stats", EqAttackKey);
        double chroma = Float(baseRow, "ChromaValue");

        for (int lvl = VanillaMaxLevel + 1; lvl <= MaxLevel; lvl++)
        {
            if (!ed.HasRow($"Level_{lvl}"))
                ed.CloneRow($"Level_{VanillaMaxLevel}", $"Level_{lvl}", numericName: true);
            var edit = new JObject
            {
                ["Level"] = lvl,
                [$"Stats.{EqAttackKey}"] = Round3(OverValue(atk, lvl)),
                ["ChromaValue"] = (int)Math.Round(OverValue(chroma, lvl)),
            };
            ed.Apply(new JObject { [$"Level_{lvl}"] = edit }.ToString());
        }
        ed.Save(dest);
        Console.WriteLine($"[weapon-overlevel] DT_EquipmentScalingTable_Weapons: levels 34-{MaxLevel} (+3%/level)");
        return MaxLevel - VanillaMaxLevel;
    }

    private static int ExtendPictoCurve(string modDir)
    {
        var dest = CopyVanilla(modDir, PictoTable);
        var ed = new DataTableEditor(dest);
        if (!ed.TryGetRow($"Level_{VanillaMaxLevel}", out var baseRow)) return 0;

        double hp = FloatMap(baseRow, "Stats", PictoHpKey);
        double def = FloatMap(baseRow, "Stats", PictoDefKey);
        double crit = FloatMap(baseRow, "Stats", PictoCritKey);
        double spd = FloatMap(baseRow, "Stats", PictoSpdKey);
        double chroma = Float(baseRow, "ChromaValue");

        for (int lvl = VanillaMaxLevel + 1; lvl <= MaxLevel; lvl++)
        {
            if (!ed.HasRow($"Level_{lvl}"))
                ed.CloneRow($"Level_{VanillaMaxLevel}", $"Level_{lvl}", numericName: true);
            var edit = new JObject
            {
                ["Level"] = lvl,
                [$"Stats.{PictoHpKey}"] = Round3(OverValuePicto(hp, lvl)),
                [$"Stats.{PictoDefKey}"] = Round3(OverValuePicto(def, lvl)),
                [$"Stats.{PictoCritKey}"] = Round3(OverValuePicto(crit, lvl)),
                [$"Stats.{PictoSpdKey}"] = Round3(OverValuePicto(spd, lvl)),
                ["ChromaValue"] = (int)Math.Round(OverValuePicto(chroma, lvl)),
            };
            ed.Apply(new JObject { [$"Level_{lvl}"] = edit }.ToString());
        }
        ed.Save(dest);
        Console.WriteLine($"[weapon-overlevel] {PictoTable}: levels 34-{MaxLevel} (+1%/level to {PictoKnee}, +2%/level after)");
        return MaxLevel - VanillaMaxLevel;
    }

    private static int ExtendUpgradeCosts(string modDir)
    {
        var dest = CopyVanilla(modDir, "DT_Equipment_ItemQuantityPerWeaponUpgrade");
        var ed = new DataTableEditor(dest);
        if (!ed.HasRow(VanillaMaxLevel.ToString())) return 0;

        for (int lvl = VanillaMaxLevel + 1; lvl <= MaxLevel; lvl++)
        {
            string key = lvl.ToString();
            if (!ed.HasRow(key)) ed.CloneRow(VanillaMaxLevel.ToString(), key);
            var edit = new JObject
            {
                ["WeaponLevel"] = lvl,
                ["ItemQuantity.ItemRow.RowName"] = PerfectCatalyst,
                ["ItemQuantity.ItemQuantity"] = 1,
            };
            ed.Apply(new JObject { [key] = edit }.ToString());
        }
        ed.Save(dest);
        Console.WriteLine($"[weapon-overlevel] DT_Equipment_ItemQuantityPerWeaponUpgrade: levels 34-{MaxLevel} use {PerfectCatalyst} x1");
        return MaxLevel - VanillaMaxLevel;
    }

    private static int PatchCap(string modDir, string stem, string? function)
    {
        var dest = CopyVanilla(modDir, stem);
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
        Console.WriteLine($"[weapon-overlevel] {stem}{(function is null ? "" : "." + function)}: {hits} cap constant(s) -> {MaxLevel}");
        return hits > 0 ? 1 : 0;
    }

    private static int UpdateCatalystDescription(string modDir)
    {
        var dest = CopyVanilla(modDir, "ST_Items");
        DataTableEditor.AddStringTableEntries(dest, new Dictionary<string, string>
        {
            ["ITEM_UpgradeMaterial_Level5_Description"] = $"Can be used at Camp to upgrade weapons to level {MaxLevel}.",
        });
        Console.WriteLine("[weapon-overlevel] ST_Items: Perfect Chroma Catalyst description updated");
        return 1;
    }

    private static double Float(UAssetAPI.PropertyTypes.Structs.StructPropertyData row, string field)
    {
        var p = row.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == field);
        return p switch
        {
            UAssetAPI.PropertyTypes.Objects.DoublePropertyData d => d.Value,
            UAssetAPI.PropertyTypes.Objects.FloatPropertyData f => f.Value,
            UAssetAPI.PropertyTypes.Objects.IntPropertyData i => i.Value,
            _ => 0,
        };
    }

    private static double FloatMap(UAssetAPI.PropertyTypes.Structs.StructPropertyData row, string mapField, string key)
    {
        var p = row.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == mapField)
            as UAssetAPI.PropertyTypes.Objects.MapPropertyData;
        if (p is null) return 0;
        foreach (var kv in p.Value)
        {
            if (kv.Key.ToString() == key)
                return kv.Value switch
                {
                    UAssetAPI.PropertyTypes.Objects.DoublePropertyData d => d.Value,
                    UAssetAPI.PropertyTypes.Objects.FloatPropertyData f => f.Value,
                    _ => 0,
                };
        }
        return 0;
    }

    private static double Round3(double v) => Math.Round(v, 3);

    private static string FindStaged(string modDir, string stem) =>
        FileCopy.FindUnder(modDir, stem + ".uasset")
        ?? throw new FileNotFoundException($"staged {stem}.uasset missing in {modDir}");

    private static JObject Dump(string modDir, string stem) =>
        JObject.Parse(new DataTableEditor(FindStaged(modDir, stem)).Dump());

    /// <summary>Reads a numeric field from a dumped row (top-level or nested map key).</summary>
    private static double Num(JObject table, string row, params string[] path)
    {
        JToken? t = table[row];
        if (t is null) throw new InvalidDataException($"{row} missing from table");
        foreach (var key in path)
        {
            if (t is not JObject o) throw new InvalidDataException($"{row}.{string.Join('.', path)} is not an object");
            t = o[key];
            if (t is null) throw new InvalidDataException($"{row}.{string.Join('.', path)} missing");
        }
        return t.ToObject<double>();
    }

    private static UAsset Open(string path)
    {
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
    }
}

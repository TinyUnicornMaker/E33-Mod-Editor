using Newtonsoft.Json.Linq;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;

namespace E33ModEditor.Core;

/// <summary>
/// Core picto over-level scaling. Vanilla rows 0–33 are untouched; rows 34–333
/// continue the shared picture curve at a flat +1% of the level-33 base per
/// level — additive, not compounding. Always on in Expedition Infinity — there
/// is no setting or UI.
///
/// <para>This is separate from <see cref="WeaponDropLevelKit"/>, which keeps
/// picto <em>drop</em> levels at 1–33. This kit only makes the scaling table
/// valid for pictos that are already above 33 (NG+, save edits, or other
/// content). Without it, a picto above 33 hits a missing row and falls back to
/// zero stats.</para>
/// </summary>
public static class PictoScalingKit
{
    public const string Table = "DT_EquipmentScalingTable_Pictos";
    public const int VanillaMaxLevel = 33;
    public const int MaxLevel = 333;

    /// <summary>Fraction of the level-33 base added per level past 33.</summary>
    public const double PerLevel = 0.01;

    public const string HpKey = "E_jRPG_StatType::NewEnumerator1";
    public const string DefKey = "E_jRPG_StatType::NewEnumerator4";
    public const string CritKey = "E_jRPG_StatType::NewEnumerator6";
    public const string SpdKey = "E_jRPG_StatType::NewEnumerator7";

    /// <summary>Linear +1% of the level-33 base per level past 33.
    /// Level 333 is 4× the level-33 value; levels at or below 33 are unchanged.</summary>
    public static double ScaleValue(double at33, int level)
    {
        int steps = Math.Max(0, level - VanillaMaxLevel);
        return at33 * (1.0 + PerLevel * steps);
    }

    public static int Apply(string modDir)
    {
        var dest = CopyIntoMod(modDir);
        var ed = new DataTableEditor(dest);
        if (!ed.TryGetRow($"Level_{VanillaMaxLevel}", out var baseRow))
            throw new InvalidDataException($"{Table}: Level_{VanillaMaxLevel} missing");

        double hp = FloatMap(baseRow, "Stats", HpKey);
        double def = FloatMap(baseRow, "Stats", DefKey);
        double crit = FloatMap(baseRow, "Stats", CritKey);
        double spd = FloatMap(baseRow, "Stats", SpdKey);
        double chroma = Float(baseRow, "ChromaValue");

        var edits = new JObject();
        for (int lvl = VanillaMaxLevel + 1; lvl <= MaxLevel; lvl++)
        {
            if (!ed.HasRow($"Level_{lvl}"))
                ed.CloneRow($"Level_{VanillaMaxLevel}", $"Level_{lvl}", numericName: true);
            edits[$"Level_{lvl}"] = new JObject
            {
                ["Level"] = lvl,
                [$"Stats.{HpKey}"] = Round3(ScaleValue(hp, lvl)),
                [$"Stats.{DefKey}"] = Round3(ScaleValue(def, lvl)),
                [$"Stats.{CritKey}"] = Round3(ScaleValue(crit, lvl)),
                [$"Stats.{SpdKey}"] = Round3(ScaleValue(spd, lvl)),
                ["ChromaValue"] = (int)Math.Round(ScaleValue(chroma, lvl)),
            };
        }

        ed.Apply(edits.ToString());
        ed.Save(dest);
        Verify(modDir);
        Console.WriteLine($"[picto-scaling] {Table}: levels 34-{MaxLevel} at +1%/level linear (core, always on)");
        return MaxLevel - VanillaMaxLevel;
    }

    /// <summary>Re-opens the staged table and checks every row from 34 to 333.</summary>
    public static void Verify(string modDir)
    {
        var path = FileCopy.FindUnder(modDir, Table + ".uasset")
            ?? throw new FileNotFoundException($"staged {Table}.uasset missing in {modDir}");
        var table = JObject.Parse(new DataTableEditor(path).Dump());
        double hp33 = Num(table, "Level_33", "Stats", HpKey);
        double def33 = Num(table, "Level_33", "Stats", DefKey);
        double crit33 = Num(table, "Level_33", "Stats", CritKey);
        double spd33 = Num(table, "Level_33", "Stats", SpdKey);
        double chroma33 = Num(table, "Level_33", "ChromaValue");

        for (int lvl = VanillaMaxLevel + 1; lvl <= MaxLevel; lvl++)
        {
            var row = table[$"Level_{lvl}"] as JObject
                ?? throw new InvalidDataException($"{Table}: Level_{lvl} missing");
            if (row["Level"]?.ToObject<int>() != lvl)
                throw new InvalidDataException($"{Table}: Level_{lvl}.Level is not {lvl}");

            Check(row, lvl, "Stats", HpKey, hp33);
            Check(row, lvl, "Stats", DefKey, def33);
            Check(row, lvl, "Stats", CritKey, crit33);
            Check(row, lvl, "Stats", SpdKey, spd33);
            Check(row, lvl, null, "ChromaValue", chroma33, integer: true);
        }

        Console.WriteLine($"[picto-scaling] Verify passed: rows 34-{MaxLevel} at +1%/level linear");
    }

    private static void Check(JObject row, int level, string? map, string key,
        double at33, bool integer = false)
    {
        double got = map is null
            ? row[key]?.ToObject<double>() ?? double.NaN
            : row[map]?[key]?.ToObject<double>() ?? double.NaN;
        double want = ScaleValue(at33, level);
        double tolerance = integer ? 0.5 : 0.01;
        if (Math.Abs(got - (integer ? Math.Round(want) : Round3(want))) > tolerance)
            throw new InvalidDataException(
                $"{Table}: Level_{level} {key} {got} != {(integer ? Math.Round(want) : Round3(want))}");
    }

    private static string CopyIntoMod(string modDir)
    {
        var src = FileCopy.FindTablesAsset(Table + ".uasset")
            ?? throw new FileNotFoundException($"work/tables extraction missing for {Table}");
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        var rel = idx >= 0
            ? src[idx..]
            : Path.Combine("Sandfall", "Content", "Gameplay", "Pictos", Table + ".uasset");
        var dest = Path.Combine(modDir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    private static double Round3(double v) => Math.Round(v, 3);

    private static double Num(JObject table, string row, params string[] path)
    {
        JToken? t = table[row];
        if (t is null) throw new InvalidDataException($"{row} missing from {Table}");
        foreach (var key in path)
        {
            if (t is not JObject o)
                throw new InvalidDataException($"{row}.{string.Join('.', path)} is not an object");
            t = o[key];
            if (t is null) throw new InvalidDataException($"{row}.{string.Join('.', path)} missing");
        }
        return t.ToObject<double>();
    }

    private static double Float(StructPropertyData row, string field)
    {
        var p = row.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == field);
        return p switch
        {
            DoublePropertyData d => d.Value,
            FloatPropertyData f => f.Value,
            IntPropertyData i => i.Value,
            _ => 0,
        };
    }

    private static double FloatMap(StructPropertyData row, string mapField, string key)
    {
        var p = row.Value.FirstOrDefault(f => DataTableEditor.CleanName(f.Name) == mapField)
            as MapPropertyData;
        if (p is null) return 0;
        foreach (var kv in p.Value)
        {
            if (kv.Key.ToString() == key)
                return kv.Value switch
                {
                    DoublePropertyData d => d.Value,
                    FloatPropertyData f => f.Value,
                    _ => 0,
                };
        }
        return 0;
    }
}

using Newtonsoft.Json.Linq;
using UAssetAPI.PropertyTypes.Objects;

namespace E33ModEditor.Core;

/// <summary>
/// Vanilla balance rework for the Draining Cleanse pictos
/// (<c>DispelOnAPConsume</c>): the AP paid to prevent a Status Effect is raised
/// from 1 to 10, and the lumina price from 15 to 21.
///
/// The AP price is entirely data-driven. <c>BP_LU_DispelOnAPConsume</c> reads
/// <c>DispelOnAPConsume_APConsumed</c> from DT_Balancing_Luminas twice — once to
/// gate ("current AP &gt;= cost") and once to spend (via <c>nReduceAP</c>) — so
/// the bytecode needs no patch; only the balancing float changes. The passive
/// row's <c>LuminaCost</c> and the shared <c>PASSIVE_DispelOnAPConsume_Description</c>
/// string (read by both the passive and the item hover) are written alongside it.
/// </summary>
public static class CleanseTweakKit
{
    public const string DrainingCleanseId = "DispelOnAPConsume";
    public const string ApConsumedKey = "DispelOnAPConsume_APConsumed";
    public const double VanillaApCost = 1.0;
    public const double ApCost = 10.0;
    public const int VanillaLuminaCost = 15;
    public const int LuminaCost = 21;
    public const string Description =
        "Consume 10 AP to prevent Status Effects application, if possible.";

    public static int Apply(string modDir)
    {
        int n = 0;
        n += PatchBalancing(modDir);
        n += PatchPassive(modDir);
        PatchDescription(modDir);
        Console.WriteLine($"[cleanse] Draining Cleanse reworked to {ApCost:0} AP / {LuminaCost} lumina");
        return n;
    }

    /// <summary>Raises the AP the wearer must spend to prevent the Status Effect.</summary>
    private static int PatchBalancing(string modDir)
    {
        var path = EnsureTable(modDir, "DT_Balancing_Luminas");
        var ed = new DataTableEditor(path);
        if (!ed.HasRow(ApConsumedKey)) return 0; // vanilla table lacks the row — leave untouched
        var edits = new JObject { [ApConsumedKey] = new JObject { ["FloatValue"] = ApCost } };
        int n = ed.Apply(edits.ToString());
        ed.Save(path);
        return n;
    }

    /// <summary>Raises the lumina cost shown on the pictos row.</summary>
    private static int PatchPassive(string modDir)
    {
        var path = EnsureTable(modDir, "DT_PassiveEffects");
        var ed = new DataTableEditor(path);
        if (!ed.TryGetRow(DrainingCleanseId, out var row)) return 0;
        int n = 0;
        foreach (var f in row.Value)
        {
            if (DataTableEditor.CleanName(f.Name) != "LuminaCost" || f is not IntPropertyData c) continue;
            if (c.Value == LuminaCost) continue;
            c.Value = LuminaCost;
            n++;
        }
        if (n > 0) ed.Save(path);
        return n;
    }

    /// <summary>Rewrites the shared hover text (passive + item rows both point at this key).</summary>
    private static void PatchDescription(string modDir)
    {
        var path = PictosCreateKit.FindAsset(modDir, "ST_PassiveEffects.uasset");
        if (path is null) return;
        DataTableEditor.AddStringTableEntries(path, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [$"PASSIVE_{DrainingCleanseId}_Description"] = Description,
        });
    }

    /// <summary>Finds the staged table, copying the vanilla extract into the mod tree when absent.</summary>
    private static string EnsureTable(string modDir, string stem)
    {
        var staged = PictosCreateKit.FindAsset(modDir, stem + ".uasset");
        if (staged is not null) return staged;
        var src = FileCopy.FindTablesAsset(stem + ".uasset")
            ?? throw new FileNotFoundException(stem + ".uasset");
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        var dest = idx >= 0 ? Path.Combine(modDir, src[idx..]) : Path.Combine(modDir, stem + ".uasset");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }
}

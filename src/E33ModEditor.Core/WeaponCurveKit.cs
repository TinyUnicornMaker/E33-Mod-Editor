using Newtonsoft.Json.Linq;

namespace E33ModEditor.Core;

/// <summary>
/// Applies the shared weapon level curve (<c>DT_WeaponScalingTable</c>) from the
/// durable <c>work/weapon_curve.json</c> overlay.
///
/// The App's Weapon Editor keeps its curve edits in memory only — they are
/// cleared after a pack and are never consumed by the CLI — so a
/// <c>rebuild-mod</c> would otherwise always ship the vanilla curve. This
/// overlay is the version-controllable source of truth.
///
/// Format: <c>{ "PhysicalAttack": [N], "MagicalAttack": [N], "ChromaValue": [N] }</c>
/// indexed by weapon level 0..N-1. Rows beyond the vanilla 0..33 are cloned from
/// <c>Level_33</c> so the over-level range works too.
/// </summary>
public static class WeaponCurveKit
{
    public const string Table = "DT_WeaponScalingTable";
    public const string OverlayFile = "weapon_curve.json";
    public const int VanillaMaxLevel = 33;

    public static int Apply(string modDir)
    {
        var overlay = Path.Combine(EditorPaths.RepoRoot, "work", OverlayFile);
        if (!File.Exists(overlay)) return 0;

        var src = FileCopy.FindTablesAsset(Table + ".uasset");
        if (src is null)
        {
            Console.WriteLine($"[weapon-curve] source table missing ({Table}); skipped");
            return 0;
        }

        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        var rel = idx >= 0 ? src[idx..] : Path.Combine("Sandfall", "Content", Table + ".uasset");
        var dest = Path.Combine(modDir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);

        var o = JObject.Parse(File.ReadAllText(overlay));
        int levels = new[] { "PhysicalAttack", "MagicalAttack", "ChromaValue" }
            .Select(f => (o[f] as JArray)?.Count ?? 0).Max();
        if (levels == 0) return 0;

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
            foreach (var field in new[] { "PhysicalAttack", "MagicalAttack", "ChromaValue" })
                if ((o[field] as JArray) is { } arr && lvl < arr.Count)
                    row[field] = arr[lvl];
        }

        var applied = ed.Apply(edits.ToString());
        ed.Save(dest);
        Console.WriteLine($"[weapon-curve] {Table}: {levels} level(s) written ({applied} values) from work/{OverlayFile}");
        return applied;
    }
}

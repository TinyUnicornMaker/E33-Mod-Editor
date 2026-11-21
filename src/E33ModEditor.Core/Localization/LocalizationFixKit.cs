using UAssetAPI.PropertyTypes.Objects;

namespace E33ModEditor.Core.Localization;

/// <summary>
/// Fixes vanilla rows that were shipped with culture-invariant English text and
/// therefore could never be translated by a locres. Each row is rewritten to a
/// StringTableEntry pointing at ST_PassiveEffects, and the English source is
/// added to the staged table so the catalogs can carry translations.
/// </summary>
public static class LocalizationFixKit
{
    private sealed record Fix(
        string Row,
        string NameKey, string Name,
        string DescriptionKey, string Description);

    private static readonly Fix[] Fixes =
    {
        new("AutoBurn",  "PASSIVE_AutoBurn_Name",  "Auto Burn",
                         "PASSIVE_AutoBurn_Description",  "Self apply Burn on battle start."),
        new("AutoCurse", "PASSIVE_AutoCurse_Name", "Auto Curse",
                         "PASSIVE_AutoCurse_Description", "Self apply Curse on battle start."),
    };

    public static int Apply(string modDir)
    {
        var stPath = Find(modDir, "ST_PassiveEffects.uasset");
        if (stPath is null) return 0;

        DataTableEditor.AddStringTableEntries(stPath, Fixes
            .SelectMany(f => new[]
            {
                new KeyValuePair<string, string>(f.NameKey, f.Name),
                new KeyValuePair<string, string>(f.DescriptionKey, f.Description),
            })
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal));

        int changed = 0;
        foreach (var table in new[] { "DT_PassiveEffects", "DT_jRPG_Items", "DT_jRPG_Items_Composite" })
        {
            var path = Find(modDir, table + ".uasset");
            if (path is null) continue;
            var ed = new DataTableEditor(path);
            bool items = table.StartsWith("DT_jRPG_Items", StringComparison.Ordinal);
            int tableChanges = 0;
            foreach (var fix in Fixes)
            {
                if (!ed.TryGetRow(fix.Row, out var row)) continue;
                if (items)
                {
                    PictosCreateKit.SetRowText(ed, row, "Item_DisplayName", fix.NameKey, fix.Name);
                    PictosCreateKit.SetRowText(ed, row, "ItemDescription", fix.DescriptionKey, fix.Description);
                }
                else
                {
                    PictosCreateKit.SetRowText(ed, row, "Name", fix.NameKey, fix.Name);
                    PictosCreateKit.SetRowText(ed, row, "Description", fix.DescriptionKey, fix.Description);
                }
                tableChanges++;
            }
            if (tableChanges > 0) { ed.Save(path); changed += tableChanges; }
        }
        return changed;
    }

    private static string? Find(string modDir, string fileName)
    {
        if (!Directory.Exists(modDir)) return null;
        return Directory.EnumerateFiles(modDir, fileName, SearchOption.AllDirectories).FirstOrDefault();
    }
}

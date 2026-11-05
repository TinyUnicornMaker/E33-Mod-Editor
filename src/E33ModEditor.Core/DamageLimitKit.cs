using E33ModEditor.Core.Localization;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Legacy damage-limit tier removed. Now only renames Painted Power (OverPowered) to Ultima Power.
/// Kept as DamageLimitKit for compatibility with existing call sites.
/// </summary>
public static class DamageLimitKit
{
    public const string UltimaId = "UltimaPower";
    public const string UltimaDisplay = "Ultima Power";
    public const string UltimaDesc = "Raises damage limit to 777 (test).";
    private const string PaintedId = "OverPowered";
    // New desired name/desc for Painted Power.
    // The description is intentionally the original flavour text; the functional
    // trigger is documented in the audit and code, but the in-game tooltip keeps
    // the original "Fully unlocks your power." wording.
    public const string RenamedDisplay = "Ultima Power";
    public const string RenamedDesc = "Fully unlocks your power.";

    // Compatibility: kept but no longer indicates tier
    public static bool HasThreeTier(string modDir) => false;

    /// <summary>Legacy entry: now just ensures rename and cleans old tier artifacts.</summary>
    public static int ApplyThreeTier(string modDir)
    {
        int n = 0;
        n += RemoveThreeTier(modDir); // clean old Ultima/BattleDamages if present
        n += RenamePaintedPower(modDir);
        return n;
    }

    public static int RemoveThreeTier(string modDir)
    {
        int n = 0;
        // Remove tier artifacts if they exist from previous builds
        foreach (var f in new[] { "BP_LU_UltimaPower.uasset", "DA_GearPictos_UltimaPower.uasset",
            "BP_BattleDamages.uasset", "AC_jRPG_CharacterBattleStats.uasset" })
        {
            var p = FindAsset(modDir, f);
            if (p != null) { try { File.Delete(p); var e = Path.ChangeExtension(p, ".uexp"); if (File.Exists(e)) File.Delete(e); n++; } catch {} }
        }
        // Remove Ultima rows from mod DTs if present (delete file to force recopy from vanilla on next inject)
        foreach (var table in new[] { "DT_jRPG_Items", "DT_jRPG_Items_Composite", "DT_PassiveEffects", "DT_PictosDefinitions" })
        {
            var p = FindAsset(modDir, table + ".uasset");
            if (p != null)
            {
                try
                {
                    var ed = new DataTableEditor(p);
                    if (ed.TryGetRow(UltimaId, out _))
                    {
                        // Remove row and save, or delete file to fallback to vanilla
                        ed.Table.Table.Data.RemoveAll(r => DataTableEditor.CleanName(r.Name) == UltimaId);
                        ed.Save(p);
                        n++;
                    }
                }
                catch {}
            }
        }
        // Remove UltimaPower from ItemTiers if present
        try
        {
            var tiers = E33ModEditor.Core.Loot.ItemTiers.Load();
            var col = tiers.FindColumn(E33ModEditor.Core.Loot.ItemTiers.Pictos, UltimaId);
            if (col != null)
            {
                tiers.Column(E33ModEditor.Core.Loot.ItemTiers.Pictos, col).Remove(UltimaId);
                tiers.Save();
                tiers.RebuildFlat();
                n++;
            }
        }
        catch {}
        return n;
    }

    /// <summary>Rename Painted Power (OverPowered) to Ultima Power. Exclusive drop is pinned to Monolith Renoir.</summary>
    public static int RenamePaintedPower(string modDir)
    {
        int n = 0;
        // DT_PassiveEffects: Name/Description for OverPowered
        n += PatchPassiveName(modDir);
        // DT_jRPG_Items (+ Composite): Item_DisplayName / ItemDescription for OverPowered
        n += PatchItemName(modDir, "DT_jRPG_Items");
        n += PatchItemName(modDir, "DT_jRPG_Items_Composite");
        // The pictos tooltip reads ST_PassiveEffects directly; keep its keys in
        // sync with the renamed row instead of leaving "Painted Power" behind.
        var st = FindAsset(modDir, "ST_PassiveEffects.uasset");
        if (st != null)
        {
            DataTableEditor.AddStringTableEntries(st, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PASSIVE_OverPowered_Name"] = RenamedDisplay,
                ["PASSIVE_OverPowered_Description"] = RenamedDesc,
            });
        }
        // Also ensure Composite's extra tables? DT_PictosDefinitions doesn't hold display text
        return n;
    }

    private static int PatchPassiveName(string modDir)
    {
        var path = FindAsset(modDir, "DT_PassiveEffects.uasset");
        if (path == null) path = CopyIntoMod("DT_PassiveEffects", modDir);
        var ed = new DataTableEditor(path);
        if (!ed.TryGetRow(PaintedId, out var row)) return 0;
        bool changed = false;
        foreach (var f in row.Value)
        {
            var name = DataTableEditor.CleanName(f.Name);
            if (name == "Name" && f is TextPropertyData td) { SetInvariant(td, "ULTIMA_Name", RenamedDisplay); changed = true; }
            else if (name == "Description" && f is TextPropertyData td2) { SetInvariant(td2, "ULTIMA_Description", RenamedDesc); changed = true; }
        }
        if (changed) { ed.Save(path); return 1; }
        return 0;
    }

    private static int PatchItemName(string modDir, string table)
    {
        var path = FindAsset(modDir, table + ".uasset");
        if (path == null) path = CopyIntoMod(table, modDir);
        var ed = new DataTableEditor(path);
        if (!ed.TryGetRow(PaintedId, out var row)) return 0;
        bool changed = false;
        foreach (var f in row.Value)
        {
            var name = DataTableEditor.CleanName(f.Name);
            if (name == "Item_DisplayName" && f is TextPropertyData td) { SetInvariant(td, "ULTIMA_Name", RenamedDisplay); changed = true; }
            else if (name == "ItemDescription" && f is TextPropertyData td2) { SetInvariant(td2, "ULTIMA_Description", RenamedDesc); changed = true; }
            else if (name == "Item_HardcodedName" && f is NamePropertyData nd) { nd.Value = new FName(ed.Asset, PaintedId); } // keep id
        }
        if (changed) { ed.Save(path); return 1; }
        return 0;
    }

    private static void SetInvariant(TextPropertyData t, string key, string display)
    {
        display = ModLocalization.Inline(key, display);
        t.HistoryType = TextHistoryType.None;
        t.Flags = ETextFlag.CultureInvariant;
        t.Namespace = null;
        t.Value = new FString(display);
        t.CultureInvariantString = new FString(display);
        t.TableId = null;
    }

    // --- helpers ---
    private static string CopyIntoMod(string asset, string modDir)
    {
        var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "damage_limit");
        var src = FindAsset(dir, asset + ".uasset") ?? FindAsset(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), asset + ".uasset");
        if (src == null)
        {
            Directory.CreateDirectory(dir);
            new Retoc().ToLegacy(dir, asset);
            src = FindAsset(dir, asset + ".uasset") ?? throw new FileNotFoundException(asset + ".uasset");
        }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) throw new InvalidDataException(asset + " not under Sandfall/");
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    public static string? FindAsset(string root, string fileName)
    {
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).FirstOrDefault();
    }

    private static void WriteAsset(UAsset asset, string path)
    {
        asset.Write(path);
    }
}

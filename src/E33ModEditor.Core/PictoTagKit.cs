using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Writes the category tags in <see cref="PictoTagCatalog"/> onto the staged
/// <c>DT_PassiveEffects</c>, and appends the new Element / Revive / Lifesteal /
/// Luck values to the shipped <c>E_PassiveEffectCategory</c> enum asset.
///
/// The pass is deliberately run last in every pack pipeline: older installs
/// keep their generated rows (CloneRow is a no-op when a row exists), so a
/// creation-time-only fix would never repair them.
/// </summary>
public static class PictoTagKit
{
    public const string EnumStem = "E_PassiveEffectCategory";
    public const string EnumRel =
        "Sandfall/Content/Gameplay/Lumina/E_PassiveEffectCategory";

    /// <summary>
    /// Repairs the tags for every catalog row present in the staged pack.
    /// Existing tags are preserved when they do not match the catalog and do
    /// not match the known donor/legacy set — that is treated as a manual edit.
    /// </summary>
    public static int Apply(string modDir)
    {
        var pePath = PictosCreateKit.FindAsset(modDir, "DT_PassiveEffects.uasset");
        if (pePath is null) return 0;

        var ed = new DataTableEditor(pePath);
        var edits = new JObject();
        int changed = 0;

        foreach (var rule in PictoTagCatalog.Rules)
        {
            if (!ed.HasRow(rule.Id)) continue;

            var current = ReadCategories(ed, rule.Id);
            var expected = new HashSet<string>(rule.Tags, StringComparer.Ordinal);
            if (current.SetEquals(expected)) continue;

            var legacy = rule.Legacy is null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(rule.Legacy, StringComparer.Ordinal);

            if (current.Count == 0 || current.SetEquals(legacy))
            {
                edits[rule.Id] = new JObject
                {
                    ["Categories"] = new JArray(rule.Tags.Select(t =>
                        EnumStem + "::" + t)),
                };
                changed++;
            }
            else
            {
                Console.WriteLine(
                    $"[picto-tags] {rule.Id}: keeping manual categories " +
                    $"[{string.Join(", ", current)}] (catalog wants " +
                    $"[{string.Join(", ", rule.Tags)}])");
            }
        }

        if (changed > 0)
        {
            ed.Apply(edits.ToString());
            ed.Save(pePath);
        }

        Console.WriteLine($"[picto-tags] {changed} row(s) retagged");
        EnsureEnum(modDir);
        return changed;
    }

    /// <summary>
    /// Appends the four new display values to the enum asset, writes the
    /// modified cache copy (so the editor renders the labels) and stages the
    /// same asset into the mod tree.
    /// </summary>
    public static void EnsureEnum(string modDir)
    {
        var src = FindEnumSource();
        if (src is null)
            throw new FileNotFoundException(
                $"{EnumStem}.uasset not found (work/enums, work/tables, work/mod)");

        var ed = DataTableEditor.OpenPropertyAsset(src);
        var ee = ed.Asset.Exports.OfType<EnumExport>().FirstOrDefault()
            ?? throw new InvalidDataException(
                $"{EnumStem} has no EnumExport");

        var map = ee.Data
            .FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "DisplayNameMap")
            as MapPropertyData
            ?? throw new InvalidDataException(
                $"{EnumStem} has no DisplayNameMap");

        bool changed = false;
        long next = ee.Enum.Names
            .Where(n => !n.Item1.ToString().EndsWith("::E_MAX", StringComparison.Ordinal))
            .Select(n => n.Item2)
            .DefaultIfEmpty(0)
            .Max() + 1;

        foreach (var (tag, label) in PictoTagCatalog.NewTags)
        {
            var full = EnumStem + "::" + tag;
            if (ee.Enum.Names.All(n => n.Item1.ToString() != full))
            {
                var maxIdx = ee.Enum.Names.FindIndex(n =>
                    n.Item1.ToString().EndsWith("::E_MAX", StringComparison.Ordinal));
                var entry = Tuple.Create(new FName(ed.Asset, full), next++);
                if (maxIdx >= 0) ee.Enum.Names.Insert(maxIdx, entry);
                else ee.Enum.Names.Add(entry);
                changed = true;
            }

            if (map.Value.Keys.All(k => KeyName(k) != tag))
            {
                var keyTemplate = (NamePropertyData)map.Value.Keys.First();
                var valueTemplate = (TextPropertyData)map.Value.Values.First();

                var key = (NamePropertyData)keyTemplate.Clone();
                key.Value = new FName(ed.Asset, tag);

                var value = (TextPropertyData)valueTemplate.Clone();
                value.HistoryType = TextHistoryType.None;
                value.Flags = ETextFlag.CultureInvariant;
                value.Namespace = null;
                value.TableId = null;
                value.Value = new FString(label);
                value.CultureInvariantString = new FString(label);

                map.Value.Add(key, value);
                changed = true;
            }
        }

        // E_MAX must stay one past the last real value.
        var maxIndex = ee.Enum.Names.FindIndex(n =>
            n.Item1.ToString().EndsWith("::E_MAX", StringComparison.Ordinal));
        if (maxIndex >= 0)
        {
            var maxName = ee.Enum.Names[maxIndex].Item1;
            long maxValue = ee.Enum.Names
                .Where(n => !n.Item1.ToString().EndsWith("::E_MAX", StringComparison.Ordinal))
                .Select(n => n.Item2)
                .DefaultIfEmpty(-1)
                .Max() + 1;
            if (ee.Enum.Names[maxIndex].Item2 != maxValue)
            {
                ee.Enum.Names[maxIndex] = Tuple.Create(maxName, maxValue);
                changed = true;
            }
        }

        if (changed)
        {
            // Keep the extracted cache in sync so the editor's category
            // checkboxes and record display resolve the new labels.
            ed.Save(src);
            DataTableEditor.ResetEnumCache();
        }

        var dest = Path.Combine(modDir, EnumRel + ".uasset");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        ed.Asset.Write(dest);
        Console.WriteLine(
            $"[picto-tags] {EnumStem} staged with {PictoTagCatalog.NewTags.Length} new value(s)");
    }

    private static string? FindEnumSource()
    {
        // Prefer the editor's extracted cache, then any staged copy (a fresh
        // install may already have the extended enum in work/mod), and only
        // then ask retoc. This avoids a hard dependency on a live extraction
        // when ~mods currently holds a pack.
        foreach (var root in new[]
                 {
                     Path.Combine(EditorPaths.RepoRoot, "work", "enums"),
                     Path.Combine(EditorPaths.RepoRoot, "work", "tables"),
                     Path.Combine(EditorPaths.RepoRoot, "work", "mod"),
                 })
        {
            if (!Directory.Exists(root)) continue;
            var found = FileCopy.FindUnder(root, EnumStem + ".uasset");
            if (found is not null) return found;
        }

        var dir = Path.Combine(EditorPaths.RepoRoot, "work", "enums");
        Directory.CreateDirectory(dir);
        new Retoc().ToLegacy(dir, $"\"{EnumStem}\"");
        return FileCopy.FindUnder(dir, EnumStem + ".uasset");
    }

    private static HashSet<string> ReadCategories(DataTableEditor ed, string rowId)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (!ed.TryGetRow(rowId, out var row)) return set;

        var arr = row.Value
            .FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "Categories")
            as ArrayPropertyData;
        if (arr is null) return set;

        foreach (var p in arr.Value)
        {
            var full = p switch
            {
                EnumPropertyData e => e.Value?.Value?.Value,
                BytePropertyData b => b.EnumValue?.Value?.Value,
                NamePropertyData n => n.Value?.Value?.Value,
                _ => null,
            };
            if (string.IsNullOrEmpty(full)) continue;
            var split = full.Split("::");
            set.Add(split.Length == 2 ? split[1] : full);
        }
        return set;
    }

    private static string KeyName(PropertyData key) => key switch
    {
        NamePropertyData n => n.Value?.Value?.Value ?? "",
        StrPropertyData s => s.Value?.Value ?? "",
        _ => "",
    };
}

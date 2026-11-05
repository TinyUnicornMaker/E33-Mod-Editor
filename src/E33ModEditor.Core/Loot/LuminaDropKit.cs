using UAssetAPI;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core.Loot;

/// <summary>
/// Optional late-pack lumina adjustment. Every regular loot source of
/// <c>Consumable_LuminaPoint</c> is scaled by the supplied multiplier:
/// <list type="bullet">
/// <item><c>DT_ChestsContent</c> sections,</item>
/// <item><c>DT_jRPG_Enemies.PossibleLoot</c>, and</item>
/// <item>every <c>DT_Merchant_*</c> stock row.</item>
/// </list>
///
/// A multiplier below one lowers quantities and drop chances; a multiplier
/// above one raises them. Sources with a single lumina point scale their drop
/// chance (or weight) instead, because a fractional stack cannot be reduced.
/// Chests that contain nothing but lumina are a special case when reducing:
/// half of their one-point sections are replaced with a tier-appropriate Chroma
/// pack, so a reduced pack never leaves a chest completely empty.
///
/// Dialogue rewards are deliberately never touched, so Blanche's pacifist
/// lumina grant (<c>BP_Dialogue_JudgeOfMercy</c>) and every other dialogue
/// payout stay vanilla.
///
/// ⚠ Must run after <see cref="RandomizerKit.Apply"/> so it adjusts the staged
/// seed's loot rather than being overwritten by it.
/// </summary>
public static class LuminaDropKit
{
    public const string LuminaItem = "Consumable_LuminaPoint";
    public const double DefaultMultiplier = 1.0;

    private const string ChestsTable = "DT_ChestsContent";
    private const string EnemiesTable = "DT_jRPG_Enemies";
    private const string MerchantPrefix = "DT_Merchant_";

    public static int Apply(string modDir, LootCatalog? catalog = null,
        double multiplier = DefaultMultiplier)
    {
        if (Math.Abs(multiplier - 1) < 0.0001) return 0;
        multiplier = Math.Clamp(multiplier, 0.05, 5.0);

        int changed = 0;
        changed += ScaleStaged(modDir, ChestsTable, path => ScaleChests(path, catalog, multiplier));
        changed += ScaleStaged(modDir, EnemiesTable, path => ScaleEnemyDrops(path, multiplier));

        foreach (var source in MerchantSources(modDir))
        {
            var dest = DestFor(modDir, source);
            bool copied = !File.Exists(dest);
            if (copied) FileCopy.AssetPair(source, dest);
            var n = ScaleMerchant(dest, multiplier);
            if (n == 0 && copied) DeletePair(dest);
            changed += n;
        }
        if (changed > 0)
            Console.WriteLine($"[lumina] {changed} lumina source(s) scaled by {multiplier:0.##}");
        return changed;
    }

    /// <summary>Stages one table (if needed), runs the editor action, and drops
    /// an unchanged scratch copy again so an enabled toggle does not add
    /// untouched vanilla tables to the pack.</summary>
    private static int ScaleStaged(string modDir, string stem, Func<string, int> edit)
    {
        var existing = GameplayTweaksKit.FindAsset(modDir, stem + ".uasset");
        if (existing is not null) return edit(existing);

        var src = FileCopy.FindTablesAsset(stem + ".uasset");
        if (src is null) return 0;
        var dest = DestFor(modDir, src);
        FileCopy.AssetPair(src, dest);
        var changed = edit(dest);
        if (changed == 0) DeletePair(dest);
        return changed;
    }

    private static int ScaleChests(string path, LootCatalog? catalog, double multiplier)
    {
        var ed = new DataTableEditor(path);
        int changed = 0;
        int onePointPureRows = 0;

        foreach (var row in ed.Table.Table.Data)
        {
            if (Named(row.Value, "LootTableSections") is not ArrayPropertyData sections) continue;
            var rows = sections.Value.OfType<StructPropertyData>().ToList();
            if (rows.Count == 0) continue;

            var luminaSections = new List<LuminaSection>();
            bool everySectionIsLumina = true;
            foreach (var sec in rows)
            {
                var entries = Named(sec.Value, "LootEntries") as ArrayPropertyData;
                var lumina = entries?.Value.OfType<StructPropertyData>()
                    .Where(IsLuminaEntry).ToList() ?? new List<StructPropertyData>();
                if (lumina.Count == 0 || entries is null || entries.Value.Length == 0)
                {
                    everySectionIsLumina = false;
                    continue;
                }
                luminaSections.Add(new LuminaSection(sec, entries, lumina));
            }
            if (luminaSections.Count == 0) continue;

            if (!everySectionIsLumina)
            {
                foreach (var section in luminaSections)
                {
                    // A normal chest keeps its other sections at full chance;
                    // only the lumina-only section's chance comes down.
                    if (section.Lumina.Count == section.Entries.Value.Length)
                        changed += ScaleSectionChance(section.Section, multiplier);
                    else
                        changed += ScaleMixedSection(section, multiplier);
                }
                continue;
            }

            if (multiplier > 1)
            {
                // Increasing a pure-lumina chest: raise section chance first;
                // once it is already at 100%, raise the stack quantity instead.
                foreach (var section in luminaSections)
                {
                    if (ScaleSectionChance(section.Section, multiplier) > 0)
                    {
                        changed++;
                        continue;
                    }
                    foreach (var entry in section.Lumina)
                        changed += ScaleQuantity(entry, multiplier);
                }
                continue;
            }

            // Chest built entirely from lumina: halve stacks, then swap half of
            // the remaining one-point sections for chroma so the row still
            // yields something. A lone one-point row alternates across rows.
            var onePoint = new List<LuminaSection>();
            foreach (var section in luminaSections)
            {
                if (section.Lumina.Count != 1)
                {
                    changed += ScaleSectionChance(section.Section, multiplier);
                    continue;
                }
                var entry = section.Lumina[0];
                if (QuantityOf(entry) > 1)
                    changed += ScaleQuantity(entry, multiplier);
                else
                    onePoint.Add(section);
            }
            if (onePoint.Count == 0) continue;

            int replace = onePoint.Count >= 2
                ? onePoint.Count / 2
                : (onePointPureRows++ % 2 == 1 ? 1 : 0);
            var chroma = ChromaForTier(ChestTier(catalog, DataTableEditor.CleanName(row.Name)));
            for (int i = 0; i < replace; i++)
                if (ReplaceEntry(ed.Asset, onePoint[i].Lumina[0], chroma)) changed++;
        }

        if (changed > 0) Save(ed, path);
        return changed;
    }

    private static int ScaleEnemyDrops(string path, double multiplier)
    {
        var ed = new DataTableEditor(path);
        int changed = 0;
        foreach (var row in ed.Table.Table.Data)
        {
            if (Named(row.Value, "PossibleLoot") is not ArrayPropertyData loot) continue;
            foreach (var entry in loot.Value.OfType<StructPropertyData>())
            {
                if (!IsLuminaEntry(entry)) continue;
                if (QuantityOf(entry) > 1)
                {
                    changed += ScaleQuantity(entry, multiplier);
                }
                else if (ScaleChance(entry, multiplier) > 0)
                {
                    changed++;
                }
                else if (multiplier > 1)
                {
                    // Chance is already capped at 100%, so increase the stack.
                    changed += ScaleQuantity(entry, multiplier);
                }
            }
        }
        if (changed > 0) Save(ed, path);
        return changed;
    }

    private static int ScaleMerchant(string path, double multiplier)
    {
        var ed = new DataTableEditor(path);
        int changed = 0;
        foreach (var row in ed.Table.Table.Data)
        {
            var rowId = DataTableEditor.CleanName(row.Name);
            var itemId = Named(row.Value, "ItemRowName") is NamePropertyData item
                ? item.Value?.ToString()
                : rowId;
            if (!string.Equals(itemId, LuminaItem, StringComparison.Ordinal)
                && !string.Equals(rowId, LuminaItem, StringComparison.Ordinal))
                continue;
            if (Named(row.Value, "Quantity") is IntPropertyData qty)
                changed += ScaleQuantity(qty, multiplier) ? 1 : 0;
        }
        if (changed > 0) Save(ed, path);
        return changed;
    }

    /// <summary>
    /// A section that mixes lumina and other entries. Scaling the section's
    /// chance would also change the other drops, so the lumina entries' weight
    /// share is adjusted instead. For reductions the other weights grow; for
    /// increases the lumina weights grow. Either way the resulting lumina share
    /// is the requested multiple of its vanilla share, within integer rounding.
    /// </summary>
    private static int ScaleMixedSection(LuminaSection section, double multiplier)
    {
        int changed = 0;
        foreach (var entry in section.Lumina)
        {
            if (QuantityOf(entry) > 1)
                changed += ScaleQuantity(entry, multiplier);
        }

        var onePoint = section.Lumina.Where(e => QuantityOf(e) <= 1).ToList();
        if (onePoint.Count == 0) return changed;

        var others = section.Entries.Value.OfType<StructPropertyData>()
            .Where(e => !IsLuminaEntry(e)).ToList();
        if (others.Count == 0)
            return changed + ScaleSectionChance(section.Section, multiplier);

        var luminaWeights = new List<(StructPropertyData Entry, IntPropertyData Weight)>();
        foreach (var entry in onePoint)
            if (Named(entry.Value, "Weight") is IntPropertyData weight)
                luminaWeights.Add((entry, weight));

        var otherWeights = new List<(StructPropertyData Entry, IntPropertyData Weight)>();
        foreach (var other in others)
            if (Named(other.Value, "Weight") is IntPropertyData weight)
                otherWeights.Add((other, weight));

        if (luminaWeights.Count == 0 || otherWeights.Count == 0)
        {
            // No weight columns to adjust: fall back to the entry's own chance.
            int fallback = 0;
            foreach (var entry in onePoint)
            {
                int scaled = ScaleChance(entry, multiplier);
                if (scaled > 0)
                    fallback += scaled;
                else if (multiplier > 1)
                    fallback += ScaleQuantity(entry, multiplier);
            }
            return changed + fallback;
        }

        int luminaTotal = luminaWeights.Sum(x => Math.Max(0, x.Weight.Value));
        int otherTotal = otherWeights.Sum(x => Math.Max(0, x.Weight.Value));
        if (luminaTotal <= 0 || otherTotal <= 0) return changed;

        int SetWeight(IntPropertyData weight, int value)
        {
            value = Math.Max(1, value);
            if (value == weight.Value) return 0;
            weight.Value = value;
            return 1;
        }

        if (multiplier < 1)
        {
            // Grow the non-lumina side; this preserves the lumina stack itself.
            foreach (var pair in otherWeights)
                changed += SetWeight(pair.Weight,
                    (int)Math.Floor(pair.Weight.Value / multiplier));

            int currentOther = otherWeights.Sum(x => x.Weight.Value);
            double targetOther = (luminaTotal + otherTotal) / multiplier - luminaTotal;
            int correction = (int)Math.Round(targetOther) - currentOther;
            if (correction != 0)
                changed += SetWeight(otherWeights[0].Weight,
                    otherWeights[0].Weight.Value + correction);
        }
        else
        {
            // Grow the lumina side so the section's other drops stay untouched.
            double denominator = luminaTotal + otherTotal - multiplier * luminaTotal;
            double targetLumina = denominator <= 0
                ? Math.Max(luminaTotal, otherTotal * 1000.0)
                : multiplier * luminaTotal * otherTotal / denominator;
            int target = Math.Max(luminaTotal, (int)Math.Round(targetLumina));

            foreach (var pair in luminaWeights)
            {
                int share = (int)Math.Floor(
                    (double)Math.Max(0, pair.Weight.Value) / luminaTotal * target + 0.5);
                changed += SetWeight(pair.Weight, share);
            }

            int assigned = luminaWeights.Sum(x => x.Weight.Value);
            int correction = target - assigned;
            if (correction != 0)
                changed += SetWeight(luminaWeights[0].Weight,
                    luminaWeights[0].Weight.Value + correction);
        }

        return changed;
    }

    private static int ScaleSectionChance(StructPropertyData section, double multiplier) =>
        Named(section.Value, "ChancePercent") is { } p && ScaleNumber(p, multiplier) ? 1 : 0;

    private static int ScaleChance(StructPropertyData entry, double multiplier) =>
        Named(entry.Value, "LootPercentageChance") is { } p && ScaleNumber(p, multiplier) ? 1 : 0;

    private static int ScaleQuantity(StructPropertyData entry, double multiplier) =>
        Named(entry.Value, "Quantity") is IntPropertyData q && ScaleQuantity(q, multiplier) ? 1 : 0;

    private static bool ScaleQuantity(IntPropertyData quantity, double multiplier)
    {
        var value = multiplier < 1
            ? (int)Math.Floor(quantity.Value * multiplier)
            : (int)Math.Floor(quantity.Value * multiplier + 0.5);
        value = Math.Max(1, value);
        if (value == quantity.Value) return false;
        quantity.Value = value;
        return true;
    }

    private static bool ScaleNumber(PropertyData field, double multiplier)
    {
        switch (field)
        {
            case FloatPropertyData f:
            {
                var value = Math.Clamp(
                    (float)Math.Round(f.Value * multiplier, 2), 0f, 100f);
                if (Math.Abs(value - f.Value) < 0.000001) return false;
                f.Value = value;
                return true;
            }
            case DoublePropertyData d:
            {
                var value = Math.Clamp(
                    Math.Round(d.Value * multiplier, 2), 0d, 100d);
                if (Math.Abs(value - d.Value) < 0.0000001) return false;
                d.Value = value;
                return true;
            }
            case IntPropertyData i:
            {
                var value = Math.Clamp(
                    (int)Math.Floor(i.Value * multiplier), 0, 100);
                if (value == i.Value) return false;
                i.Value = value;
                return true;
            }
            default:
                return false;
        }
    }

    private static bool ReplaceEntry(UAsset asset, StructPropertyData entry, string itemId)
    {
        if (Named(entry.Value, "ItemID") is not NamePropertyData id) return false;
        if (string.Equals(id.Value?.ToString(), itemId, StringComparison.Ordinal)) return false;
        asset.AddNameReference(FString.FromString(itemId));
        id.Value = new FName(asset, itemId);
        return true;
    }

    private static int ChestTier(LootCatalog? catalog, string rowId)
    {
        if (catalog is null) return 1;
        var check = catalog.Checks.FirstOrDefault(
            c => (c.Source == "Chest" || c.Source == LootCatalog.PaintCageSource)
                 && string.Equals(c.Id, rowId, StringComparison.Ordinal));
        return Math.Clamp(check?.Tier ?? 1, 1, 5);
    }

    private static string ChromaForTier(int tier) => tier switch
    {
        <= 1 => "ChromaPack_Regular",
        2 => "ChromaPack_Large",
        _ => "ChromaPack_ExtraLarge",
    };

    private static bool IsLuminaEntry(StructPropertyData entry)
    {
        if (Named(entry.Value, "Item") is StructPropertyData item
            && Named(item.Value, "RowName") is NamePropertyData name
            && string.Equals(name.Value?.ToString(), LuminaItem, StringComparison.Ordinal))
            return true;
        return Named(entry.Value, "ItemID") is NamePropertyData direct
            && string.Equals(direct.Value?.ToString(), LuminaItem, StringComparison.Ordinal);
    }

    private static int QuantityOf(StructPropertyData entry) =>
        Named(entry.Value, "Quantity") is IntPropertyData q ? q.Value : 1;

    private static int WeightOf(StructPropertyData entry) =>
        Named(entry.Value, "Weight") is IntPropertyData w ? Math.Max(0, w.Value) : 1;

    private static PropertyData? Named(IList<PropertyData> data, string name) =>
        data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == name);

    private static string DestFor(string modDir, string src)
    {
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0)
            throw new InvalidDataException(Path.GetFileName(src) + " is not under Sandfall/");
        return Path.Combine(modDir, src[idx..]);
    }

    private static IEnumerable<string> MerchantSources(string modDir)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(modDir))
        {
            foreach (var file in Directory.EnumerateFiles(
                         modDir, MerchantPrefix + "*.uasset", SearchOption.AllDirectories))
            {
                if (seen.Add(Path.GetFileName(file)))
                    yield return file;
            }
        }

        var tables = Path.Combine(EditorPaths.RepoRoot, "work", "tables");
        if (!Directory.Exists(tables)) yield break;
        foreach (var file in Directory.EnumerateFiles(
                     tables, MerchantPrefix + "*.uasset", SearchOption.AllDirectories))
        {
            if (seen.Add(Path.GetFileName(file)))
                yield return file;
        }
    }

    private static void Save(DataTableEditor editor, string path)
    {
        try { editor.Save(path); }
        catch (FormatException ex)
        {
            throw new FormatException(Path.GetFileName(path) + ": " + ex.Message, ex);
        }
    }

    private static void DeletePair(string uasset)
    {
        foreach (var ext in new[] { ".uasset", ".uexp" })
        {
            var p = Path.ChangeExtension(uasset, ext);
            if (File.Exists(p)) File.Delete(p);
        }
    }

    private sealed record LuminaSection(
        StructPropertyData Section,
        ArrayPropertyData Entries,
        List<StructPropertyData> Lumina);
}

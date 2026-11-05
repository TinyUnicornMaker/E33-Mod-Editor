using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.Core.Loot;

/// <summary>
/// Pictos / Weapon T1–T5 plus DoNotUse. Stock list is the original
/// randomizer item_tiers.json; the editor overlay lives in work/item_tiers.json
/// and is also written back to the Core data file so a shippable stock list
/// can be edited in-tool.
/// </summary>
public sealed class ItemTiers
{
    public const string Pictos = "Pictos";
    public const string Weapon = "Weapon";
    public static readonly string[] Categories = { Pictos, Weapon };
    public static readonly string[] Columns = { "Tier1", "Tier2", "Tier3", "Tier4", "Tier5", "DoNotUse" };

    public Dictionary<string, Dictionary<string, List<string>>> Data { get; private set; } = NewEmpty();

    /// <summary>Flat keys like Pictos_Tier2 used by the randomizer pools.</summary>
    public Dictionary<string, List<string>> Flat { get; } = new(StringComparer.Ordinal);

    public static string OverlayPath =>
        Path.Combine(EditorPaths.RepoRoot, "work", "item_tiers.json");

    public static string StockSourcePath =>
        Path.Combine(EditorPaths.RepoRoot, "src", "E33ModEditor.Core", "Loot", "Data", "item_tiers.json");

    public static ItemTiers Load()
    {
        var t = new ItemTiers();
        t.ResetToStock();
        if (File.Exists(OverlayPath))
        {
            try { t.ReplaceFromJson(File.ReadAllText(OverlayPath)); }
            catch { /* keep stock */ }
        }
        t.RebuildFlat();
        return t;
    }

    public void ResetToStock()
    {
        Data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<string>>>>(
                   ReadStock()) ?? NewEmpty();
        EnsureColumns();
        RebuildFlat();
    }

    public void ReplaceFromJson(string json)
    {
        var parsed = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<string>>>>(json);
        if (parsed is null) return;
        Data = parsed;
        EnsureColumns();
        RebuildFlat();
    }

    public string ToJson()
    {
        EnsureColumns();
        return JsonConvert.SerializeObject(Data, Formatting.Indented);
    }

    public void Save()
    {
        var json = ToJson();
        Directory.CreateDirectory(Path.GetDirectoryName(OverlayPath)!);
        File.WriteAllText(OverlayPath, json);
        try
        {
            var dir = Path.GetDirectoryName(StockSourcePath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                File.WriteAllText(StockSourcePath, json);
        }
        catch { /* source tree may be read-only in a packed install */ }
        RebuildFlat();
    }

    public List<string> Column(string category, string column)
    {
        if (!Data.TryGetValue(category, out var cat))
        {
            cat = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            Data[category] = cat;
        }
        if (!cat.TryGetValue(column, out var list))
        {
            list = new List<string>();
            cat[column] = list;
        }
        return list;
    }

    public string? FindColumn(string category, string id)
    {
        if (!Data.TryGetValue(category, out var cat)) return null;
        foreach (var col in Columns)
        {
            if (cat.TryGetValue(col, out var list) && list.Contains(id))
                return col;
        }
        return null;
    }

    public void Move(string category, string id, string destColumn)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!Columns.Contains(destColumn) && destColumn != "Unplaced") return;
        foreach (var col in Columns)
            Column(category, col).RemoveAll(x => x == id);
        if (destColumn != "Unplaced")
            Column(category, destColumn).Add(id);
        RebuildFlat();
    }

    public void MoveAllToDoNotUse(string category)
    {
        var dest = Column(category, "DoNotUse");
        foreach (var col in Columns)
        {
            if (col == "DoNotUse") continue;
            var src = Column(category, col);
            dest.AddRange(src);
            src.Clear();
        }
        RebuildFlat();
    }

    public IReadOnlyList<string> Unplaced(string category, IEnumerable<string> knownIds)
    {
        var placed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var col in Columns)
            foreach (var id in Column(category, col))
                placed.Add(id);
        return knownIds.Where(id => !placed.Contains(id)).Distinct(StringComparer.Ordinal).ToList();
    }

    public void RebuildFlat()
    {
        Flat.Clear();
        foreach (var (cat, tiers) in Data)
        foreach (var (tier, items) in tiers)
            Flat[$"{cat}_{tier}"] = items;
    }

    private void EnsureColumns()
    {
        foreach (var cat in Categories)
        {
            if (!Data.ContainsKey(cat))
                Data[cat] = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var col in Columns)
                if (!Data[cat].ContainsKey(col))
                    Data[cat][col] = new List<string>();
        }
    }

    private static Dictionary<string, Dictionary<string, List<string>>> NewEmpty()
    {
        var d = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);
        foreach (var cat in Categories)
        {
            var inner = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var col in Columns) inner[col] = new List<string>();
            d[cat] = inner;
        }
        return d;
    }

    private static string ReadStock()
    {
        var asm = typeof(ItemTiers).Assembly;
        var name = asm.GetManifestResourceNames()
            .First(n => n.EndsWith("item_tiers.json", StringComparison.OrdinalIgnoreCase));
        using var s = asm.GetManifestResourceStream(name)!;
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}

using System.Text.Json;

namespace E33ModEditor.Core.SaveGame;

/// <summary>One real world spawn tag harvested from the game's location table.</summary>
public sealed record SpawnPoint(string Tag, string Name, string LevelAsset)
{
    /// <summary>Area prefix: AbbestCave, WorldMap, Camp, …</summary>
    public string Area
    {
        get
        {
            const string prefix = "Level.SpawnPoint.";
            if (!Tag.StartsWith(prefix, StringComparison.Ordinal)) return "Other";
            var rest = Tag[prefix.Length..];
            var dot = rest.IndexOf('.');
            return dot > 0 ? rest[..dot] : rest;
        }
    }
}

/// <summary>Dropdown catalog for <c>SpawnPointTagToLoadAt</c> / <c>LastUsedSavePoint</c>.</summary>
public static class SpawnCatalog
{
    private static readonly Lazy<IReadOnlyList<SpawnPoint>> LazyAll = new(Load);

    public static IReadOnlyList<SpawnPoint> All => LazyAll.Value;

    public static SpawnPoint? Find(string tag) =>
        string.IsNullOrEmpty(tag) ? null
        : All.FirstOrDefault(s => s.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<SpawnPoint> Load()
    {
        var asm = typeof(SpawnCatalog).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("SpawnPoints.json", StringComparison.OrdinalIgnoreCase));
        if (name is null) return Array.Empty<SpawnPoint>();
        using var stream = asm.GetManifestResourceStream(name);
        if (stream is null) return Array.Empty<SpawnPoint>();
        var rows = JsonSerializer.Deserialize<List<JsonRow>>(stream) ?? new List<JsonRow>();
        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Tag))
            .Select(r => new SpawnPoint(
                r.Tag.Trim(),
                string.IsNullOrWhiteSpace(r.Name) ? r.Tag : r.Name.Trim(),
                r.LevelAsset?.Trim() ?? ""))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed class JsonRow
    {
        public string Tag { get; set; } = "";
        public string Name { get; set; } = "";
        public string LevelAsset { get; set; } = "";
    }
}

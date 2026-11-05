using System.Reflection;
using System.Text.Json;

namespace E33ModEditor.Core.Localization;

/// <summary>
/// The mod's own translation catalogs. English is the source language; the
/// selected language is applied while kits write text and is used to build a
/// patched Game.locres at pack time.
/// </summary>
public static class ModLocalization
{
    public sealed record Language(string Code, string Culture, string DisplayName);

    public static readonly Language English = new("en", "en-US", "English");
    public static readonly IReadOnlyList<Language> Supported = new[]
    {
        English,
        new Language("fr", "fr-FR", "Français"),
        new Language("pl", "pl", "Polski"),
        new Language("ru", "ru", "Русский"),
    };

    private sealed class Catalog
    {
        /// <summary>table\0key -> text (the locres namespace + key).</summary>
        public Dictionary<string, string> Tables { get; } = new(StringComparer.Ordinal);
        /// <summary>key -> text for helpers that do not carry a table name.</summary>
        public Dictionary<string, string> ByKey { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Inline { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Missing { get; } = new(StringComparer.Ordinal);
    }

    private static readonly Dictionary<string, Catalog> Catalogs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    public static Language Current { get; private set; } = English;
    public static bool IsEnglish => Current.Code == English.Code;

    public static Language? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return Supported.FirstOrDefault(l =>
            string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(l.Culture, code, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(l.DisplayName, code, StringComparison.OrdinalIgnoreCase));
    }

    public static void SetLanguage(string? code)
    {
        var language = Find(code) ?? English;
        Current = language;
        if (!IsEnglish) _ = Load(language);
    }

    /// <summary>Translates a StringTable key. Falls back to English when absent.</summary>
    public static string Key(string key, string english)
    {
        if (IsEnglish || string.IsNullOrEmpty(key)) return english;
        var catalog = Load(Current);
        if (catalog.ByKey.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            return value;
        // Unchanged vanilla keys are not in the catalog (there is nothing to
        // translate), but some are written inline by a kit. Use the official
        // translation from the culture's vanilla locres when available.
        if (LocalizationKit.TryVanillaTranslation(Current.Culture, key, english, out var vanilla)
            && !string.IsNullOrEmpty(vanilla))
            return vanilla;
        return Miss(catalog, key, english);
    }

    /// <summary>Translates a non-table (inline/culture-invariant) string.</summary>
    public static string Inline(string key, string english)
    {
        if (IsEnglish || string.IsNullOrEmpty(key)) return english;
        var catalog = Load(Current);
        if (catalog.Inline.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            return value;
        if (LocalizationKit.TryVanillaTranslation(Current.Culture, key, english, out var vanilla)
            && !string.IsNullOrEmpty(vanilla))
            return vanilla;
        return Miss(catalog, "inline:" + key, english);
    }

    /// <summary>All keys the selected language is missing (for build validation).</summary>
    public static IReadOnlyCollection<string> MissingKeys()
    {
        if (IsEnglish) return Array.Empty<string>();
        return Load(Current).Missing.ToArray();
    }

    /// <summary>Drops cached catalogs so the next build sees edited work/ JSON.</summary>
    public static void ResetCache()
    {
        lock (Gate) Catalogs.Clear();
        LocalizationKit.ResetVanillaCache();
    }

    public static void ClearMissing()
    {
        lock (Gate)
            foreach (var catalog in Catalogs.Values) catalog.Missing.Clear();
    }

    public static IReadOnlyDictionary<string, string> TableValues(string language) =>
        Load(Find(language) ?? English).Tables;

    public static IReadOnlyDictionary<string, string> InlineValues(string language) =>
        Load(Find(language) ?? English).Inline;

    private static string Miss(Catalog catalog, string key, string english)
    {
        lock (Gate) catalog.Missing.Add(key);
        return english;
    }

    private static Catalog Load(Language language)
    {
        lock (Gate)
        {
            if (Catalogs.TryGetValue(language.Code, out var cached)) return cached;

            var catalog = new Catalog();
            var json = ReadCatalogJson(language.Code);
            if (json is not null)
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("tables", out var tables))
                {
                    foreach (var table in tables.EnumerateObject())
                        foreach (var entry in table.Value.EnumerateObject())
                        {
                            var text = entry.Value.GetString() ?? "";
                            catalog.Tables[$"{table.Name}\0{entry.Name}"] = text;
                            catalog.ByKey[entry.Name] = text;
                        }
                }

                if (root.TryGetProperty("inline", out var inline))
                {
                    foreach (var entry in inline.EnumerateObject())
                        catalog.Inline[entry.Name] = entry.Value.GetString() ?? "";
                }
            }

            Catalogs[language.Code] = catalog;
            return catalog;
        }
    }

    private static string? ReadCatalogJson(string code)
    {
        // A work/ copy lets translators iterate without rebuilding the editor.
        var overridePath = Path.Combine(EditorPaths.RepoRoot, "work", "localization", code + ".json");
        if (File.Exists(overridePath)) return File.ReadAllText(overridePath);

        var assembly = typeof(ModLocalization).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($".Localization.Data.{code}.json", StringComparison.OrdinalIgnoreCase));
        if (resource is null) return null;

        using var stream = assembly.GetManifestResourceStream(resource);
        if (stream is null) return null;
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Loads the English source catalog from the embedded resource.</summary>
    public static IReadOnlyDictionary<string, string> EnglishTableValues() =>
        Load(English).Tables;
}

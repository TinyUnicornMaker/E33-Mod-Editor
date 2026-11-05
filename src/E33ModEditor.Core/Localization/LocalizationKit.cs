using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.Core.Localization;

/// <summary>
/// Build-time localization: extracts vanilla locres, merges the mod's catalog
/// entries into the selected culture's Game.locres, and validates translations.
/// </summary>
public static class LocalizationKit
{
    public sealed record TableSpec(string Name, string RelativePath);

    public static readonly IReadOnlyList<TableSpec> Tables = new[]
    {
        new TableSpec("ST_MainCharacters_Skills", "Sandfall/Content/StringTables/ST_MainCharacters_Skills.uasset"),
        new TableSpec("ST_PassiveEffects", "Sandfall/Content/StringTables/Gameplay/ST_PassiveEffects.uasset"),
        new TableSpec("ST_Items", "Sandfall/Content/StringTables/Gameplay/ST_Items.uasset"),
        new TableSpec("ST_UniqueMechanics", "Sandfall/Content/StringTables/Gameplay/ST_UniqueMechanics.uasset"),
        new TableSpec("ST_BattleTooltips", "Sandfall/Content/StringTables/Gameplay/ST_BattleTooltips.uasset"),
        new TableSpec("ST_UI_GameMenu", "Sandfall/Content/StringTables/UI/ST_UI_GameMenu.uasset"),
        new TableSpec("ST_UI_InputActions", "Sandfall/Content/StringTables/UI/ST_UI_InputActions.uasset"),
    };

    /// <summary>Strings written as culture-invariant FText, keyed for translation.</summary>
    public static readonly IReadOnlyDictionary<string, string> InlineSource = new SortedDictionary<string, string>(StringComparer.Ordinal)
    {
        ["SKILL_SuperSpark_Name"] = "Super Spark",
        ["SKILL_SuperSpark_Description_Long"] =
            "Deals low single target <keyword id=\"Element_Void\">Void</> damage. 3 hits.\n" +
            "Applies 5 Blight\n" +
            "<keyword id=\"Stance_Offensive\">Offensive Stance</>: Applies 10 Blight",
        ["SKILL_SuperSpark_Description_Short"] =
            "Low <keyword id=\"Element_Void\">Void</> damage. 3 hits.\n" +
            "Applies 5 Blight\n" +
            "<img id=\"Stance_Offensive\"/>: Applies 10 Blight",
        ["ULTIMA_Name"] = "Ultima Power",
        ["ULTIMA_Description"] = "Fully unlocks your power.",
        ["BUFF_SuperSonic_Name"] = "Super Sonic",
        ["BUFF_SuperSonic_Description"] = "Speed increased by 100%.",
        ["BUFF_RuptureShot_Name"] = "Rupture Shot",
        ["BUFF_UnoPuncho_Name"] = "Uno Puncho",
        ["BATTLETOOLTIP_GradientOvercharge_Template"] = "{skill}: {char} deals {value}% more damage this turn.",
    };

    public static string CultureFor(string? language) =>
        (ModLocalization.Find(language) ?? ModLocalization.English).Culture;

    public static string CacheDir => Path.Combine(EditorPaths.RepoRoot, "work", "locres");

    private static readonly Dictionary<string, LocresFile> VanillaCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object VanillaGate = new();

    public static void ResetVanillaCache()
    {
        lock (VanillaGate) VanillaCache.Clear();
    }

    /// <summary>
    /// Looks a key up in the culture's vanilla locres. Used for unchanged keys
    /// that a kit renders inline: the catalog intentionally contains only
    /// changed/new text, but the official translation still applies.
    /// </summary>
    public static bool TryVanillaTranslation(string culture, string key, string english, out string translation)
    {
        translation = "";
        LocresFile target;
        LocresFile native;
        lock (VanillaGate)
        {
            if (!VanillaCache.TryGetValue(culture, out target!))
            {
                target = LocresFile.Read(EnsureVanillaLocres(culture));
                VanillaCache[culture] = target;
            }
            if (!VanillaCache.TryGetValue("en-US", out native!))
            {
                native = LocresFile.Read(EnsureVanillaLocres("en-US"));
                VanillaCache["en-US"] = native;
            }
        }

        // Prefer the namespace whose English source matches. Duplicate key names
        // exist across vanilla namespaces, so a key-only scan can pick the wrong
        // translation.
        foreach (var (nsName, ns) in native.Namespaces)
        {
            if (!ns.Entries.TryGetValue(key, out var en) || !string.Equals(en.Translation, english, StringComparison.Ordinal))
                continue;
            if (target.Namespaces.TryGetValue(nsName, out var tns)
                && tns.Entries.TryGetValue(key, out var entry)
                && !string.IsNullOrEmpty(entry.Translation))
            {
                translation = entry.Translation;
                return true;
            }
        }

        foreach (var ns in target.Namespaces.Values)
            if (ns.Entries.TryGetValue(key, out var entry) && !string.IsNullOrEmpty(entry.Translation))
            {
                translation = entry.Translation;
                return true;
            }
        return false;
    }

    public static string SettingsPath => Path.Combine(EditorPaths.RepoRoot, "work", "language.json");

    public static string LoadLanguage()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return ModLocalization.English.Code;
            var json = JObject.Parse(File.ReadAllText(SettingsPath));
            var code = json.Value<string>("language") ?? ModLocalization.English.Code;
            return (ModLocalization.Find(code) ?? ModLocalization.English).Code;
        }
        catch
        {
            return ModLocalization.English.Code;
        }
    }

    public static void SaveLanguage(string code)
    {
        var language = ModLocalization.Find(code) ?? ModLocalization.English;
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath,
            new JObject { ["language"] = language.Code }.ToString(Newtonsoft.Json.Formatting.Indented) + "\n");
    }

    /// <summary>
    /// Extracts a culture's vanilla Game.locres from the game containers (cached
    /// under work/locres). Used both for catalog extraction and for merging.
    /// </summary>
    public static string EnsureVanillaLocres(string culture, Action<string>? log = null)
    {
        Directory.CreateDirectory(CacheDir);
        var cache = Path.Combine(CacheDir, culture + ".Game.locres");
        if (File.Exists(cache) && new FileInfo(cache).Length > 1024) return cache;

        // Standalone builds bundle repak and the CUE4Parse Oodle runtime. The
        // vanilla locres lives in the legacy pakchunk .pak, so repak can pull it
        // without mounting the game's IoStore containers. Fall back to
        // GameIndex for the full editor if repak is unavailable.
        if (TryExtractWithRepak(culture, cache, log)) return cache;

        using var index = new GameIndex();
        var match = index.AllFiles.FirstOrDefault(f =>
            f.EndsWith($"/{culture}/Game.locres", StringComparison.OrdinalIgnoreCase));
        if (match is null)
            throw new FileNotFoundException($"Game.locres for culture '{culture}' not found in the game containers.");

        var bytes = index.Provider.Files[match].Read();
        File.WriteAllBytes(cache, bytes);
        log?.Invoke($"[loc] extracted vanilla {culture}/Game.locres ({bytes.Length:N0} bytes)");
        return cache;
    }

    private static bool TryExtractWithRepak(string culture, string cache, Action<string>? log)
    {
        string repak;
        try { repak = EditorPaths.RepakExe; }
        catch { return false; }

        EditorPaths.MakeExecutable(repak);
        var paks = Directory.Exists(EditorPaths.PaksDir)
            ? Directory.EnumerateFiles(EditorPaths.PaksDir, "*.pak", SearchOption.TopDirectoryOnly)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

        string? chosenPak = null;
        string? chosenEntry = null;
        foreach (var pak in paks)
        {
            if (!RunText(repak, $"list \"{pak}\"", out var output)) continue;
            var entry = output.Split('\n')
                .Select(l => l.Trim().Trim('"'))
                .FirstOrDefault(l => l.EndsWith($"/Game/{culture}/Game.locres",
                    StringComparison.OrdinalIgnoreCase));
            if (entry is null) continue;
            chosenPak = pak;
            chosenEntry = entry;
            // Later paks in patch order win; keep scanning so a patched locres
            // is preferred over pakchunk0.
        }

        if (chosenPak is null || chosenEntry is null) return false;
        if (!RunBinary(repak, $"get \"{chosenPak}\" \"{chosenEntry}\"", out var bytes) || bytes.Length <= 1024)
            return false;

        File.WriteAllBytes(cache, bytes);
        log?.Invoke($"[loc] extracted vanilla {culture}/Game.locres from " +
                    $"{Path.GetFileName(chosenPak)} ({bytes.Length:N0} bytes)");
        return true;
    }

    private static bool RunText(string exe, string args, out string output)
    {
        output = "";
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            });
            if (p is null) return false;
            output = p.StandardOutput.ReadToEnd();
            _ = p.StandardError.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static bool RunBinary(string exe, string args, out byte[] output)
    {
        output = Array.Empty<byte>();
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (p is null) return false;
            using var ms = new MemoryStream();
            p.StandardOutput.BaseStream.CopyTo(ms);
            _ = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0) return false;
            output = ms.ToArray();
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Compares the staged English tables with the vanilla en-US locres and
    /// writes en/fr/pl/ru catalogs plus a review report. Only new or changed
    /// keys are included: unchanged keys keep their official translations.
    /// </summary>
    public static void BuildCatalogs(string modDir, string outputDir, Action<string>? log = null)
    {
        Directory.CreateDirectory(outputDir);
        var enLocres = LocresFile.Read(EnsureVanillaLocres("en-US", log));
        var references = new Dictionary<string, LocresFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in ModLocalization.Supported.Where(l => l.Code != "en"))
            references[language.Code] = LocresFile.Read(EnsureVanillaLocres(language.Culture, log));

        // Preserve translations from an existing catalog in the same output
        // directory when the English source has not changed.
        var oldEn = ReadFlatCatalog(Path.Combine(outputDir, "en.json"));
        var oldLangs = ModLocalization.Supported
            .Where(l => l.Code != "en")
            .ToDictionary(l => l.Code, l => ReadFlatCatalog(Path.Combine(outputDir, l.Code + ".json")));

        var en = new JObject { ["tables"] = new JObject(), ["inline"] = new JObject() };
        var langs = ModLocalization.Supported
            .Where(l => l.Code != "en")
            .ToDictionary(l => l.Code, _ => new JObject { ["tables"] = new JObject(), ["inline"] = new JObject() });
        var review = new JObject { ["tables"] = new JObject(), ["inline"] = new JObject() };
        var counts = new JObject();

        foreach (var table in Tables)
        {
            var stagedPath = Path.Combine(modDir, table.RelativePath);
            if (!File.Exists(stagedPath))
                throw new FileNotFoundException($"staged string table missing: {stagedPath}");
            var staged = DataTableEditor.OpenStringTable(stagedPath);
            enLocres.Namespaces.TryGetValue(table.Name, out var vanillaNs);

            var enTable = new JObject();
            var reviewTable = new JObject();
            var langTables = langs.ToDictionary(kv => kv.Key, _ => new JObject());
            var newCount = 0;
            var changedCount = 0;

            foreach (var (key, english) in staged)
            {
                var oldEnglish = vanillaNs?.Entries.TryGetValue(key, out var old) == true ? old.Translation : null;
                if (oldEnglish is not null && string.Equals(oldEnglish, english, StringComparison.Ordinal))
                    continue;

                var status = oldEnglish is null ? "new" : "changed";
                if (status == "new") newCount++;
                else changedCount++;

                enTable[key] = english;
                var item = new JObject
                {
                    ["status"] = status,
                    ["en"] = english,
                    ["oldEn"] = oldEnglish,
                    ["ref"] = new JObject(),
                };
                foreach (var (code, locres) in references)
                {
                    var reference = locres.Namespaces.TryGetValue(table.Name, out var ns)
                                    && ns.Entries.TryGetValue(key, out var entry)
                        ? entry.Translation
                        : "";
                    item["ref"]![code] = reference;
                    var composite = $"{table.Name}\0{key}";
                    var keep = oldEn.TryGetValue(composite, out var oldEnValue)
                               && string.Equals(oldEnValue, english, StringComparison.Ordinal)
                               && oldLangs.TryGetValue(code, out var oldLang)
                               && oldLang.TryGetValue(composite, out var kept)
                               && !string.IsNullOrEmpty(kept);
                    langTables[code][key] = keep ? oldLangs[code][composite] : reference;
                }
                reviewTable[key] = item;
            }

            ((JObject)en["tables"]!)[table.Name] = enTable;
            ((JObject)review["tables"]!)[table.Name] = reviewTable;
            foreach (var (code, obj) in langTables)
                ((JObject)langs[code]["tables"]!)[table.Name] = obj;
            counts[table.Name] = new JObject { ["total"] = newCount + changedCount, ["new"] = newCount, ["changed"] = changedCount };
        }

        foreach (var (key, english) in InlineSource)
        {
            ((JObject)en["inline"]!)[key] = english;
            var composite = "inline\0" + key;
            ((JObject)review["inline"]!)[key] = new JObject
            {
                ["status"] = "new",
                ["en"] = english,
                ["oldEn"] = null,
                ["ref"] = new JObject(),
            };
            foreach (var (code, obj) in langs)
            {
                var keep = oldEn.TryGetValue(composite, out var oldEnValue)
                           && string.Equals(oldEnValue, english, StringComparison.Ordinal)
                           && oldLangs.TryGetValue(code, out var oldLang)
                           && oldLang.TryGetValue(composite, out var kept)
                           && !string.IsNullOrEmpty(kept);
                ((JObject)obj["inline"]!)[key] = keep ? oldLangs[code][composite] : "";
            }
        }
        counts["inline"] = new JObject { ["total"] = InlineSource.Count, ["new"] = InlineSource.Count, ["changed"] = 0 };

        WriteJson(Path.Combine(outputDir, "en.json"), en);
        foreach (var (code, obj) in langs) WriteJson(Path.Combine(outputDir, code + ".json"), obj);
        var reportDir = Path.Combine(EditorPaths.RepoRoot, "work", "localization");
        Directory.CreateDirectory(reportDir);
        WriteJson(Path.Combine(reportDir, "review.json"), review);
        WriteJson(Path.Combine(reportDir, "counts.json"), counts);

        log?.Invoke("[loc] " + string.Join(", ",
            counts.Properties().Select(p => $"{p.Name}={p.Value!["total"]}")));
    }

    /// <summary>Writes the translated Game.locres into the staged mod tree.</summary>
    public static string Apply(string modDir, string? language, Action<string>? log = null)
    {
        var lang = ModLocalization.Find(language) ?? ModLocalization.English;
        if (lang.Code == "en") return "";

        var en = ModLocalization.TableValues("en");
        AssertCatalogCoversStage(modDir, en);
        var extra = FindCatalogKeysMissingFromStage(modDir, en);
        if (extra.Count > 0)
            log?.Invoke($"[loc] warning: {extra.Count} catalog key(s) are not in this build " +
                        $"(feature off?); first: {string.Join(", ", extra.Take(8)).Replace('\0', '/')}");
        var translated = ModLocalization.TableValues(lang.Code);
        var locres = LocresFile.Read(EnsureVanillaLocres(lang.Culture, log));
        var missing = new List<string>();
        var applied = 0;

        foreach (var (composite, english) in en)
        {
            var split = composite.IndexOf('\0');
            if (split < 0) continue;
            var table = composite[..split];
            var key = composite[(split + 1)..];
            var text = translated.TryGetValue(composite, out var value) ? value : "";
            if (string.IsNullOrWhiteSpace(text))
            {
                missing.Add($"{table}/{key}");
                continue;
            }
            // Existing vanilla keys carry the original source hash in their
            // FText history. The engine rejects a translation whose hash no
            // longer matches and falls back to the source text. Keep the
            // vanilla hash for existing keys; only truly new keys get the new
            // English-source hash.
            uint sourceHash = locres.Namespaces.TryGetValue(table, out var tableNs)
                && tableNs.Entries.TryGetValue(key, out var existing)
                    ? existing.SourceHash
                    : LocresHash.SourceHash(english);
            locres.Set(table, key, text, sourceHash);
            applied++;
        }

        if (missing.Count > 0)
            throw new InvalidDataException(
                $"[loc] {lang.DisplayName}: {missing.Count} untranslated key(s); first: " +
                string.Join(", ", missing.Take(12)));

        var helperMissing = ModLocalization.MissingKeys().ToList();
        if (helperMissing.Count > 0)
            throw new InvalidDataException(
                $"[loc] {lang.DisplayName}: {helperMissing.Count} inline/helper key(s) missing from the catalog; first: " +
                string.Join(", ", helperMissing.Take(12)));

        var dest = Path.Combine(modDir, "Sandfall", "Content", "Localization", "Game", lang.Culture, "Game.locres");
        locres.Write(dest);
        log?.Invoke($"[loc] {lang.DisplayName}: merged {applied} entries into {lang.Culture}/Game.locres " +
                    $"({new FileInfo(dest).Length:N0} bytes)");
        return dest;
    }

    /// <summary>
    /// Overwrites the staged StringTable source text with the selected
    /// translation. A pack ships one language only, so this is safe, and it
    /// gives the game a translated fallback when a base locres wins a
    /// duplicate vanilla key or a source-hash check rejects the locres entry.
    /// </summary>
    public static int InlineStagedTables(string modDir, string? language, Action<string>? log = null)
    {
        var lang = ModLocalization.Find(language) ?? ModLocalization.English;
        if (lang.Code == ModLocalization.English.Code) return 0;
        var values = ModLocalization.TableValues(lang.Code);
        int changed = 0;
        foreach (var table in Tables)
        {
            var path = Path.Combine(modDir, table.RelativePath);
            if (!File.Exists(path)) continue;
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            var prefix = table.Name + "\0";
            foreach (var (composite, text) in values)
            {
                if (!composite.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var key = composite[prefix.Length..];
                if (!string.IsNullOrWhiteSpace(text)) entries[key] = text;
            }
            if (entries.Count == 0) continue;
            DataTableEditor.AddStringTableEntries(path, entries);
            changed += entries.Count;
        }
        if (changed > 0)
            log?.Invoke($"[loc] inlined {changed} translated string-table source entries");
        return changed;
    }

    private static readonly HashSet<string> AllowUnchangedInline = new(StringComparer.Ordinal)
    {
        "BUFF_UnoPuncho_Name",
        "BUFF_SuperSonic_Name",
        "SKILL_SuperSpark_Name",
    };

    /// <summary>
    /// Fails when the stage contains a new/changed table key that the English
    /// catalog does not know about. That means the build changed text without
    /// re-running loc-catalog; shipping would silently fall back to English.
    /// </summary>
    private static void AssertCatalogCoversStage(string modDir, IReadOnlyDictionary<string, string> en)
    {
        var vanilla = LocresFile.Read(EnsureVanillaLocres("en-US"));
        var missing = new List<string>();
        var stale = new List<string>();
        foreach (var table in Tables)
        {
            var path = Path.Combine(modDir, table.RelativePath);
            if (!File.Exists(path)) continue;
            var staged = DataTableEditor.OpenStringTable(path);
            vanilla.Namespaces.TryGetValue(table.Name, out var ns);
            foreach (var (key, english) in staged)
            {
                var old = ns?.Entries.TryGetValue(key, out var entry) == true ? entry.Translation : null;
                if (old is not null && string.Equals(old, english, StringComparison.Ordinal)) continue;
                var composite = $"{table.Name}\0{key}";
                if (!en.TryGetValue(composite, out var catalogEnglish))
                    missing.Add($"{table.Name}/{key}");
                else if (!string.Equals(catalogEnglish, english, StringComparison.Ordinal))
                    stale.Add($"{table.Name}/{key}");
            }
        }
        if (missing.Count > 0)
            throw new InvalidDataException(
                "[loc] the staged mod has new/changed keys missing from the English catalog; " +
                "run `loc-catalog` and translate them first. First: " +
                string.Join(", ", missing.Take(10)));
        if (stale.Count > 0)
            throw new InvalidDataException(
                "[loc] the staged English text changed since the catalog was generated; " +
                "run `loc-catalog` and review the updated entries. First: " +
                string.Join(", ", stale.Take(10)));
    }

    private static List<string> FindCatalogKeysMissingFromStage(
        string modDir, IReadOnlyDictionary<string, string> en)
    {
        var staged = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in Tables)
        {
            var path = Path.Combine(modDir, table.RelativePath);
            if (!File.Exists(path)) continue;
            foreach (var (key, _) in DataTableEditor.OpenStringTable(path))
                staged.Add($"{table.Name}\0{key}");
        }
        return en.Keys.Where(k => !staged.Contains(k)).ToList();
    }

    /// <summary>Structural checks: coverage, markup IDs, placeholders, numbers, newlines.</summary>
    public static IReadOnlyList<string> Validate(string? language)
    {
        var lang = ModLocalization.Find(language) ?? ModLocalization.English;
        if (lang.Code == "en") return Array.Empty<string>();

        var issues = new List<string>();
        var en = ModLocalization.TableValues("en");
        var translated = ModLocalization.TableValues(lang.Code);
        foreach (var (composite, english) in en)
        {
            var text = translated.TryGetValue(composite, out var value) ? value : "";
            if (string.IsNullOrWhiteSpace(text))
            {
                issues.Add($"{composite.Replace('\0', '/')}: missing");
                continue;
            }
            Compare(composite.Replace('\0', '/'), english, text, issues);
        }

        foreach (var (key, english) in InlineSource)
        {
            var text = ModLocalization.Inline(key, english);
            if (string.Equals(text, english, StringComparison.Ordinal) && lang.Code != "en"
                && !AllowUnchangedInline.Contains(key))
                issues.Add($"inline:{key}: unchanged");
            Compare("inline:" + key, english, text, issues);
        }
        return issues;
    }

    private static void Compare(string id, string english, string translation, List<string> issues)
    {
        var enTags = MarkupIds(english);
        var trTags = MarkupIds(translation);
        if (!enTags.SequenceEqual(trTags))
            issues.Add($"{id}: markup IDs differ [{string.Join(",", enTags)}] vs [{string.Join(",", trTags)}]");

        var enPlaceholders = Placeholders(english);
        var trPlaceholders = Placeholders(translation);
        if (!enPlaceholders.SequenceEqual(trPlaceholders))
            issues.Add($"{id}: placeholders differ [{string.Join(",", enPlaceholders)}] vs [{string.Join(",", trPlaceholders)}]");

        var enNumbers = Numbers(english);
        var trNumbers = Numbers(translation);
        if (!enNumbers.SequenceEqual(trNumbers))
            issues.Add($"{id}: numbers differ [{string.Join(",", enNumbers)}] vs [{string.Join(",", trNumbers)}]");

        var enLines = english.Count(c => c == '\n');
        var trLines = translation.Count(c => c == '\n');
        if (enLines != trLines)
            issues.Add($"{id}: line breaks {enLines} vs {trLines}");
    }

    private static List<string> MarkupIds(string value) =>
        Regex.Matches(value, "id=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();

    private static List<string> Placeholders(string value) =>
        Regex.Matches(value, "\\{[^{}]+\\}").Select(m => m.Value).OrderBy(x => x, StringComparer.Ordinal).ToList();

    private static List<string> Numbers(string value) =>
        Regex.Matches(value, "\\d+(?:[.,]\\d+)?")
            .Select(m => m.Value.Replace(',', '.'))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

    private static Dictionary<string, string> ReadFlatCatalog(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path)) return result;
        try
        {
            var root = JObject.Parse(File.ReadAllText(path));
            if (root["tables"] is JObject tables)
                foreach (var table in tables.Properties())
                    if (table.Value is JObject entries)
                        foreach (var entry in entries.Properties())
                            result[$"{table.Name}\0{entry.Name}"] = entry.Value.Value<string>() ?? "";
            if (root["inline"] is JObject inline)
                foreach (var entry in inline.Properties())
                    result["inline\0" + entry.Name] = entry.Value.Value<string>() ?? "";
        }
        catch
        {
            // A corrupt/partial catalog should not block regeneration.
        }
        return result;
    }

    private static void WriteJson(string path, JObject obj) =>
        File.WriteAllText(path, obj.ToString(Newtonsoft.Json.Formatting.Indented) + "\n", new UTF8Encoding(false));
}

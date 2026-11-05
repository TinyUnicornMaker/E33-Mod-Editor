using System.Globalization;
using System.Text.RegularExpressions;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace E33ModEditor.Core;

/// <summary>
/// Loads a UE5 DataTable asset (.uasset/.uexp) via UAssetAPI, exposes its rows
/// as friendly JSON (UserDefinedStruct "_N_GUID" suffixes stripped), applies
/// dot-separated path edits (structs, arrays, maps), and writes the asset back.
/// </summary>
public sealed partial class DataTableEditor
{
    [GeneratedRegex(@"^(?<base>.+?)_\d+_[0-9A-Fa-f]{32}$")]
    private static partial Regex SuffixRegex();

    public UAsset Asset { get; }
    public DataTableExport Table { get; }

    /// <summary>A scalar leaf of a row, described for UI binding.</summary>
    public sealed record PropertyLeaf(string Path, string Kind, Newtonsoft.Json.Linq.JToken Value);

    private readonly Dictionary<string, string> _enumTypeByPath = new();

    /// <summary>The enum type name (e.g. "E_DamageType") for a flattened leaf path, if any.</summary>
    public string? EnumTypeFor(string leafPath) =>
        _enumTypeByPath.TryGetValue(leafPath, out var t) ? t : null;

    /// <summary>Walks the first row so enum-type lookups work before any explicit flatten.</summary>
    public void PrimeEnumPaths()
    {
        var first = Table.Table.Data.FirstOrDefault();
        if (first != null) FlattenRow(first);
    }


    private readonly NormalExport? _singleExport;

    /// <summary>
    /// Schema patches for fields added by game patches after the mappings file
    /// was generated: struct name -> (schema index, guessed name, property type).
    /// </summary>
    public static readonly Dictionary<string, List<(int Index, string Name, string Type)>> SchemaPatches =
        new()
        {
            // Patch 1.5.x moved IsRetryableBattle to schema index 6.
            ["S_jRPG_Encounter"] = new List<(int, string, string)>
            {
                (6, "IsRetryableBattle", "BoolProperty"),
            },
            // Older V1.2.1/V1.2.2 weapon tables carry the fifth passive slot
            // in the cooked schema, but the shared mappings predate it.
            ["S_WeaponDefinition"] = new List<(int, string, string)>
            {
                (12, "Level5Lumina", "NameProperty"),
            },
        };

    /// <summary>Older mappings that contain struct schemas missing from newer dumps.</summary>
    private const string LegacyMappingsPath = "MappingsLegacy.usmap";

    private static void ApplySchemaPatches(UAssetAPI.Unversioned.Usmap? map)
    {
        if (map is null) return;

        // Merge schemas from the legacy mappings file that the primary dump lacks.
        var legacyPath = Path.Combine(Path.GetDirectoryName(map.FilePath) ?? ".", LegacyMappingsPath);
        if (File.Exists(legacyPath))
        {
            try
            {
                var legacy = new UAssetAPI.Unversioned.Usmap(legacyPath);
                int merged = 0, enums = 0;
                foreach (var (name, schema) in legacy.Schemas)
                    if (!map.Schemas.ContainsKey(name))
                    {
                        map.Schemas[name] = schema;
                        merged++;
                    }
                foreach (var (name, e) in legacy.EnumMap)
                    if (!map.EnumMap.ContainsKey(name))
                    {
                        map.EnumMap[name] = e;
                        enums++;
                    }
                Console.Error.WriteLine($"[E33ModEditor] merged {merged} legacy schema(s), {enums} enum(s)");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[E33ModEditor] legacy mappings merge failed: {ex.Message}");
            }
        }

        foreach (var (structName, patches) in SchemaPatches)
        {
            if (!map.Schemas.TryGetValue(structName, out var schema)) continue;
            foreach (var (idx, name, type) in patches)
            {
                if (schema.Properties.ContainsKey(idx)) continue;
                var propType = Enum.Parse<UAssetAPI.Unversioned.UsmapPropertyType>(type);
                schema.AddOrReplaceProperty(name, idx, new UAssetAPI.Unversioned.UsmapPropertyData(propType));
            }
        }
    }

    private DataTableEditor(UAsset asset, NormalExport? singleExport)
    {
        Asset = asset;
        Table = null!;
        _singleExport = singleExport;
    }

    private static Usmap? _cachedMap;

    /// <summary>The merged (primary + legacy) usmap, loaded once per process.</summary>
    public static Usmap? Mappings => _cachedMap ??= LoadMappings();
    /// <summary>
    /// Drops the cached merged usmap; the next Mappings access reloads from
    /// disk. UAsset parsing MUTATES the shared map (derived struct/enum
    /// schemas are written back into it, see UAsset.cs), so a long-lived
    /// process that opens a broken asset poisons every later parse. Pack runs
    /// must start from pristine schemas.
    /// </summary>
    public static void ResetMappings() => _cachedMap = null;

    /// <summary>Drops cached enum display names (after a modded enum asset changes).</summary>
    public static void ResetEnumCache()
    {
        lock (_enumGate) _enumAssetCache.Clear();
    }


    private static Usmap? LoadMappings()
    {
        var mappingsPath = EditorPaths.Mappings;
        if (mappingsPath.Length == 0) return null;
        var map = new Usmap(mappingsPath);
        ApplySchemaPatches(map);
        return map;
    }

    /// <summary>
    /// Resolves an enum type's ordered value list ("NewEnumeratorN" -> real name) from the merged mappings.
    /// </summary>
    public static IReadOnlyList<string>? EnumValues(string typeName)
    {
        var m = Mappings;
        if (m == null || typeName.Length == 0) return null;
        if (!m.EnumMap.TryGetValue(typeName, out var e))
        {
            var hit = m.EnumMap.FirstOrDefault(k =>
                string.Equals(k.Key, typeName, StringComparison.OrdinalIgnoreCase));
            if (hit.Value is null) return null;
            e = hit.Value;
        }
        return e.Values.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
    }

    private static readonly Dictionary<string, string[]?> _enumAssetCache = new();
    private static readonly object _enumGate = new();

    /// <summary>
    /// Upper bound on how many assets an enum-name extraction may match. Every
    /// real enum name matches exactly one asset, so this only ever rejects an
    /// over-broad filter (see <see cref="Retoc.CountMatchingAssets"/>).
    /// </summary>
    public const int MaxEnumExtractMatches = 16;

    /// <summary>
    /// Resolves an enum type's display names from its cooked UEnum asset
    /// (DisplayNameMap), extracting it from the game containers on first use.
    /// Returns null when no asset exists for the type.
    /// </summary>
    public static IReadOnlyList<string>? EnumDisplayNames(string typeName)
    {
        if (typeName.Length == 0) return null;
        lock (_enumGate)
        {
            if (_enumAssetCache.TryGetValue(typeName, out var cached)) return cached;
            string[]? names = null;
            try
            {
                var dir = Path.Combine(EditorPaths.RepoRoot, "work", "enums");
                if (!Directory.EnumerateFiles(dir, $"{typeName}.uasset", SearchOption.AllDirectories).Any())
                {
                    // The extraction filter is an unanchored name substring, so a
                    // short or generic enum type name would dump a huge slice of
                    // the game here (this put 32 GB in work/enums once). A real
                    // enum name matches exactly one asset; refuse anything above
                    // the cap instead of extracting.
                    var matches = new Retoc().CountMatchingAssets(typeName);
                    if (matches is >= 0 and <= MaxEnumExtractMatches)
                        new Retoc().ToLegacy(dir, $"\"{typeName}\"");
                    else
                        Console.WriteLine(
                            $"[enums] '{typeName}': filter matches {matches} assets — refusing to extract");
                }
                var file = Directory
                    .EnumerateFiles(dir, $"{typeName}.uasset", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (file != null)
                {
                    var editor = OpenPropertyAsset(file);
                    var byIndex = new SortedDictionary<int, string>();
                    foreach (var leaf in editor.FlattenProperties())
                    {
                        // DisplayNameMap.NewEnumerator3 or DisplayNameMap.NewEnumerator3.DisplayNameMap
                        var parts = leaf.Path.Split('.');
                        if (parts.Length >= 2 && parts[0] == "DisplayNameMap" &&
                            parts[1].StartsWith("NewEnumerator", StringComparison.Ordinal))
                        {
                            var idx = EnumeratorIndex(parts[1]);
                            if (idx != null && leaf.Value is Newtonsoft.Json.Linq.JValue v)
                                byIndex[idx.Value] = v.ToString();
                        }
                    }
                    if (byIndex.Count > 0)
                    {
                        // pad so position == enumerator index even when the asset skips indices
                        var max = byIndex.Keys.Max();
                        var padded = new string[max + 1];
                        for (int i = 0; i <= max; i++)
                            padded[i] = byIndex.TryGetValue(i, out var n) ? n : "";
                        names = padded;
                    }
                }
            }
            catch { /* extraction or parse failure -> fall through to null */ }
            _enumAssetCache[typeName] = names;
            return names;
        }
    }

    private static string? ResolveDisplay(string? typeName, string enumeratorRaw)
    {
        var idx = EnumeratorIndex(enumeratorRaw);
        if (idx == null || typeName is not { Length: >0 }) return null;
        var names = EnumDisplayNames(typeName);
        if (names == null || idx >= names.Count) return null;
        var n = names[idx.Value];
        return n.Length > 0 ? n : $"#{idx}";
    }

    /// <summary>"EAttackType::NewEnumerator6" -> "Physical" (or the input unchanged).</summary>
    public static string StaticDecode(string encoded)
    {
        var split = encoded.Split("::");
        if (split.Length != 2) return encoded;
        return ResolveDisplay(split[0], split[1]) ?? encoded;
    }

    /// <summary>
    /// Decodes one leaf value for display: plain enums via their field's enum
    /// type, map values via the "EType::..." prefix they carry.
    /// </summary>
    public string DecodeLeaf(string leafPath, string raw)
    {
        var split = raw.Split("::");
        if (split.Length == 2)
            return ResolveDisplay(split[0], split[1]) ?? raw;

        var typeName = _enumTypeByPath.TryGetValue(leafPath, out var t) ? t : null;
        if (typeName != null && EnumeratorIndex(raw) is { } i)
        {
            var names = EnumDisplayNames(typeName);
            if (names != null)
                return i < names.Count && names[i].Length > 0 ? names[i] : $"#{i}";
        }
        return raw;
    }

    /// <summary>
    /// Converts a human-written value ("Physical", "#3", "C") back to the raw
    /// "NewEnumeratorN"/"EType::NewEnumeratorN" form. Unrecognised input passes through.
    /// </summary>
    public string ReverseDecode(string leafPath, string currentRaw, string input)
    {
        var trimmed = input.Trim();
        if (trimmed == currentRaw || trimmed.Length == 0) return input;

        string? typeName = null;
        string keepPrefix = "";
        if (currentRaw.Contains("::"))
        {
            keepPrefix = currentRaw[..(currentRaw.IndexOf("::") + 2)];
            typeName = currentRaw[..currentRaw.IndexOf("::")];
        }
        else _enumTypeByPath.TryGetValue(leafPath, out typeName);

        if (trimmed.StartsWith("NewEnumerator", StringComparison.Ordinal) && EnumeratorIndex(trimmed) != null)
            return $"{keepPrefix}{trimmed}";

        if (typeName == null) return input;
        var names = EnumDisplayNames(typeName);
        if (names == null) return input;

        // strip an optional "EType::" the user copied back in
        if (trimmed.Contains("::"))
        {
            var parts = trimmed.Split("::");
            if (string.Equals(parts[0], typeName, StringComparison.OrdinalIgnoreCase)) trimmed = parts[1];
        }

        if (trimmed.StartsWith("#") && int.TryParse(trimmed[1..], out var hash))
            return $"{keepPrefix}NewEnumerator{hash}";

        for (int i = 0; i < names.Count; i++)
            if (string.Equals(names[i], trimmed, StringComparison.OrdinalIgnoreCase))
                return $"{keepPrefix}NewEnumerator{i}";

        return input;
    }

    /// <summary>All enum-type paths discovered so far (leaf path -> enum type name).</summary>
    public IReadOnlyDictionary<string, string> EnumTypesByPath => _enumTypeByPath;

    /// <summary>"NewEnumerator6" -> 6; anything else returns null.</summary>
    public static int? EnumeratorIndex(string raw)
    {
        if (raw.StartsWith("NewEnumerator", StringComparison.Ordinal) &&
            int.TryParse(raw["NewEnumerator".Length..], out var i)) return i;
        return null;
    }

    /// <summary>Opens a DataTable asset.</summary>
    public DataTableEditor(string uassetPath)
    {
        var map = Mappings;
        Asset = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
        Table = Asset.Exports.OfType<DataTableExport>().FirstOrDefault()
            ?? throw new InvalidDataException(
                $"'{uassetPath}' does not contain a DataTable export. Exports: {string.Join(", ", Asset.Exports.Select(e => e.GetType().Name))}");
    }

    /// <summary>Opens any tagged-properties asset (e.g. weapon-picto DataAssets).</summary>
    public static DataTableEditor OpenPropertyAsset(string uassetPath)
    {
        var map = Mappings;
        var asset = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
        var export = asset.Exports.OfType<NormalExport>().FirstOrDefault()
            ?? throw new InvalidDataException(
                $"'{uassetPath}' has no editable export. Exports: {string.Join(", ", asset.Exports.Select(e => e.GetType().Name))}");
        return new DataTableEditor(asset, export);
    }

    /// <summary>Opens a StringTable asset and returns its key -> text map.</summary>
    public static SortedDictionary<string, string> OpenStringTable(string uassetPath)
    {
        var map = Mappings;
        var asset = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
        var export = asset.Exports.OfType<StringTableExport>().FirstOrDefault()
            ?? throw new InvalidDataException(
                $"'{uassetPath}' has no StringTable export. Exports: {string.Join(", ", asset.Exports.Select(e => e.GetType().Name))}");
        var result = new SortedDictionary<string, string>();
        foreach (var kv in export.Table.Keys)
            result[kv?.Value ?? ""] = export.Table[kv]?.Value ?? "";
        return result;
    }

    /// <summary>Flattens the properties of a property-only asset.</summary>
    public IReadOnlyList<PropertyLeaf> FlattenProperties()
    {
        var export = _singleExport ?? throw new InvalidOperationException("This asset was not opened as a property asset.");
        var leaves = new List<PropertyLeaf>();
        foreach (var f in export.Data)
            Walk(f, "", leaves);
        return leaves;
    }

    /// <summary>
    /// Inserts a missing top-level int prop into a property-only asset (e.g. a
    /// skill DA that ships without APCost). Anchored after SkillType when
    /// present to mimic the vanilla layout, else appended. Ancestry is not
    /// serialized, so a fresh prop writes cleanly.
    /// </summary>
    public static void EnsureIntProperty(string uassetPath, string propName, int value)
    {
        var map = Mappings;
        var asset = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
        var export = asset.Exports.OfType<NormalExport>().FirstOrDefault()
            ?? throw new InvalidDataException($"'{uassetPath}' has no editable export.");
        var prop = new IntPropertyData(FName.FromString(asset, propName)) { Value = value };
        int idx = export.Data.FindIndex(p => p.Name.ToString() == "SkillType");
        if (idx >= 0) export.Data.Insert(idx + 1, prop);
        else export.Data.Add(prop);
        asset.Write(uassetPath);
    }

    /// <summary>Applies dotted-path edits to a property-only asset.</summary>
    public int ApplyToProperties(string editsJson)
    {
        var export = _singleExport ?? throw new InvalidOperationException("This asset was not opened as a property asset.");
        var edits = Newtonsoft.Json.Linq.JObject.Parse(editsJson);
        int changes = 0;
        foreach (var (path, value) in edits)
        {
            if (value is null) continue;
            ApplyPath(export.Data, path.Split('.'), value);
            changes++;
        }
        return changes;
    }

    public IReadOnlyList<string> RowNames => Table.Table.Data.Select(r => CleanName(r.Name)).ToList();

    /// <summary>Finds a row by its clean (suffix-stripped) name.</summary>
    public bool TryGetRow(string cleanName, out UAssetAPI.PropertyTypes.Structs.StructPropertyData row)
    {
        row = Table.Table.Data.FirstOrDefault(r => CleanName(r.Name) == cleanName)!;
        return row is not null;
    }

    /// <summary>Dumps every row to JSON: { "RowName": { "Field": value, ... }, ... }</summary>
    public string Dump()
    {
        var root = new Newtonsoft.Json.Linq.JObject();
        foreach (var row in Table.Table.Data)
            root[CleanName(row.Name)] = DumpRow(row);
        return root.ToString(Newtonsoft.Json.Formatting.Indented);
    }

    /// <summary>Dumps a single row as a JSON object of its fields.</summary>
    public Newtonsoft.Json.Linq.JObject DumpRow(UAssetAPI.PropertyTypes.Structs.StructPropertyData row)
    {
        var o = new Newtonsoft.Json.Linq.JObject();
        foreach (var f in row.Value)
            o[CleanName(f.Name)] = ToJson(f);
        return o;
    }

    /// <summary>
    /// Flattens a row into typed scalar leaves with dot-separated paths,
    /// e.g. "EnemyStats.E_jRPG_StatType::NewEnumerator1". Non-scalar leaves
    /// (object references, localized text) are included with their kind so the
    /// UI can show them read-only.
    /// </summary>
    public IReadOnlyList<PropertyLeaf> FlattenRow(UAssetAPI.PropertyTypes.Structs.StructPropertyData row)
    {
        var leaves = new List<PropertyLeaf>();
        foreach (var f in row.Value)
            Walk(f, "", leaves);
        return leaves;
    }

    private void Walk(PropertyData p, string prefix, List<PropertyLeaf> leaves)
    {
        var path = prefix.Length == 0 ? CleanName(p.Name) : $"{prefix}.{CleanName(p.Name)}";
        WalkNode(p, path, leaves);
    }

    /// <summary>
    /// Walks a map/array value at an already-resolved path so the value's own
    /// property name (often a repeat of the container) is not appended again.
    /// </summary>
    private void WalkAt(PropertyData p, string path, List<PropertyLeaf> leaves) =>
        WalkNode(p, path, leaves);

    private void WalkNode(PropertyData p, string path, List<PropertyLeaf> leaves)
    {
        switch (p)
        {
            case BoolPropertyData v: leaves.Add(new PropertyLeaf(path, "bool", v.Value)); break;
            case Int8PropertyData v: leaves.Add(new PropertyLeaf(path, "int", v.Value)); break;
            case Int16PropertyData v: leaves.Add(new PropertyLeaf(path, "int", v.Value)); break;
            case IntPropertyData v: leaves.Add(new PropertyLeaf(path, "int", v.Value)); break;
            case Int64PropertyData v: leaves.Add(new PropertyLeaf(path, "int", v.Value)); break;
            case UInt16PropertyData v: leaves.Add(new PropertyLeaf(path, "uint", v.Value)); break;
            case UInt32PropertyData v: leaves.Add(new PropertyLeaf(path, "uint", v.Value)); break;
            case UInt64PropertyData v: leaves.Add(new PropertyLeaf(path, "uint", v.Value)); break;
            case FloatPropertyData v: leaves.Add(new PropertyLeaf(path, "float", v.Value)); break;
            case DoublePropertyData v: leaves.Add(new PropertyLeaf(path, "float", v.Value)); break;
            case BytePropertyData v when IsEnumByte(v):
                var btxt = v.EnumValue?.Value?.Value;
                if (!string.IsNullOrEmpty(btxt))
                {
                    _enumTypeByPath[path] = btxt!;
                    leaves.Add(new PropertyLeaf(path, "enum", btxt));
                }
                else leaves.Add(new PropertyLeaf(path, "enum", $"byte:{v.Value}"));
                break;
            case BytePropertyData v: leaves.Add(new PropertyLeaf(path, "uint", (int)v.Value)); break;
            case EnumPropertyData v:
                if (v.EnumType?.Value?.Value is { Length: >0 } et) _enumTypeByPath[path] = et;
                leaves.Add(new PropertyLeaf(path, "enum", v.Value?.Value?.Value ?? "")); break;
            case NamePropertyData v: leaves.Add(new PropertyLeaf(path, "name", v.Value?.Value?.Value ?? "")); break;
            case StrPropertyData v: leaves.Add(new PropertyLeaf(path, "str", v.Value?.Value ?? "")); break;
            case Utf8StrPropertyData v: leaves.Add(new PropertyLeaf(path, "str", v.Value?.Value ?? "")); break;
            case ObjectPropertyData: leaves.Add(new PropertyLeaf(path, "object", ToJson(p))); break;
            case SoftObjectPropertyData: leaves.Add(new PropertyLeaf(path, "softobject", ToJson(p))); break;
            case UAssetAPI.PropertyTypes.Objects.TextPropertyData t:
                leaves.Add(new PropertyLeaf(path, "text", TextPreview(t))); break;
            case DelegatePropertyData: leaves.Add(new PropertyLeaf(path, "delegate", "<delegate>")); break;
            case StructPropertyData s:
                foreach (var f in s.Value) Walk(f, path, leaves);
                break;
            case ArrayPropertyData a:
                for (int i = 0; i < a.Value.Length; i++)
                    WalkAt(a.Value[i], $"{path}.{i}", leaves);
                break;
            case MapPropertyData m:
            {
                int i = 0;
                foreach (var (k, v) in m.Value)
                {
                    var keyStr = KeyToString(k) ?? $"[{i}]";
                    WalkAt(v, $"{path}.{keyStr}", leaves);
                    i++;
                }
                break;
            }
            default:
                leaves.Add(new PropertyLeaf(path, p.GetType().Name, ToJson(p)));
                break;
        }
    }

    private static string? KeyToString(PropertyData k) => k switch
    {
        NamePropertyData n => n.Value?.Value?.Value,
        StrPropertyData s => s.Value?.Value,
        EnumPropertyData e => e.Value?.Value?.Value,
        BytePropertyData b => b.EnumValue?.Value?.Value,
        IntPropertyData i => $"[{i.Value}]",
        Int64PropertyData i64 => $"[{i64.Value}]",
        UInt32PropertyData u => $"[{u.Value}]",
        StructPropertyData se => se.Name.ToString(),
        _ => null,
    };

    private static string TextPreview(UAssetAPI.PropertyTypes.Objects.TextPropertyData t)
    {
        try
        {
            var table = t.TableId?.Value?.Value;
            if (!string.IsNullOrEmpty(table)) return $"[{table}] {t.Value?.Value}";
            if (!string.IsNullOrEmpty(t.CultureInvariantString?.Value)) return t.CultureInvariantString.Value;
            return $"<{t.HistoryType}>";
        }
        catch { return "<text>"; }
    }

    /// <summary>
    /// Applies edits: { "RowName": { "dotted.path": value, ... } }. Paths cross
    /// structs, array indices and map keys; leaf values are converted to the
    /// existing property's type. Returns count of changed fields.
    /// </summary>
    public int Apply(string editsJson)
    {
        var edits = Newtonsoft.Json.Linq.JObject.Parse(editsJson);
        int changes = 0;
        foreach (var (rowName, rowEdits) in edits)
        {
            var row = Table.Table.Data.FirstOrDefault(r => CleanName(r.Name) == rowName)
                ?? throw new KeyNotFoundException($"Unknown row '{rowName}'.");
            if (rowEdits is not Newtonsoft.Json.Linq.JObject fields)
                throw new InvalidDataException($"Edits for row '{rowName}' must be an object.");
            foreach (var (path, value) in fields)
            {
                if (value is null) continue;
                ApplyPath(row.Value, path.Split('.'), value);
                changes++;
            }
        }
        return changes;
    }

    private void ApplyPath(List<PropertyData> container, string[] segs, Newtonsoft.Json.Linq.JToken value)
    {
        var prop = container.FirstOrDefault(p => CleanName(p.Name) == segs[0])
            ?? throw new KeyNotFoundException(
                $"Unknown field '{segs[0]}'. Available: {string.Join(", ", container.Select(c => CleanName(c.Name)).Distinct())}");

        for (int i = 1; i < segs.Length; i++)
        {
            var seg = segs[i];
            switch (prop)
            {
                case StructPropertyData st:
                    prop = st.Value.FirstOrDefault(c => CleanName(c.Name) == seg)
                        ?? throw new KeyNotFoundException($"'{string.Join(".", segs[..i])}' has no field '{seg}'.");
                    break;
                case ArrayPropertyData arr when int.TryParse(seg, out var idx):
                    prop = idx >= 0 && idx < arr.Value.Length
                        ? arr.Value[idx]
                        : throw new IndexOutOfRangeException($"{string.Join(".", segs[..i])} index {idx} out of range.");
                    break;
                case MapPropertyData mp:
                {
                    var existing = mp.Value.FirstOrDefault(kv => KeyToString(kv.Key) == seg).Value;
                    if (existing != null)
                    {
                        prop = existing;
                        break;
                    }
                    if (i != segs.Length - 1)
                        throw new KeyNotFoundException(
                            $"'{string.Join(".", segs[..i])}' has no key '{seg}'. Keys: {string.Join(", ", mp.Value.Keys.Select(KeyToString).Take(20))}");
                    var (keyTemplate, valTemplate) = MapTemplates(mp);
                    var key = CloneKeyed(keyTemplate, seg);
                    var val = CloneKeyed(valTemplate, value.ToString());
                    mp.Value.Add(key, val);
                    prop = val;
                    break;
                }
                default:
                    throw new InvalidDataException($"'{string.Join(".", segs[..i])}' is not a container (cannot descend into '{seg}').");
            }
        }

        if (prop is MapPropertyData mapToReplace && value is Newtonsoft.Json.Linq.JObject jo)
        {
            ReplaceMapEntries(mapToReplace, jo);
            return;
        }
        if (prop is ArrayPropertyData arrayToReplace && value is Newtonsoft.Json.Linq.JArray ja)
        {
            ReplaceArrayEntries(arrayToReplace, ja);
            return;
        }

        ApplyScalar(prop, value);
    }

    /// <summary>
    /// Rebuilds a map from a JSON object of key → value strings, cloning the
    /// existing key/value property types so new entries can be added.
    /// </summary>
    private void ReplaceMapEntries(MapPropertyData mp, Newtonsoft.Json.Linq.JObject entries)
    {
        var (keyTemplate, valTemplate) = MapTemplates(mp);
        mp.Value.Clear();
        foreach (var p in entries.Properties())
        {
            var key = CloneKeyed(keyTemplate, p.Name);
            var val = CloneKeyed(valTemplate, p.Value?.ToString() ?? "");
            mp.Value.Add(key, val);
        }
    }

    private (PropertyData Key, PropertyData Value) MapTemplates(MapPropertyData mp)
    {
        foreach (var kv in mp.Value)
            return (kv.Key, kv.Value);
        if (Table?.Table?.Data != null)
        {
            foreach (var row in Table.Table.Data)
            {
                if (row.Value.FirstOrDefault(p => CleanName(p.Name) == CleanName(mp.Name)) is not MapPropertyData sibling)
                    continue;
                foreach (var kv in sibling.Value)
                    return (kv.Key, kv.Value);
            }
        }
        throw new InvalidDataException($"Map '{CleanName(mp.Name)}' is empty and no sibling row has entries to clone.");
    }

    private void ReplaceArrayEntries(ArrayPropertyData arr, Newtonsoft.Json.Linq.JArray entries)
    {
        PropertyData? template = arr.Value.FirstOrDefault();
        if (template is null && Table?.Table?.Data != null)
        {
            foreach (var row in Table.Table.Data)
            {
                if (row.Value.FirstOrDefault(p => CleanName(p.Name) == CleanName(arr.Name)) is not ArrayPropertyData sibling)
                    continue;
                template = sibling.Value.FirstOrDefault();
                if (template != null) break;
            }
        }
        if (template is null)
            throw new InvalidDataException($"Array '{CleanName(arr.Name)}' is empty and no sibling row has entries to clone.");

        var list = new List<PropertyData>(entries.Count);
        foreach (var e in entries)
            list.Add(CloneKeyed(template, e?.ToString() ?? ""));
        arr.Value = list.ToArray();
    }

    private PropertyData CloneKeyed(PropertyData src, string newValue)
    {
        switch (src)
        {
            case EnumPropertyData e:
            {
                var n = new EnumPropertyData(e.Name)
                {
                    EnumType = e.EnumType,
                    InnerType = e.InnerType,
                    Value = FNameFrom(newValue),
                    PropertyTypeName = e.PropertyTypeName,
                };
                n.Ancestry.Initialize(e.Ancestry, e.Name);
                return n;
            }
            case BytePropertyData b:
            {
                var n = new BytePropertyData(b.Name)
                {
                    ByteType = b.ByteType,
                    EnumType = b.EnumType,
                    EnumValue = FNameFrom(newValue),
                    PropertyTypeName = b.PropertyTypeName,
                };
                n.Ancestry.Initialize(b.Ancestry, b.Name);
                return n;
            }
            case NamePropertyData nm:
            {
                var n = new NamePropertyData(nm.Name) { Value = FNameFrom(newValue) };
                n.Ancestry.Initialize(nm.Ancestry, nm.Name);
                return n;
            }
            case FloatPropertyData f:
            {
                float.TryParse(newValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed);
                var n = new FloatPropertyData(f.Name)
                {
                    Value = parsed,
                    PropertyTypeName = f.PropertyTypeName,
                };
                n.Ancestry.Initialize(f.Ancestry, f.Name);
                return n;
            }
            case DoublePropertyData d:
            {
                double.TryParse(newValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed);
                var n = new DoublePropertyData(d.Name)
                {
                    Value = parsed,
                    PropertyTypeName = d.PropertyTypeName,
                };
                n.Ancestry.Initialize(d.Ancestry, d.Name);
                return n;
            }
            case IntPropertyData i:
            {
                int.TryParse(newValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed);
                var n = new IntPropertyData(i.Name)
                {
                    Value = parsed,
                    PropertyTypeName = i.PropertyTypeName,
                };
                n.Ancestry.Initialize(i.Ancestry, i.Name);
                return n;
            }
            case BoolPropertyData b:
            {
                var n = new BoolPropertyData(b.Name)
                {
                    Value = newValue is "true" or "True" or "1",
                    PropertyTypeName = b.PropertyTypeName,
                };
                n.Ancestry.Initialize(b.Ancestry, b.Name);
                return n;
            }
            default:
                throw new NotSupportedException(
                    $"Cannot clone map entry of type '{src.GetType().Name}'.");
        }
    }

    private void ApplyScalar(PropertyData prop, Newtonsoft.Json.Linq.JToken value)
    {
        switch (prop)
        {
            case BoolPropertyData b: b.Value = (bool)value; break;
            case FloatPropertyData f: f.Value = (float)value; break;
            case DoublePropertyData d: d.Value = (double)value; break;
            case IntPropertyData i: i.Value = (int)value; break;
            case Int16PropertyData i16: i16.Value = (short)value; break;
            case Int64PropertyData i64: i64.Value = (long)value; break;
            case UInt16PropertyData u16: u16.Value = (ushort)value; break;
            case UInt32PropertyData u32: u32.Value = (uint)value; break;
            case Int8PropertyData i8: i8.Value = (sbyte)value; break;
            case StrPropertyData s: s.Value = new FString((string)value); break;
            case Utf8StrPropertyData u: u.Value = new FString((string)value); break;
            case NamePropertyData n: n.Value = FNameFrom((string)value); break;
            case EnumPropertyData e:
                e.Value = value.Type == Newtonsoft.Json.Linq.JTokenType.Integer
                    ? FNameFrom(((int)value).ToString())
                    : FNameFrom((string)value);
                break;
            case BytePropertyData b when IsEnumByte(b):
                if (value.Type == Newtonsoft.Json.Linq.JTokenType.Integer)
                    b.Value = Convert.ToByte(value);
                else
                    b.EnumValue = FNameFrom((string)value);
                break;
            case BytePropertyData b2: b2.Value = Convert.ToByte(value); break;
            case LinearColorPropertyData lc:
            {
                var parts = ((string)value).Trim('(', ')').Split(',');
                if (parts.Length != 4)
                    throw new InvalidDataException($"LinearColor expects '(r, g, b, a)', got '{value}'.");
                var c = lc.Value;
                c.R = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                c.G = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                c.B = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                c.A = float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
                lc.Value = c;
                break;
            }
            default:
                throw new NotSupportedException(
                    $"Field '{CleanName(prop.Name)}' is a '{prop.GetType().Name}' and cannot be edited (yet).");
        }
    }

    public void Save(string outputUassetPath)
    {
        try
        {
            Asset.Write(outputUassetPath);
        }
        catch (FormatException ex)
        {
            throw new FormatException(Path.GetFileName(outputUassetPath) + ": " + ex.Message, ex);
        }
    }

    public bool HasRow(string cleanName) =>
        Table.Table.Data.Any(r => CleanName(r.Name) == cleanName);

    /// <summary>
    /// Deep-clones an existing row under a new name. No-op if the name already exists.
    /// <paramref name="numericName"/> uses the game's FName convention for row names
    /// ending in "_&lt;digits&gt;" (eg. Level_34 → FName("Level", 35)); vanilla data
    /// tables store those as a name + instance number, and the engine's lookup splits
    /// the suffix, so a literal "Level_34" FName never matches.
    /// </summary>
    public void CloneRow(string templateName, string newName, bool numericName = false)
    {
        if (HasRow(newName)) return;
        var src = Table.Table.Data.FirstOrDefault(r => CleanName(r.Name) == templateName)
            ?? throw new KeyNotFoundException($"row '{templateName}' not found");
        var clone = (StructPropertyData)src.Clone();
        clone.Name = numericName ? FName.FromString(Asset, newName) : new FName(Asset, newName);
        Table.Table.Data.Add(clone);
    }

    public static void AddStringTableEntries(string uassetPath, IReadOnlyDictionary<string, string> entries)
    {
        var map = Mappings;
        var asset = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
        var export = asset.Exports.OfType<StringTableExport>().FirstOrDefault()
            ?? throw new InvalidDataException($"'{uassetPath}' has no StringTable export.");
        foreach (var (key, value) in entries)
        {
            var k = new FString(key);
            var v = new FString(value);
            var existing = export.Table.Keys.FirstOrDefault(x => x?.Value == key);
            if (existing != null) export.Table[existing] = v;
            else export.Table.Add(k, v);
        }
        asset.Write(uassetPath);
    }

    // ──────────────────────────────────────────────────────────────────

    private Newtonsoft.Json.Linq.JToken ToJson(PropertyData p) => p switch
    {
        BoolPropertyData v => v.Value,
        FloatPropertyData v => v.Value,
        DoublePropertyData v => v.Value,
        IntPropertyData v => v.Value,
        Int16PropertyData v => v.Value,
        Int64PropertyData v => v.Value,
        UInt16PropertyData v => v.Value,
        UInt32PropertyData v => v.Value,
        Int8PropertyData v => v.Value,
        StrPropertyData v => v.Value?.Value ?? "",
        NamePropertyData v => v.Value?.Value?.Value ?? "",
        EnumPropertyData v => v.Value?.Value?.Value ?? "",
        BytePropertyData v when IsEnumByte(v) =>
            (Newtonsoft.Json.Linq.JToken)(v.EnumValue?.Value?.Value ?? $"byte:{v.Value}"),
        BytePropertyData v => (int)v.Value,
        ArrayPropertyData v => new Newtonsoft.Json.Linq.JArray(v.Value.Select(ToJson)),
        MapPropertyData v => MapToJson(v),
        StructPropertyData v => StructToJson(v),
        ObjectPropertyData v => ObjectRefJson(v),
        SoftObjectPropertyData v => SoftRefJson(v),
        // FText must be rendered here: TextPropertyData.ToString() returns
        // "null" for HistoryType.None because the reader leaves Value null and
        // stores the string in CultureInvariantString — which is exactly how
        // every created/modified row is written. Without this case, Dump()/
        // DumpRow() report "null" for all inline text (the whole custom pictos
        // series included) even though the data is fine. TextPreview already
        // prefers the string-table key for StringTableEntry rows and the
        // culture-invariant string otherwise, so Dump and flatten agree.
        UAssetAPI.PropertyTypes.Objects.TextPropertyData v => TextPreview(v),
        _ => p.ToString() ?? p.GetType().Name,
    };

    private Newtonsoft.Json.Linq.JObject StructToJson(StructPropertyData s)
    {
        var o = new Newtonsoft.Json.Linq.JObject();
        foreach (var f in s.Value)
            o[CleanName(f.Name)] = ToJson(f);
        return o;
    }

    private Newtonsoft.Json.Linq.JToken MapToJson(MapPropertyData m)
    {
        var o = new Newtonsoft.Json.Linq.JObject();
        int i = 0;
        foreach (var (k, v) in m.Value)
        {
            o[KeyToString(k) ?? $"[{i}]"] = ToJson(v);
            i++;
        }
        return o;
    }

    private Newtonsoft.Json.Linq.JToken ObjectRefJson(ObjectPropertyData o)
    {
        try
        {
            if (o.Value is null || o.Value.IsNull()) return "";
            if (o.Value.IsExport())
            {
                var export = o.Value.ToExport(Asset);
                var name = export?.ObjectName?.Value?.Value;
                return string.IsNullOrEmpty(name) ? $"export:{o.Value.Index}" : name;
            }
            var import = o.Value.ToImport(Asset);
            var iname = import?.ObjectName?.Value?.Value;
            return string.IsNullOrEmpty(iname) ? $"import:{-o.Value.Index - 1}" : iname;
        }
        catch
        {
            return $"index:{o.Value?.Index ?? 0}";
        }
    }

    private static Newtonsoft.Json.Linq.JToken SoftRefJson(SoftObjectPropertyData o)
    {
        try { return o.Value.ToString(); }
        catch { return ""; }
    }

    private static bool IsEnumByte(BytePropertyData b) =>
        b.EnumValue?.Value?.Value is { } s && s.Length > 0 && s != "None";

    public static string CleanName(FName name) => StripSuffix(name.ToString());

    public static string StripSuffix(string raw)
    {
        var m = SuffixRegex().Match(raw);
        return m.Success ? m.Groups["base"].Value : raw;
    }

    private FName FNameFrom(string s) => new FName(Asset, s);
}

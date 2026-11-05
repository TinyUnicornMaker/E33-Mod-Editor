using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace E33ModEditor.Core;

/// <summary>
/// Read/write the two stain maps on a skill Data Asset's
/// <c>BP_SkillDataComponent_StainsMechanic_C</c> subobject.
///
/// The community usmap has no schema for that Blueprint class, so UAssetAPI
/// normally falls back to a RawExport. Calling <see cref="EnsureSchemas"/>
/// grafts the class schema (from the extracted BP) into the live mappings so
/// the maps parse as <see cref="MapPropertyData"/>.
///
/// Maps (both <c>TMap&lt;EAttackType, int32&gt;</c>):
/// <list type="bullet">
/// <item><see cref="OverchargeMap"/> — stains consumed when the skill overcharges
/// (also the hard cast requirement for Genesis).</item>
/// <item><see cref="ProducedMap"/> — stains generated on cast via
/// <c>BP_StainHolderComponent.GenerateStainsFromSkill</c>.</item>
/// </list>
/// Extra generate/consume can still live in the skill-script kismet
/// (Electrify's Light stain, Mayhem's consume-all, Trick's crit stains).
/// </summary>
public static class StainsMechanicKit
{
    public const string MechanicClass = "BP_SkillDataComponent_StainsMechanic_C";
    public const string OverchargeMap = "OverchargeRequiredStains";
    public const string ProducedMap = "ProducedStains";

    /// <summary>Cooked <c>EAttackType</c> enumerator name → display name.</summary>
    public static readonly IReadOnlyDictionary<string, string> ElementNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["NewEnumerator0"] = "Fire",
            ["NewEnumerator1"] = "Ice",
            ["NewEnumerator2"] = "Lightning",
            ["NewEnumerator3"] = "Earth",
            ["NewEnumerator4"] = "Dark",
            ["NewEnumerator5"] = "Light",
            ["NewEnumerator6"] = "Physical",
            ["NewEnumerator7"] = "Void",
            ["NewEnumerator9"] = "Invalid",
        };

    /// <summary>Display name (Fire) or enumerator (NewEnumerator0) → enumerator.</summary>
    public static string Enumerator(string element)
    {
        if (element.StartsWith("EAttackType::", StringComparison.OrdinalIgnoreCase))
            element = element["EAttackType::".Length..];
        if (element.StartsWith("NewEnumerator", StringComparison.OrdinalIgnoreCase))
            return element;
        foreach (var (en, name) in ElementNames)
            if (name.Equals(element, StringComparison.OrdinalIgnoreCase))
                return en;
        throw new ArgumentException("unknown stain element: " + element);
    }

    /// <summary>Enumerator (NewEnumerator0) → display name (Fire).</summary>
    public static string Display(string enumerator)
    {
        enumerator = Enumerator(enumerator);
        return ElementNames.TryGetValue(enumerator, out var n) ? n : enumerator;
    }

    /// <summary>Kismet ByteConst ordinal for an element (see the docs' enumerator table).</summary>
    public static int Ordinal(string element)
    {
        return Enumerator(element) switch
        {
            "NewEnumerator9" => 0, // Invalid / empty
            "NewEnumerator6" => 1, // Physical
            "NewEnumerator0" => 2, // Fire
            "NewEnumerator1" => 3, // Ice
            "NewEnumerator2" => 4, // Lightning
            "NewEnumerator3" => 5, // Earth
            "NewEnumerator4" => 6, // Dark
            "NewEnumerator5" => 7, // Light
            "NewEnumerator7" => 8, // Void
            _ => throw new ArgumentException("unknown stain element: " + element),
        };
    }

    public sealed record StainMaps(
        IReadOnlyDictionary<string, int> OverchargeRequired,
        IReadOnlyDictionary<string, int> Produced);

    private static bool _schemasReady;

    /// <summary>
    /// Grafts <c>BP_SkillDataComponent_Base_C</c> + <c>...StainsMechanic_C</c>
    /// into the process mappings. Idempotent. Safe to call before opening a
    /// skill DA; does not replace the Healing Light raw-byte patch path unless
    /// the caller then re-serializes that export as a NormalExport.
    /// </summary>
    public static void EnsureSchemas()
    {
        if (_schemasReady) return;
        var map = DataTableEditor.Mappings
            ?? throw new InvalidOperationException("no usmap loaded");
        if (map.Schemas.ContainsKey(MechanicClass)
            && map.Schemas.ContainsKey("BP_SkillDataComponent_Base_C"))
        {
            _schemasReady = true;
            return;
        }

        InjectClass("BP_SkillDataComponent_Base.uasset", "BP_SkillDataComponent_Base_C", map);
        InjectClass("BP_SkillDataComponent_StainsMechanic.uasset", MechanicClass, map);
        if (!map.Schemas.ContainsKey(MechanicClass))
            throw new FileNotFoundException(
                "BP_SkillDataComponent_StainsMechanic.uasset not under work/; extract it from pakchunk0");
        _schemasReady = true;
    }

    private static void InjectClass(string fileName, string className, Usmap map)
    {
        if (map.Schemas.ContainsKey(className)) return;
        var path = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work"), fileName);
        if (path is null) return;
        var u = new UAsset(path, EngineVersion.VER_UE5_4, map);
        var cls = u.Exports.OfType<ClassExport>()
            .FirstOrDefault(e => e.ObjectName.ToString() == className);
        if (cls is null) return;
        var schema = Usmap.GetSchemaFromStructExport(cls, map.AreFNamesCaseInsensitive);
        if (schema is null) return;
        map.Schemas[schema.Name] = schema;
    }

    public static UAsset Open(string daPath)
    {
        EnsureSchemas();
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAsset(daPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(daPath, EngineVersion.VER_UE5_4);
    }

    public static NormalExport? FindMechanic(UAsset da) =>
        da.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().Contains("StainsMechanic", StringComparison.Ordinal));

    public static StainMaps Read(UAsset da)
    {
        var exp = FindMechanic(da);
        if (exp is null)
            return new StainMaps(new Dictionary<string, int>(), new Dictionary<string, int>());
        return new StainMaps(ReadMap(exp, OverchargeMap), ReadMap(exp, ProducedMap));
    }

    public static StainMaps ReadFile(string daPath) => Read(Open(daPath));

    /// <summary>
    /// Replaces one or both maps on the StainsMechanic subobject and writes
    /// the asset. Null argument = leave that map unchanged. Empty dictionary =
    /// clear the map. Keys may be display names ("Fire") or enumerators
    /// ("NewEnumerator0").
    /// </summary>
    public static void WriteFile(string daPath,
        IReadOnlyDictionary<string, int>? overcharge,
        IReadOnlyDictionary<string, int>? produced)
    {
        var da = Open(daPath);
        Write(da, overcharge, produced);
        da.Write(daPath);
    }

    public static void Write(UAsset da,
        IReadOnlyDictionary<string, int>? overcharge,
        IReadOnlyDictionary<string, int>? produced)
    {
        var exp = FindMechanic(da)
            ?? throw new InvalidDataException("no StainsMechanic subobject on " + da.FilePath);
        if (overcharge is not null) SetMap(da, exp, OverchargeMap, overcharge);
        if (produced is not null) SetMap(da, exp, ProducedMap, produced);
    }

    private static Dictionary<string, int> ReadMap(NormalExport exp, string name)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var map = exp.Data.OfType<MapPropertyData>()
            .FirstOrDefault(p => p.Name.ToString() == name);
        if (map is null) return result;
        foreach (var kv in map.Value)
        {
            var key = kv.Key switch
            {
                EnumPropertyData e => e.Value?.ToString() ?? "",
                BytePropertyData b => (b.EnumValue?.ToString().Length > 0 ? b.EnumValue.ToString() : b.Value.ToString()) ?? "",
                _ => kv.Key.ToString() ?? "",
            };
            var leaf = key.Contains("::", StringComparison.Ordinal) ? key[(key.LastIndexOf("::", StringComparison.Ordinal) + 2)..] : key;
            var count = kv.Value is IntPropertyData i ? i.Value : 0;
            if (leaf.Length > 0) result[leaf] = count;
        }
        return result;
    }

    private static void SetMap(UAsset da, NormalExport exp, string name,
        IReadOnlyDictionary<string, int> entries)
    {
        var map = exp.Data.OfType<MapPropertyData>()
            .FirstOrDefault(p => p.Name.ToString() == name)
            ?? throw new InvalidDataException("no " + name + " on StainsMechanic");

        PropertyData? keyTpl = null;
        PropertyData? valTpl = null;
        foreach (var kv in map.Value)
        {
            keyTpl = kv.Key;
            valTpl = kv.Value;
            break;
        }
        if (keyTpl is null || valTpl is null)
        {
            // Empty map: steal a template from the sibling stain map.
            var siblingName = name == OverchargeMap ? ProducedMap : OverchargeMap;
            var sibling = exp.Data.OfType<MapPropertyData>()
                .FirstOrDefault(p => p.Name.ToString() == siblingName);
            if (sibling is not null)
            {
                foreach (var kv in sibling.Value)
                {
                    keyTpl = (PropertyData)kv.Key.Clone();
                    keyTpl.Name = FName.FromString(da, name);
                    valTpl = (PropertyData)kv.Value.Clone();
                    valTpl.Name = FName.FromString(da, name + "_Value");
                    break;
                }
            }
        }
        if ((keyTpl is null || valTpl is null) && entries.Count > 0)
            throw new InvalidDataException(name + " is empty and no sibling template exists; clone a donor that has stains first");

        map.Value.Clear();
        foreach (var (element, count) in entries)
        {
            if (count <= 0) continue;
            var en = Enumerator(element);
            var key = (PropertyData)keyTpl!.Clone();
            switch (key)
            {
                case EnumPropertyData e:
                    e.Value = FName.FromString(da, "EAttackType::" + en);
                    break;
                case BytePropertyData b:
                    b.EnumValue = FName.FromString(da, "EAttackType::" + en);
                    break;
                default:
                    throw new InvalidDataException("unexpected stain-map key type " + key.GetType().Name);
            }
            var val = (IntPropertyData)valTpl!.Clone();
            val.Value = count;
            map.Value.Add(key, val);
        }
    }
}

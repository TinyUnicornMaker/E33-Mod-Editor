namespace E33ModEditor.Core.SaveGame;

/// <summary>
/// Unreal .sav (GVAS) object model, matching the UE 5.4+ tagged-property layout
/// used by Expedition 33 saves. Field semantics mirror uesave-rs 0.6.2 so
/// untouched saves round-trip byte-identically.
/// </summary>
public sealed class GvasFile
{
    public GvasHeader Header { get; set; } = new();
    /// <summary>Ordered root properties.</summary>
    public List<GvasProperty> Properties { get; set; } = new();
    /// <summary>Raw trailing bytes after the root terminator, preserved verbatim.</summary>

    public byte[] Extra { get; set; } = Array.Empty<byte>();

    public static GvasFile Load(string path)
    {
        using var stream = File.OpenRead(path);
        return new GvasReader(stream).Read();
    }

    public void Save(string path)
    {
        using var stream = File.Create(path);
        new GvasWriter(stream).Write(this);
    }

    public GvasProperty? Find(string name) =>
        Properties.FirstOrDefault(p => p.Name.Equals(name, StringComparison.Ordinal));

    /// <summary>Inserts or replaces a root property, keeping original ordering stable.</summary>
    public void Set(GvasProperty property)
    {
        var idx = Properties.FindIndex(p => p.Name.Equals(property.Name, StringComparison.Ordinal));
        if (idx >= 0) Properties[idx] = property;
        else Properties.Add(property);
    }
}

public sealed class GvasHeader
{
    public uint Magic { get; set; } = 0x53414747; // "GVAS"
    public uint SaveGameVersion { get; set; }
    public uint PackageVersionUe4 { get; set; }
    public uint? PackageVersionUe5 { get; set; }
    public ushort EngineMajor { get; set; }
    public ushort EngineMinor { get; set; }
    public ushort EnginePatch { get; set; }
    public uint EngineBuild { get; set; }
    public string EngineBranch { get; set; } = "";
    public uint CustomVersionFormat { get; set; }
    public List<GvasCustomVersion> CustomVersions { get; set; } = new();
    public string SaveGameType { get; set; } = "";

    /// <summary>PROPERTY_TAG_COMPLETE_TYPE_NAME arrived in UE 5.4; E33 writes 5.4+.</summary>
    public bool CompleteTypeNames => EngineMajor > 5 || (EngineMajor == 5 && EngineMinor >= 4);

    /// <summary>FSOFTOBJECTPATH_REMOVE_ASSET_PATH_FNAMES (ue5 &gt;= 1007).</summary>
    public bool SplitAssetPathFNames => (PackageVersionUe5 ?? 0) >= 1007;
}

public sealed record GvasCustomVersion(Guid Id, int Version);

/// <summary>A named root/struct property: name + typed value.</summary>
public sealed class GvasProperty
{
    public GvasProperty() { }
    public GvasProperty(string name, TypedValue value) { Name = name; Value = value; }
    public string Name { get; set; } = "";
    public TypedValue Value { get; set; } = new();
}

/// <summary>Type descriptor for any property or map/set member.</summary>
public sealed class PropType
{
    /// <summary>UE type name, e.g. <c>IntProperty</c>, <c>StructProperty</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>ArrayProperty element type.</summary>
    public PropType? Inner { get; init; }

    /// <summary>MapProperty/SetProperty member types.</summary>
    public PropType? Key { get; init; }
    public PropType? ValueMember { get; init; }

    /// <summary>Complete type name of a struct/enum/byte-enum, split as written on disk.</summary>
    public string? TypeName { get; init; }
    public string? TypePackage { get; init; }

    /// <summary>Struct property GUID (nil unless the game provided one).</summary>
    public Guid? StructId { get; init; }

    /// <summary>EnumProperty trailing container node text, written back verbatim.</summary>
    public string? EnumContainer { get; init; }

    /// <summary>True for property-list structs, false for binary builtin structs.</summary>
    public bool IsUserStruct => Kind == StructKind && !BuiltinStruct.IsBinary(TypeName ?? "");

    public const string StructKind = "StructProperty";

    public static PropType Simple(string kind) => new() { Kind = kind };
    public static PropType Struct(string typeName, string typePackage, Guid? id = null) =>
        new() { Kind = StructKind, TypeName = typeName, TypePackage = typePackage, StructId = id };
}

/// <summary>A typed value: property payload + its type (maps/sets/arrays nest these).</summary>
public sealed class TypedValue
{
    public TypedValue() { }
    public TypedValue(PropType type, object? payload) { Type = type; Payload = payload; }
    public PropType Type { get; set; } = new() { Kind = "None" };
    public object? Payload { get; set; }
}

public sealed record MapEntry(TypedValue Key, TypedValue Value);

/// <summary>Payload helpers. Payloads are plain CLR objects keyed by PropType.Kind:</summary>
public static class GvasPayload
{
    /// <summary>Struct payload: builtin leaf (DateTime ticks, Guid bytes…) or field list.</summary>
    public sealed class Struct
    {
        /// <summary>Builtin struct discriminator, e.g. "DateTime", "Guid", or null for user structs.</summary>
        public string? Builtin { get; init; }
        public List<GvasProperty>? Fields { get; init; }
        /// <summary>DateTime/Timespan tick count.</summary>
        public ulong Ticks { get; init; }
        /// <summary>Binary contents of fixed-width builtins, or the raw Guid bytes.</summary>
        public byte[] Raw { get; init; } = [];
        /// <summary>GameplayTagContainer tag list.</summary>
        public List<string>? Tags { get; init; }
        /// <summary>UniqueNetIdRepl payload: (size, type, contents); null when empty.</summary>
        public (uint Size, string Type, string Contents)? NetId { get; init; }
        /// <summary>SoftObjectPath appearing as a builtin struct payload.</summary>
        public GvasPayload.SoftObject? SoftPath { get; init; }
    }

    /// <summary>Array payload: homogeneous items whose shapes depend on the element type.</summary>
    public sealed class Array
    {
        public List<TypedValue> Items { get; } = new();
        /// <summary>True when a ByteProperty array was stored as raw bytes rather than labels.</summary>
        public bool RawBytes { get; set; }
    }

    /// <summary>ByteProperty payload: enum label string, or a lone raw byte when no enum.</summary>
    public sealed class Byte
    {
        public string? Label { get; init; }
        public byte Raw { get; init; }
    }

    /// <summary>SoftObjectProperty payload (split FName variant when the save is UE5.4+).</summary>
    public sealed class SoftObject
    {
        public string AssetPathName { get; init; } = "";
        public string PackageName { get; init; } = "";
        public string AssetName { get; init; } = "";
        public string SubPathString { get; init; } = "";
        public bool Split { get; init; }
    }

    public static TypedValue Int(int v) => new(PropType.Simple("IntProperty"), v);
    public static TypedValue Int64(long v) => new(PropType.Simple("Int64Property"), v);
    public static TypedValue Float(float v) => new(PropType.Simple("FloatProperty"), v);
    public static TypedValue Double(double v) => new(PropType.Simple("DoubleProperty"), v);
    public static TypedValue Bool(bool v) => new(PropType.Simple("BoolProperty"), v);
    public static TypedValue Str(string v) => new(PropType.Simple("StrProperty"), v);
    public static TypedValue Name(string v) => new(PropType.Simple("NameProperty"), v);
}

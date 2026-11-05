using System.Buffers.Binary;
using System.Text;

namespace E33ModEditor.Core.SaveGame;

/// <summary>Reads a UE5 GVAS save file into <see cref="GvasFile"/>.</summary>
public sealed class GvasReader(Stream stream)
{
    private readonly Stream _s = stream;
    private readonly byte[] _buf = new byte[8];
    private bool _splitFNames;

    public static GvasFile ReadFile(string path)
    {
        using var s = File.OpenRead(path);
        return new GvasReader(s).Read();
    }

    public GvasFile Read()
    {
        var header = ReadHeader();
        if (!header.CompleteTypeNames)
            throw new NotSupportedException(
                $"Only UE 5.4+ saves with complete type names are supported (found {header.EngineMajor}.{header.EngineMinor}).");
        _splitFNames = header.SplitAssetPathFNames;
        if (ReadByte() != 0)
            throw new InvalidDataException("Expected root terminator byte 0 after SaveGameType.");

        return new GvasFile
        {
            Header = header,
            Properties = ReadPropertyList(),
            Extra = ReadRemaining(),
        };
    }

    private GvasHeader ReadHeader()
    {
        var h = new GvasHeader
        {
            Magic = ReadUInt32(),
            SaveGameVersion = ReadUInt32(),
            PackageVersionUe4 = ReadUInt32(),
        };
        h.PackageVersionUe5 = h.SaveGameVersion is >= 3 and not 34 ? ReadUInt32() : null;
        h.EngineMajor = ReadUInt16();
        h.EngineMinor = ReadUInt16();
        h.EnginePatch = ReadUInt16();
        h.EngineBuild = ReadUInt32();
        h.EngineBranch = ReadString();
        if (h.EngineMajor > 4 || (h.EngineMajor == 4 && h.EngineMinor >= 12))
        {
            h.CustomVersionFormat = ReadUInt32();
            var count = ReadInt32();
            for (var i = 0; i < count; i++)
                h.CustomVersions.Add(new GvasCustomVersion(ReadGuid(), ReadInt32()));
        }
        h.SaveGameType = ReadString();
        return h;
    }

    // ───────────────────────── property lists ─────────────────────────

    private List<GvasProperty> ReadPropertyList()
    {
        var list = new List<GvasProperty>();
        while (ReadProperty() is { } prop)
            list.Add(prop);
        return list;
    }

    private GvasProperty? ReadProperty()
    {
        var start = _s.Position;
        var name = ReadString();
        if (name == "None") return null;
        var (type, size, rawFlags) = ReadTag(name);
        if (Environment.GetEnvironmentVariable("E33_SAVE_TRACE") is not null)
            Console.Error.WriteLine($"[trace] @{start} {name}: {Describe(type)} size={size} flags=0x{rawFlags:X2}");
        object payload = type.Kind == "BoolProperty"
            ? (rawFlags & 0x10) != 0
            : ReadPayload(type, size);
        return new GvasProperty(name, new TypedValue(type, payload));
    }

    private static string Describe(PropType t) => t.Kind switch
    {
        "StructProperty" => $"StructProperty<{t.TypePackage}.{t.TypeName}>",
        "ArrayProperty" => $"ArrayProperty<{t.Inner?.Kind}>",
        "MapProperty" => $"MapProperty<{t.Key?.Kind},{t.ValueMember?.Kind}>",
        _ => t.Kind,
    };

    private sealed record TagInfo(PropType Type, uint Size, byte Raw);

    /// <summary>UE 5.4 complete-type-name tag: node tree, then size + flags.</summary>
    private TagInfo ReadTag(string name)
    {
        var type = ReadTypeNode();
        var size = ReadUInt32();
        var rawFlags = ReadByte();

        if ((rawFlags & 0x01) != 0) SkipBytes(4);   // HasArrayIndex — consumed, not rewritten
        if ((rawFlags & 0x02) != 0) SkipBytes(16);  // HasPropertyGuid — consumed, not rewritten
        if ((rawFlags & 0x04) != 0)
            throw new NotSupportedException($"Property '{name}' uses HasPropertyExtensions, which is unsupported.");
        return new TagInfo(type, size, rawFlags);
    }

    private PropType ReadTypeNode()
    {
        var kind = ReadString();
        var innerCount = ReadUInt32();
        switch (kind)
        {
            case "ArrayProperty":
                Require(innerCount == 1, kind);
                return new PropType { Kind = kind, Inner = ReadTypeNode() };
            case "StructProperty":
            {
                Require(innerCount is 1 or 2, kind);
                var (tn, tp) = ReadPathNodes();
                Guid? id = innerCount == 2 ? ReadGuidNode() : null;
                return PropType.Struct(tn, tp, id);
            }
            case "SetProperty":
                Require(innerCount == 1, kind);
                return new PropType { Kind = kind, Key = ReadTypeNode() };
            case "MapProperty":
                Require(innerCount == 2, kind);
                return new PropType { Kind = kind, Key = ReadTypeNode(), ValueMember = ReadTypeNode() };
            case "ByteProperty":
            {
                Require(innerCount is 0 or 1, kind);
                var (tn, tp) = innerCount == 1 ? ReadPathNodes() : (null, null);
                return new PropType { Kind = kind, TypeName = tn, TypePackage = tp };
            }
            case "EnumProperty":
            {
                Require(innerCount == 2, kind);
                var (tn, tp) = ReadPathNodes();
                var container = ReadString();
                Require(ReadUInt32() == 0, $"{kind} container node");
                return new PropType
                {
                    Kind = kind,
                    TypeName = tn,
                    TypePackage = tp,
                    EnumContainer = container,
                };
            }
            default:
                Require(innerCount == 0, kind);
                return PropType.Simple(kind);
        }
    }

    /// <summary>Complete type names are two nodes: (name, inner=1) then (package, inner=0).</summary>
    private (string Name, string Package) ReadPathNodes()
    {
        var typeName = ReadString();
        Require(ReadUInt32() == 1, "type name node");
        var typePackage = ReadString();
        Require(ReadUInt32() == 0, "type package node");
        return (typeName, typePackage);
    }

    private Guid ReadGuidNode()
    {
        var text = ReadString();
        Require(ReadUInt32() == 0, "struct id node");
        if (!Guid.TryParse(text, out var g))
            throw new InvalidDataException($"Malformed struct id '{text}'.");
        return g;
    }

    // ───────────────────────── payloads ─────────────────────────

    private object ReadPayload(PropType type, uint bodySize)
    {
        switch (type.Kind)
        {
            case "IntProperty": return ReadInt32();
            case "Int64Property": return ReadInt64();
            case "UInt32Property": return ReadUInt32();
            case "FloatProperty": return ReadSingle();
            case "DoubleProperty": return ReadDouble();
            case "StrProperty":
            case "NameProperty": return ReadString();
            case "EnumProperty":
            case "ObjectProperty": return ReadString();
            case "SoftObjectProperty": return ReadSoftObject();
            case "ByteProperty":
                return type.TypeName != null
                    ? new GvasPayload.Byte { Label = ReadString() }
                    : new GvasPayload.Byte { Raw = ReadByte() };
            case "StructProperty": return ReadStruct(type, bodySize);
            case "ArrayProperty": return ReadArray(type.Inner!, bodySize - 4);
            case "MapProperty": return ReadMap(type.Key!, type.ValueMember!);
            case "SetProperty": return ReadSet(type.Key!);
            default:
                throw new NotSupportedException($"Unsupported property type '{type.Kind}'.");
        }
    }

    private GvasPayload.SoftObject ReadSoftObject()
    {
        var assetPathName = ReadString();
        if (!_splitFNames)
            return new GvasPayload.SoftObject { AssetPathName = assetPathName, SubPathString = ReadString() };
        return new GvasPayload.SoftObject
        {
            AssetPathName = assetPathName,
            PackageName = ReadString(),
            AssetName = ReadString(),
            Split = true,
        };
    }

    private object ReadStruct(PropType type, uint bodySize)
    {
        var builtin = BuiltinStruct.For(type.TypeName ?? "");
        if (builtin == null)
            return new GvasPayload.Struct { Fields = ReadPropertyList() };

        switch (builtin)
        {
            case "DateTime" or "Timespan":
            {
                var raw = ReadBytes(8);
                return new GvasPayload.Struct
                {
                    Builtin = builtin,
                    Raw = raw,
                    Ticks = BitConverter.ToUInt64(raw, 0),
                };
            }
            case "GameplayTagContainer":
            {
                var n = ReadInt32();
                var tags = new List<string>(n);
                for (var i = 0; i < n; i++) tags.Add(ReadString());
                return new GvasPayload.Struct { Builtin = builtin, Tags = tags };
            }
            case "UniqueNetIdRepl":
            {
                var size = ReadUInt32();
                if (size == 0) return new GvasPayload.Struct { Builtin = builtin };
                return new GvasPayload.Struct
                {
                    Builtin = builtin,
                    NetId = (size, ReadString(), ReadString()),
                };
            }
            case "SoftObjectPath":
                return new GvasPayload.Struct { Builtin = builtin, SoftPath = ReadSoftObject() };
            default:
                // Fixed-width binary structs (Vector, Quat, Color…); widths are
                // constant under large-world coordinates (UE5).
                return new GvasPayload.Struct { Builtin = builtin, Raw = ReadBytes(BuiltinStruct.Width(builtin)) };
        }
    }

    private GvasPayload.Array ReadArray(PropType inner, uint elemBytes)
    {
        var count = ReadInt32();
        var result = new GvasPayload.Array();
        if (inner.Kind == PropType.StructKind)
        {
            var perElem = count > 0 ? elemBytes / (uint)count : 0;
            for (var i = 0; i < count; i++)
            {
                var bs = count > 0 && i == count - 1 ? elemBytes - perElem * (uint)(count - 1) : perElem;
                result.Items.Add(new TypedValue(CloneStructType(inner), ReadStruct(inner, bs)));
            }
            return result;
        }
        if (inner.Kind == "ByteProperty")
        {
            // Raw byte array when the element width is exactly one byte; enum labels otherwise.
            if (inner.TypeName == null && count > 0 && elemBytes == (uint)count)
            {
                result.RawBytes = true;
                for (var i = 0; i < count; i++)
                    result.Items.Add(new TypedValue(inner, new GvasPayload.Byte { Raw = ReadByte() }));
            }
            else
            {
                for (var i = 0; i < count; i++)
                    result.Items.Add(new TypedValue(inner,
                        new GvasPayload.Byte { Label = ReadString() }));
            }
            return result;
        }
        for (var i = 0; i < count; i++)
            result.Items.Add(new TypedValue(inner, ReadSimpleInner(inner)));
        return result;
    }

    private object ReadSimpleInner(PropType inner) => inner.Kind switch
    {
        "EnumProperty" => ReadString(),
        "StrProperty" or "NameProperty" or "ObjectProperty" => ReadString(),
        "SoftObjectProperty" => ReadSoftObject(),
        "IntProperty" => ReadInt32(),
        "Int64Property" => ReadInt64(),
        "UInt32Property" => ReadUInt32(),
        "FloatProperty" => ReadSingle(),
        "DoubleProperty" => ReadDouble(),
        "BoolProperty" => ReadByte() != 0,
        _ => throw new NotSupportedException($"Unsupported array element type '{inner.Kind}'."),
    };

    private List<MapEntry> ReadMap(PropType key, PropType valueMember)
    {
        ReadUInt32(); // engine-written placeholder preceding the entry count
        var count = ReadInt32();
        var entries = new List<MapEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var k = new TypedValue(key, ReadMember(key));
            var v = new TypedValue(valueMember, ReadMember(valueMember));
            entries.Add(new MapEntry(k, v));
        }
        return entries;
    }

    private List<TypedValue> ReadSet(PropType key)
    {
        ReadUInt32();
        var count = ReadInt32();
        var items = new List<TypedValue>(count);
        for (var i = 0; i < count; i++)
            items.Add(new TypedValue(key, ReadMember(key)));
        return items;
    }

    private object ReadMember(PropType t) => t.Kind switch
    {
        PropType.StructKind => ReadStruct(t, 0),
        "BoolProperty" => ReadByte() != 0,
        "ByteProperty" => t.TypeName != null
            ? new GvasPayload.Byte { Label = ReadString() }
            : new GvasPayload.Byte { Raw = ReadByte() },
        _ => ReadPayload(t, 0),
    };

    private static PropType CloneStructType(PropType inner) =>
        inner.StructId is { } id ? PropType.Struct(inner.TypeName!, inner.TypePackage!, id) : inner;

    // ───────────────────────── primitives ─────────────────────────

    internal string ReadString()
    {
        var len = ReadInt32();
        if (len == 0) return "";
        if (len > 0)
        {
            var bytes = ReadBytes(len);
            var nul = Array.IndexOf(bytes, (byte)0);
            return Encoding.UTF8.GetString(bytes, 0, nul < 0 ? bytes.Length : nul);
        }
        var units = ReadBytes(-len * 2);
        var chars = new ushort[units.Length / 2];
        for (var i = 0; i < chars.Length; i++) chars[i] = BitConverter.ToUInt16(units, i * 2);
        var end = Array.IndexOf(chars, (ushort)0);
        return new string(Array.ConvertAll(chars[..(end < 0 ? chars.Length : end)], c => (char)c));
    }

    internal byte ReadByte()
    {
        Fill(1);
        return _buf[0];
    }

    internal int ReadInt32() { Fill(4); return BinaryPrimitives.ReadInt32LittleEndian(_buf); }
    internal uint ReadUInt32() { Fill(4); return BinaryPrimitives.ReadUInt32LittleEndian(_buf); }
    internal long ReadInt64() { Fill(8); return BinaryPrimitives.ReadInt64LittleEndian(_buf); }
    internal ulong ReadUInt64() { Fill(8); return BinaryPrimitives.ReadUInt64LittleEndian(_buf); }
    internal ushort ReadUInt16() { Fill(2); return BinaryPrimitives.ReadUInt16LittleEndian(_buf); }
    internal float ReadSingle() { Fill(4); return BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(_buf)); }
    internal double ReadDouble() { Fill(8); return BitConverter.UInt64BitsToDouble(BinaryPrimitives.ReadUInt64LittleEndian(_buf)); }

    private Guid ReadGuid()
    {
        var b = ReadBytes(16);
        return new Guid(b);
    }

    private byte[] ReadBytes(int count)
    {
        var data = new byte[count];
        var read = 0;
        while (read < count)
        {
            var n = _s.Read(data, read, count - read);
            if (n <= 0)
                throw new InvalidDataException(
                    $"Unexpected end of file at offset {_s.Position} (wanted {count - read} more bytes).");
            read += n;
        }
        return data;
    }

    private byte[] ReadRemaining()
    {
        using var ms = new MemoryStream();
        _s.CopyTo(ms);
        return ms.ToArray();
    }

    private void SkipBytes(int count) => _s.Seek(count, SeekOrigin.Current);

    private void Fill(int n)
    {
        var read = 0;
        while (read < n)
        {
            var got = _s.Read(_buf, read, n - read);
            if (got <= 0)
                throw new InvalidDataException(
                    $"Unexpected end of file at offset {_s.Position} (wanted {n - read} more bytes).");
            read += got;
        }
    }

    private static void Require(bool cond, string what)
    {
        if (!cond) throw new InvalidDataException($"Unexpected layout near {what}.");
    }
}

internal static class BuiltinStruct
{
    /// <summary>True when the struct type is serialized as binary data (not a property list).</summary>
    public static bool IsBinary(string typeName) => For(typeName) != null;

    /// <summary>Builtin (binary or special-cased) struct discriminators keyed by their type suffix.</summary>
    public static string? For(string typeName)
    {
        return typeName switch
        {
            "DateTime" => "DateTime",
            "Guid" => "Guid",
            "Timespan" => "Timespan",
            "Vector2D" => "Vector2D",
            "Vector" => "Vector",
            "IntVector" => "IntVector",
            "Box" => "Box",
            "IntPoint" => "IntPoint",
            "Quat" => "Quat",
            "Rotator" => "Rotator",
            "LinearColor" => "LinearColor",
            "Color" => "Color",
            "SoftObjectPath" => "SoftObjectPath",
            "GameplayTagContainer" => "GameplayTagContainer",
            "UniqueNetIdRepl" => "UniqueNetIdRepl",
            _ => null,
        };
    }

    /// <summary>Fixed byte widths under large-world coordinates (UE5).</summary>
    public static int Width(string builtin) => builtin switch
    {
        "DateTime" or "Timespan" => 8,
        "Guid" => 16,
        "IntPoint" => 8,
        "IntVector" => 12,
        "Vector2D" => 16,
        "Vector" or "Rotator" => 24,
        "Quat" => 32,
        "LinearColor" => 16,
        "Color" => 4,
        "Box" => 49, // Vector + Vector + valid byte
        _ => throw new NotSupportedException($"No fixed width for builtin struct '{builtin}'."),
    };
}

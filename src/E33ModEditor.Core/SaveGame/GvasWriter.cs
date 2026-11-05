using System.Buffers.Binary;
using System.Text;

namespace E33ModEditor.Core.SaveGame;

/// <summary>Serializes a <see cref="GvasFile"/> back to UE5 GVAS bytes.</summary>
public sealed class GvasWriter(Stream stream)
{
    private readonly Stream _s = stream;

    public static void WriteFile(string path, GvasFile file)
    {
        using var s = File.Create(path);
        new GvasWriter(s).Write(file);
    }

    public void Write(GvasFile file)
    {
        WriteHeader(file.Header);
        Primitives.String(_s, file.Header.SaveGameType);
        _s.WriteByte(0); // root terminator byte (UE 5.4 complete-type-name layout)
        foreach (var prop in file.Properties)
            WriteProperty(_s, prop.Name, prop);
        Primitives.String(_s, "None");
        if (file.Extra.Length > 0) _s.Write(file.Extra);
    }

    private void WriteHeader(GvasHeader h)
    {
        Primitives.UInt32(_s, h.Magic);
        Primitives.UInt32(_s, h.SaveGameVersion);
        Primitives.UInt32(_s, h.PackageVersionUe4);
        if (h.SaveGameVersion is >= 3 and not 34)
            Primitives.UInt32(_s, h.PackageVersionUe5 ?? throw new InvalidDataException("missing package UE5 version"));
        Primitives.UInt16(_s, h.EngineMajor);
        Primitives.UInt16(_s, h.EngineMinor);
        Primitives.UInt16(_s, h.EnginePatch);
        Primitives.UInt32(_s, h.EngineBuild);
        Primitives.String(_s, h.EngineBranch);
        if (h.EngineMajor > 4 || (h.EngineMajor == 4 && h.EngineMinor >= 12))
        {
            Primitives.UInt32(_s, h.CustomVersionFormat);
            Primitives.Int32(_s, h.CustomVersions.Count);
            foreach (var cv in h.CustomVersions)
            {
                _s.Write(cv.Id.ToByteArray());
                Primitives.Int32(_s, cv.Version);
            }
        }
    }

    // ───────────────────────── properties ─────────────────────────

    /// <summary>Writes one tagged property (tag + body) to <paramref name="s"/>.</summary>
    private void WriteProperty(Stream s, string name, GvasProperty prop)
    {
        var type = prop.Value.Type;
        var flags = ComputeFlags(prop);

        // Bodies are built in a scratch stream because their size goes into the tag.
        var body = new MemoryStream();
        WritePayload(body, type, prop.Value.Payload);

        Primitives.String(s, name);
        WriteTypeNodes(s, type);
        Primitives.UInt32(s, (uint)body.Length);
        s.WriteByte(flags);
        body.Position = 0;
        body.CopyTo(s);
    }

    private static byte ComputeFlags(GvasProperty prop)
    {
        // Mirror uesave: flags are recomputed, never copied. Binary/native
        // structs set the bit — including when they appear as map/set/array
        // member types inside the tag tree. Bool values ride the tag;
        // index/guid payloads are never emitted.
        byte flags = 0;
        if (TagSetsBinaryBit(prop.Value.Type))
            flags |= 0x08;
        if (prop.Value.Type.Kind == "BoolProperty" && prop.Value.Payload is true)
            flags |= 0x10;
        return flags;
    }

    private static bool TagSetsBinaryBit(PropType t) => t.Kind switch
    {
        "StructProperty" => !t.IsUserStruct,
        "ArrayProperty" => t.Inner != null && TagSetsBinaryBit(t.Inner),
        "SetProperty" => t.Key != null && TagSetsBinaryBit(t.Key),
        "MapProperty" => (t.Key != null && TagSetsBinaryBit(t.Key))
                         || (t.ValueMember != null && TagSetsBinaryBit(t.ValueMember)),
        _ => false,
    };

    private void WriteTypeNodes(Stream s, PropType t)
    {
        switch (t.Kind)
        {
            case "ArrayProperty":
                WriteNode(s, t.Kind, 1);
                WriteTypeNodes(s, t.Inner!);
                break;
            case "StructProperty":
                WriteNode(s, t.Kind, t.StructId is null || t.StructId == Guid.Empty ? 1u : 2u);
                Primitives.PathNodes(s, t.TypeName!, t.TypePackage!);
                if (t.StructId is { } id && id != Guid.Empty)
                    WriteNode(s, id.ToString(), 0);
                break;
            case "SetProperty":
                WriteNode(s, t.Kind, 1);
                WriteTypeNodes(s, t.Key!);
                break;
            case "MapProperty":
                WriteNode(s, t.Kind, 2);
                WriteTypeNodes(s, t.Key!);
                WriteTypeNodes(s, t.ValueMember!);
                break;
            case "ByteProperty":
                WriteNode(s, t.Kind, t.TypeName != null ? 1u : 0u);
                if (t.TypeName != null) Primitives.PathNodes(s, t.TypeName, t.TypePackage!);
                break;
            case "EnumProperty":
                WriteNode(s, t.Kind, 2);
                Primitives.PathNodes(s, t.TypeName!, t.TypePackage!);
                WriteNode(s, t.EnumContainer ?? "", 0);
                break;
            default:
                WriteNode(s, t.Kind, 0);
                break;
        }
    }

    private void WriteNode(Stream s, string name, uint innerCount)
    {
        Primitives.String(s, name);
        Primitives.UInt32(s, innerCount);
    }

    // ───────────────────────── payloads ─────────────────────────

    private void WritePayload(Stream s, PropType type, object? payload)
    {
        switch (type.Kind)
        {
            case "BoolProperty": /* value lives in flags */ break;
            case "IntProperty": Primitives.Int32(s, (int)payload!); break;
            case "Int64Property": Primitives.Int64(s, (long)payload!); break;
            case "UInt32Property": Primitives.UInt32(s, (uint)payload!); break;
            case "FloatProperty": Primitives.Single(s, (float)payload!); break;
            case "DoubleProperty": Primitives.Double(s, (double)payload!); break;
            case "StrProperty":
            case "NameProperty":
            case "EnumProperty":
            case "ObjectProperty":
                Primitives.String(s, (string)payload!);
                break;
            case "SoftObjectProperty": WriteSoftObject(s, (GvasPayload.SoftObject)payload!); break;
            case "ByteProperty":
            {
                var b = (GvasPayload.Byte)payload!;
                if (type.TypeName != null) Primitives.String(s, b.Label ?? "");
                else s.WriteByte(b.Raw);
                break;
            }
            case "StructProperty": WriteStruct(s, type, (GvasPayload.Struct)payload!); break;
            case "ArrayProperty": WriteArray(s, type.Inner!, (GvasPayload.Array)payload!); break;
            case "MapProperty": WriteMap(s, (List<MapEntry>)payload!); break;
            case "SetProperty": WriteSet(s, type.Key!, (List<TypedValue>)payload!); break;
            default:
                throw new NotSupportedException($"Unsupported property type '{type.Kind}'.");
        }
    }

    private void WriteSoftObject(Stream s, GvasPayload.SoftObject so)
    {
        Primitives.String(s, so.AssetPathName);
        if (!so.Split) Primitives.String(s, so.SubPathString);
        else
        {
            Primitives.String(s, so.PackageName);
            Primitives.String(s, so.AssetName);
        }
    }

    private void WriteStruct(Stream s, PropType type, GvasPayload.Struct st)
    {
        switch (BuiltinStruct.For(type.TypeName ?? ""))
        {
            case null:
                foreach (var field in st.Fields ?? new List<GvasProperty>())
                    WriteProperty(s, field.Name, field);
                Primitives.String(s, "None");
                break;
            case "DateTime" or "Timespan" or "Guid":
            case "Vector2D" or "Vector" or "IntVector" or "Box" or "IntPoint":
            case "Quat" or "Rotator" or "LinearColor" or "Color":
                s.Write(st.Raw);
                break;
            case "GameplayTagContainer":
                Primitives.Int32(s, st.Tags?.Count ?? 0);
                if (st.Tags != null) foreach (var t in st.Tags) Primitives.String(s, t);
                break;
            case "UniqueNetIdRepl":
                if (st.NetId is { } net)
                {
                    Primitives.UInt32(s, net.Size);
                    Primitives.String(s, net.Type);
                    Primitives.String(s, net.Contents);
                }
                else Primitives.UInt32(s, 0);
                break;
            case "SoftObjectPath":
                WriteSoftObject(s, st.SoftPath ?? new GvasPayload.SoftObject());
                break;
            default:
                throw new InvalidOperationException("unreachable builtin");
        }
    }

    private void WriteArray(Stream s, PropType inner, GvasPayload.Array arr)
    {
        Primitives.Int32(s, arr.Items.Count);
        foreach (var item in arr.Items)
        {
            if (inner.Kind == PropType.StructKind)
                WriteStruct(s, inner, (GvasPayload.Struct)item.Payload!);
            else if (inner.Kind == "ByteProperty")
            {
                var b = (GvasPayload.Byte)item.Payload!;
                if (arr.RawBytes) s.WriteByte(b.Raw);
                else Primitives.String(s, b.Label ?? "");
            }
            else WriteSimpleInner(s, inner, item.Payload);
        }
    }

    private void WriteSimpleInner(Stream s, PropType inner, object? payload)
    {
        switch (inner.Kind)
        {
            case "EnumProperty":
            case "StrProperty":
            case "NameProperty":
            case "ObjectProperty":
                Primitives.String(s, (string)payload!);
                break;
            case "SoftObjectProperty": WriteSoftObject(s, (GvasPayload.SoftObject)payload!); break;
            case "IntProperty": Primitives.Int32(s, (int)payload!); break;
            case "Int64Property": Primitives.Int64(s, (long)payload!); break;
            case "UInt32Property": Primitives.UInt32(s, (uint)payload!); break;
            case "FloatProperty": Primitives.Single(s, (float)payload!); break;
            case "DoubleProperty": Primitives.Double(s, (double)payload!); break;
            case "BoolProperty": s.WriteByte((bool)payload! ? (byte)1 : (byte)0); break;
            default:
                throw new NotSupportedException($"Unsupported array element type '{inner.Kind}'.");
        }
    }

    private void WriteMap(Stream s, List<MapEntry> entries)
    {
        Primitives.UInt32(s, 0); // engine placeholder preceding the count
        Primitives.Int32(s, entries.Count);
        foreach (var e in entries)
        {
            WriteMember(s, e.Key);
            WriteMember(s, e.Value);
        }
    }

    private void WriteSet(Stream s, PropType key, List<TypedValue> items)
    {
        Primitives.UInt32(s, 0);
        Primitives.Int32(s, items.Count);
        foreach (var item in items) WriteMember(s, item);
    }

    private void WriteMember(Stream s, TypedValue tv)
    {
        switch (tv.Type.Kind)
        {
            case PropType.StructKind: WriteStruct(s, tv.Type, (GvasPayload.Struct)tv.Payload!); break;
            case "BoolProperty": s.WriteByte((bool)tv.Payload! ? (byte)1 : (byte)0); break;
            case "ByteProperty":
            {
                var b = (GvasPayload.Byte)tv.Payload!;
                if (tv.Type.TypeName != null) Primitives.String(s, b.Label ?? "");
                else s.WriteByte(b.Raw);
                break;
            }
            default:
                WritePayload(s, tv.Type, tv.Payload);
                break;
        }
    }

    /// <summary>Little-endian binary and UE string primitives.</summary>
    internal static class Primitives
    {
        public static void Int32(Stream s, int v) { var b = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(b, v); s.Write(b); }
        public static void UInt32(Stream s, uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); s.Write(b); }
        public static void Int64(Stream s, long v) { var b = new byte[8]; BinaryPrimitives.WriteInt64LittleEndian(b, v); s.Write(b); }
        public static void UInt16(Stream s, ushort v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, v); s.Write(b); }
        public static void Single(Stream s, float v) => UInt32(s, BitConverter.SingleToUInt32Bits(v));
        public static void Double(Stream s, double v) => Int64(s, BitConverter.DoubleToInt64Bits(v));

        public static void String(Stream s, string str)
        {
            if (str.Length == 0)
            {
                UInt32(s, 0);
                return;
            }
            if (str.IsAscii())
            {
                Int32(s, str.Length + 1);
                s.Write(Encoding.ASCII.GetBytes(str));
                s.WriteByte(0);
            }
            else
            {
                // The stored count is the number of UTF-16 code units including
                // the trailing null (bytes are count * 2).
                Int32(s, -(str.Length + 1));
                foreach (var c in str)
                {
                    s.WriteByte((byte)c);
                    s.WriteByte((byte)((ushort)c >> 8));
                }
                s.WriteByte(0);
                s.WriteByte(0);
            }
        }

        /// <summary>Complete type name wire form: (name, inner=1) then (package, inner=0).</summary>
        public static void PathNodes(Stream s, string typeName, string typePackage)
        {
            String(s, typeName);
            UInt32(s, 1);
            String(s, typePackage);
            UInt32(s, 0);
        }
    }
}

file static class AsciiExtensions
{
    public static bool IsAscii(this string s)
    {
        foreach (var c in s) if (c > 127) return false;
        return true;
    }
}

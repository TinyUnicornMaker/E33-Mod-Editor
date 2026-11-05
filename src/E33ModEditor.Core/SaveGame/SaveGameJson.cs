using Newtonsoft.Json.Linq;

namespace E33ModEditor.Core.SaveGame;

/// <summary>Lossless JSON projection of <see cref="GvasFile"/> (Newtonsoft).</summary>
public static class SaveGameJson
{
    public static JObject ToJObject(GvasFile file)
    {
        var o = new JObject
        {
            ["header"] = new JObject
            {
                ["magic"] = file.Header.Magic,
                ["saveGameVersion"] = file.Header.SaveGameVersion,
                ["packageVersionUe4"] = file.Header.PackageVersionUe4,
                ["packageVersionUe5"] = file.Header.PackageVersionUe5,
                ["engineMajor"] = file.Header.EngineMajor,
                ["engineMinor"] = file.Header.EngineMinor,
                ["enginePatch"] = file.Header.EnginePatch,
                ["engineBuild"] = file.Header.EngineBuild,
                ["engineBranch"] = file.Header.EngineBranch,
                ["customVersionFormat"] = file.Header.CustomVersionFormat,
                ["customVersions"] = JArray.FromObject(file.Header.CustomVersions.Select(cv =>
                    new JObject { ["id"] = cv.Id, ["version"] = cv.Version })),
                ["saveGameType"] = file.Header.SaveGameType,
            },
            ["properties"] = ToArray(file.Properties),
        };
        if (file.Extra.Length > 0) o["extraBase64"] = Convert.ToBase64String(file.Extra);
        return o;
    }

    public static GvasFile FromJObject(JObject o)
    {
        var h = (JObject)o["header"]!;
        var file = new GvasFile
        {
            Header = new GvasHeader
            {
                Magic = h.Value<uint?>("magic") ?? 0x53414747,
                SaveGameVersion = h.Value<uint>("saveGameVersion"),
                PackageVersionUe4 = h.Value<uint>("packageVersionUe4"),
                PackageVersionUe5 = h.Value<uint?>("packageVersionUe5"),
                EngineMajor = h.Value<ushort>("engineMajor"),
                EngineMinor = h.Value<ushort>("engineMinor"),
                EnginePatch = h.Value<ushort>("enginePatch"),
                EngineBuild = h.Value<uint>("engineBuild"),
                EngineBranch = h.Value<string>("engineBranch") ?? "",
                CustomVersionFormat = h.Value<uint?>("customVersionFormat") ?? 0,
                SaveGameType = h.Value<string>("saveGameType") ?? "",
            },
            Properties = FromArray((JArray)o["properties"]!),
        };
        foreach (var cv in (JArray?)h["customVersions"] ?? new JArray())
            file.Header.CustomVersions.Add(new GvasCustomVersion(
                Guid.Parse(cv.Value<string>("id")!), cv.Value<int>("version")));
        file.Extra = o.Value<string?>("extraBase64") is { } b64 ? Convert.FromBase64String(b64) : [];
        return file;
    }

    // ───────────────────────── properties ─────────────────────────

    private static JArray ToArray(List<GvasProperty> props)
    {
        var arr = new JArray();
        foreach (var p in props)
            arr.Add(new JObject { ["name"] = p.Name, ["type"] = TypeToJson(p.Value.Type), ["value"] = ValueToJson(p.Value) });
        return arr;
    }

    private static List<GvasProperty> FromArray(JArray arr)
    {
        var list = new List<GvasProperty>(arr.Count);
        foreach (var t in arr)
        {
            var type = TypeFromJson((JObject)t["type"]!);
            var payload = PayloadFromJson(type, t["value"]!);
            list.Add(new GvasProperty(t.Value<string>("name")!, new TypedValue(type, payload)));
        }        return list;
    }

    private static JObject TypeToJson(PropType t)
    {
        var o = new JObject { ["kind"] = t.Kind };
        if (t.Inner != null) o["inner"] = TypeToJson(t.Inner);
        if (t.Key != null) o["key"] = TypeToJson(t.Key);
        if (t.ValueMember != null) o["valueMember"] = TypeToJson(t.ValueMember);
        if (t.TypeName != null) o["typeName"] = t.TypeName;
        if (t.TypePackage != null) o["typePackage"] = t.TypePackage;
        if (t.StructId != null) o["structId"] = t.StructId;
        if (t.EnumContainer != null) o["enumContainer"] = t.EnumContainer;
        return o;
    }

    private static PropType TypeFromJson(JObject o)
    {
        return new PropType
        {
            Kind = o.Value<string>("kind")!,
            Inner = o["inner"] is JObject i ? TypeFromJson(i) : null,
            Key = o["key"] is JObject k ? TypeFromJson(k) : null,
            ValueMember = o["valueMember"] is JObject v ? TypeFromJson(v) : null,
            TypeName = o.Value<string?>("typeName"),
            TypePackage = o.Value<string?>("typePackage"),
            StructId = o.Value<string?>("structId") is { } id ? Guid.Parse(id) : null,
            EnumContainer = o.Value<string?>("enumContainer"),
        };
    }

    private static JToken ValueToJson(TypedValue tv) => tv.Type.Kind switch
    {
        "BoolProperty" => new JValue((bool)tv.Payload!),
        "IntProperty" or "Int64Property" or "UInt32Property" => new JValue(Convert.ToInt64(tv.Payload)),
        "FloatProperty" => new JValue((double)(float)tv.Payload!),
        "DoubleProperty" => new JValue((double)tv.Payload!),
        "StrProperty" or "NameProperty" or "EnumProperty" or "ObjectProperty" => new JValue((string)tv.Payload!),
        "ByteProperty" => ByteToJson((GvasPayload.Byte)tv.Payload!),
        "SoftObjectProperty" => SoftToJson((GvasPayload.SoftObject)tv.Payload!),
        "StructProperty" => StructToJson((GvasPayload.Struct)tv.Payload!),
        "ArrayProperty" => ArrayToJson((GvasPayload.Array)tv.Payload!),
        "MapProperty" => MapToJson((List<MapEntry>)tv.Payload!),
        "SetProperty" => SetToJson((List<TypedValue>)tv.Payload!),
        _ => throw new NotSupportedException($"Unsupported property type '{tv.Type.Kind}'."),
    };

    private static JObject ByteToJson(GvasPayload.Byte b) =>
        b.Label != null ? new JObject { ["label"] = b.Label } : new JObject { ["raw"] = b.Raw };

    private static JObject SoftToJson(GvasPayload.SoftObject s) => new()
    {
        ["split"] = s.Split,
        ["assetPathName"] = s.AssetPathName,
        ["packageName"] = s.PackageName,
        ["assetName"] = s.AssetName,
        ["subPathString"] = s.SubPathString,
    };

    private static JObject StructToJson(GvasPayload.Struct st)
    {
        var o = new JObject();
        if (st.Builtin != null) o["builtin"] = st.Builtin;
        if (st.Fields != null) o["fields"] = ToArray(st.Fields);
        if (st.Ticks != 0) o["ticks"] = st.Ticks;
        if (st.Raw.Length > 0) o["rawBase64"] = Convert.ToBase64String(st.Raw);
        if (st.Tags != null) o["tags"] = JArray.FromObject(st.Tags);
        if (st.NetId is { } net)
            o["netId"] = new JObject { ["size"] = net.Size, ["type"] = net.Type, ["contents"] = net.Contents };
        if (st.SoftPath != null) o["softPath"] = SoftToJson(st.SoftPath);
        return o;
    }

    private static JObject ArrayToJson(GvasPayload.Array a) => new()
    {
        ["rawBytes"] = a.RawBytes,
        ["items"] = new JArray(a.Items.Select(ItemToJson)),
    };

    private static JObject ItemToJson(TypedValue item) => new()
    {
        ["type"] = TypeToJson(item.Type),
        ["value"] = ValueToJson(item),
    };

    private static JObject MapToJson(List<MapEntry> entries) => new()
    {
        ["entries"] = new JArray(entries.Select(e => new JObject
        {
            ["key"] = ItemToJson(e.Key),
            ["value"] = ItemToJson(e.Value),
        })),
    };

    private static JObject SetToJson(List<TypedValue> items) => new()
    {
        ["items"] = new JArray(items.Select(ItemToJson)),
    };

    // ───────────────────────── json → model ─────────────────────────

    private static object PayloadFromJson(PropType type, JToken v) => type.Kind switch
    {
        "BoolProperty" => v.Value<bool>(),
        "IntProperty" => (int)v.Value<long>(),
        "Int64Property" => v.Value<long>(),
        "UInt32Property" => unchecked((uint)v.Value<long>()),
        "FloatProperty" => (float)v.Value<double>(),
        "DoubleProperty" => v.Value<double>(),
        "StrProperty" or "NameProperty" or "EnumProperty" or "ObjectProperty" => v.Value<string>()!,
        "ByteProperty" => ByteFromJson(v),
        "SoftObjectProperty" => SoftFromJson(v),
        "StructProperty" => StructFromJson(v),
        "ArrayProperty" => ArrayFromJson(v),
        "MapProperty" => MapFromJson(v),
        "SetProperty" => SetFromJson(v),
        _ => throw new NotSupportedException($"Unsupported property type '{type.Kind}'."),
    };

    private static GvasPayload.Byte ByteFromJson(JToken v) => new()
    {
        Label = v.Value<string?>("label"),
        Raw = v.Value<byte?>("raw") ?? 0,
    };

    private static GvasPayload.SoftObject SoftFromJson(JToken v) => new()
    {
        Split = v.Value<bool>("split"),
        AssetPathName = v.Value<string>("assetPathName") ?? "",
        PackageName = v.Value<string>("packageName") ?? "",
        AssetName = v.Value<string>("assetName") ?? "",
        SubPathString = v.Value<string>("subPathString") ?? "",
    };

    private static GvasPayload.Struct StructFromJson(JToken t)
    {
        var o = (JObject)t;
        return new GvasPayload.Struct
        {
            Builtin = o.Value<string?>("builtin"),
            Fields = o["fields"] is JArray f ? FromArray(f) : null,
            Ticks = o.Value<ulong?>("ticks") ?? 0,
            Raw = o.Value<string?>("rawBase64") is { } b64 ? Convert.FromBase64String(b64) : [],
            Tags = o["tags"] is JArray tags ? tags.Select(x => x.Value<string>()!).ToList() : null,
            NetId = o["netId"] is JObject n
                ? (n.Value<uint>("size"), n.Value<string>("type") ?? "", n.Value<string>("contents") ?? "")
                : null,
            SoftPath = o["softPath"] is JObject sp ? SoftFromJson(sp) : null,
        };
    }

    private static GvasPayload.Array ArrayFromJson(JToken t)
    {
        var o = (JObject)t;
        var arr = new GvasPayload.Array { RawBytes = o.Value<bool>("rawBytes") };
        foreach (var item in (JArray)o["items"]!)
            arr.Items.Add(ItemFromJson(item));
        return arr;
    }

    private static List<MapEntry> MapFromJson(JToken t)
    {
        var entries = new List<MapEntry>();
        foreach (var e in (JArray)((JObject)t)["entries"]!)
            entries.Add(new MapEntry(ItemFromJson(e["key"]!), ItemFromJson(e["value"]!)));
        return entries;
    }

    private static List<TypedValue> SetFromJson(JToken t)
    {
        var items = new List<TypedValue>();
        foreach (var item in (JArray)((JObject)t)["items"]!)
            items.Add(ItemFromModelJson(item));
        return items;
    }

    private static TypedValue ItemFromJson(JToken? t) =>
        ItemFromModelJson(t!);

    private static TypedValue ItemFromModelJson(JToken t)
    {
        var o = (JObject)t;
        var type = TypeFromJson((JObject)o["type"]!);
        return new TypedValue(type, PayloadFromJson(type, o["value"]!));
    }
}

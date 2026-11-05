using System.Globalization;

namespace E33ModEditor.Core.SaveGame;

/// <summary>
/// High-level edits over an E33 save's well-known properties. All mutations
/// happen in memory; callers persist via <see cref="GvasFile.Save"/>. Entry
/// shapes are cloned from existing rows so new items always match the save's
/// exact struct layout.
/// </summary>
public static class SaveGameEditor
{
    // ───────────────────────── lookups ─────────────────────────

    private const string InventoryKey = "InventoryItems";
    private const string PassivesKey = "PassiveEffectsProgressions";
    private const string ProgressionsKey = "WeaponProgressions";

    public static List<MapEntry> Inventory(GvasFile file) =>
        FindMap(file, InventoryKey) ?? throw new InvalidDataException("save has no InventoryItems map");

    /// <summary>Passive progression list; created from the canonical game shape on fresh saves.</summary>
    public static GvasPayload.Array PassiveProgressions(GvasFile file) =>
        FindArray(file, PassivesKey) ?? CreateProgressionRoot(file, PassivesKey, PassiveEntryType());

    /// <summary>Weapon/pictos level list; created from the canonical game shape on fresh saves.</summary>
    public static GvasPayload.Array Progressions(GvasFile file) =>
        FindArray(file, ProgressionsKey) ?? CreateProgressionRoot(file, ProgressionsKey, ProgressionEntryType());

    private static GvasPayload.Array CreateProgressionRoot(GvasFile file, string name, PropType elementType)
    {
        var array = new GvasPayload.Array();
        file.Set(new GvasProperty(name, new TypedValue(new PropType
        {
            Kind = "ArrayProperty",
            Inner = elementType,
        }, array)));
        return array;
    }

    // Canonical entry shapes harvested from retail saves (field GUID suffixes are
    // engine-stable per build); used when a fresh save carries no entries yet.
    private const string PassiveStructType = "FPassiveEffectProgression";
    private const string PassiveStructPackage = "/Game/Gameplay/Lumina/FPassiveEffectProgression";
    private static readonly Guid PassiveStructId = new("25fd746e-4d79-298f-a2b1-aaaa36138cab");
    private const string WeaponHandleType = "S_WeaponInstanceHandle";
    private const string WeaponHandlePackage = "/Game/Gameplay/Pictos/Weapons/S_WeaponInstanceHandle";
    private static readonly Guid WeaponHandleId = new("9af55cda-4ddf-92e1-c004-d2b2ef26131f");

    private static PropType PassiveEntryType() =>
        PropType.Struct(PassiveStructType, PassiveStructPackage, PassiveStructId);

    private static PropType ProgressionEntryType() =>
        PropType.Struct(WeaponHandleType, WeaponHandlePackage, WeaponHandleId);

    private static TypedValue CanonicalPassiveEntry(string id, bool learnt) => new(
        PassiveEntryType(),
        new GvasPayload.Struct
        {
            Fields = new List<GvasProperty>
            {
                new("PassiveEffectName_3_A92DB6CC4549450728A867A714ADF6C5", GvasPayload.Name(id)),
                new("IsLearnt_9_2561000E49D90653437DE9A45BE2A86D", GvasPayload.Bool(learnt)),
                new("LearntSteps_6_A14D681549E830249C77BD95F2B4CF3F", GvasPayload.Int(learnt ? 4 : 0)),
            },
        });

    private static TypedValue CanonicalProgressionEntry(string id, int level) => new(
        ProgressionEntryType(),
        new GvasPayload.Struct
        {
            Fields = new List<GvasProperty>
            {
                new("DefinitionID_3_60EB24664894755B19F4EBA18A21AF1A", GvasPayload.Name(id)),
                new("CurrentLevel_6_227A00644D035BDD595B2D86C8455B71", GvasPayload.Int(level)),
            },
        });

    internal static List<MapEntry>? FindMap(GvasFile file, string prefix) =>
        file.Properties.FirstOrDefault(p => p.Name.StartsWith(prefix, StringComparison.Ordinal))
            ?.Value.Payload as List<MapEntry>;

    private static GvasPayload.Array? FindArray(GvasFile file, string prefix) =>
        file.Properties.FirstOrDefault(p => p.Name.StartsWith(prefix, StringComparison.Ordinal))
            ?.Value.Payload as GvasPayload.Array;

    /// <summary>All item ids present in the save's inventory.</summary>
    public static HashSet<string> InventoryIds(GvasFile file, out List<MapEntry> inventory)
    {
        inventory = Inventory(file);
        return inventory.Select(e => AsString(e.Key.Payload))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Snapshot of pictos-relevant state keyed by item id.</summary>
    public sealed record PictosState(bool Found, bool Mastered, int Level);

    public static Dictionary<string, PictosState> PictosStates(GvasFile file)
    {
        var states = new Dictionary<string, PictosState>(StringComparer.OrdinalIgnoreCase);
        var found = InventoryIds(file, out _);

        var mastered = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in PassiveProgressions(file).Items)
        {
            var (id, learnt, _) = ReadPassiveEntry(item);
            if (id.Length > 0) mastered[id] = learnt;
        }

        var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Progressions(file).Items)
        {
            var (id, level) = ReadProgressionEntry(item);
            if (id.Length > 0) levels[id] = level;
        }

        foreach (var id in found.Concat(mastered.Keys).Concat(levels.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
            states[id] = new PictosState(
                Found: found.Contains(id),
                Mastered: mastered.TryGetValue(id, out var m) && m,
                Level: levels.TryGetValue(id, out var l) ? l : 1);
        return states;
    }

    // ───────────────────────── mutations ─────────────────────────

    /// <summary>Adds/removes a plain inventory item (weapons, records, journals…).</summary>
    public static void SetItemOwned(GvasFile file, string id, bool owned)
    {
        var inventory = Inventory(file);
        var entry = inventory.FirstOrDefault(e => Matches(e.Key.Payload, id));
        if (owned && entry is null)
        {
            var template = inventory.FirstOrDefault();
            inventory.Add(new MapEntry(
                new TypedValue(CloneType(template?.Key.Type) ?? PropType.Simple("NameProperty"), id),
                new TypedValue(CloneType(template?.Value.Type) ?? PropType.Simple("IntProperty"), 1)));
        }
        else if (!owned && entry is not null)
        {
            inventory.Remove(entry);
        }
    }

    /// <summary>Sets an item's level in the shared progression array.
    /// <paramref name="maxLevel"/> defaults to the vanilla 33; over-level mods
    /// raise the cap and pass their own value (up to 333).</summary>
    public static void SetItemLevel(GvasFile file, string id, int level, int maxLevel = 33)
    {
        level = Math.Clamp(level, 1, Math.Clamp(maxLevel, 1, 333));
        var item = EnsureProgressionEntry(file, id, level: level);
        SetField(Fields(item), "CurrentLevel", GvasPayload.Int(level));
    }

    public static void SetPictosFound(GvasFile file, string id, bool found)
    {
        var inventory = Inventory(file);
        var entry = inventory.FirstOrDefault(e => Matches(e.Key.Payload, id));
        if (found)
        {
            if (entry is null)
            {
                var template = inventory.FirstOrDefault();
                inventory.Add(new MapEntry(
                    new TypedValue(CloneType(template?.Key.Type) ?? PropType.Simple("NameProperty"), id),
                    new TypedValue(CloneType(template?.Value.Type) ?? PropType.Simple("IntProperty"), 1)));
            }
            EnsurePassiveEntry(file, id, learnt: null);
            EnsureProgressionEntry(file, id, level: null);
        }
        else
        {
            if (entry is not null) inventory.Remove(entry);
            RemoveById(PassiveProgressions(file).Items, id);
            RemoveById(Progressions(file).Items, id);
            SaveGameEdits.ClearPictosLoadout(file, id);
        }
    }

    public static void SetPictosMastered(GvasFile file, string id, bool mastered)
    {
        var item = EnsurePassiveEntry(file, id, learnt: mastered);
        var fields = Fields(item);
        SetField(fields, "IsLearnt", GvasPayload.Bool(mastered));
        SetField(fields, "LearntSteps", GvasPayload.Int(mastered ? 4 : 0));
    }

    public static void SetPictosLevel(GvasFile file, string id, int level, int maxLevel = 33)
    {
        level = Math.Clamp(level, 1, Math.Clamp(maxLevel, 1, 333));
        var item = EnsureProgressionEntry(file, id, level: level);
        SetField(Fields(item), "CurrentLevel", GvasPayload.Int(level));
    }

    /// <summary>Swap report: what moved from one pictos id to another.</summary>
    public sealed record PictosSwap(
        string From, string To, int Level, bool Mastered, List<string> EquippedBy);

    /// <summary>
    /// Replaces one owned pictos with another, preserving level, mastery and
    /// every loadout reference (equipped lumina entries and worn pictos slots
    /// are re-pointed in place, so order is kept). Mastery steps reset to the
    /// canonical mastered shape (4). Throws when `from` is absent or `to` is
    /// already owned.
    /// </summary>
    public static PictosSwap SwapPictos(GvasFile file, string fromId, string toId)
    {
        var inventory = Inventory(file);
        var fromOwned = inventory.Any(e => Matches(e.Key.Payload, fromId));
        var passive = PassiveProgressions(file).Items.FirstOrDefault(MatchesId(fromId));
        var prog = Progressions(file).Items.FirstOrDefault(MatchesId(fromId));
        if (!fromOwned && passive is null && prog is null)
            throw new InvalidDataException($"save does not contain '{fromId}'.");
        if (inventory.Any(e => Matches(e.Key.Payload, toId)))
            throw new InvalidDataException($"save already owns '{toId}' — refusing to merge.");

        var level = 1;
        var learnt = false;
        if (prog is not null) level = Int(IndexFields(Fields(prog)), "CurrentLevel");
        if (passive is not null) learnt = Bool(IndexFields(Fields(passive)), "IsLearnt");

        // Re-point loadout references in place before removing `from`.
        var equippedBy = new List<string>();
        foreach (var e in SaveGameEdits.MapEntries(file, SaveGameEdits.CharactersKey))
        {
            var charId = e.Key.Payload as string ?? "";
            if (charId.Length == 0) continue;
            var fields = SaveGameEdits.StructFields(e.Value.Payload);
            if (fields is null) continue;
            var touched = false;
            var lumina = fields.FirstOrDefault(x => ShortKey(x.Name)
                .StartsWith("EquippedPassiveEffects", StringComparison.OrdinalIgnoreCase));
            if (lumina?.Value.Payload is GvasPayload.Array arr)
                foreach (var item in arr.Items)
                    if (item.Payload is string s && s.Equals(fromId, StringComparison.OrdinalIgnoreCase))
                    {
                        item.Payload = toId;
                        touched = true;
                    }
            var slots = fields.FirstOrDefault(x => ShortKey(x.Name)
                .StartsWith("EquippedItemsPerSlot", StringComparison.OrdinalIgnoreCase));
            if (slots?.Value.Payload is List<MapEntry> entries)
                foreach (var s in entries)
                {
                    if (!IsPictosSlot(s.Key.Payload)) continue;
                    if (s.Value.Payload is string sid &&
                        sid.Equals(fromId, StringComparison.OrdinalIgnoreCase))
                    {
                        s.Value.Payload = toId;
                        touched = true;
                    }
                }
            if (touched) equippedBy.Add(charId);
        }

        for (var i = inventory.Count - 1; i >= 0; i--)
            if (Matches(inventory[i].Key.Payload, fromId))
                inventory.RemoveAt(i);
        RemoveById(PassiveProgressions(file).Items, fromId);
        RemoveById(Progressions(file).Items, fromId);

        SetPictosFound(file, toId, true);
        if (learnt) SetPictosMastered(file, toId, true);
        SetPictosLevel(file, toId, level);
        return new PictosSwap(fromId, toId, level, learnt, equippedBy);
    }

    private static bool IsPictosSlot(object? payload)
    {
        if (SaveGameEdits.StructFields(payload) is not { } kf) return false;
        foreach (var f in kf)
        {
            if (!ShortKey(f.Name).StartsWith("ItemType", StringComparison.OrdinalIgnoreCase)) continue;
            var label = f.Value.Payload is GvasPayload.Byte b ? b.Label : f.Value.Payload as string;
            return label?.EndsWith("NewEnumerator10", StringComparison.Ordinal) == true;
        }
        return false;
    }

    /// <summary>Bumps SaveDateTime to now so the game shows a fresh save stamp.</summary>
    public static void TouchSaveDateTime(GvasFile file)
    {
        var prop = file.Properties.FirstOrDefault(p =>
            p.Name.StartsWith("SaveDateTime", StringComparison.Ordinal));
        if (prop?.Value.Payload is not GvasPayload.Struct st || st.Builtin != "DateTime") return;
        var ticks = DateTimeUtcTicksNow();
        prop.Value.Payload = new GvasPayload.Struct
        {
            Builtin = st.Builtin,
            Fields = st.Fields,
            Ticks = ticks,
            Raw = BitConverter.GetBytes(ticks),
            Tags = st.Tags,
            NetId = st.NetId,
            SoftPath = st.SoftPath,
        };
    }

    /// <summary>Windows FILETIME at UTC now — what UE FDateTime serialization uses.</summary>
    public static ulong DateTimeUtcTicksNow() =>
        BitConverter.ToUInt64(BitConverter.GetBytes(DateTime.UtcNow.Ticks));

    // ───────────────────────── internals ─────────────────────────

    private static TypedValue EnsurePassiveEntry(GvasFile file, string id, bool? learnt)
    {
        var array = PassiveProgressions(file);
        var existing = array.Items.FirstOrDefault(MatchesId(id));
        if (existing is not null) return existing;

        // Prefer cloning an existing entry so the shape always matches the save;
        // fall back to the canonical game shape on fresh/empty saves.
        TypedValue created;
        if (array.Items.FirstOrDefault() is { } template)
        {
            var clone = CloneProperties(Fields(template));
            SetNameField(clone, id);
            SetField(clone, "IsLearnt", GvasPayload.Bool(learnt ?? false));
            SetField(clone, "LearntSteps", GvasPayload.Int(learnt == true ? 4 : 0));
            created = new TypedValue(template.Type, new GvasPayload.Struct { Fields = clone });
        }
        else
        {
            created = CanonicalPassiveEntry(id, learnt ?? false);
        }
        array.Items.Add(created);
        return created;
    }

    private static TypedValue EnsureProgressionEntry(GvasFile file, string id, int? level)
    {
        var array = Progressions(file);
        var existing = array.Items.FirstOrDefault(MatchesId(id));
        if (existing is not null) return existing;

        TypedValue created;
        if (array.Items.FirstOrDefault() is { } template)
        {
            var clone = CloneProperties(Fields(template));
            SetNameField(clone, id);
            SetField(clone, "CurrentLevel", GvasPayload.Int(level ?? 1));
            created = new TypedValue(template.Type, new GvasPayload.Struct { Fields = clone });
        }
        else
        {
            created = CanonicalProgressionEntry(id, level ?? 1);
        }
        array.Items.Add(created);
        return created;
    }

    private static void RemoveById(List<TypedValue> items, string id)
    {
        for (var i = items.Count - 1; i >= 0; i--)
            if (MatchesId(id)(items[i]))
                items.RemoveAt(i);
    }

    private static Func<TypedValue, bool> MatchesId(string id) => item =>
        Matches(IdFromEntry(item), id);

    private static string IdFromEntry(TypedValue item) =>
        item.Payload is GvasPayload.Struct st
            ? st.Fields?.FirstOrDefault(f =>
                    f.Name.StartsWith("DefinitionID", StringComparison.Ordinal) ||
                    f.Name.StartsWith("PassiveEffectName", StringComparison.Ordinal))
              ?.Value.Payload as string ?? ""
            : "";

    private static (string Id, bool Learnt, int Steps) ReadPassiveEntry(TypedValue item)
    {
        var f = IndexFields(Fields(item));
        return (
            Str(f, "PassiveEffectName"),
            Bool(f, "IsLearnt"),
            Int(f, "LearntSteps"));
    }

    private static (string Id, int Level) ReadProgressionEntry(TypedValue item)
    {
        var f = IndexFields(Fields(item));
        return (Str(f, "DefinitionID"), Int(f, "CurrentLevel"));
    }

    private static List<GvasProperty> Fields(TypedValue item) =>
        item.Payload is GvasPayload.Struct st && st.Fields is not null
            ? st.Fields
            : throw new InvalidDataException("expected struct fields on progression entry");

    private static Dictionary<string, GvasProperty> IndexFields(List<GvasProperty> fields)
    {
        var map = new Dictionary<string, GvasProperty>(StringComparer.Ordinal);
        foreach (var f in fields)
        {
            var key = ShortKey(f.Name);
            if (key.Length > 0 && !map.ContainsKey(key)) map[key] = f;
        }
        return map;
    }

    public static Dictionary<string, GvasProperty> IndexFieldsPublic(List<GvasProperty> fields) =>
        IndexFields(fields);

    /// <summary>"PassiveEffectName_3_A92D…" → "PassiveEffectName".</summary>
    internal static string ShortKey(string name)
    {
        var us = name.IndexOf('_');
        return us > 0 ? name[..us] : name;
    }

    private static string Str(Dictionary<string, GvasProperty> f, string key) =>
        f.TryGetValue(key, out var p) && p.Value.Payload is string s ? s : "";

    private static bool Bool(Dictionary<string, GvasProperty> f, string key) =>
        f.TryGetValue(key, out var p) && p.Value.Payload is bool b && b;

    private static int Int(Dictionary<string, GvasProperty> f, string key) =>
        f.TryGetValue(key, out var p) && p.Value.Payload is int i ? i : 0;

    private static void SetField(List<GvasProperty> fields, string shortKey, TypedValue value)
    {
        var field = fields.First(f => ShortKey(f.Name).Equals(shortKey, StringComparison.Ordinal));
        field.Value = value;
    }

    private static void SetNameField(List<GvasProperty> fields, string id)
    {
        foreach (var field in fields.Where(f =>
                     ShortKey(f.Name) is "DefinitionID" or "PassiveEffectName" &&
                     f.Value.Payload is string))
            field.Value.Payload = id;
    }

    private static bool Matches(object? payload, string id) =>
        AsString(payload)?.Equals(id, StringComparison.OrdinalIgnoreCase) == true;

    private static string? AsString(object? payload) => payload as string;

    private static PropType? CloneType(PropType? t) => t is null ? null : new PropType
    {
        Kind = t.Kind,
        Inner = t.Inner is { } inner ? CloneType(inner) : null,
        Key = t.Key is { } key ? CloneType(key) : null,
        ValueMember = t.ValueMember is { } vm ? CloneType(vm) : null,
        TypeName = t.TypeName,
        TypePackage = t.TypePackage,
        StructId = t.StructId,
        EnumContainer = t.EnumContainer,
    };

    private static List<GvasProperty> CloneProperties(List<GvasProperty> source) =>
        source.Select(CloneProperty).ToList();

    private static GvasProperty CloneProperty(GvasProperty p) =>
        new(p.Name, new TypedValue(CloneType(p.Value.Type), ClonePayload(p.Value.Type.Kind, p.Value.Payload)));

    private static GvasPayload.Array CloneArray(GvasPayload.Array src)
    {
        var copy = new GvasPayload.Array { RawBytes = src.RawBytes };
        foreach (var i in src.Items)
            copy.Items.Add(new TypedValue(CloneType(i.Type), ClonePayload(i.Type.Kind, i.Payload)));
        return copy;
    }

    private static object ClonePayload(string kind, object? payload) => kind switch
    {
        "StructProperty" when payload is GvasPayload.Struct st => new GvasPayload.Struct
        {
            Builtin = st.Builtin,
            Fields = st.Fields is { } f ? CloneProperties(f) : null,
            Ticks = st.Ticks,
            Raw = st.Raw,
            Tags = st.Tags is { } tags ? new List<string>(tags) : null,
            NetId = st.NetId,
            SoftPath = st.SoftPath,
        },
        "ArrayProperty" when payload is GvasPayload.Array arr => CloneArray(arr),
        "MapProperty" when payload is List<MapEntry> map => map.Select(e => new MapEntry(
            new TypedValue(CloneType(e.Key.Type), ClonePayload(e.Key.Type.Kind, e.Key.Payload)),
            new TypedValue(CloneType(e.Value.Type), ClonePayload(e.Value.Type.Kind, e.Value.Payload)))).ToList(),
        _ => payload!,
    };

    /// <summary>Formats a tick count as a UTC timestamp for UI display.</summary>
    public static string DescribeTicks(ulong ticks)
    {
        try
        {
            var dt = new DateTime((long)ticks, DateTimeKind.Utc);
            return dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
        }
        catch
        {
            return "";
        }
    }
}

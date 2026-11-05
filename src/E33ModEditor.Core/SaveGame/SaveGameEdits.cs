namespace E33ModEditor.Core.SaveGame;

/// <summary>
/// Edit surface for the remaining save areas: characters, resources,
/// world flags, exploration and per-character skill lists. Field names are
/// matched by their short prefix (before the first underscore/GUID chunk) so
/// engine-side GUID decorations never leak into call sites.
/// </summary>
public static class SaveGameEdits
{
    // ───────────────────────── characters ─────────────────────────

    public const string CharactersKey = "CharactersCollection";

    public sealed record CharacterInfo(
        string Id, int Level, int ActionPoints, int LuminaFromConsumables,
        double Hp, double Mp, bool Excluded,
        int CurrentExperience, string Skin, string Face,
        List<string> Skills, List<string> EquippedPictos);

    public static List<CharacterInfo> Characters(GvasFile file)
    {
        var list = new List<CharacterInfo>();
        foreach (var e in MapEntries(file, CharactersKey))
        {
            var id = Str(e.Key.Payload);
            var fields = StructFields(e.Value.Payload);
            if (fields is null || id.Length == 0) continue;
            var idx = SaveGameEditor.IndexFieldsPublic(fields);
            list.Add(new CharacterInfo(
                id,
                Int(idx, "CurrentLevel"),
                Int(idx, "AvailableActionPoints"),
                Int(idx, "LuminaFromConsumables"),
                Dbl(idx, "CurrentHP"),
                Dbl(idx, "CurrentMP"),
                Bool(idx, "IsExcluded"),
                Int(idx, "CurrentExperience"),
                Customization(fields, "CharacterSkin"),
                Customization(fields, "CharacterFace"),
                StringArray(fields, "UnlockedSkills"),
                StringArray(fields, "EquippedPassiveEffects")));
        }
        return list;
    }

    /// <summary>Reads a name field out of the nested customization struct.</summary>
    internal static string Customization(List<GvasProperty> charFields, string shortKey)
    {
        var f = charFields.FirstOrDefault(x => SaveGameEditor.ShortKey(x.Name)
            .StartsWith("CharacterCustomization", StringComparison.OrdinalIgnoreCase));
        if (f?.Value.Payload is not GvasPayload.Struct st || st.Fields is null) return "";
        return st.Fields.FirstOrDefault(x => SaveGameEditor.ShortKey(x.Name)
            .StartsWith(shortKey, StringComparison.OrdinalIgnoreCase))
            ?.Value.Payload as string ?? "";
    }

    /// <summary>Mutates one character's struct; returns false when absent.</summary>
    public static bool EditCharacter(GvasFile file, string id, Action<CharacterEdit> apply)
    {
        foreach (var e in MapEntries(file, CharactersKey))
        {
            if (!Str(e.Key.Payload).Equals(id, StringComparison.OrdinalIgnoreCase)) continue;
            var fields = StructFields(e.Value.Payload)
                ?? throw new InvalidDataException($"character '{id}' has no struct fields");
            apply(new CharacterEdit(fields));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Adds a playable character to CharactersCollection when the save has no
    /// record yet (roster member who has not joined the expedition). The struct
    /// shape is cloned from an existing record so the layout always matches the
    /// retail writer; progression is reset to a fresh-recruit baseline: level 1,
    /// no XP/AP/lumina, default skin/face, <paramref name="starterSkill"/> only,
    /// no equipped gear. Vitals keep the template's values — the game
    /// recomputes maxima from level and attributes on load. Returns false when
    /// a record already exists.
    /// </summary>
    public static bool EnsureCharacterRecord(GvasFile file, string id, string? starterSkill = null)
    {
        var map = FindMapSafe(file, CharactersKey)
            ?? throw new InvalidDataException("save has no CharactersCollection map");
        if (map.Any(e => Str(e.Key.Payload).Equals(id, StringComparison.OrdinalIgnoreCase)))
            return false;

        var template = map.FirstOrDefault()
            ?? throw new InvalidDataException("save has no character record to clone");
        var fields = StructFields(template.Value.Payload)
            ?? throw new InvalidDataException("template character record has no struct fields");
        var clone = CloneFields(fields);

        foreach (var f in clone)
        {
            switch (Short(f.Name))
            {
                case "CharacterHardcodedName": f.Value.Payload = id; break;
                case "CurrentLevel": f.Value.Payload = 1; break;
                case "CurrentExperience": f.Value.Payload = 0; break;
                case "AvailableActionPoints": f.Value.Payload = 0; break;
                case "LuminaFromConsumables": f.Value.Payload = 0; break;
                case "IsExcluded": f.Value.Payload = false; break;
                case "EquippedPassiveEffects":
                    if (f.Value.Payload is GvasPayload.Array arr) arr.Items.Clear();
                    break;
                case "EquippedSkills":
                case "UnlockedSkills":
                    // Seed only the starter skill so the battle wheel is never
                    // empty; more can be added on the character page afterwards.
                    if (f.Value.Payload is GvasPayload.Array skills)
                    {
                        var elem = skills.Items.Count > 0 ? skills.Items[0].Type : null;
                        skills.Items.Clear();
                        if (elem is not null && !string.IsNullOrEmpty(starterSkill))
                            skills.Items.Add(new TypedValue(elem, starterSkill));
                    }
                    break;
                case "EquippedItemsPerSlot":
                    if (f.Value.Payload is List<MapEntry> gear) gear.Clear();
                    break;
                case "CharacterActions":
                    if (f.Value.Payload is List<MapEntry> actions) actions.Clear();
                    break;
                case "CharacterActionsOrder":
                    if (f.Value.Payload is GvasPayload.Array order) order.Items.Clear();
                    break;
            }
        }

        // Attribute points: keep the enum keys, zero the values.
        var attrField = clone.FirstOrDefault(x => Short(x.Name)
            .StartsWith("AssignedAttributePoints", StringComparison.OrdinalIgnoreCase));
        if (attrField?.Value.Payload is List<MapEntry> attrs)
            foreach (var a in attrs) a.Value.Payload = 0;

        // Default customization for this character.
        var custField = clone.FirstOrDefault(x => Short(x.Name)
            .StartsWith("CharacterCustomization", StringComparison.OrdinalIgnoreCase));
        if (custField?.Value.Payload is GvasPayload.Struct cust && cust.Fields is not null)
        {
            foreach (var cf in cust.Fields)
            {
                if (Short(cf.Name).StartsWith("CharacterSkin", StringComparison.OrdinalIgnoreCase))
                    cf.Value.Payload = $"Skin{id}_Default";
                else if (Short(cf.Name).StartsWith("CharacterFace", StringComparison.OrdinalIgnoreCase))
                    cf.Value.Payload = $"Face{id}_Default";
            }
        }

        map.Add(new MapEntry(
            new TypedValue(CloneTypeP(template.Key.Type)!, id),
            new TypedValue(CloneTypeP(template.Value.Type)!, new GvasPayload.Struct { Fields = clone })));
        return true;
    }

    private static string Short(string name)
    {
        var us = name.IndexOf('_');
        return us > 0 ? name[..us] : name;
    }

    public sealed class CharacterEdit(List<GvasProperty> fields)
    {
        public int Level { set => SetInt(fields, "CurrentLevel", value); }
        public int ActionPoints { set => SetInt(fields, "AvailableActionPoints", value); }
        public int LuminaFromConsumables { set => SetInt(fields, "LuminaFromConsumables", value); }
        public double Hp { set => SetDouble(fields, "CurrentHP", value); }
        public double Mp { set => SetDouble(fields, "CurrentMP", value); }
        public bool Excluded { set => SetBool(fields, "IsExcluded", value); }

        private int _currentExperience;
        public int CurrentExperience
        {
            get => _currentExperience;
            set { SetInt(fields, "CurrentExperience", value); _currentExperience = value; }
        }

        /// <summary>Sets the skin inside the nested customization struct.</summary>
        public string Skin
        {
            set => SetCustomization(fields, "CharacterSkin", value);
        }

        /// <summary>Sets the face inside the nested customization struct.</summary>
        public string Face
        {
            set => SetCustomization(fields, "CharacterFace", value);
        }

        /// <summary>Adds an unlocked skill (e.g. Monoco's transformed-weapon skills).</summary>
        public void AddSkill(string skill) => AddToNameArray(fields, "UnlockedSkills", skill);

        public void RemoveSkill(string skill) => RemoveFromNameArray(fields, "UnlockedSkills", skill);

        public List<string> Skills => StringArray(fields, "UnlockedSkills");

        /// <summary>Equipped pictos live in the EquippedPassiveEffects name array.</summary>
        public void EquipPictos(string pictosId) => AddToNameArray(fields, "EquippedPassiveEffects", pictosId);

        public void UnequipPictos(string pictosId) => RemoveFromNameArray(fields, "EquippedPassiveEffects", pictosId);

        public List<string> EquippedPictos => StringArray(fields, "EquippedPassiveEffects");

        public List<string> EquippedSkills => StringArray(fields, "EquippedSkills");

        public void SetEquippedSkills(IEnumerable<string> skills) =>
            WriteStringArray(fields, "EquippedSkills", skills.Take(6).ToList());

        private readonly List<GvasProperty> _fields = fields;
    }

    // ───────────────────────── equipped gear (weapon + 3 pictos) ─────────────────────────

    public const string ItemTypeWeapon = "E_jRPG_ItemType::NewEnumerator0";
    public const string ItemTypePictos = "E_jRPG_ItemType::NewEnumerator10";

    public sealed record EquipSlot(string ItemType, int SlotIndex, string ItemId);

    public static List<EquipSlot> EquipmentOf(GvasFile file, string charId)
    {
        var list = new List<EquipSlot>();
        var map = CharacterMap(file, charId, "EquippedItemsPerSlot");
        if (map is null) return list;
        foreach (var e in map)
        {
            if (ReadSlotKey(e.Key.Payload) is not { } key) continue;
            list.Add(new EquipSlot(key.Type, key.Index, Str(e.Value.Payload)));
        }
        return list;
    }

    /// <summary>Writes one equipment slot. Empty itemId removes the slot (unequip).
    /// Works on fresh records too: an empty map gets a canonical FEquipmentSlot entry.</summary>
    public static void SetEquipment(GvasFile file, string charId, string itemType, int slotIndex, string itemId)
    {
        var fields = CharacterFields(file, charId)
            ?? throw new InvalidDataException($"character '{charId}' not in save");
        var mapField = fields.FirstOrDefault(x => SaveGameEditor.ShortKey(x.Name)
            .StartsWith("EquippedItemsPerSlot", StringComparison.OrdinalIgnoreCase));
        if (mapField?.Value.Payload is not List<MapEntry> map)
            throw new InvalidDataException($"{charId} has no EquippedItemsPerSlot map");

        MapEntry? match = null;
        foreach (var e in map)
        {
            if (ReadSlotKey(e.Key.Payload) is { } key &&
                key.Type.Equals(itemType, StringComparison.OrdinalIgnoreCase) &&
                key.Index == slotIndex)
            {
                match = e;
                break;
            }
        }

        if (string.IsNullOrEmpty(itemId))
        {
            if (match is not null) map.Remove(match);
            return;
        }

        if (match is not null)
        {
            match.Value.Payload = itemId;
            return;
        }

        var template = map.FirstOrDefault(e =>
            ReadSlotKey(e.Key.Payload) is { } k &&
            k.Type.Equals(itemType, StringComparison.OrdinalIgnoreCase));
        if (template is not null)
        {
            var keyClone = (GvasPayload.Struct)ClonePayload(template.Key.Payload);
            WriteSlotKey(keyClone, itemType, slotIndex);
            map.Add(new MapEntry(
                new TypedValue(CloneTypeP(template.Key.Type)!, keyClone),
                new TypedValue(CloneTypeP(template.Value.Type) ?? PropType.Simple("NameProperty"), itemId)));
            return;
        }

        // Fresh records carry an empty equipment map: seed a canonical
        // FEquipmentSlot entry shaped like retail saves.
        map.Add(CanonicalEquipEntry(itemType, slotIndex, itemId));
    }

    private static MapEntry CanonicalEquipEntry(string itemType, int slotIndex, string itemId) => new(
        new TypedValue(new PropType
        {
            Kind = PropType.StructKind,
            TypeName = "FEquipmentSlot",
            TypePackage = "/Game/Gameplay/Inventory/FEquipmentSlot",
            StructId = Guid.Parse("4b8ac189-497e-a9e9-aefd-1487d521343a"),
        }, new GvasPayload.Struct
        {
            Fields = new List<GvasProperty>
            {
                new("ItemType_4_419B69C74E6D605B52FA82A76F128C96",
                    new TypedValue(new PropType { Kind = "ByteProperty", EnumContainer = "E_jRPG_ItemType" },
                        new GvasPayload.Byte { Label = itemType })),
                new("SlotIndex_7_4AC21CC043F87846335A25B9212005AB", GvasPayload.Int(slotIndex)),
            },
        }),
        new TypedValue(PropType.Simple("NameProperty"), itemId));

    /// <summary>
    /// Removes every loadout reference to a pictos so removing it from the
    /// inventory cannot leave dangling ids: equipped lumina names
    /// (EquippedPassiveEffects) and worn gear slots (EquippedItemsPerSlot).
    /// Characters missing those structures (locked/guest members) are skipped.
    /// </summary>
    public static void ClearPictosLoadout(GvasFile file, string pictosId)
    {
        foreach (var e in MapEntries(file, CharactersKey))
        {
            var fields = StructFields(e.Value.Payload);
            if (fields is null || Str(e.Key.Payload).Length == 0) continue;

            var luminaField = fields.FirstOrDefault(x => SaveGameEditor.ShortKey(x.Name)
                .StartsWith("EquippedPassiveEffects", StringComparison.OrdinalIgnoreCase));
            if (luminaField?.Value.Payload is GvasPayload.Array lumina)
                for (var i = lumina.Items.Count - 1; i >= 0; i--)
                    if ((lumina.Items[i].Payload as string ?? "")
                        .Equals(pictosId, StringComparison.OrdinalIgnoreCase))
                        lumina.Items.RemoveAt(i);

            var slotField = fields.FirstOrDefault(x => SaveGameEditor.ShortKey(x.Name)
                .StartsWith("EquippedItemsPerSlot", StringComparison.OrdinalIgnoreCase));
            if (slotField?.Value.Payload is not List<MapEntry> slots) continue;
            for (var i = slots.Count - 1; i >= 0; i--)
                if (ReadSlotKey(slots[i].Key.Payload) is { } key &&
                    key.Type.Equals(ItemTypePictos, StringComparison.OrdinalIgnoreCase) &&
                    Str(slots[i].Value.Payload).Equals(pictosId, StringComparison.OrdinalIgnoreCase))
                    slots.RemoveAt(i);
        }
    }

    public static Dictionary<string, int> AttributesOf(GvasFile file, string charId)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var map = CharacterMap(file, charId, "AssignedAttributePoints");
        if (map is null) return result;
        foreach (var e in map)
        {
            var label = ByteOrName(e.Key.Payload);
            if (label.Length == 0) continue;
            result[label] = e.Value.Payload is int i ? i : 0;
        }
        return result;
    }

    public static void SetAttribute(GvasFile file, string charId, string enumLabel, int value)
    {
        var map = CharacterMap(file, charId, "AssignedAttributePoints")
            ?? throw new InvalidDataException($"{charId} has no AssignedAttributePoints map");
        var entry = map.FirstOrDefault(e =>
            ByteOrName(e.Key.Payload).Equals(enumLabel, StringComparison.OrdinalIgnoreCase));
        if (entry is not null)
        {
            entry.Value.Payload = Math.Max(0, value);
            return;
        }
        var template = map.FirstOrDefault();
        map.Add(new MapEntry(
            new TypedValue(CloneTypeP(template?.Key.Type) ?? new PropType
            {
                Kind = "ByteProperty",
                EnumContainer = "ECharacterAttribute",
            }, new GvasPayload.Byte { Label = enumLabel }),
            new TypedValue(CloneTypeP(template?.Value.Type) ?? PropType.Simple("IntProperty"),
                Math.Max(0, value))));
    }

    private static List<GvasProperty>? CharacterFields(GvasFile file, string id)
    {
        foreach (var e in MapEntries(file, CharactersKey))
            if (Str(e.Key.Payload).Equals(id, StringComparison.OrdinalIgnoreCase))
                return StructFields(e.Value.Payload);
        return null;
    }

    private static List<MapEntry>? CharacterMap(GvasFile file, string charId, string shortKey)
    {
        var fields = CharacterFields(file, charId);
        var f = fields?.FirstOrDefault(x => SaveGameEditor.ShortKey(x.Name)
            .StartsWith(shortKey, StringComparison.OrdinalIgnoreCase));
        return f?.Value.Payload as List<MapEntry>;
    }

    private static (string Type, int Index)? ReadSlotKey(object? payload)
    {
        if (payload is not GvasPayload.Struct st || st.Fields is null) return null;
        var type = "";
        var index = 0;
        foreach (var f in st.Fields)
        {
            var k = SaveGameEditor.ShortKey(f.Name);
            if (k.StartsWith("ItemType", StringComparison.OrdinalIgnoreCase))
                type = ByteOrName(f.Value.Payload);
            else if (k.StartsWith("SlotIndex", StringComparison.OrdinalIgnoreCase) && f.Value.Payload is int i)
                index = i;
        }
        return (type, index);
    }

    private static void WriteSlotKey(GvasPayload.Struct key, string itemType, int slotIndex)
    {
        if (key.Fields is null) return;
        foreach (var f in key.Fields)
        {
            var k = SaveGameEditor.ShortKey(f.Name);
            if (k.StartsWith("ItemType", StringComparison.OrdinalIgnoreCase))
                f.Value.Payload = new GvasPayload.Byte { Label = itemType };
            else if (k.StartsWith("SlotIndex", StringComparison.OrdinalIgnoreCase))
                f.Value.Payload = slotIndex;
        }
    }

    // ───────────────────────── party ─────────────────────────

    /// <summary>All CurrentParty slots in save order (it is an array of structs, not a map).</summary>
    internal static List<GvasPayload.Struct>? PartySlots(GvasFile file) =>
        file.Properties.FirstOrDefault(p => p.Name.StartsWith("CurrentParty", StringComparison.Ordinal))
            ?.Value.Payload is GvasPayload.Array arr
        ? arr.Items.Select(i => i.Payload).OfType<GvasPayload.Struct>().ToList()
        : null;

    /// <summary>Party slots in save order (id + formation enum label).</summary>
    public static List<(string Id, string Formation)> Party(GvasFile file)
    {
        var result = new List<(string Id, string Formation)>();
        foreach (var slot in PartySlots(file) ?? new List<GvasPayload.Struct>())
        {
            var fields = slot.Fields;
            if (fields is null) continue;
            var idx = SaveGameEditor.IndexFieldsPublic(fields);
            var id = fields.FirstOrDefault(f => SaveGameEditor.ShortKey(f.Name)
                    .StartsWith("CharacterHardcodedName", StringComparison.OrdinalIgnoreCase))
                ?.Value.Payload as string ?? "";
            var formation = idx.TryGetValue("Formation", out var f) ? ByteOrName(f.Value.Payload) : "";
            result.Add((id, formation));
        }
        return result.Where(p => p.Id.Length > 0).ToList();
    }

    /// <summary>Byte enum label, falling back to a plain name/string payload.</summary>
    internal static string ByteOrName(object? payload) => payload switch
    {
        GvasPayload.Byte b => b.Label ?? "",
        string s => s,
        _ => "",
    };

    /// <summary>
    /// Adds/removes a character from CurrentParty. The Party of Five mod raises
    /// the game's cap to 5 active members; new slots clone the existing slot shape.
    /// </summary>
    public static bool SetPartyMember(GvasFile file, string id, bool inParty, int maxSlots = 5)
    {
        var prop = file.Properties.FirstOrDefault(p =>
            p.Name.StartsWith("CurrentParty", StringComparison.Ordinal));
        if (prop?.Value.Payload is not GvasPayload.Array arr)
        {
            if (!inParty) return false;
            arr = new GvasPayload.Array();
            prop = new GvasProperty("CurrentParty", new TypedValue(new PropType
            {
                Kind = "ArrayProperty",
                Inner = PropType.Struct(
                    "S_jRPG_PartySlot", "/Game/jRPGTemplate/Structures/S_jRPG_PartySlot",
                    Guid.Parse("33e23d64-4e13-c5f1-dc66-1ba24d213252")),
            }, arr));
            file.Set(prop);
        }

        static string NameOf(GvasProperty f) => SaveGameEditor.ShortKey(f.Name);
        static string? SlotNameOf(GvasPayload.Struct s) => s.Fields?.FirstOrDefault(f =>
            NameOf(f).StartsWith("CharacterHardcodedName", StringComparison.OrdinalIgnoreCase))
            ?.Value.Payload as string;

        if (!inParty)
        {
            var removed = false;
            for (var i = arr.Items.Count - 1; i >= 0; i--)
            {
                if (arr.Items[i].Payload is GvasPayload.Struct st &&
                    SlotNameOf(st)?.Equals(id, StringComparison.OrdinalIgnoreCase) == true)
                {
                    arr.Items.RemoveAt(i);
                    removed = true;
                }
            }
            return removed;
        }

        if (PartySlots(file)!.Any(s => SlotNameOf(s)?.Equals(id, StringComparison.OrdinalIgnoreCase) == true))
            return true;
        if (arr.Items.Count >= maxSlots) return false;

        GvasPayload.Struct template = arr.Items.Select(i => i.Payload).OfType<GvasPayload.Struct>()
            .FirstOrDefault() ?? new GvasPayload.Struct();
        var clonedFields = template.Fields is { } tf
            ? CloneFields(tf)
            : new List<GvasProperty>
            {
                new("CharacterHardcodedName_2_2A63D4C5433428900D69748563F50580", GvasPayload.Name(id)),
                new("Formation_5_710E67F044BACE578CEE51AF24DC58B0",
                    new TypedValue(new PropType { Kind = "ByteProperty", EnumContainer = "E_jRPG_FormationType" },
                        new GvasPayload.Byte { Label = "E_jRPG_FormationType::NewEnumerator0" })),
            };
        foreach (var f in clonedFields)
        {
            var key = SaveGameEditor.ShortKey(f.Name);
            if (key.StartsWith("CharacterHardcodedName", StringComparison.OrdinalIgnoreCase))
                f.Value.Payload = id;
            // Formation is a Byte enum on retail saves — keep the cloned Byte as-is.
        }
        var elemType = arr.Items.Count > 0
            ? arr.Items[0].Type
            : prop!.Value.Type.Inner!;
        arr.Items.Add(new TypedValue(elemType, new GvasPayload.Struct { Fields = clonedFields }));
        return true;
    }

    /// <summary>Replaces the active party with <paramref name="ids"/> in order.</summary>
    public static void SetParty(GvasFile file, IReadOnlyList<string> ids, int maxSlots = 5)
    {
        foreach (var id in Party(file).Select(p => p.Id).ToList())
            SetPartyMember(file, id, false, maxSlots);
        foreach (var id in ids)
            SetPartyMember(file, id, true, maxSlots);
    }

    /// <summary>Removes a character record and any active-party slot for it.</summary>
    public static bool RemoveCharacter(GvasFile file, string id)
    {
        var removed = false;
        if (FindMapSafe(file, CharactersKey) is { } map)
            removed = map.RemoveAll(e => Str(e.Key.Payload)
                .Equals(id, StringComparison.OrdinalIgnoreCase)) > 0;
        SetPartyMember(file, id, false);
        return removed;
    }

    // ───────────────────────── quests ─────────────────────────

    /// <summary>
    /// Writes one quest's top-level status and (optionally) its objective
    /// statuses. Status values use the E_QuestStatus enum index: 0 not
    /// started, 1 in progress, 2 complete, 3 failed/blocked.
    /// </summary>
    public static bool SetQuestStatus(GvasFile file, string questName, int topStatus,
        IReadOnlyDictionary<string, int>? objectives = null)
    {
        var map = FindMapSafe(file, "QuestStatuses");
        var entry = map?.FirstOrDefault(e => Str(e.Key.Payload)
            .Equals(questName, StringComparison.OrdinalIgnoreCase));
        if (entry?.Value.Payload is not GvasPayload.Struct quest || quest.Fields is null) return false;

        var status = quest.Fields.FirstOrDefault(f => SaveGameEditor.ShortKey(f.Name)
            .StartsWith("QuestStatus", StringComparison.OrdinalIgnoreCase));
        if (status is not null)
            status.Value.Payload = new GvasPayload.Byte
                { Label = $"E_QuestStatus::NewEnumerator{topStatus}" };

        if (objectives is null) return true;
        var objectivesField = quest.Fields.FirstOrDefault(f => SaveGameEditor.ShortKey(f.Name)
            .StartsWith("ObjectivesStatus", StringComparison.OrdinalIgnoreCase));
        if (objectivesField?.Value.Payload is not List<MapEntry> list) return true;

        foreach (var objective in list)
        {
            var key = Str(objective.Key.Payload);
            if (!objectives.TryGetValue(key, out var value))
            {
                // Objective keys are usually "9_MonocoStation"; accept the
                // numeric prefix as a shorthand so callers do not need the
                // exact suffix.
                var numeric = key.Split('_')[0];
                if (!int.TryParse(numeric, out var n)
                    || !objectives.TryGetValue(n.ToString(), out value))
                    continue;
            }
            objective.Value.Payload = new GvasPayload.Byte
                { Label = $"E_QuestStatus::NewEnumerator{value}" };
        }
        return true;
    }

    // ───────────────────────── map flags / travel ─────────────────────────

    /// <summary>Sets every key containing <paramref name="substring"/> to
    /// <paramref name="value"/>; returns how many entries changed.</summary>
    public static int SetMapFlagsContaining(GvasFile file, string mapName, string substring, bool value)
    {
        var map = FindMapSafe(file, mapName);
        if (map is null) return 0;
        var changed = 0;
        foreach (var e in map)
        {
            if (!Str(e.Key.Payload).Contains(substring, StringComparison.OrdinalIgnoreCase)) continue;
            e.Value.Payload = value;
            changed++;
        }
        return changed;
    }

    /// <summary>
    /// Sets a NamedIDsStates entry by the GUID printed on a <c>NID_*</c>
    /// progression asset (e.g. <c>NID_MonocoStation_StalactAttack</c>).
    /// The save key is UE's FGuid layout: four little-endian uint32 chunks,
    /// not <see cref="Guid.ToByteArray"/> order.
    /// </summary>
    public static bool SetNamedId(GvasFile file, string guidText, bool value)
    {
        var hex = guidText.Replace("-", "");
        if (hex.Length != 32) throw new ArgumentException($"not a GUID: {guidText}", nameof(guidText));
        var raw = new byte[16];
        for (var i = 0; i < 4; i++)
        {
            var chunk = Convert.ToUInt32(hex.Substring(i * 8, 8), 16);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(i * 4, 4), chunk);
        }

        var map = FindMapSafe(file, "NamedIDsStates");
        if (map is null) return false;
        foreach (var e in map)
        {
            if (e.Key.Payload is not GvasPayload.Struct key
                || !string.Equals(key.Builtin, "Guid", StringComparison.OrdinalIgnoreCase)
                || !key.Raw.AsSpan().SequenceEqual(raw))
                continue;
            e.Value.Payload = value;
            return true;
        }

        var template = map.FirstOrDefault();
        map.Add(new MapEntry(
            new TypedValue(CloneTypeP(template?.Key.Type)
                    ?? PropType.Struct("Guid", "/Script/CoreUObject"),
                new GvasPayload.Struct { Builtin = "Guid", Raw = raw }),
            new TypedValue(CloneTypeP(template?.Value.Type) ?? PropType.Simple("BoolProperty"),
                value)));
        return true;
    }

    /// <summary>
    /// Removes every entry from <c>GameDifficultyData.ActivatedChallengeIDs</c>
    /// (Enemy Life Multiplier, Damage Limit, …). Those are opt-in battle
    /// modifiers; generated story-point saves must not inherit them from a
    /// completed donor. Returns how many were cleared.
    /// </summary>
    public static int ClearActivatedChallenges(GvasFile file)
    {
        var difficulty = file.Properties.FirstOrDefault(p =>
            p.Name.StartsWith("GameDifficultyData", StringComparison.Ordinal))
            ?.Value.Payload as GvasPayload.Struct;
        if (difficulty?.Fields is null) return 0;
        var challenges = difficulty.Fields.FirstOrDefault(f => SaveGameEditor.ShortKey(f.Name)
            .StartsWith("ActivatedChallengeIDs", StringComparison.OrdinalIgnoreCase));
        if (challenges?.Value.Payload is not List<MapEntry> map) return 0;
        var count = map.Count;
        map.Clear();
        return count;
    }

    /// <summary>Adds a DT_LevelData row name to VisitedLevelRowNames when absent.</summary>
    public static void EnsureVisitedLevel(GvasFile file, string levelRowName)
    {
        var prop = file.Properties.FirstOrDefault(p =>
            p.Name.StartsWith("VisitedLevelRowNames", StringComparison.Ordinal));
        if (prop?.Value.Payload is not GvasPayload.Array arr) return;
        if (arr.Items.Any(i => Str(i.Payload).Equals(levelRowName, StringComparison.OrdinalIgnoreCase)))
            return;
        var elementType = arr.Items.FirstOrDefault()?.Type ?? PropType.Simple("NameProperty");
        arr.Items.Add(new TypedValue(CloneTypeP(elementType) ?? PropType.Simple("NameProperty"),
            levelRowName));
    }

    /// <summary>Ensures a spawn point is listed in UnlockedSpawnPoints.</summary>
    public static void UnlockSpawnPoint(GvasFile file, string levelAsset, string spawnTag)
    {
        var prop = file.Properties.FirstOrDefault(p =>
            p.Name.StartsWith("UnlockedSpawnPoints", StringComparison.Ordinal));
        if (prop?.Value.Payload is not GvasPayload.Array arr || arr.Items.Count == 0) return;

        static GvasPayload.Array? TagsOf(GvasPayload.Struct s) =>
            s.Fields is { } f && SaveGameEditor.IndexFieldsPublic(f)
                .TryGetValue("SpawnPointTags", out var p)
                ? p.Value.Payload as GvasPayload.Array
                : null;

        GvasPayload.Struct? FindLevel(string asset) =>
            arr.Items.Select(i => i.Payload).OfType<GvasPayload.Struct>().FirstOrDefault(s =>
                s.Fields is { } f && SaveGameEditor.IndexFieldsPublic(f)
                    .TryGetValue("LevelAssetName", out var p)
                    && Str(p.Value.Payload).Equals(asset, StringComparison.OrdinalIgnoreCase));

        // A tag item from any level is the shape/type template for a new tag.
        var tagTemplate = arr.Items.Select(i => i.Payload).OfType<GvasPayload.Struct>()
            .Select(TagsOf).Where(t => t is { Items.Count: > 0 })
            .Select(t => t!.Items[0]).FirstOrDefault();

        var level = FindLevel(levelAsset);
        if (level?.Fields is null)
        {
            var donor = arr.Items.Select(i => i.Payload).OfType<GvasPayload.Struct>()
                .FirstOrDefault(s => s.Fields is not null);
            if (donor?.Fields is null) return;
            var clone = CloneFields(donor.Fields);
            var cloneIdx = SaveGameEditor.IndexFieldsPublic(clone);
            if (!cloneIdx.TryGetValue("LevelAssetName", out var name)) return;
            name.Value.Payload = levelAsset;
            if (cloneIdx.TryGetValue("SpawnPointTags", out var tagsField)
                && tagsField.Value.Payload is GvasPayload.Array cloneTags)
                cloneTags.Items.Clear();
            level = new GvasPayload.Struct { Fields = clone };
            arr.Items.Add(new TypedValue(CloneTypeP(arr.Items[0].Type)!, level));
        }

        if (TagsOf(level) is not { } tags) return;
        if (tags.Items.Select(i => i.Payload).OfType<GvasPayload.Struct>()
            .Any(s => SaveGameEdits.GameplayTagOf(s)
                .Equals(spawnTag, StringComparison.OrdinalIgnoreCase)))
            return;

        var tagType = tagTemplate?.Type
            ?? PropType.Struct("GameplayTag", "/Script/GameplayTags");
        var tag = new GvasPayload.Struct
        {
            Fields = new List<GvasProperty>
            {
                new("TagName", GvasPayload.Name(spawnTag)),
            },
        };
        tags.Items.Add(new TypedValue(CloneTypeP(tagType) ?? tagType, tag));
    }

    /// <summary>Deep clone of a property list (structs, arrays and maps recurse).</summary>
    private static List<GvasProperty> CloneFields(List<GvasProperty> source) =>
        source.Select(CloneField).ToList();

    private static GvasProperty CloneField(GvasProperty p) =>
        new(p.Name, new TypedValue(CloneTypeP(p.Value.Type), ClonePayload(p.Value.Payload)));

    private static object ClonePayload(object? payload) => payload switch
    {
        GvasPayload.Struct st => new GvasPayload.Struct
        {
            Builtin = st.Builtin,
            Fields = st.Fields is { } f ? CloneFields(f) : null,
            Ticks = st.Ticks,
            Raw = st.Raw,
            Tags = st.Tags is { } tags ? new List<string>(tags) : null,
            NetId = st.NetId,
            SoftPath = st.SoftPath,
        },
        GvasPayload.Array arr => CloneArrayPayload(arr),
        List<MapEntry> map => map.Select(e => new MapEntry(
            new TypedValue(CloneTypeP(e.Key.Type), ClonePayload(e.Key.Payload)),
            new TypedValue(CloneTypeP(e.Value.Type), ClonePayload(e.Value.Payload)))).ToList(),
        _ => payload!,
    };

    private static GvasPayload.Array CloneArrayPayload(GvasPayload.Array src)
    {
        var copy = new GvasPayload.Array { RawBytes = src.RawBytes };
        foreach (var i in src.Items)
            copy.Items.Add(new TypedValue(CloneTypeP(i.Type), i.Payload is GvasPayload.Byte b
                ? new GvasPayload.Byte { Label = b.Label, Raw = b.Raw }
                : ClonePayload(i.Payload)));
        return copy;
    }

    private static void SetCustomization(List<GvasProperty> charFields, string shortKey, string value)
    {
        var f = charFields.FirstOrDefault(x => SaveGameEditor.ShortKey(x.Name)
            .StartsWith("CharacterCustomization", StringComparison.OrdinalIgnoreCase));
        if (f?.Value.Payload is not GvasPayload.Struct st || st.Fields is null)
            throw new InvalidDataException($"character has no customization struct for '{shortKey}'");
        var target = st.Fields.FirstOrDefault(x => SaveGameEditor.ShortKey(x.Name)
            .StartsWith(shortKey, StringComparison.OrdinalIgnoreCase));
        if (target is null)
            throw new InvalidDataException($"customization has no '{shortKey}' field");
        target.Value.Payload = value;
    }

    internal static void AddToNameArray(List<GvasProperty> fields, string shortKey, string value)
    {
        var arr = NameArray(fields, shortKey);
        if (!arr.Contains(value, StringComparer.Ordinal)) arr.Add(value);
        WriteStringArray(fields, shortKey, arr);
    }

    internal static void RemoveFromNameArray(List<GvasProperty> fields, string shortKey, string value)
    {
        var arr = NameArray(fields, shortKey);
        arr.RemoveAll(s => s.Equals(value, StringComparison.OrdinalIgnoreCase));
        WriteStringArray(fields, shortKey, arr);
    }

    private static List<string> NameArray(List<GvasProperty> fields, string shortKey)
    {
        var f = fields.FirstOrDefault(x => SaveGameEditor.ShortKey(x.Name)
            .StartsWith(shortKey, StringComparison.OrdinalIgnoreCase));
        if (f?.Value.Payload is GvasPayload.Array arr)
            return arr.Items.Select(i => i.Payload as string ?? "").ToList();
        throw new InvalidDataException($"{shortKey} array missing from character");
    }

    // ───────────────────────── inventory counts ─────────────────────────

    /// <summary>Current count of a stackable inventory item (0 when absent).</summary>
    public static int GetItemCount(GvasFile file, string id)
    {
        foreach (var e in MapEntries(file, "InventoryItems"))
            if (Str(e.Key.Payload).Equals(id, StringComparison.OrdinalIgnoreCase) &&
                e.Value.Payload is int i)
                return i;
        return 0;
    }

    /// <summary>Sets a stackable item's count; removes the row at 0.</summary>
    public static void SetItemCount(GvasFile file, string id, int count)
    {
        var inventory = FindMapSafe(file, "InventoryItems")
            ?? throw new InvalidDataException("save has no InventoryItems map");
        var entry = inventory.FirstOrDefault(e =>
            Str(e.Key.Payload).Equals(id, StringComparison.OrdinalIgnoreCase));
        if (count <= 0)
        {
            inventory.Remove(entry!);
            return;
        }
        if (entry is not null)
        {
            entry.Value.Payload = count;
            return;
        }
        var template = inventory.FirstOrDefault();
        inventory.Add(new MapEntry(
            new TypedValue(CloneTypeP(template?.Key.Type) ?? PropType.Simple("NameProperty"), id),
            new TypedValue(CloneTypeP(template?.Value.Type) ?? PropType.Simple("IntProperty"), count)));
    }

    // ───────────────────────── spawn / load point ─────────────────────────

    /// <summary>
    /// The spawn the game actually restores: LastUsedSavePoint.SpawnPointTag,
    /// falling back to SpawnPointTagToLoadAt (often Generic.Dynamic).
    /// </summary>
    public static string GetSpawnTag(GvasFile file)
    {
        var last = GameplayTagOf(NestedStruct(file, "LastUsedSavePoint", "SpawnPointTag"));
        if (last.Length > 0) return last;
        return GameplayTagOf(RootStruct(file, "SpawnPointTagToLoadAt"));
    }

    public static string GetSpawnLevelAsset(GvasFile file)
    {
        var fields = RootStruct(file, "LastUsedSavePoint")?.Fields;
        if (fields is null) return "";
        return fields.FirstOrDefault(f => SaveGameEditor.ShortKey(f.Name)
                .Equals("LevelAssetName", StringComparison.OrdinalIgnoreCase))
            ?.Value.Payload as string ?? "";
    }

    /// <summary>
    /// Writes the spawn tag on both load-at and last-used structs, and the
    /// matching level asset / MapToLoad when <paramref name="levelAsset"/> is set.
    /// </summary>
    public static void SetSpawn(GvasFile file, string tag, string? levelAsset)
    {
        SetGameplayTag(RootStruct(file, "SpawnPointTagToLoadAt"), tag);
        // Keep the camp-return tag aligned with the new spawn; otherwise a
        // story rewind can leave the return point in a later area.
        SetGameplayTag(RootStruct(file, "ReturnSpawnPointTag"), tag);
        var last = RootStruct(file, "LastUsedSavePoint");
        if (last is not null)
        {
            SetGameplayTag(NestedStruct(last, "SpawnPointTag"), tag);
            if (!string.IsNullOrEmpty(levelAsset))
                SetNameField(last.Fields, "LevelAssetName", levelAsset);
        }
        if (!string.IsNullOrEmpty(levelAsset))
        {
            var map = file.Properties.FirstOrDefault(p =>
                p.Name.StartsWith("MapToLoad", StringComparison.Ordinal) &&
                !p.Name.StartsWith("MapToLoadAfter", StringComparison.Ordinal));
            if (map is not null) map.Value.Payload = levelAsset;
        }
    }

    private static GvasPayload.Struct? RootStruct(GvasFile file, string prefix) =>
        file.Properties.FirstOrDefault(p => p.Name.StartsWith(prefix, StringComparison.Ordinal))
            ?.Value.Payload as GvasPayload.Struct;

    private static GvasPayload.Struct? NestedStruct(GvasFile file, string root, string nested)
    {
        var st = RootStruct(file, root);
        return st is null ? null : NestedStruct(st, nested);
    }

    private static GvasPayload.Struct? NestedStruct(GvasPayload.Struct parent, string nested)
    {
        if (parent.Fields is null) return null;
        return parent.Fields.FirstOrDefault(f => SaveGameEditor.ShortKey(f.Name)
                .StartsWith(nested, StringComparison.OrdinalIgnoreCase))
            ?.Value.Payload as GvasPayload.Struct;
    }

    private static string GameplayTagOf(GvasPayload.Struct? tag) =>
        tag?.Fields?.FirstOrDefault(f => f.Name.StartsWith("TagName", StringComparison.Ordinal))
            ?.Value.Payload as string ?? "";

    private static void SetGameplayTag(GvasPayload.Struct? tag, string value)
    {
        var field = tag?.Fields?.FirstOrDefault(f =>
            f.Name.StartsWith("TagName", StringComparison.Ordinal));
        if (field is not null) field.Value.Payload = value;
    }

    private static void SetNameField(List<GvasProperty>? fields, string shortKey, string value)
    {
        if (fields is null) return;
        var field = fields.FirstOrDefault(f => SaveGameEditor.ShortKey(f.Name)
            .Equals(shortKey, StringComparison.OrdinalIgnoreCase));
        if (field is not null) field.Value.Payload = value;
    }

    // ───────────────────────── scalars / resources ─────────────────────────

    public static long GetIntScalar(GvasFile file, string name) =>
        file.Properties.FirstOrDefault(p => p.Name.StartsWith(name, StringComparison.Ordinal))
            ?.Value.Payload is int i ? i : 0;

    public static void SetIntScalar(GvasFile file, string name, int value)
    {
        var prop = file.Properties.FirstOrDefault(p => p.Name.StartsWith(name, StringComparison.Ordinal));
        if (prop != null) prop.Value.Payload = value;
        else file.Set(new GvasProperty(name, GvasPayload.Int(value)));
    }

    public static double GetDoubleScalar(GvasFile file, string name) =>
        file.Properties.FirstOrDefault(p => p.Name.StartsWith(name, StringComparison.Ordinal))
            ?.Value.Payload is double d ? d : 0;

    public static void SetGold(GvasFile file, int value) => SetIntScalar(file, "Gold", value);
    public static int GetGold(GvasFile file) => (int)GetIntScalar(file, "Gold");
    public static void SetNgPlus(GvasFile file, int value) => SetIntScalar(file, "FinishedGameCount", value);
    public static int GetNgPlus(GvasFile file) => (int)GetIntScalar(file, "FinishedGameCount");
    public static void SetTimePlayed(GvasFile file, double hours) =>
        SetScalarPayload(file, "TimePlayed", hours);

    private static void SetScalarPayload(GvasFile file, string name, object payload)
    {
        var prop = file.Properties.FirstOrDefault(p => p.Name.StartsWith(name, StringComparison.Ordinal));
        if (prop != null) prop.Value.Payload = payload;
    }

    // ───────────────────────── name→bool maps ─────────────────────────

    public static List<(string Key, bool Value)> MapBools(GvasFile file, string mapName)
    {
        var outList = new List<(string, bool)>();
        foreach (var e in FindMapSafe(file, mapName) ?? new List<MapEntry>())
            outList.Add((Str(e.Key.Payload), BoolOf(e.Value.Payload)));
        return outList;
    }

    /// <summary>Toggles an existing entry; optionally inserts the key when missing.</summary>
    public static bool SetMapBool(GvasFile file, string mapName, string key, bool value, bool addIfMissing = true)
    {
        var map = FindMapSafe(file, mapName);
        if (map is null && !addIfMissing) return false;
        map ??= CreateMapBool(file, mapName);
        var entry = map.FirstOrDefault(e => Str(e.Key.Payload).Equals(key, StringComparison.OrdinalIgnoreCase));
        if (entry is not null)
        {
            entry.Value.Payload = value;
            return true;
        }
        if (!addIfMissing) return false;
        var template = map.FirstOrDefault();
        map.Add(new MapEntry(
            new TypedValue(CloneTypeP(template?.Key.Type) ?? PropType.Simple("NameProperty"), key),
            new TypedValue(CloneTypeP(template?.Value.Type) ?? PropType.Simple("BoolProperty"), value)));
        return true;
    }

    private static List<MapEntry>? FindMapSafe(GvasFile file, string prefix) =>
        SaveGameEditor.FindMap(file, prefix);

    private static List<MapEntry> CreateMapBool(GvasFile file, string name)
    {
        var map = new List<MapEntry>();
        file.Set(new GvasProperty(name, new TypedValue(new PropType
        {
            Kind = "MapProperty",
            Key = PropType.Simple("NameProperty"),
            ValueMember = PropType.Simple("BoolProperty"),
        }, map)));
        return map;
    }

    // ───────────────────────── exploration ─────────────────────────

    public static List<string> ExplorationCapacities(GvasFile file)
    {
        var fields = RootStructFields(file, "ExplorationProgression");
        var result = new List<string>();
        if (fields is null) return result;
        var idx = SaveGameEditor.IndexFieldsPublic(fields);
        foreach (var f in idx.Values)
        {
            if (f.Value.Payload is GvasPayload.Array arr && arr.Items.Count > 0 &&
                arr.Items[0].Payload is GvasPayload.Byte { Label: not null })
            {
                result.AddRange(arr.Items.Select(i => ((GvasPayload.Byte)i.Payload!).Label!));
            }
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static void SetFreeAimLevel(GvasFile file, int level)
    {
        var fields = RootStructFields(file, "ExplorationProgression");
        if (fields is null) throw new InvalidDataException("save has no ExplorationProgression");
        SetInt(fields, "FreeAimDamageLevel", level);
    }

    public static int GetFreeAimLevel(GvasFile file)
    {
        var fields = RootStructFields(file, "ExplorationProgression");
        if (fields is null) return 0;
        return Int(SaveGameEditor.IndexFieldsPublic(fields), "FreeAimDamageLevel");
    }

    private static List<GvasProperty>? RootStructFields(GvasFile file, string prefix) =>
        file.Properties.FirstOrDefault(p => p.Name.StartsWith(prefix, StringComparison.Ordinal))
            ?.Value.Payload is GvasPayload.Struct st ? st.Fields : null;

    // ───────────────────────── shared helpers ─────────────────────────

    internal static List<MapEntry> MapEntries(GvasFile file, string prefix) =>
        FindMapSafe(file, prefix) ?? new List<MapEntry>();

    internal static List<GvasProperty>? StructFields(object? payload) =>
        payload is MapEntry me ? Nested(me.Value.Payload) :
        payload is GvasPayload.Struct st ? st.Fields : null;

    private static List<GvasProperty>? Nested(object? payload) =>
        payload is GvasPayload.Struct st ? st.Fields : null;

    internal static string Str(object? payload) => payload as string ?? "";

    internal static bool BoolOf(object? payload) => payload is bool b && b;

    internal static int Int(Dictionary<string, GvasProperty> f, string key) =>
        f.TryGetValue(key, out var p) && p.Value.Payload is int i ? i : 0;

    internal static double Dbl(Dictionary<string, GvasProperty> f, string key) =>
        f.TryGetValue(key, out var p) && p.Value.Payload is double d ? d : 0;

    internal static bool Bool(Dictionary<string, GvasProperty> f, string key) =>
        f.TryGetValue(key, out var p) && p.Value.Payload is bool b && b;

    internal static void SetInt(List<GvasProperty> fields, string shortKey, int value) =>
        First(fields, shortKey).Value.Payload = value;

    internal static void SetDouble(List<GvasProperty> fields, string shortKey, double value) =>
        First(fields, shortKey).Value.Payload = value;

    internal static void SetBool(List<GvasProperty> fields, string shortKey, bool value) =>
        First(fields, shortKey).Value.Payload = value;

    private static GvasProperty First(List<GvasProperty> fields, string shortKey) =>
        fields.First(f => SaveGameEditor.ShortKey(f.Name).Equals(shortKey, StringComparison.Ordinal));

    internal static List<string> StringArray(List<GvasProperty> fields, string shortKey)
    {
        var f = fields.FirstOrDefault(x => SaveGameEditor.ShortKey(x.Name)
            .StartsWith(shortKey, StringComparison.OrdinalIgnoreCase));
        if (f?.Value.Payload is GvasPayload.Array arr)
            return arr.Items.Select(i => i.Payload as string ?? "").ToList();
        return new List<string>();
    }

    internal static void WriteStringArray(List<GvasProperty> fields, string shortKey, List<string> values)
    {
        var f = fields.First(x => SaveGameEditor.ShortKey(x.Name)
            .StartsWith(shortKey, StringComparison.OrdinalIgnoreCase));
        if (f.Value.Payload is not GvasPayload.Array arr)
            throw new InvalidDataException($"{shortKey} is not a string array");
        var elem = arr.Items.Count > 0
            ? CloneTypeP(arr.Items[0].Type) ?? PropType.Simple("NameProperty")
            : (CloneTypeP(f.Value.Type.Inner) ?? PropType.Simple("NameProperty"));
        arr.Items.Clear();
        foreach (var v in values)
            arr.Items.Add(new TypedValue(elem, v));
    }

    internal static PropType? CloneTypeP(PropType? t) => t is null ? null : new PropType
    {
        Kind = t.Kind,
        Inner = t.Inner is { } inner ? CloneTypeP(inner) : null,
        Key = t.Key is { } key ? CloneTypeP(key) : null,
        ValueMember = t.ValueMember is { } vm ? CloneTypeP(vm) : null,
        TypeName = t.TypeName,
        TypePackage = t.TypePackage,
        StructId = t.StructId,
        EnumContainer = t.EnumContainer,
    };
}

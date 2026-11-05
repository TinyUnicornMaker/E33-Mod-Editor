using System.Text.RegularExpressions;
using E33ModEditor.Core;

namespace E33ModEditor.App;

/// <summary>
/// Human labels for cooked enum values. DisplayNameMap assets are preferred;
/// the fallbacks match Expedition 33 v1.5.x enumerator names.
/// </summary>
public static class GameLabels
{
    public static string Humanize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var s = raw.Contains("::") ? DataTableEditor.StaticDecode(raw) : raw;
        s = DataTableEditor.StripSuffix(s);
        foreach (var prefix in new[] { "Item_", "Consumable_", "Pictos_", "Weapon_", "Enemy_" })
            if (s.StartsWith(prefix, StringComparison.Ordinal)) { s = s[prefix.Length..]; break; }
        s = s.Replace('_', ' ');
        s = Regex.Replace(s, "([a-z])([A-Z])", "$1 $2");
        return DisplayAlias(s);
    }

    public static string DisplayAlias(string name) => name switch
    {
        "Strength" => "Might",
        "Noah" => "Gustave",
        "Frey" => "Gustave",
        "Magical Attack" => "Free Aim Attack",
        "FreeAim" => "Free Aim",
        "BaseAttack" => "Base Attack",
        "StatusEffects" => "Status Effects",
        _ => name,
    };

    public static string FieldLabel(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        if (path.StartsWith("BaseStatsMultipliers.", StringComparison.Ordinal))
            return "Base Stat Multiplier";
        if (path.StartsWith("LevelCurve.", StringComparison.Ordinal))
        {
            var parts = path.Split('.');
            var lvl = parts.Length > 1 ? parts[1].Replace('_', ' ') : "";
            var field = parts.Length > 2 ? FieldLabel(parts[^1]) : "";
            return $"{lvl} {field}".Trim();
        }
        var leaf = path.Contains('.') ? path[(path.LastIndexOf('.') + 1)..] : path;
        return leaf switch
        {
            "Level1Lumina" => "Passive (Lvl 33)",
            "Level2Lumina" => "Passive (Lvl 66)",
            "Level3Lumina" => "Passive (Lvl 111)",
            "Level4Lumina" => "Passive (Lvl 222)",
            "Level5Lumina" => "Passive (Lvl 333)",
            "MagicalAttack" => "Free Aim Attack",
            "MagicalAttackMultiplier" => "Free Aim Attack Multiplier",
            "PhysicalAttack" => "Physical Attack",
            "PhysicalAttackMultiplier" => "Physical Attack Multiplier",
            "ChromaValue" => "Chroma Value",
            "ChromaValueMultiplier" => "Chroma Value Multiplier",
            "BaseDamageType" => "Base Damage Type",
            "FreeAimDamageType" => "Free Aim Damage Type",
            "HideInInventory" => "Hide in Inventory",
            "HideInLootPopup" => "Hide in Loot Popup",
            "Item_Type" => "Type",
            "Item_Subtype" => "Subtype",
            "Item_Rarity" => "Rarity",
            "DefinitionID" => "Definition ID",
            "Item_HardcodedName" => "Internal Name",
            "Item_BuyValue" => "Buy Value",
            "Item_SellValue" => "Sell Value",
            "LuminaCost" => "Lumina Cost",
            "LuminaUnlockStepsCount" => "Battles to Master",
            "Pictos_Data" => "Gear Data Asset",
            "Pictos_ItemStats" => "Leftover Item Stats",
            "BaseStatMultipliers" => "Stat Multiplier",
            _ => PrettyPath(path),
        };
    }

    public static string PrettyPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        if (path.StartsWith("LevelCurve.", StringComparison.Ordinal))
        {
            var parts = path.Split('.');
            var lvl = parts.Length > 1 ? parts[1].Replace('_', ' ') : "";
            var field = parts.Length > 2 ? Humanize(parts[^1]) : "";
            return $"{lvl} {field}".Trim();
        }
        if (path.StartsWith("Enemies.", StringComparison.Ordinal))
        {
            var parts = path.Split('.');
            var slot = parts.Length > 1 ? parts[1].Trim('[', ']') : "";
            var field = parts.Length > 2 ? Humanize(parts[^1]) : "";
            return $"Enemy {slot} {field}".Trim();
        }
        var last = path.Contains('.') ? path[(path.LastIndexOf('.') + 1)..] : path;
        if (last.Contains("::"))
            return Humanize(last);
        return Humanize(last);
    }

    public static string Decode(string? typeOrField, string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        if (raw.Contains("::"))
        {
            var decoded = DataTableEditor.StaticDecode(raw);
            if (decoded != raw) return Humanize(decoded);
        }

        var type = typeOrField ?? "";
        if (type.Contains("StatType") || type.Contains("ItemStats") || type.Contains("EnemyStats") ||
            type.Contains("BaseStats"))
        {
            var idx = DataTableEditor.EnumeratorIndex(raw);
            if (idx != null && Stats.TryGetValue(idx.Value, out var stat)) return stat;
        }

        var leaf = type.Contains('.') ? type[(type.LastIndexOf('.') + 1)..] : type;
        if (Fallbacks.TryGetValue(leaf, out var map) && map.TryGetValue(raw, out var friendly))
            return friendly;

        var fromAsset = DataTableEditor.StaticDecode(type.Length > 0 && !raw.Contains("::")
            ? $"{GuessType(leaf)}::{raw}"
            : raw);
        if (fromAsset != raw && !fromAsset.Contains("::")) return Humanize(fromAsset);

        var idx2 = DataTableEditor.EnumeratorIndex(raw);
        if (idx2 != null && Fallbacks.TryGetValue(leaf, out var byIdx) &&
            byIdx.TryGetValue($"NewEnumerator{idx2}", out var named))
            return named;

        return raw.StartsWith("NewEnumerator", StringComparison.Ordinal) ? raw : Humanize(raw);
    }

    public static string SegmentTitle(string seg) => seg switch
    {
        "" or "General" => "General",
        "EnemyScaling" => "Scaling Multipliers",
        "EnemyStats" => "Raw Stats",
        "CommonCharacterStats" => "Combat Modifiers",
        "InitialElementalAffinities" => "Elemental Affinities",
        "PossibleLoot" => "Loot Table",
        "Pictos_ItemStats" => "Stat Bonuses",
        "LuminaCost" => "Lumina",
        "BaseStatsMultipliers" => "Base Stat Multiplier",
        "AttributeScaling" => "Attribute Scaling",
        "LevelCurve" => "Level Curve (all weapons)",
        "Enemies" => "Enemies",
        "BattleBuffs" => "Passive Scripts",
        "Categories" => "Lumina Categories",
        "PortraitData" => "Portrait",
        "BattleAudioProfile" => "Battle Audio",
        _ => Humanize(seg),
    };

    public static readonly Dictionary<int, string> Stats = new()
    {
        [12] = "Invalid",
        [1] = "Max HP",
        [2] = "Max AP",
        [3] = "Physical Attack",
        [4] = "Physical Defense",
        [6] = "Critical Rate",
        [7] = "Speed",
        [10] = "Accuracy",
        [13] = "Free Aim Attack",
        [14] = "Magical Defense",
        [15] = "Mastery",
    };

    private static readonly Dictionary<string, Dictionary<string, string>> Fallbacks = new()
    {
        ["Item_Type"] = new()
        {
            ["NewEnumerator0"] = "Weapon",
            ["NewEnumerator6"] = "N/A",
            ["NewEnumerator7"] = "Consumable",
            ["NewEnumerator10"] = "Pictos",
            ["NewEnumerator11"] = "Key",
            ["NewEnumerator12"] = "Inventory",
            ["NewEnumerator14"] = "Shard",
            ["NewEnumerator15"] = "Gold",
            ["NewEnumerator16"] = "Character Customization",
            ["NewEnumerator17"] = "Skill Unlocker",
        },
        ["Item_Subtype"] = new()
        {
            ["NewEnumerator0"] = "Lune",
            ["NewEnumerator1"] = "Monoco",
            ["NewEnumerator2"] = "Sciel",
            ["NewEnumerator11"] = "Consumable",
            ["NewEnumerator14"] = "Maelle",
            ["NewEnumerator15"] = "Pictos",
            ["NewEnumerator16"] = "Gustave",
            ["NewEnumerator18"] = "Key",
            ["NewEnumerator19"] = "Inventory",
            ["NewEnumerator20"] = "Invalid",
            ["NewEnumerator21"] = "Verso",
            ["NewEnumerator22"] = "Journal",
            ["NewEnumerator23"] = "Music Record",
        },
        ["Item_Rarity"] = new()
        {
            ["NewEnumerator0"] = "Weapon",
            ["NewEnumerator1"] = "Red Core",
            ["NewEnumerator2"] = "Green Core",
            ["NewEnumerator3"] = "Blue Core",
            ["NewEnumerator4"] = "Legendary",
            ["NewEnumerator5"] = "N/A",
            ["NewEnumerator6"] = "Ancient",
        },
        ["BaseDamageType"] = Element,
        ["FreeAimDamageType"] = Element,
        ["BaseElementalType"] = Element,
        ["SkillType"] = new()
        {
            ["NewEnumerator0"] = "Invalid",
            ["NewEnumerator1"] = "Skill",
            ["NewEnumerator2"] = "Gradient Attack",
            ["NewEnumerator3"] = "Item",
        },
        ["TargetingType"] = Targeting,
        ["Consumable_TargetingType"] = Targeting,
        ["DynamicElementOverride"] = new()
        {
            ["NewEnumerator0"] = "None",
            ["NewEnumerator1"] = "Base Attack Element",
            ["NewEnumerator2"] = "Free Aim Element",
        },
        ["ECharacterAttribute"] = Attributes,
        ["AttributeScaling"] = Attributes,
        ["EAttributeScalingGrade"] = Grades,
        ["E_PassiveEffectCategory"] = PassiveCategories,
        ["Categories"] = PassiveCategories,
    };

    private static Dictionary<string, string> Attributes => new()
    {
        ["NewEnumerator0"] = "Vitality",
        ["NewEnumerator1"] = "Might",
        ["NewEnumerator2"] = "Intelligence",
        ["NewEnumerator3"] = "Agility",
        ["NewEnumerator4"] = "Defense",
        ["NewEnumerator5"] = "Luck",
        ["NewEnumerator6"] = "Invalid",
    };

    private static Dictionary<string, string> Grades => new()
    {
        ["NewEnumerator0"] = "Invalid",
        ["NewEnumerator1"] = "D",
        ["NewEnumerator2"] = "C",
        ["NewEnumerator3"] = "B",
        ["NewEnumerator4"] = "A",
        ["NewEnumerator5"] = "S",
    };

    private static Dictionary<string, string> Element => new()
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

    private static Dictionary<string, string> PassiveCategories => new()
    {
        ["NewEnumerator0"] = "Invalid",
        ["NewEnumerator2"] = "Free Aim",
        ["NewEnumerator3"] = "Base Attack",
        ["NewEnumerator4"] = "AP",
        ["NewEnumerator5"] = "Heal",
        ["NewEnumerator6"] = "Shield",
        ["NewEnumerator7"] = "Critical",
        ["NewEnumerator8"] = "Damage",
        ["NewEnumerator9"] = "Burn",
        ["NewEnumerator10"] = "Buff",
        ["NewEnumerator11"] = "Status Effects",
        ["NewEnumerator12"] = "Parry",
        ["NewEnumerator13"] = "Counter",
        ["NewEnumerator14"] = "Death",
        ["NewEnumerator15"] = "Solo",
        ["NewEnumerator16"] = "Defense",
        ["NewEnumerator17"] = "Speed",
        ["NewEnumerator18"] = "Mark",
        ["NewEnumerator19"] = "Debuff",
        ["NewEnumerator20"] = "Break",
        ["NewEnumerator21"] = "Items",
        ["NewEnumerator22"] = "Gradient",
        ["NewEnumerator23"] = "Element",
        ["NewEnumerator24"] = "Revive",
        ["NewEnumerator25"] = "Lifesteal",
        ["NewEnumerator26"] = "Luck",
    };

    private static Dictionary<string, string> Targeting => new()
    {
        ["NewEnumerator0"] = "Only Self",
        ["NewEnumerator1"] = "Single Enemy (Alive)",
        ["NewEnumerator2"] = "Single Ally (Alive)",
        ["NewEnumerator7"] = "All Enemies",
        ["NewEnumerator8"] = "All Allies",
        ["NewEnumerator12"] = "Single Ally (Dead)",
        ["NewEnumerator13"] = "All Allies (Dead)",
        ["NewEnumerator16"] = "Single Ally then Enemy",
        ["NewEnumerator17"] = "Single Ally (Any)",
        ["NewEnumerator18"] = "Single Enemy (Any)",
        ["NewEnumerator19"] = "All Enemies (Alive)",
    };

    public static IReadOnlyDictionary<string, string>? FallbackMap(string key) =>
        Fallbacks.TryGetValue(key, out var m) ? m : null;

    public static string GuessType(string leaf) => leaf switch
    {
        "Item_Type" => "E_jRPG_ItemType",
        "Item_Subtype" => "E_jRPG_ItemSubtype",
        "Item_Rarity" => "E_jRPG_ItemRarity",
        "BaseDamageType" or "FreeAimDamageType" or "BaseElementalType" => "EAttackType",
        "SkillType" => "ESkillType",
        "TargetingType" or "Consumable_TargetingType" => "E_jRPG_TargetingType",
        "DynamicElementOverride" => "ESkillElementOverride",
        "ECharacterAttribute" => "ECharacterAttribute",
        "EAttributeScalingGrade" => "EAttributeScalingGrade",
        "Categories" or "E_PassiveEffectCategory" => "E_PassiveEffectCategory",
        _ => leaf,
    };

    public static bool IsLuminaPath(string path) =>
        path is "Level1Lumina" or "Level2Lumina" or "Level3Lumina" or "Level4Lumina" or "Level5Lumina";

    public static bool IsDropdownPath(string path)
    {
        var leaf = path.Contains('.') ? path[(path.LastIndexOf('.') + 1)..] : path;
        return leaf is "Item_Type" or "Item_Subtype" or "Item_Rarity"
            or "BaseDamageType" or "FreeAimDamageType" or "BaseElementalType"
            or "HideInInventory" or "HideInLootPopup"
            or "SkillType" or "TargetingType" or "DynamicElementOverride";
    }

    public static string CharacterFromWeaponPicto(string? pictosData, string? subtype)
    {
        var p = pictosData ?? "";
        if (p.Contains("_Lune_", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("_Lune", StringComparison.OrdinalIgnoreCase)) return "Lune";
        if (p.Contains("_Maelle_", StringComparison.OrdinalIgnoreCase)) return "Maelle";
        if (p.Contains("_Sciel_", StringComparison.OrdinalIgnoreCase)) return "Sciel";
        if (p.Contains("_Monoco_", StringComparison.OrdinalIgnoreCase)) return "Monoco";
        if (p.Contains("_Verso_", StringComparison.OrdinalIgnoreCase)) return "Verso";
        if (p.Contains("_Gustave_", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("_Noah_", StringComparison.OrdinalIgnoreCase)) return "Gustave";
        return Decode("Item_Subtype", subtype ?? "");
    }

    public static string CharacterFromPath(string path)
    {
        var p = path.Replace('\\', '/');
        if (p.Contains("/Gustave/", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("/Noah/", StringComparison.OrdinalIgnoreCase)) return "Gustave";
        if (p.Contains("/Lune/", StringComparison.OrdinalIgnoreCase)) return "Lune";
        if (p.Contains("/Maelle/", StringComparison.OrdinalIgnoreCase)) return "Maelle";
        if (p.Contains("/Sciel/", StringComparison.OrdinalIgnoreCase)) return "Sciel";
        if (p.Contains("/Verso/", StringComparison.OrdinalIgnoreCase)) return "Verso";
        if (p.Contains("/Monoco/", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("/Transfo/", StringComparison.OrdinalIgnoreCase)) return "Monoco";
        if (p.Contains("/Julie/", StringComparison.OrdinalIgnoreCase)) return "Julie";
        if (p.Contains("/Consumable", StringComparison.OrdinalIgnoreCase)) return "Consumable";
        // Verso's skill DAs live in the shared NoahVerso/DA_Skills folder, so the
        // folder is not a character marker there — the asset stem is the marker.
        var stem = p[(p.LastIndexOf('/') + 1)..];
        if (stem.Contains("_Verso_", StringComparison.OrdinalIgnoreCase)) return "Verso";
        return "";
    }

    public static string? StringTableKey(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        // Flatten preview: "[/Game/...ST_Items.ST_Items] ITEM_Gelerim_Name"
        var bracket = text.LastIndexOf(']');
        if (text.StartsWith('[') && bracket > 0 && bracket + 1 < text.Length)
            return text[(bracket + 1)..].Trim();
        // Dump form: "StringTableEntry, /Game/..., ITEM_Gelerim_Name"
        if (text.StartsWith("StringTableEntry", StringComparison.OrdinalIgnoreCase))
        {
            var parts = text.Split(',');
            if (parts.Length >= 3) return parts[^1].Trim();
        }
        return null;
    }
}

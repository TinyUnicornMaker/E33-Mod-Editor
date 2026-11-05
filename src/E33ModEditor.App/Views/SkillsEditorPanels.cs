using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using E33ModEditor.App.ViewModels;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.App.Views;

internal static class SkillsEditorPanels
{
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));

    private const double LabelCol = 340;

    public static Control BuildGeneral(MainViewModel vm, RecordRow row)
    {
        var (name, desc, shortDesc) = vm.DescribeSkill(row);
        var parts = new List<Control>
        {
            PictosEditorPanels.Head("Skill"),
            new TextBlock
            {
                Text = name,
                Foreground = Gold,
                FontSize = PictosEditorPanels.FontTitle,
                FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
                TextWrapping = TextWrapping.Wrap,
            },
        };

        if (shortDesc.Length > 0)
        {
            parts.Add(new TextBlock
            {
                Text = shortDesc,
                Foreground = Cream,
                FontSize = PictosEditorPanels.FontLabel,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
            });
        }
        if (desc.Length > 0 && desc != shortDesc)
        {
            parts.Add(new TextBlock
            {
                Text = desc,
                Foreground = Cream,
                FontSize = PictosEditorPanels.FontLabel,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 8),
            });
        }

        parts.Add(PictosEditorPanels.Note(
            "AP, targeting and element live on the skill Data Asset. Damage, heals, Burn stacks and status durations live on DT_Balancing_MainCharacterSkills and are on the Numbers tab. Which status a skill applies or clears is still hardcoded in its Blueprint."));

        parts.Add(PictosEditorPanels.Head("Cost and targeting"));
        if (row.Fields.ContainsKey("APCost"))
            parts.Add(IntRow(vm, row, "APCost", "AP Cost", "Action points spent to use this skill."));
        EnumRow(vm, row, parts, "SkillType", "Type", "Skill, Gradient Attack, or item-style action.");
        EnumRow(vm, row, parts, "TargetingType", "Targeting", "Who the skill can be aimed at.");
        EnumRow(vm, row, parts, "BaseElementalType", "Element", "Damage element. Painter conversion pictos can still override this in battle.");
        EnumRow(vm, row, parts, "DynamicElementOverride", "Element override", "None, follow the weapon’s base attack element, or follow Free Aim.");

        parts.Add(PictosEditorPanels.Head("Skill tree"));
        var unlock = row.Baseline["UnlockCost"]?.ToString() ?? "—";
        var starting = row.Baseline["IsStartingSkill"]?.ToString() ?? "—";
        var secret = row.Baseline["IsSecret"]?.ToString() ?? "—";
        parts.Add(PictosEditorPanels.Meta("Unlock cost", unlock));
        parts.Add(PictosEditorPanels.Meta("Starting skill", starting));
        parts.Add(PictosEditorPanels.Meta("Secret", secret));
        parts.Add(PictosEditorPanels.Note("Unlock flags come from the character skill graph and are shown here for reference. They are not edited from this screen."));

        parts.Add(PictosEditorPanels.Head("Implementation"));
        parts.Add(PictosEditorPanels.Meta("Internal id", row.Baseline["NameID"]?.ToString() ?? row.RowName));
        parts.Add(PictosEditorPanels.Meta("Character", row.Baseline["_Character"]?.ToString() ?? "—"));
        parts.Add(PictosEditorPanels.Meta("Battle script", ScriptName(row)));
        parts.Add(PictosEditorPanels.Note(
            "Stain / stance / overcharge-on-buff components sit on this Data Asset but UAssetAPI cannot parse those Blueprint schemas yet, so stain generate/consume is not editable here."));

        return PictosEditorPanels.Page(parts.ToArray());
    }

    public static Control BuildNumbers(MainViewModel vm, RecordRow row)
    {
        var parts = new List<Control>
        {
            PictosEditorPanels.Head("Tunable combat values"),
        };

        var keys = BalancingKeys(row);
        if (keys.Count == 0)
        {
            parts.Add(PictosEditorPanels.Note(
                "This skill has no DT_Balancing_MainCharacterSkills rows matching its NameID. Consumable tints and a few odd skills store their numbers only in the Blueprint."));
            return PictosEditorPanels.Page(parts.ToArray());
        }

        parts.Add(PictosEditorPanels.Note(
            "The cooked Blueprint looks these up by name. Base Damage is a factor of the caster’s attack (1.0 = 100%). Multiplier values are factors (1.5 = 50% more). Increase values are added (0.5 = +50%). Percent / heal fields are usually 0–1 (0.5 = 50% of max HP). Durations are turns. Shared rows (QTE bonus, default Burn duration) apply to every skill that reads them."));

        var groups = new (string Title, List<string> Keys)[]
        {
            ("Damage", new List<string>()),
            ("Break", new List<string>()),
            ("Heal", new List<string>()),
            ("Status", new List<string>()),
            ("Other", new List<string>()),
        };
        foreach (var key in keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            groups[Bucket(key)].Keys.Add(key);

        foreach (var (title, list) in groups)
        {
            if (list.Count == 0) continue;
            parts.Add(PictosEditorPanels.Head(title));
            foreach (var key in list)
                parts.Add(IntRow(vm, row, "Balancing." + key, BalLabel(key), BalHint(key)));
        }

        return PictosEditorPanels.Page(parts.ToArray());
    }

    private static int Bucket(string key)
    {
        var tail = Tail(key).ToLowerInvariant();
        if (tail.Contains("heal") || tail.Contains("hp") || tail.Contains("revive")) return 2;
        if (tail.Contains("break")) return 1;
        if (tail.Contains("damage") || tail.Contains("dmg")) return 0;
        if (IsStatusTail(tail)) return 3;
        return 4;
    }

    private static bool IsStatusTail(string tail) =>
        tail.Contains("burn") || tail.Contains("duration") || tail.Contains("stack") ||
        tail.Contains("shield") || tail.Contains("shell") || tail.Contains("mark") ||
        tail.Contains("slow") || tail.Contains("rush") || tail.Contains("regen") ||
        tail.Contains("curse") || tail.Contains("powerful") || tail.Contains("powerless") ||
        tail.Contains("defens") || tail.Contains("apply") || tail.Contains("foretell") &&
        !tail.Contains("damage");

    private static string Tail(string key)
    {
        var i = key.IndexOf('_');
        return i >= 0 && i < key.Length - 1 ? key[(i + 1)..] : key;
    }

    private static string BalLabel(string key)
    {
        var rest = Tail(key);
        if (key.StartsWith("Generic_", StringComparison.Ordinal))
            return "Shared: " + GameLabels.Humanize(rest);
        return GameLabels.Humanize(rest);
    }

    private static string BalHint(string key)
    {
        var tail = Tail(key);
        if (key.StartsWith("Generic_DamageIncreaseForEachQTE", StringComparison.Ordinal))
            return "Shared. Added per successful QTE. 0.1 is +10%.";
        if (key.StartsWith("Generic_BurnDefaultDuration", StringComparison.Ordinal))
            return "Shared default Burn duration in turns.";
        if (tail.Contains("BaseDamage", StringComparison.OrdinalIgnoreCase))
            return "Skill power vs the caster’s attack. 1.0 is 100%; Stendhal vanilla is 15.";
        if (tail.Contains("BreakBar", StringComparison.OrdinalIgnoreCase))
            return "Break-bar damage factor.";
        if (tail.Contains("OverchargeDamage", StringComparison.OrdinalIgnoreCase))
            return "Factor when the skill overcharges. 1.5 is 50% more.";
        if (tail.Contains("Heal", StringComparison.OrdinalIgnoreCase) ||
            tail.Contains("Percent", StringComparison.OrdinalIgnoreCase))
            return "Usually a fraction of max HP. 0.5 is 50%.";
        if (tail.Contains("Duration", StringComparison.OrdinalIgnoreCase))
            return "Duration in turns.";
        if (tail.Contains("Burn", StringComparison.OrdinalIgnoreCase) ||
            tail.Contains("Stack", StringComparison.OrdinalIgnoreCase))
            return "Stacks applied. The status type itself is hardcoded on the Blueprint.";
        if (tail.Contains("Increase", StringComparison.OrdinalIgnoreCase))
            return "Added to 1. 0.5 is +50%.";
        if (tail.Contains("Multiplier", StringComparison.OrdinalIgnoreCase))
            return "Factor. 1.5 is 50% more; 3.0 is 200% more.";
        return "Value the skill Blueprint looks up from DT_Balancing_MainCharacterSkills.";
    }

    private static List<string> BalancingKeys(RecordRow row)
    {
        var list = new List<string>();
        if (row.Baseline["_BalancingKeys"] is JArray arr)
        {
            foreach (var x in arr)
                if (x != null) list.Add(x.ToString());
        }
        return list;
    }

    private static void EnumRow(MainViewModel vm, RecordRow row, List<Control> parts,
        string path, string label, string hint)
    {
        if (!row.Fields.ContainsKey(path)) return;
        var raw = row.AdvancedEdits.TryGetValue(path, out var adv)
            ? adv.ToString()
            : MainViewModel.TokenAt(row.Baseline, path)?.ToString() ?? "";
        var choices = vm.EnumChoices(path);
        if (choices.Count == 0)
        {
            parts.Add(IntRow(vm, row, path, label, hint));
            return;
        }
        var combo = WeaponEditorPanels.EnumCombo(choices, raw);
        combo.FontSize = PictosEditorPanels.FontInput;
        combo.MinHeight = 34;
        combo.SelectionChanged += (_, _) =>
        {
            var picked = WeaponEditorPanels.RawOf(combo);
            if (picked.Length == 0) return;
            vm.QueueEdit(row, path, picked);
        };
        parts.Add(FieldLine(label, combo, hint));
    }

    private static Control IntRow(MainViewModel vm, RecordRow row, string path, string label, string hint)
    {
        var raw = row.AdvancedEdits.TryGetValue(path, out var adv)
            ? adv.ToString()
            : MainViewModel.TokenAt(row.Baseline, path)?.ToString() ?? "";
        var box = new TextBox { Text = raw, FontSize = PictosEditorPanels.FontInput, MinHeight = 34 };
        WeaponEditorPanels.SizeToContent(box, 60, extra: 48, min: 100, max: 180);
        box.LostFocus += (_, _) =>
        {
            if (box.Text == raw) return;
            vm.QueueEdit(row, path, box.Text ?? "");
        };
        return FieldLine(label, box, hint);
    }

    private static Control FieldLine(string label, Control editor, string hint)
    {
        editor.VerticalAlignment = VerticalAlignment.Center;
        if (editor is ComboBox cb)
        {
            cb.FontSize = PictosEditorPanels.FontInput;
            cb.MinHeight = 34;
        }
        var grid = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse($"{LabelCol},Auto,*"),
            Margin = new Thickness(0, 2),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var lab = new TextBlock
        {
            Text = label,
            Foreground = Cream,
            FontSize = PictosEditorPanels.FontLabel,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
            TextWrapping = TextWrapping.NoWrap,
        };
        var tip = new TextBlock
        {
            Text = hint,
            Foreground = new SolidColorBrush(Color.Parse("#FF9C9280")),
            FontSize = PictosEditorPanels.FontHint,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetColumn(lab, 0);
        Grid.SetColumn(editor, 1);
        Grid.SetColumn(tip, 2);
        grid.Children.Add(lab);
        grid.Children.Add(editor);
        grid.Children.Add(tip);
        return grid;
    }

    private static string ScriptName(RecordRow row)
    {
        var s = row.Baseline["SkillScript"]?.ToString() ?? "";
        var slash = s.LastIndexOf('/');
        var comma = s.IndexOf(',');
        if (slash >= 0 && comma > slash) return s[(slash + 1)..comma].Trim();
        return string.IsNullOrEmpty(s) ? "—" : s;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia;
using E33ModEditor.App.ViewModels;
using E33ModEditor.Core;
using E33ModEditor.App;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.App.Views;

public partial class RecordEditorView : UserControl
{
    private static readonly (string Segment, string Title)[] CategoryOrder =
    {
        ("", "General"),
        ("EnemyScaling", "Scaling Multipliers"),
        ("EnemyStats", "Raw Stats"),
        ("Pictos_ItemStats", "Stat Bonuses"),
        ("BaseStatsMultipliers", "Base Stat Multiplier"),
        ("AttributeScaling", "Attribute Scaling"),
        ("LevelCurve", "Level Curve"),
        ("Enemies", "Enemies"),
        ("CommonCharacterStats", "Combat Modifiers"),
        ("InitialElementalAffinities", "Elemental Affinities"),
        ("PossibleLoot", "Loot Table"),
        ("BattleBuffs", "Passive Scripts"),
        ("SkillComponentList", "Skill Components"),
        ("Categories", "Lumina Categories"),
        ("PortraitData", "Portrait"),
        ("BattleAudioProfile", "Battle Audio"),
    };

    public event EventHandler? CloseRequested;

    public RecordEditorView() => InitializeComponent();

    public RecordEditorView(MainViewModel vm, RecordRow row)
    {
        InitializeComponent();
        var titleText = this.FindControl<TextBlock>("TitleText");
        if (titleText != null)
            titleText.Text = string.IsNullOrEmpty(row.DisplayName) ? row.RowName : row.DisplayName;
        var dirty = this.FindControl<TextBlock>("DirtyText");
        if (dirty != null && row.RowName != row.DisplayName)
            dirty.Text = row.RowName;
        _row = row;

        var leaves = vm.GetFlattenedLeaves(row);
        BuildTabs(vm, row, leaves);
    }

    private readonly RecordRow? _row;

    /// <summary>Pending textbox commits, flushed when the detail view closes.</summary>
    private readonly List<Action> _pendingCommits = new();

    /// <summary>
    /// Commits any textbox whose text changed since it was built or last
    /// committed. Called before the detail view is torn down so a click that
    /// never moves focus (no LostFocus) cannot silently drop a typed edit.
    /// </summary>
    public void CommitPending()
    {
        foreach (var commit in _pendingCommits.ToArray())
        {
            try { commit(); }
            catch { /* keep flushing the rest */ }
        }
    }

    private void BuildTabs(MainViewModel vm, RecordRow row,
        IReadOnlyList<DataTableEditor.PropertyLeaf> leaves)
    {
        var groups = new Dictionary<string, List<DataTableEditor.PropertyLeaf>>();
        foreach (var leaf in leaves)
        {
            var seg = leaf.Path.Contains('.')
                ? leaf.Path[..leaf.Path.IndexOf('.')]
                : "";
            if (!groups.TryGetValue(seg, out var list))
                groups[seg] = list = new List<DataTableEditor.PropertyLeaf>();
            list.Add(leaf);
        }

        if (vm.ActiveModule?.Id == "skills")
        {
            Tabs.Items.Add(new TabItem
            {
                Header = "General",
                Content = SkillsEditorPanels.BuildGeneral(vm, row),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            });
            Tabs.Items.Add(new TabItem
            {
                Header = "Numbers",
                Content = SkillsEditorPanels.BuildNumbers(vm, row),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            });
            return;
        }

        if (vm.ActiveModule?.Id == "pictos")
        {
            var general = leaves.Where(l => IsPictosGeneral(l.Path)).ToList();
            Tabs.Items.Add(new TabItem
            {
                Header = "General",
                Content = BuildPanel(vm, row, general, spacious: true),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            });
            Tabs.Items.Add(new TabItem
            {
                Header = "Stat Bonuses",
                Content = PictosEditorPanels.BuildStats(vm, row),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            });
            Tabs.Items.Add(new TabItem
            {
                Header = "Lumina",
                Content = PictosEditorPanels.BuildLumina(vm, row),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            });
            return;
        }

        if (row.Fields.ContainsKey("AttributeScaling") && !groups.ContainsKey("AttributeScaling"))
            groups["AttributeScaling"] = new List<DataTableEditor.PropertyLeaf>();
        if (vm.ActiveModule?.Id is "weapons" or "pictos-scaling" && !groups.ContainsKey("LevelCurve"))
            groups["LevelCurve"] = new List<DataTableEditor.PropertyLeaf>();

        // Monsters: the grid shows effective (archetype x scaling) HP/attack,
        // while the tabs below show raw table values. Surface the effective
        // numbers here too so a raw 1 HP / 0 ATK placeholder is not mistaken
        // for the in-game value.
        if (vm.ActiveModule?.Id == "monsters")
        {
            Tabs.Items.Add(new TabItem
            {
                Header = "Effective",
                Content = BuildEffectivePanel(row),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            });
        }

        string TitleFor(string seg)
        {
            foreach (var (s, title) in CategoryOrder)
                if (s == seg) return title;
            return GameLabels.SegmentTitle(seg);
        }

        int OrderFor(string seg)
        {
            for (int i = 0; i < CategoryOrder.Length; i++)
                if (CategoryOrder[i].Segment == seg) return i;
            return 100 + StringComparer.Ordinal.GetHashCode(seg) % 50;
        }

        var tabItems = groups
            .Select(kv => (seg: kv.Key, items: kv.Value))
            .OrderBy(g => OrderFor(g.seg))
            .Select(g =>
            {
                Control panel = g.seg switch
                {
                    "LevelCurve" when vm.ActiveModule?.Id == "weapon-scaling" =>
                        WeaponEditorPanels.BuildCurve(vm, row),
                    "LevelCurve" when vm.ActiveModule?.Id == "pictos-scaling" =>
                        PictosEditorPanels.BuildCurve(vm, row),
                    "LevelCurve" => WeaponEditorPanels.BuildWeaponPreview(vm, row),
                    "AttributeScaling" => WeaponEditorPanels.BuildScaling(vm, row),
                    _ => BuildPanel(vm, row, g.items),
                };
                return new TabItem
                {
                    Header = TitleFor(g.seg),
                    Content = panel,
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                };
            });

        foreach (var ti in tabItems) Tabs.Items.Add(ti);
    }

    private Control BuildPanel(MainViewModel vm, RecordRow row,
        IReadOnlyList<DataTableEditor.PropertyLeaf> leaves, bool spacious = false)
    {
        var stack = new StackPanel
        {
            Spacing = spacious ? 14 : 6,
            MaxWidth = spacious ? PictosEditorPanels.ContentWidth : double.PositiveInfinity,
            HorizontalAlignment = spacious
                ? Avalonia.Layout.HorizontalAlignment.Left
                : Avalonia.Layout.HorizontalAlignment.Stretch,
        };
        var subtype = row.Baseline["Item_Subtype"]?.ToString() ?? "";
        var labelSize = spacious ? PictosEditorPanels.FontLabel : 12;
        var inputSize = spacious ? PictosEditorPanels.FontInput : 12;
        var labelCol = spacious ? PictosEditorPanels.LabelCol : 260;

        foreach (var leaf in leaves)
        {
            var label = new TextBlock
            {
                Text = GameLabels.FieldLabel(leaf.Path),
                Foreground = new SolidColorBrush(Color.Parse("#FFEDE0C4")),
                FontSize = labelSize,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                MinWidth = labelCol,
                Margin = new Thickness(0, 0, 16, 0),
            };
            ToolTip.SetTip(label, leaf.Path);

            var rawText = leaf.Value?.ToString() ?? "";
            var shownText = vm.DecodeLeafForEdit(leaf);
            var forceReadOnly = leaf.Path is "Item_HardcodedName" or "DefinitionID";
            Control editor;

            if (GameLabels.IsLuminaPath(leaf.Path))
            {
                var combo = WeaponEditorPanels.PassiveCombo(vm, row, rawText);
                combo.SelectionChanged += (_, _) =>
                {
                    if (combo.SelectedItem is not ComboBoxItem { IsEnabled: true }) return;
                    vm.QueueEdit(row, leaf.Path, WeaponEditorPanels.RawOf(combo));
                };
                editor = combo;
            }
            else if (leaf.Kind == "bool" || leaf.Path is "HideInInventory" or "HideInLootPopup")
            {
                var combo = WeaponEditorPanels.BoolCombo(rawText);
                combo.SelectionChanged += (_, _) =>
                    vm.QueueEdit(row, leaf.Path, WeaponEditorPanels.RawOf(combo));
                editor = combo;
            }
            else if (leaf.Kind == "enum" || GameLabels.IsDropdownPath(leaf.Path))
            {
                var choices = vm.EnumChoices(leaf.Path);
                HashSet<string>? preferred = null;
                if (leaf.Path == "Item_Type")
                    preferred = vm.ActiveModule?.Id == "pictos"
                        ? new HashSet<string> { "NewEnumerator10" }
                        : new HashSet<string> { "NewEnumerator0" };
                else if (leaf.Path == "Item_Subtype")
                {
                    preferred = vm.ActiveModule?.Id == "pictos"
                        ? new HashSet<string> { "NewEnumerator15", "NewEnumerator16" }
                        : new HashSet<string>
                        {
                            "NewEnumerator16", "NewEnumerator0", "NewEnumerator14",
                            "NewEnumerator2", "NewEnumerator21", "NewEnumerator1",
                        };
                    if (subtype.Length > 0) preferred.Add(subtype);
                }
                if (choices.Count > 0)
                {
                    var combo = WeaponEditorPanels.EnumCombo(choices, rawText, preferred);
                    combo.SelectionChanged += (_, _) =>
                    {
                        var picked = WeaponEditorPanels.RawOf(combo);
                        if (picked.Length == 0) return;
                        vm.QueueEdit(row, leaf.Path, picked);
                    };
                    editor = combo;
                }
                else
                {
                    editor = MakeTextBox(vm, row, leaf, shownText, rawText, forceReadOnly, inputSize);
                }
            }
            else
            {
                editor = MakeTextBox(vm, row, leaf, shownText, rawText, forceReadOnly, inputSize);
            }

            editor.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            if (editor is ComboBox comboBox)
            {
                comboBox.FontSize = inputSize;
                if (spacious) comboBox.MinHeight = 34;
            }
            else if (editor is TextBox tb)
            {
                tb.FontSize = inputSize;
                if (spacious) tb.MinHeight = 34;
            }

            var rowGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions($"{labelCol},Auto"),
                Margin = new Thickness(0, spacious ? 2 : 1),
            };
            Grid.SetColumn(label, 0);
            Grid.SetColumn(editor, 1);
            rowGrid.Children.Add(label);
            rowGrid.Children.Add(editor);
            stack.Children.Add(rowGrid);
        }

        return new ScrollViewer
        {
            // Padding on the content: ScrollViewer.Padding is excluded from the
            // scrollable extent, which clipped the last fields.
            Content = new Border
            {
                Padding = spacious ? new Thickness(20, 16, 24, 24) : new Thickness(6, 10, 12, 10),
                Child = stack,
            },
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }

    private static bool IsPictosGeneral(string path) => path is
        "Item_HardcodedName" or "Item_Type" or "Item_Subtype" or "Item_Rarity"
        or "Item_BuyValue" or "Item_SellValue" or "HideInInventory" or "HideInLootPopup";

    /// <summary>
    /// Read-only effective stats for a monster: archetype(level) x scaling.
    /// Raw EnemyStats placeholders (HP 1 / ATK 0 on 153 late-game rows) are
    /// shown under Raw Stats; this tab shows what the fight actually uses.
    /// </summary>
    private static Control BuildEffectivePanel(RecordRow row)
    {
        static string Num(JToken? t) => t?.ToString() ?? "—";
        var effHp = Num(row.Baseline[E33ModEditor.Core.Loot.EnemyEffectiveStats.EffHpPath]);
        var effAtk = Num(row.Baseline[E33ModEditor.Core.Loot.EnemyEffectiveStats.EffAttackPath]);
        var effSpd = Num(row.Baseline[E33ModEditor.Core.Loot.EnemyEffectiveStats.EffSpeedPath]);
        var rawHp = Num(row.Baseline["EnemyStats"]?["E_jRPG_StatType::NewEnumerator1"]);
        var rawAtk = Num(row.Baseline["EnemyStats"]?["E_jRPG_StatType::NewEnumerator3"]);

        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock
        {
            Text = "In-game values = archetype(level) × Scaling Multipliers. "
                + "Raw Stats below are the table as cooked; 1 HP / 0 ATK rows are placeholders.",
            Foreground = new SolidColorBrush(Color.Parse("#FFB0B0B0")),
            FontSize = 12,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });
        foreach (var (label, eff, raw) in new[]
                 {
                     ("HP", effHp, rawHp),
                     ("Attack", effAtk, rawAtk),
                     ("Speed", effSpd, ""),
                 })
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("260,Auto") };
            var l = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Color.Parse("#FFEDE0C4")),
                FontSize = 12,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                MinWidth = 260,
                Margin = new Thickness(0, 0, 16, 0),
            };
            var v = new TextBlock
            {
                Text = raw.Length > 0 ? $"{eff}  (raw {raw})" : eff,
                FontSize = 12,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };
            ToolTip.SetTip(v, "Effective (in-game) with raw table value for reference");
            Grid.SetColumn(l, 0);
            Grid.SetColumn(v, 1);
            grid.Children.Add(l);
            grid.Children.Add(v);
            stack.Children.Add(grid);
        }
        return new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(6, 10, 12, 10), Child = stack },
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }

    private TextBox MakeTextBox(MainViewModel vm, RecordRow row,
        DataTableEditor.PropertyLeaf leaf, string shownText, string rawText, bool forceReadOnly = false,
        double fontSize = 12)
    {
        var longest = shownText ?? "";
        var cached = vm.LongestShown(leaf.Path, leaf.Kind);
        if (cached.Length > longest.Length) longest = cached;
        var editable = IsEditableKind(leaf.Kind) && !forceReadOnly;
        var box = new TextBox
        {
            Text = shownText,
            IsReadOnly = !editable,
            FontSize = fontSize,
            Watermark = editable ? "" : "(read-only)",
            Opacity = editable ? 1.0 : 0.55,
            TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
        };
        WeaponEditorPanels.SizeToContent(box, WeaponEditorPanels.ContentWidth(longest, fontSize), extra: 48, min: 80, max: 560);
        if (shownText != rawText)
            ToolTip.SetTip(box, $"raw: {rawText}");
        var capturedPath = leaf.Path;
        var lastCommitted = shownText;
        void TryCommit()
        {
            if (box.IsReadOnly) return;
            var text = box.Text ?? "";
            if (text == lastCommitted) return;
            lastCommitted = text;
            vm.QueueEdit(row, capturedPath, text);
        }
        box.LostFocus += (_, _) => TryCommit();
        // A click that does not move focus (e.g. straight to back/close)
        // would otherwise leave the typed value uncommitted.
        box.KeyDown += (_, e) =>
        {
            if (e.Key != Avalonia.Input.Key.Enter) return;
            TryCommit();
            e.Handled = true;
        };
        // Backstop for teardown paths that unload the control without a
        // LostFocus (detail close, module switch). Guarded: runs once.
        box.Unloaded += (_, _) => TryCommit();
        _pendingCommits.Add(TryCommit);
        return box;
    }

    private static bool IsEditableKind(string kind) =>
        kind is "bool" or "int" or "uint" or "float" or "str" or "name" or "enum";

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}

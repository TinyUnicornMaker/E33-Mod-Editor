using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using E33ModEditor.App.ViewModels;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.App.Views;

internal static class PictosEditorPanels
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#FF9C9280"));

    // Sized for 1080p; Avalonia DPI scaling carries this to 4K.
    internal const double ContentWidth = 1120;
    internal const double LabelCol = 280;
    internal const double FontLabel = 16;
    internal const double FontInput = 16;
    internal const double FontHint = 14;
    internal const double FontHead = 20;
    internal const double FontNote = 15;
    internal const double FontTitle = 26;

    private static readonly (string MultPath, string CurveField, string Label, string Hint)[] Mults =
    {
        (PictosCurve.HpMult, PictosCurve.Hp, "Max HP ×", "Multiplies the shared HP curve. 0 means this pictos grants no HP."),
        (PictosCurve.DefMult, PictosCurve.Defense, "Defense ×", "Multiplies the shared defense curve."),
        (PictosCurve.SpeedMult, PictosCurve.Speed, "Speed ×", "Multiplies the shared speed curve."),
        (PictosCurve.CritMult, PictosCurve.Crit, "Critical Rate ×", "Multiplies the shared crit-points curve (shown in-game as %)."),
        (PictosCurve.ChromaMult, PictosCurve.Chroma, "Chroma ×", "Multiplies the shared chroma value of the pictos."),
    };

    internal static Control Page(params Control[] children)
    {
        var stack = new StackPanel
        {
            Spacing = 14,
            MaxWidth = ContentWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            // Padding lives on the content (as margin), not the ScrollViewer:
            // a ScrollViewer Padding is dropped from the scrollable extent, so
            // the last section rendered past the extent and could never be
            // scrolled into view.
            Margin = new Thickness(20, 16, 24, 120),
        };
        foreach (var c in children) stack.Children.Add(c);
        var scroll = new ScrollViewer
        {
            Content = stack,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
        return new DockPanel { LastChildFill = true, Children = { scroll } };
    }

    /// <summary>
    /// Groups related controls into a bordered "card" (same look as the Skill
    /// Overhaul character cards) so long pages read as sections instead of one
    /// wall of fields.
    /// </summary>
    internal static Border Card(params Control[] children)
    {
        var inner = new StackPanel { Spacing = 8 };
        foreach (var c in children) inner.Children.Add(c);
        return new Border
        {
            Classes = { "panel" },
            Padding = new Thickness(20, 16),
            Child = inner,
        };
    }

    public static Control BuildStats(MainViewModel vm, RecordRow row)
    {
        var parts = new List<Control>
        {
            Note("Equipped bonuses scale with pictos level: bonus = shared curve(level) × this multiplier. Finding a higher-level copy of the same pictos raises the curve sample; the unique effect does not change. Edit the shared curve under Pictos Scaling."),
            Head("Stat multipliers"),
        };

        var boxes = new Dictionary<string, TextBox>();
        var preview = new TextBlock
        {
            FontFamily = new FontFamily("DejaVu Sans Mono, Consolas, monospace"),
            FontSize = FontLabel,
            Foreground = Cream,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
        };

        void RefreshPreview()
        {
            var lines = new List<string>
            {
                "Level     Max HP     Defense      Speed       Crit     Chroma",
            };
            foreach (var lvl in new[] { 1, 4, 10, 20, 33 })
            {
                var cells = new List<string> { $"{lvl,5}" };
                foreach (var spec in Mults)
                {
                    var curve = vm.SharedPictosCurve(spec.CurveField);
                    var v = lvl < curve.Length ? curve[lvl] : 0;
                    if (!TryNum(boxes[spec.MultPath].Text, out var m)) m = 0;
                    cells.Add($"{Math.Round(v * m),10:0}");
                }
                lines.Add(string.Join(" ", cells));
            }
            preview.Text = string.Join('\n', lines);
        }

        foreach (var spec in Mults)
        {
            var raw = ReadScalar(row, spec.MultPath);
            var box = new TextBox { Text = raw, FontSize = FontInput, MinHeight = 34 };
            WeaponEditorPanels.SizeToContent(box, 80, extra: 48, min: 120, max: 180);
            boxes[spec.MultPath] = box;
            box.LostFocus += (_, _) =>
            {
                vm.QueueEdit(row, spec.MultPath, box.Text ?? "0");
                RefreshPreview();
            };
            box.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty) RefreshPreview();
            };
            parts.Add(FieldLine(spec.Label, box, spec.Hint));
        }

        parts.Add(Head("Preview at common levels"));
        parts.Add(Note("Rounded to the nearest point, using the current shared curve."));
        parts.Add(preview);
        RefreshPreview();
        return Page(parts.ToArray());
    }

    public static Control BuildLumina(MainViewModel vm, RecordRow row)
    {
        var parts = new List<Control>();
        var key = PassiveKey(row);
        var (name, desc) = vm.DescribePassive(key.Length > 0 ? key : row.RowName);
        if (name.Length == 0 || name == row.RowName)
            name = row.DisplayName;

        parts.Add(Head("Effect"));
        parts.Add(new TextBlock
        {
            Text = name,
            Foreground = Gold,
            FontSize = FontTitle,
            FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
            TextWrapping = TextWrapping.Wrap,
        });
        if (desc.Length > 0)
        {
            parts.Add(new TextBlock
            {
                Text = desc,
                Foreground = Cream,
                FontSize = FontLabel,
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        var hasPassive = row.Fields.ContainsKey("LuminaCost");
        if (!hasPassive)
        {
            parts.Add(Note("This pictos has no DT_PassiveEffects row, so lumina cost and categories cannot be edited."));
        }
        else
        {
            parts.Add(IntRow(vm, row, "LuminaCost", "Lumina Cost",
                "Points spent to equip this as a lumina after it is mastered."));
            parts.Add(IntRow(vm, row, "LuminaUnlockStepsCount", "Battles to Master",
                "Battles fought with this pictos equipped before the lumina unlocks. Vanilla is 3 or 4."));
            parts.Add(Head("Categories"));
            parts.Add(BuildCategories(vm, row));
        }

        var balKeys = BalancingKeys(row);
        parts.Add(Head("Tunable effect values"));
        if (balKeys.Count == 0)
        {
            parts.Add(Note("This pictos has no DT_Balancing_Luminas row. Its Blueprint either has no numbers (Cheater, First Strike, …) or looks them up under a name that does not match this item id. Those cannot be retuned from here."));
        }
        else
        {
            parts.Add(Note("The cooked Blueprint reads these floats from DT_Balancing_Luminas. “Increase” values are added (0.5 = +50%). “Multiplier” values are factors (1.5 = 50% more damage; 3.0 = 200% more). Chance/percentage fields are usually 0–100 or 0–1 as labelled."));
            foreach (var balKey in balKeys)
            {
                var path = "Balancing." + balKey;
                parts.Add(IntRow(vm, row, path, BalLabel(balKey), BalHint(balKey)));
            }
        }

        var data = row.Baseline["Pictos_Data"]?.ToString() ?? "";
        var buffs = TokenBuffs(row);
        parts.Add(Head("Gameplay implementation"));
        parts.Add(Meta("Item data asset", string.IsNullOrEmpty(data) ? "—" : data));
        parts.Add(Meta("Battle blueprint", buffs.Length == 0 ? "—" : buffs));
        parts.Add(Note(
            "The unique mechanic is still a cooked Blueprint (BP_LU_*). You cannot invent a brand-new effect from scratch, but you can retune the numbers that Blueprint already reads. To get a different mechanic, pick another existing pictos whose Blueprint already does what you want."));

        return Page(parts.ToArray());
    }

    public static Control BuildCurve(MainViewModel vm, RecordRow row)
    {
        var series = PictosCurve.Series;
        var charts = new CurveChart[series.Length];
        var boxes = new TextBox[series.Length][];
        var colors = new[] { "#FFC9A961", "#FF8FBF7A", "#FF7EB8D4", "#FFE07A5A", "#FFC17A5A" };
        for (int s = 0; s < series.Length; s++)
        {
            charts[s] = new CurveChart(Color.Parse(colors[s]))
            {
                AxisXTitle = "Pictos Level",
                AxisYTitle = series[s].Label,
                VanillaName = "vanilla",
                PreviewName = "preview",
                IntegerValues = series[s].Integer,
                MinHeight = 160,
            };
            boxes[s] = new TextBox[PictosCurve.MaxLevel + 1];
        }

        var shape = new ComboBox { MinWidth = 140 };
        foreach (var sh in WeaponCurve.Shapes) shape.Items.Add(sh);
        shape.SelectedIndex = 0;
        var bias = new Slider { Minimum = -1, Maximum = 1, Value = 0, Width = 160 };
        var biasLabel = new TextBlock { Text = "pacing 0.00", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center };

        var starts = new TextBox[series.Length];
        var ends = new TextBox[series.Length];
        double[][] last = new double[series.Length][];
        var readout = new TextBlock
        {
            Text = "Move the pointer over a graph to read values at that pictos level.",
            Foreground = Cream, FontSize = 15, Margin = new Thickness(8, 4, 8, 2),
            TextWrapping = TextWrapping.Wrap,
        };

        string Fmt(double v) => Math.Round(v).ToString(CultureInfo.InvariantCulture);

        double[] Preview(int s)
        {
            if (!TryNum(starts[s].Text, out var a) || !TryNum(ends[s].Text, out var b))
                return series[s].Vanilla;
            return WeaponCurve.Generate(a, b, shape.SelectedItem as string ?? "Vanilla", bias.Value, series[s].Vanilla);
        }

        void UpdatePreview()
        {
            for (int s = 0; s < series.Length; s++)
            {
                last[s] = Preview(s);
                charts[s].SetCompare(series[s].Vanilla, last[s]);
            }
        }

        void ShowHover(int? lvl)
        {
            foreach (var c in charts) c.SetHover(lvl);
            if (lvl is not int n)
            {
                readout.Text = "Move the pointer over a graph to read values at that pictos level.";
                return;
            }
            readout.Text = $"Level {n}    " + string.Join("    ",
                Enumerable.Range(0, series.Length).Select(s => $"{series[s].Label} {Fmt(last[s][n])}"));
        }

        foreach (var c in charts) c.HoverChanged += (_, lvl) => ShowHover(lvl);

        for (int s = 0; s < series.Length; s++)
        {
            var cur = WeaponCurve.ReadSeries(row, series[s].Field);
            starts[s] = new TextBox { Text = Fmt(cur[1]), Width = 90, FontSize = 14 };
            ends[s] = new TextBox { Text = Fmt(cur[33]), Width = 90, FontSize = 14 };
            starts[s].PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) UpdatePreview(); };
            ends[s].PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) UpdatePreview(); };
        }
        shape.SelectionChanged += (_, _) => UpdatePreview();
        bias.PropertyChanged += (_, e) =>
        {
            if (e.Property != Slider.ValueProperty) return;
            biasLabel.Text = $"pacing {bias.Value:0.00}";
            UpdatePreview();
        };

        var genStack = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 4) };
        genStack.Children.Add(new TextBlock { Text = "Generate L1 → L33", Foreground = Muted, FontSize = 14 });
        for (int s = 0; s < series.Length; s++)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 2) };
            line.Children.Add(new TextBlock { Text = series[s].Label, Foreground = Cream, Width = 140, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 });
            line.Children.Add(starts[s]);
            line.Children.Add(new TextBlock { Text = "→", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center });
            line.Children.Add(ends[s]);
            genStack.Children.Add(line);
        }

        var apply = new Button { Content = "Apply generated curve to all stats" };
        apply.Click += (_, _) =>
        {
            UpdatePreview();
            for (int s = 0; s < series.Length; s++)
                WeaponCurve.WriteSeries(vm, row, series[s].Field, last[s], integer: true);
            SyncBoxes();
        };
        var reset = new Button { Content = "Reset to vanilla" };
        reset.Click += (_, _) =>
        {
            for (int s = 0; s < series.Length; s++)
                WeaponCurve.WriteSeries(vm, row, series[s].Field, series[s].Vanilla, integer: true);
            SyncBoxes();
        };

        var table = new Grid();
        table.ColumnDefinitions = ColumnDefinitions.Parse("80," + string.Join(",", Enumerable.Repeat("120", series.Length)));
        for (int i = 0; i <= PictosCurve.MaxLevel + 1; i++)
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var headGold = new SolidColorBrush(Color.Parse("#FFC9A961"));
        void HeadCell(string text, int col)
        {
            var t = new TextBlock { Text = text, FontWeight = FontWeight.Bold, Foreground = headGold, Margin = new Thickness(4), FontSize = 14 };
            Grid.SetRow(t, 0); Grid.SetColumn(t, col); table.Children.Add(t);
        }
        HeadCell("Level", 0);
        for (int s = 0; s < series.Length; s++) HeadCell(series[s].Label, s + 1);

        void SyncBoxes()
        {
            for (int s = 0; s < series.Length; s++)
            {
                var vals = WeaponCurve.ReadSeries(row, series[s].Field);
                starts[s].Text = Fmt(vals[1]);
                ends[s].Text = Fmt(vals[33]);
                for (int i = 0; i <= PictosCurve.MaxLevel; i++)
                    if (boxes[s][i] != null) boxes[s][i].Text = Fmt(vals[i]);
            }
            UpdatePreview();
        }

        for (int i = 0; i <= PictosCurve.MaxLevel; i++)
        {
            var lvl = i;
            var label = new TextBlock
            {
                Text = i.ToString(), VerticalAlignment = VerticalAlignment.Center,
                Foreground = i is 1 or 10 or 20 or 33 ? Gold : Cream,
                Margin = new Thickness(6, 0), FontSize = 14,
            };
            if (i == PictosCurve.MaxLevel) label.Margin = new Thickness(6, 0, 6, 28);
            Grid.SetRow(label, i + 1); Grid.SetColumn(label, 0); table.Children.Add(label);
            for (int s = 0; s < series.Length; s++)
            {
                var field = series[s].Field;
                var box = new TextBox { Text = "", FontSize = 14, Margin = new Thickness(2, 2, 2, i == PictosCurve.MaxLevel ? 28 : 2) };
                var si = s;
                box.LostFocus += (_, _) => vm.QueueEdit(row, $"LevelCurve.Level_{lvl}.{field}", box.Text ?? "0");
                Grid.SetRow(box, i + 1); Grid.SetColumn(box, s + 1);
                table.Children.Add(box);
                boxes[s][i] = box;
            }
        }

        var chartRow1 = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,*,*") };
        var chartRow2 = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,*") };
        for (int s = 0; s < 3; s++)
        {
            var block = new DockPanel { Margin = new Thickness(4, 0) };
            var cap = new TextBlock { Text = series[s].Label, Foreground = Gold, FontSize = 15, Margin = new Thickness(4) };
            DockPanel.SetDock(cap, Dock.Top);
            block.Children.Add(cap);
            block.Children.Add(charts[s]);
            Grid.SetColumn(block, s);
            chartRow1.Children.Add(block);
        }
        for (int s = 3; s < 5; s++)
        {
            var block = new DockPanel { Margin = new Thickness(4, 0) };
            var cap = new TextBlock { Text = series[s].Label, Foreground = Gold, FontSize = 15, Margin = new Thickness(4) };
            DockPanel.SetDock(cap, Dock.Top);
            block.Children.Add(cap);
            block.Children.Add(charts[s]);
            Grid.SetColumn(block, s - 3);
            chartRow2.Children.Add(block);
        }

        var top = new StackPanel { Spacing = 8, Margin = new Thickness(6, 8, 12, 8) };
        top.Children.Add(new TextBlock
        {
            Text = "Shared by every pictos (DT_EquipmentScalingTable_Pictos). Each pictos applies its own HP / Defense / Speed / Crit / Chroma multipliers on top of these raw values.",
            TextWrapping = TextWrapping.Wrap, Foreground = Muted, FontSize = 14,
        });
        top.Children.Add(chartRow1);
        top.Children.Add(chartRow2);
        top.Children.Add(readout);
        top.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 16,
            Children =
            {
                new TextBlock { Text = "dashed = vanilla", Foreground = Muted, FontSize = 14 },
                new TextBlock { Text = "solid = preview", Foreground = Muted, FontSize = 14 },
            },
        });
        top.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "Shape", Foreground = Muted, FontSize = 14, VerticalAlignment = VerticalAlignment.Center },
                shape, new TextBlock { Text = "front-loaded", Foreground = Muted, FontSize = 14, VerticalAlignment = VerticalAlignment.Center },
                bias, biasLabel,
            },
        });
        top.Children.Add(genStack);
        top.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Children = { apply, reset },
        });

        var tableScroll = new ScrollViewer
        {
            // Padding on the content (not the ScrollViewer): ScrollViewer.Padding
            // is dropped from the scrollable extent, clipping the last rows.
            Content = new Border { Padding = new Thickness(6, 4, 12, 72), Child = table },
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        var root = new Grid
        {
            RowDefinitions = RowDefinitions.Parse("Auto,*"),
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Grid.SetRow(top, 0);
        Grid.SetRow(tableScroll, 1);
        root.Children.Add(top);
        root.Children.Add(tableScroll);
        SyncBoxes();
        return root;
    }

    private static Control BuildCategories(MainViewModel vm, RecordRow row)
    {
        var wrap = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemWidth = 210,
            ItemHeight = 36,
        };
        var current = CurrentCategories(row);
        var boxes = new List<(string Raw, CheckBox Box)>();
        foreach (var (raw, label) in vm.EnumChoices("E_PassiveEffectCategory"))
        {
            var full = raw.Contains("::") ? raw : $"E_PassiveEffectCategory::{raw}";
            var box = new CheckBox
            {
                Content = label,
                IsChecked = current.Contains(full) || current.Contains(raw),
                Foreground = Cream,
                FontSize = FontLabel,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 16, 2),
            };
            boxes.Add((full, box));
            box.IsCheckedChanged += (_, _) =>
            {
                var arr = new JArray();
                foreach (var (r, b) in boxes)
                    if (b.IsChecked == true) arr.Add(r);
                vm.QueueToken(row, "Categories", arr);
            };
            wrap.Children.Add(box);
        }
        if (wrap.Children.Count == 0)
            wrap.Children.Add(new TextBlock { Text = "No category names loaded.", Foreground = Muted, FontSize = FontHint });
        return wrap;
    }

    private static HashSet<string> CurrentCategories(RecordRow row)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (row.AdvancedEdits.TryGetValue("Categories", out var adv) && adv is JArray edited)
        {
            foreach (var x in edited) if (x != null) set.Add(x.ToString());
            return set;
        }
        if (row.Baseline["Categories"] is JArray arr)
        {
            foreach (var x in arr) if (x != null) set.Add(x.ToString());
        }
        return set;
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

    private static string BalLabel(string key)
    {
        var i = key.IndexOf('_');
        var rest = i >= 0 && i < key.Length - 1 ? key[(i + 1)..] : key;
        return GameLabels.Humanize(rest);
    }

    private static string BalHint(string key)
    {
        if (key.Contains("ChancePercentage", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("ChancePercent", StringComparison.OrdinalIgnoreCase))
            return "Chance. Vanilla shot pictos use 20 for 20%.";
        if (key.Contains("Percentage", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Percent", StringComparison.OrdinalIgnoreCase))
            return "Fraction of max HP or similar. 0.5 is 50%.";
        if (key.Contains("Increase", StringComparison.OrdinalIgnoreCase))
            return "Added to 1. 0.5 is +50% damage; 2.0 is +200%.";
        if (key.Contains("Multiplier", StringComparison.OrdinalIgnoreCase))
            return "Factor. 1.5 is 50% more; 3.0 is 200% more.";
        if (key.Contains("Duration", StringComparison.OrdinalIgnoreCase))
            return "Duration in turns.";
        return "Value the Blueprint looks up for this pictos.";
    }

    private static string ReadScalar(RecordRow row, string path)
    {
        if (row.AdvancedEdits.TryGetValue(path, out var adv)) return adv.ToString();
        var tok = MainViewModel.TokenAt(row.Baseline, path);
        return tok is null || tok.Type == JTokenType.Null ? "0" : tok.ToString();
    }

    private static bool TryNum(string? text, out double d) =>
        double.TryParse((text ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d);

    private static Control IntRow(MainViewModel vm, RecordRow row, string path, string label, string hint)
    {
        var raw = MainViewModel.TokenAt(row.Baseline, path)?.ToString() ?? "";
        if (row.AdvancedEdits.TryGetValue(path, out var adv)) raw = adv.ToString();
        var box = new TextBox { Text = raw, FontSize = FontInput, MinHeight = 34 };
        WeaponEditorPanels.SizeToContent(box, 60, extra: 48, min: 100, max: 160);
        box.LostFocus += (_, _) =>
        {
            if (box.Text == raw) return;
            vm.QueueEdit(row, path, box.Text ?? "");
        };
        return FieldLine(label, box, hint, Cream);
    }

    internal static Control FieldLine(string label, Control editor, string hint, IBrush? labelBrush = null)
    {
        editor.VerticalAlignment = VerticalAlignment.Center;
        if (editor is ComboBox cb)
        {
            cb.FontSize = FontInput;
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
            Foreground = labelBrush ?? Cream,
            FontSize = FontLabel,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
            TextWrapping = TextWrapping.NoWrap,
        };
        var tip = new TextBlock
        {
            Text = hint,
            Foreground = Muted,
            FontSize = FontHint,
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

    public static string PassiveKey(RecordRow row)
    {
        var pdata = row.Baseline["Pictos_Data"]?.ToString() ?? "";
        if (pdata.StartsWith("DA_GearPictos_", StringComparison.Ordinal))
            return pdata["DA_GearPictos_".Length..];
        return row.RowName;
    }

    private static string TokenBuffs(RecordRow row)
    {
        var tok = MainViewModel.TokenAt(row.Baseline, "BattleBuffs") ?? row.Baseline["BattleBuffs"];
        if (tok is not JArray arr) return tok?.ToString() ?? "";
        var names = new List<string>();
        foreach (var x in arr)
        {
            var s = x?.ToString() ?? "";
            var slash = s.LastIndexOf('/');
            var comma = s.IndexOf(',');
            if (slash >= 0 && comma > slash)
                names.Add(s[(slash + 1)..comma].Trim());
            else if (s.Length > 0) names.Add(s);
        }
        return string.Join(", ", names);
    }

    internal static TextBlock Head(string t) => new()
    {
        Text = t, Foreground = Gold, FontSize = FontHead, FontWeight = FontWeight.SemiBold,
        FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
        Margin = new Thickness(0, 12, 0, 4),
    };

    internal static TextBlock Note(string t) => new()
    {
        Text = t, Foreground = Muted, FontSize = FontNote, TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Left,
        Margin = new Thickness(0, 0, 0, 8),
    };

    internal static Control Meta(string label, string value)
    {
        var line = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse($"{LabelCol},*"),
            Margin = new Thickness(0, 2),
        };
        var lab = new TextBlock
        {
            Text = label, Foreground = Muted, FontSize = FontLabel,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var val = new TextBlock
        {
            Text = value, Foreground = Cream, FontSize = FontLabel, TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(lab, 0);
        Grid.SetColumn(val, 1);
        line.Children.Add(lab);
        line.Children.Add(val);
        return line;
    }
}

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using E33ModEditor.App.ViewModels;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.App.Views;

internal static class WeaponEditorPanels
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush GoldDim = new SolidColorBrush(Color.Parse("#FF7E6A3F"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#FF9C9280"));
    private static readonly IBrush PhysBrush = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush AimBrush = new SolidColorBrush(Color.Parse("#FF7EB8D4"));
    private static readonly IBrush ChromaBrush = new SolidColorBrush(Color.Parse("#FFC17A5A"));

    private static readonly string[] Characters = { "Lune", "Maelle", "Sciel", "Verso", "Monoco" };

    public const string PhysMultPath = "BaseStatsMultipliers.E_jRPG_StatType::NewEnumerator3";
    public const string AimMultPath = "MagicalAttackMultiplier";
    public const string ChromaMultPath = "ChromaValueMultiplier";

    public static Control BuildCurve(MainViewModel vm, RecordRow row)
    {
        var physChart = MakeChart("#FFC9A961", "Physical Attack", vanillaName: "vanilla", previewName: "preview");
        var aimChart = MakeChart("#FF7EB8D4", "Free Aim Attack", vanillaName: "vanilla", previewName: "preview");
        var chromaChart = MakeChart("#FFC17A5A", "Chroma Value", vanillaName: "vanilla", previewName: "preview", integer: true);
        var physBoxes = new TextBox[WeaponCurve.MaxLevel + 1];
        var aimBoxes = new TextBox[WeaponCurve.MaxLevel + 1];
        var chromaBoxes = new TextBox[WeaponCurve.MaxLevel + 1];

        var shape = new ComboBox { MinWidth = 140 };
        foreach (var s in WeaponCurve.Shapes) shape.Items.Add(s);
        shape.SelectedIndex = 0;

        var bias = new Slider { Minimum = -1, Maximum = 1, Value = 0, Width = 160 };
        var biasLabel = new TextBlock { Text = "pacing 0.00", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center };

        TextBox Num(string text, double width = 90) => new()
        {
            Text = text, Width = width, FontSize = 14,
        };

        var pStart = Num(Fmt(WeaponCurve.ReadSeries(row, "PhysicalAttack")[1], false));
        var pEnd = Num(Fmt(WeaponCurve.ReadSeries(row, "PhysicalAttack")[33], false));
        var aStart = Num(Fmt(WeaponCurve.ReadSeries(row, "MagicalAttack")[1], false));
        var aEnd = Num(Fmt(WeaponCurve.ReadSeries(row, "MagicalAttack")[33], false));
        var cStart = Num(Fmt(WeaponCurve.ReadSeries(row, "ChromaValue")[1], true));
        var cEnd = Num(Fmt(WeaponCurve.ReadSeries(row, "ChromaValue")[33], true));

        double[] lastP = WeaponCurve.VanillaPhysical, lastA = WeaponCurve.VanillaPhysical, lastC = WeaponCurve.VanillaChroma;
        var readout = HoverReadout();

        double[] Preview(TextBox startBox, TextBox endBox, double[] vanilla)
        {
            if (!TryParse(startBox.Text, out var start) || !TryParse(endBox.Text, out var end))
                return vanilla;
            var shapeName = shape.SelectedItem as string ?? "Vanilla";
            return WeaponCurve.Generate(start, end, shapeName, bias.Value, vanilla);
        }

        void UpdatePreview()
        {
            lastP = Preview(pStart, pEnd, WeaponCurve.VanillaPhysical);
            lastA = Preview(aStart, aEnd, WeaponCurve.VanillaPhysical);
            lastC = Preview(cStart, cEnd, WeaponCurve.VanillaChroma);
            physChart.SetCompare(WeaponCurve.VanillaPhysical, lastP);
            aimChart.SetCompare(WeaponCurve.VanillaPhysical, lastA);
            chromaChart.SetCompare(WeaponCurve.VanillaChroma, lastC);
        }

        void ShowHover(int? lvl)
        {
            physChart.SetHover(lvl);
            aimChart.SetHover(lvl);
            chromaChart.SetHover(lvl);
            readout.Text = lvl is int n
                ? $"Level {n}    Physical {Fmt(lastP[n], false)}    Free Aim {Fmt(lastA[n], false)}    Chroma {Fmt(lastC[n], true)}"
                : "Move the pointer over a graph to read values at that weapon level.";
        }
        physChart.HoverChanged += (_, lvl) => ShowHover(lvl);
        aimChart.HoverChanged += (_, lvl) => ShowHover(lvl);
        chromaChart.HoverChanged += (_, lvl) => ShowHover(lvl);

        void SyncBoxes()
        {
            var p = WeaponCurve.ReadSeries(row, "PhysicalAttack");
            var a = WeaponCurve.ReadSeries(row, "MagicalAttack");
            var c = WeaponCurve.ReadSeries(row, "ChromaValue");
            for (int i = 0; i <= WeaponCurve.MaxLevel; i++)
            {
                if (physBoxes[i] != null) physBoxes[i].Text = Fmt(p[i], false);
                if (aimBoxes[i] != null) aimBoxes[i].Text = Fmt(a[i], false);
                if (chromaBoxes[i] != null) chromaBoxes[i].Text = Fmt(c[i], true);
            }
            UpdatePreview();
        }

        void ApplyOne(string field, TextBox startBox, TextBox endBox, double[] vanilla, bool integer)
        {
            if (!TryParse(startBox.Text, out var start) || !TryParse(endBox.Text, out var end))
            {
                vm.Status = "Start and end must be numbers.";
                return;
            }
            var shapeName = shape.SelectedItem as string ?? "Vanilla";
            var values = WeaponCurve.Generate(start, end, shapeName, bias.Value, vanilla);
            WeaponCurve.WriteSeries(vm, row, field, values, integer);
            SyncBoxes();
        }

        Button MakeApply(string title, Action act)
        {
            var b = new Button { Content = title };
            b.Click += (_, _) => act();
            return b;
        }

        void HookPreview(AvaloniaObject o, AvaloniaProperty prop)
        {
            o.PropertyChanged += (_, e) =>
            {
                if (e.Property == prop) UpdatePreview();
            };
        }
        HookPreview(pStart, TextBox.TextProperty);
        HookPreview(pEnd, TextBox.TextProperty);
        HookPreview(aStart, TextBox.TextProperty);
        HookPreview(aEnd, TextBox.TextProperty);
        HookPreview(cStart, TextBox.TextProperty);
        HookPreview(cEnd, TextBox.TextProperty);
        shape.SelectionChanged += (_, _) => UpdatePreview();
        bias.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name != nameof(Slider.Value)) return;
            biasLabel.Text = $"pacing {bias.Value:+0.00;-0.00;0.00}";
            UpdatePreview();
        };

        var gen = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("140,90,90,Auto"),
            RowDefinitions = RowDefinitions.Parse("Auto,Auto,Auto,Auto"),
            Margin = new Thickness(0, 8, 0, 4),
        };
        void Cell(Control c, int r, int col)
        {
            Grid.SetRow(c, r); Grid.SetColumn(c, col); gen.Children.Add(c);
        }
        Cell(Hint("Series"), 0, 0);
        Cell(Hint("Level 1"), 0, 1);
        Cell(Hint("Level 33"), 0, 2);
        Cell(Tint("Physical Attack", PhysBrush), 1, 0);
        Cell(pStart, 1, 1); Cell(pEnd, 1, 2);
        Cell(MakeApply("Apply", () => ApplyOne("PhysicalAttack", pStart, pEnd, WeaponCurve.VanillaPhysical, false)), 1, 3);
        Cell(Tint("Free Aim Attack", AimBrush), 2, 0);
        Cell(aStart, 2, 1); Cell(aEnd, 2, 2);
        Cell(MakeApply("Apply", () => ApplyOne("MagicalAttack", aStart, aEnd, WeaponCurve.VanillaPhysical, false)), 2, 3);
        Cell(Tint("Chroma Value", ChromaBrush), 3, 0);
        Cell(cStart, 3, 1); Cell(cEnd, 3, 2);
        Cell(MakeApply("Apply", () => ApplyOne("ChromaValue", cStart, cEnd, WeaponCurve.VanillaChroma, true)), 3, 3);

        var applyAll = MakeApply("Apply All", () =>
        {
            ApplyOne("PhysicalAttack", pStart, pEnd, WeaponCurve.VanillaPhysical, false);
            ApplyOne("MagicalAttack", aStart, aEnd, WeaponCurve.VanillaPhysical, false);
            ApplyOne("ChromaValue", cStart, cEnd, WeaponCurve.VanillaChroma, true);
        });
        var reset = MakeApply("Reset Vanilla", () =>
        {
            WeaponCurve.WriteSeries(vm, row, "PhysicalAttack", WeaponCurve.VanillaPhysical, false);
            WeaponCurve.WriteSeries(vm, row, "MagicalAttack", WeaponCurve.VanillaPhysical, false);
            WeaponCurve.WriteSeries(vm, row, "ChromaValue", WeaponCurve.VanillaChroma, true);
            pStart.Text = Fmt(WeaponCurve.VanillaPhysical[1], false);
            pEnd.Text = Fmt(WeaponCurve.VanillaPhysical[33], false);
            aStart.Text = Fmt(WeaponCurve.VanillaPhysical[1], false);
            aEnd.Text = Fmt(WeaponCurve.VanillaPhysical[33], false);
            cStart.Text = Fmt(WeaponCurve.VanillaChroma[1], true);
            cEnd.Text = Fmt(WeaponCurve.VanillaChroma[33], true);
            shape.SelectedIndex = 0;
            bias.Value = 0;
            SyncBoxes();
        });

        var table = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("70,*,*,*"),
        };
        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (int i = 0; i <= WeaponCurve.MaxLevel; i++)
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        table.RowDefinitions.Add(new RowDefinition(new GridLength(80)));

        void Head(string text, int col, IBrush? brush = null)
        {
            var t = new TextBlock
            {
                Text = text, FontWeight = FontWeight.Bold, Foreground = brush ?? Gold,
                Margin = new Thickness(4, 4), FontSize = 14,
            };
            Grid.SetRow(t, 0); Grid.SetColumn(t, col); table.Children.Add(t);
        }
        Head("Level", 0);
        Head("Physical Attack", 1, PhysBrush);
        Head("Free Aim Attack", 2, AimBrush);
        Head("Chroma Value", 3, ChromaBrush);

        TextBox CellBox(int r, int col, string text, Action<string> commit)
        {
            var box = new TextBox { Text = text, FontSize = 14, Margin = new Thickness(2) };
            box.LostFocus += (_, _) => commit(box.Text ?? "");
            Grid.SetRow(box, r); Grid.SetColumn(box, col);
            table.Children.Add(box);
            return box;
        }

        var p = WeaponCurve.ReadSeries(row, "PhysicalAttack");
        var a = WeaponCurve.ReadSeries(row, "MagicalAttack");
        var ch = WeaponCurve.ReadSeries(row, "ChromaValue");
        for (int i = 0; i <= WeaponCurve.MaxLevel; i++)
        {
            var lvl = i;
            var label = new TextBlock
            {
                Text = i == 0 ? "0" : i.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = i is 4 or 10 or 20 or 33 ? Gold : Cream,
                Margin = new Thickness(6, 0),
                FontSize = 14,
            };
            Grid.SetRow(label, i + 1); Grid.SetColumn(label, 0); table.Children.Add(label);

            physBoxes[i] = CellBox(i + 1, 1, Fmt(p[i], false), v =>
                vm.QueueEdit(row, $"LevelCurve.Level_{lvl}.PhysicalAttack", v));
            aimBoxes[i] = CellBox(i + 1, 2, Fmt(a[i], false), v =>
                vm.QueueEdit(row, $"LevelCurve.Level_{lvl}.MagicalAttack", v));
            chromaBoxes[i] = CellBox(i + 1, 3, Fmt(ch[i], true), v =>
                vm.QueueEdit(row, $"LevelCurve.Level_{lvl}.ChromaValue", v));
            if (i == WeaponCurve.MaxLevel)
            {
                label.Margin = new Thickness(6, 0, 6, 28);
                physBoxes[i].Margin = new Thickness(2, 2, 2, 28);
                aimBoxes[i].Margin = new Thickness(2, 2, 2, 28);
                chromaBoxes[i].Margin = new Thickness(2, 2, 2, 28);
            }
        }

        var top = new StackPanel { Spacing = 8, Margin = new Thickness(6, 8, 12, 8) };
        top.Children.Add(new TextBlock
        {
            Text = "Shared by every weapon (DT_WeaponScalingTable). Individual weapons apply their own Physical, Free Aim and Chroma multipliers on top of these raw values.",
            TextWrapping = TextWrapping.Wrap, Foreground = Muted, FontSize = 12,
        });
        var chartRow = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,*,*") };
        ChartBlock(chartRow, 0, "Physical Attack", PhysBrush, physChart);
        ChartBlock(chartRow, 1, "Free Aim Attack", AimBrush, aimChart);
        ChartBlock(chartRow, 2, "Chroma Value", ChromaBrush, chromaChart);
        top.Children.Add(chartRow);
        top.Children.Add(readout);
        top.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 16,
            Children =
            {
                Hint("dashed = vanilla"),
                Hint("solid = preview"),
                Tint("red fill = difference", new SolidColorBrush(Color.Parse("#FFE24B3A"))),
            }
        });
        top.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Hint("Shape"), shape,
                Hint("front-loaded"), bias, Hint("back-loaded"), biasLabel,
            }
        });
        top.Children.Add(gen);
        top.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Children = { applyAll, reset },
        });

        var tableScroll = new ScrollViewer
        {
            // Padding on the content: ScrollViewer.Padding is excluded from the
            // scrollable extent, clipping the last rows.
            Content = new Border { Padding = new Thickness(6, 4, 12, 72), Child = table },
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Visible,
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
        UpdatePreview();
        return root;
    }

    public static Control BuildWeaponPreview(MainViewModel vm, RecordRow row)
    {
        var physChart = MakeChart("#FFC9A961", "Physical Attack", vanillaName: "shared", previewName: "this weapon");
        var aimChart = MakeChart("#FF7EB8D4", "Free Aim Attack", vanillaName: "shared", previewName: "this weapon");
        var chromaChart = MakeChart("#FFC17A5A", "Chroma Value", vanillaName: "shared", previewName: "this weapon", integer: true);

        var pMult = NumBox(ReadMult(row, PhysMultPath, 1));
        var aMult = NumBox(ReadMult(row, AimMultPath, 1));
        var cMult = NumBox(ReadMult(row, ChromaMultPath, 1));

        var pCells = new TextBlock[WeaponCurve.MaxLevel + 1];
        var aCells = new TextBlock[WeaponCurve.MaxLevel + 1];
        var cCells = new TextBlock[WeaponCurve.MaxLevel + 1];
        double[] lastP = Array.Empty<double>(), lastA = Array.Empty<double>(), lastC = Array.Empty<double>();
        var readout = HoverReadout();

        void Refresh()
        {
            var physM = TryParse(pMult.Text, out var pm) ? pm : 1;
            var aimM = TryParse(aMult.Text, out var am) ? am : 1;
            var chrM = TryParse(cMult.Text, out var cm) ? cm : 1;
            var extraPhys = ReadMult(row, "PhysicalAttackMultiplier", 1);
            var sharedP = vm.SharedCurve("PhysicalAttack");
            var sharedA = vm.SharedCurve("MagicalAttack");
            var sharedC = vm.SharedCurve("ChromaValue");
            lastP = Scale(sharedP, physM * extraPhys);
            lastA = Scale(sharedA, aimM);
            lastC = Scale(sharedC, chrM);
            physChart.SetCompare(sharedP, lastP);
            aimChart.SetCompare(sharedA, lastA);
            chromaChart.SetCompare(sharedC, lastC);
            for (int i = 0; i <= WeaponCurve.MaxLevel; i++)
            {
                if (pCells[i] != null) pCells[i].Text = Fmt(lastP[i], false);
                if (aCells[i] != null) aCells[i].Text = Fmt(lastA[i], false);
                if (cCells[i] != null) cCells[i].Text = Fmt(lastC[i], true);
            }
        }

        void ShowHover(int? lvl)
        {
            physChart.SetHover(lvl);
            aimChart.SetHover(lvl);
            chromaChart.SetHover(lvl);
            readout.Text = lvl is int n && n < lastP.Length
                ? $"Level {n}    Physical {Fmt(lastP[n], false)}    Free Aim {Fmt(lastA[n], false)}    Chroma {Fmt(lastC[n], true)}"
                : "Move the pointer over a graph to read this weapon's values at that level.";
        }
        physChart.HoverChanged += (_, lvl) => ShowHover(lvl);
        aimChart.HoverChanged += (_, lvl) => ShowHover(lvl);
        chromaChart.HoverChanged += (_, lvl) => ShowHover(lvl);

        void Commit(TextBox box, string path)
        {
            if (!TryParse(box.Text, out var v)) return;
            vm.QueueEdit(row, path, v.ToString("0.###", CultureInfo.InvariantCulture));
            Refresh();
        }

        pMult.LostFocus += (_, _) => Commit(pMult, PhysMultPath);
        aMult.LostFocus += (_, _) => Commit(aMult, AimMultPath);
        cMult.LostFocus += (_, _) => Commit(cMult, ChromaMultPath);
        pMult.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Refresh(); };
        aMult.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Refresh(); };
        cMult.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Refresh(); };

        var multGrid = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("220,120"),
            RowDefinitions = RowDefinitions.Parse("Auto,Auto,Auto,Auto"),
            Margin = new Thickness(0, 4, 0, 8),
        };
        void MCell(Control c, int r, int col)
        {
            Grid.SetRow(c, r); Grid.SetColumn(c, col); multGrid.Children.Add(c);
        }
        MCell(Hint("This weapon's multipliers"), 0, 0);
        MCell(Tint("Physical Attack", PhysBrush), 1, 0); MCell(pMult, 1, 1);
        MCell(Tint("Free Aim Attack", AimBrush), 2, 0); MCell(aMult, 2, 1);
        MCell(Tint("Chroma Value", ChromaBrush), 3, 0); MCell(cMult, 3, 1);

        var table = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("70,*,*,*") };
        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (int i = 0; i <= WeaponCurve.MaxLevel; i++)
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        table.RowDefinitions.Add(new RowDefinition(new GridLength(80)));

        void Head(string text, int col, IBrush brush)
        {
            var t = new TextBlock
            {
                Text = text, FontWeight = FontWeight.Bold, Foreground = brush,
                Margin = new Thickness(4, 4), FontSize = 14,
            };
            Grid.SetRow(t, 0); Grid.SetColumn(t, col); table.Children.Add(t);
        }
        Head("Level", 0, Gold);
        Head("Physical Attack", 1, PhysBrush);
        Head("Free Aim Attack", 2, AimBrush);
        Head("Chroma Value", 3, ChromaBrush);

        for (int i = 0; i <= WeaponCurve.MaxLevel; i++)
        {
            var lab = new TextBlock
            {
                Text = i.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = i is 4 or 10 or 20 or 33 ? Gold : Cream,
                Margin = new Thickness(6, 2),
                FontSize = 14,
            };
            Grid.SetRow(lab, i + 1); Grid.SetColumn(lab, 0); table.Children.Add(lab);
            pCells[i] = RoCell(table, i + 1, 1);
            aCells[i] = RoCell(table, i + 1, 2);
            cCells[i] = RoCell(table, i + 1, 3);
            if (i == WeaponCurve.MaxLevel)
            {
                lab.Margin = new Thickness(6, 2, 6, 28);
                pCells[i].Margin = new Thickness(6, 2, 6, 28);
                aCells[i].Margin = new Thickness(6, 2, 6, 28);
                cCells[i].Margin = new Thickness(6, 2, 6, 28);
            }
        }

        var chartRow = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,*,*") };
        ChartBlock(chartRow, 0, "Physical Attack", PhysBrush, physChart);
        ChartBlock(chartRow, 1, "Free Aim Attack", AimBrush, aimChart);
        ChartBlock(chartRow, 2, "Chroma Value", ChromaBrush, chromaChart);

        var top = new StackPanel { Spacing = 8, Margin = new Thickness(6, 8, 12, 8) };
        top.Children.Add(new TextBlock
        {
            Text = "Read-only view of this weapon. Dashed is the shared curve; solid is this weapon after its multipliers. Edit Weapons Scaling to change the shared shape.",
            TextWrapping = TextWrapping.Wrap, Foreground = Muted, FontSize = 12,
        });
        top.Children.Add(multGrid);
        top.Children.Add(chartRow);
        top.Children.Add(readout);
        top.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 16,
            Children =
            {
                Hint("dashed = shared curve"),
                Hint("solid = this weapon"),
                Tint("red fill = difference", new SolidColorBrush(Color.Parse("#FFE24B3A"))),
            }
        });

        var tableScroll = new ScrollViewer
        {
            // Padding on the content: ScrollViewer.Padding is excluded from the
            // scrollable extent, clipping the last rows.
            Content = new Border { Padding = new Thickness(6, 4, 12, 72), Child = table },
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Visible,
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
        Refresh();
        return root;
    }

    private static CurveChart MakeChart(string color, string yTitle, string vanillaName, string previewName, bool integer = false) =>
        new(Color.Parse(color))
        {
            AxisXTitle = "Weapon Level",
            AxisYTitle = yTitle,
            VanillaName = vanillaName,
            PreviewName = previewName,
            IntegerValues = integer,
        };

    private static TextBlock HoverReadout() => new()
    {
        Text = "Move the pointer over a graph to read values at that weapon level.",
        Foreground = Cream,
        FontSize = 15,
        Margin = new Thickness(8, 4, 8, 2),
        TextWrapping = TextWrapping.Wrap,
    };

    private static void ChartBlock(Grid host, int col, string title, IBrush brush, CurveChart chart)
    {
        var block = new DockPanel { Margin = new Thickness(4, 0) };
        var cap = Tint(title, brush);
        DockPanel.SetDock(cap, Dock.Top);
        block.Children.Add(cap);
        block.Children.Add(chart);
        Grid.SetColumn(block, col);
        host.Children.Add(block);
    }

    public static Control BuildScaling(MainViewModel vm, RecordRow row)
    {
        var map = row.Baseline["AttributeScaling"] as JObject ?? new JObject();
        var attrs = vm.EnumChoices("ECharacterAttribute")
            .Where(c => c.Label is not "Invalid" and not "Intelligence")
            .ToList();
        // In-game the tooltip adds the weapon's quality upgrades (+3 at max
        // level) to the stored grade, so stored B/A/S overflow the letter
        // ladder and render blank. Only D (→A) and C (→S) are safe to store.
        var gradeLabels = vm.EnumChoices("EAttributeScalingGrade")
            .ToDictionary(g => g.Raw, g => g.Label);
        var grades = vm.EnumChoices("EAttributeScalingGrade")
            .Where(c => c.Raw is "NewEnumerator1" or "NewEnumerator2")
            .ToList();

        var current = map.Properties()
            .Select(p => (Attr: p.Name, Grade: p.Value?.ToString() ?? ""))
            .Where(p => p.Attr.Length > 0)
            .Take(3)
            .ToList();
        while (current.Count < 3) current.Add(("", ""));

        var attrBoxes = new ComboBox[3];
        var gradeBoxes = new ComboBox[3];

        void Commit()
        {
            var obj = new JObject();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < 3; i++)
            {
                var attrRaw = RawOf(attrBoxes[i]);
                var gradeRaw = RawOf(gradeBoxes[i]);
                if (string.IsNullOrEmpty(attrRaw) || string.IsNullOrEmpty(gradeRaw)) continue;
                var attrKey = attrRaw.Contains("::") ? attrRaw : $"ECharacterAttribute::{attrRaw}";
                if (!seen.Add(attrKey)) continue;
                var gradeVal = gradeRaw.Contains("::") ? gradeRaw : $"EAttributeScalingGrade::{gradeRaw}";
                obj[attrKey] = gradeVal;
            }
            vm.QueueToken(row, "AttributeScaling", obj);
        }

        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = "Each weapon scales with up to three character attributes (Vitality, Might, Agility, Defense, Luck). Stored grade D reads A and C reads S when fully leveled; higher grades blank at max level, so only D/C are offered.",
            TextWrapping = TextWrapping.Wrap, Foreground = Muted, FontSize = 12,
        });

        for (int i = 0; i < 3; i++)
        {
            var slot = i;
            var (attr, grade) = current[i];
            var attrRaw = attr.Contains("::") ? attr[(attr.IndexOf("::") + 2)..] : attr;
            var gradeRaw = grade.Contains("::") ? grade[(grade.IndexOf("::") + 2)..] : grade;

            attrBoxes[i] = ChoiceCombo(
                new[] { ("", "(none)") }.Concat(attrs),
                attrRaw, false);
            // A row saved before this rule may still hold B/A/S: keep it as a
            // flagged entry so the value isn't silently dropped on save.
            var gradeOptions = new List<(string Raw, string Label)> { ("", "(none)") };
            gradeOptions.AddRange(grades);
            if (gradeRaw.Length > 0 && gradeRaw is not ("NewEnumerator1" or "NewEnumerator2"))
                gradeOptions.Add((gradeRaw,
                    $"{gradeLabels.GetValueOrDefault(gradeRaw, gradeRaw)} — legacy, blanks at level 33"));
            gradeBoxes[i] = ChoiceCombo(gradeOptions, gradeRaw, false);
            attrBoxes[i].SelectionChanged += (_, _) => Commit();
            gradeBoxes[i].SelectionChanged += (_, _) => Commit();

            var line = new Grid
            {
                ColumnDefinitions = ColumnDefinitions.Parse("70,Auto,8,Auto"),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            var lab = new TextBlock
            {
                Text = $"Slot {slot + 1}", VerticalAlignment = VerticalAlignment.Center,
                Foreground = Cream, FontSize = 13,
            };
            Grid.SetColumn(lab, 0);
            Grid.SetColumn(attrBoxes[i], 1);
            Grid.SetColumn(gradeBoxes[i], 3);
            line.Children.Add(lab);
            line.Children.Add(attrBoxes[i]);
            line.Children.Add(gradeBoxes[i]);
            stack.Children.Add(line);
        }

        return new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(6, 10, 12, 10), Child = stack },
        };
    }

    public static ComboBox PassiveCombo(MainViewModel vm, RecordRow row, string currentRaw)
    {
        var character = row.Baseline["_Character"]?.ToString() ?? "";
        var preferred = character.Length > 0 && vm.PassivesByCharacter.TryGetValue(character, out var set)
            ? set : new HashSet<string>();

        const double NameSize = 13;
        const double DescSize = 12;
        const double Indent = 12;
        var maxContent = ContentWidth("(none)", NameSize);

        var combo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            MinHeight = 36,
            MaxDropDownHeight = 520,
        };
        void AddHeader(string text)
        {
            maxContent = Math.Max(maxContent, ContentWidth(text, 12));
            combo.Items.Add(new ComboBoxItem
            {
                Content = text, IsEnabled = false, Foreground = GoldDim,
                FontWeight = FontWeight.Bold, FontSize = 12, Tag = null,
                HorizontalAlignment = HorizontalAlignment.Left,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            });
        }
        void AddItem(string raw, bool pref)
        {
            if (raw.Length == 0)
            {
                combo.Items.Add(new ComboBoxItem
                {
                    Content = "(none)", Tag = "", Foreground = Muted, FontSize = NameSize,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                });
                return;
            }
            var (name, desc) = vm.DescribePassive(raw);
            maxContent = Math.Max(maxContent, ContentWidth(name, NameSize));
            var stack = new StackPanel
            {
                Spacing = 1,
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            stack.Children.Add(new TextBlock
            {
                Text = name,
                Foreground = pref ? Gold : Cream,
                FontSize = NameSize,
                HorizontalAlignment = HorizontalAlignment.Left,
                TextAlignment = TextAlignment.Left,
            });
            if (desc.Length > 0)
            {
                maxContent = Math.Max(maxContent, Math.Min(ContentWidth(desc, DescSize) + Indent, 420));
                stack.Children.Add(new TextBlock
                {
                    Text = desc,
                    Foreground = Muted,
                    FontSize = DescSize,
                    Margin = new Thickness(Indent, 1, 0, 2),
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 400,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    TextAlignment = TextAlignment.Left,
                });
            }
            var item = new ComboBoxItem
            {
                Content = stack,
                Tag = raw,
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            HoverTips.Set(item, name, desc);
            combo.Items.Add(item);
        }

        AddItem("", false);
        if (vm.PassivesByCharacter.TryGetValue("Created", out var created) && created.Count > 0)
        {
            AddHeader("— created —");
            foreach (var p in created.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                AddItem(p, true);
        }
        if (preferred.Count > 0)
        {
            if (!string.Equals(character, "Gustave", StringComparison.OrdinalIgnoreCase))
                AddHeader($"— {character} weapons —");
            foreach (var p in preferred.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                if (created != null && created.Contains(p)) continue;
                AddItem(p, true);
            }
        }
        AddHeader("— other weapons —");
        foreach (var ch in Characters)
        {
            if (string.Equals(ch, character, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(ch, "Gustave", StringComparison.OrdinalIgnoreCase)) continue;
            if (!vm.PassivesByCharacter.TryGetValue(ch, out var others) || others.Count == 0) continue;
            AddHeader($"  {ch}");
            foreach (var p in others.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                if (preferred.Contains(p)) continue;
                AddItem(p, false);
            }
        }
        var listed = new HashSet<string>(preferred, StringComparer.Ordinal);
        if (created != null)
            foreach (var p in created) listed.Add(p);
        foreach (var ch in Characters)
            if (vm.PassivesByCharacter.TryGetValue(ch, out var s))
                foreach (var p in s) listed.Add(p);
        var rest = vm.AllWeaponPassives.Where(p => !listed.Contains(p)).ToList();
        if (rest.Count > 0)
        {
            AddHeader("  Other");
            foreach (var p in rest) AddItem(p, false);
        }

        SelectRaw(combo, currentRaw);
        void TipSelected()
        {
            var raw = RawOf(combo);
            if (string.IsNullOrEmpty(raw))
            {
                HoverTips.Set(combo, "(none)", "Empty passive slot.");
                return;
            }
            var (n, d) = vm.DescribePassive(raw);
            HoverTips.Set(combo, n, d);
        }
        TipSelected();
        combo.SelectionChanged += (_, _) => TipSelected();
        SizeToContent(combo, maxContent, extra: 72, min: 180, max: 820);
        return combo;
    }

    public static ComboBox EnumCombo(IEnumerable<(string Raw, string Label)> choices, string currentRaw,
        IReadOnlySet<string>? preferredRaw = null)
    {
        var tagged = choices.Select(c => (Raw: c.Raw, Label: c.Label,
            Preferred: preferredRaw?.Contains(c.Raw) == true)).ToList();
        var preferred = tagged
            .Where(c => c.Preferred)
            .OrderBy(c => string.Equals(c.Raw, currentRaw, StringComparison.Ordinal) ? 0 : 1)
            .ToList();
        var other = tagged.Where(c => !c.Preferred).ToList();
        var ordered = preferred.Count == 0 ? tagged : preferred.Concat(other).ToList();
        return ChoiceCombo(
            ordered.Select(c => (c.Raw, c.Label)),
            currentRaw,
            colorPreferred: preferred.Count > 0,
            preferred.Select(p => p.Raw).ToHashSet());
    }

    public static ComboBox BoolCombo(string current)
    {
        var raw = current is "True" or "true" ? "true" : "false";
        return ChoiceCombo(new[] { ("false", "False"), ("true", "True") }, raw, false);
    }

    public static ComboBox ChoiceCombo(IEnumerable<(string Raw, string Label)> choices, string currentRaw,
        bool colorPreferred, HashSet<string>? preferred = null)
    {
        var combo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            MinHeight = 32,
        };
        var maxContent = 0.0;
        foreach (var (raw, label) in choices)
        {
            var pref = preferred?.Contains(raw) == true;
            maxContent = Math.Max(maxContent, ContentWidth(label, 13));
            combo.Items.Add(new ComboBoxItem
            {
                Content = label,
                Tag = raw,
                Foreground = colorPreferred && !pref && raw.Length > 0 ? Muted : (pref ? Gold : Cream),
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Left,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            });
        }
        SelectRaw(combo, currentRaw);
        SizeToContent(combo, maxContent, extra: 72, min: 96, max: 520);
        return combo;
    }

    public static ComboBox ChoiceCombo(IEnumerable<(string Raw, string Label)> choices, string currentRaw, bool _) =>
        ChoiceCombo(choices, currentRaw, false, null);

    public static string RawOf(ComboBox combo) =>
        combo.SelectedItem is ComboBoxItem it && it.Tag is string s ? s : "";

    public static void SelectRaw(ComboBox combo, string raw)
    {
        var want = StripEnumPrefix(raw);
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is not ComboBoxItem it || it.Tag is not string s) continue;
            if (string.Equals(s, raw, StringComparison.Ordinal) ||
                string.Equals(StripEnumPrefix(s), want, StringComparison.Ordinal))
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private static string StripEnumPrefix(string raw)
    {
        var i = raw.LastIndexOf("::", StringComparison.Ordinal);
        return i >= 0 ? raw[(i + 2)..] : raw;
    }

    internal static double ContentWidth(string text, double fontSize)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        if (text.Length > 240) text = text[..240];
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            Typeface.Default, fontSize, Brushes.White);
        return Math.Ceiling(ft.Width * 1.12);
    }

    internal static void SizeToContent(Control c, double content, double extra, double min, double max)
    {
        var w = Math.Clamp(content + extra, min, max);
        c.MinWidth = w;
        c.Width = w;
        c.HorizontalAlignment = HorizontalAlignment.Left;
    }

    private static TextBlock Hint(string t) => new()
    {
        Text = t, Foreground = Muted, FontSize = 14, VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 6, 0),
    };

    private static TextBlock Tint(string t, IBrush brush) => new()
    {
        Text = t, Foreground = brush, FontSize = 15, VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(4),
    };

    private static string Fmt(double v, bool integer) =>
        integer ? Math.Round(v).ToString(CultureInfo.InvariantCulture)
                : v.ToString("0.###", CultureInfo.InvariantCulture);

    private static bool TryParse(string? s, out double v) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    private static TextBox NumBox(double value) => new()
    {
        Text = Fmt(value, false), Width = 110, FontSize = 14,
    };

    private static double ReadMult(RecordRow row, string path, double fallback)
    {
        JToken? tok = row.AdvancedEdits.TryGetValue(path, out var adv)
            ? adv
            : MainViewModel.TokenAt(row.Baseline, path);
        if (tok is null) return fallback;
        return double.TryParse(tok.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d : fallback;
    }

    private static double[] Scale(double[] src, double mult)
    {
        var dst = new double[src.Length];
        for (int i = 0; i < src.Length; i++) dst[i] = src[i] * mult;
        return dst;
    }

    private static TextBlock RoCell(Grid table, int row, int col)
    {
        var t = new TextBlock
        {
            FontSize = 14, Foreground = Cream, Margin = new Thickness(6, 2),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetRow(t, row); Grid.SetColumn(t, col);
        table.Children.Add(t);
        return t;
    }
}

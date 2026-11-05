using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using E33ModEditor.App.ViewModels;
using E33ModEditor.Core.Loot;

namespace E33ModEditor.App.Views;

internal static class LootPanels
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#FF9C9280"));
    private static readonly IBrush StripeA = new SolidColorBrush(Color.Parse("#FF161411"));
    private static readonly IBrush StripeB = new SolidColorBrush(Color.Parse("#FF231E18"));
    private static readonly IBrush SelectBg = new SolidColorBrush(Color.Parse("#FF3A3224"));

    public static Control Build(LootViewModel vm)
    {
        var summary = new TextBlock
        {
            Text = vm.Summary,
            Foreground = Cream,
            FontSize = PictosEditorPanels.FontNote,
            TextWrapping = TextWrapping.Wrap,
        };
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LootViewModel.Summary))
                summary.Text = vm.Summary;
        };

        var source = Combo(vm.Sources, vm.SourceFilter, v => vm.SourceFilter = v);
        var tier = Combo(vm.Tiers, vm.TierFilter, v => vm.TierFilter = v);
        var area = Combo(vm.Areas, vm.AreaFilter, v => vm.AreaFilter = v);
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(LootViewModel.Areas)) return;
            area.ItemsSource = vm.Areas;
            if (!vm.Areas.Contains(vm.AreaFilter)) vm.AreaFilter = "All";
        };
        var search = new TextBox
        {
            Watermark = "Search id, name, item…",
            FontSize = PictosEditorPanels.FontInput,
            Width = 280,
            MinHeight = 32,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        search.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(TextBox.Text))
                vm.Search = search.Text ?? "";
        };

        var filters = new WrapPanel { ItemHeight = 40 };
        filters.Children.Add(Labeled("Source", source));
        filters.Children.Add(Labeled("Tier", tier));
        filters.Children.Add(Labeled("Area", area));
        filters.Children.Add(Labeled("Find", search));

        var freq = new StackPanel { Spacing = 4 };
        void RebuildFreq()
        {
            freq.Children.Clear();
            string? last = null;
            foreach (var row in vm.Frequencies)
            {
                if (row.Group != last)
                {
                    freq.Children.Add(PictosEditorPanels.Head(row.Group));
                    last = row.Group;
                }
                freq.Children.Add(FreqLine(row));
            }
        }
        vm.Frequencies.CollectionChanged += (_, _) => RebuildFreq();
        RebuildFreq();

        var listHost = new StackPanel { Spacing = 0 };
        void RebuildList()
        {
            listHost.Children.Clear();
            foreach (var c in vm.Visible)
                listHost.Children.Add(CheckRow(vm, c));
        }
        vm.Visible.CollectionChanged += (_, _) => RebuildList();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LootViewModel.Selected))
                RebuildList();
        };
        RebuildList();

        var listScroll = new ScrollViewer
        {
            Content = listHost,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        var left = new ScrollViewer
        {
            Content = freq,
            Width = 280,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 0, 20, 0),
        };

        var body = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(left, Dock.Left);
        body.Children.Add(left);
        body.Children.Add(listScroll);

        var editor = BuildEditor(vm);

        var root = new Grid
        {
            RowDefinitions = RowDefinitions.Parse("Auto,*,Auto"),
            Margin = new Thickness(20, 16, 24, 16),
        };
        var top = new StackPanel { Spacing = 8 };
        top.Children.Add(new TextBlock
        {
            Text = "Chests / Drops",
            Foreground = Gold,
            FontSize = PictosEditorPanels.FontTitle,
            FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
        });
        top.Children.Add(PictosEditorPanels.Note(
            "Vanilla map of every chest, Paint Cage, enemy drop and shop. Click a row, then pick a replacement item. Type any substring to filter the list. Upgrade-table chests stay catalyst rolls."));
        top.Children.Add(summary);
        top.Children.Add(filters);
        Grid.SetRow(top, 0);
        Grid.SetRow(body, 1);
        Grid.SetRow(editor, 2);
        root.Children.Add(top);
        root.Children.Add(body);
        root.Children.Add(editor);
        return root;
    }

    private static Control BuildEditor(LootViewModel vm)
    {
        var title = new TextBlock
        {
            Text = vm.SelectedLabel,
            Foreground = Gold,
            FontSize = PictosEditorPanels.FontLabel,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        };
        var slots = new StackPanel { Spacing = 8 };
        void Rebuild()
        {
            title.Text = vm.SelectedLabel;
            slots.Children.Clear();
            var check = vm.Selected;
            if (check is null) return;
            int i = 0;
            foreach (var slot in check.Slots)
            {
                i++;
                if (slot.LootTableRef)
                {
                    slots.Children.Add(new TextBlock
                    {
                        Text = $"Slot {i}: upgrade-table roll ({slot.ItemName}) — not edited here.",
                        Foreground = Muted,
                        FontSize = PictosEditorPanels.FontHint,
                    });
                    continue;
                }
                slots.Children.Add(SlotEditor(vm, slot, i));
            }
        }
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LootViewModel.Selected) or nameof(LootViewModel.SelectedLabel))
                Rebuild();
        };
        Rebuild();

        var box = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#FF3A3224")),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 12, 0, 0),
            Margin = new Thickness(0, 10, 0, 0),
            Child = new StackPanel { Spacing = 4, Children = { title, slots } },
        };
        return box;
    }

    private static Control SlotEditor(LootViewModel vm, LootCatalog.Slot slot, int index)
    {
        var box = new AutoCompleteBox
        {
            ItemsSource = vm.ItemChoices,
            FilterMode = AutoCompleteFilterMode.Contains,
            MinimumPrefixLength = 0,
            FontSize = PictosEditorPanels.FontInput,
            Width = 520,
            HorizontalAlignment = HorizontalAlignment.Left,
            Watermark = "Type part of a name or id…",
            SelectedItem = vm.ItemChoices.FirstOrDefault(c => c.Id == slot.ItemId),
            Text = vm.ItemChoices.FirstOrDefault(c => c.Id == slot.ItemId)?.Label ?? slot.ItemName,
        };
        box.ItemFilter = (search, item) =>
        {
            if (item is not ItemChoice c) return false;
            if (string.IsNullOrWhiteSpace(search)) return true;
            return c.Id.Contains(search, StringComparison.OrdinalIgnoreCase)
                   || c.Display.Contains(search, StringComparison.OrdinalIgnoreCase)
                   || c.Label.Contains(search, StringComparison.OrdinalIgnoreCase)
                   || c.Type.Contains(search, StringComparison.OrdinalIgnoreCase);
        };
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is ItemChoice c)
                vm.TrySetSlot(slot, c.Id);
        };
        box.LostFocus += (_, _) =>
        {
            if (box.SelectedItem is ItemChoice c)
                vm.TrySetSlot(slot, c.Id);
            else
            {
                var typed = (box.Text ?? "").Trim();
                var hit = vm.ItemChoices.FirstOrDefault(x =>
                    x.Id.Equals(typed, StringComparison.OrdinalIgnoreCase)
                    || x.Label.Equals(typed, StringComparison.OrdinalIgnoreCase)
                    || x.Display.Equals(typed, StringComparison.OrdinalIgnoreCase));
                if (hit is not null) vm.TrySetSlot(slot, hit.Id);
            }
        };

        var line = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        line.Children.Add(new TextBlock
        {
            Text = $"Slot {index}",
            Foreground = Cream,
            FontSize = PictosEditorPanels.FontLabel,
            Width = 72,
            VerticalAlignment = VerticalAlignment.Center,
        });
        line.Children.Add(box);
        var extra = slot.Chance < 100 ? $"  {slot.Chance:0.#}%" : "";
        if (slot.Quantity > 1) extra += $"  ×{slot.Quantity}";
        line.Children.Add(new TextBlock
        {
            Text = extra.Trim(),
            Foreground = Muted,
            FontSize = PictosEditorPanels.FontHint,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return line;
    }

    private static Control FreqLine(LootCatalog.FreqRow row)
    {
        var g = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("170,50,50"), Margin = new Thickness(0, 2) };
        var label = new TextBlock
        {
            Text = row.Label, Foreground = Cream, FontSize = 14,
            TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (!string.IsNullOrEmpty(row.Note))
            ToolTip.SetTip(label, row.Note);
        g.Children.Add(label);
        g.Children.Add(Cell(row.Slots.ToString(), Cream, 1));
        g.Children.Add(Cell(row.Expected.ToString("0.#"), Gold, 2));
        return g;
    }

    private static TextBlock Cell(string t, IBrush fg, int col)
    {
        var b = new TextBlock
        {
            Text = t, Foreground = fg, FontSize = 14, TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 8, 2),
        };
        Grid.SetColumn(b, col);
        return b;
    }

    private static Control CheckRow(LootViewModel vm, LootCatalog.Check c)
    {
        var items = string.Join(" · ", c.Slots.Select(s =>
            s.LootTableRef
                ? s.ItemName
                : $"{s.ItemName}{(s.Chance < 100 ? $" {s.Chance:0.#}%" : "")}{(s.Quantity > 1 ? $" ×{s.Quantity}" : "")}"));
        if (string.IsNullOrEmpty(items)) items = "(empty)";
        var selected = vm.Selected == c;
        var stripe = selected ? SelectBg : (Math.Abs(c.Id.GetHashCode()) % 2 == 0 ? StripeA : StripeB);
        var g = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("88,48,200,64,*"),
            Background = stripe,
            MinHeight = 36,
        };
        g.Children.Add(Cell(c.Source, Gold, 0));
        g.Children.Add(Cell("T" + c.Tier, Cream, 1));
        g.Children.Add(Cell(c.Area, Cream, 2));
        g.Children.Add(Cell("Lv " + c.AreaLevel, Muted, 3));
        var right = new StackPanel { Spacing = 0, Margin = new Thickness(0, 4, 8, 4) };
        right.Children.Add(new TextBlock { Text = c.DisplayName, Foreground = Cream, FontSize = 14, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis });
        right.Children.Add(new TextBlock { Text = items, Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(right, 4);
        g.Children.Add(right);

        var btn = new Button
        {
            Content = g,
            Padding = new Thickness(4, 0, 4, 0),
            Margin = new Thickness(0, 0, 0, 1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = stripe,
            BorderThickness = new Thickness(0),
        };
        btn.Click += (_, _) => vm.Select(c);
        return btn;
    }

    private static ComboBox Combo(IEnumerable<string> items, string current, Action<string> set)
    {
        var box = new ComboBox
        {
            ItemsSource = items.ToList(),
            SelectedItem = current,
            Width = 180,
            MinHeight = 32,
            FontSize = PictosEditorPanels.FontInput,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is string v) set(v);
        };
        return box;
    }

    private static Control Labeled(string label, Control input)
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 18, 8),
            VerticalAlignment = VerticalAlignment.Center,
        };
        stack.Children.Add(new TextBlock
        {
            Text = label, Foreground = Muted, FontSize = PictosEditorPanels.FontHint,
            VerticalAlignment = VerticalAlignment.Center,
        });
        stack.Children.Add(input);
        return stack;
    }
}

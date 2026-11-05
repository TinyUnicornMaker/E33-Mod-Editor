using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using E33ModEditor.App.ViewModels;
using E33ModEditor.Core.Loot;

namespace E33ModEditor.App.Views;

internal static class TierEditorPanels
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#FF9C9280"));
    private static readonly IBrush ColBg = new SolidColorBrush(Color.Parse("#FF161411"));
    private static readonly IBrush CardBg = new SolidColorBrush(Color.Parse("#FF231E18"));
    private static readonly IBrush SelectBg = new SolidColorBrush(Color.Parse("#FF4A3C24"));
    private static readonly IBrush DnuBg = new SolidColorBrush(Color.Parse("#FF2A1616"));

    public static Control Build(TierEditorViewModel vm)
    {
        var title = new TextBlock
        {
            Text = "Tier Editor",
            Foreground = Gold,
            FontSize = PictosEditorPanels.FontTitle,
            FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
        };
        var note = PictosEditorPanels.Note(
            "Pictos and weapon item tiers feed chests and shops. Effects and Characters feed weapon passive rolls: T1 weapons get 1 effect, T2 get 2, T3+ get 3 plus Extra chance. Stain-echo is one per weapon. Gustave is blocked from AutoDeath and may roll generic T1–T3 effects. Cut-content weapons (Mitigated Perfection and similar) show under Unplaced. Save writes the stock lists.");

        var pictosBtn = Tab("Pictos", vm.Page == TierEditorViewModel.PagePictos);
        var weaponBtn = Tab("Weapons", vm.Page == TierEditorViewModel.PageWeapons);
        var effectsBtn = Tab("Effects", vm.Page == TierEditorViewModel.PageEffects);
        var charsBtn = Tab("Characters", vm.Page == TierEditorViewModel.PageCharacters);
        void StylePages()
        {
            StyleTab(pictosBtn, vm.Page == TierEditorViewModel.PagePictos);
            StyleTab(weaponBtn, vm.Page == TierEditorViewModel.PageWeapons);
            StyleTab(effectsBtn, vm.Page == TierEditorViewModel.PageEffects);
            StyleTab(charsBtn, vm.Page == TierEditorViewModel.PageCharacters);
        }
        pictosBtn.Click += (_, _) => { vm.Page = TierEditorViewModel.PagePictos; StylePages(); };
        weaponBtn.Click += (_, _) => { vm.Page = TierEditorViewModel.PageWeapons; StylePages(); };
        effectsBtn.Click += (_, _) => { vm.Page = TierEditorViewModel.PageEffects; StylePages(); };
        charsBtn.Click += (_, _) => { vm.Page = TierEditorViewModel.PageCharacters; StylePages(); };

        var search = new TextBox
        {
            Watermark = "Search…",
            FontSize = PictosEditorPanels.FontInput,
            Width = 240,
            MinHeight = 32,
        };
        search.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(TextBox.Text))
                vm.Search = search.Text ?? "";
        };

        var stats = new TextBlock { Text = vm.Stats, Foreground = Muted, FontSize = PictosEditorPanels.FontHint, VerticalAlignment = VerticalAlignment.Center };
        var selected = new TextBlock { Text = vm.SelectedLabel, Foreground = Gold, FontSize = PictosEditorPanels.FontHint, VerticalAlignment = VerticalAlignment.Center };
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TierEditorViewModel.Stats)) stats.Text = vm.Stats;
            if (e.PropertyName == nameof(TierEditorViewModel.SelectedLabel)) selected.Text = vm.SelectedLabel;
        };

        var reset = ActionBtn("Reset to stock", () => vm.ResetToStock());
        var dnuAll = ActionBtn("All to Do Not Use", () => vm.MoveAllToDoNotUse());
        var save = ActionBtn("Save tier list", () =>
        {
            vm.Persist();
        });

        var toolbar = new WrapPanel { ItemHeight = 40 };
        toolbar.Children.Add(Pad(pictosBtn));
        toolbar.Children.Add(Pad(weaponBtn));
        toolbar.Children.Add(Pad(effectsBtn));
        toolbar.Children.Add(Pad(charsBtn));
        toolbar.Children.Add(Pad(search));
        toolbar.Children.Add(Pad(reset));
        toolbar.Children.Add(Pad(dnuAll));
        toolbar.Children.Add(Pad(save));

        var cols = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("*,*,*,*,*,*,*"),
            Margin = new Thickness(0, 10, 0, 0),
        };
        void AddCol(int i, string header, string dest, ObservableCollection<TierCard> items, bool dnu = false)
        {
            var col = Column(vm, header, dest, items, dnu);
            Grid.SetColumn(col, i);
            cols.Children.Add(col);
        }
        AddCol(0, "Unplaced", "Unplaced", vm.Unplaced);
        AddCol(1, "Tier 1", "Tier1", vm.T1);
        AddCol(2, "Tier 2", "Tier2", vm.T2);
        AddCol(3, "Tier 3", "Tier3", vm.T3);
        AddCol(4, "Tier 4", "Tier4", vm.T4);
        AddCol(5, "Tier 5", "Tier5", vm.T5);
        AddCol(6, "Do Not Use", "DoNotUse", vm.Dnu, dnu: true);

        var root = new Grid
        {
            RowDefinitions = RowDefinitions.Parse("Auto,Auto,Auto,*"),
            Margin = new Thickness(20, 16, 24, 16),
        };
        var top = new StackPanel { Spacing = 8, Children = { title, note } };
        var body = new ContentControl { Content = cols };
        var charHost = CharactersList(vm);
        void SyncBody()
        {
            body.Content = vm.Page == TierEditorViewModel.PageCharacters ? charHost : cols;
            dnuAll.IsVisible = vm.Page != TierEditorViewModel.PageCharacters;
        }
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(TierEditorViewModel.Page) or nameof(TierEditorViewModel.IsCharacterPage))
                SyncBody();
        };
        SyncBody();

        Grid.SetRow(top, 0);
        Grid.SetRow(toolbar, 1);
        var meta = new StackPanel { Spacing = 4, Margin = new Thickness(0, 6, 0, 0), Children = { stats, selected } };
        Grid.SetRow(meta, 2);
        Grid.SetRow(body, 3);
        root.Children.Add(top);
        root.Children.Add(toolbar);
        root.Children.Add(meta);
        root.Children.Add(body);
        return root;
    }

    private static Control CharactersList(TierEditorViewModel vm)
    {
        var list = new ListBox
        {
            ItemsSource = vm.CharacterRows,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        list.ItemTemplate = new FuncDataTemplate<EffectCharRow>((row, _) =>
            row is null ? new Border() : CharacterRow(vm, row), true);
        list.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Setter(ListBoxItem.PaddingProperty, new Thickness(0, 2)),
                new Setter(ListBoxItem.MarginProperty, new Thickness(0)),
                new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
                new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent),
            },
        });
        return list;
    }

    private static Control CharacterRow(TierEditorViewModel vm, EffectCharRow row)
    {
        var chips = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        foreach (var ch in vm.CharacterRoster)
        {
            var name = ch;
            var on = vm.CharacterAllowed(row.Id, name);
            var chip = new Button
            {
                Content = name,
                FontSize = 14,
                MinHeight = 28,
                Padding = new Thickness(10, 2),
            };
            StyleChip(chip, on);
            chip.Click += (_, _) =>
            {
                vm.ToggleCharacter(row.Id, name);
                StyleChip(chip, vm.CharacterAllowed(row.Id, name));
            };
            chips.Children.Add(chip);
        }
        var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("320,*") };
        grid.Children.Add(new TextBlock
        {
            Text = row.Label,
            Foreground = Cream,
            FontSize = 15,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(4, 0, 12, 0),
        });
        Grid.SetColumn(chips, 1);
        grid.Children.Add(chips);
        HoverTips.Set(grid, row.Label, string.IsNullOrEmpty(row.Description) ? row.Id : row.Description);
        return new Border
        {
            Background = CardBg,
            Padding = new Thickness(10, 6),
            Child = grid,
        };
    }

    private static void StyleChip(Button b, bool on)
    {
        b.Background = on ? Gold : new SolidColorBrush(Color.Parse("#FF2A241C"));
        b.Foreground = on ? new SolidColorBrush(Color.Parse("#FF1A1612")) : Muted;
    }

    private static Control Column(TierEditorViewModel vm, string header, string dest,
        ObservableCollection<TierCard> items, bool dnu)
    {
        var count = new TextBlock
        {
            Text = items.Count.ToString(),
            Foreground = Muted,
            FontSize = 13,
            Margin = new Thickness(6, 0, 0, 0),
        };
        var headBtn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock { Text = header, Foreground = dnu ? new SolidColorBrush(Color.Parse("#FFE57373")) : Gold, FontSize = 15 },
                    count,
                },
            },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(8, 8),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        headBtn.Click += (_, _) => vm.MoveSelectedTo(dest);

        var list = new StackPanel { Spacing = 3 };
        void Rebuild()
        {
            list.Children.Clear();
            count.Text = items.Count.ToString();
            foreach (var card in items)
                list.Children.Add(Card(vm, card, dnu));
        }
        items.CollectionChanged += (_, _) => Rebuild();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TierEditorViewModel.SelectedId))
                Rebuild();
        };
        Rebuild();

        var scroll = new ScrollViewer
        {
            Content = list,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        var stack = new DockPanel();
        DockPanel.SetDock(headBtn, Dock.Top);
        stack.Children.Add(headBtn);
        stack.Children.Add(scroll);
        return new Border
        {
            Background = dnu ? DnuBg : ColBg,
            Padding = new Thickness(4),
            Margin = new Thickness(0, 0, 8, 0),
            Child = stack,
        };
    }

    private static Control Card(TierEditorViewModel vm, TierCard card, bool dnu)
    {
        var selected = vm.SelectedId == card.Id;
        var btn = new Button
        {
            Content = new TextBlock
            {
                Text = card.Label,
                Foreground = Cream,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
            },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(8, 6),
            Background = selected ? SelectBg : CardBg,
            BorderThickness = new Thickness(0),
            Tag = card.Id,
        };
        HoverTips.Set(btn, card.Label, string.IsNullOrEmpty(card.Description) ? card.Id : card.Description);
        btn.Click += (_, _) => vm.SelectedId = card.Id;
        return btn;
    }

    private static Button Tab(string label, bool on)
    {
        var b = new Button { Content = label, MinWidth = 100, MinHeight = 32, FontSize = PictosEditorPanels.FontInput };
        StyleTab(b, on);
        return b;
    }

    private static void StyleTab(Button b, bool on)
    {
        b.Background = on ? Gold : new SolidColorBrush(Color.Parse("#FF2A241C"));
        b.Foreground = on ? new SolidColorBrush(Color.Parse("#FF1A1612")) : Cream;
    }

    private static Button ActionBtn(string label, Action act)
    {
        var b = new Button { Content = label, MinHeight = 32, FontSize = PictosEditorPanels.FontHint, Padding = new Thickness(12, 4) };
        b.Click += (_, _) => act();
        return b;
    }

    private static Control Pad(Control c) =>
        new Border { Child = c, Margin = new Thickness(0, 0, 10, 6) };
}

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using E33ModEditor.App.ViewModels;
using E33ModEditor.Core;
using E33ModEditor.Core.SaveGame;

namespace E33ModEditor.App.Views;

/// <summary>
/// Save Editor module: three stacked layers driven by
/// <see cref="SaveEditorViewModel.Phase"/> — the active-save picker, the
/// "Opening Save …" loading screen, and the tabbed editor that slides in from
/// the right with a Close-without-saving / Save-and-exit bar pinned to the
/// bottom.
/// </summary>
internal static class SaveEditorPanels
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#FF9C9280"));
    private static readonly IBrush Line = new SolidColorBrush(Color.Parse("#FF3B3529"));
    private static readonly IBrush Danger = new SolidColorBrush(Color.Parse("#FFD07A6C"));

    private static readonly Dictionary<SaveEditorViewModel, Control> Cached = new();

    public static Control Build(SaveEditorViewModel vm)
    {
        if (Cached.TryGetValue(vm, out var existing)) return existing;

        var picker = BuildPickerScreen(vm);
        var loading = BuildLoadingScreen(vm);
        // Opaque backdrop so the sliding editor fully covers the picker layer.
        var shell = new Border { Background = InkBackdrop, Child = BuildEditorShell(vm) };
        var outcome = BuildOutcomeOverlay(vm);

        AttachSlideTransition(shell);

        var root = new Grid { DataContext = vm };
        root.Children.Add(picker);
        root.Children.Add(shell);
        root.Children.Add(loading);
        root.Children.Add(outcome);

        loading.IsVisible = false;
        shell.IsVisible = false;
        shell.ZIndex = 2;
        loading.ZIndex = 3;
        outcome.ZIndex = 4;

        vm.PhaseChanged += () => ApplyPhase(vm, loading, shell);

        Cached[vm] = root;
        return root;
    }

    // ───────────────────────── phase transitions ─────────────────────────

    private static void ApplyPhase(SaveEditorViewModel vm, Control loading, Control shell)
    {
        loading.IsVisible = vm.Phase == SaveEditorViewModel.EditorPhase.Opening;
        switch (vm.Phase)
        {
            case SaveEditorViewModel.EditorPhase.Edit:
                if (!shell.IsVisible) _ = SlideInFromRight(shell);
                break;
            case SaveEditorViewModel.EditorPhase.Pick:
                if (shell.IsVisible) _ = SlideOutToRight(shell);
                break;
        }
    }

    private static void AttachSlideTransition(Control page)
    {
        if (page.RenderTransform is not TranslateTransform t)
            page.RenderTransform = t = new TranslateTransform();
        t.Transitions = new Avalonia.Animation.Transitions
        {
            new Avalonia.Animation.DoubleTransition
            {
                Property = TranslateTransform.XProperty,
                Duration = TimeSpan.FromMilliseconds(340),
                Easing = new Avalonia.Animation.Easings.CubicEaseInOut(),
            }
        };
    }

    private static readonly IBrush InkBackdrop = new SolidColorBrush(Color.Parse("#FF161411"));

    private static async System.Threading.Tasks.Task SlideInFromRight(Control c)
    {
        c.IsVisible = true;
        SetX(c, 1600);
        await Dispatcher.UIThread.InvokeAsync(() => { });
        SetX(c, 0);
        await System.Threading.Tasks.Task.Delay(360);
    }

    private static async System.Threading.Tasks.Task SlideOutToRight(Control c)
    {
        SetX(c, 1600);
        await System.Threading.Tasks.Task.Delay(360);
        c.IsVisible = false;
        SetX(c, 0);
    }

    private static void SetX(Control c, double x)
    {
        if (c.RenderTransform is TranslateTransform t)
            t.X = x;
        else
            c.RenderTransform = new TranslateTransform(x, 0);
    }

    // ───────────────────────── pick screen ─────────────────────────

    private static Control BuildPickerScreen(SaveEditorViewModel vm)
    {
        var stack = new StackPanel { Spacing = 0 };
        var header = Header("Save Editor — Active Saves",
            "These are the saves the game's Continue loads: the newest title-screen slot per Steam account. " +
            "Click one once to open the editor. Nothing is written to disk until you press Save and exit.");
        DockPanel.SetDock(header, Dock.Top);

        var refreshRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 8),
        };
        refreshRow.Children.Add(MakeButton("Refresh", vm.RefreshSavesCommand));

        var empty = new TextBlock
        {
            Text = "No active saves found.\nStart Expedition 33, save once in-game, then press Refresh.",
            Foreground = Muted,
            FontSize = 15,
            Margin = new Thickness(0, 18, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        empty.Bind(TextBlock.IsVisibleProperty, new Binding(nameof(SaveEditorViewModel.ContinueSaves.Count))
        {
            Source = vm.ContinueSaves,
            Converter = CountToBoolConverter.EmptyIsTrue,
        });

        var cards = new ListBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent,
        };
        cards.Bind(ListBox.ItemsSourceProperty,
            new Binding(nameof(SaveEditorViewModel.ContinueSaves)) { Source = vm });
        cards.ItemTemplate = MakeSaveCardTemplate();
        cards.SelectionChanged += (_, _) =>
        {
            if (cards.SelectedItem is not SaveFileEntry entry) return;
            cards.SelectedItem = null; // allow picking the same save again later
            if (vm.OpenSaveCommand.CanExecute(entry)) vm.OpenSaveCommand.Execute(entry);
        };
        vm.ContinueSaves.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset && cards.SelectedItem is not null)
                cards.SelectedItem = null;
        };

        var statusLine = new TextBlock
        {
            Foreground = Gold,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0),
        };
        statusLine.Bind(TextBlock.TextProperty,
            new Binding(nameof(SaveEditorViewModel.Status)) { Source = vm });

        var inner = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        inner.Children.Add(header);
        DockPanel.SetDock(refreshRow, Dock.Top);
        inner.Children.Add(refreshRow);
        DockPanel.SetDock(statusLine, Dock.Bottom);
        inner.Children.Add(statusLine);
        stack.Children.Add(empty);
        stack.Children.Add(cards);
        inner.Children.Add(new ScrollViewer
        {
            Content = stack,
            VerticalAlignment = VerticalAlignment.Stretch,
        });

        return Frame(inner);
    }

    private static FuncDataTemplate<SaveFileEntry> MakeSaveCardTemplate() => new((_, _) =>
    {
        var grid = new Grid
        {
            ColumnDefinitions = ColDefs("170,1*,300,130"),
            Margin = new Thickness(2, 6),
        };

        var slot = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var slotTitle = new TextBlock
        {
            FontSize = 17,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        slotTitle.Bind(TextBlock.TextProperty,
            new Binding(nameof(SaveFileEntry.SlotTitle)) { Mode = BindingMode.OneWay });
        var badges = new TextBlock { Foreground = Cream, FontSize = 12 };
        badges.Bind(TextBlock.TextProperty,
            new Binding(nameof(SaveFileEntry.Badges)) { Mode = BindingMode.OneWay });
        slot.Children.Add(slotTitle);
        slot.Children.Add(badges);
        Add(grid, 0, slot);

        var summary = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var story = new TextBlock
        {
            Foreground = Cream,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 34,
        };
        story.Bind(TextBlock.TextProperty,
            new Binding(nameof(SaveFileEntry.StoryPoint)) { Mode = BindingMode.OneWay });
        var locTime = new TextBlock { Foreground = Muted, FontSize = 12 };
        locTime.Bind(TextBlock.TextProperty, new Binding(nameof(SaveFileEntry.LocationSummary))
        {
            Mode = BindingMode.OneWay,
        });
        summary.Children.Add(story);
        summary.Children.Add(locTime);
        ToolTip.SetTip(summary, "Story progress and location");
        Add(grid, 1, summary);

        var fileCol = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var file = new TextBlock
        {
            Foreground = Cream,
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        file.Bind(TextBlock.TextProperty,
            new Binding(nameof(SaveFileEntry.Name)) { Mode = BindingMode.OneWay });
        var account = new TextBlock { Foreground = Muted, FontSize = 12 };
        account.Bind(TextBlock.TextProperty,
            new Binding(nameof(SaveFileEntry.AccountId)) { Mode = BindingMode.OneWay });
        ToolTip.SetTip(fileCol, "File name and Steam account");
        fileCol.Children.Add(file);
        fileCol.Children.Add(account);
        Add(grid, 2, fileCol);

        var modified = new TextBlock
        {
            Foreground = Muted,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
        };
        modified.Bind(TextBlock.TextProperty,
            new Binding(nameof(SaveFileEntry.Modified)) { Mode = BindingMode.OneWay });
        Add(grid, 3, modified);

        return grid;
    });

    // ───────────────────────── loading screen ─────────────────────────

    private static Control BuildLoadingScreen(SaveEditorViewModel vm)
    {
        var label = new TextBlock
        {
            Foreground = Cream,
            FontSize = 21,
            FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 780,
        };
        label.Bind(TextBlock.TextProperty,
            new Binding(nameof(SaveEditorViewModel.OpeningLabel)) { Source = vm });

        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 18,
        };
        stack.Children.Add(label);
        stack.Children.Add(new ProgressBar
        {
            IsIndeterminate = true,
            Width = 340,
            Height = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Reading the save into memory…",
            Foreground = Muted,
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F2161411")),
            Child = stack,
        };
    }

    // ───────────────────────── outcome overlay ─────────────────────────

    private static Control BuildOutcomeOverlay(SaveEditorViewModel vm)
    {
        var title = new TextBlock
        {
            FontSize = 30,
            FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.Bind(TextBlock.TextProperty,
            new Binding(nameof(SaveEditorViewModel.OutcomeTitle)) { Source = vm });
        title.Bind(TextBlock.ForegroundProperty,
            new Binding(nameof(SaveEditorViewModel.OutcomeIsError))
            {
                Source = vm,
                Converter = BoolToBrushConverter.ErrorIsDanger,
            });

        var detail = new TextBlock
        {
            Foreground = Cream,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 720,
        };
        detail.Bind(TextBlock.TextProperty,
            new Binding(nameof(SaveEditorViewModel.OutcomeDetail)) { Source = vm });

        var ok = MakeButton("OK", vm.DismissOutcomeCommand);
        ok.HorizontalAlignment = HorizontalAlignment.Center;
        ok.Bind(Button.IsVisibleProperty,
            new Binding(nameof(SaveEditorViewModel.OutcomeIsError)) { Source = vm });

        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 14,
        };
        stack.Children.Add(title);
        stack.Children.Add(detail);
        stack.Children.Add(ok);

        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F2161411")),
            Child = stack,
        };
        border.Bind(Border.IsVisibleProperty,
            new Binding(nameof(SaveEditorViewModel.ShowOutcome)) { Source = vm });
        return border;
    }

    // ───────────────────────── editor shell ─────────────────────────

    private static Control BuildEditorShell(SaveEditorViewModel vm)
    {
        var tabs = new TabControl { Margin = new Thickness(20, 12, 24, 8) };
        tabs.Items.Add(BuildCharactersTab(vm));
        tabs.Items.Add(BuildPictosTab(vm));
        tabs.Items.Add(BuildWeaponsTab(vm));
        tabs.Items.Add(BuildResourcesTab(vm));
        tabs.Items.Add(BuildWorldTab(vm));
        tabs.Items.Add(BuildItemsTab("Music", vm.MusicDisks, vm,
            "Unlock records by id. Catalog comes from DT_MusicRecords when preloaded."));
        tabs.Items.Add(BuildItemsTab("Journals", vm.Journals, vm,
            "Unlock journals by id. Catalog comes from DT_Items_Journals when preloaded."));

        var statusLine = new TextBlock
        {
            Foreground = Gold,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
        };
        statusLine.Bind(TextBlock.TextProperty,
            new Binding(nameof(SaveEditorViewModel.Status)) { Source = vm });

        var openTitle = new TextBlock
        {
            Foreground = Muted,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0),
        };
        openTitle.Bind(TextBlock.TextProperty,
            new Binding(nameof(SaveEditorViewModel.OpenSaveTitle)) { Source = vm });

        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(statusLine);
        labels.Children.Add(openTitle);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };
        buttons.Children.Add(MakeButton("Close without saving", vm.CloseWithoutSavingCommand));
        var saveBtn = MakeButton("Save and exit", vm.SaveAndExitCommand);
        ToolTip.SetTip(saveBtn, "Writes every edit to the .sav (with backup), shows the outcome, then leaves the module.");
        buttons.Children.Add(saveBtn);

        var barInner = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(buttons, Dock.Right);
        barInner.Children.Add(buttons);
        barInner.Children.Add(labels);

        var bar = new Border
        {
            Classes = { "panel" },
            Padding = new Thickness(14, 8),
            Margin = new Thickness(12, 0, 12, 10),
            Child = barInner,
        };

        var dock = new DockPanel();
        DockPanel.SetDock(bar, Dock.Bottom);
        dock.Children.Add(bar);
        dock.Children.Add(tabs);
        return dock;
    }

    // ───────────────────────── pictos tab ─────────────────────────

    private static TabItem BuildPictosTab(SaveEditorViewModel vm)
    {
        var host = new StackPanel { Spacing = 2 };
        var body = TableBody(
            Header("Pictos",
                "Found puts the pictos in your inventory; Mastered marks its lumina learnt (4/4); Level is the shared progression level. Created pictos (✨) need a CLI/standalone build (so ExpeditionInfinity_<seed>_P is in ~mods) AND Found ticked on the Continue slot, then Save and exit."),
            StickyHeader("340,90,90,170,*", "Pictos", "Found", "Mastered", "Level"),
            host,
            Footer("Edits apply instantly and persist when you press Save and exit."));

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 8),
        };
        var search = new TextBox { Watermark = "Search…", Width = 240 };
        search.Bind(TextBox.TextProperty, new Binding(nameof(SaveEditorViewModel.FilterText))
        {
            Source = vm,
            Mode = BindingMode.TwoWay,
        });
        toolbar.Children.Add(search);
        toolbar.Children.Add(BulkButton(vm, "Add all vanilla", () => vm.BulkAddAllVanilla()));
        toolbar.Children.Add(BulkButton(vm, "Add all created ✨", vm.BulkAddAllCreated));
        toolbar.Children.Add(BulkButton(vm, "Master all found", () => vm.BulkMasterFound()));
        var levelBox = new TextBox { Watermark = "1–33", Width = 64 };
        toolbar.Children.Add(levelBox);
        toolbar.Children.Add(BulkButton(vm, "Set level on found", () =>
        {
            if (int.TryParse(levelBox.Text, out var lvl)) vm.BulkSetLevel(lvl);
        }));
        InsertAfterHeader(body, toolbar);

        void Sync()
        {
            host.Children.Clear();
            foreach (var row in vm.VisibleRows)
                host.Children.Add(BuildPictoRow(vm, row));
        }
        vm.PictosRebuilt += Sync;
        vm.SaveReloaded += Sync;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SaveEditorViewModel.FilterText)) Sync();
        };
        Sync();

        return new TabItem { Header = "Pictos", Content = Frame(body) };
    }

    private static Control BuildPictoRow(SaveEditorViewModel vm, SavePictosRow row)
    {
        var grid = new Grid { ColumnDefinitions = ColDefs("340,90,90,170,*"), Margin = new Thickness(0, 1) };

        var label = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            Foreground = row.IsCreated
                ? new SolidColorBrush(Color.Parse(row.InPack ? "#FFE0C070" : "#FF9A7B45"))
                : row.NotSupported
                    ? Muted
                    : Cream,
            Text = row.DisplayLabel
                   + (row.IsCreated && !row.InPack ? "  ⚠ not in packed mod" : "")
                   + (row.NotSupported ? "  ⚠ removed from current pack" : ""),
        };
        HoverTips.Set(label, row.DisplayName,
            string.IsNullOrEmpty(row.Description) ? row.Id : row.Description);
        grid.Children.Add(label);

        var found = BoundCheck(row, nameof(SavePictosRow.Found), v => vm.ToggleFound(row, v));
        Grid.SetColumn(found, 1);
        grid.Children.Add(found);

        var mastered = BoundCheck(row, nameof(SavePictosRow.Mastered), v => vm.ToggleMastered(row, v));
        mastered.IsEnabled = row.Found;
        Grid.SetColumn(mastered, 2);
        grid.Children.Add(mastered);

        var level = BoundNumeric(row, nameof(SavePictosRow.Level), 1, 33,
            v => vm.ChangeLevel(row, v), () => row.Level);
        level.IsEnabled = row.Found;
        Grid.SetColumn(level, 3);
        grid.Children.Add(level);

        var group = new TextBlock
        {
            Foreground = Muted,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Text = row.Group switch
            {
                "created-painter" => "Painter Conversion",
                "created-affinity" => "Status Affinity",
                _ when row.NotSupported => "retired",
                _ => "",
            },
        };
        Grid.SetColumn(group, 4);
        grid.Children.Add(group);

        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SavePictosRow.Found))
            {
                mastered.IsEnabled = row.Found;
                level.IsEnabled = row.Found;
            }
        };
        return grid;
    }

    // ───────────────────────── characters ─────────────────────────

    private static TabItem BuildCharactersTab(SaveEditorViewModel vm)
    {
        var root = new DockPanel();
        var header = Header("Characters",
            "Left: the playable roster — in party or not, in this save or not (click a missing member to create their record). Right: worn gear (weapon + 3 pictos), attributes, vitals, skills and appearance. Edits stay pending until Save and exit.");
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var split = new Grid
        {
            ColumnDefinitions = ColDefs("260,1*"),
            Margin = new Thickness(0, 10, 0, 0),
        };

        var listHost = new StackPanel { Spacing = 2 };
        var listScroll = new ScrollViewer
        {
            Content = listHost,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Grid.SetColumn(listScroll, 0);
        split.Children.Add(listScroll);

        // One persistent detail view per character: switching selection never
        // destroys another character's editors, so nothing can be lost mid-edit.
        var detailCache = new Dictionary<CharacterRow, Control>();
        var rebuildCache = new Dictionary<CharacterRow, Action>();
        var detailHost = new ContentControl { Margin = new Thickness(18, 0, 0, 0) };
        Grid.SetColumn(detailHost, 1);
        split.Children.Add(detailHost);

        Control DetailFor(CharacterRow row)
        {
            if (detailCache.TryGetValue(row, out var view)) return view;
            var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            void Rebuild() => scroll.Content =
                BuildCharacterDetail(vm, row, () => { Rebuild(); SyncList(); });
            rebuildCache[row] = Rebuild;
            Rebuild();
            detailCache[row] = scroll;
            return scroll;
        }

        void SyncList()
        {
            listHost.Children.Clear();
            if (vm.Characters.Count == 0)
            {
                listHost.Children.Add(new TextBlock { Text = "Load a save first.", Foreground = Muted });
                return;
            }
            foreach (var row in vm.Characters)
            {
                var capture = row;
                var btn = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(8, 6),
                    Tag = row,
                };
                var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                line.Children.Add(new TextBlock
                {
                    Text = capture.DisplayName,
                    Foreground = capture.InSave ? Cream : Muted,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 14,
                });
                if (capture.InSave)
                    line.Children.Add(new TextBlock
                    {
                        Text = $"Lv {capture.Level}",
                        Foreground = Gold,
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                    });
                line.Children.Add(new TextBlock
                {
                    Text = capture.InSave ? capture.Badges : "+ not in save — click to add",
                    Foreground = Muted,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                btn.Content = line;
                btn.Click += (_, _) =>
                {
                    if (!capture.InSave)
                        vm.ActivateCharacterRecord(capture); // creates record + selects the fresh row
                    else
                        vm.SelectedCharacter = capture;
                };
                if (ReferenceEquals(vm.SelectedCharacter, capture))
                    btn.BorderBrush = Gold;
                listHost.Children.Add(btn);
            }
        }

        void SyncDetail()
        {
            detailHost.Content = vm.SelectedCharacter is { } row
                ? DetailFor(row)
                : new TextBlock { Text = "Load a save first.", Foreground = Muted };
        }

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SaveEditorViewModel.SelectedCharacter))
            {
                SyncList();
                SyncDetail();
            }
        };
        vm.Characters.CollectionChanged += (_, _) => SyncList();
        vm.SaveReloaded += () =>
        {
            detailCache.Clear();
            rebuildCache.Clear();
            SyncList();
            SyncDetail();
        };
        SyncList();
        SyncDetail();

        root.Children.Add(split);
        return new TabItem { Header = "Characters", Content = Frame(root) };
    }

    private static Control BuildCharacterDetail(SaveEditorViewModel vm, CharacterRow row, Action refresh)
    {
        var stack = new StackPanel { Spacing = 16 };

        var partyPanel = new StackPanel { Spacing = 6 };
        var partyCheck = BoundCheck(row, nameof(CharacterRow.InParty), v =>
        {
            vm.SetPartyMember(row, v);
            refresh();
        });
        ToolTip.SetTip(partyCheck,
            "This editor allows up to 5 active members (requires the Party of Five mod in game). " +
            "If the party is full, untick one of them first.");
        partyPanel.Children.Add(FieldRow("In active party", partyCheck));
        partyPanel.Children.Add(FieldRow("Hidden from roster",
            BoundCheck(row, nameof(CharacterRow.Excluded), v =>
            {
                row.Excluded = v;
                vm.CommitCharacter(row);
            })));
        partyPanel.Children.Add(FieldRow("Formation",
            RowLabel(string.IsNullOrEmpty(row.Formation) ? "—" : ShortFormation(row.Formation))));
        AddSection(stack, "Party", partyPanel);

        var progPanel = new StackPanel { Spacing = 6 };
        progPanel.Children.Add(FieldRow("Level", BoundNumeric(row, nameof(CharacterRow.Level),
            1, vm.LevelCap, v => { row.Level = v; vm.CommitCharacter(row); }, () => row.Level)));
        progPanel.Children.Add(FieldRow("Current XP", BoundNumeric(row, nameof(CharacterRow.CurrentXp),
            0, 999_999_999, v => { row.CurrentXp = v; vm.CommitCharacter(row); }, () => row.CurrentXp)));
        progPanel.Children.Add(FieldRow("Spare AP", BoundNumeric(row, nameof(CharacterRow.ActionPoints),
            0, 99, v => { row.ActionPoints = v; vm.CommitCharacter(row); }, () => row.ActionPoints)));
        progPanel.Children.Add(FieldRow("Lumina (consumables)", BoundNumeric(row, nameof(CharacterRow.Lumina),
            0, 9999, v => { row.Lumina = v; vm.CommitCharacter(row); }, () => row.Lumina)));
        if (vm.LevelCap <= GameplayTweaksKit.VanillaLevelCap)
            progPanel.Children.Add(Hint(
                $"Level cap follows Gameplay Tweaks → Character max level (vanilla {GameplayTweaksKit.VanillaLevelCap}, editor default {GameplayTweaksKit.DefaultLevelCap})."));
        AddSection(stack, "Progression", progPanel);

        var attrPanel = new StackPanel { Spacing = 6 };
        attrPanel.Children.Add(AttrRow(vm, row, "Vitality", "ECharacterAttribute::NewEnumerator0", () => row.Vitality, v => row.Vitality = v));
        attrPanel.Children.Add(AttrRow(vm, row, "Might", "ECharacterAttribute::NewEnumerator1", () => row.Might, v => row.Might = v));
        attrPanel.Children.Add(AttrRow(vm, row, "Agility", "ECharacterAttribute::NewEnumerator3", () => row.Agility, v => row.Agility = v));
        attrPanel.Children.Add(AttrRow(vm, row, "Defense", "ECharacterAttribute::NewEnumerator4", () => row.Defense, v => row.Defense = v));
        attrPanel.Children.Add(AttrRow(vm, row, "Luck", "ECharacterAttribute::NewEnumerator5", () => row.Luck, v => row.Luck = v));
        AddSection(stack, "Assigned attributes", attrPanel);

        var vitalsPanel = new StackPanel { Spacing = 6 };
        vitalsPanel.Children.Add(FieldRow("HP", BoundNumeric(row, nameof(CharacterRow.Hp),
            0, 99_999, v => { row.Hp = v; vm.CommitCharacter(row); }, () => row.Hp)));
        vitalsPanel.Children.Add(FieldRow("MP", BoundNumeric(row, nameof(CharacterRow.Mp),
            0, 999, v => { row.Mp = v; vm.CommitCharacter(row); }, () => row.Mp)));
        AddSection(stack, "Vitals", vitalsPanel);

        var gearPanel = new StackPanel { Spacing = 6 };
        gearPanel.Children.Add(FieldRow("Weapon", GearCombo(vm.WeaponCatalog, row.Weapon,
            id => { vm.SetWeapon(row, id); refresh(); })));
        var pictosChoices = PictosChoices(vm);
        for (var i = 0; i < 3; i++)
        {
            var slot = i;
            gearPanel.Children.Add(FieldRow($"Pictos slot {i + 1}",
                GearCombo(pictosChoices, row.PictosSlots[i],
                    id => { vm.SetPictosSlot(row, slot, id); refresh(); }, allowEmpty: true)));
        }
        gearPanel.Children.Add(Hint(
            "These three slots are what the character wears. Created pictos (✨) need the packed mod in ~mods or they will not show in-game."));
        AddSection(stack, "Worn gear", gearPanel);

        var skillsPanel = new StackPanel { Spacing = 6 };
        skillsPanel.Children.Add(new TextBlock
        {
            Text = "Battle wheel (6)",
            Foreground = Muted,
            FontSize = 12,
        });
        var unlocked = row.Skills.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        for (var i = 0; i < 6; i++)
        {
            var idx = i;
            var current = idx < row.EquippedSkills.Count ? row.EquippedSkills[idx] : "";
            var combo = new ComboBox { MinWidth = 280, MaxDropDownHeight = 320 };
            var items = new List<string> { "(empty)" };
            items.AddRange(unlocked);
            if (current.Length > 0 && !items.Contains(current)) items.Insert(1, current);
            combo.ItemsSource = items;
            combo.SelectedItem = current.Length > 0 ? current : "(empty)";
            combo.SelectionChanged += (_, _) =>
            {
                var v = combo.SelectedItem as string ?? "";
                if (v == "(empty)") v = "";
                if (v != current)
                {
                    vm.SetEquippedSkill(row, idx, v);
                    refresh();
                }
            };
            skillsPanel.Children.Add(FieldRow($"Slot {idx + 1}", combo));
        }

        skillsPanel.Children.Add(new TextBlock
        {
            Text = "Unlocked skills",
            Foreground = Muted,
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0),
        });
        foreach (var skill in row.Skills.ToList())
        {
            var s = skill;
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            line.Children.Add(RowLabel(s));
            line.Children.Add(MakeButton("Remove", new RelayCommand(() =>
            {
                vm.RemoveSkill(row, s);
                refresh();
                return Task.CompletedTask;
            }, () => vm.CanAct)));
            skillsPanel.Children.Add(line);
        }
        var addLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var skillBox = new TextBox { Watermark = $"Skill id (e.g. Combo1_{row.DisplayName})", MinWidth = 260 };
        addLine.Children.Add(skillBox);
        addLine.Children.Add(MakeButton("Add skill", new RelayCommand(() =>
        {
            vm.AddSkill(row, skillBox.Text ?? "");
            skillBox.Text = "";
            refresh();
            return Task.CompletedTask;
        }, () => vm.CanAct)));
        skillsPanel.Children.Add(addLine);
        AddSection(stack, "Skills", skillsPanel);

        var luminaPanel = new StackPanel { Spacing = 6 };
        foreach (var luminaId in row.EquippedLuminas.ToList())
        {
            var id = luminaId;
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            line.Children.Add(RowLabel(id));
            line.Children.Add(MakeButton("Remove", new RelayCommand(() =>
            {
                vm.UnequipLumina(row, id);
                refresh();
                return Task.CompletedTask;
            }, () => vm.CanAct)));
            luminaPanel.Children.Add(line);
        }
        var catalog = vm.PictosCatalog.OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        if (catalog.Count > 0)
        {
            var pickLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var combo = new ComboBox { MinWidth = 280, MaxDropDownHeight = 320 };
            var byDisplay = new Dictionary<string, string>();
            foreach (var (id, display) in catalog) byDisplay[$"{display} ({id})"] = id;
            combo.ItemsSource = byDisplay.Keys.ToList();
            pickLine.Children.Add(combo);
            pickLine.Children.Add(MakeButton("Equip lumina", new RelayCommand(() =>
            {
                if (combo.SelectedItem is string key && byDisplay.TryGetValue(key, out var pid))
                {
                    vm.EquipLumina(row, pid);
                    refresh();
                }
                return Task.CompletedTask;
            }, () => vm.CanAct)));
            luminaPanel.Children.Add(pickLine);
        }
        luminaPanel.Children.Add(Hint("Lumina is the passive loadout (EquippedPassiveEffects), separate from the three worn pictos."));
        AddSection(stack, "Lumina loadout", luminaPanel);

        var appearPanel = new StackPanel { Spacing = 6 };
        var skins = vm.SkinOptions(row);
        var faces = vm.FaceOptions(row);
        var skinCombo = new ComboBox { MinWidth = 260, ItemsSource = skins, SelectedItem = skins.Contains(row.Skin) ? row.Skin : skins.FirstOrDefault() };
        var faceCombo = new ComboBox { MinWidth = 260, ItemsSource = faces, SelectedItem = faces.Contains(row.Face) ? row.Face : faces.FirstOrDefault() };
        appearPanel.Children.Add(FieldRow("Skin", skinCombo));
        appearPanel.Children.Add(FieldRow("Face", faceCombo));
        appearPanel.Children.Add(MakeButton("Apply appearance", new RelayCommand(() =>
        {
            vm.SetAppearance(row,
                skinCombo.SelectedItem as string ?? row.Skin,
                faceCombo.SelectedItem as string ?? row.Face);
            return Task.CompletedTask;
        }, () => vm.CanAct)));
        AddSection(stack, "Appearance", appearPanel);

        var wardrobePanel = new StackPanel { Spacing = 6 };
        wardrobePanel.Children.Add(MakeButton("Unlock all costumes & hairstyles", new RelayCommand(() =>
        {
            vm.UnlockAllCostumes(row);
            refresh();
            return Task.CompletedTask;
        }, () => vm.CanAct)));
        wardrobePanel.Children.Add(Hint(
            "Adds every outfit and haircut known for this character to the inventory (Skin*/Face* items). They appear in-game at a mirror or camp."));
        AddSection(stack, "Wardrobe", wardrobePanel);

        return stack;
    }

    private static Control AttrRow(SaveEditorViewModel vm, CharacterRow row, string label, string enumLabel,
        Func<int> read, Action<int> write)
    {
        var box = GuardedNumeric(0, 9999, read, v =>
        {
            write(v);
            vm.SetAttributePoints(row, enumLabel, v);
        }, 150);
        return FieldRow(label, box);
    }

    private static Dictionary<string, string> PictosChoices(SaveEditorViewModel vm)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, name) in vm.PictosCatalog)
            d[id] = string.IsNullOrEmpty(name) ? id : name;
        return d;
    }

    private static Control GearCombo(Dictionary<string, string> catalog, string current,
        Action<string> commit, bool allowEmpty = false)
    {
        var combo = new ComboBox { MinWidth = 280, MaxDropDownHeight = 360 };
        var labels = new List<string>();
        var byLabel = new Dictionary<string, string>();
        if (allowEmpty)
        {
            labels.Add("(empty)");
            byLabel["(empty)"] = "";
        }
        foreach (var (id, name) in catalog.OrderBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase))
        {
            var label = $"{name} ({id})";
            labels.Add(label);
            byLabel[label] = id;
        }
        if (current.Length > 0 && !catalog.ContainsKey(current))
        {
            var extra = $"{current} ({current})";
            labels.Insert(allowEmpty ? 1 : 0, extra);
            byLabel[extra] = current;
        }
        combo.ItemsSource = labels;
        combo.SelectedItem = byLabel.FirstOrDefault(kv => kv.Value.Equals(current, StringComparison.OrdinalIgnoreCase)).Key
                             ?? (allowEmpty ? "(empty)" : labels.FirstOrDefault());
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is string key && byLabel.TryGetValue(key, out var id) && id != current)
                commit(id);
        };
        return combo;
    }

    private static string ShortFormation(string label)
    {
        var idx = label.LastIndexOf("NewEnumerator", StringComparison.Ordinal);
        if (idx >= 0 && int.TryParse(label[(idx + "NewEnumerator".Length)..], out var n))
            return $"Slot #{n + 1}";
        return label;
    }

    private static void AddSection(StackPanel parent, string title, Control body)
    {
        if (parent.Children.Count > 0)
            parent.Children.Add(new Border
            {
                Height = 1,
                Background = Line,
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            });
        parent.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Cream,
            FontWeight = FontWeight.Bold,
            FontSize = 15,
        });
        parent.Children.Add(body);
    }

    private static Grid FieldRow(string label, Control editor)
    {
        var grid = new Grid { ColumnDefinitions = ColDefs("190,*") };
        grid.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Cream,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
        });
        editor.SetValue(Grid.ColumnProperty, 1);
        grid.Children.Add(editor);
        return grid;
    }

    // ───────────────────────── weapons / items ─────────────────────────

    private static TabItem BuildWeaponsTab(SaveEditorViewModel vm)
    {
        var host = new StackPanel { Spacing = 2 };
        var body = TableBody(
            Header("Weapons",
                "Unlock adds the weapon to the inventory; Level writes into the shared progression list. Catalog comes from DT_WeaponDefinitions when preloaded."),
            StickyHeader("340,90,170,*", "Weapon", "Owned", "Level"),
            host);

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 8),
        };
        toolbar.Children.Add(BulkButton(vm, "Unlock all", vm.BulkUnlockAllWeapons));
        var levelBox = new TextBox { Watermark = "1–33", Width = 64 };
        toolbar.Children.Add(levelBox);
        toolbar.Children.Add(BulkButton(vm, "Set level on owned", () =>
        {
            if (int.TryParse(levelBox.Text, out var lvl)) vm.BulkSetWeaponLevel(lvl);
        }));
        InsertAfterHeader(body, toolbar);

        void Sync()
        {
            host.Children.Clear();
            if (vm.Weapons.Count == 0)
            {
                host.Children.Add(new TextBlock { Text = "Load a save first.", Foreground = Muted });
                return;
            }
            var order = SaveEditorViewModel.CharacterRoster.Concat(new[] { "Other" }).ToList();
            var groups = vm.Weapons.GroupBy(w =>
            {
                var ch = string.IsNullOrEmpty(w.Character) ? "Other" : w.Character;
                return SaveEditorViewModel.CharacterRoster.Contains(ch) ? ch : "Other";
            }).ToDictionary(g => g.Key, g => g.OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase).ToList());
            foreach (var owner in order)
            {
                if (!groups.TryGetValue(owner, out var rows) || rows.Count == 0) continue;
                host.Children.Add(new TextBlock
                {
                    Text = owner,
                    Foreground = Gold,
                    FontWeight = FontWeight.Bold,
                    FontSize = 15,
                    Margin = new Thickness(0, 10, 0, 4),
                });
                foreach (var row in rows)
                {
                    var grid = new Grid { ColumnDefinitions = ColDefs("340,90,170,*"), Margin = new Thickness(0, 1) };
                    grid.Children.Add(RowLabel($"{row.DisplayName}   ({row.Id})"));
                    Add(grid, 1, BoundCheck(row, nameof(ItemRow.Owned), v => vm.ToggleOwned(row, v)));
                    Add(grid, 2, BoundNumeric(row, nameof(ItemRow.Level), 1, vm.WeaponLevelCap,
                        v => vm.CommitWeaponLevel(row, v), () => row.Level));
                    host.Children.Add(grid);
                }
            }
        }
        vm.SaveReloaded += Sync;
        Sync();
        return new TabItem { Header = "Weapons", Content = Frame(body) };
    }

    private static TabItem BuildItemsTab(string title, System.Collections.ObjectModel.ObservableCollection<ItemRow> rows,
        SaveEditorViewModel vm, string note)
    {
        var host = new StackPanel { Spacing = 2 };
        var body = TableBody(Header(title, note),
            StickyHeader("1*,110,1*,110", "Item", "Unlocked", "Item", "Unlocked"), host);

        void Sync()
        {
            host.Children.Clear();
            if (rows.Count == 0)
            {
                host.Children.Add(new TextBlock { Text = "Load a save first.", Foreground = Muted });
                return;
            }
            var list = rows.ToList();
            for (var i = 0; i < list.Count; i += 2)
            {
                var line = new Grid { ColumnDefinitions = ColDefs("1*,110,1*,110"), Margin = new Thickness(0, 1) };
                AddItemCell(line, 0, list[i], vm);
                if (i + 1 < list.Count)
                    AddItemCell(line, 2, list[i + 1], vm);
                host.Children.Add(line);
            }
        }
        vm.SaveReloaded += Sync;
        Sync();
        return new TabItem { Header = title, Content = Frame(body) };
    }

    private static void AddItemCell(Grid line, int col, ItemRow row, SaveEditorViewModel vm)
    {
        var name = RowLabel($"{row.DisplayName}   ({row.Id})");
        Add(line, col, name);
        Add(line, col + 1, BoundCheck(row, nameof(ItemRow.Owned), v => vm.ToggleOwned(row, v)));
    }

    private static readonly (string Id, string Label)[] ResourceItems =
    {
        ("HealingTint_Shard", "Healing Tint Shard"),
        ("EnergyTint_Shard", "Energy Tint Shard"),
        ("ReviveTint_Shard", "Revive Tint Shard"),
        ("UpgradeMaterial_Level1", "Chroma Catalyst"),
        ("UpgradeMaterial_Level2", "Polished Chroma Catalyst"),
        ("UpgradeMaterial_Level3", "Resplendent Chroma Catalyst"),
        ("UpgradeMaterial_Level4", "Grandiose Chroma Catalyst"),
        ("UpgradeMaterial_Level5", "Perfect Chroma Catalyst"),
        ("ChromaPack_Regular", "Chroma Pack"),
        ("Consumable_Respec", "Recoat"),
    };

    private static TabItem BuildResourcesTab(SaveEditorViewModel vm)
    {
        var stack = new StackPanel { Spacing = 6, Margin = new Thickness(0, 10, 0, 0) };
        var body = new DockPanel();
        var header = Header("Resources",
            "Chroma on hand plus tint shards, upgrade catalysts, chroma packs and recoats.");
        DockPanel.SetDock(header, Dock.Top);
        body.Children.Add(header);
        body.Children.Add(new ScrollViewer
        {
            Content = stack,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Top,
        });

        void Sync()
        {
            stack.Children.Clear();
            var chroma = GuardedNumeric(0, int.MaxValue, () => vm.Gold, v => vm.Gold = v, 200);
            chroma.Increment = 100;
            stack.Children.Add(FieldRow("Chroma", chroma));
            foreach (var (id, label) in ResourceItems)
            {
                var resId = id;
                var resLabel = label;
                var box = GuardedNumeric(0, 99_999, () => vm.GetResource(resId),
                    v => vm.SetResource(resId, v, resLabel), 160);
                stack.Children.Add(FieldRow(label, box));
            }
        }
        vm.SaveReloaded += Sync;
        Sync();
        return new TabItem { Header = "Resources", Content = Frame(body) };
    }

    // ───────────────────────── world ─────────────────────────

    private static TabItem BuildWorldTab(SaveEditorViewModel vm)
    {
        var host = new StackPanel { Spacing = 2 };
        string filter = "";
        var search = new TextBox { Watermark = "Filter keys…", Margin = new Thickness(0, 8, 0, 8) };
        var hint = new TextBlock
        {
            Foreground = Muted,
            FontSize = 13,
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        hint.Bind(TextBlock.TextProperty,
            new Binding(nameof(SaveEditorViewModel.ExplorationSummary)) { Source = vm });

        var progress = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 4) };
        progress.Children.Add(FieldRow("New Game + count",
            GuardedNumeric(0, 99, () => vm.NgPlus, v => vm.NgPlus = v, 120)));
        var spawnCombo = new ComboBox
        {
            MinWidth = 420,
            MaxDropDownHeight = 420,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsTextSearchEnabled = true,
        };
        var spawnByLabel = new Dictionary<string, string>(StringComparer.Ordinal);
        var fillingSpawn = false;
        void FillSpawn()
        {
            fillingSpawn = true;
            try
            {
                var current = vm.SpawnPointTag;
                spawnByLabel.Clear();
                var labels = new List<string>();
                foreach (var s in SpawnCatalog.All)
                {
                    var label = $"{s.Name}  ({s.Tag})";
                    labels.Add(label);
                    spawnByLabel[label] = s.Tag;
                }
                if (current.Length > 0 && SpawnCatalog.Find(current) is null)
                {
                    var extra = $"{current}  (not in catalog)";
                    labels.Insert(0, extra);
                    spawnByLabel[extra] = current;
                }
                spawnCombo.ItemsSource = labels;
                spawnCombo.SelectedItem = spawnByLabel.FirstOrDefault(kv =>
                    kv.Value.Equals(current, StringComparison.OrdinalIgnoreCase)).Key
                    ?? labels.FirstOrDefault();
            }
            finally { fillingSpawn = false; }
        }
        spawnCombo.SelectionChanged += (_, _) =>
        {
            if (fillingSpawn) return;
            if (spawnCombo.SelectedItem is string key && spawnByLabel.TryGetValue(key, out var tag))
                vm.SpawnPointTag = tag;
        };
        FillSpawn();
        progress.Children.Add(FieldRow("Spawn point", spawnCombo));

        var storyCombo = new ComboBox
        {
            ItemsSource = vm.StoryPointChoices,
            SelectedIndex = vm.CurrentStoryPointIndex,
            MinWidth = 420,
            MaxDropDownHeight = 420,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var storyButton = new Button
        {
            Content = "Rewind story to this chapter (in memory)",
            Padding = new Thickness(12, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        storyButton.Click += (_, _) =>
        {
            if (storyCombo.SelectedIndex >= 0) vm.ApplyStoryPoint(storyCombo.SelectedIndex);
        };
        var storyPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 2) };
        storyPanel.Children.Add(new TextBlock
        {
            Text = "Main-story checkpoint",
            Foreground = Muted,
            FontSize = 12,
        });
        storyPanel.Children.Add(storyCombo);
        storyPanel.Children.Add(storyButton);
        progress.Children.Add(storyPanel);

        var body = TableBody(
            Header("World",
                "New Game+ and spawn live here. Flags below are InteractedObjects (manor doors, pickups) and BattledEnemies / EncounteredEnemies. Filter matches key or map name."),
            StickyHeader("200,90,*", "Map", "Set", "Key"),
            host,
            hint);
        InsertAfterHeader(body, search);
        InsertAfterHeader(body, progress);

        void Render()
        {
            host.Children.Clear();
            foreach (var row in vm.WorldFlags)
            {
                if (!string.IsNullOrEmpty(filter) &&
                    !row.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                    !row.MapName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                var grid = new Grid { ColumnDefinitions = ColDefs("200,90,*"), Margin = new Thickness(0, 1) };
                grid.Children.Add(RowLabel(row.MapName));
                Add(grid, 1, BoundCheck(row, nameof(FlagRow.Value), v => vm.ToggleFlag(row, v)));
                var keyText = new TextBlock
                {
                    Foreground = Cream,
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = row.Key,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                ToolTip.SetTip(keyText, row.Key);
                Add(grid, 2, keyText);
                host.Children.Add(grid);
            }
        }
        search.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(TextBox.Text))
            {
                filter = search.Text ?? "";
                Render();
            }
        };
        vm.SaveReloaded += () => { FillSpawn(); Render(); };
        Render();
        return new TabItem { Header = "World", Content = Frame(body) };
    }

    // ───────────────────────── bound controls ─────────────────────────

    private static CheckBox BoundCheck<TRow>(TRow row, string property, Action<bool> commit)
        where TRow : ViewModelBase
    {
        var box = new CheckBox { HorizontalAlignment = HorizontalAlignment.Center };
        box.Bind(ToggleButton.IsCheckedProperty, new Binding(property)
        {
            Source = row,
            Mode = BindingMode.OneWay,
        });
        box.IsCheckedChanged += (_, _) =>
        {
            var v = box.IsChecked == true;
            if (v != GetRowBool(row, property)) commit(v);
        };
        return box;
    }

    private static bool GetRowBool(ViewModelBase row, string property) => row switch
    {
        SavePictosRow p when property == nameof(p.Found) => p.Found,
        SavePictosRow p when property == nameof(p.Mastered) => p.Mastered,
        ItemRow i => i.Owned,
        FlagRow f => f.Value,
        CharacterRow c when property == nameof(c.Excluded) => c.Excluded,
        CharacterRow c when property == nameof(c.InParty) => c.InParty,
        _ => throw new ArgumentException($"No bool '{property}' on {row.GetType().Name}"),
    };

    private static NumericUpDown BoundNumeric<TRow>(TRow row, string property, int min, int max,
        Action<int> commit, Func<int> read, int width = 150)
        where TRow : ViewModelBase
    {
        var box = GuardedNumeric(min, max, read, commit, width);
        box.Bind(NumericUpDown.ValueProperty, new Binding(property)
        {
            Source = row,
            Mode = BindingMode.OneWay,
        });
        return box;
    }

    /// <summary>
    /// NumericUpDown re-applies Value (often 0) when its template attaches.
    /// Ignore those until a dispatcher tick after attach, then only commit real edits.
    /// </summary>
    private static NumericUpDown GuardedNumeric(int min, int max, Func<int> read, Action<int> commit,
        int width = 150)
    {
        var box = NumberBox(min, max, read(), width);
        var armed = false;
        box.AttachedToVisualTree += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                box.Value = read();
                armed = true;
            }, DispatcherPriority.Background);
        };
        box.ValueChanged += (_, _) =>
        {
            if (!armed || !box.Value.HasValue) return;
            var v = (int)box.Value.Value;
            if (v == read()) return;
            commit(v);
        };
        return box;
    }

    private static NumericUpDown NumberBox(int min, int max, int value, int width = 150) => new()
    {
        Minimum = min,
        Maximum = max,
        Increment = 1,
        Width = width,
        MinWidth = width,
        HorizontalAlignment = HorizontalAlignment.Left,
        HorizontalContentAlignment = HorizontalAlignment.Right,
        Padding = new Thickness(10, 2, 16, 2),
        Value = value,
    };

    // ───────────────────────── converters ─────────────────────────

    /// <summary>int → bool converter family for collection-count visibilities.</summary>
    private sealed class CountToBoolConverter : Avalonia.Data.Converters.IValueConverter
    {
        public static readonly CountToBoolConverter EmptyIsTrue = new();

        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => value is int count && count == 0;

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>bool → brush: error renders in danger red, success in gold.</summary>
    private sealed class BoolToBrushConverter : Avalonia.Data.Converters.IValueConverter
    {
        public static readonly BoolToBrushConverter ErrorIsDanger = new();

        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => value is true ? Danger : Gold;

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }

    // ───────────────────────── shared chrome ─────────────────────────

    private static Border Frame(Control content) => new()
    {
        Classes = { "panel" },
        Padding = new Thickness(20, 16),
        Child = content,
    };

    private static StackPanel Header(string title, string body) => new()
    {
        Spacing = 6,
        Margin = new Thickness(0, 0, 0, 4),
        Children =
        {
            new TextBlock
            {
                Text = title,
                Foreground = Gold,
                FontSize = 22,
                FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
            },
            new TextBlock { Text = body, Foreground = Muted, FontSize = 14, TextWrapping = TextWrapping.Wrap },
        },
    };

    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        Foreground = Muted,
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
    };

    private static TextBlock Footer(string text) => new()
    {
        Foreground = Muted,
        FontSize = 13,
        Margin = new Thickness(0, 6, 0, 0),
        Text = text,
    };

    private static TextBlock RowLabel(string text)
    {
        var tb = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Cream,
            Text = text,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ToolTip.SetTip(tb, text);
        return tb;
    }

    private static Button MakeButton(string label, ICommand command) => new()
    {
        Content = label,
        Command = command,
        Padding = new Thickness(12, 6),
    };

    private static Button BulkButton(SaveEditorViewModel vm, string label, Action act) =>
        MakeButton(label, new RelayCommand(() => { act(); return Task.CompletedTask; }, () => vm.CanAct));

    /// <summary>Header + column strip + scrolling rows, no star-row gap under the headings.</summary>
    private static DockPanel TableBody(Control header, Grid stickyHeader, Control rows, Control? footer = null)
    {
        var dock = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);
        DockPanel.SetDock(stickyHeader, Dock.Top);
        dock.Children.Add(stickyHeader);
        if (footer is not null)
        {
            DockPanel.SetDock(footer, Dock.Bottom);
            dock.Children.Add(footer);
        }
        dock.Children.Add(new ScrollViewer
        {
            Content = rows,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Stretch,
        });
        return dock;
    }

    /// <summary>Park a toolbar between the page header and the column strip.</summary>
    private static void InsertAfterHeader(DockPanel body, Control toolbar)
    {
        DockPanel.SetDock(toolbar, Dock.Top);
        body.Children.Insert(1, toolbar);
    }

    private static Grid StickyHeader(string cols, params string[] labels)
    {
        var grid = new Grid
        {
            ColumnDefinitions = ColDefs(cols),
            Margin = new Thickness(0, 8, 18, 2),
        };
        for (var i = 0; i < labels.Length; i++)
        {
            Add(grid, i, new TextBlock
            {
                Text = labels[i],
                Foreground = Cream,
                FontWeight = FontWeight.Bold,
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }
        return grid;
    }

    private static void Add(Grid g, int col, Control c)
    {
        Grid.SetColumn(c, col);
        g.Children.Add(c);
    }

    private static ColumnDefinitions ColDefs(string def)
    {
        var cols = new ColumnDefinitions();
        foreach (var part in def.Split(',')) cols.Add(new ColumnDefinition(GridLength.Parse(part.Trim())));
        return cols;
    }
}

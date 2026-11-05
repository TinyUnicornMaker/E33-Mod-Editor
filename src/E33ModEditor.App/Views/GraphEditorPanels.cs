using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using E33ModEditor.App.ViewModels;
using E33ModEditor.Core;

namespace E33ModEditor.App.Views;

/// <summary>
/// Graph Editor module: header + element legend, then one tab per character
/// (party join order). Each tab owns a SkillGraphCanvas backed by a session
/// controller that loads the character's vanilla DA_SkillGraph and applies
/// the persisted arrangement on top.
/// </summary>
internal static class GraphEditorPanels
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#FF9C9280"));
    private static readonly IBrush Danger = new SolidColorBrush(Color.Parse("#FFD07A6C"));

    private static readonly (string Element, string Hex)[] Legend =
    {
        ("Fire", "#FFD9593F"),
        ("Ice", "#FF6FBFD9"),
        ("Lightning", "#FFE3C24A"),
        ("Earth", "#FFB08968"),
        ("Dark", "#FF8E6BBF"),
        ("Light", "#FFF2E8C9"),
        ("Physical", "#FFB7AB93"),
        ("Void", "#FF7A8291"),
        ("Multi", "#FF9AD4C8"),
        ("Weapon", "#FFCFC4B0"),
    };

    public static Control Build(MainViewModel vm)
    {
        // The DA APCost cache may hold values from before the user's latest
        // rebalance; re-read the DAs each time the module opens.
        SkillGraphKit.ResetDaApCostCache();

        var root = new Grid
        {
            RowDefinitions = RowDefinitions.Parse("Auto,Auto,*"),
            Margin = new Thickness(16, 12, 16, 8),
        };

        var header = new TextBlock
        {
            Text = "Skill Graph Editor",
            Foreground = Gold,
            FontSize = PictosEditorPanels.FontTitle,
            FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
            FontWeight = FontWeight.SemiBold,
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var legendWrap = new StackPanel { Spacing = 8, Margin = new Thickness(0, 6, 0, 2) };
        legendWrap.Children.Add(new TextBlock
        {
            Text = "Diamonds show each move's AP cost, coloured by element. " +
                   "Hover a move to read its description (modded text when a kit rewrites it, vanilla otherwise). " +
                   "Drag a move to snap it to a grid point (sub-points sit halfway between the vanilla columns and rows). " +
                   "Right-click a move to start a link, right-click another move to finish it; " +
                   "right-click a link line to delete it. Moves themselves can never be deleted.",
            Foreground = Muted,
            FontSize = PictosEditorPanels.FontHint,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 1500,
        });
        var legendRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Margin = new Thickness(0, 2, 0, 0),
        };
        foreach (var (element, hex) in Legend)
            legendRow.Children.Add(Swatch(element, hex, filled: true));
        legendRow.Children.Add(Swatch("Gradient attack", "#FFFFD98A", ring: true,
            tip: "Gradient attacks / secret gradient-unlock nodes — bright gold ring + G badge"));
        legendRow.Children.Add(Swatch("Starting skill", "#FFC9A961", ring: true,
            tip: "Learned for free at the start — gold ring"));
        legendRow.Children.Add(Swatch("Gated / secret", "#FF9C9280", dashed: true,
            tip: "IsSecret node (usually requires an unlock item) — dashed ring"));
        legendWrap.Children.Add(legendRow);
        Grid.SetRow(legendWrap, 1);
        root.Children.Add(legendWrap);

        var picker = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 8, 0, 4),
        };
        var tabs = new TabControl { Margin = new Thickness(0, 2, 0, 0) };
        var pickButtons = new List<Button>();
        foreach (var (id, _, graphStem) in SkillGraphKit.Characters)
        {
            var session = new CharacterSession(vm, id, graphStem);
            var tab = new TabItem
            {
                Header = id,
                Content = session.BuildContent(),
            };
            tab.AddHandler(SelectingItemsControl.IsSelectedChangedEvent,
                (sender, _) =>
                {
                    if (!ReferenceEquals(sender, tab)) return;
                    if (tab.IsSelected) session.RefreshView();
                    else session.OnTabLeft();
                });
            tabs.Items.Add(tab);
            var btn = new Button
            {
                Content = id,
                Margin = new Thickness(0, 0, 8, 6),
                Padding = new Thickness(12, 4),
                FontSize = PictosEditorPanels.FontLabel,
            };
            btn.Click += (_, _) => tabs.SelectedItem = tab;
            pickButtons.Add(btn);
            picker.Children.Add(btn);
        }
        tabs.SelectionChanged += (_, _) =>
        {
            for (int i = 0; i < pickButtons.Count; i++)
                pickButtons[i].FontWeight = i == tabs.SelectedIndex ? FontWeight.Bold : FontWeight.Normal;
        };
        if (pickButtons.Count > 0) pickButtons[0].FontWeight = FontWeight.Bold;

        var body = new Grid
        {
            RowDefinitions = RowDefinitions.Parse("Auto,*"),
        };
        Grid.SetRow(picker, 0);
        Grid.SetRow(tabs, 1);
        body.Children.Add(picker);
        body.Children.Add(tabs);
        Grid.SetRow(body, 2);
        root.Children.Add(body);
        return root;
    }

    private static Control Swatch(string label, string hex, bool filled = false, bool ring = false,
        bool dashed = false, string tip = "")
    {
        var color = Color.Parse(hex);
        var block = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        Control dot;
        if (ring)
        {
            dot = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(7),
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(2),
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
        else if (dashed)
        {
            dot = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(7),
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(2),
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
        else
        {
            dot = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(3),
                Background = filled ? new SolidColorBrush(color) : null,
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = filled ? new Thickness(0) : new Thickness(2),
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
        block.Children.Add(dot);
        block.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Cream,
            FontSize = PictosEditorPanels.FontHint,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (tip.Length > 0)
            ToolTip.SetTip(block, tip);
        return block;
    }

    /// <summary>
    /// Owns one character's loaded graph + canvas state and the shape toolbar.
    /// Every commit mutates vm.SkillGraph (custom layout) and saves.
    /// </summary>
    private sealed class CharacterSession
    {
        private readonly MainViewModel _vm;
        private readonly string _graphStem;
        private readonly SkillGraphKit.Settings.CharacterState _state;

        private readonly List<SkillGraphNodeVM> _nodes = new();
        private readonly Dictionary<string, (double X, double Y)> _vanillaPositions = new(StringComparer.Ordinal);
        private readonly List<(string From, string To)> _vanillaLinks = new();
        private string _loadError = "";
        private bool _suppressShapeApply;

        private SkillGraphCanvas? _canvas;
        private TextBlock? _status;
        private TextBlock? _activeChip;
        private ComboBox? _shapeCombo;

        public CharacterSession(MainViewModel vm, string id, string graphStem)
        {
            _vm = vm;
            _graphStem = graphStem;
            _state = vm.SkillGraph.For(id);
            LoadGraph();
        }

        public Control BuildContent()
        {
            // Host grid: the editor (toolbar + canvas) with a right-side slide-in
            // overlay for naming a new shape (the same enter-from-right pane the
            // rest of the editor uses).
            var host = new Grid();

            var wrap = new Grid
            {
                RowDefinitions = RowDefinitions.Parse("Auto,*"),
                Margin = new Thickness(0, 8, 0, 0),
            };
            var toolbar = BuildToolbar();
            Grid.SetRow(toolbar, 0);
            wrap.Children.Add(toolbar);

            var canvasHost = new Border
            {
                Classes = { "panel" },
                Padding = new Thickness(2),
                Margin = new Thickness(0, 8, 0, 0),
            };
            Grid.SetRow(canvasHost, 1);
            wrap.Children.Add(canvasHost);

            if (_loadError.Length > 0)
            {
                canvasHost.Child = new TextBlock
                {
                    Text = _loadError,
                    Foreground = Muted,
                    FontSize = PictosEditorPanels.FontLabel,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(14),
                };
                host.Children.Add(wrap);
                return host;
            }

            var canvas = _canvas = new SkillGraphCanvas();
            // Snap the canvas lattice to the union default positions first (vanilla +
            // kit-authored new nodes), so "Reset to vanilla" restores this exact tree.
            foreach (var n in _nodes)
                if (_vanillaPositions.TryGetValue(n.Stem, out var v))
                {
                    n.Raw.X = v.X;
                    n.Raw.Y = v.Y;
                }
            canvas.Load(_nodes);
            ApplyStateToCanvas();
            canvas.GraphChanged += OnGraphChanged;
            canvas.StatusChanged += msg => { if (_status is not null) _status.Text = msg; };
            canvasHost.Child = canvas;

            host.Children.Add(wrap);
            host.Children.Add(_pane = BuildNewShapePane());

            return host;
        }

        // ── new-shape pane (slide in from the right) ──

        private Border? _pane;      // full-cover scrim
        private Border? _paneCard;  // right-docked card that slides
        private bool _paneOpen;
        private readonly TextBox _paneName = new()
        {
            Watermark = "shape name",
            MinWidth = 260,
            FontSize = PictosEditorPanels.FontLabel,
        };
        private string? _paneAction;
        private TextBlock? _paneError;

        private Border BuildNewShapePane()
        {
            var title = new TextBlock
            {
                Text = "New Shape",
                Foreground = Gold,
                FontSize = PictosEditorPanels.FontLabel + 4,
                FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
                FontWeight = FontWeight.SemiBold,
            };
            var hint = new TextBlock
            {
                Text = "Name this arrangement. It is saved from the current canvas and becomes the viewed shape.",
                Foreground = Muted,
                FontSize = PictosEditorPanels.FontHint,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 300,
            };
            var error = new TextBlock
            {
                Foreground = Danger,
                FontSize = PictosEditorPanels.FontHint,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 300,
            };
            _paneError = error;

            var confirm = new Button
            {
                Content = "Confirm",
                Classes = { "tile" },
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            confirm.Click += (_, _) =>
            {
                var name = _paneName.Text?.Trim() ?? "";
                if (name.Length == 0)
                {
                    error.Text = "Type a name first.";
                    return;
                }
                if (_paneAction is null) return;
                if (_paneAction == "new")
                {
                    if (_state.Presets.ContainsKey(name))
                    {
                        error.Text = $"A shape named '{name}' already exists. Choose another name.";
                        return;
                    }
                    SaveShape(name);
                    _status!.Text = $"Created shape '{name}' ({_nodes.Count} moves).";
                }
                ClosePane();
                RefreshShapes();
            };

            var cancel = new Button
            {
                Content = "Exit",
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            cancel.Click += (_, _) => ClosePane();

            _paneName.KeyDown += (_, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Enter)
                {
                    confirm.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                }
            };

            var stack = new StackPanel
            {
                Spacing = 12,
                Margin = new Thickness(24),
                VerticalAlignment = VerticalAlignment.Center,
            };
            stack.Children.Add(title);
            stack.Children.Add(hint);
            stack.Children.Add(error);
            stack.Children.Add(_paneName);
            stack.Children.Add(confirm);
            stack.Children.Add(cancel);

            var card = new Border
            {
                Width = 380,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Classes = { "panel" },
                Background = new SolidColorBrush(Color.Parse("#FF1D1912")),
                Child = stack,
            };
            var translate = new TranslateTransform();
            card.RenderTransform = translate;
            translate.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = TranslateTransform.XProperty,
                    Duration = TimeSpan.FromMilliseconds(340),
                    Easing = new CubicEaseInOut(),
                }
            };

            // Full-cover scrim with the card docked to its right edge. Clicking the
            // scrim (not the card) dismisses the pane.
            var scrim = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#66100E0B")),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = card,
                IsVisible = false,
                ZIndex = 10,
            };
            scrim.PointerPressed += (_, e) =>
            {
                // Ignore presses that began inside the card (buttons, the text box).
                if (card.IsPointerOver || e.GetPosition(card).X >= 0 &&
                    e.GetPosition(card).X <= card.Bounds.Width &&
                    e.GetPosition(card).Y >= 0 && e.GetPosition(card).Y <= card.Bounds.Height)
                    return;
                ClosePane();
            };
            _paneCard = card;
            return scrim;
        }

        private void OpenPane()
        {
            if (_pane is null || _paneCard is null) return; // graph failed to load
            _paneAction = "new";
            _paneName.Text = "";
            _paneName.Watermark = "shape name";
            if (_paneError is not null) _paneError.Text = "";
            _paneOpen = true;
            _pane.IsVisible = true;
            SetPaneX(1600);
            // Let the scrim lay out, then slide the card in (the same reveal
            // sequence the Save Editor uses).
            Dispatcher.UIThread.Post(() =>
            {
                SetPaneX(0);
                _paneName.Focus();
            });
        }

        private async void ClosePane()
        {
            if (_pane is null || !_paneOpen) return;
            _paneOpen = false;
            SetPaneX(1600);
            // After the slide-out completes (340ms transition), hide the scrim so it
            // no longer captures pointer events.
            await Task.Delay(360);
            if (_pane is not null && !_paneOpen)
                _pane.IsVisible = false;
        }

        private void SetPaneX(double x)
        {
            if (_paneCard?.RenderTransform is TranslateTransform t)
                t.X = x;
        }

        private Control BuildToolbar()
        {
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

            bar.Children.Add(new TextBlock
            {
                Text = "Shape:",
                Foreground = Muted,
                FontSize = PictosEditorPanels.FontLabel,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var shapeCombo = new ComboBox
            {
                MinWidth = 230,
                FontSize = PictosEditorPanels.FontLabel,
                VerticalAlignment = VerticalAlignment.Center,
                // Without this, Avalonia renders each item via ToString() — the
                // ShapeItem type name (E33ModEditor.App…+ShapeItem) would show.
                // Null-safe: Avalonia recycles item containers during an
                // ItemsSource swap and can invoke the template with a null item.
                ItemTemplate = new FuncDataTemplate<ShapeItem>((item, _) =>
                    new TextBlock
                    {
                        Text = item?.Label ?? "",
                        VerticalAlignment = VerticalAlignment.Center,
                    }),
            };
            _shapeCombo = shapeCombo;
            shapeCombo.SelectionChanged += (_, _) =>
            {
                if (_suppressShapeApply) return;
                if (shapeCombo.SelectedItem is not ShapeItem item || item.IsPlaceholder) return;
                if (item.IsVanilla)
                {
                    ApplyVanillaToCanvas();
                    _state.Shape = "vanilla";
                }
                else if (item.IsCustom)
                {
                    _state.Shape = "custom";
                    ApplyStateToCanvas();
                }
                else
                {
                    _state.Shape = item.Name;
                    ApplyStateToCanvas();
                }
                Save();
                RefreshActiveUi();
                if (_status is not null) _status.Text = $"Viewing: {item.Label}";
            };
            bar.Children.Add(shapeCombo);

            // Gold chip shown when the viewed shape is the Active (packed) one.
            var activeChip = new TextBlock
            {
                Text = "★ ACTIVE",
                Foreground = Gold,
                FontSize = PictosEditorPanels.FontHint,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = false,
            };
            _activeChip = activeChip;
            bar.Children.Add(activeChip);

            // New Shape: slide-in pane asks for a name, then saves the current
            // canvas as a fresh preset (becomes the viewed shape, not Active).
            var newBtn = new Button
            {
                Content = "New Shape",
                Classes = { "tile" },
                VerticalAlignment = VerticalAlignment.Center,
            };
            newBtn.Click += (_, _) => OpenPane();
            bar.Children.Add(newBtn);

            // Save Shape: commits the current canvas into the viewed shape.
            // (Edits already auto-save on drag/link; this is the explicit commit.)
            var saveBtn = new Button
            {
                Content = "Save shape",
                Classes = { "tile" },
                VerticalAlignment = VerticalAlignment.Center,
            };
            saveBtn.Click += (_, _) => SaveViewedShape();
            bar.Children.Add(saveBtn);

            // Mark the currently-viewed layout as the one the mod pack applies.
            var activeBtn = new Button
            {
                Content = "Set Active",
                Classes = { "tile" },
                VerticalAlignment = VerticalAlignment.Center,
            };
            activeBtn.Click += (_, _) =>
            {
                _state.Active = _state.Shape;
                Save();
                RefreshActiveUi();
                if (_status is not null)
                    _status.Text = $"Active: {ShapeLabel(_state.Active)}. The pack will use this graph.";
            };
            bar.Children.Add(activeBtn);

            // Delete the currently-viewed layout (falls back to vanilla if it was active).
            var deleteBtn = new Button
            {
                Content = "Delete shape",
                Classes = { "tile" },
                VerticalAlignment = VerticalAlignment.Center,
            };
            deleteBtn.Click += (_, _) => DeleteViewedShape();
            bar.Children.Add(deleteBtn);

            var resetBtn = new Button
            {
                Content = "Reset to vanilla",
                Classes = { "tile" },
                VerticalAlignment = VerticalAlignment.Center,
            };
            resetBtn.Click += (_, _) =>
            {
                ApplyVanillaToCanvas();
                _state.Shape = "vanilla";
                Save();
                RefreshShapes();
                RefreshActiveUi();
                if (_status is not null) _status.Text = "Viewing the vanilla layout.";
            };
            bar.Children.Add(resetBtn);

            _status = new TextBlock
            {
                Text = "",
                Foreground = Muted,
                FontSize = PictosEditorPanels.FontHint,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 700,
            };
            bar.Children.Add(_status);

            RefreshShapes();
            RefreshActiveUi();
            return bar;
        }

        private sealed class ShapeItem
        {
            public required string Name { get; init; }
            public required string Label { get; init; }
            public bool IsVanilla { get; init; }
            public bool IsCustom { get; init; }
            public bool IsPlaceholder { get; init; }
        }

        private void RefreshShapes()
        {
            if (_shapeCombo is null) return;
            var shapeCombo = _shapeCombo;
            _suppressShapeApply = true;
            try
            {
                static string ActiveSuffix(string name, string active) =>
                    name == active ? "  ★ active" : "";
                var items = new List<ShapeItem>
                {
                    new() { Name = "vanilla", Label = "Vanilla (default)" + ActiveSuffix("vanilla", _state.Active), IsVanilla = true },
                    new() { Name = "custom", Label = "Custom (current)" + ActiveSuffix("custom", _state.Active), IsCustom = true },
                };
                foreach (var preset in _state.Presets.Keys.OrderBy(k => k, StringComparer.Ordinal))
                    items.Add(new ShapeItem { Name = preset, Label = preset + ActiveSuffix(preset, _state.Active) });
                // Swap the whole ItemsSource rather than mutating Items in place —
                // clearing a realized ComboBox's item collection can throw depending
                // on focus/drop-down state.
                shapeCombo.ItemsSource = items;
                var wanted = _state.Shape is "vanilla" or "custom"
                    ? items.First(x => x.Name == _state.Shape)
                    : items.FirstOrDefault(x => x.Name == _state.Shape);
                // Select by index: if the viewed shape is missing (deleted/renamed)
                // fall back to Vanilla at index 0 so the box is never left empty.
                shapeCombo.SelectedIndex = wanted is null ? 0 : items.IndexOf(wanted);
            }
            finally
            {
                _suppressShapeApply = false;
            }
        }

        private static string ShapeLabel(string shape) => shape switch
        {
            "vanilla" => "Vanilla (default)",
            "custom" => "Custom (current)",
            "" => "Vanilla (default)",
            _ => shape,
        };

        /// <summary>Shows the ★ ACTIVE chip when the viewed shape is the one the pack applies.</summary>
        private void RefreshActiveUi()
        {
            if (_activeChip is not null)
                _activeChip.IsVisible = _state.Shape == _state.Active;
        }

        /// <summary>A sensible shape to land on after the viewed one is deleted:
        /// the live Custom layout if it has any content, else the vanilla baseline.</summary>
        private string FallbackShape() =>
            _state.Custom.Positions.Count > 0 || _state.Custom.Links.Count > 0 ? "custom" : "vanilla";

        /// <summary>Deletes the currently-viewed layout. If it was the active one,
        /// active falls back to the next best working shape (custom if it has
        /// content, else vanilla). Viewing switches to that same fallback so the
        /// editor never strands the user on an empty graph.</summary>
        private void DeleteViewedShape()
        {
            var viewed = _state.Shape;
            if (viewed is "vanilla" or "")
            {
                if (_status is not null) _status.Text = "The vanilla layout is built-in and cannot be deleted.";
                return;
            }
            var fallback = FallbackShape();
            if (viewed == "custom")
            {
                _state.Custom = new SkillGraphKit.GraphLayout();
                // Clearing Custom means it no longer has content; land on vanilla.
                if (_state.Active == "custom") _state.Active = "vanilla";
                _state.Shape = "vanilla";
                ApplyVanillaToCanvas();
                Save();
                RefreshShapes();
                RefreshActiveUi();
                if (_status is not null) _status.Text = "Custom layout cleared. Viewing the vanilla layout.";
                return;
            }
            // A named preset.
            _state.Presets.Remove(viewed);
            if (_state.Active == viewed) _state.Active = fallback;
            _state.Shape = fallback;
            if (fallback == "vanilla") ApplyVanillaToCanvas();
            else ApplyStateToCanvas();
            Save();
            RefreshShapes();
            RefreshActiveUi();
            if (_status is not null)
                _status.Text = fallback == "vanilla"
                    ? $"Deleted shape '{viewed}'. Viewing the vanilla layout."
                    : $"Deleted shape '{viewed}'. Viewing your Custom layout.";
        }

        // ── graph load + enrichment ──

        private void LoadGraph()
        {
            SkillGraphKit.GraphData data;
            try
            {
                data = SkillGraphKit.LoadEditorBaseline(_graphStem);
            }
            catch (Exception e)
            {
                _loadError = "Could not load " + _graphStem + ": " + e.Message;
                return;
            }

            foreach (var raw in data.Nodes)
            {
                _vanillaPositions[raw.Stem] = (raw.X, raw.Y);
                _nodes.Add(new SkillGraphNodeVM
                {
                    Raw = raw,
                    DisplayName = raw.DisplayName.Length > 0 ? raw.DisplayName : raw.Stem,
                });
            }
            _vanillaLinks.AddRange(data.Links.Select(l => (_nodes[l.From].Stem, _nodes[l.To].Stem)));

            // Enrich from the skills module rows (display name, element, gradient,
            // description). Descriptions prefer the modded text when a kit
            // rewrites the move, else the vanilla string-table text. Row keys
            // are full DA stems ("DA_Skill_..."), so index the nodes the same
            // way (GraphNode.Stem strips the prefix and never matches).
            var bySkill = _nodes.ToDictionary(n => n.Raw.Skill, StringComparer.Ordinal);
            var gustaveEdits = GustaveSkillsKit.DescriptionEdits();
            foreach (var (stem, row) in _vm.SkillsByStem())
            {
                if (!bySkill.TryGetValue(stem, out var vmNode)) continue;
                Enrich(vmNode, row.DisplayName,
                    MainViewModel.TokenAt(row.Baseline, "BaseElementalType")?.ToString() ?? "",
                    MainViewModel.TokenAt(row.Baseline, "SkillType")?.ToString() ?? "");
                var vanillaLong = _vm.DescribeSkill(row).Description;
                vmNode.Description = ModdedDescription(vmNode.Raw.Skill, DescBaseKey(row), vanillaLong, gustaveEdits)
                    ?? vanillaLong;
            }
            // Kit-created skills are not in the skills-module rows (they exist only
            // in the staged mod tree), so enrich them straight from their DA.
            foreach (var vmNode in _nodes)
            {
                if (vmNode.Element.Length > 0 && vmNode.Description.Length > 0) continue;
                var (element, type, nameKey, _) = SkillGraphKit.ReadDaDisplayInfo(vmNode.Raw.Skill);
                if (element.Length == 0 && type.Length == 0 && nameKey.Length == 0) continue;
                var display = _vm.StringForKey(nameKey);
                Enrich(vmNode, display.Length > 0 ? display : vmNode.DisplayName, element, type);
                if (vmNode.Description.Length == 0)
                    vmNode.Description = ModdedDescription(vmNode.Raw.Skill, null, "", gustaveEdits) ?? "";
            }
            // Moves whose element is kismet/description-driven (not a DA field):
            // Blitz Overcharge = Lightning, Genesis family = Multi, Gustave weapon moves.
            foreach (var vmNode in _nodes)
            {
                if (vmNode.Element.Length > 0) continue;
                var over = SkillGraphKit.ElementOverride(vmNode.Raw.Skill);
                if (over is not null) vmNode.Element = over;
            }
        }

        /// <summary>String-table base key for a skill row's Long description
        /// (e.g. "Lune_StormCaller_Description"), from the row's Description
        /// token. Null when the row carries no table reference.</summary>
        private static string? DescBaseKey(RecordRow row)
        {
            var key = GameLabels.StringTableKey(MainViewModel.TokenAt(row.Baseline, "Description")?.ToString());
            if (key is null) return null;
            return key.EndsWith("_Long", StringComparison.Ordinal) ? key[..^"_Long".Length] : key;
        }

        /// <summary>Modded Long description for one graph node: kit-authored new
        /// moves first (they have no vanilla text), then the Gustave / Maelle /
        /// Sciel / Lune description rewrites by string-table base key. Null = unmodded
        /// (caller falls back to the vanilla string-table text).</summary>
        private static string? ModdedDescription(string daStem, string? descBaseKey, string vanillaLong,
            IReadOnlyDictionary<string, string> gustaveEdits)
        {
            foreach (var s in LuneSkillsKit.Skills)
                if (string.Equals(s.DaStem, daStem, StringComparison.Ordinal)) return s.Description;
            foreach (var m in GustaveSkillsKit.NewMoves)
                if (string.Equals(m.DaStem, daStem, StringComparison.Ordinal)) return m.Description;
            foreach (var m in MaelleSkillsKit.NewMoves)
                if (string.Equals(m.DaStem, daStem, StringComparison.Ordinal)) return m.Description;
            // Sciel customs reach here with descBaseKey=null (pass-2 path), so match
            // by stem directly: "DA_Skill_Sciel_BlackDeath" -> "Sciel_BlackDeath".
            var stemKey = daStem.StartsWith("DA_Skill_", StringComparison.Ordinal)
                ? daStem["DA_Skill_".Length..] : daStem;
            if (ScielCustomKit.TryPreviewDescription(stemKey, out var cuStem)) return cuStem;
            if (descBaseKey is { Length: > 0 })
            {
                if (gustaveEdits.TryGetValue(descBaseKey + "_Description_Long", out var g)) return g;
                if (MaelleSkillsKit.TryPreviewDescription(descBaseKey, out var ml)) return ml;
                if (ScielSkillsKit.TryPreviewDescription(descBaseKey, out var sc)) return sc;
                if (ScielCustomKit.TryPreviewDescription(descBaseKey, out var cu)) return cu;
                if (VersoSkillsKit.TryPreviewDescription(descBaseKey, out var vs)) return vs;
                if (MonocoSkillsKit.TryPreviewDescription(descBaseKey, out var mo)) return mo;
                if (SkillDescriptionsKit.TryPreviewDescription(descBaseKey, vanillaLong, out var sl)) return sl;
            }
            return null;
        }

        private static void Enrich(SkillGraphNodeVM vmNode, string displayName, string elementRaw, string typeRaw)
        {
            vmNode.DisplayName = displayName.Length > 0 ? displayName : vmNode.Stem;
            vmNode.Element = GameLabels.Decode("BaseElementalType", elementRaw);
            var type = GameLabels.Decode("SkillType", typeRaw);
            vmNode.IsGradient = type == "Gradient Attack"
                || vmNode.Raw.RequiresUnlockItem.StartsWith("GradientUnlock_", StringComparison.Ordinal);
        }

        /// <summary>Applies the vanilla layout (positions + links) to the canvas.</summary>
        private void ApplyVanillaToCanvas()
        {
            if (_canvas is null) return;
            _canvas.SetPositions(_vanillaPositions.Select(kv => (kv.Key, kv.Value.X, kv.Value.Y)));
            _canvas.SetLinks(_vanillaLinks);
        }

        /// <summary>Applies the character's current shape (custom or a named preset).</summary>
        private void ApplyStateToCanvas()
        {
            if (_canvas is null) return;
            if (_state.Shape is "vanilla" or "")
            {
                ApplyVanillaToCanvas();
                return;
            }
            var layout = ResolveLayout();
            if (layout is null) return;
            var positions = new List<(string Stem, double X, double Y)>();
            foreach (var vmNode in _nodes)
            {
                var hit = layout.Positions.FirstOrDefault(p => p.Skill == vmNode.Stem);
                positions.Add(hit.Skill is null
                    ? (vmNode.Stem, vmNode.Raw.X, vmNode.Raw.Y)
                    : (vmNode.Stem, hit.X, hit.Y));
            }
            _canvas.SetPositions(positions);
            _canvas.SetLinks(layout.Links.Select(l => (l.From, l.To)));
        }

        private SkillGraphKit.GraphLayout? ResolveLayout()
        {
            return _state.Shape switch
            {
                "custom" => _state.Custom,
                _ => _state.Presets.TryGetValue(_state.Shape, out var p) ? p : null,
            };
        }

        // ── commit + shapes ──

        private void CaptureLayout(SkillGraphKit.GraphLayout into)
        {
            into.Positions.Clear();
            foreach (var vmNode in _nodes)
                into.Positions.Add((vmNode.Stem, vmNode.Raw.X, vmNode.Raw.Y));
            into.Links.Clear();
            into.Links.AddRange(_canvas is null ? _vanillaLinks : _canvas.CurrentLinks);
        }

        /// <summary>Where a live edit (drag/link) lands, based on what is viewed.</summary>
        private SkillGraphKit.GraphLayout? EditTarget()
        {
            if (_state.Shape == "custom") return _state.Custom;
            if (_state.Shape is "vanilla" or "") return null; // first edit breaks off into Custom
            return _state.Presets.TryGetValue(_state.Shape, out var preset) ? preset : null;
        }

        /// <summary>A live graph change. While viewing a named preset the edit goes
        /// straight into that preset; from Vanilla the first edit breaks off into the
        /// live Custom layout and switches the view to it.</summary>
        private void OnGraphChanged()
        {
            var target = EditTarget();
            if (target is null)
            {
                // Viewing vanilla: the first edit creates the custom arrangement.
                CaptureLayout(_state.Custom);
                _state.Shape = "custom";
                RefreshShapes(); // combo now shows Custom (current)
            }
            else
            {
                CaptureLayout(target);
            }
            Save();
            RefreshActiveUi();
        }

        /// <summary>Creates a brand-new named preset from the current canvas and
        /// selects it as the viewed shape (it does not become Active).</summary>
        private void SaveShape(string name)
        {
            var layout = new SkillGraphKit.GraphLayout();
            CaptureLayout(layout);
            _state.Presets[name] = layout;
            _state.Shape = name;
            Save();
        }

        /// <summary>Overwrite the currently-viewed shape with the current canvas.
        /// Viewing Vanilla has nothing to save into, so it does nothing.</summary>
        private void SaveViewedShape()
        {
            if (_state.Shape is "vanilla" or "")
            {
                if (_status is not null) _status.Text =
                    "Viewing the vanilla layout — nothing to save. Press New Shape to make a copy.";
                return;
            }
            var target = EditTarget();
            if (target is null) return;
            CaptureLayout(target);
            Save();
            RefreshShapes();
            if (_status is not null)
                _status.Text = $"Saved the current layout into '{ShapeLabel(_state.Shape)}' ({_nodes.Count} moves).";
        }

        private void Save() => _vm.SaveSkillGraph();

        /// <summary>Re-applies the persisted shape when the tab is re-shown.</summary>
        public void RefreshView()
        {
            if (_canvas is null) return;
            ApplyStateToCanvas();
            _canvas.InvalidateVisual();
        }

        /// <summary>The user left this tab: dismiss any open pane so the next visit
        /// starts clean (instantly — the tab content is hidden anyway).</summary>
        public void OnTabLeft()
        {
            _paneOpen = false;
            if (_pane is not null) _pane.IsVisible = false;
        }
    }
}

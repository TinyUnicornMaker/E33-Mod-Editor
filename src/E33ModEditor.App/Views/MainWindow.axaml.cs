using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.VisualTree;
using E33ModEditor.App.ViewModels;
using E33ModEditor.Core;

namespace E33ModEditor.App.Views;

public partial class MainWindow : Window
{
    private enum Page { Launcher, Editor, Detail }

    private static readonly IBrush StripeA = new SolidColorBrush(Color.Parse("#FF161411"));
    private static readonly IBrush StripeB = new SolidColorBrush(Color.Parse("#FF231E18"));

    private Page _page = Page.Launcher;
    private bool _sliding;
    private bool _suppressSelection;

    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        DataContext = vm;
        // Pack overlay: keep the scrolling log pinned to the newest line.
        vm.PackLog.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is { Count: > 0 } && PackLogList is not null)
                PackLogList.ScrollIntoView(e.NewItems[^1]);
        };

        SearchBox.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == "Text")
                vm.FilterText = e.NewValue?.ToString() ?? "";
        };

        vm.PropertyChanged += OnVmChanged;
        vm.PickSaveProjectPath = PickSaveProjectPath;
        vm.PickLoadProjectPath = PickLoadProjectPath;
        vm.PickPartyModFile = PickPartyModFile;
        vm.SaveEditor.ModuleExitRequested += () => vm.GoBack();
        Opened += async (_, _) =>
        {
            await vm.PreloadAllAsync();
            // Test hook: E33_AUTOOPEN=<moduleId> opens that editor immediately.
            var auto = Environment.GetEnvironmentVariable("E33_AUTOOPEN");
            if (!string.IsNullOrEmpty(auto))
                await vm.OpenTileAsync(vm.Modules.Concat(vm.Modules.SelectMany(x => x.Children))
                    .FirstOrDefault(m => m.Id == auto));
        };
        AttachSlideTransitions(LauncherPage);
        AttachSlideTransitions(EditorPage);
        AttachSlideTransitions(DetailPage);
    }

    private async Task<string?> PickSaveProjectPath()
    {
        var folder = await StorageProvider.TryGetFolderFromPathAsync(EditorPaths.ExportDir);
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save editor project",
            SuggestedFileName = "E33ModEditor.project.json",
            DefaultExtension = "project.json",
            SuggestedStartLocation = folder,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("Editor project") { Patterns = new[] { "*.project.json", "*.json" } },
            },
        });
        return file?.TryGetLocalPath();
    }


    private async Task<string?> PickLoadProjectPath()
    {        var folder = await StorageProvider.TryGetFolderFromPathAsync(EditorPaths.ExportDir);
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load editor project",
            AllowMultiple = false,
            SuggestedStartLocation = folder,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Editor project") { Patterns = new[] { "*.project.json", "*.json" } },
            },
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private async Task<string?> PickPartyModFile()
    {
        var folder = await StorageProvider.TryGetFolderFromPathAsync(EditorPaths.ModsDir);
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Locate the Bigger Party Mod container",
            AllowMultiple = false,
            SuggestedStartLocation = folder,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Unreal mod container") { Patterns = new[] { "*.utoc", "*.pak" } },
            },
        });
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path is null) return null;
        // The .utoc is the entry point; derive it if a .pak/.ucas was picked.
        if (!path.EndsWith(".utoc", StringComparison.OrdinalIgnoreCase))
            path = Path.ChangeExtension(path, ".utoc");
        return File.Exists(path) ? path : null;
    }

    private static void AttachSlideTransitions(Control page)
    {
        if (page.RenderTransform is not TranslateTransform t)
            page.RenderTransform = t = new TranslateTransform();
        t.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = TranslateTransform.XProperty,
                Duration = TimeSpan.FromMilliseconds(340),
                Easing = new CubicEaseInOut(),
            }
        };
    }

    private Control PageControl(Page page) => page switch
    {
        Page.Editor => EditorPage,
        Page.Detail => DetailPage,
        _ => LauncherPage,
    };

    private async Task SlideTo(Page next, bool forward)
    {
        if (next == _page || _sliding) return;
        _sliding = true;
        var from = PageControl(_page);
        var to = PageControl(next);
        var w = Math.Max(Stage.Bounds.Width, Bounds.Width);
        if (w < 8) w = 1400;

        to.IsVisible = true;
        to.ZIndex = 2;
        from.ZIndex = 1;
        SetX(to, forward ? w : -w);
        SetX(from, 0);

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
        SetX(to, 0);
        SetX(from, forward ? -w : w);
        await Task.Delay(360);

        from.IsVisible = false;
        SetX(from, 0);
        from.ZIndex = 0;
        to.ZIndex = 1;
        _page = next;
        _sliding = false;
    }

    private static void SetX(Control c, double x)
    {
        if (c.RenderTransform is TranslateTransform t)
            t.X = x;
        else
            c.RenderTransform = new TranslateTransform(x, 0);
    }

    private async void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (e.PropertyName is nameof(MainViewModel.ActiveModule) or nameof(MainViewModel.ActiveTable)
            or nameof(MainViewModel.IsEditing))
        {
            RebuildColumns();
            ShowDirectEditor(vm);
        }

        if (e.PropertyName is nameof(MainViewModel.IsEditing) or nameof(MainViewModel.IsHubOpen)
            or nameof(MainViewModel.ActiveHub) or nameof(MainViewModel.ActiveModule))
        {
            if (vm.IsEditing)
                await SlideTo(Page.Editor, forward: true);
            else
                await SlideTo(Page.Launcher, forward: false);
        }
    }

    private void ShowDirectEditor(MainViewModel vm)
    {
        var direct = vm.IsEditing && vm.ActiveModule?.IsDirectEditor == true;
        RecordGrid.IsVisible = !direct;
        SearchBox.IsVisible = !direct;
        DirectHost.IsVisible = direct;
        if (!direct)
        {
            DirectHost.Content = null;
            return;
        }
        if (vm.ActiveModule?.Id == "weapon-scaling")
        {
            var row = vm.WeaponScalingRow ?? (vm.Rows.Count > 0 ? vm.Rows[0] : null);
            DirectHost.Content = row is null ? null : WeaponEditorPanels.BuildCurve(vm, row);
        }
        else if (vm.ActiveModule?.Id == "pictos-scaling")
        {
            var row = vm.PictosScalingRow ?? (vm.Rows.Count > 0 ? vm.Rows[0] : null);
            DirectHost.Content = row is null ? null : PictosEditorPanels.BuildCurve(vm, row);
        }
        else if (vm.ActiveModule?.Id == "pictos-create")
        {
            DirectHost.Content = PictosCreatePanels.Build(vm);
        }
        else if (vm.ActiveModule?.Id == "weapon-effects-create")
        {
            DirectHost.Content = WeaponEffectsCreatePanels.Build(vm);
        }
        else if (vm.ActiveModule?.Id == "save-editor")
        {
            DirectHost.Content = SaveEditorPanels.Build(vm.SaveEditor);
        }
        else if (vm.ActiveModule?.Id == "gameplay-values")
        {
            DirectHost.Content = GameplayTweaksPanels.Build(vm);
        }
        else if (vm.ActiveModule?.Id == "gameplay-compat")
        {
            DirectHost.Content = CompatibilityPanels.Build(vm);
        }
        else if (vm.ActiveModule?.Id == "camera")
        {
            DirectHost.Content = CameraPanels.Build(vm);
        }
        else if (vm.ActiveModule?.Id == "party-five")
        {
            DirectHost.Content = PartyFivePanels.Build(vm);
        }
        else if (vm.ActiveModule?.Id == "skill-overhaul")
        {
            DirectHost.Content = SkillOverhaulPanels.Build(vm);
        }
        else if (vm.ActiveModule?.Id == "skill-graph-editor")
        {
            DirectHost.Content = GraphEditorPanels.Build(vm);
        }
        else if (vm.ActiveModule?.Id == "chests-drops")
        {
            DirectHost.Content = LootPanels.Build(vm.Loot);
        }
        else if (vm.ActiveModule?.Id == "randomizer")
        {
            DirectHost.Content = RandomizerPanels.Build(vm);
        }
        else if (vm.ActiveModule?.Id == "tier-editor")
        {
            vm.Tiers.Attach(vm.Loot.Catalog);
            DirectHost.Content = TierEditorPanels.Build(vm.Tiers);
        }
    }

    private void RebuildColumns()
    {
        if (DataContext is not MainViewModel vm) return;
        var cols = vm.ActiveModule is null ? Array.Empty<ColumnSpec>() : vm.ColumnsFor(vm.ActiveTable);
        DetailTitle.Text = vm.ActiveTitle;

        RecordGrid.Columns.Clear();
        RecordGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Name",
            Tag = "DisplayName",
            Binding = new Avalonia.Data.Binding("DisplayName"),
            IsReadOnly = true,
            Width = new DataGridLength(240),
        });

        for (int i = 0; i < cols.Length; i++)
        {
            var col = cols[i];
            RecordGrid.Columns.Add(new DataGridTextColumn
            {
                Header = col.Header,
                Tag = col.Path,
                Binding = new Avalonia.Data.Binding($"Values[c{i}]"),
                IsReadOnly = true,
                Width = new DataGridLength(130),
            });
        }
    }

    private void OnSorting(object? sender, DataGridColumnEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var prop = e.Column.Tag as string;
        if (string.IsNullOrEmpty(prop)) { e.Handled = true; return; }

        var (_, desc) = vm.SortBy(prop);

        foreach (var col in RecordGrid.Columns)
        {
            if (col.Tag is string p && !string.IsNullOrEmpty(p))
                col.Header = StripArrow(col.Header as string);
        }
        var baseHeader = StripArrow(e.Column.Header as string);
        e.Column.Header = desc ? $"{baseHeader} ▼" : $"{baseHeader} ▲";
        e.Handled = true;
    }

    private static string StripArrow(string? header)
    {
        if (string.IsNullOrEmpty(header)) return "";
        return header.EndsWith(" ▲") || header.EndsWith(" ▼") ? header[..^2] : header;
    }

    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (DataContext is not MainViewModel vm || e.Row.DataContext is not RecordRow row) return;
        var idx = vm.Rows.IndexOf(row);
        if (idx < 0) return;
        e.Row.Background = (idx / 3) % 2 == 0 ? StripeA : StripeB;
        if (!string.IsNullOrEmpty(row.HoverBody) || !string.IsNullOrEmpty(row.HoverTitle))
            HoverTips.Set(e.Row, row.HoverTitle, row.HoverBody);
        else
            ToolTip.SetTip(e.Row, null);
    }

    private async void OnRowSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection) return;
        if (DataContext is not MainViewModel vm || vm.SelectedRow is not { } row) return;
        if (_page == Page.Detail) return;
        try
        {
            DetailTitle.Text = string.IsNullOrEmpty(row.DisplayName) ? row.RowName : row.DisplayName;
            var editorView = new RecordEditorView(vm, row);
            editorView.CloseRequested += async (_, _) => await CloseDetailAsync();
            DetailHost.Content = editorView;
            await SlideTo(Page.Detail, forward: true);
        }
        catch (Exception ex)
        {
            vm.Status = $"Editor failed: {ex.Message}";
            Console.Error.WriteLine($"[E33ModEditor] editor crash: {ex}");
        }
    }

    private async void OnEditorBackClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.GoBack();
        await Task.CompletedTask;
    }

    private async void OnDetailBackClick(object? sender, RoutedEventArgs e) =>
        await CloseDetailAsync();

    private async Task CloseDetailAsync()
    {
        if (DataContext is MainViewModel vm)
        {
            // Flush typed edits before teardown: the focused textbox may
            // never have lost focus, so commit explicitly while the VM
            // is alive and before the grid re-hydrates below.
            if (DetailHost.Content is RecordEditorView detailView)
                detailView.CommitPending();
            var row = vm.SelectedRow;
            await SlideTo(Page.Editor, forward: false);
            _suppressSelection = true;
            vm.SelectedRow = null;
            RecordGrid.SelectedItem = null;
            _suppressSelection = false;
            if (row is not null) vm.AfterEditorClosed(row);
            DetailHost.Content = null;
        }
    }
}

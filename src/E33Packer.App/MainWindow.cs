using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using E33ModEditor.Core;
using E33ModEditor.Core.Localization;
using E33ModEditor.Core.Loot;
using E33ModEditor.Core.Packer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static E33Packer.App.AppLocalization;

namespace E33Packer.App;

public sealed partial class MainWindow : Window
{
    private sealed record PresetOption(string Name, string Label)
    {
        public override string ToString() => Label;
    }

    private const string AppVersion = GeneratorInfo.AppVersion;
    private const string PackRevision = GeneratorInfo.PackRevision;

    private readonly AppSettings _app;
    private SeedHistory _history;
    private GeneratorProfile _profile;

    private readonly TextBox _gameBox;
    private readonly TextBox _modsBox;
    private TextBox _seedBox = null!;
    private readonly TextBox _log;
    private readonly TextBlock _status;
    private readonly TextBlock _stageText;
    private readonly ProgressBar _stageBar;
    private readonly Button _build;
    private readonly Button _cancel;
    private readonly TabControl _tabs;
    private readonly ComboBox _presetBox;
    private ComboBox? _historyBox;
    private TextBlock? _seedPackLabel;
    private TextBlock? _headerSeedLabel;
    private Expander? _setupExpander;
    private readonly Border _overlay;
    private readonly ProgressBar _extractBar;
    private readonly TextBlock _extractTitle;
    private readonly TextBlock _extractDetail;

    private bool _busy;
    private bool _suppressPreset;
    private bool _presetChangeInProgress;
    private int _presetChangeVersion;
    private bool _buildingTabs;
    private int _languageGroup;
    private string? _buildLogPath;
    private CancellationTokenSource? _buildCts;
    private readonly TaskCompletionSource _revealed =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Called by the app shell after the window has been positioned.</summary>
    public void MarkRevealed() => _revealed.TrySetResult();

    public MainWindow()
    {
        _app = AppSettings.Load();
        ConfigurePathsForStartup(_app);
        try { DonorExtract.RecoverSuspendedMods(); } catch { /* best effort */ }
        SeedData.Ensure();
        _profile = NormalizeProfile(LoadLastProfile());
        AppLocalization.SetLanguage(_profile.Language);
        ModLocalization.SetLanguage(_profile.Language);
        _history = SeedHistory.Load();

        Title = $"Expedition Infinity {AppVersion}";
        Icon = AppIcon.Icon;
        Width = Math.Max(900, _app.WindowWidth);
        Height = Math.Max(680, _app.WindowHeight);
        MinWidth = 900;
        MinHeight = 680;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _gameBox = new TextBox { Watermark = T("Expedition 33 game folder"), Text = _app.GameFolder ?? "" };
        _modsBox = new TextBox { Watermark = T("Install folder (created if missing)"), Text = _app.ModsFolder ?? "" };
        _log = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("DejaVu Sans Mono, Consolas, monospace"),
            FontSize = 12,
            MinHeight = 110,
            Height = 130,
        };
        _status = new TextBlock { Classes = { "hint" }, Text = T("Ready."), TextWrapping = TextWrapping.Wrap };
        _stageText = new TextBlock
        {
            Classes = { "hint" },
            Text = T("Idle."),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };
        _stageBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Height = 14,
            MinHeight = 14,
            IsVisible = false,
        };
        _build = new Button { Content = T("Build seed pack"), FontWeight = FontWeight.SemiBold };
        ToolTip.SetTip(_build, T("Build the pack and install it into the game's ~mods folder."));
        _cancel = new Button
        {
            Content = T("Cancel"),
            IsVisible = false,
        };
        ToolTip.SetTip(_cancel,
            T("Cancel between build stages; the installed pack is not replaced."));
        _cancel.Click += (_, _) => CancelBuild();
        _tabs = new TabControl();
        _presetBox = new ComboBox { ItemsSource = PresetOptions(), Width = 200 };

        _extractTitle = new TextBlock
        {
            Classes = { "title" },
            Text = T("Preparing game data"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _extractDetail = new TextBlock
        {
            Classes = { "hint" },
            Text = T("This one-time setup reads game data. Later builds skip it."),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };
        _extractBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Height = 16,
            MinHeight = 16,
        };
        _overlay = BuildOverlay();

        Content = BuildRoot();

        SelectPreset(_profile.Name);
        _presetBox.SelectionChanged += async (_, _) =>
        {
            if (_suppressPreset || _presetChangeInProgress) return;
            if (_presetBox.SelectedItem is not PresetOption option) return;
            if (option.Name == GeneratorPresets.Custom)
            {
                if (_profile.Name != GeneratorPresets.Custom)
                {
                    _profile.Name = GeneratorPresets.Custom;
                    SaveLastProfile();
                }
                SelectPreset(GeneratorPresets.Custom);
                ToolTip.SetTip(_presetBox, PresetDescription(GeneratorPresets.Custom));
                return;
            }
            if (option.Name == _profile.Name) return;

            _presetChangeInProgress = true;
            var version = ++_presetChangeVersion;
            try
            {
                if (_profile.Name == GeneratorPresets.Custom)
                {
                    var replace = await ConfirmDialog.AskAsync(
                        this,
                        T("Discard custom changes?"),
                        F("Loading {0} will replace your custom settings.", PresetName(option.Name)),
                        T("Continue"),
                        T("Cancel"));
                    if (!replace)
                    {
                        SelectPreset(_profile.Name);
                        return;
                    }
                }

                // A queued selection event from an older item list must not
                // replace the preset the user just confirmed.
                if (version != _presetChangeVersion) return;

                var language = _profile.Language;
                _profile = GeneratorPresets.Create(option.Name, _profile.Seed);
                _profile.Language = language;
                SaveLastProfile();
                RebuildTabs();
                SelectPreset(_profile.Name);
            }
            finally
            {
                _presetChangeInProgress = false;
            }
        };

        _build.Click += async (_, _) => await BuildAsync();

        Opened += (_, _) =>
        {
            RebuildTabs();
            Dispatcher.UIThread.Post(() => SelectPreset(_profile.Name),
                DispatcherPriority.Background);
            SaveLastProfile();
            // App moves the main window into place after its first render.
            // Start first-run dialogs only after that explicit reveal.
            Dispatcher.UIThread.Post(async () =>
            {
                await _revealed.Task;
                await StartFirstRunAsync();
            }, DispatcherPriority.Background);
        };
        Closing += (_, e) =>
        {
            if (_busy)
            {
                _buildCts?.Cancel();
                e.Cancel = true;
                _status.Text = T("Cancelling build; please wait.");
                return;
            }
            _app.WindowWidth = Width;
            _app.WindowHeight = Height;
            _app.GameFolder = _gameBox.Text;
            _app.ModsFolder = _modsBox.Text;
            _app.Seed = _profile.Seed;
            _app.ProfileName = _profile.Name;
            _app.Language = _profile.Language;
            _app.Save();
            SaveLastProfile();
        };
    }

    private static void ConfigurePathsForStartup(AppSettings app)
    {
        try
        {
            var gameRoot = GameLocator.NormalizeGameRoot(app.GameFolder);
            var paks = gameRoot is null
                ? EditorPaths.PaksDir
                : GameLocator.PaksDirOf(gameRoot) ?? EditorPaths.PaksDir;
            EditorPaths.Configure(AppSettings.DataRoot, paks, AppContext.BaseDirectory, app.ModsFolder);
        }
        catch
        {
            // Setup/build reports the actionable error later. This early call
            // exists so localization can read its work/ override safely.
        }
    }

    private List<PresetOption> PresetOptions() =>
        GeneratorPresets.Names
            .Select(n => new PresetOption(n, PresetName(n)))
            .ToList();

    private static int PresetIndex(string name) =>
        Math.Max(0, Array.IndexOf(GeneratorPresets.Names, name));

    private void SelectPreset(string name)
    {
        // Do not rebuild ItemsSource here. Replacing the list can queue a
        // delayed ComboBox selection reset to the first item (Expedition
        // Infinity) after suppression is released. The list only needs its
        // localized labels refreshed when the language changes.
        _suppressPreset = true;
        _presetBox.SelectedIndex = PresetIndex(name);
        _suppressPreset = false;
        ToolTip.SetTip(_presetBox, PresetDescription(name));
    }

    private void RefreshPresetItems()
    {
        _suppressPreset = true;
        _presetBox.ItemsSource = PresetOptions();
        _presetBox.SelectedIndex = PresetIndex(_profile.Name);
        _suppressPreset = false;
        ToolTip.SetTip(_presetBox, PresetDescription(_profile.Name));
    }

    private async Task StartFirstRunAsync()
    {
        if (!await EnsureLanguageAsync()) return;
        var hasGame = await EnsureGameFolderAsync();
        if (hasGame && string.IsNullOrWhiteSpace(_modsBox.Text)) UseDefaultMods();
        if (_setupExpander is not null && hasGame)
            _setupExpander.IsExpanded = false;
        if (hasGame) await MaybeOfferFirstRunExtractionAsync();
    }

    private async Task<bool> EnsureLanguageAsync()
    {
        if (_app.LanguageChosen) return true;
        var code = await LanguageDialog.ChooseAsync(this);
        _profile.Language = ModLocalization.Find(code)?.Code ?? "en";
        _app.Language = _profile.Language;
        _app.LanguageChosen = true;
        SaveLastProfile();
        _app.Save();

        // Build a fresh window from the chosen language instead of rebuilding
        // the startup tree while its first layout is still in progress.
        var replacement = new MainWindow();
        replacement.Show();
        replacement.MarkRevealed();
        Close();
        return false;
    }

    private void ApplyLanguage(string code)
    {
        if (_busy) return;
        _profile.Language = ModLocalization.Find(code)?.Code ?? "en";
        _app.LanguageChosen = true;
        ReloadUiForProfile();
        _status.Text = T("Language changed.");
    }

    private void ReloadUiForProfile()
    {
        AppLocalization.SetLanguage(_profile.Language);
        ModLocalization.SetLanguage(_profile.Language);
        _app.Language = _profile.Language;
        SaveLastProfile();
        _app.Save();
        // Drop the old visual tree first so the reusable controls can be
        // re-parented into the freshly localized tree.
        var tabIndex = _tabs.SelectedIndex;
        if (Content is Control oldRoot) DetachTree(oldRoot);
        Content = null;
        // DetachTree intentionally empties containers; restore the extraction
        // overlay so later donor work still has its progress card.
        _overlay.Child = BuildOverlayCard();
        Content = BuildRoot();
        RefreshPresetItems();
        RebuildTabs(tabIndex);
        _seedBox.Text = _profile.Seed.ToString();
        if (_headerSeedLabel is not null) _headerSeedLabel.Text = _profile.Seed.ToString();
        _build.Content = T("Build seed pack");
        ToolTip.SetTip(_build, T("Build the pack and install it into the game's ~mods folder."));
        _cancel.Content = T("Cancel");
        ToolTip.SetTip(_cancel, T("Cancel between build stages; the installed pack is not replaced."));
        _gameBox.Watermark = T("Expedition 33 game folder");
        _modsBox.Watermark = T("Install folder (created if missing)");
        _extractTitle.Text = T("Preparing game data");
        _extractDetail.Text = T("This one-time setup reads game data. Later builds skip it.");
        _stageText.Text = T("Idle.");
    }

    private Control BuildRoot()
    {
        var main = new Grid
        {
            Margin = new Thickness(18),
            RowDefinitions = RowDefinitions.Parse("Auto,*,Auto"),
        };

        var header = BuildHeader();
        Grid.SetRow(header, 0);
        main.Children.Add(header);

        Grid.SetRow(_tabs, 1);
        _tabs.VerticalAlignment = VerticalAlignment.Stretch;
        main.Children.Add(_tabs);

        var bottom = BuildBottom();
        Grid.SetRow(bottom, 2);
        main.Children.Add(bottom);

        var root = new Grid();
        root.Children.Add(main);
        root.Children.Add(_overlay);
        return root;
    }

    private Control BuildHeader()
    {
        var header = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(new TextBlock
        {
            Text = $"Expedition Infinity {AppVersion}",
            Foreground = Brush("#FFC9A961"),
            FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
            FontSize = 27,
        });
        header.Children.Add(Tip(new TextBlock
        {
            Classes = { "hint" },
            Text = T("Build a seed pack for Expedition Infinity."),
        }, T("Core movesets, pictos and weapons are always included. The seed controls loot, stats and encounters.")));

        var setup = new Expander
        {
            Header = T("Setup"),
            IsExpanded = string.IsNullOrWhiteSpace(_app.GameFolder),
            Margin = new Thickness(0, 2, 0, 2),
            Content = BuildSetupCard(),
        };
        ToolTip.SetTip(setup, T("Game folder, output folder and profile."));
        _setupExpander = setup;
        header.Children.Add(setup);
        header.Children.Add(BuildSeedRow());
        return header;
    }

    private Control BuildSetupCard()
    {
        var stack = new StackPanel { Spacing = 6 };

        var gameRow = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("170,*,Auto,Auto"),
            Margin = new Thickness(0, 2),
        };
        gameRow.Children.Add(Label(T("Game folder"), 170,
            T("The folder that contains the \"Sandfall\" folder.")));
        Grid.SetColumn(_gameBox, 1);
        ToolTip.SetTip(_gameBox, T("Usually found automatically. Browse only if needed."));
        gameRow.Children.Add(_gameBox);
        var browseGame = new Button { Content = T("Browse…"), Margin = new Thickness(6, 0, 0, 0) };
        ToolTip.SetTip(browseGame, T("Choose the game folder manually."));
        browseGame.Click += async (_, _) =>
        {
            var path = await BrowseGameAsync();
            if (path is not null) ApplyGameFolder(path);
        };
        Grid.SetColumn(browseGame, 2);
        gameRow.Children.Add(browseGame);
        var autoFind = new Button { Content = T("Find automatically"), Margin = new Thickness(6, 0, 0, 0) };
        ToolTip.SetTip(autoFind, T("Search the usual Steam folders."));
        autoFind.Click += (_, _) =>
        {
            if (!AutoFind() && _setupExpander is not null) _setupExpander.IsExpanded = true;
        };
        Grid.SetColumn(autoFind, 3);
        gameRow.Children.Add(autoFind);
        stack.Children.Add(gameRow);

        var modsRow = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("170,*,Auto,Auto"),
            Margin = new Thickness(0, 2),
        };
        modsRow.Children.Add(Label(T("Install folder"), 170,
            T("Where the finished seed pack is installed. The game reads it from its ~mods folder.")));
        Grid.SetColumn(_modsBox, 1);
        ToolTip.SetTip(_modsBox, T("Defaults to the game's ~mods folder. Created if missing."));
        modsRow.Children.Add(_modsBox);
        var browseMods = new Button { Content = T("Browse…"), Margin = new Thickness(6, 0, 0, 0) };
        ToolTip.SetTip(browseMods, T("Choose a different install folder."));
        browseMods.Click += async (_, _) => await BrowseModsAsync();
        Grid.SetColumn(browseMods, 2);
        modsRow.Children.Add(browseMods);
        var useDefault = new Button { Content = T("Use default"), Margin = new Thickness(6, 0, 0, 0) };
        ToolTip.SetTip(useDefault, T("Use the game's standard mod folder (~mods)."));
        useDefault.Click += (_, _) => UseDefaultMods();
        Grid.SetColumn(useDefault, 3);
        modsRow.Children.Add(useDefault);
        stack.Children.Add(modsRow);

        var profileRow = new WrapPanel { Orientation = Orientation.Horizontal };
        void AddProfile(Control child)
        {
            child.Margin = new Thickness(0, 0, 8, 4);
            profileRow.Children.Add(child);
        }
        AddProfile(Label(T("Profile"), 170, T("Save or load your seed settings.")));
        AddProfile(_presetBox);
        var saveProfile = new Button { Content = T("Save profile…") };
        ToolTip.SetTip(saveProfile, T("Save these settings so you can reuse them."));
        saveProfile.Click += async (_, _) => await SaveProfileAsync();
        AddProfile(saveProfile);
        var loadProfile = new Button { Content = T("Load profile…") };
        ToolTip.SetTip(loadProfile, T("Load a saved settings file."));
        loadProfile.Click += async (_, _) => await LoadProfileAsync();
        AddProfile(loadProfile);
        stack.Children.Add(profileRow);
        return Card(stack);
    }

    private Control BuildSeedRow()
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2) };
        void AddRow(Control child)
        {
            child.Margin = new Thickness(0, 0, 8, 4);
            row.Children.Add(child);
        }
        AddRow(Label(T("Seed"), 110, T("Same seed and profile give the same results.")));
        _headerSeedLabel = new TextBlock
        {
            Text = _profile.Seed.ToString(),
            Foreground = Cream,
            FontFamily = new FontFamily("DejaVu Sans Mono, Consolas, monospace"),
            FontSize = 15,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 110,
        };
        ToolTip.SetTip(_headerSeedLabel, T("Copy this seed to recreate the same pack later."));
        AddRow(_headerSeedLabel);
        var dice = new Button { Content = "🎲 " + T("Random") };
        ToolTip.SetTip(dice, T("Pick a new random seed."));
        dice.Click += (_, _) =>
        {
            if (_busy) return;
            _profile.Seed = GeneratorPresets.NewSeed();
            _profile.Randomizer.Seed = _profile.Seed;
            _seedBox.Text = _profile.Seed.ToString();
            if (_headerSeedLabel is not null) _headerSeedLabel.Text = _profile.Seed.ToString();
            if (_seedPackLabel is not null)
                _seedPackLabel.Text = F("Pack: {0}", GeneratorInfo.PackName(_profile.Seed));
        };
        AddRow(dice);
        var copy = new Button { Content = T("Copy") };
        ToolTip.SetTip(copy, T("Copy the seed."));
        copy.Click += async (_, _) =>
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null) await clipboard.SetTextAsync(_profile.Seed.ToString());
        };
        AddRow(copy);
        AddRow(_cancel);
        AddRow(_build);
        return row;
    }

    private Control BuildBottom()
    {
        var bottom = new StackPanel { Spacing = 5, Margin = new Thickness(0, 8, 0, 0) };
        bottom.Children.Add(_status);
        bottom.Children.Add(_stageText);
        bottom.Children.Add(_stageBar);

        var buttons = new WrapPanel { Orientation = Orientation.Horizontal };
        void AddButton(Button button)
        {
            button.Margin = new Thickness(0, 0, 6, 6);
            buttons.Children.Add(button);
        }
        var openMods = new Button { Content = T("Open install folder") };
        ToolTip.SetTip(openMods, T("Open the game's ~mods folder."));
        openMods.Click += (_, _) => OpenPath(_modsBox.Text);
        AddButton(openMods);
        var openExport = new Button { Content = T("Open app folder") };
        ToolTip.SetTip(openExport, T("Open the application folder that stores seeds and caches."));
        openExport.Click += (_, _) => OpenPath(EditorPaths.ExportDir);
        AddButton(openExport);
        var openLibrary = new Button { Content = T("Open seed library") };
        ToolTip.SetTip(openLibrary, T("Open the folder that keeps every generated seed."));
        openLibrary.Click += (_, _) => OpenPath(Path.Combine(AppSettings.DataRoot, "Export", "Seeds"));
        AddButton(openLibrary);
        var copyLog = new Button { Content = T("Copy log") };
        ToolTip.SetTip(copyLog, T("Copy the build log for troubleshooting."));
        copyLog.Click += async (_, _) =>
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null) await clipboard.SetTextAsync(_log.Text ?? "");
        };
        AddButton(copyLog);
        var clearLog = new Button { Content = T("Clear log") };
        ToolTip.SetTip(clearLog, T("Clear the log window."));
        clearLog.Click += (_, _) => _log.Text = "";
        AddButton(clearLog);
        bottom.Children.Add(buttons);
        bottom.Children.Add(_log);
        return bottom;
    }

    private Border BuildOverlayCard() => new()
    {
        Background = Brush("#FF211D17"),
        BorderBrush = Brush("#FF7E6A3F"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(28, 24),
        MaxWidth = 500,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Child = new StackPanel
        {
            Spacing = 14,
            Children = { _extractTitle, _extractDetail, _extractBar },
        },
    };

    private Border BuildOverlay() => new()
    {
        Background = Brush("#E0161411"),
        IsVisible = false,
        Child = BuildOverlayCard(),
    };

    private void RebuildTabs(int? keepIndex = null)
    {
        if (_tabs is null) return;
        _buildingTabs = true;
        try
        {
            var index = keepIndex ?? _tabs.SelectedIndex;
            _tabs.Items.Clear();
            _tabs.Items.Add(Tab(T("Randomizer"), BuildRandomizerTab()));
            _tabs.Items.Add(Tab(T("Balance"), BuildBalanceTab()));
            _tabs.Items.Add(Tab(T("Quality of Life"), BuildQoLTab()));
            _tabs.Items.Add(Tab(T("Language"), BuildLanguageTab()));
            if (index >= 0 && index < _tabs.Items.Count) _tabs.SelectedIndex = index;
            else _tabs.SelectedIndex = 0;
        }
        finally
        {
            _buildingTabs = false;
        }
    }

    private async Task BuildAsync()
    {
        if (_busy) return;

        var root = GameLocator.NormalizeGameRoot(_gameBox.Text);
        if (root is null)
        {
            Append("Choose the Expedition 33 game folder first (the folder that contains Sandfall).");
            _status.Text = T("Game folder not found.");
            return;
        }
        var paks = GameLocator.PaksDirOf(root);
        if (paks is null)
        {
            Append("No Paks directory under " + root);
            _status.Text = T("Game files not found in that folder.");
            return;
        }

        string mods;
        try
        {
            mods = ResolveModsFolder(paks);
        }
        catch (Exception ex)
        {
            Append("Output folder error: " + ex.Message);
            _status.Text = T("Install folder is not writable.");
            return;
        }

        if (!ValidateSettings()) return;
        if (!await EnsureDonorDataAsync(paks, mods)) return;

        SeedData.Ensure();
        _busy = true;
        SetSettingsEnabled(false);
        _buildCts = new CancellationTokenSource();
        var token = _buildCts.Token;
        _build.IsEnabled = false;
        _build.Content = T("Building…");
        _cancel.IsVisible = true;
        _cancel.IsEnabled = true;
        _stageBar.IsVisible = true;
        _stageBar.Value = 0;
        _stageText.Text = T("Starting…");
        _status.Text = T("Building. Quit the game completely before packing.");
        _log.Text = "";
        try
        {
            var logs = Path.Combine(AppSettings.DataRoot, "logs");
            Directory.CreateDirectory(logs);
            _buildLogPath = Path.Combine(logs, $"build-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(_buildLogPath, "");
        }
        catch { _buildLogPath = null; }
        _app.GameFolder = root;
        _app.ModsFolder = mods;
        _app.Seed = _profile.Seed;
        _app.ProfileName = _profile.Name;
        _app.Save();

        _profile.Randomizer.Seed = _profile.Seed;
        SaveLastProfile();
        var tweaks = _profile.Balance.ToTweaks(_profile.QoL);
        var options = new FullPackKit.Options
        {
            Seed = _profile.Seed,
            Tweaks = tweaks,
            Randomizer = _profile.Randomizer,
            SkillOverhaul = _profile.QoL.ToSkillOverhaul(),
            WeaponBulkUpgrade = _profile.QoL.ToWeaponBulkUpgrade(),
            WeaponDropLevel = _profile.Balance.ToWeaponDropLevel(),
            PartyFive = new PartyFiveKit.Settings { Enabled = true },
            Language = _profile.Language,
            PackRevisionOverride = PackRevision,
            SeedLibraryRoot = Path.Combine(AppSettings.DataRoot, "Export", "Seeds"),
            InstallToMods = true,
            VerifyPack = true,
            IncludeSpoiler = true,
            ForceRandomizerOn = false,
            CancellationToken = token,
            Log = msg => Dispatcher.UIThread.Post(() => Append(msg)),
            OnExtractProgress = (done, total, current) =>
                Dispatcher.UIThread.Post(() => UpdateExtract(done, total, current)),
            OnStage = (stage, progress, message) =>
                Dispatcher.UIThread.Post(() => UpdateStage(stage, progress, message)),
        };

        Append("Game:    " + root);
        Append("Paks:    " + paks);
        Append("Mods:    " + mods);
        Append("Library: " + options.SeedLibraryRoot);
        Append("Seed:    " + _profile.Seed);
        Append("Profile: " + _profile.Name);
        Append("Language: " + _profile.Language);

        try
        {
            EditorPaths.Configure(AppSettings.DataRoot, paks, AppContext.BaseDirectory, mods);
            Directory.CreateDirectory(EditorPaths.ModsDir);

            var result = await Task.Run(() => FullPackKit.RebuildAndPack(options), token);
            HideExtract();

            var packName = string.IsNullOrWhiteSpace(result.PackName)
                ? GeneratorInfo.PackName(result.Seed)
                : result.PackName;
            var utoc = result.PackedFiles.FirstOrDefault(p =>
                p.EndsWith(".utoc", StringComparison.OrdinalIgnoreCase));
            var spoiler = result.SpoilerPath;
            WriteManifest(packName, result, spoiler);

            _history.Add(new SeedHistoryEntry
            {
                Seed = result.Seed,
                BuiltAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"),
                ProfileName = _profile.Name,
                Language = _profile.Language,
                PackName = packName,
                PackPath = utoc ?? "",
                SpoilerPath = File.Exists(spoiler) ? spoiler : "",
                ProfileJson = JsonConvert.SerializeObject(_profile, Formatting.None),
            });
            RefreshHistory();

            _status.Text = result.InstalledFiles.Count > 0
                ? F("Packed {0} ({1} edits); library {2}; installed in {3}",
                    packName, result.Edits, result.PackDirectory, EditorPaths.ModsDir)
                : F("Packed {0} ({1} edits) into {2}",
                    packName, result.Edits, result.PackDirectory);
            Append("Done. Fully quit and relaunch the game so IoStore reloads ~mods.");
            if (_app.AutoOpenOutput)
                OpenPath(result.InstalledFiles.Count > 0 ? EditorPaths.ModsDir : result.PackDirectory);
            if (_app.AutoOpenSpoiler && File.Exists(spoiler))
                OpenPath(Path.GetDirectoryName(spoiler));
        }
        catch (OperationCanceledException)
        {
            HideExtract();
            _status.Text = T("Build cancelled. The previously installed pack was not replaced.");
            Append("Build cancelled at a stage boundary.");
        }
        catch (Exception ex)
        {
            HideExtract();
            _status.Text = T("Pack failed.");
            Append(ex.ToString());
        }
        finally
        {
            _busy = false;
            SetSettingsEnabled(true);
            _buildCts?.Dispose();
            _buildCts = null;
            _build.IsEnabled = true;
            _build.Content = T("Build seed pack");
            _cancel.IsVisible = false;
            _cancel.IsEnabled = false;
        }
    }

    private void CancelBuild()
    {
        if (_buildCts is null) return;
        _buildCts.Cancel();
        _cancel.IsEnabled = false;
        _status.Text = T("Cancelling after the current stage…");
    }

    private void UpdateStage(BuildStage stage, double progress, string message)
    {
        _stageBar.IsVisible = true;
        _stageBar.Value = Math.Clamp(progress, 0, 1);
        _stageText.Text = AppLocalization.Stage(stage);
    }

    /// <summary>
    /// The first launch warns once about the one-time extraction. The same
    /// confirmation is repeated from Build if the user clicked Cancel.
    /// </summary>
    private async Task MaybeOfferFirstRunExtractionAsync()
    {
        if (_busy) return;

        var root = GameLocator.NormalizeGameRoot(_gameBox.Text);
        var paks = root is null ? null : GameLocator.PaksDirOf(root);
        if (paks is null || string.IsNullOrWhiteSpace(_modsBox.Text))
            return; // no game yet; Build will ask after Setup is filled in

        string mods;
        try
        {
            mods = ResolveModsFolder(paks);
        }
        catch (Exception ex)
        {
            _status.Text = T("Install folder is not writable.");
            Append(ex.Message);
            return;
        }

        try
        {
            EditorPaths.Configure(AppSettings.DataRoot, paks, AppContext.BaseDirectory, mods);
        }
        catch (Exception ex)
        {
            _status.Text = T("The application folder is not writable.");
            Append("Cannot create the extraction folders under " + AppSettings.DataRoot);
            Append(ex.Message);
            return;
        }

        var (have, total) = DonorExtract.CacheStatus();
        if (total == 0 || have >= total)
        {
            _app.ExtractionWarningShown = true;
            _app.Save();
            return;
        }

        _app.ExtractionWarningShown = true;
        _app.Save();

        var target = Path.Combine(AppSettings.DataRoot, "work", "tables");
        if (!await FirstRunDialog.ConfirmExtractionAsync(this, have, total, target))
        {
            _status.Text = T("Game data extraction skipped. The first build will ask again.");
            Append("Extraction cancelled. No game data was copied.");
            return;
        }

        await RunDonorExtractionAsync();
    }

    /// <summary>
    /// Prompts for and performs the donor extraction before a build. Returns
    /// false when the user cancels or the extraction fails; no pack is touched.
    /// </summary>
    private async Task<bool> EnsureDonorDataAsync(string paks, string mods)
    {
        try
        {
            EditorPaths.Configure(AppSettings.DataRoot, paks, AppContext.BaseDirectory, mods);
        }
        catch (Exception ex)
        {
            _status.Text = T("The application folder is not writable.");
            Append("Cannot create build folders under " + AppSettings.DataRoot);
            Append(ex.Message);
            return false;
        }

        var (have, total) = DonorExtract.CacheStatus();
        if (total == 0 || have >= total) return true;

        var target = Path.Combine(AppSettings.DataRoot, "work", "tables");
        if (!await FirstRunDialog.ConfirmExtractionAsync(this, have, total, target))
        {
            _status.Text = T("Build cancelled before extraction.");
            Append("Game data extraction cancelled; no seed was built.");
            return false;
        }

        return await RunDonorExtractionAsync();
    }

    private async Task<bool> RunDonorExtractionAsync()
    {
        if (_busy) return false;

        _busy = true;
        SetSettingsEnabled(false);
        _build.IsEnabled = false;
        _build.Content = T("Extracting…");
        _stageBar.IsVisible = true;
        _stageBar.Value = 0;
        _stageText.Text = T("Preparing game data");
        _extractTitle.Text = T("Preparing game data");
        _extractDetail.Text = T("Checking which game files are needed…");
        _overlay.IsVisible = true;
        _status.Text = T("Extracting game data. This can take several minutes; keep the app open.");
        Append("Preparing game data in " + Path.Combine(AppSettings.DataRoot, "work", "tables"));

        try
        {
            var extracted = await Task.Run(() => DonorExtract.EnsureAll(
                msg => Dispatcher.UIThread.Post(() => Append(msg)),
                (done, total, current) => Dispatcher.UIThread.Post(
                    () => UpdateExtract(done, total, current))));

            _stageBar.Value = 1;
            _stageText.Text = T("Game data ready");
            _status.Text = extracted > 0
                ? F("Game data ready — {0} extraction task(s) completed.", extracted)
                : T("Game data ready.");
            _app.ExtractionWarningShown = true;
            _app.Save();
            Append(_status.Text);
            return true;
        }
        catch (Exception ex)
        {
            _stageText.Text = T("Game data extraction failed.");
            _status.Text = T("Game data extraction failed.");
            // Let the next launch offer the warning again instead of silently
            // skipping it after a failed first attempt.
            _app.ExtractionWarningShown = false;
            _app.Save();
            Append("Extraction failed. Check that the application folder is writable and the " +
                   "game is installed and fully downloaded.");
            Append(ex.ToString());
            return false;
        }
        finally
        {
            HideExtract();
            _busy = false;
            SetSettingsEnabled(true);
            _build.IsEnabled = true;
            _build.Content = T("Build seed pack");
        }
    }

    private void SetSettingsEnabled(bool enabled)
    {
        _tabs.IsEnabled = enabled;
        _presetBox.IsEnabled = enabled;
        _gameBox.IsEnabled = enabled;
        _modsBox.IsEnabled = enabled;
    }

    private bool ValidateSettings()
    {
        var r = _profile.Randomizer;
        r.StatLo = NormalizeArray(r.StatLo, RandomizerKit.DefaultStatLo);
        r.StatHi = NormalizeArray(r.StatHi, RandomizerKit.DefaultStatHi);
        r.BossHp = NormalizeArray(r.BossHp, RandomizerKit.DefaultBossHp);
        r.AttackLo = NormalizeArray(r.AttackLo, RandomizerKit.DefaultAttackLo);
        r.AttackHi = NormalizeArray(r.AttackHi, RandomizerKit.DefaultAttackHi);
        for (int i = 0; i < 5; i++)
        {
            if (r.StatLo[i] > r.StatHi[i])
            {
                Append($"Randomizer: T{i + 1} stat min is greater than max.");
                _status.Text = T("Settings validation failed.");
                return false;
            }
            if (r.AttackLo[i] > r.AttackHi[i])
            {
                Append($"Randomizer: T{i + 1} attack min is greater than max.");
                _status.Text = T("Settings validation failed.");
                return false;
            }
        }
        if (r.RegularPictosChance is < 0 or > 100 || r.ElitePictosChance is < 0 or > 100 ||
            r.T5PerfectCatalystChance is < 0 or > 100)
        {
            Append("Randomizer: chance values must be between 0 and 100.");
            _status.Text = T("Settings validation failed.");
            return false;
        }
        if (_profile.Balance.LevelCap is < 99 or > 999 || _profile.Balance.AttributeCap is < 99 or > 500)
        {
            Append("Balance: level cap / attribute cap out of range.");
            _status.Text = T("Settings validation failed.");
            return false;
        }
        if (_profile.Balance.MaxDropLevel is < 33 or > 333)
        {
            Append("Balance: max weapon drop level must be 33–333.");
            _status.Text = T("Settings validation failed.");
            return false;
        }
        return true;
    }

    private void WriteManifest(string packName, FullPackKit.Result result, string spoiler)
    {
        try
        {
            var dir = string.IsNullOrWhiteSpace(result.PackDirectory)
                ? EditorPaths.ExportDir
                : result.PackDirectory;
            Directory.CreateDirectory(dir);
            var profileJson = JsonConvert.SerializeObject(_profile, Formatting.Indented);
            var profileSha = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(profileJson))).ToLowerInvariant();
            var manifest = new JObject
            {
                ["format"] = 1,
                ["app"] = "Expedition Infinity",
                ["appVersion"] = AppVersion,
                ["seed"] = result.Seed,
                ["builtAt"] = DateTime.UtcNow.ToString("o"),
                ["language"] = _profile.Language,
                ["packName"] = packName,
                ["packFiles"] = new JArray(result.PackedFiles.Select(Path.GetFileName)),
                ["installedFiles"] = new JArray(result.InstalledFiles.Select(Path.GetFileName)),
                ["spoiler"] = File.Exists(spoiler) ? Path.GetFileName(spoiler) : "",
                ["profileName"] = _profile.Name,
                ["profileFile"] = packName + ".eiseed.json",
                ["profileSha256"] = profileSha,
                ["gameFingerprint"] = GameFingerprint(),
                ["profile"] = JObject.FromObject(_profile),
            };
            var path = Path.Combine(dir, packName + "_manifest.json");
            WriteAtomic(Path.Combine(dir, packName + ".eiseed.json"), profileJson);
            WriteAtomic(path, manifest.ToString(Formatting.Indented));
            Append("Manifest: " + path);
        }
        catch (Exception ex)
        {
            Append("Manifest write failed: " + ex.Message);
        }
    }

    private static void WriteAtomic(string path, string text)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, text);
        File.Move(temp, path, overwrite: true);
    }

    private static string GameFingerprint()
    {
        try
        {
            if (!Directory.Exists(EditorPaths.PaksDir)) return "";
            var entries = Directory.EnumerateFiles(EditorPaths.PaksDir, "*.utoc",
                    SearchOption.TopDirectoryOnly)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .Select(p => $"{Path.GetFileName(p)}:{(new FileInfo(p)).Length}:{File.GetLastWriteTimeUtc(p).Ticks}");
            return Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(string.Join("\n", entries)))).ToLowerInvariant();
        }
        catch
        {
            return "";
        }
    }

    private string ResolveModsFolder(string paks)
    {
        var raw = _modsBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(raw)) raw = Path.Combine(paks, "~mods");
        var full = Path.GetFullPath(raw);
        if (string.Equals(full.TrimEnd(Path.DirectorySeparatorChar),
                paks.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            full = Path.Combine(paks, "~mods");
        Directory.CreateDirectory(full);
        _modsBox.Text = full;
        return full;
    }

    private async Task<bool> EnsureGameFolderAsync()
    {
        if (GameLocator.NormalizeGameRoot(_gameBox.Text) is { } existing)
        {
            ApplyGameFolder(existing);
            return true;
        }

        var found = GameLocator.FindInstall();
        if (found is not null)
        {
            Append("Found " + found);
            _status.Text = T("Expedition 33 found. Confirm the folder.");
        }
        else
        {
            Append("Could not find Expedition 33 automatically.");
            _status.Text = T("Game folder not found.");
        }

        string? problem = null;
        while (true)
        {
            if (found is not null && problem is null)
            {
                var choice = await GameFolderDialog.ConfirmFoundAsync(this, found);
                if (choice == GameFolderChoice.Quit)
                {
                    Close();
                    return false;
                }
                if (choice == GameFolderChoice.UseFound)
                {
                    ApplyGameFolder(found);
                    return true;
                }
            }
            else if (!await GameFolderDialog.PromptAsync(this, problem))
            {
                Close();
                return false;
            }

            var path = await BrowseGameAsync();
            if (string.IsNullOrWhiteSpace(path)) continue;

            if (ApplyGameFolder(path)) return true;
            problem = T("That folder does not look like Expedition 33. Choose the folder that contains the \"Sandfall\" folder.");
            found = null;
        }
    }

    private bool AutoFind()
    {
        if (_busy) return false;
        var found = GameLocator.FindInstall();
        if (found is null)
        {
            Append("Could not find Expedition 33 in the usual Steam folders.");
            _status.Text = T("Game folder not found.");
            return false;
        }
        if (!ApplyGameFolder(found)) return false;
        Append("Found " + found);
        return true;
    }

    private bool ApplyGameFolder(string path)
    {
        if (_busy) return false;
        var root = GameLocator.NormalizeGameRoot(path);
        if (root is null)
        {
            Append("That folder does not look like Expedition 33 (missing Sandfall/Content/Paks).");
            _status.Text = T("That folder does not look like Expedition 33.");
            return false;
        }
        _gameBox.Text = root;
        var paks = GameLocator.PaksDirOf(root);
        if (paks is not null && string.IsNullOrWhiteSpace(_modsBox.Text))
            _modsBox.Text = Path.Combine(paks, "~mods");
        _app.GameFolder = root;
        _app.ModsFolder = _modsBox.Text;
        _app.Save();
        _status.Text = T("Game folder ready.");
        return true;
    }

    private void UseDefaultMods()
    {
        if (_busy) return;
        var root = GameLocator.NormalizeGameRoot(_gameBox.Text);
        var paks = root is null ? null : GameLocator.PaksDirOf(root);
        if (paks is null)
        {
            Append("Set a valid game folder first.");
            return;
        }
        _modsBox.Text = Path.Combine(paks, "~mods");
        _app.ModsFolder = _modsBox.Text;
        _app.Save();
    }

    private async Task<string?> BrowseGameAsync()
    {
        if (_busy) return null;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = T("Choose the Expedition 33 game folder"),
            AllowMultiple = false,
        });
        return folders.Count == 0 ? null : folders[0].Path.LocalPath;
    }

    private async Task BrowseModsAsync()
    {
        if (_busy) return;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = T("Choose the install folder"),
            AllowMultiple = false,
        });
        if (folders.Count == 0) return;
        _modsBox.Text = folders[0].Path.LocalPath;
    }

    private async Task SaveProfileAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = T("Save Expedition Infinity profile"),
            SuggestedFileName = $"ExpeditionInfinity_{_profile.Seed}.eiseed.json",
            DefaultExtension = "json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType(T("Expedition Infinity profile"))
                {
                    Patterns = new[] { "*.eiseed.json", "*.json" },
                },
            },
        });
        if (file is null) return;
        try
        {
            File.WriteAllText(file.Path.LocalPath,
                JsonConvert.SerializeObject(_profile, Formatting.Indented));
            _status.Text = T("Profile saved.");
        }
        catch (Exception ex)
        {
            Append("Profile save failed: " + ex.Message);
        }
    }

    private async Task LoadProfileAsync()
    {
        if (_busy) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = T("Load Expedition Infinity profile"),
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(T("Expedition Infinity profile"))
                {
                    Patterns = new[] { "*.eiseed.json", "*.json" },
                },
            },
        });
        if (files.Count == 0) return;
        try
        {
            var loaded = JsonConvert.DeserializeObject<GeneratorProfile>(
                File.ReadAllText(files[0].Path.LocalPath));
            if (loaded is null) throw new InvalidDataException("profile is empty");
            _profile = NormalizeProfile(loaded);
            ReloadUiForProfile();
            _status.Text = T("Profile loaded.");
        }
        catch (Exception ex)
        {
            Append("Profile load failed: " + ex.Message);
        }
    }

    private void RefreshHistory()
    {
        if (_historyBox is null) return;
        var labels = _history.Entries
            .Select(e =>
                $"{e.Seed} · {PresetName(e.ProfileName)} · " +
                $"{ModLocalization.Find(e.Language)?.DisplayName ?? e.Language} · {e.BuiltAt}")
            .ToList();
        _historyBox.ItemsSource = labels;
        if (labels.Count > 0) _historyBox.SelectedIndex = 0;
    }

    private void RestoreHistory()
    {
        if (_busy) return;
        if (_historyBox is null || _historyBox.SelectedIndex < 0 ||
            _historyBox.SelectedIndex >= _history.Entries.Count) return;
        var entry = _history.Entries[_historyBox.SelectedIndex];
        if (!string.IsNullOrWhiteSpace(entry.ProfileJson))
        {
            try
            {
                var loaded = JsonConvert.DeserializeObject<GeneratorProfile>(entry.ProfileJson);
                if (loaded is not null) _profile = NormalizeProfile(loaded);
            }
            catch { /* fall back to seed only */ }
        }
        _profile.Seed = entry.Seed;
        _profile.Randomizer.Seed = entry.Seed;
        ReloadUiForProfile();
        _status.Text = F("Restored seed {0}.", entry.Seed);
    }

    private void MarkCustom()
    {
        if (_suppressPreset || _buildingTabs) return;
        if (_profile.Name == GeneratorPresets.Custom) return;
        _profile.Name = GeneratorPresets.Custom;
        SelectPreset(GeneratorPresets.Custom);
    }

    private static GeneratorProfile LoadLastProfile()
    {
        try
        {
            var path = Path.Combine(AppSettings.ProfilesDir, "last.json");
            if (File.Exists(path))
            {
                var loaded = JsonConvert.DeserializeObject<GeneratorProfile>(File.ReadAllText(path));
                if (loaded is not null) return loaded;
            }
        }
        catch { /* first run */ }
        var app = AppSettings.Load();
        var profile = GeneratorPresets.Create(app.ProfileName, app.Seed);
        profile.Language = app.Language;
        return profile;
    }

    private void SaveLastProfile()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.ProfilesDir);
            File.WriteAllText(Path.Combine(AppSettings.ProfilesDir, "last.json"),
                JsonConvert.SerializeObject(_profile, Formatting.Indented));
        }
        catch { /* best effort */ }
    }

    private static GeneratorProfile NormalizeProfile(GeneratorProfile p)
    {
        p.Randomizer ??= new RandomizerKit.Settings();
        p.Balance ??= new BalanceOptions();
        p.QoL ??= new QoLOptions();
        var legacyFormat = p.Format < GeneratorProfile.CurrentFormat;
        p.Seed = Math.Clamp(p.Seed, 0, 99_999_999);
        p.Name = GeneratorPresets.Normalize(p.Name);
        // Named presets are canonical. Older builds left some presets with
        // values that did not match their names; rebuild them on load. User
        // edits always mark the profile Custom, so custom values are kept.
        if (legacyFormat && p.Name != GeneratorPresets.Custom)
        {
            var canonical = GeneratorPresets.Create(p.Name, p.Seed);
            canonical.Language = p.Language;
            p = canonical;
        }
        p.Format = GeneratorProfile.CurrentFormat;
        p.Randomizer.Seed = p.Seed;
        // Repair profiles saved by older builds that defaulted the standard
        // preset to wildcard stains. A user change marks the profile Custom.
        if (p.Name == GeneratorPresets.Expedition) p.QoL.LightStainWildcard = false;

        p.Randomizer.StatLo = NormalizeArray(p.Randomizer.StatLo, RandomizerKit.DefaultStatLo, 0, 10);
        p.Randomizer.StatHi = NormalizeArray(p.Randomizer.StatHi, RandomizerKit.DefaultStatHi, 0, 10);
        p.Randomizer.BossHp = NormalizeArray(p.Randomizer.BossHp, RandomizerKit.DefaultBossHp, 0, 1000);
        p.Randomizer.AttackLo = NormalizeArray(p.Randomizer.AttackLo, RandomizerKit.DefaultAttackLo, 0, 10);
        p.Randomizer.AttackHi = NormalizeArray(p.Randomizer.AttackHi, RandomizerKit.DefaultAttackHi, 0, 10);
        p.Randomizer.BossSpeed = Math.Clamp(p.Randomizer.BossSpeed, 0, 10);
        p.Randomizer.RegularPictosChance = Math.Clamp(p.Randomizer.RegularPictosChance, 0, 100);
        p.Randomizer.ElitePictosChance = Math.Clamp(p.Randomizer.ElitePictosChance, 0, 100);
        p.Randomizer.T5PerfectCatalystChance = Math.Clamp(p.Randomizer.T5PerfectCatalystChance, 0, 100);
        p.Randomizer.PetankLumina = Math.Clamp(p.Randomizer.PetankLumina, 0, 9999);
        p.Randomizer.MerchantGearSlots = Math.Clamp(p.Randomizer.MerchantGearSlots, 0, 20);
        p.Randomizer.TintsPerType = Math.Clamp(p.Randomizer.TintsPerType, 0, 99);
        p.Randomizer.ChromaElixirShards = Math.Clamp(p.Randomizer.ChromaElixirShards, 0, 99);
        p.Randomizer.ShapesPerType = Math.Clamp(p.Randomizer.ShapesPerType, 0, 20);
        p.Randomizer.MaxEnemiesPerBattle = Math.Clamp(p.Randomizer.MaxEnemiesPerBattle, 1, BattleGroupKit.FieldCapacity);
        p.Randomizer.BattleVariants = Math.Clamp(p.Randomizer.BattleVariants, 1, 8);
        p.Randomizer.ExtraEnemiesChance = Math.Clamp(p.Randomizer.ExtraEnemiesChance, 0, 100);
        p.Randomizer.BattleGroupsAreaPrefix = (p.Randomizer.BattleGroupsAreaPrefix ?? "").Trim();
        // These encounter experiments are not finished and are not exposed.
        p.Randomizer.BattleGroups = false;
        p.Randomizer.RandomBattles = false;

        p.Balance.RegularEnemyHpBonus = NormalizeArray(
            p.Balance.RegularEnemyHpBonus, RegularEnemyBuffKit.DefaultHpBonus, -0.95, 30);
        p.Balance.RegularEnemySpeedBonus = NormalizeArray(
            p.Balance.RegularEnemySpeedBonus, RegularEnemyBuffKit.DefaultSpeedBonus, -0.95, 30);
        p.Balance.RegularEnemyAttackBonus = NormalizeArray(
            p.Balance.RegularEnemyAttackBonus, RegularEnemyBuffKit.DefaultAttackBonus, -0.95, 30);
        p.Balance.LevelCap = Math.Clamp(p.Balance.LevelCap, 99, 999);
        p.Balance.AttributeCap = Math.Clamp(p.Balance.AttributeCap, 99, 500);
        p.Balance.ExtraXpPerLevel = Math.Clamp(p.Balance.ExtraXpPerLevel, 1, 99_999_999);
        p.Balance.AttributePointsPerLevel = Math.Clamp(p.Balance.AttributePointsPerLevel, 0, 20);
        p.Balance.SkillPointsPerLevel = Math.Clamp(p.Balance.SkillPointsPerLevel, 1, 10);
        p.Balance.LuminaPerLevel = Math.Clamp(p.Balance.LuminaPerLevel, 1, 10);
        p.Balance.LuminaDropMultiplier = Math.Clamp(p.Balance.LuminaDropMultiplier, 0.1, 5.0);
        p.Balance.MaxDropLevel = Math.Clamp(p.Balance.MaxDropLevel, 33, 333);
        p.Balance.DodgeWindow = Math.Clamp(p.Balance.DodgeWindow, 0.01, 1.0);
        p.Balance.ParryWindow = Math.Clamp(p.Balance.ParryWindow, 0.01, 1.0);
        p.Balance.HpMult = Math.Clamp(p.Balance.HpMult, 0.1, 10);
        p.Balance.AttackMult = Math.Clamp(p.Balance.AttackMult, 0.1, 10);
        p.Balance.SpeedMult = Math.Clamp(p.Balance.SpeedMult, 0.1, 10);
        p.Balance.CritMult = Math.Clamp(p.Balance.CritMult, 0.1, 10);
        p.Balance.DefenseMult = Math.Clamp(p.Balance.DefenseMult, 0.1, 10);
        p.Balance.Permadeath = false;

        // The generator exposes a single on/off mastery choice. The standard
        // Expedition Infinity profile leaves it off; toggling marks Custom.
        p.QoL.PictoMastery =
            string.Equals(p.QoL.PictoMastery, "instant", StringComparison.OrdinalIgnoreCase)
                ? "instant"
                : "none";
        if (p.Name == GeneratorPresets.Expedition) p.QoL.PictoMastery = "none";
        p.QoL.PictoPopupCap = Math.Clamp(p.QoL.PictoPopupCap,
            PictoPopupCapKit.VanillaCap, PictoPopupCapKit.MaxCap);
        p.QoL.BulkUpgradeLevels = Math.Clamp(p.QoL.BulkUpgradeLevels, 1,
            WeaponBulkUpgradeKit.BulkLevels);
        p.QoL.SkipTutorials = true;
        p.QoL.LightStainWildcard = false;
        p.Language = ModLocalization.Find(p.Language)?.Code ?? "en";
        return p;
    }

    private static double[] NormalizeArray(double[]? values, double[] fallback,
        double min = double.MinValue, double max = double.MaxValue)
    {
        if (values is null || values.Length != 5) return (double[])fallback.Clone();
        var copy = (double[])values.Clone();
        for (int i = 0; i < copy.Length; i++)
        {
            if (double.IsNaN(copy[i]) || double.IsInfinity(copy[i]))
                copy[i] = fallback[i];
            copy[i] = Math.Clamp(copy[i], min, max);
        }
        return copy;
    }

    private void Append(string line)
    {
        if (_log.Text?.Length > 0) _log.Text += Environment.NewLine;
        _log.Text += line;
        _log.CaretIndex = _log.Text.Length;
        if (_buildLogPath is not null)
        {
            try { File.AppendAllText(_buildLogPath, line + Environment.NewLine); }
            catch { /* best effort */ }
        }
    }

    private void UpdateExtract(int done, int total, string current)
    {
        if (total <= 0 || done >= total)
        {
            // The completion callback is posted from the worker thread. Hide
            // here so a late progress post cannot leave the overlay stuck on
            // "Game data ready" after the build has moved on.
            HideExtract();
            return;
        }
        _overlay.IsVisible = true;
        _extractBar.Maximum = total;
        _extractBar.Value = Math.Clamp(done, 0, total);
        var pct = (int)Math.Round(100.0 * done / total);
        var what = string.IsNullOrWhiteSpace(current)
            ? T("game data")
            : T(DonorExtract.DescribeFilter(current));
        _extractTitle.Text = T("Preparing game data");
        _extractDetail.Text = F("Extracting {0}…", what) + "\n" +
                              F("{0} of {1} tasks complete ({2}%)", done, total, pct) + "\n\n" +
                              T("This is a one-time setup. Keep this window open.");
    }

    private void HideExtract() => _overlay.IsVisible = false;

    private static void OpenPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var target = File.Exists(path) || Directory.Exists(path)
                ? path
                : Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(target)) return;
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
        }
        catch { /* best effort */ }
    }

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}

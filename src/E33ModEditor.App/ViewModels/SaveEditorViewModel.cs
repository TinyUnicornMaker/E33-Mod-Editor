using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Input;
using E33ModEditor.App;
using E33ModEditor.Core;
using E33ModEditor.Core.Loot;
using E33ModEditor.Core.SaveGame;

namespace E33ModEditor.App.ViewModels;

/// <summary>A pictos row shown in the save editor's pictos grid.</summary>
public class SavePictosRow : ViewModelBase
{
    public required string Id { get; init; }
    public string DisplayName { get; init; } = "";
    /// <summary>"", "created-painter" or "created-affinity".</summary>
    public string Group { get; init; } = "";
    public bool IsCreated => Group.Length > 0;

    private bool _found;
    public bool Found { get => _found; set => Set(ref _found, value); }

    private bool _mastered;
    public bool Mastered { get => _mastered; set => Set(ref _mastered, value); }

    private int _level = 1;
    public int Level { get => _level; set => Set(ref _level, value); }

    /// <summary>Whether the packed mod currently contains this created pictos.</summary>
    public bool InPack { get; init; } = true;

    /// <summary>
    /// True when the id no longer exists in the current tables — retired mod
    /// content (e.g. Combo Attack IV-VII) or vanilla cut content. Rows are kept
    /// visible only when the save actually owns them, so they can be scrubbed.
    /// </summary>
    public bool NotSupported { get; init; }

    public string DisplayLabel => IsCreated ? $"{DisplayName} ✨" : DisplayName;
    public string Description { get; init; } = "";
}

/// <summary>A .sav discovered on disk, with summary fields for the picker.</summary>
public class SaveFileEntry : ViewModelBase
{
    public required string Path { get; init; }
    public string Name => System.IO.Path.GetFileName(Path);

    /// <summary>SteamID3 folder (or the save directory name when that is missing).</summary>
    public string AccountId
    {
        get
        {
            foreach (var part in Path.Split(System.IO.Path.DirectorySeparatorChar,
                         System.IO.Path.AltDirectorySeparatorChar))
                if (part.Length >= 16 && part.All(char.IsDigit)) return part;
            var dir = System.IO.Path.GetDirectoryName(Path) ?? "";
            if (System.IO.Path.GetFileName(dir).Equals("Backup", StringComparison.OrdinalIgnoreCase))
                dir = System.IO.Path.GetDirectoryName(dir) ?? dir;
            return System.IO.Path.GetFileName(dir) ?? "";
        }
    }

    public string AccountShort =>
        AccountId.Length > 6 ? "…" + AccountId[^6..] : AccountId;

    /// <summary>Title-screen slot, Backup/ autosave, or a renamed copy.</summary>
    public string Kind { get; init; } = "Manual";
    public DateTime ModifiedUtc { get; set; }

    /// <summary>Slot 1 / Slot 2 for EXPEDITION_N; Auto / Copy otherwise.</summary>
    public string SlotTitle
    {
        get
        {
            var m = System.Text.RegularExpressions.Regex.Match(Name, @"^EXPEDITION_(\d+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return Kind;
            var n = int.Parse(m.Groups[1].Value) + 1;
            return Kind switch
            {
                "Auto" => $"Auto slot {n}",
                "Copy" => $"Copy of slot {n}",
                _ => $"Slot {n}",
            };
        }
    }

    public string ListLabel => $"{SlotTitle}  ·  {Name}  ·  {AccountShort}";

    /// <summary>Sort key: most recent first.</summary>
    public long ModifiedTicks => ModifiedUtc.Ticks;

    private string _modified = "";
    public string Modified { get => _modified; set => Set(ref _modified, value); }

    private string _storyPoint = "…";
    public string StoryPoint { get => _storyPoint; set => Set(ref _storyPoint, value); }

    private string _location = "";
    public string Location
    {
        get => _location;
        set { if (Set(ref _location, value)) Raise(nameof(LocationSummary)); }
    }

    private int _partyLevel;
    public int PartyLevel
    {
        get => _partyLevel;
        set { if (Set(ref _partyLevel, value)) Raise(nameof(LocationSummary)); }
    }

    private string _timePlayed = "";
    public string TimePlayed
    {
        get => _timePlayed;
        set { if (Set(ref _timePlayed, value)) Raise(nameof(LocationSummary)); }
    }

    /// <summary>Compact "Lv N · playtime · location" line for the picker cards.</summary>
    public string LocationSummary =>
        string.Join("  ·  ", new[] { PartyLevel > 0 ? $"Lv {PartyLevel}" : "", TimePlayed, Location }
            .Where(b => b.Length > 0));

    private bool _isContinue;
    public bool IsContinue
    {
        get => _isContinue;
        set { if (Set(ref _isContinue, value)) Raise(nameof(Badges)); }
    }

    private bool _isOpen;
    public bool IsOpen
    {
        get => _isOpen;
        set { if (Set(ref _isOpen, value)) Raise(nameof(Badges)); }
    }

    public string Badges =>
        string.Join("  ", new[] { IsOpen ? "OPEN" : "", IsContinue ? "Continue" : "" }
            .Where(b => b.Length > 0));
}


/// <summary>One editable character row (list entry + full detail state).</summary>
public class CharacterRow : ViewModelBase
{
    public required string Id { get; init; }
    public string DisplayName => GameLabels.DisplayAlias(Id);
    /// <summary>False when this roster member has no CharactersCollection entry yet;
    /// flips true as soon as a fresh record is created for them.</summary>
    public bool InSave { get; set; } = true;
    private int _level;
    public int Level { get => _level; set => Set(ref _level, value); }
    private int _currentXp;
    public int CurrentXp { get => _currentXp; set => Set(ref _currentXp, value); }
    private int _actionPoints;
    public int ActionPoints { get => _actionPoints; set => Set(ref _actionPoints, value); }
    private int _lumina;
    public int Lumina { get => _lumina; set => Set(ref _lumina, value); }
    private int _hp;
    public int Hp { get => _hp; set => Set(ref _hp, value); }
    private int _mp;
    public int Mp { get => _mp; set => Set(ref _mp, value); }
    private bool _excluded;
    public bool Excluded { get => _excluded; set => Set(ref _excluded, value); }
    private bool _inParty;
    public bool InParty { get => _inParty; set { if (Set(ref _inParty, value)) Raise(nameof(Badges)); } }
    public string Formation { get; set; } = "";
    public string Skin { get; set; } = "";
    public string Face { get; set; } = "";
    public string Weapon { get; set; } = "";
    /// <summary>The three worn pictos (EquippedItemsPerSlot, ItemType Pictos, slots 0–2).</summary>
    public string[] PictosSlots { get; set; } = new[] { "", "", "" };
    public int Vitality { get; set; }
    public int Might { get; set; }
    public int Agility { get; set; }
    public int Defense { get; set; }
    public int Luck { get; set; }
    public List<string> Skills { get; set; } = new();
    public List<string> EquippedSkills { get; set; } = new();
    /// <summary>Lumina loadout (EquippedPassiveEffects) — not the worn pictos.</summary>
    public List<string> EquippedLuminas { get; set; } = new();

    /// <summary>Short badges shown next to the name in the master list.</summary>
    public string Badges =>
        string.Join("  ", new[] { InParty ? "◉ party" : "", Excluded ? "⊘ hidden" : "" }
            .Where(b => b.Length > 0));
}

/// <summary>A name→bool map row (enemies fought, doors opened…).</summary>
public class FlagRow : ViewModelBase
{
    public required string MapName { get; init; }
    public required string Key { get; init; }
    private bool _value;
    public bool Value { get => _value; set => Set(ref _value, value); }
}

/// <summary>An item row driven purely by the inventory map (weapons, music, journals…).</summary>
public class ItemRow : ViewModelBase
{
    public required string Id { get; init; }
    public string DisplayName { get; init; } = "";
    /// <summary>Owner character for weapons (Gustave, Lune, …); empty for other items.</summary>
    public string Character { get; init; } = "";
    private bool _owned;
    public bool Owned { get => _owned; set => Set(ref _owned, value); }
    private int _level = 1;
    public int Level { get => _level; set => Set(ref _level, value); }
    /// <summary>Whether the shared progression array tracks this item's level.</summary>
    public bool HasLevel { get; init; }
}

/// <summary>View model behind the Save Editor module.</summary>
public class SaveEditorViewModel : ViewModelBase
{
    /// <summary>Vanilla pictos catalog: id → friendly name (filled by MainViewModel).</summary>
    public Func<IEnumerable<(string Id, string DisplayName)>>? VanillaPictosCatalog { get; set; }
    /// <summary>In-game pictos / lumina description for hover tips.</summary>
    public Func<string, string>? PictosDescription { get; set; }

    /// <summary>Whether each created-pictos family is enabled in the packed mod.</summary>
    public Func<bool>? PainterConversionEnabled { get; set; }
    public Func<bool>? StatusAffinityEnabled { get; set; }
    public Func<bool>? DamageLimitEnabled { get; set; }

    /// <summary>Current level cap from the Gameplay Tweaks settings (99 vanilla / 999 expanded).</summary>
    public Func<int>? LevelCapProvider { get; set; }
    public int LevelCap => LevelCapProvider?.Invoke() ?? GameplayTweaksKit.VanillaLevelCap;

    /// <summary>Weapon upgrade cap: vanilla 33, or the over-level/drop-level
    /// setting when those kits are enabled.</summary>
    public Func<int>? WeaponLevelCapProvider { get; set; }
    public int WeaponLevelCap => WeaponLevelCapProvider?.Invoke() ?? WeaponOverLevelKit.VanillaMaxLevel;

    /// <summary>Pick screen options: only the saves the game's Continue loads.</summary>
    public ObservableCollection<SaveFileEntry> ContinueSaves { get; } = new();
    public ObservableCollection<SavePictosRow> PictosRows { get; } = new();
    public ObservableCollection<CharacterRow> Characters { get; } = new();
    public ObservableCollection<ItemRow> Weapons { get; } = new();
    public ObservableCollection<ItemRow> MusicDisks { get; } = new();
    public ObservableCollection<ItemRow> Journals { get; } = new();
    public ObservableCollection<FlagRow> WorldFlags { get; } = new();
    /// <summary>Catalogs filled by MainViewModel after preload (id → friendly name).</summary>
    public Dictionary<string, string> WeaponCatalog { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Weapon id → owner character name (filled with WeaponCatalog).</summary>

    public Dictionary<string, string> WeaponOwners { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> MusicCatalog { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> JournalCatalog { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Playable roster always listed on the Characters tab, in party or not.</summary>
    public static readonly string[] CharacterRoster =
        { "Gustave", "Lune", "Maelle", "Sciel", "Verso", "Monoco" };

    private SaveFileEntry? _selectedSave;
    public SaveFileEntry? SelectedSave { get => _selectedSave; set => Set(ref _selectedSave, value); }

    private bool _hasOpenSave;
    public bool HasOpenSave
    {
        get => _hasOpenSave;
        set
        {
            if (!Set(ref _hasOpenSave, value)) return;
            Raise(nameof(OpenSaveTitle));
            RaiseCommandCanExecuteChanged();
        }
    }

    public string OpenSaveTitle
    {
        get
        {
            if (!HasOpenSave || _openPath is null)
                return "No save open.";
            var entry = _allSaves.FirstOrDefault(s =>
                string.Equals(s.Path, _openPath, StringComparison.OrdinalIgnoreCase));
            var slot = entry?.SlotTitle ?? System.IO.Path.GetFileName(_openPath);
            var file = System.IO.Path.GetFileName(_openPath);
            var cont = entry?.IsContinue == true
                ? "This is the game's Continue slot — use Continue (or this slot) on the title screen."
                : "This is not the Continue slot. The title screen Continue loads the row marked Continue.";
            return $"Editing {slot}  ({file})\n{cont}";
        }
    }

    private string _status = "Pick a save file to edit.";
    public string Status { get => _status; set => Set(ref _status, value); }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (!Set(ref _isBusy, value)) return;
            Raise(nameof(CanAct));
            RaiseCommandCanExecuteChanged();
        }
    }

    public bool CanAct => !IsBusy;

    /// <summary>Every command's CanExecute depends on IsBusy/HasOpenSave/Phase;
    /// re-query them all whenever any of those flips.</summary>
    private void RaiseCommandCanExecuteChanged()
    {
        (RefreshSavesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenSaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CloseWithoutSavingCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveAndExitCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ScanPhantomCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RemovePhantomCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private string _filterText = "";
    public string FilterText { get => _filterText; set { if (Set(ref _filterText, value)) ApplyFilter(); } }

    /// <summary>Rows surviving the filter (bound by the grid).</summary>
    public IEnumerable<SavePictosRow> VisibleRows =>
        string.IsNullOrEmpty(FilterText)
            ? PictosRows
            : PictosRows.Where(r =>
                r.DisplayName.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                r.Id.Contains(FilterText, StringComparison.OrdinalIgnoreCase));

    private GvasFile? _save;
    private string? _openPath;

    // ───────────────────────── module flow ─────────────────────────

    /// <summary>Pick = active-save selection · Opening = loading screen · Edit = tabbed editor.</summary>
    public enum EditorPhase { Pick, Opening, Edit }
    private EditorPhase _phase = EditorPhase.Pick;
    public EditorPhase Phase
    {
        get => _phase;
        private set
        {
            if (!Set(ref _phase, value)) return;
            PhaseChanged?.Invoke();
            RaiseCommandCanExecuteChanged();
        }
    }

    /// <summary>Raised whenever <see cref="Phase"/> changes so the shell can slide layers.</summary>
    public event Action? PhaseChanged;

    /// <summary>Loading-screen headline: "Opening Save — …" with the save's details.</summary>
    private string _openingLabel = "";
    public string OpeningLabel { get => _openingLabel; private set => Set(ref _openingLabel, value); }

    private bool _outcomeIsError;
    public bool OutcomeIsError { get => _outcomeIsError; private set => Set(ref _outcomeIsError, value); }
    private bool _showOutcome;
    /// <summary>Save-and-exit result overlay: success auto-exits, failure stays until dismissed.</summary>

    public bool ShowOutcome { get => _showOutcome; private set => Set(ref _showOutcome, value); }

    private string _outcomeTitle = "";
    public string OutcomeTitle { get => _outcomeTitle; private set => Set(ref _outcomeTitle, value); }

    private string _outcomeDetail = "";
    public string OutcomeDetail { get => _outcomeDetail; private set => Set(ref _outcomeDetail, value); }

    /// <summary>Raised after pictos rows are (re)built following a load.</summary>
    public event Action? PictosRebuilt;

    /// <summary>Raised after a save is fully loaded and every tab's collections are filled.</summary>
    public event Action? SaveReloaded;

    /// <summary>Raised after a successful Save-and-exit: host closes the module.</summary>
    public event Action? ModuleExitRequested;
    public ICommand RefreshSavesCommand { get; }
    public ICommand OpenSaveCommand { get; }
    public ICommand CloseWithoutSavingCommand { get; }
    public ICommand SaveAndExitCommand { get; }
    public ICommand DismissOutcomeCommand { get; }
    public ICommand ScanPhantomCommand { get; }
    public ICommand RemovePhantomCommand { get; }

    private SaveScrubber.Report? _phantom;
    private List<string> _phantomSelection = new();

    /// <summary>Human summary of the last phantom-lumina scan ("" when none).</summary>
    public string PhantomSummary { get => _phantomSummary; set => Set(ref _phantomSummary, value); }
    private string _phantomSummary = "";

    /// <summary>True when a scan found orphans that can be removed.</summary>
    public bool CanRemovePhantom => _phantom is { OrphanUnlocked.Count: > 0 };

    private List<SaveFileEntry> _allSaves = new();

    public static string BackupRoot =>
        System.IO.Path.Combine(EditorPaths.RepoRoot, "work", "saves-backup");

    public SaveEditorViewModel()
    {
        RefreshSavesCommand = new RelayCommand(() => { RefreshSaves(); return Task.CompletedTask; }, () => CanAct);
        OpenSaveCommand = new RelayCommand(p => OpenSaveAsync(p as SaveFileEntry),
            _ => CanAct && Phase == EditorPhase.Pick);
        CloseWithoutSavingCommand = new RelayCommand(() => { CloseWithoutSaving(); return Task.CompletedTask; },
            () => CanAct && HasOpenSave);
        SaveAndExitCommand = new RelayCommand(async () => await SaveAndExitAsync(), () => CanAct && HasOpenSave);
        DismissOutcomeCommand = new RelayCommand(() => { ShowOutcome = false; return Task.CompletedTask; });
        ScanPhantomCommand = new RelayCommand(async () => await ScanPhantomsAsync(), () => CanAct && HasOpenSave);
        RemovePhantomCommand = new RelayCommand(async () => await RemovePhantomsAsync(),
            () => CanAct && HasOpenSave && CanRemovePhantom);
    }

    // ───────────────────────── discovery ─────────────────────────

    private const uint SteamAppId = 1903340; // Clair Obscur: Expedition 33

    /// <summary>Steam roots worth scanning for libraries / compatdata.</summary>
    private static IEnumerable<string> SteamRoots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            yield return "C:\\Program Files (x86)\\Steam";
            yield return System.IO.Path.Combine(home, "Steam");
            yield break;
        }
        yield return System.IO.Path.Combine(home, ".steam", "debian-installation");
        yield return System.IO.Path.Combine(home, ".steam", "steam");
        yield return System.IO.Path.Combine(home, ".local", "share", "Steam");
        yield return System.IO.Path.Combine(home, ".var", "app", "com.valvesoftware.Steam",
            ".local", "share", "Steam");
        yield return System.IO.Path.Combine(home, "Steam");
        yield return System.IO.Path.Combine(home, ".steam", "root");
    }

    /// <summary>Extra library roots parsed from a Steam install's libraryfolders.vdf.</summary>
    private static IEnumerable<string> LibraryRoots(string steamRoot)
    {
        var vdf = System.IO.Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) yield break;
        foreach (var line in File.ReadLines(vdf))
        {
            var parts = line.Trim().Split('"');
            if (parts.Length >= 4 && parts[1].Equals("path", StringComparison.OrdinalIgnoreCase))
                yield return parts[3].Replace("\\\\", "\\");
        }
    }

    private static string? CompatDataDir(string steamRoot)
    {
        var dir = System.IO.Path.Combine(steamRoot, "steamapps", "compatdata", SteamAppId.ToString());
        return Directory.Exists(dir) ? dir : null;
    }

    /// <summary>Every Sandfall SaveGames folder we can see (Proton prefix + Steam Cloud).</summary>
    private static IEnumerable<string> SaveGameDirectories()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (OperatingSystem.IsWindows())
        {
            var native = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Sandfall", "Saved", "SaveGames");
            if (Directory.Exists(native)) yield return native;
            yield break;
        }

        foreach (var root in SteamRoots())
        {
            IEnumerable<string> libs;
            try { libs = new[] { root }.Concat(LibraryRoots(root)).ToList(); }
            catch { continue; }
            foreach (var lib in libs)
            {
                string? compat = null;
                try { compat = CompatDataDir(lib); }
                catch { /* missing library */ }
                if (compat is not null)
                {
                    var users = System.IO.Path.Combine(compat, "pfx", "drive_c", "users");
                    if (Directory.Exists(users))
                    {
                        foreach (var user in Directory.EnumerateDirectories(users))
                        {
                            var saveGames = System.IO.Path.Combine(user,
                                "AppData", "Local", "Sandfall", "Saved", "SaveGames");
                            if (TryAddDir(seen, saveGames, out var full)) yield return full;
                        }
                    }
                }

                var userdata = System.IO.Path.Combine(lib, "userdata");
                if (!Directory.Exists(userdata)) continue;
                foreach (var user in Directory.EnumerateDirectories(userdata))
                {
                    var saveGames = System.IO.Path.Combine(user, SteamAppId.ToString(),
                        "ac", "WinAppDataLocal", "Sandfall", "Saved", "SaveGames");
                    if (TryAddDir(seen, saveGames, out var full)) yield return full;
                }
            }
        }
    }

    private static bool TryAddDir(HashSet<string> seen, string path, out string full)
    {
        full = "";
        try
        {
            if (!Directory.Exists(path)) return false;
            full = System.IO.Path.GetFullPath(path);
            return seen.Add(full);
        }
        catch
        {
            return false;
        }
    }

    private bool _scanning;
    private bool _rerunRequested;

    public void RefreshSaves() => _ = RefreshSavesAsync();

    /// <summary>Scans on a worker thread, applies results on the caller's context.</summary>
    public async Task RefreshSavesAsync()
    {
        // A click while a scan/enrichment pass is running queues exactly one
        // follow-up pass instead of being silently dropped.
        if (_scanning) { _rerunRequested = true; return; }
        _scanning = true;
        try
        {
            do
            {
                _rerunRequested = false;
                Status = "Scanning for save files…";
                var found = await Task.Run(ScanSaveFiles);
                _allSaves = found.OrderByDescending(e => e.ModifiedTicks).ToList();
                ApplyContinueFlags();
                MarkOpen(_openPath);
                SyncContinueList();
                Status = _allSaves.Count > 0
                    ? $"Found {_allSaves.Count} save file(s); reading summaries…"
                    : "No EXPEDITION_*.sav found.";

                await EnrichSavesAsync(_allSaves);
                Status = ContinueSaves.Count > 0
                    ? "Pick an active save to edit. Nothing is written until Save and exit."
                    : "No active saves found — start the game and save once, then refresh.";
            } while (_rerunRequested);
        }
        finally
        {
            _scanning = false;
        }
    }

    /// <summary>Auto-scan when the module opens; skips work if already populated.</summary>
    public async Task EnsureSavesLoadedAsync()
    {
        if (_allSaves.Count > 0 || _scanning) return;
        await RefreshSavesAsync();
    }

    private static List<SaveFileEntry> ScanSaveFiles()
    {
        var found = new Dictionary<string, SaveFileEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in SaveGameDirectories())
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "EXPEDITION_*.sav", SearchOption.TopDirectoryOnly);
                foreach (var account in Directory.EnumerateDirectories(root))
                {
                    files = files.Concat(Directory.EnumerateFiles(account, "EXPEDITION_*.sav",
                        SearchOption.TopDirectoryOnly));
                    var nestedBackup = System.IO.Path.Combine(account, "Backup");
                    if (Directory.Exists(nestedBackup))
                        files = files.Concat(Directory.EnumerateFiles(nestedBackup, "EXPEDITION_*.sav",
                            SearchOption.TopDirectoryOnly));
                }
            }
            catch
            {
                continue;
            }

            foreach (var sav in files)
            {
                try
                {
                    var info = new FileInfo(sav);
                    found[sav] = new SaveFileEntry
                    {
                        Path = sav,
                        Kind = ClassifySave(sav),
                        ModifiedUtc = info.LastWriteTimeUtc,
                        Modified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                    };
                }
                catch { /* skip unreadable */ }
            }
        }
        return found.Values.ToList();
    }

    /// <summary>
    /// Title-screen slots are EXPEDITION_N.sav. The game's rolling autosaves live
    /// in Backup/. Renamed copies only appear under All.
    /// </summary>
    public static string ClassifySave(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        var dir = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path) ?? "");
        if (dir.Equals("Backup", StringComparison.OrdinalIgnoreCase))
            return "Auto";
        if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^EXPEDITION_\d+\.sav$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return "Manual";
        return "Copy";
    }

    /// <summary>
    /// The pick screen lists only active saves: the newest title slot per Steam
    /// account — exactly the file the game's Continue loads.
    /// </summary>
    private void SyncContinueList()
    {
        var openPath = _openPath;
        ContinueSaves.Clear();
        foreach (var entry in _allSaves.Where(s => s.IsContinue))
            ContinueSaves.Add(entry);
        SelectedSave = ContinueSaves.FirstOrDefault(s =>
                          openPath is not null &&
                          string.Equals(s.Path, openPath, StringComparison.OrdinalIgnoreCase))
                      ?? ContinueSaves.FirstOrDefault();
    }

    /// <summary>Newest title-slot file per Steam account is what Continue loads.</summary>
    private void ApplyContinueFlags()
    {
        foreach (var s in _allSaves) s.IsContinue = false;
        foreach (var group in _allSaves.Where(s => s.Kind == "Manual").GroupBy(s => s.AccountId))
        {
            var newest = group.Max(s => s.ModifiedTicks);
            var pick = group.First(s => s.ModifiedTicks == newest);
            pick.IsContinue = true;
        }
    }

    private void MarkOpen(string? path)
    {
        foreach (var s in _allSaves)
            s.IsOpen = path is not null &&
                       string.Equals(s.Path, path, StringComparison.OrdinalIgnoreCase);
        Raise(nameof(OpenSaveTitle));
    }

    private static void ApplySummary(SaveFileEntry entry, SaveSummary summary)
    {
        entry.StoryPoint = summary.StoryPoint.Length > 0 ? summary.StoryPoint : "(prologue)";
        entry.Location = summary.Location;
        entry.PartyLevel = summary.PartyLevel;
        entry.TimePlayed = summary.TimePlayed;
    }

    /// <summary>Updates the existing picker row in place — never replaces it with a blank Lv 0 stub.</summary>
    private void SyncLoadedEntry(string path, GvasFile file)
    {
        var info = new FileInfo(path);
        var entry = _allSaves.FirstOrDefault(s =>
            string.Equals(s.Path, path, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            entry = new SaveFileEntry
            {
                Path = path,
                Kind = ClassifySave(path),
                ModifiedUtc = info.LastWriteTimeUtc,
                Modified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
            };
            _allSaves.Add(entry);
            _allSaves = _allSaves.OrderByDescending(s => s.ModifiedTicks).ToList();
        }
        else
        {
            entry.ModifiedUtc = info.LastWriteTimeUtc;
            entry.Modified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
        }
        ApplySummary(entry, SaveScan.Summarize(file));
        ApplyContinueFlags();
        SyncContinueList();
        MarkOpen(path);
    }

    /// <summary>Parses each save off the UI thread to fill story/location/level/time.</summary>
    private async Task EnrichSavesAsync(List<SaveFileEntry> entries)
    {
        foreach (var entry in entries)
        {
            var local = entry;
            var summary = await Task.Run(() =>
            {
                try
                {
                    return SaveScan.Summarize(GvasReader.ReadFile(local.Path));
                }
                catch
                {
                    return null;
                }
            });
            if (summary is null)
            {
                local.StoryPoint = "(unreadable)";
                continue;
            }
            local.StoryPoint = summary.StoryPoint.Length > 0 ? summary.StoryPoint : "(prologue)";
            local.Location = summary.Location;
            local.PartyLevel = summary.PartyLevel;
            local.TimePlayed = summary.TimePlayed;
        }
    }

    // ───────────────────────── load / save ─────────────────────────

    /// <summary>
    /// Pick-screen click (single click): shows the "Opening Save …" screen while
    /// the file parses, then slides the editor in. Nothing is written to disk
    /// until Save and exit.
    /// </summary>
    public async Task OpenSaveAsync(SaveFileEntry? entry)
    {
        if (IsBusy || entry is null) return;
        var details = new List<string> { entry.SlotTitle, entry.Name };
        if (entry.StoryPoint.Length > 0 && entry.StoryPoint != "…") details.Add(entry.StoryPoint);
        if (entry.Location.Length > 0) details.Add(entry.Location);
        if (entry.PartyLevel > 0) details.Add($"Lv {entry.PartyLevel}");
        OpeningLabel = "Opening Save — " + string.Join("  ·  ", details);
        Phase = EditorPhase.Opening;

        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        await LoadAsync(entry.Path);
        // Keep the loading screen readable even when the parse is instant.
        var remaining = 650 - (int)elapsed.ElapsedMilliseconds;
        if (remaining > 0) await Task.Delay(remaining);
        Phase = HasOpenSave ? EditorPhase.Edit : EditorPhase.Pick;
        if (!HasOpenSave) Status = $"Could not open {entry.Name} — {Status}";
    }

    public async Task LoadAsync(string? path)
    {
        if (IsBusy) return;
        if (string.IsNullOrEmpty(path))
        {
            Status = "No save selected.";
            return;
        }
        if (!File.Exists(path))
        {
            Status = $"File not found: {path}";
            return;
        }
        IsBusy = true;
        try
        {
            var parsed = await Task.Run(() => GvasReader.ReadFile(path));
            _save = parsed;
            _openPath = path;
            HasOpenSave = true;
            RebuildPictos();
            RebuildCharacters();
            RebuildWeapons();
            RebuildItems(MusicDisks, MusicCatalog);
            RebuildItems(Journals, JournalCatalog);
            RebuildWorldFlags();
            Raise(nameof(ExplorationSummary));
            Raise(nameof(Gold));
            Raise(nameof(NgPlus));
            Raise(nameof(SpawnPointTag));
            Raise(nameof(SpawnOptions));
            SyncLoadedEntry(path, parsed);
            SaveReloaded?.Invoke();
            var party = parsed is not null ? SaveScan.Summarize(parsed).PartyLevel : 0;
            var slot = SelectedSave?.SlotTitle ?? System.IO.Path.GetFileName(path);
            Status = $"Loaded {slot} ({System.IO.Path.GetFileName(path)}). Party Lv {party}. " +
                     "Nothing is written to disk until you press Save and exit.";
        }
        catch (Exception ex)
        {
            Status = $"Load failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Drops every unsaved in-memory edit and returns to the save picker.</summary>
    public void CloseWithoutSaving()
    {
        if (!HasOpenSave) return;
        DiscardLoadedSave();
        Status = "Closed without saving — the file on disk is untouched.";
        Phase = EditorPhase.Pick;
        _ = RefreshSavesAsync();
    }

    private void DiscardLoadedSave()
    {
        _save = null;
        _openPath = null;
        HasOpenSave = false;
        _phantom = null;
        _phantomSelection.Clear();
        PhantomSummary = "";
        SelectedCharacter = null;
        PictosRows.Clear();
        Characters.Clear();
        Weapons.Clear();
        MusicDisks.Clear();
        Journals.Clear();
        WorldFlags.Clear();
        Raise(nameof(ExplorationSummary));
        SaveReloaded?.Invoke();
    }

    /// <summary>
    /// Writes the edits, reports the outcome in an overlay, and on success
    /// leaves the module back to main module selection.
    /// </summary>
    public async Task SaveAndExitAsync()
    {
        var ok = await SaveAsync();
        OutcomeIsError = !ok;
        OutcomeTitle = ok ? "Saved successfully" : "Save failed";
        OutcomeDetail = Status;
        ShowOutcome = true;
        if (!ok) return;

        await Task.Delay(1600);
        if (!ShowOutcome || OutcomeIsError) return; // dismissed meanwhile
        ShowOutcome = false;
        DiscardLoadedSave();
        Phase = EditorPhase.Pick; // next module open lands on the picker
        ModuleExitRequested?.Invoke();
    }

    /// <summary>Writes the in-memory save back to disk. False on failure (Status says why).</summary>
    public async Task<bool> SaveAsync()
    {
        if (_save is null || _openPath is null) return false;
        IsBusy = true;
        try
        {
            var written = await Task.Run(() =>
            {
                EnsureOriginalBackup(_openPath);
                SaveGameEditor.TouchSaveDateTime(_save);
                var tmp = _openPath + ".tmp_e33";
                _save.Save(tmp);
                File.Move(tmp, _openPath, overwrite: true);
                BackupEdited(_openPath);
                return true;
            });
            var createdInSave = PictosRows.Count(r => r.IsCreated && r.Found);
            var createdTotal = PictosRows.Count(r => r.IsCreated);
            SyncLoadedEntry(_openPath, _save);
            var slot = SelectedSave?.SlotTitle ?? System.IO.Path.GetFileName(_openPath);
            var cont = SelectedSave?.IsContinue == true ? " This is now the Continue slot." : "";
            Status = $"Saved {slot} ({System.IO.Path.GetFileName(_openPath)}). In-game timestamp refreshed.{cont} " +
                     $"{createdInSave}/{createdTotal} created pictos are in the save" +
                     (createdInSave < createdTotal ? " — tick the rest on the Pictos tab." : ".");
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Save failed: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Scans the open save for weapon-lumina leftovers: unlocked passives no
    /// roster member may legally roll, plus character-mechanic lumina equipped
    /// on characters that cannot use them. Nothing is modified.
    /// </summary>
    public async Task ScanPhantomsAsync()
    {
        if (_save is null) return;
        IsBusy = true;
        try
        {
            var report = await Task.Run(() =>
            {
                var catalog = new LootCatalog();
                return SaveScrubber.Scan(_save, catalog);
            });
            _phantom = report;
            var parts = new List<string>();
            if (report.OrphanUnlocked.Count > 0)
                parts.Add($"{report.OrphanUnlocked.Count} orphan unlock(s): {string.Join(", ", report.OrphanUnlocked.Take(8))}");
            if (report.WrongEquipped.Count > 0)
                parts.Add($"{report.WrongEquipped.Count} wrong-owner equip(s): {string.Join(", ", report.WrongEquipped.Take(8).Select(w => $"{w.Character}←{w.Id}"))}");
            PhantomSummary = parts.Count == 0
                ? "No phantom lumina — clean."
                : string.Join("  |  ", parts);
            Status = PhantomSummary;
        }
        catch (Exception ex)
        {
            Status = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Removes scanned orphan unlocks (and unequips them) from the open save.</summary>
    public async Task RemovePhantomsAsync()
    {
        if (_save is null || _phantom is null || _openPath is null) return;
        IsBusy = true;
        try
        {
            var removed = await Task.Run(() =>
            {
                EnsureOriginalBackup(_openPath!);
                var n = SaveScrubber.Remove(_save, _phantomSelection.Count > 0 ? _phantomSelection : _phantom.OrphanUnlocked);
                SaveGameEditor.TouchSaveDateTime(_save);
                var tmp = _openPath + ".tmp_e33";
                _save.Save(tmp);
                File.Move(tmp, _openPath, overwrite: true);
                BackupEdited(_openPath);
                return n;
            });
            Status = $"Removed {removed} phantom lumina entrie(s). Use Save to persist if not already written.";
            _phantom = null;
            PhantomSummary = "Removed.";
        }
        catch (Exception ex)
        {
            Status = $"Removal failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void EnsureOriginalBackup(string path)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        var dir = System.IO.Path.Combine(BackupRoot, name);
        if (Directory.Exists(dir) &&
            Directory.EnumerateFiles(dir, "original_*.sav").Any())
            return;
        CopyBackup(path, "original");
    }

    private static void BackupEdited(string path)
    {
        CopyBackup(path, "edited");
    }

    private static void CopyBackup(string path, string kind)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        var dir = System.IO.Path.Combine(BackupRoot, name);
        Directory.CreateDirectory(dir);
        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        File.Copy(path, System.IO.Path.Combine(dir, $"{kind}_{stamp}.sav"), overwrite: true);
    }

    // ───────────────────────── pictos ─────────────────────────

    private void RebuildPictos()
    {
        PictosRows.Clear();
        if (_save is null) return;

        Dictionary<string, SaveGameEditor.PictosState> states;
        try { states = SaveGameEditor.PictosStates(_save); }
        catch (Exception ex)
        {
            Status = ex.Message;
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Current-content sets: everything the tier list quarantines or the
        // catalog flags as cut/broken counts as unsupported, except ids the
        // kits still register (they are current mod content even when the
        // randomizer deliberately never drops them).
        var createdIds = SupportedCreatedIds();
        var catalog = GameItemCatalog.Value;
        var unsupported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        unsupported.UnionWith(catalog.ItemTiers.Column(ItemTiers.Pictos, "DoNotUse"));
        foreach (var i in catalog.Items.Values)
            if (i.CutContent) unsupported.Add(i.CodeName);
        unsupported.ExceptWith(createdIds);

        // Created families first so they sit at the top of the grid.
        foreach (var (spec, group) in CreatedSpecs())
        {
            seen.Add(spec.Id);
            var st = states.GetValueOrDefault(spec.Id, new SaveGameEditor.PictosState(false, false, 1));
            PictosRows.Add(new SavePictosRow
            {
                Id = spec.Id,
                DisplayName = spec.DisplayName,
                Group = group,
                Found = st.Found,
                Mastered = st.Mastered,
                Level = Math.Max(1, st.Level),
                InPack = IsInPack(spec.Id),
                Description = PictosDescription?.Invoke(spec.Id)
                    ?? PictosCreateKit.PainterConversions.FirstOrDefault(p => p.Id == spec.Id)?.Description
                    ?? PictosCreateKit.Powers.FirstOrDefault(p => p.Id == spec.Id)?.Description
                    ?? PictosMagnitudeKit.Variants.FirstOrDefault(p => p.Id == spec.Id)?.Description
                    ?? PictosCreateKit.HitBurn.FirstOrDefault(p => p.Id == spec.Id)?.Description
                    ?? "",
            });
        }

        var vanillaRows = VanillaCatalog().ToList();
        var vanillaIds = new HashSet<string>(vanillaRows.Select(v => v.Id), StringComparer.OrdinalIgnoreCase);
        foreach (var (id, display) in vanillaRows)
        {
            if (!seen.Add(id)) continue;
            var st = states.GetValueOrDefault(id, new SaveGameEditor.PictosState(false, false, 1));
            // Cut/retired rows only stay listed when the save actually owns
            // them — unowned ones are just noise and would be swept by bulk
            // "Add all vanilla".
            if (unsupported.Contains(id) && !st.Found && !st.Mastered) continue;
            PictosRows.Add(new SavePictosRow
            {
                Id = id,
                DisplayName = string.IsNullOrEmpty(display) ? id : display,
                Found = st.Found,
                Mastered = st.Mastered,
                Level = Math.Max(1, st.Level),
                NotSupported = unsupported.Contains(id),
                Description = PictosDescription?.Invoke(id) ?? "",
            });
        }

        // Anything present in the save but missing from catalogs still shows up
        // — but only if it is actually a pictos. The inventory progression is
        // shared with weapons, music records, journals, customization and
        // resources; those must never land on this tab (bulk level/find would
        // otherwise rewrite them too). Unknown ids are retired mod content the
        // save still carries, so they are flagged and excluded from bulk ops.
        foreach (var (id, st) in states.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!seen.Add(id)) continue;
            if (!IsPictosEntry(id)) continue;
            PictosRows.Add(new SavePictosRow
            {
                Id = id,
                DisplayName = id,
                Found = st.Found,
                Mastered = st.Mastered,
                Level = Math.Max(1, st.Level),
                NotSupported = !vanillaIds.Contains(id) && !createdIds.Contains(id),
                Description = PictosDescription?.Invoke(id) ?? "",
            });
        }

        Raise(nameof(VisibleRows));
        PictosRebuilt?.Invoke();
    }

    private static readonly Lazy<LootCatalog> GameItemCatalog = new(() => new LootCatalog());

    /// <summary>
    /// Every id the pictos kits currently register (regardless of family
    /// toggle): current mod content, so never treated as unsupported even when
    /// the tier list quarantines it from randomizer pools.
    /// </summary>
    private static HashSet<string> SupportedCreatedIds()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in PictosCreateKit.PainterConversions) set.Add(s.Id);
        foreach (var s in PictosCreateKit.Powers) set.Add(s.Id);
        foreach (var s in PictosCreateKit.HitBurn) set.Add(s.Id);
        foreach (var s in PictosMagnitudeKit.Variants) set.Add(s.Id);
        foreach (var (id, _) in BombPictosKit.CreatedPictos) set.Add(id);
        foreach (var (id, _) in ReviveDebuffKit.CreatedPictos) set.Add(id);
        // Always-on unique reward pictos (dialogue / pinned fights). They are
        // valid packed content, so never flag a save that owns one as retired.
        set.Add(SpecialPictosKit.AliciaId);
        set.Add(SpecialPictosKit.LayeredId);
        set.Add(SpecialPictosKit.SuperSonicId);
        set.Add(SpecialPictosKit.RuptureId);
        set.Add(SpecialPictosKit.UnoPunchoId);
        return set;
    }

    /// <summary>
    /// True for ids that belong on the Pictos tab: known pictos plus unknown
    /// (mod-added) ids. Known non-pictos inventory — weapons, music records,
    /// journals, customization, resources, consumables — is excluded.
    /// </summary>
    private static bool IsPictosEntry(string id)
    {
        if (GameItemCatalog.Value.Items.TryGetValue(id, out var info))
            return info.Type.Equals("Pictos", StringComparison.Ordinal);
        return true;
    }


    private IEnumerable<(PictosConversionSpec Spec, string Group)> CreatedSpecs()
    {
        var modDir = System.IO.Path.Combine(EditorPaths.RepoRoot, "work", "mod");
        var painterOn = PainterConversionEnabled?.Invoke() == true
                        || PictosCreateKit.FindAsset(modDir, "BP_LU_PainterConv_Fire.uasset") is not null;
        var affinityOn = StatusAffinityEnabled?.Invoke() == true
                         || PictosCreateKit.FindAsset(modDir, "BP_LU_BerserkPower1.uasset") is not null;
        var magnitudeOn = PictosMagnitudeKit.HasGeneratedAssets(modDir)
                          || (EditorProject.TryRead(EditorProject.SidecarPath)?.PictosCreate.Value<bool?>("PictosMagnitude") == true);
        var hitBurnOn = PictosCreateKit.FindAsset(modDir, "BP_LU_BurnOnHit1.uasset") is not null;
        var bombOn = BombPictosKit.IsStaged(modDir);
        var reviveOn = ReviveDebuffKit.IsStaged(modDir);
        if (!painterOn && !affinityOn && !magnitudeOn && !hitBurnOn && !bombOn && !reviveOn) yield break;
        foreach (var s in PictosCreateKit.PainterConversions)
            if (painterOn)
                yield return (new PictosConversionSpec(s.Id, s.DisplayName), "created-painter");
        foreach (var s in PictosCreateKit.Powers)
            if (affinityOn)
                yield return (new PictosConversionSpec(s.Id, s.DisplayName), "created-power");
        foreach (var s in PictosMagnitudeKit.Variants)
            if (magnitudeOn)
                yield return (new PictosConversionSpec(s.Id, s.DisplayName), "created-magnitude");
        foreach (var s in PictosCreateKit.HitBurn)
            if (hitBurnOn)
                yield return (new PictosConversionSpec(s.Id, s.DisplayName), "created-hitburn");
        foreach (var (id, display) in BombPictosKit.CreatedPictos)
            if (bombOn)
                yield return (new PictosConversionSpec(id, display), "created-bomb");
        foreach (var (id, display) in ReviveDebuffKit.CreatedPictos)
            if (reviveOn)
                yield return (new PictosConversionSpec(id, display), "created-revive");
    }


    /// <summary>
    /// Whether the packed mod contains this created pictos: either its asset is
    /// baked into work/mod, or the packed sidecar project enables the family.
    /// </summary>
    private bool IsInPack(string id)
    {
        var modDir = System.IO.Path.Combine(EditorPaths.RepoRoot, "work", "mod");
        if (PictosCreateKit.FindAsset(modDir, "DA_GearPictos_" + id + ".uasset") is not null)
            return true;
        var project = EditorProject.TryRead(EditorProject.SidecarPath);
        if (project?.PictosCreate is not null)
        {
            var painter = project.PictosCreate.Value<bool?>("PainterConversion") == true &&
                          PictosCreateKit.PainterConversions.Any(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            var affinity = project.PictosCreate.Value<bool?>("StatusAffinity") == true &&
                           PictosCreateKit.Powers.Any(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            var magnitude = project.PictosCreate.Value<bool?>("PictosMagnitude") == true &&
                            PictosMagnitudeKit.Variants.Any(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            var hitBurn = PictosCreateKit.HitBurn.Any(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            return painter || affinity || magnitude || hitBurn;
        }
        // HitBurn is always packed (no toggle) — fallback
        if (PictosCreateKit.HitBurn.Any(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            return true;
        return false;
    }



    private IEnumerable<(string Id, string DisplayName)> VanillaCatalog() =>
        VanillaPictosCatalog?.Invoke() ?? Enumerable.Empty<(string, string)>();

    // ───────────────────────── characters / resources / world ─────────────────────────

    public int Gold
    {
        get => _save is null ? 0 : SaveGameEdits.GetGold(_save);
        set
        {
            if (_save is null) return;
            var v = Math.Max(0, value);
            if (SaveGameEdits.GetGold(_save) == v) return;
            SaveGameEdits.SetGold(_save, v);
            Raise();
            Status = $"Chroma set to {v:N0}. Press Save to persist.";
        }
    }

    public int NgPlus
    {
        get => _save is null ? 0 : SaveGameEdits.GetNgPlus(_save);
        set
        {
            if (_save is null) return;
            var v = Math.Max(0, value);
            if (SaveGameEdits.GetNgPlus(_save) == v) return;
            SaveGameEdits.SetNgPlus(_save, v);
            Raise();
            Status = $"NG+ count set to {v}. Press Save to persist.";
        }
    }

    /// <summary>Stackable inventory counts: tint shards, catalysts, chroma packs…</summary>
    public int GetResource(string id) =>
        _save is null ? 0 : SaveGameEdits.GetItemCount(_save, id);

    public void SetResource(string id, int count, string label)
    {
        if (_save is null) return;
        try
        {
            SaveGameEdits.SetItemCount(_save, id, Math.Max(0, count));
            Status = $"{label} set to {Math.Max(0, count)}. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    public string SpawnPointTag
    {
        get => _save is null ? "" : SaveGameEdits.GetSpawnTag(_save);
        set
        {
            if (_save is null || string.IsNullOrWhiteSpace(value)) return;
            if (string.Equals(SaveGameEdits.GetSpawnTag(_save), value, StringComparison.Ordinal))
                return;
            var known = SpawnCatalog.Find(value);
            SaveGameEdits.SetSpawn(_save, value.Trim(), known?.LevelAsset);
            Raise();
            Raise(nameof(SpawnPointLabel));
            var label = known is null ? value : known.Name;
            Status = $"Spawn point set to {label}. Press Save to persist.";
        }
    }

    public string SpawnPointLabel
    {
        get
        {
            var tag = SpawnPointTag;
            if (tag.Length == 0) return "(none)";
            var known = SpawnCatalog.Find(tag);
            return known is null ? tag : $"{known.Name}  ({known.Tag})";
        }
    }

    /// <summary>Friendly labels for the spawn combo; values are the raw tags.</summary>
    public IReadOnlyList<SpawnPoint> SpawnOptions => SpawnCatalog.All;

    public List<string> ExplorationCapacities => _save is null
        ? new List<string>()
        : SaveGameEdits.ExplorationCapacities(_save);

    /// <summary>Human summary for the World tab footer.</summary>
    public string ExplorationSummary
    {
        get
        {
            if (_save is null) return "";
            var caps = ExplorationCapacities;
            return caps.Count == 0
                ? "No exploration capacities recorded in this save."
                : "Unlocked exploration capacities: " + string.Join(", ", caps);
        }
    }

    public void AddExplorationCapacity(string label)
    {
        if (_save is null) return;
        try
        {
            SetExplorationLabel(_save, label, present: true);
            Status = $"Exploration capacity '{label}' enabled. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    public void SetFreeAimLevel(int level)
    {
        if (_save is null) return;
        try
        {
            SaveGameEdits.SetFreeAimLevel(_save, level);
            Status = $"Free Aim damage level set to {level}. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    /// <summary>Adds a capacity label into the first byte-label array of the struct.</summary>
    private static void SetExplorationLabel(GvasFile file, string label, bool present)
    {
        var prop = file.Properties.FirstOrDefault(p =>
            p.Name.StartsWith("ExplorationProgression", StringComparison.Ordinal));
        if (prop?.Value.Payload is not GvasPayload.Struct st || st.Fields is null)
            throw new InvalidDataException("save has no ExplorationProgression struct");

        foreach (var f in st.Fields)
        {
            if (f.Value.Payload is GvasPayload.Array arr && arr.Items.Count > 0 &&
                arr.Items[0].Payload is GvasPayload.Byte b && b.Label is not null)
            {
                var labels = arr.Items.Select(i => ((GvasPayload.Byte)i.Payload!).Label!)
                    .Where(l => !l.Equals(label, StringComparison.OrdinalIgnoreCase)).ToList();
                if (present) labels.Add(label);
                var elemType = arr.Items[0].Type;
                arr.Items.Clear();
                foreach (var l in labels)
                    arr.Items.Add(new TypedValue(elemType, new GvasPayload.Byte { Label = l }));
                return;
            }
        }
        throw new InvalidDataException("no exploration capacity array found");
    }

    private static readonly string[] AttributeOrder =
    {
        "ECharacterAttribute::NewEnumerator0", // Vitality
        "ECharacterAttribute::NewEnumerator1", // Might
        "ECharacterAttribute::NewEnumerator3", // Agility
        "ECharacterAttribute::NewEnumerator4", // Defense
        "ECharacterAttribute::NewEnumerator5", // Luck
    };

    private void RebuildCharacters()
    {
        var keepId = SelectedCharacter?.Id;
        Characters.Clear();
        if (_save is null)
        {
            SelectedCharacter = null;
            return;
        }
        var party = SaveGameEdits.Party(_save).ToDictionary(p => p.Id, p => p.Formation,
            StringComparer.OrdinalIgnoreCase);
        var loaded = SaveGameEdits.Characters(_save)
            .ToDictionary(c => c.Id, c => c, StringComparer.OrdinalIgnoreCase);
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rosterId in CharacterRoster)
        {
            var recordId = RecordIdFor(rosterId);
            covered.Add(recordId);
            if (loaded.TryGetValue(recordId, out var c))
                Characters.Add(BuildCharacterRow(c, party));
            else
                Characters.Add(new CharacterRow { Id = recordId, InSave = false });
        }
        // Records outside the roster (cut characters, guests) stay editable too.
        foreach (var c in loaded.Values)
            if (covered.Add(c.Id))
                Characters.Add(BuildCharacterRow(c, party));
        SelectedCharacter = Characters.FirstOrDefault(r => r.Id == keepId)
                            ?? Characters.FirstOrDefault(r => r.InParty)
                            ?? Characters.FirstOrDefault(r => r.InSave)
                            ?? Characters.FirstOrDefault();
        Raise(nameof(ExplorationCapacities));
    }


    /// <summary>
    /// Save-record key for a roster entry. Gustave's record is keyed
    /// <c>Frey</c> in save data (his internal codename); every other roster
    /// member's record key matches their display name.
    /// </summary>
    public static string RecordIdFor(string rosterId) =>
        rosterId.Equals("Gustave", StringComparison.OrdinalIgnoreCase) ? "Frey" : rosterId;
    private CharacterRow BuildCharacterRow(
        SaveGameEdits.CharacterInfo c,
        Dictionary<string, string> party)
    {
        var gear = SaveGameEdits.EquipmentOf(_save!, c.Id);
        var attrs = SaveGameEdits.AttributesOf(_save!, c.Id);
        var slots = new[] { "", "", "" };
        foreach (var g in gear.Where(g =>
                     g.ItemType.Equals(SaveGameEdits.ItemTypePictos, StringComparison.OrdinalIgnoreCase)))
        {
            if (g.SlotIndex is >= 0 and <= 2) slots[g.SlotIndex] = g.ItemId;
        }
        return new CharacterRow
        {
            Id = c.Id,
            InSave = true,
            Level = c.Level,
            CurrentXp = c.CurrentExperience,
            ActionPoints = c.ActionPoints,
            Lumina = c.LuminaFromConsumables,
            Hp = (int)c.Hp,
            Mp = (int)c.Mp,
            Excluded = c.Excluded,
            InParty = party.ContainsKey(c.Id),
            Formation = party.GetValueOrDefault(c.Id, ""),
            Skin = c.Skin,
            Face = c.Face,
            Weapon = gear.FirstOrDefault(g =>
                g.ItemType.Equals(SaveGameEdits.ItemTypeWeapon, StringComparison.OrdinalIgnoreCase))?.ItemId ?? "",
            PictosSlots = slots,
            Vitality = Attr(attrs, AttributeOrder[0]),
            Might = Attr(attrs, AttributeOrder[1]),
            Agility = Attr(attrs, AttributeOrder[2]),
            Defense = Attr(attrs, AttributeOrder[3]),
            Luck = Attr(attrs, AttributeOrder[4]),
            Skills = c.Skills,
            EquippedSkills = EquippedSkillsOf(c.Id),
            EquippedLuminas = c.EquippedPictos,
        };
    }

    private static int Attr(Dictionary<string, int> attrs, string key) =>
        attrs.TryGetValue(key, out var v) ? v : 0;

    private List<string> EquippedSkillsOf(string id)
    {
        if (_save is null) return new List<string>();
        List<string> skills = new();
        SaveGameEdits.EditCharacter(_save, id, edit => skills = edit.EquippedSkills.ToList());
        return skills;
    }

    private CharacterRow? _selectedCharacter;
    public CharacterRow? SelectedCharacter { get => _selectedCharacter; set => Set(ref _selectedCharacter, value); }

    /// <summary>Full pictos catalog for equipping: vanilla + created ✨.</summary>
    public IEnumerable<(string Id, string DisplayName)> PictosCatalog =>
        CreatedSpecs().Select(s => (s.Spec.Id, s.Spec.DisplayName))
            .Concat(VanillaCatalog());

    /// <summary>Skin ids from the save's inventory, preferring this character's own line.</summary>
    public List<string> SkinOptions(CharacterRow row) => CustomizationOptions("Skin", row);

    /// <summary>Face ids from the save's inventory, preferring this character's own line.</summary>
    public List<string> FaceOptions(CharacterRow row) => CustomizationOptions("Face", row);

    private List<string> CustomizationOptions(string prefix, CharacterRow row)
    {
        if (_save is null) return new List<string>();
        var all = SaveGameEditor.InventoryIds(_save, out _)
            .Where(id => id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var own = all.Where(id => id.Contains(GameLabels.DisplayAlias(row.Id), StringComparison.OrdinalIgnoreCase)).ToList();
        return own.Count > 0 ? own : all;
    }

    /// <summary>
    /// Makes a roster member editable even though the save has no record for
    /// them yet: clones a fresh-recruit record into CharactersCollection and
    /// swaps the list stub for a fully editable row.
    public void ActivateCharacterRecord(CharacterRow row)
    {
        if (_save is null || row.InSave) return;
        try
        {
            SaveGameEdits.EnsureCharacterRecord(_save, row.Id, $"Combo1_{GameLabels.DisplayAlias(row.Id)}");
            var info = SaveGameEdits.Characters(_save).FirstOrDefault(c =>
                c.Id.Equals(row.Id, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("record missing after creation");
            var party = SaveGameEdits.Party(_save).ToDictionary(p => p.Id, p => p.Formation,
                StringComparer.OrdinalIgnoreCase);
            var fresh = BuildCharacterRow(info, party);
            var idx = Characters.IndexOf(row);
            if (idx >= 0) Characters[idx] = fresh; else Characters.Add(fresh);
            SelectedCharacter = fresh;
            Status = $"{fresh.DisplayName} added to this save as a fresh level-1 recruit — " +
                     "toggle In active party to field them. Press Save and exit to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    private static readonly Lazy<IReadOnlyDictionary<string, List<string>>> CustomizationCatalog =
        new(BuildCustomizationCatalog);

    /// <summary>Character token → every Skin*/Face* customization id in the game data.</summary>
    private static IReadOnlyDictionary<string, List<string>> BuildCustomizationCatalog()
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in new LootCatalog().Items.Values)
        {
            if (!item.Type.Equals("CharacterCustomization", StringComparison.Ordinal)) continue;
            var m = System.Text.RegularExpressions.Regex.Match(item.CodeName, "^(?:Skin|Face)([A-Za-z]+)_");
            if (!m.Success) continue;
            var ch = m.Groups[1].Value;
            if (!map.TryGetValue(ch, out var list)) map[ch] = list = new List<string>();
            list.Add(item.CodeName);
        }
        return map;
    }

    /// <summary>Adds every known costume (Skin*) and hairstyle (Face*) for this
    /// character to the inventory; persisted by Save and exit.</summary>
    public void UnlockAllCostumes(CharacterRow row)
    {
        if (_save is null || !row.InSave) return;
        try
        {
            var key = GameLabels.DisplayAlias(row.Id);
            if (!CustomizationCatalog.Value.TryGetValue(key, out var ids) || ids.Count == 0)
            {
                Status = $"No costumes or hairstyles found in the catalog for {key}.";
                return;
            }
            var owned = SaveGameEditor.InventoryIds(_save, out _);
            var added = 0;
            foreach (var id in ids.Where(id => !owned.Contains(id)))
            {
                SaveGameEditor.SetItemOwned(_save, id, true);
                added++;
            }
            Status = added > 0
                ? $"Unlocked all {ids.Count} costumes/hairstyles for {key} ({added} new). Press Save and exit to persist."
                : $"All {ids.Count} costumes/hairstyles for {key} were already unlocked.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    public void CommitCharacter(CharacterRow row)
    {
        if (_save is null || !row.InSave) return;
        try
        {
            SaveGameEdits.EditCharacter(_save, row.Id, edit =>
            {
                edit.Level = Math.Clamp(row.Level, 1, LevelCap);
                edit.CurrentExperience = row.CurrentXp;
                edit.ActionPoints = row.ActionPoints;
                edit.LuminaFromConsumables = row.Lumina;
                edit.Hp = row.Hp;
                edit.Mp = row.Mp;
                edit.Excluded = row.Excluded;
                if (row.Skin.Length > 0) edit.Skin = row.Skin;
                if (row.Face.Length > 0) edit.Face = row.Face;
            });
            Status = $"{row.Id} updated. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    /// <summary>Party toggle writes CurrentParty; the Party of Five mod raises the cap to 5.</summary>
    public void SetPartyMember(CharacterRow row, bool inParty)
    {
        if (_save is null || !row.InSave) return;
        try
        {
            if (!SaveGameEdits.SetPartyMember(_save, row.Id, inParty))
            {
                row.InParty = false;
                Status = "Party is full — this editor allows at most 5 active members (requires Party of Five in game). " +
                         "Untick one of them first, then add this character.";
                return;
            }
            row.InParty = inParty;
            var party = SaveGameEdits.Party(_save);
            var formation = party.FirstOrDefault(p => p.Id.Equals(row.Id, StringComparison.OrdinalIgnoreCase))
                .Formation ?? "";
            if (!string.IsNullOrEmpty(formation)) row.Formation = formation;
            foreach (var other in Characters)
                other.InParty = party.Any(p => p.Id.Equals(other.Id, StringComparison.OrdinalIgnoreCase));
            Status = $"{row.Id} {(inParty ? "joined" : "left")} the party ({party.Count}/5). Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    /// <summary>Worn pictos slot 0–2 (the three pictos on the character sheet).</summary>
    public void SetPictosSlot(CharacterRow row, int slot, string pictosId)
    {
        if (_save is null || slot is < 0 or > 2) return;
        try
        {
            if (!string.IsNullOrEmpty(pictosId))
                SaveGameEditor.SetPictosFound(_save, pictosId, true);
            SaveGameEdits.SetEquipment(_save, row.Id, SaveGameEdits.ItemTypePictos, slot, pictosId);
            row.PictosSlots[slot] = pictosId;
            Status = string.IsNullOrEmpty(pictosId)
                ? $"{row.Id}: pictos slot {slot + 1} cleared. Press Save to persist."
                : $"{row.Id}: pictos slot {slot + 1} = {pictosId}. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    public void SetWeapon(CharacterRow row, string weaponId)
    {
        if (_save is null) return;
        try
        {
            if (!string.IsNullOrEmpty(weaponId))
                SaveGameEditor.SetItemOwned(_save, weaponId, true);
            SaveGameEdits.SetEquipment(_save, row.Id, SaveGameEdits.ItemTypeWeapon, 0, weaponId);
            row.Weapon = weaponId;
            Status = $"{row.Id}: weapon = {weaponId}. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    public void SetAttributePoints(CharacterRow row, string enumLabel, int value)
    {
        if (_save is null) return;
        try
        {
            SaveGameEdits.SetAttribute(_save, row.Id, enumLabel, value);
            if (enumLabel.EndsWith("NewEnumerator0", StringComparison.Ordinal)) row.Vitality = value;
            else if (enumLabel.EndsWith("NewEnumerator1", StringComparison.Ordinal)) row.Might = value;
            else if (enumLabel.EndsWith("NewEnumerator3", StringComparison.Ordinal)) row.Agility = value;
            else if (enumLabel.EndsWith("NewEnumerator4", StringComparison.Ordinal)) row.Defense = value;
            else if (enumLabel.EndsWith("NewEnumerator5", StringComparison.Ordinal)) row.Luck = value;
            Status = $"{row.Id}: attribute updated. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    public void EquipLumina(CharacterRow row, string pictosId)
    {
        if (_save is null || string.IsNullOrEmpty(pictosId)) return;
        try
        {
            SaveGameEdits.EditCharacter(_save, row.Id, edit => edit.EquipPictos(pictosId));
            if (!row.EquippedLuminas.Contains(pictosId, StringComparer.OrdinalIgnoreCase))
                row.EquippedLuminas.Add(pictosId);
            Status = $"{row.Id}: lumina '{pictosId}' equipped. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    public void UnequipLumina(CharacterRow row, string pictosId)
    {
        if (_save is null) return;
        try
        {
            SaveGameEdits.EditCharacter(_save, row.Id, edit => edit.UnequipPictos(pictosId));
            row.EquippedLuminas.RemoveAll(p => p.Equals(pictosId, StringComparison.OrdinalIgnoreCase));
            Status = $"{row.Id}: lumina '{pictosId}' unequipped. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    public void SetEquippedSkill(CharacterRow row, int index, string skill)
    {
        if (_save is null || index < 0) return;
        try
        {
            var list = row.EquippedSkills.ToList();
            while (list.Count <= index) list.Add("");
            list[index] = skill;
            list = list.Where(s => s.Length > 0).Take(6).ToList();
            SaveGameEdits.EditCharacter(_save, row.Id, edit => edit.SetEquippedSkills(list));
            row.EquippedSkills = list;
            Status = $"{row.Id}: equipped skills updated. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    public void AddSkill(CharacterRow row, string skill)
    {
        if (_save is null || string.IsNullOrWhiteSpace(skill)) return;
        try
        {
            skill = skill.Trim();
            SaveGameEdits.EditCharacter(_save, row.Id, edit => edit.AddSkill(skill));
            row.Skills.Add(skill);
            Status = $"{row.Id}: unlocked '{skill}'. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    public void RemoveSkill(CharacterRow row, string skill)
    {
        if (_save is null) return;
        try
        {
            SaveGameEdits.EditCharacter(_save, row.Id, edit => edit.RemoveSkill(skill));
            row.Skills.RemoveAll(s => s.Equals(skill, StringComparison.OrdinalIgnoreCase));
            Status = $"{row.Id}: removed '{skill}'. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    public void SetAppearance(CharacterRow row, string skin, string face)
    {
        if (_save is null) return;
        try
        {
            row.Skin = skin;
            row.Face = face;
            SaveGameEdits.EditCharacter(_save, row.Id, edit =>
            {
                if (skin.Length > 0) edit.Skin = skin;
                if (face.Length > 0) edit.Face = face;
            });
            Status = $"{row.Id}: appearance set. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    private void RebuildWeapons()
    {
        Weapons.Clear();
        if (_save is null) return;

        Dictionary<string, SaveGameEditor.PictosState> states;
        try { states = SaveGameEditor.PictosStates(_save); }
        catch { return; }

        foreach (var (id, name) in WeaponCatalog.OrderBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase))
        {
            var st = states.GetValueOrDefault(id, new SaveGameEditor.PictosState(false, false, 1));
            Weapons.Add(new ItemRow
            {
                Id = id,
                DisplayName = string.IsNullOrEmpty(name) ? id : name,
                Character = WeaponOwners.TryGetValue(id, out var ch) ? ch : "",
                Owned = st.Found,
                Level = Math.Max(1, st.Level),
                HasLevel = true,
            });
        }
    }

    private void RebuildItems(ObservableCollection<ItemRow> target, Dictionary<string, string> catalog)
    {
        target.Clear();
        if (_save is null || catalog.Count == 0) return;
        System.Collections.Generic.HashSet<string> owned;
        owned = SaveGameEditor.InventoryIds(_save, out _);
        foreach (var (id, name) in catalog.OrderBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase))
            target.Add(new ItemRow
            {
                Id = id,
                DisplayName = string.IsNullOrEmpty(name) ? id : name,
                Owned = owned.Contains(id),
            });
    }

    public void ToggleOwned(ItemRow row, bool owned)
    {
        if (_save is null) return;
        try
        {
            SaveGameEditor.SetItemOwned(_save, row.Id, owned);
            row.Owned = owned;
            Status = $"{row.DisplayName}: {(owned ? "added" : "removed")}. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    public void CommitWeaponLevel(ItemRow row) => CommitWeaponLevel(row, row.Level);

    public void CommitWeaponLevel(ItemRow row, int level)
    {
        if (_save is null) return;
        try
        {
            if (!row.Owned) ToggleOwned(row, true);
            var cap = WeaponLevelCap;
            SaveGameEditor.SetItemLevel(_save, row.Id, level, cap);
            row.Level = Math.Clamp(level, 1, cap);
            Status = $"{row.DisplayName}: level {row.Level}. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    private void RebuildWorldFlags()
    {
        WorldFlags.Clear();
        if (_save is null) return;
        foreach (var mapName in new[]
                 {
                     "InteractedObjects", "BattledEnemies", "EncounteredEnemies", "TransientBattledEnemies",
                 })
        {
            foreach (var (key, value) in SaveGameEdits.MapBools(_save, mapName)
                         .OrderBy(e => e.Key, StringComparer.Ordinal))
                WorldFlags.Add(new FlagRow { MapName = mapName, Key = key, Value = value });
        }
    }

    public void ToggleFlag(FlagRow row, bool value)
    {
        if (_save is null) return;
        try
        {
            SaveGameEdits.SetMapBool(_save, row.MapName, row.Key, value);
            row.Value = value;
            Status = $"{row.Key}: {(value ? "set" : "cleared")}. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    public IReadOnlyList<string> StoryPointChoices =>
        StoryPointKit.Chapters.Select(c => $"{c.Index}. {c.Name}").ToList();

    /// <summary>Chapter matching the save's current story summary (best effort).</summary>
    public int CurrentStoryPointIndex
    {
        get
        {
            if (_save is null) return 0;
            var story = SaveScan.Summarize(_save).StoryPoint;
            for (var i = 0; i < StoryPointKit.Chapters.Length; i++)
            {
                var name = StoryPointKit.Chapters[i].Name;
                if (story.Equals(name, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return 0;
        }
    }

    /// <summary>
    /// Rewinds the in-memory save to a main-story chapter. Nothing is written
    /// until Save and exit — Close without saving restores the file on disk.
    /// Side content, chests and merchants keep their current state.
    /// </summary>
    public void ApplyStoryPoint(int index)
    {
        if (_save is null || index < 0 || index >= StoryPointKit.Chapters.Length) return;
        try
        {
            var chapter = StoryPointKit.Chapters[index];
            StoryPointKit.Apply(_save, chapter, s => Status = s);
            RebuildPictos();
            RebuildCharacters();
            RebuildWeapons();
            RebuildWorldFlags();
            Raise(nameof(ExplorationSummary));
            Raise(nameof(SpawnPointTag));
            Raise(nameof(SpawnOptions));
            Status = $"Rewound story to {chapter.Name} in memory. " +
                     "Save and exit to persist, or close without saving to discard.";
        }
        catch (Exception ex)
        {
            Status = $"Story rewind failed: {ex.Message}";
        }
    }

    // ───────────────────────── mutations (grid callbacks) ─────────────────────────

    public void ToggleFound(SavePictosRow row, bool found)
    {
        if (_save is null) return;
        try
        {
            SaveGameEditor.SetPictosFound(_save, row.Id, found);
            row.Found = found;
            if (!found)
            {
                row.Mastered = false;
                row.Level = 1;
            }
            Status = $"{row.DisplayName}: {(found ? "added to inventory" : "removed")}. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    public void ToggleMastered(SavePictosRow row, bool mastered)
    {
        if (_save is null) return;
        try
        {
            if (mastered && !row.Found) ToggleFound(row, true);
            SaveGameEditor.SetPictosMastered(_save, row.Id, mastered);
            row.Mastered = mastered;
            Status = $"{row.DisplayName}: {(mastered ? "mastered" : "unmastered")}. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    public void ChangeLevel(SavePictosRow row, int level)
    {
        if (_save is null) return;
        level = Math.Clamp(level, 1, 33);
        try
        {
            if (!row.Found) ToggleFound(row, true);
            SaveGameEditor.SetPictosLevel(_save, row.Id, level);
            row.Level = level;
            Status = $"{row.DisplayName}: level {level}. Press Save to persist.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }

    /// <summary>"Find all" always includes created pictos (✨): worst case the mod is
    /// not packed yet and they simply do nothing in-game.</summary>
    public void BulkFindAll()
    {
        foreach (var row in PictosRows.Where(r => !r.Found))
            ToggleFound(row, true);
        Status = "Marked every pictos as found. Press Save to persist.";
    }

    /// <summary>Adds every created pictos (✨) to the save, packed or not.</summary>
    public void BulkAddAllCreated()
    {
        var added = 0;
        foreach (var row in PictosRows.Where(r => r.IsCreated && !r.Found))
        {
            ToggleFound(row, true);
            added++;
        }
        Status = added > 0
            ? $"Added {added} created pictos. Bake them with the CLI/standalone build, then press Save to persist."
            : "All created pictos are already in the save.";
    }

    /// <summary>Adds every vanilla pictos to the inventory (created ✨ untouched,
    /// retired/cut rows excluded — scrub those per-row via the Found toggle).</summary>
    public void BulkAddAllVanilla()
    {
        var added = 0;
        foreach (var row in PictosRows.Where(r => !r.IsCreated && !r.NotSupported && !r.Found))
        {
            ToggleFound(row, true);
            added++;
        }
        Status = added > 0
            ? $"Added {added} vanilla pictos. Press Save and exit to persist."
            : "All vanilla pictos are already in the save.";
    }

    /// <summary>Masters every vanilla pictos, founding missing ones (created ✨
    /// and retired/cut rows untouched).</summary>
    public void BulkMasterVanilla()
    {
        var count = PictosRows.Count(r => !r.IsCreated && !r.NotSupported && !r.Mastered);
        foreach (var row in PictosRows.Where(r => !r.IsCreated && !r.NotSupported && !r.Mastered))
            ToggleMastered(row, true);
        Status = count > 0
            ? $"Mastered {count} vanilla pictos. Press Save and exit to persist."
            : "All vanilla pictos are already mastered.";
    }

    /// <summary>
    /// Masters created pictos (✨) only. Deliberately never bulk-touches
    /// vanilla rows: one misclick there silently masters hundreds of pictos,
    /// and the per-row Mastered checkbox covers selective vanilla edits.
    /// </summary>
    public void BulkMasterCreated()
    {
        var count = PictosRows.Count(r => r.IsCreated && !r.Mastered);
        foreach (var row in PictosRows.Where(r => r.IsCreated && !r.Mastered))
            ToggleMastered(row, true);
        Status = count > 0
            ? $"Mastered {count} created pictos. Press Save to persist."
            : "All created pictos are already mastered.";
    }

    /// <summary>Masters every pictos currently marked Found in the save
    /// (retired/cut rows excluded — master those individually if wanted).</summary>
    public void BulkMasterFound()
    {
        var count = PictosRows.Count(r => r.Found && !r.NotSupported && !r.Mastered);
        foreach (var row in PictosRows.Where(r => r.Found && !r.NotSupported && !r.Mastered))
            ToggleMastered(row, true);
        Status = count > 0
            ? $"Mastered {count} found pictos. Press Save to persist."
            : "All found pictos are already mastered.";
    }

    public void BulkSetLevel(int level)
    {
        foreach (var row in PictosRows.Where(r => r.Found && !r.NotSupported))
            ChangeLevel(row, level);
        Status = $"Set level {Math.Clamp(level, 1, 33)} on all found pictos. Press Save to persist.";
    }

    /// <summary>Unlocks every weapon in the catalog.</summary>
    public void BulkUnlockAllWeapons()
    {
        var added = 0;
        foreach (var row in Weapons.Where(r => !r.Owned))
        {
            ToggleOwned(row, true);
            added++;
        }
        Status = added > 0
            ? $"Unlocked {added} weapons. Press Save and exit to persist."
            : "All weapons are already unlocked.";
    }

    /// <summary>Sets level on every owned weapon (mirrors pictos' Set level on found).</summary>
    public void BulkSetWeaponLevel(int level)
    {
        foreach (var row in Weapons.Where(r => r.Owned))
            CommitWeaponLevel(row, level);
        Status = $"Set level {Math.Clamp(level, 1, WeaponLevelCap)} on all owned weapons. Press Save to persist.";
    }

    public void ApplyFilter() => Raise(nameof(VisibleRows));
}

/// <summary>Loose mirror of PictosCreateKit spec records for UI listing.</summary>
public sealed record PictosConversionSpec(string Id, string DisplayName);

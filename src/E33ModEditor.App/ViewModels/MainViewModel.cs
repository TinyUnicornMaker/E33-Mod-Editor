using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using E33ModEditor.Core;
using E33ModEditor.Core.Localization;
using E33ModEditor.Core.Loot;
using E33ModEditor.Core.Spoiler;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    public const string ModBase = "ExpeditionInfinity";
    public static string ModNameFor(int seed) => E33ModEditor.Core.Packer.FullPackKit.ModNameFor(seed);

    public const string TEnemies = "DT_jRPG_Enemies";
    public const string TItems = "DT_jRPG_Items_Composite";
    public const string TWeaponDef = "DT_WeaponDefinitions";
    public const string TWeaponScale = "DT_WeaponScalingTable";
    public const string TPassives = "DT_PassiveEffects";
    public const string TPictosDef = "DT_PictosDefinitions";
    public const string TPictosScale = "DT_EquipmentScalingTable_Pictos";
    public const string TLuminaBal = "DT_Balancing_Luminas";
    public const string TSkillBal = "DT_Balancing_MainCharacterSkills";
    public const string TEncDungeon = "DT_jRPG_Encounters";
    public const string TEncTower = "DT_jRPG_Encounters_CleaTower";
    public const string TEncWorld = "DT_WorldMap_Encounters";
    public const string TMusic = "DT_MusicRecords";
    public const string TJournals = "DT_Items_Journals";
    public const string TChests = "DT_ChestsContent";

    private readonly Dictionary<string, DataTableEditor> _editors = new();
    private readonly Dictionary<string, string> _assetPaths = new();
    private readonly Dictionary<string, List<RecordRow>> _rowsByKey = new();
    private readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _passivesByCharacter = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _allWeaponPassives = new();
    private readonly Dictionary<string, string> _longestCache = new();

    public IReadOnlyDictionary<string, HashSet<string>> PassivesByCharacter => _passivesByCharacter;
    public IReadOnlyList<string> AllWeaponPassives => _allWeaponPassives;

    public ObservableCollection<ModuleVM> Modules { get; } = new();
    public ObservableCollection<RecordRow> Rows { get; } = new();

    /// <summary>Language baked into the next Apply &amp; Pack (en/fr/pl/ru).</summary>
    public ObservableCollection<ModLocalization.Language> PackLanguages { get; } = new(ModLocalization.Supported);

    private ModLocalization.Language _selectedPackLanguage = ModLocalization.English;
    public ModLocalization.Language SelectedPackLanguage
    {
        get => _selectedPackLanguage;
        set
        {
            if (value is null || !Set(ref _selectedPackLanguage, value)) return;
            LocalizationKit.SaveLanguage(value.Code);
            Raise(nameof(PackLanguageHint));
        }
    }

    public string PackLanguageHint => SelectedPackLanguage.Code == "en"
        ? "English source text (no locres override)"
        : $"Bakes a translated {SelectedPackLanguage.Culture}/Game.locres into the pack";

    private ModuleVM? _activeHub;
    public ModuleVM? ActiveHub
    {
        get => _activeHub;
        set
        {
            if (Set(ref _activeHub, value))
            {
                Raise(nameof(VisibleTiles));
                Raise(nameof(IsLauncher));
                Raise(nameof(IsHubOpen));
                Raise(nameof(LauncherTitle));
            }
        }
    }

    private ModuleVM? _activeModule;
    public ModuleVM? ActiveModule
    {
        get => _activeModule;
        set
        {
            if (Set(ref _activeModule, value))
            {
                Raise(nameof(ActiveTitle));
                Raise(nameof(IsEditing));
                Raise(nameof(IsLauncher));
            }
        }
    }

    private string _activeKey = "";
    public string ActiveTable
    {
        get => _activeKey;
        set { if (Set(ref _activeKey, value)) RefreshView(); }
    }

    public IEnumerable<ModuleVM> VisibleTiles => ActiveHub?.Children.Length > 0 ? ActiveHub.Children : Modules;

    public bool IsEditing => ActiveModule is not null;
    public bool IsLauncher => ActiveModule is null;
    public bool IsHubOpen => ActiveHub is not null;

    public string LauncherTitle => ActiveHub is null ? "Expedition 33 Editor" : ActiveHub.Name;

    private bool _painterConversionEnabled;
    public bool PainterConversionEnabled
    {
        get => _painterConversionEnabled;
        set
        {
            if (!Set(ref _painterConversionEnabled, value)) return;
            SaveCreateToggles();
            Status = value
                ? "Painter Conversion Mods will be added on the next CLI/standalone build."
                : "Painter Conversion Mods will be omitted from the next pack.";
        }
    }

    public GameplayTweaksKit.Settings GameplayTweaks { get; } = new();
    /// <summary>Third-party mod compatibility (Gameplay Tweaks ▸ Compatibility).</summary>
    public CompatibilityKit.Settings Compatibility { get; } = new();
    /// <summary>Camera submodule (packed into ExpeditionInfinity_P, not a separate pack).</summary>
    public FirstPersonCameraKit.Settings Camera { get; } = new();
    /// <summary>Party of Five submodule (packed into ExpeditionInfinity_P).</summary>
    public PartyFiveKit.Settings PartyFive { get; } = new();
    /// <summary>Per-character move-change toggles (Skill Overhaul module).</summary>
    public SkillOverhaulKit.Settings SkillOverhaul { get; } = new();
    /// <summary>Per-character skill-graph arrangements (Graph Editor module).</summary>
    public SkillGraphKit.Settings SkillGraph { get; } = new();

    private bool _statusAffinityEnabled;
    public bool StatusAffinityEnabled
    {
        get => _statusAffinityEnabled;
        set
        {
            if (!Set(ref _statusAffinityEnabled, value)) return;
            SaveCreateToggles();
            Status = value
                ? "Status Affinity Mods will be added on the next CLI/standalone build (Inverted Affinity becomes 10%)."
                : "Status Affinity Mods will be omitted from the next pack. Inverted Affinity returns to 50%.";
        }
    }

    private bool _pictosMagnitudeEnabled;
    public bool PictosMagnitudeEnabled
    {
        get => _pictosMagnitudeEnabled;
        set
        {
            if (!Set(ref _pictosMagnitudeEnabled, value)) return;
            SaveCreateToggles();
            Status = value
                ? "Pictos magnitude variants will be added on the next CLI/standalone build."
                : "Pictos magnitude variants will be omitted from the next pack.";
        }
    }

    private bool _damageLimitEnabled;
    public bool DamageLimitEnabled
    {
        get => _damageLimitEnabled;
        set
        {
            if (!Set(ref _damageLimitEnabled, value)) return;
            SaveCreateToggles();
            Status = value
                ? "Damage Limit Mods (Painted 999999 / Ultima unlimited) will be added on the next CLI/standalone build."
                : "Damage Limit Mods will be omitted from the next pack. Painted Power returns to unlimited.";
        }
    }

    private bool _weaponEffectsEnabled;
    public bool WeaponEffectsEnabled
    {
        get => _weaponEffectsEnabled;
        set
        {
            if (!Set(ref _weaponEffectsEnabled, value)) return;
            SaveCreateToggles();
            Status = value
                ? "Created weapon effects will be added on the next CLI/standalone build."
                : "Created weapon effects will be omitted from the next pack.";
        }
    }

    public string ActiveTitle => ActiveModule is null ? "Expedition 33 Editor" : ActiveModule.Name;

    private string _status = "Ready.";
    public string Status { get => _status; set { _status = value; Raise(); } }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (!Set(ref _isBusy, value)) return;
            Raise(nameof(CanPack));
            RaiseCommands();
        }
    }

    private bool _isLoading = true;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (!Set(ref _isLoading, value)) return;
            Raise(nameof(IsLauncher));
            Raise(nameof(CanPack));
            RaiseCommands();
        }
    }

    /// <summary>Save / Load stay disabled until preload finishes.</summary>
    public bool CanPack => !IsLoading && !IsBusy;

    private void RaiseCommands()
    {
        (SaveProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LoadProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private double _loadProgress;
    public double LoadProgress { get => _loadProgress; set => Set(ref _loadProgress, value); }

    private string _loadMessage = "Starting…";
    public string LoadMessage { get => _loadMessage; set => Set(ref _loadMessage, value); }

    private RecordRow? _selectedRow;
    public RecordRow? SelectedRow { get => _selectedRow; set => Set(ref _selectedRow, value); }

    private string _filterText = "";
    public string FilterText { get => _filterText; set { if (Set(ref _filterText, value)) RefreshView(); } }

    public ICommand SaveProjectCommand { get; }
    public ICommand LoadProjectCommand { get; }
    public ICommand OpenModuleCommand { get; }
    public ICommand GoHomeCommand { get; }
    public ICommand GoBackCommand { get; }

    /// <summary>Filled by the window so Save/Load can show a file picker.</summary>
    public Func<Task<string?>>? PickSaveProjectPath { get; set; }
    public Func<Task<string?>>? PickLoadProjectPath { get; set; }

    public MainViewModel()
    {
        _selectedPackLanguage = ModLocalization.Find(LocalizationKit.LoadLanguage()) ?? ModLocalization.English;
        SaveProjectCommand = new RelayCommand(() => SaveProjectPickedAsync(), () => CanPack);
        LoadProjectCommand = new RelayCommand(() => LoadProjectPickedAsync(), () => CanPack);
        OpenModuleCommand = new RelayCommand(p => OpenTileAsync(p as ModuleVM));
        GoHomeCommand = new RelayCommand(() => { GoHome(); return Task.CompletedTask; });
        GoBackCommand = new RelayCommand(() => { GoBack(); return Task.CompletedTask; });

        Modules.Add(new ModuleVM
        {
            Id = "monsters",
            Name = "Monsters",
            Description = "Enemy stats, scaling, loot & combat data",
            Glyph = "⚔",
            Tables = new[] { TEnemies },
        });
        var weaponsList = new ModuleVM
        {
            Id = "weapons",
            Name = "Weapons",
            Description = "Passives, damage types and attribute scaling",
            Glyph = "🗡",
            Tables = new[] { TWeaponDef, TItems },
        };
        var weaponsScale = new ModuleVM
        {
            Id = "weapon-scaling",
            Name = "Weapons Scaling",
            Description = "Shared level 1–33 attack and chroma curve",
            Glyph = "∿",
            IsDirectEditor = true,
            Tables = new[] { TWeaponScale },
        };
        var weaponsCreate = new ModuleVM
        {
            Id = "weapon-effects-create",
            Name = "Create new weapon effects",
            Description = "Clone existing lumina graphs with their own balancing keys",
            Glyph = "+",
            IsDirectEditor = true,
            Tables = Array.Empty<string>(),
        };
        Modules.Add(new ModuleVM
        {
            Id = "weapons-hub",
            Name = "Weapons",
            Description = "Individual weapons, the shared curve, and created effects",
            Glyph = "🗡",
            IsHub = true,
            Children = new[] { weaponsList, weaponsScale, weaponsCreate },
        });
        var pictosList = new ModuleVM
        {
            Id = "pictos",
            Name = "Pictos",
            Description = "Stat multipliers, lumina numbers, cost and categories",
            Glyph = "✶",
            Tables = new[] { TItems, TPassives, TPictosDef, TLuminaBal },
        };
        var pictosScale = new ModuleVM
        {
            Id = "pictos-scaling",
            Name = "Pictos Scaling",
            Description = "Shared level 1–33 HP, defense, speed, crit and chroma curve",
            Glyph = "∿",
            IsDirectEditor = true,
            Tables = new[] { TPictosScale },
        };
        var pictosCreate = new ModuleVM
        {
            Id = "pictos-create",
            Name = "Create new pictos",
            Description = "Add or remove generated pictos in the packed mod",
            Glyph = "+",
            IsDirectEditor = true,
            Tables = Array.Empty<string>(),
        };
        Modules.Add(new ModuleVM
        {
            Id = "pictos-hub",
            Name = "Pictos",
            Description = "Individual pictos, the shared curve, and created pictos",
            Glyph = "✶",
            IsHub = true,
            Children = new[] { pictosList, pictosScale, pictosCreate },
        });
        var skillsList = new ModuleVM
        {
            Id = "skills",
            Name = "Skills",
            Description = "Raw skill values — AP, targeting, damage and status numbers",
            Glyph = "✦",
            Tables = Array.Empty<string>(),
        };
        var skillOverhaul = new ModuleVM
        {
            Id = "skill-overhaul",
            Name = "Skill Overhaul",
            Description = "Enable or disable move changes per character",
            Glyph = "★",
            IsDirectEditor = true,
            Tables = Array.Empty<string>(),
        };
        var skillGraph = new ModuleVM
        {
            Id = "skill-graph-editor",
            Name = "Graph Editor",
            Description = "Drag moves on each character's skill grid, rewire links, save layouts",
            Glyph = "◈",
            IsDirectEditor = true,
            Tables = Array.Empty<string>(),
        };
        Modules.Add(new ModuleVM
        {
            Id = "skills-hub",
            Name = "Skills",
            Description = "Raw skill values and the per-character move overhaul",
            Glyph = "✦",
            IsHub = true,
            Children = new[] { skillsList, skillOverhaul, skillGraph },
        });

        var dungeon = new ModuleVM
        {
            Id = "enc-dungeon",
            Name = "Dungeon",
            Description = "Level encounters across the continent",
            Glyph = "🕳",
            Tables = new[] { TEncDungeon },
        };
        var tower = new ModuleVM
        {
            Id = "enc-tower",
            Name = "Tower",
            Description = "Clea's Tower floor battles",
            Glyph = "🏛",
            Tables = new[] { TEncTower },
        };
        var world = new ModuleVM
        {
            Id = "enc-world",
            Name = "World Map",
            Description = "Overworld and overworld-alpha fights",
            Glyph = "🗺",
            Tables = new[] { TEncWorld },
        };
        Modules.Add(new ModuleVM
        {
            Id = "encounters",
            Name = "Encounters",
            Description = "Dungeon, Tower and World Map battles",
            Glyph = "☰",
            IsHub = true,
            Children = new[] { dungeon, tower, world },
        });
        Modules.Add(new ModuleVM
        {
            Id = "chests-drops",
            Name = "Chests / Drops",
            Description = "Map every chest, enemy drop and shop, with tiers and frequencies",
            Glyph = "▣",
            IsDirectEditor = true,
            Tables = new[] { TChests, TEnemies },
        });
        var randoSettings = new ModuleVM
        {
            Id = "randomizer",
            Name = "Randomizer",
            Description = "Tiered chests, drops and shops, plus monster and weapon stat rolls",
            Glyph = "⚄",
            IsDirectEditor = true,
            Tables = Array.Empty<string>(),
        };
        var tierEditor = new ModuleVM
        {
            Id = "tier-editor",
            Name = "Tier Editor",
            Description = "Pictos and weapon T1–T5 lists the randomizer draws from",
            Glyph = "▤",
            IsDirectEditor = true,
            Tables = Array.Empty<string>(),
        };
        Modules.Add(new ModuleVM
        {
            Id = "randomizer-hub",
            Name = "Randomizer",
            Description = "Loot and stat rolls plus the pictos / weapon / effect tier lists",
            Glyph = "⚄",
            IsHub = true,
            Children = new[] { randoSettings, tierEditor },
        });
        var gameplayValues = new ModuleVM
        {
            Id = "gameplay-values",
            Name = "Gameplay Values",
            Description = "Level cap, stat growth, attribute cap, AP, enemy buffs, lumina drops, permadeath",
            Glyph = "⚖",
            IsDirectEditor = true,
            Tables = Array.Empty<string>(),
        };
        var compatibility = new ModuleVM
        {
            Id = "gameplay-compat",
            Name = "Compatibility",
            Description = "Patch our values into the Bigger Party Mod instead of shadowing it",
            Glyph = "🔗",
            IsDirectEditor = true,
            Tables = Array.Empty<string>(),
        };
        Modules.Add(new ModuleVM
        {
            Id = "gameplay-tweaks-hub",
            Name = "Gameplay Tweaks",
            Description = "Gameplay values and third-party mod compatibility",
            Glyph = "⚖",
            IsHub = true,
            Children = new[] { gameplayValues, compatibility },
        });
        Modules.Add(new ModuleVM
        {
            Id = "save-editor",
            Name = "Save Editor",
            Description = "Edit EXPEDITION_*.sav directly — pictos (incl. created), with backups",
            Glyph = "❖",
            IsDirectEditor = true,
            Tables = Array.Empty<string>(),
        });
        Modules.Add(new ModuleVM
        {
            Id = "camera",
            Name = "Camera",
            Description = "Close third-person / first-person camera, packed into ExpeditionInfinity_P",
            Glyph = "🎥",
            IsDirectEditor = true,
            Tables = Array.Empty<string>(),
        });
        Modules.Add(new ModuleVM
        {
            Id = "party-five",
            Name = "Party of Five",
            Description = "5-member battle party with a two-row formation, packed into ExpeditionInfinity_P",
            Glyph = "❹",
            IsDirectEditor = true,
            Tables = Array.Empty<string>(),
        });

        foreach (var m in Modules.Concat(Modules.SelectMany(x => x.Children)))
            m.OpenCommand = OpenModuleCommand;

        SaveEditor.VanillaPictosCatalog = () => _rowsByKey.TryGetValue("pictos", out var rows)
            ? rows.Select(r => (r.RowName, r.DisplayName))
            : Enumerable.Empty<(string Id, string DisplayName)>();
        SaveEditor.PictosDescription = id => DescribePassive(id).Description;
        Tiers.Describe = DescribePassive;
        SaveEditor.PainterConversionEnabled = () => PainterConversionEnabled;
        SaveEditor.StatusAffinityEnabled = () => StatusAffinityEnabled;
        SaveEditor.DamageLimitEnabled = () => DamageLimitEnabled;
        SaveEditor.LevelCapProvider = () => GameplayTweaks.LevelCap;
        SaveEditor.WeaponLevelCapProvider = () =>
        {
            var s = WeaponDropLevelKit.Settings.Load();
            return s.Enabled ? WeaponDropLevelKit.EffectiveMax(s) : WeaponOverLevelKit.VanillaMaxLevel;
        };

        LoadCreateToggles();
        LoadGameplayTweaks();
        LoadCompatibility();
        LoadCamera();
        LoadPartyFive();
        LoadRandomizer();
        LoadSkillOverhaul();
        LoadSkillGraph();
        Randomizer.Persist = SaveRandomizer;
        Loot.ApplySlot = ApplyLootSlot;
        Tiers.Attach(Loot.Catalog);
    }

    /// <summary>Direct .sav editing (GVAS) module.</summary>
    public SaveEditorViewModel SaveEditor { get; } = new();

    /// <summary>Vanilla chests, enemy drops and merchants, mapped to areas/tiers.</summary>
    public LootViewModel Loot { get; } = new();

    /// <summary>Randomizer settings written on the next CLI/standalone build.</summary>
    public RandomizerViewModel Randomizer { get; } = new();

    /// <summary>Shippable pictos / weapon T1–T5 lists.</summary>
    public TierEditorViewModel Tiers { get; } = new();

    // ─────────────────────── column specs ───────────────────────

    private static readonly Dictionary<string, ColumnSpec[]> ColumnSpecs = new()
    {
        ["monsters"] = new[]
        {
            new ColumnSpec { Header = "Lvl", Path = "Level", Numeric = true },
            new ColumnSpec { Header = "Boss", Path = "IsBoss" },
            new ColumnSpec { Header = "HP ×", Path = "EnemyScaling.HP", Numeric = true },
            new ColumnSpec { Header = "XP ×", Path = "EnemyScaling.Experience", Numeric = true },
            new ColumnSpec { Header = "Spd ×", Path = "EnemyScaling.Speed", Numeric = true },
            // Effective in-game values: archetype(level) x scaling. The raw
            // EnemyStats table carries placeholders (HP 1 / ATK 0) for 154
            // late-game rows, so showing it directly reads as 0 in-game-high.
            // Raw table stays editable under Raw Stats.
            new ColumnSpec { Header = "Eff. HP", Path = EnemyEffectiveStats.EffHpPath, Numeric = true },
            new ColumnSpec { Header = "Eff. Atk", Path = EnemyEffectiveStats.EffAttackPath, Numeric = true },
            new ColumnSpec { Header = "Loot ×", Path = "LootChanceMultiplier", Numeric = true },
        },
        ["weapons"] = new[]
        {
            new ColumnSpec { Header = "Character", Path = "_Character" },
            new ColumnSpec { Header = "Base Stat ×", Path = "BaseStatsMultipliers.E_jRPG_StatType::NewEnumerator3", Numeric = true },
            new ColumnSpec { Header = "Chroma ×", Path = "ChromaValueMultiplier", Numeric = true },
            new ColumnSpec { Header = "Damage", Path = "BaseDamageType" },
            new ColumnSpec { Header = "Free Aim", Path = "FreeAimDamageType" },
            new ColumnSpec { Header = "Scaling", Path = "AttributeScaling" },
            new ColumnSpec { Header = "Passive Lvl 33", Path = "Level1Lumina" },
            new ColumnSpec { Header = "Passive Lvl 66", Path = "Level2Lumina" },
            new ColumnSpec { Header = "Passive Lvl 111", Path = "Level3Lumina" },
            new ColumnSpec { Header = "Passive Lvl 222", Path = "Level4Lumina" },
            new ColumnSpec { Header = "Passive Lvl 333", Path = "Level5Lumina" },
        },
        ["pictos"] = new[]
        {
            new ColumnSpec { Header = "Lumina", Path = "LuminaCost", Numeric = true },
            new ColumnSpec { Header = "HP ×", Path = PictosCurve.HpMult, Numeric = true },
            new ColumnSpec { Header = "Def ×", Path = PictosCurve.DefMult, Numeric = true },
            new ColumnSpec { Header = "Speed ×", Path = PictosCurve.SpeedMult, Numeric = true },
            new ColumnSpec { Header = "Crit ×", Path = PictosCurve.CritMult, Numeric = true },
            new ColumnSpec { Header = "Chroma ×", Path = PictosCurve.ChromaMult, Numeric = true },
            new ColumnSpec { Header = "Buy", Path = "Item_BuyValue", Numeric = true },
        },
        ["skills"] = new[]
        {
            new ColumnSpec { Header = "Character", Path = "_Character" },
            new ColumnSpec { Header = "AP", Path = "APCost", Numeric = true },
            new ColumnSpec { Header = "Power", Path = "BaseDamage", Numeric = true },
            new ColumnSpec { Header = "Type", Path = "SkillType" },
            new ColumnSpec { Header = "Targeting", Path = "TargetingType" },
            new ColumnSpec { Header = "Element", Path = "BaseElementalType" },
            new ColumnSpec { Header = "Unlock", Path = "UnlockCost", Numeric = true },
            new ColumnSpec { Header = "Starting", Path = "IsStartingSkill" },
        },
        ["enc-dungeon"] = EncounterCols(),
        ["enc-tower"] = EncounterCols(),
        ["enc-world"] = EncounterCols(),
    };

    private static ColumnSpec[] EncounterCols() => new[]
    {
        new ColumnSpec { Header = "Enemies", Path = "Enemies" },
        new ColumnSpec { Header = "Lvl Override", Path = "EncounterLevelOverride", Numeric = true },
        new ColumnSpec { Header = "Flee", Path = "FleeImpossible" },
        new ColumnSpec { Header = "Retryable", Path = "IsRetryableBattle" },
        new ColumnSpec { Header = "Narrative", Path = "IsNarrativeBattle" },
    };

    // ─────────────────────── navigation ──────────────────────────

    public void GoHome()
    {
        ActiveModule = null;
        ActiveHub = null;
        ActiveTable = "";
        SelectedRow = null;
        Rows.Clear();
        Status = "Ready.";
    }

    public void GoBack()
    {
        if (ActiveModule is not null && ActiveHub is not null)
        {
            ActiveModule = null;
            ActiveTable = "";
            SelectedRow = null;
            Rows.Clear();
            return;
        }
        GoHome();
    }

    public Task OpenTileAsync(ModuleVM? module)
    {
        if (module is null || !module.IsAvailable) return Task.CompletedTask;
        if (module.IsHub)
        {
            ActiveModule = null;
            ActiveHub = module;
            ActiveTable = "";
            Rows.Clear();
            Status = module.Description;
            return Task.CompletedTask;
        }

        // The save editor scans for saves as soon as it opens.
        if (module.Id == "save-editor")
            _ = SaveEditor.EnsureSavesLoadedAsync();
        if (module.Id == "chests-drops")
            _ = EnsureMerchantsAndRefreshLootAsync();
        // The graph editor is a direct editor; it renders its own canvases and
        // never needs a record-grid. Do not RefreshView() (would clear Rows).
        if (module.Id == "skill-graph-editor")
        {
            ActiveModule = module;
            Status = "Graph Editor: drag moves to snap points, right-click to rewire links.";
            return Task.CompletedTask;
        }

        ActiveTable = module.Id;
        RefreshView();
        ActiveModule = module;
        var n = _rowsByKey.TryGetValue(module.Id, out var list) ? list.Count : 0;
        Status = n > 0 ? $"{module.Name}: {n} record(s)." : $"{module.Name}: no records loaded.";
        return Task.CompletedTask;
    }

    // ─────────────────────── preload ──────────────────────────

    public async Task PreloadAllAsync()
    {
        IsLoading = true;
        IsBusy = true;
        var steps = new (string Message, Func<Task> Act)[]
        {
            ("Loading type mappings…", () => Task.Run(() => _ = DataTableEditor.Mappings)),
            ("Extracting enemy data…", () => EnsureTableAsync(TEnemies)),
            ("Loading monsters…", () => Task.Run(() => LoadTable(TEnemies))),
            ("Extracting item composite…", () => EnsureTableAsync(TItems)),
            ("Loading items…", () => Task.Run(() => LoadTable(TItems))),
            ("Extracting weapon definitions…", () => EnsureTableAsync(TWeaponDef)),
            ("Loading weapon definitions…", () => Task.Run(() => LoadTable(TWeaponDef))),
            ("Extracting weapon scaling curve…", () => EnsureTableAsync(TWeaponScale)),
            ("Loading weapon scaling…", () => Task.Run(() => LoadTable(TWeaponScale))),
            ("Extracting lumina / pictos passives…", () => EnsureTableAsync(TPassives)),
            ("Loading pictos passives…", () => Task.Run(() => LoadTable(TPassives))),
            ("Extracting pictos definitions…", () => EnsureTableAsync(TPictosDef)),
            ("Loading pictos definitions…", () => Task.Run(() => LoadTable(TPictosDef))),
            ("Extracting pictos scaling curve…", () => EnsureTableAsync(TPictosScale)),
            ("Loading pictos scaling…", () => Task.Run(() => LoadTable(TPictosScale))),
            ("Extracting lumina balancing values…", () => EnsureTableAsync(TLuminaBal)),
            ("Loading lumina balancing…", () => Task.Run(() => LoadTable(TLuminaBal))),
            ("Extracting skill balancing values…", () => EnsureTableAsync(TSkillBal)),
            ("Loading skill balancing…", () => Task.Run(() => LoadTable(TSkillBal))),
            ("Extracting dungeon encounters…", () => EnsureTableAsync(TEncDungeon)),
            ("Loading dungeon encounters…", () => Task.Run(() => LoadTable(TEncDungeon))),
            ("Extracting tower encounters…", () => EnsureTableAsync(TEncTower)),
            ("Loading tower encounters…", () => Task.Run(() => LoadTable(TEncTower))),
            ("Extracting world map encounters…", () => EnsureTableAsync(TEncWorld)),
            ("Loading world map encounters…", () => Task.Run(() => LoadTable(TEncWorld))),
            ("Loading string tables…", () => Task.Run(LoadStringTables)),
            ("Warming enum display names…", () => Task.Run(WarmEnums)),
            ("Extracting skill data assets…", () => EnsureFilterAsync("DA_Skill_", SkillsDir, "DA_Skill_*.uasset", 50)),
            ("Extracting skill trees…", () => EnsureFilterAsync("DA_SkillGraph", GraphsDir, "DA_SkillGraph*.uasset", 5)),
            ("Loading skills…", () => Task.Run(LoadSkills)),
            ("Grouping weapons…", () => Task.Run(() => { BuildWeapons(); BuildWeaponScaling(); })),
            ("Grouping pictos…", () => Task.Run(() => { BuildPictos(); BuildPictosScaling(); })),
            ("Building encounter lists…", () => Task.Run(BuildEncounters)),
            ("Building monster list…", () => Task.Run(BuildMonsters)),
            ("Extracting music records…", () => EnsureTableAsync(TMusic)),
            ("Loading music records…", () => Task.Run(() => LoadTable(TMusic))),
            ("Extracting journal items…", () => EnsureTableAsync(TJournals)),
            ("Loading journal items…", () => Task.Run(() => LoadTable(TJournals))),
            ("Extracting chests…", () => EnsureTableAsync(TChests)),
            ("Loading chests…", () => Task.Run(() => LoadTable(TChests))),
            ("Building save editor catalogs…", () => Task.Run(BuildSaveCatalogs)),
            ("Mapping chests and drops…", () => Task.Run(BuildLootCatalog)),
        };

        var failures = new List<string>();
        for (int i = 0; i < steps.Length; i++)
        {
            LoadProgress = (i * 100.0) / steps.Length;
            LoadMessage = steps[i].Message;
            Status = steps[i].Message;
            try { await steps[i].Act(); }
            catch (Exception ex)
            {
                failures.Add($"{steps[i].Message}: {ex.Message}");
            }
        }

        LoadProgress = 100;
        LoadMessage = failures.Count == 0 ? "Ready." : "Loaded with warnings.";
        var restored = TryRestoreAutosave();
        IsLoading = false;
        IsBusy = false;
        Status = failures.Count == 0
            ? (restored
                ? "Restored the last working project. Pick a tile, then use the CLI/standalone build to write Export/ExpeditionInfinity_<seed>_P."
                : "All editors preloaded. Pick a tile to begin.")
            : "Preload finished with issues: " + string.Join(" | ", failures.Take(4));
    }

    private void BuildSaveCatalogs()
    {
        FillCatalog(TWeaponDef, SaveEditor.WeaponCatalog);
        FillCatalog(TMusic, SaveEditor.MusicCatalog);
        FillCatalog(TJournals, SaveEditor.JournalCatalog);
        SaveEditor.WeaponOwners.Clear();
        if (_rowsByKey.TryGetValue("weapons", out var weapons))
            foreach (var r in weapons)
                SaveEditor.WeaponOwners[r.RowName] = r.Baseline["_Character"]?.ToString() ?? "";
    }

    private void FillCatalog(string table, Dictionary<string, string> target)
    {
        target.Clear();
        if (!_rowsByKey.TryGetValue(table, out var rows)) return;
        foreach (var r in rows)
        {
            var n = LookupName(r.Baseline, "Item_DisplayName", "Name");
            target[r.RowName] = n.Length > 0 ? n : r.RowName;
        }
    }

    private void BuildLootCatalog()
    {
        JObject ChestsDump() =>
            _editors.TryGetValue(TChests, out var ed) ? JObject.Parse(ed.Dump()) : new JObject();
        JObject EnemiesDump() =>
            _editors.TryGetValue(TEnemies, out var ed) ? JObject.Parse(ed.Dump()) : new JObject();
        Loot.Rebuild(ChestsDump(), EnemiesDump(), LoadMerchantDumps());
        Tiers.Attach(Loot.Catalog);
    }

    private bool ApplyLootSlot(LootCatalog.Check check, LootCatalog.Slot slot, string newId)
    {
        if (string.IsNullOrEmpty(slot.Path) || string.IsNullOrEmpty(slot.RowName))
            return false;
        string? path = null;
        if (slot.Table is TChests or TEnemies)
            _assetPaths.TryGetValue(slot.Table, out path);
        else if (!string.IsNullOrEmpty(check.MerchantPath))
            path = check.MerchantPath;
        else if (!string.IsNullOrEmpty(slot.Table))
            path = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), slot.Table + ".uasset");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;
        try
        {
            DataTableEditor ed;
            if ((slot.Table is TChests or TEnemies) && _editors.TryGetValue(slot.Table, out var live))
                ed = live;
            else
                ed = new DataTableEditor(path);
            var json = new JObject { [slot.RowName] = new JObject { [slot.Path] = newId } };
            ed.Apply(json.ToString());
            ed.Save(path);
            Status = $"Drop set to {newId}. The CLI/standalone build writes it into Export/{ModBase}_<seed>_P.";
            return true;
        }
        catch (Exception ex)
        {
            Status = "Could not write drop: " + ex.Message;
            return false;
        }
    }

    private static List<(string File, JObject Rows)> LoadMerchantDumps()
    {
        var list = new List<(string, JObject)>();
        var root = Path.Combine(EditorPaths.RepoRoot, "work", "tables");
        if (!Directory.Exists(root)) return list;
        foreach (var file in Directory.EnumerateFiles(root, "DT_Merchant_*.uasset", SearchOption.AllDirectories))
        {
            try
            {
                var ed = new DataTableEditor(file);
                list.Add((file, JObject.Parse(ed.Dump())));
            }
            catch { /* skip a broken extract */ }
        }
        return list;
    }

    private async Task EnsureMerchantsAndRefreshLootAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                var root = Path.Combine(EditorPaths.RepoRoot, "work", "tables");
                var have = Directory.Exists(root)
                    ? Directory.EnumerateFiles(root, "DT_Merchant_*.uasset", SearchOption.AllDirectories).Take(8).Count()
                    : 0;
                if (have < 8)
                {
                    var dir = Path.Combine(root, "DT_Merchant_");
                    Directory.CreateDirectory(dir);
                    new Retoc().ToLegacy(dir, "DT_Merchant_");
                }
                BuildLootCatalog();
            });
            Status = Loot.Summary;
        }
        catch (Exception e)
        {
            Status = "Merchant extract: " + e.Message;
        }
    }

    private static string LegacyDir(string table) =>
        Path.Combine(EditorPaths.RepoRoot, "work", "tables", table);

    private static string SkillsDir => Path.Combine(EditorPaths.RepoRoot, "work", "tables", "skills");
    private static string GraphsDir => Path.Combine(EditorPaths.RepoRoot, "work", "tables", "graphs");

    private static bool HasAsset(string dir, string fileName) =>
        Directory.Exists(dir) &&
        Directory.EnumerateFiles(dir, fileName, SearchOption.AllDirectories).Any();

    private static Task EnsureTableAsync(string table) => Task.Run(() =>
    {
        var dir = LegacyDir(table);
        if (HasAsset(dir, table + ".uasset")) return;
        new Retoc().ToLegacy(dir, table);
    });

    private static Task EnsureFilterAsync(string filter, string dir, string pattern, int minCount) => Task.Run(() =>
    {
        var have = Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories).Take(minCount).Count()
            : 0;
        if (have >= minCount) return;
        new Retoc().ToLegacy(dir, filter);
    });

    private void LoadTable(string table)
    {
        if (_editors.ContainsKey(table)) return;
        var dir = LegacyDir(table);
        var assetPath = Directory.EnumerateFiles(dir, table + ".uasset", SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new FileNotFoundException($"{table}.uasset missing after conversion.");
        var ed = new DataTableEditor(assetPath);
        var list = ed.Table.Table.Data
            .Select(r =>
            {
                var name = DataTableEditor.StripSuffix(r.Name.ToString());
                var row = new RecordRow
                {
                    RowName = name,
                    DisplayName = name,
                    PrimaryTable = table,
                    Baseline = ed.DumpRow(r),
                };
                row.Fields[""] = new FieldRef(table, name, "");
                return row;
            })
            .ToList();
        _editors[table] = ed;
        _assetPaths[table] = assetPath;
        _rowsByKey[table] = list;
    }

    private void LoadStringTables()
    {
        void TryLoad(string fileStem)
        {
            // Prefer the staged mod copy (kit-added rows/keys only exist
            // there), then vanilla tables, then legacy work copies.
            var file = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "mod"), fileStem + ".uasset")
                ?? FileCopy.FindTablesAsset(fileStem + ".uasset")
                ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work"), fileStem + ".uasset");
            if (file is null)
            {
                try
                {
                    var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "strings");
                    new Retoc().ToLegacy(dir, fileStem);
                    file = Directory.EnumerateFiles(dir, fileStem + ".uasset", SearchOption.AllDirectories)
                        .FirstOrDefault();
                }
                catch { /* optional */ }
            }
            if (file is null) return;
            foreach (var (k, v) in DataTableEditor.OpenStringTable(file))
                if (!string.IsNullOrEmpty(k)) _strings[k] = v;
        }

        TryLoad("ST_Items");
        TryLoad("ST_PassiveEffects");
        TryLoad("ST_MainCharacters_Skills");
        TryLoad("ST_Enemies_Name");
    }

    private void WarmEnums()
    {
        foreach (var ed in _editors.Values)
        {
            ed.PrimeEnumPaths();
            foreach (var et in ed.EnumTypesByPath.Values.Distinct())
                DataTableEditor.EnumDisplayNames(et);
        }
        foreach (var type in new[]
                 {
                     "E_jRPG_StatType", "E_jRPG_ItemType", "E_jRPG_ItemSubtype", "E_jRPG_ItemRarity",
                     "ECharacterAttribute", "EAttributeScalingGrade", "EAttackType",
                     "ESkillType", "ESkillElementOverride", "E_jRPG_TargetingType",
                     "E_PassiveEffectCategory",
                 })
            DataTableEditor.EnumDisplayNames(type);
    }

    private string LookupName(JObject baseline, params string[] fields)
    {
        foreach (var f in fields)
        {
            var tok = TokenAt(baseline, f)?.ToString();
            var key = GameLabels.StringTableKey(tok);
            if (key != null && _strings.TryGetValue(key, out var n) && n.Length > 0) return n;
        }
        return "";
    }

    /// <summary>Resolves a flattened string-table reference ("[... ] KEY") to its text.</summary>
    public string StringForKey(string flattened)
    {
        var key = GameLabels.StringTableKey(flattened);
        if (key is not null && _strings.TryGetValue(key, out var n) && n.Length > 0) return n;
        return "";
    }

    // ─────────────────────── builders ──────────────────────────

    private void BuildMonsters()
    {
        if (!_rowsByKey.TryGetValue(TEnemies, out var src)) return;
        foreach (var row in src)
        {
            var pretty = LookupName(row.Baseline, "EnemyDisplayName");
            if (pretty.Length > 0) row.DisplayName = pretty;
            RefreshEffectiveStats(row);
        }
        _rowsByKey["monsters"] = src;
    }

    /// <summary>
    /// Recomputes a monster row's effective HP/attack/speed
    /// (<c>archetype(level) x scaling</c>) into synthetic baseline tokens so
    /// the grid shows in-game values instead of raw-table placeholders.
    /// No-op for non-enemy rows (weapons/pictos must never gain these keys,
    /// or they would leak into pack edits).
    /// </summary>
    private void RefreshEffectiveStats(RecordRow row)
    {
        if (row.PrimaryTable != TEnemies) return;
        string? ArchetypeByRow(string id)
        {
            if (Loot.Catalog.Enemies.TryGetValue(id, out var info)) return info.Archetype;
            return null;
        }
        if (EnemyEffectiveStats.TryCompute(row.RowName, row.Baseline, ArchetypeByRow,
                out var hp, out var atk, out var spd))
        {
            SetToken(row.Baseline, EnemyEffectiveStats.EffHpPath, JToken.FromObject(hp));
            SetToken(row.Baseline, EnemyEffectiveStats.EffAttackPath, JToken.FromObject(atk));
            SetToken(row.Baseline, EnemyEffectiveStats.EffSpeedPath, JToken.FromObject(spd));
        }
    }

    private void BuildEncounters()
    {
        _rowsByKey["enc-dungeon"] = _rowsByKey.GetValueOrDefault(TEncDungeon) ?? new();
        _rowsByKey["enc-tower"] = _rowsByKey.GetValueOrDefault(TEncTower) ?? new();
        _rowsByKey["enc-world"] = _rowsByKey.GetValueOrDefault(TEncWorld) ?? new();
    }

    private void BuildWeapons()
    {
        if (!_rowsByKey.TryGetValue(TItems, out var items)) return;
        _rowsByKey.TryGetValue(TWeaponDef, out var defs);
        var defByName = (defs ?? new()).ToDictionary(r => r.RowName, r => r, StringComparer.OrdinalIgnoreCase);

        var weapons = new List<RecordRow>();
        foreach (var item in items)
        {
            if (item.Baseline["Item_Type"]?.ToString() != "NewEnumerator0") continue;
            var merged = (JObject)item.Baseline.DeepClone();
            var row = new RecordRow
            {
                RowName = item.RowName,
                DisplayName = LookupName(item.Baseline, "Item_DisplayName") is { Length: > 0 } n ? n : item.RowName,
                PrimaryTable = TItems,
                Baseline = merged,
            };
            foreach (var leafPath in FlattenKeys(item.Baseline))
                row.Fields[leafPath] = new FieldRef(TItems, item.RowName, leafPath);

            if (defByName.TryGetValue(item.RowName, out var def))
            {
                foreach (var prop in def.Baseline.Properties())
                    merged[prop.Name] = prop.Value.DeepClone();
                foreach (var leafPath in FlattenKeys(def.Baseline))
                    row.Fields[leafPath] = new FieldRef(TWeaponDef, def.RowName, leafPath);
                row.Fields["AttributeScaling"] = new FieldRef(TWeaponDef, def.RowName, "AttributeScaling");
            }

            merged["_Character"] = GameLabels.CharacterFromWeaponPicto(
                item.Baseline["Pictos_Data"]?.ToString(),
                item.Baseline["Item_Subtype"]?.ToString());

            weapons.Add(row);
        }
        _rowsByKey["weapons"] = weapons.OrderBy(w => w.DisplayName).ToList();
        RebuildPassiveCatalog(weapons);
    }

    private void BuildWeaponScaling()
    {
        var merged = new JObject();
        var row = new RecordRow
        {
            RowName = "WeaponScaling",
            DisplayName = "Weapon Level Curve",
            PrimaryTable = TWeaponScale,
            Baseline = merged,
        };
        if (_rowsByKey.TryGetValue(TWeaponScale, out var curve))
        {
            foreach (var lvl in curve)
            {
                foreach (var prop in lvl.Baseline.Properties())
                {
                    if (prop.Name == "Level") continue;
                    var display = $"LevelCurve.{lvl.RowName}.{prop.Name}";
                    SetToken(merged, display, prop.Value.DeepClone());
                    row.Fields[display] = new FieldRef(TWeaponScale, lvl.RowName, prop.Name);
                }
            }
        }
        _rowsByKey["weapon-scaling"] = new List<RecordRow> { row };
    }

    public RecordRow? WeaponScalingRow =>
        _rowsByKey.TryGetValue("weapon-scaling", out var list) && list.Count > 0 ? list[0] : null;

    public RecordRow? PictosScalingRow =>
        _rowsByKey.TryGetValue("pictos-scaling", out var list) && list.Count > 0 ? list[0] : null;

    public double[] SharedCurve(string field) =>
        WeaponScalingRow is { } row ? WeaponCurve.ReadSeries(row, field)
            : field == "ChromaValue" ? WeaponCurve.VanillaChroma : WeaponCurve.VanillaPhysical;

    public double[] SharedPictosCurve(string field)
    {
        if (PictosScalingRow is { } row) return WeaponCurve.ReadSeries(row, field);
        foreach (var (f, _, vanilla, _) in PictosCurve.Series)
            if (f == field) return vanilla;
        return PictosCurve.VanillaHp;
    }

    public (string Name, string Description, string Short) DescribeSkill(RecordRow row)
    {
        var name = LookupName(row.Baseline, "Name");
        var desc = LookupName(row.Baseline, "Description");
        var shortDesc = LookupName(row.Baseline, "ShortDescription");
        if (name.Length == 0) name = row.DisplayName;
        return (name, desc, shortDesc);
    }

    /// <summary>Skill rows for one character, ordered by AP cost then name — the Skill Overhaul roster.</summary>
    public IReadOnlyList<RecordRow> SkillsForCharacter(string character)
    {
        if (!_rowsByKey.TryGetValue("skills", out var all)) return Array.Empty<RecordRow>();
        static int Ap(RecordRow r) => TokenAt(r.Baseline, "APCost")?.Value<int>() ?? 0;
        return all
            .Where(r => string.Equals(r.Baseline["_Character"]?.ToString(), character,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(Ap)
            .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>All skill rows keyed by DA stem (DA_Skill_&lt;prefix&gt;_&lt;Name&gt;), for graph enrichment.</summary>
    public IReadOnlyDictionary<string, RecordRow> SkillsByStem()
    {
        if (!_rowsByKey.TryGetValue("skills", out var all))
            return new Dictionary<string, RecordRow>();
        var map = new Dictionary<string, RecordRow>(StringComparer.Ordinal);
        foreach (var row in all)
            if (!map.ContainsKey(row.RowName))
                map[row.RowName] = row;
        return map;
    }

    public (string Name, string Description) DescribePassive(string id)
    {
        if (string.IsNullOrEmpty(id)) return ("(none)", "");
        if (WeaponEffectsCreateKit.TryGet(id, out var created))
            return (created.DisplayName, created.Description);
        foreach (var p in PictosCreateKit.PainterConversions)
            if (string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
                return (p.DisplayName, p.Description);
        foreach (var p in PictosCreateKit.Powers)
            if (string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
                return (p.DisplayName, p.Description);
        string? Pick(params string[] keys)
        {
            foreach (var k in keys)
                if (_strings.TryGetValue(k, out var v) && v.Length > 0) return v;
            return null;
        }
        var name = Pick($"PASSIVE_{id}_Name", $"{id}_Name") ?? GameLabels.Humanize(id);
        var desc = Pick($"PASSIVE_{id}_Description", $"{id}_Description", $"PASSIVE_{id}_Desc") ?? "";
        return (name, desc);
    }

    private void RebuildPassiveCatalog(List<RecordRow> weapons)
    {
        _passivesByCharacter.Clear();
        var all = new HashSet<string>(StringComparer.Ordinal);
        foreach (var w in weapons)
        {
            var ch = w.Baseline["_Character"]?.ToString() ?? "";
            if (ch.Length == 0) ch = "Other";
            if (!_passivesByCharacter.TryGetValue(ch, out var set))
                _passivesByCharacter[ch] = set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in new[] { "Level1Lumina", "Level2Lumina", "Level3Lumina", "Level4Lumina", "Level5Lumina" })
            {
                var p = w.Baseline[key]?.ToString() ?? "";
                if (p.Length == 0) continue;
                set.Add(p);
                all.Add(p);
            }
        }
        if (WeaponEffectsCreateKit.Specs.Count > 0)
        {
            if (!_passivesByCharacter.TryGetValue("Created", out var created))
                _passivesByCharacter["Created"] = created = new HashSet<string>(StringComparer.Ordinal);
            foreach (var spec in WeaponEffectsCreateKit.Specs)
            {
                created.Add(spec.Id);
                all.Add(spec.Id);
                if (!string.IsNullOrEmpty(spec.Character))
                {
                    if (!_passivesByCharacter.TryGetValue(spec.Character, out var chSet))
                        _passivesByCharacter[spec.Character] = chSet = new HashSet<string>(StringComparer.Ordinal);
                    chSet.Add(spec.Id);
                }
            }
        }
        _allWeaponPassives.Clear();
        _allWeaponPassives.AddRange(all.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
    }

    private void BuildPictos()
    {
        if (!_rowsByKey.TryGetValue(TItems, out var items)) return;
        _rowsByKey.TryGetValue(TPassives, out var passives);
        _rowsByKey.TryGetValue(TPictosDef, out var defs);
        _rowsByKey.TryGetValue(TLuminaBal, out var balancing);
        var passByName = (passives ?? new()).ToDictionary(r => r.RowName, r => r, StringComparer.OrdinalIgnoreCase);
        var defByName = (defs ?? new()).ToDictionary(r => r.RowName, r => r, StringComparer.OrdinalIgnoreCase);
        var balRows = balancing ?? new List<RecordRow>();
        var usedBal = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var list = new List<RecordRow>();
        foreach (var item in items)
        {
            if (item.Baseline["Item_Type"]?.ToString() != "NewEnumerator10") continue;
            var merged = (JObject)item.Baseline.DeepClone();
            var row = new RecordRow
            {
                RowName = item.RowName,
                DisplayName = LookupName(item.Baseline, "Item_DisplayName") is { Length: > 0 } n ? n : item.RowName,
                PrimaryTable = TItems,
                Baseline = merged,
            };
            foreach (var leafPath in FlattenKeys(item.Baseline))
                row.Fields[leafPath] = new FieldRef(TItems, item.RowName, leafPath);

            var passiveKey = item.RowName;
            var pdata = item.Baseline["Pictos_Data"]?.ToString() ?? "";
            if (pdata.StartsWith("DA_GearPictos_", StringComparison.Ordinal))
                passiveKey = pdata["DA_GearPictos_".Length..];
            if (passByName.TryGetValue(passiveKey, out var pas) ||
                passByName.TryGetValue(item.RowName, out pas))
            {
                foreach (var prop in pas.Baseline.Properties())
                {
                    if (merged[prop.Name] is null)
                        merged[prop.Name] = prop.Value.DeepClone();
                }
                foreach (var leafPath in FlattenKeys(pas.Baseline))
                    if (!row.Fields.ContainsKey(leafPath))
                        row.Fields[leafPath] = new FieldRef(TPassives, pas.RowName, leafPath);
                row.Fields["Categories"] = new FieldRef(TPassives, pas.RowName, "Categories");
                row.Fields["LuminaCost"] = new FieldRef(TPassives, pas.RowName, "LuminaCost");
                row.Fields["LuminaUnlockStepsCount"] = new FieldRef(TPassives, pas.RowName, "LuminaUnlockStepsCount");
                var pretty = LookupName(pas.Baseline, "Name");
                if (pretty.Length > 0) row.DisplayName = pretty;
            }

            if (defByName.TryGetValue(item.RowName, out var def) ||
                defByName.TryGetValue(passiveKey, out def))
            {
                foreach (var prop in def.Baseline.Properties())
                    merged[prop.Name] = prop.Value.DeepClone();
                foreach (var leafPath in FlattenKeys(def.Baseline))
                    row.Fields[leafPath] = new FieldRef(TPictosDef, def.RowName, leafPath);
            }

            AttachBalancing(row, merged, balRows, usedBal, item.RowName, passiveKey);
            var (pname, pdesc) = DescribePassive(passiveKey.Length > 0 ? passiveKey : item.RowName);
            row.HoverTitle = pname.Length > 0 ? pname : row.DisplayName;
            row.HoverBody = pdesc;
            list.Add(row);
        }
        _rowsByKey["pictos"] = list.OrderBy(p => p.DisplayName).ToList();
    }

    private static void AttachBalancing(RecordRow row, JObject merged, List<RecordRow> balRows,
        HashSet<string> used, params string[] ids)
    {
        var prefixes = ids
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id + "_")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(p => p.Length)
            .ToArray();
        if (prefixes.Length == 0) return;
        var names = new JArray();
        foreach (var bal in balRows)
        {
            if (used.Contains(bal.RowName)) continue;
            if (!prefixes.Any(p => bal.RowName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                continue;
            used.Add(bal.RowName);
            var display = "Balancing." + bal.RowName;
            var val = bal.Baseline["FloatValue"]?.DeepClone() ?? 0;
            SetToken(merged, display, val);
            row.Fields[display] = new FieldRef(TLuminaBal, bal.RowName, "FloatValue");
            names.Add(bal.RowName);
        }
        if (names.Count > 0)
            merged["_BalancingKeys"] = names;
    }

    private void BuildPictosScaling()
    {
        var merged = new JObject();
        var row = new RecordRow
        {
            RowName = "PictosScaling",
            DisplayName = "Pictos Level Curve",
            PrimaryTable = TPictosScale,
            Baseline = merged,
        };
        if (_rowsByKey.TryGetValue(TPictosScale, out var curve))
        {
            foreach (var lvl in curve)
            {
                foreach (var leaf in FlattenKeys(lvl.Baseline))
                {
                    if (leaf == "Level") continue;
                    var display = $"LevelCurve.{lvl.RowName}.{leaf}";
                    SetToken(merged, display, TokenAt(lvl.Baseline, leaf)?.DeepClone() ?? 0);
                    row.Fields[display] = new FieldRef(TPictosScale, lvl.RowName, leaf);
                }
            }
        }
        _rowsByKey["pictos-scaling"] = new List<RecordRow> { row };
    }

    private void LoadSkills()
    {
        var skills = new List<RecordRow>();
        var graphUnlock = new Dictionary<string, (int Cost, bool Starting, bool Secret)>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(GraphsDir))
        {
            foreach (var file in Directory.EnumerateFiles(GraphsDir, "DA_SkillGraph*.uasset", SearchOption.AllDirectories))
            {
                try
                {
                    var ed = DataTableEditor.OpenPropertyAsset(file);
                    var leaves = ed.FlattenProperties();
                    var byPath = leaves.GroupBy(l => l.Path).ToDictionary(g => g.Key, g => g.First());
                    foreach (var leaf in leaves)
                    {
                        if (!leaf.Path.EndsWith(".SkillUnlock.Skill", StringComparison.Ordinal)) continue;
                        var node = leaf.Path[..^".SkillUnlock.Skill".Length];
                        var skillName = leaf.Value?.ToString() ?? "";
                        byPath.TryGetValue(node + ".SkillUnlock.UnlockCost", out var costLeaf);
                        byPath.TryGetValue(node + ".SkillUnlock.IsStartingSkill", out var startLeaf);
                        byPath.TryGetValue(node + ".SkillUnlock.IsSecret", out var secretLeaf);
                        int.TryParse(costLeaf?.Value?.ToString() ?? "0", out var cost);
                        var starting = string.Equals(startLeaf?.Value?.ToString(), "True", StringComparison.OrdinalIgnoreCase);
                        var secret = string.Equals(secretLeaf?.Value?.ToString(), "True", StringComparison.OrdinalIgnoreCase);
                        if (skillName.Length > 0 && !graphUnlock.ContainsKey(skillName))
                            graphUnlock[skillName] = (cost, starting, secret);
                    }
                }
                catch { /* skip unreadable graphs */ }
            }
        }

        if (!Directory.Exists(SkillsDir)) { _rowsByKey["skills"] = skills; return; }

        foreach (var file in Directory.EnumerateFiles(SkillsDir, "DA_Skill_*.uasset", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            if (stem.StartsWith("DA_SkillGraph", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var ed = DataTableEditor.OpenPropertyAsset(file);
                var leaves = ed.FlattenProperties();
                var baseline = new JObject();
                var row = new RecordRow
                {
                    RowName = stem,
                    DisplayName = stem.Replace("DA_Skill_", ""),
                    PrimaryTable = stem,
                    AssetPath = file,
                    IsAsset = true,
                    Baseline = baseline,
                };
                foreach (var leaf in leaves)
                {
                    baseline[leaf.Path] = leaf.Value is null ? JValue.CreateNull() : leaf.Value.DeepClone();
                    row.Fields[leaf.Path] = new FieldRef(stem, stem, leaf.Path, IsAsset: true);
                }
                var character = GameLabels.CharacterFromPath(file);
                baseline["_Character"] = character;
                var pretty = LookupName(baseline, "Name");
                if (pretty.Length > 0) row.DisplayName = pretty;
                else if (character.Length > 0) row.DisplayName = $"{row.DisplayName} ({character})";

                if (graphUnlock.TryGetValue(stem, out var g))
                {
                    baseline["UnlockCost"] = g.Cost;
                    baseline["IsStartingSkill"] = g.Starting;
                    baseline["IsSecret"] = g.Secret;
                }

                _editors[stem] = ed;
                _assetPaths[stem] = file;
                skills.Add(row);
            }
            catch { /* skip */ }
        }
        var ordered = skills.OrderBy(s => s.Baseline["_Character"]?.ToString())
            .ThenBy(s => s.DisplayName).ToList();
        AttachSkillBalancing(ordered);
        _rowsByKey["skills"] = ordered;
    }

    private void AttachSkillBalancing(List<RecordRow> skills)
    {
        if (!_rowsByKey.TryGetValue(TSkillBal, out var balancing) || balancing.Count == 0)
            return;
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nameIdOf = new Dictionary<RecordRow, string>();
        foreach (var s in skills)
            nameIdOf[s] = s.Baseline["NameID"]?.ToString() ?? "";

        string Prefix(string id) => id.EndsWith('_') ? id : id + "_";

        foreach (var bal in balancing)
        {
            if (bal.RowName.StartsWith("Generic_", StringComparison.Ordinal)) continue;
            RecordRow? best = null;
            var bestLen = 0;
            foreach (var skill in skills)
            {
                var id = nameIdOf[skill];
                if (id.Length == 0) continue;
                var p = Prefix(id);
                if (bal.RowName.StartsWith(p, StringComparison.OrdinalIgnoreCase) && p.Length > bestLen)
                {
                    best = skill;
                    bestLen = p.Length;
                }
            }
            if (best is null) continue;
            BindSkillBal(best, bal, used);
        }

        foreach (var skill in skills)
        {
            var have = BalancingKeyList(skill);
            if (have.Count > 0)
            {
                MaybeShareGeneric(skill, balancing, have);
                skill.Baseline["_BalancingKeys"] = new JArray(have);
                continue;
            }

            foreach (var p in SkillFallbackPrefixes(skill, nameIdOf[skill]))
            {
                foreach (var bal in balancing)
                {
                    if (used.Contains(bal.RowName)) continue;
                    if (bal.RowName.StartsWith("Generic_", StringComparison.Ordinal)) continue;
                    if (!bal.RowName.StartsWith(p, StringComparison.OrdinalIgnoreCase)) continue;
                    BindSkillBal(skill, bal, used);
                    have.Add(bal.RowName);
                }
            }
            var nameId = nameIdOf[skill];
            if (nameId.Length >= 4)
            {
                foreach (var bal in balancing)
                {
                    if (used.Contains(bal.RowName)) continue;
                    if (bal.RowName.StartsWith("Generic_", StringComparison.Ordinal)) continue;
                    var head = HeadOf(bal.RowName);
                    if (head.Equals(nameId, StringComparison.OrdinalIgnoreCase) ||
                        (head.Length >= 5 && nameId.Length >= 5 &&
                         (head.EndsWith(nameId, StringComparison.OrdinalIgnoreCase) ||
                          nameId.EndsWith(head, StringComparison.OrdinalIgnoreCase))))
                    {
                        BindSkillBal(skill, bal, used);
                        have.Add(bal.RowName);
                    }
                }
            }
            MaybeShareAncestor(skill, skills, nameIdOf, balancing, have);
            MaybeShareGeneric(skill, balancing, have);
            if (have.Count > 0)
                skill.Baseline["_BalancingKeys"] = new JArray(have);
        }
    }

    private void BindSkillBal(RecordRow skill, RecordRow bal, HashSet<string>? used)
    {
        used?.Add(bal.RowName);
        var display = "Balancing." + bal.RowName;
        if (skill.Fields.ContainsKey(display)) return;
        var val = bal.Baseline["FloatValue"]?.DeepClone() ?? 0;
        SetToken(skill.Baseline, display, val);
        skill.Fields[display] = new FieldRef(TSkillBal, bal.RowName, "FloatValue");
        if (bal.RowName.EndsWith("_BaseDamage", StringComparison.OrdinalIgnoreCase)
            && !skill.Fields.ContainsKey("BaseDamage"))
        {
            skill.Baseline["BaseDamage"] = val.DeepClone();
            skill.Fields["BaseDamage"] = new FieldRef(TSkillBal, bal.RowName, "FloatValue");
        }
    }

    private static List<string> BalancingKeyList(RecordRow skill)
    {
        var list = new List<string>();
        foreach (var path in skill.Fields.Keys)
            if (path.StartsWith("Balancing.", StringComparison.Ordinal))
                list.Add(path["Balancing.".Length..]);
        return list;
    }

    private void MaybeShareAncestor(RecordRow skill, List<RecordRow> skills,
        Dictionary<RecordRow, string> nameIdOf, List<RecordRow> balancing, List<string> have)
    {
        var id = nameIdOf[skill];
        var cut = id.LastIndexOf('_');
        if (cut <= 0) return;
        var ancestor = id[..cut];
        if (!skills.Any(s => string.Equals(nameIdOf[s], ancestor, StringComparison.OrdinalIgnoreCase)))
            return;
        var p = ancestor + "_";
        foreach (var bal in balancing)
        {
            if (!bal.RowName.StartsWith(p, StringComparison.OrdinalIgnoreCase)) continue;
            if (bal.RowName.StartsWith(id + "_", StringComparison.OrdinalIgnoreCase)) continue;
            BindSkillBal(skill, bal, used: null);
            if (!have.Contains(bal.RowName)) have.Add(bal.RowName);
        }
    }

    private void MaybeShareGeneric(RecordRow skill, List<RecordRow> balancing, List<string> have)
    {
        var wantQte = have.Any(k => k.Contains("BaseDamage", StringComparison.OrdinalIgnoreCase)
                                    || k.Contains("Damage", StringComparison.OrdinalIgnoreCase));
        var wantBurn = have.Any(k => k.Contains("Burn", StringComparison.OrdinalIgnoreCase));
        foreach (var bal in balancing)
        {
            if (wantQte && bal.RowName == "Generic_DamageIncreaseForEachQTE")
            {
                BindSkillBal(skill, bal, used: null);
                if (!have.Contains(bal.RowName)) have.Add(bal.RowName);
            }
            if (wantBurn && bal.RowName == "Generic_BurnDefaultDuration")
            {
                BindSkillBal(skill, bal, used: null);
                if (!have.Contains(bal.RowName)) have.Add(bal.RowName);
            }
        }
    }

    private static string HeadOf(string key)
    {
        var i = key.IndexOf('_');
        return i > 0 ? key[..i] : key;
    }

    private static IEnumerable<string> SkillFallbackPrefixes(RecordRow row, string nameId)
    {
        var stem = row.RowName;
        if (stem.StartsWith("DA_Skill_", StringComparison.Ordinal))
            stem = stem["DA_Skill_".Length..];
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Lune", "Maelle", "Sciel", "Gustave", "Verso", "Monoco", "Julie", "Noah", "NoahVerso",
            "Transfo", "Skills",
        };
        var parts = stem.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var filtered = parts.Where(p =>
            !skip.Contains(p) &&
            !p.StartsWith("NEW", StringComparison.OrdinalIgnoreCase) &&
            !p.StartsWith("new", StringComparison.Ordinal)).ToArray();
        if (filtered.Length > 0)
            yield return string.Join("_", filtered) + "_";
        if (parts.Length > 0)
            yield return parts[^1] + "_";
        if (parts.Length >= 2 && skip.Contains(parts[0]))
            yield return parts[^1] + "_" + parts[0] + "_";
        if (nameId.StartsWith("Old", StringComparison.OrdinalIgnoreCase) && nameId.Length > 3)
            yield return nameId[3..] + "_";
        var cut = nameId.LastIndexOf('_');
        if (cut > 0) yield return nameId[..cut] + "_";
    }

    // ─────────────────────── filter & sort ───────────────────────

    private (string Path, bool Desc)? _sort;

    public ColumnSpec[] ColumnsFor(string key) =>
        ColumnSpecs.TryGetValue(key, out var c) ? c : Array.Empty<ColumnSpec>();

    public (string Path, bool Desc) SortBy(string path)
    {
        if (_sort?.Path == path) _sort = (path, !_sort.Value.Desc);
        else _sort = (path, false);
        RefreshView();
        return _sort.Value;
    }

    public void RefreshView()
    {
        var key = _activeKey;
        if (key.Length == 0 || !_rowsByKey.TryGetValue(key, out var all))
        {
            Rows.Clear();
            return;
        }

        IEnumerable<RecordRow> view = all;
        if (!string.IsNullOrWhiteSpace(_filterText))
            view = view.Where(r =>
                r.RowName.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ||
                r.DisplayName.Contains(_filterText, StringComparison.OrdinalIgnoreCase));

        var cols = ColumnsFor(key);
        if (_sort is { } s)
        {
            if (s.Path is "DisplayName" or "RowName")
            {
                view = s.Desc
                    ? view.OrderByDescending(r => r.DisplayName)
                    : view.OrderBy(r => r.DisplayName);
            }
            else if (Array.Find(cols, c => c.Path == s.Path) is { } col)
            {
                view = s.Desc
                    ? view.OrderByDescending(r => SortKeyValue(r.Baseline, col))
                    : view.OrderBy(r => SortKeyValue(r.Baseline, col));
            }
        }

        SelectedRow = null;
        Rows.Clear();
        _longestCache.Clear();
        foreach (var r in view)
        {
            HydrateValues(r, cols);
            Rows.Add(r);
        }
    }

    private static object? SortKeyValue(JObject baseline, ColumnSpec col)
    {
        var tok = TokenAt(baseline, col.Path);
        if (col.Numeric && tok?.Type is JTokenType.Float or JTokenType.Integer)
            return (double)tok;
        return tok?.ToString() ?? "";
    }

    public void HydrateValues(RecordRow row, ColumnSpec[] cols)
    {
        for (int i = 0; i < cols.Length; i++)
        {
            var col = cols[i];
            var tok = row.AdvancedEdits.TryGetValue(col.Path, out var adv)
                ? adv
                : TokenAt(row.Baseline, col.Path);
            row.Values[$"c{i}"] = DecodeValue(col.Path, tok);
        }
        FillHoverTip(row);
    }

    private void FillHoverTip(RecordRow row)
    {
        if (ActiveModule?.Id == "pictos")
        {
            if (string.IsNullOrEmpty(row.HoverBody))
            {
                var pdata = row.Baseline["Pictos_Data"]?.ToString() ?? "";
                var key = pdata.StartsWith("DA_GearPictos_", StringComparison.Ordinal)
                    ? pdata["DA_GearPictos_".Length..]
                    : row.RowName;
                var (n, d) = DescribePassive(key);
                row.HoverTitle = n.Length > 0 ? n : row.DisplayName;
                row.HoverBody = d;
            }
            return;
        }
        if (ActiveModule?.Id != "weapons") return;
        var lines = new List<string>();
        foreach (var (label, path) in new[]
                 {
                     ("Lvl 33", "Level1Lumina"),
                     ("Lvl 66", "Level2Lumina"),
                     ("Lvl 111", "Level3Lumina"),
                     ("Lvl 222", "Level4Lumina"),
                     ("Lvl 333", "Level5Lumina"),
                 })
        {
            var id = row.AdvancedEdits.TryGetValue(path, out var adv)
                ? adv.ToString()
                : row.Baseline[path]?.ToString() ?? "";
            if (string.IsNullOrEmpty(id))
            {
                lines.Add($"{label}: (none)");
                continue;
            }
            var (n, d) = DescribePassive(id);
            lines.Add(string.IsNullOrEmpty(d) ? $"{label}: {n}" : $"{label}: {n}\n{d}");
        }
        row.HoverTitle = row.DisplayName;
        row.HoverBody = string.Join("\n\n", lines);
    }

    private string DecodeValue(string path, JToken? tok)
    {
        if (tok is null) return "";
        if (path == "Enemies" && tok is JObject enemies)
            return FormatEnemies(enemies);
        if (path == "AttributeScaling" && tok is JObject scaling)
            return FormatAttributeScaling(scaling);
        if (tok is JObject obj) return FormatMapDisplay(obj);
        if (tok is JArray arr) return string.Join(", ", arr.Select(a => a?.ToString()));
        var raw = tok.ToString();
        if (path is "Level1Lumina" or "Level2Lumina" or "Level3Lumina" or "Level4Lumina" or "Level5Lumina")
        {
            if (string.IsNullOrEmpty(raw)) return "—";
            var (n, _) = DescribePassive(raw);
            return n;
        }
        if (raw is "True" or "False") return raw.ToLowerInvariant();
        var leaf = path.Contains('.') ? path[(path.LastIndexOf('.') + 1)..] : path;
        return GameLabels.Decode(leaf, raw);
    }

    private static string FormatEnemies(JObject enemies)
    {
        var names = new List<string>();
        foreach (var p in enemies.Properties())
        {
            var n = p.Value is JObject o ? o["RowName"]?.ToString() : p.Value?.ToString();
            if (!string.IsNullOrEmpty(n)) names.Add(n);
        }
        return names.Count == 0 ? "—" : string.Join(", ", names);
    }

    private static string FormatAttributeScaling(JObject obj)
    {
        if (obj.Count == 0) return "—";
        static int Rank(string key)
        {
            var leaf = key.Contains("::") ? key[(key.IndexOf("::", StringComparison.Ordinal) + 2)..] : key;
            var idx = DataTableEditor.EnumeratorIndex(leaf) ?? 99;
            return idx switch { 0 => 0, 1 => 1, 3 => 2, 4 => 3, 5 => 4, _ => 10 + idx };
        }
        var parts = obj.Properties()
            .Select(p =>
            {
                var stat = GameLabels.Decode("ECharacterAttribute", p.Name);
                var grade = GameLabels.Decode("EAttributeScalingGrade", p.Value?.ToString() ?? "");
                return (Rank: Rank(p.Name), Stat: stat, Grade: grade);
            })
            .Where(x => x.Stat.Length > 0 && x.Stat is not "Invalid" && x.Grade.Length > 0 && x.Grade is not "Invalid")
            .OrderBy(x => x.Rank)
            .Select(x => $"{x.Stat} {x.Grade}")
            .ToList();
        return parts.Count == 0 ? "—" : string.Join(", ", parts);
    }

    private static string FormatMapDisplay(JObject obj)
    {
        if (obj.Count == 0) return "—";
        var items = new List<string>();
        foreach (var p in obj.Properties())
        {
            var k = p.Name.Contains("::")
                ? GameLabels.Humanize(DataTableEditor.StaticDecode(p.Name))
                : GameLabels.Humanize(p.Name.Split('.').Last());
            var vRaw = p.Value?.ToString() ?? "";
            var v = vRaw.Contains("::") ? GameLabels.Humanize(DataTableEditor.StaticDecode(vRaw)) : vRaw;
            items.Add($"{k} {v}".Trim());
        }
        return string.Join(", ", items);
    }

    public string DecodeLeafForEdit(DataTableEditor.PropertyLeaf leaf)
    {
        var raw = leaf.Value?.ToString() ?? "";
        if (leaf.Kind is "enum" or "name")
            return GameLabels.Decode(leaf.Path, raw);
        return raw;
    }

    public void AfterEditorClosed(RecordRow row)
    {
        var dirtyCount = _rowsByKey.Values.Sum(l => l.Count(r => r.IsDirty));
        Status = dirtyCount > 0
            ? $"{dirtyCount} edit(s) pending — Apply && Pack when ready."
            : "Ready.";
        if (ActiveModule is not null)
            HydrateValues(row, ColumnsFor(ActiveModule.Id));
    }

    // ─────────────────────── editing ───────────────────────

    public IReadOnlyList<DataTableEditor.PropertyLeaf> GetFlattenedLeaves(RecordRow row)
    {
        if (row.IsAsset && _editors.TryGetValue(row.PrimaryTable, out var assetEd))
            return FilterLeaves(assetEd.FlattenProperties())
                .Select(l => OverlayQueuedEdit(row, l.Path, l))
                .ToList();

        var combined = new List<DataTableEditor.PropertyLeaf>();
        var seen = new HashSet<string>();

        void AddFrom(string table, string rowName)
        {
            if (!_editors.TryGetValue(table, out var ed) || !ed.TryGetRow(rowName, out var raw)) return;
            foreach (var leaf in FilterLeaves(ed.FlattenRow(raw)))
                if (seen.Add(leaf.Path)) combined.Add(OverlayQueuedEdit(row, leaf.Path, leaf));
        }

        var sources = row.Fields.Values
            .Select(f => (f.Table, f.RowName, f.IsAsset))
            .Distinct()
            .ToList();
        if (sources.Count == 0)
            AddFrom(row.PrimaryTable, row.RowName);
        else
        {
            var addedScale = false;
            foreach (var (table, name, isAsset) in sources)
            {
                if (isAsset) continue;
                if (table is TWeaponScale or TPictosScale)
                {
                    if (ActiveModule?.Id is "weapons" or "pictos") continue;
                    if (addedScale) continue;
                    addedScale = true;
                    if (!_editors.TryGetValue(table, out var sc)) continue;
                    foreach (var lvl in sc.Table.Table.Data)
                    {
                        var ln = DataTableEditor.StripSuffix(lvl.Name.ToString());
                        foreach (var leaf in sc.FlattenRow(lvl))
                        {
                            if (leaf.Path == "Level") continue;
                            var display = $"LevelCurve.{ln}.{leaf.Path}";
                            if (seen.Add(display))
                                combined.Add(OverlayQueuedEdit(row, display, leaf with { Path = display }));
                        }
                    }
                    continue;
                }
                AddFrom(table, name);
            }
        }
        return combined;
    }

    private IReadOnlyList<DataTableEditor.PropertyLeaf> FilterLeaves(
        IReadOnlyList<DataTableEditor.PropertyLeaf> leaves)
    {
        var id = ActiveModule?.Id ?? "";
        if (id is "weapons" or "pictos")
            return leaves.Where(l =>
                !l.Path.StartsWith("Consumable_", StringComparison.Ordinal) &&
                l.Path != "Weapon_SkeletalMesh" &&
                l.Path != "Pictos_SkinTexture" &&
                l.Path != "Pictos_UnlockedPassiveEffects" &&
                (id != "weapons" || (
                    l.Path is not "PhysicalAttackMultiplier" and not "MagicalAttackMultiplier"
                        and not "ChromaValueMultiplier" &&
                    !l.Path.StartsWith("BaseStatsMultipliers", StringComparison.Ordinal)))).ToList();
        return leaves;
    }

    /// <summary>
    /// Overlays a queued edit onto a flattened leaf so re-opening a record
    /// shows pending values instead of the stale table baseline. Keyed by the
    /// display path — the same key <see cref="QueueEdit"/> stores in
    /// <c>AdvancedEdits</c>.
    /// </summary>
    private static DataTableEditor.PropertyLeaf OverlayQueuedEdit(
        RecordRow row, string displayPath, DataTableEditor.PropertyLeaf leaf) =>
        row.AdvancedEdits.TryGetValue(displayPath, out var adv)
            ? leaf with { Value = adv }
            : leaf;

    public void QueueEdit(RecordRow row, string path, string newValue)
    {
        // Synthetic effective-stat display keys are read-only; they recompute
        // from Level/EnemyScaling and must never be queued as table edits.
        if (path is EnemyEffectiveStats.EffHpPath or EnemyEffectiveStats.EffAttackPath or EnemyEffectiveStats.EffSpeedPath)
            return;
        var dest = row.Fields.TryGetValue(path, out var fr)
            ? fr
            : new FieldRef(row.PrimaryTable, row.RowName, path, row.IsAsset);

        JToken typed = newValue.ToLowerInvariant() switch
        {
            "true" => true,
            "false" => false,
            _ when int.TryParse(newValue, out var n) && !newValue.Contains('.') => n,
            _ when double.TryParse(newValue, System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out var d) => d,
            _ => newValue,
        };

        var editorKey = dest.IsAsset ? dest.Table : dest.Table;
        if (_editors.TryGetValue(editorKey, out var qEd))
        {
            var baselineRaw = TokenAt(row.Baseline, path)?.ToString() ?? "";
            var isEnumField = baselineRaw.StartsWith("NewEnumerator", StringComparison.Ordinal)
                              || baselineRaw.Contains("::");
            var decoded = qEd.ReverseDecode(dest.Path, baselineRaw, newValue);
            var looksRaw = newValue.StartsWith("NewEnumerator", StringComparison.Ordinal)
                           || newValue.Contains("::");
            if (isEnumField && decoded == newValue && !looksRaw &&
                !string.Equals(newValue, baselineRaw, StringComparison.Ordinal))
            {
                Status = $"'{newValue}' is not a valid value for {GameLabels.FieldLabel(path)} — edit ignored.";
                return;
            }
            if (decoded != newValue)
            {
                newValue = decoded;
                typed = decoded;
            }
        }

        if (TokenAt(row.Baseline, path)?.ToString() == typed.ToString() && !row.AdvancedEdits.ContainsKey(path))
            return;

        if (!row.Fields.ContainsKey(path))
            row.Fields[path] = dest;
        row.AdvancedEdits[path] = typed;
        SetToken(row.Baseline, path, typed);
        row.Dirty();
        RefreshEffectiveStats(row);
        Status = $"Queued edit: {row.DisplayName}.{GameLabels.FieldLabel(path)} = {typed}";
        AutosaveProject();
    }

    public string LongestShown(string path, string kind)
    {
        var key = $"{_activeKey}\0{path}\0{kind}";
        if (_longestCache.TryGetValue(key, out var hit)) return hit;
        var longest = "";
        if (_rowsByKey.TryGetValue(_activeKey, out var all))
        {
            foreach (var other in all)
            {
                JToken? tok = other.AdvancedEdits.TryGetValue(path, out var adv)
                    ? adv
                    : TokenAt(other.Baseline, path);
                var raw = tok?.ToString() ?? "";
                var shown = kind is "enum" or "name" ? GameLabels.Decode(path, raw) : raw;
                if (shown.Length > longest.Length) longest = shown;
            }
        }
        _longestCache[key] = longest;
        return longest;
    }

    public void QueueToken(RecordRow row, string path, JToken typed)
    {
        if (path is EnemyEffectiveStats.EffHpPath or EnemyEffectiveStats.EffAttackPath or EnemyEffectiveStats.EffSpeedPath)
            return;
        var dest = row.Fields.TryGetValue(path, out var fr)
            ? fr
            : new FieldRef(row.PrimaryTable, row.RowName, path, row.IsAsset);
        if (!row.Fields.ContainsKey(path))
            row.Fields[path] = dest;
        row.AdvancedEdits[path] = typed;
        SetToken(row.Baseline, path, typed);
        row.Dirty();
        RefreshEffectiveStats(row);
        Status = $"Queued edit: {row.DisplayName}.{GameLabels.FieldLabel(path)}";
        AutosaveProject();
    }

    public IReadOnlyList<(string Raw, string Label)> EnumChoices(string path)
    {
        var leaf = path.Contains('.') ? path[(path.LastIndexOf('.') + 1)..] : path;
        var type = GameLabels.GuessType(leaf);
        var names = DataTableEditor.EnumDisplayNames(type);
        var map = GameLabels.FallbackMap(type) ?? GameLabels.FallbackMap(leaf);
        var list = new List<(string Raw, string Label)>();
        if (names is { Count: > 0 })
        {
            for (int i = 0; i < names.Count; i++)
            {
                var label = GameLabels.DisplayAlias(names[i]);
                if (string.IsNullOrWhiteSpace(label) ||
                    label is "Invalid" or "N/A" or "#0") continue;
                list.Add(($"NewEnumerator{i}", label));
            }
            // The game enum asset can lag behind the editor's taxonomy (new
            // mod-side tags such as Element/Revive). Append any fallback
            // labels the extracted enum does not know about yet.
            AppendFallback();
            return list;
        }

        if (map is null) return list;
        AppendFallback();
        return list;

        void AppendFallback()
        {
            if (map is null) return;
            var seen = new HashSet<string>(list.Select(x => x.Raw), StringComparer.Ordinal);
            foreach (var kv in map)
            {
                if (kv.Value is "Invalid" or "N/A") continue;
                if (!seen.Add(kv.Key)) continue;
                list.Add((kv.Key, kv.Value));
            }
        }
    }

    // ─────────────────────── apply & pack ───────────────────────

    private bool _isPacking;
    /// <summary>True while Apply &amp; Pack runs; drives the modal pack overlay.</summary>
    public bool IsPacking { get => _isPacking; private set => Set(ref _isPacking, value); }

    private string _packPhase = "";
    /// <summary>Current pack phase label shown under the overlay progress bar.</summary>
    public string PackPhase { get => _packPhase; private set => Set(ref _packPhase, value); }

    /// <summary>Scrolling pack log, bound to the overlay list. UI-thread only.</summary>
    public ObservableCollection<string> PackLog { get; } = new();

    private readonly object _packLogLock = new();
    private readonly StringBuilder _packLogText = new();
    private const int MaxPackLogLines = 2000;

    /// <summary>Thread-safe pack log sink: console capture + phase steps funnel here.</summary>
    private void PackLogLine(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        Action flush = () =>
        {
            PackLog.Add(line);
            while (PackLog.Count > MaxPackLogLines) PackLog.RemoveAt(0);
        };
        lock (_packLogLock) _packLogText.AppendLine(line);
        if (Dispatcher.UIThread.CheckAccess()) flush();
        else Dispatcher.UIThread.Post(flush);
    }

    /// <summary>Sets the overlay phase label from any thread.</summary>
    private void PackStep(string phase)
    {
        PackLogLine("── " + phase + " ──");
        if (Dispatcher.UIThread.CheckAccess()) PackPhase = phase;
        else Dispatcher.UIThread.Post(() => PackPhase = phase);
    }

    /// <summary>Captures Console output (kit [pack]/[RandomizerKit] lines) into the pack log.</summary>
    private sealed class PackLogWriter(MainViewModel vm) : TextWriter
    {
        private readonly StringBuilder _buf = new();
        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(char value)
        {
            lock (_buf)
            {
                if (value == '\n') FlushBuf();
                else if (value != '\r') _buf.Append(value);
            }
        }
        public override void Write(string? value)
        {
            if (value is null) return;
            foreach (var c in value) Write(c);
        }
        private void FlushBuf()
        {
            if (_buf.Length == 0) return;
            var line = _buf.ToString();
            _buf.Clear();
            vm.PackLogLine(line);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) lock (_buf) FlushBuf();
            base.Dispose(disposing);
        }
    }


    /// <summary>
    /// Mirrors the CLI rebuild-mod strip: LevelSequences and the shared
    /// FX_ElementalCharge Niagara system cannot round-trip (packing rewritten
    /// copies crashes the game), stance parents are opened read-only so child
    /// CDOs parse, and gated-off Lune leaves staged trees behind.
    /// </summary>
    private static void StripUnroundtrippableAssets(string modDir, bool luneOn)
    {
        void DelDir(string dirName)
        {
            if (!Directory.Exists(dirName)) return;
            Directory.Delete(dirName, true);
            Console.WriteLine("[pack] stripped " + dirName);
        }
        void DelStem(string stem)
        {
            foreach (var file in Directory.EnumerateFiles(modDir, stem + ".*", SearchOption.AllDirectories).ToList())
            {
                File.Delete(file);
                Console.WriteLine("[pack] stripped " + file);
            }
        }

        DelDir(Path.Combine(modDir, "Sandfall", "Content", "Gameplay", "EpicBattleSequence"));
        DelStem("FX_ElementalCharge");
        DelStem("BP_BattleBuff_Stance_Base");
        DelStem("BP_BattleBuff_Stance_Stanceless");
        DelStem("BP_BattleBuff_Stance_Defensive");
        DelStem("BP_BattleBuffInstance");
        DelStem("BP_BattleBuff_Berserk");

        if (!luneOn)
        {
            DelDir(Path.Combine(modDir, "Sandfall", "Content", "Gameplay", "Battle", "Skills", "Content", "Lune"));
            DelDir(Path.Combine(modDir, "Sandfall", "Content", "Gameplay", "SkillTree", "Content", "Lune"));
            DelStem("DA_SkillGraph_Lune");
        }
    }

    private static string CreateTogglePath =>
        Path.Combine(EditorPaths.RepoRoot, "work", "create_pictos.json");

    private void LoadCreateToggles()
    {
        try
        {
            if (!File.Exists(CreateTogglePath))
            {
                _weaponEffectsEnabled = WeaponEffectsCreateKit.Specs.Count > 0;
                _damageLimitEnabled = false;
                return;
            }
            var o = JObject.Parse(File.ReadAllText(CreateTogglePath));
            _painterConversionEnabled = o.Value<bool?>("PainterConversion") ?? false;
            _statusAffinityEnabled = o.Value<bool?>("StatusAffinity") ?? false;
            _pictosMagnitudeEnabled = o.Value<bool?>("PictosMagnitude") ?? false;
            _damageLimitEnabled = o.Value<bool?>("DamageLimit") ?? false;
            _weaponEffectsEnabled = o.Value<bool?>("WeaponEffects")
                ?? WeaponEffectsCreateKit.Specs.Count > 0;
        }
        catch { /* optional */ }
    }

    private void SaveCreateToggles()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CreateTogglePath)!);
        File.WriteAllText(CreateTogglePath,
            new JObject
            {
                ["PainterConversion"] = _painterConversionEnabled,
                ["StatusAffinity"] = _statusAffinityEnabled,
                ["PictosMagnitude"] = _pictosMagnitudeEnabled,
                ["DamageLimit"] = _damageLimitEnabled,
                ["WeaponEffects"] = _weaponEffectsEnabled,
            }.ToString());
        AutosaveProject();
    }

    private static string GameplayTweaksPath =>
        Path.Combine(EditorPaths.RepoRoot, "work", "gameplay_tweaks.json");
    private static string SkillOverhaulPath => SkillOverhaulKit.SettingsPath;

    private void LoadSkillOverhaul()
    {
        try
        {
            SkillOverhaul.FromJson(SkillOverhaulKit.LoadSettings().ToJson());
        }
        catch { /* optional */ }
    }

    public void SaveSkillOverhaul()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SkillOverhaulPath)!);
        File.WriteAllText(SkillOverhaulPath, SkillOverhaul.ToJson().ToString());
        Status = "Skill Overhaul saved. The CLI/standalone build writes enabled changes into Export/ExpeditionInfinity_<seed>_P.";
        AutosaveProject();
    }

    private static string SkillGraphPath => SkillGraphKit.SettingsPath;

    private void LoadSkillGraph()
    {
        try
        {
            SkillGraph.FromJson(SkillGraphKit.LoadSettings().ToJson());
        }
        catch { /* optional */ }
    }

    /// <summary>Persists the per-character graph layouts (Graph Editor module).</summary>
    public void SaveSkillGraph()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SkillGraphPath)!);
        File.WriteAllText(SkillGraphPath, SkillGraph.ToJson().ToString());
        Status = "Skill Graph layouts saved. The CLI/standalone build writes them into Export/ExpeditionInfinity_<seed>_P.";
        AutosaveProject();
    }

    private void LoadGameplayTweaks()
    {
        try
        {
            if (!File.Exists(GameplayTweaksPath)) return;
            GameplayTweaks.FromJson(JObject.Parse(File.ReadAllText(GameplayTweaksPath)));
        }
        catch { /* optional */ }
    }

    public void SaveGameplayTweaks()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(GameplayTweaksPath)!);
        File.WriteAllText(GameplayTweaksPath, GameplayTweaks.ToJson().ToString());
        Status = "Gameplay Tweaks saved. The CLI/standalone build writes them into Export/ExpeditionInfinity_<seed>_P.";
        AutosaveProject();
    }

    private static string CompatibilityPath =>
        Path.Combine(EditorPaths.RepoRoot, "work", "compatibility.json");

    private void LoadCompatibility()
    {
        try
        {
            if (!File.Exists(CompatibilityPath)) return;
            Compatibility.FromJson(JObject.Parse(File.ReadAllText(CompatibilityPath)));
        }
        catch { /* optional */ }
    }

    public void SaveCompatibility()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CompatibilityPath)!);
        File.WriteAllText(CompatibilityPath, Compatibility.ToJson().ToString());
        AutosaveProject();
    }

    private static string CameraPath => FirstPersonCameraKit.Settings.SettingsPath;
    private static string PartyFivePath => PartyFiveKit.Settings.SettingsPath;

    private void LoadCamera()
    {
        try
        {
            if (File.Exists(CameraPath))
                Camera.FromJson(JObject.Parse(File.ReadAllText(CameraPath)));
        }
        catch { /* optional */ }
    }

    public void SaveCamera()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CameraPath)!);
        File.WriteAllText(CameraPath, Camera.ToJson().ToString());
        Status = Camera.Enabled
            ? "Camera saved. The CLI/standalone build writes it into Export/ExpeditionInfinity_<seed>_P."
            : "Camera submodule off. The CLI/standalone build reverts to the vanilla third-person camera.";
        AutosaveProject();
    }

    private void LoadPartyFive()
    {
        try
        {
            // Settings.Load also reads the legacy work/party_four.json once so
            // an upgrading install keeps its formation.
            PartyFive.FromJson(PartyFiveKit.Settings.Load().ToJson());
        }
        catch { /* optional */ }
    }

    public void SavePartyFive()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PartyFivePath)!);
        File.WriteAllText(PartyFivePath, PartyFive.ToJson().ToString());
        Status = PartyFive.Enabled
            ? "Party of Five saved. The CLI/standalone build writes it into Export/ExpeditionInfinity_<seed>_P."
            : "Party of Five submodule off. The CLI/standalone build ships the vanilla 3-member party.";
        AutosaveProject();
    }

    /// <summary>Auto-detects the Bigger Party Mod and stores its path when found.</summary>
    public string? DetectPartyMod()
    {
        var hit = CompatibilityKit.Detect();
        if (hit is not null) Compatibility.ModUtoc = hit;
        return hit;
    }

    /// <summary>Wired by the shell to the window's file picker (returns a .utoc path).</summary>
    public Func<Task<string?>>? PickPartyModFile { get; set; }

    private static string RandomizerPath =>
        Path.Combine(EditorPaths.RepoRoot, "work", "randomizer.json");

    private void LoadRandomizer()
    {
        try
        {
            if (!File.Exists(RandomizerPath)) return;
            Randomizer.FromJson(JObject.Parse(File.ReadAllText(RandomizerPath)));
        }
        catch { /* optional */ }
    }

    public void SaveRandomizer()
    {
        if (_restoringProject) return;
        Directory.CreateDirectory(Path.GetDirectoryName(RandomizerPath)!);
        File.WriteAllText(RandomizerPath, Randomizer.ToJson().ToString());
        AutosaveProject();
        Status = "Randomizer settings saved. The CLI/standalone build writes them into Export/ExpeditionInfinity_<seed>_P.";
    }

    private bool _restoringProject;

    private EditorProject CaptureProject() =>
        new()
        {
            SavedAt = DateTime.UtcNow.ToString("o"),
            GameplayTweaks = TweaksJson(),
            PictosCreate = new JObject
            {
                ["PainterConversion"] = PainterConversionEnabled,
                ["StatusAffinity"] = StatusAffinityEnabled,
                ["PictosMagnitude"] = PictosMagnitudeEnabled,
                ["DamageLimit"] = DamageLimitEnabled,
                ["WeaponEffects"] = WeaponEffectsEnabled,
            },
            Randomizer = Randomizer.ToJson(),
            Edits = CollectPendingEdits(),
            SkillOverhaul = SkillOverhaul.ToJson(),
            SkillGraph = SkillGraph.ToJson(),
            Compatibility = Compatibility.ToJson(),
            Camera = Camera.ToJson(),
            PartyFive = PartyFive.ToJson(),
            Language = SelectedPackLanguage.Code,
        };

    private JObject TweaksJson() => GameplayTweaks.ToJson();

    private JArray CollectPendingEdits()
    {
        var list = new JArray();
        // One destination = one entry: the same row object is aliased under
        // several keys (e.g. monsters + DT_jRPG_Enemies), so dedupe by
        // destination to keep sidecars/autosaves free of doubles.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in _rowsByKey.SelectMany(kv => kv.Value).Where(r => r.IsDirty))
        {
            foreach (var (displayPath, val) in row.AdvancedEdits)
            {
                var dest = row.Fields.TryGetValue(displayPath, out var fr)
                    ? fr
                    : new FieldRef(row.PrimaryTable, row.RowName, displayPath, row.IsAsset);
                if (!seen.Add($"{dest.Table}\0{dest.RowName}\0{dest.Path}")) continue;
                list.Add(new JObject
                {
                    ["table"] = dest.Table,
                    ["row"] = dest.RowName,
                    ["path"] = dest.Path,
                    ["displayPath"] = displayPath,
                    ["isAsset"] = dest.IsAsset,
                    ["value"] = val,
                });
            }
        }
        return list;
    }

    private void AutosaveProject()
    {
        if (_restoringProject || IsLoading) return;
        try { CaptureProject().Write(EditorProject.AutosavePath); }
        catch { /* disk full / first run */ }
    }

    private bool TryRestoreAutosave()
    {
        var p = EditorProject.TryRead(EditorProject.AutosavePath);
        if (p is null) return false;
        ApplyProject(p);
        return true;
    }

    public async Task SaveProjectPickedAsync()
    {
        var path = PickSaveProjectPath is null
            ? Path.Combine(EditorPaths.ExportDir, "E33ModEditor.project.json")
            : await PickSaveProjectPath();
        if (string.IsNullOrWhiteSpace(path)) return;
        SaveProjectTo(path);
    }

    public async Task LoadProjectPickedAsync()
    {
        var path = PickLoadProjectPath is null ? EditorProject.SidecarPath : await PickLoadProjectPath();
        if (string.IsNullOrWhiteSpace(path)) return;
        LoadProjectFrom(path);
    }

    public void SaveProjectTo(string path)
    {
        CaptureProject().Write(path);
        AutosaveProject();
        Status = $"Saved working project to {path}";
    }

    public void LoadProjectFrom(string path)
    {
        var p = EditorProject.TryRead(path);
        if (p is null)
        {
            Status = $"Could not read project '{path}'.";
            return;
        }
        ApplyProject(p);
        AutosaveProject();
        Raise(nameof(ActiveModule));
        Status = $"Loaded project from {path}. Use the CLI/standalone build to write Export/ExpeditionInfinity_<seed>_P.";
    }

    private void ApplyProject(EditorProject p)
    {
        _restoringProject = true;
        try
        {
            ApplyTweaksObject(p.GameplayTweaks);
            if (p.PictosCreate.Count > 0)
            {
                _painterConversionEnabled = p.PictosCreate.Value<bool?>("PainterConversion") ?? false;
                _statusAffinityEnabled = p.PictosCreate.Value<bool?>("StatusAffinity") ?? false;
                _pictosMagnitudeEnabled = p.PictosCreate.Value<bool?>("PictosMagnitude") ?? false;
                _weaponEffectsEnabled = p.PictosCreate.Value<bool?>("WeaponEffects")
                    ?? WeaponEffectsCreateKit.Specs.Count > 0;
                Raise(nameof(PainterConversionEnabled));
                Raise(nameof(StatusAffinityEnabled));
                Raise(nameof(PictosMagnitudeEnabled));
                Raise(nameof(WeaponEffectsEnabled));
                SaveCreateToggles();
            }
            ApplyPendingEdits(p.Edits);
            SelectedPackLanguage = ModLocalization.Find(p.Language) ?? ModLocalization.English;
            Randomizer.FromJson(p.Randomizer);
            SkillOverhaul.FromJson(p.SkillOverhaul);
            SkillGraph.FromJson(p.SkillGraph);
            if (p.Compatibility.Count > 0)
            {
                Compatibility.FromJson(p.Compatibility);
                File.WriteAllText(CompatibilityPath, Compatibility.ToJson().ToString());
            }
            if (p.Camera.Count > 0)
            {
                Camera.FromJson(p.Camera);
                File.WriteAllText(CameraPath, Camera.ToJson().ToString());
            }
            if (p.PartyFive.Count > 0)
            {
                PartyFive.FromJson(p.PartyFive);
                File.WriteAllText(PartyFivePath, PartyFive.ToJson().ToString());
            }
            File.WriteAllText(GameplayTweaksPath, TweaksJson().ToString());
            if (SkillOverhaul.HasRecorded)
                File.WriteAllText(SkillOverhaulPath, SkillOverhaul.ToJson().ToString());
            if (SkillGraph.HasRecorded)
                File.WriteAllText(SkillGraphPath, SkillGraph.ToJson().ToString());
            Directory.CreateDirectory(Path.GetDirectoryName(RandomizerPath)!);
            File.WriteAllText(RandomizerPath, Randomizer.ToJson().ToString());
        }
        finally
        {
            _restoringProject = false;
        }
    }

    private void ApplyTweaksObject(JObject o) => GameplayTweaks.FromJson(o);

    private void ApplyPendingEdits(JArray edits)
    {
        foreach (var token in edits)
        {
            if (token is not JObject o) continue;
            var table = o.Value<string>("table") ?? "";
            var rowName = o.Value<string>("row") ?? "";
            var path = o.Value<string>("path") ?? "";
            var display = o.Value<string>("displayPath") ?? path;
            var isAsset = o.Value<bool?>("isAsset") ?? false;
            var value = o["value"];
            if (value is null || table.Length == 0 || path.Length == 0) continue;
            foreach (var row in _rowsByKey.SelectMany(kv => kv.Value))
            {
                var matches = (row.PrimaryTable == table && row.RowName == rowName)
                    || row.Fields.Values.Any(f => f.Table == table && f.RowName == rowName);
                if (!matches) continue;
                row.Fields[display] = new FieldRef(table, rowName, path, isAsset);
                QueueToken(row, display, value);
            }
        }
    }

    private static void CopyInto(string src, string dst)
    {
        if (!Directory.Exists(src)) return;
        Directory.CreateDirectory(dst);
        foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(src, dst));
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            FileCopy.Overwrite(file, file.Replace(src, dst));
    }

    private static void CopyAssetPair(string uassetPath, string modDir)
    {
        var idx = uassetPath.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return;
        var dest = Path.Combine(modDir, uassetPath[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(uassetPath, dest);
    }

    private static IEnumerable<string> FlattenKeys(JToken token, string prefix = "")
    {
        if (token is JObject o)
        {
            foreach (var p in o.Properties())
            {
                var path = prefix.Length == 0 ? p.Name : $"{prefix}.{p.Name}";
                if (p.Value is JObject or JArray)
                    foreach (var c in FlattenKeys(p.Value, path)) yield return c;
                else yield return path;
            }
        }
        else if (token is JArray a)
        {
            for (int i = 0; i < a.Count; i++)
            {
                var path = $"{prefix}.{i}";
                if (a[i] is JObject or JArray)
                    foreach (var c in FlattenKeys(a[i], path)) yield return c;
                else yield return path;
            }
        }
        else if (prefix.Length > 0) yield return prefix;
    }

    internal static JToken? TokenAt(JObject root, string path)
    {
        if (string.IsNullOrEmpty(path)) return root;
        // Flattened DA leaves are stored as a single key that may itself contain
        // dots ("SkillComponentList.0"). Try the exact key before splitting.
        if (root.TryGetValue(path, out var exact)) return exact;
        // Do not use JToken.SelectToken: paths contain '::' (map enum keys) and
        // JSONPath treats ':' specially, which can hang or allocate without bound.
        JToken cur = root;
        foreach (var seg in path.Split('.'))
        {
            if (cur is JObject jo)
            {
                if (jo.TryGetValue(seg, out var next)) cur = next;
                else return null;
            }
            else if (cur is JArray ja && int.TryParse(seg, out var i) && i >= 0 && i < ja.Count)
                cur = ja[i]!;
            else return null;
        }
        return cur;
    }

    private static void SetToken(JObject root, string path, JToken value)
    {
        var segs = path.Split('.');
        JToken cur = root;
        for (int i = 0; i < segs.Length - 1; i++)
        {
            if (cur is not JObject jo) return;
            if (jo[segs[i]] is not JObject next)
            {
                next = new JObject();
                jo[segs[i]] = next;
            }
            cur = next;
        }
        if (cur is JObject last) last[segs[^1]] = value;
    }
}

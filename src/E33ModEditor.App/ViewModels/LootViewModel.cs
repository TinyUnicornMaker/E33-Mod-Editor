using System.Collections.ObjectModel;
using E33ModEditor.Core;
using E33ModEditor.Core.Loot;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.App.ViewModels;

/// <summary>Chests / Drops browser: in-game map, tiers, frequency estimates.</summary>
public sealed class LootViewModel : ViewModelBase
{
    public LootCatalog Catalog { get; } = new();

    public ObservableCollection<LootCatalog.Check> Visible { get; } = new();
    public ObservableCollection<LootCatalog.FreqRow> Frequencies { get; } = new();

    public string[] Sources { get; } = { "All", "Chest", LootCatalog.PaintCageSource, "Enemy", "Merchant" };
    public string[] Tiers { get; } = { "All", "1", "2", "3", "4", "5" };

    private string _source = "All";
    public string SourceFilter
    {
        get => _source;
        set { if (Set(ref _source, value)) ApplyFilter(); }
    }

    private string _tier = "All";
    public string TierFilter
    {
        get => _tier;
        set { if (Set(ref _tier, value)) ApplyFilter(); }
    }

    private string _area = "All";
    public string AreaFilter
    {
        get => _area;
        set { if (Set(ref _area, value)) ApplyFilter(); }
    }

    private string _search = "";
    public string Search
    {
        get => _search;
        set { if (Set(ref _search, value)) ApplyFilter(); }
    }

    public List<string> Areas { get; private set; } = new() { "All" };

    public string Summary { get; private set; } = "Chests and drops load with the rest of the editor.";

    private LootCatalog.Check? _selected;
    public LootCatalog.Check? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
                Raise(nameof(SelectedLabel));
        }
    }

    public string SelectedLabel =>
        Selected is null
            ? "Click a row to change its drop. Type in the box to filter by any substring of the item name or id."
            : $"{Selected.Source}  ·  {Selected.DisplayName}  ·  T{Selected.Tier}  {Selected.Area}";

    public List<ItemChoice> ItemChoices { get; private set; } = new();

    /// <summary>Pending DataTable edits keyed by table stem.</summary>
    public Dictionary<string, JObject> PendingEdits { get; } = new(StringComparer.Ordinal);
    public bool HasPending => PendingEdits.Count > 0;

    public Func<LootCatalog.Check, LootCatalog.Slot, string, bool>? ApplySlot { get; set; }

    public void Select(LootCatalog.Check check) => Selected = check;

    public bool TrySetSlot(LootCatalog.Slot slot, string newId)
    {
        if (slot.LootTableRef || string.IsNullOrWhiteSpace(newId) || slot.ItemId == newId)
            return false;
        var check = Selected;
        if (check is null) return false;
        if (ApplySlot is not null && !ApplySlot(check, slot, newId))
            return false;
        QueueEdit(slot.Table, slot.RowName, slot.Path, newId);
        var info = Catalog.Describe(newId);
        slot.ItemId = newId;
        slot.ItemName = info.DisplayName;
        slot.ItemType = Catalog.TypeOf(newId);
        ApplyFilter();
        Raise(nameof(SelectedLabel));
        return true;
    }

    private void QueueEdit(string table, string row, string path, string value)
    {
        if (string.IsNullOrEmpty(table) || string.IsNullOrEmpty(path)) return;
        if (!PendingEdits.TryGetValue(table, out var o))
            PendingEdits[table] = o = new JObject();
        if (o[row] is not JObject fields)
            o[row] = fields = new JObject();
        fields[path] = value;
    }

    public void Rebuild(JObject chests, JObject enemies, IReadOnlyList<(string File, JObject Rows)> merchants)
    {
        Catalog.RegisterExtraPictos(
            PictosCreateKit.PainterConversions.Select(p => (p.Id, p.DisplayName))
                .Concat(PictosCreateKit.Powers.Select(p => (p.Id, p.DisplayName)))
                .Concat(PictosMagnitudeKit.Variants.Select(p => (p.Id, p.DisplayName)))
                .Concat(BombPictosKit.CreatedPictos)
                .Concat(ReviveDebuffKit.CreatedPictos)
                .Concat(PictosCreateKit.HitBurn.Select(p => (p.Id, p.DisplayName))));
        Catalog.Rebuild(chests, enemies, merchants);
        ItemChoices = Catalog.Items.Values
            .OrderBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(i => new ItemChoice(i.CodeName, i.DisplayName, i.Type))
            .ToList();
        Areas = new List<string> { "All" };
        Areas.AddRange(Catalog.Checks.Select(c => c.Area).Where(a => a.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(a => a, StringComparer.OrdinalIgnoreCase));
        Frequencies.Clear();
        foreach (var f in Catalog.Frequencies) Frequencies.Add(f);
        var chestsN = Catalog.Checks.Count(c => c.Source == "Chest");
        var paintCagesN = Catalog.Checks.Count(c => c.Source == LootCatalog.PaintCageSource);
        var enemyN = Catalog.Checks.Count(c => c.Source == "Enemy");
        var merchN = Catalog.Checks.Count(c => c.Source == "Merchant");
        var pictosChests = Catalog.Checks.Where(c => c.Source == "Chest" || c.Source == LootCatalog.PaintCageSource)
            .SelectMany(c => c.Slots).Count(s => s.ItemType == "Pictos");
        var pictosDrops = Catalog.Checks.Where(c => c.Source == "Enemy")
            .SelectMany(c => c.Slots).Where(s => s.ItemType == "Pictos").Sum(s => s.Chance / 100.0);
        Summary =
            $"{chestsN} chests, {paintCagesN} paint cages, {enemyN} enemies, {merchN} shops. " +
            $"{pictosChests} pictos sit in chests/cages; enemy pictos drops expect ~{pictosDrops:0.#} rolls. " +
            "Paint Cages always roll one tier-appropriate pictos or weapon. " +
            "Tiers are the original randomizer bands (Lv 1–20 = T1 … 81+ = T5) using each area's interior level.";
        ApplyFilter();
        Raise(nameof(Areas));
        Raise(nameof(Summary));
    }

    private void ApplyFilter()
    {
        IEnumerable<LootCatalog.Check> q = Catalog.Checks;
        if (SourceFilter != "All")
            q = q.Where(c => c.Source == SourceFilter);
        if (TierFilter != "All" && int.TryParse(TierFilter, out var t))
            q = q.Where(c => c.Tier == t);
        if (AreaFilter != "All" && AreaFilter.Length > 0)
            q = q.Where(c => c.Area == AreaFilter);
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim();
            q = q.Where(c =>
                c.DisplayName.Contains(s, StringComparison.OrdinalIgnoreCase)
                || c.Id.Contains(s, StringComparison.OrdinalIgnoreCase)
                || c.PrimaryItem.Contains(s, StringComparison.OrdinalIgnoreCase)
                || c.Slots.Any(x => x.ItemName.Contains(s, StringComparison.OrdinalIgnoreCase)
                                    || x.ItemId.Contains(s, StringComparison.OrdinalIgnoreCase)));
        }
        Visible.Clear();
        foreach (var c in q.OrderBy(c => c.AreaLevel).ThenBy(c => c.Area).ThenBy(c => c.DisplayName))
            Visible.Add(c);
    }
}

public sealed record ItemChoice(string Id, string Display, string Type)
{
    public string Label => string.IsNullOrEmpty(Display) || Display == Id ? Id : $"{Display}  ({Id})";
    public override string ToString() => Label;
}

/// <summary>Randomizer module settings (persisted with the editor project).</summary>
public sealed class RandomizerViewModel : ViewModelBase
{
    public const int SeedMin = 0;
    public const int SeedMax = 99_999_999;

    public static int NewRandomSeed() => Random.Shared.Next(SeedMin, SeedMax + 1);

    /// <summary>Rolls a fresh explicit seed (dice button: new number every click).</summary>
    public void RandomizeSeed() => Seed = NewRandomSeed();

    public RandomizerKit.Settings Settings { get; } = new() { Seed = 42 };

    public int Seed
    {
        get => Settings.Seed;
        set { Settings.Seed = Math.Clamp(value, SeedMin, SeedMax); Raise(); Persist?.Invoke(); }
    }
    public bool Chests
    {
        get => Settings.Chests;
        set { Settings.Chests = value; Raise(); Persist?.Invoke(); }
    }
    public bool PaintCages
    {
        get => Settings.PaintCages;
        set { Settings.PaintCages = value; Raise(); Persist?.Invoke(); }
    }
    public bool EnemyDrops
    {
        get => Settings.EnemyDrops;
        set { Settings.EnemyDrops = value; Raise(); Persist?.Invoke(); }
    }
    public bool Merchants
    {
        get => Settings.Merchants;
        set { Settings.Merchants = value; Raise(); Persist?.Invoke(); }
    }
    public bool WeaponPassives
    {
        get => Settings.WeaponPassives;
        set { Settings.WeaponPassives = value; Raise(); Persist?.Invoke(); }
    }
    public bool MonsterStats
    {
        get => Settings.MonsterStats;
        set { Settings.MonsterStats = value; Raise(); Persist?.Invoke(); }
    }
    public bool WeaponStats
    {
        get => Settings.WeaponStats;
        set { Settings.WeaponStats = value; Raise(); Persist?.Invoke(); }
    }

    public bool BattleGroups
    {
        get => Settings.BattleGroups;
        set { Settings.BattleGroups = value; Raise(); Persist?.Invoke(); }
    }

    public int MaxEnemiesPerBattle
    {
        get => Settings.MaxEnemiesPerBattle;
        set { Settings.MaxEnemiesPerBattle = Math.Clamp(value, 1, BattleGroupKit.FieldCapacity); Raise(); Persist?.Invoke(); }
    }

    public double ExtraEnemiesChance
    {
        get => Settings.ExtraEnemiesChance;
        set { Settings.ExtraEnemiesChance = Math.Clamp(value, 0, 100); Raise(); Persist?.Invoke(); }
    }

    public bool ShuffleBattleRosters
    {
        get => Settings.ShuffleBattleRosters;
        set { Settings.ShuffleBattleRosters = value; Raise(); Persist?.Invoke(); }
    }

    public bool RandomBattles
    {
        get => Settings.RandomBattles;
        set { Settings.RandomBattles = value; Raise(); Persist?.Invoke(); }
    }

    public int BattleVariants
    {
        get => Settings.BattleVariants;
        set { Settings.BattleVariants = Math.Clamp(value, 1, 8); Raise(); Persist?.Invoke(); }
    }

    /// <summary>Optional encounter-id prefix for the test area (GO_ = Flying Waters).</summary>
    public string BattleGroupsAreaPrefix
    {
        get => Settings.BattleGroupsAreaPrefix;
        set
        {
            Settings.BattleGroupsAreaPrefix = (value ?? "").Trim();
            Raise();
            Persist?.Invoke();
        }
    }

    public Action? Persist { get; set; }

    public JObject ToJson() => new()
    {
        ["Seed"] = Settings.Seed,
        ["Chests"] = Settings.Chests,
        ["PaintCages"] = Settings.PaintCages,
        ["EnemyDrops"] = Settings.EnemyDrops,
        ["Merchants"] = Settings.Merchants,
        ["WeaponPassives"] = Settings.WeaponPassives,
        ["MonsterStats"] = Settings.MonsterStats,
        ["WeaponStats"] = Settings.WeaponStats,
        ["BattleGroups"] = Settings.BattleGroups,
        ["MaxEnemiesPerBattle"] = Settings.MaxEnemiesPerBattle,
        ["ExtraEnemiesChance"] = Settings.ExtraEnemiesChance,
        ["ShuffleBattleRosters"] = Settings.ShuffleBattleRosters,
        ["RandomBattles"] = Settings.RandomBattles,
        ["BattleVariants"] = Settings.BattleVariants,
        ["BattleGroupsAreaPrefix"] = Settings.BattleGroupsAreaPrefix,
        ["StatLo"] = new JArray(Settings.StatLo),
        ["StatHi"] = new JArray(Settings.StatHi),
        ["BossHp"] = new JArray(Settings.BossHp),
        ["AttackLo"] = new JArray(Settings.AttackLo),
        ["AttackHi"] = new JArray(Settings.AttackHi),
        ["RegularPictosChance"] = Settings.RegularPictosChance,
        ["ElitePictosChance"] = Settings.ElitePictosChance,
        ["T5PerfectCatalystChance"] = Settings.T5PerfectCatalystChance,
        ["PetankLumina"] = Settings.PetankLumina,
        ["MerchantGearSlots"] = Settings.MerchantGearSlots,
        ["TintsPerType"] = Settings.TintsPerType,
        ["ChromaElixirShards"] = Settings.ChromaElixirShards,
        ["ShapesPerType"] = Settings.ShapesPerType,
    };

    public void FromJson(JObject o)
    {
        if (o.Count == 0) return;
        var seed = o.Value<int?>("Seed") ?? 42;
        // Legacy "-1 = new seed on each pack" projects become an explicit seed.
        if (seed < SeedMin) seed = NewRandomSeed();
        Settings.Seed = Math.Clamp(seed, SeedMin, SeedMax);
        Settings.Chests = o.Value<bool?>("Chests") ?? true;
        Settings.PaintCages = o.Value<bool?>("PaintCages") ?? true;
        Settings.EnemyDrops = o.Value<bool?>("EnemyDrops") ?? true;
        Settings.Merchants = o.Value<bool?>("Merchants") ?? true;
        Settings.WeaponPassives = o.Value<bool?>("WeaponPassives") ?? true;
        Settings.MonsterStats = o.Value<bool?>("MonsterStats") ?? true;
        Settings.WeaponStats = o.Value<bool?>("WeaponStats") ?? true;
        Settings.BattleGroups = o.Value<bool?>("BattleGroups") ?? false;
        Settings.MaxEnemiesPerBattle = Math.Clamp(o.Value<int?>("MaxEnemiesPerBattle") ?? 5, 1, BattleGroupKit.FieldCapacity);
        Settings.ExtraEnemiesChance = Math.Clamp(o.Value<double?>("ExtraEnemiesChance") ?? 50, 0, 100);
        Settings.ShuffleBattleRosters = o.Value<bool?>("ShuffleBattleRosters") ?? true;
        Settings.RandomBattles = o.Value<bool?>("RandomBattles") ?? false;
        Settings.BattleVariants = Math.Clamp(o.Value<int?>("BattleVariants") ?? 3, 1, 8);
        Settings.BattleGroupsAreaPrefix = (o.Value<string>("BattleGroupsAreaPrefix") ?? "").Trim();
        Settings.StatLo = Doubles(o["StatLo"], E33ModEditor.Core.Loot.RandomizerKit.DefaultStatLo);
        Settings.StatHi = Doubles(o["StatHi"], E33ModEditor.Core.Loot.RandomizerKit.DefaultStatHi);
        Settings.BossHp = Doubles(o["BossHp"], E33ModEditor.Core.Loot.RandomizerKit.DefaultBossHp);
        Settings.AttackLo = Doubles(o["AttackLo"], E33ModEditor.Core.Loot.RandomizerKit.DefaultAttackLo);
        Settings.AttackHi = Doubles(o["AttackHi"], E33ModEditor.Core.Loot.RandomizerKit.DefaultAttackHi);
        Settings.RegularPictosChance = o.Value<double?>("RegularPictosChance") ?? 10;
        Settings.ElitePictosChance = o.Value<double?>("ElitePictosChance") ?? 66;
        Settings.T5PerfectCatalystChance = o.Value<double?>("T5PerfectCatalystChance") ?? 3.125;
        Settings.PetankLumina = o.Value<int?>("PetankLumina") ?? 50;
        Settings.MerchantGearSlots = o.Value<int?>("MerchantGearSlots") ?? 4;
        Settings.TintsPerType = o.Value<int?>("TintsPerType") ?? 9;
        Settings.ChromaElixirShards = o.Value<int?>("ChromaElixirShards") ?? 2;
        Settings.ShapesPerType = o.Value<int?>("ShapesPerType") ?? 2;
        Raise(nameof(Seed)); Raise(nameof(Chests)); Raise(nameof(PaintCages)); Raise(nameof(EnemyDrops));
        Raise(nameof(Merchants));
        Raise(nameof(WeaponPassives)); Raise(nameof(MonsterStats)); Raise(nameof(WeaponStats));
        Raise(nameof(BattleGroups)); Raise(nameof(MaxEnemiesPerBattle));
        Raise(nameof(ExtraEnemiesChance)); Raise(nameof(ShuffleBattleRosters));
        Raise(nameof(RandomBattles)); Raise(nameof(BattleVariants));
        Raise(nameof(BattleGroupsAreaPrefix));
    }

    private static double[] Doubles(JToken? t, double[] fallback)
    {
        if (t is JArray ja && ja.Count >= 5)
            return ja.Take(5).Select(x => (double?)x ?? 0).ToArray();
        return (double[])fallback.Clone();
    }
}

/// <summary>Pictos / Weapon T1–T5 + weapon-effect tiers and character gates.</summary>
public sealed class TierEditorViewModel : ViewModelBase
{
    public const string PagePictos = "Pictos";
    public const string PageWeapons = "Weapons";
    public const string PageEffects = "Effects";
    public const string PageCharacters = "Characters";

    public LootCatalog Catalog { get; set; } = new();
    public Func<string, (string Name, string Description)>? Describe { get; set; }

    public string[] Categories { get; } = { ItemTiers.Pictos, ItemTiers.Weapon };

    private string _page = PagePictos;
    public string Page
    {
        get => _page;
        set
        {
            if (!Set(ref _page, value)) return;
            if (value == PagePictos) _category = ItemTiers.Pictos;
            if (value == PageWeapons) _category = ItemTiers.Weapon;
            Raise(nameof(IsCharacterPage));
            Raise(nameof(IsColumnPage));
            Refresh();
        }
    }

    public bool IsCharacterPage => Page == PageCharacters;
    public bool IsColumnPage => Page != PageCharacters;

    private string _category = ItemTiers.Pictos;
    public string Category
    {
        get => _category;
        set
        {
            if (!Set(ref _category, value)) return;
            _page = value == ItemTiers.Weapon ? PageWeapons : PagePictos;
            Raise(nameof(Page));
            Refresh();
        }
    }

    private string _search = "";
    public string Search
    {
        get => _search;
        set { if (Set(ref _search, value)) Refresh(); }
    }

    private string? _selectedId;
    public string? SelectedId
    {
        get => _selectedId;
        set { if (Set(ref _selectedId, value)) Raise(nameof(SelectedLabel)); }
    }

    public string SelectedLabel =>
        Page == PageCharacters
            ? "Click a character name to allow or block that effect."
            : string.IsNullOrEmpty(SelectedId)
                ? "Click an item, then click a column header to move it."
                : $"Selected: {LabelOf(SelectedId)} — click T1–T5 or Do Not Use to place it.";

    public string Stats { get; private set; } = "";

    public ObservableCollection<TierCard> Unplaced { get; } = new();
    public ObservableCollection<TierCard> T1 { get; } = new();
    public ObservableCollection<TierCard> T2 { get; } = new();
    public ObservableCollection<TierCard> T3 { get; } = new();
    public ObservableCollection<TierCard> T4 { get; } = new();
    public ObservableCollection<TierCard> T5 { get; } = new();
    public ObservableCollection<TierCard> Dnu { get; } = new();
    public ObservableCollection<EffectCharRow> CharacterRows { get; } = new();
    public string[] CharacterRoster => WeaponEffectCharacters.Roster;

    public void Attach(LootCatalog catalog)
    {
        WeaponEffectsCreateKit.Reload();
        Catalog = catalog;
        Catalog.RegisterExtraPictos(
            PictosCreateKit.PainterConversions.Select(p => (p.Id, p.DisplayName))
                .Concat(PictosCreateKit.Powers.Select(p => (p.Id, p.DisplayName)))
                .Concat(PictosMagnitudeKit.Variants.Select(p => (p.Id, p.DisplayName)))
                .Concat(BombPictosKit.CreatedPictos)
                .Concat(ReviveDebuffKit.CreatedPictos)
                .Concat(PictosCreateKit.HitBurn.Select(p => (p.Id, p.DisplayName))));
        Refresh();
    }

    public void MoveSelectedTo(string column)
    {
        if (string.IsNullOrEmpty(SelectedId)) return;
        if (Page == PageEffects)
            Catalog.EffectTiers.Move(SelectedId, column);
        else if (Page is PagePictos or PageWeapons)
            Catalog.ItemTiers.Move(Category, SelectedId, column);
        else return;
        Persist();
        Refresh();
    }

    public void MoveAllToDoNotUse()
    {
        if (Page == PageEffects)
            Catalog.EffectTiers.MoveAllToDoNotUse();
        else if (Page is PagePictos or PageWeapons)
            Catalog.ItemTiers.MoveAllToDoNotUse(Category);
        else return;
        Persist();
        Refresh();
    }

    public void ResetToStock()
    {
        if (Page == PageEffects)
            Catalog.EffectTiers.ResetToStock();
        else if (Page == PageCharacters)
        {
            Catalog.EffectCharacters.ResetToStock();
            Catalog.EffectCharacters.ApplyGustaveRules(Catalog.EffectTiers);
        }
        else
            Catalog.ItemTiers.ResetToStock();
        Persist();
        Refresh();
    }

    public void Persist()
    {
        Catalog.ItemTiers.Save();
        Catalog.ItemTiers.RebuildFlat();
        Catalog.EffectTiers.Save();
        Catalog.EffectCharacters.Save();
    }

    public void ToggleCharacter(string effectId, string character)
    {
        if (WeaponEffectTiers.IsGustave(character) && WeaponEffectTiers.IsAutoDeath(effectId))
            return;
        Catalog.EffectCharacters.Toggle(effectId, character);
        if (WeaponEffectTiers.IsGustave(character))
            Catalog.EffectCharacters.ApplyGustaveRules(Catalog.EffectTiers);
        Catalog.EffectCharacters.Save();
    }

    public bool CharacterAllowed(string effectId, string character) =>
        Catalog.EffectCharacters.Allows(effectId, character);

    public void Refresh()
    {
        if (Page == PageCharacters)
        {
            FillCharacters();
            Stats = $"{CharacterRows.Count} effects — click a name to allow or block.";
            Raise(nameof(Stats));
            Raise(nameof(SelectedLabel));
            return;
        }
        if (Page == PageEffects)
        {
            Fill(Unplaced, Catalog.EffectTiers.Unplaced(KnownEffectIds()));
            Fill(T1, Catalog.EffectTiers.Column("Tier1"));
            Fill(T2, Catalog.EffectTiers.Column("Tier2"));
            Fill(T3, Catalog.EffectTiers.Column("Tier3"));
            Fill(T4, Catalog.EffectTiers.Column("Tier4"));
            Fill(T5, Catalog.EffectTiers.Column("Tier5"));
            Fill(Dnu, Catalog.EffectTiers.Column("DoNotUse"));
            var placed = T1.Count + T2.Count + T3.Count + T4.Count + T5.Count;
            Stats = $"Effects: {placed} in T1–T5, {Dnu.Count} Do Not Use, {Unplaced.Count} unplaced (created effects land here until you assign them).";
        }
        else
        {
            Fill(Unplaced, Catalog.ItemTiers.Unplaced(Category, KnownIds()));
            Fill(T1, Catalog.ItemTiers.Column(Category, "Tier1"));
            Fill(T2, Catalog.ItemTiers.Column(Category, "Tier2"));
            Fill(T3, Catalog.ItemTiers.Column(Category, "Tier3"));
            Fill(T4, Catalog.ItemTiers.Column(Category, "Tier4"));
            Fill(T5, Catalog.ItemTiers.Column(Category, "Tier5"));
            Fill(Dnu, Catalog.ItemTiers.Column(Category, "DoNotUse"));
            var placed = T1.Count + T2.Count + T3.Count + T4.Count + T5.Count;
            Stats = $"{Category}: {placed} in T1–T5, {Dnu.Count} Do Not Use, {Unplaced.Count} unplaced (created pictos land here until you assign them).";
        }
        Raise(nameof(Stats));
        Raise(nameof(SelectedLabel));
    }

    private IEnumerable<string> KnownIds()
    {
        if (Category == ItemTiers.Weapon)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var i in Catalog.Items.Values)
            {
                if (string.Equals(i.Type, "Weapon", StringComparison.OrdinalIgnoreCase))
                    set.Add(i.CodeName);
            }
            foreach (var col in ItemTiers.Columns)
                foreach (var id in Catalog.ItemTiers.Column(ItemTiers.Weapon, col))
                    set.Add(id);
            return set;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var i in Catalog.Items.Values)
        {
            if (Catalog.TypeOf(i.CodeName) == "Pictos"
                || string.Equals(i.Type, "Pictos", StringComparison.OrdinalIgnoreCase))
                ids.Add(i.CodeName);
        }
        foreach (var s in PictosCreateKit.PainterConversions) ids.Add(s.Id);
        foreach (var s in PictosCreateKit.Powers) ids.Add(s.Id);
        foreach (var s in PictosCreateKit.HitBurn) ids.Add(s.Id);
        foreach (var s in PictosMagnitudeKit.Variants) ids.Add(s.Id);
        return ids;
    }

    private IEnumerable<string> KnownEffectIds()
    {
        var set = new HashSet<string>(Catalog.EffectCharacters.AllIds(), StringComparer.Ordinal);
        foreach (var spec in WeaponEffectsCreateKit.Specs)
            set.Add(spec.Id);
        foreach (var col in WeaponEffectTiers.Columns)
            foreach (var id in Catalog.EffectTiers.Column(col))
                set.Add(id);
        return set;
    }

    private void FillCharacters()
    {
        CharacterRows.Clear();
        var q = Search.Trim();
        foreach (var id in KnownEffectIds().OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            var label = LabelOf(id);
            if (q.Length > 0
                && !id.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !label.Contains(q, StringComparison.OrdinalIgnoreCase))
                continue;
            CharacterRows.Add(new EffectCharRow(id, label, DescriptionOf(id)));
        }
    }

    private void Fill(ObservableCollection<TierCard> dest, IEnumerable<string> ids)
    {
        dest.Clear();
        var q = Search.Trim();
        foreach (var id in ids)
        {
            var label = LabelOf(id);
            if (q.Length > 0
                && !id.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !label.Contains(q, StringComparison.OrdinalIgnoreCase))
                continue;
            dest.Add(new TierCard(id, label, DescriptionOf(id)));
        }
    }

    private string LabelOf(string id)
    {
        if (Describe is { } d)
        {
            var (n, _) = d(id);
            if (!string.IsNullOrEmpty(n) && n != id) return n;
        }
        if (WeaponEffectsCreateKit.TryGet(id, out var spec))
            return spec.DisplayName;
        var info = Catalog.Describe(id);
        if (!string.IsNullOrEmpty(info.DisplayName) && info.DisplayName != id
            && info.Type is "Pictos" or "Weapon")
            return info.DisplayName;
        return GameLabels.Humanize(id);
    }

    private string DescriptionOf(string id)
    {
        if (Describe is { } d)
        {
            var (_, desc) = d(id);
            if (!string.IsNullOrEmpty(desc)) return desc;
        }
        if (WeaponEffectsCreateKit.TryGet(id, out var spec) && !string.IsNullOrEmpty(spec.Description))
            return spec.Description;
        foreach (var p in PictosCreateKit.PainterConversions)
            if (string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) return p.Description;
        foreach (var p in PictosCreateKit.Powers)
            if (string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) return p.Description;
        return "";
    }
}

public sealed record TierCard(string Id, string Label, string Description = "");

public sealed record EffectCharRow(string Id, string Label, string Description = "");

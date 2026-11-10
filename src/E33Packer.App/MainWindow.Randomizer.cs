using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using static E33Packer.App.AppLocalization;

namespace E33Packer.App;

public sealed partial class MainWindow
{
    private Control BuildRandomizerTab()
    {
        var parts = new List<Control>
        {
            Note(T("The seed controls loot, Nevron stats and encounters. Core content is always included.")),
            Card(BuildSeedCard()),
            Card(BuildLootCard()),
            Card(BuildLootOptionsCard()),
        };
        return Stack(parts.ToArray());
    }

    private Control BuildSeedCard()
    {
        var r = _profile.Randomizer;
        var seedBox = new TextBox
        {
            Text = _profile.Seed.ToString(),
            Width = 150,
            FontFamily = new FontFamily("DejaVu Sans Mono, Consolas, monospace"),
        };
        _seedBox = seedBox;
        seedBox.TextChanged += (_, _) =>
        {
            if (int.TryParse(seedBox.Text, out var seed))
            {
                _profile.Seed = Math.Clamp(seed, 0, 99_999_999);
                _profile.Randomizer.Seed = _profile.Seed;
                if (_seedPackLabel is not null)
                    _seedPackLabel.Text = F("Pack: {0}", GeneratorInfo.PackName(_profile.Seed));
                if (_headerSeedLabel is not null) _headerSeedLabel.Text = _profile.Seed.ToString();
            }
        };
        seedBox.LostFocus += (_, _) => seedBox.Text = _profile.Seed.ToString();

        _seedPackLabel = new TextBlock
        {
            Text = F("Pack: {0}", GeneratorInfo.PackName(_profile.Seed)),
            Foreground = Muted,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };
        ToolTip.SetTip(_seedPackLabel, T("Name of the pack that will be generated."));

        _historyBox = new ComboBox { Width = 420 };
        ToolTip.SetTip(_historyBox, T("Recent builds, newest first."));
        RefreshHistory();
        var restore = new Button { Content = T("Restore seed + profile") };
        ToolTip.SetTip(restore, T("Reload the seed and all settings from the selected build."));
        restore.Click += (_, _) => RestoreHistory();

        var historyRow = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 2),
        };
        void AddHistory(Control child)
        {
            child.Margin = new Thickness(0, 0, 6, 4);
            historyRow.Children.Add(child);
        }
        AddHistory(Label(T("Seed history"), 170, T("Your last 20 builds.")));
        AddHistory(_historyBox);
        AddHistory(restore);

        var seedRow = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 2),
        };
        void AddSeed(Control child)
        {
            child.Margin = new Thickness(0, 0, 6, 4);
            seedRow.Children.Add(child);
        }
        AddSeed(Label(T("Seed"), 110, T("Same seed and profile give the same results.")));
        ToolTip.SetTip(_seedBox, T("Type a number from 0 to 99,999,999."));
        AddSeed(_seedBox);
        var dice = new Button { Content = "🎲 " + T("New seed") };
        ToolTip.SetTip(dice, T("Pick a random seed."));
        dice.Click += (_, _) =>
        {
            _profile.Seed = GeneratorPresets.NewSeed();
            _profile.Randomizer.Seed = _profile.Seed;
            _seedBox.Text = _profile.Seed.ToString();
            if (_seedPackLabel is not null)
                _seedPackLabel.Text = F("Pack: {0}", GeneratorInfo.PackName(_profile.Seed));
        };
        AddSeed(dice);

        return Stack(
            Head(T("Seed")),
            Note(T("Type a seed or roll a new one.")),
            seedRow,
            _seedPackLabel!,
            historyRow);
    }

    private Control BuildLootCard()
    {
        var r = _profile.Randomizer;
        return Stack(
            Head(T("Loot")),
            Note(T("Choose which loot sources the seed changes. Keys, journals, music and quest items stay vanilla.")),
            Toggle(T("Chests and pickups"), r.Chests, v => r.Chests = v,
                T("Pictos and weapons found in chests and pickups.")),
            Toggle(T("Paint Cages"), r.PaintCages, v => r.PaintCages = v,
                T("Each cage rolls one tier-appropriate picto or weapon.")),
            Toggle(T("Nevron drops"), r.EnemyDrops, v => r.EnemyDrops = v,
                T("Regular, elite, boss and overworld Nevron drops.")),
            Toggle(T("Merchant inventories"), r.Merchants, v => r.Merchants = v,
                T("Tier-appropriate shop stock.")),
            Note(T("All sources off: the seed changes nothing; core content and Balance/QoL still apply.")));
    }

    private Control BuildLootOptionsCard()
    {
        var r = _profile.Randomizer;
        var b = _profile.Balance;
        var multiplierRow = DoubleRow(T("Lumina multiplier"), b.LuminaDropMultiplier, 0.1, 5.0,
            v => b.LuminaDropMultiplier = v, T("× (0.5 = half, 2 = double)"),
            T("0.5 halves regular Lumina drops; 2 doubles them. Dialogue rewards are not changed."));
        multiplierRow.IsEnabled = b.ReduceLuminaDrops;
        var adjustLumina = Toggle(T("Adjust Lumina drops"), b.ReduceLuminaDrops,
            v =>
            {
                b.ReduceLuminaDrops = v;
                multiplierRow.IsEnabled = v;
            },
            T("Chests, Nevron drops and shops only. Dialogue rewards are untouched."));

        return Stack(
            Head(T("Loot options")),
            Note(T("Drop rates, weapon rolls and loot quantities.")),
            Toggle(T("Weapon passives"), r.WeaponPassives, v => r.WeaponPassives = v,
                T("Rolls the passive effect on weapons.")),
            Toggle(T("Weapon stats and elements"), r.WeaponStats, v => r.WeaponStats = v,
                T("Rolls weapon attack, free-aim and element.")),
            DoubleRow(T("Regular Nevron picto chance"), r.RegularPictosChance, 0, 100,
                v => r.RegularPictosChance = v, "%", T("Chance a regular Nevron drops a picto.")),
            DoubleRow(T("Elite Nevron picto chance"), r.ElitePictosChance, 0, 100,
                v => r.ElitePictosChance = v, "%",
                T("Elites are stronger regular Nevrons.")),
            DoubleRow(T("Perfect catalyst chance"), r.T5PerfectCatalystChance, 0, 100,
                v => r.T5PerfectCatalystChance = v, "%",
                T("Rare endgame catalyst. 3.125% is about 1 in 32. Set 0 to disable.")),
            IntRow(T("Petank Colour of Lumina"), r.PetankLumina, 0, 9999, v => r.PetankLumina = v,
                tooltip: T("Lumina awarded by a Petank.")),
            IntRow(T("Merchant pictos + weapon slots"), r.MerchantGearSlots, 0, 20,
                v => r.MerchantGearSlots = v, tooltip: T("Extra gear slots in shops.")),
            IntRow(T("Tint shards per type (T1–T4)"), r.TintsPerType, 0, 99, v => r.TintsPerType = v,
                tooltip: T("Tint shards found in early and mid-game chests.")),
            IntRow(T("Chroma Elixir shards (T1–T4)"), r.ChromaElixirShards, 0, 99,
                v => r.ChromaElixirShards = v, tooltip: T("Chroma Elixir shards found in early and mid-game chests.")),
            IntRow(T("Shapes per type (T1–T4)"), r.ShapesPerType, 0, 20, v => r.ShapesPerType = v,
                tooltip: T("Shapes found in early and mid-game chests.")),
            adjustLumina,
            multiplierRow);
    }
}

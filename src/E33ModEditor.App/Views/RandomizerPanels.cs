using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using E33ModEditor.App.ViewModels;
using E33ModEditor.Core.Loot;

namespace E33ModEditor.App.Views;

internal static class RandomizerPanels
{
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));

    public static Control Build(MainViewModel vm)
    {
        var r = vm.Randomizer;
        var parts = new List<Control>
        {
            new TextBlock
            {
                Text = "Randomizer",
                Foreground = new SolidColorBrush(Color.Parse("#FFC9A961")),
                FontSize = PictosEditorPanels.FontTitle,
                FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
            },
            Bullet("Chests: keys, journals, records, chroma, Colour of Lumina, Recoats and shards stay vanilla. Pictos/weapon chests roll 1 / 2 / 3 gear items (default 70 / 25 / 5). Exploration catalyst tables become a Chroma Catalyst of that chest’s tier (T1–T5). T1–T4 chests also get 9 Healing / Energy / Revive tints of each type and 2 of each Shape (Health, Energy, Life), spread evenly. The 21 Paint Cages are their own toggle and always roll one tier-appropriate pictos or weapon."),
            Bullet("Enemy drops: regular 10% pictos, elite 66%, boss / alpha 1 pictos + 1 weapon + a catalyst of that enemy’s tier, superboss 2 + 2 + matching catalyst, petank 50 lumina (chromatic Petank 1000). Overworld (WM_) fights always drop one pictos or one weapon. Non-boss field drops also roll the two catalyst tiers below their own: x1 one tier down at 50% and x1 two tiers down at 30%. Protected key/quest rows stay."),
            Bullet("Merchants stock lumina, recoat and catalysts up to the shop tier — the shop’s own catalyst at x33 and every lower catalyst at x999 bulk stock — plus 4 pictos / weapons from that tier. T5 shops never sell an L5 catalyst. Protected shop rows stay."),
            Bullet("Created pictos and weapon effects only appear if you placed them on T1–T5 in the Tier Editor. Unplaced and Do Not Use never roll. Encounter tables are never changed unless Battle groups is enabled. Infinite only drops from Act 3 Lumière Renoir (Curator). Ultima Power (Painted Power) only drops from Act 2 Monolith Renoir, just before the Paintress."),
            Bullet("Weapon passives: T1 weapons get 1 effect (Lvl 4), T2 get 2 (Lvl 4 + 10), T3+ get Lvl 4 / 10 / 20. Effect tier is weighted by the weapon's item tier, then a random effect allowed for that character and not already on that weapon. Stain-echo is one per weapon. Extra fill is 0 / 0 / 20 / 40 / 60% by weapon tier. Gustave never rolls AutoDeath and may take any generic T1–T3 effect."),
            Bullet("Monster stats: bosses get a fixed HP scale by their level band (T1 ×1.5, T2 ×2.5, T3 ×5, T4 ×10, T5 ×21). Enemy Physical/MagicalAttack first takes the attack × base pre-treatment (default ×2), then rolls the band below. Every other EnemyScaling field (except Experience and Chroma) rolls independently in T1 0.95–1.05 / T2 1–1.15 / T3 1.1–1.25 / T4 1.2–1.5 / T5 1.5–1.8. Non-boss HP uses that band too."),
            Bullet("Story HP/XP/chroma (same factor, instead of the boss HP band): Old Lumière Painted Renoir ×21, Monolith Painted Renoir ×12, The Paintress pinned at 33,333,333 HP with XP/Chroma following the same factor, Creation and Act 3 Renoir ×100. Creation and Act 3 Renoir also skip the band: attack ×3, every other combat stat ×5. Painted Renoir deals triple damage and The Paintress double damage (attack ×3 / ×2 on top of their band)."),
            Bullet("Act 3 Lumière (except those two named bosses) is baked to the level-99 curve, then T5 bands. Chests, enemy drops and Cribappa are T5. Frozen Hearts is baked +16 levels so the area tops out at 79 (high T4); chests and Melosh stay T4. XP and chroma follow the curve bake (zero XP stays zero)."),
            Bullet("Weapon stats: attack and free-aim multipliers roll independently in the same bands. Base and free-aim elements roll separately (two different elements is common). T1–T3: Physical / Earth / Fire / Lightning / Ice. T4 adds Light / Dark. T5 adds Void. Equal chance. Chroma is not scaled."),
        };

        parts.Add(PictosEditorPanels.Head("Seed"));
        parts.Add(PictosEditorPanels.Note(
            "This exact number seeds everything below — chests, enemy drops, merchant inventories, " +
            "weapon passives, weapon stats and monster stats. Same number + same toggles = same result. " +
            "Press the dice for a new number on every click, or type a number directly."));
        parts.Add(SeedRow(r));

        parts.Add(PictosEditorPanels.Head("Loot"));
        parts.Add(Toggle("Chests / pickups", r.Chests, v => r.Chests = v));
        parts.Add(Toggle("Paint Cages (one tier-appropriate pictos or weapon each)", r.PaintCages, v => r.PaintCages = v));
        parts.Add(Toggle("Enemy drops", r.EnemyDrops, v => r.EnemyDrops = v));
        parts.Add(Toggle("Merchant inventories", r.Merchants, v => r.Merchants = v));

        parts.Add(PictosEditorPanels.Head("Weapons"));
        parts.Add(Toggle("Randomize weapon passives", r.WeaponPassives, v => r.WeaponPassives = v));
        parts.Add(Toggle("Randomize weapon attack / free-aim stats and elements", r.WeaponStats, v => r.WeaponStats = v));

        parts.Add(PictosEditorPanels.Head("Monsters"));
        parts.Add(Toggle("Randomize monster stats (HP / attack / speed / …; XP and chroma stay vanilla)", r.MonsterStats, v => r.MonsterStats = v));

        parts.Add(PictosEditorPanels.Head("Battle groups (experimental)"));
        parts.Add(PictosEditorPanels.Note(
            "Adds enemies to regular encounter rosters and patches the battle assets so spots 4–8 " +
            "spawn, sort and show on field. Bosses, alphas, tutorial/scripted fights and broken rows " +
            "are left alone. The battle-map overhaul (5-member party, formation) is composed with this."));
        parts.Add(Toggle("Randomize battle groups (more than three enemies)", r.BattleGroups, v => r.BattleGroups = v));
        parts.Add(IntBox("Max enemies per battle", r.MaxEnemiesPerBattle, 1, BattleGroupKit.FieldCapacity, v => { r.MaxEnemiesPerBattle = v; r.Persist?.Invoke(); }));
        parts.Add(NumBox("Chance to add enemies %", r.ExtraEnemiesChance, v => { r.ExtraEnemiesChance = v; r.Persist?.Invoke(); }));
        parts.Add(Toggle("Shuffle the final roster", r.ShuffleBattleRosters, v => r.ShuffleBattleRosters = v));
        parts.Add(AreaRow("Battle groups area prefix (GO_ = Flying Waters; blank = all)", r));

        parts.Add(PictosEditorPanels.Head("Random battle every time (experimental)"));
        parts.Add(PictosEditorPanels.Note(
            "Pre-generates several variant encounter rows and appends them to each world enemy's " +
            "native PossibleEncounters list. The world-enemy base is patched to re-roll that list on " +
            "every battle start, so attacking the same overworld enemy can produce a different group " +
            "each time. This stages the world-enemy Blueprints and is independent of the fixed-roster " +
            "Battle groups option above."));
        parts.Add(Toggle("Re-roll the encounter on every battle start", r.RandomBattles, v => r.RandomBattles = v));
        parts.Add(IntBox("Variant rosters per encounter", r.BattleVariants, 1, 8, v => { r.BattleVariants = v; r.Persist?.Invoke(); }));

        parts.Add(PictosEditorPanels.Head("Stat bands (T1–T5)"));
        parts.Add(PictosEditorPanels.Note("Independent uniform rolls over vanilla. Boss HP uses the boss factor instead of the HP band. Experience and chroma are never scaled."));
        for (int t = 1; t <= 5; t++)
        {
            var i = t - 1;
            parts.Add(NumBox($"T{t} stat × min", r.Settings.StatLo[i], v => { r.Settings.StatLo[i] = v; r.Persist?.Invoke(); }));
            parts.Add(NumBox($"T{t} stat × max", r.Settings.StatHi[i], v => { r.Settings.StatHi[i] = v; r.Persist?.Invoke(); }));
            parts.Add(NumBox($"T{t} boss HP ×", r.Settings.BossHp[i], v => { r.Settings.BossHp[i] = v; r.Persist?.Invoke(); }));
        }

        parts.Add(PictosEditorPanels.Head("Attack bands (T1–T5, Physical/MagicalAttack only)"));
        parts.Add(PictosEditorPanels.Note("Hotter band rolled for enemy attacks. All other stats use the stat bands above."));
        for (int t = 1; t <= 5; t++)
        {
            var i = t - 1;
            parts.Add(NumBox($"T{t} attack × min", r.Settings.AttackLo[i], v => { r.Settings.AttackLo[i] = v; r.Persist?.Invoke(); }));
            parts.Add(NumBox($"T{t} attack × max", r.Settings.AttackHi[i], v => { r.Settings.AttackHi[i] = v; r.Persist?.Invoke(); }));
        }

        parts.Add(PictosEditorPanels.Head("Chest drops"));
        parts.Add(PictosEditorPanels.Note(
            "Each randomized chest rolls 1 (70%), 2 (20%) or 3 (10%) drops. Each drop is a pictos (25%), " +
            "a weapon (3%), a lumina point (36%) or a chroma pack (36%, equal chance of every size). " +
            "Tint-shard / shape quotas are met by overriding rolled lumina/chroma drops on T1–T4 chests. " +
            "Shards raise how many of each tint the party can carry; shapes raise its potency. " +
            "The tint items themselves are baseline and are never dropped."));
        parts.Add(IntBox("Tint shards of each type (T1–T4)", r.Settings.TintsPerType, 0, 99, v => { r.Settings.TintsPerType = v; r.Persist?.Invoke(); }));
        parts.Add(IntBox("Chroma Elixir shards (T1–T4)", r.Settings.ChromaElixirShards, 0, 99, v => { r.Settings.ChromaElixirShards = v; r.Persist?.Invoke(); }));
        parts.Add(IntBox("Shapes of each type (T1–T4)", r.Settings.ShapesPerType, 0, 20, v => { r.Settings.ShapesPerType = v; r.Persist?.Invoke(); }));

        parts.Add(PictosEditorPanels.Head("Drop recipe"));
        parts.Add(PictosEditorPanels.Note("Used only when Enemy drops / Merchants are on. Pictos, weapons and catalysts are drawn from that enemy or shop’s tier. Gear never repeats within a pack: fully placed tiers cascade down to T1, then fall back to chroma. Bosses always drop 1 weapon + 1 pictos (66% second pictos, 33% third); super-bosses drop 2 + 2 with the same bonus odds. Bosses also drop catalysts that scale by band — T4: 1×L5 + 3–5×L4; T5 low (L80–89): 2×L5 + 6–10×L4; T5 high (L90–99): 3×L5 + 11–25×L4 — plus 5 lumina; regulars, elites and overworld fights roll 30% for 1–3 catalysts of their own tier (T5: 60% for 2–6 L4; never L5), x1 one tier down at 50%, x1 two tiers down at 30% (so T5 rolls L3/L2), and 1 lumina at 4% per tier. On top of that, every endgame regular/elite — a T5-area row at fight level 90+ — also rolls one Perfect (L5) Chroma Catalyst at the chance below (default 1%); a row shared with a lower-band fight is excluded, but reuse in the Endless Tower is allowed. The Forgotten Battlefield Dualliste (FB_DuallisteLR) additionally always pays a T3 package on top: one Verso-only T3 weapon, two distinct T3 pictos and one Upgrade Material L3. The Paintress and Painted Renoir (both inside the Monolith) replace their boss recipe entirely: one random T4 weapon, three T4 pictos and 33 Grandiose (T4) Chroma Catalysts each. White-nevron dialogue rewards (Troubadour, Portier, Demineur, the RedWoods Bénisseur fountain, Jar, Hidden Arena Keeper, Judge of Mercy) are swapped for same-tier gear; the fountain always pays a T4 picto that never carries a Defense stat. Starting weapons and white-nevron rewards are excluded from loot."));
        parts.Add(NumBox("Regular pictos chance %", r.Settings.RegularPictosChance, v => { r.Settings.RegularPictosChance = v; r.Persist?.Invoke(); }));
        parts.Add(NumBox("Elite pictos chance %", r.Settings.ElitePictosChance, v => { r.Settings.ElitePictosChance = v; r.Persist?.Invoke(); }));
        parts.Add(NumBox("Perfect (L5) catalyst — T5 Lv90+ chance %", r.Settings.T5PerfectCatalystChance, v => { r.Settings.T5PerfectCatalystChance = v; r.Persist?.Invoke(); }));
        parts.Add(IntBox("Petank Colour of Lumina", r.Settings.PetankLumina, 0, 9999, v => { r.Settings.PetankLumina = v; r.Persist?.Invoke(); }));
        parts.Add(IntBox("Merchant pictos+weapon slots", r.Settings.MerchantGearSlots, 0, 20, v => { r.Settings.MerchantGearSlots = v; r.Persist?.Invoke(); }));

        return PictosEditorPanels.Page(parts.ToArray());
    }

    private static Control Bullet(string text) =>
        new TextBlock
        {
            Text = "•  " + text,
            Foreground = Cream,
            FontSize = PictosEditorPanels.FontNote,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4),
        };

    private static Control Toggle(string label, bool on, Action<bool> set)
    {
        var box = new CheckBox
        {
            Content = label,
            IsChecked = on,
            FontSize = PictosEditorPanels.FontInput,
            Foreground = Cream,
            Margin = new Thickness(0, 4, 0, 8),
        };
        box.IsCheckedChanged += (_, _) => set(box.IsChecked == true);
        return box;
    }

    /// <summary>
    /// Seed row: typed value commits live (every valid keystroke) as well as on
    /// Enter / focus-loss, so the number on screen is always what the pack uses.
    /// The dice button rolls a fresh number on every click.
    /// </summary>
    private static Control SeedRow(RandomizerViewModel r)
    {
        var box = new TextBox
        {
            Text = r.Seed.ToString(CultureInfo.InvariantCulture),
            FontSize = PictosEditorPanels.FontInput,
            Foreground = Cream,
            Width = 160,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        void Commit()
        {
            if (int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                v = Math.Clamp(v, RandomizerViewModel.SeedMin, RandomizerViewModel.SeedMax);
                r.Seed = v;
                box.Text = v.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                box.Text = r.Seed.ToString(CultureInfo.InvariantCulture);
            }
        }
        // Live commit so a typed value is never lost when pack runs without tabbing out.
        box.TextChanged += (_, _) =>
        {
            if (int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                r.Seed = Math.Clamp(v, RandomizerViewModel.SeedMin, RandomizerViewModel.SeedMax);
        };
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
                Commit();
        };
        var dice = new Button
        {
            Content = "🎲 Random",
            FontSize = PictosEditorPanels.FontInput,
            Margin = new Thickness(8, 0, 0, 0),
        };
        ToolTip.SetTip(dice, "Roll a new random seed (new number on every click)");
        dice.Click += (_, _) =>
        {
            r.RandomizeSeed();
            box.Text = r.Seed.ToString(CultureInfo.InvariantCulture);
        };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(0, 2),
        };
        row.Children.Add(new TextBlock
        {
            Text = "Seed:",
            Foreground = Cream,
            FontSize = PictosEditorPanels.FontLabel,
            VerticalAlignment = VerticalAlignment.Center,
            Width = PictosEditorPanels.LabelCol,
        });
        row.Children.Add(box);
        row.Children.Add(dice);
        return row;
    }

    private static Control AreaRow(string label, RandomizerViewModel r)
    {
        var box = new TextBox
        {
            Text = r.BattleGroupsAreaPrefix,
            FontSize = PictosEditorPanels.FontInput,
            Foreground = Cream,
            Width = 160,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        void Commit()
        {
            r.BattleGroupsAreaPrefix = box.Text ?? "";
            box.Text = r.BattleGroupsAreaPrefix;
        }
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter || e.Key == Key.Return) Commit();
        };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(0, 2),
        };
        row.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Cream,
            FontSize = PictosEditorPanels.FontLabel,
            VerticalAlignment = VerticalAlignment.Center,
            Width = PictosEditorPanels.LabelCol,
        });
        row.Children.Add(box);
        return row;
    }

    private static Control IntBox(string label, int value, int min, int max, Action<int> set)
    {
        var box = new TextBox
        {
            Text = value.ToString(CultureInfo.InvariantCulture),
            FontSize = PictosEditorPanels.FontInput,
            Foreground = Cream,
            Width = 160,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        void Commit()
        {
            if (int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                set(Math.Clamp(v, min, max));
            else
                box.Text = value.ToString(CultureInfo.InvariantCulture);
        }
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
                Commit();
        };
        var line = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse($"{PictosEditorPanels.LabelCol},*"),
            Margin = new Thickness(0, 2),
        };
        line.Children.Add(new TextBlock
        {
            Text = label, Foreground = Cream, FontSize = PictosEditorPanels.FontLabel,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(box, 1);
        line.Children.Add(box);
        return line;
    }

    private static Control NumBox(string label, double value, Action<double> set)
    {
        var box = new TextBox
        {
            Text = value.ToString("0.###", CultureInfo.InvariantCulture),
            FontSize = PictosEditorPanels.FontInput,
            Foreground = Cream,
            Width = 160,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        box.LostFocus += (_, _) =>
        {
            if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                && v >= 0 && v <= 1000)
                set(v);
        };
        var line = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse($"{PictosEditorPanels.LabelCol},*"),
            Margin = new Thickness(0, 2),
        };
        line.Children.Add(new TextBlock
        {
            Text = label, Foreground = Cream, FontSize = PictosEditorPanels.FontLabel,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(box, 1);
        line.Children.Add(box);
        return line;
    }
}

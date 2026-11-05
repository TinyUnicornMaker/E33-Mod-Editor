using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using E33ModEditor.App.ViewModels;
using E33ModEditor.Core;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.App.Views;

/// <summary>
/// Skill Overhaul editor: one card per character (Gustave, Lune, Maelle, Sciel,
/// Verso, Monoco), each with a name heading, a role paragraph, standout bullets,
/// the affected moves, and a single master toggle driving that character's gate +
/// specs. All cards share the same box sizing.
/// </summary>
internal static class SkillOverhaulPanels
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#FF9C9280"));

    public static Control Build(MainViewModel vm)
    {
        var stack = new StackPanel
        {
            Spacing = 14,
            MaxWidth = PictosEditorPanels.ContentWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var characters = SkillOverhaulKit.SupportedCharacters.ToArray();
        if (characters.Length == 0)
        {
            stack.Children.Add(PictosEditorPanels.Note(
                "No supported character skills found. Check work/tables/skills (the Skills module extracts them on load)."));
        }
        else
        {
            foreach (var character in characters)
                stack.Children.Add(CharacterCard(vm, character));
        }

        // DockPanel last-child fill + Stretch ScrollViewer: DirectHost gives a
        // finite height, so Monoco's card (last) can scroll into view.
        var scroll = new ScrollViewer
        {
            // Padding on the content (not the ScrollViewer): ScrollViewer.Padding
            // is dropped from the scrollable extent, clipping the last card.
            Content = new Border { Padding = new Thickness(20, 16, 24, 160), Child = stack },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
        return new DockPanel { LastChildFill = true, Children = { scroll } };
    }

    private static Control CharacterCard(MainViewModel vm, string character)
    {
        var specs = SkillOverhaulKit.Specs
            .Where(x => string.Equals(x.Character, character, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var inner = new StackPanel { Spacing = 8 };

        // ── Header: name + single master toggle, inline ──
        CheckBox? wildcardToggle = null;
        var master = new CheckBox
        {
            Content = "Enable all changes",
            // Kit-driven characters (Verso) have no ChangeSpecs — their gate alone
            // drives the kit, so don't force the box unchecked when specs is empty.
            IsChecked = vm.SkillOverhaul.CharacterEnabled(character)
                && (specs.Length == 0 || specs.All(x => vm.SkillOverhaul.IsEnabled(x))),
            FontSize = PictosEditorPanels.FontInput,
            Foreground = Cream,
            VerticalAlignment = VerticalAlignment.Center,
        };
        master.IsCheckedChanged += (_, _) =>
        {
            var on = master.IsChecked == true;
            vm.SkillOverhaul.SetCharacterEnabled(character, on);
            foreach (var x in specs) vm.SkillOverhaul.SetEnabled(x, on);
            if (wildcardToggle is not null) wildcardToggle.IsEnabled = on;
            vm.SaveSkillOverhaul();
        };
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = character,
                    Foreground = Gold,
                    FontSize = 20,
                    FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
                    VerticalAlignment = VerticalAlignment.Center,
                },
                master,
            },
        };
        inner.Children.Add(header);

        // ── Role paragraph ──
        inner.Children.Add(new TextBlock
        {
            Text = RoleParagraph(character),
            Foreground = Cream,
            FontSize = PictosEditorPanels.FontHint,
            TextWrapping = TextWrapping.Wrap,
        });

        // ── Standout changes, as bullets ──
        var featureList = new StackPanel { Spacing = 3 };
        foreach (var feature in Standouts(character))
        {
            featureList.Children.Add(new TextBlock
            {
                Text = "\u2022  " + feature,
                Foreground = Cream,
                FontSize = PictosEditorPanels.FontHint,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        inner.Children.Add(featureList);

        if (string.Equals(character, "Lune", StringComparison.OrdinalIgnoreCase))
        {
            var wildcard = new CheckBox
            {
                Content = "Light stains substitute for missing stain requirements",
                IsChecked = vm.SkillOverhaul.LightStainWildcard,
                IsEnabled = vm.SkillOverhaul.CharacterEnabled(character),
                FontSize = PictosEditorPanels.FontInput,
                Foreground = Cream,
                Margin = new Thickness(0, 4, 0, 2),
            };
            wildcardToggle = wildcard;
            wildcard.IsCheckedChanged += (_, _) =>
            {
                vm.SkillOverhaul.LightStainWildcard = wildcard.IsChecked == true;
                vm.SaveSkillOverhaul();
            };
            inner.Children.Add(PictosEditorPanels.Head("Stain rules"));
            inner.Children.Add(wildcard);
            inner.Children.Add(PictosEditorPanels.Note(
                "Applies while Lune's overhaul is enabled. On restores the vanilla wildcard: a held " +
                "Light stain covers any missing elemental requirement and is consumed in its place. " +
                "Off keeps the strict requirements. Elemental Genesis (and Elemental Cataclysm) always " +
                "require the exact four elements, and Mayhem still never consumes Light."));
        }

        // ── Short list of affected moves ──
        inner.Children.Add(new TextBlock
        {
            Text = "Moves: " + string.Join(", ", MoveRoster(character, specs)),
            Foreground = Muted,
            FontSize = PictosEditorPanels.FontHint,
            TextWrapping = TextWrapping.Wrap,
        });

        // Deliberately no MaxHeight: capping a card whose text runs taller than
        // the cap pushes the surplus lines outside the page's scrollable extent,
        // so they can never be scrolled into view (they render past the last
        // measured card). Cards are kept near a third of the screen by trimming
        // the copy instead of clipping it.
        return new Border
        {
            Classes = { "panel" },
            Padding = new Thickness(20, 16),
            Child = inner,
        };
    }

    private static string RoleParagraph(string character) => character switch
    {
        "Gustave" =>
            "Gustave is the charge engine: a methodical builder–spender who banks " +
            "charge through crits and Rush support, then cashes out with Overcharge, " +
            "whose backloaded per-charge table pays maximum damage — and bonus break — " +
            "at a full 10 charge. Hell Storm is his 12-AP Fire barrage: all of Strike " +
            "Storm's hits with no charge gain, for when the board needs clearing rather " +
            "than charging.",
        "Lune" =>
            "Lune is the stain elementalist and AP broker. Damage splits by stains " +
            "consumed, Mayhem scales backloaded per stain with dark stains doubling it, " +
            "and Ultima Noir demands four Dark stains for the biggest overcharged total " +
            "in her kit — at the price of 10 Blight on herself. Dark Power and Angel's " +
            "Gift convert stains into party AP.",
        "Maelle" =>
            "Maelle is the stance dancer. Three elemental paths (Fire/Lightning/Void) " +
            "played around Defensive/Offensive/Virtuose: elements pick the tool, stances " +
            "pick the payoff. Stendhal is her single-hit nuke, Déviation Absolue a 19-AP " +
            "counter that gets cheaper for every attack parried since last turn, and For " +
            "Those Who Come After an 8-hit Lightning memorial that keeps Marked synergy " +
            "without removing the Mark.",
        "Sciel" =>
            "Sciel is the twilight scaler: Foretell is her fuel, applied cheaply and " +
            "eaten for damage, and Twilight multiplies whatever she has banked, so her " +
            "payoff grows the longer a fight runs. End Slice counts every Foretell " +
            "spent all battle. Twelve custom moves grow her tree off Our Sacrifice " +
            "and Intervention — support on one branch, finishers on the other.",
        "Verso" =>
            "Verso is the perfection duelist: every move pays out at the Perfection " +
            "rank it asks for, with the sharpest spikes held back for A and S — and " +
            "each gate is now an explicit rank check, not hidden overcharge state. " +
            "Steeled Strike waits a full turn, then triples its damage and pays ×10 " +
            "at S. Five new moves widen his tree, from Zanmoto's two-turn finisher " +
            "to Repainted's buff battery.",
        "Monoco" =>
            "Monoco is the mask transformer: every skill is a foot-dropper, damage " +
            "is 0.33 × Clamp(AP) split across hits, and the matching mask (Almighty " +
            "as wildcard) pays ×3 — or ×5 when the skill itself wants Almighty. " +
            "Gradient finishers sit off that curve: Mighty Strike, Sanctuary, and " +
            "Break Point. Unlock costs stay on feet and gradient items, not the grid.",
        _ => "",
    };

    private static string[] Standouts(string character) => character switch
    {
        "Gustave" => new[]
        {
            "Eight vanilla skills rebalanced; skill grid cleared of cut-content nodes.",
            "Overcharge: backloaded per-charge table, max damage and 1.5x break at 10 charges.",
            "Hell Storm (12 AP Fire) and Cinder Swarm (6 AP Fire, crits build charge).",
            "Zoom Zoom grants party Rush instead of Powerful.",
            "Recovery heals 40% (was 50%); Shatter costs 8 AP (was 5).",
        },
        "Lune" => new[]
        {
            "Mayhem: backloaded per-stain multipliers — 0.11x with one stain up to 2.61x at four; dark stains double its damage.",
            "Healing rebalanced: Healing Light 1 AP (free when overcharged), Rebirth 9 AP, Revitalization heals 10–20%.",
            "Fire Rage: 4 AP ramp — 0.33x opener growing to 1.98x by turn 5, then tailing off.",
            "Earth moves deal 1.3× break damage.",
            "New: Dark Power and Angel's Gift (stains into party AP), Ultima and Ultima Noir (21-AP finishers).",
        },
        "Maelle" => new[]
        {
            "Defensive halves damage taken and pays 1 AP per parry; Offensive adds 50% damage.",
            "Virtuose trades its old ×3 for ×2 damage plus a Berserk every turn.",
            "New Lightning finishers: Lumière Above, Revers Fatal, Déviation Absolue, For Those Who Come After.",
            "New Fire/Void finishers: Vengeance Ardente, Alicia's Nightmare, Blade Waltz, L'Acte Final Incendiaire.",
            "Marked payoffs tuned (Percée, Momentum Strike ×1.5) and a global +1% per Burn rule.",
            "Death's Bargain refills her full 21-AP bar at 8 AP.",
        },
        "Sciel" => new[]
        {
            "Foretell tuning: 3% per stack on Twilight Slash, 5% on Delaying Slash / Phantom Blade / Dark Wave, 10% on Twilight Dance; Sealed Fate consumes at ×2.",
            "Our Sacrifice (14 AP): stolen-health bonus capped at ×2, +10% per Foretell.",
            "Gradient finishers: Shadow Bringer, Doom (Greater debuffs) and a 9-charge End Slice (+5% per Foretell consumed).",
            "Plentiful Harvest is now Earth; All Set buffs last 1 turn; Harvest and Grim Harvest heal per Foretell with no base heal.",
            "New: Black Death, Last Chance, False Start, Specter's Edge, Triple Hex, Cursed Frenzy, Soul Transplant.",
            "New finishers: Ultima Path, Ultima's Bargain, Second Life, Phantom Sever, Corruption.",
        },
        "Verso" => new[]
        {
            "All 23 vanilla moves respec'd; rank gates are explicit Perfection-rank checks (D–S).",
            "Steeled Strike: 13 hits after a 1-turn delay, ×10 damage at S.",
            "New: Zanmoto (24 AP) — 13 hits after 2 turns; at S every hit adds two ethereal sword strikes.",
            "New: Repainted (17 AP) — rank to S, a Berserk/Rush/Shell/Powerful battery, Cursed, 100 Burn.",
            "New: Voided Stars (17 AP Void, ×3 break at S), Red Bushido (18 AP, missing-HP bonus), Overcharged Blitzer (19 AP Lightning).",
            "Shared rows copied to Verso-only names, so changing Verso never touches Gustave.",
        },
        "Monoco" => new[]
        {
            "Every foot-dropper rebalanced: 0.33× Clamp(AP) split across hits.",
            "Matching mask ×3; Almighty-required skills ×5 so they sit above curve.",
            "Duallist's extra correct-mask bonus flattened so it does not stack on the ×5.",
            "Gradients: Mighty Strike 3 AP / ×30, Sanctuary 5 shields + Greater Shell 5 turns, Break Point 8 AP / ×80.",
            "UnlockCost on the graph is left at 0 — feet and gradient items still unlock the skills.",
        },
        _ => Array.Empty<string>(),
    };

    private static IEnumerable<string> MoveRoster(string character, SkillOverhaulKit.ChangeSpec[] specs)
    {
        var moves = specs
            .Select(x => x.MoveId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        // Structural additions (new moves) have no specs — list them explicitly.
        if (string.Equals(character, "Gustave", StringComparison.OrdinalIgnoreCase))
            moves.AddRange(new[] { "Powerful", "Zoom Zoom", "Hell Storm", "Cinder Swarm", "Exploding Shot" });
        else if (string.Equals(character, "Lune", StringComparison.OrdinalIgnoreCase))
            moves.AddRange(new[] { "Vivifying Light", "Restoration", "Chiaroscuro", "Lumière Zénithale", "Shocking Wave", "Infernal Rage", "Blitz Overcharge", "Dark Power", "Angel's Gift", "Ultima", "Ultima Noir" });
        else if (string.Equals(character, "Maelle", StringComparison.OrdinalIgnoreCase))
            moves.AddRange(new[] { "Lumière Above", "Revers Fatal", "Déviation Absolue", "For Those Who Come After", "Vengeance Ardente", "Alicia's Nightmare", "Blade Waltz", "L'Acte Final Incendiaire" });
        else if (string.Equals(character, "Sciel", StringComparison.OrdinalIgnoreCase))
            moves.AddRange(new[] { "Black Death", "Last Chance", "False Start", "Specter's Edge", "Triple Hex", "Cursed Frenzy", "Soul Transplant", "Ultima Path", "Ultima's Bargain", "Second Life", "Phantom Sever", "Corruption" });
        // Verso has no ChangeSpecs (its kit drives everything), so list the
        // headline rebalances plus every new move.
        else if (string.Equals(character, "Verso", StringComparison.OrdinalIgnoreCase))
            moves.AddRange(new[] { "Assault Zero", "Steeled Strike", "End Bringer", "Light Holder", "Phantom Stars", "Speed Burst", "Blood Strike", "Zanmoto", "Repainted", "Red Bushido", "Voided Stars", "Overcharged Blitzer" });
        else if (string.Equals(character, "Monoco", StringComparison.OrdinalIgnoreCase))
            moves.AddRange(new[] { "Abbest Wind", "Duallist Storm", "Mighty Strike", "Sanctuary", "Break Point" });
        return moves;
    }
}

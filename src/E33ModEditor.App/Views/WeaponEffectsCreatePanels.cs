using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using E33ModEditor.App.ViewModels;
using E33ModEditor.Core;

namespace E33ModEditor.App.Views;

internal static class WeaponEffectsCreatePanels
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#FF9C9280"));

    public static Control Build(MainViewModel vm)
    {
        var stack = new StackPanel
        {
            Spacing = 16,
            MaxWidth = 1120,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(20, 16, 24, 24),
        };

        stack.Children.Add(new TextBlock
        {
            Text = "Create new weapon effects",
            Foreground = Gold,
            FontSize = 26,
            FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
        });

        var box = new CheckBox
        {
            Content = vm.WeaponEffectsEnabled
                ? "Added — click to omit from the next pack"
                : "Off — click to include on the next CLI/standalone build",
            IsChecked = vm.WeaponEffectsEnabled,
            FontSize = 16,
            Foreground = Cream,
            IsEnabled = WeaponEffectsCreateKit.Specs.Count > 0,
        };
        box.IsCheckedChanged += (_, _) =>
        {
            var v = box.IsChecked == true;
            vm.WeaponEffectsEnabled = v;
            box.Content = v
                ? "Added — click to omit from the next pack"
                : "Off — click to include on the next CLI/standalone build";
        };
        stack.Children.Add(box);

        stack.Children.Add(new TextBlock
        {
            Text = "Clones an existing weapon lumina Blueprint into a new passive id, with its own DT_Balancing_Luminas keys. Vanilla magnitudes are never overwritten. Assign the new id on a weapon in the Weapons list (Extra / Lvl 4 / 10 / 20), then run the CLI/standalone build.",
            Foreground = Muted,
            FontSize = 16,
            TextWrapping = TextWrapping.Wrap,
        });

        stack.Children.Add(Bullet(
            "direct — clone one BP that already looks up named floats (burn stacks, heal %, AP, lifesteal, …) and point those lookups at new keys."));
        stack.Children.Add(Bullet(
            "elemental-skill-dmg — clone ElementalDMG+ plus the Fire/Ice/… child so +skill-element damage can change without touching SkillsFireDMG+_DamageIncrease and friends."));
        stack.Children.Add(Bullet(
            "stain-start / stain-echo — Lune start-with-2/4 of Fire Ice Lightning Earth, and a 6×6 when-X-generates-Y matrix including Light and Dark (parent TypeToTypeMap)."));
        stack.Children.Add(Bullet(
            "Specs: src/E33ModEditor.Core/Weapons/Data/weapon_effects.json, overlay work/weapon_effects.json. See docs/custom-weapon-effects.md."));

        var specs = WeaponEffectsCreateKit.Specs;
        if (specs.Count == 0)
        {
            stack.Children.Add(new Border
            {
                Classes = { "panel" },
                Padding = new Thickness(20, 16),
                Child = new TextBlock
                {
                    Text = "No created effects yet. Tell Grok the source graph, the new numbers, and a display name — a spec is added and this pack will inject it.",
                    Foreground = Cream,
                    FontSize = 16,
                    TextWrapping = TextWrapping.Wrap,
                },
            });
        }
        else
        {
            // Grouped by theme — one card per theme with list of passive display names
            var groups = GroupSpecs(specs);
            foreach (var g in groups)
            {
                var names = string.Join("  ·  ", g.Specs.Select(s => s.DisplayName));
                stack.Children.Add(GroupCard(g.Title, g.Body, names));
            }
            stack.Children.Add(new TextBlock
            {
                Text = $"{specs.Count} created passives — assign an Id on a weapon (Extra / Lvl 4 / 10 / 20) or via Tier Editor → Effects, then run the CLI/standalone build. Vanilla keys are never overwritten.",
                Foreground = Muted,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
            });
        }
        return new ScrollViewer
        {
            Content = stack,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }

    private static Control Bullet(string text) => new TextBlock
    {
        Text = "•  " + text,
        Foreground = Cream,
        FontSize = 16,
        TextWrapping = TextWrapping.Wrap,
    };

    private static Control Card(string title, string body, string keys) => new Border
    {
        Classes = { "panel" },
        Padding = new Thickness(20, 16),
        Child = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = title, Foreground = Gold, FontSize = 20,
                    FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
                },
                new TextBlock { Text = body, Foreground = Cream, FontSize = 16, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = keys, Foreground = Muted, FontSize = 14, TextWrapping = TextWrapping.Wrap },
            },
        },
    };

    private static Control GroupCard(string title, string body, string names) => new Border
    {
        Classes = { "panel" },
        Padding = new Thickness(20, 16),
        Child = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = title, Foreground = Gold, FontSize = 18,
                    FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
                },
                new TextBlock { Text = body, Foreground = Cream, FontSize = 14, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = names, Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap },
            },
        },
    };

    private sealed record Group(string Title, string Body, List<WeaponEffectsCreateKit.EffectSpec> Specs);

    private static List<Group> GroupSpecs(IReadOnlyList<WeaponEffectsCreateKit.EffectSpec> specs)
    {
        // Theme order — stain, consume, burn, etc.
        var ordered = new List<Group>();

        void Add(string title, string body, Func<WeaponEffectsCreateKit.EffectSpec, bool> pred)
        {
            var list = specs.Where(pred).OrderBy(s => s.DisplayName, StringComparer.Ordinal).ToList();
            if (list.Count > 0) ordered.Add(new Group($"{title} ({list.Count})", body, list));
        }

        Add("Lune — Stain Start", "Start battle with 2 or 4 stains (Fire / Ice / Lightning / Earth). Lune only. Each is a cloned OnBattleStart graph with StainsAmount patched.", s => s.Family == WeaponEffectsCreateKit.FamilyStainStart);
        Add("Lune — Stain Echo (conversion)", "When a stain of type X is generated, also generate type Y. Full 6×6 matrix (Fire, Ice, Lightning, Earth, Dark, Light) including same-type double. Clones the Echo parent and rewrites TypeToTypeMap + InitialStainTypeToListen.", s => s.Family == WeaponEffectsCreateKit.FamilyStainEcho);
        Add("Status Consume — Base vs Skills", "Consume Slow / Powerless / Defenceless for bonus damage. Base-attack variants fire on DamageReason==4 (vanilla), Skills variants on DamageReason==1 (skill). Each status ×3 tiers: I ×1.5 (0.5), II ×2 (1.0), III ×2.5 (1.5). Descriptions say \"with base attack\" vs \"with skills\".", s => s.Id.Contains("Consume", StringComparison.Ordinal) && (s.Id.Contains("Slow") || s.Id.Contains("Powerless") || s.Id.Contains("Defenseless")));
        Add("Lune — Stain utility", "Misc Lune graphs: TurnStart 3/4, Light on Consume 100%, Dark damage per stain, Light-or-Dark on consume, Thunder burn consume.", s => s.Id is "WepFx_TurnStart3" or "WepFx_TurnStart4" or "WepFx_LightOnConsume100" or "WepFx_DarkDmgPerStain100" or "WepFx_ConsumeLightOrDarkDmg" or "WepFx_ThunderBurn250");
        Add("Blade — Conditional (on status)", "Deal +20–50% while Cursed / Blighted / Berserked / Powerful / Rushed / Shelled / Regenerating. Direct clone, status retargeted via replaceNames.", s => s.Id.Contains("Blade", StringComparison.Ordinal));
        Add("Burn — Apply & Amplify", "Apply Burn on base attack (5 / 10) and amplify Burn damage (+62% / +78% / +98%). Includes Counter Burn 10.", s => s.Id.Contains("Burn", StringComparison.Ordinal));
        Add("Counter / Shield", "Counter per Shield, Counter damage, base attack consumes shields, self-shield synergies. Each with stacked balancing keys.", s => s.Id.Contains("Counter") || s.Id.Contains("AtkShield") || s.Id.Contains("Shield") && !s.Id.Contains("Blade"));
        Add("Break & Augmented Attack", "Break damage +100%/+200% and base-attack damage +100%/+200%.", s => s.Id is "WepFx_Break2" or "WepFx_Break3" or "WepFx_AugAtk2" or "WepFx_AugAtk3");
        Add("Self-Inflict — start with status", "Apply Blight / Berserk / Burn on self at turn start for +10–30% per stack or flat bonus. Each tier is its own clone.", s => s.Id.Contains("SelfBurn") || s.Id.Contains("SelfBlight") || s.Id.Contains("SelfBerserk"));
        Add("Crit / Mask / Other", "Lightning on Crit (2/3), Overcharged Skill Stack +50%. Leftover direct clones.", s => s.Id is "WepFx_LightningCrit2" or "WepFx_LightningCrit3" or "WepFx_MaskStack50");
        // Catch-all for anything not yet grouped (e.g. future elemental-skill-dmg)
        var groupedIds = new HashSet<string>(ordered.SelectMany(g => g.Specs.Select(s => s.Id)), StringComparer.Ordinal);
        var remaining = specs.Where(s => !groupedIds.Contains(s.Id)).OrderBy(s => s.DisplayName, StringComparer.Ordinal).ToList();
        if (remaining.Count > 0) ordered.Add(new Group($"Other ({remaining.Count})", "Remaining direct / elemental clones.", remaining));

        return ordered;
    }

}

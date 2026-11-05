using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using E33ModEditor.App.ViewModels;
using E33ModEditor.Core;

namespace E33ModEditor.App.Views;

internal static class PictosCreatePanels
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#FF9C9280"));

    public static Control Build(MainViewModel vm)
    {
        var stack = new StackPanel
        {
            Spacing = 16,
            MaxWidth = 1100,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(20, 16, 24, 24),
        };

        stack.Children.Add(new TextBlock
        {
            Text = "Create new pictos",
            Foreground = Gold,
            FontSize = 26,
            FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Toggles add extra pictos into the packed mod. They are not placed in shops or enemy drops. Run the CLI/standalone build after changing a toggle. Turning a toggle off removes those pictos from the next pack.",
            Foreground = Muted,
            FontSize = 15,
            TextWrapping = TextWrapping.Wrap,
        });

        stack.Children.Add(ToggleCard(
            "Painter Conversion Mods",
            "Adds eight pictos that convert all Skill damage to one element, using the same NewElement Blueprint the game already uses for Void / Dark / Physical conversion (the Nexus Painter II mod proved that destination element is data, not hardcoded). Vanilla Painter is unchanged. No merchant or loot tables are edited.",
            string.Join("   ·   ", PictosCreateKit.PainterConversions.Select(s => s.DisplayName)),
            vm.PainterConversionEnabled,
            on => vm.PainterConversionEnabled = on));

        stack.Children.Add(ToggleCard(
            "Pictos magnitude variants",
            "Adds every created magnitude ladder (Stun Boost, Glass Cannon, Immaculate, Energy Master I–II, The Best Defense I–V, Energising Pain/Turn/Death, First Offensive, Empowering Jumps, Critical Break, Burning Death, Random Defense, Confident Fighter, Breaking Counter, Healing/Perilous/Empowering Parry, Lucky Aim, Full Strength, Charging Critical I–II, Infinite, Death's Dividend, and the older II/III families). Vanilla rows become tier I. They land in the Tier Editor as Unplaced until you assign them — except Infinite, which only drops from Act 3 Lumière Renoir (Curator) and cannot be obtained any other way. Run the CLI/standalone build after toggling.",
            string.Join("   ·   ", PictosMagnitudeKit.Variants.Select(s => s.DisplayName)),
            vm.PictosMagnitudeEnabled,
            on => vm.PictosMagnitudeEnabled = on));

        stack.Children.Add(ToggleCard(
            "Status Affinity Mods",
            "Three power archetypes — Cursed, Berserk and Blighted — each in three tiers (5% / 10% / 20% more damage while you have the status), all separate pictos that stack additively when worn together. Vanilla Cursed Power becomes Cursed Power I; Berserk and Blight have no vanilla counterpart and gain full ladders. No merchant or loot tables are edited.",
            string.Join("   ·   ", PictosCreateKit.Powers.Select(s => s.DisplayName)),
            vm.StatusAffinityEnabled,
            on => vm.StatusAffinityEnabled = on));


        return new ScrollViewer
        {
            Content = stack,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }

    private static Control ToggleCard(string title, string body, string list, bool on, Action<bool> set)
    {
        var box = new CheckBox
        {
            Content = on ? "Added — click to remove" : "Off — click to add",
            IsChecked = on,
            FontSize = 16,
            Foreground = Cream,
            Margin = new Thickness(0, 8, 0, 0),
        };
        box.IsCheckedChanged += (_, _) =>
        {
            var v = box.IsChecked == true;
            set(v);
            box.Content = v ? "Added — click to remove" : "Off — click to add";
        };

        var card = new Border
        {
            Classes = { "panel" },
            Padding = new Thickness(20, 16),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = title, Foreground = Gold, FontSize = 20,
                        FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
                    },
                    new TextBlock { Text = body, Foreground = Cream, FontSize = 15, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = list, Foreground = Muted, FontSize = 14, TextWrapping = TextWrapping.Wrap },
                    box,
                },
            },
        };
        return card;
    }
}

using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using E33ModEditor.App.ViewModels;
using E33ModEditor.Core;

namespace E33ModEditor.App.Views;

/// <summary>
/// Gameplay Tweaks ▸ Compatibility. Lets the pack support a third-party mod
/// that ships the same assets we do, by patching our values into the user's
/// installed copy instead of shadowing it with a vanilla copy.
/// </summary>
internal static class CompatibilityPanels
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#FF9C9280"));

    public static Control Build(MainViewModel vm)
    {
        var s = vm.Compatibility;

        var status = new TextBlock
        {
            Foreground = Muted,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0),
        };

        var pathBox = new TextBox
        {
            Text = s.ModUtoc ?? "",
            IsReadOnly = true,
            FontSize = PictosEditorPanels.FontInput,
            Foreground = Cream,
            Width = 620,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        void RefreshStatus()
        {
            // Avoid a retoc scan over ~mods when the option is off and no path
            // is set — only detect once the user opts in (or has a path saved).
            if (!s.Enabled && string.IsNullOrWhiteSpace(s.ModUtoc))
            {
                status.Text = "Tick the box to auto-detect the Bigger Party Mod, " +
                              "or use Browse… to point at its .utoc.";
                return;
            }
            var resolved = CompatibilityKit.Resolve(s);
            if (resolved is null)
            {
                status.Text = "Bigger Party Mod: not found in ~mods. " +
                              "Use Detect or Browse… to point at its .utoc.";
                return;
            }
            pathBox.Text = resolved;
            var stem = Path.GetFileNameWithoutExtension(resolved);
            status.Text = $"Detected: {stem}  ({(CompatibilityKit.IsValidMod(resolved) ? "recognised" : "unrecognised")})" +
                          "\nIt will be patched in place during the CLI/standalone build (a backup is written to work/pak-backup/compat/).";
        }

        var toggle = new CheckBox
        {
            Content = "Support Bigger Party Mod (by Heavylog81)",
            IsChecked = s.Enabled,
            FontSize = PictosEditorPanels.FontInput,
            Foreground = Cream,
            Margin = new Thickness(0, 4, 0, 8),
        };
        toggle.IsCheckedChanged += (_, _) =>
        {
            s.Enabled = toggle.IsChecked == true;
            if (s.Enabled && string.IsNullOrWhiteSpace(s.ModUtoc))
                vm.DetectPartyMod();
            pathBox.Text = s.ModUtoc ?? "";
            vm.SaveCompatibility();
            RefreshStatus();
        };

        var detectBtn = new Button { Content = "Detect" };
        detectBtn.Click += (_, _) =>
        {
            var hit = vm.DetectPartyMod();
            pathBox.Text = s.ModUtoc ?? "";
            vm.SaveCompatibility();
            RefreshStatus();
            vm.Status = hit is null
                ? "Bigger Party Mod not found in ~mods."
                : "Found Bigger Party Mod: " + Path.GetFileName(hit);
        };

        var browseBtn = new Button { Content = "Browse…" };
        browseBtn.Click += async (_, _) =>
        {
            if (vm.PickPartyModFile is null) return;
            var picked = await vm.PickPartyModFile();
            if (picked is null) return;
            s.ModUtoc = picked;
            pathBox.Text = picked;
            vm.SaveCompatibility();
            RefreshStatus();
        };

        var pathRow = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("Auto,*,Auto,Auto"),
            Margin = new Thickness(0, 2, 0, 2),
        };
        var pathLabel = new TextBlock
        {
            Text = "Mod file",
            Foreground = Cream,
            FontSize = PictosEditorPanels.FontLabel,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
        };
        Grid.SetColumn(pathLabel, 0);
        Grid.SetColumn(pathBox, 1);
        Grid.SetColumn(detectBtn, 2);
        Grid.SetColumn(browseBtn, 3);
        detectBtn.Margin = new Thickness(8, 0, 0, 0);
        browseBtn.Margin = new Thickness(8, 0, 0, 0);
        pathRow.Children.Add(pathLabel);
        pathRow.Children.Add(pathBox);
        pathRow.Children.Add(detectBtn);
        pathRow.Children.Add(browseBtn);

        var parts = new List<Control>
        {
            new TextBlock
            {
                Text = "Compatibility",
                Foreground = Gold,
                FontSize = PictosEditorPanels.FontTitle,
                FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
            },
            PictosEditorPanels.Note(
                "Optional support for third-party mods that ship the same assets our pack does. " +
                "The third-party files are never included in our pack — they are only patched on this machine."),
            PictosEditorPanels.Note(
                "Superseded by Party of Five: our pack can now raise the party to five itself " +
                "(the Party of Five tile). Both own AC_jRPG_BattleManager / AC_jRPG_CharactersManager / " +
                "FL_jRPG_CustomFunctionLibrary, so when Party of Five is on this compatibility patch is " +
                "skipped automatically. Only use it if you prefer the third-party mod."),
            PictosEditorPanels.Card(
                PictosEditorPanels.Head("Bigger Party Mod"),
                PictosEditorPanels.Note(
                    "Credits: Bigger Party Mod by Heavylog81. It lets a fourth member join the active party. " +
                    "The mod owns AC_jRPG_BattleManager, AC_jRPG_CharactersManager and " +
                    "FL_jRPG_CustomFunctionLibrary; our pack ships its own CharactersManager/FL for the level cap, " +
                    "so a later-loading pack would otherwise shadow the mod's copies — which is why ally-wide " +
                    "skills (Angel's Gift, Plentiful Harvest) only reached 3 of 4. " +
                    "With this enabled our pack drops the overlapping assets, and our level cap " +
                    "is patched into the installed mod in place instead."),
                toggle,
                pathRow,
                status),
            PictosEditorPanels.Card(
                PictosEditorPanels.Head("What gets patched"),
                PictosEditorPanels.Note(
                    "Character max level (AC_jRPG_CharactersManager + FL_jRPG_CustomFunctionLibrary). The mod's own " +
                    "party-size setting (MaxCharactersAmountInParty = 4) is preserved. Consecutive turns are not part " +
                    "of this: they live in our own AC_jRPG_CharacterBattleStats, so the pack applies them in both " +
                    "modes. Patching runs during the CLI/standalone build; if the mod is updated or reinstalled, run " +
                    "the CLI/standalone build again. A copy of the original files is written under work/pak-backup/compat/.")),
        };

        RefreshStatus();
        return PictosEditorPanels.Page(parts.ToArray());
    }
}

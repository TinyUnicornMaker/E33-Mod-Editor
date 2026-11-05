using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using E33ModEditor.App.ViewModels;
using E33ModEditor.Core;

namespace E33ModEditor.App.Views;

/// <summary>
/// Party of Five submodule. Folded into Export/ExpeditionInfinity_<seed>_P;
/// settings are written to work/party_five.json and applied on Apply &amp; Pack.
/// </summary>
internal static class PartyFivePanels
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#FF9C9280"));

    private static readonly string[] SpotLabels =
    {
        "Spot 0 → party 2 (character 2, front right)",
        "Spot 1 → party 4 (character 4, back-right flank)",
        "Spot 2 → party 1 (character 1, front left)",
        "Spot 3 → party 3 (character 3, back-left flank)",
        "Spot 4 → party 5 (character 5, back centre)",
    };

    public static Control Build(MainViewModel vm)
    {
        var s = vm.PartyFive;
        var parts = new List<Control>
        {
            new TextBlock
            {
                Text = "Party of Five",
                Foreground = Gold,
                FontSize = PictosEditorPanels.FontTitle,
                FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
            },
            PictosEditorPanels.Note(
                "Raises the battle party cap to five and fields two rows — members 1/2 in front, " +
                "3/5/4 in back, left to right — built from vanilla assets only. This is a submodule of " +
                "the main pack now: apply & pack writes it into Export/ExpeditionInfinity_<seed>_P and " +
                "there is no separate party pack to install."),
            PictosEditorPanels.Note(
                "Because the pack now owns AC_jRPG_BattleManager, AC_jRPG_CharactersManager and " +
                "FL_jRPG_CustomFunctionLibrary, turning this on supersedes the third-party Bigger Party Mod " +
                "compatibility option (Gameplay Tweaks ▸ Compatibility) — the two cannot both own those " +
                "assets. With Party of Five on, compatibility is skipped automatically."),
        };

        var toggle = new CheckBox
        {
            Content = "Enable Party of Five",
            IsChecked = s.Enabled,
            FontSize = PictosEditorPanels.FontInput,
            Foreground = Cream,
            Margin = new Thickness(0, 4, 0, 8),
        };
        toggle.IsCheckedChanged += (_, _) =>
        {
            s.Enabled = toggle.IsChecked == true;
            vm.SavePartyFive();
        };

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Party size"),
            PictosEditorPanels.Note(
                "Fixed at 5 members. The former 3- and 4-member options are retired."),
            toggle));

        var formationRows = new List<Control>
        {
            PictosEditorPanels.Note(
                "Offsets are in each spot's own facing frame (forward = toward the enemies, right = the spot's " +
                "right), so they stay correct on rotated battle maps. Keep neighbours at least ~200 units apart " +
                "(the default layout's smallest gap is 221). Characters 3 and 5 alias vanilla anchors, so their " +
                "offsets reach the back-left and back-centre positions."),
        };
        for (int i = 0; i < s.Formation.Length && i < SpotLabels.Length; i++)
        {
            int idx = i;
            var fwd = NumberBox(s.Formation[i].Fwd, v => { s.Formation[idx] = (v, s.Formation[idx].Right); vm.SavePartyFive(); });
            var right = NumberBox(s.Formation[i].Right, v => { s.Formation[idx] = (s.Formation[idx].Fwd, v); vm.SavePartyFive(); });
            formationRows.Add(Labeled(SpotLabels[i], Pair(fwd, right)));
        }
        var formationCard = new List<Control>
        {
            PictosEditorPanels.Head("Two-row formation"),
        };
        formationCard.AddRange(formationRows);
        parts.Add(PictosEditorPanels.Card(formationCard.ToArray()));

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Bisect switches (advanced)"),
            PictosEditorPanels.Note(
                "Debug aids for isolating a crash. Leave all on for the full experience."),
            Toggle("Apply the formation offsets", s.UseFormationOffsets, v => { s.UseFormationOffsets = v; vm.SavePartyFive(); }),
            Toggle("Patch battle spawn / spot order (cap + remap still on)", s.UseSpawnPatch, v => { s.UseSpawnPatch = v; vm.SavePartyFive(); }),
            Toggle("On-screen home-snap debug print", s.UseResetDebugPrint, v => { s.UseResetDebugPrint = v; vm.SavePartyFive(); })));

        return PictosEditorPanels.Page(parts.ToArray());
    }

    private static Control Pair(Control a, Control b)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock { Text = "fwd", Foreground = Muted, FontSize = PictosEditorPanels.FontHint, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(a);
        row.Children.Add(new TextBlock { Text = "right", Foreground = Muted, FontSize = PictosEditorPanels.FontHint, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(b);
        return row;
    }

    private static TextBox NumberBox(double value, Action<double> set)
    {
        var box = MakeBox(value.ToString("0.###", CultureInfo.InvariantCulture), t =>
        {
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= -400 && v <= 400)
                set(v);
        });
        box.Width = 110;
        return box;
    }

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

    private static TextBox MakeBox(string text, Action<string> onChange)
    {
        var box = new TextBox
        {
            Text = text,
            FontSize = PictosEditorPanels.FontInput,
            Foreground = Cream,
            Width = 160,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        box.LostFocus += (_, _) => onChange(box.Text ?? "");
        box.KeyDown += (_, e) =>
        {
            if (e.Key != Avalonia.Input.Key.Enter) return;
            onChange(box.Text ?? "");
            e.Handled = true;
        };
        return box;
    }

    private static Control Labeled(string label, Control input)
    {
        var line = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse($"{PictosEditorPanels.LabelCol + 60},*"),
            Margin = new Thickness(0, 4),
        };
        var lab = new TextBlock
        {
            Text = label,
            Foreground = Cream,
            FontSize = PictosEditorPanels.FontLabel,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(lab, 0);
        Grid.SetColumn(input, 1);
        line.Children.Add(lab);
        line.Children.Add(input);
        return line;
    }
}

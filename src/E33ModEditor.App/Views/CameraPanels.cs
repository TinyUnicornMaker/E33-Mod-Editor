using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using E33ModEditor.App.ViewModels;
using E33ModEditor.Core;

namespace E33ModEditor.App.Views;

/// <summary>
/// Camera submodule. Formerly a standalone pack (E33SecondPerson_P /
/// SecondPersonCamera_P / ImmersiveThirdPersonCamera_P); now folded into
/// Export/ExpeditionInfinity_<seed>_P so it cannot shadow the main pack. Changes are written
/// to work/firstperson.json and applied on Apply &amp; Pack.
/// </summary>
internal static class CameraPanels
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#FF9C9280"));

    public static Control Build(MainViewModel vm)
    {
        var s = vm.Camera;
        var parts = new List<Control>
        {
            new TextBlock
            {
                Text = "Camera",
                Foreground = Gold,
                FontSize = PictosEditorPanels.FontTitle,
                FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
            },
            PictosEditorPanels.Note(
                "Rewrites the data-driven DA_CameraConfig_* assets, the character world pawns and the climbing " +
                "component. This is a submodule of the main pack now — apply & pack writes it into " +
                "Export/ExpeditionInfinity_<seed>_P and there is no separate camera pack to install. Untick to ship the " +
                "vanilla third-person camera."),
        };

        var toggle = new CheckBox
        {
            Content = "Enable camera submodule",
            IsChecked = s.Enabled,
            FontSize = PictosEditorPanels.FontInput,
            Foreground = Cream,
            Margin = new Thickness(0, 4, 0, 8),
        };
        toggle.IsCheckedChanged += (_, _) =>
        {
            s.Enabled = toggle.IsChecked == true;
            vm.SaveCamera();
        };

        Control PresetButton(string label, string tip, Action apply)
        {
            var b = new Button { Content = label, Margin = new Thickness(0, 0, 8, 0) };
            ToolTip.SetTip(b, tip);
            b.Click += (_, _) =>
            {
                apply();
                vm.SaveCamera();
                vm.Status = $"Camera preset “{label}” applied. Reopen the tile or run the CLI/standalone build to use it.";
            };
            return b;
        }

        var presets = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        presets.Children.Add(PresetButton("Third-person close", "Over-the-right-shoulder boom (130) — the ImmersiveThirdPersonCamera look.",
            () =>
            {
                s.Enabled = true;
                s.ArmLength = 130f;
                s.PivotDrop = 14f;
                s.ForwardOffset = 50f;
                s.ExploreSideOffset = 35f;
                s.FieldOfView = 85f;
                s.DisableLag = false;
            }));
        presets.Children.Add(PresetButton("First-person", "Zero boom, head-height pivot, lag off.",
            () =>
            {
                s.Enabled = true;
                s.ArmLength = 0f;
                s.PivotDrop = 28f;
                s.ForwardOffset = 38f;
                s.ExploreSideOffset = 0f;
                s.FieldOfView = 90f;
                s.DisableLag = true;
            }));
        presets.Children.Add(PresetButton("Vanilla", "Turn the submodule off and ship the stock camera.",
            () => { s.Enabled = false; }));

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Camera submodule"),
            PictosEditorPanels.Note(
                "Presets set the fields below; tune any of them afterwards. Designed around the explore " +
                "(normal/crouch/moving-platform) configs and the character pawns."),
            toggle,
            presets));

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Explore / third-person"),
            PictosEditorPanels.Note("Arm 0 is head-mounted (first person). A short positive boom is the close over-shoulder third-person look."),
            Number("Boom arm length", s.ArmLength, 0, 400, v => { s.ArmLength = (float)v; vm.SaveCamera(); }),
            Number("Lens side offset (+right)", s.ExploreSideOffset, -60, 60, v => { s.ExploreSideOffset = (float)v; vm.SaveCamera(); }),
            Number("Lens forward offset", s.ForwardOffset, 0, 80, v => { s.ForwardOffset = (float)v; vm.SaveCamera(); }),
            Number("Pivot drop (down-pitch framing)", s.PivotDrop, 0, 80, v => { s.PivotDrop = (float)v; vm.SaveCamera(); }),
            Number("Eye lift", s.EyeLift, -40, 60, v => { s.EyeLift = (float)v; vm.SaveCamera(); }),
            Number("Field of view", s.FieldOfView, 60, 120, v => { s.FieldOfView = (float)v; vm.SaveCamera(); }),
            Toggle("Disable camera lag", s.DisableLag, v => { s.DisableLag = v; vm.SaveCamera(); }),
            Number("Climb boom scale (2 = pull back 2×)", s.ClimbArmScale, 0, 4, v => { s.ClimbArmScale = (float)v; vm.SaveCamera(); }),
            Number("Overworld DOF fallback f-stop (0 = leave lens, 22 ≈ off)", s.WorldMapAperture, 0, 135, v => { s.WorldMapAperture = (float)v; vm.SaveCamera(); })));

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Depth of field (extension)"),
            PictosEditorPanels.Note(
                "Separate, opt-in extension: a moderate out-of-focus falloff on the exploration camera " +
                "in both dungeons and the overworld. Lower f-stop = more blur. While on it writes both " +
                "cameras and supersedes the world-map aperture above."),
            Toggle("Enable depth of field", s.DepthOfField, v => { s.DepthOfField = v; vm.SaveCamera(); }),
            Number("Aperture (f-stop, lower = more blur)", s.DofAperture, 0.5, 32, v => { s.DofAperture = (float)v; vm.SaveCamera(); }),
            Number("Blur amount (Gaussian/Circle method)", s.DofBlurAmount, 0, 100, v => { s.DofBlurAmount = (float)v; vm.SaveCamera(); }),
            Number("Blur radius (Gaussian/Circle method)", s.DofBlurRadius, 0, 100, v => { s.DofBlurRadius = (float)v; vm.SaveCamera(); }),
            Number("Focus distance cm (0 = auto: boom)", s.DofFocusDistance, 0, 100000, v => { s.DofFocusDistance = (float)v; vm.SaveCamera(); })));

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Aim (free-aim)"),
            PictosEditorPanels.Note(
                "Leave AimFirstPerson off to keep vanilla aiming; the explore camera is still modded. " +
                "Values below only matter when it is on."),
            Toggle("Also make aim first-person", s.AimFirstPerson, v => { s.AimFirstPerson = v; vm.SaveCamera(); }),
            Number("Aim lens side offset (− = mirrored)", s.AimSideOffset, -60, 60, v => { s.AimSideOffset = (float)v; vm.SaveCamera(); }),
            Number("Aim field of view", s.AimFieldOfView, 60, 120, v => { s.AimFieldOfView = (float)v; vm.SaveCamera(); })));

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Per-character fixes"),
            PictosEditorPanels.Note(
                "Monoco's frame is the largest and Verso's hair clips at the shared distance, so each has " +
                "its own explore framing. Aim lifts raise the lens out of fur/hair for the tall poses."),
            Number("Monoco explore forward", s.MonocoExploreForward, 0, 80, v => { s.MonocoExploreForward = (float)v; vm.SaveCamera(); }),
            Number("Monoco explore arm (0 = global)", s.MonocoExploreArm, 0, 400, v => { s.MonocoExploreArm = (float)v; vm.SaveCamera(); }),
            Number("Monoco explore side (0 = global)", s.MonocoExploreSide, -120, 120, v => { s.MonocoExploreSide = (float)v; vm.SaveCamera(); }),
            Number("Verso explore forward", s.VersoExploreForward, 0, 80, v => { s.VersoExploreForward = (float)v; vm.SaveCamera(); })));

        return PictosEditorPanels.Page(parts.ToArray());
    }

    private static Control Number(string label, double value, double min, double max, Action<double> set) =>
        Labeled(label, MakeBox(value.ToString("0.###", CultureInfo.InvariantCulture), t =>
        {
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= min && v <= max)
                set(v);
        }));

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

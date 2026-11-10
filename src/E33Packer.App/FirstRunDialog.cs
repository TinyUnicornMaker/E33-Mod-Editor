using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using static E33Packer.App.AppLocalization;

namespace E33Packer.App;

/// <summary>
/// First-run consent for the one-time donor extraction. Nothing is written to
/// the game or the app folder until the user explicitly confirms.
/// </summary>
internal static class FirstRunDialog
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#FF9C9280"));

    public static async Task<bool> ConfirmExtractionAsync(
        Window owner, int have, int total, string targetDir,
        Func<Task<string?>>? chooseFolder = null)
    {
        var resuming = have > 0 && have < total;
        var missing = Math.Max(0, total - have);

        var dialog = new Window
        {
            Title = "Expedition Infinity",
            Icon = AppIcon.Icon,
            Width = 590,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var start = new Button
        {
            Content = resuming ? T("Finish setup") : T("Start extraction"),
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(16, 8),
            IsDefault = true,
        };
        var cancel = new Button
        {
            Content = T("Not now"),
            Padding = new Thickness(16, 8),
            IsCancel = true,
        };
        start.Click += (_, _) => dialog.Close(true);
        cancel.Click += (_, _) => dialog.Close(false);

        var progress = resuming
            ? T("Existing game data is reused; nothing is downloaded again.")
            : T("This is a one-time setup step.");

        var targetView = new SelectableTextBlock
        {
            Text = targetDir,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Cream,
            FontFamily = new FontFamily("DejaVu Sans Mono, Consolas, monospace"),
            FontSize = 12,
        };
        var change = new Button
        {
            Content = T("Change…"),
            Padding = new Thickness(10, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        ToolTip.SetTip(change, T("Choose a different folder for the extracted game data."));
        change.Click += async (_, _) =>
        {
            if (chooseFolder is null) return;
            change.IsEnabled = false;
            try
            {
                var chosen = await chooseFolder();
                if (!string.IsNullOrWhiteSpace(chosen)) targetView.Text = chosen;
            }
            finally
            {
                change.IsEnabled = true;
            }
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = resuming ? T("Finish game data setup") : T("First-time setup"),
                    FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
                    FontSize = 24,
                    Foreground = Gold,
                },
                new TextBlock
                {
                    Text = resuming
                        ? F("Most game data is already prepared. {0} of {1} item(s) still need to be read from your game.",
                            missing, total)
                        : T("Expedition Infinity needs some data from your game before it can build seeds. This is a one-time step."),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Cream,
                    FontSize = 13,
                },
                new TextBlock
                {
                    Text = resuming
                        ? T("• Only the missing items are read") + "\n" +
                          T("• Usually quick") + "\n" +
                          T("• Stored in the folder shown below") + "\n" +
                          T("• Quit Expedition 33 before continuing") + "\n" +
                          T("• No game files are modified")
                        : T("• About 1.2 GB of files") + "\n" +
                          T("• Usually 5–15 minutes, depending on the drive") + "\n" +
                          T("• Stored in the folder shown below") + "\n" +
                          T("• Quit Expedition 33 before continuing") + "\n" +
                          T("• No game files are modified"),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Cream,
                    FontSize = 13,
                    LineHeight = 21,
                },
                new TextBlock
                {
                    Text = progress,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Muted,
                    FontSize = 12,
                },
                new TextBlock
                {
                    Text = T("Saved to"),
                    Foreground = Gold,
                    FontSize = 12,
                    Margin = new Thickness(0, 4, 0, 0),
                },
                targetView,
                change,
                new TextBlock
                {
                    Text = T("If you cancel, the generator will ask again before the first build or the next launch. You can still choose a seed, profile and language."),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Muted,
                    FontSize = 12,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Margin = new Thickness(0, 6, 0, 0),
                    Children = { cancel, start },
                },
            },
        };

        return await dialog.ShowDialog<bool>(owner);
    }
}

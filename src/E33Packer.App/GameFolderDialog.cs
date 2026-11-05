using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using static E33Packer.App.AppLocalization;

namespace E33Packer.App;

internal enum GameFolderChoice
{
    UseFound,
    ChooseAnother,
    Quit,
}

/// <summary>Shown when automatic Steam detection cannot find the game.</summary>
internal static class GameFolderDialog
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#FF9C9280"));

    public static async Task<bool> PromptAsync(Window owner, string? problem = null)
    {
        var dialog = new Window
        {
            Title = "Expedition Infinity",
            Icon = AppIcon.Icon,
            Width = 560,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var choose = new Button
        {
            Content = T("Choose folder…"),
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(16, 8),
            IsDefault = true,
        };
        var quit = new Button
        {
            Content = T("Quit"),
            Padding = new Thickness(16, 8),
            IsCancel = true,
        };
        choose.Click += (_, _) => dialog.Close(true);
        quit.Click += (_, _) => dialog.Close(false);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = T("Find Expedition 33"),
                    FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
                    FontSize = 24,
                    Foreground = Gold,
                },
                new TextBlock
                {
                    Text = problem ?? T("We couldn't find Expedition 33 in the usual Steam folders."),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Cream,
                    FontSize = 13,
                },
                new TextBlock
                {
                    Text = T("Choose the folder that contains the \"Sandfall\" folder."),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Cream,
                    FontSize = 13,
                },
                new TextBlock
                {
                    Text = T("Typical location: Steam → steamapps → common → Expedition 33"),
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
                    Children = { quit, choose },
                },
            },
        };

        return await dialog.ShowDialog<bool>(owner);
    }

    /// <summary>
    /// Shows the auto-detected game folder before it is committed. The user can
    /// accept it, pick a different folder, or quit.
    /// </summary>
    public static async Task<GameFolderChoice> ConfirmFoundAsync(Window owner, string path)
    {
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

        var use = new Button
        {
            Content = T("Use this folder"),
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(16, 8),
        };
        var change = new Button
        {
            Content = T("Choose another folder…"),
            Padding = new Thickness(16, 8),
        };
        var quit = new Button
        {
            Content = T("Quit"),
            Padding = new Thickness(16, 8),
        };
        use.Click += (_, _) => dialog.Close(GameFolderChoice.UseFound);
        change.Click += (_, _) => dialog.Close(GameFolderChoice.ChooseAnother);
        quit.Click += (_, _) => dialog.Close(GameFolderChoice.Quit);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = T("Expedition 33 found"),
                    FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
                    FontSize = 24,
                    Foreground = Gold,
                },
                new TextBlock
                {
                    Text = T("Game folder"),
                    Foreground = Cream,
                    FontSize = 13,
                },
                new SelectableTextBlock
                {
                    Text = path,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Cream,
                    FontFamily = new FontFamily("DejaVu Sans Mono, Consolas, monospace"),
                    FontSize = 12,
                },
                new TextBlock
                {
                    Text = T("By default, packs install to the game's ~mods folder. You can change both folders later in Setup."),
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
                    Children = { quit, change, use },
                },
            },
        };

        return await dialog.ShowDialog<GameFolderChoice>(owner);
    }
}

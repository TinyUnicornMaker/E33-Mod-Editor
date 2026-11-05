using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace E33Packer.App;

/// <summary>Small reusable confirmation dialog.</summary>
internal static class ConfirmDialog
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));

    public static async Task<bool> AskAsync(
        Window owner, string title, string message, string continueLabel, string cancelLabel)
    {
        var dialog = new Window
        {
            Title = "Expedition Infinity",
            Icon = AppIcon.Icon,
            Width = 520,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var yes = new Button
        {
            Content = continueLabel,
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(16, 8),
            IsDefault = true,
        };
        var no = new Button
        {
            Content = cancelLabel,
            Padding = new Thickness(16, 8),
            IsCancel = true,
        };
        yes.Click += (_, _) => dialog.Close(true);
        no.Click += (_, _) => dialog.Close(false);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
                    FontSize = 22,
                    Foreground = Gold,
                    TextWrapping = TextWrapping.Wrap,
                },
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Cream,
                    FontSize = 13,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Margin = new Thickness(0, 6, 0, 0),
                    Children = { no, yes },
                },
            },
        };

        return await dialog.ShowDialog<bool>(owner);
    }
}

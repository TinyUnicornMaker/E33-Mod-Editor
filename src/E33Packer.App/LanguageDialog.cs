using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using E33ModEditor.Core.Localization;
using static E33Packer.App.AppLocalization;

namespace E33Packer.App;

/// <summary>First-launch language choice. Names are always shown natively.</summary>
internal static class LanguageDialog
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));

    public static async Task<string> ChooseAsync(Window owner)
    {
        var dialog = new Window
        {
            Title = "Expedition Infinity — language",
            Icon = AppIcon.Icon,
            Width = 470,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        // The first-launch choice always starts in English.
        var chosen = "en";
        var list = new StackPanel { Spacing = 6 };
        var button = new Button
        {
            Content = AppLocalization.TFor(chosen, "Continue"),
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(18, 8),
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true,
        };

        foreach (var language in ModLocalization.Supported)
        {
            var radio = new RadioButton
            {
                GroupName = "startup-language",
                Content = language.DisplayName,
                IsChecked = language.Code == chosen,
                Foreground = Cream,
                FontSize = 14,
            };
            radio.Checked += (_, _) =>
            {
                if (radio.IsChecked != true) return;
                chosen = language.Code;
                button.Content = AppLocalization.TFor(chosen, "Continue");
            };
            list.Children.Add(radio);
        }

        button.Click += (_, _) => dialog.Close(chosen);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(26),
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = "English · Français · Polski · Русский",
                    FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
                    FontSize = 24,
                    Foreground = Gold,
                },
                new TextBlock
                {
                    Text = "Choose your language · Choisissez votre langue · " +
                           "Wybierz język · Выберите язык",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Cream,
                    FontSize = 13,
                },
                list,
                button,
            },
        };

        return await dialog.ShowDialog<string>(owner) ?? chosen;
    }
}

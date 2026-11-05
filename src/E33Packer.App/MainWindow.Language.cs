using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using E33ModEditor.Core.Localization;
using static E33Packer.App.AppLocalization;

namespace E33Packer.App;

public sealed partial class MainWindow
{
    private Control BuildLanguageTab()
    {
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(Note(T("Language of the pack and the app.")));

        // A rebuilt tab must use a fresh radio group. Reusing the old group name
        // lets the detached previous tab participate in group selection and
        // clears the checked state on the new controls.
        var group = "pack-language-" + (++_languageGroup);
        foreach (var lang in ModLocalization.Supported)
        {
            var radio = new RadioButton
            {
                GroupName = group,
                Content = lang.DisplayName,
                IsChecked = string.Equals(_profile.Language, lang.Code, StringComparison.OrdinalIgnoreCase),
                Foreground = Cream,
                FontSize = 14,
                Margin = new Thickness(0, 2, 0, 2),
            };
            ToolTip.SetTip(radio, T("Changes the pack and app language."));
            radio.Checked += (_, _) =>
            {
                if (radio.IsChecked != true) return;
                if (_profile.Language == lang.Code) return;
                ApplyLanguage(lang.Code);
            };

            var card = new StackPanel { Spacing = 2 };
            card.Children.Add(radio);
            card.Children.Add(Note(AppLocalization.NativeSubtitle(lang.Code)));
            stack.Children.Add(Card(card));
        }

        return stack;
    }
}

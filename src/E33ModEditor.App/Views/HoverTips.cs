using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace E33ModEditor.App.Views;

internal static class HoverTips
{
    private static readonly IBrush TitleFg = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush BodyFg = new SolidColorBrush(Color.Parse("#FFD4CBB8"));

    public static void Set(Control target, string title, string body)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
        {
            ToolTip.SetTip(target, null);
            return;
        }
        var stack = new StackPanel { MaxWidth = 440, Spacing = 4 };
        if (!string.IsNullOrWhiteSpace(title))
        {
            stack.Children.Add(new TextBlock
            {
                Text = title.Trim(),
                Foreground = TitleFg,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        if (!string.IsNullOrWhiteSpace(body))
        {
            stack.Children.Add(new TextBlock
            {
                Text = body.Trim(),
                Foreground = BodyFg,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        ToolTip.SetTip(target, stack);
        ToolTip.SetShowDelay(target, 200);
    }

    public static void Set(Control target, string body) => Set(target, "", body);
}

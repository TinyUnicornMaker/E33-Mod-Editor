using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace E33Packer.App;

/// <summary>Opaque, lightweight window shown while the main window is built.</summary>
internal sealed class LoadingWindow : Window
{
    private readonly TextBlock _status;

    public LoadingWindow()
    {
        Title = "Expedition Infinity";
        Icon = AppIcon.Icon;
        Width = 520;
        Height = 300;
        CanResize = false;
        ShowInTaskbar = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SystemDecorations = SystemDecorations.None;
        Background = new SolidColorBrush(Color.Parse("#FF161411"));
        Topmost = true;

        _status = new TextBlock
        {
            Text = AppLocalization.T("Loading…"),
            Foreground = new SolidColorBrush(Color.Parse("#FFEFE3C8")),
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };

        Content = new StackPanel
        {
            Margin = new Thickness(32),
            Spacing = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = $"Expedition Infinity {GeneratorInfo.AppVersion}",
                    Foreground = new SolidColorBrush(Color.Parse("#FFC9A961")),
                    FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
                    FontSize = 26,
                    HorizontalAlignment = HorizontalAlignment.Center,
                },
                _status,
                new ProgressBar
                {
                    IsIndeterminate = true,
                    Height = 8,
                    MinHeight = 8,
                },
            },
        };
    }

    public void ShowError(string message)
    {
        _status.Text = AppLocalization.T("Could not open the generator.") + "\n" + message;
        _status.Foreground = new SolidColorBrush(Color.Parse("#FFE0A0A0"));
        Topmost = false;
    }
}

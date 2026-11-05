using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace E33Packer.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (Program.AnotherInstanceRunning)
            {
                AppLocalization.SetLanguage(AppSettings.Load().Language);
                desktop.MainWindow = BuildAlreadyRunningWindow();
                base.OnFrameworkInitializationCompleted();
                return;
            }

            AppLocalization.SetLanguage(AppSettings.Load().Language);
            var loading = new LoadingWindow();
            desktop.MainWindow = loading;
            loading.Opened += async (_, _) =>
            {
                // Let the opaque splash paint before doing the heavier startup work.
                await Task.Delay(180);

                MainWindow main;
                try
                {
                    main = new MainWindow();
                }
                catch (Exception ex)
                {
                    loading.ShowError(ex.Message);
                    return;
                }

                main.WindowStartupLocation = WindowStartupLocation.Manual;
                main.Position = new PixelPoint(-12000, -12000);
                main.ShowInTaskbar = false;
                main.Opened += async (_, _) =>
                {
                    // Render the fully built window off-screen, then move it into
                    // place only after the first frame is ready.
                    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
                    await Task.Delay(100);
                    var screen = main.Screens.Primary
                        ?? main.Screens.All.FirstOrDefault();
                    if (screen is not null)
                    {
                        var area = screen.WorkingArea;
                        var width = (int)(main.Width * screen.Scaling);
                        var height = (int)(main.Height * screen.Scaling);
                        main.Position = new PixelPoint(
                            area.X + Math.Max(0, (area.Width - width) / 2),
                            area.Y + Math.Max(0, (area.Height - height) / 2));
                    }
                    else
                    {
                        main.Position = new PixelPoint(100, 100);
                    }
                    desktop.MainWindow = main;
                    main.ShowInTaskbar = true;
                    main.Opacity = 1;
                    loading.Close();
                    main.MarkRevealed();
                };
                main.Show();
            };
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static Window BuildAlreadyRunningWindow() => new()
    {
        Title = "Expedition Infinity",
        Icon = AppIcon.Icon,
        Width = 470,
        Height = 175,
        CanResize = false,
        WindowStartupLocation = WindowStartupLocation.CenterScreen,
        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = AppLocalization.T("Expedition Infinity is already running."),
                    FontSize = 18,
                    Foreground = new Avalonia.Media.SolidColorBrush(
                        Avalonia.Media.Color.Parse("#FFC9A961")),
                },
                new TextBlock
                {
                    Text = AppLocalization.T("Close the other generator window before starting another build. Two instances would race over the donor cache and the installed seed."),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
            },
        },
    };
}

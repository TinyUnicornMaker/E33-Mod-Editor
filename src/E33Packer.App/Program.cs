using Avalonia;
using System;

namespace E33Packer.App;

internal static class Program
{
    internal static bool AnotherInstanceRunning { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        using var instance = SingleInstance.TryAcquire();
        AnotherInstanceRunning = instance is null;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

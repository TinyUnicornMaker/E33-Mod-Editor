using System;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace E33Packer.App;

/// <summary>Shared window icon loaded from the embedded application assets.</summary>
internal static class AppIcon
{
    private static readonly Uri Source =
        new($"avares://{typeof(AppIcon).Assembly.GetName().Name}/Assets/app-256.png");

    private static WindowIcon? _icon;

    public static WindowIcon Icon => _icon ??= Load();

    private static WindowIcon Load()
    {
        using var stream = AssetLoader.Open(Source);
        return new WindowIcon(new Bitmap(stream));
    }
}

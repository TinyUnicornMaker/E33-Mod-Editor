namespace E33ModEditor.Core.Packer;

/// <summary>
/// Finds a Clair Obscur: Expedition 33 install from Steam libraries.
/// Accepts the game root, Sandfall, Content, or Paks as user input.
/// </summary>
public static class GameLocator
{
    public const uint SteamAppId = 1903340;

    public static readonly string[] CommonFolderNames =
    {
        "Expedition 33",
        "Clair Obscur Expedition 33",
        "Clair Obscur: Expedition 33",
    };

    /// <summary>
    /// Walk <paramref name="path"/> and its parents until
    /// <c>Sandfall/Content/Paks</c> with at least one <c>.utoc</c> is found.
    /// </summary>
    public static string? NormalizeGameRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        DirectoryInfo? dir;
        try { dir = new DirectoryInfo(path); }
        catch { return null; }
        while (dir is not null)
        {
            var paks = Path.Combine(dir.FullName, "Sandfall", "Content", "Paks");
            if (IsPaksDir(paks)) return dir.FullName;
            if (string.Equals(dir.Name, "Paks", StringComparison.OrdinalIgnoreCase)
                && IsPaksDir(dir.FullName)
                && dir.Parent?.Name == "Content"
                && dir.Parent.Parent?.Name == "Sandfall"
                && dir.Parent.Parent.Parent is { } root)
                return root.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    public static bool IsPaksDir(string paks) =>
        Directory.Exists(paks)
        && Directory.EnumerateFiles(paks, "*.utoc", SearchOption.TopDirectoryOnly).Any();

    public static string? PaksDirOf(string gameRoot) =>
        IsPaksDir(Path.Combine(gameRoot, "Sandfall", "Content", "Paks"))
            ? Path.Combine(gameRoot, "Sandfall", "Content", "Paks")
            : null;

    public static string? FindInstall()
    {
        foreach (var candidate in Candidates())
        {
            var root = NormalizeGameRoot(candidate);
            if (root is not null) return root;
        }
        return null;
    }

    private static IEnumerable<string> Candidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var steam in SteamRoots())
        {
            foreach (var library in LibraryRoots(steam).Prepend(steam))
            {
                var common = Path.Combine(library, "steamapps", "common");
                if (!Directory.Exists(common)) continue;
                foreach (var name in CommonFolderNames)
                {
                    var p = Path.Combine(common, name);
                    if (seen.Add(p)) yield return p;
                }
                foreach (var dir in Directory.EnumerateDirectories(common))
                {
                    if (!dir.Contains("Expedition", StringComparison.OrdinalIgnoreCase)
                        && !dir.Contains("Obscur", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (seen.Add(dir)) yield return dir;
                }
            }
        }
    }

    private static IEnumerable<string> SteamRoots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            yield return @"C:\Program Files (x86)\Steam";
            yield return @"C:\Program Files\Steam";
            yield return Path.Combine(home, "Steam");
            yield break;
        }
        yield return Path.Combine(home, ".steam", "debian-installation");
        yield return Path.Combine(home, ".steam", "steam");
        yield return Path.Combine(home, ".local", "share", "Steam");
        yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam",
            ".local", "share", "Steam");
        yield return Path.Combine(home, "Steam");
        yield return Path.Combine(home, ".steam", "root");
    }

    private static IEnumerable<string> LibraryRoots(string steamRoot)
    {
        var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) yield break;
        foreach (var line in File.ReadLines(vdf))
        {
            var parts = line.Trim().Split('"');
            if (parts.Length >= 4 && parts[1].Equals("path", StringComparison.OrdinalIgnoreCase))
                yield return parts[3].Replace(@"\\", @"\");
        }
    }
}

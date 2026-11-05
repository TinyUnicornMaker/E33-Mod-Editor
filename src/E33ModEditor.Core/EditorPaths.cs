namespace E33ModEditor.Core;

/// <summary>
/// Locates repo tools (retoc, mappings) and the game install.
/// The standalone packer calls <see cref="Configure"/> so this does not
/// walk for a git checkout.
/// </summary>
public static class EditorPaths
{
    private static string? _repoRoot;
    private static string? _toolsDir;

    /// <summary>
    /// Treat <paramref name="repoRoot"/> as the editor root (work/, Export/).
    /// <paramref name="toolsDir"/> is where retoc and the usmap live (the
    /// published exe directory for the packer).
    /// </summary>
    public static void Configure(string repoRoot, string paksDir, string? toolsDir = null,
        string? modsDir = null)
    {
        _repoRoot = Path.GetFullPath(repoRoot);
        _toolsDir = string.IsNullOrEmpty(toolsDir) ? null : Path.GetFullPath(toolsDir);
        PaksDir = Path.GetFullPath(paksDir);
        ModsDirOverride = string.IsNullOrEmpty(modsDir) ? null : Path.GetFullPath(modsDir);
        Directory.CreateDirectory(Path.Combine(_repoRoot, "work", "tables"));
        Directory.CreateDirectory(Path.Combine(_repoRoot, "work", "mod"));
        Directory.CreateDirectory(Path.Combine(_repoRoot, "Export"));
        Directory.CreateDirectory(Path.Combine(_repoRoot, "work", "tmp"));
    }

    public static string RepoRoot =>
        _repoRoot
        ?? Environment.GetEnvironmentVariable("E33_REPO")
        ?? FindRepoRoot(AppContext.BaseDirectory)
        ?? Path.GetFullPath(AppContext.BaseDirectory);

    public static string ToolsDir => _toolsDir ?? Path.Combine(RepoRoot, "tools");

    public static string RetocExe
    {
        get
        {
            foreach (var dir in CandidateToolDirs())
            {
                if (!Directory.Exists(dir)) continue;
                var names = OperatingSystem.IsWindows()
                    ? new[] { "retoc.exe", "retoc" }
                    : new[] { "retoc", "retoc.exe" };
                foreach (var name in names)
                {
                    var exact = Path.Combine(dir, name);
                    if (File.Exists(exact)) return exact;
                }
                var nested = Directory.EnumerateFiles(dir, "retoc*", SearchOption.AllDirectories)
                    .FirstOrDefault(f =>
                    {
                        var stem = Path.GetFileNameWithoutExtension(f);
                        return stem.Equals("retoc", StringComparison.OrdinalIgnoreCase);
                    });
                if (nested is not null) return nested;
            }
            throw new FileNotFoundException(
                "Bundled retoc binary is missing. Reinstall the application.");
        }
    }

    public static string Mappings
    {
        get
        {
            foreach (var dir in CandidateToolDirs())
            {
                var map = Path.Combine(dir, "Mappings.usmap");
                if (File.Exists(map)) return map;
            }
            return "";
        }
    }

    /// <summary>repak binary (bundled next to the standalone app).</summary>
    public static string RepakExe
    {
        get
        {
            var names = OperatingSystem.IsWindows()
                ? new[] { "repak.exe", "repak" }
                : new[] { "repak", "repak.exe" };
            foreach (var dir in CandidateToolDirs())
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var name in names)
                {
                    var nested = Path.Combine(dir, "repak", name);
                    if (File.Exists(nested)) return nested;
                    var flat = Path.Combine(dir, name);
                    if (File.Exists(flat)) return flat;
                }
            }
            throw new FileNotFoundException(
                "repak binary not found. It is bundled with the standalone packer; " +
                "for a source checkout use tools/repak/repak.");
        }
    }

    /// <summary>Mark a bundled native tool executable after single-file extraction.</summary>
    public static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows() || string.IsNullOrEmpty(path)) return;
        try
        {
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch
        {
            // Best effort; Process.Start will report the real error.
        }
    }

    /// <summary>Game Paks directory. Override with E33_PAKS env var.</summary>
    public static string PaksDir { get; set; } =
        Environment.GetEnvironmentVariable("E33_PAKS") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".steam/steam/steamapps/common/Expedition 33/Sandfall/Content/Paks");

    /// <summary>Explicit output folder; null means <c>Paks/~mods</c>.</summary>
    public static string? ModsDirOverride { get; set; }

    public static string ModsDir => ModsDirOverride ?? Path.Combine(PaksDir, "~mods");

    /// <summary>Packed .pak/.utoc/.ucas land here as one shareable modpack.</summary>
    public static string ExportDir => Path.Combine(RepoRoot, "Export");

    /// <summary>
    /// Scratch space for diagnostic runs and transient staging. Lives inside
    /// the repo (work/tmp) rather than the OS temp folder so paths behave
    /// identically for every user on every OS. Safe to delete at any time;
    /// callers must create their own subdirectory via <see cref="TempSubdir"/>.
    /// </summary>
    public static string TempDir => Path.Combine(RepoRoot, "work", "tmp");

    /// <summary>Returns (creating) a named subdirectory of <see cref="TempDir"/>.</summary>
    public static string TempSubdir(string name)
    {
        var dir = Path.Combine(TempDir, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static IEnumerable<string> CandidateToolDirs()
    {
        yield return ToolsDir;
        yield return AppContext.BaseDirectory;
        var repoTools = Path.Combine(RepoRoot, "tools");
        if (!string.Equals(repoTools, ToolsDir, StringComparison.Ordinal))
            yield return repoTools;
    }

    private static string? FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "tools")))
            dir = dir.Parent;
        return dir?.FullName;
    }
}

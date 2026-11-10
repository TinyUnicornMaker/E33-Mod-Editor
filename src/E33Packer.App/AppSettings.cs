using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace E33Packer.App;

public sealed class AppSettings
{
    public string? GameFolder { get; set; }
    public string? ModsFolder { get; set; }

    /// <summary>
    /// Optional folder for the one-time donor extraction. Defaults to
    /// <c>work/tables</c> inside <see cref="DataRoot"/>. The extractor and every
    /// donor lookup still use <c>work/tables</c>; when a custom folder is chosen
    /// that path is transparently linked to this one.
    /// </summary>
    public string? ExtractionFolder { get; set; }
    public int Seed { get; set; } = 123456;
    public string ProfileName { get; set; } = GeneratorPresets.Expedition;
    public string Language { get; set; } = "en";
    public bool LanguageChosen { get; set; }
    public bool AutoOpenSpoiler { get; set; } = true;
    public bool AutoOpenOutput { get; set; } = true;
    public bool ExtractionWarningShown { get; set; }
    public double WindowWidth { get; set; } = 980;
    public double WindowHeight { get; set; } = 820;

    /// <summary>
    /// The application folder. The portable generator keeps settings, profiles,
    /// the donor cache and the seed library beside the executable so everything
    /// it extracts stays inside the installed application folder.
    /// </summary>
    [JsonIgnore]
    public static string DataRoot
    {
        get
        {
            // Test/dev override so a build can keep its scratch inside the repo.
            var overridden = Environment.GetEnvironmentVariable("E33_DATA_ROOT");
            if (!string.IsNullOrWhiteSpace(overridden))
                return Path.GetFullPath(overridden);

            // In a single-file publish AppContext.BaseDirectory is the temporary
            // self-extraction folder. Environment.ProcessPath points at the real
            // executable, so portable data lands beside the app instead.
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                var name = Path.GetFileNameWithoutExtension(processPath);
                if (!name.Equals("dotnet", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("testhost", StringComparison.OrdinalIgnoreCase))
                {
                    var dir = Path.GetDirectoryName(Path.GetFullPath(processPath));
                    if (!string.IsNullOrWhiteSpace(dir)) return dir;
                }
            }
            return Path.GetFullPath(AppContext.BaseDirectory);
        }
    }

    /// <summary>The folder that holds the extracted game data (donor cache).</summary>
    [JsonIgnore]
    public string ExtractionTablesFolder =>
        string.IsNullOrWhiteSpace(ExtractionFolder)
            ? Path.Combine(DataRoot, "work", "tables")
            : Path.GetFullPath(ExtractionFolder);

    [JsonIgnore]
    public bool UsesDefaultExtractionFolder =>
        string.Equals(
            Path.GetFullPath(ExtractionTablesFolder),
            Path.GetFullPath(Path.Combine(DataRoot, "work", "tables")),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Points <c>work/tables</c> at <see cref="ExtractionTablesFolder"/> so the
    /// existing donor lookups keep working unchanged. Existing cache contents
    /// follow the user when the folder changes. Throws with an actionable
    /// message when the link cannot be created (permissions, network drive).
    /// </summary>
    public void PrepareExtractionFolder()
    {
        var linkPath = Path.GetFullPath(Path.Combine(DataRoot, "work", "tables"));
        var targetPath = Path.GetFullPath(ExtractionTablesFolder);
        var parent = Path.GetDirectoryName(linkPath)!;
        Directory.CreateDirectory(parent);

        if (PathsEqual(linkPath, targetPath))
        {
            if (IsDirectoryLink(linkPath))
            {
                var oldTarget = ResolveDirectoryLink(linkPath);
                Directory.Delete(linkPath);
                Directory.CreateDirectory(linkPath);
                if (oldTarget is not null && !PathsEqual(oldTarget, linkPath))
                    MoveContents(oldTarget, linkPath);
            }
            else
            {
                Directory.CreateDirectory(linkPath);
            }
            return;
        }

        Directory.CreateDirectory(targetPath);

        if (IsDirectoryLink(linkPath))
        {
            var oldTarget = ResolveDirectoryLink(linkPath);
            if (oldTarget is not null && PathsEqual(oldTarget, targetPath)) return;
            Directory.Delete(linkPath);
            if (oldTarget is not null && Directory.Exists(oldTarget) &&
                !PathsEqual(oldTarget, targetPath))
                MoveContents(oldTarget, targetPath);
        }
        else if (Directory.Exists(linkPath))
        {
            MoveContents(linkPath, targetPath);
            Directory.Delete(linkPath);
        }
        else if (File.Exists(linkPath))
        {
            throw new IOException(linkPath + " is a file, not a folder.");
        }

        CreateDirectoryLink(linkPath, targetPath);
    }

    /// <summary>Restores the default extraction folder.</summary>
    public void ClearExtractionFolder()
    {
        ExtractionFolder = null;
        PrepareExtractionFolder();
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsDirectoryLink(string path)
    {
        try
        {
            return Directory.Exists(path) &&
                   (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? ResolveDirectoryLink(string path)
    {
        try { return Directory.ResolveLinkTarget(path, true)?.FullName; }
        catch { return null; }
    }

    private static void CreateDirectoryLink(string linkPath, string targetPath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    Directory.CreateSymbolicLink(linkPath, targetPath);
                    return;
                }
                catch
                {
                    using var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c mklink /J \"{linkPath}\" \"{targetPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    });
                    p?.WaitForExit();
                    if (p is { ExitCode: 0 } && Directory.Exists(linkPath)) return;
                }
            }
            else
            {
                Directory.CreateSymbolicLink(linkPath, targetPath);
                return;
            }
        }
        catch (Exception ex)
        {
            throw new IOException(
                $"Could not link {linkPath} to {targetPath}: {ex.Message}", ex);
        }
        throw new IOException($"Could not link {linkPath} to {targetPath}.");
    }

    private static void MoveContents(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            var name = Path.GetFileName(entry);
            var dest = Path.Combine(target, name);
            if (Directory.Exists(entry))
            {
                if (Directory.Exists(dest))
                    MoveContents(entry, dest);
                else
                    Directory.Move(entry, dest);
            }
            else
            {
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(entry, dest);
            }
        }
    }

    [JsonIgnore]
    public static string SettingsPath => Path.Combine(DataRoot, "settings.json");

    [JsonIgnore]
    public static string ProfilesDir => Path.Combine(DataRoot, "profiles");

    [JsonIgnore]
    public static string HistoryPath => Path.Combine(DataRoot, "seed-history.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath))
                       ?? new AppSettings();
        }
        catch { /* first run / corrupt */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DataRoot);
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best effort */ }
    }
}

public sealed class SeedHistoryEntry
{
    public int Seed { get; set; }
    public string BuiltAt { get; set; } = DateTime.UtcNow.ToString("o");
    public string ProfileName { get; set; } = "";
    public string Language { get; set; } = "en";
    public string PackName { get; set; } = "";
    public string PackPath { get; set; } = "";
    public string SpoilerPath { get; set; } = "";
    public string ProfileJson { get; set; } = "";
}

public sealed class SeedHistory
{
    public List<SeedHistoryEntry> Entries { get; set; } = new();

    public static SeedHistory Load()
    {
        try
        {
            if (File.Exists(AppSettings.HistoryPath))
                return JsonSerializer.Deserialize<SeedHistory>(File.ReadAllText(AppSettings.HistoryPath))
                       ?? new SeedHistory();
        }
        catch { /* first run */ }
        return new SeedHistory();
    }

    public void Add(SeedHistoryEntry entry)
    {
        Entries.Insert(0, entry);
        if (Entries.Count > 20) Entries.RemoveRange(20, Entries.Count - 20);
        Save();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.DataRoot);
            File.WriteAllText(AppSettings.HistoryPath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best effort */ }
    }
}

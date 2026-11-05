using System.Text.Json;
using System.Text.Json.Serialization;

namespace E33Packer.App;

public sealed class AppSettings
{
    public string? GameFolder { get; set; }
    public string? ModsFolder { get; set; }
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

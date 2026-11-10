using System.Diagnostics;

namespace E33ModEditor.Core.Packer;

/// <summary>
/// Pulls vanilla donor assets from the user's game via retoc into
/// <c>work/tables</c>. ~mods is moved aside for the duration so the extract
/// is not shadowed by an installed pack.
/// </summary>
public static class DonorExtract
{
    public static IReadOnlyList<string> Filters()
    {
        var asm = typeof(DonorExtract).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("donor_filters.txt", StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException("donor_filters.txt");
        using var s = asm.GetManifestResourceStream(name)
                      ?? throw new FileNotFoundException("donor_filters.txt");
        using var r = new StreamReader(s);
        var list = new List<string>();
        while (r.ReadLine() is { } line)
        {
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            list.Add(line);
        }
        return list;
    }

    /// <summary>
    /// Turns a donor filter name into a short, user-facing description. The
    /// full filter still goes to the log; the first-run overlay uses this.
    /// </summary>
    public static string DescribeFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return "checking the game data";
        var f = filter.TrimEnd('_');
        bool Has(string value) => f.Contains(value, StringComparison.OrdinalIgnoreCase);

        if (Has("StringTable") || f.StartsWith("ST_", StringComparison.OrdinalIgnoreCase) ||
            Has("Localization") || Has("locres")) return "text and localization";
        if (Has("Enem") || Has("Monster")) return "enemy data";
        if (Has("Weapon") || Has("Gear")) return "weapon and gear data";
        if (Has("Skill")) return "skill data";
        if (Has("Picto")) return "picto data";
        if (Has("Chest") || Has("Loot") || Has("Merchant")) return "loot and merchant data";
        if (Has("Battle") || Has("Encounter")) return "battle data";
        if (Has("Character") || Has("Party")) return "character data";
        if (f.StartsWith("BP_", StringComparison.OrdinalIgnoreCase)) return "game blueprints";
        if (f.StartsWith("DA_", StringComparison.OrdinalIgnoreCase)) return "game data assets";
        return "game assets";
    }

    /// <summary>How many manifest stems are already on disk under work/tables.</summary>
    public static (int Have, int Total) CacheStatus()
    {
        var filters = Filters();
        int have = 0;
        foreach (var f in filters)
        {
            if (IsPresent(f)) have++;
        }
        return (have, filters.Count);
    }

    /// <param name="progress">
    /// <c>(done, total, currentFilter)</c> after each retoc call. Total is the
    /// number of missing filters for this run; done starts at 0.
    /// </param>
    public static int EnsureAll(Action<string>? log = null,
        Action<int, int, string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var missing = Filters().Where(f => !IsPresent(f)).ToList();
        if (missing.Count == 0)
        {
            EnsureLegacyLookupLinks();
            log?.Invoke("Donor cache complete.");
            progress?.Invoke(0, 0, "");
            return 0;
        }
        try
        {
            var tables = Path.Combine(EditorPaths.RepoRoot, "work", "tables");
            // Follow the extraction-folder link so the free-space check looks at
            // the drive that will actually receive the cache.
            var resolved = Directory.ResolveLinkTarget(tables, true)?.FullName ?? tables;
            var root = Path.GetPathRoot(Path.GetFullPath(resolved));
            if (!string.IsNullOrEmpty(root))
            {
                var required = Math.Max(256L * 1024 * 1024,
                    missing.Count * 3L * 1024 * 1024);
                var free = new DriveInfo(root).AvailableFreeSpace;
                if (free < required)
                    throw new IOException(
                        $"Not enough free space for game data: need about {required / (1024 * 1024)} MB, " +
                        $"only {free / (1024 * 1024)} MB available.");
            }
        }
        catch (IOException) { throw; }
        catch { /* drive info unavailable; extraction will report real errors */ }

        log?.Invoke($"Extracting {missing.Count} missing donor filter(s) from the game…");
        progress?.Invoke(0, missing.Count, missing[0]);
        int n = 0;
        using (SuspendMods())
        {
            var retoc = new Retoc();
            foreach (var filter in missing)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsPresent(filter))
                {
                    n++;
                    var next = n < missing.Count ? missing[n] : "";
                    progress?.Invoke(n, missing.Count, next);
                    continue;
                }
                var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables",
                    filter.TrimEnd('_'));
                Directory.CreateDirectory(dir);
                log?.Invoke("  retoc " + filter);
                progress?.Invoke(n, missing.Count, filter);
                retoc.ToLegacy(dir, filter);
                if (filter.EndsWith('_'))
                {
                    var produced = Directory.EnumerateFiles(dir, "*.uasset",
                        SearchOption.AllDirectories).Any(f => new FileInfo(f).Length > 0);
                    if (produced) MarkPresent(filter);
                    else log?.Invoke($"  warning: {filter} matched no assets; cache not marked complete");
                }
                n++;
                var after = n < missing.Count ? missing[n] : "";
                progress?.Invoke(n, missing.Count, after);
            }
        }
        EnsureLegacyLookupLinks();
        log?.Invoke($"Extracted {n} filter(s).");
        progress?.Invoke(n, missing.Count, "");
        return n;
    }

    /// <summary>
    /// A few kits still probe the full editor's scratch folders
    /// (work/probe-*, work/extract_maelle, extracted). A standalone data root
    /// only has work/tables. Point those legacy names at the same tree so a
    /// fresh install resolves the same donor assets.
    /// </summary>
    public static void EnsureLegacyLookupLinks()
    {
        try
        {
            var work = Path.Combine(EditorPaths.RepoRoot, "work");
            var tables = Path.Combine(work, "tables");
            if (!Directory.Exists(tables)) return;
            foreach (var link in new[]
            {
                Path.Combine(work, "probe-twilight"),
                Path.Combine(work, "probe-verso"),
                Path.Combine(work, "probe-skill"),
                Path.Combine(work, "probe-custom"),
                Path.Combine(work, "extract_maelle"),
                Path.Combine(EditorPaths.RepoRoot, "extracted"),
            })
                EnsureLink(link, tables);
        }
        catch { /* best effort */ }
    }

    private static void EnsureLink(string linkPath, string target)
    {
        try
        {
            if (Directory.Exists(linkPath) || File.Exists(linkPath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    Directory.CreateSymbolicLink(linkPath, target);
                }
                catch
                {
                    using var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c mklink /J \"{linkPath}\" \"{target}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    });
                    p?.WaitForExit();
                }
            }
            else
            {
                Directory.CreateSymbolicLink(linkPath, target);
            }
        }
        catch { /* best effort */ }
    }

    private static bool IsPresent(string filter)
    {
        if (filter.EndsWith('_'))
        {
            // A prefix filter is only complete when its extraction marker
            // exists. Counting matching files is not enough: a handful of
            // specific filters can already have produced >=5 files, which made
            // a broad family look complete while individual assets (e.g.
            // BP_Battle_SkillScript_BurningCanvas) were still missing.
            return File.Exists(MarkerPath(filter));
        }
        var asset = FileCopy.FindTablesAsset(filter + ".uasset");
        if (asset is null || new FileInfo(asset).Length == 0) return false;
        var exp = Path.ChangeExtension(asset, ".uexp");
        if (File.Exists(exp) && new FileInfo(exp).Length == 0) return false;
        return true;
    }

    private static string MarkerDir =>
        Path.Combine(EditorPaths.RepoRoot, "work", "tables", ".donor");

    private static string MarkerPath(string filter) =>
        Path.Combine(MarkerDir, filter.TrimEnd('_') + ".ok");

    private static void MarkPresent(string filter)
    {
        try
        {
            Directory.CreateDirectory(MarkerDir);
            File.WriteAllText(MarkerPath(filter), DateTime.UtcNow.ToString("o"));
        }
        catch { /* best effort */ }
    }

    /// <summary>
    /// Recovers a <c>~mods.packer-aside</c> folder left by a crashed extraction.
    /// Never deletes non-empty user files: if both folders exist they are merged.
    /// </summary>
    public static void RecoverSuspendedMods(Action<string>? log = null)
        => RecoverSuspendedMods(EditorPaths.ModsDir, log);

    public static void RecoverSuspendedMods(string mods, Action<string>? log = null)
    {
        var aside = mods + ".packer-aside";
        if (!Directory.Exists(aside)) return;

        if (!Directory.Exists(mods))
        {
            Directory.Move(aside, mods);
            log?.Invoke("Restored suspended mods folder from crash recovery.");
            return;
        }

        int restored = 0;
        foreach (var file in Directory.EnumerateFiles(aside, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(aside, file);
            var dest = Path.Combine(mods, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            if (!File.Exists(dest))
            {
                File.Copy(file, dest);
                restored++;
            }
        }
        try { Directory.Delete(aside, true); } catch { /* best effort */ }
        log?.Invoke($"Merged {restored} file(s) from a suspended mods folder.");
    }

    private static readonly object SuspendGate = new();
    private static int _suspendUsers;
    private static string? _suspendedMods;
    private static string? _suspendedAside;

    /// <summary>
    /// Move Paks/~mods aside so retoc to-legacy reads vanilla containers.
    /// Calls nest: only the outermost lease moves/restores the folder, so a
    /// caller can keep it suspended for a whole build while kits take their own
    /// extraction leases.
    /// </summary>
    public static IDisposable SuspendMods()
    {
        lock (SuspendGate)
        {
            _suspendUsers++;
            if (_suspendUsers == 1)
            {
                var mods = EditorPaths.ModsDir;
                RecoverSuspendedMods(mods);
                if (Directory.Exists(mods))
                {
                    var hasShadow = Directory.EnumerateFiles(mods, "*.pak", SearchOption.TopDirectoryOnly)
                        .Concat(Directory.EnumerateFiles(mods, "*.utoc", SearchOption.TopDirectoryOnly))
                        .Any();
                    if (hasShadow)
                    {
                        var aside = mods + ".packer-aside";
                        Directory.Move(mods, aside);
                        _suspendedMods = mods;
                        _suspendedAside = aside;
                    }
                }
            }
            return new SuspendLease();
        }
    }

    private static void ReleaseSuspend()
    {
        lock (SuspendGate)
        {
            if (_suspendUsers > 0) _suspendUsers--;
            if (_suspendUsers != 0) return;
            var mods = _suspendedMods;
            var aside = _suspendedAside;
            _suspendedMods = null;
            _suspendedAside = null;
            if (mods is null || aside is null) return;
            try
            {
                if (Directory.Exists(mods) && !Directory.EnumerateFileSystemEntries(mods).Any())
                    Directory.Delete(mods);
                if (Directory.Exists(mods))
                {
                    foreach (var file in Directory.EnumerateFiles(aside, "*", SearchOption.AllDirectories))
                    {
                        var rel = Path.GetRelativePath(aside, file);
                        var dest = Path.Combine(mods, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        File.Copy(file, dest, overwrite: true);
                    }
                    Directory.Delete(aside, true);
                }
                else
                    Directory.Move(aside, mods);
            }
            catch
            {
                // Best-effort restore; a later startup recovery will retry.
            }
        }
    }

    private sealed class SuspendLease : IDisposable
    {
        private bool _done;
        public void Dispose()
        {
            if (_done) return;
            _done = true;
            ReleaseSuspend();
        }
    }
}

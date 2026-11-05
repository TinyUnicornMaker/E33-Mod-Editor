using System.Diagnostics;
using System.Text;

namespace E33ModEditor.Core;

/// <summary>
/// Wrapper around trumank's retoc CLI for extracting and packing
/// UE5 IoStore (zen) containers.
/// </summary>
public sealed class Retoc
{
    public IEnumerable<string> Containers =>
        Directory.GetFiles(EditorPaths.PaksDir, "*.utoc", SearchOption.TopDirectoryOnly);

    /// <summary>Finds every container whose directory index lists the given asset.</summary>
    public IReadOnlyList<string> FindContainersFor(string assetNameSubstring)
    {
        var hits = new List<string>();
        foreach (var utoc in Containers)
        {
            if (Run($"list \"{utoc}\"", out var output) && output.Contains(assetNameSubstring, StringComparison.OrdinalIgnoreCase))
                hits.Add(utoc);
        }
        return hits;
    }

    /// <summary>
    /// Converts matching assets from the game's containers to legacy
    /// .uasset/.uexp files that UAssetAPI can edit. Input must be the whole
    /// Paks directory so retoc can resolve global/script-objects data.
    /// Refuses when the game's ~mods folder holds an installed mod pak:
    /// retoc resolves the mod container at higher priority, so "vanilla"
    /// extractions would silently come back modded (this already
    /// contaminated work/extracted once — 09-03). Move the pak set aside,
    /// re-extract, and re-pack (PackCombined reinstalls it) afterwards.
    /// </summary>
    public void ToLegacy(string outputDir, string filter)
    {
        var shadow = Directory.Exists(EditorPaths.ModsDir)
            ? Directory.EnumerateFiles(EditorPaths.ModsDir, "*.pak", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(EditorPaths.ModsDir, "*.utoc", SearchOption.TopDirectoryOnly))
                .Select(Path.GetFileName).ToList()
            : new List<string?>();
        if (shadow.Count > 0)
            throw new InvalidOperationException(
                $"Refusing to extract: {EditorPaths.ModsDir} holds installed mod container(s) " +
                $"({string.Join(", ", shadow)}) that would shadow vanilla assets in the extraction. " +
                "Move them aside, re-extract, then re-pack to reinstall.");
        Directory.CreateDirectory(outputDir);
        Run($"to-legacy --version UE5_4 -f \"{filter}\" \"{EditorPaths.PaksDir}\" \"{outputDir}\"", out _, failOnError: true);
    }

    /// <summary>
    /// Lists the asset paths in a single container's directory index (empty when
    /// the container is not indexed or cannot be read).
    /// </summary>
    public IReadOnlyList<string> ListPaths(string utoc)
    {
        var paths = new List<string>();
        if (!Run($"list --path \"{utoc}\"", out var output)) return paths;
        foreach (var line in output.Split('\n'))
        {
            var i = line.IndexOf("../../../", StringComparison.Ordinal);
            if (i < 0) continue;
            var path = line[i..].Trim().Trim('"');
            if (path.Length > 0) paths.Add(path);
        }
        return paths;
    }

    /// <summary>
    /// Extracts matching assets from an arbitrary Paks directory WITHOUT the
    /// <see cref="ToLegacy"/> ~mods guard. Used to pull a third-party mod's own
    /// assets (so we can patch them) from an isolated base+mod directory.
    /// </summary>
    public void ToLegacyFrom(string inputDir, string outputDir, string filter = "")
    {
        Directory.CreateDirectory(outputDir);
        var f = string.IsNullOrEmpty(filter) ? "" : $"-f \"{filter}\" ";
        Run($"to-legacy --version UE5_4 {f}\"{inputDir}\" \"{outputDir}\"",
            out _, failOnError: true);
    }

    /// <summary>Packs a legacy (loose .uasset/.uexp) tree into a priority zen mod container.</summary>
    public void PackToZen(string legacyDir, string outputUtoc)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputUtoc)!);
        Run($"to-zen --version UE5_4 \"{legacyDir}\" \"{outputUtoc}\"",
            out _, failOnError: true);
    }

    /// <summary>
    /// Extracts a container's chunks verbatim (plus a <c>manifest.json</c> mapping
    /// each chunk id to its asset path) WITHOUT parsing or re-serializing them.
    /// Use this instead of a legacy round-trip when repacking a third-party
    /// blueprint mod: <see cref="PackToZen"/> rewrites the zen header (import map /
    /// export indices) and the game rejects those re-indexed blueprints with an
    /// access violation at launch. Raw chunks are <c>[zen header][export data]</c>,
    /// so callers can splice edited export bytes into the tail and keep the
    /// author's original header byte-for-byte.
    /// </summary>
    public void UnpackRaw(string utoc, string outputDir)
    {
        // retoc creates the directory itself and fails if it already exists.
        if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
        Run($"unpack-raw \"{utoc}\" \"{outputDir}\"", out _, failOnError: true);
    }

    /// <summary>
    /// Packs a directory produced by <see cref="UnpackRaw"/> back into a zen
    /// container, preserving the original serialized header. Emits no legacy
    /// <c>.pak</c> stub — callers keep the container's original one.
    /// </summary>
    public void PackRaw(string inputDir, string outputUtoc)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputUtoc)!);
        Run($"pack-raw \"{inputDir}\" \"{outputUtoc}\"", out _, failOnError: true);
    }

    /// <summary>
    /// Packs every asset under <paramref name="legacyDir"/> into one
    /// <c>name.{pak,utoc,ucas}</c> set in <paramref name="outputDir"/> (default
    /// Export/), then optionally copies that set into the game's ~mods folder.
    /// </summary>
    public IReadOnlyList<string> PackCombined(string legacyDir, string name,
        string? locresCulture = null, string? outputDir = null, bool install = true)
    {
        var targetDir = string.IsNullOrWhiteSpace(outputDir)
            ? EditorPaths.ExportDir
            : Path.GetFullPath(outputDir);
        Directory.CreateDirectory(targetDir);
        // Drop stale files from an older pack so pak/utoc/ucas stay a matching set.
        // The locres pak is deliberately named zz_* so the engine mounts it after
        // pakchunk0 and the mod can override vanilla localization keys.
        var locName = "zz_" + name;
        foreach (var ext in new[] { ".pak", ".utoc", ".ucas" })
        {
            var stale = Path.Combine(targetDir, name + ext);
            if (File.Exists(stale)) File.Delete(stale);
        }
        var staleLoc = Path.Combine(targetDir, locName + ".pak");
        if (File.Exists(staleLoc)) File.Delete(staleLoc);

        var exportUtoc = Path.Combine(targetDir, name + ".utoc");
        PackToZen(legacyDir, exportUtoc);
        string? locPak = null;
        if (!string.IsNullOrEmpty(locresCulture))
            locPak = PackLocresPak(legacyDir, locName, locresCulture, targetDir);

        var written = new List<string>();
        foreach (var ext in new[] { ".pak", ".utoc", ".ucas" })
        {
            var src = Path.Combine(targetDir, name + ext);
            if (File.Exists(src)) written.Add(src);
        }
        if (locPak is not null && File.Exists(locPak)) written.Add(locPak);
        if (install) InstallPack(written, EditorPaths.ModsDir);
        return written;
    }

    /// <summary>
    /// Copies a packed trio into an install folder. Files are staged under
    /// temporary names and moved into place, so an interrupted copy does not
    /// leave a half-written container under the real name.
    /// </summary>
    public IReadOnlyList<string> InstallPack(IReadOnlyList<string> files, string? targetDir = null,
        Action? beforeCommit = null)
    {
        var dir = string.IsNullOrWhiteSpace(targetDir) ? EditorPaths.ModsDir : Path.GetFullPath(targetDir);
        Directory.CreateDirectory(dir);
        var staged = new List<(string Temp, string Dest)>();
        try
        {
            // Stage every file before touching the live install, so a failed
            // copy leaves the previously installed seed intact.
            foreach (var src in files)
            {
                var dest = Path.Combine(dir, Path.GetFileName(src));
                var temp = dest + ".installing";
                File.Copy(src, temp, true);
                staged.Add((temp, dest));
            }
            beforeCommit?.Invoke();
            var installed = new List<string>();
            foreach (var (temp, dest) in staged)
            {
                File.Move(temp, dest, overwrite: true);
                installed.Add(dest);
            }
            return installed;
        }
        finally
        {
            foreach (var (temp, _) in staged)
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch { /* best effort */ }
            }
        }
    }

    /// <summary>
    /// Transactional install: stages the new trio, moves every existing
    /// ExpeditionInfinity pack to a backup folder, commits the new files, and
    /// restores the old files if any commit step fails.
    /// </summary>
    public IReadOnlyList<string> InstallPackAtomic(IReadOnlyList<string> files,
        string targetDir, Action<string> log)
    {
        var dir = string.IsNullOrWhiteSpace(targetDir)
            ? EditorPaths.ModsDir
            : Path.GetFullPath(targetDir);
        Directory.CreateDirectory(dir);

        var backup = Path.Combine(dir, ".install-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);
        var staged = new List<(string Temp, string Dest)>();
        var old = new List<(string Backup, string Dest)>();
        var installed = new List<string>();
        var committed = false;
        try
        {
            foreach (var stale in Directory.EnumerateFiles(dir, "*.installing",
                         SearchOption.TopDirectoryOnly))
            {
                try { File.Delete(stale); } catch { /* best effort */ }
            }

            foreach (var src in files)
            {
                var dest = Path.Combine(dir, Path.GetFileName(src));
                var temp = dest + ".installing";
                File.Copy(src, temp, true);
                staged.Add((temp, dest));
            }

            foreach (var ext in new[] { ".pak", ".utoc", ".ucas" })
            {
                foreach (var pattern in new[] { "ExpeditionInfinity*_P" + ext, "zz_ExpeditionInfinity*_P" + ext })
                {
                    foreach (var existing in Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly))
                    {
                        var dest = Path.Combine(dir, Path.GetFileName(existing));
                        var saved = Path.Combine(backup, Path.GetFileName(existing));
                        File.Move(existing, saved);
                        old.Add((saved, dest));
                    }
                }
            }

            foreach (var (temp, dest) in staged)
            {
                File.Move(temp, dest);
                installed.Add(dest);
            }

            committed = true;
            try { Directory.Delete(backup, true); } catch { /* best effort */ }
            return installed;
        }
        catch
        {
            foreach (var file in installed)
            {
                try { if (File.Exists(file)) File.Delete(file); } catch { /* best effort */ }
            }
            foreach (var (saved, dest) in old)
            {
                try { File.Move(saved, dest, overwrite: true); }
                catch { log("Could not restore previous pack file " + dest); }
            }
            throw;
        }
        finally
        {
            foreach (var (temp, _) in staged)
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch { /* best effort */ }
            }
            if (!committed)
            {
                try
                {
                    if (Directory.Exists(backup) &&
                        !Directory.EnumerateFileSystemEntries(backup).Any())
                        Directory.Delete(backup);
                }
                catch { /* best effort */ }
            }
        }
    }

    /// <summary>Run retoc's container integrity check.</summary>
    public void Verify(string utoc, Action<string>? log = null)
    {
        Run($"verify \"{utoc}\"", out var output, failOnError: true);
        var text = output.Trim();
        if (text.Length > 0) log?.Invoke("[retoc] verify: " + text);
    }

    /// <summary>
    /// Packs a localized Game.locres into a dedicated <c>zz_</c> .pak. retoc's
    /// to-zen only converts assets, so non-asset files (locres) go through
    /// repak. The zz_ prefix makes the engine mount this patch after pakchunk0,
    /// so modified vanilla keys override the base game's translations.
    /// </summary>
    private static string PackLocresPak(string legacyDir, string name, string culture,
        string outputDir)
    {
        var rel = Path.Combine("Sandfall", "Content", "Localization", "Game", culture, "Game.locres");
        var src = Path.Combine(legacyDir, rel);
        if (!File.Exists(src))
            throw new FileNotFoundException($"localized locres missing from stage: {src}");

        var temp = Path.Combine(EditorPaths.RepoRoot, "work", "tmp", "locpak", name);
        try
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
            var dest = Path.Combine(temp, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(src, dest);

            var repak = EditorPaths.RepakExe;

            var pak = Path.Combine(outputDir, name + ".pak");
            RunExe(repak, $"pack --version V11 --mount-point \"../../../\" \"{temp}\" \"{pak}\"", failOnError: true);
            var listing = RunExe(repak, $"list \"{pak}\"", failOnError: true);
            if (!listing.Contains(rel.Replace('\\', '/'), StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"localized .pak was created without {rel}; the culture would fall back to English");
            return pak;
        }
        finally
        {
            try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { /* best effort */ }
        }
    }

    private static string RunExe(string exe, string arguments, bool failOnError = false)
    {
        EditorPaths.MakeExecutable(exe);
        var psi = new ProcessStartInfo(exe, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {exe}.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(stdoutTask, stderrTask);
        var stdout = stdoutTask.Result;
        var stderr = stderrTask.Result;
        if (failOnError && process.ExitCode != 0)
            throw new InvalidOperationException($"{Path.GetFileName(exe)} failed ({process.ExitCode}):\n{stdout}{stderr}");
        return stdout;
    }

    /// <summary>
    /// How many assets a legacy-extraction filter would match, without writing
    /// anything (retoc dry run).
    ///
    /// <see cref="ToLegacy"/>'s filter is an UNANCHORED file-name substring
    /// match, so a short or generic term matches a huge slice of the game —
    /// filtering on "E" matches ~40,800 assets. Callers that derive a filter
    /// from data (e.g. an enum type name parsed out of an asset) MUST check this
    /// first; that mistake once dumped 32 GB of unrelated content into
    /// work/enums. Returns -1 when the count cannot be parsed.
    /// </summary>
    public int CountMatchingAssets(string filter)
    {
        Run($"to-legacy -d --no-shaders --version UE5_4 -f \"{filter}\" " +
            $"\"{EditorPaths.PaksDir}\" \"{EditorPaths.RepoRoot}\"", out var output);
        var m = System.Text.RegularExpressions.Regex.Match(
            output, @"Extracted\s+(\d+)\s+\(\d+ failed\)\s+legacy assets");
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : -1;
    }

    private static bool Run(string arguments, out string output, bool failOnError = false)
    {
        var exe = EditorPaths.RetocExe;
        // Single-file publishing extracts bundled tools without the executable
        // bit on Unix. Repair it before every launch; harmless on Windows.
        EditorPaths.MakeExecutable(exe);
        var psi = new ProcessStartInfo(exe, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        Process process;
        try
        {
            process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start retoc.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not start the bundled retoc at '{exe}'. " +
                "Check that the application folder and its .NET extraction cache are writable, " +
                "then try again.", ex);
        }
        using var _ = process;
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(stdoutTask, stderrTask);

        output = stdoutTask.Result + stderrTask.Result;
        if (failOnError && process.ExitCode != 0)
            throw new InvalidOperationException($"retoc failed ({process.ExitCode}):\n{output}");
        return process.ExitCode == 0;
    }
}

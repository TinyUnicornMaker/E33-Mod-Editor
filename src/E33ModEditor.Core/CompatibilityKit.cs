using System.Globalization;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.Core;

/// <summary>
/// Optional support for third-party mods that ship the same assets our pack
/// does. The Bigger Party Mod (by Heavylog81) owns the party/targeting logic in
/// <c>AC_jRPG_BattleManager</c>, <c>AC_jRPG_CharactersManager</c> and
/// <c>FL_jRPG_CustomFunctionLibrary</c>; when our pack also ships those paths it
/// shadows the mod (later-sorting <c>_P</c> pak wins) and ally-wide effects
/// stop reaching a 4th member.
///
/// With this enabled we stop shipping those three assets and instead patch our
/// gameplay values into the user's locally installed copy of the mod, in place,
/// with a backup. The third-party assets are never redistributed in our pack.
/// </summary>
public static class CompatibilityKit
{
    public const string PartyModAuthor = "Heavylog81";
    public const string PartyModDisplayName = "Bigger Party Mod";

    /// <summary>Assets the Party Mod owns that our pack would otherwise shadow.</summary>
    public static readonly string[] PatchAssets =
    {
        "AC_jRPG_BattleManager.uasset",
        "AC_jRPG_CharactersManager.uasset",
        "FL_jRPG_CustomFunctionLibrary.uasset",
    };

    /// <summary>Assets only the Party Mod ships — used to recognise it.</summary>
    private static readonly string[] SignatureAssets =
    {
        "BP_jRPG_Character_World.uasset",
        "WBP_GM_TeamOverview3D_Page.uasset",
    };

    public sealed class Settings
    {
        public bool Enabled { get; set; }
        public string? ModUtoc { get; set; }

        public JObject ToJson() => new()
        {
            ["Enabled"] = Enabled,
            ["ModUtoc"] = ModUtoc,
        };

        public void FromJson(JObject o)
        {
            if (o.Count == 0) return;
            Enabled = o.Value<bool?>("Enabled") ?? false;
            ModUtoc = o.Value<string?>("ModUtoc");
        }
    }

    /// <summary>Scans ~mods for the Bigger Party Mod and returns its .utoc path.</summary>
    public static string? Detect()
    {
        var mods = EditorPaths.ModsDir;
        if (!Directory.Exists(mods)) return null;
        var retoc = new Retoc();
        foreach (var utoc in Directory.EnumerateFiles(mods, "*.utoc", SearchOption.TopDirectoryOnly)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var stem = Path.GetFileNameWithoutExtension(utoc);
            if (stem.StartsWith("E33ModEditor", StringComparison.OrdinalIgnoreCase)) continue;
            var paths = retoc.ListPaths(utoc);
            if (SignatureAssets.All(a => paths.Any(p => p.EndsWith(a, StringComparison.OrdinalIgnoreCase))))
                return utoc;
        }
        return null;
    }

    /// <summary>True when <paramref name="utoc"/> is a recognised Bigger Party Mod.</summary>
    public static bool IsValidMod(string utoc)
    {
        if (string.IsNullOrWhiteSpace(utoc) || !File.Exists(utoc)) return false;
        var paths = new Retoc().ListPaths(utoc);
        return SignatureAssets.All(a => paths.Any(p => p.EndsWith(a, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Explicit path when valid, otherwise auto-detect.</summary>
    public static string? Resolve(Settings s) =>
        s.ModUtoc is { Length: > 0 } p && IsValidMod(p) ? p : Detect();

    /// <summary>
    /// Removes the three assets our pack would otherwise shadow the mod with.
    /// Called before packing when Compatibility is enabled.
    /// </summary>
    public static int StripConflicts(string modDir)
    {
        int n = 0;
        foreach (var asset in PatchAssets)
        {
            foreach (var file in Directory.EnumerateFiles(modDir, asset, SearchOption.AllDirectories).ToList())
            {
                File.Delete(file);
                n++;
                var uexp = Path.ChangeExtension(file, ".uexp");
                if (File.Exists(uexp)) { File.Delete(uexp); n++; }
            }
        }
        return n;
    }

    /// <summary>
    /// Patches our gameplay values into the locally installed Bigger Party Mod,
    /// in place, backing up the originals first.
    ///
    /// The edit is written by splicing the edited export-data bytes back into the
    /// ORIGINAL container chunks (<see cref="Retoc.UnpackRaw"/> /
    /// <see cref="Retoc.PackRaw"/>). A legacy round-trip (<c>to-zen</c>) rewrites
    /// the zen header — the import map and export indices — and the game rejects
    /// those re-indexed third-party blueprints with an access violation at launch.
    /// Raw chunks are <c>[zen header][export data]</c>, so replacing the tail
    /// keeps the author's header byte-for-byte.
    /// </summary>
    public static int Apply(string utoc, GameplayTweaksKit.Settings tweaks, Action<string>? log = null)
    {
        void L(string m) => log?.Invoke("[compat] " + m);
        if (!IsValidMod(utoc))
            throw new InvalidDataException($"{PartyModDisplayName} not found or unrecognised at '{utoc}'.");

        var modsDir = Path.GetDirectoryName(utoc)!;
        var stem = Path.GetFileNameWithoutExtension(utoc);

        // Always patch from the pristine copy. Re-patching an already-patched mod
        // compounds the edits until the blueprint is unloadable (launch crash),
        // so we keep the untouched original beside the dated backups and use it
        // as the extraction source every time.
        var pristine = Path.Combine(EditorPaths.RepoRoot, "work", "pak-backup", "compat", stem + "-pristine");
        if (!File.Exists(Path.Combine(pristine, stem + ".utoc")))
        {
            Directory.CreateDirectory(pristine);
            foreach (var ext in new[] { ".utoc", ".ucas", ".pak" })
            {
                var from = Path.Combine(modsDir, stem + ext);
                if (File.Exists(from)) File.Copy(from, Path.Combine(pristine, stem + ext), true);
            }
        }

        var work = EditorPaths.TempSubdir("compat");
        var src = Path.Combine(work, "src");
        var legacy = Path.Combine(work, "legacy");
        var raw = Path.Combine(work, "raw");
        var outDir = Path.Combine(work, "out");
        foreach (var d in new[] { src, legacy, outDir })
        {
            if (Directory.Exists(d)) Directory.Delete(d, true);
            Directory.CreateDirectory(d);
        }

        // Isolated base+mod directory: the global script objects plus the mod's
        // own files. With no base pak present retoc resolves the mod's assets.
        var paksDir = EditorPaths.PaksDir;
        foreach (var g in new[] { "global.utoc", "global.ucas" })
        {
            var from = Path.Combine(paksDir, g);
            if (File.Exists(from)) File.Copy(from, Path.Combine(src, g), true);
        }
        foreach (var ext in new[] { ".utoc", ".ucas", ".pak" })
        {
            var from = Path.Combine(pristine, stem + ext);
            if (File.Exists(from)) File.Copy(from, Path.Combine(src, stem + ext), true);
        }

        var retoc = new Retoc();
        L("extracting " + stem);
        retoc.ToLegacyFrom(src, legacy);

        // Script objects are the base game's, not the mod's — don't repack them.
        var sc = Path.Combine(legacy, "scriptobjects.bin");
        if (File.Exists(sc)) File.Delete(sc);

        // Snapshot the untouched export data so we can tell which assets the edit
        // changed and splice only those bytes back into the original chunks.
        var pristineUexp = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var ue in Directory.EnumerateFiles(legacy, "*.uexp", SearchOption.AllDirectories))
            pristineUexp[ue] = File.ReadAllBytes(ue);

        int edits = GameplayTweaksKit.ApplyCompatEdits(legacy, tweaks);
        L($"applied {edits} value edit(s) to the mod's copies");

        L("unpacking original container");
        retoc.UnpackRaw(Path.Combine(src, stem + ".utoc"), raw);

        var chunkByAsset = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (chunkId, assetPath) in RawChunks(raw))
            chunkByAsset[assetPath] = Path.Combine(raw, "chunks", chunkId);

        int spliced = 0;
        foreach (var (uexpPath, before) in pristineUexp)
        {
            var after = File.ReadAllBytes(uexpPath);
            if (before.AsSpan().SequenceEqual(after.AsSpan())) continue; // not an edited asset

            // Value edits must not change the export-data length; a length change
            // means UAssetAPI structurally rewrote the blueprint, which the game
            // will not accept. Refuse rather than ship a launch crash.
            if (after.Length != before.Length)
                throw new InvalidDataException(
                    $"compat: {Path.GetFileName(uexpPath)} changed length ({before.Length} -> {after.Length}); " +
                    "refusing to repack a structurally-rewritten blueprint.");

            var assetRel = Path.ChangeExtension(Path.GetRelativePath(legacy, uexpPath), ".uasset")
                .Replace('\\', '/');
            if (!chunkByAsset.TryGetValue(assetRel, out var chunkFile))
                throw new InvalidDataException($"compat: no container chunk for edited asset {assetRel}.");

            var chunk = File.ReadAllBytes(chunkFile);
            int off = chunk.Length - before.Length;
            if (off <= 0 || !chunk.AsSpan(off).SequenceEqual(before))
                throw new InvalidDataException($"compat: unexpected chunk layout for {Path.GetFileName(assetRel)}.");
            after.CopyTo(chunk, off);
            File.WriteAllBytes(chunkFile, chunk);
            spliced++;
        }
        L($"spliced {spliced} edited export(s) into the original chunks");

        var outUtoc = Path.Combine(outDir, stem + ".utoc");
        L("repacking");
        // pack-raw writes no legacy .pak stub; the container's own stub is
        // content-independent, so keep the untouched original.
        retoc.PackRaw(raw, outUtoc);
        var pristinePak = Path.Combine(pristine, stem + ".pak");
        if (File.Exists(pristinePak)) File.Copy(pristinePak, Path.Combine(outDir, stem + ".pak"), true);

        var backup = Path.Combine(EditorPaths.RepoRoot, "work", "pak-backup", "compat",
            stem + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(backup);
        foreach (var ext in new[] { ".utoc", ".ucas", ".pak" })
        {
            var from = Path.Combine(modsDir, stem + ext);
            if (File.Exists(from)) File.Copy(from, Path.Combine(backup, stem + ext), true);
        }
        L("backup -> " + backup);

        int installed = 0;
        foreach (var ext in new[] { ".utoc", ".ucas", ".pak" })
        {
            var from = Path.Combine(outDir, stem + ext);
            if (!File.Exists(from)) continue;
            File.Copy(from, Path.Combine(modsDir, stem + ext), true);
            installed++;
        }
        L($"patched {stem} in place ({installed} file(s))");
        return edits;
    }

    /// <summary>
    /// Chunk id -> asset path (relative to the game root) for a directory written
    /// by <see cref="Retoc.UnpackRaw"/>.
    /// </summary>
    private static IEnumerable<(string ChunkId, string AssetPath)> RawChunks(string rawDir)
    {
        var manifest = Path.Combine(rawDir, "manifest.json");
        if (!File.Exists(manifest)) yield break;
        if (JObject.Parse(File.ReadAllText(manifest))["chunk_paths"] is not JObject map) yield break;
        foreach (var kv in map)
        {
            var path = kv.Value?.ToString().Replace('\\', '/');
            if (string.IsNullOrEmpty(path)) continue;
            var i = path.IndexOf("Sandfall/", StringComparison.Ordinal);
            if (i < 0) continue;
            yield return (kv.Key, path[i..]);
        }
    }
}

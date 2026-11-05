using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;

using OodleDotNet;

namespace E33ModEditor.Core;

/// <summary>
/// Indexes the game's containers via CUE4Parse so assets can be located by
/// path (retoc's list command only exposes chunk ids, not file names).
/// </summary>
public sealed class GameIndex : IDisposable
{
    private DefaultFileProvider? _provider;

    public DefaultFileProvider Provider
    {
        get
        {
            if (_provider is not null) return _provider;
            var oodleName = OperatingSystem.IsLinux()
                ? OodleHelper.OODLE_NAME_LINUX
                : OodleHelper.OODLE_NAME_CURRENT;
            var oodlePath = Path.Combine(EditorPaths.ToolsDir, oodleName);
            if (!File.Exists(oodlePath))
                oodlePath = Path.Combine(AppContext.BaseDirectory, oodleName);
            if (!File.Exists(oodlePath))
                throw new FileNotFoundException(
                    $"Bundled Oodle runtime missing: {oodleName}. Reinstall the application.");
            OodleHelper.Initialize(new Oodle(oodlePath));

            _provider = new DefaultFileProvider(
                EditorPaths.PaksDir, SearchOption.TopDirectoryOnly,
                true, new VersionContainer(EGame.GAME_UE5_4));
            var usmap = EditorPaths.Mappings;
            if (!string.IsNullOrEmpty(usmap) && File.Exists(usmap))
                _provider.MappingsContainer = new FileUsmapTypeMappingsProvider(usmap);
            _provider.Initialize();
            _provider.SubmitKey(new FGuid(), new FAesKey("0x0000000000000000000000000000000000000000000000000000000000000000"));
            _provider.Mount();
            return _provider;
        }
    }

    /// <summary>All indexed asset paths.</summary>
    public IReadOnlyCollection<string> AllFiles => Provider.Files.Keys.ToList();

    /// <summary>Returns the container (.utoc) files that hold the given asset path substring.</summary>
    public IReadOnlyList<string> FindContainersFor(string assetNameSubstring)
    {
        var paths = Provider.Files.Keys
            .Where(k => k.Contains(assetNameSubstring, StringComparison.OrdinalIgnoreCase))
            .ToHashSet();

        var hits = new List<string>();
        foreach (var reader in Provider.MountedVfs)
        {
            foreach (var key in reader.Files.Keys)
            {
                if (paths.Contains(key)) { hits.Add(reader.Name); break; }
            }
        }
        return hits;
    }

    public void Dispose() => _provider?.Dispose();
}

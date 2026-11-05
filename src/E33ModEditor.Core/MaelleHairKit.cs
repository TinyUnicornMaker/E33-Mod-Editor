using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Maelle golden-blonde hair kit. Retints every red/auburn Maelle hair
/// MaterialInstance (MI_Hair_*Maelle*) from redhead to golden blonde by
/// rewriting the Color Down / Color Top / Highlights_Color vector parameters.
///
/// Discovery (2026-09-20): Maelle's hair color lives entirely in MI vector
/// params on parent MI_Hair_HighlightsVariation — red MI_Hair_Maelle_2 uses
/// Down (0.146, 0.022, 0.011), Top (0.109, 0.016, 0.009),
/// Highlights (0.490, 0.138, 0.074), while the white variant only differs in
/// those vectors (+ Brightness/gradient scalars). Textures are shared
/// (T_Hair_Maelle_V2_*), so no texture cook is needed.
///
/// Excluded on purpose: *White* variants + MI_Hair_Maelle_Acte3 (the later
/// pure-white story hair) stay untouched, and MI_Hair_Lune_DoubleBraided is
/// skipped because it is near-black shared stock also used by Lune.
/// Also excluded (proven shared via Face-BP import audit, 2026-09-20):
/// MI_Hair_Maelle_Var_Short1Sciel drives Sciel's Short face, and
/// MI_Hair_Maelle_Brown drives Sciel's DoubleBraid AND Ponytail faces —
/// Maelle's own Short keeps MI_Hair_Maelle_Var_Short (its mesh default).
/// Eyebrow/eyelash grooms are untouched (separate MetaHuman assets).
/// </summary>
public static class MaelleHairKit
{
    // NPC-Blond recipe, 2026-09-20: colors lifted from the "Charming Gustave
    // Blonde" reference mod, which repoints Gustave's Charming face at the
    // vanilla NPC wig MI_Hair_NPCs_Blond. Its vectors (linear):
    // Color Down (0.417, 0.266851, 0.152031),
    // Color Top  (0.130208, 0.067106, 0.021023).
    // Maelle's parent also needs Highlights_Color, which the NPC MI lacks
    // (its Highlight 1 Strength is 0) — derived as Down x 1.35 to keep the
    // highlight separation Maelle's shader needs. Brightness is set to 1 to
    // match the reference (vanilla red uses 2, both NPC Blond and Maelle's
    // own White variant use 1).
    public static readonly FLinearColor BlondeDown = new(0.417000f, 0.266851f, 0.152031f, 1);
    public static readonly FLinearColor BlondeTop = new(0.130208f, 0.067106f, 0.021023f, 1);
    public static readonly FLinearColor BlondeHighlights = new(0.562950f, 0.360249f, 0.205242f, 1);
    public const float BlondeBrightness = 1f;

    /// <summary>Game-relative MI stems (under Sandfall/Content) to retint.</summary>
    public static readonly IReadOnlyList<string> Targets = new[]
    {
        "Characters/Hair/Heroes/Maelle/MI_Hair_Maelle_2",
        "Characters/Hair/Heroes/Maelle/MI_Hair_Maelle_3",
        "Characters/Hair/Heroes/Maelle/MI_Hair_Maelle_DarkHair_2",
        "Characters/Hair/Heroes/Maelle/MI_Hair_Maelle_old",
        "Characters/Hair/Heroes/Lune/MI_Hair_Braid_Maelle",
        "Characters/Hair/Heroes/Lune/MI_Hairvar_Maelle_Rebellious",
        "Characters/Hair/Heroes/Sciel/MI_Hair_Maelle_Messybun",
        "Characters/Hair/Heroes/Z_Hair_Variation/Lune/MI_Hair_Maelle_Var_FrenchBob",
        "Characters/Hair/Heroes/Z_Hair_Variation/Lune/MI_Hairvar_Maelle_DoubleBun",
        "Characters/Hair/Heroes/Z_Hair_Variation/Lune/MI_Hairvar_Maelle_Esquie",
        "Characters/Hair/Heroes/Z_Hair_Variation/Lune/MI_Hairvar_Maelle_Osquio",
        "Characters/Hair/Heroes/Z_Hair_Variation/Maelle/MI_Hair_Maelle_DoubleBraided",
        "Characters/Hair/Heroes/Z_Hair_Variation/Maelle/MI_Hair_Maelle_MyFlower",
        "Characters/Hair/Heroes/Z_Hair_Variation/Maelle/MI_Hair_Maelle_Var_Short",
        "Characters/Hair/Heroes/Z_Hair_Variation/Sciel/Materials/MI_Hair_Maelle_Voluminous_",
        "Characters/Hair/Heroes/Z_Hair_Variation/Common/Materials/MI_Hair_Gestral_Maelle",
        "Characters/Hair/Mirror_Family/Clea/MI_Hair_Clea_Maelle",
        "Characters/Hair/NPCs/Julie/MI_Hair_Artist_s_One_Maelle",
    };

    private static readonly Dictionary<string, FLinearColor> ColorByParam = new(StringComparer.Ordinal)
    {
        ["Color Down"] = BlondeDown,
        ["Color Top"] = BlondeTop,
        ["Highlights_Color"] = BlondeHighlights,
    };

    private static UAsset Open(string p)
    {
        var map = DataTableEditor.Mappings;
        return map != null ? new UAsset(p, EngineVersion.VER_UE5_4, map) : new UAsset(p, EngineVersion.VER_UE5_4);
    }

    private static string? FindSource(string stem)
    {
        var file = stem + ".uasset";
        var repo = EditorPaths.RepoRoot;
        return FileCopy.FindUnder(Path.Combine(repo, "work", "hair"), Path.GetFileName(file))
            ?? FileCopy.FindUnder(Path.Combine(repo, "work", "tables"), Path.GetFileName(file))
            ?? FileCopy.FindUnder(Path.Combine(repo, "work", "extract_maelle"), Path.GetFileName(file))
            ?? FileCopy.FindUnder(Path.Combine(repo, "extracted"), Path.GetFileName(file))
            ?? FileCopy.FindTablesAsset(Path.GetFileName(file));
    }

    /// <summary>
    /// Stages every target MI into <paramref name="modDir"/> (preserving the
    /// Sandfall/Content tree) and retints its hair vectors to golden blonde.
    /// Returns the number of vector params rewritten.
    /// </summary>
    public static int Apply(string modDir)
    {
        int changes = 0;
        foreach (var stem in Targets)
        {
            var src = FindSource(stem)
                ?? throw new FileNotFoundException(stem + ".uasset (run: extract MI_Hair_Maelle --out work/hair)");
            var dest = Path.Combine(modDir, "Sandfall", "Content", stem.Replace('/', Path.DirectorySeparatorChar) + ".uasset");
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            FileCopy.AssetPair(src, dest);
            changes += Retint(dest);
        }
        return changes;
    }

    /// <summary>Rewrites hair vectors in one staged MI by parameter name (index-independent).</summary>
    public static int Retint(string uassetPath)
    {
        var asset = Open(uassetPath);
        var export = asset.Exports.OfType<NormalExport>().FirstOrDefault()
            ?? throw new InvalidDataException($"'{uassetPath}' has no editable export.");
        int changes = 0;
        foreach (var prop in export.Data)
        {
            if (prop is not ArrayPropertyData arr)
                continue;
            var arrName = DataTableEditor.CleanName(arr.Name);
            if (string.Equals(arrName, "VectorParameterValues", StringComparison.Ordinal))
            {
                foreach (var element in arr.Value)
                {
                    if (element is not StructPropertyData entry) continue;
                    var info = entry.Value.OfType<StructPropertyData>()
                        .FirstOrDefault(s => string.Equals(DataTableEditor.CleanName(s.Name), "ParameterInfo", StringComparison.Ordinal));
                    var nameProp = info?.Value.OfType<NamePropertyData>()
                        .FirstOrDefault(n => string.Equals(DataTableEditor.CleanName(n.Name), "Name", StringComparison.Ordinal));
                    var paramName = nameProp?.Value?.Value?.Value ?? "";
                    if (!ColorByParam.TryGetValue(paramName, out var color)) continue;
                    var valueStruct = entry.Value.OfType<StructPropertyData>()
                        .FirstOrDefault(s => string.Equals(DataTableEditor.CleanName(s.Name), "ParameterValue", StringComparison.Ordinal));
                    var colorProp = valueStruct?.Value.OfType<LinearColorPropertyData>()
                        .FirstOrDefault(c => string.Equals(DataTableEditor.CleanName(c.Name), "ParameterValue", StringComparison.Ordinal));
                    if (colorProp is null) continue;
                    colorProp.Value = color;
                    changes++;
                }
            }
            else if (string.Equals(arrName, "ScalarParameterValues", StringComparison.Ordinal))
            {
                foreach (var element in arr.Value)
                {
                    if (element is not StructPropertyData entry) continue;
                    var info = entry.Value.OfType<StructPropertyData>()
                        .FirstOrDefault(s => string.Equals(DataTableEditor.CleanName(s.Name), "ParameterInfo", StringComparison.Ordinal));
                    var nameProp = info?.Value.OfType<NamePropertyData>()
                        .FirstOrDefault(n => string.Equals(DataTableEditor.CleanName(n.Name), "Name", StringComparison.Ordinal));
                    var paramName = nameProp?.Value?.Value?.Value ?? "";
                    if (!string.Equals(paramName, "Brightness", StringComparison.Ordinal)) continue;
                    var floatProp = entry.Value.OfType<FloatPropertyData>()
                        .FirstOrDefault(f => string.Equals(DataTableEditor.CleanName(f.Name), "ParameterValue", StringComparison.Ordinal));
                    if (floatProp is null) continue;
                    floatProp.Value = BlondeBrightness;
                    changes++;
                }
            }
        }
        if (changes == 0)
            throw new InvalidDataException($"'{uassetPath}': no Color Down/Top/Highlights_Color vector params found.");
        asset.Write(uassetPath);
        return changes;
    }
}

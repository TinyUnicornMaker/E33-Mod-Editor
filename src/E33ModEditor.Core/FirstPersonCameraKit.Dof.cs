using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using E33ModEditor.Core.Packer;

namespace E33ModEditor.Core;

/// <summary>
/// Depth-of-field extension for the camera submodule — a separate, opt-in
/// feature (<see cref="FirstPersonCameraKit.Settings.DepthOfField"/>) that
/// gives the exploration camera a moderate–cinematic out-of-focus falloff in
/// both dungeons and the overworld.
///
/// Both pawns expose the gameplay lens as <c>Main_CineCamera_GEN_VARIABLE</c>
/// (the component <c>GetMainCamera</c> returns; the plain <c>MainCamera</c> is
/// inactive). Its <c>CurrentAperture</c> alone does NOT drive the renderer:
/// a cine camera only produces DoF through its <c>PostProcessSettings</c>
/// depth-of-field fields with the matching <c>bOverride_</c> flags. This
/// extension therefore writes a full DoF post-process block onto the lens
/// (Bokeh f-stop + focal distance, plus the Gaussian/Circle blur amount so it
/// works whichever method the project uses) and refocuses it on the subject.
/// </summary>
public static partial class FirstPersonCameraKit
{
    /// <summary>The pawns that carry the exploration lens.</summary>
    public static readonly string[] DofPawns =
    [
        "BP_jRPG_Character_World",
        "BP_jRPG_Character_WorldMap_pawn",
        "BP_WorldMapCharacter",
    ];

    /// <summary>
    /// The overworld lenses: the world-map game mode's pawn
    /// (<c>BP_jRPG_Character_WorldMap_pawn</c>) plus the second overworld
    /// character (<c>BP_WorldMapCharacter</c>, referenced by the world-map
    /// controller component). Both ship an extreme shallow-focus diorama lens
    /// (f/0.185 and f/0.13) and the second one is easy to miss.
    /// </summary>
    public static readonly string[] OverworldDofPawns =
    [
        "BP_jRPG_Character_WorldMap_pawn",
        "BP_WorldMapCharacter",
    ];

    public const string CineCameraExport = "Main_CineCamera_GEN_VARIABLE";

    /// <summary>Focal plane the extension focuses on, in cm.</summary>
    public static float DofFocus(Settings s) =>
        s.DofFocusDistance > 0f ? s.DofFocusDistance
        : s.ArmLength > 0f ? s.ArmLength
        : 250f;

    /// <summary>
    /// Writes the DoF extension to both exploration cameras. No-op unless
    /// <see cref="Settings.DepthOfField"/> is on. Returns the edit count.
    /// </summary>
    public static int StageDepthOfFieldExtension(string modDir, Settings s)
    {
        if (!s.DepthOfField) return 0;
        int n = 0;
        foreach (var pawn in DofPawns)
            n += PatchCineDof(modDir, pawn, s);
        return n;
    }

    /// <summary>
    /// Turns camera depth of field fully off on the overworld lenses. No-op
    /// while the DoF extension is on (that owns the lenses then). It clears the
    /// cameras' DoF override flags, so neither the baked diorama f-stop nor a
    /// runtime focus/aperture change can produce blur — this is what made the
    /// effect reappear in the overworld after a fight. The aperture is also
    /// parked at <see cref="Settings.WorldMapAperture"/> as a belt-and-braces
    /// fallback. Returns the edit count.
    /// </summary>
    public static int DisableOverworldDof(string modDir, Settings s)
    {
        if (s.DepthOfField) return 0;
        int n = 0;
        foreach (var pawn in OverworldDofPawns)
            n += ClearCineDof(modDir, pawn, s);
        return n;
    }

    private static int ClearCineDof(string modDir, string pawnStem, Settings s)
    {
        var dest = EnsureModCopy(pawnStem, modDir);
        if (dest is null) return 0;
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(dest, EngineVersion.VER_UE5_4, map)
            : new UAsset(dest, EngineVersion.VER_UE5_4);
        var cam = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString() == CineCameraExport);
        if (cam is null) return 0;
        int n = 0;
        if (s.WorldMapAperture > 0f
            && SetFloatProp(cam.Data, "CurrentAperture", s.WorldMapAperture)) n++;

        var pp = Find(cam.Data, "PostProcessSettings") as StructPropertyData;
        if (pp is null)
        {
            pp = new StructPropertyData(
                FName.FromString(asset, "PostProcessSettings"),
                FName.FromString(asset, "PostProcessSettings"));
            cam.Data.Add(pp);
            n++;
        }
        n += ClearDofOverrides(pp, asset);
        if (n > 0) asset.Write(dest);
        return n;
    }

    /// <summary>Clears every CineCamera depth-of-field override flag on a lens.</summary>
    private static int ClearDofOverrides(StructPropertyData pp, UAsset asset)
    {
        int n = 0;
        foreach (var name in new[]
        {
            "bOverride_DepthOfFieldFstop",
            "bOverride_DepthOfFieldMinFstop",
            "bOverride_DepthOfFieldFocalDistance",
            "bOverride_DepthOfFieldDepthBlurAmount",
            "bOverride_DepthOfFieldDepthBlurRadius",
        })
        {
            if (Find(pp.Value, name) is BoolPropertyData b)
            {
                if (b.Value) { b.Value = false; n++; }
            }
            else
            {
                pp.Value.Add(new BoolPropertyData(FName.FromString(asset, name)) { Value = false });
                n++;
            }
        }
        return n;
    }

    /// <summary>Reads a DoF override bool from a lens (false/absent → "0").</summary>
    public static string? ReadCineDofFlag(string uassetPath, string field)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
        var cam = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString() == CineCameraExport);
        if (cam is null) return null;
        var pp = Find(cam.Data, "PostProcessSettings") as StructPropertyData;
        if (pp is null) return "0";
        return Find(pp.Value, field) is BoolPropertyData b ? (b.Value ? "1" : "0") : "0";
    }

    private static int PatchCineDof(string modDir, string pawnStem, Settings s)
    {
        var dest = EnsureModCopy(pawnStem, modDir);
        if (dest is null) return 0;
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(dest, EngineVersion.VER_UE5_4, map)
            : new UAsset(dest, EngineVersion.VER_UE5_4);
        var cam = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString() == CineCameraExport);
        int n = 0;
        if (cam is not null)
        {
            float focus = DofFocus(s);
            if (SetFloatProp(cam.Data, "CurrentAperture", s.DofAperture)) n++;
            if (Find(cam.Data, "FocusSettings") is StructPropertyData fs
                && SetFloatProp(fs.Value, "ManualFocusDistance", focus)) n++;

            var pp = Find(cam.Data, "PostProcessSettings") as StructPropertyData;
            if (pp is null)
            {
                pp = new StructPropertyData(
                    FName.FromString(asset, "PostProcessSettings"),
                    FName.FromString(asset, "PostProcessSettings"));
                cam.Data.Add(pp);
            }
            n += WriteDofPostProcess(pp, asset, s, focus);
            n += 1; // count the camera block even when every field already matched
        }
        if (n > 0) asset.Write(dest);
        return n;
    }

    /// <summary>
    /// Writes the depth-of-field post-process fields (with override flags) into
    /// a camera's PostProcessSettings struct. Bokeh uses the f-stop; the
    /// Gaussian/Circle methods use the blur amount/radius — all are set so the
    /// extension works regardless of the project's DoF method.
    /// </summary>
    private static int WriteDofPostProcess(
        StructPropertyData pp, UAsset asset, Settings s, float focus)
    {
        int n = 0;
        void Flag(string name)
        {
            if (Find(pp.Value, name) is BoolPropertyData b)
            {
                if (!b.Value) { b.Value = true; n++; }
            }
            else
            {
                pp.Value.Add(new BoolPropertyData(FName.FromString(asset, name)) { Value = true });
                n++;
            }
        }
        void Float(string name, float value)
        {
            switch (Find(pp.Value, name))
            {
                case FloatPropertyData f when Math.Abs(f.Value - value) > 0.001f:
                    f.Value = value; n++; break;
                case DoublePropertyData d when Math.Abs(d.Value - value) > 0.001:
                    d.Value = value; n++; break;
                case null:
                    pp.Value.Add(new FloatPropertyData(FName.FromString(asset, name)) { Value = value });
                    n++; break;
            }
        }

        Flag("bOverride_DepthOfFieldFstop");
        Float("DepthOfFieldFstop", s.DofAperture);
        Flag("bOverride_DepthOfFieldMinFstop");
        Float("DepthOfFieldMinFstop", 1f);
        Flag("bOverride_DepthOfFieldFocalDistance");
        Float("DepthOfFieldFocalDistance", focus);
        Flag("bOverride_DepthOfFieldDepthBlurAmount");
        Float("DepthOfFieldDepthBlurAmount", s.DofBlurAmount);
        Flag("bOverride_DepthOfFieldDepthBlurRadius");
        Float("DepthOfFieldDepthBlurRadius", s.DofBlurRadius);
        return n;
    }

    /// <summary>Reads Main_CineCamera's CurrentAperture (f-stop) for smoke checks.</summary>
    public static string? ReadCineAperture(string uassetPath) =>
        ReadCineFloat(uassetPath, "CurrentAperture");

    /// <summary>Reads Main_CineCamera's manual focus distance for smoke checks.</summary>
    public static string? ReadCineFocus(string uassetPath) =>
        ReadCineFloat(uassetPath, "ManualFocusDistance");

    /// <summary>Reads a DoF field from Main_CineCamera's PostProcessSettings.</summary>
    public static string? ReadCineDof(string uassetPath, string field) =>
        ReadCineFloat(uassetPath, field);

    private static string? ReadCineFloat(string uassetPath, string prop)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
        var cam = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString() == CineCameraExport);
        if (cam is null) return null;
        List<PropertyData> list = cam.Data;
        if (prop == "ManualFocusDistance")
            list = (Find(cam.Data, "FocusSettings") as StructPropertyData)?.Value ?? new List<PropertyData>();
        else if (prop.StartsWith("DepthOfField", StringComparison.Ordinal))
            list = (Find(cam.Data, "PostProcessSettings") as StructPropertyData)?.Value ?? new List<PropertyData>();
        return Find(list, prop) switch
        {
            FloatPropertyData f => f.Value.ToString("0.###"),
            DoublePropertyData d => d.Value.ToString("0.###"),
            _ => null,
        };
    }
}

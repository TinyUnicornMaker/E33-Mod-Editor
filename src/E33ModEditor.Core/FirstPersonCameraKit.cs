using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using E33ModEditor.Core.Packer;

namespace E33ModEditor.Core;

/// <summary>
/// First-person exploration camera. Rewrites the data-driven camera configs
/// (DA_CameraConfig_*) so the third-person spring arm becomes a head-mounted
/// lens: zero arm length, pivot lifted to eye height, socket pushed slightly
/// forward so the near plane sits in front of the face instead of inside the
/// skull, lag off, FOV unified. Covers overworld + dungeons (Normal / Crouch /
/// MovingPlatform configs); FreeAim and Dialogue configs are left alone.
/// </summary>
public static partial class FirstPersonCameraKit
{
    public static readonly string[] Stems =
    [
        "DA_CameraConfig_Default",
        "DA_CameraConfig_Maelle",
        "DA_CameraConfig_Monoco",
        "DA_CameraConfig_Crouch_Default",
        "DA_CameraConfig_MovingPlatform_Default",
    ];

    /// <summary>Free-aim (shooting) configs: same head-mounted treatment so
    /// aiming stays in first person. FOV keeps its own default (80) to
    /// preserve the aim zoom.</summary>
    public static readonly string[] AimStems =
    [
        "DA_CameraConfig_FreeAim_Default",
        "DA_CameraConfig_FreeAim_Lune",
        "DA_CameraConfig_FreeAim_Maelle",
        "DA_CameraConfig_FreeAim_Monoco",
    ];

    public sealed class Settings
    {
        public bool Enabled { get; set; }
        public float ArmLength { get; set; } = 0f;
        public float EyeLift { get; set; } = 22f;
        /// <summary>
        /// Drops the pivot below eye height toward the chest. The ~30-degree
        /// native down-pitch clamp cannot be moved by a data mod, so framing
        /// the body inside the available window is done geometrically: a chest
        /// pivot plus forward offset puts the head above the frame when
        /// looking down and the torso/legs inside it.
        /// </summary>
        public float PivotDrop { get; set; } = 28f;
        public float ForwardOffset { get; set; } = 38f;
        /// <summary>
        /// Lateral lens shift for explore configs (+Y = character's right).
        /// 0 = centered (first-person); ~35 = over-the-right-shoulder.
        /// </summary>
        public float ExploreSideOffset { get; set; }
        /// <summary>
        /// Lateral lens shift for aim configs only (+Y = character's right).
        /// Vanilla aims from +30 (over the right shoulder, arm renders left of
        /// center); -30 mirrors it so the right arm renders right of center.
        /// Explore configs stay centered (0).
        /// </summary>
        public float AimSideOffset { get; set; } = -30f;
        /// <summary>Per-character aim overrides (fall back to the globals).</summary>
        public float MaelleAimSide { get; set; } = -40f;
        public float MaelleAimForward { get; set; } = 34f;
        public float MonocoAimForward { get; set; } = 58f;
        /// <summary>
        /// Extra pivot height for aim configs (added after EyeLift-PivotDrop).
        /// Aim poses raise the gun above the explore eye line; Monoco's mane
        /// also needs the lens up out of the fur. Lune's arm was framing high.
        /// </summary>
        public float MonocoAimLift { get; set; } = 20f;
        public float LuneAimLift { get; set; } = 14f;
        /// <summary>
        /// Sciel and Verso share vanilla aim configs (Lune's and Default's)
        /// yet need opposite corrections, so the kit clones dedicated DAs for
        /// them and rewires their DT_jRPG_WorldCharacters rows.
        /// </summary>
        public float ScielAimForward { get; set; } = 44f;
        public float ScielAimLift { get; set; } = 18f;
        public float VersoAimLift { get; set; } = -12f;
        /// <summary>
        /// When false (default), aim configs are left vanilla and only the
        /// explore/view camera is first-person. Enable to also convert aim.
        /// </summary>
        public bool AimFirstPerson { get; set; }
        /// <summary>Explore (Normal) forward offset for Monoco specifically.</summary>
        public float MonocoExploreForward { get; set; } = 64f;
        /// <summary>
        /// Explore boom for Monoco specifically (0 = <see cref="ArmLength"/>).
        /// His frame is the largest, so a longer boom keeps the body from
        /// swallowing the view. 2x the *visible* back distance means
        /// <c>MonocoExploreForward + 2 * (ArmLength - MonocoExploreForward)</c>.
        /// </summary>
        public float MonocoExploreArm { get; set; }
        /// <summary>
        /// Explore lateral offset for Monoco specifically
        /// (0 = <see cref="ExploreSideOffset"/>).
        /// </summary>
        public float MonocoExploreSide { get; set; }
        /// <summary>
        /// Explore forward offset for Verso specifically (long hair clips at
        /// the shared Default distance). Uses a dedicated DA_CameraConfig_Verso
        /// clone; see <see cref="CreatedExploreStems"/>.
        /// </summary>
        public float VersoExploreForward { get; set; } = 50f;
        public float FieldOfView { get; set; } = 90f;
        public float AimFieldOfView { get; set; } = 80f;
        public bool DisableLag { get; set; } = true;
        /// <summary>
        /// Boom length used while climbing. Vanilla zooms the boom out to
        /// 700cm on grab; 0 (default) keeps the explore framing instead, so
        /// the climb stays as zoomed in as normal play. Set a larger value
        /// for a controlled pull-back (e.g. 200).
        /// </summary>
        public float ClimbArmLength { get; set; }
        /// <summary>
        /// Multiplier applied to <see cref="ArmLength"/> for the climb boom
        /// when <see cref="ClimbArmLength"/> is 0. 1 keeps the explore framing
        /// (default), 2 doubles the boom while climbing. Ignored by the
        /// absolute <see cref="ClimbArmLength"/>.
        /// </summary>
        public float ClimbArmScale { get; set; } = 1f;
        /// <summary>
        /// Fallback f-stop for the overworld cine cameras
        /// (<c>BP_jRPG_Character_WorldMap_pawn</c> + <c>BP_WorldMapCharacter</c>,
        /// which ship a 21mm f/0.185–0.13 diorama lens). The overworld DoF is
        /// turned off outright by clearing the lens DoF overrides; this value
        /// is the secondary lever, parked at 22 (effectively off) so that even
        /// if a runtime path re-enables the override the blur stays gone.
        /// 0 leaves the aperture untouched. The dungeon camera runs f/120 and
        /// is left alone.
        /// </summary>
        public float WorldMapAperture { get; set; } = 22f;

        /// <summary>
        /// Depth-of-field extension (separate, opt-in). When on, all three
        /// exploration lenses — the dungeon/world pawn and the two overworld
        /// pawns — get <see cref="DofAperture"/> on their <c>Main_CineCamera</c>
        /// (the lens <c>GetMainCamera</c> returns), giving a moderate
        /// out-of-focus falloff in dungeons and the overworld alike. Off by
        /// default: the dungeon runs f/120 and is effectively sharp, and the
        /// overworld lenses are switched fully off by
        /// <see cref="DisableOverworldDof"/> (with
        /// <see cref="WorldMapAperture"/> as the fallback f-stop).
        /// See <see cref="StageDepthOfFieldExtension"/>.
        /// </summary>
        public bool DepthOfField { get; set; }
        /// <summary>
        /// f-stop the DoF extension writes to both exploration cameras.
        /// Lower = more blur (shallower focus); higher = less. ~4 is moderate,
        /// 2.8 stronger, 8+ nearly off. While on it supersedes
        /// <see cref="WorldMapAperture"/> so both cameras share one value.
        /// </summary>
        public float DofAperture { get; set; } = 2.45f;
        /// <summary>
        /// Gaussian/Circle DoF blur amount written alongside
        /// <see cref="DofAperture"/>. The cinematic (Bokeh) DoF method uses the
        /// f-stop; the Gaussian/Circle methods use this instead. Both are
        /// written so the extension works whichever method the project uses.
        /// </summary>
        public float DofBlurAmount { get; set; } = 19f;
        /// <summary>Gaussian/Circle DoF blur radius (see <see cref="DofBlurAmount"/>).</summary>
        public float DofBlurRadius { get; set; } = 3f;
        /// <summary>
        /// Focal plane for the DoF extension, in cm. 0 = auto (follows the
        /// boom: <see cref="ArmLength"/> + 150, so the subject stays sharp
        /// while the background falls off). Set an explicit distance to pin it.
        /// Only applied where the camera already carries <c>FocusSettings</c>
        /// (the dungeon/world camera).
        /// </summary>
        public float DofFocusDistance { get; set; }

        /// <summary>Boom to use while climbing: explicit value, or the explore arm scaled.</summary>
        public float ClimbArm => ClimbArmLength > 0f ? ClimbArmLength : ArmLength * ClimbArmScale;

        public static string SettingsPath =>
            Path.Combine(EditorPaths.RepoRoot, "work", "firstperson.json");

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (File.Exists(SettingsPath))
                    s.FromJson(JObject.Parse(File.ReadAllText(SettingsPath)));
            }
            catch { /* keep defaults */ }
            return s;
        }

        public void Save() =>
            File.WriteAllText(SettingsPath, ToJson().ToString(Newtonsoft.Json.Formatting.Indented));

        public JObject ToJson() => new()
        {
            ["Enabled"] = Enabled,
            ["ArmLength"] = ArmLength,
            ["EyeLift"] = EyeLift,
            ["PivotDrop"] = PivotDrop,
            ["ForwardOffset"] = ForwardOffset,
            ["ExploreSideOffset"] = ExploreSideOffset,
            ["AimSideOffset"] = AimSideOffset,
            ["MaelleAimSide"] = MaelleAimSide,
            ["MaelleAimForward"] = MaelleAimForward,
            ["MonocoAimForward"] = MonocoAimForward,
            ["MonocoAimLift"] = MonocoAimLift,
            ["LuneAimLift"] = LuneAimLift,
            ["ScielAimForward"] = ScielAimForward,
            ["ScielAimLift"] = ScielAimLift,
            ["VersoAimLift"] = VersoAimLift,
            ["AimFirstPerson"] = AimFirstPerson,
            ["MonocoExploreForward"] = MonocoExploreForward,
            ["MonocoExploreArm"] = MonocoExploreArm,
            ["MonocoExploreSide"] = MonocoExploreSide,
            ["VersoExploreForward"] = VersoExploreForward,
            ["FieldOfView"] = FieldOfView,
            ["AimFieldOfView"] = AimFieldOfView,
            ["DisableLag"] = DisableLag,
            ["ClimbArmLength"] = ClimbArmLength,
            ["ClimbArmScale"] = ClimbArmScale,
            ["WorldMapAperture"] = WorldMapAperture,
            ["DepthOfField"] = DepthOfField,
            ["DofAperture"] = DofAperture,
            ["DofBlurAmount"] = DofBlurAmount,
            ["DofBlurRadius"] = DofBlurRadius,
            ["DofFocusDistance"] = DofFocusDistance,
        };

        public void FromJson(JObject o)
        {
            if (o.Count == 0) return;
            Enabled = o.Value<bool?>("Enabled") ?? false;
            ArmLength = Math.Clamp(o.Value<float?>("ArmLength") ?? 0f, 0f, 400f);
            EyeLift = Math.Clamp(o.Value<float?>("EyeLift") ?? 22f, -40f, 60f);
            PivotDrop = Math.Clamp(o.Value<float?>("PivotDrop") ?? 28f, 0f, 80f);
            ForwardOffset = Math.Clamp(o.Value<float?>("ForwardOffset") ?? 38f, 0f, 80f);
            ExploreSideOffset = Math.Clamp(o.Value<float?>("ExploreSideOffset") ?? 0f, -60f, 60f);
            AimSideOffset = Math.Clamp(o.Value<float?>("AimSideOffset") ?? -30f, -60f, 60f);
            MaelleAimSide = Math.Clamp(o.Value<float?>("MaelleAimSide") ?? -40f, -60f, 60f);
            MaelleAimForward = Math.Clamp(o.Value<float?>("MaelleAimForward") ?? 34f, 0f, 60f);
            MonocoAimForward = Math.Clamp(o.Value<float?>("MonocoAimForward") ?? 58f, 0f, 80f);
            MonocoAimLift = Math.Clamp(o.Value<float?>("MonocoAimLift") ?? 20f, -40f, 60f);
            LuneAimLift = Math.Clamp(o.Value<float?>("LuneAimLift") ?? 14f, -40f, 60f);
            ScielAimForward = Math.Clamp(o.Value<float?>("ScielAimForward") ?? 44f, 0f, 80f);
            ScielAimLift = Math.Clamp(o.Value<float?>("ScielAimLift") ?? 18f, -40f, 60f);
            VersoAimLift = Math.Clamp(o.Value<float?>("VersoAimLift") ?? -12f, -40f, 60f);
            AimFirstPerson = o.Value<bool?>("AimFirstPerson") ?? false;
            MonocoExploreForward = Math.Clamp(o.Value<float?>("MonocoExploreForward") ?? 64f, 0f, 80f);
            MonocoExploreArm = Math.Clamp(o.Value<float?>("MonocoExploreArm") ?? 0f, 0f, 400f);
            MonocoExploreSide = Math.Clamp(o.Value<float?>("MonocoExploreSide") ?? 0f, -120f, 120f);
            VersoExploreForward = Math.Clamp(o.Value<float?>("VersoExploreForward") ?? 50f, 0f, 80f);
            FieldOfView = Math.Clamp(o.Value<float?>("FieldOfView") ?? 90f, 60f, 120f);
            AimFieldOfView = Math.Clamp(o.Value<float?>("AimFieldOfView") ?? 80f, 60f, 120f);
            DisableLag = o.Value<bool?>("DisableLag") ?? true;
            ClimbArmLength = Math.Clamp(o.Value<float?>("ClimbArmLength") ?? 0f, 0f, 400f);
            ClimbArmScale = Math.Clamp(o.Value<float?>("ClimbArmScale") ?? 1f, 0f, 4f);
            WorldMapAperture = Math.Clamp(o.Value<float?>("WorldMapAperture") ?? 22f, 0f, 135f);
            DepthOfField = o.Value<bool?>("DepthOfField") ?? false;
            DofAperture = Math.Clamp(o.Value<float?>("DofAperture") ?? 2.45f, 0.5f, 32f);
            DofBlurAmount = Math.Clamp(o.Value<float?>("DofBlurAmount") ?? 19f, 0f, 100f);
            DofBlurRadius = Math.Clamp(o.Value<float?>("DofBlurRadius") ?? 3f, 0f, 100f);
            DofFocusDistance = Math.Clamp(o.Value<float?>("DofFocusDistance") ?? 0f, 0f, 100000f);
        }
    }

    /// <summary>
    /// Aim configs cloned per character (donor vanilla stem). Sciel shares
    /// Lune's and Verso shares Default's vanilla, but need opposite fixes.
    /// </summary>
    public static readonly (string Stem, string Donor)[] CreatedAimStems =
    [
        ("DA_CameraConfig_FreeAim_Sciel", "DA_CameraConfig_FreeAim_Lune"),
        ("DA_CameraConfig_FreeAim_Verso", "DA_CameraConfig_FreeAim_Default"),
    ];

    public const string WorldCharactersTable = "DT_jRPG_WorldCharacters";

    /// <summary>
    /// Explore configs cloned per character (donor vanilla stem). Verso shares
    /// Default's explore config but his hair needs more forward clearance.
    /// </summary>
    public static readonly (string Stem, string Donor)[] CreatedExploreStems =
    [
        ("DA_CameraConfig_Verso", "DA_CameraConfig_Default"),
    ];

    /// <summary>Resolves (fov, forward, side, pivotLift, arm) for a config stem.</summary>
    public static (float Fov, float Forward, float Side, float PivotLift, float Arm) Resolve(string stem, Settings s)
    {
        // Aim configs are always head-mounted (arm 0) when the aim path is on;
        // the explore boom length never leaks into them.
        float arm = stem.Contains("FreeAim", StringComparison.Ordinal) ? 0f : s.ArmLength;
        if (string.Equals(stem, "DA_CameraConfig_Monoco", StringComparison.Ordinal))
        {
            // Monoco's frame is much larger; his own boom/side overrides keep
            // the body from swallowing the view (0 = inherit the globals).
            float monocoArm = s.MonocoExploreArm > 0f ? s.MonocoExploreArm : arm;
            float monocoSide = s.MonocoExploreSide != 0f ? s.MonocoExploreSide : s.ExploreSideOffset;
            return (s.FieldOfView, s.MonocoExploreForward, monocoSide, 0f, monocoArm);
        }
        if (string.Equals(stem, "DA_CameraConfig_Verso", StringComparison.Ordinal))
            return (s.FieldOfView, s.VersoExploreForward, s.ExploreSideOffset, 0f, arm);
        if (stem.Contains("Sciel", StringComparison.Ordinal))
            return (s.AimFieldOfView, s.ScielAimForward, s.AimSideOffset, s.ScielAimLift, arm);
        if (stem.Contains("Verso", StringComparison.Ordinal))
            return (s.AimFieldOfView, s.ForwardOffset, s.AimSideOffset, s.VersoAimLift, arm);
        if (!stem.StartsWith("DA_CameraConfig_FreeAim", StringComparison.Ordinal))
            return (s.FieldOfView, s.ForwardOffset, s.ExploreSideOffset, 0f, arm);
        if (stem.Contains("Maelle", StringComparison.Ordinal))
            return (s.AimFieldOfView, s.MaelleAimForward, s.MaelleAimSide, 0f, arm);
        if (stem.Contains("Monoco", StringComparison.Ordinal))
            return (s.AimFieldOfView, s.MonocoAimForward, s.AimSideOffset, s.MonocoAimLift, arm);
        if (stem.Contains("Lune", StringComparison.Ordinal))
            return (s.AimFieldOfView, s.ForwardOffset, s.AimSideOffset, s.LuneAimLift, arm);
        return (s.AimFieldOfView, s.ForwardOffset, s.AimSideOffset, 0f, arm);
    }

    /// <summary>Stages patched camera configs into modDir. Returns edit count.</summary>
    public static int Apply(string modDir, Settings s)
    {
        if (!s.Enabled) return 0;
        int n = 0;
        foreach (var stem in Stems)
        {
            var dest = EnsureModCopy(stem, modDir);
            if (dest is null) continue;
            var (fov, forward, side, lift, boom) = Resolve(stem, s);
            n += PatchFile(dest, s, fov, forward, side, lift, boom);
        }
        foreach (var (stem, donor) in CreatedExploreStems)
        {
            var dest = EnsureCreatedCopy(stem, donor, modDir);
            if (dest is null) continue;
            var (fov, forward, side, lift, boom) = Resolve(stem, s);
            n += PatchFile(dest, s, fov, forward, side, lift, boom);
        }
        n += StageTraversalFix(modDir, s);
        n += StageClimbZoom(modDir, s);
        n += DisableOverworldDof(modDir, s);
        n += StageDepthOfFieldExtension(modDir, s);
        if (!s.AimFirstPerson)
        {
            n += RewireWorldCharacters(modDir, s);
            return n;
        }
        foreach (var stem in AimStems)
        {
            var dest = EnsureModCopy(stem, modDir);
            if (dest is null) continue;
            var (fov, forward, side, lift, boom) = Resolve(stem, s);
            n += PatchFile(dest, s, fov, forward, side, lift, boom);
        }
        foreach (var (stem, donor) in CreatedAimStems)
        {
            var dest = EnsureCreatedCopy(stem, donor, modDir);
            if (dest is null) continue;
            var (fov, forward, side, lift, boom) = Resolve(stem, s);
            n += PatchFile(dest, s, fov, forward, side, lift, boom);
        }
        n += RewireWorldCharacters(modDir, s);
        return n;
    }

    /// <summary>
    /// Points DT_jRPG_WorldCharacters rows at the cloned DAs (hard
    /// ObjectPropertyData refs: package + class imports are added).
    /// Verso's explore row always rewires; the aim rows only when aim FP is on.
    /// </summary>
    private static int RewireWorldCharacters(string modDir, Settings s)
    {
        var dest = EnsureModCopy(WorldCharactersTable, modDir);
        if (dest is null) return 0;
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(dest, EngineVersion.VER_UE5_4, map)
            : new UAsset(dest, EngineVersion.VER_UE5_4);
        var table = asset.Exports.OfType<DataTableExport>().FirstOrDefault()
            ?? throw new InvalidDataException($"'{dest}' has no DataTable export.");
        var rewires = new List<(string Row, string Field, string Stem)>
        {
            ("Verso", "CameraConfig", "DA_CameraConfig_Verso"),
        };
        if (s.AimFirstPerson)
        {
            rewires.Add(("Sciel", "FreeAimCameraConfig", "DA_CameraConfig_FreeAim_Sciel"));
            rewires.Add(("Verso", "FreeAimCameraConfig", "DA_CameraConfig_FreeAim_Verso"));
        }
        int n = 0;
        foreach (var (rowName, fieldName, stem) in rewires)
        {
            var row = table.Table.Data.FirstOrDefault(r =>
                DataTableEditor.CleanName(r.Name) == rowName);
            var field = row?.Value.FirstOrDefault(p =>
                DataTableEditor.CleanName(p.Name) == fieldName) as ObjectPropertyData;
            if (row is null || field is null) continue;
            var folder = fieldName == "CameraConfig" ? "Data/Normal" : "Data/FreeAim";
            var pkgIdx = asset.AddImport(new Import("/Script/CoreUObject", "Package",
                FPackageIndex.FromRawIndex(0),
                "/Game/Gameplay/Camera/Configs/" + folder + "/" + stem, false, asset));
            var objIdx = asset.AddImport(new Import(
                "/Game/Gameplay/Camera/Configs/BP_DataAsset_CameraConfig",
                "BP_DataAsset_CameraConfig_C", pkgIdx, stem, false, asset));
            field.Value = objIdx;
            n++;
        }
        if (n > 0) asset.Write(dest);
        return n;
    }

    /// <summary>
    /// Traversal-restore hardening (climb pulls the boom to 700; the post
    /// traversal state must come back to the modded view, not a stale
    /// default). Bakes the preset boom + socket into the SCS template of
    /// SmartSpringArm_MainCamera — the arm every camera system actually
    /// drives (<c>GetSpringArmMainCamera</c>, the DA blender,
    /// <c>SpringArmControl</c>/<c>SpringArmReset</c>, the cache snapshot).
    /// The world-map pawn overrides that template with its own 1500cm boom
    /// and (0,300,142) socket, so it gets the same treatment. Pure data, and
    /// the DA blender overwrites these at runtime so normal play is
    /// unaffected.
    /// (CDO Cache defaults were tried and dropped: added CDO props corrupt
    /// the pawn's CDO export on re-parse, and the Save snapshot they would
    /// seed is equidistant to stale either way.)
    /// </summary>
    public static int StageTraversalFix(string modDir, Settings s)
    {
        int n = 0;
        n += StagePawnCameraArm(modDir, "BP_jRPG_Character_World", s,
            patchSnapshot: true, revertPivotArm: true);
        n += StagePawnCameraArm(modDir, "BP_jRPG_Character_WorldMap_pawn", s,
            patchSnapshot: false, revertPivotArm: false);
        return n;
    }

    private static int StagePawnCameraArm(string modDir, string pawnStem,
        Settings s, bool patchSnapshot, bool revertPivotArm)
    {
        var dest = EnsureModCopy(pawnStem, modDir);
        if (dest is null) return 0;
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(dest, EngineVersion.VER_UE5_4, map)
            : new UAsset(dest, EngineVersion.VER_UE5_4);
        int n = 0;

        // Main-camera boom: the exploration camera hangs off
        // SmartSpringArm_MainCamera, not CharacterSpringArm (a crouch pivot).
        var armTpl = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString() == "SmartSpringArm_MainCamera_GEN_VARIABLE");
        if (armTpl != null)
        {
            if (SetFloatProp(armTpl.Data, "TargetArmLength", s.ArmLength)) n++;
            var sock = FindVectorDeep(armTpl.Data, "SocketOffset");
            if (sock != null)
            {
                var want = new FVector(s.ForwardOffset, s.ExploreSideOffset, 0);
                if (!VecEquals(sock.Value, want)) { sock.Value = want; n++; }
            }
            else
            {
                armTpl.Data.Add(new VectorPropertyData(FName.FromString(asset, "SocketOffset"))
                {
                    Value = new FVector(s.ForwardOffset, s.ExploreSideOffset, 0),
                });
                n++;
            }
            if (Find(armTpl.Data, "bEnableCameraLag") is BoolPropertyData lag
                && lag.Value != !s.DisableLag)
            {
                lag.Value = !s.DisableLag;
                n++;
            }
        }

        if (revertPivotArm)
        {
            // Earlier builds baked the preset into CharacterSpringArm (the
            // crouch pivot); vanilla keeps it at arm 0 with no socket. Undo
            // that on already-staged copies so the pivot keeps its role.
            var pivot = asset.Exports.OfType<NormalExport>()
                .FirstOrDefault(e => e.ObjectName.ToString() == "CharacterSpringArm_GEN_VARIABLE");
            if (pivot != null)
            {
                if (SetFloatProp(pivot.Data, "TargetArmLength", 0f)) n++;
                int removed = pivot.Data.RemoveAll(p =>
                    DataTableEditor.CleanName(p.Name) == "SocketOffset");
                if (removed > 0) n++;
            }
        }

        if (patchSnapshot && PatchSaveSnapshot(asset, s, out int snapChanged)) n += snapChanged;

        if (n > 0) asset.Write(dest);
        return n;
    }

    private static bool VecEquals(FVector a, FVector b) =>
        Math.Abs(a.X - b.X) < 0.001 && Math.Abs(a.Y - b.Y) < 0.001 && Math.Abs(a.Z - b.Z) < 0.001;

    /// <summary>
    /// Keeps the climb camera zoomed in. On grab, BP_ClimbingComponent hands
    /// its <c>SpringArmTargetArmLength</c> (vanilla 700) to the pawn's
    /// <c>SpringArmControl</c>, which is what yanks the boom far out. The
    /// pawn's component template carries no override, so patching the
    /// component class default is enough. <see cref="Settings.ClimbArm"/>
    /// resolves 0 to the explore arm (same framing as normal play).
    /// </summary>
    public static int StageClimbZoom(string modDir, Settings s)
    {
        var dest = EnsureModCopy("BP_ClimbingComponent", modDir);
        if (dest is null) return 0;
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(dest, EngineVersion.VER_UE5_4, map)
            : new UAsset(dest, EngineVersion.VER_UE5_4);
        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString()
                .StartsWith("Default__", StringComparison.Ordinal));
        int n = 0;
        if (cdo is not null && SetFloatProp(cdo.Data, "SpringArmTargetArmLength", s.ClimbArm)) n++;
        if (n > 0) asset.Write(dest);
        return n;
    }

    /// <summary>Reads BP_ClimbingComponent's climb boom for smoke checks.</summary>
    public static string? ReadClimbArm(string uassetPath)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString()
                .StartsWith("Default__", StringComparison.Ordinal));
        if (cdo is null) return null;
        return Find(cdo.Data, "SpringArmTargetArmLength") switch
        {
            FloatPropertyData f => f.Value.ToString("0.###"),
            DoublePropertyData d => d.Value.ToString("0.###"),
            _ => null,
        };
    }

    /// <summary>
    /// Reads the world-map cine camera f-stop for smoke checks.
    /// (The overworld DoF itself is disabled in
    /// <see cref="DisableOverworldDof"/>.)
    /// </summary>
    public static string? ReadWorldMapAperture(string uassetPath)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
        var cam = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString() == "Main_CineCamera_GEN_VARIABLE");
        if (cam is null) return null;
        return Find(cam.Data, "CurrentAperture") switch
        {
            FloatPropertyData f => f.Value.ToString("0.###"),
            DoublePropertyData d => d.Value.ToString("0.###"),
            _ => null,
        };
    }

    /// <summary>
    /// Points the traversal-restore caches at preset-correct constants.
    /// SaveCamValuesInCache snapshots LIVE components at BeginPlay, which
    /// predates the DA blend — so every downstream restore (reset timeline,
    /// full restore) lands on stale SCS values instead of the modded view.
    /// Rewrites the snapshot sources to literals; location/speed snapshots
    /// are left untouched. Size changes are safe: calls resolve by name and
    /// no absolute offsets target this body (verified via uberjumps), and the
    /// body is straight-line (asserted — jumps would need ShiftAbsolute).
    /// Returns false when the body is not as expected (caller skips).
    /// </summary>
    private static bool PatchSaveSnapshot(UAsset asset, Settings s, out int changed)
    {
        changed = 0;
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "SaveCamValuesInCache");
        if (fn?.ScriptBytecode is null) return false;
        if (MayhemFixKit.WalkHas(fn.ScriptBytecode, e =>
                e is UAssetAPI.Kismet.Bytecode.Expressions.EX_Jump
                or UAssetAPI.Kismet.Bytecode.Expressions.EX_JumpIfNot
                or UAssetAPI.Kismet.Bytecode.Expressions.EX_PushExecutionFlow
                or UAssetAPI.Kismet.Bytecode.Expressions.EX_SwitchValue))
            return false;
        var targets = new Dictionary<string, Func<UAssetAPI.Kismet.Bytecode.KismetExpression>>(StringComparer.Ordinal)
        {
            ["TargetArmLengthCache"] = () => new UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst { Value = s.ArmLength },
            ["CameraLagMaxDistanceCache"] = () => new UAssetAPI.Kismet.Bytecode.Expressions.EX_FloatConst { Value = 70f },
            ["CameraLagSpeedCache"] = () => new UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst { Value = 2.5 },
            ["FieldOfViewCache"] = () => new UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst { Value = s.FieldOfView },
            ["SocketOffsetCache"] = () => new UAssetAPI.Kismet.Bytecode.Expressions.EX_VectorConst
                { Value = new UAssetAPI.UnrealTypes.FVector(s.ForwardOffset, s.ExploreSideOffset, 0) },
            ["TargetOffsetCache"] = () => new UAssetAPI.Kismet.Bytecode.Expressions.EX_VectorConst
                { Value = new UAssetAPI.UnrealTypes.FVector(0, 0, 0) },
        };
        foreach (var stmt in fn.ScriptBytecode)
        {
            if (stmt is not UAssetAPI.Kismet.Bytecode.Expressions.EX_Let let) continue;
            var destName = DestName(let);
            if (destName is null) continue;
            foreach (var (member, makeConst) in targets)
            {
                if (destName.Contains(member, StringComparison.Ordinal))
                {
                    let.Expression = makeConst();
                    changed++;
                    break;
                }
            }
        }
        if (changed == 0) return false;
        MayhemFixKit.RecalcContextSkips(asset, fn);
        return true;
    }

    private static string? DestName(UAssetAPI.Kismet.Bytecode.Expressions.EX_Let let)
    {
        if (let.Variable is UAssetAPI.Kismet.Bytecode.Expressions.EX_LocalVariable lv)
            return lv.Variable?.New?.Path?.FirstOrDefault()?.ToString() ?? lv.Variable?.ToString();
        if (let.Variable is UAssetAPI.Kismet.Bytecode.Expressions.EX_InstanceVariable iv)
            return iv.Variable?.New?.Path?.FirstOrDefault()?.ToString() ?? iv.Variable?.ToString();
        return let.Value?.New?.Path?.FirstOrDefault()?.ToString() ?? let.Value?.ToString();
    }

    private static bool SetFloatProp(List<PropertyData> list, string cleanName, float value)
    {
        switch (Find(list, cleanName))
        {
            case FloatPropertyData f:
                if (Math.Abs(f.Value - value) > 0.001f) { f.Value = value; return true; }
                return false;
            case DoublePropertyData d:
                if (Math.Abs(d.Value - value) > 0.001) { d.Value = value; return true; }
                return false;
            default:
                return false;
        }
    }

    private static VectorPropertyData? FindVectorDeep(List<PropertyData> list, string cleanName)
    {
        foreach (var p in list)
        {
            if (DataTableEditor.CleanName(p.Name) == cleanName && FindVector(p) is { } v) return v;
            if (p is StructPropertyData st)
            {
                var r = FindVectorDeep(st.Value, cleanName);
                if (r is not null) return r;
            }
        }
        return null;
    }

    /// <summary>
    /// Reads back the traversal-fix state of a staged/vanilla pawn for smoke
    /// checks: the SmartSpringArm_MainCamera SCS template (the arm the
    /// exploration camera actually rides) plus the crouch pivot arm.
    /// </summary>
    public static Dictionary<string, string> ReadPawnCameraState(string uassetPath)
    {
        var out_ = new Dictionary<string, string>();
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);

        void ReadArm(string templateName, string armKey, string sockKey)
        {
            var tpl = asset.Exports.OfType<NormalExport>()
                .FirstOrDefault(e => e.ObjectName.ToString() == templateName);
            if (tpl is null) return;
            if (Find(tpl.Data, "TargetArmLength") is FloatPropertyData a)
                out_[armKey] = a.Value.ToString("0.###");
            if (Find(tpl.Data, "TargetArmLength") is DoublePropertyData d)
                out_[armKey] = d.Value.ToString("0.###");
            var sock = FindVectorDeep(tpl.Data, "SocketOffset");
            if (sock is not null) out_[sockKey] = Vec(sock);
        }

        ReadArm("SmartSpringArm_MainCamera_GEN_VARIABLE", "ScsArm", "ScsSocket");
        ReadArm("CharacterSpringArm_GEN_VARIABLE", "PivotArm", "PivotSocket");
        return out_;
    }

    /// <summary>
    /// Lists scalar/vector constants inside SaveCamValuesInCache, for smoke
    /// checks that the snapshot patch landed.
    /// </summary>
    public static List<string> SaveSnapshotConsts(string uassetPath)
    {
        var found = new List<string>();
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "SaveCamValuesInCache");
        if (fn?.ScriptBytecode is null) return found;
        MayhemFixKit.Walk(fn.ScriptBytecode, e =>
        {
            switch (e)
            {
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst d:
                    found.Add("double:" + d.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_FloatConst f:
                    found.Add("float:" + f.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case UAssetAPI.Kismet.Bytecode.Expressions.EX_VectorConst v:
                    found.Add($"vector:({v.Value.X:0.###}, {v.Value.Y:0.###}, {v.Value.Z:0.###})");
                    break;
            }
        });
        return found;
    }

    /// <summary>True when SaveCamValuesInCache still assigns a member (i.e.
    /// that snapshot statement was left untouched by the patch).</summary>
    public static bool SaveSnapshotMentions(string uassetPath, string memberSubstring)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "SaveCamValuesInCache");
        if (fn?.ScriptBytecode is null) return false;
        foreach (var stmt in fn.ScriptBytecode)
            if (stmt is UAssetAPI.Kismet.Bytecode.Expressions.EX_Let let
                && DestName(let) is string d
                && d.Contains(memberSubstring, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static string? EnsureCreatedCopy(string stem, string donorStem, string modDir)
    {
        var existing = FileCopy.FindUnder(modDir, stem + ".uasset");
        if (existing is not null) return existing;
        var src = FileCopy.FindTablesAsset(donorStem + ".uasset");
        if (src is null) return null;
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return null;
        var relDir = Path.GetDirectoryName(src[idx..]) ?? "";
        var dest = Path.Combine(modDir, relDir, stem + ".uasset");
        FileCopy.AssetPair(src, dest);
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(dest, EngineVersion.VER_UE5_4, map)
            : new UAsset(dest, EngineVersion.VER_UE5_4);
        var full = dest.Replace('\\', '/');
        var ci = full.IndexOf("/Content/", StringComparison.OrdinalIgnoreCase);
        var newPackage = "/Game/" + full[(ci + "/Content/".Length)..^".uasset".Length];
        var oldPackage = asset.FolderName?.Value;
        asset.FolderName = FString.FromString(newPackage);
        var names = asset.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var nm = names[i]?.Value;
            if (string.IsNullOrEmpty(nm)) continue;
            var next = nm;
            if (!string.IsNullOrEmpty(oldPackage))
                next = next.Replace(oldPackage, newPackage, StringComparison.Ordinal);
            if (!next.Contains(stem, StringComparison.Ordinal))
                next = next.Replace(donorStem, stem, StringComparison.Ordinal);
            if (next != nm) asset.SetNameReference(i, FString.FromString(next));
        }
        foreach (var exp in asset.Exports.OfType<NormalExport>())
            if (exp.ObjectName.ToString() == donorStem)
                exp.ObjectName = new FName(asset, stem);
        asset.Write(dest);
        return dest;
    }

    /// <summary>Reads back staged/vanilla values for smoke checks.</summary>
    public static Dictionary<string, string> ReadValues(string uassetPath)
    {
        var out_ = new Dictionary<string, string>();
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
        var exp = asset.Exports.OfType<NormalExport>().FirstOrDefault();
        if (exp is null) return out_;
        var cam = Find(exp.Data, "CameraConfig") as StructPropertyData;
        var arm = cam is null ? null : Find(cam.Value, "SpringArmComponentConfig") as StructPropertyData;
        var lens = cam is null ? null : Find(cam.Value, "CameraComponentConfig") as StructPropertyData;
        if (arm != null)
        {
            var a = GetFloat(arm.Value, "TargetArmLength");
            if (a is not null) out_["TargetArmLength"] = a.Value.ToString("0.###");
            if (Find(arm.Value, "EnableCameraLag") is BoolPropertyData l)
                out_["EnableCameraLag"] = l.Value.ToString();
            var rel = arm.Value.Select(p => FindVector(p)).FirstOrDefault(v => v is not null);
            var sock = Find(arm.Value, "SocketOffset");
            var rv = sock is null ? null : FindVector(sock);
            if (rel is not null) out_["RelativeLocation"] = Vec(rel);
            if (rv is not null) out_["SocketOffset"] = Vec(rv);
        }
        if (lens != null)
        {
            var f = GetFloat(lens.Value, "FieldOfView");
            if (f is not null) out_["FieldOfView"] = f.Value.ToString("0.###");
        }
        return out_;
    }

    private static float? GetFloat(List<PropertyData> list, string cleanName) =>
        Find(list, cleanName) switch
        {
            FloatPropertyData f => f.Value,
            DoublePropertyData d => (float)d.Value,
            _ => null,
        };

    /// <summary>
    /// Resolves a DT_jRPG_WorldCharacters row's camera config import to its
    /// object name (the DA stem), for smoke checks.
    /// </summary>
    public static string? ReadWorldCharacterAim(string uassetPath, string rowName, string fieldName = "FreeAimCameraConfig")
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
        var table = asset.Exports.OfType<DataTableExport>().FirstOrDefault();
        var row = table?.Table.Data.FirstOrDefault(r =>
            DataTableEditor.CleanName(r.Name) == rowName);
        var field = row?.Value.FirstOrDefault(p =>
            DataTableEditor.CleanName(p.Name) == fieldName) as ObjectPropertyData;
        if (field?.Value is null) return null;
        try { return field.Value.ToImport(asset).ObjectName.ToString(); }
        catch { return null; }
    }

    private static int PatchFile(string dest, Settings s, float fov, float forward, float sideY, float pivotLift, float boom)
    {
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(dest, EngineVersion.VER_UE5_4, map)
            : new UAsset(dest, EngineVersion.VER_UE5_4);
        var exp = asset.Exports.OfType<NormalExport>().FirstOrDefault()
            ?? throw new InvalidDataException($"'{dest}' has no editable export.");
        var cam = Find(exp.Data, "CameraConfig") as StructPropertyData
            ?? throw new KeyNotFoundException($"'{dest}' has no CameraConfig struct.");
        var arm = Find(cam.Value, "SpringArmComponentConfig") as StructPropertyData
            ?? throw new KeyNotFoundException($"'{dest}' has no SpringArmComponentConfig struct.");
        var lens = Find(cam.Value, "CameraComponentConfig") as StructPropertyData
            ?? throw new KeyNotFoundException($"'{dest}' has no CameraComponentConfig struct.");

        int n = 0;
        if (SetFloat(arm.Value, "TargetArmLength", boom)) n++;

        var relProp = Find(arm.Value, "RelativeLocation");
        if (relProp != null)
        {
            var v = FindVector(relProp);
            if (v != null)
            {
                v.Value = new FVector(v.Value.X, v.Value.Y, v.Value.Z + s.EyeLift - s.PivotDrop + pivotLift);
                n++;
            }
        }

        var sockProp = Find(arm.Value, "SocketOffset");
        if (sockProp != null && SetVector(sockProp, forward, sideY, 0)) n++;

        if (s.DisableLag && Find(arm.Value, "EnableCameraLag") is BoolPropertyData lag && lag.Value)
        {
            lag.Value = false;
            n++;
        }

        if (SetFloat(lens.Value, "FieldOfView", fov)) n++;

        // Aim configs (Lune/Maelle) carry a lens offset for the over-shoulder
        // framing; zero it so the sight line stays on the pivot axis.
        var lensOff = Find(lens.Value, "RelativeLocation");
        if (lensOff != null && SetVector(lensOff, 0, 0, 0)) n++;

        if (n > 0) asset.Write(dest);
        return n;
    }

    private static string? EnsureModCopy(string stem, string modDir)
    {
        var existing = FileCopy.FindUnder(modDir, stem + ".uasset");
        if (existing is not null) return existing;
        var src = FileCopy.FindTablesAsset(stem + ".uasset");
        if (src is null)
        {
            EnsureDonors();
            src = FileCopy.FindTablesAsset(stem + ".uasset");
        }
        if (src is null) return null;
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return null;
        var dest = Path.Combine(modDir, src[idx..]);
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    private static void EnsureDonors()
    {
        using (DonorExtract.SuspendMods())
        {
            var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "camera_fp");
            Directory.CreateDirectory(dir);
            new Retoc().ToLegacy(dir, "DA_CameraConfig");
            // The standalone camera also ships the pawns (climb restore +
            // world-map override) and the climb component (climb boom).
            // "BP_jRPG_Character_World" is an unanchored substring, so it also
            // pulls BP_jRPG_Character_WorldMap_pawn.
            new Retoc().ToLegacy(dir, "BP_jRPG_Character_World");
            new Retoc().ToLegacy(dir, "BP_WorldMapCharacter");
            new Retoc().ToLegacy(dir, "BP_ClimbingComponent");
        }
    }

    private static PropertyData? Find(List<PropertyData> list, string cleanName) =>
        list.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == cleanName);

    private static bool SetFloat(List<PropertyData> list, string cleanName, float value)
    {
        switch (Find(list, cleanName))
        {
            case FloatPropertyData f when Math.Abs(f.Value - value) > 0.001f:
                f.Value = value;
                return true;
            case DoublePropertyData d when Math.Abs(d.Value - value) > 0.001:
                d.Value = value;
                return true;
            default:
                return false;
        }
    }

    private static VectorPropertyData? FindVector(PropertyData prop)
    {
        if (prop is VectorPropertyData v) return v;
        if (prop is StructPropertyData st)
            foreach (var c in st.Value)
            {
                var r = FindVector(c);
                if (r is not null) return r;
            }
        return null;
    }

    private static bool SetVector(PropertyData prop, double x, double y, double z)
    {
        var v = FindVector(prop);
        if (v is null) return false;
        v.Value = new FVector(x, y, z);
        return true;
    }

    private static string Vec(VectorPropertyData v) =>
        $"({v.Value.X:0.###}, {v.Value.Y:0.###}, {v.Value.Z:0.###})";
}

using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Raises Might weapon scaling from vanilla's fixed 55 to a fixed 192.
///
/// <para>Vanilla <c>BP_CharacterData.GetWeaponScalingPotencyMultiplier</c> contains
/// <c>Attribute == 2</c> (<c>ECharacterAttribute</c> index 2 = <c>NewEnumerator1</c>,
/// Might). Its true branch uses a hard-coded <c>55</c> instead of the grade factor,
/// so at S (110) Might is exactly half. The first fix removed the branch; that only
/// made Might equal to S other stats (about +1% because of the flat Strength table).</para>
///
/// <para>This kit keeps the Might branch but raises the constant to <c>192</c>
/// (about 1.75× S's 110). With the measured 55→60k / 110→90k curve, that lands
/// around 135k versus 90k — roughly +50% total Attack at 333/S.</para>
///
/// <para>Both edits are same-size constants, so no bytecode offsets move. This is a
/// deliberate balance change, not a vanilla bug fix.</para>
/// </summary>
public static class MightScalingKit
{
    public const string AssetStem = "BP_CharacterData";
    public const string FunctionName = "GetWeaponScalingPotencyMultiplier";
    public const byte MightAttribute = 2;       // ECharacterAttribute::NewEnumerator1
    public const double VanillaMightFactor = 55;
    public const double MightFactor = 192;      // ~1.75 × S (110); ~+50% total at 333
    private const byte PreviousDisabled = 255;

    public static int Apply(string modDir)
    {
        var path = FindStaged(modDir) ?? Stage(modDir);
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);

        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == FunctionName)
            ?? throw new InvalidDataException($"[might-scaling] {AssetStem}.{FunctionName} not found");
        if (fn.ScriptBytecode is null)
            throw new InvalidDataException($"[might-scaling] {FunctionName} has no bytecode");

        var bytes = new List<EX_ByteConst>();
        var doubles = new List<EX_DoubleConst>();
        foreach (var stmt in fn.ScriptBytecode)
            MayhemFixKit.Walk(new[] { stmt }, e =>
            {
                if (e is EX_ByteConst bc) bytes.Add(bc);
                if (e is EX_DoubleConst dc) doubles.Add(dc);
            });

        // Restore the Might comparison if the earlier "disable branch" build is present.
        var mightBytes = bytes.Where(b => b.Value == MightAttribute).ToList();
        var disabledBytes = bytes.Where(b => b.Value == PreviousDisabled).ToList();
        if (mightBytes.Count == 0 && disabledBytes.Count == 1)
        {
            disabledBytes[0].Value = MightAttribute;
            mightBytes.Add(disabledBytes[0]);
        }
        if (mightBytes.Count != 1)
            throw new InvalidDataException($"[might-scaling] expected one Might ByteConst, found {mightBytes.Count}");

        var factor = doubles.Where(d => d.Value == VanillaMightFactor || d.Value == MightFactor).ToList();
        if (factor.Count != 1)
            throw new InvalidDataException(
                $"[might-scaling] expected one Might factor ({VanillaMightFactor} or {MightFactor}), found {factor.Count}");

        bool changed = factor[0].Value != MightFactor;
        if (changed)
        {
            factor[0].Value = MightFactor;
            asset.Write(path);
        }

        Console.WriteLine(changed
            ? $"[might-scaling] Might factor {VanillaMightFactor} → {MightFactor} (grade branch kept)"
            : "[might-scaling] already applied");
        return changed ? 1 : 0;
    }

    public static void Verify(string modDir)
    {
        var path = FindStaged(modDir)
            ?? throw new FileNotFoundException("[might-scaling] staged BP_CharacterData missing");
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == FunctionName)
            ?? throw new InvalidDataException("[might-scaling] function missing");
        int might = 0, factor = 0, previous = 0;
        foreach (var stmt in fn.ScriptBytecode ?? Array.Empty<KismetExpression>())
            MayhemFixKit.Walk(new[] { stmt }, e =>
            {
                if (e is EX_ByteConst bc && bc.Value == MightAttribute) might++;
                if (e is EX_DoubleConst dc && dc.Value == MightFactor) factor++;
                if (e is EX_ByteConst bc2 && bc2.Value == PreviousDisabled) previous++;
            });
        if (might != 1 || factor != 1 || previous != 0)
            throw new InvalidDataException(
                $"[might-scaling] verify failed: Might={might}, factor={factor}, disabled={previous}");
        Console.WriteLine($"[might-scaling] Verify passed: Might factor {MightFactor}");
    }

    private static string? FindStaged(string modDir) =>
        FileCopy.FindUnder(modDir, AssetStem + ".uasset");

    private static string Stage(string modDir)
    {
        var src = GameplayTweaksKit.FindAsset(
                      Path.Combine(EditorPaths.RepoRoot, "work", "tables", "gameplay_tweaks"),
                      AssetStem + ".uasset")
                  ?? GameplayTweaksKit.FindAsset(
                      Path.Combine(EditorPaths.RepoRoot, "work", "tables"),
                      AssetStem + ".uasset")
                  ?? throw new FileNotFoundException("[might-scaling] vanilla " + AssetStem + ".uasset not found");
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) throw new InvalidDataException("[might-scaling] source is not under Sandfall/");
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }
}

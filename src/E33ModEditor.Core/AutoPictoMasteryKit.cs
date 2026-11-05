using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Enables the game's own <c>Sandfall.LearnAnyLuminaAfterOneBattle</c> debug
/// CVar by flipping its registration default in
/// <c>BP_jRPG_Controller_World.DEBUG_RegisterCVars</c>.
///
/// The game reads that CVar in
/// <c>BP_CharacterData.SetPassiveEffectSharedProgression</c>. When it is on,
/// the shared progression is written straight to the mastered shape
/// (IsLearnt + full LearntSteps) as soon as a picto is progressed, instead of
/// waiting for its <c>LuminaUnlockStepsCount</c> (3–4) battles. The CVar is
/// registered from the controller's UserConstructionScript, so changing the
/// registered default is the switch — no extra bytecode or table rows.
///
/// This is the built-in "Luminas are always learnt after one battle only"
/// debug option, not a bespoke reimplementation.
/// </summary>
public static class AutoPictoMasteryKit
{
    public const string ControllerAsset = "BP_jRPG_Controller_World";
    public const string RegisterFunction = "DEBUG_RegisterCVars";
    public const string CvarName = "Sandfall.LearnAnyLuminaAfterOneBattle";
    public const string RegisterCall = "RegisterBoolCVar";

    /// <summary>Stages the controller with the CVar default changed to true.</summary>
    public static int Apply(string modDir)
    {
        var path = CopyIntoMod(ControllerAsset, modDir);
        var asset = Open(path);
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == RegisterFunction)
            ?? throw new InvalidDataException(
                $"[auto-picto] {ControllerAsset}.{RegisterFunction} not found");

        var call = FindRegisterCall(asset, fn)
            ?? throw new InvalidDataException(
                $"[auto-picto] {CvarName} registration not found in {RegisterFunction}");

        var parameters = call.Parameters ?? Array.Empty<KismetExpression>();
        var defaultIndex = Array.FindIndex(parameters, p => p is EX_False);
        if (defaultIndex < 0)
        {
            if (parameters.Any(p => p is EX_True)) return 0; // already on
            throw new InvalidDataException(
                $"[auto-picto] {CvarName} registration has no bool default");
        }

        // EX_True and EX_False are both one byte, so no jump/offset fixups are
        // needed: this is a pure value swap inside the existing call statement.
        call.Parameters[defaultIndex] = new EX_True();
        asset.Write(path);
        return 1;
    }

    /// <summary>True when the staged controller carries the enabled default.</summary>
    public static bool Verify(string uassetPath)
    {
        var asset = Open(uassetPath);
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == RegisterFunction);
        if (fn?.ScriptBytecode is null) return false;
        var call = FindRegisterCall(asset, fn);
        if (call is null) return false;
        var parameters = call.Parameters ?? Array.Empty<KismetExpression>();
        return parameters.Any(p => p is EX_True)
            && !parameters.Any(p => p is EX_False);
    }

    private static EX_CallMath? FindRegisterCall(UAsset asset, FunctionExport fn)
    {
        foreach (var stmt in fn.ScriptBytecode ?? Array.Empty<KismetExpression>())
        {
            if (stmt is not EX_CallMath call) continue;
            if (call.StackNode is not { } node || !node.IsImport()) continue;
            var importIndex = Math.Abs(node.Index) - 1;
            if (importIndex < 0 || importIndex >= asset.Imports.Count) continue;
            if (asset.Imports[importIndex].ObjectName.ToString() != RegisterCall) continue;
            var parameters = call.Parameters ?? Array.Empty<KismetExpression>();
            if (!parameters.Any(p => p is EX_NameConst n
                    && n.Value.ToString() == CvarName)) continue;
            return call;
        }
        return null;
    }

    private static UAsset Open(string path)
    {
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
    }

    private static string CopyIntoMod(string asset, string modDir)
    {
        var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "auto_picto_mastery");
        var src = FindAsset(dir, asset + ".uasset")
            ?? FindAsset(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), asset + ".uasset");
        if (src is null)
        {
            Directory.CreateDirectory(dir);
            // Never let an installed mod shadow the vanilla extraction.
            using (E33ModEditor.Core.Packer.DonorExtract.SuspendMods())
                new Retoc().ToLegacy(dir, asset);
            src = FindAsset(dir, asset + ".uasset")
                ?? throw new FileNotFoundException(asset + ".uasset");
        }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) throw new InvalidDataException(asset + " is not under Sandfall/");
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    private static string? FindAsset(string root, string fileName)
    {
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).FirstOrDefault();
    }
}

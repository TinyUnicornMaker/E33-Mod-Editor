using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Owns the real critical-hit damage formula in <c>BP_BattleDamages.ApplyCriticalHitModifiers</c>.
///
/// Vanilla builds a damage modifier of <c>1.5 x ExtraCriticalHitDamageMultiplier</c>, where the
/// <c>1.5</c> is a hardcoded <see cref="EX_DoubleConst"/> and <c>ExtraCriticalHitDamageMultiplier</c>
/// carries the per-skill / picto / passive crit bonuses. That <c>1.5</c> is the ONLY place crit
/// damage is set — the <c>DT_Balancing_MainCharacterSkills.Generic_CriticalStrikeDMGIncrease</c>
/// row is not read by the shipped battle code, so patching it does nothing.
///
/// This kit edits the constant and (optionally) the multiplier:
/// <code>critMultiplier = (1 + CritDamageIncrease) x [ max(1, critRate/100) ] x Extra</code>
/// <list type="bullet">
/// <item><c>CritDamageIncrease</c> (the "Crit damage addend", vanilla 0.5) rewrites the baked
/// constant to <c>1 + addend</c>. Vanilla 0.5 keeps 1.5.</item>
/// <item>crit-rate scaling wraps that base in <c>max(1, critRate/100)</c>, so 100 crit rate keeps
/// the base and 200 doubles it. The crit rate is the same <c>GetCriticalRollToBeat()</c> the
/// crit roll uses, and the floor stops a crit ever dealing less than the base.</item>
/// </list>
///
/// Both edits live in the same function and compose: the scaling reads the (possibly new) base
/// constant. Every new expression reuses existing imports/locals and the inserted call is an
/// in-asset statement clone, so the asset round-trips. With both at vanilla the kit is not
/// invoked and the staged asset is removed.
/// </summary>
public static class CritDamageKit
{
    public const string AssetName = "BP_BattleDamages";
    public const string FunctionName = "ApplyCriticalHitModifiers";

    /// <summary>Vanilla crit base baked into the function (DoubleConst 1.5 = 1 + 0.5).</summary>
    public const double VanillaBase = 1.5;

    /// <summary>The crit stat is a percentage against a 0–100 roll.</summary>
    public const double CritRateDivisor = 100.0;

    /// <summary>Floor applied to critRate/100 so a crit never drops below the base.</summary>
    public const double Floor = 1.0;

    public static int Apply(string modDir, float addend, bool critRateScaling)
    {
        var dest = Stage(modDir);
        var asset = Open(dest);
        var n = Patch(asset, addend, critRateScaling);
        asset.Write(dest);
        return n;
    }

    /// <summary>
    /// Re-opens a staged copy and asserts the requested edits survived: the base constant equals
    /// <c>1 + addend</c>, the crit-rate floor/read are present when enabled, and every absolute
    /// jump target still lands on a statement start.
    /// </summary>
    public static bool Verify(string uassetPath, float addend, bool critRateScaling)
    {
        var asset = Open(uassetPath);
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == FunctionName);
        if (fn?.ScriptBytecode is null) return false;

        double wantBase = 1.0 + addend;
        bool baseOk = false, readsCrit = false, fmax = false, floorOne = false;
        foreach (var s in fn.ScriptBytecode)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_CallMath cm && ImportName(asset, cm.StackNode) == "MakeLiteralDouble"
                    && cm.Parameters is { Length: 1 } && cm.Parameters[0] is EX_DoubleConst d
                    && Math.Abs(d.Value - wantBase) < 1e-6)
                    baseOk = true;
                if (e is EX_LocalVariable lv
                    && PartyFiveKit.FieldName(lv.Variable) == "CallFunc_GetCriticalRollToBeat_Value")
                    readsCrit = true;
                if (e is EX_CallMath fm && ImportName(asset, fm.StackNode) == "FMax")
                {
                    fmax = true;
                    if (fm.Parameters is { Length: 2 }
                        && fm.Parameters[1] is EX_DoubleConst fd && Math.Abs(fd.Value - Floor) < 1e-9)
                        floorOne = true;
                }
            });

        if (!baseOk) return false;
        if (critRateScaling && !(readsCrit && fmax && floorOne)) return false;

        try { PartyFiveKit.VerifyJumps(asset, fn); }
        catch { return false; }
        return true;
    }

    private static int Patch(UAsset asset, float addend, bool critRateScaling)
    {
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == FunctionName)
            ?? throw new InvalidDataException($"[critdamage] {FunctionName} not found in {AssetName}");
        if (fn.ScriptBytecode is null || fn.ScriptBytecode.Length == 0)
            throw new InvalidDataException($"[critdamage] {FunctionName} has no bytecode");

        var stmts = fn.ScriptBytecode.ToList();

        // Vanilla crit base:
        //   Let CallFunc_Multiply_DoubleDouble_ReturnValue =
        //       Multiply_DoubleDouble( CallFunc_MakeLiteralDouble_ReturnValue(=1.5),
        //                              ExtraCriticalHitDamageMultiplier )
        int mulIdx = -1;
        EX_CallMath? mulCall = null;
        for (int i = 0; i < stmts.Count; i++)
        {
            if (stmts[i] is not EX_Let let) continue;
            if (PartyFiveKit.FieldName(let.Value) != "CallFunc_Multiply_DoubleDouble_ReturnValue") continue;
            if (let.Expression is not EX_CallMath cm
                || ImportName(asset, cm.StackNode) != "Multiply_DoubleDouble"
                || cm.Parameters is not { Length: 2 }) continue;
            mulIdx = i; mulCall = cm; break;
        }
        if (mulIdx < 0 || mulCall is null)
            throw new InvalidDataException(
                "[critdamage] vanilla crit multiply statement not found — asset shape changed?");

        // The base constant is the MakeLiteralDouble feeding the multiply's first operand.
        // mulCall.Parameters[0] reads that local; find the statement that writes it.
        var baseLocal = (mulCall.Parameters[0] as EX_VariableBase) is { } bv
            ? PartyFiveKit.FieldName(bv.Variable) : null;
        EX_DoubleConst? baseConst = null;
        if (baseLocal is not null)
            foreach (var s in stmts)
                if (s is EX_Let bl && PartyFiveKit.FieldName(bl.Value) == baseLocal
                    && bl.Expression is EX_CallMath bcm
                    && ImportName(asset, bcm.StackNode) == "MakeLiteralDouble"
                    && bcm.Parameters is { Length: 1 }
                    && bcm.Parameters[0] is EX_DoubleConst dc)
                {
                    baseConst = dc; break;
                }
        if (baseConst is null)
            throw new InvalidDataException("[critdamage] crit base constant not found");

        double wantBase = 1.0 + addend;
        bool baseChanged = Math.Abs(baseConst.Value - wantBase) > 1e-6;
        baseConst.Value = wantBase;

        uint baseOffset = 0;
        for (int i = 0; i < mulIdx; i++) baseOffset += (uint)stmts[i].GetSize(asset);

        int edits = baseChanged ? 1 : 0;

        if (critRateScaling)
        {
            // Template: an existing GetCriticalRollToBeat() call that writes
            // CallFunc_GetCriticalRollToBeat_Value (the same local the debug log reads).
            EX_LocalVirtualFunction? critCallTpl = null;
            foreach (var s in stmts)
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (critCallTpl is not null) return;
                    if (e is EX_LocalVirtualFunction vf
                        && vf.VirtualFunctionName.ToString() == "GetCriticalRollToBeat"
                        && vf.Parameters is { Length: 1 }
                        && PartyFiveKit.FieldName((vf.Parameters[0] as EX_VariableBase)?.Variable)
                           == "CallFunc_GetCriticalRollToBeat_Value")
                        critCallTpl = vf;
                });
            if (critCallTpl is null)
                throw new InvalidDataException("[critdamage] GetCriticalRollToBeat call template not found");

            // The FMax import isn't used in this function but exists elsewhere in the asset
            // (ApplyDefense). Clone its CallMath so the StackNode import index is valid.
            EX_CallMath? fmaxTpl = null;
            foreach (var f in asset.Exports.OfType<FunctionExport>())
                foreach (var s in f.ScriptBytecode ?? Array.Empty<KismetExpression>())
                    MayhemFixKit.Walk(new[] { s }, e =>
                    {
                        if (fmaxTpl is null && e is EX_CallMath cm2
                            && ImportName(asset, cm2.StackNode) == "FMax")
                            fmaxTpl = cm2;
                    });
            if (fmaxTpl is null)
                throw new InvalidDataException("[critdamage] FMax import not found in " + AssetName);

            // critRate (percent) -> factor = FMax(critRate × 0.01, 1.0)
            var critRead = (EX_LocalVariable)MayhemFixKit.CloneKismet((KismetExpression)critCallTpl.Parameters[0]);
            var critFactor = new EX_CallMath
            {
                StackNode = mulCall.StackNode,
                Parameters = new KismetExpression[] { critRead, new EX_DoubleConst { Value = 1.0 / CritRateDivisor } },
            };
            var floorFactor = new EX_CallMath
            {
                StackNode = fmaxTpl.StackNode,
                Parameters = new KismetExpression[] { critFactor, new EX_DoubleConst { Value = Floor } },
            };
            var scaledBase = new EX_CallMath
            {
                StackNode = mulCall.StackNode,
                Parameters = new KismetExpression[]
                {
                    MayhemFixKit.CloneKismet(mulCall.Parameters[0]),
                    floorFactor,
                },
            };

            var critCall = (EX_LocalVirtualFunction)MayhemFixKit.CloneKismet(critCallTpl);

            // 1) Grow the multiply statement in place (shifts every target after it).
            uint oldSize = (uint)stmts[mulIdx].GetSize(asset);
            mulCall.Parameters[0] = scaledBase;
            uint newSize = (uint)stmts[mulIdx].GetSize(asset);
            long grow = (long)newSize - oldSize;
            if (grow != 0) ShiftTargetsAtOrAfter(asset, fn, baseOffset + oldSize, grow);

            // 2) Insert the crit-rate call just before the multiply.
            stmts.Insert(mulIdx, critCall);
            fn.ScriptBytecode = stmts.ToArray();
            uint insertSize = (uint)critCall.GetSize(asset);
            if (insertSize != 0) ShiftTargetsAtOrAfter(asset, fn, baseOffset, insertSize);

            MayhemFixKit.RecalcContextSkips(asset, fn);
            edits += 1;
        }

        PartyFiveKit.VerifyJumps(asset, fn);

        Console.WriteLine(
            $"[critdamage] {FunctionName}: base {VanillaBase}->{wantBase}" +
            (critRateScaling ? ", scaled by max(1, critRate/100)" : "") + $" ({edits} edit(s))");
        return edits;
    }

    private static UAsset Open(string path)
    {
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
    }

    private static string? ImportName(UAsset asset, FPackageIndex? idx)
    {
        if (idx is null || !idx.IsImport()) return null;
        var i = Math.Abs(idx.Index) - 1;
        return i >= 0 && i < asset.Imports.Count ? asset.Imports[i].ObjectName.ToString() : null;
    }

    private static string Stage(string modDir)
    {
        var src = FileCopy.FindUnder(
                Path.Combine(EditorPaths.RepoRoot, "work", "tables", "battle_damages"),
                AssetName + ".uasset")
            ?? FileCopy.FindTablesAsset(AssetName + ".uasset");
        if (src is null)
        {
            var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "battle_damages");
            Directory.CreateDirectory(dir);
            using (E33ModEditor.Core.Packer.DonorExtract.SuspendMods())
                new Retoc().ToLegacy(dir, AssetName);
            src = FileCopy.FindUnder(dir, AssetName + ".uasset")
                ?? throw new FileNotFoundException(AssetName + ".uasset");
        }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) throw new InvalidDataException(AssetName + " is not under Sandfall/");
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    /// <summary>Moves every absolute target (jump / push / switch) at or after
    /// <paramref name="point"/> by <paramref name="delta"/> after a statement grew
    /// or was inserted.</summary>
    private static void ShiftTargetsAtOrAfter(UAsset asset, FunctionExport fn, uint point, long delta)
    {
        if (delta == 0) return;
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref pos, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j when j.CodeOffset >= point:
                        j.CodeOffset = (uint)(j.CodeOffset + delta); break;
                    case EX_JumpIfNot jn when jn.CodeOffset >= point:
                        jn.CodeOffset = (uint)(jn.CodeOffset + delta); break;
                    case EX_PushExecutionFlow pf when pf.PushingAddress >= point:
                        pf.PushingAddress = (uint)(pf.PushingAddress + delta); break;
                    case EX_SwitchValue sv:
                        if (sv.EndGotoOffset >= point) sv.EndGotoOffset = (uint)(sv.EndGotoOffset + delta);
                        for (int i = 0; i < sv.Cases.Length; i++)
                            if (sv.Cases[i].NextOffset >= point)
                                sv.Cases[i].NextOffset = (uint)(sv.Cases[i].NextOffset + delta);
                        break;
                }
            });
    }
}

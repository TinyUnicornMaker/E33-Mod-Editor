using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Per-stance bonus effects. Damage multipliers live in
/// DT_Balancing_MainCharacterSkills (CombatBalanceKit); this kit patches the
/// buff Blueprints so the missing handlers and extra effects exist:
///  - Offensive: +50% dealt (row only; the earlier +1 AP/hit bytecode insert
///    was removed — the buff BP is no longer staged and falls back vanilla).
///  - Defensive: received ×0.5 (row only, vanilla value) + vanilla
///    ProcessAPChange still gives +1 AP per parry. No staged BP.
///  - Virtuous: +100% dealt (row 2.0); +1 Berserk/turn via
///    BP_StanceMechanicComponent.OnTurnStart (existing function; clones
///    SwitchStance's SpawnObject/ActivateBuff). The earlier cloned
///    ProcessReceivedRawDamages handler was removed — Virtuous has no
///    received modifier, so its buff BP is not staged (vanilla fallback).
///  - Stanceless: left vanilla (empty BP).
///
/// Load-crash note (2026-09-01): these BPs are unversioned children of
/// BP_BattleBuff_Stance_Base. Opening an isolated copy makes the CDO fall
/// back to RawExport ("schema index 6"); packing that override crashes new
/// game. Always extract+open from a shared Content root that contains
/// Stance_Base so the CDO parses, then Write the parsed export.
/// </summary>
public static class StanceEffectsKit
{
    public static int Apply(string modDir)
    {
        var root = PictosCreateKit.EnsureExtractedShared("stance_family",
            "BP_BattleBuff_Stance_Base",
            "BP_BattleBuff_Stance_Offensive",
            "BP_BattleBuff_Stance_Virtuose",
            "BP_StanceMechanicComponent",
            "BP_BattleBuffInstance",
            "BP_BattleBuff_Berserk");
        // Prime the parent schema so child CDOs parse (same pattern as pictos).
        var basePath = Find(root, "BP_BattleBuff_Stance_Base.uasset")
            ?? throw new FileNotFoundException("BP_BattleBuff_Stance_Base");
        _ = Open(basePath);

        int n = 0;
        // Offensive + Virtuose buff BPs are no longer staged: their damage
        // effects are pure balancing rows now (Offensive dealt 1.5, Virtuose
        // dealt 2.0), and the old cloned handlers (Offensive +1 AP/hit,
        // Virtuose received) must not ship. Remove stale staged copies so an
        // incremental GUI pack converges to vanilla-clean BPs.
        n += RemoveStagedBuff(modDir, "BP_BattleBuff_Stance_Offensive");
        n += RemoveStagedBuff(modDir, "BP_BattleBuff_Stance_Virtuose");
        n += StageVirtuoseBerserk(root, modDir);
        n += PatchMechanicBerserkPerTurn(root, modDir);
        return n;
    }

    /// <summary>Deletes a previously-staged stance buff BP (and its .uexp) from the mod tree.</summary>
    private static int RemoveStagedBuff(string modDir, string stem)
    {
        int n = 0;
        if (!Directory.Exists(modDir)) return 0;
        foreach (var file in Directory.EnumerateFiles(modDir, stem + ".*", SearchOption.AllDirectories).ToList())
        {
            File.Delete(file);
            n++;
            Console.WriteLine("[StanceEffects] removed staged " + Path.GetFileName(file) + " (row-driven stance)");
        }
        return n;
    }

    // ── Unique 12-turn Berserk clone (cap is 12 stacks). Vanilla Berserk is permanent; applying it
    // once in Virtuose left the buff on in every later stance. Do not call
    // SetPermanent/SetTurnDuration from OnTurnStart — those synthesized
    // virtuals crashed battle load with Maelle.

    private const string VirtuoseBerserkStem = "BP_BattleBuff_VirtuoseBerserk";
    private const string VirtuoseBerserkClass = "BP_BattleBuff_VirtuoseBerserk_C";
    private const string VirtuoseBerserkPkg = "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_VirtuoseBerserk";

    private static int StageVirtuoseBerserk(string root, string modDir)
    {
        var inst = Find(root, "BP_BattleBuffInstance.uasset");
        if (inst is not null) _ = Open(inst);
        var src = Find(root, "BP_BattleBuff_Berserk.uasset")
            ?? throw new FileNotFoundException("BP_BattleBuff_Berserk");
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return 0;
        var dest = Path.Combine(modDir, src[idx..].Replace("BP_BattleBuff_Berserk", VirtuoseBerserkStem, StringComparison.Ordinal));
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        var asset = Open(src);
        AssertCdoParsed(asset, "Berserk donor");
        LuneSkillsKit.RetargetPairs(asset, dest, ("BP_BattleBuff_Berserk", VirtuoseBerserkStem));
        asset.FolderName = new FString(VirtuoseBerserkPkg);
        var cdo = asset.Exports.OfType<NormalExport>()
            .First(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
        foreach (var p in cdo.Data)
        {
            var n = p.Name?.ToString() ?? "";
            if (n == "IsPermanent" && p is BoolPropertyData b) b.Value = false;
            if (n == "TurnDuration" && p is IntPropertyData i) i.Value = 12;
        }
        int stripped = StripBerserkGrowth(asset);
        WriteParsed(asset, dest);
        Console.WriteLine("[StanceEffects] " + VirtuoseBerserkStem + " CDO: 12-turn, not permanent"
            + (stripped > 0 ? $", growth stripped ({stripped} scale param(s) -> 1.0)" : ", growth already stripped"));
        return 1;
    }

    // ── Berserk growth strip: vanilla Berserk calls ChangeSize(1.035) per
    // stack (while DamageMultiplierStackCount <= 5). The Virtuose clone is
    // re-applied every turn in Virtuose, so each overlapping instance grew
    // Maelle and DeactivateBuff has no inverse — permanent battle-long
    // ballooning. Neutralize the scale factor to 1.0 in place: EX_DoubleConst
    // is fixed 8 bytes, so no statement sizes, jumps, or context skips change.
    private static int StripBerserkGrowth(UAsset asset)
    {
        int n = 0;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            MayhemFixKit.Walk(fn.ScriptBytecode, e =>
            {
                // EX_LocalVirtualFunction derives from EX_VirtualFunction, so one check covers both.
                if (e is EX_VirtualFunction vf
                    && vf.VirtualFunctionName.ToString() == "ChangeSize"
                    && vf.Parameters is not null)
                {
                    foreach (var p in vf.Parameters)
                    {
                        if (p is EX_DoubleConst dc && Math.Abs(dc.Value - 1.0) > 1e-9)
                        {
                            dc.Value = 1.0;
                            n++;
                        }
                    }
                }
            });
        }
        return n;
    }

    // ── Virtuose berserk: OnTurnStart only (gated on CurrentStance == Virtuose).
    // Do not insert into SwitchStance: OnTurnEnd calls SwitchStance(Stanceless)
    // every turn, and ShiftAbsolute on that function's PushExecutionFlow made
    // the Virtuose sound/berserk tail run regardless of stance.

    private static int PatchMechanicBerserkPerTurn(string root, string modDir)
    {
        var src = Find(root, "BP_StanceMechanicComponent.uasset")
            ?? throw new FileNotFoundException("BP_StanceMechanicComponent");
        var dest = DestUnderMod(modDir, src);
        var asset = Open(src);
        AssertCdoParsed(asset, "StanceMechanic");
        var switchFn = Fn(asset, "SwitchStance");
        var turnFn = Fn(asset, "OnTurnStart");
        if (switchFn.ScriptBytecode is null || turnFn.ScriptBytecode is null) return 0;
        if (HasObjectConst(asset, turnFn, VirtuoseBerserkClass))
        {
            WriteParsed(asset, dest);
            return 1;
        }

        var t = FindSwitchTemplates(asset, switchFn);
        var berserkClass = LuneSkillsKit.EnsureObjectImport(asset,
            VirtuoseBerserkPkg, VirtuoseBerserkClass, VirtuoseBerserkClass, "/Script/Engine");

        EnsureLocals(turnFn, switchFn,
            LocalNames(t.EqStance3)
                .Concat(LocalNames(t.GetOwner))
                .Concat(LocalNames(t.DynCast))
                .Concat(LocalNames(t.DynSuccess))
                .Concat(LocalNames(t.Spawn))
                .Concat(LocalNames(t.Activate))
                .Distinct(StringComparer.Ordinal)
                .ToArray());
        var owner = FPackageIndex.FromExport(asset.Exports.IndexOf(turnFn));
        var eq = RetargetLocals(MayhemFixKit.CloneKismet(t.EqStance3), owner);
        var gate = new EX_JumpIfNot
        {
            CodeOffset = 0,
            BooleanExpression = eq is EX_LetBool lb
                ? MayhemFixKit.CloneKismet(lb.VariableExpression)
                : throw new InvalidDataException("eq template is not LetBool"),
        };
        RetargetLocals(gate.BooleanExpression, owner);
        var block = new List<KismetExpression>
        {
            eq,
            gate,
            RetargetLocals(MayhemFixKit.CloneKismet(t.GetOwner), owner),
            RetargetLocals(MayhemFixKit.CloneKismet(t.DynCast), owner),
            RetargetLocals(MayhemFixKit.CloneKismet(t.DynSuccess), owner),
            RetargetLocals(RetargetSpawn(MayhemFixKit.CloneKismet(t.Spawn), asset, berserkClass), owner),
            RetargetLocals(MayhemFixKit.CloneKismet(t.Activate), owner),
        };
        var stmts = turnFn.ScriptBytecode.ToList();
        int insertAt = stmts.FindIndex(s => s is EX_Return);
        if (insertAt < 0) throw new InvalidDataException("OnTurnStart has no Return");
        uint insertOffset = 0;
        for (int i = 0; i < insertAt; i++) insertOffset += (uint)stmts[i].GetSize(asset);
        uint blockSize = (uint)block.Sum(b => (long)b.GetSize(asset));
        gate.CodeOffset = insertOffset + blockSize;
        InsertBlock(asset, turnFn, insertAt, block);
        PointJumpIfNotAtReturn(asset, turnFn);
        WriteParsed(asset, dest);
        Console.WriteLine("[StanceEffects] OnTurnStart: +1 " + VirtuoseBerserkStem + " while CurrentStance=Virtuose");
        return 1;
    }

    private sealed record SwitchTemplates(
        KismetExpression Spawn, KismetExpression Activate, KismetExpression GetOwner,
        KismetExpression DynCast, KismetExpression DynSuccess, KismetExpression EqStance3);

    private static SwitchTemplates FindSwitchTemplates(UAsset asset, FunctionExport fn)
    {
        KismetExpression? spawn = null, activate = null, getOwner = null, dynCast = null, dynOk = null, eq3 = null;
        foreach (var s in fn.ScriptBytecode!)
        {
            bool isSpawn = false, isAct = false, isGetOwner = false, hasEq = false, has3 = false, hasStance = false;
            string? letName = null;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_FinalFunction ff && ff.StackNode.IsImport())
                {
                    var nm = asset.Imports[Math.Abs(ff.StackNode.Index) - 1].ObjectName.ToString();
                    if (nm == "SpawnObject") isSpawn = true;
                    if (nm == "GetOwner") isGetOwner = true;
                    if (nm == "EqualEqual_ByteByte") hasEq = true;
                }
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "ActivateBuff") isAct = true;
                if (e is EX_ByteConst bc && bc.Value == 3) has3 = true;
                if (e is EX_InstanceVariable iv && PtrName(iv.Variable) == "CurrentStance") hasStance = true;
                if (e is EX_LocalVariable lv) letName ??= PtrName(lv.Variable);
                if (e is EX_LetObj lo && lo.VariableExpression is EX_LocalVariable lv2)
                    letName = PtrName(lv2.Variable);
            });
            if (isSpawn && spawn is null) spawn = s;
            if (isAct && activate is null) activate = s;
            if (isGetOwner && letName == "CallFunc_GetOwner_ReturnValue_3") getOwner = s;
            if (letName == "K2Node_DynamicCast_AsBP_J_RPG_Character_Battle_Base_3" && dynCast is null) dynCast = s;
            if (letName == "K2Node_DynamicCast_bSuccess_3" && dynOk is null) dynOk = s;
            if (hasEq && has3 && hasStance && eq3 is null) eq3 = s;
        }
        if (spawn is null || activate is null || getOwner is null || dynCast is null || dynOk is null || eq3 is null)
            throw new InvalidDataException(
                $"SwitchStance templates spawn={spawn != null} act={activate != null} owner={getOwner != null} cast={dynCast != null} ok={dynOk != null} eq3={eq3 != null}");
        return new SwitchTemplates(spawn, activate, getOwner, dynCast, dynOk, eq3);
    }

    private static bool HasObjectConst(UAsset asset, FunctionExport fn, string objectName)
    {
        bool hit = false;
        if (fn.ScriptBytecode is null) return false;
        MayhemFixKit.Walk(fn.ScriptBytecode, e =>
        {
            if (e is EX_ObjectConst oc && oc.Value.IsImport()
                && asset.Imports[Math.Abs(oc.Value.Index) - 1].ObjectName.ToString() == objectName)
                hit = true;
        });
        return hit;
    }

    private static KismetExpression RetargetSpawn(KismetExpression spawn, UAsset asset, FPackageIndex berserkClass)
    {
        bool swapped = false;
        MayhemFixKit.Walk(new[] { spawn }, e =>
        {
            if (swapped) return;
            if (e is EX_FinalFunction ff && ff.StackNode.IsImport()
                && asset.Imports[Math.Abs(ff.StackNode.Index) - 1].ObjectName.ToString() == "SpawnObject"
                && ff.Parameters is { Length: >= 1 })
            {
                ff.Parameters[0] = new EX_ObjectConst { Value = berserkClass };
                swapped = true;
            }
        });
        if (!swapped) throw new InvalidDataException("could not retarget SpawnObject to Berserk");
        return spawn;
    }

    private static KismetExpression RetargetLocals(KismetExpression stmt, FPackageIndex owner)
    {
        MayhemFixKit.Walk(new[] { stmt }, e =>
        {
            if (e is EX_LocalVariable lv && lv.Variable?.New is not null)
                lv.Variable = CopyPtr(lv.Variable, owner);
            if (e is EX_LocalOutVariable ov && ov.Variable?.New is not null)
                ov.Variable = CopyPtr(ov.Variable, owner);
        });
        return stmt;
    }

    private static KismetPropertyPointer CopyPtr(KismetPropertyPointer p, FPackageIndex owner) =>
        new(new FFieldPath(p.New!.Path?.ToArray() ?? [], owner));

    private static void PointJumpIfNotAtReturn(UAsset asset, FunctionExport fn)
    {
        uint pos = 0, retAt = 0;
        EX_JumpIfNot? gate = null;
        foreach (var s in fn.ScriptBytecode!)
        {
            if (s is EX_JumpIfNot j) gate = j;
            if (s is EX_Return) retAt = pos;
            pos += (uint)s.GetSize(asset);
        }
        if (gate is not null) gate.CodeOffset = retAt;
    }

    private static void InsertBlock(UAsset asset, FunctionExport fn, int insertAt, List<KismetExpression> block)
    {
        var stmts = fn.ScriptBytecode!.ToList();
        uint insertOffset = 0;
        for (int i = 0; i < insertAt; i++) insertOffset += (uint)stmts[i].GetSize(asset);
        uint blockSize = (uint)block.Sum(b => (long)b.GetSize(asset));
        MayhemFixKit.ShiftAbsolute(asset, fn, insertOffset, blockSize, null);
        stmts.InsertRange(insertAt, block);
        fn.ScriptBytecode = stmts.ToArray();
        MayhemFixKit.RecalcContextSkips(asset, fn);
    }

    private static IEnumerable<string> LocalNames(KismetExpression s)
    {
        var names = new List<string>();
        MayhemFixKit.Walk(new[] { s }, e =>
        {
            if (e is EX_LocalVariable lv) names.Add(PtrName(lv.Variable));
            if (e is EX_LocalOutVariable ov) names.Add(PtrName(ov.Variable));
        });
        return names.Where(n => n.Length > 0);
    }

    private static void EnsureLocals(FunctionExport dest, FunctionExport src, params string[] names)
    {
        var have = new HashSet<string>(
            (dest.LoadedProperties ?? Array.Empty<FProperty>()).Select(p => p.Name.ToString()),
            StringComparer.Ordinal);
        var extra = (src.LoadedProperties ?? Array.Empty<FProperty>())
            .Where(p => names.Contains(p.Name.ToString(), StringComparer.Ordinal) && have.Add(p.Name.ToString()))
            .ToArray();
        if (extra.Length == 0) return;
        dest.LoadedProperties = (dest.LoadedProperties ?? Array.Empty<FProperty>()).Concat(extra).ToArray();
    }

    private static string PtrName(KismetPropertyPointer? p) =>
        p?.New?.Path?.FirstOrDefault()?.ToString() ?? "";

    // ── shared helpers ───────────────────────────────────────────────────────

    private static UAsset Open(string path) => PictosCreateKit.OpenAsset(path);

    private static FunctionExport Fn(UAsset asset, string name) =>
        FnOrNull(asset, name) ?? throw new InvalidDataException(name + " missing in " + asset.FilePath);

    private static FunctionExport? FnOrNull(UAsset asset, string name) =>
        asset.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == name);

    private static string? Find(string root, string fileName) => FileCopy.FindUnder(root, fileName);

    private static string DestUnderMod(string modDir, string srcUasset)
    {
        var idx = srcUasset.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) throw new InvalidDataException("not under Sandfall: " + srcUasset);
        var dest = Path.Combine(modDir, srcUasset[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        return dest;
    }

    private static void AssertCdoParsed(UAsset asset, string label)
    {
        if (asset.Exports.Any(e => e is RawExport))
            throw new InvalidDataException(label + " has RawExport(s) — CDO/parent schema did not parse.");
        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
        if (cdo is null || cdo.Data.Count < 3)
            throw new InvalidDataException(
                label + " CDO did not parse (need BP_BattleBuff_Stance_Base beside it). props="
                + (cdo?.Data.Count ?? -1));
    }

    private static void WriteParsed(UAsset asset, string dest)
    {
        AssertCdoParsed(asset, Path.GetFileNameWithoutExtension(dest));
        try { asset.Write(dest); }
        catch (FormatException ex) { throw new FormatException(Path.GetFileName(dest) + ": " + ex.Message, ex); }
    }
}

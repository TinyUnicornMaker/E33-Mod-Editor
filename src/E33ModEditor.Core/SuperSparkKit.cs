using System.Reflection;
using E33ModEditor.Core.Localization;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Turns Maelle's Spark into Super Spark in place (same DA / script / sequence
/// names so the skill tree and save unlocks keep working):
///   Void damage, 3 hits, 5 Blight (10 in Offensive / overcharge),
///   Fleuret Fury's authored 3-hit sequence (not a looped Spark clip),
///   slash Niagara swapped for Void systems.
/// Always copies vanilla Spark DA/BP and the Fury sequence, then patches, so
/// re-inject is not additive.
///
/// Lesson: do not loop a 1-hit authored clip. Spark's LevelSequence is one
/// strike + recovery (camera, BattleKeyframes, FX, end events). Truncating
/// and replaying that window desyncs the choreography. Use a real N-hit donor.
/// </summary>
public static class SuperSparkKit
{
    public const string DaStem = "DA_Skill_Maelle_NEW18_Spark";
    public const string BpStem = "BP_Battle_SkillScript_New18_Spark";
    public const string SeqStem = "SEQ_SkillMaelle_New18_Spark";
    public const string SeqDonorStem = "SEQ_SkillMaelle_FleuretFury";

    private const byte FireDamageByte = 2;
    private const byte VoidDamageByte = 8;

    private const string LongDesc =
        "Deals low single target <keyword id=\"Element_Void\">Void</> damage. 3 hits.\n" +
        "Applies 5 Blight\n" +
        "<keyword id=\"Stance_Offensive\">Offensive Stance</>: Applies 10 Blight";

    private const string ShortDesc =
        "Low <keyword id=\"Element_Void\">Void</> damage. 3 hits.\n" +
        "Applies 5 Blight\n" +
        "<img id=\"Stance_Offensive\"/>: Applies 10 Blight";

    public static int Inject(string modDir)
    {
        Directory.CreateDirectory(modDir);
        PictosCreateKit.EnsureExtracted(DaStem);
        PictosCreateKit.EnsureExtracted(BpStem);
        PictosCreateKit.EnsureExtracted(SeqDonorStem);
        PictosCreateKit.EnsureExtracted("DT_Balancing_MainCharacterSkills");

        var daSrc = FileCopy.FindTablesAsset(DaStem + ".uasset")
            ?? throw new FileNotFoundException(DaStem);
        var bpSrc = FileCopy.FindTablesAsset(BpStem + ".uasset")
            ?? throw new FileNotFoundException(BpStem);
        var seqSrc = FileCopy.FindTablesAsset(SeqDonorStem + ".uasset")
            ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "probe-skill"), SeqDonorStem + ".uasset")
            ?? throw new FileNotFoundException(SeqDonorStem);

        var daDest = DestUnder(modDir, daSrc);
        var bpDest = DestUnder(modDir, bpSrc);
        // Donor lives next to Spark; write it under Spark's sequence name so the
        // skill script's ObjectConst SEQ_SkillMaelle_New18_Spark still resolves.
        var seqDest = DestUnder(modDir, seqSrc).Replace(SeqDonorStem, SeqStem, StringComparison.Ordinal);
        FileCopy.AssetPair(daSrc, daDest);
        FileCopy.AssetPair(bpSrc, bpDest);
        FileCopy.AssetPair(seqSrc, seqDest);

        PatchDataAsset(daDest);
        PatchScript(bpDest);
        PatchSequence(seqDest);
        PatchBalancing(modDir);
        Console.WriteLine("[superspark] Spark → Super Spark (Void, Fury 3-hit anim, Blight 5/10 Offensive, Void VFX)");
        return 4;
    }

    private static string DestUnder(string modDir, string srcUasset)
    {
        var full = srcUasset.Replace('\\', '/');
        var idx = full.IndexOf("/Sandfall/", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) throw new InvalidDataException("not under Sandfall/: " + srcUasset);
        return Path.Combine(modDir, full[idx..].TrimStart('/'));
    }

    private static UAsset Open(string path)
    {
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
    }

    // ── data asset ──────────────────────────────────────────────────────────

    private static void PatchDataAsset(string path)
    {
        var da = Open(path);
        foreach (var ex in da.Exports.OfType<NormalExport>())
            foreach (var p in ex.Data)
            {
                if (p.Name.ToString() == "Name" && p is TextPropertyData tname)
                    SetInvariant(tname, "SKILL_SuperSpark_Name", "Super Spark");
                else if (p.Name.ToString() == "Description" && p is TextPropertyData tlong)
                    SetInvariant(tlong, "SKILL_SuperSpark_Description_Long", LongDesc);
                else if (p.Name.ToString() == "ShortDescription" && p is TextPropertyData tshort)
                    SetInvariant(tshort, "SKILL_SuperSpark_Description_Short", ShortDesc);
                else if (p.Name.ToString() == "BaseElementalType" && p is EnumPropertyData en)
                    en.Value = new FName(da, EnumValue(en.Value, "NewEnumerator7"));
                else if (p.Name.ToString() == "BaseElementalType" && p is BytePropertyData b
                         && b.EnumType?.ToString().Contains("Attack", StringComparison.OrdinalIgnoreCase) == true)
                    b.EnumValue = new FName(da, EnumValue(b.EnumValue, "NewEnumerator7"));
            }
        da.Write(path);
    }

    private static string EnumValue(FName? current, string enumerator)
    {
        var s = current?.ToString() ?? "";
        var i = s.IndexOf("::", StringComparison.Ordinal);
        return i >= 0 ? s[..i] + "::" + enumerator : enumerator;
    }

    private static void SetInvariant(TextPropertyData t, string key, string display)
    {
        display = ModLocalization.Inline(key, display);
        t.HistoryType = TextHistoryType.None;
        t.Flags = ETextFlag.CultureInvariant;
        t.Namespace = null;
        t.Value = new FString(display);
        t.CultureInvariantString = new FString(display);
        t.TableId = null;
    }

    // ── skill script ────────────────────────────────────────────────────────

    private static void PatchScript(string path)
    {
        var bp = Open(path);
        var blightIdx = EnsureObjectImport(bp,
            "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Blight",
            "BP_BattleBuff_Blight_C", "BlueprintGeneratedClass", "/Script/Engine");
        EnsureObjectImport(bp,
            "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Blight",
            "Default__BP_BattleBuff_Blight_C", "BP_BattleBuff_Blight_C",
            "/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Blight");

        RetargetNiagara(bp,
            "FX_Explosion",
            "/Game/FX/FX_Leo/Fx/Lune/Unstable_Fire/System/FX_Explosion",
            "/Game/FX/FX_Leo/Fx/ElementalFX/Void/FX_ExplosionVoid",
            "FX_ExplosionVoid");

        var uber = bp.Exports.OfType<FunctionExport>()
            .First(f => f.ObjectName.ToString().StartsWith("ExecuteUbergraph", StringComparison.Ordinal));
        PatchStandardSkillExecutionFlags(uber);

        var fn = bp.Exports.OfType<FunctionExport>()
            .First(f => f.ObjectName.ToString().Contains("OnActionEffect", StringComparison.Ordinal));
        if (fn.ScriptBytecode is null) throw new InvalidDataException("OnActionEffect has no bytecode");

        PatchMakeDamageBuilder(fn);
        RetargetBurnToBlight(bp, fn, blightIdx);
        InsertLastHitGate(bp, fn);
        RecalcContextSkips(bp, uber);
        RecalcContextSkips(bp, fn);
        bp.Write(path);
    }

    /// <summary>
    /// Spark's StandardSkillExecution is (seq, True, False, False, False, …).
    /// Fury's 3-hit script is (seq, True, False, True, True, …). Those last two
    /// bools are what actually dispatches OnActionEffect per BattleKeyframe;
    /// leaving Spark's False,False is why the Fury anim played 3 swings but
    /// only the first key counted as damage, and IsLast never went true so
    /// the status apply was skipped.
    /// </summary>
    private static void PatchStandardSkillExecutionFlags(FunctionExport uber)
    {
        int n = 0;
        Walk(uber.ScriptBytecode!, e =>
        {
            if (e is not EX_VirtualFunction vf) return;
            if (!vf.VirtualFunctionName.ToString().Contains("StandardSkillExecution", StringComparison.Ordinal)) return;
            if (vf.Parameters is not { Length: > 4 } p) return;
            if (p[3] is EX_False) { p[3] = new EX_True(); n++; }
            if (p[4] is EX_False) { p[4] = new EX_True(); n++; }
        });
        if (n < 2)
            throw new InvalidDataException("StandardSkillExecution bools 3/4 were not False→True (patched " + n + ")");
        Console.WriteLine("[superspark] StandardSkillExecution flags → Fury multi-hit (True,True)");
    }

    private static void PatchMakeDamageBuilder(FunctionExport fn)
    {
        int n = 0;
        Walk(fn.ScriptBytecode!, e =>
        {
            if (e is not EX_VirtualFunction vf) return;
            if (!vf.VirtualFunctionName.ToString().Contains("MakeDamageBuilder", StringComparison.Ordinal)) return;
            if (vf.Parameters is null) return;
            foreach (var p in vf.Parameters)
            {
                if (p is EX_ByteConst b && b.Value == FireDamageByte)
                {
                    b.Value = VoidDamageByte;
                    n++;
                    break;
                }
            }
        });
        if (n == 0)
            throw new InvalidDataException("MakeDamageBuilder Fire byte (2) not found");
        Console.WriteLine($"[superspark] MakeDamageBuilder element Fire→Void ({n})");
    }

    private static void RetargetBurnToBlight(UAsset bp, FunctionExport fn, FPackageIndex blightClass)
    {
        int spawn = 0;
        Walk(fn.ScriptBytecode!, e =>
        {
            if (e is EX_ObjectConst oc && oc.Value is { } idx && idx.IsImport())
            {
                try
                {
                    if (idx.ToImport(bp)?.ObjectName.ToString() == "BP_BattleBuff_Burn_C")
                    {
                        oc.Value = blightClass;
                        spawn++;
                    }
                }
                catch { /* ignore */ }
            }
        });
        if (spawn == 0) throw new InvalidDataException("no BP_BattleBuff_Burn_C ObjectConst to retarget");
        Console.WriteLine($"[superspark] spawn buff Burn → Blight ({spawn})");

        // Blight has no BurnMultiplier; the Set-by-name would fail the rest of
        // the status apply. Jump from Spark_BurnMultiplier to Spark_BaseBurnStacks.
        var stmts = fn.ScriptBytecode!.ToList();
        int mult = -1, stacks = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            if (ContainsNameConst(stmts[i], "Spark_BurnMultiplier") && mult < 0) mult = i;
            if (ContainsNameConst(stmts[i], "Spark_BaseBurnStacks") && stacks < 0) stacks = i;
        }
        if (mult < 0 || stacks < 0 || stacks <= mult)
            throw new InvalidDataException($"BurnMultiplier skip markers mult={mult} stacks={stacks}");
        uint dest = StmtStart(bp, fn, stacks);
        var jmp = new EX_Jump { CodeOffset = dest };
        uint jmpSize = jmp.GetSize(bp);
        uint insertAt = StmtStart(bp, fn, mult);
        jmp.CodeOffset = dest + jmpSize;
        ShiftAbsolute(bp, fn, insertAt, jmpSize);
        stmts.Insert(mult, jmp);
        fn.ScriptBytecode = stmts.ToArray();
        Console.WriteLine($"[superspark] skip BurnMultiplier @{mult} → BaseBurnStacks");
    }

    private static void InsertLastHitGate(UAsset bp, FunctionExport fn)
    {
        var stmts = fn.ScriptBytecode!.ToList();
        int isLastStmt = -1, activate = -1, burnLoop = -1, overchargeStmt = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            if (ContainsVirt(stmts[i], "IsLastActionEffectOfSkill") && stmts[i] is EX_LetBool)
                isLastStmt = i;
            if (ContainsInstVar(stmts[i], "IsOvercharged") && stmts[i] is EX_LetBool)
                overchargeStmt = i;
            if (ContainsVirt(stmts[i], "ActivateBuff"))
                activate = i;
            if (ContainsNameConst(stmts[i], "Spark_BaseBurnStacks") && burnLoop < 0)
                burnLoop = i;
        }
        if (isLastStmt < 0 || activate < 0 || burnLoop < 0)
            throw new InvalidDataException(
                $"last-hit gate markers isLast={isLastStmt} act={activate} stacks={burnLoop}");

        int loopStart = burnLoop;
        int floor = overchargeStmt > 0 ? overchargeStmt - 25 : 0;
        while (loopStart > floor)
        {
            if (stmts[loopStart] is EX_Let let && let.Expression is EX_IntConst ic && ic.Value == 0)
            {
                if (loopStart > 0 && stmts[loopStart - 1] is EX_Let let0
                    && let0.Expression is EX_IntConst ic0 && ic0.Value == 0)
                    loopStart -= 1;
                break;
            }
            loopStart--;
        }

        int innerEnd = activate + 1;
        while (innerEnd < stmts.Count && stmts[innerEnd] is not EX_PopExecutionFlow)
            innerEnd++;
        int afterInnerLoop = innerEnd;
        while (afterInnerLoop < stmts.Count && stmts[afterInnerLoop] is not EX_Jump)
            afterInnerLoop++;
        afterInnerLoop++;

        var isLastLocal = CloneExpr(((EX_LetBool)stmts[isLastStmt]).VariableExpression);
        uint loopICode = StmtStart(bp, fn, loopStart);
        uint afterICode = StmtStart(bp, fn, afterInnerLoop);
        var skip = new EX_JumpIfNot { BooleanExpression = isLastLocal, CodeOffset = 0 };
        uint skipSize = skip.GetSize(bp);
        skip.CodeOffset = afterICode + skipSize;
        ShiftAbsolute(bp, fn, loopICode, skipSize);
        stmts.Insert(loopStart, skip);
        fn.ScriptBytecode = stmts.ToArray();
        Console.WriteLine($"[superspark] last-hit gate +{skipSize} iCode @ stmt {loopStart} → {skip.CodeOffset}");
    }

    private static uint StmtStart(UAsset asset, FunctionExport fn, int index)
    {
        uint pos = 0;
        for (int i = 0; i < index; i++)
            fn.ScriptBytecode![i].Visit(asset, ref pos, (_, __) => { });
        return pos;
    }

    private static void ShiftAbsolute(UAsset asset, FunctionExport fn, uint insertionPoint, uint delta)
    {
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref pos, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j when j.CodeOffset > insertionPoint:
                        j.CodeOffset += delta; break;
                    case EX_JumpIfNot jn when jn.CodeOffset > insertionPoint:
                        jn.CodeOffset += delta; break;
                    case EX_PushExecutionFlow pf when pf.PushingAddress > insertionPoint:
                        pf.PushingAddress += delta; break;
                    case EX_SkipOffsetConst sc when sc.Value > insertionPoint:
                        sc.Value += delta; break;
                    case EX_Skip sk when sk.CodeOffset > insertionPoint:
                        sk.CodeOffset += delta; break;
                    case EX_SwitchValue sv:
                        if (sv.EndGotoOffset > insertionPoint) sv.EndGotoOffset += delta;
                        if (sv.Cases is null) break;
                        for (int i = 0; i < sv.Cases.Length; i++)
                            if (sv.Cases[i].NextOffset > insertionPoint)
                                sv.Cases[i].NextOffset += delta;
                        break;
                }
            });
    }

    private static void RecalcContextSkips(UAsset asset, FunctionExport fn)
    {
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref pos, (e, _) =>
            {
                if (e is EX_Context ctx)
                    ctx.Offset = ctx.ContextExpression.GetSize(asset);
            });
    }

    // ── sequence: Fleuret Fury 3-hit donor, retargeted into Spark's package ─

    private static void PatchSequence(string path)
    {
        var asset = Open(path);
        RetargetPairs(asset, path, (SeqDonorStem, SeqStem));

        // Keep Fury's authored skeletal/camera/BattleKeyframes. Only the slash
        // Niagara is swapped so Super Spark still reads as Void.
        RetargetNiagara(asset,
            "FX_Slash",
            "/Game/FX/FX_Leo/Fx/Generic/Systems/FX_Slash",
            "/Game/FX/FX_Leo/Fx/Enemies/Painters/General/FX_VoidSlashInfinite",
            "FX_VoidSlashInfinite");

        var battleSection = BattleKeyframesSection(asset)
            ?? throw new InvalidOperationException("BattleKeyframes section missing on Fury donor");
        int kept = TrimBattleKeyframes(battleSection, 3);
        // Spark's only hit key is byte 50. Fury stores 50, 2, 2, 2 — byte 2 is
        // a different keyframe enum (Track2-style) and Spark's 1-hit dispatcher
        // ignored those, so only the first swing dealt damage.
        int typed = ForceKeyframeByte(battleSection, 50);
        var (lastKey, count) = LastKeyOf(battleSection);
        Console.WriteLine($"[superspark] sequence donor {SeqDonorStem} → {SeqStem}; BattleKeyframes {kept} keys type50={typed} last={lastKey} (count={count})");
        asset.Write(path);
    }

    private static void RetargetPairs(UAsset asset, string destUasset, params (string from, string to)[] pairs)
    {
        var full = destUasset.Replace('\\', '/');
        const string marker = "/Content/";
        var mIdx = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        asset.FolderName = FString.FromString("/Game/" + full[(mIdx + marker.Length)..][..^".uasset".Length]);
        var names = asset.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s)) continue;
            var next = s;
            foreach (var (from, to) in pairs)
                next = next.Replace(from, to, StringComparison.Ordinal);
            if (next != s) asset.SetNameReference(i, FString.FromString(next));
        }
    }

    /// <summary>Keeps the first <paramref name="maxHits"/> BattleKeyframe times (vanilla Fury has a 4th late key).</summary>
    private static int TrimBattleKeyframes(NormalExport section, int maxHits)
    {
        var curve = section.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "ByteCurve") as StructPropertyData;
        if (curve is null) return 0;
        var times = curve.Value.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "Times") as ArrayPropertyData;
        var values = curve.Value.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "Values") as ArrayPropertyData;
        if (times?.Value is null) return 0;
        if (times.Value.Length <= maxHits) return times.Value.Length;
        times.Value = times.Value.Take(maxHits).ToArray();
        if (values?.Value is { Length: > 0 })
            values.Value = values.Value.Take(Math.Min(maxHits, values.Value.Length)).ToArray();
        return times.Value.Length;
    }

    private static int ForceKeyframeByte(NormalExport section, byte hit)
    {
        var curve = section.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "ByteCurve") as StructPropertyData;
        if (curve is null) return 0;
        var values = curve.Value.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "Values") as ArrayPropertyData;
        int n = 0;
        if (values?.Value is null) return 0;
        foreach (var v in values.Value)
        {
            if (v is BytePropertyData b)
            {
                b.Value = hit;
                n++;
            }
        }
        if (curve.Value.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "DefaultValue") is BytePropertyData def)
            def.Value = hit;
        return n;
    }

    private static NormalExport? BattleKeyframesSection(UAsset asset)
    {
        foreach (var ex in asset.Exports.OfType<NormalExport>())
        {
            if (!ex.ObjectName.ToString().StartsWith("MovieSceneByteTrack")) continue;
            var bind = ex.Data.OfType<StructPropertyData>()
                .FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "PropertyBinding");
            var pname = bind?.Value.OfType<NamePropertyData>()
                .FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "PropertyName");
            if (pname?.Value.ToString() != "BattleKeyframes") continue;
            var sections = ex.Data.OfType<ArrayPropertyData>()
                .FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "Sections");
            var idx = sections?.Value.OfType<ObjectPropertyData>().FirstOrDefault()?.Value?.Index ?? 0;
            if (idx > 0 && idx <= asset.Exports.Count && asset.Exports[idx - 1] is NormalExport sec)
                return sec;
        }
        return null;
    }

    private static (int lastKey, int count) LastKeyOf(NormalExport section)
    {
        var curve = section.Data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "ByteCurve") as StructPropertyData;
        var times = curve?.Value.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "Times") as ArrayPropertyData;
        int last = int.MinValue, n = 0;
        if (times?.Value is null) return (last, 0);
        foreach (var t in times.Value.OfType<StructPropertyData>())
        {
            int f = FrameOf(t);
            if (f == int.MinValue) continue;
            n++;
            if (f > last) last = f;
        }
        return (last, n);
    }

    // ── balancing ───────────────────────────────────────────────────────────

    private static void PatchBalancing(string modDir)
    {
        var dest = PictosCreateKit.FindAsset(modDir, "DT_Balancing_MainCharacterSkills.uasset");
        if (dest is null)
        {
            var src = FileCopy.FindTablesAsset("DT_Balancing_MainCharacterSkills.uasset")
                ?? throw new FileNotFoundException("DT_Balancing_MainCharacterSkills");
            dest = DestUnder(modDir, src);
            FileCopy.AssetPair(src, dest);
        }
        var ed = new DataTableEditor(dest);
        SetFloatRow(ed, "Spark_BaseBurnStacks", 5f);
        SetFloatRow(ed, "Spark_OverchargeBurnStacks", 10f);
        ed.Save(dest);
        Console.WriteLine("[superspark] blight stacks via Spark_BaseBurnStacks=5 OverchargeBurnStacks=10");
    }

    private static void SetFloatRow(DataTableEditor ed, string row, float val)
    {
        if (!ed.TryGetRow(row, out var r))
            throw new KeyNotFoundException(row);
        foreach (var child in r.Value)
        {
            switch (child)
            {
                case FloatPropertyData f: f.Value = val; break;
                case DoublePropertyData d: d.Value = val; break;
            }
        }
    }

    // ── niagara / import retarget ───────────────────────────────────────────

    private static void RetargetNiagara(UAsset asset, string oldName, string oldPkg, string newPkg, string newName)
    {
        var names = asset.GetNameMapIndexList();
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s)) continue;
            if (s == oldName) asset.SetNameReference(i, FString.FromString(newName));
            else if (s == oldPkg) asset.SetNameReference(i, FString.FromString(newPkg));
            else if (s.Contains(oldPkg, StringComparison.Ordinal))
                asset.SetNameReference(i, FString.FromString(s.Replace(oldPkg, newPkg, StringComparison.Ordinal)));
        }
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            var n = imp.ObjectName.ToString();
            if (n == oldName)
                imp.ObjectName = new FName(asset, newName);
            else if (n == oldPkg)
                imp.ObjectName = new FName(asset, newPkg);
            else if (n == "Default__" + oldName)
                imp.ObjectName = new FName(asset, "Default__" + newName);
        }
        if (asset.SoftObjectPathList is { Count: > 0 } list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                var an = p.AssetPath.PackageName?.ToString() ?? "";
                if (an.Contains(oldPkg, StringComparison.Ordinal) || an.Contains(oldName, StringComparison.Ordinal))
                    list[i] = new FSoftObjectPath(new FName(asset, newPkg), new FName(asset, newName), p.SubPathString);
            }
        }
        EnsureObjectImport(asset, newPkg, newName, "NiagaraSystem", "/Script/Niagara");
    }

    private static FPackageIndex EnsureObjectImport(UAsset asset, string packagePath, string objectName,
        string className, string classPackage)
    {
        FPackageIndex? pkgIndex = null;
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (imp.ObjectName.ToString() == packagePath && imp.ClassName.ToString() == "Package")
                pkgIndex = FPackageIndex.FromImport(i);
            if (imp.ObjectName.ToString() == objectName && imp.ClassName.ToString() == className)
                return FPackageIndex.FromImport(i);
        }
        pkgIndex ??= asset.AddImport(new Import("/Script/CoreUObject", "Package",
            FPackageIndex.FromRawIndex(0), packagePath, false, asset));
        return asset.AddImport(new Import(classPackage, className, pkgIndex, objectName, false, asset));
    }

    // ── kismet helpers ──────────────────────────────────────────────────────

    private static bool ContainsVirt(KismetExpression e, string name)
    {
        bool hit = false;
        Walk(new[] { e }, x =>
        {
            if (x is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString().Contains(name, StringComparison.Ordinal))
                hit = true;
        });
        return hit;
    }

    private static bool ContainsNameConst(KismetExpression e, string name)
    {
        bool hit = false;
        Walk(new[] { e }, x =>
        {
            if (x is EX_NameConst nc && nc.Value.ToString() == name) hit = true;
        });
        return hit;
    }

    private static bool ContainsInstVar(KismetExpression e, string name)
    {
        bool hit = false;
        Walk(new[] { e }, x =>
        {
            if (x is EX_InstanceVariable iv && PtrName(iv.Variable).Contains(name, StringComparison.Ordinal))
                hit = true;
        });
        return hit;
    }

    private static bool ContainsObjectName(UAsset asset, KismetExpression e, string name)
    {
        bool hit = false;
        Walk(new[] { e }, x =>
        {
            if (x is EX_ObjectConst oc && oc.Value is { } idx && idx.IsImport())
            {
                try
                {
                    if (idx.ToImport(asset)?.ObjectName.ToString() == name) hit = true;
                }
                catch { /* ignore */ }
            }
        });
        return hit;
    }

    private static void ReplaceObjectConst(UAsset asset, KismetExpression e, string oldName, FPackageIndex neu)
    {
        Walk(new[] { e }, x =>
        {
            if (x is EX_ObjectConst oc && oc.Value is { } idx && idx.IsImport())
            {
                try
                {
                    if (idx.ToImport(asset)?.ObjectName.ToString() == oldName)
                        oc.Value = neu;
                }
                catch { /* ignore */ }
            }
        });
    }

    private static string PtrName(KismetPropertyPointer? p)
    {
        if (p?.New?.Path is { Length: > 0 } path)
            return string.Join(".", path.Select(n => n.ToString()));
        return p?.Old?.Index.ToString() ?? "";
    }

    private static void Walk(IEnumerable<KismetExpression> exprs, Action<KismetExpression> visit)
    {
        foreach (var e in exprs)
        {
            if (e is null) continue;
            visit(e);
            foreach (var f in e.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var v = f.GetValue(e);
                if (v is KismetExpression ke) Walk(new[] { ke }, visit);
                else if (v is KismetExpression[] arr) Walk(arr, visit);
            }
            foreach (var p in e.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                if (p.Name is "Token" or "Inst" or "RawValue" or "Tag") continue;
                object? v;
                try { v = p.GetValue(e); }
                catch { continue; }
                if (v is KismetExpression ke) Walk(new[] { ke }, visit);
                else if (v is KismetExpression[] arr) Walk(arr, visit);
            }
        }
    }

    private static KismetExpression CloneExpr(KismetExpression e)
    {
        var t = e.GetType();
        var n = (KismetExpression)Activator.CreateInstance(t)!;
        const BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var f in t.GetFields(bf))
        {
            var v = f.GetValue(e);
            f.SetValue(n, CloneValue(v));
        }
        // EX_ByteConst etc. store the constant on KismetExpression<T>.Value
        if (e is KismetExpression<object> or not null)
        {
            var valProp = t.GetProperty("Value", bf);
            if (valProp is { CanRead: true, CanWrite: true } && valProp.GetIndexParameters().Length == 0)
            {
                try { valProp.SetValue(n, valProp.GetValue(e)); }
                catch { /* some Value props are not clone-writable */ }
            }
        }
        if (e.RawValue is not null && n.RawValue is null)
            n.RawValue = e.RawValue;
        return n;

        static object? CloneValue(object? v)
        {
            switch (v)
            {
                case null: return null;
                case KismetExpression ke: return CloneExpr(ke);
                case KismetExpression[] arr: return arr.Select(CloneExpr).ToArray();
                case FKismetSwitchCase[] cases:
                    return cases.Select(c => new FKismetSwitchCase(
                        CloneExpr(c.CaseIndexValueTerm), c.NextOffset, CloneExpr(c.CaseTerm))).ToArray();
                default: return v;
            }
        }
    }

    private static int FrameOf(StructPropertyData container)
    {
        int found = int.MinValue;
        void WalkFrames(StructPropertyData s)
        {
            foreach (var c in s.Value)
            {
                if (c is FrameNumberPropertyData fn) found = fn.Value.Value;
                else if (c is StructPropertyData inner) WalkFrames(inner);
            }
        }
        WalkFrames(container);
        return found;
    }
}

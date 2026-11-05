using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Rebuilds the battle HUD's gradient-charge widget (WBP_GradientChargeGauge) for a
/// raised BP_GradientGaugeSystem.MaxCharges.
///
/// The widget carries its own copy of the maximum (<c>MaxCharges</c>, vanilla 3) and
/// hard-codes the per-charge layout for three charges: the fill arrays
/// (<c>CurrentPercentage</c>/<c>PercentageTarget</c>) and two 4-case switches —
/// <c>GetChargeColor</c> (pip colour) and <c>SetCharges</c>' <c>ScaleTarget</c> (pip
/// scale). All of them are regenerated for <c>MaxCharges + 1</c> states, with the
/// colour/scale ramp resampled evenly from its vanilla anchor stops.
/// </summary>
public static class GradientGaugeUiKit
{
    public static int Apply(string uassetPath, int maxCharges)
    {
        int states = Math.Clamp(maxCharges, 1, 99) + 1;
        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
            : new UAsset(uassetPath, EngineVersion.VER_UE5_4);

        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
        if (cdo is null) return 0;

        int n = 0;

        // The "n/max" text is built from the widget's own MaxCharges at Initialize.
        if (cdo.Data.FirstOrDefault(p => Clean(p.Name) == "MaxCharges") is IntPropertyData mc && mc.Value != maxCharges)
        {
            mc.Value = maxCharges;
            n++;
        }

        // Per-charge fill arrays are indexed by charge count; grow them to match.
        n += ResizeDoubleArray(cdo, "PercentageTarget", states);
        n += ResizeDoubleArray(cdo, "CurrentPercentage", states);

        // Colour + scale ramps.
        n += PatchColourRamp(asset, cdo, states);
        n += PatchScaleRamp(asset, states);
        // Bar fill = progress toward the next charge. Vanilla reads it from
        // PercentageTarget[Charges]; when that index is past the array's length (which is
        // what happens above the authored charge count) the read yields 0 and the bar sits
        // at its floor. Source it from the PreviewProgress scalar instead, which always exists.
        n += PatchProgressScalar(asset);
        // Value text: vanilla roles (count tinted in CurrentValueText, neutral "/max" in
        // MaxValueText) with both blocks right-justified and snugly sized so the value sits
        // flush against the bar's right end (the slot's -40 right padding does the rest).
        n += PatchValueBoxWidths(asset);

        if (n > 0)
        {
            try { asset.Write(uassetPath); }
            catch (FormatException ex) { throw new FormatException(Path.GetFileName(uassetPath) + ": " + ex.Message, ex); }
            Console.WriteLine($"[GradientGaugeUi] {states} states, {n} edits -> {Path.GetFileName(uassetPath)}");
            VerifyWritten(uassetPath);
        }
        return n;
    }

    /// <summary>
    /// Re-reads the saved widget and re-derives both switches' layouts from the
    /// serialized bytecode, requiring the stored offsets to agree. Catches a bad
    /// write (or a size-model regression) before it ever reaches the pak.
    /// </summary>
    private static void VerifyWritten(string uassetPath)
    {
        try
        {
            var map = DataTableEditor.Mappings;
            var a = map != null
                ? new UAsset(uassetPath, EngineVersion.VER_UE5_4, map)
                : new UAsset(uassetPath, EngineVersion.VER_UE5_4);
            foreach (var fnName in new[] { "GetChargeColor", "SetCharges" })
            {
                var fn = a.Exports.OfType<FunctionExport>()
                    .FirstOrDefault(f => f.ObjectName.ToString() == fnName);
                if (fn?.ScriptBytecode is null) continue;
                if (!FindLetSwitch(a, fn, out var sv, out _, out _, out uint start))
                {
                    Console.WriteLine($"  [verify] {fnName}: no switch found");
                    continue;
                }
                VanillaLayoutOk(a, sv!, start, "verify " + fnName + $" cases={sv!.Cases.Length}");
            }
            VerifyJumps(a, "SetGaugeLerpAlpha");
            VerifyJumps(a, "SetProgress");
        }
        catch (Exception ex)
        {
            Console.WriteLine("  [verify] failed: " + ex.Message);
        }
    }

    /// <summary>
    /// Requires every jump target in a function to land on a statement start. Catches a
    /// bad growth shift after the cumulative-fill rewrite (which moves ~500 bytes).
    /// </summary>
    private static void VerifyJumps(UAsset a, string fnName)
    {
        var fn = a.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == fnName);
        if (fn?.ScriptBytecode is null) return;
        var starts = new HashSet<uint>();
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode)
        {
            starts.Add(pos);
            pos += (uint)st.GetSize(a);
        }
        starts.Add(pos);
        int bad = 0;
        uint p = 0;
        foreach (var st in fn.ScriptBytecode)
            st.Visit(a, ref p, (e, _) =>
            {
                if (e is EX_Jump j && !starts.Contains(j.CodeOffset)) bad++;
                else if (e is EX_JumpIfNot jn && !starts.Contains(jn.CodeOffset)) bad++;
            });
        Console.WriteLine($"  [verify] {fnName}: {starts.Count} statement starts, badJumps={bad}");
    }

    // ── ramps ────────────────────────────────────────────────────────────────

    private static int PatchColourRamp(UAsset asset, NormalExport cdo, int states)
    {
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "GetChargeColor");
        if (fn?.ScriptBytecode is null) return 0;
        if (!FindLetSwitch(asset, fn, out var sw, out var let, out int idx, out uint stmtStart)) return 0;
        if (!VanillaLayoutOk(asset, sw, stmtStart, "colour")) return 0;

        // Vanilla anchors: case 0 = grey struct const, cases 1..3 = the three colour vars.
        var anchors = new List<FLinearColor>();
        foreach (var c in sw.Cases)
        {
            bool ok = EvalColour(fn, cdo, c.CaseTerm, 0, out var col);
            if (!ok) Console.WriteLine($"    [colour] case {anchors.Count}: {Describe(c.CaseTerm)} -> FAIL");
            if (ok) anchors.Add(col);
        }
        if (anchors.Count < 2) return 0;

        var template = FindStruct(fn, e => e is EX_FloatConst);
        if (template is null) return 0;

        var ramp = ResampleColour(anchors, states);
        Console.WriteLine($"  [colour] anchors={Desc(anchors)} -> {states} stops");
        return Rebuild(asset, fn, sw, let, idx, stmtStart, states, i =>
        {
            var c = (EX_StructConst)MayhemFixKit.CloneKismet(template);
            var f = c.Value.OfType<EX_FloatConst>().ToArray();
            if (f.Length >= 4)
            {
                f[0].Value = ramp[i].R;
                f[1].Value = ramp[i].G;
                f[2].Value = ramp[i].B;
                f[3].Value = ramp[i].A;
            }
            return c;
        });
    }

    private static int PatchScaleRamp(UAsset asset, int states)
    {
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "SetCharges");
        if (fn?.ScriptBytecode is null) return 0;
        if (!FindLetSwitch(asset, fn, out var sw, out var let, out int idx, out uint stmtStart)) return 0;
        if (!VanillaLayoutOk(asset, sw, stmtStart, "scale")) return 0;

        var anchors = new List<double>();
        foreach (var c in sw.Cases)
            if (EvalScalar(fn, c.CaseTerm, 0, out var v)) anchors.Add(v);
        if (anchors.Count < 2) return 0;

        var template = FindStruct(fn, e => e is EX_DoubleConst or EX_FloatConst);
        if (template is null) return 0;

        var ramp = ResampleScalar(anchors, states);
        Console.WriteLine($"  [scale] anchors=[{string.Join(", ", anchors.Select(x => x.ToString("0.###")))}] -> {states} stops");
        return Rebuild(asset, fn, sw, let, idx, stmtStart, states, i =>
        {
            var c = (EX_StructConst)MayhemFixKit.CloneKismet(template);
            foreach (var e in c.Value)
            {
                if (e is EX_DoubleConst d) d.Value = ramp[i];
                else if (e is EX_FloatConst f) f.Value = (float)ramp[i];
            }
            return c;
        });
    }

    // ── fill semantics + value text ──────────────────────────────────────────

    /// <summary>
    /// The bar is a progress bar for the *next* gradient charge: <c>PercentageTarget[Charges]</c>
    /// (written by SetProgress) holds the fraction 0..1 toward the following charge and
    /// SetGaugeLerpAlpha maps it onto the bar width. Once Charges runs past the authored
    /// array length the read yields 0 and the bar collapses to its floor, so source the
    /// fraction from the PreviewProgress scalar (always present, unused at runtime) instead:
    ///   SetProgress       : PreviewProgress = TargetProgress
    ///   SetGaugeLerpAlpha : Lerp(PreviewProgress, PreviewProgress, alpha) == PreviewProgress
    /// The mapping and art are untouched, so half progress still means half a bar.
    /// </summary>
    private static int PatchProgressScalar(UAsset asset) =>
        WriteProgressScalar(asset) + ReadProgressScalar(asset);

    private static int WriteProgressScalar(UAsset asset)
    {
        var fn = Fn(asset, "SetProgress");
        if (fn?.ScriptBytecode is null) return 0;
        var target = FindInstanceVar(asset, "PreviewProgress");
        var value = FindLocal(fn, "TargetProgress");
        if (target is null || value is null) return 0;

        var prototype = fn.ScriptBytecode.OfType<EX_Let>().FirstOrDefault();
        if (prototype is null) return 0;
        var assign = (EX_Let)MayhemFixKit.CloneKismet(prototype);
        assign.Variable = MayhemFixKit.CloneKismet(target);
        assign.Expression = MayhemFixKit.CloneKismet(value);
        var stmts = fn.ScriptBytecode.ToList();
        uint size = (uint)assign.GetSize(asset);
        stmts.Insert(0, assign);
        fn.ScriptBytecode = stmts.ToArray();
        ShiftOffsetsAtOrAfter(asset, fn, 0, size);
        Console.WriteLine($"  [fill] SetProgress: PreviewProgress = TargetProgress (+{size}b)");
        return 1;
    }

    private static int ReadProgressScalar(UAsset asset)
    {
        var fn = Fn(asset, "SetGaugeLerpAlpha");
        if (fn?.ScriptBytecode is null) return 0;
        var progress = FindInstanceVar(asset, "PreviewProgress");
        if (progress is null) return 0;

        var stmts = fn.ScriptBytecode.ToList();
        uint stmtStart = 0;
        for (int i = 0; i < stmts.Count; i++)
        {
            var cm = (stmts[i] as EX_Let)?.Expression as EX_CallMath;
            if (cm is null || !IsFn(asset, cm, "Lerp") || cm.Parameters is not { Length: 3 }
                || cm.Parameters[0] is not EX_ArrayGetByRef || cm.Parameters[1] is not EX_ArrayGetByRef)
            {
                stmtStart += (uint)stmts[i].GetSize(asset);
                continue;
            }
            uint oldSize = (uint)stmts[i].GetSize(asset);
            cm.Parameters[0] = MayhemFixKit.CloneKismet(progress);
            cm.Parameters[1] = MayhemFixKit.CloneKismet(progress);
            uint newSize = (uint)stmts[i].GetSize(asset);
            if (newSize < oldSize) ShiftAfterShrink(asset, fn, stmtStart, oldSize, oldSize - newSize);
            Console.WriteLine($"  [fill] SetGaugeLerpAlpha reads PreviewProgress ({(int)newSize - (int)oldSize}b)");
            return 1;
        }
        return 0;
    }

    private static FunctionExport? Fn(UAsset asset, string name) =>
        asset.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == name);

    private static EX_InstanceVariable? FindInstanceVar(UAsset asset, string name)
    {
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            EX_InstanceVariable? found = null;
            MayhemFixKit.Walk(fn.ScriptBytecode, e =>
            {
                if (found is null && e is EX_InstanceVariable iv && PtrName(iv.Variable) == name) found = iv;
            });
            if (found is not null) return found;
        }
        // Not referenced anywhere (a designer-only variable): build the pointer from any
        // existing instance variable so the FFieldPath's owner is the right class.
        EX_InstanceVariable? template = null;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            MayhemFixKit.Walk(fn.ScriptBytecode, e =>
            {
                if (template is null && e is EX_InstanceVariable iv && iv.Variable?.New is not null) template = iv;
            });
            if (template is not null) break;
        }
        if (template is null) return null;
        return new EX_InstanceVariable
        {
            Variable = new KismetPropertyPointer(new FFieldPath(
                new[] { FName.FromString(asset, name) },
                template.Variable.New!.ResolvedOwner)),
        };
    }

    private static KismetExpression? FindLocal(FunctionExport fn, string name)
    {
        KismetExpression? found = null;
        MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (found is null && e is EX_LocalVariable lv && PtrName(lv.Variable) == name) found = lv;
        });
        return found;
    }

    private static bool IsFn(UAsset asset, EX_CallMath cm, string name) =>
        cm.StackNode is { } sn && sn.IsImport() && sn.ToImport(asset).ObjectName.ToString() == name;

    /// <summary>Moves absolute targets after a statement that got shorter back by
    /// <paramref name="shrink"/> bytes (mirrors GameplayTweaksKit.ShiftAfterShrink).</summary>
    private static void ShiftAfterShrink(UAsset asset, FunctionExport fn, uint editStart, uint editOldSize, uint shrink)
    {
        uint editEndOld = editStart + editOldSize;
        uint editEndNew = editEndOld - shrink;
        uint pos = 0;
        foreach (var stmt in fn.ScriptBytecode!)
            stmt.Visit(asset, ref pos, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j: j.CodeOffset = Shift(j.CodeOffset); break;
                    case EX_JumpIfNot jn: jn.CodeOffset = Shift(jn.CodeOffset); break;
                    case EX_PushExecutionFlow pf: pf.PushingAddress = Shift(pf.PushingAddress); break;
                    case EX_SwitchValue sv:
                        sv.EndGotoOffset = Shift(sv.EndGotoOffset);
                        for (int i = 0; i < sv.Cases.Length; i++)
                            sv.Cases[i].NextOffset = Shift(sv.Cases[i].NextOffset);
                        break;
                }

                uint Shift(uint addr) =>
                    addr >= editEndOld ? addr - shrink
                    : addr > editStart ? editEndNew
                    : addr;
            });
    }

    /// <summary>
    /// <summary>
    /// The value is two adjacent WBP_WrappingText blocks. Vanilla renders the count in
    /// CurrentValueText (set and tinted by SetCharges, which receives the real charge count)
    /// and the neutral "/max" in MaxValueText. Each block is pinned internally to ~151px,
    /// which fits two characters, so "/10" (three) wraps. Rebalance the two boxes with the
    /// widget's own setters so the three-character max fits:
    ///   CurrentValueText: min 30 / max 100   (needs at most "10")
    ///   MaxValueText    : min 120 / max 175  (needs "/10")
    /// The setters are plain virtual calls (no imports) and the widget re-applies its inner
    /// SizeBox from them, so SetText (which runs after these) picks up the new widths.
    /// </summary>
    private static int PatchValueBoxWidths(UAsset asset)
    {
        var fn = Fn(asset, "Initialize");
        if (fn?.ScriptBytecode is null) return 0;

        EX_Context? proto = null;
        foreach (var st in fn.ScriptBytecode)
            MayhemFixKit.Walk(new[] { st }, e =>
            {
                if (proto is not null) return;
                if (e is EX_Context c && c.ContextExpression is EX_VirtualFunction vf
                    && vf.VirtualFunctionName.ToString() == "Set Text"
                    && c.ObjectExpression is EX_InstanceVariable) proto = c;
            });
        if (proto is null) return 0;

        var inserts = new KismetExpression[]
        {
            // Both blocks right-justified: each block's text hugs its own right edge, and the
            // blocks are themselves right-aligned in the cell, so the value lands flush against
            // the bar's right end. Snug max box keeps the count and "/10" from separating.
            BoxCall(asset, proto, "CurrentValueText", "SetJustification", new EX_ByteConst { Value = 2 }),
            BoxCall(asset, proto, "MaxValueText", "SetJustification", new EX_ByteConst { Value = 2 }),
            BoxCall(asset, proto, "CurrentValueText", "SetTextMinDesiredWidth", new EX_DoubleConst { Value = 25 }),
            BoxCall(asset, proto, "CurrentValueText", "SetTextMaxDesiredWidth", new EX_DoubleConst { Value = 45 }),
            BoxCall(asset, proto, "MaxValueText", "SetTextMinDesiredWidth", new EX_DoubleConst { Value = 55 }),
            BoxCall(asset, proto, "MaxValueText", "SetTextMaxDesiredWidth", new EX_DoubleConst { Value = 80 }),
        };
        uint size = 0;
        foreach (var e in inserts) size += (uint)e.GetSize(asset);

        var stmts = fn.ScriptBytecode.ToList();
        stmts.InsertRange(0, inserts);
        fn.ScriptBytecode = stmts.ToArray();
        ShiftOffsetsAtOrAfter(asset, fn, 0, size);
        MayhemFixKit.RecalcContextSkips(asset, fn);
        Console.WriteLine($"  [text] value box widths rebalanced (+{size}b)");
        return 1;
    }

    private static KismetExpression BoxCall(
        UAsset asset, EX_Context proto, string widget, string fnName, KismetExpression arg)
    {
        var call = (EX_Context)MayhemFixKit.CloneKismet(proto);
        if (call.ObjectExpression is EX_InstanceVariable iv)
            iv.Variable = new KismetPropertyPointer(new FFieldPath(
                new[] { FName.FromString(asset, widget) },
                iv.Variable.New?.ResolvedOwner ?? FPackageIndex.FromRawIndex(0)));
        if (call.ContextExpression is EX_VirtualFunction vf)
        {
            vf.VirtualFunctionName = FName.FromString(asset, fnName);
            vf.Parameters = new KismetExpression[] { arg };
        }
        return call;
    }

    // ── switch rebuild + offset normalisation ────────────────────────────────

    private static int Rebuild(
        UAsset asset, FunctionExport fn, EX_SwitchValue sw, EX_Let let, int idx, uint stmtStart,
        int states, Func<int, KismetExpression> termFor)
    {
        var proto = sw.Cases.Length > 0 ? sw.Cases[0].CaseIndexValueTerm : null;
        var newSw = (EX_SwitchValue)MayhemFixKit.CloneKismet(sw);
        newSw.Cases = new FKismetSwitchCase[states];
        for (int i = 0; i < states; i++)
            newSw.Cases[i] = new FKismetSwitchCase(MakeIndex(proto, i), 0, termFor(i));
        newSw.EndGotoOffset = 0;

        uint oldSize = (uint)fn.ScriptBytecode![idx].GetSize(asset);
        let.Expression = newSw;
        uint newSize = (uint)fn.ScriptBytecode[idx].GetSize(asset);
        uint delta = newSize > oldSize ? newSize - oldSize : 0;
        if (delta > 0) ShiftOffsetsAtOrAfter(asset, fn, stmtStart + oldSize, delta);
        NormalizeSwitches(asset, fn);
        return 1;
    }

    private static KismetExpression MakeIndex(KismetExpression? proto, int v) =>
        proto is EX_IntConstByte ? new EX_IntConstByte { Value = (byte)v } : new EX_IntConst { Value = v };

    /// <summary>
    /// Sanity guard: recompute the existing switch's layout and require it to match the
    /// stored offsets. If it does not, our bytecode model is wrong and we refuse to
    /// rewrite (better a vanilla widget than a corrupt one).
    /// </summary>
    private static bool VanillaLayoutOk(UAsset asset, EX_SwitchValue sv, uint start, string tag)
    {
        uint p = start + 1 + 6;
        p += (uint)sv.IndexTerm.GetSize(asset);
        var idxPos = new uint[sv.Cases.Length];
        for (int i = 0; i < sv.Cases.Length; i++)
        {
            idxPos[i] = p;
            p += (uint)sv.Cases[i].CaseIndexValueTerm.GetSize(asset) + 4;
            p += (uint)sv.Cases[i].CaseTerm.GetSize(asset);
        }
        uint defPos = p;
        uint end = p + (uint)sv.DefaultTerm.GetSize(asset);

        bool ok = sv.EndGotoOffset == end;
        for (int i = 0; i < sv.Cases.Length; i++)
            ok &= sv.Cases[i].NextOffset == (i + 1 < sv.Cases.Length ? idxPos[i + 1] : defPos);
        Console.WriteLine($"  [{tag}] vanilla switch: cases={sv.Cases.Length} start={start} end={sv.EndGotoOffset}/{end} " +
                          $"indexTerm={sv.Cases[0].CaseIndexValueTerm.GetType().Name} {(ok ? "OK" : "MISMATCH")}");
        return ok;
    }

    /// <summary>
    /// Recomputes every switch's internal flow offsets from the current bytecode
    /// layout. A switch serialises as: token, numCases(2), EndGotoOffset(4), index
    /// term, then per case {index term, NextOffset(4), case term}, then default term.
    /// <c>NextOffset</c> points at the next case's index term (or the default term for
    /// the last case); <c>EndGotoOffset</c> points just past the switch.
    /// </summary>
    private static void NormalizeSwitches(UAsset asset, FunctionExport fn)
    {
        var found = new List<(EX_SwitchValue sv, uint off)>();
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref pos, (e, off) => { if (e is EX_SwitchValue s) found.Add((s, off)); });

        foreach (var (sv, start) in found)
        {
            uint p = start + 1 + 6;
            p += (uint)sv.IndexTerm.GetSize(asset);
            var idxPos = new uint[sv.Cases.Length];
            for (int i = 0; i < sv.Cases.Length; i++)
            {
                idxPos[i] = p;
                p += (uint)sv.Cases[i].CaseIndexValueTerm.GetSize(asset) + 4;
                p += (uint)sv.Cases[i].CaseTerm.GetSize(asset);
            }
            uint defaultPos = p;
            sv.EndGotoOffset = p + (uint)sv.DefaultTerm.GetSize(asset);
            for (int i = 0; i < sv.Cases.Length; i++)
                sv.Cases[i].NextOffset = i + 1 < sv.Cases.Length ? idxPos[i + 1] : defaultPos;
        }
    }

    private static void ShiftOffsetsAtOrAfter(UAsset asset, FunctionExport fn, uint point, uint delta)
    {
        if (delta == 0) return;
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref pos, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j when j.CodeOffset >= point: j.CodeOffset += delta; break;
                    case EX_JumpIfNot jn when jn.CodeOffset >= point: jn.CodeOffset += delta; break;
                    case EX_PushExecutionFlow pf when pf.PushingAddress >= point: pf.PushingAddress += delta; break;
                    case EX_SwitchValue sv:
                        if (sv.EndGotoOffset >= point) sv.EndGotoOffset += delta;
                        for (int i = 0; i < sv.Cases.Length; i++)
                            if (sv.Cases[i].NextOffset >= point) sv.Cases[i].NextOffset += delta;
                        break;
                }
            });
    }

    private static bool FindLetSwitch(
        UAsset asset, FunctionExport fn,
        out EX_SwitchValue? sw, out EX_Let? let, out int idx, out uint start)
    {
        sw = null; let = null; idx = -1; start = 0;
        uint pos = 0;
        for (int i = 0; i < fn.ScriptBytecode!.Length; i++)
        {
            var st = fn.ScriptBytecode[i];
            uint stmtStart = pos;
            EX_SwitchValue? found = null;
            uint foundAt = 0;
            st.Visit(asset, ref pos, (e, off) =>
            {
                if (found is null && e is EX_SwitchValue s) { found = s; foundAt = off; }
            });
            if (found is not null && st is EX_Let l && ReferenceEquals(l.Expression, found))
            {
                sw = found; let = l; idx = i; start = foundAt;
                return true;
            }
            _ = stmtStart;
        }
        return false;
    }

    // ── anchors ──────────────────────────────────────────────────────────────

    private static bool EvalColour(FunctionExport fn, NormalExport cdo, KismetExpression? term, int depth, out FLinearColor col)
    {
        col = default;
        if (term is null || depth > 8) return false;
        switch (term)
        {
            case EX_StructConst sc:
            {
                var f = sc.Value.OfType<EX_FloatConst>().ToArray();
                if (f.Length < 4) return false;
                col = new FLinearColor(f[0].Value, f[1].Value, f[2].Value, f[3].Value);
                return true;
            }
            case EX_LocalVariable lv:
                return EvalColour(fn, cdo, ResolveLocal(fn, lv), depth + 1, out col);
            case EX_InstanceVariable iv:
                return TryCdoColour(cdo, PtrName(iv.Variable), out col);
        }
        return false;
    }

    /// <summary>
    /// Reads a LinearColor CDO property by name. UAssetAPI sometimes nests the
    /// LinearColor inside a StructPropertyData wrapper, so search the subtree.
    /// </summary>
    private static bool TryCdoColour(NormalExport cdo, string field, out FLinearColor col)
    {
        col = default;
        foreach (var p in cdo.Data)
        {
            if (Clean(p.Name) != field) continue;
            var lc = FindLinear(p);
            if (lc is not null) { col = lc.Value; return true; }
        }
        return false;
    }

    private static LinearColorPropertyData? FindLinear(PropertyData p)
    {
        if (p is LinearColorPropertyData l) return l;
        if (p is StructPropertyData s && s.Value is not null)
            foreach (var c in s.Value)
            {
                var r = FindLinear(c);
                if (r is not null) return r;
            }
        return null;
    }

    private static bool EvalScalar(FunctionExport fn, KismetExpression? term, int depth, out double v)
    {
        v = 0;
        if (term is null || depth > 8) return false;
        switch (term)
        {
            case EX_DoubleConst dc: v = dc.Value; return true;
            case EX_FloatConst fc: v = fc.Value; return true;
            case EX_IntConst ic: v = ic.Value; return true;
            case EX_StructConst sc:
            {
                foreach (var e in sc.Value)
                {
                    if (e is EX_DoubleConst d) { v = d.Value; return true; }
                    if (e is EX_FloatConst f) { v = f.Value; return true; }
                }
                return false;
            }
            case EX_LocalVariable lv:
                return EvalScalar(fn, ResolveLocal(fn, lv), depth + 1, out v);
        }
        return false;
    }

    private static KismetExpression? ResolveLocal(FunctionExport fn, EX_LocalVariable lv)
    {
        var name = PtrName(lv.Variable);
        foreach (var st in fn.ScriptBytecode!)
            if (st is EX_Let l && l.Variable is EX_LocalVariable lv2 && PtrName(lv2.Variable) == name)
                return l.Expression;
        return null;
    }

    private static EX_StructConst? FindStruct(FunctionExport fn, Func<KismetExpression, bool> valuePredicate)
    {
        EX_StructConst? found = null;
        MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (found is null && e is EX_StructConst sc && sc.Value.Length > 0 && sc.Value.All(valuePredicate))
                found = sc;
        });
        return found;
    }

    // ── resampling ───────────────────────────────────────────────────────────

    private static double T(int i, int states, int anchors) =>
        states <= 1 || anchors <= 1 ? 0 : (double)i / (states - 1) * (anchors - 1);

    private static List<double> ResampleScalar(IReadOnlyList<double> a, int states)
    {
        var outp = new List<double>(states);
        for (int i = 0; i < states; i++)
        {
            double t = T(i, states, a.Count);
            int s = Math.Min((int)t, a.Count - 2);
            double f = t - s;
            outp.Add(a[s] + (a[s + 1] - a[s]) * f);
        }
        return outp;
    }

    private static List<FLinearColor> ResampleColour(IReadOnlyList<FLinearColor> a, int states)
    {
        var outp = new List<FLinearColor>(states);
        for (int i = 0; i < states; i++)
        {
            double t = T(i, states, a.Count);
            int s = Math.Min((int)t, a.Count - 2);
            float f = (float)(t - s);
            outp.Add(new FLinearColor(
                a[s].R + (a[s + 1].R - a[s].R) * f,
                a[s].G + (a[s + 1].G - a[s].G) * f,
                a[s].B + (a[s + 1].B - a[s].B) * f,
                a[s].A + (a[s + 1].A - a[s].A) * f));
        }
        return outp;
    }

    // ── CDO helpers ──────────────────────────────────────────────────────────

    private static int ResizeDoubleArray(NormalExport cdo, string field, int len)
    {
        if (cdo.Data.FirstOrDefault(p => Clean(p.Name) == field) is not ArrayPropertyData a) return 0;
        var cur = a.Value ?? [];
        if (cur.Length == len) return 0;
        var proto = cur.OfType<DoublePropertyData>().FirstOrDefault();
        var list = new List<PropertyData>(len);
        for (int i = 0; i < len; i++)
        {
            double v = i < cur.Length && cur[i] is DoublePropertyData d ? d.Value : 0;
            var nd = proto is not null
                ? (DoublePropertyData)proto.Clone()
                : new DoublePropertyData(a.Name);
            nd.Value = v;
            list.Add(nd);
        }
        a.Value = list.ToArray();
        return 1;
    }

    private static string Clean(FName? n) => DataTableEditor.CleanName(n);

    private static string PtrName(KismetPropertyPointer p) =>
        p.New?.Path?.FirstOrDefault()?.ToString() ?? "";

    private static string Desc(IReadOnlyList<FLinearColor> cs) =>
        string.Join(" | ", cs.Select(c => $"({c.R:0.##},{c.G:0.##},{c.B:0.##},{c.A:0.##})"));

    private static string Describe(KismetExpression? e) => e switch
    {
        EX_InstanceVariable iv => "ivar:" + PtrName(iv.Variable),
        EX_LocalVariable lv => "local:" + PtrName(lv.Variable),
        EX_StructConst sc => $"struct[{sc.Value.Length}]",
        null => "null",
        _ => e.GetType().Name,
    };
}

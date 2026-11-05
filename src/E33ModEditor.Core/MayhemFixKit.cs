using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

public static class MayhemFixKit
{
    private static readonly HashSet<string> Copied = new(StringComparer.Ordinal);

    public static int Apply(string modDir)
    {
        var dest = CopyMayhemIntoMod(modDir);
        int patches = 0;
        if (dest is not null)
        {
            var map = DataTableEditor.Mappings;
            var asset = map != null ? new UAsset(dest, EngineVersion.VER_UE5_4, map) : new UAsset(dest, EngineVersion.VER_UE5_4);
            patches += PatchByteConstAllowDark(asset);
            patches += PatchFxSwitchForDark(asset);
            // Cast-level dark scaling: damage multiplier x 2^(dark stains consumed)
            patches += InjectDarkCastFactor(asset);
            patches += PatchDarkAnimationPerHit(asset);
            patches += PatchExcludeLight(asset);
            if (patches > 0)
            {
                foreach (var fn in asset.Exports.OfType<FunctionExport>())
                {
                    if (fn.ScriptBytecode is null) continue;
                    RecalcContextSkips(asset, fn);
                }
                WriteAsset(asset, dest);
            }
        }
        // Mayhem balancing rows are owned by SkillOverhaulKit specs so the toggles
        // control them; the description (gold multipliers) is owned by
        // SkillDescriptionsKit.
        return patches;
    }

    private static string? CopyMayhemIntoMod(string modDir)
    {
        var tablesRoot = Path.Combine(EditorPaths.RepoRoot, "work", "tables");
        var src = FileCopy.FindUnder(tablesRoot, "BP_Battle_SkillScript_Mayhem.uasset");
        if (src is null)
        {
            var tmpDir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "gameplay_tweaks");
            Directory.CreateDirectory(tmpDir);
            try { new Retoc().ToLegacy(tmpDir, "BP_Battle_SkillScript_Mayhem"); } catch { }
            src = FileCopy.FindUnder(tmpDir, "BP_Battle_SkillScript_Mayhem.uasset") ?? FileCopy.FindUnder(tablesRoot, "BP_Battle_SkillScript_Mayhem.uasset");
        }
        if (src is null) return null;
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return null;
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!Copied.Add(dest))
        {
            if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);
            return dest;
        }
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    private static int PatchByteConstAllowDark(UAsset asset)
    {
        int n = 0;
        // HasFullyValidStains and ValidateSkillAvailability: change all ByteConst 6 -> 9
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            var name = fn.ObjectName.ToString();
            if (name is "HasFullyValidStains" or "ValidateSkillAvailability")
            {
                if (fn.ScriptBytecode is null) continue;
                Walk(fn.ScriptBytecode, e =>
                {
                    if (e is EX_ByteConst bc && bc.Value == 6)
                    {
                        bc.Value = 9;
                        n++;
                    }
                });
            }
        }
        // OnActionEffect: change all ByteConst 6 -> 0. The tier key (6 = Dark) becomes
        // 0 = Empty: tier = queueLen(4) - emptyCount = ACTUAL stains consumed, so dark
        // stains count toward the tier instead of the move always using FourStains.
        var onAction2 = asset.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (onAction2?.ScriptBytecode is not null)
        {
            Walk(onAction2.ScriptBytecode, e =>
            {
                if (e is EX_ByteConst bc3 && bc3.Value == 6)
                {
                    bc3.Value = 0;
                    n++;
                }
            });
        }

        return n;
    }

    private static int PatchFxSwitchForDark(UAsset asset)
    {
        var fn = asset.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (fn?.ScriptBytecode is null) return 0;
        // Find DealDamages index to distinguish FX switch (after) from initial guard (before)
        int dealIdx = -1;
        for (int i = 0; i < fn.ScriptBytecode.Length; i++)
        {
            bool hasDeal = false;
            Walk(new[] { fn.ScriptBytecode[i] }, e => { if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "DealDamages") hasDeal = true; });
            if (hasDeal) { dealIdx = i; break; }
        }
        if (dealIdx < 0) return 0;
        int n = 0;
        // The FX dispatch is a chain of K2Node_SwitchEnum_CmpSuccess = NotEqual(Stain, X) + JumpIfNot
        // Repurpose the first check for 0 (Empty, never hit) to 6 (Dark) after DealDamages
        bool patched = false;
        Walk(fn.ScriptBytecode, e =>
        {
            if (patched) return;
            if (e is EX_LetBool lb && lb.AssignmentExpression is EX_FinalFunction ff && ff.Parameters.Length == 2 && ff.Parameters[1] is EX_ByteConst bc && bc.Value == 0)
            {
                // Check if this LetBool is after DealDamages by checking its position relative to dealIdx
                // Find its index in ScriptBytecode
                for (int i = dealIdx+1; i < fn.ScriptBytecode.Length; i++)
                {
                    bool found = false;
                    Walk(new[] { fn.ScriptBytecode[i] }, inner => { if (inner == lb) found = true; });
                    if (found)
                    {
                        if (ff.StackNode.IsImport() && ff.StackNode.ToImport(asset).ObjectName.ToString() == "NotEqual_ByteByte")
                        {
                            bc.Value = 6;
                            n = 1;
                            patched = true;
                        }
                        break;
                    }
                }
            }
        });
        // Fallback: if not found via LetBool, directly search ByteConst 0 after DealDamages
        if (n == 0)
        {
            for (int i = dealIdx+1; i < fn.ScriptBytecode.Length; i++)
            {
                bool changed = false;
                Walk(new[] { fn.ScriptBytecode[i] }, e =>
                {
                    if (e is EX_ByteConst bc2 && bc2.Value == 0 && !changed)
                    {
                        // Check if this ByteConst is inside a NotEqual for FX (not the initial guard)
                        // The initial guard's 0 is before MakeDamageBuilder, FX's 0 is after DealDamages, so this is FX
                        bc2.Value = 6;
                        changed = true;
                        n = 1;
                    }
                });
                if (changed) break;
            }
        }
        return n;
    }

    private static int InjectDarkCastFactor(UAsset asset)
    {
        // Cast-level dark scaling: StainAmountDamageMultiplier *= 2^darkCount, computed
        // once at cast start. Per-hit doubling (old InjectDarkDouble) scaled a 4-hit cast
        // by (2^d + 4 - d)/4 instead of 2^d; the user formula is total x 2^d, so the dark
        // factor multiplies the whole cast. darkCount = Map_Find(countMap, 6); factor =
        // powMap[darkCount] with powMap {0:1, 1:2, 2:4, 3:8, 4:16}.
        var fn = asset.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();

        // Tier block anchor: Let StainAmountDamageMultiplier = Map_Find_Value_1.
        // Match on the expression local (instance-variable name extraction is unreliable).
        int finalLetIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            if (stmts[i] is EX_Let let && let.Expression is EX_LocalVariable lvExpr)
            {
                var nmE = lvExpr.Variable?.New?.Path?.FirstOrDefault()?.ToString() ?? lvExpr.Variable?.ToString() ?? "";
                if (nmE == "CallFunc_Map_Find_Value_1")
                {
                    finalLetIdx = i;
                    break;
                }
            }
        }
        if (finalLetIdx < 0) { System.Console.WriteLine("[MayhemFix] castFactor: no finalLet"); return 0; }

        int setMapIdx = -1, firstFindIdx = -1, secondFindIdx = -1;
        for (int i = 0; i < finalLetIdx; i++)
        {
            if (stmts[i] is EX_SetMap && setMapIdx < 0) { setMapIdx = i; continue; }
            KismetExpression? assign = (stmts[i] as EX_LetBool)?.AssignmentExpression;
            if (assign is EX_Context lc) assign = lc.ContextExpression; // Map_Find is context-wrapped
            if (assign is EX_FinalFunction ff
                && ff.StackNode.IsImport() && ff.StackNode.ToImport(asset).ObjectName.ToString() == "Map_Find")
            {
                if (firstFindIdx < 0) firstFindIdx = i;
                else if (secondFindIdx < 0) secondFindIdx = i;
            }
        }
        if (setMapIdx < 0 || firstFindIdx < 0 || secondFindIdx < 0)
        {
            System.Console.WriteLine($"[MayhemFix] castFactor: setMap={setMapIdx} first={firstFindIdx} second={secondFindIdx}");
            return 0;
        }

        FPackageIndex? mapFindIdx = null, multiplyIdx = null, convIdx = null;
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var nm = asset.Imports[i].ObjectName.ToString();
            if (nm == "Map_Find") mapFindIdx = FPackageIndex.FromImport(i);
            if (nm == "Multiply_DoubleDouble") multiplyIdx = FPackageIndex.FromImport(i);
            if (nm == "Conv_IntToDouble") convIdx = FPackageIndex.FromImport(i);
        }
        if (mapFindIdx is null || multiplyIdx is null || convIdx is null)
        {
            System.Console.WriteLine($"[MayhemFix] castFactor: imports mapFind={mapFindIdx is not null} mult={multiplyIdx is not null} conv={convIdx is not null}");
            return 0;
        }

        KismetPropertyPointer? countMapPtr = null, mapPtr = null, findValuePtr = null, findValue1Ptr = null, tierPtr = null;
        foreach (var s in stmts)
        {
            Walk(new[] { s }, e =>
            {
                if (e is EX_LocalVariable lv)
                {
                    var nm = lv.Variable?.New?.Path?.FirstOrDefault()?.ToString() ?? lv.Variable?.ToString() ?? "";
                    if (nm == "CallFunc_Get_Active_Stain_Count_By_Type_result" && countMapPtr is null) countMapPtr = lv.Variable;
                    if (nm == "K2Node_MakeMap_Map" && mapPtr is null) mapPtr = lv.Variable;
                    if (nm == "CallFunc_Map_Find_Value" && findValuePtr is null) findValuePtr = lv.Variable;
                    if (nm == "CallFunc_Map_Find_Value_1" && findValue1Ptr is null) findValue1Ptr = lv.Variable;
                    if (nm == "CallFunc_Subtract_IntInt_ReturnValue_1" && tierPtr is null) tierPtr = lv.Variable;
                }
            });
        }
        if (countMapPtr is null || mapPtr is null || findValuePtr is null || findValue1Ptr is null || tierPtr is null)
        {
            System.Console.WriteLine($"[MayhemFix] castFactor: locals countMap={countMapPtr is not null} map={mapPtr is not null} fv={findValuePtr is not null} fv1={findValue1Ptr is not null} tier={tierPtr is not null}");
            return 0;
        }

        // powMap: clone the tier SetMap, replace the row-lookup values with 1,2,4,8,16
        var powMap = (EX_SetMap)CloneKismet(stmts[setMapIdx]);
        int powVal = 1;
        for (int i = 0; i < powMap.Elements.Length; i++)
        {
            if (powMap.Elements[i] is EX_StructMemberContext)
            {
                powMap.Elements[i] = new EX_IntConst { Value = powVal };
                powVal *= 2;
            }
        }

        // darkCount find: clone the first Map_Find, key Temp_byte_Variable -> ByteConst 6
        var darkFind = CloneKismet(stmts[firstFindIdx]);
        Walk(new[] { darkFind }, e =>
        {
            if (e is EX_FinalFunction ff && ff.StackNode.IsImport()
                && ff.StackNode.ToImport(asset).ObjectName.ToString() == "Map_Find" && ff.Parameters.Length >= 2)
                ff.Parameters[1] = new EX_ByteConst { Value = 6 };
        });

        // factor find: clone the second Map_Find; key -> darkCount (Map_Find_Value);
        // value target -> the tier local (CallFunc_Subtract_IntInt_ReturnValue_1, dead here)
        var factorFind = CloneKismet(stmts[secondFindIdx]);
        Walk(new[] { factorFind }, e =>
        {
            if (e is EX_FinalFunction ff && ff.StackNode.IsImport()
                && ff.StackNode.ToImport(asset).ObjectName.ToString() == "Map_Find" && ff.Parameters.Length >= 2)
                ff.Parameters[1] = new EX_LocalVariable { Variable = findValuePtr };
            if (e is EX_LocalVariable lv)
            {
                var nm = lv.Variable?.New?.Path?.FirstOrDefault()?.ToString() ?? lv.Variable?.ToString() ?? "";
                if (nm == "CallFunc_Map_Find_Value_1") lv.Variable = tierPtr;
            }
        });

        // Final Let: StainAmountDamageMultiplier = Multiply(mult, Conv_IntToDouble(factor))
        var finalLet = (EX_Let)stmts[finalLetIdx];
        uint oldLetSize = (uint)finalLet.GetSize(asset);
        finalLet.Expression = new EX_FinalFunction
        {
            StackNode = multiplyIdx,
            Parameters = new KismetExpression[]
            {
                new EX_LocalVariable { Variable = findValue1Ptr },
                new EX_FinalFunction
                {
                    StackNode = convIdx,
                    Parameters = new KismetExpression[] { new EX_LocalVariable { Variable = tierPtr } }
                }
            }
        };
        uint newLetSize = (uint)finalLet.GetSize(asset);

        var newExprs = new KismetExpression[] { powMap, darkFind, factorFind };
        uint insertOffset = 0;
        for (int i = 0; i < finalLetIdx; i++) insertOffset += (uint)stmts[i].GetSize(asset);
        long delta = 0;
        foreach (var ne in newExprs) delta += ne.GetSize(asset);
        delta += newLetSize - oldLetSize;

        ShiftAbsolute(asset, fn, insertOffset, (uint)delta, null);

        var list = fn.ScriptBytecode.ToList();
        list.InsertRange(finalLetIdx, newExprs);
        fn.ScriptBytecode = list.ToArray();
        return 1;
    }

    private static int PatchDarkAnimationPerHit(UAsset asset)
    {
        // Shared violet (crash-proof): Dark reuses Light's FX which is tinted violet.
        // Light stains are now excluded from consumption (PatchExcludeLight), so Mayhem
        // will only play Dark's violet FX for Dark stains; Light hits won't occur for Mayhem.
        var onAction = asset.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        var fxLight = asset.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "FX_ImpactLight");
        if (onAction?.ScriptBytecode is null || fxLight?.ScriptBytecode is null) return 0;

        TintViolet(fxLight.ScriptBytecode);
        InjectExplosionLightViolet(fxLight);

        int dealStmt = -1;
        for (int i = 0; i < onAction.ScriptBytecode.Length; i++)
        {
            bool hasDeal = false;
            Walk(new[] { onAction.ScriptBytecode[i] }, e => { if (e is EX_LocalVirtualFunction lv2 && lv2.VirtualFunctionName.ToString() == "DealDamages") hasDeal = true; });
            if (hasDeal) { dealStmt = i; break; }
        }
        if (dealStmt < 0) return 0;

        int lightCall = -1;
        for (int i = 0; i < onAction.ScriptBytecode.Length; i++)
            if (onAction.ScriptBytecode[i] is EX_LocalVirtualFunction lvf && lvf.VirtualFunctionName.ToString() == "FX_ImpactLight") lightCall = i;
        if (lightCall < 2) return 0;
        uint lightOffset = 0;
        for (int i = 0; i < lightCall - 2; i++) lightOffset += (uint)onAction.ScriptBytecode[i].GetSize(asset);

        for (int i = dealStmt + 1; i < onAction.ScriptBytecode.Length; i++)
        {
            if (onAction.ScriptBytecode[i] is EX_JumpIfNot jn && jn.BooleanExpression is EX_LocalVariable lv3 &&
                lv3.Variable?.New?.Path is { Length: > 0 } pp && pp[0]?.ToString() == "K2Node_SwitchEnum_CmpSuccess")
            {
                bool isDarkCheck = false;
                if (i > 0 && onAction.ScriptBytecode[i - 1] is EX_LetBool lb && lb.AssignmentExpression is EX_FinalFunction ff)
                    foreach (var pp2 in ff.Parameters) if (pp2 is EX_ByteConst bc && bc.Value == 6) isDarkCheck = true;
                if (!isDarkCheck) continue;
                jn.CodeOffset = lightOffset;
                return 1;
            }
        }
        return 0;
    }

    internal static int PatchExcludeLight(UAsset asset, byte? consumeOnlyElement = null)
    {
        // Replaces the tail-only consume guard with a scan over the reversed stain queue.
        // Vanilla consumed ONLY Reverse[0] (the tail) and guarded it with NotEqual(stain, 6->9).
        // That is a dead hit whenever the tail is Light (7) or Empty (0); queue order is
        // uncontrolled, so Mayhem must consume the first non-Light, non-Empty stain
        // (Fire 2 / Ice 3 / Lightning 4 / Earth 5 / Dark 6) found from the tail, at its true
        // original index (Len-1-i). Light stays in the queue; Dark/elementals are always consumed.
        //
        // The old AND-guard approach changed statement sizes without ShiftAbsolute, leaving every
        // stored jump target stale (+31/+62 bytes) - the crash. This version inserts a proper
        // scan loop and shifts all offsets, so jump targets stay valid.
        var fn = asset.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();

        // Locate the consume block: stmt with ConsumeStainAtIndex (vanilla idx 46), then the guard
        // PopExecutionFlowIfNot before it (vanilla idx 40); block = [guardPopIdx-4 .. consumeIdx].
        int consumeIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool isConsume = false;
            Walk(new[] { stmts[i] }, e => { if (e is EX_LocalVirtualFunction lvf && lvf.VirtualFunctionName.ToString().Contains("ConsumeStainAtIndex", StringComparison.Ordinal)) isConsume = true; });
            if (isConsume) { consumeIdx = i; break; }
        }
        if (consumeIdx < 0) return 0;
        int guardPopIdx = -1;
        for (int i = consumeIdx - 1; i >= 0 && i >= consumeIdx - 12; i--)
            if (stmts[i] is EX_PopExecutionFlowIfNot) { guardPopIdx = i; break; }
        if (guardPopIdx < 0) return 0;
        int blockStart = guardPopIdx - 4;
        if (blockStart < 0) return 0;
        bool guardIsGet = false;
        Walk(new[] { stmts[blockStart] }, e =>
        {
            if (e is EX_FinalFunction ffG && ffG.StackNode.IsImport() && ffG.StackNode.ToImport(asset).ObjectName.ToString() == "Array_Get") guardIsGet = true;
        });
        if (!guardIsGet) return 0;

        // --- Local pointers (all exist in vanilla OnActionEffect) ---
        KismetPropertyPointer? counterPtr = null, lenPtr = null, stainPtr = null, consumedPtr = null,
            addPtr = null, subPtr = null, comp2Ptr = null, lessBoolPtr = null, notEqBoolPtr = null;
        foreach (var s in stmts)
        {
            Walk(new[] { s }, e =>
            {
                if (e is EX_LocalVariable lv)
                {
                    var nm = lv.Variable?.New?.Path?.FirstOrDefault()?.ToString() ?? lv.Variable?.ToString() ?? "";
                    if (nm == "Temp_int_Loop_Counter_Variable" && counterPtr is null) counterPtr = lv.Variable;
                    if (nm == "CallFunc_Array_Length_ReturnValue" && lenPtr is null) lenPtr = lv.Variable;
                    if (nm == "CallFunc_Array_Get_Item" && stainPtr is null) stainPtr = lv.Variable;
                    if (nm == "Local_StainConsumedElement" && consumedPtr is null) consumedPtr = lv.Variable;
                    if (nm == "CallFunc_Add_IntInt_ReturnValue_1" && addPtr is null) addPtr = lv.Variable;
                    if (nm == "CallFunc_Subtract_IntInt_ReturnValue" && subPtr is null) subPtr = lv.Variable;
                    if (nm == "CallFunc_GetComponentByClass_ReturnValue_2" && comp2Ptr is null) comp2Ptr = lv.Variable;
                    if (nm == "CallFunc_Less_IntInt_ReturnValue" && lessBoolPtr is null) lessBoolPtr = lv.Variable;
                    if (nm == "CallFunc_NotEqual_ByteByte_ReturnValue_1" && notEqBoolPtr is null) notEqBoolPtr = lv.Variable;
                }
            });
        }
        if (counterPtr is null || lenPtr is null || stainPtr is null || consumedPtr is null
            || addPtr is null || subPtr is null || comp2Ptr is null || lessBoolPtr is null || notEqBoolPtr is null)
            return 0;

        // --- Clone template statements from the whole function ---
        KismetExpression? lenRead = null, getRead = null, lessTpl = null, consumedTpl = null,
            addTpl = null, subTpl = null, compTpl = null, consumeTpl = null, loopPopTpl = null, notEqTpl = null, counterZeroTpl = null, counterAssignTpl = null;
        foreach (var s in stmts)
        {
            bool isGet = false, isConsume = false;
            Walk(new[] { s }, e =>
            {
                if (e is EX_FinalFunction ff2 && ff2.StackNode.IsImport())
                {
                    var nm2 = ff2.StackNode.ToImport(asset).ObjectName.ToString();
                    if (nm2 == "Array_Get") isGet = true;
                }
                if (e is EX_LocalVirtualFunction lvf2 && lvf2.VirtualFunctionName.ToString().Contains("ConsumeStainAtIndex", StringComparison.Ordinal)) isConsume = true;
            });
            if (s is EX_Let letL && (letL.Variable as EX_LocalVariable)?.Variable == lenPtr && lenRead is null)
            {
                bool hasLen = false;
                Walk(new[] { letL.Expression }, e => { if (e is EX_FinalFunction ffL && ffL.StackNode.IsImport() && ffL.StackNode.ToImport(asset).ObjectName.ToString() == "Array_Length") hasLen = true; });
                if (hasLen) lenRead = CloneKismet(s);
            }
            if (isGet && getRead is null) getRead = CloneKismet(s);
            if (s is EX_LetBool lbL && lbL.AssignmentExpression is EX_FinalFunction ffLess && ffLess.StackNode.IsImport()
                && ffLess.StackNode.ToImport(asset).ObjectName.ToString() == "Less_IntInt" && lessTpl is null) lessTpl = CloneKismet(s);
            if (s is EX_Let letC && letC.Expression is EX_LocalVariable lvC && consumedTpl is null)
            {
                var nmVarC = (letC.Variable as EX_LocalVariable)?.Variable?.New?.Path?.FirstOrDefault()?.ToString() ?? (letC.Variable as EX_LocalVariable)?.Variable?.ToString() ?? "";
                var nmC = lvC.Variable?.New?.Path?.FirstOrDefault()?.ToString() ?? lvC.Variable?.ToString() ?? "";
                if (nmVarC == "Local_StainConsumedElement" && nmC == "CallFunc_Array_Get_Item") consumedTpl = CloneKismet(s);
            }
            if (s is EX_Let letA && letA.Expression is EX_FinalFunction ffAdd && ffAdd.StackNode.IsImport()
                && ffAdd.StackNode.ToImport(asset).ObjectName.ToString() == "Add_IntInt"
                && (letA.Variable as EX_LocalVariable)?.Variable == addPtr && addTpl is null) addTpl = CloneKismet(s);
            if (s is EX_Let letS && letS.Expression is EX_FinalFunction ffSub && ffSub.StackNode.IsImport()
                && ffSub.StackNode.ToImport(asset).ObjectName.ToString() == "Subtract_IntInt"
                && (letS.Variable as EX_LocalVariable)?.Variable == subPtr && subTpl is null) subTpl = CloneKismet(s);
            if (s is EX_LetObj lo && (lo.VariableExpression as EX_LocalVariable)?.Variable == comp2Ptr && compTpl is null) compTpl = CloneKismet(s);
            if (s is EX_Context ctxC && isConsume && consumeTpl is null) consumeTpl = CloneKismet(s);
            if (s is EX_PopExecutionFlowIfNot pif && pif.BooleanExpression is EX_LocalVariable lvP && loopPopTpl is null)
            {
                var nmP = lvP.Variable?.New?.Path?.FirstOrDefault()?.ToString() ?? lvP.Variable?.ToString() ?? "";
                if (nmP == "CallFunc_Less_IntInt_ReturnValue") loopPopTpl = CloneKismet(s);
            }
            if (s is EX_LetBool lbN && lbN.AssignmentExpression is EX_FinalFunction ffN && ffN.StackNode.IsImport()
                && ffN.StackNode.ToImport(asset).ObjectName.ToString() == "NotEqual_ByteByte" && notEqTpl is null) notEqTpl = CloneKismet(s);
            if (s is EX_Let let0 && let0.Expression is EX_IntConst ic0 && ic0.Value == 0
                && (let0.Variable as EX_LocalVariable)?.Variable == counterPtr && counterZeroTpl is null) counterZeroTpl = CloneKismet(s);
            if (s is EX_Let letA2 && letA2.Expression is EX_LocalVariable lvA2 && counterAssignTpl is null)
            {
                var nmVarA2 = (letA2.Variable as EX_LocalVariable)?.Variable?.New?.Path?.FirstOrDefault()?.ToString() ?? (letA2.Variable as EX_LocalVariable)?.Variable?.ToString() ?? "";
                var nmA2 = lvA2.Variable?.New?.Path?.FirstOrDefault()?.ToString() ?? lvA2.Variable?.ToString() ?? "";
                if (nmVarA2 == "Temp_int_Loop_Counter_Variable" && nmA2 == "CallFunc_Add_IntInt_ReturnValue_1") counterAssignTpl = CloneKismet(s);
            }
        }
        if (lenRead is null || getRead is null || lessTpl is null || consumedTpl is null || addTpl is null
            || subTpl is null || compTpl is null || consumeTpl is null || loopPopTpl is null || notEqTpl is null || counterZeroTpl is null || counterAssignTpl is null)
            return 0;

        // --- Mutate clones ---
        // getRead: Array_Get(Reverse, 0) -> Array_Get(Reverse, counter)
        Walk(new[] { getRead }, e =>
        {
            if (e is EX_FinalFunction ffG2 && ffG2.StackNode.IsImport() && ffG2.StackNode.ToImport(asset).ObjectName.ToString() == "Array_Get"
                && ffG2.Parameters.Length >= 2 && ffG2.Parameters[1] is EX_IntConst)
                ffG2.Parameters[1] = new EX_LocalVariable { Variable = counterPtr };
        });
        // lessTpl: Less(counter, len2) -> Less(counter, len)
        Walk(new[] { lessTpl }, e =>
        {
            if (e is EX_FinalFunction ffL2 && ffL2.StackNode.IsImport() && ffL2.StackNode.ToImport(asset).ObjectName.ToString() == "Less_IntInt"
                && ffL2.Parameters.Length >= 2)
                ffL2.Parameters[1] = new EX_LocalVariable { Variable = lenPtr };
        });
        // subTpl: Subtract(len, 1) -> Subtract(len, add)
        Walk(new[] { subTpl }, e =>
        {
            if (e is EX_FinalFunction ffS2 && ffS2.StackNode.IsImport() && ffS2.StackNode.ToImport(asset).ObjectName.ToString() == "Subtract_IntInt"
                && ffS2.Parameters.Length >= 2)
                ffS2.Parameters[1] = new EX_LocalVariable { Variable = addPtr };
        });

        // --- Build scan statements ---
        var s1 = counterZeroTpl;                                    // counter = 0
        var s2 = lenRead;                                           // len = Array_Length(Reverse)
        var s3 = lessTpl;                                           // less = Less(counter, len)   [loop head]
        var s4 = loopPopTpl;                                        // PopIfNot less -> pop flow (Return) when exhausted
        var s5 = getRead;                                           // stain = Array_Get(Reverse, counter)
        // notEqTpl is mutated in place for s6, so clone s8 BEFORE the rewrite, and force
        // the bytes unconditionally (the template byte may already be 7, which the old
        // 6||9 filter missed, leaving the notEmpty check comparing against 7 = Light).
        var s6 = (EX_LetBool)notEqTpl;                              // notLight = NotEqual(stain, 7)
        var s8 = (EX_LetBool)CloneKismet(notEqTpl);                 // notEmpty = NotEqual(stain, 0)
        bool rewrote6 = false, rewrote8 = false;
        Walk(new[] { s6 }, e => { if (e is EX_ByteConst bc && !rewrote6) { bc.Value = 7; rewrote6 = true; } });
        Walk(new[] { s8 }, e => { if (e is EX_ByteConst bc && !rewrote8) { bc.Value = 0; rewrote8 = true; } });
        var s7 = new EX_JumpIfNot { BooleanExpression = new EX_LocalVariable { Variable = notEqBoolPtr }, CodeOffset = 0 };
        var s9 = new EX_JumpIfNot { BooleanExpression = new EX_LocalVariable { Variable = notEqBoolPtr }, CodeOffset = 0 };
        if (consumeOnlyElement is not null)
        {
            // Lightning-only (Blitz): consume ONLY the target element. Replace the two
            // NotEqual guards with a single EqualEqual(stain, element): skip unless the
            // stain IS the element. Keeps Dark/Light/Empty in the queue.
            var eqIdx = LuneSkillsKit.EnsureKismetMathFn(asset, "EqualEqual_ByteByte");
            s6 = (EX_LetBool)CloneKismet(notEqTpl);
            // The template is captured from the FIRST NotEqual_ByteByte LetBool in the
            // function. For Blitz, PatchBlitz step 1 has already rewritten the original
            // consume guard to EqualEqual, so that template can come from a DIFFERENT
            // statement (e.g. the per-hit element check) whose result local is not the
            // one s7's JumpIfNot reads — leaving the guard testing a stale boolean
            // (the in-game consume crash with a full Lightning queue). Force the guard
            // result into the same local the jump reads.
            s6.VariableExpression = new EX_LocalVariable { Variable = notEqBoolPtr };
            Walk(new[] { s6 }, e =>
            {
                if (e is EX_FinalFunction ffE && ffE.StackNode.IsImport()
                    && ffE.StackNode.ToImport(asset).ObjectName.ToString() == "NotEqual_ByteByte")
                {
                    ffE.StackNode = eqIdx;
                    // First arg must be the scanned stain (Array_Get result), not
                    // whatever local the template happened to compare.
                    if (ffE.Parameters.Length >= 1)
                        ffE.Parameters[0] = new EX_LocalVariable { Variable = stainPtr };
                }
                if (e is EX_ByteConst bc && bc.Value == 7) bc.Value = consumeOnlyElement.Value;
            });
            s7 = new EX_JumpIfNot { BooleanExpression = new EX_LocalVariable { Variable = notEqBoolPtr }, CodeOffset = 0 };
            // s8/s9 are dropped for the single-guard case.
            s8 = null!; s9 = null!;
        }
        var s10 = consumedTpl;                                      // Local_StainConsumedElement = stain
        var s11 = addTpl;                                           // add = Add(counter, 1)
        var s12 = subTpl;                                           // sub = Subtract(len, add)  => index Len-1-counter
        var s13 = compTpl;                                          // comp2 = GetComponentByClass
        var s14 = consumeTpl;                                       // ConsumeStainAtIndex(sub, 1)
        var s15 = new EX_Jump { CodeOffset = 0 };                   // -> DONE (old stmt 47 CameraShake)
        var s16 = (KismetExpression)CloneKismet(addTpl);            // INC: add = Add(counter, 1)
        var s17 = counterAssignTpl;                                 // counter = add
        var s18 = new EX_Jump { CodeOffset = 0 };                   // -> loop head (s3)

        var newStmts = consumeOnlyElement is null
            ? new List<KismetExpression> { s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12, s13, s14, s15, s16, s17, s18 }
            : new List<KismetExpression> { s1, s2, s3, s4, s5, s6, s7, s10, s11, s12, s13, s14, s15, s16, s17, s18 };

        // --- Byte offsets ---
        uint insertOffset = 0;
        for (int i = 0; i < blockStart; i++) insertOffset += (uint)stmts[i].GetSize(asset);
        uint oldBlockSize = 0;
        for (int i = blockStart; i <= consumeIdx; i++) oldBlockSize += (uint)stmts[i].GetSize(asset);
        uint newBlockSize = 0;
        foreach (var ns in newStmts) newBlockSize += (uint)ns.GetSize(asset);
        uint delta = newBlockSize - oldBlockSize;

        uint offS3 = insertOffset + (uint)s1.GetSize(asset) + (uint)s2.GetSize(asset);
        // Skip-target = start of the increment (s16). The two-guard layout keeps s8/s9 so
        // s16 sits at index 15; the single-guard (consumeOnlyElement) layout drops s8/s9,
        // shifting s16 to index 13. A hardcoded "sum 15 statements" lands on s18 (the
        // loop-back jump) in the single-guard layout, so the guard's no-match path skips
        // the counter increment and spins forever on the same queue slot (in-game crash
        // when the queue has an Empty slot — i.e. fewer than 4 stains). Compute the
        // offset from s16's actual position in the list.
        uint offS16 = insertOffset;
        for (int i = 0; i < newStmts.Count; i++)
        {
            if (ReferenceEquals(newStmts[i], s16)) break;
            offS16 += (uint)newStmts[i].GetSize(asset);
        }
        uint doneOffset = insertOffset + newBlockSize; // original stmt 47 (CameraShake) now starts here

        s7.CodeOffset = offS16;
        if (s9 is not null) s9.CodeOffset = offS16;
        s15.CodeOffset = doneOffset;
        s18.CodeOffset = offS3;

        // Shift existing jumps/pushes pointing past the insertion point
        ShiftAbsolute(asset, fn, insertOffset, delta, null);

        stmts.RemoveRange(blockStart, consumeIdx - blockStart + 1);
        stmts.InsertRange(blockStart, newStmts);
        fn.ScriptBytecode = stmts.ToArray();
        System.Console.WriteLine($"[MayhemFix] scan consume: replaced {consumeIdx - blockStart + 1} stmts with {newStmts.Count} ({delta:+0;-0} bytes) at offset {insertOffset}");
        return 1;
    }



    private static void TintViolet(KismetExpression[] exprs)
    {
        Walk(exprs, e =>
        {
            if (e is EX_StructConst sc && sc.Value.Length == 4 && sc.Value.All(x => x is EX_FloatConst))
            {
                var f = sc.Value.Cast<EX_FloatConst>().ToList();
                float r = f[0].Value, g = f[1].Value, b = f[2].Value;
                if (Math.Abs(r - 1f) < 0.01f && Math.Abs(g - 0.958f) < 0.01f && Math.Abs(b - 0.916f) < 0.01f)
                { f[0].Value = 0.55f; f[1].Value = 0.15f; f[2].Value = 0.95f; }   // PowerColor -> violet
                else if (Math.Abs(r - 0.87f) < 0.01f && Math.Abs(g - 0.87f) < 0.01f && Math.Abs(b - 0.87f) < 0.01f)
                { f[0].Value = 0.25f; f[1].Value = 0.08f; f[2].Value = 0.50f; }   // Minimum_Color -> dark violet
                else if (Math.Abs(r - 1f) < 0.01f && Math.Abs(g - 1f) < 0.01f && Math.Abs(b - 1f) < 0.01f)
                { f[0].Value = 0.80f; f[1].Value = 0.40f; f[2].Value = 1.00f; }   // Maximum_Color -> bright violet
            }
        });
    }


    private static void InjectExplosionLightViolet(FunctionExport fx)
    {
        // Central FX_ExplosionLight has no color param originally -> white core remains.
        // We already injected PowerColor violet for it. The outer white ring is still
        // hardcoded in the Niagara system (not param-driven). Replace the system with
        // FX_RockImpact which respects PowerColor, and also add an extra outer ring
        // for the edge. This makes base+center+edge all violet.
        int spawnIdx = -1, powerIdx = -1;
        for (int i = 0; i < fx.ScriptBytecode.Length; i++)
        {
            if (fx.ScriptBytecode[i] is EX_LetObj lo)
            {
                bool isExplosionLight = false;
                Walk(new[] { lo.AssignmentExpression }, e => { if (e is EX_ObjectConst oc && oc.Value.IsImport() && oc.Value.ToImport(fx.Asset).ObjectName.ToString() == "FX_ExplosionLight") isExplosionLight = true; });
                if (isExplosionLight) spawnIdx = i;
            }
            if (fx.ScriptBytecode[i] is EX_Context ctx)
            {
                bool isPower = false;
                Walk(new[] { ctx }, e => { if (e is EX_NameConst nc && nc.Value.ToString() == "PowerColor") isPower = true; });
                if (isPower && powerIdx == -1) powerIdx = i;
            }
        }
        if (spawnIdx < 0 || powerIdx < 0) return;

        // 1) Swap FX_ExplosionLight -> FX_RockImpact (tintable) for the central flash
        if (fx.ScriptBytecode[spawnIdx] is EX_LetObj letObj)
        {
            Walk(new[] { letObj.AssignmentExpression }, e =>
            {
                if (e is EX_ObjectConst oc && oc.Value.IsImport() && oc.Value.ToImport(fx.Asset).ObjectName.ToString() == "FX_ExplosionLight")
                {
                    // Find FX_RockImpact import
                    for (int k = 0; k < fx.Asset.Imports.Count; k++) if (fx.Asset.Imports[k].ObjectName.ToString() == "FX_RockImpact") { oc.Value = FPackageIndex.FromImport(k); break; }
                }
            });
        }

        // 2) Ensure PowerColor exists for this (now RockImpact) spawn - inject if missing
        int sizeIdx = -1;
        for (int i = spawnIdx + 1; i < fx.ScriptBytecode.Length && i < spawnIdx + 4; i++)
        {
            if (fx.ScriptBytecode[i] is EX_Context ctx2)
            {
                bool isSize = false;
                bool targetsExplosion = false;
                Walk(new[] { ctx2 }, e => { if (e is EX_NameConst nc && nc.Value.ToString() == "Size") isSize = true; });
                if (ctx2 is EX_Context ec && ec.ObjectExpression is EX_LocalVariable lv && lv.Variable?.New?.Path?[0]?.ToString() == "CallFunc_SpawnSystemAtLocation_ReturnValue_2") targetsExplosion = true;
                if (isSize && targetsExplosion) { sizeIdx = i; break; }
            }
        }
        bool hasPower = false;
        if (sizeIdx >= 0)
        {
            for (int i = sizeIdx + 1; i < fx.ScriptBytecode.Length && i < sizeIdx + 3; i++)
                if (fx.ScriptBytecode[i] is EX_Context c2) Walk(new[] { c2 }, e => { if (e is EX_NameConst nc && nc.Value.ToString() == "PowerColor") hasPower = true; });
            if (!hasPower && powerIdx >= 0)
            {
                var powerClone = (EX_Context)CloneKismet(fx.ScriptBytecode[powerIdx]);
                if (fx.ScriptBytecode[sizeIdx] is EX_Context sizeCtx && sizeCtx.ObjectExpression is EX_LocalVariable sizeVar)
                    powerClone.ObjectExpression = (EX_LocalVariable)CloneKismet(sizeVar);
                var list = fx.ScriptBytecode.ToList();
                list.Insert(sizeIdx + 1, powerClone);
                fx.ScriptBytecode = list.ToArray();
            }
        }

        // 3) Enlarge the central a bit so the outer ring reads as dark violet edge
        for (int i = 0; i < fx.ScriptBytecode.Length; i++)
        {
            if (fx.ScriptBytecode[i] is EX_Context ctx && ctx.ObjectExpression is EX_LocalVariable lv && lv.Variable?.New?.Path?[0]?.ToString() == "CallFunc_SpawnSystemAtLocation_ReturnValue_2")
            {
                bool isSizeCtx = false;
                Walk(new[] { ctx }, e => { if (e is EX_NameConst nc && nc.Value.ToString() == "Size") isSizeCtx = true; });
                if (isSizeCtx) Walk(new[] { ctx }, e => { if (e is EX_FloatConst fc && Math.Abs(fc.Value - 2f) < 0.01f) fc.Value = 2.6f; });
            }
        }
        // 4) Remove the rocky base entirely (first FX_RockImpact at Location) - Dark shouldn't be earthy.
        // The base is LetObj[0] + Context PowerColor[1] + Context Size[2] + Context LittleMeshSize[3].
        // Delete those 4 exprs so only violet center (now RockImpact) + smoke remain.
        if (fx.ScriptBytecode.Length >= 4 && fx.ScriptBytecode[0] is EX_LetObj lo0)
        {
            bool isRockBase = false;
            Walk(new[] { lo0.AssignmentExpression }, e => { if (e is EX_ObjectConst oc && oc.Value.IsImport() && oc.Value.ToImport(fx.Asset).ObjectName.ToString() == "FX_RockImpact") isRockBase = true; });
            if (isRockBase)
            {
                var lst = fx.ScriptBytecode.ToList();
                lst.RemoveRange(0, 4);
                fx.ScriptBytecode = lst.ToArray();
            }
        }

    }

    internal static KismetExpression CloneKismet(KismetExpression e)
    {
        // Deep clone via UAssetAPI's Clone helpers (simple reflection)
        var t = e.GetType();
        var n = (KismetExpression)System.Activator.CreateInstance(t)!;
        const System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        foreach (var f in t.GetFields(bf))
        {
            var v = f.GetValue(e);
            if (v is KismetExpression ke) f.SetValue(n, CloneKismet(ke));
            else if (v is KismetExpression[] arr) f.SetValue(n, arr.Select(CloneKismet).ToArray());
            else f.SetValue(n, v);
        }
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_FloatConst fc && n is UAssetAPI.Kismet.Bytecode.Expressions.EX_FloatConst nfc) nfc.Value = fc.Value;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_ByteConst bc && n is UAssetAPI.Kismet.Bytecode.Expressions.EX_ByteConst nbc) nbc.Value = bc.Value;
        if (e is UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst dc && n is UAssetAPI.Kismet.Bytecode.Expressions.EX_DoubleConst ndc) ndc.Value = dc.Value;
        return n;
    }

    /// <summary>Depth-first walk of kismet expressions (public for CLI smoke audits).</summary>
    public static void Walk(IEnumerable<KismetExpression> exprs, Action<KismetExpression> visit)
    {
        foreach (var e in exprs)
        {
            if (e is null) continue;
            visit(e);
            foreach (var f in e.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
            {
                var v = f.GetValue(e);
                if (v is KismetExpression ke) Walk(new[] { ke }, visit);
                else if (v is KismetExpression[] arr) Walk(arr, visit);
            }
            foreach (var p in e.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                if (p.Name is "Token" or "Inst" or "RawValue" or "Tag") continue;
                object? v;
                try { v = p.GetValue(e); } catch { continue; }
                if (v is KismetExpression ke) Walk(new[] { ke }, visit);
                else if (v is KismetExpression[] arr) Walk(arr, visit);
            }
        }
    }

    /// <summary>True when <paramref name="expr"/> or any expression nested inside
    /// it satisfies <paramref name="pred"/>. Used to assert a cloned subtree
    /// carries no reference into the function it was cloned from.</summary>
    public static bool WalkHas(KismetExpression expr, Predicate<KismetExpression> pred, out KismetExpression? found)
    {
        found = null;
        if (expr is null) return false;
        if (pred(expr)) { found = expr; return true; }
        return HasMatchingChild(expr, pred, out found);
    }

    public static bool WalkHas(KismetExpression expr, Predicate<KismetExpression> pred)
        => WalkHas(expr, pred, out _);

    public static bool WalkHas(IEnumerable<KismetExpression> exprs, Predicate<KismetExpression> pred)
    {
        foreach (var e in exprs) if (e is not null && WalkHas(e, pred, out _)) return true;
        return false;
    }

    private static bool HasMatchingChild(KismetExpression e, Predicate<KismetExpression> pred, out KismetExpression? found)
    {
        foreach (var child in Children(e))
            if (WalkHas(child, pred, out found)) return true;
        found = null;
        return false;
    }

    /// <summary>Direct expression children of a node: fields first, then
    /// properties, mirroring the traversal order of <see cref="Walk"/>.</summary>
    private static IEnumerable<KismetExpression> Children(KismetExpression e)
    {
        foreach (var f in e.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
        {
            object? v;
            try { v = f.GetValue(e); } catch { continue; }
            if (v is KismetExpression ke) yield return ke;
            else if (v is KismetExpression[] arr) foreach (var a in arr) if (a is not null) yield return a;
        }
        foreach (var p in e.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            if (p.GetIndexParameters().Length > 0) continue;
            if (p.Name is "Token" or "Inst" or "RawValue" or "Tag") continue;
            object? v;
            try { v = p.GetValue(e); } catch { continue; }
            if (v is KismetExpression ke) yield return ke;
            else if (v is KismetExpression[] arr) foreach (var a in arr) if (a is not null) yield return a;
        }
    }

    internal static void ShiftAbsolute(UAsset asset, FunctionExport fn, uint insertionPoint, uint delta, EX_SwitchValue? except)
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
                    case EX_SwitchValue sv when !ReferenceEquals(sv, except):
                        if (sv.EndGotoOffset > insertionPoint) sv.EndGotoOffset += delta;
                        for (int i = 0; i < sv.Cases.Length; i++)
                            if (sv.Cases[i].NextOffset > insertionPoint) sv.Cases[i].NextOffset += delta;
                        break;
                }
            });
    }

    internal static void RecalcContextSkips(UAsset asset, FunctionExport fn)
    {
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref pos, (e, _) => { if (e is EX_Context ctx) ctx.Offset = ctx.ContextExpression.GetSize(asset); });
    }

    private static void WriteAsset(UAsset asset, string path)
    {
        try { asset.Write(path); } catch (FormatException ex) { throw new FormatException(Path.GetFileName(path) + ": " + ex.Message, ex); }
    }
}

using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Enforces a real maximum on the total enemy Burn stack count.
///
/// <para>Burn is an aggregator buff: every application creates a new
/// <c>BP_BattleBuff_Burn</c> child instance, and
/// <c>BP_BattleBuffInstance_AggregatorBase.ComputeAggregatorStackCount</c> sums
/// every child's <c>StackCount</c> for the HUD, damage and consumption paths.
/// Capping each child individually therefore is not enough — several children
/// can still add up to more than the cap. This kit caps the combined total.</para>
///
/// <para>Both Burn-specific events run the same total-cap block:</para>
/// <list type="bullet">
/// <item><c>OnChildBuffInstanceAdded</c> catches the common skill pattern
/// <c>SpawnObject → ChangeStackCount(N) → ActivateBuff</c>, where a new child
/// arrives already stacked and the stack-change delegate is not bound yet.</item>
/// <item><c>OnChildBuffInstanceStackChanged</c> catches later changes.</item>
/// </list>
///
/// <code>
/// team = TargetCharacterStats.GetTeam()
/// if team == player then continue with vanilla refresh
/// total = sum(child.GetStackCount() for child in ChildBuffInstances)
/// i = ChildBuffInstances.Length - 1
/// while i &gt;= 0 and total &gt; cap:
///     child = ChildBuffInstances[i]
///     stacks = child.GetStackCount()
///     without = total - stacks
///     if without &gt;= cap:
///         child.StackCount = 0
///         total = without
///     else:
///         child.StackCount = cap - without
///         total = cap
///     i--
/// refreshBurnCounterWidget()
/// </code>
///
/// <para>The reductions write <c>StackCount</c> directly with
/// <c>KismetSystemLibrary.SetIntPropertyByName</c> instead of calling
/// <c>ChangeStackCount</c>. That avoids re-entering the stack-changed event while
/// still leaving the real instance value, damage, consumption and HUD in sync;
/// the event explicitly refreshes the Burn counter after the writes.</para>
/// </summary>
public static class BurnStackCapKit
{
    public const string AggregatorStem = "BP_BattleBuff_BurnAggregator";
    public const int MaxCap = 999;

    private const string StackChangedFunction = "OnChildBuffInstanceStackChanged";
    private const string AddedFunction = "OnChildBuffInstanceAdded";
    private const string IndexLocal = "E33_BurnCap_Index";
    private const string CountLocal = "E33_BurnCap_Count";
    private const string TeamLocal = "E33_BurnCap_Team";
    private const string ChildLocal = "E33_BurnCap_Child";
    private const string StacksLocal = "E33_BurnCap_Stacks";
    private const string TotalLocal = "E33_BurnCap_Total";
    private const string WithoutLocal = "E33_BurnCap_Without";
    private const int ClampBlockCount = 25;
    private const int LegacyClampBlockCount = 11;

    public static int Apply(string modDir, int cap)
    {
        cap = Math.Clamp(cap, 1, MaxCap);
        var path = GameplayTweaksKit.FindAsset(modDir, AggregatorStem + ".uasset")
            ?? CopyIntoMod(modDir);
        var asset = Open(path);
        var stackChanged = FindFunction(asset, StackChangedFunction);
        var added = FindFunction(asset, AddedFunction);

        BuildStackChanged(asset, stackChanged, cap);
        BuildAdded(asset, added, cap);
        MayhemFixKit.RecalcContextSkips(asset, stackChanged);
        MayhemFixKit.RecalcContextSkips(asset, added);
        PartyFiveKit.VerifyJumps(asset, stackChanged);
        PartyFiveKit.VerifyJumps(asset, added);
        asset.Write(path);
        if (!Verify(path, cap))
            throw new InvalidDataException($"[burncap] verification failed for {Path.GetFileName(path)}");
        Console.WriteLine($"[burncap] {Path.GetFileName(path)}: total enemy Burn stacks capped at {cap}");
        return 1;
    }

    private static FunctionExport FindFunction(UAsset asset, string name) =>
        asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == name)
        ?? throw new InvalidDataException($"[burncap] {name} not found");

    private sealed class ClampBlock
    {
        public required List<KismetExpression> Statements { get; init; }
        public required EX_JumpIfNot PlayerGate { get; init; }
        public required EX_JumpIfNot SumGate { get; init; }
        public required EX_JumpIfNot ReduceLoopGate { get; init; }
        public required EX_JumpIfNot TotalGate { get; init; }
        public required EX_JumpIfNot FullRemoveGate { get; init; }
        public required EX_Jump SumJump { get; init; }
        public required EX_Jump JumpToDecrement { get; init; }
        public required EX_Jump ReduceJump { get; init; }
    }

    private static void BuildStackChanged(UAsset asset, FunctionExport fn, int cap)
    {
        var block = BuildClampBlock(asset, fn, cap);
        var body = new List<KismetExpression>(block.Statements)
        {
            new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(asset, "RefreshBurnCounterWidget"),
                Parameters = Array.Empty<KismetExpression>(),
            },
            new EX_Return { ReturnExpression = new EX_Nothing() },
            new EX_EndOfScript(),
        };
        fn.ScriptBytecode = body.ToArray();

        var starts = PartyFiveKit.StatementStarts(asset, body);
        PointGates(block, starts, doneIndex: block.Statements.Count);
    }

    /// <summary>
    /// Inserts the total-cap block in front of the vanilla "child added" body
    /// (refresh + feedback). This catches stacks assigned before
    /// <c>ActivateBuff</c> binds the child's stack-change delegate.
    /// </summary>
    private static void BuildAdded(UAsset asset, FunctionExport fn, int cap)
    {
        var block = BuildClampBlock(asset, fn, cap);
        var stmts = (fn.ScriptBytecode ?? Array.Empty<KismetExpression>()).ToList();

        // Idempotent re-apply: remove the previous clamp block before inserting
        // the new cap. Vanilla's added-event tail has no jumps, but shift any
        // absolute targets anyway so this stays safe if the graph changes.
        if (stmts.Count >= 2
            && stmts[0] is EX_Let first
            && PartyFiveKit.FieldName(first.Value) == IndexLocal)
        {
            // Current blocks start with index=0, total=0. The first version of
            // this kit used index=0, count=length and had 11 statements.
            var oldCount = stmts[1] is EX_Let second
                && PartyFiveKit.FieldName(second.Value) == TotalLocal
                ? ClampBlockCount
                : LegacyClampBlockCount;
            if (stmts.Count < oldCount)
                throw new InvalidDataException("[burncap] malformed existing clamp block");
            uint oldSize = 0;
            for (var i = 0; i < oldCount; i++)
                oldSize += (uint)stmts[i].GetSize(asset);
            fn.ScriptBytecode = stmts.ToArray();
            LuneSkillsKit.ShiftAbsoluteSigned(asset, fn, 0, oldSize);
            stmts.RemoveRange(0, oldCount);
        }

        uint blockSize = 0;
        foreach (var s in block.Statements)
            blockSize += (uint)s.GetSize(asset);

        fn.ScriptBytecode = stmts.ToArray();
        ShiftOffsetsAtOrAfter(asset, fn, 0, blockSize);
        stmts.InsertRange(0, block.Statements);
        fn.ScriptBytecode = stmts.ToArray();

        var starts = PartyFiveKit.StatementStarts(asset, stmts);
        PointGates(block, starts, doneIndex: block.Statements.Count);
    }

    private static void PointGates(ClampBlock block, List<uint> starts, int doneIndex)
    {
        // Block layout:
        // 0 index=0, 1 total=0, 2 count, 3 team, 4 player gate,
        // 5 sum gate, 6 get child, 7 get stacks, 8 total+=stacks,
        // 9 index++, 10 sum jump, 11 index=count-1,
        // 12 reduce gate, 13 total gate, 14 get child, 15 get stacks,
        // 16 without=total-stacks, 17 full-remove gate,
        // 18 StackCount=0, 19 total=without, 20 jump to decrement,
        // 21 keep path, 22 total=cap, 23 index--, 24 reduce jump,
        // then the caller's tail.
        var doneOffset = starts[doneIndex];
        block.PlayerGate.CodeOffset = doneOffset;
        block.ReduceLoopGate.CodeOffset = doneOffset;
        block.TotalGate.CodeOffset = doneOffset;
        block.SumGate.CodeOffset = starts[11];
        block.SumJump.CodeOffset = starts[5];
        block.FullRemoveGate.CodeOffset = starts[21];
        block.JumpToDecrement.CodeOffset = starts[23];
        block.ReduceJump.CodeOffset = starts[12];

        if (block.PlayerGate.CodeOffset == 0xFFFFFFFE
            || block.SumGate.CodeOffset == 0xFFFFFFFE
            || block.SumJump.CodeOffset == 0xFFFFFFFE
            || block.ReduceLoopGate.CodeOffset == 0xFFFFFFFE
            || block.TotalGate.CodeOffset == 0xFFFFFFFE
            || block.FullRemoveGate.CodeOffset == 0xFFFFFFFE
            || block.JumpToDecrement.CodeOffset == 0xFFFFFFFE
            || block.ReduceJump.CodeOffset == 0xFFFFFFFE)
            throw new InvalidDataException("[burncap] unresolved jump target");
    }

    private static ClampBlock BuildClampBlock(UAsset asset, FunctionExport fn, int cap)
    {
        var indexPtr = AddLocal(asset, fn, IndexLocal, IntProp(asset));
        var countPtr = AddLocal(asset, fn, CountLocal, IntProp(asset));
        var teamPtr = AddLocal(asset, fn, TeamLocal, ByteProp(asset));
        var stacksPtr = AddLocal(asset, fn, StacksLocal, IntProp(asset));
        var totalPtr = AddLocal(asset, fn, TotalLocal, IntProp(asset));
        var withoutPtr = AddLocal(asset, fn, WithoutLocal, IntProp(asset));
        var childPtr = AddLocal(asset, fn, ChildLocal, ChildProp(asset));

        var childInstancesPtr = FindInstancePtr(asset, "ChildBuffInstances");
        var targetStatsPtr = FindInstancePtr(asset, "TargetCharacterStats");
        var arrayLibCdo = EnsureArrayLibraryCdo(asset);
        var arrayGet = GameplayTweaksKit.EnsureEngineFunction(asset, "KismetArrayLibrary", "Array_Get");
        var arrayLength = GameplayTweaksKit.EnsureEngineFunction(asset, "KismetArrayLibrary", "Array_Length");
        var less = GameplayTweaksKit.EnsureEngineFunction(asset, "KismetMathLibrary", "Less_IntInt");
        var greater = GameplayTweaksKit.EnsureEngineFunction(asset, "KismetMathLibrary", "Greater_IntInt");
        var greaterEqual = GameplayTweaksKit.EnsureEngineFunction(asset, "KismetMathLibrary", "GreaterEqual_IntInt");
        var add = GameplayTweaksKit.EnsureEngineFunction(asset, "KismetMathLibrary", "Add_IntInt");
        var subtract = GameplayTweaksKit.EnsureEngineFunction(asset, "KismetMathLibrary", "Subtract_IntInt");
        var notEqualByte = GameplayTweaksKit.EnsureEngineFunction(asset, "KismetMathLibrary", "NotEqual_ByteByte");
        var setIntByName = GameplayTweaksKit.EnsureEngineFunction(asset, "KismetSystemLibrary", "SetIntPropertyByName");

        EX_LocalVariable L(KismetPropertyPointer p) => new() { Variable = p };
        EX_InstanceVariable Inst(KismetPropertyPointer p) => new() { Variable = ClonePtr(asset, p) };
        EX_Context ChildAt() => new()
        {
            ObjectExpression = new EX_ObjectConst { Value = arrayLibCdo },
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            ContextExpression = new EX_FinalFunction
            {
                StackNode = arrayGet,
                Parameters = new KismetExpression[]
                {
                    Inst(childInstancesPtr),
                    L(indexPtr),
                    L(childPtr),
                },
            },
        };
        EX_Context ReadStacks() => new()
        {
            ObjectExpression = L(childPtr),
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            ContextExpression = new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(asset, "GetStackCount"),
                Parameters = new KismetExpression[] { L(stacksPtr) },
            },
        };
        EX_FinalFunction SetStacks(KismetExpression value) => new()
        {
            StackNode = setIntByName,
            Parameters = new KismetExpression[]
            {
                L(childPtr),
                new EX_NameConst { Value = FName.FromString(asset, "StackCount") },
                value,
            },
        };
        EX_CallMath Math(FPackageIndex fnIndex, params KismetExpression[] args) => new()
        {
            StackNode = fnIndex,
            Parameters = args,
        };

        var indexZero = new EX_Let
        {
            Value = indexPtr,
            Variable = L(indexPtr),
            Expression = new EX_IntConst { Value = 0 },
        };
        var totalZero = new EX_Let
        {
            Value = totalPtr,
            Variable = L(totalPtr),
            Expression = new EX_IntConst { Value = 0 },
        };

        var childCount = new EX_Context
        {
            ObjectExpression = new EX_ObjectConst { Value = arrayLibCdo },
            RValuePointer = countPtr,
            ContextExpression = new EX_FinalFunction
            {
                StackNode = arrayLength,
                Parameters = new KismetExpression[] { Inst(childInstancesPtr) },
            },
        };
        var countLet = new EX_Let
        {
            Value = countPtr,
            Variable = L(countPtr),
            Expression = childCount,
        };

        var getTeam = new EX_Context
        {
            ObjectExpression = Inst(targetStatsPtr),
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            ContextExpression = new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(asset, "GetTeam"),
                Parameters = new KismetExpression[] { L(teamPtr) },
            },
        };
        var teamLet = new EX_Let
        {
            Value = teamPtr,
            Variable = L(teamPtr),
            Expression = getTeam,
        };
        var playerGate = new EX_JumpIfNot
        {
            CodeOffset = 0,
            BooleanExpression = Math(notEqualByte, L(teamPtr), new EX_ByteConst { Value = 1 }),
        };

        var sumGate = new EX_JumpIfNot
        {
            CodeOffset = 0,
            BooleanExpression = Math(less, L(indexPtr), L(countPtr)),
        };
        var addTotal = new EX_Let
        {
            Value = totalPtr,
            Variable = L(totalPtr),
            Expression = Math(add, L(totalPtr), L(stacksPtr)),
        };
        var sumIncrement = new EX_Let
        {
            Value = indexPtr,
            Variable = L(indexPtr),
            Expression = Math(add, L(indexPtr), new EX_IntConst { Value = 1 }),
        };
        var sumJump = new EX_Jump { CodeOffset = 0 };

        var reduceStart = new EX_Let
        {
            Value = indexPtr,
            Variable = L(indexPtr),
            Expression = Math(subtract, L(countPtr), new EX_IntConst { Value = 1 }),
        };
        var reduceLoopGate = new EX_JumpIfNot
        {
            CodeOffset = 0,
            BooleanExpression = Math(less, L(indexPtr), new EX_IntConst { Value = 0 }),
        };
        var totalGate = new EX_JumpIfNot
        {
            CodeOffset = 0,
            BooleanExpression = Math(greater, L(totalPtr), new EX_IntConst { Value = cap }),
        };
        var withoutLet = new EX_Let
        {
            Value = withoutPtr,
            Variable = L(withoutPtr),
            Expression = Math(subtract, L(totalPtr), L(stacksPtr)),
        };
        var fullRemoveGate = new EX_JumpIfNot
        {
            CodeOffset = 0,
            BooleanExpression = Math(greaterEqual, L(withoutPtr), new EX_IntConst { Value = cap }),
        };
        var setZero = SetStacks(new EX_IntConst { Value = 0 });
        var totalSetWithout = new EX_Let
        {
            Value = totalPtr,
            Variable = L(totalPtr),
            Expression = L(withoutPtr),
        };
        var jumpToDecrement = new EX_Jump { CodeOffset = 0 };

        var setKeep = SetStacks(Math(subtract,
            new EX_IntConst { Value = cap }, L(withoutPtr)));
        var totalSetCap = new EX_Let
        {
            Value = totalPtr,
            Variable = L(totalPtr),
            Expression = new EX_IntConst { Value = cap },
        };
        var decrement = new EX_Let
        {
            Value = indexPtr,
            Variable = L(indexPtr),
            Expression = Math(subtract, L(indexPtr), new EX_IntConst { Value = 1 }),
        };
        var reduceJump = new EX_Jump { CodeOffset = 0 };

        return new ClampBlock
        {
            Statements = new List<KismetExpression>
            {
                indexZero,          // 0
                totalZero,          // 1
                countLet,           // 2
                teamLet,            // 3
                playerGate,         // 4 -> done
                sumGate,            // 5 -> reduceStart
                ChildAt(),          // 6
                ReadStacks(),       // 7
                addTotal,           // 8
                sumIncrement,       // 9
                sumJump,            // 10 -> sumGate
                reduceStart,        // 11
                reduceLoopGate,     // 12 -> done
                totalGate,          // 13 -> done
                ChildAt(),          // 14
                ReadStacks(),       // 15
                withoutLet,         // 16
                fullRemoveGate,     // 17 -> setKeep
                setZero,            // 18
                totalSetWithout,    // 19
                jumpToDecrement,    // 20 -> decrement
                setKeep,            // 21
                totalSetCap,        // 22
                decrement,          // 23
                reduceJump,         // 24 -> reduceLoopGate
            },
            PlayerGate = playerGate,
            SumGate = sumGate,
            ReduceLoopGate = reduceLoopGate,
            TotalGate = totalGate,
            FullRemoveGate = fullRemoveGate,
            SumJump = sumJump,
            JumpToDecrement = jumpToDecrement,
            ReduceJump = reduceJump,
        };
    }

    public static bool Verify(string path, int cap)
    {
        try
        {
            var asset = Open(path);
            var stackChanged = FindFunction(asset, StackChangedFunction);
            var added = FindFunction(asset, AddedFunction);
            bool stackChangedOk = VerifyFunction(asset, stackChanged, cap);
            bool addedOk = VerifyFunction(asset, added, cap);
            PartyFiveKit.VerifyJumps(asset, stackChanged);
            PartyFiveKit.VerifyJumps(asset, added);
            Console.WriteLine($"[burncap] verify: changed={stackChangedOk} added={addedOk}");
            return stackChangedOk && addedOk;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[burncap] verify failed: {ex.Message}");
            return false;
        }
    }

    private static bool VerifyFunction(UAsset asset, FunctionExport fn, int cap)
    {
        if (fn.ScriptBytecode is null) return false;
        var locals = (fn.LoadedProperties ?? Array.Empty<FProperty>())
            .Select(p => p.Name.ToString())
            .ToHashSet(StringComparer.Ordinal);
        bool hasLocals = locals.Contains(IndexLocal) && locals.Contains(CountLocal)
            && locals.Contains(TeamLocal) && locals.Contains(ChildLocal)
            && locals.Contains(StacksLocal) && locals.Contains(TotalLocal)
            && locals.Contains(WithoutLocal);

        bool hasCap = false, hasGetTeam = false, hasGetStacks = false;
        bool hasSetStacks = false, readsStackCount = false, hasRefresh = false;
        foreach (var st in fn.ScriptBytecode)
            MayhemFixKit.Walk(new[] { st }, e =>
            {
                switch (e)
                {
                    case EX_IntConst ic when ic.Value == cap:
                        hasCap = true;
                        break;
                    case EX_NameConst nc when nc.Value.ToString() == "StackCount":
                        readsStackCount = true;
                        break;
                    case EX_LocalVirtualFunction vf:
                        var n = vf.VirtualFunctionName.ToString();
                        if (n == "GetTeam") hasGetTeam = true;
                        if (n == "GetStackCount") hasGetStacks = true;
                        if (n == "RefreshBurnCounterWidget") hasRefresh = true;
                        break;
                    case EX_FinalFunction ff when ff.StackNode is { } sn && sn.IsImport():
                        if (asset.Imports[Math.Abs(sn.Index) - 1].ObjectName.ToString()
                            == "SetIntPropertyByName")
                            hasSetStacks = true;
                        break;
                }
            });

        // Verify the block's internal jump graph explicitly. The generic
        // "target is a statement start" check cannot catch a jump accidentally
        // left at offset 0, which is itself a valid statement.
        var stmts = fn.ScriptBytecode;
        var starts = PartyFiveKit.StatementStarts(asset, stmts);
        bool layout = stmts.Length >= ClampBlockCount
            && stmts[0] is EX_Let first
            && PartyFiveKit.FieldName(first.Value) == IndexLocal
            && stmts[1] is EX_Let second
            && PartyFiveKit.FieldName(second.Value) == TotalLocal
            && stmts[4] is EX_JumpIfNot g4 && g4.CodeOffset == starts[ClampBlockCount]
            && stmts[5] is EX_JumpIfNot g5 && g5.CodeOffset == starts[11]
            && stmts[10] is EX_Jump j10 && j10.CodeOffset == starts[5]
            && stmts[12] is EX_JumpIfNot g12 && g12.CodeOffset == starts[ClampBlockCount]
            && stmts[13] is EX_JumpIfNot g13 && g13.CodeOffset == starts[ClampBlockCount]
            && stmts[17] is EX_JumpIfNot g17 && g17.CodeOffset == starts[21]
            && stmts[20] is EX_Jump j20 && j20.CodeOffset == starts[23]
            && stmts[24] is EX_Jump j24 && j24.CodeOffset == starts[12];

        return hasLocals && hasCap && hasGetTeam && hasGetStacks
            && hasSetStacks && readsStackCount && hasRefresh && layout;
    }

    /// <summary>Insert fixup with at-or-after semantics. An insertion exactly
    /// at an existing target must move that target too.</summary>
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
                        for (var i = 0; i < sv.Cases.Length; i++)
                            if (sv.Cases[i].NextOffset >= point) sv.Cases[i].NextOffset += delta;
                        break;
                }
            });
    }

    private static FGenericProperty IntProp(UAsset asset) => new()
    {
        SerializedType = FName.FromString(asset, "IntProperty"),
        ElementSize = 4,
    };

    private static FByteProperty ByteProp(UAsset asset) => new()
    {
        SerializedType = FName.FromString(asset, "ByteProperty"),
        ElementSize = 1,
        Enum = FPackageIndex.FromRawIndex(0),
    };

    private static FObjectProperty ChildProp(UAsset asset)
    {
        var idx = FindImportIndex(asset, "BP_BattleBuffInstance_C")
            ?? throw new InvalidDataException("[burncap] BP_BattleBuffInstance_C import missing");
        return new FObjectProperty
        {
            SerializedType = FName.FromString(asset, "ObjectProperty"),
            ElementSize = 8,
            PropertyClass = FPackageIndex.FromImport(idx),
        };
    }

    private static KismetPropertyPointer AddLocal(
        UAsset asset, FunctionExport fn, string name, FProperty prop)
    {
        var props = (fn.LoadedProperties ?? Array.Empty<FProperty>()).ToList();
        var existing = props.FirstOrDefault(p => p.Name.ToString() == name);
        if (existing is not null)
        {
            NormalizeArrayDim(existing);
            fn.LoadedProperties = props.ToArray();
            return LocalPtr(asset, fn, name);
        }

        prop.Name = FName.FromString(asset, name);
        prop.PropertyFlags = EPropertyFlags.CPF_None;
        prop.RepIndex = 0;
        NormalizeArrayDim(prop);
        props.Add(prop);
        fn.LoadedProperties = props.ToArray();
        return LocalPtr(asset, fn, name);
    }

    private static void NormalizeArrayDim(FProperty prop)
    {
        prop.ArrayDim = EArrayDim.TArray;
        if (prop is FArrayProperty array && array.Inner is not null)
            NormalizeArrayDim(array.Inner);
    }

    private static KismetPropertyPointer LocalPtr(UAsset asset, FunctionExport fn, string name)
    {
        var owner = FPackageIndex.FromExport(asset.Exports.IndexOf(fn));
        return new KismetPropertyPointer(new FFieldPath(
            new[] { FName.FromString(asset, name) }, owner));
    }

    private static KismetPropertyPointer FindInstancePtr(UAsset asset, string field)
    {
        KismetPropertyPointer? found = null;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            foreach (var st in fn.ScriptBytecode ?? Array.Empty<KismetExpression>())
                MayhemFixKit.Walk(new[] { st }, e =>
                {
                    if (found is null && e is EX_InstanceVariable iv
                        && PartyFiveKit.FieldName(iv.Variable) == field)
                        found = iv.Variable;
                });
            if (found is not null) break;
        }
        return found ?? throw new InvalidDataException($"[burncap] instance field {field} not found");
    }

    private static KismetPropertyPointer ClonePtr(UAsset asset, KismetPropertyPointer src)
    {
        if (src.New is null)
            throw new InvalidDataException("[burncap] legacy property pointer");
        var path = (src.New.Path ?? Array.Empty<FName>())
            .Select(n => FName.FromString(asset, n.ToString()))
            .ToArray();
        return new KismetPropertyPointer(new FFieldPath(path, src.New.ResolvedOwner));
    }

    private static int? FindImportIndex(UAsset asset, string objectName)
    {
        for (var i = 0; i < asset.Imports.Count; i++)
            if (asset.Imports[i].ObjectName.ToString() == objectName)
                return i;
        return null;
    }

    private static FPackageIndex EnsureArrayLibraryCdo(UAsset asset)
    {
        var existing = FindImportIndex(asset, "Default__KismetArrayLibrary");
        if (existing is not null) return FPackageIndex.FromImport(existing.Value);

        FPackageIndex? enginePkg = null;
        for (var i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (imp.ObjectName.ToString() == "/Script/Engine"
                && imp.ClassName.ToString() == "Package")
            {
                enginePkg = FPackageIndex.FromImport(i);
                break;
            }
        }
        enginePkg ??= asset.AddImport(new Import("/Script/CoreUObject", "Package",
            FPackageIndex.FromRawIndex(0), "/Script/Engine", false, asset));
        return asset.AddImport(new Import("/Script/Engine", "KismetArrayLibrary",
            enginePkg, "Default__KismetArrayLibrary", false, asset));
    }

    private static UAsset Open(string path)
    {
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
    }

    private static string CopyIntoMod(string modDir)
    {
        var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "burn_stack_cap");
        var src = FindAsset(dir, AggregatorStem + ".uasset")
            ?? FindAsset(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), AggregatorStem + ".uasset");
        if (src is null)
        {
            Directory.CreateDirectory(dir);
            using (E33ModEditor.Core.Packer.DonorExtract.SuspendMods())
                new Retoc().ToLegacy(dir, AggregatorStem);
            src = FindAsset(dir, AggregatorStem + ".uasset")
                ?? throw new FileNotFoundException(AggregatorStem + ".uasset");
        }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) throw new InvalidDataException(AggregatorStem + " is not under Sandfall/");
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

using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Enforces a real maximum on enemy Burn stacks, not just a clamp in the damage
/// readout. Vanilla has no max-stack field: <c>BP_BattleBuff_Burn</c> only
/// carries <c>BurnMultiplier</c> / <c>TurnDuration</c>, and
/// <c>BP_BattleBuffInstance.ChangeStackCount</c> stores the integer directly.
/// The Burn-specific choke points are the aggregator's child-added and
/// child-stack-changed events:
///
/// <list type="bullet">
/// <item><c>OnChildBuffInstanceAdded</c> catches the common skill pattern
/// <c>SpawnObject → ChangeStackCount(N) → ActivateBuff</c>, where the new child
/// arrives with N stacks already set and the stack-change delegate is not bound
/// yet.</item>
/// <item><c>OnChildBuffInstanceStackChanged</c> catches every later stack
/// update. This is the delegate each child Burn instance invokes from
/// <c>BP_BattleBuffInstance.ChangeStackCount</c>.</item>
/// </list>
///
/// <para>Each event is rebuilt to:</para>
/// <code>
/// team = TargetCharacterStats.GetTeam()
/// if team == player then continue with vanilla refresh
/// i = 0
/// while i &lt; ChildBuffInstances.Length:
///     child = ChildBuffInstances[i]
///     stacks = child.GetStackCount()
///     if cap &lt; stacks: child.ChangeStackCount(cap)
///     i++
/// </code>
///
/// <para>The re-entrant <c>ChangeStackCount</c> call fires the stack-changed
/// event again; the second pass sees <c>stacks == cap</c>, so it only refreshes
/// the HUD. This keeps the cap on the actual child instance seen by damage,
/// consumption and UI code.</para>
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
    private const int ClampBlockCount = 11;

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
        Console.WriteLine($"[burncap] {Path.GetFileName(path)}: enemy Burn stacks capped at {cap}");
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
        public required EX_JumpIfNot LoopGate { get; init; }
        public required EX_JumpIfNot StackGate { get; init; }
        public required EX_Jump LoopJump { get; init; }
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
        PointGates(block, starts, refreshIndex: block.Statements.Count);
    }

    /// <summary>
    /// Inserts the same clamp loop in front of the vanilla "child added" body
    /// (refresh + feedback). This catches stacks that were assigned before
    /// <c>ActivateBuff</c> bound the child's stack-change delegate.
    /// </summary>
    private static void BuildAdded(UAsset asset, FunctionExport fn, int cap)
    {
        var block = BuildClampBlock(asset, fn, cap);
        var stmts = (fn.ScriptBytecode ?? Array.Empty<KismetExpression>()).ToList();

        // Idempotent re-apply: remove the previous clamp block before inserting
        // the new cap. Vanilla's added-event tail has no jumps, but shift any
        // absolute targets anyway so this stays safe if the graph changes.
        if (stmts.Count >= ClampBlockCount
            && stmts[0] is EX_Let first
            && PartyFiveKit.FieldName(first.Value) == IndexLocal)
        {
            uint oldSize = 0;
            for (var i = 0; i < ClampBlockCount; i++)
                oldSize += (uint)stmts[i].GetSize(asset);
            fn.ScriptBytecode = stmts.ToArray();
            LuneSkillsKit.ShiftAbsoluteSigned(asset, fn, 0, oldSize);
            stmts.RemoveRange(0, ClampBlockCount);
        }

        uint blockSize = 0;
        foreach (var s in block.Statements)
            blockSize += (uint)s.GetSize(asset);

        fn.ScriptBytecode = stmts.ToArray();
        ShiftOffsetsAtOrAfter(asset, fn, 0, blockSize);
        stmts.InsertRange(0, block.Statements);
        fn.ScriptBytecode = stmts.ToArray();

        var starts = PartyFiveKit.StatementStarts(asset, stmts);
        PointGates(block, starts, refreshIndex: block.Statements.Count);
    }

    private static void PointGates(ClampBlock block, List<uint> starts, int refreshIndex)
    {
        // Block layout:
        // 0 index=0, 1 count, 2 team, 3 player gate, 4 loop gate,
        // 5 get child, 6 get stacks, 7 stack gate, 8 set stacks,
        // 9 increment, 10 loop jump. Then the caller's refresh/return tail.
        var refreshOffset = starts[refreshIndex];
        block.PlayerGate.CodeOffset = refreshOffset;
        block.LoopGate.CodeOffset = refreshOffset;
        block.StackGate.CodeOffset = starts[9];
        block.LoopJump.CodeOffset = starts[4];

        if (block.PlayerGate.CodeOffset == 0xFFFFFFFE || block.LoopGate.CodeOffset == 0xFFFFFFFE
            || block.StackGate.CodeOffset == 0xFFFFFFFE || block.LoopJump.CodeOffset == 0xFFFFFFFE)
            throw new InvalidDataException("[burncap] unresolved jump target");
    }

    private static ClampBlock BuildClampBlock(UAsset asset, FunctionExport fn, int cap)
    {
        var indexPtr = AddLocal(asset, fn, IndexLocal, IntProp(asset));
        var countPtr = AddLocal(asset, fn, CountLocal, IntProp(asset));
        var teamPtr = AddLocal(asset, fn, TeamLocal, ByteProp(asset));
        var stacksPtr = AddLocal(asset, fn, StacksLocal, IntProp(asset));
        var childPtr = AddLocal(asset, fn, ChildLocal, ChildProp(asset));

        var childInstancesPtr = FindInstancePtr(asset, "ChildBuffInstances");
        var targetStatsPtr = FindInstancePtr(asset, "TargetCharacterStats");
        var arrayLibCdo = EnsureArrayLibraryCdo(asset);
        var arrayGet = GameplayTweaksKit.EnsureEngineFunction(asset, "KismetArrayLibrary", "Array_Get");
        var arrayLength = GameplayTweaksKit.EnsureEngineFunction(asset, "KismetArrayLibrary", "Array_Length");
        var less = GameplayTweaksKit.EnsureEngineFunction(asset, "KismetMathLibrary", "Less_IntInt");
        var add = GameplayTweaksKit.EnsureEngineFunction(asset, "KismetMathLibrary", "Add_IntInt");
        var notEqualByte = GameplayTweaksKit.EnsureEngineFunction(asset, "KismetMathLibrary", "NotEqual_ByteByte");

        EX_LocalVariable L(KismetPropertyPointer p) => new() { Variable = p };
        EX_InstanceVariable Inst(KismetPropertyPointer p) => new() { Variable = ClonePtr(asset, p) };

        var indexZero = new EX_Let
        {
            Value = indexPtr,
            Variable = L(indexPtr),
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
            BooleanExpression = new EX_CallMath
            {
                StackNode = notEqualByte,
                Parameters = new KismetExpression[]
                {
                    L(teamPtr),
                    new EX_ByteConst { Value = 1 }, // team 1 is the player side
                },
            },
        };

        var loopGate = new EX_JumpIfNot
        {
            CodeOffset = 0,
            BooleanExpression = new EX_CallMath
            {
                StackNode = less,
                Parameters = new KismetExpression[] { L(indexPtr), L(countPtr) },
            },
        };

        var getChild = new EX_Context
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

        var getStacks = new EX_Context
        {
            ObjectExpression = L(childPtr),
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            ContextExpression = new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(asset, "GetStackCount"),
                Parameters = new KismetExpression[] { L(stacksPtr) },
            },
        };
        var stackGate = new EX_JumpIfNot
        {
            CodeOffset = 0,
            BooleanExpression = new EX_CallMath
            {
                StackNode = less,
                Parameters = new KismetExpression[]
                {
                    new EX_IntConst { Value = cap },
                    L(stacksPtr),
                },
            },
        };
        var setStacks = new EX_Context
        {
            ObjectExpression = L(childPtr),
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            ContextExpression = new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(asset, "ChangeStackCount"),
                Parameters = new KismetExpression[] { new EX_IntConst { Value = cap } },
            },
        };

        var increment = new EX_Let
        {
            Value = indexPtr,
            Variable = L(indexPtr),
            Expression = new EX_CallMath
            {
                StackNode = add,
                Parameters = new KismetExpression[]
                {
                    L(indexPtr),
                    new EX_IntConst { Value = 1 },
                },
            },
        };
        var loopJump = new EX_Jump { CodeOffset = 0 };

        return new ClampBlock
        {
            Statements = new List<KismetExpression>
            {
                indexZero,   // 0
                countLet,    // 1
                teamLet,     // 2
                playerGate,  // 3 -> refresh
                loopGate,    // 4 -> refresh
                getChild,    // 5
                getStacks,   // 6
                stackGate,   // 7 -> increment
                setStacks,   // 8
                increment,   // 9
                loopJump,    // 10 -> loopGate
            },
            PlayerGate = playerGate,
            LoopGate = loopGate,
            StackGate = stackGate,
            LoopJump = loopJump,
        };
    }

    public static bool Verify(string path, int cap)
    {
        try
        {
            var asset = Open(path);
            var stackChanged = FindFunction(asset, StackChangedFunction);
            var added = FindFunction(asset, AddedFunction);
            bool stackChangedOk = VerifyFunction(asset, stackChanged, cap, requireVanillaTail: true);
            bool addedOk = VerifyFunction(asset, added, cap, requireVanillaTail: false);
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

    private static bool VerifyFunction(UAsset asset, FunctionExport fn, int cap, bool requireVanillaTail)
    {
        if (fn.ScriptBytecode is null) return false;
        var locals = (fn.LoadedProperties ?? Array.Empty<FProperty>())
            .Select(p => p.Name.ToString())
            .ToHashSet(StringComparer.Ordinal);
        bool hasLocals = locals.Contains(IndexLocal) && locals.Contains(CountLocal)
            && locals.Contains(TeamLocal) && locals.Contains(ChildLocal)
            && locals.Contains(StacksLocal);

        bool hasCap = false, hasGetTeam = false, hasChange = false, hasGetStacks = false;
        foreach (var st in fn.ScriptBytecode)
            MayhemFixKit.Walk(new[] { st }, e =>
            {
                switch (e)
                {
                    case EX_IntConst ic when ic.Value == cap:
                        hasCap = true;
                        break;
                    case EX_LocalVirtualFunction vf:
                        var n = vf.VirtualFunctionName.ToString();
                        if (n == "GetTeam") hasGetTeam = true;
                        if (n == "GetStackCount") hasGetStacks = true;
                        if (n == "ChangeStackCount") hasChange = true;
                        break;
                }
            });

        bool hasTail = !requireVanillaTail
            || fn.ScriptBytecode.Any(st => st is EX_LocalVirtualFunction vf
                && vf.VirtualFunctionName.ToString() == "RefreshBurnCounterWidget");
        return hasLocals && hasCap && hasGetTeam && hasGetStacks && hasChange && hasTail;
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

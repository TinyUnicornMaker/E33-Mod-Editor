using System.Reflection;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Caps the number of simultaneous passive / picto proc cards on the battle HUD.
///
/// <para>Every proc funnels through
/// <c>WBP_PassivePopupContainer.AddPassiveEffectNotification</c>, which keeps a
/// <c>TMap&lt;FName, WBP_PassiveEffectNotification*&gt; ExistingPassivePopups</c>
/// so repeated procs of the same passive increment one card's counter instead of
/// spawning duplicates. A new card is created for every passive id not already
/// in that map, and each card removes itself (via
/// <c>OnNotificationExpired</c>) after its 3 s <c>DisappearTime</c>. With 20–30
/// equipped pictos all proccing on the same turn, that means 20–30 cards.</para>
///
/// <para>The patch inserts a guard just before the card is created:</para>
/// <code>
/// keys = ExistingPassivePopups.Keys()
/// if (Array_IsValidIndex(keys, Cap - 1)) return;   // map already holds Cap cards
/// </code>
/// <para>The guard sits between vanilla's two entry pushes: the first saves the
/// function-exit address, the second saves the duplicate/increment path. That
/// leaves exactly one address on the execution-flow stack, so a full screen can
/// return with a single clean <c>EX_PopExecutionFlow</c> (placing it later would
/// pop the duplicate handler instead). While the screen is full a repeat proc of
/// a visible card is ignored too — the card stays up until its 3 s timer
/// expires; below the cap vanilla's duplicate path still increments it. The map
/// is the exact live-card set (the expiry callback removes the entry), so no
/// extra counter or teardown patch is needed. <see cref="VanillaCap"/> (0)
/// means the asset is not staged at all.</para>
/// </summary>
public static class PictoPopupCapKit
{
    public const string AssetStem = "WBP_PassivePopupContainer";
    public const string FunctionName = "AddPassiveEffectNotification";
    public const int VanillaCap = 0;
    public const int DefaultCap = 5;
    public const int MaxCap = 30;

    private const string KeysLocal = "E33PopupCapKeys";
    private const string ReachedLocal = "E33PopupCapReached";

    public static int Apply(string modDir, int cap)
    {
        if (cap <= VanillaCap)
        {
            var stale = FileCopy.FindUnder(modDir, AssetStem + ".uasset");
            if (stale is not null)
            {
                File.Delete(stale);
                var staleExp = Path.ChangeExtension(stale, ".uexp");
                if (File.Exists(staleExp)) File.Delete(staleExp);
            }
            return 0;
        }

        cap = Math.Clamp(cap, 1, MaxCap);
        var path = CopyIntoMod(modDir);
        var asset = Open(path);
        var fn = FindFunction(asset);

        if (TryUpdateCap(asset, fn, cap))
        {
            Write(asset, path);
            if (!Verify(path, cap))
                throw new InvalidDataException($"{AssetStem}: popup cap update did not verify");
            Console.WriteLine($"[popup-cap] {AssetStem}: cap updated to {cap}");
            return 1;
        }

        InsertGuard(asset, fn, cap);
        Write(asset, path);
        if (!Verify(path, cap))
            throw new InvalidDataException($"{AssetStem}: popup cap patch did not verify");
        Console.WriteLine($"[popup-cap] {AssetStem}: cards capped at {cap}");
        return 1;
    }

    private static void InsertGuard(UAsset asset, FunctionExport fn, int cap)
    {
        var stmts = (fn.ScriptBytecode ?? throw new InvalidDataException(
            $"{AssetStem}: {FunctionName} has no bytecode")).ToList();

        // Vanilla opens with two pushes: the function-exit address, then the
        // duplicate/increment path. Insert between them so the guard runs with a
        // single address on the flow stack and can return with one clean pop.
        // (After the duplicate check there are two addresses on the not-found
        // path, and EX_PopExecutionFlow would land on the duplicate handler.)
        var pushes = new List<int>();
        for (int i = 0; i < stmts.Count; i++)
            if (stmts[i] is EX_PushExecutionFlow) pushes.Add(i);
        if (pushes.Count < 2 || pushes[0] != 0)
            throw new InvalidDataException($"{AssetStem}: unexpected entry flow shape");
        int createIdx = pushes[1];

        // Only insert once; a second call updates the cap instead.
        if (AlreadyPatched(asset, fn))
            throw new InvalidDataException($"{AssetStem}: guard already present but cap lookup failed");

        // The array local and the bool local are cloned from this same function's
        // own locals, never from another asset (cooked frame layout rule).
        var arrayTpl = (fn.LoadedProperties ?? Array.Empty<FProperty>())
            .OfType<FArrayProperty>()
            .FirstOrDefault(p => p.Inner is FGenericProperty g && g.SerializedType.ToString() == "NameProperty")
            ?? throw new InvalidDataException($"{AssetStem}: no FName-array local to clone");
        var boolTpl = (fn.LoadedProperties ?? Array.Empty<FProperty>())
            .OfType<FBoolProperty>()
            .FirstOrDefault(p => (p.PropertyFlags & EPropertyFlags.CPF_Parm) == 0)
            ?? throw new InvalidDataException($"{AssetStem}: no bool local to clone");

        var keysPtr = AddLocal(asset, fn, KeysLocal, arrayTpl);
        var reachedPtr = AddLocal(asset, fn, ReachedLocal, boolTpl);

        var mapVar = FindInstanceVariable(asset, fn, "ExistingPassivePopups")
            ?? throw new InvalidDataException($"{AssetStem}: ExistingPassivePopups map not found");
        var mapLibCdo = FindImport(asset, "Default__BlueprintMapLibrary");
        var arrayLibCdo = FindImport(asset, "Default__KismetArrayLibrary");

        var mapKeysFn = GameplayTweaksKit.EnsureEngineFunction(
            asset, "BlueprintMapLibrary", "Map_Keys");
        var isValidIndexFn = GameplayTweaksKit.EnsureEngineFunction(
            asset, "KismetArrayLibrary", "Array_IsValidIndex");

        EX_LocalVariable L(KismetPropertyPointer p) => new() { Variable = p };

        var mapKeys = new EX_Context
        {
            ObjectExpression = new EX_ObjectConst { Value = mapLibCdo },
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            ContextExpression = new EX_FinalFunction
            {
                StackNode = mapKeysFn,
                Parameters = new KismetExpression[]
                {
                    MayhemFixKit.CloneKismet(mapVar),
                    L(keysPtr),
                },
            },
        };
        var capReached = new EX_LetBool
        {
            VariableExpression = L(reachedPtr),
            AssignmentExpression = new EX_Context
            {
                ObjectExpression = new EX_ObjectConst { Value = arrayLibCdo },
                RValuePointer = reachedPtr,
                ContextExpression = new EX_FinalFunction
                {
                    StackNode = isValidIndexFn,
                    Parameters = new KismetExpression[]
                    {
                        L(keysPtr),
                        new EX_IntConst { Value = cap - 1 },
                    },
                },
            },
        };
        var skipGuard = new EX_JumpIfNot
        {
            CodeOffset = 0, // fixed after the insertion shifts every target
            BooleanExpression = L(reachedPtr),
        };
        var exit = new EX_PopExecutionFlow();

        var inserted = new KismetExpression[] { mapKeys, capReached, skipGuard, exit };
        uint insertOff = 0;
        for (int i = 0; i < createIdx; i++) insertOff += (uint)stmts[i].GetSize(asset);
        uint added = (uint)inserted.Sum(s => (long)s.GetSize(asset));

        stmts.InsertRange(createIdx, inserted);
        fn.ScriptBytecode = stmts.ToArray();

        // Move every pre-existing absolute target at/after the insertion point.
        MayhemFixKit.ShiftAbsolute(asset, fn, insertOff, added, null);
        ShiftTargetsAt(asset, fn, insertOff, added);

        // The guard is skipped when the map is below the cap; falling through
        // runs EX_PopExecutionFlow, which returns via vanilla's pushed exit.
        skipGuard.CodeOffset = insertOff + added;

        MayhemFixKit.RecalcContextSkips(asset, fn);
        PartyFiveKit.VerifyJumps(asset, fn);
    }

    /// <summary>Moves absolute targets that point exactly at the insertion point
    /// (ShiftAbsolute's strict boundary misses those).</summary>
    private static void ShiftTargetsAt(UAsset asset, FunctionExport fn, uint at, long delta)
    {
        if (delta == 0) return;
        uint pos = 0;
        foreach (var stmt in fn.ScriptBytecode!)
            stmt.Visit(asset, ref pos, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j when j.CodeOffset == at:
                        j.CodeOffset = (uint)(j.CodeOffset + delta); break;
                    case EX_JumpIfNot jn when jn.CodeOffset == at:
                        jn.CodeOffset = (uint)(jn.CodeOffset + delta); break;
                    case EX_PushExecutionFlow pf when pf.PushingAddress == at:
                        pf.PushingAddress = (uint)(pf.PushingAddress + delta); break;
                }
            });
    }

    /// <summary>Updates the cap literal on an already-patched asset.</summary>
    private static bool TryUpdateCap(UAsset asset, FunctionExport fn, int cap)
    {
        EX_IntConst? literal = null;
        MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (literal is not null) return;
            if (e is EX_FinalFunction ff && ff.StackNode is { } sn && sn.IsImport()
                && sn.ToImport(asset).ObjectName.ToString() == "Array_IsValidIndex"
                && ff.Parameters is { Length: 2 }
                && ff.Parameters[1] is EX_IntConst ic)
                literal = ic;
        });
        if (literal is null) return false;
        literal.Value = cap - 1;
        return true;
    }

    private static bool AlreadyPatched(UAsset asset, FunctionExport fn)
    {
        bool found = false;
        MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (found) return;
            if (e is EX_FinalFunction ff && ff.StackNode is { } sn && sn.IsImport()
                && sn.ToImport(asset).ObjectName.ToString() == "Array_IsValidIndex")
                found = true;
        });
        return found;
    }

    public static bool Verify(string path, int cap)
    {
        try
        {
            var asset = Open(path);
            var fn = FindFunction(asset);
            var stmts = fn.ScriptBytecode ?? Array.Empty<KismetExpression>();

            int mapKeys = -1;
            for (int i = 0; i < stmts.Length; i++)
            {
                if (stmts[i] is not EX_Context ctx
                    || ctx.ContextExpression is not EX_FinalFunction ff
                    || ff.StackNode is not { } sn || !sn.IsImport()
                    || sn.ToImport(asset).ObjectName.ToString() != "Map_Keys")
                    continue;
                mapKeys = i;
                break;
            }
            if (mapKeys < 0) return false;
            if (mapKeys + 4 >= stmts.Length) return false;
            // The guard must sit between vanilla's exit push and duplicate push,
            // so the full-screen path pops exactly one (the exit) address.
            if (mapKeys == 0 || stmts[mapKeys - 1] is not EX_PushExecutionFlow) return false;
            if (stmts[mapKeys + 4] is not EX_PushExecutionFlow) return false;
            if (stmts[mapKeys + 1] is not EX_LetBool let
                || let.AssignmentExpression is not EX_Context check
                || check.ContextExpression is not EX_FinalFunction isFn
                || isFn.StackNode is not { } isn || !isn.IsImport()
                || isn.ToImport(asset).ObjectName.ToString() != "Array_IsValidIndex"
                || isFn.Parameters is not { Length: 2 }
                || isFn.Parameters[1] is not EX_IntConst lit
                || lit.Value != cap - 1)
                return false;
            if (stmts[mapKeys + 2] is not EX_JumpIfNot) return false;
            if (stmts[mapKeys + 3] is not EX_PopExecutionFlow) return false;

            var names = new HashSet<string>(
                (fn.LoadedProperties ?? Array.Empty<FProperty>()).Select(p => p.Name.ToString()));
            if (!names.Contains(KeysLocal) || !names.Contains(ReachedLocal)) return false;
            foreach (var p in fn.LoadedProperties ?? Array.Empty<FProperty>())
            {
                if (p.ArrayDim != EArrayDim.TArray || p.ElementSize <= 0) return false;
                if (p is FArrayProperty a && a.Inner is { } inner
                    && (inner.ArrayDim != EArrayDim.TArray || inner.ElementSize <= 0))
                    return false;
            }

            PartyFiveKit.VerifyJumps(asset, fn);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static FunctionExport FindFunction(UAsset asset) =>
        asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == FunctionName)
        ?? throw new InvalidDataException($"{AssetStem}: {FunctionName} not found");

    private static EX_InstanceVariable? FindInstanceVariable(UAsset asset, FunctionExport fn, string name)
    {
        EX_InstanceVariable? found = null;
        MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (found is null && e is EX_InstanceVariable iv && FieldName(iv.Variable) == name)
                found = iv;
        });
        return found;
    }

    private static string FieldName(KismetPropertyPointer? p) =>
        p?.New?.Path?.FirstOrDefault()?.ToString() ?? p?.ToString() ?? "";

    private static KismetPropertyPointer AddLocal(UAsset asset, FunctionExport fn, string name, FProperty template)
    {
        var props = (fn.LoadedProperties ?? Array.Empty<FProperty>()).ToList();
        var existing = props.FirstOrDefault(p => p.Name.ToString() == name);
        if (existing is not null)
        {
            NormalizeArrayDim(existing);
            fn.LoadedProperties = props.ToArray();
            return LocalPtr(asset, fn, name);
        }

        var prop = (FProperty)ShallowClone(template);
        prop.Name = FName.FromString(asset, name);
        prop.RepIndex = 0;
        if (prop is FArrayProperty array && array.Inner is not null)
            array.Inner.Name = FName.FromString(asset, name);
        NormalizeArrayDim(prop);
        props.Add(prop);
        fn.LoadedProperties = props.ToArray();
        return LocalPtr(asset, fn, name);
    }

    /// <summary>The VM reserves <c>ElementSize × ArrayDim</c> per local; a
    /// default NotAnArray (0) makes the local zero-sized and crashes at runtime.</summary>
    private static void NormalizeArrayDim(FProperty prop)
    {
        prop.ArrayDim = EArrayDim.TArray;
        if (prop is FArrayProperty array && array.Inner is not null)
            NormalizeArrayDim(array.Inner);
    }

    private static KismetPropertyPointer LocalPtr(UAsset asset, FunctionExport fn, string name) =>
        new(new FFieldPath(
            new[] { FName.FromString(asset, name) },
            FPackageIndex.FromExport(asset.Exports.IndexOf(fn))));

    private static FPackageIndex FindImport(UAsset asset, string objectName)
    {
        for (int i = 0; i < asset.Imports.Count; i++)
            if (asset.Imports[i].ObjectName.ToString() == objectName)
                return FPackageIndex.FromImport(i);
        throw new InvalidDataException($"{AssetStem}: import not found: {objectName}");
    }

    private static object ShallowClone(object o)
    {
        var m = o.GetType().GetMethod("MemberwiseClone",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return m.Invoke(o, null)!;
    }

    private static UAsset Open(string path)
    {
        var map = DataTableEditor.Mappings;
        return map is not null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
    }

    private static void Write(UAsset asset, string path)
    {
        try { asset.Write(path); }
        catch (FormatException ex)
        {
            throw new FormatException(Path.GetFileName(path) + ": " + ex.Message, ex);
        }
    }

    private static string CopyIntoMod(string modDir)
    {
        var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "popup_cap");
        var src = FileCopy.FindTablesAsset(AssetStem + ".uasset");
        if (src is null)
        {
            Directory.CreateDirectory(dir);
            using (E33ModEditor.Core.Packer.DonorExtract.SuspendMods())
                new Retoc().ToLegacy(dir, AssetStem);
            src = FileCopy.FindUnder(dir, AssetStem + ".uasset")
                ?? throw new FileNotFoundException(AssetStem + ".uasset");
        }

        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) throw new InvalidDataException(AssetStem + " is not under Sandfall/");
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }
}

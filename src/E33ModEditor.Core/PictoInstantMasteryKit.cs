using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Forces a picto's lumina passive to be learnt/mastered at the moment the
/// picto enters the inventory.
///
/// <para>Every acquisition route (chest, enemy drop, shop, quest reward, save
/// fill) funnels through <c>AC_jRPG_InventoryManager.AddItemToInventory</c>,
/// which calls <c>BP_DataAsset_Item.OnAddedToInventory(InventoryManager,
/// ItemID, LootContext, GeneratedItem)</c> on the item's data asset. The picto
/// data asset inherits the base implementation and does not override it — the
/// base is an empty stub.</para>
///
/// <para>The kit rewrites that stub. For a picto it:</para>
/// <list type="number">
/// <item>casts <c>Self</c> to <c>BP_DataAsset_Item_Pictos_C</c> and bails on
/// failure (non-picto items keep vanilla behaviour);</item>
/// <item>calls the picto data asset's <c>GetUnlockedPassiveEffects</c> (the
/// same <c>PassiveEffects[].Row.RowName</c> projection the battle path uses);</item>
/// <item>gets the custom game instance through
/// <c>InventoryManager.GameInstanceReference</c> and takes the first installed
/// character from <c>GetCharacterCollection</c>;</item>
/// <item>calls that character's existing <c>LearnPassiveEffectShared</c>, which
/// writes the passive row as mastered into the shared progression map
/// (<c>LearntSteps = LuminaUnlockStepsCount</c>, <c>IsLearnt = true</c>).</item>
/// </list>
///
/// <para>The first character is only a writer for the shared progression map;
/// any character in the collection would do. The call is idempotent — picking up
/// the same picto again rewrites the same mastered row.</para>
/// </summary>
public static class PictoInstantMasteryKit
{
    public const string AssetStem = "BP_DataAsset_Item";
    public const string FunctionName = "OnAddedToInventory";
    private const string PictosPackage = "/Game/Gameplay/Inventory/BP_DataAsset_Item_Pictos";
    private const string PictosClass = "BP_DataAsset_Item_Pictos_C";
    // Weapons inherit from BP_DataAsset_Item_Pictos_C, so the pictos cast alone
    // also matches weapons. A weapon's LevelNLumina passives must NOT be written
    // into the picto/lumina progression as if they were pictos.
    private const string WeaponPackage = "/Game/Gameplay/Pictos/Weapons/BP_DataAsset_Weapon";
    private const string WeaponClass = "BP_DataAsset_Weapon_C";
    private const string CharacterPackage = "/Game/Gameplay/CharacterData/BP_CharacterData";
    private const string CharacterClass = "BP_CharacterData_C";

    public static int Apply(string modDir)
    {
        var path = CopyIntoMod(AssetStem, modDir);
        var asset = Open(path);
        var fn = FindFunction(asset, FunctionName);

        // Older builds added an E33PictoCastOk bool local that the engine
        // rejects at runtime ("size 0"). Remove it and rebuild the graph with
        // inline jump conditions instead.
        RemoveOldBoolLocal(asset, fn);

        var pictosClass = EnsureBlueprintClass(asset, PictosPackage, PictosClass);
        var weaponClass = EnsureBlueprintClass(asset, WeaponPackage, WeaponClass);
        var characterClass = EnsureBlueprintClass(asset, CharacterPackage, CharacterClass);
        var invMgrClass = FindImport(asset, "AC_jRPG_InventoryManager_C")
            ?? throw new InvalidDataException("[picto-instant] inventory manager class import missing");
        var arrayGet = GameplayTweaksKit.EnsureEngineFunction(asset, "KismetArrayLibrary", "Array_Get");
        var arrayLength = GameplayTweaksKit.EnsureEngineFunction(asset, "KismetArrayLibrary", "Array_Length");
        var arrayLibCdo = EnsureArrayLibraryCdo(asset);
        var greater = GameplayTweaksKit.EnsureEngineFunction(asset, "KismetMathLibrary", "Greater_IntInt");

        var castPtr = AddLocal(asset, fn, "E33PictoCastData", new FObjectProperty
        {
            SerializedType = FName.FromString(asset, "ObjectProperty"),
            ElementSize = 8,
            PropertyClass = pictosClass,
        });
        var weaponPtr = AddLocal(asset, fn, "E33PictoWeaponCast", new FObjectProperty
        {
            SerializedType = FName.FromString(asset, "ObjectProperty"),
            ElementSize = 8,
            PropertyClass = weaponClass,
        });
        var passivePtr = AddLocal(asset, fn, "E33PictoPassives", new FArrayProperty
        {
            SerializedType = FName.FromString(asset, "ArrayProperty"),
            ElementSize = 16,
            Inner = new FGenericProperty
            {
                Name = FName.FromString(asset, "E33PictoPassives"),
                SerializedType = FName.FromString(asset, "NameProperty"),
                ElementSize = 12,
            },
        });
        var charsPtr = AddLocal(asset, fn, "E33PictoCharacters", new FArrayProperty
        {
            SerializedType = FName.FromString(asset, "ArrayProperty"),
            ElementSize = 16,
            Inner = new FObjectProperty
            {
                Name = FName.FromString(asset, "E33PictoCharacters"),
                SerializedType = FName.FromString(asset, "ObjectProperty"),
                ElementSize = 8,
                PropertyClass = characterClass,
            },
        });
        var firstPassivePtr = AddLocal(asset, fn, "E33PictoFirstPassive", new FGenericProperty
        {
            SerializedType = FName.FromString(asset, "NameProperty"),
            ElementSize = 12,
        });
        var firstCharPtr = AddLocal(asset, fn, "E33PictoFirstCharacter", new FObjectProperty
        {
            SerializedType = FName.FromString(asset, "ObjectProperty"),
            ElementSize = 8,
            PropertyClass = characterClass,
        });
        var countPtr = AddLocal(asset, fn, "E33PictoArrayCount", new FGenericProperty
        {
            SerializedType = FName.FromString(asset, "IntProperty"),
            ElementSize = 4,
        });

        EX_LocalVariable L(KismetPropertyPointer p) => new() { Variable = p };

        // Keep vanilla's first statement: clear the GeneratedItem out param. The
        // inventory manager reads it right after this call and hands it to the
        // loot-notification path, so it must never be left uninitialised.
        var clearGenerated = new EX_LetObj
        {
            VariableExpression = new EX_LocalOutVariable
            {
                Variable = LocalPtr(asset, fn, "GeneratedItem"),
            },
            AssignmentExpression = new EX_NoObject(),
        };

        var castLet = new EX_Let
        {
            Value = castPtr,
            Variable = L(castPtr),
            Expression = new EX_DynamicCast
            {
                ClassPtr = pictosClass,
                Target = new EX_Self(),
            },
        };
        var skipNonPicto = new EX_JumpIfNot
        {
            CodeOffset = 0xFFFFFFFE,
            BooleanExpression = new EX_PrimitiveCast
            {
                ConversionType = (ECastToken)1,
                Target = L(castPtr),
            },
        };

        // BP_DataAsset_Weapon_C derives from BP_DataAsset_Item_Pictos_C, so the
        // pictos cast above passes for weapons too. Cast to the weapon class and
        // bail when it succeeds; otherwise weapon LevelNLumina passives end up
        // learned in the picto/lumina list (e.g. the "Offensive" start-stance
        // passive).
        var weaponCast = new EX_Let
        {
            Value = weaponPtr,
            Variable = L(weaponPtr),
            Expression = new EX_DynamicCast
            {
                ClassPtr = weaponClass,
                Target = new EX_Self(),
            },
        };
        var notWeapon = GameplayTweaksKit.EnsureEngineFunction(
            asset, "KismetMathLibrary", "Not_PreBool");
        var skipWeapon = new EX_JumpIfNot
        {
            CodeOffset = 0xFFFFFFFE,
            BooleanExpression = new EX_CallMath
            {
                StackNode = notWeapon,
                Parameters = new KismetExpression[] { L(weaponPtr) },
            },
        };

        var passiveCall = new EX_Context
        {
            ObjectExpression = L(castPtr),
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            ContextExpression = new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(asset, "GetUnlockedPassiveEffects"),
                Parameters = new KismetExpression[] { L(passivePtr) },
            },
        };
        var passiveLenCall = new EX_Context
        {
            ObjectExpression = new EX_ObjectConst { Value = arrayLibCdo },
            RValuePointer = countPtr,
            ContextExpression = new EX_FinalFunction
            {
                StackNode = arrayLength,
                Parameters = new KismetExpression[] { L(passivePtr) },
            },
        };
        var passiveLenLet = new EX_Let
        {
            Value = countPtr,
            Variable = L(countPtr),
            Expression = passiveLenCall,
        };
        var skipNoPassive = new EX_JumpIfNot
        {
            CodeOffset = 0xFFFFFFFE,
            BooleanExpression = new EX_CallMath
            {
                StackNode = greater,
                Parameters = new KismetExpression[] { L(countPtr), new EX_IntConst { Value = 0 } },
            },
        };
        var firstPassive = new EX_Context
        {
            ObjectExpression = new EX_ObjectConst { Value = arrayLibCdo },
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            ContextExpression = new EX_FinalFunction
            {
                StackNode = arrayGet,
                Parameters = new KismetExpression[]
                {
                    L(passivePtr), new EX_IntConst { Value = 0 }, L(firstPassivePtr),
                },
            },
        };

        var invMgr = new EX_LocalVariable { Variable = LocalPtr(asset, fn, "InventoryManager") };
        var giPtr = Ptr(asset, "GameInstanceReference", invMgrClass);
        var giRead = new EX_Context
        {
            ObjectExpression = invMgr,
            RValuePointer = giPtr,
            ContextExpression = new EX_InstanceVariable { Variable = giPtr },
        };
        var charsCall = new EX_Context
        {
            ObjectExpression = giRead,
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            ContextExpression = new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(asset, "GetCharacterCollection"),
                Parameters = new KismetExpression[] { new EX_True(), L(charsPtr) },
            },
        };
        var charsLenCall = new EX_Context
        {
            ObjectExpression = new EX_ObjectConst { Value = arrayLibCdo },
            RValuePointer = countPtr,
            ContextExpression = new EX_FinalFunction
            {
                StackNode = arrayLength,
                Parameters = new KismetExpression[] { L(charsPtr) },
            },
        };
        var charsLenLet = new EX_Let
        {
            Value = countPtr,
            Variable = L(countPtr),
            Expression = charsLenCall,
        };
        var skipNoChar = new EX_JumpIfNot
        {
            CodeOffset = 0xFFFFFFFE,
            BooleanExpression = new EX_CallMath
            {
                StackNode = greater,
                Parameters = new KismetExpression[] { L(countPtr), new EX_IntConst { Value = 0 } },
            },
        };

        var firstChar = new EX_Context
        {
            ObjectExpression = new EX_ObjectConst { Value = arrayLibCdo },
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            ContextExpression = new EX_FinalFunction
            {
                StackNode = arrayGet,
                Parameters = new KismetExpression[]
                {
                    L(charsPtr), new EX_IntConst { Value = 0 }, L(firstCharPtr),
                },
            },
        };
        var learn = new EX_Context
        {
            ObjectExpression = L(firstCharPtr),
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            ContextExpression = new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(asset, "LearnPassiveEffectShared"),
                Parameters = new KismetExpression[] { L(firstPassivePtr) },
            },
        };

        var body = new List<KismetExpression>
        {
            clearGenerated,
            castLet,
            skipNonPicto,
            weaponCast,
            skipWeapon,
            passiveCall,
            passiveLenLet,
            skipNoPassive,
            firstPassive,
            charsCall,
            charsLenLet,
            skipNoChar,
            firstChar,
            learn,
            new EX_Return { ReturnExpression = new EX_Nothing() },
            new EX_EndOfScript(),
        };
        fn.ScriptBytecode = body.ToArray();

        var starts = PartyFiveKit.StatementStarts(asset, body);
        uint end = starts[body.Count - 2]; // Return
        skipNonPicto.CodeOffset = end;
        skipWeapon.CodeOffset = end;
        skipNoPassive.CodeOffset = end;
        skipNoChar.CodeOffset = end;

        MayhemFixKit.RecalcContextSkips(asset, fn);
        PartyFiveKit.VerifyJumps(asset, fn);
        AssertNoPlaceholders(fn);
        asset.Write(path);
        Verify(path);
        return 1;
    }

    public static bool Verify(string path)
    {
        var asset = Open(path);
        var fn = FindFunction(asset, FunctionName);
        var body = fn.ScriptBytecode ?? Array.Empty<KismetExpression>();
        if (body.Length < 16) return false;
        if (body[0] is not EX_LetObj clear
            || clear.VariableExpression is not EX_LocalOutVariable
            || clear.AssignmentExpression is not EX_NoObject) return false;
        if (body[1] is not EX_Let cast || cast.Expression is not EX_DynamicCast) return false;
        if (body[2] is not EX_JumpIfNot castCheck
            || castCheck.BooleanExpression is not EX_PrimitiveCast) return false;
        if (body[3] is not EX_Let weaponCast || weaponCast.Expression is not EX_DynamicCast) return false;
        if (body[4] is not EX_JumpIfNot weaponCheck
            || weaponCheck.BooleanExpression is not EX_CallMath) return false;
        if (body[5] is not EX_Context passiveCall) return false;
        if (body[6] is not EX_Let passiveLen || passiveLen.Expression is not EX_Context) return false;
        if (body[7] is not EX_JumpIfNot passiveCheck
            || passiveCheck.BooleanExpression is not EX_CallMath) return false;
        if (body[8] is not EX_Context getPassive
            || getPassive.ContextExpression is not EX_FinalFunction gp
            || gp.Parameters?.Length != 3) return false;
        if (body[9] is not EX_Context charsCall) return false;
        if (body[10] is not EX_Let charsLen || charsLen.Expression is not EX_Context) return false;
        if (body[11] is not EX_JumpIfNot charCheck
            || charCheck.BooleanExpression is not EX_CallMath) return false;
        if (body[12] is not EX_Context getChar
            || getChar.ContextExpression is not EX_FinalFunction gc
            || gc.Parameters?.Length != 3) return false;
        if (body[13] is not EX_Context learn
            || learn.ContextExpression is not EX_LocalVirtualFunction lvf
            || lvf.VirtualFunctionName.ToString() != "LearnPassiveEffectShared") return false;
        if (body[^2] is not EX_Return || body[^1] is not EX_EndOfScript) return false;
        if (fn.LoadedProperties?.Any(p => p.Name.ToString() == "E33PictoCastOk") == true)
            return false;
        if (!AddedLocalsAreWellFormed(fn)) return false;
        try { PartyFiveKit.VerifyJumps(asset, fn); }
        catch { return false; }
        return true;
    }

    /// <summary>
    /// The engine computes a local's frame slot as <c>ElementSize * ArrayDim</c>.
    /// UAssetAPI's <see cref="FProperty"/> default is <c>NotAnArray (0)</c>, which
    /// makes every kit-added local zero-sized; the VM then asserts on the first
    /// property it touches ("Unsupported FBoolProperty ... size 0") or walks off
    /// the frame. Every local, and every array inner property, must be
    /// <c>TArray (1)</c> exactly like the vanilla locals.
    /// </summary>
    private static bool AddedLocalsAreWellFormed(FunctionExport fn)
    {
        foreach (var p in fn.LoadedProperties ?? Array.Empty<FProperty>())
        {
            if (!p.Name.ToString().StartsWith("E33Picto", StringComparison.Ordinal)) continue;
            if (!IsWellFormed(p)) return false;
        }
        return true;

        static bool IsWellFormed(FProperty p)
        {
            if (p.ArrayDim != EArrayDim.TArray || p.ElementSize <= 0) return false;
            return p is not FArrayProperty a || a.Inner is null || IsWellFormed(a.Inner);
        }
    }

    private static bool IsPatched(FunctionExport fn)
    {
        if (fn.ScriptBytecode is null) return false;
        return MayhemFixKit.WalkHas(fn.ScriptBytecode, e =>
            e is EX_LocalVirtualFunction vf
            && vf.VirtualFunctionName.ToString() == "LearnPassiveEffectShared");
    }

    /// <summary>
    /// Earlier builds created E33PictoCastOk with FieldMask=1 (bitfield shape).
    /// The engine treats that as an unsupported bool ("size 0") at runtime, so
    /// repair it to a native bool: FieldMask=255, matching vanilla bool locals.
    /// </summary>
    private static bool RepairCastOk(UAsset asset, FunctionExport fn)
    {
        var p = fn.LoadedProperties?.OfType<FBoolProperty>()
            .FirstOrDefault(x => x.Name.ToString() == "E33PictoCastOk");
        if (p is null) return false;
        bool changed = p.ElementSize != 1 || p.FieldSize != 1
            || p.ByteOffset != 0 || p.ByteMask != 1 || p.FieldMask != 255;
        p.ElementSize = 1;
        p.FieldSize = 1;
        p.ByteOffset = 0;
        p.ByteMask = 1;
        p.FieldMask = 255;
        return changed;
    }

    private static KismetPropertyPointer LocalPtr(UAsset asset, FunctionExport fn, string name) =>
        new(new FFieldPath(
            new[] { FName.FromString(asset, name) },
            FPackageIndex.FromExport(asset.Exports.IndexOf(fn))));

    private static KismetPropertyPointer Ptr(UAsset asset, string name, FPackageIndex owner) =>
        new(new FFieldPath(new[] { FName.FromString(asset, name) }, owner));

    private static FPackageIndex? FindImport(UAsset asset, string objectName)
    {
        for (var i = 0; i < asset.Imports.Count; i++)
            if (asset.Imports[i].ObjectName.ToString() == objectName)
                return FPackageIndex.FromImport(i);
        return null;
    }

    private static FPackageIndex EnsureBlueprintClass(UAsset asset, string packagePath, string className)
    {
        var existing = FindImport(asset, className);
        if (existing is not null) return existing;
        var pkg = asset.AddImport(new Import("/Script/CoreUObject", "Package",
            FPackageIndex.FromRawIndex(0), packagePath, false, asset));
        return asset.AddImport(new Import("/Script/Engine", "BlueprintGeneratedClass",
            pkg, className, false, asset));
    }

    /// <summary>
    /// <c>KismetArrayLibrary.Array_Get/Array_Length</c> are compiled as
    /// <c>Context(Default__KismetArrayLibrary).Function(…)</c>; the VM needs the
    /// library's CDO as the context object. Add the import if this asset has
    /// never referenced the array library before.
    /// </summary>
    private static FPackageIndex EnsureArrayLibraryCdo(UAsset asset)
    {
        var existing = FindImport(asset, "Default__KismetArrayLibrary");
        if (existing is not null) return existing;

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

    private static void RemoveOldBoolLocal(UAsset asset, FunctionExport fn)
    {
        var props = fn.LoadedProperties?.ToList();
        if (props is null) return;
        if (props.RemoveAll(p => p.Name.ToString() == "E33PictoCastOk") > 0)
            fn.LoadedProperties = props.ToArray();
    }

    private static KismetPropertyPointer AddLocal(UAsset asset, FunctionExport fn, string name, FProperty prop)
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
        // FProperty.ArrayDim defaults to NotAnArray (0). The engine multiplies
        // ElementSize by ArrayDim when it reserves the local's frame slot, so a
        // zero here makes the property "size 0" and crashes at runtime. Vanilla
        // locals (and array inner properties) all carry TArray (1).
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

    private static FunctionExport FindFunction(UAsset asset, string name) =>
        asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == name)
        ?? throw new InvalidDataException($"[picto-instant] {name} not found");

    private static void AssertNoPlaceholders(FunctionExport fn)
    {
        foreach (var s in fn.ScriptBytecode ?? Array.Empty<KismetExpression>())
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_Jump j && j.CodeOffset == 0xFFFFFFFE)
                    throw new InvalidDataException("[picto-instant] unresolved jump");
                if (e is EX_JumpIfNot jn && jn.CodeOffset == 0xFFFFFFFE)
                    throw new InvalidDataException("[picto-instant] unresolved jump-if-not");
            });
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
        var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "picto_instant_mastery");
        var src = FindAsset(dir, asset + ".uasset")
            ?? FindAsset(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), asset + ".uasset");
        if (src is null)
        {
            Directory.CreateDirectory(dir);
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

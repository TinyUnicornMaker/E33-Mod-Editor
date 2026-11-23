using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.Core;

/// <summary>
/// Adds a persistent, per-character-coloured aura to the focused ally while
/// battle targeting is active.
///
/// The game already toggles a small <c>TargetPointer</c> billboard in
/// <c>BP_jRPG_Character_Battle_Base.OnTargetingHoverStart</c>, but a downed
/// ally lies flat and the pointer is easy to miss — especially when several
/// allies are dead and a revive is being aimed. This kit:
///
/// * spawns <c>FX_ElementalCharge</c> attached to <c>Attachment_Spine03</c>
///   (body position, no artificial lift),
/// * colours it from the character's <c>CharacterHardcodedName</c>,
/// * stores the spawned component in the existing, otherwise-unused
///   <c>DefaultHitSound</c> object slot (no new class property; adding one
///   corrupts this Blueprint's raw-CDO export layout),
/// * destroys the component from <c>OnTargetingHoverEnd</c>, so it stays lit
///   until the target is changed, confirmed or cancelled.
///
/// Both event stubs are edited; the ubergraph is untouched. The patch is
/// independent of Party of Five, camera, row-damage and skill kits that share
/// the same Blueprint.
/// </summary>
public static class TargetGlowKit
{
    private const string BattleBaseStem = "BP_jRPG_Character_Battle_Base";
    private const string EventName = "OnTargetingHoverStart";
    private const string FxName = "FX_HealSmall";
    private const string FxPackage = "/Game/FX/FX_Leo/Fx/Generic/Systems/" + FxName;
    private const string FxClass = "NiagaraSystem";
    private const string FxClassPackage = "/Script/Niagara";

    private const string SpotLocal = "E33TargetGlowSpot";
    private const string AllyLocal = "E33TargetGlowIsAlly";
    private const string ColorLocal = "E33TargetGlowColor";
    private const string LocationLocal = "E33TargetGlowLocation";
    private const string FxLocal = "E33TargetGlowFX";

    /// <summary>
    /// Persistent component reference. The character class has a vestigial
    /// object slot (DefaultHitSound) with no bytecode references and no
    /// sequence bindings; its class is repointed to NiagaraComponent. Adding a
    /// new class property is not safe: UAssetAPI cannot serialize a longer
    /// ClassExport next to this Blueprint's raw CDO without corrupting the
    /// following export offsets.
    /// </summary>
    private const string GlowComponent = "DefaultHitSound";
    private const string GlowFx = "FX_ElementalCharge";
    private const string GlowFxPackage =
        "/Game/FX/FX_Leo/Fx/Generic/Systems/FX_ElementalCharge";

    private static readonly (string Name, float R, float G, float B)[] CharacterColors =
    {
        ("Frey",   1.00f, 0.85f, 0.15f), // Gustave
        ("Maelle", 1.00f, 0.25f, 0.15f),
        ("Lune",   0.20f, 0.60f, 1.00f),
        ("Sciel",  0.75f, 0.30f, 1.00f),
        ("Verso",  0.20f, 1.00f, 0.55f),
        ("Monoco", 1.00f, 0.55f, 0.10f),
    };

    /// <summary>Per-slot colours. Slot order is the battle spot index (0..4).</summary>
    private static readonly (float R, float G, float B)[] SlotColors =
    {
        (1.00f, 0.78f, 0.25f), // 0 gold
        (0.25f, 0.65f, 1.00f), // 1 azure
        (1.00f, 0.30f, 0.80f), // 2 rose
        (0.35f, 1.00f, 0.45f), // 3 emerald
        (0.70f, 0.38f, 1.00f), // 4 violet
    };

    public sealed class Settings
    {
        public bool Enabled { get; set; } = false;

        public static Settings Load()
        {
            var path = Path.Combine(EditorPaths.RepoRoot, "work", "target_glow.json");
            if (!File.Exists(path)) return new Settings();
            try
            {
                var json = JObject.Parse(File.ReadAllText(path));
                return new Settings
                {
                    Enabled = json.Value<bool?>("Enabled") ?? false,
                };
            }
            catch
            {
                // A malformed personal settings file must not break a full build.
                return new Settings();
            }
        }
    }

    public static int Apply(string modDir, Settings? settings = null)
    {
        var s = settings ?? Settings.Load();
        if (!s.Enabled) return 0;

        var path = Stage(modDir);
        var asset = Open(path);
        var start = FindEvent(asset);
        var end = FindEvent(asset, "OnTargetingHoverEnd");

        if (HasPersistentPatch(asset, start))
        {
            VerifyPersistent(path, asset);
            return 0;
        }

        PatchPersistent(asset, start, end);
        asset.Write(path);
        VerifyPersistent(path, Open(path));
        return 1;
    }

    /// <summary>
    /// Diagnostic mode used to bisect a battle-load hang: no colour switch and
    /// no SetColorParameter, just one vanilla Niagara burst on ally focus.
    /// </summary>
    public static int ApplyMinimal(string modDir)
    {
        var path = Stage(modDir);
        var asset = Open(path);
        var fn = FindEvent(asset);

        if (HasSpawn(asset, fn, "FX_ShieldSmall"))
        {
            VerifyMinimal(asset, fn);
            return 0;
        }

        var enemy = FindInstancePtr(asset, "Enemy?")
            ?? throw new InvalidDataException($"{BattleBaseStem}: Enemy? pointer not found");
        var skip = new EX_JumpIfNot
        {
            CodeOffset = 0,
            // skip when Enemy? is true; allies have Enemy? = false
            BooleanExpression = new EX_CallMath
            {
                StackNode = Import(asset, "Not_PreBool"),
                Parameters = new KismetExpression[]
                {
                    new EX_InstanceVariable { Variable = enemy },
                },
            },
        };
        var spawn = new EX_CallMath
        {
            StackNode = Import(asset, "SpawnSystemAtLocation"),
            Parameters = new KismetExpression[]
            {
                new EX_Self(),
                new EX_ObjectConst { Value = ImportOfClass(asset, "FX_ShieldSmall", "NiagaraSystem") },
                new EX_CallMath
                {
                    StackNode = Import(asset, "Add_VectorVector"),
                    Parameters = new KismetExpression[]
                    {
                        // K2_GetActorLocation is a zero-argument native UFUNCTION.
                        // An extra EX_Self here makes the native thunk advance
                        // one byte short, leaving a top-level EndFunctionParms
                        // that spins the VM forever (battle-load freeze).
                        new EX_FinalFunction
                        {
                            StackNode = Import(asset, "K2_GetActorLocation"),
                            Parameters = Array.Empty<KismetExpression>(),
                        },
                        new EX_VectorConst { Value = new FVector(0.0f, 0.0f, 70.0f) },
                    },
                },
                new EX_RotationConst { Value = new FRotator(0.0f, 0.0f, 0.0f) },
                new EX_VectorConst { Value = new FVector(1.0f, 1.0f, 1.0f) },
                new EX_True(),
                new EX_True(),
                new EX_ByteConst { Value = 0 },
                new EX_True(),
            },
        };

        var block = new List<KismetExpression> { skip, spawn };
        uint blockSize = 0;
        foreach (var st in block) blockSize += (uint)st.GetSize(asset);
        skip.CodeOffset = blockSize;
        block.AddRange(fn.ScriptBytecode ?? Array.Empty<KismetExpression>());
        fn.ScriptBytecode = block.ToArray();

        MayhemFixKit.RecalcContextSkips(asset, fn);
        PartyFiveKit.VerifyJumps(asset, fn);
        asset.Write(path);
        var reopened = Open(path);
        VerifyMinimal(reopened, FindEvent(reopened));
        return 1;
    }

    private static bool HasSpawn(UAsset asset, FunctionExport fn, string system)
    {
        bool hit = false;
        MayhemFixKit.Walk(fn.ScriptBytecode ?? Array.Empty<KismetExpression>(), e =>
        {
            if (e is EX_ObjectConst oc && ImportName(asset, oc.Value) == system)
                hit = true;
        });
        return hit;
    }

    private static void VerifyMinimal(UAsset asset, FunctionExport fn)
    {
        int spawns = 0;
        bool fx = false;
        int skips = 0;
        MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (e is EX_CallMath cm && ImportName(asset, cm.StackNode) == "SpawnSystemAtLocation")
                spawns++;
            if (e is EX_ObjectConst oc && ImportName(asset, oc.Value) == "FX_ShieldSmall")
                fx = true;
            if (e is EX_JumpIfNot) skips++;
        });
        if (spawns != 1 || !fx || skips != 1)
            throw new InvalidDataException(
                $"{BattleBaseStem}: minimal glow block incomplete (spawn={spawns} fx={fx} skips={skips})");

        var starts = StatementStarts(asset, fn.ScriptBytecode!);
        foreach (var st in fn.ScriptBytecode!)
            if (st is EX_JumpIfNot jump && !starts.Contains(jump.CodeOffset))
                throw new InvalidDataException($"{BattleBaseStem}: minimal glow skip bad");
        VerifyCallArity(asset, fn);
    }

    /// <summary>
    /// Catches the class of bug that hung battle load: a native call with an
    /// extra argument leaves the VM one byte short of EndFunctionParms and spins.
    /// </summary>
    private static void VerifyCallArity(UAsset asset, FunctionExport fn)
    {
        var expected = new Dictionary<string, int>
        {
            ["K2_GetActorLocation"] = 0,
            ["Add_VectorVector"] = 2,
            ["SpawnSystemAtLocation"] = 9,
            ["SpawnSystemAttached"] = 10,
            ["Not_PreBool"] = 1,
            ["NotEqual_NameName"] = 2,
            ["IsValid"] = 1,
            ["K2_DestroyComponent"] = 1,
        };

        VisitAll(asset, fn, e =>
        {
            switch (e)
            {
                case EX_CallMath cm when ImportName(asset, cm.StackNode) is { } cmName &&
                    expected.TryGetValue(cmName, out var wantCm) && cm.Parameters.Length != wantCm:
                    throw new InvalidDataException(
                        $"{BattleBaseStem}: {cmName} has {cm.Parameters.Length} args (expect {wantCm})");
                case EX_FinalFunction ff when e is not EX_CallMath &&
                    ImportName(asset, ff.StackNode) is { } ffName &&
                    expected.TryGetValue(ffName, out var wantFf) && ff.Parameters.Length != wantFf:
                    throw new InvalidDataException(
                        $"{BattleBaseStem}: {ffName} has {ff.Parameters.Length} args (expect {wantFf})");
                case EX_Context { ContextExpression: EX_VirtualFunction vf }
                    when vf.VirtualFunctionName.ToString() == "SetColorParameter" &&
                         vf.Parameters.Length != 2:
                    throw new InvalidDataException(
                        $"{BattleBaseStem}: SetColorParameter has {vf.Parameters.Length} args (expect 2)");
                case EX_Context { ContextExpression: EX_VirtualFunction vf }
                    when vf.VirtualFunctionName.ToString() == "Activate" &&
                         vf.Parameters.Length != 1:
                    throw new InvalidDataException(
                        $"{BattleBaseStem}: Activate has {vf.Parameters.Length} args (expect 1)");
                case EX_Context { ContextExpression: EX_VirtualFunction vf }
                    when vf.VirtualFunctionName.ToString() == "SetRelativeScale3D" &&
                         vf.Parameters.Length != 1:
                    throw new InvalidDataException(
                        $"{BattleBaseStem}: SetRelativeScale3D has {vf.Parameters.Length} args (expect 1)");
                case EX_LocalVirtualFunction lvf
                    when (lvf.VirtualFunctionName.ToString() is "IsPlayerTeamCharacter" or "GetBattleSpotIndex") &&
                         lvf.Parameters.Length != 1:
                    throw new InvalidDataException(
                        $"{BattleBaseStem}: {lvf.VirtualFunctionName} has {lvf.Parameters.Length} out args (expect 1)");
            }
        });
    }

    private static KismetPropertyPointer? FindInstancePtr(UAsset asset, string field)
    {
        KismetPropertyPointer? found = null;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            foreach (var st in fn.ScriptBytecode ?? Array.Empty<KismetExpression>())
                MayhemFixKit.Walk(new[] { st }, e =>
                {
                    if (found is null && e is EX_InstanceVariable iv &&
                        PartyFiveKit.FieldName(iv.Variable) == field)
                        found = iv.Variable;
                });
            if (found is not null) return found;
        }
        return found;
    }

    private static FPackageIndex ImportOfClass(UAsset asset, string name, string className)
    {
        for (int i = 0; i < asset.Imports.Count; i++)
            if (asset.Imports[i].ObjectName.ToString() == name &&
                asset.Imports[i].ClassName.ToString() == className)
                return FPackageIndex.FromImport(i);
        throw new InvalidDataException($"{BattleBaseStem}: import {name} ({className}) missing");
    }

    /// <summary>Reuse an already staged Blueprint (Party of Five / camera / row damage).</summary>
    private static string Stage(string modDir)
    {
        var existing = FileCopy.FindUnder(modDir, BattleBaseStem + ".uasset");
        if (existing is not null) return existing;

        // Prefer the dedicated donor/party staging copy. The session also has
        // an __rt round-trip copy of the same Blueprint; never take that one.
        var src = FindVanilla();
        if (src is null)
        {
            var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", BattleBaseStem);
            Directory.CreateDirectory(dir);
            using (Packer.DonorExtract.SuspendMods())
                new Retoc().ToLegacy(dir, BattleBaseStem);
            src = FindVanilla()
                ?? throw new FileNotFoundException($"{BattleBaseStem}.uasset not found after extraction");
        }

        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) throw new InvalidDataException(src + " is not under Sandfall/");
        var dest = Path.Combine(modDir, src[idx..]);
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    private static string? FindVanilla()
    {
        var tables = Path.Combine(EditorPaths.RepoRoot, "work", "tables");
        foreach (var root in new[]
                 {
                     Path.Combine(tables, BattleBaseStem),
                     Path.Combine(tables, "party_five"),
                 })
        {
            if (!Directory.Exists(root)) continue;
            var hit = Directory.EnumerateFiles(root, BattleBaseStem + ".uasset", SearchOption.AllDirectories)
                .FirstOrDefault(p => !p.Contains(Path.DirectorySeparatorChar + "__rt" +
                                                Path.DirectorySeparatorChar, StringComparison.Ordinal));
            if (hit is not null) return hit;
        }

        return Directory.Exists(tables)
            ? Directory.EnumerateFiles(tables, BattleBaseStem + ".uasset", SearchOption.AllDirectories)
                .FirstOrDefault(p => !p.Contains(Path.DirectorySeparatorChar + "__rt" +
                                                Path.DirectorySeparatorChar, StringComparison.Ordinal))
            : null;
    }

    private static UAsset Open(string path)
    {
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
    }

    private static FunctionExport FindEvent(UAsset asset, string name = EventName) =>
        asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == name)
        ?? throw new InvalidDataException($"{BattleBaseStem}: {name} not found");

    private static bool HasPersistentPatch(UAsset asset, FunctionExport start)
    {
        bool hasVar = asset.Exports.OfType<ClassExport>()
            .SelectMany(c => c.LoadedProperties ?? Array.Empty<FProperty>())
            .Any(p => p.Name.ToString() == GlowComponent);
        bool hasSpawn = false;
        MayhemFixKit.Walk(start.ScriptBytecode ?? Array.Empty<KismetExpression>(), e =>
        {
            if (e is EX_CallMath cm && ImportName(asset, cm.StackNode) == "SpawnSystemAttached")
                hasSpawn = true;
        });
        return hasVar && hasSpawn;
    }

    private static void PatchPersistent(UAsset asset, FunctionExport start, FunctionExport end)
    {
        // DefaultHitSound is not referenced by bytecode, so use a real class
        // variable path (SpawnedParticleSystem) and only swap the leaf name.
        var tplPtr = FindInstancePtr(asset, "SpawnedParticleSystem")
            ?? throw new InvalidDataException($"{BattleBaseStem}: class-var pointer template missing");
        var varPtr = new KismetPropertyPointer(new FFieldPath(
            new[] { FName.FromString(asset, GlowComponent) },
            tplPtr.New?.ResolvedOwner ?? FPackageIndex.FromRawIndex(0)));
        RepurposeGlowSlot(asset);
        var spine = FindInstancePtr(asset, "Attachment_Spine03")
            ?? throw new InvalidDataException($"{BattleBaseStem}: Attachment_Spine03 missing");
        var ally = EnsureLocal(asset, start, AllyLocal, p => p is FBoolProperty);
        var colorPtr = EnsureLocal(asset, start, ColorLocal, p =>
            p is FStructProperty sp && ImportName(asset, sp.Struct) == "LinearColor");
        var empty = new KismetPropertyPointer(new FFieldPath());

        KismetExpression L(KismetPropertyPointer p) => new EX_LocalVariable { Variable = p };
        KismetExpression I(KismetPropertyPointer p) => new EX_InstanceVariable { Variable = p };

        // −─ hover start ───────────────────────────────────────────────────
        var allyCall = new EX_LocalVirtualFunction
        {
            VirtualFunctionName = FName.FromString(asset, "IsPlayerTeamCharacter"),
            Parameters = new KismetExpression[] { L(ally) },
        };
        var skipAlly = new EX_JumpIfNot { BooleanExpression = L(ally) };

        var prevValid = new EX_CallMath
        {
            StackNode = Import(asset, "IsValid"),
            Parameters = new KismetExpression[] { I(varPtr) },
        };
        var skipPrev = new EX_JumpIfNot { BooleanExpression = prevValid };
        var destroyPrev = new EX_Context
        {
            ObjectExpression = I(varPtr),
            RValuePointer = empty,
            ContextExpression = new EX_FinalFunction
            {
                StackNode = Import(asset, "K2_DestroyComponent"),
                Parameters = new KismetExpression[] { new EX_Self() },
            },
        };
        var clearPrev = new EX_LetObj
        {
            VariableExpression = I(varPtr),
            AssignmentExpression = new EX_NoObject(),
        };

        var fxImport = LuneSkillsKit.EnsureObjectImport(asset, GlowFxPackage, GlowFx,
            "NiagaraSystem", "/Script/Niagara");
        var spawnCall = new EX_CallMath
        {
            StackNode = Import(asset, "SpawnSystemAttached"),
            Parameters = new KismetExpression[]
            {
                new EX_ObjectConst { Value = fxImport },
                I(spine),
                new EX_NameConst { Value = FName.FromString(asset, "None") },
                new EX_VectorConst { Value = new FVector(0.0f, 0.0f, 0.0f) },
                new EX_RotationConst { Value = new FRotator(0.0f, 0.0f, 0.0f) },
                new EX_ByteConst { Value = 3 }, // snap to target
                new EX_False(),                // do not auto-destroy: persist until hover ends
                new EX_False(),                // activate after colour is set
                new EX_ByteConst { Value = 0 }, // no pooling
                new EX_False(),                // do not return null for a culled target
            },
        };
        var spawnLet = new EX_LetObj
        {
            VariableExpression = I(varPtr),
            AssignmentExpression = spawnCall,
        };

        // Pure NotEqual_NameName calls must be assigned to a local before being
        // used as a SwitchValue index. Passing a CallMath directly as IndexTerm
        // leaves the VM result-buffer stack unbalanced; the next stock pure
        // call (NotEqual_ObjectObject during party-selection setup) then faults
        // with a null result buffer.
        var matchLocals = CharacterColors
            .Select((_, i) => EnsureLocal(asset, start, $"E33TargetGlowName{i}",
                p => p is FBoolProperty))
            .ToArray();
        var nameAccess = FindNameAccess(asset);
        var nameLets = new List<KismetExpression>();
        for (int i = 0; i < CharacterColors.Length; i++)
        {
            nameLets.Add(new EX_LetBool
            {
                VariableExpression = L(matchLocals[i]),
                AssignmentExpression = new EX_CallMath
                {
                    StackNode = Import(asset, "NotEqual_NameName"),
                    Parameters = new KismetExpression[]
                    {
                        (KismetExpression)MayhemFixKit.CloneKismet(nameAccess),
                        new EX_NameConst { Value = FName.FromString(asset, CharacterColors[i].Name) },
                    },
                },
            });
        }

        // SpawnSystemAttached is allowed to return null. Calling
        // SetColorParameter/Activate on a null component crashes the VM.
        var spawnValid = new EX_CallMath
        {
            StackNode = Import(asset, "IsValid"),
            Parameters = new KismetExpression[] { new EX_InstanceVariable { Variable = varPtr } },
        };
        var skipSpawn = new EX_JumpIfNot { BooleanExpression = spawnValid };

        // FX_ElementalCharge is authored as a full-size charge aura. Scale the
        // attached instance down to a target marker (20% keeps it around body
        // size instead of filling half the battlefield).
        var setScale = new EX_Context
        {
            ObjectExpression = new EX_InstanceVariable { Variable = varPtr },
            RValuePointer = empty,
            ContextExpression = new EX_VirtualFunction
            {
                VirtualFunctionName = FName.FromString(asset, "SetRelativeScale3D"),
                Parameters = new KismetExpression[]
                {
                    new EX_VectorConst { Value = new FVector(0.20f, 0.20f, 0.20f) },
                },
            },
        };

        var colorLet = new EX_Let
        {
            Value = colorPtr,
            Variable = L(colorPtr),
            Expression = CharacterColor(asset, matchLocals),
        };
        var setColor = SetColorCall(asset, varPtr, "Color", L(colorPtr));
        var setColor1 = SetColorCall(asset, varPtr, "Color1", L(colorPtr));
        var activate = new EX_Context
        {
            ObjectExpression = new EX_InstanceVariable { Variable = varPtr },
            RValuePointer = empty,
            ContextExpression = new EX_VirtualFunction
            {
                VirtualFunctionName = FName.FromString(asset, "Activate"),
                // Activate(bool bReset) — vanilla calls pass true.
                Parameters = new KismetExpression[] { new EX_True() },
            },
        };

        var startBlock = new List<KismetExpression>
        {
            allyCall,
            skipAlly,
            skipPrev,
            destroyPrev,
            clearPrev,
            spawnLet,
            skipSpawn,
        };
        startBlock.Add(setScale);
        startBlock.AddRange(nameLets);
        startBlock.Add(colorLet);
        startBlock.Add(setColor);
        startBlock.Add(setColor1);
        startBlock.Add(activate);

        var startOriginal = start.ScriptBytecode ?? Array.Empty<KismetExpression>();
        if (startOriginal.Length == 0)
            throw new InvalidDataException($"{BattleBaseStem}: {EventName} has no bytecode");
        var startOriginalFirst = startOriginal[0];
        startBlock.AddRange(startOriginal);
        skipAlly.CodeOffset = OffsetOf(asset, startBlock, startOriginalFirst);
        skipPrev.CodeOffset = OffsetOf(asset, startBlock, spawnLet);
        skipSpawn.CodeOffset = OffsetOf(asset, startBlock, startOriginalFirst);
        start.ScriptBytecode = startBlock.ToArray();

        // −─ hover end ─────────────────────────────────────────────────────
        var endValid = new EX_CallMath
        {
            StackNode = Import(asset, "IsValid"),
            Parameters = new KismetExpression[] { I(varPtr) },
        };
        var skipEnd = new EX_JumpIfNot { BooleanExpression = endValid };
        var destroyEnd = new EX_Context
        {
            ObjectExpression = I(varPtr),
            RValuePointer = empty,
            ContextExpression = new EX_FinalFunction
            {
                StackNode = Import(asset, "K2_DestroyComponent"),
                Parameters = new KismetExpression[] { new EX_Self() },
            },
        };
        var clearEnd = new EX_LetObj
        {
            VariableExpression = I(varPtr),
            AssignmentExpression = new EX_NoObject(),
        };

        var endBlock = new List<KismetExpression> { skipEnd, destroyEnd, clearEnd };
        var endOriginal = end.ScriptBytecode ?? Array.Empty<KismetExpression>();
        if (endOriginal.Length == 0)
            throw new InvalidDataException($"{BattleBaseStem}: OnTargetingHoverEnd has no bytecode");
        var endOriginalFirst = endOriginal[0];
        endBlock.AddRange(endOriginal);
        skipEnd.CodeOffset = OffsetOf(asset, endBlock, endOriginalFirst);
        end.ScriptBytecode = endBlock.ToArray();

        foreach (var fn in new[] { start, end })
        {
            NormalizeSwitches(asset, fn);
            MayhemFixKit.RecalcContextSkips(asset, fn);
            PartyFiveKit.VerifyJumps(asset, fn);
        }
    }

    private static EX_Context SetColorCall(UAsset asset, KismetPropertyPointer varPtr,
        string parameter, KismetExpression color) =>
        new()
        {
            ObjectExpression = new EX_InstanceVariable { Variable = varPtr },
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            ContextExpression = new EX_VirtualFunction
            {
                VirtualFunctionName = FName.FromString(asset, "SetColorParameter"),
                Parameters = new KismetExpression[]
                {
                    new EX_NameConst { Value = FName.FromString(asset, parameter) },
                    color,
                },
            },
        };

    private static uint OffsetOf(UAsset asset, IReadOnlyList<KismetExpression> block,
        KismetExpression target)
    {
        uint off = 0;
        foreach (var stmt in block)
        {
            if (ReferenceEquals(stmt, target)) return off;
            off += (uint)stmt.GetSize(asset);
        }
        throw new InvalidDataException("offset target not in block");
    }

    private static void RepurposeGlowSlot(UAsset asset)
    {
        var cls = asset.Exports.OfType<ClassExport>()
            .FirstOrDefault(c => c.ObjectName.ToString() == BattleBaseStem + "_C")
            ?? throw new InvalidDataException($"{BattleBaseStem}: ClassExport missing");
        var prop = (cls.LoadedProperties ?? Array.Empty<FProperty>())
            .OfType<FObjectProperty>()
            .FirstOrDefault(p => p.Name.ToString() == GlowComponent)
            ?? throw new InvalidDataException($"{BattleBaseStem}: {GlowComponent} class variable missing");

        // Same-sized object slot: only the class pointer and flags change, so
        // ClassExport.SerialSize is untouched and the following raw CDO stays
        // aligned.
        if (ImportName(asset, prop.PropertyClass) != "NiagaraComponent")
            prop.PropertyClass = ImportOfClass(asset, "NiagaraComponent", "Class");
        prop.PropertyFlags |= EPropertyFlags.CPF_InstancedReference;
    }

    private static EX_StructConst ColorConst(UAsset asset, float r, float g, float b)
    {
        var tpl = FindExpr<EX_StructConst>(asset,
            sc => ImportName(asset, sc.Struct) == "LinearColor")
            ?? throw new InvalidDataException($"{BattleBaseStem}: no LinearColor template");
        var c = (EX_StructConst)MayhemFixKit.CloneKismet(tpl);
        c.StructSize = 16;
        c.Value = new KismetExpression[]
        {
            new EX_FloatConst { Value = r },
            new EX_FloatConst { Value = g },
            new EX_FloatConst { Value = b },
            new EX_FloatConst { Value = 1.0f },
        };
        return c;
    }

    private static EX_Context FindNameAccess(UAsset asset)
    {
        EX_Context? hit = null;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            MayhemFixKit.Walk(fn.ScriptBytecode, e =>
            {
                if (hit is not null || e is not EX_Context ctx) return;
                var path = ctx.RValuePointer?.New?.Path;
                if (path is { Length: > 0 } && path[^1].ToString() == "CharacterHardcodedName")
                    hit = ctx;
            });
            if (hit is not null) break;
        }
        return (EX_Context?)MayhemFixKit.CloneKismet(hit
            ?? throw new InvalidDataException($"{BattleBaseStem}: CharacterHardcodedName access not found"));
    }

    private static KismetExpression CharacterColor(UAsset asset, KismetPropertyPointer[] matchLocals)
    {
        var fallback = ColorConst(asset, 1.00f, 0.90f, 0.60f);
        KismetExpression next = fallback;
        for (int i = CharacterColors.Length - 1; i >= 0; i--)
        {
            var (_, r, g, b) = CharacterColors[i];
            var sw = new EX_SwitchValue
            {
                IndexTerm = new EX_LocalVariable { Variable = matchLocals[i] },
                Cases = new[]
                {
                    new FKismetSwitchCase(new EX_False(), 0, ColorConst(asset, r, g, b)),
                    new FKismetSwitchCase(new EX_True(), 0, next),
                },
                // A simple default breaks the chain; do not point it at `next`
                // or the tree re-serialises exponentially.
                DefaultTerm = fallback,
                EndGotoOffset = 0,
            };
            next = sw;
        }
        return next;
    }

    private static void Patch(UAsset asset, FunctionExport fn)
    {
        var isAlly = EnsureLocal(asset, fn, AllyLocal, p => p is FBoolProperty);
        var spot = EnsureLocal(asset, fn, SpotLocal, p =>
            p is not FArrayProperty && p is FGenericProperty g &&
            g.SerializedType?.ToString() == "IntProperty" && g.ElementSize == 4);
        var color = EnsureLocal(asset, fn, ColorLocal, p =>
            p is FStructProperty sp && ImportName(asset, sp.Struct) == "LinearColor");
        var location = EnsureLocal(asset, fn, LocationLocal, p =>
            p is FStructProperty vp && ImportName(asset, vp.Struct) == "Vector");
        // Must be typed to NiagaraComponent, not a generic object local: the
        // SetColorParameter virtual dispatch is resolved against this class
        // when the Blueprint links. A wrong/missing class freezes battle load.
        var fx = EnsureLocal(asset, fn, FxLocal, p =>
            p is FObjectProperty op &&
            ImportName(asset, op.PropertyClass) == "NiagaraComponent");

        KismetExpression L(KismetPropertyPointer p) => new EX_LocalVariable { Variable = p };

        var colorTemplate = FindExpr<EX_StructConst>(asset,
            sc => ImportName(asset, sc.Struct) == "LinearColor")
            ?? throw new InvalidDataException($"{BattleBaseStem}: no LinearColor const template");
        if (colorTemplate.Value is null || colorTemplate.Value.Length != 4)
            throw new InvalidDataException($"{BattleBaseStem}: LinearColor const has " +
                $"{colorTemplate.Value?.Length ?? 0} fields (expect 4)");

        KismetExpression Color(float r, float g, float b)
        {
            var c = (EX_StructConst)MayhemFixKit.CloneKismet(colorTemplate);
            c.StructSize = 16;
            c.Value = new KismetExpression[]
            {
                new EX_FloatConst { Value = r },
                new EX_FloatConst { Value = g },
                new EX_FloatConst { Value = b },
                new EX_FloatConst { Value = 1.0f },
            };
            return c;
        }

        // 1. ally gate (targeting groups also cover enemies; leave those alone)
        var allyCall = new EX_LocalVirtualFunction
        {
            VirtualFunctionName = FName.FromString(asset, "IsPlayerTeamCharacter"),
            Parameters = new KismetExpression[] { L(isAlly) },
        };

        // 2. battle slot → stable colour. Each slot gets a different hue so a
        //    row of downed allies is readable at a glance.
        var spotCall = new EX_LocalVirtualFunction
        {
            VirtualFunctionName = FName.FromString(asset, "GetBattleSpotIndex"),
            Parameters = new KismetExpression[] { L(spot) },
        };
        var colorSwitch = new EX_SwitchValue
        {
            IndexTerm = L(spot),
            DefaultTerm = Color(1.00f, 0.90f, 0.60f),
            Cases = SlotColors.Select((c, i) => new FKismetSwitchCase(
                new EX_IntConst { Value = i }, 0, Color(c.R, c.G, c.B))).ToArray(),
            EndGotoOffset = 0,
        };
        var colorLet = new EX_Let
        {
            Value = color,
            Variable = L(color),
            Expression = colorSwitch,
        };

        // 3. glow at the actor, raised enough to clear a downed body.
        // Zero parameters: see ApplyMinimal. Passing EX_Self here corrupts the
        // native thunk's code pointer and hangs the targeting UI.
        var actorLocation = new EX_FinalFunction
        {
            StackNode = Import(asset, "K2_GetActorLocation"),
            Parameters = Array.Empty<KismetExpression>(),
        };
        var raisedLocation = new EX_CallMath
        {
            StackNode = Import(asset, "Add_VectorVector"),
            Parameters = new KismetExpression[]
            {
                actorLocation,
                new EX_VectorConst { Value = new FVector(0.0f, 0.0f, 70.0f) },
            },
        };
        var locationLet = new EX_Let
        {
            Value = location,
            Variable = L(location),
            Expression = raisedLocation,
        };

        // 4. one-shot, auto-destroying Niagara. FX_HealSmall exposes User.Color.
        var fxImport = LuneSkillsKit.EnsureObjectImport(asset, FxPackage, FxName,
            FxClass, FxClassPackage);
        var spawnCall = new EX_CallMath
        {
            StackNode = Import(asset, "SpawnSystemAtLocation"),
            Parameters = new KismetExpression[]
            {
                new EX_Self(),
                new EX_ObjectConst { Value = fxImport },
                L(location),
                new EX_RotationConst { Value = new FRotator(0.0f, 0.0f, 0.0f) },
                new EX_VectorConst { Value = new FVector(1.3f, 1.3f, 1.3f) },
                new EX_True(),  // auto destroy
                new EX_True(),  // pre-cull check
                new EX_ByteConst { Value = 0 }, // no pooling
                new EX_True(),  // auto activate
            },
        };
        var spawnLet = new EX_LetObj
        {
            VariableExpression = L(fx),
            AssignmentExpression = spawnCall,
        };

        var setColor = new EX_Context
        {
            ObjectExpression = L(fx),
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
            ContextExpression = new EX_VirtualFunction
            {
                VirtualFunctionName = FName.FromString(asset, "SetColorParameter"),
                Parameters = new KismetExpression[]
                {
                    new EX_NameConst { Value = FName.FromString(asset, "Color") },
                    L(color),
                },
            },
        };
        setColor.Offset = (uint)setColor.ContextExpression.GetSize(asset);

        var skip = new EX_JumpIfNot
        {
            CodeOffset = 0,
            BooleanExpression = L(isAlly),
        };

        var block = new List<KismetExpression>
        {
            allyCall,
            skip,
            spotCall,
            colorLet,
            locationLet,
            spawnLet,
            setColor,
        };

        uint blockSize = 0;
        foreach (var stmt in block) blockSize += (uint)stmt.GetSize(asset);
        skip.CodeOffset = blockSize;

        block.AddRange(fn.ScriptBytecode ?? Array.Empty<KismetExpression>());
        fn.ScriptBytecode = block.ToArray();

        NormalizeSwitches(asset, fn);
        MayhemFixKit.RecalcContextSkips(asset, fn);
        PartyFiveKit.VerifyJumps(asset, fn);
    }

    // ── locals ───────────────────────────────────────────────────────────────

    private static KismetPropertyPointer EnsureLocal(
        UAsset asset, FunctionExport fn, string name, Func<FProperty, bool> match)
    {
        var props = (fn.LoadedProperties ?? Array.Empty<FProperty>()).ToList();
        var existing = props.FirstOrDefault(p => p.Name.ToString() == name);
        if (existing is not null && match(existing))
            return LocalPtr(asset, fn, name);
        if (existing is not null)
            props.Remove(existing); // wrong type from an older patch: recreate it

        var tpl = FindTemplateLocal(asset, match)
            ?? throw new InvalidDataException(
                $"{BattleBaseStem}: no same-type local template for {name}");
        var prop = (FProperty)ShallowClone(tpl);
        prop.Name = FName.FromString(asset, name);
        // Locals are not params, but a spawned-component temp keeps the
        // source's CPF_InstancedReference so GC/lifetime semantics match.
        prop.PropertyFlags = tpl.PropertyFlags & EPropertyFlags.CPF_InstancedReference;
        prop.RepIndex = 0;
        NormalizeArrayDim(prop);
        props.Add(prop);
        fn.LoadedProperties = props.ToArray();
        return LocalPtr(asset, fn, name);
    }

    private static FProperty? Local(List<FProperty> props, string name) =>
        props.FirstOrDefault(p => p.Name.ToString() == name);

    private static FProperty? FindTemplateLocal(UAsset asset, Func<FProperty, bool> match)
    {
        foreach (var f in asset.Exports.OfType<FunctionExport>())
            foreach (var p in f.LoadedProperties ?? Array.Empty<FProperty>())
                if (match(p) && p.PropertyFlags == EPropertyFlags.CPF_None)
                    return p;
        foreach (var f in asset.Exports.OfType<FunctionExport>())
            foreach (var p in f.LoadedProperties ?? Array.Empty<FProperty>())
                if (match(p)) return p;
        return null;
    }

    private static void NormalizeArrayDim(FProperty prop)
    {
        prop.ArrayDim = EArrayDim.TArray;
        if (prop is FArrayProperty array && array.Inner is not null)
            NormalizeArrayDim(array.Inner);
    }

    private static KismetPropertyPointer LocalPtr(UAsset asset, FunctionExport fn, string name) =>
        new(new FFieldPath(new[] { FName.FromString(asset, name) },
            FPackageIndex.FromExport(asset.Exports.IndexOf(fn))));

    private static object ShallowClone(object o) =>
        o.GetType().GetMethod("MemberwiseClone",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)!.Invoke(o, null)!;

    // ── expression helpers ───────────────────────────────────────────────────

    private static FPackageIndex Import(UAsset asset, string name)
    {
        for (int i = 0; i < asset.Imports.Count; i++)
            if (asset.Imports[i].ObjectName.ToString() == name)
                return FPackageIndex.FromImport(i);
        throw new InvalidDataException($"{BattleBaseStem}: import {name} missing");
    }

    private static string ImportName(UAsset asset, FPackageIndex? idx) =>
        idx is not null && idx.IsImport() &&
        Math.Abs(idx.Index) - 1 < asset.Imports.Count
            ? asset.Imports[Math.Abs(idx.Index) - 1].ObjectName.ToString()
            : "";

    private static T? FindExpr<T>(UAsset asset, Func<T, bool> match) where T : KismetExpression
    {
        T? hit = null;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            MayhemFixKit.Walk(fn.ScriptBytecode, e =>
            {
                if (hit is null && e is T t && match(t)) hit = t;
            });
            if (hit is not null) return hit;
        }
        return hit;
    }

    /// <summary>
    /// Recomputes every switch's internal flow offsets from the current bytecode
    /// layout (same model as GradientGaugeUiKit): a switch serialises as token,
    /// case count, EndGotoOffset, index term, cases { index term, NextOffset,
    /// case term }, default term.
    /// </summary>
    private static List<(EX_SwitchValue Sw, uint Off)> SwitchLayouts(UAsset asset, FunctionExport fn)
    {
        var found = new List<(EX_SwitchValue Sw, uint Off)>();
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref pos, (e, off) =>
            {
                if (e is EX_SwitchValue sw) found.Add((sw, off));
            });
        return found;
    }

    private static void VisitAll(UAsset asset, FunctionExport fn, Action<KismetExpression> visit)
    {
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref pos, (e, _) => visit(e));
    }

    private static void NormalizeSwitches(UAsset asset, FunctionExport fn)
    {
        foreach (var (sw, start) in SwitchLayouts(asset, fn))
        {
            uint p = start + 1 + 6;
            p += (uint)sw.IndexTerm.GetSize(asset);
            var casePos = new uint[sw.Cases.Length];
            for (int i = 0; i < sw.Cases.Length; i++)
            {
                casePos[i] = p;
                p += (uint)sw.Cases[i].CaseIndexValueTerm.GetSize(asset) + 4;
                p += (uint)sw.Cases[i].CaseTerm.GetSize(asset);
            }
            uint defaultPos = p;
            sw.EndGotoOffset = p + (uint)sw.DefaultTerm.GetSize(asset);
            for (int i = 0; i < sw.Cases.Length; i++)
                sw.Cases[i].NextOffset = i + 1 < sw.Cases.Length ? casePos[i + 1] : defaultPos;
        }
    }

    /// <summary>
    /// Recompute every switch's internal offsets and compare them to the stored
    /// values. A mismatch means the switch model is wrong; the VM would run off
    /// the end of the expression and freeze on the first hover.
    /// </summary>
    private static void VerifySwitchLayouts(UAsset asset, FunctionExport fn)
    {
        foreach (var (sw, start) in SwitchLayouts(asset, fn))
        {
            uint p = start + 1 + 6;
            p += (uint)sw.IndexTerm.GetSize(asset);
            var casePos = new uint[sw.Cases.Length];
            for (int i = 0; i < sw.Cases.Length; i++)
            {
                casePos[i] = p;
                p += (uint)sw.Cases[i].CaseIndexValueTerm.GetSize(asset) + 4;
                p += (uint)sw.Cases[i].CaseTerm.GetSize(asset);
            }
            uint defaultPos = p;
            uint end = p + (uint)sw.DefaultTerm.GetSize(asset);
            if (sw.EndGotoOffset != end)
                throw new InvalidDataException(
                    $"{BattleBaseStem}: switch end {sw.EndGotoOffset} != computed {end}");
            for (int i = 0; i < sw.Cases.Length; i++)
            {
                uint want = i + 1 < sw.Cases.Length ? casePos[i + 1] : defaultPos;
                if (sw.Cases[i].NextOffset != want)
                    throw new InvalidDataException(
                        $"{BattleBaseStem}: switch case {i} next {sw.Cases[i].NextOffset} != computed {want}");
            }
        }
    }

    // ── verification ─────────────────────────────────────────────────────────

    /// <summary>Verifies the persistent (class-variable backed) glow patch.</summary>
    public static void VerifyPersistent(string path, UAsset? opened = null)
    {
        var asset = opened ?? Open(path);
        var start = FindEvent(asset);
        var end = FindEvent(asset, "OnTargetingHoverEnd");

        var cls = asset.Exports.OfType<ClassExport>()
            .FirstOrDefault(c => c.ObjectName.ToString() == BattleBaseStem + "_C")
            ?? throw new InvalidDataException($"{BattleBaseStem}: ClassExport missing");
        var prop = (cls.LoadedProperties ?? Array.Empty<FProperty>())
            .OfType<FObjectProperty>().FirstOrDefault(p => p.Name.ToString() == GlowComponent);
        if (prop is null || ImportName(asset, prop.PropertyClass) != "NiagaraComponent")
            throw new InvalidDataException($"{BattleBaseStem}: {GlowComponent} class variable missing/wrong type");
        if ((prop.PropertyFlags & EPropertyFlags.CPF_InstancedReference) == 0)
            throw new InvalidDataException($"{BattleBaseStem}: {GlowComponent} lost CPF_InstancedReference");
        if ((start.LoadedProperties ?? Array.Empty<FProperty>())
            .OfType<FStructProperty>().FirstOrDefault(p => p.Name.ToString() == ColorLocal) is not { } colorLocal ||
            ImportName(asset, colorLocal.Struct) != "LinearColor")
            throw new InvalidDataException($"{BattleBaseStem}: {ColorLocal} local missing/wrong type");

        int ally = 0, spawns = 0, colors = 0, scales = 0, activates = 0, valid = 0, destroys = 0, nameSwitches = 0;
        bool fx = false;
        EX_CallMath? spawnCall = null;
        var switchColors = new HashSet<string>();

        VisitAll(asset, start, e =>
        {
            switch (e)
            {
                case EX_LocalVirtualFunction vf
                    when vf.VirtualFunctionName.ToString() == "IsPlayerTeamCharacter":
                    ally++;
                    break;
                case EX_CallMath cm when ImportName(asset, cm.StackNode) == "SpawnSystemAttached":
                    spawns++;
                    spawnCall = cm;
                    break;
                case EX_ObjectConst oc when ImportName(asset, oc.Value) == GlowFx:
                    fx = true;
                    break;
                case EX_VirtualFunction vf when vf.VirtualFunctionName.ToString() == "SetColorParameter":
                    colors++;
                    break;
                case EX_VirtualFunction vf when vf.VirtualFunctionName.ToString() == "SetRelativeScale3D":
                    scales++;
                    break;
                case EX_VirtualFunction vf when vf.VirtualFunctionName.ToString() == "Activate":
                    activates++;
                    break;
                case EX_SwitchValue sw when sw.Cases.Length == 2:
                    nameSwitches++;
                    foreach (var c in sw.Cases)
                        if (c.CaseTerm is EX_StructConst sc)
                            switchColors.Add(string.Join(",",
                                sc.Value.OfType<EX_FloatConst>().Select(f => f.Value)));
                    break;
            }
        });
        VisitAll(asset, end, e =>
        {
            if (e is EX_CallMath cm && ImportName(asset, cm.StackNode) == "IsValid") valid++;
            if (e is EX_FinalFunction ff &&
                ImportName(asset, ff.StackNode) == "K2_DestroyComponent") destroys++;
        });

        if (ally != 1 || spawns != 1 || colors != 2 || scales != 1 || activates != 1 ||
            valid < 1 || destroys < 1 || nameSwitches != CharacterColors.Length || !fx)
            throw new InvalidDataException(
                $"{BattleBaseStem}: persistent glow incomplete " +
                $"(ally={ally} spawn={spawns} setColor={colors} scale={scales} " +
                $"activate={activates} valid={valid} destroy={destroys} " +
                $"switches={nameSwitches} fx={fx})");

        if (spawnCall is null || spawnCall.Parameters.Length != 10 ||
            spawnCall.Parameters[6] is not EX_False ||
            spawnCall.Parameters[7] is not EX_False ||
            spawnCall.Parameters[9] is not EX_False)
            throw new InvalidDataException(
                $"{BattleBaseStem}: glow is not a persistent, colour-then-activate SpawnSystemAttached");

        if (switchColors.Count != CharacterColors.Length + 1)
            throw new InvalidDataException(
                $"{BattleBaseStem}: {switchColors.Count} character colours " +
                $"(expect {CharacterColors.Length + 1})");

        foreach (var fn in new[] { start, end })
        {
            VerifySwitchLayouts(asset, fn);
            VerifyCallArity(asset, fn);
            var starts = StatementStarts(asset, fn.ScriptBytecode!);
            foreach (var st in fn.ScriptBytecode!)
                if (st is EX_JumpIfNot jump && !starts.Contains(jump.CodeOffset))
                    throw new InvalidDataException($"{BattleBaseStem}: glow jump target invalid");
        }

        VerifyUbergraphTail(asset, start, EventName);
        VerifyUbergraphTail(asset, end, "OnTargetingHoverEnd");
    }

    private static void VerifyUbergraphTail(UAsset asset, FunctionExport fn, string eventName)
    {
        var tail = fn.ScriptBytecode!.OfType<EX_LocalFinalFunction>().FirstOrDefault();
        if (tail?.Parameters is not { Length: 1 } ||
            tail.Parameters[0] is not EX_IntConst entry)
            throw new InvalidDataException($"{BattleBaseStem}: {eventName} no longer dispatches to the ubergraph");
        var uber = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "ExecuteUbergraph_" + BattleBaseStem);
        if (uber?.ScriptBytecode is null ||
            !StatementStarts(asset, uber.ScriptBytecode).Contains((uint)entry.Value))
            throw new InvalidDataException(
                $"{BattleBaseStem}: {eventName} entry {entry.Value} is not a ubergraph statement start");
    }

    /// <summary>Re-opens the staged asset and asserts the injected block survived.</summary>
    public static void Verify(string path, UAsset? opened = null)
    {
        var asset = opened ?? Open(path);
        var fn = FindEvent(asset);
        var localProps = (fn.LoadedProperties ?? Array.Empty<FProperty>()).ToList();
        var locals = localProps.Select(p => p.Name.ToString()).ToHashSet();
        foreach (var required in new[] { AllyLocal, SpotLocal, ColorLocal, LocationLocal, FxLocal })
            if (!locals.Contains(required))
                throw new InvalidDataException($"{BattleBaseStem}: local {required} missing");

        // Type/owner checks matter as much as names: a stale patch with the
        // right local names but the wrong object class hangs Blueprint link.
        if (Local(localProps, AllyLocal) is not FBoolProperty)
            throw new InvalidDataException($"{BattleBaseStem}: {AllyLocal} is not a bool");
        if (Local(localProps, SpotLocal) is not FGenericProperty spotProp ||
            spotProp.SerializedType?.ToString() != "IntProperty" || spotProp.ElementSize != 4)
            throw new InvalidDataException($"{BattleBaseStem}: {SpotLocal} is not an int");
        if (Local(localProps, ColorLocal) is not FStructProperty colorProp ||
            ImportName(asset, colorProp.Struct) != "LinearColor")
            throw new InvalidDataException($"{BattleBaseStem}: {ColorLocal} is not LinearColor");
        if (Local(localProps, LocationLocal) is not FStructProperty locProp ||
            ImportName(asset, locProp.Struct) != "Vector")
            throw new InvalidDataException($"{BattleBaseStem}: {LocationLocal} is not Vector");
        if (Local(localProps, FxLocal) is not FObjectProperty fxProp ||
            ImportName(asset, fxProp.PropertyClass) != "NiagaraComponent")
            throw new InvalidDataException($"{BattleBaseStem}: {FxLocal} is not NiagaraComponent");
        if ((fxProp.PropertyFlags & EPropertyFlags.CPF_InstancedReference) == 0)
            throw new InvalidDataException($"{BattleBaseStem}: {FxLocal} lost CPF_InstancedReference");

        int allyCalls = 0, spotCalls = 0, colors = 0, spawns = 0, switches = 0;
        bool fxImport = false;
        EX_SwitchValue? colorSwitch = null;

        MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            switch (e)
            {
                case EX_LocalVirtualFunction vf
                    when vf.VirtualFunctionName.ToString() == "IsPlayerTeamCharacter":
                    allyCalls++;
                    break;
                case EX_LocalVirtualFunction vf
                    when vf.VirtualFunctionName.ToString() == "GetBattleSpotIndex":
                    spotCalls++;
                    break;
                case EX_VirtualFunction vf
                    when vf.VirtualFunctionName.ToString() == "SetColorParameter":
                    colors++;
                    break;
                case EX_CallMath cm when ImportName(asset, cm.StackNode) == "SpawnSystemAtLocation":
                    spawns++;
                    break;
                case EX_ObjectConst oc when ImportName(asset, oc.Value) == FxName:
                    fxImport = true;
                    break;
                case EX_SwitchValue sw when sw.Cases.Length == SlotColors.Length:
                    switches++;
                    if (colorSwitch is null) colorSwitch = sw;
                    break;
            }
        });

        if (allyCalls != 1 || spotCalls != 1 || colors != 1 || spawns != 1 || switches != 1 || !fxImport)
            throw new InvalidDataException(
                $"{BattleBaseStem}: target glow block incomplete " +
                $"(ally={allyCalls} spot={spotCalls} setColor={colors} spawn={spawns} " +
                $"switches={switches} fx={fxImport})");

        var starts = StatementStarts(asset, fn.ScriptBytecode!);
        foreach (var st in fn.ScriptBytecode!)
        {
            if (st is EX_JumpIfNot jump && !starts.Contains(jump.CodeOffset))
                throw new InvalidDataException(
                    $"{BattleBaseStem}: target glow skip points at non-statement offset {jump.CodeOffset}");
        }
        VerifySwitchLayouts(asset, fn);
        VerifyCallArity(asset, fn);

        // The stock event tail must still dispatch into the ubergraph.
        var tail = fn.ScriptBytecode!.OfType<EX_LocalFinalFunction>().FirstOrDefault();
        if (tail?.Parameters is not { Length: 1 } ||
            tail.Parameters[0] is not EX_IntConst entry)
            throw new InvalidDataException($"{BattleBaseStem}: {EventName} no longer dispatches to the ubergraph");
        var uber = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() ==
                "ExecuteUbergraph_" + BattleBaseStem);
        if (uber?.ScriptBytecode is null || !StatementStarts(asset, uber.ScriptBytecode).Contains((uint)entry.Value))
            throw new InvalidDataException(
                $"{BattleBaseStem}: {EventName} entry {entry.Value} is not a ubergraph statement start");

        if (colorSwitch is not null)
        {
            var shades = colorSwitch.Cases.Select(c => c.CaseTerm)
                .OfType<EX_StructConst>()
                .Select(c => string.Join(",", c.Value.OfType<EX_FloatConst>().Select(f => f.Value)))
                .Distinct().Count();
            if (shades != SlotColors.Length)
                throw new InvalidDataException(
                    $"{BattleBaseStem}: {shades} distinct target colours (expect {SlotColors.Length})");
        }
    }

    private static HashSet<uint> StatementStarts(UAsset asset, KismetExpression[] bytecode)
    {
        var starts = new HashSet<uint>();
        uint off = 0;
        foreach (var st in bytecode)
        {
            starts.Add(off);
            off += (uint)st.GetSize(asset);
        }
        return starts;
    }
}

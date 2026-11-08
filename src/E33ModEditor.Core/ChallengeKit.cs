using System.Reflection;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace E33ModEditor.Core;

/// <summary>
/// Expands the game's save-point "Challenges" menu:
///   • Enemy Max Health gains 500× / 1000× / 2000× / 3000× / 5000× / 10000×.
///   • New Enemy Defense (×2/5/10/25/50/75/100) and Enemy Speed (×2/3/5/10)
///     challenges, wired through the same numeric DataAsset + modifier-buff
///     pipeline the vanilla health challenge uses.
///
/// The health challenge is data-only (the modifier BP calls
/// <c>Challenge_SetEnemyHealthMutliplier</c>). Defense and speed are read from
/// the difficulty manager <b>inside the existing</b>
/// <c>Challenge_ApplyEnemyStatChanges</c> function, which already owns the
/// enemy stat map. That function's locals and bytecode are extended; the
/// BattleManager class/CDO is deliberately left untouched because UAssetAPI
/// cannot fully parse it (missing <c>S_BattleInitialState</c> schema) and
/// re-serializing its CDO with new class properties crashes the game on load.
/// </summary>
public static class ChallengeKit
{
    public const string Toggle = "Challenges";

    private const string ChallengesRoot = "challenges";
    private const string ChallengesContent = "Sandfall/Content/Gameplay/Battle/Challenges/Content";
    private const string DifficultyAsset = "BP_GameDifficultyManager";
    private const string BattleManagerAsset = "AC_jRPG_BattleManager";
    private const string ListWidgetAsset = "WBP_ChallengesListWidget";
    private const string EntryWidgetAsset = "WBP_ChallengeEntry";
    private const string StringTableAsset = "ST_UI_GameMenu";

    private const string HealthDaStem = "DA_BattleChallenge_EnemyLifeMultiplier";
    private const string HealthBuffStem = "BP_BattleModifierBuff_EnemiesHPMultiplier";
    private const string DefenseDaStem = "DA_BattleChallenge_EnemyDefenseMultiplier";
    private const string SpeedDaStem = "DA_BattleChallenge_EnemySpeedMultiplier";
    private const string BaseBuffPackage = "/Game/Gameplay/Battle/Challenges/BP_BattleModifierBuff_Challenge_Base";
    private const string BaseBuffClass = "BP_BattleModifierBuff_Challenge_Base_C";

    private const string HealthChallengeId = "BATTLECHALLENGE_ENEMYLIFEMULT";
    private const string DefenseChallengeId = "BATTLECHALLENGE_ENEMYDEFENSEMULT";
    private const string SpeedChallengeId = "BATTLECHALLENGE_ENEMYSPEEDMULT";

    private const string HealthNameKey = "ST_GM_FLAG_CHALLENGE_LIFE_MULTIPLIER";
    private const string DefenseNameKey = "ST_GM_FLAG_CHALLENGE_DEFENSE_MULTIPLIER";
    private const string SpeedNameKey = "ST_GM_FLAG_CHALLENGE_SPEED_MULTIPLIER";

    private const string ClassPackage = "/Game/Gameplay/Battle/Challenges/BP_DataAsset_BattleChallenge_Numeric";
    private const string ClassName = "BP_DataAsset_BattleChallenge_Numeric_C";

    /// <summary>Values appended to the vanilla health challenge (2/5/10/20/50/100).</summary>
    public static readonly double[] ExtraHealthMultipliers =
        { 500, 1000, 2000, 3000, 5000, 10000 };
    public static readonly double[] DefenseMultipliers =
        { 2, 5, 10, 25, 50, 75, 100 };
    public static readonly double[] SpeedMultipliers =
        { 2, 3, 5, 10 };

    public sealed record Result(int Edits, string HealthDa, string DefenseDa, string SpeedDa);

    public static Result Apply(string modDir, Action<string>? log = null)
    {
        var root = PictosCreateKit.EnsureExtractedShared(ChallengesRoot,
            "BP_DataAsset_BattleChallenge",
            "BP_DataAsset_BattleChallenge_Numeric",
            HealthDaStem,
            HealthBuffStem,
            BattleManagerAsset,
            DifficultyAsset,
            ListWidgetAsset,
            EntryWidgetAsset,
            StringTableAsset);
        RegisterDataAssetSchemas(root);

        var contentDir = Path.Combine(modDir, ChallengesContent);
        Directory.CreateDirectory(contentDir);

        var edits = 0;
        log?.Invoke("[challenges] extending Enemy Max Health options…");
        var healthDa = StageChallengesAsset(root, modDir, HealthDaStem + ".uasset", ChallengesContent);
        edits += ExtendHealth(healthDa);

        log?.Invoke("[challenges] creating Enemy Defense / Speed challenges…");
        // The base modifier BP is a valid, existing class whose LoadNumericValue
        // is already empty. Pointing the new DAs at it avoids adding/cloning
        // modifier assets; the stat application lives in Challenge_ApplyEnemyStatChanges.
        var defenseDa = StageClone(root, modDir, HealthDaStem + ".uasset", DefenseDaStem,
            edit: da =>
            {
                SetChallenge(da, DefenseChallengeId, DefenseNameKey,
                    BaseBuffPackage, BaseBuffClass, DefenseMultipliers);
            });
        var speedDa = StageClone(root, modDir, HealthDaStem + ".uasset", SpeedDaStem,
            edit: da =>
            {
                SetChallenge(da, SpeedChallengeId, SpeedNameKey,
                    BaseBuffPackage, BaseBuffClass, SpeedMultipliers);
            });

        log?.Invoke("[challenges] patching BattleManager stat application…");
        edits += PatchBattleManager(root, modDir);

        log?.Invoke("[challenges] registering challenge assets…");
        edits += PatchDifficultyManager(root, modDir);

        log?.Invoke("[challenges] adding menu rows…");
        edits += PatchListWidget(root, modDir);

        log?.Invoke("[challenges] adding menu text…");
        edits += AddStrings(root, modDir);

        Verify(modDir);

        return new Result(edits,
            Path.GetFileName(healthDa), Path.GetFileName(defenseDa), Path.GetFileName(speedDa));
    }

    /// <summary>Re-opens the staged assets and checks the pieces the menu and
    /// battle manager depend on. Throws when a pack would ship a half-patch.</summary>
    public static void Verify(string modDir)
    {
        var errors = new List<string>();
        var content = Path.Combine(modDir, ChallengesContent);

        foreach (var (stem, challengeId, expected) in new[]
                 {
                     (HealthDaStem, HealthChallengeId, 12),
                     (DefenseDaStem, DefenseChallengeId, DefenseMultipliers.Length),
                     (SpeedDaStem, SpeedChallengeId, SpeedMultipliers.Length),
                 })
        {
            var path = Path.Combine(content, stem + ".uasset");
            if (!File.Exists(path)) { errors.Add(stem + ": missing"); continue; }
            try
            {
                var da = OpenDataAsset(path);
                var export = DataAssetExport(da);
                var id = export.Data.OfType<NamePropertyData>()
                    .FirstOrDefault(p => Clean(p.Name) == "ChallengeID")?.Value.ToString();
                var map = export.Data.OfType<MapPropertyData>()
                    .FirstOrDefault(p => Clean(p.Name) == "AvailableValues");
                if (id != challengeId) errors.Add($"{stem}: ChallengeID={id ?? "<null>"}");
                if (map is null || map.Value.Count != expected)
                    errors.Add($"{stem}: {map?.Value.Count ?? 0} options (expected {expected})");
                if (stem != HealthDaStem)
                {
                    var buff = export.Data.OfType<SoftObjectPropertyData>()
                        .FirstOrDefault(p => Clean(p.Name) == "BattleModifierClass");
                    if (buff is null || buff.Value.AssetPath.PackageName.ToString() != BaseBuffPackage)
                        errors.Add($"{stem}: BattleModifierClass not the challenge base");
                }
            }
            catch (Exception ex) { errors.Add($"{stem}: {ex.Message}"); }
        }

        var managerPath = Path.Combine(modDir, "Sandfall/Content/Gameplay/Difficulty", DifficultyAsset + ".uasset");
        try
        {
            var manager = new UAsset(managerPath, EngineVersion.VER_UE5_4, DataTableEditor.Mappings);
            var cdo = manager.Exports.OfType<NormalExport>()
                .First(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
            var assets = (ArrayPropertyData)cdo.Data.First(p => Clean(p.Name) == "ChallengeAssets");
            if (assets.Value.Length < 4)
                errors.Add($"difficulty manager: {assets.Value.Length} challenge asset(s) (expected ≥4)");
        }
        catch (Exception ex) { errors.Add("difficulty manager: " + ex.Message); }

        var battlePath = FileCopy.FindUnder(modDir, BattleManagerAsset + ".uasset");
        try
        {
            var battle = new UAsset(battlePath!, EngineVersion.VER_UE5_4, DataTableEditor.Mappings);
            var apply = battle.Exports.OfType<FunctionExport>()
                .FirstOrDefault(f => f.ObjectName.ToString() == "Challenge_ApplyEnemyStatChanges");
            if (apply is null || apply.ScriptBytecodeSize < 1000)
                errors.Add("battle manager: stat apply blocks missing");
            else
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                var bytes = new HashSet<byte>();
                CollectNamesAndBytes(apply.ScriptBytecode, names, bytes);
                if (!names.Contains(DefenseChallengeId)) errors.Add("battle manager: defense challenge lookup missing");
                if (!names.Contains(SpeedChallengeId)) errors.Add("battle manager: speed challenge lookup missing");
                if (!bytes.Contains(6)) errors.Add("battle manager: defense stat key missing");
                if (!bytes.Contains(8)) errors.Add("battle manager: speed stat key missing");
            }
        }
        catch (Exception ex) { errors.Add("battle manager: " + ex.Message); }

        var widgetPath = FileCopy.FindUnder(modDir, ListWidgetAsset + ".uasset");
        try
        {
            var widget = new UAsset(widgetPath!, EngineVersion.VER_UE5_4, DataTableEditor.Mappings);
            var names = widget.GetNameMapIndexList().Select(n => n.Value).ToHashSet(StringComparer.Ordinal);
            if (!names.Contains("DefenseMultiplierEntry")) errors.Add("challenge list: defense row missing");
            if (!names.Contains("SpeedMultiplierEntry")) errors.Add("challenge list: speed row missing");
        }
        catch (Exception ex) { errors.Add("challenge list: " + ex.Message); }

        var stPath = FileCopy.FindUnder(modDir, StringTableAsset + ".uasset");
        try
        {
            var strings = DataTableEditor.OpenStringTable(stPath!);
            if (!strings.ContainsKey(DefenseNameKey)) errors.Add("string table: defense name missing");
            if (!strings.ContainsKey(SpeedNameKey)) errors.Add("string table: speed name missing");
        }
        catch (Exception ex) { errors.Add("string table: " + ex.Message); }

        if (errors.Count > 0)
            throw new InvalidDataException("challenge verification failed: " + string.Join(" | ", errors));
    }

    private static string StageChallengesAsset(string root, string modDir, string fileName, string relativeDir)
    {
        var src = FileCopy.FindUnder(root, fileName)
            ?? throw new FileNotFoundException("challenge donor missing: " + fileName);
        var dest = Path.Combine(modDir, relativeDir, fileName);
        if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);
        return dest;
    }

    private static string StageClone(string root, string modDir, string srcName, string newStem,
        Action<UAsset>? edit = null)
    {
        var src = FileCopy.FindUnder(root, srcName)
            ?? throw new FileNotFoundException("challenge donor missing: " + srcName);
        var dest = Path.Combine(modDir, ChallengesContent, newStem + ".uasset");
        FileCopy.AssetPair(src, dest);

        var map = DataTableEditor.Mappings;
        var asset = new UAsset(dest, EngineVersion.VER_UE5_4, map);
        var oldStem = Path.GetFileNameWithoutExtension(srcName);
        PictosCreateKit.RetargetPackage(asset, dest, oldStem, newStem);

        edit?.Invoke(asset);
        asset.Write(dest);
        return newStem;
    }

    private static void RegisterDataAssetSchemas(string root)
    {
        var map = DataTableEditor.Mappings;
        foreach (var stem in new[] { "BP_DataAsset_BattleChallenge", "BP_DataAsset_BattleChallenge_Numeric" })
        {
            var path = FileCopy.FindUnder(root, stem + ".uasset");
            if (path is null) continue;
            var asset = new UAsset(path, EngineVersion.VER_UE5_4, map);
            foreach (var ce in asset.Exports.OfType<ClassExport>())
            {
                var schema = Usmap.GetSchemaFromStructExport(ce, true);
                if (schema is not null) map.Schemas[schema.Name] = schema;
            }
        }
    }

    private static UAsset OpenDataAsset(string path)
    {
        var map = DataTableEditor.Mappings;
        return new UAsset(path, EngineVersion.VER_UE5_4, map);
    }

    // ───────────────────────────── health DA ─────────────────────────────

    private static int ExtendHealth(string daPath)
    {
        var da = OpenDataAsset(daPath);
        var export = DataAssetExport(da);
        var mapProp = (MapPropertyData)export.Data.First(p => Clean(p.Name) == "AvailableValues");
        int added = 0;
        foreach (var value in ExtraHealthMultipliers)
        {
            var id = "MULT_" + value.ToString("0");
            if (mapProp.Value.Any(kv => Clean(((NamePropertyData)kv.Key).Value.ToString()) == id)) continue;
            mapProp.Value.Add(NameProp(da, "AvailableValues", id), DoubleProp(da, "AvailableValues", value));
            added++;
        }
        if (added > 0) da.Write(daPath);
        return added;
    }

    private static void SetChallenge(UAsset da, string challengeId, string nameKey,
        string buffPackage, string buffClass, double[] values)
    {
        var export = DataAssetExport(da);
        foreach (var p in export.Data)
        {
            var n = Clean(p.Name);
            if (n == "ChallengeID" && p is NamePropertyData id)
                id.Value = FName.FromString(da, challengeId);
            else if (n == "Name" && p is TextPropertyData name)
                name.Value = FString.FromString(nameKey);
            else if (n == "BattleModifierClass" && p is SoftObjectPropertyData cls)
            {
                cls.Value = new FSoftObjectPath(
                    FName.FromString(da, buffPackage),
                    FName.FromString(da, buffClass),
                    null);
            }
            else if (n == "AvailableValues" && p is MapPropertyData map)
            {
                map.Value.Clear();
                foreach (var value in values)
                {
                    var optId = "MULT_" + value.ToString("0");
                    map.Value.Add(NameProp(da, "AvailableValues", optId), DoubleProp(da, "AvailableValues", value));
                }
            }
        }
    }

    private static NormalExport DataAssetExport(UAsset da) =>
        da.Exports.OfType<NormalExport>()
          .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("DA_", StringComparison.Ordinal))
        ?? throw new InvalidDataException("no DataAsset export in " + da.FolderName);

    private static NamePropertyData NameProp(UAsset asset, string propName, string value) =>
        new(FName.FromString(asset, propName)) { Value = FName.FromString(asset, value) };

    private static DoublePropertyData DoubleProp(UAsset asset, string propName, double value) =>
        new(FName.FromString(asset, propName)) { Value = value };

    // ──────────────────────── BattleManager bytecode ────────────────────────

    private static int PatchBattleManager(string root, string modDir)
    {
        var path = FileCopy.FindUnder(modDir, BattleManagerAsset + ".uasset");
        if (path is null)
        {
            var src = FileCopy.FindUnder(root, BattleManagerAsset + ".uasset")
                ?? FileCopy.FindTablesAsset(BattleManagerAsset + ".uasset")
                ?? throw new FileNotFoundException(BattleManagerAsset);
            var rel = src[src.IndexOf("Sandfall", StringComparison.Ordinal)..];
            path = Path.Combine(modDir, rel);
            FileCopy.AssetPair(src, path);
        }

        var map = DataTableEditor.Mappings;
        var asset = new UAsset(path, EngineVersion.VER_UE5_4, map);
        var applyFn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "Challenge_ApplyEnemyStatChanges")
            ?? throw new InvalidDataException("BattleManager: Challenge_ApplyEnemyStatChanges missing");

        if (ContainsName(applyFn.ScriptBytecode, DefenseChallengeId))
            return 0; // already patched

        // Read the selected options straight from the difficulty manager inside
        // the existing apply function. This avoids adding class variables or
        // function exports to AC_jRPG_BattleManager: that class cannot be fully
        // parsed by UAssetAPI (missing S_BattleInitialState schema), and
        // re-serializing its CDO corrupts the cooked package.
        var helperImport = EnsureObjectImport(asset,
            "/Game/Gameplay/Difficulty/BP_FL_GameDifficultyHelpers",
            "Default__BP_FL_GameDifficultyHelpers_C",
            "BP_FL_GameDifficultyHelpers_C",
            "/Game/Gameplay/Difficulty/BP_FL_GameDifficultyHelpers");
        int isValidImport = Enumerable.Range(0, asset.Imports.Count)
            .First(i => asset.Imports[i].ObjectName.ToString() == "IsValid"
                        && asset.Imports[i].ClassName.ToString() == "Object");
        var isValidFn = FPackageIndex.FromImport(isValidImport);

        var fnRef = asset.Exports.IndexOf(applyFn) + 1;
        var objectTpl = asset.Exports.OfType<FunctionExport>()
            .First(f => f.ObjectName.ToString() == "TryNotifyChallenges")
            .LoadedProperties.First(p => Clean(p.Name) == "CallFunc_GetDifficultyManager_ReturnValue");
        var challengeTpl = asset.Exports.OfType<FunctionExport>()
            .First(f => f.ObjectName.ToString() == "TryNotifyChallenges")
            .LoadedProperties.First(p => Clean(p.Name) == "CallFunc_Map_Find_Value");
        var boolTpl = applyFn.LoadedProperties.First(p => Clean(p.Name) == "CallFunc_Map_Find_ReturnValue");
        var nameTpl = asset.Exports.OfType<FunctionExport>()
            .First(f => f.ObjectName.ToString() == "TryNotifyChallenges")
            .LoadedProperties.First(p => Clean(p.Name) == "CallFunc_GetSelectedChallengeOption_OptionID");
        var doubleTpl = applyFn.LoadedProperties.First(p => Clean(p.Name) == "CallFunc_Multiply_DoubleDouble_ReturnValue");

        var locals = new[]
        {
            CloneLocal(objectTpl, asset, "ChallengeKit_Manager"),
            CloneLocal(boolTpl, asset, "ChallengeKit_Found"),
            CloneLocal(nameTpl, asset, "ChallengeKit_Option"),
            CloneLocal(challengeTpl, asset, "ChallengeKit_Challenge"),
            CloneLocal(boolTpl, asset, "ChallengeKit_FoundValue"),
            CloneLocal(doubleTpl, asset, "ChallengeKit_Value"),
        };
        applyFn.LoadedProperties = applyFn.LoadedProperties.Concat(locals).ToArray();

        var bc = applyFn.ScriptBytecode.ToList();
        var healthBody = bc.Take(9).ToList(); // condition + health modifications
        var tail = bc.Skip(11).ToList();       // OutStats = InStats; return; end

        var defenseBlock = BuildChallengeApplyBlock(asset, applyFn, fnRef, helperImport, isValidFn,
            DefenseChallengeId, 6); // E_jRPG_StatType::NewEnumerator4
        var speedBlock = BuildChallengeApplyBlock(asset, applyFn, fnRef, helperImport, isValidFn,
            SpeedChallengeId, 8);   // E_jRPG_StatType::NewEnumerator7

        int defenseStart = Size(healthBody, asset);
        int defenseEnd = defenseStart + Size(defenseBlock, asset);
        int speedEnd = defenseEnd + Size(speedBlock, asset);

        // Health inactive -> skip its block, continue with Defense.
        healthBody.OfType<EX_JumpIfNot>().First().CodeOffset = (uint)defenseStart;
        // Defense gates -> its block end (Speed start); Speed gates -> tail.
        foreach (var gate in defenseBlock.OfType<EX_JumpIfNot>())
            gate.CodeOffset = (uint)defenseEnd;
        foreach (var gate in speedBlock.OfType<EX_JumpIfNot>())
            gate.CodeOffset = (uint)speedEnd;

        var newBc = new List<KismetExpression>();
        newBc.AddRange(healthBody);
        newBc.AddRange(defenseBlock);
        newBc.AddRange(speedBlock);
        newBc.AddRange(tail.Select(CloneExpression));
        applyFn.ScriptBytecode = newBc.ToArray();
        applyFn.ScriptBytecodeSize = Size(newBc, asset);
        asset.Write(path);
        return 1;
    }

    private static List<KismetExpression> BuildChallengeApplyBlock(UAsset asset, FunctionExport fn,
        int fnRef, FPackageIndex helperImport, FPackageIndex isValidFn, string challengeId, byte statKey)
    {
        var block = new List<KismetExpression>();

        // Manager = GetDifficultyManager(Self)
        block.Add(new EX_LetObj
        {
            VariableExpression = Local(asset, "ChallengeKit_Manager", fnRef),
            AssignmentExpression = Context(
                new EX_ObjectConst { Value = helperImport },
                new EX_LocalVirtualFunction
                {
                    VirtualFunctionName = FName.FromString(asset, "GetDifficultyManager"),
                    Parameters = new KismetExpression[] { new EX_Self() },
                }),
        });

        // if (!IsValid(Manager)) skip this challenge
        block.Add(LetBool(asset, "ChallengeKit_Found", fnRef,
            new EX_CallMath
            {
                StackNode = isValidFn,
                Parameters = new KismetExpression[] { Local(asset, "ChallengeKit_Manager", fnRef) },
            }));
        block.Add(new EX_JumpIfNot
        {
            BooleanExpression = Local(asset, "ChallengeKit_Found", fnRef),
            CodeOffset = 0,
        });

        // Manager.GetSelectedChallengeOption(ChallengeID, out Found, out OptionID)
        block.Add(CallOnLocal(asset, "ChallengeKit_Manager", fnRef,
            "GetSelectedChallengeOption",
            new EX_NameConst { Value = FName.FromString(asset, challengeId) },
            Local(asset, "ChallengeKit_Found", fnRef),
            Local(asset, "ChallengeKit_Option", fnRef)));

        block.Add(new EX_JumpIfNot
        {
            BooleanExpression = Local(asset, "ChallengeKit_Found", fnRef),
            CodeOffset = 0,
        });

        // Challenge = Manager.FindChallengeDefinition(ChallengeID)
        block.Add(new EX_LetObj
        {
            VariableExpression = Local(asset, "ChallengeKit_Challenge", fnRef),
            AssignmentExpression = CallOnLocal(asset, "ChallengeKit_Manager", fnRef,
                "FindChallengeDefinition",
                new EX_NameConst { Value = FName.FromString(asset, challengeId) }),
        });

        // if (!IsValid(Challenge)) skip this challenge
        block.Add(LetBool(asset, "ChallengeKit_FoundValue", fnRef,
            new EX_CallMath
            {
                StackNode = isValidFn,
                Parameters = new KismetExpression[] { Local(asset, "ChallengeKit_Challenge", fnRef) },
            }));
        block.Add(new EX_JumpIfNot
        {
            BooleanExpression = Local(asset, "ChallengeKit_FoundValue", fnRef),
            CodeOffset = 0,
        });

        // Challenge.GetOptionNumericValue(OptionID, out FoundValue, out Value)
        block.Add(CallOnLocal(asset, "ChallengeKit_Challenge", fnRef,
            "GetOptionNumericValue",
            Local(asset, "ChallengeKit_Option", fnRef),
            Local(asset, "ChallengeKit_FoundValue", fnRef),
            Local(asset, "ChallengeKit_Value", fnRef)));

        block.Add(new EX_JumpIfNot
        {
            BooleanExpression = Local(asset, "ChallengeKit_FoundValue", fnRef),
            CodeOffset = 0,
        });

        // Re-use the vanilla health multiply block (Map_Find -> multiply ->
        // Round64 -> Conv -> Map_Add), swapping the multiplier source for the
        // selected challenge value and the stat key for Defense/Speed.
        var mapBlock = fn.ScriptBytecode.Skip(2).Take(7).Select(CloneExpression).ToList();
        foreach (var expression in mapBlock)
            ReplaceHealthStat(expression, asset, fnRef, statKey);
        block.AddRange(mapBlock);
        return block;
    }

    private static KismetExpression CallOnLocal(UAsset asset, string objectLocal, int fnRef,
        string function, params KismetExpression[] parameters) =>
        Context(Local(asset, objectLocal, fnRef),
            new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(asset, function),
                Parameters = parameters,
            });

    /// <summary>EX_Context carries an RValuePointer that the writer dereferences
    /// unconditionally; hand-built contexts need an empty field path.</summary>
    private static EX_Context Context(KismetExpression obj, KismetExpression context) =>
        new()
        {
            ObjectExpression = obj,
            ContextExpression = context,
            RValuePointer = new KismetPropertyPointer(
                new FFieldPath(Array.Empty<FName>(), FPackageIndex.FromRawIndex(0))),
        };

    private static EX_LetBool LetBool(UAsset asset, string name, int fnRef,
        KismetExpression expression) =>
        new()
        {
            VariableExpression = Local(asset, name, fnRef),
            AssignmentExpression = expression,
        };

    private static EX_LocalVariable Local(UAsset asset, string name, int fnRef) =>
        new() { Variable = VarPointer(asset, name, fnRef) };

    private static void ReplaceHealthStat(KismetExpression expression, UAsset asset,
        int fnRef, byte statKey)
    {
        if (expression is EX_ByteConst b) b.Value = statKey;
        if (expression is EX_InstanceVariable iv
            && iv.Variable?.New?.Path is { Length: > 0 }
            && Clean(iv.Variable.New.Path[0].ToString()) == "Challenge_EnemyHealthMultiplier")
        {
            iv.Variable = VarPointer(asset, "ChallengeKit_Value", fnRef);
        }

        for (var t = expression.GetType(); t is not null && t != typeof(object); t = t.BaseType)
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var value = f.GetValue(expression);
                if (value is KismetExpression child)
                    f.SetValue(expression, ReplaceChild(child, asset, fnRef, statKey));
                else if (value is KismetExpression[] array)
                    f.SetValue(expression, array.Select(x => x is null ? null : ReplaceChild(x, asset, fnRef, statKey)).ToArray());
            }
    }

    private static KismetExpression ReplaceChild(KismetExpression child, UAsset asset,
        int fnRef, byte statKey)
    {
        if (child is EX_InstanceVariable iv
            && iv.Variable?.New?.Path is { Length: > 0 }
            && Clean(iv.Variable.New.Path[0].ToString()) == "Challenge_EnemyHealthMultiplier")
            return Local(asset, "ChallengeKit_Value", fnRef);
        ReplaceHealthStat(child, asset, fnRef, statKey);
        return child;
    }

    private static bool ContainsName(KismetExpression[]? bytecode, string name)
    {
        var found = false;
        void Visit(KismetExpression e)
        {
            if (e is EX_NameConst n && n.Value.ToString() == name) found = true;
            foreach (var child in ExpressionsOf(e))
                if (child is not null) Visit(child);
        }
        foreach (var e in bytecode ?? Array.Empty<KismetExpression>()) Visit(e);
        return found;
    }

    private static void CollectNamesAndBytes(KismetExpression[]? bytecode,
        HashSet<string> names, HashSet<byte> bytes)
    {
        void Visit(KismetExpression e)
        {
            if (e is EX_NameConst n) names.Add(n.Value.ToString());
            if (e is EX_ByteConst b) bytes.Add(b.Value);
            foreach (var child in ExpressionsOf(e))
                if (child is not null) Visit(child);
        }
        foreach (var e in bytecode ?? Array.Empty<KismetExpression>()) Visit(e);
    }

    private static FProperty CloneLocal(FProperty source, UAsset asset, string name)
    {
        var clone = (FProperty)CloneMember(source);
        var field = clone.GetType().GetField("Name", BindingFlags.Public | BindingFlags.Instance)
                    ?? clone.GetType().BaseType?.GetField("Name", BindingFlags.Public | BindingFlags.Instance);
        field?.SetValue(clone, FName.FromString(asset, name));
        return clone;
    }

    private static int Size(IEnumerable<KismetExpression> expressions, UAsset asset) =>
        (int)expressions.Sum(e => (long)e.GetSize(asset));

    private static KismetPropertyPointer VarPointer(UAsset asset, string name, int owner) =>
        new(new FFieldPath(new[] { FName.FromString(asset, name) }, new FPackageIndex(owner)));

    private static KismetExpression CloneExpression(KismetExpression source)
    {
        var clone = (KismetExpression)Activator.CreateInstance(source.GetType())!;
        for (var t = source.GetType(); t is not null && t != typeof(object); t = t.BaseType)
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var value = f.GetValue(source);
                if (value is KismetExpression child) f.SetValue(clone, CloneExpression(child));
                else if (value is KismetExpression[] array)
                    f.SetValue(clone, array.Select(x => x is null ? null : CloneExpression(x)).ToArray());
                else f.SetValue(clone, value);
            }
        return clone;
    }

    private static IEnumerable<KismetExpression?> ExpressionsOf(KismetExpression expression)
    {
        for (var t = expression.GetType(); t is not null && t != typeof(object); t = t.BaseType)
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var value = f.GetValue(expression);
                if (value is KismetExpression child) yield return child;
                else if (value is KismetExpression[] array)
                    foreach (var item in array) yield return item;
            }
    }

    private static object CloneMember(object source) =>
        source.GetType().GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!
              .Invoke(source, null)!;

    // ─────────────────────── difficulty manager ───────────────────────

    private static int PatchDifficultyManager(string root, string modDir)
    {
        var src = FileCopy.FindUnder(root, DifficultyAsset + ".uasset")
            ?? throw new FileNotFoundException(DifficultyAsset);
        var dest = Path.Combine(modDir, "Sandfall/Content/Gameplay/Difficulty", DifficultyAsset + ".uasset");
        if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);

        var map = DataTableEditor.Mappings;
        var asset = new UAsset(src, EngineVersion.VER_UE5_4, map);
        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal))
            ?? throw new InvalidDataException("difficulty manager CDO missing");
        var list = (ArrayPropertyData)cdo.Data.First(p => Clean(p.Name) == "ChallengeAssets");

        int added = 0;
        foreach (var stem in new[] { DefenseDaStem, SpeedDaStem })
        {
            var import = EnsureObjectImport(asset,
                "/Game/Gameplay/Battle/Challenges/Content/" + stem, stem, ClassName, ClassPackage);
            if (list.Value.Any(p => p is ObjectPropertyData o && o.Value.Index == import.Index)) continue;
            var element = (ObjectPropertyData)list.Value[0].Clone();
            element.Name = FName.FromString(asset, list.Value.Length.ToString());
            element.Value = import;
            list.Value = list.Value.Concat(new PropertyData[] { element }).ToArray();
            added++;
        }
        if (added > 0) asset.Write(dest);
        return added;
    }

    // ───────────────────────────── list widget ─────────────────────────────

    private static int PatchListWidget(string root, string modDir)
    {
        var src = FileCopy.FindUnder(root, ListWidgetAsset + ".uasset")
            ?? throw new FileNotFoundException(ListWidgetAsset);
        var dest = Path.Combine(modDir,
            "Sandfall/Content/UI/Widgets/HUD_Exploration/SavePointMenu/" + ListWidgetAsset + ".uasset");
        if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);

        var map = DataTableEditor.Mappings;
        var asset = new UAsset(src, EngineVersion.VER_UE5_4, map);
        if (asset.Exports.OfType<NormalExport>().Any(e => e.ObjectName.ToString() == "DefenseMultiplierEntry"))
            return 0;

        var verticalBox = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString() == "VerticalBox_36")
            ?? throw new InvalidDataException("challenge list: VerticalBox_36 missing");
        var template = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString() == "LifeMultiplierEntry")
            ?? throw new InvalidDataException("challenge list: LifeMultiplierEntry missing");
        var templateSlot = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString() == "VerticalBoxSlot_2")
            ?? throw new InvalidDataException("challenge list: VerticalBoxSlot_2 missing");
        var slots = (ArrayPropertyData)verticalBox.Data.First(p => Clean(p.Name) == "Slots");

        int added = 0;
        AddEntry("DefenseMultiplierEntry", DefenseDaStem, DefenseMultipliers);
        AddEntry("SpeedMultiplierEntry", SpeedDaStem, SpeedMultipliers);
        if (added > 0) asset.Write(dest);
        return added;

        void AddEntry(string name, string daStem, double[] values)
        {
            var import = EnsureObjectImport(asset,
                "/Game/Gameplay/Battle/Challenges/Content/" + daStem, daStem, ClassName, ClassPackage);

            int entryIndex = asset.Exports.Count;
            int slotIndex = entryIndex + 1;

            var entry = (NormalExport)template.Clone();
            entry.ObjectName = FName.FromString(asset, name);
            entry.Data = template.Data.Select(ClonePropertyData).ToList();
            ((ObjectPropertyData)entry.Data.First(p => Clean(p.Name) == "ChallengeAsset")).Value = import;
            var challengeList = (ArrayPropertyData)entry.Data.First(p => Clean(p.Name) == "ChallengeList");
            challengeList.Value = values
                .Select(v => (PropertyData)new NamePropertyData(FName.FromString(asset, "ChallengeList"))
                {
                    Value = FName.FromString(asset, "MULT_" + v.ToString("0"))
                })
                .ToArray();
            ((ObjectPropertyData)entry.Data.First(p => Clean(p.Name) == "Slot")).Value =
                new FPackageIndex(slotIndex + 1);

            var slot = (NormalExport)templateSlot.Clone();
            slot.ObjectName = FName.FromString(asset, name + "_Slot");
            slot.Data = templateSlot.Data.Select(ClonePropertyData).ToList();
            ((ObjectPropertyData)slot.Data.First(p => Clean(p.Name) == "Content")).Value =
                new FPackageIndex(entryIndex + 1);
            ((ObjectPropertyData)slot.Data.First(p => Clean(p.Name) == "Parent")).Value =
                new FPackageIndex(asset.Exports.IndexOf(verticalBox) + 1);

            asset.Exports.Add(entry);
            asset.Exports.Add(slot);

            var element = (ObjectPropertyData)slots.Value[0].Clone();
            element.Value = new FPackageIndex(slotIndex + 1);
            slots.Value = slots.Value.Concat(new PropertyData[] { element }).ToArray();
            added++;
        }
    }

    private static PropertyData ClonePropertyData(PropertyData source)
    {
        var clone = (PropertyData)source.Clone();
        switch (source)
        {
            case StructPropertyData st:
                ((StructPropertyData)clone).Value = st.Value.Select(ClonePropertyData).ToList();
                break;
            case ArrayPropertyData arr:
                ((ArrayPropertyData)clone).Value = arr.Value.Select(ClonePropertyData).ToArray();
                break;
            case MapPropertyData map:
                var target = (MapPropertyData)clone;
                target.Value.Clear();
                foreach (var kv in map.Value)
                    target.Value.Add(ClonePropertyData(kv.Key), ClonePropertyData(kv.Value));
                break;
        }
        return clone;
    }

    // ─────────────────────────────── text ───────────────────────────────

    private static int AddStrings(string root, string modDir)
    {
        var src = FileCopy.FindUnder(root, StringTableAsset + ".uasset")
            ?? FileCopy.FindTablesAsset(StringTableAsset + ".uasset")
            ?? throw new FileNotFoundException(StringTableAsset);
        var dest = FileCopy.FindUnder(modDir, StringTableAsset + ".uasset");
        if (dest is null)
        {
            var rel = src[src.IndexOf("Sandfall", StringComparison.Ordinal)..];
            dest = Path.Combine(modDir, rel);
            FileCopy.AssetPair(src, dest);
        }

        var existing = DataTableEditor.OpenStringTable(dest);
        var add = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!existing.ContainsKey(DefenseNameKey)) add[DefenseNameKey] = "Enemy Defense Multiplier";
        if (!existing.ContainsKey(SpeedNameKey)) add[SpeedNameKey] = "Enemy Speed Multiplier";
        if (add.Count == 0) return 0;
        DataTableEditor.AddStringTableEntries(dest, add);
        return add.Count;
    }

    // ───────────────────────────── helpers ─────────────────────────────

    private static FPackageIndex EnsureObjectImport(UAsset asset, string packagePath,
        string objectName, string className, string classPackage)
    {
        FPackageIndex? package = null;
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (imp.ObjectName.ToString() == packagePath && imp.ClassName.ToString() == "Package")
                package = FPackageIndex.FromImport(i);
            if (imp.ObjectName.ToString() == objectName && imp.ClassName.ToString() == className)
                return FPackageIndex.FromImport(i);
        }
        package ??= asset.AddImport(new Import("/Script/CoreUObject", "Package",
            FPackageIndex.FromRawIndex(0), packagePath, false, asset));
        return asset.AddImport(new Import(classPackage, className, package, objectName, false, asset));
    }

    private static string Clean(FName? name) =>
        name is null ? "" : DataTableEditor.CleanName(name);

    private static string Clean(string value) =>
        value.Contains("::", StringComparison.Ordinal) ? value[(value.IndexOf("::", StringComparison.Ordinal) + 2)..] : value;
}

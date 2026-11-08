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
/// <c>Challenge_SetEnemyHealthMutliplier</c>). Defense and speed need two extra
/// multipliers on <c>AC_jRPG_BattleManager</c> and two extra apply blocks in
/// <c>Challenge_ApplyEnemyStatChanges</c>; this kit clones the health setter and
/// the health apply block rather than recompiling Blueprint bytecode.
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
    private const string DefenseBuffStem = "BP_BattleModifierBuff_EnemiesDefenseMultiplier";
    private const string SpeedDaStem = "DA_BattleChallenge_EnemySpeedMultiplier";
    private const string SpeedBuffStem = "BP_BattleModifierBuff_EnemiesSpeedMultiplier";

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
        var defenseBuff = StageClone(root, modDir, HealthBuffStem + ".uasset", DefenseBuffStem,
            "Challenge_SetEnemyDefenseMultiplier");
        var speedBuff = StageClone(root, modDir, HealthBuffStem + ".uasset", SpeedBuffStem,
            "Challenge_SetEnemySpeedMultiplier");
        var defenseDa = StageClone(root, modDir, HealthDaStem + ".uasset", DefenseDaStem, null,
            da =>
            {
                SetChallenge(da, DefenseChallengeId, DefenseNameKey, defenseBuff, DefenseMultipliers);
            });
        var speedDa = StageClone(root, modDir, HealthDaStem + ".uasset", SpeedDaStem, null,
            da =>
            {
                SetChallenge(da, SpeedChallengeId, SpeedNameKey, speedBuff, SpeedMultipliers);
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
            var cls = battle.Exports.OfType<ClassExport>().First();
            if (!cls.LoadedProperties.Any(p => Clean(p.Name) == "Challenge_EnemyDefenseMultiplier"))
                errors.Add("battle manager: defense multiplier variable missing");
            if (!battle.Exports.OfType<FunctionExport>().Any(f => f.ObjectName.ToString() == "Challenge_SetEnemySpeedMultiplier"))
                errors.Add("battle manager: speed setter missing");
            var apply = battle.Exports.OfType<FunctionExport>()
                .FirstOrDefault(f => f.ObjectName.ToString() == "Challenge_ApplyEnemyStatChanges");
            if (apply is null || apply.ScriptBytecodeSize < 1000)
                errors.Add("battle manager: stat apply blocks missing");
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

        foreach (var (stem, setter) in new[]
                 {
                     (DefenseBuffStem, "Challenge_SetEnemyDefenseMultiplier"),
                     (SpeedBuffStem, "Challenge_SetEnemySpeedMultiplier"),
                 })
        {
            try
            {
                var buff = new UAsset(Path.Combine(content, stem + ".uasset"),
                    EngineVersion.VER_UE5_4, DataTableEditor.Mappings);
                var fn = buff.Exports.OfType<FunctionExport>()
                    .FirstOrDefault(f => f.ObjectName.ToString() == "LoadNumericValue");
                var calls = new HashSet<string>(StringComparer.Ordinal);
                if (fn is not null) CollectCalls(fn.ScriptBytecode, calls);
                if (!calls.Contains("GetBattleManager")) errors.Add($"{stem}: GetBattleManager call missing");
                if (!calls.Contains(setter)) errors.Add($"{stem}: {setter} call missing");
            }
            catch (Exception ex) { errors.Add($"{stem}: {ex.Message}"); }
        }

        if (errors.Count > 0)
            throw new InvalidDataException("challenge verification failed: " + string.Join(" | ", errors));
    }

    private static void CollectCalls(KismetExpression[]? bytecode, HashSet<string> calls)
    {
        void Visit(KismetExpression e)
        {
            switch (e)
            {
                case EX_LocalVirtualFunction vf: calls.Add(vf.VirtualFunctionName.ToString()); break;
                case EX_VirtualFunction v: calls.Add(v.VirtualFunctionName.ToString()); break;
            }
            foreach (var child in ExpressionsOf(e))
                if (child is not null) Visit(child);
        }
        foreach (var e in bytecode ?? Array.Empty<KismetExpression>()) Visit(e);
    }

    // ─────────────────────────────── assets ───────────────────────────────

    private static string StageChallengesAsset(string root, string modDir, string fileName, string relativeDir)
    {
        var src = FileCopy.FindUnder(root, fileName)
            ?? throw new FileNotFoundException("challenge donor missing: " + fileName);
        var dest = Path.Combine(modDir, relativeDir, fileName);
        if (!File.Exists(dest)) FileCopy.AssetPair(src, dest);
        return dest;
    }

    private static string StageClone(string root, string modDir, string srcName, string newStem,
        string? setterName, Action<UAsset>? edit = null)
    {
        var src = FileCopy.FindUnder(root, srcName)
            ?? throw new FileNotFoundException("challenge donor missing: " + srcName);
        var dest = Path.Combine(modDir, ChallengesContent, newStem + ".uasset");
        FileCopy.AssetPair(src, dest);

        var map = DataTableEditor.Mappings;
        var asset = new UAsset(dest, EngineVersion.VER_UE5_4, map);
        var oldStem = Path.GetFileNameWithoutExtension(srcName);
        PictosCreateKit.RetargetPackage(asset, dest, oldStem, newStem);

        if (setterName is not null)
        {
            var fn = asset.Exports.OfType<FunctionExport>()
                          .FirstOrDefault(f => f.ObjectName.ToString() == "LoadNumericValue")
                      ?? throw new InvalidDataException(newStem + ": LoadNumericValue missing");
            RewriteVirtualCalls(fn.ScriptBytecode, "Challenge_SetEnemyHealthMutliplier", setterName, asset);
        }

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
        string buffStem, double[] values)
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
                    FName.FromString(da, "/Game/Gameplay/Battle/Challenges/Content/" + buffStem),
                    FName.FromString(da, buffStem + "_C"),
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
        var cls = asset.Exports.OfType<ClassExport>().FirstOrDefault()
            ?? throw new InvalidDataException("BattleManager: class export missing");

        var healthVar = cls.LoadedProperties.FirstOrDefault(p => Clean(p.Name) == "Challenge_EnemyHealthMultiplier");
        if (healthVar is null) return 0;
        if (cls.LoadedProperties.Any(p => Clean(p.Name) == "Challenge_EnemyDefenseMultiplier"))
            return 0; // already patched

        int classRef = asset.Exports.IndexOf(cls) + 1;
        var applyFn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "Challenge_ApplyEnemyStatChanges")
            ?? throw new InvalidDataException("BattleManager: Challenge_ApplyEnemyStatChanges missing");
        var setterFn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "Challenge_SetEnemyHealthMutliplier")
            ?? throw new InvalidDataException("BattleManager: Challenge_SetEnemyHealthMutliplier missing");

        // 1. Two new class-scope doubles, cloned from the health multiplier.
        var defenseVar = CloneProperty(healthVar, asset, "Challenge_EnemyDefenseMultiplier");
        var speedVar = CloneProperty(healthVar, asset, "Challenge_EnemySpeedMultiplier");
        cls.LoadedProperties = cls.LoadedProperties.Concat(new[] { defenseVar, speedVar }).ToArray();

        // 2. One setter per stat, cloned from the health setter. The new
        //    functions only assign the variable; the vanilla setter's logging
        //    boilerplate is intentionally not copied.
        CloneSetter(asset, cls, setterFn, classRef,
            "Challenge_SetEnemyDefenseMultiplier", "Challenge_EnemyDefenseMultiplier");
        CloneSetter(asset, cls, setterFn, classRef,
            "Challenge_SetEnemySpeedMultiplier", "Challenge_EnemySpeedMultiplier");

        // 3. Duplicate the health apply block for Defense (stat 6) and
        //    Speed (stat 8). The vanilla block's trailing "OutStats = InStats;
        //    return" is dropped so an active health challenge falls through to
        //    the Defense/Speed blocks instead of returning early. Offsets are
        //    absolute byte offsets in the function.
        var bc = applyFn.ScriptBytecode.ToList();
        var healthBlock = bc.Take(9).ToList(); // condition + health modifications
        var tail = bc.Skip(11).ToList();       // OutStats = InStats; return; end

        var defenseBlock = CloneApplyBlock(healthBlock, asset, classRef,
            "Challenge_EnemyDefenseMultiplier", 6); // E_jRPG_StatType::NewEnumerator4
        var speedBlock = CloneApplyBlock(healthBlock, asset, classRef,
            "Challenge_EnemySpeedMultiplier", 8);   // E_jRPG_StatType::NewEnumerator7

        var newBc = new List<KismetExpression>();
        newBc.AddRange(healthBlock);
        newBc.AddRange(defenseBlock);
        newBc.AddRange(speedBlock);
        newBc.AddRange(tail.Select(CloneExpression));

        int defenseStart = Size(healthBlock, asset);
        int speedStart = defenseStart + Size(defenseBlock, asset);
        int finalStart = speedStart + Size(speedBlock, asset);

        FixJumpTargets(healthBlock, defenseStart);
        FixJumpTargets(defenseBlock, speedStart);
        FixJumpTargets(speedBlock, finalStart);

        applyFn.ScriptBytecode = newBc.ToArray();
        applyFn.ScriptBytecodeSize = Size(newBc, asset);
        asset.Write(path);
        return 2 + 2 + 1; // variables + functions + apply blocks (informational)
    }

    private static void CloneSetter(UAsset asset, ClassExport cls, FunctionExport source,
        int classRef, string name, string variable)
    {
        var fn = (FunctionExport)source.Clone();
        fn.ObjectName = FName.FromString(asset, name);
        fn.LoadedProperties = source.LoadedProperties
            .Select(p => (FProperty)CloneMember(p))
            .ToArray();

        int exportIndex = asset.Exports.Count + 1;
        var pointer = VarPointer(asset, variable, classRef);
        var body = new EX_Let
        {
            Value = VarPointer(asset, variable, classRef),
            Variable = new EX_InstanceVariable { Variable = pointer },
            Expression = new EX_LocalVariable { Variable = VarPointer(asset, "Value", exportIndex) },
        };
        fn.ScriptBytecode = new KismetExpression[]
        {
            body,
            new EX_Return { ReturnExpression = new EX_Nothing() },
            new EX_EndOfScript(),
        };
        fn.ScriptBytecodeSize = Size(fn.ScriptBytecode, asset);
        asset.Exports.Add(fn);

        var refIndex = new FPackageIndex(exportIndex);
        cls.FuncMap[fn.ObjectName] = refIndex;
        cls.Children = (cls.Children ?? Array.Empty<FPackageIndex>()).Concat(new[] { refIndex }).ToArray();
    }

    private static List<KismetExpression> CloneApplyBlock(List<KismetExpression> source,
        UAsset asset, int classRef, string variable, byte statKey)
    {
        var block = source.Select(CloneExpression).ToList();
        void Fix(KismetExpression e)
        {
            switch (e)
            {
                case EX_InstanceVariable iv
                    when iv.Variable?.New?.Path is { Length: > 0 }
                         && Clean(iv.Variable.New.Path[0].ToString()) == "Challenge_EnemyHealthMultiplier":
                    iv.Variable = VarPointer(asset, variable, classRef);
                    break;
                case EX_ByteConst b:
                    b.Value = statKey;
                    break;
            }
            foreach (var f in ExpressionsOf(e))
                if (f is not null) Fix(f);
        }
        foreach (var e in block) Fix(e);
        return block;
    }

    private static void FixJumpTargets(List<KismetExpression> block, int falseTarget)
    {
        var jumpIfNot = block.OfType<EX_JumpIfNot>().FirstOrDefault();
        if (jumpIfNot is not null) jumpIfNot.CodeOffset = (uint)falseTarget;
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

    private static FProperty CloneProperty(FProperty source, UAsset asset, string name)
    {
        var clone = (FProperty)CloneMember(source);
        var field = clone.GetType().GetField("Name", BindingFlags.Public | BindingFlags.Instance)
                    ?? clone.GetType().BaseType?.GetField("Name", BindingFlags.Public | BindingFlags.Instance);
        field?.SetValue(clone, FName.FromString(asset, name));
        return clone;
    }

    private static object CloneMember(object source) =>
        source.GetType().GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!
              .Invoke(source, null)!;

    private static void RewriteVirtualCalls(KismetExpression[]? bytecode,
        string oldName, string newName, UAsset asset)
    {
        void Visit(KismetExpression e)
        {
            if (e is EX_LocalVirtualFunction vf
                && string.Equals(vf.VirtualFunctionName.ToString(), oldName, StringComparison.Ordinal))
                vf.VirtualFunctionName = FName.FromString(asset, newName);
            else if (e is EX_VirtualFunction v
                     && string.Equals(v.VirtualFunctionName.ToString(), oldName, StringComparison.Ordinal))
                v.VirtualFunctionName = FName.FromString(asset, newName);
            foreach (var child in ExpressionsOf(e))
                if (child is not null) Visit(child);
        }
        foreach (var e in bytecode ?? Array.Empty<KismetExpression>()) Visit(e);
    }

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

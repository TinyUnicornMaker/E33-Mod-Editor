using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Weapon-only drop-level scaling. The shared loot formula
/// (<c>FL_LootSystemHelpers.GetItemLevelFromLootContext</c>) stays vanilla:
///
/// <code>itemLevel = Clamp(Ceil((EncounterLevel + LootContextLevelOffset) / 3.0), 1, GetGearMaxUpgradeLevel())</code>
///
/// so pictos keep their vanilla 1–33 levels. Weapons get a second stage at the
/// three places that actually use that result:
///
/// <list type="number">
/// <item><c>BP_ItemInstance_Gear.GenerateItem</c> — the normal item-add path
/// (enemies, chests, bosses, randomizer, merchant purchases).</item>
/// <item><c>BP_ItemInstance_Gear.ChangeLevelWithLootContext</c> — the direct
/// loot-context path.</item>
/// <item><c>AC_jRPG_BattleManager.Helper_TryAddRolledLootToMap</c> — the
/// battle reward pre-filter. Without this the vanilla drop level (≤33) is
/// compared against an owned 34+ weapon and the drop is never added to the
/// rewards map, so <c>GenerateItem</c> is never reached.</item>
/// </list>
///
/// <para>The discriminator is the existing virtual
/// <c>GetScalingDataTable()</c>: weapons return
/// <c>DT_EquipmentScalingTable_Weapons</c>, pictos return
/// <c>DT_EquipmentScalingTable_Pictos</c>. The gear paths also gate on
/// <c>IsLootContextValid(LootContext)</c> so merchant/story grants with
/// <c>EncounterLevel = -1</c> keep their vanilla level.</para>
///
/// <para><b>Scaling.</b> Vanilla item levels 1–33 map proportionally to
/// 1–<see cref="Settings.MaxDropLevel"/>. With the default 222: 33 → 222,
/// 1 → 6, 2 → 13. The formula is integer-only so no cast/new local is needed:
/// <c>Clamp((level * MaxDropLevel) / 33, 1, MaxDropLevel)</c>.</para>
///
/// <para><b>Pack order.</b> This kit only raises the level the game asks for;
/// the weapon scaling table must already contain rows up to
/// <c>MaxDropLevel</c>. <c>FullPackKit</c> runs it right after
/// <see cref="WeaponOverLevelKit"/>, which extends
/// <c>DT_EquipmentScalingTable_Weapons</c> to 333.</para>
///
/// <para><b>Settings</b> live in <c>work/weapon_drop_level.json</c>:
/// <c>Enabled</c> (default true) and <c>MaxDropLevel</c> (default 222,
/// clamped 33…333; 33 means vanilla/no-op).</para>
/// </summary>
public static class WeaponDropLevelKit
{
    public const string SettingsFile = "weapon_drop_level.json";
    public const string GearStem = "BP_ItemInstance_Gear";
    public const string BattleManagerStem = "AC_jRPG_BattleManager";
    public const int VanillaMaxItemLevel = 33;

    private const string WeaponTable = "DT_EquipmentScalingTable_Weapons";
    private const string WeaponTablePath = "/Game/Gameplay/Pictos/DT_EquipmentScalingTable_Weapons";

    public sealed class Settings
    {
        public bool Enabled = true;
        public int MaxDropLevel = 222;

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                var path = Path.Combine(EditorPaths.RepoRoot, "work", SettingsFile);
                if (!File.Exists(path)) return s;
                var o = JObject.Parse(File.ReadAllText(path));
                if (o["Enabled"] is { } e) s.Enabled = e.ToObject<bool>();
                if (o["MaxDropLevel"] is { } m) s.MaxDropLevel = m.ToObject<int>();
            }
            catch { /* corrupt/unreadable → defaults */ }
            return s;
        }
    }

    public static int Apply(string modDir) => Apply(modDir, Settings.Load());

    public static int Apply(string modDir, Settings settings)
    {
        if (!settings.Enabled)
        {
            Console.WriteLine("[weapon-drop] disabled by work/" + SettingsFile + " — skipping");
            return 0;
        }

        int max = EffectiveMax(settings);
        if (max <= VanillaMaxItemLevel)
        {
            Console.WriteLine($"[weapon-drop] MaxDropLevel {max} is vanilla — no asset staged");
            return 0;
        }

        int n = 0;
        var gear = Stage(modDir, GearStem);
        n += PatchGear(gear, max);
        var battle = Stage(modDir, BattleManagerStem);
        n += PatchBattleManager(battle, max);

        Console.WriteLine(
            $"[weapon-drop] MaxDropLevel {max}: weapon item levels 1–{VanillaMaxItemLevel} → " +
            $"1–{max} (pictos stay 1–{VanillaMaxItemLevel})");
        return n;
    }

    public static int EffectiveMax(Settings settings) =>
        Math.Clamp(settings.MaxDropLevel, VanillaMaxItemLevel, WeaponOverLevelKit.MaxLevel);

    // ────────────────────────── staging ──────────────────────────

    private static string Stage(string modDir, string stem)
    {
        // Compose with a copy another kit already staged (Party Five patches
        // the BattleManager late and reuses whatever is in work/mod).
        var existing = FileCopy.FindUnder(modDir, stem + ".uasset");
        if (existing is not null) return existing;

        string? src = null;
        if (stem == BattleManagerStem)
        {
            // The Party Five cache is known-pristine. work/tables may hold a
            // previous kit's staged copy under party_five/gameplay_tweaks.
            src = FileCopy.FindUnder(
                Path.Combine(EditorPaths.RepoRoot, "work", "party5", "vanilla"),
                stem + ".uasset");
        }
        src ??= FileCopy.FindTablesAsset(stem + ".uasset");

        if (src is null)
        {
            var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "weapon_drop");
            Directory.CreateDirectory(dir);
            using (Packer.DonorExtract.SuspendMods())
                new Retoc().ToLegacy(dir, stem);
            src = FileCopy.FindUnder(dir, stem + ".uasset")
                ?? throw new FileNotFoundException($"weapon-drop: {stem}.uasset not found after extraction");
        }

        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        var rel = idx >= 0 ? src[idx..] : Path.Combine("Sandfall", "Content", stem + ".uasset");
        var dest = Path.Combine(modDir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    // ────────────────────────── patching ──────────────────────────

    private static int PatchGear(string path, int max)
    {
        var asset = Open(path);
        int n = 0;
        n += PatchGearFunction(asset, "GenerateItem", "local_CurrentLevel", max);
        n += PatchGearFunction(asset, "ChangeLevelWithLootContext",
            "CallFunc_GetItemLevelFromLootContext_ReturnValue", max);
        if (n > 0) asset.Write(path);
        return n;
    }

    private static int PatchGearFunction(UAsset asset, string function, string levelLocal, int max)
    {
        var fn = Fn(asset, function);
        var stmts = fn.ScriptBytecode;
        if (stmts is null || stmts.Length == 0)
            throw new InvalidDataException($"[weapon-drop] {function} has no bytecode");
        if (HasCall(asset, fn, "Multiply_IntInt"))
        {
            Console.WriteLine($"[weapon-drop] {function}: already applied — skipping");
            return 0;
        }

        var helperCtx = HelperContext(fn, "GetItemLevelFromLootContext")
            ?? throw new InvalidDataException($"[weapon-drop] {function}: loot-context helper call missing");
        var target = FindLevelLet(fn, levelLocal)
            ?? throw new InvalidDataException($"[weapon-drop] {function}: {levelLocal} assignment missing");

        var weaponTable = LuneSkillsKit.EnsureObjectImport(
            asset, WeaponTablePath, WeaponTable, "DataTable", "/Script/Engine");
        var condition = BuildWeaponCondition(asset, helperCtx, weaponTable);
        var scale = BuildScale(asset, target, max);

        var list = stmts.ToList();
        InsertBlock(asset, fn, list.IndexOf(target) + 1, condition, scale);
        Console.WriteLine($"[weapon-drop] {function}: weapon level scaling inserted ({max})");
        return 1;
    }

    private static int PatchBattleManager(string path, int max)
    {
        var asset = Open(path);
        const string function = "Helper_TryAddRolledLootToMap";
        var fn = Fn(asset, function);
        var stmts = fn.ScriptBytecode;
        if (stmts is null || stmts.Length == 0)
            throw new InvalidDataException($"[weapon-drop] {function} has no bytecode");
        if (HasCall(asset, fn, "Multiply_IntInt"))
        {
            Console.WriteLine($"[weapon-drop] {function}: already applied — skipping");
            return 0;
        }

        var target = FindLevelLet(fn, "CallFunc_GetItemLevelFromLootContext_ReturnValue")
            ?? throw new InvalidDataException($"[weapon-drop] {function}: loot-level assignment missing");
        var weaponBool = FindLocal(fn, "CallFunc_EqualEqual_ByteByte_ReturnValue_1")
            ?? throw new InvalidDataException($"[weapon-drop] {function}: weapon item-type comparison missing");
        var condition = new EX_LocalVariable { Variable = weaponBool.Variable };
        var scale = BuildScale(asset, target, max);

        var list = stmts.ToList();
        InsertBlock(asset, fn, list.IndexOf(target) + 1, condition, scale);
        asset.Write(path);
        Console.WriteLine($"[weapon-drop] {function}: reward pre-filter scaling inserted ({max})");
        return 1;
    }

    private static EX_CallMath BuildWeaponCondition(UAsset asset, EX_Context helperCtx, FPackageIndex weaponTable)
    {
        // IsLootContextValid(LootContext, __WorldContext) — clone the existing
        // helper Context so the __WorldContext/Self parameter and struct
        // pointer stay exactly as authored.
        var validCtx = (EX_Context)MayhemFixKit.CloneKismet(helperCtx);
        var validCall = (EX_LocalVirtualFunction)validCtx.ContextExpression;
        validCall.VirtualFunctionName = FName.FromString(asset, "IsLootContextValid");

        var getTable = new EX_LocalVirtualFunction
        {
            VirtualFunctionName = FName.FromString(asset, "GetScalingDataTable"),
            Parameters = Array.Empty<KismetExpression>(),
        };
        var eq = new EX_CallMath
        {
            StackNode = LuneSkillsKit.EnsureKismetMathFn(asset, "EqualEqual_ObjectObject"),
            Parameters = new KismetExpression[]
            {
                getTable,
                new EX_ObjectConst { Value = weaponTable },
            },
        };
        return new EX_CallMath
        {
            StackNode = LuneSkillsKit.EnsureKismetMathFn(asset, "BooleanAND"),
            Parameters = new KismetExpression[] { eq, validCtx },
        };
    }

    private static EX_Let BuildScale(UAsset asset, EX_Let target, int max)
    {
        var ptr = target.Value;
        var read = new EX_LocalVariable { Variable = ptr };
        var mult = new EX_CallMath
        {
            StackNode = LuneSkillsKit.EnsureKismetMathFn(asset, "Multiply_IntInt"),
            Parameters = new KismetExpression[] { read, new EX_IntConst { Value = max } },
        };
        var div = new EX_CallMath
        {
            StackNode = LuneSkillsKit.EnsureKismetMathFn(asset, "Divide_IntInt"),
            Parameters = new KismetExpression[] { mult, new EX_IntConst { Value = VanillaMaxItemLevel } },
        };
        var clamp = new EX_CallMath
        {
            StackNode = LuneSkillsKit.EnsureKismetMathFn(asset, "Clamp"),
            Parameters = new KismetExpression[]
            {
                div,
                new EX_IntConst { Value = 1 },
                new EX_IntConst { Value = max },
            },
        };
        return new EX_Let
        {
            Value = ptr,
            Variable = new EX_LocalVariable { Variable = ptr },
            Expression = clamp,
        };
    }

    private static void InsertBlock(
        UAsset asset, FunctionExport fn, int index, KismetExpression condition, EX_Let scale)
    {
        var stmts = fn.ScriptBytecode!.ToList();
        var jump = new EX_JumpIfNot { BooleanExpression = condition };
        uint delta = (uint)(jump.GetSize(asset) + scale.GetSize(asset));

        uint insertOffset = 0;
        for (int i = 0; i < index; i++) insertOffset += (uint)stmts[i].GetSize(asset);
        jump.CodeOffset = insertOffset + delta;

        ShiftTargetsAtOrAfter(asset, fn, insertOffset, delta);
        stmts.Insert(index, jump);
        stmts.Insert(index + 1, scale);
        fn.ScriptBytecode = stmts.ToArray();

        MayhemFixKit.RecalcContextSkips(asset, fn);
        PartyFiveKit.VerifyJumps(asset, fn);
    }

    /// <summary>Moves every absolute target (jump / push / switch) at or after
    /// the insertion point by <paramref name="delta"/>.</summary>
    private static void ShiftTargetsAtOrAfter(UAsset asset, FunctionExport fn, uint point, long delta)
    {
        if (delta == 0) return;
        uint pos = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref pos, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j when j.CodeOffset >= point:
                        j.CodeOffset = (uint)(j.CodeOffset + delta); break;
                    case EX_JumpIfNot jn when jn.CodeOffset >= point:
                        jn.CodeOffset = (uint)(jn.CodeOffset + delta); break;
                    case EX_PushExecutionFlow pf when pf.PushingAddress >= point:
                        pf.PushingAddress = (uint)(pf.PushingAddress + delta); break;
                    case EX_SwitchValue sv:
                        if (sv.EndGotoOffset >= point) sv.EndGotoOffset = (uint)(sv.EndGotoOffset + delta);
                        for (int i = 0; i < sv.Cases.Length; i++)
                            if (sv.Cases[i].NextOffset >= point)
                                sv.Cases[i].NextOffset = (uint)(sv.Cases[i].NextOffset + delta);
                        break;
                }
            });
    }

    // ────────────────────────── verification ──────────────────────────

    public static void Verify(string modDir) => Verify(modDir, Settings.Load());

    public static void Verify(string modDir, Settings settings)
    {
        int max = EffectiveMax(settings);
        if (!settings.Enabled || max <= VanillaMaxItemLevel)
        {
            Console.WriteLine("[weapon-drop] vanilla setting — nothing to verify");
            return;
        }

        var gearPath = FileCopy.FindUnder(modDir, GearStem + ".uasset")
            ?? throw new FileNotFoundException("[weapon-drop] staged " + GearStem + ".uasset missing");
        var gear = Open(gearPath);
        CheckGearFunction(gear, "GenerateItem", max);
        CheckGearFunction(gear, "ChangeLevelWithLootContext", max);

        var battlePath = FileCopy.FindUnder(modDir, BattleManagerStem + ".uasset")
            ?? throw new FileNotFoundException("[weapon-drop] staged " + BattleManagerStem + ".uasset missing");
        var battle = Open(battlePath);
        var fn = Fn(battle, "Helper_TryAddRolledLootToMap");
        if (FindLocal(fn, "CallFunc_EqualEqual_ByteByte_ReturnValue_1") is null)
            throw new InvalidDataException("[weapon-drop] battle pre-filter weapon-type gate missing");
        if (!HasCall(battle, fn, "Multiply_IntInt"))
            throw new InvalidDataException("[weapon-drop] battle pre-filter scaling missing");
        if (!HasCall(battle, fn, "Clamp"))
            throw new InvalidDataException("[weapon-drop] battle pre-filter clamp missing");
        PartyFiveKit.VerifyJumps(battle, fn);

        if (FileCopy.FindUnder(modDir, "FL_LootSystemHelpers.uasset") is not null)
            throw new InvalidDataException("[weapon-drop] shared loot helper must not be staged");
        if (FileCopy.FindUnder(modDir, "DT_EquipmentScalingTable_Pictos.uasset") is not null)
            throw new InvalidDataException("[weapon-drop] picto scaling table must not be staged");

        Console.WriteLine($"[weapon-drop] Verify passed: weapon-only scaling to {max}, pictos untouched");
    }

    private static void CheckGearFunction(UAsset asset, string function, int max)
    {
        var fn = Fn(asset, function);
        if (!HasVirtual(fn, "GetScalingDataTable"))
            throw new InvalidDataException($"[weapon-drop] {function}: weapon-table discriminator missing");
        if (!HasVirtual(fn, "IsLootContextValid"))
            throw new InvalidDataException($"[weapon-drop] {function}: loot-context validity gate missing");
        if (!HasObject(asset, fn, WeaponTable))
            throw new InvalidDataException($"[weapon-drop] {function}: weapon scaling table reference missing");
        if (!HasCall(asset, fn, "Multiply_IntInt"))
            throw new InvalidDataException($"[weapon-drop] {function}: multiply missing");
        if (!HasCall(asset, fn, "Clamp"))
            throw new InvalidDataException($"[weapon-drop] {function}: clamp missing");
        if (!HasInt(fn, VanillaMaxItemLevel))
            throw new InvalidDataException($"[weapon-drop] {function}: vanilla max constant missing");
        if (!HasInt(fn, max))
            throw new InvalidDataException($"[weapon-drop] {function}: MaxDropLevel constant missing");
        PartyFiveKit.VerifyJumps(asset, fn);
    }

    // ────────────────────────── bytecode helpers ──────────────────────────

    private static FunctionExport Fn(UAsset asset, string name) =>
        asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == name)
        ?? throw new InvalidDataException($"[weapon-drop] {name} not found");

    private static EX_Context? HelperContext(FunctionExport fn, string name)
    {
        EX_Context? found = null;
        foreach (var s in fn.ScriptBytecode ?? Array.Empty<KismetExpression>())
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (found is null && e is EX_Context c
                    && c.ContextExpression is EX_LocalVirtualFunction vf
                    && vf.VirtualFunctionName.ToString() == name)
                    found = c;
            });
        return found;
    }

    private static EX_Let? FindLevelLet(FunctionExport fn, string local)
    {
        var hits = (fn.ScriptBytecode ?? Array.Empty<KismetExpression>()).OfType<EX_Let>()
            .Where(l => PartyFiveKit.FieldName(l.Value) == local).ToList();
        // GenerateItem initialises local_CurrentLevel to 0 first; the loot
        // assignment is the one whose RHS reads another local.
        return hits.FirstOrDefault(l => l.Expression is EX_LocalVariable) ?? hits.LastOrDefault();
    }

    private static EX_LocalVariable? FindLocal(FunctionExport fn, string local)
    {
        EX_LocalVariable? found = null;
        foreach (var s in fn.ScriptBytecode ?? Array.Empty<KismetExpression>())
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (found is null && e is EX_LocalVariable lv
                    && PartyFiveKit.FieldName(lv.Variable) == local)
                    found = lv;
            });
        return found;
    }

    private static bool HasVirtual(FunctionExport fn, string name)
    {
        bool hit = false;
        foreach (var s in fn.ScriptBytecode ?? Array.Empty<KismetExpression>())
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == name)
                    hit = true;
            });
        return hit;
    }

    private static bool HasCall(UAsset asset, FunctionExport fn, string name)
    {
        bool hit = false;
        foreach (var s in fn.ScriptBytecode ?? Array.Empty<KismetExpression>())
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_CallMath cm && ImportName(asset, cm.StackNode) == name) hit = true;
            });
        return hit;
    }

    private static bool HasObject(UAsset asset, FunctionExport fn, string name)
    {
        bool hit = false;
        foreach (var s in fn.ScriptBytecode ?? Array.Empty<KismetExpression>())
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_ObjectConst oc && ImportName(asset, oc.Value) == name) hit = true;
            });
        return hit;
    }

    private static bool HasInt(FunctionExport fn, int value)
    {
        bool hit = false;
        foreach (var s in fn.ScriptBytecode ?? Array.Empty<KismetExpression>())
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_IntConst ic && ic.Value == value) hit = true;
            });
        return hit;
    }

    private static string? ImportName(UAsset asset, FPackageIndex? idx)
    {
        if (idx is null || !idx.IsImport()) return null;
        var i = Math.Abs(idx.Index) - 1;
        return i >= 0 && i < asset.Imports.Count ? asset.Imports[i].ObjectName.ToString() : null;
    }

    private static UAsset Open(string path)
    {
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
    }
}

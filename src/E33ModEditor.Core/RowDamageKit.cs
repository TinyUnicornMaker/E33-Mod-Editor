using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Position-based damage. Back-row party members deal and take a configurable
/// fraction of normal damage; front-row members and enemies are unchanged by
/// default.
///
/// Rows are derived from the Party of Five formation offsets (negative forward
/// offset = behind the line), not from the raw battle-spot order. The vanilla
/// spots are only anchors on one line; the two-row layout is created entirely
/// by the offsets.
///
/// The patch is one <c>FDamageModifier</c> added to the damage builder in
/// <c>AC_jRPG_CharacterBattleStats.ReceiveDamageFromObject</c>, immediately
/// after <c>ProcessReceivedRawDamages</c> and before the final
/// <c>ApplyActiveModifiers</c>. That is the same modifier path Glass Cannon,
/// Confident and the other received-damage passives use.
/// </summary>
public static class RowDamageKit
{
    public const string AssetName = "AC_jRPG_CharacterBattleStats";
    public const string FunctionName = "ReceiveDamageFromObject";
    public const string BaseClassPackage = "/Game/jRPGTemplate/Blueprints/Basics/BP_jRPG_Character_Battle_Base";
    public const string BaseClassName = "BP_jRPG_Character_Battle_Base_C";

    public sealed class Settings
    {
        /// <summary>Off disables the whole row rule. Party of Five must also be on.</summary>
        public bool Enabled { get; set; } = true;

        public double FrontDamageDealtMultiplier { get; set; } = 1.0;
        public double FrontDamageTakenMultiplier { get; set; } = 1.0;
        public double BackDamageDealtMultiplier { get; set; } = 0.8;
        public double BackDamageTakenMultiplier { get; set; } = 0.8;

        public static string SettingsPath =>
            Path.Combine(EditorPaths.RepoRoot, "work", "row_damage.json");

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (File.Exists(SettingsPath))
                    s.FromJson(JObject.Parse(File.ReadAllText(SettingsPath)));
            }
            catch { /* keep defaults */ }
            return s;
        }

        public void Save() =>
            File.WriteAllText(SettingsPath, ToJson().ToString(Newtonsoft.Json.Formatting.Indented));

        public JObject ToJson() => new()
        {
            ["Enabled"] = Enabled,
            ["FrontDamageDealtMultiplier"] = FrontDamageDealtMultiplier,
            ["FrontDamageTakenMultiplier"] = FrontDamageTakenMultiplier,
            ["BackDamageDealtMultiplier"] = BackDamageDealtMultiplier,
            ["BackDamageTakenMultiplier"] = BackDamageTakenMultiplier,
        };

        public void FromJson(JObject o)
        {
            if (o.Count == 0) return;
            Enabled = o.Value<bool?>("Enabled") ?? Enabled;
            FrontDamageDealtMultiplier = o.Value<double?>("FrontDamageDealtMultiplier") ?? FrontDamageDealtMultiplier;
            FrontDamageTakenMultiplier = o.Value<double?>("FrontDamageTakenMultiplier") ?? FrontDamageTakenMultiplier;
            BackDamageDealtMultiplier = o.Value<double?>("BackDamageDealtMultiplier") ?? BackDamageDealtMultiplier;
            BackDamageTakenMultiplier = o.Value<double?>("BackDamageTakenMultiplier") ?? BackDamageTakenMultiplier;
        }
    }

    /// <summary>Spot indices behind the front line, derived from the formation
    /// offsets. A negative forward offset means the spot is placed away from
    /// the enemies. Current default formation: { 1, 3, 4 }.</summary>
    public static int[] BackSpots(PartyFiveKit.Settings partyFive)
    {
        if (partyFive.Formation is { Length: >= PartyFiveKit.DefaultPartySize })
            return Enumerable.Range(0, PartyFiveKit.DefaultPartySize)
                .Where(i => partyFive.Formation[i].Fwd < 0)
                .ToArray();

        // Documented default: front slots 2/0, back slots 3/1/4.
        return new[] { 1, 3, 4 };
    }

    public static int Apply(string modDir, PartyFiveKit.Settings partyFive,
        Settings settings, Action<string>? log = null)
    {
        if (!settings.Enabled || !partyFive.Enabled) return 0;
        if (!partyFive.UseFormationOffsets)
        {
            log?.Invoke("[rowdamage] skipped: Party of Five formation offsets are off");
            return 0;
        }

        var back = BackSpots(partyFive);
        if (back.Length == 0)
            throw new InvalidDataException(
                "[rowdamage] no back-row spots in party_five.json (all forward offsets are >= 0)");
        if (back.Length != 3)
            log?.Invoke($"[rowdamage] warning: {back.Length} back-row spot(s): [{string.Join(",", back)}]");

        var path = CopyIntoMod(modDir);
        var asset = Open(path);
        var fn = Fn(asset, FunctionName);
        int edits = Patch(asset, fn, back, settings);
        Write(asset, path);

        if (!Verify(path, back, settings))
            throw new InvalidDataException(
                "[rowdamage] verification failed after writing " + AssetName);

        log?.Invoke($"[rowdamage] back spots [{string.Join(",", back)}]: " +
                    $"dealt x{settings.BackDamageDealtMultiplier:0.###}, " +
                    $"taken x{settings.BackDamageTakenMultiplier:0.###}");
        return edits;
    }

    // ───────────────────────────── patch ─────────────────────────────

    private static int Patch(UAsset asset, FunctionExport fn, int[] back, Settings s)
    {
        var stmts = fn.ScriptBytecode?.ToList()
            ?? throw new InvalidDataException($"[rowdamage] {FunctionName} has no bytecode");
        var oldStarts = PartyFiveKit.StatementStarts(asset, stmts);

        // Already patched? Locate our own shape (Multiply_DoubleDouble feeding an
        // AddDamageModifier) and leave it alone; Apply() verifies it afterwards.
        if (HasRowModifier(asset, stmts)) return 0;

        int recvIdx = FindCallStatement(asset, stmts, "ProcessReceivedRawDamages");
        if (recvIdx < 0)
            throw new InvalidDataException("[rowdamage] ProcessReceivedRawDamages call not found");
        int applyIdx = recvIdx + 1;
        if (applyIdx >= stmts.Count || !ContainsCall(asset, stmts[applyIdx], "ApplyActiveModifiers"))
            throw new InvalidDataException(
                "[rowdamage] ProcessReceivedRawDamages is not followed by ApplyActiveModifiers");

        int classImp = -1;
        for (int i = 0; i < asset.Imports.Count; i++)
            if (asset.Imports[i].ObjectName.ToString() == BaseClassName
                && asset.Imports[i].ClassName.ToString() == "BlueprintGeneratedClass")
            {
                classImp = i;
                break;
            }
        if (classImp < 0)
        {
            var added = LuneSkillsKit.EnsureObjectImport(asset, BaseClassPackage, BaseClassName,
                "BlueprintGeneratedClass", "/Script/Engine");
            classImp = Math.Abs(added.Index) - 1;
        }

        // Existing locals we reuse. Their owning function is the same export, so
        // no new locals are added to the function signature.
        var targetBase = Local(fn, "CallFunc_GetCharacterBattleBase_jRPG_CharacterBattleBase_3");
        var sourceBase = Local(fn, "CallFunc_GetCharacterBattleBase_jRPG_CharacterBattleBase_4");
        var sourceActor = Local(fn, "local_DamageSourceCharacter");

        // 1. target = GetCharacterBattleBase(Self)
        var targetCall = FindTargetBaseCall(asset, stmts)
            ?? throw new InvalidDataException("[rowdamage] target GetCharacterBattleBase call not found");

        // 2. source = local_DamageSourceCharacter.GetCharacterBattleBase()
        var sourceCall = FindSourceBaseCall(asset, stmts, sourceActor)
            ?? throw new InvalidDataException("[rowdamage] source GetCharacterBattleBase call not found");

        var selectInt = LuneSkillsKit.EnsureKismetMathFn(asset, "SelectInt");
        var eqInt = LuneSkillsKit.EnsureKismetMathFn(asset, "EqualEqual_IntInt");
        var convIntToDouble = LuneSkillsKit.EnsureKismetMathFn(asset, "Conv_IntToDouble");
        var add = LuneSkillsKit.EnsureKismetMathFn(asset, "Add_DoubleDouble");
        var subtract = LuneSkillsKit.EnsureKismetMathFn(asset, "Subtract_DoubleDouble");
        var multiply = LuneSkillsKit.EnsureKismetMathFn(asset, "Multiply_DoubleDouble");
        var isBack = Enumerable.Range(0, PartyFiveKit.DefaultPartySize)
            .Select(i => back.Contains(i)).ToArray();

        KismetPropertyPointer Ptr(string name) =>
            new(new FFieldPath(new[] { FName.FromString(asset, name) },
                FPackageIndex.FromImport(classImp)));

        KismetExpression Read(KismetExpression obj, string prop)
        {
            var ptr = Ptr(prop);
            return new EX_Context
            {
                ObjectExpression = obj,
                RValuePointer = ptr,
                ContextExpression = new EX_InstanceVariable { Variable = ptr },
            };
        }

        KismetExpression Spot(KismetExpression baseLocal) =>
            Read(MayhemFixKit.CloneKismet(baseLocal), "BattleSpotIndex");

        KismetExpression Enemy(KismetExpression baseLocal) =>
            Read(MayhemFixKit.CloneKismet(baseLocal), "Enemy?");

        KismetExpression D(double v) => new EX_DoubleConst { Value = v };
        KismetExpression I(int v) => new EX_IntConst { Value = v };
        KismetExpression Call(FPackageIndex fn, params KismetExpression[] p) =>
            new EX_CallMath { StackNode = fn, Parameters = p };
        KismetExpression Eq(KismetExpression spotRead, int i) =>
            Call(eqInt, MayhemFixKit.CloneKismet(spotRead), I(i));

        // Spot -> 1 for a back-row spot, 0 otherwise. The select is on ints so
        // no float/double coercion is involved.
        KismetExpression BackFlag(KismetExpression spotRead)
        {
            KismetExpression acc = I(0);
            for (int i = isBack.Length - 1; i >= 0; i--)
                acc = Call(selectInt, I(isBack[i] ? 1 : 0), acc, Eq(spotRead, i));
            return acc;
        }

        // row = front + (back - front) * isBack
        // neutral = 1 + (row - 1) * isPlayer
        // All terms are doubles; Enemy?/spot only feed int selects.
        KismetExpression RowMultiplier(KismetExpression baseLocal, double front, double backMult)
        {
            var row = Call(add, D(front),
                Call(multiply, D(backMult - front),
                    Call(convIntToDouble, BackFlag(Spot(baseLocal)))));
            var isEnemyDouble = Call(convIntToDouble,
                Call(selectInt, I(1), I(0), Enemy(baseLocal)));
            var isPlayerDouble = Call(subtract, D(1.0), isEnemyDouble);
            return Call(add, D(1.0), Call(multiply, Call(subtract, row, D(1.0)), isPlayerDouble));
        }

        var sourceMult = RowMultiplier(sourceBase, s.FrontDamageDealtMultiplier, s.BackDamageDealtMultiplier);
        var targetMult = RowMultiplier(targetBase, s.FrontDamageTakenMultiplier, s.BackDamageTakenMultiplier);
        var combined = new EX_CallMath
        {
            StackNode = multiply,
            Parameters = new KismetExpression[] { sourceMult, targetMult },
        };

        // 3. Build the modifier from the function's own proven FDamageModifier block.
        int addIdx = FindCallStatement(asset, stmts, "AddDamageModifier");
        if (addIdx < 4)
            throw new InvalidDataException("[rowdamage] FDamageModifier/AddDamageModifier block not found");
        var blockStart = addIdx - 4;
        if (stmts[blockStart] is not EX_Let typeLet
            || !(PartyFiveKit.FieldName(typeLet.Value)?.StartsWith("Type_", StringComparison.Ordinal) ?? false)
            || stmts[blockStart + 1] is not EX_Let valueLet
            || !(PartyFiveKit.FieldName(valueLet.Value)?.StartsWith("Value_", StringComparison.Ordinal) ?? false)
            || stmts[blockStart + 2] is not EX_LetBool affects
            || affects.VariableExpression is not EX_StructMemberContext affectsMember
            || !(PartyFiveKit.FieldName(affectsMember.StructMemberExpression)?.StartsWith("AffectsBaseDamages_", StringComparison.Ordinal) ?? false)
            || stmts[blockStart + 3] is not EX_Let reasonLet
            || !(PartyFiveKit.FieldName(reasonLet.Value)?.StartsWith("Reason_", StringComparison.Ordinal) ?? false))
        {
            throw new InvalidDataException("[rowdamage] unexpected FDamageModifier block shape");
        }

        var textConst = FindTextConst(stmts)
            ?? throw new InvalidDataException("[rowdamage] no text literal for the modifier reason");

        var inserted = new List<KismetExpression>
        {
            MayhemFixKit.CloneKismet(targetCall),
            MayhemFixKit.CloneKismet(sourceCall),
            MayhemFixKit.CloneKismet(typeLet),
            MayhemFixKit.CloneKismet(valueLet),
            MayhemFixKit.CloneKismet(affects),
            MayhemFixKit.CloneKismet(reasonLet),
            MayhemFixKit.CloneKismet(stmts[addIdx]),
        };
        ((EX_Let)inserted[3]).Expression = combined;
        ((EX_Let)inserted[5]).Expression = MayhemFixKit.CloneKismet(textConst);

        uint insertedSize = (uint)inserted.Sum(x => x.GetSize(asset));
        stmts.InsertRange(applyIdx, inserted);
        fn.ScriptBytecode = stmts.ToArray();

        PartyFiveKit.RemapOffsetsForInsertion(asset, fn, oldStarts, applyIdx, insertedSize, inserted.Count);
        MayhemFixKit.RecalcContextSkips(asset, fn);
        PartyFiveKit.VerifyJumps(asset, fn);
        return 1;
    }

    /// <summary>True when the function already contains a multiplier feeding
    /// an AddDamageModifier (our row block).</summary>
    private static bool HasRowModifier(UAsset asset, IReadOnlyList<KismetExpression> stmts)
    {
        for (int i = 0; i < stmts.Count; i++)
        {
            if (!ContainsCall(asset, stmts[i], "AddDamageModifier")) continue;
            if (i >= 3 && stmts[i - 3] is EX_Let let
                && let.Expression is EX_CallMath cm
                && Name(asset, cm.StackNode) == "Multiply_DoubleDouble"
                && cm.Parameters is { Length: 2 })
                return true;
        }
        return false;
    }

    private static KismetExpression? FindTargetBaseCall(UAsset asset, IReadOnlyList<KismetExpression> stmts)
    {
        foreach (var s in stmts)
        {
            if (s is EX_LocalVirtualFunction vf
                && vf.VirtualFunctionName.ToString() == "GetCharacterBattleBase"
                && vf.Parameters is { Length: 2 }
                && vf.Parameters[0] is EX_LocalVariable lv
                && PartyFiveKit.FieldName(lv.Variable) == "CallFunc_GetCharacterBattleBase_jRPG_CharacterBattleBase_3")
                return s;
        }
        return null;
    }

    private static KismetExpression? FindSourceBaseCall(UAsset asset, IReadOnlyList<KismetExpression> stmts,
        EX_LocalVariable sourceActor)
    {
        foreach (var s in stmts)
        {
            if (s is not EX_Context ctx) continue;
            if (ctx.ContextExpression is not EX_LocalVirtualFunction vf
                || vf.VirtualFunctionName.ToString() != "GetCharacterBattleBase"
                || vf.Parameters is not { Length: 2 }
                || vf.Parameters[0] is not EX_LocalVariable lv
                || PartyFiveKit.FieldName(lv.Variable) != "CallFunc_GetCharacterBattleBase_jRPG_CharacterBattleBase_4")
                continue;
            if (ctx.ObjectExpression is EX_LocalVariable obj
                && PartyFiveKit.FieldName(obj.Variable) == PartyFiveKit.FieldName(sourceActor.Variable))
                return s;
        }
        return null;
    }

    private static EX_TextConst? FindTextConst(IReadOnlyList<KismetExpression> stmts)
    {
        EX_TextConst? fallback = null;
        EX_TextConst? plain = null;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is not EX_TextConst tc) return;
                fallback ??= tc;
                var body = TextBody(tc);
                if (body.Contains("Damages received", StringComparison.OrdinalIgnoreCase)) plain ??= tc;
                else if (plain is null && body.Length > 0 && !body.Contains('{')) plain ??= tc;
            });
        return plain ?? fallback;
    }

    private static string TextBody(EX_TextConst tc)
    {
        var text = tc.Value;
        if (text?.InvariantLiteralString is EX_StringConst inv) return inv.Value ?? "";
        if (text?.LiteralString is EX_StringConst lit) return lit.Value ?? "";
        return "";
    }

    private static EX_LocalVariable Local(FunctionExport fn, string name)
    {
        EX_LocalVariable? hit = null;
        foreach (var s in fn.ScriptBytecode ?? Array.Empty<KismetExpression>())
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (hit is null && e is EX_LocalVariable lv
                    && PartyFiveKit.FieldName(lv.Variable) == name)
                    hit = lv;
            });
        return hit is null
            ? throw new InvalidDataException($"[rowdamage] local '{name}' not found")
            : (EX_LocalVariable)MayhemFixKit.CloneKismet(hit);
    }

    private static int FindCallStatement(UAsset asset, IReadOnlyList<KismetExpression> stmts, string name)
    {
        for (int i = 0; i < stmts.Count; i++)
            if (ContainsCall(asset, stmts[i], name)) return i;
        return -1;
    }

    private static bool ContainsCall(UAsset asset, KismetExpression stmt, string name)
    {
        bool found = false;
        MayhemFixKit.Walk(new[] { stmt }, e =>
        {
            if (found) return;
            if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == name) found = true;
            else if (e is EX_VirtualFunction v && v.VirtualFunctionName.ToString() == name) found = true;
        });
        return found;
    }

    private static string Name(UAsset asset, FPackageIndex? idx) =>
        idx is not null && idx.IsImport() && Math.Abs(idx.Index) - 1 < asset.Imports.Count
            ? asset.Imports[Math.Abs(idx.Index) - 1].ObjectName.ToString()
            : "?";

    private static FunctionExport Fn(UAsset asset, string name) =>
        asset.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == name)
        ?? throw new InvalidDataException($"[rowdamage] function '{name}' not found");

    private static UAsset Open(string path)
    {
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
    }

    private static void Write(UAsset asset, string path)
    {
        try { asset.Write(path); }
        catch (FormatException ex) { throw new FormatException(Path.GetFileName(path) + ": " + ex.Message, ex); }
    }

    // ─────────────────────────── verification ───────────────────────────

    public static bool Verify(string uassetPath, int[] back, Settings s)
    {
        var asset = Open(uassetPath);
        var fn = Fn(asset, FunctionName);
        var stmts = fn.ScriptBytecode;
        if (stmts is null) return false;

        int selects = 0, convs = 0, multiplies = 0, battleSpots = 0, enemies = 0, adds = 0, badCtx = 0;
        bool rowScales = true;
        foreach (var st in stmts)
            MayhemFixKit.Walk(new[] { st }, e =>
            {
                if (e is EX_CallMath cm && Name(asset, cm.StackNode) == "SelectInt") selects++;
                if (e is EX_CallMath cv && Name(asset, cv.StackNode) == "Conv_IntToDouble") convs++;
                if (e is EX_CallMath mm && Name(asset, mm.StackNode) == "Multiply_DoubleDouble") multiplies++;
                if (e is EX_LocalVirtualFunction vf && vf.VirtualFunctionName.ToString() == "AddDamageModifier") adds++;
                if (e is EX_InstanceVariable iv)
                {
                    var nm = PartyFiveKit.FieldName(iv.Variable);
                    if (nm == "BattleSpotIndex") battleSpots++;
                    if (nm == "Enemy?") enemies++;
                }
                if (e is EX_Context ctx && ctx.Offset != (uint)ctx.ContextExpression.GetSize(asset)) badCtx++;
            });

        // The row formula stores front and (back - front). Check the constants
        // that actually drive the arithmetic are present in the function.
        var doubles = new List<double>();
        foreach (var st in stmts)
            MayhemFixKit.Walk(new[] { st }, e =>
            {
                if (e is EX_DoubleConst d) doubles.Add(d.Value);
            });
        // The row formula stores front and (back - front), not the literal
        // back multiplier, so verify the deltas that actually drive the select.
        double dealtDelta = s.BackDamageDealtMultiplier - s.FrontDamageDealtMultiplier;
        double takenDelta = s.BackDamageTakenMultiplier - s.FrontDamageTakenMultiplier;
        if (Math.Abs(dealtDelta) > 1e-9) rowScales &= Contains(doubles, dealtDelta);
        if (Math.Abs(takenDelta) > 1e-9) rowScales &= Contains(doubles, takenDelta);
        rowScales &= Contains(doubles, s.FrontDamageDealtMultiplier);

        try { PartyFiveKit.VerifyJumps(asset, fn); }
        catch { return false; }

        bool ok = selects >= 8 && convs >= 4 && multiplies >= 1 && battleSpots >= 2 && enemies >= 2
                  && adds >= 2 && badCtx == 0 && rowScales;
        Console.WriteLine(
            $"[rowdamage] verify: selects={selects} convs={convs} multiplies={multiplies} " +
            $"spots={battleSpots} enemies={enemies} adds={adds} badCtx={badCtx} scales={rowScales} -> {ok}");
        return ok;
    }

    private static bool Contains(IEnumerable<double> values, double want) =>
        values.Any(v => Math.Abs(v - want) < 1e-9);

    // ─────────────────────────── staging ───────────────────────────

    private static string CopyIntoMod(string modDir)
    {
        var existing = FileCopy.FindUnder(modDir, AssetName + ".uasset");
        if (existing is not null) return existing;

        var src = FileCopy.FindTablesAsset(AssetName + ".uasset");
        if (src is null)
        {
            var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "row_damage");
            Directory.CreateDirectory(dir);
            new Retoc().ToLegacy(dir, AssetName);
            src = FileCopy.FindUnder(dir, AssetName + ".uasset")
                ?? throw new FileNotFoundException(AssetName + ".uasset");
        }

        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) throw new InvalidDataException(AssetName + " is not under Sandfall/");
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }
}

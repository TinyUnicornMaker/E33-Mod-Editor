using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Source-based damage caps in <c>BP_BattleDamages.ApplyClamp</c>.
///
/// Vanilla has one hard-coded 9,999 cap for heroes, removed entirely once
/// <c>DamageLimitUnlockRefCount</c> is present. This kit replaces that flat cap
/// with a <c>DamageReason</c> lookup and adds the unlocked-state caps:
///
/// <code>
/// reason                     locked     Ultima Power
/// Invalid (0)                 9,999      333,333
/// SkillCast (1)              33,333      unlimited (0)
/// BuffEffect / Burn (2)       3,333      333,333
/// FreeAim (3)                 3,333      333,333
/// Attack (4)                  6,666      333,333
/// Counter (5)               333,333      unlimited (0)
/// Lumina (6)                  9,999      333,333
/// Item (7)                    9,999      333,333
/// GradientCounter (8)       333,333      unlimited (0)
/// RangeCounter (9)          333,333      unlimited (0)
/// AOECounter (10)           333,333      unlimited (0)
/// JumpCounter (11)          333,333      unlimited (0)
/// anything else               9,999      333,333
/// </code>
///
/// The numeric values above come from <c>EDamageReason</c>'s cooked
/// <c>DisplayNameMap</c>, not the enumerator names:
/// <c>NewEnumerator2=0</c> is Invalid, <c>NewEnumerator0=1</c> is SkillCast and
/// <c>NewEnumerator1=2</c> is BuffEffect. Burn's aggregator passes reason 2,
/// so Burn is the BuffEffect row (3,333 / 333,333), not Invalid.
///
/// The locked caps are inserted directly after the vanilla 9,999 assignment
/// (inside the "not unlocked" branch). The unlocked caps are a second block
/// before the challenge override: when <c>local_clampValue</c> is still 0
/// (unlocked), it is replaced by the unlocked-state cap. A 0 result means the
/// final <c>if (local_clampValue &gt; 0)</c> clamp is skipped, i.e. unlimited.
/// Challenge overrides still run afterwards and can lower any of these caps.
///
/// No new properties, no reference-counter tier, and no new picto are needed.
/// </summary>
public static class DamageCapKit
{
    public const string BattleDamages = "BP_BattleDamages";
    public const string ClampFunction = "ApplyClamp";

    // Locked (no Ultima Power).
    public const int BuffEffectCap = 3333;
    public const int SkillCap = 33333;
    public const int FreeAimCap = 3333;
    public const int AttackCap = 6666;
    public const int CounterCap = 333333;
    public const int OtherCap = 9999;

    // Unlocked (Ultima Power).
    public const int UnlockedOtherCap = 333333;
    public const int Unlimited = 0;

    // Cooked EDamageReason values: NewEnumerator2=0 (Invalid),
    // NewEnumerator0=1 (SkillCast), NewEnumerator1=2 (BuffEffect).
    // Burn's DOT tick passes BuffEffect (2).
    public const byte Invalid = 0;
    public const byte SkillCast = 1;
    public const byte BuffEffect = 2;
    public const byte FreeAim = 3;
    public const byte Attack = 4;
    public const byte Counter = 5;
    public const byte Lumina = 6;
    public const byte Item = 7;
    public const byte GradientCounter = 8;
    public const byte RangeCounter = 9;
    public const byte AoeCounter = 10;
    public const byte JumpCounter = 11;

    public static readonly byte[] CounterReasons =
        { Counter, GradientCounter, RangeCounter, AoeCounter, JumpCounter };

    /// <summary>Cap used while the hero has no damage-limit unlock reference.</summary>
    public static int LockedCap(byte reason) => reason switch
    {
        BuffEffect => BuffEffectCap, // Burn's DOT tick uses this reason
        SkillCast => SkillCap,
        FreeAim => FreeAimCap,
        Attack => AttackCap,
        Counter or GradientCounter or RangeCounter or AoeCounter or JumpCounter => CounterCap,
        _ => OtherCap,
    };

    /// <summary>
    /// Cap used while the hero has the unlock reference (Ultima Power).
    /// Skills and counters are unlimited; everything else is 333,333.
    /// </summary>
    public static int UnlockedCap(byte reason) =>
        reason is SkillCast or Counter or GradientCounter or RangeCounter or AoeCounter or JumpCounter
            ? Unlimited
            : UnlockedOtherCap;

    // ───────────────────────────── pack ─────────────────────────────

    /// <summary>Stage and patch BP_BattleDamages in a mod directory.</summary>
    public static int Apply(string modDir)
    {
        var path = FileCopy.FindUnder(modDir, BattleDamages + ".uasset")
            ?? Stage(modDir);
        var asset = Open(path);
        int n = PatchApplyClamp(asset);
        if (n > 0) asset.Write(path);
        if (!Verify(path))
            throw new InvalidDataException(
                "[damagecap] verification failed after writing " + BattleDamages);
        return n;
    }

    public static bool Verify(string uassetPath)
    {
        var asset = Open(uassetPath);
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == ClampFunction);
        if (fn?.ScriptBytecode is null) return false;

        bool unlockCheck = false, challenge = false, fclamp = false, unlockedEquals = false;
        int selectInt = 0, convDouble = 0, reasonReads = 0;
        var ints = new HashSet<int>();
        var reasons = new HashSet<byte>();
        foreach (var st in fn.ScriptBytecode)
            MayhemFixKit.Walk(new[] { st }, e =>
            {
                switch (e)
                {
                    case EX_CallMath cm:
                        var name = ImportName(asset, cm.StackNode);
                        if (name == "SelectInt") selectInt++;
                        if (name == "Conv_IntToDouble") convDouble++;
                        if (name == "FClamp") fclamp = true;
                        if (name == "GetReferenceCounterState") unlockCheck = true;
                        if (name == "EqualEqual_DoubleDouble"
                            && cm.Parameters is { Length: 2 }
                            && cm.Parameters[0] is EX_LocalVariable lv
                            && PartyFiveKit.FieldName(lv.Variable) == "local_clampValue"
                            && cm.Parameters[1] is EX_DoubleConst d
                            && Math.Abs(d.Value) < 1e-9)
                            unlockedEquals = true;
                        break;
                    case EX_LocalVirtualFunction vf:
                        var vn = vf.VirtualFunctionName.ToString();
                        if (vn == "Challenge_GetHeroesDamageLimitOverride") challenge = true;
                        if (vn == "GetReferenceCounterState") unlockCheck = true;
                        break;
                    case EX_InstanceVariable iv:
                        if (PartyFiveKit.FieldName(iv.Variable) == "DamageReason") reasonReads++;
                        break;
                    case EX_IntConst ic:
                        ints.Add(ic.Value);
                        break;
                    case EX_ByteConst bc:
                        reasons.Add(bc.Value);
                        break;
                }
            });

        // Two cap statements: the locked replacement and the unlocked override.
        var capLets = new List<EX_Let>();
        foreach (var st in fn.ScriptBytecode)
            if (st is EX_Let let
                && PartyFiveKit.FieldName(let.Value) == "local_clampValue"
                && let.Expression is EX_CallMath cm
                && ImportName(asset, cm.StackNode) == "Conv_IntToDouble")
                capLets.Add(let);

        if (capLets.Count != 2)
        {
            Console.WriteLine($"[damagecap] verify: cap statements={capLets.Count} (want 2)");
            return false;
        }

        // The unlocked block must be: equality test, JumpIfNot, then the cap
        // assignment; the jump skips the assignment when the test is false.
        bool unlockedFlowOk = false;
        var starts = PartyFiveKit.StatementStarts(asset, fn.ScriptBytecode);
        for (int i = 0; i + 2 < fn.ScriptBytecode.Length; i++)
        {
            if (fn.ScriptBytecode[i] is not EX_LetBool lb
                || lb.AssignmentExpression is not EX_CallMath test
                || ImportName(asset, test.StackNode) != "EqualEqual_DoubleDouble")
                continue;
            if (fn.ScriptBytecode[i + 1] is not EX_JumpIfNot jn
                || fn.ScriptBytecode[i + 2] is not EX_Let let
                || PartyFiveKit.FieldName(let.Value) != "local_clampValue")
                continue;
            unlockedFlowOk = i + 3 < starts.Count && jn.CodeOffset == starts[i + 3];
            break;
        }

        // Evaluate the installed expressions rather than trusting constants.
        for (byte reason = 0; reason <= 11; reason++)
        {
            var locked = Eval(asset, capLets[0].Expression, reason);
            var unlocked = Eval(asset, capLets[1].Expression, reason);
            if (locked is null || Math.Abs(locked.Value - LockedCap(reason)) > 1e-9)
            {
                Console.WriteLine($"[damagecap] verify: locked reason {reason} -> {locked?.ToString() ?? "null"} " +
                                  $"(want {LockedCap(reason)})");
                return false;
            }
            if (unlocked is null || Math.Abs(unlocked.Value - UnlockedCap(reason)) > 1e-9)
            {
                Console.WriteLine($"[damagecap] verify: unlocked reason {reason} -> {unlocked?.ToString() ?? "null"} " +
                                  $"(want {UnlockedCap(reason)})");
                return false;
            }
        }

        try { PartyFiveKit.VerifyJumps(asset, fn); }
        catch { return false; }

        bool constants = ints.Contains(BuffEffectCap) && ints.Contains(SkillCap)
                         && ints.Contains(FreeAimCap) && ints.Contains(AttackCap)
                         && ints.Contains(CounterCap) && ints.Contains(OtherCap)
                         && ints.Contains(UnlockedOtherCap)
                         && ints.Contains(Unlimited);
        bool reasonSet = reasons.Contains(BuffEffect) && reasons.Contains(SkillCast)
                         && reasons.Contains(FreeAim) && reasons.Contains(Attack)
                         && CounterReasons.All(reasons.Contains);

        bool ok = unlockCheck && challenge && fclamp && unlockedEquals && unlockedFlowOk
                  && selectInt >= 10 && convDouble == 2
                  && reasonReads >= 2 && constants && reasonSet;
        Console.WriteLine($"[damagecap] verify {ClampFunction}: caps=2 unlock={unlockCheck} " +
                          $"challenge={challenge} clamp={fclamp} unlockedEq={unlockedEquals} " +
                          $"flow={unlockedFlowOk} select={selectInt} conv={convDouble} " +
                          $"reasons={reasonReads} constants={constants} reasonSet={reasonSet} -> {ok}");
        return ok;
    }

    // ─────────────────────────── bytecode patch ───────────────────────────

    public static int PatchApplyClamp(UAsset asset)
    {
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == ClampFunction)
            ?? throw new InvalidDataException($"[damagecap] {ClampFunction} not found");
        if (fn.ScriptBytecode is null || fn.ScriptBytecode.Length == 0)
            throw new InvalidDataException($"[damagecap] {ClampFunction} has no bytecode");

        var stmts = fn.ScriptBytecode.ToList();
        if (HasSourceCaps(stmts)) return 0;

        int clampLetIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            if (stmts[i] is EX_Let let
                && PartyFiveKit.FieldName(let.Value) == "local_clampValue"
                && let.Expression is EX_DoubleConst dc && Math.Abs(dc.Value - 9999) < 1e-9)
            {
                clampLetIdx = i;
                break;
            }
        }
        if (clampLetIdx < 0)
            throw new InvalidDataException("[damagecap] vanilla 9,999 clamp not found");

        int challengeLetIdx = FindChallengeLet(stmts);
        if (challengeLetIdx < 0)
            throw new InvalidDataException("[damagecap] challenge override anchor not found");

        var clampLet = (EX_Let)stmts[clampLetIdx];
        var reason = FindInstance(asset, "DamageReason")
            ?? throw new InvalidDataException("[damagecap] DamageReason property not found");
        var eq = LuneSkillsKit.EnsureKismetMathFn(asset, "EqualEqual_ByteByte");
        var eqDouble = LuneSkillsKit.EnsureKismetMathFn(asset, "EqualEqual_DoubleDouble");
        var select = LuneSkillsKit.EnsureKismetMathFn(asset, "SelectInt");
        var conv = LuneSkillsKit.EnsureKismetMathFn(asset, "Conv_IntToDouble");

        KismetExpression ReasonIs(byte r) => new EX_CallMath
        {
            StackNode = eq,
            Parameters = new KismetExpression[]
            {
                (EX_InstanceVariable)MayhemFixKit.CloneKismet(reason),
                new EX_ByteConst { Value = r },
            },
        };

        KismetExpression Select(int value, KismetExpression previous, byte r) =>
            new EX_CallMath
            {
                StackNode = select,
                Parameters = new KismetExpression[]
                {
                    new EX_IntConst { Value = value },
                    previous,
                    ReasonIs(r),
                },
            };

        KismetExpression Conv(KismetExpression e) => new EX_CallMath
        {
            StackNode = conv,
            Parameters = new KismetExpression[] { e },
        };

        // Locked caps. Unlisted reasons keep the vanilla 9,999.
        KismetExpression locked = new EX_IntConst { Value = OtherCap };
        locked = Select(BuffEffectCap, locked, BuffEffect);
        locked = Select(SkillCap, locked, SkillCast);
        locked = Select(FreeAimCap, locked, FreeAim);
        locked = Select(AttackCap, locked, Attack);
        foreach (var r in CounterReasons)
            locked = Select(CounterCap, locked, r);
        locked = Conv(locked);

        // Ultima Power: skills/counters unlimited, everything else 333,333.
        KismetExpression unlocked = new EX_IntConst { Value = UnlockedOtherCap };
        foreach (var r in new[] { SkillCast, Counter, GradientCounter, RangeCounter, AoeCounter, JumpCounter })
            unlocked = Select(Unlimited, unlocked, r);
        unlocked = Conv(unlocked);

        // 1) Insert the locked cap right after the vanilla 9,999 assignment.
        //    The unlock branch's pop target skips it because it lands on the
        //    original EX_PopExecutionFlow, which is shifted after the insert.
        var lockedLet = (EX_Let)MayhemFixKit.CloneKismet(clampLet);
        lockedLet.Expression = locked;
        var oldStarts = PartyFiveKit.StatementStarts(asset, stmts);
        stmts.Insert(clampLetIdx + 1, lockedLet);
        fn.ScriptBytecode = stmts.ToArray();
        uint lockedSize = (uint)lockedLet.GetSize(asset);
        PartyFiveKit.RemapOffsetsForInsertion(asset, fn, oldStarts, clampLetIdx + 1, lockedSize, 1);
        MayhemFixKit.RecalcContextSkips(asset, fn);

        // 2) Insert the unlocked cap before the challenge override. At this
        //    point local_clampValue is still 0 only on the unlocked path.
        stmts = fn.ScriptBytecode!.ToList();
        challengeLetIdx = FindChallengeLet(stmts);
        if (challengeLetIdx < 0)
            throw new InvalidDataException("[damagecap] challenge anchor lost after first insert");

        var localRead = Local(fn, "local_clampValue");
        var notEqTpl = stmts.OfType<EX_LetBool>().FirstOrDefault(lb =>
            LocalName(lb.VariableExpression) == "CallFunc_NotEqual_DoubleDouble_ReturnValue"
            && lb.AssignmentExpression is EX_CallMath cm
            && ImportName(asset, cm.StackNode) == "NotEqual_DoubleDouble")
            ?? throw new InvalidDataException("[damagecap] NotEqual bool template not found");
        var testUnlocked = (EX_LetBool)MayhemFixKit.CloneKismet(notEqTpl);
        var isZero = (EX_CallMath)testUnlocked.AssignmentExpression;
        // The local name is just the donor template's name. `JumpIfNot` jumps
        // when the condition is FALSE, so this must be EqualEqual(local, 0):
        // true = unlocked -> do not jump -> apply the unlocked cap.
        // NotEqual here would apply the unlocked cap on the locked path and
        // skip it on the unlocked path (the exact opposite of the intent).
        isZero.StackNode = eqDouble;
        isZero.Parameters = new KismetExpression[]
        {
            (EX_LocalVariable)MayhemFixKit.CloneKismet(localRead),
            new EX_DoubleConst { Value = 0 },
        };
        var boolRead = (EX_LocalVariable)MayhemFixKit.CloneKismet(testUnlocked.VariableExpression);
        var skip = new EX_JumpIfNot
        {
            BooleanExpression = boolRead,
            CodeOffset = 0, // filled after insertion
        };
        var unlockedLet = (EX_Let)MayhemFixKit.CloneKismet(clampLet);
        unlockedLet.Expression = unlocked;

        var block = new List<KismetExpression> { testUnlocked, skip, unlockedLet };
        var oldStarts2 = PartyFiveKit.StatementStarts(asset, stmts);
        stmts.InsertRange(challengeLetIdx, block);
        fn.ScriptBytecode = stmts.ToArray();
        uint blockSize = (uint)block.Sum(x => x.GetSize(asset));
        PartyFiveKit.RemapOffsetsForInsertion(asset, fn, oldStarts2, challengeLetIdx, blockSize, block.Count);
        MayhemFixKit.RecalcContextSkips(asset, fn);

        var starts = PartyFiveKit.StatementStarts(asset, fn.ScriptBytecode!);
        skip.CodeOffset = starts[challengeLetIdx + block.Count];
        PartyFiveKit.VerifyJumps(asset, fn);

        Console.WriteLine($"[damagecap] {ClampFunction}: source caps installed");
        return 1;
    }

    // ───────────────────────────── helpers ─────────────────────────────

    private static bool HasSourceCaps(IReadOnlyList<KismetExpression> stmts)
    {
        foreach (var st in stmts)
            if (st is EX_Let let
                && PartyFiveKit.FieldName(let.Value) == "local_clampValue"
                && let.Expression is EX_CallMath cm
                && cm.StackNode is not null && cm.StackNode.IsImport())
                return true;
        return false;
    }

    private static int FindChallengeLet(IReadOnlyList<KismetExpression> stmts)
    {
        for (int i = 0; i < stmts.Count; i++)
            if (stmts[i] is EX_Let let
                && PartyFiveKit.FieldName(let.Value) == "CallFunc_Challenge_GetHeroesDamageLimitOverride_ReturnValue")
                return i;
        return -1;
    }

    private static string? LocalName(KismetExpression? e) => e switch
    {
        EX_LocalVariable lv => PartyFiveKit.FieldName(lv.Variable),
        EX_LocalOutVariable ov => PartyFiveKit.FieldName(ov.Variable),
        _ => null,
    };

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
            ? throw new InvalidDataException($"[damagecap] local '{name}' not found")
            : (EX_LocalVariable)MayhemFixKit.CloneKismet(hit);
    }

    private static EX_InstanceVariable? FindInstance(UAsset asset, string field)
    {
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
            foreach (var s in fn.ScriptBytecode ?? Array.Empty<KismetExpression>())
            {
                EX_InstanceVariable? hit = null;
                MayhemFixKit.Walk(new[] { s }, e =>
                {
                    if (hit is null && e is EX_InstanceVariable iv
                        && PartyFiveKit.FieldName(iv.Variable) == field)
                        hit = iv;
                });
                if (hit is not null) return hit;
            }
        return null;
    }

    private static double? Eval(UAsset asset, KismetExpression e, byte reason)
    {
        switch (e)
        {
            case EX_IntConst ic: return ic.Value;
            case EX_ByteConst bc: return bc.Value;
            case EX_InstanceVariable iv when PartyFiveKit.FieldName(iv.Variable) == "DamageReason":
                return reason;
            case EX_CallMath cm:
                var name = ImportName(asset, cm.StackNode);
                if (name == "Conv_IntToDouble" && cm.Parameters is { Length: 1 })
                    return Eval(asset, cm.Parameters[0], reason);
                if (name == "SelectInt" && cm.Parameters is { Length: 3 })
                    return Truthy(asset, cm.Parameters[2], reason)
                        ? Eval(asset, cm.Parameters[0], reason)
                        : Eval(asset, cm.Parameters[1], reason);
                if (name == "EqualEqual_ByteByte" && cm.Parameters is { Length: 2 })
                {
                    var a = Eval(asset, cm.Parameters[0], reason);
                    var b = Eval(asset, cm.Parameters[1], reason);
                    return a is not null && b is not null && Math.Abs(a.Value - b.Value) < 1e-9 ? 1 : 0;
                }
                return null;
            default: return null;
        }
    }

    private static bool Truthy(UAsset asset, KismetExpression e, byte reason)
    {
        var v = Eval(asset, e, reason);
        return v is > 0;
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

    private static string Stage(string modDir)
    {
        var src = FileCopy.FindUnder(
                Path.Combine(EditorPaths.RepoRoot, "work", "tables", "battle_damages"),
                BattleDamages + ".uasset")
            ?? FileCopy.FindTablesAsset(BattleDamages + ".uasset");
        if (src is null)
        {
            var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "battle_damages");
            Directory.CreateDirectory(dir);
            using (E33ModEditor.Core.Packer.DonorExtract.SuspendMods())
                new Retoc().ToLegacy(dir, BattleDamages);
            src = FileCopy.FindUnder(dir, BattleDamages + ".uasset")
                ?? throw new FileNotFoundException(BattleDamages + ".uasset");
        }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) throw new InvalidDataException(BattleDamages + " is not under Sandfall/");
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }
}

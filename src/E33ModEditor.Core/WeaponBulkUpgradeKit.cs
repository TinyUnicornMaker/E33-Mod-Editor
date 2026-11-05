using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Weapon bulk upgrade (main pack): one activation upgrades up to
/// <see cref="BulkLevels"/> levels, clamped to the current catalyst tier.
///
/// <para><b>Why append-only.</b> Earlier revisions spliced the loop into the
/// middle of <c>ExecuteUbergraph_WBP_BS_WeaponUpgrade</c>; the game crashed
/// when the section opened even though the added code was unreachable, while
/// (a) padding 32 dead statements at the end of the same function and (b)
/// inserting 23 statements at the 3107 entry both ran fine. Any growth before
/// the post-upgrade block therefore breaks this widget at load, so this kit
/// never changes a statement's size in place. It only:</para>
/// <list type="number">
/// <item>repoints the click path's own <c>PushExecutionFlow</c> (vanilla pushes
/// the post-upgrade block, <c>offset 1747</c>) at a new block appended
/// <i>after</i> the function's final <c>EX_Return</c>;</item>
/// <item>appends that block: reset the stale counter, count one upgrade, stop
/// after <see cref="BulkLevels"/>, stop at the current catalyst tier, else jump
/// back into the vanilla upgrade chain (<c>offset 3107</c>).</item>
/// </list>
/// <para>Every pre-existing entry offset is untouched (nothing moves), so no
/// delegate constants change. No imports or locals are added: the counter is
/// <c>CallFunc_GetGearMaxUpgradeLevel_ReturnValue</c>, the tier ceiling is
/// <c>Temp_int_Array_Index_Variable</c>, and comparisons reuse the existing
/// <c>Less_IntInt</c>/<c>Add_IntInt</c> imports.</para>
///
/// <para><b>Tier clamp.</b> The ladder uses <c>&lt;</c> 34 / 67 / 112 / 223,
/// i.e. levels 1–33 → 33, 34–66 → 66, 67–111 → 111, 112–222 → 222, 223+ → 333.
/// Starting at 28 with T1 catalysts therefore stops at 33 (never charges a T2),
/// and the next activation continues into the T2 band. Partial affordability
/// stops the loop at the level vanilla's per-level cost check refuses.</para>
///
/// <para><b>Timing.</b> Each looped level re-enters the vanilla chain, which
/// includes the 0.2 s post-upgrade delay, so a full x10 takes roughly two
/// seconds and shows the level counter climbing.</para>
/// </summary>
public static class WeaponBulkUpgradeKit
{
    public const string WidgetStem = "WBP_BS_WeaponUpgrade";
    public const string GraphFn = "ExecuteUbergraph_WBP_BS_WeaponUpgrade";
    public const string SettingsFile = "weapon_bulk.json";
    public const string UiTable = "ST_UI_GameMenu";

    /// <summary>Levels per activation, including the vanilla one.</summary>
    public const int BulkLevels = 10;

    // Vanilla anchors (asserted before patching).
    private const uint ChainEntry = 3107;      // instance fetch → cost → consume → upgrade
    private const uint PostUpgrade = 1747;     // continuation after the 0.2 s delay
    private const uint ClickPush = 676;        // PushExecutionFlow(1747) in the click path
    private const uint VanillaReturn = 3893;   // final EX_Return (normal exit)

    // Borrowed locals (all ints; see fn-locals for WBP_BS_WeaponUpgrade).
    private const string CounterLocal = "CallFunc_GetGearMaxUpgradeLevel_ReturnValue";
    private const string TierLocal = "Temp_int_Array_Index_Variable";
    private const string LevelLocal = "CallFunc_GetCurrentLevel_ReturnValue";
    private const string BoolLocal = "CallFunc_Less_IntInt_ReturnValue";
    private const string AddRetLocal = "CallFunc_Add_IntInt_ReturnValue";

    public sealed class Settings
    {
        public bool Enabled = true;
        public int Levels = BulkLevels;
        /// <summary>Tap the upgrade button for a single level. Off by default:
        /// a completed hold's release tick is indistinguishable from a tap, so
        /// combined they overshoot. The dedicated +1 row will replace this.</summary>
        public bool TapPlusOne = false;

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                var path = Path.Combine(EditorPaths.RepoRoot, "work", SettingsFile);
                if (!File.Exists(path)) return s;
                var o = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
                if (o["Enabled"] is { } e) s.Enabled = e.ToObject<bool>();
                if (o["Levels"] is { } n) s.Levels = Math.Clamp(n.ToObject<int>(), 1, BulkLevels);
                if (o["TapPlusOne"] is { } t) s.TapPlusOne = t.ToObject<bool>();
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
            Console.WriteLine("[bulk-upgrade] disabled by work/weapon_bulk.json — skipping");
            return 0;
        }
        var dest = FileCopy.FindUnder(modDir, WidgetStem + ".uasset")
            ?? throw new FileNotFoundException("[bulk-upgrade] staged widget missing (over-level kit runs first)");
        var asset = Open(dest);
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == GraphFn)
            ?? throw new InvalidDataException($"[bulk-upgrade] {GraphFn} not found");
        if (fn.ScriptBytecode is null || fn.ScriptBytecode.Length == 0)
            throw new InvalidDataException($"[bulk-upgrade] {GraphFn} has no bytecode");

        var stmts = fn.ScriptBytecode.ToList();
        var push = FindClickPush(asset, stmts);
        if (push.PushingAddress != PostUpgrade)
        {
            if (push.PushingAddress > VanillaReturn)
            {
                Console.WriteLine("[bulk-upgrade] already applied — skipping");
                return 0;
            }
            throw new InvalidDataException(
                $"[bulk-upgrade] click push targets {push.PushingAddress}, want {PostUpgrade} — shape changed");
        }
        uint chainOff = AssertStmtAt(asset, stmts, ChainEntry, "chain entry");
        uint postOff = AssertStmtAt(asset, stmts, PostUpgrade, "post-upgrade block");
        int retIdx = stmts.FindLastIndex(s => s is EX_Return);
        if (retIdx < 0 || PartyFiveKit.StatementStarts(asset, stmts)[retIdx] != VanillaReturn)
            throw new InvalidDataException("[bulk-upgrade] final EX_Return not at the vanilla offset — shape changed");

        // ── Build the appended block ──
        _pendingFixups.Clear();
        int loopLevels = settings.TapPlusOne ? Math.Max(1, settings.Levels - 1) : settings.Levels;
        var block = new List<KismetExpression>();
        var counter = CloneLocal(stmts, CounterLocal);
        var tier = CloneLocal(stmts, TierLocal);
        var level = CloneLocal(stmts, LevelLocal);
        var addTpl = FindAddInt(asset, stmts);
        var lessTpl = FindLess(asset, stmts);
        var letIntTpl = FindLetIntConst(stmts);
        var assignTpl = FindLetAssign(stmts);
        var levelTpl = FindLevelRead(stmts);
        var getInst = FindContext(stmts, "GetGearWeaponInstance");

        void Expr(KismetExpression e) => block.Add(e);

        // stale = (counter > loopLevels-1) == Less_IntInt(loopLevels-1, counter); reset on a fresh activation
        var stale = CloneLess(lessTpl, new EX_IntConst { Value = loopLevels - 1 }, counter);
        var staleJif = Jif(BoolOf(stale), Fix.AfterReset);
        Expr(stale);
        Expr(staleJif);
        Expr(CloneLetInt(letIntTpl, counter, 0));
        int afterReset = block.Count;

        // counter = counter + 1
        var inc = (EX_Let)MayhemFixKit.CloneKismet(addTpl);
        SetCallParam(inc, 0, counter);
        Expr(inc);
        Expr(CloneAssign(assignTpl, counter));

        // if (counter >= loopLevels) stop
        var more = CloneLess(lessTpl, counter, new EX_IntConst { Value = loopLevels });
        Expr(more);
        Expr(Jif(BoolOf(more), Fix.Continuation));

        // level = instance.GetCurrentLevel()
        Expr((KismetExpression)MayhemFixKit.CloneKismet(getInst));
        Expr((KismetExpression)MayhemFixKit.CloneKismet(levelTpl));

        // tier ladder: <34→33, <67→66, <112→111, <223→222, else 333
        int[] bounds = { 34, 67, 112, 223 };
        int[] bands = { 33, 66, 111, 222 };
        var tests = new List<KismetExpression>();
        var bandJumps = new List<EX_JumpIfNot>();
        var doneJumps = new List<EX_Jump>();
        for (int i = 0; i < bounds.Length; i++)
        {
            var t = CloneLess(lessTpl, level, new EX_IntConst { Value = bounds[i] });
            var jif = new EX_JumpIfNot { CodeOffset = 0xFFFFFFFE, BooleanExpression = BoolOf(t) };
            var done = new EX_Jump { CodeOffset = 0xFFFFFFFE };
            tests.Add(t);
            bandJumps.Add(jif);
            doneJumps.Add(done);
            Expr(t);
            Expr(jif);
            Expr(CloneLetInt(letIntTpl, tier, bands[i]));
            Expr(done);
        }
        Expr(CloneLetInt(letIntTpl, tier, 333));
        int compareIdx = block.Count;
        var inTier = CloneLess(lessTpl, level, tier);
        Expr(inTier);
        Expr(Jif(BoolOf(inTier), Fix.Continuation));
        Expr(new EX_Jump { CodeOffset = 0xFFFFFFFE });   // back to the vanilla chain
        var again = (EX_Jump)block[^1];

        // ── Tap gate (optional): press tick → exactly one level ──
        List<KismetExpression>? tapBlock = null;
        EX_JumpIfNot? tapSkip = null;
        KismetExpression? tapStartStmt = null;
        var starts = PartyFiveKit.StatementStarts(asset, stmts);
        uint StartOf(KismetExpression e) => starts[stmts.IndexOf(e)];
        uint progressOff = AssertStmtAt(asset, stmts, 3415, "progress entry");
        if (settings.TapPlusOne)
        {
            var gate = BuildTapGate(asset, stmts, counter, letIntTpl, loopLevels, chainOff, progressOff);
            tapBlock = gate.Block;
            tapSkip = gate.SkipToTap;
            tapStartStmt = gate.TapStart;
            stmts.InsertRange(stmts.FindLastIndex(s => s is EX_EndOfScript), tapBlock);
            fn.ScriptBytecode = stmts.ToArray();
            starts = PartyFiveKit.StatementStarts(asset, stmts);
            tapSkip.CodeOffset = StartOf(tapStartStmt!);
            RetargetProgressDelegate(asset, StartOf(tapBlock[0]));
        }

        // ── Append after the final Return (nothing else moves) ──
        int endIdx = stmts.FindLastIndex(s => s is EX_EndOfScript);
        if (endIdx < 0) throw new InvalidDataException("[bulk-upgrade] EX_EndOfScript not found");
        int insertAt = endIdx;
        stmts.InsertRange(insertAt, block);
        fn.ScriptBytecode = stmts.ToArray();
        starts = PartyFiveKit.StatementStarts(asset, stmts);
        uint blockStart = StartOf(block[0]);

        // ── Resolve intra-block + external targets by reference ──
        foreach (var (node, target) in _pendingFixups)
        {
            uint off = target switch
            {
                Fix.AfterReset => StartOf(block[afterReset]),
                Fix.Continuation => postOff,
                _ => throw new InvalidDataException("[bulk-upgrade] unknown fixup"),
            };
            SetJump(node, off);
        }
        for (int i = 0; i < bandJumps.Count; i++)
        {
            KismetExpression next = i + 1 < tests.Count ? tests[i + 1] : block[compareIdx - 1];
            bandJumps[i].CodeOffset = StartOf(next);
            doneJumps[i].CodeOffset = StartOf(block[compareIdx]);
        }
        again.CodeOffset = chainOff;

        // ── Redirect the click path's push at the appended block ──
        push.PushingAddress = blockStart;

        MayhemFixKit.RecalcContextSkips(asset, fn);
        AssertNoPlaceholders(fn);
        asset.Write(dest);

        Verify(modDir, settings);
        Console.WriteLine($"[bulk-upgrade] {WidgetStem}.{GraphFn}: append-only x{settings.Levels} loop " +
                          $"({block.Count} statement(s) after the final Return; click push {ClickPush}→{blockStart})");
        UpdateHintText(modDir, settings);
        UpdatePromptLabel(modDir, settings);
        return 1;
    }

    // ────────────────────────── second prompt row ──────────────────────────

    public const string BaseAction = "CommonUI_GameMenu_UpgradeWeapon";
    public const string InputActionsTable = "ST_UI_InputActions";

    /// <summary>Updates the button prompt text ("Upgrade N Levels"). The label
    /// is the input action's display name, so this is static per pack.</summary>
    private static int UpdatePromptLabel(string modDir, Settings settings)
    {
        var strings = StageTable(modDir, InputActionsTable);
        if (strings is null) return 0;
        DataTableEditor.AddStringTableEntries(strings, new Dictionary<string, string>
        {
            ["ST_IA_WEAPON_UPGRADE"] = $"Upgrade {settings.Levels} Levels",
        });
        Console.WriteLine($"[bulk-upgrade] prompt label: Upgrade {settings.Levels} Levels");
        return 1;
    }

    /// <summary>Returns a staged copy of <paramref name="stem"/> (patching the
    /// copy another kit already staged), or null if the vanilla asset is absent.</summary>
    private static string? StageTable(string modDir, string stem)
    {
        var staged = FileCopy.FindUnder(modDir, stem + ".uasset");
        if (staged is not null) return staged;
        var src = FileCopy.FindTablesAsset(stem + ".uasset");
        if (src is null)
        {
            Console.WriteLine($"[bulk-upgrade] {stem} not extracted — skipped");
            return null;
        }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        var rel = idx >= 0 ? src[idx..] : Path.Combine("Sandfall", "Content", stem + ".uasset");
        var dest = Path.Combine(modDir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    /// <summary>
    /// Writes the in-game hint: the Curator tile description plus the upgrade
    /// panel's "Required Materials" header. Patches the already-staged table
    /// when another kit owns it, so the edits compose instead of overwriting.
    /// </summary>
    private static int UpdateHintText(string modDir, Settings settings)
    {
        var staged = FileCopy.FindUnder(modDir, UiTable + ".uasset");
        string dest;
        if (staged is not null)
        {
            dest = staged;
        }
        else
        {
            var src = FileCopy.FindTablesAsset(UiTable + ".uasset");
            if (src is null)
            {
                Console.WriteLine($"[bulk-upgrade] {UiTable} not extracted — hint text skipped");
                return 0;
            }
            var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
            var rel = idx >= 0 ? src[idx..] : Path.Combine("Sandfall", "Content", "StringTables", "UI", UiTable + ".uasset");
            dest = Path.Combine(modDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            FileCopy.AssetPair(src, dest);
        }

        var entries = new Dictionary<string, string>
        {
            ["ST_GM_FLAG_UP_WEAPONS_DESC"] =
                $"Upgrade weapons with chroma catalysts. Hold the upgrade button to raise a weapon by up to {settings.Levels} levels at once (stops at the next catalyst tier).",
            ["ST_GM_BS_REQUIRED_MATS"] = "Required Materials",
        };
        DataTableEditor.AddStringTableEntries(dest, entries);
        Console.WriteLine($"[bulk-upgrade] {UiTable}: {entries.Count} hint string(s) written");
        return entries.Count;
    }

    /// <summary>Re-opens the staged widget and asserts the append-only patch survived.</summary>
    public static void Verify(string modDir) => Verify(modDir, Settings.Load());

    internal static void Verify(string modDir, Settings settings)
    {
        _ = settings;
        var path = FileCopy.FindUnder(modDir, WidgetStem + ".uasset")
            ?? throw new FileNotFoundException("[bulk-upgrade] staged widget missing");
        var asset = Open(path);
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == GraphFn)
            ?? throw new InvalidDataException("[bulk-upgrade] graph function missing");
        var list = fn.ScriptBytecode!.ToList();
        var starts = PartyFiveKit.StatementStarts(asset, list);

        // This kit adds imports only for the optional tap gate.
        bool hasGreater = asset.Imports.Any(i => i.ObjectName.ToString() == "Greater_DoubleDouble");
        bool hasValid = asset.Imports.Any(i => i.ObjectName.ToString() == "IsValid");
        bool wantTap = hasGreater || hasValid;
        if (hasGreater != hasValid)
            throw new InvalidDataException("[bulk-upgrade] half-present tap gate (one import missing)");
        if (wantTap != settings.TapPlusOne)
            throw new InvalidDataException($"[bulk-upgrade] tap gate present={wantTap}, want {settings.TapPlusOne}");

        var push = FindClickPush(asset, list);
        if (push.PushingAddress <= VanillaReturn)
            throw new InvalidDataException($"[bulk-upgrade] click push not redirected ({push.PushingAddress})");

        if (settings.TapPlusOne)
        {
            var del = asset.Exports.OfType<FunctionExport>().FirstOrDefault(f =>
                f.ObjectName.ToString().EndsWith("_1_OnButtonBaseActionProgress__DelegateSignature", StringComparison.Ordinal))
                ?? throw new InvalidDataException("[bulk-upgrade] progress delegate missing");
            int entry = -1;
            foreach (var s in del.ScriptBytecode ?? Array.Empty<KismetExpression>())
                MayhemFixKit.Walk(new[] { s }, e => { if (e is EX_IntConst ic) entry = ic.Value; });
            if (entry <= VanillaReturn)
                throw new InvalidDataException($"[bulk-upgrade] progress delegate still on vanilla entry ({entry})");
        }

        // Append-only: the vanilla anchors must still sit at their original offsets.
        if (!starts.Contains(ChainEntry) || !starts.Contains(PostUpgrade) || !starts.Contains(VanillaReturn))
            throw new InvalidDataException("[bulk-upgrade] vanilla anchor offsets moved — not an append-only patch");

        int bands = 0;
        foreach (var s in list)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_IntConst ic && (ic.Value is 34 or 67 or 112 or 223 or 333)) bands++;
            });
        if (bands < 5)
            throw new InvalidDataException($"[bulk-upgrade] tier ladder constants missing ({bands}/5)");

        AssertNoPlaceholders(fn);
        PartyFiveKit.VerifyJumps(asset, fn);
        Console.WriteLine("[bulk-upgrade] Verify() passed: append-only loop, push redirected, jumps valid");
    }

    // ────────────────────────── tap gate ──────────────────────────

    private sealed class TapGate
    {
        public required List<KismetExpression> Block;
        public required EX_JumpIfNot SkipToTap;
        public required KismetExpression TapStart;
    }

    /// <summary>
    /// Press-tick gate: progress ticks above zero belong to a hold and fall
    /// through to the vanilla radial update; a zero tick (button pressed) with a
    /// selected weapon runs exactly one vanilla upgrade by presetting the bulk
    /// counter at its limit, so the appended loop exits after that one level.
    /// </summary>
    private static TapGate BuildTapGate(UAsset asset, List<KismetExpression> stmts,
        LocalRef counter, KismetExpression letIntTpl, int loopLevels, uint chainOff, uint progressOff)
    {
        var greater = PartyFiveKit.CloneEngineFunction(asset, "KismetMathLibrary", "Less_IntInt", "Greater_DoubleDouble");
        var isValid = PartyFiveKit.CloneEngineFunction(asset, "KismetSystemLibrary", "Delay", "IsValid");
        var progress = CloneLocal(stmts, "K2Node_ComponentBoundEvent_Progress");
        var item = CloneInstance(stmts, "CurrentWeaponItem");

        var block = new List<KismetExpression>();
        var hold = new EX_LetBool
        {
            VariableExpression = CloneBoolLocal(stmts).Node,
            AssignmentExpression = new EX_CallMath
            {
                StackNode = greater,
                Parameters = new KismetExpression[] { progress.Node, new EX_DoubleConst { Value = 0.0 } },
            },
        };
        var skipToTap = new EX_JumpIfNot { CodeOffset = 0xFFFFFFFE, BooleanExpression = CloneBoolLocal(stmts).Node };
        block.Add(hold);
        block.Add(skipToTap);
        block.Add(new EX_Jump { CodeOffset = progressOff });

        // Debounce: press and release both report zero progress, and both run the
        // same call site, so RetriggerableDelay collapses the pair into one
        // continuation ~150 ms after the last tick. A hold's nonzero ticks skip
        // this block entirely, so only its press contributes one level.
        var debounce = CloneRetriggerableDelay(asset, stmts);
        var tapStart = debounce;
        block.Add(debounce);

        var valid = new EX_LetBool
        {
            VariableExpression = CloneBoolLocal(stmts).Node,
            AssignmentExpression = new EX_CallMath
            {
                StackNode = isValid,
                Parameters = new KismetExpression[] { item },
            },
        };
        block.Add(valid);
        block.Add(new EX_JumpIfNot { CodeOffset = progressOff, BooleanExpression = CloneBoolLocal(stmts).Node });
        block.Add(CloneLetInt(letIntTpl, counter, loopLevels - 1));
        block.Add(new EX_Jump { CodeOffset = chainOff });

        return new TapGate { Block = block, SkipToTap = skipToTap, TapStart = tapStart };
    }

    /// <summary>Clones the widget's own Delay call into a retriggerable 150 ms
    /// delay with its own latent-action id (so it cannot cancel the vanilla
    /// post-upgrade delay, and repeated ticks reset it instead of stacking).</summary>
    private static EX_CallMath CloneRetriggerableDelay(UAsset asset, List<KismetExpression> stmts)
    {
        EX_CallMath? tpl = null;
        foreach (var s in stmts)
            if (s is EX_CallMath cm
                && cm.StackNode.IsImport()
                && Math.Abs(cm.StackNode.Index) - 1 < asset.Imports.Count
                && asset.Imports[Math.Abs(cm.StackNode.Index) - 1].ObjectName.ToString() == "Delay"
                && cm.Parameters is { Length: 3 })
            {
                tpl = cm;
                break;
            }
        if (tpl is null) throw new InvalidDataException("[bulk-upgrade] Delay call template not found");

        var clone = (EX_CallMath)MayhemFixKit.CloneKismet(tpl);
        clone.StackNode = PartyFiveKit.CloneEngineFunction(asset, "KismetSystemLibrary", "Delay", "RetriggerableDelay");
        clone.Parameters[1] = new EX_FloatConst { Value = 0.15f };
        MayhemFixKit.Walk(new[] { clone.Parameters[2] }, e =>
        {
            if (e is EX_IntConst ic) ic.Value = TapLatentId;
        });
        return clone;
    }

    private const int TapLatentId = 0x0B1D0B1D;

    /// <summary>Points the button's progress delegate at the appended tap gate.</summary>
    private static void RetargetProgressDelegate(UAsset asset, uint entry)
    {
        var del = asset.Exports.OfType<FunctionExport>().FirstOrDefault(f =>
            f.ObjectName.ToString().EndsWith("_1_OnButtonBaseActionProgress__DelegateSignature", StringComparison.Ordinal))
            ?? throw new InvalidDataException("[bulk-upgrade] progress delegate not found");
        var consts = new List<EX_IntConst>();
        foreach (var s in del.ScriptBytecode ?? Array.Empty<KismetExpression>())
            MayhemFixKit.Walk(new[] { s }, e => { if (e is EX_IntConst ic) consts.Add(ic); });
        if (consts.Count != 1)
            throw new InvalidDataException($"[bulk-upgrade] progress delegate has {consts.Count} int consts, want 1");
        consts[0].Value = (int)entry;
    }

    private static LocalRef CloneBoolLocal(List<KismetExpression> stmts) => CloneLocal(stmts, BoolLocal);

    private static EX_InstanceVariable CloneInstance(List<KismetExpression> stmts, string name)
    {
        EX_InstanceVariable? hit = null;
        foreach (var s in stmts)
        {
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (hit is null && e is EX_InstanceVariable iv && PartyFiveKit.FieldName(iv.Variable) == name)
                    hit = iv;
            });
            if (hit is not null) break;
        }
        if (hit is null)
            throw new InvalidDataException($"[bulk-upgrade] instance var '{name}' not found — shape changed");
        return (EX_InstanceVariable)MayhemFixKit.CloneKismet(hit);
    }

    // ────────────────────────── builders ──────────────────────────

    private enum Fix { AfterReset, Continuation }

    private static EX_JumpIfNot Jif(KismetExpression cond, Fix target)
    {
        var j = new EX_JumpIfNot { CodeOffset = 0xFFFFFFFE, BooleanExpression = cond };
        _pendingFixups.Add((j, target));
        return j;
    }

    private static readonly List<(KismetExpression Node, Fix Target)> _pendingFixups = new();

    private static void SetJump(KismetExpression node, uint offset)
    {
        if (node is EX_Jump j) j.CodeOffset = offset;
        else if (node is EX_JumpIfNot jn) jn.CodeOffset = offset;
        else throw new InvalidDataException("[bulk-upgrade] bad jump node");
    }

    private sealed class LocalRef
    {
        public required EX_LocalVariable Node;
    }

    private static LocalRef CloneLocal(List<KismetExpression> stmts, string name)
    {
        EX_LocalVariable? hit = null;
        foreach (var s in stmts)
        {
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (hit is null && e is EX_LocalVariable lv && PartyFiveKit.FieldName(lv.Variable) == name)
                    hit = lv;
            });
            if (hit is not null) break;
        }
        if (hit is null) throw new InvalidDataException($"[bulk-upgrade] local '{name}' not found — shape changed");
        return new LocalRef { Node = (EX_LocalVariable)MayhemFixKit.CloneKismet(hit) };
    }

    private static void SetCallParam(EX_Let let, int index, LocalRef local)
    {
        if (let.Expression is not EX_CallMath cm || cm.Parameters is not { Length: > 0 } || index >= cm.Parameters.Length)
            throw new InvalidDataException("[bulk-upgrade] Add template shape changed");
        cm.Parameters[index] = local.Node;
    }

    private static EX_LetBool CloneLess(KismetExpression lessTpl, object left, object right)
    {
        var clone = (EX_LetBool)MayhemFixKit.CloneKismet(lessTpl);
        if (clone.AssignmentExpression is not EX_CallMath cm || cm.Parameters is not { Length: 2 })
            throw new InvalidDataException("[bulk-upgrade] Less template shape changed");
        cm.Parameters[0] = ToExpr(left);
        cm.Parameters[1] = ToExpr(right);
        return clone;
    }

    private static KismetExpression ToExpr(object o) => o switch
    {
        LocalRef l => l.Node,
        EX_IntConst c => c,
        _ => throw new InvalidDataException("[bulk-upgrade] bad comparison operand"),
    };

    private static KismetExpression BoolOf(EX_LetBool lb)
    {
        if (lb.VariableExpression is not EX_LocalVariable lv)
            throw new InvalidDataException("[bulk-upgrade] Less template destination changed");
        return MayhemFixKit.CloneKismet(lv);
    }

    /// <summary>counter = AddRet, reusing the vanilla loop-counter assign shape.</summary>
    private static EX_Let CloneAssign(KismetExpression letAssignTpl, LocalRef dest)
    {
        var clone = (EX_Let)MayhemFixKit.CloneKismet(letAssignTpl);
        clone.Variable = dest.Node;
        clone.Value = dest.Node.Variable;
        return clone;
    }

    private static EX_Let CloneLetInt(KismetExpression letIntTpl, LocalRef dest, int value)
    {
        var clone = (EX_Let)MayhemFixKit.CloneKismet(letIntTpl);
        clone.Variable = dest.Node;
        clone.Value = dest.Node.Variable;
        if (clone.Expression is EX_IntConst ic) ic.Value = value;
        else throw new InvalidDataException("[bulk-upgrade] int-assign template shape changed");
        return clone;
    }

    // ────────────────────────── anchors ──────────────────────────

    private static EX_PushExecutionFlow FindClickPush(UAsset asset, List<KismetExpression> stmts)
    {
        var pushes = new List<(EX_PushExecutionFlow Push, uint At)>();
        var starts = PartyFiveKit.StatementStarts(asset, stmts);
        for (int i = 0; i < stmts.Count; i++)
            if (stmts[i] is EX_PushExecutionFlow p) pushes.Add((p, starts[i]));
        var hit = pushes.Where(p => p.At == ClickPush).ToList();
        if (hit.Count != 1)
            throw new InvalidDataException($"[bulk-upgrade] click push at {ClickPush} not found ({hit.Count})");
        return hit[0].Push;
    }

    private static uint AssertStmtAt(UAsset asset, List<KismetExpression> stmts, uint offset, string what)
    {
        var starts = PartyFiveKit.StatementStarts(asset, stmts);
        if (!starts.Contains(offset))
            throw new InvalidDataException($"[bulk-upgrade] {what} (offset {offset}) not found — shape changed");
        return offset;
    }

    private static KismetExpression FindAddInt(UAsset asset, List<KismetExpression> stmts)
    {
        foreach (var s in stmts)
            if (s is EX_Let let && let.Expression is EX_CallMath cm && MathName(asset, cm.StackNode) == "Add_IntInt")
                return s;
        throw new InvalidDataException("[bulk-upgrade] Add_IntInt template not found");
    }

    private static KismetExpression FindLess(UAsset asset, List<KismetExpression> stmts)
    {
        foreach (var s in stmts)
            if (s is EX_LetBool lb && lb.AssignmentExpression is EX_CallMath cm && MathName(asset, cm.StackNode) == "Less_IntInt")
                return s;
        throw new InvalidDataException("[bulk-upgrade] Less_IntInt template not found");
    }

    private static KismetExpression FindLetIntConst(List<KismetExpression> stmts)
    {
        foreach (var s in stmts)
            if (s is EX_Let let && let.Expression is EX_IntConst) return s;
        throw new InvalidDataException("[bulk-upgrade] int-const assign template not found");
    }

    /// <summary>Vanilla <c>Let Temp_int_Loop_Counter = AddRet</c> shape.</summary>
    private static KismetExpression FindLetAssign(List<KismetExpression> stmts)
    {
        foreach (var s in stmts)
            if (s is EX_Let let
                && let.Expression is EX_LocalVariable lv
                && PartyFiveKit.FieldName(lv.Variable) == AddRetLocal)
                return s;
        throw new InvalidDataException("[bulk-upgrade] int assign template not found");
    }

    private static KismetExpression FindLevelRead(List<KismetExpression> stmts)
    {
        foreach (var s in stmts)
            if (s is EX_Let let
                && PartyFiveKit.FieldName(((EX_LocalVariable?)let.Variable)?.Variable) == LevelLocal
                && let.Expression is EX_Context)
                return s;
        throw new InvalidDataException("[bulk-upgrade] level-read template not found");
    }

    private static KismetExpression FindContext(List<KismetExpression> stmts, string virt)
    {
        foreach (var s in stmts)
            if (s is EX_Context ctx && ContainsVirt(ctx, virt)) return s;
        throw new InvalidDataException($"[bulk-upgrade] {virt} context template not found");
    }

    private static bool ContainsVirt(KismetExpression s, string virt)
    {
        bool hit = false;
        MayhemFixKit.Walk(new[] { s }, e =>
        {
            if (hit) return;
            if (e is EX_LocalVirtualFunction lv && lv.VirtualFunctionName.ToString() == virt) hit = true;
            if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == virt) hit = true;
        });
        return hit;
    }

    private static string MathName(UAsset asset, FPackageIndex idx) =>
        idx.IsImport() && Math.Abs(idx.Index) - 1 < asset.Imports.Count
            ? asset.Imports[Math.Abs(idx.Index) - 1].ObjectName.ToString()
            : "?";

    private static void AssertNoPlaceholders(FunctionExport fn)
    {
        foreach (var s in fn.ScriptBytecode!)
        {
            bool bad = false;
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (e is EX_Jump j && j.CodeOffset == 0xFFFFFFFE) bad = true;
                if (e is EX_JumpIfNot jn && jn.CodeOffset == 0xFFFFFFFE) bad = true;
            });
            if (bad) throw new InvalidDataException("[bulk-upgrade] unresolved jump placeholder");
        }
    }

    private static UAsset Open(string path)
    {
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
    }
}

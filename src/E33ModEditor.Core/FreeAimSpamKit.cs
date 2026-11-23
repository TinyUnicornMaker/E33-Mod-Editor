using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Makes repeated Free Aim shots in the same turn increasingly inefficient.
///
/// The regular Free Aim action is implemented in
/// <c>BP_jRPG_Character_Battle_Base.ExecuteUbergraph</c>. That block builds a
/// damage builder with the "FreeAim Damage" source, then calls
/// <c>DealDamages</c>. The shooter component exposes <c>ShotsFired</c>, which
/// is reset to 0 when Free Aim starts and incremented for every shot, so it is
/// exactly the number of Free Aim shots taken in the current turn.
///
/// We insert one <c>AddFinalDamageMultiplier</c> on that builder only:
///
/// <code>
/// multiplier = 0.97 ^ ShotsFired
/// </code>
///
/// It is multiplicative per shot (shot 1 = 0.97x, shot 2 = 0.9409x, …), and
/// it never touches skills, base attacks, counters, Burn or Lumina damage.
/// </summary>
public static class FreeAimSpamKit
{
    public const string BattleBase = "BP_jRPG_Character_Battle_Base";
    public const string ShooterComponent = "BP_FreeAimShooterBattleComponent";
    public const string BuilderLocal = "CallFunc_MakeDamageBuilder_ReturnValue_2";
    public const string FreeAimSource = "FreeAim Damage";
    public const double PerShotMultiplier = 0.97;
    public const double PerShotReduction = 1.0 - PerShotMultiplier;
    public const int PenaltyShotCap = 100;

    public static int Apply(string modDir)
    {
        var path = Stage(modDir);
        var asset = Open(path);
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "ExecuteUbergraph_" + BattleBase)
            ?? throw new InvalidDataException($"[freeaim] ExecuteUbergraph_{BattleBase} not found");

        if (FindPenalty(asset, fn.ScriptBytecode!) >= 0)
        {
            if (!Verify(path))
                throw new InvalidDataException("[freeaim] existing penalty block does not verify");
            return 0;
        }

        int n = Patch(asset, fn);
        if (n > 0) asset.Write(path);
        if (!Verify(path))
            throw new InvalidDataException("[freeaim] verification failed after writing " + BattleBase);
        return n;
    }

    public static bool Verify(string uassetPath)
    {
        var asset = Open(uassetPath);
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "ExecuteUbergraph_" + BattleBase);
        if (fn?.ScriptBytecode is null) return false;

        var stmts = fn.ScriptBytecode.ToList();
        int deal = FindFreeAimDeal(asset, stmts);
        int add = FindPenalty(asset, stmts);
        if (deal < 0 || add < 0 || add >= deal)
        {
            Console.WriteLine($"[freeaim] verify: add={add} deal={deal}");
            return false;
        }

        int selectCalls = 0, eqCalls = 0, shotsFired = 0;
        bool rate = false, zeroGuard = false, floor = false;
        MayhemFixKit.Walk(new[] { stmts[add] }, e =>
        {
            if (e is EX_CallMath cm)
            {
                var name = ImportName(asset, cm.StackNode);
                if (name == "SelectFloat") selectCalls++;
                if (name == "EqualEqual_IntInt") eqCalls++;
            }
            if (e is EX_DoubleConst dc)
            {
                if (Math.Abs(dc.Value - PerShotMultiplier) < 1e-9) rate = true;
                if (Math.Abs(dc.Value - 1.0) < 1e-9) zeroGuard = true;
                if (Math.Abs(dc.Value - Math.Pow(PerShotMultiplier, PenaltyShotCap)) < 1e-9)
                    floor = true;
            }
            if (e is EX_InstanceVariable iv
                && PartyFiveKit.FieldName(iv.Variable) == "ShotsFired")
                shotsFired++;
        });
        // 100 shot branches + the zero-shot guard.
        bool ok = selectCalls >= 101 && eqCalls >= 101
                  && shotsFired >= 101 && rate && zeroGuard && floor;
        Console.WriteLine($"[freeaim] verify {BattleBase}: add={add} deal={deal} " +
                          $"selects={selectCalls} eq={eqCalls} shots={shotsFired} " +
                          $"rate={rate} zero={zeroGuard} floor={floor} -> {ok}");
        return ok;
    }

    // ─────────────────────────── bytecode patch ───────────────────────────

    private static int Patch(UAsset asset, FunctionExport fn)
    {
        var stmts = fn.ScriptBytecode!.ToList();
        int dealIdx = FindFreeAimDeal(asset, stmts);
        if (dealIdx < 0)
            throw new InvalidDataException("[freeaim] regular Free Aim DealDamages not found");
        if (FindPenalty(asset, stmts) >= 0)
            return 0;

        var builder = FindLocal(fn, BuilderLocal)
            ?? throw new InvalidDataException($"[freeaim] builder local {BuilderLocal} not found");
        var component = FindInstance(asset, ShooterComponent)
            ?? throw new InvalidDataException($"[freeaim] component {ShooterComponent} not found");
        var shots = FindInstance(asset, "ShotsLeft")
            ?? throw new InvalidDataException("[freeaim] ShotsLeft property not found");

        var shotsPtr = RenamePtr(asset, ClonePtr(shots.Variable), "ShotsFired");
        var shotsRead = Read(component, shotsPtr);
        // Nested SelectFloat avoids Pow/Switch jump plumbing while keeping the
        // exact multiplicative values. The table covers shots 1..100; 100+ uses
        // 0.97^100.
        var eq = LuneSkillsKit.EnsureKismetMathFn(asset, "EqualEqual_IntInt");
        var select = LuneSkillsKit.EnsureKismetMathFn(asset, "SelectFloat");
        KismetExpression amount = new EX_DoubleConst { Value = Math.Pow(PerShotMultiplier, PenaltyShotCap) };
        for (int n = PenaltyShotCap; n >= 1; n--)
            amount = new EX_CallMath
            {
                StackNode = select,
                Parameters = new KismetExpression[]
                {
                    new EX_DoubleConst { Value = Math.Pow(PerShotMultiplier, n) },
                    amount,
                    new EX_CallMath
                    {
                        StackNode = eq,
                        Parameters = new KismetExpression[]
                        {
                            (KismetExpression)MayhemFixKit.CloneKismet(shotsRead),
                            new EX_IntConst { Value = n },
                        },
                    },
                },
            };
        // ShotsFired is 1-based at damage time; a zero value must not fall
        // through to the 100-shot floor if an event ordering changes.
        amount = new EX_CallMath
        {
            StackNode = select,
            Parameters = new KismetExpression[]
            {
                new EX_DoubleConst { Value = 1.0 },
                amount,
                new EX_CallMath
                {
                    StackNode = eq,
                    Parameters = new KismetExpression[]
                    {
                        (KismetExpression)MayhemFixKit.CloneKismet(shotsRead),
                        new EX_IntConst { Value = 0 },
                    },
                },
            },
        };

        var add = new EX_Context
        {
            ObjectExpression = (KismetExpression)MayhemFixKit.CloneKismet(builder),
            ContextExpression = new EX_LocalVirtualFunction
            {
                VirtualFunctionName = FName.FromString(asset, "AddFinalDamageMultiplier"),
                Parameters = new KismetExpression[]
                {
                    amount,
                    new EX_StringConst { Value = "FreeAim repeat penalty" },
                },
            },
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
        };

        var oldStarts = PartyFiveKit.StatementStarts(asset, stmts);
        uint insertOffset = 0;
        for (int i = 0; i < dealIdx; i++) insertOffset += (uint)stmts[i].GetSize(asset);
        uint insertedSize = (uint)add.GetSize(asset);
        stmts.Insert(dealIdx, add);
        fn.ScriptBytecode = stmts.ToArray();
        PartyFiveKit.RemapOffsetsForInsertion(asset, fn, oldStarts, dealIdx,
            insertedSize, 1);
        // Event stubs hold absolute ExecuteUbergraph entry offsets. This insert
        // lands before several of them; without this every later event starts
        // in the middle of an expression and crashes the VM.
        PartyFiveKit.ShiftUbergraphEntryOffsets(asset, fn, insertOffset, insertedSize);
        MayhemFixKit.RecalcContextSkips(asset, fn);
        PartyFiveKit.VerifyJumps(asset, fn);
        Console.WriteLine($"[freeaim] repeat penalty installed before DealDamages ({PerShotReduction:P0}/shot)");
        return 1;
    }

    /// <summary>Index of the regular Free Aim DealDamages statement.</summary>
    private static int FindFreeAimDeal(UAsset asset, IReadOnlyList<KismetExpression> stmts)
    {
        int source = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (source < 0 && e is EX_StringConst s && s.Value == FreeAimSource)
                    source = i;
            });
            if (source >= 0) break;
        }
        if (source < 0) return -1;

        for (int i = source + 1; i < stmts.Count; i++)
        {
            int hit = -1;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (hit < 0
                    && e is EX_Context ctx
                    && ctx.ObjectExpression is EX_LocalVariable lv
                    && PartyFiveKit.FieldName(lv.Variable) == BuilderLocal
                    && ctx.ContextExpression is EX_LocalVirtualFunction vf
                    && vf.VirtualFunctionName.ToString() == "DealDamages")
                    hit = i;
            });
            if (hit >= 0) return hit;
        }
        return -1;
    }

    /// <summary>Index of our AddFinalDamageMultiplier(0.97^shots) statement.</summary>
    private static int FindPenalty(UAsset asset, IReadOnlyList<KismetExpression> stmts)
    {
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hit = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (hit) return;
                if (e is EX_Context ctx
                    && ctx.ObjectExpression is EX_LocalVariable lv
                    && PartyFiveKit.FieldName(lv.Variable) == BuilderLocal
                    && ctx.ContextExpression is EX_LocalVirtualFunction vf
                    && vf.VirtualFunctionName.ToString() == "AddFinalDamageMultiplier")
                {
                    MayhemFixKit.Walk(new[] { stmts[i] }, x =>
                    {
                        if (x is EX_CallMath cm
                            && ImportName(asset, cm.StackNode) == "SelectFloat")
                            hit = true;
                    });
                }
            });
            if (hit) return i;
        }
        return -1;
    }

    private static KismetExpression Read(EX_InstanceVariable component, KismetPropertyPointer ptr) =>
        new EX_Context
        {
            ObjectExpression = (KismetExpression)MayhemFixKit.CloneKismet(component),
            RValuePointer = ptr,
            ContextExpression = new EX_InstanceVariable { Variable = ptr },
        };

    private static KismetPropertyPointer ClonePtr(KismetPropertyPointer? p) =>
        new(new FFieldPath((FName[])(p?.New?.Path ?? Array.Empty<FName>()).Clone(),
            p?.New?.ResolvedOwner ?? FPackageIndex.FromRawIndex(0)));

    private static KismetPropertyPointer RenamePtr(UAsset asset, KismetPropertyPointer p, string name)
    {
        var path = p.New?.Path ?? Array.Empty<FName>();
        var renamed = path.Length == 0
            ? new[] { FName.FromString(asset, name) }
            : path.Take(path.Length - 1).Append(FName.FromString(asset, name)).ToArray();
        return new KismetPropertyPointer(new FFieldPath(renamed,
            p.New?.ResolvedOwner ?? FPackageIndex.FromRawIndex(0)));
    }

    private static EX_InstanceVariable? FindInstance(UAsset asset, string field)
    {
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
            foreach (var st in fn.ScriptBytecode ?? Array.Empty<KismetExpression>())
            {
                EX_InstanceVariable? hit = null;
                MayhemFixKit.Walk(new[] { st }, e =>
                {
                    if (hit is null && e is EX_InstanceVariable iv
                        && PartyFiveKit.FieldName(iv.Variable) == field)
                        hit = iv;
                });
                if (hit is not null) return hit;
            }
        return null;
    }

    private static EX_LocalVariable? FindLocal(FunctionExport fn, string name)
    {
        EX_LocalVariable? hit = null;
        foreach (var st in fn.ScriptBytecode ?? Array.Empty<KismetExpression>())
        {
            MayhemFixKit.Walk(new[] { st }, e =>
            {
                if (hit is null && e is EX_LocalVariable lv
                    && PartyFiveKit.FieldName(lv.Variable) == name)
                    hit = lv;
            });
            if (hit is not null) break;
        }
        return hit;
    }

    private static string? ImportName(UAsset asset, FPackageIndex? idx)
    {
        if (idx is null || !idx.IsImport()) return null;
        var i = Math.Abs(idx.Index) - 1;
        return i >= 0 && i < asset.Imports.Count ? asset.Imports[i].ObjectName.ToString() : null;
    }

    private static string Stage(string modDir)
    {
        var existing = FileCopy.FindUnder(modDir, BattleBase + ".uasset");
        if (existing is not null) return existing;

        var src = FindVanilla();
        if (src is null)
        {
            var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", BattleBase);
            Directory.CreateDirectory(dir);
            using (Packer.DonorExtract.SuspendMods())
                new Retoc().ToLegacy(dir, BattleBase);
            src = FindVanilla()
                ?? throw new FileNotFoundException(BattleBase + ".uasset not found after extraction");
        }

        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) throw new InvalidDataException(BattleBase + " is not under Sandfall/");
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    private static string? FindVanilla()
    {
        var tables = Path.Combine(EditorPaths.RepoRoot, "work", "tables");
        foreach (var root in new[]
                 {
                     Path.Combine(tables, BattleBase),
                     Path.Combine(tables, "party_five"),
                 })
        {
            if (!Directory.Exists(root)) continue;
            var hit = Directory.EnumerateFiles(root, BattleBase + ".uasset", SearchOption.AllDirectories)
                .FirstOrDefault(p => !p.Contains(Path.DirectorySeparatorChar + "__rt" +
                                                Path.DirectorySeparatorChar, StringComparison.Ordinal));
            if (hit is not null) return hit;
        }

        return Directory.Exists(tables)
            ? Directory.EnumerateFiles(tables, BattleBase + ".uasset", SearchOption.AllDirectories)
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
}

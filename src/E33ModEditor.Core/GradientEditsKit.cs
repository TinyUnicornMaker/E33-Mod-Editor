using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Gradient-move final edits. Lune: Quake (Tremor), Tree of Life, Meteor
/// (SkyBreak). Verso: Sabotage, Striker, Angel's Eyes.
///
/// Gradient attacks are SkillType GradientAttack and charge the gradient gauge;
/// their charge cost is the DA "APCost" field. Damage/break/heal values live in
/// DT_Balancing_MainCharacterSkills rows, generated stains in the DA's
/// StainsMechanic subobject.
///
/// The skill-graph node cost is deliberately NOT written here — the tree layout
/// and its costs are owned by the Graph Editor module (SkillGraphKit mirrors DA
/// AP onto graph nodes when a shape packs).
/// </summary>
public static class GradientEditsKit
{
    private const string BalTable = "DT_Balancing_MainCharacterSkills";
    private const string StTable = "ST_MainCharacters_Skills";

    // ── description markup (same ids the other kits use) ──
    private const string Physical = "<keyword id=\"Element_Physical\">Physical</>";
    private const string BreakKw = "<keyword id=\"Break\">Break</>";
    private const string MarkKw = "<keyword id=\"StatusEffect_Mark\">Mark</>";
    private const string PerfectionKw = "<keyword id=\"Perfection\">Perfection</>";

    /// <summary>Gold per-hit multiplier, matching SkillDescriptionsKit's format.</summary>
    private static string Gold(double v) => $"<keyword id=\"DamageMultiplier\">{v:0.00}x</>";

    // ── Verso gradient totals: damage is split across the move's hit count ──
    private const double SabotageTotal = 10.0, StrikerTotal = 30.0, AngelsEyesTotal = 70.0;
    private const int SabotageHits = 1, StrikerHits = 1, AngelsEyesHits = 8;

    private sealed record MoveSpec(
        string Stem, int Cost, string Element, int Stains,
        IReadOnlyDictionary<string, double> Rows);

    private static readonly MoveSpec[] Moves =
    {
        // Tremor -> Quake: 3 gradient charges, 4 Earth stains, x30 damage, x3 break.
        // Shield-strip effect is in the Tremor script and is untouched.
        new("DA_Skill_Lune_Tremor", 3, "Earth", 4,
            new Dictionary<string, double>
            {
                ["Tremor_BaseDamage"] = 30.0,
                ["Tremor_BreakBarMultiplier"] = 3.0,
            }),
        // Tree of Life: 5 gradient charges, 4 Light stains, heal 50% (revive added in script).
        new("DA_Skill_Lune_TreeOfLife", 5, "Light", 4,
            new Dictionary<string, double>
            {
                ["TreeOfLife_HealPercent"] = 0.5,
            }),
        // Sky Break -> Meteor: 7 gradient charges, 4 Fire stains, x70 damage, x5 break,
        // always-Fire element (script splice).
        new("DA_Skill_Lune_SkyBreak", 7, "Fire", 4,
            new Dictionary<string, double>
            {
                ["SkyBreak_BaseDamage"] = 70.0,
                ["SkyBreak_BreakBarMultiplier"] = 5.0,
            }),
        // ── Verso (gradient attacks; no stain generation, break rows untouched) ──
        // Sabotage: 1 gradient charge (kept), x10 total over 1 hit, Physical AoE.
        new("DA_Skill_Verso_NEW_Sabotage", 1, "Physical", 0,
            new Dictionary<string, double>
            {
                ["Sabotage_BaseDamage"] = SabotageTotal / SabotageHits,
            }),
        // Striker: cost 3, x30 total over 1 hit, Physical single target.
        new("DA_Skill_Verso_Striker", 3, "Physical", 0,
            new Dictionary<string, double>
            {
                ["Striker_BaseDamage"] = StrikerTotal / StrikerHits,
            }),
        // Angel's Eyes: 7 gradient charges, x70 total over 8 hits, Physical.
        new("DA_Skill_Verso_NEW_AngelsEyes", 7, "Physical", 0,
            new Dictionary<string, double>
            {
                ["AngelsEyes_BaseDamage"] = AngelsEyesTotal / AngelsEyesHits,
            }),
    };

    public static int Apply(string modDir)
    {
        int n = 0;
        n += PatchDataAssets(modDir);
        n += PatchBalancingRows(modDir);
        n += PatchNamesAndDescriptions(modDir);
        n += PatchTreeOfLifeScript(modDir);
        n += PatchSkyBreakScript(modDir);
        return n;
    }

    private static int PatchDataAssets(string modDir)
    {
        int n = 0;
        foreach (var m in Moves)
        {
            var dest = EnsureModCopy(m.Stem, modDir);
            if (dest is null)
            {
                System.Console.WriteLine($"[{m.Stem}] DA not found; skipping");
                continue;
            }

            var map = DataTableEditor.Mappings;
            var da = map != null ? new UAsset(dest, EngineVersion.VER_UE5_4, map) : new UAsset(dest, EngineVersion.VER_UE5_4);
            foreach (var ex in da.Exports.OfType<NormalExport>())
            {
                foreach (var p in ex.Data)
                {
                    if (p.Name.ToString() == "APCost" && p is IntPropertyData ap)
                        ap.Value = m.Cost;
                    else if (p.Name.ToString() == "BaseElementalType" && p is EnumPropertyData bpe
                             && m.Stem == "DA_Skill_Lune_Tremor")
                        bpe.Value = FName.FromString(da, "EAttackType::" + StainsMechanicKit.Enumerator(m.Element));
                }
            }
            da.Write(dest);
            // Only the Lune gradients generate stains; the Verso moves write none.
            if (m.Stains > 0)
                StainsMechanicKit.WriteFile(dest, null, new Dictionary<string, int> { [m.Element] = m.Stains });
            System.Console.WriteLine(m.Stains > 0
                ? $"[{m.Stem}] APCost -> {m.Cost}, produce {m.Element}x{m.Stains}"
                : $"[{m.Stem}] APCost -> {m.Cost}");
            n++;
        }
        return n;
    }

    private static int PatchBalancingRows(string modDir)
    {
        var dest = EnsureModCopy(BalTable, modDir);
        if (dest is null) return 0;
        var rows = new JObject();
        foreach (var m in Moves)
            foreach (var (row, value) in m.Rows)
                rows[row] = new JObject { ["FloatValue"] = value };
        var ed = new DataTableEditor(dest);
        int n = ed.Apply(rows.ToString(Newtonsoft.Json.Formatting.None));
        ed.Save(dest);
        return n;
    }

    private static int PatchNamesAndDescriptions(string modDir)
    {
        var dest = EnsureModCopy(StTable, modDir);
        if (dest is null) return 0;
        var entries = new Dictionary<string, string>
        {
            ["Lune_Tremor_Name"] = "Quake",
            ["Lune_SkyBreak_Name"] = "Meteor",
        };
        // Tremor/Quake description (Long + Short).
        string quake =
            "Deals <keyword id=\"DamageMultiplier\">30.00x</> <keyword id=\"Element_Earth\">Earth</> damage to all enemies. 1 hit.\n" +
            "Removes all enemies' <keyword id=\"Shield\">Shields</>.";
        entries["Lune_Tremor_Description_Long"] = quake;
        entries["Lune_Tremor_Description_Short"] = quake;
        // Tree of Life description.
        string tree =
            "Revives all allies and <keyword id=\"Heal\">Heals</> all allies by 50% Health.\n" +
            "Cleanses all status effects.";
        entries["Lune_TreeOfLife_Description_Long"] = tree;
        entries["Lune_TreeOfLife_Description_Short"] = tree;
        // Sky Break/Meteor description.
        string meteor =
            "Deals <keyword id=\"DamageMultiplier\">70.00x</> <keyword id=\"Element_Fire\">Fire</> damage to all enemies. 1 hit.\n" +
            "Can <keyword id=\"Break\">Break</>.";
        entries["Lune_SkyBreak_Description_Long"] = meteor;
        entries["Lune_SkyBreak_Description_Short"] = meteor;

        // ── Verso gradients: gold per-hit multiplier + explicit hit count ──
        // Sabotage — 1 hit, Physical AoE, applies Mark.
        entries["Verso_Sabotage_Description_Long"] =
            $"Deals {Gold(SabotageTotal / SabotageHits)} {Physical} damage to all enemies. {SabotageHits} hit.\n" +
            $"Applies {MarkKw}.";
        entries["Verso_Sabotage_Description_Short"] =
            $"{Gold(SabotageTotal / SabotageHits)} {Physical} damage to all enemies. {SabotageHits} hit.\n" +
            $"Applies {MarkKw}.";
        // Striker — 1 hit, Physical single target, can Break.
        entries["Verso_Striker_Description_Long"] =
            $"Deals {Gold(StrikerTotal / StrikerHits)} single target {Physical} damage. {StrikerHits} hit.\n" +
            $"Can {BreakKw}.";
        entries["Verso_Striker_Description_Short"] =
            $"{Gold(StrikerTotal / StrikerHits)} {Physical} damage. {StrikerHits} hit.\n" +
            $"Can {BreakKw}.";
        // Angel's Eyes — 8 hits, Physical, Perfection per hit, Aureole revive.
        entries["Verso_AngelsEyes_Description_Long"] =
            $"Deals {Gold(AngelsEyesTotal / AngelsEyesHits)} single target {Physical} damage. {AngelsEyesHits} hits.\n" +
            $"Gain 1 additional {PerfectionKw} per hit.\n" +
            "Applies Aureole to Verso, reviving him if he dies.";
        entries["Verso_AngelsEyes_Description_Short"] =
            $"{Gold(AngelsEyesTotal / AngelsEyesHits)} {Physical} damage. {AngelsEyesHits} hits.\n" +
            $"Gain 1 additional {PerfectionKw} per hit.\n" +
            "Applies Aureole to Verso, reviving him if he dies.";

        DataTableEditor.AddStringTableEntries(dest, entries);
        return entries.Count;
    }

    private static string? EnsureModCopy(string stem, string modDir)
    {
        var existing = Directory.EnumerateFiles(modDir, stem + ".uasset", SearchOption.AllDirectories).FirstOrDefault();
        if (existing is not null) return existing;
        var src = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), stem + ".uasset")
            ?? FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables", "skills"), stem + ".uasset");
        if (src is null) return null;
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return null;
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    // ── Tree of Life: revive all allies first ─────────────────────────────────
    // Vanilla targets only GetAliveHeroes and heals. Change the target set to
    // GetAllHeroesAliveAndDead and insert a Resurrect(full HP) call per hero before
    // the heal, so downed allies come back at 100% and then the normal heal applies.
    private static int PatchTreeOfLifeScript(string modDir)
    {
        const string script = "BP_Battle_SkillScript_TreeOfLife";
        var dest = EnsureModCopy(script, modDir);
        if (dest is null)
        {
            System.Console.WriteLine($"[{script}] script not staged; revive skipped");
            return 0;
        }
        var map = DataTableEditor.Mappings;
        var bp = map != null ? new UAsset(dest, EngineVersion.VER_UE5_4, map) : new UAsset(dest, EngineVersion.VER_UE5_4);
        var fn = bp.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == "OnActionEffect");
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();

        // 1. Locate the target-set call GetAllAliveHeroes and the per-hero heal call.
        //    Get the "current hero" local (CallFunc_Array_Get_Item) used by the heal.
        int targetCallIdx = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            bool has = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (has) return;
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "GetAllAliveHeroes")
                    has = true;
            });
            if (has) { targetCallIdx = i; break; }
        }
        if (targetCallIdx < 0) return 0;

        int healIdx = -1;
        EX_LocalVariable? maxHpLocal = null;
        EX_InstanceVariable? statsInstVar = null;    // AC_jRPG_CharacterStats instance var
        for (int i = 0; i < stmts.Count; i++)
        {
            bool hasHeal = false;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_VirtualFunction vf && vf.VirtualFunctionName.ToString() == "RecoverHP") hasHeal = true;
            });
            if (!hasHeal) continue;
            // The heal uses the value computed by a GetMaxHP call that is its own
            // statement (the heal param is CallFunc_Multiply_DoubleDouble_ReturnValue =
            // MaxHP x HealPercent). Find the GetMaxHP statement and capture its local.
            for (int j = Math.Max(0, i - 3); j < i; j++)
            {
                bool hasMax = false;
                MayhemFixKit.Walk(new[] { stmts[j] }, e =>
                {
                    if (e is EX_VirtualFunction vf2 && vf2.VirtualFunctionName.ToString() == "GetMaxHP") hasMax = true;
                });
                if (!hasMax) continue;
                MayhemFixKit.Walk(new[] { stmts[j] }, e =>
                {
                    if (maxHpLocal is null && e is EX_LocalVariable lv && PtrName(lv.Variable) == "CallFunc_GetMaxHP_MaxHP")
                        maxHpLocal = lv;
                });
                break;
            }
            // Capture the stats instance var used in the heal statement.
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (statsInstVar is null && e is EX_InstanceVariable iv2 && PtrName(iv2.Variable) == "AC_jRPG_CharacterStats")
                    statsInstVar = iv2;
            });
            healIdx = i;
            break;
        }
        if (healIdx < 0 || maxHpLocal is null || statsInstVar is null) return 0;

        // 2. Swap the target-set call name GetAllAliveHeroes → GetAllHeroesAliveAndDead.
        //    The output local (CallFunc_GetAllAliveHeroes_Target_Heroes) and its references
        //    are unchanged — the replacement returns the same array shape.
        bool swappedName = false;
        MayhemFixKit.Walk(new[] { stmts[targetCallIdx] }, e =>
        {
            if (!swappedName && e is EX_VirtualFunction vf2 && vf2.VirtualFunctionName.ToString() == "GetAllAliveHeroes")
            {
                vf2.VirtualFunctionName = FName.FromString(bp, "GetAllHeroesAliveAndDead");
                swappedName = true;
            }
        });
        if (!swappedName) return 0;

        // 3. Build the Resurrect receiver from the heal's own context: the heal is called
        //    on Context(Context(<current hero>, AC_jRPG_CharacterStats)). Clone that whole
        //    object expression (the target's stats context) for the Resurrect receiver.
        EX_Context? healCallCtx = null;
        MayhemFixKit.Walk(new[] { stmts[healIdx] }, e =>
        {
            if (healCallCtx is null && e is EX_Context ctx && ctx.ContextExpression is EX_VirtualFunction vf3
                && vf3.VirtualFunctionName.ToString() == "RecoverHP")
                healCallCtx = ctx;
        });
        if (healCallCtx is null) return 0;
        var resurrectTarget = (KismetExpression)MayhemFixKit.CloneKismet(healCallCtx.ObjectExpression);

        // Instigator: Context(Context(CurrentCharacter, AC_jRPG_CharacterStats)).
        // Find CurrentCharacter instance var anywhere in the function.
        EX_InstanceVariable? selfIv = null;
        foreach (var s in stmts)
        {
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (selfIv is null && e is EX_InstanceVariable iv && PtrName(iv.Variable) == "CurrentCharacter")
                    selfIv = iv;
            });
            if (selfIv is not null) break;
        }
        if (selfIv is null) return 0;
        var instigator = new EX_Context
        {
            ObjectExpression = (EX_InstanceVariable)MayhemFixKit.CloneKismet(selfIv),
            ContextExpression = (EX_InstanceVariable)MayhemFixKit.CloneKismet(statsInstVar),
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
        };

        var resurrect = new EX_Context
        {
            ObjectExpression = resurrectTarget,
            ContextExpression = new EX_VirtualFunction
            {
                VirtualFunctionName = FName.FromString(bp, "Resurrect"),
                Parameters = new KismetExpression[]
                {
                    (EX_LocalVariable)MayhemFixKit.CloneKismet(maxHpLocal), // full HP revive
                    instigator,
                },
            },
            RValuePointer = new KismetPropertyPointer(new FFieldPath()),
        };

        // 4. Insert before the heal and fix offsets.
        uint insertOffset = 0;
        for (int i = 0; i < healIdx; i++) insertOffset += (uint)stmts[i].GetSize(bp);
        uint delta = (uint)resurrect.GetSize(bp);
        MayhemFixKit.ShiftAbsolute(bp, fn, insertOffset, delta, null);
        stmts.Insert(healIdx, resurrect);
        fn.ScriptBytecode = stmts.ToArray();
        MayhemFixKit.RecalcContextSkips(bp, fn);
        bp.Write(dest);
        System.Console.WriteLine($"[TreeOfLife] targets -> AliveAndDead, Resurrect(100%) inserted before heal");
        return 1;
    }

    // ── Sky Break → Meteor: force always-Fire ─────────────────────────────────
    // The uber graph picks the element from Lune's dominant stain into
    // CurrentMeteorElement. Replace that select with a direct Fire (ByteConst 2).
    private static int PatchSkyBreakScript(string modDir)
    {
        const string script = "BP_Battle_SkillScript_SkyBreak";
        var dest = EnsureModCopy(script, modDir);
        if (dest is null)
        {
            System.Console.WriteLine($"[{script}] script not staged; Fire splice skipped");
            return 0;
        }
        var map = DataTableEditor.Mappings;
        var bp = map != null ? new UAsset(dest, EngineVersion.VER_UE5_4, map) : new UAsset(dest, EngineVersion.VER_UE5_4);
        var fn = bp.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString().StartsWith("ExecuteUbergraph", StringComparison.Ordinal));
        if (fn?.ScriptBytecode is null) return 0;
        var stmts = fn.ScriptBytecode.ToList();

        int selIdx = -1;
        EX_Let? meteorLet = null;
        for (int i = 0; i < stmts.Count; i++)
        {
            EX_Let? let = null;
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (let is null && e is EX_Let l && l.Variable is EX_InstanceVariable iv
                    && PtrName(iv.Variable) == "CurrentMeteorElement")
                    let = l;
            });
            if (let is not null && let.Expression is EX_SwitchValue)
            {
                selIdx = i;
                meteorLet = let;
                break;
            }
        }
        if (selIdx < 0 || meteorLet is null) return 0;

        // Replace only the assigned value (the dominant-stain SwitchValue) with a direct
        // Fire byte. Keeping the original EX_Let preserves its property pointer (Value).
        // The statement shrinks (SwitchValue -> ByteConst), so every absolute offset past
        // the old statement's end must shift back by the shrink amount. Compute sizes from
        // clones so the shift runs against the unchanged (old-layout) bytecode first.
        uint stmtStart = 0;
        for (int i = 0; i < selIdx; i++) stmtStart += (uint)stmts[i].GetSize(bp);
        var oldStmtClone = (EX_Let)MayhemFixKit.CloneKismet(stmts[selIdx]);
        uint oldSize = (uint)oldStmtClone.GetSize(bp);
        uint oldEnd = stmtStart + oldSize;
        uint uberEnd = oldEnd;
        for (int i = selIdx + 1; i < stmts.Count; i++) uberEnd += (uint)stmts[i].GetSize(bp);
        var newLet = new EX_Let
        {
            Value = meteorLet.Value,
            Variable = meteorLet.Variable,
            Expression = new EX_ByteConst { Value = 2 }, // Fire
        };
        uint newSize = (uint)newLet.GetSize(bp);
        uint removed = oldSize > newSize ? oldSize - newSize : 0;
        if (removed > 0)
        {
            LuneSkillsKit.ShiftAbsoluteSigned(bp, fn, stmtStart + newSize, removed);
            // The UberGraph's own jumps shift above, but custom-event entry points live
            // in the sibling functions as EX_IntConst arguments to the ubergraph stub.
            // Without this the cast path still calls ExecuteUbergraph(601) after the
            // graph shrank to 557 bytes — the VM jumps past EndOfScript and crashes on
            // use. Shift any entry point in [oldEnd, uberEnd) by the same amount.
            ShiftExternalEntryPoints(bp, fn, oldEnd, uberEnd, removed);
        }
        stmts[selIdx] = newLet;
        fn.ScriptBytecode = stmts.ToArray();
        MayhemFixKit.RecalcContextSkips(bp, fn);
        bp.Write(dest);
        System.Console.WriteLine($"[SkyBreak] CurrentMeteorElement -> always Fire (stmt {stmtStart}: {oldSize} -> {newSize} bytes)");
        return 1;
    }

    /// <summary>
    /// Shifts custom-event entry-point IntConsts in the sibling functions of a
    /// Blueprint after an in-place statement shrink. Mirrors the external-offset
    /// pass in <see cref="LuneSkillsKit.RemoveVoiceFromDirector"/>.
    /// </summary>
    private static void ShiftExternalEntryPoints(
        UAsset bp, FunctionExport uber, uint oldEnd, uint uberEnd, uint removed)
    {
        foreach (var other in bp.Exports.OfType<FunctionExport>())
        {
            if (ReferenceEquals(other, uber) || other.ScriptBytecode is null) continue;
            MayhemFixKit.Walk(other.ScriptBytecode, e =>
            {
                if (e is not EX_IntConst ic) return;
                if (ic.Value < (int)oldEnd || ic.Value >= (int)uberEnd) return;
                ic.Value -= (int)removed;
            });
        }
    }

    private static string PtrName(KismetPropertyPointer p) =>
        p.New?.Path?.FirstOrDefault()?.ToString() ?? p.ToString() ?? "";
}

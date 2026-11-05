using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core.Loot;

/// <summary>
/// "More than three enemies" groundwork.
///
/// <para><b>Field capacity.</b> Vanilla <c>BP_BattleMap</c> ships four enemy
/// spot actors (slot indices 0–3) and four-entry left-to-right tables. The
/// spawn/placement code is otherwise array-driven, so increasing the usable
/// count is mostly a matter of teaching three assets about extra slots:</para>
/// <list type="bullet">
/// <item><c>FL_jRPG_CustomFunctionLibrary.GetEnemyIndexesLeftToRight</c> and
/// <c>GetEnemyIndexToBattleSpotsLeftToRight</c> — the fixed order tables used
/// by <c>DispatchEnemiesAlongSpline</c>. Extra entries let all spawned enemies
/// be laid out along the map's enemy spline.</item>
/// <item><c>BP_jRPG_Character_Battle_Base.GetBattleSpotIndexLeftToRight_</c> —
/// the enemy left-to-right sort table used by target grouping.</item>
/// <item><c>AC_jRPG_BattleManager.GetProperBattleSpawnPointTransform</c> — extra
/// enemy slot indices alias an existing vanilla anchor (cycling 0–3) and are
/// pushed into a second row by a per-slot offset. Maps only need their four
/// vanilla anchors; no new battle-map assets or spot actors are required.</item>
/// </list>
///
/// <para><b>Battle groups.</b> <see cref="RandomizeEncounters"/> rewrites the
/// roster map on eligible <c>DT_jRPG_Encounters</c> / <c>DT_WorldMap_Encounters</c>
/// rows, adding same-area/same-tier enemies up to
/// <see cref="RandomizerKit.Settings.MaxEnemiesPerBattle"/> and writing
/// contiguous spot keys 0..N-1. Bosses, alphas, tutorial/scripted fights and
/// known broken rows are left alone. This is deliberately a first pass: the
/// field-side capacity is the important half, and the randomizer only needs to
/// create valid rosters for it.</para>
/// </summary>
public static class BattleGroupKit
{
    /// <summary>Field-side hard cap this kit can patch up to (vanilla is 4).
    /// 32 is an experimental stress setting: the alias chain, order tables and
    /// offset expression are count-generic, but the single enemy spline, the
    /// four authored spot actors and the per-map cameras were never designed for
    /// that many, so expect cramped or bunched placement.</summary>
    public const int FieldCapacity = 32;

    public const string EncountersTable = "DT_jRPG_Encounters";
    public const string WorldEncountersTable = "DT_WorldMap_Encounters";
    public const string TowerEncountersTable = "DT_jRPG_Encounters_CleaTower";
    public const string WorldEnemyBase = "BP_jRPG_Enemy_World_Base_Seamless";
    /// <summary>Prefix for runtime variant rows (never generated from another variant).</summary>
    public const string VariantPrefix = "RAND_";

    private static readonly HashSet<string> BossArchetypes =
        new(StringComparer.OrdinalIgnoreCase) { "Boss", "Alpha", "SuperBoss", "Merchant", "Petank" };

    private static readonly string[] BlockedEncounterPrefixes =
    {
        "Quest_", "QUEST_", "Test_", "TEST_",
    };

    private static readonly string[] BlockedEncounterFragments =
    {
        "Tutorial", "Tuto", "Phase1", "Phase2",
    };

    /// <summary>Tutorial roster rows (including SM_FirstLancelier, whose name
    /// carries no "Tutorial" marker). Never grow these fights or draw them into
    /// a random group.</summary>
    private static readonly HashSet<string> TutorialEnemyRows =
        new(TutorialDisableKit.EnemyMap.Select(x => x.Tutorial), StringComparer.Ordinal);

    // ─────────────────────────── field capacity ───────────────────────────

    /// <summary>
    /// Patches the battle assets so spot indices 4..<paramref name="maxEnemies"/>-1
    /// spawn and sort correctly. Returns the number of assets edited.
    /// </summary>
    public static int ApplyCapacity(string modDir, int maxEnemies, Action<string>? log = null)
    {
        var capacity = Math.Clamp(maxEnemies, 4, FieldCapacity);
        // Vanilla already supports four spots; don't stage/overwrite battle
        // assets just for a 1–4 enemy group.
        if (capacity <= 4) return 0;
        int n = 0;
        n += PatchLeftToRightTables(modDir, capacity, log);
        n += PatchCharacterSpotOrder(modDir, capacity, log);
        n += PatchSpawnTransforms(modDir, capacity, log);
        n += PatchSplineDispatch(modDir, capacity, log);
        return n;
    }

    /// <summary>
    /// Beyond eight enemies the single enemy spline in
    /// <c>DispatchEnemiesAlongSpline</c> cannot hold the accumulated capsule
    /// widths: every distance past the spline end clamps, so the whole back half
    /// of the roster piles into one or two overlapping clumps. For groups larger
    /// than the spline can carry, skip the dispatch entirely — the enemies then
    /// stay on their staggered spawn slots (anchor + <see cref="EnemyOffsets"/>),
    /// which is the layout the capacity patch was designed around.
    /// </summary>
    private static int PatchSplineDispatch(string modDir, int capacity, Action<string>? log)
    {
        const int splineSafe = 8;
        if (capacity <= splineSafe) return 0;

        var path = FileCopy.FindUnder(modDir, "AC_jRPG_BattleManager.uasset")
            ?? CopyIntoMod("AC_jRPG_BattleManager", modDir);
        var asset = Open(path);
        var fn = Fn(asset, "DispatchEnemiesAlongSpline");
        const string countLocal = "BG_EnemyCount";
        if ((fn.LoadedProperties ?? Array.Empty<FProperty>())
            .Any(p => p.Name.ToString() == countLocal))
            return 0;

        // ── an int local of the same shape as the function's own counters ──
        var intTpl = (fn.LoadedProperties ?? Array.Empty<FProperty>())
            .OfType<FGenericProperty>()
            .FirstOrDefault(p => p.Name.ToString() == "CallFunc_Array_Length_ReturnValue_2")
            ?? throw new InvalidDataException("DispatchEnemiesAlongSpline: length local template not found");
        var props = (fn.LoadedProperties ?? Array.Empty<FProperty>()).ToList();
        var countProp = (FGenericProperty)ShallowClone(intTpl);
        countProp.Name = FName.FromString(asset, countLocal);
        props.Add(countProp);
        fn.LoadedProperties = props.ToArray();

        // ── template statements ──
        var stmts = fn.ScriptBytecode!.ToList();
        KismetExpression? getAllTpl = null;
        EX_Let? lengthTpl = null;
        EX_Return? returnTpl = null;
        foreach (var st in stmts)
        {
            // The compiler emits the GetAllEnemies call as a bare context whose
            // out parameter is a local; accept the Let-wrapped form too.
            if (getAllTpl is null)
            {
                if (st is EX_Context gc0
                    && gc0.ContextExpression is EX_LocalVirtualFunction gv0
                    && gv0.VirtualFunctionName.ToString() == "GetAllEnemies")
                    getAllTpl = gc0;
                else if (st is EX_Let gl
                    && gl.Expression is EX_Context gc
                    && gc.ContextExpression is EX_LocalVirtualFunction gv
                    && gv.VirtualFunctionName.ToString() == "GetAllEnemies")
                    getAllTpl = gl;
            }
            if (lengthTpl is null && st is EX_Let ll
                && ll.Expression is EX_Context lc
                && lc.ContextExpression is EX_FinalFunction lf
                && Name(asset, lf.StackNode) == "Array_Length")
                lengthTpl = ll;
            if (returnTpl is null && st is EX_Return r) returnTpl = r;
        }
        if (getAllTpl is null || lengthTpl is null || returnTpl is null)
            throw new InvalidDataException(
                "DispatchEnemiesAlongSpline: guard templates not found " +
                $"(getAll={getAllTpl is not null}, length={lengthTpl is not null}, " +
                $"return={returnTpl is not null})");

        var lessImp = PartyFiveKit.FindImportByName(asset, "Less_IntInt");
        KismetPropertyPointer PropPtr(string name) => new(new FFieldPath(
            new[] { FName.FromString(asset, name) },
            FPackageIndex.FromExport(asset.Exports.IndexOf(fn))));

        var getAll = MayhemFixKit.CloneKismet(getAllTpl);
        var length = (EX_Let)MayhemFixKit.CloneKismet(lengthTpl);
        length.Value = PropPtr(countLocal);
        length.Variable = new EX_LocalVariable { Variable = PropPtr(countLocal) };
        // The cloned Let measures the index-mapping array; we need the spawned
        // enemy array instead, so retarget its Array_Length operand.
        var enemiesLocal = (fn.LoadedProperties ?? Array.Empty<FProperty>())
            .FirstOrDefault(p => p.Name.ToString().StartsWith("CallFunc_GetAllEnemies_"))
            ?? throw new InvalidDataException("DispatchEnemiesAlongSpline: enemies local not found");
        if (length.Expression is not EX_Context lenCtx
            || lenCtx.ContextExpression is not EX_FinalFunction lenCall)
            throw new InvalidDataException("DispatchEnemiesAlongSpline: length call shape unexpected");
        lenCall.Parameters[0] = new EX_LocalVariable
        {
            Variable = PropPtr(enemiesLocal.Name.ToString()),
        };

        var guard = new EX_JumpIfNot
        {
            CodeOffset = 0,
            BooleanExpression = new EX_CallMath
            {
                StackNode = lessImp,
                Parameters = new KismetExpression[]
                {
                    new EX_IntConst { Value = splineSafe },
                    new EX_LocalVariable { Variable = PropPtr(countLocal) },
                },
            },
        };
        var earlyOut = (EX_Return)MayhemFixKit.CloneKismet(returnTpl);

        var block = new KismetExpression[] { getAll, length, guard, earlyOut };
        var oldStarts = PartyFiveKit.StatementStarts(asset, stmts);
        uint insertedSize = 0;
        foreach (var b in block) insertedSize += (uint)b.GetSize(asset);
        stmts.InsertRange(0, block);
        fn.ScriptBytecode = stmts.ToArray();
        PartyFiveKit.RemapOffsetsForInsertion(asset, fn, oldStarts, 0, insertedSize, block.Length);
        guard.CodeOffset = PartyFiveKit.StatementStarts(asset, fn.ScriptBytecode!)[block.Length];
        MayhemFixKit.RecalcContextSkips(asset, fn);
        PartyFiveKit.VerifyJumps(asset, fn);
        Write(asset, path);
        log?.Invoke($"[battle-groups] spline dispatch skipped for groups over {splineSafe} " +
                    "(staggered spawn rows kept)");
        return 1;
    }

    private static int PatchLeftToRightTables(string modDir, int capacity, Action<string>? log)
    {
        var path = CopyIntoMod("FL_jRPG_CustomFunctionLibrary", modDir);
        var asset = Open(path);
        int n = 0;

        var indexFn = Fn(asset, "GetEnemyIndexesLeftToRight");
        n += ReplaceIntArray(asset, indexFn, new[] { 2, 0, 1, 3 }, EnemyIndexOrder(capacity));

        var spotFn = Fn(asset, "GetEnemyIndexToBattleSpotsLeftToRight");
        n += ReplaceIntArray(asset, spotFn, new[] { 1, 2, 0, 3 }, EnemySpotOrder(capacity));

        Write(asset, path);
        log?.Invoke($"[battle-groups] enemy order tables -> {capacity} slot(s)");
        return n > 0 ? 1 : 0;
    }

    private static int PatchCharacterSpotOrder(string modDir, int capacity, Action<string>? log)
    {
        var path = CopyIntoMod("BP_jRPG_Character_Battle_Base", modDir);
        var asset = Open(path);
        var fn = Fn(asset, "GetBattleSpotIndexLeftToRight_(WhenFacingCharacters)");
        var desired = EnemySortOrder(capacity);
        var arrays = IntArrays(fn);
        if (arrays.Any(a => a.SequenceEqual(desired))) return 0;
        int n = ReplaceIntArray(asset, fn, new[] { 1, 2, 0, 3 }, desired);
        if (n == 0)
            throw new InvalidDataException(
                "GetBattleSpotIndexLeftToRight: enemy [1,2,0,3] table not found (or already widened)");
        Write(asset, path);
        log?.Invoke($"[battle-groups] enemy spot-order table -> {capacity} slot(s)");
        return 1;
    }

    private static int PatchSpawnTransforms(string modDir, int capacity, Action<string>? log)
    {
        if (capacity <= 4) return 0;

        var path = CopyIntoMod("AC_jRPG_BattleManager", modDir);
        var asset = Open(path);
        var fn = Fn(asset, "GetProperBattleSpawnPointTransform");
        var stmts = fn.ScriptBytecode!;
        var oldStarts = PartyFiveKit.StatementStarts(asset, stmts);

        // The enemy-branch equality is the second `EqualEqual_IntInt(slot,
        // SpotIndex)` in the function. The character branch's result local is
        // `..._ReturnValue`; the enemy branch's is `..._ReturnValue_1`.
        EX_LetBool? eqLet = null;
        foreach (var st in stmts)
        {
            if (st is not EX_LetBool lb) continue;
            if (lb.VariableExpression is not EX_LocalVariable dst
                || PartyFiveKit.FieldName(dst.Variable) != "CallFunc_EqualEqual_IntInt_ReturnValue_1")
                continue;
            if (lb.AssignmentExpression is not EX_CallMath) continue;
            eqLet = lb;
            break;
        }
        if (eqLet is null)
            throw new InvalidDataException(
                "GetProperBattleSpawnPointTransform: enemy spot equality not found");

        var spotIndexRead = FindLocal(stmts, "SpotIndex")
            ?? throw new InvalidDataException("GetProperBattleSpawnPointTransform: SpotIndex local not found");

        // Already widened (e.g. a second Apply in the same staged mod)? Any
        // constant >= 4 in the enemy equality can only come from this patch.
        bool already = MayhemFixKit.WalkHas(eqLet.AssignmentExpression,
            e => e is EX_IntConst ic && ic.Value >= 4);

        if (!already)
        {
            var eqImport = PartyFiveKit.ImportFn(asset, "EqualEqual_IntInt");
            var orImport = PartyFiveKit.ImportFn(asset, "BooleanOR");
            var spotSlot = ((EX_CallMath)eqLet.AssignmentExpression).Parameters[0];

            // Every slot is placed on the lattice relative to one anchor, so the
            // group reads as a single formation instead of a front line plus a
            // separate cluster. The vanilla `SlotIndex == SpotIndex` term is
            // replaced for that reason.
            KismetExpression? aliases = null;
            for (int spot = 0; spot < capacity; spot++)
            {
                var clause = Call(PartyFiveKit.ImportFn(asset, "BooleanAND"),
                    Call(eqImport, MayhemFixKit.CloneKismet(spotIndexRead), new EX_IntConst { Value = spot }),
                    Call(eqImport, MayhemFixKit.CloneKismet(spotSlot), new EX_IntConst { Value = LatticeAnchor }));
                aliases = aliases is null ? clause : Call(orImport, aliases, clause);
            }
            eqLet.AssignmentExpression = aliases!;

            // Enemy MakeTransform uses the spot's SpawnPosition location and the
            // enemy arrow's world rotation (the character branch's locals carry
            // the `_1` suffix instead).
            EX_Let? makeLet = null;
            foreach (var st in stmts)
            {
                if (st is not EX_Let let) continue;
                if (let.Expression is not EX_CallMath mt || Name(asset, mt.StackNode) != "MakeTransform") continue;
                if (mt.Parameters is not { Length: 3 }) continue;
                if (mt.Parameters[0] is not EX_LocalVariable loc
                    || PartyFiveKit.FieldName(loc.Variable) != "CallFunc_K2_GetComponentLocation_ReturnValue_1")
                    continue;
                if (mt.Parameters[1] is not EX_LocalVariable rot
                    || PartyFiveKit.FieldName(rot.Variable) != "CallFunc_BreakTransform_Rotation")
                    continue;
                makeLet = let;
                break;
            }
            if (makeLet is null)
                throw new InvalidDataException(
                    "GetProperBattleSpawnPointTransform: enemy MakeTransform not found");

            var make = (EX_CallMath)makeLet.Expression;
            var spawnLoc = make.Parameters[0];
            var offsets = EnemyOffsets(capacity);
            var eqImport2 = PartyFiveKit.ImportFn(asset, "EqualEqual_IntInt");
            var selectFloat = PartyFiveKit.ImportFn(asset, "SelectFloat");
            var addVector = PartyFiveKit.ImportFn(asset, "Add_VectorVector");
            var makeVector = PartyFiveKit.ImportFn(asset, "MakeVector");

            // Offsets are applied in the battle map's LOCAL frame, not each
            // anchor's own arrow frame. The four anchors sit on an arc with up to
            // 68 degrees of yaw between them, so per-anchor frames splay the same
            // offset in four different directions and the group reads as diagonal
            // streaks. The map frame is consistent across maps: the party starts
            // near the origin and the enemy anchors sit at +X, so +X is "behind".
            // EnemyOffsets stores depth as a negative forward value; negating it
            // on X pushes the slot away from the party.
            var offset = Call(makeVector,
                SelectBySpot(eqImport2, selectFloat, spotIndexRead,
                    offsets.Select(o => -o.Fwd).ToArray()),
                SelectBySpot(eqImport2, selectFloat, spotIndexRead,
                    offsets.Select(o => o.Right).ToArray()),
                new EX_FloatConst { Value = 0f });
            make.Parameters[0] = Call(addVector, spawnLoc, offset);
        }

        PartyFiveKit.RemapFlowOffsets(asset, fn, oldStarts);
        MayhemFixKit.RecalcContextSkips(asset, fn);
        PartyFiveKit.VerifyJumps(asset, fn);
        Write(asset, path);
        log?.Invoke($"[battle-groups] enemy spawn aliases + formation offsets for slots 4..{capacity - 1}");
        return 1;
    }

    // ─────────────────────────── rosters ───────────────────────────

    /// <summary>
    /// Rewrites eligible encounter rosters with up to
    /// <see cref="RandomizerKit.Settings.MaxEnemiesPerBattle"/> enemies. Returns
    /// the number of encounter rows changed.
    /// </summary>
    public static int RandomizeEncounters(string modDir, LootCatalog catalog, Random rng,
        RandomizerKit.Settings s, Action<string>? log = null)
    {
        int total = 0;
        foreach (var table in new[] { EncountersTable, WorldEncountersTable })
        {
            var path = FileCopy.FindUnder(modDir, table + ".uasset") ?? StageTable(modDir, table);
            if (path is null) continue;
            total += RandomizeTable(path, catalog, rng, s, log);
        }
        return total;
    }

    private static int RandomizeTable(string path, LootCatalog catalog, Random rng,
        RandomizerKit.Settings s, Action<string>? log)
    {
        var ed = new DataTableEditor(path);
        var tierCache = new Dictionary<string, int>(StringComparer.Ordinal);
        var poolCache = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        int max = Math.Clamp(s.MaxEnemiesPerBattle, 1, FieldCapacity);
        var areaPrefix = (s.BattleGroupsAreaPrefix ?? "").Trim();
        int changed = 0;

        foreach (var row in ed.Table.Table.Data)
        {
            var id = DataTableEditor.CleanName(row.Name);
            if (id.StartsWith(VariantPrefix, StringComparison.Ordinal)) continue;
            if (areaPrefix.Length > 0
                && !id.StartsWith(areaPrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!IsEligible(id, row.Value, catalog)) continue;
            if (Named(row.Value, "Enemies") is not MapPropertyData map || map.Value.Count == 0) continue;

            var originals = map.Value.Values
                .OfType<StructPropertyData>()
                .Select(v => RowNameOf(v))
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
            if (originals.Count == 0) continue;
            if (originals.Count >= max) continue;
            if (rng.NextDouble() * 100 >= s.ExtraEnemiesChance) continue;

            int target = rng.Next(originals.Count + 1, max + 1);
            var roster = new List<string>(originals);
            while (roster.Count < target)
            {
                var seed = originals[rng.Next(originals.Count)];
                var pool = PoolFor(seed, catalog, tierCache, poolCache);
                roster.Add(pool[rng.Next(pool.Count)]);
            }
            if (s.ShuffleBattleRosters) Shuffle(roster, rng);

            WriteRoster(ed, map, roster);
            changed++;
        }

        if (changed > 0) ed.Save(path);
        log?.Invoke($"[battle-groups] {Path.GetFileNameWithoutExtension(path)}: " +
                    $"{changed} roster(s) rewritten (max {max}" +
                    (areaPrefix.Length > 0 ? $", area {areaPrefix}" : "") + ")");
        return changed;
    }

    // ─────────────────────── runtime random battles ───────────────────────

    /// <summary>
    /// Pre-generates variant encounter rows and appends their handles to the
    /// world enemies that reference the original. The battle world base is then
    /// patched to re-roll <c>SelectedEncounter</c> from <c>PossibleEncounters</c>
    /// on every <c>StartBattle</c>, so the same overworld enemy can produce a
    /// different group each time it is attacked. Eligible base rows are also
    /// rewritten in place (seeded): level-placed instances, groups and triggers
    /// name base rows directly and never consult <c>PossibleEncounters</c>, so
    /// without the in-place rewrite those fights would stay vanilla. Finally the
    /// re-roll is extended with a variant-family re-pick, so even those placed
    /// fights roll a fresh group every attempt instead of one fixed roster.
    /// </summary>
    public static int BuildRuntimeBattles(string modDir, LootCatalog catalog, Random rng,
        RandomizerKit.Settings s, Action<string>? log = null)
    {
        int variantCount = Math.Clamp(s.BattleVariants, 1, 8);
        var areaPrefix = (s.BattleGroupsAreaPrefix ?? "").Trim();
        int made = 0;
        var variants = new Dictionary<(string Table, string Row), List<string>>();

        foreach (var table in new[] { EncountersTable, WorldEncountersTable })
        {
            var path = FileCopy.FindUnder(modDir, table + ".uasset") ?? StageTable(modDir, table);
            if (path is null) continue;

            var ed = new DataTableEditor(path);
            var tierCache = new Dictionary<string, int>(StringComparer.Ordinal);
            var poolCache = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            bool changed = false;

            foreach (var row in ed.Table.Table.Data.ToList())
            {
                var id = DataTableEditor.CleanName(row.Name);
                if (id.StartsWith(VariantPrefix, StringComparison.Ordinal)) continue;
                if (areaPrefix.Length > 0
                    && !id.StartsWith(areaPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!IsEligible(id, row.Value, catalog)) continue;
                if (Named(row.Value, "Enemies") is not MapPropertyData originalMap
                    || originalMap.Value.Count == 0) continue;

                var originals = originalMap.Value.Values
                    .OfType<StructPropertyData>()
                    .Select(RowNameOf)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();
                if (originals.Count == 0) continue;

                int max = Math.Clamp(s.MaxEnemiesPerBattle, 1, FieldCapacity);
                var sizes = Enumerable.Range(1, max).ToList();
                Shuffle(sizes, rng);

                var list = new List<string>(variantCount);
                int rowMade = 0;
                for (int i = 1; i <= variantCount; i++)
                {
                    var variant = VariantName(id, i);
                    if (!ed.HasRow(variant))
                    {
                        ed.CloneRow(id, variant);
                        var vrow = ed.Table.Table.Data.First(r => DataTableEditor.CleanName(r.Name) == variant);
                        if (Named(vrow.Value, "Enemies") is not MapPropertyData variantMap) continue;
                        int target = i <= sizes.Count ? sizes[i - 1] : rng.Next(1, max + 1);
                        var roster = BuildRuntimeRoster(originals, catalog, rng, s, tierCache, poolCache, target);
                        WriteRoster(ed, variantMap, roster);
                        made++;
                        rowMade++;
                        changed = true;
                    }
                    list.Add(variant);
                }
                if (list.Count > 0) variants[(table, id)] = list;

                // Placed fights (level-map instances, groups, triggers) name the
                // base row directly and never consult PossibleEncounters, so the
                // StartBattle re-roll cannot reach them. Rewrite the base roster
                // in place too (seeded, same pool rules) so every launch path
                // shows randomized content. Variants above were already cloned
                // from the vanilla roster, so this does not narrow them. Gated
                // on freshly-made variants to avoid drift on re-runs.
                if (rowMade > 0)
                {
                    var baseRoster = BuildRuntimeRoster(originals, catalog, rng, s, tierCache, poolCache);
                    WriteRoster(ed, originalMap, baseRoster);
                    changed = true;
                }

                // Test hook: force one or more encounters (and their variants) to
                // a fixed roster, e.g. 32 Demineurs, to probe the field capacity.
                if (s.BattleTestRow.Length > 0
                    && s.BattleTestRow.Split(',', StringSplitOptions.RemoveEmptyEntries
                            | StringSplitOptions.TrimEntries)
                        .Any(x => string.Equals(id, x, StringComparison.Ordinal))
                    && s.BattleTestEnemy.Length > 0 && s.BattleTestCount > 0)
                {
                    int count = Math.Clamp(s.BattleTestCount, 1, FieldCapacity);
                    var forced = Enumerable.Repeat(s.BattleTestEnemy, count).ToList();
                    WriteRoster(ed, originalMap, forced);
                    foreach (var variant in list)
                    {
                        var vrow = ed.Table.Table.Data.FirstOrDefault(r =>
                            DataTableEditor.CleanName(r.Name) == variant);
                        if (vrow?.Value is null) continue;
                        if (Named(vrow.Value, "Enemies") is MapPropertyData vm)
                            WriteRoster(ed, vm, forced);
                    }
                    changed = true;
                    log?.Invoke($"[random-battles] TEST ROW {id} forced to {count}x {s.BattleTestEnemy}");
                }
            }

            if (changed) ed.Save(path);
        }

        if (variants.Count == 0)
        {
            log?.Invoke("[random-battles] no eligible encounter rows; nothing to do");
            return 0;
        }

        int worldEnemies = PatchWorldEnemies(modDir, variants, log);
        // The world-enemy scan opens a lot of Blueprints; UAssetAPI mutates the
        // shared schema map while parsing, so start the base-class patch (and the
        // kits that run after the randomizer) from pristine schemas.
        DataTableEditor.ResetMappings();
        int reroll = PatchWorldEnemyStartBattle(modDir, s.BattleVariants, log);
        string famMode = FamilyRepipckMode switch
        {
            3 => "; StartBattle re-picks the variant family every attempt)",
            2 => "; family re-pick DIAGNOSTIC array-test (copies only))",
            1 => "; family re-pick DIAGNOSTIC guard-only probe)",
            _ => "; family re-pick OFF, see FamilyRepipckMode)",
        };
        log?.Invoke($"[random-battles] {made} variant row(s), {worldEnemies} world enemy BP(s), " +
                    $"re-roll patch {(reroll > 0 ? "applied" : "already present")} " +
                    $"(eligible base rosters rewritten in place for placed fights{famMode}");
        DataTableEditor.ResetMappings();
        return made + worldEnemies + reroll;
    }

    private static string VariantName(string original, int index) =>
        $"{VariantPrefix}{original}_r{index}";

    private static List<string> BuildRuntimeRoster(IReadOnlyList<string> originals, LootCatalog catalog,
        Random rng, RandomizerKit.Settings s, Dictionary<string, int> tierCache,
        Dictionary<string, List<string>> poolCache, int? forcedTarget = null)
    {
        int max = Math.Clamp(s.MaxEnemiesPerBattle, 1, FieldCapacity);
        // Runtime variants deliberately span the whole 1..max band, including
        // groups smaller than the vanilla row. The fixed-roster Battle groups
        // pass uses ExtraEnemiesChance for its own growth behaviour.
        int target = Math.Clamp(forcedTarget ?? rng.Next(1, max + 1), 1, max);
        if (originals.Count == 0) return new List<string>();

        var roster = new List<string>(originals.Take(Math.Min(target, originals.Count)));
        while (roster.Count < target)
        {
            var seed = originals[rng.Next(originals.Count)];
            var pool = PoolFor(seed, catalog, tierCache, poolCache);
            roster.Add(pool[rng.Next(pool.Count)]);
        }
        if (s.ShuffleBattleRosters) Shuffle(roster, rng);
        return roster;
    }

    private static int PatchWorldEnemies(string modDir,
        Dictionary<(string Table, string Row), List<string>> variants, Action<string>? log)
    {
        var donorRoot = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "EnemyWorld");
        EnsureEnemyWorldExtracted(donorRoot);
        if (!Directory.Exists(donorRoot)) return 0;

        int changedBps = 0;
        foreach (var src in Directory.EnumerateFiles(donorRoot, "*.uasset", SearchOption.AllDirectories).ToList())
        {
            var rel = RelativeSandfall(src);
            var dest = Path.Combine(modDir, rel);
            var readPath = File.Exists(dest) ? dest : src;

            UAsset asset;
            try { asset = Open(readPath); }
            catch { continue; }

            var cdo = asset.Exports.OfType<NormalExport>()
                .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
            if (cdo is null) continue;
            var arr = cdo.Data.OfType<ArrayPropertyData>()
                .FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "PossibleEncounters");
            if (arr is null) continue;

            bool any = arr.Value.OfType<StructPropertyData>()
                .Any(e => TryHandleKey(asset, e, out var t, out var r) && variants.ContainsKey((t, r)));
            if (!any) continue;

            var list = arr.Value.ToList();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in list.OfType<StructPropertyData>())
                if (TryHandleKey(asset, entry, out var t, out var r)) seen.Add(t + "|" + r);

            int added = 0;
            foreach (var entry in list.OfType<StructPropertyData>().ToList())
            {
                if (!TryHandleKey(asset, entry, out var table, out var row)) continue;
                if (!variants.TryGetValue((table, row), out var names)) continue;
                foreach (var name in names)
                {
                    if (!seen.Add(table + "|" + name)) continue;
                    var clone = (StructPropertyData)entry.Clone();
                    if (Named(clone.Value, "RowName") is NamePropertyData nm)
                        nm.Value = FName.FromString(asset, name);
                    list.Add(clone);
                    added++;
                }
            }
            if (added == 0) continue;

            arr.Value = list.ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            asset.Write(dest);
            changedBps++;
        }

        log?.Invoke($"[random-battles] appended variant handles to {changedBps} world enemy BP(s)");
        return changedBps;
    }

    private static bool TryHandleKey(UAsset asset, StructPropertyData handle, out string table, out string row)
    {
        table = "";
        row = "";
        if (Named(handle.Value, "DataTable") is not ObjectPropertyData dt
            || dt.Value is null || dt.Value.IsNull())
            return false;
        if (dt.Value.IsImport())
        {
            var idx = Math.Abs(dt.Value.Index) - 1;
            if (idx < 0 || idx >= asset.Imports.Count) return false;
            table = asset.Imports[idx].ObjectName.ToString();
        }
        else if (dt.Value.IsExport())
        {
            var idx = dt.Value.Index - 1;
            if (idx < 0 || idx >= asset.Exports.Count) return false;
            table = asset.Exports[idx].ObjectName.ToString();
        }
        else return false;

        if (Named(handle.Value, "RowName") is not NamePropertyData nm || nm.Value is null) return false;
        row = nm.Value.ToString();
        return table.Length > 0 && row.Length > 0;
    }

    private static void EnsureEnemyWorldExtracted(string root)
    {
        if (Directory.Exists(root)
            && Directory.EnumerateFiles(root, "*.uasset", SearchOption.AllDirectories).Any())
            return;
        Directory.CreateDirectory(root);
        using (Packer.DonorExtract.SuspendMods())
            new Retoc().ToLegacy(root, "EnemyWorld");
    }

    private static int PatchWorldEnemyStartBattle(string modDir, int variantCount, Action<string>? log)
    {
        var path = CopyIntoMod(WorldEnemyBase, modDir);
        var asset = Open(path);
        var fn = Fn(asset, "StartBattle");

        const string outItemName = "RAND_ArrayRandom_OutItem";
        const string outIndexName = "RAND_ArrayRandom_OutIndex";
        bool hasStage1 = (fn.LoadedProperties ?? Array.Empty<FProperty>())
            .Any(p => p.Name.ToString() == outItemName);
        bool hasStage2Full = (fn.LoadedProperties ?? Array.Empty<FProperty>())
            .Any(p => p.Name.ToString() == FamArrayName);
        bool hasStage2Guard = (fn.LoadedProperties ?? Array.Empty<FProperty>())
            .Any(p => p.Name.ToString() == FamOkName);
        int n = 0;
        if (!hasStage1)
        {
            n += PatchStartBattleReroll(asset, fn, outItemName, outIndexName);
            asset.Write(path);
        }
        if (FamilyRepipckMode == 3 && !hasStage2Full && !hasStage2Guard)
        {
            n += PatchFamilyRepipck(asset, fn, variantCount, guardOnly: false, arrayTest: false);
            asset.Write(path);
        }
        else if (FamilyRepipckMode == 3 && hasStage2Guard && !hasStage2Full)
        {
            throw new InvalidDataException(
                "StartBattle has a guard-only diagnostic block; rebuild the full family " +
                "re-pick from a fresh mod dir (FullPackKit always starts fresh).");
        }
        else if (FamilyRepipckMode == 2 && !hasStage2Full && !hasStage2Guard)
        {
            n += PatchFamilyRepipck(asset, fn, variantCount, guardOnly: false, arrayTest: true);
            asset.Write(path);
        }
        else if (FamilyRepipckMode == 2 && hasStage2Guard && !hasStage2Full)
        {
            throw new InvalidDataException(
                "StartBattle has a guard-only diagnostic block; rebuild the array-test " +
                "from a fresh mod dir (FullPackKit always starts fresh).");
        }
        else if (FamilyRepipckMode == 1 && !hasStage2Guard && !hasStage2Full)
        {
            n += PatchFamilyRepipck(asset, fn, variantCount, guardOnly: true, arrayTest: false);
            asset.Write(path);
        }
        if (n > 0)
            log?.Invoke("[random-battles] StartBattle now re-rolls SelectedEncounter from PossibleEncounters");
        return n;
    }

    private static int PatchStartBattleReroll(UAsset asset, FunctionExport fn,
        string outItemName, string outIndexName)
    {
        var uber = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString().StartsWith("ExecuteUbergraph_", StringComparison.Ordinal))
            ?? throw new InvalidDataException("world enemy ubergraph not found");

        var structTpl = (uber.LoadedProperties ?? Array.Empty<FProperty>())
            .OfType<FStructProperty>().FirstOrDefault(p => p.Name.ToString() == "CallFunc_Array_Random_OutItem")
            ?? throw new InvalidDataException("Array_Random OutItem local template not found");
        var intTpl = (uber.LoadedProperties ?? Array.Empty<FProperty>())
            .OfType<FGenericProperty>().FirstOrDefault(p => p.Name.ToString() == "CallFunc_Array_Random_OutIndex")
            ?? throw new InvalidDataException("Array_Random OutIndex local template not found");

        var props = (fn.LoadedProperties ?? Array.Empty<FProperty>()).ToList();
        var structLocal = (FStructProperty)ShallowClone(structTpl);
        structLocal.Name = FName.FromString(asset, outItemName);
        props.Add(structLocal);
        var intLocal = (FGenericProperty)ShallowClone(intTpl);
        intLocal.Name = FName.FromString(asset, outIndexName);
        props.Add(intLocal);
        fn.LoadedProperties = props.ToArray();

        EX_Context? uberCtx = null;
        EX_Let? uberLet = null;
        var uberStmts = uber.ScriptBytecode ?? Array.Empty<KismetExpression>();
        for (int i = 0; i < uberStmts.Length - 1; i++)
        {
            if (uberStmts[i] is EX_Context c
                && c.ContextExpression is EX_FinalFunction ff
                && Name(asset, ff.StackNode) == "Array_Random"
                && uberStmts[i + 1] is EX_Let l)
            {
                uberCtx = c;
                uberLet = l;
                break;
            }
        }
        if (uberCtx is null || uberLet is null)
            throw new InvalidDataException("world enemy ubergraph Array_Random pattern not found");

        var ctx = (EX_Context)MayhemFixKit.CloneKismet(uberCtx);
        var final = (EX_FinalFunction)ctx.ContextExpression;
        var possible = ((EX_FinalFunction)uberCtx.ContextExpression).Parameters[0];
        final.Parameters = new KismetExpression[]
        {
            (KismetExpression)MayhemFixKit.CloneKismet(possible),
            LocalRead(asset, fn, outItemName),
            LocalRead(asset, fn, outIndexName),
        };

        var let = (EX_Let)MayhemFixKit.CloneKismet(uberLet);
        let.Expression = LocalRead(asset, fn, outItemName);

        var stmts = fn.ScriptBytecode!.ToList();
        var oldStarts = PartyFiveKit.StatementStarts(asset, stmts);
        int insert = -1;
        for (int i = 0; i < stmts.Count; i++)
        {
            if (stmts[i] is EX_Let l
                && MayhemFixKit.WalkHas(l.Expression,
                    e => e is EX_InstanceVariable iv
                         && PartyFiveKit.FieldName(iv.Variable) == "SelectedEncounter"))
            {
                insert = i;
                break;
            }
        }
        if (insert < 0)
            throw new InvalidDataException("StartBattle: SelectedEncounter assignment not found");

        uint insertedSize = (uint)ctx.GetSize(asset) + (uint)let.GetSize(asset);
        stmts.Insert(insert, let);
        stmts.Insert(insert, ctx);
        fn.ScriptBytecode = stmts.ToArray();
        PartyFiveKit.RemapOffsetsForInsertion(asset, fn, oldStarts, insert, insertedSize, 2);
        MayhemFixKit.RecalcContextSkips(asset, fn);
        PartyFiveKit.VerifyJumps(asset, fn);
        return 1;
    }

    // ── Family re-pick (stage 2): per-attempt rolling for placed fights ──

    /// <summary>Family re-pick stage selector. Stage 2 (variant-family re-pick in
    /// <c>StartBattle</c>) crashed battles on start in-game (2026-10-29), so it
    /// is bisected back in: 0 = off (stage 1 + seeded base rows only), 1 =
    /// guard-only probe (name-break, existence guard, jump-to-next; behaviorally
    /// a no-op — SHIPPED and confirmed innocent in-game), 2 = array-test (family
    /// array, Adds and second re-roll, but every entry is a copy of the selected
    /// row, so behaviorally identical to stage 1; isolates the array machinery
    /// from the handle member-write), 3 = full family re-pick (SHIPPED and
    /// confirmed working in-game 2026-10-29: two Array_Add defects found and
    /// fixed along the way — RValue mispointed at the array, then the dead-code
    /// EX_LocalOutVariable operand shape).</summary>
    private const int FamilyRepipckMode = 3;
    private const string FamArrayName = "RAND_FamilyArray";
    private const string FamHandleName = "RAND_FamilyHandle";
    private const string FamBaseStrName = "RAND_FamilyBaseStr";
    private const string FamWorkStrName = "RAND_FamilyWorkStr";
    private const string FamOkName = "RAND_FamilyExists";
    private const string FamIdxName = "RAND_FamilyAddIndex";

    /// <summary>
    /// Second stage of the <c>StartBattle</c> patch: after the stage-1 re-roll
    /// picks <c>SelectedEncounter</c> from <c>PossibleEncounters</c>, re-pick
    /// uniformly from that row plus its pack-time variant family
    /// (<c>RAND_&lt;base&gt;_r1..rN</c>). Placed map instances, groups and
    /// triggers name base rows directly and never see the appended
    /// <c>PossibleEncounters</c> handles, so without this they play one fixed
    /// roster per seed. Guarded by <c>DoesDataTableRowExist</c> on the r1 name:
    /// rows without a variant family (bosses, narrative, tower, already-variant
    /// picks) keep the stage-1 selection untouched.
    /// </summary>
    private static int PatchFamilyRepipck(UAsset asset, FunctionExport fn, int variantCount,
        bool guardOnly, bool arrayTest)
    {
        variantCount = Math.Clamp(variantCount, 1, 8);

        var concatImp = PartyFiveKit.FindImportByName(asset, "Concat_StrStr");
        var existImp = PartyFiveKit.FindImportByName(asset, "DoesDataTableRowExist");
        var nameToStrImp = PartyFiveKit.CloneEngineFunction(asset, "KismetStringLibrary", "Concat_StrStr", "Conv_NameToString");
        var strToNameImp = PartyFiveKit.CloneEngineFunction(asset, "KismetStringLibrary", "Concat_StrStr", "Conv_StringToName");

        // ── locals (cloned shapes, renamed) ──
        var uber = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString().StartsWith("ExecuteUbergraph_", StringComparison.Ordinal))
            ?? throw new InvalidDataException("world enemy ubergraph not found");
        FProperty strTpl = (uber.LoadedProperties ?? Array.Empty<FProperty>())
            .FirstOrDefault(p => p.Name.ToString() == "CallFunc_Concat_StrStr_ReturnValue")
            ?? throw new InvalidDataException("string local template not found");
        FProperty intTpl = (fn.LoadedProperties ?? Array.Empty<FProperty>())
            .FirstOrDefault(p => p.Name.ToString() == "RAND_ArrayRandom_OutIndex")
            ?? throw new InvalidDataException("int local template not found");
        FProperty boolTpl = (fn.LoadedProperties ?? Array.Empty<FProperty>())
            .FirstOrDefault(p => p.Name.ToString() == "CallFunc_Not_PreBool_ReturnValue")
            ?? throw new InvalidDataException("bool local template not found");
        FProperty handleTpl = (fn.LoadedProperties ?? Array.Empty<FProperty>())
            .FirstOrDefault(p => p.Name.ToString() == "RAND_ArrayRandom_OutItem")
            ?? throw new InvalidDataException("row-handle local template not found (stage 1 must run first)");
        var cls = asset.Exports.OfType<StructExport>()
            .FirstOrDefault(e => e.ObjectName.ToString() == "BP_jRPG_Enemy_World_Base_Seamless_C")
            ?? throw new InvalidDataException("world enemy generated class not found");
        var arrTpl = (cls.LoadedProperties ?? Array.Empty<FProperty>())
            .OfType<FArrayProperty>()
            .FirstOrDefault(p => p.Name.ToString() == "PossibleEncounters")
            ?? throw new InvalidDataException("PossibleEncounters array template not found");

        var props = (fn.LoadedProperties ?? Array.Empty<FProperty>()).ToList();
        FProperty AddLocal(FProperty tpl, string name)
        {
            var c = ShallowClone(tpl);
            c.Name = FName.FromString(asset, name);
            props.Add(c);
            return c;
        }
        AddLocal(strTpl, FamBaseStrName);
        AddLocal(boolTpl, FamOkName);
        if (!guardOnly)
        {
            if (!arrayTest) AddLocal(strTpl, FamWorkStrName);
            AddLocal(intTpl, FamIdxName);
            AddLocal(handleTpl, FamHandleName);
            AddLocal(arrTpl, FamArrayName);
        }
        fn.LoadedProperties = props.ToArray();

        // ── template nodes (vanilla shapes, cloned per use) ──
        var stmts = fn.ScriptBytecode!.ToList();
        int anchor = -1;
        EX_Let? stage1Let = null;
        for (int i = 0; i < stmts.Count; i++)
        {
            if (stmts[i] is EX_Let l && l.Variable is EX_InstanceVariable iv
                && PartyFiveKit.FieldName(iv.Variable) == "SelectedEncounter"
                && MayhemFixKit.WalkHas(l.Expression, e => e is EX_LocalVariable lv
                    && PartyFiveKit.FieldName(lv.Variable) == "RAND_ArrayRandom_OutItem"))
            {
                anchor = i;
                stage1Let = l;
                break;
            }
        }
        if (anchor < 0 || stage1Let is null)
            throw new InvalidDataException("StartBattle: stage-1 re-roll assignment not found");
        var selTpl = (EX_InstanceVariable)stage1Let.Variable;
        EX_InstanceVariable Sel() => (EX_InstanceVariable)MayhemFixKit.CloneKismet(selTpl);

        var rowNameTpl = FindMemberNode(asset, "IsPlayerTooPowerfullToChase", "RowName");
        var dtTpl = FindMemberNode(asset, "CheckForErrors", "DataTable");
        EX_StructMemberContext Member(EX_StructMemberContext tpl, KismetExpression @base)
        {
            var c = (EX_StructMemberContext)MayhemFixKit.CloneKismet(tpl);
            c.StructExpression = @base;
            return c;
        }

        EX_Let? addTpl = null;
        if (!guardOnly)
        {
            var cfe = Fn(asset, "CheckForErrors");
            foreach (var s in cfe.ScriptBytecode ?? Array.Empty<KismetExpression>())
            {
                if (s is EX_Let l && l.Expression is EX_Context c
                    && c.ContextExpression is EX_FinalFunction ff
                    && Name(asset, ff.StackNode) == "Array_Add")
                {
                    addTpl = l;
                    break;
                }
            }
            if (addTpl is null)
                throw new InvalidDataException("Array_Add template not found");
        }
        EX_Context? randTpl = null;
        if (!guardOnly)
        {
            foreach (var s in stmts)
            {
                if (s is EX_Context c && c.ContextExpression is EX_FinalFunction ff
                    && Name(asset, ff.StackNode) == "Array_Random") { randTpl = c; break; }
            }
            if (randTpl is null)
                throw new InvalidDataException("Array_Random template not found");
        }

        // ── small builders ──
        KismetPropertyPointer PropPtr(string name) => new(new FFieldPath(
            new[] { FName.FromString(asset, name) },
            FPackageIndex.FromExport(asset.Exports.IndexOf(fn))));
        EX_LocalVariable Loc(string name) => new() { Variable = PropPtr(name) };
        EX_Let LetTo(string name, KismetExpression dest, KismetExpression expr) =>
            new() { Value = PropPtr(name), Variable = dest, Expression = expr };
        EX_CallMath Pure(FPackageIndex imp, params KismetExpression[] ps) =>
            new() { StackNode = imp, Parameters = ps };
        EX_StringConst Str(string v) => new() { Value = v };
        EX_Let AddToFamily(KismetExpression item)
        {
            var add = (EX_Let)MayhemFixKit.CloneKismet(addTpl);
            add.Value = PropPtr(FamIdxName);
            add.Variable = Loc(FamIdxName);
            var ctx = (EX_Context)add.Expression;
            // RValue receives the int32 Add return (every vanilla site points it
            // at the Add-return local). The array operand is a plain local read:
            // every Array_Add that provably runs in-game in this asset
            // (InitializeGameplayRooms, OnVisionShapeBeginOverlap) passes the
            // array explicitly, while the EX_LocalOutVariable-operand shape only
            // exists in CheckForErrors — which nothing calls (dead code), so its
            // shape is unproven in this VM and stays out of the patch.
            ctx.RValuePointer = PropPtr(FamIdxName);
            var call = (EX_FinalFunction)ctx.ContextExpression;
            call.Parameters[0] = Loc(FamArrayName);
            call.Parameters[1] = item;
            return add;
        }

        // ── block ──
        var block = new List<KismetExpression>();
        // s0 = NameToString(RowName(SelectedEncounter))
        block.Add(LetTo(FamBaseStrName, Loc(FamBaseStrName),
            Pure(nameToStrImp, Member(rowNameTpl, Sel()))));
        // ok = DoesExist(DataTable(SelectedEncounter), StringToName("RAND_" + s0 + "_r1"))
        KismetExpression FamName(string suffix) => Pure(strToNameImp,
            Pure(concatImp, Pure(concatImp, Str("RAND_"), Loc(FamBaseStrName)), Str(suffix)));
        block.Add(new EX_LetBool
        {
            VariableExpression = Loc(FamOkName),
            AssignmentExpression = Pure(existImp, Member(dtTpl, Sel()), FamName("_r1")),
        });
        var jump = new EX_JumpIfNot { CodeOffset = 0, BooleanExpression = Loc(FamOkName) };
        block.Add(jump);
        if (!guardOnly)
        {
            // NOTE: no EX_SetArray init — function-local arrays start empty,
            // so the explicit clear only added an unproven construct.
            // arr.Add(SelectedEncounter)
            block.Add(AddToFamily(Sel()));
            for (int i = 1; i <= variantCount; i++)
            {
                if (!arrayTest)
                {
                    // v = "RAND_" + s0 + "_rN"
                    block.Add(LetTo(FamWorkStrName, Loc(FamWorkStrName),
                        Pure(concatImp, Pure(concatImp, Str("RAND_"), Loc(FamBaseStrName)), Str("_r" + i))));
                }
                // H = SelectedEncounter (copies the live DataTable pointer)
                block.Add(LetTo(FamHandleName, Loc(FamHandleName), Sel()));
                if (!arrayTest)
                {
                    // H.RowName = StringToName(v)
                    var w = Member(rowNameTpl, Loc(FamHandleName));
                    block.Add(new EX_Let
                    {
                        Value = w.StructMemberExpression,
                        Variable = w,
                        Expression = Pure(strToNameImp, Loc(FamWorkStrName)),
                    });
                }
                // arr.Add(H)
                block.Add(AddToFamily(Loc(FamHandleName)));
            }
            // SelectedEncounter = Array_Random(arr)
            var rc = (EX_Context)MayhemFixKit.CloneKismet(randTpl);
            var rcall = (EX_FinalFunction)rc.ContextExpression;
            rcall.Parameters[0] = Loc(FamArrayName);
            rcall.Parameters[1] = LocalRead(asset, fn, "RAND_ArrayRandom_OutItem");
            rcall.Parameters[2] = LocalRead(asset, fn, "RAND_ArrayRandom_OutIndex");
            block.Add(rc);
            block.Add((EX_Let)MayhemFixKit.CloneKismet(stage1Let));
        }

        // ── splice ──
        var oldStarts = PartyFiveKit.StatementStarts(asset, stmts);
        uint insertedSize = 0;
        foreach (var b in block) insertedSize += (uint)b.GetSize(asset);
        int insert = anchor + 1;
        stmts.InsertRange(insert, block);
        fn.ScriptBytecode = stmts.ToArray();
        PartyFiveKit.RemapOffsetsForInsertion(asset, fn, oldStarts, insert, insertedSize, block.Count);
        jump.CodeOffset = PartyFiveKit.StatementStarts(asset, fn.ScriptBytecode!)[insert + block.Count];
        MayhemFixKit.RecalcContextSkips(asset, fn);
        PartyFiveKit.VerifyJumps(asset, fn);
        return block.Count;
    }

    /// <summary>Finds a struct-member read template (e.g. the RowName member of
    /// a row handle) in another function of the same asset.</summary>
    private static EX_StructMemberContext FindMemberNode(UAsset asset, string fnName, string member)
    {
        var f = Fn(asset, fnName);
        foreach (var s in f.ScriptBytecode ?? Array.Empty<KismetExpression>())
        {
            if (MayhemFixKit.WalkHas(s,
                    e => e is EX_StructMemberContext smc
                        && PartyFiveKit.FieldName(smc.StructMemberExpression) == member,
                    out var found)
                && found is EX_StructMemberContext hit)
                return hit;
        }
        throw new InvalidDataException($"{fnName}: struct member '{member}' template not found");
    }

    private static FProperty ShallowClone(FProperty p) =>
        (FProperty)p.GetType()
            .GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance |
                                          System.Reflection.BindingFlags.NonPublic)!
            .Invoke(p, null)!;

    private static EX_LocalVariable LocalRead(UAsset asset, FunctionExport fn, string name) =>
        new()
        {
            Variable = new KismetPropertyPointer(new FFieldPath(
                new[] { FName.FromString(asset, name) },
                FPackageIndex.FromExport(asset.Exports.IndexOf(fn)))),
        };

    private static bool IsEligible(string id, List<PropertyData> row, LootCatalog catalog)
    {
        if (BlockedEncounterPrefixes.Any(p => id.StartsWith(p, StringComparison.Ordinal))) return false;
        if (BlockedEncounterFragments.Any(f => id.Contains(f, StringComparison.OrdinalIgnoreCase))) return false;
        if (LootCatalog.BrokenEncounters.Contains(id)) return false;
        if (Named(row, "IsNarrativeBattle") is BoolPropertyData { Value: true }) return false;
        if (Named(row, "Enemies") is not MapPropertyData map || map.Value.Count == 0) return false;

        foreach (var value in map.Value.Values.OfType<StructPropertyData>())
        {
            var enemy = RowNameOf(value);
            if (enemy.Length == 0) return false;
            if (TutorialEnemyRows.Contains(enemy)) return false;
            if (IsMime(enemy)) return false;
            if (!TryInfo(enemy, catalog, out var info)) return false;
            if (info.CutContent) return false;
            if (BossArchetypes.Contains(info.Archetype)) return false;
            if (LootCatalog.BrokenEnemies.Contains(enemy)) return false;
        }
        return true;
    }

    /// <summary>Resolves a variant row the catalog may not know about
    /// (<c>WMV_</c> clones are only registered in the staged enemy table).</summary>
    private static bool TryInfo(string id, LootCatalog catalog, out LootCatalog.EnemyInfo info)
    {
        if (catalog.Enemies.TryGetValue(id, out info!)) return true;
        foreach (var prefix in new[] { "WMV_", "T5_" })
        {
            if (id.StartsWith(prefix, StringComparison.Ordinal)
                && catalog.Enemies.TryGetValue(id[prefix.Length..], out info!))
                return true;
        }
        info = null!;
        return false;
    }

    private static List<string> PoolFor(string seedId, LootCatalog catalog,
        Dictionary<string, int> tierCache, Dictionary<string, List<string>> poolCache)
    {
        int tier = TierFor(seedId, catalog, tierCache);
        var prefix = PrefixOf(seedId);
        var key = prefix + "|" + tier;
        if (poolCache.TryGetValue(key, out var cached)) return cached;

        var pool = catalog.Enemies
            .Where(kv => IsAddable(kv.Key, kv.Value, tier, catalog, tierCache))
            .Where(kv => PrefixOf(kv.Key) == prefix)
            .Select(kv => kv.Key)
            .ToList();

        // Keep the group in its own area/theme. A thin pool simply duplicates
        // the original row rather than pulling a stranger from another biome.
        if (pool.Count == 0) pool.Add(seedId);
        poolCache[key] = pool;
        return pool;
    }

    private static bool IsAddable(string id, LootCatalog.EnemyInfo info, int tier,
        LootCatalog catalog, Dictionary<string, int> tierCache)
    {
        if (info.CutContent) return false;
        if (BossArchetypes.Contains(info.Archetype)) return false;
        if (LootCatalog.BrokenEnemies.Contains(id)) return false;
        if (TutorialEnemyRows.Contains(id)) return false;
        if (IsMime(id)) return false;
        if (id.Contains("Test", StringComparison.OrdinalIgnoreCase)) return false;
        if (BlockedEncounterPrefixes.Any(p => id.StartsWith(p, StringComparison.Ordinal))) return false;
        if (BlockedEncounterFragments.Any(f => id.Contains(f, StringComparison.OrdinalIgnoreCase))) return false;
        return TierFor(id, catalog, tierCache) == tier;
    }

    private static int TierFor(string id, LootCatalog catalog, Dictionary<string, int> cache)
    {
        if (cache.TryGetValue(id, out var t)) return t;
        if (!TryInfo(id, catalog, out var info))
        {
            cache[id] = t = 1;
            return t;
        }
        // Variant clones inherit their base row's curated/area tier so a WMV_
        // repoint does not fall back to a bogus row level.
        var tierKey = id;
        foreach (var prefix in new[] { "WMV_", "T5_" })
            if (id.StartsWith(prefix, StringComparison.Ordinal)
                && catalog.Enemies.ContainsKey(id[prefix.Length..]))
            {
                tierKey = id[prefix.Length..];
                break;
            }
        var level = LootCatalog.ResolveEnemyLevel(info.Level, info);
        var rowTier = LootCatalog.TierFromLevel(Math.Max(1, level));
        t = catalog.AreaTierForEnemy(tierKey, info, rowTier);
        cache[id] = t;
        return t;
    }

    private static string PrefixOf(string id)
    {
        var i = id.IndexOf('_');
        return i > 0 ? id[..i] : id;
    }

    /// <summary>Mimes are intentionally excluded from random battle groups:
    /// their mimic/copy behavior does not compose with arbitrary rosters.</summary>
    private static bool IsMime(string id) =>
        id.Contains("Mime", StringComparison.OrdinalIgnoreCase);

    private static string RowNameOf(StructPropertyData entry) =>
        Named(entry.Value, "RowName") is NamePropertyData nm ? nm.Value?.ToString() ?? "" : "";

    private static void WriteRoster(DataTableEditor ed, MapPropertyData map, IReadOnlyList<string> roster)
    {
        var first = map.Value.First();
        var keyTemplate = (IntPropertyData)first.Key;
        var valueTemplate = (StructPropertyData)first.Value;
        map.Value.Clear();
        for (int i = 0; i < roster.Count; i++)
        {
            var key = (IntPropertyData)keyTemplate.Clone();
            key.Value = i;
            var value = (StructPropertyData)valueTemplate.Clone();
            var rowName = Named(value.Value, "RowName") as NamePropertyData
                ?? throw new InvalidDataException("Encounter enemy entry has no RowName");
            rowName.Value = FName.FromString(ed.Asset, roster[i]);
            map.Value.Add(key, value);
        }
    }

    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ─────────────────────────── table helpers ───────────────────────────

    private static string? StageTable(string modDir, string table)
    {
        var src = FileCopy.FindTablesAsset(table + ".uasset");
        if (src is null) return null;
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        var dest = idx >= 0
            ? Path.Combine(modDir, src[idx..])
            : Path.Combine(modDir, Path.GetFileName(src));
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    // ─────────────────────────── asset helpers ───────────────────────────

    private static string CopyIntoMod(string asset, string modDir)
    {
        var existing = FileCopy.FindUnder(modDir, asset + ".uasset");
        if (existing is not null) return existing;

        var vanilla = Path.Combine(EditorPaths.RepoRoot, "work", "party5", "vanilla");
        var src = FileCopy.FindUnder(vanilla, asset + ".uasset");
        if (src is null)
        {
            var stage = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "battle_groups");
            Directory.CreateDirectory(stage);
            using (Packer.DonorExtract.SuspendMods())
                new Retoc().ToLegacy(stage, asset);
            src = FileCopy.FindUnder(stage, asset + ".uasset")
                ?? throw new FileNotFoundException($"{asset}.uasset not found after extraction");
        }

        var rel = RelativeSandfall(src);
        var dest = Path.Combine(modDir, rel);
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    private static string RelativeSandfall(string path)
    {
        var idx = path.IndexOf("Sandfall" + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        if (idx < 0) idx = path.IndexOf("Sandfall/", StringComparison.Ordinal);
        if (idx < 0) throw new InvalidDataException(path + " is not under Sandfall/");
        return path[idx..];
    }

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

    private static FunctionExport Fn(UAsset asset, string name) =>
        asset.Exports.OfType<FunctionExport>().FirstOrDefault(f => f.ObjectName.ToString() == name)
        ?? throw new InvalidDataException(name + " function not found");

    private static PropertyData? Named(IList<PropertyData> data, string name) =>
        data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == name);

    private static string Name(UAsset asset, FPackageIndex idx) =>
        idx is not null && idx.IsImport() && Math.Abs(idx.Index) - 1 < asset.Imports.Count
            ? asset.Imports[Math.Abs(idx.Index) - 1].ObjectName.ToString()
            : "?";

    private static EX_CallMath Call(FPackageIndex import, params KismetExpression[] parameters) =>
        new() { StackNode = import, Parameters = parameters };

    private static EX_LocalVariable? FindLocal(IEnumerable<KismetExpression> stmts, string name)
    {
        EX_LocalVariable? hit = null;
        MayhemFixKit.Walk(stmts, e =>
        {
            if (hit is null && e is EX_LocalVariable lv && PartyFiveKit.FieldName(lv.Variable) == name)
                hit = lv;
        });
        return hit;
    }

    private static int ReplaceIntArray(UAsset asset, FunctionExport fn, int[] current, int[] replacement)
    {
        var oldStarts = PartyFiveKit.StatementStarts(asset, fn.ScriptBytecode!);
        int n = 0;
        foreach (var st in fn.ScriptBytecode!)
        {
            if (st is not EX_SetArray set || set.Elements is null) continue;
            var ints = set.Elements.OfType<EX_IntConst>().Select(e => e.Value).ToArray();
            if (ints.Length != set.Elements.Length || !ints.SequenceEqual(current)) continue;
            set.Elements = replacement
                .Select(v => (KismetExpression)new EX_IntConst { Value = v })
                .ToArray();
            n++;
        }
        if (n == 0) return 0;
        PartyFiveKit.RemapFlowOffsets(asset, fn, oldStarts);
        MayhemFixKit.RecalcContextSkips(asset, fn);
        PartyFiveKit.VerifyJumps(asset, fn);
        return n;
    }

    /// <summary>Nested SelectFloat: slots 0..N-2 → values[0..N-2], anything else
    /// (the last slot) → values[N-1]. Mirrors <c>PartyFiveKit</c>'s helper.</summary>
    private static KismetExpression SelectBySpot(FPackageIndex eqImport, FPackageIndex selectImport,
        KismetExpression spotIndexRead, double[] values)
    {
        if (values.Length == 0) throw new InvalidDataException("formation table is empty");
        KismetExpression acc = new EX_DoubleConst { Value = values[^1] };
        for (int i = values.Length - 2; i >= 0; i--)
        {
            acc = Call(selectImport,
                new EX_DoubleConst { Value = values[i] },
                acc,
                Call(eqImport, spotIndexRead, new EX_IntConst { Value = i }));
        }
        return acc;
    }

    // ─────────────────────────── tables ───────────────────────────

    /// <summary>Order in which enemy-array indices are dispatched left to right.
    /// The vanilla four entries are preserved; extra slots append in index order.</summary>
    public static int[] EnemyIndexOrder(int capacity)
    {
        var list = new List<int> { 2, 0, 1, 3 };
        for (int i = 4; i < Math.Clamp(capacity, 4, FieldCapacity); i++) list.Add(i);
        return list.ToArray();
    }

    /// <summary>Enemy left-to-right sort keys. Extra slots reuse the vanilla
    /// 0..2 clamp-safe values (ties are handled by the min-extraction sorter).</summary>
    public static int[] EnemySortOrder(int capacity)
    {
        var list = new List<int> { 1, 2, 0, 3 };
        var cycle = new[] { 1, 2, 0 };
        for (int i = 4; i < Math.Clamp(capacity, 4, FieldCapacity); i++)
            list.Add(cycle[(i - 4) % cycle.Length]);
        return list.ToArray();
    }

    public static int[] EnemySpotOrder(int capacity) => EnemySortOrder(capacity);

    /// <summary>Per-slot (forward, right) offsets in the spot arrow's own facing
    /// frame. Vanilla slots 0–3 stay at zero (they ARE the four authored
    /// anchors). Each further group of four goes one rank deeper, but the ranks
    /// are deliberately broken up: alternate ranks shift half a lane sideways and
    /// every extra slot gets a small deterministic jitter in both axes, so the
    /// group reads as a loose crowd instead of parallel lines. Slot indices are
    /// the hash seed, so a given slot lands in the same place every build.</summary>
    public static (double Fwd, double Right)[] EnemyOffsets(int capacity)
    {
        var cap = Math.Clamp(capacity, 4, FieldCapacity);
        var list = new (double Fwd, double Right)[cap];
        // Staggered lattice: rows of BlockColumns across, alternate columns
        // pushed half a row deeper so no two enemies share a row or a column.
        // Rows advance away from the party; every slot is placed, so a smaller
        // fight simply uses the first cells of the same lattice.
        int cols = Math.Min(BlockColumns, cap);
        for (int i = 0; i < cap; i++)
        {
            int col = i % cols;
            int row = i / cols;
            double lateral = (col - (cols - 1) / 2.0) * LatticeLateralStep;
            double depth = LatticeFirstDepth
                + row * LatticeDepthStep
                + (col % 2) * (LatticeDepthStep / 2.0);
            list[i] = (-depth, lateral);
        }
        return list;
    }

    /// <summary>Deterministic 0..1 value for a slot (formation hashing).</summary>
    /// <summary>Anchor every lattice slot is measured from (0 = leftmost
    /// authored spot, closest to the middle of the enemy arc).</summary>
    public const int LatticeAnchor = 0;

    /// <summary>Enemies per lattice row.</summary>
    public const int BlockColumns = 4;
    /// <summary>Lateral gap between lattice columns.</summary>
    public const double LatticeLateralStep = 150;
    /// <summary>Depth gap between lattice rows.</summary>
    public const double LatticeDepthStep = 140;
    /// <summary>Depth of the first lattice row behind the anchor.</summary>
    public const double LatticeFirstDepth = 130;

    /// <summary>Deterministic 0..1 value for a slot, so formation scatter is
    /// stable across rebuilds (no RNG ordering to preserve).</summary>
    private static double Hash01(int slot, int salt)
    {
        unchecked
        {
            uint x = (uint)(slot * 73856093) ^ (uint)(salt * 19349663) ^ 0x9E3779B9u;
            x ^= x >> 13;
            x *= 0x5BD1E995u;
            x ^= x >> 15;
            return (x & 0xFFFFFF) / (double)0xFFFFFF;
        }
    }

    // ─────────────────────────── verification ───────────────────────────

    /// <summary>Re-opens the staged runtime-battle assets and asserts the variant
    /// rows, world-enemy handle appends and StartBattle re-roll patch survived.</summary>
    public static void VerifyRuntimeBattles(string modDir, Action<string>? log = null)
    {
        var enc = FileCopy.FindUnder(modDir, EncountersTable + ".uasset")
            ?? throw new FileNotFoundException("staged DT_jRPG_Encounters missing");
        var ed = new DataTableEditor(enc);
        int variants = ed.Table.Table.Data.Count(r =>
            DataTableEditor.CleanName(r.Name).StartsWith(VariantPrefix, StringComparison.Ordinal));
        if (variants == 0)
            throw new InvalidDataException("no runtime variant encounter rows found");

        bool foundHandle = false;
        foreach (var path in Directory.EnumerateFiles(modDir, "*.uasset", SearchOption.AllDirectories))
        {
            var file = Path.GetFileNameWithoutExtension(path);
            if (!file.Contains("EnemyWorld", StringComparison.Ordinal)
                && !file.Contains("Enemy_World", StringComparison.Ordinal))
                continue;
            UAsset asset;
            try { asset = Open(path); }
            catch { continue; }
            var cdo = asset.Exports.OfType<NormalExport>()
                .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
            var arr = cdo?.Data.OfType<ArrayPropertyData>()
                .FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "PossibleEncounters");
            if (arr is null) continue;
            if (arr.Value.OfType<StructPropertyData>()
                .Select(e => Named(e.Value, "RowName"))
                .OfType<NamePropertyData>()
                .Any(n => n.Value?.ToString().StartsWith(VariantPrefix, StringComparison.Ordinal) == true))
            {
                foundHandle = true;
                break;
            }
        }
        if (!foundHandle)
            throw new InvalidDataException("no world enemy BP has a RAND_ PossibleEncounters handle");

        var baseBp = FileCopy.FindUnder(modDir, WorldEnemyBase + ".uasset")
            ?? throw new FileNotFoundException("staged world enemy base missing");
        var baseAsset = Open(baseBp);
        var fn = Fn(baseAsset, "StartBattle");
        if (!(fn.LoadedProperties ?? Array.Empty<FProperty>())
            .Any(p => p.Name.ToString() == "RAND_ArrayRandom_OutItem"))
            throw new InvalidDataException("StartBattle re-roll local missing");
        bool arrayRandom = false;
        MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (e is EX_FinalFunction ff
                && Name(baseAsset, ff.StackNode) == "Array_Random"
                && ff.Parameters is { Length: > 0 }
                && ff.Parameters[0] is EX_InstanceVariable iv
                && PartyFiveKit.FieldName(iv.Variable) == "PossibleEncounters")
                arrayRandom = true;
        });
        if (!arrayRandom)
            throw new InvalidDataException("StartBattle does not re-roll PossibleEncounters");
        // Family re-pick assertions follow the shipped shape (see
        // FamilyRepipckMode): a family array means the array-test or the full
        // stage; a guard local without the array means the guard-only probe.
        bool hasFamily = (fn.LoadedProperties ?? Array.Empty<FProperty>())
            .Any(p => p.Name.ToString() == FamArrayName);
        bool hasGuard = (fn.LoadedProperties ?? Array.Empty<FProperty>())
            .Any(p => p.Name.ToString() == FamOkName);
        if (hasFamily || FamilyRepipckMode >= 2)
        {
            if (!hasFamily)
                throw new InvalidDataException("StartBattle family re-pick local missing");
            bool existGuard = false;
            bool nameToStr = false;
            bool localReroll = false;
            MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
            {
                if (e is EX_CallMath cm && Name(baseAsset, cm.StackNode) == "DoesDataTableRowExist")
                    existGuard = true;
                if (e is EX_CallMath cm2 && Name(baseAsset, cm2.StackNode) == "Conv_NameToString")
                    nameToStr = true;
                if (e is EX_FinalFunction ff && Name(baseAsset, ff.StackNode) == "Array_Random"
                    && ff.Parameters is { Length: > 0 } && ff.Parameters[0] is EX_LocalVariable)
                    localReroll = true;
            });
            if (!existGuard || !nameToStr || !localReroll)
                throw new InvalidDataException("StartBattle does not re-pick the variant family");
        }
        else if (hasGuard || FamilyRepipckMode == 1)
        {
            if (!hasGuard)
                throw new InvalidDataException("StartBattle guard-only probe local missing");
            if (hasFamily)
                throw new InvalidDataException("guard-only probe must not carry the family array");
            bool existGuard = false;
            bool nameToStr = false;
            MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
            {
                if (e is EX_CallMath cm && Name(baseAsset, cm.StackNode) == "DoesDataTableRowExist")
                    existGuard = true;
                if (e is EX_CallMath cm2 && Name(baseAsset, cm2.StackNode) == "Conv_NameToString")
                    nameToStr = true;
            });
            if (!existGuard || !nameToStr)
                throw new InvalidDataException("StartBattle guard-only probe incomplete");
        }
        PartyFiveKit.VerifyJumps(baseAsset, fn);
        log?.Invoke($"[random-battles] verified {variants} variant row(s), " +
                    "world-enemy handles and StartBattle re-roll" +
                    (hasFamily ? " and family re-pick" : hasGuard ? " and guard-only probe" : ""));
    }

    /// <summary>Re-opens the staged battle assets and asserts the capacity patch
    /// survived. Used by <c>battle-groups-smoke</c>.</summary>
    public static void VerifyCapacity(string modDir, int capacity, Action<string>? log = null)
    {
        capacity = Math.Clamp(capacity, 4, FieldCapacity);

        var flPath = FileCopy.FindUnder(modDir, "FL_jRPG_CustomFunctionLibrary.uasset")
            ?? throw new FileNotFoundException("staged FL missing");
        var fl = Open(flPath);
        var idx = IntArray(Fn(fl, "GetEnemyIndexesLeftToRight"));
        if (!idx.SequenceEqual(EnemyIndexOrder(capacity)))
            throw new InvalidDataException($"GetEnemyIndexesLeftToRight = [{string.Join(",", idx)}]");
        var spot = IntArray(Fn(fl, "GetEnemyIndexToBattleSpotsLeftToRight"));
        if (!spot.SequenceEqual(EnemySpotOrder(capacity)))
            throw new InvalidDataException($"GetEnemyIndexToBattleSpotsLeftToRight = [{string.Join(",", spot)}]");

        var cbPath = FileCopy.FindUnder(modDir, "BP_jRPG_Character_Battle_Base.uasset")
            ?? throw new FileNotFoundException("staged CharacterBattleBase missing");
        var cb = Open(cbPath);
        var tables = IntArrays(Fn(cb, "GetBattleSpotIndexLeftToRight_(WhenFacingCharacters)"));
        if (!tables.Any(a => a.SequenceEqual(EnemySortOrder(capacity))))
            throw new InvalidDataException("enemy spot-order table not widened");

        if (capacity > 4)
        {
            var bmPath = FileCopy.FindUnder(modDir, "AC_jRPG_BattleManager.uasset")
                ?? throw new FileNotFoundException("staged BattleManager missing");
            var bm = Open(bmPath);
            var fn = Fn(bm, "GetProperBattleSpawnPointTransform");
            PartyFiveKit.VerifyJumps(bm, fn);
            bool alias = false;
            foreach (var st in fn.ScriptBytecode!)
                if (st is EX_LetBool lb
                    && lb.VariableExpression is EX_LocalVariable dst
                    && PartyFiveKit.FieldName(dst.Variable) == "CallFunc_EqualEqual_IntInt_ReturnValue_1")
                    alias = MayhemFixKit.WalkHas(lb.AssignmentExpression,
                        e => e is EX_IntConst ic && ic.Value == capacity - 1);
            if (!alias)
                throw new InvalidDataException(
                    $"BattleManager enemy spawn alias for slot {capacity - 1} missing");

            bool offset = false;
            foreach (var st in fn.ScriptBytecode!)
            {
                if (st is not EX_Let let || let.Expression is not EX_CallMath mt) continue;
                if (Name(bm, mt.StackNode) != "MakeTransform" || mt.Parameters is not { Length: 3 }) continue;
                if (mt.Parameters[1] is not EX_LocalVariable rot
                    || PartyFiveKit.FieldName(rot.Variable) != "CallFunc_BreakTransform_Rotation")
                    continue;
                offset = mt.Parameters[0] is EX_CallMath add && Name(bm, add.StackNode) == "Add_VectorVector";
            }
            if (!offset)
                throw new InvalidDataException("BattleManager enemy spawn offset expression missing");
        }
        log?.Invoke($"[battle-groups] capacity verified at {capacity} slot(s)");
    }

    /// <summary>Re-opens randomized encounter tables and asserts every changed
    /// roster has contiguous 0..N-1 keys and at least one group is over three.</summary>
    public static void VerifyEncounterGroups(string modDir, Action<string>? log = null,
        LootCatalog? catalog = null)
    {
        catalog ??= new LootCatalog();
        int rows = 0, big = 0;
        foreach (var table in new[] { EncountersTable, WorldEncountersTable })
        {
            var path = FileCopy.FindUnder(modDir, table + ".uasset");
            if (path is null) continue;
            var ed = new DataTableEditor(path);
            foreach (var row in ed.Table.Table.Data)
            {
                var id = DataTableEditor.CleanName(row.Name);
                if (!IsEligible(id, row.Value, catalog)) continue;
                if (Named(row.Value, "Enemies") is not MapPropertyData map || map.Value.Count == 0) continue;
                rows++;
                int i = 0;
                foreach (var key in map.Value.Keys)
                {
                    if (key is not IntPropertyData ip || ip.Value != i)
                        throw new InvalidDataException(
                            $"{table}:{id} has non-contiguous spot key {key}");
                    i++;
                }
                if (map.Value.Count > 3) big++;
            }
        }
        if (rows == 0)
            throw new InvalidDataException("no eligible encounter rows found in the staged mod");
        if (big == 0)
            throw new InvalidDataException("no encounter group over three enemies was produced");
        log?.Invoke($"[battle-groups] encounter groups verified: {big}/{rows} over three enemies");
    }

    private static int[] IntArray(FunctionExport fn)
    {
        var arrays = IntArrays(fn);
        return arrays.Count > 0
            ? arrays[0]
            : throw new InvalidDataException(fn.ObjectName + ": no int array");
    }

    private static List<int[]> IntArrays(FunctionExport fn)
    {
        var list = new List<int[]>();
        if (fn.ScriptBytecode is null) return list;
        foreach (var st in fn.ScriptBytecode)
            if (st is EX_SetArray set && set.Elements is { Length: > 0 }
                && set.Elements.All(e => e is EX_IntConst))
                list.Add(set.Elements.OfType<EX_IntConst>().Select(e => e.Value).ToArray());
        return list;
    }
}

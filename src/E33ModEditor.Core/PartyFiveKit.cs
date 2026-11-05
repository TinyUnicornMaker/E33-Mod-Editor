using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// "Party of Five" kit: the only supported party size is five. Members 1+2
/// are in the front row (left to right), members 3, 5, 4 in the back row
/// (left to right). The left-to-right ordering is expressed as
/// <c>[2,0,3,1,4]</c> party-slot → battle-spot remap: 1 and 2 take the two
/// front anchors, 3 and 4 the back flanks and 5 the back centre.
///
/// Everything is built from vanilla assets only (no third-party mod assets):
///
///   AC_jRPG_CharactersManager   CDO <c>MaxCharactersAmountInParty</c> 3 → 5.
///                               This one int gates AddCharacterToParty,
///                               ComputePartyWithExclusions and the team menu.
///   FL_jRPG_CustomFunctionLibrary
///                               <c>RemapPartyIndexToBattleSpotIndex</c> grows
///                               to five entries so party slots 4/5 get their
///                               own battle-spot indices instead of reading
///                               past the vanilla [2,0,1] table.
///   AC_jRPG_BattleManager       <c>GetProperBattleSpawnPointTransform</c>:
///                               spot indices 3/4 alias the left/centre spots
///                               when the map has no such actor, and per-slot
///                               formation offsets are added to the spawn
///                               transform (rotated into the spot's facing
///                               frame). All existing maps keep their own spot
///                               anchors; only the offsets are ours.
///
/// The formation is expressed in each spot's local frame (X = character
/// forward, Y = character right) so it adapts to rotated battle maps.
/// </summary>
public static class PartyFiveKit
{
    public const int DefaultPartySize = 5;

    /// <summary>Default per-spot formation offsets, shared by the settings
    /// defaults and the JSON migration that back-fills older four-party files.</summary>
    private static readonly (double Fwd, double Right)[] DefaultFormation =
    {
        (60, 134),    // spot 0 → party 2 (character 2): front line, right of centre
        (-64, 1),     // spot 1 → party 4 (character 4): back row, right flank
        (93, 136),    // spot 2 → party 1 (character 1): front line, left of centre
        (-69, -15),   // spot 3 → party 3 (character 3): back row, left flank
        (-100, -22),  // spot 4 → party 5 (character 5): back row, centre
    };

    public sealed class Settings
    {
        /// <summary>
        /// Whether the submodule is applied to the combined pack. Off = the
        /// pack ships the vanilla 3-member party.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>Fixed at five; Party of Four is retired.</summary>
        public int PartySize => DefaultPartySize;

        /// <summary>Set false to skip the battle-spawn formation offsets (still
        /// adds the slot-3/4 aliases, so the extra members spawn on the
        /// aliased anchors). A bisect switch for crashes.</summary>
        public bool UseFormationOffsets { get; set; } = true;

        /// <summary>Set false to skip the BattleManager spawn patch and the
        /// player spot-order table entirely (no slot-3/4 aliases either).</summary>
        public bool UseSpawnPatch { get; set; } = true;

        /// <summary>Set false to skip the on-screen
        /// <c>InitialLocation</c>/<c>TargetLocation</c> debug print that runs on
        /// every home snap. Diagnostic aid: the stock equivalents go through
        /// <c>PrintStringInLog</c>, which is log-file only.</summary>
        public bool UseResetDebugPrint { get; set; } = true;

        /// <summary>Per-spot offsets in the spot's own facing frame
        /// (Fwd = along the arrow forward, toward the enemies; Right = along the
        /// arrow right), indexed by spot index.
        ///
        /// The vanilla <c>BP_BattleMap</c> character-spot anchors sit at
        /// (−230,+8), (−223,+265) and (−211,−274) — a line running along Y with
        /// the enemies at +X, so screen-right is +Y. The values below are solved
        /// per anchor (each offset is decomposed through its own arrow yaw) so
        /// the five members land as:
        ///
        ///     front row:   1  2
        ///     back row:  3  5  4
        ///
        /// Character 1 uses the left-hand anchor, character 2 the centre one,
        /// character 3 aliases the left-hand anchor, character 4 the right-hand
        /// one, and character 5 aliases the centre anchor again. Spots 3 and 4
        /// are synthesised by <c>GetProperBattleSpawnPointTransform</c> because
        /// vanilla maps only carry slot anchors 0/1/2.
        ///
        /// Retune freely. Keep neighbours at least ~200 units apart for the
        /// game's own spacing target (`GetCharacterRadiusForPlacement`).</summary>
        public (double Fwd, double Right)[] Formation { get; set; } = DefaultFormation.ToArray();

        public static string SettingsPath =>
            Path.Combine(EditorPaths.RepoRoot, "work", "party_five.json");

        /// <summary>Pre-rename settings file (work/party_four.json). Read once so
        /// an existing install keeps its formation when it first upgrades.</summary>
        private static string LegacySettingsPath =>
            Path.Combine(EditorPaths.RepoRoot, "work", "party_four.json");

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                var path = File.Exists(SettingsPath) ? SettingsPath
                    : File.Exists(LegacySettingsPath) ? LegacySettingsPath
                    : null;
                if (path is not null)
                    s.FromJson(Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path)));
            }
            catch { /* keep defaults */ }
            return s;
        }

        public void Save() =>
            File.WriteAllText(SettingsPath, ToJson().ToString(Newtonsoft.Json.Formatting.Indented));

        public Newtonsoft.Json.Linq.JObject ToJson()
        {
            var formation = new Newtonsoft.Json.Linq.JArray();
            foreach (var (fwd, right) in Formation)
                formation.Add(new Newtonsoft.Json.Linq.JObject { ["fwd"] = fwd, ["right"] = right });
            return new Newtonsoft.Json.Linq.JObject
            {
                ["Enabled"] = Enabled,
                ["UseFormationOffsets"] = UseFormationOffsets,
                ["UseSpawnPatch"] = UseSpawnPatch,
                ["UseResetDebugPrint"] = UseResetDebugPrint,
                ["Formation"] = formation,
            };
        }

        public void FromJson(Newtonsoft.Json.Linq.JObject o)
        {
            if (o.Count == 0) return;
            Enabled = o.Value<bool?>("Enabled") ?? false;
            UseFormationOffsets = o.Value<bool?>("UseFormationOffsets") ?? true;
            UseSpawnPatch = o.Value<bool?>("UseSpawnPatch") ?? true;
            UseResetDebugPrint = o.Value<bool?>("UseResetDebugPrint") ?? true;
            if (o["Formation"] is Newtonsoft.Json.Linq.JArray arr && arr.Count > 0)
            {
                var parsed = new List<(double Fwd, double Right)>();
                foreach (var token in arr)
                {
                    if (token is not Newtonsoft.Json.Linq.JObject e) continue;
                    parsed.Add((e.Value<double?>("fwd") ?? 0, e.Value<double?>("right") ?? 0));
                }
                // Back-fill missing spots from the defaults so a settings file
                // written by the legacy four-member kit grows cleanly to five.
                for (int i = parsed.Count; i < DefaultFormation.Length; i++)
                    parsed.Add(DefaultFormation[i]);
                Formation = parsed.Take(DefaultFormation.Length).ToArray();
            }
        }
    }

    /// <summary>Assets this kit owns in a pack dir.</summary>
    public static readonly string[] OwnedAssets =
    {
        "AC_jRPG_CharactersManager",
        "FL_jRPG_CustomFunctionLibrary",
        "AC_jRPG_BattleManager",
        "BP_jRPG_Character_Battle_Base",
        "BP_AICompanion_TargetPointsComponent",
        "BP_jRPG_Character_World",
        "WBP_GM_TeamOverview3D_Page",
        "AC_jRPG_CombatMovement",
    };

    /// <summary>
    /// Player/mask skills whose ally buff count is <c>Min(Array_LastIndex,
    /// QTE successes)</c>. The moves are designed for a 3-member party: with
    /// their 0-2 QTE successes the clamp tops out at index 2, i.e. three
    /// allies. A 4th/5th party slot is never reached even when every QTE is
    /// Perfect. <see cref="PatchMultiAllyBuffTargets"/> forces the count to the
    /// target array's last index when <c>OnlySuccess</c> is true.
    ///
    /// Keep this list in sync with the "1-5 allies" / "up to 5 allies" rows in
    /// work/mod's ST_MainCharacters_Skills and the two clone moves (Restoration,
    /// Zoom Zoom). The clone entries are skipped when their character overhaul
    /// is off and the BP was never staged.
    /// </summary>
    private static readonly string[] MultiAllyBuffScripts =
    {
        "BP_Battle_SkillScript_GuardUp",                       // Maelle - Guard Up
        "BP_Battle_SkillScript_Revitalization",                // Lune - Revitalization
        "BP_Battle_SkillScript_Restoration",                   // Lune - Restoration (mod clone)
        "BP_Battle_SkillScript_Gustave_Powerful",              // Gustave - Powerful
        "BP_Battle_SkillScript_Gustave_ZoomZoom",              // Gustave - Zoom Zoom (mod clone)
        "BP_Battle_SkillScript_Powerful",                      // Verso - Powerful
        "BP_Battle_SkillScript_SpeedUp",                       // Sciel - Rush
        "BP_Battle_SkillScript_Transfo_BoucheclierShield",     // Monoco - Boucheclier Shield
        "BP_Battle_SkillScript_Transfo_OrphelinBuff",          // Monoco - Orphelin Cheers
        "BP_Battle_SkillScript_Transfo_TroubadourBuff",        // Monoco - Trumpet / random buff
    };

    /// <summary>Scripts above that only exist when their character kit has run.</summary>
    private static readonly HashSet<string> ModOnlyMultiAllyBuffScripts = new(StringComparer.Ordinal)
    {
        "BP_Battle_SkillScript_Restoration",
        "BP_Battle_SkillScript_Gustave_ZoomZoom",
    };

    /// <summary>ST_MainCharacters_Skills prefixes whose text advertises the
    /// old "1-3 allies" cap. Patched here as well as in the character kits so
    /// the text is correct even when a character overhaul is disabled.</summary>
    private static readonly string[] MultiAllyBuffDescriptionKeys =
    {
        "Lune_Revitalization_Description",
        "Lune_Restoration_Description",
        "Maelle_GuardUp_Description",
        "Sciel_SpeedUp_Description",
        "Verso_Powerful_Description",
        "Verso_Powerful_Gustave_Description",
        "Verso_ZoomZoom_Gustave_Description",
        "Monoco_BoucheclierShield_Description",
        "Monoco_OrphelinPowerful_Description",
        "Monoco_TroubadourBuff_Description",
    };

    public static int Apply(string modDir, Settings? settings = null, Action<string>? log = null)
    {
        var s = settings ?? new Settings();
        if (!s.Enabled) return 0;
        // Party size is fixed at five; the old 3/4-member paths are retired.
        // Back-fill anything a legacy settings file did not carry.
        if (s.Formation.Length < s.PartySize)
        {
            var grown = s.Formation.ToList();
            while (grown.Count < s.PartySize)
                grown.Add(DefaultFormation[Math.Min(grown.Count, DefaultFormation.Length - 1)]);
            s.Formation = grown.ToArray();
        }
        int n = 0;
        n += PatchPartyCap(modDir, s, log);
        n += PatchRemap(modDir, s, log);
        if (s.UseSpawnPatch)
        {
            n += PatchSpawnPositions(modDir, s, log);
            n += PatchCharacterBattleBase(modDir, s, log);
        }
        n += PatchCompanionPoints(modDir, s, log);
        n += PatchWorldCharacterCompanions(modDir, s, log);
        n += PatchPartyMenu(modDir, s, log);
        n += PatchMultiAllyBuffTargets(modDir, log);
        if (s.UseResetDebugPrint) n += AddResetDebugPrint(modDir, s, log);
        return n;
    }

    // ────────────────────────── asset staging ──────────────────────────

    private static string StageDir => Path.Combine(EditorPaths.RepoRoot, "work", "tables", "party_five");

    private static string? FindStaged(string fileName) =>
        FileCopy.FindUnder(StageDir, fileName);

    private static string CopyIntoMod(string asset, string modDir)
    {
        // Reuse an asset another submodule already staged. The camera kit
        // shares BP_jRPG_Character_World and the gameplay-tweaks kit shares
        // CharactersManager / FL / BattleManager, so re-copying the vanilla
        // donor over their edits would silently drop them. Every pack stages
        // into a fresh dir, so a pre-existing copy is always the merged one.
        var existing = FileCopy.FindUnder(modDir, asset + ".uasset");
        if (existing is not null) return existing;

        var src = FindStaged(asset + ".uasset");
        if (src is null)
        {
            // Fall back to the session extraction, then to extracting fresh
            // from the game (mods are moved aside for the duration).
            var session = Path.Combine(EditorPaths.RepoRoot, "work", "party5", "vanilla");
            var hit = FileCopy.FindUnder(session, asset + ".uasset");
            if (hit is not null)
            {
                var rel = RelativeSandfall(hit);
                var destStage = Path.Combine(StageDir, rel);
                FileCopy.AssetPair(hit, destStage);
                src = destStage;
            }
        }
        if (src is null)
        {
            Directory.CreateDirectory(StageDir);
            using (Packer.DonorExtract.SuspendMods())
                new Retoc().ToLegacy(StageDir, asset);
            src = FindStaged(asset + ".uasset")
                ?? throw new FileNotFoundException($"{asset}.uasset not found after extraction");
        }

        var relPath = RelativeSandfall(src);
        var dest = Path.Combine(modDir, relPath);
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

    // ────────────────────────── patch 1: party cap ──────────────────────────

    private static int PatchPartyCap(string modDir, Settings s, Action<string>? log)
    {
        var path = CopyIntoMod("AC_jRPG_CharactersManager", modDir);
        int n = 0;
        if (GameplayTweaksKit.SetCdoInt(path, "MaxCharactersAmountInParty", s.PartySize))
        {
            n++;
            log?.Invoke($"[party5] MaxCharactersAmountInParty = {s.PartySize}");
        }
        return n;
    }

    // ────────────────────────── patch 2: spot remap ──────────────────────────

    /// <summary>Party slot → battle spot index. Members 1/2 take the front
    /// anchors (spots 2/0), members 3/4 the back flanks (spots 3/1) and member 5
    /// the back centre (spot 4). Spots 3/4 are aliased by the BattleManager to
    /// vanilla anchors because maps only ship slot actors 0/1/2.</summary>
    public static int[] RemapTable() => new[] { 2, 0, 3, 1, 4 };

    /// <summary>Player left-to-right order values, indexed by battle spot. The
    /// values must stay inside <c>0..2</c>: <c>ClampAngleForLookLeftRight</c>
    /// only has switch cases for 0/1/2, and anything else locks free-aim yaw to
    /// zero. Spot 3 mirrors the left-hand spot (2); spot 4 is the back-centre
    /// and uses the middle clamp (1).</summary>
    internal static int[] SpotOrderTable() => new[] { 1, 0, 2, 2, 1 };

    /// <summary>Vanilla maps party slot → spot index via [2,0,1]. Replace the
    /// table with the configured party-size mapping (append spots 3/4).</summary>
    private static int PatchRemap(string modDir, Settings s, Action<string>? log)
    {
        var path = CopyIntoMod("FL_jRPG_CustomFunctionLibrary", modDir);
        var asset = Open(path);
        var fn = Fn(asset, "RemapPartyIndexToBattleSpotIndex");
        var stmts = fn.ScriptBytecode!.ToList();
        var oldStarts = StatementStarts(asset, stmts);
        var table = RemapTable();

        int patched = 0;
        foreach (var st in stmts)
        {
            if (st is not EX_SetArray set) continue;
            if (set.Elements is null || set.Elements.Length < 3) continue;
            var current = set.Elements.OfType<EX_IntConst>().Select(e => e.Value).ToArray();
            if (current.Length != set.Elements.Length || !current.Take(3).SequenceEqual(new[] { 2, 0, 1 }))
                continue;
            set.Elements = table
                .Select(v => (KismetExpression)new EX_IntConst { Value = v })
                .ToArray();
            patched++;
        }
        if (patched == 0)
            throw new InvalidDataException("RemapPartyIndexToBattleSpotIndex: no [2,0,1] array found");

        RemapFlowOffsets(asset, fn, oldStarts);
        MayhemFixKit.RecalcContextSkips(asset, fn);
        Write(asset, path);
        log?.Invoke($"[party5] remap table = [{string.Join(",", table)}] ({patched} array(s))");
        return patched;
    }

    // ────────────────────────── patch 3: spawn transform ──────────────────────────

    /// <summary>Adds the aliased slot-3 lookup and the formation offsets to the
    /// character branch of <c>GetProperBattleSpawnPointTransform</c>.</summary>
    private static int PatchSpawnPositions(string modDir, Settings s, Action<string>? log)
    {
        var path = CopyIntoMod("AC_jRPG_BattleManager", modDir);
        var asset = Open(path);
        var fn = Fn(asset, "GetProperBattleSpawnPointTransform");
        var stmts = fn.ScriptBytecode!.ToList();
        var oldStarts = StatementStarts(asset, stmts);
        int n = 0;

        var eqImport = ImportFn(asset, "EqualEqual_IntInt");
        var andImport = ImportFn(asset, "BooleanAND");
        var orImport = ImportFn(asset, "BooleanOR");
        var notImport = ImportFn(asset, "Not_PreBool");
        var selectFloat = ImportFn(asset, "SelectFloat");
        var mulVF = ImportFn(asset, "Multiply_VectorFloat");
        var addVector = ImportFn(asset, "Add_VectorVector");
        // KismetMathLibrary pure vector helpers on the spot's arrow rotator:
        // nesting a CallMath inside a CallMath is a vanilla pattern (verified),
        // whereas an Actor/context call cannot be nested. Using each spot's own
        // facing frame keeps the offsets correct on rotated maps.
        var fwdVector = FindImportByName(asset, "GetForwardVector");
        var rightVector = CloneEngineFunction(asset, "KismetMathLibrary", "GetForwardVector", "GetRightVector");

        // ── 3a. spot lookup: index 3 aliases the centre spot (slot 2) ──
        EX_LetBool? eqLet = null;
        foreach (var st in stmts)
        {
            if (st is not EX_LetBool lb) continue;
            if (lb.VariableExpression is not EX_LocalVariable dst
                || FieldName(dst.Variable) != "CallFunc_EqualEqual_IntInt_ReturnValue") continue;
            if (lb.AssignmentExpression is not EX_CallMath cm || Name(asset, cm.StackNode) != "EqualEqual_IntInt") continue;
            if (cm.Parameters is not { Length: 2 }) continue;
            if (cm.Parameters[1] is not EX_LocalVariable idx || FieldName(idx.Variable) != "SpotIndex") continue;
            eqLet = lb;
            break;
        }
        if (eqLet is null)
            throw new InvalidDataException("GetProperBattleSpawnPointTransform: spot equality not found");

        var spotSlot = ((EX_CallMath)eqLet.AssignmentExpression).Parameters[0];
        var enemySpot = FindLocal(asset, fn.ScriptBytecode!, "EnemySpot?")
            ?? throw new InvalidDataException("EnemySpot? local not found");
        var spotIndexRead = FindLocal(asset, fn.ScriptBytecode!, "SpotIndex")
            ?? throw new InvalidDataException("SpotIndex local not found");

        // alias = (slot == spotIndex) OR
        //         (!EnemySpot? AND ((spotIndex == 3 AND slot == 2) OR
        //                          (spotIndex == 4 AND slot == 0)))
        // Spot 3 reuses the left-hand anchor (slot 2), spot 4 the centre anchor
        // (slot 0). Both are outside the vanilla 0..2 map-slot range.
        KismetExpression Alias(int spot, int anchor) =>
            Call(andImport,
                Call(eqImport, MayhemFixKit.CloneKismet(spotIndexRead), new EX_IntConst { Value = spot }),
                Call(eqImport, MayhemFixKit.CloneKismet(spotSlot), new EX_IntConst { Value = anchor }));

        var aliases = Call(orImport, Alias(3, 2), Alias(4, 0));
        var oldEq = eqLet.AssignmentExpression;
        eqLet.AssignmentExpression = Call(orImport, oldEq,
            Call(andImport,
                new EX_CallMath { StackNode = notImport, Parameters = new[] { enemySpot } },
                aliases));

        // ── 3b. formation offsets on the character match branch ──
        EX_Let? makeLet = null;
        foreach (var st in stmts)
        {
            if (st is not EX_Let let) continue;
            if (let.Expression is not EX_CallMath mt || Name(asset, mt.StackNode) != "MakeTransform") continue;
            if (mt.Parameters is not { Length: 3 }) continue;
            if (mt.Parameters[0] is not EX_LocalVariable loc
                || FieldName(loc.Variable) != "CallFunc_K2_GetComponentLocation_ReturnValue") continue;
            if (mt.Parameters[1] is not EX_LocalVariable rot
                || FieldName(rot.Variable) != "CallFunc_BreakTransform_Rotation_1") continue;
            makeLet = let;
            break;
        }
        if (makeLet is null)
            throw new InvalidDataException("GetProperBattleSpawnPointTransform: character MakeTransform not found");

        var make = (EX_CallMath)makeLet.Expression;
        var spawnLoc = make.Parameters[0];
        var arrowRot = make.Parameters[1];

        if (s.UseFormationOffsets)
        {
            // Spawn offset in the spot's own facing frame:
            //   offset = GetForwardVector(arrowRot) * Fwd(spot)
            //          + GetRightVector(arrowRot)   * Right(spot)
            //   location = spawnLoc + offset
            // Fresh clones of the arrow-rotator read so the node isn't shared.
            var rotA = (KismetExpression)MayhemFixKit.CloneKismet(arrowRot);
            var rotB = (KismetExpression)MayhemFixKit.CloneKismet(arrowRot);
            var offset = Call(addVector,
                Call(mulVF,
                    Call(fwdVector, rotA),
                    SelectBySpot(eqImport, selectFloat, spotIndexRead, s.Formation.Select(f => f.Fwd).ToArray())),
                Call(mulVF,
                    Call(rightVector, rotB),
                    SelectBySpot(eqImport, selectFloat, spotIndexRead, s.Formation.Select(f => f.Right).ToArray())));
            make.Parameters[0] = Call(addVector, spawnLoc, offset);
            n += 2;
        }

        // ── 3c. widen the spawn spot-search to the party size ─────────────
        // `Spawn Player Character` has two hard-coded 3s: a
        // `GetAllPlayerCharacters().Length < 3` "free spots left" gate (past
        // three spawned members it asserts and returns *without* spawning),
        // and a `(spot + 1) % 3` collision wrap. Both must move with the size
        // or later members silently never spawn. Same-size int edits, so no
        // flow offsets shift.
        int widened = WidenSpawnSpotSearch(asset, s.PartySize);
        n += widened;

        RemapFlowOffsets(asset, fn, oldStarts);
        MayhemFixKit.RecalcContextSkips(asset, fn);
        VerifyJumps(asset, fn);
        VerifyJumps(asset, Fn(asset, "Spawn Player Character"));
        Write(asset, path);
        log?.Invoke(s.UseFormationOffsets
            ? $"[party5] formation + slot-3/4 aliases + {widened} spawn cap edit(s)"
            : $"[party5] slot-3/4 aliases only + {widened} spawn cap edit(s)");
        return n;
    }

    /// <summary>Finds an existing import by object name alone (any outer).
    /// Used for functions whose library the name-only search can resolve
    /// unambiguously; throws instead of fabricating an import.</summary>
    internal static FPackageIndex FindImportByName(UAsset asset, string name)
    {
        for (int i = 0; i < asset.Imports.Count; i++)
            if (asset.Imports[i].ObjectName.ToString() == name)
                return FPackageIndex.FromImport(i);
        throw new InvalidDataException($"import '{name}' not found in asset");
    }

    /// <summary>Battle characters expose a "left to right when facing
    /// characters" order derived from <c>BattleSpotIndex</c>. The player table
    /// is <c>[1,0,2]</c> (three entries) while the enemy table already has four
    /// <c>[1,2,0,3]</c>; a 5th party member reads past it. Replace the player
    /// table with the party-size ordering (always within the 0..2 yaw-clamp
    /// range).
    /// NOTE: unused (Apply routes through PatchCharacterBattleBase); kept in
    /// sync with WidenSpotOrderTable.</summary>
    private static int PatchSpotOrder(string modDir, Settings s, Action<string>? log)
    {
        var path = CopyIntoMod("BP_jRPG_Character_Battle_Base", modDir);
        var asset = Open(path);
        var fn = Fn(asset, "GetBattleSpotIndexLeftToRight_(WhenFacingCharacters)");
        var stmts = fn.ScriptBytecode!.ToList();
        var oldStarts = StatementStarts(asset, stmts);
        int n = 0;
        var table = SpotOrderTable();
        foreach (var st in stmts)
        {
            if (st is not EX_SetArray set) continue;
            if (set.Elements is null || set.Elements.Length < 3) continue;
            var current = set.Elements.OfType<EX_IntConst>().Select(e => e.Value).ToArray();
            if (current.Length != set.Elements.Length || !current.Take(3).SequenceEqual(new[] { 1, 0, 2 }))
                continue;
            set.Elements = table
                .Select(v => (KismetExpression)new EX_IntConst { Value = v })
                .ToArray();
            n++;
        }
        if (n == 0)
            throw new InvalidDataException("GetBattleSpotIndexLeftToRight: no [1,0,2] player array");
        RemapFlowOffsets(asset, fn, oldStarts);
        MayhemFixKit.RecalcContextSkips(asset, fn);
        VerifyJumps(asset, fn);
        Write(asset, path);
        log?.Invoke($"[party5] player spot-order table = [{string.Join(",", table)}]");
        return n;
    }

    /// <summary>
    /// Applies every <c>BP_jRPG_Character_Battle_Base</c> edit in ONE pass.
    /// <c>CopyIntoMod</c> re-copies the vanilla donor on every call, so all
    /// edits to one asset must share a single open — otherwise the second patch
    /// silently overwrites the first.
    /// </summary>
    private static int PatchCharacterBattleBase(string modDir, Settings s, Action<string>? log)
    {
        var path = CopyIntoMod("BP_jRPG_Character_Battle_Base", modDir);
        var asset = Open(path);
        int n = 0;

        // ── a. widen the player spot-order table to the party size ────────────
        // (see SpotOrderTable for why the appended values must stay inside the
        // vanilla 0..2 range)
        var orderFn = Fn(asset, "GetBattleSpotIndexLeftToRight_(WhenFacingCharacters)");
        n += WidenSpotOrderTable(asset, orderFn);

        Write(asset, path);
        log?.Invoke($"[party5] player spot-order table = [{string.Join(",", SpotOrderTable())}]");
        return n;
    }

    /// <summary>Replaces the player left-to-right table <c>[1,0,2]</c> with the
    /// party-size table.
    ///
    /// The appended values MUST stay within the vanilla <c>0..2</c> range, not
    /// <c>PartySize-1</c>: <c>ClampAngleForLookLeftRight</c> selects the free-aim
    /// yaw limits through a switch with cases only for 0/1/2
    /// (min/max = ‑58/105, ‑80/90, ‑80/75). An order value of 3 falls into both
    /// switch defaults, which are unassigned locals (0.0), so
    /// <c>ClampAngle(LookX, 0, 0) = 0</c> — the character can only aim up and
    /// down. Spot 3 mirrors spot 2 (both are the screen-left side); spot 4 is
    /// the back-centre and therefore uses the middle clamp (1). The only other
    /// consumer, <c>ComputeTargetGroups</c>, sorts by repeated min-extraction,
    /// which handles the resulting ties gracefully.</summary>
    private static int WidenSpotOrderTable(UAsset asset, FunctionExport fn)
    {
        var stmts = fn.ScriptBytecode!.ToList();
        var oldStarts = StatementStarts(asset, stmts);
        var table = SpotOrderTable();
        int n = 0;
        foreach (var st in stmts)
        {
            if (st is not EX_SetArray set) continue;
            if (set.Elements is null || set.Elements.Length < 3) continue;
            var current = set.Elements.OfType<EX_IntConst>().Select(e => e.Value).ToArray();
            if (current.Length != set.Elements.Length || !current.Take(3).SequenceEqual(new[] { 1, 0, 2 }))
                continue;
            set.Elements = table
                .Select(v => (KismetExpression)new EX_IntConst { Value = v })
                .ToArray();
            n++;
        }
        if (n == 0)
            throw new InvalidDataException("GetBattleSpotIndexLeftToRight: no [1,0,2] player array");
        RemapFlowOffsets(asset, fn, oldStarts);
        MayhemFixKit.RecalcContextSkips(asset, fn);
        VerifyJumps(asset, fn);
        return n;
    }

    /// <summary>
    /// Shifts the absolute ubergraph entry offsets held by the Blueprint's
    /// function stubs.
    ///
    /// NOTE: required for ANY insert into an ExecuteUbergraph. The earlier
    /// InsertRefreshInitialLocation experiment (re-capturing InitialLocation at
    /// spawn) passed every offline structural check — stub offsets, jump
    /// targets, context offsets, per-statement sizes — and still crashed
    /// battle load, so it was reverted. If a ubergraph insert is retried, the
    /// risk is the semantic flow the insert lands in, not the offsets. Each stub is
    /// <c>EX_LocalFinalFunction(StackNode = ExecuteUbergraph, [IntConst offset])</c>,
    /// where the offset is an absolute bytecode position inside the ubergraph.
    /// Inserting a statement into the ubergraph moves every statement after it,
    /// so every stub whose offset is at or past the insertion point must be
    /// bumped by the inserted byte count. Missing this crashes the VM, because
    /// those event entry points then land in the middle of an expression.
    /// </summary>
    internal static void ShiftUbergraphEntryOffsets(UAsset asset, FunctionExport uber,
        uint insertOffset, uint delta)
    {
        if (delta == 0) return;
        var uberIdx = FPackageIndex.FromExport(asset.Exports.IndexOf(uber));
        int shifted = 0;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;
            MayhemFixKit.Walk(fn.ScriptBytecode, e =>
            {
                if (e is not EX_LocalFinalFunction lf) return;
                // StackNode may be null / an import; compare by export index.
                if (lf.StackNode is null || uberIdx is null || !lf.StackNode.Equals(uberIdx)) return;
                if (lf.Parameters is not { Length: 1 } p) return;
                if (p[0] is EX_IntConst c && c.Value >= insertOffset)
                {
                    c.Value = (int)(c.Value + delta);
                    shifted++;
                }
            });
        }
        if (shifted == 0)
            throw new InvalidDataException(
                "ubergraph entry offsets: no stub shifted — insert point may be wrong");
    }

    /// <summary>World companions follow the active character at the target
    /// points configured on BP_AICompanion_TargetPointsComponent. Vanilla has
    /// two (left/right of the follower); a 5-member party fields four
    /// followers, and the companion manager caps its spawn list at the point
    /// count, so append the missing points.</summary>
    private static int PatchCompanionPoints(string modDir, Settings s, Action<string>? log)
    {
        const int want = DefaultPartySize - 1;
        var path = CopyIntoMod("BP_AICompanion_TargetPointsComponent", modDir);
        var asset = Open(path);
        var cdo = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal))
            ?? throw new InvalidDataException("TargetPoints CDO missing");
        var arr = cdo.Data.OfType<UAssetAPI.PropertyTypes.Objects.ArrayPropertyData>()
            .FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "TargetPoints")
            ?? throw new InvalidDataException("TargetPoints array missing");
        // Five-member party, four followers: the vanilla near flanks plus a
        // balanced pair of outer flanks (left/right near + left/right far).
        var extra = new[]
        {
            new FVector(-150, -400, -95),  // left outer
            new FVector(-150, 400, -95),   // right outer
            new FVector(-150, 0, -95),     // centre behind
            new FVector(-300, 0, -95),     // far centre
        };
        int n = 0;
        while (arr.Value.Length < want)
        {
            var tpl = arr.Value[0].Clone();
            if (!SetFirstVector((UAssetAPI.PropertyTypes.Objects.PropertyData)tpl, extra[Math.Min(n, extra.Length - 1)]))
                throw new InvalidDataException("TargetPoints element has no vector value");
            arr.Value = arr.Value.Append((UAssetAPI.PropertyTypes.Objects.PropertyData)tpl).ToArray();
            n++;
        }
        if (n > 0)
        {
            Write(asset, path);
            log?.Invoke($"[party5] companion follow points = {arr.Value.Length}");
        }
        return n;
    }

    /// <summary>The active world character carries its own
    /// <c>BP_AICompanion_TargetPointsComponent</c> component template, and that
    /// template overrides <c>TargetPoints</c> with the vanilla two entries. A
    /// component-template property wins over the component's class default, so
    /// patching the CDO (see <see cref="PatchCompanionPoints"/>) alone never
    /// reaches the running game: the companion manager still resolves
    /// <c>GetTotalNumberOfPoints</c> to 2 and resizes its activation list to
    /// <c>Min(party - 1, 2)</c>. Widen the override the same way.</summary>
    private static int PatchWorldCharacterCompanions(string modDir, Settings s, Action<string>? log)
    {
        const int want = DefaultPartySize - 1;
        var path = CopyIntoMod("BP_jRPG_Character_World", modDir);
        var asset = Open(path);
        var comp = asset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString() == "BP_AICompanion_TargetPointsComponent_GEN_VARIABLE")
            ?? throw new InvalidDataException("world character TargetPoints component template not found");
        var arr = comp.Data.OfType<UAssetAPI.PropertyTypes.Objects.ArrayPropertyData>()
            .FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "TargetPoints")
            ?? throw new InvalidDataException("world character TargetPoints array missing");

        int n = 0;
        while (arr.Value.Length < want)
        {
            var pts = arr.Value
                .Select(v => ReadFirstVector((UAssetAPI.PropertyTypes.Objects.PropertyData)v))
                .ToList();
            if (pts.Any(p => p is null))
                throw new InvalidDataException("world character TargetPoints element has no vector value");

            // Four followers: the vanilla near flanks plus a balanced pair of
            // outer flanks (left/right near + left/right far).
            double a = pts[0]!.Value.Y;
            double b = pts[1]!.Value.Y;
            double step = Math.Max(50, Math.Abs(a - b) / 2);
            double y = n == 0 ? Math.Min(a, b) - step
                : n == 1 ? Math.Max(a, b) + step
                : 0;
            FVector next = n <= 1
                ? new FVector(pts[0]!.Value.X, y, pts[0]!.Value.Z)
                : new FVector(pts[0]!.Value.X - 150, 0, pts[0]!.Value.Z);

            var tpl = arr.Value[^1].Clone();
            if (!SetFirstVector((UAssetAPI.PropertyTypes.Objects.PropertyData)tpl, next))
                throw new InvalidDataException("world character TargetPoints element has no vector value");
            arr.Value = arr.Value.Append((UAssetAPI.PropertyTypes.Objects.PropertyData)tpl).ToArray();
            n++;
        }
        if (n > 0)
        {
            Write(asset, path);
            log?.Invoke($"[party5] world-character follow points = {arr.Value.Length}");
        }
        return n;
    }

    /// <summary>The team menu hard-codes the party cap of 3 in two places in its
    /// event graph: the "add character to party" branch runs only while
    /// <c>CurrentParty.Length &lt; 3</c>, and the toggle action-bar button is
    /// marked forbidden once <c>CurrentParty.Length &gt;= 3</c>. Both literals
    /// become the configured party size.</summary>
    private static int PatchPartyMenu(string modDir, Settings s, Action<string>? log)
    {
        var path = CopyIntoMod("WBP_GM_TeamOverview3D_Page", modDir);
        var asset = Open(path);
        var fn = Fn(asset, "ExecuteUbergraph_WBP_GM_TeamOverview3D_Page");
        int n = 0;
        MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (e is not EX_CallMath cm || cm.Parameters is not { Length: 2 }) return;
            if (cm.StackNode is null || !cm.StackNode.IsImport()) return;
            var name = Name(asset, cm.StackNode);
            if (name is not ("Less_IntInt" or "GreaterEqual_IntInt")) return;
            if (cm.Parameters[1] is EX_IntConst c && c.Value == 3)
            {
                c.Value = s.PartySize;
                n++;
            }
        });
        if (n > 0)
        {
            Write(asset, path);
            log?.Invoke($"[party5] party-menu cap gates = {s.PartySize} ({n})");
        }
        return n;
    }

    // ─────────────────── multi-ally buff target count ───────────────────

    /// <summary>
    /// Widens the QTE-driven ally count on the moves that buff "1-3 allies".
    ///
    /// Every affected <c>OnActionEffect</c> does:
    /// <code>
    /// FillTargetsWithOtherHeroes(...)
    /// n = Min(Array_LastIndex(TargetedCharacters), SuccessCount)   // or SuccessCount + PerfectCount
    /// for i in 0..n: ApplyBuff(TargetedCharacters[i], ...)
    /// </code>
    /// With the 0-2 QTE successes these moves were authored for, the clamp
    /// stops at 2 -> three allies. The patch inserts, immediately before the
    /// count expression:
    /// <code>
    /// if (OnlySuccess) SuccessCount = Array_LastIndex
    /// </code>
    /// so a QTE chain with no misses reaches the last party slot, while partial
    /// QTE runs keep their existing 1-3 scaling. Party size is fixed at five.
    /// </summary>
    private static int PatchMultiAllyBuffTargets(string modDir, Action<string>? log)
    {
        int patched = 0;
        foreach (var stem in MultiAllyBuffScripts)
        {
            var path = FileCopy.FindUnder(modDir, stem + ".uasset");
            if (path is null)
            {
                // Restoration / Zoom Zoom are created by the character kits. If
                // that overhaul is off they do not exist, and there is nothing
                // to patch.
                if (ModOnlyMultiAllyBuffScripts.Contains(stem)) continue;
                path = CopyIntoMod(stem, modDir);
            }
            if (PatchMultiAllyBuffScript(path, stem, log)) patched++;
        }
        if (patched > 0)
            log?.Invoke($"[party5] no-miss QTE -> every party slot for {patched} multi-ally buff script(s)");
        int text = PatchMultiAllyBuffDescriptions(modDir, log);
        return patched + text;
    }

    private static int PatchMultiAllyBuffDescriptions(string modDir, Action<string>? log)
    {
        var path = CopyIntoMod("ST_MainCharacters_Skills", modDir);
        var table = DataTableEditor.OpenStringTable(path);
        var entries = new Dictionary<string, string>();
        foreach (var key in MultiAllyBuffDescriptionKeys)
        {
            foreach (var suffix in new[] { "_Long", "_Short" })
            {
                if (!table.TryGetValue(key + suffix, out var old)) continue;
                var text = old
                    .Replace("1-3 allies", $"1-{DefaultPartySize} allies", StringComparison.Ordinal)
                    .Replace("1-4 allies", $"1-{DefaultPartySize} allies", StringComparison.Ordinal)
                    .Replace("up to 3 allies", $"up to {DefaultPartySize} allies", StringComparison.Ordinal)
                    .Replace("up to 4 allies", $"up to {DefaultPartySize} allies", StringComparison.Ordinal)
                    .Replace("1 to 3 allies", $"1 to {DefaultPartySize} allies", StringComparison.Ordinal)
                    .Replace("1 to 4 allies", $"1 to {DefaultPartySize} allies", StringComparison.Ordinal);
                if (!string.Equals(text, old, StringComparison.Ordinal))
                    entries[key + suffix] = text;
            }
        }
        if (entries.Count == 0) return 0;
        DataTableEditor.AddStringTableEntries(path, entries);
        log?.Invoke($"[party5] multi-ally buff descriptions -> up to {DefaultPartySize} allies ({entries.Count} string(s))");
        return entries.Count;
    }

    private static bool PatchMultiAllyBuffScript(string path, string stem, Action<string>? log)
    {
        var asset = Open(path);
        var fn = Fn(asset, "OnActionEffect");
        var stmts = fn.ScriptBytecode!.ToList();
        var oldStarts = StatementStarts(asset, stmts);

        static string? LocalName(KismetExpression e) =>
            e is EX_LocalVariable lv ? FieldName(lv.Variable) : null;

        // Idempotence guard for this patch alone: CmdParty5 deletes its output
        // dir and rebuild-mod starts from a fresh work/mod, but this one patch
        // can safely be re-run against an already-patched copy.
        bool already = false;
        foreach (var s in stmts)
            MayhemFixKit.Walk(new[] { s }, e =>
            {
                if (already) return;
                if (e is EX_CallMath cm && Name(asset, cm.StackNode) == "Min"
                    && cm.Parameters is { Length: 2 }
                    && cm.Parameters[1] is EX_CallMath sel
                    && Name(asset, sel.StackNode) == "SelectInt")
                    already = true;
            });
        if (already) return false;

        // Locate the target-count Min. Its first operand is the target array's
        // last index; its second is either the QTE success count or the
        // success+perfect Add result.
        int minIdx = -1, comboIdx = -1, lastIndexIdx = -1;
        EX_CallMath? minCall = null;
        EX_VirtualFunction? combo = null;
        EX_LocalVariable? lastIndexRead = null;

        for (int i = 0; i < stmts.Count && minIdx < 0; i++)
        {
            if (stmts[i] is not EX_Let let) continue;
            EX_CallMath? found = null;
            MayhemFixKit.Walk(new[] { let.Expression }, e =>
            {
                if (found is not null || e is not EX_CallMath cm || cm.Parameters is not { Length: 2 }) return;
                if (Name(asset, cm.StackNode) != "Min") return;
                var first = LocalName(cm.Parameters[0]);
                var second = LocalName(cm.Parameters[1]);
                if (first?.StartsWith("CallFunc_Array_LastIndex_ReturnValue", StringComparison.Ordinal) != true) return;
                if (second?.StartsWith("CallFunc_GetLastSkillComboInfo_SuccessCount", StringComparison.Ordinal) != true
                    && second?.StartsWith("CallFunc_Add_IntInt_ReturnValue", StringComparison.Ordinal) != true) return;
                found = cm;
            });
            if (found is not null) { minIdx = i; minCall = found; }
        }

        if (minCall is null)
            throw new InvalidDataException($"{stem}: QTE ally-count Min(...) clamp not found");

        // The last combo-info call before the clamp is the one feeding it;
        // earlier calls (Gustave/Verso charges, Lune heal multiplier) use a
        // separate local set and must be left alone.
        for (int i = 0; i < minIdx; i++)
        {
            MayhemFixKit.Walk(new[] { stmts[i] }, e =>
            {
                if (e is EX_VirtualFunction vf
                    && vf.VirtualFunctionName.ToString() == "GetLastSkillComboInfo")
                {
                    combo = vf;
                    comboIdx = i;
                }
            });
        }
        if (combo is null || combo.Parameters.Length < 6)
            throw new InvalidDataException($"{stem}: target-loop GetLastSkillComboInfo not found");
        // Out params: OnlySuccess, OnlyFail, OnlyPerfect, SuccessCount, ...
        if (combo.Parameters[0] is not EX_LocalVariable onlySuccessRead
            || combo.Parameters[3] is not EX_LocalVariable successCountRead)
            throw new InvalidDataException($"{stem}: combo-info OnlySuccess/SuccessCount out params are not locals");

        // Array_LastIndex over Temp_wildcard_Variable is the target array
        // returned by FillTargetsWithOtherHeroes.
        for (int i = 0; i < minIdx; i++)
        {
            if (stmts[i] is not EX_Let let || let.Variable is not EX_LocalVariable lv) continue;
            if (LocalName(lv)?.StartsWith("CallFunc_Array_LastIndex_ReturnValue", StringComparison.Ordinal) != true) continue;
            bool overTargets = false;
            MayhemFixKit.Walk(new[] { let.Expression }, e =>
            {
                if (e is EX_FinalFunction ff
                    && Name(asset, ff.StackNode) == "Array_LastIndex"
                    && ff.Parameters is { Length: 1 }
                    && LocalName(ff.Parameters[0]) == "Temp_wildcard_Variable")
                    overTargets = true;
            });
            if (overTargets) { lastIndexIdx = i; lastIndexRead = lv; }
        }
        if (lastIndexRead is null)
            throw new InvalidDataException($"{stem}: target-array Array_LastIndex not found");

        // Replace the Min's count argument with:
        //   SelectInt(Array_LastIndex, defaultCount, OnlySuccess)
        // (true -> last index, false -> the original SuccessCount/Add result).
        // A pure math expression avoids new locals, writes and flow branches —
        // the previous two forms crashed the VM when the override path ran.
        var defaultCount = minCall.Parameters[1];
        var onlySuccess = (EX_LocalVariable)MayhemFixKit.CloneKismet(onlySuccessRead);
        var lastIndex = (EX_LocalVariable)MayhemFixKit.CloneKismet(lastIndexRead);
        minCall.Parameters[1] = new EX_CallMath
        {
            StackNode = LuneSkillsKit.EnsureKismetMathFn(asset, "SelectInt"),
            Parameters = new KismetExpression[]
            {
                lastIndex,
                MayhemFixKit.CloneKismet(defaultCount),
                onlySuccess,
            },
        };
        fn.ScriptBytecode = stmts.ToArray();

        // The Min statement changed size: remap every absolute flow target
        // from the pre-edit layout (statements are only resized, not moved).
        RemapFlowOffsets(asset, fn, oldStarts);

        VerifyJumps(asset, fn);
        Write(asset, path);
        log?.Invoke($"[party5] {stem}: OnlySuccess -> target count = Array_LastIndex");
        return true;
    }

    /// <summary>
    /// On-screen debug print of the home-snap values, taken inside
    /// <c>AC_jRPG_CombatMovement.MoveToInitialLocation</c>: the character's stored
    /// <c>InitialLocation</c> and its live <c>TargetLocation</c>.
    ///
    /// The stock equivalents go through <c>PrintStringInLog</c> (BattleManager
    /// ubergraph), which writes to the log file only — invisible in play.
    /// <c>PrintString</c> draws on screen. Everything used here is already
    /// imported in this asset (<c>PrintString</c>, <c>Concat_StrStr</c>,
    /// <c>Conv_VectorToString</c>, both <c>InitialLocation</c> and
    /// <c>TargetLocation</c> instance-variable reads), so no import is fabricated —
    /// a fabricated import with the wrong shape is a known hard-crash source.
    ///
    /// This fires at every snap: the initial five members, each resurrection and
    /// every AfterTurnEnd reset. Comparing a member's spawn-time print against
    /// its reset-time print shows whether the stored home went stale.
    ///
    /// The insert lands immediately <em>before</em> the stub's
    /// <c>EX_LocalFinalFunction</c> jump into the ubergraph. That jump carries an
    /// absolute ubergraph offset (the body of the latent action) into a function
    /// this kit does not touch, so no flow offset anywhere in the package moves —
    /// the only insert shape that cannot desynchronise an entry stub.
    /// </summary>
    private static int AddResetDebugPrint(string modDir, Settings s, Action<string>? log)
    {
        var path = CopyIntoMod("AC_jRPG_CombatMovement", modDir);
        var asset = Open(path);
        var fn = Fn(asset, "MoveToInitialLocation");
        var stmts = fn.ScriptBytecode!.ToList();

        // Locate the jump by shape rather than by index: the statements before it
        // are the latent-action parameter writes, and corrupting THAT layout is
        // far worse than this diagnostic is worth, so a layout change must fail
        // loudly instead of inserting into the wrong place.
        int insertAt = stmts.FindIndex(st => st is EX_LocalFinalFunction);
        if (insertAt < 0)
            throw new InvalidDataException("MoveToInitialLocation: ubergraph jump not found");
        if (stmts.Skip(insertAt).Any(st => st is not EX_LocalFinalFunction and not EX_Return and not EX_EndOfScript))
            throw new InvalidDataException("MoveToInitialLocation: unexpected statements after the jump");

        // Template PrintString from this same asset, already called on `self` of
        // this same component by vanilla (UpdateMovementProgression), so the
        // WorldContextObject parameter is known-good in the new host too.
        EX_CallMath? template = null;
        foreach (var st in Fn(asset, "UpdateMovementProgression").ScriptBytecode!)
            if (st is EX_CallMath cm && Name(asset, cm.StackNode) == "PrintString") { template = cm; break; }
        if (template is null) throw new InvalidDataException("PrintString template not found");
        if (template.Parameters is not { Length: >= 7 })
            throw new InvalidDataException(
                $"PrintString template has {template.Parameters?.Length ?? 0} params, expected >= 7");

        var uber = Fn(asset, "ExecuteUbergraph_AC_jRPG_CombatMovement");
        KismetExpression ReadField(string field)
        {
            KismetExpression? hit = null;
            MayhemFixKit.Walk(uber.ScriptBytecode!, e =>
            {
                if (hit is null && e is EX_InstanceVariable iv && FieldName(iv.Variable) == field)
                    hit = iv;
            });
            return hit ?? throw new InvalidDataException($"{field} variable read not found");
        }

        var concat = FindImportByName(asset, "Concat_StrStr");
        var vecToStr = FindImportByName(asset, "Conv_VectorToString");

        EX_CallMath DebugPrint(string label, string field, string key)
        {
            var p = (EX_CallMath)MayhemFixKit.CloneKismet(template);
            // Parameters[1] (InString) held a local that exists only in the
            // template function; replace it rather than carry the dangling
            // reference across, which the VM would resolve as garbage.
            p.Parameters[1] = Call(concat,
                new EX_StringConst { Value = $"[party5] {label} = " },
                Call(vecToStr, MayhemFixKit.CloneKismet(ReadField(field))));
            // ToScreen is already true in the template; mirror it into ToLog so
            // the value lands in the log too, in case the on-screen debug
            // channel is disabled in this build's viewport.
            p.Parameters[3] = MayhemFixKit.CloneKismet(p.Parameters[2]);
            p.Parameters[5] = new EX_FloatConst { Value = 30f };   // duration, seconds
            p.Parameters[6] = new EX_NameConst { Value = FName.FromString(asset, key) };
            return p;
        }

        // Distinct on-screen keys: UE's on-screen debug messages are per-key
        // slots, so a shared key would leave only the last print visible.
        stmts.Insert(insertAt, DebugPrint("InitialLocation", "InitialLocation", "PartyFiveInitial"));
        stmts.Insert(insertAt + 1, DebugPrint("TargetLocation", "TargetLocation", "PartyFiveTarget"));
        fn.ScriptBytecode = stmts.ToArray();

        VerifyJumps(asset, fn);
        Write(asset, path);
        log?.Invoke("[party5] on-screen InitialLocation/TargetLocation print at every home snap");
        return 2;
    }

    /// <summary>Mirror image of <see cref="SetFirstVector"/>: reads the vector
    /// out of a TargetPoints element, or <c>null</c> if it has none.</summary>
    private static FVector? ReadFirstVector(UAssetAPI.PropertyTypes.Objects.PropertyData p)
    {
        if (p is UAssetAPI.PropertyTypes.Structs.VectorPropertyData vp) return vp.Value;
        if (p is UAssetAPI.PropertyTypes.Structs.StructPropertyData sp && sp.Value is not null)
            foreach (var c in sp.Value)
            {
                var r = ReadFirstVector(c);
                if (r is not null) return r;
            }
        return null;
    }

    private static bool SetFirstVector(UAssetAPI.PropertyTypes.Objects.PropertyData p, FVector v)
    {
        if (p is UAssetAPI.PropertyTypes.Structs.VectorPropertyData vp)
        {
            vp.Value = v;
            return true;
        }
        if (p is UAssetAPI.PropertyTypes.Structs.StructPropertyData sp && sp.Value is not null)
            foreach (var c in sp.Value)
                if (SetFirstVector(c, v)) return true;
        return false;
    }

    /// <summary>
    /// Remaps flow offsets after a block of <paramref name="insertedCount"/>
    /// statements was INSERTED at <paramref name="insertIdx"/>. The plain linear
    /// remap is wrong here because statement indices shift: statements before the
    /// insertion keep their position, statements at/after it move to
    /// index + insertedCount.
    /// </summary>
    internal static void RemapOffsetsForInsertion(UAsset asset, FunctionExport fn,
        List<uint> oldStarts, int insertIdx, uint insertedSize, int insertedCount = 1)
    {
        if (insertedSize == 0 || insertedCount <= 0) return;
        var curStarts = StatementStarts(asset, fn.ScriptBytecode!);
        uint Map(uint target)
        {
            int idx = 0;
            for (int i = 0; i < oldStarts.Count; i++)
            {
                if (oldStarts[i] <= target) idx = i;
                else break;
            }
            uint rel = target - oldStarts[idx];
            int newIdx = idx < insertIdx ? idx : Math.Min(idx + insertedCount, curStarts.Count - 1);
            return curStarts[newIdx] + rel;
        }
        uint p = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref p, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j: j.CodeOffset = Map(j.CodeOffset); break;
                    case EX_JumpIfNot jn: jn.CodeOffset = Map(jn.CodeOffset); break;
                    case EX_PushExecutionFlow pf: pf.PushingAddress = Map(pf.PushingAddress); break;
                    case EX_SwitchValue sv:
                        sv.EndGotoOffset = Map(sv.EndGotoOffset);
                        for (int i = 0; i < sv.Cases.Length; i++)
                            sv.Cases[i].NextOffset = Map(sv.Cases[i].NextOffset);
                        break;
                }
            });
    }

    /// <summary>Widens the two hard-coded 3s in <c>Spawn Player Character</c>:
    /// the `GetAllPlayerCharacters().Length &lt; 3` gate and the `(spot + 1) % 3`
    /// collision wrap. Without this the 4th/5th members never spawn at all.</summary>
    internal static int WidenSpawnSpotSearch(UAsset asset, int partySize)
    {
        var fn = Fn(asset, "Spawn Player Character");
        var stmts = fn.ScriptBytecode!;
        int n = 0;

        for (int i = 0; i < stmts.Length; i++)
        {
            // Gate: LetBool CallFunc_Less_IntInt_ReturnValue = Less_IntInt(
            //          CallFunc_Array_Length_ReturnValue_2, 3)
            if (stmts[i] is EX_LetBool lb
                && lb.AssignmentExpression is EX_CallMath gate
                && Name(asset, gate.StackNode) == "Less_IntInt"
                && gate.Parameters is { Length: 2 }
                && gate.Parameters[0] is EX_LocalVariable gl
                && FieldName(gl.Variable) == "CallFunc_Array_Length_ReturnValue_2"
                && gate.Parameters[1] is EX_IntConst gc && gc.Value == 3)
            {
                gc.Value = partySize;
                n++;
                continue;
            }

            // Wrap: Let CallFunc_Percent_IntInt_ReturnValue = Percent_IntInt(
            //          CallFunc_Add_IntInt_ReturnValue, 3)
            if (stmts[i] is EX_Let let
                && let.Expression is EX_CallMath pct
                && Name(asset, pct.StackNode) == "Percent_IntInt"
                && pct.Parameters is { Length: 2 }
                && pct.Parameters[0] is EX_LocalVariable pl
                && FieldName(pl.Variable) == "CallFunc_Add_IntInt_ReturnValue"
                && pct.Parameters[1] is EX_IntConst pc && pc.Value == 3)
            {
                pc.Value = partySize;
                n++;
            }
        }

        if (n != 2)
            throw new InvalidDataException(
                $"Spawn Player Character: expected 2 hard-coded spot caps to widen, found {n}");
        return n;
    }

    /// <summary>Nested SelectFloat: spot 0..N-2 → values[0..N-2], anything else
    /// (the last configured spot) → values[N-1].</summary>
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

    // ────────────────────────── helpers ──────────────────────────

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

    public static string? FieldName(KismetPropertyPointer? p) =>
        p?.New?.Path is { Length: > 0 } path ? path[0].ToString() : null;

    private static string Name(UAsset asset, FPackageIndex idx) =>
        idx is not null && idx.IsImport() && Math.Abs(idx.Index) - 1 < asset.Imports.Count
            ? asset.Imports[Math.Abs(idx.Index) - 1].ObjectName.ToString()
            : "?";

    internal static FPackageIndex ImportFn(UAsset asset, string functionName)
    {
        // 1. An import of that name inside KismetMathLibrary.
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (imp.ObjectName.ToString() != functionName) continue;
            if (!imp.OuterIndex.IsImport()) continue;
            var outer = asset.Imports[Math.Abs(imp.OuterIndex.Index) - 1];
            if (outer.ObjectName.ToString() != "KismetMathLibrary") continue;
            return FPackageIndex.FromImport(i);
        }
        // 2. Any import of that name at all (Actor/library methods). Returning the
        //    existing import is always correct; silently fabricating a new one was
        //    a crash source (an import the game cannot resolve).
        for (int i = 0; i < asset.Imports.Count; i++)
            if (asset.Imports[i].ObjectName.ToString() == functionName)
                return FPackageIndex.FromImport(i);
        throw new InvalidDataException($"import '{functionName}' not found in asset");
    }

    private static EX_CallMath Call(FPackageIndex import, params KismetExpression[] parameters) =>
        new() { StackNode = import, Parameters = parameters };

    /// <summary>Adds an engine-library function import with the *exact* shape of an
    /// existing one (ClassPackage/ClassName/OuterIndex). Engine-class function
    /// imports carry <c>ClassName=Object</c> while BP-generated ones carry
    /// <c>Function</c>; constructing one with the wrong class name makes the
    /// package's import unresolvable, and the VM dereferences a null StackNode on
    /// the first call — a hard crash at battle start.</summary>
    internal static FPackageIndex CloneEngineFunction(UAsset asset, string library, string templateName, string newName)
    {
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (imp.ObjectName.ToString() != newName) continue;
            if (!imp.OuterIndex.IsImport()) continue;
            if (asset.Imports[Math.Abs(imp.OuterIndex.Index) - 1].ObjectName.ToString() == library)
                return FPackageIndex.FromImport(i);
        }
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            if (imp.ObjectName.ToString() != templateName) continue;
            if (!imp.OuterIndex.IsImport()) continue;
            if (asset.Imports[Math.Abs(imp.OuterIndex.Index) - 1].ObjectName.ToString() != library) continue;
            return asset.AddImport(new Import(imp.ClassPackage, imp.ClassName, imp.OuterIndex,
                FName.FromString(asset, newName), false));
        }
        throw new InvalidDataException($"{library}.{templateName} template import not found");
    }

    private static EX_LocalVariable? FindLocal(UAsset asset, KismetExpression[] stmts, string name)
    {
        EX_LocalVariable? hit = null;
        MayhemFixKit.Walk(stmts, e =>
        {
            if (hit is null && e is EX_LocalVariable lv && FieldName(lv.Variable) == name)
                hit = lv;
        });
        return hit;
    }

    public static List<uint> StatementStarts(UAsset asset, IReadOnlyList<KismetExpression> stmts)
    {
        var starts = new List<uint>(stmts.Count);
        uint pos = 0;
        foreach (var s in stmts)
        {
            starts.Add(pos);
            pos += (uint)s.GetSize(asset);
        }
        return starts;
    }

    /// <summary>Remaps every absolute flow offset from the pre-edit bytecode to
    /// the post-edit bytecode. Because statements are only resized (never
    /// reordered), each old offset maps to the statement that contains it plus
    /// its original in-statement delta. This is essential for
    /// <see cref="EX_SwitchValue"/>'s <c>EndGotoOffset</c> and per-case
    /// <c>NextOffset</c>, which point *inside* the switch expression and are not
    /// statement starts — mapping them to a statement start corrupts the switch.</summary>
    internal static void RemapFlowOffsets(UAsset asset, FunctionExport fn, List<uint> oldStarts)
    {
        var curStarts = StatementStarts(asset, fn.ScriptBytecode!);
        uint Map(uint target)
        {
            int idx = 0;
            for (int i = 0; i < oldStarts.Count; i++)
            {
                if (oldStarts[i] <= target) idx = i;
                else break;
            }
            idx = Math.Min(idx, curStarts.Count - 1);
            return curStarts[idx] + (target - oldStarts[idx]);
        }
        uint p = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref p, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j: j.CodeOffset = Map(j.CodeOffset); break;
                    case EX_JumpIfNot jn: jn.CodeOffset = Map(jn.CodeOffset); break;
                    case EX_PushExecutionFlow pf: pf.PushingAddress = Map(pf.PushingAddress); break;
                    case EX_SwitchValue sv:
                        sv.EndGotoOffset = Map(sv.EndGotoOffset);
                        for (int i = 0; i < sv.Cases.Length; i++)
                            sv.Cases[i].NextOffset = Map(sv.Cases[i].NextOffset);
                        break;
                }
            });
    }

    /// <summary>Every jump/flow target must land on a statement start (the game's
    /// VM executes unchecked code offsets; a stale one is a hard crash).
    /// Switch <c>EndGotoOffset</c>/case <c>NextOffset</c> are intra-statement
    /// offsets, so they are only range-checked.</summary>
    internal static void VerifyJumps(UAsset asset, FunctionExport fn)
    {
        var starts = StatementStarts(asset, fn.ScriptBytecode!);
        var startSet = new HashSet<uint>(starts);
        uint total = 0;
        foreach (var s in fn.ScriptBytecode!) total += (uint)s.GetSize(asset);
        var bad = new List<string>();
        uint p = 0;
        foreach (var st in fn.ScriptBytecode!)
            st.Visit(asset, ref p, (e, _) =>
            {
                switch (e)
                {
                    case EX_Jump j when !startSet.Contains(j.CodeOffset): bad.Add("Jump→" + j.CodeOffset); break;
                    case EX_JumpIfNot jn when !startSet.Contains(jn.CodeOffset): bad.Add("JumpIfNot→" + jn.CodeOffset); break;
                    case EX_PushExecutionFlow pf when !startSet.Contains(pf.PushingAddress): bad.Add("Push→" + pf.PushingAddress); break;
                    case EX_SwitchValue sv:
                        if (sv.EndGotoOffset == 0 || sv.EndGotoOffset > total) bad.Add("SwitchEnd→" + sv.EndGotoOffset);
                        foreach (var c in sv.Cases)
                            if (c.NextOffset == 0 || c.NextOffset > total) bad.Add("SwitchCase→" + c.NextOffset);
                        break;
                }
            });
        if (bad.Count > 0)
            throw new InvalidDataException($"{fn.ObjectName}: invalid jump targets: {string.Join(", ", bad.Take(6))}");
    }

    // ────────────────────────── verification ──────────────────────────

    /// <summary>Re-opens the staged assets and asserts the patch survived the
    /// write. Used by the smoke test and safe after every pack.</summary>
    public static void Verify(string modDir, Settings? settings = null)
    {
        var s = settings ?? new Settings();

        // cap
        var cm = FileCopy.FindUnder(modDir, "AC_jRPG_CharactersManager.uasset")
            ?? throw new FileNotFoundException("staged CharactersManager missing");
        var cmAsset = Open(cm);
        var cdo = cmAsset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal))
            ?? throw new InvalidDataException("CharactersManager CDO missing");
        var cap = cdo.Data.OfType<UAssetAPI.PropertyTypes.Objects.IntPropertyData>()
            .FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == "MaxCharactersAmountInParty")
            ?? throw new InvalidDataException("MaxCharactersAmountInParty missing");
        if (cap.Value != s.PartySize)
            throw new InvalidDataException($"MaxCharactersAmountInParty = {cap.Value}, want {s.PartySize}");

        // remap
        var fl = FileCopy.FindUnder(modDir, "FL_jRPG_CustomFunctionLibrary.uasset")
            ?? throw new FileNotFoundException("staged FL missing");
        var flAsset = Open(fl);
        var remap = Fn(flAsset, "RemapPartyIndexToBattleSpotIndex");
        var arrays = new List<int[]>();
        foreach (var st in remap.ScriptBytecode!)
        {
            if (st is EX_SetArray sa)
                arrays.Add(sa.Elements.OfType<EX_IntConst>().Select(e => e.Value).ToArray());
        }
        var expectedRemap = RemapTable();
        if (arrays.Count < 2 || arrays.Any(a => !a.SequenceEqual(expectedRemap)))
            throw new InvalidDataException(
                $"remap table is not [{string.Join(",", expectedRemap)}]");
        VerifyJumps(flAsset, remap);

        if (s.UseFormationOffsets && s.Formation.Length < s.PartySize)
            throw new InvalidDataException(
                $"formation has {s.Formation.Length} spot(s), want {s.PartySize}");

        // spawn positions
        var bm = FileCopy.FindUnder(modDir, "AC_jRPG_BattleManager.uasset");
        if (bm is not null && s.UseSpawnPatch)
        {
            var bmAsset = Open(bm);
            var spawn = Fn(bmAsset, "GetProperBattleSpawnPointTransform");
            VerifyJumps(bmAsset, spawn);

            // The GetRightVector import must match the vanilla shape of another
            // KismetMathLibrary function import (ClassName=Object); a mismatched
            // import never resolves and crashes the VM on the first call.
            var rv = bmAsset.Imports.FirstOrDefault(i => i.ObjectName.ToString() == "GetRightVector");
            var mv = bmAsset.Imports.FirstOrDefault(i => i.ObjectName.ToString() == "MakeVector");
            if (rv is null || mv is null) throw new InvalidDataException("GetRightVector/GetForwardVector import missing");
            if (rv.ClassName.ToString() != mv.ClassName.ToString()
                || rv.ClassPackage.ToString() != mv.ClassPackage.ToString())
                throw new InvalidDataException(
                    $"GetRightVector import shape {rv.ClassPackage}/{rv.ClassName} != GetForwardVector {mv.ClassPackage}/{mv.ClassName}");
            bool hasOffsets = false, hasAlias = false;
            int aliasAnds = 0;
            var aliasPairs = new List<(int Spot, int Anchor)>();
            MayhemFixKit.Walk(spawn.ScriptBytecode!, e =>
            {
                if (e is EX_CallMath cmc && Name(bmAsset, cmc.StackNode) == "GetRightVector")
                    hasOffsets = true;
                if (e is EX_LetBool lb
                    && FieldName((lb.VariableExpression as EX_LocalVariable)?.Variable) == "CallFunc_EqualEqual_IntInt_ReturnValue"
                    && lb.AssignmentExpression is EX_CallMath o && Name(bmAsset, o.StackNode) == "BooleanOR")
                {
                    hasAlias = true;
                    MayhemFixKit.Walk(new[] { (KismetExpression)lb.AssignmentExpression }, n =>
                    {
                        if (n is not EX_CallMath a || Name(bmAsset, a.StackNode) != "BooleanAND") return;
                        aliasAnds++;
                        if (a.Parameters is not { Length: 2 }) return;
                        int? spot = null, anchor = null;
                        foreach (var p in a.Parameters.OfType<EX_CallMath>())
                        {
                            if (p.Parameters is not { Length: 2 }) continue;
                            if (p.Parameters[1] is not EX_IntConst c) continue;
                            if (p.Parameters[0] is EX_LocalVariable lv
                                && FieldName(lv.Variable) == "SpotIndex")
                                spot = c.Value;
                            else
                                anchor = c.Value;
                        }
                        if (spot.HasValue && anchor.HasValue)
                            aliasPairs.Add((spot.Value, anchor.Value));
                    });
                }
            });
            const int wantAnds = 3;
            if (!hasAlias || aliasAnds < wantAnds)
                throw new InvalidDataException(
                    $"BattleManager spawn alias incomplete ({aliasAnds} AND node(s), want {wantAnds})");
            foreach (var want in new[] { (Spot: 3, Anchor: 2), (Spot: 4, Anchor: 0) })
            {
                if (!aliasPairs.Contains(want))
                    throw new InvalidDataException(
                        $"BattleManager spawn alias spot {want.Spot} -> anchor {want.Anchor} missing");
            }
            if (!hasOffsets && s.UseFormationOffsets)
                throw new InvalidDataException("BattleManager spawn patch incomplete");

            if (s.UseFormationOffsets)
            {
                var doubles = new List<double>();
                MayhemFixKit.Walk(spawn.ScriptBytecode!, e =>
                {
                    if (e is EX_DoubleConst d) doubles.Add(d.Value);
                });
                foreach (var (fwd, right) in s.Formation.Take(s.PartySize))
                {
                    if (!doubles.Any(d => Math.Abs(d - fwd) < 1e-6))
                        throw new InvalidDataException($"formation Fwd value {fwd} missing from spawn offsets");
                    if (!doubles.Any(d => Math.Abs(d - right) < 1e-6))
                        throw new InvalidDataException($"formation Right value {right} missing from spawn offsets");
                }
            }

            // The two hard-coded spot caps in "Spawn Player Character" must have
            // been widened; otherwise the 4th/5th members are never spawned.
            var spFn = Fn(bmAsset, "Spawn Player Character");
            int widened = 0;
            MayhemFixKit.Walk(spFn.ScriptBytecode!, e =>
            {
                if (e is not EX_CallMath cm || cm.Parameters is not { Length: 2 }) return;
                if (cm.Parameters[1] is not EX_IntConst c || c.Value != s.PartySize) return;
                var nm2 = Name(bmAsset, cm.StackNode);
                if (nm2 is "Less_IntInt" && cm.Parameters[0] is EX_LocalVariable lv0
                    && FieldName(lv0.Variable) == "CallFunc_Array_Length_ReturnValue_2")
                    widened++;
                else if (nm2 == "Percent_IntInt" && cm.Parameters[0] is EX_LocalVariable lv1
                    && FieldName(lv1.Variable) == "CallFunc_Add_IntInt_ReturnValue")
                    widened++;
            });
            if (widened != 2)
                throw new InvalidDataException(
                    $"Spawn Player Character: {widened}/2 spot caps widened to {s.PartySize}");
        }

        // companion follow points
        var tp = FileCopy.FindUnder(modDir, "BP_AICompanion_TargetPointsComponent.uasset")
            ?? throw new FileNotFoundException("staged TargetPoints missing");
        var tpAsset = Open(tp);
        var tpCdo = tpAsset.Exports.OfType<NormalExport>()
            .First(e => e.ObjectName.ToString().StartsWith("Default__", StringComparison.Ordinal));
        var tpArr = tpCdo.Data.OfType<UAssetAPI.PropertyTypes.Objects.ArrayPropertyData>()
            .First(p => DataTableEditor.CleanName(p.Name) == "TargetPoints");
        if (tpArr.Value.Length < s.PartySize - 1)
            throw new InvalidDataException($"TargetPoints = {tpArr.Value.Length}, want >= {s.PartySize - 1}");

        // world-character follow points: the component *template* override, not
        // the class CDO, is what the running component actually uses.
        var wc = FileCopy.FindUnder(modDir, "BP_jRPG_Character_World.uasset")
            ?? throw new FileNotFoundException("staged BP_jRPG_Character_World missing");
        var wcAsset = Open(wc);
        var comp = wcAsset.Exports.OfType<NormalExport>()
            .FirstOrDefault(e => e.ObjectName.ToString() == "BP_AICompanion_TargetPointsComponent_GEN_VARIABLE")
            ?? throw new InvalidDataException("world character TargetPoints component template missing");
        var compArr = comp.Data.OfType<UAssetAPI.PropertyTypes.Objects.ArrayPropertyData>()
            .First(p => DataTableEditor.CleanName(p.Name) == "TargetPoints");
        if (compArr.Value.Length < s.PartySize - 1)
            throw new InvalidDataException(
                $"world character TargetPoints = {compArr.Value.Length}, want >= {s.PartySize - 1}");
        var wcPts = compArr.Value.Select(v => ReadFirstVector((UAssetAPI.PropertyTypes.Objects.PropertyData)v));
        if (wcPts.Any(p => p is null))
            throw new InvalidDataException("world character TargetPoints element has no vector value");

        // player left-to-right spot order
        var cbb = FileCopy.FindUnder(modDir, "BP_jRPG_Character_Battle_Base.uasset");
        if (cbb is not null && s.UseSpawnPatch)
        {
            var cbbAsset = Open(cbb);
            var orderFn = Fn(cbbAsset, "GetBattleSpotIndexLeftToRight_(WhenFacingCharacters)");
            var orderArrays = orderFn.ScriptBytecode!
                .OfType<EX_SetArray>()
                .Select(sa => sa.Elements.OfType<EX_IntConst>().Select(e => e.Value).ToArray())
                .ToList();
            // Every entry of the *player* table must fall inside the vanilla
            // 0..2 range that ClampAngleForLookLeftRight's switch covers;
            // anything else locks that character's free-aim yaw (see
            // SpotOrderTable). The enemy table [1,2,0,3] legitimately contains
            // a 3 and is untouched.
            var expectedOrder = SpotOrderTable();
            var playerTable = orderArrays.FirstOrDefault(a => a.Length == expectedOrder.Length
                && a.Length >= 3 && a[0] == 1 && a[1] == 0 && a[2] == 2);
            if (playerTable is null || !playerTable.SequenceEqual(expectedOrder))
                throw new InvalidDataException(
                    $"player spot-order table is not [{string.Join(",", expectedOrder)}]");
            if (playerTable.Any(v => v < 0 || v > 2))
                throw new InvalidDataException("player spot-order table has a value outside 0..2");
            VerifyJumps(cbbAsset, orderFn);
        }

        // team-menu cap gates
        var menu = FileCopy.FindUnder(modDir, "WBP_GM_TeamOverview3D_Page.uasset")
            ?? throw new FileNotFoundException("staged team menu missing");
        var menuAsset = Open(menu);
        var uber = Fn(menuAsset, "ExecuteUbergraph_WBP_GM_TeamOverview3D_Page");
        int gates = 0, stale = 0;
        MayhemFixKit.Walk(uber.ScriptBytecode!, e =>
        {
            if (e is not EX_CallMath cm || cm.Parameters is not { Length: 2 }) return;
            if (cm.StackNode is null || !cm.StackNode.IsImport()) return;
            var name = Name(menuAsset, cm.StackNode);
            if (name is not ("Less_IntInt" or "GreaterEqual_IntInt")) return;
            if (cm.Parameters[1] is not EX_IntConst c) return;
            if (c.Value == s.PartySize) gates++;
            else if (c.Value == 3) stale++;
        });
        if (gates < 2 || stale > 0)
            throw new InvalidDataException($"team-menu gates: {gates} at {s.PartySize}, {stale} stale at 3");

        // on-screen home-snap debug print
        if (s.UseResetDebugPrint)
        {
            var mv = FileCopy.FindUnder(modDir, "AC_jRPG_CombatMovement.uasset")
                ?? throw new FileNotFoundException("staged CombatMovement missing");
            var mvAsset = Open(mv);
            var moveFn = Fn(mvAsset, "MoveToInitialLocation");
            var moveStmts = moveFn.ScriptBytecode!;
            int prints = 0;
            foreach (var st in moveStmts)
            {
                if (st is not EX_CallMath pcm || Name(mvAsset, pcm.StackNode) != "PrintString") continue;
                prints++;
                // A dangling local-variable reference is the classic VM crash: the
                // template function's locals do not exist in this one.
                if (MayhemFixKit.WalkHas(pcm, e => e is EX_LocalVariable))
                    throw new InvalidDataException("debug print still references a foreign local");
            }
            if (prints != 2)
                throw new InvalidDataException($"MoveToInitialLocation: {prints}/2 debug prints");
            if (moveStmts.Count(st => st is EX_LocalFinalFunction) != 1)
                throw new InvalidDataException("MoveToInitialLocation: ubergraph jump missing or duplicated");
            VerifyJumps(mvAsset, moveFn);
        }

        // multi-ally buff target count
        foreach (var stem in MultiAllyBuffScripts)
        {
            var bp = FileCopy.FindUnder(modDir, stem + ".uasset");
            if (bp is null)
            {
                if (ModOnlyMultiAllyBuffScripts.Contains(stem)) continue;
                throw new FileNotFoundException($"staged {stem} missing");
            }
            var bpAsset = Open(bp);
            var effect = Fn(bpAsset, "OnActionEffect");
            var code = effect.ScriptBytecode!;
            bool guarded = false;
            MayhemFixKit.Walk(code, e =>
            {
                if (guarded) return;
                if (e is EX_CallMath cm && Name(bpAsset, cm.StackNode) == "Min"
                    && cm.Parameters is { Length: 2 }
                    && cm.Parameters[1] is EX_CallMath sel
                    && Name(bpAsset, sel.StackNode) == "SelectInt"
                    && sel.Parameters is { Length: 3 }
                    && (sel.Parameters[0] as EX_LocalVariable) is { } l0
                    && FieldName(l0.Variable)?.StartsWith(
                        "CallFunc_Array_LastIndex_ReturnValue", StringComparison.Ordinal) == true
                    && (sel.Parameters[2] as EX_LocalVariable) is { } l2
                    && FieldName(l2.Variable)?.StartsWith(
                        "CallFunc_GetLastSkillComboInfo_OnlySuccess", StringComparison.Ordinal) == true)
                    guarded = true;
            });
            if (!guarded)
                throw new InvalidDataException($"{stem}: no-miss QTE SelectInt guard missing");
            VerifyJumps(bpAsset, effect);
        }
    }
}

using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Force-disables the tutorial system instead of trying to tune its scripted
/// fights.
///
/// Three layers:
/// <list type="bullet">
/// <item><c>BP_TutorialSystemComponent.AreTutorialsEnabled</c> is rewritten to
/// return <c>false</c>. Every tutorial card, hint, live lesson and the
/// tutorial condition checkers flow through that function.</item>
/// <item><c>DT_jRPG_Encounters</c> is repointed from the tutorial enemy rows to
/// their no-tutorial counterparts (Maelle, Curator, Lancelier, Évêque, Chalier,
/// Volester, Sciel). That guarantees the narrative fights use the plain enemy
/// blueprints even if a level action still requests the tutorial encounter
/// row.</item>
/// <item>Stalact's gradient lesson and Pelerin at Monoco's Station share a row
/// with a late-game native fight. Repointing those encounters at the native
/// rows would import their Frozen Hearts T4 randomizer band into a level-25
/// fight and change the drops. <see cref="ActorSwaps"/> therefore leaves the
/// encounter on the tutorial row and swaps only its actor class to the plain
/// blueprint: same level, stats and <c>PossibleLoot</c> as the tutorial.</item>
/// </list>
///
/// The encounter table is also edited by the randomizer/boss-venue kits, so
/// this kit exposes <see cref="ApplyEncounters"/> to be called again late in
/// the pack (after the randomizer's RestoreVanilla + BossVenueKit pass). It is
/// idempotent; on revert it repoints the no-tutorial rows back to the tutorial
/// rows instead of deleting a table another kit may own.
/// </summary>
public static class TutorialDisableKit
{
    public const string TutorialSystemAsset = "BP_TutorialSystemComponent";
    public const string EncountersTable = "DT_jRPG_Encounters";

    /// <summary>Tutorial enemy row -> the plain fight that should replace it.</summary>
    public static readonly (string Tutorial, string Normal)[] EnemyMap =
    {
        ("L_MaelleTutorial", "L_MaelleTutorial_NoTutorial"),
        ("L_MaelleTutorial_Civilian", "L_MaelleTutorial_NoTutorial_Civilian"),
        ("GO_Curator_JumpTutorial", "GO_Curator_JumpTutorial_NoTuto"),
        ("SM_FirstLancelier", "SM_FirstLancelier_NoTuto"),
        ("SM_Boss_Eveque_ShieldTutorial", "SM_Boss_Eveque"),
        ("FB_Chalier_GradientCounterTutorial", "FB_Chalier_GradientCounterTutorial_Notuto"),
        ("SM_Volester_TutoFlying", "SM_Volester"),
        ("MM_Pelerin_MonocoTuto", "MM_Pelerin"),
        ("MM_Stalact_GradientAttackTutorial", "MM_Stalact"),
        ("GV_GestralScielTuto", "QUEST_DominiqueGiantFeet"),
    };

    /// <summary>
    /// Tutorial rows whose plain counterpart lives in a different biome. The
    /// tutorial fight keeps its own row (and therefore its stats and drops);
    /// only the actor class is swapped to the native row's plain blueprint.
    /// Repointing the encounter at the native row instead would import that
    /// biome's randomizer band into the level-25 fight.
    /// </summary>
    private sealed record ActorSwap(string Tutorial, string Normal);

    private static readonly ActorSwap[] ActorSwaps =
    {
        // Monoco's Station, level 25. The native rows are Frozen Hearts
        // (level 58/74, T4 band), so using them directly inflated the fight.
        new("MM_Stalact_GradientAttackTutorial", "MM_Stalact"),
        new("MM_Pelerin_MonocoTuto", "MM_Pelerin"),
    };

    public static int Apply(string modDir)
    {
        int n = PatchTutorialSystem(modDir);
        n += ApplyEncounters(modDir);
        n += ApplyActorSwaps(modDir);
        return n;
    }

    /// <summary>Late pack pass: re-apply the encounter repoint after the
    /// randomizer's RestoreVanilla/BossVenueKit, or restore on revert.</summary>
    public static int ApplyLate(string modDir, bool disable)
    {
        if (disable) return ApplyEncounters(modDir) + ApplyActorSwaps(modDir);
        RestoreEncounters(modDir);
        return 0;
    }

    public static int PatchTutorialSystem(string modDir)
    {
        var path = CopyIntoMod(TutorialSystemAsset, modDir);
        var asset = Open(path);
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "AreTutorialsEnabled")
            ?? throw new InvalidDataException("[tutorial-disable] AreTutorialsEnabled not found");

        var stmts = fn.ScriptBytecode?.ToList()
            ?? throw new InvalidDataException("[tutorial-disable] AreTutorialsEnabled has no bytecode");
        var retLet = stmts.OfType<EX_LetBool>().FirstOrDefault(s =>
            s.VariableExpression is EX_VariableBase vb
            && PartyFiveKit.FieldName(vb.Variable) == "ReturnValue");
        var ret = stmts.FirstOrDefault(s => s is EX_Return);
        var end = stmts.FirstOrDefault(s => s is EX_EndOfScript);
        if (retLet is null || ret is null || end is null)
            throw new InvalidDataException("[tutorial-disable] AreTutorialsEnabled shape changed");

        var falseLet = (EX_LetBool)MayhemFixKit.CloneKismet(retLet);
        falseLet.AssignmentExpression = new EX_False();

        fn.ScriptBytecode = new KismetExpression[]
        {
            falseLet,
            (KismetExpression)MayhemFixKit.CloneKismet(ret),
            (KismetExpression)MayhemFixKit.CloneKismet(end),
        };
        PartyFiveKit.VerifyJumps(asset, fn);
        asset.Write(path);
        return 1;
    }

    public static bool VerifyTutorialSystem(string uassetPath)
    {
        var asset = Open(uassetPath);
        var fn = asset.Exports.OfType<FunctionExport>()
            .FirstOrDefault(f => f.ObjectName.ToString() == "AreTutorialsEnabled");
        if (fn?.ScriptBytecode is not { Length: > 0 } body) return false;
        if (body[0] is not EX_LetBool let || let.AssignmentExpression is not EX_False) return false;
        if (body[^1] is not EX_EndOfScript) return false;
        try { PartyFiveKit.VerifyJumps(asset, fn); }
        catch { return false; }
        return true;
    }

    public static int ApplyEncounters(string modDir)
    {
        var path = CopyIntoMod(EncountersTable, modDir);
        var map = EnemyMap.ToDictionary(x => x.Tutorial, x => x.Normal, StringComparer.Ordinal);
        // These two keep their own tutorial row: the actor swap below removes
        // the tutorial blueprint without touching the row's stats or drops.
        foreach (var swap in ActorSwaps) map.Remove(swap.Tutorial);

        var ed = new DataTableEditor(path);
        int changed = 0;
        foreach (var row in ed.Table.Table.Data)
        {
            if (Named(row.Value, "Enemies") is not MapPropertyData enemies) continue;
            foreach (var kv in enemies.Value)
            {
                if (kv.Value is not StructPropertyData handle) continue;
                if (Named(handle.Value, "RowName") is not NamePropertyData rowName) continue;
                var id = rowName.Value?.ToString();
                if (id is null || !map.TryGetValue(id, out var replacement)) continue;
                rowName.Value = FName.FromString(ed.Asset, replacement);
                changed++;
            }
        }
        if (changed > 0) ed.Save(path);
        return changed;
    }

    /// <summary>True when the mod already carries the force-disabled tutorial
    /// component (see <see cref="PatchTutorialSystem"/>).</summary>
    public static bool IsDisabled(string modDir)
    {
        var path = FindAsset(modDir, TutorialSystemAsset + ".uasset");
        return path is not null && VerifyTutorialSystem(path);
    }

    /// <summary>
    /// Swaps each <see cref="ActorSwaps"/> tutorial row's actor class to the
    /// native row's plain blueprint. The row itself, its level, stats and
    /// <c>PossibleLoot</c> stay untouched, so the fight is byte-for-byte the
    /// tutorial variant apart from the spawned actor. Runs after the
    /// randomizer stages/restores <c>DT_jRPG_Enemies</c>.
    /// </summary>
    public static int ApplyActorSwaps(string modDir)
    {
        var path = FindAsset(modDir, "DT_jRPG_Enemies.uasset");
        if (path is null) return 0;

        var ed = new DataTableEditor(path);
        int changed = 0;
        foreach (var swap in ActorSwaps)
        {
            var tutorial = FindRow(ed, swap.Tutorial);
            var normal = FindRow(ed, swap.Normal);
            if (tutorial is null || normal is null) continue;
            if (CopyProperty(normal.Value, tutorial.Value, "EnemyActorClass")) changed++;
            if (CopyProperty(normal.Value, tutorial.Value, "EnemyActorClassSoft")) changed++;
        }
        if (changed > 0) ed.Save(path);
        return changed;
    }

    private static StructPropertyData? FindRow(DataTableEditor ed, string id) =>
        ed.Table.Table.Data.FirstOrDefault(r =>
            DataTableEditor.CleanName(r.Name).Equals(id, StringComparison.OrdinalIgnoreCase));

    private static string? ReadSoft(StructPropertyData row, string field) =>
        Named(row.Value, field) is SoftObjectPropertyData p ? p.Value.ToString() : null;

    private static bool CopyProperty(
        IList<PropertyData> from, IList<PropertyData> to, string name)
    {
        var source = Named(from, name);
        if (source is null) return false;
        var copy = (PropertyData)source.Clone();
        for (int i = 0; i < to.Count; i++)
        {
            if (DataTableEditor.CleanName(to[i].Name) != name) continue;
            to[i] = copy;
            return true;
        }
        to.Add(copy);
        return true;
    }

    /// <summary>
    /// Post-randomizer check for <see cref="ActorSwaps"/>: each tutorial row
    /// still exists and now points at the native row's plain actor, and the
    /// tutorial encounter still references the tutorial row (so its drops are
    /// unchanged).
    /// </summary>
    public static bool VerifyActorSwaps(string modDir)
    {
        var enemiesPath = FindAsset(modDir, "DT_jRPG_Enemies.uasset");
        var encountersPath = FindAsset(modDir, EncountersTable + ".uasset");
        if (enemiesPath is null || encountersPath is null) return false;

        var enemies = new DataTableEditor(enemiesPath);
        foreach (var swap in ActorSwaps)
        {
            var tutorial = FindRow(enemies, swap.Tutorial);
            var normal = FindRow(enemies, swap.Normal);
            if (tutorial is null || normal is null) return false;
            if (ReadSoft(tutorial, "EnemyActorClassSoft") != ReadSoft(normal, "EnemyActorClassSoft"))
                return false;
        }

        var encounters = new DataTableEditor(encountersPath);
        foreach (var swap in ActorSwaps)
        {
            bool sawTutorial = false;
            foreach (var row in encounters.Table.Table.Data)
            {
                if (!DataTableEditor.CleanName(row.Name)
                        .Contains(swap.Tutorial, StringComparison.OrdinalIgnoreCase)) continue;
                if (Named(row.Value, "Enemies") is not MapPropertyData map) continue;
                foreach (var kv in map.Value)
                {
                    if (kv.Value is not StructPropertyData handle) continue;
                    var id = (Named(handle.Value, "RowName") as NamePropertyData)?
                        .Value?.ToString();
                    if (id is null) continue;
                    if (id.Equals(swap.Normal, StringComparison.OrdinalIgnoreCase)) return false;
                    if (id.Equals(swap.Tutorial, StringComparison.OrdinalIgnoreCase))
                        sawTutorial = true;
                }
            }
            if (!sawTutorial) return false;
        }
        return true;
    }

    /// <summary>
    /// Reverts only the encounter rows that vanilla pointed at a tutorial
    /// enemy. Unlike a blunt reverse map this leaves unrelated legitimate
    /// references to the normal enemy rows alone (and preserves randomizer /
    /// boss-venue edits elsewhere in the table).
    /// </summary>
    public static void RestoreEncounters(string modDir)
    {
        var staged = FindAsset(modDir, EncountersTable + ".uasset");
        if (staged is null) return;
        var vanilla = FindAsset(Path.Combine(EditorPaths.RepoRoot, "work", "tables"),
            EncountersTable + ".uasset");
        if (vanilla is null) return;

        var originals = TutorialRows(vanilla);
        if (originals.Count == 0) return;

        var ed = new DataTableEditor(staged);
        int changed = 0;
        foreach (var row in ed.Table.Table.Data)
        {
            var name = DataTableEditor.CleanName(row.Name);
            if (!originals.TryGetValue(name, out var ids)) continue;
            if (Named(row.Value, "Enemies") is not MapPropertyData enemies) continue;
            int i = 0;
            foreach (var kv in enemies.Value)
            {
                if (kv.Value is StructPropertyData handle
                    && Named(handle.Value, "RowName") is NamePropertyData rowName
                    && i < ids.Count)
                {
                    if (rowName.Value?.ToString() != ids[i])
                    {
                        rowName.Value = FName.FromString(ed.Asset, ids[i]);
                        changed++;
                    }
                }
                i++;
            }
        }
        if (changed > 0) ed.Save(staged);
    }

    public static bool VerifyEncounters(string uassetPath, bool disabled)
    {
        if (!disabled) return true;
        var tutorialIds = EnemyMap.Select(x => x.Tutorial).ToHashSet(StringComparer.Ordinal);
        var normalIds = EnemyMap.Select(x => x.Normal).ToHashSet(StringComparer.Ordinal);
        // Actor-swap rows deliberately stay on their tutorial row so their
        // stats and drops are untouched; VerifyActorSwaps checks the actor.
        var actorSwapIds = ActorSwaps.Select(x => x.Tutorial)
            .ToHashSet(StringComparer.Ordinal);

        var ed = new DataTableEditor(uassetPath);
        bool sawNormal = false;
        foreach (var row in ed.Table.Table.Data)
        {
            if (Named(row.Value, "Enemies") is not MapPropertyData enemies) continue;
            foreach (var kv in enemies.Value)
            {
                if (kv.Value is not StructPropertyData handle) continue;
                if (Named(handle.Value, "RowName") is not NamePropertyData rowName) continue;
                var id = rowName.Value?.ToString();
                if (id is null) continue;
                if (tutorialIds.Contains(id) && !actorSwapIds.Contains(id)) return false;
                if (normalIds.Contains(id)) sawNormal = true;
            }
        }
        return sawNormal;
    }

    private static Dictionary<string, List<string>> TutorialRows(string vanillaPath)
    {
        var tutorialIds = EnemyMap.Select(x => x.Tutorial).ToHashSet(StringComparer.Ordinal);
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var ed = new DataTableEditor(vanillaPath);
        foreach (var row in ed.Table.Table.Data)
        {
            if (Named(row.Value, "Enemies") is not MapPropertyData enemies) continue;
            var ids = new List<string>();
            foreach (var kv in enemies.Value)
            {
                if (kv.Value is not StructPropertyData handle) continue;
                if (Named(handle.Value, "RowName") is not NamePropertyData rowName) continue;
                var id = rowName.Value?.ToString();
                if (id is not null && tutorialIds.Contains(id)) ids.Add(id);
            }
            if (ids.Count > 0) result[DataTableEditor.CleanName(row.Name)] = ids;
        }
        return result;
    }

    private static PropertyData? Named(IList<PropertyData> data, string name) =>
        data.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == name);

    private static UAsset Open(string path)
    {
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
    }

    private static string CopyIntoMod(string asset, string modDir)
    {
        // ApplyLate() runs after RandomizerKit/BattleGroupKit. Reuse an already
        // staged encounter table instead of copying vanilla over it, or the
        // battle-group rosters are silently reverted.
        var existing = FindAsset(modDir, asset + ".uasset");
        if (existing is not null) return existing;

        var dir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "tutorial_disable");
        var src = FindAsset(dir, asset + ".uasset")
            ?? FindAsset(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), asset + ".uasset");
        if (src is null)
        {
            Directory.CreateDirectory(dir);
            using (E33ModEditor.Core.Packer.DonorExtract.SuspendMods())
                new Retoc().ToLegacy(dir, asset);
            src = FindAsset(dir, asset + ".uasset")
                ?? throw new FileNotFoundException(asset + ".uasset");
        }
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) throw new InvalidDataException(asset + " is not under Sandfall/");
        var dest = Path.Combine(modDir, src[idx..]);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    private static string? FindAsset(string root, string fileName)
    {
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).FirstOrDefault();
    }
}

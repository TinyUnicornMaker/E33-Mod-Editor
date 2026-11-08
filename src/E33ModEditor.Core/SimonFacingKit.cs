using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Simon (BP_EnemyBattle_Simon, inherited by _Phase2 and _ALPHA): keep his
/// facing aimed at the centre of the party and stand farther back before each
/// move.
///
/// <para>Vanilla picks <c>AcquireTargetRandomHero</c> for skills 1/2/3/4/6 and
/// <c>AcquireTargetWithHigherHealth</c> for LifeTaker. With the five-member
/// formation the random pick can be either flank, so Simon turns sharply
/// between moves (reported as "phase 2 facing the wrong way"). Skill 7 already
/// uses <c>GetMiddleHeroForFeedback</c> for exactly this reason. Point the
/// rest of the rotation at the same helper: its one-out-parameter call shape
/// is identical, so this is a name edit only and no bytecode offsets move.</para>
///
/// <para>The distance argument of <c>StartMoveToControlledBySequence</c> is
/// raised by <see cref="DistanceBonus"/>, so he stops farther from the party.
/// The cap keeps Skill 7 (already 550) the farthest move instead of turning
/// it into an outlier. Both phases inherit this blueprint, so the phase 1,
/// phase 2 and Star Simon variants all pick up the change.</para>
/// </summary>
public static class SimonFacingKit
{
    public const string BpStem = "BP_EnemyBattle_Simon";
    public const string BpRelDir =
        "Sandfall/Content/Characters/Enemies/HumanEnnemies/Simon";

    /// <summary>Extra units added to every <c>StartMoveToControlledBySequence</c>
    /// distance. Vanilla stops 200–550 units from the target; the five-member
    /// formation needs more room, but not so much that the authored attack
    /// sequences stop meeting the party.</summary>
    public const double DistanceBonus = 150;

    /// <summary>Upper bound after the bonus. Skill 7 already uses 550; capping
    /// at 650 keeps the spread tight enough for the melee sequences.</summary>
    public const double MaxDistance = 650;

    public static int Apply(string modDir, Action<string>? log = null)
    {
        var src = FileCopy.FindTablesAsset(BpStem + ".uasset");
        if (src is null)
        {
            log?.Invoke($"[simon-facing] source extract missing ({BpStem}.uasset); skipped");
            return 0;
        }

        var dest = Path.Combine(modDir, BpRelDir, BpStem + ".uasset");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);

        var map = DataTableEditor.Mappings;
        var asset = map != null
            ? new UAsset(dest, EngineVersion.VER_UE5_4, map)
            : new UAsset(dest, EngineVersion.VER_UE5_4);

        int centred = 0, moved = 0, skipped = 0;
        var touched = new List<FunctionExport>();
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ScriptBytecode is null) continue;

            var middleName = FindExistingMiddleHeroName(fn)
                ?? FName.FromString(asset, "GetMiddleHeroForFeedback");
            bool changed = false;

            MayhemFixKit.Walk(fn.ScriptBytecode, e =>
            {
                if (e is not EX_VirtualFunction call) return;
                var name = call.VirtualFunctionName.ToString();

                // The random and highest-health picks both feed the same
                // one-object out local used by StartMoveTo / TargetCharacters.
                if (name is "AcquireTargetRandomHero" or "AcquireTargetWithHigherHealth")
                {
                    call.VirtualFunctionName = middleName;
                    centred++;
                    changed = true;
                    return;
                }

                if (name == "StartMoveToControlledBySequence")
                {
                    switch (IncreaseDistance(fn, call))
                    {
                        case > 0: moved++; changed = true; break;
                        case < 0: skipped++; break;
                    }
                }
            });

            if (changed) touched.Add(fn);
        }

        if (centred == 0 && moved == 0)
        {
            log?.Invoke($"[simon-facing] {BpStem}: no vanilla target/move calls found; skipped");
            return 0;
        }

        // Name and constant edits keep every expression the same size, but
        // recalc the context skip table for the functions we rewrote so a later
        // UAssetAPI serialisation can never carry a stale relative offset.
        foreach (var fn in touched)
            MayhemFixKit.RecalcContextSkips(asset, fn);
        asset.Write(dest);

        var detail = $"{centred} target call(s) centred, {moved} move distance(s) +{DistanceBonus:0}";
        if (skipped > 0) detail += $", {skipped} distance(s) left vanilla (unknown shape)";
        log?.Invoke($"[simon-facing] {BpStem}: {detail}");
        return centred + moved;
    }

    /// <summary>Reuse the FName already used by Skill 7 so the name map does
    /// not gain a second spelling of the same function.</summary>
    private static FName? FindExistingMiddleHeroName(FunctionExport fn)
    {
        FName? found = null;
        MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (found is null
                && e is EX_VirtualFunction vf
                && vf.VirtualFunctionName.ToString() == "GetMiddleHeroForFeedback")
                found = vf.VirtualFunctionName;
        });
        return found;
    }

    /// <summary>
    /// +<see cref="DistanceBonus"/> on parameter 1 of StartMoveTo. The value is
    /// either a literal DoubleConst or the phase-dependent SelectFloat local
    /// (500 for phase &gt;= 2, 300 otherwise). Returns 1 when changed, -1 when
    /// the shape is unknown (left untouched, logged), 0 when already capped.
    /// </summary>
    private static int IncreaseDistance(FunctionExport fn, EX_VirtualFunction call)
    {
        if (call.Parameters is not { Length: 2 }) return -1;

        if (call.Parameters[1] is EX_DoubleConst dc)
        {
            var next = Math.Min(dc.Value + DistanceBonus, MaxDistance);
            if (next <= dc.Value) return 0;
            dc.Value = next;
            return 1;
        }

        if (call.Parameters[1] is not EX_LocalVariable lv) return -1;
        var localName = PartyFiveKit.FieldName(lv.Variable);
        if (string.IsNullOrEmpty(localName)) return -1;

        // Find Let <localName> = SelectFloat(high, low, condition) in the same
        // function and raise BOTH constants, preserving the phase switch.
        EX_Let? holder = null;
        MayhemFixKit.Walk(fn.ScriptBytecode!, e =>
        {
            if (holder is not null) return;
            if (e is EX_Let let
                && let.Variable is EX_LocalVariable target
                && PartyFiveKit.FieldName(target.Variable) == localName)
                holder = let;
        });
        if (holder?.Expression is not EX_CallMath cm
            || cm.Parameters is not { Length: 3 })
            return -1;

        int changed = 0;
        foreach (var p in cm.Parameters.Take(2))
        {
            if (p is not EX_DoubleConst d) continue;
            var next = Math.Min(d.Value + DistanceBonus, MaxDistance);
            if (next > d.Value) { d.Value = next; changed++; }
        }
        return changed > 0 ? 1 : 0;
    }
}

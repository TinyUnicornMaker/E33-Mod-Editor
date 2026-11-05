using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Global combat rebalance (always-on, all characters):
///  - Base attack: AC_jRPG_ActionsManager.Attack builds the free attack's
///    per-hit ratio as Divide(0.5, Add(Conv_IntToDouble(BaseAttackNumber), 1)),
///    i.e. 0.5/(hits) so any combo count totals 0.5x base. This halves basic
///    attacks only — hero/enemy skills build damage directly via
///    MakeDamageBuilder + DealDamages and never call Attack(), so skills are
///    untouched (verified on Lune Immolation/Hell: zero *Attack* calls).
///    Enemies roll pure tier bands on all stats (no flat attack multiplier).
///    PatchBaseAttackRatio normalizes any leftover 1.0 copies (it matches 1.0
///    and 0.5 numerators).
///  - Standard counters (melee + ranged): the DT_Balancing_MainCharacterSkills
///    rows Generic_MeleeCounter_BaseDamage / Generic_RangeCounter_BaseDamage
    ///    drop 2.5 → 0.75x of base = 0.75 × the 0.5x basic attack (still
    ///    × (1 + (level−1)·0.04) per level).
///  - Maelle stance rows (Maelle overhaul): Defensive received 0.5 (vanilla),
///    Offensive dealt 1.5 (vanilla), Virtuous dealt 3.0 → 2.0. Stanceless is
///    vanilla. Extra BP handlers are StanceEffectsKit.
/// AoE / Jump / Gradient counters keep their distinct designs.
/// </summary>
public static class CombatBalanceKit
{
    private static readonly HashSet<string> Copied = new(StringComparer.Ordinal);

    public const string SkillBalTable = "DT_Balancing_MainCharacterSkills";
    public const double BaseAttackRatio = 0.5;
    public const double CounterBaseDamage = 0.75;
    public const double CounterDamagePerLevel = 0.04;

    public static int Apply(string modDir, bool maelleOn = true)
    {
        int n = 0;
        n += PatchBaseAttackRatio(modDir);
        n += PatchCounterRows(modDir);
        // Maelle stance rows + descriptions follow the Skill Overhaul character
        // gate (virtuose dealt is the only row that differs from vanilla; the
        // revert itself is a spec pin in SkillOverhaulKit).
        if (maelleOn)
        {
            n += PatchStanceRows(modDir);
            n += PatchStanceDescriptions(modDir);
        }
        return n;
    }

    /// <summary>
    /// Updates the Maelle stance descriptions in ST_UniqueMechanics (the
    /// WBP_StanceMechanicComponent reads UNIQUEMECHANIC_Maelle_*_Description).
    /// Kept concise and in the vanilla voice.
    /// </summary>
    private static int PatchStanceDescriptions(string modDir)
    {
        const string stem = "ST_UniqueMechanics.uasset";
        var existing = Directory.EnumerateFiles(modDir, stem, SearchOption.AllDirectories).FirstOrDefault();
        string dest;
        if (existing is not null)
        {
            dest = existing;
        }
        else
        {
            var src = FileCopy.FindUnder(Path.Combine(EditorPaths.RepoRoot, "work", "tables"), stem);
            if (src is null) return 0;
            var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
            if (idx < 0) return 0;
            dest = Path.Combine(modDir, src[idx..]);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            FileCopy.AssetPair(src, dest);
        }

        var entries = new Dictionary<string, string>
        {
            ["UNIQUEMECHANIC_Maelle_Virtuose_Description"] =
                "+100% damage dealt. Gain 1 Berserk at the start of each turn.",
            ["UNIQUEMECHANIC_Maelle_Defensive_Description"] =
                "-50% damage taken. Gain 1 AP per Parry.",
            ["UNIQUEMECHANIC_Maelle_Offensive_Description"] =
                "+50% damage dealt.",
            ["UNIQUEMECHANIC_Maelle_Stanceless_Description"] =
                "No stance bonuses.",
        };
        DataTableEditor.AddStringTableEntries(dest, entries);
        return entries.Count;
    }

    /// <summary>
    /// Maelle stance damage values (DT_Balancing_MainCharacterSkills rows read by
    /// the stance buff BPs). Data-only: the buffs already exist and read these rows.
    /// User target (Maelle overhaul):
    ///  - Defensive: taken ×0.5 (vanilla) + 1 AP per parry (vanilla hook).
    ///  - Offensive: dealt ×1.5 (vanilla).
    ///  - Virtuous: dealt ×2.0 (vanilla 3.0 → 2.0), +1 Berserk/turn (StanceEffectsKit).
    /// The old rework rows (Offensive 2.5/2.5, Defensive 0.4, Virtuose 1.5 and its
    /// cloned received 0.5) are gone — the Virtuose received row is never created.
    /// </summary>
    private static int PatchStanceRows(string modDir)
    {
        var dest = CopyIntoMod(SkillBalTable, modDir);
        if (dest is null) return 0;

        var ed = new DataTableEditor(dest);
        var edits = new JObject
        {
            ["Maelle_DefensiveStance_DMGReceivedMultiplier"] = new JObject { ["FloatValue"] = 0.5f },
            ["Maelle_OffensiveStance_DMGDealtMultiplier"] = new JObject { ["FloatValue"] = 1.5f },
            ["Maelle_OffensiveStance_DMGReceivedMultiplier"] = new JObject { ["FloatValue"] = 1.5f },
            ["Maelle_VirtuoseStance_DMGDealtMultiplier"] = new JObject { ["FloatValue"] = 2.0f },
        };
        int n = ed.Apply(edits.ToString(Formatting.None));
        ed.Save(dest);
        return n;
    }

    /// <summary>
    /// Normalizes the base attack damage ratio to <see cref="BaseAttackRatio"/>
    /// (0.5: basics total half base; skills bypass Attack() and are unaffected).
    /// Matches both vanilla 1.0 and leftover 0.5 numerators so already-packed
    /// copies heal on the next pack.
    /// </summary>
    private static int PatchBaseAttackRatio(string modDir)
    {
        var dest = CopyIntoMod("AC_jRPG_ActionsManager", modDir);
        if (dest is null) return 0;

        var map = DataTableEditor.Mappings;
        var asset = map != null ? new UAsset(dest, EngineVersion.VER_UE5_4, map) : new UAsset(dest, EngineVersion.VER_UE5_4);
        int n = 0;
        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            if (fn.ObjectName.ToString() != "Attack" || fn.ScriptBytecode is null) continue;
            bool changed = false;
            MayhemFixKit.Walk(fn.ScriptBytecode, e =>
            {
                if (changed || e is not EX_FinalFunction ff) return;
                if (ff.StackNode.IsNull() || !ff.StackNode.IsImport()) return;
                var name = asset.Imports[Math.Abs(ff.StackNode.Index) - 1].ObjectName.ToString();
                if (name != "Divide_DoubleDouble") return;
                if (ff.Parameters is not { Length: >= 2 } ps) return;
                // ratio = Divide(1, Add(Conv_IntToDouble(BaseAttackNumber), 1))
                if (ps[0] is EX_DoubleConst dc && (dc.Value == 1.0 || dc.Value == 0.5) && ps[1] is EX_LocalVariable)
                {
                    if (dc.Value != BaseAttackRatio) dc.Value = BaseAttackRatio;
                    changed = true;
                }
            });
            if (changed)
            {
                MayhemFixKit.RecalcContextSkips(asset, fn);
                n++;
            }
        }
        if (n > 0) WriteAsset(asset, dest);
        return n;
    }

    /// <summary>Melee + ranged counter rows 2.5 → 0.75 in the shared balancing table; per-level scaling 0.02 → 0.04.</summary>
    private static int PatchCounterRows(string modDir)
    {
        var dest = CopyIntoMod(SkillBalTable, modDir);
        if (dest is null) return 0;

        var edits = new JObject
        {
            ["Generic_MeleeCounter_BaseDamage"] = new JObject { ["FloatValue"] = CounterBaseDamage },
            ["Generic_RangeCounter_BaseDamage"] = new JObject { ["FloatValue"] = CounterBaseDamage },
            ["Generic_CounterDamageIncreasePerLevel"] = new JObject { ["FloatValue"] = CounterDamagePerLevel },
        };
        var ed = new DataTableEditor(dest);
        int n = ed.Apply(edits.ToString(Formatting.None));
        ed.Save(dest);
        return n;
    }

    private static string? CopyIntoMod(string stem, string modDir)
    {
        var src = FileCopy.FindTablesAsset(stem + ".uasset");
        if (src is null)
        {
            var tmpDir = Path.Combine(EditorPaths.RepoRoot, "work", "tables", "combat_balance");
            Directory.CreateDirectory(tmpDir);
            try { new Retoc().ToLegacy(tmpDir, stem); } catch { }
            src = FileCopy.FindTablesAsset(stem + ".uasset") ?? FileCopy.FindUnder(tmpDir, stem + ".uasset");
        }
        if (src is null) return null;
        var idx = src.IndexOf("Sandfall", StringComparison.Ordinal);
        if (idx < 0) return null;
        var dest = Path.Combine(modDir, src[idx..]);
        // Reuse an already-staged copy (LuneSkillsKit clones rows into this table before
        // this kit runs); only stage from vanilla when the asset is not in the mod yet.
        if (File.Exists(dest)) return dest;
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);
        return dest;
    }

    private static void WriteAsset(UAsset asset, string path)
    {
        try { asset.Write(path); }
        catch (FormatException ex) { throw new FormatException(Path.GetFileName(path) + ": " + ex.Message, ex); }
    }
}

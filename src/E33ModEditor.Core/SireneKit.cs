using UAssetAPI;
using UAssetAPI.UnrealTypes;

namespace E33ModEditor.Core;

/// <summary>
/// Sirène (Axon heart, BP_EnemyBattle_SireneHeart): keep her Tisseur-synergy
/// move in the rotation even after the Tisseur battle has been won.
///
/// GetShouldTisseurHelp() returns
/// <c>!HasBattledEnemy("LD_Battle_Tisseur") &amp;&amp; ShouldUseTisseurHelpSkill</c>,
/// so once the player has defeated Tisseur the check is permanently false and
/// Skill_TisseurHeal (balancing row SI_Axon_Sirene_TisseurShield, which just
/// calls AddShieldPoints on Sirène herself) is dropped from her moveset.
/// Re-pointing the LD_Battle_Tisseur name at an id nothing can ever mark as
/// battled makes HasBattledEnemy always miss, so her full moveset survives
/// clearing Tisseur. The edit is a name-map rename only: no kismet statement is
/// inserted, deleted or resized, so every bytecode offset is untouched.
/// </summary>
public static class SireneKit
{
    public const string BpStem = "BP_EnemyBattle_SireneHeart";
    public const string BpRelDir = "Sandfall/Content/Characters/Enemies/Axons/Sirene";

    /// <summary>EnemyGlobalID the boss checks to decide whether Tisseur is already dead.</summary>
    public const string TisseurFlag = "LD_Battle_Tisseur";

    /// <summary>Replacement id nothing can ever mark as battled, so the lookup always misses.</summary>
    public const string AlwaysAliveFlag = "NID_TisseurNeverDefeated";

    public static int Apply(string modDir, bool enabled = true)
    {
        if (!enabled)
        {
            Console.WriteLine("[sirene] off (Gameplay Tweaks ▸ Sirène full moveset)");
            return 0;
        }

        var src = FileCopy.FindTablesAsset(BpStem + ".uasset");
        if (src is null)
        {
            Console.WriteLine($"[sirene] source extract missing ({BpStem}.uasset); skipped");
            return 0;
        }

        var dest = Path.Combine(modDir, BpRelDir, BpStem + ".uasset");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        FileCopy.AssetPair(src, dest);

        var bp = Open(dest);
        var names = bp.GetNameMapIndexList();
        int renamed = 0;
        for (int i = 0; i < names.Count; i++)
        {
            var s = names[i]?.Value;
            if (string.IsNullOrEmpty(s) || !s.Contains(TisseurFlag, StringComparison.Ordinal)) continue;
            bp.SetNameReference(i, FString.FromString(s.Replace(TisseurFlag, AlwaysAliveFlag, StringComparison.Ordinal)));
            renamed++;
        }
        bp.Write(dest);

        Console.WriteLine(renamed > 0
            ? $"[sirene] {BpStem}: {renamed} Tisseur-defeat reference(s) re-pointed at {AlwaysAliveFlag} — full Sirène moveset kept"
            : $"[sirene] {BpStem}: no Tisseur-defeat references found");
        return renamed > 0 ? 1 : 0;
    }

    private static UAsset Open(string path)
    {
        var map = DataTableEditor.Mappings;
        return map != null
            ? new UAsset(path, EngineVersion.VER_UE5_4, map)
            : new UAsset(path, EngineVersion.VER_UE5_4);
    }
}

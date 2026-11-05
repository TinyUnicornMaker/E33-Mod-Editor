using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;

namespace E33ModEditor.Core.Loot;

/// <summary>
/// Writes the design fight level into <c>S_jRPG_Encounter.EncounterLevelOverride</c>
/// for story/venue rows whose intended level differs from the area level.
///
/// The runtime builds stats from <c>CurrentBattleEncounterLevel</c> (the
/// encounter override, else the area/team level) — never from
/// <c>DT_jRPG_Enemies.Level</c>. A row's curated level in
/// <c>enemy_data.json</c> only drives the mod's target maths, so fights whose
/// design level is above/below their area (Alicia 69 in a level-58 Reacher,
/// Clea 99 in level-83 Flying Manor, the Act 3 finale pins derived at 99 in
/// level-48 Lumiere, the L79 Floating Cemetery Chaliers) land on the wrong
/// archetype base unless the encounter itself carries the level.
///
/// Runs before <see cref="BossVenueKit.Build"/> so venue clones are created at
/// the corrected level (e.g. the level-50 world-map Grosse Tete).
/// </summary>
public static class StoryLevelKit
{
    private static readonly string[] Tables =
        { "DT_jRPG_Encounters", "DT_jRPG_Encounters_CleaTower", "DT_WorldMap_Encounters" };

    /// <summary>Encounter row -> design fight level.</summary>
    public static readonly Dictionary<string, int> Overrides =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // The Reacher's main map is level 58; the design fight is 69.
            ["RC_Alicia"] = 69,
            // Flying Manor is level 83; Clea is designed at 99.
            ["CFH_Boss_Clea"] = 99,
            // Act 3 Lumière is level 48, but the finale trio is designed at 99
            // (their absolute HP pins were derived there).
            ["L_Creation"] = 99,
            ["L_Creation*1_Aberration*2"] = 99,
            ["L_Aberration*2_Creation*1"] = 99,
            // L_Creation is reused by the level-96 Tower floor. Its single
            // scaling is derived for the level-99 story target, so pin the
            // Tower encounter to 99 as well instead of leaving 0.89x HP there.
            ["TowerBattle_32"] = 99,
            ["L_Boss_Curator_P1"] = 99,
            // Echassier's 900M HP pin is derived at its level-48 spawn level.
            // Pin the encounter so a later level change cannot turn the same
            // scaling into a level-99 ~13.27B HP fight.
            ["LU_EchassierAlpha*1"] = 48,
            // Floating Cemetery white Chalier (T4 curve, design level 79).
            ["FC_ChalierSword*1"] = 79,
            ["FC_ChalierHammer*1"] = 79,
            ["FC_ChalierSpear*1"] = 79,
            // World-map Grosse Tete: design boss level 50 (was 45).
            ["WM_GrossTete"] = 50,
            // Approved high-ratio venues: re-pin the encounter to the design
            // level so the boss lands on its documented curve target.
            ["WM_Goblu_Alpha"] = 65,
            ["DarkGestralArena_Round1"] = 65,
            ["DarkGestralArena_Round2"] = 65,
            ["DarkGestralArena_Round3"] = 65,
            ["WM_Serpenphare"] = 75,
            ["WM_BenisseurAlpha"] = 71,
            ["WM_PetankAlpha"] = 81,
            ["MF_BoucheclierALPHA"] = 61,
            ["WM_Sprong"] = 65,
            ["CW_Boss_LampmasterAlpha"] = 86,
            ["ML_Gargant"] = 41,
            ["MM_Gargant"] = 61,
        };

    public static int Apply(string modDir)
    {
        int changed = 0;
        foreach (var table in Tables)
        {
            var path = PictosCreateKit.FindAsset(modDir, table + ".uasset") ?? Stage(modDir, table);
            if (path is null) continue;

            var ed = new DataTableEditor(path);
            bool dirty = false;
            foreach (var row in ed.Table.Table.Data)
            {
                var id = DataTableEditor.CleanName(row.Name);
                if (!Overrides.TryGetValue(id, out var level) || level <= 0) continue;
                if (Named(row.Value, "EncounterLevelOverride") is not IntPropertyData over) continue;
                if (over.Value == level) continue;
                over.Value = level;
                dirty = true;
                changed++;
            }
            if (dirty) ed.Save(path);
        }
        if (changed > 0)
            Console.WriteLine($"[story-levels] wrote {changed} encounter level override(s)");
        return changed;
    }

    private static string? Stage(string modDir, string table)
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

    private static PropertyData? Named(IList<PropertyData> props, string name) =>
        props.FirstOrDefault(p => DataTableEditor.CleanName(p.Name) == name);
}

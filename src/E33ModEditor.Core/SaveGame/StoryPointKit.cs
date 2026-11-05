using Newtonsoft.Json.Linq;

namespace E33ModEditor.Core.SaveGame;

/// <summary>
/// Rebuilds a save's main-story state for a chapter checkpoint. The golden-path
/// quest stores one objective per story chapter, so a checkpoint is mostly a
/// question of writing those enum values, aligning the forced-camp quest and
/// re-opening the level triggers that belong to the chapter.
///
/// The save format is not a scripted state machine: individual set-pieces also
/// keep their own flags (boss rows in BattledEnemies, object ids in
/// InteractedObjects / GPE_States, cinematics, named GUID flags). This kit
/// covers the main spine and the named Monoco/Stalact markers; side content
/// keeps whatever state the donor save had.
/// </summary>
public static class StoryPointKit
{
    public sealed record Chapter(
        int Index,
        string Key,
        string Name,
        string LevelAsset,
        string SpawnTag,
        string LevelRow,
        string[] Party);

    /// <summary>
    /// The 17 Main_GoldenPath objectives, in order. Spawns are the level's
    /// natural entry flag; later chapters (11+) are best-effort because the
    /// golden path does not name a single sub-area.
    /// </summary>
    public static readonly Chapter[] Chapters =
    {
        new(1, "prologue", "Lumiere Beginning", "Level_Lumiere_Main_V2",
            "Level.SpawnPoint.LumiereAct01.Entry", "Lumiere", new[] { "Frey", "Lune" }),
        new(2, "spring-meadows", "Spring Meadow", "Level_SpringMeadows_Main_V2",
            "Level.SpawnPoint.SpringMeadows.Entry", "SpringMeadows",
            new[] { "Frey", "Lune", "Maelle" }),
        new(3, "goblu", "Flying Waters", "Level_Goblu_Main_V5",
            "Level.SpawnPoint.Goblu.Entry", "GoblusLair",
            new[] { "Frey", "Lune", "Maelle" }),
        new(4, "ancient-sanctuary", "Ancient Sanctuary", "Level_AncientSanctuary_Main_V2",
            "Level.SpawnPoint.AncientSanctuary.Entry", "AncientSanctuary",
            new[] { "Frey", "Lune", "Maelle" }),
        new(5, "gestral-village", "Gestral Village", "Level_Main_GestralVillage_V2",
            "Level.SpawnPoint.GestralVillage.Entry", "GestralVillage",
            new[] { "Frey", "Lune", "Maelle" }),
        new(6, "esquie-nest", "Esquie's Nest", "LevelMain_EsquieNest",
            "Level.SpawnPoint.EsquieNest.Entry", "EsquieNest",
            new[] { "Frey", "Lune", "Maelle" }),
        new(7, "stone-wave-cliffs", "Stone Wave Cliffs", "Level_SeaCliff_Main_V2",
            "Level.SpawnPoint.SeaCliff.Entry", "SeaCliff",
            new[] { "Verso", "Lune", "Maelle", "Sciel" }),
        new(8, "forgotten-battlefield", "Forgotten Battlefield",
            "Level_Main_ForgottenBattlefield_V2",
            "Level.SpawnPoint.ForgottenBattlefield.Entry", "ForgottenBattlefield",
            new[] { "Verso", "Lune", "Maelle", "Sciel" }),
        new(9, "monoco-station", "Monoco's Station", "Level_MonocoStation",
            "Level.SpawnPoint.MonocoStation.InsideStation", "MonocoStation",
            new[] { "Lune", "Maelle", "Sciel", "Verso" }),
        new(10, "old-lumiere", "Old Lumiere", "Level_OldLumiere_Main",
            "Level.SpawnPoint.OldLumiere.Entry", "OldLumiere",
            new[] { "Lune", "Maelle", "Sciel", "Verso" }),
        new(11, "axon-1", "Axon 1", "Level_Visages_Main_V1",
            "Level.SpawnPoint.Visages.Entry", "Visages",
            new[] { "Lune", "Maelle", "Sciel", "Verso", "Monoco" }),
        new(12, "axon-2", "Axon 2", "Level_Sirene_Main_V2",
            "Level.SpawnPoint.Sirene.Entry", "Sirene",
            new[] { "Lune", "Maelle", "Sciel", "Verso", "Monoco" }),
        new(13, "monolith", "Enter The Monolith", "Level_PaintressIntro_Main",
            "Level.SpawnPoint.MonolithInterior.PaintressIntro.Entry",
            "Monolith_Interior_PaintressIntro",
            new[] { "Lune", "Maelle", "Sciel", "Verso", "Monoco" }),
        new(14, "paintress", "Defeat The Paintress", "Level_Monolith_Exterior_Peak_Main",
            "Level.SpawnPoint.MonolithExterior.Peak.Entry", "Monolith_Exterior_Peak",
            new[] { "Lune", "Maelle", "Sciel", "Verso", "Monoco" }),
        new(15, "return-to-lumiere", "Return To Lumiere", "Level_Lumiere_Main_V2",
            "Level.SpawnPoint.LumiereAct03.Entry", "Lumiere",
            new[] { "Lune", "Maelle", "Sciel", "Verso", "Monoco" }),
        new(16, "renoir", "Defeat Renoir", "Level_Lumiere_Main_V2",
            "Level.SpawnPoint.LumiereAct03.Entry", "Lumiere",
            new[] { "Lune", "Maelle", "Sciel", "Verso", "Monoco" }),
        new(17, "renoir-defeated", "Renoir Defeated", "Level_Lumiere_Main_V2",
            "Level.SpawnPoint.LumiereAct03.PostEnding", "Lumiere",
            new[] { "Lune", "Maelle", "Sciel", "Verso", "Monoco" }),
    };

    public static Chapter? Find(string value)
    {
        if (int.TryParse(value, out var index)) return Chapters.FirstOrDefault(c => c.Index == index);
        return Chapters.FirstOrDefault(c => c.Key.Equals(value, StringComparison.OrdinalIgnoreCase))
               ?? Chapters.FirstOrDefault(c => c.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Writes the chapter's quest state, location, party and the named
    /// Monoco/Stalact markers. Character records and gear are left to the caller.</summary>
    public static void Apply(GvasFile file, Chapter chapter, Action<string>? log = null)
    {
        // Main_GoldenPath: completed before the chapter, active at it, unstarted after.
        var golden = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 1; i <= 17; i++)
            golden[i.ToString()] = i < chapter.Index ? 2 : i == chapter.Index ? 1 : 0;
        SaveGameEdits.SetQuestStatus(file, "Main_GoldenPath", 1, golden);

        // Forced camps are a parallel quest tied to the same story beats.
        var campsDone = CampsDone(chapter.Index);
        var camps = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 1; i <= 10; i++)
            camps[i.ToString()] = i <= campsDone ? 2 : 0;
        SaveGameEdits.SetQuestStatus(file, "Main_ForcedCamps", 1, camps);

        // Challenge modifiers (enemy life ×100, damage limit, …) belong to the
        // donor's playthrough, not to a fresh story-point test save.
        var challenges = SaveGameEdits.ClearActivatedChallenges(file);
        if (challenges > 0)
            log?.Invoke($"[story] cleared {challenges} activated challenge modifier(s)");

        if (chapter.Index <= 9)
            ResetMonocoStation(file, log);

        if (chapter.Index <= 9 && SaveGameEdits.RemoveCharacter(file, "Monoco"))
            log?.Invoke("[story] removed Monoco's character record (he has not joined yet)");

        SaveGameEdits.SetSpawn(file, chapter.SpawnTag, chapter.LevelAsset);
        SaveGameEdits.EnsureVisitedLevel(file, chapter.LevelRow);
        SaveGameEdits.UnlockSpawnPoint(file, chapter.LevelAsset, chapter.SpawnTag);
        log?.Invoke($"[story] {chapter.Name}: {chapter.LevelAsset} @ {chapter.SpawnTag}");

        // A donor from earlier in the run may not have every roster record yet
        // (Sciel/Verso at chapter 9, or Gustave in a post-game donor). Clone a
        // fresh record with that character's starter skill instead of silently
        // dropping them from the party.
        var present = SaveGameEdits.Characters(file).Select(c => c.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var id in chapter.Party.Where(id => !present.Contains(id)))
        {
            var skill = "Combo1_" + (id.Equals("Frey", StringComparison.OrdinalIgnoreCase)
                ? "Gustave" : id);
            if (SaveGameEdits.EnsureCharacterRecord(file, id, skill))
            {
                present.Add(id);
                log?.Invoke($"[story] added missing roster record {id}");
            }
        }

        var party = chapter.Party.Where(present.Contains).ToList();
        if (party.Count == 0) party = SaveGameEdits.Party(file).Select(p => p.Id).ToList();
        if (party.Count > 0)
        {
            SaveGameEdits.SetParty(file, party);
            var world = file.Properties.FirstOrDefault(p =>
                p.Name.StartsWith("CurrentCharacterWorld", StringComparison.Ordinal));
            if (world is not null) world.Value.Payload = party[0];
            log?.Invoke("[story] party: " + string.Join(", ", party));
        }
    }

    /// <summary>Levels every character currently in the save (or the given ids).</summary>
    public static void SetCharacterLevels(GvasFile file, int level,
        IReadOnlyCollection<string>? only = null, Action<string>? log = null)
    {
        var ids = only ?? SaveGameEdits.Characters(file).Select(c => c.Id).ToList();
        foreach (var id in ids)
            SaveGameEdits.EditCharacter(file, id, c =>
            {
                c.Level = level;
                c.CurrentExperience = 0;
            });
        log?.Invoke($"[story] set {ids.Count} character(s) to level {level}");
    }

    /// <summary>
    /// Grants every current weapon and pictos from the editor's dumped tables.
    /// Weapons use <paramref name="weaponLevel"/>, pictos use
    /// <paramref name="pictosLevel"/>, and pictos are marked mastered.
    /// </summary>
    public static void GrantAllGear(GvasFile file, int weaponLevel, int pictosLevel,
        bool masterPictos = true, Action<string>? log = null)
    {
        var repo = EditorPaths.RepoRoot;
        var weaponsPath = Path.Combine(repo, "work", "current-weapons.json");
        var itemsPath = Path.Combine(repo, "work", "items.json");
        var tiersPath = Path.Combine(repo, "work", "item_tiers.json");
        if (!File.Exists(weaponsPath) || !File.Exists(itemsPath))
        {
            log?.Invoke("[story] gear catalogs missing (work/current-weapons.json + work/items.json); skipped");
            return;
        }

        var weapons = JObject.Parse(File.ReadAllText(weaponsPath)).Properties()
            .Select(p => p.Name)
            .Where(IsRealId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var doNotUse = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(tiersPath))
            foreach (var p in JObject.Parse(File.ReadAllText(tiersPath))
                         .SelectToken("Pictos.DoNotUse") as JArray ?? new JArray())
                if (p.Type == JTokenType.String) doNotUse.Add(p.ToString());

        var pictos = JObject.Parse(File.ReadAllText(itemsPath)).Properties()
            .Where(p => p.Value?["Item_Type"]?.ToString() == "NewEnumerator10")
            .Select(p => p.Name)
            .Where(id => IsRealId(id) && !doNotUse.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var id in weapons)
        {
            SaveGameEditor.SetItemOwned(file, id, true);
            SaveGameEditor.SetItemLevel(file, id, weaponLevel, 333);
        }
        foreach (var id in pictos)
        {
            SaveGameEditor.SetPictosFound(file, id, true);
            if (masterPictos) SaveGameEditor.SetPictosMastered(file, id, true);
            SaveGameEditor.SetPictosLevel(file, id, pictosLevel, 333);
        }
        log?.Invoke($"[story] granted {weapons.Count} weapons at level {weaponLevel} and " +
                    $"{pictos.Count} pictos at level {pictosLevel}");
    }

    private static bool IsRealId(string id) =>
        !id.StartsWith("Debug", StringComparison.OrdinalIgnoreCase)
        && !id.StartsWith("GDC_", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("Placeholder", StringComparison.OrdinalIgnoreCase)
        && !id.StartsWith("TEST_", StringComparison.OrdinalIgnoreCase);

    private static int CampsDone(int chapter) => chapter switch
    {
        <= 2 => 1,
        3 => 2,
        4 => 2,
        5 => 3,
        6 => 4,
        7 => 5,
        8 => 6,
        9 => 6,
        10 => 7,
        11 => 8,
        12 => 9,
        13 or 14 => 9,
        _ => 10,
    };

    /// <summary>
    /// Removes the two Monoco's Station set-pieces from the save: the duel and
    /// the Stalact fight, plus the station trigger zones that would otherwise
    /// short-circuit their cutscenes. Also clears the Monoco relationship and
    /// the Stalact side-quest objective.
    /// </summary>
    private static void ResetMonocoStation(GvasFile file, Action<string>? log)
    {
        var cleared = 0;
        foreach (var map in new[] { "BattledEnemies", "EncounteredEnemies", "TransientBattledEnemies" })
        {
            foreach (var key in new[] { "LD_Battle_Monoco", "LD_Battle_Stalact", "QUEST_MonocoDuel" })
                cleared += SaveGameEdits.SetMapFlagsContaining(file, map, key, false);
            // MonocosMountain is the post-station optional area; its world
            // enemies cannot have been fought yet at this checkpoint.
            cleared += SaveGameEdits.SetMapFlagsContaining(file, map, "MonocosMountain", false);
        }
        cleared += SaveGameEdits.SetMapFlagsContaining(file, "InteractedObjects",
            "Level_MonocoStation", false);
        cleared += SaveGameEdits.SetMapFlagsContaining(file, "TransientInteractedObjects",
            "Level_MonocoStation", false);
        foreach (var map in new[] { "InteractedTutorials", "InteractedCinematics" })
        {
            cleared += SaveGameEdits.SetMapFlagsContaining(file, map, "Monoco", false);
            cleared += SaveGameEdits.SetMapFlagsContaining(file, map, "Stalact", false);
        }

        SaveGameEdits.SetQuestStatus(file, "MM_StalactStuck", 0,
            new Dictionary<string, int> { ["GetFireKey"] = 0, ["StalactFinished"] = 0 });
        SaveGameEdits.SetQuestStatus(file, "Relationship_Monoco", 0,
            Enumerable.Range(0, 8).ToDictionary(i => $"Relationship_Lvl{i}", _ => 0));

        // The level's BP_ConditionalDataLayersManager reads these NamedIDs to
        // choose PreMonocoBattle / PostMonocoBattle / Grandis_BeforeStalact /
        // Grandis_AfterStalact. A post-game donor has the two Stalact flags set,
        // which leaves the station on the after-Stalact layer and hides the
        // MeetingMonoco trigger. Clear them with the rest of the set-pieces.
        foreach (var nid in new[]
                 {
                     "D8AD6611-4162-C20F-58A7-A2AF4FA5CD79", // NID_MonocoStation_GrandisFashion_EloquenceBattle
                     "71BAE11F-4574-E90D-E907-FBAB4F471AB7", // NID_MonocoStation_StalactAttack
                     "BEDC3616-4C33-59E2-A24C-01B54E081257", // NID_MonocoStation_StalactDefeated
                     "B1FD5CA7-4379-6A7C-0FAD-8E89733EE79A", // NID_OldL_MonocoSmash (post-station)
                 })
            SaveGameEdits.SetNamedId(file, nid, false);

        log?.Invoke($"[story] cleared {cleared} Monoco/Stalact flag(s) and re-opened the station");
    }
}

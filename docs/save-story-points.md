# Story-point saves

A save is a flat bag of flags, not a chapter id. The main story is the easiest
part to reconstruct because `QuestStatuses` carries an explicit 17-step
golden-path quest:

```
Main_GoldenPath
  1_LumiereBeginning
  2_SpringMeadow
  3_Goblu
  4_AncientSanctuary
  5_GestralVillage
  6_EsquieNest
  7_SeaCliff
  8_ForgottenBattlefield
  9_MonocoStation
 10_OldLumiere
 11_Axon1
 12_Axon2
 13_EnterTheMonolith
 14_DefeatthePaintress
 15_ReturnToLumiereAfterMonolith
 16_GoBackToLumiereAndDefeatRenoir
 17_RenoirDefeated
```

Each objective is an `E_QuestStatus` byte: `0` not started, `1` in progress,
`2` complete, `3` failed/blocked. The save editor's picker now reads the
in-progress objective and shows "Monoco Station" instead of "Golden Path".

## What is mapped

| Area | Storage | Mapping quality |
|---|---|---|
| Main story chapters | `QuestStatuses.Main_GoldenPath.ObjectivesStatus` | **Complete.** The 17 objectives are ordered and named. |
| Forced camps | `QuestStatuses.Main_ForcedCamps.ObjectivesStatus` | **Complete.** Ten camp beats, aligned to the golden path. |
| Side quests / relationships | `QuestStatuses.*` | **Good.** Keys are readable (`Relationship_Monoco`, `MM_StalactStuck`, …). |
| Map + save flag | `MapToLoad`, `LastUsedSavePoint`, `SpawnPointTagToLoadAt` | **Complete.** 330 spawn tags are catalogued in `SpawnPoints.json`. |
| Visited levels | `VisitedLevelRowNames` | **Complete.** DT_LevelData row names. |
| Unlocked flags | `UnlockedSpawnPoints` | **Good.** Level asset + gameplay-tag lists. |
| Party / roster | `CurrentParty`, `CharactersCollection` | **Complete.** Records can be cloned or removed. |
| Bosses / encounters | `BattledEnemies`, `EncounteredEnemies`, `TransientBattledEnemies` | **Partial by design.** Keys are readable (`LD_Battle_Monoco`, `LD_Battle_Stalact`, `QUEST_MonocoDuel`) but there is no full boss → chapter table. |
| World interactions | `InteractedObjects`, `TransientInteractedObjects` | **Partial.** Object IDs; specific sets (manor doors, Monoco's Station trigger zones, friendly Nevron quests) are known. |
| GPE state | `GPE_States` | **Partial.** `ObjectID_GPE_*` keys, mostly ropes/bridges/doors. |
| Cinematics | `InteractedCinematics` | **Partial.** Readable names, no chapter catalogue. |
| Dialogues | `InteractedDialogues` | **Weak.** GUID keys; the asset side has readable dialogue names. |
| Named IDs | `NamedIDsStates` | **Weak.** GUID -> bool. Mapping those GUIDs to the `NID_*` progression assets is not implemented. |
| Merchants | `MerchantStates` | **Good.** Per-merchant object ids and remaining stock. |

So: the main spine and the location are well mapped; arbitrary object-level
reconstruction is not. A save rewound from a completed donor keeps the donor's
side content, chests, merchants and later-area interaction flags. That is fine
for testing a fight, but it is not a bit-exact "fresh at chapter N" save.

## The game's own debug saves

`pakchunk900` contains 37 `DA_DebugSaveGame_*` assets for early/mid/late
checkpoints:

```
Level_01_SpringMeadows_Start
Level_02_SpringMeadows_MaelleJoin
Level_05_Goblu_Start
Level_10_AncientSanctuary_Start
Level_16_EsquiesNest_Start
Level_17_SeaCliff_Start
Level_20_SeaCliffEnd
Level_21_BattlefieldStart
Level_26_GrandisStation_Start
Level_28_OldLumiere_Start
…
```

There is **no** Monoco's Station debug save; the nearest is
`Level_21_BattlefieldStart` (before the station) and
`Level_26_GrandisStation_Start` (after the Monoco/Stalact set-pieces). They are
`BP_DataAsset_DebugSaveGame` assets rather than `.sav` files and are not
covered by `Mappings.usmap`, so they are not currently usable as save donors.

## Generating a story-point save

In the GUI, **Save Editor → World** has a **Main-story checkpoint** dropdown.
The button applies the rewind in memory only — nothing is written until **Save
and exit**, and **Close without saving** discards it.

For a full save with endgame gear, use the CLI:

```bash
CLI=src/E33ModEditor.Cli/bin/Release/net10.0/E33ModEditor.Cli.dll

# list the chapter checkpoints
dotnet $CLI save-story --list

# rewind a donor and grant endgame test gear
dotnet $CLI save-story "$SAV" out.sav \
  --point monoco-station \
  --char-level 100 --weapon-level 66 --pictos-level 10
```

`--point` accepts the index (1–17) or the key
(`prologue`, `spring-meadows`, …, `monoco-station`, …, `renoir-defeated`).
`--no-gear` skips the weapon/pictos grant; `--no-master` leaves pictos at
their current mastery. The gear catalogues come from `work/items.json`,
`work/current-weapons.json` and `work/item_tiers.json`; debug, GDC,
placeholder and cut-content ids are excluded.

What the command does:

0. clears every `GameDifficultyData.ActivatedChallengeIDs` modifier
   (`BATTLECHALLENGE_ENEMYLIFEMULT`, damage limit, …) inherited from the
   donor — a completed save can carry the 100× enemy-life challenge, which
   turns Monoco's ~149k HP into ~15M;
1. writes the golden-path and forced-camp objective statuses;
2. for chapter 9 and earlier, clears `LD_Battle_Monoco`,
   `LD_Battle_Stalact` and `QUEST_MonocoDuel`, clears the Monoco relationship
   and `MM_StalactStuck`, re-opens the four Monoco's Station trigger zones, and
   clears the station's persistent flags
   (`NID_MonocoStation_StalactAttack`, `NID_MonocoStation_StalactDefeated`,
   `NID_MonocoStation_GrandisFashion_EloquenceBattle`, `NID_OldL_MonocoSmash`);
3. sets `MapToLoad` / `LastUsedSavePoint` / `SpawnPointTagToLoadAt` to the
   chapter's entry flag and adds it to the visited/unlocked lists;
4. rebuilds the party from the chapter roster and removes Monoco before he
   joins;
5. optionally sets character levels and grants all weapons/pictos.

## The generated Monoco save

`Export/Saves/EXPEDITION_5_Monoco_Lv100.sav` (also installed as title slot 6,
`EXPEDITION_5.sav`) was built from the completed save:

- story: `9_MonocoStation` active, 1–8 complete, 10–17 not started;
- location: `Level_MonocoStation` at
  `Level.SpawnPoint.MonocoStation.InsideStation`;
- party: Lune, Maelle, Sciel, Verso at level 100;
- Monoco not recruited, no Monoco/Stalact battle flags;
- all 124 real weapons at level 66;
- all 243 current pictos at level 10, mastered;
- no activated challenge modifiers (the completed donor had
  `BATTLECHALLENGE_ENEMYLIFEMULT = MULT_100`, i.e. 100× enemy HP).

The donor was a post-game save, so resources, side quests, chests, merchants
and exploration unlocks are post-game. The two station fights and the main
story gate are reset.

### Monoco's Station data layers

The station's `BP_ConditionalDataLayersManager` chooses
`DL_MonocoStation_PreMonocoBattle` / `PostMonocoBattle` and
`Grandis_BeforeStalact` / `Grandis_AfterStalact` from NamedID flags:

| NamedID asset | GUID | Pre-Monoco value |
|---|---|---|
| `NID_MonocoStation_GrandisFashion_EloquenceBattle` | `D8AD6611-4162-C20F-58A7-A2AF4FA5CD79` | false |
| `NID_MonocoStation_StalactAttack` | `71BAE11F-4574-E90D-E907-FBAB4F471AB7` | false |
| `NID_MonocoStation_StalactDefeated` | `BEDC3616-4C33-59E2-A24C-01B54E081257` | false |
| `NID_OldL_MonocoSmash` | `B1FD5CA7-4379-6A7C-0FAD-8E89733EE79A` | false |

A post-game donor has both Stalact flags set. That leaves the station on the
after-Stalact layer, and the `DA_GA_SQT_MeetingMonoco` trigger zone (in runtime
cell `4MRX096R1DZ81TX9L30RU78FE`) does not run. The first build of the Monoco
save had this bug; clearing these four flags fixed the gate. `save-story`
does it automatically for chapter 9 and earlier.

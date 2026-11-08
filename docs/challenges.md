# Challenges: Enemy Max Health, Defense and Speed

The save-point **Challenges** menu is data-driven. Each row is a
`DataAsset` of class `BP_DataAsset_BattleChallenge_Numeric`:

| Field | Meaning |
|---|---|
| `ChallengeID` | save key (`BATTLECHALLENGE_*`) |
| `Name` | `ST_UI_GameMenu` string-table key shown as the row label |
| `BattleModifierClass` | soft class spawned at battle start |
| `AvailableValues` | map of option id (`MULT_<n>`) → numeric value |

The spawned modifier BP loads the selected option and pushes it into the battle
manager. `AddChallengeModifiers` in `AC_jRPG_BattleManager` iterates the
manager's `ChallengeAssets` array, spawns each active modifier and calls
`LoadChallengeAsset`.

## What the mod changes

- `DA_BattleChallenge_EnemyLifeMultiplier`
  - keeps vanilla `2 / 5 / 10 / 20 / 50 / 100`
  - appends `500 / 1000 / 2000 / 3000 / 5000 / 10000`
- New `DA_BattleChallenge_EnemyDefenseMultiplier`
  - `2 / 5 / 10 / 25 / 50 / 75 / 100`
- New `DA_BattleChallenge_EnemySpeedMultiplier`
  - `2 / 3 / 5 / 10`

The new DAs use cloned modifier BPs
(`BP_BattleModifierBuff_EnemiesDefenseMultiplier` /
`...EnemiesSpeedMultiplier`) and are added to
`BP_GameDifficultyManager.ChallengeAssets` and to the
`WBP_ChallengesListWidget` vertical box (one `WBP_ChallengeEntry` each).

## BattleManager patch

The vanilla manager only has `Challenge_EnemyHealthMultiplier`. The kit adds:

- class doubles `Challenge_EnemyDefenseMultiplier` and
  `Challenge_EnemySpeedMultiplier`
- setters `Challenge_SetEnemyDefenseMultiplier` and
  `Challenge_SetEnemySpeedMultiplier` (clones of the health setter; they only
  assign the variable)
- two extra conditional blocks in `Challenge_ApplyEnemyStatChanges`, cloned
  from the health block, which multiply the stat map entries:
  - `E_jRPG_StatType` **6** = `NewEnumerator4` (Defense)
  - `E_jRPG_StatType` **8** = `NewEnumerator7` (Speed)

The enum values come from the usmap (`EnumMap`), not the `NewEnumeratorN`
suffix. The health block uses **1** (`NewEnumerator1`).

## Build hook

`ChallengeKit.Apply(modDir, log)` runs from `FullPackKit` after Party of Five /
Gameplay Tweaks so it patches the already-merged `AC_jRPG_BattleManager` copy.
It is idempotent and ends with `Verify`, which re-opens the staged assets and
throws if a DA option count, manager entry, BattleManager block, menu row or
string-table key is missing.

## Verification

`ChallengeKit.Verify(modDir)` can be called independently after a pack (the
DataAsset class schemas are registered when the kit runs). For a quick manual
check the CLI equivalents are:

```text
dump   <DA>.uasset
st     <ST_UI_GameMenu>.uasset CHALLENGE
exports <WBP_ChallengesListWidget>.uasset
fn-list <AC_jRPG_BattleManager>.uasset
```

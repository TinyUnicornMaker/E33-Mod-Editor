# Challenges: Enemy Max Health, Defense and Speed

> **Standalone scope:** `ChallengeKit` is **not** part of the Expedition
> Infinity standalone packer. The feature is incomplete and is excluded from
> 1.3.6; do not ship it in a deployed pack. This document is kept for the
> full-editor/CLI experiment only.

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

The new DAs use cloned, **no-op** modifier BPs
(`BP_BattleModifierBuff_EnemiesDefenseMultiplier` /
`...EnemiesSpeedMultiplier`) and are added to
`BP_GameDifficultyManager.ChallengeAssets` and to the
`WBP_ChallengesListWidget` vertical box (one `WBP_ChallengeEntry` each).

## BattleManager patch

The vanilla manager only applies `Challenge_EnemyHealthMultiplier`. Defense
and Speed are read **inside the existing**
`Challenge_ApplyEnemyStatChanges` function, which already has the enemy stat
map and the health block:

- for each of `BATTLECHALLENGE_ENEMYDEFENSEMULT` and
  `BATTLECHALLENGE_ENEMYSPEEDMULT`, call
  `GetDifficultyManager().GetSelectedChallengeOption(...)`
- resolve the challenge DA with `FindChallengeDefinition(...)`
- read the selected value with `GetOptionNumericValue(...)`
- multiply the stat map entries:
  - `E_jRPG_StatType` **6** = `NewEnumerator4` (Defense)
  - `E_jRPG_StatType` **8** = `NewEnumerator7` (Speed)

The enum values come from the usmap (`EnumMap`), not the `NewEnumeratorN`
suffix. The health block uses **1** (`NewEnumerator1`).

**Do not add class variables or function exports to
`AC_jRPG_BattleManager`.** UAssetAPI cannot fully parse that class because its
`S_BattleInitialState` schema is missing; re-serializing its
`Default__AC_jRPG_BattleManager_C` CDO with extra class properties produces a
`Bad export index` fatal error at load. Patch only the existing function's
locals and bytecode.

## Build hook

`ChallengeKit.Apply(modDir, log)` runs from `FullPackKit` after Party of Five /
Gameplay Tweaks so it patches the already-merged `AC_jRPG_BattleManager` copy.
It is idempotent and ends with `Verify`, which re-opens the staged assets and
throws if a DA option count, manager entry, BattleManager lookup, menu row or
string-table key is missing.

## Verification

`ChallengeKit.Verify(modDir)` can be called independently after a pack (the
DataAsset class schemas are registered when the kit runs). For a quick manual
check the CLI equivalents are:

```text
dump    <DA>.uasset
st      <ST_UI_GameMenu>.uasset CHALLENGE
exports <WBP_ChallengesListWidget>.uasset
fn-list <AC_jRPG_BattleManager>.uasset
```

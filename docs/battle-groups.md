# More than three enemies (battle groups, first pass)

Status: **in principle / smoke-verified**, not yet played in-game. The field-side
capacity and the randomizer group generator are implemented; camera framing and
the second-row offsets are the parts most likely to need tuning after a real
battle.

## Why vanilla stops at four

`BP_BattleMap` ships four `BP_jRPG_EnemyBattleSpot` child actors (slot indices
0–3). Most battle-map Blueprints inherit those anchors instead of declaring
their own, and the rest of the battle code is array-driven:

* `AC_jRPG_BattleManager.SpawnEnemies` loops the encounter's `Enemies` map keys
  and calls `SpawnEnemyOnSpot` for each one.
* `GetProperBattleSpawnPointTransform` looks up the matching spot actor by
  `SlotIndex`; there is no fallback, so a slot without an anchor spawns at the
  origin.
* `DispatchEnemiesAlongSpline` then lays the spawned enemies out along the
  map's `EnemyLineSpline`, using `FL_jRPG_CustomFunctionLibrary`'s fixed
  left-to-right order table. The vanilla table has four entries.
* `WBP_HUD_TurnOrder`, the per-enemy overhead health bars and the targeting
  groups are already dynamic — they have no three/four enemy cap.

So the limiting pieces are the **spot order tables** and the **missing spawn
transform for slots ≥ 4**, not the HUD.

## What `BattleGroupKit` changes

`src/E33ModEditor.Core/Loot/BattleGroupKit.cs` patches three vanilla assets:

| Asset | Change |
|---|---|
| `FL_jRPG_CustomFunctionLibrary` | `GetEnemyIndexesLeftToRight` and `GetEnemyIndexToBattleSpotsLeftToRight` grow to the configured capacity (vanilla four entries preserved, extra slots appended). This lets `DispatchEnemiesAlongSpline` process every enemy. |
| `BP_jRPG_Character_Battle_Base` | The enemy half of `GetBattleSpotIndexLeftToRight_(WhenFacingCharacters)` grows to the same capacity. Extra slots reuse the clamp-safe `0..2` order values. |
| `AC_jRPG_BattleManager` | `GetProperBattleSpawnPointTransform` aliases extra enemy slots to a vanilla anchor (`4→0`, `5→1`, `6→2`, `7→3`, then repeats) and adds a per-slot offset in the anchor's own facing frame. Vanilla slots 0–3 keep offset zero. |

No battle-map assets, new spot actors or third-party packages are needed. A
map's four vanilla anchors are reused, and extra enemies start behind them. In
normal (non-boss, non-static) fights `DispatchEnemiesAlongSpline` then respaces
the whole group along the map's enemy spline, so the initial offsets mostly
matter for static-location fights and for the first frame.

Default field capacity is **32** (`BattleGroupKit.FieldCapacity`). Only 4-8 is
validated in-game; 32 is an experimental stress setting. The alias chain, order
tables and offset expression are count-generic, but the four authored spot
actors, the single enemy spline (`DispatchEnemiesAlongSpline`), the per-map
cameras and the turn-order HUD were designed for small groups. Expect bunched
placement, no camera coverage and an overflowing HUD long before the bytecode
complains. `FieldCapacityOverride` lets a pack patch 32 slots while normal
rosters stay small.

**Spline dispatch guard (2026-10-29).** `DispatchEnemiesAlongSpline` lays every
enemy on one spline by accumulating capsule widths. Past ~8 enemies the
accumulated distance exceeds the spline, positions clamp, and the back half of
the roster piles into one or two overlapping clumps — the reason a 32-enemy
fight looked like five. `BattleGroupKit.PatchSplineDispatch` now inserts a
guard at the top of that function: if the spawned enemy count is over 8 it
returns immediately, leaving every enemy on its staggered spawn slot
(anchor + `EnemyOffsets`). Groups of 8 or fewer keep vanilla dispatch.

**Staggered lattice (2026-10-29).** Every slot — including the four vanilla
ones — is placed on one lattice measured from a single anchor
(`LatticeAnchor`), so the group reads as one formation rather than a front line
plus a cluster. `BlockColumns` (4) across, `LatticeLateralStep` (150) apart,
`LatticeDepthStep` (140) per row back, with alternate columns pushed half a row
deeper so no two enemies share a row or a column. A smaller fight just uses the
first cells of the same lattice.
per-anchor sub-clouds overlap into one crowd.

The patch composes with **Party of Five**: both kits stage
`AC_jRPG_BattleManager`, `FL_jRPG_CustomFunctionLibrary` and
`BP_jRPG_Character_Battle_Base`, and each edit is keyed to its own table/branch.
`battle-groups-smoke` applies both kits to the same staged tree and re-verifies
the battle-group tables afterwards.

## Randomizer groups

`RandomizerKit.Settings` gains:

| Setting | Default | Meaning |
|---|---|---|
| `BattleGroups` | `false` | Master toggle. Also patches field capacity when on. |
| `MaxEnemiesPerBattle` | `5` | Largest roster the generator may write (1–32). |
| `FieldCapacityOverride` | `0` | Patch this many field slots regardless of roster size (0 = use the roster max). |
| `ExtraEnemiesChance` | `50` | Percent chance per eligible encounter to add at least one enemy. |
| `ShuffleBattleRosters` | `true` | Shuffle the finished roster instead of appending adds. |

For each eligible row in `DT_jRPG_Encounters` and
`DT_WorldMap_Encounters`, the generator:

1. Reads the vanilla/current roster.
2. Skips bosses, alphas, superbosses, merchants/petanks, cut content, narrative
   fights, tutorial/test/quest rows and `LootCatalog.BrokenEncounters`.
3. Rolls `ExtraEnemiesChance`; on a hit picks a target size between
   `current + 1` and `MaxEnemiesPerBattle`.
4. Fills the new slots from the **same area prefix and same tier** as a random
   original member (e.g. a `YF_` fight only draws `YF_` rows). A thin pool
   duplicates the original row rather than importing a stranger from another
   biome.
5. Optionally shuffles the roster and writes contiguous spot keys `0..N-1`.

It runs at the very end of `RandomizerKit.Apply`, after loot/stat passes and
after the variant kits have repointed endgame/world-map rows. The Endless Tower
(`DT_jRPG_Encounters_CleaTower`) is deliberately untouched for now.

## Random battle every time (runtime)

The fixed-roster mode above rewrites an encounter once at pack time. The
runtime mode uses the game's own encounter-variation mechanism instead.

Every world enemy Blueprint carries a `PossibleEncounters` array of
`FDataTableRowHandle`s. On `ReceiveBeginPlay` vanilla calls
`Array_Random(PossibleEncounters)` once and stores the result in
`SelectedEncounter`; `StartBattle` then uses that stored handle. To make the
same overworld enemy roll a fresh group every time it is attacked:

1. **Pack time** (`RandomBattles` on): generate `BattleVariants` extra encounter
   rows per eligible original (default 3). Each variant has the same row
   settings as its original but a different randomized roster.
2. **Pack time**: append the variant handles to every world enemy Blueprint
   whose `PossibleEncounters` references the original. The original stays in the
   array, so vanilla remains one of the outcomes.
3. **Pack time**: patch
   `BP_jRPG_Enemy_World_Base_Seamless.StartBattle` to call
   `Array_Random(PossibleEncounters)` and write the result to
   `SelectedEncounter` before building `FBattleStartParams`.
4. **Pack time**: rewrite eligible base rows in place with a seeded
   randomized roster (same pool rules as the variants). Level-placed
   instances, `BP_EnemyGroup`s and triggers name base rows directly and never
   consult `PossibleEncounters` — verified: the Flying Waters
   (`Level_Goblu_Main_V5`) map embeds rows such as
   `GO_Bruler*1_Demineur*1` and `GO_Demineur*2` in placed-actor data while no
   world-enemy Blueprint references those multi-enemy rows (each roaming
   `BP_EnemyWorld_*` only holds its own single-enemy encounter). Without the
   in-place rewrite those placed fights stay vanilla no matter what the
   re-roll does.
5. **Pack time + runtime**: extend the `StartBattle` patch with a
   variant-family re-pick. After the stage-1 pick, the patch breaks
   `SelectedEncounter` into its DataTable + RowName, builds the
   `RAND_<base>_r1..rN` names with string concatenation, guards on
   `DoesDataTableRowExist` for the r1 name, and — when a family exists —
   re-picks uniformly from the base row plus its variants. This is what makes
   placed fights roll a fresh group every attempt: their instance arrays only
   ever name base rows, but the family is derived from the selected row at
   runtime. Rows without a family (bosses, narrative, tower, already-variant
   picks) keep the stage-1 selection untouched.
   > **Status 2026-10-29: SHIPPED and confirmed working in-game** — all three
   > variants plus the rewritten base seen rolling per attempt at a placed
   > Flying Waters fight. Bisect history: mode 1 (guard-only) ran clean; mode 2
   > crashed twice on `Array_Add` defects (RValue mispointed at the array, then
   > the dead-code `EX_LocalOutVariable` operand shape — its only template
   > source is the uncalled `CheckForErrors`); live `Array_Add`s pass the array
   > explicitly. `FamilyRepipckMode` in `BattleGroupKit.cs` retains the modes
   > if a future game update ever breaks the block.

The `StartBattle` insert is the same two-statement pattern vanilla already uses
in `ReceiveBeginPlay` (`Array_Random` with two locals, then
`SelectedEncounter = OutItem`), so it reuses the existing imports and the
DataTableRowHandle local type.

Settings:

| Setting | Default | Meaning |
|---|---|---|
| `RandomBattles` | `false` | Master runtime toggle. Also patches field capacity. |
| `BattleVariants` | `3` | Variant rows generated per eligible encounter (1–8). |
| `BattleGroupsAreaPrefix` | `""` | Optional test filter shared with fixed-roster mode (`GO_` = Flying Waters). |

Bosses, alphas, narrative/tutorial fights and the other exclusions are the same
as the fixed-roster mode, so a boss world enemy with one authored encounter is
left with a one-entry `PossibleEncounters` array and the re-roll is a no-op.

### Caveats

* This stages every world enemy Blueprint that references an eligible encounter
  (roughly 100 in vanilla), so it is a larger override set than fixed-roster
  mode.
* `PossibleEncounters` is a class default. If a level instance overrides that
  array, the instance copy wins and will not see the appended variants. This
  was confirmed in-game for Flying Waters multi-enemy fights (they repeated the
  vanilla roster): the placed instances reference base rows directly. The
  in-place base rewrite plus the runtime family re-pick (steps 4–5 above)
  cover those fights — every battle path now rolls per attempt.
* Roaming world enemies re-roll every battle from their extended array; placed
  fights re-roll every battle from the selected base row's variant family
  (step 5) — confirmed working in-game 2026-10-29.
* The `ReceiveBeginPlay` pick is still what pre-battle checks such as
  `IsPlayerTooPowerfullToChase` see until the first battle starts. `StartBattle`
  then re-rolls, so the first battle and every subsequent one use a fresh
  `PossibleEncounters` pick.
* `random-battles-smoke` verifies the variant rows, the Gault
  `PossibleEncounters` growth, the `StartBattle` insert and jump integrity. It
  does not run the game.

## Running it

```bash
CLI=src/E33ModEditor.Cli/bin/Release/net10.0/E33ModEditor.Cli.dll

# Patch-only smoke: capacity + composition with Party of Five + group writing.
dotnet $CLI battle-groups-smoke

# Runtime variant smoke: variant rows, world-enemy handle appends, StartBattle re-roll.
dotnet $CLI random-battles-smoke

# Apply to an existing staged mod (default work/mod) for a quick test.
dotnet $CLI battle-groups --mod-dir work/mod --max 5 --chance 50 --seed 123
dotnet $CLI random-battles --mod-dir work/mod --max 5 --variants 3 --chance 50 --seed 123

# Normal path: set Battle groups in the Randomizer, then run `rebuild-mod` / the standalone generator.
```

The GUI exposes the fixed-roster settings under **Randomizer ▸ Battle groups
(experimental)** and the runtime toggle under **Randomizer ▸ Random battle every
time (experimental)**; the standalone packer exposes both under **Randomizer ▸
Loot tuning**.

## Known limits / next steps

* **Camera.** The overview/targeting camera paths are per-map. Extra enemies
  placed behind the vanilla line are inside the spline's normal spread, but a
  six–eight enemy group may still frame tightly. This needs an in-game pass;
  the offsets are centralised in `BattleGroupKit.EnemyOffsets`.
* **Static-location maps.** Fights that set `UseStaticCharacterLocations` skip
  `DispatchEnemiesAlongSpline`, so extras keep the aliased anchor + offset. The
  current two-row offsets are a first guess.
* **Boss fights** are intentionally excluded from roster growth; their scripted
  cameras, phases and add spawns assume the authored roster.
* **Encounter rows are rewritten, not created.** New rows would not be
  referenced by world actors/triggers, so the randomizer edits the rows the game
  already uses. Adding brand-new encounter IDs needs a source-side repoint.
* **In-game validation pending:** battle start with 4–8 enemies, overhead bars,
  turn order, targeting left/right, free-aim target cycling, and post-battle
  drops.

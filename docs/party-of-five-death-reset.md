# Party-of-Five: death-position reset — findings

Status: **diagnosed and instrumented — fix not yet applied.**

The re-capture-home insert passed every offline structural check and still
crashed battle load. It has been removed; the mod is back on the configuration
confirmed working (all five spawn, position reset on death still present).

The one assumption in the diagnosis above that has never been verified against
the running game is the *value* of the reserve's stale `InitialLocation`. The mod
now prints it (see *Instrumentation* below), so the remaining work is a playtest
plus a fix built on numbers rather than inference.

## What actually resets positions

`AC_jRPG_BattleManager` (vanilla) calls `MoveCharactersBackToTheirSpots` from an
ubergraph block whose own debug label is:

```
MoveAllCharactersBackToTheirSpot_AfterTurnEnd
```

i.e. it runs **AfterTurnEnd**, which includes the turn in which a character
dies. Flow around BattleManager ubergraph stmts #1139–#1148 and (repeated)
#1165 / #1179.

That function moves each live player character via its
`AC_jRPG_CombatMovement` **`InitialLocation`**. The game's own debug string
proves the target (BattleManager ubergraph stmt #1122):

```
  TargetLocation is not InitialLocation: Starting to wait for new
  MoveToInitialLocation of '{char}'
```

`AC_jRPG_CombatMovement.UpdateInitialTransform()` (CombatMovement ubergraph
stmt #75–#77, live at bytecode offset 1975–2075) is the only writer:

```
GetOwner() -> K2_GetActorLocation() -> InitialLocation =
```

`ReceiveBeginPlay` (CombatMovement ubergraph offset 96) does **not** call it.

## Who calls UpdateInitialTransform

| Caller | Object | When |
|---|---|---|
| `BP_jRPG_Character_Battle_Base` ubergraph #1719 | `self.AC_jRPG_CombatMovement` | battle base `OnBattleStart` flow |
| `AC_jRPG_BattleManager.UpdateDummyMiddleCharacterLocation` | `DummyMiddleCharacter` | battle start / camera |
| `AC_jRPG_BattleManager.ChangeBattleMap` | — | battle map change |

So **every character's `InitialLocation` is captured exactly once, at battle
start**, from its spawned position.

## Verified placement paths (all consistent, all patched)

All player placement uses `AC_jRPG_BattleManager.GetProperBattleSpawnPointTransform`,
whose character branch the kit patches with the `Formation` offsets. Callers of
`Spawn Player Character`:

| Caller | Spot index source |
|---|---|
| `SpawnPlayerCharactersFromParty` | `RemapPartyIndexToBattleSpotIndex(i, BattleParty.Length)` → `[2,0,3,1,4]` |
| `SpawnExtraPlayerCharacter` | same remap |
| `ExecuteUbergraph` stmt #844 | same remap |
| `SpawnCharacterFromStoredState` (reintegrate/reserve) | `StoredState.PartyIndex`, which `KickCharacterFromBattle_Internal` stores from `Character.BattleSpotIndex` |

`GetPlayerCharacterAtBattleSpot` compares `BattleSpotIndex` in a loop over
`GetAllPlayerCharacters()`, so spot index 3 is safe there.

## The bug

A character that is kicked (`OnPlayerCharacterKilled` →
`Internal_MakePlayerCharacterDead` → `KickCharacterFromBattle_Internal`) and
later re-integrated via `SpawnCharacterFromStoredState` **does spawn at the
correct formation position** (all spawns use the patched transform). But
`UpdateInitialTransform` is only invoked in the `OnBattleStart` flow, which has
already run — so the new actor's `InitialLocation` keeps the component default.

At the next `AfterTurnEnd`, `MoveCharactersBackToTheirSpots` moves everyone to
their `InitialLocation`: survivors keep their offsets, the re-integrated
character converges on the default home. That is the observed reset.

## Fix applied

Implemented as `PartyFiveKit.PatchCharacterBattleBase`:
- `InsertRefreshInitialLocation` clones the battle base's own statement #1719
  (`self.AC_jRPG_CombatMovement.UpdateInitialTransform()`) and inserts it into
  the per-spawn block (`FinishSpawningActor` -> `StartSequence` -> guards ->
  `StartLatentAction` -> **[new]** -> `MoveToInitialLocation`).
- Both battle-base edits (spot-order widening + this insert) share ONE
  `CopyIntoMod`/`Open` pass, because `CopyIntoMod` re-copies the vanilla donor
  on every call and the second patch would silently overwrite the first.

Verified: `party5-smoke` passes (29 checks); ubergraph 2267 -> 2268 statements
with every jump/flow offset validated; insert confirmed present in the
re-extracted packed container.

### Crash fixed: ubergraph entry-offset stubs

The insert initially *crashed* battle start, because this Blueprint's 131
function stubs are `EX_LocalFinalFunction(StackNode = ExecuteUbergraph,
[IntConst offset])` — the offset is an **absolute bytecode position inside the
ubergraph**. Growing the ubergraph by 36 bytes invalidated 130 of them, so
those event entry points landed mid-expression and the VM crashed.

`PartyFiveKit.ShiftUbergraphEntryOffsets` bumps every stub whose offset is at or
past the insertion point by the inserted byte count. 130 shifted +36, 1
untouched (offset 15, before the insert). Independently verified on the
installed pack: all 131 stub offsets land exactly on statement starts.

Insertion-aware offset remap (linear remap alone is wrong once the statement
count changes):

```
Map(target) = oldStarts[idx] <= target
   rel = target - oldStarts[idx]
   idx <  insertIdx -> curStarts[idx]   + rel
   idx >= insertIdx -> curStarts[idx+1] + rel
```

### Why it still crashed (best hypothesis)

The insert landed in the per-spawn block between `StartLatentAction`
(registers the latent op that `MoveToInitialLocation` will use) and
`MoveToInitialLocation` itself. A latent function's setup in UE5 is a two-step
contract — register, then call — and a synchronous node spliced into the gap is
exactly the kind of thing no structural check can see. Verifying it needs either
the game or a Kismet VM trace, neither of which is available offline.

Any future attempt should therefore avoid this block entirely and host the call
somewhere the parameters are already settled:

1. `AC_jRPG_BattleManager`'s `Spawn Player Character`, right after
   `FinishSpawningActor` — the spawned character is reachable there and it is a
   real function (not a ubergraph stub), so only its own internal offsets move.
   This is where the confirmed-working constant edits already live.
2. `AC_jRPG_BattleManager.UpdateInitialTransform`'s own callers — or
   `MoveCharactersBackToTheirSpots`, if the home is re-derived instead of
   re-captured.
3. `AC_jRPG_CombatMovement.MoveToInitialLocation`'s ubergraph body — the
   natural home for "re-capture, then move".

## Open question (to confirm)

After one death + reserve, does *every* member end up in the vanilla line, or
only the re-integrated member misplace? If it is everyone, the reset is
`InitialLocation`-driven for the survivors too and the fix must re-capture
location for all five rather than only the newcomer.

## Instrumentation (in the mod now)

`AC_jRPG_CombatMovement.MoveToInitialLocation` — the latent function that snaps a
character home, and therefore the function that runs at the moment of reset —
prints two lines on screen:

```
[party5] InitialLocation = X=… Y=… Z=…
[party5] TargetLocation  = X=… Y=… Z=…
```

The stock equivalent is the `PrintStringInLog` block in the BattleManager
ubergraph (stmt #1122/#1123) next to the `MoveAllCharactersBackToTheirSpot_
AfterTurnEnd` debug label, which writes to the log file only and so is invisible
without a log. The kit's version uses `PrintString` (to screen *and* to log),
and uses only nodes and imports already present in that asset.

Read it as follows:

- At battle start the print should appear five times in quick
  succession, each `InitialLocation` matching a formation position.
- The reserve's own snap prints a `TargetLocation` equal to its formation
  position (it spawned correctly — every spawn path uses the patched
  transform), so if its `InitialLocation` is `(0,0,0)` or the default home, the
  print will say so directly.
- After the next `AfterTurnEnd`, whichever character converges on a spot other
  than its own `InitialLocation` is the one the reset is misplacing.

Each print is keyed separately (`PartyFiveInitial` / `PartyFiveTarget`), so both
stay on screen for 30 s rather than replacing one another.

`--no-resetdebug` removes both statements for a release build.

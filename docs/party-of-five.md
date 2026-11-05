# Party of Five (5-member battle party submodule)

A 5-member battle party built from vanilla assets only — no third-party mod
code, assets, or containers. It raises the battle party cap to 5 and fields a
**two-row formation**: members 1 and 2 in the front row (left to right), and
members 3, 5 and 4 in the back row (left to right).

> **It is a submodule of the main pack now.** The party patches are folded into
> `Export/E33ModEditor_P` rather than shipped as a standalone `E33PartyFive_P`.
> Enable it in the GUI's **Party of Five** tile (or `work/party_five.json`) and
> run `rebuild-mod` / the standalone generator. The standalone `party5 --pack` path still
> exists for bisecting crashes but is debug-only — never ship it beside the main
> pack (both would own `AC_jRPG_BattleManager` and shadow each other).

```
               enemies
                 [1] [2]           front row, left to right
             [3]  [5]  [4]         back row, left to right
```

> Member 3 sits back-left and member 4 back-right (their left-to-right order is
> 3/5/4), because the remap pins 3 to the left-hand anchor and 4 to the right-hand
> one. Member 5 aliases the centre anchor and is pushed behind the front pair.

The optional **back-row damage** rule (back row deals and takes ×0.8) is
documented in [row-damage.md](row-damage.md). Rows are derived from these
offsets; the default back spots are `{1, 3, 4}`.

## Where the geometry comes from

The vanilla `BP_BattleMap` has three character-spot child actors. From the
asset:

| slot | location (X, Y) | arrow yaw |
|---|---|---|
| 0 | (−230.06, +7.97) | −12.95° |
| 1 | (−223.11, +264.81) | −32.74° |
| 2 | (−211.20, −274.30) | +9.33° |

The enemies sit at X ≈ +360…+402, so the party's forward is +X.
`GetBattleSpotIndexLeftToRight_(WhenFacingCharacters)` returns
`table[BattleSpotIndex]`; the vanilla player table `[1,0,2]` puts slot 2
leftmost and slot 1 rightmost, and the enemy table `[1,2,0,3]` puts
Y = −212 → −48 → +135 → +321 in that order. So **screen-right is +Y**, and the
vanilla party is a line running along Y at −274, +8 and +265 — i.e. screen-left
to screen-right: characters 1, 2, 3.

With `RemapPartyIndexToBattleSpotIndex = [2,0,3,1,4]` the anchors are:

| character | anchor slot | anchor |
|---|---|---|
| 1 | 2 | (−211.20, −274.30) |
| 2 | 0 | (−230.06, +7.97) |
| 3 | 3 (aliased to 2) | (−211.20, −274.30) |
| 4 | 1 | (−223.11, +264.81) |
| 5 | 4 (aliased to 0) | (−230.06, +7.97) |

Characters 1 and 3 share the left-hand anchor, and 2 and 5 the centre one, which
is why each needs its own offset. Each offset is decomposed through its *own*
arrow yaw, so a single per-spot table still colocates them correctly.

## Build

```bash
CLI=src/E33ModEditor.Cli/bin/Release/net10.0/E33ModEditor.Cli.dll

# The supported path: fold the party into the main pack.
# Enable it once (writes work/party_five.json), then build the combined mod.
dotnet $CLI party5                 # stage + verify work/mod_party5, save settings
dotnet $CLI rebuild-mod            # pack E33ModEditor_P (camera + party + everything)

# Enable Party of Five in the editor, then build with `rebuild-mod` / the standalone generator.

# Debug-only bisect ladder (does NOT save settings / is not shipped):
dotnet $CLI party5 --out work/mod_party5 --no-formation
dotnet $CLI party5 --out work/mod_party5 --no-resetdebug
dotnet $CLI party5 --out work/mod_party5 --no-spawn

# patch-only smoke test (no game files touched)
dotnet $CLI party5-smoke
```

> ⚠ Remove `0000_Party_Of_Four_P` (or any other party-size mod) from
> `Sandfall/Content/Paks/~mods/` before playing. Its own `BP_BattleMap`,
> `BP_jRPG_Character_World` and `WBP_GM_TeamOverview3D_Page` are written
> against *its* BattleManager/FL functions; with our assets mounted those
> call into a different blueprint and can break at battle start. Party of Five
> makes the third-party mod (and the Gameplay Tweaks ▸ Compatibility path that
> patched it) unnecessary — with Party of Five on, compatibility is skipped
> automatically.

## What it patches (9 vanilla assets + multi-ally buff scripts)

| Asset | Change | Why |
|---|---|---|
| `AC_jRPG_CharactersManager` | CDO `MaxCharactersAmountInParty` 3 → 5 | The single int gating `AddCharacterToParty`, `ComputePartyWithExclusions` (battle party build) and the team-menu toggle. |
| `FL_jRPG_CustomFunctionLibrary` | `RemapPartyIndexToBattleSpotIndex` table `[2,0,1]` → `[2,0,3,1,4]` | Party slots 4/5 previously read past the vanilla 3-entry table. Slots now map 1/2 to the front, 3/4 to the back flanks and 5 to the back centre. |
| `AC_jRPG_BattleManager` | `GetProperBattleSpawnPointTransform` (spots 3/4 alias vanilla anchors + formation offsets) **and** two hard-coded `3`s in `Spawn Player Character` | Positions the two rows *and* lets the 4th/5th members spawn at all. Offsets are expressed in each spot's own facing frame (`GetForwardVector`/`GetRightVector` of the spot's arrow rotator), so they stay correct on rotated maps and on every map's own spot anchors — no per-map edits needed. |
| `BP_AICompanion_TargetPointsComponent` | `TargetPoints` array 2 → 4 | World followers are capped at the number of follow points (`Min(companions, points)` in `BP_AICompanion_CompanionManager`); vanilla has two (left/right). See *Overworld followers* below. |
| `BP_jRPG_Character_World` | the `BP_AICompanion_TargetPointsComponent_GEN_VARIABLE` component template's `TargetPoints` array 2 → 4 | **This is the one that actually applies** — see *Overworld followers* below. |
| `WBP_GM_TeamOverview3D_Page` | two event-graph literals `3` → `5` | The team menu hard-codes the cap: the *add to party* branch runs only while `CurrentParty.Length < 3`, and the toggle action-bar button is marked forbidden once `CurrentParty.Length >= 3`. Both must move with the party size or the 4th/5th members cannot be selected. |
| `BP_jRPG_Character_Battle_Base` | `GetBattleSpotIndexLeftToRight_(WhenFacingCharacters)`: player table `[1,0,2]` → `[1,0,2,2,1]` | The enemy table already has four entries; the player table had three, so allies with `BattleSpotIndex` 3/4 read past it. Spot 3 mirrors spot 2's entry (both screen-left), spot 4 uses the middle entry — see *Free-aim yaw clamp* below. This table drives ally targeting/camera left-to-right ordering. |
| 10 multi-ally buff skill scripts (8 vanilla + 2 mod clones) | `OnActionEffect`: on a QTE chain with no misses, set the target count to `Array_LastIndex(TargetedCharacters)` | See *Multi-ally buff target count* below. Restores the 4th/5th party members on moves authored as "1-3 allies". |
| `AC_jRPG_CombatMovement` | `MoveToInitialLocation`: two `PrintString` statements inserted | See *The home-snap debug print* below. Diagnostic only — no game logic changes. |

The spawn offsets (`PartyFiveKit.Settings.Formation`, spot index ->
`(forward, right)` in the spot's own facing frame; values solved per anchor so
the result is the requested two-row formation):

| spot | party slot | char | (Fwd, Right) | lands at | screen-x |
|---|---|---|---|---|---|
| 2 | 0 | 1 | `(+93, +136)` | (-141.5, -125.0) | -125 (front, left of centre) |
| 0 | 1 | 2 | `(+60, +134)` | (-141.6, +125.1) | +125 (front, right of centre) |
| 3 (aliased) | 2 | 3 | `(-69, -15)` | (-276.8, -300.3) | -300 (back, left flank) |
| 1 | 3 | 4 | `(-64, +1)` | (-276.4, +300.3) | +300 (back, right flank) |
| 4 (aliased) | 4 | 5 | `(-100, -22)` | (-332.4, +8.9) | +9 (back, centre) |

The front row straddles the vanilla line's centre (Y ~ 0) and sits 90 units
closer to the enemies than vanilla; the back row spreads to +/-300 with member 5
behind the front pair.

What the offsets must respect:

* **Spacing.** `GetCharacterRadiusForPlacement` returns
  `BoxExtent.Y + FMax(BoxExtent.Y * 0.1, 50)` — with a typical mesh Y extent
  that is roughly 100 units of character radius, so the game's own spacing
  target is around 200 units between centres. The smallest gap here is **221**
  (character 2 to character 4), and the front pair is 250 apart, matching the
  vanilla 257-282 spacing. This is the "not enough space for both characters"
  concern: the front pair cannot be pulled closer than about 230 without
  visual clipping.
* **Characters 3 and 5 alias vanilla anchors.** Character 3 aliases slot 2
  (character 1's left anchor) and character 5 aliases slot 0 (character 2's
  centre anchor), so their offsets have to travel further.

## Overworld followers

The active character's `BP_AICompanion_CompanionManager` decides how many
companions to show:

```
CompanionNamesInParty  = CurrentParty                          (all names)
CompanionNamesToActivate = CompanionNamesInParty minus the main character,
                           then Array_Resize to
                           Min(count, TargetPointsComponent.GetTotalNumberOfPoints())
```

`GetTotalNumberOfPoints` returns `TargetPoints.Length`, so the point count is
the only cap. Everything downstream is array-driven: `SpawnAndUpdateCompanions`
loops the party list, `CanSpawnCompanions` only checks the
`Sandfall.DisableCompanions` CVar, and `UpdatePointsBasedOnDistance` repositions
already-acquired points rather than adding or removing them. No function in the
component or the manager hard-codes a count (the only int constants anywhere in
the component are `0`, `1` and `-1`).

**The trap.** `BP_jRPG_Character_World` carries its own
`BP_AICompanion_TargetPointsComponent_GEN_VARIABLE` component template, and that
template overrides `TargetPoints`:

```
TargetPoints: array[2] vec=(-150,-200,-95) | vec=(-150,200,-95)
```

A component-template property wins over the component's class default, so
patching `Default__BP_AICompanion_TargetPointsComponent_C` on its own never
reaches the running game -- the manager still resolved
`GetTotalNumberOfPoints` to 2 and resized its activation list to
`Min(3, 2) = 2`. This is the same class of bug as the donor re-copy: patching
the right *name* on the wrong *object*. Both arrays are now widened to four
points (four followers for a five-member party):

| where | before | after |
|---|---|---|
| `Default__BP_AICompanion_TargetPointsComponent_C` (class CDO) | `(-150,±115,-95)` | `(-150,±115,-95)` plus outer flanks `(-150,±400,-95)` |
| world character component template | `(-150,±200,-95)` | `(-150,±200,-95)` plus outer flanks `(-150,±400,-95)` |

The vanilla flanks stay exactly where they were; the fourth follower gets the
matching outer flank on the other side, so the four points are balanced
(left/right near + left/right far). Seven companion pawn classes exist
(`BP_Pawn_AICompanion_{Lune,Maelle,Monoco,Noah,Sciel,Sophie,Verso}`), so a
5-member party never runs out of classes.

## Free-aim yaw clamp

The 4th/5th members could only aim up and down if their order values fell
outside the switch range. The cause was the spot-order table above, not
collision or friendly-fire logic.

`ClampAngleForLookLeftRight` (on `BP_jRPG_Character_Battle_Base`) clamps the
free-aim yaw (`LookX`) through `ClampAngle(Angle, Min, Max)`, where Min and Max
each come from a switch over the character's own
`GetBattleSpotIndexLeftToRight_` order value. The switches have cases only for
`0/1/2`:

| order | min | max |
|---|---|---|
| 0 (rightmost) | −58 | +105 |
| 1 (middle) | −80 | +90 |
| 2 (leftmost) | −80 | +75 |

(The order value counts from the right: vanilla `[1,0,2]` puts the Y=+265
right-hand spot at 0, the centre at 1, the Y=−274 left-hand spot at 2 — each
with the widest range toward its own side.)

An order value of `3` — what an append-only table would produce — falls into
*both* switch defaults, which are unassigned locals (`0.0`), so
`ClampAngle(LookX, 0, 0) = 0` permanently. Pitch (`LookY`,
`ClampAngleForLookUpDown` = −30…+45) is unrelated, hence "up and down only".
The aim raycast itself (`ComputeFreeAim3rdPersonTargetLocation`) already
ignores all player characters, and the battle dummy spawns hidden with
collision disabled, so neither blocks the ray — both were investigated and
ruled out.

The five-member table therefore stays entirely inside `0..2`:
`[1,0,2,2,1]`. Spot 3 (back-left) mirrors spot 2 (front-left), for which
(−80, +75) is the vanilla leftmost clamp; spot 4 (back-centre) uses the middle
entry (−80, +90). The only other consumer, `ComputeTargetGroups` (ally target
sorting by repeated min-extraction), handles the resulting ties gracefully. The
smoke test asserts every table value stays inside `0..2` so this cannot regress
silently.

## Multi-ally buff target count

Several buff/heal moves were authored for a 3-member party: they fill
`TargetedCharacters` with the party and then apply the effect to
`Min(Array_LastIndex, QTE count)` entries. With their 0–2 QTE successes the
clamp tops out at index 2 — three allies — so a 4th/5th party member is never
buffed, even when every QTE is Perfect.

The affected scripts are:

| Character | Move | Script |
|---|---|---|
| Maelle | Guard Up | `BP_Battle_SkillScript_GuardUp` |
| Lune | Revitalization | `BP_Battle_SkillScript_Revitalization` |
| Lune | Restoration (mod move) | `BP_Battle_SkillScript_Restoration` |
| Gustave | Powerful | `BP_Battle_SkillScript_Gustave_Powerful` |
| Gustave | Zoom Zoom (mod move) | `BP_Battle_SkillScript_Gustave_ZoomZoom` |
| Verso | Powerful | `BP_Battle_SkillScript_Powerful` |
| Sciel | Rush | `BP_Battle_SkillScript_SpeedUp` |
| Monoco | Boucheclier Shield | `BP_Battle_SkillScript_Transfo_BoucheclierShield` |
| Monoco | Orphelin Cheers | `BP_Battle_SkillScript_Transfo_OrphelinBuff` |
| Monoco | Trumpet (random buff) | `BP_Battle_SkillScript_Transfo_TroubadourBuff` |

`PartyFiveKit.PatchMultiAllyBuffTargets` inserts one guarded assignment before
the clamp in each `OnActionEffect`:

```text
if (OnlySuccess):
    SuccessCount = Array_LastIndex(TargetedCharacters)
```

`OnlySuccess` is the existing `GetLastSkillComboInfo` out-parameter
(`FailCount == 0`), so a QTE chain with no misses — including an all-Perfect
chain — now reaches the last party slot. Partial QTE runs keep their original
1–3 scaling. The patch runs last (after every character kit) and reuses
already-staged copies, so it preserves other kits' edits (Verso's Powerful
rank duration, Monoco's mask changes, etc.).

Lune's **Angel's Gift** (`LuneApocalypseKit`) has a separate per-stain AP loop
rather than a buff target count. It is unrolled for 5 `TargetedCharacters`
indices per stain burst (4 bursts × 5 = 20 `GainAP` nodes); the old 4-target
unroll silently left the fifth member unpaid. The array fetch is
bounds-checked, so a smaller party (only possible with the module off) is safe.

## The home-snap debug print

The extra members are *reserves*: they join mid-battle, after the battle-start
sequence has run. `AC_jRPG_CombatMovement.InitialLocation` is captured once,
inside the `OnBattleStart` flow, and never re-captured — so a reserve enters the
AfterTurnEnd reset still holding a stale home. See
`docs/party-of-five-death-reset.md` for the full diagnosis.

To read that value without a log file, the kit also patches
`AC_jRPG_CombatMovement.MoveToInitialLocation` (the latent function that snaps a
character home, at spawn, on resurrection and at every reset) to print:

```
[party5] InitialLocation = X=… Y=… Z=…
[party5] TargetLocation  = X=… Y=… Z=…
```

The stock equivalents go through `PrintStringInLog` (log file only, invisible in
play); these use `PrintString` with both to-screen and to-log flags. Everything
used is already imported in that asset, so no import is fabricated.

This is the safest insert shape available: it lands *before* the stub's
`EX_LocalFinalFunction` jump into the ubergraph, a function the kit does not
touch. That jump carries the only absolute offset in the stub, so no flow offset
anywhere in the package moves — the smoke test asserts the ubergraph is still
byte-identical to vanilla and that every other function is unchanged too.

Turn it off (for a clean release build) with `--no-resetdebug`.

## How the spawn patch works

`SpawnPlayerCharactersFromParty` maps each party slot through
`RemapPartyIndexToBattleSpotIndex` and calls `Spawn Player Character`, which
places the actor at the transform returned by
`GetProperBattleSpawnPointTransform(EnemySpot?, SpotIndex, FormationType)`.

That function iterates the battle map's `BP_jRPG_CharacterBattleSpot` child
actors and matches `SlotIndex == SpotIndex`. Vanilla maps have slot indices
0/1/2 only. The kit:

1. Extends the match to `slot == spotIndex OR (!EnemySpot? AND
   ((spotIndex == 3 AND slot == 2) OR (spotIndex == 4 AND slot == 0)))`
   — party slots 4/5 reuse the left-hand and centre anchors. Enemy spawns are
   explicitly excluded from both aliases.
2. Replaces the character-branch `MakeTransform` location with
   `spawnLocation + GetForwardVector(arrowRot) * F(spot)
                 + GetRightVector(arrowRot)   * R(spot)`
   where `F`/`R` are nested `SelectFloat` chains on the spot index, and
   `arrowRot` is the branch's existing arrow rotation.
3. Widens **two hard-coded 3s in `Spawn Player Character`** to the party
   size. These have nothing to do with positioning and were the reason only
   three members ever appeared in battle:
   - `GetAllPlayerCharacters().Length < 3` — the "free spots left" gate; once
     three members are spawned it asserts and **returns without spawning**,
     so the 4th/5th never exist.
   - `(spot + 1) % 3` — the collision wrap when a spot is already taken.

   Both are same-size `EX_IntConst` edits, so no flow offsets shift. The wrap
   now ranges over five spots, consistent with the alias.

Every flow offset is remapped and validated (`party5-smoke` asserts every jump
target lands on a statement start, switch offsets keep their in-statement
positions, re-serialization is byte-stable, and no other function's bytecode
changed).

## Verification status

`party5-smoke` covers: cap, remap table, the computed front/back layout and
minimum spacing, jump integrity, alias wiring (including the exact
spot 3 → anchor 2 and spot 4 → anchor 0 pairs), the overworld follow-point
override on the world character's component template, formation values in the
offset expression, the two widened spawn caps, companion points, the
team-menu gates, the player spot-order table, the no-miss multi-ally buff
guards, the kit's own `Verify()`, byte stability, per-function diffing against
vanilla, and — importantly — that every `EX_SwitchValue` `EndGotoOffset`/case
`NextOffset` keeps its exact in-statement position.

For the debug print it additionally asserts that both statements sit *before*
the latent-action jump, that the jump still targets the untouched ubergraph, that
the latent-action parameter writes and the `Return`/`EndOfScript` tail are
byte-identical to vanilla, that no local-variable reference from the template
function survives, that the ubergraph is byte-identical to vanilla, and that no
other function in the asset changed.

> Flow-offset correctness: `EX_SwitchValue` offsets point *inside* the switch
> expression, not at statement starts. An early version remapped them by
> statement ordinal, which corrupted the arrow-select switch in
> `GetProperBattleSpawnPointTransform` and froze the game on battle entry. The
> remap is now linear (statement start + original in-statement delta), and the
> smoke asserts it.

> Import shape: `GetRightVector` does not exist as an import in this asset, so it
> is added by cloning the existing `GetForwardVector` import (same
> `ClassPackage`/`ClassName`/`OuterIndex`). Engine-class function imports carry
> `ClassName=Object` while BP-generated ones carry `Function`, and a mismatched
> or fabricated import fails to resolve — the VM then crashes on the first call.

## Bisect ladder (crash isolation)

```
$CLI party5 --pack E33PartyFive_P --install              # full (formation offsets + debug print)
$CLI party5 --pack E33PartyFive_NoFormation_P --no-formation
$CLI party5 --pack E33PartyFive_NoDebug_P --no-resetdebug
$CLI party5 --pack E33PartyFive_CapOnly_P --no-spawn
```

Build the last three with the flags `--no-formation` / `--no-resetdebug` /
`--no-spawn`; swap in by deleting `~mods/E33PartyFive_P.*` and copying the
chosen variant's three files. `NoFormation` isolates the offset math;
`NoDebug` isolates the diagnostic print; `CapOnly` isolates the party-cap work.

In-game checks still needed (cannot be run headless):

- battle start with 5 members: front 1/2 and back 3/5/4 placement, no overlap;
- camera framing of the wider two-row formation (paths/focus points are fixed per map);
- battle HUD portraits (`WBP_HUD_DisplayCharacters`) and turn order
  (`WBP_HUD_TurnOrder`) at 5 members;
- world exploration with 4 AI companions.

If framing needs tuning, adjust `PartyFiveKit.Settings.Formation` and rebuild —
no other code changes required.

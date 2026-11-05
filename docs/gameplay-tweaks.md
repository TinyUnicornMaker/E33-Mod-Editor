# Gameplay Tweaks

The **Gameplay Tweaks** module (id `gameplay-tweaks`) writes optional balance
changes into the packed mod only; the vanilla work tables stay untouched. Its
settings live in `GameplayTweaksKit.Settings`, persist to
`work/gameplay_tweaks.json` (and mirror into the project sidecar under
`"gameplayTweaks"`), and are applied on `rebuild-mod` (CLI) / the standalone generator.

## Dodge and parry windows (`DodgeWindow`, `ParryWindow`)

The global defense windows live in
`DT_Balancing_BattleDifficultySettings`: `ImmuneDuration_Dodge_Normal/Hard`
and `ImmuneDuration_Parry_Normal/Hard`. Vanilla is `0.22 s` dodge and
`0.15 s` parry; the Expedition Infinity preset is `0.165 s` / `0.1125 s`.
`BP_jRPG_Character_Battle_Base.ApplyDifficultySettings` copies the rows into
each battle character's `DodgeImmuneDuration` / `ParryImmuneDuration` at spawn.

`TightenDefenseWindows` is the on/off switch for the whole change. When it is
off, the table is not staged (and any previously staged copy is deleted);
`DodgeWindow` / `ParryWindow` keep their last values so turning the switch back
on restores the preset. `IsVanilla` treats the values as inert while the switch
is off.

## Tutorials (`DisableTutorials`)

Tutorials are **force-disabled by default** rather than tuned. The scripted
Maelle and Curator fights depend on fixed animation timing and dead-ended with
the tightened windows, so the mod now bypasses them entirely:

- `TutorialDisableKit` rewrites
  `BP_TutorialSystemComponent.AreTutorialsEnabled` to return `false`. Every
  tutorial card, hint, live lesson and `BP_ConditionChecker_AreTutorialsEnabled`
  flows through that function, so none of them can activate.
- `DT_jRPG_Encounters` is repointed from each tutorial enemy row to its plain
  no-tutorial counterpart: Maelle, Maelle civilian, Curator jump, Lancelier,
  Évêque shield, Chalier gradient counter, Volester flying and the Sciel
  tutorial. The narrative fights therefore use the normal enemy blueprints even
  if a level action still requests the tutorial encounter row.
- Two of those tutorials (Stalact's gradient lesson and Pelerin at Monoco's
  Station) share a name with a late-game native row. Pointing the encounter at
  `MM_Stalact` / `MM_Pelerin` would import their Frozen Hearts T4 randomizer
  band into the level-25 fight (the reported "over-inflated Stalact") and swap
  the tutorial drops for the native row's. Instead, those encounters stay on
  `MM_Stalact_GradientAttackTutorial` / `MM_Pelerin_MonocoTuto`, and
  `TutorialDisableKit` replaces only those rows' actor class with the native
  row's plain blueprint. Level, stats and `PossibleLoot` are untouched, so the
  drops are exactly the tutorial variant's. The swap is re-applied after the
  randomizer restores/stages `DT_jRPG_Enemies` (`rando-smoke` asserts it).
- The repoint is applied **again late** in `FullPackKit` and the GUI pack,
  after `RandomizerKit` restores/re-stages `DT_jRPG_Encounters` and
  `BossVenueKit` edits it. `ApplyLate` is idempotent.
- On revert, only the encounter rows that vanilla pointed at a tutorial enemy
  are restored, so randomizer/boss-venue edits elsewhere in the table are
  preserved.

`TightenDefenseWindows` remains independent: turning it off ships vanilla
windows, but tutorials stay disabled unless `DisableTutorials` is also turned
off.

**Verification.** `tweaks-smoke` checks the Normal/Hard rows, Easy untouched,
that `AreTutorialsEnabled` is a single `false` return with valid flow targets,
and that no tutorial enemy row is referenced by `DT_jRPG_Encounters`. The
revert pass asserts `BP_TutorialSystemComponent` is removed.

**Not yet wired:** jump and gradient timing. Tutorials being off means those
lessons are skipped too; when the standalone seed tool adds jump/gradient
presets, the same `DisableTutorials` switch will keep their scripted lessons
out of the way.

## Auto Picto Mastery (`AutoPictoMastery`)

Vanilla pictos master after `LuminaUnlockStepsCount` equipped battles — 4 for
most rows, 3 for a few. The game already ships a debug switch for this:
`Sandfall.LearnAnyLuminaAfterOneBattle`, registered from
`BP_jRPG_Controller_World.DEBUG_RegisterCVars`.
`BP_CharacterData.SetPassiveEffectSharedProgression` reads it and, when true,
writes the shared progression straight to the mastered shape
(`IsLearnt = true`, full `LearntSteps`) on the first progress tick instead of
waiting for the row count.

`AutoPictoMastery` flips that CVar's registered default from `false` to `true`.
It is a one-byte `EX_False` → `EX_True` swap inside the existing
`RegisterBoolCVar` call, so no jump targets or bytecode offsets move.
`AutoPictoMasteryKit.Verify` re-reads the call and asserts exactly that shape.

The kit stages `BP_jRPG_Controller_World`; turning the switch off deletes the
staged controller (and `HasLeftoverAssets`/`RemoveOwnedAssets` cover the revert
path). It does **not** grant pictos the player has not found and does not touch
`DT_PassiveEffects`; it only removes the multi-battle mastery grind once a
picto is equipped. For a save-side shortcut, the Save Editor's **Master all
found** action still exists.

## Instant Picto Mastery (`InstantPictoMastery`)

`PictoInstantMasteryKit` hooks the acquisition funnel itself. Every chest,
enemy drop, shop purchase, quest reward and save fill goes through
`AC_jRPG_InventoryManager.AddItemToInventory`, which calls
`BP_DataAsset_Item.OnAddedToInventory(InventoryManager, ItemID, LootContext,
GeneratedItem)` on the item's data asset. The picto data asset inherits the
base implementation, and the base implementation is an empty stub.

The kit rewrites that stub to:

1. cast `Self` to `BP_DataAsset_Item_Pictos_C` and return for every non-picto
   item (vanilla behaviour is preserved for shards, consumables, chroma, …);
2. call the picto's own `GetUnlockedPassiveEffects` (the same
   `PassiveEffects[].Row.RowName` projection the battle path uses);
3. get the custom game instance from
   `InventoryManager.GameInstanceReference` and take the first character from
   `GetCharacterCollection`;
4. call that character's `LearnPassiveEffectShared` for the passive ID, which
   writes the shared progression as `LearntSteps = LuminaUnlockStepsCount` and
   `IsLearnt = true`.

Any character can write the shared progression map, so the first collection
entry is only a writer; the picto is available to every character immediately.
The call is idempotent, and it runs before the inventory notification, so the
picto arrives already mastered.

The hook stages `BP_DataAsset_Item`; turning the switch off deletes it. It does
not need the one-battle CVar, so `AutoPictoMastery` can stay off when
`InstantPictoMastery` is on. Existing pictos that were picked up before the
hook was installed are not retroactively mastered — use the one-battle switch
or the Save Editor's **Master all found** for those.

### Hook bytecode rules (why the first builds crashed)

The engine validates function locals and script-call shapes at load/execute
time. Three details matter here and are covered by
`PictoInstantMasteryKit.Verify`:

- **Every local needs `ArrayDim = 1`.** UAssetAPI's `FProperty.ArrayDim`
  defaults to `NotAnArray (0)`; the engine allocates a local slot as
  `ElementSize * ArrayDim`, so a default-constructed local is zero bytes.
- **Out-parameter calls are bare `EX_Context` statements.** `GetUnlockedPassiveEffects`
  and `GetCharacterCollection` return only through their out arrays, so the
  compiler emits `Context(…)` without an enclosing `EX_Let`. Wrapping them in
  `EX_Let` makes the VM try to read a non-existent return value.
- **`Array_Get` / `Array_Length` go through the library CDO and take an out
  item.** They are compiled as
  `Context(Default__KismetArrayLibrary).Array_Get(array, index, outItem)`; the
  third parameter is the retrieved element. `Greater_IntInt`, by contrast, is
  `EX_CallMath`, not `EX_FinalFunction`. The earlier two-parameter `Array_Get`
  was a crash source.
- **No generated bool locals.** This build's loader rejected the generated
  `FBoolProperty` (`E33PictoHasAny size 0`) even with `ArrayDim=1` and
  `FieldSize=1`. The hook branches on direct cast / `EX_CallMath` expressions
  instead, so it has no bool local to serialize.

With those shapes, the installed hook matches the game's own
`GetPassiveEffectProgressionFromItem` bytecode and passes `Verify` after packing.
**In-game status: PASS (2026-10-27, 1.3 English 654321) — confirmed on pickup.**

Related built-in debug CVars (not wired here): `Sandfall.LearnAllLuminasOnBoot`
masters every `DT_PassiveEffects` row at boot, which unlocks every lumina
regardless of what the save has found.

## Battle proc cards (`PictoPopupCap`)

**What vanilla does.** Every passive/picto proc creates a card through
`WBP_PassivePopupContainer.AddPassiveEffectNotification`. The container keeps a
`TMap<FName, WBP_PassiveEffectNotification*> ExistingPassivePopups` so a repeated
proc of a passive that is already on screen calls that card's `Increment`
instead of spawning a duplicate. A passive that is *not* in the map gets a new
`WBP_PassiveEffectNotification`, which plays its appear animation, sets a
`DisappearTime` (3 s) timer, and removes its map entry when the timer fires.
There is no maximum: 20–30 equipped pictos whose turn-start effects all trigger
together produce 20–30 live cards and cover the HUD.

**What this does.** `PictoPopupCapKit` inserts a guard at the top of
`AddPassiveEffectNotification`, immediately before the card-creation call:

```
keys = ExistingPassivePopups.Keys()
if (Array_IsValidIndex(keys, Cap - 1)) return;   // Cap cards are already live
```

The guard is inserted between vanilla's two entry pushes: the first saves the
function-exit address and the second saves the duplicate/increment path. That
leaves exactly one address on the execution-flow stack, so the full-screen path
returns with one clean `EX_PopExecutionFlow`. (After the duplicate lookup the
not-found path has two addresses on the stack, and a pop there would land on the
duplicate handler instead of returning.) While the screen is full a repeat proc
of a visible card is ignored too — the card stays up and only its counter tick is
skipped; below the cap vanilla's duplicate path still increments it. A dropped
passive can appear again after one of the visible cards expires. The map is the
exact live-card set — the expiry callback removes the entry — so there is no
separate counter to maintain.

The guard is bytecode only: `Map_Keys` + `Array_IsValidIndex` are existing
engine library calls (both used elsewhere in the shipped UI). The two working
locals are shallow-cloned from this function's own `FName[]` and `bool` locals,
and the insertion shifts every absolute jump target
(`MayhemFixKit.ShiftAbsolute` + `PartyFiveKit.VerifyJumps`).

**Settings.** `PictoPopupCap` (int, default `5`, range `0`–`30`; `0` is vanilla
and the widget is not staged). The cap covers all passive/buff proc cards, not
only picto-sourced ones — the container does not distinguish the source.
`HasLeftoverAssets`/`RemoveOwnedAssets` include
`WBP_PassivePopupContainer.uasset`, and the donor filter list extracts it for a
fresh standalone install.

**Verification.** `popup-cap-smoke` stages the widget, asserts the guard shape
(`Map_Keys`, `Array_IsValidIndex` literal `Cap - 1`, fall-through to creation,
`PopExecutionFlow` on the full path), re-applies with a different cap, and
asserts cap `0` removes the widget. `tweaks-smoke` also checks the staged asset
at the pack's configured cap and that the vanilla revert deletes it.

## Attribute point cap (`AttributeCap`)

**What the vanilla 99 cap actually is.** It is *not* a hardcoded clamp. Each
attribute's scaling table carries exactly the rows `Points_0 … Points_99`, and
nothing reads a `Points_100`. `BP_CharacterData`
(`AssignPointToAttribute`, `GetMaxAttributePoints`, `GetAttributePointsToScaling`,
`GetCurrentAttributeScaling`) contains no 99/100 attribute constant — the sibling
level table's code documents the convention **"missing row → fall back to the
last valid index"**, so points above 99 simply reuse `Points_99` and stop
scaling.

**Two coupled things must change to lift it**, which is why the component does
both:

1. **The five scaling tables** — `Agility`, `Strength` (Might), `Vitality`,
   `Defense`, `Luck`, all under
   `extracted/Sandfall/Content/Gameplay/CharacterData/AttributeScalingTables/`.
   They are structurally identical (`S_Stats_AttributesScaling`, a `Stats` map,
   row-name base `Points`). Rows `Points_100 … ` are cloned from `Points_99` and
   filled by continuing the `Points_90`→`Points_99` slope per stat.
   Note the extension target is `max(AttributeCap, LevelCap × AttributePointsPerLevel)`
   — so raising the level cap also extends these tables (that path predates the
   cap setting).
2. **The assignment UI clamp** — `WBP_Raw_Attributes_Display`
   (`UI/Widgets/InGame_Menu/Stats_Panel/`) is the one asset that owns
   `AllowedMaxValue`, and both `SetAllowedMaxValue` and `SetAllowedMinValue`
   contain a literal `Clamp(arg, 0, 99)` in bytecode. Without patching those two
   constants the wheel refuses to assign past 99 **no matter how many rows the
   tables have**. The kit stages a patched copy of this widget and rewrites both
   constants to `AttributeCap`.

⚠ The widget is staged from a pre-extracted copy in
`work/tables/gameplay_tweaks/…`, not extracted on demand: `Retoc.ToLegacy`
refuses to run while `Paks/~mods` holds an installed mod container.

## Attribute gain curve

The five scaling tables are not left vanilla below the cap: `ApplyAttributeCurve`
rewrites the whole point curve so stats stop tailing off as values climb.

| points | shape |
| --- | --- |
| 1–80 | straight line from 0 landing on the vanilla `Points_80` value — vanilla's uneven early gains (Vitality runs 6, 13, … 19, 18, 16, 14, 11, 10, 7) become one flat per-point rate |
| 81–200 | accelerates — quadratic, continuous in value *and* slope at 80 — so `Points_200` lands on **3 × the vanilla `Points_99` value** |
| 201+ | continues at the 200 slope, so the table stays monotone to `AttributeTableCap` |

Constants: `AttributeCurveKnee` (80), `AttributeCurveTarget` (200),
`AttributeCurveTargetMultiple` (3.0). Resulting endpoints:

| attribute | vanilla v99 | new v99 | new v200 |
| --- | --- | --- | --- |
| Vitality | 1389 | 1552 | 4167 |
| Strength | 156 | 175 | 468 |
| Agility | 182 | 166 | 546 |
| Defense | 547 | 498 | 1641 |
| Luck | 139 | 161 | 417 |

Note `Points_80` is preserved exactly and `Points_0` stays 0, so the opening
curve is pinned to vanilla at both ends; 81–99 is slightly stronger than vanilla
because the acceleration starts at the knee.

**Setting.** `AttributeCap`, integer, range `99` (vanilla) … `500`, default `99`.
`99` means "vanilla" — `IsVanilla` includes it, so nothing is staged and any
previously staged copies are removed.

**Point pool.** Points available to spend are
`level cap × AttributePointsPerLevel`. The mod defaults to 2 points/level with a
250 level cap, which is far more than 200; play the level-cap and
points-per-level fields too if you want a full 200 in a single attribute.

**Cleanup parity.** The five tables and the widget are listed in both
`HasLeftoverAssets` (so reverting to vanilla still triggers the cleanup pass) and
`RemoveOwnedAssets` (which deletes them).

**Verification.** `tweaks-smoke` asserts all five tables reach
`Points_200` (and exactly `AttributeTableCap`, with no row past it) and dumps the
widget's patched constants. In-game: level up, assign an attribute past 99, and
confirm the stat keeps scaling and the wheel allows it.

## Enemy and lumina balance (`BuffRegularEnemies` / `ReduceLuminaDrops`)

Two optional, off-by-default switches ship in the Gameplay Values page under
**Enemy & lumina balance**. Both run **after** `RandomizerKit` in the GUI pack
and `FullPackKit`, so they scale the seed's staged values instead of being
overwritten by it.

### Buffed regular enemies

`RegularEnemyBuffKit` multiplies the staged `DT_jRPG_Enemies.EnemyScaling`
fields for ordinary, non-boss rows:

| Tier | HP | Speed | Physical / Magical Attack |
| --- | --- | --- | --- |
| T1 | +30% | +20% | +10% |
| T2 | +50% | +30% | +20% |
| T3 | +200% | +40% | +50% |
| T4 | +500% | +50% | +100% |
| T5 | +1000% | +100% | +200% |

"Regular" means: not cut content, not a `Boss` / `Alpha` / `SuperBoss`
archetype, and not a real boss row under `RandomizerKit.IsRealBossRow`.
Elites count as regular enemies; merchants and real boss rows do not. Tutorial
rows and Petank lumina enemies are not treated as bosses just because the table
sets `IsBoss`, matching the randomizer's own boss filter.

The five-entry bonus arrays (`RegularEnemyHpBonus`, `RegularEnemySpeedBonus`,
`RegularEnemyAttackBonus`) live in `work/gameplay_tweaks.json` so presets can be
tuned without touching the GUI. `balance-smoke` asserts a T1 and a T5 regular
row get the right factors and that a boss row is untouched.

### Adjusted regular lumina

`LuminaDropKit` scales every regular source of `Consumable_LuminaPoint` by
`LuminaDropMultiplier`. Values below `1` reduce drops; values above `1` increase
them. The default is `1.0` (vanilla); the standalone generator accepts
`0.1–5.0`.

- `DT_ChestsContent` loot sections (drop chance, weight or stack quantity),
- `DT_jRPG_Enemies.PossibleLoot` (chance for one-point drops; integer-scaled
  stacks for boss/Petank payouts), and
- every `DT_Merchant_*` stock row (`Quantity`).

Dialogue reward blueprints are never opened or staged, so Blanche's
`BP_Dialogue_JudgeOfMercy` pacifist grant (and every other dialogue payout)
stays vanilla.

A chest made entirely of one-point lumina would otherwise come up empty when
its section chance is halved. The kit therefore swaps half of those one-point
sections for a tier-appropriate Chroma pack (Regular / Large / ExtraLarge)
instead, so the expected lumina still halves and the chest always yields an
item. Increasing a chest beyond 100% raises stack quantities after the chance
caps out. `balance-smoke` checks the enemy chance/stack splits, merchant stock,
the dialogue exclusion, and the pure-lumina chest alternation.

## Critical damage (`CritDamageIncrease` + `CritRateDamageScaling`)

**What vanilla does.** Crit *chance* is `BP_BattleDamages.GetCriticalRollToBeat()`
— the character's crit stat (a percentage) plus flat crit-chance bonuses, rolled
against 0–100. Crit *damage* is a separate, flat modifier: when `IsCriticalHit`,
`BP_BattleDamages.ApplyCriticalHitModifiers` adds a damage modifier whose value is
`1.5 × ExtraCriticalHitDamageMultiplier`, where the `1.5` is a `DoubleConst` baked
into the bytecode and `ExtraCriticalHitDamageMultiplier` carries the per-skill /
picto / passive crit-damage bonuses.

⚠ The `DT_Balancing_MainCharacterSkills.Generic_CriticalStrikeDMGIncrease` row
(0.5) is **read by nothing** in the shipped battle code — the base is the baked
`1.5`. The old `CritDamageIncrease` setting used to write that row (a no-op); it
now edits the real constant instead, and the row is no longer written.

**What this does.** `CritDamageKit` edits `BP_BattleDamages.ApplyCriticalHitModifiers`:

```
critMultiplier = (1 + CritDamageIncrease) × [ max(1, critRate/100) ] × ExtraCriticalHitDamageMultiplier
```

- `CritDamageIncrease` rewrites the baked base constant to `1 + addend`.
  Vanilla `0.5` → ×1.5; `1.0` → ×2.0; `1.5` → ×2.5.
- `CritRateDamageScaling` wraps that base in `max(1, critRate/100)`:

  | crit rate | scaling factor |
  | --- | --- |
  | ≤100% | ×1 (base unchanged) |
  | 150% | ×1.5 |
  | 200% | ×2 |

The floor keeps a crit from ever dealing **less** than the addend base (a raw
`critRate/100` would make a 50% crit a ×0.5 hit). The rate is read through the
same `GetCriticalRollToBeat()` the roll uses, so flat crit-chance pictos/passives
raise crit damage too.

**Why it is bytecode, not data.** No table or curve links crit rate to crit
damage, so the kit edits the function directly: it rewrites the `MakeLiteralDouble`
constant, and (for scaling) inserts a clone of the function's own
`GetCriticalRollToBeat()` call and wraps the base operand in
`Multiply(base, FMax(crit × 0.01, 1))`. It reuses only existing imports/locals and
an in-asset statement clone, so it round-trips; every jump target is re-shifted
and asserted against statement starts (`PartyFiveKit.VerifyJumps`).

**Settings.** `CritDamageIncrease` (float, default `0.5` = vanilla) and
`CritRateDamageScaling` (bool, default `false`). `IsVanilla` includes both, the
staged `BP_BattleDamages.uasset` is listed in `HasLeftoverAssets`/
`RemoveOwnedAssets`, and it is deleted whenever both are vanilla. ⚠
`DamageLimitKit.RemoveThreeTier` also deletes that asset — it runs before
`GameplayTweaksKit.Apply` in both the GUI pack and `FullPackKit`, so there is no
conflict, but keep that order if you move the kits around.

**Verification.** `tweaks-smoke` runs the splice for scaling-only, addend-only and
addend+scaling, asserts the base constant equals `1 + addend` and (when scaling)
the `FMax(…, 1.0)` floor and the `CallFunc_GetCriticalRollToBeat_Value` read are
present with valid jump targets, then reverts and asserts the asset is gone.

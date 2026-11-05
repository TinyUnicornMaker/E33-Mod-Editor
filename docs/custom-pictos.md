# How to correctly make custom pictos

This is the working recipe the editor uses (`PictosCreateKit`). A pictos that
shows up in the collection list but hovers as **Accelerating Heal**, with no
level and no stats, is almost always a failed Data Asset load — not a
localisation-only bug.

Vanilla pictos are not one table row. They are a bundle of packages that the
game looks up by **IoStore package ID**, then by **item row name**.

## What a pictos actually is

Each worn pictos is all of these, keyed by the same id (example `Painter`):

| Piece | Table / asset | Why it exists |
| --- | --- | --- |
| Item identity | `DT_jRPG_Items` (parent) | Runtime lookup. **Not** extra rows on the composite. |
| Composite dump | `DT_jRPG_Items_Composite` | Editor consistency only. UE `PostLoad` empties the composite and rebuilds it from `ParentTables`. Extra composite rows are discarded. |
| Passive / lumina | `DT_PassiveEffects` | Name, description, `BattleBuffs` soft path to the Blueprint. |
| Stat multipliers | `DT_PictosDefinitions` | `BaseStatMultipliers` (HP / Def / Spd / Crit) and `ChromaValueMultiplier`. This is what the HUD uses for the numbered bonuses. |
| Hover leftovers | `Pictos_ItemStats` on the item row | Parallel raw numbers. Empty here + failed DA load = “no stats”. |
| Balancing floats | `DT_Balancing_Luminas` | Only if the Blueprint reads a balancing key (status affinities). |
| Gear Data Asset | `DA_GearPictos_<Id>` | `BP_DataAsset_Item_Pictos_C`. Tooltip widget binds this. Failed load → widget CDO default = AcceleratorHeal. |
| Effect Blueprint | `BP_LU_<Id>` | The actual lumina logic. Soft-referenced from `DT_PassiveEffects.BattleBuffs`. |
| Icon / learn pips | `DT_PictoIcons` | `WBP_PictoIcon` looks up this table by pictos id. **Missing row → gold “learnt” frame at 0/4 battles** (the widget skips `LoadPassiveEffectLearnState` and keeps the CDO). Clone the donor’s row; same textures are fine. Stage under `UI/Resources/DataTables/PictoIcons/` (capital T) — extract writes `Datatables` and that path never loads. |
| Optional strings | `ST_PassiveEffects` | Only useful if FText is a `StringTableEntry` into a key that **already exists in cooked locres**. New keys are missing in-game. |

Inventory / save:

- **Found** = `InventoryItems` name → count.
- **Worn** = `EquippedItemsPerSlot` enumerator pictos, slots 0–2.
- **Lumina (unequipped effect)** = `EquippedPassiveEffects`.
- **Level / mastered** = `PassiveEffectsProgressions` (and the matching weapon-progression-style entry the save writer already adds).
- Unticking Found also clears the id from worn slots and
  `EquippedPassiveEffects`, so no dangling references stay in the save.

A save that contains the id is not enough. The packed `_P` mod must contain
**unique** DA + BP packages, and the game must be fully quit so IoStore reloads
`~mods`.

## Do not invent data the cooked files do not have

- There is one shared pictos level curve (`DT_EquipmentScalingTable_Pictos`).
  Per-pictos “unique curves” do not exist. Bonuses are
  `curve(level) × BaseStatMultipliers`.
- Do not put generated rows only onto `DT_jRPG_Items_Composite`.
- Do not edit shops or drops from the pictos creator. Placement is a separate
  Chests / Drops / Randomizer concern.
- Work tables under `work/tables` stay vanilla. Generated files go to
  `work/mod` then `Export/` (copied to `Paks/~mods/E33ModEditor_P`).

## Clone from a real vanilla pictos

Do not author a DA/BP from scratch. Clone a neighbour that already has the
right graph:

- Element conversion → `DA_GearPictos_Painter` + `BP_LU_SkillsConversionToVoid`
  (or Dark / Physical when those graphs already exist).
- Self-status damage bonus → `DA_GearPictos_InvertedAffinity` +
  `BP_LU_25PercentMoreDamagewhenCharacterExhaust`.
- Magnitude variants (Glass Cannon II/III, Consuming Attack 250, Roulette II/III, …) → **Pictos magnitude variants** toggle. Clone the matching vanilla DA/BP with new balancing keys. Not placed in shops or drops.

### New magnitude families (2026-09)

Ladders whose vanilla row is reworked into tier I ("<Display> I", renamed +
retuned to the ladder value) — see `PictosMagnitudeKit.BaseTierRenames`,
`ReworkNewVanillaBases` + the tier-I edits in `InjectBalancing`:

- Immaculate I–V (10/20/30/50/100% increased damage until a hit is received)
- Alternating Critical I–V (10–50% on the non-Crit after a Crit)
- AP Discount I–II (−1/−2 AP)
- First Life I–III (5/5, 10/10, 21/21 increased-until-death / decreased-on-death)
- Augmented First Strike I–V (5/10/20/30/50% on first hit, once per battle)
- Confident I–V (take 5–25% less damage, can't be Healed; const-patched BP — the
  vanilla 0.5 received multiplier is a `DoubleConst`, so tier I stages a patched
  BP copy and tiers II–V patch their clones via `PatchConstValue`)
- Attack Lifesteal I–III (5/10/20% Health recovered on Base Attack;
  `AttackLifesteal_DamagePercentageToHeal`)
- Gradient Overcharge I–III (I = renamed vanilla: 3 charges for 200% more; II =
  5 charges for 400% more; III = 10 charges for 1000% more — per-tier
  `GradientConsumedAmount`/`DamageMultiplier` rows)
- Macabre Power I–III (Clea's Death rework: allies gain 5/10/15% damage on your
  death until they die; two-asset family — `CloneCleasBuff` also clones the
  companion `BP_LU_<Id>_Buff`)
- At Death's Door I (renamed vanilla LastStand — already 50% below 10% HP) and
  Beneficial Contamination I (rename-only) ship now; their II/III tiers are
  **deferred**: the effect BPs (`BP_LU_LastStand`, `BP_LU_BeneficialContamination`)
  are not extractable while the installed mod container shadows the paks
  (move `Paks/~mods` aside to extract, then reinstall), and those donors carry
  no balancing row to ladder from.
- Energy Master I–II (+1 / +2 on every AP gain)
- The Best Defense I–V (can't Dodge or Parry; +10/25/50/100/200% damage)
- Energising Pain I–V (same +1 AP on hit / no parry AP; stack; rising cost)
- First Offensive I–V (+10/25/50/100/200% on first hit dealt and taken)
- Energising Turn I–III (same +1 AP on turn start; stack)
- Empowering Jumps I–V (+10/25/50/100/200% per jump counter this turn)
- Critical Break I–V (+10/20/30/40/50% Break on Crit) — row `BreakDamageOnCrit`,
  **not** `CriticalBreak` (that id is Critical Stun)
- Burning Death I–V (3/5/10/20/50 Burn on death)
- Energising Death I–V (+1/2/3/4/5 AP to allies on death; I nerfed from +4)
- Random Defense I–V (incoming 50% or 150%, like Roulette)
- Confident Fighter I–V (+5/10/20/50/100% damage, can't be Healed)
- Breaking Counter I–V (+10/25/50/100/200% Break on counter; const-patched BP)
- Healing Parry I–III (3/5/10% Health on Parry; I vanilla, II/III const-patched)
- Perilous Parry I–III (same 2× incoming / +1 AP on parry; stack; rising cost)
- Parry Specialist I–III (each −10% parry window, ×2 counter damage as a
  direct multiplier; tiers stack; 5/10/20 lumina)
- Empowering Parry I–III (+5/10/15% per parry)
- Lucky Aim I–III (same 15% chance +1 AP on Free Aim; stack; 1 / 7 / 21 lumina —
  the 30% roll in `BP_LU_APFreeAim_50Recover.ProcessAPChange` is retuned on the
  vanilla base and on both clones)
- Full Strength I–V (+5/10/20/30/50% on full Health; 5/10/20/30/50 lumina;
  I const-patched)
- Charging Critical I (vanilla once/turn 20%) / II (20% every crit)
- Infinite (The One donor; max Health 3,333,333). **Only drops from Act 3
  Lumière Renoir** (`L_Boss_Curator`); never chests, shops, or other enemies.
- Death's Dividend (clone of weapon `DamageBonusOnRevive`: +10% damage per
  revive this battle)
- Critical Chain I (new family, Tier 1 only): each consecutive Critical Hit
  +1% damage, resets on a non-Critical hit. Cloned from Empowering Parry
  (`BP_LU_ReinforcementParade`, int counter × per-event key) with new Kismet
  surgery (`PatchCriticalChain`): parry-increment, turn-end reset and
  hit-taken reset neutered to unconditional skips, and a crit-gated
  counter update (`IsCriticalHit` Context, same shape as Alternating
  Critical) inserted before the parent `ProcessDealtRawDamages` call.

Renames/ladder text mirror the vanilla wording with the tier value; ST keys
(`PASSIVE_<RowId>_Name/_Description`) are rewritten alongside the row FText.

Copy **both** `.uasset` and `.uexp` (`FileCopy.AssetPair`). Never `File.Copy`
when source and dest resolve to the same path (Unix throws
`IO_SharingViolation_File`).

### The unversioned-CDO trap (why clones came out as Void)

These Blueprints serialize properties **unversioned**: names/types come from
schemas resolved by class name (usmap/jmap, or pulled from dependency packages
on disk). If a clone is opened somewhere its dependencies cannot be found, the
CDO export fails to parse, falls back to raw bytes, and every property edit is
**silently skipped** — that is how Painter clones kept the void `NewElement`
payload of their source. Rules:

- Open the SOURCE from an extraction root that also contains the schema-chain
  siblings (`work/tables/LuminaPassives` for conversions,
  `work/tables/AutoApplyBuff` for Auto buffs + `BP_BattleBuff_AutoApplyBuff`),
  mutate in memory, then `Write(dest)`.
- After editing, re-open must succeed once those schemas are loaded
  (`pictos-smoke` asserts every element/duration value).

Auto-buff duration is **not** balancing data: no Blueprint reads the
`*_BuffDuration` keys (they are dead rows in `DT_Balancing_Luminas`).
`BP_BattleBuff_AutoApplyBuff` copies its child's `BuffTurnDuration`
(`IntPropertyData`, parent CDO default 3) onto the spawned buff, so II/III
variants add/override `BuffTurnDuration` = 5 / 10 on their own CDO.

## Picto magnitude vs weapon-passive magnitude (why the numbers differ)

Several effects exist twice — as a **picto** and as a **weapon passive** — and
both use the same display name (e.g. *Alternating Critical III*, *Critical
Chain II*). **Their ladders are deliberately on different scales.** This is a
balance rule, not drift, so never "fix" one to match the other.

| | How many can you run? | Ladder shape |
| --- | --- | --- |
| Picto | Many at once — a player can own and wear the whole family across the party | Small per-point values, modest tiers |
| Weapon passive | Scarce — usually a single weapon carries a single instance | Large per-point values |

Pictos are stackable: a player can eventually equip every tier of a family, so
their magnitudes are capped low — otherwise a full set trivialises the game and
each extra tier compounds on the last. Weapon passives are rare (one per weapon
slot, and a weapon has only a handful of slots), so each point has to be worth
the slot and the numbers are much larger for the same name.

Shipped pairs:

| Name | Picto (stackable) | Weapon passive (scarce) |
| --- | --- | --- |
| Alternating Critical I–V | 10 / 20 / 30 / 40 / 50 % | 50 / 100 / 200 / 400 / 600 % |
| Critical Chain I–V | 1 / 2 / 3 % (picto I–III only) | 2 / 4 / 6 / 8 / 10 % |
| Full Strength I–V | 5 / 10 / 20 / 30 / 50 % | 20 / 50 / 100 / 200 / 400 % |

Everything else matches: same trigger (Critical hit), same stack cap for
Critical Chain (100), same reset rule (non-Critical hit). Only the per-point
magnitude is scaled to the acquisition model.

Source of truth:

- Pictos — `PictosMagnitudeKit` (`AltCrit`, `CritChain`, `BaseTierRenames`).
- Weapon passives — `Weapons/Data/weapon_effects.json` (`WepFx_AltCrit*`,
  `WepFx_CriticalChain*`); see [custom-weapon-effects.md](custom-weapon-effects.md).

When retuning one side, do **not** mirror the value onto the other — that
reintroduces exactly the overpowered stacking this split exists to prevent.

## Package identity (the Accelerating Heal trap)

IoStore package IDs are hashed from `UAsset.FolderName` (the UE5 package name
`/Game/...`), **not** from the filename on disk.

If every clone keeps `FolderName = /Game/Gameplay/Pictos/Gear/NewLumina/DA_GearPictos_Painter`,
retoc stores **one** chunk per hash. The directory index then shows a leftover
filename of whichever clone won. In-game every custom row still points at a
package the container does not actually contain. The tooltip widget falls back
to its CDO:

- Name / hover: Accelerating Heal
- `Pictos_ItemStats` empty → no stats
- No level line

### Retarget every clone

1. Set `asset.FolderName` to `/Game/` + path under `Content/` without `.uasset`.
   Example dest
   `.../Content/Gameplay/Pictos/Gear/NewLumina/DA_GearPictos_PainterConv_Fire.uasset`
   → `/Game/Gameplay/Pictos/Gear/NewLumina/DA_GearPictos_PainterConv_Fire`.
2. Rewrite the name map: replace the old package path with the new one.
3. Then replace the source stem (`DA_GearPictos_Painter`) with the new stem
   (`DA_GearPictos_PainterConv_Fire`) **only if** `newStem` is not already in
   the string. `Painter` is a prefix of `PainterConv_Fire`; a blind replace
   produces `DA_GearPictos_PainterConv_FireConv_Fire`.
4. After pack, verify with `retoc list --path` on the `_P.utoc`: every DA and
   every BP must be a **distinct** `/Game/...` path. Duplicate hashes collapse.

The `.pak` next to `.utoc`/`.ucas` is a tiny stub. All three files must come
from the **same** pack. `PackCombined` deletes stale Export files first.

## Class identity (the doubled-rename trap)

Clones are renamed in two passes: `RetargetPackage` swaps the **full stem**
(`BP_LU_CursedPower` → `BP_LU_CursedPower3`) across the name map, then the kit
swaps the **short name** (`CursedPower` → `CursedPower3`). An id that *contains*
the donor's short name trips both passes: the class is already
`BP_LU_CursedPower3_C` after the stem pass, and the short pass turns it into
`BP_LU_CursedPower33_C`. Nothing in the package then defines the class that
`DT_PassiveEffects` references, and the game **crashes at battle load** the moment
the picto is equipped — this is what hit Cursed Power II (`…22_C`) and III
(`…33_C`). The other six Powers (`Berserk*`, `Blighted*`) inherit ids that do not
contain `CursedPower`, so only the Cursed tiers were affected.

Rule: swap the short name only when the string does **not** already contain the
new short name. `pictos-smoke` now asserts the invariant for every created BP and
for every passive row pointing at a package we stage:

- each `BP_LU_<stem>.uasset` defines the class `<stem>_C`;
- each `DT_PassiveEffects` row resolves to a class its package defines.

The one-off sweep is the same check by hand: byte-scan every staged
`BP_LU_*.uasset` for `<stem>_C` (300 BPs in the 2026-09 pack — 298 clean, the two
Cursed Power clones broken), then cross-check the passive rows against them.

## Hover text

Vanilla `Item_DisplayName` / `ItemDescription` (and passive Name / Description)
are `FText` `HistoryType = StringTableEntry` into `ST_PassiveEffects`. New keys
are not in the cooked locres, so lookup fails.

Working custom items (including the old E33 randomizer) use:

- `HistoryType = None`
- `Flags = CultureInvariant`
- `Value` and `CultureInvariantString` = the actual display string
- no `TableId`

That is necessary, but **not sufficient**. If the DA still fails to load, the
widget never reads this FText and you still see Accelerating Heal.

Keep descriptions short (one line of what it does). The widget is a hover.

## Stats (vanilla-friendly)

HUD numbers come from `DT_PictosDefinitions.BaseStatMultipliers`, not from
guessing raw HP.

Enum keys (usmap has no friendly names):

- HP `E_jRPG_StatType::NewEnumerator1`
- Defense `NewEnumerator4`
- Crit `NewEnumerator6`
- Speed `NewEnumerator7`

Vanilla pictos typically grant **two** of HP / Def / Spd / Crit, multipliers
summing around 1.9–2.0, chroma 1.0–1.75. Also fill `Pictos_ItemStats` with the
matching hover integers so a successful DA load still has leftover numbers.

Zero a multiplier you are not using (omit the map entry). Do not invent a
fourth stat the shared curve does not drive.

Pictos that grant **Defense** are excluded from Dark Shores / Frozen Hearts
chests (the randomizer's `RandomizerKit.ChestNoGearAreas` + `DefensePictos`
ban — those areas are reachable without combat, so no free Defense there). The
ban set is refreshed from the **packed** `DT_PictosDefinitions` at pack time, so
created pictos are covered automatically — no separate list to keep in sync.

## Categories (tags)

Every picto/passive row has a `Categories` array in `DT_PassiveEffects`
(`TArray<E_PassiveEffectCategory>`). It drives the picto menu's category chips
/ filters; it is not balance data. Vanilla is inconsistent — 438 of 657 rows
have an empty array, including several donors we clone (`Painter`,
`CursedPower`, `BurningDodge`, `RandomDefense`, `LuckyAim`, `ParryHelper`,
`ParrySpecialist`, `LongerDodge`, `AutoCurse`) — so **do not rely on
inheritance**.

The mod keeps this correct centrally:

- `PictoTagCatalog` has an explicit `id → tags` rule for every created,
  revived, or reworked picto, with an optional `Legacy` set (the donor value).
- `PictoTagKit.Apply(modDir)` runs **last** among the picto kits in
  `FullPackKit`, the CLI/standalone build, `pictos-smoke` and `rando-smoke`. It
  repairs empty/stale sets and leaves a category set that is neither the
  catalog's nor the known legacy set alone (treated as a manual edit, with a
  warning).
- `dotnet <CLI> picto-tags [modDir]` repairs an existing package in place
  (default `work/mod`).

Use one primary tag plus context tags when appropriate. Existing values:
`FreeAim`, `BaseAttack`, `AP`, `Heal`, `Shield`, `Critical`, `Damage`,
`Burn`, `Buff`, `StatusEffects`, `Parry`, `Counter`, `Death`, `Solo`,
`Defense`, `Speed`, `Mark`, `Debuff`, `Break`, `Items`, `Gradient`.

The mod appends four values to the shipped enum asset:
`Element` (Painter conversions), `Revive` (revive-triggered pictos),
`Lifesteal` (Attack Lifesteal), and `Luck` (chance/random effects).
`PictoTagKit.EnsureEnum` updates the `E_PassiveEffectCategory` `Names` and
`DisplayNameMap`, stages the asset under
`Sandfall/Content/Gameplay/Lumina/`, and refreshes the editor's extracted
enum cache.

To add a new tag: add the constant/`NewTags` entry in `PictoTagCatalog`,
extend the editor labels in `GameLabels.PassiveCategories`, then verify the
in-game filter/tooltip actually shows the value. A new enum value is valid
data even if a fixed filter UI ignores it.

## Blueprint retarget

After `RetargetPackage`:

- Conversion clones: replace the source short name in the name map, set
  `NewElement` to the damage enum (`NewEnumerator0` Fire … `NewEnumerator7` Void).
- Status-affinity clones: replace the Exhaust buff path, class, dynamic-cast
  node name, and balancing key (`ExhaustAffinity_DamageIncrease` →
  `<Id>_DamageIncrease`). Point `DT_Balancing_Luminas` at that key. Keep the
  bonus at 10% when many of these can stack; vanilla Inverted is reduced to 10%
  on the same pack.

`DT_PassiveEffects.BattleBuffs[0]` must be a `SoftObjectPath` to
`/Game/.../BP_LU_<Id>` + `BP_LU_<Id>_C`. Add the path to
`asset.SoftObjectPathList` when the source asset has that list.

Item row `Pictos_Data` must be an object import of the **new** DA, not the
source Painter / Inverted DA.

## Tables to copy into `work/mod`

Always copy the parents that runtime uses:

- `DT_jRPG_Items`
- `DT_jRPG_Items_Composite` (harmless, not authoritative)
- `DT_PassiveEffects`
- `DT_PictosDefinitions`
- `DT_Balancing_Luminas` (affinities)
- `ST_PassiveEffects` (optional; culture-invariant FText does not need it)

Use `FileCopy.FindTablesAsset` so the source is `work/tables`, never a file
already sitting in `work/mod`.

## Save + install

1. Run `rebuild-mod` / the standalone generator so `Export/E33ModEditor_P.{pak,utoc,ucas}` and `~mods` match.
2. Sidecar `Export/E33ModEditor_P.project.json` records which create toggles
   were on.
3. Save Editor: Found the new ids (Continue slot already had the 30 created
   ids from the visibility pass). Worn via pictos slots, not only lumina.
4. **Fully quit the game** (not the title screen). IoStore does not reload
   `~mods` on Continue alone if the process stayed up.
5. Confirm `Paks` realpath: Steam debian-installation and
   `~/.steam/steam/.../Paks` can be the same tree. One `_P` set.

## Checklist before calling a clone “done”

- [ ] Unique `/Game/...` `FolderName` on every DA and BP
- [ ] `retoc list --path` shows 1:1 paths, no `PainterConv_FireConv_Fire`
- [ ] Item row lives on `DT_jRPG_Items`
- [ ] `Pictos_Data` import points at the new DA
- [ ] Passive `BattleBuffs` soft path points at the new BP
- [ ] FText is culture-invariant, not a missing StringTable key
- [ ] `DT_PictosDefinitions` has two-stat multipliers + chroma
- [ ] `Pictos_ItemStats` has the matching leftover integers
- [ ] No shop / drop edits from the creator
- [ ] Packed pak/utoc/ucas are one matching set in `~mods`
- [ ] Game process was killed after the pack

If the list still shows Accelerating Heal after that, the DA is still not the
package the item row references — dump `FolderName` on the clone and the path
in `Pictos_Data`, they must be the same `/Game/...` stem.

## Cut content: Gatherer A (2026-09-03 investigation)

`GathererA` ("Gatherer A", "[WIP] Gain more Chroma at the end of a battle.") is
shipped cut content. Do **not** try to revive it as a working "more Chroma"
picto — the mechanic it describes was never wired into the game:

- Item row `GathererA` sits in the `Pictos > DoNotUse` loot band (never drops).
- `DA_GearPictos_GathererA` exists and correctly points at passive row
  `DT_PassiveEffects.GathererA`, but that row's `BattleBuffs` is **empty** and
  no `BP_LU_GathererA` effect Blueprint was ever shipped → equipping it does
  nothing.
- Post-battle reward flow (`AC_jRPG_BattleManager.RollBattleRewards`) only
  produces Experience + Gold + item loot. `S_BattleRewardParameters` exposes
  `ExperienceMultiplier` / `GoldMultiplier` (per-battle config from
  arena/dialogue), but there is **no Chroma reward or multiplier** in BP.
- "Chroma" is (a) a per-enemy stat (`EnemyScaling.Chroma`) never converted to
  currency in BP, and (b) ChromaPack hidden items
  (`ChromaPack_Regular/Large/ExtraLarge`, `Multiplier` 2.5/5/10 via
  `DA_Item_Chroma_Size*`) that drop from **chests** (`DT_ChestsContent`), not
  enemies. No vanilla enemy `PossibleLoot` grants Chroma.

The currency payout is native (inventory-side, not visible in BP), so a
Gatherer-style "enemies drop more Chroma" passive has no lever to multiply.

## Revamped roster (2026-08-26)

- **Stun Boost I–V** — vanilla Stun Boost becomes tier I (+10%, lowered from 30%);
  created tiers II–V (+20/50/100/150%) each carry their own balancing key. The
  modifier is additive (serialized type 3), so wearing several tiers sums their
  percentages rather than multiplying them.
- **Powers** (replaces the per-status affinity set): Cursed / Berserk / Blighted
  Power I–III (+5/10/20% while you have the status). Cloned from vanilla
  Cursed Power, which gates on the wearer's own buff component; vanilla
  Cursed Power is reworked into tier I. The old `StatusAffinity_*` set no longer
  exists in the kit.
- **Auto Berserk / Auto Blight** — battle-start statuses (vanilla Auto Curse
  untouched); the eight auto duration upgrades have been removed.
- **SOS Curse / SOS Berserk / SOS Blight** — status when you fall below half
  Health (5 turns).
- **Revive with Curse / Berserk / Blight** — cloned revive-base Blueprints
  (all enum cases retargeted); status for 3 turns when you are revived.
- **Weapon passives**: Self Blight I–III and Self Berserk I–III
  (10/20/30% per stack, 3-turn self status each turn start).

### Revived cut pictos

Vanilla **cut** pictos that are in the loot tiers get handed out by the
randomizer. Some ship with their Data Asset + effect Blueprint (the effect
works) but with literal `"null"` name/description and an **all-zero**
`DT_PictosDefinitions` row — so they hover blank and grant no stats.
`CutPictosKit` fills only what is missing (item/passive text, a vanilla-style
stat spread, and `ST_PassiveEffects` entries) and leaves the effect untouched.
Effects were verified from the shipped Blueprints:

- **Anti Dizzy / Anti Curse / Anti Exhaust** — PreventBuff pointed at the status.
- **Revive with Shell / Revive with Rush** — on revive, apply the status to all
  allies.
- **Defense Riser Dodge / Power Riser Dodge** — on dodge, gain Shell / Powerful.
- **Critical Havoc** — defeating an enemy makes your next hit a Critical Hit.
- **Empowering Death** — on death, apply Powerful to the party.

Spreads that grant **Defence** (Anti Dizzy/Curse/Exhaust, Revive with Shell,
Defense Riser Dodge) are automatically excluded from Dark Shores /
Frozen Hearts chests — `RandomizerKit` derives `DefensePictos` from the packed
table, so no separate list is needed and `rando-smoke` asserts it.

### Audit: incomplete deployed pictos

Objective signals: `DT_PictosDefinitions.BaseStatMultipliers` all zero
(**no stats**) and no `DT_PassiveEffects` row / no `BattleBuffs` (**no effect**).
Cut pictos whose Blueprint does no work are parked in `item_tiers.json`
`DoNotUse` **and** `LootCatalog.BrokenItems` rather than revived:
`CriticalCursedPower` (a gutted `ProcessAttackElement` stub that duplicates
Cursed Power I's gate but does nothing), `CritChanceOnWeak`, `Auto1Hp`,
`AntiSilence` (no passive row at all), `PostGradient`, `PerfectHeal`,
`ProtectingFire`, `PatientFighter` (Blueprint has no working logic), and
`DefenseBreakerDodge` / `PowerBreakerDodge` (both point at the same
`BP_LU_DodgeDefensless`, so "Power Breaker" applies Defenceless instead of
lowering Power). `SpeedsterDodge` and `PowerBreakerShot` were parked earlier for
the same reason. The deployed tiers are now clean of stat-less / effect-less
pictos.

### Loot only stocks packed ids

Chests, enemy drops and merchants only draw ids that actually exist in the
packed `DT_jRPG_Items` (`RandomizerKit.PackedItemIds`). Tier-list membership is
still the content gate, but a created id whose create toggle was off at pack
time is never handed out as a phantom entry that renders as a placeholder.

### World-map encounter loot tiers

An enemy's loot tier comes from its own row: `AreaTierForEnemy` uses the area
band of the region in its display name, else the row's `Level`
(`ResolveEnemyLevel` → `TierFromLevel`), and an "(Frozen Hearts)" row is pinned
to `level + 16`. The **encounter's** `EncounterLevelOverride` is never read — the
randomizer restores `DT_WorldMap_Encounters` (and `DT_jRPG_Encounters`) to
vanilla at pack time, so encounter edits do not reach the pack.

World-map fights mix regions, so tiering each member by its own region makes one
fight hand out several tiers — `WM_Veilleur*1_Pelerin*1_ChalierHammer` spawns a
Forgotten Battlefield Chalier (T2), a Reacher Veilleur (T3) and a Frozen Hearts
Pelerin (T4), so the Pelerin drops a Grandiose (`UpgradeMaterial_Level4`)
catalyst next to the others' T2/T3 ones. `Loot/Data/worldmap_encounter_tiers.json`
pins those fights to a single tier (16 fights, curated from the vanilla roster);
`LootCatalog.EncounterTierByEnemy` is consulted first in `AreaTierForEnemy`, so
drops **and** the modded monster-stat scaling both follow the fight's tier.

Two caveats:

- Loot is keyed by **enemy row**, not by encounter, so a row shared by curated
  fights that disagree can only carry one tier. The row takes the majority tier
  (tie → the higher one) and each conflict is listed by `rando-smoke`
  (`LootCatalog.EncounterTierConflicts`). Today five rows are shared
  (`FB_Ramasseur`, `SC_CultistFlying`, `MM_Danseuse`, `MM_Braseleur`,
  `MM_Stalact`), all resolving to T2 — so the four fights curated
  T1 keep one T2 member each.
- The Sirene arena is a T3 area, so `SI_Ballet` and `SI_Benisseur` are pinned T3
  by `LootCatalog.AreaTierOverrides`. Because the curated-encounter check runs
  *before* the row overrides, both were removed from their curated fights
  (`WM_Stalact_Ballet_Sapling`, `WM_Noire_Danseuse_Benisseur`,
  `WM_Bourgeon_Benisseur_OrphelinNailBig`) so that pin takes effect. The other
  members of those fights keep their curated tiers, so a fight can now field one
  T3 Sirene member beside T1/T2 region members.
- The same release applies to the borrowed natives of every other T4/T5 area:
  The Reacher (`RE_*`, plus the `reacher` band floor), Dark Shores (`DS_Noire`),
  Frozen Hearts (`MM_*` + the `+16` remap) and the Crimson Forest
  (`YF_Chevaliere_C` / `YF_Chevaliere_C_Alpha`, pinned T4; the Crimson Forest
  chests are lifted to the 60–79 band alongside them). Releasing a row is the
  half that lets the native area keep its tier; the other half is
  `WorldMapVariantKit.DoubleDipped`, which clones each released row into a
  world-only `WMV_` variant pinned to the world tier (`WorldLevel = 40`, T3 —
  or `WorldLevelHigh = 60`, T4, for the rows in `T4Variants`) and repoints
  `DT_WorldMap_Encounters` at the clone, so the optional world fight still pays
  its lower tier instead of the area's. `YF_Chevaliere_C` is borrowed twice
  (`WM_ChevaliereC_Glaise_OrphelinSpoonBig`, `WM_Noire_ChevaliereC_OrphelinHammerBig`)
  and both get the clone; the curated fight keeps T2 for its remaining member
  (`YF_Glaise`).
- The three solo world-map chromatics borrow T5 natives into much lower-band
  fights, so they also go through the split — at T4, matching their fight band
  rather than the default T3: `WM_Demineur_Alpha` (label 68) borrows the L99
  `GO_Demineur_ALPHA`, `WM_EvequeIce_Alpha` (label 50) borrows the L83
  `SM_Boss_Eveque_Ice_ALPHA`, and `WM_MoissoneuseAlpha` (label 50) borrows the
  L99 `MF_Moissonneuse_ALPHA`. Without the split the world fights would run the
  natives' absolute T5 curve (up to the 900M soft cap) and pay the T5-high boss
  package (3×L5 + 11–25×L4). Their `WMV_` clones curve at the T4 world tier
  instead — still genuine chromatic-alpha stats, one band down. Note the Demineur
  encounter references its row as `GO_Demineur_Alpha` (case differs from the DT
  row); the repoint lookup is case-insensitive for exactly this reason.
- The curation is loot/stat-side only: the game's own encounter level (and so the
  fight's difficulty) is left vanilla. Writing the corrected level into the
  encounter rows would change difficulty, which was out of scope.

### Grosse Tete's dodge-only farm

Grosse Tete can be killed by repeatedly dodging his attacks without landing a
hit, so his rewards are deliberately capped:

- The world-map venue clone (`BV_MF_GrossTete_50`, created from
  `WM_GrossTete` at design level 50) is **T3**, not T4. It uses a T3 boss
  package: no Perfect (L5) catalyst, just the T3 catalyst at 3–5. The native
  `MF_GrossTete` row keeps its own T4 band for its non-world encounter.
- The surviving T4/T5 variants (`MF_GrossTete`, `CFH_GrossTete`, and any
  higher-band `BV_` clone) keep their variants but cap the Perfect Catalyst
  payload at **3×L5** instead of the normal 5/15
  (`RandomizerKit.GrosseTetePerfectCatalystQty`).

### Status-swap retargeting (SOS / Revive / Death / Auto)

The Death / Revive / SOS / Auto status clones do more than a name-map string
sweep. The game resolves a class import as `(package_path, class_name)`, so
renaming only the class FName leaves the outer Package pointing at the source
class and the import fails — the game hangs on battle load when it tries to
resolve a class that does not exist. Each status clone therefore:

- Rewrites **both** the class FName and the outer Package FName on every
  matching import (`RetargetBuffImports`), running before the name-map sweep.
- SOS keeps the trigger inheriting the apply-on-low-HP logic from the shared
  vanilla sub-buff `BP_LU_SosStatusEffect` and only swaps the CDO's
  `StatusToApply` (an object reference) from `BP_BattleBuff_Powerful_125` to
  the target status class. Earlier attempts re-parented the trigger to
  `BP_BattleBuffInstance` and cloned a per-variant sub-buff; that orphaned the
  sub-buff's runtime logic and left a dangling `Default__BP_LU_SosStatusEffect`
  import at the renamed path, which froze the game on battle load.

**`_125` is the FName `Number`, not a string.** Stat-buff variants like
`BP_BattleBuff_Powerful_125` store the `_125` as the FName instance number, not
in the Value string. A name-map string sweep can never match it; only setting
`ObjectName.Value` **and clearing `Number`** re-points the import. `pictos-smoke`
asserts no `_125_C` residual survives in any clone.

The target status classes live at
`/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_{Cursed,Berserk,Blight}` —
not the `StatsBuffs`/`ParentClasses` paths the vanilla stat-buff sources use.
Verify everything with `dotnet $CLI pictos-smoke` after changing any kit.

## Super Sonic — a custom STATUS, not just a passive (2026-10)

`SpecialPictosKit.SuperSonicId` is the third hand-authored unique picto. It
grants **Speed only at 3× the shared picto curve** (`BaseStatMultipliers`
`{ Speed: 3.0 }`, `ChromaValueMultiplier` 1.5) for **100 lumina**, and drops
only from `LU_EchassierAlpha` (Act 3 Lumière Chromatic Echassier), pinned by
`PictosMagnitudeKit.PinExclusiveDrops` and listed in
`LootCatalog.BuildProtected`.

Its effect is a genuinely new status buff, `BP_LU_SuperSonic_Buff`:

- It clones the vanilla **Rush** buff `BP_BattleBuff_Speed`.
- Rush's whole speed effect is a **tagged CDO property**, not bytecode:
  `Default__BP_BattleBuff_Speed_C.CharacterStatMultipliers[0]` is
  `{ StatType = Speed (NewEnumerator7), Multiplier = 1.33 }` (+33%), with
  `TurnDuration = 3`. The clone edits the `Multiplier` double to **2.0**
  (+100%) and rewrites `Name`/`Description` to culture-invariant inline text
  ("Super Sonic"). `BP_BattleBuff_Slow` is the same struct at `0.75`.
- `RetargetPackage` renames the class **and** the outer Package (both are in the
  name map; no numeric FName suffix like `_125`, so a plain stem sweep is safe),
  writing the clone to a distinct
  `/Game/Gameplay/Buffs/ParentClasses/BP_LU_SuperSonic_Buff`.

The **picto's passive** (`BP_LU_SuperSonic`) is a clone of the Layered Canvas
turn-start donor `BP_LU_SelfBurnButDmg+`, with Burn retargeted to
`BP_LU_SuperSonic_Buff` (class + package). Three balancing keys are cloned onto
`SuperSonic_*` so the Burn riders are inert (`BurnSelfDamageMultiplier` 0,
`DamageIncreasePerBurnStack` 0) and the status lasts **1 turn** — it is
re-applied at every turn start, so it is effectively permanent while worn.

Trap worth remembering: `CharacterStatMultipliers` only parses when the buff's
schema chain (`BP_BattleBuffInstance`) is in the merged mappings. `BP_BattleBuff_Speed`
carries a parseable CDO export; the revive-debuff donors (`Slow`, `Powerless`,
`Defenseless`) do **not** — their magnitude/duration lives in the graph, which is
why `ReviveDebuffKit` can only retarget them and cannot retune them. Do not
assume every `BP_BattleBuff_*` has a CDO to edit.

`BP_BattleBuff_Speed` is in `Packer/donor_filters.txt`, so the standalone packer
stages the donor; the dev flow extracts it on demand via `EnsureExtracted`.

## Rupture Shot — the vanilla Inverted status as a player-facing tool (2026-10)

`SpecialPictosKit.RuptureId` is the fourth special picto. **Inverted**
(`BP_BattleBuff_Inverted`) is a vanilla status whose description is *"The
character affected can't be healed, it takes damage instead"*; vanilla Inverted
Affinity rewards wearing it (+50% damage). Rupture Shot turns it on enemies:
**Free Aim shots can inflict Inverted** (50% / 5 turns; vanilla's cut Free-Aim
Inverted passive is 33% / 5).

- Donor is `BP_LU_FreeAimInvertedShot` — the **CDO-parseable** member of the
  `InvertShot` / `FreeAimInvertedShot` pair (the twin `BP_LU_InvertShot` drops
  its CDO as raw `RawExport` bytes). Its CDO carries `ChancePercentage`,
  `BuffToApply` (`BP_BattleBuff_Inverted`), `BuffToApplyTurn Duration` and
  matching `FreeAimInvertedShot_ChancePercentage/_BuffToApplyDuration` balancing
  rows. The clone retargets package/class, renames the bare donor token
  (`FreeAimInvertedShot` → `RuptureShot`) so the balancing keys follow, and sets
  `ChancePercentage` 33 → 50.
- It costs 100 lumina, grants Speed 1.5 / Crit 0.5 (chroma 1.5), and is pinned to
  `L_Creation` ("Création", Act 3 Lumière) via `PinExclusiveDrops` + the
  `LootCatalog` protected set.
- The donor needs its schema **parent** (`BP_LU_FreeAimShotEffectParent`) on disk
  or the CDO will not parse: `EnsureExtractedShared("RuptureShotDonor",
  both filters)` stages them under one Content root. Both filters are in
  `donor_filters.txt`.

`EnsureExtractedShared` now copies an already-staged `work/tables/<filter>` tree
into the shared root when it finds one (the packer's `DonorExtract` stages each
filter that way) instead of re-running retoc, which refuses while `~mods` holds a
live pack. That keeps a fresh packer run from dying on a shared root it has not
seen before, for every kit, not just this one.

## Uno Puncho — first hit ×10, everything after ×0 (2026-10)

`SpecialPictosKit.UnoPunchoId` is the fifth special picto and a deliberate
one-shot gimmick: **"First hit deals 10x damage. All following hits deal no
damage."** It costs **21 lumina** and grants Speed 1.5 / Crit 0.5 (chroma 1.5).

- Donor is `BP_LU_FirstLife` — a permanent (battle-long) buff that already
  selects between `FirstLife_DamageIncrease` and `FirstLife_DamageDecrease`
  through its instance `HasDied` bool. The clone renames both rows to
  `UnoPuncho_DamageIncrease` / `_DamageDecrease`, sets them to **10.0** and
  **0.0**, changes the `FDamageModifier` serialized type from 3 (flat additive)
  to **2 (direct multiplier)**, and inserts `HasDied = true` immediately after
  the first `AddDamageModifier`. The direct multiplier makes the first hit
  `base × 10` and every later hit `base × 0`; it cannot go negative and cannot
  heal the target. The old flat `-1,000,000` value was removed because the
  game's 9,999 clamp is optional and inactive by default, so negative damage
  healed the enemy instead of landing on 0.
- The gear DA is a clone of `DA_GearPictos_Immaculate` (FirstLife's own DA is
  not extractable while a mod is installed). The passive, definition, item and
  icon rows use the `FirstLife` templates.
- **Not a random drop.** `BP_Dialogue_GV_OnoPuncho` keeps its single vanilla
  `AddItemToInventory` call, but its CDO `Reward` row is swapped from
  `SkinMaelle_Potatoe_Bag` to `UnoPuncho` by
  `DialogueRewardKit.ReplaceFixedReward` (data-only, no bytecode change). The
  earlier `AddFixedItemReward` second-call insertion crashed the dialogue when
  the `Bonus_StrongestPunch` objective was already complete and must not be
  reintroduced without an in-game retest on a completed quest.
- The fight itself is stat-pinned in `RandomizerKit.AbsoluteStatPins`:
  effective **Speed 500**, **PhysicalDefense 500** and **MagicalDefense 500**
  (HP/attack keep the normal T5 boss curve). The curve pass skips a field that
  has an absolute pin, so the arena boss speed multiplier cannot overwrite the
  target. `Quest_GestralSumo9999` is also explicitly excluded from every
  `PossibleLoot` branch — he is undefeatable, so the 9,999-damage objective pays
  through dialogue and he carries no battle drops (not even arena Lumina).

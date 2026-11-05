# Picto categories / tags — scope & plan (2026-11-02)

Status: **implemented 2026-11-02** — `PictoTagCatalog` / `PictoTagKit`, the
extended `E_PassiveEffectCategory` enum (Element / Revive / Lifesteal / Luck),
pipeline hooks, `picto-tags` CLI command, and `pictos-smoke` assertions are in.
`work/mod` was repaired with `dotnet … picto-tags`; `pictos-smoke` and
`rando-smoke` pass. See §8 for the implementation record.

## 1. What the game actually stores

The "groups/tags" are the **`Categories`** array on `DT_PassiveEffects`
(`Sandfall/Content/Gameplay/Lumina/DT_PassiveEffects.uasset`). It is a
`TArray<E_PassiveEffectCategory>` and every passive/picto row has the field:

```
Categories: [ E_PassiveEffectCategory::NewEnumerator4 ]   // AP
```

The game's enum display names come from the extracted
`E_PassiveEffectCategory` asset. The editor's `GameLabels.PassiveCategories`
map already matches them:

| Enum | Label | Enum | Label |
|---|---|---|---|
| `NewEnumerator0` | Invalid | `NewEnumerator12` | Parry |
| `NewEnumerator2` | FreeAim | `NewEnumerator13` | Counter |
| `NewEnumerator3` | BaseAttack | `NewEnumerator14` | Death |
| `NewEnumerator4` | AP | `NewEnumerator15` | Solo |
| `NewEnumerator5` | Heal | `NewEnumerator16` | Defense |
| `NewEnumerator6` | Shield | `NewEnumerator17` | Speed |
| `NewEnumerator7` | Critical | `NewEnumerator18` | Mark |
| `NewEnumerator8` | Damage | `NewEnumerator19` | Debuff |
| `NewEnumerator9` | Burn | `NewEnumerator20` | Break |
| `NewEnumerator10` | Buff | `NewEnumerator21` | Items |
| `NewEnumerator11` | StatusEffects | `NewEnumerator22` | Gradient |

There is **no other tag field**: `DT_jRPG_Items` only has
`Item_Type`/`Item_Subtype`/`Item_Rarity`, and the gear data asset only points
at the passive row. `Categories` on `DT_PassiveEffects` is the single source
of truth.

These tags are menu/tooltip metadata, not balance data. A wrong tag does not
change an effect; it makes the picto appear under the wrong filter or with the
wrong category chip.

Important: **not every vanilla row has categories.** 438 of the 657 vanilla
passive rows have an empty array, including some pictos we clone from
(`Painter`, `CursedPower`, `BurningDodge`, `RandomDefense`, `LuckyAim`,
`ParryHelper`, `ParrySpecialist`, `LongerDodge`, `AutoCurse`). The goal is
therefore not "copy vanilla", but "give every mod-owned picto a meaningful
tag" — for those clones we will be adding information the vanilla row never
had.

## 2. Current state (built `work/mod` audit)

`work/mod` has 968 passive rows; 657 are vanilla, 312 are new. After removing
weapon passives (`WepFx_*`), the picto picture is:

| Group | Count | Notes |
|---|---:|---|
| New picto rows | 165 | from `PictosCreateKit`, `PictosMagnitudeKit`, `SpecialPictosKit`, `BombPictosKit`, `CutPictosKit`, `ReviveDebuffKit` |
| **New picto rows with no categories** | **37** | almost all cloned from `Painter`, `CursedPower`, `BurningDodge`, `RandomDefense`, `LuckyAim`, `ParryHelper`, `ParrySpecialist`, `LongerDodge`, or a cut row with an empty category |
| Modified vanilla rows | 89 | includes reworked/renamed/revived pictos and vanilla rows repurposed as weapon passives; **17 picto rows are still empty** |
| **Modified picto rows still with no categories** | **17** | `LuckyAim`, `CursedPower`, `RandomDefense`, `LongerDodge`, `ParryHelper`, `ParrySpecialist`, `DodgeSpecialist`, `AutoCurse`, the revived `Anti*`, `ReviveWith*Dodge`, `CriticalHavoc`, `EmpoweringDeath` |
| **Rows with wrong/incomplete inherited tags** | **21** | 18 mismatched-donor rows (`SuperSonic`, `LayeredCanvas`, `CriticalChain*`, `ReviveWithPowerless/Defenceless/Slow`, `CursingDeath*`, `ReviveWithCurse*`, `SosCurse*`, `Death's Dividend`) + the three `TitaniumShell*` rows, which need `Defense` added |
| New weapon-passive rows (`WepFx_*`) | 147 | **out of scope** — not pictos and not shown in the picto category filter |

### Root cause

Every kit creates rows with `DataTableEditor.CloneRow(donor, id)` and then
edits name/text/cost/buff path. **No kit ever writes `Categories`**, so the
clone inherits whatever the donor had:

- `Painter`, `CursedPower`, `BurningDodge`, `RandomDefense`, `LuckyAim`,
  `ParryHelper`, `ParrySpecialist`, `LongerDodge`, `AutoCurse` have **empty**
  categories in vanilla → every clone is empty.
- `ReinforcementParade` (Empowering Parry) has `Parry` → `CriticalChain`
  wrongly carries `Parry`.
- `BurningDeath` has `Burn` → the three revive-debuff pictos wrongly carry
  `Burn`.
- `ProtectingDeath` has `Defense` → the three `*Death` status swaps carry
  `Defense` even though they apply a status.
- `ReviveWithPower` has `Damage` → the three revive-status swaps carry
  `Damage` even though they apply a status.
- `SosPower` has `Damage` → the three SOS status swaps carry `Damage`.

Also, `CloneRow` is a no-op when the row already exists. A category fix added
only to the creation path would **not repair an existing `work/mod`**; a final
pass is needed for old installs.

## 3. Proposed mapping

This is the Phase 1 mapping (existing enum values only). The implemented
catalog additionally applies the approved new tags from §4: Painter
conversions also get **Element**, revive rows get **Revive**, Attack Lifesteal
gets **Lifesteal**, and the chance/random families get **Luck**. Primary tag
first; the live source of truth is `PictoTagCatalog.Rules`.

### 3.1 Empty rows — created by our kits

| Family / ids | Current | Proposed | Why |
|---|---|---|---|
| `PainterConv_Fire/Ice/Lightning/Earth/Dark/Light/Physical/Void` | — | `Damage` | Converts skill damage; no Element tag exists (see §4) |
| `CursedPower2/3`, `BerserkPower1/2/3`, `BlightedPower1/2/3` | — | `Damage, StatusEffects` | Damage bonus gated on the wearer's status |
| `BurnOnHit1–5` (Burning Skin) | — | `Burn, StatusEffects` | Self-applies Burn on dodge |
| `Dodger2/3` (Dodge Helper II/III) | — | `Defense` | Dodge-window increase; same family as `Dodger` (AP + Defense) minus the AP rider |
| `ParryHelper2/3` | — | `Parry` | Parry-window increase |
| `ParrySpecialist2/3` | — | `Parry, Counter` | Window penalty + double counter damage |
| `RandomDefense2–5` | — | `Defense` | Random incoming-damage multiplier |
| `LuckyAim2/3` | — | `FreeAim, AP` | Chance to gain AP on a Free Aim shot |
| `Infinite` | — | `Defense` | Sets max Health (no Health tag; Defense is the closest existing one) |
| `RuptureShot` | — | `FreeAim, StatusEffects, Debuff` | Free Aim applies Inverted (a debuff) |
| `AutoBerserk`, `AutoBlight` | — | `Buff, StatusEffects` | Battle-start status; same shape as Auto Rush/Shell/Powerful |
| `StainPictos_*` (currently removed from the pack) | — | `Damage, StatusEffects` (or new `Stains` tag if added) | Battle-start elemental stains; add when/if the kit is re-enabled |

### 3.2 Empty rows — modified vanilla / revived cut content

| Id | Current | Proposed | Why |
|---|---|---|---|
| `CursedPower` (tier I) | — | `Damage, StatusEffects` | Same as its II/III clones |
| `LuckyAim` (tier I) | — | `FreeAim, AP` | Same as II/III |
| `RandomDefense` (tier I) | — | `Defense` | Same as II–V |
| `LongerDodge` (Dodge Helper I) | — | `Defense` | Dodge-window increase |
| `ParryHelper` (tier I) | — | `Parry` | Parry-window increase |
| `ParrySpecialist` (tier I) | — | `Parry, Counter` | Same as II/III |
| `DodgeSpecialist` | — | `AP, Defense` | −25% dodge window, 50% chance +1 AP on dodge |
| `AutoCurse` | — | `Buff, StatusEffects` | Battle-start status |
| `AntiDizzy`, `AntiCurse`, `AntiExhaust` | — | `StatusEffects, Defense` | Prevent-buff family; matches vanilla `AntiStun`/`AntiBurn` |
| `ReviveWithShell` | — | `Death, Defense, Buff` | On revive, Shell to all allies |
| `ReviveWithRush` | — | `Death, Speed, Buff` | On revive, Rush to all allies |
| `DefenseRiserDodge` | — | `Defense, Buff` | On dodge, gain Shell |
| `PowerRiserDodge` | — | `Damage, Buff, Defense` | On dodge, gain Powerful |
| `CriticalHavoc` | — | `Critical, Damage` | Next hit after a kill is a Critical |
| `EmpoweringDeath` | — | `Death, Buff, Damage` | On death, Powerful to all allies |

### 3.3 Rows with the wrong inherited tags

| Id(s) | Current | Proposed | Why |
|---|---|---|---|
| `SuperSonic` | `Burn, StatusEffects` | `Speed, Buff` | +100% Speed status; inherited from the Burn donor |
| `LayeredCanvas` | `Burn, StatusEffects` | `Burn, Buff, StatusEffects` | Grants Burn + six buffs each turn; add `Buff` |
| `CriticalChain`, `CriticalChain2`, `CriticalChain3` | `Damage, Parry` | `Damage, Critical` | Per-Critical-Hit scaling; `Parry` came from the Empowering Parry donor |
| `ReviveWithPowerless`, `ReviveWithDefenceless`, `ReviveWithSlow` | `Burn, Death, StatusEffects` | `Death, Debuff, StatusEffects` | Inflict a debuff on enemies on revive; no Burn |
| `CursingDeath`, `BerserkingDeath`, `BlightingDeath` | `Death, Buff, Defense` | `Death, Buff, StatusEffects` | Apply a status to allies on death; `Defense` came from Protecting Death |
| `ReviveWithCurse`, `ReviveWithBerserk`, `ReviveWithBlight` | `Death, Damage, Buff` | `Death, Buff, StatusEffects` | Apply a status on revive; `Damage` came from Revive with Power |
| `SosCurse`, `SosBerserk`, `SosBlight` | `Damage, Buff` | `Buff, StatusEffects` | Apply a status below 50% HP; `Damage` came from SOS Power |
| `Death's Dividend` | `Damage` | `Damage, Death` | Scales with revives |
| `TitaniumShell`, `TitaniumShell2`, `TitaniumShell3` | `Damage` | `Damage, Defense` | François's Shell trades damage dealt for damage taken; our rows cloned Glass Cannon |

Everything else already carries the correct inherited tags: the
`PictosMagnitudeKit` tiers of `StunBoost`, `Immolate`, `AlternatingCritical`,
`BurningDeath`, `BreakDamageOnCrit`, `EnergyDeath`, `HealingParry`,
`BreakingCounter`, etc. all inherit non-empty, matching donor rows.

## 4. Suggestions for new tags

Approved and implemented:

| Value | Tag | Used on |
|---|---|---|
| `NewEnumerator23` | `Element` | the eight Painter conversions (and Stain pictos, if re-enabled) |
| `NewEnumerator24` | `Revive` | all revive-triggered pictos + Death's Dividend |
| `NewEnumerator25` | `Lifesteal` | Attack Lifesteal I–III |
| `NewEnumerator26` | `Luck` | Lucky Aim, Roulette, Random Defense, Charging Critical, Rupture Shot |

`PictoTagKit` appends these to the enum asset's `Names` list (values 22–25,
`E_MAX` becomes 26), adds the four `DisplayNameMap` entries, updates the
`work/enums` cache so the editor renders the labels, and stages the modified
asset into `work/mod/Sandfall/Content/Gameplay/Lumina/`.

The game UI must still be checked in-game: the category **data** is valid, but
the picto filter/tooltip may or may not enumerate the new values. If it does
not, the tags remain in the table (and the editor/loot tooling can use them)
without affecting existing filters.

Not added: `Stains` (only if the stain kit returns), `Weapon` (weapon passives
are out of scope), `Random` (covered by `Luck`).

Note: `NewEnumerator21` is genuinely named **`Items`** in the game enum, not
"Tint". The rows that use it are tint mechanics, but renaming the label would
only change the editor, not the game, and would diverge from the enum asset.

## 5. Implementation record

1. **`src/E33ModEditor.Core/PictoTagCatalog.cs`** — tag constants for all 21
   vanilla values plus the four new ones, and an explicit `id → tag[]` map for
   every affected row. Each rule may carry a `Legacy` set (the donor/inherited
   value) so the final pass can distinguish "stale clone" from "manual edit".
2. **`src/E33ModEditor.Core/PictoTagKit.cs`** — `Apply(modDir)` repairs the
   staged `DT_PassiveEffects`; `EnsureEnum(modDir)` appends the four enum
   values, updates the extracted cache and stages the enum into the pack.
3. **Pipeline hooks** — called last among the picto kits in
   `FullPackKit`, the CLI/standalone build, `pictos-smoke` and the
   `rando-smoke` chest tree.
4. **`picto-tags [modDir]` CLI** — repairs an existing package and stages the
   extended enum (defaults to `work/mod`). Current `work/mod` was repaired with
   it (85 rows retagged).
5. **`pictos-smoke`** now asserts:
   - every catalog rule present in the pack matches exactly;
   - every mod-owned non-weapon picto row has at least one category;
   - the staged enum defines Element/Revive/Lifesteal/Luck;
   - the specific donor regressions (Super Sonic, Critical Chain, Revive with
     Powerless, Painter conversion, Attack Lifesteal, Rupture Shot).
6. **Editor support** — `GameLabels.PassiveCategories` contains the four new
   labels, and `EnumChoices` merges fallback labels if the extracted enum cache
   has not picked them up yet.
7. **Docs** — `docs/custom-pictos.md` has a new "Categories (tags)" section.

## 6. Decisions taken

1. **Manual edits:** preserved when the existing set differs from both the
   catalog and the known donor/legacy set; the pass logs a warning and leaves
   it alone. Empty and known-stale sets are repaired.
2. **Weapon passives:** out of scope. `WepFx_*` rows and vanilla rows reused as
   weapon passives keep their existing categories; the smoke guard ignores
   them.
3. **New enum values:** approved and shipped as values 23–26. In-game filter
   visibility still needs a visual check (data is valid either way).

## 7. Acceptance checklist

- [x] Every mod-created picto row has at least one category.
- [x] Every row in §3 matches the proposed tags.
- [x] No row carries a tag solely because of a mismatched donor
      (`SuperSonic`, `CriticalChain*`, `ReviveWithPowerless*`, `SosCurse*`, …).
- [x] `pictos-smoke` asserts catalog equality and reports zero empty custom
      pictos.
- [x] `rando-smoke` passes (it stages the same kits into a chest tree).
- [x] Rebuilt pack contains the tags; old `work/mod` was repaired with
      `picto-tags`.
- [x] The extended enum asset carries Element / Revive / Lifesteal / Luck.
- [ ] In-game: confirm the new tags appear in the picto filter/tooltip. Data is
      valid either way; only the UI visibility is unverified.
- [x] Docs explain where tags live and how to add a new one.

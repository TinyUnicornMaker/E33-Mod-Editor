# Weapon effect archetypes (randomizer coherence)

Status: **implemented**. The loader, the archetype-aware roller, the Lune
melee-element gate and the pack-time sweep all shipped; `fx-roll` and a passive
`rando-smoke` pack validate the behavior. The original review package (this
document, the data under `Planning/archetypes/` and the six character images)
is still the reference for the grouping itself.

## Why

The weapon-passive randomizer currently rolls an effect tier from the weapon
tier, then picks uniformly among every effect that tier allows for the
character. That produces weapons whose passives fight each other: Burn damage
next to a Counter kit, Ice-stain payoffs on a Fire weapon, T1 filler next to a
T5 capstone with no relationship.

The proposal is to draw from **archetypes** — groups of effects that share a
gameplan — plus **sibling links** between archetypes that combo, and keep one
**wildcard** slot so weapons still surprise.

## Audit

| | |
| --- | ---: |
| Tier entries (T1–T5) | 415 |
| — vanilla | 274 |
| — new / custom (`WepFx_*` + built-in stains) | 141 |
| Do Not Use (excluded) | 3 |
| T1 / T2 / T3 / T4 / T5 | 47 / 86 / 115 / 93 / 74 |
| Effects in more than one archetype | 222 |
| Archetypes | 32 (14 generic + 18 character) |

Every T1–T5 effect is mapped to at least one archetype (verified by
`Planning/archetypes/effect_archetype_map.csv`). Do Not Use effects
(`WepFx_SelfBlight`, `FreezeSelfOnLowHP2`, `DefreezeAndHealOnTurnStart`) stay
out of the pools, exactly as today.

## Agreed rules (as implemented)

1. **Tier split.** T1 = 1 wildcard. T2 = 1 archetype + 1 wildcard. T3 = 2 + 1.
   T4 = 3 + 1. T5 = 4 + 1. The wildcard occupies the weakest slot
   (`Extra`/`Level1`); archetype picks are ranked strongest-first into the
   higher slots.
2. **One primary archetype per weapon**, weighted by how many of its effects
   are eligible for the character (and the Lune element). Every archetype slot
   first rolls the effect tier, then draws an archetype: the **primary with
   `PrimaryShare = 0.5`**, otherwise a uniformly random sibling link that has a
   candidate at that tier (see `WeaponEffectRoller.PrimaryShare`). The effect is
   then drawn uniformly inside that archetype+tier cell. A sibling may repeat;
   the same *effect* never appears twice.
3. **Tier distribution is preserved.** Each pick rolls the existing weapon-tier
   mix first (T5: 60% T5 / 30% T4 / 10% T3, etc.). If the chosen archetype has
   no eligible effect at the rolled tier, re-roll (bounded) or fall back to the
   next tier in the mix, then a high-to-low walk.
4. **Wildcard = today's roll.** Fully random among character-allowed effects at
   the rolled tier, subject to the Lune element gate and the exclusive groups.
   It fills the weakest slot.
5. **Gustave** stays T1–T3, generic-only and never gets `AutoDeath`; he has no
   character archetypes.
6. **Exclusive groups stay.** Stain-echo is still at most one per weapon.
7. **Coherence fallback.** If an archetype slot cannot find anything in the
   primary/sibling pool (exhausted pools), it falls back to the unconstrained
   wildcard roll so slot counts never collapse.
8. **Status finishers: one per weapon.** `WepFx_SlowConsume*`,
   `WepFx_PowerlessConsume*` and `WepFx_DefenselessConsume*` (base and Skills
   variants) share one exclusive group, so a weapon can hold at most one
   "deal X more damage to a status and consume it" effect across all three
   statuses. Same mechanism as stain-echo.
9. **Condition conflicts are enforced.** Archetypes group effects by *theme*
   (burn, crit, counter…); they say nothing about the *condition* an effect
   needs. A separate condition table tags each effect with the build condition
   it needs and forbids mutually exclusive conditions on one weapon (Full
   Strength "on full Health" next to Self Burn, …). See below.

## Condition layer

The archetypes answer "what is this effect about"; the condition layer answers
"when does it pay off". They are orthogonal: a Burn weapon can hold a
`heal` effect and a `crit-always` effect, but never `hp-full` **and**
`self-damage`.

- Tags live in `weapon_effect_conditions.json` (stock embedded, `work/`
  overlay wins). 395 of the 420 T1–T5 effects carry a tag; 25 are untagged.
  Only **84** carry a *conflict* condition — the rest are behaviour/synergy
  tags (65 of them: `burn-apply`, `burn-scale`, `on-crit`, `mark-apply`,
  `charge-gen`, `perfection-gain`, `shield-gen`, `stain-gen-*`, …).
- `conflicts` is an explicit tag-pair list, deliberately small. It only
  contains pairs that are impossible under **any** play:

  | Axis | Pairs |
  | --- | --- |
  | Health / damage taken | `hp-full` × {`hp-low`, `self-damage`, `on-death`}; `no-hit` × {`hp-low`, `self-damage`, `on-death`} |
  | First Life (survival) | `first-life` × {`self-damage`, `self-status`, `on-death`} |
  | Healing | `heal` × `no-heal` |
  | Status | `keep-powerless` × `consume-powerless` |
  | Stains | `stain-scale` × `consume-stain` |
  | Crit | `crit-always` × `crit-alternate` |
  | Party | `solo` × `ally` |

  `hp-full` and `no-hit` are **aligned** (taking no hits keeps you at full
  Health) and deliberately do not conflict; `self-damage` *helps* an `hp-low`
  plan; `AutoDeath` (`on-death`) is the only suicide effect. `first-life` is a
  **hard** survival condition: First Life pays +damage until you die and
  −damage after, so it cannot share a weapon with anything that chips you down
  (self burn / blight, self curse / berserk) or ends the run. `MacabrePower`
  wants the opposite (dying is its payoff) and so keeps the *informational*
  `death-trigger` tag instead.
- **Everything soft is informational, not a conflict.** `counter`, `reckless`
  (damage taken), `multi-hit`, `first-hit`, `death-trigger` (MacabrePower),
  `revive`, and the whole **Shield / Shell** cluster describe a build axis
  without forbidding anything. The exceptions are `first-life` and
  `self-status`, which are *conflict* tags because a "never die" plan cannot
  coexist with self-harm. In particular:
  - **Shield** (absorb points) and **Shell** (damage-reduction buff) are two
    resources. `shield-gen` (ShieldOnCounter, ShieldAllHeroesOnHeavy,
    Stance_ShieldOnParry/OSstance, Stance_VirtuoseDoubleShields, CanStealShield,
    Foretell_ShieldOnTurnStartWhenSunCharge, Stains_EarthAppliesShield),
    `shield-payoff` (APOnShield, CounterDamagePerShield, `WepFx_CounterShield*`)
    and `shield-spend` (AttackConsumeShieldForExtraDMG, `WepFx_AtkShield*`)
    coexist: make Shields, gain AP/counter damage off them, cash a burst — it is
    a rotation, not a contradiction.
  - `shell-gen` (Stance_ShellOnHighHP/OutVStance, Masks_ShellOnHeavy) and
    `shell-need` (`WepFx_ShellBlade`, ShieldOnShell) feed the Shell plan;
    `ShieldOnShell` is the bridge (applying Shell also applies a Shield).
  - `shield-break` marks the effects that break the **enemy's** Shields
    (FreeAimBreaksMoreShields, MarkBreakShields, …) — a Break build, unrelated
    to your own Shields.
- **Wildcard is neutral-first.** Archetype picks are drawn before the wildcard
  so they seed the build's conditions; the wildcard then prefers a neutral
  effect and falls back to any conflict-free one. It can no longer break a
  coherent kit.
- **Synergy bias.** `synergies` maps a trigger tag to `prefer` tags (and an
  optional `avoid` list). Once a trigger is on the weapon, later archetype slots
  *and* the wildcard weight candidates carrying a preferred tag, and sibling
  archetypes holding one are preferred too; `avoid` subtracts. Weight uses the
  **strongest** active trigger, not a sum, so a weight-4 engine pull isn't
  diluted by several weight-2 preferences. This is how a Shield Spender pulls a
  Shield Maker, a Burn applier pulls Burn scaling, a Mark applier pulls a Mark
  payoff, etc.
- **Two-slots-and-a-condition trap (P70).** `SimonimOvercharge` needs Fire +
  Ice + Lightning + Earth active at once — i.e. **four distinct Stains in four
  slots**. Only the neutral random/cycling generators (`Stains_TurnStart…`,
  `WepFx_TurnStart3/4`, `Stains_RandomGeneration*`) can fill four different
  types; light/dark generators and `BaseAttackToDarkStain` waste or remove a
  needed type, so `stain-diversity` **prefers** `stain-gen-any` and **avoids**
  `stain-gen-lightdark`/`stain-convert`.
- **Element gate.** `elementGate` maps the remaining element-locked families
  (`Skills<El>DMG+`, `<El>APCost-`, `LightDMGBurnOnCrit`, `LightDamageBurn`) to
  their element; the roller and the pack-time sweep only allow them on a weapon
  whose melee element matches, exactly like the Lune stain families.
- A derived `situations` block in the archetype JSON lists the effects per
  condition, and each archetype carries the union of its members' tags
  (`conditionTags`) to expose archetypes that span incompatible plans.

## Lune stain gating (melee element)

Lune's stain effects are split into an element-neutral **Stain Cycling** family
and six element families. For each Lune weapon only the family matching the
weapon's **melee element (`BaseDamageType`)** is eligible; the other elements
are removed from the pool entirely (not merely deprioritised).

- Fire / Ice / Lightning / Earth / Light / Dark weapons: neutral + matching
  element.
- Physical / Void weapons: neutral/random stain generation only.
- Random-type generation (`Stains_RandomGeneration`, `...OnAttack`, TurnStart,
  `Stains_GenerateLightOrDarkStainRandom`) is element-neutral and available to
  every Lune weapon.
- Element gating is applied to the wildcard as well.

The same melee-element gate applies to all *element-specific stain* effects —
starts, echoes, per-element payoffs ("per Fire Stain") and consume effects.
Element AP discounts (`FireAPCost-`, …) and element Skill Damage
(`SkillsFireDMG+`, …) are **not** gated in this pass.

## Archetype catalog

Global membership counts (an effect can appear in several archetypes); per-character
eligible lists are under `Planning/archetypes/compatibility/`.

| Archetype | Id | Group | Element | Members | Sibling links |
| --- | --- | --- | --- | ---: | --- |
| Shield & Sustain | `guard` | generic | — | 44 | Break & Stun, Ailment Blades, Burn, Counter, Critical, Risk & Sacrifice, Masks, Perfection, Moon Charges, Sun Charges, Stain Cycling, Earth Stains, Ice Stains, Light Stains, Defensive Stance, Virtuose, AP & Tempo |
| Burn | `burn` | generic | — | 41 | Base Attack, Ailment Blades, Counter, Critical, Foretell, Risk & Sacrifice, Shield & Sustain, Masks, Upgraded Skills, Perfection, Sustained Damage, Sun Charges, Twilight, Skill Damage, Stain Cycling, Fire Stains, Offensive Stance, Virtuose, Status & Mark |
| Sustained Damage | `raw` | generic | — | 40 | Base Attack, Break & Stun, Ailment Blades, Burn, Critical, Risk & Sacrifice, Masks, Perfection, Twilight, Skill Damage, Dark Stains, Light Stains, Virtuose |
| Status & Mark | `status` | generic | — | 38 | Base Attack, Break & Stun, Ailment Blades, Burn, Counter, Foretell, Twilight, Skill Damage, Stain Cycling, Ice Stains, Virtuose, AP & Tempo |
| Skill Damage | `skill` | generic | — | 33 | Break & Stun, Burn, Critical, Foretell, Free Aim, Masks, Upgraded Skills, Sustained Damage, Moon Charges, Stain Cycling, Dark Stains, Lightning Stains, Status & Mark, AP & Tempo |
| AP & Tempo | `tempo` | generic | — | 33 | Base Attack, Break & Stun, Counter, Gradient, Shield & Sustain, Masks, Upgraded Skills, Sun Charges, Skill Damage, Stain Cycling, Fire Stains, Ice Stains, Light Stains, Lightning Stains, Defensive Stance, Status & Mark |
| Base Attack | `base` | generic | — | 29 | Break & Stun, Burn, Counter, Critical, Free Aim, Perfection, Sustained Damage, Moon Charges, Sun Charges, Dark Stains, Earth Stains, Fire Stains, Offensive Stance, Status & Mark, AP & Tempo, Bestial Wheel |
| Critical | `crit` | generic | — | 29 | Base Attack, Burn, Counter, Free Aim, Gradient, Shield & Sustain, Perfection, Sustained Damage, Twilight, Skill Damage, Stain Cycling, Fire Stains, Offensive Stance |
| Risk & Sacrifice | `gambit` | generic | — | 22 | Ailment Blades, Burn, Shield & Sustain, Sustained Damage |
| Break & Stun | `break` | generic | — | 19 | Base Attack, Shield & Sustain, Sustained Damage, Skill Damage, Stain Cycling, Earth Stains, Defensive Stance, Virtuose, Status & Mark, AP & Tempo |
| Counter | `counter` | generic | — | 18 | Base Attack, Burn, Critical, Gradient, Shield & Sustain, Perfection, Defensive Stance, Virtuose, Status & Mark, AP & Tempo |
| Ailment Blades | `buff` | generic | — | 15 | Burn, Risk & Sacrifice, Shield & Sustain, Sustained Damage, Status & Mark |
| Free Aim | `freeaim` | generic | — | 12 | Base Attack, Critical, Masks, Perfection, Moon Charges, Twilight, Skill Damage, Bestial Wheel |
| Gradient | `gradient` | generic | — | 6 | Counter, Critical, Light Stains, Defensive Stance, AP & Tempo |
| Stain Cycling | `stain` | lune | — | 14 | Break & Stun, Burn, Critical, Shield & Sustain, Skill Damage, Dark Stains, Earth Stains, Fire Stains, Ice Stains, Light Stains, Lightning Stains, Status & Mark, AP & Tempo |
| Light Stains | `stain_light` | lune | Light | 12 | Gradient, Shield & Sustain, Sustained Damage, Stain Cycling, AP & Tempo |
| Fire Stains | `stain_fire` | lune | Fire | 13 | Base Attack, Burn, Critical, Stain Cycling, AP & Tempo |
| Earth Stains | `stain_earth` | lune | Earth | 12 | Base Attack, Break & Stun, Shield & Sustain, Stain Cycling |
| Ice Stains | `stain_ice` | lune | Ice | 11 | Shield & Sustain, Stain Cycling, Status & Mark, AP & Tempo |
| Dark Stains | `stain_dark` | lune | Dark | 10 | Base Attack, Sustained Damage, Skill Damage, Stain Cycling |
| Lightning Stains | `stain_lightning` | lune | Lightning | 8 | Skill Damage, Stain Cycling, AP & Tempo |
| Virtuose | `stance_vir` | maelle | — | 15 | Break & Stun, Burn, Counter, Shield & Sustain, Sustained Damage, Defensive Stance, Offensive Stance, Status & Mark |
| Defensive Stance | `stance_def` | maelle | — | 9 | Break & Stun, Counter, Gradient, Shield & Sustain, Virtuose, AP & Tempo |
| Offensive Stance | `stance_off` | maelle | — | 5 | Base Attack, Burn, Critical, Virtuose |
| Foretell | `foretell` | sciel | — | 53 | Burn, Moon Charges, Sun Charges, Twilight, Skill Damage, Status & Mark |
| Sun Charges | `sciel_sun` | sciel | — | 16 | Base Attack, Burn, Foretell, Shield & Sustain, AP & Tempo |
| Twilight | `sciel_twilight` | sciel | — | 16 | Burn, Critical, Foretell, Free Aim, Sustained Damage, Status & Mark |
| Moon Charges | `sciel_moon` | sciel | — | 10 | Base Attack, Foretell, Free Aim, Shield & Sustain, Skill Damage |
| Perfection | `perfection` | verso | — | 36 | Base Attack, Burn, Counter, Critical, Free Aim, Shield & Sustain, Sustained Damage |
| Masks | `mask` | monoco | — | 30 | Burn, Free Aim, Shield & Sustain, Upgraded Skills, Sustained Damage, Skill Damage, AP & Tempo, Bestial Wheel |
| Bestial Wheel | `wheel` | monoco | — | 6 | Base Attack, Free Aim, Masks |
| Upgraded Skills | `overcharge` | monoco | — | 4 | Burn, Masks, Skill Damage, AP & Tempo |

## Per-character circles

Eligible-effect counts in the review images (an effect can count toward several
linked circles):

| Character | Circles | Archetypes |
| --- | ---: | --- |
| Gustave | 14 | generic only |
| Lune | 21 | generic + Stain Cycling + 6 elements |
| Maelle | 18 | generic + Offensive / Defensive / Virtuose |
| Sciel | 19 | generic + Foretell / Sun / Moon / Twilight |
| Verso | 15 | generic + Perfection |
| Monoco | 18 | generic + Masks / Bestial Wheel / Upgraded Skills |

## Worked examples

**T4 Fire Lune, primary Burn**
- Element gate removes Ice/Earth/Lightning/Light/Dark stain families.
- Siblings of Burn that still exist for her: Base Attack, Ailment Blades,
  Counter, Critical, Shield & Sustain, Skill Damage, Stain Cycling, Fire
  Stains, Status & Mark, Sustained Damage, AP & Tempo.
- 3 archetype picks + 1 wildcard could land: `WepFx_AttackBurn5` (Burn),
  `CritAppliesBurn` (Critical), `WepFx_BurnDmg4` (Burn) and wildcard
  `APOnCrit`. Everything feeds the same engine.

**T3 Virtuose Maelle**
- Primary Virtuose pulls from Burn (double Burn), Shield & Sustain (double
  Shields, heal), Status & Mark (keep Mark), Counter, Break, plus generic
  siblings. A wildcard can still roll anything she is allowed.

## Implementation

| Path | Role |
| --- | --- |
| `src/E33ModEditor.Core/Loot/Data/weapon_effect_archetypes.json` | stock table (embedded resource) |
| `work/weapon_effect_archetypes.json` | overlay, wins when present (same pattern as tiers/characters) |
| `src/E33ModEditor.Core/Loot/WeaponEffectArchetypes.cs` | loader: archetypes, symmetric links, effect→element map, `Save()` |
| `Planning/archetypes/build_conditions.py` | condition table authoring (tags, conflict pairs, element gate) |
| `src/E33ModEditor.Core/Loot/Data/weapon_effect_conditions.json` | stock condition table (embedded resource) |
| `work/weapon_effect_conditions.json` | condition overlay, wins when present |
| `src/E33ModEditor.Core/Loot/WeaponEffectConditions.cs` | loader: tags, `Conflicts`, `IsNeutral`, `PreferredTags`/`PreferWeight`, `ElementOf`, `Save()` |
| `src/E33ModEditor.Core/Loot/WeaponEffectRoller.cs` | primary pick, per-slot archetype/effect draw, condition + element filters, synergy-weighted picks, neutral wildcard |
| `src/E33ModEditor.Core/Loot/WeaponEffectTiers.cs` | shared `Allows(chars, character, id)` (Gustave rules live here now) + wildcard `allow` filter |
| `src/E33ModEditor.Core/Loot/RandomizerKit.cs` | `RollWeaponPassives` drives the roller; melee element is rolled with the passives and reused for `BaseDamageType`; sweep checks stain element vs melee element |
| `Planning/archetypes/build_archetypes.py` | regenerates the table, per-character lists, images, and installs stock + work overlay |

`LootCatalog` exposes `EffectArchetypes`, so the CLI and GUI load the same data.
The stock JSON is regenerated by the script; edit the script (not the JSON)
when adding effects or links, then re-run it.

## Validation

`fx-roll` (the CLI simulation) over 200 seeded generations:

| Check | Result |
| --- | --- |
| Stain-echo doubles | 0 |
| Duplicate effect on one weapon | 0 |
| Character mismatches | 0 |
| Slot-count violations | 0 |
| Gustave AutoDeath / T4+ effects | 0 / 0 |
| Lune stain element mismatches | 0 |
| Status-consume stacks (Slow/Powerless/Defenceless) | 0 |
| **Condition conflicts on one weapon** | **0** |
| **Element mismatches (stain + element skill/AP families)** | **0** |
| Synergy satisfaction on T4/T5 weapons (trigger also holds a partner) | `base-attack` 85%, `break-dmg` 86%, `shell-need` 91%, `overcharge` 88%, `burn-consume` 83%, `charge-gen` 85%, `perfection-payoff` 80%, `twilight-payoff` 80%, `shell-gen` 73%, `self-status` 73%, `counter` 55%, `shield-gen` 54%, `crit-chance` 56% |
| Archetype cohesion (slots in primary + siblings) | 99.2% |
| Archetype slots taken from the primary | 52.1% |
| T4 effect tiers | 60% T4 / 21% T3 / 15% T2 / 5% T1 (target 60/20/15/5) |
| T5 effect tiers | 60% T5 / 30% T4 / 10% T3 (target 60/30/10) |
| Lune weapons with ≥1 stain effect | 12.2% |
| Element effects with 0 hits across 200 packs | 1 of 66 |

`fx-smoke` passes. `rando-smoke` now stages a passive pack
(`work/mod_randosmoke_passives`) and sweeps it: 0 sweep problems. (The three
monster-speed `rando-smoke` failures are pre-existing balance checks on the
MonsterStats path, untouched here.)

## RNG note

Passives are still pre-rolled before the weapon stat pass, but the melee
element is now rolled there too (it drives the Lune stain gate) and
`RandomizeWeapons` reuses it for `BaseDamageType`. The RNG draw order therefore
changed, so a seeded pack is not byte-identical to packs produced before this
change. This is expected.

## Still optional

- Tier Editor tab for viewing/editing archetypes and links (the loader already
  exposes `Save()` for it).
- Element gating for non-stain element effects (element AP discounts,
  element Skill Damage).

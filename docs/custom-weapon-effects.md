# Custom weapon effects

Pipeline for **new weapon passives** cloned from cooked graphs. There is no
in-app form for authoring a spec — add JSON, pack, then assign the new id on
a weapon in the Weapons list.

Vanilla `DT_Balancing_Luminas` keys are **never written by the clone path**.
Every `WepFx_*` clone gets new keys. Changing `AttackBurn_BurnDamageMultiplier`
in place would retune every vanilla weapon that already uses Attack Burn.

**Named vanilla reworks are the exception**, and are listed explicitly in
`WeaponEffectsCreateKit`:

- `VanillaTextOverrides` — rewrites a vanilla passive's tooltip in place
  (row FText + `ST_PassiveEffects`), no clone, no balance change.
- `VanillaBalancingOverrides` — sets an existing vanilla balancing row (e.g.
  `APOnCounter_APAmount` → 5.0). This is a deliberate, global rework of that
  one passive.
- `VanillaBpIntPatches` — stages a vanilla effect Blueprint at its original
  game path (so it shadows) and patches an int constant (e.g.
  `BP_LU_CounterBurn` `ChangeStackCount(5)` → 50).

### Weapon-effects vs pictos: different numbers on purpose

A weapon effect may share a display name with a picto (e.g. *Alternating
Critical III*, *Critical Chain II*) but carries **much larger** magnitudes.
Weapon passives are scarce — usually one weapon slot holds a single instance of
an effect — whereas pictos are stackable (a player can wear the whole family),
so the picto ladders are capped low on purpose. Treat the two ladders as
independent: never retune a `WepFx_*` value to match its picto twin. Full rule
and the shipped pairs: [custom-pictos.md](custom-pictos.md) → *Picto magnitude
vs weapon-passive magnitude*.

## What a weapon effect is

| Piece | Asset | Role |
| --- | --- | --- |
| Logic | `BP_LU_<source>` cloned to `/Game/Gameplay/Lumina/Passives_Weapons/Created/BP_LU_<Id>` | Cooked graph. Unique `FolderName` or IoStore collapses clones. |
| Identity | `DT_PassiveEffects` row `<Id>` | Name, description, `BattleBuffs` soft path to the new BP. |
| Magnitude | `DT_Balancing_Luminas` rows cloned from the vanilla keys | Chance, duration, AP, heal %, damage multiplier. **New row names.** |
| Hover | culture-invariant FText on the passive row | New `ST_PassiveEffects` keys are missing from locres. |
| Use | `DT_WeaponDefinitions` `Level1Lumina`…`Level5Lumina` | Lvl 33 / 66 / 111 / 222 / 333 (tier ends). Not auto-assigned. |

Ids **must** start with `WepFx_`.

## Spec file

Shipped stock:

`src/E33ModEditor.Core/Weapons/Data/weapon_effects.json`

Working overlay (wins if present):

`work/weapon_effects.json`

### Direct clone (graph already has its own float keys)

Use this for Attack Burn, crit-applies-burn, heal-on-crit, lifesteal, AP-on-counter, …

```json
{
  "effects": [
    {
      "id": "WepFx_HeavyAttackBurn",
      "displayName": "Heavy Attack Burn",
      "description": "Base attacks apply Burn at 4× for 5 turns.",
      "family": "direct",
      "sourceBp": "BP_LU_AttackBurn",
      "sourcePassive": "AttackBurn",
      "sourcePathContains": "OnBaseAttack",
      "balancing": [
        { "key": "WepFx_HeavyAttackBurn_BurnDamageMultiplier", "cloneFrom": "AttackBurn_BurnDamageMultiplier", "value": 4.0 },
        { "key": "WepFx_HeavyAttackBurn_Duration", "cloneFrom": "AttackBurn_Duration", "value": 5.0 }
      ]
    }
  ]
}
```

`replaceNames` is filled automatically from each `cloneFrom` → `key`. Add extra
pairs only for buff class / enum retargets.

### Elemental skill-damage family

`BP_LU_SkillsFireDMG+` is a thin child. The **parent** `BP_LU_ElementalDMG+`
hardcodes `SkillsFireDMG+_DamageIncrease` … `SkillsVoidDMG+_DamageIncrease`.
Cloning only the child and adding a new key does nothing — the parent still
reads the vanilla Fire key. A cloned child also cannot inherit a cloned
parent: the new parent class is missing from usmap and will not serialize.

This family clones the **parent graph** as a sibling Blueprint, rewrites every
element key to `<Id>_DamageIncrease`, and copies the child's `DamageType` /
`CheckDamageReason` onto the clone so it still filters to one element.
Vanilla SkillsFireDMG+ stays at 20%.

```json
{
  "id": "WepFx_FireSkills50",
  "displayName": "Fire Skills +50%",
  "description": "+50% Fire skill damage.",
  "family": "elemental-skill-dmg",
  "element": "Fire",
  "value": 0.5
}
```

`element`: Fire, Ice, Lightning, Earth, Dark, Light, Physical, Void.

### Stain start / stain echo (built in)

Shipped in code (`WeaponEffectsBuiltins`), not JSON:

- `stain-start` — clone parent `BP_LU_Stains_OnBattleStart1`, set stain `ByteConst` and amount `IntConst` (2 or 4) for Fire / Ice / Lightning / Earth. Light/Dark have no vanilla Start child; they still appear in the echo matrix.
- `stain-echo` — clone parent `BP_LU_Stains_Stains_+StainOnSpecificStainGenerated` and rewrite `TypeToTypeMap` to one pair **plus** set `InitialStainTypeToListen`. The graph only converts when the gained stain equals the listener, so both must match `fromStain`. Cooked enum FNames are fully qualified (`EAttackType::NewEnumerator2`) — bare `NewEnumeratorN` values never match in game.

Ground truth (decoded from vanilla graphs): stain bytes Fire=2 Ice=3 Lightning=4
Earth=5 Dark=6 Light=7; `EAttackType` enumerators 0–5 = Fire Ice Lightning
Earth Dark Light.

### Stock effects by theme (grouped, not per-passive)

The hub groups created passives by theme. Within a theme each passive is its own clone with its own `DT_Balancing_Luminas` keys.

- **Status Consume — Base vs Skills (18)** — consume Slow / Powerless / Defenceless for bonus damage. Base-attack variants check `DamageReason==4` (vanilla), Skills variants check `DamageReason==1` (SkillCast). Each status ×3 tiers: I ×1.5 (0.5), II ×2 (1.0), III ×2.5 (1.5). Descriptions say “with base attack” vs “with skills”. Names: *Slow Consume, Slow Consume II/III, Powerless Consume I/II/III, Defenceless Consume I/II/III* and *… (Skills)* for the 9 skills variants (e.g. `WepFx_SlowConsumeSkills`).
- **Lune — Stain Start (8)** — `Fire/Ice/Lightning/Earth Start 2` and `… 4`. `WepFx_FireStart2` etc. Lune only.
- **Lune — Stain Echo / Conversion (36)** — full 6×6 `Fire/Ice/Lightning/Earth/Dark/Light → Fire/Ice/Lightning/Earth/Dark/Light` matrix, e.g. `WepFx_EchoFireFire` … `WepFx_EchoLightLight`. Includes same-type doubles.
- **Lune — Stain utility (5)** — `TurnStart3/4`, `LightOnConsume100`, `DarkDmgPerStain100`, `ConsumeLightOrDarkDmg`, `ThunderBurn250`.
- **Blade — Conditional (7)** — `CursedBlade`, `BlightBlade`, `BerserkBlade`, `PowerfulBlade`, `RushBlade`, `ShellBlade`, `RegenBlade` — +20–50% while status active.
- **Burn family (7)** — `AttackBurn5/10`, `BurnDmg2/3/4`, `CounterBurn10`.
- **Skill Consume Burn (5)** — `WepFx_BurnConsume1Skills`…`5Skills` (I–V): skills consume up to 25/50/100/250/1000 Burn for +1% damage per Burn. `BP_LU_ConsumingAttack` kept on Burn (no buff retarget); skills-only via the `*Skills` id-suffix patch (**`DamageReason == 1`**, SkillCast). Not in the status-consume exclusive group.

  > **Do not use `DamageReason != 4` here.** Burn's damage-over-time tick is
  > `DamageReason == 2` ("Invalid") and the Burn aggregator credits that tick
  > to the character who *first* applied Burn. With `!= 4` the passive fires on
  > its own DOT, removes the full stack (up to the cap), and Burn can never be
  > stacked — even by other party members once the wearer applied the first
  > stack. The same applies to the Slow / Powerless / Defenceless skills
  > variants. Vanilla `BP_LU_ThunderSkillConsumeBurn` is the reference shape:
  > it already gates on `DamageReason == 1`.
- **Counter / Shield (6)** — `CounterShield2/3`, `CounterDmg2/3`, `AtkShield2/3`.
- **Break & Augmented Attack (4)** — `Break2/3`, `AugAtk2/3`.
- **Self-Inflict (6)** — `SelfBurn2/3`, `SelfBlight1/2/3`, `SelfBerserk1/2/3`.
- **Crit / Mask & Other (3)** — `LightningCrit2/3`, `MaskStack50`.
- **Other / future** — `elemental-skill-dmg` etc. remain their own family when added.

Skipped when the cooked graph has no honest knob: extra lightning hits, Free Aim extra shots, Perfection share without Verso penalty, all-buffs-in-one-slot, AP-above-9, Virtuose triple damage, stain-generation 60%.

### Status / requirement retarget

Same as pictos status-affinity: clone a graph that already checks a buff,
then add `replaceNames` for the buff package, class, dynamic-cast node, and
the new balancing keys. Put the new floats in `balancing` with a `cloneFrom`
that exists.

Character-system graphs (Stains, Perfection, Masks, Foretell) only fire on
that character. Set `"character": "Lune"` so the Weapons dropdown groups it.


## Pack

Weapons hub → **Create new weapon effects** → toggle on → run the CLI/standalone build.

Then either assign the id on a weapon in the Weapons list, **or** put it in
the Tier Editor **Effects** list (and gate it on **Characters**) so the
randomizer can roll it.

Created ids show under **created** in the passive dropdown even before a
weapon uses them.

Do not place these from the creator into shops or drops.

## Randomizer (Effects + Characters)

Tier Editor tabs:

- **Effects** — T1–T5 + Do Not Use + Unplaced. Created `WepFx_*` start Unplaced.
- **Characters** — one row per effect; click Gustave / Lune / Maelle / Sciel /
  Verso / Monoco to allow or block.

When **Randomize weapon passives** is on (default), each weapon that is in
Weapons T1–T5 gets new passives:

1. Slot count is one per **weapon** item tier — the five slots unlock at the
   tier ends 33/66/111/222/333, so each catalyst band
   (T1 1–33, T2 34–66, T3 67–111, T4 112–222, T5 223–333) starts just past the
   previous tier's unlock and T1–T5 fill **1 / 2 / 3 / 4 / 5** slots
   (`Level1Lumina`…`Level5Lumina`).
2. The slots are **archetype-driven** (see
   `docs/weapon-effect-archetypes.md`): one primary archetype per weapon
   (weighted by eligible effect count), then each archetype slot draws the
   primary (50%) or a sibling link, and the effect inside it. T1 = 1 wildcard,
   T2 = 1 archetype + 1 wildcard, T3 = 2 + 1, T4 = 3 + 1, T5 = 4 + 1.
3. Every pick still rolls an **effect tier** from the weapon's item tier
   (T5 mix: 60% T5 / 30% T4 / 10% T3). Gustave always uses the T3 mix
   (T1–T3 only) and his generic (non-system) T1–T3 effects are eligible even
   if his Characters chip is off; he never rolls `AutoDeath`.
4. The **wildcard** is the old uniform pick — character-allowed, not already
   on the weapon — and fills the weakest slot (`Level1Lumina`). It now prefers
   a **neutral** effect (one with no conflicting build condition) so it cannot
   break the kit the archetype picks just established. Stain-echo (the 6×6
   `WepFx_Echo*` matrix plus vanilla `Stains_LightningOnFire`) is **one per
   weapon**, and the status finishers
   (`WepFx_Slow/Powerless/DefenselessConsume*`, base and Skills variants) are
   **one per weapon total** — one consume effect across all three statuses.
5. **Conditions are enforced per weapon.** Every effect may carry condition
   tags (see `docs/weapon-effect-archetypes.md` → Condition layer); two
   mutually exclusive conditions can never share a weapon (Full Strength ×
   Self Burn, halve-heal × heal-on-crit, consume-Stains × per-Stain scaling, …).
   Soft axes (counter, damage-taken, multi-hit, and the whole Shield / Shell
   cluster) are **informational** and still combine freely. The same table also
   carries **`synergies`** (trigger tag → preferred tags): once a weapon holds a
   Shield Spender, later slots and the wildcard are weighted toward Shield
   Makers so the spender gets a generator to refill from. The table is
   `Loot/Data/weapon_effect_conditions.json`, authored by
   `Planning/archetypes/build_conditions.py`; the pack-time sweep fails the
   pack on any conflicting pair.
6. **Lune** stain effects are gated by the weapon's **melee element**
   (`BaseDamageType`): element-specific stains, echoes and payoffs of the other
   elements are removed from the pool. Physical/Void keep the neutral
   `Stain Cycling` family (random generation). The wildcard is gated too.
7. Archetype picks are ranked strongest-first into the higher slots, so
   `Level1Lumina` keeps the wildcard and `Level5Lumina` gets the strongest
   archetype effect.

The same effect may land on many weapons. It never appears twice on one
weapon. Unplaced weapons and Do Not Use weapons keep vanilla passives.

### Fifth slot

`WeaponPassiveSlotKit` adds `Level5Lumina` to the `S_WeaponDefinition`
UserDefinedStruct (schema + default instance), to every
`DT_WeaponDefinitions` row, and the quality-5 entry to
`FL_ItemUpgradeHelpers.GetWeaponPassivesByUpgradeLevel`. The engine already
carries five quality levels (`EWeaponQuality` 0–5,
`GetWeaponQualityLevelsEnum` {1..5}, `GetUnlockedPassiveEffectsIDs` loops
0–5); quality 5 (level 333+, the end of the T5 band) simply had nothing to
grant.

The quality bands are Q0 ≤32 / Q1 33–65 / Q2 66–110 / Q3 111–221 /
Q4 222–332 / Q5 333+, so the slots unlock at the tier ends. Because the same
quality value drives the attribute-upgrade unlocks,
`WeaponOverLevelKit` retimes `GetAttributeUpgradeRanks` from `{2,4,5}` to
`{1,3,4}` to keep those at 33/111/222.

## Checklist

- [ ] Id starts with `WepFx_`
- [ ] Every balancing `key` ≠ `cloneFrom`
- [ ] `FolderName` unique (`retoc list --path` on the `_P.utoc`)
- [ ] Passive `BattleBuffs` points at `/Game/.../Created/BP_LU_<Id>`
- [ ] FText is culture-invariant
- [ ] Assigned on a weapon row and packed
- [ ] Game fully quit so IoStore reloads `~mods`
- [ ] Editor app fully restarted after rebuilds — a running instance keeps the old assemblies and can pack stale tables while reporting success
- [ ] `fx-smoke` green (rows, BattleBuffs, string table, echo map + listener round-trip)
- [ ] `fx-roll` green (condition conflicts 0, element mismatches 0, tier mix/cohesion unchanged)
- [ ] Echo clones verified: single fully-qualified map pair **and** matching `InitialStainTypeToListen` (`dumpcdo` on the clone)

## Instructing Grok

Give:

1. Source effect (in-game name or `BP_LU_` / passive id)
2. What changes (magnitude, chance, duration, AP, heal %, element, status)
3. Display name + one-line description
4. Optional: which character, which weapon slot to assign

Grok adds a spec, packs, does not retune vanilla keys.

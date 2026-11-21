# Expedition Infinity 1.4.0 — source damage caps

Released 2026-11-21. App version **1.4.0**; seed packs use `_v1.4.0_P`.

> **1.4.1 errata:** 1.4.0 shipped with the numeric `BuffEffect` and `Invalid`
> rows swapped. Burn is `BuffEffect` (2), so 1.4.0 actually capped it at 9,999.
> 1.4.1 corrects this to 3,333; the table below shows the intended mapping.

## Headline: different damage caps by source

1.4.0 replaces the single hard-coded 9,999 hero cap with source-based caps in
`BP_BattleDamages.ApplyClamp`. There is **no new picto** and no new character
property; the existing `DamageReason` and `DamageLimitUnlockRefCount` (Ultima
Power) drive the two states.

### Normal state (no Ultima Power)

| Source | `DamageReason` | Cap |
|---|---:|---:|
| Invalid | 0 | 9,999 |
| Skills | 1 | **33,333** |
| BuffEffect / Burn | 2 | **3,333** |
| Free Aim | 3 | **3,333** |
| Base attack | 4 | **6,666** |
| Counter | 5 | **333,333** |
| Lumina | 6 | 9,999 |
| Item | 7 | 9,999 |
| Gradient counter | 8 | **333,333** |
| Range counter | 9 | **333,333** |
| AOE counter | 10 | **333,333** |
| Jump counter | 11 | **333,333** |

### Ultima Power active

- **Skills and every counter variant are fully unlocked** (no cap from this
  system).
- **Free Aim, base attack and buff effect are capped at 333,333.**
- Invalid, Lumina and Item damage are also capped at 333,333.

The challenge `HeroesDamageLimitOverride` still runs afterwards and can lower
any cap. Enemy damage is unchanged: the vanilla hero-only team check remains.

### Why this is safer than the old three-tier attempt

- No new `AC_jRPG_CharacterBattleStats` property, no second
  `SF_ReferenceCounter`, and no middle-tier picto.
- The caps are a single bytecode edit to `BP_BattleDamages.ApplyClamp`, using
  the existing `DamageReason` enum and the existing Ultima Power reference
  counter.
- The unlocked block is guarded by `EqualEqual(local_clampValue, 0)` before
  `JumpIfNot`; the first 1.4.0 test build had this inverted (`NotEqual`), which
  made locked builds use the Ultima caps and Ultima builds go fully uncapped.
  The fix and a branch-flow regression check are in `damage-cap-smoke`.

### Seed generator

- Damage caps are a **core, always-on seed-generator setting**
  (`GameplayTweaksKit.Settings.DamageCaps = true`). There is **no UI** for it;
  `GeneratorProfile.Balance.ToTweaks` forces it on for every preset.
- The standalone pack path applies and verifies the caps after the crit damage
  patch, then verifies both edits on the same `BP_BattleDamages` asset.

## Seeds

The 1.3.x curated seed numbers are kept for 1.4.0; only the pack revision
changes:

| Mode | Seed | Pack |
|---|---:|---|
| Easy | 61281975 | `ExpeditionInfinity_61281975_v1.4.0_P` |
| Normal | 87694963 | `ExpeditionInfinity_87694963_v1.4.0_P` |
| Hard | 23035873 | `ExpeditionInfinity_23035873_v1.4.0_P` |

Easy, Normal and Hard keep their 1.3.10 balance: proc cards hidden, camera off,
tutorials off, Party of Five core; Easy 2× Lumina and instant mastery, Normal
1× and instant mastery, Hard 0.5× Lumina, tightened windows and the extreme
T4–T5 boss defense curve. Permadeath, Challenges and the Burn stack cap remain
out of the standalone scope.

## Languages

English source plus shared language packs for `fr-FR`, `pl`, `ru`, `es` and
`zh-Hans` under `Languages/<culture>/`. The generator installs the selected
culture's pak alongside the seed.

## Verification

- `damage-cap-smoke`: 11 checks — all 12 damage reasons, locked and Ultima
  states, branch-flow target, idempotence, staging, and crit composition.
- `tweaks-smoke` and `row-damage-smoke` pass.
- Each seed container is packed and `retoc verify` returns `verified`.
- In-game test: Free Aim, base attack, buff-effect damage, every skill, all
  five counter types, DOT, and the same set with Ultima Power active.

## Downloads

The generator binaries and notes are attached to the GitHub release. Mod seed
packs and shared language packs are distributed in the release folder /
archive, not mixed into the executable download because the self-contained
binaries can trigger antivirus false positives.

## Version

- App version **1.4.0**
- Pack revision **`_v1.4.0`**
- No backwards compatibility for old pack containers; old `.eiseed.json`
  profiles still load.

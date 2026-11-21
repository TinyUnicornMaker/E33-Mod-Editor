# Expedition Infinity — changes from 1.4.0 to 1.4.1

## Read this first

- **Data-only seed-pack correction.** No new generator application binary is
  included in 1.4.1.
- Pack names changed to **`_v1.4.1_P`**. Remove old 1.3.x/1.4.0 containers
  before installing a 1.4.1 seed.
- Same seed numbers as 1.3.10/1.4.0 (Easy 61281975, Normal 87694963, Hard
  23035873); only the pack revision and the Burn/BuffEffect mapping changed.
- Old `.eiseed.json` profiles still load.

> **1.4.2 update:** the Ultima caps for `BuffEffect / Burn` (2) and `Lumina`
> (6) were later raised to **3,333,333**. The 1.4.1 values below are
> historical; see `release-notes-1.4.2.md`.

## 1.4.1 — Burn / BuffEffect correction

The cooked `EDamageReason` enum maps:

| Numeric | Enumerator | Display name |
|---:|---|---|
| 0 | `NewEnumerator2` | **Invalid** |
| 1 | `NewEnumerator0` | **SkillCast** |
| 2 | `NewEnumerator1` | **BuffEffect** |

`BP_BattleBuff_BurnAggregator` passes `2` in `MakeDamageBuilder`'s
`DamageReason` slot, so **Burn is BuffEffect**, not Invalid. 1.4.0 had
`BuffEffect = 0` / `Invalid = 2` in `DamageCapKit`, which put Burn in the
9,999 bucket. 1.4.1 swaps them.

### Corrected no-Ultima caps

| Source | Cap |
|---|---:|
| Invalid | 9,999 |
| Skills | **33,333** |
| **BuffEffect / Burn** | **3,333** |
| Free Aim | **3,333** |
| Base attack | **6,666** |
| Counter | **333,333** |
| Lumina | 9,999 |
| Item | 9,999 |
| Gradient / Range / AOE / Jump counters | **333,333** |

### Corrected Ultima Power caps

- Skills and all counter variants: unlimited.
- **Burn / BuffEffect: 333,333 per tick.**
- Free Aim, base attack: 333,333.
- Invalid, Lumina, Item: 333,333.

Challenge overrides still apply; enemy damage is unchanged.

## Generator application

The 1.4.0 generator remains the current application; it was not rebuilt for
1.4.1. Its custom-seed path still uses the old 0/2 mapping, so use the
prebuilt 1.4.1 seed packs for the corrected Burn cap. A future generator build
will pick up the source fix.

## Release layout

```
Expedition Infinity 1.4.1/
  [Easy] 61281975/
  [Normal] 87694963/
  [Hard] 23035873/
  Languages/
    fr-FR/ pl/ ru/ es/ zh-Hans/
  BUILD.txt
  INSTALL.txt
  RELEASE-NOTES.md
  UPDATES.md (this file)
  SHA256SUMS
```

## Verification

- `damage-cap-smoke` updated with explicit `BuffEffect == 2` / `Invalid == 0`
  guards; all 12 reasons pass in both states.
- All three 1.4.1 seed containers: `retoc verify` → `verified`.
- `tweaks-smoke` and `row-damage-smoke` pass.

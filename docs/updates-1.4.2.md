# Expedition Infinity — changes from 1.4.1 to 1.4.2

## Read this first

- **Data-only seed-pack release.** No generator application binary is included.
- Pack names changed to **`_v1.4.2_P`**. Remove old 1.3.x/1.4.x containers
  before installing a 1.4.2 seed.
- Same seed numbers: Easy 61281975, Normal 87694963, Hard 23035873.
- Old `.eiseed.json` profiles still load.

## 1.4.2 — Ultima caps for Burn/Lumina

With Ultima Power active, these caps are raised from **333,333** to
**3,333,333**:

- `DamageReason 2` — **BuffEffect / Burn** (also Storm Caller / Maelstrom)
- `DamageReason 6` — **Lumina** (weapon/passive-triggered strikes, e.g.
  `TriggerLightningOnCrit`)

Normal-state caps are unchanged.

### Caps

| Source | `DamageReason` | No Ultima Power | Ultima Power |
|---|---:|---:|---:|
| Invalid | 0 | 9,999 | 333,333 |
| Skills | 1 | 33,333 | unlimited |
| **BuffEffect / Burn** | **2** | **3,333** | **3,333,333** |
| Free Aim | 3 | 3,333 | 333,333 |
| Base attack | 4 | 6,666 | 333,333 |
| Counter | 5 | 333,333 | unlimited |
| **Lumina** | **6** | 9,999 | **3,333,333** |
| Item | 7 | 9,999 | 333,333 |
| Gradient / Range / AOE / Jump counters | 8–11 | 333,333 | unlimited |

Burn ticks and Storm Caller/Maelstrom strikes use the BuffEffect row; the
weapon `TriggerLightningOnCrit` strike uses the Lumina row. Challenge
overrides still apply; enemy damage is unchanged.

## Generator application

The 1.4.0 generator remains the current application; it was not rebuilt. Its
custom-seed path has the older cap table, so use the prebuilt 1.4.2 seed packs
for these caps. A future generator build will pick up the source fix.

## Release layout

```
Expedition Infinity 1.4.2/
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

- `damage-cap-smoke`: 16 checks, including explicit BuffEffect=2, Invalid=0 and
  Lumina=6 guards, all 12 reasons in both states.
- All three 1.4.2 seed containers: `retoc verify` → `verified`.
- `tweaks-smoke` and `row-damage-smoke` pass.

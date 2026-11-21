# Expedition Infinity 1.4.2 — Ultima caps for Burn/Lumina

Released 2026-11-21. **Data-only seed-pack release.** No generator application
binary is included. Seed packs use `_v1.4.2_P`.

## What changed

With Ultima Power active, the caps for two non-skill damage reasons are raised
from **333,333** to **3,333,333**:

- `DamageReason 2` — **BuffEffect / Burn** (also Storm Caller / Maelstrom zone
  strikes)
- `DamageReason 6` — **Lumina** (weapon/passive-triggered strikes such as
  `TriggerLightningOnCrit`)

Normal-state caps are unchanged.

## Caps

| Source | `DamageReason` | No Ultima Power | Ultima Power |
|---|---:|---:|---:|
| Invalid | 0 | 9,999 | 333,333 |
| Skills | 1 | **33,333** | unlimited |
| **BuffEffect / Burn** | **2** | **3,333** | **3,333,333** |
| Free Aim | 3 | **3,333** | 333,333 |
| Base attack | 4 | **6,666** | 333,333 |
| Counter | 5 | **333,333** | unlimited |
| **Lumina** | **6** | 9,999 | **3,333,333** |
| Item | 7 | 9,999 | 333,333 |
| Gradient / Range / AOE / Jump counters | 8–11 | **333,333** | unlimited |

**Burn / BuffEffect with Ultima Power: 3,333,333 per tick.**
**Lumina (weapon-triggered Lightning strikes, etc.) with Ultima Power:
3,333,333 per hit.**

Challenge overrides still apply and can lower any cap; enemy damage is
unchanged.

## What this affects in practice

- **Burn** damage-over-time ticks: 3,333 normal → 3,333,333 with Ultima.
- **Storm Caller / Maelstrom** zone strikes: same BuffEffect row.
- **`TriggerLightningOnCrit`** weapon strikes: Lumina row, now 3,333,333 with
  Ultima (still 9,999 normally).
- Other Lumina-sourced damage procs: same Lumina row.
- **Skills and counters** remain unlimited with Ultima.
- Free Aim, base attack, Invalid and Item remain at 333,333 with Ultima.

## No application binary in 1.4.2

The generator was not rebuilt. The existing 1.4.0 generator remains the current
application; its custom-seed path has the older cap table. Use the prebuilt
1.4.2 seed packs below. A future generator build will pick up the source fix.

## Seeds

| Mode | Seed | Pack |
|---|---:|---|
| Easy | 61281975 | `ExpeditionInfinity_61281975_v1.4.2_P` |
| Normal | 87694963 | `ExpeditionInfinity_87694963_v1.4.2_P` |
| Hard | 23035873 | `ExpeditionInfinity_23035873_v1.4.2_P` |

Everything else from 1.4.0/1.4.1 carries forward: proc cards hidden, camera
off, tutorials off, Party of Five core; Easy 2× Lumina / Normal 1× / Hard 0.5×,
and the Hard extreme T4–T5 boss defense curve.

## Verification

- `damage-cap-smoke`: 16 checks, including explicit BuffEffect=2, Invalid=0 and
  Lumina=6 guards, all 12 reasons in both states, branch flow, idempotence and
  crit composition.
- All three 1.4.2 seed containers: `retoc verify` → `verified`.
- `tweaks-smoke` and `row-damage-smoke` pass.

## Downloads

- **Seed packs + language packs:** `ExpeditionInfinity_1.4.2_seed-packs.zip`
- **Hashes:** `ExpeditionInfinity_1.4.2_SHA256SUMS`

No `ExpeditionInfinity-1.4.2` / `.exe` application asset is included.

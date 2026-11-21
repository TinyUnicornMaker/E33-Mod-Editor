# Expedition Infinity 1.4.1 — Burn / BuffEffect cap correction

Released 2026-11-21. This is a **data-only seed-pack correction**; no new
generator application binary is shipped in this release. Seed packs use
`_v1.4.1_P`.

## What changed

1.4.0 mapped the two non-direct damage reasons backwards:

- numeric `0` is **Invalid**
- numeric `1` is **SkillCast**
- numeric `2` is **BuffEffect**

The cooked `EDamageReason` `DisplayNameMap` proves this, and
`BP_BattleBuff_BurnAggregator` passes **2** in `MakeDamageBuilder`'s
`DamageReason` slot. Burn is therefore **BuffEffect**, not Invalid. The Burn
tick is also Fire-element flat straight damage (`AddStraightDamageFlat`).

1.4.0 shipped `BuffEffect = 0` and `Invalid = 2`, so Burn was capped at 9,999
instead of 3,333. 1.4.1 swaps those two constants.

## Corrected caps

| Source | `DamageReason` | No Ultima Power | Ultima Power |
|---|---:|---:|---:|
| Invalid | 0 | 9,999 | 333,333 |
| Skills | 1 | **33,333** | unlimited |
| **BuffEffect / Burn** | **2** | **3,333** | **333,333** |
| Free Aim | 3 | **3,333** | **333,333** |
| Base attack | 4 | **6,666** | **333,333** |
| Counter | 5 | **333,333** | unlimited |
| Lumina | 6 | 9,999 | 333,333 |
| Item | 7 | 9,999 | 333,333 |
| Gradient counter | 8 | **333,333** | unlimited |
| Range counter | 9 | **333,333** | unlimited |
| AOE counter | 10 | **333,333** | unlimited |
| Jump counter | 11 | **333,333** | unlimited |

**Burn / BuffEffect cap:** **3,333 per tick** normally, **333,333 per tick**
with Ultima Power active.

The challenge `HeroesDamageLimitOverride` still applies and can lower any cap.
Enemy damage is unchanged.

## Seeds

The 1.3.x curated seed numbers are kept:

| Mode | Seed | Pack |
|---|---:|---|
| Easy | 61281975 | `ExpeditionInfinity_61281975_v1.4.1_P` |
| Normal | 87694963 | `ExpeditionInfinity_87694963_v1.4.1_P` |
| Hard | 23035873 | `ExpeditionInfinity_23035873_v1.4.1_P` |

Everything else from 1.4.0 carries forward: proc cards hidden, camera off,
tutorials off, Party of Five core; Easy 2× Lumina and instant mastery, Normal
1× and instant mastery, Hard 0.5× Lumina with the extreme T4–T5 boss defense
curve. Permadeath, Challenges and the Burn stack cap remain out of the
standalone scope.

## No application binary in 1.4.1

Per the 1.4.1 scope, the generator application was **not rebuilt**. The
existing 1.4.0 generator remains the current application. Its custom-seed path
still contains the old 0/2 mapping, so use the prebuilt 1.4.1 seed packs below
for the corrected Burn cap. A future generator build will pick up the source
fix.

## Verification

- `damage-cap-smoke`: all 12 damage reasons in both states, explicit
  `BuffEffect == 2` / `Invalid == 0` guards, branch flow, idempotence, staging,
  and crit composition.
- All three 1.4.1 seed containers: `retoc verify` → `verified`.
- `tweaks-smoke` and `row-damage-smoke` pass.

## Downloads

- **Seed packs + language packs:** `ExpeditionInfinity_1.4.1_seed-packs.zip`
- **Hashes:** `ExpeditionInfinity_1.4.1_SHA256SUMS`

No `ExpeditionInfinity-1.4.1` or `.exe` application asset is included.

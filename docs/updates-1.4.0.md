# Expedition Infinity — changes from 1.3.10 to 1.4.0

## Read this first

- **Pack names changed to `_v1.4.0_P`.** Old 1.3.10 containers are not migrated;
  remove them before installing a 1.4.0 seed.
- **Same seed numbers as 1.3.10** (Easy 61281975, Normal 87694963, Hard
  23035873). Loot and stat rolls are unchanged; only the pack revision and the
  damage-cap rules changed.
- Old `.eiseed.json` profiles still load.
- The generator binaries are distributed separately from the seed-pack folder
  because the self-contained executables can trigger antivirus false
  positives.

## 1.4.0 — source damage caps

The single vanilla 9,999 hero cap is replaced by a `DamageReason` lookup in
`BP_BattleDamages.ApplyClamp`. No new picto and no new character property were
added; the existing Ultima Power reference counter selects the second state.

### No Ultima Power

| Source | Cap |
|---|---:|
| Buff effect | **3,333** |
| Skills | **33,333** |
| DOT / invalid | 9,999 |
| Free Aim | **3,333** |
| Base attack | **6,666** |
| Counter | **333,333** |
| Lumina | 9,999 |
| Item | 9,999 |
| Gradient counter | **333,333** |
| Range counter | **333,333** |
| AOE counter | **333,333** |
| Jump counter | **333,333** |

### Ultima Power active

- Skills and **all counter variants** are fully unlocked.
- Free Aim, base attack and buff effect are capped at **333,333**.
- DOT, Lumina and Item damage are also capped at 333,333.

Challenge damage-limit overrides still apply and can lower any cap. Enemy
damage is unaffected.

### Implementation and safety

- `DamageCapKit` inserts the locked cap immediately after the vanilla `9999`
  assignment and a second unlocked block before the challenge override.
- The unlocked block tests `EqualEqual(local_clampValue, 0)` before
  `JumpIfNot`; a first test build used `NotEqual`, which inverted the branches.
  The fixed build and a branch-target regression check are covered by
  `damage-cap-smoke`.
- Damage caps are a core seed-generator setting:
  `GameplayTweaksKit.Settings.DamageCaps = true`, forced on by
  `GeneratorProfile.Balance.ToTweaks`. **No UI control.**
- `FullPackKit` applies and verifies the caps after the crit patch and verifies
  both edits on the same `BP_BattleDamages` asset.

## Carried forward from 1.3.10

- Three curated seeds: Easy / Normal / Hard.
- Battle proc cards hidden (`LimitBattleProcCards` on, cap **0**).
- Close/first-person camera off in the shipped seeds.
- Tutorials off; Party of Five core and always on.
- Easy: regular defense windows, 25% weaker regular-enemy bonuses, instant
  Picto mastery, 2× Lumina.
- Normal: standard Expedition Infinity, instant Picto mastery, 1× Lumina.
- Hard: Brutal + extreme T4–T5 boss defense (1× at the T4 floor to 30× at
  L99), tightened windows, 2× HP bonus, 1.5× speed/attack bonuses, 0.5× Lumina.
  Permadeath stays off.
- Shared language packs: `fr-FR`, `pl`, `ru`, `es`, `zh-Hans`.
- Picto over-level scaling rows 34–333, linear +1% of the level-33 base per
  level.

## Release layout

```
Expedition Infinity 1.4.0/
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

- `damage-cap-smoke`: 11 checks, all 12 `DamageReason` values in both states,
  branch flow, idempotence, staging, and crit composition.
- `tweaks-smoke` and `row-damage-smoke` pass.
- Each seed container: `retoc verify` → `verified`.
- In-game pass still required for Free Aim, base attack, buff effect, skills,
  all five counter types, DOT, and Ultima Power interactions.

## Known limitation

Ultima Power keeps its existing conditional trigger: the wearer must take a
hit over 9,999 once before `DamageLimitUnlockRefCount` becomes active. The
source caps switch only after that reference is present.

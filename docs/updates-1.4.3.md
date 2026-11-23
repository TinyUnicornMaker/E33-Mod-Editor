# Expedition Infinity — changes from 1.4.2 to 1.4.3

1.4.3 is a standalone generator release and a new seed-pack revision.

## Balance

- **Damage caps are editable in the seed generator.** Every `DamageReason` row
  has a Normal cap and an Ultima cap. `0` means unlimited. Normal Lumina defaults
  to 3,333; the Ultima Lumina/Buff defaults remain 3,333,333.
- **Free Aim repeat penalty** is a new balance toggle, on by default. Regular
  Free Aim shots in a turn use `0.97^shotsFired` up to 100 shots.

## Quality of Life

- **Target glow** is available as a QoL toggle. It is off by default.

## Fixes

- Turning **Randomize weapon passives** off now leaves the entire weapon
  definition vanilla, including attack/free-aim stats and element. The separate
  weapon-stat toggle has no effect while passives are off.
- The spoiler codex reads staged picto name/description/Lumina tables so
  mod-added pictos no longer show blank effect text.

## Pack names

Seed packs use `_v1.4.3_P`. Remove older 1.3.x/1.4.x seed containers before
installing a 1.4.3 seed.

## Downloads

- `ExpeditionInfinity-1.4.3.exe` — Windows x64, self-contained
- `ExpeditionInfinity-1.4.3` — Linux x64, self-contained
- `ExpeditionInfinity_1.4.3_seed-packs.zip` — Easy / Normal / Hard seed packs
- `ExpeditionInfinity_1.4.3_SHA256SUMS`

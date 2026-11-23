# Expedition Infinity — changes from 1.4.3 to 1.4.4

1.4.4 is a codex/UI correction release. Gameplay and pak loot data are
unchanged from 1.4.3.

## Spoiler codex

- **Venue variants are labeled clearly.** Rows cloned by `BossVenueKit` now
  show `[Venue Lxx]` and the encounter area, e.g.
  `Frost Eveque [Venue L50]` / `World Map — WM_EvequeIce_Alpha` instead of the
  donor row ID and donor geography.
- **Item sources distinguish same-named enemies.** Source deduplication now
  includes area and level, so a drop from a world-map clone no longer looks
  like it came from the native boss row.
- This fixes the reported “Frost Eveque drops do not match” confusion. The
  world-map fight uses `BV_SM_Boss_Eveque_Ice_ALPHA_50` (Chevalam package);
  the base `SM_Boss_Eveque_Ice_ALPHA` row has a different native package.

## Pack names

Seed packs use `_v1.4.4_P`.

## Downloads

- `ExpeditionInfinity-1.4.4.exe` — Windows x64, self-contained
- `ExpeditionInfinity-1.4.4` — Linux x64, self-contained
- `ExpeditionInfinity_1.4.4_seed-packs.zip` — Easy / Normal / Hard seed packs
- `ExpeditionInfinity_1.4.4_SHA256SUMS`

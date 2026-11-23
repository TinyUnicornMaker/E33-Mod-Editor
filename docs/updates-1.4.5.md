# Expedition Infinity — changes from 1.4.4 to 1.4.5

1.4.5 is a loot-assignment update. Gameplay balance values are unchanged.

## Loot assignment

- Loot sources are now assigned in tier order:
  1. bosses and Nevrons, T1→T5
  2. shops, sorted by tier
  3. chests, sorted by tier
- Tint shards and potency shapes are restricted to **T1–T3**; tint shards still
  place three per tier for each tint.
- Shops and chests prefer unassigned items at their own tier, then fall back to
  tier-appropriate repeats.
- T4/T5 bosses use one T4/T5 weapon-or-picto plus multiple **T1–T3** pictos.
  T4 bosses never draw T5 items; T5 bosses prefer T5 then lower tiers.
- Hand-authored boss packages remain exempt from the one-item T4/T5 rule.
- Unreachable tutorial and venue variants now copy the playable row’s final
  drops; the remaining unreachable non-tutorial rows drop nothing.

## Pack names

Seed packs use `_v1.4.5_P`.

## Releases

This source update does not create a release artifact by itself. The next
release build uses the 1.4.5 version and pack revision.

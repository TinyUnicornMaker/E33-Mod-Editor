# Expedition Infinity — running update log

This is the permanent, newest-first chain of every recorded Expedition Infinity
update. Each entry links to the detailed change document where one exists.
Version tags in this repository begin at `v1.3.3`; the pre-tag history is noted
at the bottom.

## 1.4.6 — 2026-11-23 — weapon tier repair and one tier source

**Detail:** [updates-1.4.6.md](updates-1.4.6.md)

- Reacher weapon variants moved to **T4** (10 rows).
- Siren weapon variants moved to **T3** (10 rows).
- `VD_*` DLC weapon variants moved to **T5** (11 rows).
- Baguettes moved to **T1** (5 rows), with a roster-gate exception so all five
  can appear at T1.
- `DoNotUse` reduced to `03_Weapon_Placeholder`, `MitigatedPerfection`, and
  `Milerim`.
- `src/E33ModEditor.Core/Loot/Data/item_tiers.json` is now the only
  weapon-tier source. The `work/` overlay, archive copy, and backups were
  deleted without replacement.
- `ItemTiers`, `LootCatalog`, and `StoryPointKit` now share that source.
- New counts: T1 23, T2 21, T3 37, T4 29, T5 22; 132 tiered weapons total.
- Pack revision: `_v1.4.6_P`.

## 1.4.5 — 2026-11-23 — tier-ordered loot assignment

**Detail:** [updates-1.4.5.md](updates-1.4.5.md)

- Loot sources assigned in tier order: bosses/Nevrons T1→T5, then shops, then
  chests.
- Tint shards and potency shapes restricted to T1–T3.
- Shops and chests prefer unassigned same-tier items, then tier-appropriate
  repeats.
- T4/T5 bosses use one T4/T5 weapon-or-picto plus T1–T3 pictos; T4 bosses never
  draw T5 items.
- Hand-authored boss packages exempted from the one-item T4/T5 rule.
- Unreachable tutorial and venue variants copy their playable row's drops;
  remaining unreachable non-tutorial rows drop nothing.
- Pack revision: `_v1.4.5_P`.

## 1.4.4 — 2026-11-23 — spoiler codex venue variants

**Detail:** [updates-1.4.4.md](updates-1.4.4.md)

- Venue-cloned enemies show `[Venue Lxx]` and the borrowing encounter's area.
- Item-source rows distinguish same-named enemies by area and level.
- No gameplay, loot-table, or pak-content changes.
- Pack revision: `_v1.4.4_P`.

## 1.4.3 — 2026-11-23 — editable damage caps and QoL toggles

**Detail:** [updates-1.4.3.md](updates-1.4.3.md)

- Editable Normal and Ultima damage caps for every damage source.
- Free Aim repeat penalty toggle, on by default.
- Target-glow QoL toggle, off by default.
- Turning weapon-passive randomization off now leaves the whole weapon vanilla.
- Spoiler codex reads staged picto name/description/Lumina tables.
- Pack revision: `_v1.4.3_P`.

## 1.4.2 — 2026-11-21 — Ultima caps for Burn and Lumina

**Detail:** [updates-1.4.2.md](updates-1.4.2.md) ·
[release notes](release-notes-1.4.2.md)

- Raised Ultima `BuffEffect / Burn` and `Lumina` caps from 333,333 to
  3,333,333.
- Normal-state caps unchanged.
- Data-only seed-pack release.
- Pack revision: `_v1.4.2_P`.

## 1.4.1 — 2026-11-21 — Burn / BuffEffect cap correction

**Detail:** [updates-1.4.1.md](updates-1.4.1.md) ·
[release notes](release-notes-1.4.1.md)

- Corrected the numeric `Invalid`/`BuffEffect` mapping introduced in 1.4.0.
- Burn is `DamageReason 2` and was capped at 3,333 instead of 9,999.
- Data-only seed-pack correction; no new generator binary.
- Pack revision: `_v1.4.1_P`.

## 1.4.0 — 2026-11-21 — source-based damage caps

**Detail:** [updates-1.4.0.md](updates-1.4.0.md) ·
[release notes](release-notes-1.4.0.md)

- Replaced the single hero damage cap with per-`DamageReason` caps in
  `BP_BattleDamages.ApplyClamp`.
- Skills, Free Aim, base attacks, counters, Burn, Lumina, items, and gradient
  counters received separate Normal and Ultima limits.
- Existing Ultima Power reference count selects the high-cap state.
- Pack revision: `_v1.4.0_P`.

## 1.3.10 — 2026-11-16 — three seeds, shared language packs

**Detail:** [updates-1.3.5-to-1.3.10.md](updates-1.3.5-to-1.3.10.md)

- Shipped Easy, Normal, and Hard seeds.
- Shared language packs under `Languages/<culture>/`, including Simplified
  Chinese.
- Battle proc cards hidden; close/first-person camera disabled in the release
  seeds.
- Easy/Normal instant Picto mastery; Easy 2× Lumina, Hard 0.5×.
- Hard enables extreme T4–T5 boss defense.
- Fixed changed-key locres hashes and staged vanilla Auto Burn/Auto Curse rows.
- Pack revision: `_v1.3.10_P`.

## 1.3.9 — 2026-11-15 — donor extraction restored

**Detail:** [updates-1.3.5-to-1.3.10.md](updates-1.3.5-to-1.3.10.md)

- Restored the 1.3.5 donor-extraction path with script objects enabled.
- Removed the strict per-filter abort added in 1.3.8.
- Kept the Oodle working-directory/PATH hardening.
- Pack revision: `_v1.3.9_P`.

## 1.3.8 — 2026-11-15 — Windows donor extraction fix

**Detail:** [updates-1.3.5-to-1.3.10.md](updates-1.3.5-to-1.3.10.md)

- Ran `retoc` from its bundled tool folder with Oodle discoverable on Windows.
- Added per-filter extraction verification and surfaced real `retoc` errors.
- Temporarily used `--no-script-objects`; 1.3.9 reverted this.
- Pack revision: `_v1.3.8_P`.

## 1.3.7 — 2026-11-14 — Spanish, versioned executables, Picto scaling

**Detail:** [updates-1.3.5-to-1.3.10.md](updates-1.3.5-to-1.3.10.md)

- Added Spanish as a fifth pack and UI language.
- Every executable now carries its version in the filename.
- Extended Picto over-level scaling to 34–333 with linear +1% of the level-33
  base per level.
- Pack revision: `_v1.3.7_P`.

## 1.3.6 — 2026-11-14 — standalone scope cleanup

**Detail:** [updates-1.3.5-to-1.3.10.md](updates-1.3.5-to-1.3.10.md)

- Removed incomplete Permadeath, Challenges, and Burn-stack-cap experiments
  from the standalone pack path.
- Made the game-data folder selectable and movable.
- Added the battle-proc-card toggle.
- Moved Nevron controls from Randomizer to Balance.
- Signed percentage fields and clearer UI.
- Pack revision: `_v1.3.6_P`.

## 1.3.5 — 2026-11-08 — Challenges experiment and Simon facing

**History:** tag `v1.3.5`

- Added the Challenges module and Simon facing fix.
- The standalone 1.3.5 release excluded the experimental Challenges and Burn
  stack-cap modules.
- Pack revision: `_v1.3.5_P`.

## 1.3.4 — 2026-11-07 — extreme boss defense

**History:** tag `v1.3.4`

- Added the extreme T4–T5 boss defense toggle.
- Applied extreme defense to the Act 3 finale pins.
- Added persistence coverage for the extreme-defense setting.
- Pack revision: `_v1.3.4_P`.

## 1.3.3 — 2026-11-05 — first tagged standalone release

**History:** tag `v1.3.3`

- Established the standalone seed generator in this repository.
- Four tabs: Randomizer, Balance, Quality of Life, Language.
- One-time game-data extraction, seed library, profiles, spoiler codex, and
  `retoc verify` before install.
- Packs named `ExpeditionInfinity_<seed>_v1.3.3_P`.

## Before 1.3.3

No tags, update files, or complete changelog survive for earlier releases. The
oldest release still referenced by the repository is **1.2.1** (2026-10-22;
seeds `7777777`, `333333`, `666666`), recorded in
`Planning/Bug Report 1.2.md` and the archived test packs. It predates the
1.3.x standalone generator chain and is not reconstructable as a full update
list.

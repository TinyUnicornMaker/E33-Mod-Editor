# Expedition Infinity 1.4.4 — corrected spoiler codex variants

Released 2026-11-23. **Standalone generator and seed-pack update** for
Windows x64 and Linux x64. Seed packs use `_v1.4.4_P`.

## What changed

- **Venue-clone labels in the spoiler codex.** Enemies cloned for a specific
  fight now show their real name and venue instead of the donor row ID, for
  example `Frost Eveque [Venue L50]` with area
  `World Map — WM_EvequeIce_Alpha`.
- **Venue clone areas now match the encounter.** Codex cards use the borrowing
  encounter’s area (Endless Tower floor or world-map fight) rather than the
  donor’s original geography.
- **Item-source rows no longer collapse across areas/levels.** The same enemy
  name in two venues is kept as two distinct sources, so an item cannot appear
  to come from the wrong variant.
- **No gameplay, loot-table or pak-content changes.** The 1.4.3 damage caps,
  Free Aim penalty, target-glow toggle and weapon-passive fix are unchanged.

## Download

| Platform | File |
|---|---|
| Windows x64 | `ExpeditionInfinity-1.4.4.exe` |
| Linux x64 | `ExpeditionInfinity-1.4.4` |
| Seed packs | `ExpeditionInfinity_1.4.4_seed-packs.zip` |
| Hashes | `ExpeditionInfinity_1.4.4_SHA256SUMS` |

The Linux file is a plain self-contained ELF executable; after downloading,
run `chmod +x ExpeditionInfinity-1.4.4` before launching it.

## Verification

- Codex variant labels and areas regenerated for Normal seed `87694963`.
- All 522 mod enemy drop rows still match the staged `DT_jRPG_Enemies` table.
- Generator tests, app builds, and seed-pack `retoc verify`.

# Expedition Infinity 1.4.3 — editable damage caps and QoL toggles

Released 2026-11-23. **Standalone generator release** for Windows x64 and Linux x64.
Seed packs use `_v1.4.3_P`.

## What changed

- **Editable damage caps.** The seed generator’s Balance tab now exposes a
  Normal and Ultima cap for every damage source. Set a row to `0` for no cap.
  Defaults match the 1.4.2 table except normal Lumina, which is deliberately
  lowered to 3,333 (Ultima Lumina remains 3,333,333).
- **Free Aim repeat penalty toggle.** On by default. Each regular Free Aim shot
  in the same turn multiplies damage by `0.97`, through shot 100. Skills,
  counters, Burn and Lumina are untouched.
- **Target glow QoL toggle.** Off by default. When enabled, the selected ally
  receives the coloured selected-ally hover glow.
- **Weapon passive randomization fix.** Turning weapon passives off now keeps
  the entire weapon vanilla, including stats and element. The stats toggle is
  inactive in that state.
- **Spoiler codex.** Mod-added pictos now use the staged name, description and
  Lumina-cost tables, including case-insensitive string-table lookups.

## Download

| Platform | File |
|---|---|
| Windows x64 | `ExpeditionInfinity-1.4.3.exe` |
| Linux x64 | `ExpeditionInfinity-1.4.3` |
| Seed packs | `ExpeditionInfinity_1.4.3_seed-packs.zip` |
| Hashes | `ExpeditionInfinity_1.4.3_SHA256SUMS` |

Both are self-contained single-file builds. The generator’s existing bundled
tools are included; no separate .NET installation is required.

## Verification

- Generator preset/option tests.
- Damage-cap smoke, including custom values and unlimited (`0`) caps.
- Free Aim smoke.
- Target-glow smoke.
- Full build + `retoc verify` for the release seed packs.

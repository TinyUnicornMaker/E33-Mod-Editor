# Expedition Infinity — changes from 1.3.5 to 1.3.10

## Read this first

- **No backwards compatibility for packs.** New packs are named
  `ExpeditionInfinity_<seed>_v1.3.10_P`. Old packs are not migrated or updated.
  Old `.eiseed.json` profiles still load.
- **This release ships three seeds**, not one: Easy, Normal and Hard.
- **Language packs are shared** under `Languages/<culture>/`, not per seed.
- The generator/application binaries are distributed **separately** from this
  mod release folder (antivirus false positives); this folder contains only the
  seed packs, language packs and documentation.
- Old 1.3.5 saves continue to load; the save format did not change.

## 1.3.10 — this release

### Three seeds

| Mode | Seed | Folder |
|---|---:|---|
| Easy | 61281975 | `[Easy] 61281975` |
| Normal | 87694963 | `[Normal] 87694963` |
| Hard | 23035873 | `[Hard] 23035873` |

Common to all three:

- Battle proc cards hidden (`LimitBattleProcCards` on, cap **0**).
- Close/first-person camera **off** — no camera configs, climbing, world-map
  or first-person assets in the containers.
- Tutorials off; Party of Five core and always on.

Mode-specific:

- **Easy** — regular parry/dodge timings (0.15 / 0.22; the defense-window table
  is not staged), 25% weaker regular-enemy bonuses, instant Picto mastery.
- **Normal** — standard Expedition Infinity.
- **Hard** — the Brutal preset plus extreme T4–T5 boss defense (1× at the T4
  floor to 30× at L99): tightened defense windows (0.1125 / 0.165), 2× HP
  bonus, 1.5× speed/attack bonuses, half Lumina. Permadeath stays off.
- **Boss defense (Hard only)** — the Hard seed enables
  `ExtremeBossDefense`: 1× at the T4 floor, smoothly ramping to **30× the
  regular defense curve at L99**. Easy and Normal keep the regular curve, so
  the codex now shows the Hard endgame bosses at roughly 30× the Easy/Normal
  defense at the same level.

### Languages

- **Simplified Chinese (`zh-Hans`)** is included as a sixth language. The first
  launch language dialog, UI strings and the pack catalog are translated; the
  `Languages/zh-Hans/` pak is built from the same catalog as the other cultures.
- Seed containers are language-neutral English (no locres inside the `.utoc`).
- One pak per culture is built under `Languages/`:
  `fr-FR`, `pl`, `ru`, `es`, `zh-Hans`.
- The generator installs the selected culture's pak alongside the seed; manual
  install instructions are in `INSTALL.txt`.
- The `zz_` prefix keeps the language pak mounting after `pakchunk0`.

### Setup fixes

- Choosing a new game folder now updates the install folder to that game's
  `Sandfall/Content/Paks/~mods` (a custom external install folder is preserved).
- The Game data folder uses the same text-box field as the other setup rows.

### Release layout

```
Expedition Infinity 1.3.10/
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

## 1.3.9

- Donor extraction restored to the 1.3.5 behavior: the original
  `retoc to-legacy` invocation with script objects enabled.
- Removed the strict per-filter `.uasset` abort and final missing-filter abort
  introduced in 1.3.8, so extraction follows the path confirmed working on
  Windows.
- Kept the Oodle working-directory / `PATH` hardening.
- Kept Spanish, picto scaling, signed percentage fields, versioned executables
  and the cleaned scope.

## 1.3.8

- Windows donor-extraction fix: `retoc` runs from its bundled tool folder with
  Oodle on `PATH` / `LD_LIBRARY_PATH`.
- Donor extraction briefly used `--no-script-objects` and verified every filter
  with a hard abort (both reverted in 1.3.9).
- Versioned executables only; Spanish; linear picto scaling; UI fixes.
- Fresh extraction tested against the Steam containers: 2,093 `.uasset` files,
  zero `scriptobjects.bin`.

## 1.3.7

- **Spanish (Spain)** added: English, Français, Polski, Русский, Español.
  Spanish catalog: 1,244 table + 10 inline entries; validation passes.
- Every distributed executable carries its version in the filename
  (`ExpeditionInfinity-1.3.7`, `ExpeditionInfinity-1.3.7.exe`).
- **Picto over-level scaling**: `DT_EquipmentScalingTable_Pictos` extends
  levels 34–333 at a linear +1% of the level-33 base per level (level 333 =
  4×). Applies to HP, Defense, Speed, Crit and Chroma; rows 0–33 untouched.
  Picto drop levels remain 1–33.
- Percentage fields show signed values with `%` inside the box.
- Removed the “Five-member party: always on.” note from Boss movesets.

## 1.3.6

- **Cleaned standalone scope**: Permadeath, Challenges (enemy Max Health /
  Defense / Speed options) and the Burn stack cap are excluded from the
  standalone pack path. Party of Five is core and always on; battle groups and
  per-battle random battles stay off.
- **Game data folder** is selectable in Setup, with **Change…** and
  **Use default**; changing it moves the existing extraction cache.
- **Limit battle proc cards** toggle: with it on, `0` hides every proc card;
  off restores the vanilla unlimited stack. Old profiles using `0` for vanilla
  are migrated.
- **Nevrons** moved from Randomizer to Balance; disabling Nevron stats also
  clears the regular Nevron percentage boosts; Elite Nevron tooltip clarified.
- Percentage fields show an explicit sign and keep `%` in the box.
- Tabs remain switchable while a build is running.
- Picto over-level scaling was prepared in 1.3.6 and shipped in 1.3.7.

## Cumulative summary: 1.3.5 → 1.3.10

| Area | Change |
|---|---|
| Seeds | One seed → three curated seeds (Easy / Normal / Hard) |
| Language | Four languages → six (Spanish, Simplified Chinese); shared `Languages/<culture>/` packs |
| Pictos | Core over-level scaling 34–333 (linear, 4× at 333) |
| Proc cards | Toggle added; `0` hides all; 1.3.10 seeds ship cap 0 |
| Camera | Close/first-person camera disabled in all 1.3.10 seeds |
| Setup | Game data folder selectable/movable; install folder follows game folder |
| UI | Signed percentages, Nevron section moved, boss note removed, tabs during build |
| Executables | Versioned filenames only |
| Windows | Donor extraction Oodle/PATH hardening; 1.3.9 restored the 1.3.5 extraction path |
| Scope | Permadeath/Challenges/Burn cap excluded; Party of Five core; random battles off |
| Packaging | Packs use `_v1.3.10_P`; no old-pack migration; app binaries distributed separately |

## Known limitation

The shared language packs translate all string-table text. Seven
culture-invariant/inline strings remain English because they live inside assets
rather than the string tables: Super Spark name/description, Super Sonic,
Rupture Shot and Uno Puncho names, and the Gradient Overcharge tooltip
template. Translating those requires per-language asset overrides.

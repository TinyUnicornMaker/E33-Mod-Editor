# Windows 1.3.5 pack: missing string-table text — pipeline analysis

Status: investigation, 2026-11-17. Scope: 1.3.5 Windows build, English pack,
"<MISSING STRING TABLE ENTRY>" / `Lorem Ipsum` on modded skills and weapon
passives while stat/row edits appear to apply.

## TL;DR

**The supplied pack is not broken.** It is a complete, canonical English 1.3.5
pack:

* `retoc verify` → `verified`; container flags, TOC version and mount point are
  identical to a known-good pack.
* `ST_MainCharacters_Skills` is **byte-identical** to the developer's own 1.3.5
  pack; `ST_Items`, `ST_PassiveEffects`, `ST_BattleTooltips`, `ST_UniqueMechanics`
  and `ST_UI_InputActions` are byte-identical too. `ST_UI_GameMenu` differs only
  by the Challenge rows the clean 1.3.5 build does not ship.
* All 3,410 string-table keys are present in the pack, all resources sit at the
  canonical `../../../Sandfall/Content/StringTables/...` mount paths, and every
  key-like string used by the pack's own assets matches the known-good build.
* `DT_PassiveEffects` and `WBP_WeaponTooltip` are byte-identical to the
  developer's pack.
* The only asset differences against the developer's 1.3.5 pack are optional
  features the user had switched off (First Person camera, Challenges,
  world-map/climbing, Bulk Upgrade hint) plus one randomizer line.

So the text is missing because **the game is not loading this container (or is
loading another container that overrides the same `ST_*` packages)** — not
because of anything in these files. The highest-value next steps are therefore
on the install side (see "What to check on the user's machine"), and the
decisive cheap experiment is to rename the trio with a `zz_` prefix so it mounts
after the base containers.

The rest of this document records the pipeline analysis and the latent
failure modes that a future pack could hit; several of them
(`retoc verify` not checking coverage, silent staging fallbacks, stale donor
caches) are worth fixing regardless, but they are **not** the cause of this
report.

---

## Update 2026-11-18 — inspection of the supplied pack

Files: `work/reports/2026-11-18-broken-1.3.5/pack/`
(`ExpeditionInfinity_53471695_v1.3.5_P.{pak,utoc,ucas}`, built 2026-11-17 17:50).

| check | result |
|---|---|
| `retoc verify` | `verified` |
| `retoc info` | `flags=Indexed`, `version=OnDemandMetaData`, `mount_point=../../../`, 942 chunks / 941 packages |
| `.pak` stub | byte-identical to a good pack (347 B) |
| ST mount paths | all 7 canonical, no duplicates |
| ST keys | 660 / 843 / 1433 / 38 / 66 / 176 / 194 — complete |
| `ST_MainCharacters_Skills` | md5 `7fcde44061a6f90446edb4845c495bad` — identical to the dev's 1.3.5 pack |
| `ST_PassiveEffects` | 115656 B, identical to the dev's 1.3.5 pack |
| `ST_Items` | identical |
| `ST_UI_GameMenu` | differs only by the four Challenge strings the clean build omits |
| `DT_PassiveEffects` | byte-identical to the dev's 1.3.5 pack |
| `WBP_WeaponTooltip` | byte-identical |
| total packages | 941 (dev pack 958; the 17 extra are Challenges, camera/First Person, world-map/climbing, Bulk Upgrade hint) |

Because the skill/passive tables are byte-for-byte the developer's known-good
1.3.5 output, a correctly mounted copy of this pack cannot produce
`<MISSING STRING TABLE ENTRY>` for modded skills. The failure has to be at
mount/install time.

### What to check on the user's machine

1. `~mods` listing: the three files must sit **directly** in
   `...\Sandfall\Content\Paks\~mods\`, not in a `current seed\` subfolder
   (the supplied zip preserves a folder called `current seed`).
2. Only one `ExpeditionInfinity_*` trio in `~mods`. `InstallPackAtomic`
   removes old ones, but a hand copy can leave two; the game then loads
   whichever it enumerated first.
3. Other mods that ship `ST_MainCharacters_Skills` / `ST_PassiveEffects`
   (translation packs, other overhauls). Move them out and retest.
4. The game was fully quit before installing and relaunched afterwards.
5. Rename the trio to `zz_ExpeditionInfinity_53471695_v1.3.5_P.{pak,utoc,ucas}`
   as a test. The build already ships the locres pak as `zz_` specifically so it
   mounts after `pakchunk0`; if the main trio is being mounted *before* the base
   containers on this machine, the rename will fix the override and prove it.
   (`Retoc.InstallPackAtomic` and `CleanInstalledSeedPacks` already tolerate the
   `zz_` name, so the app could adopt it permanently.)
6. Game language vs pack language: this is an English pack (no `zz_` locres
   pak). That should still show English source for new keys, never a missing
   entry, but confirm the user is not testing a French/Polish/Russian pack
   without its `zz_...pak`.

If (1) or (3) turns out to be true, the fix is delivery/install, not code. If
(5) is needed, the pipeline should prefix every shipped trio with `zz_`.

---

## Pipeline analysis (context)

---

## What the pipeline does (string-table path)

```
work/tables/<donor>              extracted by retoc to-legacy (per donor filter)
   └─ Sandfall/Content/StringTables/.../ST_*.uasset + .uexp
        │  FileCopy.FindUnder / FindTablesAsset   (first match wins!)
        ▼
work/mod (staging)
   ├─ character kits:       LuneSkillsKit / MaelleSkillsKit / ...   (ST_MainCharacters_Skills)
   ├─ picto/weapon kits:    PictosCreateKit / WeaponEffectsCreateKit (ST_PassiveEffects, ST_Items)
   ├─ LocalizationKit:      only for fr/pl/ru/es (locres + inlined table source)
   ▼
DataTableEditor.AddStringTableEntries(path, dict)   (UAssetAPI; writes .uasset + .uexp)
   ▼
Retoc.PackCombined → retoc to-zen work/mod → <name>.utoc/.ucas
   ▼
retoc verify → InstallPackAtomic → ~mods
```

Reference counts in a good English 1.3.5/1.3.6-era stage (game 1.5.x):

| table | vanilla entries | staged entries |
|---|---:|---:|
| ST_MainCharacters_Skills | 499 | **660** |
| ST_PassiveEffects | 784 | **1433** |
| ST_Items | 843 | 843 (values only) |
| ST_UI_GameMenu | 176 | 176 (values only) |
| ST_UI_InputActions | 194 | 194 |
| ST_BattleTooltips | 66 | 66 |
| ST_UniqueMechanics | 38 | 38 |

A user pack that shows missing text will almost certainly have **499** and
**784** for the first two. Those numbers are a quick fingerprint of a vanilla
table slipping through.

---

## Evidence collected

### Cross-OS reproduction

Same donor cache and settings, two builds of the same source:

* Linux: `E33ModEditor.Cli rebuild-mod --language en --revision _v1.3.5`
* Windows: the same CLI published `-r win-x64 --self-contained` and run under
  Wine 9.

Results:

* Both staged **1904 files with identical paths**.
* File hashes differed only in the 24 randomizer-written `DT_Merchant_*`
  assets (unordered dictionary enumeration affects the roll, not text).
* All seven staged string tables were **byte-identical**.
* `retoc to-zen` produced **953 chunks in both**; every `ST_*` chunk was
  byte-identical.
* A scanner over every staged asset (DataTable rows, DataAsset properties,
  blueprint bytecode text literals) found **0 unresolved references** to the
  mod-owned tables. (It found 4 pre-existing dangling vanilla keys, see
  "Minor findings".)

Conclusion: the text pipeline itself does not lose keys on Windows.

### retoc packs by **file path**, not by package header

This is the single most important property for this class of bug. A copy of the
modified `ST_MainCharacters_Skills` placed at `work/mod/WRONGPATH/` is packed as:

```
../../../WRONGPATH/ST_MainCharacters_Skills.uasset
```

The chunk id is still the asset's, but the **directory index path is wrong**,
so the game never mounts it. It is not an error and `retoc verify` passes.

### Duplicate-package behaviour

If two files carry the same package header (e.g. a modified table at
`work/mod/ST_PassiveEffects.uasset` and a vanilla one at the canonical
`work/mod/Sandfall/Content/StringTables/Gameplay/ST_PassiveEffects.uasset`),
`to-zen` keeps **one** chunk and **one** path, chosen by directory enumeration
order. In one direction it kept the canonical path and the modified content; in
the other it kept the **root path** and therefore shipped the modified table to
an unmountable location while the vanilla canonical copy was dropped. On NTFS
the enumeration order differs from ext4, so this is exactly the kind of thing
that can behave differently on Windows with no error.

---

## Why the symptom maps to "table not mounted", not "keys missing"

* Weapon **names** still resolve (`LUNERIM`): they live in `ST_Items`, which
  only gets *value* edits and is staged by helpers that have a **canonical-path
  fallback** when they cannot slice the donor path
  (`WeaponOverLevelKit.CopyIntoMod`, `WeaponOverlevelingKit.CopyVanilla` both
  fall back to `Sandfall/Content/<stem>.uasset`).
* Weapon **passive descriptions** (`Lorem Ipsum` placeholder) and the tooltip
  in image 3 are `PASSIVE_*` keys from `ST_PassiveEffects`, the table with
  649 **new** keys.
* Skill names/descriptions are `ST_MainCharacters_Skills`, the table with
  161 **new** keys.

If the game were using the mod's table, all of these would resolve (verified
offline). If the game were using the vanilla table, exactly the new keys would
fail and the edited values would be unnoticed. That is the observed picture —
and since the supplied pack provably contains the modified tables at the
canonical paths, the game must be resolving **vanilla** copies from another
container or not mounting this one.

---

## Ranked causes for the observed runtime failure

### 1. The pack is not mounted at all (most likely)

The zip preserves a folder named `current seed`. If the files were dropped into
`~mods\current seed\` rather than `~mods\`, the game may never see them. What
the user then sees is an earlier pack still installed (or vanilla plus a few
leftovers), which looks exactly like "some modded things exist, their text does
not". Check the `~mods` listing first; ask for a screenshot of the folder.

Also check that the game was **fully quit** when the files were copied and
relaunched afterwards; IoStore caches the mounted container set per launch.

### 2. Another container overrides the same `ST_*` packages

A translation or overhaul mod that ships `ST_MainCharacters_Skills` /
`ST_PassiveEffects` and mounts after ours replaces the whole table. New keys
vanish while translated vanilla text stays. Ask the user to move every other
mod out of `~mods` and retest.

### 3. Mount order: the base containers win the override

This is the only mechanism that is both Windows-specific and consistent with
"new assets load, overridden assets do not". The build ships the locres pak as
`zz_<pack>.pak` specifically so it mounts after `pakchunk0`; the main trio has
no such prefix. If the game enumerates containers alphabetically (NTFS does;
ext4 does not), `ExpeditionInfinity...` sorts before `global.utoc` and
`pakchunk*`, and the base package wins for every overridden asset — including
the string tables — while mod-only assets (new skills) still load.

Cheap decisive test: rename the trio to
`zz_ExpeditionInfinity_53471695_v1.3.5_P.{pak,utoc,ucas}` and relaunch. The
install code already tolerates `zz_` names, so this can be made permanent if it
works.

### 4. Partial install / stale duplicate trio

`InstallPackAtomic` is transactional, but a hand copy can leave two trios; the
game loads one of them. Hash the installed files against the seed library and
remove every other `ExpeditionInfinity_*` / `zz_ExpeditionInfinity_*` set.

### 5. Game language vs pack language

This is an English pack (no `zz_` locres pak). Non-English play should still
fall back to the English source for new keys, not produce a missing entry, but
confirm the user is not running a French/Polish/Russian pack that lost its
`zz_...pak`.

---

## Latent pipeline risks (none of them caused this pack)

### A. A string table can be staged to a non-canonical path

Code paths that can silently stage an ST asset to the wrong place:


* `PictosCreateKit.CopyCreateTables` (line 130) does
  `if (idx >= 0) FileCopy.AssetPair(...)` where `idx` is
  `st.IndexOf("Sandfall", StringComparison.Ordinal)` — **silent skip** if the
  donor path does not contain the exact-case substring.
* `PictosCreateKit.InjectStrings` (line 935) then falls back to
  `Path.Combine(modDir, Path.GetFileName(src))`, i.e. **`work/mod/ST_PassiveEffects.uasset`
  at the staging root**. retoc packs that as `../../../ST_PassiveEffects.uasset`;
  the game ignores it. This is the **only** ST fallback in the tree that is not
  mountable — every other table either re-prepends `Sandfall/Content` or throws.
* From that point on, every other kit finds the ST with
  `FindAsset(modDir, "ST_PassiveEffects.uasset")` — a `Directory.EnumerateFiles`
  "first match", which on Windows returns the root-level file first — so all
  subsequent new keys are written to the wrong copy.
* `WeaponEffectsCreateKit.CopyTables` (line 118) also uses
  `st.IndexOf("Sandfall", Ordinal)` with `if (idx >= 0)` and no failure, so it
  has the same silent-skip behaviour.

`"Sandfall"` is matched **case-sensitively** in 71 places in the codebase. Any
donor path whose game-side directory casing is not exactly `Sandfall` (lowercase
cook, a repacked game build, a reshaped cache) turns those into silent skips or
wrong destinations. The character kits are safer: `StageDest` uses the
case-insensitive `/Content/` marker and re-prepends `Sandfall/Content`, so they
still write the right path — which is why a partial failure is possible.

### B. Donor cache is stale / contaminated

`DonorExtract.IsPresent` (line 204) only checks that `<filter>.uasset` exists
(and that a *zero-length* `.uexp` is absent). It never checks freshness:

* The app records `gameFingerprint` (hash of `Paks/*.utoc` names/sizes/mtimes)
  in each manifest (`MainWindow.GameFingerprint`), but **never uses it** to
  invalidate `work/tables`.
* A cache extracted before a game patch, or extracted while another mod was
  shadowing the tables (mods are only suspended when they sit as top-level
  `.pak`/`.utoc` in `~mods`), is reused as-is.
* Prefix filters (`BP_LU_`, `BP_Battle_SkillScript_`, …) are considered present
  from a `.donor/*.ok` marker written by an **older, smaller filter list**
  (I reproduced this: a marker existed while dozens of `BP_Battle_SkillScript_*`
  assets were absent).

A stale `ST_PassiveEffects`/`ST_MainCharacters_Skills` plus fresh row edits gives
a pack where only new keys are missing. The supplied pack is not affected (its
tables match the current game's donor data bit-for-bit), but the next user with
a partial cache could be.

### C. Real-Windows-only filesystem edges (not reproduced under Wine)

These cannot be ruled out from Linux/Wine and are worth asking about:

* **MAX_PATH** — the deepest staged path is 130 chars below `work/mod`; add the
  data root and the donor path below `work/tables/<filter>` and a long
  `C:\Users\…\OneDrive\…` root can cross 260. .NET then throws (build fails),
  but `retoc` (Rust) and shell helpers can behave inconsistently.
* **Protected/non-writable app folder** — data root is the exe folder
  (`AppSettings.DataRoot` uses `Environment.ProcessPath`). Running from
  `C:\Program Files`, a read-only share, or a folder under Defender Controlled
  Folder Access can fail mid-write.
* **Case-insensitive collapsing** — if any kit ever writes the same asset under
  two different-cased directory literals, Linux keeps two files (the correctly
  cased one is loaded) while NTFS keeps one, with the first-created casing
  winning for all later writes. That can turn an asset that works on Linux into
  a wrong-cased, never-mounted asset on Windows. The current stage has no such
  collisions, but the codebase has many hand-written path literals.
* **Junction-based extraction folder** — `PrepareExtractionFolder` creates an
  NTFS junction; `Directory.ResolveLinkTarget`/enumeration behave differently
  on real junctions than on Wine symlinks.

---

## Recommended build-time guards

These are defence-in-depth. None of them would have changed the supplied pack
(it is already correct); they close the silent-failure classes that could hit
the next build.

1. **Post-`to-zen` path assertion.** After `Retoc.PackToZen`, run
   `retoc list --path <utoc>` and require exactly:

   ```
   ../../../Sandfall/Content/StringTables/ST_MainCharacters_Skills.uasset
   ../../../Sandfall/Content/StringTables/Gameplay/ST_PassiveEffects.uasset
   ../../../Sandfall/Content/StringTables/Gameplay/ST_Items.uasset
   ../../../Sandfall/Content/StringTables/Gameplay/ST_UniqueMechanics.uasset
   ../../../Sandfall/Content/StringTables/Gameplay/ST_BattleTooltips.uasset
   ../../../Sandfall/Content/StringTables/UI/ST_UI_GameMenu.uasset
   ../../../Sandfall/Content/StringTables/UI/ST_UI_InputActions.uasset
   ```

   Abort the build if any is missing, duplicated, or under a different prefix.
   This is cheap and closes the whole class of silent path bugs.

2. **Content assertion on the staged tables.** Before packing, require
   `ST_MainCharacters_Skills.Count > 499` and
   `ST_PassiveEffects.Count > 784` (or, better, "vanilla count + number of keys
   the kits wrote"). A vanilla table reaching this point aborts the build.

3. **Fail-closed staging.** Replace every
   `var idx = src.IndexOf("Sandfall", Ordinal); if (idx >= 0) copy;` with a
   helper that throws (or derives the path from the `/Content/` marker with
   `OrdinalIgnoreCase` and re-prepends `Sandfall/Content`), exactly like
   `LuneSkillsKit.StageDest`. Remove the
   `Path.Combine(modDir, Path.GetFileName(src))` fallback in
   `PictosCreateKit.InjectStrings`: a root-level asset is never mountable.

4. **Donor cache fingerprinting.** Persist the `gameFingerprint` next to
   `work/tables/.donor` and re-extract when it changes. Also store the number
   of assets produced per prefix filter and re-run the filter when the marker
   is older than the filter list.

5. **Duplicate-package detection.** Walk the staging tree before `to-zen`,
   group by the package header name (`FolderName` + object name), and abort if
   two files claim the same package at different paths. This is the case that
   made retoc pick the wrong path in testing.

6. **Install diagnostics.** Record the absolute `~mods` path, a hash of each
   installed file, and the `~mods` directory listing in the build log/manifest.
   If a user reports missing content, that answers "is it the pack or the
   install" immediately.

7. **Ship the reference scanner.** `tools/stverify` (added with this report)
   loads a staged tree and reports any `StringTableEntry` FText whose key is not
   in the corresponding staged table. It is the fastest way to prove a pack
   coherent without launching the game.

---

## What to ask the Windows user for

The pack is already verified, so the remaining questions are about install:

1. A screenshot/listing of `...\Sandfall\Content\Paks\~mods` — are the three
   files **directly** in `~mods` (not `~mods\current seed\`) and is any other
   pack present?
2. `logs\build-*.log` from the app folder (it records the install path).
3. Whether the pack was installed by the app or copied by hand, and whether the
   game was running during install.
4. The result of temporarily renaming the trio to `zz_...` (mount-order test)
   and/or removing every other mod.
5. The game language, and whether any `zz_...pak` locres file is present.


---

## Minor findings (not the cause)

* `DT_PassiveEffects` references four keys that exist in neither the vanilla nor
  the staged `ST_PassiveEffects`:
  `PASSIVE_Weapon_ATKSPDUpNoAP_Description`,
  `PASSIVE_Weapon_ATKLifestealLow_Description`,
  `PASSIVE_Weapon_ATKLifestealMedium_Description`,
  `PASSIVE_Weapon_ATKLifestealHigh_Description`.
  They are dangling in vanilla too (unused rows or another table); harmless but
  the scanner will keep flagging them.
* `retoc verify` reporting "verified" says nothing about asset coverage. It
  should not be treated as proof that the pack contains what the build staged.
* `ModLocalization` / `LocalizationKit.AssertCatalogCoversStage` are only
  exercised for non-English builds; an English pack can lose table content and
  still build cleanly.

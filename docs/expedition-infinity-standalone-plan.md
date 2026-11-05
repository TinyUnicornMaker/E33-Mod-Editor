# Expedition Infinity — Standalone Seed Generator Plan

**Status:** implemented baseline for 1.3; updated 2026-10-31  
**Target app name:** Expedition Infinity  
**Scope:** a lightweight, single-application seed generator for the Expedition
Infinity mod. Four tabs: **Randomizer**, **Balance**, **Quality of Life**,
**Language**.

---

## 1. Goal

Build one distributable application that:

- Finds the Expedition 33 install (or lets the user pick it) and resolves /
  creates the game's `Paks/~mods` folder.
- Generates a complete Expedition Infinity seed pack from a seed + a small set
  of user choices.
- Keeps the **core mod fixed** (movesets, tier randomization, picto/weapon
  content, boss defenses, story scaling, etc.).
- Exposes only the seed, balance and QoL knobs that make sense outside the full
  editor.
- Writes `ExpeditionInfinity_<seed>_v<version>_P.{pak,ucas,utoc}` into `~mods`
  (or a user-selected folder), plus `zz_ExpeditionInfinity_<seed>_v<version>_P.pak`
  for non-English localization, a spoiler codex and a build manifest.
  Version 1.3 therefore produces `ExpeditionInfinity_<seed>_v1.3_P`.
- Ships as one self-contained executable per platform. No .NET install, no
  Python, no repo checkout, no separate tool folder.

**Non-goals**

- The full E33 Mod Editor (monster grids, per-move editing, skill graphs,
  weapon-effect editor, tier editor, save editor, camera tuning).
- Editing the game's save files.
- Bundling the game's extracted assets in the download.
- Multiple simultaneously installed seed packs (the game cannot load two
  overrides of the same assets).

---

## 2. What already exists

### 2.1 The old standalone build

`src/E33Packer.App` is the partially-started standalone app (Sep 2026):

- Avalonia window titled "Expedition 33 Packer".
- Auto-finds the game via `GameLocator`; folder picker fallback.
- Seed box + random seed.
- One **Build mod pack** button.
- First-run extraction overlay with progress (`DonorExtract`).
- Uses `FullPackKit.RebuildAndPack`.
- Single-file self-contained CI for Linux x64 and Windows x64
  (`.github/workflows/packer.yml`).
- Persists only `{ gameFolder, seed }` under
  `LocalAppData/E33Packer/settings.json`.

It is a good shell, but it is not the requested product.

### 2.2 The pack pipeline is already complete

`FullPackKit.RebuildAndPack` builds the entire overhaul: skills, pictos,
weapon effects, curves, over-levelling, passives, combat balance, randomizer,
boss/story/endgame fixes, localization, spoiler codex and the IoStore pack.

The relevant setting objects already exist:

| Area | Type | Notes |
|---|---|---|
| Seed / loot / enemy rolls | `Loot.RandomizerKit.Settings` | 18 public settings |
| Balance / combat / progression | `GameplayTweaksKit.Settings` | ~30 settings + 5 arrays |
| Skill overhaul + stain wildcard | `SkillOverhaulKit.Settings` | includes `LightStainWildcard` |
| Weapon bulk upgrade | `WeaponBulkUpgradeKit.Settings` | enabled, levels, tap +1 |
| Weapon drop levels | `WeaponDropLevelKit.Settings` | enabled, max drop level |
| Party of Five | `PartyFiveKit.Settings` | enabled + formation |
| First-person camera | `FirstPersonCameraKit.Settings` | out of scope for seed generator |
| Language | `ModLocalization` / `LocalizationKit` | en, fr, pl, ru |

### 2.3 What the old standalone actually builds

The old app calls `FullPackKit` with **code defaults**, not the current
Expedition Infinity preset in `work/gameplay_tweaks.json`. It also omits Party
of Five (the current `work/party_five.json` has it enabled) and the camera (off
anyway). This is the biggest correctness gap.

Current differences (old standalone vs the current shipped preset):

| Setting | Code default (old standalone) | Current `work/` preset |
|---|---:|---:|
| Character level cap | 250 | 500 |
| Max AP | 9 | 21 |
| Max consecutive turns | 2 | 3 |
| Permadeath | false | false (feature not completed; always off) |
| Max Foretell | 20 | 30 |
| Crit damage addend | 0.5 (×1.5) | 1.5 (×2.5) |
| Attribute cap | 99 | 500 |
| Max shields | 9 | 5 |
| Burn multiplier | 0.66 | 0.075 |
| Burn tail-off | 2.0 | 3.0 |
| Tighten dodge/parry | false | true (0.165 / 0.1125) |
| Tutorials off | true | true |
| Party of Five | off | on |

`CritRateDamageScaling` is **true in both** the code defaults and the current
preset. The old standalone therefore does apply the crit-rate → crit-damage
patch; it just uses the vanilla addend 0.5 (base ×1.5) instead of the current
1.5 (base ×2.5).

So a pack from the old standalone is **not** the current mod. This must be
fixed before adding UI.

**Verified against the shipped v7 pack (2026-10-26).** We extracted
`AC_jRPG_CharactersManager` from
`Export/ExpeditionInfinity_7777777_v7_P` and inspected its CDO:

```
MaxCharactersAmountInParty = 5
CharacterMaxLevel           = 500
```

We also extracted `BP_BattleDamages` from the same pack and ran
`CritDamageKit.Verify(path, addend: 1.5, critRateScaling: true)`, which returned
`true`. The v7 pack therefore **does contain the critical-rate damage patch**:

```
critMultiplier = (1 + 1.5) × max(1, critRate / 100) × ExtraCriticalHitDamageMultiplier
               = 2.5 × max(1, critRate / 100) × Extra
```

So the current v7 pack is **500**, not 333, it **does** include the five-member
party, and the crit-rate scaling is present. The user decision to use 333 is
therefore a *design change* from what v7 actually shipped, not just a docs
correction. See §13.

### 2.4 1.3 standalone pack test (completed 2026-10-27)

The finished Linux single-file app was run end-to-end with the default Expedition
Infinity profile. Test artifacts are preserved in `Export/TestBuilds/`.

| Test | Seed | Result |
|---|---:|---|
| English | 123456 | `ExpeditionInfinity_123456_v1.3_P` built; 13 MB `.ucas`, 347-byte `.pak` stub, 3.4 MB spoiler |
| French | 654321 | `ExpeditionInfinity_654321_v1.3_P` built; 2.1 MB `.pak` containing `fr-FR/Game.locres`; 1,244 entries merged |

Packed-asset assertions from the English build:

- `AC_jRPG_CharactersManager.CharacterMaxLevel = 333`
- `MaxCharactersAmountInParty = 5`
- `CritDamageKit.Verify(1.5, true) = true`
- `DT_CharactersLevelScaling.Level_333` present; `Level_500` absent
- `WBP_Raw_Attributes_Display` staged
- `Check_CanResurrect = CanResurrect` (Permadeath off)
- `BP_DataAsset_Item` staged (Instant Picto Mastery on pickup)
- `WBP_PassivePopupContainer` staged and `PictoPopupCapKit.Verify(path, 5) = true`
- Manifest profile: `Permadeath = false`, `QoL.PictoMastery = "instant"`,
  `QoL.PictoPopupCap = 5`, `QoL.LightStainWildcard = false`

The English test artifact was rebuilt with these corrected settings on
2026-10-28. The French artifact remains the earlier localization test; it was
built before the Permadeath/Picto correction and is kept only to prove the
locres path.

Two standalone-only issues were found and fixed during the test:

1. The donor filter list was incomplete for a fresh cache. Broad families were
   added for skill scripts, skill data components, battle buffs, `BP_LU_`,
   `DA_Skill_`, `DA_GearPictos_`, enemy archetype tables, string tables and the
   weapon-upgrade widgets.
2. Prefix filters used to treat “≥5 matching files” as complete, which skipped
   broad extraction when specific filters had already produced a few files.
   Prefix filters now write a `.donor/<filter>.ok` marker after a full
   extraction and only count as present when that marker exists.

The app also creates compatibility links for the full editor's legacy donor
lookup folders (`work/probe-*`, `work/extract_maelle`, `extracted`) so kits that
were written against the editor scratch layout find the same `work/tables`
assets in a standalone data root.

**Config audit for 333.** We searched every persisted setting on this machine:
`work/gameplay_tweaks.json`, both live `Export/*.project.json` sidecars, every
`work/deploy-backup/*/Export/*.project.json`, `work/archive/v1.2-pre-release-
current-v7/gameplay_tweaks.json`, and `work/safe-points/*`. None contain
`LevelCap = 333` or `AttributeCap = 333`; the current and archived v7 values
are **500 / 500** (the oldest safe point is 250 / 200). The only 333 references
are the guide and the weapon-level docs (weapon cap is 333). If the main
editor's UI has been showing 333, its saved file was overwritten at some point;
the current on-disk state is 500. Treat 333 as the intended design and fix the
config, but do not assume the v7 pack already has it.

---

## 3. Product decisions (locked 2026-10-26)

These are the decisions the rest of the plan uses. The 333/500 question is
resolved in §13: the generator uses 333; v7 shipped 500.

1. **The app is a generator, not an editor.** Core content is always applied.
   The tabs only select seed rolls, optional balance changes and QoL changes.
2. **No game assets are bundled.** On first build the app extracts the donor
   tables it needs from the user's game via `retoc` (~547 filters, ~1.2 GB
   cache). This is what keeps the download small. The game must be installed.
3. **One seed pack installed at a time.** The app builds into a seed library
   and installs the newest pack files into `~mods`; it does not leave two
   `ExpeditionInfinity_*` packs fighting each other.
4. **Seed + profile = result.** The seed alone is not reproducible if the
   Balance/QoL/Language settings differ. A profile (and a manifest) travels
   with every build.
5. **The seed generator's default profile is the Expedition Infinity preset**,
   not C# field defaults. The canonical values live in one place and are
   shared by the app and tests. Character and attribute cap are **333** (a
   deliberate design change from the shipped v7 500); AP 21, 3 turns,
   permadeath and the other current values stay.
6. **Language switches both the pack language and the app UI.** English,
   French, Polish and Russian use native language names and translated UI
   strings. The first launch offers the language choice before game setup.
7. **Per-platform one-file builds.** Linux x64 and Windows x64 are separate
   single-file executables, each carrying its own `retoc`/`repak`. A universal
   binary would roughly double the download.
8. **Party of Five is always on**, because it is part of the current pack.
   It is shown as core, not as a toggle.
9. **Balance is curated only.** The full raw Gameplay Values page stays in the
   editor; the generator exposes only the safe difficulty/economy/progression
   knobs.
10. **V1 also ships** canonical presets, profile import/export, seed history,
    a build manifest and the spoiler link.

---

## 4. Information architecture

### 4.1 Main window

```
┌──────────────────────────────────────────────────────────────────────┐
│  EXPEDITION INFINITY                    Seed 12345  [🎲]   [Build]   │
│  Randomizer │ Balance │ Quality of Life │ Language                  │
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  tab content (scrollable cards)                                      │
│                                                                      │
├──────────────────────────────────────────────────────────────────────┤
│  Game: found   ~mods: ready   Game data: ready (547/547)   v1.0.0    │
└──────────────────────────────────────────────────────────────────────┘
```

- Dark E33 palette from the old app (`Ink/Panel/Gold/Cream`).
- Persistent seed chip + Build button in the header.
- A status strip shows game path, mods path, donor-cache status, and app
  version; clicking opens **Setup**.
- **Setup** (dialog, not a tab): game folder, mods folder, seed library,
  refresh game data, open logs, check for updates (optional).
- Keyboard: `Ctrl+B` build, `Ctrl+R` new seed, `Ctrl+S` save profile.

### 4.2 First run

1. Auto-detect the game; if missing, ask for the game folder.
2. Resolve `Paks/~mods`; create it if absent; show it.
3. Choose a language (default English).
4. Choose a starting profile: **Expedition Infinity** (default), **Easy**,
   **Brutal**, **Custom**.
5. On first launch, a warning explains the one-time donor extraction
   (~1.2 GB, 5–15 minutes) and where it is stored (inside the application
   folder). The user must choose **Start extraction** or **Cancel**. Cancel
   leaves the app usable; Build asks again before extracting. The extraction
   overlay shows a friendly category, task count and progress bar.

### 4.3 Build flow

A build sheet replaces the content area (or opens as a modal) with:

1. Validate paths / free space / lock.
2. Prepare tools (extract embedded `retoc`, `repak`, mappings, licences).
3. Ensure donor cache (resumable, progress per filter).
4. Build core mod.
5. Apply Balance.
6. Apply QoL.
7. Apply Randomizer (seed).
8. Localize (if not English).
9. Build spoiler codex.
10. Pack + install + write manifest/profile.
11. Done: pack path, size, seed, profile, spoiler link, Open folder, Copy
    seed, Build another.

Cancellation is accepted between stages; the installed pack is only replaced
after the final copy. Errors keep the previous working pack.

---

## 5. Tab specifications

Legend: **Available** = already supported by Core; **New** = needs new Core
work; **Fixed** = core invariant, not exposed.

### 5.1 Randomizer

Purpose: produce the seed and control what the seed rolls.

#### Seed

| Control | Detail |
|---|---|
| Seed number | 0–99,999,999, typed or `🎲` |
| Dice | New number every click |
| Copy seed | Clipboard |
| Seed history | Last 20 builds with seed, profile, language, timestamp; click to restore seed + profile |
| Build label | Show `ExpeditionInfinity_<seed>_v1.3_P` |
| Determinism note | "Same seed + same profile = same content. Not necessarily byte-identical files." |
| Share profile | Export/import a `.eiseed.json` profile; seed alone is not enough |

#### Loot sources (Available)

| Toggle | Effect |
|---|---|
| Chests / pickups | Pictos/weapon chests roll; keys/journals/music/quest items stay vanilla |
| Paint Cages | 21 cages roll one tier-appropriate picto/weapon |
| Enemy drops | Regular/elite/boss/overworld drops |
| Merchant inventories | Tier-appropriate stock |
| Weapon passives | Effect rolls per weapon/tier/character |
| Monster stats | HP/attack/speed/defense bands |
| Weapon stats | Attack, free-aim and element rolls |

If all are off, the build is "core + Balance/QoL only" and the seed does
nothing. Show that clearly.

#### Battle groups (new, experimental)

| Setting | Value |
|---|---|
| Battle groups | **on** |
| Max enemies per battle | **8** |
| Chance to add enemies | 100% |
| Test area | **Flying Waters only** (`GO_` encounter prefix) |
| Shuffle final roster | on |

The field-side capacity patch raises the battle assets from four to eight
enemy slots. The back rank sits 220 units behind the front anchors with a
±145 lateral spread. Boss, alpha and scripted rows are excluded. The area prefix is a
first-pass test filter: `GO_` matches Flying Waters; blank means all eligible
encounter rows. Verified on seed 123456: 31 of 45 Flying Waters rosters ended
over three enemies, 7 at the full eight, and zero non-Flying-Waters rosters
changed.

**RandomBattles (runtime re-roll).** The newer module pre-generates variant
encounter rows and patches the world-enemy base to re-roll
`SelectedEncounter` from `PossibleEncounters` on every battle start. For the
Flying Waters test it uses **3 variants per eligible `GO_` encounter**, group
sizes **1–8**, and excludes bosses, alphas and mimes. Seed 123456 produced
33 eligible originals × 3 = 99 variant rows; five regular Flying Waters
world-enemy BPs received 15 runtime handles, and the `StartBattle` re-roll
patch verified. The fixed-roster Battle groups pass stays off while
RandomBattles is active.

#### Difficulty / scaling (Available, with presets New)

Base T1–T5 stat bands stay fixed in the core and are **not exposed** in the
generator. The UI exposes only percentage boosts:

| Setting | Default | Range | Notes |
|---|---:|---|---|
| Regular Nevron HP boost T1–T5 | +30/+50/+200/+500/+1000% | −100–100000% | Applied after the seed roll |
| Regular Nevron speed boost T1–T5 | +20/+30/+40/+50/+100% | −100–100000% | Applied after the seed roll |
| Regular Nevron attack boost T1–T5 | +10/+20/+50/+100/+200% | −100–100000% | Applied after the seed roll |
| Boss HP boost T1–T3 | +50/+150/+900% | −100–100000% | Percentage over vanilla boss HP; T4–T5 use the hidden endgame curve |
| Boss speed boost | +100% | −99–10000% | Percentage over vanilla boss speed |
| Regular pictos chance | 10% | 0–100 | |
| Elite pictos chance | 66% | 0–100 | |
| Perfect catalyst chance | 3.125% | 0–100 | T5-area level 80+ regulars/elites |
| Petank lumina | 50 | 0–9999 | Chromatic Petank pays 1000 |
| Merchant gear slots | 4 | 0–20 | Pictos + weapons per shop |
| Tint shards per type | 9 | 0–99 | T1–T4 chests |
| Chroma Elixir shards | 2 | 0–99 | T1–T4 chests |
| Shapes per type | 2 | 0–20 | T1–T4 chests |

Presets (app-level): **Expedition** (current), **Easy**, **Brutal**,
**Custom**. Presets only change the settings above; they never touch core
content.

#### New Randomizer options (optional, needs Core)

- **Independent sub-seeds**: loot seed, monster-stat seed, weapon seed. Today
  one `Random` stream drives everything. This is a bigger change to
  `RandomizerKit.Apply` and should be a separate phase.
- **Duplicate policy**: today gear is unique within a pack (cascades down a
  tier, then chroma). Offer "unique" (default) vs "allow repeats".
- **Per-source gear mix**: chest picto/weapon/lumina/chroma weights are
  hard-coded (25/3/36/36); boss recipes and catalyst quantities are hard-coded.
  Expose only if requested.
- **Spoiler codex on/off** and **open after build** (app-level; Core flag for
  generation).
- **Dry-run preview**: counts of chests/drops/shops and a sample of rolls
  before packing. Needs a new non-packing preview path.

#### Always-on core (not shown)

Tier system (T1–T5), movesets, 165 new pictos, weapon effect families, five
passive slots, 333 weapon levels, boss defenses, story-level pins, endgame
variants, quest/key protections, unique Act 3 drops, Petank payout rules,
spoiler codex. These are what make it Expedition Infinity; they are not
"options".

---

### 5.2 Balance

Purpose: change the difficulty/economy *after* the seed rolls, without
changing the core content.

#### Curated knobs (v1)

| Group | Setting | Default | Range / notes |
|---|---|---:|---|
| Enemy toughness | Buff regular enemies | off | Non-boss rows only |
| | HP bonus T1–T5 | 10/30/50/200/500% | advanced per-tier array |
| | Speed bonus T1–T5 | 10/20/30/40/50% | advanced per-tier array |
| | Attack bonus T1–T5 | 5/10/20/50/100% | advanced per-tier array |
| Economy | Adjust lumina drops | off | Chest/enemy/shop only; dialogue untouched |
| | Lumina multiplier | 1.0 | 0.1–5.0; below 1 reduces, above 1 increases |
| Difficulty | Permadeath | **off, not exposed** | Not a completed feature; the generator always forces it off |
| | Dodge/parry preset | Expedition | Expedition / Normal / Custom |
| | Dodge window | 0.165 s | 0.01–1.0 |
| | Parry window | 0.1125 s | 0.01–1.0 |
| Boss movesets | Mask Keeper full moveset | on | `MaskKeeperFullMoveset` |
| | Sirène full moveset | on | `SireneFullMoveset` |
| Progression | Character level cap | 333 | 99–999; see note below |
| | XP per extra level | 33,333,333 | 1–99,999,999 |
| | Attribute points/level | 2 | 0–20 |
| | Attribute cap | 333 | 99–500 |
| | Skill points/level | 1 | 1–10 |
| | Lumina per level | 2 | 1–10 |
| Player power | HP / Attack / Speed / Crit / Defense × | 1.0 each | 0.1–10 |
| Weapon progression | Max weapon drop level | 222 | 33–333 |

**Decision: curated only.** The generator's Balance tab is not the full
Gameplay Values page. The following values are **fixed to the core design** and
are not exposed in v1:

| Fixed value | Why |
|---|---|
| Max AP 21 | Core moves cost up to 21 |
| Max consecutive turns 3 | Core tempo |
| Max Foretell 30 | Sciel/Twilight tuning |
| Max shields 5 | Global clamp (enemies too) |
| Max gradient charges 10 | HUD rebuild + balance |
| Crit damage addend 1.5 | Core crit design |
| Crit-rate damage scaling on | Core crit design |
| Burn 0.075 / player 0.01 / 3 turns / tail-off 3.0 | Burn is a core damage path |

**The crit-rate patch is explicitly part of the core.** It is already in v7
(verified above) and must stay in every generated pack:
`BP_BattleDamages.ApplyCriticalHitModifiers` uses
`2.5 × max(1, critRate/100) × ExtraCriticalHitDamageMultiplier`. The app does
not expose it as an option and the canonical preset must set
`CritDamageIncrease = 1.5` and `CritRateDamageScaling = true`. Add a build-time
assertion that calls `CritDamageKit.Verify` on the generated asset.

The full editor remains the place to edit those. This keeps the seed
generator safe and avoids shipping a "break the core curve" panel.

**Level-cap decision (2026-10-26): 333 character / 333 attribute cap.** This
is a deliberate design change from the shipped v7 pack, whose
`AC_jRPG_CharactersManager.CharacterMaxLevel` is **500**. Implementation must:

1. set the canonical preset and `work/gameplay_tweaks.json` to 333;
2. update the guide (already says 333) and any 500 references;
3. rebuild and verify the endgame curve, XP and attribute tables at 333;
4. note in the manifest/app version that this is a post-v7 balance change.

Do not ship both values by accident. If testing shows 333 breaks the endgame,
revisit before release.

#### Presets (New, app-level)

- **Expedition Infinity (as shipped)** — the standard enemy buffs, tight
  windows and 100% lumina.
- **Easy** — normal defense windows, 100% lumina, regular-enemy stat bonuses
  25% weaker than the Expedition Infinity preset, plus instant Picto mastery.
- **Brutal** — standard tight windows, 50% lumina, regular-enemy HP bonus
  doubled and speed/attack bonuses 50% stronger than Expedition Infinity.
- **Custom** — anything changed.

Presets are starting points; they do not change core content and should be
labelled "not officially balanced".

---

### 5.3 Quality of Life

Purpose: remove grind/friction or restore vanilla conveniences without
changing the core content.

| Feature | Control | Default | Source |
|---|---|---|---|
| Picto mastery | On/off | Off (optional instant pickup) | `PictoInstantMasteryKit` |
| Max battle proc cards | 0–30 | 5 | `PictoPopupCapKit` (`WBP_PassivePopupContainer`); 0 = vanilla |
| Light stain substitution | Fixed off, not exposed | off | `SkillOverhaulKit.LightStainWildcard` |
| Tutorials | Always skipped (fixed) | on | `DisableTutorials` / `TutorialDisableKit` |
| Weapon bulk upgrade | On/off + levels per hold (1–10) | on, 10 | `WeaponBulkUpgradeKit` |
| Party of Five | Always on (display only) | on | `PartyFiveKit` |
| Auto-open spoiler | On/off | on | app-level |
| Open output folder after build | On/off | on | app-level |
| Remember last output folder | On/off | on | app-level |

Notes:

- **Picto mastery** is a toggle, off in the Expedition Infinity default profile.
  Turning it on grants instant mastery on pickup.
- **Stain substitution** is not exposed in the generator and is always off.
  Elemental Genesis/Cataclysm stay strict by design.
- **Tutorials are always skipped** in the generator and are not exposed as an option;
  if the user picks Normal windows, tutorials can still stay off or be turned
  back on (core rewrites the narrative encounters either way).
- **Weapon bulk upgrade** is a QoL feature, not a balance cheat: it upgrades in
  batches clamped to the catalyst tier.
- **Party of Five is always on**, matching both `work/party_five.json` and the
  shipped v7 pack (`AC_jRPG_CharactersManager.MaxCharactersAmountInParty = 5`).
  It is a heavy submodule (BattleManager / CharactersManager / world pawns) and
  must be tested on both platforms. Show it as a read-only "Core: five-member
  party" row, not a toggle.
- **Revive limit** is deferred to a later version; the generator ships vanilla
  revive behaviour for v1.

---

### 5.4 Language

Purpose: choose the language baked into the generated pack.

| Language | Code | Culture | Status |
|---|---|---|---|
| English | en | en-US | Source; no locres shipped |
| Français | fr | fr-FR | Catalog exists; review notes minor issues |
| Polski | pl | pl | Catalog exists; review notes mechanical issues |
| Русский | ru | ru | Catalog exists; review notes mechanical issues |

Behavior:

- One pack = one language. The app does not ship multi-language packs.
- English leaves the game locres untouched.
- Non-English merges the mod-owned keys into the culture's vanilla
  `Game.locres` and packs it into a dedicated `zz_<packName>_P.pak` so it
  loads after `pakchunk0` and overrides modified vanilla keys.
- The tab changes both the mod content and the app UI immediately.
- Show coverage/validation status. A **Validate** button runs
  `LocalizationKit.Validate(code)` and shows structural issues.
- The spoiler codex is English-only for now; state that in the tab.

**Decision: ship all four with translated UI and pack text.** The language
cards show the native name and a native subtitle, with no culture codes or
validation button. English remains the default unless the system language is
one of the supported languages.

---

## 6. Settings model

### 6.1 Data root

The standalone build is portable. Keep its data inside the application folder
(the directory that contains the published executable), not the repo or the
user profile:

- Windows/Linux: `<application folder>/`

```
ExpeditionInfinity/        application folder
  settings.json            last game path, output mode, window state
  profiles/
    expedition.json        built-in copy / user profile
    my-run.eiseed.json     shared profile
  seed-history.json        last 20 builds
  tools/
    retoc[.exe]
    repak[.exe]
    Mappings.usmap
    MappingsLegacy.usmap
  work/
    tables/                ~1.2 GB donor cache
    mod/                   staging (~20 MB)
    locres/                cached vanilla locres
  Export/
    Seeds/
      ExpeditionInfinity_12345_v1.3_P/
        ExpeditionInfinity_12345_v1.3_P.{pak,ucas,utoc}
        zz_ExpeditionInfinity_12345_v1.3_P.pak   (non-English only)
        ExpeditionInfinity_12345_v1.3_P_manifest.json
        ExpeditionInfinity_12345_v1.3_P.eiseed.json
        ExpeditionInfinity_12345_Spoiler.html
  logs/
    build-2026-10-26T19-00-00.log
```

`EditorPaths.Configure(dataRoot, paksDir, toolsDir)` already supports this
layout.

### 6.2 Profile shape

```json
{
  "format": 1,
  "name": "Expedition Infinity",
  "generatorVersion": "1.0.0",
  "seed": 12345,
  "language": "en",
  "randomizer": { "...": "RandomizerKit.Settings" },
  "balance": { "...": "curated balance DTO" },
  "qol": {
    "pictoMastery": "none",
    "lightStainWildcard": false,
    "skipTutorials": true,
    "bulkUpgrade": { "enabled": true, "levels": 10, "tapPlusOne": false },
    "maskKeeper": true,
    "sirene": true,
    "partyOfFive": false
  }
}
```

Use app-level DTOs with explicit JSON names, not raw Core types, so old
profiles keep loading when Core changes. Map to Core settings at build time.
Clamp/validate on load and show warnings instead of failing silently.

### 6.3 Canonical default profile

Create one `ExpeditionPreset` (in Core or a shared `Generator` project) with:

**Randomizer**

| Setting | Value |
|---|---|
| Chests / PaintCages / EnemyDrops / Merchants | true |
| WeaponPassives / MonsterStats / WeaponStats | true |
| StatLo / StatHi | 0.95–1.05 … 1.50–1.80 |
| BossHp | 1.5 / 2.5 / 10 / 25 / 50 |
| AttackLo / AttackHi | 1.20–1.30 … 2.15–2.45 |
| BossSpeed | 2.0 |
| RegularPictosChance / ElitePictosChance | 10 / 66 |
| T5PerfectCatalystChance | 3.125 |
| PetankLumina | 50 |
| MerchantGearSlots | 4 |
| TintsPerType / ChromaElixirShards / ShapesPerType | 9 / 2 / 2 |

**Balance**

| Setting | Value |
|---|---|
| LevelCap / AttributeCap | 333 / 333 (canonical; update `work/` from 500) |
| ExtraXpPerLevel | 33,333,333 |
| AttributePointsPerLevel / SkillPointsPerLevel | 2 / 1 |
| LuminaPerLevel | 2 |
| Hp/Attack/Speed/Crit/Defense multipliers | 1.0 |
| MaxAP / MaxConsecutiveTurns | 21 / 3 |
| MaxForetell / MaxShields | 30 / 5 |
| MaxGradientCharges | 10 |
| CritDamageIncrease / CritRateDamageScaling | 1.5 / true |
| BurnMultiplier / BurnPlayerScale / BurnDuration / BurnTailOff | 0.075 / 0.01 / 3 / 3.0 |
| DodgeWindow / ParryWindow / TightenDefenseWindows | 0.165 / 0.1125 / true |
| Permadeath | **false, always** (feature not completed) |
| BuffRegularEnemies / ReduceLuminaDrops | false / false |
| LuminaDropMultiplier | 1.0 |
| WeaponDropLevel | enabled, 222 |
| MaskKeeper / Sirène | on / on |

**QoL**

| Setting | Value |
|---|---|
| Picto mastery | off (toggleable; optional instant pickup) |
| Max battle proc cards | 5 |
| Light stain wildcard | false (fixed) |
| Tutorials | always skipped (fixed) |
| Bulk upgrade | on, 10, tap +1 off |
| Party of Five | always on |

The existing `GameplayTweaksKit` code defaults are **not** this preset. The
canonical preset must be an explicit object, and `FullPackKit` must receive it
from the app (not re-load a possibly missing `work/gameplay_tweaks.json`).

### 6.4 Manifest

Write next to every pack:

```json
{
  "format": 1,
  "app": "Expedition Infinity",
  "appVersion": "1.0.0",
  "seed": 12345,
  "builtAt": "2026-10-26T19:00:00Z",
  "language": "en",
  "profileName": "Expedition Infinity",
  "profileSha256": "…",
  "gameFingerprint": "…",
  "packFiles": ["…pak", "…ucas", "…utoc"],
  "spoiler": "…html",
  "profile": { "...": "full profile" }
}
```

This is the support/debug artifact and the reproducibility record. The seed
alone is not enough; the manifest proves which profile produced the pack.

---

## 7. Build pipeline

### 7.1 Stages and progress

Extend `FullPackKit.Options` with a stage callback:

```csharp
public enum BuildStage
{
    Validating, Tools, Donors, Core, Balance, QualityOfLife,
    Randomizer, Localization, Spoiler, Packing, Installing, Done
}

public Action<BuildStage, double, string>? OnStage { get; set; }
public Action<string>? Log { get; set; }
```

The UI maps stages to a checklist/progress bar with a human label. The existing
donor extraction callback stays for per-filter progress.

### 7.2 Required Core changes

| Change | Why |
|---|---|
| Add `SkillOverhaulKit.Settings? SkillOverhaul` to `FullPackKit.Options` | Expose `LightStainWildcard` and character gates without writing `work/skill_overhaul.json` |
| Add `WeaponDropLevelKit.Settings? WeaponDropLevel` | Expose max drop level |
| Add `WeaponBulkUpgradeKit.Settings? WeaponBulkUpgrade` | Expose bulk levels / tap +1 |
| Always pass `PartyFiveKit.Settings { Enabled = true }` | Party of Five is core; do not let a missing `work/party_five.json` silently turn it off |
| Add `bool IncludeSpoiler` | Let the app skip the codex if desired |
| Add `string? SeedLibraryRoot` | Give every seed its own library folder instead of the flat `Export/` |
| Add `bool InstallToMods` | Support "export only" / custom folder |
| Add `bool VerifyPack` | Run `retoc verify` before the trio is installed |
| Add stage callback + cancellation token | UI progress and stage-boundary cancellation |
| Clean only installed seeds | Keep old seed trios in the library |
| Add `string? PackRevisionOverride` to `FullPackKit.Options` | The standalone uses `_v1.3` (`ExpeditionInfinity_<seed>_v1.3_P`); CLI/editor keep `_v7` |
| Split `PackCombined` into pack-to-export + install | Keep the library copy even when not installing |
| Split library vs install cleanup (`CleanInstalledSeedPacks`) | Do not delete the seed library |
| Add `BuildManifest` writer | Reproducibility/support |
| Add profile/settings validation | Fail early with actionable messages |

Keep `FullPackKit`'s current default behavior (load `work/*.json` if no options
are passed) so the CLI and full editor are unchanged.

### 7.3 Tool bootstrap

Embed in the app and extract on first launch to `<dataRoot>/tools/`:

- `retoc` (strip it; see §9)
- `repak`
- `Mappings.usmap`, `MappingsLegacy.usmap`
- licences/notices
- (only if CUE4Parse is kept) Oodle DLL; better: remove the CUE4Parse
  dependency for the generator (see §9.3)

Do **not** rely on `AppContext.BaseDirectory` or single-file temp extraction
paths to find tools. Pass the extracted `toolsDir` into
`EditorPaths.Configure(...)`. Verify file existence + executable bit; show a
clear error if extraction fails (antivirus, read-only app data).

### 7.4 Donor cache

- First run: `DonorExtract.EnsureAll` extracts 547 filters (~1.2 GB). The
  current per-filter `IsPresent` check makes it resumable; keep that.
- Show a first-run warning with estimated disk/time. Check free space (~2 GB).
- Write `work/tables/cache.json` with a game fingerprint (versions/sizes of
  `pakchunk0-Windows.utoc`, `global.utoc`, patch containers) and filter-list
  hash. On startup, if the game changed, warn "Game updated; refresh game
  data" and offer to clear the cache.
- Recovery: if `~mods.packer-aside` exists from a crashed extraction, restore
  it on startup.
- Do not delete other users' mods in `~mods`. `SuspendMods` moves and restores
  the folder; keep that behavior and surface it in the log.

### 7.5 Output and install

- Pack into `Export/Seeds/<packName>/` in the data root (seed library).
- Copy the pack files to `~mods` (or a user-picked folder) unless "export only".
  Non-English builds include a separate `zz_..._P.pak` localization archive.
- Before installing, remove only other `ExpeditionInfinity_*` packs from the
  **install folder**, after warning. Never touch unrelated mods.
- Create `~mods` if missing; verify write permission.
- Warn if the game is running.
- Warn that a seed change mid-playthrough is not safe; start a new game.
- Success screen shows pack path, size, seed, profile, spoiler, manifest.

### 7.6 Error handling

- Stage + exception + last 200 log lines in the UI.
- Full log saved to `logs/build-*.log`; "Copy log" button.
- Actionable messages for: game not found, no Paks, no free space, no write
  permission, retoc/repak missing, donor extraction failed, pack failed,
  language locres missing, game running, `~mods` restore conflict.
- Cancellation between stages leaves the previous installed pack intact.

---

## 8. Architecture / code layout

Repurpose the old `E33Packer.App` rather than starting a new project.

```
src/ExpeditionInfinity.App/              (renamed from E33Packer.App)
  Program.cs
  App.axaml(.cs)
  Assets/
  Views/
    MainWindow.axaml(.cs)
    RandomizerView.axaml(.cs)
    BalanceView.axaml(.cs)
    QualityOfLifeView.axaml(.cs)
    LanguageView.axaml(.cs)
    SetupDialog.axaml(.cs)
    BuildProgressView.axaml(.cs)
  ViewModels/
    MainViewModel.cs
    RandomizerViewModel.cs
    BalanceViewModel.cs
    QualityOfLifeViewModel.cs
    LanguageViewModel.cs
    BuildViewModel.cs
  Services/
    AppPaths.cs
    ToolBootstrapper.cs
    GameInstallService.cs
    GeneratorProfile.cs
    ProfileStore.cs
    PresetCatalog.cs
    SeedHistory.cs
    BuildManifest.cs
    BuildService.cs
    CrashRecovery.cs
  Controls/
    SettingRow.axaml
    TierBandEditor.axaml
    Card.axaml
```

Use the existing hand-rolled `INotifyPropertyChanged` pattern (no new MVVM
package). Use Avalonia 11.2.8, Fluent theme, E33 palette. Reuse the old app's
icon and style resources.

**BuildService** owns the `Task.Run` around `FullPackKit`, maps app profiles to
Core settings, and streams progress/log to the UI via `Dispatcher`.

**PresetCatalog** is the single source of truth for defaults; tests assert the
serialized preset matches the canonical table in §6.3.

**ProfileStore** handles versioning, clamping, import/export and the seed
history.

**ToolBootstrapper** extracts embedded binaries to the data root and verifies
them.

---

## 9. Standalone packaging and size

### 9.1 Current measurements (Linux x64, this machine)

| Build | Size |
|---|---:|
| Current self-contained single file, unstripped `retoc` | **83 MB** |
| Same, with `retoc` stripped (93 MB → 6.9 MB) | **63 MB** |
| Same, `PublishTrimmed=true` (experimental) | **39 MB** |
| Final 1.3 Linux build: four tabs, unstripped retoc + repak + mod data | **85 MB** |

The trimmed build succeeds but emits many `IL2026`/`IL2057` warnings from
UAssetAPI + Newtonsoft reflection. Do not ship trimming until the app is
smoke-tested against real assets; a trimmed build that silently fails to
deserialize a table is worse than a larger download.

### 9.2 Recommended shipping configuration

- One self-contained single-file per RID (`linux-x64`, `win-x64`).
- `PublishSingleFile`, `EnableCompressionInSingleFile`,
  `IncludeNativeLibrariesForSelfExtract`.
- Strip `retoc` in CI (`strip` on Linux; the Windows release is already
  stripped). Consider stripping `repak` too.
- Embed `retoc`, `repak`, mappings and licences; extract to data root at
  startup.
- Include only the current RID's tools (not both platforms).
- Bundle no game assets. Donor cache is created on the player's machine.
- Target: **~65 MB untrimmed** per platform. Investigate trimming later for a
  ~45 MB build, with a full smoke test.
- NativeAOT is not recommended for v1 (Avalonia + UAssetAPI reflection).

### 9.3 Optional size/dependency win: drop CUE4Parse from the generator

`GameIndex`/CUE4Parse is used by:
- `LocalizationKit.EnsureVanillaLocres` (to read `Game.locres` from the game).
- `TextureExportKit` (not used by the generator).

The vanilla `Game.locres` files are inside the game's legacy
`pakchunk0-Windows.pak`, and `repak` can already list/get them
(`Sandfall/Content/Localization/Game/<culture>/Game.locres`). Replacing
`EnsureVanillaLocres` with a `repak get` path removes the CUE4Parse/Oodle
dependency from the generator, cuts size and removes a network-download risk.
Recommended as a phase-2 refactor (keep CUE4Parse for the full editor).

### 9.4 Licences / notices

Bundle the licences for `retoc`, `repak`, UAssetAPI, CUE4Parse, Avalonia,
Newtonsoft.Json, the mappings and the mod's own content. Add an **About /
Licences** dialog. Do not redistribute game assets.

---

## 10. Issues and risks

| # | Issue | Severity | Mitigation |
|---|---|---|---|
| 1 | Old standalone uses stale code defaults, not the current mod preset | **High** | Canonical `ExpeditionPreset`; pass explicit `GameplayTweaksKit.Settings`; add a preset test |
| 2 | Decided 333 character/attribute cap, but `work/`, both project sidecars and the shipped v7 pack all verify at 500; code default 250 | **High** | Change preset + `work/` to 333, update the guide, rebuild/verify the endgame curve. Do not ship a silent mismatch |
| 3 | Old standalone omits Party of Five (current `work/party_five.json` = on; user wants always on) | **High** | Pass `PartyFiveKit.Settings` with `Enabled = true`, test on both platforms, and update the guide's "4 in battle" text |
| 4 | Language builds need `repak`, but the CI app does not bundle it | **High** | Embed/strip `repak`; add a language build smoke test |
| 5 | CUE4Parse localization may download Oodle at runtime; offline risk | **Medium** | Bundle Oodle or replace `EnsureVanillaLocres` with `repak get` |
| 6 | `CleanOldSeedPacks` deletes all seed exports, not just the installed one | **High** | **Resolved for the generator:** `CleanInstalledSeedPacks` cleans only `~mods`; the library under `Export/Seeds/<packName>/` is preserved |
| 7 | Single-file tool lookup may not find `retoc`/`repak` in the publish layout | **High** | Explicit `ToolBootstrapper` extraction to data root; never trust `AppContext.BaseDirectory` |
| 8 | Seed alone is not reproducible across profiles | **Medium** | Profile + manifest + share profile; UI copy explains it |
| 9 | First run extracts 1.2 GB / 547 filters; slow and disk-heavy | **Medium** | Clear onboarding, disk check, resumable cache, cache fingerprint, optional pre-warm |
| 10 | `~mods.packer-aside` can be left behind on a crash | **Medium** | Startup recovery |
| 10a | Donor filter list omitted assets the skill kits resolve by name | **High** | Found in the 1.3 test; broad family filters added and verified |
| 10b | Prefix filters counted ≥5 files as complete, skipping broad extraction | **High** | Fixed with `.donor/<filter>.ok` extraction markers |
| 10c | Kits still look in editor-only `work/probe-*` / `extracted` folders | **High** | App creates compatibility links to `work/tables`; verified |
| 10d | Windows language builds need a Windows `repak.exe` | **Medium** | `tools/repak/repak.exe` added; Windows artifact still needs a full test |
| 11 | Non-English translations can contain text defects | **High** | All four languages ship with translated UI/pack text; no validation panel or beta badge is shown |
| 12 | Core damage curve assumes Max AP 21 / 3 turns / 333 (or 500) level cap; exposing raw values can break it | **High** | Curated-only Balance tab; keep AP/turns/burn/crit fixed |
| 13 | Party of Five is a heavy, risky submodule and will always be on | **Medium** | Test both platforms and existing saves; document that it is core, not a toggle |
| 15 | App UI is English only while the Language tab selects pack content | **Low** | State clearly; add UI localization later if requested |
| 16 | Any `~mods` seed pack conflict can shadow assets | **High** | Install one seed at a time; detect/clean only our packs; warn |
| 17 | Changing seed mid-playthrough can invalidate a save's gear | **High** | Onboarding warning: new game per seed; one seed at a time |
| 18 | Game updates invalidate donor cache / mappings | **Medium** | Cache fingerprint; refresh button; mappings version check |
| 19 | Builds can take minutes | **Medium** | **Resolved:** stage progress + cancellation between stages; retoc subprocess calls remain non-interruptible |
| 20 | A versioned name (`ExpeditionInfinity_<seed>_v1.3_P`) can still collide with a poisoned pack | **Low** | Install only one seed; clean only our packs; manifest carries app version; add a "force rebuild name" escape hatch if needed |
| 21 | Antivirus/SmartScreen on unsigned self-extracting exe | **Medium** | Code-sign if distributing publicly; document |
| 22 | Docs drift (perfect catalyst 1% vs 3.125%, level 90+ vs 80) | **Low** | Reconcile in UI text and docs |
| 23 | Two builds could overwrite `work/mod` | **Medium** | **Resolved:** named single-instance mutex; a second window explains that the generator is already running |
| 24 | No pack verification after build | **Medium** | **Resolved:** `retoc verify` runs before install; `.utoc`/`.ucas` presence checked; manifest records files and profile hash |
| 25 | Seed history/profile sharing can bloat app data | **Low** | Cap history at 20; prune old logs |

---

## 11. Implementation phases

### Phase 0 — Confirm decisions

- Answer the open questions in §13.
- Freeze the canonical Expedition preset (333 level/attribute cap, Party of
  Five always on, language shipping). Change `work/` from 500 to 333, rebuild
  and verify the endgame curve before UI work depends on it.
- Set the standalone pack name to `ExpeditionInfinity_<seed>_v1.3_P`; leave
  the CLI/editor at `_v7`.

### Phase 1 — Shell and settings (app-only)

- Rename `E33Packer.App` → `ExpeditionInfinity.App`; update namespaces, icon,
  window title, assembly name, CI artifact names.
- Build the dark tabbed shell: Randomizer, Balance, Quality of Life, Language.
- Implement `GeneratorProfile`, `PresetCatalog`, `ProfileStore`,
  `SeedHistory`, `AppPaths`, `BuildManifest`.
- Implement Setup dialog: game folder, mods folder, seed library, refresh game
  data, logs.
- Implement first-run detection and the "ready / missing" status strip.
- Implement settings persistence and profile import/export.
- No Core changes yet; Build button can call the existing pipeline with the
  canonical preset once passed explicitly.

### Phase 2 — Core plumbing

- Extend `FullPackKit.Options` (§7.2).
- Add stage progress, install/export split, library cleanup, manifest.
- Align defaults to `ExpeditionPreset`.
- Add `ToolBootstrapper` and single-file-safe tool paths.
- Add startup recovery for `~mods.packer-aside` and stale locks.
- Add a canonical-preset unit test and a no-game profile round-trip test.

### Phase 3 — Feature-complete tabs

- Randomizer: all available controls, T1–T5 editors, presets, validation,
  seed history.
- Balance: curated safe knobs, presets, warnings; no raw AP/turn/burn/crit
  panel.
- QoL: mastery dropdown, stain wildcard, tutorials, bulk upgrade, boss
  movesets; Party of Five is always-on core and read-only.
- Language: four native language cards; selection switches the app UI and pack text immediately.
- Build sheet: stages, cancel, log, success screen, open folder, copy seed,
  spoiler link.

### Phase 4 — Packaging and hardening

- CI: strip tools, embed `repak` + mappings + licences, per-RID publish,
  artifact naming.
- First-run extraction UX, disk check, cache fingerprint, refresh.
- Install/cleanup warnings, one-seed enforcement, game-running warning.
- Smoke tests: FR/PL/RU build, missing game, read-only folder, cancel,
  interrupted extraction, existing ~mods, long paths.
- Decide trimmed vs untrimmed release (default untrimmed).

### Phase 5 — New options (only if approved)

- Independent sub-seeds.
- Duplicate policy / per-source weights.
- Dry-run preview.
- Revive cap integration (revisit after v1).
- Profile share codes.
- UI localization.

Party of Five is **not** in this phase; it is always-on core from Phase 2.

---

## 12. Test plan

### Automated

- `ExpeditionPreset` serializes/deserializes and maps to the expected Core
  settings.
- Profile version migration (v1 → current).
- Seed range/clamping and invalid band validation (`lo <= hi`).
- Pack name + manifest generation.
- `FullPackKit` option plumbing: a fake build with each setting produces the
  expected flag in the manifest (no game assets required for the settings
  layer).
- `LocalizationKit.Validate` for en/fr/pl/ru (structural only).
- CI builds the single-file app for linux-x64 and win-x64.

### Manual / integration (game installed)

- Fresh build with default profile; verify pack loads and seed matches.
- Same seed + same profile twice: same content (spot-check loot/shops/stats).
- Each Randomizer toggle off/on.
- Each Balance preset.
- Each QoL toggle.
- Crit-rate damage assertion: extract the built `BP_BattleDamages` and run
  `CritDamageKit.Verify(path, 1.5f, true)`; it must return `true`. This is a
  core invariant, not an optional balance setting.
- Each language; verify in-game text and pack contents.
- First run with empty `~mods`, with other mods present, with an old seed
  installed.
- Interrupted first-run extraction; resume.
- Game not installed / wrong folder / read-only `~mods` / low disk.
- Cancel during each stage.
- Windows long path and non-ASCII user name.
- Verify spoiler codex opens offline and matches the pack.
- Verify only one `ExpeditionInfinity_*` trio in `~mods`; library keeps old
  seeds.

### Acceptance criteria

- A new user can install the app, point it at the game, pick a seed, press
  Build, and get a playable pack in `~mods` without a terminal.
- The default build is the current Expedition Infinity preset, not stale C#
  defaults.
- The app is one executable; no .NET install or repo files required.
- The pack is reproducible from its manifest/profile.
- A failed/cancelled build never leaves the previously working pack broken.
- The app never deletes unrelated mods or the user's seed library.

---

## 13. Decisions and remaining questions

### Resolved (2026-10-26)

| Topic | Decision |
|---|---|
| Data strategy | Extract donor assets from the installed game; keep the download small |
| Platforms | Windows x64 + Linux x64, one self-contained file each |
| Default preset | **333 character / 333 attribute cap** (deliberate change from v7's 500); other balance values stay at the current preset. Update `work/`, rebuild and verify |
| Party of Five | **Always on** |
| Languages | Ship all four; language selection switches both the app UI and the pack text |
| Balance depth | **Curated only**; no raw AP/turn/burn/crit panel |
| Crit-rate damage patch | **Core, always on**: addend 1.5, scaling on; verified in v7; build-time assertion required |
| V1 extras | Canonical presets, profile import/export, seed history, manifest, spoiler link |
| Revive cap | **Leave out for v1**; revisit later |
| Pack naming | `ExpeditionInfinity_<seed>_v1.3_P` (app version appended to the seed) |

**333 decision evidence:** the shipped v7 pack was inspected on 2026-10-26:
`AC_jRPG_CharactersManager.CharacterMaxLevel = 500`. The generator deliberately
changes the default to 333, so this is a post-v7 balance change, not a docs
fix. The release notes must say so.

### Still to confirm

1. **Artifact distribution.** GitHub Releases from the existing
   `.github/workflows/packer.yml`, or another channel? Code signing? Not
   required for development, but matters for public Windows distribution.
2. **Profile sharing.** Profiles + manifest are in v1. Do you also want a
   compact "seed code" string or `.eiseed.json` file attachment? Recommended:
   `.eiseed.json`; a compact string can come later.

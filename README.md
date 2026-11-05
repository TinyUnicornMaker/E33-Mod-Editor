# E33 Mod Editor

Standalone editor for Clair Obscur: Expedition 33 game data, starting with
monster DataTable editing. Built for Linux and Windows.

## Build

Requires the **.NET 10 SDK**. The GitHub Actions release job fetches
[`retoc` v0.1.5](https://github.com/trumank/retoc/releases/tag/v0.1.5) at build
time. The published executable currently bundles retoc/repak; end users do not
download them. For a local publish, place `retoc`/`retoc.exe` and its runtime
libraries in `src/E33Packer.App/tools/`. `repak` for both platforms is already
under `tools/repak/`. See [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).

```bash
# Linux x64
CUE4PARSE_SKIP_NATIVE=true dotnet publish src/E33Packer.App/E33Packer.App.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:IncludeAllContentForSelfExtract=true \
  -p:DebugType=None -p:PublishTrimmed=false \
  -o dist/linux-x64

# Windows x64 (run on Windows, or use the GitHub Actions Windows job)
$env:CUE4PARSE_SKIP_NATIVE="true"
dotnet publish src/E33Packer.App/E33Packer.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:IncludeAllContentForSelfExtract=true `
  -p:DebugType=None -p:PublishTrimmed=false `
  -o dist/win-x64
```

## Architecture

```
Game (Steam, IoStore containers)
   │  CUE4Parse        → locate assets in containers
   │  retoc to-legacy  → convert to editable .uasset/.uexp
   ▼
DataTableEditor (UAssetAPI) → dump/apply JSON edits
   │  retoc to-zen     → pack into priority mod container (_P)
   ▼
Paks/~mods/E33ModEditor_P.{utoc,ucas,pak}
```

- `src/UAssetAPI/` — vendored UAssetAPI (asset read/write, net8.0)
- `src/CUE4Parse/` — vendored CUE4Parse core (archive indexing, net10.0,
  native helpers skipped via `CUE4PARSE_SKIP_NATIVE=true`)
- `src/E33ModEditor.Core` — GameIndex, Retoc wrapper, DataTableEditor,
  SaveGame (GVAS .sav read/write)
- `src/E33ModEditor.Cli` — command line interface
- `tools/retoc_cli-x86_64-unknown-linux-gnu/retoc` — retoc v0.1.5
- `tools/Mappings.usmap` — community mappings for E33

## Bundled submodules

The editor writes **one** modpack, `Export/E33ModEditor_P`. The camera and the
5-member party are **submodules** of that pack, not separate `_P` paks — two
paks shipping the same assets shadow each other by load order, which is how the
camera pack used to fight the party pack over `BP_jRPG_Character_World`.

| Submodule | Tile | Settings | Notes |
|---|---|---|---|
| Camera | **Camera** | `work/firstperson.json` | Close third-person / first-person camera. Rewrites the `DA_CameraConfig_*` assets, world pawns and the climb component. Includes an opt-in depth-of-field extension (dungeons + overworld). |
| Party of Five | **Party of Five** | `work/party_five.json` | 5-member party with a two-row formation (1/2 front, 3/5/4 back). Owns `AC_jRPG_BattleManager`, `AC_jRPG_CharactersManager`, `FL_jRPG_CustomFunctionLibrary`, `BP_jRPG_Character_World`, etc. |

Both are applied by `rebuild-mod` (CLI) and by the standalone generator. The
editor GUI no longer builds or packs mods. The party runs last so no other kit
restages (and reverts) the assets it shares. When
Party of Five is on, the third-party **Bigger Party Mod** compatibility path is
skipped — the pack owns the party assets itself.

## Build

```bash
export PATH="$HOME/.dotnet:$PATH"   # .NET 10 SDK lives here
dotnet build -c Release
```

## Expedition Infinity standalone seed generator (1.3.3)

The full editor is not required to generate a seed. The standalone app lives in
`src/E33Packer.App` and is published as one self-contained file per platform:

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet publish src/E33Packer.App/E33Packer.App.csproj -c Release -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:IncludeAllContentForSelfExtract=true \
  -p:DebugType=None -p:PublishTrimmed=false \
  -o dist/linux-x64-1.3
```

The Linux build is `dist/linux-x64-1.3/ExpeditionInfinity` (85 MB) and bundles
an unstripped `retoc`, `repak`, mappings and the mod data. It has four tabs:
**Randomizer**, **Balance**, **Quality of Life** and **Language**. Packs are
named `ExpeditionInfinity_<seed>_v1.3.3_P`. All settings, the one-time game-data
cache and the seed library live inside the application folder
(`<app>/Export/Seeds/<packName>/`). On first launch the app searches the usual
Steam folders for the game. It shows the detected full path and lets you choose
another folder; if detection fails, it explains what happened and offers the
same manual folder picker. It then warns that the one-time setup needs about
1.2 GB and 5–15 minutes, and waits for confirmation before extracting. Only the
newest pack files are copied to the game's `~mods` or a folder you choose.
The profile, manifest and spoiler codex are written next to the library pack.
Choosing a profile in Setup applies its settings immediately; if you changed
anything manually, the app asks before replacing those custom settings. Builds
show stage progress, can be cancelled between stages, and run
`retoc verify` before installing. A
second generator instance is blocked so two builds cannot race over the donor
cache or `~mods`. On Linux, run `dist/linux-x64-1.3/install-desktop-entry.sh`
once to register the launcher entry, icon and window class with the desktop. See
[`docs/expedition-infinity-standalone-plan.md`](docs/expedition-infinity-standalone-plan.md)
for the design and
[`Export/TestBuilds/README.md`](Export/TestBuilds/README.md) for the 1.3
English/French verification results.

## Usage

```bash
CLI=src/E33ModEditor.Cli/bin/Release/net10.0/E33ModEditor.Cli.dll

# which container holds an asset?
dotnet $CLI find DT_jRPG_Enemies.uasset

# extract to legacy files (filter = asset name)
dotnet $CLI extract DT_jRPG_Enemies --out work/legacy

# dump rows to JSON (UserDefinedStruct suffixes stripped)
dotnet $CLI dump work/legacy/Sandfall/Content/jRPGTemplate/Datatables/DT_jRPG_Enemies.uasset --out work/enemies.json

# dump an asset's import table (index, raw, class.object <- outer)
dotnet $CLI dumpimports work/tables/graphs/Sandfall/Content/Gameplay/SkillTree/Content/Maelle/DA_SkillGraph_Maelle.uasset FleuretFury

# hand-rolled probe: clone Fencers Flurry into a NEW Maelle skill "Charming Shot"
# (applies BP_BattleBuff_Charmed), wire it into her graph + balancing table,
# pack E33Probe_P. See the skill-probe source for the full recipe.
dotnet $CLI skill-probe

# edit rows: { "RowName": { "Field": value } }  — arrays accept {"<index>": v},
# structs recurse, enum-keyed stats maps are visible in dumps
cat > work/test_edits.json <<'EOF'
{ "SM_FirstLancelier": { "Level": 42, "EnemyScaling": { "HP": 9.9 } } }
EOF
dotnet $CLI apply work/legacy/Sandfall/Content/jRPGTemplate/Datatables/DT_jRPG_Enemies.uasset work/test_edits.json

# pack into the game's ~mods directory as E33Test_P.{utoc,ucas,pak}
dotnet $CLI pack work/mod --name E33Test_P

# full combined rebuild -> E33ModEditor_P (all submodules: camera + party of five)
# reads work/{gameplay_tweaks,randomizer,firstperson,party_five,compatibility}.json
dotnet $CLI rebuild-mod

# save file editing (GVAS)
dotnet $CLI save-roundtrip "$SAV"            # verify byte-identical re-serialization
dotnet $CLI save-dump "$SAV" --out save.json # full save as JSON
dotnet $CLI save-write save.json new.sav     # JSON back into a .sav
dotnet $CLI save-pictos-add "$SAV" --id PainterConv_Fire --level 3 --master
                                             # default writes <sav>.bak_<timestamp> next to the file;
                                             # pass --no-backup to skip it
```

## Save Editor (GUI)

The **Save Editor** module edits `EXPEDITION_*.sav` directly (UE 5.4 GVAS,
byte-exact reader/writer — untouched saves re-serialize identically, verified
against every save on the dev machine).

Module flow: the module opens on an **Active Saves** screen listing only the
saves the game's Continue loads (the newest title-screen slot per Steam
account). One click shows an "Opening Save …" screen while the file parses,
then the editor slides in with tabs: Pictos, Characters, Weapons, Resources,
World, Music, Journals. The bar at the bottom has **Close without saving**
(discards every in-memory edit, back to save selection, file untouched) and
**Save and exit** (writes the .sav atomically with timestamped backups under
`work/saves-backup/<name>/`, reports the outcome — success auto-returns to the
module launcher, failure stays with the reason).

- Pictos tab — Found / Mastered / Level for every pictos, plus a gold
  **Created** section fed by `PictosCreateKit` and the `work/create_pictos.json`
  toggles. **Find all** always includes created pictos, there is an
  **Add all created ✨** button, and the phantom-lumina scan / orphan removal
  live in the toolbar. Presence checks consult the packed sidecar
  (`Export/E33ModEditor_P.project.json`) as well as `work/mod`.
- Characters tab — the playable roster is always listed. Members missing from
  the save show "+ not in save — click to add": clicking clones a fresh
  level-1 record (default skin/face, starter skill, empty gear) into
  `CharactersCollection`, immediately editable and fieldable via the party
  toggle. The internal `Frey` record displays as **Gustave**. Each character
  page ends with a **Wardrobe → Unlock all costumes & hairstyles** button
  adding every `Skin*/Face*` item for that character to the inventory.
  Edits write into the open save immediately and persist across character
  switches; nothing touches disk until Save and exit.
- Weapons tab — unlock + level via the shared progression list.
- Resources tab — Chroma (currency), tint shards, upgrade catalysts,
  chroma packs.
- World tab — NG+ count, spawn point, InteractedObjects /
  BattledEnemies / EncounteredEnemies flag toggles.
- Music / Journals tabs — inventory-driven unlocks using catalogs built during
  preload from `DT_MusicRecords` / `DT_Items_Journals`.

Recommended flow for created pictos: enable them in **Pictos → Create new
pictos**, run `rebuild-mod` (or the standalone generator) so the `_P` mod contains them, then open your save in
the Save Editor and tick the created rows.

```bash
GUI=src/E33ModEditor.App/bin/Release/net10.0/E33ModEditor.App.dll
dotnet $GUI          # E33_AUTOOPEN=save-editor opens the module directly
```

## Story-point saves

The save's main story is a 17-step `Main_GoldenPath` quest; the Save Editor's
picker reads the in-progress objective (e.g. **Monoco Station**) rather than
the generic quest name. **Save Editor → World → Main-story checkpoint** applies
a chapter rewind in memory (write with Save and exit, or discard with Close
without saving). A CLI helper does the same and optionally grants test gear:

```bash
dotnet $CLI save-story --list
dotnet $CLI save-story "$SAV" out.sav --point monoco-station \
  --char-level 100 --weapon-level 66 --pictos-level 10
```

`--point` takes the chapter index (1–17) or key. The tool writes the chapter's
quest/forced-camp state, map/spawn, visited/unlocked lists and party; from
Monoco's Station backwards it also clears the Monoco and Stalact battle flags,
the station trigger zones and the Monoco relationship. Side content (chests,
merchants, side quests) keeps the donor's state, so this is a test save, not a
bit-exact fresh chapter. See
[`docs/save-story-points.md`](docs/save-story-points.md) for the full flag map
and the game's own debug-save list.

## Verified end-to-end (2026-08-24)

1. Extracted `DT_jRPG_Enemies` fresh from game v1.5.4 (434 rows)
2. Applied `Level: 42` + nested `EnemyScaling.HP: 9.9` edits
3. Packed into `~mods/E33Test_P.*`
4. Re-read with a clean CUE4Parse mount of game + mods:
   mod values override originals; all untouched fields round-trip intact
5. Preload extracts `DT_MusicRecords` (33 tracks) and `DT_Items_Journals`
   (49 journals) and fills the Save Editor catalogs (weapons: 135)
6. `save-roundtrip` byte-identical over all 23 saves on the dev machine
   (including `Backup/`); `save-pictos-add --id --level --master` verified
   via re-dump (inventory + passive progression + weapon progression entries)

## Languages

The launcher and pack support **English**, **French**, **Polish** and
**Russian**. On first launch the launcher asks for a language, defaulting to
English and updating the confirm button as you choose. Changing it in
the **Language** tab updates both the app UI and the generated pack
immediately. Pack builds can also select a language with
`rebuild-mod --language fr|pl|ru|en`. A translated build ships a patched
`Sandfall/Content/Localization/Game/<culture>/Game.locres` inside a separate
load-last `zz_<packName>_P.pak`; English leaves the game's locres untouched.

Catalogs live in `src/E33ModEditor.Core/Localization/Data/`, with
`work/localization/<code>.json` as a live override while translating. See
[docs/localization.md](docs/localization.md) for the catalog workflow
(`loc-catalog`, `loc-validate`, `loc-apply`) and the markup/number rules.

## Custom pictos

See [docs/custom-pictos.md](docs/custom-pictos.md) for the clone/pack rules
(parent table vs composite, unique IoStore `FolderName`, culture-invariant
hover text, two-stat `BaseStatMultipliers`). Missing any of those is why a new
pictos can appear as Accelerating Heal with no level or stats.

## Chests / Drops and Randomizer

The **Chests / Drops** tile maps `DT_ChestsContent`, enemy `PossibleLoot`, and
merchant inventories onto the original randomizer's T1–T5 pictos/weapon bands
and the interior level of each area. The frequency panel is cooked slot counts
plus expected rolls (`LootPercentageChance`).

The **Randomizer** tile shuffles those sources with the original protocols,
minus the later chest-wipe that replaced every pickup with chroma/lumina. Keys,
journals, music, quest items and exploration catalyst tables stay vanilla.
`rebuild-mod` / the standalone generator writes the result into the same `E33ModEditor_P` pack.

**Lower-tier catalyst drops.** Non-boss field drops (regular, elite and
overworld) also roll the two catalyst tiers below their own: x1 one tier down
at 50% and x1 two tiers down at 30%, never below L1. T5 rows use the field
catalyst they actually drop (the capped L4), so they roll L3 at 50% and L2 at
30%.

**Shop catalysts.** Merchants stock their own catalyst tier at x33 and every
lower catalyst at x999 bulk stock. T5 shops keep their vanilla restriction and
never sell an L5 catalyst.

**Paint Cages.** The 21 Paint Cage reward rows are their own randomizer group
with an independent toggle. Each cage always rolls exactly one tier-appropriate
pictos or weapon — never a shard, catalyst, chroma or lumina. The group is
identified in `LootCatalog.PaintCageChests`; the cage actor itself carries no
chest/loot link, so the list is curated from the exclusive rewards and level
actor placement.

**Petank lumina.** Regular Petanks pay the configured 50 Colour of Lumina. The
chromatic Petank (`WM_Petank_Alpha`) pays exactly 1000 — its signature payout
replaces the standard boss 5 lumina instead of stacking with it.

**Battle groups (experimental).** An off-by-default randomizer toggle adds
enemies to regular encounter rosters and patches the battle assets so slots
4–8 spawn, sort and display. Bosses, alphas, tutorial/scripted fights and known
broken rows are left alone. The field patch reuses each map's four vanilla
enemy anchors (extra slots alias an anchor and start in a second row);
`DispatchEnemiesAlongSpline` then respaces the whole group along the map's
enemy spline. The separate **Random battle every time** toggle pre-generates
variant encounter rows and appends them to each world enemy's native
`PossibleEncounters` list; the world-enemy base is patched to re-roll that list
on every battle start, so the same overworld enemy can produce a different
group each time it is attacked. See [`docs/battle-groups.md`](docs/battle-groups.md)
for the capacity patch, roster rules, settings and the in-game checks still
needed.

**Battle proc cards.** `PictoPopupCap` (Gameplay Values → Battle proc cards,
default 5; 0 = vanilla) caps the passive/picto proc cards on the battle HUD.
The container already tracks its live cards in a map, so the patch drops a proc
once the cap is reached (a visible card stays up and only skips its counter
tick while full). See
[`docs/gameplay-tweaks.md`](docs/gameplay-tweaks.md#battle-proc-cards-pictopopupcap).

**Enemy / lumina balance switches.** Two off-by-default toggles in
**Gameplay Values → Enemy & lumina balance** scale the finished pack after the
randomizer: *Buffed regular enemies* raises non-boss HP/Speed/Attack per tier
(HP +10/30/50/200/500%, Speed +10/20/30/40/50%, Attack +5/10/20/50/100%), and
*Reduce regular lumina drops* halves chest, enemy-drop and shop lumina while
leaving every dialogue grant (Blanche) untouched. See
[`docs/gameplay-tweaks.md`](docs/gameplay-tweaks.md#enemy-and-lumina-balance-buffregularenemies--reduceluminadrops).

**Rare Perfect Catalyst.** On top of the tier recipes, every endgame
regular/elite enemy — a T5-area row at fight level 80+ (the whole T5 band,
which includes Flying Manor 83 and Verso's Drafts 86; Elites included,
bosses/alphas/superbosses/overworld/scripted rows excluded) — also rolls one
Perfect (`UpgradeMaterial_Level5`) Chroma Catalyst at the configured chance
(default 3.125%, ≈ 1/32 — `T5PerfectCatalystChance`; 0 disables). Because loot lives on the
enemy row, a row that also spawns in a lower-band encounter is barred so the
catalyst cannot leak outside the endgame areas; reuse in the Endless Tower is
exempt (`RandomizerKit.QualifiesForT5PerfectCatalyst` + `T5CatalystBarred`,
verified by `rando-smoke`).

**Endgame enemy variants.** The endgame areas reuse lower-tier enemy rows:
Flying Manor is a gauntlet built almost entirely from borrowed early-game rows
(Spring Meadows, Flying Waters, Stone Wave Cliffs, …), and Renoir's Drafts /
Act 3 Lumière borrow a few from Visages, Sirene and The Reacher. Loot lives on
the row, so a borrowed row would drop its native low-tier loot even at T5.
`EndgameVariantKit` clones each borrowed row into a `T5_<Row>` variant pinned to
T5 (level 99) and repoints only the endgame encounters (`DT_jRPG_Encounters`) at
the variant, so those fights roll full T5 loot and the rare catalyst while the
native row keeps its own tier — the T5 loot cannot leak down into earlier
content (mirrors `WorldMapVariantKit` for the optional world-map fights).

## Spoiler Codex

Every `rebuild-mod` / standalone generator run writes a single-file, player-facing
spoiler for the seed it just packed:

```
Export/Spoilers/ExpeditionInfinity_<seed>_Spoiler.html
```

That file carries its own data — no companion JSON, no Python, no server — and
can be handed to other players and opened offline. It covers every Nevron by
area with effective stats and scaling multipliers, seed drop percentages, the
seed's chests grouped by area, and browsable Pictos, Chroma, Key Item, Tint,
Weapon and full item catalogues. The **Nevron data** switch toggles the seed
against vanilla rows; cut content is hidden by default.

Generator: `src/E33ModEditor.Core/Spoiler/SpoilerCodexKit.cs`; template:
`src/E33ModEditor.Core/Spoiler/template.html`. For a generator-only check from
the current `work/mod` stage:

```bash
dotnet $CLI spoiler-build 42
```

See [docs/spoiler-tool/README.md](docs/spoiler-tool/README.md).

## Weapon bulk upgrade

The Camp weapon-upgrade button upgrades **up to 10 levels per hold**, clamped to
the current catalyst tier (so a bulk at level 28 stops at 33 and never charges a
T2 catalyst). Costs, VFX and the maxed checks still run per level. The prompt
reads "Upgrade 10 Levels".

Because this widget crashes if its graph grows mid-function, the patch is
append-only: it redirects the click path's own `PushExecutionFlow` at a block
placed after the final `Return`. See
[docs/weapon-bulk-upgrade.md](docs/weapon-bulk-upgrade.md) for the design,
crash history, verification (`bulk-smoke`) and the approaches that were tested
and rejected (tap-to-+1, a second prompt row, dynamic label text).

## Weapon drop levels

Weapon drops are scaled independently of pictos: the vanilla shared loot level
(1–33) is mapped proportionally to 1–`MaxDropLevel` (222 by default) at the
gear instance and BattleManager reward-filter sites. Pictos stay vanilla 1–33;
the shared `FL_LootSystemHelpers` formula is not changed. Configure in
`work/weapon_drop_level.json` and verify with `drop-smoke`. See
[docs/weapon-drop-level.md](docs/weapon-drop-level.md).

## Notes / next steps

- EnemyStats map keys show as `E_jRPG_StatType::NewEnumeratorN` (usmap lacks
  friendly enum names). Editing them by key is not supported yet.
- Monsters grid shows **effective** HP/attack (`Eff. HP` / `Eff. Atk`):
  `Normal-archetype(level) x EnemyScaling`, which is what
  `FL_EnemyScalingHelpers.ComputeCharacterStats` builds at runtime. The raw
  `EnemyStats` table carries placeholders (`HP 1.0 / ATK 0.0`) for 153
  late-game rows, so showing it directly read as 0 in-game-high. Raw values
  stay editable under Raw Stats; scaling edits refresh the effective columns.
- GUI: Avalonia data-grid front-end over DataTableEditor is the natural next step.
- `work/` holds scratch extraction output and is safe to delete.

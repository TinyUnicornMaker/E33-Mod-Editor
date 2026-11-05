# New Game Test Batch — 1.3 / seed 654321

Build: `ExpeditionInfinity_654321_v1.3_P`, deployed 2026-10-28 14:05 (English).
Purpose: batch every check that needs a fresh start so the opening sector is played once.

## Pre-flight (already verified)

- Deployed `DT_jRPG_Enemies.uexp` hash matches the staged 19:31 build: `b88a9493…`.
- White-Nevron pins are present in the deployed table.
- Meteor entry is 550; damage path matches the safe 18:36 build; Might factor is 192.
- Row Damage is **enabled**: rear spots [1,3,4] take/deal ×0.8.
- World-map Grosse Tête is `BV_MF_GrossTete_50`, T3, no Perfect catalyst.
- Stalact tutorial stays on the tutorial row with the plain actor; HP is 68,670 at L25.
- Close camera is **enabled**: boom 130 / socket (50,35) / FOV 85 exploration framing.
- Buffed regular enemies is **on (steepened)**: HP +30/+50/+200/+500/**+1000%**, Speed +20/+30/+40/+50/**+100%**, Attack +10/+20/+50/+100/**+200%** by tier. Judge regulars against White Nevrons and bosses accordingly.

## A. White Nevrons

Compare each against the normal enemies in the same area and the nearest boss, not just the raw HP number.

| Encounter | Venue / level | Deployed HP scaling | Intended effective HP |
|---|---|---:|---:|
| White Jar (`QUEST_JarNeedLight`) | Spring Meadows / 3 | 3.25 | 3,627 |
| White Demineur (`QUEST_DemineurWithoutMine`) | Flying Waters / 8 | 4.875 | 18,252 |
| White Demineur (`QUEST_DemineurWithMine`) | Flying Waters / 8 | 4.875 | 18,252 |
| White Hexga (`QUEST_HexgaLuster`) | Stone Wave Cliffs / 19 | 3.5208565 | 56,890 |
| White Rocher (`QUEST_Rocher_HexgaQuest`) | Stone Wave Cliffs / 19 | 21.125139 | 56,890 |
| White Troubadour kill (`QUEST_TroubadourCantPlay_Kill`) | Stone Quarry / 30 | 10.1684 | 404,560 |
| White Portier (`Portier_DoorSoul`) | Esoteric Ruins / 28 | 45.5 | 1,570,023 |
| White Grown Bourgeon (`GO_Bourgeon_Quest`) | The Small Bourgeon / 13 | 4.5 | ~35,856 — **not pinned** |

Watch specifically:

- **Grown Bourgeon** — the one White Nevron deliberately left out of the pin list. Compare it with the normal Nevrons used to feed it; this is the most likely remaining “dies instantly” case.
- **White Jar** — the first-map one the player originally called ~1k HP. The quest light is a gate, not a stat variant: `LightJarLamp` must be completed before the NPC's free-aim target is enabled, and the completed Jar then uses the same `QUEST_JarNeedLight` row (HP scaling 3.25, ATK ×2.0, SPD ×1.5).
- **Troubadour** — only the `_Kill` row is the real fight; the dodge-trial row is intentionally low.

Record: time-to-kill, relative to the normal enemies next to it and the area boss.

## B. Tutorial / narrative redirection

The tutorial-disable kit rewrites encounters and actor classes. Check that the intended plain fights load and keep their stats/drops:

- Maelle tutorial
- Curator
- First Lancelier
- Évêque shield tutorial
- Chalier gradient-counter tutorial
- Volester flying tutorial
- Pelerin at Monoco’s Station
- Stalact gradient tutorial → plain `MM_Stalact` actor
- Gustave/Sciel tutorial encounter

For Stalact, confirm it is the level-25 tutorial fight, not the level-58 Frozen Hearts stat block. Current check: `MM_Stalact_GradientAttackTutorial*1` still points at the tutorial row, and that row uses the plain `BP_EnemyBattle_Stalact` actor. HP is `Elite[25] × 2.5 = 68,670`; the old Strong-based reference was 22,890, so decide in-game whether 68k is too tanky.

## C. Starting weapons, shops and early loot

- Starting weapon for each character, including its **Might scaling** on a low-level weapon.
- First merchant: stock, catalysts, 999 lower-tier catalyst behaviour.
- First chests and paint cages: item levels/tiers, no missing or 0-stat pictos.
- **Dodge Specialist** should display a **1 Lumina** equip cost.
- White-Nevron quest rewards: XP/Chroma/drops follow the new HP.
- **Grosse Tête**: `WM_GrossTete` must still resolve to `BV_MF_GrossTete_50` and drop the T3 catalyst, not a Perfect/L5 catalyst.

## D. Early story bosses and encounter levels

Check the first-sector fights are not inflated by world-map/tier remaps:

- Goblu
- Évêque
- Curator
- First Lancelier
- Ultimate Sakapatate (if reached in the batch)
- Any tutorial row that shares a native encounter row

## E. Party of Five and first level-up

- Fifth member joins correctly; party menu and battle slots work.
- Formation/back-row positioning is correct.
- Row Damage is **enabled** in this build: rear spots [1,3,4] should take and deal ×0.8. Check in the first battle.
- First level-up: +2 attribute points, 333 cap, UI accepts points, Might/other stats display correctly.
- AP/level cap values look sane at the first scripted fight.
- Camera framing: over-the-shoulder exploration view in dungeon and overworld, no Verso hair clipping, climb stays zoomed in.

## F. Picto acquisition — Instant mastery is ON

The 22:30 English 654321 pack has **InstantPictoMastery** enabled (`AutoPictoMastery` off), with the hook rebuilt without any generated bool local (the loader rejects UAssetAPI `FBoolProperty` locals as `size 0`), using bare out-param contexts and three-parameter `Array_Get` through the array-library CDO.

**Status: PASS (2026-10-27) — the user confirmed auto-learn works in-game.** The acquisition matrix below is now a regression check rather than an open requirement. Every acquisition route should still master the picto immediately:

- chest
- enemy drop
- shop
- quest reward
- save fill
- duplicate pickup
- picto with multiple passives

Also check the Picto/Lumina UI refreshes immediately and the passive can be equipped without a battle. Pictos acquired before the pack was installed are **not** retroactively mastered; use the Save Editor “Master all found” for those.

The one-battle path is off; `BP_jRPG_Controller_World` is intentionally not staged.

## G. Language pass — deferred

The current deployed pack is **English** (`en`, source strings). The previous
Russian pass (`ru/Game.locres`) was backed up before switching back to English;
run this section only when a Russian build is deployed again. Check:

- Main menu, HUD, battle UI and system messages.
- Skill names/descriptions, especially the mod-created skills and tooltips.
- Picto and weapon names/descriptions, including wrapped/long text.
- Item/shop strings and catalyst descriptions.
- Dialogue/subtitles and any tutorial-adjacent text.
- Font glyphs for Cyrillic: no missing boxes, no clipped lines.
- `loc-smoke` already confirms 1244 entries, 0 missing, 0 structural issues; this pass is visual/in-game.

Russian backup: `work/pak-backup/2026-10-27-ru-before-en/`.
French backup: `work/pak-backup/2026-10-27-fr-before-ru/`.

## Not new-game-specific — test separately

- **Clone EXP** — existing Dance Class save.
- **Picto levels / NG+** — NG+ or data check.
- **Row Damage re-enable** — after re-enabling `work/row_damage.json`, test on any existing battle.
- **Picto mastery matrix** — **PASS** (2026-10-27); keep the routes in section F as a regression check.
- **Lune Light-stain wildcard** — cast a normal elemental skill with one required stain missing while holding Light; it should cast and consume Light. Genesis/Cataclysm must still require all four elements; Mayhem must not consume Light.
- **Orbs / Gestral arena** — existing saves; confirm vanilla/document.

## Pass criteria

- No fight crashes; no missing/blank assets.
- White Nevrons are meaningfully durable relative to their area, not one-shot trash.
- Tutorial/narrative fights use the intended venue stats and drops.
- Starting weapons/shops/chests produce valid items at sane levels.
- First level-up and party-of-five flow work.
- No new Sentry events during the batch.

## Evidence to capture

- Build/seed + `~mods` list.
- Screenshots or short clips for Grown Bourgeon and White Jar.
- Time-to-kill notes for each White Nevron.
- Any Sentry `undefined opcode` / flow-stack events.

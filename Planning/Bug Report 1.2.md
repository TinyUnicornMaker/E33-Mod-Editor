# Expedition Infinity 1.2.x — Running Bug Report

**Last updated:** 2026-10-28 14:05 (Regular-enemy buff steepened: tiers shifted up one, new T5 is 2× old T5)
**Scope:** player report from the 1.2.1 comment thread + follow-up testing on 1.2.1 and 1.3.
**Status:** running document — add a test-log row whenever something is checked in-game.

## Builds under test

| Build | Seeds | Notes |
|---|---|---|
| 1.2.1 | 7777777, 333333 (Vanilla Parry & Dodge), 666666 | Released 2026-10-22; packs under `~/Downloads/ExpeditionInfinity/v 1.2.1/` |
| 1.3 | 654321 (French) deployed; 123456 test build | Deployed in `~mods`; 2026-10-27 build |
| 1.3 English pack | 654321 | Deployed 2026-10-27 21:12; English source strings; InstantPictoMastery on; `.pak` stub |
| 1.3 Russian pack | 654321 | Built 2026-10-27 20:40; `ru/Game.locres`; backed up at `work/pak-backup/2026-10-27-ru-before-en` |

## Status legend

| Status | Meaning |
|---|---|
| **PASS** | Tested and working; no action unless it regresses |
| **OPEN** | Reproducible / unresolved |
| **REPRO** | Needs exact reproduction details from the player |
| **TESTING** | Actively being tested |
| **VANILLA** | Confirmed vanilla behaviour or intended design |
| **DEFER** | Feature / backlog, not a release blocker |

## Standalone 1.3.6 scope

- **Excluded:** Permadeath, Challenges (enemy HP/Defense/Speed options) and the
  Burn stack cap. They are incomplete/broken and are not part of the standalone
  packer.
- **Core, always on:** Party of Five — confirmed fully working.
- **Experimental, forced off:** Battle groups and RandomBattles.
- The Gargant TTK, orb-summoning parry loop and Gestral arena payout are
  vanilla behaviour, not issues.

## Bug / test tracker

| ID | Report | Type | Status | Tested on | Evidence / next step |
|---|---|---|---|---|---|
| BR1.2-01a | White Nevrons feel weak (first map and the “grown” one) | Balance / verify | **OPEN** — pins deployed; new-game batch | Agent: deployed hash verified | Deployed `DT_jRPG_Enemies.uexp` matches the 19:31 build (`b88a9493…`). Pin rows: Jar 3.25→3,627; Demineur 4.875→18,252; Hexga 3.5208565→56,890; Rocher 21.125139→56,890; Troubadour 10.1684→404,560; Portier 45.5→1,570,023. `GO_Bourgeon_Quest` is **not pinned** and is the likely remaining outlier. See `Planning/New Game Test Batch 1.3.md`. |
| BR1.2-01b | 32-hour Gargant side-quest fight | Balance / access | **VANILLA** | — | Intended game behavior, not a mod issue. The 9,999 damage cap and quest gating are vanilla. |
| BR1.2-02 | Shadow Bringer consumes 10 AP when AP > 10 | Bug | **PASS** | 1.3 in-game | Retested by the user: Shadow Bringer does **not** consume AP. Further edge-case testing is optional. |
| BR1.2-03 | Clone kills give inconsistent EXP | Bug / exploit | **FIXED (code + rando-smoke)** | 1.3.4 code + `rando-smoke` 2026-11-06 | `MM_Danseuse_CloneAlpha` now pays 10% XP/chroma (`CloneRewardFactor`), and respawning clones (`MM_Danseuse_CloneAlpha`, `QUEST_Danseuse_DanceClass_Clone`) pay no catalysts or lumina. Runtime evidence: 200 chromatic-Danseuse summon kills ≈ 3.2B XP; the clone was on the main alpha's reward rate. `rando-smoke` asserts clone XP = 10% of main and no catalyst/lumina drops. `BV_SM_Abbest_Alpha_69` also splits the Tower Abbest XP outlier (venue scaling 86 vs native 1837 in the smoke pack). |
| BR1.2-04 | Orb-summoning enemy attacks forever if parried | Vanilla / confirm | **VANILLA** | — | Confirmed intended behavior; not a mod issue and not a 1.3.6 blocker. |
| BR1.2-05 | Auto / instant Picto Mastery not yet tested | Testing | **PASS** | 1.3 English 654321 22:30 — user | **InstantPictoMastery works in-game.** The hook masters the picto when it enters the inventory. Final implementation: no generated bool locals, bare `EX_Context` out-param calls, `Array_Get(array, index, outItem)` through `Default__KismetArrayLibrary`, direct cast/`EX_CallMath` branch expressions. Earlier failures: zero `ArrayDim` locals, `EX_Let` around out-param calls, two-parameter `Array_Get`, and a rejected generated `FBoolProperty`. Reopen only if a pickup route regresses. |
| BR1.2-06 | Gestral arena is a fast Lumina grind | Intended / confirm | **VANILLA** | — | Intended payout; not an issue. Use the Reduce Lumina Drops toggle only if a player wants it slower. |
| BR1.2-07 | Might gives less Attack than Vitality/Defense | Vanilla balance | **PASS** | 1.3 main pack 19:31 — user | Vanilla Might used fixed **55** vs S’s **110**. Deployed factor **192** (~1.75× S); user tested at 333 and lower-level weapons and confirmed the scaling feels appropriate. |
| BR1.2-08 | Picto level above 33 / NG+ scaling | Verify / potential bug | **OPEN** | — | Check `DT_EquipmentScalingTable_Pictos` rows above 33; determine NG+ enemy-level scaling; document max picto level. |
| BR1.2-09 | x10 weapon upgrade only gives one level | Bug / deployment | **PASS** | 1.3 in-game; 1.2.1 assets verified | Works in-game on 1.3. The 1.2.1 and 1.3 x10 assets are byte-identical. Feature is hold-to-bulk; tapping gives one level. Evidence: `work/verify-v121/REPORT.md`. |
| BR1.2-12 | Maelle — Virtuous Strike (`Maelle_VirtuoseStrike`, in-game “Golgra’s Combo”) | Skill fix | **PASS** | 1.3 in-game after fix | Fixed in a previous session and retested successfully. 5-hit Lightning gradient, 3 charges, 6.00x per hit (30x total). Source: `MaelleSkillsKit` `GradientMoves` / `ElementRethemes`. |
| BR1.2-13 | Lune — Meteor (`Lune_SkyBreak`) crashes on use | Crash | **PASS** | 1.3 main pack 18:36 — user | All Lune gradient attacks now work, including Meteor. `OnExecuteSkill` → 550; container verified. |
| BR1.2-14 | Every damage instance crashes after the 18:26 rebuild | Crash | **PASS** | 1.3 main pack 18:36 — user | Basic attacks, shooting, Free Aim and skills all work. Root cause was the Row Damage block remapper; fixed with `insertedCount` and re-enabled at 20:36. |
| BR1.2-15 | Row Damage disabled / rear three need 0.8 | Feature | **TESTING** | 1.3 main pack 20:36 | Row Damage re-enabled with the block remapper fix. Build log: back spots **[1,3,4]**, dealt **×0.8**, taken **×0.8**. Installed `AC_jRPG_CharacterBattleStats` has the row block and two `AddDamageModifier` calls. Needs an in-game rear-row check. |
| BR1.2-16 | Tutorial Stalact HP after tutorial skip | Verify | **PASS** | 2026-11-14 — user | Tested in-game; tutorial Stalact uses the intended tutorial row/actor and the fight works correctly. No change needed. |
| BR1.2-17 | World-map Grosse Tête T3 reward | Loot tier | **PASS** | 1.3 main pack 20:36 | `WM_GrossTete` → `BV_MF_GrossTete_50` at encounter level 50. Clone loot is T3: `UpgradeMaterial_Level3` ×5, **no Level5/Perfect catalyst**. Native `MF_GrossTete` remains T4 for its non-world fight; native/CFH Perfect catalyst is capped at 3. |
| BR1.2-18 | Language builds | Localization | **TESTING** | English 21:06; Russian/French backups | English deployed at 21:06: source strings, `.pak` stub, `retoc verify` passed. Gameplay assets unchanged: Might 192, row block, Meteor 550, enemy hash `b88a9493…`. Russian build backed up at `work/pak-backup/2026-10-27-ru-before-en`; French at `work/pak-backup/2026-10-27-fr-before-ru`. |
| BR1.2-19 | Lune Light-stain wildcard | Feature / test | **TESTING** | 1.3 English 654321 23:15 | **Enabled** (`Option.LightStainWildcard: true` in `work/skill_overhaul.json`). The strict `BP_StainHolderComponent` override is gone, so vanilla Light substitution is used for normal skills. Elemental Genesis and Elemental Cataclysm remain strict; Mayhem still never consumes Light. `lune-heal-smoke` wildcard checks pass; `retoc verify` passed. Needs an in-game Lune check. |
| BR1.2-20 | Dodge Specialist Picto cost | Balance | **TESTING** | 1.3 English 654321 23:38 | Cost reduced **15 → 1 Lumina** (`DT_PassiveEffects.DodgeSpecialist.LuminaCost`). The picto has no separate AP equip cost; 1 AP in the request was read as its Lumina equip price. Installed pack re-extracted and verified at 1. Needs an in-game inventory/tooltip check. |
| BR1.2-22 | Confident Fighter Picto costs | Balance | **TESTING** | 1.3 English 654321 00:30 | Costs reduced to **5 / 10 / 20 / 50 / 100 Lumina** for tiers I–V (`PictosMagnitudeKit.LuminaCosts`). Effects unchanged (+5/+10/+20/+50/+100% damage, can't be Healed). Installed pack re-extracted and verified; `pictos-smoke` passed; `retoc verify` passed. Needs an in-game tooltip check. |
| BR1.2-23 | Buffed regular enemies | Balance / test | **TESTING** | 1.3 English 654321 14:05 | **Steepened:** tiers shifted up one slot and new T5 is 2× old T5 — HP +30/+50/+200/+500/**+1000%**, Speed +20/+30/+40/+50/**+100%**, Attack +10/+20/+50/+100/**+200%** (final mults HP ×1.3/1.5/3/6/**11**, Atk ×1.1/1.2/1.5/2/**3**, Spd ×1.2/1.3/1.4/1.5/**2**). Boss/alpha/real-boss rows untouched (Eveque 1.5, White Jar 3.25); YF_Jar (T2) now HP 1.55. 860 fields raised; `balance-smoke` OK; `retoc verify` passed. |
| BR1.2-24 | Alternating Critical Picto costs | Balance | **TESTING** | 1.3 English 654321 13:50 | Costs reduced to **5 / 10 / 15 / 20 / 25 Lumina** for tiers I–V (`PictosMagnitudeKit.LuminaCosts`). Effects unchanged (+10/+20/+30/+40/+50% next non-crit after a crit). Installed pack re-extracted and verified; `pictos-smoke` passed; `retoc verify` passed. Needs an in-game tooltip check. |
| BR1.2-25 | “Skill Consume Burn IV” weapon passive eats every Burn stack, even when another character applies it (1.2.1 seed 333333, Lune `Simonim`) | Bug / kismet | **TESTING (deployed 1.3.1)** | Code + `fx-smoke` + 1.3.1 pack 2026-11-02 | Root cause: the `*Skills` consume variants were patched from vanilla `DamageReason == 4` (base attack) to **`!= 4`**. Burn/status DOT ticks are `DamageReason == 2` (“Invalid”), and `BP_BattleBuffInstance_AggregatorBase.GetDamageSourceCharacter` returns `ChildBuffInstances[0].SourceCharacterStats` — the character who **first** applied Burn, not the one who added the latest stack. The wearer’s passive therefore fired on its own DOT and removed the whole stack (up to the cap), so Burn could never accumulate — for the whole party once the wearer applied the first stack. Fixed by gating skills variants on **`DamageReason == 1` (SkillCast)** instead; vanilla `BP_LU_ThunderSkillConsumeBurn` already uses that shape. Affects `WepFx_BurnConsume1–5Skills` and all `Slow/Powerless/DefenselessConsume*Skills` variants. `fx-smoke` now asserts the comparison. In-game retest needed. |
| BR1.2-21 | Close camera (ImmersiveThirdPerson look) | Feature / test | **TESTING** | 1.3 English 654321 00:14 | **Enabled the built-in camera submodule** (`work/firstperson.json` → `Enabled: true`) instead of shipping the external `ImmersiveThirdPersonCamera_P` pak: it stages the same 6 `DA_CameraConfig_*` assets at boom 130 / socket (50,35) / FOV 85 plus the Verso clone, climb, world-map pawn and `DT_jRPG_WorldCharacters` rewire. The external pak shares `BP_jRPG_Character_World` with Party of Five, so two paks would shadow-fight; the submodule merges both edits into the one staged asset (SmartSpringArm 130/(50,35) + 4 TargetPoints). `firstperson-smoke` passed; `retoc verify` passed. External pak left in Downloads, **not** installed. Needs an in-game framing check. |
| BR1.2-10 | Filter/sort for “not mastered” Pictos | Feature | **DEFER** | — | Spike the Picto inventory widget; add an “Unmastered” filter or sort after 1.3. |
| BR1.2-11 | Level up Pictos like weapons | Feature | **DEFER** | — | Not a quick change; separate UI/economy. Backlog. |

## Test log

| Date | Build / seed | Tester | Test | Result |
|---|---|---|---|---|
| 2026-10-27 | 1.3 / 654321 deployed | user | x10 hold upgrade in-game | **PASS** — works |
| 2026-10-27 | 1.3 | user | Maelle Virtuous Strike (Golgra’s Combo) after fix | **PASS** — works in-game |
| 2026-10-27 | 1.3 | user | Shadow Bringer AP retest | **PASS** — no AP consumed |
| 2026-10-27 | 1.3 deployed | user | Lune Meteor cast | **FAIL** — old asset still loaded |
| 2026-10-27 18:21 | 1.3 main (old) | user | Meteor crash — Sentry event | **FAIL** — `undefined opcode (0x82) at byte offset 601`; `OnExecuteSkill` → `ExecuteUbergraph` |
| 2026-10-27 18:30 | 1.3 main (18:26) | user | Any damage / basic attack | **FAIL** — Sentry `Tried to pop from an empty flow stack` in `ReceiveDamageFromObject` |
| 2026-10-27 18:35 | 1.3 main | agent | Row Damage remapper fix | **TESTING** — `RemapOffsetsForInsertion` now shifts by `insertedCount` |
| 2026-10-27 18:36 | 1.3 main | agent | safe rebuild, Row Damage disabled | **TESTING** — `AC_jRPG_CharacterBattleStats` hash matches the pre-regression pack; Meteor entry 550; container verified |
| 2026-10-27 | 1.3 main 18:36 | user | Basic attack / shoot / Free Aim / skills | **PASS** — no crash |
| 2026-10-27 | 1.3 main 18:36 | user | All Lune gradient attacks incl. Meteor | **PASS** — all working |
| 2026-10-27 | 1.3 code/data | agent | Might vs Attack scaling trace | **INFO** — per-weapon `AttributeScaling`; no formula bug found |
| 2026-10-27 | Medalum 333 in-game | user | Might 333 vs Vitality/Defense 333 | **CONFIRMED** — Might 60k, Vitality/Defense 90k; all three show S |
| 2026-10-27 | 1.3 code/data | agent | Compare base vs mod weapon potency function | **CONFIRMED** — identical; vanilla `Attribute == 2` Might branch uses fixed 55 |
| 2026-10-27 19:19 | 1.3 main | agent | build/deploy Might grade-factor patch | **TESTING** — installed ByteConst 255; Meteor 550; damage path matches old safe build |
| 2026-10-27 19:31 | 1.3 main | agent | build/deploy Might factor 192 | **TESTING** — installed ByteConst 2 + DoubleConst 192; Meteor 550; damage path matches old safe build |
| 2026-10-27 | 1.3 main 19:31 | user | Might 333 + lower-level weapon scaling | **PASS** — scales appropriately; not just max-level |
| 2026-10-27 | 1.3 main 19:31 | agent | Verify deployed White-Nevron table | **PASS** — `b88a9493…` matches staged build; pins present; Grown Bourgeon not pinned |
| 2026-10-27 20:36 | 1.3 main | agent | enable Row Damage + rebuild | **TESTING** — back spots [1,3,4], dealt/taken ×0.8; installed row block verified |
| 2026-10-27 20:36 | 1.3 main | agent | Stalact tutorial routing/HP | **INFO** — tutorial row + plain actor; 68,670 HP at L25 |
| 2026-10-27 20:36 | 1.3 main | agent | World-map Grosse Tête tier | **PASS** — BV clone L50 T3; Level3 catalyst ×5, no Perfect catalyst |
| 2026-10-27 20:40 | 1.3 654321 | agent | Russian language rebuild/deploy | **TESTING** — `ru/Game.locres`; `loc-smoke` ok; gameplay assets unchanged |
| 2026-10-27 21:06 | 1.3 654321 | agent | English rebuild/deploy | **TESTING** — source strings; `.pak` stub; gameplay assets unchanged |
| 2026-10-27 21:12 | 1.3 English 654321 | agent | enable InstantPictoMastery + rebuild | **TESTING** — `BP_DataAsset_Item` installed; OnAddedToInventory hook verified; one-battle path off |
| 2026-10-27 21:20 | 1.3 English 654321 | agent | fix E33PictoCastOk bool mask | **TESTING** — `FieldMask` 255; installed asset verified; rebuild/deploy |
| 2026-10-27 21:25 | 1.3 English 654321 | agent | remove E33PictoCastOk bool local | **TESTING** — inline cast/length jumps; no bool local in installed asset |
| 2026-10-27 21:31 | 1.3 English 654321 | agent | fix out-param call shape | **TESTING** — empty RValuePointer on passive/character calls; rebuilt/deployed |
| 2026-10-27 22:01 | 1.3 English 654321 | agent | canonical Instant Picto bytecode | **TESTING** — `ArrayDim=1` on every added local/inner; vanilla-generated-item clear; bare out-param contexts; `Array_Get` three-param CDO context; `Greater_IntInt` via `EX_CallMath`; rebuilt/deployed |
| 2026-10-27 22:01 | 1.3 English 654321 | agent | installed hook verification | **PASS (static)** — extracted container: `OnAddedToInventory` call shapes matched vanilla; `PictoInstantMasteryKit.Verify` passed; `retoc verify` = verified. In-game launch later exposed the generated bool local. |
| 2026-10-27 22:21 | 1.3 English 654321 | user | launch | **FAIL** — `PropertyBool.cpp` line 145: `Unsupported FBoolProperty E33PictoHasAny size 0` at `SecondsSinceStart: 0` |
| 2026-10-27 22:30 | 1.3 English 654321 | agent | remove generated bool local + rebuild | **TESTING** — no `FBoolProperty` locals; branch on direct cast/`EX_CallMath`; game process launched against the new pack and stayed running (no new crash context) |
| 2026-10-27 | 1.3 English 654321 | user | Might scaling + Virtuous Strike | **PASS** — both tested in-game and working |
| 2026-10-27 23:15 | 1.3 English 654321 | agent | enable Lune Light-stain wildcard + rebuild | **TESTING** — strict stain holder removed; vanilla Light substitution restored; Genesis/Cataclysm stay strict; `lune-heal-smoke` wildcard checks pass; `retoc verify` passed |
| 2026-10-27 23:38 | 1.3 English 654321 | agent | Dodge Specialist cost + rebuild | **TESTING** — `LuminaCost` 15 → 1; extracted installed `DT_PassiveEffects` confirms 1; `retoc verify` passed |
| 2026-10-28 00:14 | 1.3 English 654321 | agent | enable built-in close camera + rebuild | **TESTING** — `firstperson.json` on (boom 130, FOV 85); 6 camera configs + climb + world-map pawn + WorldCharacters staged; `BP_jRPG_Character_World` carries camera + party edits; `firstperson-smoke` passed; `retoc verify` passed |
| 2026-10-28 00:30 | 1.3 English 654321 | agent | Confident Fighter costs + rebuild | **TESTING** — I–V now 5/10/20/50/100; installed `DT_PassiveEffects` confirms; `pictos-smoke` passed; `retoc verify` passed |
| 2026-10-28 13:37 | 1.3 English 654321 | agent | enable buffed regular enemies + rebuild | **TESTING** — 860 scaling fields raised; bosses untouched; `retoc verify` passed |
| 2026-10-28 13:50 | 1.3 English 654321 | agent | Alternating Critical costs + rebuild | **TESTING** — I–V now 5/10/15/20/25; installed `DT_PassiveEffects` confirms; `pictos-smoke` passed; `retoc verify` passed |
| 2026-10-28 14:05 | 1.3 English 654321 | agent | steepen regular-enemy buff + rebuild | **TESTING** — tiers shifted up one, new T5 2× old (HP +1000%, Atk +200%, Spd +100%); YF_Jar HP 1.55; bosses untouched; `balance-smoke` OK; `retoc verify` passed |
| 2026-10-28 14:21 | 1.3 English 654321 + z_BetterMimes | agent | Better Mimes compat + install | **PASS (static)** — 2 assets (`BP_EnemyBattle_Mime` moveset, `ST_Enemies_Skills` strings), no stat tables; neither shipped by our pack nor any other `~mods` pak; trio installed + `retoc verify` passed |
| 2026-11-02 | source + `work/tables` | agent | consume-skills DamageReason fix | **TESTING** — `*Skills` consume clones now gate on `DamageReason==1` (SkillCast) instead of `!=4`; Burn DOT is reason 2 and was consuming the stack. `fx-smoke` passed, including new checks for `WepFx_BurnConsume4Skills`, `WepFx_SlowConsumeSkills` and the base-attack `WepFx_SlowConsume` (still `==4`). Needs a rebuilt pack + in-game retest |
| 2026-11-02 | 1.3.1 / 7539815 | agent | build/deploy consume fix | **TESTING** — standard `ExpeditionInfinity_7539815_v1.3.1_P` rebuilt from the current 7539815 Developer settings; all 14 `*Skills` consume assets patched and re-extracted to confirm no `NotEqual_ByteByte` remains; `retoc verify` = verified; installed into `~mods`; game restart needed for the in-game retest |
| 2026-10-27 | 1.3 English 654321 22:30 | user | Instant Picto Mastery pickup | **PASS** — auto-learn works in-game |
| 2026-10-27 | 1.3 `work/mod` + `work/meteorfix` | agent | `entry-audit` all staged assets | **PASS** — only the vanilla dialogue quirk remains; no other skill entry point bad |
| 2026-10-27 | 1.3 deployed + `work/mod` | agent | SkyBreak bytecode trace | **OPEN** — `OnExecuteSkill` entry still 601; `EX_Jump` 550; `EX_EndOfScript` 557 |
| 2026-10-27 | 1.3 / 654321 deployed | agent | `bulk-smoke` + deployed widget check | **PASS** — append-only loop, click push redirected, prompt present |
| 2026-10-27 | 1.3 / 654321 deployed | agent | `~mods` shadow check | **PASS** — no other mod ships `WBP_BS_WeaponUpgrade` / `ST_UI_InputActions` |
| 2026-10-27 | 1.2.1 / 7777777, 333333, 666666 | agent | `retoc verify` on all three `.utoc` files | **PASS** — all verified |
| 2026-10-27 | 1.2.1 / all three seeds | agent | x10 widget + caps + strings vs 1.3 | **PASS** — relevant `.uexp` files byte-identical to 1.3 |
| 2026-10-27 | 1.2.1 / all three seeds | agent | Dance Class clone row | **PASS** — clone scaling values present in all three |
| 2026-10-27 | 1.2.1 / all three seeds | agent | prompt / hint text | **PASS** — `Upgrade 10 Levels` + hold description present |

## Meteor crash trace (BR1.2-13)

**Symptom:** casting Lune’s Meteor crashes immediately.

**Root cause:** `GradientEditsKit.PatchSkyBreakScript` replaces the dominant-stain `SwitchValue` assigned to `CurrentMeteorElement` with a direct `ByteConst 2` (Fire). That shrinks the statement from **71 bytes to 20 bytes** (`removed = 51`), and the code shifts the UberGraph’s own jumps/pushes. It does **not** shift the external `EX_IntConst` entry point in `OnExecuteSkill`.

Evidence from `work/mod` and the deployed `654321` pack (identical `.uexp` hash `24d2403d…`):

| Item | Vanilla | Staged / deployed |
|---|---|---|
| `OnExecuteSkill` entry | `EX_IntConst 601` | `EX_IntConst 601` — **not shifted** |
| `#14 @478` | `EX_Let` + `SwitchValue`, 71 bytes | `EX_Let` + `ByteConst 2`, 20 bytes |
| `#18` | `EX_Jump @601` | `EX_Jump @550` |
| `#20` | `EX_EndOfScript @608` | `EX_EndOfScript @557` |

The cast path calls `ExecuteUbergraph(601)`, but the staged graph ends at 557. The VM jumps past the end, so the crash happens on **use**, not on load.

### Fix deployed — main pack rebuild (2026-10-27 18:26)

The first `ZZZZ_MeteorFix_P` override did **not** win the container load order. The game’s Sentry event from the failed test is conclusive:

```
Encountered an undefined opcode (0x82) at byte offset 601
BP_Battle_SkillScript_SkyBreak_C
Function ...:OnExecuteSkill
Function ...:ExecuteUbergraph_BP_Battle_SkillScript_SkyBreak
```

That is the old bytecode. The override was removed and the actual seed pack was rebuilt with the source fix:

- `~mods/ExpeditionInfinity_654321_v1.3_P` rebuilt at **18:26** with the 654321 profile and French locres.
- `OnExecuteSkill` → **550**; `EX_Jump @550`; `EX_EndOfScript @557`.
- Installed SkyBreak chunk is `c259a853de061dde…` and `retoc verify` reports **verified**.
- `ZZZZ_MeteorFix_P` moved to `work/pak-backup/2026-10-27-meteor-override/` and no longer loads.

`work/mod` was also patched in place so future `pack work/mod` runs do not reintroduce the bug; the durable fix is the source change.

## Other skills with the same risk

Added `entry-audit [dir]` to the CLI. It validates every `EX_LocalFinalFunction` entry into an `ExecuteUbergraph` against the graph’s statement starts and total bytecode size.

Results:

- `work/mod` after the SkyBreak fix and 654321 rebuild: **939 assets, 989 entry points, 1 bad** — `BP_Dialogue_HiddenArena_Keeper` (`StartDialog` → 20092, inside `EX_Jump` @20091). The same offset exists in the **vanilla** base asset extracted from `pakchunk0`, so it is not a mod regression and not a skill.
- `work/meteorfix`: 1 asset, 1 entry point, **0 bad**.
- No other skill entry point is out of range.

The size-changing skill paths that pass the audit include Lune Quake / Tree of Life / Meteor, Lune’s other `LuneSkillsKit` / `LuneApocalypseKit` moves, Maelle’s Virtuose/status splices, Sciel’s custom moves, Gustave’s charge-drain insertion, and the Monoco/Verso new-move kits. The audit is the regression gate for this class; run it after any new script splice.

**Closest latent risk:** `GradientEditsKit.PatchTreeOfLifeScript` inserts the `Resurrect` call and calls `MayhemFixKit.ShiftAbsolute` but does not shift external entry points. It passes today because the insertion is after the entry point, but it should get the same external-entry shift before any future edit moves the heal block. No Tree of Life crash has been observed.

**Fix:** in `PatchSkyBreakScript`, after `ShiftAbsoluteSigned`, walk the other `FunctionExport`s and shift every `EX_IntConst` entry point in `[oldEnd, uberEnd)` by `removed`, exactly like `LuneSkillsKit.RemoveVoiceFromDirector`. For SkyBreak: `oldEnd = 549`, `uberEnd ≈ 608`, so **601 → 550**. Add a smoke check that `OnExecuteSkill` equals the `EX_Jump` offset and is less than `EX_EndOfScript`.

## Next test — new-game batch

Might is closed. White Nevrons are deployed and verified, but need a fresh game to judge them in context.

Use **`Planning/New Game Test Batch 1.3.md`** so the opening is played once. It batches:

- all White Nevrons, including `GO_Bourgeon_Quest`
- tutorial/narrative encounter redirection (Stalact, Pelerin, Maelle, Curator, etc.)
- starting weapons, shops and early chests/cages
- early story bosses / encounter levels
- Party of Five join + first level-up
- rear-row Row Damage (rear three take/deal ×0.8)
- first picto acquisitions

The most important single check is **Grown Bourgeon vs the normal Nevrons used to feed it**. It is the only White Nevron intentionally left off the HP pin list, so if it still melts, that is the row to fix.

### Not part of the new-game batch

- **BR1.2-03 Clone EXP** — existing Dance Class save.
- **BR1.2-08 Picto levels / NG+** — NG+ or data check.
- **Row Damage re-enable** — existing battle after re-enabling.
- **BR1.2-05 Picto mastery** — acquisition matrix, separate.
- **BR1.2-04 / BR1.2-06** — confirmed vanilla, not issues.

### Resolved in this pass

- **BR1.2-07 Might scaling** — PASS, including lower-level weapons.
- **BR1.2-13 Meteor** — PASS; all Lune gradient attacks work.
- **BR1.2-14 damage regression** — PASS; basic attack, shoot, Free Aim and skills all work.
- **BR1.2-02 Shadow Bringer** — PASS; no AP consumed.
- **BR1.2-12 Maelle Virtuous Strike** — PASS.
- **BR1.2-09 x10 upgrade** — PASS.

## Evidence files

- `work/verify-v121/REPORT.md` — full 1.2.1 x10/caps/clone verification.
- `work/verify-v121/<seed>/` — extracted 1.2.1 assets and dumps.
- `work/verify-v13-123456/` — 1.3 English comparison assets.
- `work/verify-v13-654321/` — deployed 1.3 SkyBreak asset used for the Meteor trace.
- `work/meteorfix/` — fixed SkyBreak legacy asset used for the failed override.
- `work/verify-meteorfix/` — extracted override, verified entry 550.
- `work/verify-meteor-main/` — extracted rebuilt main pack, verified entry 550.
- `work/rebuild-654321/rebuild.log` — first 654321 rebuild (Meteor fix, row damage on).
- `work/rebuild-654321/rebuild-rowfix.log` — Row Damage remapper fix build.
- `work/rebuild-654321/rebuild-safedamage.log` — 18:36 safe build (Row Damage disabled).
- `work/rebuild-654321/rebuild-mightfix.log` — 19:19 Might grade-factor build.
- `work/rebuild-654321/rebuild-mightfactor.log` — 19:31 Might factor 192 build.
- `work/rebuild-654321/rebuild-rowdamage-enabled.log` — 20:36 Row Damage enabled build.
- `work/verify-rowdamage-enabled/` — installed AC stats / enemy table / world-map table.
- `work/enemies-rowdamage.json` — current enemy rows for Stalact / Grosse Tête checks.
- `work/wm-current.json` — current `WM_GrossTete` encounter row.
- `work/rebuild-654321/rebuild-russian.log` — Russian build log.
- `work/verify-russian/` — installed Russian pack gameplay verification.
- `work/pak-backup/2026-10-27-fr-before-ru/` — French pack backup before switching language.
- `work/rebuild-654321/rebuild-english.log` — English rebuild log.
- `work/verify-english/` — installed English pack gameplay verification.
- `work/pak-backup/2026-10-27-ru-before-en/` — Russian pack backup before switching back to English.
- `work/rebuild-654321/rebuild-instant-picto.log` — Instant Picto Mastery build log.
- `work/verify-instant/` — installed Instant Picto Mastery verification.
- `work/rebuild-654321/rebuild-instant-boolfix.log` — bool-mask fix rebuild log.
- `work/verify-instant-boolfix/` — installed bool-mask verification.
- `work/rebuild-654321/rebuild-instant-nobool.log` — bool-local removal rebuild log.
- `work/verify-instant-nobool/` — installed no-bool verification.
- `work/rebuild-654321/rebuild-instant-outparam.log` — out-param call fix rebuild log.
- `work/verify-instant-outparam/` — installed out-param verification.
- `work/rebuild-654321/rebuild-instant-final.log` — canonical bytecode rebuild log (ArrayDim + call shapes).
- `work/rebuild-654321/rebuild-instant-nobool2.log` — current no-bool hook rebuild log.
- `work/rebuild-654321/rebuild-lightstain.log` — Light-stain wildcard rebuild log.
- `work/rebuild-654321/lune-heal-smoke-lightstain.log` — wildcard smoke results.
- `work/pak-backup/2026-10-27-en-before-lightstain/` — previous English pack before enabling wildcard.
- `work/rebuild-654321/rebuild-dodgespec.log` — Dodge Specialist cost rebuild log.
- `work/pak-backup/2026-10-27-en-before-dodgespec/` — previous English pack before the Dodge Specialist price change.
- `work/rebuild-654321/firstperson-smoke-camera.log` — camera smoke results.
- `work/rebuild-654321/rebuild-camera.log` — close-camera rebuild log.
- `work/pak-backup/2026-10-28-pre-camera/` — previous English pack + disabled `firstperson.json` before enabling the camera.
- `work/rebuild-654321/rebuild-conffighter.log` — Confident Fighter cost rebuild log.
- `work/rebuild-654321/pictos-smoke-conffighter.log` — picto smoke results.
- `work/pak-backup/2026-10-28-pre-conffighter/` — previous English pack before the Confident Fighter price change.
- `work/rebuild-654321/rebuild-regbuff.log` — buffed-regular-enemies rebuild log.
- `work/pak-backup/2026-10-28-pre-regbuff/` — previous English pack + unbuffed `gameplay_tweaks.json` before enabling the buff.
- `work/rebuild-654321/rebuild-altcrit.log` — Alternating Critical cost rebuild log.
- `work/rebuild-654321/pictos-smoke-altcrit.log` — picto smoke results.
- `work/pak-backup/2026-10-28-pre-altcrit/` — previous English pack before the Alternating Critical price change.
- `work/rebuild-654321/rebuild-regbuff2.log` — steepened-buff rebuild log.
- `work/rebuild-654321/balance-smoke-regbuff2.log` — balance smoke results.
- `work/pak-backup/2026-10-28-pre-regbuff2/` — previous English pack + old bonus arrays before steepening.
- `Mods for Testing/` — third-party E33 mods moved out of `~/Downloads` (BetterMimes source zip, SecondPersonCamera, Weapon Overlevelling, True Expert Dodge/Parry, Julie-over-Maelle, ExpeditionInfinity releases). `z_BetterMimes_P` trio installed to `~mods`.
- `work/verify-instant-final/` — extracted installed `BP_DataAsset_Item` and `REPORT.md` for the current hook.
- `work/pak-backup/2026-10-27-en-before-instant/` — English pack backup before enabling instant mastery.
- `work/pak-backup/2026-10-27-en-instant-broken-bool/` — broken instant-mastery pack kept for comparison.
- `work/verify-mightfix/` — installed 19:19 verification.
- `work/verify-mightfactor/` — installed 19:31 verification.
- `work/verify-white-nevrons-deployed/` — installed enemy table; hash matches 19:31 build.
- `Planning/New Game Test Batch 1.3.md` — single-pass new-game checklist.
- `work/rebuild-654321/backup/` — pre-rebuild `randomizer.json`, `gameplay_tweaks.json`, `row_damage.json`.
- `work/verify-battle-stats-old/` — pre-regression working `AC_jRPG_CharacterBattleStats`.
- `work/verify-safedamage/` — 18:36 pack; battle stats hash matches old; Meteor entry 550.
- `work/white-nevron-audit.md` — White-Nevron balance audit.
- `work/fight-stat-audit-after-fixes.md` — clone/boss stat findings.

## Still needed from the player

- Exact build/seed and the contents of `~mods`.
- x10: no longer needed; marked **PASS**. Reopen only if it regresses.
- AP: resolved — Shadow Bringer does **not** consume AP in the retest. Reopen only if it regresses.
- Meteor: **PASS** — all Lune gradients work. No longer needed.
- Damage: **PASS** — basic attack / shoot / Free Aim / skills work. No longer needed.
- Might: **PASS** — scales appropriately at 333 and on lower-level weapons. Virtuous Strike also re-confirmed in-game.
- White Nevron: new-game batch; especially Grown Bourgeon vs the normal Nevrons used to feed it.
- Row Damage: test rear-row damage in the first battle — rear three should take/deal ×0.8.
- Stalact: decide whether 68,670 HP at L25 is acceptable or should be pinned to the 22,890 reference.
- Russian: restore `work/pak-backup/2026-10-27-ru-before-en/` into `~mods` when ready to test; English is currently deployed.
- English: test UI/skill/picto/item text and the first battle on the 23:15 pack.
- Lune Light-stain wildcard: test a normal elemental skill with one required stain missing while holding Light; it should cast and consume Light. Elemental Genesis / Cataclysm should still require the exact four elements, and Mayhem should not consume Light.
- Dodge Specialist: confirm the inventory/tooltip shows a **1 Lumina** cost and the passive still gives 1 AP on a successful dodge.
- Confident Fighter: confirm tiers I–V show **5 / 10 / 20 / 50 / 100 Lumina**.
- Buffed regular enemies: judge regular-mob TTK vs White Nevrons vs bosses; elites count as regulars (buffed), real bosses are not.
- Alternating Critical: confirm tiers I–V show **5 / 10 / 15 / 20 / 25 Lumina**.
- Close camera: confirm over-the-shoulder framing in dungeon and overworld, no clipping through Verso's hair, climb stays zoomed in. Do **not** also install `ImmersiveThirdPersonCamera_P` — the built-in submodule already ships that look; two paks would fight over `BP_jRPG_Character_World`.
- Instant Picto Mastery: **PASS** — no longer needed. Reopen only if a pickup route regresses.
- Clone EXP: fight name, how many clones, whether the main body was alive, and the EXP numbers.
- Picto levels: NG+ count and a screenshot of a >33 picto.

## Update rules

1. Change the **Status** and add a row to **Test log** for every in-game check.
2. Put a date in the test log, not just in the bug row.
3. Link the evidence file or screenshot path in the **Evidence** column.
4. Keep **PASS** rows visible — they are the regression baseline.

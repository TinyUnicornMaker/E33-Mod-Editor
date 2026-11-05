# T4 / T5 Boss and Super-Boss Stat Audit

Full combat-stat audit of every **boss, super-boss and chromatic sub-boss** the
randomizer runs through the monster-stat pass: 43 T4 rows, 44 T5 rows
(43 chromatic alphas, 7 super-bosses). Shipped code is
canonical, and this file is generated from the companion workbook
`docs/boss-stat-audit.xlsx` (sheets: **Bosses**, **Areas**, **T3 Baseline**,
**Enemy Targets**) by `work/build_boss_audit_md.py`, so the two cannot drift.

Effective stat = `archetype[fight level] x randomized EnemyScaling` — the formula
`FL_EnemyScalingHelpers.ComputeCharacterStats` uses.

---

## 1. How this randomizer scales bosses

A boss row takes the **curve** when its resolved tier is 4 or 5
(`RandomizerKit.RandomizeMonsterStats`, gate `isBoss && tier >= 4`); the resolved
tier comes from `LootCatalog.AreaTierForEnemy`:

| Precedence | Rule |
|---|---|
| 1 | Act 3 Lumière rows remap to 99, Frozen Hearts rows to `level + 16` (T5 / T4) |
| 2 | a curated world-map fight tier (`worldmap_encounter_tiers.json`) |
| 3 | an explicit `AreaTierOverrides` pin |
| 4 | the area band (`TierFromLevel(area level)`, T1 1-19 … T5 80-99), raised by `AreaTierFloors` |
| 5 | **boss rows keep their own row tier** when it beats the area band — this is why the Endless Tower (a T3 area) bosses are T5 |

For curve rows the targets are absolute:

| Field | Target |
|---|---|
| HP | `HP(L) = min(900M, 11M + 889M x ((L-21)/78)^1.8)` — the same curve as the chromatic alphas |
| ATK | `atkShare x (6,000 + 54,000 x ((L-21)/78)^1.8)` |
| SPD | `spdShare x (5,000 + 10,000 x ((L-21)/78)^1.8)` |
| DEF | `defShare x DefMax(L)` — 66% of the reference weapon power, S-shaped (L60 floor → 50% at L70 → 60% at L80 → top pin at L99) with a 3× endgame stretch, at the fight's mapped weapon level (anchors 61→222, 70→250, 81→300, 99→17,318,400 top pin); staged P.DEF and M.DEF identically |
| P.DEF / M.DEF staging | neutral 1.0 archetype bases on the staged tables (vanilla 0.0), so staged scaling == effective defense |
| MAG | 0 in game (no scaling has an effect) |
| CRIT | archetype 0.1 x the randomized crit multiplier — a per-seed band roll |
| XP / Chroma | `RewardLevels(L)`: 1 level at the T4 floor (L60) → 3 at L99, flat 33,333,333 XP each |

Shares resolve from the per-row attack/defense focuses through the quadratic
pool (`atk²+spd²+def² ≤ 0.75`, floors at 0.20): balanced builds land 50/50/50,
pair focuses ≈59.6/59.6/20, solo focuses ≈81.9/20/20. Story exceptions keep
hand-authored values (Painted Renoir variants; the Act 3 finale trio runs
specialist 1.2× with the other two at 0.66× of the level-99 curve maxima — ATK
60,000, SPD 15,000, DEF 17,318,400 — so Echassier 900M/39,600/18,000/11,430,144,
Creation 1.3B/39,600/9,900/20,782,080, Renoir 2B/72,000/9,900/11,430,144;
Echassier scales to the L99 curves with HP derived at its spawn level 48 —
shares never apply to them).

Because HP/ATK/SPD/DEF are absolute targets, the effective value **is** the target —
that is why the "Target HP" (and "Target DEF") column equals HP (DEF) on every curve row.

---

## 2. The Paintress baseline (T3 comparison only)

| Boss | id | Lvl | HP | ATK | SPD | CRIT | HP × |
|---|---|---:|---:|---:|---:|---:|---:|
| The Paintress (The Monolith) | `MO_Boss_Paintress` | 43 | 33,333,333 | 5,668 | 3,622 | 12.4% | 129.6 |
| Sirene (Sirène's Coliseum) | `SI_Axon_Sirene` | 37 | 4,426,171 | 2,008 | 2,963 | 12.9% | 27.3 |
| Tisseur (Sirene) | `SI_Tisseur` | 37 | 1,621,308 | 2,163 | 2,506 | 12.0% | 10.0 |

---

## 3. T4 bosses (43 rows)

| Boss | id | Class | Lvl | **HP** | **ATK** | **SPD** | **DEF** | CRIT |
|---|---|---|---:|---:|---:|---:|---:|---:|
| Chromatic Abbest (Abbest Cave) | `SM_Abbest_Alpha` | Chromatic Alpha | 21 | **11,000,000** | **4,883** | **1,000** | **1,190** | 12.2% |
| Chromatic Orphelin - Hammer (Yellow Harvest) | `RE_OrphelinHammer_Alpha` | Chromatic Alpha | 21 | **11,000,000** | **4,260** | **2,170** | **1,308** | 13.6% |
| Chromatic Orphelin - Nail (Yellow Harvest) | `RE_OrphelinNail_Alpha` | Chromatic Alpha | 21 | **11,000,000** | **4,440** | **1,903** | **1,308** | 15.0% |
| Chromatic Orphelin - Spoon (Yellow Harvest) | `RE_OrphelinSpoon_Alpha` | Chromatic Alpha | 21 | **11,000,000** | **4,680** | **1,489** | **1,253** | 14.5% |
| Painted Renoir (Old Lumiere) | `OL_MirrorRenoir_FirstFight` | Boss | 28 | **1,570,023** | **1,548** | **2,295** | **—** | 12.8% |
| Chromatic Danseuse (Old Lumiere) | `MM_Danseuse_Alpha` | Chromatic Alpha | 36 | **56,718,897** | **5,530** | **2,946** | **2,489** | 12.3% |
| Chromatic Danseuse - Clone (Old Lumiere) | `MM_Danseuse_CloneAlpha` | Chromatic Alpha | 36 | **5,671,890** | **2,765** | **1,473** | **1,245** | 13.9% |
| Gargant (Flying Manor) | `CFH_Gargant` | Boss | 40 | **80,966,147** | **3,485** | **2,964** | **6,733** | 13.1% |
| Gargant (The Monolith) | `ML_Gargant_Gold` | Boss | 41 | **87,733,581** | **4,501** | **1,173** | **8,334** | 12.1% |
| Painted Renoir (The Monolith) | `MM_MirrorRenoir` | Boss | 50 | **3,686,280** | **11,836** | **3,884** | **—** | 13.2% |
| Grosse Tete (World Map) | `MF_GrossTete` | Elite | 55 | **210,431,395** | **7,608** | **1,461** | **13,119** | 12.1% |
| Golgra (Sacred River) | `QUEST_Golgra_SacredRiver` | Boss | 60 | **266,298,209** | **14,498** | **1,574** | **10,482** | 12.8% |
| White Benisseur (Red Woods) | `QUEST_SleepingBenisseur` | Elite | 60 | **266,298,209** | **8,173** | **4,557** | **10,781** | 14.4% |
| Benisseur (The Chosen Path) | `CP_Benisseur` | Boss | 61 | **278,201,850** | **4,891** | **6,092** | **7,457** | 13.1% |
| Chromatic Boucheclier (Isle of the Eyes) | `MF_Boucheclier_ALPHA` | Chromatic Alpha | 61 | **278,201,850** | **15,243** | **1,601** | **10,434** | 13.2% |
| Chromatic Braseleur (The Reacher) | `MM_Braseleur_ALPHA` | Chromatic Alpha | 61 | **278,201,850** | **15,037** | **1,601** | **10,705** | 12.7% |
| Chromatic Gold Chevaliere (Crimson Forest) | `YF_Chevaliere_C_Alpha` | Chromatic Alpha | 61 | **278,201,850** | **18,196** | **1,601** | **4,261** | 12.4% |
| Contorsionniste (The Chosen Path) | `CP_Contorsionniste` | Elite | 61 | **278,201,850** | **5,558** | **5,900** | **8,096** | 14.6% |
| Gargant (Frozen Hearts) | `MM_Gargant` | Boss | 61 | **278,201,850** | **13,666** | **1,601** | **12,278** | 13.4% |
| Glaise (The Chosen Path) | `CP_Glaise` | Boss | 61 | **278,201,850** | **6,225** | **5,634** | **8,948** | 13.6% |
| Jar (The Chosen Path) | `CP_Jar` | Boss | 61 | **278,201,850** | **6,891** | **5,379** | **9,587** | 12.4% |
| Stalact (The Chosen Path) | `CP_Stalact` | Elite | 61 | **278,201,850** | **7,558** | **5,088** | **10,226** | 12.2% |
| Chromatic Veilleur (Frozen Hearts) | `RE_Veilleur_Alpha` | Chromatic Alpha | 63 | **302,729,400** | **15,877** | **1,656** | **19,317** | 14.3% |
| Danseuse Teacher Fight (Frozen Hearts) | `QUEST_Danseuse_DanceClass_Kill` | Elite | 63 | **302,729,400** | **8,539** | **5,884** | **12,831** | 14.9% |
| Chromatic Catapult Sakapatate (Dark Gestral Arena) | `AS_PotatoBag_Mage_ALPHA` | Chromatic Alpha | 65 | **328,209,579** | **6,064** | **6,827** | **14,386** | 14.4% |
| Chromatic Chapelier (The Crows) | `MF_Chapelier_ALPHA` | Chromatic Alpha | 65 | **108,309,161** | **16,930** | **2,116** | **29,371** | 12.2% |
| Chromatic Glaise (Sky Island) | `YF_Glaise_ALPHA` | Chromatic Alpha | 65 | **328,209,579** | **20,683** | **1,714** | **11,988** | 13.1% |
| Chromatic Glissando (Sirene's Dress) | `SI_Glissando_Alpha` | Chromatic Alpha | 65 | **328,209,579** | **17,688** | **3,533** | **17,982** | 13.1% |
| Chromatic Goblu (World Map) | `GO_Goblu_ALPHA` | Chromatic Alpha | 65 | **328,209,579** | **12,129** | **5,080** | **24,576** | 12.6% |
| Chromatic Ranger Sakapatate (Dark Gestral Arena) | `AS_PotatoBag_Ranger_ALPHA` | Chromatic Alpha | 65 | **328,209,579** | **7,075** | **6,630** | **16,184** | 12.1% |
| Chromatic Robust Sakapatate (Dark Gestral Arena) | `AS_PotatoBag_Tank_ALPHA` | Chromatic Alpha | 65 | **328,209,579** | **6,317** | **5,667** | **29,971** | 12.2% |
| Sprong (World Map) | `WM_Sprong` | Boss | 65 | **328,209,579** | **16,166** | **1,714** | **32,870** | 13.6% |
| Alicia (The Reacher) | `RC_Alicia` | Boss | 69 | **381,992,990** | **19,148** | **1,835** | **65,595** | 13.5% |
| Chromatic Benisseur (World Map) | `FB_Benisseur_Alpha` | Chromatic Alpha | 71 | **410,279,933** | **13,614** | **6,026** | **71,372** | 14.9% |
| Chromatic Cruler (Endless Night Sanctuary) | `GO_Cruler_ALPHA` | Chromatic Alpha | 73 | **439,486,846** | **12,811** | **3,105** | **196,193** | 12.2% |
| Ultimate Sakapatate (Endless Night Sanctuary) | `TS_PotatoBag_Boss_Twilight` | Boss | 73 | **439,486,846** | **14,006** | **1,964** | **201,871** | 14.3% |
| Golgra (Dark Gestral Arena) | `QUEST_Golgra_DarkArena` | Boss | 75 | **469,606,540** | **22,935** | **2,032** | **206,395** | 14.6% |
| Serpenphare (World Map) | `WM_Serpenphare` | Boss | 75 | **469,606,540** | **21,729** | **2,032** | **224,886** | 15.0% |
| Chromatic Eveque - Earth (Endless Tower) | `SM_Boss_Eveque_Earth_ALPHA` | Chromatic Alpha | 79 | **532,557,103** | **20,348** | **5,399** | **395,457** | 14.8% |
| Chromatic Luster (Endless Tower) | `GO_Luster_Alpha_CleasTower` | Chromatic Alpha | 79 | **532,557,103** | **19,217** | **5,915** | **378,263** | 12.5% |
| Flame Eveque (Endless Tower) | `SM_Boss_Eveque_Fire_ALPHA_CleaTower` | Chromatic Alpha | 79 | **532,557,103** | **21,478** | **4,674** | **421,247** | 12.0% |
| Frost Eveque (Endless Tower) | `SM_Boss_Eveque_Ice_ALPHA_CleaTower` | Chromatic Alpha | 79 | **532,557,103** | **22,608** | **3,758** | **447,038** | 14.9% |
| Thunder Eveque (Endless Tower) | `SM_Boss_Eveque_Lightning_ALPHA` | Chromatic Alpha | 79 | **532,557,103** | **24,080** | **2,173** | **472,131** | 13.6% |

---

## 4. T5 bosses and super-bosses (44 rows)

| Boss | id | Class | Lvl | **HP** | **ATK** | **SPD** | **DEF** | CRIT |
|---|---|---|---:|---:|---:|---:|---:|---:|
| Blanche (The Fountain) | `QUEST_JudgeOfMercy` | Chromatic Alpha | 80 | **548,854,860** | **31,088** | **2,210** | **259,371** | 16.1% |
| White Portier (Esoteric Ruins) | `Portier_DoorSoul` | Elite | 80 | **548,854,860** | **16,396** | **2,210** | **748,291** | 18.0% |
| Chromatic Petank (World Map) | `WM_Petank_Alpha` | Chromatic Alpha | 81 | **565,375,114** | **32,475** | **2,247** | **245,179** | 17.5% |
| Dualliste (Flying Manor) | `CFH_Dualliste` | Boss | 83 | **599,080,146** | **24,198** | **5,755** | **606,464** | 15.3% |
| Flame Eveque (Flying Manor) | `CFH_Boss_Eveque_Fire_ALPHA` | Chromatic Alpha | 83 | **599,080,146** | **14,602** | **8,516** | **443,754** | 17.3% |
| Frost Eveque (Flying Manor) | `CFH_Boss_Eveque_Ice_ALPHA` | Chromatic Alpha | 83 | **599,080,146** | **15,854** | **8,186** | **488,130** | 15.5% |
| Frost Eveque (World Map) | `SM_Boss_Eveque_Ice_ALPHA` | Chromatic Alpha | 83 | **599,080,146** | **30,039** | **3,108** | **591,672** | 15.4% |
| Goblu (Flying Manor) | `CFH_Goblu` | Boss | 83 | **599,080,146** | **15,020** | **5,333** | **946,676** | 17.5% |
| Grosse Tete (Flying Manor) | `CFH_GrossTete` | Elite | 83 | **599,080,146** | **8,344** | **2,323** | **1,210,762** | 15.8% |
| Lampmaster (Flying Manor) | `CFH_LampMaster` | Boss | 83 | **599,080,146** | **15,854** | **4,598** | **991,051** | 15.7% |
| Thunder Eveque (Flying Manor) | `CFH_Boss_Eveque_Lightning_ALPHA` | Chromatic Alpha | 83 | **599,080,146** | **17,106** | **7,873** | **517,713** | 17.0% |
| Chromatic Barbasucette (DLC Enemy) | `VD_Barbasucette_Alpha` | Chromatic Alpha | 86 | **651,288,229** | **18,406** | **6,450** | **1,066,638** | 16.6% |
| Chromatic Franctale (DLC Enemy) | `VD_Franctale_Alpha` | Chromatic Alpha | 86 | **651,288,229** | **19,753** | **5,723** | **1,124,818** | 15.3% |
| Chromatic Licorne (DLC Enemy) | `VD_Licorne_Alpha` | Chromatic Alpha | 86 | **651,288,229** | **21,100** | **4,642** | **1,202,392** | 16.1% |
| Chromatic Machinapieds (DLC Enemy) | `VD_Machinapieds_Alpha` | Chromatic Alpha | 86 | **651,288,229** | **22,446** | **3,397** | **1,260,572** | 16.6% |
| Lampmaster (Painting Workshop) | `CW_LampMasterAlpha` | Boss | 86 | **651,288,229** | **33,669** | **3,560** | **620,589** | 17.6% |
| Licornapieds (DLC Enemy) | `VD_Licornapieds` | Boss | 86 | **651,288,229** | **17,957** | **8,145** | **736,950** | 16.5% |
| Simon - Phase 1 (The Abyss) | `Boss_Simon` | Boss | 86 | **651,288,229** | **12,121** | **7,681** | **1,027,851** | 17.3% |
| Simon - Phase 2 (The Abyss) | `Boss_Simon_Phase2` | Boss | 86 | **651,288,229** | **13,019** | **7,243** | **1,086,031** | 15.5% |
| Chromatic Creation (Renoir's Drafts) | `RD_Creation_Alpha` | Chromatic Alpha | 91 | **742,657,701** | **40,920** | **2,646** | **678,621** | 16.5% |
| Creation (Renoir's Drafts) | `RD_Creation` | Elite | 91 | **742,657,701** | **33,903** | **2,646** | **1,513,436** | 17.6% |
| Mime_Bald (Sunless Cliffs) | `WM_Mime_Bald` | Chromatic Alpha | 92 | **761,579,163** | **10,318** | **11,004** | **647,138** | 17.7% |
| Chromatic Ceramic Chevaliere (Endless Tower) | `YF_Chevaliere_A_Alpha` | Chromatic Alpha | 99 | **900,000,000** | **49,112** | **3,000** | **1,154,560** | 16.8% |
| Chromatic Demineur (Endless Tower) | `GO_Demineur_ALPHA` | Chromatic Alpha | 99 | **297,000,000** | **33,000** | **9,113** | **1,616,384** | 15.6% |
| Chromatic Echassier (Lumiere - Act 3) | `LU_EchassierAlpha` | Chromatic Alpha | 99 | **900,000,000** | **39,600** | **18,000** | **11,430,144** | 16.8% |
| Chromatic Jar (Endless Tower) | `YF_Jar_ALPHA` | Chromatic Alpha | 99 | **900,000,000** | **26,363** | **3,000** | **4,150,574** | 17.9% |
| Chromatic Lampmaster (DLC Enemy) | `Boss_LampMaster_ALPHA` | Super-Boss | 99 | **900,000,000** | **42,000** | **5,562** | **2,020,480** | 17.9% |
| Chromatic Moissonneuse (Endless Tower) | `MF_Moissonneuse_ALPHA` | Chromatic Alpha | 99 | **900,000,000** | **35,400** | **8,604** | **1,558,656** | 15.8% |
| Chromatic Portier (Endless Tower) | `SM_Portier_ALPHA` | Chromatic Alpha | 99 | **900,000,000** | **26,168** | **3,000** | **4,161,963** | 16.8% |
| Chromatic Steel Chevaliere (Endless Tower) | `YF_Chevaliere_B_Alpha` | Chromatic Alpha | 99 | **900,000,000** | **49,112** | **3,000** | **1,154,560** | 16.2% |
| Clea (Flying Manor) | `CFH_Boss_Clea` | Boss | 99 | **900,000,000** | **18,600** | **8,295** | **3,405,952** | 15.7% |
| Clea Unleashed (DLC Enemy) | `Boss_Clea_ALPHA` | Super-Boss | 99 | **900,000,000** | **33,000** | **8,258** | **2,193,664** | 16.5% |
| Creation (Lumiere - Act 3) | `L_Creation` | Elite | 99 | **1,300,000,000** | **39,600** | **9,900** | **20,782,080** | 50.0% |
| Dualliste (Endless Tower) | `FB_Dualliste_CleasTower` | Boss | 99 | **900,000,000** | **47,962** | **3,000** | **1,538,210** | 16.6% |
| Duollistes - Dark (DLC Enemy) | `Duolliste_A` | Super-Boss | 99 | **900,000,000** | **46,800** | **3,200** | **1,789,568** | 15.3% |
| Duollistes - Light (DLC Enemy) | `Duolliste_B` | Super-Boss | 99 | **900,000,000** | **47,410** | **3,000** | **1,689,423** | 17.0% |
| Duollistes - P2 (DLC Enemy) | `Duolliste_P2` | Super-Boss | 99 | **900,000,000** | **49,112** | **3,000** | **1,154,560** | 16.8% |
| Lampmaster (Endless Tower) | `SC_LampMaster_CleasTower` | Boss | 99 | **900,000,000** | **48,920** | **3,000** | **1,227,843** | 15.1% |
| Mask Keeper 2 (Endless Tower) | `CT_MaskKeeper_NoMask_CleaTwoer` | Boss | 99 | **900,000,000** | **38,400** | **5,383** | **2,655,488** | 15.9% |
| Osquio (DLC Enemy) | `VD_Osquio` | Super-Boss | 99 | **900,000,000** | **26,303** | **3,000** | **4,154,111** | 16.3% |
| Painted Love - Curator (Endless Tower) | `CT_Boss_Curator_CleaTower` | Boss | 99 | **900,000,000** | **36,600** | **6,590** | **2,482,304** | 16.8% |
| Painted Love - Paintress (Endless Tower) | `CT_Boss_Paintress_CleaTower` | Boss | 99 | **900,000,000** | **43,800** | **4,779** | **1,962,752** | 17.2% |
| Renoir (Lumiere - Act 3) | `L_Boss_Curator` | Boss | 99 | **2,000,000,000** | **72,000** | **9,900** | **11,430,144** | 50.0% |
| Simon, the Divergent Star (DLC Enemy) | `Boss_Simon_ALPHA` | Super-Boss | 99 | **900,000,000** | **19,200** | **11,139** | **1,789,568** | 16.6% |

---

## 5. Chromatic alphas (43 rows)

43 of the 87 audited rows are `Alpha` archetype rows, spread across
both tiers (24 T4 / 19 T5); they are
marked **Chromatic Alpha** in the tables above. Alphas are scaled wherever they are
fought, not where the enemy model lives: three of them (the Visages island's chromatic
bosses) only reached the curve in this pass — see §7.

---

## 6. Areas (24 rows)

One row per area that matters to the T4/T5 bands, ordered by tier then level. The
mod's shipped tier is authoritative; the game's own LevelScaling is kept beside it
and "Tier basis" says where the tier comes from. Merged from the old T4 Areas /
T5 Areas / Location Regions / Region Scaling Tokens sheets.

| # | Area | Tier | Mod level | Game scaling | Game tier | AreaKey | Tier basis |
|---:|---|---:|---:|---:|---:|---|---|
| 1 | Crimson Forest | T4 | 60 | 52 | T3 | `crimsonforest` | Chest mapping (lifted) |
| 2 | Red Woods | T4 | 60 | 60 | T4 | `smalllevelredwoods` | Chest mapping |
| 3 | Sacred River | T4 | 60 | 60 | T4 | `smalllevelsacredriver` | Chest mapping |
| 4 | Sky Island | T4 | 60 | 60 | T4 | `smalllevelfloatingisland` | Chest mapping |
| 5 | The Chosen Path | T4 | 60 | 60 | T4 | `smalllevelchosenpath` | Chest mapping |
| 6 | The Reacher | T4 | 60 | 58 | T3 | `reacher / smalllevelreacher` | Chest mapping 60 + AreaTierFloors["reacher"] = 4 |
| 7 | Isle of the Eyes | T4 | 65 | 65 | T4 | `smalllevelvisages` | Chest mapping |
| 8 | The Crows | T4 | 65 | 65 | T4 | `smalllevelrocktrailing` | Chest mapping |
| 9 | Dark Shores | T4 | 68 | 68 | T4 | `darkshores` | Chest mapping |
| 10 | Endless Night Sanctuary | T4 | 73 | 73 | T4 | `twilightsanctuary` | Chest mapping |
| 11 | Dark Gestral Arena | T4 | 75 | 75 | T4 | `darkgestralarena` | Chest mapping (no chest rows) |
| 12 | Flying Cemetery | T4 | — | — | — | `forgottenbattlefield (FC_ rows only)` | AreaTierOverrides (FC_ rows = 4) |
| 13 | Frozen Hearts | T4 | 79 | 63 | T4 | `frozenhearts` | Chest mapping (lifted) |
| 14 | The Fountain | T5 | — | 80 | T5 | `darkshores (enemy row only)` | Row tier (Alpha L80) |
| 15 | Flying Manor | T5 | 83 | 83 | T5 | `flyingmanor` | Chest mapping |
| 16 | Painting Workshop | T5 | — | 90 | T5 | `cleaworkshop (no bucket; enemy rows)` | Row tier (Boss L86) |
| 17 | The Abyss | T5 | — | 99 | T5 | `simonarea (no bucket; enemy rows)` | Row tier (Boss L86) |
| 18 | Verso's Drafts | T5 | 86 | 86 | T5 | `sidelevelversosdraft` | Chest mapping |
| 19 | Sunless Cliffs | T5 | — | 92 | T5 | `sunlesscliffs (no bucket; enemy row)` | Row tier (Alpha L92) |
| 20 | Renoir's Drafts | T5 | 93 | 93 | T5 | `renoirsdraft` | Chest mapping |
| 21 | Lumière — Act 3 | T5 | 99 | 48 | T3 | `lumiere` | Remap (IsAct3Lumiere → 99) |
| 22 | Root of All Evil | T5 | 99 | 99 | T5 | `esquierealcousin` | Location |
| 23 | Endless Tower | T3 | 55 | 55 | T3 | `sidelevelcleastower` | Chest mapping |
| 24 | Sirene's Dress | T3 | — | 60 | T4 | `sirenesmalllevel (no bucket)` | Region token only (no mod bucket) |

---

## 7. What changed in this pass

- **The Visages island's chromatic bosses now take the curve.** `MF_Boucheclier_ALPHA`
  (Isle of the Eyes, L61), `MF_Chapelier_ALPHA` (The Crows, L65) and
  `MF_Moissonneuse_ALPHA` (Endless Tower, L99) were dropped from the blanket
  `MF_*` T3 pin that exists for the T3 island, so they resolve at T4/T4/T5 and land
  exactly on their level-mates' curve values (278.2M / 328.2M / 900M HP). The
  island's regular mobs stay pinned T3.
- **The Crimson Forest is a T4 area**: its chests and its two Chevaliere rows were
  lifted to the 60-79 band.
- **Flying curve rows stage a third of the curve HP** (`IsFlying` flag × 0.33):
  `GO_Demineur_ALPHA` (297M instead of 900M) and `MF_Chapelier_ALPHA` (108.3M
  instead of 328.2M). They dodge most base attacks, so a full pool would last
  far longer than its number. Loot, XP and ATK/SPD/DEF are untouched.
- **Chromatic Glissando takes the T4 curve.** `SI_Glissando_Alpha` was unpinned
  from the T3 Sirene-arena `SI_` list: fought in Sirene's Dress at L65, it keeps
  its T4 row tier (boss rows beat the area band) and lands on the curve
  (328.2M HP). The arena's regular rows stay pinned T3.
- **Alicia fights at L69.** `RC_Alicia` (The Reacher, still T4) moved 61→69:
  382.0M HP / 19,148 ATK / 1,835 SPD / 65,595 DEF.
- Their CRIT and XP figures are per-seed band rolls and are intentionally blank in
  the workbook.

## 8. Warnings

- **P.DEF / M.DEF / MAG are 0 for every enemy**, named bosses included: the archetype
  base is 0 at every level on Normal/Hard/Easy and the runtime multiplies the two.
  The randomizer's defence bands therefore change only the scaling number.
- **The Endless Tower is a T3 area with T5 boss rows** — boss rows keep their row tier.
  Most of the T5 list above is fought there.
- An enemy's loot tier and its stat tier come from the same `AreaTierForEnemy`
  call, so lifting a boss changes both its drops and its stats.

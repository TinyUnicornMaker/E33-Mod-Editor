# Maelle Overhaul Spec — Vanilla Discovery + Design

Status: **discovery complete + implementation staged** (kit builds, packs, and all edited assets pass `kismet-check`; pending in-game test). All vanilla values verified from extracted assets (DAs, balancing table, skill-script kismet, skill graph, string table).

Implementation: `src/E33ModEditor.Core/MaelleSkillsKit.cs`, gated by the Skill Overhaul `Character.Maelle` switch (`work/skill_overhaul.json`, absent = on; legacy `work/maelle_overhaul.json` is inert), wired into `rebuild-mod` / the standalone generator after `StanceEffectsKit`. Stance values: `CombatBalanceKit` (Virtuose dealt only — the other rows equal vanilla) + `StanceEffectsKit`.

---

## 0. Baseline references (verified from assets)

### Gradient gauge (BP_GradientGaugeSystem CDO)
- `MaxCharges` = **10**, `ProgressPerAPSpent` = 0.05 (1 AP = +5% progress = 1/20 charge).
- Charge APIs: `ChangeGradientGauge(float)`, `ConsumeCharge()`, `GetAvailableCharges()`.
- Gradient skill charge cost = DA `APCost` (mirrored in graph node `UnlockCost`). **Fractional progress is supported** (Phantom Strike reads a float `GradientChargeAmount` and calls `ChangeGradientGauge`).

### Damage curve (Lune rule, reused)
`clamp(ap) = 0.33·ap` (≤9 AP), then `3·e^(0.24·(ap−9))`. **Maelle base damage = 0.75 × clamp(AP)** unless a move is exempt.

### EAttackType bytes
Physical=1, Fire=2, Ice=3, Lightning=4, Earth=5, Dark=6, Light=7, **Void=8**.

### Act-gated / secret nodes (leave as-is)
| Move | RowName | Notes |
|---|---|---|
| Gustave's Homage (`GustaveSMemoire`) | `Quest_MaelleGustaveUnlock` | secret, starting=true (quest-opened) |
| Phantom Strike | `Quest_MaellePainterSkillsUnlock` | secret |
| Burning Canvas | `Quest_MaellePainterSkillsUnlock` | secret |
| Stendhal | `Quest_MaellePainterSkillsUnlock` | secret |
| Phoenix Flame | `GradientUnlock_Maelle2` | secret gradient |
| Gommage | `GradientUnlock_Maelle3` | secret gradient |
| Virtuose Strike | `GradientUnlock_Maelle1` | secret gradient |

The three "painter" Void moves share one quest gate; the user controls progression by routing links through them. **Do not un-gate or re-gate anything.**

---

## 1. Per-move vanilla inventory (DA + balancing + graph)

Legend: AP = DA `APCost`; element from `BaseElementalType` (enumerator) or `DynamicElementOverride` (= weapon); hit lines from the description.

| Display name | DA stem | SkillType | AP (DA) | Element | Balancing rows (vanilla) | Graph node |
|---|---|---|---|---|---|---|
| Offensive Switch | `DA_Skill_Maelle_OffensiveSwitch` | normal | 1 | weapon | `OffensiveSwitch_BaseDamage` 1.0, Break 1.0 | node0 starter, (425,425) |
| Spark | `DA_Skill_Maelle_NEW18_Spark` | normal | 3 | Fire | `Spark_BaseDamage` 1.0, `Spark_BaseBurnStacks` 3, `Spark_OverchargeBurnStacks` 5, `Spark_BurnMultiplier` 1.0, Break 1.0 | node1 (425,525) |
| Fleuret Fury | `DA_Skill_Maelle_FleuretFury` | normal | 6 | Physical | `FleuretFury_BaseDamage` 0.8 (3 hits), Break 0.5 | node2 (800,625) |
| Breaking Rules | `DA_Skill_Maelle_BreakingRules` | normal | 3 | Physical | `BreakingRules_BaseDamage` 0.75 (2 hits), `BreakingRules_APGainPerShield` 1.0, Break 0.7 | node3 (925,625) |
| Swift Stride | `DA_Skill_Maelle_NEW4_FireRise` | normal | 3 | Physical | `FireRise_BaseDamage` 1.0, Break 1.0 | node4 (675,525) |
| Égide | `DA_Skill_Maelle_NEW7_Egide` | normal | 3 | — | `Egide_TurnDuration` 2.0 | node5 (675,725) |
| Guard Up | `DA_Skill_Maelle_GuardUp` | normal | 3 | — | `GuardUp_ShellDuration` 3.0 | node6 (675,625) |
| Rain of Fire | `DA_Skill_Maelle_NEW9_RainOfFire` | normal | 5 | Fire | `RainOfFire_BaseDamage` 0.8 (2 hits), `RainOfFire_BaseBurnPerHit` 3.0, `RainOfFire_OverchargeBurnPerHit` 5.0, Break 0.7 | node7 (300,625) |
| Last Chance | `DA_Skill_Maelle_LastChance` | normal | 1 | — | `LastChance_APGain` 9.0, `LastChance_NewHpAmount` 1.0 | node8 (425,825) |
| Revenge | `DA_Skill_Maelle_Revenge` | normal | 5 | Fire | `Revenge_BaseDamage` 1.2, `Revenge_DamageIncreasePerHit` 1.5, Break 1.0 | node9 (175,725) |
| Payback | `DA_Skill_Maelle_NEW13_Payback` | normal | 9 | Physical | `Payback_BaseDamage` 5.0, Break 1.0 | node10 (550,825) |
| Percée | `DA_Skill_Maelle_NEW14_Percee` | normal | 5 | Physical | `Percee_BaseDamage` 2.2, `Percee_MarkedDamageMultiplier` 2.0, Break 1.0 | node11 starter, (675,425) |
| Gustave's Homage | `DA_Skill_Maelle_GustaveSMemoire` | normal | 8 | Lightning | `GustaveSMemoire_BaseDamage` 0.5 (8 hits), `GustaveSMemoire_LastHitMultiplier` 2.0, `GustaveSMemoire_MarkedDamageMultiplier` 1.5, Break 0.1 | node12 secret, (850,475) |
| Combustion | `DA_Skill_Maelle_NEW19_InvigoratingFire` | normal | 4 | Physical | `InvigoratingFire_BaseDamage` 0.8 (2 hits), `InvigoratingFire_DamageIncreasePerBurn` 0.4, Break 0.7 | node13 (175,625) |
| Guard Down | `DA_Skill_Maelle_GuardDown` | normal | 3 | — | `GuardDown_DefenslessDuration` 3.0 | node14 (550,625) |
| Sword Ballet | `DA_Skill_Maelle_SwordBallet` | normal | 9 | weapon | `SwordBallet_BaseDamage` 0.75 (5 hits), `SwordBallet_CriticalDamageMultiplier` 2.0, Break 0.3 | node15 (925,825) |
| Momentum Strike | `DA_Skill_Maelle_MomentumStrike` | normal | 7 | weapon | `MomentumStrike_BaseDamage` 3.5, `MomentumStrike_MarkedDamageMultiplier` 2.0, Break 1.0 | node16 (675,825) |
| Degagement | `DA_Skill_Maelle_Degagement` | normal | 2 | Fire | `Degagement_BaseDamage` 1.0, `Degagement_WeaknessDuration` 2.0, Break 1.0 | node17 (425,625) |
| Pyrolyse | `DA_Skill_Maelle_Pyrolyse` | normal | 9 | Fire | `Pyrolyse_BaseDamage` 1.667 (3 hits), `Pyrolyse_BurnPerHit` 5.0, `Pyrolyse_BurnOnOvercharge` 2.0, Break 0.5 | node18 (175,825) |
| Phoenix Flame | `DA_Skill_Maelle_PhoenixFlame` | gradient | 2 | — | `PhoenixFlame_BurnAmount` 10.0, `PhoenixFlame_BaseRevivePercentHealth` 0.5, `PhoenixFlame_RevivePercentHealthPerQTE` 0.1 | node19 gradient secret |
| Phantom Strike | `DA_Skill_Maelle_PhantomStrike` | normal | 7 | Void | `PhantomStrike_BaseDamage` 0.7 (4 hits), `PhantomStrike_GradientChargeAmount` 0.35, Break 0.4 | node20 secret |
| Burning Canvas | `DA_Skill_Maelle_BurningCanvas` | normal | 5 | Void | `BurningCanvas_BaseDamage` 0.4 (5 hits), `BurningCanvas_BurnAppliedPerHit` 1.0, `BurningCanvas_DamageIncreasePerBurnStack` 0.1, Break 0.3 | node21 secret |
| Stendhal | `DA_Skill_Maelle_Stendhal` | normal | 8 | Void | `Stendhal_BaseDamage` 15.0, Break 1.0 | node22 secret |
| Gommage | `DA_Skill_Maelle_Gommage` | gradient | 3 | Void | `Gommage_BaseDamage` 50.0, Break 1.0 | node23 gradient secret |
| Virtuose Strike | `DA_Skill_Maelle_VirtuoseStrike` | gradient | 1 | Physical | `VirtuoseStrike_BaseDamage` 0.3 (5 hits), Break 0.3 | node24 gradient secret |
| Fencer's Flurry | `DA_Skill_Maelle_FencersFlurry` | normal | 4 | weapon | `FencersFlurry_BaseDamage` 1.4→0.99 (= 0.75×clamp(4), 1 hit AoE), `FencersFlurry_DefenselessDuration` 1.0, Break 1.0 | node25 (?) |
| Mezzo Forte | `DA_Skill_Maelle_NEW11_MezzoForte` | normal | 1 | — | `MezzoForte_BaseAPGain` 2.0, `MezzoForte_APGainPerQTE` 1.0 | node26 |
| We Are Me | `DA_Skill_Maelle_WeAreMe` | normal | — | Void | `WeAreMe_BaseDamage` 4.2 (2 hits) | not on graph (cut content) |

**DynamicElementOverride** (weapon-element moves — cannot take a fixed element byte without crashing): FencersFlurry, MomentumStrike, OffensiveSwitch, SwordBallet. Also all {DynamicElement} descriptions. **Leave their element as weapon.**

---

## 2. Mechanics findings (answers to the design questions)

### 2a. Combustion burn-consume (InvigoratingFire)
Kismet (`OnActionEffect`): reads `InstVar BurnToConsume` (an instance value set elsewhere, capped ~10 by the "Consumes up to 10 Burn" text), calls `TryRemoveBuffStacks(BP_BattleBuff_Burn_C, BurnToConsume)` per target, then damage =
`BaseDamage × (1 + BurnToConsume × InvigoratingFire_DamageIncreasePerBurn) × QteDamageMultiplier`.
- **Linear** per consumed stack, not exponential.
- **Fully controllable** via `InvigoratingFire_DamageIncreasePerBurn` (vanilla 0.4 = +40% per stack).
- For **Thermal Arc** (Lightning): same consume structure, retheme element; keep or tune `DamageIncreasePerBurn`.

### 2b. Burning Canvas burn scaling
Kismet: per target, `GetActiveBuffCounts(BP_BattleBuff_Burn_C)` (clamped 0..100) → `AddFinalDamageMultiplier(BurningCanvas_DamageIncreasePerBurnStack × burnCount)` → `DealDamages`.
- **Linear** with current burn stacks; **does not consume** (it reads the stack, leaves it).
- Row-controllable: `BurningCanvas_DamageIncreasePerBurnStack` 0.1 = +10% per stack.
- For **11 AP**: base rescale to 0.4×clamp(11) → see §3.

### 2c. Marked-target damage (Percée / Momentum / Gustave's Homage)
Kismet: `HasBuffOfClass(BP_BattleBuff_Marked_C)` → applies `*_MarkedDamageMultiplier` (row).
- Percée: `Percee_MarkedDamageMultiplier` 2.0.
- Momentum: `MomentumStrike_MarkedDamageMultiplier` 2.0.
- Homage: `GustaveSMemoire_MarkedDamageMultiplier` 1.5; also `SetIgnoreMarkedRemoval(7)` (8-hit move doesn't remove mark until the 7th hit action).
- **Rate is a controllable row** per move. User target: ×1.5 for Percée and Momentum (was ×2.0).
- **Virtuose ×1.5 note:** Percée/Momentum descriptions say "Virtuose Stance: Costs 2/4 AP" — the stance interaction is an **AP cost reduction**, not a damage multiplier. The user's "Virtuous ×1.5 damage" needs interpretation (see §4 Open questions).

### 2d. Revenge scaling
Kismet: `BP_BattleBuffInstance_HitCountSinceSincePreviousTurn_C` accumulates hits received since prior turn; `HitCountDamageMultiplier = 1 + Revenge_DamageIncreasePerHit × HitCountSincePreviousTurn`. Damage = `Revenge_BaseDamage × HitCountDamageMultiplier`.
- **Flat/linear** per hit received (each hit = +150% at vanilla `DamageIncreasePerHit` 1.5). Not exponential.
- Row-controllable. No per-level curve.

### 2e. Degagement fire weakness
Kismet: deals Fire, then `ApplyBuff(BP_BattleBuffInstance_Degagement_C, target, Degagement_WeaknessDuration)`; switches Offensive.
- The applied buff (`BP_BattleBuffInstance_Degagement_C`) **inherits `BP_BattleBuffInstance_SetTargetAffinity`** and overrides the target's Fire elemental affinity:
  - `ElementToModifyAffinity` = `EAttackType::NewEnumerator0` (Fire)
  - `NewAffinityValue` = `EElementalAffinity::NewEnumerator1`
  - On activate: `GetElementalAffinity(Fire)` → `AddElementalAffinityOverride(Fire, NewAffinityValue, TurnCount, TurnDuration)`.
- So Degagement **sets the target's Fire elemental affinity** to `NewEnumerator1` for N turns — it does **not** add a separate stacking +% multiplier. It is the **same mechanic** as the enemy's innate elemental weakness (an `EElementalAffinity` override).
- Consequence: vs an enemy already weak to Fire it is a no-op (same value, refreshed); vs a Fire-resistant enemy it overrides resistance to `NewEnumerator1` for the duration. It does **not** stack multiplicatively with existing weakness.
- Duration row: `Degagement_WeaknessDuration` 2.0 (user wants 5 turns → set 5.0).
- The actual damage-% of affinity `NewEnumerator1` is in the game's damage/affinity code, not in the buff BP — not user-tunable here, but consistent with how all elemental weakness works in the game.

### 2f. Phantom Strike gradient charge
Kismet: `ChangeGradientGauge(GradientGaugeSystem, PhantomStrike_GradientChargeAmount)` where the row is a **float fraction of one charge** (0.35 = +35% of a charge; gauge MaxCharges=10).
- User wants **50%** → set `PhantomStrike_GradientChargeAmount` 0.5.
- AP 7 → 12 (act-gated Void move, higher cost/effect).

### 2g. Phoenix Flame revive
Kismet: applies `PhoenixFlame_BurnAmount` Burn to all enemies via `ChangeStackCount`; then iterates `DeadPlayerCharacters`, `Resurrect` with HP = `GetMaxHP × (BaseRevivePercentHealth + RevivePercentHealthPerQTE × QTE)`.
- User wants: 5 charges, revive **100%** HP, **100** Burn.
- Set `PhoenixFlame_BaseRevivePercentHealth` 1.0, `PhoenixFlame_RevivePercentHealthPerQTE` 0.0 (or keep a small QTE bonus), `PhoenixFlame_BurnAmount` 100. DA `APCost` 2 → 5.

### 2h. Last Chance → Death's Bargain
Vanilla kismet (`OnActionEffect`): switch on `CurrentActionEffectNumber` 0/1 → `SetHP(LastChance_NewHpAmount=1)`, `GainAP(LastChance_APGain=9)`, `SwitchStance(Virtuose)`. **No self-Curse exists** — must be added (ApplyBuff `BP_BattleBuff_Cursed_C` on self) for Death's Bargain.
- User wants 8 AP (DA). Rename + self-Curse + description. AP refill value `LastChance_APGain` 9.0 likely = full AP (9 is max) — keep as refills-all.

---

## 3. Target design (0.4×clamp base rule)

`clamp(AP)` and `0.4×clamp(AP)` (verified against `SkillOverhaulKit.Clamp`):

| AP | clamp | 0.4×clamp |
|---|---|---|
| 1 | 0.33 | 0.132 |
| 2 | 0.66 | 0.264 |
| 3 | 0.99 | 0.396 |
| 4 | 1.32 | 0.528 |
| 5 | 1.65 | 0.66 |
| 6 | 1.98 | 0.792 |
| 7 | 2.31 | 0.924 |
| 8 | 2.64 | 1.056 |
| 9 | 2.97 | 1.188 |
| 11 | 4.848 | 1.939 |
| 12 | 6.163 | 2.465 |
| 13 | 7.835 | 3.134 |
| 15 | 12.662 | 5.065 |

Base row = `0.4×clamp ÷ hits` where the row is per-hit (all damage rows are per-hit).

Per-move targets (base rows), assuming the user's AP:
- **Offensive Switch**: 1 AP — already; Defenceless 3→1 turn (vanilla description says 3).
- **Thermal Arc (Combustion/InvigoratingFire)**: 4 AP Lightning → `InvigoratingFire_BaseDamage` 0.8 → 0.4×1.32=0.528 ÷ 2 hits = 0.264; keep consume-burn, cap 10→50.
- **Electric Fury (Fleuret Fury)**: 9 AP Lightning → `FleuretFury_BaseDamage` 0.8→1.188 ÷ 3 hits = 0.396.
- **Breaking Rules**: 5 AP Lightning → `BreakingRules_BaseDamage` 0.75→0.66 ÷ 2 hits = 0.33.
- **Payback**: 5 AP Lightning → `Payback_BaseDamage` 5.0→0.66.
- **Gustave's Homage**: 13 AP → `GustaveSMemoire_BaseDamage` 0.5→3.134 ÷ 8 hits = 0.392 (plus LastHit ×2 and Marked ×1.5 kept).
- **Percée**: 1 AP Fire → `Percee_BaseDamage` 2.2→0.132; Marked ×2→×1.5.
- **Momentum Strike**: 7 AP → `MomentumStrike_BaseDamage` 3.5→0.924; Marked ×2→×1.5.
- **Flame Eater (Swift Stride/FireRise)**: 3 AP Fire → `FireRise_BaseDamage` 1.0→0.396; remove AP regain, keep burning→Virtuose switch.
- **Pyrolyse**: rescale at 9 AP → `Pyrolyse_BaseDamage` 1.667→1.188 ÷ 3 hits = 0.396.
- **Sword Ballet**: 9 AP weapon → `SwordBallet_BaseDamage` 0.75→1.188 ÷ 5 hits = 0.2376.
- **Revenge**: 5 AP → `Revenge_BaseDamage` 1.2→0.66; DamageIncreasePerHit 1.5→0.5 (+50%/hit).
- **Rain of Fire**: **rebalanced** onto the 5 AP curve, and description restyled. Its damage does **not** come from `RainOfFire_BaseDamage` — the skill script never reads that row (dead data, vanilla left it at 0.8) and hardcodes `Local_DamageMultiplier` instead, so `PatchRainOfFire` rewrites that DoubleConst **1.25 → 0.33** (= 0.4×clamp(5) ÷ 2). Description quotes the same `RainOfFirePerHit` constant, so the text and the script cannot drift. Single target (`TargetingType` `NewEnumerator1`).
- **Spark**: 2 AP → `Spark_BaseDamage` 1.0→0.264; keep burn.
- **Fencer's Flurry**: rebalanced onto the Maelle curve (4 AP AoE → `FencersFlurry_BaseDamage` 1.4→0.99) with a numeric vanilla-voice description; Defenceless 1 turn kept.
- **Phantom Strike**: 12 AP Void → `PhantomStrike_BaseDamage` 0.7→2.465 ÷ 4 hits = 0.616; `GradientChargeAmount` 0.35→0.5.
- **Burning Canvas**: 11 AP Void → `BurningCanvas_BaseDamage` 0.4→1.939 ÷ 5 hits = 0.388; per-burn scaling kept.
- **Stendhal**: 15 AP → `Stendhal_BaseDamage` 15→5.065 (rescale).
- **Death's Bargain (Last Chance)**: 8 AP → add Curse; `LastChance_APGain`/`NewHpAmount` semantics preserved.
- **Guard Down / Guard Up**: durations 3→2 turns.
- **Égide / Mezzo Forte**: keep as-is.
- **Golgra's Combo (Virtuose Strike)**: gradient 3 charges, Lightning, ×30 → `VirtuoseStrike_BaseDamage` 0.3→30; element Lightning.
- **Phoenix Flame**: gradient 5 charges, `BurnAmount` 10→100, revive 50%→100% (`BaseRevivePercentHealth` 1.0, per-QTE 0).
- **Gommage**: gradient 10 charges, `Gommage_BaseDamage` 50→100.

---

## 4. Implemented changes (MaelleSkillsKit) + remaining risks

**Resolved during implementation (user decisions):**
1. **Virtuose ×1.5 on Percée / Momentum** → implemented as a stance-gated `AddFinalDamageMultiplier(1.5)` splice before `DealDamages` (BP surgery, cloned from each file's own proven `CurrentStance==3` shape). Percée is now **Fire**; Momentum stays weapon-element (its damage element flows through `GetBaseDamageType` — cannot be forced).
2. **Degagement** → `WeaknessDuration` 2→5. The mechanic sets the target's Fire elemental affinity (`EElementalAffinity::NewEnumerator1`) via `AddElementalAffinityOverride` — it does **not** stack multiplicatively with innate weakness; it overrides/refreshes it.
3. **Thermal Arc** → keeps the consumes-burn mechanic, **cap 10→50** (BP `IntConst` in `Min(burn, 10)` → 50), Lightning element, 4 AP.
4. **Revenge** → base rescaled to 0.4×clamp(5)=0.66; `DamageIncreasePerHit` 1.5→**0.5** (+50% per hit received, linear).
5. **Burning Canvas** → base rescaled to 0.4×clamp(11)÷5=0.388/hit; per-burn `DamageIncreasePerBurnStack` 0.1 kept (linear, non-consuming).
6. **Death's Bargain (Last Chance)** → 8 AP; self-Curse `ApplyBuff(BP_BattleBuff_Cursed_C)` inserted after `SetHP`; AP refill (`LastChance_APGain` 9 = full) + HP→1 + Virtuose switch kept.
7. **Phoenix Flame** → revive exactly 100% (`BaseRevivePercentHealth` 1.0, per-QTE 0), burn 10→100, 5 gradient charges.

**Stance rework (CombatBalanceKit + StanceEffectsKit):** Defensive taken 0.5 (vanilla), Offensive dealt 1.5 (vanilla), Virtuous dealt **2.0**; removed the mod's Offensive +1 AP/hit insert and the Virtuose received-handler clone; Virtuous +1 Berserk/turn (12-turn) kept. Descriptions updated in `ST_UniqueMechanics`.

**Staged per move** (verified in the packed `work/mod` after `rebuild-mod`):
- Lightning: Thermal Arc (4 AP, Lightning, consume-50), Electric Fury (9 AP, Lightning), Breaking Rules (5 AP, Lightning), Payback (5 AP, Lightning), Gustave's Homage (13 AP), Guard Down/Up (2 turns).
- Fire: Percée (1 AP, Fire, Marked ×1.5, Virtuose ×1.5), Momentum Strike (7 AP, weapon, Marked ×1.5, Virtuose ×1.5), Flame Eater (3 AP, Fire, AP-regain stripped), Pyrolyse (9 AP), Sword Ballet (9 AP), Revenge (5 AP), Spark (2 AP), Degagement (weak 5 turns), Rain of Fire (5 AP, script constant 1.25→0.33).
- Void: Phantom Strike (12 AP, gradient 0.5), Burning Canvas (11 AP), Stendhal (15 AP), Death's Bargain (8 AP + Curse).
- Gradient: Golgra's Combo (3 charges, Lightning, ×30), Phoenix Flame (5 charges, revive 100%, burn 100), Gommage (10 charges, ×100).
- Renames via string table: Thermal Arc / Electric Fury / Flame Eater / Death's Bargain / Golgra's Combo.

**Not changed (as requested / out of scope):** Égide, Mezzo Forte keep stats. Weapon-element moves keep `{DynamicElement}` descriptions. Rain of Fire is now rebalanced too (above) — it was the only reworked-path move missing a `MaelleSkillsKit.StringEdits` entry, so it had kept rendering the vanilla prose while every sibling showed a gold damage figure.

**Description keyword convention (empty-status fix):** statuses with a standalone rich-text keyword style are the only ones that render as `<keyword id="StatusEffect_*">`: **Burn, Mark, Stunned** (verified against `DT_RichTextDecorator_KeywordStyles` — only those three have non-`_Left` styles). Blight/Cursed/etc. only have `_Left` styles (HUD use), so keyword-wrapping them in a skill description renders as an empty gap. The game's convention (verified in `ST_PassiveEffects`) is to write those as **plain words** ("Blight", "Cursed"). Death's Bargain ("Applies Curse to self") and Ultima Noir ("Inflicts 10 Blight on self") use plain text accordingly.

**Remaining risks / verification needed:**
- Percée/Momentum Virtuose splices are structurally verified and the ×1.5 bonus was **confirmed working in-game** at the temporary ×100 test value (2026-09-05); reverted to ×1.5.
- Percée/Momentum `SwitchValue` offsets flagged by kismet-check are **pre-existing in the vanilla assets** (identical before/after) — not introduced by the kit.
- Momentum is weapon-element; its "Fire path" role is thematic (Marked ×1.5 + Virtuose ×1.5), not a Fire element.
- FX/visual rethemes for the renamed Lightning/Fire moves were **not** changed (donor sequences are never written — repo hard rule); only damage element bytes changed. Lightning/Fire visuals would need FX reference swaps at BP level (a follow-up if desired).
- Gradient FX ("Add lightning FX if possible" for Golgra's Combo) not yet done — sequence-baked, needs a safe FX-swap approach.

---

## 5. Stance rework targets (supersedes current mod values)

Current mod (CombatBalanceKit) vs new targets:

| Stance | Row | Mod now | New target |
|---|---|---|---|
| Defensive | `Maelle_DefensiveStance_DMGReceivedMultiplier` | 0.4 | **0.5** |
| Offensive | `Maelle_OffensiveStance_DMGDealtMultiplier` | 2.5 | **1.5** |
| Offensive | `Maelle_OffensiveStance_DMGReceivedMultiplier` | 2.5 | 1.5 (vanilla) |
| Virtuous | `Maelle_VirtuoseStance_DMGDealtMultiplier` | 1.5 | **2.0** |

StanceEffectsKit changes:
- Remove Offensive +1 AP/hit insert (`ProcessDealtRawDamages` GainAP).
- Keep Defensive parry +1 AP (vanilla ProcessAPChange).
- Keep Virtuous 12-turn Berserk per turn (`BP_StanceMechanicComponent.OnTurnStart` gated on Virtuose + `BP_BattleBuff_VirtuoseBerserk`).

Descriptions (ST_UniqueMechanics): Virtuous "+100% damage dealt. Gain 1 Berserk each turn." Defensive "−50% damage taken. Gain 1 AP per Parry." Offensive "+50% damage dealt." Stanceless "No stance bonuses."

---

## 6. New Maelle moves (donor clones, floating tree nodes)

Added 2026-09-06 (Lightning path), extended 2026-09-07 (Fire path): eight brand-new Maelle skills cloned from existing Maelle donors. Each is a full new DA + skill-script BP + balancing rows + `DT_SkillIcons` row + `ST_MainCharacters_Skills` rows, appended to `DA_SkillGraph_Maelle` as a **floating node** (no prerequisite edge — the user connects them in the Graph Editor later). Damage = Maelle rule `0.75 × clamp(AP)`, shown **per-hit** in descriptions.

| New move | Donor | AP | Hits | Element | BaseDamage row (per-hit) | Special |
|---|---|---|---|---|---|---|
| **Lumière Above** | Rain of Fire (`NEW9_RainOfFire`) | 15 | 2 | Lightning | `LumiereAbove_BaseDamage` 2.5324 | **single target** (inherits the donor's `TargetingType` `NewEnumerator1` — corrected 2026-09-13, was documented/described as AoE); **Burn removed**; forced **Defensive** stance on cast (was Offensive); **×2 while in Virtuose** (`AddFinalDamageMultiplier(2.0)` behind Percée's cloned `CurrentStance==3` gate) |
| **Revers Fatal** | Thermal Arc / Combustion (`NEW19_InvigoratingFire`) | 17 | 2 | Lightning | `ReversFatal_BaseDamage` 4.0926 | no Burn apply; **consumes up to 100 Burn**, +1%/Burn (`DamageIncreasePerBurn` 0.01); forced stance switch removed; **Virtuose-gated Greater Slow + Greater Defenceless** (3 turns, Percée gate + bare-ApplyBuff) |
| **Déviation Absolue** | Payback (`NEW13_Payback`) | 19 | 1 | Lightning | `DeviationAbsolue_BaseDamage` 13.2278 | parry discount **−2 AP per hit parried last turn** (Payback's `GetSkillCostOverride` `Subtract(base, parries)` → `Subtract(base, Multiply(parries, 2))`); Break |
| **For Those Who Come After** | Gustave's Homage (`GustaveSMemoire`) | 21 | 8 | Lightning | `ForThoseWhoComeAfter_BaseDamage` 2.6721 | keeps Marked ×1.5 + LastHit ×2 + Break ("Doesn't remove Mark"); icon row donor is `GustavesMemoire` (the DA stem is `GustaveSMemoire`) |
| **Vengeance Ardente** | Revenge (`Revenge`) | 16 | 1 | Fire | `VengeanceArdente_BaseDamage` 6.4387 | **+50% damage per hit received since previous turn** (`DamageIncreasePerHit` 0.5); forces Offensive on cast (donor behavior); Break |
| **Alicia's Nightmare** | Pyrolyse (`Pyrolyse`) | 21 | 3 | Fire | `AliciasNightmare_BaseDamage` 7.1257 | applies **30 Burn/hit** (`BurnPerHit` 30); **+20 Burn/hit in Offensive Stance** (`BurnOnOvercharge` 20 — Pyrolyse's stance-gated bonus scaled up); forces Offensive on cast |
| **Blade Waltz** | Sword Ballet (`SwordBallet`) | 18 | 5 | **weapon** | `BladeWaltz_BaseDamage` 2.0811 | weapon element (donor is `DynamicElementOverride` — cannot take a fixed byte); crits ×2 (`CriticalDamageMultiplier` 2.0); forces Offensive on cast |
| **L'Acte Final Incendiaire** | Burning Canvas (`BurningCanvas`) | 20 | 5 | Fire (retheme) | `LActeFinalIncendiaire_BaseDamage` 3.3632 | applies **5 Burn/hit**; **+1% damage per Burn on the target** (`DamageIncreasePerBurnStack` 0.01, non-consuming read); Void donor rethemed Fire; forces Offensive on cast |

All eight use each donor's vanilla LevelSequence (never written — repo hard rule). FX note: the requested "recolour lightning yellow" for Lumière Above is not done — the donor's sequence-baked FX cannot be swapped without a sequence write; the damage element byte is Lightning and descriptions read Lightning.

### Description conventions (audit, 2026-09-07)

- **Multi-hit moves quote the per-hit multiplier** (the balancing `*_BaseDamage` row = each hit's floating damage), phrased "… damage per hit. N hits." Single-hit moves quote total = per-hit.
- **Conditional scaling states the exact rate**: "+50% for each hit received" (Revenge/Vengeance), "1 less AP per attack parried" (Payback) / "−2 AP per attack parried" (Déviation), "+1% damage per Burn consumed" (Thermal Arc / Revers Fatal), "+1% damage per Burn on the target" (Burning Canvas / L'Acte Final).
- **Burn-scaling global rule**: every Maelle move scaling damage off Burn uses **+1% per Burn** (row 0.01). Applies to `InvigoratingFire_DamageIncreasePerBurn`, `BurningCanvas_DamageIncreasePerBurnStack`, and both clone rows. (Was 0.4 / 0.1.)

### Virtuose-gate cross-BP clone (the hard part)

Rain of Fire and Thermal Arc have **no `CurrentStance` read** in their bytecode, so a Virtuose gate cannot be synthesized (synthesized InstVar FFieldPaths are VM no-ops). The fix clones Percée's proven gate — `LetObj GetComponentByClass(StanceMechanic)` + `LetBool EqualEqual_ByteByte(Context(comp).CurrentStance, ByteConst 3)` — across BPs with every cross-BP hazard handled:
- local `EX_LocalVariable` FFieldPaths retargeted to the destination function's export;
- missing local FProperties **ShallowCloned from the destination BP's own** same-type properties (never copied cross-BP — that corrupts the asset);
- InstVar (`CurrentCharacter`/`CurrentStance`) ResolvedOwners retargeted to the destination BP's class export;
- the `BP_StanceMechanicComponent_C` ObjectConst re-pointed at the destination BP's own import.

Revers Fatal's statuses apply through the game's proven bare `ApplyBuff` (target stats, buff class, duration IntConst, `NoObject`, `ByteConst 4`, out-local) — the same shape GuardDown uses.

### Verification status

- `kismet-check` clean on all clones except those whose **donor carries pre-existing** bad SwitchValue targets: For Those Who Come After (byte-identical to vanilla `GustaveSMemoire`) and Alicia's Nightmare (byte-identical to vanilla `Pyrolyse`).
- `skill-graph-smoke` asserts all eight floating nodes appear in the Maelle editor baseline (35 nodes).
- Damage rows verified in the packed balancing table; string rows verified in `ST_MainCharacters_Skills` (accents included); icon rows present; graph nodes present with no edges.
- In-game test pending (cast each move, stance gates, parry discount).


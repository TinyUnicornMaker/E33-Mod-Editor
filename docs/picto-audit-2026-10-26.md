# Picto Audit — 2026-10-26

**Build audited:** `ExpeditionInfinity_7777777_v7_P` (seed 7777777), staged tree `work/mod`.
**Scope:** every picto the mod adds, renames, retunes, or re-costs. Untouched vanilla pictos were inspected only when a modded row depends on them (`Glass Cannon`, `Burning Dodge`, `ManOfParry`, `Random Defense`, `Teamwork`, `Painted Power`, `Last Stand`).
**Read-only:** no code, data, or packed assets were modified during the audit. The only file written by the audit is this report.

> **Post-audit correction applied (2026-10-26):** `ParrySpecialist*_CounterDamageMultiplier` was restored to **2.0** (direct multiplier = ×2); the audited build's `1.0` is documented in C1 as the broken state.
>
> **Post-audit fixes applied (2026-10-26):**
> - Random Defense BP now yields the advertised **50% / 150%** (not 50–200%).
> - Dodge Helper and Parry Helper costs are **5 / 10 / 15**.
> - Auto Curse, Auto Berserk, Auto Blight cost **10**.
> - Ultima Power costs **33** and its row/ST text is the conditional unlock text.
> - Burning Skin costs **1 / 3 / 5 / 10 / 15**.
> - `WepFx_CounterDmg2/3` are restored to **3.0 / 4.0** (×3/×4, matching +200%/+300%).
> - Stale ST text for Stun Boost, Roulette, Teamwork, Cursed Power and Ultima Power is corrected.
> - Parry Specialist costs remain 5/10/20; counter is 2.0.
>
> The findings below remain the audit record; the fixes above supersede the corresponding “open” recommendations.
>
> **Post-audit categories (2026-11-02):** every mod-owned picto now carries
> `DT_PassiveEffects.Categories`. The central `PictoTagCatalog` covers created,
> revived and reworked rows; `PictoTagKit` repairs old packs and ships the
> extended `E_PassiveEffectCategory` enum (Element / Revive / Lifesteal / Luck).
> See [picto-tags-plan.md](picto-tags-plan.md).

## Verification basis

Verified directly from the built `work/mod` data:

| Source | Path |
|---|---|
| Passive rows (name, text, cost, BP link) | `work/mod/Sandfall/Content/Gameplay/Lumina/DT_PassiveEffects.uasset` |
| Balancing rows | `work/mod/Sandfall/Content/Gameplay/Battle/ScalingSystem/BalancingTables/Content/DT_Balancing_Luminas.uasset` |
| String table | `work/mod/Sandfall/Content/StringTables/Gameplay/ST_PassiveEffects.uasset` |
| Item rows | `work/mod/Sandfall/Content/jRPGTemplate/Datatables/DT_jRPG_Items.uasset` |
| Stat definitions | `work/mod/Sandfall/Content/Gameplay/Pictos/DT_PictosDefinitions.uasset` |
| Loot tiers | `work/item_tiers.json` and `src/E33ModEditor.Core/Loot/Data/item_tiers.json` (identical) |
| Effect logic | kismet dumps of the staged BPs and vanilla donors under `work/mod` / `work/tables` |

Dumps were produced with the repo CLI (`dump`, `st`, `dumpkismet`, `dumpasset`) and cross-checked between the built table and the effect Blueprint. Where this report says “built”, it means the value in the staged `work/mod` table, not a source constant. Two items are explicitly labelled as inferred because the built package does not expose the value as a parsable CDO in this dump: Super Sonic’s buff multiplier (2.0, from `SpecialPictosKit`/the buff CDO description) and Revive Bomb Slow’s 2-turn default (from the `ReviveDebuffKit` comments).

**Modifier-type semantics** were re-derived from the built BPs because the source comments and docs are inconsistent:

- `FDamageModifier` **serialized type byte 2** = `EDamageModifierType::NewEnumerator3` → **direct multiplier** (`base × value`).
  Evidence: vanilla `BP_LU_Teamwork` 1.1 = “10% increased”; vanilla `BP_LU_GlassCanon` 1.25 = “25% more”; vanilla `BP_LU_ParryApX2DamageReceivedX2` 2.0 = “damage received is doubled”; `BP_BattleDamages.ApplyCriticalHitModifiers` builds type 2 with 1.5 and logs `Crit: x1.5`; `BP_LU_UnoPuncho` builds type 2 with 10.0 / 0.0 = ×10 / ×0.
- `FDamageModifier` **serialized type byte 3** = `EDamageModifierType::NewEnumerator2` → **flat additive** (`base × (1 + Σ values)`).
  Evidence: `BP_LU_StunBoost` 0.3 = vanilla “30% increased”; `BP_LU_Immaculate` 0.3 = “30% increased”. A large negative flat value is **not** a safe zero: the game’s 9,999 clamp is optional, so negative damage heals instead.

The enum offset is important: “type 2” in some notes means enum entry `NewEnumerator2` (serialized 3), while Parry Specialist’s row is serialized 2 (`NewEnumerator3`). The recent Parry fix conflated the two — see C1.

---

## Executive summary

1. **Parry Specialist I–III no longer double counter damage.** Built `CounterDamageMultiplier` is `1.0` on all three. The row is a **multiplier** (`ByteConst 2`), so `1.0` is ×1 (no bonus), not ×2. The recent fix changed the wrong number: the old `2.0` was already ×2, not ×3. Recommended value: `2.0` on all three. This is the highest-severity finding.
2. **Random Defense’s tooltip is wrong.** The BP reads only `RandomDefense_LowMultiplier`; `RandomDefense_HighMultiplier` is dead. The graph produces a continuous random multiplier from 50% to 200% (the vanilla text), not a 50%/150% coin flip. All five tiers have the same effect at rising cost.
3. **Glass Cannon II/III are more expensive but worse.** All three deal the same +25%; II/III only increase the damage *taken* penalty (50%/100%) for 20/30 lumina. Higher tier is strictly worse.
4. **François’s Shell II/III are more expensive but worse.** All three take 10% less damage; II/III only increase the damage *dealt* penalty (−20%/−30%) for 20/30 lumina. This looks like a copy-paste of the received multiplier (`0.9` on all three); the ladder should probably be 10/20/30% mitigation at 10/20/30 lumina.
5. **Parry Helper costs are inverted:** I = 30 lumina (T3), II = 20 (T4), III = 25 (T5), all +22% window. The mirror family Dodge Helper is 15/20/25. The base cost was never added to `LuminaCosts`, so it kept vanilla 30.
6. **Berserk Power II/III are one loot tier too high:** T4/T5 instead of T3/T4, while Cursed/Blighted Power with identical 5/10/20% values and 5/10/15 costs are T3/T4.
7. **Stale ST text:** `PASSIVE_StunBoost_Description` still says 30% (built row says 10%); `PASSIVE_Roulette_Description` still says 200% (built row and BP say 150%). `PASSIVE_OverPowered_*` still says “Painted Power / Damage can exceed 9,999” while the row/item say “Ultima Power / Fully unlocks your power.”
8. **Undocumented identical-tier stackers.** Augmented Aim (five ×10%), Solo Fighter (three ×1.5), Roulette (five identical), Random Defense (five identical), Energising Pain (five ×+1 AP), Perilous Parry (three identical), Energising Turn (three ×+1 AP), Lucky Aim (three ×15%) all let a character wear up to three tiers. Worst case: Solo Fighter I+II+III = ×3.375 (+237.5%) for 18 lumina; Perilous Parry I+II+III = ×8 incoming damage for 30 lumina. Tooltips do not mention stacking.
9. **Cost/tier outliers:** Auto Berserk/Blight are T5 at 1 lumina; Infinite is 10 lumina for 3,333,333 max HP; Ultima Power is 5 lumina for the damage-cap unlock; Burning Skin is 5 lumina at every tier from 20% to 100%; Longer Burn is 15 lumina at every tier for +2/+3/+4.

---

## Confirmed findings and mismatches

### C1. Parry Specialist I–III — “double damage” is currently ×1

| Field | Value |
|---|---|
| Ids | `ParrySpecialist` (T2/5), `ParrySpecialist2` (T3/10), `ParrySpecialist3` (T4/20) |
| Built text | “Parry window reduced by 10%. Counterattacks deal double damage.” |
| Built rows | `ParrySpecialist_CounterDamageMultiplier = 1.0`, `...2 = 1.0`, `...3 = 1.0` |
| BP | `BP_LU_ParrySpecialist` builds `FDamageModifier` with serialized type `2` (`NewEnumerator3`) and the row value |
| Actual | ×1.0 counter damage (no bonus); the −10% parry window still applies |
| Expected | `2.0` on all three |
| Post-audit status | **Corrected to 2.0**; costs remain 5/10/20 |

**Why `1.0` is wrong.** Serialized type 2 is a direct multiplier, not the `1 + Σ` additive type. The same struct/type is used by:
- `BP_LU_ParryApX2DamageReceivedX2`, value `2.0`, vanilla description “damage received is doubled”;
- `BP_LU_GlassCanon`, `1.25` for “25% more damage”;
- vanilla `BP_LU_Teamwork`, `1.1` for “10% increased”;
- `BP_BattleDamages.ApplyCriticalHitModifiers`, `1.5` with log `Crit: x1.5`.

The additive type is serialized 3 (`NewEnumerator2`), used by Stun Boost/Immaculate (`0.1` = +10%) and Uno Puncho (`9.0` = ×10). The previous `2.0` therefore already matched “double”; changing it to `1.0` removed the effect. If the intent is truly ×2, restore `2.0`.

**Related (out of picto scope, same root cause):** the created weapon passives `WepFx_CounterDmg2/3` now read `2.0/3.0` in `DT_Balancing_Luminas`. Under direct-multiplier semantics those are +100%/+200%, not the advertised +200%/+300%; the matching values should be `3.0/4.0` if the tooltips are right. (The original values are not in the vanilla table, so this is inferred from the current rows plus the documented tooltip text.)

### C2. Random Defense I–V — tooltip says 50%/150%, BP produces 50–200%

| Field | Value |
|---|---|
| Ids | `RandomDefense` (T1/5) … `RandomDefense5` (T5/25) |
| Built text | “Damage taken is randomly multiplied by 50% or 150%.” |
| Built rows | `LowMultiplier = 0.5`, `HighMultiplier = 1.5` (all tiers) |
| BP | `BP_LU_RandomDefense.ProcessReceivedRawDamages` reads **only** `RandomDefense_LowMultiplier` |
| Actual | random integer in [50,100] / 100 → 0.5–1.0; then either that value or its reciprocal → 1.0–2.0; random bool selects the branch → overall 0.5–2.0× |
| Expected | Either restore the vanilla text “between 50% and 200%”, or rewrite the BP to read both rows and select 0.5/1.5 |

`RandomDefense_HighMultiplier` is dead data: the only `NameConst` in the kismet dump is `RandomDefense_LowMultiplier`. The 1.5 value never reaches the graph.

### C3. Burning Skin I–V — verified self-burn, not a text mismatch

| Field | Value |
|---|---|
| Ids | `BurnOnHit1` (T1/5) … `BurnOnHit5` (T5/5) |
| Built text | “Gain 1 Burn on dodge (20%).” … “(100%).” |
| Actual BP | clones of `BP_LU_DodgeBurn` / `BP_LU_DodgeCanInflictStatus`; the parent’s only `K2Node_CustomEvent_Enemy` reference (the `ActivateBuff` target) is retargeted to `K2Node_CustomEvent_Character` by `PatchBurnKismetRec`, so the Burn lands on the wearer; chance patched 0.20/0.40/0.60/0.80/1.00; duration 5 |
| Vanilla donor text | `BurningDodge`: “Successful Dodges can Burn the attacker.” |

This **looks** like a direction bug if you read only the donor text, but the bytecode retarget means the wearer really does gain the Burn. The mod’s text is correct for the built effect. The remaining issues are economy, not text: all five tiers cost 5 lumina while the chance rises 20→100%, and three copies can be worn (up to 3 Burn stacks on dodge for 15 lumina).

### C4. Glass Cannon II/III — higher cost, same benefit, bigger drawback

| Id | Tier/Cost | Dealt | Received | Text |
|---|---|---|---|---|
| `GlassCanon` | T1/10 | ×1.25 (+25%) | ×1.25 (+25%) | Deal 25%, take 25% |
| `GlassCanon2` | T3/20 | ×1.25 (+25%) | ×1.5 (+50%) | Deal 25%, take 50% |
| `GlassCanon3` | T5/30 | ×1.25 (+25%) | ×2.0 (+100%) | Deal 25%, take 100% |

The text matches the rows, but the ladder is a pure penalty increase. II/III cost more for no extra damage. Either raise `DamageMultiplier` to `1.5/2.0` for II/III, or lower their costs/received penalties. Flagged as a cost/effect anomaly, not a text mismatch.

### C5. François’s Shell II/III — higher cost, same mitigation, bigger drawback

| Id | Tier/Cost | Dealt | Received | Text |
|---|---|---|---|---|
| `TitaniumShell` | T1/10 | ×0.9 (−10%) | ×0.9 (−10%) | Take 10% less, deal 10% less |
| `TitaniumShell2` | T2/20 | ×0.8 (−20%) | ×0.9 (−10%) | Take 10% less, deal 20% less |
| `TitaniumShell3` | T3/30 | ×0.7 (−30%) | ×0.9 (−10%) | Take 10% less, deal 30% less |

The received multiplier never moves off `0.9`. The source comment says the design “reduces damage dealt AND damage received”; the implementation only reduces the first. The most likely intended ladder is received `0.9/0.8/0.7` with dealt `0.9/0.8/0.7` (or costs reversed). As built, tier III is strictly worse than tier I and costs 3× as much.

### C6. Parry Helper I cost is higher than II

| Id | Tier | Cost | Effect |
|---|---|---|---|
| `ParryHelper` | T3 | **30** | +22% parry window |
| `ParryHelper2` | T4 | 20 | +22% parry window |
| `ParryHelper3` | T5 | 25 | +22% parry window |

`LuminaCosts` has no `ParryHelper` entry, so the vanilla 30 was left in place; the clones were given 20/25. The mirror family Dodge Helper is `LongerDodge` 15 / `Dodger2` 20 / `Dodger3` 25. Expected: 15/20/25 or 20/25/30; the current order is non-monotonic.

### C7. Berserk Power II/III tier placement

| Family | I | II | III | Values | Costs |
|---|---|---|---|---|---|
| Cursed Power | T2 | T3 | T4 | 5/10/20% | 5/10/15 |
| Blighted Power | T2 | T3 | T4 | 5/10/20% | 5/10/15 |
| **Berserk Power** | **T2** | **T4** | **T5** | 5/10/20% | 5/10/15 |

`work/item_tiers.json` puts `BerserkPower2` in `Tier4` and `BerserkPower3` in `Tier5`. With identical effects and costs to the other two families, this is almost certainly a copy-paste placement error. Expected: II → Tier3, III → Tier4.

### C8. Stale ST text — Stun Boost I

| Source | Value |
|---|---|
| `DT_PassiveEffects.StunBoost.Description` | “Deal 10% more damage to Stunned targets.” (inline) |
| `DT_Balancing_Luminas.StunBoost_DamageIncrease` | `0.1` |
| `ST_PassiveEffects.PASSIVE_StunBoost_Description` | **“30% increased damage on Stunned targets.”** |

The row and the balancing data are tier-I-correct; the ST entry is stale. If the pictos tooltip resolves the ST key (as the kit comments say it does), it shows 30%.

### C9. Stale ST text — Roulette I

| Source | Value |
|---|---|
| `DT_PassiveEffects.Roulette.Description` | “…50% or 150%…” (inline) |
| `DT_Balancing_Luminas.Roulette_HighValue` | `1.5` |
| `ST_PassiveEffects.PASSIVE_Roulette_Description` | **“…50% or 200%…”** |

The actual BP selects `0.5` or `1.5`, so the built row is right and the ST entry is stale. If the tooltip reads ST, it advertises the vanilla 200%.

### C10. Teamwork text omits the “all allies alive” condition

| Field | Value |
|---|---|
| Built text | “Deal 5% more damage while not fighting alone.” (II: 10%, III: 15%) |
| BP condition (`BP_LU_Teamwork`) | `AllPlayerCharacters.Length > 1` **AND** `DeadPlayerCharacters.Length == 0` |
| Actual | The buff stops when any ally dies, even if you are still not alone. |

The vanilla text was “10% increased damage while all allies are alive.” The new text is weaker than the actual condition. Expected: “while all allies are alive and you are not alone” or restore the vanilla wording.

### C11. Revive Bomb — Slow: 3 turns advertised, likely 2

| Id | Tier/Cost | Text | BP/buff default |
|---|---|---|---|
| `ReviveWithSlow` | T3/5 | “Inflict Slow on all enemies for 3 turns when revived.” | `BP_BattleBuff_Slow` parent default 2 (per `ReviveDebuffKit` comments); no duration override is applied to the clone |
| `ReviveWithPowerless` | T3/5 | 3 turns | parent default 3 |
| `ReviveWithDefenceless` | T3/5 | 3 turns | parent default 3 |

This is **inferred**, not proven from a parsed CDO: the clone only retargets the package and the kit itself logs that Slow reads 2 while the other two inherit 3. Verify in-game or with a CDO dump; if confirmed, change the text to 2 or splice a `SetTurnDuration(3)` into the clone.

### C12. Ultima Power — effect text no longer describes the effect; ST stale

| Source | Value |
|---|---|
| Item / passive row | “Ultima Power” / “Fully unlocks your power.” |
| ST | `PASSIVE_OverPowered_Name` = **“Painted Power”**, `PASSIVE_OverPowered_Description` = **“Damage can exceed 9,999.”** |
| BP (`BP_LU_Overpowered`) | Adds `DamageLimitUnlockRefCount` only when the wearer receives a hit with `ReceivedDamages > 9999` |

The flavour text is not false, but it hides both the real effect and the conditional trigger. Expected: “After taking 9,999+ damage in a hit, damage can exceed 9,999.” plus updated ST keys.

### C13. Cursed Power I — ST keys missing (latent)

`DT_PassiveEffects.CursedPower` and the item row are inline and correct (“Cursed Power I”, “Deal 5% more damage while Cursed.”). `ST_PassiveEffects` has **no** `PASSIVE_CursedPower_Name/Description` entries (vanilla also had none for the base row). If the tooltip resolves ST, the row has no fallback. Low risk, but inconsistent with every other base-tier rename.

### C14. Painter conversions — identical effect split across T4/T5

| Id | Tier | Cost | Effect |
|---|---|---|---|
| `PainterConv_Fire/Ice/Lightning/Earth` | T4 | 10 | Convert all Skill damage to that element |
| `PainterConv_Dark/Light/Physical/Void` | T5 | 10 | Convert all Skill damage to that element |

All eight have the same effect and cost but two different loot tiers. This may be intentional rarity by element, but it is not documented; if not, normalise all to one tier.

### C15. Revive-with-status cost split

| Ids | Tier | Cost | Effect |
|---|---|---|---|
| `ReviveWithCurse`, `ReviveWithBerserk`, `ReviveWithBlight` | T3 | **3** | Apply status (to all allies) 3 turns on revive |
| `ReviveWithShell`, `ReviveWithRush` | T3 | **5** | Apply status (to all allies) 3 turns on revive |
| `ReviveWithPowerless`, `ReviveWithDefenceless`, `ReviveWithSlow` | T3 | **5** | Inflict status on all enemies 3 turns on revive |

Same tier and trigger; the first three inherited the vanilla `ReviveWithPower` cost (3). If the statuses are considered equivalent, this should be uniform. Also, all “Apply X … when revived” texts omit that the buff goes to **all allies**, not just the wearer.

---

## Likely mismatches and design smells

### S1. Identical-tier stackers with no “stacks” text

A character has three picto slots, so a family with identical tiers can put at most three copies on one character. Even so, the hidden totals are large:

| Family | Identical effect | Max 3-tier stack | Costs |
|---|---|---|---|
| Augmented Aim | +10% free-aim damage each | +30% | 1/3/5/10/21 |
| Solo Fighter | ×1.5 each | ×3.375 (+237.5%) | 1/6/11 |
| Roulette | 50%: 0.5× or 1.5× each | 3 independent rolls (0.125×–3.375×) | 5/10/15/20/25 |
| Random Defense | 0.5–2.0× incoming each | 3 independent multipliers | 5/10/15/20/25 |
| Energising Pain | +1 AP on hit each | +3 AP per hit | 10/20/30/40/50 |
| Perilous Parry | ×2 incoming, +1 AP per parry each | ×8 incoming, +3 AP | 5/10/15 |
| Energising Turn | +1 AP at turn start each | +3 AP/turn | 20/30/40 |
| Lucky Aim | 15% chance +1 AP each | 3 independent 15% rolls | 1/7/21 |

The tooltips describe a single copy only. The docs call some of these intentional stackers, but the in-game text does not. At minimum, add “Stacks with other tiers”; at worst, differentiate the tiers or remove the duplicates.

On-death families also stack because each passive fires independently: three Death Bomb tiers (III+IV+V) = 17× AoE for 85 lumina; Burning Death III+IV+V = 80 Burn for 80; Energising Death III+IV+V = +12 AP to allies for 60. Again, the tooltips describe one copy.

### S2. Multiplicative ladder stacking not disclosed

Families whose tiers use a direct multiplier (FDamageModifier serialized type 2, or the break-bar multiplier, which is also multiplicative) compound across equipped tiers:

- **Full Strength** best three (III+IV+V) = `1.2 × 1.3 × 1.5 = 2.34` (+134%) for 100 lumina.
- **Parry Specialist** all three = `2.0 × 2.0 × 2.0 = 8.0` counters for 35 lumina, plus −30% parry window.
- **The Best Defense** best three = `1.5 × 2.0 × 3.0 = 9.0` (+800%) for 105 lumina.
- **First Offensive** best three = ×9.0 for 105 lumina.
- **Breaking Counter** best three = ×9.0 for 80 lumina.
- **Critical Break** best three = `1.3 × 1.4 × 1.5 = 2.73` (+173%) for 60 lumina.
- **Empowering Jumps** best three at one jump counter = `1.5 × 2.0 × 3.0 = 9.0` (+800%) for 105 lumina; at two counters it is ×30.
- **Confident** best three = `0.85 × 0.8 × 0.75 = 0.51` (49% less damage taken) for 75 lumina.

The per-copy tooltip is correct; the family-level result is not described. This is the same class of issue as the Parry confusion: a multiplier row is being treated as if it were additive in the design notes.

### S3. Stun Boost is additive, not multiplicative, as documented

`custom-pictos.md` says the Stun Boost tiers “add each own damage modifier, so worn together they compound multiplicatively.” Built BPs use serialized type 3 (flat additive): `0.1 + 0.2 + 0.5 + 1.0 + 1.5 = 3.3` → ×4.3 (+330%) if all five could be worn. They sum, they do not compound. The per-tier text is correct; the stacking note is wrong. With the three-slot limit, the strongest three sum to +300% for 32 lumina, still the cheapest large damage ladder in the pack.

### S4. Flat costs make higher tiers free upgrades

| Family | Costs | Effect ladder |
|---|---|---|
| Burning Skin | 5 / 5 / 5 / 5 / 5 | 20 / 40 / 60 / 80 / 100% chance |
| Longer Burn | 15 / 15 / 15 | +2 / +3 / +4 burn duration |
| Break Damage | 10 / 20 / 30 | +25% / +25% / +25% (identical) |
| Augmented Aim | 1 / 3 / 5 / 10 / 21 | +10% / +10% / +10% / +10% / +10% |
| Solo Fighter | 1 / 6 / 11 | +50% / +50% / +50% |
| Energising Pain | 10 / 20 / 30 / 40 / 50 | +1 AP / +1 AP / +1 AP / +1 AP / +1 AP |
| Perilous Parry | 5 / 10 / 15 | identical |
| Energising Turn | 20 / 30 / 40 | identical |
| Lucky Aim | 1 / 7 / 21 | identical 15% |

For Burning Skin/Longer Burn the later tier is the better deal; for the identical families the earlier tier is the better deal. Neither progression is “higher tier = better value”.

### S5. Cost/tier outliers

| Id | Tier | Cost | Why it stands out |
|---|---|---|---|
| `AutoBerserk`, `AutoBlight` | T5 | **1** | T5 item at vanilla auto-status cost 1; AutoCurse is T2/1, AutoBurn T4/1. If the status is a downside enabler this may be intentional, but it is the cheapest T5 picto. |
| `Infinite` | — | **10** | 3,333,333 max HP for 10 lumina (exclusive drop from `L_Boss_Curator`). |
| `OverPowered` (Ultima Power) | — | **5** | Damage-cap unlock for 5 lumina (exclusive drop from `MM_MirrorRenoir`). |
| `APDiscount` | T4 | **10** | Was 30 vanilla; −1 AP to every skill for 10 lumina at T4 is cheap. II is 20 at T5. |
| `AugmentedAttack` | T1 | **1** | +10% base attack for 1 lumina (was 7). |
| `StunBoost` | T1–T5 | 3/5/7/10/15 | +150% at T5 for 15; the three strongest sum to +300% for 32. |
| `Death's Dividend` | T1 | **15** | T1 cost above the T1 median 5, for a revive-count scaling effect. |
| `Painters` | T4/T5 | **10** | T4/T5 utility at T1/T2 prices. |
| `ReviveWithCurse/Berserk/Blight` | T3 | **3** | Same tier/trigger as the 5-lumina revive statuses. |

### S6. Other under-specified text

- **SOS Curse/Berserk/Blight** — “Apply X when falling below 50% health.” Actual duration is 5 turns (vanilla `SosStatusEffect`), not stated.
- **Auto Berserk/Blight** — “Self apply X on battle start.” Actual duration 3 turns (parent CDO default), not stated.
- **Rupture Shot** — “Free Aim shots can inflict Inverted on the target.” Actual 50% chance / 5 turns; neither number is stated.
- **Revive with Shell/Rush/Curse/Berserk/Blight** — “Apply X for 3 turns when revived.” Actual target is all allies, not just the wearer.
- **Layered Canvas** — lists Burn as a gain; Burn is a self-damage status. The text is accurate but easy to misread as all-upside.
- **Ultima Power** — see C12.

---

## Cost anomalies table

| Id / family | Tier | Cost | Effect | Assessment |
|---|---|---|---|---|
| `ParryHelper` / `2` / `3` | T3/T4/T5 | 30 / 20 / 25 | +22% parry window each | **Non-monotonic**; base 30 > II 20. Fix to 15/20/25 or 20/25/30. |
| `GlassCanon` / `2` / `3` | T1/T3/T5 | 10 / 20 / 30 | +25% dealt; +25/50/100% taken | **Higher tier worse** for more cost. |
| `TitaniumShell` / `2` / `3` | T1/T2/T3 | 10 / 20 / 30 | −10% taken; −10/20/30% dealt | **Higher tier worse** for more cost. |
| `Breaker` / `BreakDamage2` / `3` | T1/T2/T3 | 10 / 20 / 30 | +25% break each | Same effect, escalating cost; only makes sense as a hidden stacker. |
| `BurnOnHit1`–`5` | T1–T5 | 5 / 5 / 5 / 5 / 5 | 20/40/60/80/100% chance | Flat cost; T5 is a free upgrade over T1. |
| `BurnDurationIncrease` / `2` / `3` | T3/T4/T5 | 15 / 15 / 15 | +2/+3/+4 duration | Flat cost; T5 is a free upgrade over T3. |
| `AutoBerserk`, `AutoBlight` | T5 | 1 | Auto status | T5 at 1 lumina. |
| `Infinite` | — | 10 | 3.33M max HP | Endgame trophy; cost trivial. |
| `OverPowered` | — | 5 | Damage cap unlock | Endgame trophy; cost trivial. |
| `APDiscount` / `2` | T4/T5 | 10 / 20 | −1 / −2 AP | Cheap T4 utility; vanilla was 30. |
| `AugmentedAttack` | T1 | 1 | +10% base attack | Cheap T1 outlier. |
| `StunBoost` ladder | T1–T5 | 3/5/7/10/15 | +10/20/50/100/150% | Very cheap per point at III–V. |
| `Death's Dividend` | T1 | 15 | +10% per revive | T1 cost 3× median. |
| `PainterConv_*` | T4/T5 | 10 | Element conversion | T4/T5 at T1/T2 price. |
| `ReviveWithCurse/Berserk/Blight` | T3 | 3 | Status on revive | 2 lumina cheaper than the other revive statuses. |
| `AugmentedAim1`–`5` | T1–T5 | 1/3/5/10/21 | +10% each | Same effect; cost per point rises 7×. |
| `SoloFighter1`–`3` | T1/T3/T5 | 1/6/11 | +50% each | Same effect; cost per copy rises 11×. |
| `PowerOfPain1`–`5` | T1–T5 | 10/20/30/40/50 | +1 AP each | Same effect; 50 lumina for +1 AP at V. |
| `ManOfParry1`–`3` | T2/T3/T4 | 5/10/15 | identical | Same effect; costs rise. |
| `LuckyAim1`–`3` | T1/T2/T3 | 1/7/21 | identical 15% | Same effect; cost per copy rises 21×. |
| `EnergisingTurn1`–`3` | T2/T3/T4 | 20/30/40 | +1 AP each | Same effect; T2 already expensive. |

---

## Stale / missing text and rows

| Item | Source | Finding | Severity |
|---|---|---|---|
| Stun Boost I | `ST_PassiveEffects.PASSIVE_StunBoost_Description` | 30% vanilla text; built row/balance = 10% | High if tooltip reads ST |
| Roulette I | `ST_PassiveEffects.PASSIVE_Roulette_Description` | 200% vanilla text; built row/balance = 150% | High if tooltip reads ST |
| Ultima Power | `ST_PassiveEffects.PASSIVE_OverPowered_Name/_Description` | “Painted Power” / “Damage can exceed 9,999” vs built row “Ultima Power” / “Fully unlocks your power.” | Medium |
| Cursed Power I | `ST_PassiveEffects` | No `PASSIVE_CursedPower_*` keys; row/item inline correct | Low (latent) |
| Teamwork | row text vs `BP_LU_Teamwork` | Text omits “all allies alive” | Medium |
| Revive Bomb Slow | row text vs `BP_BattleBuff_Slow` | 3 turns advertised; likely 2 | Medium (verify) |
| Burning Skin | row text vs donor | Verified self-burn after Enemy→Character retarget; no text mismatch | — |
| Random Defense | row text vs `BP_LU_RandomDefense` | 50/150 vs 50–200 | High |
| Ultima Power | row text vs `BP_LU_Overpowered` | Flavour text hides conditional unlock | Medium |
| Revive with Shell/Rush/Curse/Berserk/Blight | row text | Omits “all allies” target | Low |
| SOS / Auto / Rupture | row text | Omits durations (5 / 3 / 5 turns) | Low |
| Painter conversions | `work/item_tiers.json` | Identical effect split T4/T5 | Low |
| Berserk Power II/III | `work/item_tiers.json` | T4/T5 vs peers T3/T4 | Medium |

No missing `DT_PassiveEffects`, `DT_PictosDefinitions`, `DT_jRPG_Items`, or asset references were found for any created picto. Every new id resolves to a staged `BP_LU_*` and `DA_GearPictos_*`; all new stat-definition rows are non-zero. `BootyHunter` intentionally points at `BP_LU_DeadEnergyII` and reads `DeadEnergyII_ApAmount = 5` (the old `BootyHunter_ApAmount = 3` row is dead). `DeathBombPhysical` intentionally points at the vanilla `BP_LU_DeathBomb` and reads `DeathBombPhysical_FinalDamageMultiplier = 0.5`.

---

## Audited roster

Format: **Id (tier/cost)**. “Built text” is the resolved `DT_PassiveEffects`/item text. “Actual” is from the balancing row and/or the BP. Status: **OK**, **TEXT**, **COST**, **DESIGN**, **LIKELY**.

### Magnitude ladders

| Family / ids | Built text | Actual effect | Status |
|---|---|---|---|
| **Stun Boost** — `StunBoost` T1/3, `2` T2/5, `3` T3/7, `4` T4/10, `5` T5/15 | +10/20/50/100/150% vs Stunned | type 3 additive `0.1/0.2/0.5/1.0/1.5`; sums across tiers | OK (ST I stale; stacking note wrong) |
| **Glass Cannon** — `GlassCanon` T1/10, `2` T3/20, `3` T5/30 | +25% dealt; +25/50/100% taken | ×1.25 dealt; ×1.25/1.5/2.0 taken | COST/DESIGN |
| **Roulette** — `Roulette` T1/5, `2` T2/10, `3` T3/15, `4` T4/20, `5` T5/25 | 50% chance 50% or 150% | type 2, selects 0.5/1.5; all tiers identical | OK (ST I stale; stacker) |
| **Augmented Attack** — `AugmentedAttack` T1/1, `2` T2/5, `3` T3/20 | +10/25/50% Base Attack | type 3 additive 0.10/0.25/0.50 | OK |
| **Augmented Aim** — `AugmentedAim` T1/1, `2` T2/3, `3` T3/5, `4` T4/10, `5` T5/21 | +10% Free Aim each | type 3 additive 0.10 each; identical | DESIGN (stacker) |
| **Solo Fighter** — `SoloFighter` T1/1, `2` T3/6, `3` T5/11 | +50% while alone | type 2 ×1.5 each; identical | DESIGN (×3.375 for 18) |
| **Teamwork** — `Teamwork` T3/5, `2` T4/10, `3` T5/15 | +5/10/15% while not alone | type 2 ×1.05/1.10/1.15; requires no dead allies + >1 player | TEXT |
| **Break Damage** — `Breaker` T1/10, `BreakDamage2` T2/20, `BreakDamage3` T3/30 | +25% Break | `AddBreakBarDamageMultiplier` ×1.25 each; identical | COST/DESIGN |
| **Longer Burn** — `BurnDurationIncrease` T3/15, `2` T4/15, `3` T5/15 | +2/+3/+4 duration | `DurationIncrease` 2/3/4 | COST (flat) |
| **Dodge Helper** — `LongerDodge` T2/15, `Dodger2` T3/20, `Dodger3` T4/25 | +22% dodge window | `DodgeWindowIncrease` 0.22 each | DESIGN (stacker) |
| **Parry Helper** — `ParryHelper` T3/30, `2` T4/20, `3` T5/25 | +22% parry window | `ParryWindowIncrease` 0.22 each | COST (inverted) |
| **Parry Specialist** — `ParrySpecialist` T2/5, `2` T3/10, `3` T4/20 | −10% window; counter double | window −0.10; counter **×1.0** at audit / **2.0 corrected** | **TEXT (critical; corrected)** |
| **François’s Shell** — `TitaniumShell` T1/10, `2` T2/20, `3` T3/30 | −10% taken; −10/20/30% dealt | ×0.9 taken all; ×0.9/0.8/0.7 dealt | COST/DESIGN |
| **Immaculate** — `Immaculate` T1/10, `2` T2/15, `3` T3/20, `4` T4/30, `5` T5/40 | +10/20/30/50/100% until hit | type 3 additive 0.1/0.2/0.3/0.5/1.0 | OK |
| **Alternating Critical** — `AlternatingCritical` T1/10, `2` T2/15, `3` T3/20, `4` T4/25, `5` T5/30 | +10/20/30/40/50% next non-crit | type 3 additive 0.1/0.2/0.3/0.4/0.5 | OK |
| **AP Discount** — `APDiscount` T4/10, `2` T5/20 | Skills cost 1/2 less AP | `APReduction` −1/−2 | COST (cheap T4) |
| **First Life** — `FirstLife` T3/10, `2` T4/15, `3` T5/25 | +5/−5, +10/−10, +21/−21 | type 3 additive ±0.05/0.1/0.21 | OK |
| **Augmented First Strike** — `Augmented1stStrike` T1/10, `2` T2/15, `3` T3/20, `4` T4/30, `5` T5/40 | +5/10/20/30/50% first hit | type 3 additive 0.05/0.1/0.2/0.3/0.5 | OK (T1 10 lumina for 5%) |
| **Confident** — `Confident` T1/10, `2` T2/15, `3` T3/20, `4` T4/25, `5` T5/30 | Take 5/10/15/20/25% less; can’t heal | type 2 received 0.95/0.90/0.85/0.80/0.75 | OK (stacker) |
| **Macabre Power** — `CleasDeath` T3/10, `2` T4/15, `3` T5/20 | Allies +5/10/15% until death | type 3 additive 0.05/0.1/0.15 | OK |
| **Attack Lifesteal** — `AttackLifesteal` T2/10, `2` T3/15, `3` T4/20 | Heal 5/10/20% on Base Attack | `DamagePercentageToHeal` 0.05/0.1/0.2 | OK |
| **Gradient Overcharge** — `GradientOvercharge` T3/10, `2` T4/30, `3` T5/50 | 3/5/10 charges → +200/400/1000% | 3/5/10 charges; ×3/5/11 | OK |
| **Energy Master** — `DoubleAP` T5/40, `2` T5/60 | +1/+2 AP per gain | `BonusApAmount` 1/2 | OK (both T5) |
| **The Best Defense** — `BestDefense` T1/5, `2` T2/10, `3` T3/20, `4` T4/35, `5` T5/50 | +10/25/50/100/200%; no dodge/parry | type 2 ×1.1/1.25/1.5/2/3 | OK (hidden multiplicative stack) |
| **Energising Pain** — `PowerOfPain` T1/10, `2` T2/20, `3` T3/30, `4` T4/40, `5` T5/50 | +1 AP on hit; no parry AP | `ApGainAmount` 1.0 all | DESIGN (identical stacker) |
| **First Offensive** — `FirstOffensive` T1/5, `2` T2/10, `3` T3/20, `4` T4/35, `5` T5/50 | +10/25/50/100/200% first hit dealt/taken | type 2 ×1.1/1.25/1.5/2/3 | OK (hidden stack) |
| **Energising Turn** — `AP+1TurnStart` T2/20, `2` T3/30, `3` T4/40 | +1 AP turn start | `ApBonus` 1.0 all | DESIGN (identical) |
| **Empowering Jumps** — `EmpoweringJumps` T1/5, `2` T2/10, `3` T3/20, `4` T4/35, `5` T5/50 | +10/25/50/100/200% per jump counter | type 2 `1 + n×inc`; 0.1/0.25/0.5/1/2 | DESIGN (multiplicative stack) |
| **Critical Break** — `BreakDamageOnCrit` T1/5, `2` T2/10, `3` T3/15, `4` T4/20, `5` T5/25 | +10/20/30/40/50% Break on crit | `BreakDamageIncrease` 1.1/1.2/1.3/1.4/1.5 | OK (hidden stack) |
| **Burning Death** — `BurningDeath` T1/5, `2` T2/10, `3` T3/15, `4` T4/25, `5` T5/40 | 3/5/10/20/50 Burn on death | `BurnStackAmount` 3/5/10/20/50 | OK |
| **Energising Death** — `EnergyDeath` T1/5, `2` T2/10, `3` T3/15, `4` T4/20, `5` T5/25 | +1/+2/+3/+4/+5 AP on death | `GainApAmount` 1–5 | OK |
| **Random Defense** — `RandomDefense` T1/5, `2` T2/10, `3` T3/15, `4` T4/20, `5` T5/25 | 50% or 150% taken | **0.5–2.0× continuous; High row dead** | **TEXT (critical)** |
| **Confident Fighter** — `ConfidentFighter` T1/15, `2` T2/20, `3` T3/30, `4` T4/40, `5` T5/50 | +5/10/20/50/100%; can’t heal | type 3 additive 0.05/0.1/0.2/0.5/1.0 | OK |
| **Breaking Counter** — `BreakingCounter` T1/3, `2` T2/8, `3` T3/15, `4` T4/25, `5` T5/40 | +10/25/50/100/200% Break on counter | const 1.1/1.25/1.5/2/3 | OK (hidden stack) |
| **Healing Parry** — `HealingParry` T2/5, `2` T3/10, `3` T4/20 | Heal 3/5/10% on parry | const 0.03/0.05/0.10 | OK |
| **Perilous Parry** — `ManOfParry` T2/5, `2` T3/10, `3` T4/15 | Incoming doubled; +1 AP on parry | type 2 ×2.0; +1 AP; identical | DESIGN (×8 for 3 tiers) |
| **Empowering Parry** — `ReinforcementParade` T3/5, `2` T4/10, `3` T5/15 | +5/10/15% per parry | type 3 additive 0.05/0.1/0.15 | OK |
| **Lucky Aim** — `LuckyAim` T1/1, `2` T2/7, `3` T3/21 | 15% chance +1 AP on Free Aim | const 15% all | DESIGN (identical) |
| **Full Strength** — `Stand` T1/5, `2` T2/10, `3` T3/20, `4` T4/30, `5` T5/50 | +5/10/20/30/50% full HP | const ×1.05/1.1/1.2/1.3/1.5 | OK (hidden stack) |
| **Charging Critical** — `GradientOnCrit` T3/10, `2` T5/20 | 20% charge; I once/turn, II every crit | `GradientChargeOnCrit` 0.2 | OK |
| **Critical Chain** — `CriticalChain` T3/10, `2` T4/20, `3` T5/40 | +1/2/3% per crit, max 100 | type 3 additive 0.01/0.02/0.03, clamp 100 | OK |
| **Cursed Power** — `CursedPower` T2/5, `2` T3/10, `3` T4/15 | +5/10/20% while Cursed | type 3 additive 0.05/0.1/0.2 | OK (ST missing) |
| **Berserk Power** — `BerserkPower1` T2/5, `2` T4/10, `3` T5/15 | +5/10/20% while Berserked | type 3 additive 0.05/0.1/0.2 | **COST (tier)** |
| **Blighted Power** — `BlightedPower1` T2/5, `2` T3/10, `3` T4/15 | +5/10/20% while Blighted | type 3 additive 0.05/0.1/0.2 | OK |
| **Death Bomb** — `DeathBombPhysical` T1/1, `2` T2/5, `3` T3/10, `4` T4/25, `5` T5/50 | 0.5×/1×/2×/5×/10× on death | `FinalDamageMultiplier` 0.5/1/2/5/10 | OK |
| **Burning Skin** — `BurnOnHit1` T1/5, `2` T2/5, `3` T3/5, `4` T4/5, `5` T5/5 | “Gain 1 Burn on dodge (20–100%)” | 20/40/60/80/100% chance to self-apply Burn (donor retargeted Enemy→Character); 5 turns | OK (self-burn verified); cost flat |
| **Consuming Attack** — `ConsumingAttack` T4/10, `2` T5/25 | 100 Burns ×10% / 250 Burns ×5% | `MaxBurnConsumed` 100/250; `DamageMultiplierPerBurn` 0.1/0.05 | OK |
| **Triple Burn** — `DoubleBurn2` T5/50 | Every Burn tripled | `BurnMultiplier` 3.0 | OK |
| **Draining Cleanse** — `DispelOnAPConsume` T3/21 | Consume 10 AP to prevent status | `APConsumed` 10.0 | OK (cost high) |
| **Death’s Dividend** — `DeathsDividend` T1/15 | +10% per revive | type 3 additive `0.10 × ReviveCount` | OK (cost high) |

### Created support / status / special

| Family / ids | Built text | Actual effect | Status |
|---|---|---|---|
| **Dead Energy II** — `BootyHunter` T2/7 | Gain 5 AP on kill | `DeadEnergyII_ApAmount = 5.0` | OK |
| **At Death’s Door I** — `LastStand` T1/5 | +50% below 10% HP | vanilla effect; rename-only | OK |
| **Beneficial Contamination I** — `BeneficialContamination` T2/15 | +2 AP on applying a status, once/turn | vanilla effect; rename-only | OK |
| **Anti Dizzy / Curse / Exhaust** — T4/5 | Prevents X | `BP_BattleBuff_PreventBuff_*` | OK |
| **Revive with Shell / Rush** — T3/5 | Apply X 3 turns on revive | applies to all allies via `BP_LU_ReviveWithStatusOnAllie`; text omits “all allies” | TEXT (minor) |
| **Revive with Curse / Berserk / Blight** — T3/3 | Apply X 3 turns on revive | same; cost 3 | COST/TEXT |
| **Revive Bomb Powerless / Defenceless / Slow** — T3/5 | Inflict X on all enemies 3 turns | all enemies; Slow likely 2 turns | LIKELY (Slow) |
| **Defense Riser Dodge / Power Riser Dodge** — T3/5 | On dodge, gain Shell / Powerful | on-dodge buff | OK |
| **Critical Havoc** — T2/5 | Next hit crit after a kill | vanilla BP | OK |
| **Empowering Death** — T3/5 | Powerful to all allies on death | vanilla BP | OK |
| **Cursing / Berserking / Blighting Death** — T4/5 | Allies gain X on death | status swap | OK |
| **Auto Berserk / Auto Blight** — T5/1 | Self apply X on battle start | status swap; 3 turns | OK (T5/1 outlier) |
| **SOS Curse / Berserk / Blight** — T2/5 | Apply X below 50% HP | status swap; 5 turns | OK (duration omitted) |
| **Painter conversion** — Fire/Ice/Lightning/Earth T4/10, Dark/Light/Physical/Void T5/10 | Convert all Skill damage to X | `NewElement` retargeted | OK (tier split) |
| **Infinite** — T−/10 | Max Health 3,333,333 | `NewHPValue`/`NewMaxHpValue` 3,333,333 | OK (cost outlier) |
| **Super Sonic** — T−/100 | +100% Speed each turn | Speed ×2.0 status, reapplied; Speed stat ×3 curve (buff multiplier inferred from `SpecialPictosKit`; not CDO-parsed here) | OK* |
| **Rupture Shot** — T−/100 | Free Aim can inflict Inverted | 50% / 5 turns | OK (numbers omitted) |
| **Uno Puncho** — T−/21 | First hit 10×, rest 0 | type 3 `+9.0`, then `−1,000,000` | OK |
| **Alicia’s Suffering** — T−/100 | +1000% Burn damage | type 3 `10.0` = +1000% | OK |
| **Layered Canvas** — T−/100 | 1 of seven statuses for 1 turn each turn | seven turn-start appliers, duration 1 | OK |
| **Ultima Power** — T−/5 | “Fully unlocks your power.” | conditional damage-cap unlock after >9,999 received; ST stale | TEXT |

---

## Recommended fixes (not applied)

### Priority 0 — correctness

1. **Restore `ParrySpecialist*_CounterDamageMultiplier` to `2.0`.** **[Applied post-audit; costs remain 5/10/20.]** The row is a direct multiplier (`ByteConst 2`/`NewEnumerator3`), so `1.0` is ×1. Update the smoke test to assert `2.0`. Re-check the related `WepFx_CounterDmg2/3` rows too: if their tooltips are +200%/+300%, the multiplier values should be `3.0/4.0`, not `2.0/3.0`.
2. **Fix Random Defense.** Easiest: change the tooltip to the vanilla “between 50% and 200%” and leave the BP. Better: rewrite `ProcessReceivedRawDamages` to read `RandomDefense_HighMultiplier` and `SelectFloat` 0.5/1.5, matching the intended 50%/150% text.
3. **Burning Skin economy:** the text and 20/40/60/80/100% chance ladder are correct (self-burn is intentional). Re-cost the tiers (e.g. 5/10/15/20/25) instead of 5 flat, or accept that T5 is a free upgrade over T1.
4. **Update stale ST entries:** `PASSIVE_StunBoost_Description` → 10%, `PASSIVE_Roulette_Description` → 150%, `PASSIVE_OverPowered_Name/Description` → Ultima Power text.
5. **Teamwork text:** restore “while all allies are alive” (and not alone) or change the BP condition if the intent is “not alone” only.
6. **Revive Bomb Slow:** verify duration; change text to 2 or patch the buff to 3.

### Priority 1 — ladders and costs

7. **François’s Shell:** set `DamageReceivedMultiplier` to `0.8/0.7` for II/III (mirroring the dealt penalty), or reduce the costs so higher tiers are not strictly worse. The current 0.9/0.9/0.9 is almost certainly a copy-paste.
8. **Glass Cannon:** either raise `DamageMultiplier` to `1.5/2.0` for II/III, or lower their costs/received penalties. As built, II/III are pure downside upgrades.
9. **Parry Helper:** add `ParryHelper = 15` (or 20) to `LuminaCosts` so the base is not 30; use a monotonic 15/20/25 or 20/25/30.
10. **Berserk Power:** move `BerserkPower2` to Tier3 and `BerserkPower3` to Tier4 in `item_tiers.json`.
11. **Painter conversions:** normalise all eight to the same tier, or document the element-based rarity split.
12. **Revive status costs:** make Curse/Berserk/Blight 5 lumina like the other revives, or reduce the others to 3; add “to all allies” to the text.

### Priority 2 — design/cost

13. **Document stacking on identical families** (Augmented Aim, Solo Fighter, Roulette, Random Defense, Energising Pain, Perilous Parry, Energising Turn, Lucky Aim) or differentiate the tiers. At minimum add “Stacks with other tiers.”
14. **Revisit flat-cost ladders:** Burning Skin (5 each), Longer Burn (15 each), Break Damage (10/20/30 for the same +25%), Augmented Aim (1/3/5/10/21 for the same +10%), Solo Fighter (1/6/11), PowerOfPain (10…50), Lucky Aim (1/7/21).
15. **Revisit T5/1-lumina Auto statuses and the 5/10-lumina Infinite/Ultima Power.** If they are meant as endgame trophies, document that; otherwise raise costs.
16. **Correct the docs:** `custom-pictos.md` says Stun Boost tiers “compound multiplicatively”; they are additive. It also implies the Parry ladder fix is correct; it is not.
17. **Add “stacks multiplicatively”** to Full Strength, Best Defense, First Offensive, Breaking Counter, Critical Break, Empowering Jumps, and Confident if multi-tier wear is intended.

### Priority 3 — polish

18. Add the missing durations/targets to SOS, Auto, Rupture, and Revive texts.
19. Add `PASSIVE_CursedPower_*` ST entries for consistency (even though the row is inline).
20. Re-check `Pictos_ItemStats` on the special pictos; several rows have placeholder HP/Spd 1.0 values (`OverPowered`, `Infinite`). The HUD reads `DT_PictosDefinitions`, so this is cosmetic.

---

## Appendix — key evidence

### Modifier type dumps

```
BP_LU_Teamwork (vanilla donor)     ByteConst 2, DoubleConst 1.1   -> "10% increased"
BP_LU_GlassCanon (vanilla)         ByteConst 2, DoubleConst 1.25  -> "25% more"
BP_LU_ParryApX2DamageReceivedX2    ByteConst 2, DoubleConst 2.0   -> "damage received is doubled"
BP_LU_StunBoost (vanilla)          ByteConst 3, row 0.3           -> "30% increased"
BP_LU_Immaculate (vanilla)         ByteConst 3, row 0.3           -> "30% increased"
BP_LU_UnoPuncho                    ByteConst 2, row 10.0 / 0.0    -> "10x" / "0x"
BP_BattleDamages.ApplyCriticalHitModifiers
                                   ByteConst 2, DoubleConst 1.5   -> log "Crit: x1.5"
```

### Random Defense

```
BP_LU_RandomDefense.ProcessReceivedRawDamages
  NameConst RandomDefense_LowMultiplier      (only NameConst in the function)
  Multiply_DoubleDouble(Low, 100)
  FTrunc
  RandomIntegerInRange(FTrunc, 100)
  Divide_DoubleDouble(..., 100)              -> 0.5..1.0
  Divide_DoubleDouble(1, ...)                -> 1.0..2.0
  RandomBool -> SelectFloat                  -> overall 0.5..2.0
```

### Burning Skin

```
BP_LU_DodgeCanInflictStatus_BurnOnHit1..5
  DoubleConst 0.2 / 0.4 / 0.6 / 0.8 / 1.0
  PatchBurnKismetRec: K2Node_CustomEvent_Enemy -> K2Node_CustomEvent_Character
  at the ActivateBuff target -> the wearer gains the Burn.
  (The vanilla BurningDodge text says "Burn the attacker", but the mod
   intentionally retargets it. The built text "Gain 1 Burn" is correct.)
```

### Teamwork

```
BP_LU_Teamwork.ProcessDealtRawDamages
  GetAllPlayerCharacters.Length > 1
  DeadPlayerCharacters.Length == 0
  -> BooleanAND -> damage modifier 1.05 / 1.10 / 1.15
```

### Ultima Power

```
BP_LU_Overpowered.ExecuteUbergraph
  OnCharacterReceivedDamages
  ReceivedDamages > 9999
  -> DamageLimitUnlockRefCount.AddReference
```

### Commands used

```
dotnet <CLI> dump work/mod/.../DT_PassiveEffects.uasset --out passives.json
dotnet <CLI> dump work/mod/.../DT_Balancing_Luminas.uasset --out balance.json
dotnet <CLI> dump work/mod/.../DT_jRPG_Items.uasset --out items.json
dotnet <CLI> dump work/mod/.../DT_PictosDefinitions.uasset --out defs.json
dotnet <CLI> st work/mod/.../ST_PassiveEffects.uasset > st.txt
dotnet <CLI> dumpkismet <BP_LU_*.uasset>
dotnet <CLI> dumpasset <BP_LU_*.uasset>
```

No `rebuild-mod`, `extract`, `extract-suspend`, `pictos-smoke`, or packer command was run. `work/mod`, `work/tables`, and `~mods` were not modified.

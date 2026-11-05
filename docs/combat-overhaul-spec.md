# Combat Overhaul Spec — Stances, Windows, Pictos, Burn, Counters

Status: **proposed** — values reviewed, implementation pending. All mechanics verified against
extracted game assets (v1.5.4) with the E33 Mod Editor tooling.

This spec covers five balance changes folded into the editor's always-on combat layer
(`GameplayTweaksKit` + `CombatBalanceKit` + `PictosMagnitudeKit`), wired into both the GUI
`rebuild-mod` and the standalone generator.

---

## 0. Baseline references (verified from assets)

| Mechanic | Vanilla value | Where | Mod value (current) |
|---|---|---|---|
| Base attack (full combo) | 1.0× of Attack Power | `AC_jRPG_ActionsManager.Attack` kismet ratio `0.5/(hits)` | **0.5× AP** — basics-only nerf: hero/enemy skills bypass `Attack()` (`MakeDamageBuilder`+`DealDamages`), so skills are untouched; enemies roll tier bands on all stats, attacks on a hotter band (T1 1.20–1.30 / T2 1.35–1.50 / T3 1.55–1.70 / T4 1.75–2.05 / T5 2.15–2.45); the tier follows the **area band** (`LootCatalog.AreaTierForEnemy`: chest/LevelScaling lookup, explicit `AreaTierOverrides`, Act 3 / Frozen Hearts remaps, boss rows never downgrade). **T1–T3 real boss rows double the attack bonus above 1.0**, so the same rolls become T1 1.40–1.60 / T2 1.70–2.00 / T3 2.10–2.40; T4/T5 bosses instead use the curve. Boss HP profile defaults are T1 ×1.5 / T2 ×2.5 / T3 ×10 / T4 ×25 / T5 ×50 (story rows can pin absolute targets). |
| Melee counter | 2.5× base | `DT_Balancing_MainCharacterSkills.Generic_MeleeCounter_BaseDamage` | 0.75× base |
| Ranged counter | 2.5× base | `DT_Balancing_MainCharacterSkills.Generic_RangeCounter_BaseDamage` | 0.75× base |
| Counter per-level | +2%/level | `Generic_CounterDamageIncreasePerLevel` | **+4%/level** |
| Dodge window | 0.22 s | `DT_Balancing_BattleDifficultySettings` `ImmuneDuration_Dodge_Normal/Hard` | 0.165 s (Normal/Hard) |
| Parry window | 0.15 s | `DT_Balancing_BattleDifficultySettings` `ImmuneDuration_Parry_Normal/Hard` | 0.1125 s (Normal/Hard) |
| Burn global mult | 0.5 | `BP_BattleBuff_BurnAggregator` CDO `BurnDamageMultiplier` | 0.165 (BurnMultiplier 0.33) |
| Burn per-stack mult | 1.0 | `BP_BattleBuff_Burn` CDO `BurnMultiplier` | unchanged |
| Burn player scale | 0.2 | `ComputeBurnDamageTotal` kismet DoubleConst | unchanged |
| Burn duration | 3 turns | both CDOs `TurnDuration` | unchanged |

Damage model (community-verified): `D = AP × skill% × ∏(buffs) × crit × mitigation`.
A basic attack ≈ AP (100%). Maelle's Virtuose stance is +200% = ×3 (this spec fixes that).

---

## 1. Maelle stance rework

Stances are buffs (`BP_BattleBuff_Stance_*`) spawned by `BP_StanceMechanicComponent.SwitchStance`
(byte: 0 stanceless, 1 Offensive, 2 Defensive, 3 Virtuous). Damage effects read rows from
`DT_Balancing_MainCharacterSkills`; the multiplier values are **multiplicative** (0.5 = take half).

| Stance | Vanilla | Target (user) | Implementation |
|---|---|---|---|
| Stanceless | no damage effect (empty BP) | take ×2.0, deal ×0.1 | ⚠️ **blocked** — the Stanceless buff has no damage handlers; adding new FunctionExport handlers does not round-trip in UAssetAPI (validated). Needs whole-asset clone + in-game test. |
| Defensive | take ×0.5 (only received handler) | take ×0.4 (−60%), deal ×0.5, **+1 AP per parry** | ✅ received row 0.5→**0.4**. ✅ **+1 AP per parry already implemented** — vanilla `ProcessAPChange` adds +1 when `ChangeReason == 4` (parry), same hook as the Energising Parry picto. ⚠️ dealt ×0.5 blocked (missing handler). |
| Offensive | deal ×1.5, take ×1.5 | deal ×2.5, take ×2.5, **+1 AP per hit** | ✅ both rows 1.5→**2.5**. ✅ **+1 AP per hit** via `StanceEffectsKit` (bytecode edit to `ProcessDealtRawDamages`: `TargetCharacterStats → GainAP(1)`). |
| Virtuous | deal ×3.0 (no received handler) | deal ×1.5, take ×0.5, **+1 berserk/turn** | ✅ dealt row 3.0→**1.5**. ✅ **+1 Berserk/turn** via `StanceEffectsKit` (new `OnCharacterTurnStart` override: super call + `SpawnObject(Berserk_C) → ActivateBuff` on owner). ⚠️ received ×0.5 blocked (missing handler). |

**Why some handlers are blocked:** adding a brand-new `FunctionExport` to a BP *cross-asset* (with
local-variable `LoadedProperties` + unversioned property serialization) writes but does not round-trip
in UAssetAPI. The **in-asset function clone** (copying an existing export within the same BP, keeping
all FPackageIndex valid) DOES round-trip — that's how `StanceEffectsKit` adds Virtuous's
`OnCharacterTurnStart`. The clone produces a CDO/class-schema mismatch that UAssetAPI flags on
re-read, but the shipped `BP_LU_DodgeCanInflictStatus_*` pictos have the identical artifact and work
in-game (the game deserializes against the asset's own class export, not the usmap) — so it needs a
real-game test before shipping.

**Implemented now:** Offensive ×2.5/×2.5 + **+1 AP per hit** (`StanceEffectsKit`), Defensive received
×0.4 (−60%) + **+1 AP per parry** (vanilla `ProcessAPChange`), Virtuous dealt ×1.5 + **+1 Berserk per
turn** (`StanceEffectsKit`).
**Deferred (needs game-side validation):** Stanceless both, Defensive dealt ×0.5, Virtuous received
×0.5.

Rows added to `DT_Balancing_MainCharacterSkills` (future, once handlers exist):
- `Maelle_StancelessStance_DMGDealtMultiplier` = 0.1
- `Maelle_StancelessStance_DMGReceivedMultiplier` = 2.0
- `Maelle_DefensiveStance_DMGDealtMultiplier` = 0.5
- `Maelle_VirtuoseStance_DMGReceivedMultiplier` = 0.5

**Interactions to watch:** the lumina passives `BP_LU_Stance_VirtuoseDoubleDMG` (×2 Virtuous
dealt) and `BP_LU_Stance_VirtuoseDoubleBurn` (×2 burn) stack multiplicatively — with Virtuous
now ×1.5, the double-damage passive yields ×3.0 (was ×6.0). Verify these still feel right.

---

## 2. Stricter base dodge/parry windows + Parry Helper II/III

Window formula (from `GetDodgeImmuneDuration`/`GetParryImmuneDuration` kismet):
`base × (1 + charMod) × (1 + globalMod)`. Picto modifiers are additive in the stack.

**Where the game actually reads the window (verified in `BP_jRPG_Character_Battle_Base`
kismet):** at spawn, `ApplyDifficultySettings` overwrites each character's instance
`DodgeImmuneDuration`/`ParryImmuneDuration` from `DT_Balancing_BattleDifficultySettings`
(rows `ImmuneDuration_{Dodge,Parry}_{Easy,Normal,Hard}`, picked by current difficulty:
byte 0 Easy/Story, 1 Normal/Expedition, 2 Hard/Expert). The CDO defaults are **not** the
operative values in battle — an earlier version of this spec patched the CDO and had no
effect (and its `ParryLockDuration` shrink was the only live change, which crashed on
edge-of-window parries; see below).

Picto ladder (each +22% window):

| Picto | Row | Vanilla | Mod |
|---|---|---|---|
| Dodge Helper | `LongerDodge_DodgeWindowIncrease` | +0.25 | +0.22 |
| Dodge Helper II | `Dodger2_DodgeWindowIncrease` | — | +0.22 |
| Dodge Helper III | `Dodger3_DodgeWindowIncrease` | — | +0.22 |
| Parry Helper | `ParryHelper_ParryWindowIncrease` | +0.25 | +0.22 |
| **Parry Helper II (new)** | `ParryHelper2_ParryWindowIncrease` | — | **+0.22** |
| **Parry Helper III (new)** | `ParryHelper3_ParryWindowIncrease` | — | **+0.22** |

Three helpers equipped scale the window by ≈1.667× (additive: 1+3×0.22).

**Target:** new base window = 0.75 × vanilla, so all three helpers land at ≈1.25 ×
vanilla (0.75 × 1.667).

| Window | Vanilla base | New base | All 3 helpers |
|---|---|---|---|
| Dodge immune | 0.22 s | **0.165 s** | 0.275 s (1.25× vanilla) |
| Parry immune | 0.15 s | **0.1125 s** | 0.1875 s (1.25× vanilla) |

Implementation:
- `DT_Balancing_BattleDifficultySettings` rows (staged by `GameplayTweaksKit.ApplyDefenseWindows`):
  `ImmuneDuration_Dodge_Normal`/`ImmuneDuration_Dodge_Hard` → 0.165,
  `ImmuneDuration_Parry_Normal`/`ImmuneDuration_Parry_Hard` → 0.1125.
  Both Expedition (Normal) and Expert (Hard) get the tuned values because they share
  vanilla timings; Story/Easy rows stay at their more lenient vanilla values (0.5 / 0.35).
- **`ParryLockDuration` is deliberately left at vanilla 1.2 s** — it is passed RAW to
  `StartDefensiveMove` (never picto-scaled) and it is the gate that stops a second parry
  from re-entering the native parry/counter state machine. The earlier CDO patch shrank it
  to 0.614 s while `ApplyDifficultySettings` kept the runtime window at 0.15 s (table), so
  ~0.11 s after the parry anim the lock cleared while the old parry was still finalizing —
  a re-parry in that edge window crashed the game. `TryStartParry` guards only on the lock
  (`CanDoDefensiveMove`), not on `IsParrying`, so the lock must stay longer than the anim.
- `PictosMagnitudeKit`: clone `ParryHelper` → `Parry Helper II/III` (exact `Dodger2/3` pattern: `BP_LU_ParryHelper2/3`, `DA_GearPictos_ParryHelper2/3`, rows `ParryHelper2/3_ParryWindowIncrease` = 0.22); tier-I vanilla rows retuned to 0.22 in `InjectBalancing`, all six tooltips reworded to 22%.
- New lumina costs: both helper ladders are **5 / 10 / 15** (Dodge Helper I/II/III and Parry Helper I/II/III).

Note: `ParrySpecialist` is now tier I of a Parry Specialist ladder (I/II/III each −10%
window, ×2 counter; balancing `ParrySpecialist{,_2,_3}_ParryWindowDecrease` = −0.10,
`ParrySpecialist{,_2,_3}_CounterDamageMultiplier` = 2.0 — serialized `FDamageModifier` type 2
(`NewEnumerator3`) is a **direct multiplier**, so 2.0 is the true ×2; the additive type is
serialized 3 / `NewEnumerator2`). Vanilla `ParrySpecialist` rows `-0.25`/`2.0` were the
ladder's old tier-I values; the ladder keeps the 2.0 counter and lowers the window penalty to
−0.10. `DodgeSpecialist` (−25% window) stays vanilla. Both become harsher against the tighter
base — that's the intent ("you need the helper pictos").

The tier-I rework must also rewrite the `ST_PassiveEffects` entries
(`PASSIVE_ParrySpecialist_Name/Description`) — the pictos tooltip reads that string table
directly, so row-inline FText alone left the in-game tooltip showing the stale vanilla text.
Other reworked vanilla pictos (Stun Boost, Roulette, Teamwork, Glass Cannon) share the same
latent ST gap; Parry Specialist is fixed, the rest remain as-is pending a follow-up.

---

## 3. François's Shell pictos (renamed from Titanium Shell; Glass Cannon inverse)

Glass Cannon reads `GlassCanon_DamageMultiplier` (dealt) / `GlassCanon_DamageReceivedMultiplier`
(received) from `DT_Balancing_Luminas`, applied in separate `ProcessDealt/ReceivedRawDamages`
handlers. François's Shell clones that BP with inverted values.

| Tier | Name | Dealt | Received | Lumina cost |
|---|---|---|---|---|
| I | François's Shell I | −10% (×0.9) | −10% (×0.9) | 10 |
| II | François's Shell II | −20% (×0.8) | −10% (×0.9) | 20 |
| III | François's Shell III | −30% (×0.7) | −10% (×0.9) | 30 |

Rows (new, `DT_Balancing_Luminas`):
- `TitaniumShell_DamageMultiplier` = 0.9, `TitaniumShell_DamageReceivedMultiplier` = 0.9
- `TitaniumShell2_DamageMultiplier` = 0.8, `TitaniumShell2_DamageReceivedMultiplier` = 0.9
- `TitaniumShell3_DamageMultiplier` = 0.7, `TitaniumShell3_DamageReceivedMultiplier` = 0.9

Descriptions (rephrased: "Take X% less, but deal Y% less"):
- I: "Take 10% less damage, but deal 10% less damage."
- II: "Take 10% less damage, but deal 20% less damage."
- III: "Take 10% less damage, but deal 30% less damage."

Assets: `BP_LU_TitaniumShell{1..3}` (clone of `BP_LU_GlassCanon` with name-map row retarget;
asset ids unchanged — only display names/descriptions changed),
`DA_GearPictos_TitaniumShell{1..3}`, item/passive/definition rows, string-table entries
(`ST_PassiveEffects`).

---

## 4. Burn nerf (relative to mod basic attack)

Mod basic attack = 0.5× AP. Burn tick (per stack, from kismet):
`base = BurnMultiplier × (Atk − Def/(1 + 2·Def/Atk))`, aggregated per child buff, scaled by
`ComputeBurnDamage(stack, base, finalMult)` where `finalMult` = 1.0 (enemy) / 0.2 (player).

**Anchor (user):** 20 stacks = 1 basic attack = 0.5× AP; 100 stacks = 3 basic attacks = 1.5× AP.

The only linear knob is `BurnMultiplier` (scales the aggregator's `BurnDamageMultiplier`).
Community data says burn scales roughly linearly with stacks up to a ~300 cap (defense
interaction was patched). **Target `BurnMultiplier` ≈ 0.075** (from vanilla per-stack ≈
0.5 × (Atk − Def/(1+2·Def/Atk)) ≈ Atk/6 at Atk≈Def; need 20 × per-stack = 0.5·Atk →
per-stack = 0.025·Atk → 0.025/(1/6) ≈ 0.15 relative to the 0.5 aggregator... see math note).

**Math note / open item:** the exact per-stack behavior of the native `ComputeBurnDamage`
(stack curve, defense term, cap) is not visible in kismet. The linear anchor is set so
**20 stacks = 0.5× AP exactly**. 100 stacks will land at ~2.5× AP under pure linearity —
**if the native function applies diminishing returns at high stacks, 100 may land closer to
1.5× AP; must verify in-game and adjust.** Player scale stays 0.2 (burn vs players is already
5× weaker) — the user confirmed it applies to players too, so the 0.2 player scalar remains.

`GameplayTweaksKit.Settings.BurnMultiplier` default: 0.33 → **0.075** (aggregator
`BurnDamageMultiplier` 0.165 → 0.0375).

---

## 5. Counter damage nerf

Counters: `Generic_MeleeCounter_BaseDamage` / `Generic_RangeCounter_BaseDamage`.
Target: counter = basic attack × 1.5. Mod basic attack = 0.5× base → **counter = 0.75× base**.

`CombatBalanceKit.CounterBaseDamage`: 2.0 → **0.75**.

| Counter | Vanilla | Mod current | Target |
|---|---|---|---|
| Melee | 2.5× | 2.0× | **0.75×** |
| Ranged | 2.5× | 2.0× | **0.75×** |

`Generic_CounterDamageIncreasePerLevel` 0.02 → **0.04** (+4%/level) via `CombatBalanceKit.CounterDamagePerLevel` — at 0.75 base it still scales
sensibly. `ParrySpecialist` (×2 counter) now yields 1.5× base (was 4×). AoE/Jump/Gradient
counters keep their distinct designs (`Generic_AOECounter_BaseDamage` 0.8, Jump 1.0, Gradient
3.5 are untouched).

### 5a. Counter weapon passive values (Increase Counter II/III)

**Corrected 2026-10-26 after the picto audit.** Serialized `FDamageModifier` type 2
(`NewEnumerator3`) is a **direct multiplier** (`base × value`), not the additive
`base × (1 + Σ values)` type. The additive type is serialized 3 (`NewEnumerator2`), used by
Stun Boost and Immaculate. Evidence: vanilla `BP_LU_Teamwork` type 2 with 1.1 = "10%
increased"; vanilla `BP_LU_ParryApX2DamageReceivedX2` type 2 with 2.0 = "damage received is
doubled"; `BP_BattleDamages.ApplyCriticalHitModifiers` type 2 with 1.5 logs `Crit: x1.5`.

The created weapon passives `WepFx_CounterDmg2/3` are advertised as +200%/+300% counter
damage, so they need direct multipliers of **3.0/4.0** (×3/×4). Those values are now applied
in both `weapon_effects.json` and the `work/weapon_effects.json` overlay. The previous
**2.0/3.0** rows under-delivered at +100%/+200%. Vanilla `IncreaseCounterDamage`
(1.5 = ×1.5, +50%) remains stronger than its text implies.

`ParrySpecialist` is a **different row** and correctly uses 2.0 for ×2; it is not ×3. The
ladder keeps **2.0** on all three tiers (see §2).

### 5b. Stance descriptions

The stance tooltip descriptions are **not** on the buff CDOs — they're string-table entries in
`ST_UniqueMechanics` (`UNIQUEMECHANIC_Maelle_{Virtuose,Defensive,Offensive,Stanceless}_Description`)
read by `WBP_StanceMechanicComponent.UpdateStanceUI`. Updated to match the reworked values
(vanilla voice, concise): Virtuose "+50% dealt / −50% taken / +1 Berserk per turn", Defensive
"−60% taken / −50% dealt", Offensive "+150% dealt / +150% taken", Stanceless "+100% taken /
−90% dealt". Applied via `CombatBalanceKit.PatchStanceDescriptions`.

---

## 6. Module wiring

All changes fold into the existing always-on combat layer:

| Change | Module | GUI | CLI |
|---|---|---|---|
| Stance values + BP handlers | `GameplayTweaksKit` (new `ApplyStanceRework`) | ✅ | ✅ |
| Dodge/parry base windows | `GameplayTweaksKit` (new `ApplyDefenseWindows`) | ✅ | ✅ |
| Parry Helper II/III | `PictosMagnitudeKit.Variants` | ✅ (PictosMagnitude toggle) | ✅ |
| François's Shell I–III (was Titanium Shell) | `PictosMagnitudeKit.Variants` | ✅ | ✅ |
| Burn multiplier | `GameplayTweaksKit.Settings.BurnMultiplier` | ✅ (existing) | ✅ |
| Counter damage | `CombatBalanceKit.CounterBaseDamage` | ✅ **fix: wire CombatBalanceKit into GUI pack** | ✅ (existing) |

**Fixes a latent bug:** the GUI "Apply & Pack All" does not currently run `CombatBalanceKit`,
so the base-attack 0.5× / counter 2.0× changes only applied via CLI `rebuild-mod`. Wiring it
into the GUI pack makes the GUI output match the CLI.

### 6a. Visages Mask Keeper (Gameplay Tweak)

`BP_EnemyBattle_MaskKeeper_VisagesPhase2` works out which of the three masks are alive from the persistent
flags `NID_JoyMaskDefeated` / `NID_AngerMaskDefeated` / `NID_SadnessMaskDefeated`
(`BP_GlobalVariablesFunctionLibrary.HasPersistentFlag` → `BP_jRPG_GI_Custom.GetPersistentFlagValue`, a
`Map_Find` on `NamedIDsStates` whose miss branch returns false; `LoadPersistentFlags` resolves saved GUIDs
through `FindNamedIDByGuid`). `UpdateDefeatedMasks` copies them into `{Joy,Anger,Sad}MaskDefeated`, which in
turn gate the mask turn-actions (`TurnStartAction_*Mask`), the mask actors bound by
`Bossfight.Visages.MaskActors.*` and `Skill_SummonMasks` — so every sub-boss beaten deletes its turn from the
boss's rotation.

`MaskKeeperKit` (toggle: Gameplay Tweaks ▸ Mask Keeper, `Settings.MaskKeeperFullMoveset`, default on)
re-points those three reads at `NID_VisagesMasksNeverDefeated`, a clone of the vanilla flag at a new package
path carrying its own GUID. Nothing can set it, so every lookup misses and the moveset stays full. The trio is
untouched — still fought, still drops, and the island's progression still gates on the real flags. The masks
help the boss (Anger grants extra turns, Joy heals, Sadness inflicts Exhaust), so on is the harder fight.

The edit is an import/name-map rename inside the boss BP: no kismet statement is inserted, deleted or resized,
so every bytecode offset is unchanged. The clone's GUID is stamped byte-wise in the `.uexp` because UAssetAPI
re-serialises `GuidPropertyData` from its parsed inner data and drops the property write (`MaskKeeperKit.Source`
extracts come from `work/tables`, so they are staged once, not re-extracted per pack).

Visages content is otherwise pinned to **T3**: all `MF_` rows in `LootCatalog.AreaTierOverrides` (the
`manyfaces`/`axons` actor folders are shared with Renoir's Drafts / Chosen Path / Frozen Hearts rows, so this
is an explicit row list, not an area key), Blooraga `DT_Merchant_Visages` = 3, and the two Axon rows take a
flat **×21 HP/Experience/Chroma** via `RandomizerKit.StoryHpOverride`. `MF_Axon_MaskKeeper_VisagesPhase2` had
been catalogued as cut content (level 1), which excluded the real final fight from both stats and drops; it is
now level 60 and randomised. The island's three chromatic **bosses** are the exception to the T3 pin:
`MF_Boucheclier_ALPHA` (Isle of the Eyes, L61), `MF_Chapelier_ALPHA` (The Crows, L65) and
`MF_Moissonneuse_ALPHA` (Endless Tower, L99) are fought in T4/T5 endgame areas, so they are not pinned and
take the boss curve at their own tier — see `docs/boss-audit.md` §7.

---

## 7. Verification checklist

- [x] `tweaks-smoke` / `pictos-smoke` / `skill-desc-smoke` / `skill-overhaul-smoke` pass
- [x] Stance rows read correctly by the buff BPs (Offensive/Defensive/Virtuous rows verified in staged table)
- [x] Dodge/parry windows: staged `DT_Balancing_BattleDifficultySettings` Normal+Hard rows = 0.165 / 0.1125 s; Story rows untouched; 3 helpers land at ≈1.25× vanilla (0.275/0.1875 s)
- [x] François's Shell + Parry Helper II/III: BP/DA/item/passive/balancing/string-table rows all staged
- [x] Burn aggregator staged = 0.0375 (`BurnMultiplier` 0.075)
- [x] Counters staged = 0.75× base (melee + ranged), +4%/level scaling
- [x] GUI pack now runs `CombatBalanceKit` (was CLI-only — latent bug fixed)
- [ ] In-game: burn 20 stacks ≈ 0.5× AP; 100-stack behavior (linear vs native curve)
- [ ] In-game: Virtuous berserk-per-turn (deferred — needs handler surgery)
- [ ] In-game: Stanceless/Defensive-dealt/Virtuous-received stance effects (deferred)

## 8. Open questions / next steps

1. **Stance BP surgery (Stanceless, Defensive dealt, Virtuous received + berserk):** adding new
   FunctionExport handlers doesn't round-trip in UAssetAPI. Next step: whole-asset clone of the
   Offensive buff (which has all handlers) retargeted to each target buff, then test in-game — the
   CDO/class-schema mismatch UAssetAPI flags may not matter to the game (it uses the asset's own
   class export). If the game tolerates it, the remaining stance effects are one clone + row retarget
   each.
2. **Virtuous + `VirtuoseDoubleDMG` passive = ×3 dealt** (1.5 × 2.0); keep or re-tune the passive?
3. **Stricter base windows apply to enemies too** (same difficulty table, read by every battle
   character's `ApplyDifficultySettings`) — currently global across both sides of a fight.
4. **Burn 100-stack behavior** — linear (≈2.5× AP) vs native curve; verify in-game and adjust
   `BurnMultiplier` if the native `ComputeBurnDamage` has diminishing returns.
5. **CLI `rebuild-mod` crashes in `LuneApocalypseKit.DarkPower`** (pre-existing, GUI unaffected) —
   separate fix needed for CLI/`rebuild-mod` parity.

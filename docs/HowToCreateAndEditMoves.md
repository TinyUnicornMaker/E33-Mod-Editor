# How to create and edit moves

This is the only move-authoring guide for the E33 Mod Editor. Follow it. Do not invent a second pipeline.

Hard problems (kismet size shifts, stain maps, Dark icons, graph append, FolderName) already live in the kits. Your job is to pick a donor, fill a kit spec, and reuse those helpers.

```
CLI=src/E33ModEditor.Cli/bin/Release/net10.0/E33ModEditor.Cli.dll
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
```

Pack command: `dotnet $CLI rebuild-mod`. It wipes `work/mod`, runs every kit, and installs `E33ModEditor_P` into `Paks/~mods`. Do not hand-edit `work/mod` and expect the next pack to keep it unless the kit restages that file from vanilla every run.

---

## 0. Absolute rules (read first)

1. **Clone a donor that already does the same job.** Same hit count, same targeting, same sequence family. Do not loop a 1-hit clip to fake extra hits.
2. **Never write or pack a LevelSequence.** UAssetAPI cannot round-trip them (possessables, Niagara, hit-sound tracks). Open+Write of `SEQ_*` or `FX_ElementalCharge` crashes **battle load / new game**. Keep the donor's vanilla sequence. Point the cloned BP at it.
3. **Never synthesize `EX_InstanceVariable` / `EX_LocalVariable` FFieldPaths** (`new FFieldPath(new[] { new FName(asset, "CurrentCharacter") }, new FPackageIndex(0))`). The VM treats those as no-ops. Clone the pointer from a statement in the **same** Blueprint that already uses it (GainAP, SetHP, ApplyBuff, SpawnObject).
4. **After any kismet insert/delete:** `MayhemFixKit.ShiftAbsolute` (insert) or `LuneSkillsKit.ShiftAbsoluteSigned` (delete) **and** `MayhemFixKit.RecalcContextSkips`. `EX_Context` needs a non-null `RValuePointer` (`new KismetPropertyPointer(new FFieldPath())`) or the writer NREs.
5. **`asset.FolderName`** = `/Game/` + path under `Content/` without extension. Wrong FolderName → `retoc to-zen` drops the file as a duplicate of the donor (the skill vanishes).
6. **Surgical `RetargetPairs` only.** Blanket “replace IceGust with Chiaroscuro” across a sequence/BP smashes `SEQ_*` / VFX names that contain the stem.
7. **Do not pack rewritten always-loaded vanilla assets unless the CDO parsed.** Unversioned Blueprint CDOs only parse when parent schemas sit on disk under one Content root (`PictosCreateKit.EnsureExtractedShared`). Isolated Open+Write of `BP_BattleBuff_Stance_*` crashed new game. Unique new package paths are load-safe.
8. **Kismet math functions** (`EqualEqual_ByteByte`, …) must be imported as siblings of `NotEqual_ByteByte` (`EnsureKismetMathFn`). A fake `/Script/Engine` class import crashes Blueprint link on save load.
9. When something is ambiguous (new character, new widget, new kismet shape), stop. Wrong bytecode in a skill script or sequence DirectorBP is worse than a missing feature.

---

## 1. What a skill is (keep every piece in sync)

| Piece | Path pattern | Role |
|---|---|---|
| Data Asset | `DA_Skill_<Char>_<NameId>` | AP, NameID, descriptions, `SkillScript` (SoftObject), `SkillComponentList` (StainsMechanic), `BaseElementalType`, `TargetingType`, `SkillTags` |
| Skill script BP | `BP_Battle_SkillScript_<Name>` | `OnActionEffect` / `ExecuteUbergraph` — damage, consume, generate, buffs |
| LevelSequence | `SEQ_Skill<Char>_<Name>` | animation, FX, voiceline, hit events. **Do not write.** |
| Skill graph | `DA_SkillGraph_<Char>` | tree node + prerequisite edge |
| Balancing rows | `DT_Balancing_MainCharacterSkills` | `NameId_BaseDamage`, overcharge mult, burn stacks, … |
| Icon row | `DT_SkillIcons` | **required** or the node is invisible |
| String table | `ST_MainCharacters_Skills` | `<Char>_<NameId>_Name` / `_Description_Long` / `_Description_Short` |

Character folders:

| Character | Internal name | Skills / graph |
|---|---|---|
| Lune | Lune | `.../Lune/`, `DA_SkillGraph_Lune` |
| Maelle | Maelle | `.../Maelle/`, `DA_SkillGraph_Maelle` |
| Gustave | **Noah** | `.../Gustave/` skills, graph `NoahVerso/DA_SkillGraph_Noah` |
| Verso | Verso | `.../Verso/` |
| Sciel / Monoco | Sciel / Monoco | matching folders |

Work dirs: `work/tables/` (vanilla extracts), `work/mod/` (staged overrides), `Export/` + `Paks/~mods/` (packed).

Inspect a DA:

```bash
dotnet $CLI extract DA_Skill_Lune_Wildfire --out work/tables
dotnet $CLI dumpasset work/tables/.../DA_Skill_Lune_Wildfire.uasset
dotnet $CLI dump-stains work/tables/.../DA_Skill_Lune_Wildfire.uasset
dotnet $CLI dumpkismet work/tables/.../BP_Battle_SkillScript_Wildfire.uasset OnActionEffect
dotnet $CLI dumpimports path/to/BP.uasset
dotnet $CLI kismet-check path/to/BP.uasset
```

---

## 2. Kits and toggles (where work actually lives)

| Kit | Toggle / when it runs | What it is for |
|---|---|---|
| `LuneSkillsKit` | Skill Overhaul `Character.Lune` gate | New Lune skills: clone DA+BP+rows+icon+ST+graph node. `NewSkill` + `KismetEdits` |
| `LuneApocalypseKit` | same gate | Dark Power, Angel's Gift, Ultima, Ultima Noir |
| `MayhemFixKit` | same gate | Mayhem Dark consume / tier / `2^dark` scaling |
| `GustaveSkillsKit` | Skill Overhaul `Character.Gustave` gate | Gustave grid cleanup, rebalance, Zoom Zoom / Hell Storm / Cinder Swarm / Exploding Shot |
| `MaelleSkillsKit` | Skill Overhaul `Character.Maelle` gate | Maelle AP/element/renames/kismet (Death's Bargain, Flame Eater, …) + **new moves** (`MaelleSkillsKit.NewMoves`: Lumière Above, Revers Fatal, Déviation Absolue, For Those Who Come After, Vengeance Ardente, Alicia's Nightmare, Blade Waltz, L'Acte Final Incendiaire — donor clones with floating graph nodes) |
| `SkillOverhaulKit` | `work/skill_overhaul.json` | Per-character gates (`Character.Gustave/Lune/Maelle`, absent = on) + per-move toggles on vanilla cells; applied last. A gated-off character reverts (cells pinned to vanilla, exclusive staged files deleted). Legacy `work/lune_skills.json` / `work/maelle_overhaul.json` are inert. |
| `SkillDescriptionsKit` | always | Gold `N.NNx` multipliers in `ST_MainCharacters_Skills` |
| `SkillGraphKit` | `work/skill_graph.json` | Visual tree layout (Graph Editor). Active shape packs last |
| `StainsMechanicKit` | used by the skill kits | Read/write DA consume/produce maps |
| `StainUiKit` | wired from Lune kit | Dark stain HUD icon (`T_HUD_Icon_Element_DARK`) |
| `CombatBalanceKit` / `StanceEffectsKit` | always | Global combat + Maelle stance values/effects |

Pack builds use the CLI `rebuild-mod` or the standalone generator. After a C# change, rebuild the CLI; a running GUI keeps old assemblies.

The packed skill tree comes from the Skill Graph editor's **active layout** for each character — that module is the only thing that decides node positions and links on pack.

---

## 3. Damage numbers

`D = AttackPower × skill% × ∏(buffs) × crit × mitigation`.

`BaseDamage` in the balancing table is **per hit**. Total ≈ `BaseDamage × hits × overchargeMult`.

### Clamp curve (`SkillOverhaulKit.Clamp`)

```
clamp(ap) = 0.33·ap          if ap ≤ 9
          = 3·e^(0.24·(ap−9))  if ap > 9
```

- **Lune / Gustave damage** → total at max = `clamp(AP)` unless a stated exception.
- **Maelle damage** → total at max = `0.75 × clamp(AP)` unless a stated exception.
- **Maelle description convention** → multi-hit moves quote the **per-hit** multiplier (the `*_BaseDamage` row), phrased "… damage per hit. N hits." Single-hit moves quote total (= per-hit). Conditional scaling states the exact rate.
- **Maelle burn-scaling rule** → every move that scales damage off Burn uses **+1% per Burn** (row `0.01`), whether it consumes Burn or reads the target's stack.
- **Gradient attack damage rule** → a damage-dealing gradient's total = **10 × gradient charge cost** (DA `APCost`), split across hits (per-hit row = total ÷ hits). No curve, no overcharge. Every gradient in the mod follows it: Quake 3→30, Meteor 7→70, Sabotage 1→10, Striker 3→30, Angel's Eyes 7→70, Golgra 3→30, Gommage 10→100, Shadow Bringer 2→20, Doom 4→40, End Slice 9→90, Mighty Strike 3→30, Break Point 8→80.
- **Conditional damage phrasing** → describe a conditional boost as a **multiplier** ("deals N.NNx more damage"), never as the absolute resulting value. Covers overcharge, stance, and Monoco's mask (`*_OverchargeDamageMultiplier`) bonuses.

### Lune overcharge split (set every damage move unless exempt)

1. **Four-elemental hard requirement** (Genesis family: Fire+Ice+Lightning+Earth, no Light substitution) → **×1.2 over the clamp**.
2. **Consumes Dark** → **+25% additive per Dark** over the clamp. Mayhem is exempt (`2^dark` in kismet).
3. **Consumes elemental / Light** → at the clamp **when overcharged**. Split by stain count: 1 stain = 30% from overcharge (×1.43), 2 = 50% (×2.0), 3 = 70% (×3.33), 4 = 90% (×10.0).  
   `overchargeMult = 1 / (1 − share)`  
   `basePerHit = clamp × (1 − share) / hits`

Exempt (do not re-derive): Mayhem/Blitz (tiered), Fire Rage family (ramp), Terraquake / Typhoon (zone), Thermal Transfer (OC = +4 AP + second turn), Elemental Trick (no stains), heals, gradients.

Storm-zone rule (Storm Caller / Maelstrom only): **every Lightning strike = clamp(AP)/16** — `SkillOverhaulKit.StormCallerPerStrike` (3 AP → 0.0619), `SkillOverhaulKit.MaelstromPerStrike` (10 AP → 0.2384). Rows, kit BalancingRows and descriptions all reference these fields (descs via `SkillDescriptionsKit.Gold`) so values and text can never drift apart.

Earth break: non-gradient earth moves `BreakBarMultiplier` = **1.3** (Crustal Crush uses `BreakBarBaseMultiplier`). Full Lune table: `Planning/Lune_Moves_Current.xlsx`.

Write rows with `DataTableEditor`: clone the donor row, then set `FloatValue`. `SkillOverhaulKit` pins disabled changes back to vanilla.

---

## 4. Changing parameters (AP, DA fields, balancing)

### Data Asset fields (`dumpasset` on the DA)

| Field | Type | Meaning |
|---|---|---|
| `APCost` | int | AP (or gradient charges on gradient skills) |
| `NameID` | name | Icon lookup + graph stem. Must match the new skill id |
| `Name` / `Description` / `ShortDescription` | FText | Often string-table keys (`Lune_Wildfire_Name`). Kits write ST entries and point the DA at them |
| `BaseElementalType` | `EAttackType` | Display + some damage paths. See §5 |
| `TargetingType` | `E_jRPG_TargetingType` | Who the UI lets you aim at. See §7 |
| `SkillType` | enum | Skill vs Gradient Attack |
| `SkillScript` | SoftObject | `/Game/.../BP_Battle_SkillScript_<Name>_C` |
| `SkillTags` | GameplayTagContainer | e.g. `SkillInfos.SkillEffect.Heal` vs `.Buff` — Heal tag makes the UI offer **allies** even if TargetingType is self |
| `DynamicElementOverride` | enum | Weapon-element moves. **Do not force a fixed element byte** on this family |

Gradient charge cost = DA `APCost` (also mirrored to graph `UnlockCost` when a Graph Editor shape packs). Fractional gradient progress is a float `GradientChargeAmount` on some BPs (Phantom Strike).

### Balancing table

Rows the BP actually **reads** (dumpkismet `NameConst` / `GetDataTableRowFromName`). Typical names:

- `<Id>_BaseDamage` — per hit
- `<Id>_OverchargeDamageMultiplier`
- `<Id>_BreakBarMultiplier` / `_BreakBarBaseMultiplier`
- status amounts: `_BaseBurnStacks`, `_BurnMultiplier`, `_MarkedDamageMultiplier`, …

Clone the donor's rows, retarget names, set values. A row the BP never reads does nothing.

### AP cost vs graph cost

Tune **DA `APCost`**. Graph `UnlockCost` historically lags; `SkillGraphKit` copies DA AP onto graph nodes when a shape packs. Kit-created skills should set both at create time.

---

## 5. Changing element

Two places must agree (except weapon-element donors):

1. **DA `BaseElementalType`** — `EAttackType::NewEnumeratorN`
2. **Skill-script `MakeDamageBuilder` ByteConst** — kismet ordinal (scan **every** function export; Lightning Dance builds damage in `DealDamageAndFeedback`, not `OnActionEffect`)

`LuneSkillsKit.KismetEdits.DamageElement = "Light"` does both. `MaelleSkillsKit` patches DA + `MakeDamageBuilder` the same way.

### Enumerator table (`EAttackType`)

| Display | Enumerator | Kismet `ByteConst` |
|---|---|---|
| Invalid / empty stain slot | `NewEnumerator9` | 0 |
| Physical | `NewEnumerator6` | 1 |
| Fire | `NewEnumerator0` | 2 |
| Ice | `NewEnumerator1` | 3 |
| Lightning | `NewEnumerator2` | 4 |
| Earth | `NewEnumerator3` | 5 |
| Dark | `NewEnumerator4` | 6 |
| Light | `NewEnumerator5` | 7 |
| Void | `NewEnumerator7` | 8 |

Verify on a new game version with `dumpasset` / `dumpkismet` before patching.

### Weapon-element family (do not force a byte)

Donors that call `GetBaseDamageType()` from the **equipped weapon**: Fencer's Flurry, Momentum Strike, Offensive Switch, Sword Ballet, Gustave Combo1 / Strike Storm clones (Hell Storm, Cinder Swarm).

Patching `MakeDamageBuilder`'s element byte on these **crashes the move in-game** even when `kismet-check` is clean. Leave the element as weapon. Descriptions: “Uses weapon's element” / `{DynamicElement}`.

### FX vs damage element

Damage byte ≠ visuals. Visuals live in the **vanilla sequence** you are not allowed to write. Optional BP-level `ExtraPairs` can retarget Niagara **only to assets that exist** (`FX_ExplosionLight`, `FX_WorldLightTrail`, `FX_ExplosionVoid`, `FX_ExplosionEarth`, …). Missing Niagara (`FX_PermeateLight`) crashes sequence load.

**Spawning an FX actor from a skill BP** (following swords, etc.): do not hand-build the spawn. Clone the analogous weapon passive's own routine (`BP_LU_DoubleHit.SpawnFollowingSword` shape: `MakeTransform` → `BeginDeferredActorSpawnFromClass` → `SetObjectPropertyByName`/`SetDoublePropertyByName` → `FinishSpawningActor`). Keep the transform in an `FTransform` local (96B) — see §13.

---

## 6. Stains (generate vs consume)

There is **no** DA map named `RequiredStains`. That name is a **UI widget property**.

On the DA child export `BP_SkillDataComponent_StainsMechanic_C`:

| Map | What it does | Player sees it |
|---|---|---|
| `OverchargeRequiredStains` | stains **consumed** to overcharge (Genesis: hard cast requirement) | description body `Consumes <img id="Stain_*"/>` |
| `ProducedStains` | stains **generated** on cast | **top-right skill-button icons**, not the description |

Set via `StainsMechanicKit.WriteFile` / `NewSkill.OverchargeRequiredStains` / `ProducedStains`. Keys: `"Fire"` / `"Ice"` / `"Lightning"` / `"Earth"` / `"Dark"` / `"Light"` (or `NewEnumeratorN`).

**Never** write “Generates …” into Long/Short text. Vanilla never does. Empty produce map = no top-right icons.

```bash
dotnet $CLI dump-stains
dotnet $CLI dump-stains path/to/DA_Skill_Lune_Chiaroscuro.uasset
```

Typical `OnActionEffect` order:

1. `TryConsumeOverchargeStainsFromSkill` (consume map)
2. optional extra `CreateStainByType` (Electrify Light on overcharge)
3. `GenerateStainsFromSkill` (produce map)
4. damage

Light stains (byte 7) can substitute for missing required types in three places. With the wildcard toggle **off** (default), `LuneSkillsKit.Apply` makes all three strict (verified by `lune-heal-smoke`):
1. `BP_StainHolderComponent.HasStainQuantities` (availability gate) — lookup byte 7 → 9.
2. `BP_StainHolderComponent.FindStainsObjectsByQuantities` (backs the native `TryConsumeOverchargeStainsFromSkill` consume) — fallback bytes 7 → 9.
3. Per-BP `ValidateSkillAvailability` — byte 7 → 9 (`KismetEdits.NoLightSubstitution` for clones; vanilla Elemental Genesis is staged + patched since only its DA was in the mod). Light-only requirements still work: the exact-type match handles Light as a normal key.

The Skill Overhaul → Lune card has a **Light stains substitute for missing stain requirements** toggle. Off (default) applies the strict patches 1–2; On skips them and removes any strict holder left in a reused mod tree, restoring vanilla wildcard behaviour. Patch 3 is always on for Elemental Genesis / Elemental Cataclysm, so those keep the exact four-element requirement in either mode. Mayhem also still never consumes Light (see `MayhemFixKit.PatchExcludeLight`).

The current 1.3 English 654321 pack has this toggle **ON** (`work/skill_overhaul.json` → `Option.LightStainWildcard: true`). The installed pack therefore has no `BP_StainHolderComponent` override; vanilla Light substitution is used. `lune-heal-smoke` checks the wildcard-on mode.

Why byte 9, never byte 0: empty stain-queue slots ARE byte 0 (`GetFirstAvailableSlotIndex` finds them with `Equal(Array_Get, 0)`), and `Get Active Stain Count By Type` counts all nine enumerator indices 0–8 — so a lookup of 0 returns the EMPTY-slot count and substitution stays alive (this caused the always-overcharged Healing Light). Key 9 is absent from that map, so `Map_Find` yields 0 and the check is strict. Same reason the consume fallback uses 9: consumed queue slots are marked 0, so searching for 0 would double-spend eaten stains.
4 Dark can overcharge when `CanOverchargeWithDarkStains` is set.

### Dark consume

Native `TryConsumeOverchargeStainsFromSkill` does **not** reliably eat Dark. Replace it with `holder.ConsumeStainByType(Dark, n)` (`KismetEdits.CustomDarkConsume` / `CustomConsume = "Earth|Earth|Dark"`). The holder is `CallFunc_GetComponentByClass_ReturnValue*` on `CurrentCharacter`.

**Never** read `Self.ReverseStainQueue`. That InstVar exists only on Mayhem. Using it on Ice Gust (Chiaroscuro) crashes the VM at cast.

Dark had **no** texture in `WBP_GenericElement_StainIconeOnly` (byte 6 → `EX_NoObject` → white square). `StainUiKit.PatchDarkIcon` points it at `T_HUD_Icon_Element_DARK` (wired from `LuneSkillsKit.Apply`). Do not put Dark in `ProducedStains` without that patch.

### Either-or generate (Chiaroscuro)

- `ProducedStains` still lists **both** types so the widget can draw them.
- `KismetEdits.RandomGenerate = "Light|Dark"` replaces `GenerateStainsFromSkill` with a `RandomBool` branch of `CreateStainByType`.
- Slash between icons: `MechanicStem = StainUiKit.EitherOrMechanic`. Do not put the slash in the description.

Skills with **no** StainsMechanic (extra consume lives only in kismet): Elemental Trick (crits generate the hit's element), Mayhem (queue consume in `MayhemFixKit`).

To author different stains: clone a donor that **already has** a StainsMechanic, then pass the maps on `NewSkill`. If the donor script has extra `CreateStainByType`, patch that `ByteConst` separately.

---

## 7. Changing targets

**Pick a donor that already aims at the right people.** Targeting is DA + kismet + tags. Changing only the DA enum leaves `FillTargets` / `GetTargetedCharacters` / ally loops doing the donor's job.

Known DA `TargetingType` values used in this repo:

| Enumerator | Used as |
|---|---|
| `NewEnumerator1` | Single enemy (Ice Gust / Ultima) |
| `NewEnumerator2` | Self (Dark Power, SpeedUp/GuardUp shape) |

Confirm with `dumpasset` on the donor before copying.

**SkillTags override the picker.** Dark Power kept `TargetingType=NewEnumerator2` (self) but the donor Heal tag made the game offer **allies**. Rewrite the GameplayTagContainer to `SkillInfos.SkillEffect.Buff` (do not empty it — empty containers break `ToString`). Heal skills keep the Heal tag.

Party AP (Angel's Gift): donor with an ally array (`TargetedCharacters` / Mezzo Forte pattern). Unroll a 2-ally `Array_Get` + `GainAP` using **cloned** locals from that donor. Do not invent `GetTargetedCharacters()` as a phantom virtual.

AoE vs single: clone Wildfire/Thunderfall (AoE) vs Ice Gust/Stendhal-style (single). Hit count must match the sequence's `BattleKeyframe` count.

---

## 8. Statuses (buffs / debuffs)

Two runtime paths. Using the wrong one looks like “it ran but nothing applied.”

| Path | Use for | How |
|---|---|---|
| `ApplyBuff(class, target, turns, …)` | Plain statuses the donor already applies (Burn, Mark, Slow, Defenseless, Blight on many skill scripts) | Clone the donor's **own** `ApplyBuff` statement. Swap `ObjectConst` class + duration `IntConst`. Keep every FFieldPath |
| `SpawnObject(Buff_C, stats)` then `stats.ActivateBuff(spawned, ByteConst 4, stats, success)` | Statuses with **custom ActivateBuff** that must register hooks — especially **Cursed** on a **player**. Auto Curse / Death's Bargain | Clone a working ActivateBuff/SpawnObject from the **same** BP (or the stats context from that BP's GainAP/SetHP). Set `TurnDuration` via `SetIntPropertyByName` **before** ActivateBuff — SpawnObject instances are **not** seeded from the CDO, so duration 0 = invisible |

**Cursed on a player does not bind through bare `ApplyBuff`.** Every vanilla BP that curses a player uses SpawnObject+ActivateBuff. Blight on Lune Ultima Noir **does** bind through cloned `ApplyBuff` (10× the donor statement, self target, already-declared out-local). Do **not** synthesize SpawnObject→ChangeStackCount→ActivateBuff with new undeclared locals (VM crash on overcharged Ultima Noir).

Helpers already in kits:

- `KismetEdits.StatusOnOvercharge = "Slow|Defenseless|Powerless"`
- `KismetEdits.StatusesOnHit` + `StatusesOnHitTurns` + `RemoveNativeStatus`
- `KismetEdits.DefenselessTurns`
- Death's Bargain: clone Last Chance `GainAP` stats context, then Auto Curse shape
- Gustave Zoom Zoom: `SwapBuffClass` retargets Powerful → Rush after clone (broad row retarget invents a nonexistent `_125` class)

Buff class paths (examples):

| Status | Package |
|---|---|
| Burn | `/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Burn` |
| Mark | `BP_BattleBuff_Mark` (dump the donor) |
| Slow / Greater Slow | dump the donor ApplyBuff |
| Defenseless | dump the donor |
| Blight | `/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Blight` |
| Cursed | `/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Cursed` |
| Stun | `/Game/Gameplay/Buffs/GenericBuff/BP_BattleBuff_Stun` |
| Rush (Speed) | `/Game/Gameplay/Buffs/ParentClasses/BP_BattleBuff_Speed` |
| Powerful | `BP_BattleBuff_Powerful_125` |

Always `EnsureObjectImport` the `_C` class **and** often `Default__<Class>_C` (SpawnObject resolves the archetype through the package).

Unique buff clones (1-turn Virtuose Berserk, Maelstrom zone buff, Fire Rage interrupt stun): clone the whole buff BP to a **new package path**, patch the CDO (`TurnDuration`, `IsPermanent`) while the parent (`BP_BattleBuffInstance`) sits on the same extract root so the CDO parses, then retarget the skill BP at the clone. Do not `SetPermanent`/`SetTurnDuration` as synthesized virtuals on a spawned instance — that crashed Maelle battle load.

Self vs enemy: clone a statement that already targets self (`CurrentCharacter.AC_jRPG_CharacterStats`) or already targets the enemy (`BattleDamages` / `GetCurrentTarget`). Do not guess.

---

## 9. Sequences, hits, voicelines

**Do not clone, Open, or Write LevelSequences.** The cloned skill BP keeps the donor's vanilla `SEQ_*`. `NewSkill.SequenceStem` / `SequenceNewStem` in comments are historical — packing a rewritten sequence crashed new game.

Implications:

- Hit count = donor sequence hit count. Use a real N-hit donor.
- Voiceline / camera / FX = donor's. Accept that, or pick a different donor.
- `RemoveVoice: true` (DirectorBP `PlaySkillBattleLineInternal`) is only safe if you also subtract that statement's size from every custom-event `EntryPoint` IntConst. Deleting the call without retargeting offsets kills the skill. Prefer not touching the sequence at all.
- `StandardSkillExecution` ubergraph flags must match hit count. Super Spark copied Fury's 3-hit clip but left Spark's flags: only the first keyframe fired `OnActionEffect`.
- Hit-key byte: Spark-family hits are `50`; other tracks use `2`. Do not invent keys.

---

## 10. Names, icons, graph, packing

### Strings

`ST_MainCharacters_Skills`: `<Char>_<NameId>_Name`, `_Description_Long`, `_Description_Short`.

Vanilla description shape: damage line, then `Consumes <img id="Stain_*"/>:` overcharge effect. Gold multipliers: `<keyword id="DamageMultiplier">1.32x</>` (`SkillDescriptionsKit`).

**Keyword wrapping:** only Burn, Mark, Stunned have standalone `StatusEffect_*` rich-text styles. Wrapping Blight/Cursed/etc. as `<keyword id="StatusEffect_Blight">` renders as an **empty gap**. Write those as plain words (“Blight”, “Cursed”).

### Icons

`DT_SkillIcons` row named **NameID**. Staging path must be `Resources/DataTables/` (capital T). `work/tables` extracts `Datatables`; that ships but **never loads** → no icon → hidden node. Clone the donor row, keep the texture unless you have a new one.

### Graph

Append a node + edge from a real prerequisite (`LuneSkillsKit` skips append if the node already exists — do not wrap the whole `Create` in “if file exists, skip”).

`IsSecret=false`, empty `RequiresUnlockItem`, reachable prereq. Place on the lattice (`SkillGraphKit`: columns `100+125·i`, rows `125+100·j`). Kit extras historically used Y=125, X = 725 + 125·index so they sit off the vanilla tree.

Gustave: remove hidden `Gustave_SkillTreeUnlock` / cut-content nodes from `DA_SkillGraph_Noah` (`GustaveSkillsKit`). Do not un-gate Maelle's painter Void quest skills.

### Pack

```bash
dotnet $CLI rebuild-mod
# confirm the new package is in the packed container
# retoc unpack-raw Export/E33ModEditor_P.utoc /tmp/unpacked
# grep the new DA/BP stems in the manifest
dotnet $CLI dump-stains work/mod/.../DA_Skill_Lune_<New>.uasset
dotnet $CLI kismet-check work/mod/.../BP_Battle_SkillScript_<New>.uasset
```

In-game: node visible, generate icons top-right, consume icons in the body, skill plays, no new-game / battle-load crash.

Kits that restage from vanilla every run (Death's Bargain, stance BPs) overwrite `work/mod` copies. Kits that `if (!File.Exists(dest)) copy` will keep a stale broken BP — **always FileCopy from extract** when you are replacing a kismet patch.

---

## 11. Recipe: new skill (Lune — copy this for other characters)

1. Pick a donor with the same hits / targeting / sequence length / stain-mechanic presence.
2. Add a `NewSkill` in `LuneSkillsKit.Skills` (Gustave: `GustaveSkillsKit.NewMoves`; **Maelle: `MaelleSkillsKit.NewMoves`** — a `MaelleNewMove` record, same clone anatomy but Maelle paths + floating graph nodes).
3. Set `DaStem`, `BpStem`, `NameId`, donors, `ApCost`, display strings, `BalancingRows`, `PrereqNameId`, `Position` (Maelle: `Position` only, no prereq → the node floats until the user links it).
4. Set `OverchargeRequiredStains` / `ProducedStains`. Do not write generate sentences in the text.
5. Description: vanilla shape; gold multipliers; consume `<img>`; plain words for Cursed/Blight.
6. Element: `KismetEdits.DamageElement` **unless** the donor is weapon-element.
7. Status / Dark consume / random generate: existing `KismetEdits` flags. Do not write new kismet from scratch. Clone statements from the same BP.
8. `ExtraPairs` for FX path swaps (full path pair **first**, then the object name).
9. `FolderName` on DA and BP. Icon row casing. Graph append.
10. Set the Skill Overhaul character gate on (GUI card toggle, or `Character.<Name>: true` in `work/skill_overhaul.json`; absent = on). `dotnet $CLI rebuild-mod`.
11. `dump-stains` + in-game: node, icons, cast, no crash.

Maelle / Gustave: same anatomy. Maelle damage = 0.4×clamp. Gustave graph is Noah. Do not force Fire on Hell Storm / Cinder Swarm. Maelle clone donors must be Maelle-native (cross-character imports crash battle load); a Maelle new-move that needs a Virtuose (CurrentStance==3) gate in a donor that lacks one should clone Percée's proven gate via `MaelleSkillsKit.ClonePerceeVirtuoseGate` (never synthesize InstVar FFieldPaths, never copy FProperties cross-BP).

---

## 12. Recipe: edit an existing move

1. Extract the vanilla DA + BP (`dotnet $CLI extract <stem> --out work/tables/<stem>`).
2. Stage with `FileCopy.AssetPair` from that extract **every pack** (do not keep a previous broken write).
3. Change DA fields (`APCost`, `BaseElementalType`, `TargetingType`, tags) and/or balancing rows.
4. For kismet: `dumpkismet` the function, find a donor statement in **this** BP, `CloneKismet`, swap `ByteConst` / `ObjectConst` / `NameConst` / `IntConst` only.
5. Insert with `ShiftAbsolute` + `RecalcContextSkips`. Delete with `ShiftAbsoluteSigned`.
6. Pack. If it crashes on **new game**, you packed an unroundtrippable override (sequence, Niagara, stance CDO). If it crashes on **battle load with that character**, leftover Maelle paths in a Lune BP, or a rewritten always-loaded component. If the effect is a **no-op**, you synthesized FFieldPaths.

---

## 13. Crash / no-op catalogue (repo-proven)

| Symptom | Cause | Fix |
|---|---|---|
| New game crash | Written `SEQ_*` or `FX_ElementalCharge` | Do not pack them |
| New game crash | Stance buff BP written with unparsed CDO | Extract `BP_BattleBuff_Stance_Base` beside it; `AssertCdo` parsed; unique clones OK |
| Save-load crash | Fake `EqualEqual_ByteByte` as `/Script/Engine` class | `EnsureKismetMathFn` clones `NotEqual_ByteByte` |
| Battle load crash (character in party) | Cross-character leftover imports (`/Maelle/` in a Lune BP); synthesized `SetPermanent` on a spawned buff | Stay on same-character donors; unique buff CDO instead of instance virtuals |
| Cast VM crash | Undeclared local / synthesized FFieldPath; `Self.ReverseStainQueue` on non-Mayhem; `SwitchStance` on Lune (null component) | Clone declared locals; Dark consume via StainHolder; excise stance blocks on Lune |
| Skill vanishes from tree | Wrong `FolderName`; `DT_SkillIcons` under `Datatables` | `/Game/...` FolderName; `Resources/DataTables/` |
| Node invisible | Missing icon row | Clone `DT_SkillIcons` by NameID |
| White Dark stain icon | Byte 6 → `NoObject` | `StainUiKit.PatchDarkIcon` |
| Curse/status does not apply | `ApplyBuff` on player-Cursed; synthesized stats InstVars | SpawnObject+ActivateBuff with **cloned** GainAP stats context |
| Weapon-element clone crashes on use | Forced `MakeDamageBuilder` element byte | Leave weapon element |
| Sequence plays donor VO/FX | Shared vanilla sequence (required) | Pick a different donor if VO is unacceptable |
| Cast CTD spawning an FX actor | Spawned class import doesn't resolve (wrong name / folder casing) | Confirm the package exists (`findassets`); clone the class import from the analogous vanilla passive, casing included |
| Cast CTD (struct local) | `FStructProperty` local declared with the wrong `Struct`/`ElementSize` — e.g. an `FTransform` written into a 24-byte Vector slot, corrupting the VM stack | Measure the real size (`fn-locals <bp> <fn>`; FVector = 24, FTransform = 96) and declare the local with that struct |

---

## 14. Kismet editing (how to do it without breaking the VM)

1. `dumpkismet <bp> <FunctionName>` — find the statement.
2. Clone that statement (`MayhemFixKit.CloneKismet`). Locals/InstVars stay valid **in the same function**.
3. If you copy a statement into **another** function, retarget every `EX_LocalVariable` `FFieldPath.ResolvedOwner` to that function's export index and copy the `LoadedProperties` entries by **actual local names** used in the clone (`EnsureLocals`).
4. Instance variables (`CurrentCharacter`, `CurrentStance`) are class-owned (`ResolvedOwner` = class export). Cloning them between functions of the **same** BP is OK. Synthesizing them is not.
5. Swap only: `EX_ByteConst` (element, stain type, stance byte 0/1/2/3), `EX_ObjectConst` (buff class), `EX_NameConst` (balancing row), `EX_IntConst` / `EX_DoubleConst` (duration, amount).
6. `MakeDamageBuilder` element = first matching `ByteConst` in that call (confirm against a known Fire=2 donor).
7. Insert before `EX_Return` or immediately after a proven statement (GainAP, DealDamages). Compute insert offset as the sum of `GetSize` of prior statements. `ShiftAbsolute` then insert. After insert, if you added a `JumpIfNot`, set `CodeOffset` to the **actual** offset of the skip target by walking the new bytecode (`PointJumpIfNotAtReturn` pattern).
8. Do not `ShiftAbsolute` a huge function (`SwitchStance`) unless you have proven the PushExecutionFlow table still lands on the right cases. OnTurnEnd calls `SwitchStance(Stanceless)` every turn — a bad insert there ran Virtuose logic every turn.
9. `dumpimports` after adding classes. `kismet-check` before pack.

---

## 15. Do not

- Edit `work/mod` by hand as the source of truth.
- Raw-poke StainsMechanic byte `0x16` — `StainsMechanicKit` parses the maps.
- “Fix” a crash by deleting more sequence statements.
- Use Mayhem as a kismet template unless the donor **is** Mayhem.
- Pack `BP_BattleBuff_Stance_Base` / `BP_BattleBuffInstance` (extract them only so child CDOs parse).
- Keyword-wrap Cursed/Blight/Shell in skill text.
- Force a fixed element on DynamicElementOverride / `GetBaseDamageType()` donors.

---

## 16. Where the code is

| Task | File |
|---|---|
| New Lune clone spec | `src/E33ModEditor.Core/LuneSkillsKit.cs` (`NewSkill`, `KismetEdits`) |
| Lune Apocalypse (Dark Power / Gift / Ultima) | `src/E33ModEditor.Core/LuneApocalypseKit.cs` |
| Mayhem | `src/E33ModEditor.Core/MayhemFixKit.cs` |
| Gustave | `src/E33ModEditor.Core/GustaveSkillsKit.cs` |
| Maelle overhaul | `src/E33ModEditor.Core/MaelleSkillsKit.cs` |
| Stain maps | `src/E33ModEditor.Core/StainsMechanicKit.cs` |
| Shared kismet walk/clone/shift | `MayhemFixKit.Walk` / `CloneKismet` / `ShiftAbsolute` / `RecalcContextSkips` |
| Pack order | `src/E33ModEditor.Cli/Program.cs` `rebuild-mod` |
| Lune numbers spreadsheet | `Planning/Lune_Moves_Current.xlsx` |

Character-specific balance targets (Maelle 0.4×, stance rework, named rethemes) live in `docs/maelle-overhaul-spec.md` and the kit comments. Super Spark (parked Spark test) post-mortem: `docs/super-spark.md`.

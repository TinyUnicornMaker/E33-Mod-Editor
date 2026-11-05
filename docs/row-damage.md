# Back-row damage

The Party of Five formation is positional only: the battle-spot indices are
anchors on one line, and the two rows are created by the per-spot forward/right
offsets. This module adds a damage rule on top of that geometry:

| Row | Damage dealt | Damage taken |
|---|---:|---:|
| Front | ×1.0 | ×1.0 |
| Back | ×0.8 | ×0.8 |

Enemies are always ×1.0. The rule is only applied when Party of Five is on and
`UseFormationOffsets` is true.

## Rows come from the offsets, not the spot numbers

`RowDamageKit.BackSpots` marks a spot as back when its `Fwd` offset in
`work/party_five.json` is negative (placed away from the enemies). With the
default formation:

| Party position | Spot | Final X | Row |
|---|---:|---:|---|
| 1 | 2 | −141.5 | front |
| 2 | 0 | −141.6 | front |
| 3 | 3 | −276.9 | back |
| 4 | 1 | −276.4 | back |
| 5 | 4 | −332.5 | back |

So the default back spots are **`{1, 3, 4}`**. Retuning the offsets retunes the
row set automatically; there is no hard-coded `0/1/2/3` order.

## Settings

`work/row_damage.json`:

```json
{
  "Enabled": true,
  "FrontDamageDealtMultiplier": 1.0,
  "FrontDamageTakenMultiplier": 1.0,
  "BackDamageDealtMultiplier": 0.8,
  "BackDamageTakenMultiplier": 0.8
}
```

Set `Enabled` to `false` to ship the vanilla damage rule. Front-row values are
configurable too, so a harsher front line can be set without code changes.

## Implementation

`RowDamageKit` patches `AC_jRPG_CharacterBattleStats.ReceiveDamageFromObject`,
immediately after `ProcessReceivedRawDamages` and before the final
`ApplyActiveModifiers`. It adds one `FDamageModifier`:

- `Type = 2` (direct multiplier)
- `AffectsBaseDamages = true`
- value = attacker row multiplier × target row multiplier

The attacker is `local_DamageSourceCharacter`; the target is the component's
owner. Both are read through `BP_jRPG_Character_Battle_Base.BattleSpotIndex`,
and `Enemy?` neutralises the rule for enemy characters. All arithmetic is
double math fed by int selects (`SelectInt` + `Conv_IntToDouble`), so no
float/double coercion is involved.

The existing received-damage passives (Glass Cannon, Confident, François’s
Shell, etc.) use the same modifier path, so the row multiplier stacks
multiplicatively with them.

## Verification

```bash
dotnet $CLI row-damage-smoke
```

The smoke test checks:

- the default formation yields back spots `[1,3,4]`;
- a custom formation flips the set to follow the offsets;
- the staged asset contains the row modifier, `SelectInt`/`Conv_IntToDouble`
  calls, and the two spot/team reads;
- every context offset and jump target is valid;
- the patch composes with `GameplayTweaksKit`’s consecutive-turns edit on the
  same asset.

`row-damage-smoke` also verifies the expression tree numerically for every
front/back and enemy combination.

## Caveats

- **Both back row.** A back-row attacker hitting a back-row target applies both
  penalties: `0.8 × 0.8 = 0.64`. If that double-dip is not wanted, the source
  or target term can be dropped.
- **Self-damage.** A back-row character taking self-damage gets the taken
  penalty (and the dealt term also applies because source == target).
- **Burn/DoT.** The target’s taken multiplier applies to burn ticks. A
  back-row caster’s dealt penalty only affects ticks whose damage builder keeps
  that caster as the source; if burn ticks are attributed to the target, they
  are not reduced. Direct attacks, counters and skills are covered.
- **Changing the multipliers needs a clean rebuild.** `rebuild-mod` wipes
  `work/mod` and re-patches from vanilla. An incremental pack that finds an
  existing row block keeps it and fails verification if the configured values
  changed, rather than silently shipping the old multipliers.
- **In-game test still required.** The asset-level verification and byte-stable
  round-trip pass, but the patch has not been run in the game yet.

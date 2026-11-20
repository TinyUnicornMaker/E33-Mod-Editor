# Weapon Drop Levels

Weapon-only drop-level scaling. Pictos remain vanilla: the shared loot
formula is untouched, and the picto scaling table is never staged.

## Why the shared formula is not changed

`FL_LootSystemHelpers.GetItemLevelFromLootContext` is shared by every gear
instance:

```text
itemLevel = Clamp(
    Ceil((EncounterLevel + LootContextLevelOffset) / 3.0),
    1,
    GetGearMaxUpgradeLevel())
```

Changing the `3.0` would raise pictos too. `WeaponDropLevelKit` leaves that
function vanilla and adds a second stage only where the runtime consumes the
result for weapons.

## The three runtime sites

| Site | Asset | Role |
|---|---|---|
| `BP_ItemInstance_Gear.GenerateItem` | `BP_ItemInstance_Gear` | Normal item-add path: enemies, chests, bosses, randomizer, merchants |
| `BP_ItemInstance_Gear.ChangeLevelWithLootContext` | `BP_ItemInstance_Gear` | Direct loot-context path |
| `AC_jRPG_BattleManager.Helper_TryAddRolledLootToMap` | `AC_jRPG_BattleManager` | Battle reward pre-filter |

The BattleManager site is required. It compares the owned gear level against
the vanilla drop level (≤33). With 34+ weapons, an unpatched pre-filter drops
the reward before `GenerateItem` can run.

## Weapon discriminator

The existing virtual `GetScalingDataTable()` is the gate:

- `BP_ItemInstance_Gear_Weapon` returns `DT_EquipmentScalingTable_Weapons`
- `BP_ItemInstance_Gear_Pictos` returns `DT_EquipmentScalingTable_Pictos`

The gear paths also require `IsLootContextValid(LootContext)` so merchant and
story grants with `EncounterLevel = -1` keep their vanilla level. The
BattleManager pre-filter reuses its existing `ItemType == 1` (weapon) bool.

## Scaling formula

```text
scaledLevel = Clamp((vanillaLevel * MaxDropLevel) / 33, 1, MaxDropLevel)
```

Default `MaxDropLevel` is **222**, so:

| Vanilla item level | Scaled |
|---:|---:|
| 1 | 6 |
| 10 | 67 |
| 20 | 134 |
| 33 | 222 |

The formula is integer-only; the kit reuses `Multiply_IntInt`, `Divide_IntInt`
and `Clamp`, and adds no locals or function exports.

## Setting

`work/weapon_drop_level.json`:

```json
{
  "Enabled": true,
  "MaxDropLevel": 222
}
```

- `MaxDropLevel` is clamped to `33…333`.
- `33` is vanilla and stages nothing.
- `Enabled: false` skips the kit entirely.

## Wiring

`FullPackKit.RebuildAndPack` calls `WeaponDropLevelKit.Apply` right after
`WeaponOverLevelKit.Apply`, before `WeaponBulkUpgradeKit` and before
`PartyFiveKit`. Party of Five reuses the already-staged BattleManager copy, so
both patches compose.

The kit assumes `DT_EquipmentScalingTable_Weapons` already has rows up to
`MaxDropLevel`; `WeaponOverLevelKit` (or the standalone over-level kit) must run
first. It only raises the level the game asks for — it does not create scaling
rows.

## Verification

```bash
CLI=src/E33ModEditor.Cli/bin/Release/net10.0/E33ModEditor.Cli.dll
dotnet $CLI drop-smoke
```

`drop-smoke` stages a scratch copy, applies the 222 scale to all three sites,
verifies the discriminator/gate/scaling and the jump geometry, re-applies to
prove idempotence, and confirms `MaxDropLevel: 33` stages nothing.

## Notes

- Pictos are never raised above 33 by this kit. `DT_EquipmentScalingTable_Pictos`
  and `FL_LootSystemHelpers` are deliberately not staged.
- Early-game weapons scale too: with 222, a vanilla level-1 drop becomes 6 and
  a vanilla level-10 drop becomes 67. Lower `MaxDropLevel` if that is too strong.
- If the third-party Bigger Party Mod compatibility path strips
  `AC_jRPG_BattleManager` (Party Five disabled), the pre-filter patch must be
  applied to that mod's own BattleManager as well.

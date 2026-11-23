# Expedition Infinity — changes from 1.4.5 to 1.4.6

1.4.6 is a loot-tier and data-ownership repair. Gameplay balance values are
unchanged from 1.4.5.

## Weapon tier repair

The randomizer's weapon pools now include the previously quarantined weapon
families:

| Family | Rows | New tier | Count |
|---|---|---:|---:|
| Reacher variants | `Reacharo_1/2`, `Reacherim_1/2`, `Reacheron_1/2`, `Reacheso_1/2`, `Reachum_1/2` | **T4** | 10 |
| Siren variants | `Sirenaro_1/2`, `Sirenim_1/2`, `Sirenon_1/2`, `Sirenum_1/2`, `Sireso_1/2` | **T3** | 10 |
| `VD_*` DLC variants | `VD_Maelle_1/2`, `VD_Lune_1/2`, `VD_Monoco_1/2`, `VD_Sciel_1/2`, `VD_Verso_1/2/3` | **T5** | 11 |
| Baguettes | `DebugSciel`, `DebugLune`, `DebugVerso`, `DebugMaelle`, `DebugMonoco` | **T1** | 5 |

The five Baguettes also receive a narrow roster-gate exception so Sciel's and
Monoco's T1 Baguettes can be placed before those characters' normal join
tiers.

Weapon tier totals are now T1 **23**, T2 **21**, T3 **37**, T4 **29**, T5
**22**. `DoNotUse` is reduced to **3** rows:

- `03_Weapon_Placeholder`
- `MitigatedPerfection`
- `Milerim`

## One weapon-tier source of truth

`src/E33ModEditor.Core/Loot/Data/item_tiers.json` is now the only weapon-tier
source. The previous `work/item_tiers.json` overlay was removed.

- `ItemTiers.Load()`, `ResetToStock()` and `Save()` use the Core data file.
- `LootCatalog` reads the same `ItemTiers.Flat` data instead of parsing a
  second copy.
- `StoryPointKit` reads `ItemTiers` instead of a separate overlay path.
- The embedded resource remains only as a read-only fallback for packaged
  installs that do not carry the source tree.

Removed without creating a backup:

- `work/item_tiers.json`
- `archive/src-1.3.6/E33ModEditor.Core/Loot/Data/item_tiers.json`
- `backups/item_tiers.*`

## Verification

- `dotnet test`: **35/35 passed**
- `loot-audit`: **clean** — every tier id resolves
- `rando-smoke`: **411 passed**, with the same 6 unrelated pre-existing
  Paintress/Sirene stat and missing-archetype-table failures
- Local Normal-seed build (seed `87694963`): **`retoc verify: verified`**

The regenerated weapon chart contains **132 tiered weapons**, **125
randomizable**, **7 fixed** starters/quest rewards, and **11 cut-content
weapons**.

## Pack names

Seed packs use `_v1.4.6_P`.

## Release status

This is a source update. No tag, release artifact, or uploaded seed pack is
created by this change.

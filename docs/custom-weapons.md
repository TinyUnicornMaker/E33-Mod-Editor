# New weapons (including new models) — not built yet

Notes only. There is no weapon-creator module. Custom **effects** are
`docs/custom-weapon-effects.md`. Custom **pictos** are `docs/custom-pictos.md`.

## What a vanilla weapon is

135 weapons. Item row name = definition row name.

| Piece | Asset | Role |
| --- | --- | --- |
| Identity | `DT_jRPG_Items` **parent** | Runtime lookup. Extra rows on `DT_jRPG_Items_Composite` are wiped on load. |
| Stats / passives / elements | `DT_WeaponDefinitions` | Shared 1–33 curve × `BaseStatsMultipliers`; chroma / Free Aim multipliers; up to 3 attribute grades; `BaseDamageType` / `FreeAimDamageType`; `Level1Lumina`…`Level5Lumina` (slots unlock at the tier ends 33/66/111/222/333; `Level5Lumina` is added by `WeaponPassiveSlotKit`). Every slot is empty on vanilla weapons. |
| Display | `Item_DisplayName` / `ItemDescription` | Culture-invariant FText for new ids (same locres trap as pictos). |
| Owner | `Item_Subtype` | Lune, Maelle, Sciel, Gustave, Verso, Monoco. |
| Gear DA | `DA_WeaponPictos_<Char>_<Id>` class `BP_DataAsset_Weapon_C` | One field: `WeaponSkin` → a skin Blueprint. `Weapon_SkeletalMesh` / `Pictos_SkinTexture` on the item row are empty. |
| Look | `BP_WeaponSkin_<Char>_<Id>` | Static mesh components (blade / handle / gun), Niagara trails. Gustave and Verso share the Noah skin family (`BP_WeaponSkin_Noah_Lanceram`). |
| Inventory | save `InventoryItems` + `WeaponProgressions` | Save Editor catalog comes from `DT_WeaponDefinitions`. |
| Placement | Chests / Drops / Tier Editor | Separate from a creator, same rule as pictos. |

Clone a neighbour of the **same character**. Retarget `UAsset.FolderName` on
every DA and skin clone or IoStore collapses them (Accelerating Heal class of
bug).

## v1 without new art (still not built)

Clone item + definition + DA, **reuse** an existing `BP_WeaponSkin_*`. New id,
name, stats, elements, `WepFx_*` or vanilla passives, old look. That is enough
to put extra weapons in the save editor and in chests.

## New models / textures

Lanceram’s skin references pieces such as:

- `/Game/Characters/Heros/Noah/Weapons/SM_Gustave_Weapon_Blade_Lanceram_01`
- `SM_Gustave_Weapon_Dagger_Lanceram_01`
- `SM_Gustave_Weapon_Gun_Lanceram_01`

plus trail / FX components. Socket / component names include
`Weapon_L_SM_Blade`, `Weapon_L_SM_Handle`, `Weapon_R_SM_Blade`,
`Weapon_R_SM_Handle`.

### Export (out of the game)

`retoc to-legacy` already extracts the packages. This repo’s CUE4Parse can
**read** static meshes and textures. It does not include CUE4Parse.Conversion
(glTF / PSK / PNG writers). Practical export today is FModel: mesh → glTF,
texture → PNG/DDS. An in-editor export would add that conversion package.

### Import (new art into the game)

UAssetAPI cannot author a valid UE5.4 `StaticMesh` / `Texture2D` / material
from an FBX. Cook in Unreal 5.4, then pack:

1. Unreal 5.4 project (dummy cook project is enough).
2. Import FBX with the **same socket / component names** the skin Blueprint
   expects.
3. Materials: reuse vanilla MI_* or cook new ones and pack them beside the
   mesh.
4. Cook, copy `.uasset` / `.uexp` into `work/mod`.
5. Clone `BP_WeaponSkin_*`, retarget `FolderName`, point mesh components at
   the new `SM_*` packages.
6. Clone `DA_WeaponPictos_*`, point `WeaponSkin` at the new skin BP.
7. `retoc list --path` must show unique `/Game/...` paths for DA, skin, and
   each mesh. Matching `.pak` / `.utoc` / `.ucas` from one pack.

Until a cook station exists, new weapons should reuse or retarget vanilla
`SM_*` (clone a skin Blueprint and swap mesh paths to another vanilla blade).
That swap does not need Unreal. A brand-new blade does.

## Do not invent

- Per-weapon level curves. One shared `DT_WeaponScalingTable`.
- Composite-only item rows.
- Shop / drop edits from a future creator.
- New combat graphs from scratch (use `docs/custom-weapon-effects.md`).

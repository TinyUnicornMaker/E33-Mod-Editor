# Weapon Bulk Upgrade (x10)

Main-pack kit: `src/E33ModEditor.Core/WeaponBulkUpgradeKit.cs`, wired in
`FullPackKit` right after `WeaponOverLevelKit.Apply`.

Commands:

```bash
# stage a vanilla copy (or a known-good overleveled copy) and verify the splice
dotnet $CLI bulk-smoke
dotnet $CLI bulk-smoke work/mod/Sandfall/Content/UI/Widgets/NPCServices/Panels/WBP_BS_WeaponUpgrade.uasset

# extract assets that installed mods would shadow (moves ~mods aside, always restores)
dotnet $CLI extract-suspend DT_CommonUIActionComposite DT_CommonUISpecificAction ST_UI_InputActions
```

## Behavior (shipped)

- **One button.** Hold to completion triggers one bulk activation that upgrades
  **up to 10 levels**; near a band boundary it stops at the boundary
  (28 → 33, 60 → 66, …), so the actual gain is
  `min(10, band end − level, affordable)`.
- **Tier clamp:** a bulk never crosses the current catalyst tier, so it never
  charges the next tier's catalyst by accident. The next activation continues
  into the following band: 1–33 → 34–66 → 67–111 → 112–222 → 223–333.
- **Partial affordability:** each level re-fetches and re-checks the vanilla
  cost, so running dry stops the loop and keeps the levels already gained.
- **~0.2 s per level** (the vanilla post-upgrade delay), so a full x10 takes
  about two seconds and the level counter climbs visibly.
- Costs/UI refresh, sound, VFX and the maxed/full-upgrade checks still run
  through the vanilla flow once per level.
- **Prompt label:** the button reads **"Upgrade 10 Levels"**
  (`ST_UI_InputActions.ST_IA_WEAPON_UPGRADE`). The Curator tile description
  (`ST_UI_GameMenu.ST_GM_FLAG_UP_WEAPONS_DESC`) explains the hold gesture and
  the tier clamp. Both tables are patched in place if another kit already
  staged them, so edits compose.

## Why append-only (crash history)

Three earlier designs grew `ExecuteUbergraph_WBP_BS_WeaponUpgrade` in the
middle of the graph (press gate, tier ladder, loop-back). Every one crashed the
game when the weapon-upgrade section opened, even though the added statements
could not execute on open. Controls proved the boundary:

| build | change | result |
| --- | --- | --- |
| probe | 32 dead statements appended at the end | opens fine |
| ladder | 23 statements inserted at entry 3107 | opens fine, leveling works |
| loop | 8 statements inserted before the cost refresh | **crash on open** |

The widget therefore does not tolerate bytecode growth before its post-upgrade
block, but appending after the final `EX_Return` is safe. The shipped design
never changes a statement's size:

1. Repoints the click path's own `PushExecutionFlow` (`offset 676`, vanilla
   target `1747`) at a block appended after the final `EX_Return`.
2. The appended block resets a stale counter, counts one upgrade, stops at
   `Levels`, computes the tier ceiling (`< 34 → 33`, `< 67 → 66`,
   `< 112 → 111`, `< 223 → 222`, else `333`), stops if the level reached it,
   else jumps back into the vanilla chain (`offset 3107`).

Every pre-existing offset is untouched (no delegate constants change), and the
kit adds **no imports and no locals** in the shipped configuration:

- counter: `CallFunc_GetGearMaxUpgradeLevel_ReturnValue`
- tier ceiling: `Temp_int_Array_Index_Variable`
- level: `CallFunc_GetCurrentLevel_ReturnValue`
- comparisons: the existing `Less_IntInt` / `Add_IntInt` imports

A stale counter is detected by `counter > Levels-1` (the post-upgrade block
leaves `GetGearMaxUpgradeLevel()` = 333 there), so a fresh activation starts at
zero without needing an init hook.

## Settings (`work/weapon_bulk.json`, optional)

| key | default | meaning |
| --- | --- | --- |
| `Enabled` | `true` | `false` skips the widget splice (control build). |
| `Levels` | `10` | levels per activation, clamped 1…10. |
| `TapPlusOne` | `false` | experimental press-to-+1 gate (see below). Off: tap does nothing, hold bulks. |

## Verification

`bulk-smoke` asserts: no imports added (shipped mode), click push redirected
past the final `Return`, the vanilla anchors still at 3107/1747/3893
(append-only), all five tier-band constants, no unresolved jump placeholders,
and every jump target on a statement start. Re-apply is a no-op.

## In-game test plan / results

Checked in the Curator weapon panel:

1. Weapon at 28 with 10+ T1 catalysts: hold → stops at **33**, T2 untouched. ✅
2. At 33 with T2 catalysts: next hold continues into the 34–66 band. ✅
3. Limited catalysts: the loop stops at what is affordable and the cost UI
   refreshes. ✅
4. At 333 / tier max: no loop, normal maxed behavior. ✅

## Attempted and rejected

Documented so these are not retried blindly.

### Tap = +1 on the same button

- **Mid-graph gate** (zero progress tick → single upgrade): crashed the panel,
  but that turned out to be the mid-function growth (see crash history), not the
  gate logic.
- **Append-only gate + `IsPressed()`**: retargeted the progress delegate at an
  appended block and used the button's native `IsPressed()` to ignore the
  release tick. `IsPressed` is native (not a script call), so it resolved false
  and taps did nothing.
- **Append-only gate + `RetriggerableDelay(0.15)`**: tap worked, but the release
  tick after a completed hold also passed the gate, giving 11 levels on a hold.
  `TapPlusOne` remains in the kit (disabled) for a future attempt.

### Second prompt row (Triangle)

A second visual prompt was attempted with cloned widget exports: the CommonUI
action binding (`WBP_TextualInputActionBinding`), a cloned status text block,
and the new slot inserted above and below the button. In every case the packed
asset was verified (by converting the installed container) to contain the new
exports and the updated `VerticalBox_142.Slots` array, yet the game rendered
only the original three children. This build ignores widget exports added after
cook time, so a cloned row cannot be shown.

Related data work that **is** staged and reusable: a
`CommonUI_GameMenu_UpgradeWeaponOnce` action row (Triangle/`Gamepad_FaceButton_Right`,
keyboard `R`, no hold) in `DT_CommonUIActionComposite` and
`DT_CommonUISpecificAction`, plus `ST_IA_WEAPON_UPGRADE_ONCE` = "Upgrade 1 Level".
It is currently not referenced by any widget (the label no longer carries a
second line), so it adds no visible UI.

### Dynamic label

The prompt is the CommonUI action's display name, which is static per pack —
there is no label setter on the button and the text block lives in the raw
button export. A countdown ("×3" near a boundary) would need a runtime `FText`
override on the action binding (e.g. `UpgradeWeaponBinding.SetInputText` with
text selected from the tier constants), which is a new mechanism and was parked.

### 1.5 s hold retime

Still needs `BP_UI_FrontendHoldData` (not extracted). The current hold length is
vanilla's.

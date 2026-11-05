# Camera (submodule of E33ModEditor_P)

The close third-person / first-person camera. It rewrites the data-driven
`DA_CameraConfig_*` assets so the third-person spring arm becomes whatever the
settings say: a short over-the-shoulder boom (close third person) or a
head-mounted lens (first person).

> **It is a submodule of the main pack now.** The camera patches are folded into
> `Export/E33ModEditor_P` rather than shipped as a standalone pack. The old
> standalone names (`E33SecondPerson_P`, `SecondPersonCamera_P`,
> `ImmersiveThirdPersonCamera_P`) are gone; a separate camera pack would shadow
> the main pack and fight it over `BP_jRPG_Character_World`.
>
> `secondperson-pack` still exists as a legacy debug path for isolating camera
> changes without a full rebuild. Do not ship its output beside the main pack.

## Configure

GUI: the **Camera** tile. CLI: `dotnet $CLI firstperson …` (writes
`work/firstperson.json`). Then `rebuild-mod` (or the standalone generator) folds it
into `E33ModEditor_P`. Turn the submodule off to ship the vanilla camera.

## Presets

| Preset | Boom | Notes |
|---|---|---|
| Third-person close | 130 | Over-the-right-shoulder (side +35, FOV 85). The old *ImmersiveThirdPersonCamera* look. |
| First-person | 0 | Head-mounted, lag off, pivot dropped for framing. |
| Vanilla | — | Disables the submodule. |

## What it patches

- `DA_CameraConfig_Default` / `_Maelle` / `_Monoco` / `_Crouch_Default` /
  `_MovingPlatform_Default` — explore boom, socket, pivot, FOV, lag.
- `DA_CameraConfig_Verso` — cloned explore config (his hair clips at the shared
  Default distance).
- `DA_CameraConfig_FreeAim_*` (Lune/Maelle/Monoco) plus cloned `_Sciel` and
  `_Verso` aim configs, rewired through `DT_jRPG_WorldCharacters` — only when
  **Also make aim first-person** is on.
- `BP_jRPG_Character_World` (and `BP_jRPG_Character_WorldMap_pawn`) — the
  smart-spring-arm template and the traversal-restore cache, so climb/reset
  returns to the modded view.
- `BP_ClimbingComponent` — keeps the climb camera zoomed in instead of yanking
  the boom to the vanilla 700 cm.
- World-map cine camera depth of field (overworld diorama blur turned off).

`BP_jRPG_Character_World` is shared with the **Party of Five** submodule; the
party kit reuses the camera-staged copy instead of restaging it, so both edits
survive in one pack.

## Depth of field

The overworld lenses ship extreme shallow-focus "diorama" DoF: the world-map
pawn (`BP_jRPG_Character_WorldMap_pawn`, 21 mm f/0.185) and the second
overworld character (`BP_WorldMapCharacter`, 21 mm f/0.13). Both are turned
**off** by default — `DisableOverworldDof` clears the CineCamera DoF override
flags on each lens and parks the aperture at `WorldMapAperture` (22 = effectively
off) as a fallback. Clearing the overrides is what keeps the blur gone even when
the game hands the view back after an overworld fight (a plain f-stop change did
not survive that). `WorldMapAperture` (`work/firstperson.json`) is the secondary
lever: 0 leaves the lens aperture alone.

The dungeon/region pawn (`BP_jRPG_Character_World`) runs f/120 and is left alone.

### DoF extension (opt-in)

A separate, opt-in extension of the camera submodule: a moderate
out-of-focus falloff on the exploration camera in **both dungeons and the
overworld**. Turn it on in the GUI **Camera ▸ Depth of field** card, or:

```bash
dotnet $CLI firstperson --dof --dof-aperture 2.45 --dof-blur 19
```

Settings (`work/firstperson.json`):

| Field | Default | Meaning |
|---|---|---|
| `DepthOfField` | `false` | Enable the extension. |
| `DofAperture` | `2.45` | Bokeh f-stop written to both exploration lenses. Lower = more blur (1.0–1.4 cinematic, 8+ nearly off). |
| `DofBlurAmount` | `19` | Blur amount for the Gaussian/Circle DoF methods (the f-stop is ignored there). |
| `DofBlurRadius` | `3` | Blur radius for the Gaussian/Circle methods. |
| `DofFocusDistance` | `0` | Focal plane (cm). `0` = auto (the boom length), so the subject stays sharp while the background falls off. |

**How it works.** Both the dungeon/world pawn (`BP_jRPG_Character_World`) and
the overworld pawns (`BP_jRPG_Character_WorldMap_pawn`, `BP_WorldMapCharacter`)
expose the gameplay lens as `Main_CineCamera_GEN_VARIABLE` — the component
`GetMainCamera` returns; the plain `MainCamera` is inactive. A cine camera's
`CurrentAperture` alone does **not** drive the renderer: it only produces DoF
through its `PostProcessSettings` fields with the matching `bOverride_` flags.
The extension therefore writes a full DoF post-process block onto each lens —
`bOverride_DepthOfFieldFstop` + `DepthOfFieldFstop` (Bokeh),
`bOverride_DepthOfFieldFocalDistance` + `DepthOfFieldFocalDistance`, and
`bOverride_DepthOfFieldDepthBlurAmount`/`Radius` (Gaussian/Circle) — so it works
whichever DoF method the project renders with, and refocuses the lens (via
`FocusSettings.ManualFocusDistance`) on the subject. While the extension is on
it owns all three lenses and supersedes `WorldMapAperture`; with it off, the
dungeon lens is untouched and the overworld lenses get the DoF-off treatment
above.


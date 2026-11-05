# Guide textures — curated export manifest

Exported with the new CLI (CUE4Parse direct, immune to `~mods` shadowing):

```
dotnet E33ModEditor.Cli.dll texture-info "<filter>" [max]
dotnet E33ModEditor.Cli.dll texture-export <NameOrPath,...|@list> [--out DIR] [--max N]
```

PNGs live in `work/guide_textures/` (17 files, ~14 MB). All so far are
DXT1/DXT5 or raw — decode verified. Formats are masks/grunge tinted at
runtime by `MI_*` materials, so print color comes from `palette.json`, not
the PNG pixels.

## Panel grounds (chapter openers / full-bleed)

- `T_CharacterSheetBackground.png` — 1360x2160 DXT5 — character chapter ground
- `T_UI_AttributesBackground.png` — 3840x2160 DXT5 — stats/progression spread
- `T_UI_LuminaBackground.png` — 2048x1040 DXT5 — pictos/lumina spread
- `T_UI_LuminaBackgroundStain.png` — 2040x1400 DXT5 — stained variant, more texture
- `T_UI_LuminaPanelBackground.png` — 3840x1260 DXT5 — wide banner
- `T_UI_SkillPointsBackground.png` — 2048x1144 DXT5 — progression accent
- `T_UI_TutorialCardBackground.png` — 1040x2048 DXT5 — getting-started card

## Tooltip grounds (move tables / sidebars)

- `T_UI_SkillTooltipBackground.png` — 2048x1732 DXT5
- `T_UI_WeaponTooltipBackground.png` — 2048x1776 DXT5

## Lines / stains / icons (dividers, bullets)

- `T_GM_GrungeLine.png` — 1024x4 DXT5 — hairline divider (tint gold)
- `T_BackgroundGrungeFade_Raw.png` — 136x57 raw — grunge fade
- `T_GM_PaintTopTitle.png` — 256x256 DXT5 — paint dab title accent
- `T_HUD_ArtdecoLine_Overlay.png` — 256x32 DXT5 — art-deco divider
- `T_HUD_DecoBottomLine_Loot.png` — 128x64 DXT5 — loot divider
- `T_UI_JournalDecorator.png` — 2048x612 DXT5 — journal flourish
- `T_UI_LuminaPointsIcon.png` — 512x512 DXT5 — lumina pip

## Suggested print use

- Masters: Charcoal ground + Bone text + Gold hairline (`T_GM_GrungeLine`
  tinted `#C9A86A`).
- Move tables: tooltip grounds at 8–12% opacity behind tables.
- Chapter openers: one full-bleed panel ground per part + journal flourish.
- Keep `.designcraft` embeds light — link these externally until final export.

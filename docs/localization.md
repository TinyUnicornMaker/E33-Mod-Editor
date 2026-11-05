# Localization / language selection

The pack builder can bake one translated language into the mod. English is the
default and the source language; French, Polish and Russian are shipped.

## Choosing a language

- **GUI**: the launcher top bar has a **Language** dropdown. The choice is saved
  in `work/language.json` and mirrored into the project sidecar.
- **CLI**: `dotnet <cli> rebuild-mod --language fr` (also `pl`, `ru`, `en`).
  Without a flag it reads `work/language.json`, then falls back to `en`.

The language applies to a whole pack build. A pack is not multi-language at
runtime: it ships one culture's patched `Game.locres`.

## How it works

1. Kits build the mod with English **table source** text (the locres overrides
   it at runtime), exactly as before.
2. Inline/culture-invariant strings call `ModLocalization.Key(...)` /
   `ModLocalization.Inline(...)`, so they are written in the selected language
   while the assets are staged. Unchanged vanilla keys that a kit writes inline
   fall back to the culture's official locres translation.
3. At the end of the build, `LocalizationKit.Apply` loads the selected culture's
   vanilla `Game.locres`, merges every mod-owned key, and writes:

   ```
   work/mod/Sandfall/Content/Localization/Game/<culture>/Game.locres
   ```

4. `Retoc.PackCombined` packs that locres into a dedicated
   `zz_<packName>_P.pak` with `repak` (retoc's `to-zen` only converts assets,
   so non-asset files need the legacy pak). The `zz_` prefix makes it load
   after `pakchunk0`; otherwise the base game's `Game.locres` wins duplicate
   vanilla keys and modified skill text falls back to English while new keys
   still work. As a deterministic fallback, the selected translation is also
   written into the staged StringTable source; if the engine rejects a stale
   source hash or a base locres entry wins, the skill still displays
   in-language. The `.utoc`/`.ucas` still hold the assets. English ships no
   locres pak.

Only **new or changed** keys are in the catalogs. Unchanged keys keep the
game's official translations. For existing vanilla keys, the locres keeps the
original vanilla `SourceStringHash` so the engine does not reject the
translated entry as stale; new keys use CRC32 of their English source.

## Translation catalogs

Source data lives in `src/E33ModEditor.Core/Localization/Data/`:

| File | Contents |
|---|---|
| `en.json` | English source for every mod-owned key |
| `fr.json` | French translations |
| `pl.json` | Polish translations |
| `ru.json` | Russian translations |

Shape:

```json
{
  "tables": {
    "ST_PassiveEffects": { "PASSIVE_X_Name": "…" }
  },
  "inline": {
    "SKILL_SuperSpark_Name": "…"
  }
}
```

`work/localization/<code>.json` overrides the embedded catalog at runtime, which
is handy while translating without rebuilding.

## Workflow for a new language or an update

```bash
CLI=src/E33ModEditor.Cli/bin/Release/net10.0/E33ModEditor.Cli.dll

# 1. Build an English stage (or use an existing work/mod).
dotnet $CLI rebuild-mod --language en

# 2. Compare the stage with vanilla and regenerate the catalogs. Translations
#    are preserved when the English source is unchanged; changed keys get the
#    official reference (for review) and new keys are blank. Write straight to
#    the embedded catalog directory so the next build picks them up.
dotnet $CLI loc-catalog --out src/E33ModEditor.Core/Localization/Data

# 3. Translate src/E33ModEditor.Core/Localization/Data/<lang>.json.
#    For quick iteration without rebuilding, copy it to
#    work/localization/<lang>.json (that file overrides the embedded catalog).
#    loc-catalog writes review.json/counts.json to work/localization/, not into
#    the embedded data directory.

# 4. Check coverage, markup IDs, placeholders, numbers and newlines.
dotnet $CLI loc-validate --language fr

# 5. Merge into the stage and inspect the result.
dotnet $CLI loc-apply --language fr

# 6. Build the real pack.
dotnet $CLI rebuild-mod --language fr
```

## Rules for translators

- Preserve `<keyword id="…">…</>` and `<img id="…"/>`; translate only the
  visible text inside a keyword.
- Preserve `{placeholders}`, `\n`, `%`, `x` multipliers and every number.
- Do not translate keys, IDs or proper nouns.
- Use the official terminology in `work/localization/glossary-<code>.md`.
- Descriptions are tooltips: concise, present tense, same voice as vanilla.

`loc-validate` is structural only (coverage/markup/numbers/newlines). Semantic
review is still needed for a new language.

## Adding a language

1. The base game must ship the culture under
   `Sandfall/Content/Localization/Game/<culture>/Game.locres`. The mod cannot
   add a language the game doesn't support.
2. Add a `ModLocalization.Language` entry to `ModLocalization.Supported`.
3. Add `<code>.json` next to the other catalogs and translate it.
4. Add a glossary under `work/localization/glossary-<code>.md`.
5. `loc-validate`, then build.

## Scope

The catalogs cover the seven StringTable assets the pack writes
(`ST_MainCharacters_Skills`, `ST_PassiveEffects`, `ST_Items`,
`ST_UniqueMechanics`, `ST_BattleTooltips`, `ST_UI_GameMenu`,
`ST_UI_InputActions`) plus the inline strings written by
`SuperSparkKit`, `DamageLimitKit`, `SpecialPictosKit`, and the Gradient
Overcharge proc template. The Spoiler Codex HTML is generated in English and is
a separate translation track.

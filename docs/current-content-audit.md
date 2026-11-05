# Current-content audit (2026-09-09)

Audit of every list that enumerates pictos / moves against the **currently
supported set** (vanilla rows in use + mod-kit content), removing references to
content that no longer exists.

## Method

- Base rows: `work/items.json` (full `DT_jRPG_Items` dump — every vanilla row
  that still exists, including cut content shipped in the base data).
- Mod content: the pictos/move kit registries in `src/E33ModEditor.Core`
  (`PictosCreateKit.*`, `PictosMagnitudeKit.Variants`, the skills kits).
- Verdict rule: an id referenced by a list is **dead** when it matches no base
  row and no mod registry. Vanilla cut content (rows that exist but are never
  placed) is intentionally *kept* in the tier-list `DoNotUse` quarantine — that
  bucket is what stops the randomizer from placing it.
- Re-run the machine check any time: `dotnet …/E33ModEditor.Cli.dll loot-audit`
  (fails listing any tier id that resolves to nothing).

## Pictos tier list (`item_tiers.json`, stock + `work/` overlay)

Dead ids removed from `Pictos.DoNotUse` (both copies):

| Id | Why dead |
|---|---|
| `ComboAttack4` … `ComboAttack7` | Mod-created magnitude variants from the abandoned combo extension; deleted outright with the rest of that machinery, never existed in vanilla data |
| `DoubleBurn3` | Never existed — kit registers only `DoubleBurn2` (Triple Burn) |
| `DeadEnergyII` | Asset-name id only; its item/passive/def rows were written under `BootyHunter` (`RowIdOverride`), already `Tier2` |

Removed the `StartsWith("ComboAttack")` known-id catch-all in
`LootViewModel.KnownIds()` so purged ids can't resurface in the Tier Editor's
**Unplaced** bucket and be dragged back into an active pool.

Guarded: `loot-audit` CLI command now fails if any tier id (any category, any
column incl. DoNotUse) resolves to nothing.

## Save editor pictos tab

The vanilla list is generated from current tables, so dead ids only surface
when an old save still owns them. Behaviour now:

- Rows the save owns whose id is gone (retired mod content such as Combo Attack
  IV–VII, or vanilla cut rows) stay visible with **"⚠ removed from current
  pack"** and a `retired` tag, so they can be scrubbed via the Found toggle.
- Unowned cut/quarantined vanilla rows are no longer listed (they were swept by
  "Add all vanilla").
- "Add all vanilla", "Master all vanilla", "Master all found" and "Set level on
  found" exclude flagged rows; per-row edits stay enabled.
- `save-pictos-add` (CLI) warns when the id is in no current table or mod kit.

## Moves / skills

No skill row is ever deleted by the mod; "hidden" moves are removed only from
the playable skill graph, and the DA assets still exist. Nothing on the skill
side references a move that exists nowhere, so there is nothing to delete.

Verified clean: `work/skill_graph.json`, `Export/E33ModEditor_P.project.json`
(`skillGraph`), `work/projects/autosave.project.json` — none reference the 14
hidden `DA_SkillGraph_Noah` nodes or the cut "We Are Me".

### Currently supported moves per character

Vanilla visible graph rows + kit-authored adds. Kit registries (authoritative):

| Character | New-move source | Count |
|---|---|---|
| Lune | `LuneSkillsKit.Skills` | 19 |
| Lune | `LuneApocalypseKit.Moves` (Dark Power, Angel's Gift, Ultima, Ultima Noir) | 4 |
| Maelle | `MaelleSkillsKit.NewMoves` (Lumière Above, Revers Fatal, … L'Acte Final Incendiaire) | 8 |
| Gustave (Noah graph) | `GustaveSkillsKit.NewMoves` (Zoom Zoom, Hell Storm, Cinder Swarm, Exploding Shot) | 4 |
| Sciel | `ScielCustomKit.Moves` (Black Death … Soul Transplant) | 7 |

Retired-but-still-present-as-rows (excluded from playable trees, do not revive):
the 14 `GustaveSkillsKit.HiddenNodeDaStems` (`DA_Skill_Blitz`, `DA_Skill_Combo1`,
`DA_Skill_LightHolder`, and the `DA_Skill_Verso_*` unlock-gated nodes), Maelle's
cut "We Are Me". The mod-created combo ladder (`ComboAttack4–7`), the old auto
duration upgrades and the per-status affinity set have been deleted from the kit
entirely — there is no retire list left to keep them out.

The save editor's per-character skill lists are free-form save strings with no
catalog enumeration — they cannot go stale; unknown ids round-trip untouched.

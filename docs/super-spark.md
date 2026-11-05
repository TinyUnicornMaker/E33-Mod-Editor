# Super Spark (Maelle Spark override)

In-place rewrite of Maelle's Spark. Same skill-tree node and `NameID` (`Spark`).

## What it is

- **Void** damage (`EAttackType::NewEnumerator7`, `MakeDamageBuilder` byte 8)
- **3 hits** from Fleuret Fury's authored sequence
- **5 Blight** on the last hit, **10 Blight** in Offensive / overcharge
- Slash Niagara swapped to Void (`FX_VoidSlashInfinite`); hit explosion from the Spark script is `FX_ExplosionVoid`

## Why the Fury anim only dealt 1 hit (and skipped status)

Two independent Spark-vs-Fury mismatches:

1. **`StandardSkillExecution` flags.** Spark (1-hit) calls it with `(seq, True, False, False, False, …)`. Fury (3-hit) uses `(seq, True, False, True, True, …)`. Those last two bools are what actually fires `OnActionEffect` per BattleKeyframe. Super Spark kept Spark's False,False, so the Fury clip played three swings but only the first key ran the damage script.

2. **Keyframe enum byte.** Spark's only hit key is **byte 50**. Fury's BattleKeyframes are **50, 2, 2, 2**. Byte 2 is the value used on `BattleKeyframes_Track2` (not a Spark hit). The kit now forces all three Super Spark keys to 50.

Because only one `OnActionEffect` ran, `IsLastActionEffectOfSkill` stayed false (the sequence still had pending keys), and the last-hit status gate skipped Burn/Blight entirely.

Do not loop a 1-hit clip to fake extra hits. Use a real N-hit donor **and** the donor's `StandardSkillExecution` flags / hit-key byte.

## Animation lesson (do not loop a 1-hit clip)

Spark's LevelSequence is **one authored strike plus recovery** — a single BattleKeyframe, one camera/FX timeline, end events after the recovery clip. It is not a cycle.

The first Super Spark attempt truncated that strike window and replayed it twice. That desyncs:

- skeletal pose vs camera cuts
- BattleKeyframes vs the mesh
- recovery / "attack finished" events (character can idle or hitch)

**Do not loop the same animation to add hits.** Use a real N-hit donor sequence.

## Current donor

`SEQ_SkillMaelle_FleuretFury` (vanilla tooltip: 3 hits). Copied over Spark's sequence package (`SEQ_SkillMaelle_New18_Spark`) so the Spark skill script still points at the same path. Skeletal clips stay Fury's (`Frank_Sword2_Combo01_All_UE5`, `Maelle_Skill_DancingFury_B_Inplace`). Fury's BattleKeyframes track is trimmed to the first **3** keys (vanilla has a 4th late key at 78715).

Gameplay (Void / Burn / Blight / last-hit gate) stays on `BP_Battle_SkillScript_New18_Spark`.

## Re-pack

```
dotnet $CLI superspark
```

Fully quit the game so IoStore drops the previous pack.

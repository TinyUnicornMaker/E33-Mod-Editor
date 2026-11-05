# Expedition Infinity — Guidebook (first pass)

> Design system: E33-inspired print tints (see `palette.json`), headings in
> Cinzel (Trajan stand-in), body in EB Garamond / IM Fell Double Pica,
> numbers in Bebas Neue. Backing art: exported UI masks in
> `work/guide_textures/` (see `textures.md`). The `.designcraft` file in this
> folder now carries the title + E33 swatches; full typesetting is still to do.

---

## Foreword

Thank you for checking out Expedition Infinity.

This guide covers the full mod — not any single seed. Expedition Infinity
overhauls progression, enemies, weapons, pictos, battles, moves, attributes
and drops, and the seed generator exists so every run draws fresh,
tier-appropriate gear while keeping the same design rules. Rules first,
rolls second: that is what this book documents.

It has been extensively playtested — including full-game completions — but a
mod this size always hides small issues. Please send comments, criticism and
bug reports through the usual channel. Once remaining issues are resolved,
the generator program itself will be released so you can roll your own seeds
for infinite replayability of this wonderful game.

---

## Known issues / play notes (read first)

- **Intended difficulty is Expedition.** This is a difficulty-raising mod.
- **Parry/dodge windows are tighter** (Expedition preset: dodge 0.165 s,
  parry 0.1125 s; three Helper pictos land at ~1.25x vanilla). Some mechanics
  changed with it — e.g. Lune can't substitute a Light stain for other stains.
  A normal-timing option is planned.
- **Tutorials are skipped automatically.** The tighter windows would break the
  scripted tutorial parry/dodge beats.
- **Weapon levelling is slow by design right now** (333 levels). Faster
  paths are planned.
- **Picto auto-learn requested.** If feasible it will arrive as an option.

---

## Getting Started

> **Warning.** This mod touches effectively all gameplay systems. It is
> expressly **not compatible** with anything that touches weapons, AP,
> pictos, enemy stats, player stats, shops, drops, skills, or area names.
> Clothing mods are fine.
>
> **Requires a new game. Not compatible with NG+.** Install **one** seed
> pack at a time.

1. Pick **one** seed pack — it ships with this guide, no download needed.
2. Extract the zip.
3. Copy every file from the pack folder to your `~mods` folder —
   `.pak`, `.ucas`, `.utoc`, and for non-English packs the
   `zz_..._P.pak` localization file —
   Windows: `C:\Program Files (x86)\Steam\steamapps\common\Expedition 33\Sandfall\Content\Paks\~mods`;
   Linux (Steam): `~/.steam/steamapps/common/Expedition 33/Sandfall/Content/Paks/~mods`.
   Create `~mods` if it is missing. Install **one** seed pack at a time.
4. Play. Report comments, criticism and bug reports.

### What to expect

Early game is very tough. It eases after a few hours — levels, weapon
upgrades, pictos — then bosses keep ramping while regulars become trivial.
Major story bosses are huge fights; no waltzing up and one-shotting
Dualliste or Renoir.

- **Early Act I.** Very tough. Hanging on by a thread in bosses. Some
  optionals are impossible — come back later.
- **Mid Act I.** After Esquie you have members + pictos; manageable with
  strategy and planning.
- **Act II.** Regulars are a breeze. Bosses stay very hard. Major story
  bosses: expect 20 min – 1 h.
- **End Act II.** Difficulty jumps before the Paintress. Bosses hit very
  hard; near-perfect play required. Build around death/revive if parries are
  inconsistent. Reference: Paintress in ~15 min after several wipes.
- **Act III.** Difficulty depends on preparation and order. Bosses now have
  **defense stats** — hits do less the higher the boss level. All bosses
  drop **5–21 Perfect Catalysts** by level. Regulars in higher areas roll
  **1/32 Perfect Catalyst**. Goal: level weapons, find the strongest pictos.
  You do **not** need level 333 weapons or every picto. Beat Renoir early
  for a crazy fight, or grind until you one-shot him. Your call.

Intended Act III order (area — level):

**Tier 4**
1. Crimson Forest — 60
2. Red Woods — 60
3. Sacred River — 60
4. Sky Island — 60
5. The Chosen Path — 60
6. Isle of the Eyes — 65
7. The Crows — 65
8. Dark Shores — 68
9. Endless Night Sanctuary — 73
10. Dark Gestral Arena — 75
11. The Reacher — 79
12. Frozen Hearts — 79
13. Flying Cemetery — 79

**Tier 5** (regulars drop Perfect Catalysts 1/32)
14. The Fountain — 80
15. Flying Manor — 83
16. Verso's Drafts — 86
17. Painting Workshop — 90
18. Sunless Cliffs — 92
19. Renoir's Drafts — 93
20. The Abyss — 99
21. Lumière (Act 3) — 99
22. Root of All Evil — 99

---

## The overhaul (flavor, not exhaustive)

Detailing everything would be death by a thousand words. The shape:

- **Tier-based randomization.** Five tiers keep low areas weak and strong
  areas strong. Still random within tier — no more broken early-game
  jackpots, no more trivial Act 1.
- **Enemies.** Early beach feels like a fight to survive between flags;
  bosses force full-kit thinking. Past mid Act I regulars fade, bosses keep
  climbing.
- **Weapons.** One effect at first, unlock at 33 — because 33 is the new ~3
  (cap is **333**). Fewer early Chroma Catalysts on purpose; you need every
  one. Higher tiers = stronger base stats + more passives: T3 has 3, **T5
  has 5**. Passives roll per weapon per seed from an **effect-family
  system** so rolls tend to synergize. Elements reroll too.
- **Pictos.** **165 new.** Roulette I–V (gamble every hit), the four B's
  (burning / blight / berserk / curse — big damage wants self-ailments),
  plus unique drops from late optional story bosses (unspoiled here).
- **Battles.** **Max AP 21**, **up to 4 in battle** — specialize. Tight
  parry/dodge, scarce early healing, ~1 revive tint. **Gradient to 10
  charges** — the mid/late damage answer. **Luck scales crit damage past
  100% crit.**
- **Movesets.** **40+ new moves**, all moves refit to one damage curve so
  every path is viable: a 9 AP move always beats 8 AP; two 8 AP moves match.
  Moves use up to the full 21 AP with steep scaling past 9 AP. Taste:
  Sciel's Second Life (die in 7 turns + Aureole), Lune's Angel's Gift (eats
  Light stains, team AP), Verso's Overcharged Blitzer (speed → lightning),
  Maelle's Revenge (getting hit pays back next turn).
- **Leveling / attributes.** Smoothed curve — even returns per point to 80,
  then **accelerating to 333** (level + attribute cap). Bosses pay big XP so
  333 is not a grind. Nerf: **2 attribute points/level** (was 3) — specialize.
- **Drops.** Bosses = gear/pictos (guaranteed). Regulars/chests = catalysts
  + **lumina** (rare but steady) — spend it. **Petanks pay lumina**; beat
  them ASAP. White nevrons = fat Act III lumina (or kill them — they count
  as bosses, allegedly). Chroma will not be a problem; shops (also
  tier-randomized) will make you feel rich.
- **Key items.** Untouched and in place: music, journals, clothing,
  keys/cake/etc.

---

## Progression (how a run feels)

Play the difficulty curve above, not a checklist. Practical beats:

1. Survive Early I (flags > greed, skip impossibles).
2. Mid I: assemble team + first synergies after Esquie.
3. Act II: rotate builds per boss; bank lumina; start gradient play.
4. End II → Paintress: add death/revive insurance if parries wobble.
5. Act III: follow the Tier 4 → Tier 5 order; chase Perfect Catalysts
   (bosses 5–21, T5 regulars 1/32); level weapons toward — not necessarily
   to — 333; hunt top-tier pictos; then decide when to face Renoir.

---

## Characters

Template per character (fill from Planning sheets + in-game verification):

- Fantasy in one line
- Resource loop (Stances / Stains / Foretell / Perfection / Masks / Gradient)
- Stat priority (to 80 → past 80 acceleration)
- Weapon archetypes (see `Planning/archetypes/`: 32 archetypes — Sustained,
  Base Attack, Skill Damage, Critical, Break & Stun, Burn, Counter,
  Status & Mark, Shield & Sustain, AP & Tempo, Gradient, Free Aim,
  Ailment Blades, Risk & Sacrifice, Stain Cycling, Fire/Ice/Lightning/
  Earth/Light/Dark Stains, Offensive/Defensive/Virtuose, Foretell,
  Sun/Moon/Twilight, Perfection, Masks, Bestial Wheel, Upgraded Skills)
- Full move list: AP | element | hits | base/hit | special | new?
- New-move highlights + changed mechanics (e.g. Lune Light-stain rule,
  Maelle Virtuose gates, Sciel Second Life timer)

Status: move tables are **scaffolded, not final**. Canonical new-move
counts (current-content-audit): Lune 19 + 4 (Dark Power, Angel's Gift,
Ultima, Ultima Noir), Maelle 8 (Lumière Above, Revers Fatal, Déviation
Absolue, For Those Who Come After, Vengeance Ardente, Alicia's Nightmare,
Blade Waltz, L'Acte Final Incendiaire), Gustave/Noah 4 (Zoom Zoom,
Hell Storm, Cinder Swarm, Exploding Shot), Sciel 7 (Black Death … Soul
Transplant). Maelle refit details in `docs/maelle-overhaul-spec.md`
(0.4×clamp rule, per-hit quoting, +1%/Burn global, Marked ×1.5,
Virtuose ×1.5 gates, 8→21 AP range).

### Lune — TODO (stains control, Angel's Gift team AP)
### Maelle — TODO (stances: Offensive/Defensive/Virtuose; 8 new moves)
### Gustave — TODO (charge / marking)
### Sciel — TODO (Foretell + Second Life risk line)
### Verso — TODO (Perfection + Overcharged Blitzer)
### Monoco — TODO (Bestial wheel / masks)

---

## Appendices (planned)

- A. Pictos ladder reference (I–V families, B's sets, uniques)
- B. Status glossary (Burn/Mark keyword rule)
- C. Install / troubleshoot (one seed at a time, new game only, no NG+,
  tutorials OFF, Expedition intended)
- D. Texture/palette manifest (`textures.md`, `palette.json`)

# Target glow

A persistent, per-character-coloured aura on the ally currently focused by
battle targeting. It was added for revive targeting: a downed ally lies flat,
and with several dead allies the stock `TargetPointer` billboard is easy to
miss when choosing who to resurrect.

## What it does

`BP_jRPG_Character_Battle_Base.OnTargetingHoverStart`:

```
if (IsPlayerTeamCharacter):
    if (DefaultHitSound is a valid NiagaraComponent):
        destroy it
    colour = switch CharacterHardcodedName { Frey, Maelle, Lune, Sciel, Verso, Monoco }
    DefaultHitSound = SpawnSystemAttached(
        FX_ElementalCharge, Attachment_Spine03, ...,
        bAutoDestroy: false, bAutoActivate: false, bPreCullCheck: false)
    if (IsValid(DefaultHitSound)):
        DefaultHitSound.SetRelativeScale3D(0.20)   # marker-sized, not the full aura
        DefaultHitSound.SetColorParameter("Color", colour)
        DefaultHitSound.SetColorParameter("Color1", colour)
        DefaultHitSound.Activate(true)
```

`OnTargetingHoverEnd`:

```
if (DefaultHitSound is a valid NiagaraComponent):
    DefaultHitSound.K2_DestroyComponent()
    DefaultHitSound = null
```

The effect is attached to the character's `Attachment_Spine03` scene component
with no offset, so it sits at the body rather than 70 cm above the actor. It
stays visible for the whole target focus and is destroyed when the target is
changed, confirmed or cancelled.

Because all members of a focused targeting group receive the event, an
all-ally skill lights every candidate in its own colour, while a single-target
revive lights the one selected ally.

## Colours

Colours are keyed to `AC_jRPG_CharacterStats.CharacterHardcodedName`, so they
stay with the character regardless of party order.

| Character ID | Character | Colour |
|---|---|---|
| `Frey` | Gustave | gold |
| `Maelle` | Maelle | coral red |
| `Lune` | Lune | azure |
| `Sciel` | Sciel | violet |
| `Verso` | Verso | emerald |
| `Monoco` | Monoco | amber |
| other | — | warm white |

## Component slot

The character Blueprint has an otherwise-unused `DefaultHitSound` object
variable. It is repointed from `Object` to `NiagaraComponent` and gains
`CPF_InstancedReference`; the slot then holds the active target-glow component.

A **new** class property is deliberately not added: UAssetAPI cannot serialize
a longer `ClassExport` next to this Blueprint's raw CDO without corrupting the
following export offsets (it broke `Camera_GEN_VARIABLE`). Reusing the existing
8-byte slot changes no class/export sizes.

`SpawnFXOnSelectedTargets` is left untouched; it still uses its own
`SpawnedParticleSystem` cascade slot.

## Toggle

The seed-generator toggle is **off by default**. The standalone CLI/editor can still enable it with `work/target_glow.json`:

```json
{ "Enabled": true }
```


## Implementation notes

- Both event **stubs** are edited (`OnTargetingHoverStart` and
  `OnTargetingHoverEnd`); the ubergraph is untouched.
- The patch reuses an already staged `BP_jRPG_Character_Battle_Base` from Party
  of Five / row damage / camera instead of copying vanilla over their edits.
- In Bigger Party Mod compatibility mode the kit is skipped, because shipping
  our own `Battle_Base` would shadow the third-party mod's copy.
- `FX_ElementalCharge` is a hard reference to the vanilla system; the mod does
  not ship a copy of it.
- Native call arity is asserted for every injected call. The earlier version
  passed an extra argument to the zero-argument `K2_GetActorLocation`, which
  desynchronised the Blueprint VM and froze battle load.
- Each `NotEqual_NameName` comparison is evaluated into a bool local before the
  switch, matching vanilla compiler output. A pure `CallMath` used directly as
  a `SwitchValue.IndexTerm` leaves the VM result-destination stack stale and
  makes a later stock pure call fault with a null result buffer.

## Verification

```bash
DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" \
  dotnet src/E33ModEditor.Cli/bin/Release/net10.0/E33ModEditor.Cli.dll target-glow-smoke
```

The smoke test stages the patch into `work/mod_targetglowsmoke`, checks the
native call arity, all six nested colour switches and their offsets, the
persistent spawn (auto-destroy and auto-activate both off), the `Activate`
call, the hover-end cleanup, jump targets, the ubergraph entry points and
idempotence.

In-game checks still needed: whether `FX_ElementalCharge` loops for the full
hover, how the spine attachment looks on a downed body from the revive camera,
and whether the six colours read clearly against every battle background.

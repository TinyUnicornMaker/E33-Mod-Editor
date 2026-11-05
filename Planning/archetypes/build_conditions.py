#!/usr/bin/env python3
"""Builds the weapon-effect *condition* table used by the passive randomizer.

The archetype table groups effects by theme (burn, crit, counter, ...). This
table adds the orthogonal axis the randomizer was missing: the *condition*
under which an effect pays off. Two effects whose conditions are mutually
exclusive must never land on one weapon (e.g. Full Strength "on full Health"
next to Self Burn).

Output: src/E33ModEditor.Core/Loot/Data/weapon_effect_conditions.json
(stock, embedded). A work/weapon_effect_conditions.json overlay wins at
runtime, same pattern as tiers/characters/archetypes.

Two tag kinds:

* **conflict tags** — mutually exclusive conditions (`hp-full` vs
  `self-damage`). An effect with none of these is "neutral" and never fights
  anything.
* **informational tags** — describe a build axis but are never impossible
  (`counter`, `reckless`, `multi-hit`, the Shield / Shell cluster). They never
  block a pick; they only feed the derived `situations` view.

Facts come from the effect descriptions in
src/E33ModEditor.Core/Weapons/Data/weapon_effects.json plus vanilla passive
text (`ST_PassiveEffects`); edit the rules here, not the JSON.
"""

import json, os
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]

TIERS = json.load(open(ROOT / 'work/weapon_effect_tiers.json'))
ARCH = json.load(open(ROOT / 'work/weapon_effect_archetypes.json'))
OUT = ROOT / 'src/E33ModEditor.Core/Loot/Data/weapon_effect_conditions.json'

ALL = [i for c in ('Tier1', 'Tier2', 'Tier3', 'Tier4', 'Tier5') for i in TIERS[c]]
ALLSET = set(ALL)


def R(prefix, lo, hi):
    return [f'{prefix}{i}' for i in range(lo, hi + 1)]


def arch_members(aid):
    for a in ARCH['archetypes']:
        if a['id'] == aid:
            return [m['id'] for m in a['members']]
    return []


TAGS = {}   # conflict tags: two effects carrying a conflicting pair never share one weapon
INFO = {}   # informational tags: describe a build axis, never conflict


def _add(store, ids, tags):
    for i in ids:
        if i not in ALLSET:
            raise SystemExit(f'tagging unknown effect: {i}')
        store.setdefault(i, [])
        for t in tags:
            if t not in store[i]:
                store[i].append(t)


def tag(ids, *tags):
    _add(TAGS, ids, tags)


def info(ids, *tags):
    _add(INFO, ids, tags)


# ── damage-taken / health profile ───────────────────────────────────────────
# Full Health bonus cannot survive any self-inflicted or forced damage.
tag(R('WepFx_FullStrength', 1, 5), 'hp-full')
tag(['Stance_ShellOnHighHP'], 'hp-full')

# "until a hit is received" / "when no damage taken".
tag(R('WepFx_Immaculate', 1, 5), 'no-hit')
tag(['DMG+WhenNoDMGTaken', 'Masks_BestialMeter+OnParrybutResetOnDMG'], 'no-hit')

# Low-Health payoffs.
tag(['Stance_VirtuoseOnLowHP'], 'hp-low')

# Self-inflicted damage / status ticks (drop you off full HP / break no-hit).
tag(['SelfBurnButDmg+'] + R('WepFx_SelfBurn', 2, 3) + R('WepFx_SelfBlight', 1, 3), 'self-damage')

# Suicide ends the run, so it cannot coexist with a full / no-damage plan.
tag(['AutoDeath'], 'on-death')

# First Life pays +damage until you die, then *penalises* you: its whole plan is
# "stay on the first life". Dying is MacabrePower's payoff, so that ladder keeps
# the informational death-trigger tag (opposite plan); only First Life is a hard
# survival condition. Hard-blocked against anything that chips you down or ends
# the run — self-damage, self-status, on-death (see CONFLICTS).
tag(R('WepFx_FirstLife', 1, 3), 'first-life')

# ── healing ─────────────────────────────────────────────────────────────────
tag(['AttackLifesteal_Weapon', 'HealOnCrit', 'LightSkillsRegenHP', 'WepFx_RegenBlade',
     'Stance_HealOnSwitch', 'Stance_VirtuoseHealAllies', 'Stance_BurnHealOnDStance',
     'Foretell_HealOnApply', 'Foretell_HealEfficiencyPerMoonCharge',
     'Foretell_HealOnTurnStartConsumeMoonCharge', 'Masks_RegenOnCaster',
     'Perfection_AttackLifestealOnD', 'Perfection_MinC']
    + R('WepFx_HealParry', 1, 3), 'heal')
tag(['Perfection_StartRankSButNoHeal', 'Perfection_PerfectionLostOnHeal'], 'no-heal')

# ── status: keep vs consume ─────────────────────────────────────────────────
tag(['Perfection_NoPerfectionLossOnPowerlessEnemy',
     'Perfection_Perfection+1OnPowerlessEnemy'], 'keep-powerless')
tag([i for i in ALLSET if i.startswith('WepFx_PowerlessConsume')], 'consume-powerless')

# Stains: scaling per active Stain vs consuming Stains.
tag(['WepFx_DarkDmgPerStain100', 'Stains_IncreaseDMGPerLightStain',
     'Stains_IncreaseBurnDMGPerFireStain', 'Stains_IncreaseCritChancePerFireStain',
     'Stains_IncreaseBreakDMGPerEarthStain', 'Stains_FullCritIfFullStain'], 'stain-scale')
tag(['Stains_ConsumeFourDarkStainsDMG+OnSkills', 'Stains_ConsumeDarkStainsOnAttackDMG+',
     'Stains_APOnConsumption', 'Stains_GainAPOnLightStainConsumption',
     'WepFx_LightOnConsume100'], 'consume-stain')

# ── crit: forced crits vs the alternating-crit plan ─────────────────────────
tag(['Perfection_AlwaysCritOnRankS', 'Stains_FullCritIfFullStain', 'Stance_FullCritIfStanceless',
     'Masks_AlwaysCritOnAlmighty', 'Foretell_ChangeTwilightAlwaysCrit',
     'Foretell_MoonSkillsAlwaysCritOnMoon'], 'crit-always')
tag(R('WepFx_AltCrit', 1, 5), 'crit-alternate')

# ── party shape ─────────────────────────────────────────────────────────────
tag(R('WepFx_SoloFighter', 1, 3), 'solo')
tag(R('WepFx_MacabrePower', 1, 3) + ['ShieldAllHeroesOnHeavy', 'Stance_VirtuoseHealAllies',
                                     'Masks_GainAPAllAlliesOnAlmightySwitch',
                                     'Masks_Overcharge+APOnAllies'], 'ally')

# ── soft build axes (informational — describe a situation, never block a pick)
# In particular the Shield / Shell cluster is *synergy*: making Shields and
# spending them is a rotation, and Shell feeds Shield through ShieldOnShell.
info(R('WepFx_FirstOffensive', 1, 5) + ['firstHitApplyMark', 'Foretell_CurseSelfButDMG+'],
     'reckless')  # raises damage taken / first hit
info(arch_members('counter'), 'counter')

# Multi-hit payoffs scale with the number of hits; first-hit payoffs only fire
# once. A multi-hit build still gets its first hit, so these never conflict.
info(['DamageBonusPerHit', 'DoubleHit', 'LuneDoubleHit', 'FrenzyAttack', 'DamagePerMaskChange',
      'Foretell_DoubleChargeGain', 'Perfection_Attackx2', 'Perfection_Parryx2',
      'Masks_WheelSpinOnAttack', 'AttackPlayAgain', 'FreeAimDoubleShot'], 'multi-hit')

# death-trigger stays informational: MacabrePower wants to die (its payoff), so
# it must not be swept into the First Life survival conflict. The First Life
# ladder carries the conflict tag `first-life` instead (see the health-profile
# block above).
info(R('WepFx_MacabrePower', 1, 3) + R('WepFx_FirstLife', 1, 3), 'death-trigger')
info(['SecondChance', 'DamageBonusOnRevive', 'Masks_InstantReviveOnDeathOnAlmighty'], 'revive')

# Shields (absorb points): producers, payoffs that scale with them, and the
# base-attack spender. All three belong in one rotation.
info(['ShieldOnCounter', 'ShieldAllHeroesOnHeavy', 'CanStealShield', 'Stance_ShieldOnParry',
      'Stance_ShieldOnOSstance', 'Stance_VirtuoseDoubleShields',
      'Foretell_ShieldOnTurnStartWhenSunCharge', 'Stains_EarthAppliesShield'], 'shield-gen')
info(['APOnShield', 'CounterDamagePerShield', 'WepFx_CounterShield2', 'WepFx_CounterShield3'],
     'shield-payoff')
info(['AttackConsumeShieldForExtraDMG'] + R('WepFx_AtkShield', 2, 3), 'shield-spend')

# Shell (damage-reduction buff). ShieldOnShell bridges Shell → Shield.
info(['Stance_ShellOnHighHP', 'Stance_ShellOutVStance', 'Masks_ShellOnHeavy',
      'ShieldOnShell'], 'shell-gen')
info(['ShieldOnShell', 'WepFx_ShellBlade'], 'shell-need')

# Effects that break the *enemy's* Shields (a Break build, not own-Shield).
info(['FreeAimBreaksMoreShields', 'MarkBreakShields',
      'Foretell_ApplyForetellOnShieldBreakPerSunCharge', 'Stance_VirtuoseOnShieldBreak'],
     'shield-break')

# Status consumers other than Powerless (kept one-per-weapon by the exclusive
# group, so these are informational here).
info([i for i in ALLSET if i.startswith('WepFx_SlowConsume')], 'consume-slow')
info([i for i in ALLSET if i.startswith('WepFx_DefenselessConsume')], 'consume-defenseless')


def RN(prefix, lo, hi, suffix=''):
    return [f'{prefix}{i}{suffix}' for i in range(lo, hi + 1)]


# ── behaviour tags (the vocabulary the synergy table speaks) ────────────────
BURN_APPLY = ['AttackBurn', 'AttackBurnWhilePowerful', 'BurnOnCrit',
              'CritAppliesBurn', 'CounterBurn', 'WepFx_CounterBurn10', 'LightDMGBurnOnCrit',
              'LightDamageBurn', 'MarkIsBurn', 'Stains_ApplyBurnStackPerFireStainOnAttack',
              'Foretell_ApplyBurnOnHitTakenOnSun', 'Foretell_ApplyBurnEveryXForetell',
              'Foretell_ApplyBurnPerSunChargeOnConsume', 'Foretell_ApplyBurnPerSunChargeOnCounterAttack',
              'Foretell_AttackConsumeToApplyBurnMoonCharge', 'Perfection_BurnOnBaseAtkPerRank',
              'Stance_OffensiveBurnStacks', 'BurnInVirtuose', 'AlmightyDoubleBurn']
BURN_SCALE = ['BurnDamage'] + RN('WepFx_BurnDmg', 2, 5) + [
    'Stains_IncreaseBurnDMGPerFireStain', 'Foretell_OnTwilightBurnDMG+',
    'Perfection_BurnDamagePerRank', 'Stance_VirtuoseDoubleDMG', 'Stance_VirtuoseDoubleBurn']
BURN_CONSUME = RN('WepFx_BurnConsume', 1, 5, 'Skills') + [
    'ThunderSkillConsumeBurn', 'WepFx_ThunderBurn250', 'AlmightyConsumeBurn']

ON_CRIT = ['APOnCrit', 'HealOnCrit', 'GradientPointOnCrit', 'TriggerLightningOnCrit',
           'WepFx_LightningCrit2', 'WepFx_LightningCrit3', 'BurnOnCrit', 'CritAppliesBurn',
           'LightDMGBurnOnCrit', 'Foretell_ApplyForetellOnCrit', 'Perfection_Perfection+1OnCrit']
CRIT_CHANCE = ['CritPerBurn', 'Stains_IncreaseCritChancePerFireStain', 'Stance_CritChanceDefensive',
               'Stance_CritChanceOffensive', 'Masks_CritChanceOnAgile']
CRIT_SCALING = RN('WepFx_CriticalChain', 1, 5)  # damage per consecutive Critical Hit

MARK_APPLY = ['MarkOnBaseAttack', 'firstHitApplyMark', 'PowerlessMark', 'InflictMarkIfSkillLess4Ap',
              'Foretell_OnTwilightSwitchApplyMarkAOE', 'Foretell_MarkOnConsume']
MARK_PAYOFF = ['MarkBreakShields', 'MarkIsBurn', 'Perfection_RankUpOnMark', 'SecondHitDontConsumeMark',
               'Stance_VirtuoseKeepMark', 'Foretell_DontRemoveMarkOnTwilight',
               'Foretell_ApplyForetellOnApplyMark']
KEEP_MARK = ['SecondHitDontConsumeMark', 'Stance_VirtuoseKeepMark', 'Foretell_DontRemoveMarkOnTwilight']
POWERLESS_APPLY = ['PowerlessMark', 'PowerlessOnMarking', 'PowerlessOnCounter']
BUFF_APPLY = ['Masks_PowerfulOnBalanced', 'Masks_RushOnAgile', 'Perfection_PowerfulOnB',
              'Perfection_RushOnS', 'Foretell_BuffsOnStates', 'Stance_ShellOnHighHP',
              'Stance_ShellOutVStance', 'Masks_ShellOnHeavy', 'Masks_RegenOnCaster']
BUFF_CONDITIONAL = ['WepFx_PowerfulBlade', 'WepFx_RushBlade', 'WepFx_ShellBlade', 'WepFx_RegenBlade',
                    'WepFx_BerserkBlade', 'WepFx_BlightBlade', 'WepFx_CursedBlade']
SELF_STATUS = (RN('WepFx_SelfBerserk', 1, 3) + RN('WepFx_SelfBlight', 1, 3)
               + RN('WepFx_SelfBurn', 2, 3) + ['SelfBurnButDmg+', 'Foretell_CurseSelfButDMG+'])
# Self status (berserk / curse, and the blight / burn ticks) is a conflict tag:
# it is a committed risk that First Life's "never die" plan cannot take. This
# also makes these effects non-neutral for the wildcard slot (a deliberate
# knock-on — see the First Life synergy comment).
tag(SELF_STATUS, 'self-status')

# Stains. The random/cycling generators are the only ones that can fill four
# slots with *different* elements (needed by SimonimOvercharge); light/dark and
# the Dark conversion actively waste or remove the required types.
STAIN_ANY = ['Stains_TurnStart+StainsIfNoStain', 'WepFx_TurnStart3', 'WepFx_TurnStart4',
             'Stains_RandomGeneration', 'Stains_RandomGenerationOnAttack',
             'Stains_GenerateLightOrDarkStainRandom']
STAIN_LIGHTDARK = ['Stains_GenerateLightStainRandom', 'Stains_GenerateDarkStainRandom',
                   'WepFx_LightOnConsume100', 'Stains_+RandomStainOnHeal',
                   'Stains_GenerateLightOrDarkStainRandom']
STAIN_CONVERT = ['BaseAttackToDarkStain']
STAIN_FIRE = ['Stains_FireStart', 'WepFx_FireStart2', 'WepFx_FireStart4', 'Stains_FireStainOnTurnStart',
              'Stains_ApplyBurnStackPerFireStainOnAttack', 'Stains_LightningOnFire']
STAIN_ICE = ['Stains_IceStart', 'WepFx_IceStart2', 'WepFx_IceStart4', 'Stains_IceStainOnTurnStart',
             'Stains_Ice+IfSelfFrozen']
STAIN_LIGHTNING = ['Stains_LightningStart', 'WepFx_LightningStart2', 'WepFx_LightningStart4']
STAIN_EARTH = ['Stains_EarthStart', 'WepFx_EarthStart2', 'WepFx_EarthStart4',
               'Stains_EarthStainOnTurnStart', 'Stains_AttackEarth+']
STAIN_LIGHT = ['Stains_AttackLight+', 'Stains_+RandomStainOnHeal', 'Stains_GenerateStainsOnGradientAttack',
               'Stains_GenerateLightStainRandom', 'WepFx_LightOnConsume100',
               'Stains_GenerateLightOrDarkStainRandom']
STAIN_DARK = ['Stains_GenerateDarkStainRandom', 'BaseAttackToDarkStain',
              'Stains_GenerateLightOrDarkStainRandom']
STAIN_ECHO = [i for i in ALLSET if i.startswith('WepFx_Echo')] + ['Stains_LightningOnFire']

PERFECTION_GAIN = ['StartD', 'Perfection_OnTurnStart', 'Perfection_Attackx2', 'Perfection_Parryx2',
                   'Perfection_GainAPOnRankUp', 'Perfection_RankUpOnCounter', 'Perfection_RankUpOnMark',
                   'Perfection_DoublePerfectionVSBurn', 'Perfection_DoubleOnFreeAimShots',
                   'Perfection_Perfection+1OnCrit', 'Perfection_Perfection+1OnPowerful',
                   'Perfection_Perfection+1OnPowerlessEnemy', 'Perfection_Perfection+butDMG+']
PERFECTION_PAYOFF = ['DamageOnPerfectionRank', 'Perfection_BurnDamagePerRank',
                     'Perfection_IncreasedDamageOnRankD', 'Perfection_AlwaysCritOnRankS',
                     'Perfection_PerfectionBonusDamageShare', 'Perfection_SurvivorRankA']
PERFECTION_PROTECT = ['PerfectDefense', 'PerfectDefenseB', 'Perfection_RankLossOneByOne',
                      'Perfection_NoPerfectionLossButNoDamageIncrease', 'Perfection_MinC',
                      'Perfection_NoPerfectionLossOnPowerlessEnemy']

ALMIGHTY = ['Masks_AlwaysCritOnAlmighty', 'Masks_APOnAlmighty', 'Masks_InstantReplayOnAlmighty',
            'Masks_InstantReviveOnDeathOnAlmighty', 'DoubleFreeAimDamageAlmighty', 'AlmightyConsumeBurn',
            'AlmightyDoubleBurn', 'Masks_DoubleBreakDMGOnAlmighty', 'Masks_SwitchToAlmightyOnBreak',
            'Masks_StartAlmighty', 'Masks_GainAPAllAlliesOnAlmightySwitch']
HEAVY = ['Masks_ShellOnHeavy', 'ShieldAllHeroesOnHeavy', 'ReducedDamageOnHeavy',
         'Masks_GainAPOnDMGTakenOnHeavy', 'BaseAttackToHeavy', 'Masks_StartHeavy']
WHEEL = ['Masks_WheelSpinOnAttack', 'Masks_WheelSpinOnFreeAimShot', 'FreeAimForwardWheel',
         'BaseAttackToHeavy', 'Masks_ReverseBestialMeter', 'Masks_BestialMeter+OnParrybutResetOnDMG',
         'DamagePerMaskChange']
OVERCHARGE = ['Masks_DMG+OverchargedSkills', 'Masks_DMG+BonusStackOnOverchargedSkillsChain',
              'Masks_Overcharge+APOnAllies', 'WepFx_MaskStack50']
MASK_SWITCH = ['Masks_GainAPOnMaskSwitch', 'DamagePerMaskChange', 'Masks_SwitchToAlmightyOnBreak',
               'Masks_StartHeavy', 'Masks_StartAlmighty', 'Masks_StartAgile', 'Masks_StartBalanced',
               'Masks_StartCaster']

FORETELL_APPLY = ['Foretell_ApplyOnAttack', 'Foretell_ApplyOnCounter',
                  'Foretell_ApplyForetellOnCrit',
                  'Foretell_ApplyForetellOnApplyMark', 'FourthHitForetell',
                  'Foretell_ApplyMaxForetellOnSunSkills', 'Foretell_ApplyForetellPerChargeOnTwilightSwitch',
                  'Foretell_PropagateForetellOnApplication', 'Foretell_ApplyForetellOnShieldBreakPerSunCharge']
FORETELL_CONSUME = ['Foretell_SunSkillsDMG+PerForetell', 'Foretell_FreeAimShotsConsumeForDMG+',
                    'Foretell_ConsumeChargesOnFreeAim', 'ShotApplyAttackConsumeForetell',
                    'Foretell_AttackConsumeToApplyBurnMoonCharge', 'Foretell_AttackConsumeBurnToApplyForetell',
                    'Foretell_GivesAPOnConsumingCharge', 'TwilightForetellNotConsumed']
CHARGE_GEN = ['Foretell_SunSkillsCharge+', 'Foretell_MoonSkillsCharge+', 'Foretell_DoubleChargeGain',
              'FourthHitForetell', 'Foretell_GivesSunChargesOnCounterAttack',
              'Foretell_AttackGainMoonCharge', 'AttackSunItemMoon', 'RandomChargeTurnStart']
CHARGE_CONSUME = ['Foretell_AttackConsumeSunCharge', 'Foretell_ConsumeChargesOnFreeAim',
                  'Foretell_AttackConsumeToApplyBurnMoonCharge', 'Foretell_GivesAPOnConsumingCharge',
                  'Foretell_SunAPIncrease', 'Foretell_HealOnTurnStartConsumeMoonCharge',
                  'Foretell_GainAPPerMoonChargeOnTurnStart', 'DamagePerSunCharge',
                  'Foretell_DMG+PerMoonCharge', 'ChargesBalance']
SUN_PAYOFF = ['SunAPDiscount', 'Foretell_SunSkillsCharge+', 'Foretell_SunSkillsDMG+PerForetell',
              'Foretell_ApplyMaxForetellOnSunSkills', 'DamagePerSunCharge',
              'Foretell_ShieldOnTurnStartWhenSunCharge', 'Foretell_SunAPIncrease',
              'Foretell_ApplyBurnPerSunChargeOnConsume',
              'Foretell_ApplyBurnPerSunChargeOnCounterAttack', 'Foretell_ApplyBurnOnHitTakenOnSun']
MOON_PAYOFF = ['Foretell_MoonSkillsAlwaysCritOnMoon', 'Foretell_MoonSkillsCharge+',
               'Foretell_DMG+PerMoonCharge', 'Foretell_HealEfficiencyPerMoonCharge',
               'Foretell_HealOnTurnStartConsumeMoonCharge', 'Foretell_GainAPPerMoonChargeOnTurnStart',
               'Foretell_AttackGainMoonCharge']
TWILIGHT_ENTER = ['Foretell_ExtendTwilight', 'Foretell_ExtendOrIncreaseTwilight',
                  'Foretell_ApplyForetellPerChargeOnTwilightSwitch',
                  'Foretell_DealAOEDamagePerChargeOnTwilightSwitch',
                  'Foretell_OnTwilightSwitchApplyMarkAOE', 'Foretell_InstantReplayOnTwilight',
                  'Foretell_ResetCurseOnTwilight', 'Foretell_DarkConversionOnTwilight']
TWILIGHT_PAYOFF = ['Foretell_TwilightFreeAimDMG+', 'Foretell_OnTwilightBurnDMG+',
                   'Foretell_DontRemoveMarkOnTwilight', 'Foretell_DealAOEDamagePerChargeOnTwilightSwitch',
                   'Foretell_ApplyForetellPerChargeOnTwilightSwitch', 'TwilightForetellNotConsumed',
                   'Foretell_DarkConversionOnTwilight', 'Foretell_ExtendOrIncreaseTwilight']

GRADIENT_GEN = ['GradientPointOnCrit', 'GradientChargesOnParry', 'Stance_DefensiveGradientCharges']
GRADIENT_PAYOFF = ['MoreGradientDamageLessCharge', 'GradientAttackAndCounterDMG+',
                   'Stains_GenerateStainsOnGradientAttack']

BREAK_DMG = ['BreakDMG+', 'WepFx_Break2', 'WepFx_Break3', 'BreakEarthBurst', 'Stance_DefensiveBreakDamage',
             'Masks_DoubleBreakDMGOnAlmighty', 'Stains_IncreaseBreakDMGPerEarthStain']
BREAK_PAYOFF = ['APonBreak', 'MarkBreakShields', 'Perfection_SwitchRankSOnBreak',
                'Stance_VirtuoseOnShieldBreak', 'Masks_SwitchToAlmightyOnBreak',
                'Foretell_ApplyForetellOnShieldBreakPerSunCharge']
STUN = RN('WepFx_StunBoost', 1, 5)

AP_GAIN = ['APOnCrit', 'APonBreak', 'APOnCounter', 'APOnShield', 'Perfection_GainAPOnRankUp',
           'Masks_APOnAlmighty', 'Masks_GainAPOnMaskSwitch', 'Masks_GainAPOnDMGTakenOnHeavy',
           'GainAPOnApplyBuffsDebuffs', 'Stance_APOnSwitch', 'Stance_DefensiveSharedAP',
           'Foretell_GivesAPOnConsumingCharge', 'Foretell_GainAPPerMoonChargeOnTurnStart',
           'Foretell_SunAPIncrease', 'Stains_APOnConsumption', 'Stains_GainAPOnLightStainConsumption',
           'Masks_Overcharge+APOnAllies', 'Masks_GainAPAllAlliesOnAlmightySwitch']
AP_COST = ['EarthAPCost-', 'FireAPCost-', 'IceAPCost-', 'LightningAPCost-', 'PhysicalAPCost-',
           'HealAPCost-', 'SupportSkillsCostReduced', 'SunAPDiscount', 'Mask_APCost-1OnCaster']
EXTRA_ACTION = ['AttackPlayAgain', 'Masks_InstantReplayOnAlmighty', 'Foretell_InstantReplayOnTwilight',
                'InstantReplayOnOverchargedHealSkills']

BASE_ATTACK = ['AugmentedAttack_Weapon', 'WepFx_AugAtk2', 'WepFx_AugAtk3', 'AttackBurn',
               'AttackBurnWhilePowerful', 'AttackLifesteal_Weapon',
               'AttackPlayAgain', 'AttackConsumeShieldForExtraDMG', 'AttackSunItemMoon',
               'MarkOnBaseAttack', 'BaseAttackToHeavy', 'BaseAttackToDarkStain',
               'Stains_ApplyBurnStackPerFireStainOnAttack', 'Stains_ConsumeDarkStainsOnAttackDMG+',
               'Perfection_BurnOnBaseAtkPerRank', 'Perfection_DoublePerfectionVSBurn']
SKILL_ATTACK = ['SkillsEarthDMG+', 'SkillsFireDMG+', 'SkillsIceDMG+', 'SkillsLightningDMG+',
                'SkillsPhysicalDMG+', 'SkillsDarkDMG+', 'SkillsLightDMG+', 'SkillsLightToPhysical',
                'Foretell_SunSkillsDMG+PerForetell', 'Foretell_SunSkillsCharge+',
                'Foretell_MoonSkillsCharge+', 'ThunderSkillConsumeBurn', 'WepFx_ThunderBurn250',
                'Masks_DMG+OverchargedSkills', 'Masks_DMG+BonusStackOnOverchargedSkillsChain',
                'WepFx_MaskStack50', 'Stains_ConsumeFourDarkStainsDMG+OnSkills', 'Stains_OverchargeDMG+',
                'Stains_ElementalCyclingDMG+', 'NoOverchargeSkillBuff',
                'InstantReplayOnOverchargedHealSkills', 'LightSkillsRegenHP', 'More4ApDamageBonus',
                'DamageBonusPerHit', 'DoubleHit', 'LuneDoubleHit']
FREE_AIM = ['AugmentedAim_Weapon', 'FreeAimDoubleShot', 'FreeAimBreaksMoreShields', 'FreeAimForwardWheel',
            'Masks_DMG+FreeAimIfNotAlmighty', 'DoubleFreeAimDamageAlmighty',
            'Foretell_TwilightFreeAimDMG+', 'Foretell_FreeAimShotsConsumeForDMG+',
            'Foretell_ConsumeChargesOnFreeAim', 'Perfection_DoubleOnFreeAimShots',
            'Masks_WheelSpinOnFreeAimShot', 'Stains_RandomGeneration']

for _tag, _ids in [
    ('burn-apply', BURN_APPLY), ('burn-scale', BURN_SCALE), ('burn-consume', BURN_CONSUME),
    ('on-crit', ON_CRIT), ('crit-chance', CRIT_CHANCE), ('crit-damage', ['Masks_CritDMG+OnBalanced']),
    ('crit-scaling', CRIT_SCALING),
    ('mark-apply', MARK_APPLY), ('mark-payoff', MARK_PAYOFF), ('keep-mark', KEEP_MARK),
    ('powerless-apply', POWERLESS_APPLY), ('buff-apply', BUFF_APPLY),
    ('buff-conditional', BUFF_CONDITIONAL),
    ('stain-gen-any', STAIN_ANY), ('stain-gen-lightdark', STAIN_LIGHTDARK),
    ('stain-convert', STAIN_CONVERT), ('stain-gen-fire', STAIN_FIRE), ('stain-gen-ice', STAIN_ICE),
    ('stain-gen-lightning', STAIN_LIGHTNING), ('stain-gen-earth', STAIN_EARTH),
    ('stain-gen-light', STAIN_LIGHT), ('stain-gen-dark', STAIN_DARK), ('stain-echo', STAIN_ECHO),
    ('stain-diversity', ['SimonimOvercharge']), ('stain-fullcrit', ['Stains_FullCritIfFullStain']),
    ('perfection-gain', PERFECTION_GAIN), ('perfection-payoff', PERFECTION_PAYOFF),
    ('perfection-protect', PERFECTION_PROTECT),
    ('almighty', ALMIGHTY), ('heavy', HEAVY), ('wheel', WHEEL), ('overcharge', OVERCHARGE),
    ('mask-switch', MASK_SWITCH),
    ('foretell-apply', FORETELL_APPLY), ('foretell-consume', FORETELL_CONSUME),
    ('charge-gen', CHARGE_GEN), ('charge-consume', CHARGE_CONSUME),
    ('sun-payoff', SUN_PAYOFF), ('moon-payoff', MOON_PAYOFF),
    ('twilight-enter', TWILIGHT_ENTER), ('twilight-payoff', TWILIGHT_PAYOFF),
    ('gradient-gen', GRADIENT_GEN), ('gradient-payoff', GRADIENT_PAYOFF),
    ('break-dmg', BREAK_DMG), ('break-payoff', BREAK_PAYOFF), ('stun', STUN),
    ('ap-gain', AP_GAIN), ('ap-cost', AP_COST), ('extra-action', EXTRA_ACTION),
    ('base-attack', BASE_ATTACK), ('skill-attack', SKILL_ATTACK), ('free-aim', FREE_AIM),
]:
    info(_ids, _tag)

# ── synergies (soft preference; never a block) ──────────────────────────────
# When a weapon already carries an effect tagged `when`, later picks gain draw
# weight toward `prefer` tags and lose it for `avoid` tags (extra weight =
# 1 + Σ weight, clamped at ≥1 so nothing becomes unpickable).
SYNERGIES = [
    # ── Shield / Shell ──
    {'when': 'shield-spend', 'prefer': ['shield-gen', 'shell-gen'], 'weight': 5},
    {'when': 'shell-need', 'prefer': ['shell-gen', 'shield-gen'], 'weight': 4},
    {'when': 'shield-gen', 'prefer': ['shield-payoff'], 'weight': 2},
    {'when': 'shield-payoff', 'prefer': ['shield-gen'], 'weight': 3},
    {'when': 'counter', 'prefer': ['shield-payoff', 'shield-gen'], 'weight': 2},
    {'when': 'shield-gen', 'prefer': ['counter'], 'weight': 1},
    {'when': 'shield-break', 'prefer': ['shield-gen'], 'weight': 3},
    {'when': 'shield-gen', 'prefer': ['shield-break'], 'weight': 1},
    {'when': 'shell-gen', 'prefer': ['shell-need', 'shield-gen'], 'weight': 2},
    {'when': 'hp-full', 'prefer': ['shell-gen'], 'weight': 1},
    # ── Burn ──
    {'when': 'burn-apply', 'prefer': ['burn-scale'], 'weight': 3},
    {'when': 'burn-scale', 'prefer': ['burn-apply'], 'weight': 3},
    {'when': 'burn-apply', 'prefer': ['burn-consume'], 'weight': 2},
    {'when': 'burn-consume', 'prefer': ['burn-apply'], 'weight': 2},
    {'when': 'burn-apply', 'prefer': ['crit-chance', 'on-crit'], 'weight': 2},
    {'when': 'burn-scale', 'prefer': ['crit-chance'], 'weight': 1},
    {'when': 'burn-apply', 'prefer': ['mark-apply'], 'weight': 1},
    {'when': 'burn-apply', 'prefer': ['sun-payoff'], 'weight': 1},
    # Self-inflicted damage wants the payoffs, buffs and sustain.
    {'when': 'self-status', 'prefer': ['burn-scale', 'buff-conditional', 'heal'], 'weight': 3},
    {'when': 'self-damage', 'prefer': ['heal', 'hp-low'], 'weight': 2},
    {'when': 'buff-apply', 'prefer': ['buff-conditional'], 'weight': 4},
    {'when': 'buff-conditional', 'prefer': ['buff-apply'], 'weight': 2},
    # ── Crit ──
    {'when': 'crit-chance', 'prefer': ['on-crit'], 'weight': 3},
    {'when': 'on-crit', 'prefer': ['crit-chance', 'crit-damage', 'crit-always'], 'weight': 3},
    {'when': 'crit-always', 'prefer': ['on-crit'], 'weight': 2},
    {'when': 'crit-alternate', 'prefer': ['crit-chance'], 'weight': 2},
    {'when': 'crit-scaling', 'prefer': ['crit-chance', 'multi-hit'], 'weight': 3},
    {'when': 'crit-chance', 'prefer': ['crit-scaling', 'on-crit'], 'weight': 2},
    # ── Status / Mark ──
    {'when': 'mark-apply', 'prefer': ['mark-payoff'], 'weight': 4},
    {'when': 'mark-payoff', 'prefer': ['mark-apply'], 'weight': 2},
    {'when': 'mark-apply', 'prefer': ['keep-mark'], 'weight': 1},
    {'when': 'powerless-apply', 'prefer': ['consume-powerless'], 'weight': 3},
    {'when': 'slow-apply', 'prefer': ['consume-slow'], 'weight': 3},
    {'when': 'defenceless-apply', 'prefer': ['consume-defenseless'], 'weight': 3},
    {'when': 'buff-apply', 'prefer': ['ap-gain'], 'weight': 1},
    # ── Multi-hit / base / skill / free aim ──
    {'when': 'base-attack', 'prefer': ['shield-spend', 'mark-apply', 'burn-apply'], 'weight': 2},
    {'when': 'free-aim', 'prefer': ['shield-break', 'stain-gen-any'], 'weight': 2},
    {'when': 'ap-gain', 'prefer': ['extra-action', 'ap-cost'], 'weight': 1},
    {'when': 'ap-cost', 'prefer': ['ap-gain', 'extra-action'], 'weight': 1},
    # ── Stains ──
    {'when': 'stain-gen-any', 'prefer': ['stain-scale', 'stain-echo'], 'weight': 3},
    {'when': 'stain-gen-fire', 'prefer': ['stain-echo', 'stain-scale', 'burn-scale'], 'weight': 2},
    {'when': 'stain-gen-ice', 'prefer': ['stain-echo', 'stain-scale'], 'weight': 2},
    {'when': 'stain-gen-lightning', 'prefer': ['stain-echo', 'stain-scale'], 'weight': 2},
    {'when': 'stain-gen-earth', 'prefer': ['stain-echo', 'stain-scale', 'break-dmg'], 'weight': 2},
    {'when': 'stain-gen-light', 'prefer': ['stain-echo', 'stain-scale'], 'weight': 2},
    {'when': 'stain-gen-dark', 'prefer': ['stain-echo', 'stain-scale'], 'weight': 2},
    {'when': 'consume-stain', 'prefer': ['stain-gen-any', 'stain-gen-lightdark'], 'weight': 2},
    {'when': 'stain-echo', 'prefer': ['stain-gen-any'], 'weight': 2},
    {'when': 'stain-gen-any', 'prefer': ['gradient-gen'], 'weight': 1},
    {'when': 'stain-fullcrit',
     'prefer': ['stain-gen-any', 'stain-gen-fire', 'stain-gen-ice', 'stain-gen-lightning',
                'stain-gen-earth'], 'weight': 2},
    # P70: four *distinct* stains. Only random/cycling generation can fill all
    # four slots with different elements; light/dark and the Dark conversion
    # waste or remove the types SimonimOvercharge needs, so avoid them.
    {'when': 'stain-diversity', 'prefer': ['stain-gen-any'], 'weight': 4,
     'avoid': ['stain-gen-lightdark', 'stain-convert']},
    # ── Gradient ──
    {'when': 'gradient-gen', 'prefer': ['gradient-payoff'], 'weight': 2},
    {'when': 'gradient-payoff', 'prefer': ['gradient-gen'], 'weight': 1},
    # ── Stances (Maelle) ──
    {'when': 'shield-gen', 'prefer': ['counter', 'break-payoff'], 'weight': 1},
    # ── Perfection (Verso) ──
    {'when': 'perfection-gain', 'prefer': ['perfection-payoff', 'perfection-protect'], 'weight': 2},
    {'when': 'perfection-payoff', 'prefer': ['perfection-gain'], 'weight': 1},
    {'when': 'perfection-protect', 'prefer': ['perfection-gain'], 'weight': 1},
    # ── Masks (Monoco) ──
    {'when': 'overcharge', 'prefer': ['skill-attack'], 'weight': 2},
    {'when': 'wheel', 'prefer': ['mask-switch'], 'weight': 2},
    {'when': 'mask-switch', 'prefer': ['wheel'], 'weight': 2},
    # ── Sciel ──
    {'when': 'foretell-apply', 'prefer': ['foretell-consume'], 'weight': 2},
    {'when': 'foretell-consume', 'prefer': ['foretell-apply', 'charge-gen'], 'weight': 2},
    {'when': 'charge-gen', 'prefer': ['charge-consume', 'sun-payoff', 'moon-payoff'], 'weight': 2},
    {'when': 'charge-consume', 'prefer': ['charge-gen'], 'weight': 2},
    {'when': 'sun-payoff', 'prefer': ['charge-gen'], 'weight': 1},
    {'when': 'moon-payoff', 'prefer': ['charge-gen'], 'weight': 1},
    {'when': 'twilight-enter', 'prefer': ['twilight-payoff', 'charge-gen'], 'weight': 1},
    {'when': 'twilight-payoff', 'prefer': ['twilight-enter', 'charge-gen'], 'weight': 1},
    # ── Break / Stun ──
    {'when': 'break-dmg', 'prefer': ['break-payoff', 'stun'], 'weight': 3},
    {'when': 'break-payoff', 'prefer': ['break-dmg'], 'weight': 1},
    {'when': 'stun', 'prefer': ['break-dmg'], 'weight': 1},
    # ── Sustain / party / HP ──
    {'when': 'reckless', 'prefer': ['heal'], 'weight': 3},
    {'when': 'revive', 'prefer': ['death-trigger'], 'weight': 1},
    {'when': 'death-trigger', 'prefer': ['revive', 'ally'], 'weight': 2},
    # First Life: +damage until you die, −damage once you have. It wants to stay
    # on the first life, so it pulls healing. It cannot sit next to anything that
    # chips you down or ends the run — self burn / blight (self-damage), self
    # curse / berserk (self-status) and AutoDeath (on-death) are now hard
    # CONFLICTS, so no `avoid` bias is needed here (and `avoid` could only floor
    # a weight at 1, never exclude the pairing).
    {'when': 'first-life', 'prefer': ['heal'], 'weight': 4},
    {'when': 'heal', 'prefer': ['first-life'], 'weight': 2},
    {'when': 'hp-low', 'prefer': ['self-damage'], 'weight': 2},
    {'when': 'no-hit', 'prefer': ['shell-gen', 'heal'], 'weight': 1},
    {'when': 'hp-full', 'prefer': ['heal', 'shell-gen'], 'weight': 1},
]

# ── conflicts ───────────────────────────────────────────────────────────────
# Only pairs that are genuinely mutually exclusive under any play. Soft axes
# (counter, reckless, shields, multi-hit, …) are informational and never block.
# hp-full and no-hit are *aligned* (taking no hits keeps you at full Health),
# and self-damage *helps* an hp-low plan, so neither pair conflicts.
CONFLICTS = [
    ('hp-full', 'hp-low'), ('hp-full', 'self-damage'), ('hp-full', 'on-death'),
    ('no-hit', 'hp-low'), ('no-hit', 'self-damage'), ('no-hit', 'on-death'),
    ('heal', 'no-heal'),
    ('keep-powerless', 'consume-powerless'),
    ('stain-scale', 'consume-stain'),
    ('crit-always', 'crit-alternate'),
    ('solo', 'ally'),
    # First Life ("stay alive to keep the bonus") is impossible next to anything
    # that chips you down or ends the run. Hard block, not a bias: Medalum could
    # still roll FirstLife3 + SelfBurn2/3 while `avoid` only floored the weight.
    ('first-life', 'self-damage'),
    ('first-life', 'self-status'),
    ('first-life', 'on-death'),
]

# Element-specific effects. These only do something on a weapon whose melee
# element matches (the randomizer already treats the melee element as the
# character's element identity for Lune's stains). Stain elements live in the
# archetype table; these are the remaining element-locked families.
ELEMENT_EFFECTS = {}
for el in ('Fire', 'Ice', 'Lightning', 'Earth', 'Light', 'Dark', 'Physical'):
    for eid in (f'Skills{el}DMG+',):
        ELEMENT_EFFECTS[eid] = el
for el in ('Fire', 'Ice', 'Lightning', 'Earth'):
    ELEMENT_EFFECTS[f'{el}APCost-'] = el
ELEMENT_EFFECTS['PhysicalAPCost-'] = 'Physical'
ELEMENT_EFFECTS['LightDMGBurnOnCrit'] = 'Light'
ELEMENT_EFFECTS['LightDamageBurn'] = 'Light'
ELEMENT_EFFECTS = {k: v for k, v in ELEMENT_EFFECTS.items() if k in ALLSET}

conflict_tags = sorted({t for tags in TAGS.values() for t in tags})
info_tags = sorted({t for tags in INFO.values() for t in tags})
merged = {i: sorted(set(TAGS.get(i, []) + INFO.get(i, []))) for i in sorted(set(TAGS) | set(INFO))}

out = {
    'version': 1,
    'generatedBy': 'Planning/archetypes/build_conditions.py',
    'conditionTags': conflict_tags,
    'informationalTags': info_tags,
    'conflicts': [list(c) for c in CONFLICTS],
    'synergies': SYNERGIES,
    'elementGate': {'enabled': True, 'elements': dict(sorted(ELEMENT_EFFECTS.items()))},
    'tags': merged,
    'conflictTags': conflict_tags,
}

os.makedirs(OUT.parent, exist_ok=True)
json.dump(out, open(OUT, 'w'), indent=2)
print(f'wrote {OUT.relative_to(ROOT)}')
print(f'  tagged {len(merged)} / {len(ALLSET)} T1-T5 effects '
      f'({len(TAGS)} with a conflict condition); {len(ALLSET) - len(merged)} neutral')
print(f'  conflict tags: {", ".join(out["conditionTags"])}')
print(f'  informational tags: {", ".join(out["informationalTags"])}')
print(f'  element-gated effects: {len(ELEMENT_EFFECTS)}')

# ── coverage sanity: every tag in a conflict must be used ───────────────────
used = {t for tags in TAGS.values() for t in tags}
for a, b in CONFLICTS:
    for t in (a, b):
        if t not in used:
            print(f'!! conflict tag never used: {t}')
uncovered = sorted(ALLSET - set(merged))
print(f'  neutral examples: {", ".join(uncovered[:8])} …')

#!/usr/bin/env python3
"""Proposes weapon-effect archetypes for the E33 Mod Editor randomizer.

Reads the live tier table + character compatibility + custom effect specs and
emits a review JSON plus per-character summaries. Every T1-T5 effect must land
in at least one archetype; DoNotUse effects are deliberately excluded.
"""
import json, os, collections, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
OUT = HERE
TIERS = json.load(open(ROOT / 'work/weapon_effect_tiers.json'))
CHARS = json.load(open(ROOT / 'work/weapon_effect_characters.json'))
CUSTOM = {e['id']: e for e in json.load(open(ROOT / 'work/weapon_effects.json'))['effects']}
# Condition layer (see build_conditions.py): the orthogonal build axis the
# randomizer enforces. Optional so the archetype table still builds without it.
COND_PATH = ROOT / 'src/E33ModEditor.Core/Loot/Data/weapon_effect_conditions.json'
COND = json.load(open(COND_PATH)) if COND_PATH.exists() else {
    'tags': {}, 'conflicts': [], 'conditionTags': [], 'informationalTags': [], 'elementGate': {}}


def cond_tags(eid):
    return COND['tags'].get(eid, [])

TIER_COLS = ['Tier1', 'Tier2', 'Tier3', 'Tier4', 'Tier5']
ALL = [i for c in TIER_COLS for i in TIERS[c]]
ALLSET = set(ALL)
DNU = TIERS['DoNotUse']

def R(prefix, lo, hi):
    return [f'{prefix}{i}' for i in range(lo, hi + 1)]

# consume variants: WepFx_<Status>Consume<N>[Skills]
def consume(status, n, skills=False):
    suffix = 'Skills' if skills else ''
    if n == 1:
        return [f'WepFx_{status}Consume{suffix}' if status == 'Slow' else f'WepFx_{status}Consume1{suffix}']
    return [f'WepFx_{status}Consume{n}{suffix}']

STATUS_CONSUMES = (
    ['WepFx_SlowConsume'] + consume('Slow', 2) + consume('Slow', 3)
    + ['WepFx_SlowConsumeSkills'] + consume('Slow', 2, True) + consume('Slow', 3, True)
    + ['WepFx_PowerlessConsume1', 'WepFx_PowerlessConsume2', 'WepFx_PowerlessConsume3',
       'WepFx_PowerlessConsume1Skills', 'WepFx_PowerlessConsume2Skills', 'WepFx_PowerlessConsume3Skills']
    + ['WepFx_DefenselessConsume1', 'WepFx_DefenselessConsume2', 'WepFx_DefenselessConsume3',
       'WepFx_DefenselessConsume1Skills', 'WepFx_DefenselessConsume2Skills', 'WepFx_DefenselessConsume3Skills']
)
STATUS_CONSUMES_SKILLS = [x for x in STATUS_CONSUMES if x.endswith('Skills')]
STATUS_CONSUMES_BASE = [x for x in STATUS_CONSUMES if not x.endswith('Skills')]

# Generic Burn-consume skills (ConsumingAttack graph kept on Burn, skills-only
# patch via the WepFx_*Skills id suffix): 1%/Burn, caps 25/50/100/250/1000.
BURN_CONSUMES_SKILLS = [f'WepFx_BurnConsume{n}Skills' for n in (1, 2, 3, 4, 5)]

STAIN_ELS = ['Fire', 'Ice', 'Lightning', 'Earth', 'Light', 'Dark']
ECHOES = [f'WepFx_Echo{a}{b}' for a in STAIN_ELS for b in STAIN_ELS if a != b]

def echo_from(eid):
    rest = eid[len('WepFx_Echo'):]
    for el in STAIN_ELS:  # Lightning before Light
        if rest.startswith(el) and rest != el:
            return el
    return None

# Element-specific stain effects (allowed only when the weapon element matches)
STAIN_FIRE = ['Stains_FireStart', 'WepFx_FireStart2', 'WepFx_FireStart4',
              'Stains_FireStainOnTurnStart', 'Stains_IncreaseCritChancePerFireStain',
              'Stains_IncreaseBurnDMGPerFireStain', 'Stains_ApplyBurnStackPerFireStainOnAttack',
              'Stains_LightningOnFire'] + [e for e in ECHOES if echo_from(e) == 'Fire']
STAIN_ICE = ['Stains_IceStart', 'WepFx_IceStart2', 'WepFx_IceStart4',
             'Stains_IceStainOnTurnStart', 'Stains_IceAppliesSlow',
             'Stains_Ice+IfSelfFrozen'] + [e for e in ECHOES if echo_from(e) == 'Ice']
STAIN_LIGHTNING = ['Stains_LightningStart', 'WepFx_LightningStart2', 'WepFx_LightningStart4'] + \
                  [e for e in ECHOES if echo_from(e) == 'Lightning']
STAIN_EARTH = ['Stains_EarthStart', 'WepFx_EarthStart2', 'WepFx_EarthStart4',
               'Stains_EarthStainOnTurnStart', 'Stains_AttackEarth+',
               'Stains_IncreaseBreakDMGPerEarthStain', 'Stains_EarthAppliesShield'] + \
              [e for e in ECHOES if echo_from(e) == 'Earth']
STAIN_LIGHT = ['Stains_AttackLight+', 'Stains_+RandomStainOnHeal',
               'Stains_GainAPOnLightStainConsumption', 'Stains_GenerateStainsOnGradientAttack',
               'Stains_GenerateLightStainRandom', 'Stains_IncreaseDMGPerLightStain',
               'WepFx_LightOnConsume100'] + [e for e in ECHOES if echo_from(e) == 'Light']
STAIN_DARK = ['Stains_GenerateDarkStainRandom', 'BaseAttackToDarkStain',
              'Stains_ConsumeDarkStainsOnAttackDMG+', 'Stains_ConsumeFourDarkStainsDMG+OnSkills',
              'WepFx_DarkDmgPerStain100'] + [e for e in ECHOES if echo_from(e) == 'Dark']
STAIN_NEUTRAL = ['Stains_TurnStart+StainsIfNoStain', 'WepFx_TurnStart3', 'WepFx_TurnStart4',
                 'Stains_RandomGeneration', 'Stains_RandomGenerationOnAttack',
                 'Stains_GenerateLightOrDarkStainRandom', 'Stains_APOnConsumption',
                 'Stains_OverchargeDMG+', 'Stains_ElementalCyclingDMG+',
                 'Stains_FullCritIfFullStain', 'NoOverchargeSkillBuff',
                 'InstantReplayOnOverchargedHealSkills', 'SimonimOvercharge',
                 'WepFx_ConsumeLightOrDarkDmg']

FORETELL = [
 'Foretell_ApplyBurnEveryXForetell', 'Foretell_ApplyBurnOnHitTakenOnSun',
 'Foretell_ApplyBurnPerSunChargeOnConsume', 'Foretell_ApplyBurnPerSunChargeOnCounterAttack',
 'Foretell_ApplyForetellOnApplyMark', 'Foretell_ApplyForetellOnCrit',
 'Foretell_ApplyForetellOnShieldBreakPerSunCharge', 'Foretell_ApplyForetellPerChargeOnTwilightSwitch',
 'Foretell_ApplyGreaterRushSlowOnSunMoon', 'Foretell_ApplyGreaterRushSlowOnTwilight',
 'Foretell_ApplyMaxForetellOnSunSkills', 'Foretell_ApplyOnAttack', 'Foretell_ApplyOnCounter',
 'Foretell_AttackConsumeBurnToApplyForetell', 'Foretell_AttackConsumeSunCharge',
 'Foretell_AttackConsumeToApplyBurnMoonCharge', 'Foretell_AttackGainMoonCharge',
 'Foretell_BaseAttackConsumeAllAPOnTwilight', 'Foretell_BuffsOnStates',
 'Foretell_ChangeTwilightAlwaysCrit', 'Foretell_ConsumeChargesOnFreeAim',
 'Foretell_CurseSelfButDMG+', 'Foretell_DarkConversionOnTwilight',
 'Foretell_DealAOEDamagePerChargeOnTwilightSwitch', 'Foretell_DMG+PerMoonCharge',
 'Foretell_DontRemoveMarkOnTwilight', 'Foretell_DoubleChargeGain',
 'Foretell_ExtendOrIncreaseTwilight', 'Foretell_ExtendTwilight',
 'Foretell_FreeAimShotsConsumeForDMG+', 'Foretell_GainAPPerMoonChargeOnTurnStart',
 'Foretell_GivesAPOnConsumingCharge', 'Foretell_GivesSunChargesOnCounterAttack',
 'Foretell_HealEfficiencyPerMoonCharge', 'Foretell_HealOnApply',
 'Foretell_HealOnTurnStartConsumeMoonCharge', 'Foretell_InstantReplayOnTwilight',
 'Foretell_MarkOnConsume', 'Foretell_MoonSkillsAlwaysCritOnMoon', 'Foretell_MoonSkillsCharge+',
 'Foretell_OnTwilightBurnDMG+', 'Foretell_OnTwilightSwitchApplyMarkAOE',
 'Foretell_PropagateForetellOnApplication', 'Foretell_PropagationOnKill',
 'Foretell_ResetCurseOnTwilight', 'Foretell_ShieldOnTurnStartWhenSunCharge',
 'Foretell_SunAPIncrease', 'Foretell_SunSkillsCharge+', 'Foretell_SunSkillsDMG+PerForetell',
 'Foretell_TwilightFreeAimDMG+', 'FourthHitForetell', 'ShotApplyAttackConsumeForetell',
 'TwilightForetellNotConsumed', 'RandomChargeTurnStart',
]
SCIEL_SUN = [
 'SunAPDiscount', 'Foretell_ApplyBurnOnHitTakenOnSun', 'Foretell_ApplyForetellOnShieldBreakPerSunCharge',
 'Foretell_AttackConsumeSunCharge', 'Foretell_GivesSunChargesOnCounterAttack',
 'Foretell_SunSkillsCharge+', 'Foretell_SunSkillsDMG+PerForetell', 'Foretell_SunAPIncrease',
 'Foretell_ApplyBurnPerSunChargeOnConsume', 'Foretell_ApplyBurnPerSunChargeOnCounterAttack',
 'Foretell_ApplyMaxForetellOnSunSkills', 'DamagePerSunCharge',
 'Foretell_ShieldOnTurnStartWhenSunCharge', 'Foretell_ApplyGreaterRushSlowOnSunMoon',
 'AttackSunItemMoon', 'ChargesBalance',
]
SCIEL_MOON = [
 'Foretell_MoonSkillsAlwaysCritOnMoon', 'Foretell_MoonSkillsCharge+',
 'Foretell_GainAPPerMoonChargeOnTurnStart', 'Foretell_HealEfficiencyPerMoonCharge',
 'Foretell_HealOnTurnStartConsumeMoonCharge', 'Foretell_ApplyGreaterRushSlowOnSunMoon',
 'Foretell_DMG+PerMoonCharge', 'Foretell_AttackGainMoonCharge', 'AttackSunItemMoon', 'ChargesBalance',
]
SCIEL_TWILIGHT = [
 'Foretell_ChangeTwilightAlwaysCrit', 'Foretell_DarkConversionOnTwilight',
 'Foretell_OnTwilightBurnDMG+', 'Foretell_ResetCurseOnTwilight', 'Foretell_TwilightFreeAimDMG+',
 'Foretell_ApplyForetellPerChargeOnTwilightSwitch', 'Foretell_InstantReplayOnTwilight',
 'Foretell_PropagateForetellOnApplication', 'Foretell_ExtendTwilight',
 'Foretell_ExtendOrIncreaseTwilight', 'Foretell_DealAOEDamagePerChargeOnTwilightSwitch',
 'Foretell_OnTwilightSwitchApplyMarkAOE', 'Foretell_DontRemoveMarkOnTwilight',
 'Foretell_BaseAttackConsumeAllAPOnTwilight', 'Foretell_ApplyGreaterRushSlowOnTwilight',
 'TwilightForetellNotConsumed',
]

PERFECTION = [k for k in ALLSET if k.startswith('Perfection_') or k == 'PerfectionOnDamage'] + \
             ['PerfectDefense', 'PerfectDefenseB', 'StartD', 'AureoleRankSToD', 'DamageOnPerfectionRank']
MASKS = [k for k in ALLSET if k.startswith('Masks_') or k.startswith('Mask_')] + ['DamagePerMaskChange']
MASKS_WHEEL = ['Masks_ReverseBestialMeter', 'Masks_BestialMeter+OnParrybutResetOnDMG',
               'Masks_WheelSpinOnAttack', 'Masks_WheelSpinOnFreeAimShot',
               'FreeAimForwardWheel', 'BaseAttackToHeavy']
MASKS_OVERCHARGE = ['Masks_DMG+OverchargedSkills', 'Masks_DMG+BonusStackOnOverchargedSkillsChain',
                    'Masks_Overcharge+APOnAllies', 'WepFx_MaskStack50']
STANCES = [k for k in ALLSET if k.startswith('Stance_')]
STANCE_OFF = ['Stance_AttackOffensive', 'Stance_StancelessSwitchToOStance',
              'Stance_CritChanceOffensive', 'Stance_OffensiveBurnStacks', 'Stance_ShieldOnOSstance']
STANCE_DEF = ['Stance_AttackDefensive', 'Stance_StancelessSwitchToDStance',
              'Stance_CritChanceDefensive', 'Stance_DefensiveBreakDamage',
              'Stance_DefensiveGradientCharges', 'Stance_DefensiveSharedAP',
              'Stance_ShieldOnParry', 'Stance_BurnHealOnDStance', 'Stance_ShellOnHighHP']
STANCE_VIR = ['Stance_APOnSwitch', 'Stance_CleanseVirtuose', 'Stance_PierceOnVStance',
              'Stance_ShellOutVStance', 'Stance_HealOnSwitch', 'Stance_VirtuoseDoubleBurn',
              'Stance_VirtuoseHealAllies', 'Stance_VirtuoseDoubleDMG', 'Stance_VirtuoseKeepMark',
              'Stance_VirtuoseOnShieldBreak', 'Stance_VirtuoseDoubleShields', 'Stance_VirtuoseOnLowHP',
              'Stance_StartOnVStance', 'Stance_CounterSwitchToVStance', 'BurnInVirtuose']

# ── archetype table ──────────────────────────────────────────────────────────
ARCH = []

def A(aid, name, group, desc, links, members, element=None):
    members = sorted(set(m for m in members if m))
    ARCH.append(dict(id=aid, name=name, group=group, element=element, links=links,
                     members=members, desc=desc))

A('raw', 'Sustained Damage', 'generic',
  'Flat damage that builds over the fight: full Health, no-damage streaks, '
  'successive hits, revival, affinity bypass.',
  ['base', 'skill', 'crit', 'burn'],
  ['BypassAffinities', 'DamageBonusPerHit', 'DoubleHit', 'LuneDoubleHit', 'DamageBonusOnRevive',
   'DMG+WhenNoDMGTaken', 'FrenzyAttack', 'More4ApDamageBonus', 'SelfBurnButDmg+', 'SimonimOvercharge',
   'DamageOnPerfectionRank', 'DamagePerSunCharge', 'DamagePerMaskChange', 'Stance_VirtuoseDoubleDMG',
   'DMG+OnAlternatingPhysicalLight', 'Stains_IncreaseDMGPerLightStain']
  + R('WepFx_FirstLife', 1, 3) + R('WepFx_FirstOffensive', 1, 5) + R('WepFx_FullStrength', 1, 5)
  + R('WepFx_Immaculate', 1, 5) + R('WepFx_MacabrePower', 1, 3) + R('WepFx_SoloFighter', 1, 3))

A('base', 'Base Attack', 'generic',
  'Everything that improves, repeats or triggers off the basic attack.',
  # guard: the base-attack Shield spender (AttackConsumeShieldForExtraDMG)
  # needs the Shield/Shell generators that live in Shield & Sustain.
  ['raw', 'burn', 'crit', 'freeaim', 'counter', 'guard'],
  ['AugmentedAttack_Weapon', 'AugmentedAim_Weapon', 'AttackPlayAgain', 'AttackLifesteal_Weapon',
   'AttackConsumeShieldForExtraDMG', 'AttackSunItemMoon', 'AttackBurn', 'AttackBurnWhilePowerful',
   'MarkOnBaseAttack', 'firstHitApplyMark', 'BaseAttackToHeavy',
   'BaseAttackToDarkStain', 'FrenzyAttack', 'Stains_AttackEarth+', 'Stains_AttackLight+',
   'Stains_ApplyBurnStackPerFireStainOnAttack', 'Stains_ConsumeDarkStainsOnAttackDMG+']
  + R('WepFx_AugAtk', 2, 3) + STATUS_CONSUMES_BASE)

A('skill', 'Skill Damage', 'generic',
  'Skill multipliers, per-hit scaling, element conversions and Stain-consuming skill payoffs.',
  ['raw', 'crit', 'burn', 'tempo', 'status'],
  ['SkillsEarthDMG+', 'SkillsFireDMG+', 'SkillsIceDMG+', 'SkillsLightningDMG+', 'SkillsPhysicalDMG+',
   'SkillsDarkDMG+', 'SkillsLightDMG+', 'SkillsLightToPhysical', 'DamageBonusPerHit', 'DoubleHit',
   'LuneDoubleHit', 'More4ApDamageBonus', 'NoOverchargeSkillBuff', 'Stains_OverchargeDMG+',
   'Stains_ElementalCyclingDMG+', 'Stains_ConsumeFourDarkStainsDMG+OnSkills',
   'InstantReplayOnOverchargedHealSkills', 'Masks_DMG+OverchargedSkills',
   'Masks_DMG+BonusStackOnOverchargedSkillsChain', 'WepFx_MaskStack50', 'Foretell_SunSkillsDMG+PerForetell',
   'ThunderSkillConsumeBurn', 'WepFx_ThunderBurn250', 'LightSkillsRegenHP']
  + STATUS_CONSUMES_SKILLS + BURN_CONSUMES_SKILLS)

A('crit', 'Critical', 'generic',
  'Crit chance, crit damage and everything that triggers on or after a Critical hit.',
  ['raw', 'base', 'skill', 'burn', 'counter', 'freeaim', 'gradient'],
  ['APOnCrit', 'HealOnCrit', 'TriggerLightningOnCrit', 'BurnOnCrit', 'CritAppliesBurn', 'CritPerBurn',
   'LightDMGBurnOnCrit', 'AlwaysCritOnCounterDamage', 'GradientPointOnCrit',
   'Stains_IncreaseCritChancePerFireStain', 'Stains_FullCritIfFullStain', 'Masks_CritDMG+OnBalanced',
   'Masks_CritChanceOnAgile', 'Masks_AlwaysCritOnAlmighty', 'Stance_CritChanceDefensive',
   'Stance_CritChanceOffensive', 'Stance_FullCritIfStanceless', 'Foretell_ChangeTwilightAlwaysCrit',
   'Foretell_ApplyForetellOnCrit', 'Foretell_MoonSkillsAlwaysCritOnMoon', 'Perfection_AlwaysCritOnRankS',
   'Perfection_Perfection+1OnCrit']
  + R('WepFx_AltCrit', 1, 5) + R('WepFx_LightningCrit', 2, 3)
  + R('WepFx_CriticalChain', 1, 5))

A('break', 'Break & Stun', 'generic',
  'Break damage, AP-on-Break, Shield-break payoffs and Stun burst.',
  ['raw', 'base', 'skill', 'tempo', 'status', 'guard'],
  ['BreakDMG+', 'WepFx_Break2', 'WepFx_Break3', 'BreakEarthBurst', 'APonBreak', 'MarkBreakShields',
   'FreeAimBreaksMoreShields', 'Stance_DefensiveBreakDamage', 'Masks_DoubleBreakDMGOnAlmighty',
   'Stains_IncreaseBreakDMGPerEarthStain', 'Perfection_SwitchRankSOnBreak',
   'Stance_VirtuoseOnShieldBreak', 'Foretell_ApplyForetellOnShieldBreakPerSunCharge',
   'Masks_SwitchToAlmightyOnBreak'] + R('WepFx_StunBoost', 1, 5))

A('burn', 'Burn', 'generic',
  'Burn application, Burn damage multipliers and consume-Burn payoffs.',
  ['raw', 'base', 'skill', 'crit', 'counter', 'status', 'stain_fire'],
  ['BurnDamage', 'AttackBurn', 'AttackBurnWhilePowerful', 'BurnOnCrit',
   'CritAppliesBurn', 'CritPerBurn', 'MarkIsBurn', 'LightDMGBurnOnCrit', 'LightDamageBurn',
   'CounterBurn', 'WepFx_CounterBurn10', 'SelfBurnButDmg+', 'Stance_OffensiveBurnStacks',
   'Stance_VirtuoseDoubleBurn', 'Stance_BurnHealOnDStance', 'BurnInVirtuose',
   'Stains_ApplyBurnStackPerFireStainOnAttack', 'Stains_IncreaseBurnDMGPerFireStain',
   'Foretell_ApplyBurnOnHitTakenOnSun', 'Foretell_ApplyBurnEveryXForetell', 'Foretell_OnTwilightBurnDMG+',
   'Foretell_ApplyBurnPerSunChargeOnConsume', 'Foretell_ApplyBurnPerSunChargeOnCounterAttack',
   'Foretell_AttackConsumeBurnToApplyForetell', 'Foretell_AttackConsumeToApplyBurnMoonCharge',
   'Perfection_BurnDamagePerRank', 'Perfection_BurnOnBaseAtkPerRank', 'Perfection_DoublePerfectionVSBurn',
   'AlmightyConsumeBurn', 'AlmightyDoubleBurn', 'ThunderSkillConsumeBurn', 'WepFx_ThunderBurn250',
   'WepFx_AttackBurn5', 'WepFx_AttackBurn10'] + R('WepFx_BurnDmg', 2, 5) + R('WepFx_SelfBurn', 2, 3)
  + BURN_CONSUMES_SKILLS)

A('counter', 'Counter', 'generic',
  'Counterattack damage, on-Counter triggers and Counter-tank payoffs.',
  ['base', 'crit', 'burn', 'guard', 'status', 'gradient'],
  ['IncreaseCounterDamage', 'APOnCounter', 'CounterDefenseless', 'CounterBurn', 'WepFx_CounterBurn10',
   'CounterDamagePerShield', 'AlwaysCritOnCounterDamage', 'PowerlessOnCounter',
   'GradientAttackAndCounterDMG+', 'ShieldOnCounter', 'Stance_CounterSwitchToVStance',
   'Perfection_RankUpOnCounter', 'Foretell_GivesSunChargesOnCounterAttack',
   'Foretell_ApplyBurnPerSunChargeOnCounterAttack', 'Foretell_ApplyOnCounter']
  + R('WepFx_CounterDmg', 2, 3) + R('WepFx_CounterShield', 2, 3))

A('status', 'Status & Mark', 'generic',
  'Slowed / Powerless / Defenceless application and consumption, plus Mark tech.',
  ['skill', 'burn', 'counter', 'break', 'tempo', 'base'],
  ['PowerlessMark', 'PowerlessOnMarking', 'PowerlessOnCounter', 'CounterDefenseless',
   'MarkOnBaseAttack', 'firstHitApplyMark', 'MarkBreakShields', 'MarkIsBurn',
   'SecondHitDontConsumeMark', 'InflictMarkIfSkillLess4Ap', 'Stains_IceAppliesSlow',
   'GainAPOnApplyBuffsDebuffs', 'Perfection_RankUpOnMark', 'Perfection_Perfection+1OnPowerlessEnemy',
   'Perfection_NoPerfectionLossOnPowerlessEnemy', 'Foretell_MarkOnConsume',
   'Foretell_ApplyForetellOnApplyMark', 'Foretell_DontRemoveMarkOnTwilight',
   'Foretell_OnTwilightSwitchApplyMarkAOE', 'Stance_VirtuoseKeepMark']
  + STATUS_CONSUMES)

A('guard', 'Shield & Sustain', 'generic',
  'Shields, healing, damage reduction, revives and defensive triggers.',
  ['break', 'counter', 'crit', 'tempo', 'burn'],
  ['ShieldOnCounter', 'ShieldOnShell', 'ShieldAllHeroesOnHeavy', 'CanStealShield', 'APOnShield',
   'Stance_ShieldOnParry', 'Stance_ShieldOnOSstance', 'Stance_ShellOnHighHP', 'Stance_ShellOutVStance',
   'Stance_VirtuoseDoubleShields', 'Stance_VirtuoseHealAllies', 'Masks_ShellOnHeavy',
   'Masks_InstantReviveOnDeathOnAlmighty', 'AttackConsumeShieldForExtraDMG', 'WepFx_FrancoisShell1',
   'WepFx_FrancoisShell2', 'WepFx_FrancoisShell3', 'HealOnCrit', 'HealAPCost-', 'LightSkillsRegenHP',
   'AttackLifesteal_Weapon', 'SecondChance', 'PerfectDefense', 'ReducedDamageOnHeavy',
   'Stance_HealOnSwitch', 'Stance_CleanseVirtuose', 'Stance_BurnHealOnDStance', 'Foretell_HealOnApply',
   'Foretell_HealEfficiencyPerMoonCharge', 'Foretell_HealOnTurnStartConsumeMoonCharge',
   'Foretell_ShieldOnTurnStartWhenSunCharge', 'Perfection_AttackLifestealOnD', 'Perfection_RankLossOneByOne',
   'Perfection_SurvivorRankA', 'PerfectDefenseB', 'Perfection_MinC', 'Masks_RegenOnCaster',
   'WepFx_RegenBlade', 'WepFx_ShellBlade']
  + R('WepFx_AtkShield', 2, 3) + R('WepFx_HealParry', 1, 3))

A('tempo', 'AP & Tempo', 'generic',
  'AP discounts, AP gain and extra-action effects that accelerate any build.',
  ['base', 'skill', 'break', 'counter', 'guard', 'status'],
  ['FirstStrike', 'APOnCrit', 'APonBreak', 'APOnCounter', 'APOnShield', 'SupportSkillsCostReduced',
   'HealAPCost-', 'EarthAPCost-', 'FireAPCost-', 'IceAPCost-', 'LightningAPCost-', 'SunAPDiscount',
   'PhysicalAPCost-', 'Mask_APCost-1OnCaster', 'GainAPOnApplyBuffsDebuffs', 'AttackPlayAgain',
   'More4ApDamageBonus', 'InflictMarkIfSkillLess4Ap', 'Stance_APOnSwitch', 'Stance_DefensiveSharedAP',
   'Masks_APOnAlmighty', 'Masks_GainAPOnDMGTakenOnHeavy', 'Masks_GainAPAllAlliesOnAlmightySwitch',
   'Masks_Overcharge+APOnAllies', 'Masks_GainAPOnMaskSwitch', 'Perfection_GainAPOnRankUp',
   'Foretell_GivesAPOnConsumingCharge', 'Foretell_GainAPPerMoonChargeOnTurnStart',
   'Foretell_SunAPIncrease', 'Foretell_InstantReplayOnTwilight', 'Stains_GainAPOnLightStainConsumption',
   'Stains_APOnConsumption', 'Stains_Ice+IfSelfFrozen'])

A('gradient', 'Gradient', 'generic',
  'Gradient charge generation and Gradient attack/counter payoffs.',
  ['crit', 'counter', 'tempo'],
  ['GradientPointOnCrit', 'GradientChargesOnParry', 'GradientAttackAndCounterDMG+',
   'MoreGradientDamageLessCharge', 'Stance_DefensiveGradientCharges',
   'Stains_GenerateStainsOnGradientAttack'])

A('freeaim', 'Free Aim', 'generic',
  'Extra shots, Free Aim multipliers and Free Aim shield-breaking.',
  ['base', 'skill', 'crit'],
  ['AugmentedAim_Weapon', 'FreeAimDoubleShot', 'FreeAimBreaksMoreShields', 'FreeAimForwardWheel',
   'Masks_DMG+FreeAimIfNotAlmighty', 'DoubleFreeAimDamageAlmighty', 'Foretell_TwilightFreeAimDMG+',
   'Foretell_FreeAimShotsConsumeForDMG+', 'Foretell_ConsumeChargesOnFreeAim',
   'Perfection_DoubleOnFreeAimShots', 'Masks_WheelSpinOnFreeAimShot', 'Stains_RandomGeneration'])

A('buff', 'Ailment Blades', 'generic',
  'Flat damage while a buff/ailment is active, plus the buffs that switch them on.',
  ['status', 'raw', 'guard', 'burn', 'gambit'],
  ['WepFx_PowerfulBlade', 'WepFx_RushBlade', 'WepFx_RegenBlade', 'WepFx_ShellBlade',
   'WepFx_BerserkBlade', 'WepFx_BlightBlade', 'WepFx_CursedBlade', 'Perfection_RushOnS',
   'Perfection_PowerfulOnB', 'Perfection_Perfection+1OnPowerful', 'Masks_PowerfulOnBalanced',
   'Masks_RushOnAgile', 'Foretell_BuffsOnStates', 'Foretell_ApplyGreaterRushSlowOnSunMoon',
   'Foretell_ApplyGreaterRushSlowOnTwilight'])

A('gambit', 'Risk & Sacrifice', 'generic',
  'Self-inflicted statuses, death payoffs and revives: a high-risk damage plan.',
  ['raw', 'burn', 'buff', 'guard'],
  ['SelfBurnButDmg+', 'Foretell_CurseSelfButDMG+', 'Foretell_ResetCurseOnTwilight', 'AutoDeath',
   'SecondChance', 'DamageBonusOnRevive', 'Perfection_StartRankBButLessHP',
   'Perfection_StartRankSButNoHeal']
  + R('WepFx_SelfBerserk', 1, 3) + R('WepFx_SelfBlight', 1, 3) + R('WepFx_SelfBurn', 2, 3)
  + R('WepFx_FirstLife', 1, 3) + R('WepFx_MacabrePower', 1, 3))

# ── Lune stains ──────────────────────────────────────────────────────────────
A('stain', 'Stain Cycling', 'lune',
  'Element-neutral Stain generation, consumption and cycling payoffs. '
  'Usable by any Lune weapon, including Physical and Void.',
  ['burn', 'skill', 'crit', 'break', 'status', 'tempo', 'guard', 'stain_fire', 'stain_ice',
   'stain_lightning', 'stain_earth', 'stain_light', 'stain_dark'],
  STAIN_NEUTRAL)
A('stain_fire', 'Fire Stains', 'lune', 'Fire Stain generation and payoffs.',
  ['stain', 'burn', 'crit', 'base', 'tempo'], STAIN_FIRE, element='Fire')
A('stain_ice', 'Ice Stains', 'lune', 'Ice Stain generation and payoffs.',
  ['stain', 'status', 'guard', 'tempo'], STAIN_ICE, element='Ice')
A('stain_lightning', 'Lightning Stains', 'lune', 'Lightning Stain generation and echoes.',
  ['stain', 'tempo', 'skill'], STAIN_LIGHTNING, element='Lightning')
A('stain_earth', 'Earth Stains', 'lune', 'Earth Stain generation, Break and Shield payoffs.',
  ['stain', 'break', 'guard', 'base'], STAIN_EARTH, element='Earth')
A('stain_light', 'Light Stains', 'lune', 'Light Stain generation, AP and damage payoffs.',
  ['stain', 'tempo', 'gradient', 'guard', 'raw'], STAIN_LIGHT, element='Light')
A('stain_dark', 'Dark Stains', 'lune', 'Dark Stain conversion, consumption and damage payoffs.',
  ['stain', 'skill', 'raw', 'base'], STAIN_DARK, element='Dark')

# ── Maelle stances ───────────────────────────────────────────────────────────
A('stance_off', 'Offensive Stance', 'maelle', 'Offensive Stance attacks, crits, Burn and pierce.',
  ['burn', 'crit', 'base', 'stance_vir'], STANCE_OFF)
A('stance_def', 'Defensive Stance', 'maelle', 'Defensive Stance break, shields, gradient and shared AP.',
  ['guard', 'break', 'counter', 'gradient', 'tempo', 'stance_vir'], STANCE_DEF)
A('stance_vir', 'Virtuose', 'maelle', 'Virtuose payoff: doubled damage, Burn, Shields and utility.',
  ['stance_off', 'stance_def', 'burn', 'guard', 'status', 'counter', 'break', 'raw'], STANCE_VIR)

# ── Sciel sun/moon/foretell/twilight ─────────────────────────────────────────
A('foretell', 'Foretell', 'sciel', 'Foretell application, propagation and consumption.',
  ['sciel_sun', 'sciel_moon', 'sciel_twilight', 'burn', 'status', 'skill'], FORETELL)
A('sciel_sun', 'Sun Charges', 'sciel', 'Sun charge generation and Sun payoffs.',
  ['foretell', 'burn', 'tempo', 'guard', 'base'], SCIEL_SUN)
A('sciel_moon', 'Moon Charges', 'sciel', 'Moon charge generation and Moon payoffs.',
  ['foretell', 'guard', 'freeaim', 'skill', 'base'], SCIEL_MOON)
A('sciel_twilight', 'Twilight', 'sciel', 'Entering, extending and abusing Twilight state.',
  ['foretell', 'burn', 'freeaim', 'crit', 'status', 'raw'], SCIEL_TWILIGHT)

# ── Verso perfection ─────────────────────────────────────────────────────────
A('perfection', 'Perfection', 'verso', 'Rank gain, Rank protection and Rank payoffs.',
  ['burn', 'crit', 'counter', 'guard', 'base', 'freeaim', 'raw'], PERFECTION)

# ── Monoco masks ─────────────────────────────────────────────────────────────
A('mask', 'Masks', 'monoco', 'Mask switching, mask buffs and Almighty/Heavy/Caster payoffs.',
  ['guard', 'tempo', 'skill', 'freeaim', 'raw', 'wheel', 'overcharge', 'burn'], MASKS)
A('wheel', 'Bestial Wheel', 'monoco', 'Wheel spins, Parry meter and Heavy Mask switching.',
  ['mask', 'base', 'freeaim'], MASKS_WHEEL)
A('overcharge', 'Upgraded Skills', 'monoco', 'Overcharged/Upgraded Skill stacking and AP sharing.',
  ['mask', 'skill', 'tempo', 'burn'], MASKS_OVERCHARGE)

# ── validation ───────────────────────────────────────────────────────────────
by_id = {a['id']: a for a in ARCH}
for a in ARCH:
    missing = [m for m in a['members'] if m not in ALLSET]
    if missing:
        print(f"!! {a['id']} has ids not in T1-T5: {missing}")
    for l in a['links']:
        if l not in by_id:
            print(f"!! {a['id']} links unknown {l}")

listed = set()
for a in ARCH:
    listed |= set(a['members'])
uncovered = sorted(ALLSET - listed)
print(f'archetypes: {len(ARCH)}  effects in T1-T5: {len(ALLSET)}  uncovered: {len(uncovered)}')
for u in uncovered:
    print('  UNCOVERED', u)
print('DoNotUse excluded:', DNU)

# symmetric links
_sym = {a['id']: set(a['links']) for a in ARCH}
for a in ARCH:
    for l in a['links']:
        _sym[l].add(a['id'])
for a in ARCH:
    a['links'] = sorted(_sym[a['id']])

# tier lookup + per-effect element (stain effects only, per the agreed policy)
tier_of = {}
for c in TIER_COLS:
    for i in TIERS[c]:
        tier_of[i] = c

EFFECT_ELEMENT = {}
for aid, el in [('stain_fire', 'Fire'), ('stain_ice', 'Ice'), ('stain_lightning', 'Lightning'),
                ('stain_earth', 'Earth'), ('stain_light', 'Light'), ('stain_dark', 'Dark')]:
    for m in by_id[aid]['members']:
        EFFECT_ELEMENT[m] = el

# Gustave: T1-T3 only, no AutoDeath, generic (non-system) effects only.
SYSTEM_IDS = {
    'PerfectionOnDamage', 'DamageOnPerfectionRank', 'SimonimOvercharge',
    'InstantReplayOnOverchargedHealSkills', 'Mask_APCost-1OnCaster', 'DoubleFreeAimDamageAlmighty',
    'BaseAttackToHeavy', 'ReducedDamageOnHeavy', 'FourthHitForetell', 'SunAPDiscount',
    'ChargesBalance', 'RandomChargeTurnStart', 'StartD', 'PerfectDefenseB',
}

def is_system_effect(eid):
    if eid.startswith(('Stains_', 'Perfection_', 'Masks_', 'Stance_', 'Foretell_')):
        return True
    if eid in SYSTEM_IDS:
        return True
    if eid in CUSTOM and CUSTOM[eid].get('character'):
        return True
    # built-in stain start/echo specs carry character=Lune in WeaponEffectsBuiltins
    if eid.startswith('WepFx_Echo') or (eid.startswith('WepFx_') and 'Start' in eid):
        return True
    return False

def eligible(ch, eid):
    tier = tier_of.get(eid)
    if tier is None:
        return False
    t = int(tier[4:])
    if ch == 'Gustave':
        # Mirrors RandomizerKit.Permitted + PickEffect: T1-T3 only, no AutoDeath,
        # explicit character allowance wins, otherwise generic (non-system) only.
        if t > 3 or eid == 'AutoDeath':
            return False
        return 'Gustave' in (CHARS.get(eid) or []) or not is_system_effect(eid)
    return ch in (CHARS.get(eid) or [])

def arch_effects(ch, aid):
    return [m for m in by_id[aid]['members'] if eligible(ch, m)]

ROSTER = ['Gustave', 'Lune', 'Maelle', 'Sciel', 'Verso', 'Monoco']

# ── global archetype JSON ────────────────────────────────────────────────────
def member_rec(m):
    return {
        'id': m,
        'tier': int(tier_of[m][4:]),
        'element': EFFECT_ELEMENT.get(m),
        'characters': [c for c in ROSTER if eligible(c, m)],
        'custom': m in CUSTOM or m.startswith('WepFx_'),
        'tags': cond_tags(m),
    }


SITUATION_NAMES = {
    'hp-full': 'Full Health', 'hp-low': 'Low Health', 'no-hit': 'Untouched',
    'first-hit': 'First Strike', 'multi-hit': 'Multi-Hit',
    'counter': 'Counter', 'self-damage': 'Self-Damage', 'reckless': 'Reckless',
    'heal': 'Healing', 'no-heal': 'No Healing',
    'shield-gen': 'Shield Maker', 'shield-payoff': 'Shield Payoff', 'shield-spend': 'Shield Spender',
    'shell-gen': 'Shell Maker', 'shell-need': 'Shell Payoff', 'shield-break': 'Enemy Shield Break',
    'keep-powerless': 'Keep Powerless', 'consume-powerless': 'Consume Powerless',
    'consume-slow': 'Consume Slow', 'consume-defenseless': 'Consume Defenceless',
    'stain-scale': 'Stain Scaling', 'consume-stain': 'Stain Consumer',
    'crit-always': 'Always Crit', 'crit-alternate': 'Alternating Crit',
    'solo': 'Solo', 'ally': 'Allies', 'on-death': 'Suicide', 'death-trigger': 'On Death',
    'revive': 'Revive',
}


def situation_list():
    order = list(COND.get('conditionTags', [])) + list(COND.get('informationalTags', []))
    out = []
    for tag in order:
        effs = [m for m in ALL if tag in cond_tags(m)]
        if not effs:
            continue
        out.append({
            'id': tag,
            'name': SITUATION_NAMES.get(tag, tag),
            'kind': 'informational' if tag in COND.get('informationalTags', []) else 'condition',
            'effects': effs,
        })
    return out


def archetype_condition_tags(a):
    tags = set()
    for m in a['members']:
        tags |= set(cond_tags(m))
    return sorted(tags)

global_out = {
    'version': 1,
    'generatedBy': 'Planning/archetypes/build_archetypes.py',
    'tierDistribution': {'T1': 1, 'T2': 0.70, 'T3': 0.70, 'T4': 0.60, 'T5': 0.60},
    'rules': {
        'wildcardPerTier': {'T1': 1, 'T2': 1, 'T3': 1, 'T4': 1, 'T5': 1},
        'archetypePicksPerTier': {'T1': 0, 'T2': 1, 'T3': 2, 'T4': 3, 'T5': 4},
        'repeatArchetype': True,
        'sameEffectTwice': False,
        'luneStainElementPolicy': 'melee (BaseDamageType) only; Physical/Void keep element-neutral stainless effects',
        'gatingScope': 'element-specific stain effects only',
        'wildcardSlot': 'weakest (Extra / Level1)',
    },
    'conditionTags': COND.get('conditionTags', []),
    'informationalTags': COND.get('informationalTags', []),
    'conflictMatrix': COND.get('conflicts', []),
    'elementGate': COND.get('elementGate', {}),
    'situations': situation_list(),
    'archetypes': [{**{k: v for k, v in a.items() if k != 'members'},
                    'memberCount': len(a['members']),
                    'conditionTags': archetype_condition_tags(a),
                    'members': [member_rec(m) for m in a['members']]} for a in ARCH],
}
json.dump(global_out, open(OUT / 'weapon_effect_archetypes.json', 'w'), indent=2)
print('wrote weapon_effect_archetypes.json')

# Install into the app: embedded stock + work/ overlay (the loader prefers work/).
_install = json.dumps(global_out, indent=2)
for _p in (ROOT / 'src/E33ModEditor.Core/Loot/Data/weapon_effect_archetypes.json',
           ROOT / 'work/weapon_effect_archetypes.json'):
    _p.parent.mkdir(parents=True, exist_ok=True)
    _p.write_text(_install)
print('installed stock + work/weapon_effect_archetypes.json')

# ── per-character compatibility JSON + markdown ──────────────────────────────
compat_dir = OUT / 'compatibility'
compat_dir.mkdir(exist_ok=True)
GROUP_ORDER = ['generic', 'lune', 'maelle', 'sciel', 'verso', 'monoco']
GROUP_LABEL = {'generic': 'Generic', 'lune': 'Lune', 'maelle': 'Maelle', 'sciel': 'Sciel',
               'verso': 'Verso', 'monoco': 'Monoco'}

for ch in ROSTER:
    archs = []
    effect_map = {}
    for a in ARCH:
        effs = arch_effects(ch, a['id'])
        if not effs:
            continue
        links = [l for l in a['links'] if arch_effects(ch, l)]
        archs.append({
            'id': a['id'], 'name': a['name'], 'group': a['group'], 'element': a['element'],
            'description': a['desc'], 'count': len(effs), 'links': links,
            'effects': [{'id': m, 'tier': int(tier_of[m][4:]), 'element': EFFECT_ELEMENT.get(m)}
                        for m in effs],
        })
        for m in effs:
            rec = effect_map.setdefault(m, {'tier': int(tier_of[m][4:]),
                                            'element': EFFECT_ELEMENT.get(m), 'archetypes': []})
            rec['archetypes'].append(a['id'])
    for rec in effect_map.values():
        rec['archetypes'].sort()
    doc = {
        'character': ch,
        'elementPolicy': 'melee-only' if ch == 'Lune' else None,
        'neutralStainsAlways': ch == 'Lune',
        'archetypes': archs,
        'effects': dict(sorted(effect_map.items())),
    }
    json.dump(doc, open(compat_dir / f'{ch}.json', 'w'), indent=2)

    # markdown
    lines = [f'# {ch} — weapon effect archetypes', '']
    if ch == 'Lune':
        lines += ['Stain effects follow the weapon melee element (BaseDamageType). '
                  'Physical/Void weapons keep the element-neutral Stain Cycling family; '
                  'element-specific stain effects are excluded per element.',
                  '']
    lines += ['| Archetype | Group | Element | Effects | Links |',
              '| --- | --- | --- | ---: | --- |']
    name_of = {a['id']: a['name'] for a in ARCH}
    for a in sorted(archs, key=lambda x: (GROUP_ORDER.index(x['group']), x['name'])):
        links = ', '.join(name_of[l] for l in a['links']) or '—'
        lines.append(f"| {a['name']} | {GROUP_LABEL[a['group']]} | {a['element'] or '—'} | "
                     f"{a['count']} | {links} |")
    for a in sorted(archs, key=lambda x: (GROUP_ORDER.index(x['group']), x['name'])):
        lines += ['', f"## {a['name']} ({a['count']})", '',
                  ', '.join(f"`{e['id']}` (T{e['tier']})" for e in a['effects'])]
    open(compat_dir / f'{ch}.md', 'w').write('\n'.join(lines) + '\n')

print('wrote compatibility/*.json + *.md')

# ── effect-centric CSV ───────────────────────────────────────────────────────
import csv
with open(OUT / 'effect_archetype_map.csv', 'w', newline='') as f:
    w = csv.writer(f)
    w.writerow(['id', 'tier', 'element', 'archetypes', 'characters', 'new'])
    for eid in ALL:
        w.writerow([eid, int(tier_of[eid][4:]), EFFECT_ELEMENT.get(eid) or '',
                    ';'.join(sorted(a['id'] for a in ARCH if eid in a['members'])),
                    ';'.join(c for c in ROSTER if eligible(c, eid)),
                    'yes' if (eid in CUSTOM or eid.startswith('WepFx_')) else ''])
print('wrote effect_archetype_map.csv')

# ── images ───────────────────────────────────────────────────────────────────
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import matplotlib.patheffects as pe
import networkx as nx
import math

GROUP_COLOR = {'generic': '#4C78A8', 'lune': '#2A9D8F', 'maelle': '#BC4749',
               'sciel': '#7B2CBF', 'verso': '#D4A017', 'monoco': '#52B788'}
ELEMENT_COLOR = {'Fire': '#E76F51', 'Ice': '#48CAE4', 'Lightning': '#F4A261',
                 'Earth': '#90BE6D', 'Light': '#D9C97A', 'Dark': '#5E548E'}

def node_color(a):
    if a['element']:
        return ELEMENT_COLOR[a['element']]
    return GROUP_COLOR[a['group']]

def draw_character(ch):
    nodes = {a['id']: a for a in [dict(x) for x in ARCH]}
    present = [a for a in ARCH if arch_effects(ch, a['id'])]
    G = nx.Graph()
    for a in present:
        G.add_node(a['id'])
    for a in present:
        for l in a['links']:
            if l in G:
                G.add_edge(a['id'], l)
    n = len(G)
    if n == 0:
        return
    pos = nx.spring_layout(G, seed=11, k=4.2 / math.sqrt(n), iterations=1600)
    pos = {k: (x * 10.0, y * 10.0) for k, (x, y) in pos.items()}
    fig, ax = plt.subplots(figsize=(22, 15))
    fig.patch.set_facecolor('#ffffff')
    # edges
    for u, v in G.edges():
        au, av = nodes[u], nodes[v]
        special = au['element'] or av['element']
        color = node_color(au) if au['element'] else (node_color(av) if av['element'] else '#9aa0a6')
        ax.plot([pos[u][0], pos[v][0]], [pos[u][1], pos[v][1]], color=color,
                alpha=0.22 if not special else 0.42, lw=1.0 if not special else 1.4,
                ls='-' if not special else (0, (5, 3)), zorder=1)
    sizes = {a['id']: 120 + 62 * len(arch_effects(ch, a['id'])) for a in present}
    for a in present:
        x, y = pos[a['id']]
        s = sizes[a['id']]
        ax.scatter([x], [y], s=s, c=node_color(a), edgecolors='#ffffff', linewidths=2,
                   zorder=3, alpha=0.95)
        cnt = len(arch_effects(ch, a['id']))
        d_pt = 2 * math.sqrt(s / math.pi)
        label = f"{a['name']}\n({cnt})"
        weight = 'bold' if a['group'] != 'generic' else 'normal'
        if d_pt >= 62:
            fs = max(7.4, min(10.0, d_pt / 7.0))
            ax.text(x, y, label, fontsize=fs, ha='center', va='center', color='#1d2733',
                    weight=weight, zorder=4,
                    path_effects=[pe.withStroke(linewidth=2.6, foreground='white')])
        else:
            fs = 8.8 if cnt < 20 else 9.4
            ax.annotate(label, (x, y), textcoords='offset points',
                        xytext=(0, -(d_pt / 2 + 5)), ha='center', va='top', fontsize=fs,
                        color='#1d2733', weight=weight, zorder=4,
                        path_effects=[pe.withStroke(linewidth=3, foreground='white')])
    # size legend (bottom-left, roomy)
    legend = [(10, 0.055), (30, 0.155), (50, 0.27)]
    for nn, lx in legend:
        s = 120 + 62 * nn
        ax.scatter([lx], [0.045], s=s, c='#cfd8e0', edgecolors='#ffffff', linewidths=1.2,
                   zorder=2, transform=ax.transAxes)
        ax.text(lx, 0.045 - (math.sqrt(s / math.pi) / 72.0) / (fig.get_figheight() * 0.92),
                f'N={nn}', transform=ax.transAxes, ha='center', va='top', fontsize=9,
                color='#5a6673', path_effects=[pe.withStroke(linewidth=2.5, foreground='white')])
    ax.text(0.02, 0.125, 'circle area ∝ eligible effects', transform=ax.transAxes,
            fontsize=10, color='#5a6673', style='italic')
    sub = 'links = sibling archetypes that work well together'
    if ch == 'Lune':
        sub += '  ·  dashed links touch element-specific Stain families (melee element gates them)'
    ax.set_title(f'{ch} — Weapon Effect Archetypes', fontsize=21, weight='bold', color='#1d2733', pad=18)
    ax.text(0.5, 0.965, sub, transform=ax.transAxes, ha='center', fontsize=11, color='#5a6673')
    ax.axis('off')
    ax.margins(0.14)
    fig.tight_layout()
    fig.savefig(OUT / f'{ch}.png', dpi=140, facecolor='#ffffff')
    fig.savefig(OUT / f'{ch}.svg', facecolor='#ffffff')
    plt.close(fig)

for ch in ROSTER:
    draw_character(ch)
print('wrote character images + svg')

# ── review README ────────────────────────────────────────────────────────────
readme = ['# Weapon effect archetypes — review artifacts', '',
          f'Generated by `build_archetypes.py` from `work/weapon_effect_tiers.json`, '
          f'`work/weapon_effect_characters.json` and the custom effect specs.', '',
          f'- **{len(ARCH)} archetypes**, {len(ALLSET)} T1–T5 effects all mapped '
          f'(Do Not Use excluded: {", ".join(DNU)}).',
          '- Wildcard count per tier: 1 / 1 / 1 / 1 / 1; archetype picks: 0 / 1 / 2 / 3 / 4.',
          '- Same archetype may repeat on one weapon; the same effect never repeats.',
          '- Lune stain gating: melee (`BaseDamageType`) element only. Physical/Void keep the '
          'element-neutral Stain Cycling family.', '',
          '## Files', '',
          '- `weapon_effect_archetypes.json` — canonical archetype table + effect members',
          '- `compatibility/<Character>.json|md` — full per-character lists and links',
          '- `effect_archetype_map.csv` — one row per effect with its archetypes',
          '- `<Character>.png|svg` — the pictures below', '',
          '## Character images', '']
for ch in ROSTER:
    readme += [f'### {ch}', '', f'![{ch} archetypes]({ch}.png)', '']
open(OUT / 'README.md', 'w').write('\n'.join(readme))
print('wrote README.md')

# ── console summary ──────────────────────────────────────────────────────────
print('\nper-character archetype counts:')
for ch in ROSTER:
    row = []
    for a in ARCH:
        n = len(arch_effects(ch, a['id']))
        if n:
            row.append(f"{a['id']}:{n}")
    total = sum(int(x.split(':')[1]) for x in row)
    print(f"  {ch:8} total {total:3}  " + '  '.join(row))

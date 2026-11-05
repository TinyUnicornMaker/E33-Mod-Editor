# Lune — weapon effect archetypes

Stain effects follow the weapon melee element (BaseDamageType). Physical/Void weapons keep the element-neutral Stain Cycling family; element-specific stain effects are excluded per element.

| Archetype | Group | Element | Effects | Links |
| --- | --- | --- | ---: | --- |
| AP & Tempo | Generic | — | 17 | Base Attack, Break & Stun, Counter, Gradient, Shield & Sustain, Skill Damage, Stain Cycling, Fire Stains, Ice Stains, Light Stains, Lightning Stains, Status & Mark |
| Ailment Blades | Generic | — | 7 | Burn, Risk & Sacrifice, Shield & Sustain, Sustained Damage, Status & Mark |
| Base Attack | Generic | — | 26 | Break & Stun, Burn, Counter, Critical, Free Aim, Shield & Sustain, Sustained Damage, Dark Stains, Earth Stains, Fire Stains, Status & Mark, AP & Tempo |
| Break & Stun | Generic | — | 13 | Base Attack, Shield & Sustain, Sustained Damage, Skill Damage, Stain Cycling, Earth Stains, Status & Mark, AP & Tempo |
| Burn | Generic | — | 29 | Base Attack, Ailment Blades, Counter, Critical, Risk & Sacrifice, Shield & Sustain, Sustained Damage, Skill Damage, Stain Cycling, Fire Stains, Status & Mark |
| Counter | Generic | — | 14 | Base Attack, Burn, Critical, Gradient, Shield & Sustain, Status & Mark, AP & Tempo |
| Critical | Generic | — | 18 | Base Attack, Burn, Counter, Free Aim, Gradient, Shield & Sustain, Sustained Damage, Skill Damage, Stain Cycling, Fire Stains |
| Free Aim | Generic | — | 4 | Base Attack, Critical, Skill Damage |
| Gradient | Generic | — | 5 | Counter, Critical, Light Stains, AP & Tempo |
| Risk & Sacrifice | Generic | — | 18 | Ailment Blades, Burn, Shield & Sustain, Sustained Damage |
| Shield & Sustain | Generic | — | 20 | Base Attack, Break & Stun, Ailment Blades, Burn, Counter, Critical, Risk & Sacrifice, Stain Cycling, Earth Stains, Ice Stains, Light Stains, AP & Tempo |
| Skill Damage | Generic | — | 34 | Break & Stun, Burn, Critical, Free Aim, Sustained Damage, Stain Cycling, Dark Stains, Lightning Stains, Status & Mark, AP & Tempo |
| Status & Mark | Generic | — | 30 | Base Attack, Break & Stun, Ailment Blades, Burn, Counter, Skill Damage, Stain Cycling, Ice Stains, AP & Tempo |
| Sustained Damage | Generic | — | 35 | Base Attack, Break & Stun, Ailment Blades, Burn, Critical, Risk & Sacrifice, Skill Damage, Dark Stains, Light Stains |
| Dark Stains | Lune | Dark | 10 | Base Attack, Sustained Damage, Skill Damage, Stain Cycling |
| Earth Stains | Lune | Earth | 12 | Base Attack, Break & Stun, Shield & Sustain, Stain Cycling |
| Fire Stains | Lune | Fire | 13 | Base Attack, Burn, Critical, Stain Cycling, AP & Tempo |
| Ice Stains | Lune | Ice | 11 | Shield & Sustain, Stain Cycling, Status & Mark, AP & Tempo |
| Light Stains | Lune | Light | 12 | Gradient, Shield & Sustain, Sustained Damage, Stain Cycling, AP & Tempo |
| Lightning Stains | Lune | Lightning | 8 | Skill Damage, Stain Cycling, AP & Tempo |
| Stain Cycling | Lune | — | 14 | Break & Stun, Burn, Critical, Shield & Sustain, Skill Damage, Dark Stains, Earth Stains, Fire Stains, Ice Stains, Light Stains, Lightning Stains, Status & Mark, AP & Tempo |

## AP & Tempo (17)

`APOnCounter` (T4), `APOnCrit` (T3), `APOnShield` (T4), `APonBreak` (T4), `AttackPlayAgain` (T4), `EarthAPCost-` (T1), `FireAPCost-` (T1), `FirstStrike` (T2), `GainAPOnApplyBuffsDebuffs` (T2), `HealAPCost-` (T2), `IceAPCost-` (T1), `InflictMarkIfSkillLess4Ap` (T5), `LightningAPCost-` (T1), `More4ApDamageBonus` (T5), `Stains_APOnConsumption` (T4), `Stains_GainAPOnLightStainConsumption` (T1), `Stains_Ice+IfSelfFrozen` (T2)

## Ailment Blades (7)

`WepFx_BerserkBlade` (T2), `WepFx_BlightBlade` (T2), `WepFx_CursedBlade` (T2), `WepFx_PowerfulBlade` (T1), `WepFx_RegenBlade` (T1), `WepFx_RushBlade` (T1), `WepFx_ShellBlade` (T1)

## Base Attack (26)

`AttackBurn` (T3), `AttackBurnWhilePowerful` (T2), `AttackConsumeShieldForExtraDMG` (T2), `AttackLifesteal_Weapon` (T3), `AttackPlayAgain` (T4), `AugmentedAim_Weapon` (T2), `AugmentedAttack_Weapon` (T1), `BaseAttackToDarkStain` (T4), `FrenzyAttack` (T5), `MarkOnBaseAttack` (T2), `Stains_ApplyBurnStackPerFireStainOnAttack` (T1), `Stains_AttackEarth+` (T1), `Stains_AttackLight+` (T3), `Stains_ConsumeDarkStainsOnAttackDMG+` (T5), `WepFx_AugAtk2` (T2), `WepFx_AugAtk3` (T3), `WepFx_DefenselessConsume1` (T1), `WepFx_DefenselessConsume2` (T2), `WepFx_DefenselessConsume3` (T3), `WepFx_PowerlessConsume1` (T1), `WepFx_PowerlessConsume2` (T2), `WepFx_PowerlessConsume3` (T3), `WepFx_SlowConsume` (T1), `WepFx_SlowConsume2` (T2), `WepFx_SlowConsume3` (T3), `firstHitApplyMark` (T1)

## Break & Stun (13)

`APonBreak` (T4), `BreakDMG+` (T1), `BreakEarthBurst` (T2), `FreeAimBreaksMoreShields` (T2), `MarkBreakShields` (T4), `Stains_IncreaseBreakDMGPerEarthStain` (T2), `WepFx_Break2` (T2), `WepFx_Break3` (T3), `WepFx_StunBoost1` (T1), `WepFx_StunBoost2` (T2), `WepFx_StunBoost3` (T3), `WepFx_StunBoost4` (T4), `WepFx_StunBoost5` (T5)

## Burn (29)

`AttackBurn` (T3), `AttackBurnWhilePowerful` (T2), `BurnDamage` (T1), `BurnOnCrit` (T4), `CounterBurn` (T4), `CritAppliesBurn` (T4), `CritPerBurn` (T3), `LightDMGBurnOnCrit` (T3), `LightDamageBurn` (T4), `MarkIsBurn` (T4), `SelfBurnButDmg+` (T3), `Stains_ApplyBurnStackPerFireStainOnAttack` (T1), `Stains_IncreaseBurnDMGPerFireStain` (T3), `ThunderSkillConsumeBurn` (T4), `WepFx_AttackBurn10` (T5), `WepFx_AttackBurn5` (T4), `WepFx_BurnConsume1Skills` (T1), `WepFx_BurnConsume2Skills` (T2), `WepFx_BurnConsume3Skills` (T3), `WepFx_BurnConsume4Skills` (T4), `WepFx_BurnConsume5Skills` (T5), `WepFx_BurnDmg2` (T2), `WepFx_BurnDmg3` (T3), `WepFx_BurnDmg4` (T4), `WepFx_BurnDmg5` (T5), `WepFx_CounterBurn10` (T5), `WepFx_SelfBurn2` (T4), `WepFx_SelfBurn3` (T5), `WepFx_ThunderBurn250` (T5)

## Counter (14)

`APOnCounter` (T4), `AlwaysCritOnCounterDamage` (T3), `CounterBurn` (T4), `CounterDamagePerShield` (T3), `CounterDefenseless` (T3), `GradientAttackAndCounterDMG+` (T3), `IncreaseCounterDamage` (T2), `PowerlessOnCounter` (T3), `ShieldOnCounter` (T5), `WepFx_CounterBurn10` (T5), `WepFx_CounterDmg2` (T3), `WepFx_CounterDmg3` (T4), `WepFx_CounterShield2` (T4), `WepFx_CounterShield3` (T5)

## Critical (18)

`APOnCrit` (T3), `AlwaysCritOnCounterDamage` (T3), `BurnOnCrit` (T4), `CritAppliesBurn` (T4), `CritPerBurn` (T3), `GradientPointOnCrit` (T3), `HealOnCrit` (T3), `LightDMGBurnOnCrit` (T3), `Stains_FullCritIfFullStain` (T2), `Stains_IncreaseCritChancePerFireStain` (T1), `TriggerLightningOnCrit` (T3), `WepFx_AltCrit1` (T1), `WepFx_AltCrit2` (T2), `WepFx_AltCrit3` (T3), `WepFx_AltCrit4` (T4), `WepFx_AltCrit5` (T5), `WepFx_LightningCrit2` (T4), `WepFx_LightningCrit3` (T5)

## Free Aim (4)

`AugmentedAim_Weapon` (T2), `FreeAimBreaksMoreShields` (T2), `FreeAimDoubleShot` (T3), `Stains_RandomGeneration` (T4)

## Gradient (5)

`GradientAttackAndCounterDMG+` (T3), `GradientChargesOnParry` (T4), `GradientPointOnCrit` (T3), `MoreGradientDamageLessCharge` (T3), `Stains_GenerateStainsOnGradientAttack` (T3)

## Risk & Sacrifice (18)

`AutoDeath` (T1), `DamageBonusOnRevive` (T5), `SecondChance` (T5), `SelfBurnButDmg+` (T3), `WepFx_FirstLife1` (T1), `WepFx_FirstLife2` (T2), `WepFx_FirstLife3` (T3), `WepFx_MacabrePower1` (T1), `WepFx_MacabrePower2` (T2), `WepFx_MacabrePower3` (T3), `WepFx_SelfBerserk1` (T3), `WepFx_SelfBerserk2` (T4), `WepFx_SelfBerserk3` (T5), `WepFx_SelfBlight1` (T3), `WepFx_SelfBlight2` (T4), `WepFx_SelfBlight3` (T5), `WepFx_SelfBurn2` (T4), `WepFx_SelfBurn3` (T5)

## Shield & Sustain (20)

`APOnShield` (T4), `AttackConsumeShieldForExtraDMG` (T2), `AttackLifesteal_Weapon` (T3), `CanStealShield` (T5), `HealAPCost-` (T2), `HealOnCrit` (T3), `LightSkillsRegenHP` (T3), `SecondChance` (T5), `ShieldOnCounter` (T5), `ShieldOnShell` (T5), `WepFx_AtkShield2` (T3), `WepFx_AtkShield3` (T4), `WepFx_FrancoisShell1` (T1), `WepFx_FrancoisShell2` (T2), `WepFx_FrancoisShell3` (T3), `WepFx_HealParry1` (T2), `WepFx_HealParry2` (T3), `WepFx_HealParry3` (T4), `WepFx_RegenBlade` (T1), `WepFx_ShellBlade` (T1)

## Skill Damage (34)

`DamageBonusPerHit` (T5), `DoubleHit` (T5), `InstantReplayOnOverchargedHealSkills` (T3), `LightSkillsRegenHP` (T3), `LuneDoubleHit` (T5), `More4ApDamageBonus` (T5), `NoOverchargeSkillBuff` (T1), `SkillsDarkDMG+` (T3), `SkillsEarthDMG+` (T2), `SkillsFireDMG+` (T2), `SkillsIceDMG+` (T2), `SkillsLightDMG+` (T3), `SkillsLightToPhysical` (T3), `SkillsLightningDMG+` (T2), `SkillsPhysicalDMG+` (T2), `Stains_ConsumeFourDarkStainsDMG+OnSkills` (T5), `Stains_ElementalCyclingDMG+` (T5), `Stains_OverchargeDMG+` (T4), `ThunderSkillConsumeBurn` (T4), `WepFx_BurnConsume1Skills` (T1), `WepFx_BurnConsume2Skills` (T2), `WepFx_BurnConsume3Skills` (T3), `WepFx_BurnConsume4Skills` (T4), `WepFx_BurnConsume5Skills` (T5), `WepFx_DefenselessConsume1Skills` (T1), `WepFx_DefenselessConsume2Skills` (T2), `WepFx_DefenselessConsume3Skills` (T3), `WepFx_PowerlessConsume1Skills` (T1), `WepFx_PowerlessConsume2Skills` (T2), `WepFx_PowerlessConsume3Skills` (T3), `WepFx_SlowConsume2Skills` (T2), `WepFx_SlowConsume3Skills` (T3), `WepFx_SlowConsumeSkills` (T1), `WepFx_ThunderBurn250` (T5)

## Status & Mark (30)

`CounterDefenseless` (T3), `GainAPOnApplyBuffsDebuffs` (T2), `InflictMarkIfSkillLess4Ap` (T5), `MarkBreakShields` (T4), `MarkIsBurn` (T4), `MarkOnBaseAttack` (T2), `PowerlessMark` (T3), `PowerlessOnCounter` (T3), `PowerlessOnMarking` (T2), `SecondHitDontConsumeMark` (T4), `Stains_IceAppliesSlow` (T3), `WepFx_DefenselessConsume1` (T1), `WepFx_DefenselessConsume1Skills` (T1), `WepFx_DefenselessConsume2` (T2), `WepFx_DefenselessConsume2Skills` (T2), `WepFx_DefenselessConsume3` (T3), `WepFx_DefenselessConsume3Skills` (T3), `WepFx_PowerlessConsume1` (T1), `WepFx_PowerlessConsume1Skills` (T1), `WepFx_PowerlessConsume2` (T2), `WepFx_PowerlessConsume2Skills` (T2), `WepFx_PowerlessConsume3` (T3), `WepFx_PowerlessConsume3Skills` (T3), `WepFx_SlowConsume` (T1), `WepFx_SlowConsume2` (T2), `WepFx_SlowConsume2Skills` (T2), `WepFx_SlowConsume3` (T3), `WepFx_SlowConsume3Skills` (T3), `WepFx_SlowConsumeSkills` (T1), `firstHitApplyMark` (T1)

## Sustained Damage (35)

`BypassAffinities` (T5), `DMG+WhenNoDMGTaken` (T5), `DamageBonusOnRevive` (T5), `DamageBonusPerHit` (T5), `DoubleHit` (T5), `FrenzyAttack` (T5), `LuneDoubleHit` (T5), `More4ApDamageBonus` (T5), `SelfBurnButDmg+` (T3), `SimonimOvercharge` (T3), `Stains_IncreaseDMGPerLightStain` (T4), `WepFx_FirstLife1` (T1), `WepFx_FirstLife2` (T2), `WepFx_FirstLife3` (T3), `WepFx_FirstOffensive1` (T1), `WepFx_FirstOffensive2` (T2), `WepFx_FirstOffensive3` (T3), `WepFx_FirstOffensive4` (T4), `WepFx_FirstOffensive5` (T5), `WepFx_FullStrength1` (T1), `WepFx_FullStrength2` (T2), `WepFx_FullStrength3` (T3), `WepFx_FullStrength4` (T4), `WepFx_FullStrength5` (T5), `WepFx_Immaculate1` (T1), `WepFx_Immaculate2` (T2), `WepFx_Immaculate3` (T3), `WepFx_Immaculate4` (T4), `WepFx_Immaculate5` (T5), `WepFx_MacabrePower1` (T1), `WepFx_MacabrePower2` (T2), `WepFx_MacabrePower3` (T3), `WepFx_SoloFighter1` (T1), `WepFx_SoloFighter2` (T2), `WepFx_SoloFighter3` (T3)

## Dark Stains (10)

`BaseAttackToDarkStain` (T4), `Stains_ConsumeDarkStainsOnAttackDMG+` (T5), `Stains_ConsumeFourDarkStainsDMG+OnSkills` (T5), `Stains_GenerateDarkStainRandom` (T4), `WepFx_DarkDmgPerStain100` (T5), `WepFx_EchoDarkEarth` (T3), `WepFx_EchoDarkFire` (T3), `WepFx_EchoDarkIce` (T3), `WepFx_EchoDarkLight` (T5), `WepFx_EchoDarkLightning` (T3)

## Earth Stains (12)

`Stains_AttackEarth+` (T1), `Stains_EarthAppliesShield` (T3), `Stains_EarthStainOnTurnStart` (T2), `Stains_EarthStart` (T1), `Stains_IncreaseBreakDMGPerEarthStain` (T2), `WepFx_EarthStart2` (T2), `WepFx_EarthStart4` (T3), `WepFx_EchoEarthDark` (T4), `WepFx_EchoEarthFire` (T3), `WepFx_EchoEarthIce` (T3), `WepFx_EchoEarthLight` (T4), `WepFx_EchoEarthLightning` (T3)

## Fire Stains (13)

`Stains_ApplyBurnStackPerFireStainOnAttack` (T1), `Stains_FireStainOnTurnStart` (T2), `Stains_FireStart` (T1), `Stains_IncreaseBurnDMGPerFireStain` (T3), `Stains_IncreaseCritChancePerFireStain` (T1), `Stains_LightningOnFire` (T2), `WepFx_EchoFireDark` (T4), `WepFx_EchoFireEarth` (T3), `WepFx_EchoFireIce` (T3), `WepFx_EchoFireLight` (T4), `WepFx_EchoFireLightning` (T3), `WepFx_FireStart2` (T2), `WepFx_FireStart4` (T3)

## Ice Stains (11)

`Stains_Ice+IfSelfFrozen` (T2), `Stains_IceAppliesSlow` (T3), `Stains_IceStainOnTurnStart` (T2), `Stains_IceStart` (T1), `WepFx_EchoIceDark` (T4), `WepFx_EchoIceEarth` (T3), `WepFx_EchoIceFire` (T3), `WepFx_EchoIceLight` (T4), `WepFx_EchoIceLightning` (T3), `WepFx_IceStart2` (T2), `WepFx_IceStart4` (T3)

## Light Stains (12)

`Stains_+RandomStainOnHeal` (T2), `Stains_AttackLight+` (T3), `Stains_GainAPOnLightStainConsumption` (T1), `Stains_GenerateLightStainRandom` (T4), `Stains_GenerateStainsOnGradientAttack` (T3), `Stains_IncreaseDMGPerLightStain` (T4), `WepFx_EchoLightDark` (T5), `WepFx_EchoLightEarth` (T3), `WepFx_EchoLightFire` (T3), `WepFx_EchoLightIce` (T3), `WepFx_EchoLightLightning` (T3), `WepFx_LightOnConsume100` (T5)

## Lightning Stains (8)

`Stains_LightningStart` (T1), `WepFx_EchoLightningDark` (T4), `WepFx_EchoLightningEarth` (T3), `WepFx_EchoLightningFire` (T3), `WepFx_EchoLightningIce` (T3), `WepFx_EchoLightningLight` (T4), `WepFx_LightningStart2` (T2), `WepFx_LightningStart4` (T3)

## Stain Cycling (14)

`InstantReplayOnOverchargedHealSkills` (T3), `NoOverchargeSkillBuff` (T1), `SimonimOvercharge` (T3), `Stains_APOnConsumption` (T4), `Stains_ElementalCyclingDMG+` (T5), `Stains_FullCritIfFullStain` (T2), `Stains_GenerateLightOrDarkStainRandom` (T4), `Stains_OverchargeDMG+` (T4), `Stains_RandomGeneration` (T4), `Stains_RandomGenerationOnAttack` (T3), `Stains_TurnStart+StainsIfNoStain` (T2), `WepFx_ConsumeLightOrDarkDmg` (T5), `WepFx_TurnStart3` (T3), `WepFx_TurnStart4` (T4)

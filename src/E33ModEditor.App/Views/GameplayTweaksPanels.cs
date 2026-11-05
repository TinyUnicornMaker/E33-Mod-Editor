using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using E33ModEditor.App.ViewModels;
using E33ModEditor.Core;

namespace E33ModEditor.App.Views;

internal static class GameplayTweaksPanels
{
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#FF9C9280"));

    public static Control Build(MainViewModel vm)
    {
        var s = vm.GameplayTweaks;
        var parts = new List<Control>
        {
            new TextBlock
            {
                Text = "Gameplay Values",
                Foreground = Gold,
                FontSize = PictosEditorPanels.FontTitle,
                FontFamily = new FontFamily("Georgia, Liberation Serif, Times New Roman"),
            },
            PictosEditorPanels.Note(
                "These write into the packed mod only. Vanilla work tables stay untouched. Run the CLI/standalone build after changing anything. Shop and loot tables are not edited."),
        };

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Leveling"),
            PictosEditorPanels.Note(
                "Vanilla stops at 99 because GetMaximumLevel returns a hardcoded 99. Values above 99 patch that function, fill extra XP rows, and extend the per-level stat curves/tables. Skill points already equal (level − 1) on the skill graph, so they keep rising once the cap is lifted. Attribute points vanilla is 3 per level."),
            IntNumber("Character max level", s.LevelCap, 1, GameplayTweaksKit.ExpandedLevelCap, v =>
            {
                s.LevelCap = v;
                vm.SaveGameplayTweaks();
            }),
            IntNumber("XP per extra level (100+)", s.ExtraXpPerLevel, 1, 99_999_999, v =>
            {
                s.ExtraXpPerLevel = v;
                vm.SaveGameplayTweaks();
            }),
            IntNumber("Attribute points per level", s.AttributePointsPerLevel, 0, 20, v =>
            {
                s.AttributePointsPerLevel = v;
                vm.SaveGameplayTweaks();
            }),
            IntNumber("Skill-tree points per level", s.SkillPointsPerLevel, 1, 10, v =>
            {
                s.SkillPointsPerLevel = v;
                vm.SaveGameplayTweaks();
            }),
            IntNumber("Lumina points per level", s.LuminaPerLevel, 1, 10, v =>
            {
                s.LuminaPerLevel = v;
                vm.SaveGameplayTweaks();
            })));

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Picto mastery"),
            PictosEditorPanels.Note(
                "Vanilla pictos master after 3–4 equipped battles (LuminaUnlockStepsCount in DT_PassiveEffects). " +
                "The game ships its own debug switch for this: Sandfall.LearnAnyLuminaAfterOneBattle, read by " +
                "BP_CharacterData.SetPassiveEffectSharedProgression. On flips that CVar's registered default in " +
                "BP_jRPG_Controller_World, so every equipped picto masters on its first post-battle progress tick. " +
                "The instant switch instead rewrites the base item data asset's OnAddedToInventory hook, which runs for " +
                "every chest/drop/shop/quest pickup, and calls the picto's own GetUnlockedPassiveEffects + the first " +
                "party character's LearnPassiveEffectShared. Off is vanilla; no controller/item asset is staged. " +
                "Neither switch unlocks pictos you have not found — they only remove the mastery grind."),
            Toggle("Auto Picto Mastery (master after one battle)", s.AutoPictoMastery, v =>
            {
                s.AutoPictoMastery = v;
                vm.SaveGameplayTweaks();
            }),
            Toggle("Instant Picto Mastery (master on pickup)", s.InstantPictoMastery, v =>
            {
                s.InstantPictoMastery = v;
                vm.SaveGameplayTweaks();
            })));

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Battle proc cards"),
            PictosEditorPanels.Note(
                "Every passive/picto proc spawns a card in WBP_PassivePopupContainer and the card lives for 3 s " +
                "(WBP_PassiveEffectNotification.DisappearTime). With 20–30 equipped pictos whose turn-start effects " +
                "all fire together, the cards stack up and cover the HUD. The cap gates " +
                "AddPassiveEffectNotification: when the container's live-card map already holds this many cards, " +
                "the proc is dropped instead of spawning another card (a repeat proc of a visible card is " +
                "ignored while full, which only skips its counter tick). A dropped passive can appear again once " +
                "a card expires. 0 is vanilla (no cap, widget not staged). The cap covers every passive/buff " +
                "popup, not only pictos."),
            IntNumber("Maximum simultaneous proc cards (0 = vanilla)", s.PictoPopupCap,
                GameplayTweaksKit.VanillaPictoPopupCap, PictoPopupCapKit.MaxCap, v =>
                {
                    s.PictoPopupCap = v;
                    vm.SaveGameplayTweaks();
                })));

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Attribute point cap"),
            PictosEditorPanels.Note(
                "Vanilla caps each attribute at 99 because the scaling tables only carry Points_0..Points_99 — there is no hardcoded clamp, extra points just reuse the last row. Raising this adds Points_100.. rows to all five attribute tables (Vitality, Might/Strength, Agility, Defense, Luck) by continuing the 90→99 slope, and lifts the 99 clamp in the attributes wheel (WBP_Raw_Attributes_Display.SetAllowedMaxValue). The points you can spend are level cap × attribute points per level, so raise those too if you want a full 200 in one attribute."),
            IntNumber("Attribute point cap", s.AttributeCap,
                GameplayTweaksKit.VanillaAttributeCap, GameplayTweaksKit.MaxAttributeCap, v =>
                {
                    s.AttributeCap = v;
                    vm.SaveGameplayTweaks();
                })));

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Level-up stat growth"),
            PictosEditorPanels.Note(
                "Multiplies the cooked per-level curves (HP, Physical Attack, Speed, Critical Rate, Defense) and the matching DT_CharactersLevelScaling rows. 1.0 is vanilla. Max AP is not on this curve — it lives on the character as a separate stat (see AP below)."),
            Number("Max HP ×", s.HpMult, v => { s.HpMult = v; vm.SaveGameplayTweaks(); }),
            Number("Physical Attack ×", s.AttackMult, v => { s.AttackMult = v; vm.SaveGameplayTweaks(); }),
            Number("Speed ×", s.SpeedMult, v => { s.SpeedMult = v; vm.SaveGameplayTweaks(); }),
            Number("Critical Rate ×", s.CritMult, v => { s.CritMult = v; vm.SaveGameplayTweaks(); }),
            Number("Defense ×", s.DefenseMult, v => { s.DefenseMult = v; vm.SaveGameplayTweaks(); })));

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("AP"),
            PictosEditorPanels.Note(
                "Vanilla max AP is 9 (BP_CharacterData.MaxActionPoints and CharacterStats Max AP on every character definition). This does not come from the level-up curves."),
            IntNumber("Maximum AP", s.MaxAP, 1, 99, v => { s.MaxAP = v; vm.SaveGameplayTweaks(); })));

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Max consecutive turns"),
            PictosEditorPanels.Note(
                "How many turns in a row a character may take. Vanilla is 2: AC_jRPG_CharacterBattleStats.Force Play Again refuses another bonus turn while the RemoveItselfAtAnotherCharacterTurn buff is up, and that buff is only cleared when another character acts. 3 and above replaces that on/off test with a count of those buffs (plus a per-action guard, so a multi-hit attack counts once), capping consecutive turns at this value. 1-2 are vanilla."),
            IntNumber("Consecutive turns", s.MaxConsecutiveTurns, 1, 8, v =>
            {
                s.MaxConsecutiveTurns = v;
                vm.SaveGameplayTweaks();
            })));

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Combat caps"),
            PictosEditorPanels.Note(
                "Foretell is a Generic_ key on a mod copy of DT_Balancing_MainCharacterSkills (vanilla max Foretell 20). Twilight is 2× Generic_MaxForetell in the character graph if it uses that key — 15 base is 30 Twilight. Max shields patches SetCurrentShieldPoints on the shared battle-stats component (vanilla clamp 9). Enemies use the same function, so this is a global cap — 5 is the current pack value (less extreme than 3). Lune stain-generation chance is not a single float — skills each roll their own, so 60% of original is not wired. Max gradient charges patches BP_GradientGaugeSystem.MaxCharges (vanilla 3, default 10) and rebuilds the battle HUD widget WBP_GradientChargeGauge — which carries its own copy of the maximum and hard-codes the per-charge layout — so the shared party gauge that builds from AP spent (0.05 per AP) and powers Gradient Attacks/Counters shows, fills, colours and scales correctly for every charge up to the cap (the colour/scale ramp is resampled evenly from the vanilla stops)."),
            Number("Max Foretell", s.MaxForetell, v => { s.MaxForetell = (float)v; vm.SaveGameplayTweaks(); }),
            IntNumber("Max shields", s.MaxShields, 0, 99, v =>
            {
                s.MaxShields = v;
                vm.SaveGameplayTweaks();
            }),
            IntNumber("Max gradient charges", s.MaxGradientCharges, 1, 99, v =>
            {
                s.MaxGradientCharges = v;
                vm.SaveGameplayTweaks();
            })));

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Dodge & parry timing"),
            PictosEditorPanels.Note(
                "Vanilla windows are 0.22 s dodge / 0.15 s parry. The tight preset is 0.165 s / 0.1125 s (165 ms / 113 ms). " +
                "Off ships the vanilla difficulty rows and removes the table from the pack. " +
                "Disable tutorials forces the tutorial system off and repoints the narrative encounters to their no-tutorial enemy variants, so the scripted lessons/fights are skipped entirely. " +
                "Turning the timing toggle on from vanilla fills in the tight preset; the numbers are remembered while the toggle is off."),
            Toggle("Tighten dodge/parry windows", s.TightenDefenseWindows, v =>
            {
                s.TightenDefenseWindows = v;
                if (v && Math.Abs(s.DodgeWindow - GameplayTweaksKit.VanillaDodgeWindow) < 0.0001
                       && Math.Abs(s.ParryWindow - GameplayTweaksKit.VanillaParryWindow) < 0.0001)
                {
                    s.DodgeWindow = 0.165;
                    s.ParryWindow = 0.1125;
                }
                vm.SaveGameplayTweaks();
            }),
            TimingNumber("Dodge window (seconds, vanilla 0.22)", s.DodgeWindow, v =>
            {
                s.DodgeWindow = v;
                vm.SaveGameplayTweaks();
            }),
            TimingNumber("Parry window (seconds, vanilla 0.15)", s.ParryWindow, v =>
            {
                s.ParryWindow = v;
                vm.SaveGameplayTweaks();
            }),
            Toggle("Disable tutorials", s.DisableTutorials, v =>
            {
                s.DisableTutorials = v;
                vm.SaveGameplayTweaks();
            })));

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Critical damage"),
            PictosEditorPanels.Note(
                "Vanilla crits deal a flat ×1.5: BP_BattleDamages.ApplyCriticalHitModifiers adds a damage modifier of 1.5 × the attack's crit-damage multiplier. Both controls below edit that real path (a kismet edit to BP_BattleDamages — the old Generic_CriticalStrikeDMGIncrease table row is read by nothing, so it was dropped).\n" +
                "Crit damage addend: the baked base becomes (1 + addend). Vanilla 0.5 → ×1.5; 1.0 → ×2.0; 1.5 → ×2.5.\n" +
                "Crit-rate scaling: multiplies the base by max(1, critRate/100), so 100% crit rate is unchanged, 200% doubles it, and a crit never drops below the addend base. The rate is read through the same GetCriticalRollToBeat() the crit roll uses, so flat crit-chance pictos/passives count.\n" +
                "Combined: crit ×= (1 + addend) × max(1, critRate/100) × per-attack crit-damage multiplier."),
            Number("Crit damage addend (0.5 vanilla → base ×1.5)", s.CritDamageIncrease, v =>
            {
                s.CritDamageIncrease = (float)v;
                vm.SaveGameplayTweaks();
            }),
            Toggle("Scale crit damage by crit rate (× max(1, critRate/100))", s.CritRateDamageScaling, v =>
            {
                s.CritRateDamageScaling = v;
                vm.SaveGameplayTweaks();
            })));

        // Single burn graph: X = burn stacks 0–1000, Y = damage, Weapon Strength = Atk
        var weaponStrength = BurnCurve.DefaultWeaponStrength;
        var burnChart = new CurveChart(Color.Parse("#FFE24B3A"))
        {
            AxisXTitle = "Burn stacks",
            AxisYTitle = "Damage per tick",
            VanillaName = "vanilla",
            PreviewName = "preview",
            IntegerValues = false,
            XMin = 0,
            XMax = BurnCurve.MaxStacks,
        };
        var burnReadout = new TextBlock { Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
        void UpdateBurn()
        {
            var vStacks = BurnCurve.VanillaStack(weaponStrength, BurnCurve.PreviewDefense, false);
            var pStacks = BurnCurve.GenerateStacks(s.BurnMultiplier, s.BurnPlayerScale, s.BurnDuration, s.BurnTailOff, weaponStrength, BurnCurve.PreviewDefense, false, false);
            burnChart.SetCompare(vStacks, pStacks);
            // sample points for readout: 10, 100, 500, 1000
            double Sample(double[] arr, int n) => n >= 0 && n < arr.Length ? arr[n] : 0;
            var v10 = Sample(vStacks, 10); var p10 = Sample(pStacks, 10);
            var v100 = Sample(vStacks, 100); var p100 = Sample(pStacks, 100);
            var v500 = Sample(vStacks, 500); var p500 = Sample(pStacks, 500);
            var v1000 = Sample(vStacks, 1000); var p1000 = Sample(pStacks, 1000);
            var v5 = Sample(vStacks, 5); var p5 = Sample(pStacks, 5);
            var vTot = BurnCurve.GenerateTotalStacks(GameplayTweaksKit.VanillaBurnMultiplier, GameplayTweaksKit.VanillaBurnPlayerScale, GameplayTweaksKit.VanillaBurnDuration, GameplayTweaksKit.VanillaBurnTailOff, weaponStrength, BurnCurve.PreviewDefense)[5];
            var pTot = BurnCurve.GenerateTotalStacks(s.BurnMultiplier, s.BurnPlayerScale, s.BurnDuration, s.BurnTailOff, weaponStrength, BurnCurve.PreviewDefense)[5];
            burnReadout.Text = $"Atk {weaponStrength} Def {BurnCurve.PreviewDefense:0} (enemy) – per-tick: 10× vanilla {v10:0}→ preview {p10:0}  |  100× {v100:0}→ {p100:0}  |  500× {v500:0}→ {p500:0}  |  1000× {v1000:0}→ {p1000:0}\n" +
                               $"5 stacks total over duration: vanilla {vTot:0} ({GameplayTweaksKit.VanillaBurnDuration}×) → preview {pTot:0} ({s.BurnDuration}×)    k={s.BurnTailOff:0.##} (vanilla 2.0): base=Atk²/(k·Def+Atk); k=0 ⇒ no defence mitigation.  5× per-tick vanilla {v5:0}→ preview {p5:0}.";
        }
        var logToggle = new CheckBox
        {
            Content = "Logarithmic damage scale",
            IsChecked = burnChart.LogScaleY,
            FontSize = PictosEditorPanels.FontInput,
            Foreground = Cream,
            Margin = new Thickness(0, 4, 0, 8),
        };
        logToggle.IsCheckedChanged += (_, _) =>
        {
            burnChart.LogScaleY = logToggle.IsChecked == true;
            burnChart.InvalidateVisual();
        };
        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Burn"),
            PictosEditorPanels.Note(
                "Burn tick: BP_BattleBuff_BurnAggregator.ComputeBurnDamageTotal.\n" +
                "base = Atk²/(k·Def+Atk)  (k = BurnTailOff, vanilla 2.0 – two DoubleConst 2.0)  larger k ⇒ stronger defence tail-off.\n" +
                "Total per-tick for N stacks = base×N×teamScale(0.2 player /1.0 enemy) ×0.5 agg × stacks. BurnMultiplier scales agg 0.5→0.5×mult. Burn duration patches TurnDuration (vanilla 3). Player scale patches 0.2 branch.\n" +
                "Graph X = burn stacks 0–1000, Y = damage per tick (enemy target, Def 300). Weapon Strength controls Atk."),
            BurnNumber("Burn damage × (global, 1.0 vanilla, 0–10)", s.BurnMultiplier, 0, 10, v => { s.BurnMultiplier = v; vm.SaveGameplayTweaks(); UpdateBurn(); }),
            BurnNumber("Player burn taken × (0.2 vanilla, 0.01 default, 0–5)", s.BurnPlayerScale, 0, 5, v => { s.BurnPlayerScale = v; vm.SaveGameplayTweaks(); UpdateBurn(); }),
            IntNumber("Burn duration (turns, 3 vanilla)", s.BurnDuration, 1, 20, v => { s.BurnDuration = v; vm.SaveGameplayTweaks(); UpdateBurn(); }),
            BurnNumber("Burn defence tail-off k (2.0 vanilla, 0–10, 0=no mitigation)", s.BurnTailOff, 0, 10, v => { s.BurnTailOff = v; vm.SaveGameplayTweaks(); UpdateBurn(); }),
            IntNumber("Weapon Strength (Attack)", weaponStrength, BurnCurve.MinWeaponStrength, BurnCurve.MaxWeaponStrength, v => { weaponStrength = v; UpdateBurn(); }),
            logToggle,
            PictosEditorPanels.Note("Graph orange dashed = vanilla, red solid = preview. X = burn stacks 0–1000. Y = damage per tick. Damage ∝ stacks (linear in stacks) but base grows with Weapon Strength. Toggle log scale to inspect extreme values (log₁₀(damage+1))."),
            burnChart,
            burnReadout));
        UpdateBurn();

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Mask Keeper (Visages)"),
            PictosEditorPanels.Note(
                "BP_EnemyBattle_MaskKeeper_VisagesPhase2 works out which of the three masks are still alive from the persistent flags NID_JoyMaskDefeated / NID_AngerMaskDefeated / NID_SadnessMaskDefeated, then gates the mask turn-actions, the bound mask actors (Bossfight.Visages.MaskActors.*) and Skill_SummonMasks on them — so every sub-boss you beat deletes its turn from the boss's rotation. On: those three reads are re-pointed at a flag of ours that no save can ever set, so the full moveset survives clearing Joy, Sadness and Anger. You still fight all three and keep their drops, and the island's progression still gates on the real flags. Off is vanilla: each beaten mask removes its turn. The masks help the boss (Anger grants extra turns, Joy heals it, Sadness inflicts Exhaust), so On is the harder fight."),
            Toggle("Keep the full mask moveset after the sub-bosses", s.MaskKeeperFullMoveset, v =>
            {
                s.MaskKeeperFullMoveset = v;
                vm.SaveGameplayTweaks();
            })));

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Sirène (Axon heart)"),
            PictosEditorPanels.Note(
                "BP_EnemyBattle_SireneHeart returns her Tisseur-synergy move Skill_TisseurHeal only while GetShouldTisseurHelp() is true — that is !HasBattledEnemy(\"LD_Battle_Tisseur\") AND ShouldUseTisseurHelpSkill. Once Tisseur is beaten the HasBattledEnemy check is permanently true, so the move is dropped from her rotation. On: the check is re-pointed at a flag no save can ever set (NID_TisseurNeverDefeated), so HasBattledEnemy always misses and the full moveset survives clearing Tisseur. Tisseur's own defeat record and the island's progression are untouched. Off is vanilla."),
            Toggle("Keep Sirène's full moveset after Tisseur", s.SireneFullMoveset, v =>
            {
                s.SireneFullMoveset = v;
                vm.SaveGameplayTweaks();
            })));

        parts.Add(PictosEditorPanels.Card(
            PictosEditorPanels.Head("Enemy & lumina balance"),
            PictosEditorPanels.Note(
                "Buffed regular enemies raises ordinary (non-boss) enemies per tier, on top of the vanilla or randomized EnemyScaling. " +
                "HP +10/30/50/200/500% and Speed +10/20/30/40/50% from T1 to T5; Physical and Magical Attack +5/10/20/50/100%. " +
                "Bosses, chromatic alphas, superbosses, merchants and cut content are untouched. " +
                "Adjust regular lumina scales every chest, enemy-drop and shop source of Colour of Lumina; dialogue rewards " +
                "(Blanche's pacifist grant) are excluded. Pure-lumina chests swap half of their one-point drops for a tier-appropriate " +
                "Chroma pack when reducing, so no chest is left empty. Both switches are off by default."),
            Toggle("Buff regular enemies by tier", s.BuffRegularEnemies, v =>
            {
                s.BuffRegularEnemies = v;
                vm.SaveGameplayTweaks();
            }),
            Toggle("Adjust regular lumina drops", s.ReduceLuminaDrops, v =>
            {
                s.ReduceLuminaDrops = v;
                vm.SaveGameplayTweaks();
            }),
            BurnNumber("Lumina multiplier (0.5 = half, 2 = double)", s.LuminaDropMultiplier, 0.1, 5.0, v =>
            {
                s.LuminaDropMultiplier = v;
                vm.SaveGameplayTweaks();
            })));

        return PictosEditorPanels.Page(parts.ToArray());
    }

    private static Control BurnNumber(string label, double value, double min, double max, Action<double> set) =>
        Labeled(label, MakeBox(value.ToString("0.###", CultureInfo.InvariantCulture), t =>
        {
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= min && v <= max)
                set(v);
        }));

    private static Control Toggle(string label, bool on, Action<bool> set)
    {
        var box = new CheckBox
        {
            Content = label,
            IsChecked = on,
            FontSize = PictosEditorPanels.FontInput,
            Foreground = Cream,
            Margin = new Thickness(0, 4, 0, 8),
        };
        box.IsCheckedChanged += (_, _) => set(box.IsChecked == true);
        return box;
    }

    private static Control Number(string label, double value, Action<double> set) =>
        Labeled(label, MakeBox(value.ToString("0.###", CultureInfo.InvariantCulture), t =>
        {
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0 && v <= 100)
                set(v);
        }));

    /// <summary>Seconds with four decimals so 0.1125 survives the round-trip
    /// (the generic Number helper's 3-decimal format turns it into 0.113).</summary>
    private static Control TimingNumber(string label, double value, Action<double> set) =>
        Labeled(label, MakeBox(value.ToString("0.####", CultureInfo.InvariantCulture), t =>
        {
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0 && v <= 10)
                set(v);
        }));

    private static Control IntNumber(string label, int value, int min, int max, Action<int> set) =>
        Labeled(label, MakeBox(value.ToString(CultureInfo.InvariantCulture), t =>
        {
            if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                set(Math.Clamp(v, min, max));
        }));

    private static TextBox MakeBox(string text, Action<string> onChange)
    {
        var box = new TextBox
        {
            Text = text,
            FontSize = PictosEditorPanels.FontInput,
            Foreground = Cream,
            Width = 160,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        box.LostFocus += (_, _) => onChange(box.Text ?? "");
        // Commit on Enter too: a click that does not move focus would otherwise
        // leave the typed value uncommitted, and the next Apply would pack the
        // old one (how a typed attribute cap silently failed to take effect).
        box.KeyDown += (_, e) =>
        {
            if (e.Key != Avalonia.Input.Key.Enter) return;
            onChange(box.Text ?? "");
            e.Handled = true;
        };
        return box;
    }

    private static Control Labeled(string label, Control input)
    {
        var line = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse($"{PictosEditorPanels.LabelCol + 60},*"),
            Margin = new Thickness(0, 4),
        };
        var lab = new TextBlock
        {
            Text = label,
            Foreground = Cream,
            FontSize = PictosEditorPanels.FontLabel,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(lab, 0);
        Grid.SetColumn(input, 1);
        line.Children.Add(lab);
        line.Children.Add(input);
        return line;
    }
}

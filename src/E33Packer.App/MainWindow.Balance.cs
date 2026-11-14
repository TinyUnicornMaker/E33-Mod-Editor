using Avalonia;
using Avalonia.Controls;
using E33ModEditor.Core;
using E33ModEditor.Core.Loot;
using static E33Packer.App.AppLocalization;

namespace E33Packer.App;

public sealed partial class MainWindow
{
    private Control BuildBalanceTab()
    {
        return Stack(
            Note(T("Adjust progression, attributes, boss movesets and parry timing after the seed rolls.")),
            Card(BuildProgressionCard()),
            Card(BuildAttributesCard()),
            Card(BuildNevronSectionCard()),
            Card(BuildBossMovesetCard()),
            Card(BuildParryTimingsCard()),
            Card(BuildWeaponProgressionCard()));
    }

    private Control BuildProgressionCard()
    {
        var b = _profile.Balance;
        return Stack(
            Head(T("Progression")),
            Note(T("Level and stat growth.")),
            IntRow(T("Character level cap"), b.LevelCap, 99, 999, v => b.LevelCap = v,
                tooltip: T("Maximum character level.")),
            IntRow(T("Attribute point cap"), b.AttributeCap, 99, GameplayTweaksKit.MaxAttributeCap,
                v => b.AttributeCap = v, tooltip: T("Maximum points in a single attribute.")),
            IntRow(T("XP per extra level (100+)"), b.ExtraXpPerLevel, 1, 99_999_999,
                v => b.ExtraXpPerLevel = v, tooltip: T("XP needed for each level above 99.")),
            IntRow(T("Attribute points per level"), b.AttributePointsPerLevel, 0, 20,
                v => b.AttributePointsPerLevel = v, tooltip: T("Attribute points gained per level.")),
            IntRow(T("Skill-tree points per level"), b.SkillPointsPerLevel, 1, 10,
                v => b.SkillPointsPerLevel = v, tooltip: T("Skill points gained per level.")),
            IntRow(T("Lumina points per level"), b.LuminaPerLevel, 1, 10, v => b.LuminaPerLevel = v,
                tooltip: T("Lumina gained per level.")));
    }

    private Control BuildAttributesCard()
    {
        var b = _profile.Balance;
        return Stack(
            Head(T("Attributes")),
            Note(T("Multipliers on the character's attribute curves. 1.0 is intended.")),
            DoubleRow(T("Max HP ×"), b.HpMult, 0.1, 10, v => b.HpMult = v),
            DoubleRow(T("Physical Attack ×"), b.AttackMult, 0.1, 10, v => b.AttackMult = v),
            DoubleRow(T("Speed ×"), b.SpeedMult, 0.1, 10, v => b.SpeedMult = v),
            DoubleRow(T("Critical Rate ×"), b.CritMult, 0.1, 10, v => b.CritMult = v),
            DoubleRow(T("Defense ×"), b.DefenseMult, 0.1, 10, v => b.DefenseMult = v));
    }

    private Control BuildBossMovesetCard()
    {
        var q = _profile.QoL;
        return Stack(
            Head(T("Boss movesets")),
            Toggle(T("Mask Keeper keeps the full moveset"), q.MaskKeeperFullMoveset,
                v => q.MaskKeeperFullMoveset = v,
                T("The Mask Keeper keeps its mask moves after the sub-bosses are defeated.")),
            Toggle(T("Sirène keeps her full moveset"), q.SireneFullMoveset,
                v => q.SireneFullMoveset = v,
                T("Sirène keeps her Tisseur-synergy moves after Tisseur is defeated.")));
    }

    private Control BuildParryTimingsCard()
    {
        var b = _profile.Balance;
        var expedition = new Button { Content = T("Expedition (0.165 / 0.1125)") };
        ToolTip.SetTip(expedition, T("Tighter dodge and parry windows."));
        expedition.Click += (_, _) =>
        {
            b.TightenDefenseWindows = true;
            b.DodgeWindow = 0.165;
            b.ParryWindow = 0.1125;
            MarkCustom();
            RebuildTabs();
        };
        var normal = new Button { Content = T("Normal (0.22 / 0.15)") };
        ToolTip.SetTip(normal, T("Vanilla dodge and parry windows."));
        normal.Click += (_, _) =>
        {
            b.TightenDefenseWindows = false;
            b.DodgeWindow = 0.22;
            b.ParryWindow = 0.15;
            MarkCustom();
            RebuildTabs();
        };
        var presetRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 2),
        };
        presetRow.Children.Add(expedition);
        presetRow.Children.Add(normal);

        var dodgeRow = DoubleRow(T("Dodge window (seconds)"), b.DodgeWindow, 0.01, 1.0,
            v => b.DodgeWindow = v, T("vanilla 0.22"), T("How long a dodge input is accepted."));
        var parryRow = DoubleRow(T("Parry window (seconds)"), b.ParryWindow, 0.01, 1.0,
            v => b.ParryWindow = v, T("vanilla 0.15"), T("How long a parry input is accepted."));
        var tunedToggle = Toggle(T("Use tuned windows"), b.TightenDefenseWindows,
            v => b.TightenDefenseWindows = v,
            T("Shorter windows make dodging and parrying harder."));
        EnableWith(tunedToggle, dodgeRow, parryRow);

        return Stack(
            Head(T("Parry timings")),
            Note(T("Dodge and parry windows.")),
            tunedToggle,
            presetRow,
            dodgeRow,
            parryRow);
    }

    private Control BuildWeaponProgressionCard()
    {
        var b = _profile.Balance;
        var maxLevelRow = IntRow(T("Max weapon drop level"), b.MaxDropLevel, 33, 333,
            v => b.MaxDropLevel = v,
            tooltip: T("Weapon drops scale from 1 to this level. Pictos stay 1–33."));
        var scaleToggle = Toggle(T("Scale weapon drop levels"), b.WeaponDropLevelEnabled,
            v => b.WeaponDropLevelEnabled = v,
            T("Extends weapon drops beyond the vanilla level range."));
        EnableWith(scaleToggle, maxLevelRow);

        return Stack(
            Head(T("Weapon progression")),
            scaleToggle,
            maxLevelRow);
    }

    private Control BuildNevronSectionCard()
    {
        var r = _profile.Randomizer;
        var b = _profile.Balance;

        var statsToggle = Toggle(T("Nevron stats"), r.MonsterStats,
            v => r.MonsterStats = v,
            T("Rolls Nevron HP, attack, speed and defense."));

        var regularReset = new Button { Content = T("Reset regular boosts") };
        ToolTip.SetTip(regularReset, T("Restore the default regular Nevron boosts."));
        regularReset.Click += (_, _) =>
        {
            b.RegularEnemyHpBonus = (double[])RegularEnemyBuffKit.DefaultHpBonus.Clone();
            b.RegularEnemySpeedBonus = (double[])RegularEnemyBuffKit.DefaultSpeedBonus.Clone();
            b.RegularEnemyAttackBonus = (double[])RegularEnemyBuffKit.DefaultAttackBonus.Clone();
            MarkCustom();
            RebuildTabs();
        };

        var hpRow = PercentArrayRow(T("HP boost T1–T5"), b.RegularEnemyHpBonus,
            (i, v) => b.RegularEnemyHpBonus[i] = v,
            T("Percentage boost to regular Nevron HP. 0% = no boost."));
        var speedRow = PercentArrayRow(T("Speed boost T1–T5"), b.RegularEnemySpeedBonus,
            (i, v) => b.RegularEnemySpeedBonus[i] = v,
            T("Percentage boost to regular Nevron speed. 0% = no boost."));
        var attackRow = PercentArrayRow(T("Attack boost T1–T5"), b.RegularEnemyAttackBonus,
            (i, v) => b.RegularEnemyAttackBonus[i] = v,
            T("Percentage boost to regular Nevron attack. 0% = no boost."));
        var boostToggle = Toggle(T("Apply regular Nevron percentage boosts"),
            b.BuffRegularEnemies, v => b.BuffRegularEnemies = v,
            T("Positive percentages strengthen regular Nevrons; negative percentages weaken them. Bosses and alphas are not affected."));

        var bossHp = r.BossHp.Select(v => v - 1).ToArray();
        var bossHpRow = PercentArrayRow(T("Boss HP boost T1–T5"), bossHp,
            (i, v) =>
            {
                bossHp[i] = v;
                r.BossHp[i] = 1 + v;
            },
            T("Percentage boost to boss HP for T1–T3. 0% = vanilla."),
            editable: 3, lockedText: T("curve"),
            lockedTooltip: T("T4–T5 boss HP is controlled by the hidden endgame curve."));
        var bossSpeedRow = DoubleRow(T("Boss speed boost"), (r.BossSpeed - 1) * 100,
            -99, 10000, v => r.BossSpeed = 1 + v / 100, null,
            T("Percentage boost to boss speed. 0% = vanilla."), percent: true);
        var extremeDefenseToggle = Toggle(T("Extreme T4–T5 boss defense"),
            r.ExtremeBossDefense, v => r.ExtremeBossDefense = v,
            T("Off uses the regular T4–T5 boss defense curve. On ramps defense from 1× at the T4 floor to 30× the regular curve at the top of T5."));

        var bossReset = new Button { Content = T("Reset boss boosts") };
        ToolTip.SetTip(bossReset, T("Restore the default boss boosts."));
        bossReset.Click += (_, _) =>
        {
            r.BossHp = (double[])RandomizerKit.DefaultBossHp.Clone();
            r.BossSpeed = RandomizerKit.DefaultBossSpeed;
            r.ExtremeBossDefense = false;
            MarkCustom();
            RebuildTabs();
        };

        void SyncEnabled()
        {
            var stats = statsToggle.IsChecked == true;
            var regularBoosts = stats && boostToggle.IsChecked == true;
            boostToggle.IsEnabled = stats;
            hpRow.IsEnabled = regularBoosts;
            speedRow.IsEnabled = regularBoosts;
            attackRow.IsEnabled = regularBoosts;
            regularReset.IsEnabled = regularBoosts;
            bossHpRow.IsEnabled = stats;
            bossSpeedRow.IsEnabled = stats;
            extremeDefenseToggle.IsEnabled = stats;
            bossReset.IsEnabled = stats;
        }
        statsToggle.IsCheckedChanged += (_, _) =>
        {
            // Turning off the seed's Nevron stat rolls also opts out of the
            // stacked regular-Nevron percentage boosts.
            if (statsToggle.IsChecked != true && boostToggle.IsChecked == true)
                boostToggle.IsChecked = false;
            SyncEnabled();
        };
        boostToggle.IsCheckedChanged += (_, _) => SyncEnabled();
        SyncEnabled();

        return Stack(
            Head(T("Nevrons")),
            Note(T("Choose whether Nevron stats are rerolled by the seed.")),
            statsToggle,
            Note(T("Turned off: the seed leaves Nevron stats at their rolled band defaults.")),
            SubHead(T("Regular Nevrons")),
            Note(T("Percentage boosts applied after the seed roll. 0% = no boost.")),
            boostToggle,
            TierHeader(),
            hpRow,
            speedRow,
            attackRow,
            regularReset,
            SubHead(T("Bosses")),
            Note(T("Percentage boosts over vanilla boss values. Regular Nevron boosts do not affect bosses.")),
            TierHeader(),
            bossHpRow,
            Note(T("T4–T5 boss HP uses the hidden endgame curve and has no exposed control.")),
            bossSpeedRow,
            extremeDefenseToggle,
            bossReset);
    }

    private static bool IsNear(double a, double b) => Math.Abs(a - b) < 0.0001;
}

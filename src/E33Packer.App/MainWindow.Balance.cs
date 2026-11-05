using Avalonia;
using Avalonia.Controls;
using E33ModEditor.Core;
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
                T("Sirène keeps her Tisseur-synergy moves after Tisseur is defeated.")),
            Note(T("Five-member party: always on.")));
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

    private static bool IsNear(double a, double b) => Math.Abs(a - b) < 0.0001;
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using E33ModEditor.Core;
using static E33Packer.App.AppLocalization;

namespace E33Packer.App;

public sealed partial class MainWindow
{
    private Control BuildQoLTab()
    {
        return Stack(
            Note(T("Convenience changes. Core balance is unchanged.")),
            Card(BuildPictoQolCard()),
            Card(BuildBattleProcCard()),
            Card(BuildWeaponQolCard()),
            Card(BuildBuildQolCard()));
    }

    private Control BuildPictoQolCard()
    {
        var q = _profile.QoL;
        return Stack(
            Head(T("Picto mastery")),
            Toggle(T("Instant Picto mastery"), q.PictoMastery == "instant",
                v => q.PictoMastery = v ? "instant" : "none",
                T("A found picto is mastered immediately. It does not grant pictos you have not found.")));
    }

    private Control BuildBattleProcCard()
    {
        var q = _profile.QoL;
        return Stack(
            Head(T("Battle proc cards")),
            IntRow(T("Max battle proc cards (0 = vanilla)"), q.PictoPopupCap,
                PictoPopupCapKit.VanillaCap, PictoPopupCapKit.MaxCap, v => q.PictoPopupCap = v,
                tooltip: T("Limits how many picto/passive cards can stack on screen during battle. 0 leaves it vanilla.")));
    }

    private Control BuildWeaponQolCard()
    {
        var q = _profile.QoL;
        var levelsRow = IntRow(T("Levels per hold"), q.BulkUpgradeLevels, 1,
            WeaponBulkUpgradeKit.BulkLevels,
            v => q.BulkUpgradeLevels = v, tooltip: T("Maximum levels added by one hold."));
        var bulkToggle = Toggle(T("Bulk upgrade"), q.BulkUpgradeEnabled,
            v => q.BulkUpgradeEnabled = v,
            T("Hold to upgrade multiple levels at once, up to the current catalyst tier limit."));
        EnableWith(bulkToggle, levelsRow);

        return Stack(
            Head(T("Weapon upgrades")),
            bulkToggle,
            levelsRow);
    }

    private Control BuildBuildQolCard()
    {
        var autoOutput = new CheckBox
        {
            Content = T("Open the install folder after a build"),
            IsChecked = _app.AutoOpenOutput,
            Foreground = Cream,
            FontSize = 13,
        };
        ToolTip.SetTip(autoOutput, T("Open the game's ~mods folder when the build finishes."));
        autoOutput.IsCheckedChanged += (_, _) =>
        {
            _app.AutoOpenOutput = autoOutput.IsChecked == true;
            _app.Save();
        };
        var autoSpoiler = new CheckBox
        {
            Content = T("Open the spoiler folder after a build"),
            IsChecked = _app.AutoOpenSpoiler,
            Foreground = Cream,
            FontSize = 13,
        };
        ToolTip.SetTip(autoSpoiler,
            T("Open the folder that contains the generated spoiler page."));
        autoSpoiler.IsCheckedChanged += (_, _) =>
        {
            _app.AutoOpenSpoiler = autoSpoiler.IsChecked == true;
            _app.Save();
        };

        return Stack(
            Head(T("Build convenience")),
            autoOutput,
            autoSpoiler);
    }
}

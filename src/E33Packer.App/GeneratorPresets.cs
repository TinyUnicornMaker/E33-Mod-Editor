using E33ModEditor.Core;
using E33ModEditor.Core.Loot;

namespace E33Packer.App;

public static class GeneratorPresets
{
    public const string Expedition = "Expedition Infinity";
    public const string Easy = "Easy";
    public const string Brutal = "Brutal";
    public const string Custom = "Custom";

    public static readonly string[] Names =
    {
        Expedition, Easy, Brutal, Custom,
    };

    public static GeneratorProfile Create(string name, int? seed = null)
    {
        var profile = new GeneratorProfile
        {
            Name = Normalize(name),
            Seed = seed ?? NewSeed(),
            Format = GeneratorProfile.CurrentFormat,
        };
        profile.Randomizer.Seed = profile.Seed;
        // V1.3 ships stable rosters: no battle-group growth and no per-battle
        // re-rolls. The feature stays available on the Randomizer tab but is off
        // by default, and the tuning uses the documented defaults rather than
        // the Flying Waters test filter.
        profile.Randomizer.BattleGroups = false;
        profile.Randomizer.RandomBattles = false;
        profile.Randomizer.BattleVariants = 3;
        profile.Randomizer.MaxEnemiesPerBattle = 5;
        profile.Randomizer.ExtraEnemiesChance = 50;
        profile.Randomizer.BattleGroupsAreaPrefix = "";
        profile.Randomizer.ShuffleBattleRosters = true;
        // V1.3 keeps the steepened regular-enemy buff on by default.
        profile.Balance.BuffRegularEnemies = true;
        profile.Balance.RegularEnemyHpBonus =
            (double[])RegularEnemyBuffKit.DefaultHpBonus.Clone();
        profile.Balance.RegularEnemySpeedBonus =
            (double[])RegularEnemyBuffKit.DefaultSpeedBonus.Clone();
        profile.Balance.RegularEnemyAttackBonus =
            (double[])RegularEnemyBuffKit.DefaultAttackBonus.Clone();
        // The standard 1.3 seeds use strict Light stains. Easy Mode can enable
        // the wildcard explicitly from Quality of Life.
        profile.QoL.LightStainWildcard = false;
        // Instant Picto Mastery is optional and off in the standard profile.
        profile.QoL.PictoMastery = "none";
        switch (profile.Name)
        {
            case Easy:
                profile.Balance.Permadeath = false;
                profile.Balance.TightenDefenseWindows = false;
                profile.Balance.DodgeWindow = 0.22;
                profile.Balance.ParryWindow = 0.15;
                profile.Balance.ReduceLuminaDrops = false;
                profile.Balance.LuminaDropMultiplier = 1.0;
                // 25% weaker enemy stat bonuses than the Expedition preset.
                profile.Balance.RegularEnemyHpBonus =
                    Scale(RegularEnemyBuffKit.DefaultHpBonus, 0.75);
                profile.Balance.RegularEnemySpeedBonus =
                    Scale(RegularEnemyBuffKit.DefaultSpeedBonus, 0.75);
                profile.Balance.RegularEnemyAttackBonus =
                    Scale(RegularEnemyBuffKit.DefaultAttackBonus, 0.75);
                profile.QoL.PictoMastery = "instant";
                break;

            case Brutal:
                // Permadeath is not a completed feature; keep it off.
                profile.Balance.Permadeath = false;
                profile.Balance.TightenDefenseWindows = true;
                profile.Balance.BuffRegularEnemies = true;
                profile.Balance.ReduceLuminaDrops = true;
                profile.Balance.LuminaDropMultiplier = 0.5;
                // Double the HP bonus; make speed/attack bonuses 50% stronger.
                profile.Balance.RegularEnemyHpBonus =
                    Scale(RegularEnemyBuffKit.DefaultHpBonus, 2.0);
                profile.Balance.RegularEnemySpeedBonus =
                    Scale(RegularEnemyBuffKit.DefaultSpeedBonus, 1.5);
                profile.Balance.RegularEnemyAttackBonus =
                    Scale(RegularEnemyBuffKit.DefaultAttackBonus, 1.5);
                break;

            case Custom:
                break;

            default:
                profile.Name = Expedition;
                break;
        }
        return profile;
    }

    private static double[] Scale(double[] values, double factor) =>
        values.Select(v => v * factor).ToArray();

    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Expedition;
        if (name.Trim().Equals("Gentler", StringComparison.OrdinalIgnoreCase))
            return Easy; // legacy name
        foreach (var n in Names)
            if (n.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
                return n;
        return Custom;
    }

    public static int NewSeed() => Random.Shared.Next(0, 99_999_999 + 1);
}

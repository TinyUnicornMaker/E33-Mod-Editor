using E33ModEditor.Core;

namespace E33ModEditor.App;

/// <summary>
/// Burn tick damage model derived from BP_BattleBuff_BurnAggregator::Compute Burn Damage Total
/// Kismet: base = Atk² / (k·Def + Atk), per-stack, where k = BurnTailOff (vanilla 2.0).
///   Two DoubleConst 2.0 values implement the tail-off: base = Atk²/(k·Def+Atk) → Atk when Def→0, → Atk²/(k·Def) when Def≫Atk.
/// Team scale: 0.2 for player targets (GetTeam==1), 1.0 for enemies, then ×Aggregator.BurnDamageMultiplier 0.5 (scaled by BurnMultiplier).
/// Total per-turn = base × stacks × teamScale × 0.5 × BurnMultiplier. Total over duration = perTurn × duration.
/// X = burn stacks 0..1000, Y = damage per tick. Weapon Strength = Atk.
/// </summary>
public static class BurnCurve
{
    public const int MaxStacks = 1000;
    public const double PreviewAttack = 400;
    public const double PreviewDefense = 300;

    // Weapon Strength range for the graph control
    public const int MinWeaponStrength = 1;
    public const int MaxWeaponStrength = 5000;
    public const int DefaultWeaponStrength = 400;

    public static double BasePerStack(double attack, double defense, double tailOff = 2.0)
    {
        if (attack <= 0) return 0;
        double denom = tailOff * defense + attack;
        if (denom <= 0.001) return attack;
        return attack * attack / denom;
    }

    public static double DamagePerTick(int stacks, double attack, double defense, double burnMultiplier, double playerScale, bool isPlayerTarget, double tailOff = 2.0)
    {
        if (stacks <= 0) return 0;
        double teamScale = isPlayerTarget ? playerScale : 1.0;
        double aggScale = 0.5 * burnMultiplier;
        double perStack = BasePerStack(attack, defense, tailOff);
        return perStack * stacks * teamScale * aggScale;
    }

    public static double[] VanillaStack(double attack = PreviewAttack, double defense = PreviewDefense, bool isPlayerTarget = false)
        => GenerateStacks(GameplayTweaksKit.VanillaBurnMultiplier, GameplayTweaksKit.VanillaBurnPlayerScale, GameplayTweaksKit.VanillaBurnDuration, GameplayTweaksKit.VanillaBurnTailOff, attack, defense, isPlayerTarget, false);

    public static double[] GenerateStacks(double burnMultiplier, double playerScale, int duration, double tailOff = 2.0, double attack = PreviewAttack, double defense = PreviewDefense, bool isPlayerTarget = false, bool totalOverDuration = false)
    {
        var arr = new double[MaxStacks + 1];
        for (int s = 0; s <= MaxStacks; s++)
        {
            double tick = DamagePerTick(s, attack, defense, burnMultiplier, playerScale, isPlayerTarget, tailOff);
            arr[s] = totalOverDuration ? tick * duration : tick;
        }
        return arr;
    }

    public static double[] GenerateTotalStacks(double burnMultiplier, double playerScale, int duration, double tailOff = 2.0, double attack = PreviewAttack, double defense = PreviewDefense, bool isPlayerTarget = false)
        => GenerateStacks(burnMultiplier, playerScale, duration, tailOff, attack, defense, isPlayerTarget, true);

    // Convenience for the single graph: vanilla vs preview at same weapon strength
    public static double[] VanillaForWeapon(int weaponStrength, double defense = PreviewDefense)
        => VanillaStack(weaponStrength, defense, false);

    public static double[] PreviewForWeapon(int weaponStrength, double burnMultiplier, double playerScale, int duration, double tailOff, double defense = PreviewDefense)
        => GenerateStacks(burnMultiplier, playerScale, duration, tailOff, weaponStrength, defense, false, false);
}

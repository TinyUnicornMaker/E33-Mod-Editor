namespace E33ModEditor.App;

/// <summary>Shared pictos level curve (DT_EquipmentScalingTable_Pictos).</summary>
public static class PictosCurve
{
    public const int MaxLevel = 33;

    public const string Hp = "Stats.E_jRPG_StatType::NewEnumerator1";
    public const string Defense = "Stats.E_jRPG_StatType::NewEnumerator4";
    public const string Speed = "Stats.E_jRPG_StatType::NewEnumerator7";
    public const string Crit = "Stats.E_jRPG_StatType::NewEnumerator6";
    public const string Chroma = "ChromaValue";

    public const string HpMult = "BaseStatMultipliers.E_jRPG_StatType::NewEnumerator1";
    public const string DefMult = "BaseStatMultipliers.E_jRPG_StatType::NewEnumerator4";
    public const string SpeedMult = "BaseStatMultipliers.E_jRPG_StatType::NewEnumerator7";
    public const string CritMult = "BaseStatMultipliers.E_jRPG_StatType::NewEnumerator6";
    public const string ChromaMult = "ChromaValueMultiplier";

    /// <summary>Vanilla Max HP at pictos levels 0–33.</summary>
    public static readonly double[] VanillaHp =
    {
        0, 36, 42, 63, 92, 127, 168, 216, 270, 329,
        395, 467, 546, 630, 721, 818, 922, 1032, 1149, 1273,
        1403, 1541, 1686, 1837, 1996, 2162, 2336, 2517, 2705, 2902,
        3106, 3317, 3537, 3765,
    };

    /// <summary>Vanilla physical defense 0–33.</summary>
    public static readonly double[] VanillaDefense =
    {
        0, 6, 8, 13, 22, 34, 50, 71, 96, 122,
        152, 187, 218, 252, 288, 335, 392, 454, 523, 598,
        681, 771, 843, 919, 998, 1103, 1226, 1359, 1501, 1654,
        1817, 1990, 2228, 2485,
    };

    /// <summary>Vanilla speed 0–33.</summary>
    public static readonly double[] VanillaSpeed =
    {
        0, 8, 10, 14, 20, 26, 34, 43, 54, 65,
        78, 92, 108, 124, 142, 162, 182, 205, 228, 253,
        280, 308, 337, 368, 400, 434, 470, 507, 546, 586,
        628, 672, 717, 764,
    };

    /// <summary>Vanilla crit chance points 0–33 (shown in-game as %).</summary>
    public static readonly double[] VanillaCrit =
    {
        0, 6, 7, 7, 8, 9, 10, 10, 11, 12,
        13, 14, 15, 16, 16, 17, 18, 19, 20, 21,
        23, 24, 25, 26, 27, 28, 29, 31, 32, 33,
        34, 36, 37, 38,
    };

    /// <summary>Vanilla chroma 0–33.</summary>
    public static readonly double[] VanillaChroma =
    {
        50, 1200, 1700, 2400, 3400, 4600, 6000, 7600, 9400, 11400,
        13600, 16000, 18500, 21200, 24100, 27200, 30400, 33800, 37400, 41100,
        44900, 49000, 53200, 57500, 62000, 66700, 71500, 76400, 81500, 86800,
        92200, 97700, 103400, 109200,
    };

    public static (string Field, string Label, double[] Vanilla, bool Integer)[] Series { get; } =
    {
        (Hp, "Max HP", VanillaHp, true),
        (Defense, "Defense", VanillaDefense, true),
        (Speed, "Speed", VanillaSpeed, true),
        (Crit, "Critical Rate", VanillaCrit, true),
        (Chroma, "Chroma Value", VanillaChroma, true),
    };
}

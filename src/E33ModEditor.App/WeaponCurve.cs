using System.Globalization;
using E33ModEditor.App.ViewModels;
using Newtonsoft.Json.Linq;

namespace E33ModEditor.App;

/// <summary>Shared weapon level curve (DT_WeaponScalingTable) helpers.</summary>
public static class WeaponCurve
{
    public const int MaxLevel = 33;

    /// <summary>Vanilla Physical / Free Aim attack by level 0–33.</summary>
    public static readonly double[] VanillaPhysical =
    {
        20, 20, 44, 68, 99, 122, 145, 168, 191, 214,
        252, 272, 292, 312, 332, 352, 372, 392, 412, 432,
        472, 554, 636, 718, 800, 882, 964, 1046, 1128, 1210,
        1292, 1374, 1456, 1652,
    };

    /// <summary>Vanilla chroma sell value by level 0–33.</summary>
    public static readonly double[] VanillaChroma =
    {
        50, 55, 70, 95, 130, 175, 230, 295, 370, 455,
        550, 655, 770, 895, 1030, 1175, 1330, 1495, 1670, 1855,
        2050, 2255, 2470, 2695, 2930, 3175, 3430, 3695, 3970, 4255,
        4550, 4855, 5170, 5495,
    };

    public static readonly string[] Shapes = { "Vanilla", "Linear", "Ease In", "Ease Out", "Ease In-Out" };

    public static double[] ReadSeries(RecordRow row, string field)
    {
        var arr = new double[MaxLevel + 1];
        for (int i = 0; i <= MaxLevel; i++)
        {
            var tok = Token(row, i, field);
            arr[i] = tok is null ? 0 : tok.Type is JTokenType.Integer or JTokenType.Float
                ? tok.Value<double>()
                : double.TryParse(tok.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
        }
        return arr;
    }

    public static JToken? Token(RecordRow row, int level, string field) =>
        row.AdvancedEdits.TryGetValue($"LevelCurve.Level_{level}.{field}", out var adv)
            ? adv
            : MainViewModel.TokenAt(row.Baseline, $"LevelCurve.Level_{level}.{field}");

    public static double[] Generate(double start, double end, string shape, double bias, double[] vanilla)
    {
        var arr = new double[MaxLevel + 1];
        var useVanilla = string.Equals(shape, "Vanilla", StringComparison.OrdinalIgnoreCase);
        for (int i = 0; i <= MaxLevel; i++)
        {
            double t;
            if (useVanilla)
            {
                var a = vanilla[1];
                var b = vanilla[MaxLevel];
                t = Math.Abs(b - a) < 1e-9 ? 0 : (vanilla[i] - a) / (b - a);
            }
            else
            {
                t = i / (double)MaxLevel;
                t = shape switch
                {
                    "Ease In" => t * t,
                    "Ease Out" => 1 - (1 - t) * (1 - t),
                    "Ease In-Out" => t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2,
                    _ => t,
                };
            }
            var p = Math.Pow(2, bias);
            t = Math.Clamp(t, 0, 1);
            if (Math.Abs(p - 1) > 1e-6)
                t = Math.Pow(t, p);
            arr[i] = start + (end - start) * t;
        }
        return arr;
    }

    public static void WriteSeries(MainViewModel vm, RecordRow row, string field, double[] values, bool integer)
    {
        var n = Math.Min(values.Length, MaxLevel + 1);
        for (int i = 0; i < n; i++)
        {
            var v = integer ? Math.Round(values[i]).ToString(CultureInfo.InvariantCulture)
                            : values[i].ToString("0.###", CultureInfo.InvariantCulture);
            vm.QueueEdit(row, $"LevelCurve.Level_{i}.{field}", v);
        }
    }
}

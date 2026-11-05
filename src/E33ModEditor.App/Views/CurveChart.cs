using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace E33ModEditor.App.Views;

/// <summary>One series: dashed vanilla vs solid preview, red fill between them.
/// Supports arbitrary X range (XMin..XMax) and optional logarithmic Y scale.
/// XMin/XMax inclusive. Default 1..33 for weapon/pictos curves; burn uses 0..1000.
/// Y toggle: linear (default) or logarithmic (LogScaleY = true, transform = log10(v+1)).
/// Also overlays labelled dots (SetPoints) — e.g. move max totals vs the AP clamp.
/// </summary>
public sealed class CurveChart : Control
{
    private static readonly IBrush Axis = new SolidColorBrush(Color.Parse("#FF3B3529"));
    private static readonly IBrush TickLabel = new SolidColorBrush(Color.Parse("#FF9C9280"));
    private static readonly IBrush AxisTitle = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush HoverFill = new SolidColorBrush(Color.Parse("#33EFE3C8"));
    private static readonly IBrush TooltipBg = new SolidColorBrush(Color.Parse("#E61C1812"));
    private static readonly IBrush TooltipFg = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush DiffFill = new SolidColorBrush(Color.Parse("#66E24B3A"));
    private static readonly IPen VanillaPen = new Pen(new SolidColorBrush(Color.Parse("#FF9C9280")), 1.4)
    {
        DashStyle = DashStyle.Dash,
    };
    private static readonly IPen HoverPen = new Pen(new SolidColorBrush(Color.Parse("#FFEFE3C8")), 1)
    {
        DashStyle = DashStyle.Dot,
    };

    private readonly IPen _previewPen;
    private double[] _vanilla = Array.Empty<double>();
    private double[] _preview = Array.Empty<double>();
    private IReadOnlyList<(int X, double Y, string Label)> _points = Array.Empty<(int, double, string)>();
    private int? _hoverLevel;
    private Rect _plot;

    public string AxisXTitle { get; set; } = "Weapon Level";
    public string AxisYTitle { get; set; } = "Value";
    public string VanillaName { get; set; } = "vanilla";
    public string PreviewName { get; set; } = "preview";
    public bool IntegerValues { get; set; }
    public int XMin { get; set; } = 1;
    public int XMax { get; set; } = 33;
    public bool LogScaleY { get; set; }

    public event EventHandler<int?>? HoverChanged;

    public CurveChart(Color previewColor)
    {
        MinHeight = 170;
        Height = 210;
        ClipToBounds = true;
        _previewPen = new Pen(new SolidColorBrush(previewColor), 2.2);
        PointerMoved += OnPointerMoved;
        PointerExited += (_, _) => SetHover(null, notify: true);
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    public void SetCompare(double[] vanilla, double[] preview)
    {
        _vanilla = vanilla;
        _preview = preview;
        InvalidateVisual();
    }

    /// <summary>Overlays labelled dots at (X, Y) — e.g. move max totals vs the AP clamp.</summary>
    public void SetPoints(IReadOnlyList<(int X, double Y, string Label)> points)
    {
        _points = points;
        InvalidateVisual();
    }

    public void SetHover(int? level, bool notify = false)
    {
        var clamped = level is int l ? Math.Clamp(l, XMin, XMax) : (int?)null;
        if (_hoverLevel == clamped) return;
        _hoverLevel = clamped;
        InvalidateVisual();
        if (notify) HoverChanged?.Invoke(this, _hoverLevel);
    }

    // Transform for Y positioning; log uses log10(v+1) so 0 maps to 0 cleanly
    private double TransformY(double v) => LogScaleY ? Math.Log10(Math.Max(0, v) + 1) : v;
    private double InverseY(double t) => LogScaleY ? Math.Pow(10, t) - 1 : t;

    private double Yn(double val, double minTr, double maxTr)
    {
        if (Math.Abs(maxTr - minTr) < 1e-12) return 0.5;
        var tv = TransformY(val);
        return (tv - minTr) / (maxTr - minTr);
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 8 || h < 8) return;

        const double padL = 56, padR = 10, padT = 18, padB = 38;
        _plot = new Rect(padL, padT, Math.Max(8, w - padL - padR), Math.Max(8, h - padT - padB));
        var plot = _plot;
        context.DrawRectangle(null, new Pen(Axis), plot);

        var yTitle = new FormattedText(AxisYTitle, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, Typeface.Default, 13, AxisTitle);
        using (context.PushTransform(Matrix.CreateTranslation(2, plot.Center.Y)))
        using (context.PushTransform(Matrix.CreateRotation(-Math.PI / 2)))
        {
            context.DrawText(yTitle, new Point(-yTitle.Width / 2, 0));
        }

        var xTitle = new FormattedText(AxisXTitle, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, Typeface.Default, 13, AxisTitle);
        context.DrawText(xTitle, new Point(plot.Center.X - xTitle.Width / 2, h - xTitle.Height - 1));

        // X ticks – adapt to range
        int[] xTicks;
        if (XMax - XMin >= 500)
            xTicks = new[] { XMin, 250, 500, 750, XMax };
        else if (XMin == 1 && XMax == 33)
            xTicks = new[] { 1, 4, 10, 20, 33 };
        else if (XMax - XMin <= 10)
            xTicks = Enumerable.Range(XMin, XMax - XMin + 1).ToArray();
        else
        {
            var span = XMax - XMin;
            xTicks = new[] { XMin, XMin + span / 4, XMin + span / 2, XMin + 3 * span / 4, XMax };
        }
        foreach (var lvl in xTicks)
        {
            var x = XOf(lvl, plot);
            context.DrawLine(new Pen(Axis), new Point(x, plot.Top), new Point(x, plot.Bottom));
            var ft = new FormattedText($"{lvl}", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 13, TickLabel);
            context.DrawText(ft, new Point(x - ft.Width / 2, plot.Bottom + 3));
        }

        if (!TryRange(out var min, out var max)) return;
        var minTr = TransformY(min);
        var maxTr = TransformY(max);
        if (Math.Abs(maxTr - minTr) < 1e-9)
        {
            maxTr = minTr + 1;
            max = InverseY(maxTr);
        }

        // Y ticks: 3 ticks at 0, 0.5, 1 in transformed space, labels inverse-transformed
        foreach (var t in new[] { 0.0, 0.5, 1.0 })
        {
            var vTr = minTr + (maxTr - minTr) * t;
            var v = InverseY(vTr);
            var y = plot.Bottom - t * plot.Height;
            context.DrawLine(new Pen(Axis), new Point(plot.X - 3, y), new Point(plot.X, y));
            var ft = new FormattedText(Fmt(v), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 12, TickLabel);
            context.DrawText(ft, new Point(plot.X - 6 - ft.Width, y - ft.Height / 2));
        }

        Point Pt(double[] data, int level)
        {
            var denom = Math.Max(1, XMax - XMin);
            var t = (level - XMin) / (double)denom;
            var x = plot.X + t * plot.Width;
            var val = Sample(data, level);
            var yn = Yn(val, minTr, maxTr);
            var y = plot.Bottom - yn * plot.Height;
            return new Point(x, y);
        }

        // Fill between curves
        var fill = new StreamGeometry();
        using (var g = fill.Open())
        {
            // preview forward
            bool first = true;
            for (int i = XMin; i <= XMax; i++)
            {
                var pt = Pt(_preview, i);
                if (first) { g.BeginFigure(pt, true); first = false; }
                else g.LineTo(pt);
            }
            // vanilla back
            for (int i = XMax; i >= XMin; i--) g.LineTo(Pt(_vanilla, i));
        }
        context.DrawGeometry(DiffFill, null, fill);

        DrawLine(context, _vanilla, VanillaPen, plot, minTr, maxTr);
        DrawLine(context, _preview, _previewPen, plot, minTr, maxTr);

        foreach (var (px, py, _) in _points)
        {
            if (px < XMin || px > XMax) continue;
            var x = XOf(px, plot);
            var yn = Yn(py, minTr, maxTr);
            var y = plot.Bottom - yn * plot.Height;
            var dot = new StreamGeometry();
            using (var g = dot.Open())
            {
                g.BeginFigure(new Point(x - 4, y), true);
                g.ArcTo(new Point(x + 4, y), new Size(4, 4), 0, true, SweepDirection.Clockwise);
                g.ArcTo(new Point(x - 4, y), new Size(4, 4), 0, true, SweepDirection.Clockwise);
            }
            context.DrawGeometry(_previewPen.Brush, null, dot);
        }

        if (_hoverLevel is int hl)
        {
            var hx = XOf(hl, plot);
            var span = Math.Max(1, XMax - XMin);
            var band = Math.Max(8, plot.Width / span * 0.9);
            // for large spans keep band visible but not too thin
            if (span > 100) band = Math.Max(6, plot.Width / span * 1.2);
            context.FillRectangle(HoverFill, new Rect(hx - band / 2, plot.Top, band, plot.Height));
            context.DrawLine(HoverPen, new Point(hx, plot.Top), new Point(hx, plot.Bottom));
            var pv = Sample(_preview, hl);
            var vv = Sample(_vanilla, hl);
            var tipText = XMax <= 33
                ? $"Lv {hl}\n{PreviewName}  {Fmt(pv)}\n{VanillaName}  {Fmt(vv)}"
                : $"Burn {hl}\n{PreviewName}  {Fmt(pv)}\n{VanillaName}  {Fmt(vv)}";
            var at = _points.Where(p => p.X == hl).Select(p => p.Label).ToList();
            if (at.Count > 0)
                tipText += "\n" + string.Join("\n", at);
            var tip = new FormattedText(tipText,
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 13, TooltipFg);
            var tx = Math.Clamp(hx + 8, plot.X + 4, plot.Right - tip.Width - 8);
            var ty = plot.Top + 6;
            var box = new Rect(tx - 5, ty - 3, tip.Width + 10, tip.Height + 6);
            context.DrawRectangle(TooltipBg, new Pen(Axis), box, 3, 3);
            context.DrawText(tip, new Point(tx, ty));
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_plot.Width < 8)
        {
            SetHover(null, notify: true);
            return;
        }
        var pos = e.GetPosition(this);
        var t = (pos.X - _plot.X) / _plot.Width;
        t = Math.Clamp(t, 0, 1);
        var range = Math.Max(1, XMax - XMin);
        var level = (int)Math.Round(t * range) + XMin;
        SetHover(Math.Clamp(level, XMin, XMax), notify: true);
    }

    private void DrawLine(DrawingContext ctx, double[] data, IPen pen, Rect plot, double minTr, double maxTr)
    {
        if (data.Length == 0) return;
        Point Pt(int level)
        {
            var denom = Math.Max(1, XMax - XMin);
            var t = (level - XMin) / (double)denom;
            var x = plot.X + t * plot.Width;
            var v = Sample(data, level);
            var yn = Yn(v, minTr, maxTr);
            var y = plot.Bottom - yn * plot.Height;
            return new Point(x, y);
        }
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(Pt(XMin), false);
            for (int i = XMin + 1; i <= XMax; i++) g.LineTo(Pt(i));
        }
        ctx.DrawGeometry(null, pen, geo);
    }

    private bool TryRange(out double min, out double max)
    {
        min = double.MaxValue;
        max = double.MinValue;
        Acc(_vanilla, ref min, ref max);
        Acc(_preview, ref min, ref max);
        foreach (var (_, y, _) in _points)
        {
            min = Math.Min(min, y);
            max = Math.Max(max, y);
        }
        if (min == double.MaxValue) return false;
        if (Math.Abs(max - min) < 1e-9) max = min + 1;
        // padding in transformed space – compute padding after transform to keep log proportions
        // For simplicity pad raw values as before then transform; that gives visible margin.
        var pad = (max - min) * 0.06;
        // Avoid negative min for log (Transform handles it but keep padding non-negative)
        min -= pad;
        max += pad;
        if (LogScaleY && min < 0) min = 0;
        return true;
    }

    private void Acc(double[] data, ref double min, ref double max)
    {
        var lo = Math.Max(0, XMin);
        var hi = Math.Min(data.Length - 1, XMax);
        for (int i = lo; i <= hi; i++)
        {
            min = Math.Min(min, data[i]);
            max = Math.Max(max, data[i]);
        }
    }

    private string Fmt(double v) => IntegerValues
        ? v.ToString("0", CultureInfo.InvariantCulture)
        : v.ToString("0.##", CultureInfo.InvariantCulture);

    private static double Sample(double[] data, int level) =>
        level >= 0 && level < data.Length ? data[level] : 0;

    private double XOf(int level, Rect plot)
    {
        var denom = Math.Max(1, XMax - XMin);
        return plot.X + plot.Width * ((level - XMin) / (double)denom);
    }
}

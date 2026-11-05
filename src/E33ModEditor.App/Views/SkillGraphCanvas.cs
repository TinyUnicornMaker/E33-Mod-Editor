using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using E33ModEditor.Core;

namespace E33ModEditor.App.Views;

/// <summary>
/// One visual skill-tree node: the diamond itself, enriched with display data.
/// Positions are in game graph coordinates; the canvas maps them to screen.
/// </summary>
public sealed class SkillGraphNodeVM
{
    public required SkillGraphKit.GraphNode Raw { get; init; }
    public string DisplayName { get; set; } = "";
    public string Element { get; set; } = "";
    /// <summary>Modded Long description when a kit rewrites the move, else the
    /// vanilla string-table text (empty when neither is known).</summary>
    public string Description { get; set; } = "";
    public bool IsGradient { get; set; }
    /// <summary>Screen-space diamond centre (recomputed every Render).</summary>
    public double ScreenX { get; set; }
    public double ScreenY { get; set; }
    public string Stem => Raw.Stem;
    public double X => Raw.X;
    public double Y => Raw.Y;
}

/// <summary>
/// Custom-drawn skill grid: element-coloured AP diamonds on a snap lattice
/// derived from the character's vanilla node positions, dependency links,
/// drag-to-snap and right-click link editing. Never adds or deletes skills.
/// </summary>
public sealed class SkillGraphCanvas : Control
{
    // Theme palette (Expedition-33 dark-gold look).
    private static readonly IBrush Axis = new SolidColorBrush(Color.Parse("#FF3B3529"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#FFEFE3C8"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#FF9C9280"));
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#FFC9A961"));
    private static readonly IBrush GoldBright = new SolidColorBrush(Color.Parse("#FFFFD98A"));
    private static readonly IBrush TooltipBg = new SolidColorBrush(Color.Parse("#F21C1812"));
    private static readonly IPen LinkPen = new Pen(new SolidColorBrush(Color.Parse("#FFB9AD93")), 1.6);
    private static readonly IPen LinkHoverPen = new Pen(new SolidColorBrush(Color.Parse("#FFFFD98A")), 3);
    private static readonly IPen LinkPreviewPen = new Pen(GoldBright, 1.8)
    {
        DashStyle = new DashStyle(new[] { 4.0, 3.0 }, 0),
    };
    private static readonly IPen LatticeDot = new Pen(Muted, 1);
    private static readonly IPen SubDot = new Pen(new SolidColorBrush(Color.Parse("#FF6A6152")), 1);
    private static readonly IBrush FontBrush = new SolidColorBrush(Color.Parse("#FF1C1812"));
    private static readonly IBrush CanvasBackdrop = new SolidColorBrush(Color.Parse("#FF15120D"));

    private static readonly IReadOnlyDictionary<string, IBrush> ElementColours =
        new Dictionary<string, IBrush>(StringComparer.OrdinalIgnoreCase)
        {
            ["Fire"] = new SolidColorBrush(Color.Parse("#FFD9593F")),
            ["Ice"] = new SolidColorBrush(Color.Parse("#FF6FBFD9")),
            ["Lightning"] = new SolidColorBrush(Color.Parse("#FFE3C24A")),
            ["Earth"] = new SolidColorBrush(Color.Parse("#FFB08968")),
            ["Dark"] = new SolidColorBrush(Color.Parse("#FF8E6BBF")),
            ["Light"] = new SolidColorBrush(Color.Parse("#FFF2E8C9")),
            ["Physical"] = new SolidColorBrush(Color.Parse("#FFB7AB93")),
            ["Void"] = new SolidColorBrush(Color.Parse("#FF7A8291")),
            ["Multi"] = new SolidColorBrush(Color.Parse("#FF9AD4C8")),   // Genesis family (all four elements)
            ["Weapon"] = new SolidColorBrush(Color.Parse("#FFCFC4B0")),  // follows the equipped weapon
        };

    private const double DiamondW = 40;   // full width of the diamond (screen px)
    private const double DiamondH = 32;   // full height
    private const double R = 5.0;         // link hit radius
    private const double Pad = 46.0;      // canvas padding around the graph bounds

    // ── fixed lattice world ─────────────────────────────────────────────
    // The game's skill-tree grid is uniform everywhere: columns every 125
    // units, rows every 100, with one shared phase per graph. Lune's in-game
    // (and custom) tree uses x ≡ 100 (mod 125), y ≡ 125 (mod 100); every
    // character's graph is snapped to that same absolute lattice so a given
    // number of game units is the same pixel distance on every tab.
    // (Lattice constants + snap live in SkillGraphKit — one source of truth.)
    private const double LatticeX0 = SkillGraphKit.LatticeX0;
    private const double LatticeY0 = SkillGraphKit.LatticeY0;
    private const double StepX = SkillGraphKit.StepX;
    private const double StepY = SkillGraphKit.StepY;
    // The shared world is sized from Lune's full union tree (x up to 1600,
    // y up to 825) plus one step of margin so every graph has at least as
    // much room as Lune's current custom graph.
    private const double WorldX0Default = LatticeX0 - StepX;
    private const double WorldX1Default = 1600 + StepX;
    private const double WorldY0Default = LatticeY0 - StepY;
    private const double WorldY1Default = 925 + StepY; // Verso vanilla already sits at Y=925

    private double _worldX0 = WorldX0Default;
    private double _worldX1 = WorldX1Default;
    private double _worldY0 = WorldY0Default;
    private double _worldY1 = WorldY1Default;

    private readonly List<SkillGraphNodeVM> _nodes = new();
    private readonly List<(SkillGraphNodeVM From, SkillGraphNodeVM To)> _links = new();

    // Geometry computed per Render from the fixed world: the scale is the same
    // on every character tab (screen = origin + graph * scale).
    private double _originX;
    private double _originY;
    private double _scale;

    // Interaction state.
    private SkillGraphNodeVM? _dragNode;
    private Point _dragStartScreen;       // grab point on the canvas (screen)
    private Point _lastPointer;
    private SkillGraphNodeVM? _pendingLink;
    private SkillGraphNodeVM? _hoverNode;
    private (SkillGraphNodeVM From, SkillGraphNodeVM To)? _hoverLink;

    /// <summary>Raised after any committed change (move / link add / link delete).</summary>
    public event Action? GraphChanged;

    /// <summary>Raised with a human status message for the module's status line.</summary>
    public event Action<string>? StatusChanged;

    public SkillGraphCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerExited += (_, _) =>
        {
            _hoverNode = null;
            _hoverLink = null;
            InvalidateVisual();
        };
    }

    public static IBrush? ElementBrush(string element) =>
        ElementColours.TryGetValue(element ?? "", out var b) ? b : null;

    /// <summary>Loads the nodes for one character's graph. The snap lattice is
    /// the fixed 125×100 grid shared by every character tab.</summary>
    public void Load(IEnumerable<SkillGraphNodeVM> nodes)
    {
        _nodes.Clear();
        _nodes.AddRange(nodes);
        _dragNode = null;
        _pendingLink = null;
        if (_nodes.Count > 0)
        {
            _worldX0 = Math.Min(WorldX0Default, _nodes.Min(n => n.Raw.X) - StepX);
            _worldX1 = Math.Max(WorldX1Default, _nodes.Max(n => n.Raw.X) + StepX);
            _worldY0 = Math.Min(WorldY0Default, _nodes.Min(n => n.Raw.Y) - StepY);
            _worldY1 = Math.Max(WorldY1Default, _nodes.Max(n => n.Raw.Y) + StepY);
        }
        InvalidateVisual();
    }

    /// <summary>Replaces the link set (from the session's layout).</summary>
    public void SetLinks(IEnumerable<(string From, string To)> links)
    {
        _links.Clear();
        var byStem = new Dictionary<string, SkillGraphNodeVM>(StringComparer.Ordinal);
        foreach (var n in _nodes) byStem[n.Stem] = n;
        foreach (var (from, to) in links)
            if (byStem.TryGetValue(from, out var f) && byStem.TryGetValue(to, out var t) && f != t)
                _links.Add((f, t));
        _hoverLink = null;
        InvalidateVisual();
    }

    public IReadOnlyList<(string From, string To)> CurrentLinks =>
        _links.Select(l => (l.From.Stem, l.To.Stem)).ToList();

    /// <summary>Repositions nodes to the given coords (vanilla restore or a
    /// saved layout). The snap lattice is fixed and never touched. Coordinates
    /// that do not already sit on the fixed lattice (major or half-step sub
    /// points) are snapped onto it, so on-grid content never moves but legacy
    /// off-grid authored nodes align to the shared grid.</summary>
    public void SetPositions(IEnumerable<(string Stem, double X, double Y)> positions)
    {
        var map = positions.ToDictionary(p => p.Stem, p => p, StringComparer.Ordinal);
        foreach (var n in _nodes)
            if (map.TryGetValue(n.Stem, out var v))
            {
                n.Raw.X = SnapX(v.X);
                n.Raw.Y = SnapY(v.Y);
            }
        if (_nodes.Count > 0)
        {
            _worldX0 = Math.Min(WorldX0Default, _nodes.Min(n => n.Raw.X) - StepX);
            _worldX1 = Math.Max(WorldX1Default, _nodes.Max(n => n.Raw.X) + StepX);
            _worldY0 = Math.Min(WorldY0Default, _nodes.Min(n => n.Raw.Y) - StepY);
            _worldY1 = Math.Max(WorldY1Default, _nodes.Max(n => n.Raw.Y) + StepY);
        }
        InvalidateVisual();
    }

    // ── fixed lattice helpers ─────────────────────────────

    /// <summary>Nearest lattice x for a game x coordinate: exact major points
    /// (LatticeX0 + i*StepX) and the halfway sub-points between them.</summary>
    private static double SnapX(double x) => SkillGraphKit.SnapGraphX(x);

    /// <summary>Nearest lattice y for a game y coordinate (rows every 100 from
    /// LatticeY0, with halfway sub-points).</summary>
    private static double SnapY(double y) => SkillGraphKit.SnapGraphY(y);

    // ───────────────────────── rendering ─────────────────────────

    /// <summary>World rectangle visible in this canvas (in game units).</summary>
    private (double X0, double Y0, double X1, double Y1) VisibleWorld()
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (_scale <= 0 || w < 8 || h < 8) return (0, 0, 0, 0);
        // screen = origin + world*scale  ⇒  world = (screen - origin)/scale
        return ((-_originX) / _scale, (-_originY) / _scale,
            (w - _originX) / _scale, (h - _originY) / _scale);
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 8 || h < 8 || _nodes.Count == 0) return;

        // Canvas backdrop.
        context.FillRectangle(CanvasBackdrop, new Rect(0, 0, w, h));

        // One scale for every character: fit the fixed shared world (at least as
        // big as Lune's full union tree) into the canvas. A 125-unit column step
        // is therefore the same pixel distance on every tab.
        var worldSpanX = Math.Max(StepX, _worldX1 - _worldX0);
        var worldSpanY = Math.Max(StepY, _worldY1 - _worldY0);
        _scale = Math.Min((w - 2 * Pad) / worldSpanX, (h - 2 * Pad) / worldSpanY);
        _scale = Math.Min(_scale, 1.4); // never zoom past a readable size

        // Center this graph's own content within the shared-scale world so small
        // graphs sit centered (same spacing, just fewer dots).
        double minX = _nodes.Min(n => n.Raw.X), maxX = _nodes.Max(n => n.Raw.X);
        double minY = _nodes.Min(n => n.Raw.Y), maxY = _nodes.Max(n => n.Raw.Y);
        var cx = (minX + maxX) / 2;
        var cy = (minY + maxY) / 2;
        _originX = w / 2 - cx * _scale;
        _originY = h / 2 - cy * _scale;

        foreach (var n in _nodes)
        {
            var x = _originX + n.Raw.X * _scale;
            var y = _originY + n.Raw.Y * _scale;
            if (n == _dragNode)
            {
                // Drag preview: follow the pointer (screen-space delta), while
                // the node's Raw position stays anchored until release.
                x += _lastPointer.X - _dragStartScreen.X;
                y += _lastPointer.Y - _dragStartScreen.Y;
            }
            n.ScreenX = x;
            n.ScreenY = y;
        }

        DrawLattice(context);

        // Links (under nodes).
        foreach (var (from, to) in _links)
        {
            var f = new Point(from.ScreenX, from.ScreenY);
            var t = new Point(to.ScreenX, to.ScreenY);
            var pen = _hoverLink is { } hl && hl == (from, to) ? LinkHoverPen : LinkPen;
            context.DrawLine(pen, f, t);
        }
        if (_pendingLink is { } src)
        {
            var sp = new Point(src.ScreenX, src.ScreenY);
            context.DrawLine(LinkPreviewPen, sp, _lastPointer);
        }

        // Nodes.
        foreach (var n in _nodes)
            DrawDiamond(context, n);

        if (_hoverNode is { } hn && hn != _dragNode)
            DrawTooltip(context, hn);
    }

    /// <summary>Draws the fixed 125×100 lattice (majors + halfway sub-points)
    /// across the whole visible world — the same absolute grid on every tab.</summary>
    private void DrawLattice(DrawingContext context)
    {
        var (vx0, vy0, vx1, vy1) = VisibleWorld();
        if (vx1 <= vx0 || vy1 <= vy0) return;

        // Faint by default; brighter while a node is being dragged.
        var full = _dragNode is not null ? LatticeDot : DotPen(0.5);
        var sub = _dragNode is not null ? SubDot : DotPen(0.28);

        // Sub-points: half-step columns and half-step rows. A sub-dot sits on
        // every (half column × full row), every (full column × half row), and
        // every (half × half) — i.e. the two midline families plus their cell
        // centres. Only draw the visible slice so the grid stays cheap.
        var halfX = StepX / 2;
        var halfY = StepY / 2;
        long i0 = (long)Math.Floor((vx0 - LatticeX0) / StepX);
        long i1 = (long)Math.Ceiling((vx1 - LatticeX0) / StepX);
        long j0 = (long)Math.Floor((vy0 - LatticeY0) / StepY);
        long j1 = (long)Math.Ceiling((vy1 - LatticeY0) / StepY);

        for (long i = i0; i <= i1; i++)
            for (long j = j0; j <= j1; j++)
            {
                var xMaj = LatticeX0 + i * StepX;
                var yMaj = LatticeY0 + j * StepY;
                var xHalf = xMaj + halfX;
                var yHalf = yMaj + halfY;
                // horizontal midlines: (major col, half row)
                if (xMaj >= vx0 && xMaj <= vx1 && yHalf >= vy0 && yHalf <= vy1)
                    DrawSubDot(context, sub, xMaj, yHalf);
                // vertical midlines: (half col, major row)
                if (xHalf >= vx0 && xHalf <= vx1 && yMaj >= vy0 && yMaj <= vy1)
                    DrawSubDot(context, sub, xHalf, yMaj);
                // cell centres: (half col, half row)
                if (xHalf >= vx0 && xHalf <= vx1 && yHalf >= vy0 && yHalf <= vy1)
                    DrawSubDot(context, sub, xHalf, yHalf);
            }

        for (long i = i0; i <= i1; i++)
            for (long j = j0; j <= j1; j++)
            {
                var mx = LatticeX0 + i * StepX;
                var my = LatticeY0 + j * StepY;
                if (mx < vx0 || mx > vx1 || my < vy0 || my > vy1) continue;
                var p = new Point(_originX + mx * _scale, _originY + my * _scale);
                context.DrawEllipse(full.Brush, null, new Rect(p.X - 2.5, p.Y - 2.5, 5, 5));
            }
    }

    private void DrawSubDot(DrawingContext context, IPen pen, double worldX, double worldY)
    {
        var p = new Point(_originX + worldX * _scale, _originY + worldY * _scale);
        context.DrawLine(pen, new Point(p.X - 2, p.Y), new Point(p.X + 2, p.Y));
        context.DrawLine(pen, new Point(p.X, p.Y - 2), new Point(p.X, p.Y + 2));
    }

    private static Pen DotPen(double alpha) =>
        new(new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), 0xFF, 0xC9, 0xA9)), 1);

    private void DrawDiamond(DrawingContext context, SkillGraphNodeVM n)
    {
        var x = n.ScreenX;
        var y = n.ScreenY;
        var brush = ElementBrush(n.Element) ?? Muted;
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(x, y - DiamondH / 2), true);
            g.LineTo(new Point(x + DiamondW / 2, y));
            g.LineTo(new Point(x, y + DiamondH / 2));
            g.LineTo(new Point(x - DiamondW / 2, y));
        }
        context.DrawGeometry(brush, null, geo);
        context.DrawGeometry(null, new Pen(_dragNode == n ? GoldBright : Cream, _dragNode == n ? 2.2 : 1.1), geo);

        var cost = n.Raw.DisplayCost.ToString(CultureInfo.InvariantCulture);
        var ft = new FormattedText(cost, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Georgia, Liberation Serif, Times New Roman"), 13, FontBrush);
        context.DrawText(ft, new Point(x - ft.Width / 2, y - ft.Height / 2));

        if (n.Raw.IsStartingSkill)
        {
            var ring = new Pen(Gold, 1.6);
            context.DrawEllipse(null, ring, new Rect(x - DiamondW / 2 - 4, y - DiamondH / 2 - 4, DiamondW + 8, DiamondH + 8));
        }
        else if (n.IsGradient)
        {
            var ring = new Pen(GoldBright, 2.4);
            context.DrawEllipse(null, ring, new Rect(x - DiamondW / 2 - 4, y - DiamondH / 2 - 4, DiamondW + 8, DiamondH + 8));
            var badge = new FormattedText("G", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Georgia, Liberation Serif, Times New Roman"), 10, GoldBright);
            context.DrawText(badge, new Point(x + DiamondW / 2 - 9, y - DiamondH / 2 - 2));
        }
        else if (n.Raw.IsSecret)
        {
            var ring = new Pen(Muted, 1.2) { DashStyle = DashStyle.Dash };
            context.DrawEllipse(null, ring, new Rect(x - DiamondW / 2 - 4, y - DiamondH / 2 - 4, DiamondW + 8, DiamondH + 8));
        }
    }

    private const double TooltipMaxWidth = 440;
    private static readonly Regex TagRx = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex RankImgRx = new("<img\\s+id=\"Rank_([A-Za-z])\"\\s*/>", RegexOptions.Compiled);

    /// <summary>Strips the game's rich-text markup (keyword/img tags) for the
    /// plain-text tooltip. Rank icons have no text fallback in the cooked
    /// strings, so <img id="Rank_X"/> becomes the letter X first (editor-only;
    /// game files are untouched) — otherwise Verso's rank gates read as blank.
    /// Real newlines are preserved by the caller.</summary>
    private static string Plain(string s) => TagRx.Replace(RankImgRx.Replace(s, "$1"), "");

    private static double MeasureLine(string s) =>
        new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            Typeface.Default, 13, Cream).Width;

    /// <summary>Word-wraps one paragraph to the tooltip width.</summary>
    private static IEnumerable<string> Wrap(string text, double maxWidth)
    {
        if (text.Length == 0) yield break;
        var cur = "";
        foreach (var w in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var cand = cur.Length == 0 ? w : cur + " " + w;
            if (cur.Length == 0 || MeasureLine(cand) <= maxWidth) { cur = cand; continue; }
            yield return cur;
            cur = w;
        }
        if (cur.Length > 0) yield return cur;
    }

    private void DrawTooltip(DrawingContext context, SkillGraphNodeVM n)
    {
        var header = $"{n.DisplayName} — {n.Element} — {n.Raw.DisplayCost} AP";
        if (n.IsGradient) header += "  (gradient)";
        if (n.Raw.IsStartingSkill) header += "  (starter)";
        var body = new List<string>();
        var desc = (n.Description ?? "").Replace("\r", "");
        if (desc.Length > 0)
            foreach (var para in desc.Split('\n'))
                body.AddRange(Wrap(Plain(para).Trim(), TooltipMaxWidth));

        var headFt = new FormattedText(header, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            Typeface.Default, 13, GoldBright);
        var bodyFts = body.Select(l => new FormattedText(l, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, Typeface.Default, 13, Cream)).ToList();
        var w = headFt.Width;
        foreach (var f in bodyFts) w = Math.Max(w, f.Width);
        var h = headFt.Height + bodyFts.Sum(f => f.Height);
        var tx = Math.Clamp(n.ScreenX + DiamondW / 2 + 8, 4, Math.Max(4, Bounds.Width - w - 12));
        var ty = Math.Clamp(n.ScreenY - DiamondH / 2 - h - 10, 4, Math.Max(4, Bounds.Height - h - 8));
        var box = new Rect(tx - 6, ty - 4, w + 12, h + 8);
        context.DrawRectangle(TooltipBg, new Pen(Axis), box, 4, 4);
        context.DrawText(headFt, new Point(tx, ty));
        var y = ty + headFt.Height;
        foreach (var f in bodyFts)
        {
            context.DrawText(f, new Point(tx, y));
            y += f.Height;
        }
    }

    private Point ToScreen(double x, double y) => new(_originX + x * _scale, _originY + y * _scale);

    // ───────────────────────── interaction ─────────────────────────

    private bool DiamondContains(SkillGraphNodeVM n, Point p) =>
        Math.Abs(p.X - n.ScreenX) / (DiamondW / 2) + Math.Abs(p.Y - n.ScreenY) / (DiamondH / 2) <= 1;

    private SkillGraphNodeVM? NodeAt(Point p)
    {
        for (int i = _nodes.Count - 1; i >= 0; i--)
            if (DiamondContains(_nodes[i], p)) return _nodes[i];
        return null;
    }

    private (SkillGraphNodeVM From, SkillGraphNodeVM To)? LinkAt(Point p)
    {
        foreach (var link in _links)
        {
            var a = new Point(link.From.ScreenX, link.From.ScreenY);
            var b = new Point(link.To.ScreenX, link.To.ScreenY);
            if (DistToSegment(p, a, b) <= R) return link;
        }
        return null;
    }

    private static double DistToSegment(Point p, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-12) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
        var qx = a.X + t * dx;
        var qy = a.Y + t * dy;
        return Math.Sqrt((p.X - qx) * (p.X - qx) + (p.Y - qy) * (p.Y - qy));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsRightButtonPressed)
        {
            OpenContextMenu(pos);
            e.Handled = true;
            return;
        }
        if (props.IsLeftButtonPressed && _nodes.Count > 0)
        {
            var hit = NodeAt(pos);
            if (hit is not null)
            {
                _dragNode = hit;
                _dragStartScreen = pos;
                _hoverNode = hit;
                Focus();
                e.Handled = true;
            }
        }
        InvalidateVisual();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        _lastPointer = pos;
        if (_dragNode is not null)
        {
            // The diamond preview is drawn from the pointer delta in Render.
            InvalidateVisual();
            return;
        }
        var node = NodeAt(pos);
        var link = node is null ? LinkAt(pos) : null;
        if (node != _hoverNode || link != _hoverLink)
        {
            _hoverNode = node;
            _hoverLink = link;
            Cursor = node is not null || link is not null
                ? new Cursor(StandardCursorType.Hand)
                : new Cursor(StandardCursorType.Cross);
            InvalidateVisual();
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragNode is null) return;
        // The dragged node's current on-screen centre (base + pointer delta),
        // matching where Render drew it last.
        var node = _dragNode;
        var preview = new Point(
            _originX + node.Raw.X * _scale + (_lastPointer.X - _dragStartScreen.X),
            _originY + node.Raw.Y * _scale + (_lastPointer.Y - _dragStartScreen.Y));
        // Back to world units, then snap onto the fixed lattice (major or sub).
        var worldX = (preview.X - _originX) / _scale;
        var worldY = (preview.Y - _originY) / _scale;
        node.Raw.X = SnapX(worldX);
        node.Raw.Y = SnapY(worldY);
        GraphChanged?.Invoke();
        StatusChanged?.Invoke($"{node.DisplayName} moved to grid ({node.Raw.X:0}, {node.Raw.Y:0}).");
        _dragNode = null;
        InvalidateVisual();
    }

    // ───────────────────────── context menu ─────────────────────────

    private void OpenContextMenu(Point pos)
    {
        var node = NodeAt(pos);
        var link = node is null ? LinkAt(pos) : null;

        var menu = new ContextMenu();
        var items = new List<MenuItem>();
        if (link is { } hl)
        {
            var del = new MenuItem { Header = $"Delete link: {hl.From.DisplayName} → {hl.To.DisplayName}" };
            del.Click += (_, _) => DeleteLink(hl);
            items.Add(del);
        }
        else if (node is not null)
        {
            if (_pendingLink is null)
            {
                var start = new MenuItem { Header = $"Link from {node.DisplayName}…" };
                start.Click += (_, _) =>
                {
                    _pendingLink = node;
                    StatusChanged?.Invoke($"Pending link from {node.DisplayName}. Right-click a target move to link it.");
                    InvalidateVisual();
                };
                items.Add(start);
            }
            else if (_pendingLink == node)
            {
                var clear = new MenuItem { Header = "Clear pending link" };
                clear.Click += (_, _) => { _pendingLink = null; InvalidateVisual(); };
                items.Add(clear);
            }
            else
            {
                var link2 = new MenuItem { Header = $"Link {_pendingLink.DisplayName} → {node.DisplayName}" };
                link2.Click += (_, _) => { AddLink(_pendingLink, node); _pendingLink = null; };
                items.Add(link2);
                var clear2 = new MenuItem { Header = "Clear pending link" };
                clear2.Click += (_, _) => { _pendingLink = null; InvalidateVisual(); };
                items.Add(clear2);
            }
            var info = new MenuItem { Header = $"{node.DisplayName} — {node.Element} — {node.Raw.DisplayCost} AP" };
            info.IsEnabled = false;
            items.Insert(0, info);
        }
        else if (_pendingLink is not null)
        {
            var clear = new MenuItem { Header = "Clear pending link" };
            clear.Click += (_, _) => { _pendingLink = null; InvalidateVisual(); };
            items.Add(clear);
        }

        if (items.Count == 0) return;
        foreach (var i in items) menu.Items.Add(i);
        menu.Placement = PlacementMode.Pointer;
        menu.Open(this);
    }

    private void AddLink(SkillGraphNodeVM from, SkillGraphNodeVM to)
    {
        if (from == to) return;
        if (_links.Any(l => l.From == from && l.To == to)) return;
        _links.Add((from, to));
        GraphChanged?.Invoke();
        StatusChanged?.Invoke($"Linked {from.DisplayName} → {to.DisplayName}.");
        InvalidateVisual();
    }

    private void DeleteLink((SkillGraphNodeVM From, SkillGraphNodeVM To) link)
    {
        _links.RemoveAll(l => l.From == link.From && l.To == link.To);
        GraphChanged?.Invoke();
        StatusChanged?.Invoke($"Deleted link {link.From.DisplayName} → {link.To.DisplayName}.");
        InvalidateVisual();
    }
}

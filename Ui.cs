using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CachyDualBoot;

enum Level { Ok, Warn, Bad, Info }
enum StepState { Pending, Running, Done, Failed }

/// <summary>Açık, düz, keskin köşeli klasik masaüstü teması.</summary>
static class Theme
{
    public static readonly Color Bg = Color.FromArgb(240, 240, 240);      // pencere / içerik zemini
    public static readonly Color Paper = Color.White;                     // başlık bandı
    public static readonly Color Side = Color.FromArgb(230, 230, 230);    // adım listesi
    public static readonly Color Card = Color.White;
    public static readonly Color CardHi = Color.FromArgb(229, 241, 251);  // üzerine gelme / seçili
    public static readonly Color Input = Color.White;
    public static readonly Color Track = Color.FromArgb(218, 218, 218);
    public static readonly Color Border = Color.FromArgb(173, 173, 173);
    public static readonly Color Line = Color.FromArgb(204, 204, 204);
    public static readonly Color Fg = Color.FromArgb(28, 28, 28);
    public static readonly Color Muted = Color.FromArgb(96, 96, 96);
    public static readonly Color Accent = Color.FromArgb(0, 90, 158);
    public static readonly Color Ok = Color.FromArgb(16, 124, 16);
    public static readonly Color Warn = Color.FromArgb(176, 96, 0);
    public static readonly Color Bad = Color.FromArgb(196, 43, 28);
    public static readonly Color WinColor = Color.FromArgb(0, 120, 212);
    public static readonly Color LinuxColor = Color.FromArgb(46, 160, 67);
    public static readonly Color IsoColor = Color.FromArgb(230, 126, 0);

    public static readonly Font Body = new("Segoe UI", 9.5f);
    public static readonly Font Small = new("Segoe UI", 8.5f);
    public static readonly Font Bold = new("Segoe UI Semibold", 9.5f);
    public static readonly Font H1 = new("Segoe UI Semibold", 14f);
    public static readonly Font Big = new("Segoe UI Semibold", 15f);
    public static readonly Font Mono = new("Consolas", 9.5f);

    /// <summary>true: tüm köşeler keskin (yarıçap yok sayılır).</summary>
    public static bool Sharp = true;

    static float? _scale;
    public static float Scale
    {
        get
        {
            if (_scale == null) { using var g = Graphics.FromHwnd(IntPtr.Zero); _scale = g.DpiX / 96f; }
            return _scale.Value;
        }
    }
    public static int Dp(int v) => (int)Math.Round(v * Scale);
    public static float Dp(float v) => v * Scale;

    public static Color A(Color c, int alpha) => Color.FromArgb(alpha, c);
    public static Color Mix(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    public static Color LevelColor(Level l) => l switch { Level.Ok => Ok, Level.Warn => Warn, Level.Bad => Bad, _ => Accent };

    public static void Smooth(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    public static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = Sharp ? 0 : Math.Min(rad * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>Düz dolgulu, 1 px kenarlıklı dikdörtgen (keskin: antialias kapalı, net çizgi).</summary>
    public static void FillRound(Graphics g, RectangleF r, float rad, Color fill, Color? border = null)
    {
        var ir = Rectangle.Round(r);
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;
        using (var b = new SolidBrush(fill)) g.FillRectangle(b, ir);
        if (border is Color bc) { using var pen = new Pen(bc, 1f); g.DrawRectangle(pen, ir.X, ir.Y, ir.Width - 1, ir.Height - 1); }
        g.SmoothingMode = old;
    }

    public const TextFormatFlags Left = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
    public const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
    public const TextFormatFlags Right = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
    public const TextFormatFlags Wrap = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
    public const TextFormatFlags WrapTop = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;

    public static void T(Graphics g, string s, Font f, Color c, Rectangle r, TextFormatFlags fl = Left)
        => TextRenderer.DrawText(g, s, f, r, c, fl);

    /// <summary>Durum simgesi: renkli dolu daire içinde beyaz işaret (klasik Windows iletişim simgesi gibi).</summary>
    public static void Badge(Graphics g, Level l, RectangleF r)
    {
        var c = LevelColor(l);
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(c)) g.FillEllipse(b, r);
        using var pen = new Pen(Color.White, Math.Max(2f, Dp(2f))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        PointF P(float fx, float fy) => new(r.X + r.Width * fx, r.Y + r.Height * fy);
        switch (l)
        {
            case Level.Ok:
                g.DrawLines(pen, new[] { P(.28f, .52f), P(.43f, .67f), P(.72f, .35f) });
                break;
            case Level.Bad:
                g.DrawLine(pen, P(.34f, .34f), P(.66f, .66f));
                g.DrawLine(pen, P(.66f, .34f), P(.34f, .66f));
                break;
            case Level.Warn:
                g.DrawLine(pen, P(.5f, .25f), P(.5f, .57f));
                g.DrawLine(pen, P(.5f, .74f), P(.5f, .745f));
                break;
            default:
                g.DrawLine(pen, P(.5f, .27f), P(.5f, .275f));
                g.DrawLine(pen, P(.5f, .45f), P(.5f, .74f));
                break;
        }
        g.SmoothingMode = old;
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    /// <summary>Başlık çubuğunu açık temaya sabitler (sistem koyu moddaysa bile).</summary>
    public static void LightTitleBar(IntPtr hwnd)
    {
        try { int off = 0; DwmSetWindowAttribute(hwnd, 20, ref off, 4); } catch { }
    }
}

/// <summary>Kendi çizen kontroller için ortak taban (titremesiz çizim).</summary>
abstract class Surface : Control
{
    protected Surface()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
    }
}

sealed class Card : Panel
{
    readonly Color _fill;
    public Card(Color? fill = null)
    {
        _fill = fill ?? Theme.Card;
        BackColor = _fill;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        Padding = new Padding(Theme.Dp(16));
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Bg);
        Theme.FillRound(g, new RectangleF(0, 0, Width, Height), 0, _fill, Theme.Line);
    }
}

sealed class PillButton : Surface
{
    bool _hover, _down;
    public bool Primary { get; set; }
    public Color? Tint { get; set; }

    public PillButton()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        Font = Theme.Body;
        Height = Theme.Dp(32);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Focus(); Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override bool IsInputKey(Keys k) => k == Keys.Enter || base.IsInputKey(k);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Enabled && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)) { OnClick(EventArgs.Empty); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        Color fill, fg, border;
        if (!Enabled) { fill = Color.FromArgb(225, 225, 225); fg = Color.FromArgb(135, 135, 135); border = Color.FromArgb(200, 200, 200); }
        else if (Primary)
        {
            var baseC = Tint ?? Theme.Accent;
            fill = _down ? Theme.Mix(baseC, Color.Black, .25f) : _hover ? Theme.Mix(baseC, Color.White, .15f) : baseC;
            border = Theme.Mix(baseC, Color.Black, .3f);
            fg = Color.White;
        }
        else
        {
            fill = _down ? Color.FromArgb(204, 228, 247) : _hover ? Theme.CardHi : Color.FromArgb(253, 253, 253);
            border = _hover || _down ? Theme.Accent : Theme.Border;
            fg = Theme.Fg;
        }
        Theme.FillRound(g, new RectangleF(0, 0, Width, Height), 0, fill, border);
        if (Focused && Enabled)
        {
            using var pen = new Pen(Primary ? Color.White : Theme.Fg) { DashStyle = DashStyle.Dot };
            g.DrawRectangle(pen, 3, 3, Width - 7, Height - 7);
        }
        Theme.T(g, Text, Font, fg, new Rectangle(0, 0, Width, Height), Theme.Center);
    }
}

/// <summary>Küçük durum etiketi.</summary>
sealed class Chip : Surface
{
    Level _lvl = Level.Info;
    public void Set(string text, Level lvl)
    {
        Text = text; _lvl = lvl;
        Width = TextRenderer.MeasureText(text, Theme.Small).Width + Theme.Dp(20);
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor);
        var c = Theme.LevelColor(_lvl);
        int h = Theme.Dp(26);
        var r = new Rectangle(0, (Height - h) / 2, Width, h);
        Theme.FillRound(g, r, 0, Theme.Mix(Color.White, c, .10f), c);
        Theme.T(g, Text, Theme.Small, c, r, Theme.Center);
    }
}

/// <summary>Klasik onay kutusu (kare kutu + onay işareti).</summary>
sealed class Toggle : Surface
{
    bool _checked, _hover;
    public Color OnColor { get; set; } = Theme.Accent;
    public event EventHandler? CheckedChanged;

    public Toggle() { SetStyle(ControlStyles.Selectable, true); TabStop = true; Cursor = Cursors.Hand; Font = Theme.Body; }

    public bool Checked
    {
        get => _checked;
        set { if (_checked == value) return; _checked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }
    protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor);
        int s = Theme.Dp(18);
        var box = new Rectangle(1, (Height - s) / 2, s, s);
        Theme.FillRound(g, box, 0, _checked ? OnColor : Color.White, _checked ? OnColor : _hover ? Theme.Accent : Theme.Border);
        if (_checked)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.White, Math.Max(2f, Theme.Dp(2f))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pen, new[] {
                new PointF(box.X + s * .24f, box.Y + s * .54f), new PointF(box.X + s * .43f, box.Y + s * .72f), new PointF(box.X + s * .78f, box.Y + s * .30f) });
        }
        int x = s + Theme.Dp(10);
        var tr = new Rectangle(x, 0, Width - x, Height);
        Theme.T(g, Text, Font, Theme.Fg, tr, Theme.Wrap);
        if (Focused)
        {
            using var pen = new Pen(Theme.Fg) { DashStyle = DashStyle.Dot };
            g.SmoothingMode = SmoothingMode.None;
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}

sealed class Slider : Surface
{
    int _min = 0, _max = 100, _val;
    bool _drag;
    public event EventHandler? ValueChanged;

    public Slider() { SetStyle(ControlStyles.Selectable, true); TabStop = true; Cursor = Cursors.Hand; }

    public int Minimum { get => _min; set { _min = value; Value = _val; Invalidate(); } }
    public int Maximum { get => _max; set { _max = Math.Max(value, _min); Value = _val; Invalidate(); } }
    public int Value
    {
        get => _val;
        set
        {
            int v = Math.Clamp(value, _min, _max);
            if (v == _val) { Invalidate(); return; }
            _val = v; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    int Pad => Theme.Dp(8);
    float Frac => _max == _min ? 0 : (_val - _min) / (float)(_max - _min);
    void SetFromX(int x)
    {
        float t = Math.Clamp((x - Pad) / (float)Math.Max(1, Width - Pad * 2), 0f, 1f);
        Value = _min + (int)Math.Round(t * (_max - _min));
    }

    protected override void OnMouseDown(MouseEventArgs e) { Focus(); _drag = true; Capture = true; SetFromX(e.X); base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (_drag) SetFromX(e.X); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _drag = false; Capture = false; base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override bool IsInputKey(Keys k) => k is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(k);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Left or Keys.Down: Value -= 1; e.Handled = true; break;
            case Keys.Right or Keys.Up: Value += 1; e.Handled = true; break;
            case Keys.PageDown: Value -= 10; e.Handled = true; break;
            case Keys.PageUp: Value += 10; e.Handled = true; break;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor);
        int cy = Height / 2, th = Theme.Dp(4);
        int x0 = Pad, x1 = Width - Pad;
        Theme.FillRound(g, new Rectangle(x0, cy - th / 2, x1 - x0, th), 0, Theme.Track, Theme.Line);
        int tx = x0 + (int)((x1 - x0) * Frac);
        if (tx > x0) Theme.FillRound(g, new Rectangle(x0, cy - th / 2, tx - x0, th), 0, Theme.Accent);
        // klasik dikdörtgen tutamaç
        int tw = Theme.Dp(10), tH = Theme.Dp(22);
        Theme.FillRound(g, new Rectangle(tx - tw / 2, cy - tH / 2, tw, tH), 0,
            _drag ? Theme.Mix(Theme.Accent, Color.Black, .2f) : Theme.Accent, Theme.Mix(Theme.Accent, Color.Black, .3f));
        if (Focused)
        {
            using var pen = new Pen(Theme.Fg) { DashStyle = DashStyle.Dot };
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}

sealed class Segmented : Surface
{
    string[] _items = Array.Empty<string>();
    int _sel;
    public event EventHandler? SelectedChanged;

    public Segmented() { SetStyle(ControlStyles.Selectable, true); TabStop = true; Cursor = Cursors.Hand; Font = Theme.Body; Height = Theme.Dp(32); }

    public string[] Items { get => _items; set { _items = value; Invalidate(); } }
    public int SelectedIndex
    {
        get => _sel;
        set { if (value < 0 || value >= _items.Length || value == _sel) return; _sel = value; Invalidate(); SelectedChanged?.Invoke(this, EventArgs.Empty); }
    }
    public string Selected => _items.Length == 0 ? "" : _items[_sel];

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (_items.Length > 0) SelectedIndex = Math.Clamp(e.X * _items.Length / Math.Max(1, Width), 0, _items.Length - 1);
        base.OnMouseDown(e);
    }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override bool IsInputKey(Keys k) => k is Keys.Left or Keys.Right || base.IsInputKey(k);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Left) { SelectedIndex = _sel - 1; e.Handled = true; }
        else if (e.KeyCode == Keys.Right) { SelectedIndex = _sel + 1; e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor);
        if (_items.Length == 0) return;
        int w = Width / _items.Length;
        for (int i = 0; i < _items.Length; i++)
        {
            int x = i * w, ww = i == _items.Length - 1 ? Width - x : w;
            var r = new Rectangle(x, 0, ww, Height);
            bool sel = i == _sel;
            Theme.FillRound(g, new Rectangle(x, 0, ww + (i < _items.Length - 1 ? 1 : 0), Height), 0,
                sel ? Theme.Accent : Color.White, sel ? Theme.Mix(Theme.Accent, Color.Black, .3f) : Theme.Border);
            Theme.T(g, _items[i], sel ? Theme.Bold : Font, sel ? Color.White : Theme.Fg, r, Theme.Center);
        }
        if (Focused)
        {
            using var pen = new Pen(Color.White) { DashStyle = DashStyle.Dot };
            var fr = new Rectangle(_sel * w + 3, 3, (_sel == _items.Length - 1 ? Width - _sel * w : w) - 7, Height - 7);
            g.DrawRectangle(pen, fr);
        }
    }
}

/// <summary>Diskin bölümlere ayrılışını oransal çubuk + açıklama olarak gösterir.</summary>
sealed class DiskBar : Surface
{
    readonly List<(string Label, long Bytes, Color Color)> _segs = new();
    public string Empty { get; set; } = "";

    public void Set(params (string Label, long Bytes, Color Color)[] segs)
    {
        _segs.Clear(); _segs.AddRange(segs); Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor);
        int bh = Theme.Dp(24);
        var bar = new Rectangle(0, 0, Width, bh);
        long total = _segs.Sum(s => Math.Max(0, s.Bytes));
        if (total <= 0)
        {
            Theme.FillRound(g, bar, 0, Theme.Input, Theme.Border);
            if (Empty.Length > 0) Theme.T(g, Empty, Theme.Small, Theme.Muted, bar, Theme.Center);
            return;
        }
        float x = 0, minW = Theme.Dp(4f);
        var vis = _segs.Where(s => s.Bytes > 0).ToList();
        for (int i = 0; i < vis.Count; i++)
        {
            float w = Math.Max(minW, (float)vis[i].Bytes / total * Width);
            if (i == vis.Count - 1) w = Width - x;
            using var b = new SolidBrush(vis[i].Color);
            g.FillRectangle(b, (int)x, 0, (int)Math.Ceiling(w), bh);
            if (i > 0) { using var sep = new Pen(Color.White); g.DrawLine(sep, (int)x, 0, (int)x, bh); }
            x += w;
        }
        using (var pen = new Pen(Theme.Border)) g.DrawRectangle(pen, 0, 0, Width - 1, bh - 1);

        // açıklama
        int lx = 0, ly = bh + Theme.Dp(12), lh = Theme.Dp(20);
        foreach (var s in _segs)
        {
            string text = $"{s.Label}  {s.Bytes / 1073741824.0:F0} GB";
            int tw = TextRenderer.MeasureText(text, Theme.Small).Width;
            int need = Theme.Dp(18) + tw + Theme.Dp(22);
            if (lx > 0 && lx + need > Width) { lx = 0; ly += lh; }
            using (var b = new SolidBrush(s.Color)) g.FillRectangle(b, lx, ly + (lh - Theme.Dp(10)) / 2, Theme.Dp(10), Theme.Dp(10));
            Theme.T(g, text, Theme.Small, Theme.Fg, new Rectangle(lx + Theme.Dp(16), ly, tw + 4, lh));
            lx += need;
        }
    }
}

sealed class StatTile : Surface
{
    string _caption = "", _value = "—", _sub = "";
    public void Set(string caption, string value, string sub = "") { _caption = caption; _value = value; _sub = sub; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Theme.Bg);
        Theme.FillRound(g, new RectangleF(0, 0, Width, Height), 0, Theme.Card, Theme.Line);
        int p = Theme.Dp(14);
        Theme.T(g, _caption, Theme.Small, Theme.Muted, new Rectangle(p, Theme.Dp(9), Width - p * 2, Theme.Dp(18)));
        Theme.T(g, _value, Theme.Big, Theme.Fg, new Rectangle(p, Theme.Dp(26), Width - p * 2, Theme.Dp(30)));
        if (_sub.Length > 0) Theme.T(g, _sub, Theme.Small, Theme.Muted, new Rectangle(p, Theme.Dp(58), Width - p * 2, Theme.Dp(16)));
    }
}

sealed class StatusRow : Surface
{
    Level _lvl = Level.Info;
    string _title = "", _value = "", _detail = "";
    public bool Separator { get; set; } = true;

    public void Set(Level lvl, string title, string value, string detail)
    {
        _lvl = lvl; _title = title; _value = value; _detail = detail; Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor);
        int d = Theme.Dp(24);
        Theme.Badge(g, _lvl, new RectangleF(2, (Height - d) / 2f, d, d));
        int tx = d + Theme.Dp(16);
        int vw = _value.Length == 0 ? 0 : TextRenderer.MeasureText(_value, Theme.Bold).Width + Theme.Dp(8);
        Theme.T(g, _title, Theme.Bold, Theme.Fg, new Rectangle(tx, Theme.Dp(6), Math.Max(10, Width - tx - vw - Theme.Dp(8)), Theme.Dp(22)));
        Theme.T(g, _detail, Theme.Small, Theme.Muted, new Rectangle(tx, Theme.Dp(28), Math.Max(10, Width - tx), Theme.Dp(20)));
        if (vw > 0) Theme.T(g, _value, Theme.Bold, Theme.LevelColor(_lvl), new Rectangle(Width - vw, Theme.Dp(6), vw, Theme.Dp(22)), Theme.Right);
        if (Separator)
        {
            using var pen = new Pen(Theme.Line);
            g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }
}

sealed class Banner : Surface
{
    Level _lvl = Level.Info;
    public void Set(Level lvl, string text) { _lvl = lvl; Text = text; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor);
        var c = Theme.LevelColor(_lvl);
        Theme.FillRound(g, new RectangleF(0, 0, Width, Height), 0, Theme.Mix(Color.White, c, .10f), c);
        int d = Theme.Dp(22), p = Theme.Dp(12);
        Theme.Badge(g, _lvl, new RectangleF(p, (Height - d) / 2f, d, d));
        int tx = p + d + Theme.Dp(12);
        Theme.T(g, Text, Theme.Body, Theme.Fg, new Rectangle(tx, 2, Width - tx - p, Height - 4), Theme.Wrap);
    }
}

/// <summary>Tıklanabilir dosya seçme alanı.</summary>
sealed class DropZone : Surface
{
    bool _hover;
    string? _name; string _detail = "";
    Level? _state;
    public event EventHandler? BrowseRequested;

    public DropZone() { SetStyle(ControlStyles.Selectable, true); TabStop = true; Cursor = Cursors.Hand; }

    public void Set(string? name, string detail, Level? state)
    {
        _name = name; _detail = detail; _state = state; Invalidate();
    }
    public void SetState(Level? state, string detail) { _state = state; _detail = detail; Invalidate(); }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnClick(EventArgs e) { base.OnClick(e); BrowseRequested?.Invoke(this, EventArgs.Empty); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter) { BrowseRequested?.Invoke(this, EventArgs.Empty); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor);
        var edge = _state is Level l ? Theme.LevelColor(l) : (_hover || Focused ? Theme.Accent : Theme.Border);
        Theme.FillRound(g, new RectangleF(0, 0, Width, Height), 0, _hover ? Theme.CardHi : Theme.Card, edge);

        // sol: durum / dosya simgesi alanı
        int iw = Theme.Dp(72);
        using (var b = new SolidBrush(Theme.Side)) g.FillRectangle(b, 1, 1, iw - 1, Height - 2);
        using (var pen = new Pen(Theme.Line)) g.DrawLine(pen, iw, 1, iw, Height - 2);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var col = _state is Level l3 ? Theme.LevelColor(l3) : Theme.Accent;
        using (var pen = new Pen(col, Theme.Dp(2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            float cx = iw / 2f, cy = Height / 2f, s = Theme.Dp(10f);
            if (_name == null)
            {   // klasör simgesi
                g.DrawLines(pen, new[] { new PointF(cx - s * 1.5f, cy - s), new PointF(cx - s * .5f, cy - s), new PointF(cx, cy - s * .5f),
                    new PointF(cx + s * 1.5f, cy - s * .5f), new PointF(cx + s * 1.5f, cy + s), new PointF(cx - s * 1.5f, cy + s), new PointF(cx - s * 1.5f, cy - s) });
            }
            else
            {   // disk simgesi
                g.DrawEllipse(pen, cx - s * 1.2f, cy - s * 1.2f, s * 2.4f, s * 2.4f);
                g.DrawEllipse(pen, cx - s * .35f, cy - s * .35f, s * .7f, s * .7f);
            }
        }

        int tx = iw + Theme.Dp(18);
        int tw = Width - tx - Theme.Dp(16);
        if (_name == null)
        {
            Theme.T(g, "ISO dosyası seçilmedi", Theme.Bold, Theme.Fg, new Rectangle(tx, Height / 2 - Theme.Dp(22), tw, Theme.Dp(22)));
            Theme.T(g, "Seçmek için tıklayın. UEFI ile açılabilen bir .iso dosyası gerekir (ör. CachyOS, EndeavourOS, Arch).", Theme.Small, Theme.Muted, new Rectangle(tx, Height / 2 + Theme.Dp(2), tw, Theme.Dp(20)));
        }
        else
        {
            Theme.T(g, _name, Theme.Bold, Theme.Fg, new Rectangle(tx, Height / 2 - Theme.Dp(24), tw - Theme.Dp(90), Theme.Dp(22)));
            Theme.T(g, _detail, Theme.Small, Theme.Muted, new Rectangle(tx, Height / 2 + Theme.Dp(2), tw, Theme.Dp(20)));
            Theme.T(g, "Değiştir…", Theme.Small, Theme.Accent, new Rectangle(Width - Theme.Dp(100), Theme.Dp(10), Theme.Dp(84), Theme.Dp(20)), Theme.Right);
        }
    }
}

sealed class ProgressLine : Surface
{
    int _value; bool _ind; float _pos;
    readonly System.Windows.Forms.Timer _t = new() { Interval = 20 };

    public ProgressLine() { _t.Tick += (_, _) => { _pos = (_pos + .018f) % 1.4f; Invalidate(); }; }

    public int Value { get => _value; set { _value = Math.Clamp(value, 0, 100); Invalidate(); } }
    public bool Indeterminate { get => _ind; set { _ind = value; _t.Enabled = value; Invalidate(); } }

    protected override void Dispose(bool disposing) { if (disposing) _t.Dispose(); base.Dispose(disposing); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor);
        int h = Math.Min(Height, Theme.Dp(8)), y = (Height - h) / 2;
        Theme.FillRound(g, new Rectangle(0, y, Width, h), 0, Theme.Track, Theme.Line);
        if (_ind)
        {
            int bw = (int)(Width * .3f), x = (int)((_pos - .3f) * Width);
            var state = g.Save();
            g.SetClip(new Rectangle(1, y + 1, Width - 2, h - 2));
            Theme.FillRound(g, new Rectangle(x, y + 1, bw, h - 2), 0, Theme.Accent);
            g.Restore(state);
        }
        else if (_value > 0)
            Theme.FillRound(g, new Rectangle(1, y + 1, Math.Max(1, (Width - 2) * _value / 100), h - 2), 0, Theme.Accent);
    }
}

sealed class StepList : Surface
{
    string[] _items = Array.Empty<string>();
    StepState[] _st = Array.Empty<StepState>();
    float _ang;
    readonly System.Windows.Forms.Timer _t = new() { Interval = 30 };

    public StepList() { _t.Tick += (_, _) => { _ang = (_ang + 14) % 360; Invalidate(); }; }
    protected override void Dispose(bool disposing) { if (disposing) _t.Dispose(); base.Dispose(disposing); }

    public void SetItems(IEnumerable<string> items)
    {
        _items = items.ToArray(); _st = new StepState[_items.Length]; Sync();
    }
    public void Set(int i, StepState s) { if (i >= 0 && i < _st.Length) { _st[i] = s; Sync(); } }
    public void FailRunning() { for (int i = 0; i < _st.Length; i++) if (_st[i] == StepState.Running) _st[i] = StepState.Failed; Sync(); }
    void Sync() { _t.Enabled = _st.Contains(StepState.Running); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor); Theme.Smooth(g);
        int rh = Theme.Dp(30), d = Theme.Dp(18);
        for (int i = 0; i < _items.Length; i++)
        {
            int y = i * rh;
            var ic = new RectangleF(1, y + (rh - d) / 2f, d, d);
            switch (_st[i])
            {
                case StepState.Done: Theme.Badge(g, Level.Ok, ic); break;
                case StepState.Failed: Theme.Badge(g, Level.Bad, ic); break;
                case StepState.Running:
                    using (var pen = new Pen(Theme.Track, 2.2f)) g.DrawEllipse(pen, ic.X + 1, ic.Y + 1, d - 2, d - 2);
                    using (var pen = new Pen(Theme.Accent, 2.4f) { StartCap = LineCap.Flat, EndCap = LineCap.Flat }) g.DrawArc(pen, ic.X + 1, ic.Y + 1, d - 2, d - 2, _ang, 100);
                    break;
                default:
                    using (var pen = new Pen(Theme.Border, 1.4f)) g.DrawEllipse(pen, ic.X + 1, ic.Y + 1, d - 2, d - 2);
                    break;
            }
            var col = _st[i] switch { StepState.Pending => Theme.Muted, StepState.Failed => Theme.Bad, _ => Theme.Fg };
            Theme.T(g, _items[i], Theme.Body, col, new Rectangle(d + Theme.Dp(12), y, Width - d - Theme.Dp(12), rh));
        }
    }
}

sealed class Field : Surface
{
    public readonly TextBox Box = new()
    {
        BorderStyle = BorderStyle.None, BackColor = Theme.Input, ForeColor = Theme.Fg, Font = Theme.Body,
    };

    public Field()
    {
        Height = Theme.Dp(30);
        Controls.Add(Box);
        Box.GotFocus += (_, _) => Invalidate();
        Box.LostFocus += (_, _) => Invalidate();
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text { get => Box.Text; set => Box.Text = value ?? ""; }
    public string Placeholder { get => Box.PlaceholderText; set => Box.PlaceholderText = value; }
    public bool ReadOnly
    {
        get => Box.ReadOnly;
        set { Box.ReadOnly = value; Box.TabStop = !value; Box.BackColor = value ? Theme.Bg : Theme.Input; Invalidate(); }
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int pad = Theme.Dp(8);
        Box.SetBounds(pad, (Height - Box.Height) / 2, Math.Max(10, Width - pad * 2), Box.Height);
    }
    protected override void OnMouseDown(MouseEventArgs e) { Box.Focus(); base.OnMouseDown(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor);
        Theme.FillRound(g, new RectangleF(0, 0, Width, Height), 0, Box.ReadOnly ? Theme.Bg : Theme.Input, Box.Focused ? Theme.Accent : Theme.Border);
    }
}

/// <summary>Açık zeminli, renkli satır ekleyen günlük kutusu.</summary>
class DarkRich : RichTextBox
{
    public DarkRich()
    {
        ReadOnly = true; BorderStyle = BorderStyle.None; BackColor = Theme.Input; ForeColor = Theme.Fg;
        Font = Theme.Mono; DetectUrls = false; HideSelection = false; TabStop = false; ScrollBars = RichTextBoxScrollBars.Vertical;
    }

    protected void Put(string text, Color c, Font? f = null)
    {
        SelectionStart = TextLength; SelectionLength = 0;
        SelectionColor = c; SelectionFont = f ?? Font;
        SelectedText = text;
    }
}

sealed class LogBox : DarkRich
{
    public void Add(string text)
    {
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var t = raw.TrimStart();
            Color c =
                t.StartsWith('✔') ? Theme.Ok :
                t.StartsWith('✖') || t.StartsWith("HATA") || t.StartsWith("Hata") ? Theme.Bad :
                t.StartsWith('⚠') ? Theme.Warn :
                t.StartsWith("===") ? Theme.Accent :
                t.StartsWith('ℹ') || t.StartsWith("[deneme]") ? Theme.Muted : Theme.Fg;
            Put(raw + "\n", c);
        }
        ScrollToCaret();
    }
    public void SetText(string text) { Clear(); if (text.Length > 0) Add(text.TrimEnd('\r', '\n')); }
}

sealed class DocBox : DarkRich
{
    static readonly Font Body = new("Segoe UI", 10f);
    static readonly Font Head = new("Segoe UI Semibold", 11.5f);

    public DocBox() { Font = Body; }

    public void SetGuide(string text)
    {
        Clear();
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var t = raw.TrimStart();
            if (t.StartsWith("ADIM")) Put(raw + "\n", Theme.Accent, Head);
            else if (t.StartsWith("Geri alma")) Put(raw + "\n", Theme.Warn, Body);
            else if (t.Contains("KESİNLİKLE") || t.Contains("KULLANMAYIN") || t.Contains("DURUN") || t.Contains("EN KRİTİK")) Put(raw + "\n", Theme.Warn, Body);
            else Put(raw + "\n", t.StartsWith('(') ? Theme.Muted : Theme.Fg, Body);
        }
        SelectionStart = 0; SelectionLength = 0; ScrollToCaret();
    }
}

/// <summary>Sol adım listesi: düz, kare numaralı, seçili satırda mavi şerit.</summary>
sealed class Sidebar : Surface
{
    public string[] Titles { get; set; } = Array.Empty<string>();
    public string[] Subs { get; set; } = Array.Empty<string>();
    public string Footer { get; set; } = "";
    public Func<int, bool>? CanNavigate { get; set; }
    public event Action<int>? StepClicked;
    int _cur, _hot = -1;

    public Sidebar() { BackColor = Theme.Side; }

    public int Current { get => _cur; set { _cur = value; Invalidate(); } }

    int ItemH => Theme.Dp(54);
    int Top0 => Theme.Dp(84);
    Rectangle ItemRect(int i) => new(0, Top0 + i * ItemH, Width - 1, ItemH);
    int Hit(Point p) { for (int i = 0; i < Titles.Length; i++) if (ItemRect(i).Contains(p)) return i; return -1; }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = Hit(e.Location);
        if (h != _hot) { _hot = h; Invalidate(); }
        Cursor = h >= 0 && CanNavigate?.Invoke(h) == true ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(EventArgs e) { _hot = -1; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        int h = Hit(e.Location);
        if (h >= 0 && CanNavigate?.Invoke(h) == true) StepClicked?.Invoke(h);
        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Theme.Side);

        // başlık
        Theme.T(g, "Linux Dual Boot", Theme.Bold, Theme.Fg, new Rectangle(Theme.Dp(18), Theme.Dp(18), Width - Theme.Dp(36), Theme.Dp(22)));
        Theme.T(g, "Kurulum Sihirbazı", Theme.Small, Theme.Muted, new Rectangle(Theme.Dp(18), Theme.Dp(40), Width - Theme.Dp(36), Theme.Dp(20)));
        using (var pen = new Pen(Theme.Line)) g.DrawLine(pen, Theme.Dp(18), Theme.Dp(72), Width - Theme.Dp(18), Theme.Dp(72));

        // adımlar
        int bs = Theme.Dp(24);
        for (int i = 0; i < Titles.Length; i++)
        {
            var r = ItemRect(i);
            bool cur = i == _cur, done = i < _cur;
            if (cur)
            {
                Theme.FillRound(g, r, 0, Theme.Paper);
                using var bar = new SolidBrush(Theme.Accent);
                g.FillRectangle(bar, r.X, r.Y, Theme.Dp(4), r.Height);
            }
            else if (i == _hot && CanNavigate?.Invoke(i) == true) Theme.FillRound(g, r, 0, Theme.CardHi);

            var box = new Rectangle(r.X + Theme.Dp(18), r.Y + (r.Height - bs) / 2, bs, bs);
            if (done)
            {
                Theme.FillRound(g, box, 0, Theme.Ok);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(Color.White, Math.Max(2f, Theme.Dp(2f))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLines(pen, new[] { new PointF(box.X + bs * .24f, box.Y + bs * .53f), new PointF(box.X + bs * .43f, box.Y + bs * .71f), new PointF(box.X + bs * .77f, box.Y + bs * .31f) });
                g.SmoothingMode = SmoothingMode.None;
            }
            else if (cur)
            {
                Theme.FillRound(g, box, 0, Theme.Accent);
                Theme.T(g, (i + 1).ToString(), Theme.Bold, Color.White, box, Theme.Center);
            }
            else
            {
                Theme.FillRound(g, box, 0, Theme.Side, Theme.Border);
                Theme.T(g, (i + 1).ToString(), Theme.Body, Theme.Muted, box, Theme.Center);
            }

            int x = box.Right + Theme.Dp(12);
            Theme.T(g, Titles[i], cur ? Theme.Bold : Theme.Body, cur || done ? Theme.Fg : Theme.Muted, new Rectangle(x, r.Y + Theme.Dp(8), r.Right - x, Theme.Dp(20)));
            if (i < Subs.Length) Theme.T(g, Subs[i], Theme.Small, Theme.Muted, new Rectangle(x, r.Y + Theme.Dp(27), r.Right - x, Theme.Dp(18)));
        }

        if (Footer.Length > 0)
            Theme.T(g, Footer, Theme.Small, Theme.Muted, new Rectangle(Theme.Dp(18), Height - Theme.Dp(34), Width - Theme.Dp(36), Theme.Dp(20)));

        using (var pen = new Pen(Theme.Line)) g.DrawLine(pen, Width - 1, 0, Width - 1, Height);
    }
}

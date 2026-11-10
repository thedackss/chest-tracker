using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using L = ChestTracker.Text;

namespace ChestTracker;

/// <summary>What the overlay shows.</summary>
public sealed record OverlayState(Totals Totals, bool InMatch, bool Locked);

/// <summary>
/// Always-on-top HUD: outlined figures with per-pixel transparency (UpdateLayeredWindow), click-through when locked,
/// draggable when unlocked. Fortnite must be in borderless/windowed fullscreen for it to show over the game.
/// </summary>
public sealed class OverlayForm : Form
{
    private const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_TOPMOST = 0x8, WS_EX_NOACTIVATE = 0x8000000;
    private const int GWL_EXSTYLE = -20, WM_NCHITTEST = 0x84, HTCAPTION = 2, WM_EXITSIZEMOVE = 0x232;
    private const int SW_HIDE = 0, SW_SHOWNOACTIVATE = 4;

    private OverlayState? _state;
    private float _scale = 1f;

    /// <summary>Raised after the user dragged the overlay (new top-left corner).</summary>
    public event Action<Point>? Moved;

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        // Topmost comes from WS_EX_TOPMOST in CreateParams. Form.TopMost would also call SetWindowPos when the window
        // is created, which takes the focus from the active window (the tray panel, or the game).
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    /// <summary>On screen (shown with <see cref="ShowQuietly"/>).</summary>
    public bool IsOnScreen { get; private set; }

    /// <summary>
    /// Shows the overlay without touching the focus. Form.Show() deactivates the active window even with
    /// ShowWithoutActivation: in game that is Fortnite, otherwise the tray panel (which then closes).
    /// </summary>
    public void ShowQuietly()
    {
        ShowWindow(Handle, SW_SHOWNOACTIVATE); // Handle creates the window the first time
        IsOnScreen = true;
    }

    public void HideQuietly()
    {
        if (IsHandleCreated) ShowWindow(Handle, SW_HIDE);
        IsOnScreen = false;
    }

    /// <param name="sizePercent">User scale (Settings.OverlaySize), on top of the monitor DPI.</param>
    public void UpdateState(OverlayState state, int sizePercent)
    {
        _state = state;
        _scale = DeviceDpi / 96f * Math.Clamp(sizePercent, 50, 300) / 100f;
        SetClickThrough(state.Locked);
        if (IsHandleCreated)
        {
            Render();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_state != null)
        {
            SetClickThrough(_state.Locked);
            Render();
        }
    }

    private void SetClickThrough(bool locked)
    {
        if (!IsHandleCreated) return;
        var ex = GetWindowLong(Handle, GWL_EXSTYLE);
        var wanted = locked ? ex | WS_EX_TRANSPARENT : ex & ~WS_EX_TRANSPARENT;
        if (wanted != ex) SetWindowLong(Handle, GWL_EXSTYLE, wanted);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST && _state is { Locked: false })
        {
            m.Result = HTCAPTION; // drag from anywhere
            return;
        }
        base.WndProc(ref m);
        if (m.Msg == WM_EXITSIZEMOVE)
        {
            Moved?.Invoke(Location);
        }
    }

    /// <summary>Draws the HUD into a bitmap (also used for previews) .</summary>
    public static Bitmap Draw(OverlayState state, float scale)
    {
        int S(float v) => (int)Math.Round(v * scale);
        var t = state.Totals;
        using var bigFont = new Font("Bahnschrift SemiBold Condensed", 30 * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var smallFont = new Font("Bahnschrift SemiBold SemiConden", 15 * scale, FontStyle.Regular, GraphicsUnit.Pixel);

        var labelColor = Color.FromArgb(255, 233, 166);
        var rows = new List<(string Label, string Value, Color Color)>
        {
            (L.T("Quest", "Misión"), L.N(t.Quest), Color.White),
            state.InMatch
                ? (L.T("In match", "En partida"), "…", Color.FromArgb(255, 120, 110))
                : (L.T("Last", "Última"), t.Last != null ? L.N(t.Last.ChestsTotal) : "—", Color.White),
            (L.T("Session", "Sesión"), L.N(t.Session), Color.White),
            ("★", L.N(t.Treasures), Color.FromArgb(214, 190, 255)),
        };
        const string Sep = "  •  ";
        var gap = S(5);

        // Every width comes from the glyph outlines actually drawn, so pieces line up exactly.
        var bigW = TextWidth(L.N(t.Real), bigFont);
        var rowsW = rows.Max(r => (r.Label == "★" ? TextWidth("★", smallFont) : TextWidth(r.Label + Sep, smallFont)) + gap + TextWidth(r.Value, smallFont));
        var width = (int)Math.Ceiling(Math.Max(bigW + S(34), rowsW)) + S(24);
        var height = S(10) + bigFont.Height + rows.Count * (smallFont.Height + S(1)) + S(10);
        if (!state.Locked)
        {
            height += smallFont.Height;
            width = Math.Max(width, S(190));
        }

        var bmp = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            if (!state.Locked)
            {
                // Visible frame so it can be grabbed and moved.
                using var bg = new SolidBrush(Color.FromArgb(150, 10, 12, 20));
                using var border = new Pen(Color.FromArgb(200, 255, 205, 70), Math.Max(1, S(1.5f))) { DashStyle = DashStyle.Dash };
                g.FillRectangle(bg, 0, 0, width - 1, height - 1);
                g.DrawRectangle(border, 0, 0, width - 1, height - 1);
            }

            var right = width - S(10);
            var y = (float)S(8);

            // Big total, right-aligned, with the chest on its left.
            var totalLeft = Outlined(g, L.N(t.Real), bigFont, right, y, state.InMatch ? ChestArt.Gold : Color.White, 3.5f * scale);
            var chestW = S(26);
            ChestArt.Draw(g, new RectangleF(totalLeft - chestW - S(6), y + (bigFont.Height - S(22)) / 2f, chestW, S(22)));
            y += bigFont.Height;

            foreach (var (label, value, color) in rows)
            {
                var valueLeft = Outlined(g, value, smallFont, right, y, color, 2.5f * scale);
                Outlined(g, label == "★" ? "★" : label + Sep.TrimEnd(), smallFont, valueLeft - gap, y, label == "★" ? color : labelColor, 2.5f * scale);
                y += smallFont.Height + S(1);
            }

            if (!state.Locked)
            {
                var hint = L.T("drag me · Ctrl+Shift+L to lock", "arrástrame · Ctrl+Shift+L para fijar");
                using var hintFont = new Font("Segoe UI", 11 * scale, GraphicsUnit.Pixel);
                using var hintBrush = new SolidBrush(Color.FromArgb(220, 220, 220, 230));
                var hw = g.MeasureString(hint, hintFont).Width;
                g.DrawString(hint, hintFont, hintBrush, right - hw, y + S(2));
            }
        }
        return bmp;
    }

    private static GraphicsPath TextPath(string text, Font font)
    {
        // Winding fill: Bahnschrift (a variable font) builds glyphs from overlapping pieces. With the default
        // alternate fill the overlaps stay empty and the black outline shows through (marks in 4, 6, 9, t).
        var path = new GraphicsPath(FillMode.Winding);
        path.AddString(text, font.FontFamily, (int)font.Style, font.Size, PointF.Empty, StringFormat.GenericTypographic);
        return path;
    }

    private static float TextWidth(string text, Font font)
    {
        using var path = TextPath(text, font);
        return path.GetBounds().Width;
    }

    /// <summary>Text with a dark outline like the game's own HUD, its right edge at <paramref name="right"/>.</summary>
    /// <returns>The left edge of the drawn text.</returns>
    private static float Outlined(Graphics g, string text, Font font, float right, float y, Color color, float outline)
    {
        using var path = TextPath(text, font);
        var bounds = path.GetBounds();
        using (var move = new Matrix())
        {
            move.Translate(right - bounds.Right, y);
            path.Transform(move);
        }
        using var pen = new Pen(Color.FromArgb(230, 0, 0, 0), outline) { LineJoin = LineJoin.Round };
        g.DrawPath(pen, path);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
        return right - bounds.Width;
    }

    private void Render()
    {
        if (_state == null) return;
        using var bmp = Draw(_state, _scale);
        Size = bmp.Size;
        var screenDc = GetDC(IntPtr.Zero);
        var memDc = CreateCompatibleDC(screenDc);
        var hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
        var old = SelectObject(memDc, hBitmap);
        try
        {
            var size = new SIZE(bmp.Width, bmp.Height);
            var src = new POINT(0, 0);
            var dst = new POINT(Left, Top);
            var blend = new BLENDFUNCTION { BlendOp = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
            UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, 2 /* ULW_ALPHA */);
        }
        finally
        {
            SelectObject(memDc, old);
            DeleteObject(hBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int Cx, Cy; public SIZE(int cx, int cy) { Cx = cx; Cy = cy; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
}

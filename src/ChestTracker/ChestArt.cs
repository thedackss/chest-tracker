using System.Drawing.Drawing2D;

namespace ChestTracker;

/// <summary>The chest drawn in the overlay, tray and report (no image files needed).</summary>
public static class ChestArt
{
    public static readonly Color Gold = Color.FromArgb(255, 205, 70);
    public static readonly Color GoldDark = Color.FromArgb(214, 150, 30);
    public static readonly Color Wood = Color.FromArgb(140, 82, 40);
    public static readonly Color Ink = Color.FromArgb(28, 20, 12);

    public static void Draw(Graphics g, RectangleF r)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var stroke = Math.Max(1f, r.Width / 14f);
        var lidH = r.Height * 0.42f;
        var lid = new RectangleF(r.X, r.Y, r.Width, lidH);
        var body = new RectangleF(r.X, r.Y + lidH, r.Width, r.Height - lidH);

        using var ink = new Pen(Ink, stroke) { LineJoin = LineJoin.Round };
        using var lidPath = Rounded(lid, r.Width * 0.22f, top: true);
        using var bodyPath = Rounded(body, r.Width * 0.08f, top: false);
        using (var wood = new SolidBrush(Wood)) g.FillPath(wood, bodyPath);
        using (var lidBrush = new LinearGradientBrush(lid, Gold, GoldDark, LinearGradientMode.Vertical)) g.FillPath(lidBrush, lidPath);

        // Gold bands and the lock.
        using var band = new SolidBrush(Gold);
        var bw = r.Width * 0.14f;
        g.FillRectangle(band, r.X + r.Width * 0.12f, body.Y, bw, body.Height);
        g.FillRectangle(band, r.Right - r.Width * 0.12f - bw, body.Y, bw, body.Height);
        var lockR = new RectangleF(r.X + r.Width * 0.39f, r.Y + lidH - r.Height * 0.12f, r.Width * 0.22f, r.Height * 0.3f);
        g.FillRectangle(band, lockR);

        g.DrawPath(ink, bodyPath);
        g.DrawPath(ink, lidPath);
        g.DrawRectangle(ink, lockR.X, lockR.Y, lockR.Width, lockR.Height);
    }

    public static Bitmap Render(int size)
    {
        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        var pad = size * 0.06f;
        Draw(g, new RectangleF(pad, pad + size * 0.08f, size - 2 * pad, size - 2 * pad - size * 0.16f));
        return bmp;
    }

    public static Icon AppIcon()
    {
        using var stream = typeof(ChestArt).Assembly.GetManifestResourceStream("app.ico");
        return stream != null ? new Icon(stream, SystemInformation.SmallIconSize) : SystemIcons.Application;
    }

    private static GraphicsPath Rounded(RectangleF r, float radius, bool top)
    {
        var d = radius * 2;
        var p = new GraphicsPath();
        if (top)
        {
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
        }
        else
        {
            p.AddLine(r.X, r.Y, r.Right, r.Y);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        }
        p.CloseFigure();
        return p;
    }
}

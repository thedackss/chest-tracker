using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ChestTracker;

/// <summary>
/// The chest drawn in the overlay, the app icon and previews (no image files needed).
/// Same drawing as the "Classic" chest in design/mockups.html: 64×56 design units.
/// </summary>
public static class ChestArt
{
    public static readonly Color Gold = Color.FromArgb(255, 205, 70);
    private static readonly Color Ink = Color.FromArgb(28, 20, 12);
    private const float W = 64, H = 56;

    /// <summary>Draws the chest as large as fits in <paramref name="r"/>, centered, keeping its proportions.</summary>
    public static void Draw(Graphics g, RectangleF r)
    {
        var scale = Math.Min(r.Width / W, r.Height / H);
        var state = g.Save();
        try
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TranslateTransform(r.X + (r.Width - W * scale) / 2, r.Y + (r.Height - H * scale) / 2);
            g.ScaleTransform(scale, scale);
            DrawUnits(g);
        }
        finally
        {
            g.Restore(state);
        }
    }

    private static void DrawUnits(Graphics g)
    {
        using var lid = Lid(close: true);
        using var body = RoundedRect(new RectangleF(6, 24, 52, 28), 3);

        using (var wood = Vertical(24, 52, (0, Color.FromArgb(0x94, 0x58, 0x2a)), (1, Color.FromArgb(0x6e, 0x3f, 0x1d))))
        {
            g.FillPath(wood, body);
        }
        using (var wood = Vertical(6, 24, (0, Color.FromArgb(0xc0, 0x7c, 0x3c)), (1, Color.FromArgb(0x8f, 0x54, 0x27))))
        {
            g.FillPath(wood, lid);
        }

        // Planks and the two gold bands, clipped to the chest.
        var clip = g.Clip;
        using (var shape = new Region(lid))
        {
            shape.Union(body);
            g.SetClip(shape, CombineMode.Intersect);
            using (var plank = new Pen(Color.FromArgb(0x5a, 0x32, 0x16), 2))
            {
                g.DrawLine(plank, 6, 38, 58, 38);
            }
            using (var plank = new Pen(Color.FromArgb(180, 0x7a, 0x45, 0x20), 1.5f))
            {
                g.DrawLine(plank, 6, 15, 58, 15);
            }
            using var band = Vertical(0, 56, (0, Color.FromArgb(0xf0, 0xb4, 0x3a)), (1, Color.FromArgb(0xc4, 0x7f, 0x12)));
            g.FillRectangle(band, 12, 0, 8, 56);
            g.FillRectangle(band, 44, 0, 8, 56);
        }
        g.Clip = clip;

        using var ink3 = new Pen(Ink, 3) { LineJoin = LineJoin.Round };
        using var ink25 = new Pen(Ink, 2.5f) { LineJoin = LineJoin.Round };

        // Trim between lid and body.
        using (var trim = RoundedRect(new RectangleF(4, 21, 56, 6), 1.5f))
        using (var gold = GoldFill(21, 27))
        {
            g.FillPath(gold, trim);
            g.DrawPath(ink25, trim);
        }

        using (var lidLine = Lid(close: false))
        {
            g.DrawPath(ink3, lidLine);
        }
        using (var bodyLine = RoundedRect(new RectangleF(6, 27, 52, 25), 3))
        {
            g.DrawPath(ink3, bodyLine);
        }

        // Lock and keyhole.
        using (var lockPath = RoundedRect(new RectangleF(26, 22, 12, 14), 2))
        using (var gold = GoldFill(22, 36))
        {
            g.FillPath(gold, lockPath);
            g.DrawPath(ink25, lockPath);
        }
        using var inkBrush = new SolidBrush(Ink);
        g.FillEllipse(inkBrush, 32 - 2.2f, 28 - 2.2f, 4.4f, 4.4f);
        g.FillRectangle(inkBrush, 31, 28, 2, 5);
    }

    /// <summary>Rounded lid: M6 24 V18 Q6 6 32 6 T58 18 V24 (quadratic curves as cubics).</summary>
    private static GraphicsPath Lid(bool close)
    {
        var p = new GraphicsPath();
        p.AddLine(6, 24, 6, 18);
        p.AddBezier(6, 18, 6, 10, 14.67f, 6, 32, 6);
        p.AddBezier(32, 6, 49.33f, 6, 58, 10, 58, 18);
        p.AddLine(58, 18, 58, 24);
        if (close) p.CloseFigure();
        return p;
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private static LinearGradientBrush GoldFill(float top, float bottom) =>
        Vertical(top, bottom, (0, Color.FromArgb(0xff, 0xe4, 0x89)), (0.55f, Gold), (1, Color.FromArgb(0xd9, 0x96, 0x1c)));

    /// <summary>Top-to-bottom gradient between two y values (design units).</summary>
    private static LinearGradientBrush Vertical(float top, float bottom, params (float At, Color Color)[] stops)
    {
        // Slightly larger than the band so edge pixels don't wrap around to the other end's colour.
        var brush = new LinearGradientBrush(new PointF(0, top - 0.01f), new PointF(0, bottom + 0.01f), stops[0].Color, stops[^1].Color)
        {
            WrapMode = WrapMode.TileFlipXY,
        };
        if (stops.Length > 2)
        {
            brush.InterpolationColors = new ColorBlend
            {
                Colors = stops.Select(s => s.Color).ToArray(),
                Positions = stops.Select(s => s.At).ToArray(),
            };
        }
        return brush;
    }

    /// <summary>The chest on a transparent square (icon images).</summary>
    public static Bitmap Render(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        var pad = size >= 32 ? size * 0.03f : 0;
        Draw(g, new RectangleF(pad, pad, size - 2 * pad, size - 2 * pad));
        return bmp;
    }

    /// <summary>Writes a PNG-compressed .ico (16-256 px) of the chest. Used by tools/make-icon.ps1.</summary>
    public static void WriteIcon(string path)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
        var images = sizes.Select(s =>
        {
            using var bmp = Render(s);
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }).ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var w = new BinaryWriter(File.Create(path));
        w.Write((ushort)0);
        w.Write((ushort)1);
        w.Write((ushort)sizes.Length);
        var offset = 6 + 16 * sizes.Length;
        for (var i = 0; i < sizes.Length; i++)
        {
            w.Write((byte)(sizes[i] % 256)); // 0 means 256
            w.Write((byte)(sizes[i] % 256));
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((ushort)1);
            w.Write((ushort)32);
            w.Write((uint)images[i].Length);
            w.Write((uint)offset);
            offset += images[i].Length;
        }
        foreach (var img in images) w.Write(img);
    }

    public static Icon AppIcon()
    {
        using var stream = typeof(ChestArt).Assembly.GetManifestResourceStream("app.ico");
        return stream != null ? new Icon(stream, SystemInformation.SmallIconSize) : SystemIcons.Application;
    }
}

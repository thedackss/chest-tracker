using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace ChestTracker.Ui;

/// <summary>One bar: label under it (may be empty) and its hover text.</summary>
public sealed record Bar(double Value, string Label, string Tip);

/// <summary>Simple bar chart (chests per day). The last bar can be highlighted (today).</summary>
public sealed class BarChart : Control
{
    private const double LabelHeight = 18;
    private IReadOnlyList<Bar> _bars = Array.Empty<Bar>();
    private int _hover = -1;

    public BarChart()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public bool HighlightLast { get; set; }

    public IReadOnlyList<Bar> Bars
    {
        get => _bars;
        set
        {
            _bars = value;
            _hover = -1;
            InvalidateVisual();
        }
    }

    private IBrush Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : Brushes.Gray;

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size)); // hover works over empty days too
        if (_bars.Count == 0) return;
        var w = Bounds.Width;
        var h = Bounds.Height - LabelHeight;
        if (w <= 0 || h <= 0) return;

        var max = Math.Max(1, _bars.Max(b => b.Value));
        var slot = w / _bars.Count;
        var gap = Math.Min(4, slot * 0.25);
        var bar = Brush("CtBar");
        var today = Brush("CtAccent2");
        var line = Brush("CtLine");
        var muted = Brush("CtMuted");
        var typeface = new Typeface(FontFamily.Default);

        ctx.DrawRectangle(line, null, new Rect(0, h, w, 1));
        for (var i = 0; i < _bars.Count; i++)
        {
            var b = _bars[i];
            var x = i * slot + gap / 2;
            if (i == _hover)
            {
                ctx.DrawRectangle(line, null, new RoundedRect(new Rect(i * slot, 0, slot, h), 4));
            }
            if (b.Value > 0)
            {
                var bh = Math.Max(3, b.Value / max * (h - 4));
                var brush = HighlightLast && i == _bars.Count - 1 ? today : bar;
                ctx.DrawRectangle(brush, null, new RoundedRect(new Rect(x, h - bh, slot - gap, bh), 3, 3, 0, 0));
            }
            if (b.Label.Length > 0)
            {
                var text = new FormattedText(b.Label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10, muted);
                ctx.DrawText(text, new Point(i * slot + slot / 2 - text.Width / 2, h + 3));
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_bars.Count == 0) return;
        var i = (int)(e.GetPosition(this).X / (Bounds.Width / _bars.Count));
        i = Math.Clamp(i, 0, _bars.Count - 1);
        if (i == _hover) return;
        _hover = i;
        ToolTip.SetTip(this, _bars[i].Tip);
        ToolTip.SetIsOpen(this, false);
        ToolTip.SetIsOpen(this, true);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = -1;
        ToolTip.SetIsOpen(this, false);
        InvalidateVisual();
    }
}

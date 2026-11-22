using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DockForWindows.Views;

internal sealed class WindowControlGlyph : FrameworkElement
{
    private readonly DrawingVisual _shadow = new();
    private readonly DrawingVisual _face = new();
    private readonly VisualCollection _visuals;
    private readonly Geometry _shape;
    private readonly Brush _background;
    private readonly Brush? _fill;
    private readonly Pen? _pen;

    internal WindowControlGlyph(string label, Brush ink, Brush background)
    {
        Width = Height = 28;
        IsHitTestVisible = false;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        _background = background;
        _shape = Geometry.Parse(label == "×"
            ? "M-2.7,-2.7 L2.7,2.7 M2.7,-2.7 L-2.7,2.7"
            : label == "−" ? "M-2.7,0 L2.7,0" : "M-4,-4 L1,-3 L-3,1 Z M4,4 L-1,3 L3,-1 Z");
        _shape.Freeze();
        if (label is "×" or "−") {
            _pen = new Pen(ink, 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            _pen.Freeze();
        } else _fill = ink;
        _shadow.Effect = new DropShadowEffect { BlurRadius = 5, ShadowDepth = 1, Opacity = .16 };
        _visuals = new VisualCollection(this) { _shadow, _face };
    }

    protected override int VisualChildrenCount => _visuals?.Count ?? 0;
    protected override Visual GetVisualChild(int index) => _visuals[index];

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        // Both layers use one local center, including fractional positions at non-integer DPI.
        var center = new Point(RenderSize.Width / 2, RenderSize.Height / 2);
        using (var light = _shadow.RenderOpen()) light.DrawEllipse(Brushes.Black, null, center, 8, 8);
        using var symbol = _face.RenderOpen();
        symbol.DrawEllipse(_background, null, center, 9, 9);
        symbol.PushTransform(new TranslateTransform(center.X, center.Y));
        symbol.DrawGeometry(_fill, _pen, _shape);
        symbol.Pop();
    }
}

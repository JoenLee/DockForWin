using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DockForWindows.Services;

namespace DockForWindows.Views;

public sealed class DockStrip : Panel
{
    public static readonly DependencyProperty IsSeparatorProperty = DependencyProperty.RegisterAttached("IsSeparator", typeof(bool), typeof(DockStrip), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsParentMeasure));
    public static void SetIsSeparator(UIElement element, bool value) => element.SetValue(IsSeparatorProperty, value);
    public static bool GetIsSeparator(UIElement element) => (bool)element.GetValue(IsSeparatorProperty);
    private double[] _scales = Array.Empty<double>(), _centers = Array.Empty<double>();
    private Rect[] _rectangles = Array.Empty<Rect>();
    private ScaleTransform[] _scaleTransforms = Array.Empty<ScaleTransform>();
    private TranslateTransform[] _moves = Array.Empty<TranslateTransform>();
    private double _pointer = double.NaN;
    private Point _lastPointer;
    private bool _hasPointer;
    private bool _rendering;
    private long _lastTick;
    private int[] _order = Array.Empty<int>(), _originalOrder = Array.Empty<int>();
    private UIElement? _dragElement;
    private bool _movingPositions, _positionsReady;
    private Action? _afterReorder;
    private long _lastSwap;
    private UIElement? _lastSwapTarget;
    private double[] _avoidance = Array.Empty<double>();
    private double[] _lastBend = Array.Empty<double>();
    public bool ReorderingOrSettling => _dragElement is not null || _afterReorder is not null;
    public Rect IconSpan { get; private set; } = Rect.Empty;
    public event Action<Rect>? IconSpanChanged;
    public double IconSize { get; set; } = 48;
    public double Gap { get; set; } = 8;
    public double Magnification { get; set; } = 1.45;
    public double HoverLift { get; set; } = 12;
    public double EdgeDistance { get; set; } = 6;
    public double ReflectionExtent { get; set; }
    public double BounceHeadroom { get; set; } = 24;
    public bool Animate { get; set; } = true;
    public bool Vertical { get; set; }
    public bool RightEdge { get; set; }
    private double CellWidth => IconSize + 12;
    private double CellHeight => CellWidth + ReflectionExtent;
    private double Reserve => CellWidth * (Magnification - 1) * 2.4 + 8;
    private double MainSize(int index) => GetIsSeparator(InternalChildren[index]) ? Math.Max(12, Gap * 2) : Vertical ? CellHeight : CellWidth;
    public double RestingLength
    {
        get
        {
            double length = Math.Max(0, InternalChildren.Count - 1) * Gap;
            for (int i = 0; i < InternalChildren.Count; i++) length += MainSize(i);
            return length;
        }
    }
    public DockStrip()
    {
        // A one-alpha input surface keeps layered-window hit testing stable even
        // when the visible dock surface is fully absent. It has no visible panel.
        Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        Unloaded += (_, _) => Stop();
        Loaded += (_, _) => { _hasPointer = false; SynchronizePointer(); };
    }
    protected override Size MeasureOverride(Size available)
    {
        Initialize();
        double along = Reserve * 2 + (Vertical ? BounceHeadroom : 0);
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            bool divider = GetIsSeparator(InternalChildren[i]);
            InternalChildren[i].Measure(divider ? new Size(Vertical ? CellWidth : MainSize(i), Vertical ? MainSize(i) : CellHeight) : new Size(CellWidth, CellHeight));
            _centers[i] = along + MainSize(i) / 2;
            along += MainSize(i) + Gap;
        }
        // All extra height is reserved above the resting baseline. Reflection and
        // magnification must not push the visible dock away from the screen edge.
        double across = (IconSize + 3) * Magnification + HoverLift + EdgeDistance + 26 + (Vertical ? 0 : BounceHeadroom);
        return Vertical ? new Size(across, Math.Max(1, along - Gap)) : new Size(Math.Max(1, along - Gap), across);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        Initialize();
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            bool divider = GetIsSeparator(InternalChildren[i]);
            InternalChildren[i].Arrange(new Rect(0, 0, divider ? (Vertical ? CellWidth : MainSize(i)) : CellWidth, divider ? (Vertical ? MainSize(i) : CellHeight) : CellHeight));
        }
        ApplyTransforms(finalSize); return finalSize;
    }
    private void Initialize()
    {
        bool same = _scales.Length == InternalChildren.Count;
        if (same)
            for (int i = 0; i < InternalChildren.Count; i++)
                if (InternalChildren[i].RenderTransform is not TransformGroup group || group.Children.Count != 2 || !ReferenceEquals(group.Children[0], _scaleTransforms[i])) { same = false; break; }
        if (same) return;
        int count = InternalChildren.Count;
        _scales = new double[count]; _centers = new double[count]; _rectangles = new Rect[count];
        _scaleTransforms = new ScaleTransform[count]; _moves = new TranslateTransform[count];
        _order = Enumerable.Range(0, count).ToArray();
        _avoidance = new double[count];
        _lastBend = new double[count];
        _positionsReady = false;
        for (int i = 0; i < count; i++)
        {
            _scales[i] = 1; _scaleTransforms[i] = new(); _moves[i] = new();
            var group = new TransformGroup(); group.Children.Add(_scaleTransforms[i]); group.Children.Add(_moves[i]); InternalChildren[i].RenderTransform = group;
        }
        _pointer = double.NaN;
        _hasPointer = false;
    }
    private bool ApplyTransforms(Size size, double elapsed = 0)
    {
        double total = Math.Max(0, InternalChildren.Count - 1) * Gap;
        for (int i = 0; i < InternalChildren.Count; i++) total += MainSize(i) * _scales[i];
        double offset = ((Vertical ? size.Height : size.Width) - total) / 2;
        var iconOrder = _order.Where(i => !GetIsSeparator(InternalChildren[i])).ToArray();
        if (iconOrder.Length > 0)
        {
            double first = _scales[iconOrder[0]], last = _scales[iconOrder[^1]];
            offset += Vertical ? ((9 + ReflectionExtent) * last - 3 * first) / 2 : 3 * (last - first);
        }
        double across = Vertical ? size.Width : size.Height;
        var iconSpan = Rect.Empty;
        bool settled = true;
        for (int slot = 0; slot < InternalChildren.Count; slot++)
        {
            int i = _order[slot];
            double scale = _scales[i]; bool divider = GetIsSeparator(InternalChildren[i]);
            double w = (divider && !Vertical ? MainSize(i) : CellWidth) * scale;
            double h = (divider && Vertical ? MainSize(i) : CellHeight) * scale;
            double weight = Magnification > 1.0001 ? Math.Clamp((scale - 1) / (Magnification - 1), 0, 1) : 0;
            double lift = HoverLift * weight;
            double x;
            double y;
            if (Vertical)
            {
                double edge = RightEdge ? across - EdgeDistance - 14 : EdgeDistance + 14;
                x = RightEdge ? edge - (IconSize + 6) * scale - lift : edge - 6 * scale + lift;
                y = offset;
            }
            else
            {
                double iconBottom = across - EdgeDistance - 14;
                x = offset;
                y = iconBottom - (IconSize + 3) * scale - lift;
            }
            if (_movingPositions && _positionsReady)
            {
                double targetX = x, targetY = y;
                double blend = Animate ? 1 - Math.Exp(-24 * Math.Clamp(elapsed, 0, .05)) : 1;
                double currentX=_moves[i].X-(Vertical?(RightEdge?-_lastBend[i]:_lastBend[i]):0);
                double currentY=_moves[i].Y+(Vertical?0:_lastBend[i]);
                x = currentX + (x - currentX) * blend;
                y = currentY + (y - currentY) * blend;
                if (Math.Abs(x - targetX) < .08) x = targetX; else settled = false;
                if (Math.Abs(y - targetY) < .08) y = targetY; else settled = false;
            }
            _lastBend[i]=0;
            if(Animate&&_lastSwap!=0&&_avoidance.Length>i)
            {
                double progress=Math.Clamp((Stopwatch.GetTimestamp()-_lastSwap)/(double)Stopwatch.Frequency/.36,0,1);
                double bend=Math.Sin(Math.PI*progress)*_avoidance[i];if(progress<1&&Math.Abs(_avoidance[i])>.01)settled=false;
                // A short separate lane lets exchanged icons pass, while neighbors
                // slide into the insertion gap. Only compositor transforms change.
                if(Vertical)x+=RightEdge?-bend:bend;else y-=bend;
                _lastBend[i]=bend;
            }
            _scaleTransforms[i].ScaleX = _scaleTransforms[i].ScaleY = scale;
            _moves[i].X = x; _moves[i].Y = y; _rectangles[i] = new Rect(x, y, w, h);
            if (!divider) iconSpan.Union(new Rect(x + 6 * scale, y + 3 * scale, IconSize * scale, IconSize * scale));
            offset += MainSize(i) * scale + Gap;
        }
        IconSpan = iconSpan;
        _positionsReady = true;
        IconSpanChanged?.Invoke(iconSpan);
        return settled;
    }
    public void BeginReorder(UIElement source)
    {
        Initialize(); _originalOrder = (int[])_order.Clone(); _dragElement = source;
        _afterReorder = null; _lastSwap = 0; _lastSwapTarget=null;Array.Clear(_avoidance);_movingPositions = true;
        _pointer = double.NaN; _hasPointer = false; Start();
    }
    public void PreviewSwap(UIElement target, bool force = false)
    {
        if (_dragElement is null || ReferenceEquals(target, _dragElement)) return;
        if(ReferenceEquals(target,_lastSwapTarget))return;
        long now = Stopwatch.GetTimestamp();
        if (!force && (now - _lastSwap) / (double)Stopwatch.Frequency < .14) return;
        int sourceIndex = InternalChildren.IndexOf(_dragElement), targetIndex = InternalChildren.IndexOf(target);
        if (sourceIndex < 0 || targetIndex < 0) return;
        int a = Array.IndexOf(_order, sourceIndex), b = Array.IndexOf(_order, targetIndex);
        if (a < 0 || b < 0 || a == b) return;
        int moving=_order[a];Array.Clear(_avoidance);int direction=a<b?1:-1;
        for(int slot=a;slot!=b;slot+=direction){_order[slot]=_order[slot+direction];_avoidance[_order[slot]]=-IconSize*.12;}
        _order[b]=moving;_avoidance[moving]=IconSize*.45;_lastSwapTarget=target;
        _lastSwap = now; _movingPositions = true; Start();
    }
    public string[] PreviewPinnedOrder() => _order.Select(i => InternalChildren[i]).OfType<DockTile>()
        .Where(t => t.Item.IsPinned && t.Item.Pin.Kind != Models.ItemKind.RecycleBin).Select(t => t.Item.Pin.Id).ToArray();
    public void EndReorder(bool committed, Action afterAnimation)
    {
        if (!committed && _originalOrder.Length == InternalChildren.Count) _order = _originalOrder;
        _dragElement = null; _afterReorder = afterAnimation; _movingPositions = true;
        _pointer = double.NaN; _hasPointer = false; Start();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); UpdatePointer(e.GetPosition(this));
    }
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e); _hasPointer = false; SynchronizePointer();
    }
    public void SynchronizePointer()
    {
        if (ReorderingOrSettling || !IsLoaded || !IsVisible || !IsHitTestVisible || PresentationSource.FromVisual(this) is null || !NativeMethods.GetCursorPos(out var cursor)) return;
        var point = PointFromScreen(new Point(cursor.X, cursor.Y));
        if (new Rect(RenderSize).Contains(point)) UpdatePointer(point);
        else if (_hasPointer || !double.IsNaN(_pointer)) ResetHover();
    }
    private void UpdatePointer(Point point)
    {
        if (ReorderingOrSettling) return;
        double position = Vertical ? point.Y : point.X;
        double previous = Vertical ? _lastPointer.Y : _lastPointer.X;
        if (_hasPointer && Math.Abs(position - previous) < .2) return;
        _lastPointer = point; _hasPointer = true;
        for (int i = 0; i < _rectangles.Length; i++)
        {
            var rect = _rectangles[i]; double start = Vertical ? rect.Y : rect.X; double length = Vertical ? rect.Height : rect.Width;
            if (position < start - Gap / 2 || position > start + length + Gap / 2) continue;
            _pointer = _centers[i] + ((position - start) / Math.Max(1, length) - .5) * MainSize(i);
            Start(); return;
        }
        _pointer = double.NaN; Start();
    }
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (!new Rect(RenderSize).Contains(Mouse.GetPosition(this))) ResetHover();
    }
    public void ResetHover() { if (double.IsNaN(_pointer) && !_hasPointer) return; _pointer = double.NaN; _hasPointer = false; Start(); }
    private void Start() { if (_rendering) return; _rendering = true; _lastTick = Stopwatch.GetTimestamp(); CompositionTarget.Rendering += Frame; }
    private void Stop() { CompositionTarget.Rendering -= Frame; _rendering = false; }
    private void Frame(object? sender, EventArgs args)
    {
        SynchronizePointer();
        if (_scales.Length != InternalChildren.Count) { Stop(); InvalidateMeasure(); return; }
        long now = Stopwatch.GetTimestamp(); double dt = Math.Clamp((now - _lastTick) / (double)Stopwatch.Frequency, 0, .05); _lastTick = now;
        bool done = true;
        for (int i = 0; i < _scales.Length; i++)
        {
            double target = double.IsNaN(_pointer) || GetIsSeparator(InternalChildren[i]) ? 1 : DockMath.Scale(Math.Abs(_centers[i] - _pointer), CellWidth * 2.4, Magnification);
            double next = Animate ? _scales[i] + (target - _scales[i]) * (1 - Math.Exp(-24 * dt)) : target;
            if (Math.Abs(next - target) < .0005) next = target; else done = false;
            _scales[i] = next;
        }
        // No Measure/Arrange invalidation in animation frames: only compositor transforms.
        bool positionsDone = ApplyTransforms(RenderSize, dt);
        if (done && positionsDone)
        {
            Stop();
            if (_afterReorder is not null)
            {
                var callback = _afterReorder; _afterReorder = null; _movingPositions = false;
                Dispatcher.BeginInvoke(callback);
            }
        }
    }
}

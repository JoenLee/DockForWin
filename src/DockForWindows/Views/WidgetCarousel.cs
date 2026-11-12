using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DockForWindows.Models;

namespace DockForWindows.Views;

internal sealed class WidgetCarousel : Grid
{
    private sealed record Card(WidgetHost Host, ScaleTransform Scale, TranslateTransform Shift);
    private readonly Grid _viewport = new() { Height = 296, ClipToBounds = true, Background = Brushes.Transparent };
    private readonly List<Card> _cards = new();
    private readonly List<(Border Active, ScaleTransform Scale, Button Button)> _dots = new();
    private readonly TextBlock _caption = new() { FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 9) };
    private readonly Button _previous, _next;
    private bool _pressed, _dragging;
    private Point _pressPoint;
    private double _pressPosition, _pressPitch, _lastPosition, _lastTime, _dragVelocity;
    private int _pressedIndex;
    private double _largestWidth;
    private readonly double[] _centers;
    private const double CardGap = 22;
    private HwndSource? _source;
    private nint HorizontalWheel(nint hwnd,int message,nint wParam,nint lParam,ref bool handled)
    {
        if(message==0x020E&&IsVisible&&IsMouseOver){int delta=unchecked((short)((wParam.ToInt64()>>16)&0xffff));TryWheel(-delta);handled=true;}return 0;
    }
    // One WPF animation clock drives all transforms. No carousel subscribes to CompositionTarget.Rendering.
    private static readonly DependencyProperty PositionProperty = DependencyProperty.Register(nameof(Position), typeof(double), typeof(WidgetCarousel),
        new FrameworkPropertyMetadata(0d, (owner, _) => ((WidgetCarousel)owner).ApplyFrame()));
    internal double Position => (double)GetValue(PositionProperty);
    internal int SelectedIndex { get; private set; }
    internal bool IsDragging => _dragging;
    internal IReadOnlyList<WidgetHost> Hosts => _cards.Select(c => c.Host).ToArray();
    internal event Action<int>? SelectionChanged;
    internal WidgetCarousel(IReadOnlyList<WidgetDefinition> definitions, int selected, double exponent)
    {
        if (definitions.Count == 0) throw new ArgumentException("轮播不能为空。", nameof(definitions));
        _largestWidth = definitions.Max(d => d.Size.Width);
        _centers = new double[definitions.Count];
        Focusable = true; FocusVisualStyle = null; ClipToBounds = true;
        RowDefinitions.Add(new() { Height = new GridLength(296) });
        RowDefinitions.Add(new() { Height = new GridLength(27) });
        RowDefinitions.Add(new() { Height = new GridLength(26) });
        Children.Add(_viewport);
        for (int i = 0; i < definitions.Count; i++) {
            var host = new WidgetHost(definitions[i], exponent) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(.5, .5) };
            var scale = new ScaleTransform(); var shift = new TranslateTransform(); var transform = new TransformGroup();
            transform.Children.Add(scale); transform.Children.Add(shift); host.RenderTransform = transform;
            AutomationProperties.SetAutomationId(host, definitions[i].Id); AutomationProperties.SetName(host, definitions[i].Title + " " + definitions[i].Size);
            _cards.Add(new(host, scale, shift)); _viewport.Children.Add(host);
            host.VisualScaleChanged+=ApplyFrame;
        }
        _caption.SetResourceReference(TextBlock.ForegroundProperty, "SettingsMuted"); Grid.SetRow(_caption, 1); Children.Add(_caption);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        _previous = Arrow("‹", "上一个小组件", () => Select(SelectedIndex - 1)); controls.Children.Add(_previous);
        for (int i = 0; i < _cards.Count; i++) {
            int index = i; var body = new Grid { Width = 28, Height = 24 };
            body.Children.Add(new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = WidgetBrushes.Get("#CCD7D7") });
            var scale = new ScaleTransform(.25, 1);
            var active = new Border { Width = 22, Height = 6, CornerRadius = new CornerRadius(3), RenderTransformOrigin = new Point(.5, .5), RenderTransform = scale };
            active.SetResourceReference(Border.BackgroundProperty, "SettingsAccent"); body.Children.Add(active);
            var dot = new Button { Content = body, Padding = new Thickness(0), Margin = new Thickness(0), MinHeight = 24, Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), ToolTip = definitions[i].Title + " · " + definitions[i].Size };
            AutomationProperties.SetName(dot, $"选择{definitions[i].Title}，第{i + 1}项，共{definitions.Count}项");
            AutomationProperties.SetAutomationId(dot, definitions[0].Kind.ToString().ToLowerInvariant() + "-page-" + i);
            dot.Click += (_, _) => Select(index); _dots.Add((active, scale, dot)); controls.Children.Add(dot);
        }
        _next = Arrow("›", "下一个小组件", () => Select(SelectedIndex + 1)); controls.Children.Add(_next);
        Grid.SetRow(controls, 2); Children.Add(controls);
        SizeChanged += (_, _) => ApplyFrame();
        Loaded+=(_,_)=>{_source=PresentationSource.FromVisual(this) as HwndSource;_source?.AddHook(HorizontalWheel);};
        Unloaded += (_, _) => { _source?.RemoveHook(HorizontalWheel);_source=null;_pressed = _dragging = false; if (IsMouseCaptured) ReleaseMouseCapture(); ControlMotion.Stop(this, PositionProperty, SelectedIndex); };
        Select(selected, false);
    }
    internal void ResizeSelected(WidgetSize size){var host=_cards[SelectedIndex].Host;host.Resize(size);_largestWidth=_cards.Max(c=>c.Host.Width);Select(SelectedIndex,false);ApplyFrame();}
    private static Button Arrow(string text, string name, Action click)
    {
        var button = new Button { Content = text, Width = 28, MinHeight = 24, FontSize = 18, Padding = new Thickness(0), Margin = new Thickness(5, 0, 5, 0),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), ToolTip = name };
        AutomationProperties.SetName(button, name); button.Click += (_, _) => click(); return button;
    }
    internal void Select(int index, bool animate = true, double? velocity = null)
    {
        index = Math.Clamp(index, 0, _cards.Count - 1); bool changed = SelectedIndex != index; SelectedIndex = index;
        _previous.IsEnabled = index > 0; _next.IsEnabled = index < _cards.Count - 1;
        _caption.Text = $"{_cards[index].Host.Definition.Title}   ·   {_cards[index].Host.Definition.Size}   ·   {index + 1} / {_cards.Count}";
        for (int i = 0; i < _dots.Count; i++) AutomationProperties.SetItemStatus(_dots[i].Button, i == index ? "已选中" : "未选中");
        if (animate && IsLoaded && IsVisible && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast)
            ControlMotion.To(this, PositionProperty, index, speed: 1.28, initialVelocity: velocity);
        else ControlMotion.Stop(this, PositionProperty, index);
        if (changed) SelectionChanged?.Invoke(index);
    }
    private double Pitch {
        get {
            int index = Math.Clamp((int)Math.Round(_pressPosition), 0, _cards.Count - 1);
            if (_cards.Count == 1) return _largestWidth + CardGap;
            return index + 1 < _cards.Count ? _centers[index + 1] - _centers[index] : _centers[index] - _centers[index - 1];
        }
    }
    private void ApplyFrame()
    {
        if (_cards.Count == 0) return;
        double previousWidth = 0;
        for (int i = 0; i < _cards.Count; i++) {
            var card = _cards[i];double fit=Math.Min(Math.Min(1,Math.Max(1,_viewport.ActualHeight-42)/(card.Host.Height*card.Host.PanelScale*1.025)),Math.Max(1,_viewport.ActualWidth-2*(CardGap+28))/(card.Host.Width*card.Host.PanelScale*1.025));
            double scale = fit * (.77 + .23 * Math.Max(0, 1 - Math.Abs(i - Position)));
            card.Scale.ScaleX = card.Scale.ScaleY = scale;
            double width = card.Host.Width * scale * card.Host.VisualScale;
            _centers[i] = i == 0 ? 0 : _centers[i - 1] + (previousWidth + width) / 2 + CardGap;
            previousWidth = width;
        }
        int left = Math.Clamp((int)Math.Floor(Position), 0, Math.Max(0, _cards.Count - 2));
        double center = _cards.Count == 1 ? 0 : _centers[left] + (Position - left) * (_centers[left + 1] - _centers[left]);
        for (int i = 0; i < _cards.Count; i++) {
            var card = _cards[i]; double distance = i - Position, abs = Math.Abs(distance);
            double emphasis = Math.Max(0, 1 - abs);
            card.Shift.X = _centers[i] - center; card.Shift.Y = 6 * Math.Min(abs, 2);
            card.Host.Opacity = Math.Clamp(1 - abs * .31, 0, 1);
            card.Host.Visibility = abs > 2.4 ? Visibility.Hidden : Visibility.Visible;
            card.Host.IsHitTestVisible = abs < 1.6;
            Panel.SetZIndex(card.Host, (int)Math.Round(100 - abs * 10));
            _dots[i].Active.Opacity = emphasis;
            _dots[i].Scale.ScaleX = .28 + .72 * emphasis;
        }
    }
    internal bool TryWheel(int delta)
    {
        if (delta == 0 || _dragging) return false;
        int next = Math.Clamp(SelectedIndex + (delta < 0 ? 1 : -1) * Math.Max(1, Math.Abs(delta / 120)), 0, _cards.Count - 1);
        if (next == SelectedIndex) return false;
        Select(next); return true;
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (!_dragging) TryWheel(e.Delta);
        base.OnMouseWheel(e);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        int? target = e.Key switch { Key.Left => SelectedIndex - 1, Key.Right => SelectedIndex + 1, Key.Home => 0, Key.End => _cards.Count - 1, _ => null };
        if (target.HasValue) { Select(target.Value); e.Handled = true; }
        base.OnKeyDown(e);
    }
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        var point = e.GetPosition(_viewport);
        if (!new Rect(_viewport.RenderSize).Contains(point)) return;
        DependencyObject? source = e.OriginalSource as DependencyObject; WidgetHost? clicked = null;
        for (var at = source; at != null && at != this; at = at is Visual?VisualTreeHelper.GetParent(at):LogicalTreeHelper.GetParent(at)) {
            if (at is ButtonBase or TextBoxBase or Selector) return;
            if (at is WidgetHost host) clicked = host;
        }
        _pressedIndex = clicked == null ? SelectedIndex : _cards.FindIndex(c => c.Host == clicked);
        ControlMotion.Stop(this, PositionProperty, Position);
        foreach(var c in _cards)c.Host.SetGestureActive(true);
        _pressed = true; _pressPoint = e.GetPosition(this); _pressPosition = Position; _pressPitch = Pitch; _dragVelocity = 0;
    }
    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (!_pressed) return;
        if (e.LeftButton != MouseButtonState.Pressed) { _pressed = false; EndGesture();Select(SelectedIndex); return; }
        var point = e.GetPosition(this); double dx = point.X - _pressPoint.X, dy = point.Y - _pressPoint.Y;
        if (!_dragging) {
            if (Math.Abs(dx) < 6) return;
            if (Math.Abs(dy) > Math.Abs(dx)) { _pressed = false;EndGesture(); Select(SelectedIndex); return; }
            if (!CaptureMouse()) { _pressed = false;EndGesture(); Select(SelectedIndex);return; }
            _dragging = true; Focus(); _lastPosition = Position; _lastTime = ControlMotion.Now;
            ControlMotion.Stop(this, PositionProperty, Position);
        }
        DragTo(_pressPosition - dx / Math.Max(1, _pressPitch)); e.Handled = true;
    }
    internal static double Resist(double value, int count)
    {
        double clamped = Math.Clamp(value, 0, count - 1), excess = value - clamped;
        return clamped + .22 * excess / (.22 + Math.Abs(excess));
    }
    private void DragTo(double value)
    {
        double position = Resist(value, _cards.Count), now = ControlMotion.Now, dt = now - _lastTime;
        if (dt > .001) _dragVelocity = .55 * _dragVelocity + .45 * Math.Clamp((position - _lastPosition) / dt, -8, 8);
        SetCurrentValue(PositionProperty, position); _lastPosition = position; _lastTime = now;
    }
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (!_pressed) return;
        bool dragged = _dragging; _pressed = _dragging = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
        EndGesture();
        Select(dragged ? (int)Math.Round(Position) : _pressedIndex, velocity: dragged ? _dragVelocity : null); e.Handled = true;
    }
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (!_dragging) return;
        _pressed = _dragging = false;EndGesture(); Select((int)Math.Round(Position));
    }
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_pressed && !_dragging) { _pressed = false;EndGesture(); Select(SelectedIndex); }
    }
    private void EndGesture(){foreach(var c in _cards)c.Host.SetGestureActive(false);}
}

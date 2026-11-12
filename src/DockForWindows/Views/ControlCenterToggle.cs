using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace DockForWindows.Views;

// 阻尼弹簧缓动：收敛快、带轻微 overshoot，用于控制中心的展开/收起/开关位移。
// 仅控制中心使用，不触碰 Dock 的 DockMath 时间步进引擎。
internal sealed class SpringEase : EasingFunctionBase
{
    public SpringEase(){EasingMode=EasingMode.EaseIn;}
    protected override Freezable CreateInstanceCore() => new SpringEase();
    protected override double EaseInCore(double normalizedTime)
    {
        if (normalizedTime <= 0) return 0;
        if (normalizedTime >= 1) return 1;
        // x(t) = 1 - e^(-ζωt)·(cos(ωd·t) + (ζω/ωd)·sin(ωd·t))
        const double zeta = ControlMotion.DampingRatio; double omega = Math.Sqrt(ControlMotion.Stiffness/ControlMotion.Mass)*.7;
        double wd = omega * Math.Sqrt(1 - zeta * zeta);
        double decay = Math.Exp(-zeta * omega * normalizedTime);
        double phi = wd * normalizedTime;
        return 1 - decay * (Math.Cos(phi) + (zeta * omega / wd) * Math.Sin(phi));
    }
}

/// <summary>
/// macOS 风格滑动开关（40×22 胶囊 + 白色圆滑块）。纯自绘单元素：
/// 一个 Progress 依赖属性驱动滑块位移与轨道颜色插值，AffectsRender 触发轻量重绘，
/// 不含玻璃采样控件——开关在 macOS 原生中本来就是纯色材质，且这样动画零额外采样开销。
/// </summary>
public sealed class ControlCenterToggle : FrameworkElement
{
    public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(
        nameof(IsOn), typeof(bool), typeof(ControlCenterToggle),
        new FrameworkPropertyMetadata(false, OnIsOnChanged));

    private static readonly DependencyProperty ProgressPropertyKey = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(ControlCenterToggle),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ProgressProperty = ProgressPropertyKey;

    public bool IsOn { get => (bool)GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }
    public double Progress { get => (double)GetValue(ProgressProperty); private set => SetValue(ProgressPropertyKey, value); }
    public bool Animate { get; set; } = true;
    public event Action<bool>? Toggled;

    private readonly Color _on, _off;
    private bool _armed;

    public ControlCenterToggle() : this(Parse("#34C759"), Parse("#787880")) { }
    public ControlCenterToggle(Color on, Color off)
    {
        _on = on; _off = off;
        Width = 40; Height = 22;
        Cursor = Cursors.Hand;Focusable=true;
    }

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static void OnIsOnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ControlCenterToggle)d).Play((bool)e.NewValue);

    private void Play(bool on)
    {
        double target = on ? 1 : 0;
        if (!Animate || !IsLoaded)
        {
            BeginAnimation(ProgressProperty, null);
            Progress = target;
            return;
        }
        ControlMotion.To(this,ProgressProperty,target);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 8 || h < 4) return;
        double t = Math.Clamp(Progress, 0, 1);
        var track = new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(_off.A + (_on.A - _off.A) * t),
            (byte)Math.Round(_off.R + (_on.R - _off.R) * t),
            (byte)Math.Round(_off.G + (_on.G - _off.G) * t),
            (byte)Math.Round(_off.B + (_on.B - _off.B) * t)));
        track.Freeze();
        double radius = h / 2;
        dc.DrawRoundedRectangle(track, null, new Rect(0, 0, w, h), radius, radius);
        double knob = h - 4;
        double x = 2 + t * (w - knob - 4);
        var center = new Point(x + knob / 2, h / 2);
        var shadow = new SolidColorBrush(Color.FromArgb(55, 0, 0, 0)); shadow.Freeze();
        dc.DrawEllipse(shadow, null, new Point(center.X, center.Y + 1), knob / 2, knob / 2);
        dc.DrawEllipse(Brushes.White, null, center, knob / 2, knob / 2);
    }

    protected override HitTestResult HitTestCore(PointHitTestParameters parameters) => new PointHitTestResult(this, parameters.HitPoint);

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if(!IsEnabled)return;_armed = true;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (IsMouseCaptured) ReleaseMouseCapture();
        bool click = _armed && IsMouseOver && IsEnabled;
        _armed = false;
        if (click) { IsOn = !IsOn; Toggled?.Invoke(IsOn); }
        e.Handled = true;
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if(IsEnabled&&e.Key is Key.Space or Key.Enter){IsOn=!IsOn;Toggled?.Invoke(IsOn);e.Handled=true;}
    }
    protected override AutomationPeer OnCreateAutomationPeer()=>new TogglePeer(this);
    private sealed class TogglePeer : FrameworkElementAutomationPeer,IToggleProvider
    {
        private readonly ControlCenterToggle _owner;
        public TogglePeer(ControlCenterToggle owner):base(owner){_owner=owner;}
        protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.CheckBox;
        protected override string GetClassNameCore()=>nameof(ControlCenterToggle);
        public override object? GetPattern(PatternInterface pattern)=>pattern==PatternInterface.Toggle?this:base.GetPattern(pattern);
        public ToggleState ToggleState=>_owner.IsOn?ToggleState.On:ToggleState.Off;
        public void Toggle(){if(!_owner.IsEnabled)throw new ElementNotEnabledException();_owner.IsOn=!_owner.IsOn;_owner.Toggled?.Invoke(_owner.IsOn);}
    }
}

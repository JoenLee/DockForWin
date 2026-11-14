using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DockForWindows.Models;

namespace DockForWindows.Views;

internal sealed class DockTile : Border
{
    private readonly Ellipse _dot;
    private readonly SolidColorBrush _indicatorBrush;
    private readonly Color _originalIndicatorColor;
    private readonly AdaptiveGlassAppearance _indicatorBackground = new();
    private bool? _lightIndicator;
    private bool _attentionActive;
    private readonly bool _animateAttention;
    private readonly bool _darkIconPlate;
    private readonly Image _image;
    private Image? _reflectionImage;
    private readonly TranslateTransform _bounce = new();
    private readonly TranslateTransform _mirrorBounce = new();
    public DockItem Item { get; }
    public event Action? Invoked;
    public void Invoke() => Invoked?.Invoke();
    protected override AutomationPeer OnCreateAutomationPeer() => new TilePeer(this);
    public DockTile(DockItem item, Palette palette, DockSettings settings)
    {
        _animateAttention=settings.Animations&&!palette.ReducedMotion;
        Item = item; Background = Brushes.Transparent; Cursor = Cursors.Hand; Focusable = true; AllowDrop = item.Pin.Kind != ItemKind.RecycleBin;
        _darkIconPlate = new Palette(new DockSettings { InterfaceTheme = InterfaceTheme.System }).Dark;
        double icon = settings.IconSize;
        bool reflect = AppearancePolicy.ShowReflections(settings) && !SystemParameters.HighContrast;
        double extension = reflect ? icon * settings.ReflectionLength + settings.ReflectionGap + 4 : 0;
        Width = icon + 12; Height = icon + 12 + extension;
        CacheMode = new BitmapCache { RenderAtScale = 1.8, EnableClearType = false };
        AutomationProperties.SetName(this, item.Name);
        var canvas = new Canvas { Width = Width, Height = Height, Background = Brushes.Transparent };
        var image = new Image { Source = IconPresentation.Normalize(item.Icon,item.Pin.Kind,_darkIconPlate,PreservePlate(item)), Width = icon, Height = icon, Stretch = Stretch.Uniform, RenderTransform = _bounce, IsHitTestVisible = false };
        _image = image;
        AutomationProperties.SetAutomationId(image, "dock-icon");
        AutomationProperties.SetName(image, item.Name + LocalizedText.Get("图标"));
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        Canvas.SetLeft(image, 6); Canvas.SetTop(image, 3); canvas.Children.Add(image);
        _originalIndicatorColor = palette.Indicator is SolidColorBrush indicator ? indicator.Color : Colors.White;
        _indicatorBrush = new SolidColorBrush(_originalIndicatorColor);
        _dot = new Ellipse { Width = 4, Height = 4, Fill = _indicatorBrush, IsHitTestVisible = false };
        AutomationProperties.SetAutomationId(_dot, "dock-running-indicator");
        Canvas.SetLeft(_dot, (Width - 4) / 2); Canvas.SetTop(_dot, icon + 6); canvas.Children.Add(_dot);
        if (reflect)
        {
            var reflected = new Image { Source = image.Source, Width = icon, Height = icon, Stretch = Stretch.Uniform, RenderTransformOrigin = new Point(.5, .5), IsHitTestVisible = false };
            _reflectionImage = reflected;
            AutomationProperties.SetAutomationId(reflected, "dock-reflection");
            AutomationProperties.SetName(reflected, item.Name + LocalizedText.Get("倒影"));
            var transforms = new TransformGroup(); transforms.Children.Add(new ScaleTransform(1, -1)); transforms.Children.Add(_mirrorBounce); reflected.RenderTransform = transforms;
            RenderOptions.SetBitmapScalingMode(reflected, BitmapScalingMode.HighQuality);
            var reflectedCanvas = new Canvas { Width = icon, Height = icon, IsHitTestVisible = false };
            reflectedCanvas.Children.Add(reflected);
            var clip = new Border
            {
                Width = icon, Height = icon * settings.ReflectionLength, Child = reflectedCanvas, ClipToBounds = true,
                Opacity = settings.ReflectionOpacity, VerticalAlignment = VerticalAlignment.Top,
                OpacityMask = new LinearGradientBrush(new GradientStopCollection { new(Colors.White, 0), new(Color.FromArgb(0, 255, 255, 255), 1) }, new Point(0, 0), new Point(0, 1)),
                IsHitTestVisible = false
            };
            // Keep the full mirrored icon above a clipped, fading viewport.
            reflected.VerticalAlignment = VerticalAlignment.Top;
            if (settings.ReflectionBlur > 0) clip.Effect = new BlurEffect { Radius = settings.ReflectionBlur, RenderingBias = RenderingBias.Performance };
            Canvas.SetLeft(clip, 6); Canvas.SetTop(clip, icon + 12 + settings.ReflectionGap); canvas.Children.Add(clip);
        }
        Child = canvas; UpdateStatus(); item.PropertyChanged += ItemChanged;
        Loaded+=(_,_)=>{item.PropertyChanged-=ItemChanged;item.PropertyChanged+=ItemChanged;UpdateAttention();};
        Unloaded += (_, _) => {item.PropertyChanged -= ItemChanged;_dot.BeginAnimation(OpacityProperty,null);};
        ToolTipService.SetInitialShowDelay(this, 400); ToolTipService.SetPlacement(this, System.Windows.Controls.Primitives.PlacementMode.Top);
        // No hover/selection fill. The icon and running dot provide the visual feedback.
    }
    private static bool PreservePlate(DockItem item)=>item.Name.Contains("GPT",StringComparison.OrdinalIgnoreCase)||item.Pin.AppUserModelId.Contains("OpenAI.ChatGPT",StringComparison.OrdinalIgnoreCase);
    private void ItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName is nameof(DockItem.Windows) or nameof(DockItem.NeedsAttention))UpdateStatus();
        if (e.PropertyName == nameof(DockItem.Icon)) { _image.Source = IconPresentation.Normalize(Item.Icon,Item.Pin.Kind,_darkIconPlate,PreservePlate(Item)); if (_reflectionImage is not null) _reflectionImage.Source = _image.Source; }
    }
    private void UpdateStatus()
    {
        _dot.Visibility = Item.IsRunning||Item.NeedsAttention ? Visibility.Visible : Visibility.Hidden;
        UpdateAttention();
        ToolTip = Item.Name + (Item.IsRunning ? LocalizedText.Format($" · {Item.Windows.Count} 个窗口") : "") + (!Item.IsPinned ? Item.IsRunning ? LocalizedText.Get(" · 未固定") : LocalizedText.Get(" · 最近使用") : "");
    }
    private void UpdateAttention()
    {
        bool wanted=Item.NeedsAttention;
        if(wanted==_attentionActive&&(!wanted||_dot.HasAnimatedProperties||!_animateAttention))return;
        _attentionActive=wanted;_dot.BeginAnimation(OpacityProperty,null);_dot.Opacity=1;
        _indicatorBrush.BeginAnimation(SolidColorBrush.ColorProperty,null);_lightIndicator=null;
        _indicatorBrush.Color=wanted?Color.FromRgb(255,159,10):_originalIndicatorColor;
        if(wanted&&IsLoaded&&_animateAttention&&SystemParameters.ClientAreaAnimation)
            _dot.BeginAnimation(OpacityProperty,new DoubleAnimation(.3,1,TimeSpan.FromMilliseconds(900)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}});
    }
    public void RestoreIndicatorColor()
    {
        if(Item.NeedsAttention)return;
        if (_lightIndicator is null) return;
        _lightIndicator = null;
        _indicatorBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
        _indicatorBrush.Color = _originalIndicatorColor;
    }
    public void UpdateIndicatorBackdrop(BitmapSource backdrop, Rect bounds, double tintOpacity, double seconds, bool animate)
    {
        if(Item.NeedsAttention)return;
        if (!Item.IsRunning || !IsLoaded || PresentationSource.FromVisual(this) is null) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        var center = _dot.PointToScreen(new Point(2, 2));
        int pad = (int)Math.Ceiling(6 * dpi.DpiScaleX);
        var region = new Int32Rect((int)(center.X - bounds.X) - pad, (int)(center.Y - bounds.Y) - pad, pad * 2, pad * 2);
        _indicatorBackground.Observe(backdrop, region, seconds);
        bool light = AdaptiveGlassAppearance.LightIndicator(_indicatorBackground.Luminance * (1 - tintOpacity), _lightIndicator);
        if (_lightIndicator == light) return;
        _lightIndicator = light;
        var target = light ? Color.FromRgb(245, 246, 248) : Color.FromRgb(24, 26, 30);
        if (animate)
            _indicatorBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(target, TimeSpan.FromMilliseconds(240)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        else { _indicatorBrush.BeginAnimation(SolidColorBrush.ColorProperty, null); _indicatorBrush.Color = target; }
    }
    public void Bounce(bool enabled, double height, double milliseconds)
    {
        if (!enabled) return;
        DoubleAnimationUsingKeyFrames Create(double direction) => new()
        {
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            KeyFrames =
            {
                new EasingDoubleKeyFrame(direction * height, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(milliseconds * .38)), new CubicEase { EasingMode = EasingMode.EaseOut }),
                new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(milliseconds)), new CubicEase { EasingMode = EasingMode.EaseInOut })
            }
        };
        _bounce.BeginAnimation(TranslateTransform.YProperty, Create(-1)); _mirrorBounce.BeginAnimation(TranslateTransform.YProperty, Create(1));
    }
    private sealed class TilePeer : FrameworkElementAutomationPeer, IInvokeProvider
    {
        public TilePeer(DockTile owner) : base(owner) { }
        protected override string GetClassNameCore() => "DockTile";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
        public override object? GetPattern(PatternInterface pattern) => pattern == PatternInterface.Invoke ? this : base.GetPattern(pattern);
        public void Invoke() { if (!IsEnabled()) throw new ElementNotEnabledException(); Owner.Dispatcher.BeginInvoke(((DockTile)Owner).Invoke); }
    }
}

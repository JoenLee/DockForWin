using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace DockForWindows.Views;

/// <summary>
/// Small control material using the Dock's actual lens and lighting shader.
/// Samples the host's background brush, never the desktop or its own text.
/// The semantic fill stays intact; only the material is refracted and lit.
/// </summary>
public sealed class GlassButtonChrome : Grid
{
    public static readonly DependencyProperty SurfaceBrushProperty=DependencyProperty.Register(nameof(SurfaceBrush),typeof(Brush),typeof(GlassButtonChrome),new FrameworkPropertyMetadata(Brushes.White,Changed));
    public static readonly DependencyProperty EdgeBrushProperty=DependencyProperty.Register(nameof(EdgeBrush),typeof(Brush),typeof(GlassButtonChrome),new FrameworkPropertyMetadata(Brushes.Transparent,Changed));
    public static readonly DependencyProperty EdgeThicknessProperty=DependencyProperty.Register(nameof(EdgeThickness),typeof(Thickness),typeof(GlassButtonChrome),new FrameworkPropertyMetadata(new Thickness(),Changed));
    public static readonly DependencyProperty InteractionProperty=DependencyProperty.Register(nameof(Interaction),typeof(double),typeof(GlassButtonChrome),new FrameworkPropertyMetadata(0d,Changed));
    // Optional explicit backdrop for hosts that paint an image/gradient behind the control.
    public static readonly DependencyProperty BackdropBrushProperty=DependencyProperty.Register(nameof(BackdropBrush),typeof(Brush),typeof(GlassButtonChrome),new FrameworkPropertyMetadata(null,Changed));
    public Brush SurfaceBrush {get=>(Brush)GetValue(SurfaceBrushProperty);set=>SetValue(SurfaceBrushProperty,value);}
    public Brush EdgeBrush {get=>(Brush)GetValue(EdgeBrushProperty);set=>SetValue(EdgeBrushProperty,value);}
    public Thickness EdgeThickness {get=>(Thickness)GetValue(EdgeThicknessProperty);set=>SetValue(EdgeThicknessProperty,value);}
    public double Interaction {get=>(double)GetValue(InteractionProperty);set=>SetValue(InteractionProperty,value);}
    public Brush? BackdropBrush {get=>(Brush?)GetValue(BackdropBrushProperty);set=>SetValue(BackdropBrushProperty,value);}
    private readonly MaterialLayer _lens=new(),_light=new();
    private readonly OutlineLayer _tint=new(){Opacity=.96};
    private readonly OutlineLayer _edge=new();
    private OutlineLayer? _shadow;
    private readonly ImageBrush _input=new(){Stretch=Stretch.Fill};
    private readonly LiquidGlassEffect _lensEffect=new(),_lightEffect=new(){LightingOnly=1};
    private readonly TranslateTransform _edgeOffset=new();
    private Size _outlineSize;private double _outlineInset=double.NaN;
    private static void Changed(DependencyObject o,DependencyPropertyChangedEventArgs e)=>((GlassButtonChrome)o).Refresh();
    public GlassButtonChrome()
    {
        Background=Brushes.Transparent;
        _lensEffect.Input=_lightEffect.Input=_input;_lens.Effect=_lensEffect;_light.Effect=_lightEffect;_edge.RenderTransform=_edgeOffset;
        Children.Add(_lens);Children.Add(_tint);Children.Add(_light);Children.Add(_edge);
        Loaded+=(_,_)=>Refresh();SizeChanged+=(_,_)=>Refresh();IsVisibleChanged+=(_,_)=>{if(IsVisible)Refresh();};
    }
    private (Brush Brush,Rect Bounds) HostBackground(double pad)
    {
        for(DependencyObject? p=VisualTreeHelper.GetParent(this);p is not null;p=VisualTreeHelper.GetParent(p))
        {
            // A Button's background is the semantic fill, not the backdrop.
            Brush? brush=p switch{Border b=>b.Background,Panel panel=>panel.Background,Window w=>w.Background,_=>null};
            if(brush is not null&&brush!=Brushes.Transparent&&p is FrameworkElement host)
            {
                var origin=TranslatePoint(new Point(),host);
                return (brush,new Rect(pad-origin.X,pad-origin.Y,host.ActualWidth,host.ActualHeight));
            }
        }
        return (Brushes.White,new Rect(0,0,ActualWidth+pad*2,ActualHeight+pad*2));
    }
    private void Refresh()
    {
        if(!IsLoaded||!IsVisible||ActualWidth<1||ActualHeight<1)return;
        bool controlCenter=Window.GetWindow(this) is ControlCenterWindow;
        if(controlCenter&&_shadow is not null){Children.Remove(_shadow);_shadow=null;}
        else if(!controlCenter&&_shadow is null){_shadow=new(){Fill=Brushes.Black,Opacity=.10,Outline=_lens.Clip,Effect=new DropShadowEffect{BlurRadius=7,ShadowDepth=2,Opacity=.6,Color=Colors.Black}};Children.Insert(0,_shadow);}
        _lightEffect.SetControlCenterLighting(controlCenter);
        double w=ActualWidth,h=ActualHeight,pad=12;
        var dpi=VisualTreeHelper.GetDpi(this);double scale=dpi.DpiScaleX;
        double inset=EdgeThickness.Left/2;
        if(_lens.Clip is null||_outlineSize!=RenderSize||_outlineInset!=inset){
            _outlineSize=RenderSize;_outlineInset=inset;
            var geometry=CapsuleBorder.SmoothOutline(RenderSize);
            _lens.Clip=geometry;_light.Clip=geometry;if(_shadow is not null){_shadow.Outline=geometry;_shadow.InvalidateVisual();}_tint.Outline=geometry;
            _edge.Outline=CapsuleBorder.SmoothOutline(new Size(Math.Max(0,w-inset*2),Math.Max(0,h-inset*2)));
            _edgeOffset.X=_edgeOffset.Y=inset;
        }
        _tint.Fill=SurfaceBrush;_tint.InvalidateVisual();
        bool flat=Window.GetWindow(this) is ControlCenterWindow center&&center.UsesClassicBlur;
        _lens.Visibility=_light.Visibility=_edge.Visibility=flat?Visibility.Collapsed:Visibility.Visible;
        if(_shadow is not null)_shadow.Visibility=flat?Visibility.Collapsed:Visibility.Visible;
        if(flat){_input.ImageSource=null;return;}
        BitmapSource bitmap;Point plateOrigin;
        if(Window.GetWindow(this) is IGlassBackdropHost host&&host.GlassBackdrop is BitmapSource frame&&!host.GlassBackdropBounds.IsEmpty)
        {
            bitmap=frame;var origin=PointToScreen(new Point());plateOrigin=new Point(origin.X-host.GlassBackdropBounds.X,origin.Y-host.GlassBackdropBounds.Y);
        }
        else
        {
        var visual=new DrawingVisual();
        using(var dc=visual.RenderOpen())
        {
            // Copy only the brush behind the control in host coordinates.
            // Do not rescale an image/gradient to the button or sample its label.
            if(BackdropBrush is not null)dc.DrawRectangle(BackdropBrush,null,new Rect(0,0,w+2*pad,h+2*pad));
            else {var background=HostBackground(pad);dc.DrawRectangle(background.Brush,null,background.Bounds);}
        }
        var rendered=new RenderTargetBitmap((int)Math.Ceiling((w+2*pad)*scale),(int)Math.Ceiling((h+2*pad)*scale),96*scale,96*scale,PixelFormats.Pbgra32);
        rendered.Render(visual);rendered.Freeze();bitmap=rendered;plateOrigin=new Point(pad*scale,pad*scale);
        }
        if(!ReferenceEquals(_input.ImageSource,bitmap))_input.ImageSource=bitmap;
        UpdateEffect(_lensEffect);UpdateEffect(_lightEffect);
        void UpdateEffect(LiquidGlassEffect effect){
            effect.SourceSize=new Point(bitmap.PixelWidth,bitmap.PixelHeight);effect.PlateOrigin=plateOrigin;effect.PlateSize=new Point(w*scale,h*scale);
            effect.Shape=new Point(100,.22);effect.Material=new Point(scale,0);effect.Texture=new Point(0,.32);effect.Lighting=new Point(.95+Interaction,2);
            effect.Sides=new Point(.32,0);effect.Tint=new Point(0,0);effect.ControlProfile=new Point(1,CapsuleBorder.PillExponent);
        }
        _edge.Stroke=EdgeBrush;_edge.StrokeThickness=EdgeThickness.Left;_edge.InvalidateVisual();
    }
    internal void RefreshMaterial()=>Refresh();
    private sealed class MaterialLayer : FrameworkElement
    {
        public MaterialLayer(){IsHitTestVisible=false;}
        protected override void OnRender(DrawingContext dc)=>dc.DrawRectangle(Brushes.White,null,new Rect(RenderSize));
    }
    private sealed class OutlineLayer : FrameworkElement
    {
        public Geometry? Outline;public Brush? Fill,Stroke;public double StrokeThickness;
        public OutlineLayer(){IsHitTestVisible=false;}
        protected override void OnRender(DrawingContext dc){if(Outline is not null)dc.DrawGeometry(Fill,StrokeThickness>0?new Pen(Stroke,StrokeThickness):null,Outline);}
    }
}

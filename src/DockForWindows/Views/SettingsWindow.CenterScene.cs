using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DockForWindows.Models;
using DockForWindows.Services;

namespace DockForWindows.Views;

internal sealed partial class SettingsWindow
{
    private readonly Canvas _centerScene=new(){IsHitTestVisible=false};
    private FrameworkElement? _centerSceneFrame;
    private FrameworkElement BuildCenterScene()
    {
        var body=new StackPanel{Margin=new Thickness(0,0,0,18)};
        var view=new Viewbox{Child=_centerScene,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,Margin=new Thickness(18),MaxHeight=260};
        var frame=new SquircleBorder{CornerRadius=new CornerRadius(16),Height=296,Background=new ImageBrush(_wallpaper){Stretch=Stretch.UniformToFill},Child=view};
        _centerSceneFrame=frame;frame.SizeChanged+=(_,_)=>RefreshCenterSceneBackdrop();
        AutomationProperties.SetAutomationId(frame,"control-center-layout-preview");body.Children.Add(frame);
        var actions=new Grid{Margin=new Thickness(0,8,0,0)};
        var note=new TextBlock{Text=LocalizedText.Get("布局预览 · 随草稿更新"),VerticalAlignment=VerticalAlignment.Center};note.SetResourceReference(TextBlock.ForegroundProperty,"SettingsMuted");actions.Children.Add(note);
        var open=SettingsButton(LocalizedText.Get("打开实时预览"),(_,_)=>{if(_controlLive is not null){_controlLive.IsChecked=true;UpdateControlPreview();}});open.HorizontalAlignment=HorizontalAlignment.Right;AutomationProperties.SetAutomationId(open,"control-center-open-preview");actions.Children.Add(open);body.Children.Add(actions);
        frame.Loaded+=(_,_)=>RefreshCenterScene();return body;
    }
    private void RefreshCenterScene()
    {
        if(!_ready)return;
        if(_centerSceneFrame is SquircleBorder frame)frame.Background=new ImageBrush(_wallpaper){Stretch=Stretch.UniformToFill};
        _centerScene.Children.Clear();var options=_draft.ControlCenter;var placements=ControlCenterLayout.Arrange(options);
        _centerScene.Width=Math.Max(options.Geometry.PanelWidth-36,placements.Count==0?1:placements.Max(p=>p.Bounds.Right));_centerScene.Height=Math.Max(80,placements.Count==0?0:placements.Max(p=>p.Bounds.Bottom));
        var material=options.Material(_draft);var palette=new Palette(material);
        foreach(var placement in placements){
            var item=options.Modules.Single(m=>m.Module==placement.Module);var bounds=placement.Bounds;
            double radius=item.Module==ControlModule.Media?options.Geometry.MediaRoundness:item.EffectiveShape switch{ControlCardShape.Circle=>100,ControlCardShape.Capsule=>options.Geometry.CapsuleRoundness,ControlCardShape.Square=>options.Geometry.SquareRoundness,_=>options.Geometry.RectangleRoundness};
            var card=new Grid{Width=bounds.Width,Height=bounds.Height,ClipToBounds=true};Canvas.SetLeft(card,bounds.Left);Canvas.SetTop(card,bounds.Top);
            var surface=new DockSurface{Fill=palette.Surface,Stroke=palette.Line,Radius=radius,UseGlass=options.MaterialMode==ControlCenterMaterialMode.LiquidGlass&&material.Theme==DockTheme.Glass,GlassIntensity=palette.GlassIntensity,GlassOpacity=material.GlassOpacity,GlassFrost=material.GlassFrost,GlassDistortion=material.GlassDistortion,GlassHighlight=material.GlassHighlight,GlassHighlightWidth=material.GlassHighlightWidth,GlassSideDarkness=material.GlassSideDarkness,GlassTint=material.GlassTint,GlassTintColor=GlassTint.Resolve(material)};card.Children.Add(surface);
            surface.UseGlass=options.MaterialMode==ControlCenterMaterialMode.LiquidGlass&&!SystemParameters.HighContrast;ControlCenterWindow.ConfigurePreviewMaterial(surface,options,palette);
            var content=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(ControlCenterTokens.Padding(options.Geometry.CardPadding)/2)};
            var glyph=ControlCenterIcon.AvailableGlyphs.Contains(item.Glyph)?item.Glyph:item.Module.ToString();
            var icon=new ControlCenterIcon(glyph){Width=options.Geometry.IconSize,Height=options.Geometry.IconSize};icon.SetResourceReference(ControlCenterIcon.BrushProperty,"SettingsAccent");content.Children.Add(icon);
            if(item.EffectiveShape!=ControlCardShape.Circle){var label=new TextBlock{Text=string.IsNullOrWhiteSpace(item.DisplayName)?ControlCenterCatalog.Name(item.Module):item.DisplayName,FontSize=options.Geometry.FontSize,TextTrimming=TextTrimming.CharacterEllipsis,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,options.Geometry.ContentGap,0,0),Foreground=palette.Text,MaxWidth=Math.Max(1,bounds.Width-18)};content.Children.Add(label);}
            card.Children.Add(content);_centerScene.Children.Add(card);
        }
        if(placements.Count==0){var empty=new TextBlock{Text=LocalizedText.Get("当前页面没有模块"),Foreground=palette.Text};_centerScene.Children.Add(empty);}
        Dispatcher.BeginInvoke(RefreshCenterSceneBackdrop,DispatcherPriority.Render);
    }
    private void RefreshCenterSceneBackdrop()
    {
        if(_centerSceneFrame is not {IsVisible:true,ActualWidth:>0,ActualHeight:>0} frame)return;
        var picture=(BitmapSource)_wallpaper;var cover=Cover(picture,frame.ActualWidth,frame.ActualHeight);var dpi=VisualTreeHelper.GetDpi(frame);
        foreach(var card in _centerScene.Children.OfType<Grid>()){
            var surface=(DockSurface)card.Children[0];if(surface.ActualWidth<=0)continue;
            var a=surface.TranslatePoint(new Point(),frame);var b=surface.TranslatePoint(new Point(surface.ActualWidth,surface.ActualHeight),frame);
            surface.CapturePixelScale=new DpiScale(Math.Max(.01,(b.X-a.X)*dpi.DpiScaleX/surface.ActualWidth),Math.Max(.01,(b.Y-a.Y)*dpi.DpiScaleY/surface.ActualHeight));
            double pad=24*dpi.DpiScaleX;int width=Math.Max(1,(int)Math.Ceiling((b.X-a.X)*dpi.DpiScaleX+2*pad)),height=Math.Max(1,(int)Math.Ceiling((b.Y-a.Y)*dpi.DpiScaleY+2*pad));
            var brush=new ImageBrush(picture){Stretch=Stretch.Fill,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(cover.X+(a.X-24)/frame.ActualWidth*cover.Width,cover.Y+(a.Y-24)/frame.ActualHeight*cover.Height,cover.Width*width/dpi.DpiScaleX/frame.ActualWidth,cover.Height*height/dpi.DpiScaleY/frame.ActualHeight)};
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawRectangle(brush,null,new Rect(0,0,width,height));
            var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();surface.Backdrop=bitmap;
            var screen=surface.PointToScreen(new Point());surface.BackdropScreenBounds=new Rect(screen.X-pad,screen.Y-pad,width,height);surface.InvalidateVisual();
        }
    }
}

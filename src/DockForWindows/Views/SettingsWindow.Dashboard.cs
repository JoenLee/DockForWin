using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DockForWindows.Models;

namespace DockForWindows.Views;

internal sealed partial class SettingsWindow
{
    // The dashboard hero shows the user's own pinned apps in a dock of the current material, floating on the
    // preview wallpaper. Glass refracts a render of that same wallpaper, so the sample is optically honest.
    protected override void DecorateDashboardHero(Grid scene)
    {
        var plate=new DockSurface{IsHitTestVisible=false,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom};
        var icons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false};
        scene.Children.Add(plate);scene.Children.Add(icons);
        void Backdrop()
        {
            if(!scene.IsLoaded||scene.ActualWidth<=0||scene.ActualHeight<=0||PresentationSource.FromVisual(scene) is null||scene.Background is not Brush wallpaper)return;
            var dpi=VisualTreeHelper.GetDpi(scene);int w=Math.Max(1,(int)Math.Ceiling(scene.ActualWidth*dpi.DpiScaleX)),h=Math.Max(1,(int)Math.Ceiling(scene.ActualHeight*dpi.DpiScaleY));
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawRectangle(wallpaper,null,new Rect(0,0,scene.ActualWidth,scene.ActualHeight));
            var bitmap=new RenderTargetBitmap(w,h,96*dpi.DpiScaleX,96*dpi.DpiScaleY,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();
            var origin=scene.PointToScreen(new Point());plate.BackdropScreenBounds=new Rect(origin.X,origin.Y,w,h);plate.Backdrop=bitmap;
        }
        void Refresh()
        {
            if(!scene.IsVisible)return;
            var p=new Palette(_draft);bool glass=_draft.Theme==DockTheme.Glass;
            plate.Fill=p.Surface;plate.Stroke=p.Line;plate.StrokeWidth=glass?0:p.BorderThickness;plate.Radius=p.CornerRadius;plate.GlassIntensity=p.GlassIntensity;
            plate.UseGlass=glass;plate.GlassOpacity=_draft.GlassOpacity;plate.GlassFrost=_draft.GlassFrost;plate.GlassDistortion=_draft.GlassDistortion;plate.GlassHighlight=_draft.GlassHighlight;plate.GlassHighlightWidth=_draft.GlassHighlightWidth;plate.GlassSideDarkness=_draft.GlassSideDarkness;plate.GlassTint=_draft.GlassTint;
            plate.Effect=p.ShadowEnabled?new DropShadowEffect{BlurRadius=Math.Max(10,p.ShadowBlur),ShadowDepth=4,Direction=270,Opacity=Math.Max(.18,p.ShadowOpacity)}:null;
            var sample=_draft.Copy();sample.IconSize=40;icons.Children.Clear();
            var items=_dock.Model.DisplayItems.Where(i=>i.IsPinned).Take(8).ToArray();
            foreach(var item in items)icons.Children.Add(new DockTile(item,p,sample){IsHitTestVisible=false,Focusable=false});
            plate.Width=Math.Max(140,items.Length*52+20);plate.Height=60;plate.Margin=new Thickness(0,0,0,20);icons.Margin=new Thickness(0,0,0,26);
            plate.InvalidateVisual();Dispatcher.BeginInvoke(Backdrop,DispatcherPriority.Render);
        }
        scene.Loaded+=(_,_)=>Refresh();scene.IsVisibleChanged+=(_,_)=>Refresh();scene.SizeChanged+=(_,_)=>Backdrop();
        LocationChanged+=(_,_)=>{if(scene.IsVisible)Dispatcher.BeginInvoke(Backdrop);};
    }
}

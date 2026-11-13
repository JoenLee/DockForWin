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
    // 通用 › 偏好: 开机自启 and 窗口外观 are single controls, so they sit on the 通用 page itself. Each one drives the
    // detailed control with the same meaning, so the draft is written (and saved) exactly once.
    protected override void BuildGeneralPreferences(Panel group)
    {
        var startup = new CheckBox { Content = "开机自启（保存后生效）", IsChecked = _draft.StartWithWindows };
        System.Windows.Automation.AutomationProperties.SetAutomationId(startup, "general-startup"); System.Windows.Automation.AutomationProperties.SetName(startup, "开机自启");
        void Startup() { if (!_ready || _updating) return; if (Setting<CheckBox>("start-with-windows") is { } detail) detail.IsChecked = startup.IsChecked; }
        startup.Checked += (_, _) => Startup(); startup.Unchecked += (_, _) => Startup();
        AddRow(group, startup);
        var theme = Segmented(new[] { "跟随系统", "浅色", "深色" }, (int)_draft.InterfaceTheme);
        System.Windows.Automation.AutomationProperties.SetAutomationId(theme, "general-interface-theme"); System.Windows.Automation.AutomationProperties.SetName(theme, "窗口外观");
        theme.SelectionChanged += (_, _) => { if (!_ready || _updating || theme.SelectedIndex < 0) return; if (Setting<ComboBox>("interface-theme") is { } detail) detail.SelectedIndex = theme.SelectedIndex; };
        AddRow(group, LabeledRow("窗口外观", theme));
    }

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

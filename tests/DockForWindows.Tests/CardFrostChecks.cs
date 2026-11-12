using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class CardFrostChecks
{
    private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    private static object Field(object value,string name)=>value.GetType().GetField(name,Flags)!.GetValue(value)!;
    private static object? Call(object value,string name,params object?[] args)=>value.GetType().GetMethod(name,Flags)!.Invoke(value,args);
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ControlCenterWindow? center=null;
        app.Startup+=async(_,_)=>{try{
            var settings=new DockSettings{Animations=true};settings.ControlCenter.MaterialMode=ControlCenterMaterialMode.ClassicBlur;
            var fake=(IControlCenterConnectivity)Activator.CreateInstance(typeof(ControlMotionChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true)!;
            center=new ControlCenterWindow(settings,connectivity:fake){DismissOnDeactivate=false};center.Show();for(int i=0;i<300&&(center.GlassBackdrop is null||center.Opacity<1);i++)await Task.Delay(20);await Task.Delay(400);
            var background=Field(center,"_classicBackdrop");var cards=Field(center,"_classicCards");var bgImage=(Image)Field(background,"_image");var cardImage=(Image)Field(cards,"_image");
            Check(((BlurEffect)bgImage.Effect).Radius==22,"shared outer background uses gentler 22 DIP Gaussian radius");
            Check(((BlurEffect)cardImage.Effect).Radius==40&&((FrameworkElement)cards).Opacity==.83,"card-only layer keeps 40 DIP Gaussian blur at 83% blend");
            Check(ReferenceEquals(bgImage.Source,cardImage.Source)&&ReferenceEquals(bgImage.Source,center.GlassBackdrop),"both layers share the same fresh captured bitmap");
            var host=(Grid)center.Content;Check(host.Children.Cast<object>().Count(v=>v.GetType().Name=="ClassicBlurBackdrop")==2,"one shared card layer serves all cards without per-card capture");
            var brush=(DrawingBrush)Field(center,"_cardFrostMask");var module=((IEnumerable)Field(center,"_modules")).Cast<object>().First();var surface=(DockSurface)Field(module,"Surface");var point=surface.TransformToAncestor(host).Transform(new Point(surface.ActualWidth/2,surface.ActualHeight/2));
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawRectangle(brush,null,new Rect(host.RenderSize));var bitmap=new RenderTargetBitmap((int)Math.Ceiling(host.ActualWidth),(int)Math.Ceiling(host.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
            byte Alpha(int x,int y){var p=new byte[4];bitmap.CopyPixels(new Int32Rect(x,y,1,1),p,4,0);return p[3];}
            Check(Alpha((int)point.X,(int)point.Y)>240&&Alpha(3,(int)(host.ActualHeight/2))==0,"strong blur mask covers cards and leaves the outer background untouched");
            var baseLayer=(FrameworkElement)background;
            Call(background,"SetFalloff",new Rect(100,100,240,400),1d);
            Check(Math.Abs(((FrameworkElement)Field(background,"_tint")).Opacity-.07)<.0001,"bright desktop receives only seven percent black tint");
            var falloff=(DrawingBrush)baseLayer.OpacityMask;
            Check(falloff.Viewport==new Rect(43,43,354,514),"background reaches zero 57 DIP beyond panel boundaries");
            var maskVisual=new DrawingVisual();using(var dc=maskVisual.RenderOpen())dc.DrawRectangle(falloff,null,new Rect(0,0,500,650));
            var maskBitmap=new RenderTargetBitmap(500,650,96,96,PixelFormats.Pbgra32);maskBitmap.Render(maskVisual);
            byte MaskAlpha(int x,int y){var pixel=new byte[4];maskBitmap.CopyPixels(new Int32Rect(x,y,1,1),pixel,4,0);return pixel[3];}
            Check(MaskAlpha(42,300)==0&&MaskAlpha(398,300)==0&&MaskAlpha(220,558)==0,"rendered mask is fully transparent beyond the specified falloff");
            Check(MaskAlpha(143,143)>MaskAlpha(297,457),"rendered diffusion is stronger at upper left and weaker at lower right");
            Call(background,"SetFalloff",new Rect(101,102,240,400),.3d);
            Check(ReferenceEquals(falloff,baseLayer.OpacityMask)&&((FrameworkElement)Field(background,"_tint")).Opacity==0,"moving panel reuses mask and dark desktop receives no tint");
            var capture=Field(center,"_capture");Call(center,"OpenModule",ControlModule.Wifi,false);await Task.Delay(700);Call(center,"HideDetails");await Task.Delay(700);
            Check(!(bool)Field(center,"_detailVisible")&&((IEnumerable)Field(center,"_cardFrostMasks")).Cast<object>().Any(),"shared card mask survives expand and return");
            settings.ControlCenter.MaterialMode=ControlCenterMaterialMode.LiquidGlass;Call(center,"ApplyPreview",settings);await Task.Delay(150);
            Check(ReferenceEquals(background,Field(center,"_classicBackdrop"))&&baseLayer.Visibility==Visibility.Visible&&((FrameworkElement)cards).Visibility==Visibility.Collapsed&&ReferenceEquals(bgImage.Source,center.GlassBackdrop),"Liquid Glass reuses live base blur and hides only card frost");
            for(int i=0;i<6;i++){settings.ControlCenter.MaterialMode=i%2==0?ControlCenterMaterialMode.LiquidGlass:ControlCenterMaterialMode.ClassicBlur;Call(center,"ApplyPreview",settings);await Task.Delay(100);}await Task.Delay(200);
            Check(ReferenceEquals(capture,Field(center,"_capture"))&&ReferenceEquals(cards,Field(center,"_classicCards")),"material switches reuse capture service and shared card layer");
            var type=typeof(DockSurface).Assembly.GetType("DockForWindows.Services.ControlCenterTaskbarEntry")!;using(var entry=(IDisposable)Activator.CreateInstance(type,Flags,null,new object[]{(Action)(()=>{})},null)!){var image=(Image)((Button)Field(entry,"_button")).Content;Check(image.Width==16&&image.Height==16,"network-side toggle symbol is reduced to 16 DIP");}
            center.Dismiss();await Task.Delay(1200);Check(bgImage.Source is null&&cardImage.Source is null&&((IDictionary)Field(center,"_cardFrostMasks")).Count==0,"closing releases both image bindings and card masks");result=0;
        }catch(Exception error){Console.Error.WriteLine(error);}finally{center?.Dismiss();await Task.Delay(900);app.Shutdown(result);}};app.Run();return result;
    }
}

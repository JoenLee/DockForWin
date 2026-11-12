using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class GlassAlignmentChecks
{
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o)!;
    private static IEnumerable<T> Tree<T>(DependencyObject root)where T:DependencyObject{if(root is T value)yield return value;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var x in Tree<T>(VisualTreeHelper.GetChild(root,i)))yield return x;}
    private static void Check(bool condition,string text){if(!condition)throw new Exception(text);Console.WriteLine("PASS "+text);}
    private static void Capture(Window w,string file){var p=w.PointToScreen(new Point());var d=VisualTreeHelper.GetDpi(w);using var b=new System.Drawing.Bitmap((int)Math.Ceiling(w.ActualWidth*d.DpiScaleX),(int)Math.Ceiling(w.ActualHeight*d.DpiScaleY));using var g=System.Drawing.Graphics.FromImage(b);g.CopyFromScreen((int)p.X,(int)p.Y,0,0,b.Size);b.Save(file);}
    private static double Variance(FrameworkElement e){var p=e.PointToScreen(new Point(e.ActualWidth/2,e.ActualHeight/2));using var b=new System.Drawing.Bitmap(100,40);using var g=System.Drawing.Graphics.FromImage(b);g.CopyFromScreen((int)p.X-50,(int)p.Y-20,0,0,b.Size);double sum=0,sq=0;for(int y=0;y<40;y++)for(int x=0;x<100;x++){double v=b.GetPixel(x,y).R;sum+=v;sq+=v*v;}return sq/4000-Math.Pow(sum/4000,2);}
    private static BitmapSource Pattern(int width,int height){var bytes=new byte[width*height*4];for(int y=0;y<height;y++)for(int x=0;x<width;x++){int i=(y*width+x)*4;byte v=(byte)((x/6+y/6)%2==0?240:20);bytes[i]=v;bytes[i+1]=v;bytes[i+2]=v;bytes[i+3]=255;}var image=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,bytes,width*4);image.Freeze();return image;}
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ControlCenterWindow? center=null;Window? pane=null;Window? fixture=null;
        app.Startup+=async(_,_)=>{
            try{
                var area=SystemParameters.WorkArea;fixture=new Window{WindowStyle=WindowStyle.None,ShowInTaskbar=false,Topmost=true,Width=area.Width,Height=area.Height,Left=area.Left,Top=area.Top,Background=new LinearGradientBrush(Colors.DarkSlateBlue,Colors.LightSalmon,22)};fixture.Show();
                var surface=new DockSurface{UseGlass=true,Fill=Brushes.White,GlassOpacity=1,GlassIntensity=0,GlassTint=0,GlassHighlight=0,GlassSideDarkness=0,Radius=85};
                pane=new Window{WindowStyle=WindowStyle.None,ShowInTaskbar=false,AllowsTransparency=true,Background=Brushes.Transparent,Topmost=true,Width=400,Height=160,Content=surface,Left=area.Left+100,Top=area.Top+100};pane.Show();await Task.Delay(200);var dpi=VisualTreeHelper.GetDpi(surface);surface.Backdrop=Pattern((int)Math.Ceiling(surface.ActualWidth*dpi.DpiScaleX),(int)Math.Ceiling(surface.ActualHeight*dpi.DpiScaleY));await Task.Delay(250);
                Check(VisualTreeHelper.GetChild(surface,0) is Grid,"Dock keeps original direct backdrop layer");double clear=Variance(surface);surface.GlassFrost=.6;surface.InvalidateVisual();await Task.Delay(300);double frosted=Variance(surface);Console.WriteLine($"Dock screen-pixel variance: clear={clear:0.0}, frost60={frosted:0.0}");Check(clear>1000&&frosted<clear*.25,"Dock frost visibly blurs real rendered pixels");Capture(pane,Path.Combine(directory,"Dock-frost.png"));pane.Close();pane=null;
                var settings=new DockSettings{GlassFrost=.55};settings.ControlCenter.OverrideMaterial=false;settings.ControlCenter.Geometry.PanelScale=.8;
                var fake=Activator.CreateInstance(typeof(ControlCenterPresentationChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true) as IControlCenterConnectivity;
                center=new ControlCenterWindow(settings,connectivity:fake){DismissOnDeactivate=false};center.Show();await Task.Delay(1100);
                foreach(double scale in new[]{.8,1.2}){
                    settings.ControlCenter.Geometry.PanelScale=scale;typeof(ControlCenterWindow).GetMethod("ApplyPreview",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(center,new object[]{settings});await Task.Delay(700);
                    foreach(var glass in Tree<DockSurface>(center).Where(s=>s.IsVisible&&s.Backdrop is not null)){
                        var image=Field<Image>(glass,"_backdropImage");var origin=image.PointToScreen(new Point());var end=image.PointToScreen(new Point(image.ActualWidth,image.ActualHeight));var bounds=glass.BackdropScreenBounds;
                        double error=(origin-bounds.TopLeft).Length+(end-bounds.BottomRight).Length;Console.WriteLine($"source alignment scale={scale}: {error:0.00}px; actual {origin} to {end}; expected {bounds}; image {image.RenderSize}");Check(error<3,"glass sample, blur and pane share the same screen coordinates");
                    }
                    var tile=Tree<Grid>(center).Single(g=>AutomationProperties.GetAutomationId(g)=="control-card-Volume");var host=(Grid)center.Content;var boundsTile=tile.TransformToAncestor(host).TransformBounds(new Rect(tile.RenderSize));Check(boundsTile.Left>=60,"whole card lies inside the safety gutter, not in its fade");Capture(center,Path.Combine(directory,$"Aligned-{scale}.png"));
                }
                result=0;
            }catch(Exception ex){Console.Error.WriteLine(ex);}
            finally{center?.Dismiss();await Task.Delay(350);pane?.Close();fixture?.Close();app.Shutdown();}
        };app.Run();return result;
    }
}

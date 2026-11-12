using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class InteractiveMotionChecks
{
    [DllImport("user32.dll")]private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")]private static extern void mouse_event(uint flags,uint x,uint y,uint data,nuint extra);
    private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private static object Field(object o,string n)=>o.GetType().GetField(n,Flags)!.GetValue(o)!;
    private static object? Call(object o,string n,params object?[] values)=>o.GetType().GetMethod(n,Flags)!.Invoke(o,values);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    private static byte[] Pixels(FrameworkElement icon){var parent=(FrameworkElement)VisualTreeHelper.GetParent(icon);var bitmap=new RenderTargetBitmap((int)parent.ActualWidth,(int)parent.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(parent);var offset=VisualTreeHelper.GetOffset(icon);var pixels=new byte[40*40*4];bitmap.CopyPixels(new Int32Rect((int)offset.X,0,40,40),pixels,160,0);return pixels;}
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Window? fixture=null;ControlCenterWindow? center=null;
        app.Startup+=async(_,_)=>{try{
            var assembly=typeof(DockSurface).Assembly;var iconType=assembly.GetType("DockForWindows.Views.ControlCenterIcon")!;
            var strip=new StackPanel{Orientation=Orientation.Horizontal};
            var icons=new[]{"Wifi","Bluetooth","Focus","Brightness","Volume","Airplane","Bell"}.Select(name=>{var icon=(FrameworkElement)Activator.CreateInstance(iconType,new object[]{name})!;icon.Width=icon.Height=40;iconType.GetProperty("Brush")!.SetValue(icon,Brushes.Black);strip.Children.Add(icon);return icon;}).ToArray();
            var slider=new Slider{Width=240,Minimum=0,Maximum=100,Value=50,Margin=new Thickness(20),VerticalAlignment=VerticalAlignment.Center};
            var stack=new StackPanel();stack.Children.Add(strip);stack.Children.Add(slider);fixture=new Window{Title="Isolated interactive motion",Content=stack,Width=420,Height=240,Topmost=true};fixture.Show();await Task.Delay(180);
            var pixels=icons.Select(Pixels).ToArray();
            foreach(var icon in icons)Call(icon,"Feedback",false);await Task.Delay(100);
            Check(icons.Select((icon,i)=>!Pixels(icon).SequenceEqual(pixels[i])).All(v=>v),"Wi-Fi, Bluetooth, moon, sun, volume, airplane and bell animate independently");
            for(int i=0;i<12;i++){foreach(var icon in icons)Call(icon,"Feedback",false);await Task.Delay(22);}await Task.Delay(500);
            Check(icons.Select((icon,i)=>Pixels(icon).SequenceEqual(pixels[i])).All(v=>v),"rapid symbol feedback restores the original SVG pixels exactly");
            var elasticType=assembly.GetType("DockForWindows.Views.ControlCenterElasticSlider")!;double deformation=0;bool released=false;
            var elastic=Activator.CreateInstance(elasticType,Flags,null,new object[]{slider,(Func<bool>)(()=>true),(Action<double,bool>)((value,release)=>{deformation=value;released=release;})},null)!;
            double Rubber(double value)=>(double)elasticType.GetMethod("Rubber",Flags)!.Invoke(null,new object[]{value})!;
            Check(Rubber(0)==0&&Rubber(55)>13&&Rubber(550)<22&&Rubber(550)>21.9&&Math.Abs(Rubber(-55)+Rubber(55))<.00001,"rubber banding is nonlinear, symmetric and asymptotically limited to 22 DIP");
            Call(elastic,"Update",117.25);double a=slider.Value;Call(elastic,"Update",117.75);Check(slider.Value>a&&slider.Value-a<1,"normal pointer movement changes the slider continuously without integer steps");
            Call(elastic,"Update",500d);Check(slider.Value==100&&deformation>21&&deformation<22,"outward drag holds system value at 100 and continues stretching visually");
            Call(elastic,"Update",-200d);Check(slider.Value==0&&deformation<-21,"lower edge mirrors upper edge without changing the card position");
            elasticType.GetField("_dragging",Flags)!.SetValue(elastic,true);Call(elastic,"Release");await Task.Delay(900);
            var scale=(ScaleTransform)Field(elastic,"_scale");var shift=(TranslateTransform)Field(elastic,"_shift");
            Check(released&&Math.Abs(scale.ScaleX-1)<.00001&&Math.Abs(shift.X)<.00001,"release spring settles the slider and notifies the shared contour layer");
            var track=(System.Windows.Controls.Primitives.Track)slider.Template.FindName("PART_Track",slider)!;var thumb=track.Thumb;var pointer=thumb.PointToScreen(new Point(thumb.ActualWidth/2,thumb.ActualHeight/2));
            SetCursorPos((int)pointer.X,(int)pointer.Y);mouse_event(2,0,0,0,0);await Task.Delay(35);Check(slider.IsMouseCaptured,"real slider press captures pointer independently of the thumb");
            var far=slider.PointToScreen(new Point(slider.ActualWidth+160,slider.ActualHeight/2));SetCursorPos((int)far.X,(int)far.Y);await Task.Delay(80);Check(slider.Value==100&&deformation>20,"real captured pointer stretches beyond the upper endpoint");mouse_event(4,0,0,0,0);await Task.Delay(35);
            pointer=thumb.PointToScreen(new Point(thumb.ActualWidth/2,thumb.ActualHeight/2));SetCursorPos((int)pointer.X,(int)pointer.Y);double displayed=shift.X;mouse_event(2,0,0,0,0);await Task.Delay(25);
            Console.WriteLine($"regrab captured={slider.IsMouseCaptured} before={displayed} after={shift.X}");Check(slider.IsMouseCaptured&&Math.Abs(shift.X-displayed)<2,"re-grabbing during spring recovery preserves displayed stretch");mouse_event(4,0,0,0,0);await Task.Delay(950);
            fixture.Close();fixture=null;
            foreach(var mode in new[]{ControlCenterMaterialMode.ClassicBlur,ControlCenterMaterialMode.LiquidGlass}){
                var settings=new DockSettings{Animations=true};settings.ControlCenter.Normalize();foreach(var kind in new[]{ControlModule.NightLight,ControlModule.Airplane,ControlModule.MobileHotspot}){Check(settings.ControlCenter.Modules.Count(m=>m.Module==kind)==1,"new module registered: "+kind);settings.ControlCenter.Modules.Single(m=>m.Module==kind).Visible=true;}settings.ControlCenter.MaterialMode=mode;
                var fake=(IControlCenterConnectivity)Activator.CreateInstance(typeof(ControlMotionChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true)!;
                center=new ControlCenterWindow(settings,connectivity:fake){DismissOnDeactivate=false};center.Show();
                for(int i=0;i<200&&!(bool)Field(center,"_entranceStarted");i++)await Task.Delay(10);
                Check((bool)Field(center,"_entranceStarted")&&center.GlassBackdrop is not null,mode+": entrance waits for a real background sample");
                await Task.Delay(130);Check(((TranslateTransform)Field(center,"_entrance")).Y<-.05&&center.Opacity>0,mode+": entrance remains in motion after the old short fade interval");
                await Task.Delay(750);Check(center.Opacity>.999&&Math.Abs(((TranslateTransform)Field(center,"_entrance")).Y)<.01,mode+": entrance reaches its exact open position");
                var header=(Grid)((Grid)Field(center,"_root")).Children[0];var point=header.PointToScreen(new Point(42,header.ActualHeight/2));double dpi=VisualTreeHelper.GetDpi(center).DpiScaleY;
                center.DismissOnDeactivate=true;SetCursorPos((int)point.X,(int)point.Y);mouse_event(2,0,0,0,0);await Task.Delay(35);SetCursorPos((int)point.X,(int)(point.Y-4*dpi));await Task.Delay(35);SetCursorPos((int)point.X,(int)(point.Y-18*dpi));await Task.Delay(50);
                Check((bool)Field(center,"_revealDragging")&&((TranslateTransform)Field(center,"_entrance")).Y<-8,mode+": real header drag follows the pointer independently of layout");
                SetCursorPos((int)point.X,(int)point.Y);await Task.Delay(30);mouse_event(4,0,0,0,0);await Task.Delay(1000);center.DismissOnDeactivate=false;
                Check(!(bool)Field(center,"_revealDragging")&&center.IsVisible&&center.Opacity>.999,mode+": real release settles open without outside-click dismissal");
                long samplesBeforeClose=center.CapturedFrames;var deferredTool=(Task<bool>)Call(center,"DismissForTool")!;for(int wait=0;wait<30&&Math.Abs(((TranslateTransform)Field(center,"_entrance")).Y)<.1;wait++)await Task.Delay(10);double before=((TranslateTransform)Field(center,"_entrance")).Y;center.TogglePresentation();double after=((TranslateTransform)Field(center,"_entrance")).Y;
                Check(!await deferredTool,"reversing dismissal cancels the pending external action");Check(before<-.05&&Math.Abs(before-after)<.1&&!((bool)Field(center,"_closing")),mode+": reversing dismissal keeps the same window and position");
                await Task.Delay(1000);Check(center.IsVisible&&center.Opacity>.999&&!((bool)Field(center,"_dismissRequested")),mode+": interrupted close returns to a working live window");
                Check(center.CapturedFrames>samplesBeforeClose,mode+": background acquisition remains live through close/reverse");
                foreach(var kind in new[]{ControlModule.NightLight,ControlModule.Airplane,ControlModule.MobileHotspot}){Call(center,"OpenModule",kind,false);await Task.Delay(400);Check((bool)Field(center,"_detailVisible"),mode+": native/real-state feature page opens: "+kind);Call(center,"HideDetails");await Task.Delay(400);}
                Call(center,"SettleReveal",false,(double?) -3);var closeTask=(Task<bool>)Call(center,"DismissForTool")!;await Task.Delay(1100);Check(closeTask.IsCompletedSuccessfully&&closeTask.Result&&!center.IsVisible,mode+": velocity-driven dismissal closes and releases lifetime resources");
            }
            result=0;
        }catch(Exception error){Console.Error.WriteLine(error);}finally{mouse_event(4,0,0,0,0);center?.Dismiss();await Task.Delay(1200);fixture?.Close();app.Shutdown(result);}};app.Run();return result;
    }
}

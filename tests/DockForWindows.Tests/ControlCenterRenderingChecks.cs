using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Views;

internal static class ControlCenterRenderingChecks
{
    internal static int ScaledBackdrop()
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var area=SystemParameters.WorkArea;
        var fixture=new Window{WindowStyle=WindowStyle.None,ShowInTaskbar=false,Topmost=true,Width=area.Width,Height=area.Height,Left=area.Left,Top=area.Top,Background=Brushes.RoyalBlue};
        var settings=new DockSettings();settings.ControlCenter.Geometry.PanelScale=1.25;settings.ControlCenter.Geometry.ComponentScale=1.15;
        var center=new ControlCenterWindow(settings){DismissOnDeactivate=false};int result=1;
        fixture.Loaded+=async(_,_)=>{
            try
            {
                center.Show();await Task.Delay(1600); // Wait for initial system status and entrance layout before checking bitmap reuse.
                async Task<System.Windows.Media.Imaging.BitmapSource> AwaitColor(bool blue)
                {
                    for(int i=0;i<40;i++){
                        await Task.Delay(100);
                        if(center.GlassBackdrop is not {} frame)continue;
                        var tile=FindCard(center);if(tile is null)continue;
                        var p=tile.PointToScreen(new Point(tile.ActualWidth*.5,tile.ActualHeight*.5));
                        int x=(int)(p.X-center.GlassBackdropBounds.X),y=(int)(p.Y-center.GlassBackdropBounds.Y);
                        if(x<0||y<0||x>=frame.PixelWidth||y>=frame.PixelHeight)continue;
                        var pixel=new byte[4];frame.CopyPixels(new Int32Rect(x,y,1,1),pixel,4,0);
                        if((blue?pixel[0]-pixel[2]:pixel[2]-pixel[0])>60)return frame;
                    }
                    throw new Exception("Background color did not follow the test window");
                }
                var first=await AwaitColor(true);fixture.Background=Brushes.OrangeRed;var next=await AwaitColor(false);
                if(!ReferenceEquals(first,next))throw new Exception($"Backdrop changed: {first.PixelWidth}x{first.PixelHeight} to {next.PixelWidth}x{next.PixelHeight}");
                Console.WriteLine("PASS 125% panel / 115% components: live blue-to-red application window sample; same bitmap reused");result=0;
            }
            catch(Exception ex){Console.WriteLine("FAIL "+ex);}
            finally{center.Dismiss();await Task.Delay(330);fixture.Close();app.Shutdown(result);}
        };
        app.Run(fixture);return result;
    }
    private static FrameworkElement? FindCard(DependencyObject root)
    {
        if(root is FrameworkElement element&&AutomationProperties.GetAutomationId(element)=="control-card-Volume")return element;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)if(FindCard(VisualTreeHelper.GetChild(root,i)) is {} found)return found;
        return null;
    }
    private static Button? ButtonNamed(DependencyObject node,string name)
    {
        if(node is Button b && b.IsVisible && AutomationProperties.GetName(b)==name)return b;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)
            if(ButtonNamed(VisualTreeHelper.GetChild(node,i),name) is Button found)return found;
        return null;
    }
    internal static int Run(bool shortRun=false)
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var area=SystemParameters.WorkArea;
        var fixture=new Window{Title="Dock 渲染验证",WindowStyle=WindowStyle.None,ShowInTaskbar=false,ShowActivated=false,Width=Math.Min(430,area.Width),Height=Math.Min(650,area.Height),Left=area.Right-Math.Min(430,area.Width),Top=area.Bottom-Math.Min(650,area.Height),Background=Brushes.SteelBlue};
        var center=new ControlCenterWindow(new DockSettings{GlassFrost=.23,GlassIntensity=1}){DismissOnDeactivate=false,ShowActivated=false};
        int result=1;
        fixture.Loaded+=async(_,_)=>{
            try
            {
                center.Show();using var process=Process.GetCurrentProcess();var timer=Stopwatch.StartNew();var memory=new List<double>();
                for(int tick=0;tick<=(shortRun?12:42);tick++)
                {
                    if(tick>0)await Task.Delay(shortRun?5000:10000);
                    fixture.Background=tick%2==0?Brushes.SteelBlue:Brushes.Maroon;
                    if(tick>0){var button=ButtonNamed(center,tick%2==1?"展开音量":"返回")??throw new Exception("Missing page navigation");button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
                    process.Refresh();double mb=process.PrivateMemorySize64/1048576d;memory.Add(mb);
                    if(tick%3==0)Console.WriteLine($"{timer.Elapsed.TotalSeconds:0}s private={mb:0} MB managed={GC.GetTotalMemory(false)/1048576d:0} MB handles={process.HandleCount} threads={process.Threads.Count} rendering={center.AnimationFrames/Math.Max(1,timer.Elapsed.TotalSeconds):0.0}/s capture={center.CapturedFrames/Math.Max(1,timer.Elapsed.TotalSeconds):0.0}/s");
                    if(mb>1536)throw new Exception("Memory exceeded 1536 MB");
                }
                if(center.CapturedFrames<420||memory.Last()-memory.Skip(3).Min()>240)throw new Exception("Capture stalled or private memory continued growing");
                Console.WriteLine(shortRun?"PASS one minute of capture and navigation":"PASS seven minutes of capture and navigation");result=0;
            }
            catch(Exception ex){Console.WriteLine("FAIL "+ex);}
            finally{center.Dismiss();await Task.Delay(350);fixture.Close();app.Shutdown(result);}
        };app.Run(fixture);return result;
    }
}

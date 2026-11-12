using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class NavigationLifetimeChecks
{
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    private static object? Field(object owner,string name)=>owner.GetType().GetField(name,Private)!.GetValue(owner);
    private static void Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Private)!.Invoke(owner,args);
    private static void Assert(bool value,string message){if(!value)throw new Exception(message);}
    internal static int Run()
    {
        int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};ControlCenterWindow? center=null;
        app.Startup+=async(_,_)=>{
            try{
                var fake=(IControlCenterConnectivity)Activator.CreateInstance(typeof(ControlMotionChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true)!;
                var settings=new DockSettings{GlassFrost=1,GlassIntensity=1,GlassDistortion=1,GlassHighlight=1};
                center=new ControlCenterWindow(settings,connectivity:fake){DismissOnDeactivate=false};center.Show();await Task.Delay(1200);
                var cards=((IEnumerable)Field(center,"_modules")!).Cast<object>().ToArray();
                ControlCenterLayout.MoveBefore((ControlCenterOptions)Field(center,"_options")!,ControlModule.Wifi,ControlModule.Projection);Call(center,"LayoutCards",true);
                Assert(cards.Any(c=>(double)Field(c,"MoveStarted")!>=0),"Fixture did not start animated reordering");await Task.Delay(800);
                foreach(var item in cards){var target=(Rect)Field(item,"Target")!;var position=(TranslateTransform)Field(item,"Position")!;Assert((double)Field(item,"MoveStarted")!<0&&Math.Abs(position.X-target.X)<.01&&Math.Abs(position.Y-target.Y)<.01,"Reordering failed to settle at the original grid targets");}
                for(int i=0;i<8;i++){if(i%2==0)Call(center,"OpenModule",ControlModule.Wifi,false);else Call(center,"HideDetails");await Task.Delay(65);}
                await Task.Delay(700);
                var details=(Grid)Field(center,"_details")!;
                var card=((IEnumerable)Field(center,"_modules")!).Cast<object>().Single(c=>(ControlModule)Field(c,"Kind")! ==ControlModule.Wifi);
                var body=(Grid)Field(card,"Body")!;var root=(Grid)Field(card,"Root")!;
                Assert(VisualTreeHelper.GetParent(body)==root,"Rapid return did not restore card content");
                Call(center,"OpenModule",ControlModule.Wifi,false);await Task.Delay(70);
                Assert(VisualTreeHelper.GetParent(body)!=root,"Fixture did not enter shared-element navigation");
                center.Dismiss();await Task.Delay(1000);
                Assert(!center.IsVisible,"Close did not finish");
                Assert(VisualTreeHelper.GetParent(body)==root,"Closing during navigation left the source content in a temporary host");
                Assert(VisualTreeHelper.GetParent((Grid)Field(center,"_detailContent")!)==details&&VisualTreeHelper.GetParent((Grid)Field(center,"_detailHeader")!)==details,"Closing did not restore detail content/header parents");
                Assert(center.GlassBackdrop is null&&Field(center,"_displayedSample") is null,"Closed window retained a leased frame");
                long frames=center.AnimationFrames,captures=center.CapturedFrames;await Task.Delay(300);
                Assert(frames==center.AnimationFrames&&captures==center.CapturedFrames,"Closed window still received render/capture updates");
                Console.WriteLine("PASS animated reordering settles on the grid; rapid reversals and close during morph restore actual control parents, release frame leases, and stop render/capture updates");result=0;
            }catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);}
            finally{center?.Dismiss();app.Shutdown(result);}
        };
        app.Run();return result;
    }
}

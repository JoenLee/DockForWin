using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;
using MediaState=DockForWindows.Services.MediaState;

internal static class FigmaVisualCorrectionChecks
{
    private static int passed;private static string output="";
    private static void Check(bool condition,string description){if(!condition)throw new Exception(description);passed++;Console.WriteLine("PASS "+description);}
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    internal static int Run(string folder)
    {
        output=folder;Directory.CreateDirectory(folder);NativeMethods.SetProcessDpiAwarenessContext(-4);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async(_,_)=>{try{await Verify();File.WriteAllText(Path.Combine(folder,"result.json"),JsonSerializer.Serialize(new{Passed=passed,Failed=0,Media="Explicit fixtures; commands do not control the system",Capture="CopyFromScreen",Assembly=typeof(WidgetHost).Assembly.Location}));app.Shutdown();}catch(Exception e){Console.WriteLine(e);File.WriteAllText(Path.Combine(folder,"failure.txt"),e.ToString());app.Shutdown(1);}};return app.Run();
    }
    private static void Screen(FrameworkElement element,string file)
    {
        var at=element.PointToScreen(new Point());var dpi=VisualTreeHelper.GetDpi(element);using var image=new System.Drawing.Bitmap((int)Math.Round(element.ActualWidth*dpi.DpiScaleX),(int)Math.Round(element.ActualHeight*dpi.DpiScaleY));using(var g=System.Drawing.Graphics.FromImage(image))g.CopyFromScreen((int)Math.Round(at.X),(int)Math.Round(at.Y),0,0,image.Size);image.Save(Path.Combine(output,file+".png"));
    }
    private static Dictionary<string,JsonElement> Nodes(FigmaWidgetView view)=>Field<Dictionary<string,JsonElement>>(view,"_nodes");
    private static TextBlock Text(FigmaWidgetView view,string originalName)
    {
        var node=Nodes(view).Values.First(n=>n.GetProperty("n").GetString()==originalName);return Field<Dictionary<string,TextBlock>>(view,"_text")[node.GetProperty("id").GetString()!];
    }
    private static void Fits(FigmaWidgetView view,string name)
    {
        var label=Text(view,name);var text=new FormattedText(label.Text,System.Globalization.CultureInfo.GetCultureInfo("en-US"),FlowDirection.LeftToRight,new Typeface(label.FontFamily,label.FontStyle,label.FontWeight,label.FontStretch),label.FontSize,label.Foreground,1);
        Check(text.WidthIncludingTrailingWhitespace<=label.Width+1,"full text fits its real slot: "+name+" = "+label.Text);
        var owner=(FrameworkElement)label.Parent;var region=label.TransformToAncestor(owner).TransformBounds(new Rect(0,0,text.WidthIncludingTrailingWhitespace,Math.Min(label.ActualHeight,owner.ActualHeight)));
        Check(region.Left>=-.5&&region.Right<=owner.ActualWidth+1,"text stays inside the allocated layer: "+name);
    }
    private static async Task Verify()
    {
        using var art=Application.GetResourceStream(new Uri("/DockForWindows;component/Assets/FigmaWidgets/assets/ipod/a9e67.png",UriKind.Relative))!.Stream;using var bytes=new MemoryStream();art.CopyTo(bytes);
        var now=new DateTime(2026,11,21,12,0,0,DateTimeKind.Utc);var days=new[]{new WidgetForecastDay(new(2026,11,21),0,21,11),new WidgetForecastDay(new(2026,11,22),0,20,10)};
        var weather=new WidgetWeatherSnapshot(15,0,false,now,DateTimeOffset.UtcNow,days){TimeZone="Asia/Shanghai",UtcOffsetSeconds=28800,SolarDays=new[]{new WidgetSolarDay(new(2026,11,21),new DateTime(2026,11,21,6,26,0),new DateTime(2026,11,21,17,18,0)),new WidgetSolarDay(new(2026,11,22),new DateTime(2026,11,22,6,27,0),new DateTime(2026,11,22,17,18,0))}};
        var media=new MediaState("Blinding Lights","The Weeknd — After Hours",false,true,true,true,bytes.ToArray(),TimeSpan.Zero,TimeSpan.FromSeconds(200),true);
        var data=new WidgetRenderData(new(),new(){City="Current location",TimeZone="China Standard Time"},weather,media,Preview:false,MediaCommand:_=>{}){Now=now,MotionEnabled=false,Seek=_=>{}};
        var panel=new StackPanel{Background=WidgetBrushes.Get("#E5E9E8"),Margin=new Thickness(16)};var caption=new TextBlock{Text="修正检查 · 固定验收数据，媒体控制不操作系统",FontSize=11,Margin=new Thickness(0,0,0,12)};panel.Children.Add(caption);
        var hosts=new List<WidgetHost>();foreach(var id in new[]{"weather-figma-atmosphere-rose","music-figma-ipod-graphite-wide","music-figma-ipod-horizontal","music-figma-ipod-aluminum"}){
            var host=new WidgetHost(WidgetRegistry.Get(id),2.5){Margin=new Thickness(0,0,0,12)};host.SetMaterial(WidgetMaterial.Light,new(){Shadow=0});host.SetData(data);hosts.Add(host);panel.Children.Add(host);
        }
        var window=new Window{Content=panel,SizeToContent=SizeToContent.WidthAndHeight,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,Left=35,Top=20};window.Show();await Task.Delay(300);window.UpdateLayout();
        foreach(var host in hosts){var view=WidgetMusicView.Elements<FigmaWidgetView>(host).Single();Check(Math.Abs(view.NativeCanvasSize.Width/view.NativeCanvasSize.Height-host.Width/host.Height)<.00001,"root fills native card without letterboxing: "+host.Definition.Id);Screen(host,host.Definition.Id);}
        var sky=WidgetMusicView.Elements<FigmaWidgetView>(hosts[0]).Single();foreach(var name in new[]{"Location name","Next event","Next event time","Sunrise value","Sunset value"})Fits(sky,name);
        foreach(var host in hosts.Skip(1).Take(2)){var view=WidgetMusicView.Elements<FigmaWidgetView>(host).Single();var label=Text(view,"Song title");var layer=(FrameworkElement)label.Parent;var parent=(FrameworkElement)layer.Parent;var center=label.TranslatePoint(new Point(label.Width/2,0),parent);Check(Math.Abs(center.X-parent.ActualWidth/2)<1,"live song title stays centered: "+host.Definition.Id);Check(label.Text=="Blinding Lights","live title is intact: "+host.Definition.Id);}
        Screen(panel,"four-widget-window");window.Close();await Task.Delay(60);
        var recordHost=new WidgetHost(WidgetRegistry.Get("music-figma-record"),2.5);recordHost.SetMaterial(WidgetMaterial.Light);recordHost.SetData(data);var recordWindow=new Window{Content=recordHost,SizeToContent=SizeToContent.WidthAndHeight,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,Left=45,Top=80};recordWindow.Show();await Task.Delay(200);recordWindow.UpdateLayout();
        var recordView=WidgetMusicView.Elements<FigmaWidgetView>(recordHost).Single();var record=WidgetMusicView.Elements<WidgetRecordPlayer>(recordView).Single();Check(Math.Abs(record.NeedleAngle+28)<.0001,"rest angle comes from the actual +23 to -5 degree design matrices");NeedlePose(recordView,"initial paused pose matches source");Screen(recordHost,"needle-parked");
        var moving=data with{MotionEnabled=true};for(int i=0;i<8;i++){recordHost.SetData(moving with{Media=media with{Playing=i%2==0}});await Task.Delay(35);}await Task.Delay(800);NeedlePose(recordView,"rapid reversals finish at the source rest pose");Check(!record.IsSpinning,"paused record has released its rendering handler");Screen(recordHost,"needle-after-reversals");
        recordHost.SetData(data with{Media=media with{Playing=true}});await Task.Delay(100);Check(Math.Abs(record.NeedleAngle)<.0001,"playing needle returns to source play pose with reduced motion");recordHost.SetData(data);await Task.Delay(60);NeedlePose(recordView,"final pause parks correctly");recordWindow.Close();
    }
    private static void NeedlePose(FigmaWidgetView view,string description)
    {
        var node=Nodes(view).Values.First(n=>n.GetProperty("n").GetString()=="Tonearm");var layer=Field<Dictionary<string,Canvas>>(view,"_layers")[node.GetProperty("id").GetString()!];var scene=Field<Canvas>(view,"_scene");var transform=layer.TransformToAncestor(scene);
        double a=.9961947202682495,b=.08715574443340302,c=-.08715574443340302,d=.9961947202682495,x=326.5898742675781,y=41.895328521728516;
        double worst=0;foreach(var point in new[]{new Point(),new Point(40,0),new Point(0,228)}){var expected=new Point(a*point.X+b*point.Y+x,c*point.X+d*point.Y+y);worst=Math.Max(worst,(transform.Transform(point)-expected).Length);}
        Check(worst<.03,description+"; error="+worst.ToString("0.0000")+" design pixels");
    }
}

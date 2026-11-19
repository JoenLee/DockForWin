using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class WidgetGlassChecks
{
    private static int _passed;
    private static object Field(object owner,string name)=>owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);_passed++;Console.WriteLine("PASS "+message);}
    internal static int Run(string output)
    {
        NativeMethods.SetProcessDpiAwarenessContext(new nint(-4));Directory.CreateDirectory(output);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async(_,_)=>{try{await Verify(output);File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=_passed,Failed=0}));app.Shutdown();}catch(Exception e){Console.WriteLine(e);File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());app.Shutdown(1);}};return app.Run();
    }
    private static async Task WaitFor(Func<bool> ready)
    {
        for(int n=0;n<60&&!ready();n++)await Task.Delay(100);
        Check(ready(),"live background reaches the glass surface");
    }
    private static async Task Verify(string output)
    {
        var root=new Canvas{Width=540,Height=375,Background=Brushes.Transparent};
        var rear=new Window{Title="小组件背景验证",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,Width=root.Width,Height=root.Height,Left=SystemParameters.WorkArea.Left+50,Top=SystemParameters.WorkArea.Top+70,ShowInTaskbar=false,ShowActivated=false,Topmost=true,Background=new SolidColorBrush(Color.FromRgb(210,70,30))};
        var front=new Window{Title="小组件液态玻璃验证",WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ResizeMode=ResizeMode.NoResize,Width=root.Width,Height=root.Height,Left=rear.Left,Top=rear.Top,ShowInTaskbar=false,ShowActivated=false,Topmost=true,Content=root};
        var hosts=new List<WidgetHost>();
        try{
            int index=0;foreach(string id in new[]{"weather-small","calendar-day","clock-classic","reminder-list","notes-small","music-small"}){
                var definition=WidgetRegistry.Get(id);var host=new WidgetHost(definition,2.5){LayoutTransform=new ScaleTransform(.85,.85)};host.SetMaterial(WidgetMaterial.Glass);host.SetContent(WidgetContent.Render(definition,new(new(),new())));Canvas.SetLeft(host,24+index%3*172);Canvas.SetTop(host,24+index/3*172);root.Children.Add(host);hosts.Add(host);index++;}
            rear.Show();front.Show();front.UpdateLayout();
            await WaitFor(()=>hosts.All(h=>((DockSurface)Field(h,"_glass")).Backdrop is BitmapSource));
            Check(WidgetGlassBackdrop.ActiveGroups==1,"six widget categories share one background capture");
            var surfaces=hosts.Select(h=>(DockSurface)Field(h,"_glass")).ToArray();
            Check(surfaces.All(s=>ReferenceEquals(s.Backdrop,surfaces[0].Backdrop)),"all six glass surfaces share one uploaded bitmap");
            foreach(var host in hosts){var surface=(DockSurface)Field(host,"_glass");Check(surface.UseGlass&&surface.FusedOptics&&surface.ConstrainFrostToOutline&&surface.GlassIntensity>0&&surface.GlassHighlight>0,"control center optics enabled for "+host.Definition.Kind);Check(ReferenceEquals(surface.Clip,host.Outline),"glass keeps the existing widget outline for "+host.Definition.Kind);}
            Color Pixel(){var source=(BitmapSource)surfaces[0].Backdrop!;byte[] p=new byte[4];source.CopyPixels(new Int32Rect(source.PixelWidth/2,source.PixelHeight/2,1,1),p,4,0);return Color.FromRgb(p[2],p[1],p[0]);}
            await WaitFor(()=>Pixel().R>Pixel().B+50);var firstFrame=surfaces[0].Backdrop;
            Capture(root,Path.Combine(output,"glass-warm.png"));
            rear.Background=new SolidColorBrush(Color.FromRgb(30,75,210));
            await WaitFor(()=>Pixel().B>Pixel().R+50);
            Check(ReferenceEquals(firstFrame,surfaces[0].Backdrop),"changing live background updates pixels without reallocating the shared bitmap");Capture(root,Path.Combine(output,"glass-cool.png"));
            var probeOrigin=front.PointToScreen(new Point(80,80));var probe=new System.Drawing.Rectangle((int)probeOrigin.X,(int)probeOrigin.Y,16,16);double probeDpi=VisualTreeHelper.GetDpi(front).DpiScaleX;
            nint frontHandle=new WindowInteropHelper(front).Handle,rearHandle=new WindowInteropHelper(rear).Handle;
            async Task<uint> Probe(bool exclude){using var capture=new GlassBackdropService{ExcludedWindows=exclude?new HashSet<nint>{rearHandle}:null};return await Task.Run(()=>{var raw=capture.ReadPixels(frontHandle,probe,probeDpi)??throw new Exception("No sample for exclusion regression");int pad=(int)Math.Ceiling(24*probeDpi);return BitConverter.ToUInt32(raw.Pixels,((pad+8)*raw.Width+pad+8)*4);});}
            uint included=await Probe(false),excluded=await Probe(true);
            Check(included!=excluded,"widget-only exclusions remove peer windows while default control-center sampling still includes them");
            var group=(WidgetGlassBackdrop)Field(hosts[0],"_capture");await Task.Delay(500);long before=group.UploadedFrames;await Task.Delay(500);Check(group.UploadedFrames==before,"an unchanged background is not uploaded again");
            var content=hosts.Select(h=>((Grid)Field(h,"_content")).Children[0]).ToArray();
            foreach(var host in hosts)host.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseEnterEvent});await Task.Delay(350);
            for(int i=0;i<hosts.Count;i++)Check(Math.Abs(hosts[i].HoverScale-1)<.000001&&ReferenceEquals(content[i],((Grid)Field(hosts[i],"_content")).Children[0]),"hover keeps size and content stable for "+hosts[i].Definition.Kind);
            hosts[0].SetMaterial(WidgetMaterial.Glass,new(){PanelScale=1.15});hosts[0].SetGestureActive(true);hosts[0].SetGestureActive(false);await Task.Delay(350);Check(Math.Abs(hosts[0].VisualScale-1.15)<.000001,"manual panel scaling remains independent of hover and drag");Check(ReferenceEquals(firstFrame,surfaces[0].Backdrop),"rescaling a widget preserves the shared capture texture");
            foreach(var host in hosts)host.SetMaterial(WidgetMaterial.Light);Check(WidgetGlassBackdrop.ActiveGroups==0&&surfaces.All(s=>s.Backdrop is null),"switching every widget to a solid material releases capture and frame references");
            foreach(var host in hosts)host.SetMaterial(WidgetMaterial.Glass);await WaitFor(()=>surfaces.All(s=>s.Backdrop is BitmapSource));Check(WidgetGlassBackdrop.ActiveGroups==1,"returning to glass creates exactly one capture group");
            front.Close();await Task.Delay(200);Check(WidgetGlassBackdrop.ActiveGroups==0&&surfaces.All(s=>s.Backdrop is null),"closing the widget host stops sampling and releases its shared frames");
            foreach(string language in new[]{"zh-CN","en-US"}){UiText.SetLanguage(language);var options=new WidgetOptions{Material=WidgetMaterial.Glass};var page=new WidgetGalleryPage(options);var selector=Tree<SettingsSegmentedControl>(page).Single(x=>AutomationProperties.GetAutomationId(x)=="widget-material");Check(selector.Items.Count==3&&selector.SelectedIndex==2&&options.Material==WidgetMaterial.Glass,"gallery exposes three materials and preserves glass selection in "+language);}
        }finally{front.Close();rear.Close();}
    }
    private static IEnumerable<T> Tree<T>(DependencyObject root)where T:DependencyObject
    {
        if(root is T item)yield return item;foreach(var node in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var child in Tree<T>(node))yield return child;
    }
    private static void Capture(FrameworkElement target,string path)
    {
        var bmp=new RenderTargetBitmap((int)Math.Ceiling(target.ActualWidth),(int)Math.Ceiling(target.ActualHeight),96,96,PixelFormats.Pbgra32);bmp.Render(target);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using var file=File.Create(path);encoder.Save(file);
    }
}

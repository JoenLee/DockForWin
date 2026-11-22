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
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class DocumentClockChecks
{
    private static int passed;
    private static string output = "";
    private static void Check(bool condition, string label) { if (!condition) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
    private static IEnumerable<T> Tree<T>(DependencyObject root) where T : DependencyObject {
        if (root is T item) yield return item;
        for (int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(var child in Tree<T>(VisualTreeHelper.GetChild(root,i))) yield return child;
    }
    private static string Label(DependencyObject owner,string id) => Tree<TextBlock>(owner).Single(t=>AutomationProperties.GetAutomationId(t)==id).Text;
    internal static int Run(string folder)
    {
        NativeMethods.SetProcessDpiAwarenessContext(-4); output=folder;Directory.CreateDirectory(folder);
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        app.Startup+=async(_,_)=>{try { Models();await Visuals();File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new {Passed=passed,Failed=0,Capture="Actual WPF window, CopyFromScreen",Fixture="2026-11-20 17:41:20 UTC. Alarm is explicitly preview-only; unavailable on desktop."}));app.Shutdown();}
            catch(Exception error){Console.WriteLine(error);File.WriteAllText(Path.Combine(output,"failure.txt"),error.ToString());app.Shutdown(1);}};return app.Run();
    }
    private static void Models()
    {
        Check(DocumentClockStyles.All.Count==6,"only the six screenshot designs are registered");
        Check(!WidgetRegistry.Definitions.Any(d=>d.Id.StartsWith("clock-css-")),"all twelve discarded CSS-only variants are removed");
        var settings=new DockSettings();
        foreach(var style in DocumentClockStyles.All){var instance=WidgetRegistry.CreateInstance(style.Id,WidgetMaterial.Light);
            Check(instance.Size==new WidgetSize(style.Wide?4:2,2),"native grid size "+style.Id);
            Check(instance.Source.TimeZone==(style.Content==DocumentClockContent.City?"GMT Standard Time":TimeZoneInfo.Local.Id),"reference time zone default "+style.Id);
            settings.Widgets.Instances.Add(instance);}
        var service=new SettingsService(Path.Combine(output,"profile"));service.Save(settings);var loaded=service.Load();
        Check(loaded.Widgets.Instances.Count==6,"six definitions persist through existing settings service");
        Check(WidgetRegistry.Get("clock-classic").Id=="clock-classic"&&WidgetRegistry.Get("music-figma-record").Id=="music-figma-record","retained clocks and Figma music remain registered");
        Check(ReferenceEquals(WidgetGeometry.Get(180,180,2.5),WidgetGeometry.Get(180,180,2.5,20)),"existing widget outline cache remains unchanged");
    }
    private static WidgetHost Host(DocumentClockStyle style,bool fixture=true,bool preview=true)
    {
        var host=new WidgetHost(style.Definition,2.5);host.SetMaterial(WidgetMaterial.Light,new(){Shadow=0});
        host.SetData(new(new(),DocumentClockStyles.DefaultSource(style.Id)!,Preview:preview){Now=fixture?new DateTime(2026,11,20,17,41,20,DateTimeKind.Utc):null});return host;
    }
    private static void Screen(FrameworkElement element,string name)
    {
        var at=element.PointToScreen(new Point());var dpi=VisualTreeHelper.GetDpi(element);
        using var bitmap=new System.Drawing.Bitmap((int)Math.Round(element.ActualWidth*dpi.DpiScaleX),(int)Math.Round(element.ActualHeight*dpi.DpiScaleY));
        using(var g=System.Drawing.Graphics.FromImage(bitmap))g.CopyFromScreen((int)Math.Round(at.X),(int)Math.Round(at.Y),0,0,bitmap.Size);
        bitmap.Save(Path.Combine(output,name+".png"),System.Drawing.Imaging.ImageFormat.Png);
    }
    private static async Task Visuals()
    {
        var canvas=new Canvas{Width=1509,Height=804,Background=WidgetBrushes.Get("#808080")};
        var hosts=new List<WidgetHost>();
        double[] x={40,406,772,1138,40,772};double[] y={66,66,66,66,432,432};
        for(int i=0;i<6;i++){
            var style=DocumentClockStyles.All[i];var host=Host(style);hosts.Add(host);
            var label=new TextBlock{Text=style.ReferenceName,FontFamily=new FontFamily("Arial"),FontSize=18,Foreground=WidgetBrushes.Get("#CBCBCB")};
            Canvas.SetLeft(label,x[i]);Canvas.SetTop(label,y[i]-(i<4?34:32));canvas.Children.Add(label);
            var box=new Viewbox{Width=style.Wide?702:336,Height=336,Stretch=Stretch.Fill,Child=host};Canvas.SetLeft(box,x[i]);Canvas.SetTop(box,y[i]);canvas.Children.Add(box);
        }
        double scale=Math.Min(1,Math.Min((SystemParameters.WorkArea.Width-60)/1509,(SystemParameters.WorkArea.Height-90)/804));
        var window=new Window{Title="六款时钟 · 参考版式实际运行",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,
            Content=new Viewbox{Child=canvas,Stretch=Stretch.Uniform},Width=1509*scale,Height=804*scale,Left=20,Top=30,Topmost=true,ShowInTaskbar=false,ShowActivated=false};
        try{
            window.Show();await Task.Delay(650);window.UpdateLayout();Screen((FrameworkElement)window.Content,"reference-layout-screen");
            for(int i=0;i<6;i++)Screen(hosts[i],DocumentClockStyles.All[i].Id);
            Check(Label(hosts[2],"reference-clock-time")=="9:41","digital fixture matches reference time");
            Check(Label(hosts[2],"reference-clock-weekday")=="FRIDAY"&&Label(hosts[2],"reference-clock-date")=="November 20","weekday/date placement uses correct zone date");
            Check(Label(hosts[2],"reference-clock-location")=="Cupertino · PST","digital location and real zone abbreviation");
            Check(Label(hosts[3],"reference-alarm-time")=="7:00"&&Label(hosts[3],"reference-alarm-remaining")=="In 21 hr, 19 min","explicit alarm design fixture matches screenshot");
            Check(Tree<DocumentClockDial>(hosts[4]).Count()==4,"world card contains four actual analog dials");
            Check(new[]{"9:41","12:41","17:41","2:41"}.SequenceEqual(Enumerable.Range(0,4).Select(i=>Label(hosts[4],"reference-world-time-"+i))),"world times derive from one UTC instant");
            Check(Label(hosts[4],"reference-world-day-3")=="Tomorrow","world day rollover is relative to source zone");
            Check(Label(hosts[5],"reference-city-time")=="17:41"&&Label(hosts[5],"reference-city-offset")=="8 hours ahead of Cupertino","city digital/analog/offset use actual time zones");
            foreach(var host in hosts){
                var view=Tree<DocumentClockView>(host).Single();Check(!view.TimerRunning,"fixture does not start live timer "+host.Definition.Id);
                foreach(var text in Tree<TextBlock>(view))Check(Canvas.GetLeft(text)+text.Width<=view.ReferenceFrame.Width+.1,"text remains inside reference frame "+AutomationProperties.GetAutomationId(text));
            }
            var outer=(Viewbox)window.Content;canvas.Children.Clear();
            var live=Host(DocumentClockStyles.All[0],false,false);Canvas.SetLeft(live,40);Canvas.SetTop(live,40);canvas.Children.Add(live);await Task.Delay(250);window.UpdateLayout();
            var liveView=Tree<DocumentClockView>(live).Single();var dial=Tree<DocumentClockDial>(live).Single();double angle=dial.SecondAngle;await Task.Delay(300);
            Check(dial.Animating&&Math.Abs(dial.SecondAngle-angle)>.5,"live second hand progresses using real clock");
            live.Visibility=Visibility.Hidden;await Task.Delay(80);Check(!liveView.TimerRunning&&!dial.Animating,"hidden clock suspends timer and hand animation");
            live.Visibility=Visibility.Visible;await Task.Delay(80);Check(liveView.TimerRunning&&dial.Animating,"visible clock resumes from actual time");
            canvas.Children.Clear();await Task.Delay(80);Check(!liveView.TimerRunning&&!dial.Animating,"removed clock releases timer and animations");
            var desktopAlarm=Host(DocumentClockStyles.All[3],false,false);canvas.Children.Add(desktopAlarm);await Task.Delay(60);window.UpdateLayout();
            Check(Label(desktopAlarm,"reference-alarm-time")=="—:—"&&Label(desktopAlarm,"reference-alarm-day")=="未接入系统闹钟","desktop does not fake system alarm data");
            canvas.Children.Clear();
            var native=new Canvas{Width=842,Height=500,Background=WidgetBrushes.Get("#808080")};
            for(int i=0;i<6;i++){var host=Host(DocumentClockStyles.All[i],false,i==3);Canvas.SetLeft(host,i<4?20+i*202:i==4?20:424);Canvas.SetTop(host,i<4?28:260);native.Children.Add(host);}
            outer.Child=native;window.Width=842;window.Height=500;await Task.Delay(450);window.UpdateLayout();Screen(outer,"native-grid-live-screen");
            foreach(var host in native.Children.OfType<WidgetHost>().ToArray())foreach(var size in WidgetSizes.For(host.Definition)){
                host.Resize(size);host.SetData(new(new(),DocumentClockStyles.DefaultSource(host.Definition.Id)!));window.UpdateLayout();
                var view=Tree<DocumentClockView>(host).Single();var bounds=view.TransformToAncestor(host).TransformBounds(new Rect(view.RenderSize));
                Check(bounds.Left>=-.1&&bounds.Right<=host.Width+.1&&bounds.Bottom<=host.Height+.1,"supported grid resize stays bounded "+host.Definition.Id+" "+size);
            }
            native.Children.Clear();
            var shell=new SettingsShellWindow(new DockSettings(),(_,_)=>{},9);window.Resources=shell.Resources;
            var gallery=new WidgetGalleryPage(new WidgetOptions()){Width=810,Height=620};outer.Child=gallery;window.Width=842;window.Height=640;
            gallery.ShowCategory(WidgetKind.Clock);await Task.Delay(250);window.UpdateLayout();
            var carousel=Tree<WidgetCarousel>(gallery).Single(c=>AutomationProperties.GetAutomationId(c)=="clock-carousel");
            Check(carousel.Hosts.Count(h=>DocumentClockStyles.Find(h.Definition.Id)!=null)==6,"six replacement designs appear in existing clock gallery");
            var sourceEditor=Tree<Expander>(gallery).Single(e=>AutomationProperties.GetAutomationId(e)=="widget-source-clock");sourceEditor.IsExpanded=true;await Task.Delay(150);window.UpdateLayout();
            foreach(var style in DocumentClockStyles.All){int index=carousel.Hosts.ToList().FindIndex(h=>h.Definition.Id==style.Id);carousel.Select(index,false);await Task.Delay(90);window.UpdateLayout();
                var zone=Tree<ComboBox>(gallery).Single(c=>AutomationProperties.GetAutomationId(c)=="widget-source-zone-clock");
                Check((string?)zone.SelectedItem==TimeZoneInfo.FindSystemTimeZoneById(DocumentClockStyles.DefaultSource(style.Id)!.TimeZone).DisplayName,"gallery source editor uses reference zone "+style.Id);}
            carousel.Select(carousel.Hosts.ToList().FindIndex(h=>h.Definition.Id=="clock-reference-digital"),false);await Task.Delay(200);window.UpdateLayout();Screen(outer,"clock-gallery-screen");
            outer.Child=null;shell.ClosePermanently();
        }finally{window.Close();}
    }
}

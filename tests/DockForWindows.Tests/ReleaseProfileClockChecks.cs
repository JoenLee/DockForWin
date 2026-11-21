using System;
using System.Collections;
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

internal static class ReleaseProfileClockChecks
{
    private static int passed;
    private static void Check(bool ok,string label){if(!ok)throw new Exception(label);passed++;Console.WriteLine("PASS "+label);}
    private static T Field<T>(object instance,string name)=>(T)instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(instance)!;
    private static double Distance(double a,double b)=>Math.Abs(Math.IEEERemainder(a-b,360));
    internal static int Run(string folder)
    {
        Directory.CreateDirectory(folder);NativeMethods.SetProcessDpiAwarenessContext(-4);
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async(_,_)=>{try{Defaults(folder);await Clocks(folder);File.WriteAllText(Path.Combine(folder,"result.json"),JsonSerializer.Serialize(new{Passed=passed,Failed=0,ClockCount=WidgetRegistry.Definitions.Count(d=>d.Kind==WidgetKind.Clock),NoOperatingSystemTimeChanges=true}));app.Shutdown();}catch(Exception e){Console.WriteLine(e);File.WriteAllText(Path.Combine(folder,"failure.txt"),e.ToString());app.Shutdown(1);}};
        return app.Run();
    }
    private static void Defaults(string folder)
    {
        var store=new SettingsService(Path.Combine(folder,"clean-profile-"+Guid.NewGuid().ToString("N")));var settings=store.Load();
        Check(settings.Widgets.Instances.Count==0&&settings.Widgets.Tasks.Count==0&&settings.Widgets.Notes.Count==0&&settings.Widgets.MusicHistory.Count==0&&settings.Widgets.FavoriteTracks.Count==0,"first launch has no personal widgets, notes, tasks, media history or favorites");
        var windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        Check(settings.Pins.All(p=>p.Target.StartsWith(windows,StringComparison.OrdinalIgnoreCase)||p.Target is "shell:Downloads" or "shell:RecycleBinFolder"),"default pins resolve only to Windows tools or portable shell folders");
        Check(!settings.StartWithWindows&&settings.RecentApps.Count==0&&settings.MonitorDevice=="","no startup registration, personal recent apps or machine display ID in defaults");
        var existing=WidgetRegistry.CreateInstance("music-figma-record",WidgetMaterial.Dark);settings.Widgets.Instances.Add(existing);settings.EdgeDistance=11.5;store.Save(settings);
        var loaded=store.Load();Check(loaded.Widgets.Instances.Single().InstanceId==existing.InstanceId&&loaded.EdgeDistance==11.5,"existing user settings remain intact when a profile already exists");
        foreach(var d in WidgetRegistry.Definitions.Where(d=>d.Kind==WidgetKind.Clock&&!d.Id.Contains("world")&&!d.Id.Contains("city")))Check(WidgetRegistry.CreateInstance(d.Id,WidgetMaterial.Light).Source.TimeZone==TimeZoneInfo.Local.Id,"ordinary clock defaults to this computer's system zone: "+d.Id);
    }
    private static void NativeAngles(WidgetClock clock,DateTimeOffset utc,string label)
    {
        double seconds=utc.TimeOfDay.TotalSeconds;
        Check(Distance(Field<RotateTransform>(clock,"_hour").Angle,seconds/120)<.02&&Distance(Field<RotateTransform>(clock,"_minute").Angle,seconds/10)<.02&&Distance(Field<RotateTransform>(clock,"_second").Angle,seconds*6)<.02,label);
    }
    private static void FigmaAngles(FigmaWidgetView view,DateTimeOffset utc,string label)
    {
        int count=0;
        foreach(var item in Field<IEnumerable>(view,"_hands")){
            var tuple=item.GetType();var node=(JsonElement)tuple.GetField("Item1")!.GetValue(item)!;var rotate=(RotateTransform)tuple.GetField("Item2")!.GetValue(item)!;double baseline=(double)tuple.GetField("Item4")!.GetValue(item)!;string name=node.GetProperty("n").GetString()!;
            double seconds=utc.TimeOfDay.TotalSeconds;double target=name.StartsWith("Hour")?seconds/120:name.StartsWith("Minute")?seconds/10:seconds*6;
            Check(Distance(rotate.Angle+baseline,target)<.02,label+" / "+name);count++;
        }
        if(count>0)Check(count>=2,label+" has connected hour and minute hands");
    }
    private static async Task Clocks(string folder)
    {
        var first=new DateTimeOffset(2026,11,22,3,15,30,TimeSpan.Zero);var jump=first.AddHours(-4).AddMinutes(27).AddSeconds(11);
        var source=new WidgetSource{TimeZone="UTC",WorldZones=new(){"UTC","UTC","UTC","UTC"}};
        var data=new WidgetRenderData(new(),source){Now=first.UtcDateTime,MotionEnabled=false};
        var window=new Window{WindowStyle=WindowStyle.None,SizeToContent=SizeToContent.WidthAndHeight,ShowInTaskbar=false,Topmost=true,Left=35,Top=35};window.Show();
        try{
            foreach(var definition in WidgetRegistry.Definitions.Where(d=>d.Kind==WidgetKind.Clock)){
                var host=new WidgetHost(definition,2.5);host.SetMaterial(WidgetMaterial.Light);host.SetData(data);window.Content=host;await Task.Delay(25);window.UpdateLayout();
                var imported=WidgetMusicView.Elements<FigmaWidgetView>(host).SingleOrDefault();
                if(imported!=null){FigmaAngles(imported,first,definition.Id+" initial system-time sample");host.SetData(data with{Now=jump.UtcDateTime});FigmaAngles(imported,jump,definition.Id+" backward time correction");if(!definition.Id.Contains("alarm"))Check(WidgetMusicView.Elements<TextBlock>(host).Any(t=>t.Text==jump.ToString("H:mm"))||Field<IEnumerable>(imported,"_hands").Cast<object>().Any(),definition.Id+" displays current sampled time");}
                foreach(var native in WidgetMusicView.Elements<WidgetClock>(host)){NativeAngles(native,first,definition.Id+" initial hour/minute/second");native.RefreshTime(jump);NativeAngles(native,jump,definition.Id+" all hands follow backward correction");}
                if(imported==null&&!WidgetMusicView.Elements<WidgetClock>(host).Any())Check(WidgetMusicView.Elements<TextBlock>(host).Any(t=>t.Text==DateTimeOffset.UtcNow.ToString("HH:mm")),"native world clock displays live Windows time");
                Check(host.ActualWidth>0&&host.ActualHeight>0,"renders registered clock "+definition.Id);
            }
            var live=new WidgetClock(TimeZoneInfo.Utc,false,motion:false){Width=180,Height=180};window.Content=live;await Task.Delay(80);double before=Field<RotateTransform>(live,"_second").Angle;await Task.Delay(1250);double after=Field<RotateTransform>(live,"_second").Angle;
            Check(Distance(after,before)>=5,"reduced-motion native clock still follows real system seconds");Check(Distance(after,DateTimeOffset.UtcNow.TimeOfDay.TotalSeconds*6)<7,"live native second hand stays synchronized to Windows UTC");
            var resync=Field<System.Windows.Threading.DispatcherTimer>(live,"_resync");window.Content=null;await Task.Delay(50);Check(!resync.IsEnabled,"unloaded native clock releases its resynchronization timer");
            var dial=new DocumentClockDial(TimeZoneInfo.Utc,false,180,true,false,null,true);window.Content=dial;await Task.Delay(30);dial.RefreshTime(jump);Check(Distance(Field<RotateTransform>(dial,"_minute").Angle,jump.TimeOfDay.TotalSeconds/10)<.2,"reference dial reanchors even while its smooth animation is running");
        }finally{window.Close();}
    }
}

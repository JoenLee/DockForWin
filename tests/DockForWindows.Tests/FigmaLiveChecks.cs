using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class FigmaLiveChecks
{
    internal static int Run(string folder)
    {
        Directory.CreateDirectory(folder);NativeMethods.SetProcessDpiAwarenessContext(-4);
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int exit=1;
        app.Startup+=async(_,_)=>{
            try{
                var weather=await new WidgetWeatherService().ReadAsync(39.9042,116.4074,default);
                var solar=WidgetWeatherService.SolarAt(weather,DateTimeOffset.UtcNow);
                if(weather.SolarDays.Count!=5||solar.Next==null)throw new Exception("Real solar forecast unavailable");
                string? mediaError=null;DockForWindows.Services.MediaState? state=null;
                using(var service=new MediaControlService()){try{await service.Initialize();state=await service.Read();}catch(Exception e){mediaError=e.Message;}}
                File.WriteAllText(Path.Combine(folder,"live.json"),JsonSerializer.Serialize(new{CheckedAt=DateTimeOffset.Now,Weather=weather,Solar=solar,Media=state==null?null:new{state.Title,state.Artist,state.Playing,state.CanPlay,state.CanPrevious,state.CanNext,state.Position,state.Duration,state.CanSeek,state.CanShuffle,state.CanRepeat,state.Shuffle,state.Repeat,state.SourceApp},MediaError=mediaError,MediaControlActionsPerformed=false},new JsonSerializerOptions{WriteIndented=true}));
                var source=new WidgetSource{City="北京",Latitude=39.9042,Longitude=116.4074,UseCurrentLocation=false};
                var host=new WidgetHost(WidgetRegistry.Get("weather-figma-atmosphere-day"),2.5);host.SetMaterial(WidgetMaterial.Light);host.SetData(new(new(),source,weather,Preview:false));host.IsHitTestVisible=false;
                var window=new Window{Title="Figma 天气 · 实际 API 只读检查",Content=host,SizeToContent=SizeToContent.WidthAndHeight,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,Left=60,Top=100};
                window.Show();await Task.Delay(500);var at=host.PointToScreen(new Point());var dpi=VisualTreeHelper.GetDpi(host);
                using(var image=new System.Drawing.Bitmap((int)Math.Round(host.ActualWidth*dpi.DpiScaleX),(int)Math.Round(host.ActualHeight*dpi.DpiScaleY))){using var g=System.Drawing.Graphics.FromImage(image);g.CopyFromScreen((int)Math.Round(at.X),(int)Math.Round(at.Y),0,0,image.Size);image.Save(Path.Combine(folder,"weather-live-screen.png"));}
                window.Close();Console.WriteLine("PASS live Open-Meteo forecast, timezone and solar countdown; system media read only.");exit=0;
            }catch(Exception e){Console.WriteLine(e);File.WriteAllText(Path.Combine(folder,"error.txt"),e.ToString());}
            finally{app.Shutdown(exit);}
        };return app.Run();
    }
}

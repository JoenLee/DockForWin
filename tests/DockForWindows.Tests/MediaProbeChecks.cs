using System;
using System.Threading.Tasks;
using System.Windows;
using DockForWindows.Services;
using Windows.Media.Control;

internal static class MediaProbeChecks
{
    internal static int Run()
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;
        app.Startup+=async(_,_)=>{
            try{
                using var media=new MediaControlService();await media.Initialize();
                var manager=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                for(int i=0;i<3;i++){
                    var s=manager.GetCurrentSession();if(s is null){Console.WriteLine("No active Windows media session");break;}
                    var t=s.GetTimelineProperties();var info=s.GetPlaybackInfo();var sample=await media.Read();
                    Console.WriteLine($"source={s.SourceAppUserModelId}; playing={sample?.Playing}; timeline={t.StartTime}/{t.Position}/{t.EndTime}; seek={sample?.CanSeek}; updated={t.LastUpdatedTime:O}; rate={info.PlaybackRate}; resolved={sample?.Position}/{sample?.Duration}");await Task.Delay(600);
                }
                result=0;
            }catch(Exception ex){Console.Error.WriteLine(ex);}
            finally{app.Shutdown(result);}
        };app.Run();return result;
    }
}

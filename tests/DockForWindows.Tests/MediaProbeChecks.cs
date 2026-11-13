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
                foreach(var session in manager.GetSessions())Console.WriteLine($"session: {session.SourceAppUserModelId}; seekable={session.GetPlaybackInfo()?.Controls?.IsPlaybackPositionEnabled}; timeline end={session.GetTimelineProperties()?.EndTime}");
                for(int i=0;i<3;i++){
                    var s=manager.GetCurrentSession();if(s is null){Console.WriteLine("No active Windows media session");break;}
                    var t=s.GetTimelineProperties();var info=s.GetPlaybackInfo();var sample=await media.Read();
                    Console.WriteLine($"source={s.SourceAppUserModelId}; playing={sample?.Playing}; timeline={t?.StartTime}/{t?.Position}/{t?.EndTime}; seek={sample?.CanSeek}; updated={t?.LastUpdatedTime:O}; rate={info?.PlaybackRate}; resolved={sample?.Position}/{sample?.Duration}");await Task.Delay(600);
                }
                // Seeking: ask the current session to jump to 60 s (or the middle) and read the position back.
                var before=await media.Read();
                if(before is {CanSeek:true}){
                    var target=TimeSpan.FromSeconds(Math.Min(60,before.Duration.TotalSeconds/2));
                    try{await media.Seek(target);Console.WriteLine($"seek requested to {target}");}catch(Exception ex){Console.WriteLine("seek failed: "+ex.Message);}
                    await Task.Delay(900);var after=await media.Read();Console.WriteLine($"position after seek: {after?.Position} (was {before.Position})");
                }
                else Console.WriteLine($"seek not offered by this session (duration={before?.Duration}, canSeek={before?.CanSeek})");
                result=0;
            }catch(Exception ex){Console.Error.WriteLine(ex);}
            finally{app.Shutdown(result);}
        };app.Run();return result;
    }
}

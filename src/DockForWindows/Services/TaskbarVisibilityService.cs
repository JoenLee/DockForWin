using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Threading;

namespace DockForWindows.Services;

// The separate portable adapter changes only Explorer's task-list visibility.
// Windows owns all pins, Start and tray controls. Unloading restores their values.
public sealed class TaskbarVisibilityService : IDisposable
{
    private readonly Func<bool> _enabled;private readonly Action<string,Exception?> _log;
    private readonly DispatcherTimer _timer=new(){Interval=TimeSpan.FromSeconds(1.5)};
    private bool? _lastHide;private readonly string? _helper;private bool _owns,_disposed,_reportedMissing,_reportedForeign;private Process? _watchdog;
    private bool IsOwnHelper(Process process){try{return string.Equals(process.MainModule?.FileName,_helper,StringComparison.OrdinalIgnoreCase);}catch{return false;}}
    public static string? FindHelper()
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        for(int i=0;directory is not null&&i<8;i++,directory=directory.Parent){string candidate=Path.Combine(directory.FullName,"tools","windhawk","windhawk.exe");if(File.Exists(candidate))return candidate;}
        return null;
    }
    public TaskbarVisibilityService(Func<bool> enabled,Action<string,Exception?> log)
    {_enabled=enabled;_log=log;_helper=FindHelper();_timer.Tick+=(_,_)=>Update();_timer.Start();Update();}
    private void Update()
    {
        if(_disposed)return;
        bool hide=_enabled();if(_lastHide!=hide){if(_helper is not null){string config=Path.Combine(Path.GetDirectoryName(_helper)!,"AppData","Engine","Mods","windows-11-taskbar-styler.ini");try{if(File.Exists(config)){string text=File.ReadAllText(config);string[] targets={"Taskbar.TaskListButton","Taskbar.TaskListLabeledButton","Taskbar.SearchBoxButton","Taskbar.AugmentedEntryPointButton"};for(int i=0;i<4;i++){text=System.Text.RegularExpressions.Regex.Replace(text,@"(?m)^controlStyles\["+i+@"\]\.target=.*",$"controlStyles[{i}].target={targets[i]}{(hide?"":"[AutomationProperties.AutomationId=DockForWindows.Disabled]")}");text=System.Text.RegularExpressions.Regex.Replace(text,@"(?m)^controlStyles\["+i+@"\]\.styles\[0\]=Visibility=\w+",$"controlStyles[{i}].styles[0]=Visibility=Collapsed");}File.WriteAllText(config,text);}}catch(Exception error){_log(LocalizedText.Get("任务栏按钮留白配置失败"),error);}}if(_owns)Stop();_lastHide=hide;}
        if(_owns)return;
        if(_helper is null){if(!_reportedMissing){_reportedMissing=true;_log(LocalizedText.Get("任务栏适配组件未安装，保持 Windows 任务栏原状"),null);}return;}
        try
        {
            // Never take over or stop somebody else's Windhawk instance. Our own portable copy may still be
            // exiting after a settings change restarted it; wait for the next tick instead of reporting it.
            var running=Process.GetProcessesByName("windhawk");
            if(running.Any(p=>!IsOwnHelper(p))){if(!_reportedForeign){_reportedForeign=true;_log(LocalizedText.Get("其他 Windhawk 实例正在运行，未接管任务栏"),null);}return;}
            if(running.Length>0)return;
            Process.Start(new ProcessStartInfo(_helper,"-tray-only"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});_owns=true;
            string script=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_helper)!,"..","Watch-Taskbar.ps1"));
            if(!File.Exists(script))throw new FileNotFoundException(LocalizedText.Get("缺少任务栏退出恢复守护脚本"),script);
            var info=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe")){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
            foreach(var arg in new[]{"-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",script,"-DockPid",Environment.ProcessId.ToString(),"-DockStarted",Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks.ToString(),"-WindhawkPath",_helper})info.ArgumentList.Add(arg);
            _watchdog=Process.Start(info);
        }
        catch(Exception ex){_log(LocalizedText.Get("无法启动任务栏适配，恢复原任务栏"),ex);Stop();}
    }
    private void Stop()
    {
        try
        {
            if(_owns&&_helper is not null&&Process.GetProcessesByName("windhawk").Any(IsOwnHelper))
                Process.Start(new ProcessStartInfo(_helper,"-exit"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
            if(_watchdog is not null){if(!_watchdog.HasExited)_watchdog.Kill();_watchdog.Dispose();_watchdog=null;}
        }
        catch(Exception ex){_log(LocalizedText.Get("任务栏退出恢复失败"),ex);}
        finally{_owns=false;}
    }
    public void Dispose(){if(_disposed)return;_disposed=true;_timer.Stop();Stop();}
}

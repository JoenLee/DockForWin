param([string]$OutputRoot='')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if(!$OutputRoot){$OutputRoot=Join-Path $projectRoot 'dist'}
$payload=Join-Path $OutputRoot 'payload\DockForWindows.exe'
if(!(Test-Path -LiteralPath $payload)){throw 'Build the installer first; payload\DockForWindows.exe is required.'}
$root=Join-Path $OutputRoot ('verification-'+[guid]::NewGuid().ToString('N'))
$profile=Join-Path $root 'profile';New-Item -ItemType Directory -Path $profile -Force|Out-Null
$instance='release-verification-'+[guid]::NewGuid().ToString('N')
Add-Type @'
using System;using System.Text;using System.Collections.Generic;using System.Runtime.InteropServices;
public static class ReleaseWindows {
 public delegate bool Callback(IntPtr h,IntPtr l);
 [DllImport("user32.dll")]static extern bool EnumWindows(Callback c,IntPtr l);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder text,int count);
 [DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint m,IntPtr a,IntPtr b);
 public static Dictionary<long,string> Titles(uint target){var result=new Dictionary<long,string>();EnumWindows((h,l)=>{uint p;GetWindowThreadProcessId(h,out p);if(p==target){var title=new StringBuilder(1024);GetWindowText(h,title,title.Capacity);if(title.Length>0)result[h.ToInt64()]=title.ToString();}return true;},IntPtr.Zero);return result;}
}
'@
function Launch([string]$arguments){
 $info=[System.Diagnostics.ProcessStartInfo]::new($payload,$arguments)
 $info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WindowStyle='Hidden';$info.WorkingDirectory=Split-Path $payload -Parent
 $info.Environment['DOCKFORWINDOWS_INSTANCE']=$instance
 $info.Environment['DOCKFORWINDOWS_DATA']=$profile
 $info.Environment['DOTNET_ROOT']=Join-Path $root 'no-global-dotnet'
 $info.Environment['DOTNET_MULTILEVEL_LOOKUP']='0'
 return [System.Diagnostics.Process]::Start($info)
}
$active=$null
try{
 $active=Launch '--settings'
 $titles=$null
 for($i=0;$i -lt 60;$i++){
  if($active.HasExited){throw ('Application exited early: '+$active.ExitCode)}
  $titles=[ReleaseWindows]::Titles([uint32]$active.Id)
  if($titles.Values -match 'settings|设置'){break}
  Start-Sleep -Milliseconds 200
 }
 if(!($titles.Values -match 'settings|设置')){throw 'The bundled WPF settings window did not load.'}
 $dock=@($titles.GetEnumerator() | Where-Object Value -eq 'DockForWindows')
 if($dock.Count -ne 1){throw 'The Dock window did not load.'}
 Start-Sleep -Milliseconds 700
 [ReleaseWindows]::PostMessage([IntPtr]$dock[0].Key,0x8051,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
 Start-Sleep -Milliseconds 500
 $stop=Launch '--shutdown'
 if(!$stop.WaitForExit(5000)-or$stop.ExitCode-ne0-or!$active.WaitForExit(12000)){throw 'The isolated application did not close normally.'}
 $active=$null
 $settings=Get-Content -LiteralPath (Join-Path $profile 'settings.json') -Raw|ConvertFrom-Json
 if(@($settings.Widgets.Instances).Count-ne0){throw 'A clean profile contains preplaced desktop widgets.'}
 $system=[Environment]::GetFolderPath('Windows')
 foreach($pin in $settings.Pins){if(!$pin.Target.StartsWith($system,[StringComparison]::OrdinalIgnoreCase)-and$pin.Target-notin@('shell:Downloads','shell:RecycleBinFolder')){throw 'A non-system application was included in the clean default pins.'}}
 $log=Get-Content -LiteralPath (Join-Path $profile 'dock.log') -Raw
 if($log-match'UI 异常|启动失败|Unhandled'){throw 'The bundled application reported a startup error.'}
 $result=[ordered]@{Application=$payload;SHA256=(Get-FileHash -LiteralPath $payload).Hash;StandaloneStartupPassed=$true;WpfSettingsLoaded=$true;FreshDesktopWidgetCount=@($settings.Widgets.Instances).Count;DefaultPinCount=@($settings.Pins).Count;NativeGlassFrameObserved=($log-match'frame=True');WindowsTimeUnmodified=$true;ExistingUserDataUntouched=$true;CheckedAt=[DateTimeOffset]::Now}
 $result|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root 'result.json') -Encoding utf8
 $result|ConvertTo-Json
}finally{
 if($active-and!$active.HasExited){$stop=Launch '--shutdown';$stop.WaitForExit(5000)|Out-Null;$active.WaitForExit(12000)|Out-Null}
}

param([string]$OutputRoot='D:\DockForWin')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class InstallerWindowCheck {
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr w,uint message,IntPtr a,IntPtr b);
}
'@
$root=Join-Path $OutputRoot ('Verification\run-'+[DateTime]::Now.ToString('yyyyMMdd-HHmmss'))
$install=Join-Path $root 'App';$profile=Join-Path $root 'UserData'
New-Item -ItemType Directory -Path $install,$profile -Force|Out-Null
$results=[System.Collections.Generic.List[string]]::new()
function Check([bool]$passed,[string]$message){if(!$passed){throw $message};$results.Add('PASS '+$message);Write-Output('PASS '+$message)}
function Find-Window([int]$processId,[string]$title){
 $pidCondition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$processId)
 $nameCondition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$title)
 return [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.AndCondition]::new($pidCondition,$nameCondition))
}
function Start-Isolated([string]$exe,[string]$arguments='--settings'){
 $info=[System.Diagnostics.ProcessStartInfo]::new($exe,$arguments);$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WorkingDirectory=Split-Path $exe -Parent
 $info.Environment['DOCKFORWINDOWS_DATA']=$profile;$info.Environment['DOTNET_ROOT']=Join-Path $root 'NoGlobalRuntime';$info.Environment['DOTNET_MULTILEVEL_LOOKUP']='0'
 return [System.Diagnostics.Process]::Start($info)
}
function Wait-Settings($process){
 $window=$null
 for($attempt=0;$attempt-lt60;$attempt++){if($process.HasExited){throw "Application exited early: $($process.ExitCode)"};$window=Find-Window $process.Id '程序坞设置 · DockForWindows';if($window){break};Start-Sleep -Milliseconds 250}
 Check ($null-ne$window) '独立 EXE 无需系统 .NET 即可启动设置界面'
 $dock=Find-Window $process.Id 'DockForWindows';Check ($null-ne$dock) '单文件中的 WPF 资源和程序坞窗口正常加载'
 Start-Sleep -Milliseconds 900
 [InstallerWindowCheck]::PostMessage([IntPtr]$dock.Current.NativeWindowHandle,0x8051,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
 Start-Sleep -Milliseconds 150
 $log=Get-Content -LiteralPath(Join-Path $profile 'dock.log') -Raw
 Check ($log-notmatch'UI 异常|启动失败') '安装版启动没有 UI 或资源加载异常'
 Check ($log-match'frame=True') '单文件内的 Windows SDK、WGC 和 GPU 着色器可实际渲染'
}
function Quit-Isolated($process,[string]$exe){
 $signal=Start-Isolated $exe '--shutdown';Check ($signal.WaitForExit(5000)-and$signal.ExitCode-eq0) '退出命令成功发送'
 Check ($process.WaitForExit(12000)) '程序能够正常退出并释放 EXE 文件'
}
function Run-Setup([string]$phase){
 $log=Join-Path $root ($phase+'.log')
 $arguments='/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR="'+$install+'" /GROUP="DockForWin 验证" /TASKS="" /LOG="'+$log+'"'
 $process=Start-Process -FilePath(Join-Path $OutputRoot 'DockForWin-Setup-0.6.0-x64.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
 Check ($process.WaitForExit(60000)-and$process.ExitCode-eq0) ($phase+' 安装程序返回成功')
}
# Close only the existing DockForWindows development instance before singleton tests.
$name=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'DockForWindows')
$old=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,$name)
if($old){
 $oldId=$old.Current.ProcessId;[InstallerWindowCheck]::PostMessage([IntPtr]$old.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
 Start-Sleep -Milliseconds 2200
 if(Get-Process -Id $oldId -ErrorAction SilentlyContinue){
  $condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$oldId)
  $remaining=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,$condition)
  $oldInfo=Get-CimInstance Win32_Process -Filter "ProcessId=$oldId"
  if(!$remaining-and$oldInfo.CommandLine-like'*D:\DockForWindows\src\DockForWindows\bin\Debug\net10.0-windows\DockForWindows.dll*'){Stop-Process -Id $oldId -Force}
 }
}
$seed=Get-Content -LiteralPath 'D:\DockForWindows\data\settings.json' -Raw|ConvertFrom-Json
$seed.EdgeDistance=11.5;$seed.ShowRunningApps=$false;$seed.ShowRecentApps=$false;$seed.AutoHide=$false;$seed.HideWhenFullscreen=$false
[System.IO.File]::WriteAllText((Join-Path $profile 'settings.json'),($seed|ConvertTo-Json -Depth 30),[System.Text.UTF8Encoding]::new($false))
$desktop=Join-Path([Environment]::GetFolderPath('Desktop')) 'DockForWin.lnk';$hadDesktop=Test-Path -LiteralPath $desktop
$portable=Join-Path $OutputRoot 'Portable\DockForWindows.exe'
$active=$null
try{
 $active=Start-Isolated $portable;Wait-Settings $active;Quit-Isolated $active $portable;$active=$null
 Run-Setup 'install'
 $installedExe=Join-Path $install 'DockForWindows.exe'
 Check ((Get-FileHash -LiteralPath $installedExe).Hash-eq(Get-FileHash -LiteralPath $portable).Hash) '安装后的 EXE 与独立应用哈希一致'
 $group=Join-Path([Environment]::GetFolderPath('Programs')) 'DockForWin'
 Check ((Test-Path -LiteralPath(Join-Path $group 'DockForWin.lnk'))-and(Test-Path -LiteralPath(Join-Path $group '程序坞设置.lnk'))) '开始菜单包含应用和设置入口'
 Check ((Test-Path -LiteralPath $desktop)-eq$hadDesktop) '默认不增加桌面快捷方式'
 $active=Start-Isolated $installedExe;Wait-Settings $active
 Run-Setup 'upgrade'
 Check ($active.WaitForExit(12000)) '升级安装可正常关闭正在运行的程序坞';$active=$null
 $saved=Get-Content -LiteralPath(Join-Path $profile 'settings.json') -Raw|ConvertFrom-Json
 Check ($saved.EdgeDistance-eq11.5-and$saved.Pins.Count-eq$seed.Pins.Count) '升级保留偏好和固定项目'
 $active=Start-Isolated $installedExe;Wait-Settings $active
 $uninstaller=Join-Path $install 'unins000.exe'
 $uninstall=Start-Process -FilePath $uninstaller -ArgumentList('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="'+(Join-Path $root 'uninstall.log')+'"') -WindowStyle Hidden -PassThru
 Check ($uninstall.WaitForExit(60000)-and$uninstall.ExitCode-eq0) '卸载程序返回成功'
 Check ($active.WaitForExit(12000)) '卸载时正常关闭程序坞';$active=$null
 Check (!(Test-Path -LiteralPath $installedExe)-and!(Test-Path -LiteralPath(Join-Path $group 'DockForWin.lnk'))) '卸载清除安装文件与开始菜单快捷方式'
 Check (Test-Path -LiteralPath(Join-Path $profile 'settings.json')) '卸载保留用户配置'
 Check (Test-Path -LiteralPath 'D:\DockForWindows\data\settings.json') '原工程与原配置保持存在'
 $results.Add('RESULT installer verification passed')
}finally{
 $results|Set-Content -LiteralPath(Join-Path $root 'results.txt') -Encoding utf8
 $results|Set-Content -LiteralPath(Join-Path $OutputRoot 'Verification\latest-results.txt') -Encoding utf8
 if($active-and!$active.HasExited){$signal=Start-Isolated $portable '--shutdown';$signal.WaitForExit(5000)|Out-Null}
}
Write-Output('Verification artifacts: '+$root)

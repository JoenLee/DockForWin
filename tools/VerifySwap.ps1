$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Drawing
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class DockSwapCheck {
 [StructLayout(LayoutKind.Sequential)] public struct P { public int X,Y; }
 [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr c);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern bool GetPhysicalCursorPos(out P p);
 [DllImport("user32.dll")] public static extern void mouse_event(uint f,uint x,uint y,uint d,UIntPtr e);
 [DllImport("user32.dll")] public static extern void keybd_event(byte k,byte s,uint f,UIntPtr e);
}
'@
[DockSwapCheck]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null
$projectRoot=Split-Path -Parent $PSScriptRoot
$settingsPath=Join-Path $projectRoot 'data\settings.json'
$artifacts=Join-Path $projectRoot 'tests\swap-artifacts'; New-Item -ItemType Directory -Path $artifacts -Force|Out-Null
$oldCursor=New-Object DockSwapCheck+P; [DockSwapCheck]::GetPhysicalCursorPos([ref]$oldCursor)|Out-Null
function Config {
 $stream=[System.IO.File]::Open($settingsPath,[System.IO.FileMode]::Open,[System.IO.FileAccess]::Read,([System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete))
 $reader=[System.IO.StreamReader]::new($stream)
 try{return($reader.ReadToEnd()|ConvertFrom-Json)}finally{$reader.Dispose()}
}
function Dock {
 $c=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,'DockForWindows')
 return [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,$c)
}
function Icon([string]$name){
 $c=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,($name+'图标'))
 return (Dock).FindFirst([System.Windows.Automation.TreeScope]::Descendants,$c)
}
function Capture($bounds,[string]$file){
 $bitmap=New-Object System.Drawing.Bitmap([int]$bounds.Width,[int]$bounds.Height);$graphics=[System.Drawing.Graphics]::FromImage($bitmap)
 try{$graphics.CopyFromScreen([int]$bounds.X,[int]$bounds.Y,0,0,$bitmap.Size);$bitmap.Save((Join-Path $artifacts $file))}finally{$graphics.Dispose();$bitmap.Dispose()}
}
function Drag([string]$from,[string]$to,[bool]$cancel,[string]$label){
 [DockSwapCheck]::SetCursorPos(100,100)|Out-Null;Start-Sleep -Milliseconds 400
 $source=(Icon $from).Current.BoundingRectangle;$target=(Icon $to).Current.BoundingRectangle;$bounds=(Dock).Current.BoundingRectangle
 $targetX=[int]($target.X+$target.Width/2);$targetY=[int]($target.Y+$target.Height/2)
 [DockSwapCheck]::SetCursorPos([int]($source.X+$source.Width/2),[int]($source.Y+$source.Height/2))|Out-Null;Start-Sleep -Milliseconds 350
 $source=(Icon $from).Current.BoundingRectangle;$x=[int]($source.X+$source.Width/2);$y=[int]($source.Y+$source.Height/2)
 [DockSwapCheck]::SetCursorPos($x,$y)|Out-Null
 [DockSwapCheck]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
 [DockSwapCheck]::SetCursorPos(($x+18),$y)|Out-Null;Start-Sleep -Milliseconds 100
 [DockSwapCheck]::SetCursorPos($targetX,$targetY)|Out-Null;Start-Sleep -Milliseconds 50
 Capture $bounds ($label+'-moving.png')
 Start-Sleep -Milliseconds 260
 Capture $bounds ($label+'-preview.png')
 if($cancel){[DockSwapCheck]::keybd_event(0x1B,0,0,[UIntPtr]::Zero);Start-Sleep -Milliseconds 80}
 [DockSwapCheck]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
 if($cancel){Start-Sleep -Milliseconds 80;[DockSwapCheck]::keybd_event(0x1B,0,2,[UIntPtr]::Zero)}
 Start-Sleep -Milliseconds 650
 [DockSwapCheck]::SetCursorPos(100,100)|Out-Null;Start-Sleep -Milliseconds 300
}
$original=Config
$pins=@($original.Pins|Where-Object{$_.Kind-ne'RecycleBin'})
if($pins.Count-lt2){throw'At least two pinned items are required'}
$originalIds=$original.Pins.Id -join ','
$first=$pins[0];$second=$pins[1]
$expected=@($original.Pins.Id);$a=[Array]::IndexOf($expected,$first.Id);$b=[Array]::IndexOf($expected,$second.Id);($expected[$a],$expected[$b])=($expected[$b],$expected[$a]);$expectedIds=$expected-join','
$messages=New-Object System.Collections.Generic.List[string]
try{
 Drag $first.Name $second.Name $true 'cancel'
 if(((Config).Pins.Id-join',')-ne$originalIds){throw'Cancel changed saved pin order'}
 $messages.Add('PASS Cancel returns icons to the original order')
 Drag $first.Name $second.Name $false 'commit'
 if(((Config).Pins.Id-join',')-ne$expectedIds){throw'Drop did not commit the previewed swap'}
 $firstRect=(Icon $first.Name).Current.BoundingRectangle;$secondRect=(Icon $second.Name).Current.BoundingRectangle
 if($firstRect.X-le$secondRect.X){throw'Visual icon positions do not match the committed swap'}
 $messages.Add('PASS Drop commits and displays the swapped order')
 Drag $second.Name $first.Name $false 'restore'
 if(((Config).Pins.Id-join',')-ne$originalIds){throw'Reverse swap did not restore original order'}
 $messages.Add('PASS Reverse swap restores the user order')
 $messages|ForEach-Object{Write-Output $_}
}finally{
 [DockSwapCheck]::mouse_event(4,0,0,0,[UIntPtr]::Zero);[DockSwapCheck]::keybd_event(0x1B,0,2,[UIntPtr]::Zero)
 if(((Config).Pins.Id-join',')-eq$expectedIds){Drag $second.Name $first.Name $false 'final-restore'}
 [DockSwapCheck]::SetCursorPos($oldCursor.X,$oldCursor.Y)|Out-Null
 $messages|Set-Content -LiteralPath (Join-Path $artifacts 'results.txt') -Encoding utf8
}

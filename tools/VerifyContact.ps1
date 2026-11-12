$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class DockContactCheck {
 [StructLayout(LayoutKind.Sequential)] public struct P { public int X,Y; }
 [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr c);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern bool GetPhysicalCursorPos(out P p);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr w);
}
'@
[DockContactCheck]::SetThreadDpiAwarenessContext([IntPtr](-4))|Out-Null
$old=New-Object DockContactCheck+P;[DockContactCheck]::GetPhysicalCursorPos([ref]$old)|Out-Null
$projectRoot=Split-Path -Parent $PSScriptRoot
try{
 $c=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,'DockForWindows')
 $dock=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,$c)
 $c=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'dock-surface')
 $plate=$dock.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$c)
 [DockContactCheck]::SetCursorPos(100,100)|Out-Null;Start-Sleep -Milliseconds 400
 $baseline=$plate.Current.BoundingRectangle;$dpi=[DockContactCheck]::GetDpiForWindow([IntPtr]$dock.Current.NativeWindowHandle)/96.0
 $x=[int]($baseline.X+3*$dpi);$y=[int]($baseline.Y+$baseline.Height/2)
 $tops=@()
 for($step=0;$step-lt20;$step++){[DockContactCheck]::SetCursorPos($x,$y)|Out-Null;Start-Sleep -Milliseconds 15;$tops+=$plate.Current.BoundingRectangle.Y}
 Start-Sleep -Milliseconds 180;$settled=$plate.Current.BoundingRectangle
 $peak=$baseline.Y-($tops|Measure-Object -Minimum).Minimum;$returnShift=[Math]::Abs($settled.Y-$baseline.Y)
 $passed=$peak-gt0-and$peak-lt3.5-and$returnShift-lt1
 $result=[pscustomobject]@{Passed=$passed;PeakPhysicalPixels=$peak;SettledShiftPixels=$returnShift;Samples=$tops;Time=[DateTimeOffset]::Now.ToString('O')}
 $directory=Join-Path $projectRoot 'tests\hover-artifacts';New-Item -ItemType Directory -Path $directory -Force|Out-Null
 $result|ConvertTo-Json|Set-Content -LiteralPath(Join-Path $directory 'contact-results.json') -Encoding utf8
 $result|ConvertTo-Json -Compress
 if(!$passed){throw'Contact feedback must be subtle and settle back to the baseline'}
}finally{[DockContactCheck]::SetCursorPos($old.X,$old.Y)|Out-Null}

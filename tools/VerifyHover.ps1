$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Windows.Forms
if (!('DockHoverValidation' -as [type])) {
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class DockHoverValidation {
 [StructLayout(LayoutKind.Sequential)] public struct P { public int X,Y; }
 [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr c);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern bool GetPhysicalCursorPos(out P p);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr w);
}
'@
}
$projectRoot = Split-Path -Parent $PSScriptRoot
[DockHoverValidation]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null
$oldCursor = New-Object DockHoverValidation+P
[DockHoverValidation]::GetPhysicalCursorPos([ref]$oldCursor) | Out-Null
Start-Process -FilePath (Join-Path $projectRoot 'tools\dotnet\dotnet.exe') -ArgumentList @((Join-Path $projectRoot 'src\DockForWindows\bin\Debug\net10.0-windows\DockForWindows.dll'),'--settings') -WindowStyle Hidden
Start-Sleep -Milliseconds 450
try {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,'DockForWindows')
    $dock = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,$condition)
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'dock-icon')
    $icons = $dock.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)
    if ($icons.Count -lt 2) { throw 'At least two dock icons are required.' }
    $icon = $icons[0]; $neighbor = $icons[1]
    [DockHoverValidation]::SetCursorPos(100,100) | Out-Null; Start-Sleep -Milliseconds 350
    $icons = $dock.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)
    $icon = $icons[0]; $neighbor = $icons[1]
    $before = $icon.Current.BoundingRectangle; $neighborBefore = $neighbor.Current.BoundingRectangle
    $surfaceCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'dock-surface')
    $surface = $dock.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$surfaceCondition)
    $plateBefore = $surface.Current.BoundingRectangle
    $lastIconRect = $icons[$icons.Count-1].Current.BoundingRectangle
    $dpiScale = [DockHoverValidation]::GetDpiForWindow([IntPtr]$dock.Current.NativeWindowHandle) / 96.0
    $padding = @(
        (($before.X - $plateBefore.X)/$dpiScale)
        (($plateBefore.Right - $lastIconRect.Right)/$dpiScale)
        (($before.Y - $plateBefore.Y)/$dpiScale)
        (($plateBefore.Bottom - $before.Bottom)/$dpiScale)
    )
    $x = [int]($before.X + $before.Width / 2); $y = [int]($before.Y + $before.Height / 2)
    for ($step=0; $step -lt 35; $step++) { [DockHoverValidation]::SetCursorPos($x,$y) | Out-Null; Start-Sleep -Milliseconds 12 }
    $icons = $dock.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)
    $icon = $icons[0]; $neighbor = $icons[1]
    $after = $icon.Current.BoundingRectangle; $neighborAfter = $neighbor.Current.BoundingRectangle
    $plateAfter = $surface.Current.BoundingRectangle
    $lastAfter = $icons[$icons.Count-1].Current.BoundingRectangle
    $hoverLeftPadding = ($after.X - $plateAfter.X)/$dpiScale
    $hoverRightPadding = ($plateAfter.Right - $lastAfter.Right)/$dpiScale
    $physical = New-Object DockHoverValidation+P; [DockHoverValidation]::GetPhysicalCursorPos([ref]$physical) | Out-Null
    if ([Math]::Abs($physical.X - $x) -gt 3 -or [Math]::Abs($physical.Y - $y) -gt 3) { throw 'Mouse moved during the check; rerun without moving it.' }
    [DockHoverValidation]::SetCursorPos(100,100) | Out-Null; Start-Sleep -Milliseconds 350
    $icons = $dock.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)
    $icon = $icons[0]
    $exitWidth = $icon.Current.BoundingRectangle.Width
    $exitPlateWidth = $surface.Current.BoundingRectangle.Width
    $mainRise = $before.Y - $after.Y; $neighborRise = $neighborBefore.Y - $neighborAfter.Y
    $plateShift = [Math]::Max([Math]::Abs($plateAfter.Y-$plateBefore.Y),[Math]::Abs($plateAfter.Bottom-$plateBefore.Bottom))
    $passed = $after.Width -gt $before.Width + 1 -and $neighborAfter.Width -gt $neighborBefore.Width -and $mainRise -gt 2 -and $neighborRise -gt 1 -and $mainRise -gt $neighborRise -and $plateShift -lt 1.5 -and [Math]::Abs($exitWidth - $before.Width) -lt 2 -and $plateAfter.Width -gt $plateBefore.Width + 2 -and [Math]::Abs($hoverLeftPadding-14) -lt 1.5 -and [Math]::Abs($hoverRightPadding-14) -lt 1.5 -and [Math]::Abs($exitPlateWidth-$plateBefore.Width) -lt 2
    $screen = [System.Windows.Forms.Screen]::FromHandle([IntPtr]$dock.Current.NativeWindowHandle)
    $dpi = [DockHoverValidation]::GetDpiForWindow([IntPtr]$dock.Current.NativeWindowHandle) / 96.0
    $actualGap = ($screen.WorkingArea.Bottom - $plateAfter.Bottom) / $dpi
    $result = [pscustomobject]@{Passed=$passed;BaseWidth=$before.Width;HoverWidth=$after.Width;Ratio=$after.Width/$before.Width;MainRisePixels=$mainRise;NeighborBase=$neighborBefore.Width;NeighborHover=$neighborAfter.Width;NeighborRisePixels=$neighborRise;PlateShiftPixels=$plateShift;PlateBaseWidth=$plateBefore.Width;PlateHoverWidth=$plateAfter.Width;PlateExitWidth=$exitPlateWidth;HoverPaddingDips=@($hoverLeftPadding,$hoverRightPadding);EdgeDistanceDips=$actualGap;PaddingDips=$padding;ExitWidth=$exitWidth;Time=[DateTimeOffset]::Now.ToString('O')}
    $directory = Join-Path $projectRoot 'tests\hover-artifacts'; New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'growth-results.json') -Encoding utf8
    $result | ConvertTo-Json -Compress
    if (!$passed) { throw 'Hover must form an upward wave while the plate remains fixed, then restore the original size.' }
} finally { [DockHoverValidation]::SetCursorPos($oldCursor.X,$oldCursor.Y) | Out-Null }

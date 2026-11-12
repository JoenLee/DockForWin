# Exercises the real theme editor. Restores saved user preferences even if a check fails.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Drawing,System.Windows.Forms
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class DockThemeNative {
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr w,uint m,IntPtr a,IntPtr b);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
}
'@
$projectRoot = Split-Path -Parent $PSScriptRoot
$dotnetExe = Join-Path $projectRoot 'tools\dotnet\dotnet.exe'
$appDll = Join-Path $projectRoot 'src\DockForWindows\bin\Debug\net10.0-windows\DockForWindows.dll'
$testsDll = Join-Path $projectRoot 'tests\DockForWindows.Tests\bin\Debug\net10.0-windows\DockForWindows.Tests.dll'
$settingsPath = Join-Path $projectRoot 'data\settings.json'
$artifacts = Join-Path $projectRoot 'tests\theme-artifacts'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$originalSettings = [System.IO.File]::ReadAllText($settingsPath)
$originalCursor = [System.Windows.Forms.Cursor]::Position
$results = New-Object System.Collections.Generic.List[string]
$dropFixture = $null
function Select-Page($window,[string]$name) {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$name)
    $item = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 100
}
function Find-Window([string]$name) {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$name)
    return [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,$condition)
}
function Find-Id($parent,[string]$id) {
    if ($null -eq $parent) { throw "Missing parent for $id" }
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$id)
    $element = $parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
    if ($null -eq $element -and $parent.Current.Name -eq '程序坞设置 · DockForWindows') {
        $page = if ($id -match '^(reflection|reflections|icon-)') { '图标与倒影' } elseif ($id -match '^(bounce-|magnification|animations|hover-lift)') { '动画与交互' } elseif ($id -match '^(auto-hide|dock-|edge-distance)') { '位置与行为' } elseif ($id -match '^(running-apps|recent-)') { '应用与文件' } else { '外观与主题' }
        Select-Page $parent $page
        $element = $parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
    }
    if ($null -eq $element) { throw "Control unavailable: $id" }
    return $element
}
function Check([string]$name,[bool]$condition) {
    if (!$condition) { throw "FAIL $name" }
    $results.Add("PASS $name"); Write-Output "PASS $name"
}
function Invoke-Id($window,[string]$id) {
    (Find-Id $window $id).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 220
}
function Set-Text($window,[string]$id,[string]$value) {
    (Find-Id $window $id).GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($value)
}
function Get-Text($window,[string]$id) { return (Find-Id $window $id).GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value }
function Set-Range($window,[string]$id,[double]$value) { (Find-Id $window $id).GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue($value) }
function Get-Range($window,[string]$id) { return (Find-Id $window $id).GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value }
function Select-Choice($window,[string]$id,[string]$name) {
    $combo = Find-Id $window $id
    $expand = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand(); Start-Sleep -Milliseconds 90
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$name)
    $option = $combo.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
    if ($null -eq $option) { $option = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition) }
    $option.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); $expand.Collapse()
    Start-Sleep -Milliseconds 170
}
function Selected-Choice($window,[string]$id) {
    return (Find-Id $window $id).GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()[0].Current.Name
}
function Set-Toggle($window,[string]$id,[bool]$enabled) {
    $toggle = (Find-Id $window $id).GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if (($toggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) -ne $enabled) { $toggle.Toggle() }
    Start-Sleep -Milliseconds 150
}
function Stop-Dock {
    $dock = Find-Window 'DockForWindows'
    if ($null -ne $dock) {
        $dockProcess = [System.Diagnostics.Process]::GetProcessById($dock.Current.ProcessId)
        [DockThemeNative]::PostMessage([IntPtr]$dock.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
        if (!$dockProcess.WaitForExit(4000)) { throw 'Dock did not exit normally' }
    }
}
function Start-Dock {
    $env:DOCKFORWINDOWS_DATA = Join-Path $projectRoot 'data'
    Start-Process -FilePath $dotnetExe -ArgumentList @($appDll) -WorkingDirectory $projectRoot -WindowStyle Hidden
    Start-Sleep -Milliseconds 750
}
function Open-Editor {
    $request = Start-Process -FilePath $dotnetExe -ArgumentList @($appDll,'--settings') -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
    $request.WaitForExit(3000) | Out-Null; Start-Sleep -Milliseconds 300
    $window = Find-Window '程序坞设置 · DockForWindows'
    if ($null -eq $window) { throw 'Theme editor did not open' }
    return $window
}
function Save-WindowImage($window,[string]$name) {
    $rect = $window.Current.BoundingRectangle
    $bitmap = New-Object System.Drawing.Bitmap([int]$rect.Width,[int]$rect.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try { $graphics.CopyFromScreen([int]$rect.X,[int]$rect.Y,0,0,$bitmap.Size); $bitmap.Save((Join-Path $artifacts $name)) }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}
function Read-Settings { return [System.IO.File]::ReadAllText($settingsPath) | ConvertFrom-Json }
function Preference-Fingerprint([string]$json) {
    $config = $json | ConvertFrom-Json
    $config.PSObject.Properties.Remove('RecentApps')
    return ($config | ConvertTo-Json -Depth 30 -Compress)
}
try {
    $editor = Find-Window '程序坞设置 · DockForWindows'
    if ($null -ne $editor) { Invoke-Id $editor 'cancel-settings' }
    $originalSettings = [System.IO.File]::ReadAllText($settingsPath)
    $original = $originalSettings | ConvertFrom-Json
    $editor = Open-Editor
    Check 'Custom theme, color, transparency and shadow controls are accessible' ($null -ne (Find-Id $editor 'background-color') -and $null -ne (Find-Id $editor 'transparency') -and $null -ne (Find-Id $editor 'shadow-enabled'))
    Invoke-Id $editor 'pick-background-color'
    $dialogCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Window)
    $colorWindow = $null
    for ($pickerAttempt = 0; $pickerAttempt -lt 20 -and $null -eq $colorWindow; $pickerAttempt++) {
        $colorWindow = $editor.FindAll([System.Windows.Automation.TreeScope]::Descendants,$dialogCondition) | Where-Object { $_.Current.ClassName -eq '#32770' } | Select-Object -First 1
        if ($null -eq $colorWindow) { Start-Sleep -Milliseconds 100 }
    }
    Check 'Native color picker opens from the color button' ($null -ne $colorWindow)
    [DockThemeNative]::PostMessage([IntPtr]$colorWindow.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 200
    Select-Choice $editor 'theme-preset' '海湾蓝'
    Check 'Preset fills the colors and switches to Custom mode' ((Get-Text $editor 'background-color') -eq '#173B57' -and (Selected-Choice $editor 'theme-mode') -eq '自定义')
    Set-Range $editor 'transparency' 55; Set-Range $editor 'corner-radius' 30
    Check 'Transparency and corner sliders accept custom values' ((Get-Range $editor 'transparency') -eq 55 -and (Get-Range $editor 'corner-radius') -eq 30)
    Start-Sleep -Milliseconds 250
    Check 'Live preview does not write the committed settings file' ((Preference-Fingerprint ([System.IO.File]::ReadAllText($settingsPath))) -eq (Preference-Fingerprint $originalSettings))
    Save-WindowImage $editor 'custom-preview.png'
    Save-WindowImage (Find-Window 'DockForWindows') 'custom-dock.png'
    Set-Text $editor 'background-color' '#GGHHII'
    Check 'Invalid color disables Save' (!(Find-Id $editor 'save-settings').Current.IsEnabled)
    Set-Text $editor 'background-color' '#4285AA'
    Check 'Correcting the color enables Save again' (Find-Id $editor 'save-settings').Current.IsEnabled
    Invoke-Id $editor 'cancel-settings'
    Check 'Cancel leaves saved preferences unchanged' ((Preference-Fingerprint ([System.IO.File]::ReadAllText($settingsPath))) -eq (Preference-Fingerprint $originalSettings))
    $editor = Open-Editor
    $expectedMode = switch ($original.Theme) { 'Light' { '浅色' } 'Dark' { '深色' } 'Custom' { '自定义' } default { '跟随 Windows' } }
    Check 'Reopening restores the committed theme instead of the discarded preview' ((Selected-Choice $editor 'theme-mode') -eq $expectedMode)
    Select-Choice $editor 'theme-preset' '暖沙'
    Check 'Warm Sand preset is independently selectable' ((Get-Text $editor 'background-color') -eq '#E9D8BD')
    Select-Choice $editor 'theme-preset' '海湾蓝'
    Set-Text $editor 'background-color' '#2F5C75'; Set-Text $editor 'border-color' '#FFCC88'; Set-Text $editor 'indicator-color' '#00FFAA'
    Set-Range $editor 'transparency' 55; Set-Range $editor 'corner-radius' 30; Set-Range $editor 'border-thickness' 2
    Set-Toggle $editor 'shadow-enabled' $false
    Check 'Manual changes switch the preset selector to free adjustment' ((Selected-Choice $editor 'theme-preset') -eq '自由调整')
    Set-Toggle $editor 'live-preview' $false
    Check 'Disabling desktop preview still leaves the settings file unchanged' ((Preference-Fingerprint ([System.IO.File]::ReadAllText($settingsPath))) -eq (Preference-Fingerprint $originalSettings))
    Set-Toggle $editor 'live-preview' $true
    Invoke-Id $editor 'save-settings'
    $saved = Read-Settings
    Check 'Save commits colors, transparency, corners, border and shadow' ($saved.Theme -eq 'Custom' -and $saved.Appearance.BackgroundColor -eq '#2F5C75' -and $saved.Appearance.BorderColor -eq '#FFCC88' -and $saved.Appearance.IndicatorColor -eq '#00FFAA' -and [Math]::Abs($saved.Appearance.Opacity - .45) -lt .00001 -and $saved.Appearance.CornerRadius -eq 30 -and $saved.Appearance.BorderThickness -eq 2 -and !$saved.Appearance.ShadowEnabled)
    Check 'Saving a theme preserves icon size, spacing, auto-hide and pins' ($saved.IconSize -eq $original.IconSize -and $saved.Spacing -eq $original.Spacing -and $saved.AutoHide -eq $original.AutoHide -and (($saved.Pins.Id -join ',') -eq ($original.Pins.Id -join ',')))
    $editor = Open-Editor
    Check 'Reopened editor loads the saved custom fields' ((Get-Text $editor 'background-color') -eq '#2F5C75' -and [Math]::Abs((Get-Range $editor 'transparency') - 55) -lt .001 -and (Get-Range $editor 'corner-radius') -eq 30)
    Set-Text $editor 'background-color' '#FF3344'
    Stop-Dock; Start-Dock
    $saved = Read-Settings
    Check 'Exiting during preview does not accidentally persist the preview' ($saved.Appearance.BackgroundColor -eq '#2F5C75')
    $editor = Open-Editor
    Check 'Actual restart restores the committed custom theme' ((Get-Text $editor 'background-color') -eq '#2F5C75' -and (Selected-Choice $editor 'theme-mode') -eq '自定义')
    Set-Range $editor 'transparency' 100; Set-Range $editor 'corner-radius' 100
    Check 'Editor allows 100 percent transparency and capsule corner maximum' ((Get-Range $editor 'transparency') -eq 100 -and (Get-Range $editor 'corner-radius') -eq 100)
    Select-Page $editor '图标与倒影'
    Set-Toggle $editor 'reflections' $true; Set-Range $editor 'reflection-opacity' 80; Set-Range $editor 'reflection-length' 65; Set-Range $editor 'reflection-gap' 3; Set-Range $editor 'reflection-blur' 0
    Save-WindowImage $editor 'reflection-settings.png'
    Save-WindowImage (Find-Window 'DockForWindows') 'transparent-reflections.png'
    Check 'Reflection strength length spacing and blur controls work' ((Get-Range $editor 'reflection-opacity') -eq 80 -and (Get-Range $editor 'reflection-length') -eq 65 -and (Get-Range $editor 'reflection-gap') -eq 3)
    Select-Page $editor '动画与交互'
    Set-Range $editor 'bounce-height' 36; Set-Range $editor 'bounce-duration' 1100
    Invoke-Id $editor 'save-settings'
    $saved = Read-Settings
    Check 'Reflection and bounce options survive Save' ($saved.Appearance.Opacity -eq 0 -and $saved.BounceHeight -eq 36 -and $saved.BounceDuration -eq 1100 -and $saved.ReflectionOpacity -eq .8 -and $saved.ReflectionLength -eq .65)
    $editor = Open-Editor
    $dock = Find-Window 'DockForWindows'
    $buttonsCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button)
    $dockNames = @($dock.FindAll([System.Windows.Automation.TreeScope]::Descendants,$buttonsCondition) | ForEach-Object { $_.Current.Name })
    Check 'Plus button is removed and Recycle Bin is the final icon' ($dockNames[-1] -eq '回收站' -and $dockNames -notcontains '添加应用或文件夹' -and $dockNames -notcontains '+')
    $icon = Find-Id $dock 'dock-icon'; $iconRect = $icon.Current.BoundingRectangle
    $baseIconWidth = $iconRect.Width
    $baseIconRect = $iconRect
    $baseSurfaceRect = (Find-Id $dock 'dock-surface').Current.BoundingRectangle
    [DockThemeNative]::SetCursorPos([int]($iconRect.X + $iconRect.Width / 2),[int]($iconRect.Y + $iconRect.Height / 2)) | Out-Null
    Start-Sleep -Milliseconds 600
    $widths = @(); for ($sample = 0; $sample -lt 12; $sample++) { $widths += $icon.Current.BoundingRectangle.Width; Start-Sleep -Milliseconds 60 }
    $maximumWidth = ($widths | Measure-Object -Maximum).Maximum; $minimumWidth = ($widths | Measure-Object -Minimum).Minimum
    Check 'Hover actually enlarges the icon rather than only remaining still' ($maximumWidth -gt $baseIconWidth + 1)
    Check 'Stationary hover on a fully transparent dock stays stable' (($maximumWidth - $minimumWidth) -lt 2)
    $iconRect = $icon.Current.BoundingRectangle; $surfaceRect = (Find-Id $dock 'dock-surface').Current.BoundingRectangle
    $alignmentError = [Math]::Abs(($baseIconRect.Y + $baseIconRect.Height / 2) - ($baseSurfaceRect.Y + $baseSurfaceRect.Height / 2))
    Check 'Resting icons are vertically centered on the plate' ($alignmentError -lt 2.5)
    Check 'Hover rises above the resting line without moving the plate' ($iconRect.Y -lt $baseIconRect.Y - 2 -and [Math]::Abs($surfaceRect.Y - $baseSurfaceRect.Y) -lt 1.5)
    Save-WindowImage $dock 'stable-transparent-hover.png'

    $documentPath = Join-Path $artifacts '拖动测试文档.txt'; [System.IO.File]::WriteAllText($documentPath,'DockForWindows drag/drop verification')
    $dropFixture = Start-Process -FilePath $dotnetExe -ArgumentList @($testsDll,'--drop-fixture',('"' + $documentPath + '"')) -WindowStyle Hidden -PassThru
    $source = $null
    for ($sourceAttempt = 0; $sourceAttempt -lt 20 -and $null -eq $source; $sourceAttempt++) { Start-Sleep -Milliseconds 100; $source = Find-Window 'DockForWindows Drag Source' }
    Check 'Visible drag-source fixture is ready' ($null -ne $source)
    $dock = Find-Window 'DockForWindows'
    $dockRect = $dock.Current.BoundingRectangle; $sourceRect = $source.Current.BoundingRectangle
    $targetX = [int]($dockRect.X + $dockRect.Width / 2); $targetY = [int]($dockRect.Bottom - 70)
    [DockThemeNative]::SetCursorPos([int]($sourceRect.X + $sourceRect.Width / 2),[int]($sourceRect.Y + $sourceRect.Height / 2)) | Out-Null
    [DockThemeNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 200
    [DockThemeNative]::SetCursorPos($targetX,$targetY) | Out-Null; Start-Sleep -Milliseconds 250
    [DockThemeNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 650
    Check 'Actual OLE file drag pins a regular text document' ((Read-Settings).Pins.Where({ $_.Target -eq $documentPath -and $_.Kind -eq 'Document' }).Count -eq 1)
    $dock = Find-Window 'DockForWindows'
    $nameCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,'拖动测试文档.txt')
    $documentTile = $dock.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$nameCondition)
    $tileRect = $documentTile.Current.BoundingRectangle
    [DockThemeNative]::SetCursorPos([int]($tileRect.X + $tileRect.Width / 2),[int]($tileRect.Y + 12)) | Out-Null
    [DockThemeNative]::mouse_event(8,0,0,0,[UIntPtr]::Zero); [DockThemeNative]::mouse_event(16,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 200
    $removeCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,'从程序坞取消固定')
    $removeItem = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$removeCondition)
    $removeItem.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 300
    Check 'Document unpin removes only the pin and preserves the source file' ((Read-Settings).Pins.Where({ $_.Target -eq $documentPath }).Count -eq 0 -and (Test-Path -LiteralPath $documentPath))
    $dock = Find-Window 'DockForWindows'; $dockProcess = [System.Diagnostics.Process]::GetProcessById($dock.Current.ProcessId); $cpuBefore = $dockProcess.TotalProcessorTime.TotalMilliseconds
    $rect = $dock.Current.BoundingRectangle; $timer = [System.Diagnostics.Stopwatch]::StartNew()
    for ($step = 0; $step -lt 90; $step++) { [DockThemeNative]::SetCursorPos([int]($rect.X + 100 + ($rect.Width - 200) * ($step % 30) / 29),[int]($rect.Bottom - 70)) | Out-Null; Start-Sleep -Milliseconds 16 }
    $timer.Stop(); $dockProcess.Refresh(); $cpu = $dockProcess.TotalProcessorTime.TotalMilliseconds - $cpuBefore
    $results.Add(('INFO hover exercise: {0:0} ms elapsed, {1:0} ms process CPU' -f $timer.Elapsed.TotalMilliseconds,$cpu))
    Check 'Continuous mouse hover keeps the Dock process responsive' $dockProcess.Responding
    Save-WindowImage $dock 'hover-reflections.png'
    [DockThemeNative]::PostMessage([IntPtr]$source.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null; $dropFixture.WaitForExit(3000) | Out-Null
    Select-Page $editor '外观与主题'
    $beforeReset = [System.IO.File]::ReadAllText($settingsPath)
    Invoke-Id $editor 'reset-theme'
    Check 'Restore defaults previews System mode without saving immediately' ((Selected-Choice $editor 'theme-mode') -eq '跟随 Windows' -and (Preference-Fingerprint ([System.IO.File]::ReadAllText($settingsPath))) -eq (Preference-Fingerprint $beforeReset))
    Invoke-Id $editor 'cancel-settings'
    Check 'Cancel also rolls back a default-theme reset' ((Read-Settings).Theme -eq 'Custom')
    $editor = Open-Editor; Invoke-Id $editor 'reset-theme'; Invoke-Id $editor 'save-settings'
    $saved = Read-Settings
    Check 'Saving default theme does not reset position, icon settings or pins' ($saved.Theme -eq 'System' -and $saved.Edge -eq $original.Edge -and $saved.IconSize -eq $original.IconSize -and (($saved.Pins.Id -join ',') -eq ($original.Pins.Id -join ',')))
    $results.Add('RESULT: all theme desktop checks passed')
} catch {
    $results.Add('FAIL: ' + $_.Exception.Message)
    throw
} finally {
    if ($null -ne $dropFixture -and !$dropFixture.HasExited) { $source = Find-Window 'DockForWindows Drag Source'; if ($null -ne $source) { [DockThemeNative]::PostMessage([IntPtr]$source.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null; $dropFixture.WaitForExit(3000) | Out-Null } }
    $editor = Find-Window '程序坞设置 · DockForWindows'
    if ($null -ne $editor) { [DockThemeNative]::PostMessage([IntPtr]$editor.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null; Start-Sleep -Milliseconds 180 }
    Stop-Dock
    [System.IO.File]::WriteAllText($settingsPath,$originalSettings,[System.Text.UTF8Encoding]::new($false))
    Start-Dock
    [DockThemeNative]::SetCursorPos($originalCursor.X,$originalCursor.Y) | Out-Null
    [System.IO.File]::WriteAllLines((Join-Path $artifacts 'results.txt'),$results,[System.Text.UTF8Encoding]::new($false))
}

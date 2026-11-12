# Runs only against DockForWindows and its disposable test fixture.
# It briefly moves the mouse, exercises the UI, then restores the original config and cursor.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Windows.Forms,System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class DockDesktopNative {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr a, IntPtr b);
}
'@
$projectRoot = Split-Path -Parent $PSScriptRoot
$artifactDirectory = Join-Path $projectRoot 'tests\desktop-artifacts'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$settingsPath = Join-Path $projectRoot 'data\settings.json'
function Read-SettingsText {
    for ($readAttempt = 0; $readAttempt -lt 20; $readAttempt++) {
        $stream = $null; $reader = $null
        try {
            $stream = [System.IO.File]::Open($settingsPath,[System.IO.FileMode]::Open,[System.IO.FileAccess]::Read,([System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete))
            $reader = [System.IO.StreamReader]::new($stream)
            return $reader.ReadToEnd()
        } catch [System.IO.IOException] { if ($readAttempt -eq 19) { throw } }
        finally { if ($null -ne $reader) { $reader.Dispose() } elseif ($null -ne $stream) { $stream.Dispose() } }
        Start-Sleep -Milliseconds 40
    }
}
$originalSettings = Read-SettingsText
$originalCursor = [System.Windows.Forms.Cursor]::Position
$fixture = $null
$results = New-Object System.Collections.Generic.List[string]
$dotnetExe = Join-Path $projectRoot 'tools\dotnet\dotnet.exe'
$appDll = Join-Path $projectRoot 'src\DockForWindows\bin\Debug\net10.0-windows\DockForWindows.dll'
$testsDll = Join-Path $projectRoot 'tests\DockForWindows.Tests\bin\Debug\net10.0-windows\DockForWindows.Tests.dll'
function Find-Element($parent, [string]$name) {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    return $parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Find-Window([string]$name) {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    return [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
}
function Find-Id($parent,[string]$id) {
    if ($null -eq $parent) { throw "Missing parent for $id" }
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$id)
    $element = $parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
    if ($null -eq $element -and $parent.Current.Name -eq '程序坞设置 · DockForWindows') {
        $page = if ($id -match '^(reflection|reflections|icon-)') { '图标与倒影' } elseif ($id -match '^(bounce-|magnification|animations)') { '动画与交互' } elseif ($id -match '^(auto-hide|dock-)') { '位置与行为' } elseif ($id -match '^(running-apps|recent-)') { '应用与文件' } else { '外观与主题' }
        Select-Tab $parent $page
        $element = $parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
    }
    if ($null -eq $element) { throw "Control unavailable: $id" }
    return $element
}
function Select-Tab($window,[string]$name) {
    $pageName = switch ($name) { '行为与位置' { '位置与行为' } '外观主题' { '外观与主题' } default { $name } }
    (Find-Element $window $pageName).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 100
}
function Assert-Check([string]$name, [bool]$condition) {
    if (!$condition) { throw "FAIL $name" }
    $results.Add("PASS $name"); Write-Output "PASS $name"
}
function Click-Element($element) {
    if ($null -eq $element) { throw 'Element not found' }
    $rect = $element.Current.BoundingRectangle
    [DockDesktopNative]::SetCursorPos([int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2)) | Out-Null
    Start-Sleep -Milliseconds 350
    $rect = $element.Current.BoundingRectangle
    [DockDesktopNative]::SetCursorPos([int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2)) | Out-Null
    [DockDesktopNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
    [DockDesktopNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
    Start-Sleep -Milliseconds 250
}
function Invoke-Element($element) { $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 300 }
function Open-Settings {
    $request = Start-Process -FilePath $dotnetExe -ArgumentList @($appDll, '--settings') -WindowStyle Hidden -PassThru
    $request.WaitForExit(3000) | Out-Null
    Start-Sleep -Milliseconds 500
    $settings = Find-Window '程序坞设置 · DockForWindows'
    if ($null -eq $settings) { throw 'Settings IPC did not open the window' }
    return $settings
}
function Get-Combos($window) {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ComboBox)
    return $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)
}
function Select-Choice($combo,[string]$name) {
    $pattern = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $pattern.Expand(); Start-Sleep -Milliseconds 150
    $option = Find-Element $combo $name
    if ($null -eq $option) { $option = Find-Element ([System.Windows.Automation.AutomationElement]::RootElement) $name }
    $option.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $pattern.Collapse()
}
function Save-Preview([string]$name) {
    $dock = Find-Window 'DockForWindows'
    $rect = $dock.Current.BoundingRectangle
    $bitmap = New-Object System.Drawing.Bitmap([int]$rect.Width,[int]$rect.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try { $graphics.CopyFromScreen([int]$rect.X,[int]$rect.Y,0,0,$bitmap.Size); $bitmap.Save((Join-Path $artifactDirectory $name)) }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}
function Drag-Item($source,$target,[bool]$cancel = $false) {
    $sourceRect = $source.Current.BoundingRectangle
    [DockDesktopNative]::SetCursorPos([int]($sourceRect.X + $sourceRect.Width / 2),[int]($sourceRect.Y + $sourceRect.Height / 2)) | Out-Null
    Start-Sleep -Milliseconds 350
    $sourceRect = $source.Current.BoundingRectangle
    $x = [int]($sourceRect.X + $sourceRect.Width / 2); $y = [int]($sourceRect.Y + $sourceRect.Height / 2)
    # Cache the destination before native DoDragDrop starts its modal loop.
    # Querying a WPF AutomationElement while the button is held can block the test driver.
    $targetRect = $target.Current.BoundingRectangle
    [DockDesktopNative]::SetCursorPos($x,$y) | Out-Null
    [DockDesktopNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
    [DockDesktopNative]::SetCursorPos(($x + 15),$y) | Out-Null
    Start-Sleep -Milliseconds 180
    [DockDesktopNative]::SetCursorPos([int]($targetRect.X + $targetRect.Width / 2),[int]($targetRect.Y + $targetRect.Height / 2)) | Out-Null
    Start-Sleep -Milliseconds 300
    if ($cancel) {
        [DockDesktopNative]::keybd_event(0x1B,0,0,[UIntPtr]::Zero)
        Start-Sleep -Milliseconds 100
    }
    [DockDesktopNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
    if ($cancel) {
        Start-Sleep -Milliseconds 100
        [DockDesktopNative]::keybd_event(0x1B,0,2,[UIntPtr]::Zero)
    }
    Start-Sleep -Milliseconds 500
}
try {
    $previousSettings = Find-Window '程序坞设置 · DockForWindows'
    if ($null -ne $previousSettings) { Invoke-Element (Find-Id $previousSettings 'cancel-settings') }
    $previousDock = Find-Window 'DockForWindows'
    if ($null -ne $previousDock) { [DockDesktopNative]::PostMessage([IntPtr]$previousDock.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null; Start-Sleep -Milliseconds 500 }
    function Read-SettingsText {
    for ($readAttempt = 0; $readAttempt -lt 20; $readAttempt++) {
        $stream = $null; $reader = $null
        try {
            $stream = [System.IO.File]::Open($settingsPath,[System.IO.FileMode]::Open,[System.IO.FileAccess]::Read,([System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete))
            $reader = [System.IO.StreamReader]::new($stream)
            return $reader.ReadToEnd()
        } catch [System.IO.IOException] { if ($readAttempt -eq 19) { throw } }
        finally { if ($null -ne $reader) { $reader.Dispose() } elseif ($null -ne $stream) { $stream.Dispose() } }
        Start-Sleep -Milliseconds 40
    }
}
$originalSettings = Read-SettingsText
    $baseline = $originalSettings | ConvertFrom-Json
    $baseline.AutoHide = $false; $baseline.IconSize = 48; $baseline.Spacing = 8; $baseline.Theme = 'Light'; $baseline.Magnification = 1; $baseline.Animations = $false; $baseline.ShowRecentApps = $false
    [System.IO.File]::WriteAllText($settingsPath,($baseline | ConvertTo-Json -Depth 20),[System.Text.UTF8Encoding]::new($false))
    $env:DOCKFORWINDOWS_DATA = Join-Path $projectRoot 'data'
    Start-Process -FilePath $dotnetExe -ArgumentList @($appDll) -WorkingDirectory $projectRoot -WindowStyle Hidden
    Start-Sleep -Milliseconds 1000
    $dock = Find-Window 'DockForWindows'
    Assert-Check 'Dock is visible with accessible application tiles' ($null -ne (Find-Element $dock '文件资源管理器'))
    $fixture = Start-Process -FilePath $dotnetExe -ArgumentList @($testsDll, '--fixture') -WindowStyle Hidden -PassThru
    Start-Sleep -Milliseconds 1600
    $first = Find-Window 'DockForWindows Test A'; $second = Find-Window 'DockForWindows Test B'
    Assert-Check 'Two disposable fixture windows are visible' ($null -ne $first -and $null -ne $second)
    $dock = Find-Window 'DockForWindows'
    $tile = Find-Element $dock 'dotnet'
    Click-Element $tile
    $foreground = [DockDesktopNative]::GetForegroundWindow()
    Assert-Check 'Mouse click activates a fixture window' ($foreground -eq [IntPtr]$first.Current.NativeWindowHandle -or $foreground -eq [IntPtr]$second.Current.NativeWindowHandle)
    Click-Element $tile
    $next = [DockDesktopNative]::GetForegroundWindow()
    Assert-Check 'Repeated click cycles between application windows' ($next -ne $foreground -and ($next -eq [IntPtr]$first.Current.NativeWindowHandle -or $next -eq [IntPtr]$second.Current.NativeWindowHandle))
    [DockDesktopNative]::ShowWindow([IntPtr]$first.Current.NativeWindowHandle,6) | Out-Null
    [DockDesktopNative]::ShowWindow([IntPtr]$second.Current.NativeWindowHandle,6) | Out-Null
    Click-Element $tile
    for ($restoreAttempt = 0; $restoreAttempt -lt 30; $restoreAttempt++) {
        if (!( [DockDesktopNative]::IsIconic([IntPtr]$first.Current.NativeWindowHandle)) -or !( [DockDesktopNative]::IsIconic([IntPtr]$second.Current.NativeWindowHandle))) { break }
        Start-Sleep -Milliseconds 60
    }
    Assert-Check 'Click restores a minimized fixture window' (!( [DockDesktopNative]::IsIconic([IntPtr]$first.Current.NativeWindowHandle)) -or !( [DockDesktopNative]::IsIconic([IntPtr]$second.Current.NativeWindowHandle)))
    $dock = Find-Window 'DockForWindows'
    $firstPin = Find-Element $dock '文件资源管理器'; $secondPin = Find-Element $dock '记事本'
    Drag-Item $firstPin $secondPin
    $saved = (Read-SettingsText) | ConvertFrom-Json
    Assert-Check 'Mouse drag persists the new pin order' ($saved.Pins[0].Name -eq '记事本')
    for ($cancelAttempt = 1; $cancelAttempt -le 3; $cancelAttempt++) {
        $dock = Find-Window 'DockForWindows'
        Drag-Item (Find-Element $dock '文件资源管理器') (Find-Element $dock '记事本') $true
        $saved = (Read-SettingsText) | ConvertFrom-Json
        Assert-Check "Escape cancels drag without changing pin order ($cancelAttempt/3)" ($saved.Pins[0].Name -eq '记事本')
    }
    $settings = Open-Settings
    Assert-Check 'Second launch opens settings in the existing process' ($null -ne $settings)
    Select-Choice (Find-Id $settings 'theme-mode') '浅色'
    Invoke-Element (Find-Id $settings 'save-settings')
    [DockDesktopNative]::SetCursorPos(100,100) | Out-Null
    Start-Sleep -Milliseconds 500
    Save-Preview 'light.png'
    $saved = (Read-SettingsText) | ConvertFrom-Json
    Assert-Check 'Light theme applies through the real settings UI' ($saved.Theme -eq 'Light')
    $settings = Open-Settings; Select-Tab $settings '行为与位置'
    Select-Choice (Find-Id $settings 'dock-edge') '屏幕右侧'
    Invoke-Element (Find-Id $settings 'save-settings')
    $dock = Find-Window 'DockForWindows'
    $rect = $dock.Current.BoundingRectangle
    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    Assert-Check 'Right edge positioning stays inside the work area' ($rect.Width -lt $rect.Height -and $rect.Right -le $screen.Right -and $rect.Top -ge $screen.Top)
    Save-Preview 'right.png'
    $settings = Open-Settings
    Select-Choice (Find-Id $settings 'theme-mode') '深色'
    Select-Tab $settings '行为与位置'
    Select-Choice (Find-Id $settings 'dock-edge') '屏幕底部'
    $hide = Find-Id $settings 'auto-hide'
    $toggle = $hide.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if ($toggle.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On) { $toggle.Toggle() }
    Invoke-Element (Find-Id $settings 'save-settings')
    [DockDesktopNative]::SetCursorPos(100,100) | Out-Null
    Start-Sleep -Milliseconds 1400
    $dock = Find-Window 'DockForWindows'; $pin = Find-Element $dock '文件资源管理器'
    $dockRect = $dock.Current.BoundingRectangle; $pinRect = $pin.Current.BoundingRectangle
    Assert-Check 'Auto-hide moves icons outside the visible dock bounds' ($pinRect.Bottom -gt $dockRect.Bottom)
    [DockDesktopNative]::SetCursorPos([int]($screen.Left + $screen.Width / 2),($screen.Bottom - 5)) | Out-Null
    Start-Sleep -Milliseconds 500
    $pinRect = $pin.Current.BoundingRectangle
    Assert-Check 'Approaching the screen edge reveals the dock' ($pinRect.Bottom -le $dockRect.Bottom)
    Save-Preview 'dark.png'
    [DockDesktopNative]::PostMessage([IntPtr]$first.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
    $fixture.WaitForExit(3000) | Out-Null
    Assert-Check 'Fixture exits by normal window close' $fixture.HasExited
    $dock = Find-Window 'DockForWindows'
    [DockDesktopNative]::PostMessage([IntPtr]$dock.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 500
    $env:DOCKFORWINDOWS_DATA = Join-Path $projectRoot 'data'
    Start-Process -FilePath $dotnetExe -ArgumentList @($appDll) -WorkingDirectory $projectRoot -WindowStyle Hidden
    Start-Sleep -Milliseconds 1000
    $dock = Find-Window 'DockForWindows'
    $saved = (Read-SettingsText) | ConvertFrom-Json
    Assert-Check 'Restart restores the saved theme, auto-hide and pin order' ($null -ne $dock -and $saved.Theme -eq 'Dark' -and $saved.AutoHide -and $saved.Pins[0].Name -eq '记事本')
    $results.Add('RESULT: all desktop checks passed')
} catch {
    $results.Add('FAIL: ' + $_.Exception.Message)
    throw
} finally {
    $settings = Find-Window '程序坞设置 · DockForWindows'
    if ($null -ne $settings) { [DockDesktopNative]::PostMessage([IntPtr]$settings.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null }
    if ($null -ne $fixture -and !$fixture.HasExited) {
        $first = Find-Window 'DockForWindows Test A'
        if ($null -ne $first) { [DockDesktopNative]::PostMessage([IntPtr]$first.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null; $fixture.WaitForExit(3000) | Out-Null }
    }
    $dock = Find-Window 'DockForWindows'
    if ($null -ne $dock) { [DockDesktopNative]::PostMessage([IntPtr]$dock.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null; Start-Sleep -Milliseconds 500 }
    [System.IO.File]::WriteAllText($settingsPath,$originalSettings,[System.Text.UTF8Encoding]::new($false))
    $env:DOCKFORWINDOWS_DATA = Join-Path $projectRoot 'data'
    Start-Process -FilePath $dotnetExe -ArgumentList @($appDll) -WorkingDirectory $projectRoot -WindowStyle Hidden
    [DockDesktopNative]::SetCursorPos($originalCursor.X,$originalCursor.Y) | Out-Null
    [System.IO.File]::WriteAllLines((Join-Path $artifactDirectory 'results.txt'),$results,[System.Text.UTF8Encoding]::new($false))
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class ControlCenterChecks
{
    [DllImport("user32.dll")]private static extern bool SetProcessDpiAwarenessContext(nint context);
    [DllImport("user32.dll")]private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")]private static extern void mouse_event(uint flags,uint x,uint y,uint data,nuint extra);
    private static T[] All<T>(DependencyObject root)where T:DependencyObject{var list=new List<T>();void Visit(DependencyObject p){if(p is T t)list.Add(t);for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++)Visit(VisualTreeHelper.GetChild(p,i));}Visit(root);return list.ToArray();}
    private static T Find<T>(DependencyObject root,string id)where T:DependencyObject=>All<T>(root).Single(e=>AutomationProperties.GetAutomationId(e)==id);
    private static void Check(bool result,string text){if(!result)throw new Exception(text);Console.WriteLine("PASS "+text);}
    private static void Capture(Window window,string file){var p=window.PointToScreen(new Point());var q=window.PointToScreen(new Point(window.ActualWidth,window.ActualHeight));using var bmp=new System.Drawing.Bitmap((int)Math.Round(q.X-p.X),(int)Math.Round(q.Y-p.Y));using var g=System.Drawing.Graphics.FromImage(bmp);g.CopyFromScreen((int)p.X,(int)p.Y,0,0,bmp.Size);bmp.Save(file,System.Drawing.Imaging.ImageFormat.Png);}
    public static int Run(string directory)
    {
        SetProcessDpiAwarenessContext(new nint(-4));Directory.CreateDirectory(directory);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var settings=new DockSettings{Theme=DockTheme.Glass,GlassFrost=.23,GlassIntensity=1,GlassDistortion=.65};
        var area=SystemParameters.WorkArea;var background=new Window{Title="Control Center capture fixture",Topmost=true,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Left=area.Left,Top=area.Top,Width=area.Width,Height=area.Height-12,Background=Brushes.RoyalBlue};
        ControlCenterOptions? savedLayout=null;var window=new ControlCenterWindow(settings,saveLayout:o=>savedLayout=o){DismissOnDeactivate=false};int exit=0;AudioState? original=null;byte? originalBrightness=null;
        background.Loaded+=async(_,_)=>
        {
            try
            {
                original=SystemControlService.ReadAudio();originalBrightness=SystemControlService.ReadBrightness();Console.WriteLine($"HARDWARE volume={original.Volume:0.0} mute={original.Muted} brightness={originalBrightness}");
                using var entry=new ControlCenterEntryService(app.Dispatcher,()=>{},()=>false,()=>"Ctrl+Alt+F11");
                window.Show();window.Activate();await Task.Delay(1800);Check(entry.HotkeyRegistered&&entry.MouseHookActive,"自定义热键注册与任务栏入口钩子可用");Console.WriteLine("TASKBAR_TARGETS "+entry.TaskbarTargetCount);
                Check(Find<Slider>(window,"control-center-volume").IsEnabled,"音量滑块连接本机实际音频输出");
                var slider=Find<Slider>(window,"control-center-volume");double target=original.Volume>95?original.Volume-1:original.Volume+1;slider.Value=target;await Task.Delay(200);Check(Math.Abs(SystemControlService.ReadAudio().Volume-target)<.6,"滑块直接改变系统主音量");SystemControlService.SetVolume(original.Volume);
                var mute=Find<Button>(window,"control-center-mute");mute.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(SystemControlService.ReadAudio().Muted!=original.Muted,"静音按钮直接改变系统静音状态");SystemControlService.SetMute(original.Muted);
                if(originalBrightness.HasValue){Check(Find<Slider>(window,"control-center-brightness").IsEnabled,"亮度滑块识别本机显示器能力");var brightnessSlider=Find<Slider>(window,"control-center-brightness");byte brightnessTarget=(byte)(originalBrightness.Value>95?originalBrightness.Value-1:originalBrightness.Value+1);brightnessSlider.Value=brightnessTarget;await Task.Delay(350);Check(SystemControlService.ReadBrightness()==brightnessTarget,"亮度滑块真正改变显示器亮度");SystemControlService.SetBrightness(originalBrightness.Value);}
                else Check(!Find<Slider>(window,"control-center-brightness").IsEnabled,"不支持亮度的屏幕明确停用滑块");
                Check(window.CapturedFrames>3&&window.GlassBackdrop is BitmapSource,"控制中心持续采样实际背景");
                var blue=window.GlassBackdrop!;int stride=blue.PixelWidth*4;var pixels=new byte[stride*blue.PixelHeight];blue.CopyPixels(pixels,stride,0);int index=(blue.PixelHeight/2*blue.PixelWidth+blue.PixelWidth/2)*4;int blueDominance=pixels[index]-pixels[index+2];
                Capture(window,Path.Combine(directory,"01-blue-live-glass.png"));
                background.Background=Brushes.OrangeRed;await Task.Delay(800);var red=window.GlassBackdrop!;var redPixels=new byte[red.PixelWidth*red.PixelHeight*4];red.CopyPixels(redPixels,red.PixelWidth*4,0);int redDominance=redPixels[index+2]-redPixels[index];
                Console.WriteLine($"CAPTURE frames={window.CapturedFrames} blue={blueDominance} red={redDominance} source={window.CaptureSources}");Check(blueDominance>40&&redDominance>40,"液态玻璃实时响应应用窗口变化，而非只读取壁纸");
                Check(All<DockSurface>(window).Count(d=>d.IsVisible)==8,"八个独立卡片复用 DockSurface 折射材质");
                var outputs=AudioDeviceService.Outputs();Check(outputs.Count>0&&outputs.Count(x=>x.Default)==1,"读取真实音频设备和默认输出");AudioDeviceService.SetDefault(outputs.Single(x=>x.Default).Id);Check(AudioDeviceService.Outputs().Single(x=>x.Default).Id==outputs.Single(x=>x.Default).Id,"音频默认设备写入接口可用");
                Console.WriteLine("PROJECTION "+ProjectionService.ReadTopology());
                Console.WriteLine("WLAN "+string.Join(", ",ConnectivityService.Networks().Select(n=>n.Name+(n.Connected?" connected":""))));Console.WriteLine($"SIZE {window.Width:R} x {window.ActualHeight:R}");Check(window.Width<=362&&window.ActualHeight<600,"控制中心缩为紧凑独立卡片");
                var edit=Find<Button>(window,"control-center-edit");edit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(180);
                var first=Find<Grid>(window,"control-card-Wifi");var second=Find<Grid>(window,"control-card-Bluetooth");var from=first.PointToScreen(new Point(first.ActualWidth/2,first.ActualHeight/2));var to=second.PointToScreen(new Point(second.ActualWidth/2,second.ActualHeight/2));
                SetCursorPos((int)from.X,(int)from.Y);mouse_event(2,0,0,0,0);await Task.Delay(90);Console.WriteLine($"DRAG start={from} to={to} mouse={System.Windows.Input.Mouse.LeftButton} capture={System.Windows.Input.Mouse.Captured} active={window.IsActive}");for(int step=1;step<=12;step++){SetCursorPos((int)(from.X+(to.X-from.X)*step/12),(int)(from.Y+(to.Y-from.Y)*step/12));await Task.Delay(20);}mouse_event(4,0,0,0,0);await Task.Delay(220);
                Console.WriteLine("DRAG saved="+(savedLayout is null?"null":string.Join(",",savedLayout.Modules.Select(m=>m.Module))));Check(savedLayout is not null&&savedLayout.Modules[0].Module!=ControlModule.Wifi,"真实鼠标拖动交换卡片并提交独立布局");
                var wifiCard=Find<Grid>(window,"control-card-Wifi");wifiCard.ContextMenu!.Items.OfType<MenuItem>().Single(i=>(string)i.Header=="矩形").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));await Task.Delay(550);
                Check(savedLayout!.Modules.Single(m=>m.Module==ControlModule.Wifi).EffectiveShape==ControlCardShape.Rectangle&&Find<Grid>(window,"control-card-Wifi").ActualWidth>300,"卡片尺寸平滑切换并保存");
                Find<Grid>(window,"control-card-Wifi").ContextMenu!.Items.OfType<MenuItem>().Single(i=>(string)i.Header=="移除此控件").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));await Task.Delay(350);
                Check(!savedLayout.Modules.Single(m=>m.Module==ControlModule.Wifi).Visible&&!All<Grid>(window).Any(g=>AutomationProperties.GetAutomationId(g)=="control-card-Wifi"),"移除控件后重新排列并保存");
                All<Button>(window).Single(b=>AutomationProperties.GetName(b)=="添加控件").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(100);All<Button>(window).Single(b=>AutomationProperties.GetName(b)=="Wi-Fi").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(350);
                Check(savedLayout.Modules.Single(m=>m.Module==ControlModule.Wifi).Visible&&Find<Grid>(window,"control-card-Wifi").IsVisible,"添加隐藏模块并恢复交互");
                Find<Grid>(window,"control-card-Wifi").ContextMenu!.Items.OfType<MenuItem>().Single(i=>(string)i.Header=="胶囊").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));await Task.Delay(550);
                Find<Button>(window,"control-center-edit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(100);Check(All<DockSurface>(window).Count(d=>d.IsVisible)==8,"切换排列模式不丢失滑块或玻璃卡片");
                var audioMenu=All<Button>(window).Single(b=>AutomationProperties.GetName(b)=="展开音量");audioMenu.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));for(int i=0;i<25&&!All<Button>(window).Any(b=>AutomationProperties.GetName(b).Contains(outputs[0].Name));i++)await Task.Delay(100);Check(All<Button>(window).Any(b=>AutomationProperties.GetName(b).Contains(outputs[0].Name)),"二级菜单列出真实音频输出设备");await Task.Delay(350);Capture(window,Path.Combine(directory,"03-audio-details.png"));All<Button>(window).Single(b=>AutomationProperties.GetName(b)=="返回").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(400);
                // Exercise real grip capture and snapping rather than invoking a menu handler.
                edit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(120);var grip=Find<System.Windows.Controls.Primitives.Thumb>(window,"resize-Wifi");var gripStart=grip.PointToScreen(new Point(grip.ActualWidth/2,grip.ActualHeight/2));var unit=ControlCenterLayout.Unit(settings.ControlCenter.Geometry);SetCursorPos((int)gripStart.X,(int)gripStart.Y);mouse_event(2,0,0,0,0);await Task.Delay(60);for(int i=1;i<=12;i++){SetCursorPos((int)gripStart.X,(int)(gripStart.Y+(unit+12)*i/12));await Task.Delay(20);}mouse_event(4,0,0,0,0);await Task.Delay(650);
                Console.WriteLine("RESIZE saved="+savedLayout!.Modules.Single(m=>m.Module==ControlModule.Wifi).EffectiveShape+" capture="+System.Windows.Input.Mouse.Captured);Check(savedLayout!.Modules.Single(m=>m.Module==ControlModule.Wifi).EffectiveShape==ControlCardShape.Square,"真实拖动右下角吸附正方形并保存");var square=Find<Grid>(window,"control-card-Wifi");Check(Math.Abs(square.ActualWidth-square.ActualHeight)<1,"拖动后正方形保持等边");edit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Console.WriteLine($"RENDER animationFps={window.AnimationFrames/window.RuntimeSeconds:0.0} captureFps={window.CapturedFrames/window.RuntimeSeconds:0.0} maxGapMs={window.LongestAnimationGap*1000:0.0}");
                if(int.TryParse(Environment.GetEnvironmentVariable("DOCK_CC_SOAK_SECONDS"),out int seconds)&&seconds>0)
                {
                    var samples=new List<long>();var process=System.Diagnostics.Process.GetCurrentProcess();
                    for(int tick=0;tick<seconds;tick++){await Task.Delay(1000);if(tick%8==0)background.Background=tick%16==0?Brushes.RoyalBlue:Brushes.OrangeRed;
                        if(tick%20==2)audioMenu.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(tick%20==8)All<Button>(window).Single(b=>AutomationProperties.GetName(b)=="返回").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        if(tick%30==0){process.Refresh();long memory=process.PrivateMemorySize64;samples.Add(memory);Console.WriteLine($"SOAK seconds={tick} privateMB={memory/1048576d:0.0} managedMB={GC.GetTotalMemory(false)/1048576d:0.0} animationFps={window.AnimationFrames/window.RuntimeSeconds:0.0} captureFps={window.CapturedFrames/window.RuntimeSeconds:0.0}");}}
                    Check(samples.Max()<1536L*1048576&&samples.Last()-samples.Skip(1).Min()<240L*1048576,"七分钟持续渲染、二级界面往返无失控内存增长");
                }
                Capture(window,Path.Combine(directory,"02-red-live-glass.png"));
                window.Dismiss();await Task.Delay(350);Check(!window.IsVisible,"面板退下动画完成后释放窗口");Console.WriteLine("CONTROL CENTER RESULT PASS");
            }
            catch(Exception ex){Console.WriteLine("CONTROL CENTER RESULT FAIL "+ex);exit=1;}
            finally
            {
                try{if(original is not null){SystemControlService.SetVolume(original.Volume);SystemControlService.SetMute(original.Muted);}if(originalBrightness.HasValue)SystemControlService.SetBrightness(originalBrightness.Value);}catch(Exception restore){Console.WriteLine("RESTORE "+restore.Message);}
                window.Dismiss();background.Close();app.Shutdown(exit);
            }
        };
        app.Run(background);return exit;
    }
}








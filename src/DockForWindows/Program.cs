using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using DockForWindows.Views;
using Forms = System.Windows.Forms;

namespace DockForWindows;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try { NativeMethods.SetProcessDpiAwarenessContext(new nint(-4)); } catch { }
        using var mutex = new Mutex(true, @"Local\DockForWindows.Development.Instance", out bool first);
        using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\DockForWindows.Development.Show");
        using var settingsSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\DockForWindows.Development.Settings");
        using var exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\DockForWindows.Development.Exit");
        using var controlSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\DockForWindows.Development.ControlCenter");
        bool exitRequested = Array.Exists(args, a => a == "--shutdown");
        if (!first) { if (exitRequested) exitSignal.Set(); else if (Array.Exists(args,a=>a=="--control-center"))controlSignal.Set(); else if (Array.Exists(args, a => a == "--settings")) settingsSignal.Set(); else signal.Set(); return 0; }
        if (exitRequested) { mutex.ReleaseMutex(); return 0; }
        int profileIndex=Array.IndexOf(args,"--data-directory");
        string? explicitProfile=profileIndex>=0 && profileIndex+1<args.Length ? args[profileIndex+1] : null;
        var store = new SettingsService(explicitProfile);
        if (explicitProfile is null && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKFORWINDOWS_DATA"))) store.TryImportFrom(@"D:\DockForWindows\data");
        store.Log("启动 DockForWindows 0.6.0");
        try
        {
            var settings = store.Load();
            using var icons = new IconService();
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            var tracker = new WindowTracker(app.Dispatcher);
            var model = new DockViewModel(settings, store, icons);
            var window = new DockWindow(model, tracker);
            app.MainWindow = window;
            using var widgets=new WidgetDesktopService(model);
            tracker.WindowsChanged += model.UpdateWindows;
            app.DispatcherUnhandledException += (_, e) =>
            {
                store.Log("UI 异常", e.Exception);
                MessageBox.Show("程序遇到错误，日志：" + Path.Combine(store.DataDirectory, "dock.log") + "\n" + e.Exception.Message, "DockForWindows", MessageBoxButton.OK, MessageBoxImage.Error);
                // Terminate rather than continuing with possibly inconsistent UI state.
                e.Handled = true; app.Shutdown(1);
            };
            using var trayIcon = CreateTrayIcon();
            using var tray = new Forms.NotifyIcon { Text = "DockForWindows · 程序坞", Icon = trayIcon, Visible = true };
            using var controlTaskbar=new ControlCenterTaskbarEntry(window.ToggleControlCenter);window.ControlCenterStateChanged+=controlTaskbar.SetOpen;controlTaskbar.Show();
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("显示程序坞", null, (_, _) => window.Reveal());
            menu.Items.Add("添加应用与文件…", null, (_, _) => window.AddApplication());
            menu.Items.Add("外观与设置…", null, (_, _) => window.OpenSettings());
            menu.Items.Add("液态玻璃控制中心",null,(_,_)=>window.ToggleControlCenter());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("退出", null, (_, _) => app.Shutdown());
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (_, _) => window.Reveal();
            var registration = ThreadPool.RegisterWaitForSingleObject(signal, (_, _) => app.Dispatcher.BeginInvoke(window.Reveal), null, Timeout.Infinite, false);
            var settingsRegistration = ThreadPool.RegisterWaitForSingleObject(settingsSignal, (_, _) => app.Dispatcher.BeginInvoke(window.OpenSettings), null, Timeout.Infinite, false);
            var exitRegistration = ThreadPool.RegisterWaitForSingleObject(exitSignal, (_, _) => app.Dispatcher.BeginInvoke(() => app.Shutdown()), null, Timeout.Infinite, false);
            var controlRegistration=ThreadPool.RegisterWaitForSingleObject(controlSignal,(_,_)=>app.Dispatcher.BeginInvoke(window.ToggleControlCenter),null,Timeout.Infinite,false);
            using var taskbarVisibility=new TaskbarVisibilityService(()=>model.Settings.HideTaskbarApplications,(message,error)=>store.Log(message,error));
            using var controlEntry=new ControlCenterEntryService(app.Dispatcher,window.ToggleControlCenter,()=>model.Settings.UseGlassControlCenter,()=>model.Settings.ControlCenter.Hotkey,message=>{store.Log(message);tray.ShowBalloonTip(2500,"控制中心",message,Forms.ToolTipIcon.Info);});
            void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => app.Dispatcher.BeginInvoke(() => { window.RefreshSystemAppearance(); window.RefreshSettingsAppearance(); }, DispatcherPriority.Background);
            void OnDisplayChanged(object? sender, EventArgs e) => app.Dispatcher.BeginInvoke(window.PositionDock, DispatcherPriority.Background);
            SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            app.Exit += (_, _) =>
            {
                widgets.Dispose();
                window.ControlCenterStateChanged-=controlTaskbar.SetOpen;
                AwakeService.Shared.Dispose();
                registration.Unregister(null);
                settingsRegistration.Unregister(null);
                exitRegistration.Unregister(null);
                controlRegistration.Unregister(null);
                SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
                SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
                tray.Visible = false; menu.Dispose();
                model.Save(); store.Log("程序退出");
            };
            if (store.RecoveryNotice is not null) window.Loaded += (_, _) => window.ShowError(store.RecoveryNotice);
            if (Array.Exists(args, a => a == "--settings")) window.Loaded += (_, _) => window.OpenSettings();
            if(Array.Exists(args,a=>a=="--control-center"))window.Loaded+=(_,_)=>window.ToggleControlCenter();
            return app.Run(window);
        }
        catch (Exception ex)
        {
            store.Log("启动失败", ex);
            MessageBox.Show("启动失败：" + ex.Message, "DockForWindows", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
        finally { mutex.ReleaseMutex(); }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint icon);
    private static Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var drawing = Graphics.FromImage(bitmap);
        drawing.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var background = new SolidBrush(Color.FromArgb(44, 57, 82));
        using var accent = new SolidBrush(Color.FromArgb(117, 172, 255));
        using var white = new SolidBrush(Color.White);
        drawing.FillRectangle(background, 1, 9, 30, 20);
        drawing.FillRectangle(white, 5, 12, 5, 10);
        drawing.FillRectangle(accent, 13, 6, 6, 16);
        drawing.FillRectangle(white, 22, 12, 5, 10);
        nint handle = bitmap.GetHicon();
        try { using var source = Icon.FromHandle(handle); return (Icon)source.Clone(); }
        finally { DestroyIcon(handle); }
    }
}




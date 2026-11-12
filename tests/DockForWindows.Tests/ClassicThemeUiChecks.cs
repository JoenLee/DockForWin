using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using DockForWindows.Views;

internal static class ClassicThemeUiChecks
{
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(value)!;
    private static IEnumerable<T> Tree<T>(DependencyObject value)where T:DependencyObject{if(value is T item)yield return item;for(int i=0;i<VisualTreeHelper.GetChildrenCount(value);i++)foreach(var child in Tree<T>(VisualTreeHelper.GetChild(value,i)))yield return child;}
    private static T Find<T>(Window window,string id)where T:DependencyObject=>Tree<T>(window).Single(x=>AutomationProperties.GetAutomationId(x)==id);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;
        string? startup=StartupRegistration.ReadCommand();
        var settings=new DockSettings{AutoHide=false,HideWhenFullscreen=false,ShowRunningApps=false,ShowRecentApps=false,UseGlassControlCenter=false,HideTaskbarApplications=false};
        using var icons=new IconService();var store=new SettingsService(Path.Combine(directory,"isolated-ui-data"));var model=new DockViewModel(settings,store,icons);var tracker=new WindowTracker(app.Dispatcher);var dock=new DockWindow(model,tracker);
        dock.Loaded+=async(_,_)=>{
            try{
                dock.OpenSettings();await Task.Delay(450);var window=app.Windows.Cast<Window>().Single(x=>x.Title.StartsWith("程序坞设置"));window.Topmost=true;
                void Show(string id)=>window.GetType().GetMethod("ShowSetting",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,new object[]{id});
                Show("cc-material-mode");await Task.Delay(300);var mode=Find<SettingsSegmentedControl>(window,"cc-material-mode");mode.SelectedIndex=1;await Task.Delay(180);
                Check(Field<DockSettings>(window,"_draft").ControlCenter.MaterialMode==ControlCenterMaterialMode.ClassicBlur&&model.Settings.ControlCenter.MaterialMode==ControlCenterMaterialMode.LiquidGlass,"segmented theme changes draft only");
                var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(directory,"theme-settings.png")))encoder.Save(file);
                Show("control-live-preview");await Task.Delay(320);Find<CheckBox>(window,"control-live-preview").IsChecked=true;await Task.Delay(1300);
                var preview=Field<ControlCenterWindow>(window,"_controlPreview");var capture=Field<object>(preview,"_capture");Check(Field<ControlCenterOptions>(preview,"_options").MaterialMode==ControlCenterMaterialMode.ClassicBlur,"preview opens in selected classic theme");
                Show("cc-material-mode");await Task.Delay(300);mode=Find<SettingsSegmentedControl>(window,"cc-material-mode");mode.SelectedIndex=0;await Task.Delay(350);
                Check(ReferenceEquals(preview,Field<ControlCenterWindow>(window,"_controlPreview"))&&ReferenceEquals(capture,Field<object>(preview,"_capture"))&&Field<ControlCenterOptions>(preview,"_options").MaterialMode==ControlCenterMaterialMode.LiquidGlass,"live switch reuses preview and capture");
                mode.SelectedIndex=1;await Task.Delay(250);Find<Button>(window,"save-settings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(400);
                Check(store.Load().ControlCenter.MaterialMode==ControlCenterMaterialMode.ClassicBlur&&window.IsVisible,"save persists classic theme without closing settings");
                window.Close();await Task.Delay(200);Check(StartupRegistration.ReadCommand()==startup,"startup registration unchanged");result=0;
            }catch(Exception error){Console.Error.WriteLine(error);}
            finally{foreach(var window in app.Windows.Cast<Window>().Where(x=>x!=dock).ToArray())window.Close();dock.Close();app.Shutdown(result);}
        };app.Run(dock);return result;
    }
}

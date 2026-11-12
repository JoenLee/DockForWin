using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using DockForWindows.Views;

internal static class SettingsPreviewChecks
{
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(value)!;
    private static System.Collections.Generic.IEnumerable<T> Tree<T>(DependencyObject root)where T:DependencyObject{if(root is T value)yield return value;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var x in Tree<T>(VisualTreeHelper.GetChild(root,i)))yield return x;}
    private static T Find<T>(DependencyObject root,string id)where T:DependencyObject=>Tree<T>(root).Single(x=>AutomationProperties.GetAutomationId(x)==id);
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
    internal static int Run(string directory)
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;string? startup=StartupRegistration.ReadCommand();
        var settings=new DockSettings{AutoHide=false,HideWhenFullscreen=false,ShowRunningApps=false,ShowRecentApps=false,UseGlassControlCenter=false,HideTaskbarApplications=false};
        using var icons=new IconService();var store=new SettingsService(Path.Combine(directory,"isolated-settings"));var model=new DockViewModel(settings,store,icons);var tracker=new WindowTracker(app.Dispatcher);var dock=new DockWindow(model,tracker);
        dock.Loaded+=async(_,_)=>{
            try{
                dock.OpenSettings();await Task.Delay(400);var window=app.Windows.Cast<Window>().Single(x=>x.Title.StartsWith("程序坞设置"));window.Topmost=true;
                window.GetType().GetMethod("ShowSetting",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,new object[]{"control-live-preview"});await Task.Delay(320);Find<CheckBox>(window,"control-live-preview").IsChecked=true;await Task.Delay(900);
                var preview=Field<ControlCenterWindow>(window,"_controlPreview");Check(preview.IsVisible,"control center live preview opens alongside settings");
                window.GetType().GetMethod("ShowSetting",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,new object[]{"cc-rectangle-radius"});await Task.Delay(320);var range=Find<Slider>(window,"cc-rectangle-radius");range.Value=88;await Task.Delay(250);Check(ReferenceEquals(preview,Field<ControlCenterWindow>(window,"_controlPreview")),"preview updates in the existing window");Check(Field<ControlCenterOptions>(preview,"_options").Geometry.RectangleRoundness==88,"live preview receives unsaved geometry");
                Console.WriteLine($"save enabled={Find<Button>(window,"save-settings").IsEnabled}, colors={string.Join(",",Field<System.Collections.Generic.Dictionary<string,TextBox>>(window,"_colors").Select(k=>k.Key+":"+k.Value.Text))}, data={store.SettingsPath}");int clicked=0;Find<Button>(window,"save-settings").Click+=(_,_)=>clicked++;Find<Button>(window,"save-settings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Console.WriteLine($"click observed={clicked}, actualStore={Field<SettingsService>(model,"_store").SettingsPath}, modelRadius={model.Settings.ControlCenter.Geometry.RectangleRoundness}");await Task.Delay(300);Console.WriteLine($"after save visible={window.IsVisible}, file={File.Exists(store.SettingsPath)}, validation={Field<TextBlock>(window,"_validation").Text}");Check(window.IsVisible&&store.Load().ControlCenter.Geometry.RectangleRoundness==88,"save persists geometry and keeps settings open");
                range.Value=94;await Task.Delay(150);Find<Button>(window,"save-settings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(200);Check(window.IsVisible&&store.Load().ControlCenter.Geometry.RectangleRoundness==94,"repeated save stays on the same settings page");
                range.Value=72;await Task.Delay(150);Find<Button>(window,"cancel-settings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(500);var restored=app.Windows.Cast<Window>().Single(x=>x.Title.StartsWith("程序坞设置"));Check(restored.IsVisible&&Field<DockSettings>(restored,"_draft").ControlCenter.Geometry.RectangleRoundness==94,"cancel restores the last save while keeping settings visible");
                restored.Close();await Task.Delay(200);Check(!restored.IsVisible,"window X/Close ends settings");result=0;
            }catch(Exception ex){Console.Error.WriteLine(ex);}
            finally{if(StartupRegistration.ReadCommand()!=startup)throw new Exception("Preview unexpectedly changed startup registration");foreach(var window in app.Windows.Cast<Window>().Where(w=>w!=dock).ToArray())window.Close();dock.Close();app.Shutdown();}
        };app.Run(dock);return result;
    }
}

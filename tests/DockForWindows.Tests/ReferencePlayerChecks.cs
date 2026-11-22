using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;
internal static class ReferencePlayerChecks {
 internal static int Run(string output) {
  NativeMethods.SetProcessDpiAwarenessContext(-4);Directory.CreateDirectory(output);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  app.Startup+=async(_,_)=>{try {
   var fixture=(WidgetRenderData)typeof(WidgetReferenceChecks).GetMethod("Fixture",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!;
   foreach(var id in new[]{"music-form-horizontal","music-form-room","music-form-single","music-studio-record","music-pocket"}) {
    var commands=new List<string>();var data=fixture with {Media=fixture.Media! with {Title="Night Drive",Artist="Milo Fields",Playing=true},MediaCommand=commands.Add};
    var definition=RetiredWidgetFixtures.Get(id);var host=new WidgetHost(definition,2.5);host.SetMaterial(WidgetMaterial.Light);host.SetData(data);
    var window=new Window{Width=definition.Size.Width+40,Height=definition.Size.Height+40,Content=new Border{Padding=new Thickness(20),Background=WidgetBrushes.Get("#171F1A"),Child=host},WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,Left=30,Top=30,Topmost=true,ShowInTaskbar=false};
    try {window.Show();await Task.Delay(1100);window.UpdateLayout();Screenshot(host,Path.Combine(output,id+".png"));
     foreach(var command in new[]{"previous","toggle","next"}){var buttons=WidgetMusicView.Elements<Button>(host).Where(b=>AutomationProperties.GetAutomationId(b)=="widget-music-"+command).ToArray();if(command=="toggle"||id!="music-form-single"){if(buttons.Length==0)throw new Exception(id+command);buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}}
     if(commands.Count<1)throw new Exception("No command boundary");
     var records=WidgetMusicView.Elements<WidgetRecordPlayer>(host).ToArray();if(id!="music-pocket"&&records.Length!=1)throw new Exception("Missing record");
     if(records.Length>0){double a=records[0].DiscAngle;await Task.Delay(180);if(records[0].DiscAngle==a)throw new Exception("No rotation");host.SetData(data with {Media=data.Media! with {Playing=false}});await Task.Delay(600);if(records[0].IsSpinning)throw new Exception("Paused disc spins");}
     if(id!="music-form-single"&&!WidgetMusicView.Elements<Slider>(host).Any(s=>AutomationProperties.GetAutomationId(s)=="widget-music-seek"))throw new Exception("No seek");Console.WriteLine("PASS "+id);
    }finally {window.Close();}
   }
   var shell=new SettingsShellWindow(new DockSettings(),(_,_)=>{},0);shell.Show();await Task.Delay(500);Screenshot(shell,Path.Combine(output,"settings-shell.png"));shell.ClosePermanently();
   using(var icons=new IconService()) {
    var seed=new DockSettings{InterfaceTheme=InterfaceTheme.Light,StartWithWindows=StartupRegistration.ReadCommand()!=null};var store=new SettingsService(Path.Combine(output,"isolated-settings"));
    var model=new DockForWindows.ViewModels.DockViewModel(seed,store,icons);using var tracker=new WindowTracker(app.Dispatcher);var dock=new DockWindow(model,tracker);
    var settings=new SettingsWindow(dock,new Palette(seed),seed,0);
    var live=(CheckBox)typeof(SettingsWindow).GetField("_live",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(settings)!;live.IsChecked=false;
    settings.Topmost=true;settings.Show();settings.ShowSetting("theme-mode");await Task.Delay(450);settings.UpdateLayout();
    if(WidgetMusicView.Elements<Button>(settings).Any(b=>AutomationProperties.GetAutomationId(b).StartsWith("advanced-category-")))throw new Exception("Repeated navigation remains");
    foreach(var glyph in WidgetMusicView.Elements<WindowControlGlyph>(settings)){DependencyObject? parent=glyph;while(parent!=null&&parent is not Button)parent=VisualTreeHelper.GetParent(parent);if(parent is Button button){var center=glyph.TransformToAncestor(button).Transform(new Point(glyph.ActualWidth/2,glyph.ActualHeight/2));if(Math.Abs(center.X-button.ActualWidth/2)>.5||Math.Abs(center.Y-button.ActualHeight/2)>.5)throw new Exception("Window glyph not centered");}}
    Screenshot(settings,Path.Combine(output,"full-settings-without-secondary-navigation.png"));settings.ClosePermanently();dock.Close();
   }
   File.WriteAllText(Path.Combine(output,"result.json"),"{\"Passed\":5,\"Failed\":0,\"Fixture\":\"Fixed metadata and test command callbacks; actual WPF screen captures\"}");app.Shutdown();
  }catch(Exception e){Console.WriteLine(e);app.Shutdown(1);}};return app.Run();
 }
 static void Screenshot(FrameworkElement view,string file){var p=view.PointToScreen(new Point());var d=VisualTreeHelper.GetDpi(view);using var b=new System.Drawing.Bitmap((int)(view.ActualWidth*d.DpiScaleX),(int)(view.ActualHeight*d.DpiScaleY));using(var g=System.Drawing.Graphics.FromImage(b))g.CopyFromScreen((int)p.X,(int)p.Y,0,0,b.Size);b.Save(file);}
}

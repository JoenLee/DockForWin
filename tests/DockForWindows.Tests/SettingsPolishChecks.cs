using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Controls.Primitives;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using DockForWindows.Views;

internal static class SettingsPolishChecks
{
    private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    [DllImport("user32.dll")]private static extern bool SetProcessDpiAwarenessContext(nint context);
    [DllImport("user32.dll")]private static extern nint SendMessage(nint hwnd,uint message,nint wParam,nint lParam);
    private static object Field(object o,string name){for(Type? t=o.GetType();t!=null;t=t.BaseType)if(t.GetField(name,Flags|BindingFlags.DeclaredOnly) is FieldInfo f)return f.GetValue(o)!;throw new Exception(name);}
    private static IEnumerable<T> Tree<T>(DependencyObject o)where T:DependencyObject{if(o is T t)yield return t;for(int i=0;i<VisualTreeHelper.GetChildrenCount(o);i++)foreach(var child in Tree<T>(VisualTreeHelper.GetChild(o,i)))yield return child;}
    private static T Find<T>(Window w,string id)where T:DependencyObject=>Tree<T>(w).Single(x=>AutomationProperties.GetAutomationId(x)==id);
    private static void Click(Window w,string id)=>Find<Button>(w,id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
    private static void Screenshot(Window w,string path){var a=w.PointToScreen(new Point());var b=w.PointToScreen(new Point(w.ActualWidth,w.ActualHeight));using var bitmap=new System.Drawing.Bitmap((int)Math.Round(b.X-a.X),(int)Math.Round(b.Y-a.Y));using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.CopyFromScreen((int)a.X,(int)a.Y,0,0,bitmap.Size);bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}
    internal static int Preview(string directory,bool english)
    {
        SetProcessDpiAwarenessContext(-4);UiText.SetLanguage(english?"en-US":"zh-CN");
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};using var icons=new IconService();
        var store=new SettingsService(Path.Combine(directory,"preview-data"));var seed=store.Load();seed.Language=english?"en-US":"zh-CN";seed.StartWithWindows=StartupRegistration.ReadCommand()!=null;
        var model=new DockViewModel(seed,store,icons);using var tracker=new WindowTracker(app.Dispatcher);var dock=new DockWindow(model,tracker);
        var settings=new SettingsWindow(dock,new Palette(seed),seed,0);((CheckBox)Field(settings,"_live")).IsChecked=false;
        settings.Title=english?"DockForWin · Isolated preview":"DockForWin · 新版独立预览";
        typeof(DockWindow).GetField("_settings",Flags)!.SetValue(dock,settings);
        bool shown=false;settings.ContentRendered+=(_,_)=>shown=true;
        settings.IsVisibleChanged+=(_,_)=>{if(shown&&!settings.IsVisible)app.Dispatcher.BeginInvoke(()=>{settings.ClosePermanently();dock.Close();app.Shutdown();});};
        settings.Closed+=(_,_)=>app.Dispatcher.BeginInvoke(()=>{dock.Close();app.Shutdown();});
        settings.Show();settings.UpdateLayout();settings.ShowSetting("start-with-windows");settings.UpdateLayout();Find<CheckBox>(settings,"start-with-windows").IsEnabled=false;Click(settings,"settings-tab-general");settings.UpdateLayout();Find<CheckBox>(settings,"general-startup").IsEnabled=false;Click(settings,"settings-tab-appearance");app.Run();return 0;
    }
    internal static int Run(string directory,bool english)
    {
        SetProcessDpiAwarenessContext(-4);UiText.SetLanguage("zh-CN");new DockSettings().Normalize();_=GlassMaterial.Tints;_=ThemePresets.Names;UiText.SetLanguage(english?"en-US":"zh-CN");Directory.CreateDirectory(directory);
        int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};SettingsWindow? settings=null;DockWindow? dock=null;WindowTracker? tracker=null;IconService? icons=null;
        app.Startup+=async(_,_)=>{try{
            using(var stream=typeof(UiText).Assembly.GetManifestResourceStream("DockForWindows.Resources.Strings.en-US.json")!){var dictionary=JsonSerializer.Deserialize<Dictionary<string,string>>(stream)!;
                foreach(var pair in dictionary){var source=Regex.Matches(pair.Key,@"\{\d+(?:[^}]*)\}").Select(m=>m.Value).Order().ToArray();var translated=Regex.Matches(pair.Value,@"\{\d+(?:[^}]*)\}").Select(m=>m.Value).Order().ToArray();Check(source.SequenceEqual(translated),"translation placeholders: "+pair.Key.Replace('\n',' '));}
                Check(dictionary.Count>=943,"global English catalog is complete");}
            Check(UiText.Get("日历")== (english?"Calendar":"日历"),"language selection translates built-in text");
            Check(SettingsAccentPresets.Values.Last().Name==(english?"Black":"黑色")&&WidgetRegistry.Get("clock-world").Title==(english?"World clock":"世界时钟")&&GlassMaterial.Tints[1].Name==(english?"Ice blue":"冰蓝"),"catalogs loaded during config normalization follow the startup language");
            var presetKeys=new[]{"自由调整","雾白","深空","海湾蓝","暖沙"};var presetColors=new[]{"#242B38","#F2F5FA","#242B38","#173B57","#E9D8BD"};
            Check(ThemePresets.Names.SequenceEqual(presetKeys.Select(UiText.Get)),"appearance preset names follow language even after early catalog access");
            for(int i=0;i<presetKeys.Length;i++)Check(ThemePresets.Create(ThemePresets.Names[i]).BackgroundColor==presetColors[i]&&ThemePresets.Create(presetKeys[i]).BackgroundColor==presetColors[i],"localized appearance preset selects its original color: "+presetKeys[i]);
            string formatted=UiText.Format($"最高 {12.5:0}° / 最低 {3.5:0}°");Check(formatted==(english?"High 13° / Low 4°":"最高 13° / 最低 4°"),"interpolated numeric formatting follows selected language");
            var original=new DockSettings{Language=english?"en-US":"zh-CN",InterfaceTheme=InterfaceTheme.Light,Theme=DockTheme.Glass,InterfaceAccent="#000000",AutoHide=false,StartWithWindows=StartupRegistration.ReadCommand()!=null};
            original.Widgets.Notes.Add(new(){Title="提醒事项",Body="用户输入：项目笔记"});var copy=original.Copy();var applied=new DockSettings();applied.ApplyPreferences(copy);var store=new SettingsService(Path.Combine(directory,"data"));store.Save(applied);var loaded=store.Load();
            Check(loaded.Language==original.Language&&loaded.InterfaceAccent=="#000000","language and black accent survive copy/apply/save/load");Check(loaded.Widgets.Notes.Last().Title=="提醒事项"&&loaded.Widgets.Notes.Last().Body=="用户输入：项目笔记","user notes are preserved in both languages");loaded.Language="invalid";loaded.Normalize();Check(loaded.Language=="zh-CN","unsupported language falls back safely");
            icons=new IconService();var model=new DockViewModel(original,store,icons);tracker=new WindowTracker(app.Dispatcher);dock=new DockWindow(model,tracker);
            settings=new SettingsWindow(dock,new Palette(original),original,0);var draft=(DockSettings)Field(settings,"_draft");Check(((CheckBox)Field(settings,"_live")).IsChecked==true,"desktop Dock preview is enabled by default");typeof(DockWindow).GetField("_settings",Flags)!.SetValue(dock,settings);settings.Topmost=true;settings.Show();await Task.Delay(900);
            var sidebar=(Grid)Field(settings,"_sidebar");var outline=(Border)Field(settings,"_sidebarOutline");
            Check(sidebar.Margin==new Thickness(8)&&outline.CornerRadius.TopLeft==9&&outline.BorderThickness==new Thickness(0)&&((SolidColorBrush)outline.Background).Color==Colors.White,"sidebar is solid white and borderless with an equal inset");
            Check(((Border)Field(settings,"_sidebarShadow")).Effect is DropShadowEffect&&outline.Effect==null&&!Tree<ClassicBlurBackdrop>(sidebar).Any(),"sidebar has an independent shadow and no frost");
            Check(((RectangleGeometry)((GeometryDrawing)((DrawingBrush)((Grid)Field(settings,"_shellContent")).OpacityMask).Drawing).Geometry).RadiusX==12&&outline.CornerRadius.TopLeft==9&&sidebar.Margin.Left==8,"outer radius is 12 DIP while inner radius stays 9 DIP and inset stays 8 DIP");
            var capsule=Find<SquircleBorder>(settings,"settings-navigation-capsule");Check(capsule.Exponent==2.5&&capsule.CornerRadius.TopLeft==8&&capsule.Padding.Left==2&&capsule.BorderThickness.Left==0&&capsule.ActualHeight==Find<Button>(settings,"save-settings").ActualHeight,"navigation uses a compact 2.5 superellipse matching action height");
            async Task Switch(bool on){var box=Find<CheckBox>(settings,"live-preview");box.IsChecked=on;await Task.Delay(300);var knob=(Border)box.Template.FindName("SwitchKnob",box);var track=(Border)box.Template.FindName("SwitchTrack",box);var bounds=knob.TransformToAncestor(track.Parent as Visual).TransformBounds(new Rect(knob.RenderSize));var edge=track.TranslatePoint(new Point(track.ActualWidth,0),box);Check(knob.Width==27&&knob.Height==20&&Math.Abs(bounds.Top-2)<.6&&bounds.Left>=1.4&&bounds.Right<=track.ActualWidth-1.4&&box.ActualWidth-edge.X>=5,"original capsule switch has room for its edge and shadow: "+on);}
            var shell=(Grid)Field(settings,"_shellContent");
            var edgeBitmap=new RenderTargetBitmap((int)Math.Ceiling(shell.ActualWidth*2),(int)Math.Ceiling(shell.ActualHeight*2),192,192,PixelFormats.Pbgra32);edgeBitmap.Render(settings);int edgeStride=edgeBitmap.PixelWidth*4;var edgePixels=new byte[edgeStride*edgeBitmap.PixelHeight];edgeBitmap.CopyPixels(edgePixels,edgeStride,0);
            int soft=0;for(int y=0;y<24;y++)for(int x=0;x<24;x++){byte alpha=edgePixels[y*edgeStride+x*4+3];if(alpha>0&&alpha<255)soft++;}Screenshot(settings,Path.Combine(directory,"07-smooth-corners.png"));Check(settings.AllowsTransparency&&edgePixels[3]==0&&soft>8,"outer corner uses smooth partial alpha coverage rather than a hard native cut");
            var initialWidth=settings.Width;settings.Width+=40;settings.UpdateLayout();Check(((RectangleGeometry)((GeometryDrawing)((DrawingBrush)shell.OpacityMask).Drawing).Geometry).Rect.Size==shell.RenderSize,"smooth outline follows resized window bounds");settings.Width=initialWidth;settings.WindowState=WindowState.Maximized;await Task.Delay(120);Check(((RectangleGeometry)((GeometryDrawing)((DrawingBrush)shell.OpacityMask).Drawing).Geometry).RadiusX==0,"maximized window fills the work area without transparent corner gaps");settings.WindowState=WindowState.Normal;await Task.Delay(150);Check(((RectangleGeometry)((GeometryDrawing)((DrawingBrush)shell.OpacityMask).Drawing).Geometry).RadiusX==12,"restoring the window restores the same 12 DIP outer radius");
            int Hit(Point local){var physical=settings.PointToScreen(local);long packed=((long)(int)physical.Y<<16)|((long)(int)physical.X&0xFFFF);return (int)SendMessage(new WindowInteropHelper(settings).Handle,0x84,0,(nint)packed);}
            Check(Hit(new Point(settings.ActualWidth*.55,16))==2&&Hit(new Point(2,settings.ActualHeight*.5))==10,"smooth frame retains native caption dragging and edge resize hit testing");
            dock.Show();await Task.Delay(300);
            var iconSizeSlider=Find<Slider>(settings,"dock-icon-size");iconSizeSlider.Value=61;await Task.Delay(180);var strip=(DockStrip)Field(dock,"_strip");Check(strip.IconSize==61&&((DockSettings)Field(dock,"_previewSettings")).IconSize==61&&model.Settings.IconSize==original.IconSize,"changing size updates the Dock immediately without saving");
            var surface=(DockSurface)Field(dock,"_surface");dock.UpdateLayout();Check(dock.IsVisible&&surface.ActualHeight>0&&Math.Abs(surface.ActualHeight-89*(double)Field(dock,"_fitScale"))<1,"real Dock surface resizes to the changed icon size");Screenshot(dock,Path.Combine(directory,"00-live-dock.png"));dock.Hide();
            Find<Slider>(settings,"dock-spacing").Value=9;Find<Slider>(settings,"dock-magnification").Value=1.6;for(int attempt=0;attempt<20&&(strip.Gap!=9||Math.Abs(strip.Magnification-1.6)>=.01);attempt++)await Task.Delay(100);Check(strip.Gap==9&&Math.Abs(strip.Magnification-1.6)<.01,$"spacing and magnification reach the live Dock (gap={strip.Gap}, magnification={strip.Magnification})");
            Find<CheckBox>(settings,"live-preview").IsChecked=false;await Task.Delay(180);Check(Field(dock,"_previewSettings")==null&&strip.IconSize==original.IconSize,"turning off live preview restores committed Dock settings");
            await Switch(true);await Switch(false);await Switch(true);Screenshot(settings,Path.Combine(directory,"01-dock.png"));
            Click(settings,"settings-sidebar-toggle");await Task.Delay(200);double center=Find<Button>(settings,"settings-sidebar-toggle").TranslatePoint(new Point(16,0),settings).X;
            Check(Tree<ControlCenterIcon>(sidebar).All(icon=>Math.Abs(icon.TranslatePoint(new Point(icon.ActualWidth/2,0),settings).X-center)<.6),"collapsed sidebar icons align with toggle");Screenshot(settings,Path.Combine(directory,"02-collapsed.png"));Click(settings,"settings-sidebar-toggle");
            Click(settings,"settings-tab-general");await Task.Delay(250);var language=Find<ComboBox>(settings,"general-language");language.SelectedIndex=english?0:1;Check(draft.Language==(english?"zh-CN":"en-US")&&model.Settings.Language==original.Language,"language changes remain in the draft");language.SelectedIndex=english?1:0;Click(settings,"general-accent-000000");
            var accentGrid=Find<UniformGrid>(settings,"general-accent-grid");settings.UpdateLayout();var colorButtons=accentGrid.Children.Cast<Button>().ToArray();Check(colorButtons.All(b=>Math.Abs(b.ActualWidth-colorButtons[0].ActualWidth)<.6)&&Enumerable.Range(0,3).All(c=>Enumerable.Range(1,2).All(r=>Math.Abs(colorButtons[c].TranslatePoint(new Point(),accentGrid).X-colorButtons[r*3+c].TranslatePoint(new Point(),accentGrid).X)<.6)),"accent choices use three equal aligned columns");
            var custom=Find<TextBox>(settings,"general-accent-custom");custom.Text="#2a9fbd";await Task.Delay(100);Check(draft.InterfaceAccent=="#2A9FBD"&&model.Settings.InterfaceAccent=="#000000","custom accent previews only in the draft");
            var customCopy=new DockSettings();customCopy.ApplyPreferences(draft.Copy());customCopy.Normalize();var customStore=new SettingsService(Path.Combine(directory,"custom-color-data"));customStore.Save(customCopy);Check(customStore.Load().InterfaceAccent=="#2A9FBD","custom accent survives copy, normalize, apply, save and reload");
            custom.Text="#ZZZZZZ";Check(!Find<Button>(settings,"save-settings").IsEnabled&&draft.InterfaceAccent=="#2A9FBD","invalid custom color is rejected without replacing the last valid draft");custom.Text="#2A9FBD";
            var host=(SettingsPageHost)Field(settings,"_pages");var page=host.Children.OfType<ScrollViewer>().Single(p=>p.IsVisible);page.ScrollToVerticalOffset(30);settings.UpdateLayout();double offset=page.VerticalOffset;var hwnd=new WindowInteropHelper(settings).Handle;var boundsBefore=new Rect(settings.Left,settings.Top,settings.Width,settings.Height);int hidden=0;settings.IsVisibleChanged+=(_,_)=>{if(!settings.IsVisible)hidden++;};
            var mode=Find<SettingsSegmentedControl>(settings,"general-interface-theme");var live=Find<CheckBox>(settings,"live-preview");live.IsChecked=true;
            foreach(int index in new[]{2,1,2,1}){mode.SelectedIndex=index;await Task.Delay(120);Check(ReferenceEquals(Field(dock,"_settings"),settings)&&new WindowInteropHelper(settings).Handle==hwnd&&settings.IsVisible&&hidden==0&&host.CurrentKey=="general"&&Math.Abs(page.VerticalOffset-offset)<.5&&new Rect(settings.Left,settings.Top,settings.Width,settings.Height)==boundsBefore&&live.IsChecked==true,"theme changes keep the same visible window, page, scroll and preview state: "+index);var sidebarColor=((SolidColorBrush)outline.Background).Color;var sidebarInk=((SolidColorBrush)settings.Resources["SettingsFloatingSidebarInk"]).Color;var sidebarSelection=((SolidColorBrush)settings.Resources["SettingsFloatingSidebarSelection"]).Color;Check(sidebarColor==(index==2?DesignSystem.Hex("#242427"):Colors.White)&&((index==2&&sidebarInk.R>200)||(index==1&&sidebarInk.R<64))&&sidebarSelection.A>0&&sidebarSelection.R==(index==2?255:0),"sidebar background, text and selected row follow light/dark appearance");Check(Tree<ControlCenterIcon>(sidebar).All(icon=>((SolidColorBrush)icon.Brush).Color==sidebarInk),"sidebar icons use the matching contrasting foreground");if(index==2)Screenshot(settings,Path.Combine(directory,"06-dark.png"));}
            custom.Text="#000000";live.IsChecked=false;await Task.Delay(150);Screenshot(settings,Path.Combine(directory,"03-general.png"));
            Click(settings,"settings-tab-widgets");await Task.Delay(300);Click(settings,"widget-home-notes");await Task.Delay(600);
            var choices=Find<ListBox>(settings,"widget-material");Check(choices.Items.Count==2,"widget material selector contains only Light and Dark");
            foreach(var category in new[]{"notes","clock","calendar","weather","reminders","music"}){
                var gallery=Find<ScrollViewer>(settings,"widget-gallery");gallery.GetType().GetMethod("ShowCategory",Flags)!.Invoke(gallery,new object[]{Enum.Parse<WidgetKind>(category,true)});await Task.Delay(100);
                var reset=Find<Button>(settings,"widget-color-reset-"+category);var gradient=Find<TextBox>(settings,"widget-gradient-end-"+category);var size=Find<ComboBox>(settings,"widget-size-"+category);var inspector=Find<ScrollViewer>(settings,"widget-inspector-scroll");
                Check(Math.Abs(reset.ActualWidth-gradient.ActualWidth)<1&&Math.Abs(size.ActualWidth-gradient.ActualWidth)<1,"widget inspector controls have equal widths: "+category);
                var expander=Find<Expander>(settings,"widget-colors-"+category);var header=(FrameworkElement)expander.Template.FindName("HeaderSite",expander);Check(header.TranslatePoint(new Point(),expander).X>=0,"expander header has no clipped negative margin: "+category);
                if(category=="notes")Screenshot(settings,Path.Combine(directory,"04-notes.png"));
            }
            if(english){var labels=Tree<TextBlock>(settings).Where(x=>x.IsVisible&&Regex.IsMatch(x.Text,"[\\u4e00-\\u9fff]")).Select(x=>x.Text).Distinct().ToArray();Check(labels.Length==0,"visible widget UI is English: "+string.Join(" | ",labels));}
            Click(settings,"settings-tab-control-center");await Task.Delay(250);Screenshot(settings,Path.Combine(directory,"05-control-center.png"));
            Click(settings,"save-settings");await Task.Delay(200);Check(store.Load().Language==draft.Language&&store.Load().InterfaceAccent=="#000000","Save commits language and black accent through normal persistence");
            result=0;
        }catch(Exception ex){Console.Error.WriteLine(ex);}finally{settings?.ClosePermanently();dock?.Close();tracker?.Dispose();icons?.Dispose();app.Shutdown(result);}};
        app.Run();return result;
    }
}

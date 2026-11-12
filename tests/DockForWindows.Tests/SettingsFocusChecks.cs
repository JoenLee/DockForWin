using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using DockForWindows.Views;

internal static class SettingsFocusChecks
{
    private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    private static System.Collections.Generic.IEnumerable<T> Tree<T>(DependencyObject root)where T:DependencyObject{if(root is T found)yield return found;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Tree<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private static T Find<T>(Window window,string id)where T:DependencyObject=>Tree<T>(window).Single(x=>AutomationProperties.GetAutomationId(x)==id);
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
    private static bool Clear(Border border)=>border.BorderBrush is SolidColorBrush brush&&brush.Color.A==0;
    internal static int Run(string directory,bool iconsOnly=false,bool colorOnly=false)
    {
        Directory.CreateDirectory(directory);int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};using var icons=new IconService();var seed=new DockSettings{InterfaceTheme=InterfaceTheme.Light};var model=new DockViewModel(seed,new SettingsService(Path.Combine(directory,"isolated-data")),icons);var tracker=new WindowTracker(app.Dispatcher);var dock=new DockWindow(model,tracker);Window? window=null;
        app.Startup+=async(_,_)=>{try{
            var assembly=typeof(DockSurface).Assembly;var type=assembly.GetType("DockForWindows.Views.SettingsWindow")!;var palette=Activator.CreateInstance(assembly.GetType("DockForWindows.Views.Palette")!,new object[]{seed})!;window=(Window)Activator.CreateInstance(type,new object[]{dock,palette,seed,0})!;((CheckBox)type.GetField("_live",Flags)!.GetValue(window)!).IsChecked=false;window.Topmost=true;window.Show();await Task.Delay(300);
            var nav=Find<ListBox>(window,"settings-navigation");var item=(ListBoxItem)nav.Items[3];Keyboard.Focus(item);await Task.Delay(80);
            if(colorOnly){
                type.GetMethod("ShowSetting",Flags)!.Invoke(window,new object[]{"cc-color-mode"});await Task.Delay(310);
                var colors=Find<ComboBox>(window,"cc-color-mode");Check(colors.BorderThickness==new Thickness(0)&&Tree<Border>(colors).First().BorderThickness==new Thickness(0),"color setting renders without an inner border");
                colors.IsDropDownOpen=true;await Task.Delay(120);Check(colors.IsDropDownOpen,"borderless color setting still opens its native dropdown");colors.IsDropDownOpen=false;colors.SelectedIndex=1;await Task.Delay(140);
                var draft=(DockSettings)type.GetField("_draft",Flags)!.GetValue(window)!;Check(draft.ControlCenter.ColorMode==ControlCenterColorMode.Light&&seed.ControlCenter.ColorMode!=ControlCenterColorMode.Light,"color choice still edits only the draft");
                Capture(window,Path.Combine(directory,"single-color-frame.png"));result=0;return;
            }
            if(iconsOnly){
                foreach(var glyph in Tree<System.Windows.Shapes.Path>(nav).Where(p=>AutomationProperties.GetAutomationId(p).StartsWith("navigation-glyph-")))Check(glyph.Data.IsFrozen&&glyph.Width==18&&glyph.Height==18,"filled sidebar icon uses frozen native geometry");
                Capture(window,Path.Combine(directory,"ionicons-filled.png"));result=0;return;
            }
            Check(item.IsKeyboardFocused&&Clear((Border)item.Template.FindName("Hover",item)!)&&item.FocusVisualStyle==null,"sidebar keeps its selected background without a blue focus border");
            var button=Find<Button>(window,"save-settings");Keyboard.Focus(button);Check(button.IsKeyboardFocused&&Clear((Border)button.Template.FindName("Body",button)!)&&button.FocusVisualStyle==null,"buttons have no focus outline");
            var segment=Find<ListBox>(window,"theme-mode-segment");var option=(ListBoxItem)segment.ItemContainerGenerator.ContainerFromIndex(segment.SelectedIndex);Keyboard.Focus(option);Check(Clear((Border)option.Template.FindName("Focus",option)!)&&option.FocusVisualStyle==null&&segment.FocusVisualStyle==null,"segmented options keep their indicator without a focus border");
            var slider=Find<Slider>(window,"transparency");Keyboard.Focus(slider);var track=(Track)slider.Template.FindName("PART_Track",slider)!;Check(slider.IsKeyboardFocused&&slider.FocusVisualStyle==null&&track.Thumb.FocusVisualStyle==null,"slider and thumb have no default focus adornment");
            var combo=Find<ComboBox>(window,"theme-preset");var border=Tree<Border>(combo).First();var before=border.BorderBrush;Keyboard.Focus(combo);Check(combo.IsKeyboardFocused&&Equals(border.BorderBrush,before)&&combo.FocusVisualStyle==null,"dropdown focus does not change its border");
            var search=Find<TextBox>(window,"settings-shell-search");Keyboard.Focus(search);search.Text="开机";await Task.Delay(100);Check(nav.SelectedIndex==6&&search.FocusVisualStyle==null&&search.Template.FindName("PART_ContentHost",search) is ScrollViewer,"borderless-focus text boxes still accept search input");search.Clear();
            type.GetMethod("ShowSetting",Flags)!.Invoke(window,new object[]{"start-with-windows"});await Task.Delay(300);var toggle=Find<CheckBox>(window,"start-with-windows");Keyboard.Focus(toggle);Check(toggle.IsKeyboardFocused&&Clear((Border)toggle.Template.FindName("SwitchTrack",toggle)!)&&toggle.FocusVisualStyle==null,"switch retains its state without a blue focus stroke");
            nav.SelectedIndex=0;await Task.Delay(250);Keyboard.Focus((ListBoxItem)nav.Items[0]);await Task.Delay(80);Capture(window,Path.Combine(directory,"no-focus-border.png"));result=0;
        }catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);}finally{window?.Close();dock.Close();tracker.Dispose();app.Shutdown(result);}};app.Run();return result;
    }
    private static void Capture(Window window,string file){var a=window.PointToScreen(new Point());var b=window.PointToScreen(new Point(window.ActualWidth,window.ActualHeight));using var bitmap=new System.Drawing.Bitmap((int)(b.X-a.X),(int)(b.Y-a.Y));using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.CopyFromScreen((int)a.X,(int)a.Y,0,0,bitmap.Size);bitmap.Save(file,System.Drawing.Imaging.ImageFormat.Png);}
}

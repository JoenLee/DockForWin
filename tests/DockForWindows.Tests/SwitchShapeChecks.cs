using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DockForWindows.Views;

internal static class SwitchShapeChecks
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    private static Rect Bounds(CheckBox box){var knob=(Border)box.Template.FindName("SwitchKnob",box)!;return knob.TransformToAncestor((Grid)VisualTreeHelper.GetParent(knob)).TransformBounds(new Rect(knob.RenderSize));}
    private static void Contained(CheckBox box){var knob=(Border)box.Template.FindName("SwitchKnob",box)!;var parent=(Grid)VisualTreeHelper.GetParent(knob);var rect=Bounds(box);if(rect.Left<3.9||rect.Top<3.9||parent.ActualWidth-rect.Right<3.9||parent.ActualHeight-rect.Bottom<3.9)throw new Exception("Thumb intrudes into the track edge: "+rect);}
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var off=new CheckBox{Content="关闭",IsChecked=false};var on=new CheckBox{Content="开启",IsChecked=true};var body=new StackPanel();body.Children.Add(off);body.Children.Add(on);
        var window=new Window{Title="开关比例",Width=380,Height=225,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen,Topmost=true,Background=new SolidColorBrush(Color.FromRgb(244,244,244)),Content=new Border{Margin=new Thickness(22),Padding=new Thickness(20,10,20,10),CornerRadius=new CornerRadius(12),Background=Brushes.White,Child=body}};
        window.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/DockForWindows;component/Views/SettingsShellStyles.xaml",UriKind.Relative)});
        window.Loaded+=async(_,_)=>{try{
            await Task.Delay(200);Contained(off);Contained(on);
            var offColor=((SolidColorBrush)((Border)off.Template.FindName("SwitchTrack",off)!).Background).Color;var onColor=((SolidColorBrush)((Border)on.Template.FindName("SwitchTrack",on)!).Background).Color;Check(offColor==Color.FromRgb(233,233,235)&&onColor==Color.FromRgb(8,8,8),"off track is grey and checked track is black");Check(true,"both switch states retain a visible inset on all four sides");
            var left=Bounds(off);var right=Bounds(on);Check(Math.Abs(left.Top-left.Bottom+18)<1&&Math.Abs(left.Top-right.Top)<.1,"thumb height and vertical centering stay identical between states");
            var knob=(Border)on.Template.FindName("SwitchKnob",on)!;Check(knob.Effect!=null&&knob.CornerRadius.TopLeft==9&&knob.Width==knob.Height,"thumb is a true circle with a subtle shadow");
            for(int i=0;i<24;i++){on.IsChecked=i%2==0;await Task.Delay(16);Contained(on);}on.IsChecked=true;await Task.Delay(300);Contained(on);Check(true,"rapid reversal keeps the thumb inside the track throughout motion");
            var shift=(TranslateTransform)((TransformGroup)knob.RenderTransform).Children[1];Check(Math.Abs(shift.X-18)<.01,"checked state settles at the correct inset");
            var a=window.PointToScreen(new Point());var b=window.PointToScreen(new Point(window.ActualWidth,window.ActualHeight));using var image=new System.Drawing.Bitmap((int)(b.X-a.X),(int)(b.Y-a.Y));using(var graphics=System.Drawing.Graphics.FromImage(image))graphics.CopyFromScreen((int)a.X,(int)a.Y,0,0,image.Size);image.Save(Path.Combine(directory,"switch-shape.png"),System.Drawing.Imaging.ImageFormat.Png);result=0;
        }catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);}finally{window.Close();app.Shutdown(result);}};app.Run(window);return result;
    }
}

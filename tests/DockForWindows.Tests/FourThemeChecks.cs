using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using DockForWindows.Views;

internal static class FourThemeChecks
{
    [DllImport("user32.dll")]private static extern bool SetProcessDpiAwarenessContext(nint c);
    private static T[] All<T>(DependencyObject root)where T:DependencyObject{
        var seen=new HashSet<DependencyObject>();void Visit(DependencyObject p){if(!seen.Add(p))return;foreach(var c in LogicalTreeHelper.GetChildren(p).OfType<DependencyObject>())Visit(c);if(p is Visual)for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++)Visit(VisualTreeHelper.GetChild(p,i));}Visit(root);return seen.OfType<T>().ToArray();
    }
    private static T Find<T>(DependencyObject root,string id)where T:DependencyObject=>All<T>(root).Single(x=>AutomationProperties.GetAutomationId(x)==id);
    private static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS "+text);}
    private static void Capture(Window w,string file){var root=(FrameworkElement)w.Content;var a=root.PointToScreen(new Point());var b=root.PointToScreen(new Point(root.ActualWidth,root.ActualHeight));using var image=new System.Drawing.Bitmap((int)(b.X-a.X),(int)(b.Y-a.Y));using var g=System.Drawing.Graphics.FromImage(image);g.CopyFromScreen((int)a.X,(int)a.Y,0,0,image.Size);image.Save(file,System.Drawing.Imaging.ImageFormat.Png);}
    public static int Run(string directory)=>FullSettingsChecks.Run(directory);
    private static async Task CheckButtonOptics(string directory)
    {
        var backdrop=new LinearGradientBrush(Colors.Black,Colors.White,90);
        var chrome=new GlassButtonChrome{Width=220,Height=60,SurfaceBrush=Brushes.Transparent,BackdropBrush=backdrop,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        var probe=new Window{Title="Button glass optics check",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,Width=320,Height=120,Left=120,Top=120,Background=Brushes.Gray,Content=chrome};
        try{
            probe.Show();await Task.Delay(250);
            var effects=All<FrameworkElement>(chrome).Select(e=>e.Effect).OfType<LiquidGlassEffect>().ToArray();Check(effects.Length==2,"玻璃按钮已创建实际 GPU 折射通道");
            var lens=effects.Single(e=>e.LightingOnly==0);var shape=lens.Shape;lens.Shape=new Point(shape.X,0);await Task.Delay(120);
            using var flat=Snapshot(chrome);lens.Shape=shape;await Task.Delay(120);using var bent=Snapshot(chrome);
            int x=bent.Width/2,y=Math.Max(2,(int)(3*VisualTreeHelper.GetDpi(chrome).DpiScaleY));
            int edgeChange=Math.Abs(flat.GetPixel(x,y).G-bent.GetPixel(x,y).G),middleChange=Math.Abs(flat.GetPixel(x,bent.Height/2).G-bent.GetPixel(x,bent.Height/2).G);
            Console.WriteLine($"BUTTON OPTICS edgeChange={edgeChange} centerChange={middleChange}");
            Check(edgeChange>=4&&middleChange<=2,"按钮折射确实改变边缘采样且不拉伸中心背景");bent.Save(Path.Combine(directory,"07-button-lens-proof.png"),System.Drawing.Imaging.ImageFormat.Png);
            chrome.SurfaceBrush=Brushes.Black;await Task.Delay(100);using var black=Snapshot(chrome);Check(black.GetPixel(x,black.Height/2).R<20,"黑色按钮使用玻璃后仍保持黑色");
            chrome.SurfaceBrush=Brushes.White;await Task.Delay(100);using var white=Snapshot(chrome);Check(white.GetPixel(x,white.Height/2).R>245,"白色按钮使用玻璃后仍保持白色");
        }finally{probe.Close();}
    }
    private static System.Drawing.Bitmap Snapshot(FrameworkElement e)
    {
        var a=e.PointToScreen(new Point());var b=e.PointToScreen(new Point(e.ActualWidth,e.ActualHeight));var image=new System.Drawing.Bitmap((int)Math.Round(b.X-a.X),(int)Math.Round(b.Y-a.Y));using var g=System.Drawing.Graphics.FromImage(image);g.CopyFromScreen((int)a.X,(int)a.Y,0,0,image.Size);return image;
    }
}



using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using DockForWindows.Views;
using Color=System.Windows.Media.Color;
using Brushes=System.Windows.Media.Brushes;
using Point=System.Windows.Point;

internal static class ShaderEquivalenceChecks
{
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static Bitmap Snapshot(FrameworkElement element)
    {
        var start=element.PointToScreen(new Point());var end=element.PointToScreen(new Point(element.ActualWidth,element.ActualHeight));
        var bitmap=new Bitmap((int)Math.Round(end.X-start.X),(int)Math.Round(end.Y-start.Y));
        using var g=Graphics.FromImage(bitmap);g.CopyFromScreen((int)start.X,(int)start.Y,0,0,bitmap.Size);return bitmap;
    }
    internal static int Run(string originalShaderPath,string directory)
    {
        Directory.CreateDirectory(directory);int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var surface=new DockSurface{Width=360,Height=160,UseGlass=true,Fill=Brushes.White,GlassOpacity=1,GlassIntensity=1,GlassDistortion=1,GlassFrost=1,GlassHighlight=1,GlassHighlightWidth=8,GlassSideDarkness=1,GlassTint=.25,StrokeWidth=0,ConstrainFrostToOutline=true};
        var window=new Window{Width=440,Height=240,Left=120,Top=120,WindowStyle=WindowStyle.None,ShowInTaskbar=false,Topmost=true,Background=Brushes.DarkSlateGray,Content=surface};
        window.Loaded+=async(_,_)=>{
            try{
                var origin=surface.PointToScreen(new Point());var dpi=VisualTreeHelper.GetDpi(surface);int pad=180,width=(int)Math.Ceiling(surface.ActualWidth*dpi.DpiScaleX)+2*pad,height=(int)Math.Ceiling(surface.ActualHeight*dpi.DpiScaleY)+2*pad;
                var pixels=new byte[width*height*4];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++){int i=(y*width+x)*4;pixels[i]=(byte)((x/9+y/7)%2==0?30:235);pixels[i+1]=(byte)(y%256);pixels[i+2]=(byte)(x%256);pixels[i+3]=255;}
                var source=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);source.Freeze();surface.BackdropScreenBounds=new Rect(origin.X-pad,origin.Y-pad,width,height);surface.Backdrop=source;
                ((BlurEffect)typeof(DockSurface).GetField("_frost",Private)!.GetValue(surface)!).RenderingBias=RenderingBias.Quality;
                var lens=(LiquidGlassEffect)typeof(DockSurface).GetField("_refraction",Private)!.GetValue(surface)!;var lighting=(LiquidGlassEffect)typeof(DockSurface).GetField("_lighting",Private)!.GetValue(surface)!;
                var property=typeof(ShaderEffect).GetProperty("PixelShader",Private)!;
                var candidate=(PixelShader)property.GetValue(lens)!;var original=new PixelShader{UriSource=new Uri(Path.GetFullPath(originalShaderPath))};
                await Task.Delay(300);int cases=0,globalMax=0;double globalMean=0;
                foreach(double radius in new[]{0d,56d,85d,100d})foreach(double frost in new[]{0d,1d})foreach(double distortion in new[]{0d,.65,1d}){
                    surface.Radius=radius;surface.GlassFrost=frost;surface.GlassDistortion=distortion;
                    property.SetValue(lens,original);property.SetValue(lighting,original);surface.InvalidateVisual();await Task.Delay(85);using var before=Snapshot(surface);
                    property.SetValue(lens,candidate);property.SetValue(lighting,candidate);surface.InvalidateVisual();await Task.Delay(85);using var after=Snapshot(surface);
                    long sum=0;int maximum=0;
                    for(int y=0;y<before.Height;y++)for(int x=0;x<before.Width;x++){
                        var a=before.GetPixel(x,y);var b=after.GetPixel(x,y);int r=Math.Abs(a.R-b.R),g=Math.Abs(a.G-b.G),bb=Math.Abs(a.B-b.B);sum+=r+g+bb;maximum=Math.Max(maximum,Math.Max(r,Math.Max(g,bb)));
                    }
                    double mean=sum/(double)(before.Width*before.Height*3);globalMax=Math.Max(globalMax,maximum);globalMean=Math.Max(globalMean,mean);
                    if(maximum>2||mean>.02)throw new Exception($"Shader mismatch radius={radius}, frost={frost}, distortion={distortion}: max={maximum}, mean={mean}");
                    if(radius==56&&frost==1&&distortion==1){before.Save(Path.Combine(directory,"max-quality-before.png"));after.Save(Path.Combine(directory,"max-quality-after.png"));}
                    cases++;
                }
                string report=$"PASS {cases} real GPU screen comparisons at {dpi.DpiScaleX*100:F2}% DPI; highest-quality Gaussian + complete lighting/refraction; max channel delta={globalMax}/255, worst mean RGB delta={globalMean:F6}/255";
                File.WriteAllText(Path.Combine(directory,"result.txt"),report);Console.WriteLine(report);result=0;
            }catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);}
            finally{window.Close();app.Shutdown(result);}
        };
        app.Run(window);return result;
    }
}

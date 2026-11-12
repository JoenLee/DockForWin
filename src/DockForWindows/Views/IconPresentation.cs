using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;

namespace DockForWindows.Views;

public static class IconPresentation
{
    private sealed class Variants { public ImageSource? Light, Dark, Naked; }
    private static readonly ConditionalWeakTable<ImageSource,Variants> Cache = new();
    public static ImageSource? Normalize(ImageSource? source, ItemKind kind, bool dark, bool preservePlate=false)
    {
        if(source is null)return source;
        if(kind==ItemKind.RecycleBin&&preservePlate)return source;
        var cache=Cache.GetOrCreateValue(source); var ready=preservePlate?(dark?cache.Dark:cache.Light):cache.Naked; if(ready is not null)return ready;
        const int size=256;
        double aspect=source.Width>0 && source.Height>0?source.Width/source.Height:1;
        double drawWidth=aspect>=1?size:size*aspect,drawHeight=aspect>=1?size/aspect:size;
        var drawing=new DrawingVisual(); using(var dc=drawing.RenderOpen()) dc.DrawImage(source,new Rect((size-drawWidth)/2,(size-drawHeight)/2,drawWidth,drawHeight));
        var bitmap=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32); bitmap.Render(drawing);
        var pixels=new byte[size*size*4];bitmap.CopyPixels(pixels,size*4,0);
        int left=size,top=size,right=-1,bottom=-1,opaque=0,white=0;
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){
            int p=(y*size+x)*4; if(pixels[p+3]<40)continue;
            left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y); opaque++;
            if(Math.Min(pixels[p],Math.Min(pixels[p+1],pixels[p+2]))>pixels[p+3]*.88)white++;
        }
        if(right<left||bottom<top)return source;
        int width=right-left+1,height=bottom-top+1;
        bool square=Math.Abs(width-height)<=Math.Max(width,height)*.12;
        int edge=0,total=0;
        for(int i=1;i<9;i++){
            int x=left+width*i/10,y=top+height*i/10;
            foreach(var p in new[]{(top*size+x)*4,((bottom)*size+x)*4,(y*size+left)*4,(y*size+right)*4}){total++;if(pixels[p+3]>150)edge++;}
        }
        bool regular=square && opaque/(double)(width*height)>.83 && edge>total*.5;
        bool monochromeWhite=!regular && white>opaque*.88;
        var foreground=new CroppedBitmap(bitmap,new Int32Rect(left,top,width,height));foreground.Freeze();
        if(!preservePlate){
            var naked=new DrawingVisual();using(var dc=naked.RenderOpen()){
                double scale=size*.88/Math.Max(width,height);dc.DrawImage(foreground,new Rect((size-width*scale)/2,(size-height*scale)/2,width*scale,height*scale));
            }
            var normalized=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);normalized.Render(naked);normalized.Freeze();return cache.Naked=normalized;
        }
        ImageSource icon=foreground;
        if(monochromeWhite){
            // Preserve alpha/antialiasing, remove only the white glyph's RGB.
            for(int p=0;p<pixels.Length;p+=4)for(int c=0;c<3;c++)pixels[p+c]=(byte)(pixels[p+3]-pixels[p+c]);
            var black=BitmapSource.Create(size,size,96,96,PixelFormats.Pbgra32,null,pixels,size*4);
            icon=new CroppedBitmap(black,new Int32Rect(left,top,width,height)); icon.Freeze();
        }
        var output=new DrawingVisual();using(var dc=output.RenderOpen()){
            var outline=SquircleBorder.Outline(new Size(size,size),size*.24);
            dc.PushClip(outline);
            if(!regular){
                var face=monochromeWhite||!dark
                    ? new LinearGradientBrush(new GradientStopCollection{new(Colors.White,0),new(Color.FromRgb(245,245,248),.55),new(Colors.White,1)},new Point(0,0),new Point(0,1))
                    : new LinearGradientBrush(new GradientStopCollection{new(Color.FromRgb(25,25,28),0),new(Color.FromRgb(12,12,14),.55),new(Color.FromRgb(23,23,26),1)},new Point(0,0),new Point(0,1));
                dc.DrawGeometry(face,null,outline);
                double inset=size*.16,scale=Math.Min((size-2*inset)/width,(size-2*inset)/height);
                dc.DrawImage(icon,new Rect((size-width*scale)/2,(size-height*scale)/2,width*scale,height*scale));
                var rim=new LinearGradientBrush(new GradientStopCollection{new(Color.FromArgb(dark&&!monochromeWhite?(byte)110:(byte)220,255,255,255),0),new(Colors.Transparent,.12),new(Colors.Transparent,.88),new(Color.FromArgb(65,255,255,255),1)},new Point(0,0),new Point(0,1));
                dc.DrawGeometry(null,new Pen(rim,1.8),outline);
            }else dc.DrawImage(foreground,new Rect(0,0,size,size));
            dc.Pop();
        }
        var result=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);result.Render(output);result.Freeze();
        if(dark)cache.Dark=result;else cache.Light=result;return result;
    }
}

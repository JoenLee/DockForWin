using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;

namespace DockForWindows.Services;

public static class ScreenshotService
{
    private static BitmapSource Convert(System.Drawing.Bitmap bitmap)
    {
        var rect=new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height);var data=bitmap.LockBits(rect,ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try{int stride=bitmap.Width*4;var pixels=new byte[stride*bitmap.Height];for(int y=0;y<bitmap.Height;y++)Marshal.Copy(data.Scan0+y*data.Stride,pixels,y*stride,stride);var image=BitmapSource.Create(bitmap.Width,bitmap.Height,96,96,PixelFormats.Bgra32,null,pixels,stride);image.Freeze();return image;}
        finally{bitmap.UnlockBits(data);}
    }
    public static BitmapSource Monitor(string device)
    {
        var screen=Array.Find(Forms.Screen.AllScreens,s=>s.DeviceName==device)??throw new InvalidOperationException("显示器已断开。");var rect=screen.Bounds;using var image=new System.Drawing.Bitmap(rect.Width,rect.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);using var graphics=System.Drawing.Graphics.FromImage(image);graphics.CopyFromScreen(rect.Left,rect.Top,0,0,image.Size,System.Drawing.CopyPixelOperation.SourceCopy|System.Drawing.CopyPixelOperation.CaptureBlt);return Convert(image);
    }
    public static async Task<BitmapSource> Window(nint window)
    {
        if(!NativeMethods.IsWindow(window)||!NativeMethods.GetWindowRect(window,out var rect))throw new InvalidOperationException("目标窗口已关闭。");
        using var capture=new WindowFrameCapture();var bounds=System.Drawing.Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);
        for(int attempt=0;attempt<50;attempt++){using var image=capture.Read(window,bounds,out _);if(image is not null)return Convert(image);await Task.Delay(30);}
        throw new InvalidOperationException("无法捕获此窗口；受保护的内容或权限限制可能禁止截图。");
    }
}

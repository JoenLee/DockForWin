using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.ViewModels;
using DockForWindows.Views;
internal static class DockInteractionChecks
{
    private const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    [DllImport("user32.dll")]static extern bool IsIconic(nint window);
    [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint x,uint y,uint data,nuint extra);
    [DllImport("user32.dll")]static extern int GetWindowRgn(nint window,nint region);
    [DllImport("gdi32.dll")]static extern nint CreateRectRgn(int l,int t,int r,int b);
    [DllImport("gdi32.dll")]static extern bool PtInRegion(nint region,int x,int y);
    [DllImport("gdi32.dll")]static extern bool DeleteObject(nint region);
    [DllImport("dwmapi.dll")]static extern int DwmQueryThumbnailSourceSize(nint thumb,out System.Drawing.Size size);
    private static object? Field(object o,string n)=>o.GetType().GetField(n,F)!.GetValue(o);
    private static object? Call(object o,string n,params object?[] a)=>o.GetType().GetMethod(n,F)!.Invoke(o,a);
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    private static byte[] Pixel(BitmapSource image,int x,int y){var p=new byte[4];image.CopyPixels(new Int32Rect(x,y,1,1),p,4,0);return p;}
    private static Int32Rect AlphaBounds(BitmapSource image){var p=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(p,image.PixelWidth*4,0);int l=image.PixelWidth,t=image.PixelHeight,r=0,b=0;for(int y=0;y<image.PixelHeight;y++)for(int x=0;x<image.PixelWidth;x++)if(p[(y*image.PixelWidth+x)*4+3]>24){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);b=Math.Max(b,y);}return new Int32Rect(l,t,r-l+1,b-t+1);}
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};DockWindow? dock=null;WindowTracker? tracker=null;object? stack=null;Window[] windows=Array.Empty<Window>();using var icons=new IconService();var cursor=System.Windows.Forms.Cursor.Position;
        app.Startup+=async(_,_)=>{try{
            var white=RedesignChecks.Glyph(Brushes.White);var red=RedesignChecks.Glyph(Brushes.Red);
            var bare=(BitmapSource)IconPresentation.Normalize(red,ItemKind.Application,false)!;var gpt=(BitmapSource)IconPresentation.Normalize(red,ItemKind.Application,false,true)!;
            Check(Pixel(bare,40,40)[3]==0&&Pixel(gpt,40,40)[3]>230,"ordinary icon has no generated square; GPT exception keeps its plate");
            var narrow=new DrawingVisual();using(var dc=narrow.RenderOpen())dc.DrawRectangle(Brushes.Red,null,new Rect(20,4,8,80));var narrowImage=new RenderTargetBitmap(100,100,96,96,PixelFormats.Pbgra32);narrowImage.Render(narrow);
            var normalized=(BitmapSource)IconPresentation.Normalize(narrowImage,ItemKind.Document,true)!;var a=AlphaBounds(bare);var b=AlphaBounds(normalized);
            Check(Math.Abs(Math.Max(a.Width,a.Height)-Math.Max(b.Width,b.Height))<=1,"bare icons share one visible longest-edge size without stretching");
            Check(ReferenceEquals(bare,IconPresentation.Normalize(red,ItemKind.Application,true)),"transparent icon normalization reuses one cached result across themes");
            var iconType=typeof(DockSurface).Assembly.GetType("DockForWindows.Views.ControlCenterIcon")!;var map=(System.Collections.Generic.Dictionary<string,string>)iconType.GetField("Icons",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
            Check(map["Hdr"]=="color-filter"&&map["NightLight"]=="eye"&&map["Screenshot"]=="scan"&&map["ScreenRecording"]=="videocam","HDR, eye-care, screenshot and recording use distinct official Filled symbols");
            var battery=(GeometryGroup)iconType.GetMethod("Shape",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{"Battery"})!;Check(battery.Children.Count>=4&&battery.Bounds.Width>.9,"SVG battery outline and terminal strokes are retained");
            foreach(string label in new[]{"×","−","+"}){var type=typeof(DockSurface).Assembly.GetType("DockForWindows.Views.WindowControlGlyph")!;var glyph=(FrameworkElement)Activator.CreateInstance(type,F,null,new object[]{label,Brushes.Black},null)!;glyph.Measure(new Size(10,10));glyph.Arrange(new Rect(0,0,10,10));var image=new RenderTargetBitmap(10,10,96,96,PixelFormats.Pbgra32);image.Render(glyph);var bound=AlphaBounds(image);Check(Math.Abs(bound.X+bound.Width/2d-5)<=.5&&Math.Abs(bound.Y+bound.Height/2d-5)<=.5,"window "+label+" glyph is centered in both axes");}
            Check(LaunchService.CanCreateNewWindow(new PinnedItem{Target="C:/apps/code.exe"})&&LaunchService.CanCreateNewWindow(new PinnedItem{Target="C:/apps/msedge.exe"})&&!LaunchService.CanCreateNewWindow(new PinnedItem{Target="C:/apps/unknown-single.exe"}),"new-window actions appear only for known supported commands");
            var settings=new DockSettings{Theme=DockTheme.Dark,Animations=true,AutoHide=false,HideOnDesktop=false,ShowRecentApps=true};settings.Pins.Clear();
            var pin=new PinnedItem{Name="Preview test app",Target=Environment.ProcessPath!,ExecutablePath=Environment.ProcessPath!};settings.Pins.Add(pin);settings.Pins.Add(new PinnedItem{Name="ChatGPT",Target=Environment.ProcessPath!,ExecutablePath=Environment.ProcessPath!});
            var model=new DockViewModel(settings,new SettingsService(Path.Combine(directory,"data")),icons);model.Items.Add(new DockItem(new PinnedItem{Name="Recent test",Target=Environment.ProcessPath!,ExecutablePath=Environment.ProcessPath!},false));tracker=new WindowTracker(app.Dispatcher);dock=new DockWindow(model,tracker);dock.Show();await Task.Delay(1500);((DispatcherTimer)Field(dock,"_edgeTimer")!).Stop();((DispatcherTimer)Field(dock,"_glassTimer")!).Stop();
            var strip=(Panel)Field(dock,"_strip")!;var separator=typeof(DockSurface).Assembly.GetType("DockForWindows.Views.DockStrip")!.GetMethod("GetIsSeparator",BindingFlags.Public|BindingFlags.Static)!;
            Check(strip.Children.Cast<UIElement>().Count(e=>(bool)separator.Invoke(null,new object[]{e})!)==2,"separators split pinned/recent and recent/recycle sections");
            var tile=strip.Children.Cast<UIElement>().First(e=>e.GetType().Name=="DockTile");var item=(DockItem)Field(tile,"<Item>k__BackingField")!;
            windows=Enumerable.Range(0,3).Select(i=>new Window{ShowInTaskbar=false,WindowStyle=WindowStyle.ToolWindow,Title="Preview source "+(i+1),Width=420,Height=280,Left=50+i*45,Top=140+i*35,Background=i==0?Brushes.OrangeRed:i==1?Brushes.Teal:Brushes.SlateBlue,Content=new TextBlock{Text="Window "+(i+1),FontSize=36,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}}).ToArray();foreach(var window in windows)window.Show();await Task.Delay(300);
            item.Windows=windows.Select(w=>new TrackedWindow(new WindowInteropHelper(w).Handle,(uint)Environment.ProcessId,w.Title,Environment.ProcessPath!)).ToArray();var first=item.Windows[0];WindowTracker.Activate(first);await Task.Delay(120);
            Call(dock,"ActivateItem",item,tile);await Task.Delay(200);Check(IsIconic(first.Handle),"second click minimizes the active application window");
            var tilePoint=((FrameworkElement)tile).PointToScreen(new Point(((FrameworkElement)tile).ActualWidth/2,((FrameworkElement)tile).ActualHeight/2));SetCursorPos((int)tilePoint.X,(int)tilePoint.Y);mouse_event(2,0,0,0,0);mouse_event(4,0,0,0,0);await Task.Delay(300);Check(!IsIconic(first.Handle),"next click restores the minimized application window");
            WindowTracker.Activate(item.Windows[1]);await Task.Delay(100);var shellMessage=(uint)Field(dock,"_shellMessage")!;Call(dock,"ObserveShellMessage",(int)shellMessage,(nint)0x8006,first.Handle);
            var dot=(UIElement)Field(tile,"_dot")!;var brush=(SolidColorBrush)Field(tile,"_indicatorBrush")!;
            Check(item.NeedsAttention&&brush.Color==Color.FromRgb(255,159,10),"actual shell-attention message turns the indicator orange");
            Check(!SystemParameters.ClientAreaAnimation||dot.HasAnimatedProperties,"notification indicator breathes while motion is allowed");
            var clearPoint=windows[0].PointToScreen(new Point(30,60));SetCursorPos((int)clearPoint.X,(int)clearPoint.Y);mouse_event(2,0,0,0,0);mouse_event(4,0,0,0,0);await Task.Delay(160);Call(dock,"UpdateAttentionState");Check(!item.NeedsAttention&&!dot.HasAnimatedProperties,"activating the app clears attention and stops the animation");
            var menu=(ContextMenu)Call(dock,"BuildItemMenu",new DockItem(new PinnedItem{Name="Code",Target="C:/apps/code.exe"}))!;menu.PlacementTarget=(UIElement)tile;menu.Placement=PlacementMode.Top;menu.StaysOpen=true;menu.IsOpen=true;await Task.Delay(150);
            Check(menu.Items.OfType<MenuItem>().Any(m=>(string?)m.Header=="新建窗口"),"supported app context menu offers new window");
            var watcher=Field(dock,"_outsideClicks")!;Check((bool)watcher.GetType().GetProperty("Active",F)!.GetValue(watcher)!,"outside-click hook exists only while a popup is open");
            var outsidePoint=windows[0].PointToScreen(new Point(30,60));SetCursorPos((int)outsidePoint.X,(int)outsidePoint.Y);mouse_event(2,0,0,0,0);mouse_event(4,0,0,0,0);await Task.Delay(250);Check(!menu.IsOpen&&Field(dock,"_outsideClicks") is null,"clicking outside on another window closes menu and releases hook");
            int closed=0;var stackType=typeof(DockSurface).Assembly.GetType("DockForWindows.Views.WindowPreviewStack")!;stack=Activator.CreateInstance(stackType,F,null,new object[]{dock,tile,DockEdge.Bottom,(Action)(()=>closed++)},null)!;Call(stack,"Show",item.Windows);await Task.Delay(300);
            var cards=((IEnumerable)Field(stack,"_cards")!).Cast<Window>().ToArray();Check(cards.Length==3&&cards.All(c=>c.IsVisible),"hover stack displays three separate preview cards");
            Check(cards.All(c=>(nint)Field(c,"_thumbnail")! !=0),"all preview cards have live native DWM thumbnails");
            nint region=CreateRectRgn(0,0,0,0);try{Check(GetWindowRgn(new WindowInteropHelper(cards[0]).Handle,region)>0&&!PtInRegion(region,1,1),"preview cards use actual rounded native clipping");}finally{DeleteObject(region);}
            Call(stack,"Scroll",-120);Check((int)stackType.GetProperty("SelectedIndex",F)!.GetValue(stack)! ==1,"wheel down advances the front preview");Call(stack,"Scroll",120);Check((int)stackType.GetProperty("SelectedIndex",F)!.GetValue(stack)! ==0,"wheel up restores the previous preview");
            Capture(cards[0],Path.Combine(directory,"preview-stack.png"));var handles=cards.Select(c=>(nint)Field(c,"_thumbnail")!).ToArray();((IDisposable)stack).Dispose();Check(closed==1&&cards.All(c=>!c.IsVisible)&&handles.All(h=>DwmQueryThumbnailSourceSize(h,out _) <0),"closing stack releases every DWM thumbnail and preview window");stack=null;
            result=0;
        }catch(Exception e){Console.Error.WriteLine(e);}finally{(stack as IDisposable)?.Dispose();foreach(var w in windows)w.Close();dock?.Close();tracker?.Dispose();SetCursorPos(cursor.X,cursor.Y);app.Shutdown(result);}};app.Run();return result;
    }
    private static void Capture(Window window,string path){var p=window.PointToScreen(new Point());var end=window.PointToScreen(new Point(window.ActualWidth,window.ActualHeight));using var image=new System.Drawing.Bitmap((int)(end.X-p.X),(int)(end.Y-p.Y));using(var g=System.Drawing.Graphics.FromImage(image))g.CopyFromScreen((int)p.X,(int)p.Y,0,0,image.Size);image.Save(path);}
}

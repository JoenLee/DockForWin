using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace DockForWindows.Services;

// A small non-activating taskbar accessory in the reserved gap beside quick settings.
internal sealed class ControlCenterTaskbarEntry:Window,IDisposable
{
    private readonly Button _button=new();private bool _open,_disposed;
    internal ControlCenterTaskbarEntry(Action toggle)
    {
        Title="控制中心任务栏开关";Width=28;Height=28;Left=-32000;Top=-32000;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=true;
        var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(6));border.SetValue(Border.BackgroundProperty,new SolidColorBrush(Color.FromArgb(8,128,128,128)));var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(content);
        _button.Template=new ControlTemplate(typeof(Button)){VisualTree=border};_button.BorderThickness=new Thickness(0);_button.Padding=new Thickness();_button.ToolTip="控制中心";
        System.Windows.Automation.AutomationProperties.SetName(_button,"控制中心开关");_button.Click+=(_,_)=>toggle();_button.MouseEnter+=(_,_)=>_button.Opacity=.75;_button.MouseLeave+=(_,_)=>_button.Opacity=1;
        Content=_button;SetOpen(false);
        SourceInitialized+=(_,_)=>{var hwnd=new WindowInteropHelper(this).Handle;var style=NativeMethods.GetWindowLongPtr(hwnd,-20);SetWindowLongPtr(hwnd,-20,new nint((long)style|0x08000080));HwndSource.FromHwnd(hwnd)?.AddHook((nint h,int m,nint w,nint l,ref bool handled)=>{if(m==0x21){handled=true;return 3;}return 0;});};
        ControlCenterEntryService.NetworkAnchorChanged+=Place;SystemEvents.UserPreferenceChanged+=Preferences;
        Loaded+=(_,_)=>Place(ControlCenterEntryService.NetworkAnchor);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]private static extern nint SetWindowLongPtr(nint hwnd,int index,nint value);
    private void Preferences(object sender,UserPreferenceChangedEventArgs e)=>Dispatcher.BeginInvoke(()=>{if(!_disposed)SetOpen(_open);});
    internal void SetOpen(bool open){_open=open;_button.Content=new Image{Source=CreateSymbol(open),Width=16,Height=16,IsHitTestVisible=false};_button.ToolTip=open?"收起控制中心":"打开控制中心";}
    internal static DrawingImage CreateSymbol(bool open)
    {
        bool light=false;try{using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");light=key?.GetValue("SystemUsesLightTheme") is int v&&v!=0;}catch{}
        var ink=light?Brushes.Black:Brushes.White;var group=new DrawingGroup();using(var dc=group.Open()){
            dc.DrawRoundedRectangle(null,new Pen(ink,2),new Rect(3,3,26,10),5,5);dc.DrawEllipse(ink,null,new Point(8,8),3,3);
            dc.DrawRoundedRectangle(ink,null,new Rect(3,19,26,10),5,5);dc.DrawEllipse(open?new SolidColorBrush(Color.FromRgb(10,132,255)):new SolidColorBrush(Color.FromRgb(135,145,155)),null,new Point(23,24),3,3);
        }group.Freeze();var image=new DrawingImage(group);image.Freeze();return image;
    }
    private void Place(Rect anchor)
    {
        if(_disposed)return;if(anchor.IsEmpty){Visibility=Visibility.Hidden;ControlCenterEntryService.AccessoryBounds=Rect.Empty;return;}Visibility=Visibility.Visible;var hwnd=new WindowInteropHelper(this).Handle;if(hwnd==0)return;
        NativeMethods.SetWindowPos(hwnd,new nint(-1),(int)anchor.Left-70,(int)anchor.Top+2,0,0,0x0015);
        double dpi=NativeMethods.GetDpiForWindow(hwnd)/96d;int width=(int)Math.Round(28*dpi),height=(int)Math.Min(anchor.Height-4,28*dpi),x=(int)Math.Round(anchor.Left-width-6*dpi),y=(int)Math.Round(anchor.Top+(anchor.Height-height)/2);
        NativeMethods.SetWindowPos(hwnd,new nint(-1),x,y,width,height,0x0010);ControlCenterEntryService.AccessoryBounds=new Rect(x,y,width,height);
    }
    public void Dispose(){if(_disposed)return;_disposed=true;ControlCenterEntryService.NetworkAnchorChanged-=Place;SystemEvents.UserPreferenceChanged-=Preferences;ControlCenterEntryService.AccessoryBounds=Rect.Empty;Close();}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using DockForWindows.Models;
using Forms=System.Windows.Forms;

namespace DockForWindows.Services;

internal static class WidgetDesktopLayout
{
    internal const double DesktopScale=.85,EdgeInset=24;
    internal static double ContentScale(WidgetInstance instance)=>DesktopScale*instance.Appearance.PanelScale;
    internal static double Pitch(WidgetInstance instance,double dpi)=>(WidgetSize.Unit+WidgetSize.Gap)*ContentScale(instance)*dpi/96;
    internal static Rect Bounds(WidgetInstance instance,Forms.Screen screen,double dpi)
    {
        double scale=dpi/96,pitch=Pitch(instance,dpi);return new Rect(screen.WorkingArea.Left+EdgeInset*scale+instance.Column*pitch,
            screen.WorkingArea.Top+EdgeInset*scale+instance.Row*pitch,instance.Size.Width*ContentScale(instance)*scale,instance.Size.Height*ContentScale(instance)*scale);
    }
    internal static bool FindSpace(WidgetInstance item,Forms.Screen screen,double dpi,IReadOnlyList<Rect> occupied,out int column,out int row,int? desiredColumn=null,int? desiredRow=null)
    {
        double scale=dpi/96,pitch=Pitch(item,dpi),width=item.Size.Width*ContentScale(item)*scale,height=item.Size.Height*ContentScale(item)*scale;int maxX=(int)Math.Floor((screen.WorkingArea.Width-2*EdgeInset*scale-width)/pitch),maxY=(int)Math.Floor((screen.WorkingArea.Height-2*EdgeInset*scale-height)/pitch);
        var cells=from y in Enumerable.Range(0,Math.Max(0,maxY+1)) from x in Enumerable.Range(0,Math.Max(0,maxX+1)) orderby desiredColumn.HasValue?(x-desiredColumn.Value)*(x-desiredColumn.Value)+(y-desiredRow!.Value)*(y-desiredRow.Value):y*1000+x select (x,y);
        // One scaled gutter belongs to every span, so adjacent squares and a wider card share their outer edges.
        double clearance=WidgetSize.Gap*ContentScale(item)*scale-.001;
        foreach(var cell in cells){var candidate=new Rect(screen.WorkingArea.Left+EdgeInset*scale+cell.x*pitch,screen.WorkingArea.Top+EdgeInset*scale+cell.y*pitch,width,height);candidate.Inflate(clearance,clearance);
            if(occupied.All(r=>!r.IntersectsWith(candidate))){column=cell.x;row=cell.y;return true;}}
        column=row=0;return false;
    }
    private static Task<IReadOnlyList<Rect>>? _iconRead;
    internal static async Task<IReadOnlyList<Rect>> Icons()
    {
        // UIA reads desktop item bounds without opening Explorer's process memory.
        if(_iconRead==null||_iconRead.IsCompleted)_iconRead=Task.Run<IReadOnlyList<Rect>>(()=>{
            var result=new List<Rect>();nint list=0;
            EnumWindows((h,_)=>{nint view=FindWindowEx(h,0,"SHELLDLL_DefView",null);if(view!=0){list=FindWindowEx(view,0,"SysListView32",null);return false;}return true;},0);
            if(list==0)throw new InvalidOperationException(LocalizedText.Get("无法读取桌面图标区域，请确认使用 Windows Explorer 桌面。"));
            var root=AutomationElement.FromHandle(list);foreach(AutomationElement item in root.FindAll(TreeScope.Children,System.Windows.Automation.Condition.TrueCondition)){
                var bounds=item.Current.BoundingRectangle;if(!bounds.IsEmpty&&bounds.Width>0&&bounds.Height>0)result.Add(bounds);}
            return result;
        });
        return await _iconRead.WaitAsync(TimeSpan.FromSeconds(3));
    }
    internal static double Dpi(Forms.Screen screen)
    {
        var point=new PointI{X=screen.Bounds.Left+1,Y=screen.Bounds.Top+1};var monitor=MonitorFromPoint(point,2);
        return GetDpiForMonitor(monitor,0,out uint x,out _)==0?x:96;
    }
    internal static nint DesktopParent()
    {
        var progman=FindWindow("Progman",null);if(progman==0)return 0;
        nint parent=0;EnumWindows((h,_)=>{if(FindWindowEx(h,0,"SHELLDLL_DefView",null)!=0){parent=h;return false;}return true;},0);
        // Use the top-level Explorer desktop as the owner of per-pixel transparent widget windows.
        return parent!=0?parent:progman;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct PointI{public int X,Y;}
    private delegate bool EnumProc(nint h,nint p);
    [DllImport("user32.dll")]private static extern bool EnumWindows(EnumProc callback,nint p);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern nint FindWindow(string name,string? title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern nint FindWindowEx(nint parent,nint after,string name,string? title);
    [DllImport("user32.dll")]private static extern nint MonitorFromPoint(PointI point,uint flags);
    [DllImport("shcore.dll")]private static extern int GetDpiForMonitor(nint monitor,int type,out uint x,out uint y);
}

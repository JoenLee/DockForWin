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
    internal static Rect Bounds(WidgetInstance instance,Forms.Screen screen,double dpi)
    {
        double scale=dpi/96;return new Rect(screen.WorkingArea.Left+instance.Column*(WidgetSize.Unit+WidgetSize.Gap)*scale,
            screen.WorkingArea.Top+instance.Row*(WidgetSize.Unit+WidgetSize.Gap)*scale,instance.Size.Width*instance.Appearance.PanelScale*scale,instance.Size.Height*instance.Appearance.PanelScale*scale);
    }
    internal static bool FindSpace(WidgetInstance item,Forms.Screen screen,double dpi,IReadOnlyList<Rect> occupied,out int column,out int row,int? desiredColumn=null,int? desiredRow=null)
    {
        double scale=dpi/96,pitch=(WidgetSize.Unit+WidgetSize.Gap)*scale,width=item.Size.Width*item.Appearance.PanelScale*scale,height=item.Size.Height*item.Appearance.PanelScale*scale;int maxX=(int)Math.Floor((screen.WorkingArea.Width-width)/pitch),maxY=(int)Math.Floor((screen.WorkingArea.Height-height)/pitch);
        var cells=from y in Enumerable.Range(0,Math.Max(0,maxY+1)) from x in Enumerable.Range(0,Math.Max(0,maxX+1)) orderby desiredColumn.HasValue?(x-desiredColumn.Value)*(x-desiredColumn.Value)+(y-desiredRow!.Value)*(y-desiredRow.Value):y*1000+x select (x,y);
        foreach(var cell in cells){var candidate=new Rect(screen.WorkingArea.Left+cell.x*pitch,screen.WorkingArea.Top+cell.y*pitch,width,height);candidate.Inflate(8*scale,8*scale);
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
            if(list==0)throw new InvalidOperationException("无法读取桌面图标区域，请确认使用 Windows Explorer 桌面。");
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
        // Parent to the same desktop view container: widgets are children, behind ordinary app windows.
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

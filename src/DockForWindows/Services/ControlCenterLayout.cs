using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DockForWindows.Models;

namespace DockForWindows.Services;

public sealed record ControlCardPlacement(ControlModule Module,Rect Bounds,int Column,int Row,int Columns,int Rows,string Group="");
public static class ControlCenterLayout
{
    public static (int Columns,int Rows) Cells(ControlCardShape shape)=>shape switch {ControlCardShape.Circle=>(1,1),ControlCardShape.Capsule=>(2,1),ControlCardShape.Square=>(2,2),ControlCardShape.Large=>(4,7),_=>(4,1)};
    public static ControlCardShape[] Shapes(ControlModule? module)=>module==ControlModule.Media?new[]{ControlCardShape.Strip,ControlCardShape.Square,ControlCardShape.Rectangle,ControlCardShape.Large}:new[]{ControlCardShape.Circle,ControlCardShape.Capsule,ControlCardShape.Square,ControlCardShape.Rectangle};
    public static double FitPanel(double requested,Size content,Size available) =>
        Math.Min(requested,Math.Min(Math.Max(1,available.Width)/Math.Max(1,content.Width),Math.Max(1,available.Height)/Math.Max(1,content.Height)));
    public static double Unit(ControlCenterGeometry geometry)=>(geometry.PanelWidth-36-3*geometry.Gap)/4*geometry.ComponentScale;
    public static Size Size(ControlCardShape shape,ControlCenterGeometry geometry,ControlModule? module=null)
    {var (columns,rows)=Cells(shape);if(module==ControlModule.Media&&shape==ControlCardShape.Rectangle)rows=2;double unit=Unit(geometry),gap=geometry.Gap*geometry.ComponentScale;return new(columns*unit+(columns-1)*gap,rows*unit+(rows-1)*gap);}
    public static IReadOnlyList<ControlCardPlacement> Arrange(ControlCenterOptions options)
    {
        var result=new List<ControlCardPlacement>();double z=options.Geometry.ComponentScale,step=Unit(options.Geometry)+options.Geometry.Gap*z,offset=0;
        foreach(var group in options.Modules.Where(m=>m.Visible&&m.PageId==options.ActivePageId).GroupBy(m=>m.Group)){
            if(group.Key.Length>0)offset+=32*z;var occupied=new HashSet<(int,int)>();double end=offset;
            foreach(var module in group){
                var (columns,rows)=Cells(module.EffectiveShape);if(module.Module==ControlModule.Media&&module.EffectiveShape==ControlCardShape.Rectangle)rows=2;int row=0,column=0;
                for(;;){bool found=false;for(int x=0;x<=4-columns;x++){bool free=true;for(int y=row;y<row+rows;y++)for(int k=x;k<x+columns;k++)if(occupied.Contains((k,y)))free=false;if(free){column=x;found=true;break;}}if(found)break;row++;}
                for(int y=row;y<row+rows;y++)for(int x=column;x<column+columns;x++)occupied.Add((x,y));var bounds=new Rect(new Point(column*step,offset+row*step),Size(module.EffectiveShape,options.Geometry,module.Module));end=Math.Max(end,bounds.Bottom);
                result.Add(new(module.Module,bounds,column,row,columns,rows,group.Key));
            }
            offset=end+options.Geometry.Gap*z;
        }
        return result;
    }
    public static ControlCardShape Snap(Size requested,ControlCenterGeometry geometry,ControlCardShape current,ControlModule? module=null)
    {
        double Distance(ControlCardShape shape){var size=Size(shape,geometry,module);return Math.Pow(requested.Width-size.Width,2)+Math.Pow(requested.Height-size.Height,2);}
        var best=Shapes(module).MinBy(Distance);
        return best!=current&&Distance(current)-Distance(best)>Math.Pow(Unit(geometry)*.12,2)?best:current;
    }
    public static void MoveBefore(ControlCenterOptions options,ControlModule moving,ControlModule target)
    {
        int from=options.Modules.FindIndex(m=>m.Module==moving),to=options.Modules.FindIndex(m=>m.Module==target);if(from<0||to<0||from==to)return;
        var module=options.Modules[from];module.Group=options.Modules[to].Group;module.PageId=options.Modules[to].PageId;options.Modules.RemoveAt(from);options.Modules.Insert(to,module);
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DockForWindows.Views;

internal static class ReorderAnimationChecks
{
    public static int Run()
    {
        var app=new Application();var window=new Window{Title="Dock reorder verification",Width=620,Height=260,Left=20,Top=80,Background=Brushes.DimGray,ShowInTaskbar=false,Topmost=true};int exit=0;
        window.Loaded+=async(_,_)=>
        {
            try
            {
                foreach(int edge in new[]{0,1,2})
                {
                    var strip=new DockStrip{IconSize=40,Gap=6,Vertical=edge>0,RightEdge=edge==2,Magnification=1.4,HoverLift=9};var icons=Enumerable.Range(0,4).Select(i=>new Border{Background=new SolidColorBrush(Color.FromRgb((byte)(60+i*35),120,180)),Width=44,Height=44,CornerRadius=new CornerRadius(8)}).ToArray();foreach(var icon in icons)strip.Children.Add(icon);
                    window.Width=edge==0?620:250;window.Height=edge==0?260:620;window.Content=strip;await Task.Delay(200);
                    Point Center(Border icon)=>icon.TransformToAncestor(strip).Transform(new Point(22,22));double Along(Border icon)=>edge==0?Center(icon).X:Center(icon).Y;
                    var before=icons.Select(Center).ToArray();strip.BeginReorder(icons[0]);strip.PreviewSwap(icons[2],true);await Task.Delay(110);
                    var mid=icons.Select(Center).ToArray();if(!(Along(icons[0])>(edge==0?before[0].X:before[0].Y)+3))throw new Exception("拖动图标没有平滑向新位置移动");
                    double cross0=edge==0?mid[0].Y:mid[0].X,cross1=edge==0?mid[1].Y:mid[1].X;if(Math.Abs(cross0-cross1)<3)throw new Exception("换位没有避让轨迹");
                    bool settled=false;strip.EndReorder(true,()=>settled=true);await Task.Delay(750);if(!settled||!(Along(icons[1])<Along(icons[2])&&Along(icons[2])<Along(icons[0])&&Along(icons[0])<Along(icons[3])))throw new Exception("插入排序没有自动让位或未完成回调");
                    var committed=icons.Select(Along).ToArray();strip.BeginReorder(icons[0]);strip.PreviewSwap(icons[3],true);strip.EndReorder(false,()=>{});await Task.Delay(800);if(icons.Where((icon,i)=>Math.Abs(Along(icon)-committed[i])>.2).Any())throw new Exception("取消拖动未恢复排列");Console.WriteLine($"PASS edge={edge} 插入、让位、避让、取消恢复");
                }
                Console.WriteLine("REORDER ANIMATION RESULT PASS");
            }
            catch(Exception ex){exit=1;Console.WriteLine("REORDER ANIMATION RESULT FAIL "+ex);}
            finally{window.Close();}
        };app.Run(window);return exit;
    }
}

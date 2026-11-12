using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class ClassicRefinementChecks
{
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.GetValue(value)!;
    private static object? Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(value,args);
    private static IEnumerable<T> Tree<T>(DependencyObject value)where T:DependencyObject{if(value is T item)yield return item;for(int i=0;i<VisualTreeHelper.GetChildrenCount(value);i++)foreach(var child in Tree<T>(VisualTreeHelper.GetChild(value,i)))yield return child;}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);int result=1;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async(_,_)=>{
            ControlCenterWindow? center=null;var area=SystemParameters.WorkArea;
            var drawing=new DrawingGroup();using(var dc=drawing.Open()){dc.DrawRectangle(Brushes.DarkSlateGray,null,new Rect(0,0,64,64));dc.DrawRectangle(Brushes.SlateGray,null,new Rect(0,0,32,32));dc.DrawRectangle(Brushes.SlateGray,null,new Rect(32,32,32,32));}drawing.Freeze();
            var checker=new DrawingBrush(drawing){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,64,64)};
            var fixture=new Window{Title="Classic refinement isolated source",WindowStyle=WindowStyle.None,ShowInTaskbar=false,Left=area.Left,Top=area.Top,Width=area.Width,Height=area.Height,Background=checker};
            try{
                fixture.Show();var settings=new DockSettings{Theme=DockTheme.Glass,Animations=true};settings.ControlCenter.MaterialMode=ControlCenterMaterialMode.ClassicBlur;
                foreach(var kind in new[]{ControlModule.Wifi,ControlModule.Bluetooth}){var module=settings.ControlCenter.Modules.Single(m=>m.Module==kind);module.Visible=true;module.SetShape(ControlCardShape.Capsule);}
                var fake=(IControlCenterConnectivity)Activator.CreateInstance(typeof(ControlMotionChecks).GetNestedType("Connectivity",BindingFlags.NonPublic)!,true)!;
                center=new ControlCenterWindow(settings,connectivity:fake){DismissOnDeactivate=false};center.Show();await Task.Delay(1500);
                Field<System.Windows.Threading.DispatcherTimer>(center,"_poll").Stop();Field<System.Windows.Threading.DispatcherTimer>(center,"_stateRefresh").Stop();Field<System.Windows.Threading.DispatcherTimer>(center,"_mediaPoll").Stop();
                var background=Field<FrameworkElement>(center,"_classicBackdrop");Check(background.OpacityMask is DrawingBrush,"edge feather uses a vector opacity mask");
                Check(background.Clip is RectangleGeometry limit&&limit.Rect.Size==background.RenderSize&&((DrawingBrush)background.OpacityMask).Viewport==new Rect(background.RenderSize),"feather is anchored to plate bounds and cannot blur outside it");
                var maskVisual=new DrawingVisual();using(var dc=maskVisual.RenderOpen())dc.DrawRectangle(background.OpacityMask,null,new Rect(background.RenderSize));
                var mask=new RenderTargetBitmap((int)Math.Ceiling(background.ActualWidth),(int)Math.Ceiling(background.ActualHeight),96,96,PixelFormats.Pbgra32);mask.Render(maskVisual);
                byte Alpha(int x,int y){var bytes=new byte[4];mask.CopyPixels(new Int32Rect(x,y,1,1),bytes,4,0);return bytes[3];}
                int mid=mask.PixelWidth/2;Check(Alpha(mid,0)<30&&Alpha(mid,24)>Alpha(mid,0)&&Alpha(mid,24)<Alpha(mid,48)&&Alpha(mid,80)>250,"blur alpha fades continuously from clear desktop to full blur");
                Check(Tree<FrameworkElement>(center).Count(e=>e.IsVisible&&e.Effect is BlurEffect)==2,"feather and card-only material use two shared Gaussian layers");
                Check(VisualTreeHelper.GetChildrenCount(background)==1&&VisualTreeHelper.GetChild(background,0) is Image&&ReferenceEquals(VisualTreeHelper.GetParent(background),center.Content),"background is only an image blur across the window, with no tinted plate");
                var surfaces=Field<List<DockSurface>>(center,"_surfaces");var cardColor=((SolidColorBrush)surfaces[0].Fill!).Color;Check(cardColor.A>100&&cardColor.R<35,"dark translucent cards remain independent of the background blur");
                foreach(double radius in new[]{0d,35d,99d}){
                    settings.ControlCenter.Geometry.CapsuleRoundness=radius;Call(center,"ApplyPreview",settings);await Task.Delay(220);
                    var holders=Field<IEnumerable>(center,"_modules").Cast<object>().Select(c=>Field<Border?>(c,"Holder")).Where(h=>h is not null).Cast<Border>().ToArray();
                    Check(holders.Length>=2&&holders.All(h=>h.Clip is EllipseGeometry e&&Math.Abs(h.ActualWidth-h.ActualHeight)<.01&&Math.Abs(e.Bounds.Width-e.Bounds.Height)<.01&&e.FillContains(new Point(e.Center.X,e.Center.Y-e.RadiusY+.2))&&!e.FillContains(new Point(.2,.2))),"inner buttons remain complete circles at outer roundness "+radius);
                }
                Check(Field<IEnumerable>(center,"_modules").Cast<object>().Select(c=>Field<TextBlock?>(c,"Title")).Where(t=>t is not null).All(t=>t!.Foreground is SolidColorBrush brush&&brush.Color==Colors.White),"rebuilding card corners keeps text readable on the dark material");
                var iconType=typeof(DockSurface).Assembly.GetType("DockForWindows.Views.ControlCenterIcon")!;var names=(IReadOnlyList<string>)iconType.GetProperty("AvailableGlyphs",BindingFlags.Static|BindingFlags.Public)!.GetValue(null)!;
                var shapeMethod=iconType.GetMethod("Shape",BindingFlags.Static|BindingFlags.NonPublic)!;var load=Stopwatch.StartNew();
                foreach(string name in names){var geometry=(Geometry)shapeMethod.Invoke(null,new object[]{name})!;Check(geometry.IsFrozen&&Math.Abs(Math.Max(geometry.Bounds.Width,geometry.Bounds.Height)-1)<.00001,"official SVG normalized/frozen: "+name);}
                Console.WriteLine("cached all official glyphs in "+load.Elapsed.TotalMilliseconds.ToString("F2")+"ms");
                var cards=Field<IEnumerable>(center,"_modules").Cast<object>().ToArray();var volume=cards.Single(c=>Field<ControlModule>(c,"Kind")==ControlModule.Volume);var button=Field<Button>(volume,"Primary");var icon=(FrameworkElement)button.Content;
                Check(cards.Select(c=>Field<Button>(c,"Primary").Content).OfType<FrameworkElement>().All(i=>Math.Abs(i.Width-20)<.01&&Math.Abs(i.Height-20)<.01),"primary icons share the same viewport size");
                Call(icon,"ChangeGlyph","Muted",true);await Task.Delay(55);Call(icon,"ChangeGlyph","Volume",true);await Task.Delay(55);Call(icon,"ChangeGlyph","Muted",true);await Task.Delay(250);
                Check((string)iconType.GetProperty("Glyph")!.GetValue(icon)! == "Muted"&&Field<Geometry?>(icon,"_outgoing") is null,"rapid state crossfade ends on actual target without retained old glyph");
                Call(icon,"Feedback",false);await Task.Delay(70);Check(Field<ScaleTransform>(icon,"_feedbackScale").ScaleX<1,"click gives icon a visible press bounce");await Task.Delay(380);Check(Math.Abs(Field<ScaleTransform>(icon,"_feedbackScale").ScaleX-1)<.001,"press animation settles to the original size");
                Call(icon,"ChangeGlyph","Muted",false);Call(icon,"ChangeGlyph","Bell",false);Call(icon,"Feedback",false);bool swung=false;for(int frame=0;frame<24;frame++){await Task.Delay(25);if(Math.Abs(Field<RotateTransform>(icon,"_feedbackRotation").Angle)>.1){swung=true;break;}}Check(swung,"sound/bell style feedback swings lightly");await Task.Delay(850);Check(Math.Abs(Field<RotateTransform>(icon,"_feedbackRotation").Angle)<.001,"swing returns to neutral");
                settings.ControlCenter.Geometry.CapsuleRoundness=35;Call(center,"ApplyPreview",settings);await Task.Delay(350);
                var point=center.PointToScreen(new Point());var far=center.PointToScreen(new Point(center.ActualWidth,center.ActualHeight));using(var shot=new System.Drawing.Bitmap((int)(far.X-point.X),(int)(far.Y-point.Y))){using(var graphics=System.Drawing.Graphics.FromImage(shot))graphics.CopyFromScreen((int)point.X,(int)point.Y,0,0,shot.Size);shot.Save(Path.Combine(directory,"classic-refined.png"));}
                Call(center,"OpenModule",ControlModule.Wifi,false);await Task.Delay(450);Call(center,"HideDetails");await Task.Delay(450);Check(Tree<FrameworkElement>(center).Count(e=>e.IsVisible&&e.Effect is BlurEffect)==2,"navigation preserves shared blur and icon feedback");
                settings.ControlCenter.MaterialMode=ControlCenterMaterialMode.LiquidGlass;Call(center,"ApplyPreview",settings);await Task.Delay(350);Check(background.Visibility==Visibility.Collapsed&&Tree<FrameworkElement>(center).Any(e=>e.IsVisible&&e.Effect is LiquidGlassEffect),"original glass theme still restores");
                Console.WriteLine("PASS refinement checks; no hardware writes or actual-FPS claim");result=0;
            }catch(Exception error){Console.Error.WriteLine(error);}
            finally{center?.Dismiss();await Task.Delay(650);fixture.Close();app.Shutdown(result);}
        };app.Run();return result;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;
using MediaState=DockForWindows.Services.MediaState;

internal static class WidgetStyleChecks
{
    static int Passed,Skipped;static string Output="";
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Passed++;Console.WriteLine("PASS "+text);}
    static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    static IEnumerable<T> Tree<T>(DependencyObject root)where T:DependencyObject{if(root is T item)yield return item;for(int n=0;n<VisualTreeHelper.GetChildrenCount(root);n++)foreach(var child in Tree<T>(VisualTreeHelper.GetChild(root,n)))yield return child;}
    internal static int Run(string output)
    {
        NativeMethods.SetProcessDpiAwarenessContext(-4);Output=output;Directory.CreateDirectory(output);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async(_,_)=>{try{Models();await Render();await FrostProof();File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed,Failed=0,Skipped}));app.Shutdown();}catch(Exception error){Console.WriteLine(error);File.WriteAllText(Path.Combine(output,"failure.txt"),error.ToString());app.Shutdown(1);}};return app.Run();
    }
    internal static int LiveUsage(string output)
    {
        Directory.CreateDirectory(output);var snapshot=WidgetAiUsageService.Read("",CancellationToken.None);
        var result=new{CodexUpdatedAt=snapshot.Codex.UpdatedAt,PrimaryAvailable=snapshot.Codex.FiveHour.RemainingPercent!=null,WeeklyAvailable=snapshot.Codex.Week.RemainingPercent!=null,DailyRecords=snapshot.Codex.Daily.Count,ClaudeAvailable=snapshot.Claude.UpdatedAt!=null,Error=snapshot.Error};
        File.WriteAllText(Path.Combine(output,"live-usage.json"),JsonSerializer.Serialize(result));Console.WriteLine(JsonSerializer.Serialize(result));return snapshot.Codex.UpdatedAt!=null?0:1;
    }
    static void Models()
    {
        var color=new WidgetCardColors{GlassFrost=double.NaN,GlassTint=2};color.Normalize();Check(color.GlassFrost==null&&color.GlassTint==.25,"invalid frost values normalize and tint is bounded");
        color.GlassFrost=0;color.GlassTint=0;color.Normalize();Check(!color.IsDefault&&color.Copy().GlassFrost==0,"zero frost remains an explicit clear-glass setting");
        var now=DateTimeOffset.Now;
        string Entry(long count,int used)=>JsonSerializer.Serialize(new{timestamp=now,payload=new{type="token_count",info=new{total_token_usage=new{total_tokens=count}},rate_limits=new{limit_id="codex",primary=new{used_percent=used,window_minutes=300,resets_at=now.AddHours(5).ToUnixTimeSeconds()},secondary=new{used_percent=63,window_minutes=10080,resets_at=now.AddDays(7).ToUnixTimeSeconds()}}}});
        var lines=new[]{"broken",Entry(100,20),Entry(140,21),Entry(200,22),Entry(200,22)};var account=WidgetAiUsageService.ParseCodex(lines,now);
        Check(account.FiveHour.RemainingPercent==78&&account.Week.RemainingPercent==37,"Codex quota uses real rate-limit fields and labels remaining percentages");Check(account.Daily.Sum(d=>d.Tokens)==200,"duplicate token updates are not double-counted");
        Check(WidgetAiUsageService.ParseCodex(new[]{Entry(140,21),Entry(200,22)},now,true).Daily.Sum(x=>x.Tokens)==60,"a truncated log never assigns earlier unseen tokens to the current day");
        Check(WidgetAiUsageService.ParseCodex(lines,now.AddDays(1)).FiveHour.RemainingPercent==null,"expired quota waits for an updated record instead of inventing a reset");
        using var malformed=JsonDocument.Parse("{\"claude\":{\"fiveHour\":{\"remainingPercent\":120},\"week\":{\"remainingPercent\":null}}}");var unknown=WidgetAiUsageService.ParseFile(malformed.RootElement,now);Check(unknown.Claude.FiveHour.RemainingPercent==null&&unknown.Claude.Week.RemainingPercent==null,"missing and out-of-range imported quota stays unknown");
        string file=Path.Combine(Output,"usage.json");File.WriteAllText(file,JsonSerializer.Serialize(new{codex=new{updatedAt=now,fiveHour=new{remainingPercent=78,resetsAt=now.AddHours(5)},week=new{remainingPercent=37,resetsAt=now.AddDays(7)},dailyTokens=Enumerable.Range(0,7).Select(i=>new{date=DateTime.Today.AddDays(-6+i).ToString("yyyy-MM-dd"),tokens=1000+i*350})},claude=new{updatedAt=now,fiveHour=new{remainingPercent=30,resetsAt=now.AddHours(3)},week=new{remainingPercent=3,resetsAt=now.AddDays(5)}}}));
        Check(WidgetAiUsageService.Read(file,CancellationToken.None).Claude.FiveHour.RemainingPercent==30,"Claude can read a configured usage file without credentials");
        var store=new SettingsService(Path.Combine(Output,"profile"));var settings=new DockSettings();settings.Widgets.CardColors["ai-codex"]=new(){GlassFrost=.8,GlassTint=.11};settings.Widgets.Sources["countdown-dot"]=new(){EventName="验收日期",TargetDate=DateTime.Today.AddDays(99),UsageFile=file};store.Save(settings);var loaded=store.Load();Check(loaded.Widgets.CardColors["ai-codex"].GlassFrost==.8&&loaded.Widgets.Sources["countdown-dot"].TargetDate==DateTime.Today.AddDays(99),"frost and countdown source round-trip through SettingsService");
    }
    static WidgetRenderData Data()
    {
        var image=new DrawingVisual();using(var dc=image.RenderOpen()){dc.DrawRectangle(new LinearGradientBrush(Colors.Coral,Colors.MidnightBlue,45),null,new Rect(0,0,150,150));dc.DrawEllipse(Brushes.Gold,null,new Point(100,48),27,27);}
        var bitmap=new RenderTargetBitmap(150,150,96,96,PixelFormats.Pbgra32);bitmap.Render(image);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();encoder.Save(stream);
        var weather=new WidgetWeatherSnapshot(24,2,true,DateTime.Now,DateTimeOffset.Now,Enumerable.Range(0,5).Select(i=>new WidgetForecastDay(DateOnly.FromDateTime(DateTime.Today.AddDays(i)),2,30+i,20+i)).ToArray());
        var data=new WidgetRenderData(new(),new(){City="布局验收",EventName="下一次旅行",TargetDate=DateTime.Today.AddDays(286)},weather,new MediaState("A long song title · 晚风与星光","Layout fixture · Artist",true,true,true,true,stream.ToArray(),TimeSpan.FromSeconds(88),TimeSpan.FromSeconds(275),true),Preview:true,MediaCommand:_=>{}){Now=DateTime.Now,Power=new(70,false),Storage=new("C:",512L*1073741824,256L*1073741824),AiUsage=WidgetAiUsageService.Read(Path.Combine(Output,"usage.json"),CancellationToken.None),Seek=_=>{}};return data;
    }
    static async Task Render()
    {
        var root=new Grid{Background=WidgetBrushes.Get("#343C44")};var window=new Window{Title="小组件款式检查 · 离线数据",Width=1040,Height=800,ShowActivated=false,ShowInTaskbar=false,Content=root};window.Show();var data=Data();var tiles=new List<(string Name,BitmapSource Image)>();
        try{
            var added=WidgetRegistry.Definitions.Where(d=>d.Layout>=WidgetLayout.ClockDot).ToArray();Check(added.Length==24,"24 new templates are registered without replacing existing templates");
            foreach(var definition in added)foreach(var material in new[]{WidgetMaterial.Light,WidgetMaterial.Dark})foreach(var size in WidgetSizes.For(definition)){
                var d=definition with{Size=size};var host=new WidgetHost(d,2.5){HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};host.SetMaterial(material);host.SetContent(WidgetContent.Render(d,data));root.Children.Clear();root.Children.Add(host);await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);window.UpdateLayout();
                Check(host.IsMeasureValid&&Math.Abs(host.ActualWidth-size.Width)<1&&Math.Abs(host.ActualHeight-size.Height)<1,"layout "+d.Id+" "+size+" "+material);
                var buttons=Tree<Button>(host).Where(b=>AutomationProperties.GetAutomationId(b).StartsWith("widget-music-")).ToArray();if(d.Kind==WidgetKind.Music){Check(buttons.Length==3&&buttons.All(b=>b.IsEnabled&&b.ActualWidth>=32&&b.ActualHeight>=32),"usable transport "+d.Id+" "+size);Check(buttons.All(b=>new Rect(host.RenderSize).Contains(b.TransformToAncestor(host).TransformBounds(new Rect(b.RenderSize)))),"transport stays inside "+d.Id+" "+size);}
                var shot=Capture(host,Path.Combine(Output,d.Id+"-"+size.Columns+"x"+size.Rows+"-"+material+".png"));if(size==definition.Size&&material==WidgetMaterial.Dark)tiles.Add((definition.Title,shot));
            }
            ContactSheet(tiles);
            TimeSpan sought=TimeSpan.Zero;var player=new WidgetHost(RetiredWidgetFixtures.Get("music-retro"),2.5);player.SetContent(WidgetContent.Render(player.Definition,data with{Seek=v=>sought=v}));root.Children.Clear();root.Children.Add(player);window.UpdateLayout();var seek=Tree<Slider>(player).Single();seek.Value=120;seek.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});Check(sought==TimeSpan.FromSeconds(120),"retro timeline sends the requested position through the media seek callback");
            var unavailable=new WidgetHost(player.Definition,2.5);unavailable.SetContent(WidgetContent.Render(player.Definition,data with{Media=null,Seek=null,MediaCommand=null}));root.Children.Clear();root.Children.Add(unavailable);window.UpdateLayout();Check(Tree<Button>(unavailable).All(b=>!b.IsEnabled)&&Tree<Slider>(unavailable).All(s=>!s.IsEnabled),"missing media disables transport and timeline without fabricated progress");
            var options=new WidgetOptions{Material=WidgetMaterial.Glass};foreach(var d in WidgetRegistry.Definitions)options.Sources[d.Id]=new(){UsageFile=d.Kind==WidgetKind.AiUsage?Path.Combine(Output,"usage.json"):""};var shell=new SettingsShellWindow(new DockSettings{Widgets=options},(_,_)=>{},9);window.Resources=shell.Resources;var page=new WidgetGalleryPage(options);root.Children.Clear();root.Children.Add(page);await Task.Delay(150);page.ShowCategory(WidgetKind.AiUsage);await Task.Delay(200);window.UpdateLayout();
            Check(Tree<WidgetPreviewFrame>(page).Count()==Enum.GetValues<WidgetKind>().Length,"all eight categories have the existing preview and inspector structure");
            var frost=Tree<Slider>(page).Single(x=>AutomationProperties.GetAutomationId(x)=="widget-glass-frost-aiusage");var tint=Tree<Slider>(page).Single(x=>AutomationProperties.GetAutomationId(x)=="widget-glass-tint-aiusage");frost.Value=80;tint.Value=11;
            Check(options.CardColors["ai-codex"].GlassFrost==.8&&options.CardColors["ai-codex"].GlassTint==.11,"gallery frost and tint update only the selected card draft");var chosen=Tree<WidgetHost>(page).Single(h=>h.Definition.Id=="ai-codex");Check(Field<DockSurface>(chosen,"_glass").GlassFrost==.8,"the shared optical surface receives the per-card frost value");Check(!options.CardColors.ContainsKey("ai-claude"),"neighboring card material settings remain independent");Capture(root,Path.Combine(Output,"ai-inspector.png"));
            page.ShowCategory(WidgetKind.Status);await Task.Delay(150);window.UpdateLayout();Capture(root,Path.Combine(Output,"status-inspector.png"));root.Children.Clear();shell.Close();
        }finally{window.Close();}
    }
    static async Task FrostProof()
    {
        if((RenderCapability.Tier>>16)<2||SystemParameters.HighContrast){Skipped++;Console.WriteLine("SKIP hardware frost: unavailable in the current render mode");return;}
        var pattern=new DrawingGroup();pattern.Children.Add(new GeometryDrawing(Brushes.White,null,new RectangleGeometry(new Rect(0,0,16,16))));pattern.Children.Add(new GeometryDrawing(Brushes.Black,null,new RectangleGeometry(new Rect(0,0,8,8))));pattern.Children.Add(new GeometryDrawing(Brushes.Black,null,new RectangleGeometry(new Rect(8,8,8,8))));pattern.Freeze();var brush=new DrawingBrush(pattern){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,16,16)};
        var rear=new Window{WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,Width=410,Height=220,Left=SystemParameters.WorkArea.Left+60,Top=SystemParameters.WorkArea.Top+60,Background=brush,ShowActivated=false,ShowInTaskbar=false,Topmost=true};var canvas=new Canvas();var front=new Window{WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ResizeMode=ResizeMode.NoResize,Width=410,Height=220,Left=rear.Left,Top=rear.Top,Content=canvas,ShowActivated=false,ShowInTaskbar=false,Topmost=true};var hosts=new List<WidgetHost>();
        try{for(int i=0;i<2;i++){var host=new WidgetHost(RetiredWidgetFixtures.Get("clock-classic"),2.5);host.SetContent(new Grid());host.SetMaterial(WidgetMaterial.Glass,new(){GlassFrost=i,GlassTint=0,Shadow=0});Canvas.SetLeft(host,12+i*196);Canvas.SetTop(host,20);canvas.Children.Add(host);hosts.Add(host);}rear.Show();front.Show();front.UpdateLayout();for(int i=0;i<60&&hosts.Any(h=>Field<DockSurface>(h,"_glass").Backdrop is null);i++)await Task.Delay(100);Check(hosts.All(h=>Field<DockSurface>(h,"_glass").Backdrop is BitmapSource),"real checkerboard background reaches both clear and frosted panes");await Task.Delay(600);
            double Variation(WidgetHost host){var start=host.PointToScreen(new Point(42,42));var dpi=VisualTreeHelper.GetDpi(host).DpiScaleX;int side=(int)Math.Round(96*dpi);using var bitmap=new System.Drawing.Bitmap(side,side);using(var g=System.Drawing.Graphics.FromImage(bitmap))g.CopyFromScreen((int)start.X,(int)start.Y,0,0,bitmap.Size);bitmap.Save(Path.Combine(Output,host==hosts[0]?"clear-screen.png":"frost-screen.png"));double sum=0,squares=0;int count=0;for(int y=0;y<side;y+=2)for(int x=0;x<side;x+=2){double v=bitmap.GetPixel(x,y).R;sum+=v;squares+=v*v;count++;}return squares/count-Math.Pow(sum/count,2);}
            double clear=Variation(hosts[0]),frost=Variation(hosts[1]);Console.WriteLine($"SCREEN VARIANCE clear={clear:F2}, frost={frost:F2}");Check(clear>frost*1.3&&clear>100,"increasing frost visibly blurs the real screen background");var origin=front.PointToScreen(new Point());var displayDpi=VisualTreeHelper.GetDpi(front).DpiScaleX;using var screen=new System.Drawing.Bitmap((int)(front.ActualWidth*displayDpi),(int)(front.ActualHeight*displayDpi));using(var g=System.Drawing.Graphics.FromImage(screen))g.CopyFromScreen((int)origin.X,(int)origin.Y,0,0,screen.Size);screen.Save(Path.Combine(Output,"frost-comparison.png"));
        }finally{front.Close();rear.Close();}
    }
    static BitmapSource Capture(FrameworkElement target,string file){var bitmap=new RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(target.ActualWidth)),Math.Max(1,(int)Math.Ceiling(target.ActualHeight)),96,96,PixelFormats.Pbgra32);bitmap.Render(target);bitmap.Freeze();var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(file);encoder.Save(stream);return bitmap;}
    static void ContactSheet(List<(string Name,BitmapSource Image)> tiles)
    {
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(WidgetBrushes.Get("#151A20"),null,new Rect(0,0,1280,1320));var face=new Typeface("Microsoft YaHei UI");dc.DrawText(new FormattedText("新增小组件 · 离线样式检查",System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,face,23,Brushes.White,1),new Point(24,15));for(int i=0;i<tiles.Count;i++){var tile=tiles[i];double scale=Math.Min(282d/tile.Image.PixelWidth,168d/tile.Image.PixelHeight),x=22+i%4*320,y=62+i/4*207;dc.DrawImage(tile.Image,new Rect(x,y,tile.Image.PixelWidth*scale,tile.Image.PixelHeight*scale));dc.DrawText(new FormattedText(tile.Name,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,face,13,Brushes.LightGray,1),new Point(x,y+175));}}
        var bitmap=new RenderTargetBitmap(1280,1320,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(Output,"new-styles.png"));encoder.Save(stream);
    }
}

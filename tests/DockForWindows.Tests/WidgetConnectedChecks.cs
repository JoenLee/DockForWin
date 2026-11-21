using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using DockForWindows.Models;
using DockForWindows.Services;
using DockForWindows.Views;

internal static class WidgetConnectedChecks
{
    private static int _passed;
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);_passed++;Console.WriteLine("PASS "+message);}
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async(_,_)=>{try{await Account(directory);await Visuals(directory);File.WriteAllText(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(new{Passed=_passed,Failed=0}));app.Shutdown();}catch(Exception e){Console.WriteLine(e);File.WriteAllText(Path.Combine(directory,"failure.txt"),e.ToString());app.Shutdown(1);}};return app.Run();
    }
    private static async Task Account(string directory)
    {
        using var handler=new GraphHandler();using var http=new HttpClient(handler);using var service=new WidgetMicrosoftService(Path.Combine(directory,"account"),http);
        try{
            await service.SignInAsync("de06cd60-f087-49c5-a87d-704d7fec4280",(code,url)=>Check(code=="TEST-CODE"&&url=="https://microsoft.com/devicelogin","device-code flow displays the Microsoft URL and code"),default);
            Check(service.Connected,"refresh credential stored in Windows credential manager");
            Check(!File.ReadAllText(Path.Combine(directory,"account","microsoft-client.json")).Contains("fake-refresh"),"client configuration contains no OAuth token");
            await Refresh(service,WidgetKind.Reminders);Check(service.Tasks.Count==2&&handler.TaskPages==2,"To Do follows validated Graph pagination");
            var task=service.Tasks[0];Check(task.Title=="Remote task"&&task.List=="Work"&&task.Detail=="Body & text"&&task.Due.HasValue&&!task.Completed,"To Do maps title, list, HTML body, due time and state");
            var taskId=task.Id;await Refresh(service,WidgetKind.Reminders);Check(service.Tasks[0].Id==taskId,"remote IDs remain stable across refreshes");
            await Refresh(service,WidgetKind.Notes);Check(service.Notes.Count==1&&service.Notes[0].Body=="Note body\nMore"&&service.Notes[0].Folder=="Section","OneNote loads page content and section from Graph");
            await Refresh(service,WidgetKind.Notes);Check(handler.ContentReads==1,"unchanged OneNote content uses the memory cache");
            service.Complete(task.Id);for(int i=0;i<100&&handler.Patches==0;i++)await Task.Delay(10);Check(handler.Patches==1&&handler.PatchBody.Contains("completed"),"completion sends an actual Graph PATCH");
            handler.EvilNext=true;await Refresh(service,WidgetKind.Reminders);Check(service.Errors[WidgetKind.Reminders]?.Contains("Invalid Graph URL")==true&&!handler.EvilRequested,"pagination cannot send access tokens to another host");
            handler.EvilNext=false;handler.Hold=true;var pending=Refresh(service,WidgetKind.Reminders);await handler.Started.Task;service.SignOut();handler.Release.TrySetResult();await pending;
            Check(service.Tasks.Count==0&&service.Errors.Count==0,"sign-out cancels in-flight reads without restoring stale data or errors");
            service.SignOut();Check(!service.Connected&&service.Tasks.Count==0&&service.Notes.Count==0,"sign-out removes credential and cloud snapshots");
            await Refresh(service,WidgetKind.Notes);Check(service.Errors[WidgetKind.Notes]!=null&&service.Notes.Count==0,"signed-out errors do not fabricate cloud notes");
        }finally{service.SignOut();}
        Check(!WidgetMicrosoftService.SafeNoteUrl("https://evil.test/note")&&WidgetMicrosoftService.SafeNoteUrl("https://www.onenote.com/notebooks"),"note links are limited to Microsoft HTTPS sites");
        Check(WidgetMicrosoftService.PlainText("<head>x</head><script>alert(1)</script><p>A &amp; B</p><p>C</p>")=="A & B\nC","OneNote HTML is converted to inert readable text");
        var options=new WidgetOptions();using var runtime=new WidgetRuntime(System.Windows.Threading.Dispatcher.CurrentDispatcher,()=>options,cacheDirectory:Path.Combine(directory,"media"));
        typeof(WidgetRuntime).GetField("_location",Private)!.SetValue(runtime,new WidgetLocation("Current","",30.25,120.1));typeof(WidgetRuntime).GetField("_nextLocation",Private)!.SetValue(runtime,DateTime.Now.AddHours(1));
        var source=new WidgetSource{UseCurrentLocation=true,City="Old",Latitude=1,Longitude=2};var resolved=runtime.ResolvedSource(source);
        Check(resolved.Latitude==30.25&&resolved.Longitude==120.1&&source.Latitude==1,"weather resolves live coordinates without overwriting a draft or saved city");
        source.UseCurrentLocation=false;Check(ReferenceEquals(runtime.ResolvedSource(source),source),"manual weather locations bypass geolocation");
        var instance=WidgetRegistry.CreateInstance("notes-list",WidgetMaterial.Light,new WidgetOptions{Sources=new(){["notes-list"]=new(){MicrosoftAccount=true,List="Section"}}});
        Check(instance.Source.MicrosoftAccount&&instance.Source.List=="Section","desktop placement copies the selected Microsoft data source");
    }
    private static Task Refresh(WidgetMicrosoftService service,WidgetKind kind)=>(Task)typeof(WidgetMicrosoftService).GetMethod("RefreshAsync",Private)!.Invoke(service,new object[]{kind})!;
    private static IEnumerable<T> Tree<T>(DependencyObject root)where T:DependencyObject{if(root is T value)yield return value;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Tree<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private static async Task Visuals(string directory)
    {
        var image=BitmapSource.Create(96,48,96,96,PixelFormats.Bgra32,null,Enumerable.Repeat(new byte[]{40,130,230,255},96*48).SelectMany(x=>x).ToArray(),96*4);image.Freeze();var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=new MemoryStream();encoder.Save(stream);var art=stream.ToArray();
        var media=new DockForWindows.Services.MediaState("A long album title with artwork","Artist",true,true,true,true,art,TimeSpan.FromSeconds(42),TimeSpan.FromMinutes(4),true);
        var options=new WidgetOptions();options.MusicHistory.Add(new(){Title="History",Artist="Artist",ArtworkKey="test"});var data=new WidgetRenderData(options,new WidgetSource(),Media:media,MediaCommand:_=>{},HistoryArt:new Dictionary<string,byte[]>{{"test",art}});
        var root=new Grid{Background=Brushes.LightGray};var window=new Window{Content=root,Width=900,Height=760,ShowInTaskbar=false};window.Show();
        foreach(var definition in WidgetRegistry.Definitions.Where(x=>x.Kind==WidgetKind.Music))foreach(var size in WidgetSizes.For(definition)){
            var host=new WidgetHost(definition with{Size=size},2.5){HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};host.SetContent(WidgetContent.Render(host.Definition,data));root.Children.Clear();root.Children.Add(host);window.UpdateLayout();await System.Windows.Threading.Dispatcher.Yield();
            var covers=Tree<Border>(host).Where(x=>AutomationProperties.GetAutomationId(x)=="widget-music-art").ToArray();
            Check((definition.Id=="music-form-single"||covers.Length>0)&&covers.All(x=>x.CornerRadius.TopLeft>0&&x.Background is ImageBrush{Stretch:Stretch.UniformToFill,AlignmentX:AlignmentX.Center}),$"rounded centered bitmap artwork {definition.Id} {size}");
            var buttons=Tree<Button>(host).Where(x=>AutomationProperties.GetAutomationId(x) is "widget-music-previous" or "widget-music-toggle" or "widget-music-next").ToArray();
            Check(buttons.Length==3&&buttons.All(x=>x.ActualWidth>=32&&x.ActualHeight>=32&&x.IsEnabled),$"three usable transport buttons {definition.Id} {size}");
            Check(buttons.All(x=>new Rect(host.RenderSize).Contains(x.TransformToAncestor(host).TransformBounds(new Rect(x.RenderSize)))),$"transport fits inside widget {definition.Id} {size}");
            Capture(host,Path.Combine(directory,definition.Id+"-"+size.Columns+"x"+size.Rows+".png"));
        }
        var clock=new WidgetClock(TimeZoneInfo.Local,false);root.Children.Clear();root.Children.Add(clock);window.UpdateLayout();Check(Tree<TextBlock>(clock).All(x=>x.FontWeight==FontWeights.Bold)&&Tree<System.Windows.Shapes.Rectangle>(clock).Count(x=>x.Width>=3)>=12,"analog clock numbers and hour ticks are bold");
        var desktop=new WidgetHost(WidgetRegistry.Get("clock-classic"),2.5);desktop.SetMaterial(WidgetMaterial.Light,new(){Shadow=0});
        var gallery=new WidgetHost(WidgetRegistry.Get("clock-classic"),2.5);gallery.UseGalleryShadow();gallery.SetMaterial(WidgetMaterial.Light,new(){Shadow=0});
        double Shadow(WidgetHost h)=>((DropShadowEffect)((System.Windows.Shapes.Path)typeof(WidgetHost).GetField("_shadow",Private)!.GetValue(h)!).Effect).Opacity;
        Check(Shadow(desktop)==0&&Shadow(gallery)>.1,"gallery card shadow stays visible while desktop shadow remains independent");
        foreach(var definition in WidgetRegistry.Definitions)options.Sources[definition.Id]=new WidgetSource();
        var page=new WidgetGalleryPage(options,null);root.Children.Clear();root.Children.Add(page);await Task.Delay(150);window.UpdateLayout();
        Check(Tree<Slider>(page).All(x=>!AutomationProperties.GetAutomationId(x).StartsWith("widget-shadow-")),"gallery no longer offers shadow strength");
        Check(Tree<WidgetPreviewFrame>(page).Count()==Enum.GetValues<WidgetKind>().Length,"every gallery category has static vignette and backlight");
        foreach(var kind in Enum.GetValues<WidgetKind>()){page.ShowCategory(kind);window.UpdateLayout();Capture(root,Path.Combine(directory,"gallery-"+kind.ToString().ToLowerInvariant()+".png"));}
        root.Children.Clear();
        foreach(var theme in new[]{InterfaceTheme.Light,InterfaceTheme.Dark}){
            var shell=new SettingsShellWindow(new DockSettings{InterfaceTheme=theme,Widgets=options},(_,_)=>{},9);
            window.Resources=shell.Resources;root.SetResourceReference(Panel.BackgroundProperty,"SettingsField");
            page=new WidgetGalleryPage(options,null);root.Children.Add(page);await Task.Delay(150);page.ShowCategory(WidgetKind.Music);window.UpdateLayout();Capture(root,Path.Combine(directory,"styled-music-"+theme+".png"));
            page.ShowCategory(WidgetKind.Reminders);Tree<Expander>(page).First(x=>AutomationProperties.GetAutomationId(x)=="widget-source-reminders").IsExpanded=true;window.UpdateLayout();
            var use=Tree<CheckBox>(page).First(x=>AutomationProperties.GetAutomationId(x)=="widget-microsoft-source-reminders");use.IsChecked=true;await Task.Delay(80);window.UpdateLayout();
            Check(Tree<Button>(page).First(x=>AutomationProperties.GetAutomationId(x)=="widget-cloud-open-reminders").IsVisible&&!Tree<Button>(page).First(x=>AutomationProperties.GetAutomationId(x)=="widget-task-add").IsVisible,"cloud source hides local create controls in "+theme);
            Capture(root,Path.Combine(directory,"styled-todo-"+theme+".png"));root.Children.Clear();shell.Close();
        }
        window.Close();
    }
    private static void Capture(FrameworkElement target,string path){var bmp=new RenderTargetBitmap((int)Math.Ceiling(target.ActualWidth),(int)Math.Ceiling(target.ActualHeight),96,96,PixelFormats.Pbgra32);bmp.Render(target);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using var f=File.Create(path);encoder.Save(f);}
    private sealed class GraphHandler:HttpMessageHandler
    {
        internal bool Hold;internal TaskCompletionSource Started=new(),Release=new();internal int TaskPages,ContentReads,Patches;internal string PatchBody="";internal bool EvilNext,EvilRequested;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            string path=request.RequestUri!.AbsolutePath,body;if(Hold&&path.EndsWith("/todo/lists")){Started.TrySetResult();await Release.Task.WaitAsync(token);}
            if(request.RequestUri.Host=="evil.test"){EvilRequested=true;throw new Exception("Token leak");}
            if(path.EndsWith("/devicecode"))body="""{"device_code":"fake-device","user_code":"TEST-CODE","verification_uri":"https://microsoft.com/devicelogin","expires_in":60,"interval":1}""";
            else if(path.EndsWith("/token"))body="""{"access_token":"fake-access","refresh_token":"fake-refresh","expires_in":3600}""";
            else{
                Check(request.Headers.Authorization?.Parameter=="fake-access","Graph uses the delegated access token");
                if(request.Method==HttpMethod.Patch){Patches++;PatchBody=await request.Content!.ReadAsStringAsync(token);body="{}";}
                else if(path.EndsWith("/todo/lists"))body="""{"value":[{"id":"list/id","displayName":"Work"}]}""";
                else if(path.EndsWith("/tasks")){TaskPages++;body="""{"value":[{"id":"task/1","title":"Remote task","status":"notStarted","body":{"contentType":"html","content":"<p>Body &amp; text</p>"},"dueDateTime":{"dateTime":"2026-11-14T10:00:00","timeZone":"UTC"}}],"@odata.nextLink":"NEXT"}""".Replace("NEXT",EvilNext?"https://evil.test/tasks":"https://graph.microsoft.com/v1.0/next-tasks");}
                else if(path.EndsWith("/next-tasks")){TaskPages++;body="""{"value":[{"id":"task2","title":"Second","status":"completed"}]}""";}
                else if(path.EndsWith("/onenote/pages"))body="""{"value":[{"id":"page1","title":"Cloud note","lastModifiedDateTime":"2026-11-14T10:00:00Z","parentSection":{"displayName":"Section"},"links":{"oneNoteWebUrl":{"href":"https://www.onenote.com/notebooks"}}}]}""";
                else if(path.EndsWith("/content")){ContentReads++;body="<html><head><title>Ignored</title></head><body><p>Note body</p><p>More</p></body></html>";}
                else throw new Exception("Unexpected URL "+request.RequestUri);
            }return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,"application/json")};
        }
    }
}

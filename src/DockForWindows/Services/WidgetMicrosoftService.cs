using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DockForWindows.Models;

namespace DockForWindows.Services;

// Delegated device-code login: no client secret and no credentials in settings.json.
internal sealed class WidgetMicrosoftService : IDisposable
{
    private const string Authority="https://login.microsoftonline.com/common/oauth2/v2.0/";
    private const string Graph="https://graph.microsoft.com/v1.0/";
    private const string Scope="offline_access https://graph.microsoft.com/Tasks.ReadWrite https://graph.microsoft.com/Notes.Read";
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _configuration;
    private readonly SemaphoreSlim _auth=new(1,1);
    private readonly CancellationTokenSource _lifetime=new();
    private readonly Dictionary<WidgetKind,DateTime> _next=new();
    private readonly HashSet<WidgetKind> _reading=new();
    private readonly HashSet<Guid> _completing=new();
    private readonly Dictionary<string,(string Stamp,string Body)> _pages=new();
    private CancellationTokenSource _session=new();
    private int _sessionVersion;
    private string? _access;
    private DateTimeOffset _expires;
    private bool _disposed;
    internal string ClientId {get;private set;}="";
    internal bool Connected=>!string.IsNullOrEmpty(ClientId)&&WidgetAccountCredential.Read(CredentialKey)!=null;
    private string CredentialKey=>"DockForWin/Microsoft/"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_configuration+ClientId)));
    internal IReadOnlyList<WidgetTask> Tasks {get;private set;}=Array.Empty<WidgetTask>();
    internal IReadOnlyList<WidgetNote> Notes {get;private set;}=Array.Empty<WidgetNote>();
    internal Dictionary<WidgetKind,string?> Errors {get;}=new();
    internal event Action<WidgetKind?>? Changed;
    internal WidgetMicrosoftService(string directory,HttpClient? http=null)
    {
        _configuration=Path.Combine(directory,"microsoft-client.json");_http=http??new HttpClient{Timeout=TimeSpan.FromSeconds(25)};_ownsHttp=http==null;
        try{if(File.Exists(_configuration)){using var doc=JsonDocument.Parse(File.ReadAllText(_configuration));var id=Text(doc.RootElement,"clientId");if(Guid.TryParse(id,out var guid))ClientId=guid.ToString();}}catch(IOException){}catch(UnauthorizedAccessException){}catch(JsonException){}
    }
    internal async Task SignInAsync(string clientId,Action<string,string> showCode,CancellationToken cancellation)
    {
        if(!Guid.TryParse(clientId,out var guid)||guid==Guid.Empty)throw new ArgumentException(LocalizedText.Get("请输入有效的 Microsoft 客户端 ID。"));
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,_lifetime.Token);var token=linked.Token;
        await _auth.WaitAsync(token);
        try{
            SignOut();ClientId=guid.ToString();
            Directory.CreateDirectory(Path.GetDirectoryName(_configuration)!);
            await File.WriteAllTextAsync(_configuration,JsonSerializer.Serialize(new{clientId=ClientId}),token);
            using var response=await PostAsync("devicecode",new(){["client_id"]=ClientId,["scope"]=Scope},token);
            var root=response.RootElement;string deviceCode=Text(root,"device_code"),code=Text(root,"user_code"),url=Text(root,"verification_uri");
            if(deviceCode.Length==0||code.Length==0||!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!(uri.Host=="microsoft.com"||uri.Host.EndsWith(".microsoft.com",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException(LocalizedText.Get("Microsoft 返回了无效的登录地址。"));
            showCode(code,url);int interval=root.TryGetProperty("interval",out var iv)?Math.Clamp(iv.GetInt32(),1,60):5;
            var until=DateTimeOffset.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32());
            while(DateTimeOffset.UtcNow<until){
                await Task.Delay(TimeSpan.FromSeconds(interval),token);
                using var poll=await _http.PostAsync(Authority+"token",new FormUrlEncodedContent(new Dictionary<string,string>{["grant_type"]="urn:ietf:params:oauth:grant-type:device_code",["client_id"]=ClientId,["device_code"]=deviceCode}),token);
                using var result=JsonDocument.Parse(await poll.Content.ReadAsStringAsync(token));string error=Text(result.RootElement,"error");
                if(error=="authorization_pending")continue;if(error=="slow_down"){interval+=5;continue;}
                if(!poll.IsSuccessStatusCode)throw new InvalidOperationException(LocalizedText.Get("Microsoft 登录未完成：")+error);
                token.ThrowIfCancellationRequested();SetToken(result.RootElement);Errors.Clear();_next.Clear();Changed?.Invoke(null);return;
            }
            throw new TimeoutException(LocalizedText.Get("登录代码已过期，请重试。"));
        }finally{_auth.Release();}
    }
    private void SetToken(JsonElement root)
    {
        string access=Text(root,"access_token");if(access.Length==0)throw new InvalidDataException(LocalizedText.Get("Microsoft 未返回访问令牌。"));
        string refresh=Text(root,"refresh_token");if(refresh.Length>0)WidgetAccountCredential.Write(CredentialKey,refresh);
        _access=access;_expires=DateTimeOffset.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32()-60);
    }
    private async Task<string> TokenAsync(CancellationToken token)
    {
        await _auth.WaitAsync(token);try{
            if(_access!=null&&_expires>DateTimeOffset.UtcNow)return _access;
            string refresh=WidgetAccountCredential.Read(CredentialKey)??throw new InvalidOperationException(LocalizedText.Get("请先登录 Microsoft 账户。"));
            using var doc=await PostAsync("token",new(){["client_id"]=ClientId,["grant_type"]="refresh_token",["refresh_token"]=refresh,["scope"]=Scope},token);
            token.ThrowIfCancellationRequested();SetToken(doc.RootElement);return _access!;
        }finally{_auth.Release();}
    }
    private async Task<JsonDocument> PostAsync(string endpoint,Dictionary<string,string> body,CancellationToken token)
    {
        using var response=await _http.PostAsync(Authority+endpoint,new FormUrlEncodedContent(body),token);
        var document=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        if(!response.IsSuccessStatusCode){string code=Text(document.RootElement,"error");document.Dispose();throw new InvalidOperationException(LocalizedText.Get("Microsoft 授权失败：")+code);}
        return document;
    }
    private async Task<string> SendAsync(string path,CancellationToken token,HttpMethod? method=null,object? body=null)
    {
        var uri=new Uri(path.StartsWith("https://",StringComparison.OrdinalIgnoreCase)?path:Graph+path);
        if(uri.Scheme!="https"||uri.Host!="graph.microsoft.com"||!uri.AbsolutePath.StartsWith("/v1.0/",StringComparison.Ordinal))throw new InvalidDataException("Invalid Graph URL");
        using var request=new HttpRequestMessage(method??HttpMethod.Get,uri);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",await TokenAsync(token));
        if(body!=null)request.Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json");
        using var response=await _http.SendAsync(request,token);
        if(!response.IsSuccessStatusCode){if(response.StatusCode==HttpStatusCode.Unauthorized){_access=null;_expires=default;}
            string code="";try{using var problem=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));code=Text(problem.RootElement.GetProperty("error"),"code");}catch(JsonException){}catch(KeyNotFoundException){}
            throw new InvalidOperationException(LocalizedText.Get("Microsoft 数据读取失败：")+$"{(int)response.StatusCode} {code}");}
        return await response.Content.ReadAsStringAsync(token);
    }
    private async Task<List<JsonElement>> CollectionAsync(string path,int limit,CancellationToken token)
    {
        var values=new List<JsonElement>();var visited=new HashSet<string>();
        while(path.Length>0&&values.Count<limit&&visited.Add(path)){
            using var doc=JsonDocument.Parse(await SendAsync(path,token));
            foreach(var value in doc.RootElement.GetProperty("value").EnumerateArray()){values.Add(value.Clone());if(values.Count==limit)break;}
            path=Text(doc.RootElement,"@odata.nextLink");
        }return values;
    }
    internal void Ensure(WidgetKind kind)
    {
        if(_disposed||kind is not (WidgetKind.Reminders or WidgetKind.Notes))return;
        if(!_next.TryGetValue(kind,out var next)||next<=DateTime.UtcNow){if(_reading.Add(kind))_ =RefreshAsync(kind);}
    }
    internal void Refresh(){_next.Clear();Ensure(WidgetKind.Reminders);Ensure(WidgetKind.Notes);}
    private async Task RefreshAsync(WidgetKind kind)
    {
        int version=_sessionVersion;try{
            using var work=CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token,_session.Token);var token=work.Token;
            if(kind==WidgetKind.Reminders){var tasks=new List<WidgetTask>();
                foreach(var list in await CollectionAsync("me/todo/lists?$top=100",100,token)){
                    string listId=Text(list,"id");foreach(var task in await CollectionAsync("me/todo/lists/"+Uri.EscapeDataString(listId)+"/tasks?$top=100",500-tasks.Count,token))tasks.Add(ParseTask(task,listId,Text(list,"displayName")));
                    if(tasks.Count>=500)break;
                }if(!_disposed&&version==_sessionVersion)Tasks=tasks;
            }else{
                var notes=new List<WidgetNote>();var retained=new HashSet<string>();
                foreach(var page in await CollectionAsync("me/onenote/pages?$top=30&$orderby=lastModifiedDateTime%20desc&$expand=parentSection($select=displayName)",30,token)){
                    string id=Text(page,"id"),stamp=Text(page,"lastModifiedDateTime");retained.Add(id);
                    if(!_pages.TryGetValue(id,out var cached)||cached.Stamp!=stamp){string html=await SendAsync("me/onenote/pages/"+Uri.EscapeDataString(id)+"/content",token);token.ThrowIfCancellationRequested();cached=(stamp,PlainText(html));_pages[id]=cached;}
                    notes.Add(ParseNote(page,cached.Body));
                }
                foreach(var id in _pages.Keys.Where(id=>!retained.Contains(id)).ToArray())_pages.Remove(id);
                if(!_disposed&&version==_sessionVersion)Notes=notes;
            }
            if(!_disposed&&version==_sessionVersion){Errors[kind]=null;_next[kind]=DateTime.UtcNow.AddMinutes(1);}
        }catch(OperationCanceledException){}catch(Exception e){if(!_disposed&&version==_sessionVersion){Errors[kind]=e.Message;_next[kind]=DateTime.UtcNow.AddMinutes(1);}}
        finally{_reading.Remove(kind);if(!_disposed&&version==_sessionVersion)Changed?.Invoke(kind);}
    }
    internal async void Complete(Guid id)
    {
        var item=Tasks.FirstOrDefault(x=>x.Id==id);if(item==null||!_completing.Add(id))return;
        using var operation=CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token,_session.Token);try{await SendAsync("me/todo/lists/"+Uri.EscapeDataString(item.RemoteListId)+"/tasks/"+Uri.EscapeDataString(item.RemoteId),operation.Token,HttpMethod.Patch,new{status=item.Completed?"notStarted":"completed"});_next.Remove(WidgetKind.Reminders);Ensure(WidgetKind.Reminders);}
        catch(OperationCanceledException){}catch(Exception e){if(operation.IsCancellationRequested)return;Errors[WidgetKind.Reminders]=e.Message;if(!_disposed)Changed?.Invoke(WidgetKind.Reminders);}
        finally{_completing.Remove(id);}
    }
    internal static WidgetTask ParseTask(JsonElement task,string listId,string name)
    {
        DateTime? due=null;if(task.TryGetProperty("dueDateTime",out var date)&&date.ValueKind==JsonValueKind.Object&&DateTime.TryParse(Text(date,"dateTime"),CultureInfo.InvariantCulture,DateTimeStyles.None,out var time)){
            try{var zone=TimeZoneInfo.FindSystemTimeZoneById(Text(date,"timeZone"));due=TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(time,DateTimeKind.Unspecified),zone,TimeZoneInfo.Local);}catch(TimeZoneNotFoundException){}catch(ArgumentException){}
        }
        string id=Text(task,"id");string detail=task.TryGetProperty("body",out var body)?Text(body,"content"):"";
        if(task.TryGetProperty("body",out body)&&Text(body,"contentType")=="html")detail=PlainText(detail);
        return new(){Id=StableId(listId+":"+id),RemoteId=id,RemoteListId=listId,Title=Text(task,"title"),List=name,Completed=Text(task,"status")=="completed",Due=due,Detail=detail};
    }
    internal static WidgetNote ParseNote(JsonElement page,string body)
    {
        string url="";if(page.TryGetProperty("links",out var links)&&links.TryGetProperty("oneNoteWebUrl",out var web))url=Text(web,"href");
        string folder=page.TryGetProperty("parentSection",out var section)?Text(section,"displayName"):"OneNote";
        DateTime.TryParse(Text(page,"lastModifiedDateTime"),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var modified);
        return new(){Id=StableId(Text(page,"id")),Title=Text(page,"title"),Body=body,Folder=folder,Modified=modified.ToLocalTime(),WebUrl=SafeNoteUrl(url)?url:""};
    }
    internal static bool SafeNoteUrl(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&(uri.Host=="onenote.com"||uri.Host=="onedrive.live.com"||uri.Host.EndsWith(".onenote.com",StringComparison.OrdinalIgnoreCase)||uri.Host.EndsWith(".officeapps.live.com",StringComparison.OrdinalIgnoreCase)||uri.Host.EndsWith(".sharepoint.com",StringComparison.OrdinalIgnoreCase));
    internal static string PlainText(string html)
    {
        var timeout=TimeSpan.FromSeconds(1);
        string text=Regex.Replace(html,@"<(script|style|head)\b[^>]*>.*?</\1>","",RegexOptions.Singleline|RegexOptions.IgnoreCase,timeout);
        text=Regex.Replace(text,@"<br\b[^>]*>|</(?:p|div|h[1-6]|li|tr)>","\n",RegexOptions.IgnoreCase,timeout);
        text=WebUtility.HtmlDecode(Regex.Replace(text,"<[^>]*>","",RegexOptions.None,timeout));return text.Trim()[..Math.Min(4096,text.Trim().Length)];
    }
    private static string Text(JsonElement obj,string key)=>obj.ValueKind==JsonValueKind.Object&&obj.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
    private static Guid StableId(string key)=>new(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0,16));
    internal void SignOut(){_sessionVersion++;_session.Cancel();_session.Dispose();_session=new();WidgetAccountCredential.Delete(CredentialKey);_access=null;_expires=default;Tasks=Array.Empty<WidgetTask>();Notes=Array.Empty<WidgetNote>();_pages.Clear();_next.Clear();Errors.Clear();Changed?.Invoke(null);}
    public void Dispose(){if(_disposed)return;_disposed=true;_lifetime.Cancel();_session.Cancel();if(_ownsHttp)_http.Dispose();Changed=null;}
}

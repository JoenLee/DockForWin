using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace DockForWindows.Services;

internal sealed record WidgetUsageWindow(double? RemainingPercent,DateTimeOffset? ResetsAt);
internal sealed record WidgetDailyTokens(DateOnly Date,long Tokens);
internal sealed record WidgetAiAccount(string Name,WidgetUsageWindow FiveHour,WidgetUsageWindow Week,DateTimeOffset? UpdatedAt,IReadOnlyList<WidgetDailyTokens> Daily);
internal sealed record WidgetAiSnapshot(WidgetAiAccount Codex,WidgetAiAccount Claude,string? Error=null)
{
    internal static WidgetAiAccount Empty(string name)=>new(name,new(null,null),new(null,null),null,Array.Empty<WidgetDailyTokens>());
    internal static WidgetAiSnapshot Missing(string? error=null)=>new(Empty("Codex"),Empty("Claude"),error);
}

internal static class WidgetAiUsageService
{
    // Session files are data only: no credential files are opened and no text in a session is executed.
    internal static WidgetAiSnapshot Read(string file,CancellationToken token)
    {
        try{
            if(!string.IsNullOrWhiteSpace(file)){
                var info=new FileInfo(file);if(!info.Exists)throw new IOException(UiText.IsEnglish?"Usage file was not found.":"用量文件不存在。");
                if(info.Length>2_000_000)throw new IOException(UiText.IsEnglish?"Usage file must be smaller than 2 MB.":"用量文件不能超过 2 MB。");
                using var json=JsonDocument.Parse(File.ReadAllText(file));return ParseFile(json.RootElement,DateTimeOffset.Now);
            }
            string root=Path.Combine(Environment.GetEnvironmentVariable("CODEX_HOME")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex"),"sessions");
            if(!Directory.Exists(root))return WidgetAiSnapshot.Missing(UiText.IsEnglish?"No local Codex usage records.":"暂无本机 Codex 用量记录。");
            var files=Directory.EnumerateFiles(root,"*.jsonl",SearchOption.AllDirectories).Select(f=>new FileInfo(f)).Where(f=>f.LastWriteTimeUtc>=DateTime.UtcNow.AddDays(-7)).OrderByDescending(f=>f.LastWriteTimeUtc).Take(16).ToArray();
            WidgetAiAccount latest=WidgetAiSnapshot.Empty("Codex");var daily=new Dictionary<DateOnly,long>();
            foreach(var info in files){token.ThrowIfCancellationRequested();try{
                using var stream=new FileStream(info.FullName,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
                bool tail=stream.Length>4_000_000;if(tail)stream.Seek(-4_000_000,SeekOrigin.End);
                using var reader=new StreamReader(stream);if(tail)reader.ReadLine();
                var account=ParseCodex(Lines(reader,token),DateTimeOffset.Now,tail);
                if(account.UpdatedAt>latest.UpdatedAt||latest.UpdatedAt==null)latest=account;
                foreach(var item in account.Daily)daily[item.Date]=daily.GetValueOrDefault(item.Date)+item.Tokens;
            }catch(IOException){}catch(UnauthorizedAccessException){}}
            return new(latest with{Daily=daily.OrderBy(p=>p.Key).Select(p=>new WidgetDailyTokens(p.Key,p.Value)).ToArray()},WidgetAiSnapshot.Empty("Claude"));
        }catch(OperationCanceledException){throw;}catch(Exception error)when(error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException){return WidgetAiSnapshot.Missing(error.Message);}
    }
    private static IEnumerable<string> Lines(StreamReader reader,CancellationToken token)
    {
        while(reader.ReadLine() is {} line){token.ThrowIfCancellationRequested();if(line.Contains("\"token_count\"",StringComparison.Ordinal))yield return line;}
    }
    internal static WidgetAiAccount ParseCodex(IEnumerable<string> lines,DateTimeOffset now,bool partial=false)
    {
        var account=WidgetAiSnapshot.Empty("Codex");var daily=new Dictionary<DateOnly,long>();long previous=0;bool first=true;
        foreach(var line in lines)try{
            using var doc=JsonDocument.Parse(line);var root=doc.RootElement;
            if(!root.TryGetProperty("payload",out var payload)||!payload.TryGetProperty("type",out var type)||type.GetString()!="token_count"||!root.TryGetProperty("timestamp",out var stamp)||!stamp.TryGetDateTimeOffset(out var at))continue;
            if(payload.TryGetProperty("info",out var info)&&info.ValueKind==JsonValueKind.Object&&info.TryGetProperty("total_token_usage",out var usage)&&usage.TryGetProperty("total_tokens",out var total)&&total.TryGetInt64(out long count)){
                long delta=first&&partial?0:Math.Max(0,count-previous);previous=count;first=false;var day=DateOnly.FromDateTime(at.LocalDateTime);
                if(day>=DateOnly.FromDateTime(now.LocalDateTime.AddDays(-6))&&day<=DateOnly.FromDateTime(now.LocalDateTime))daily[day]=daily.GetValueOrDefault(day)+delta;
            }
            if(!payload.TryGetProperty("rate_limits",out var limits)||limits.ValueKind!=JsonValueKind.Object)continue;
            if(limits.TryGetProperty("limit_id",out var id)&&id.ValueKind==JsonValueKind.String&&id.GetString()!="codex")continue;
            if(account.UpdatedAt is {} updated&&at<updated)continue;
            WidgetUsageWindow Window(string key){if(!limits.TryGetProperty(key,out var w)||w.ValueKind!=JsonValueKind.Object)return new(null,null);
                DateTimeOffset? resets=w.TryGetProperty("resets_at",out var reset)&&reset.TryGetInt64(out long epoch)?DateTimeOffset.FromUnixTimeSeconds(epoch):null;
                double? percent=w.TryGetProperty("used_percent",out var used)&&used.TryGetDouble(out double v)&&double.IsFinite(v)&&v>=0&&v<=100?100-v:null;
                return new(resets<=now?null:percent,resets);}
            account=new("Codex",Window("primary"),Window("secondary"),at,Array.Empty<WidgetDailyTokens>());
        }catch(JsonException){}catch(InvalidOperationException){}catch(ArgumentOutOfRangeException){}
        return account with{Daily=daily.OrderBy(p=>p.Key).Select(p=>new WidgetDailyTokens(p.Key,p.Value)).ToArray()};
    }
    internal static WidgetAiSnapshot ParseFile(JsonElement root,DateTimeOffset now)
    {
        WidgetAiAccount Account(string key,string name){if(!root.TryGetProperty(key,out var item)||item.ValueKind!=JsonValueKind.Object)return WidgetAiSnapshot.Empty(name);
            DateTimeOffset? updated=item.TryGetProperty("updatedAt",out var stamp)&&stamp.ValueKind==JsonValueKind.String&&stamp.TryGetDateTimeOffset(out var at)?at:null;
            WidgetUsageWindow Window(string key){if(!item.TryGetProperty(key,out var w)||w.ValueKind!=JsonValueKind.Object)return new(null,null);
                DateTimeOffset? resets=w.TryGetProperty("resetsAt",out var reset)&&reset.ValueKind==JsonValueKind.String&&reset.TryGetDateTimeOffset(out var date)?date:null;
                double? percent=w.TryGetProperty("remainingPercent",out var remaining)&&remaining.ValueKind==JsonValueKind.Number&&remaining.TryGetDouble(out double v)&&double.IsFinite(v)&&v>=0&&v<=100?v:null;
                return new(resets<=now?null:percent,resets);}
            var daily=new List<WidgetDailyTokens>();if(item.TryGetProperty("dailyTokens",out var history)&&history.ValueKind==JsonValueKind.Array)foreach(var row in history.EnumerateArray().Take(31)){
                if(row.TryGetProperty("date",out var date)&&DateOnly.TryParseExact(date.GetString(),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var day)&&row.TryGetProperty("tokens",out var tokens)&&tokens.TryGetInt64(out long count)&&count>=0)daily.Add(new(day,count));}
            return new(name,Window("fiveHour"),Window("week"),updated,daily);}
        return new(Account("codex","Codex"),Account("claude","Claude"));
    }
}

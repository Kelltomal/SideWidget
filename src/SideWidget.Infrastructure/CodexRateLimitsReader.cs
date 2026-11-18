using System.Diagnostics;
using System.Text;
using System.Text.Json;
using SideWidget.Core;

namespace SideWidget.Infrastructure;

public sealed class CodexRateLimitsReader(string directory)
{
    public async Task<CodexLimitsSnapshot> ReadAsync(CancellationToken cancellation=default)
    {
        try
        {
            var executable=FindExecutable();
            if(executable is not null)
            {
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                var live=await ReadLiveAsync(executable,timeout.Token).ConfigureAwait(false);
                if(live is not null)return live;
            }
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception or OperationCanceledException or JsonException)
        {if(cancellation.IsCancellationRequested)throw new OperationCanceledException(cancellation);}
        cancellation.ThrowIfCancellationRequested();
        return new(false,DateTimeOffset.UtcNow,[],"Лимиты Codex недоступны. Войдите в Codex и проверьте подключение.");
    }

    static string? FindExecutable()
    {
        var configured=Environment.GetEnvironmentVariable("CODEX_EXECUTABLE");
        if(!string.IsNullOrWhiteSpace(configured)&&File.Exists(configured))return configured;
        foreach(var part in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator))
        {
            var path=Path.Combine(part.Trim('"'),"codex.exe");if(File.Exists(path))return path;
        }
        var alias=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","WindowsApps","codex.exe");
        return File.Exists(alias)?alias:null;
    }

    async Task<CodexLimitsSnapshot?> ReadLiveAsync(string executable,CancellationToken cancellation)
    {
        using var process=new Process{StartInfo=new ProcessStartInfo(executable)
        {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8}};
        process.StartInfo.Environment["CODEX_HOME"]=directory;
        process.StartInfo.ArgumentList.Add("app-server");process.StartInfo.ArgumentList.Add("--stdio");
        if(!process.Start())return null;
        // Drain diagnostics without retaining or displaying config/account data.
        var drain=DrainAsync(process.StandardError,cancellation);
        try
        {
            await process.StandardInput.WriteLineAsync("{\"id\":1,\"method\":\"initialize\",\"params\":{\"clientInfo\":{\"name\":\"sidewidget-limit-reader\",\"version\":\"0.1.0\"},\"capabilities\":{\"experimentalApi\":false}}}".AsMemory(),cancellation);
            await process.StandardInput.FlushAsync(cancellation);
            if(await ReadResponseAsync(process.StandardOutput,1,cancellation) is not {} initialized||initialized.TryGetProperty("error",out _))return null;
            await process.StandardInput.WriteLineAsync("{\"method\":\"initialized\",\"params\":{}}".AsMemory(),cancellation);
            await process.StandardInput.WriteLineAsync("{\"id\":2,\"method\":\"account/rateLimits/read\",\"params\":{}}".AsMemory(),cancellation);
            await process.StandardInput.FlushAsync(cancellation);
            var response=await ReadResponseAsync(process.StandardOutput,2,cancellation);
            return response is {} json&&json.TryGetProperty("result",out var result)?ParseApiResult(result.GetRawText(),DateTimeOffset.UtcNow):null;
        }
        finally
        {
            try {if(!process.HasExited)process.Kill(entireProcessTree:true);}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
            try {await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));}catch(TimeoutException){}
            try {await drain.WaitAsync(TimeSpan.FromSeconds(1));}catch(OperationCanceledException){}catch(TimeoutException){}catch(IOException){}
        }
    }

    static async Task DrainAsync(StreamReader reader,CancellationToken cancellation)
    {
        var buffer=new char[2048];while(await reader.ReadAsync(buffer.AsMemory(),cancellation)>0){}
    }

    static async Task<JsonElement?> ReadResponseAsync(StreamReader reader,int id,CancellationToken cancellation)
    {
        for(var lines=0;lines<256;lines++)
        {
            var line=await reader.ReadLineAsync(cancellation);if(line is null)return null;
            if(line.Length>1024*1024)continue;
            try
            {
                using var document=JsonDocument.Parse(line);var root=document.RootElement;
                if(root.TryGetProperty("id",out var responseId)&&responseId.ValueKind==JsonValueKind.Number&&responseId.TryGetInt32(out var number)&&number==id)return root.Clone();
            }
            catch(JsonException){}
        }
        return null;
    }

    public static CodexLimitsSnapshot? ParseApiResult(string json,DateTimeOffset checkedAt)
    {
        try
        {
            using var document=JsonDocument.Parse(json);var root=document.RootElement;
            var buckets=new List<CodexLimitBucket>();
            if(root.TryGetProperty("rateLimitsByLimitId",out var byId)&&byId.ValueKind==JsonValueKind.Object)
                foreach(var entry in byId.EnumerateObject())if(ParseBucket(entry.Value,true,entry.Name) is {} bucket)buckets.Add(bucket);
            if(buckets.Count==0&&root.TryGetProperty("rateLimits",out var limits)&&ParseBucket(limits,true,"codex") is {} fallback)buckets.Add(fallback);
            return buckets.Count==0?null:new(true,checkedAt,buckets);
        }
        catch(Exception ex) when(ex is JsonException or InvalidOperationException or ArgumentOutOfRangeException){return null;}
    }

    static CodexLimitBucket? ParseBucket(JsonElement value,bool camel,string fallbackId)
    {
        if(value.ValueKind!=JsonValueKind.Object)return null;
        string? Text(string key)=>value.TryGetProperty(key,out var element)&&element.ValueKind==JsonValueKind.String?element.GetString():null;
        var primary=ParseWindow(value,"primary",camel);var secondary=ParseWindow(value,"secondary",camel);
        return primary is null&&secondary is null?null:new(Text(camel?"limitId":"limit_id")??fallbackId,Text(camel?"limitName":"limit_name"),primary,secondary);
    }

    static CodexLimitWindow? ParseWindow(JsonElement bucket,string key,bool camel)
    {
        if(!bucket.TryGetProperty(key,out var window)||window.ValueKind!=JsonValueKind.Object)return null;
        if(!window.TryGetProperty(camel?"usedPercent":"used_percent",out var percent)||!percent.TryGetDouble(out var used)||!double.IsFinite(used)||used<0||used>100)return null;
        if(!window.TryGetProperty(camel?"windowDurationMins":"window_minutes",out var duration)||!duration.TryGetInt32(out var minutes)||minutes<=0)return null;
        DateTimeOffset? reset=null;
        if(window.TryGetProperty(camel?"resetsAt":"resets_at",out var resets)&&resets.TryGetInt64(out var seconds))reset=DateTimeOffset.FromUnixTimeSeconds(seconds);
        return new(used,minutes,reset);
    }

}

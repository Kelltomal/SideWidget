using System.Diagnostics;
using System.Net;
using System.Text;
using SideWidget.Core;

namespace SideWidget.Infrastructure;

public sealed record WebsiteTarget(string Id,string Url,string ReportUrl,bool Enabled,int IntervalMinutes=5);
public sealed record WebsiteMonitorCheck(DateTimeOffset CheckedAt,string Status,int? HttpStatus,double? ResponseMs,
    string Message,string ReportStatus,int? ReportCount);
public sealed record WebsiteMonitorSite(string Id,string Url,string ReportUrl,bool Enabled,int IntervalMinutes,
    DateTimeOffset? NextCheckAt,WebsiteMonitorCheck? LastCheck,WebsiteMonitorCheck[] History);

/// <summary>Local HTTP reachability and independently attributed public complaint sources.</summary>
public sealed class WebsiteMonitorService:IDisposable
{
    const string Key="personal.websiteMonitor.v1";
    readonly Store store;
    readonly HttpClient client;
    readonly SemaphoreSlim checkGate=new(1,1);
    readonly object stateGate=new();
    readonly CancellationTokenSource lifetime=new();
    readonly CancellationToken stopping;
    readonly System.Threading.Timer timer;
    readonly Dictionary<string,(DateTimeOffset CheckedAt,string Message)> reportCache=new(StringComparer.OrdinalIgnoreCase);
    WebsiteMonitorSite[] sites;
    string? message;
    bool disposed;

    public WebsiteMonitorService(Store store,HttpMessageHandler? handler=null)
    {
        this.store=store;stopping=lifetime.Token;
        client=new HttpClient(handler??new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false,AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate})
        {Timeout=Timeout.InfiniteTimeSpan};
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SideWidgetWebsiteMonitor/1.0");
        var saved=store.Read(Key,Array.Empty<WebsiteMonitorSite>());
        var restored=new List<WebsiteMonitorSite>();
        foreach(var item in saved.Where(x=>x is not null).Take(20))
        {
            try
            {
                var target=Validate(new(item.Id,item.Url,item.ReportUrl,item.Enabled,item.IntervalMinutes));
                if(restored.Any(x=>x.Id==target.Id))continue;
                restored.Add(new(target.Id,target.Url,target.ReportUrl,target.Enabled,target.IntervalMinutes,
                    target.Enabled?item.NextCheckAt??DateTimeOffset.UtcNow:null,item.LastCheck,(item.History??[]).Where(x=>x is not null).Take(24).ToArray()));
            }
            catch(CommandFault){message="Некоторые сохранённые адреса некорректны. Проверьте список сайтов.";}
        }
        sites=restored.ToArray();
        timer=new System.Threading.Timer(_=>{_ = CheckDueSafelyAsync();},null,TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(30));
    }

    public object Snapshot()
    {
        lock(stateGate)return new{sites=sites.Select(site=>site with{History=site.History.ToArray()}).ToArray(),message};
    }

    static WebsiteTarget Validate(WebsiteTarget target)
    {
        if(target is null)throw new CommandFault("website","Укажите сайт");
        var id=target.Id?.Trim()??"";
        if(id.Length==0)id=Guid.NewGuid().ToString("N");
        if(id.Length>100||id.Any(char.IsControl))throw new CommandFault("website","Некорректный идентификатор сайта");
        if(target.IntervalMinutes is <1 or >1440)throw new CommandFault("website","Интервал проверки — от 1 до 1440 минут");
        var url=ValidateUrl(target.Url);
        var report=string.IsNullOrWhiteSpace(target.ReportUrl)?new Uri("https://downradar.ru/ne-rabotaet/"+Uri.EscapeDataString(url.IdnHost)):ValidateUrl(target.ReportUrl);
        return target with{Id=id,Url=url.AbsoluteUri,ReportUrl=report.AbsoluteUri};
    }

    static Uri ValidateUrl(string? value)
    {
        if(string.IsNullOrWhiteSpace(value))throw new CommandFault("website","Введите адрес HTTP или HTTPS");
        var uri=WebLink.Validate(value.Trim());
        if(uri.Fragment.Length!=0)throw new CommandFault("website","Укажите адрес без фрагмента после #");
        return uri;
    }

    public Task<object> SaveAsync(WebsiteTarget site)
    {
        var target=Validate(site);
        lock(stateGate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            var existing=sites.FirstOrDefault(x=>x.Id==target.Id);
            if(existing is null&&sites.Length>=20)throw new CommandFault("limit","Можно отслеживать до 20 сайтов");
            var sameUrl=existing?.Url==target.Url;
            var next=new WebsiteMonitorSite(target.Id,target.Url,target.ReportUrl,target.Enabled,target.IntervalMinutes,
                target.Enabled?DateTimeOffset.UtcNow:null,sameUrl?existing!.LastCheck:null,sameUrl?existing!.History:[]);
            Commit(existing is null?[..sites,next]:sites.Select(x=>x.Id==target.Id?next:x).ToArray());
            return Task.FromResult(Snapshot());
        }
    }

    public Task<object> DeleteAsync(string id)
    {
        lock(stateGate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(!sites.Any(x=>x.Id==id))throw new CommandFault("website","Сайт уже удалён");
            Commit(sites.Where(x=>x.Id!=id).ToArray());return Task.FromResult(Snapshot());
        }
    }

    void Commit(WebsiteMonitorSite[] next)
    {
        // Persist before publishing: failed writes leave current state available for retry.
        lock(stateGate){store.Save(Key,next);sites=next;message=null;}
    }

    public Task<object> CheckAsync(string? id=null)=>CheckInternalAsync(id,false);

    async Task<object> CheckInternalAsync(string? id,bool dueOnly)
    {
        if(!await checkGate.WaitAsync(0,stopping).ConfigureAwait(false))return Snapshot();
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(stopping);
        budget.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            var now=DateTimeOffset.UtcNow;
            WebsiteMonitorSite[] selected;
            lock(stateGate)
            {
                if(id is not null&&!sites.Any(x=>x.Id==id))throw new CommandFault("website","Сайт уже удалён");
                // Oldest due targets first; a timed-out batch cannot starve later targets.
                selected=sites.Where(x=>(id is null?x.Enabled:x.Id==id)&&(!dueOnly||x.NextCheckAt is null||x.NextCheckAt<=now))
                    .OrderBy(x=>x.NextCheckAt??DateTimeOffset.MinValue).ThenBy(x=>x.LastCheck?.CheckedAt??DateTimeOffset.MinValue).ToArray();
            }
            foreach(var site in selected)
            {
                budget.Token.ThrowIfCancellationRequested();
                lock(stateGate){if(!sites.Any(x=>ReferenceEquals(x,site)))continue;}
                var check=await ProbeAsync(site,budget.Token).ConfigureAwait(false);
                budget.Token.ThrowIfCancellationRequested();
                var next=site with{LastCheck=check,History=new[]{check}.Concat(site.History).Take(24).ToArray(),
                    NextCheckAt=site.Enabled?check.CheckedAt.AddMinutes(site.IntervalMinutes):null};
                lock(stateGate)
                {
                    // Immutable record identity acts as revision. Edits and deletion win over
                    // an older probe, including edits which happen to retain the same URL.
                    if(!disposed&&sites.Any(x=>ReferenceEquals(x,site)))Commit(sites.Select(x=>ReferenceEquals(x,site)?next:x).ToArray());
                }
            }
            return Snapshot();
        }
        catch(OperationCanceledException) when(budget.IsCancellationRequested&&!stopping.IsCancellationRequested){return Snapshot();}
        finally {checkGate.Release();}
    }

    async Task CheckDueSafelyAsync()
    {
        try {await CheckInternalAsync(null,true).ConfigureAwait(false);}
        catch(OperationCanceledException){}
        catch(ObjectDisposedException){}
        catch(Exception){lock(stateGate)message="Не удалось сохранить проверку сайтов. Следующая попытка — через 30 секунд.";}
    }

    async Task<WebsiteMonitorCheck> ProbeAsync(WebsiteMonitorSite site,CancellationToken cancellation)
    {
        var status="unknown";int? httpStatus=null;double? responseMs=null;string localMessage;
        var watch=Stopwatch.StartNew();
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        {
            try
            {
                using var request=new HttpRequestMessage(HttpMethod.Get,site.Url);
                using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false);
                responseMs=Math.Round(watch.Elapsed.TotalMilliseconds,1);httpStatus=(int)response.StatusCode;
                status=httpStatus switch{>=200 and <=399=>"up",>=400 and <=499=>"restricted",>=500 and <=599=>"down",_=>"unknown"};
                localMessage="HTTP "+httpStatus+" с этого ПК.";
            }
            catch(OperationCanceledException) when(!stopping.IsCancellationRequested){localMessage="Проверка с этого ПК не завершилась за 8 секунд.";}
            catch(HttpRequestException){localMessage="Сетевой запрос с этого ПК не удался.";}
        }
        stopping.ThrowIfCancellationRequested();
        var report=await ReadReportSourceAsync(site,timeout.Token).ConfigureAwait(false);
        return new(DateTimeOffset.UtcNow,status,httpStatus,responseMs,localMessage+" "+report,"unknown",null);
    }

    async Task<string> ReadReportSourceAsync(WebsiteMonitorSite site,CancellationToken cancellation)
    {
        var source=new Uri(site.ReportUrl);var domain=new Uri(site.Url).IdnHost;
        var cacheKey=domain+"|"+source.AbsoluteUri;
        var now=DateTimeOffset.UtcNow;
        if(reportCache.TryGetValue(cacheKey,out var cache)&&now-cache.CheckedAt<TimeSpan.FromMinutes(1))return cache.Message;
        var result="Сведения о жалобах не подтверждены; откройте источник.";
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,source);
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false);
            if(response.IsSuccessStatusCode)
            {
                if(response.Content.Headers.ContentLength is >512*1024)result="Источник жалоб вернул слишком большой ответ.";
                else
                {
                    await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                    using var body=new MemoryStream();var buffer=new byte[8192];int read;
                    while((read=await stream.ReadAsync(buffer,timeout.Token).ConfigureAwait(false))>0)
                    {
                        if(body.Length+read>512*1024){result="Источник жалоб вернул слишком большой ответ.";break;}
                        body.Write(buffer,0,read);
                    }
                    var html=Encoding.UTF8.GetString(body.GetBuffer(),0,checked((int)body.Length));
                    if(html.Contains("captcha",StringComparison.OrdinalIgnoreCase)||html.Contains("user verification",StringComparison.OrdinalIgnoreCase))
                        result="Источник жалоб требует проверки в браузере.";
                    // Public pages observed on this host are blocked, protected, or ambiguous.
                    // HTTP 200 and complaint-like prose are insufficient evidence for a count.
                }
            }
            else result="Источник жалоб недоступен (HTTP "+(int)response.StatusCode+").";
        }
        catch(OperationCanceledException) when(!stopping.IsCancellationRequested){result="Источник жалоб не ответил за 8 секунд.";}
        catch(HttpRequestException){result="Источник жалоб недоступен по сети.";}
        catch(IOException){result="Не удалось прочитать источник жалоб.";}
        reportCache[cacheKey]=(DateTimeOffset.UtcNow,result);
        if(reportCache.Count>40)
            foreach(var key in reportCache.OrderBy(x=>x.Value.CheckedAt).Take(reportCache.Count-40).Select(x=>x.Key).ToArray())reportCache.Remove(key);
        return result;
    }

    public void Dispose()
    {
        if(disposed)return;disposed=true;timer.Dispose();lifetime.Cancel();client.Dispose();lifetime.Dispose();
    }
}

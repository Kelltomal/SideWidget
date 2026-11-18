using System.Net;
using System.Text.Json;
using SideWidget.Core;
using SideWidget.Infrastructure;

static class WebsiteMonitorChecks
{
    public static void Run()=>RunAsync().GetAwaiter().GetResult();
    static async Task RunAsync()
    {
        void Check(bool condition,string name){if(!condition)throw new Exception("Website monitor: "+name);}
        var directory=Path.Combine(Path.GetTempPath(),"sidewidget-websites-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store=new Store(directory);var handler=new WebsiteHandler();
            JsonElement Snapshot(WebsiteMonitorService monitor)=>JsonSerializer.SerializeToElement(monitor.Snapshot(),Protocol.Json);
            using(var service=new WebsiteMonitorService(store,handler))
            {
                await service.SaveAsync(new("up","https://up.example/","",true,5));
                await service.SaveAsync(new("restricted","https://restricted.example/","",true,1));
                await service.SaveAsync(new("down","https://down.example/","",true,1440));
                await service.SaveAsync(new("redirect","https://redirect.example/","",true,5));
                await service.CheckAsync();
                var sites=Snapshot(service).GetProperty("sites").EnumerateArray().ToDictionary(x=>x.GetProperty("id").GetString()!);
                Check(sites["up"].GetProperty("lastCheck").GetProperty("status").GetString()=="up","2xx available from local PC");
                Check(sites["redirect"].GetProperty("lastCheck").GetProperty("status").GetString()=="up","3xx confirms HTTP reachability");
                Check(sites["restricted"].GetProperty("lastCheck").GetProperty("status").GetString()=="restricted","403 never global outage");
                Check(sites["down"].GetProperty("lastCheck").GetProperty("status").GetString()=="down","5xx local probe failure");
                Check(sites["up"].GetProperty("lastCheck").GetProperty("reportStatus").GetString()=="unknown","captcha cannot report no complaints");
                Check(sites["up"].GetProperty("lastCheck").GetProperty("reportCount").ValueKind==JsonValueKind.Null,"ambiguous HTML cannot invent count");
                foreach(var interval in new[]{0,1441})
                {
                    var rejected=false;try{await service.SaveAsync(new("bad","https://example.com","",true,interval));}catch(CommandFault){rejected=true;}
                    Check(rejected,"invalid interval rejected");
                }
                foreach(var url in new[]{"file:///tmp/file","https://user:password@example.com/","https://example.com/#part"})
                {
                    var rejected=false;try{await service.SaveAsync(new("bad",url,"",true,5));}catch(CommandFault){rejected=true;}
                    Check(rejected,"unsafe URL rejected");
                }
                for(var i=0;i<26;i++)await service.CheckAsync("up");
                Check(Snapshot(service).GetProperty("sites")[0].GetProperty("history").GetArrayLength()==24,"history bounded to24");
                Check(handler.ReportRequests==4,"report source cached once per target domain per minute");
                handler.Block=true;var first=service.CheckAsync("up");await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
                await service.CheckAsync("up");
                await service.SaveAsync(new("up","https://edited.example/","",true,5)).WaitAsync(TimeSpan.FromSeconds(1));
                handler.Release.TrySetResult();await first;
                Check(Snapshot(service).GetProperty("sites")[0].GetProperty("history").GetArrayLength()==0,"inflight probe cannot overwrite edited target");
                await service.SaveAsync(new("up","https://up.example/","",true,5));handler.Block=false;
                for(var i=0;i<24;i++)await service.CheckAsync("up");
                Check(handler.MaximumActive==1,"concurrent checks never overlap");
                for(var i=4;i<20;i++)await service.SaveAsync(new("site"+i,"https://site"+i+".example/","",false,5));
                var capped=false;try{await service.SaveAsync(new("overflow","https://overflow.example/","",true,5));}catch(CommandFault){capped=true;}
                Check(capped,"site limit20 enforced");
            }
            using(var persisted=new WebsiteMonitorService(store,new WebsiteHandler()))
                Check(Snapshot(persisted).GetProperty("sites")[0].GetProperty("history").GetArrayLength()==24,"full bounded history survives restart");
            var delayedHandler=new WebsiteHandler{Block=true};
            using(var delayed=new WebsiteMonitorService(store,delayedHandler))
            {
                var inFlight=delayed.CheckAsync("up");
                await delayedHandler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
                await delayed.DeleteAsync("up").WaitAsync(TimeSpan.FromSeconds(1));
                delayedHandler.Release.TrySetResult();await inFlight;
                Check(!Snapshot(delayed).GetProperty("sites").EnumerateArray().Any(x=>x.GetProperty("id").GetString()=="up"),"deleted target never resurrected");
            }
            using(var restored=new WebsiteMonitorService(store,new WebsiteHandler()))
            {
                var sites=Snapshot(restored).GetProperty("sites");
                Check(sites.GetArrayLength()==19&&sites[0].GetProperty("history").GetArrayLength()==1,"targets and history survive restart and inflight deletion");
                Check(sites[0].GetProperty("reportUrl").GetString()=="https://downradar.ru/ne-rabotaet/restricted.example","default report link persists");
                await restored.DeleteAsync("restricted");Check(Snapshot(restored).GetProperty("sites").GetArrayLength()==18,"delete persists");
            }
            Console.WriteLine("PASS Website monitor: HTTP classification, unknown public reports, validation, bounded history, cache, persistence and nonoverlap");
        }
        finally {Directory.Delete(directory,true);}
    }

    sealed class WebsiteHandler:HttpMessageHandler
    {
        public int ReportRequests;public bool Block;public int MaximumActive;int active;
        public TaskCompletionSource Started {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            var count=Interlocked.Increment(ref active);MaximumActive=Math.Max(MaximumActive,count);
            try
            {
                if(request.RequestUri!.Host=="downradar.ru")
                {ReportRequests++;return new(HttpStatusCode.OK){Content=new StringContent("<html><title>captcha verification</title><p>0 reports</p></html>")};}
                if(Block){Started.TrySetResult();await Release.Task.WaitAsync(cancellation);}
                return new(request.RequestUri.Host switch{"restricted.example"=>HttpStatusCode.Forbidden,"down.example"=>HttpStatusCode.ServiceUnavailable,"redirect.example"=>HttpStatusCode.Redirect,_=>HttpStatusCode.OK});
            }
            finally {Interlocked.Decrement(ref active);}
        }
    }
}

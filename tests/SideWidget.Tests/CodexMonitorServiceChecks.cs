using System.Text.Json;
using SideWidget.App;
using SideWidget.Core;
using SideWidget.Infrastructure;

static class CodexMonitorServiceChecks
{
    public static async Task Run()
    {
        var directory=Path.Combine(Path.GetTempPath(),"sidewidget-watch-"+Guid.NewGuid().ToString("N"));
        try{
            var store=new Store(directory);var status="running";var limitCalls=0;var now=DateTimeOffset.UtcNow;
            var queued=false;
            CodexThreadsSnapshot Read()=>new(true,now,[new("chat","Test",status,"turn",now,queued)]);
            Task<CodexLimitsSnapshot> Limits(CancellationToken token){limitCalls++;return Task.FromResult(new CodexLimitsSnapshot(true,now,[new("codex",null,new(100,300,now.AddHours(1)),null)]));}
            using(var monitor=new CodexMonitorService(store,Read,Limits,()=>"boot-a",false)){
                await monitor.RefreshAsync(true);var power=new PowerSchedule();monitor.Arm("selected",["chat"],power);
                status="completed";await monitor.RefreshForPowerAsync();monitor.Update(now,power);
                if(power.State is null||limitCalls!=1)throw new Exception("Chat completion revalidation must not fetch quota");
                var due=power.State;
                queued=true;
                if(await monitor.RevalidateDueAsync(power,due)||power.State is not null)
                    throw new Exception("Fresh queue check must veto a previously authorized countdown");
                monitor.Update(DateTimeOffset.UtcNow,power);
                if(power.State is not null)throw new Exception("Older full snapshot must not restore the cancelled countdown");
                queued=false;
                status="running";await monitor.RefreshForPowerAsync();monitor.Update(now,power);
                if(power.State is not null)throw new Exception("Resumed chat must cancel countdown");
                monitor.Arm("limit",[],power);
                if(power.State is null)throw new Exception("Exhaustion starts countdown");
                monitor.Cancel(power);
            }
            using(var restarted=new CodexMonitorService(store,Read,Limits,()=>"boot-a",false)){
                var state=JsonSerializer.SerializeToElement(restarted.Snapshot(),Protocol.Json);
                if(!state.GetProperty("limitUsedThisBoot").GetBoolean())throw new Exception("Exhaustion claim must survive application restart and cancel");
            }
            using(var nextBoot=new CodexMonitorService(store,Read,Limits,()=>"boot-b",false)){
                if(JsonSerializer.SerializeToElement(nextBoot.Snapshot(),Protocol.Json).GetProperty("limitUsedThisBoot").GetBoolean())throw new Exception("New boot must permit new explicit arming");
            }
            Task<CodexLimitsSnapshot> SlowQuota(CancellationToken token){status="running";return Limits(token);}
            status="completed";
            using(var ordered=new CodexMonitorService(store,Read,SlowQuota,()=>"boot-b",false)){
                await ordered.RefreshAsync(true);
                var state=JsonSerializer.SerializeToElement(ordered.Snapshot(),Protocol.Json);
                if(state.GetProperty("threads")[0].GetProperty("status").GetString()!="running")throw new Exception("Thread snapshot must follow account request");
            }
            Console.WriteLine("PASS Codex monitor: relevant-source revalidation, resumed work, durable boot claim and refresh ordering");
        }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}

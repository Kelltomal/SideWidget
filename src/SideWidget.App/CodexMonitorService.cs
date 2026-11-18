using SideWidget.Core;
using SideWidget.Infrastructure;

namespace SideWidget.App;

sealed class CodexMonitorService : IDisposable
{
    const string UsedKey="codex.limitShutdown.boot";
    readonly Func<CodexThreadsSnapshot> readThreads;
    readonly Func<CancellationToken,Task<CodexLimitsSnapshot>> readLimits;
    readonly bool autoPoll;
    readonly SemaphoreSlim refreshGate=new(1,1);
    readonly System.Threading.Timer poll;
    readonly CancellationTokenSource lifetime=new();
    readonly CodexShutdownPolicy policy;
    readonly string? bootId;
    CodexThreadsSnapshot threads=new(false,DateTimeOffset.MinValue,[],"Получаем данные Codex…");
    CodexLimitsSnapshot limits=new(false,DateTimeOffset.MinValue,[],"Получаем лимиты Codex…");
    PowerScheduleState? owned;
    string? error;
    DateTimeOffset lastLimitAttempt;
    bool disposed,started;
    public long Revision {get;private set;}
    public CodexMonitorService(Store store,Func<CodexThreadsSnapshot>? readThreads=null,Func<CancellationToken,Task<CodexLimitsSnapshot>>? readLimits=null,Func<string?>? readBoot=null,bool autoPoll=true)
    {
        var home=Environment.GetEnvironmentVariable("CODEX_HOME");
        if(string.IsNullOrWhiteSpace(home))home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex");
        var reader=new CodexStateReader(home);var limitReader=new CodexRateLimitsReader(home);
        this.readThreads=readThreads??(()=>reader.Read());this.readLimits=readLimits??(token=>limitReader.ReadAsync(token));this.autoPoll=autoPoll;
        bootId=readBoot is null?WindowsBootSession.Read():readBoot();
        policy=new(bootId is not null&&store.Read<string?>(UsedKey,null)==bootId,()=>{
            if(bootId is null)throw new CommandFault("codex","Не удалось определить текущую сессию ПК. Выключение по лимиту недоступно.");
            store.Save(UsedKey,bootId);
        });
        poll=new(async _=>await RefreshAsync(false),null,Timeout.Infinite,Timeout.Infinite);
    }
    public void Wake(){if(started||disposed)return;started=true;if(autoPoll)poll.Change(TimeSpan.Zero,TimeSpan.FromSeconds(5));}
    public object Snapshot(){Wake();return new{threads.Available,threads.CheckedAt,threads.Threads,message=error??threads.Message,limits,shutdown=policy.State,policy.LimitUsedThisBoot};}
    public async Task RefreshAsync(bool forceLimits)
    {
        if(disposed)return;
        if(forceLimits)await refreshGate.WaitAsync();else if(!await refreshGate.WaitAsync(0))return;
        try{
            if(disposed)return;
            if(forceLimits||DateTimeOffset.UtcNow-lastLimitAttempt>TimeSpan.FromSeconds(60)){
                lastLimitAttempt=DateTimeOffset.UtcNow;
                limits=await readLimits(lifetime.Token);
            }
            threads=await Task.Run(readThreads); // Chat state must be newer than the slower account request.
        }catch(Exception e) when(e is not OutOfMemoryException){threads=new(false,DateTimeOffset.UtcNow,[],"Не удалось прочитать состояние Codex. Повторите обновление.");}
        finally{refreshGate.Release();}
    }
    public async Task RefreshForPowerAsync()
    {
        if(policy.State?.Mode!="chats"){await RefreshAsync(true);return;}
        await refreshGate.WaitAsync();
        try{if(!disposed)threads=await Task.Run(readThreads);}
        catch(Exception e) when(e is not OutOfMemoryException){threads=new(false,DateTimeOffset.UtcNow,[],"Не удалось прочитать состояние Codex. Повторите обновление.");}
        finally{refreshGate.Release();}
    }
    public void Arm(string mode,string[] ids,PowerSchedule power)
    {
        Wake();
        if(mode=="limit"){
            if(bootId is null)throw new CommandFault("codex","Не удалось определить текущую сессию ПК. Выключение по лимиту недоступно.");
            policy.ArmLimit();
        }else{
            var chosen=mode=="all"?threads.Threads.Where(t=>t.Status=="running"||t.HasQueuedInput).Select(t=>t.Id).ToArray():ids;
            policy.ArmChats(threads,chosen);
        }
        power.Cancel();owned=null;error=null;Revision++;Update(DateTimeOffset.UtcNow,power);
    }
    public void Cancel(PowerSchedule power){if(Owns(power.State))power.Cancel();owned=null;policy.Cancel();error=null;Revision++;}
    public bool Owns(PowerScheduleState? state)=>state is not null&&ReferenceEquals(owned,state);
    public bool CanProceed(DateTimeOffset now)=>policy.CanProceed(threads,limits,now);
    public void Update(DateTimeOffset now,PowerSchedule power)
    {
        try{policy.Update(threads,limits,now);error=null;}
        catch(Exception e) when(e is not OutOfMemoryException){error="Не удалось сохранить одноразовое выключение. Проверьте свободное место.";policy.Cancel();}
        var deadline=policy.State?.Deadline;
        if(deadline is null){if(Owns(power.State))power.Cancel();owned=null;return;}
        if(Owns(power.State)&&owned!.Deadline==deadline)return;
        power.ScheduleDeadline("shutdown",deadline.Value);owned=power.State;
    }
    public void Complete(){owned=null;policy.Cancel();Revision++;}
    public void Dispose(){disposed=true;poll.Dispose();lifetime.Cancel();policy.Cancel();}
}

using SideWidget.Core;
namespace SideWidget.Infrastructure;

public sealed record FocusTimerEntry(string Id,FocusTimerState State);

public sealed class FocusTimerService
{
    const string Key="personal.focusTimers.v2";
    readonly Action<IReadOnlyList<FocusTimerEntry>> save;
    bool pendingSave;
    public IReadOnlyList<FocusTimerEntry> Items {get;private set;}
    public FocusTimerEntry? Primary=>Items.OrderBy(item=>item.State.Status=="ringing"?0:item.State.Status=="running"?1:2)
        .ThenBy(item=>item.State.Deadline??DateTimeOffset.MaxValue).FirstOrDefault();
    public FocusTimerState State=>Primary?.State??new();
    public FocusTimerService(Store store):this(store.Read<FocusTimerEntry[]?>(Key,null)
        ??Migrate(store.Read("personal.focusTimer.v1",new FocusTimerState())),value=>store.Save(Key,value)){}
    static FocusTimerEntry[] Migrate(FocusTimerState state)=>state.Status=="idle"?[]:[new("legacy",state)];
    public FocusTimerService(FocusTimerState state,Action<FocusTimerState> save):this(Migrate(state),items=>save(items.FirstOrDefault()?.State??new())){}
    public FocusTimerService(IEnumerable<FocusTimerEntry> items,Action<IReadOnlyList<FocusTimerEntry>> save)
    {
        this.save=save;
        Items=items.Where(item=>item is not null&&!string.IsNullOrEmpty(item.Id)&&item.State is not null)
            .DistinctBy(item=>item.Id).Select(item=>item with{State=new FocusTimer(item.State).State})
            .Where(item=>item.State.Status!="idle").ToArray();
    }
    // Expiry is derived from the already durable deadline, so a full disk cannot mute the signal.
    public void Tick(DateTimeOffset now)
    {
        var next=Items.Select(item=>{var timer=new FocusTimer(item.State);timer.Tick(now);return item with{State=timer.State};}).ToArray();
        if(!next.SequenceEqual(Items)){Items=next;pendingSave=true;}
        if(pendingSave){save(Items);pendingSave=false;}
    }
    // User actions persist first. Failed writes retain the current session for a safe retry.
    public void Add(Action<FocusTimer> start)
    {
        if(Items.Count>=100)throw new CommandFault("limit","Можно создать до 100 отсчётов. Удалите ненужные.");
        var timer=new FocusTimer();start(timer);
        Commit([..Items,new(Guid.NewGuid().ToString("N"),timer.State)]);
    }
    void Commit(FocusTimerEntry[] next){save(next);Items=next;pendingSave=false;}
    public bool Change(Action<FocusTimer> action,string? id=null)
    {
        var entry=id is null?Primary:Items.FirstOrDefault(item=>item.Id==id);
        if(entry is null){if(id is not null)throw new CommandFault("timer","Отсчёт уже удалён");return false;}
        var timer=new FocusTimer(entry.State);action(timer);if(timer.State==entry.State)return false;
        Commit(Items.Select(item=>item.Id==entry.Id?item with{State=timer.State}:item).Where(item=>item.State.Status!="idle").ToArray());
        return true;
    }
    public int Remaining(DateTimeOffset now)=>new FocusTimer(State).Remaining(now);
}

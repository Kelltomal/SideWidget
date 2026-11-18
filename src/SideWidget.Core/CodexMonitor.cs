namespace SideWidget.Core;

public sealed record CodexThreadInfo(string Id,string Title,string Status,string? TurnId,DateTimeOffset? UpdatedAt,bool HasQueuedInput=false);
public sealed record CodexThreadsSnapshot(bool Available,DateTimeOffset CheckedAt,IReadOnlyList<CodexThreadInfo> Threads,string? Message=null);
public sealed record CodexLimitWindow(double UsedPercent,int WindowMinutes,DateTimeOffset? ResetsAt);
public sealed record CodexLimitBucket(string Id,string? Name,CodexLimitWindow? Primary,CodexLimitWindow? Secondary);
public sealed record CodexLimitsSnapshot(bool Available,DateTimeOffset CheckedAt,IReadOnlyList<CodexLimitBucket> Buckets,string? Message=null);
public sealed record CodexShutdownState(string Mode,string Phase,string[] Ids,DateTimeOffset? Deadline);

public sealed class CodexShutdownPolicy(bool limitUsedThisBoot,Action markLimitUsed)
{
    public CodexShutdownState? State {get;private set;}
    public bool LimitUsedThisBoot {get;private set;}=limitUsedThisBoot;
    public void ArmChats(CodexThreadsSnapshot snapshot,IEnumerable<string> ids)
    {
        var selected=ids.Distinct(StringComparer.Ordinal).ToArray();
        if(!snapshot.Available||selected.Length==0||selected.Length>500||selected.Any(id=>!snapshot.Threads.Any(t=>t.Id==id&&(t.Status=="running"||t.HasQueuedInput))))
            throw new CommandFault("codex","Выберите работающие чаты Codex");
        State=new("chats","waiting",selected,null);
    }
    public void ArmLimit()
    {
        if(LimitUsedThisBoot)throw new CommandFault("codex","Выключение по лимиту уже использовано в этой сессии ПК");
        State=new("limit","waiting",[],null);
    }
    public void Cancel()=>State=null;
    public bool CanProceed(CodexThreadsSnapshot threads,CodexLimitsSnapshot limits,DateTimeOffset now)
    {
        if(State is null)return false;
        if(State.Mode=="chats")return threads.Available&&Fresh(threads.CheckedAt,now,15)&&State.Ids.All(id=>threads.Threads.Any(t=>t.Id==id&&t.Status=="completed"&&!t.HasQueuedInput));
        if(!limits.Available||!Fresh(limits.CheckedAt,now,180))return false;
        var bucket=limits.Buckets.FirstOrDefault(b=>b.Id=="codex");
        return bucket is not null&&new[]{bucket.Primary,bucket.Secondary}.Any(w=>w is not null&&double.IsFinite(w.UsedPercent)&&w.UsedPercent>=100&&(w.ResetsAt is null||w.ResetsAt>now));
    }
    static bool Fresh(DateTimeOffset at,DateTimeOffset now,int seconds)=>at<=now.AddSeconds(5)&&at>=now.AddSeconds(-seconds);
    public void Update(CodexThreadsSnapshot threads,CodexLimitsSnapshot limits,DateTimeOffset now)
    {
        if(State is not {} state)return;
        if(!CanProceed(threads,limits,now)){
            if(state.Deadline is not null)State=state.Mode=="limit"?null:state with{Phase="waiting",Deadline=null};
            return;
        }
        if(state.Deadline is not null)return;
        if(state.Mode=="limit"){
            // Durable claim comes before any countdown, including when quota is already zero.
            markLimitUsed();LimitUsedThisBoot=true;
        }
        State=state with{Phase="countdown",Deadline=now.AddSeconds(60)};
    }
}

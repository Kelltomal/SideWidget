namespace SideWidget.Core;

public sealed record CodexThreadInfo(string Id,string Title,string Status,string? TurnId,DateTimeOffset? UpdatedAt,bool HasQueuedInput=false);
public sealed record CodexThreadsSnapshot(bool Available,DateTimeOffset CheckedAt,IReadOnlyList<CodexThreadInfo> Threads,string? Message=null);
public sealed record CodexLimitWindow(double UsedPercent,int WindowMinutes,DateTimeOffset? ResetsAt);
public sealed record CodexLimitBucket(string Id,string? Name,CodexLimitWindow? Primary,CodexLimitWindow? Secondary);
public sealed record CodexLimitsSnapshot(bool Available,DateTimeOffset CheckedAt,IReadOnlyList<CodexLimitBucket> Buckets,string? Message=null);
public sealed record CodexShutdownState(string Mode,string Phase,string[] Ids,DateTimeOffset? Deadline);

public sealed class CodexShutdownPolicy(bool limitUsedThisBoot,Action markLimitUsed)
{
    string[]? countdownTurns;
    public CodexShutdownState? State {get;private set;}
    public bool LimitUsedThisBoot {get;private set;}=limitUsedThisBoot;
    public void ArmChats(CodexThreadsSnapshot snapshot,IEnumerable<string> ids)
    {
        var selected=ids.Distinct(StringComparer.Ordinal).ToArray();
        if(!snapshot.Available||selected.Length==0||selected.Length>500||selected.Any(id=>!snapshot.Threads.Any(t=>t.Id==id&&(t.Status=="running"||t.HasQueuedInput))))
            throw new CommandFault("codex","Выберите работающие чаты Codex");
        State=new("chats","waiting",selected,null);
        countdownTurns=null;
    }
    public void ArmLimit()
    {
        if(LimitUsedThisBoot)throw new CommandFault("codex","Выключение по лимиту уже использовано в этой сессии ПК");
        State=new("limit","waiting",[],null);
        countdownTurns=null;
    }
    public void Cancel(){State=null;countdownTurns=null;}
    public bool CanProceed(CodexThreadsSnapshot threads,CodexLimitsSnapshot limits,DateTimeOffset now)
    {
        if(State is null)return false;
        if(State.Mode=="chats")return CompletedTurns(State,threads,now) is {} turns&&
            (State.Deadline is null||countdownTurns is not null&&turns.SequenceEqual(countdownTurns));
        if(!limits.Available||!Fresh(limits.CheckedAt,now,180))return false;
        var bucket=limits.Buckets.FirstOrDefault(b=>b.Id=="codex");
        return bucket is not null&&new[]{bucket.Primary,bucket.Secondary}.Any(w=>w is not null&&double.IsFinite(w.UsedPercent)&&w.UsedPercent>=100&&(w.ResetsAt is null||w.ResetsAt>now));
    }
    static bool Fresh(DateTimeOffset at,DateTimeOffset now,int seconds)=>at<=now.AddSeconds(5)&&at>=now.AddSeconds(-seconds);
    static string[]? CompletedTurns(CodexShutdownState state,CodexThreadsSnapshot snapshot,DateTimeOffset now)
    {
        if(!snapshot.Available||!Fresh(snapshot.CheckedAt,now,15))return null;
        var turns=new string[state.Ids.Length];
        for(var i=0;i<turns.Length;i++)
        {
            var thread=snapshot.Threads.FirstOrDefault(t=>t.Id==state.Ids[i]);
            if(thread is not {Status:"completed",HasQueuedInput:false,TurnId:{Length:>0} turn})return null;
            turns[i]=turn;
        }
        return turns;
    }
    public void Update(CodexThreadsSnapshot threads,CodexLimitsSnapshot limits,DateTimeOffset now)
    {
        if(State is not {} state)return;
        if(state.Mode=="chats")
        {
            var turns=CompletedTurns(state,threads,now);
            if(turns is null)State=state with{Phase="waiting",Deadline=null};
            else if(state.Deadline is null||countdownTurns is null||!turns.SequenceEqual(countdownTurns))
                State=state with{Phase="countdown",Deadline=now.AddSeconds(60)};
            countdownTurns=turns;
            return;
        }
        if(!CanProceed(threads,limits,now)){
            if(state.Deadline is not null)State=null;
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

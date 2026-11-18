using SideWidget.Core;

static class CodexPolicyChecks
{
    public static void Run()
    {
        var now=DateTimeOffset.UtcNow;var used=false;
        var policy=new CodexShutdownPolicy(false,()=>used=true);
        CodexThreadInfo Thread(string status,bool queued=false)=>new("chat","Test chat",status,"turn",now,queued);
        CodexThreadsSnapshot Snapshot(string status,bool queued=false)=>new(true,now,[Thread(status,queued)]);
        var none=new CodexLimitsSnapshot(false,now,[]);
        void Check(bool condition,string name){if(!condition)throw new Exception("Codex policy: "+name);}
        policy.ArmChats(Snapshot("running"),["chat"]);
        foreach(var status in new[]{"running","failed","interrupted","unknown"}){
            policy.Update(Snapshot(status),none,now);Check(policy.State?.Deadline==null,status+" must not shutdown");
        }
        policy.Update(Snapshot("completed",true),none,now);Check(policy.State?.Deadline==null,"queued input prevents shutdown");
        policy.Update(Snapshot("completed"),none,now);Check(policy.State?.Deadline==now.AddSeconds(60),"successful completion starts grace");
        policy.Update(Snapshot("running"),none,now.AddSeconds(1));Check(policy.State?.Deadline==null,"new work cancels countdown");
        policy.Update(new(false,now,[]),none,now);Check(policy.State?.Deadline==null,"unavailable never complete");
        policy.Cancel();Check(policy.State==null,"manual cancellation");
        var exhausted=new CodexLimitsSnapshot(true,now,[new("codex",null,new(100,300,now.AddHours(1)),null)]);
        policy.ArmLimit();policy.Update(Snapshot("running"),exhausted,now);
        Check(used&&policy.LimitUsedThisBoot&&policy.State?.Deadline==now.AddSeconds(60),"zero quota consumed before countdown");
        policy.Cancel();var rejected=false;try{policy.ArmLimit();}catch(CommandFault){rejected=true;}
        Check(rejected,"cannot rearm exhaustion this boot after cancellation");
        var restarted=new CodexShutdownPolicy(used,()=>{});rejected=false;try{restarted.ArmLimit();}catch(CommandFault){rejected=true;}
        Check(rejected,"marker survives app restart");
        var nextBoot=new CodexShutdownPolicy(false,()=>{});nextBoot.ArmLimit();
        nextBoot.Update(Snapshot("running"),exhausted with{CheckedAt=now.AddMinutes(-10)},now);
        Check(nextBoot.State?.Deadline==null,"stale limits cannot shutdown");
        var otherBucket=exhausted with{Buckets=[new("other",null,new(100,300,now.AddHours(1)),null),new("codex",null,new(10,300,now.AddHours(1)),null)]};
        nextBoot.Update(Snapshot("running"),otherBucket,now);Check(nextBoot.State?.Deadline==null,"unrelated bucket does not shutdown");
        Console.WriteLine("PASS Codex shutdown policy: completion, queue, stale data, boot-once limit and cancellation");
    }
}

namespace SideWidget.Core;

public sealed record FocusTimerState(string Mode="timer",string Status="idle",string Phase="work",int Cycle=0,
    DateTimeOffset? Deadline=null,int RemainingSeconds=0,int DurationSeconds=0,string AlarmTime="09:00",
    int WorkMinutes=25,int BreakMinutes=5,int LongBreakMinutes=15);

// A single deadline is authoritative: hidden WebView, sleep and restart cannot lose elapsed time.
public sealed class FocusTimer
{
    public FocusTimerState State {get;private set;}
    public FocusTimer(FocusTimerState? state=null)
    {
        State=state??new();
        if(State.Mode is not ("timer" or "alarm" or "pomodoro")||State.Status is not ("idle" or "ready" or "running" or "paused" or "ringing")||State.Phase is not ("work" or "break" or "longBreak")||State.RemainingSeconds is <0 or >90000||State.DurationSeconds is <0 or >90000||State.Cycle is <0 or >4||State.WorkMinutes is <1 or >180||State.BreakMinutes is <1 or >180||State.LongBreakMinutes is <1 or >180||(State.Status=="running"&&State.Deadline==null))State=new();
    }
    public int Remaining(DateTimeOffset now)=>State.Status=="running"&&State.Deadline is {} deadline
        ?(int)Math.Clamp(Math.Ceiling((deadline-now).TotalSeconds),0,90000):State.RemainingSeconds;
    public void Start(string mode,int seconds,string? alarmTime,DateTimeOffset now,int workMinutes=25,int breakMinutes=5,int longBreakMinutes=15,TimeZoneInfo? timeZone=null)
    {
        if(mode is not ("timer" or "alarm" or "pomodoro"))throw new CommandFault("timer","Выберите режим таймера");
        if(mode=="pomodoro"&&(workMinutes is <1 or >180||breakMinutes is <1 or >180||longBreakMinutes is <1 or >180))throw new CommandFault("timer","Длительность этапа: от 1 до 180 минут");
        DateTimeOffset deadline;
        if(mode=="alarm"){
            if(!TimeOnly.TryParseExact(alarmTime,"HH:mm",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var time))throw new CommandFault("timer","Укажите время будильника в формате ЧЧ:ММ");
            deadline=NextAlarm(time,now,timeZone??TimeZoneInfo.Local);
            seconds=(int)Math.Ceiling((deadline-now).TotalSeconds);
        }else{
            if(mode=="pomodoro")seconds=workMinutes*60;
            if(seconds is <1 or >86400)throw new CommandFault("timer","Длительность таймера: от 1 секунды до 24 часов");
            deadline=now.AddSeconds(seconds);
        }
        if(mode!="pomodoro"){workMinutes=25;breakMinutes=5;longBreakMinutes=15;}
        State=new(mode,"running","work",0,deadline,seconds,seconds,alarmTime??State.AlarmTime,workMinutes,breakMinutes,longBreakMinutes);
    }
    static DateTimeOffset NextAlarm(TimeOnly time,DateTimeOffset now,TimeZoneInfo zone)
    {
        var date=TimeZoneInfo.ConvertTime(now,zone).Date;
        for(var day=0;day<3;day++){
            var local=DateTime.SpecifyKind(date.AddDays(day).Add(time.ToTimeSpan()),DateTimeKind.Unspecified);
            // Match JavaScript Date: shift a skipped local time by the offset gap.
            if(zone.IsInvalidTime(local)){
                var before=local;var after=local;
                while(zone.IsInvalidTime(before))before=before.AddMinutes(-1);
                while(zone.IsInvalidTime(after))after=after.AddMinutes(1);
                local=local.Add(zone.GetUtcOffset(after)-zone.GetUtcOffset(before));
            }
            var offsets=zone.IsAmbiguousTime(local)?zone.GetAmbiguousTimeOffsets(local):[zone.GetUtcOffset(local)];
            foreach(var offset in offsets.OrderByDescending(value=>value)){
                var candidate=new DateTimeOffset(local,offset);if(candidate>now)return candidate;
            }
        }
        throw new CommandFault("timer","Не удалось определить время будильника");
    }
    public bool Tick(DateTimeOffset now)
    {
        if(State.Status!="running"||Remaining(now)>0)return false;
        State=State with{Status="ringing",RemainingSeconds=0,Deadline=null};return true;
    }
    public void Pause(DateTimeOffset now)
    {
        Tick(now);if(State.Status!="running"||State.Mode=="alarm")return;
        State=State with{Status="paused",RemainingSeconds=Remaining(now),Deadline=null};
    }
    public void Resume(DateTimeOffset now)
    {
        if(State.Status is not ("paused" or "ready"))return;
        State=State with{Status="running",Deadline=now.AddSeconds(State.RemainingSeconds)};
    }
    public void Reset()=>State=State with{Status="idle",Deadline=null,RemainingSeconds=0,Cycle=0,Phase="work"};
    public void Dismiss()
    {
        if(State.Status!="ringing")return;
        if(State.Mode!="pomodoro"){Reset();return;}
        var cycle=State.Phase=="work"?State.Cycle+1:State.Cycle;
        var phase=State.Phase=="work"?(cycle==4?"longBreak":"break"):"work";
        if(State.Phase=="longBreak")cycle=0;
        var seconds=(phase=="work"?State.WorkMinutes:phase=="break"?State.BreakMinutes:State.LongBreakMinutes)*60;
        State=State with{Status="ready",Phase=phase,Cycle=cycle,RemainingSeconds=seconds,DurationSeconds=seconds,Deadline=null};
    }
    public static string Format(int seconds)=>seconds>=3600?$"{seconds/3600}:{seconds/60%60:00}:{seconds%60:00}":$"{seconds/60:00}:{seconds%60:00}";
}


namespace SideWidget.Core;

public sealed record PowerScheduleState(string Action,DateTimeOffset Deadline);

// Owned by the desktop session. Exiting SideWidget cancels the schedule.
public sealed class PowerSchedule
{
    public PowerScheduleState? State {get;private set;}
    public static int Validate(string action,int hours,int minutes)
    {
        if(action is not ("shutdown" or "restart"))throw new CommandFault("power","Выберите выключение или перезагрузку");
        if(hours<0||hours>24||minutes<0||minutes>59||(hours==24&&minutes!=0))
            throw new CommandFault("power","Задержка: до 24 часов, минуты — от 0 до 59");
        var seconds=hours*3600+minutes*60;
        if(seconds==0)throw new CommandFault("power","Укажите хотя бы 1 минуту");
        return seconds;
    }
    public void Schedule(string action,int hours,int minutes,DateTimeOffset now)
        =>State=new(action,now.AddSeconds(Validate(action,hours,minutes)));
    public void Cancel()=>State=null;
    public void ScheduleDeadline(string action,DateTimeOffset deadline){_=Validate(action,0,1);State=new(action,deadline);}
    public int Remaining(DateTimeOffset now)=>State is {} state?Math.Max(0,(int)Math.Ceiling((state.Deadline-now).TotalSeconds)):0;
    public void Tick(DateTimeOffset now,Action<string> execute)
    {
        if(State is not {} state||state.Deadline>now)return;
        State=null; // Claim before execution: even a failure must never repeat.
        execute(state.Action);
    }
}

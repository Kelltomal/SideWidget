using SideWidget.Core;

static class PowerScheduleChecks
{
    public static void Run()
    {
        var schedule=new PowerSchedule();
        var now=DateTimeOffset.Parse("2026-11-18T10:00:00Z");
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        void Reject(string action,int hours,int minutes){try{schedule.Schedule(action,hours,minutes,now);}catch(CommandFault){return;}throw new Exception("Invalid power schedule accepted");}
        Reject("shutdown",0,0);Reject("restart",-1,30);Reject("sleep",0,1);
        Reject("shutdown",1,60);Reject("shutdown",24,1);Reject("shutdown",int.MaxValue,1);
        schedule.Schedule("shutdown",0,30,now);
        Check(schedule.Remaining(now)==1800,"minutes-only delay");
        schedule.Schedule("restart",2,0,now);
        Check(schedule.Remaining(now)==7200,"hours-only delay replaces previous");
        schedule.Schedule("shutdown",1,30,now);
        Check(schedule.Remaining(now)==5400,"combined delay");
        var calls=new List<string>();
        schedule.Tick(now.AddMinutes(89),calls.Add);Check(calls.Count==0,"not early");
        schedule.Tick(now.AddHours(2),calls.Add);schedule.Tick(now.AddHours(3),calls.Add);
        Check(calls.SequenceEqual(new[]{"shutdown"})&&schedule.State==null,"late tick runs once");
        schedule.Schedule("restart",0,1,now);schedule.Cancel();schedule.Tick(now.AddHours(1),calls.Add);
        Check(calls.Count==1&&schedule.State==null,"cancel prevents execution");
        schedule.Schedule("restart",24,0,now);Check(schedule.Remaining(now)==86400,"24 hour boundary");
        Reject("restart",0,0);Check(schedule.Remaining(now)==86400,"invalid replacement retains schedule");
        try{schedule.Tick(now.AddDays(2),_=>throw new InvalidOperationException("test"));}catch(InvalidOperationException){}
        schedule.Tick(now.AddDays(3),calls.Add);Check(calls.Count==1&&schedule.State==null,"failed action never repeats");
        Console.WriteLine("Power schedule checks passed");
    }
}

using SideWidget.Core;
using SideWidget.Infrastructure;

static class CalendarChecks
{
    public static void Run()
    {
        var directory=Path.Combine(Path.GetTempPath(),"SideWidget-calendar-"+Guid.NewGuid().ToString("N"));
        try{
            var store=new Store(directory);var calendar=new CalendarService(store);
            var now=DateTimeOffset.Now;var start=now.AddMinutes(15);
            var entry=new CalendarEntry(Guid.NewGuid().ToString("N"),"Meeting",start.ToString("yyyy-MM-dd"),start.ToString("HH:mm"),"",false,"Notes",15,false);
            calendar.Save(entry);
            if(new CalendarService(store).Events.Single()!=entry)throw new Exception("Calendar persistence");
            if(calendar.Due(now.AddMinutes(-2)).Count!=0)throw new Exception("Reminder fired early");
            if(calendar.Due(now).Count!=1||calendar.Due(now).Count!=0||new CalendarService(store).Due(now).Count!=0)throw new Exception("Reminder must be durable and once-only");
            void Reject(CalendarEntry invalid){try{calendar.Save(invalid);}catch(CommandFault){return;}throw new Exception("Invalid event accepted");}
            Reject(entry with{Title=""});Reject(entry with{Date="2026-02-30"});Reject(entry with{Time="25:00"});Reject(entry with{ReminderMinutes=0});
            calendar.Save(entry with{Done=true});if(calendar.Due(now).Count!=0)throw new Exception("Done event reminder");
            calendar.Delete(entry.Id);if(new CalendarService(store).Events.Count!=0)throw new Exception("Calendar deletion");
            Console.WriteLine("PASS calendar: validation, durable events, once-only reminders and deletion");
        }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}

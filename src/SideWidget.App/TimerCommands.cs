using System.Text.Json;
using SideWidget.Core;
using SideWidget.Infrastructure;
namespace SideWidget.App;
sealed partial class Controller
{
    public FocusTimerService Timer {get;private set;}=null!;
    public void TickTimer()
    {
        TickCalendar();
        _=TickPowerSchedule();
        var before=Timer.Items;
        try{Timer.Tick(DateTimeOffset.Now);timerStorageError=false;}
        catch(Exception e) when(e is Microsoft.Data.Sqlite.SqliteException or IOException){
            // Keep the clock alive if storage fails; a later tick retries the transition.
            if(!timerStorageError){timerStorageError=true;Emit("notice",new{message=DesktopLanguage.Text("Не удалось сохранить таймер. Проверьте свободное место.")});}
        }finally{if(before!=Timer.Items)Emit("timer.changed",Timer.Items);}
    }
    bool timerStorageError;
    public IReadOnlyList<FocusTimerEntry> ChangeTimer(Action<FocusTimer> change,string? id=null)
    {
        if(Timer.Change(change,id)){timerStorageError=false;Emit("timer.changed",Timer.Items);}return Timer.Items;
    }
    object HandleTimer(Command command)
    {
        var p=command.Params;
        int Number(string key,int fallback){if(!p.TryGetProperty(key,out var value))return fallback;if(value.ValueKind!=JsonValueKind.Number||!value.TryGetInt32(out var result))throw new CommandFault("timer","Укажите целое число минут или секунд");return result;}
        var now=DateTimeOffset.Now;
        var id=p.TryGetProperty("id",out _)?Protocol.Text(p,"id",64):null;
        switch(command.ActionId){
            case "timer.get":TickTimer();return Timer.Items;
            case "timer.start":
                Timer.Add(timer=>timer.Start(Protocol.Text(p,"mode",16),Number("seconds",0),p.TryGetProperty("alarmTime",out _)?Protocol.Text(p,"alarmTime",5):null,now,Number("workMinutes",25),Number("breakMinutes",5),Number("longBreakMinutes",15)));
                timerStorageError=false;Emit("timer.changed",Timer.Items);return Timer.Items;
            case "timer.pause":return ChangeTimer(timer=>timer.Pause(now),id);
            case "timer.resume":return ChangeTimer(timer=>timer.Resume(now),id);
            case "timer.reset":return ChangeTimer(timer=>timer.Reset(),id);
            case "timer.dismiss":return ChangeTimer(timer=>timer.Dismiss(),id);
            default:throw new CommandFault("unknown_action","Действие не поддерживается");
        }
    }
}

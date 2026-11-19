using SideWidget.Core;

namespace SideWidget.App;

sealed partial class Controller
{
    public PowerSchedule PowerSchedule=>desktopState.PowerSchedule;
    void TickPowerSchedule()
    {
        var before=PowerSchedule.State;
        try{PowerSchedule.Tick(DateTimeOffset.Now,QuickActions.Power);}
        catch(Exception error){Emit("notice",new{message=DesktopLanguage.Text("Не удалось выполнить действие питания")+": "+error.Message});}
        finally{if(before!=PowerSchedule.State)Emit("power.changed",PowerSchedule.State!);}
    }
    async Task<object?> HandlePowerSchedule(Command command)
    {
        if(command.ActionId=="power.get")return PowerSchedule.State;
        if(command.ActionId=="power.cancel"){
            PowerSchedule.Cancel();Emit("power.changed",PowerSchedule.State!);return null;
        }
        var p=command.Params;
        int Number(string key){
            if(!p.TryGetProperty(key,out var value)||value.ValueKind!=System.Text.Json.JsonValueKind.Number||!value.TryGetInt32(out var number))
                throw new CommandFault("power","Укажите целое число часов и минут");
            return number;
        }
        var action=Protocol.Text(p,"action",20);var hours=Number("hours");var minutes=Number("minutes");
        var seconds=SideWidget.Core.PowerSchedule.Validate(action,hours,minutes);
        var deadline=DateTimeOffset.Now.AddSeconds(seconds).ToLocalTime().ToString("g");
        var caption=DesktopLanguage.Text(action=="restart"?"Перезагрузить через…":"Выключить через…");
        if(await Confirm(caption,deadline+"\n"+DesktopLanguage.Text("Сохраните документы. SideWidget должен оставаться запущен. Новый запуск заменит предыдущий."))){
            PowerSchedule.Schedule(action,hours,minutes,DateTimeOffset.Now);
            Emit("power.changed",PowerSchedule.State!);
        }
        return PowerSchedule.State;
    }
}

using SideWidget.Core;

namespace SideWidget.App;

sealed partial class Controller
{
    public PowerSchedule PowerSchedule=>desktopState.PowerSchedule;
    async Task TickPowerSchedule()
    {
        if(!desktopState.PowerExecution.Wait(0))return;
        var before=PowerSchedule.State;
        try{
            desktopState.Codex.Update(DateTimeOffset.UtcNow,PowerSchedule);
            if(PowerSchedule.State is {} due&&due.Deadline<=DateTimeOffset.UtcNow){
                var watched=desktopState.Codex.Owns(due);
                if(watched){
                    await desktopState.Codex.RefreshForPowerAsync();
                    desktopState.Codex.Update(DateTimeOffset.UtcNow,PowerSchedule);
                    if(!ReferenceEquals(PowerSchedule.State,due)||!desktopState.Codex.CanProceed(DateTimeOffset.UtcNow))return;
                }
                PowerSchedule.Tick(DateTimeOffset.Now,action=>{
                    if(watched)desktopState.Codex.Complete(); // Claim the watch even if Windows rejects the action.
                    QuickActions.Power(action);
                });
            }
        }
        catch(Exception error){Emit("notice",new{message=DesktopLanguage.Text("Не удалось выполнить действие питания")+": "+error.Message});}
        finally{if(before!=PowerSchedule.State)Emit("power.changed",PowerSchedule.State!);desktopState.PowerExecution.Release();}
    }
    async Task<object?> HandlePowerSchedule(Command command)
    {
        if(command.ActionId=="power.get")return PowerSchedule.State;
        if(command.ActionId=="power.cancel"){
            desktopState.Codex.Cancel(PowerSchedule);PowerSchedule.Cancel();Emit("power.changed",PowerSchedule.State!);return null;
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
            desktopState.Codex.Cancel(PowerSchedule);
            PowerSchedule.Schedule(action,hours,minutes,DateTimeOffset.Now);
            Emit("power.changed",PowerSchedule.State!);
        }
        return PowerSchedule.State;
    }
}

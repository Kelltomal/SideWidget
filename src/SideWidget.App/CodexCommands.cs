using System.Text.Json;
using SideWidget.Core;

namespace SideWidget.App;

sealed partial class Controller
{
    async Task<object> HandleCodex(Command command)
    {
        var monitor=desktopState.Codex;
        if(command.ActionId=="codex.get")return monitor.Snapshot();
        if(command.ActionId=="codex.refresh"){await monitor.RefreshAsync(true);return monitor.Snapshot();}
        if(command.ActionId=="codex.cancel"){
            monitor.Cancel(PowerSchedule);PowerSchedule.Cancel();Emit("power.changed",PowerSchedule.State!);return monitor.Snapshot();
        }
        var mode=Protocol.Text(command.Params,"mode",16);
        if(mode is not ("selected" or "all" or "limit"))throw new CommandFault("codex","Выберите условие выключения");
        string[] ids=[];
        if(command.Params.TryGetProperty("ids",out var value)){
            if(value.ValueKind!=JsonValueKind.Array||value.GetArrayLength()>500||value.EnumerateArray().Any(v=>v.ValueKind!=JsonValueKind.String||v.GetString() is not {Length:>0 and <=64}))
                throw new CommandFault("codex","Проверьте список чатов");
            ids=value.EnumerateArray().Select(v=>v.GetString()!).ToArray();
        }
        await monitor.RefreshAsync(true);var revision=monitor.Revision;
        var explanation=mode=="limit"?"При исчерпании лимита Codex компьютер выключится через 60 секунд. Один раз до следующей перезагрузки ПК.":"После успешного завершения выбранных чатов компьютер выключится через 60 секунд. Ошибки и ожидание ответа не считаются завершением.";
        if(await Confirm("Запланировать выключение ПК?",DesktopLanguage.Text(explanation)+"\n"+DesktopLanguage.Text("Сохраните документы. SideWidget должен оставаться запущен. Новый запуск заменит предыдущий."))){
            if(monitor.Revision!=revision)throw new CommandFault("codex","Сценарий питания уже изменён в другом окне. Повторите действие.");
            monitor.Arm(mode,ids,PowerSchedule);Emit("power.changed",PowerSchedule.State!);
        }
        return monitor.Snapshot();
    }
}

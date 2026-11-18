using System.Text.Json;
using SideWidget.Core;

namespace SideWidget.App;

sealed partial class Controller
{
    object HandleCalendar(Command command)
    {
        object result;
        if(command.ActionId=="calendar.get")return desktopState.Calendar.Snapshot();
        if(command.ActionId=="calendar.delete")result=desktopState.Calendar.Delete(Protocol.Text(command.Params,"id",64));
        else{
            if(!command.Params.TryGetProperty("event",out var value)||value.ValueKind!=JsonValueKind.Object)throw new CommandFault("calendar","Проверьте событие календаря");
            CalendarEntry entry;
            try{entry=value.Deserialize<CalendarEntry>(Protocol.Json)??throw new JsonException();}
            catch(JsonException){throw new CommandFault("calendar","Проверьте событие календаря");}
            result=desktopState.Calendar.Save(entry);
        }
        Emit("calendar.changed",result);return result;
    }
    void TickCalendar()
    {
        try{foreach(var entry in desktopState.Calendar.Due(DateTimeOffset.Now))Emit("calendar.reminder",new{message=DesktopLanguage.Text("Напоминание: {0} · {1}",entry.Title,entry.Time)});}
        catch(Exception error) when(error is Microsoft.Data.Sqlite.SqliteException or IOException){ }
    }
}

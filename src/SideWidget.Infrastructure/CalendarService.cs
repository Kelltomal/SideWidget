using SideWidget.Core;

namespace SideWidget.Infrastructure;

public sealed class CalendarService
{
    const string EventsKey="personal.calendar.events.v1",RemindedKey="personal.calendar.reminded.v1";
    readonly Store store;
    HashSet<string> reminded;
    public IReadOnlyList<CalendarEntry> Events {get;private set;}
    public CalendarService(Store store)
    {
        this.store=store;Events=store.Read<CalendarEntry[]>(EventsKey,[]).Select(e=>e.Validated()).ToArray();
        reminded=store.Read<string[]>(RemindedKey,[]).ToHashSet(StringComparer.Ordinal);
    }
    public object Snapshot()=>new{events=Events};
    public object Save(CalendarEntry entry)
    {
        var valid=entry.Validated();
        if(Events.Count>=1000&&!Events.Any(e=>e.Id==valid.Id))throw new CommandFault("calendar","В календаре уже 1000 событий. Удалите ненужные.");
        var next=Events.Where(e=>e.Id!=valid.Id).Append(valid).OrderBy(e=>e.Date,StringComparer.Ordinal).ThenBy(e=>e.Time,StringComparer.Ordinal).ToArray();
        store.Save(EventsKey,next);Events=next;return Snapshot();
    }
    public object Delete(string id){var next=Events.Where(e=>e.Id!=id).ToArray();store.Save(EventsKey,next);Events=next;return Snapshot();}
    static string Key(CalendarEntry entry)=>entry.Id+":"+entry.Date+":"+entry.Time+":"+entry.ReminderMinutes;
    public IReadOnlyList<CalendarEntry> Due(DateTimeOffset now)
    {
        var due=Events.Where(e=>!e.Done&&e.ReminderMinutes is not null&&!reminded.Contains(Key(e))&&e.StartsAt() is {} start&&now>=start.AddMinutes(-e.ReminderMinutes.Value)&&now<=start.AddMinutes(5)).ToArray();
        if(due.Length==0)return due;
        var active=Events.Select(Key).ToHashSet(StringComparer.Ordinal);
        var claimed=reminded.Where(active.Contains).Concat(due.Select(Key)).ToHashSet(StringComparer.Ordinal);
        store.Save(RemindedKey,claimed.ToArray());reminded=claimed;return due;
    }
}

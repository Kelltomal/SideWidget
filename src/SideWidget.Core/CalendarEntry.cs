using System.Globalization;

namespace SideWidget.Core;

public sealed record CalendarEntry(string Id,string Title,string Date,string Time,string EndTime,bool AllDay,string Notes,int? ReminderMinutes,bool Done)
{
    public CalendarEntry Validated()
    {
        if(!Guid.TryParse(Id,out _)||string.IsNullOrWhiteSpace(Title)||Title.Length>120||Notes is null||Notes.Length>4000||
            !DateOnly.TryParseExact(Date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))
            throw new CommandFault("calendar","Проверьте название, дату и заметку события");
        var time=AllDay?"":Time;var end=AllDay?"":EndTime;
        if(time is null||end is null||(time.Length>0&&!TimeOnly.TryParseExact(time,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))||
            (end.Length>0&&(!TimeOnly.TryParseExact(end,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)||time.Length==0||string.CompareOrdinal(end,time)<=0)))
            throw new CommandFault("calendar","Укажите время в формате ЧЧ:ММ. Окончание должно быть позже начала.");
        if(ReminderMinutes is not (null or 5 or 15))throw new CommandFault("calendar","Выберите напоминание за 5 или 15 минут");
        return this with{Title=Title.Trim(),Time=time,EndTime=end,ReminderMinutes=AllDay||time.Length==0?null:ReminderMinutes};
    }
    public DateTimeOffset? StartsAt()
    {
        if(AllDay||Time.Length==0)return null;
        var local=DateTime.ParseExact(Date+" "+Time,"yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None);
        if(TimeZoneInfo.Local.IsInvalidTime(local))return null;
        return new DateTimeOffset(local,TimeZoneInfo.Local.GetUtcOffset(local));
    }
}

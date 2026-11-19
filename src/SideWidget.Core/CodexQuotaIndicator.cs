using System.Globalization;

namespace SideWidget.Core;

public sealed record CodexQuotaIndicator(double? WeeklyRemaining,double? FiveHourRemaining)
{
    public string WeeklyText=>WeeklyRemaining is not {} value?"—":value is >0 and <1?"<1":Math.Floor(value).ToString(CultureInfo.InvariantCulture);
    public static CodexQuotaIndicator? Create(bool enabled,bool running,CodexLimitsSnapshot limits,DateTimeOffset now)
    {
        if(!enabled||!running)return null;
        var bucket=limits.Available&&limits.CheckedAt>=now.AddMinutes(-3)&&limits.CheckedAt<=now.AddSeconds(5)
            ?limits.Buckets.FirstOrDefault(bucket=>bucket.Id=="codex"):null;
        double? Remaining(int minutes)
        {
            var window=new[]{bucket?.Primary,bucket?.Secondary}.FirstOrDefault(window=>window?.WindowMinutes==minutes);
            return window is not null&&double.IsFinite(window.UsedPercent)&&window.UsedPercent is >=0 and <=100&&
                (window.ResetsAt is null||window.ResetsAt>now)?Math.Round(100-window.UsedPercent,1):null;
        }
        return new(Remaining(10080),Remaining(300));
    }
}

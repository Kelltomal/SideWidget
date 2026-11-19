using System.Text.Json;
using SideWidget.Core;

static class CodexQuotaIndicatorChecks
{
    public static int Run()
    {
        var count=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
        var now=DateTimeOffset.UtcNow;
        var limits=new CodexLimitsSnapshot(true,now,[new("codex",null,
            new(88,10080,now.AddDays(1)),new(35,300,now.AddHours(1)))]);
        Check(CodexQuotaIndicator.Create(false,true,limits,now) is null,"manual disable wins while Codex runs");
        Check(CodexQuotaIndicator.Create(true,false,limits,now) is null,"closing Codex hides compact ring");
        var ring=CodexQuotaIndicator.Create(true,true,limits,now)!;
        Check(ring.WeeklyRemaining==12&&ring.FiveHourRemaining==65,"rings map weekly and five-hour quotas by duration");
        foreach(var (remaining,text) in new (double?,string)[]{(7,"7"),(42,"42"),(100,"100"),(0,"0"),(.4,"<1"),(null,"—")})
            Check(new CodexQuotaIndicator(remaining,95).WeeklyText==text,"center represents weekly quota without padding or a percent sign");
        ring=CodexQuotaIndicator.Create(true,true,limits with{CheckedAt=now.AddMinutes(-5)},now)!;
        Check(ring.WeeklyRemaining is null&&ring.FiveHourRemaining is null,"stale quotas use dotted unknown rings");
        ring=CodexQuotaIndicator.Create(true,true,limits with{Available=false},now)!;
        Check(ring.WeeklyRemaining is null&&ring.FiveHourRemaining is null,"API failure never becomes a full ring");
        ring=CodexQuotaIndicator.Create(true,true,new(true,now,[new("codex",null,new(100,10080,now.AddHours(1)),null)]),now)!;
        Check(ring.WeeklyRemaining==0&&ring.FiveHourRemaining is null,"empty quota differs from missing quota");
        ring=CodexQuotaIndicator.Create(true,true,new(true,now,[new("codex",null,new(20,10080,now.AddSeconds(-1)),new(double.NaN,300,null))]),now)!;
        Check(ring.WeeklyRemaining is null&&ring.FiveHourRemaining is null,"expired and nonfinite quotas stay unknown");
        var settings=JsonSerializer.Deserialize<Preferences>("{\"showCodexLimits\":false}",Protocol.Json)!;
        Check(!settings.ShowCodexLimits,"ring preference survives persistence");
        Protocol.Parse(Protocol.Origin,JsonSerializer.Serialize(new{protocolVersion=1,requestId=Guid.NewGuid(),actionId="settings.clock",@params=new{showCodexLimits=false}},Protocol.Json));
        return count+1;
    }
}

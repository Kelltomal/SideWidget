using System.Text.Json;
using System.Text.RegularExpressions;
namespace SideWidget.Core;
public sealed record Command(int ProtocolVersion,string RequestId,string ActionId,JsonElement Params);
public sealed class CommandFault(string code,string message):Exception(message){public string Code {get;}=code;public string? RequestId {get;internal set;}}
public static class Protocol
{
    public const string Origin="https://sidewidget.local";
    public static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public static Command Parse(string origin,string json)
    {
        if(!Uri.TryCreate(origin,UriKind.Absolute,out var uri)||uri.GetLeftPart(UriPartial.Authority)!=Origin||uri.UserInfo!="")throw new CommandFault("origin","Недоверенный источник");
        if(json.Length>2*1024*1024)throw new CommandFault("limit","Сообщение слишком большое");
        try {
            using var doc=JsonDocument.Parse(json,new(){MaxDepth=16});CheckKeys(doc.RootElement);var e=doc.RootElement;
            if(e.ValueKind!=JsonValueKind.Object||e.EnumerateObject().Count()!=4||e.GetProperty("protocolVersion").GetInt32()!=1||!Guid.TryParse(e.GetProperty("requestId").GetString(),out _)||e.GetProperty("params").ValueKind!=JsonValueKind.Object)throw new JsonException();
            var action=e.GetProperty("actionId").GetString()!;if(!Regex.IsMatch(action,"^[a-z][a-zA-Z0-9.]{1,63}$"))throw new JsonException();
            var command=new Command(1,e.GetProperty("requestId").GetString()!,action,e.GetProperty("params").Clone());
            try{ValidateParams(command);}catch(CommandFault error){error.RequestId=command.RequestId;throw;}
            return command;
        }catch(Exception e) when(e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException){throw new CommandFault("schema","Некорректное сообщение");}
    }
    static void CheckKeys(JsonElement e){if(e.ValueKind==JsonValueKind.Object){var keys=new HashSet<string>();foreach(var p in e.EnumerateObject()){if(!keys.Add(p.Name))throw new JsonException();CheckKeys(p.Value);}}else if(e.ValueKind==JsonValueKind.Array)foreach(var v in e.EnumerateArray())CheckKeys(v);}
    public static void ValidateParams(Command c)
    {
        string[] keys=c.ActionId switch {
            "sites.get"=>[],"sites.save"=>["site"],"sites.delete" or "sites.check"=>["id"],
            "calendar.get"=>[],"calendar.save"=>["event"],"calendar.delete"=>["id"],
            "codex.get" or "codex.refresh" or "codex.cancel"=>[],
            "codex.arm"=>["mode","ids"],
            "power.get" or "power.cancel"=>[],
            "power.schedule"=>["action","hours","minutes"],
            "timer.get"=>[],
            "timer.pause" or "timer.resume" or "timer.reset" or "timer.dismiss"=>["id"],
            "timer.start"=>["mode","seconds","alarmTime","workMinutes","breakMinutes","longBreakMinutes"],
            "desktop.undo" or "desktop.open" or "desktop.icons.undo" or "desktop.status"=>[],
            "desktop.command"=>["action","rules"],
            "desktop.documents"=>[],
            "desktop.move"=>["previewId","ids","topics"],
            "desktop.icon" or "desktop.icons.preview"=>["previewId"],
            "desktop.icons.apply"=>["previewId","ids","styles","icons"],
            "bridge.ready" or "state.get" or "shell.pin" or "shell.collapse" or "shell.interactive" or "history.clear" or "apps.add" or "apps.undo" or "apps.pickWindow" or "color.pick" or "media.get" or "visualizer.get" or "audio.get" or "system.get" or "currency.get" or "translation.configure" or "translation.get" or "translation.cancel"=>[],
            "shell.tooltip"=>["visible","label","description","path"],
            "shell.searchFocused"=>["id"],"shell.homeReady"=>[],
            "shell.editing"=>["value"],"history.add"=>["expression","result"],"history.delete" or "apps.launch" or "apps.remove" or "apps.icon"=>["id"],
            "color.remember"=>["hex"],"units.favorite"=>["from","to"],"clipboard.text"=>["text"],"clipboard.image"=>["dataUrl"],"qr.save"=>["format","data"],
            "qr.print"=>["html"],
            "apps.drop"=>[],"apps.rename"=>["id","name"],"apps.reorder"=>["ids"],"media.command"=>["id","command","positionSeconds"],"audio.set"=>["volume","muted","microphoneMuted","sessionId"],
            "internet.get"=>[],"internet.check"=>["method","target"],"links.open"=>["url"],
            "search.query"=>["text"],"search.open"=>["path","reveal"],"quick.open"=>["target"],"quick.power"=>["action"],"diary.draft"=>["text"],"weather.get"=>["city"],"translation.run"=>["text","source","target","provider"],
            "visualizer.settings"=>["enabled","showInHeader","brightness","sensitivity","style"],
            "settings.theme"=>["theme"],
            "settings.clock"=>["clockFontSize","compactStripHeight","showCodexLimits"],
            "clock.fullscreen"=>[],
            "settings.language"=>["language"],
            "app.share" or "startup.get" or "updates.check" or "updates.defer"=>[],"startup.set"=>["enabled"],
            "updates.skip" or "updates.install"=>["version"],
            "remote.get" or "remote.invite"=>[],
            "remote.configure"=>["enabled","adapterId","port","setupPort","startup"],
            "remote.setup"=>["adapterId","port","setupPort","startup"],
            "remote.decide"=>["id","approved"],"remote.revoke"=>["id"],
            "settings.save"=>["width","height","hideDelay","openDelay","hotkey","monitor","position","largeMode","order","disabled","theme","visualizer","displayIds","pinnedTools","displayPositions","language","clockFontSize","compactStripHeight","codexShortcut","showCodexLimits"],
            _=>throw new CommandFault("unknown_action","Действие не поддерживается")};
        if(c.Params.EnumerateObject().Any(p=>!keys.Contains(p.Name)))throw new CommandFault("schema","Неизвестный параметр");
    }
    public static string Text(JsonElement p,string key,int limit=10000){if(!p.TryGetProperty(key,out var e)||e.ValueKind!=JsonValueKind.String||e.GetString()!.Length>limit)throw new CommandFault("schema","Проверьте поле «"+key+"»");return e.GetString()!;}
}




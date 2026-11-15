using System.Text.Json;
using SideWidget.Core;
using SideWidget.Infrastructure;

internal static class DisplaySelectionChecks
{
    public static int Run()
    {
        var count=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
        void Reject(Preferences value){try{value.Validate();}catch(CommandFault){count++;return;}throw new Exception("invalid display selection accepted");}
        DisplayDescriptor[] displays=[new("a","Экран 1",false),new("b","Экран 2",true),new("c","Экран 3",false)];
        Check(DisplaySelection.Resolve(new Preferences(Monitor:2),displays).SequenceEqual(["c"]),"legacy index preserved");
        Check(DisplaySelection.Resolve(new Preferences(Monitor:15),displays).SequenceEqual(["b"]),"missing legacy monitor falls back to primary");
        var settings=new Preferences(DisplayIds:["c","a"]);
        Check(DisplaySelection.Resolve(settings,displays).SequenceEqual(["a","c"]),"selected displays appear exactly once in catalog order");
        Check(DisplaySelection.Resolve(settings,[displays[0],displays[1]]).SequenceEqual(["a"]),"unplug removes only unavailable window");
        Check(DisplaySelection.Resolve(settings,[displays[1]]).SequenceEqual(["b"]),"all unavailable uses primary without rewriting selection");
        Check(settings.DisplayIds!.SequenceEqual(["c","a"]),"fallback preserves chosen IDs");
        Check(DisplaySelection.Resolve(settings,displays).SequenceEqual(["a","c"]),"reconnect restores selection");
        Check(DisplaySelection.Resolve(settings,[]).Length==0,"empty catalog is safe");
        Reject(new Preferences(DisplayIds:[]));Reject(new Preferences(DisplayIds:["a","a"]));Reject(new Preferences(DisplayIds:[" "]));Reject(new Preferences(DisplayIds:[new string('x',513)]));
        Reject(new Preferences(DisplayIds:Enumerable.Range(0,17).Select(x=>x.ToString()).ToArray()));
        var old=JsonSerializer.Deserialize<Preferences>("{\"monitor\":1}",Protocol.Json)!;
        Check(old.DisplayIds==null&&DisplaySelection.Resolve(old,displays).SequenceEqual(["b"]),"old stored JSON remains compatible");
        var directory=Path.Combine(Path.GetTempPath(),"SideWidget-displays-"+Guid.NewGuid().ToString("N"));
        var state=new PersonalState(directory);state.SaveSettings(settings);
        Check(new PersonalState(directory).Settings.DisplayIds!.SequenceEqual(settings.DisplayIds!),"selected IDs persist");
        var envelope=JsonSerializer.Serialize(new{protocolVersion=1,requestId=Guid.NewGuid().ToString(),actionId="settings.save",@params=settings},Protocol.Json);
        Check(Protocol.Parse(Protocol.Origin,envelope).ActionId=="settings.save","bridge accepts display selection");
        return count;
    }
}

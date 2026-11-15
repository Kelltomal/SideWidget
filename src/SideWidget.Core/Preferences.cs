namespace SideWidget.Core;
public sealed record Preferences(int Width=444,int Height=620,int HideDelay=650,int OpenDelay=100,string Hotkey="Ctrl+Alt+Space",int Monitor=0,double Position=1,bool LargeMode=false,string[]? Order=null,string[]? Disabled=null,string Theme="dark",VisualizerOptions? Visualizer=null,string[]? DisplayIds=null,string[]? PinnedTools=null,Dictionary<string,double>? DisplayPositions=null)
{
    static readonly string[] Tools=["calculator","translation","qr","currency","units","colors","diary","timer","desktop"];
    public double PositionForDisplay(string id)=>DisplayPositions?.GetValueOrDefault(id,Position)??Position;
    public Preferences WithDisplayPosition(string id,double position)
    {
        var positions=DisplayPositions is null?new Dictionary<string,double>(StringComparer.Ordinal):new(DisplayPositions,StringComparer.Ordinal);
        positions[id]=position;
        return this with{DisplayPositions=positions};
    }
    void ValidateDisplayPositions()
    {
        if(DisplayPositions?.Any(entry=>string.IsNullOrWhiteSpace(entry.Key)||entry.Key.Length>512||!double.IsFinite(entry.Value)||entry.Value is <0 or >1)==true)
            throw new CommandFault("settings","Проверьте сохранённые позиции экранов");
    }
    public void Validate(){if(Width is <360 or >900||Height is <360 or >1200||HideDelay is <200 or >5000||OpenDelay is <0 or >2000||Monitor is <0 or >15||!double.IsFinite(Position)||Position is <0 or >1||Order?.Length>25||Disabled?.Length>25)throw new CommandFault("settings","Проверьте размеры, задержки и положение");if(DisplayIds is not null&&(DisplayIds.Length is <1 or >16||DisplayIds.Any(id=>string.IsNullOrWhiteSpace(id)||id.Length>512)||DisplayIds.Distinct(StringComparer.Ordinal).Count()!=DisplayIds.Length))throw new CommandFault("settings","Выберите хотя бы один экран без повторений");if(Theme is not ("dark" or "light" or "system"))throw new CommandFault("settings","Выберите тему приложения");if(PinnedTools is not null&&(PinnedTools.Length>4||PinnedTools.Any(id=>!Tools.Contains(id))||PinnedTools.Distinct(StringComparer.Ordinal).Count()!=PinnedTools.Length))throw new CommandFault("settings","Выберите до четырёх разных инструментов");ValidateDisplayPositions();Visualizer?.Validate();HotkeyParser.Parse(Hotkey);}
}
public static class HotkeyParser
{
    public static (uint Modifiers,uint Key) Parse(string text)
    {
        if(string.IsNullOrWhiteSpace(text)||text.Length>60)throw new CommandFault("hotkey","Укажите сочетание до 60 символов");var parts=text.Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);uint modifiers=0;
        foreach(var part in parts.SkipLast(1)){var flag=part.ToUpperInvariant() switch{"CTRL"=>2u,"ALT"=>1u,"SHIFT"=>4u,"WIN"=>8u,_=>0u};if(flag==0||(modifiers&flag)!=0)throw new CommandFault("hotkey","Пример сочетания: Ctrl+Alt+Space");modifiers|=flag;}
        var key=parts.LastOrDefault()?.ToUpperInvariant()??"";uint value=key switch{"SPACE"=>32u,_ when key.Length==1&&char.IsAsciiLetterOrDigit(key[0])=>(uint)key[0],_ when key.StartsWith('F')&&int.TryParse(key[1..],out var n)&&n is >=1 and <=24=>(uint)(111+n),_=>0};
        if(modifiers==0||value==0)throw new CommandFault("hotkey","Нужен Ctrl, Alt, Shift или Win и буква, цифра, Space или F1–F24");return(modifiers|0x4000,value);
    }
}
public sealed record Calculation(string Id,string Expression,string Result,DateTimeOffset Time);
public sealed record UnitPair(string From,string To);
public sealed record PinnedApp(string Id,string Name,string Path,string? Icon=null,bool Packaged=false,string? Identity=null);

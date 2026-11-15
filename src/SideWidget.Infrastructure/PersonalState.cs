using SideWidget.Core;
using System.Text.Json;
namespace SideWidget.Infrastructure;
public sealed class PersonalState
{
    public Store Store {get;}
    public Preferences Settings {get;set;}
    public List<Calculation> CalculatorHistory {get;}
    public List<string> ColorHistory {get;}
    public List<UnitPair> UnitFavorites {get;}
    public IReadOnlyList<PinnedApp> Apps {get;private set;}
    public string DiaryDraft {get;private set;}
    public PersonalState(string directory)
    {
        Store=new(directory);var current=Store.Read<Preferences?>("personal.preferences.v2",null);
        if(current==null){var legacy=Store.Read<JsonElement>("preferences",default);current=new();if(legacy.ValueKind==JsonValueKind.Object){if(legacy.TryGetProperty("width",out var w)&&w.TryGetInt32(out var width))current=current with{Width=Math.Clamp(width,360,900)};if(legacy.TryGetProperty("height",out var h)&&h.TryGetInt32(out var height))current=current with{Height=Math.Clamp(height,360,1200)};}Store.Save("personal.preferences.v2",current);}
        try{current.Validate();}catch(CommandFault){current=new();}Settings=current;
        CalculatorHistory=Store.Read("personal.calculations",new List<Calculation>());ColorHistory=Store.Read("personal.colors",new List<string>());UnitFavorites=Store.Read("personal.units",new List<UnitPair>());Apps=Store.ReadApps().AsReadOnly();DiaryDraft=Store.Read("personal.diaryDraft","");
    }
    public void SaveSettings(Preferences p){p.Validate();Store.Save("personal.preferences.v2",p);Settings=p;}
    public void AddCalculation(string expression,string result){if(expression.Length is <1 or >500||result.Length>200)throw new CommandFault("limit","Выражение слишком длинное");CalculatorHistory.Insert(0,new(Guid.NewGuid().ToString("N"),expression,result,DateTimeOffset.Now));if(CalculatorHistory.Count>100)CalculatorHistory.RemoveRange(100,CalculatorHistory.Count-100);SaveCalculations();}
    public void SaveCalculations()=>Store.Save("personal.calculations",CalculatorHistory);
    public void RememberColor(string hex){if(!System.Text.RegularExpressions.Regex.IsMatch(hex,"^#[0-9a-fA-F]{6}$"))throw new CommandFault("color","Некорректный HEX");hex=hex.ToUpperInvariant();ColorHistory.Remove(hex);ColorHistory.Insert(0,hex);if(ColorHistory.Count>24)ColorHistory.RemoveAt(24);Store.Save("personal.colors",ColorHistory);}
    public void FavoriteUnit(string from,string to){if(from.Length>32||to.Length>32)throw new CommandFault("unit","Некорректная единица");var pair=new UnitPair(from,to);if(!UnitFavorites.Remove(pair))UnitFavorites.Insert(0,pair);if(UnitFavorites.Count>20)UnitFavorites.RemoveAt(20);Store.Save("personal.units",UnitFavorites);}
    public void SaveApps(IEnumerable<PinnedApp> candidate)
    {
        var next = candidate.ToList();
        Store.SaveApps(next);
        Apps = next.AsReadOnly();
    }
    public void SaveDraft(string text){if(text.Length>80000)throw new CommandFault("limit","Черновик слишком большой");Store.Save("personal.diaryDraft",text);DiaryDraft=text;}
}

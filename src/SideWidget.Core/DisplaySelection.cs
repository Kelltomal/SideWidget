namespace SideWidget.Core;

public sealed record DisplayDescriptor(string Id,string Name,bool Primary);

public static class DisplaySelection
{
    public static string[] Resolve(Preferences settings,IReadOnlyList<DisplayDescriptor> available)
    {
        if(available.Count==0)return [];
        if(settings.DisplayIds is null)
            return [available.ElementAtOrDefault(settings.Monitor)?.Id??Fallback(available).Id];
        var selected=available.Where(display=>settings.DisplayIds.Contains(display.Id,StringComparer.Ordinal)).Select(display=>display.Id).Distinct().ToArray();
        return selected.Length>0?selected:[Fallback(available).Id];
    }

    static DisplayDescriptor Fallback(IReadOnlyList<DisplayDescriptor> available)=>available.FirstOrDefault(display=>display.Primary)??available[0];
}

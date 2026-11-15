namespace SideWidget.Core;

public enum PanelState { Collapsed, Opening, Expanded, Closing }
public sealed class PanelMotion
{
    public double Height { get; private set; } = Preferences.DefaultClockHeaderHeight;
    public double Expansion { get; private set; }
    public PanelState State { get; private set; } = PanelState.Collapsed;
    public bool Pinned { get; set; }
    public bool Editing { get; set; }
    public bool Suspended { get; set; }
    private double collapsedHeight=Preferences.DefaultClockHeaderHeight;
    private double start=Preferences.DefaultClockHeaderHeight,target=Preferences.DefaultClockHeaderHeight,elapsed;
    private double startExpansion, targetExpansion;
    public void ResizeHeader(double headerHeight,double maximum)
    {
        if(collapsedHeight==headerHeight)return;
        collapsedHeight=headerHeight;
        var travel=Math.Max(0,maximum-headerHeight);
        Height=headerHeight+Expansion*travel;
        start=headerHeight+startExpansion*travel;
        target=headerHeight+targetExpansion*travel;
    }

    public void Target(bool open, double maximum = 620, bool immediate = false)
    {
        if (open && Suspended || !open && (Pinned || Editing) && !Suspended) return;
        var next = open ? Math.Max(collapsedHeight, maximum) : collapsedHeight;
        var nextExpansion=open?1:0;
        if (target == next && targetExpansion==nextExpansion && !immediate) return;
        start = Height; target = next; elapsed = 0;
        startExpansion=Expansion;targetExpansion=nextExpansion;
        State = open ? PanelState.Opening : PanelState.Closing;
        if (immediate) Tick(240);
    }
    public bool Tick(double elapsedMs)
    {
        if (State is PanelState.Collapsed or PanelState.Expanded) return false;
        elapsed += Math.Max(0, elapsedMs);
        var t = Math.Clamp(elapsed / 240, 0, 1);
        Height = start + (target - start) * (1 - Math.Pow(1 - t, 3));
        Expansion=startExpansion+(targetExpansion-startExpansion)*(1-Math.Pow(1-t,3));
        if (t == 1) State = targetExpansion==0 ? PanelState.Collapsed : PanelState.Expanded;
        return true;
    }
}

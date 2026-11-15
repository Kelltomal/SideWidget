namespace SideWidget.Core;

public enum PanelState { Collapsed, Opening, Expanded, Closing }
public sealed class PanelMotion
{
    public double Height { get; private set; } = 52;
    public double Expansion { get; private set; }
    public PanelState State { get; private set; } = PanelState.Collapsed;
    public bool Pinned { get; set; }
    public bool Editing { get; set; }
    public bool Suspended { get; set; }
    private double start = 52, target = 52, elapsed;
    private double startExpansion, targetExpansion;
    public void Target(bool open, double maximum = 620, bool immediate = false)
    {
        if (open && Suspended || !open && (Pinned || Editing) && !Suspended) return;
        var next = open ? Math.Max(52, maximum) : 52;
        if (target == next && !immediate) return;
        start = Height; target = next; elapsed = 0;
        startExpansion=Expansion;targetExpansion=open?1:0;
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
        if (t == 1) State = target == 52 ? PanelState.Collapsed : PanelState.Expanded;
        return true;
    }
}

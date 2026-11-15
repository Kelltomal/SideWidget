namespace SideWidget.Core;

public readonly record struct PanelBounds(int Left,int Width);

public readonly record struct PanelViewport(PanelBounds Host,int ClipLeft,int ClipWidth);

public static class PanelGeometry
{
    public const double ExpandedWidthFactor=1.8;

    public static PanelBounds Calculate(int workLeft,int workRight,int baseWidth,double position,double expansion)
    {
        var available=Math.Max(1,workRight-workLeft);
        var collapsedWidth=Math.Clamp(baseWidth,1,available);
        var width=Math.Min(available,(int)Math.Round(collapsedWidth*(1+.8*Math.Clamp(expansion,0,1))));
        var anchor=workLeft+(available-collapsedWidth)*Math.Clamp(position,0,1);
        var left=(int)Math.Round(anchor-(width-collapsedWidth)/2d);
        return new(Math.Clamp(left,workLeft,workRight-width),width);
    }

    // Keep the HWND and WebView at their final size; reveal only the region.
    public static PanelViewport Viewport(int workLeft,int workRight,int baseWidth,double position,double expansion)
    {
        var host=Calculate(workLeft,workRight,baseWidth,position,1);
        var visible=Calculate(workLeft,workRight,baseWidth,position,expansion);
        return new(host,visible.Left-host.Left,visible.Width);
    }

    // Invert the visible left edge without saving the temporarily expanded width.
    public static double PositionForLeft(int workLeft,int workRight,int baseWidth,int visibleLeft,double expansion)
    {
        var available=Math.Max(1,workRight-workLeft);
        var collapsedWidth=Math.Clamp(baseWidth,1,available);
        var width=Calculate(workLeft,workRight,baseWidth,0,expansion).Width;
        if(available==collapsedWidth)return 0;
        var left=Math.Clamp(visibleLeft,workLeft,workRight-width);
        return (left-workLeft+(width-collapsedWidth)/2d)/(available-collapsedWidth);
    }
}

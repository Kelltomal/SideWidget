using System.Text.Json;
using System.Text.RegularExpressions;

namespace SideWidget.Core;

public sealed record TooltipContent(string Label,string Description,string Path)
{
    public static TooltipContent Read(JsonElement value)
    {
        var label=Protocol.Text(value,"label",180);
        var description=Protocol.Text(value,"description",800);
        var path=Protocol.Text(value,"path",4096);
        if(!Regex.IsMatch(path,@"\A[\sMmZzLlHhVvCcSsQqTtAa0-9.,+eE-]*\z"))
            throw new CommandFault("schema","Некорректный значок подсказки");
        return new(label,description,path);
    }
}

public readonly record struct TooltipBounds(int Left,int Top,int Width,int Height);
public static class TooltipLayout
{
    // Never move help over controls when there is no space beneath the panel.
    public static TooltipBounds? Below(int panelLeft,int panelBottom,int panelWidth,int workLeft,int workRight,int workBottom,double scale,int desiredHeight=100)
    {
        var gap=Math.Max(1,(int)Math.Round(6*scale));
        var width=Math.Min((int)Math.Round(340*scale),Math.Min(panelWidth-2*gap,workRight-workLeft-2*gap));
        var height=Math.Min((int)Math.Round(desiredHeight*scale),workBottom-panelBottom-gap);
        if(width<120*scale||height<64*scale)return null;
        var left=Math.Clamp(panelLeft+(panelWidth-width)/2,workLeft+gap,workRight-width-gap);
        return new(left,panelBottom,width,height);
    }
}

using System.Text.Json;
using SideWidget.Core;

static class TooltipChecks
{
    public static int Run()
    {
        var count=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
        var normal=TooltipLayout.Below(100,620,648,0,1920,1040,1)!.Value;
        Check(normal.Top==620&&normal.Left+normal.Width/2==424,"tooltip centered beneath panel with no gap");
        var left=TooltipLayout.Below(-1920,620,360,-1920,0,1040,1)!.Value;
        Check(left.Left>=-1920&&left.Left+left.Width<=0,"tooltip on negative-coordinate monitor");
        Check(TooltipLayout.Below(0,1000,648,0,1920,1040,1)==null,"no room hides help instead of covering menu");
        var dpi=TooltipLayout.Below(200,700,900,0,2560,1440,1.5)!.Value;
        Check(dpi.Width==510&&dpi.Top==700,"tooltip respects monitor DPI without a vertical gap");
        using var json=JsonDocument.Parse("{\"label\":\"Wi-Fi\",\"description\":\"Настройки сети\",\"path\":\"M2 8a16 16 0 0 1 20 0\"}");
        Check(TooltipContent.Read(json.RootElement).Label=="Wi-Fi","tooltip reads text and SVG geometry");
        using var bad=JsonDocument.Parse("{\"label\":\"Wi-Fi\",\"description\":\"\",\"path\":\"<Image Source='https://example.com'/>\"}");
        try{TooltipContent.Read(bad.RootElement);throw new Exception("SVG markup accepted");}catch(CommandFault){count++;}
        return count;
    }
}

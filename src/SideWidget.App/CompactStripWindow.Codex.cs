using System.Runtime.InteropServices;
using SideWidget.Core;

namespace SideWidget.App;

internal sealed partial class CompactStripWindow
{
    CodexQuotaIndicator? codexQuota;
    string? centeredClock,clockDate;
    public void SetClockLabels(string? clock,string? date)
    {
        if(centeredClock==clock&&clockDate==date)return;
        centeredClock=clock;clockDate=date;
        if(visible)InvalidateRect(Handle,0,false);
    }
    public void SetCodexQuota(CodexQuotaIndicator? quota)
    {
        if(codexQuota==quota)return;
        codexQuota=quota;RefreshLanguage();
        if(visible)InvalidateRect(Handle,0,false);
    }
    int PaintCodexQuota(nint dc,Native.Rect textBounds,string displayText)
    {
        var measured=textBounds;
        DrawTextW(dc,displayText,displayText.Length,ref measured,0x0020|0x0400); // SINGLELINE | CALCRECT
        var textRight=width/2d+(measured.Right-measured.Left)/2d;
        if(codexQuota is null)return (int)Math.Ceiling(textRight);
        var size=Math.Min(30*scale,height-4*scale);
        var factor=size/30;
        var cx=Math.Min(textBounds.Right-size/2,textRight+8*scale+size/2);
        var cy=height/2d;
        var oldBrush=SelectObject(dc,GetStockObject(5)); // HOLLOW_BRUSH
        var oldDirection=SetArcDirection(dc,2); // clockwise, matching the existing SVG
        try
        {
            foreach(var (radius,remaining) in new[]{(13d,codexQuota.WeeklyRemaining),(9d,codexQuota.FiveHourRemaining)})
            {
                var r=radius*factor;
                var left=(int)Math.Round(cx-r);var top=(int)Math.Round(cy-r);
                var right=(int)Math.Round(cx+r);var bottom=(int)Math.Round(cy+r);
                void Draw(uint color,bool dotted,bool full)
                {
                    var pen=CreatePen(dotted?2:0,dotted?1:Math.Max(1,(int)Math.Round(1.6*factor)),color);
                    if(pen==0)return;
                    var oldPen=SelectObject(dc,pen);
                    try
                    {
                        if(full)Ellipse(dc,left,top,right,bottom);
                        else if(remaining>0)
                        {
                            var angle=remaining.Value/100*Math.PI*2;
                            var startX=(int)Math.Round(cx);
                            var endX=(int)Math.Round(cx+r*Math.Sin(angle));
                            var endY=(int)Math.Round(cy-r*Math.Cos(angle));
                            // GDI draws a complete circle when rounded endpoints coincide.
                            if(startX==endX&&top==endY)SetPixelV(dc,startX,top,color);
                            else Arc(dc,left,top,right,bottom,startX,top,endX,endY);
                        }
                    }
                    finally{SelectObject(dc,oldPen);DeleteObject(pen);}
                }
                Draw(light?0x00D3CEC9u:0x003D3730u,false,true);
                Draw(remaining is null?(light?0x006E6359u:0x00BAB1A9u):remaining<=15?
                    (light?0x002A39A9u:0x00C1C8FFu):(light?0x00745D45u:0x00D0C3B6u),remaining is null,remaining is null||remaining>=100);
            }
            var value=codexQuota.WeeklyText;
            var pixelSize=(int)Math.Round(value.Length>2?Math.Max(6*scale,8*factor):Math.Max(8*scale,10*factor));
            var valueFont=CreateFontW(-pixelSize,0,0,0,600,0,0,0,1,0,0,4,0,"Medeo Sans");
            var savedFont=valueFont!=0?SelectObject(dc,valueFont):0;
            var savedColor=SetTextColor(dc,light?0x002C2620u:0x00F5F4F2u);
            try
            {
                var valueBounds=new Native.Rect{Left=(int)Math.Floor(cx-size/2),Right=(int)Math.Ceiling(cx+size/2),Top=0,Bottom=height};
                DrawTextW(dc,value,value.Length,ref valueBounds,0x0001|0x0004|0x0020|0x0800);
            }
            finally{SetTextColor(dc,savedColor);if(savedFont!=0)SelectObject(dc,savedFont);if(valueFont!=0)DeleteObject(valueFont);}
        }
        finally{SetArcDirection(dc,oldDirection);SelectObject(dc,oldBrush);}
        return (int)Math.Ceiling(cx+size/2);
    }
    [DllImport("gdi32.dll")] static extern nint CreatePen(int style,int width,uint color);
    [DllImport("gdi32.dll")] static extern nint GetStockObject(int item);
    [DllImport("gdi32.dll")] static extern bool Ellipse(nint dc,int left,int top,int right,int bottom);
    [DllImport("gdi32.dll")] static extern bool Arc(nint dc,int left,int top,int right,int bottom,int startX,int startY,int endX,int endY);
    [DllImport("gdi32.dll")] static extern int SetArcDirection(nint dc,int direction);
    [DllImport("gdi32.dll")] static extern bool SetPixelV(nint dc,int x,int y,uint color);
}

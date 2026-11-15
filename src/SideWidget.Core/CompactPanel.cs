namespace SideWidget.Core;

public sealed class CompactPanel
{
    public const int Height=23;
    public const byte Alpha=77; // 30% of 255, rounded.
    public const uint DoubleClickTime=400;
    public bool Covered { get; private set; }
    public bool StripVisible => Covered&&!revealed;
    public bool ClockOnly { get; private set; }
    bool revealed;
    long? firstClick;
    int firstX,firstY;
    bool firstRight, firstExpanded;

    public void Update(bool covered)
    {
        if(Covered==covered)return;
        Covered=covered;revealed=false;firstClick=null;
    }
    public void ObserveWindow(bool onActiveDisplay, bool coversDisplay)
    {
        if(onActiveDisplay)Update(coversDisplay);
    }
    public void Fold(){revealed=false;firstClick=null;}
    public void Reveal(){revealed=true;firstClick=null;}
    public void ResetClicks()=>firstClick=null;
    public bool Click(long milliseconds,int x,int y,int distanceX,int distanceY,bool right=false,bool expanded=false)
    {
        if(!StripVisible&&!right)return false;
        if(firstClick is long first&&firstRight==right&&firstExpanded==expanded&&milliseconds>=first&&milliseconds-first<=DoubleClickTime
            &&Math.Abs(x-firstX)<=distanceX/2&&Math.Abs(y-firstY)<=distanceY/2)
        {
            firstClick=null;
            if(!right)revealed=true;
            else if(!expanded)ClockOnly=!ClockOnly;
            return true;
        }
        firstClick=milliseconds;firstX=x;firstY=y;firstRight=right;firstExpanded=expanded;return false;
    }
}

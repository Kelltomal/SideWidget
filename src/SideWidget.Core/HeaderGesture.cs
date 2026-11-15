namespace SideWidget.Core;

public sealed class HeaderGesture
{
    public const int HoldMilliseconds=250;
    bool pressed;
    int startX,startY;
    long started;
    public bool Moved { get; private set; }
    public void Press(int x,int y,long milliseconds)
    {
        pressed=true;Moved=false;startX=x;startY=y;started=milliseconds;
    }
    public void Move(int x,int y)
    {
        if(pressed&&(Math.Abs(x-startX)>5||Math.Abs(y-startY)>5))Moved=true;
    }
    public bool Release(int x,int y,long milliseconds)
    {
        Move(x,y);
        var click=pressed&&!Moved&&milliseconds-started<HoldMilliseconds;
        pressed=false;
        return click;
    }
    public void Cancel()=>pressed=false;
}

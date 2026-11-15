using SideWidget.Core;

static class PanelInteractionChecks
{
    public static int Run()
    {
        var count=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
        var press=new HeaderGesture();
        press.Press(100,200,0);
        Check(press.Release(100,200,100),"short release opens");
        press.Press(100,200,0);
        Check(!press.Release(100,200,400),"hold without movement does not open");
        press.Press(100,200,0);press.Move(120,200);
        Check(!press.Release(100,200,100),"drag returning to origin does not open");
        press.Press(100,200,0);
        Check(!press.Release(120,200,100),"release displacement does not open even without move event");
        press.Press(100,200,0);press.Move(100,220);
        Check(!press.Release(100,220,100),"vertical movement suppresses click");
        press.Press(100,200,0);press.Cancel();
        Check(!press.Release(100,200,100),"capture loss does not open");
        foreach(var position in new[]{0,.2,.5,.8,1})
        {
            var host=PanelGeometry.Viewport(-1920,0,444,position,0);
            foreach(var expansion in new[]{0,.1,.4,.8,1})
            {
                var frame=PanelGeometry.Viewport(-1920,0,444,position,expansion);
                var visible=PanelGeometry.Calculate(-1920,0,444,position,expansion);
                Check(frame.Host==host.Host,"WebView host width and origin stay fixed throughout reveal");
                Check(frame.Host.Left+frame.ClipLeft==visible.Left&&frame.ClipWidth==visible.Width,"clip matches visible geometry");
                Check(frame.ClipLeft>=0&&frame.ClipLeft+frame.ClipWidth<=frame.Host.Width,"clip stays inside host");
            }
        }
        return count;
    }
}

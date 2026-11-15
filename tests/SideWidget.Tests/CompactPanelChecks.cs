using SideWidget.Core;

static class CompactPanelChecks
{
    public static int Run()
    {
        var count=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
        var panel=new CompactPanel();
        Check(!panel.StripVisible,"ordinary desktop keeps header");
        panel.Update(true);
        Check(panel.StripVisible,"covering application shows strip");
        Check(!panel.Click(100,0,0,4,4),"single click does not reveal");
        Check(panel.StripVisible,"single click leaves strip visible");
        Check(panel.Click(300,1,1,4,4),"double click reveals");
        Check(!panel.StripVisible&&panel.Covered,"revealed panel retains covering context");
        panel.Update(true);
        Check(!panel.StripVisible,"polling covering window does not close revealed panel");
        panel.Fold();Check(panel.StripVisible,"collapse restores strip");
        Check(!panel.Click(350,1,1,4,4),"third click is not a second double click");
        Check(!panel.Click(1000,1,1,4,4),"slow clicks do not reveal");
        Check(!panel.Click(1100,20,1,4,4),"distant clicks do not reveal");
        Check(panel.Click(1200,20,1,4,4),"nearby second click reveals");
        panel.Fold();panel.Click(1300,0,0,4,4);panel.Update(false);panel.Update(true);
        Check(!panel.Click(1400,0,0,4,4),"mode change clears pending click");
        panel.Update(false);
        Check(!panel.StripVisible&&!panel.Covered,"restore application restores header");
        Check(!panel.Click(1450,0,0,4,4),"normal header does not use strip gesture");
        panel.Update(true);panel.Click(1500,0,0,4,4);panel.ResetClicks();
        Check(!panel.Click(1600,0,0,4,4),"outside click interrupts double click");
        Check(!panel.Click(1700,3,0,4,4),"double-click rectangle uses half system width");
        panel.ResetClicks();
        panel.Click(2000,0,0,4,4);
        Check(!panel.Click(2401,0,0,4,4),"401 ms is too late for the compact strip");
        Check(panel.Click(2801,0,0,4,4),"400 ms still qualifies for the compact strip");
        panel.Update(false);
        Check(!panel.Click(3000,0,0,4,4,right:true)&&!panel.ClockOnly,"one right click leaves the ordinary header full");
        Check(panel.Click(3200,0,0,4,4,right:true)&&panel.ClockOnly,"double right click makes the ordinary header clock-only");
        panel.Update(true);
        Check(panel.ClockOnly&&panel.StripVisible,"clock-only mode carries over to the compact strip");
        panel.Click(3300,0,0,4,4);
        Check(!panel.Click(3400,0,0,4,4,right:true),"mixed buttons are separate clicks");
        Check(panel.Click(3500,0,0,4,4,right:true)&&!panel.ClockOnly&&panel.StripVisible,
            "double right click restores the full strip without revealing the panel");
        panel.Update(false);
        panel.Click(3600,0,0,4,4,right:true);
        Check(!panel.Click(3700,0,0,4,4,right:true,expanded:true),"opening interrupts the right-click pair");
        Check(panel.Click(3800,0,0,4,4,right:true,expanded:true)&&!panel.ClockOnly,
            "expanded right-click pair leaves clock-only mode untouched");
        Check(CompactPanel.Height==23&&CompactPanel.Alpha==77,"compact height and 30 percent alpha");
        var firstScreen=new CompactPanel();
        var secondScreen=new CompactPanel();
        firstScreen.ObserveWindow(true,true);
        secondScreen.ObserveWindow(false,true);
        Check(!secondScreen.Covered,"inactive monitor ignores even a covering observation");
        firstScreen.ObserveWindow(false,false);
        secondScreen.ObserveWindow(true,true);
        Check(firstScreen.StripVisible&&secondScreen.StripVisible,"switching focus keeps the previous screen compact");
        firstScreen.Reveal();
        firstScreen.ObserveWindow(false,false);
        Check(!firstScreen.StripVisible&&firstScreen.Covered,"another screen does not reset a deliberate reveal");
        firstScreen.Fold();
        firstScreen.ObserveWindow(false,false);
        Check(firstScreen.StripVisible,"losing fullscreen on the inactive monitor preserves its compact state");
        firstScreen.ObserveWindow(true,false);
        Check(!firstScreen.Covered,"returning to an ordinary window restores header");
        secondScreen.ObserveWindow(true,false);
        Check(!secondScreen.Covered,"ordinary foreground window on the same screen restores header");
        return count;
    }
}

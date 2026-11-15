using SideWidget.Core;

static class PanelGeometryChecks
{
    public static int Run()
    {
        var count=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
        var closed=PanelGeometry.Calculate(0,1920,444,.5,0);
        var open=PanelGeometry.Calculate(0,1920,444,.5,1);
        Check(closed.Width==444&&closed.Left==738,"collapsed base width and anchor");
        Check(open.Width==799&&Math.Abs(open.Left+open.Width/2d-960)<=.5,"expanded width grows 40% each side");
        Check(PanelGeometry.Calculate(0,1920,444,0,1).Left==0,"left monitor edge");
        var right=PanelGeometry.Calculate(0,1920,444,1,1);
        Check(right.Left+right.Width==1920,"right monitor edge");
        var narrow=PanelGeometry.Calculate(-800,0,900,.5,1);
        Check(narrow.Left==-800&&narrow.Width==800,"narrow negative-coordinate monitor");
        var half=PanelGeometry.Calculate(0,1920,444,.5,.5);
        var legacy=new Preferences(Position:.7);
        var moved=legacy.WithDisplayPosition("left",.25);
        Check(moved.PositionForDisplay("left")==.25&&moved.PositionForDisplay("right")==.7,"moving one display preserves the other display's position");
        var both=moved.WithDisplayPosition("right",.9);
        var restoredSettings=System.Text.Json.JsonSerializer.Deserialize<Preferences>(System.Text.Json.JsonSerializer.Serialize(both,Protocol.Json),Protocol.Json)!;
        Check(restoredSettings.PositionForDisplay("left")==.25&&restoredSettings.PositionForDisplay("right")==.9,"independent display positions survive serialization");
        var edge=PanelGeometry.PositionForLeft(-1920,0,444,-1920,1);
        var folded=PanelGeometry.Calculate(-1920,0,444,edge,0);
        Check(Math.Abs(folded.Left+folded.Width/2d-(-1920+799/2d))<=1,"expanded drop at edge retains the visible center when folded");
        Check(half.Width>closed.Width&&half.Width<open.Width,"intermediate horizontal size");
        foreach(var position in new[]{0,.2,.5,.8,1})
        {
            var bounds=PanelGeometry.Calculate(-1920,0,555,position,1);
            var restored=PanelGeometry.PositionForLeft(-1920,0,555,bounds.Left,1);
            Check(PanelGeometry.Calculate(-1920,0,555,restored,1)==bounds,"drag and resize preserve visible left edge");
        }
        var motion=new PanelMotion();motion.Target(true,620);motion.Tick(90);
        Check(motion.Expansion>.5&&motion.Expansion<1,"opening decelerates");
        var expansion=motion.Expansion;motion.Target(false);Check(motion.Expansion==expansion,"reversal preserves width");
        motion.Tick(240);Check(motion.Expansion==0,"collapsed width restored");
        motion.Target(true,900,true);Check(motion.Expansion==1,"immediate expanded width");
        motion.Target(true,400,true);Check(motion.Expansion==1,"height changes keep expanded width");
        motion.Target(false,immediate:true);Check(motion.Expansion==0,"immediate collapsed width");
        motion.Target(true,620);motion.Tick(60);var first=motion.Expansion;
        motion.Tick(60);var second=motion.Expansion-first;
        motion.Tick(60);var third=motion.Expansion-first-second;
        motion.Tick(60);var fourth=motion.Expansion-first-second-third;
        Check(first>second&&second>third&&third>fourth&&fourth>0,"opening slows throughout its finish");
        for(var i=0;i<100;i++){motion.Target(i%2==0);motion.Tick(7);Check(motion.Expansion is >=0 and <=1,"rapid horizontal reversals stay bounded");}
        return count;
    }
}

using SideWidget.Core;
using System.Text.Json;
static class FocusTimerChecks
{
 public static int Run()
 {
  var count=0;
  void Check(bool value,string name){if(!value)throw new Exception("Timer: "+name);count++;}
  void Reject(Action action){try{action();}catch(CommandFault){count++;return;}throw new Exception("Timer: invalid input accepted");}
  var now=new DateTimeOffset(2026,11,5,12,0,0,TimeSpan.FromHours(3));
  var timer=new FocusTimer();timer.Start("timer",60,null,now);
  Check(timer.State.Status=="running"&&timer.Remaining(now.AddSeconds(10))==50,"deadline");
  timer.Pause(now.AddSeconds(10));Check(timer.Remaining(now.AddHours(1))==50,"pause freezes");
  timer.Resume(now.AddHours(1));Check(timer.Remaining(now.AddHours(1).AddSeconds(20))==30,"resume shifts deadline");
  Check(timer.Tick(now.AddHours(2))&&timer.State.Status=="ringing","sleep catches deadline");
  timer.Dismiss();Check(timer.State.Status=="idle","dismiss timer");timer.Dismiss();Check(timer.State.Status=="idle","dismiss idempotent");
  timer.Start("alarm",0,"11:59",now);Check(timer.State.Deadline==now.AddDays(1).AddMinutes(-1),"alarm tomorrow UTC comparison");
  timer.Start("timer",1,null,now);var restored=new FocusTimer(JsonSerializer.Deserialize<FocusTimerState>(JsonSerializer.Serialize(timer.State))!);restored.Tick(now.AddDays(1));Check(restored.State.Status=="ringing","restart overdue");
  timer.Reset();timer.Start("pomodoro",0,null,now);
  for(var cycle=1;cycle<=4;cycle++){
   Check(timer.State.Phase=="work"&&timer.Remaining(now)==1500,"work 25");
   timer.Tick(now.AddMinutes(25));Check(timer.State.Status=="ringing","work completion waits");
   timer.Dismiss();Check(timer.State.Status=="ready"&&timer.State.Cycle==cycle,"dismiss only prepares next");
   Check(timer.State.Phase==(cycle==4?"longBreak":"break"),"correct break");
   Check(timer.Remaining(now)==(cycle==4?900:300),"break 5 or 15");
   timer.Resume(now);timer.Tick(now.AddMinutes(15));timer.Dismiss();Check(timer.State.Phase=="work"&&timer.State.Status=="ready","break prepares work");timer.Resume(now);
  }
  Reject(()=>timer.Start("timer",0,null,now));Reject(()=>timer.Start("timer",86401,null,now));Reject(()=>timer.Start("bad",60,null,now));Reject(()=>timer.Start("alarm",0,"25:80",now));
  timer.Reset();Check(timer.State.Status=="idle","reset");
  var eastern=TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
  var autumn=new DateTimeOffset(2026,10,31,12,0,0,TimeSpan.FromHours(-4));
  timer.Start("alarm",0,"09:00",autumn,timeZone:eastern);
  Check(timer.State.Deadline==new DateTimeOffset(2026,11,1,9,0,0,TimeSpan.FromHours(-5)),"alarm DST offset for target day");
  timer.Start("alarm",0,"02:30",new DateTimeOffset(2027,3,13,12,0,0,TimeSpan.FromHours(-5)),timeZone:eastern);
  Check(timer.State.Deadline==new DateTimeOffset(2027,3,14,3,30,0,TimeSpan.FromHours(-4)),"skipped alarm matches JS local normalization");
  timer.Start("timer",1,null,now);var fail=true;FocusTimerState? saved=null;
  var service=new SideWidget.Infrastructure.FocusTimerService(timer.State,value=>{if(fail)throw new IOException("full");saved=value;});
  try{service.Tick(now.AddSeconds(2));}catch(IOException){}
  Check(service.State.Status=="ringing","expiry visible despite storage error");
  fail=false;service.Tick(now.AddSeconds(3));Check(saved?.Status=="ringing","expiry save retries");
  SideWidget.Infrastructure.FocusTimerEntry[] durable=[];
  var many=new SideWidget.Infrastructure.FocusTimerService(durable,items=>{if(fail)throw new IOException("full");durable=items.ToArray();});
  many.Add(t=>t.Start("timer",60,null,now));var first=many.Items[0].Id;
  many.Add(t=>t.Start("timer",30,null,now));var second=many.Items[1].Id;
  many.Add(t=>t.Start("alarm",0,"13:00",now));
  many.Add(t=>t.Start("alarm",0,"13:00",now));
  Check(many.Items.Count==4&&many.Primary?.Id==second,"multiple timers and alarms; header nearest deadline");
  many.Change(t=>t.Pause(now.AddSeconds(10)),first);
  Check(many.Items[0].State.Status=="paused"&&many.Items[1].State.Status=="running","pause targets one id");
  many=new(durable,items=>{if(fail)throw new IOException("full");durable=items.ToArray();});
  Check(many.Items.Count==4&&many.Items[0].State.RemainingSeconds==50,"collection survives restore");
  fail=true;try{many.Change(t=>t.Reset(),second);}catch(IOException){}
  Check(many.Items.Count==4&&many.Items[1].Id==second,"failed delete retains all timers");
  try{many.Add(t=>t.Start("timer",5,null,now));}catch(IOException){}
  Check(many.Items.Count==4,"failed add retains all timers");
  try{many.Tick(now.AddHours(2));}catch(IOException){}
  Check(many.Items.Count(item=>item.State.Status=="ringing")==3&&many.Primary?.State.Status=="ringing","all overdue signals survive write failure");
  fail=false;many.Tick(now.AddHours(2));
  Check(durable.Count(item=>item.State.Status=="ringing")==3,"all expired states saved on retry");
  many.Change(t=>t.Dismiss());
  Check(many.Items.Count==3&&many.Items.Count(item=>item.State.Status=="ringing")==2,"header dismisses only one signal");
  var directory=Path.Combine(Path.GetTempPath(),"sidewidget-timers-"+Guid.NewGuid().ToString("N"));
  try{
   var store=new SideWidget.Infrastructure.Store(directory);
   var legacy=new FocusTimer();legacy.Start("timer",60,null,now);store.Save("personal.focusTimer.v1",legacy.State);
   var migrated=new SideWidget.Infrastructure.FocusTimerService(store);
   Check(migrated.Items.Count==1&&migrated.Items[0].State.Deadline==legacy.State.Deadline,"legacy timer migration retains deadline");
   migrated.Change(t=>t.Reset());
   Check(new SideWidget.Infrastructure.FocusTimerService(store).Items.Count==0,"empty saved collection does not resurrect legacy timer");
  }finally{Directory.Delete(directory,true);}
  return count;
 }
}


using SideWidget.Core;
using SideWidget.Infrastructure;
using Microsoft.Data.Sqlite;
using System.Text;
using System.Text.Json;
if(args is ["--check-codex-discovery"]){
    Environment.SetEnvironmentVariable("CODEX_EXECUTABLE",null);
    Environment.SetEnvironmentVariable("PATH",Environment.GetFolderPath(Environment.SpecialFolder.System));
    var home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex");
    var limits=await new CodexRateLimitsReader(home).ReadAsync();
    if(!limits.Available||limits.Buckets.Count==0)throw new Exception("Installed Codex must supply live limits without PATH or CODEX_EXECUTABLE");
    Console.WriteLine("PASS installed Codex discovery without PATH: live quota available");
    return;
}
if(args is ["--inspect-codex"]){
    var home=Environment.GetEnvironmentVariable("CODEX_HOME")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex");
    var threads=new CodexStateReader(home).Read();var limits=await new CodexRateLimitsReader(home).ReadAsync();
    var boot=WindowsBootSession.Read();
    Console.WriteLine(JsonSerializer.Serialize(new{threads.Available,threadCount=threads.Threads.Count,statuses=threads.Threads.GroupBy(t=>t.Status).ToDictionary(g=>g.Key,g=>g.Count()),limitsAvailable=limits.Available,limits.CheckedAt,limits.Buckets,bootAvailable=boot is not null,bootStable=boot==WindowsBootSession.Read()},Protocol.Json));
    return;
}
DesktopCleanupChecks.Run();
PowerScheduleChecks.Run();
TimerProtocolChecks.Run();
CodexPolicyChecks.Run();
await CodexMonitorServiceChecks.Run();
CodexReaderChecks.Run();
CalendarChecks.Run();
WebsiteMonitorChecks.Run();
var count=0;
void Check(bool v,string name){if(!v)throw new Exception(name);count++;}
void Reject(Action action,string name){try{action();}catch(CommandFault){count++;return;}throw new Exception(name);}
var p=new PanelMotion();p.Target(true);p.Tick(80);Check(p.Height>52&&p.Height<620,"intermediate");var start=p.Height;p.Target(false);Check(p.Height==start,"reverse");p.Tick(300);Check(p.Height==52,"collapse");p.Pinned=true;p.Target(true,400,true);p.Target(false);Check(p.Height==400,"pin");p.Pinned=false;p.Editing=true;p.Target(false);Check(p.Height==400,"editing");p.Editing=false;p.Suspended=true;p.Target(false,immediate:true);p.Target(true);Check(p.Height==52,"suspended");
for(var i=0;i<500;i++){p.Suspended=false;p.Target(i%2==0);p.Tick(9);Check(p.Height is >=52 and <=620,"rapid transitions");}p.Target(false);p.Tick(300);Check(p.Height==52,"settles");
string Envelope(string action,object? args=null)=>JsonSerializer.Serialize(new{protocolVersion=1,requestId=Guid.NewGuid().ToString(),actionId=action,@params=args??new{}},Protocol.Json);
Check(Protocol.Parse(Protocol.Origin,Envelope("state.get")).ActionId=="state.get","command");Reject(()=>Protocol.Parse("https://evil.test",Envelope("state.get")),"origin");Reject(()=>Protocol.Parse(Protocol.Origin,"{}"),"bad JSON");foreach(var old in new[]{"api.execute","files.run","shell.run","profile.add"})Reject(()=>Protocol.Parse(Protocol.Origin,Envelope(old)),"removed "+old);Reject(()=>Protocol.Parse(Protocol.Origin,Envelope("state.get",new{extra=1})),"extra param");Reject(()=>Protocol.Parse(Protocol.Origin,Envelope("shell.editing",new{value=true}).Replace("\"value\":true","\"value\":true,\"value\":false")),"duplicate keys");
Check(HotkeyParser.Parse("Ctrl+Shift+F12").Key==123,"F12");Check(HotkeyParser.Parse("Alt+K").Key==75,"letter hotkey");Reject(()=>HotkeyParser.Parse("A"),"modifier");Reject(()=>HotkeyParser.Parse("Ctrl+Ctrl+A"),"duplicate modifier");Reject(()=>new Preferences(Width:100).Validate(),"width bounds");
foreach(var theme in new[]{"dark","light","system"}){
    var themed=new Preferences(Theme:theme);themed.Validate();
    Check(Protocol.Parse(Protocol.Origin,Envelope("settings.theme",new{theme})).ActionId=="settings.theme","theme command");
    Check(JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(themed,Protocol.Json),Protocol.Json)!.Theme==theme,"theme roundtrip");
}
Check(JsonSerializer.Deserialize<Preferences>("{}",Protocol.Json)!.Theme=="dark","existing settings default dark");
var oldClockSettings=JsonSerializer.Deserialize<Preferences>("{}",Protocol.Json)!;
Check(oldClockSettings.ClockFontSize==22&&oldClockSettings.ClockHeaderHeight==52&&oldClockSettings.CompactClockHeight==23,"old profiles keep clock geometry");
var largerClock=oldClockSettings with{ClockFontSize=34,CompactStripHeight=60};
var restoredClock=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(largerClock,Protocol.Json),Protocol.Json)!;
Check(restoredClock.ClockHeaderHeight==64&&restoredClock.CompactClockHeight==60,"clock preferences survive JSON round trip");
Check((largerClock with{CompactStripHeight=23}).CompactClockHeight>23,"compact strip grows to fit larger clock");
Reject(()=>(largerClock with{ClockFontSize=35}).Validate(),"clock size bounds");
Reject(()=>(largerClock with{CompactStripHeight=121}).Validate(),"compact strip height bounds");
var resizedMotion=new PanelMotion();
resizedMotion.ResizeHeader(64,632);
Check(resizedMotion.Height==64&&resizedMotion.State==PanelState.Collapsed,"clock size grows collapsed panel");
resizedMotion.Target(true,632,true);
resizedMotion.ResizeHeader(44,612);
Check(resizedMotion.Height==612&&resizedMotion.State==PanelState.Expanded,"clock size shrinks expanded panel");
resizedMotion.Target(false,immediate:true);
Check(resizedMotion.Height==44&&resizedMotion.State==PanelState.Collapsed,"collapse uses resized header");
Reject(()=>new Preferences(Theme:"invalid").Validate(),"invalid theme");
var dir=Path.Combine(Path.GetTempPath(),"SideWidget-reset-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);var db=Path.Combine(dir,"sidewidget.db");
Check(WebLink.Validate("https://example.com/a?q=test").Host=="example.com","web link");
foreach(var link in new[]{"file:///C:/Windows/notepad.exe","javascript:alert(1)","ms-settings:sound","https://name:secret@example.com/","https://example.com/\n"})Reject(()=>WebLink.Validate(link),"unsafe web link");
Reject(()=>HotkeyParser.Parse(null!),"null hotkey");
using(var connection=new SqliteConnection("Data Source="+db)){connection.Open();using var command=connection.CreateCommand();command.CommandText="CREATE TABLE settings(key TEXT PRIMARY KEY,value TEXT NOT NULL);INSERT INTO settings VALUES('preferences','{\"width\":480,\"height\":700,\"order\":[\"servers\"]}');INSERT INTO settings VALUES('profiles','[{\"id\":\"keep-me\"}]');";command.ExecuteNonQuery();}
var state=new PersonalState(dir);Check(File.Exists(Path.Combine(dir,"backups","before-product-reset-v2.db")),"backup");Check(state.Store.Read<JsonElement>("profiles",default)[0].GetProperty("id").GetString()=="keep-me","old settings preserved");Check(state.Settings.Width==480&&state.Settings.Order==null,"geometry migrated without old cards");state.AddCalculation("2+2","4");state.RememberColor("#aabbcc");state.FavoriteUnit("m","cm");state.SaveDraft("# Черновик\nНе потерять");state.SaveSettings(new Preferences(Width:420,Position:.25,Hotkey:"Ctrl+Shift+F12"));var reload=new PersonalState(dir);Check(reload.CalculatorHistory.Single().Result=="4","history persists");Check(reload.ColorHistory.Single()=="#AABBCC","color persists");Check(reload.UnitFavorites.Single().From=="m","units persist");Check(reload.DiaryDraft.Contains("Не потерять"),"draft UTF8");Check(reload.Settings.Position==.25&&reload.Settings.Width==420,"placement persists");for(var i=0;i<105;i++)reload.AddCalculation(i.ToString(),i.ToString());Check(reload.CalculatorHistory.Count==100,"history bound");Reject(()=>reload.RememberColor("#INVALID"),"color bound");
var vault=new SecretVault(Path.Combine(dir,"secrets"));var reference=vault.Save("private-test-key");Check(vault.Read(reference)=="private-test-key","DPAPI roundtrip");Check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(dir,"secrets",reference))).Contains("private-test-key"),"encrypted at rest");
var rates=OnlineServices.ParseCurrency(Encoding.UTF8.GetBytes("<ValCurs Date=\"04.11.2026\"><Valute><CharCode>JPY</CharCode><Nominal>100</Nominal><Name>Йена</Name><Value>60,0000</Value></Valute><Valute><CharCode>USD</CharCode><Nominal>1</Nominal><Name>Доллар</Name><Value>90,0000</Value></Valute></ValCurs>"),DateTimeOffset.UtcNow);Check(rates.Rates.Single(r=>r.Code=="JPY").Rate==.6m,"nominal");Check(100*.6m/90m==100*rates.Rates.Single(r=>r.Code=="JPY").Rate/rates.Rates.Single(r=>r.Code=="USD").Rate,"cross rate");Check(rates.Date=="2026-11-04","quote date");
var quota=new Store(Path.Combine(dir,"quota"));var limited=false;try{for(var i=0;i<150;i++)quota.Save("x"+i,new string('x',128*1024));}catch(SqliteException e)when(e.SqliteErrorCode==13){limited=true;}Check(limited,"quota persists per connection");

count += DisplaySelectionChecks.Run();
count += DistributionChecks.Run();
count += AudioSpectrumChecks.Run();
count += VisualizerSettingsChecks.Run();
count += WebTranslationChecks.Run();
count += FocusTimerChecks.Run();
count += StoreChecks.Run();
count += PanelGeometryChecks.Run();
count += TooltipChecks.Run();
count += PanelInteractionChecks.Run();
count += CompactPanelChecks.Run();
count += CodexQuotaIndicatorChecks.Run();
count += PhoneNetworkChecks.Run();
count += LauncherPersistenceChecks.Run();
count += await OnlineChecks.Run();
count += await GitHubUpdateChecks.Run();
Console.WriteLine($"PASS {count} checks, including 500 rapid motion transitions; storage/migration/protocol/DPAPI/currency.");
if (args.Contains("--public-online")) Environment.ExitCode = await OnlineChecks.RunPublic();





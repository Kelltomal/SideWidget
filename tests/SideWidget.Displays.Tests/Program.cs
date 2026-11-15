using Microsoft.UI.Dispatching;
using SideWidget.App;
using SideWidget.Core;

var checks=0;
void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
Check(DesktopLanguage.ResolveSystemLanguage("zh_Hans_CN")=="zh-CN","Weblate Chinese catalog retains existing locale");
Check(DesktopLanguage.ResolveSystemLanguage("pt_BR")=="pt-BR","Weblate Brazilian Portuguese code resolves");
Check(DesktopLanguage.IsSupported("de"),"web catalog is selectable on desktop");
DesktopLanguage.Select("de");
Check(DesktopLanguage.Text("Открыть панель")=="Panel öffnen","native catalog is embedded");
DesktopLanguage.Select("system");
var a=new DesktopDisplay(new("a","Экран 1",true),1,new(){Monitor=new(){Left=0,Top=0,Right=1920,Bottom=1080}});
var b=new DesktopDisplay(new("b","Экран 2",false),2,new(){Monitor=new(){Left=-2560,Top=0,Right=0,Bottom=1440}});
DisplayCatalog.Available=[a,b];
var desktop=new DesktopWindows("test");desktop.Start();
var shared=DesktopState.Last!;
Check(MainWindow.Live.Count==1,"legacy startup opens one curtain");
var primary=MainWindow.Live.Single();
shared.Personal.Settings=new(DisplayIds:["a","b"]);desktop.Synchronize(primary);
DispatcherQueue.Drain();
Check(MainWindow.Live.Count==2&&MainWindow.Live.Select(window=>window.DisplayId).Distinct().Count()==2,"select creates exactly one window per screen");
Check(MainWindow.Trays==1&&MainWindow.Hotkeys==1,"only primary registers tray/hotkey");
var secondary=MainWindow.Live.Single(window=>window!=primary);
Native.Foreground=17;Native.ForegroundMonitor=b.Handle;desktop.CheckActiveDisplayFullscreen();
Check(secondary.FullscreenWindow==17&&primary.FullscreenWindow==0,"fullscreen check targets the active display");
Native.Cursor=new(){X=-200,Y=20};desktop.ToggleAtPointer(123);
Check(secondary.Toggles==1&&secondary.HotkeyReceivedAt==123&&primary.Toggles==0,"hotkey and timestamp target pointer on negative-coordinate display");
Native.Cursor=new(){X=3000,Y=20};desktop.ToggleAtPointer(456);
Check(primary.Toggles==1,"hotkey falls back when pointer has no selected curtain");
desktop.ValidateSettings(new(Hotkey:"Ctrl+Shift+F12"));
Check(primary.ValidatedHotkey=="Ctrl+Shift+F12"&&secondary.ValidatedHotkey==null,"hotkey validation belongs to primary");
desktop.Synchronize(primary);
Check(secondary.Events.Contains("state.changed"),"shared mutation delivered to sibling");
DispatcherQueue.Drain();
shared.Personal.Settings=new(DisplayIds:["b"]);desktop.Synchronize(secondary);
Check(MainWindow.Live.Count==2,"source window remains until deferred refresh");
DispatcherQueue.Drain();
Check(MainWindow.Live.Count==1&&primary.DisplayId=="b"&&!MainWindow.Live.Contains(secondary),"deselect moves owner and closes duplicate");
shared.Personal.Settings=new(DisplayIds:["a","b"]);DisplayCatalog.Available=[a];desktop.QueueRefresh();DispatcherQueue.Drain();
Check(MainWindow.Live.Count==1&&primary.DisplayId=="a","unplug retains available curtain");
Check(desktop.Multiple,"unplug does not turn multi-screen drag into a destructive single-screen transfer");
DisplayCatalog.Available=[b];desktop.QueueRefresh();DispatcherQueue.Drain();
Check(MainWindow.Live.Count==1&&primary.DisplayId=="b","other selected screen becomes available");
shared.Personal.Settings=new(DisplayIds:["missing"]);desktop.QueueRefresh();DispatcherQueue.Drain();
Check(MainWindow.Live.Count==1&&primary.DisplayId=="b"&&shared.Personal.Settings.DisplayIds![0]=="missing","fallback preserves disconnected selection");
Check(!desktop.CanTransferDisplay,"fallback drag must retain unavailable selection");
DisplayCatalog.Available=[];desktop.QueueRefresh();DispatcherQueue.Drain();
Check(MainWindow.Live.Count==1,"transient empty enumeration does not destroy owner");
DisplayCatalog.Available=[a,b];shared.Personal.Settings=new(DisplayIds:["a","b"]);
desktop.QueueRefresh();desktop.QueueRefresh();desktop.QueueRefresh();DispatcherQueue.Drain();
Check(MainWindow.Live.Count==2,"reconnection and repeated notifications do not duplicate windows");
shared.Audio.Raise(.3f,false);DispatcherQueue.Drain();
Check(MainWindow.Live.All(window=>window.Events.Count(type=>type=="audio.level.changed")==1),"single audio subscription broadcasts once per window");
desktop.Stop();DispatcherQueue.Drain();desktop.Stop();
Check(MainWindow.Live.Count==0&&shared.Disposals==1,"shutdown closes all windows and disposes shared services once");
Console.WriteLine($"PASS {checks} display coordinator checks (real coordinator; deterministic native/window adapters, no GUI).");

namespace Microsoft.UI.Dispatching
{
    sealed class DispatcherQueue
    {
        static readonly DispatcherQueue instance=new();static readonly Queue<Action> pending=[];
        public static DispatcherQueue GetForCurrentThread()=>instance;
        public bool TryEnqueue(Action action){pending.Enqueue(action);return true;}
        public static void Drain(){while(pending.TryDequeue(out var action))action();}
    }
}
namespace Microsoft.UI.Xaml {sealed class Application {public static Application Current {get;}=new();public void Exit(){}}}
namespace SideWidget.App
{
    sealed class DesktopState
    {
        public static DesktopState? Last;
        public PersonalData Personal {get;}=new();
        public FakeAudio Audio {get;}=new();public FakeSpectrum Spectrum {get;}=new();public int Disposals;
        public void Dispose()=>Disposals++;
        public DesktopState(string directory){Last=this;}
        // Set by test window on first construction, before any assertions.
        public sealed class PersonalData {public Preferences Settings {get;set;}=new();}
        public sealed class FakeSpectrum {public void SetEnabled(bool enabled){}}
        public sealed class FakeAudio {public event Action<float,bool>? LevelChanged;public void Raise(float level,bool muted)=>LevelChanged?.Invoke(level,muted);}
    }
    sealed class MainWindow
    {
        readonly DesktopWindows desktop;readonly bool primary;
        public static readonly List<MainWindow> Live=[];public static int Trays,Hotkeys;
        public string DisplayId {get;private set;}public List<string> Events=[];public int Toggles;public string? ValidatedHotkey;
        public MainWindow(DesktopWindows desktop,DesktopState state,string id,bool primary){this.desktop=desktop;this.primary=primary;DisplayId=id;Live.Add(this);DesktopState.Last=state;if(primary){Trays++;Hotkeys++;}}
        public void ShowWithoutActivation(){}public void RefreshSharedSettings(){}public object Snapshot()=>new{};
        public void DeliverEvent(string type,object data)=>Events.Add(type);
        public void AssignDisplay(string id)=>DisplayId=id;
        public void Close(){Live.Remove(this);desktop.WindowClosed(this,primary);}
        public void ValidateHotkey(string shortcut)=>ValidatedHotkey=shortcut;
        public long HotkeyReceivedAt {get;private set;}
        public nint FullscreenWindow {get;private set;}
        public static bool IsSideWidgetWindow(nint window)=>false;
        public void CheckFullscreen(nint window,Native.Rect monitor)=>FullscreenWindow=window;
        public void ToggleFromHotkey(long receivedAt){Toggles++;HotkeyReceivedAt=receivedAt;}
        public void OpenFromTray(){}
    }
    sealed record DesktopDisplay(DisplayDescriptor Descriptor,nint Handle,Native.MonitorInfo Bounds)
    {
        public string Id => Descriptor.Id;
    }
    static class DisplayCatalog {public static IReadOnlyList<DesktopDisplay> Available=[];public static IReadOnlyList<DesktopDisplay> Read()=>Available;}
    static class Native
    {
        public struct Rect {public int Left,Top,Right,Bottom;}
        public struct Point {public int X,Y;}
        public struct MonitorInfo {public Rect Monitor;}
        public static nint Foreground,ForegroundMonitor;
        public static nint GetForegroundWindow()=>Foreground;
        public static nint MonitorFromWindow(nint window,uint flags)=>ForegroundMonitor;
        public static Point Cursor;public static bool GetCursorPos(out Point point){point=Cursor;return true;}
    }
}

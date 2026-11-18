using Microsoft.UI.Dispatching;
using SideWidget.Core;

namespace SideWidget.App;

sealed class DesktopWindows
{
    readonly DesktopState state;
    readonly List<MainWindow> windows=[];
    IReadOnlyList<DesktopDisplay> displays=[];
    FullscreenClockWindow? fullscreenClock;
    bool refreshing,queued,stopping;
    readonly DispatcherQueue dispatcher=DispatcherQueue.GetForCurrentThread();
    public IReadOnlyList<DisplayDescriptor> Displays=>displays.Select(display=>display.Descriptor).ToArray();
    public bool Multiple=>windows.Count>1||state.Personal.Settings.DisplayIds?.Length>1;
    public bool CanTransferDisplay=>!Multiple&&(state.Personal.Settings.DisplayIds is null||state.Personal.Settings.DisplayIds.All(id=>displays.Any(display=>display.Id==id)));
    public DesktopWindows(string directory)
    {
        state=new(directory);
        DesktopLanguage.Select(state.Personal.Settings.Language);
        state.Audio.LevelChanged+=(volume,muted)=>dispatcher.TryEnqueue(()=>{
            if(!stopping&&windows.FirstOrDefault() is {} source)Emit(source,"audio.level.changed",new{volume,muted});
        });
    }

    public void Start()=>Refresh();
    public void QueueRefresh()
    {
        if(queued||stopping)return;queued=true;
        dispatcher.TryEnqueue(()=>{queued=false;if(!stopping)Refresh();});
    }
    void Refresh()
    {
        if(refreshing||stopping)return;refreshing=true;
        try
        {
            var next=DisplayCatalog.Read();if(next.Count==0)return;
            displays=next;
            var selected=DisplaySelection.Resolve(state.Personal.Settings,Displays);
            var primary=windows.FirstOrDefault();
            if(primary==null)
            {
                primary=new MainWindow(this,state,selected[0],true);windows.Add(primary);primary.ShowWithoutActivation();
            }
            // Reuse the tray/hotkey owner, retaining it on its existing display if still selected.
            var primaryId=selected.Contains(primary.DisplayId)?primary.DisplayId:selected[0];
            var stale=windows.Skip(1).Where(window=>!selected.Contains(window.DisplayId)||window.DisplayId==primaryId).ToArray();
            foreach(var window in stale){windows.Remove(window);window.Close();}
            primary.AssignDisplay(primaryId);
            foreach(var id in selected.Where(id=>!windows.Any(window=>window.DisplayId==id)))
            {
                var window=new MainWindow(this,state,id,false);windows.Add(window);window.ShowWithoutActivation();
            }
            foreach(var window in windows){window.RefreshSharedSettings();window.DeliverEvent("displays.changed",Displays);}
        }
        finally{refreshing=false;}
    }
    public Native.MonitorInfo Monitor(string id)
        =>displays.FirstOrDefault(display=>display.Id==id)?.Bounds??displays.First().Bounds;
    public DesktopDisplay? Display(nint handle)=>displays.FirstOrDefault(display=>display.Handle==handle);

    public void CheckActiveDisplayFullscreen()
    {
        var foreground=Native.GetForegroundWindow();
        if(foreground==0||MainWindow.IsSideWidgetWindow(foreground))return;
        var active=Display(Native.MonitorFromWindow(foreground,2));
        if(active is null)return;
        windows.FirstOrDefault(window=>window.DisplayId==active.Id)?.CheckFullscreen(foreground,active.Bounds.Monitor);
    }

    public void ValidateSettings(Preferences settings)=>windows.FirstOrDefault()?.ValidateHotkey(settings.Hotkey);

    public void Emit(MainWindow source,string type,object data)
    {
        if(stopping)return;
        if(type=="settings.changed")
        {
            foreach(var window in windows.ToArray())window.RefreshSharedSettings();
            QueueRefresh();
        }
        var shared=type is "settings.changed" or "timer.changed" or "power.changed" or "calendar.changed" or "calendar.reminder" or "launcher.changed" or "audio.level.changed";
        foreach(var window in windows.ToArray())if(shared||window==source)window.DeliverEvent(type,data);
        if(type=="launcher.changed")Synchronize(source);
    }
    public void Synchronize(MainWindow source)
    {
        if(stopping)return;
        state.Spectrum.SetEnabled((state.Personal.Settings.Visualizer??new()).Enabled);
        foreach(var window in windows.ToArray())
        {
            window.RefreshSharedSettings();
            if(window!=source)window.DeliverEvent("state.changed",window.Snapshot());
        }
        QueueRefresh();
    }
    MainWindow? AtPointer()
    {
        Native.GetCursorPos(out var point);
        return windows.FirstOrDefault(window=>{var bounds=Monitor(window.DisplayId).Monitor;return point.X>=bounds.Left&&point.X<bounds.Right&&point.Y>=bounds.Top&&point.Y<bounds.Bottom;})??windows.FirstOrDefault();
    }
    public void ToggleAtPointer(long receivedAt)
    {
        if(fullscreenClock is not null){fullscreenClock.Close();return;}
        AtPointer()?.ToggleFromHotkey(receivedAt);
    }
    public void ShowFullscreenClockAtPointer(Native.Point point,bool light)
    {
        var display=displays.FirstOrDefault(display=>
        {
            var bounds=display.Bounds.Monitor;
            return point.X>=bounds.Left&&point.X<bounds.Right&&point.Y>=bounds.Top&&point.Y<bounds.Bottom;
        })??displays.First();
        ShowFullscreenClock(display.Bounds.Monitor,light);
    }
    public void ShowFullscreenClock(Native.Rect monitor,bool light)
    {
        if(stopping)return;
        fullscreenClock?.Close();
        var window=new FullscreenClockWindow(light);
        fullscreenClock=window;
        window.Closed+=(_,_)=>{if(fullscreenClock==window)fullscreenClock=null;};
        try{window.ShowOn(monitor);}
        catch{window.Close();throw;}
    }
    public void OpenAtPointer()=>AtPointer()?.OpenFromTray();
    public void WindowClosed(MainWindow window,bool primary)
    {
        windows.Remove(window);if(primary&&!stopping)Stop();
    }
    public void Stop()
    {
        if(stopping)return;stopping=true;
        fullscreenClock?.Close();
        foreach(var window in windows.ToArray())window.Close();windows.Clear();state.Dispose();
        Microsoft.UI.Xaml.Application.Current.Exit();
    }
}

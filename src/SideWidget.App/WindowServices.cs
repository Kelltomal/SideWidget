using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using SideWidget.Core;

namespace SideWidget.App;
public sealed partial class MainWindow
{
    Action? focusNativeDialog;
    readonly bool openingDiagnostics=Environment.GetCommandLineArgs().Contains("--measure-opening");
    long searchFocusId,searchOpenedAt;
    bool pendingSearchFocus;
    readonly uint taskbarCreated=Native.RegisterWindowMessage("TaskbarCreated");
    void OpenInteractive(bool immediate=false)
    {
        if(compactPanel.StripVisible)return;
        UpdateCompactSurface();
        outsideMs=0;motion.Suspended=false;EnterInteractive();
        var instant=immediate||ReducedMotion();motion.Target(true,MaximumHeight(),instant);
        if(instant)ApplyGeometry();
        else previous=watch.ElapsedMilliseconds;
        SendEvent("panel",new{expanded=true,pinned=motion.Pinned});
    }
    void Collapse(){if(dialogCount>0)return;pendingSearchFocus=false;outsideMs=0;webEditing=false;motion.Editing=false;motion.Pinned=false;motion.Target(false,immediate:true);ApplyGeometry();SendEvent("panel",new{expanded=false,pinned=false});RestoreFocus();compactPanel.Fold();UpdateCompactSurface();}
    void Shell(string action)
    {
        switch(action){case "interactive":EnterInteractive();break;case "editing":webEditing=true;break;case "idle":webEditing=false;break;case "pin":motion.Pinned=!motion.Pinned;SendEvent("panel",new{expanded=true,pinned=motion.Pinned});break;case "collapse":Collapse();break;}
        motion.Editing=webEditing||dialogCount>0||startupUpdateOffer is not null;
    }
    internal void CheckFullscreen(nint foreground,Native.Rect monitor)
    {
        if(dialogCount>0||dragging||resizing||startupUpdateOffer is not null)return;
        var wasCovered=compactPanel.Covered;
        compactPanel.ObserveWindow(true,CoversDisplay(foreground,monitor));
        if(wasCovered!=compactPanel.Covered)UpdateCompactSurface();
    }

    internal static bool IsSideWidgetWindow(nint window)
    {
        Native.GetWindowThreadProcessId(window,out var processId);
        return processId==Environment.ProcessId;
    }

    static bool CoversDisplay(nint window,Native.Rect monitor)
    {
        if(window==Native.GetShellWindow()||!Native.IsWindowVisible(window)||Native.IsIconic(window))return false;
        if(Native.DwmGetWindowAttribute(window,14,out uint cloaked,sizeof(uint))==0&&cloaked!=0)return false;
        if(Native.IsZoomed(window))return true;
        if(Native.DwmGetWindowAttribute(window,9,out Native.Rect bounds,Marshal.SizeOf<Native.Rect>())!=0&&
            !Native.GetWindowRect(window,out bounds))return false;
        return bounds.Left<=monitor.Left&&bounds.Top<=monitor.Top&&bounds.Right>=monitor.Right&&bounds.Bottom>=monitor.Bottom;
    }
    string? registeredHotkey;
    void RegisterHotkey(string shortcut)
    {
        var hotkey=HotkeyParser.Parse(shortcut);
        if(!Native.RegisterHotKey(hwnd,1,hotkey.Modifiers,hotkey.Key))startupError="Горячая клавиша занята. Выберите другую в настройках.";
        else registeredHotkey=shortcut;
    }
    internal void ValidateHotkey(string shortcut)
    {
        if(!primaryWindow||shortcut==registeredHotkey)return;
        var previousShortcut=registeredHotkey;
        Native.UnregisterHotKey(hwnd,1);registeredHotkey=null;startupError=null;RegisterHotkey(shortcut);
        if(startupError!=null){if(previousShortcut!=null)RegisterHotkey(previousShortcut);throw new CommandFault("hotkey_conflict","Горячая клавиша занята другим приложением");}
    }
    internal void RefreshSharedSettings()
    {
        if(closing)return;
        RefreshNativeLanguage();
        UpdateClock();
        RefreshAudioVisualizerSettings();ApplyTheme();Place();
        if(motion.State!=PanelState.Collapsed){motion.Target(true,MaximumHeight(),true);ApplyGeometry();}
        UpdateCompactSurface();
    }
    internal void ToggleFromHotkey(long receivedAt)
    {
        if(motion.State!=PanelState.Collapsed){Collapse();return;}
        compactPanel.Reveal();
        searchOpenedAt=receivedAt;
        searchFocusId++;
        pendingSearchFocus=controller.Settings.Disabled?.Contains("search")!=true;
        OpenInteractive(true);
        RecordOpening("hotkey.expanded",searchOpenedAt,searchFocusId);
        SendSearchFocus();
    }
    void SendSearchFocus()
    {
        if(!pendingSearchFocus||!controller.Ready||motion.State!=PanelState.Expanded||dialogCount!=0)return;
        web.Focus(FocusState.Programmatic);
        SendEvent("search.focus",new{id=searchFocusId});
    }
    void RecordOpening(string stage,long? from=null,long? id=null)
    {
        if(!openingDiagnostics)return;
        using var process=Process.GetCurrentProcess();
        var elapsedMs=from is long start?Stopwatch.GetElapsedTime(start).TotalMilliseconds:
            (DateTime.UtcNow-process.StartTime.ToUniversalTime()).TotalMilliseconds;
        try
        {
            File.AppendAllText(Path.Combine(controller.Store.DirectoryPath,"opening.ndjson"),
                JsonSerializer.Serialize(new{stage,elapsedMs,id,pid=process.Id,displayId=DisplayId,primary=primaryWindow,foreground=Native.GetForegroundWindow()==hwnd})+"\n");
        }
        catch(IOException){}
        catch(UnauthorizedAccessException){}
    }
    internal void OpenFromTray()=>OpenInteractive();
    nint trayIcon;
    void AddTray()
    {
        if(trayIcon==0)trayIcon=Native.LoadImage(0,Path.Combine(AppContext.BaseDirectory,"Assets","Icons","sidemenu.ico"),1,Native.GetSystemMetrics(49),Native.GetSystemMetrics(50),0x10);
        tray=new(){Size=(uint)Marshal.SizeOf<Native.NotifyIcon>(),Hwnd=hwnd,Id=1,Flags=7,Callback=0x8001,Icon=trayIcon!=0?trayIcon:Native.LoadIcon(0,new nint(32512)),Tip=DesktopLanguage.Text("SideMenu — открыть или выйти"),Info="",InfoTitle=""};
        Native.Shell_NotifyIcon(0,ref tray);
    }
    nint WindowProcedure(nint window,uint message,nuint wParam,nint lParam,nuint id,nuint data)
    {
        // Own the complete window rectangle. WinUI can restore WS_DLGFRAME on
        // activation; suppressing non-client sizing keeps its bevel out of view.
        if(message==0x0083&&wParam!=0)return 0;
        // WinUI restores extended edge styles during activation. Keep the actual
        // client area borderless, including after focus and presenter changes.
        if(message==0x007c&&unchecked((int)wParam)==-20&&lParam!=0)
        {
            var styles=Marshal.PtrToStructure<Native.StyleChange>(lParam);
            styles.NewStyle&=~0x00000301u;
            Marshal.StructureToPtr(styles,lParam,false);
        }
        if(message==taskbarCreated&&primaryWindow)AddTray();
        if(message==0x0312&&primaryWindow){desktop.ToggleAtPointer(Stopwatch.GetTimestamp());return 0;}
        if(message==0x8001&&primaryWindow)
        {
            if((int)lParam==0x0202)desktop.OpenAtPointer();
            if((int)lParam==0x0205)
            {
                var menu=Native.CreatePopupMenu();
                Native.AppendMenu(menu,0,1,DesktopLanguage.Text("Открыть панель"));
                Native.AppendMenu(menu,0,6,DesktopLanguage.Text("Часы на весь экран"));
                Native.AppendMenu(menu,0,2,DesktopLanguage.Text(motion.Pinned?"Открепить":"Закрепить"));
                Native.AppendMenu(menu,0,4,DesktopLanguage.Text("Перезапустить"));
                Native.AppendMenu(menu,0,5,DesktopLanguage.Text("Запустить новую сборку"));
                Native.AppendMenu(menu,0,3,DesktopLanguage.Text("Выйти"));
                Native.GetCursorPos(out var point);
                Native.SetForegroundWindow(hwnd);
                var selected=Native.TrackPopupMenu(menu,0x100|0x2,point.X,point.Y,0,hwnd,0);
                Native.DestroyMenu(menu);
                if(selected==1)desktop.OpenAtPointer();
                if(selected==6)desktop.ShowFullscreenClockAtPointer(point,currentLightTheme==true);
                if(selected==2){motion.Pinned=!motion.Pinned;OpenInteractive();}
                if(selected==4){Program.RestartRequested=true;desktop.Stop();}
                if(selected==5)DispatcherQueue.TryEnqueue(async()=>await LaunchNewBuild());
                if(selected==3)desktop.Stop();
            }
            return 0;
        }
        if(message is 0x007e or 0x0218)desktop.QueueRefresh();
        if(message==0x02e0)DispatcherQueue.TryEnqueue(Place);
        if(message==0x0021)return new nint(3);
        return Native.DefSubclassProc(window,message,wParam,lParam);
    }
    async Task InitializeWeb()
    {
        if(web.CoreWebView2!=null)return;
        try
        {
            var environment=await CoreWebView2Environment.CreateWithOptionsAsync(BundledBrowser.Folder(),Path.Combine(controller.Store.DirectoryPath,"WebView2"),new CoreWebView2EnvironmentOptions());
            await web.EnsureCoreWebView2Async(environment);
            var core=web.CoreWebView2!;core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.IsStatusBarEnabled=false;core.Settings.AreBrowserAcceleratorKeysEnabled=false;core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;
            core.SetVirtualHostNameToFolderMapping("sidewidget.local",Path.Combine(AppContext.BaseDirectory,"Web"),CoreWebView2HostResourceAccessKind.DenyCors);
            
            core.NavigationStarting+=(_,e)=>{if(!e.Uri.StartsWith(Protocol.Origin+"/",StringComparison.Ordinal))e.Cancel=true;else controller.Ready=false;};
            core.NewWindowRequested+=(_,e)=>e.Handled=true;
            core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
            core.DownloadStarting+=(_,e)=>e.Cancel=true;
            core.WebMessageReceived+=(_,e)=>{
                // Finish the WebView2 callback before opening native modal UI.
                // Nested modal work inside the callback is unsupported reentrancy.
                var origin=e.Source;var json=e.WebMessageAsJson;
                DispatcherQueue.TryEnqueue(async()=>{if(!closing)await Message(origin,json);});
            };
            core.ProcessFailed+=(_,_)=>{controller.Ready=false;startupError="Интерфейс перезапущен после ошибки WebView2";core.Reload();};
            await core.AddScriptToExecuteOnDocumentCreatedAsync("document.addEventListener('DOMContentLoaded',()=>document.documentElement.dataset.theme='"+(currentLightTheme==true?"light":"dark")+"',{once:true});");
            core.Navigate(Protocol.Origin+"/index.html?theme="+controller.Settings.Theme+"&windowId="+Uri.EscapeDataString(DisplayId));
        }
        catch(Exception e)
        {
            var error=new TextBlock{Text=DesktopLanguage.Text("Не удалось запустить интерфейс WebView2.\nРаспакуйте полную сборку SideWidget в локальную папку, доступную для записи. Сохраните папку WebView2Runtime рядом с приложением.\n\nТип ошибки: {0}",e.GetType().Name),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(16),Foreground=Brush("F2F4F5")};Grid.SetRow(error,1);root.Children.Add(error);
        }
    }
    async Task Message(string origin,string json)
    {
        Command? command=null;
        try{command=Protocol.Parse(origin,json);
            if(command.ActionId=="shell.homeReady")
            {
                if(controller.Ready)
                {
                    RecordOpening("home.ready");
                    _ = MaybeShowStartupUpdate();
                }
            }
            else if(command.ActionId=="shell.searchFocused")
            {
                if(pendingSearchFocus&&command.Params.TryGetProperty("id",out var id)&&id.ValueKind==JsonValueKind.Number&&id.TryGetInt64(out var focusedId)&&focusedId==searchFocusId)
                {
                    pendingSearchFocus=false;
                    RecordOpening("hotkey.searchFocused",searchOpenedAt,focusedId);
                }
            }
            else
            {
                var data=await controller.Handle(command);
                Send(new{protocolVersion=1,requestId=command.RequestId,status="result",data});
                if(command.ActionId is "settings.save" or "settings.theme" or "settings.clock" or "settings.language" or "visualizer.settings" or "history.add" or "history.delete" or "history.clear" or "color.remember" or "units.favorite" or "diary.draft" or "apps.undo" or "apps.add" or "apps.pickWindow" or "apps.icon" or "apps.remove" or "apps.rename" or "apps.reorder")desktop.Synchronize(this);
                if(command.ActionId is "updates.skip" or "updates.defer")
                {
                    startupUpdateOffer=null;
                    motion.Editing=webEditing||dialogCount>0;
                }
                if(command.ActionId=="bridge.ready")
                {
                    SendEvent("panel",new{expanded=motion.State!=PanelState.Collapsed,pinned=motion.Pinned});
                    SendSearchFocus();
                    if(startupError!=null)SendEvent("notice",new{message=DesktopLanguage.Text(startupError)});
                }
                return;
            }
            Send(new{protocolVersion=1,requestId=command.RequestId,status="result",data=new{}});
        }
        catch(CommandFault e){if(command!=null)Send(new{protocolVersion=1,requestId=command.RequestId,status="error",error=new{code=e.Code,message=DesktopLanguage.Text(e.Message)}});}
        catch(Exception){if(command!=null)Send(new{protocolVersion=1,requestId=command.RequestId,status="error",error=new{code="internal",message=DesktopLanguage.Text("Не удалось выполнить действие. Проверьте параметры и повторите действие.")}});}
    }
    void Send(object value){if(!closing&&web.CoreWebView2!=null&&web.Source?.GetLeftPart(UriPartial.Authority)==Protocol.Origin)web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(value,Protocol.Json));}
    void SendEvent(string type,object data)=>Send(new{protocolVersion=1,type,sequence=++sequence,time=DateTimeOffset.UtcNow,data});
    async Task<ContentDialogResult> ShowNativeDialog(ContentDialog dialog)
    {
        // Keep protected input in a native view constrained to the real client area,
        // with the WebView hidden until the form has completed.
        var completion=new TaskCompletionSource<ContentDialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var layout=new Grid{Padding=new Thickness(20),RowSpacing=16,Background=root.Background,RequestedTheme=root.RequestedTheme,TabFocusNavigation=Microsoft.UI.Xaml.Input.KeyboardNavigationMode.Cycle};
        layout.RowDefinitions.Add(new(){Height=GridLength.Auto});layout.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});layout.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var title=new TextBlock{Text=dialog.Title?.ToString()??"SideWidget",FontFamily=dialog.FontFamily,FontSize=20,TextWrapping=TextWrapping.Wrap};layout.Children.Add(title);
        var content=dialog.Content;dialog.Content=null;
        var scroll=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(scroll,1);layout.Children.Add(scroll);
        var buttons=new Grid{ColumnSpacing=8};buttons.ColumnDefinitions.Add(new());buttons.ColumnDefinitions.Add(new());
        var save=new Button{Content=dialog.PrimaryButtonText,FontFamily=dialog.FontFamily,HorizontalAlignment=HorizontalAlignment.Stretch,IsEnabled=dialog.IsPrimaryButtonEnabled};
        var enabledToken=dialog.RegisterPropertyChangedCallback(ContentDialog.IsPrimaryButtonEnabledProperty,(_,_)=>save.IsEnabled=dialog.IsPrimaryButtonEnabled);
        var cancel=new Button{Content=dialog.CloseButtonText,FontFamily=dialog.FontFamily,HorizontalAlignment=HorizontalAlignment.Stretch};
        save.Click+=(_,_)=>completion.TrySetResult(ContentDialogResult.Primary);cancel.Click+=(_,_)=>completion.TrySetResult(ContentDialogResult.None);
        Grid.SetColumn(cancel,1);buttons.Children.Add(save);buttons.Children.Add(cancel);Grid.SetRow(buttons,2);layout.Children.Add(buttons);
        layout.KeyDown+=(_,e)=>{if(e.Key==Windows.System.VirtualKey.Escape){e.Handled=true;completion.TrySetResult(ContentDialogResult.None);}};
        Grid.SetRow(layout,1);root.Children.Add(layout);web.Visibility=Visibility.Collapsed;
        focusNativeDialog=()=>cancel.Focus(FocusState.Programmatic);
        cancel.Focus(FocusState.Programmatic);
        try{return await completion.Task;}
        finally{focusNativeDialog=null;dialog.UnregisterPropertyChangedCallback(ContentDialog.IsPrimaryButtonEnabledProperty,enabledToken);root.Children.Remove(layout);web.Visibility=Visibility.Visible;web.Focus(FocusState.Programmatic);}
    }
    async Task<bool> Confirm(string title,string text)
    {
        await dialogs.WaitAsync();dialogCount++;motion.Editing=true;OpenInteractive(true);
        try{var dialog=new ContentDialog{XamlRoot=root.XamlRoot,FontFamily=new Microsoft.UI.Xaml.Media.FontFamily("ms-appx:///Assets/Fonts/MedeoSans.ttf#Medeo Sans"),Title=DesktopLanguage.Text(title),Content=new ScrollViewer{MaxHeight=320,Content=new TextBlock{Text=DesktopLanguage.Text(text),TextWrapping=TextWrapping.Wrap,FontSize=14}},PrimaryButtonText=DesktopLanguage.Text("Подтвердить"),CloseButtonText=DesktopLanguage.Text("Отмена"),DefaultButton=ContentDialogButton.Close,RequestedTheme=ElementTheme.Dark};return await ShowNativeDialog(dialog)==ContentDialogResult.Primary;}
        finally{dialogCount--;motion.Editing=webEditing||dialogCount>0;dialogs.Release();}
    }
}



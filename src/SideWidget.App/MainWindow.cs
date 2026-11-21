using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SideWidget.Core;
using Windows.UI;

namespace SideWidget.App;
public sealed partial class MainWindow : Window
{
    readonly PanelMotion motion = new();
    readonly nint hwnd;
    readonly Grid root = new();
    readonly TextBlock clock = new();
    readonly TextBlock day = new();
    readonly TextBlock date = new();
    readonly DispatcherTimer pulse = new() { Interval = TimeSpan.FromMilliseconds(100) };
    readonly Stopwatch watch = Stopwatch.StartNew();
    readonly ScaleTransform headerGripScale = new();
    readonly Grid header = new() { Padding = new Thickness(16,0,16,0), HorizontalAlignment=HorizontalAlignment.Left,
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
    readonly HeaderGesture headerGesture = new();
    int hostX;
    (int X,int Y,int Width,int Height)? appliedHost;
    readonly Controller controller;
    readonly DesktopWindows desktop;
    readonly bool primaryWindow;
    internal string DisplayId {get;private set;}
    readonly WebView2 web=new();
    readonly Grid resizeGrip;
    readonly Native.SubclassProc subclass;
    readonly SemaphoreSlim dialogs=new(1);
    Native.NotifyIcon tray;
    long sequence;
    int dialogCount;
    bool webEditing,closing,dragging,dragMoved;
    int dragPointerX,dragWindowX;
    string? startupError;
    string? nativeLanguage;
    long previous;
    double outsideMs;
    double scale = 1;
    int x, y, width;
    nint previousFocus;
    internal MainWindow(DesktopWindows desktop,DesktopState state,string displayId,bool primary)
    {
        Title = "SideWidget";
        this.desktop=desktop;primaryWindow=primary;DisplayId=displayId;
        controller=new(state);controller.Displays=()=>desktop.Displays;
        root.Background = Brush("181A1C"); root.RequestedTheme = ElementTheme.Dark;
        root.RowDefinitions.Add(new() { Height = new GridLength(controller.Settings.ClockHeaderHeight) });
        root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new() { Width = new GridLength(1,GridUnitType.Star) });
        header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new() { Width = new GridLength(1,GridUnitType.Star) });
        var font = new FontFamily("ms-appx:///Assets/Fonts/MedeoSans.ttf#Medeo Sans");
        day.FontFamily=font; clock.FontFamily=font; date.FontFamily=font;
        day.VerticalAlignment = VerticalAlignment.Center; day.FontSize=13; day.Foreground=Brush("A9B1BA");
        clock.Foreground=Brush("F2F4F5"); clock.VerticalAlignment=VerticalAlignment.Center; clock.FontSize=22; clock.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold; Grid.SetColumn(clock,1);
        date.HorizontalAlignment=HorizontalAlignment.Right; date.VerticalAlignment=VerticalAlignment.Center; date.FontSize=13; date.Foreground=Brush("A9B1BA"); Grid.SetColumn(date,2);
        header.Children.Add(day); header.Children.Add(clock); header.Children.Add(date);
        var grip = new Border
        {
            Width = 26, Height = 3, CornerRadius = new CornerRadius(2), Background = Brush("69727B"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 3), IsHitTestVisible = false,
            RenderTransform = headerGripScale, RenderTransformOrigin = new(.5, .5)
        };
        Grid.SetColumn(grip, 1);
        header.Children.Add(grip);
        InitializeTimerHeader(header);
        InitializeCodexHeader();
        InitializeUpdateHeader(font);
        header.PointerPressed += HeaderPressed; header.PointerMoved += HeaderMoved; header.PointerReleased += HeaderReleased; header.PointerCaptureLost += (_,_)=>FinishDrag(false);
        root.Children.Add(header);
        root.AddHandler(UIElement.PointerReleasedEvent,new Microsoft.UI.Xaml.Input.PointerEventHandler(PanelPointerReleased),true);
        var cards = new Grid { Padding=new Thickness(16,0,16,16), ColumnSpacing=8 };
        cards.ColumnDefinitions.Add(new()); cards.ColumnDefinitions.Add(new());
        for(var i=0;i<2;i++) { var card = new Border { Background=Brush("22272B"),CornerRadius=new CornerRadius(10),Height=160,VerticalAlignment=VerticalAlignment.Top }; Grid.SetColumn(card,i); cards.Children.Add(card); }
        Grid.SetRow(cards,1);
        if(Environment.GetCommandLineArgs().Contains("--prototype")) root.Children.Add(cards);
        else { Grid.SetRow(web,1);root.Children.Add(web);web.DefaultBackgroundColor=Color.FromArgb(255,24,26,28);web.Loaded+=async(_,_)=>await InitializeWeb(); }
        resizeGrip=new Grid{Width=24,Height=24,Background=new SolidColorBrush(Color.FromArgb(1,24,26,28)),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom};
        resizeGrip.Children.Add(new TextBlock{Text="\uE784",FontFamily=new FontFamily("Segoe Fluent Icons"),FontSize=12,Foreground=Brush("A9B1BA"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false});
        resizeGrip.PointerPressed+=ResizePressed;resizeGrip.PointerMoved+=ResizeMoved;resizeGrip.PointerReleased+=(_,_)=>FinishResize(false);resizeGrip.PointerCaptureLost+=(_,_)=>FinishResize(false);Grid.SetRow(resizeGrip,1);root.Children.Add(resizeGrip);
        Content=root;
        InitializeTheme();
        hwnd=WinRT.Interop.WindowNative.GetWindowHandle(this);
        var presenter = Microsoft.UI.Windowing.OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false,false); presenter.IsResizable=false; presenter.IsMaximizable=false; presenter.IsMinimizable=false;
        AppWindow.SetPresenter(presenter);
        var inspect = Environment.GetCommandLineArgs().Contains("--inspect");
        AppWindow.IsShownInSwitchers=inspect;
        // Presenter leaves WS_EX_WINDOWEDGE behind even with border/title bar hidden.
        // Remove the non-client bevel, retaining tool-window and activation behavior.
        var extendedStyle=Native.GetWindowLong(hwnd,-20)&~0x00000301;
        Native.SetWindowLong(hwnd,-20,inspect ? (extendedStyle|0x08000000)&~0x80 : (extendedStyle|0x08000080)&~0x40000);
        Native.SetWindowLong(hwnd,-16,Native.GetWindowLong(hwnd,-16)&~0x00c40000);
        // Flush the cached non-client frame after changing window styles, before the first XAML render.
        Native.SetWindowPos(hwnd,0,0,0,0,0,0x0001|0x0002|0x0004|0x0010|0x0020);
        subclass=WindowProcedure;Native.SetWindowSubclass(hwnd,subclass,1,0);
        controller.Confirm=Confirm;controller.AddApp=AddApplication;controller.ChangeAppIcon=ChangeApplicationIcon;controller.PickWindow=PickApplicationWindow;controller.PickColor=PickScreenColor;controller.ConfigureTranslation=ConfigureTranslation;controller.AllowTranslationMicrophone=AllowTranslationMicrophone;controller.TranslateWebsite=TranslateWebsite;controller.CancelWebsiteTranslation=CancelWebsiteTranslation;controller.SaveQr=SaveQr;controller.PrintQr=PrintQr;controller.CopyText=CopyText;controller.CopyImage=CopyImage;controller.SettingsChanged=desktop.ValidateSettings;
        controller.PickFolder=PickDesktopFolder;
        controller.ShowFullscreenClock=()=>desktop.ShowFullscreenClock(Monitor().Monitor,currentLightTheme==true);
        root.AllowDrop=true;root.DragOver+=AppDragOver;root.Drop+=AppDrop;
        controller.Emit=(type,data)=>DispatcherQueue.TryEnqueue(()=>{if(!closing)desktop.Emit(this,type,data);});controller.Shell=Shell;controller.Tooltip=SetTooltip;
        controller.InitializePhoneRemote(work =>
        {
            var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (closing || !DispatcherQueue.TryEnqueue(async () =>
            {
                try { if (closing) throw new ObjectDisposedException("SideWidget"); completion.TrySetResult(await work()); }
                catch (Exception error) { completion.TrySetException(error); }
            })) completion.TrySetException(new ObjectDisposedException("SideWidget"));
            return completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
        });
        var corner=1; Native.DwmSetWindowAttribute(hwnd,33,ref corner,4);
        var borderColor=unchecked((int)0xFFFFFFFE);Native.DwmSetWindowAttribute(hwnd,34,ref borderColor,4);
        InitializeAudioVisualizer();
        controller.VisualizerChanged=RefreshAudioVisualizerSettings;
        Place(); RefreshNativeLanguage();if(primaryWindow){RegisterHotkey(controller.Settings.Hotkey);AddTray();}
        pulse.Tick += async(_,_) => await Tick(); pulse.Start();
        CompositionTarget.Rendering += RenderFrame;
        Closed += (_,_) => {closing=true;compactStrip?.Dispose();StopAudioVisualizer();tooltip?.Close();CompositionTarget.Rendering-=RenderFrame;pulse.Stop();controller.Stop();if(primaryWindow){Native.UnregisterHotKey(hwnd,1);Native.Shell_NotifyIcon(2,ref tray);}if(trayIcon!=0){Native.DestroyIcon(trayIcon);trayIcon=0;}web.Close();desktop.WindowClosed(this,primaryWindow);};
        root.KeyDown += (_,e) => { if(e.Key==Windows.System.VirtualKey.Escape && dialogCount==0) { if(resizing)FinishResize(true);else if(dragging)FinishDrag(true);else Collapse();e.Handled=true; } };
    }
    public void ShowWithoutActivation()
    {
        // WinUI needs its first activation to attach/render the XAML tree.
        // WS_EX_NOACTIVATE remains set; restore any foreground change immediately.
        var foreground=Native.GetForegroundWindow();
        Activate();AppWindow.Show(false);
        if(foreground!=0&&Native.GetForegroundWindow()!=foreground)Native.SetForegroundWindow(foreground);
        RecordOpening("header.shown");
        if(Environment.GetCommandLineArgs().Contains("--diagnostics"))File.WriteAllText(Path.Combine(controller.Store.DirectoryPath,"startup.json"),System.Text.Json.JsonSerializer.Serialize(new{elapsedMs=(DateTime.UtcNow-Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalMilliseconds,foregroundPreserved=Native.GetForegroundWindow()==foreground}));
    }
    static SolidColorBrush Brush(string hex) => new(Color.FromArgb(255,Convert.ToByte(hex[..2],16),Convert.ToByte(hex[2..4],16),Convert.ToByte(hex[4..],16)));
    void UpdateClock()
    {
        var now=DateTime.Now;
        var culture=DesktopLanguage.Culture;
        day.Text=culture.TextInfo.ToTitleCase(now.ToString("dddd",culture));
        clock.Text=now.ToString("HH':'mm",culture);
        date.Text=DesktopLanguage.HeaderDate(now,culture);
        UpdateTimerHeader();
        UpdateCodexHeader();
        if(compactHidden)UpdateCompactStrip(now);
    }
    void RefreshNativeLanguage()
    {
        var language=DesktopLanguage.Language;
        if(nativeLanguage==language)return;
        nativeLanguage=language;
        root.Language=language;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(resizeGrip,DesktopLanguage.Text("Изменить размер панели"));
        UpdateClock();
        compactStrip?.RefreshLanguage();
        if(primaryWindow&&tray.Size>0)
        {
            tray.Tip=DesktopLanguage.Text("SideMenu — открыть или выйти");
            Native.Shell_NotifyIcon(1,ref tray);
        }
    }
    Native.MonitorInfo Monitor()=>dragDisplay?.Bounds??desktop.Monitor(DisplayId);
    Preferences Placement=>resizePreview??controller.Settings;
    double PanelPosition=>dragPosition??Placement.PositionForDisplay(DisplayId);
    internal object Snapshot()=>controller.Snapshot();
    internal void DeliverEvent(string type,object data)
    {
        if(type=="calendar.reminder"){
            if(primaryWindow){
                var message=System.Text.Json.JsonSerializer.SerializeToElement(data,Protocol.Json).GetProperty("message").GetString()??"";
                var balloon=tray;balloon.Flags=0x10;balloon.Info=message[..Math.Min(message.Length,255)];
                balloon.InfoTitle="SideWidget · "+DesktopLanguage.Text("Календарь");balloon.InfoFlags=1;balloon.Timeout=10000;
                Native.Shell_NotifyIcon(1,ref balloon);
            }
            SendEvent("notice",data);return;
        }
        SendEvent(type,data);
    }
    internal void AssignDisplay(string id){if(id==DisplayId)return;DisplayId=id;compactPanel.Update(false);Collapse();Place();}
    double MaximumHeight()
    {
        var work=Monitor().Work;
        return Math.Min(Placement.Height+controller.Settings.ClockHeaderHeight-Preferences.DefaultClockHeaderHeight,
            (work.Bottom-work.Top)/scale);
    }
    void Place()
    {
        scale=Math.Max(1,Native.GetDpiForWindow(hwnd)/96d);
        var headerHeight=Math.Min(controller.Settings.ClockHeaderHeight,MaximumHeight());
        root.RowDefinitions[0].Height=new GridLength(headerHeight);
        motion.ResizeHeader(headerHeight,MaximumHeight());
        ApplyGeometry();
    }
    void ApplyGeometry()
    {
        var monitor=Monitor();
        var viewport=PanelGeometry.Viewport(monitor.Work.Left,monitor.Work.Right,(int)Math.Round(Placement.Width*scale),PanelPosition,motion.Expansion);
        var clockOnly=!updateInProgress&&compactPanel.ClockOnly&&motion.State==PanelState.Collapsed;
        if(clockOnly)
        {
            var clockWidthDip=100+Math.Max(0,controller.Settings.ClockFontSize-Preferences.DefaultClockFontSize)*3+(codexQuota is null?0:76);
            var clockWidth=Math.Min(viewport.ClipWidth,(int)Math.Round(clockWidthDip*scale));
            viewport=viewport with{ClipLeft=viewport.ClipLeft+(viewport.ClipWidth-clockWidth)/2,ClipWidth=clockWidth};
        }
        if(clockOnlyHeader!=clockOnly)
        {
            clockOnlyHeader=clockOnly;
            day.Visibility=date.Visibility=clockOnly?Visibility.Collapsed:Visibility.Visible;
            header.Padding=clockOnly?new Thickness(12,0,12,0):new Thickness(16,0,16,0);
            UpdateClock();
        }
        var h=(int)Math.Round(motion.Height*scale);
        hostX=viewport.Host.Left;x=hostX+viewport.ClipLeft;width=viewport.ClipWidth;
        y=updateTop is int top?Math.Clamp(top,monitor.Work.Top,Math.Max(monitor.Work.Top,monitor.Work.Bottom-h)):monitor.Work.Top;
        var hostHeight=(int)Math.Round(MaximumHeight()*scale);
        var hostBounds=(hostX,y,viewport.Host.Width,hostHeight);
        // Resizing HWND on every frame also resizes WebView2 and forces async reflow.
        // The backing surface stays full-sized, even while collapsed.
        if(appliedHost!=hostBounds)
        {
            Native.SetWindowPos(hwnd,new nint(-1),hostX,y,viewport.Host.Width,hostHeight,0x0010);
            appliedHost=hostBounds;
        }
        header.Width=width/scale;
        header.RenderTransform=new TranslateTransform { X=viewport.ClipLeft/scale };
        headerGripScale.ScaleX=1+motion.Expansion;
        UpdateAudioVisualizerGeometry();
        resizeGrip.Visibility=motion.State==PanelState.Expanded?Visibility.Visible:Visibility.Collapsed;
        // Move the upper arcs above the HWND: square top, unchanged 16-DIP bottom corners.
        var cornerDiameter=(int)(32*scale);
        Native.SetWindowRgn(hwnd,Native.CreateRoundRectRgn(viewport.ClipLeft,-cornerDiameter,viewport.ClipLeft+width+1,h+1,cornerDiameter,cornerDiameter),true);
        PositionTooltip();
        if(Environment.GetCommandLineArgs().Contains("--diagnostics")){Native.GetWindowRect(hwnd,out var r);File.AppendAllText(Path.Combine(controller.Store.DirectoryPath,"geometry.ndjson"),System.Text.Json.JsonSerializer.Serialize(new{ms=watch.ElapsedMilliseconds,mode="clip",state=motion.State.ToString(),x,y,width,height=h,hostX=r.Left,hostWidth=r.Right-r.Left,hostHeight=r.Bottom-r.Top,foreground=Native.GetForegroundWindow()==hwnd,dpi=scale*96})+"\n");}
    }

    void EnterInteractive()
    {
        var foreground=Native.GetForegroundWindow();
        if(foreground==hwnd)return;
        previousFocus=foreground!=0&&!IsSideWidgetWindow(foreground)?foreground:0;
        Native.SetWindowLong(hwnd,-20,Native.GetWindowLong(hwnd,-20)&~0x08000000);
        Native.SetForegroundWindow(hwnd);
    }
    void RestoreFocus()
    {
        var target=previousFocus;
        previousFocus=0;
        var ownsForeground=Native.GetForegroundWindow()==hwnd;
        Native.SetWindowLong(hwnd,-20,Native.GetWindowLong(hwnd,-20)|0x08000000);
        if(ownsForeground&&Native.GetForegroundWindow()==hwnd&&target!=0&&Native.IsWindowVisible(target)&&
            !Native.IsIconic(target)&&!IsSideWidgetWindow(target))Native.SetForegroundWindow(target);
    }
    Task Tick()
    {
        PositionTooltip();
        var beforeHeight=motion.Height;var beforeExpansion=motion.Expansion;var beforeState=motion.State;
        var now=watch.ElapsedMilliseconds; var delta=now-previous; previous=now;
        Native.GetCursorPos(out var point);
        var inside=point.X>=x && point.X<x+width && point.Y>=y && point.Y<y+motion.Height*scale;
        if(pendingSearchFocus&&Stopwatch.GetElapsedTime(searchOpenedAt)>TimeSpan.FromSeconds(5))pendingSearchFocus=false;
        if(inside||pendingSearchFocus) outsideMs=0;
        else {outsideMs+=delta;if(outsideMs>controller.Settings.HideDelay)motion.Target(false,immediate:ReducedMotion());}
        // An idle timer interval precedes the target change; it is not animation time.
        var justStarted=beforeState is PanelState.Collapsed or PanelState.Expanded && motion.State is PanelState.Opening or PanelState.Closing;
        motion.Tick(justStarted?0:delta);
        if(beforeHeight!=motion.Height||beforeExpansion!=motion.Expansion) { ApplyGeometry(); if(motion.State==PanelState.Collapsed) RestoreFocus(); }
        if(beforeState!=motion.State){SendEvent("panel",new{expanded=motion.State!=PanelState.Collapsed,pinned=motion.Pinned});if(motion.State==PanelState.Collapsed){pendingSearchFocus=false;compactPanel.Fold();UpdateCompactSurface();}}

        if(now/1000!=(now-delta)/1000){UpdateClock();if(primaryWindow)desktop.CheckActiveDisplayFullscreen();if(Environment.GetCommandLineArgs().Contains("--diagnostics")&&now<10000)File.AppendAllText(Path.Combine(controller.Store.DirectoryPath,"layout.ndjson"),System.Text.Json.JsonSerializer.Serialize(new{ms=now,rootWidth=root.ActualWidth,rootHeight=root.ActualHeight,xamlVisible=root.XamlRoot?.IsHostVisible,clockWidth=clock.ActualWidth,clockHeight=clock.ActualHeight,webHeight=web.ActualHeight,visible=Visible,compact=compactHidden?compactStrip?.LayoutSnapshot():null})+"\n");}
        return Task.CompletedTask;
    }
    void RenderFrame(object? sender,object args)
    {
        if(!closing && motion.State is PanelState.Opening or PanelState.Closing) _=Tick();
    }
    static bool ReducedMotion() { var enabled=1; Native.SystemParametersInfo(0x1042,0,ref enabled,0); return enabled==0; }
}








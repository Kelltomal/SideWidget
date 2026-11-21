using SideWidget.Core;

namespace SideWidget.App;

public sealed partial class MainWindow
{
    readonly CompactPanel compactPanel=new();
    CompactStripWindow? compactStrip;
    bool compactHidden;
    bool clockOnlyHeader;

    void OpenCompactStrip()
    {
        DispatcherQueue.TryEnqueue(()=>{if(!closing)OpenInteractive(true);});
    }
    void UpdateCompactClockMode()
    {
        DispatcherQueue.TryEnqueue(()=>{if(!closing)UpdateCompactSurface();});
    }
    void DismissCompactSignal()
    {
        DispatcherQueue.TryEnqueue(()=>{if(!closing)DismissTimerSignal();});
    }
    void UpdateCompactStrip(DateTime now)
    {
        if(compactStrip is null)return;
        var monitor=Monitor();
        var settings=Placement;
        var bounds=PanelGeometry.Calculate(monitor.Work.Left,monitor.Work.Right,
            (int)Math.Round(settings.Width*scale),PanelPosition,0);
        var timer=FocusTimerPresentation.Create(controller.Timer.State,controller.Timer.Remaining(DateTimeOffset.Now));
        if(timer==null&&controller.PowerSchedule.State is {} power)
            timer=new(DesktopLanguage.Text(power.Action=="restart"?"До перезагрузки ПК":"До выключения ПК"),
                FocusTimer.Format(controller.PowerSchedule.Remaining(DateTimeOffset.Now)),"","running",0);
        var clockOnly=compactPanel.ClockOnly;
        if(clockOnly&&timer?.NeedsFullWidth!=true)
        {
            // A countdown can be longer than the HH:mm clock when it includes hours.
            var clockWidth=Math.Min(bounds.Width,(int)Math.Round((timer?.Value.Length>5?88:72)*
                Math.Max(1,settings.CompactClockFontSize/13d)*scale));
            bounds=new(bounds.Left+(bounds.Width-clockWidth)/2,clockWidth);
        }
        var culture=DesktopLanguage.Culture;
        var time=now.ToString("HH':'mm",culture);
        var label=timer?.CompactLabel(clockOnly)??(clockOnly?time:time+"   "+DesktopLanguage.HeaderDate(now,culture));
        compactStrip.Show(bounds,monitor.Work.Top,scale,settings,label,timer,currentLightTheme==true);
    }
    void UpdateCompactSurface()
    {
        var strip=!updateInProgress&&compactPanel.StripVisible&&dialogCount==0&&!dragging&&!resizing;
        if(strip)
        {
            compactStrip??=new CompactStripWindow(compactPanel,OpenCompactStrip,UpdateCompactClockMode,DismissCompactSignal);
            motion.Suspended=true;motion.Target(false,immediate:true);
            compactStrip.ConfigureSpectrum(()=>controller.Spectrum.Latest,controller.Settings.Visualizer??new VisualizerOptions());
            UpdateCompactStrip(DateTime.Now);
            if(!compactHidden)
            {
                tooltip?.Hide();Native.ShowWindow(hwnd,0);compactHidden=true;RefreshAudioVisualizerSettings();
                SendEvent("panel",new{expanded=false,pinned=motion.Pinned});
            }
        }
        else
        {
            compactStrip?.Hide();motion.Suspended=false;
            if(compactHidden){compactHidden=false;Place();Native.ShowWindow(hwnd,4);}
        }
    }
}

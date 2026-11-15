using System.Globalization;
using SideWidget.Core;

namespace SideWidget.App;

public sealed partial class MainWindow
{
    readonly CompactPanel compactPanel=new();
    CompactStripWindow? compactStrip;
    bool compactHidden;
    bool clockOnlyHeader;

    string CompactClockLabel()=>DateTime.Now.ToString(compactPanel.ClockOnly?"HH:mm":"HH:mm   d MMM",
        CultureInfo.GetCultureInfo("ru-RU")).TrimEnd('.');

    void OpenCompactStrip()
    {
        DispatcherQueue.TryEnqueue(()=>{if(!closing)OpenInteractive(true);});
    }
    void UpdateCompactClockMode()
    {
        DispatcherQueue.TryEnqueue(()=>{if(!closing)UpdateCompactSurface();});
    }
    void UpdateCompactSurface()
    {
        var strip=compactPanel.StripVisible&&dialogCount==0&&!dragging&&!resizing;
        if(strip)
        {
            compactStrip??=new CompactStripWindow(compactPanel,OpenCompactStrip,UpdateCompactClockMode);
            motion.Suspended=true;motion.Target(false,immediate:true);
            var monitor=Monitor();
            var bounds=PanelGeometry.Calculate(monitor.Work.Left,monitor.Work.Right,
                (int)Math.Round(Placement.Width*scale),PanelPosition,0);
            var clockOnly=compactPanel.ClockOnly;
            if(clockOnly)
            {
                var clockWidth=Math.Min(bounds.Width,(int)Math.Round(72*scale));
                bounds=new(bounds.Left+(bounds.Width-clockWidth)/2,clockWidth);
            }
            compactStrip.ConfigureSpectrum(()=>controller.Spectrum.Latest,controller.Settings.Visualizer??new VisualizerOptions());
            compactStrip.Show(bounds.Left,monitor.Work.Top,bounds.Width,scale,CompactClockLabel(),currentLightTheme==true);
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

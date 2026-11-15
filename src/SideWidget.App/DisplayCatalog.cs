using System.Runtime.InteropServices;
using SideWidget.Core;

namespace SideWidget.App;

sealed record DesktopDisplay(string Id,int Index,nint Handle,Native.MonitorInfo Bounds)
{
    public DisplayDescriptor Descriptor => new(Id,DesktopLanguage.Text("Экран {0} — {1} × {2}",
        Index,Bounds.Monitor.Right-Bounds.Monitor.Left,Bounds.Monitor.Bottom-Bounds.Monitor.Top),(Bounds.Flags&1)!=0);
}

static class DisplayCatalog
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct MonitorInfoEx
    {
        public int Size;
        public Native.Rect Monitor,Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device;
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Name;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Description;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Id;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Key;
    }
    [DllImport("user32.dll",EntryPoint="GetMonitorInfoW",CharSet=CharSet.Unicode)]static extern bool GetMonitorInfo(nint monitor,ref MonitorInfoEx info);
    [DllImport("user32.dll",EntryPoint="EnumDisplayDevicesW",CharSet=CharSet.Unicode)]static extern bool EnumDisplayDevices(string device,uint index,ref DisplayDevice display,uint flags);

    public static IReadOnlyList<DesktopDisplay> Read()
    {
        var displays=new List<DesktopDisplay>();
        Native.EnumDisplayMonitors(0,0,(nint handle,nint dc,ref Native.Rect rect,nint data)=>{
            var info=new MonitorInfoEx{Size=Marshal.SizeOf<MonitorInfoEx>(),Device=""};
            if(!GetMonitorInfo(handle,ref info))return true;
            var device=new DisplayDevice{Size=Marshal.SizeOf<DisplayDevice>(),Name="",Description="",Id="",Key=""};
            var found=EnumDisplayDevices(info.Device,0,ref device,1);
            var id=found&&!string.IsNullOrWhiteSpace(device.Id)?device.Id:info.Device;
            // Mirrored/virtual drivers can reuse their device ID. Keep each desktop target distinct.
            if(displays.Any(display=>display.Id==id))id=info.Device;
            var index=displays.Count+1;
            displays.Add(new(id,index,handle,new(){Size=Marshal.SizeOf<Native.MonitorInfo>(),Monitor=info.Monitor,Work=info.Work,Flags=info.Flags}));
            return true;
        },0);
        return displays;
    }
}

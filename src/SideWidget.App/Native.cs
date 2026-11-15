using System.Runtime.InteropServices;

namespace SideWidget.App;
internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct StyleChange { public uint OldStyle, NewStyle; }
    [StructLayout(LayoutKind.Sequential)] public struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll",EntryPoint="MessageBoxW",CharSet=CharSet.Unicode)] public static extern int MessageBox(nint hwnd,string text,string caption,uint type);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] public static extern int GetWindowLong(nint hwnd, int index);
    [DllImport("user32.dll")] public static extern int SetWindowLong(nint hwnd, int index, int value);
    [DllImport("user32.dll")] public static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(nint hwnd);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(nint hwnd,int attribute,out uint value,int size);
    [DllImport("user32.dll")] public static extern bool IsZoomed(nint hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(nint hwnd);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(nint hwnd,int attribute,out Rect value,int size);
    [DllImport("user32.dll")] public static extern int SetWindowRgn(nint hwnd, nint region, bool redraw);
    [DllImport("gdi32.dll")] public static extern nint CreateRoundRectRgn(int left,int top,int right,int bottom,int width,int height);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(nint hwnd,int attribute,ref int value,int size);
    [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint action,uint param,ref int value,uint flags);
    public delegate nint SubclassProc(nint hwnd,uint message,nuint wParam,nint lParam,nuint id,nuint data);
    [DllImport("comctl32.dll")] public static extern bool SetWindowSubclass(nint hwnd,SubclassProc callback,nuint id,nuint data);
    [DllImport("comctl32.dll")] public static extern nint DefSubclassProc(nint hwnd,uint message,nuint wParam,nint lParam);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(nint hwnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(nint hwnd,int id);
    [DllImport("user32.dll")] public static extern nint LoadIcon(nint instance,nint name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern nint LoadImage(nint instance,string name,uint type,int width,int height,uint flags);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(nint icon);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] public static extern bool Shell_NotifyIcon(uint message,ref NotifyIcon data);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct NotifyIcon {
        public uint Size; public nint Hwnd; public uint Id,Flags,Callback; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Tip;
        public uint State,StateMask; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)] public string Info;
        public uint Timeout; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public nint Balloon;
    }
    [DllImport("user32.dll")] public static extern nint CreatePopupMenu();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern bool AppendMenu(nint menu,uint flags,nuint id,string text);
    [DllImport("user32.dll")] public static extern uint TrackPopupMenu(nint menu,uint flags,int x,int y,int reserved,nint hwnd,nint rect);
    [DllImport("user32.dll")] public static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern uint RegisterWindowMessage(string name);
    public delegate bool MonitorProc(nint monitor,nint hdc,ref Rect rect,nint data);
    [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(nint hdc,nint clip,MonitorProc callback,nint data);
    [DllImport("user32.dll")] public static extern nint GetShellWindow();
}

using System.ComponentModel;
using System.Runtime.InteropServices;
using SideWidget.Core;

namespace SideWidget.App;

// Real desktop alpha without changing WinUI/WebView2 composition.
internal sealed partial class CompactStripWindow : IDisposable
{
    public nint Handle { get; }
    readonly CompactPanel panel;
    readonly Action open;
    readonly Action updateClockMode;
    readonly Native.SubclassProc procedure;
    readonly MouseProc mouseProcedure;
    nint mouseHook;
    int left,top,width,height;
    string text="";
    double scale=1;
    bool light,visible;
    readonly string fontPath=Path.Combine(AppContext.BaseDirectory,"Assets","Fonts","MedeoSans.ttf");
    readonly bool fontLoaded;

    public CompactStripWindow(CompactPanel panel,Action open,Action updateClockMode)
    {
        this.panel=panel;this.open=open;this.updateClockMode=updateClockMode;procedure=Procedure;mouseProcedure=ObserveMouse;
        Handle=CreateWindowExW(0x080800A8,"STATIC",DesktopLanguage.Text("SideWidget — время и дата"),0x80000100,
            0,0,1,1,0,0,0,0); // NOACTIVATE | LAYERED | TRANSPARENT | TOOLWINDOW | TOPMOST
        if(Handle==0)throw new Win32Exception(Marshal.GetLastWin32Error());
        if(!Native.SetWindowSubclass(Handle,procedure,1,0)||!SetLayeredWindowAttributes(Handle,0,CompactPanel.Alpha,2))
        {
            var error=Marshal.GetLastWin32Error();DestroyWindow(Handle);throw new Win32Exception(error);
        }
        fontLoaded=AddFontResourceExW(fontPath,0x10,0)>0;
        var corner=1;Native.DwmSetWindowAttribute(Handle,33,ref corner,4);
    }
    public void Show(int left,int top,int width,double dpiScale,string label,bool lightTheme)
    {
        var changed=text!=label||scale!=dpiScale||light!=lightTheme;
        var nextHeight=(int)Math.Round(CompactPanel.Height*dpiScale);
        var moved=this.left!=left||this.top!=top||this.width!=width||height!=nextHeight;
        text=label;scale=dpiScale;light=lightTheme;
        this.left=left;this.top=top;this.width=width;height=nextHeight;
        if(mouseHook==0)
        {
            mouseHook=SetWindowsHookExW(14,mouseProcedure,GetModuleHandleW(null),0);
            if(mouseHook==0)throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        if(moved||!visible)Native.SetWindowPos(Handle,new nint(-1),left,top,width,height,0x0010|0x0040);
        if(changed||!visible)InvalidateRect(Handle,0,false);
        visible=true;
        RefreshSpectrumAnimation();
    }
    public void Hide()
    {
        if(!visible)return;
        Native.ShowWindow(Handle,0);visible=false;StopSpectrumAnimation();
        panel.ResetClicks();
        if(swallowRelease==0 && pendingDown is null)StopObserving();
    }
    public void UpdateLabel(string label)
    {
        if(!visible||text==label)return;
        text=label;
        InvalidateRect(Handle,0,false);
    }
    public void RefreshLanguage()=>SetWindowTextW(Handle,DesktopLanguage.Text("SideWidget — время и дата"));
    nint Procedure(nint window,uint message,nuint wParam,nint lParam,nuint id,nuint data)
    {
        if(message==0x0021)return 3; // MA_NOACTIVATE
        if(message==0x0084)return 1; // HTCLIENT
        if(message==0x0014)return 1;
        if(message==0x000f){Paint(window);return 0;}
        if(message==0x0113&&wParam==SpectrumTimerId){TickSpectrum();return 0;}
        if(message==0x0113&&wParam==ClickTimerId){ExpirePendingClick();return 0;}
        return Native.DefSubclassProc(window,message,wParam,lParam);
    }
    void Paint(nint window)
    {
        var dc=BeginPaint(window,out var paint);
        if(dc==0)return;
        nint brush=0,font=0,oldFont=0;
        try
        {
            GetClientRect(window,out var bounds);
            brush=CreateSolidBrush(light?0x00F6F5F4u:0x001C1A18u);FillRect(dc,ref bounds,brush);
            PaintSpectrum(dc,bounds);
            SetBkMode(dc,1);SetTextColor(dc,light?0x002C2620u:0x00F5F4F2u);
            var face=DesktopLanguage.Language switch{"zh-CN"=>"Microsoft YaHei UI","ja"=>"Yu Gothic UI",_=>"Medeo Sans"};
            font=CreateFontW(-(int)Math.Round(13*scale),0,0,0,500,0,0,0,1,0,0,4,0,face);
            if(font!=0)oldFont=SelectObject(dc,font);
            bounds.Left+=(int)Math.Round(12*scale);bounds.Right-=(int)Math.Round(12*scale);
            DrawTextW(dc,text,text.Length,ref bounds,0x0001|0x0004|0x0020|0x0800);
        }
        finally
        {
            if(oldFont!=0)SelectObject(dc,oldFont);
            if(font!=0)DeleteObject(font);
            if(brush!=0)DeleteObject(brush);
            EndPaint(window,ref paint);
        }
    }
    public void Dispose()
    {
        spectrumDisposed=true;StopSpectrumAnimation();
        ReplayPendingClick();
        StopObserving();
        DestroyWindow(Handle);
        if(fontLoaded)RemoveFontResourceExW(fontPath,0x10,0);
        GC.KeepAlive(procedure);
        GC.KeepAlive(mouseProcedure);
    }
    [StructLayout(LayoutKind.Sequential)] struct PaintInfo
    {
        public nint Dc;public int Erase;public Native.Rect Bounds;public int Restore,Update;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=32)] public byte[] Reserved;
    }
    [StructLayout(LayoutKind.Sequential)] struct MouseInfo
    {
        public Native.Point Position;public uint Data,Flags,Time;public nuint Extra;
    }
    delegate nint MouseProc(int code,nint message,nint data);
    [DllImport("user32.dll",SetLastError=true)] static extern nint SetWindowsHookExW(int id,MouseProc procedure,nint module,uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] static extern nint CallNextHookEx(nint hook,int code,nint message,nint data);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern nint GetModuleHandleW(string? name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern nint CreateWindowExW(uint extendedStyle,string className,string name,uint style,int x,int y,int width,int height,nint parent,nint menu,nint instance,nint parameter);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern bool SetWindowTextW(nint window,string name);
    [DllImport("user32.dll",SetLastError=true)] static extern bool SetLayeredWindowAttributes(nint window,uint key,byte alpha,uint flags);
    [DllImport("user32.dll")] static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] static extern bool InvalidateRect(nint window,nint bounds,bool erase);
    [DllImport("user32.dll")] static extern nint BeginPaint(nint window,out PaintInfo paint);
    [DllImport("user32.dll")] static extern bool EndPaint(nint window,ref PaintInfo paint);
    [DllImport("user32.dll")] static extern bool GetClientRect(nint window,out Native.Rect bounds);
    [DllImport("user32.dll")] static extern int FillRect(nint dc,ref Native.Rect bounds,nint brush);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int DrawTextW(nint dc,string text,int length,ref Native.Rect bounds,uint format);
    [DllImport("gdi32.dll")] static extern nint CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")] static extern nint SelectObject(nint dc,nint value);
    [DllImport("gdi32.dll")] static extern int SetBkMode(nint dc,int mode);
    [DllImport("gdi32.dll")] static extern uint SetTextColor(nint dc,uint color);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] static extern nint CreateFontW(int height,int width,int escapement,int orientation,int weight,uint italic,uint underline,uint strikeout,uint charset,uint precision,uint clip,uint quality,uint family,string face);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] static extern int AddFontResourceExW(string path,uint flags,nint reserved);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] static extern bool RemoveFontResourceExW(string path,uint flags,nint reserved);
}

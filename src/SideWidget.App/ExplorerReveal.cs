using System.Runtime.InteropServices;

namespace SideWidget.App;

static class ExplorerReveal
{
    // Shell takes an item ID, so spaces, commas and Unicode never pass through
    // Explorer's command-line parser. Work off the WinUI dispatcher thread.
    public static Task<bool> TryRevealAsync(string path)=>Task.Run(()=>TryReveal(path,SelectNative));

    internal static bool TryReveal(string path,Func<string,int> select)
    {
        if(!File.Exists(path))return false;
        try{return select(Path.GetFullPath(path))==0;}
        catch(Exception error)when(error is COMException or IOException or UnauthorizedAccessException or ArgumentException or DllNotFoundException or EntryPointNotFoundException){return false;}
    }
    static int SelectNative(string path)
    {
        var initialized=CoInitializeEx(0,0); // MTA; Task.Run may already have initialized COM.
        if(initialized<0&&initialized!=unchecked((int)0x80010106))return initialized;
        nint item=0;
        try
        {
            var parsed=SHParseDisplayName(path,0,out item,0,out _);
            if(parsed!=0||item==0)return parsed!=0?parsed:unchecked((int)0x80004005);
            // cidl=0 means a fully qualified SINGLE item: open its parent and select it.
            return SHOpenFolderAndSelectItems(item,0,0,0);
        }
        finally{if(item!=0)Marshal.FreeCoTaskMem(item);if(initialized>=0)CoUninitialize();}
    }
    [DllImport("ole32.dll",ExactSpelling=true)]static extern int CoInitializeEx(nint reserved,uint flags);
    [DllImport("ole32.dll",ExactSpelling=true)]static extern void CoUninitialize();
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]static extern int SHParseDisplayName(string name,nint binding,out nint item,uint requested,out uint attributes);
    [DllImport("shell32.dll",ExactSpelling=true)]static extern int SHOpenFolderAndSelectItems(nint item,uint count,nint children,uint flags);
}

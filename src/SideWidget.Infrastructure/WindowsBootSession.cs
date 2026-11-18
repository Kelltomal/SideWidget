using System.Runtime.InteropServices;

namespace SideWidget.Infrastructure;

public static class WindowsBootSession
{
    // The kernel boot GUID is unchanged by application restart, sleep and clock adjustments.
    public static string? Read()
    {
        if(!OperatingSystem.IsWindows())return null;
        try{
            var buffer=new byte[64];
            if(NtQuerySystemInformation(90,buffer,buffer.Length,out var length)!=0||length<16)return null;
            var id=new Guid(buffer.AsSpan(0,16));return id==Guid.Empty?null:id.ToString("N");
        }catch(Exception error) when(error is DllNotFoundException or EntryPointNotFoundException){return null;}
    }
    [DllImport("ntdll.dll")] static extern int NtQuerySystemInformation(int informationClass,[Out]byte[] information,int size,out int returned);
}

using Microsoft.Win32;
using SideWidget.Core;

namespace SideWidget.App;

sealed record StartupState(bool Enabled,string? Message=null);

sealed class StartupService(string executable,
    string runPath=@"Software\Microsoft\Windows\CurrentVersion\Run",
    string approvedPath=@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
    string? previousExecutable=null)
{
    const string Name="SideWidget";
    string Command=>$"\"{Path.GetFullPath(executable)}\"";
    bool Matches(string entry)=>string.Equals(entry,Command,StringComparison.OrdinalIgnoreCase)||
        previousExecutable is not null&&string.Equals(entry,$"\"{Path.GetFullPath(previousExecutable)}\"",StringComparison.OrdinalIgnoreCase);

    public StartupState Get()=>Access(()=>{
        using var run=Registry.CurrentUser.OpenSubKey(runPath);
        using var approved=Registry.CurrentUser.OpenSubKey(approvedPath);
        var registered=run?.GetValue(Name) is string value&&Matches(value);
        var blocked=approved?.GetValue(Name) is byte[] bytes&&bytes.Length>0&&(bytes[0]&1)==1;
        return new StartupState(registered&&!blocked,registered&&blocked?"Автозапуск отключён в Windows. Включите SideWidget: Параметры Windows → Приложения → Автозагрузка.":null);
    });

    public StartupState Set(bool enabled)=>Access(()=>{
        using var run=Registry.CurrentUser.CreateSubKey(runPath,true);
        if(enabled)run.SetValue(Name,Command,RegistryValueKind.String);
        else run.DeleteValue(Name,false);
        return Get();
    });

    public void TransferTo(string updatedExecutable)=>Access(()=>{
        using var run=Registry.CurrentUser.OpenSubKey(runPath,writable:true);
        if(run?.GetValue(Name) is string entry && Matches(entry))
            run.SetValue(Name,$"\"{Path.GetFullPath(updatedExecutable)}\"",RegistryValueKind.String);
        return true;
    });

    static T Access<T>(Func<T> action){
        try{return action();}
        catch(Exception error) when(error is UnauthorizedAccessException or System.Security.SecurityException or IOException){
            throw new CommandFault("startup","Не удалось изменить или прочитать автозапуск. Проверьте доступ к настройкам Windows и повторите попытку.");
        }
    }
}

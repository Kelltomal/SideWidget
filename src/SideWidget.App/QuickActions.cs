using System.Diagnostics;
using System.Runtime.InteropServices;
using SideWidget.Core;
namespace SideWidget.App;
static class QuickActions
{
    public static void Open(string target)
    {
        var uri=target switch{"sound"=>"ms-settings:sound","wifi"=>"ms-settings:network-wifi","bluetooth"=>"ms-settings:bluetooth","notifications"=>"ms-settings:notifications","power"=>"ms-settings:powersleep",_=>null};
        if(uri!=null){Process.Start(new ProcessStartInfo(uri){UseShellExecute=true});return;}
        var windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var file=target switch{"taskmanager"=>Path.Combine(windows,"System32","Taskmgr.exe"),"cmd"=>Path.Combine(windows,"System32","cmd.exe"),"powershell"=>Path.Combine(windows,"System32","WindowsPowerShell","v1.0","powershell.exe"),_=>throw new CommandFault("action","Действие не поддерживается")};
        Process.Start(new ProcessStartInfo(file){UseShellExecute=true});
    }
    public static string Description(string action)=>action switch{"sleep"=>"Перевести компьютер в сон? Сохраните текущую работу.","shutdown"=>"Выключить компьютер? Сохраните документы. Приложения не будут принудительно завершены.","restart"=>"Перезагрузить компьютер? Сохраните документы. Приложения не будут принудительно завершены.","lock"=>"Заблокировать сеанс Windows?","explorer"=>throw new CommandFault("unavailable","Безопасный перезапуск Проводника пока не реализован. Можно открыть Диспетчер задач."),_=>throw new CommandFault("action","Действие не поддерживается")};
    public static void Power(string action){_=Description(action);if(action=="lock"){if(!LockWorkStation())throw new CommandFault("windows","Windows не выполнила блокировку");return;}if(action=="sleep"){if(!SetSuspendState(false,false,false))throw new CommandFault("windows","Windows не разрешила переход в сон");return;}var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","shutdown.exe")){UseShellExecute=false,CreateNoWindow=true};start.ArgumentList.Add(action=="restart"?"/r":"/s");start.ArgumentList.Add("/t");start.ArgumentList.Add("0");Process.Start(start);}
    [DllImport("user32.dll")]static extern bool LockWorkStation();
    [DllImport("powrprof.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.U1)]static extern bool SetSuspendState([MarshalAs(UnmanagedType.U1)]bool hibernate,[MarshalAs(UnmanagedType.U1)]bool force,[MarshalAs(UnmanagedType.U1)]bool disableWake);
}

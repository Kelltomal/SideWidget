using Microsoft.Win32;
using SideWidget.App;

static class StartupChecks
{
    public static Task Run()
    {
        var root=@"Software\SideWidget.Tests\"+Guid.NewGuid().ToString("N");
        try {
            var service=new StartupService(@"C:\Test Folder\Виджет\SideWidget.App.exe",root+@"\Run",root+@"\Approved");
            Require(!service.Get().Enabled);
            Require(service.Set(true).Enabled);
            using(var key=Registry.CurrentUser.OpenSubKey(root+@"\Run",true)){
                Require((string?)key!.GetValue("SideWidget")=="\"C:\\Test Folder\\Виджет\\SideWidget.App.exe\"");
                key.SetValue("OtherApp","untouched");
            }
            Require(new StartupService(@"C:\Test Folder\Виджет\SideWidget.App.exe",root+@"\Run",root+@"\Approved").Get().Enabled);
            Require(service.Set(true).Enabled);
            using(var key=Registry.CurrentUser.CreateSubKey(root+@"\Approved"))key.SetValue("SideWidget",new byte[]{3,0,0,0},RegistryValueKind.Binary);
            Require(!service.Get().Enabled&&service.Get().Message!=null);
            Require(!service.Set(true).Enabled&&service.Get().Message!=null);
            using(var key=Registry.CurrentUser.OpenSubKey(root+@"\Approved",true))key!.SetValue("SideWidget",new byte[]{7,0,0,0},RegistryValueKind.Binary);
            Require(!service.Get().Enabled);
            Require(!service.Set(false).Enabled);
            Require(!service.Set(false).Enabled);
            using(var key=Registry.CurrentUser.OpenSubKey(root+@"\Run"))Require((string?)key!.GetValue("OtherApp")=="untouched"&&key.GetValue("SideWidget")==null);
        }
        finally{Registry.CurrentUser.DeleteSubKeyTree(root,false);}
        return Task.CompletedTask;
    }
    static void Require(bool value){if(!value)throw new Exception("Startup registration check failed");}
}

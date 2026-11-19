using System.Security.AccessControl;
using System.Security.Principal;
using SideWidget.Infrastructure;
namespace SideWidget.App;
static class BundledBrowser
{
    static bool permissionsReady;
    public static string? Folder()
    {
        var folder = LiteInstallation.IsLite
            ? LiteInstallation.Dependencies is { } dependencies ? Path.Combine(dependencies, "webview2") : null
            : Path.Combine(AppContext.BaseDirectory, "WebView2Runtime");
        if (folder is null || (LiteInstallation.IsLite && !File.Exists(Path.Combine(folder, ".complete"))))
            return null;
        if(!File.Exists(Path.Combine(folder,"msedgewebview2.exe")))
        {
            if(LiteInstallation.IsLite || File.Exists(Path.Combine(AppContext.BaseDirectory,DistributionArchive.ManifestName)))
                throw new FileNotFoundException("Папка WebView2Runtime отсутствует. Повторно распакуйте SideWidget и запустите SideWidget.exe.");
            return null; // Unpublished development output may use Evergreen.
        }
        // Fixed runtime v120+ requires AppContainer read/execute permissions on Windows 10.
        if(!permissionsReady&&OperatingSystem.IsWindowsVersionAtLeast(10)&&!OperatingSystem.IsWindowsVersionAtLeast(10,0,22000))
        {
            var directory=new DirectoryInfo(folder);var security=directory.GetAccessControl();
            foreach(var sid in new[]{"S-1-15-2-1","S-1-15-2-2"})
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid),FileSystemRights.ReadAndExecute,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
            directory.SetAccessControl(security);permissionsReady=true;
        }
        return folder;
    }
}

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Web.WebView2.Core;
using SideWidget.Infrastructure;

namespace SideWidget.App;

static class PortableVerification
{
    // Runs before the desktop mutex/UI. Lets release checks execute the actual
    // shipped apphost with DOTNET_ROOT pointed at an empty directory.
    public static bool TryRun(string[] args)
    {
        if(args.Length!=2||args[0] is not ("--verify-portable" or "--share-portable"))return false;
        try
        {
            if(args[0]=="--share-portable")
            {
                DistributionArchive.Create(AppContext.BaseDirectory,args[1]);return true;
            }
            DistributionArchive.Validate(AppContext.BaseDirectory);
            var root=Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)+Path.DirectorySeparatorChar;
            var runtime=RuntimeEnvironment.GetRuntimeDirectory();
            if(!Path.GetFullPath(runtime).StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(".NET loaded outside the distribution: "+runtime);
            // Load native subsystems from their app-local paths, not System32 copies.
            var libraries=new[]{"vcruntime140.dll","vcruntime140_1.dll","msvcp140.dll","concrt140.dll","Microsoft.WindowsAppRuntime.dll","Microsoft.ui.xaml.dll","WebView2Loader.dll","Everything64.dll"};
            foreach(var name in libraries)NativeLibrary.Load(Path.Combine(root,name));
            using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();
            var browser=BundledBrowser.Folder()??throw new InvalidOperationException("Bundled WebView2 missing");
            var browserVersion=CoreWebView2Environment.GetAvailableBrowserVersionString(browser);
            // Include the optional local remote-server stack if this build contains it.
            if(File.Exists(Path.Combine(root,"Microsoft.AspNetCore.Server.Kestrel.Core.dll")))Assembly.Load("Microsoft.AspNetCore.Server.Kestrel.Core");
            var modules=Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                .Where(module=>module.FileName.StartsWith(root,StringComparison.OrdinalIgnoreCase)).Select(module=>module.FileName).ToArray();
            File.WriteAllText(args[1],JsonSerializer.Serialize(new{passed=true,application=root,runtime,browser,browserVersion,sqlite=connection.ServerVersion,modules},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception error)
        {
            Environment.ExitCode=1;
            if(args[0]=="--verify-portable")File.WriteAllText(args[1],JsonSerializer.Serialize(new{passed=false,error=error.ToString()}));
        }
        return true;
    }
}

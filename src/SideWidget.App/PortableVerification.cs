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
    // Runs the shipped apphost before the desktop mutex/UI.
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
            var lite = DistributionArchive.IsLite(AppContext.BaseDirectory);
            var root=Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)+Path.DirectorySeparatorChar;
            var runtime=RuntimeEnvironment.GetRuntimeDirectory();
            bool IsLocal(string path) => Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
            if (IsLocal(runtime) == lite)
                throw new InvalidOperationException("Unexpected .NET runtime location: " + runtime);
            var libraries = lite
                ? new[] { "Microsoft.WindowsAppRuntime.Bootstrap.dll", "WebView2Loader.dll", "Everything64.dll" }
                : new[] { "vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "concrt140.dll", "Microsoft.WindowsAppRuntime.dll", "Microsoft.ui.xaml.dll", "WebView2Loader.dll", "Everything64.dll" };
            foreach(var name in libraries)NativeLibrary.Load(Path.Combine(root,name));
            if (lite)
            {
                NativeLibrary.Load("Microsoft.WindowsAppRuntime.dll");
                NativeLibrary.Load("Microsoft.ui.xaml.dll");
                NativeLibrary.Load("vcruntime140.dll");
            }
            using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();
            var browser=BundledBrowser.Folder();
            if ((browser is null) != lite) throw new InvalidOperationException("Unexpected WebView2 configuration");
            var browserVersion=CoreWebView2Environment.GetAvailableBrowserVersionString(browser);
            var aspNet = Assembly.Load("Microsoft.AspNetCore.Server.Kestrel.Core").Location;
            if (IsLocal(aspNet) == lite)
                throw new InvalidOperationException("Unexpected ASP.NET Core runtime location: " + aspNet);
            var modules=Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                .Select(module=>module.FileName).ToArray();
            var winUi = modules.FirstOrDefault(path => Path.GetFileName(path).Equals("Microsoft.ui.xaml.dll", StringComparison.OrdinalIgnoreCase));
            if (winUi is null || IsLocal(winUi) == lite)
                throw new InvalidOperationException("Unexpected WinUI runtime location: " + winUi);
            File.WriteAllText(args[1],JsonSerializer.Serialize(new{passed=true,lite,application=root,runtime,aspNet,winUi,browser,browserVersion,sqlite=connection.ServerVersion,modules},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception error)
        {
            Environment.ExitCode=1;
            if(args[0]=="--verify-portable")File.WriteAllText(args[1],JsonSerializer.Serialize(new{passed=false,error=error.ToString()}));
        }
        return true;
    }
}

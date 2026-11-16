using System;
using System.IO;

internal static class LiteDependencyPackChecks
{
    public static void Main(string[] args)
    {
        var installRoot = args[0];
        var catalog = args[1];
        var release = File.ReadAllLines(catalog)[0];
        var dependencies = Path.Combine(installRoot, "Dependencies", release);
        var installer = new SideWidgetPrerequisites(installRoot, File.OpenRead(catalog));
        var dotnet = Path.Combine(dependencies, "dotnet");
        try
        {
            using (var truncated = new MemoryStream(new byte[] { 1, 2, 3 }))
                installer.InstallPack("dotnet", truncated, dotnet, _ => { }, (_, __) => { });
            throw new Exception("Truncated dependency accepted.");
        }
        catch (InvalidDataException) { }

        if (Directory.GetFileSystemEntries(dependencies).Length != 0)
            throw new Exception("Failed dependency installation left staged files.");

        foreach (var name in new[] { "dotnet", "desktop", "webview2" })
        {
            var file = "SideWidget-" + release + "-deps-" + name + "-win-x64.zip";
            using (var payload = File.OpenRead(Path.Combine(Path.GetDirectoryName(catalog), file)))
                installer.InstallPack(name, payload, Path.Combine(dependencies, name), _ => { }, (_, __) => { });
        }
        Console.WriteLine("PASS three Lite dependency packages verified and extracted with the launcher installer.");
    }
}

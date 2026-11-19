using SideWidget.Infrastructure;

namespace SideWidget.App;

static class LiteInstallation
{
    public static string? Root { get; } = FindRoot();
    public static string ApplicationDirectory => Root is null ? AppContext.BaseDirectory : Path.Combine(Root, "App");
    public static bool IsLite { get; } = Root is not null || DistributionArchive.IsLite(AppContext.BaseDirectory);
    public static string Launcher => Root is null ? Environment.ProcessPath! : Path.Combine(Root, "SideWidget.exe");
    static string VersionFolder { get; } = typeof(LiteInstallation).Assembly.GetName().Version!.ToString(3);
    public static string? Dependencies => Root is null ? null : Path.Combine(Root, "Dependencies", VersionFolder);
    public static bool LocalDesktop => Root is not null &&
        Path.GetFileName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)) == "desktop";

    public static StartupService Startup() => new(Launcher,
        previousExecutable: Root is null ? null : Path.Combine(ApplicationDirectory, "SideWidget.App.exe"));

    static string? FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        var parent = current.Parent;
        var root = current.Name == "App" ? parent :
            current.Name == "desktop" && parent?.Parent?.Name == "Dependencies" ? parent.Parent.Parent : null;
        return root is not null && File.Exists(Path.Combine(root.FullName, "SideWidget.exe")) &&
            DistributionArchive.IsLite(Path.Combine(root.FullName, "App")) ? root.FullName : null;
    }
}

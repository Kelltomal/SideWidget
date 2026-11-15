using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;
using Windows.Management.Deployment;
using Windows.System;

namespace SideWidget.App;

static class LiteDependencies
{
    const string DotNetKey = @"SOFTWARE\dotnet\Setup\InstalledVersions\x64";
    const string WebView2Key = @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
    const string VisualCppKey = @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64";
    const string WindowsAppRuntimeFamily = "Microsoft.WindowsAppRuntime.1.8_8wekyb3d8bbwe";
    static readonly Version MinimumWindowsAppRuntime = new(8000, 994, 2142, 0);

    public static bool AreAvailable()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
            return false;

        try
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var machine32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            return HasDotNet(machine) && HasWindowsAppRuntime() &&
                (HasWebView2(machine32) || HasWebView2(user)) &&
                (HasVisualCppRegistration(machine) || HasVisualCppRegistration(machine32)) && HasVisualCppLibraries();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException or
            ArgumentException or InvalidOperationException or BadImageFormatException or COMException)
        {
            return false;
        }
    }

    static bool HasDotNet(RegistryKey machine)
    {
        using var installed = machine.OpenSubKey(DotNetKey);
        var defaultRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
        if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
            defaultRoot = Path.Combine(defaultRoot, "x64");

        string?[] roots = [Environment.GetEnvironmentVariable("DOTNET_ROOT_X64"),
            Environment.GetEnvironmentVariable("DOTNET_ROOT"), installed?.GetValue("InstallLocation") as string, defaultRoot];
        var root = roots.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        if (root is null || !Path.IsPathFullyQualified(root) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_ROLL_FORWARD")))
            return false;

        return HasSharedVersion(Path.Combine(root, "host", "fxr"), path => IsX64(Path.Combine(path, "hostfxr.dll"))) &&
            HasSharedVersion(Path.Combine(root, "shared", "Microsoft.NETCore.App"), path =>
                File.Exists(Path.Combine(path, "Microsoft.NETCore.App.deps.json")) &&
                File.Exists(Path.Combine(path, "hostpolicy.dll")) && IsX64(Path.Combine(path, "coreclr.dll"))) &&
            HasSharedVersion(Path.Combine(root, "shared", "Microsoft.AspNetCore.App"), path =>
                File.Exists(Path.Combine(path, "Microsoft.AspNetCore.App.deps.json")) &&
                File.Exists(Path.Combine(path, "Microsoft.AspNetCore.Hosting.dll")));
    }

    static bool HasSharedVersion(string directory, Func<string, bool> isComplete) =>
        Directory.Exists(directory) && Directory.EnumerateDirectories(directory).Any(path =>
            Version.TryParse(Path.GetFileName(path), out var version) && version.Major == 10 && version.Minor == 0 &&
            version.Build >= 0 && isComplete(path));

    static bool IsX64(string file)
    {
        if (!File.Exists(file)) return false;
        using var reader = new PEReader(File.OpenRead(file));
        return reader.PEHeaders.CoffHeader.Machine == Machine.Amd64;
    }

    static bool HasWindowsAppRuntime() =>
        new PackageManager().FindPackagesForUser("", WindowsAppRuntimeFamily).Any(package =>
        {
            var id = package.Id;
            var version = id.Version;
            return id.Architecture == ProcessorArchitecture.X64 &&
                new Version(version.Major, version.Minor, version.Build, version.Revision) >= MinimumWindowsAppRuntime &&
                package.Status.VerifyIsOK();
        });

    static bool HasWebView2(RegistryKey registry)
    {
        using var client = registry.OpenSubKey(WebView2Key);
        return Version.TryParse(client?.GetValue("pv") as string, out var version) && version.Major > 0;
    }

    static bool HasVisualCppRegistration(RegistryKey machine)
    {
        using var runtime = machine.OpenSubKey(VisualCppKey);
        return runtime?.GetValue("Installed") is 1;
    }

    static bool HasVisualCppLibraries() =>
        new[] { "vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "concrt140.dll" }.All(name =>
        {
            var installed = Path.Combine(Environment.SystemDirectory, name);
            var bundled = Path.Combine(AppContext.BaseDirectory, name);
            if (!IsX64(installed)) return false;
            if (!File.Exists(bundled)) return true;
            return FileVersion(installed) >= FileVersion(bundled);
        });

    static Version FileVersion(string path)
    {
        var file = FileVersionInfo.GetVersionInfo(path);
        return new Version(file.FileMajorPart, file.FileMinorPart, file.FileBuildPart, file.FilePrivatePart);
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.7.2")]

internal sealed class SideWidgetPrerequisites
{
    private const string Repository = "https://github.com/Kelltomal/SideWidget/releases/download/";
    private const string WebView2Key = @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
    private const string AppRuntimeFamily = "Microsoft.WindowsAppRuntime.1.8_8wekyb3d8bbwe";
    private static readonly Version MinimumAppRuntime = new Version(8000, 994, 2142, 0);
    private readonly Dictionary<string, Pack> packs = new Dictionary<string, Pack>(StringComparer.Ordinal);
    private readonly string root, release, baseApp;
    internal string AppDirectory { get; private set; }
    internal string DotnetRoot { get; private set; }

    internal SideWidgetPrerequisites(string installRoot, Stream catalog)
    {
        baseApp = AppDirectory = Path.Combine(installRoot, "App");
        using (var reader = new StreamReader(catalog))
        {
            release = reader.ReadLine();
            Version version;
            if (!Version.TryParse(release, out version) || version.Build < 0 || version.Revision != -1)
                throw new InvalidDataException("Invalid dependency release.");
            for (string line; (line = reader.ReadLine()) != null;)
            {
                string[] fields = line.Split(' ');
                long size;
                if (fields.Length != 3 || !long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out size) ||
                    size <= 0 || size > 1024L * 1024 * 1024 || fields[2].Length != 64 ||
                    !Array.TrueForAll(fields[2].ToCharArray(), Uri.IsHexDigit))
                    throw new InvalidDataException("Invalid dependency package.");
                packs.Add(fields[0], new Pack { Size = size, Hash = fields[2] });
            }
            if (packs.Count != 3 || !packs.ContainsKey("dotnet") || !packs.ContainsKey("desktop") || !packs.ContainsKey("webview2"))
                throw new InvalidDataException("Incomplete dependency catalog.");
        }
        root = Path.Combine(installRoot, "Dependencies", release);
    }

    internal void Prepare()
    {
        string dotnet = Path.Combine(root, "dotnet");
        string desktop = Path.Combine(root, "desktop");
        string browser = Path.Combine(root, "webview2");
        bool needsDotnet = IsInstalled("dotnet", dotnet) || !HasSharedDotnet();
        bool needsDesktop = IsInstalled("desktop", desktop) || !HasSharedDesktop();
        // Once installed, keep using this release's verified fixed browser.
        bool needsBrowser = !HasSharedBrowser() || IsInstalled("webview2", browser);
        if ((needsDotnet && !IsInstalled("dotnet", dotnet)) ||
            (needsDesktop && !IsInstalled("desktop", desktop)) ||
            (needsBrowser && !IsInstalled("webview2", browser)))
        {
            Directory.CreateDirectory(root);
            // File lock covers concurrent launchers for this installation and releases on process exit.
            using (AcquireInstallLock())
            using (var progress = new DependencyProgress())
            {
                if (needsDotnet) EnsureInstalled("dotnet", dotnet, progress);
                if (needsDesktop) EnsureInstalled("desktop", desktop, progress);
                if (needsBrowser) EnsureInstalled("webview2", browser, progress);
            }
        }
        if (needsDotnet) DotnetRoot = dotnet;
        if (needsDesktop) AppDirectory = desktop;
    }

    private FileStream AcquireInstallLock()
    {
        string path = Path.Combine(root, ".install.lock");
        try { return File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException error)
        {
            if ((error.HResult & 0xffff) != 32 && (error.HResult & 0xffff) != 33) throw;
            throw new IOException("Another SideWidget launcher is installing dependencies. Wait for it to finish, then retry.", error);
        }
    }

    private bool IsInstalled(string name, string directory)
    {
        string marker = Path.Combine(directory, ".complete");
        return File.Exists(marker) && File.ReadAllText(marker).Equals(packs[name].Hash, StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureInstalled(string name, string destination, DependencyProgress progress)
    {
        if (IsInstalled(name, destination)) return;
        string file = "SideWidget-" + release + "-deps-" + name + "-win-x64.zip";
        progress.Stage("Downloading " + name + " from GitHub");
        var request = (HttpWebRequest)WebRequest.Create(Repository + "v" + release + "/" + file);
        request.Timeout = 30000;
        request.ReadWriteTimeout = 30000;
        request.UserAgent = "SideWidget";
        using (var response = (HttpWebResponse)request.GetResponse())
        using (var input = response.GetResponseStream())
        {
            if (response.ResponseUri.Scheme != Uri.UriSchemeHttps ||
                (response.ContentLength >= 0 && response.ContentLength != packs[name].Size))
                throw new InvalidDataException("Unexpected download source or size for " + file + ".");
            InstallPack(name, input, destination, progress.Stage, progress.Report);
        }
    }

    internal void InstallPack(string name, Stream input, string destination, Action<string> stage, Action<long, long> report)
    {
        string staging = ExtendedPath(Path.Combine(root, ".install-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(staging);
        try
        {
            string archive = Path.Combine(staging, "download.zip");
            string extracted = Path.Combine(staging, "files");
            Download(input, archive, packs[name].Size, packs[name].Hash, report);
            stage("Extracting " + name);
            using (var zip = ZipFile.OpenRead(archive))
            {
                foreach (var entry in zip.Entries)
                {
                    string path = ContainedPath(extracted, entry.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    entry.ExtractToFile(path);
                }
            }
            if (name == "desktop") ReuseAppFiles(extracted, ExtendedPath(baseApp));
            report(1, 1);
            File.WriteAllText(Path.Combine(extracted, ".complete"), packs[name].Hash);
            Directory.Move(extracted, ExtendedPath(destination));
        }
        finally { Directory.Delete(staging, true); }
    }

    private static string ExtendedPath(string path)
    {
        path = Path.GetFullPath(path);
        return path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + path.Substring(2) : @"\\?\" + path;
    }

    private static void Download(Stream input, string archive, long size, string expectedHash, Action<long, long> progress)
    {
        using (var hash = SHA256.Create())
        using (var output = new FileStream(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024))
        {
            var buffer = new byte[128 * 1024];
            long written = 0;
            int percent = -1;
            for (int count; (count = input.Read(buffer, 0, buffer.Length)) > 0;)
            {
                written += count;
                if (written > size) throw new InvalidDataException("Dependency download exceeds its expected size.");
                hash.TransformBlock(buffer, 0, count, null, 0);
                output.Write(buffer, 0, count);
                int next = (int)(written * 100 / size);
                if (percent != next)
                {
                    percent = next;
                    progress(written, size);
                }
            }
            hash.TransformFinalBlock(buffer, 0, 0);
            var actual = BitConverter.ToString(hash.Hash).Replace("-", "");
            if (written != size || !actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Dependency download failed SHA-256 verification.");
        }
    }

    private static void ReuseAppFiles(string extracted, string app)
    {
        string inventory = Path.Combine(extracted, "reuse.txt");
        if (!File.Exists(inventory)) return;
        foreach (string file in File.ReadLines(inventory))
        {
            string path = ContainedPath(extracted, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.Copy(ContainedPath(app, file), path);
        }
        File.Delete(inventory);
    }

    private static string ContainedPath(string directory, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) ||
            relative.IndexOfAny(new[] { ':', '\\' }) >= 0 ||
            Array.Exists(relative.Split('/'), part => part.Length == 0 || part.EndsWith(".") || part.EndsWith(" ")))
            throw new InvalidDataException("Invalid dependency file path.");
        return Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private static bool HasSharedDotnet()
    {
        string directory = Environment.GetEnvironmentVariable("DOTNET_ROOT_X64");
        if (string.IsNullOrWhiteSpace(directory))
            directory = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (string.IsNullOrWhiteSpace(directory))
        {
            using (var registry = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\dotnet\Setup\InstalledVersions\x64"))
                directory = registry == null ? null : registry.GetValue("InstallLocation") as string;
        }
        if (string.IsNullOrWhiteSpace(directory))
            directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
        return HasFramework(directory, "host/fxr", "hostfxr.dll") &&
            HasFramework(directory, "shared/Microsoft.NETCore.App", "coreclr.dll") &&
            HasFramework(directory, "shared/Microsoft.AspNetCore.App", "Microsoft.AspNetCore.Hosting.dll");
    }

    private static bool HasFramework(string root, string path, string requiredFile)
    {
        string directory = Path.Combine(root, path);
        if (!Path.IsPathRooted(root) || !Directory.Exists(directory)) return false;
        foreach (string framework in Directory.EnumerateDirectories(directory))
        {
            Version version;
            if (Version.TryParse(Path.GetFileName(framework), out version) && version.Major == 10 &&
                version.Minor == 0 && version.Build >= 0 && File.Exists(Path.Combine(framework, requiredFile)))
                return true;
        }
        return false;
    }

    private static bool HasSharedBrowser()
    {
        using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            return HasBrowserRegistration(machine) || HasBrowserRegistration(Registry.CurrentUser);
    }

    private static bool HasBrowserRegistration(RegistryKey hive)
    {
        using (var key = hive.OpenSubKey(WebView2Key))
        {
            Version version;
            return key != null && Version.TryParse(key.GetValue("pv") as string, out version) && version.Major > 0;
        }
    }

    private static bool HasSharedDesktop()
    {
        foreach (string library in new[] { "vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "concrt140.dll" })
            if (!File.Exists(Path.Combine(Environment.SystemDirectory, library))) return false;

        uint count = 0, length = 0;
        if (GetPackagesByPackageFamily(AppRuntimeFamily, ref count, null, ref length, IntPtr.Zero) != 122 || count == 0)
            return false;
        var names = new IntPtr[count];
        IntPtr buffer = Marshal.AllocHGlobal(checked((int)length * 2));
        try
        {
            if (GetPackagesByPackageFamily(AppRuntimeFamily, ref count, names, ref length, buffer) != 0)
                return false;
            for (int i = 0; i < count; i++)
            {
                var parts = Marshal.PtrToStringUni(names[i]).Split('_');
                Version version;
                if (parts.Length == 5 && parts[2] == "x64" && Version.TryParse(parts[1], out version) &&
                    version >= MinimumAppRuntime)
                    return true;
            }
            return false;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int GetPackagesByPackageFamily(string family, ref uint count, [Out] IntPtr[] names, ref uint length, IntPtr buffer);

    private sealed class Pack
    {
        internal long Size;
        internal string Hash;
    }
}

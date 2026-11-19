using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

namespace SideWidget.Infrastructure;

public sealed class PortableUpdate(HttpClient? transport = null)
{
    const long MaximumBytes = 2L * 1024 * 1024 * 1024;
    static readonly HttpClient Downloads = new() { Timeout = Timeout.InfiniteTimeSpan };

    public Task<string> PrepareAsync(GitHubPortableAsset asset, Version version, string versionsDirectory,
        Action<int?> progress, CancellationToken cancellationToken = default) =>
        PrepareAsync(asset, version, versionsDirectory, progress, lite: false, cancellationToken);

    public async Task<string> PrepareAsync(GitHubPortableAsset asset, Version version, string versionsDirectory,
        Action<int?> progress, bool lite, CancellationToken cancellationToken = default)
    {
        var legacy = !lite && asset.Name == GitHubPortableAsset.LegacyArchiveName(version);
        if (version.Revision != -1 || (asset.Tag != $"v{version}" && asset.Tag != $"V{version}" && asset.Tag != $"{version}") ||
            (!legacy && asset.Name != GitHubPortableAsset.ArchiveName(version, lite)) || asset.Size is <= 0 or > MaximumBytes ||
            asset.Sha256.Length != 64 || !asset.Sha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid update metadata.");

        Directory.CreateDirectory(versionsDirectory);
        var id = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(versionsDirectory, $".update-{id}");
        var target = Path.Combine(versionsDirectory, $"{version}-{id[..8]}");
        Directory.CreateDirectory(staging);
        try
        {
            var archive = Path.Combine(staging, "release.zip");
            var content = Path.Combine(staging, "application");
            var applicationDirectory = legacy ? "" : "App";
            var application = Path.Combine(content, applicationDirectory);
            await DownloadAsync(asset, archive, progress, cancellationToken).ConfigureAwait(false);
            progress(null);
            await Task.Run(() =>
            {
                using var zip = ZipFile.OpenRead(archive);
                if (zip.Entries.Count > 10000 || zip.Entries.Sum(entry => entry.Length) > MaximumBytes)
                    throw new InvalidDataException("Invalid update archive size.");
                zip.ExtractToDirectory(content);
                if (!legacy)
                    ValidateLayout(content, application, lite);
                DistributionArchive.Validate(application, expectedLite: lite);
                var assembled = AssemblyName.GetAssemblyName(Path.Combine(application, "SideWidget.App.dll")).Version;
                if (assembled != new Version(version.Major, version.Minor, version.Build, 0))
                    throw new InvalidDataException("Update assembly version differs from its release tag.");
                cancellationToken.ThrowIfCancellationRequested();
                Directory.Move(content, target);
            }, cancellationToken).ConfigureAwait(false);
            return lite ? Path.Combine(target, "SideWidget.exe") :
                Path.Combine(target, applicationDirectory, "SideWidget.App.exe");
        }
        finally
        {
            try { Directory.Delete(staging, recursive: true); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }

    static void ValidateLayout(string content, string application, bool lite)
    {
        string[] rootFiles = lite ? ["SideWidget.exe", "Install-Dependencies.bat"] : ["SideWidget.exe"];
        var rootEntries = Directory.EnumerateFileSystemEntries(content).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);
        if (!rootEntries.SetEquals(rootFiles.Append("App")) || !Directory.Exists(application))
            throw new InvalidDataException("Invalid portable update layout.");
        foreach (var name in rootFiles)
        {
            var file = new FileInfo(Path.Combine(content, name));
            if (!file.Exists || file.Length == 0)
                throw new InvalidDataException("Invalid portable update layout.");
        }
    }

    async Task DownloadAsync(GitHubPortableAsset asset, string destination, Action<int?> progress, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUrl);
        request.Headers.UserAgent.ParseAdd("SideWidget");
        using var response = await (transport ?? Downloads).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long size && size != asset.Size)
            throw new InvalidDataException("Update download size differs from GitHub metadata.");
        await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, useAsync: true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024];
        long written = 0;
        var percent = -1;
        for (int count; (count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0;)
        {
            written += count;
            if (written > asset.Size) throw new InvalidDataException("Update download exceeds its declared size.");
            hash.AppendData(buffer.AsSpan(0, count));
            await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
            var next = (int)(written * 100 / asset.Size);
            if (next != percent) { percent = next; progress(percent); }
        }
        if (written != asset.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Update download failed SHA-256 verification.");
    }
}

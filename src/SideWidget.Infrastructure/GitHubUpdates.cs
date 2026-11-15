using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SideWidget.Infrastructure;

[JsonConverter(typeof(JsonStringEnumConverter<GitHubUpdateStatus>))]
public enum GitHubUpdateStatus
{
    [JsonStringEnumMemberName("available")]
    Available,
    [JsonStringEnumMemberName("current")]
    Current,
    [JsonStringEnumMemberName("unpublished")]
    Unpublished,
    [JsonStringEnumMemberName("unavailable")]
    Unavailable,
}

public sealed record GitHubUpdateResult(
    GitHubUpdateStatus Status,
    string CurrentVersion,
    string? LatestVersion = null,
    string? ReleaseUrl = null,
    bool HasPortableAsset = false,
    GitHubPortableAsset? PortableAsset = null);

public sealed record GitHubPortableAsset(string Tag, string Name, long Size, string Sha256)
{
    public Uri DownloadUrl => new($"https://github.com/Kelltomal/SideWidget/releases/download/{Tag}/{Name}");

    [JsonIgnore]
    public bool IsLite => Name.EndsWith("-lite-win-x64.zip", StringComparison.Ordinal);

    internal static string ArchiveName(Version version, bool lite) =>
        $"SideWidget-{version}-{(lite ? "lite" : "portable")}-win-x64.zip";

    internal static string LegacyArchiveName(Version version) => $"SideWidget-{version}-win-x64.zip";
}

public sealed class GitHubUpdates(HttpClient? transport = null)
{
    const string Repository = "Kelltomal/SideWidget";
    const int ResponseLimit = 1024 * 1024;
    static readonly Uri LatestRelease = new($"https://api.github.com/repos/{Repository}/releases/latest");
    static readonly HttpClient PublicClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    });

    public Task<GitHubUpdateResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default) =>
        CheckAsync(currentVersion, lite: false, cancellationToken);

    public async Task<GitHubUpdateResult> CheckAsync(Version currentVersion, bool lite, CancellationToken cancellationToken = default,
        bool preferLite = false)
    {
        // Assembly versions generally add a fourth, zero revision component.
        var current = currentVersion.Revision > 0
            ? currentVersion
            : new Version(currentVersion.Major, currentVersion.Minor, Math.Max(0, currentVersion.Build));
        var unavailable = new GitHubUpdateResult(GitHubUpdateStatus.Unavailable, current.ToString());
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestRelease);
            request.Headers.UserAgent.ParseAdd("SideWidget");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
            using var response = await (transport ?? PublicClient)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return unavailable with { Status = GitHubUpdateStatus.Unpublished };
            if (response.StatusCode != HttpStatusCode.OK)
                return unavailable;

            await response.Content.LoadIntoBufferAsync(ResponseLimit, deadline.Token).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(deadline.Token).ConfigureAwait(false);
            using var document = JsonDocument.Parse(bytes);
            var release = document.RootElement;

            if (release.ValueKind != JsonValueKind.Object ||
                !release.TryGetProperty("draft", out var draft) || draft.ValueKind != JsonValueKind.False ||
                !release.TryGetProperty("prerelease", out var prerelease) || prerelease.ValueKind != JsonValueKind.False ||
                ReadString(release, "tag_name") is not { Length: > 0 and <= 33 } tag)
                return unavailable;

            var versionText = tag[0] is 'v' or 'V' ? tag[1..] : tag;
            // Canonical three-part versions make the original tag safe as a single URL segment.
            if (!Version.TryParse(versionText, out var latest) || latest.Build < 0 || latest.Revision >= 0 ||
                versionText != latest.ToString())
                return unavailable;

            if (latest <= current)
                return new(GitHubUpdateStatus.Current, unavailable.CurrentVersion, versionText);

            var (hasPortableAsset, verified) = lite || preferLite
                ? FindPortable(release, tag, GitHubPortableAsset.ArchiveName(latest, lite: true)) : default;
            if (!lite && verified is null)
            {
                (hasPortableAsset, verified) = FindPortable(release, tag, GitHubPortableAsset.ArchiveName(latest, lite: false));
                if (!hasPortableAsset)
                    (hasPortableAsset, verified) = FindPortable(release, tag, GitHubPortableAsset.LegacyArchiveName(latest));
            }
            return new(GitHubUpdateStatus.Available, unavailable.CurrentVersion, versionText,
                $"https://github.com/{Repository}/releases/tag/{tag}", hasPortableAsset, verified);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return unavailable;
        }
        catch (Exception error) when (error is HttpRequestException or IOException or JsonException)
        {
            return unavailable;
        }
    }

    static (bool Uploaded, GitHubPortableAsset? Verified) FindPortable(JsonElement release, string tag, string name)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return (false, null);
        var portable = assets.EnumerateArray().FirstOrDefault(asset => IsUploadedPortable(asset, name));
        if (portable.ValueKind != JsonValueKind.Object)
            return (false, null);
        var digest = ReadString(portable, "digest");
        var verified = digest is { Length: 71 } && digest.StartsWith("sha256:", StringComparison.Ordinal) &&
            digest[7..].All(Uri.IsHexDigit)
            ? new GitHubPortableAsset(tag, name, portable.GetProperty("size").GetInt64(), digest[7..]) : null;
        return (true, verified);
    }

    static bool IsUploadedPortable(JsonElement asset, string portableName) =>
        asset.ValueKind == JsonValueKind.Object &&
        ReadString(asset, "name") == portableName && ReadString(asset, "state") == "uploaded" &&
        asset.TryGetProperty("size", out var size) && size.ValueKind == JsonValueKind.Number && size.TryGetInt64(out var bytes) && bytes > 0;

    static string? ReadString(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var property) || property.ValueKind != JsonValueKind.String)
            return null;
        try
        {
            return property.GetString();
        }
        catch (InvalidOperationException)
        {
            // A syntactically valid JSON string can still contain an invalid Unicode escape.
            return null;
        }
    }
}

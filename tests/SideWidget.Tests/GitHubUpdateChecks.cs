using System.Net;
using System.Text;
using System.Text.Json;
using SideWidget.Infrastructure;

public static class GitHubUpdateChecks
{
    public static async Task<int> Run()
    {
        var checks = 0;
        void Check(bool condition, string description)
        {
            if (!condition) throw new Exception("GitHub update: " + description);
            checks++;
        }

        var current = new Version(0, 2, 0, 0);
        using var client = new HttpClient(new Handler((request, _) =>
        {
            Check(request.Method == HttpMethod.Get && request.RequestUri?.AbsoluteUri ==
                "https://api.github.com/repos/Kelltomal/SideWidget/releases/latest", "only requests the official stable endpoint");
            Check(request.Headers.Authorization is null && request.Headers.UserAgent.Count > 0,
                "sends a user agent and no GitHub credentials");
            return Task.FromResult(Json(Release("v0.10.0", "SideWidget-0.10.0-win-x64.zip")));
        }));
        var available = await new GitHubUpdates(client).CheckAsync(current);
        Check(available is { Status: GitHubUpdateStatus.Available, CurrentVersion: "0.2.0", LatestVersion: "0.10.0", HasPortableAsset: true },
            "compares numeric versions and identifies the portable Windows ZIP");
        Check(available.ReleaseUrl == "https://github.com/Kelltomal/SideWidget/releases/tag/v0.10.0",
            "opens the official release page even if html_url points elsewhere");
        Check(available.PortableAsset is null, "auto-install requires an official SHA-256 digest");
        var digest = new string('a', 64);
        var installable = await Respond(current, Json(Release("v0.3.0", "SideWidget-0.3.0-win-x64.zip", digest: "sha256:" + digest)));
        Check(installable.PortableAsset is { Size: 4096 } zip && zip.Sha256 == digest &&
            zip.DownloadUrl.AbsoluteUri == "https://github.com/Kelltomal/SideWidget/releases/download/v0.3.0/SideWidget-0.3.0-win-x64.zip",
            "verified metadata only points at the official versioned asset");
        var workspace = Path.Combine(Path.GetTempPath(), $"sidewidget-update-test-{Guid.NewGuid():N}");
        using (var download = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[4096]),
        }))))
        {
            try
            {
                await new PortableUpdate(download).PrepareAsync(installable.PortableAsset!, new Version(0, 3, 0), workspace, _ => { });
                throw new Exception("GitHub update: invalid digest was accepted");
            }
            catch (InvalidDataException)
            {
                Check(!Directory.EnumerateFileSystemEntries(workspace).Any(), "invalid archive leaves no staged installation");
            }
            finally { if (Directory.Exists(workspace)) Directory.Delete(workspace); }
        }
        using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(available));
        Check(serialized.RootElement.GetProperty("Status").GetString() == "available", "serializes the status in lower case");

        foreach (var tag in new[] { "v0.2.0", "v0.1.9" })
        {
            var result = await Respond(current, Json(Release(tag)));
            Check(result is { Status: GitHubUpdateStatus.Current, ReleaseUrl: null }, "equal and older releases cannot prompt updates");
        }

        var withoutZip = await Respond(current, Json(Release("0.3.0", "SideWidget-0.3.0-win-x64.zip.sha256")));
        Check(withoutZip is { Status: GitHubUpdateStatus.Available, HasPortableAsset: false, ReleaseUrl: "https://github.com/Kelltomal/SideWidget/releases/tag/0.3.0" },
            "a checksum alone leaves portable availability false and preserves the official release link");

        foreach (var invalid in new[]
        {
            Release("v0.3.0-beta.1"), Release("v0.3.0/../../other"), Release("v0.3.0\n"),
            Release("v0.3.0", prerelease: true), Release("v0.3.0", draft: true),
            "{\"tag_name\":\"v0.3.0\"}", """{"tag_name":"\ud800","draft":false,"prerelease":false}""",
            "invalid JSON", new string(' ', 1024 * 1024) + Release("v0.3.0"),
        })
        {
            var result = await Respond(current, Json(invalid));
            Check(result is { Status: GitHubUpdateStatus.Unavailable, ReleaseUrl: null }, "invalid, unstable or oversized responses expose no release link");
        }

        Check((await Respond(current, new HttpResponseMessage(HttpStatusCode.NotFound))).Status == GitHubUpdateStatus.Unpublished,
            "GitHub 404 maps to no published stable release");
        foreach (var status in new[] { HttpStatusCode.Found, HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable })
        {
            var result = await Respond(current, new HttpResponseMessage(status));
            Check(result is { Status: GitHubUpdateStatus.Unavailable, ReleaseUrl: null }, "GitHub failures and redirects report unavailable");
        }

        using var offline = new HttpClient(new Handler((_, _) => throw new HttpRequestException("test connection unavailable")));
        Check((await new GitHubUpdates(offline).CheckAsync(current)).Status == GitHubUpdateStatus.Unavailable,
            "network errors report unavailable");
        using var cancel = new CancellationTokenSource();
        using var hanging = new HttpClient(new Handler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Json(Release("v0.3.0"));
        }));
        var pending = new GitHubUpdates(hanging).CheckAsync(current, cancel.Token);
        cancel.Cancel();
        try
        {
            await pending;
            throw new Exception("GitHub update: caller cancellation must propagate");
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { checks++; }

        Console.WriteLine($"PASS {checks} GitHub update checks; synthetic HTTP only.");
        return checks;
    }

    static string Release(string tag, string? asset = null, bool prerelease = false, bool draft = false, string? digest = null) =>
        JsonSerializer.Serialize(new
        {
            tag_name = tag,
            html_url = "https://example.net/foreign-release",
            prerelease,
            draft,
            assets = asset is null ? [] : new[] { new { name = asset, state = "uploaded", size = 4096, digest } },
        });

    static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    static async Task<GitHubUpdateResult> Respond(Version current, HttpResponseMessage response)
    {
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(response)));
        return await new GitHubUpdates(client).CheckAsync(current);
    }

    sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request, cancellationToken);
    }
}

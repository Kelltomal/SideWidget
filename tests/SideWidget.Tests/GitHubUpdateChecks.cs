using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
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
        const string legacyName = "SideWidget-0.3.0-win-x64.zip";
        const string fullName = "SideWidget-0.3.0-portable-win-x64.zip";
        const string liteName = "SideWidget-0.3.0-lite-win-x64.zip";
        var lite = await Respond(current, Json(Release("v0.3.0", fullName, digest: "sha256:" + digest,
            additionalAsset: liteName)), lite: true);
        Check(lite.PortableAsset is { Name: liteName, Size: 4096 } liteZip && liteZip.Sha256 == digest &&
            liteZip.DownloadUrl.AbsoluteUri == "https://github.com/Kelltomal/SideWidget/releases/download/v0.3.0/SideWidget-0.3.0-lite-win-x64.zip",
            "lite selects only the verified lite asset when both are uploaded");
        var full = await Respond(current, Json(Release("v0.3.0", liteName, digest: "sha256:" + digest,
            additionalAsset: fullName)));
        Check(full.PortableAsset is { Name: fullName }, "full selects only the full asset when both are uploaded");
        var preferLite = await Respond(current, Json(Release("v0.3.0", fullName, digest: "sha256:" + digest,
            additionalAsset: liteName)), preferLite: true);
        Check(preferLite.PortableAsset is { Name: liteName, IsLite: true },
            "full with shared prerequisites prefers verified lite");
        var preferLiteUnverified = await Respond(current, Json(Release("v0.3.0", fullName, digest: "sha256:" + digest,
            additionalAsset: liteName, additionalDigest: "invalid")), preferLite: true);
        Check(preferLiteUnverified.PortableAsset is { Name: fullName, IsLite: false },
            "full with shared prerequisites falls back to verified full when the lite digest is invalid");
        var onlyLiteWithoutPrerequisites = await Respond(current, Json(Release("v0.3.0", liteName, digest: "sha256:" + digest)));
        Check(onlyLiteWithoutPrerequisites is { PortableAsset: null, ReleaseUrl: "https://github.com/Kelltomal/SideWidget/releases/tag/v0.3.0" },
            "full without shared prerequisites cannot auto-install even verified lite");
        var fullWithLegacy = await Respond(current, Json(Release("v0.3.0", legacyName, digest: "sha256:" + digest,
            additionalAsset: fullName)));
        Check(fullWithLegacy.PortableAsset is { Name: fullName }, "full prefers the clean archive over the legacy flat archive");
        var liteMissing = await Respond(current, Json(Release("v0.3.0", fullName, digest: "sha256:" + digest)), lite: true);
        Check(liteMissing is { Status: GitHubUpdateStatus.Available, HasPortableAsset: false, PortableAsset: null,
            ReleaseUrl: "https://github.com/Kelltomal/SideWidget/releases/tag/v0.3.0" },
            "lite offers the release page when only the verified full asset exists");
        var liteUnverified = await Respond(current, Json(Release("v0.3.0", liteName, digest: "sha256:" + new string('x', 64))), lite: true);
        Check(liteUnverified is { Status: GitHubUpdateStatus.Available, HasPortableAsset: true, PortableAsset: null,
            ReleaseUrl: "https://github.com/Kelltomal/SideWidget/releases/tag/v0.3.0" },
            "lite keeps the release link when its uploaded archive has an invalid digest");
        var workspace = Path.Combine(Path.GetTempPath(), $"sidewidget-update-test-{Guid.NewGuid():N}");
        using (var download = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[4096]),
        }))))
        {
            try
            {
                foreach (var (offer, isLite) in new[] { (installable, false), (lite, true) })
                {
                    await Reject(() => new PortableUpdate(download).PrepareAsync(offer.PortableAsset!, new Version(0, 3, 0),
                        workspace, _ => { }, lite: isLite), "invalid digest was accepted");
                    Check(!Directory.EnumerateFileSystemEntries(workspace).Any(), "invalid digest leaves no staged installation");
                }
            }
            finally { if (Directory.Exists(workspace)) Directory.Delete(workspace); }
        }
        foreach (var (isLite, legacy) in new[] { (false, false), (true, false), (false, true) })
        {
            var app = Path.Combine(workspace, "fixture");
            var versions = Path.Combine(workspace, "versions");
            Directory.CreateDirectory(app);
            try
            {
                var archive = MakePortableFixture(app, workspace, isLite, out var version);
                var bytes = legacy ? File.ReadAllBytes(archive) : WrapPortableFixture(archive, isLite);
                using var download = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(bytes),
                })));
                var updater = new PortableUpdate(download);
                var suffix = legacy ? "" : isLite ? "-lite" : "-portable";
                var asset = new GitHubPortableAsset($"v{version}", $"SideWidget-{version}{suffix}-win-x64.zip", bytes.Length,
                    Convert.ToHexString(SHA256.HashData(bytes)));
                var executable = await updater.PrepareAsync(asset, version, versions, _ => { }, lite: isLite);
                var launchDirectory = Path.GetDirectoryName(executable)!;
                var application = isLite ? Path.Combine(launchDirectory, "App") : launchDirectory;
                Check(File.Exists(executable) && Path.GetFileName(executable) == (isLite ? "SideWidget.exe" : "SideWidget.App.exe") &&
                    (Path.GetFileName(application) == "App") == !legacy && DistributionArchive.IsLite(application) == isLite,
                    "matching portable archive stages an executable of the same edition");
                await Reject(() => updater.PrepareAsync(asset, version, versions, _ => { }, lite: !isLite),
                    "asset name for a different edition was accepted");
                var otherSuffix = isLite ? "-portable" : "-lite";
                bytes = WrapPortableFixture(archive, !isLite);
                var mislabeled = new GitHubPortableAsset($"v{version}", $"SideWidget-{version}{otherSuffix}-win-x64.zip",
                    bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
                await Reject(() => updater.PrepareAsync(mislabeled, version, versions, _ => { }, lite: !isLite),
                    "archive with a manifest for a different edition was accepted");
                bytes = WrapPortableFixture(archive, isLite, includeLauncher: false);
                var missingLauncher = new GitHubPortableAsset($"v{version}",
                    $"SideWidget-{version}{(isLite ? "-lite" : "-portable")}-win-x64.zip", bytes.Length,
                    Convert.ToHexString(SHA256.HashData(bytes)));
                await Reject(() => updater.PrepareAsync(missingLauncher, version, versions, _ => { }, lite: isLite),
                    "clean archive without a launcher was accepted");
                Check(Directory.EnumerateFileSystemEntries(versions).Count() == 1,
                    "rejected archive leaves only the previously staged installation");
            }
            finally { Directory.Delete(workspace, recursive: true); }
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
        foreach (var status in new[] { HttpStatusCode.Found, HttpStatusCode.ServiceUnavailable })
        {
            var result = await Respond(current, new HttpResponseMessage(status));
            Check(result is { Status: GitHubUpdateStatus.Unavailable, ReleaseUrl: null }, "GitHub failures and redirects report unavailable");
        }

        using var exhausted = new HttpResponseMessage(HttpStatusCode.Forbidden);
        exhausted.Headers.Add("X-RateLimit-Remaining", "0");
        var reset = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds());
        exhausted.Headers.Add("X-RateLimit-Reset", reset.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        var rateLimited = await Respond(current, exhausted);
        Check(rateLimited.Status == GitHubUpdateStatus.RateLimited && rateLimited.RetryAt == reset &&
            rateLimited.ReleaseUrl == "https://github.com/Kelltomal/SideWidget/releases/latest",
            "exhausted public quota has a distinct status and manual download");
        using var backoff = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        backoff.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
        Check((await Respond(current, backoff)).Status == GitHubUpdateStatus.RateLimited, "429 reports rate limit");
        using var denied = new HttpResponseMessage(HttpStatusCode.Forbidden);
        Check((await Respond(current, denied)).Status == GitHubUpdateStatus.Forbidden, "403 without quota evidence reports forbidden");

        var bearerRequests = 0;
        using var rejected = new HttpClient(new Handler((request, _) =>
        {
            bearerRequests++;
            Check(request.RequestUri?.Host == "api.github.com" &&
                request.Headers.Authorization is { Scheme: "Bearer", Parameter: "fixture_token" },
                "bearer token is sent only with the GitHub API request");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }));
        var rejectedResult = await new GitHubUpdates(rejected).CheckAsync(current, lite: false, accessToken: "fixture_token");
        Check(rejectedResult.Status == GitHubUpdateStatus.AuthRequired && bearerRequests == 1,
            "invalid bearer token reports reauthentication without redirect or retry");

        var deviceReplies = new Queue<string>([
            """{"device_code":"fixture_device","user_code":"AB12-XY34","verification_uri":"https://github.com/login/device","expires_in":900,"interval":1}""",
            """{"error":"authorization_pending"}""",
            """{"access_token":"fixture_access","token_type":"bearer","scope":"","expires_in":28800,"refresh_token":"fixture_refresh"}""",
            """{"access_token":"fixture_renewed","token_type":"bearer","scope":"","expires_in":28800,"refresh_token":"fixture_rotated"}"""
        ]);
        var requests = new List<(string Path, string Form)>();
        using var deviceTransport = new HttpClient(new Handler(async (request, token) =>
        {
            Check(request.RequestUri?.Host == "github.com" && request.Method == HttpMethod.Post &&
                 request.Headers.Authorization is null, "device requests use GitHub HTTPS without bearer headers");
            requests.Add((request.RequestUri!.AbsolutePath, await request.Content!.ReadAsStringAsync(token)));
            return Json(deviceReplies.Dequeue());
        }));
        string? deviceUserCode = null;
        var oauth = new GitHubOAuth(deviceTransport);
        var deviceCredential = await oauth.AuthorizeAsync(code => deviceUserCode = code, CancellationToken.None);
        Check(deviceUserCode == "AB12-XY34" && deviceCredential is
            { AccessToken: "fixture_access", RefreshToken: "fixture_refresh" } && requests.Count == 3,
            "device authorization displays only the user code and handles pending approval");
        Check(requests[0].Path == "/login/device/code" && !requests[0].Form.Contains("scope", StringComparison.Ordinal) &&
            requests.Skip(1).All(request => request.Path == "/login/oauth/access_token" &&
                request.Form.Contains("device_code=fixture_device", StringComparison.Ordinal) &&
                !request.Form.Contains("client_secret", StringComparison.Ordinal)),
            "device authorization requests no additional GitHub permissions or client secret");
        var renewed = await oauth.RefreshAsync("fixture_refresh", CancellationToken.None);
        Check(renewed is { AccessToken: "fixture_renewed", RefreshToken: "fixture_rotated" } &&
            requests.Last().Form.Contains("grant_type=refresh_token&refresh_token=fixture_refresh", StringComparison.Ordinal),
            "device credentials can rotate without a client secret");

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

    static string MakePortableFixture(string app, string output, bool lite, out Version version)
    {
        var required = lite
            ? DistributionArchive.LiteRequiredFiles
            : DistributionArchive.RequiredFiles.Append("WebView2Runtime/Locales/ru.pak");
        foreach (var file in required)
        {
            var path = Path.Combine(app, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "test-fixture");
        }
        File.WriteAllText(Path.Combine(app, "SideWidget.App.runtimeconfig.json"),
            lite
                ? """{"runtimeOptions":{"frameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"},{"name":"Microsoft.AspNetCore.App","version":"10.0.0"}]}}"""
                : """{"runtimeOptions":{"includedFrameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.14"}]}}""");
        File.WriteAllText(Path.Combine(app, "SideWidget.App.deps.json"),
            """{"runtimeTarget":{"name":".NETCoreApp,Version=v10.0/win-x64"},"targets":{".NETCoreApp,Version=v10.0/win-x64":{}}}""");
        var assembly = typeof(GitHubUpdateChecks).Assembly;
        var assembled = assembly.GetName().Version!;
        version = new Version(assembled.Major, assembled.Minor, assembled.Build);
        File.Copy(assembly.Location, Path.Combine(app, "SideWidget.App.dll"), overwrite: true);
        DistributionArchive.Prepare(app, lite);
        var archive = Path.Combine(output, "fixture.zip");
        ZipFile.CreateFromDirectory(app, archive);
        return archive;
    }

    static async Task Reject(Func<Task> run, string failure)
    {
        try { await run(); }
        catch (InvalidDataException) { return; }
        throw new Exception("GitHub update: " + failure);
    }

    static byte[] WrapPortableFixture(string archive, bool lite, bool includeLauncher = true)
    {
        using var output = new MemoryStream();
        using (var wrapper = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (includeLauncher)
            {
                using var launcher = wrapper.CreateEntry("SideWidget.exe").Open();
                launcher.WriteByte(1);
            }
            if (lite)
            {
                using var installer = wrapper.CreateEntry("Install-Dependencies.bat").Open();
                installer.WriteByte(1);
            }
            using var source = ZipFile.OpenRead(archive);
            foreach (var entry in source.Entries)
            {
                using var input = entry.Open();
                using var entryOutput = wrapper.CreateEntry("App/" + entry.FullName).Open();
                input.CopyTo(entryOutput);
            }
        }
        return output.ToArray();
    }

    static string Release(string tag, string? asset = null, bool prerelease = false, bool draft = false, string? digest = null,
        string? additionalAsset = null, string? additionalDigest = null) =>
        JsonSerializer.Serialize(new
        {
            tag_name = tag,
            html_url = "https://example.net/foreign-release",
            prerelease,
            draft,
            assets = new[] { asset, additionalAsset }.Where(name => name is not null)
                .Select(name => new { name, state = "uploaded", size = 4096,
                    digest = name == additionalAsset ? additionalDigest ?? digest : digest }),
        });

    static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    static async Task<GitHubUpdateResult> Respond(Version current, HttpResponseMessage response, bool lite = false, bool preferLite = false)
    {
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(response)));
        return await new GitHubUpdates(client).CheckAsync(current, lite, preferLite: preferLite);
    }

    sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request, cancellationToken);
    }
}

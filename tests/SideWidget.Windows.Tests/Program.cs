using System.Diagnostics;
using System.Text.Json;
using SideWidget.App;

if(args.SequenceEqual(new[]{"--explorer-reveal-tests"})){await ExplorerRevealChecks.Run();return 0;}

if (args.Any(argument => argument != "--public-online"))
{
    Console.Error.WriteLine("Usage: SideWidget.Windows.Tests [--public-online]");
    return 2;
}
if (!OperatingSystem.IsWindows())
{
    Console.WriteLine("SKIP: native Windows APIs require Windows.");
    return 0;
}

var checks = new Checks();
await checks.Run("tray finds the newest complete local build and skips older or broken builds", () =>
{
    var root=Directory.CreateTempSubdirectory("sidewidget-switch-");
    try
    {
        var now=DateTime.UtcNow;
        string Build(string relative,int minutes)
        {
            var directory=Path.GetFullPath(Path.Combine(root.FullName,relative));
            foreach(var name in SideWidget.Infrastructure.DistributionArchive.RequiredFiles.Append("WebView2Runtime/Locales/ru.pak"))
            {
                var path=Path.Combine(directory,name);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path,relative);
            }
            File.WriteAllText(Path.Combine(directory,"SideWidget.App.runtimeconfig.json"),"""{"runtimeOptions":{"includedFrameworks":[{"name":"Microsoft.NETCore.App"}]}}""");
            File.WriteAllText(Path.Combine(directory,"SideWidget.App.deps.json"),"""{"runtimeTarget":{"name":"win/win-x64"},"targets":{"win/win-x64":{}}}""");
            SideWidget.Infrastructure.DistributionArchive.Prepare(directory);
            File.SetLastWriteTimeUtc(Path.Combine(directory,"distribution-files.json"),now.AddMinutes(minutes));
            return Path.Combine(directory,"SideWidget.App.exe");
        }
        var current=Build("output/current",-10);
        Build("output/older",-20);
        var recent=Build("artifacts/release",-4);
        var newest=Build("output/newest",-2);
        var broken=Build("output/broken",-1);
        File.WriteAllText(broken,"broken");
        var next=BuildSwitch.FindNewer(current);
        if(next?.FileName!=newest||next.WorkingDirectory!=Path.GetDirectoryName(newest))throw new ArgumentException("Newest output build was not selected.");
        if(BuildSwitch.FindNewer(newest) is not null)throw new InvalidOperationException("Current or corrupt build was selected.");
        if(BuildSwitch.FindNewer(recent)?.FileName!=newest)throw new NotSupportedException("Release could not find the newer output build.");
    }
    finally{root.Delete(recursive:true);}
    return Task.CompletedTask;
});
await checks.Run("tray build switch rejects the current executable and incomplete selections", () =>
{
    var current=Path.Combine(AppContext.BaseDirectory,"SideWidget.App.exe");
    RejectBuild(current);
    RejectBuild(Path.Combine(AppContext.BaseDirectory,"other.exe"));
    RejectBuild(Path.Combine(AppContext.BaseDirectory,"incomplete","SideWidget.App.exe"));
    return Task.CompletedTask;

    void RejectBuild(string candidate)
    {
        try {BuildSwitch.Validate(candidate,current);}
        catch(InvalidDataException){return;}
        throw new Exception("Unexpected build accepted: "+candidate);
    }
});
await checks.Run("startup registration, Windows override and removal", StartupChecks.Run);
await checks.Run("launcher shortcut identity and transactional undo", LauncherChecks.Run);
await checks.Run("system metrics", async () =>
{
    var metrics = new SystemMetrics();
    var initial = Json(metrics.Read());
    Require(initial.GetProperty("cpuPercent").ValueKind == JsonValueKind.Null);
    Require(initial.GetProperty("network").GetProperty("receiveBytesPerSecond").ValueKind == JsonValueKind.Null);
    await Task.Delay(250);
    var current = Json(metrics.Read());
    if (current.GetProperty("cpuPercent").ValueKind == JsonValueKind.Null) checks.Skip("CPU measurement unavailable");
    else Require(current.GetProperty("cpuPercent").GetDouble() is >= 0 and <= 100);
    if (current.GetProperty("memoryTotal").ValueKind == JsonValueKind.Null) checks.Skip("memory measurement unavailable");
    else
    {
        var total = current.GetProperty("memoryTotal").GetUInt64();
        Require(total > 0 && current.GetProperty("memoryUsed").GetUInt64() <= total);
    }
    Require(current.GetProperty("uptimeSeconds").GetDouble() > 0);
    foreach (var metric in new[] { "receiveBytesPerSecond", "sendBytesPerSecond" })
    {
        var value = current.GetProperty("network").GetProperty(metric);
        Require(value.ValueKind == JsonValueKind.Null || double.IsFinite(value.GetDouble()) && value.GetDouble() >= 0);
    }
    foreach (var disk in current.GetProperty("disks").EnumerateArray())
        Require(disk.GetProperty("total").GetInt64() > 0 && disk.GetProperty("free").GetInt64() >= 0 && disk.GetProperty("free").GetInt64() <= disk.GetProperty("total").GetInt64());
    var gpuPercent = current.GetProperty("gpuPercent");
    var gpuMemory = current.GetProperty("gpuMemoryUsed");
    var gpuTotal = current.GetProperty("gpuMemoryTotal");
    Require(gpuTotal.ValueKind == JsonValueKind.Null || gpuTotal.GetDouble() >= 0);
    if (gpuTotal.ValueKind != JsonValueKind.Null) checks.Count("dedicated GPU memory MiB", (int)(gpuTotal.GetDouble() / 1048576));
    Require(gpuPercent.ValueKind == JsonValueKind.Null || gpuPercent.GetDouble() is >= 0 and <= 100);
    Require(gpuMemory.ValueKind == JsonValueKind.Null || gpuMemory.GetDouble() >= 0);
    Require(current.GetProperty("gpuAvailable").GetBoolean() == (gpuPercent.ValueKind != JsonValueKind.Null || gpuMemory.ValueKind != JsonValueKind.Null));
    if (gpuPercent.ValueKind == JsonValueKind.Null) checks.Skip("GPU utilization unavailable");
    if (gpuMemory.ValueKind == JsonValueKind.Null) checks.Skip("GPU dedicated memory unavailable");
    checks.Count("fixed disks", current.GetProperty("disks").GetArrayLength());
});

await checks.Run("system output spectrum lifecycle", async () =>
{
    using var spectrum = new AudioSpectrumService();
    Require(!spectrum.Latest.Available);
    spectrum.SetEnabled(true);
    for(var attempt=0;attempt<40&&!spectrum.Latest.Available;attempt++)await Task.Delay(50);
    var frame=spectrum.Latest;
    if(!frame.Available)checks.Skip("WASAPI loopback unavailable on this endpoint");
    else {
        Require(frame.Bands.Length==32&&frame.Bands.All(value=>float.IsFinite(value)&&value is >=0 and <=1));
        Require(double.IsFinite(frame.Rms)&&frame.Rms>=0);
        frame.Bands[0]=99;Require(spectrum.Latest.Bands[0]<=1);
        Console.WriteLine($"  loopback RMS {frame.Rms:F5}, sequence {frame.Sequence}");
    }
    spectrum.SetEnabled(false);Require(!spectrum.Latest.Available&&spectrum.Latest.Rms==0);
    await Task.Delay(80);Require(!spectrum.Latest.Available);
    spectrum.SetEnabled(true);
    for(var attempt=0;attempt<40&&!spectrum.Latest.Available;attempt++)await Task.Delay(50);
    if(frame.Available)Require(spectrum.Latest.Available);
    spectrum.SetEnabled(false);spectrum.Dispose();spectrum.Dispose();
});

await checks.Run("Core Audio read", () =>
{
    using var service = new AudioService();
    var state = Json(service.Get());
    var devices = state.GetProperty("devices");
    var sessions = state.GetProperty("sessions");
    Require(devices.ValueKind == JsonValueKind.Array && sessions.ValueKind == JsonValueKind.Array);
    if (state.GetProperty("available").GetBoolean())
    {
        Require(state.GetProperty("volume").GetDouble() is >= 0 and <= 1);
        Require(state.GetProperty("muted").ValueKind is JsonValueKind.True or JsonValueKind.False);
    }
    else checks.Skip("default output unavailable; no state changed");
    foreach (var session in sessions.EnumerateArray())
    {
        Require(session.GetProperty("volume").GetDouble() is >= 0 and <= 1);
        Require(session.GetProperty("muted").ValueKind is JsonValueKind.True or JsonValueKind.False);
        Require(!string.IsNullOrEmpty(session.GetProperty("id").GetString()));
    }
    foreach (var flow in new[] { "input", "output" })
        Require(devices.EnumerateArray().Count(device => device.GetProperty("flow").GetString() == flow && device.GetProperty("default").GetBoolean()) <= 1);
    Require(state.GetProperty("microphoneMuted").ValueKind is JsonValueKind.Null or JsonValueKind.True or JsonValueKind.False);
    checks.Count("audio devices", devices.GetArrayLength());
    checks.Count("audio sessions", sessions.GetArrayLength());
    return Task.CompletedTask;
});

await checks.Run("GSMTC read and artwork bounds", async () =>
{
    using var service = new MediaService();
    var before = Json(await service.Get());
    if (!before.GetProperty("available").GetBoolean()) { checks.Skip("GSMTC unavailable"); return; }
    var previous = before.GetProperty("sessions").EnumerateArray().ToArray();
    checks.Count("media sessions", previous.Length);
    if (previous.Length == 0) { checks.Skip("no active media source; playback not started by tests"); return; }
    var after = Json(await service.Get());
    if (!after.GetProperty("available").GetBoolean()) { checks.Skip("media source changed during observation"); return; }
    var current = after.GetProperty("sessions").EnumerateArray().ToArray();
    Require(previous.Select(song => song.GetProperty("id").GetString()).Distinct().Count() == previous.Length);
    var stableTracks = 0;
    var covers = 0;
    foreach (var song in previous)
    {
        foreach (var capability in new[] { "canPlay", "canPause", "canPrevious", "canNext", "canSeek", "playing" })
            Require(song.GetProperty(capability).ValueKind is JsonValueKind.True or JsonValueKind.False);
        var duration = song.GetProperty("durationSeconds");
        var position = song.GetProperty("positionSeconds");
        if (duration.ValueKind != JsonValueKind.Null)
            Require(duration.GetDouble() > 0 && position.GetDouble() >= 0 && position.GetDouble() <= duration.GetDouble());
        var matches = current.Where(other => TrackKey(other) == TrackKey(song)).ToArray();
        if (matches.Length == 1 && previous.Count(other => TrackKey(other) == TrackKey(song)) == 1)
        {
            Require(matches[0].GetProperty("id").GetString() == song.GetProperty("id").GetString());
            stableTracks++;
        }
        if (!song.TryGetProperty("artwork", out var artwork) || artwork.ValueKind == JsonValueKind.Null) continue;
        var data = artwork.GetString()!;
        Require(data.Length <= 350000);
        Require(data.StartsWith("data:image/png;base64,", StringComparison.Ordinal) || data.StartsWith("data:image/jpeg;base64,", StringComparison.Ordinal) || data.StartsWith("data:image/webp;base64,", StringComparison.Ordinal));
        Require(Convert.FromBase64String(data[(data.IndexOf(',') + 1)..]).Length is > 0 and <= 262144);
        if (matches.Length == 1) Require(matches[0].GetProperty("artwork").GetString() == data);
        covers++;
    }
    if (stableTracks == 0) checks.Skip("no unchanged unambiguous track for stable-ID check");
    checks.Count("stable media identities", stableTracks);
    checks.Count("bounded covers", covers);
});

await checks.Run("Everything IPC isolation", async () =>
{
    var service = new EverythingService();
    await Reject<ArgumentException>(() => service.Query("bad\0query"));
    await Reject<ArgumentException>(() => service.Query(new string('a', 1025)));
    var files = Json(await service.Query("SideWidget.App.csproj"));
    if (!files.GetProperty("available").GetBoolean())
    {
        Require(files.GetProperty("results").GetArrayLength() == 0);
        Require(!string.IsNullOrWhiteSpace(files.GetProperty("message").GetString()));
        checks.Skip("Everything IPC unavailable; start normal user instance to test indexed results");
        return;
    }
    var hits = files.GetProperty("results").GetArrayLength();
    Require(hits <= 50);
    foreach (var file in files.GetProperty("results").EnumerateArray())
        Require(Path.IsPathFullyQualified(file.GetProperty("path").GetString()!) && !string.IsNullOrWhiteSpace(file.GetProperty("name").GetString()));
    var replies = await Task.WhenAll(service.Query("SideWidget.App.csproj"), service.Query("sidewidget-no-such-" + Guid.NewGuid().ToString("N")));
    var positive = Json(replies[0]);
    var negative = Json(replies[1]);
    if (!positive.GetProperty("available").GetBoolean() || !negative.GetProperty("available").GetBoolean())
    { checks.Skip("Everything exited or became unavailable during test"); return; }
    Require(negative.GetProperty("results").GetArrayLength() == 0);
    Require(positive.GetProperty("results").GetArrayLength() <= 50);
    if (hits == 0) checks.Skip("project not indexed; empty-result contract checked");
    else Require(positive.GetProperty("results").GetArrayLength() > 0);
    checks.Count("indexed project matches", hits);
});

var internet = new InternetService();
await checks.Run("Windows connectivity and target validation", async () =>
{
    var before = Json(internet.Get());
    Require(before.GetProperty("lastCheck").ValueKind == JsonValueKind.Null);
    Require(before.GetProperty("connectivity").GetString() is "internet" or "local" or "limited" or "none" or "unknown");
    foreach (var target in new[] { "https://yandex.ru", "user@yandex.ru", "yandex.ru:443", "yandex.ru/path", "yandex.ru?x=1", "yandex.ru#part", "bad host.ru", "bad\r\nhost.ru", "a..ru", "-host.ru", "" })
        await Reject<ArgumentException>(() => internet.Check("https", target));
    await Reject<ArgumentException>(() => internet.Check("ftp", "yandex.ru"));
    Require(Json(internet.Get()).GetProperty("lastCheck").ValueKind == JsonValueKind.Null);
});

if (args.Contains("--public-online", StringComparer.Ordinal))
{
    await checks.Run("public HTTPS and ICMP bounded checks", async () =>
    {
        var elapsed = Stopwatch.StartNew();
        var pending = internet.Check("https", "YANDEX.RU");
        await Reject<InvalidOperationException>(() => internet.Check("icmp", "cloudflare.com"));
        var https = Json(await pending).GetProperty("lastCheck");
        Require(elapsed.Elapsed.TotalSeconds < 9);
        Require(https.GetProperty("method").GetString() == "https" && https.GetProperty("target").GetString() == "yandex.ru");
        Require(DateTimeOffset.TryParse(https.GetProperty("checkedAt").GetString(), out _));
        if (!https.GetProperty("success").GetBoolean()) checks.Skip("public HTTPS response unavailable; bounded failure verified");
        if (elapsed.Elapsed.TotalSeconds < 5.1) await Task.Delay(TimeSpan.FromSeconds(5.1 - elapsed.Elapsed.TotalSeconds));
        elapsed.Restart();
        var icmp = Json(await internet.Check("icmp", "cloudflare.com")).GetProperty("lastCheck");
        Require(elapsed.Elapsed.TotalSeconds < 9);
        Require(icmp.GetProperty("method").GetString() == "icmp" && icmp.GetProperty("httpStatus").ValueKind == JsonValueKind.Null);
        if (!icmp.GetProperty("success").GetBoolean()) checks.Skip("public ICMP reply unavailable; bounded failure verified");
        checks.Count("successful public probes", (https.GetProperty("success").GetBoolean() ? 1 : 0) + (icmp.GetProperty("success").GetBoolean() ? 1 : 0));
    });
}
else checks.Skip("public network probes disabled; use --public-online explicitly");

return checks.Complete();

static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);
static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Native contract assertion failed."); }
static string TrackKey(JsonElement song) => string.Join('\0', song.GetProperty("source").GetString(), song.GetProperty("title").GetString(), song.GetProperty("artist").GetString(), song.GetProperty("durationSeconds").GetRawText());
static async Task Reject<TException>(Func<Task<object>> action) where TException : Exception
{
    try { await action(); }
    catch (TException) { return; }
    throw new InvalidOperationException("Invalid input or concurrent operation accepted.");
}

internal sealed class Checks
{
    private int passed, failed, skipped;
    internal async Task Run(string name, Func<Task> action)
    {
        try { await action(); passed++; Console.WriteLine("PASS: " + name); }
        catch (Exception error)
        {
            failed++;
            // Never print exception messages/stacks: provider text can contain private device names or paths.
            Console.Error.WriteLine("FAIL: " + name + " (" + error.GetType().Name + ")");
        }
    }
    internal void Skip(string reason) { skipped++; Console.WriteLine("SKIP: " + reason); }
    internal void Count(string name, int count) => Console.WriteLine("COUNT: " + name + "=" + count);
    internal int Complete() { Console.WriteLine($"RESULT: passed={passed} failed={failed} skipped={skipped}"); return failed == 0 ? 0 : 1; }
}

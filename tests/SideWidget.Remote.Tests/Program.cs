using SideWidget.Remote;
using SideWidget.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

if (args.Length == 2 && args[0] == "--browser-fixture") { await BrowserFixture.Run(Path.GetFullPath(args[1])); return; }
if (args is ["--inspect-network-setup"])
{
    using var audit = JsonDocument.Parse(File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SideWidget/phone-remote/network-setup-last.json")));
    var actualPlan = audit.RootElement.GetProperty("plan").Deserialize<SideWidget.Core.PhoneNetworkPlan>()!;
    var ready = SideWidget.App.PhoneNetworkSetup.IsReady(actualPlan);
    Console.WriteLine(JsonSerializer.Serialize(new { ready, actualPlan.Application, actualPlan.LocalAddress, actualPlan.RemoteSubnet, actualPlan.Ports, readOnly = true }));
    Environment.ExitCode = ready ? 0 : 1; return;
}

var checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
void Reject(Action action, string message) { try { action(); } catch (RemoteFault) { checks++; return; } throw new Exception(message); }
Check(SideWidget.App.PhoneNetworkSetup.TryRun(["--phone-network-setup"], out var invalidMode) && invalidMode == 64, "malformed privileged helper request is rejected before any mutation");
Check(!SideWidget.App.PhoneNetworkSetup.TryRun(["--verify-portable", "unused.json"], out _), "portable CLI remains outside network helper mode");
Check(!SideWidget.App.PhoneNetworkSetup.TryRun([], out _), "normal startup remains outside helper mode");
var actualCandidates = SideWidget.App.PhoneRemoteService.SetupAdapters();
Check(actualCandidates.All(a => RemoteNetwork.IsHomeAddress(a.Address) && !a.Name.Contains("VPN", StringComparison.OrdinalIgnoreCase) && !a.Name.Contains("TUN", StringComparison.OrdinalIgnoreCase)), "read-only real candidate enumeration excludes VPN");
if (actualCandidates.FirstOrDefault() is { } actual)
{
    var scope = SideWidget.Core.PhoneNetworkPlan.Create(actual.Id, Path.GetFullPath("SideWidget.App.exe"), actual.Address, actual.Mask, 47831, 47830);
    var inspect = typeof(SideWidget.App.PhoneNetworkSetup).GetMethod("Inspect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
    var status = JsonSerializer.SerializeToElement(inspect.Invoke(null, [scope]));
    Check(status.GetProperty("category").ValueKind == JsonValueKind.Number && status.GetProperty("category").GetInt32() is 0 or 1 or 2, "real NLM reads selected home category without privilege: " + status.GetProperty("categoryError").GetString());
}
var directory = Path.Combine(Path.GetTempPath(), "SideWidget.Remote.Tests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var staleDirectory = Path.Combine(directory, "stale-setup");
using (var service = new SideWidget.App.PhoneRemoteService(staleDirectory, () => Task.FromResult<object>(new { }), _ => Task.CompletedTask))
{
    var serviceType = service.GetType();
    serviceType.GetField("networkGeneration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(service, 2L);
    var configure = serviceType.GetMethod("ConfigureCore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
    var rejected = false;
    try { await (Task<object>)configure.Invoke(service, [false, null, 47831, 47830, false, 1L])!; }
    catch (SideWidget.Core.CommandFault) { rejected = true; }
    Check(rejected && !File.Exists(Path.Combine(staleDirectory, "settings.json")), "late setup cannot persist after newer disable generation; no startup/network mutation");
}
var now = DateTimeOffset.UtcNow;
var access = new RemoteAccess(Path.Combine(directory, "devices.json"), () => now);
var invite = access.CreateInvite();
var pairing = access.BeginPair(invite.Token, "Android");
Reject(() => access.BeginPair(invite.Token, "Second"), "invite must be single use");
Check(access.PollPair(pairing.Id, pairing.Secret).Status == "pending", "desktop approval required");
Reject(() => access.PollPair(pairing.Id, "wrong"), "poll secret required");
access.Decide(pairing.Id, true);
var approved = access.PollPair(pairing.Id, pairing.Secret);
Check(approved.Status == "approved" && approved.Token is not null, "approved receives key");
Check(access.Authorize(approved.Token!) is not null, "key authorizes");
Check(new RemoteAccess(Path.Combine(directory, "devices.json")).Authorize(approved.Token!) is not null, "trust survives restart");
Check(!File.ReadAllText(Path.Combine(directory, "devices.json")).Contains(approved.Token!), "raw key never persisted");
access.Revoke(approved.DeviceId!);
Check(access.Authorize(approved.Token!) is null, "revocation is immediate");
var expired = access.CreateInvite(); now = now.AddMinutes(6);
Reject(() => access.BeginPair(expired.Token, "Android"), "invite expires");
var denied = access.BeginPair(access.CreateInvite().Token, "Android"); access.Decide(denied.Id, false);
Check(access.PollPair(denied.Id, denied.Secret).Status == "denied", "denial visible");
for (var i = 0; i < 8; i++) { var p = access.BeginPair(access.CreateInvite().Token, "Phone " + i); access.Decide(p.Id, true); }
Reject(() => access.BeginPair(access.CreateInvite().Token, "Ninth"), "device count bounded");
Console.WriteLine($"PASS {checks} remote access checks");

Check(RemoteNetwork.IsHomeAddress(IPAddress.Parse("192.168.1.10")), "home IPv4 accepted");
Check(!RemoteNetwork.IsHomeAddress(IPAddress.Parse("8.8.8.8")), "public IPv4 excluded");
Check(RemoteNetwork.IsPhysicalCandidate(NetworkInterfaceType.Wireless80211, "Intel Wi-Fi"), "physical Wi-Fi candidate");
Check(!RemoteNetwork.IsPhysicalCandidate(NetworkInterfaceType.Ethernet, "WireGuard Tunnel"), "VPN Ethernet excluded");
Check(!RemoteNetwork.IsPhysicalCandidate(NetworkInterfaceType.Ethernet, "Hyper-V Virtual Ethernet"), "virtual excluded");
var adapter = new RemoteAdapter("test", "Loopback test", IPAddress.Loopback, IPAddress.Parse("255.0.0.0"));
Check(!RemoteNetwork.SameSubnet(IPAddress.Parse("192.168.1.4"), adapter), "foreign subnet excluded");
using var certificates = new RemoteCertificates(Path.Combine(directory, "certificates"));
using var leaf = certificates.Get(IPAddress.Loopback);
using var root = X509CertificateLoader.LoadCertificate(certificates.ExportRoot());
using var chain = new X509Chain();
chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
chain.ChainPolicy.CustomTrustStore.Add(root);
chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
Check(chain.Build(leaf), "TLS leaf chains to local root");
Check(!RemoteCertificates.NeedsRenewal(leaf, DateTimeOffset.UtcNow), "fresh TLS leaf remains valid");
Check(RemoteCertificates.NeedsRenewal(leaf, DateTimeOffset.UtcNow.AddDays(85)), "TLS renewal before expiry");
using var reloadedCertificates = new RemoteCertificates(Path.Combine(directory, "certificates"));
Check(reloadedCertificates.Fingerprint == certificates.Fingerprint, "CA survives restart");
Check(!File.ReadAllBytes(Path.Combine(directory, "certificates/root.pfx.dpapi")).AsSpan().SequenceEqual(root.RawData), "encrypted private root");

int Port() { var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port; }
var tlsPort = Port(); var httpPort = Port();
var config = new RemoteConfiguration(Port: tlsPort, SetupPort: httpPort);
var web = Path.Combine(directory, "Web"); Directory.CreateDirectory(web); File.WriteAllText(Path.Combine(web, "remote.html"), "remote shell");
var httpAccess = new RemoteAccess(Path.Combine(directory, "http-devices.json"));
var commands = new List<RemoteCommand>();
TaskCompletionSource? dispatchReached = null, releaseDispatch = null;
await using var server = new RemoteServer(httpAccess, adapter, config, leaf, root.RawData, web,
    () => Task.FromResult<object>(new { media = new { available = true, sessions = new[] { new { id = "live-session", artwork = "data:image/png;base64,dGVzdA==" } } }, audio = new { volume = .5, muted = false } }),
    async c => { if (dispatchReached is not null) { dispatchReached.TrySetResult(); await releaseDispatch!.Task; } if (c.StillAuthorized?.Invoke() != true) throw new RemoteFault("Access revoked before dispatch"); commands.Add(c); });
await server.StartAsync();
using var handler = new HttpClientHandler { UseProxy = false, ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert is not null && cert.Thumbprint == leaf.Thumbprint };
using var client = new HttpClient(handler) { BaseAddress = new Uri($"https://127.0.0.1:{tlsPort}"), Timeout = TimeSpan.FromSeconds(10) };
client.DefaultRequestHeaders.Add("Origin", client.BaseAddress.GetLeftPart(UriPartial.Authority));
Check((await client.GetAsync("/")).IsSuccessStatusCode, "HTTPS shell served");
Check((await client.GetAsync("/api/state")).StatusCode == HttpStatusCode.Unauthorized, "key required");
using (var evil = new HttpRequestMessage(HttpMethod.Post, "/api/pair/start") { Content = JsonContent.Create(new { invite = "x", name = "Android" }) })
{ evil.Headers.Add("Origin", "https://evil.test"); Check((await client.SendAsync(evil)).StatusCode == HttpStatusCode.Forbidden, "foreign Origin rejected"); }
using (var evil = new HttpRequestMessage(HttpMethod.Get, "/") )
{ evil.Headers.Host = "evil.test:" + tlsPort; Check((await client.SendAsync(evil)).StatusCode == HttpStatusCode.Forbidden, "foreign Host rejected"); }
using var plain = new HttpClient(new HttpClientHandler { UseProxy = false });
Check((await plain.GetAsync($"http://127.0.0.1:{httpPort}/api/state")).StatusCode == HttpStatusCode.Forbidden, "no control over plaintext HTTP");
Check((await plain.GetAsync($"http://127.0.0.1:{httpPort}/remote/root.crt")).IsSuccessStatusCode, "public CA download");
var newInvite = httpAccess.CreateInvite();
var startedResponse = await client.PostAsJsonAsync("/api/pair/start", new { invite = newInvite.Token, name = "Android test" });
Check(startedResponse.IsSuccessStatusCode, "HTTP pair starts");
var started = (await startedResponse.Content.ReadFromJsonAsync<PairStarted>())!;
httpAccess.Decide(started.Id, true);
var poll = await client.PostAsJsonAsync("/api/pair/poll", new { id = started.Id, secret = started.Secret });
var deviceKey = (await poll.Content.ReadFromJsonAsync<PairResult>())!;
client.DefaultRequestHeaders.Add("X-SideWidget-Key", deviceKey.Token);
Check((await client.GetAsync("/api/state")).IsSuccessStatusCode, "paired phone reads state");
var artworkState = (await client.GetFromJsonAsync<JsonElement>("/api/state"));
var artworkKey = artworkState.GetProperty("media").GetProperty("sessions")[0].GetProperty("artworkKey").GetString();
client.DefaultRequestHeaders.Add("X-SideWidget-Artwork", artworkKey);
var cachedState = await client.GetFromJsonAsync<JsonElement>("/api/state");
Check(!cachedState.GetProperty("media").GetProperty("sessions")[0].TryGetProperty("artwork", out _), "known artwork not retransmitted");
Check(cachedState.GetProperty("media").GetProperty("sessions")[0].GetProperty("artworkKey").GetString() == artworkKey, "artwork version stable");
var command = new { requestId = Guid.NewGuid().ToString(), command = "next", id = "live-session" };
Check((await client.PostAsJsonAsync("/api/command", command)).IsSuccessStatusCode && commands.Count == 1, "paired command executes");
Check((await client.PostAsJsonAsync("/api/command", command)).StatusCode == HttpStatusCode.BadRequest && commands.Count == 1, "duplicate command not executed");
Check((await client.PostAsJsonAsync("/api/command", new { requestId = Guid.NewGuid().ToString(), command = "volume", volume = 2 })).StatusCode == HttpStatusCode.BadRequest, "volume bounds");
Check((await client.PostAsJsonAsync("/api/command", new { requestId = Guid.NewGuid().ToString(), command = "mute", muted = true, microphoneMuted = true })).StatusCode == HttpStatusCode.BadRequest, "microphone excluded");
Check((await client.GetAsync("/index.html")).StatusCode == HttpStatusCode.NotFound, "desktop HTML excluded");
using var slowBody = new SlowJsonContent(JsonSerializer.Serialize(new { requestId = Guid.NewGuid().ToString(), command = "next", id = "live-session" }));
slowBody.Headers.ContentType = new("application/json");
var slowRequest = client.PostAsync("/api/command", slowBody);
await slowBody.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
httpAccess.Revoke(deviceKey.DeviceId!);
slowBody.Release.TrySetResult();
Check((await slowRequest).StatusCode == HttpStatusCode.Unauthorized && commands.Count == 1, "revocation during body read prevents command");
Check((await client.GetAsync("/api/state")).StatusCode == HttpStatusCode.Unauthorized, "revoked key rejected by HTTP");
var queuedPair = httpAccess.BeginPair(httpAccess.CreateInvite().Token, "Queued test"); httpAccess.Decide(queuedPair.Id, true);
var queuedKey = httpAccess.PollPair(queuedPair.Id, queuedPair.Secret);
client.DefaultRequestHeaders.Remove("X-SideWidget-Key"); client.DefaultRequestHeaders.Add("X-SideWidget-Key", queuedKey.Token);
dispatchReached = new(TaskCreationOptions.RunContinuationsAsynchronously); releaseDispatch = new(TaskCreationOptions.RunContinuationsAsynchronously);
var queuedRequest = client.PostAsJsonAsync("/api/command", new { requestId = Guid.NewGuid().ToString(), command = "next", id = "live-session" });
await dispatchReached.Task.WaitAsync(TimeSpan.FromSeconds(3)); httpAccess.Revoke(queuedKey.DeviceId!); releaseDispatch.TrySetResult();
Check((await queuedRequest).StatusCode == HttpStatusCode.BadRequest && commands.Count == 1, "revocation before dispatch prevents command");
await server.DisposeAsync();
await server.StartAsync();
Check((await client.GetAsync("/")).IsSuccessStatusCode, "listener restarts on same address");
Console.WriteLine($"PASS {checks} remote access/network/certificate/HTTPS checks; local synthetic media only");

sealed class SlowJsonContent(string json) : HttpContent
{
    readonly byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected override bool TryComputeLength(out long length) { length = bytes.Length; return true; }
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        await stream.WriteAsync(bytes.AsMemory(0, 1)); await stream.FlushAsync(); Started.TrySetResult();
        await Release.Task; await stream.WriteAsync(bytes.AsMemory(1));
    }
}

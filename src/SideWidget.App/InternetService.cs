using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Windows.Networking.Connectivity;

namespace SideWidget.App;

/// <summary>Windows connectivity plus explicit, bounded connectivity checks. Never polls remote hosts.</summary>
public sealed class InternetService
{
    private const int CooldownSeconds = 5;
    private static readonly string[] Presets = ["yandex.ru", "vk.com", "google.com", "cloudflare.com"];
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false, UseCookies = false, ConnectTimeout = TimeSpan.FromSeconds(4),
        MaxResponseHeadersLength = 16, MaxConnectionsPerServer = 1,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    }) { Timeout = Timeout.InfiniteTimeSpan };
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object stateGate = new();
    private long? lastStarted;
    private CheckResult? lastCheck;

    public object Get()
    {
        CheckResult? previous;
        lock (stateGate) previous = lastCheck;
        var connectivity = "unknown";
        var available = false;
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            connectivity = (profile?.GetNetworkConnectivityLevel() ?? NetworkConnectivityLevel.None) switch
            {
                NetworkConnectivityLevel.InternetAccess => "internet",
                NetworkConnectivityLevel.LocalAccess => "local",
                NetworkConnectivityLevel.ConstrainedInternetAccess => "limited",
                _ => "none"
            };
            available = true;
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or UnauthorizedAccessException) { }
        return new { available, connectivity, source = "Windows", target = previous?.target ?? "cloudflare.com",
            presets = Presets.ToArray(), lastCheck = previous, cooldownSeconds = CooldownSeconds };
    }

    public async Task<object> Check(string method, string target)
    {
        if (method is not ("icmp" or "https")) throw new ArgumentException("Выберите ICMP или HTTPS.", nameof(method));
        var host = NormalizeHost(target);
        if (!await gate.WaitAsync(0)) throw new InvalidOperationException("Проверка уже выполняется.");
        try
        {
            var now = Stopwatch.GetTimestamp();
            if (lastStarted is { } previous && (now - previous) / (double)Stopwatch.Frequency < CooldownSeconds)
                throw new InvalidOperationException("Между проверками должно пройти 5 секунд.");
            lastStarted = now;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            CheckResult result;
            try { result = method == "icmp" ? await Ping(host, timeout.Token) : await Https(host, timeout.Token); }
            catch (OperationCanceledException)
            {
                result = new(method, host, DateTimeOffset.UtcNow, false, null, null, "timeout",
                    "Ответ не получен за 6 секунд. Это не доказывает отсутствие интернета.");
            }
            catch (Exception e) when (e is PingException or SocketException or HttpRequestException or InvalidOperationException or UnauthorizedAccessException)
            {
                result = new(method, host, DateTimeOffset.UtcNow, false, null, null, "unavailable",
                    method == "icmp" ? "ICMP-ответ недоступен: цель, сеть или политика могут блокировать ping." : "HTTPS-проверка не удалась. Проверьте адрес, сеть и сертификат сайта.");
            }
            lock (stateGate) lastCheck = result;
            return Get();
        }
        finally { gate.Release(); }
    }

    private static async Task<CheckResult> Ping(string host, CancellationToken cancellation)
    {
        var addresses = await Dns.GetHostAddressesAsync(host, cancellation);
        var address = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
            ?? addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6)
            ?? throw new SocketException((int)SocketError.HostNotFound);
        using var ping = new System.Net.NetworkInformation.Ping();
        var reply = await ping.SendPingAsync(address, TimeSpan.FromSeconds(4), Array.Empty<byte>(), new PingOptions(64, false), cancellation);
        var success = reply.Status == IPStatus.Success;
        return new("icmp", host, DateTimeOffset.UtcNow, success, success ? reply.RoundtripTime : null, null,
            reply.Status.ToString(), success ? "Время ICMP echo round-trip; DNS не включён." : "ICMP-ответ не получен. Это не означает, что весь интернет недоступен.");
    }

    private static async Task<CheckResult> Https(string host, CancellationToken cancellation)
    {
        var uri = new UriBuilder(Uri.UriSchemeHttps, host) { Path = "/" }.Uri;
        using var request = new HttpRequestMessage(HttpMethod.Head, uri);
        request.Headers.UserAgent.ParseAdd("SideWidget/0.1");
        var started = Stopwatch.GetTimestamp();
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var status = (int)response.StatusCode;
        return new("https", host, DateTimeOffset.UtcNow, status is >= 200 and < 400, Math.Round(elapsed, 1), status,
            "HTTP " + status, "Время HTTPS-запроса до заголовков ответа, включая DNS/соединение при необходимости. Это не ICMP ping.");
    }

    private static string NormalizeHost(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 255) throw InvalidHost();
        var host = value.Trim();
        if (host.Any(char.IsWhiteSpace) || host.IndexOfAny(['/', '\\', '@', '?', '#', '%']) >= 0) throw InvalidHost();
        if (host.StartsWith('[') && host.EndsWith(']')) host = host[1..^1];
        if (IPAddress.TryParse(host, out var address)) return address.ToString();
        if (host.Contains(':') || host.Contains('[') || host.Contains(']')) throw InvalidHost();
        try { host = new IdnMapping().GetAscii(host.TrimEnd('.')).ToLowerInvariant(); }
        catch (ArgumentException) { throw InvalidHost(); }
        if (host.Length is 0 or > 253 || !host.Contains('.')) throw InvalidHost();
        foreach (var label in host.Split('.'))
            if (label.Length is 0 or > 63 || label[0] == '-' || label[^1] == '-' ||
                label.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))) throw InvalidHost();
        return host;
    }

    private static ArgumentException InvalidHost() => new("Введите домен или IP без протокола, порта, пути и учётных данных.", "target");

    private sealed record CheckResult(string method, string target, DateTimeOffset checkedAt, bool success,
        double? latencyMs, int? httpStatus, string status, string message);
}

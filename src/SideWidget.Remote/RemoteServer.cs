using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SideWidget.Remote;

public sealed class RemoteServer(RemoteAccess access, RemoteAdapter adapter, RemoteConfiguration config,
    X509Certificate2 certificate, byte[] rootCertificate, string webDirectory,
    Func<Task<object>> snapshot, Func<RemoteCommand, Task> command) : IAsyncDisposable
{
    WebApplication? app;
    readonly SemaphoreSlim parallel = new(8, 8);
    readonly object rateGate = new();
    readonly Dictionary<string, (DateTimeOffset Start, int Count)> rates = new();
    readonly object commandGate = new();
    readonly Dictionary<string, DateTimeOffset> requests = new();
    public bool Running => app is not null;

    public async Task StartAsync(CancellationToken cancellation = default)
    {
        if (app is not null) return;
        config.Validate();
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [], ContentRootPath = webDirectory });
        builder.Logging.ClearProviders(); // No request, token, track or device-name logging.
        builder.WebHost.UseKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = 8192;
            options.Limits.MaxConcurrentConnections = 32;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
            options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
            options.Listen(adapter.Address, config.Port, endpoint => endpoint.UseHttps(certificate));
            options.Listen(adapter.Address, config.SetupPort);
        });
        var host = builder.Build();
        host.Run(Handle);
        try { await host.StartAsync(cancellation); app = host; }
        catch { await host.DisposeAsync(); throw; }
    }

    async Task Handle(HttpContext context)
    {
        var request = context.Request; var response = context.Response;
        response.Headers.CacheControl = "no-store";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers["X-Frame-Options"] = "DENY";
        response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        response.Headers.ContentSecurityPolicy = "default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; font-src 'self'; img-src 'self' data:; connect-src 'self'; manifest-src 'self'; worker-src 'self'; base-uri 'none'; frame-ancestors 'none'; form-action 'none'";
        var port = request.IsHttps ? config.Port : config.SetupPort;
        if (!string.Equals(request.Host.Value, $"{adapter.Address}:{port}", StringComparison.Ordinal) ||
            context.Connection.RemoteIpAddress is not { } ip || !RemoteNetwork.SameSubnet(ip, adapter))
        { response.StatusCode = 403; return; }
        if (!TakeRate(ip.ToString()) || !await parallel.WaitAsync(0)) { response.StatusCode = 429; return; }
        try
        {
            var path = request.Path.Value ?? "/";
            if (request.Method == "GET" && path == "/remote/root.crt")
            {
                response.ContentType = "application/x-x509-ca-cert";
                response.Headers.ContentDisposition = "attachment; filename=SideWidget-Root.crt";
                await response.Body.WriteAsync(rootCertificate, context.RequestAborted); return;
            }
            if (request.Method == "GET" && path == "/remote/config")
            {
                await response.WriteAsJsonAsync(new { secureUrl = $"https://{adapter.Address}:{config.Port}/", fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(rootCertificate)) }, context.RequestAborted); return;
            }
            if (path.StartsWith("/api/", StringComparison.Ordinal))
            {
                if (!request.IsHttps) { response.StatusCode = 403; return; }
                var origin = request.Headers.Origin.ToString();
                if ((request.Method != "GET" || origin.Length > 0) && origin != $"https://{adapter.Address}:{config.Port}")
                { response.StatusCode = 403; return; }
                if (request.Method == "POST" && !request.HasJsonContentType()) { response.StatusCode = 415; return; }
                if (path == "/api/pair/start" && request.Method == "POST")
                {
                    var data = await Body(context, "invite", "name");
                    await response.WriteAsJsonAsync(access.BeginPair(Text(data, "invite", 64), Text(data, "name", 60)), context.RequestAborted); return;
                }
                if (path == "/api/pair/poll" && request.Method == "POST")
                {
                    var data = await Body(context, "id", "secret");
                    await response.WriteAsJsonAsync(access.PollPair(Text(data, "id", 32), Text(data, "secret", 64)), context.RequestAborted); return;
                }
                var token = request.Headers["X-SideWidget-Key"].ToString();
                var device = access.Authorize(token);
                if (device is null) { response.StatusCode = 401; return; }
                if (path == "/api/state" && request.Method == "GET")
                { await response.WriteAsJsonAsync(WithoutKnownArtwork(await snapshot(), request.Headers["X-SideWidget-Artwork"].ToString()), context.RequestAborted); return; }
                if (path == "/api/command" && request.Method == "POST")
                {
                    var data = await Body(context, "requestId", "command", "id", "positionSeconds", "volume", "muted");
                    if (access.Authorize(token) != device) { response.StatusCode = 401; return; }
                    var parsed = ParseCommand(data) with { StillAuthorized = () => Running && access.Authorize(token) == device };
                    lock (commandGate)
                    {
                        foreach (var expiredKey in requests.Where(p => p.Value < DateTimeOffset.UtcNow.AddMinutes(-5)).Select(p => p.Key).ToArray()) requests.Remove(expiredKey);
                        var key = device + ":" + parsed.RequestId;
                        if (requests.ContainsKey(key)) throw new RemoteFault("Команда уже отправлялась. Обновите состояние.");
                        if (requests.Count >= 2048) { response.StatusCode = 429; return; }
                        requests.Add(key, DateTimeOffset.UtcNow);
                    }
                    await command(parsed);
                    await response.WriteAsJsonAsync(new { accepted = true }, context.RequestAborted); return;
                }
                response.StatusCode = 404; return;
            }
            if (request.Method != "GET") { response.StatusCode = 405; return; }
            // An explicit public asset set. Never serve the desktop entry point, profile, paths or arbitrary files.
            string? file = path switch
            {
                "/" or "/remote.html" => "remote.html",
                "/remote/manifest.webmanifest" => request.IsHttps ? "remote/manifest.webmanifest" : null,
                "/remote/sw.js" => request.IsHttps ? "remote/sw.js" : null,
                "/remote/icon-192.png" => "remote/icon-192.png",
                "/remote/icon-512.png" => "remote/icon-512.png",
                "/remote/icon-maskable-512.png" => "remote/icon-maskable-512.png",
                "/fonts/MedeoSansVariable.woff2" => "fonts/MedeoSansVariable.woff2",
                _ => null
            };
            if (file is null && path.StartsWith("/remote-assets/", StringComparison.Ordinal) &&
                path.Length < 200 && !path.Contains("..") && path.Count(c => c == '/') == 2 &&
                path.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_' or '.')) file = path.TrimStart('/');
            if (file is null || !File.Exists(Path.Combine(webDirectory, file))) { response.StatusCode = 404; return; }
            response.ContentType = Path.GetExtension(file) switch
            {
                ".html" => "text/html; charset=utf-8", ".js" => "text/javascript; charset=utf-8", ".css" => "text/css; charset=utf-8",
                ".webmanifest" => "application/manifest+json", ".png" => "image/png", ".woff2" => "font/woff2", _ => "application/octet-stream"
            };
            if (file == "remote/sw.js") response.Headers["Service-Worker-Allowed"] = "/";
            await response.SendFileAsync(Path.Combine(webDirectory, file), context.RequestAborted);
        }
        catch (BadHttpRequestException error) { if (!response.HasStarted) response.StatusCode = error.StatusCode; }
        catch (RemoteFault error) { if (!response.HasStarted) { response.StatusCode = 400; await response.WriteAsJsonAsync(new { message = error.Message }); } }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or ArgumentException)
        { if (!response.HasStarted) { response.StatusCode = 400; await response.WriteAsJsonAsync(new { message = "Команда недоступна или некорректна. Обновите состояние плеера." }); } }
        catch (OperationCanceledException) { if (!response.HasStarted) response.StatusCode = 408; }
        catch (Exception) { if (!response.HasStarted) { response.StatusCode = 503; await response.WriteAsJsonAsync(new { message = "ПК пока не может выполнить действие. Обновите состояние и повторите вручную." }); } }
        finally { parallel.Release(); }
    }

    static JsonNode? WithoutKnownArtwork(object snapshot, string knownHeader)
    {
        var known = knownHeader.Length <= 1100 ? knownHeader.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal) : [];
        var result = JsonSerializer.SerializeToNode(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (result?["media"]?["sessions"] is JsonArray sessions)
            foreach (var session in sessions.OfType<JsonObject>())
                if (session["artwork"]?.GetValue<string>() is { Length: > 0 } artwork)
                {
                    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(artwork)));
                    session["artworkKey"] = hash;
                    if (known.Contains(hash)) session.Remove("artwork");
                }
        return result;
    }

    bool TakeRate(string address)
    {
        lock (rateGate)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var key in rates.Where(p => p.Value.Start < now.AddSeconds(-10)).Select(p => p.Key).ToArray()) rates.Remove(key);
            if (!rates.TryGetValue(address, out var rate)) { if (rates.Count >= 256) return false; rate = (now, 0); }
            rates[address] = (rate.Start, rate.Count + 1); return rate.Count < 80;
        }
    }
    static async Task<JsonElement> Body(HttpContext context, params string[] allowed)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        using var data = await JsonDocument.ParseAsync(context.Request.Body, new() { MaxDepth = 4 }, deadline.Token);
        if (data.RootElement.ValueKind != JsonValueKind.Object) throw new RemoteFault("Ожидается объект команды.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in data.RootElement.EnumerateObject()) if (!keys.Add(p.Name) || !allowed.Contains(p.Name)) throw new RemoteFault("Неизвестное или повторное поле.");
        return data.RootElement.Clone();
    }
    static string Text(JsonElement value, string name, int max) => value.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String && field.GetString() is { } text && text.Length is > 0 && text.Length <= max
        ? text : throw new RemoteFault("Проверьте поле " + name + ".");

    public static RemoteCommand ParseCommand(JsonElement data)
    {
        var action = Text(data, "command", 16); var requestId = Text(data, "requestId", 36);
        if (!Guid.TryParse(requestId, out _)) throw new RemoteFault("Некорректный ID команды.");
        var fields = action switch
        {
            "play" or "pause" or "previous" or "next" => new[] { "requestId", "command", "id" },
            "seek" => ["requestId", "command", "id", "positionSeconds"],
            "volume" => ["requestId", "command", "volume"],
            "mute" => ["requestId", "command", "muted"],
            _ => throw new RemoteFault("Действие не разрешено.")
        };
        if (data.EnumerateObject().Count() != fields.Length || data.EnumerateObject().Any(p => !fields.Contains(p.Name))) throw new RemoteFault("Поля не соответствуют команде.");
        var id = fields.Contains("id") ? Text(data, "id", 256) : null;
        double? Number(string name, double max)
        {
            if (!fields.Contains(name)) return null;
            var number = data.GetProperty(name).GetDouble();
            if (!double.IsFinite(number) || number < 0 || number > max) throw new RemoteFault("Значение вне диапазона.");
            return number;
        }
        return new(requestId, action, id, Number("positionSeconds", TimeSpan.MaxValue.TotalSeconds / 2), Number("volume", 1),
            fields.Contains("muted") ? data.GetProperty("muted").GetBoolean() : null);
    }

    public async ValueTask DisposeAsync()
    {
        var host = app; app = null;
        if (host is null) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await host.StopAsync(timeout.Token); } finally { await host.DisposeAsync(); }
    }
}

using System.Text.Json;
using SideWidget.Core;

namespace SideWidget.Infrastructure;

public sealed record GitHubCredential(string AccessToken, DateTimeOffset? ExpiresAt, string? RefreshToken);

public sealed class GitHubOAuth(HttpClient? transport = null)
{
    const string ClientId = "Ov23lilzBRn9UnoPcw4w";
    const string DeviceGrant = "urn:ietf:params:oauth:grant-type:device_code";
    public const string VerificationPage = "https://github.com/login/device";
    static readonly Uri DeviceEndpoint = new("https://github.com/login/device/code");
    static readonly Uri TokenEndpoint = new("https://github.com/login/oauth/access_token");
    static readonly HttpClient Client = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
    {
        Timeout = TimeSpan.FromSeconds(15),
        MaxResponseContentBufferSize = 32768,
    };

    public async Task<GitHubCredential> AuthorizeAsync(Action<string> showCode, CancellationToken cancellation)
    {
        var challenge = await PostAsync(DeviceEndpoint, new() { ["client_id"] = ClientId }, cancellation).ConfigureAwait(false);
        var deviceCode = ReadString(challenge, "device_code");
        var userCode = ReadString(challenge, "user_code");
        if (!ValidToken(deviceCode) || userCode is not { Length: 9 } || userCode[4] != '-' ||
            !userCode.Where((_, index) => index != 4).All(char.IsAsciiLetterOrDigit) ||
            ReadString(challenge, "verification_uri") != VerificationPage ||
            ReadSeconds(challenge, "expires_in") is not (>= 1 and <= 900) ||
            ReadSeconds(challenge, "interval") is not (>= 1 and <= 900))
            throw Error();

        using var expiry = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        expiry.CancelAfter(TimeSpan.FromSeconds(ReadSeconds(challenge, "expires_in")));
        var interval = ReadSeconds(challenge, "interval");
        showCode(userCode);
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), expiry.Token).ConfigureAwait(false);
                var reply = await PostAsync(TokenEndpoint, new()
                {
                    ["client_id"] = ClientId,
                    ["device_code"] = deviceCode!,
                    ["grant_type"] = DeviceGrant,
                }, expiry.Token).ConfigureAwait(false);
                switch (ReadString(reply, "error"))
                {
                    case null: return ReadCredential(reply);
                    case "authorization_pending": continue;
                    case "slow_down":
                        interval = Math.Min(900, Math.Max(interval + 5, ReadSeconds(reply, "interval")));
                        continue;
                    case "access_denied": throw new CommandFault("github_denied", "Вход в GitHub отменён.");
                    case "expired_token" or "token_expired": throw Expired();
                    default: throw Error();
                }
            }
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested && expiry.IsCancellationRequested)
        {
            throw Expired();
        }
    }

    public async Task<GitHubCredential?> RefreshAsync(string refreshToken, CancellationToken cancellation)
    {
        var reply = await PostAsync(TokenEndpoint, new()
        {
            ["client_id"] = ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        }, cancellation).ConfigureAwait(false);
        return ReadString(reply, "error") switch
        {
            null => ReadCredential(reply),
            "bad_refresh_token" or "invalid_grant" => null,
            _ => throw Error(),
        };
    }

    async Task<JsonElement> PostAsync(Uri uri, Dictionary<string, string> fields, CancellationToken cancellation)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new FormUrlEncodedContent(fields) };
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.UserAgent.ParseAdd("SideWidget");
            using var response = await (transport ?? Client).SendAsync(request, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellation).ConfigureAwait(false));
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw Error();
            return document.RootElement.Clone();
        }
        catch (Exception error) when (error is HttpRequestException or IOException or JsonException ||
                                      error is OperationCanceledException && !cancellation.IsCancellationRequested)
        {
            throw Error();
        }
    }

    static GitHubCredential ReadCredential(JsonElement reply)
    {
        var access = ReadString(reply, "access_token");
        var refresh = ReadString(reply, "refresh_token");
        var seconds = ReadSeconds(reply, "expires_in");
        if (!ValidToken(access) || (refresh is not null && !ValidToken(refresh)) ||
            !string.Equals(ReadString(reply, "token_type"), "bearer", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(ReadString(reply, "scope")))
            throw Error();

        return new(access!, seconds > 0 ? DateTimeOffset.UtcNow.AddSeconds(seconds) : null, refresh);
    }

    static bool ValidToken(string? value) =>
        value is { Length: > 0 and <= 512 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
    static string? ReadString(JsonElement json, string field) =>
        json.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    static int ReadSeconds(JsonElement json, string field) =>
        json.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var seconds)
            ? seconds : 0;
    static CommandFault Error() => new("github_login", "Не удалось подключиться к GitHub. Попробуйте ещё раз.");
    static CommandFault Expired() => new("github_expired", "Код GitHub истёк. Начните вход заново.");
}

using System.Security.Cryptography;
using System.Text.Json;
using SideWidget.Core;
using SideWidget.Infrastructure;

namespace SideWidget.App;

sealed class GitHubConnection(SecretVault vault)
{
    const string SecretId = "e507987b9df1484abf5ddacbaed5ec9a";
    readonly GitHubOAuth oauth = new();
    readonly SemaphoreSlim gate = new(1, 1);
    int connecting;
    int generation;

    public bool Connected => Read() is not null;

    public async Task ConnectAsync(Action<string> showCode, CancellationToken cancellation)
    {
        var attempt = Volatile.Read(ref generation);
        if (Interlocked.Exchange(ref connecting, 1) != 0)
            throw new CommandFault("github_busy", "Вход в GitHub уже открыт.");

        try
        {
            var credential = await oauth.AuthorizeAsync(showCode, cancellation).ConfigureAwait(false);
            await gate.WaitAsync(cancellation).ConfigureAwait(false);
            try
            {
                cancellation.ThrowIfCancellationRequested();
                if (attempt != Volatile.Read(ref generation))
                    throw new CommandFault("github_denied", "Вход в GitHub отменён.");
                Save(credential);
            }
            finally { gate.Release(); }
        }
        finally { Volatile.Write(ref connecting, 0); }
    }

    public async Task<(string? Token, bool Reauthenticate)> AccessAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var current = Read();
            if (current is null) return (null, false);
            if (current.ExpiresAt is null || current.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
                return (current.AccessToken, false);

            var renewed = current.RefreshToken is { } refresh
                ? await oauth.RefreshAsync(refresh, CancellationToken.None).ConfigureAwait(false)
                : null;
            if (renewed is null)
            {
                vault.Delete(SecretId);
                return (null, true);
            }
            Save(renewed);
            return (renewed.AccessToken, false);
        }
        finally { gate.Release(); }
    }

    public async Task DisconnectAsync(string? rejectedToken = null)
    {
        if (rejectedToken is null) Interlocked.Increment(ref generation);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (rejectedToken is null || Read()?.AccessToken == rejectedToken) vault.Delete(SecretId);
        }
        finally { gate.Release(); }
    }

    GitHubCredential? Read()
    {
        try { return JsonSerializer.Deserialize<GitHubCredential>(vault.Read(SecretId), Protocol.Json); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
        {
            return null;
        }
    }

    void Save(GitHubCredential credential) => vault.Replace(SecretId, JsonSerializer.Serialize(credential, Protocol.Json));
}

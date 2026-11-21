using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SideWidget.Core;

namespace SideWidget.Infrastructure;

public sealed partial class OnlineServices
{
    const string SonioxSecret = "personal.soniox.secret";
    public const int SonioxRecordingLimit = 512 * 1024;
    static readonly HashSet<string> SonioxLanguages = ["ru", "en", "de", "fr", "es", "zh", "ja"];

    public void ConfigureSoniox(string key)
    {
        key = key.Trim();
        if (key.Length is 0 or > 200 || key.Any(c => c <= ' ' || c > '~'))
            throw new CommandFault("key", "Введите действующий ключ Soniox API");
        store.Save(SonioxSecret, vault.Save(key));
    }

    public void RequireSonioxKey()
    {
        if (store.Read<string?>(SonioxSecret, null) is null)
            throw new CommandFault("key", "Настройте ключ Soniox API");
    }

    public Task<object> TranslateVoice(string encodedAudio, string source, string? target, CancellationToken requestToken)
    {
        if ((target is not null && !SonioxLanguages.Contains(target)) || (source != "auto" && !SonioxLanguages.Contains(source)))
            throw new CommandFault("language", "Выберите язык");
        byte[] audio;
        try { audio = Convert.FromBase64String(encodedAudio); }
        catch (FormatException) { throw new CommandFault("soniox_audio", "Не удалось прочитать запись. Запишите фразу заново."); }
        if (audio.Length < 4 || audio.Length > SonioxRecordingLimit ||
            !audio.AsSpan(0, 4).SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }))
            throw new CommandFault("soniox_audio", "Не удалось прочитать запись. Запишите фразу заново.");

        return WithSoniox(async (key, cancellation) =>
        {
            using var socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("Authorization", "Bearer " + key);
            await socket.ConnectAsync(new("wss://stt-rt.soniox.com/transcribe-websocket"), cancellation);
            var settings = new Dictionary<string, object>
            {
                ["model"] = "stt-rt-v6",
                ["audio_format"] = "webm",
                ["enable_language_identification"] = true,
                ["language_hints"] = source == "auto" ? Array.Empty<string>() : new[] { source },
                ["language_hints_strict"] = source != "auto"
            };
            if (target is not null) settings["translation"] = new { type = "one_way", target_language = target };
            var configuration = JsonSerializer.SerializeToUtf8Bytes(settings);
            await socket.SendAsync(configuration.AsMemory(), WebSocketMessageType.Text, true, cancellation);
            var receive = ReadSoniox(socket, target, cancellation);
            try
            {
                for (var offset = 0; offset < audio.Length; offset += 8192)
                {
                    if (receive.IsCompleted) break;
                    await socket.SendAsync(audio.AsMemory(offset, Math.Min(8192, audio.Length - offset)),
                        WebSocketMessageType.Binary, true, cancellation);
                }
                if (!receive.IsCompleted)
                    await socket.SendAsync(ReadOnlyMemory<byte>.Empty, WebSocketMessageType.Text, true, cancellation);
                return await receive;
            }
            catch
            {
                socket.Abort();
                if (receive.IsCompletedSuccessfully) return await receive;
                try { await receive; }
                catch (CommandFault fault) when (fault.Code is "key" or "quota") { throw; }
                catch (Exception) { /* Preserve the send error. */ }
                throw;
            }
        }, requestToken);
    }

    static async Task<object> ReadSoniox(ClientWebSocket socket, string? target, CancellationToken cancellation)
    {
        var transcript = new StringBuilder();
        var translation = new StringBuilder();
        var originalInTarget = new StringBuilder();
        var buffer = new byte[8192];
        while (true)
        {
            using var message = new MemoryStream();
            ValueWebSocketReceiveResult frame;
            do
            {
                frame = await socket.ReceiveAsync(buffer.AsMemory(), cancellation);
                if (frame.MessageType != WebSocketMessageType.Text || message.Length + frame.Count > 256 * 1024)
                    throw new JsonException();
                message.Write(buffer, 0, frame.Count);
            } while (!frame.EndOfMessage);

            using var response = JsonDocument.Parse(message.ToArray());
            var root = response.RootElement;
            if (root.TryGetProperty("error_code", out var error))
                throw SonioxError(error.TryGetInt32(out var status) ? status : 0);
            foreach (var token in root.GetProperty("tokens").EnumerateArray())
            {
                if (!token.GetProperty("is_final").GetBoolean()) continue;
                var text = token.GetProperty("text").GetString();
                if (text is "<end>" or "<fin>") continue;
                if (token.TryGetProperty("translation_status", out var kind) && kind.GetString() == "translation")
                    translation.Append(text);
                else
                {
                    transcript.Append(text);
                    if (token.TryGetProperty("language", out var language) && language.GetString() == target)
                        originalInTarget.Append(text);
                }
                if (transcript.Length + translation.Length > 20_000) throw new JsonException();
            }
            if (!root.TryGetProperty("finished", out var finished) || !finished.GetBoolean()) continue;

            var spoken = transcript.ToString().Trim();
            var translated = translation.ToString().Trim();
            if (spoken.Length == 0)
                throw new CommandFault("soniox_speech", "Речь не распознана. Запишите фразу заново.");
            if (target is null) return new { transcript = spoken };
            if (translated.Length == 0 && spoken == originalInTarget.ToString().Trim()) translated = spoken;
            if (translated.Length == 0)
                throw new CommandFault("soniox_translation", "Soniox не вернул перевод. Проверьте выбранные языки.");
            return new { transcript = spoken, text = translated };
        }
    }

    public Task<object> SpeakTranslation(string text, string target, CancellationToken requestToken)
    {
        if (!SonioxLanguages.Contains(target)) throw new CommandFault("language", "Выберите язык");
        if (string.IsNullOrWhiteSpace(text)) throw new CommandFault("translation", "Сначала переведите фразу.");

        return WithSoniox(async (key, cancellation) =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://tts-rt.soniox.com/tts");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Content = JsonContent.Create(new
            {
                model = "tts-rt-v2", language = target, voice = "Adrian", audio_format = "mp3", bitrate = 32000, text
            });
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
            if (!response.IsSuccessStatusCode) throw SonioxError((int)response.StatusCode);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellation)) != 0)
            {
                if (output.Length + read > 1024 * 1024) throw new JsonException();
                output.Write(buffer, 0, read);
            }
            if (output.Length == 0) throw new JsonException();
            return new { audio = Convert.ToBase64String(output.ToArray()) };
        }, requestToken);
    }

    async Task<object> WithSoniox(Func<string, CancellationToken, Task<object>> run, CancellationToken requestToken)
    {
        var reference = store.Read<string?>(SonioxSecret, null)
            ?? throw new CommandFault("key", "Настройте ключ Soniox API");
        var key = vault.Read(reference);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            var result = await run(key, timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            return result;
        }
        catch (Exception error) when (requestToken.IsCancellationRequested && error is OperationCanceledException or WebSocketException or IOException)
        {
            throw new CommandFault("cancelled", "Запрос Soniox отменён.");
        }
        catch (Exception error) when (error is OperationCanceledException or WebSocketException or HttpRequestException or IOException)
        {
            if (error is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } http)
                throw SonioxError((int)http.StatusCode!.Value);
            throw new CommandFault("soniox_network", "Soniox недоступен. Проверьте соединение и ключ API.");
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new CommandFault("soniox_response", "Soniox вернул неожиданный ответ. Повторите запрос.");
        }
    }

    static CommandFault SonioxError(int status) => status switch
    {
        401 or 403 => new("key", "Soniox отклонил запрос. Проверьте ключ API и его разрешения."),
        402 or 429 => new("quota", "Проверьте баланс и лимиты в Soniox Console."),
        _ => new("soniox_response", "Soniox не обработал запрос. Повторите позже.")
    };
}

using System.Net;
using System.Text;
using System.Text.Json;
using SideWidget.Core;
using SideWidget.Infrastructure;

public static class OnlineChecks
{
    const string SyntheticKey = "sidewidget-synthetic-test-key-not-a-provider-credential";
    const string Location = """{"results":[{"name":"Москва","country":"Россия","latitude":55.75,"longitude":37.625}]}""";
    const string Forecast = """{"current":{"temperature_2m":7.5,"apparent_temperature":4.25,"precipitation":0.6,"weather_code":61,"wind_speed_10m":12.5,"time":"2026-11-05T12:00"},"daily":{"time":["2026-11-05","2026-11-06"],"temperature_2m_min":[2.5,1.0],"temperature_2m_max":[8.0,6.5],"weather_code":[61,3]}}""";

    public static async Task<int> Run()
    {
        var checks = new Checks();
        var failures = new List<Exception>();
        foreach (var (name, test) in new (string, Func<Checks, Task>)[] {
            ("free translation without credentials", FreeTranslation),
            ("free translation validation and upstream failures", FreeTranslationFailures),
            ("translation request and protected configuration", TranslationRequest),
            ("translation auto language and paid host", TranslationAutoLanguage),
            ("translation cancellation and late result", TranslationCancellation),
            ("translation provider failures and response limit", TranslationFailures),
            ("Soniox credential isolation and speech synthesis", SonioxSpeech),
            ("weather encoding and persisted offline cache", WeatherCache),
            ("weather malformed response retains cached forecast", WeatherSchemaFailure),
        })
        {
            try { await test(checks); }
            catch (Exception error) { failures.Add(new Exception(name, error)); }
        }
        if (failures.Count != 0)
        {
            Console.WriteLine($"Online services: {checks.Count} checks passed, {failures.Count} scenarios failed; synthetic HTTP only.");
            throw new AggregateException("Online service checks failed", failures);
        }
        Console.WriteLine($"PASS {checks.Count} online service checks; synthetic HTTP only.");
        return checks.Count;
    }

    static async Task FreeTranslation(Checks checks)
    {
        using var fixture = new Fixture();
        Request? captured = null;
        using var handler = new Handler(async (request, token) => {
            captured = await Request.Capture(request, token);
            return Json("""{"responseStatus":200,"quotaFinished":false,"responseData":{"translatedText":"Привет &amp; мир"}}""");
        });
        using var client = new HttpClient(handler);
        using var service = new OnlineServices(fixture.Store, fixture.Vault, client);
        var info = Snapshot(service.TranslationInfo());
        checks.That(info.TryGetProperty("defaultProvider", out var provider) && provider.GetString()=="mymemory", "free provider is available by default without key");
        var result = Snapshot(await service.Translate("Hello & world", "en", "ru"));
        checks.That(captured!.Uri.Host=="api.mymemory.translated.net" && captured.Uri.AbsolutePath=="/get", "free translation only queries documented endpoint");
        checks.That(captured.Authorization==null && captured.Uri.Query.Contains("q=Hello%20%26%20world"), "free request encodes input and sends no credential");
        checks.That(result.GetProperty("text").GetString()=="Привет & мир", "free response decodes HTML entities as plain text");
    }

    static async Task FreeTranslationFailures(Checks checks)
    {
        using var fixture=new Fixture();
        var response="""{"quotaFinished":true,"responseStatus":403,"responseDetails":"private diagnostics"}""";
        using var handler=new Handler((_,_)=>Task.FromResult(Json(response)));
        using var client=new HttpClient(handler);
        using var service=new OnlineServices(fixture.Store,fixture.Vault,client);
        await checks.Fault(()=>service.Translate(new string('я',251),"ru","en"),"limit","free limit counts UTF-8 bytes");
        await checks.Fault(()=>service.Translate("hello","auto","ru"),"language","free provider does not fake language detection");
        await checks.Fault(()=>service.Translate("hello","en","ru","unknown"),"provider","provider cannot select arbitrary endpoint");
        checks.That(handler.Calls==0,"invalid inputs cause no network traffic");
        var fault=await checks.Fault(()=>service.Translate("hello","en","ru"),"quota","quota exhaustion is actionable");
        checks.That(!fault.Message.Contains("private diagnostics"),"upstream diagnostic is not exposed");
        response="""{"responseStatus":400,"responseDetails":"bad pair"}""";
        await checks.Fault(()=>service.Translate("hello","en","ru"),"translation","HTTP 200 with failed status is not a translation");
        response="""{"responseStatus":200,"responseData":{}}""";
        await checks.Fault(()=>service.Translate("hello","en","ru"),"translation","malformed free response is handled");
        response="not JSON";
        await checks.Fault(()=>service.Translate("hello","en","ru"),"translation","invalid JSON is handled");
    }

    // Explicit opt-in probe; ordinary regression tests never use external network.
    public static async Task<int> RunPublic()
    {
        using var fixture = new Fixture();
        using var service = new OnlineServices(fixture.Store, fixture.Vault);
        var failures = 0;
        Console.WriteLine($"PUBLIC PROBE {DateTimeOffset.UtcNow:O}; Moscow is an explicit test city; no credentials.");
        try
        {
            var weather = Snapshot(await service.Weather("Moscow"));
            Console.WriteLine($"PUBLIC WEATHER OK: city={weather.GetProperty("city").GetString()}; time={weather.GetProperty("time").GetString()}; days={weather.GetProperty("forecast").GetArrayLength()}; stale={weather.GetProperty("stale").GetBoolean()}");
        }
        catch (Exception error)
        {
            failures++;
            Console.WriteLine($"PUBLIC WEATHER FAILED: {error.GetType().Name}; {error.Message}");
        }
        try
        {
            var currency = (CurrencySnapshot)await service.Currency();
            Console.WriteLine($"PUBLIC CBR OK: quoteDate={currency.Date}; rates={currency.Rates.Count}; stale={currency.Stale}; source={currency.Source}");
        }
        catch (Exception error)
        {
            failures++;
            Console.WriteLine($"PUBLIC CBR FAILED: {error.GetType().Name}; {error.Message}");
        }
        return failures;
    }

    static async Task TranslationRequest(Checks checks)
    {
        using var fixture = new Fixture();
        const string text = "  Первая строка\nВторая строка 😀\r\n  ";
        const string translated = "  First line\nSecond line 😀\r\n  ";
        Request? captured = null;
        using var handler = new Handler(async (request, cancellation) => {
            captured = await Request.Capture(request, cancellation);
            return Json(JsonSerializer.Serialize(new { translations = new[] { new { text = translated } } }));
        });
        using var client = new HttpClient(handler);
        using var service = new OnlineServices(fixture.Store, fixture.Vault, client);
        service.ConfigureTranslation(SyntheticKey, free: true);
        var info = Snapshot(service.TranslationInfo());
        checks.That(info.GetProperty("configured").GetBoolean(), "configuration is available after saving");
        checks.That(handler.Calls == 0, "configuration and provider info must not send user text or validate key over network");
        checks.That(!info.GetRawText().Contains(SyntheticKey), "provider info does not disclose credential to UI");
        var reference = fixture.Store.Read<string?>("personal.deepl.secret", null);
        checks.That(reference != SyntheticKey && Guid.TryParseExact(reference, "N", out _), "settings contain only vault reference");
        checks.That(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(fixture.Secrets, reference!))).Contains(SyntheticKey), "vault file is not plaintext");

        var result = Snapshot(await service.Translate(text, "ru", "en-gb", "deepl"));
        checks.That(captured is not null && captured.Method == HttpMethod.Post, "translation sends POST");
        checks.That(captured!.Uri == new Uri("https://api-free.deepl.com/v2/translate"), "free credential uses documented DeepL endpoint");
        checks.That(captured.Authorization == "DeepL-Auth-Key " + fixture.Vault.Read(reference!), "authorization comes from protected vault credential");
        checks.That(captured.MediaType == "application/json", "translation is JSON");
        using var body = JsonDocument.Parse(captured.Body);
        var root = body.RootElement;
        checks.That(root.GetProperty("text").GetArrayLength() == 1 && root.GetProperty("text")[0].GetString() == text, "request preserves spaces, Unicode and both newline forms");
        checks.That(root.GetProperty("source_lang").GetString() == "RU", "explicit source language reaches provider");
        checks.That(root.GetProperty("target_lang").GetString() == "EN-GB", "regional target reaches provider");
        checks.That(root.GetProperty("preserve_formatting").GetBoolean(), "format preservation requested");
        checks.That(!captured.Body.Contains(SyntheticKey), "credential is absent from request content");
        checks.That(result.GetProperty("text").GetString() == translated, "response preserves provider text exactly");
        checks.That(result.GetProperty("provider").GetString() == "DeepL API", "result identifies provider");
    }

    static async Task TranslationAutoLanguage(Checks checks)
    {
        using var fixture = new Fixture();
        Request? captured = null;
        using var handler = new Handler(async (request, cancellation) => {
            captured = await Request.Capture(request, cancellation);
            return Json("""{"translations":[{"text":"Bonjour"}]}""");
        });
        using var client = new HttpClient(handler);
        using var service = new OnlineServices(fixture.Store, fixture.Vault, client);
        await checks.Fault(() => service.Translate("Hello", "AUTO", "FR", "deepl"), "key", "unconfigured translation fails before network");
        checks.That(handler.Calls == 0, "unconfigured translation makes no request");
        service.ConfigureTranslation(SyntheticKey, free: false);
        await service.Translate("Hello", "auto", "fr", "deepl");
        checks.That(captured!.Uri.Host == "api.deepl.com", "paid credential uses paid endpoint");
        using var body = JsonDocument.Parse(captured.Body);
        checks.That(!body.RootElement.TryGetProperty("source_lang", out _), "auto detection omits source_lang rather than sending AUTO");
        await checks.Fault(() => service.Translate("Hello", "RU", "en\r\nInjected", "deepl"), "language", "invalid language is rejected");
        checks.That(handler.Calls == 1, "invalid language causes no second network request");
    }

    static async Task TranslationCancellation(Checks checks)
    {
        using var fixture = new Fixture();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(async (request, token) => {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var input = body.RootElement.GetProperty("text")[0].GetString();
            if (input == "old")
            {
                using var registration = token.Register(() => cancelled.TrySetResult());
                started.TrySetResult();
                // Deliberately ignore cancellation, as an already buffered late response may do.
                await releaseOld.Task.WaitAsync(TimeSpan.FromSeconds(5));
                return Json("""{"translations":[{"text":"obsolete result"}]}""");
            }
            return Json("""{"translations":[{"text":"current result"}]}""");
        });
        using var client = new HttpClient(handler);
        using var service = new OnlineServices(fixture.Store, fixture.Vault, client);
        service.ConfigureTranslation(SyntheticKey, true);
        var old = service.Translate("old", "AUTO", "EN", "deepl");
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var current = Snapshot(await service.Translate("new", "AUTO", "EN", "deepl"));
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            checks.That(current.GetProperty("text").GetString() == "current result", "new translation succeeds while obsolete call is pending");
        }
        finally { releaseOld.TrySetResult(); }
        await checks.Fault(() => old, "cancelled", "late obsolete response cannot return as successful translation");

        var explicitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var blockingHandler = new Handler(async (_, token) => {
            explicitStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Cancelled handler unexpectedly resumed");
        });
        using var blockingClient = new HttpClient(blockingHandler);
        using var blockingService = new OnlineServices(fixture.Store, fixture.Vault, blockingClient);
        var pending = blockingService.Translate("cancel me", "AUTO", "EN", "deepl");
        await explicitStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        blockingService.CancelTranslation();
        await checks.Fault(() => pending.WaitAsync(TimeSpan.FromSeconds(5)), "cancelled", "explicit cancel returns structured cancellation");
    }

    static async Task TranslationFailures(Checks checks)
    {
        using var fixture = new Fixture();
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests) {
            Content = new StringContent("provider-private-diagnostic-do-not-expose")
        }));
        using var client = new HttpClient(handler);
        using var service = new OnlineServices(fixture.Store, fixture.Vault, client);
        service.ConfigureTranslation(SyntheticKey, true);
        var fault = await checks.Fault(() => service.Translate("Hello", "AUTO", "EN", "deepl"), "network", "provider HTTP failure becomes actionable fault");
        checks.That(fault.Message.Contains("429") && !fault.Message.Contains("provider-private-diagnostic"), "error preserves status without exposing response body");

        using var largeHandler = new Handler((_, _) => {
            // No Content-Length: protection must also bound a streaming response.
            var response = new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StreamContent(new NonSeekableStream(new byte[2 * 1024 * 1024]))
            };
            return Task.FromResult(response);
        });
        using var largeClient = new HttpClient(largeHandler);
        using var largeService = new OnlineServices(fixture.Store, fixture.Vault, largeClient);
        await checks.Fault(() => largeService.Translate("Hello", "AUTO", "EN", "deepl"), "limit", "unbounded provider stream is rejected before JSON parsing");

        using var offlineHandler = new Handler((_, _) => throw new HttpRequestException("synthetic disconnected transport"));
        using var offlineClient = new HttpClient(offlineHandler);
        using var offlineService = new OnlineServices(fixture.Store, fixture.Vault, offlineClient);
        await checks.Fault(() => offlineService.Translate("Hello", "AUTO", "EN", "deepl"), "offline", "disconnected transport produces offline state");
    }

    static async Task SonioxSpeech(Checks checks)
    {
        using var fixture = new Fixture();
        Request? captured = null;
        byte[] audio = [0x49, 0x44, 0x33];
        using var handler = new Handler(async (request, cancellation) => {
            captured = await Request.Capture(request, cancellation);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(audio) };
        });
        using var client = new HttpClient(handler);
        using var service = new OnlineServices(fixture.Store, fixture.Vault, client);
        await checks.Fault(() => service.SpeakTranslation("Привет", "ru", default), "key", "speech requires a Soniox key");
        service.ConfigureSoniox(SyntheticKey);
        var info = Snapshot(service.TranslationInfo());
        checks.That(info.GetProperty("sonioxConfigured").GetBoolean() && !info.GetProperty("configured").GetBoolean(),
            "Soniox and DeepL are independently configured");
        checks.That(!info.GetRawText().Contains(SyntheticKey), "status does not expose the Soniox key");
        await checks.Fault(() => service.TranslateVoice("not valid base64", "en", "ru", default),
            "soniox_audio", "invalid recording is rejected before opening a connection");
        checks.That(handler.Calls == 0, "saving a key and checking configuration make no HTTP requests");

        var response = Snapshot(await service.SpeakTranslation("Привет", "ru", default));
        checks.That(captured is { Method: var method } && method == HttpMethod.Post &&
            captured.Uri == new Uri("https://tts-rt.soniox.com/tts"), "speech uses the Soniox TTS endpoint");
        checks.That(captured!.Authorization == "Bearer " + SyntheticKey && !captured.Body.Contains(SyntheticKey),
            "Soniox credential stays in the authorization header");
        using var body = JsonDocument.Parse(captured.Body);
        checks.That(body.RootElement.GetProperty("text").GetString() == "Привет" &&
            body.RootElement.GetProperty("language").GetString() == "ru" &&
            body.RootElement.GetProperty("audio_format").GetString() == "mp3", "TTS requests the translated text and language");
        checks.That(response.GetProperty("audio").GetBytesFromBase64().SequenceEqual(audio), "the bridge receives the audio bytes intact");
    }

    static async Task WeatherCache(Checks checks)
    {
        using var fixture = new Fixture();
        const string city = "Москва & count=99 /?#";
        var requests = new List<Uri>();
        using var handler = new Handler((request, _) => {
            requests.Add(request.RequestUri!);
            return Task.FromResult(Json(request.RequestUri!.Host == "geocoding-api.open-meteo.com" ? Location : Forecast));
        });
        using var client = new HttpClient(handler);
        using var service = new OnlineServices(fixture.Store, fixture.Vault, client);
        var fresh = Snapshot(await service.Weather("  " + city + "  "));
        checks.That(requests.Count == 2, "weather resolves city then fetches coordinates");
        var query = requests[0].Query.TrimStart('?').Split('&');
        checks.That(query.Length == 3 && query.Contains("count=1") && query.Contains("language=ru"), "city cannot inject additional query parameters");
        checks.That(Uri.UnescapeDataString(query.Single(part => part.StartsWith("name="))[5..]) == city, "city text survives URL encoding");
        checks.That(requests[1].Host == "api.open-meteo.com" && requests[1].Query.Contains("latitude=55.75&longitude=37.625"), "forecast uses resolved coordinates with invariant decimals");
        checks.That(fresh.GetProperty("city").GetString() == "Москва, Россия", "weather shows resolved place");
        checks.That(fresh.GetProperty("temperature").GetDouble() == 7.5 && fresh.GetProperty("precipitation").GetDouble() == .6, "weather values reach result without unit confusion");
        checks.That(fresh.GetProperty("forecast")[1].GetProperty("min").GetDouble() == 1, "daily forecast values retained");
        checks.That(!fresh.GetProperty("stale").GetBoolean(), "successful forecast is fresh");
        checks.That(fresh.GetProperty("source").GetString()!.Contains("Open-Meteo"), "forecast identifies source");

        using var offlineHandler = new Handler((_, _) => throw new HttpRequestException("synthetic offline"));
        using var offlineClient = new HttpClient(offlineHandler);
        // Recreate state as after restart; the result must come from durable Store.
        var reloaded = new PersonalState(fixture.Directory);
        using var offline = new OnlineServices(reloaded.Store, fixture.Vault, offlineClient);
        var cached = Snapshot(await offline.Weather(city));
        checks.That(cached.GetProperty("stale").GetBoolean(), "offline forecast explicitly marked stale");
        checks.That(cached.GetProperty("temperature").GetDouble() == 7.5, "offline cache retains last real temperature");
        checks.That(cached.GetProperty("fetchedAt").GetString() == fresh.GetProperty("fetchedAt").GetString(), "offline fallback never fabricates new fetch timestamp");

        using var missing = new OnlineServices(reloaded.Store, fixture.Vault, offlineClient);
        await checks.Fault(() => missing.Weather("Другой город"), "offline", "offline cache does not leak one city's weather into another");
    }

    static async Task WeatherSchemaFailure(Checks checks)
    {
        using var fixture = new Fixture();
        using var initialHandler = new Handler((request, _) => Task.FromResult(Json(request.RequestUri!.Host == "geocoding-api.open-meteo.com" ? Location : Forecast)));
        using var initialClient = new HttpClient(initialHandler);
        using var initial = new OnlineServices(fixture.Store, fixture.Vault, initialClient);
        await initial.Weather("Москва");
        foreach (var badForecast in new[] {
            """{"current":{},"daily":{}}""",
            Forecast.Replace("\"temperature_2m\":7.5", "\"temperature_2m\":\"unknown\""),
            Forecast.Replace("\"temperature_2m_min\":[2.5,1.0]", "\"temperature_2m_min\":[2.5]"),
        })
        {
            using var changedHandler = new Handler((request, _) => Task.FromResult(Json(request.RequestUri!.Host == "geocoding-api.open-meteo.com" ? Location : badForecast)));
            using var changedClient = new HttpClient(changedHandler);
            using var changed = new OnlineServices(fixture.Store, fixture.Vault, changedClient);
            var fallback = Snapshot(await changed.Weather("Москва"));
            checks.That(fallback.GetProperty("stale").GetBoolean() && fallback.GetProperty("temperature").GetDouble() == 7.5, "missing fields, wrong types or short daily arrays retain last useful forecast as stale");
        }
        using var emptyFixture = new Fixture();
        using var noCacheHandler = new Handler((request, _) => Task.FromResult(Json(request.RequestUri!.Host == "geocoding-api.open-meteo.com" ? Location : "{}")));
        using var noCacheClient = new HttpClient(noCacheHandler);
        using var noCache = new OnlineServices(emptyFixture.Store, emptyFixture.Vault, noCacheClient);
        await checks.Fault(() => noCache.Weather("Москва"), "offline", "malformed forecast without cache reports unavailable weather");
    }

    static JsonElement Snapshot(object value) => JsonSerializer.SerializeToElement(value, Protocol.Json);
    static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    sealed class Checks
    {
        public int Count { get; private set; }
        public void That(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Count++;
        }
        public async Task<CommandFault> Fault(Func<Task<object>> action, string code, string message)
        {
            try { await action(); }
            catch (CommandFault error) { That(error.Code == code, message + ": incorrect error code " + error.Code); return error; }
            throw new InvalidOperationException(message + ": expected failure");
        }
    }

    sealed record Request(HttpMethod Method, Uri Uri, string? Authorization, string? MediaType, string Body)
    {
        public static async Task<Request> Capture(HttpRequestMessage request, CancellationToken cancellation) => new(
            request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(),
            request.Content?.Headers.ContentType?.MediaType,
            request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellation));
    }

    sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return send(request, cancellationToken);
        }
    }

    sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        public override bool CanSeek => false;
    }

    sealed class Fixture : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "SideWidget-online-" + Guid.NewGuid().ToString("N"));
        public string Secrets => Path.Combine(Directory, "secrets");
        public Store Store { get; }
        public SecretVault Vault { get; }
        public Fixture() { Store = new Store(Directory); Vault = new SecretVault(Secrets); }
        public void Dispose()
        {
            var resolved = Path.GetFullPath(Directory);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("SideWidget-online-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside the test directory");
            System.IO.Directory.Delete(resolved, recursive: true);
        }
    }
}

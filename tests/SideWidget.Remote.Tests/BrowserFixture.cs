using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using SideWidget.Infrastructure;
using SideWidget.Remote;

static class BrowserFixture
{
    public static async Task Run(string projectRoot)
    {
        var directory = Path.Combine(Path.GetTempPath(), "SideWidget.Remote.Browser", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var access = new RemoteAccess(Path.Combine(directory, "devices.json"));
        using var certificates = new RemoteCertificates(Path.Combine(directory, "certificates"));
        using var leaf = certificates.Get(IPAddress.Loopback);
        using var rsa = leaf.GetRSAPublicKey()!;
        var pin = Convert.ToBase64String(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()));
        var playing = true; var volume = .55; var number = 1;
        await using var host = new RemoteServer(access, new("fixture", "Loopback fixture", IPAddress.Loopback, IPAddress.Parse("255.0.0.0")),
            new(Port: 47841, SetupPort: 47840), leaf, certificates.ExportRoot(), Path.Combine(projectRoot, "src/SideWidget.Web/dist"),
            () => Task.FromResult<object>(new
            {
                media = new { available = true, sessions = new[] { new { id = "fixture-session", title = number == 1 ? "Тестовая композиция" : "Следующая тестовая композиция", artist = "Демонстрационные данные", source = "Тест Яндекс Музыки", playing, positionSeconds = 42, durationSeconds = 240, canPlay = true, canPause = true, canPrevious = true, canNext = true, canSeek = true } } },
                audio = new { available = true, volume, muted = false }
            }), request =>
            {
                if (request.Command == "pause") playing = false;
                if (request.Command == "play") playing = true;
                if (request.Command == "next") number++;
                if (request.Command == "volume") volume = request.Volume!.Value;
                return Task.CompletedTask;
            });
        await host.StartAsync();
        var output = Path.Combine(projectRoot, "output/phone-remote-browser.json");
        var invite = access.CreateInvite();
        File.WriteAllText(output, JsonSerializer.Serialize(new { url = "https://127.0.0.1:47841/#invite=" + invite.Token, setupUrl = "http://127.0.0.1:47840/", pin, fixture = true }));
        Console.WriteLine("Browser fixture ready on loopback. Type approve / revoke / stop. Synthetic media only.");
        while (true)
        {
            var action = await Console.In.ReadLineAsync();
            if (action is null or "stop") break;
            var info = JsonSerializer.SerializeToElement(access.Info(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (action == "approve") foreach (var p in info.GetProperty("pending").EnumerateArray()) access.Decide(p.GetProperty("id").GetString()!, true);
            if (action == "revoke") foreach (var d in info.GetProperty("devices").EnumerateArray()) access.Revoke(d.GetProperty("id").GetString()!);
            Console.WriteLine("Fixture action: " + action);
        }
    }
}

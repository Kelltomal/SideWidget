using System.IO.Compression;
using System.Security.Cryptography;
using SideWidget.Core;

namespace SideWidget.App;

sealed class PiperVoices(string profile)
{
    // Match the Windows Piper runtime with single-codepoint phoneme maps.
    const string VoiceBase = "https://huggingface.co/rhasspy/piper-voices/resolve/375a0fe641dea077c2a47b4e9a056d6da521eed3/";
    const string JapaneseBase = "https://huggingface.co/ayousanz/piper-plus-css10-ja-6lang/resolve/bd0d812d4db9182ecdb907ef074becf9e230c17f/";
    const string DownloadError = "Не удалось скачать голос Piper. Проверьте интернет и свободное место.";
    readonly string directory = Path.GetFullPath(Path.Combine(profile, "piper"));
    readonly SemaphoreSlim gate = new(1, 1);
    static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    sealed record FileAsset(string Url, long Size, string Hash);
    sealed record Runtime(string Folder, string Executable, FileAsset Archive);
    sealed record Voice(FileAsset Model, FileAsset Config, Runtime Engine);
    internal sealed record Installation(string Executable, string Model, string Config, string? OpenJTalkDictionary);
    internal readonly record struct DownloadProgress(long Received, long Total);

    static readonly Runtime Piper = new("piper-2023.11.14-2", "piper/piper.exe",
        new("https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip",
            22477236, "f3c58906402b24f3a96d92145f58acba6d86c9b5db896d207f78dc80811efcea"));
    static readonly Runtime PiperPlus = new("piper-plus-2.1.0", "piper-plus/bin/piper-plus.exe",
        new("https://github.com/ayutaz/piper-plus/releases/download/v2.1.0/piper-plus-cpp-windows-x64.zip",
            34917696, "37e2bf2d80877f583b98dfa4064a49f39af2cb2f937a457f160f80c14dfb3e67"));

    static readonly IReadOnlyDictionary<string, Voice> Voices = new Dictionary<string, Voice>(StringComparer.Ordinal)
    {
        ["en"] = Classic("en/en_US/lessac/medium/en_US-lessac-medium", 63201294,
            "5efe09e69902187827af646e1a6e9d269dee769f9877d17b16b1b46eeaaf019f", 4885,
            "efe19c417bed055f2d69908248c6ba650fa135bc868b0e6abb3da181dab690a0"),
        ["ru"] = Classic("ru/ru_RU/irina/medium/ru_RU-irina-medium", 63201294,
            "8ff38212d23da300bbe3705c645e6e5b9475f0bfde01558eb17813e22acaaaaa", 4765,
            "c2ec28bb38e2b59e93b959b3e40348c1afebbd272f30fed5d41205d08e98a9d7"),
        ["de"] = Classic("de/de_DE/thorsten/medium/de_DE-thorsten-medium", 63201294,
            "7e64762d8e5118bb578f2eea6207e1a35a8e0c30595010b666f983fc87bb7819", 4819,
            "974adee790533adb273a1ac88f49027d2a1b8f0f2cf4905954a4791e79264e85"),
        ["fr"] = Classic("fr/fr_FR/siwis/medium/fr_FR-siwis-medium", 63201294,
            "641d1ab097da2b81128c076810edb052b385decc8be3381814802a64a73baf99", 4875,
            "39479916c2db192b5ac9764daddd0c744d83e023ad890c6976c0633ae4df8959"),
        ["es"] = Classic("es/es_ES/davefx/medium/es_ES-davefx-medium", 63201294,
            "6658b03b1a6c316ee4c265a9896abc1393353c2d9e1bca7d66c2c442e222a917", 4817,
            "0e0dda87c732f6f38771ff274a6380d9252f327dca77aa2963d5fbdf9ec54842"),
        ["zh"] = Classic("zh/zh_CN/huayan/medium/zh_CN-huayan-medium", 63201294,
            "9929917bf8cabb26fd528ea44d3a6699c11e87317a14765312420be230be0f3d", 4822,
            "d521dc45504a8ccc99e325822b35946dd701840bfb07e3dbb31a40929ed6a82b"),
        ["ja"] = new(new(JapaneseBase + "css10-ja-6lang-fp16.onnx", 39652717,
                "5ebc51dbf897238523f3df0d6e0f6c93033bc5cda3f8602a8379ebe2a4738c42"),
            new(JapaneseBase + "config.json", 5912, "3c613dacf139349c20159433559b2e94e6364e9a340d95245f3de6006f453fcc"),
            PiperPlus),
    };

    static Voice Classic(string path, long modelSize, string modelHash, long configSize, string configHash) =>
        new(new(VoiceBase + path + ".onnx", modelSize, modelHash),
            new(VoiceBase + path + ".onnx.json", configSize, configHash), Piper);

    internal async Task<Installation> PrepareAsync(string language, Action<DownloadProgress> progress, CancellationToken cancellation)
    {
        if (!Voices.TryGetValue(language, out var voice))
            throw new CommandFault("speech_language", "Выберите язык озвучивания.");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMinutes(8));
        var token = deadline.Token;
        try
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var engineDirectory = Path.Combine(directory, "runtime", voice.Engine.Folder);
                var executable = Path.Combine(engineDirectory, voice.Engine.Executable);
                var dictionary = voice.Engine == PiperPlus
                    ? Path.Combine(engineDirectory, "piper-plus", "share", "open_jtalk", "dic") : null;
                var voiceDirectory = Path.Combine(directory, "voices", language);
                var model = Path.Combine(voiceDirectory, "voice.onnx");
                var config = Path.Combine(voiceDirectory, "voice.onnx.json");
                var needsEngine = !File.Exists(executable) || (dictionary is not null && !File.Exists(Path.Combine(dictionary, "sys.dic")));
                var needsModel = !await MatchesAsync(voice.Model, model, token).ConfigureAwait(false);
                var needsConfig = !await MatchesAsync(voice.Config, config, token).ConfigureAwait(false);
                var total = (needsEngine ? voice.Engine.Archive.Size : 0) +
                    (needsModel ? voice.Model.Size : 0) + (needsConfig ? voice.Config.Size : 0);
                long completed = 0;
                if (total > 0) progress(new(0, total));
                void Report(long bytes) => progress(new(completed + bytes, total));
                if (needsEngine)
                {
                    await InstallRuntimeAsync(voice.Engine, engineDirectory, Report, token).ConfigureAwait(false);
                    completed += voice.Engine.Archive.Size;
                }
                if (needsModel)
                {
                    await DownloadAsync(voice.Model, model, Report, token).ConfigureAwait(false);
                    completed += voice.Model.Size;
                }
                if (needsConfig) await DownloadAsync(voice.Config, config, Report, token).ConfigureAwait(false);
                return new(executable, model, config, dictionary);
            }
            finally { gate.Release(); }
        }
        catch (Exception error) when (error is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException ||
                                      error is OperationCanceledException && !cancellation.IsCancellationRequested)
        {
            throw new CommandFault("speech_download", DownloadError);
        }
    }

    static async Task InstallRuntimeAsync(Runtime runtime, string destination, Action<long> progress, CancellationToken token)
    {
        var staging = destination + "." + Guid.NewGuid().ToString("N");
        var archive = Path.Combine(staging, "runtime.zip");
        var extracted = Path.Combine(staging, "files");
        try
        {
            await DownloadAsync(runtime.Archive, archive, progress, token).ConfigureAwait(false);
            await Task.Run(() =>
            {
                using var zip = ZipFile.OpenRead(archive);
                if (zip.Entries.Count > 4000 || zip.Entries.Sum(entry => entry.Length) > 300L * 1024 * 1024)
                    throw new InvalidDataException("Invalid Piper archive.");
                zip.ExtractToDirectory(extracted);
                token.ThrowIfCancellationRequested();
                if (!File.Exists(Path.Combine(extracted, runtime.Executable)))
                    throw new InvalidDataException("Piper executable missing.");
                if (Directory.Exists(destination)) Directory.Delete(destination, recursive: true);
                Directory.Move(extracted, destination);
            }, token).ConfigureAwait(false);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    static async Task<bool> MatchesAsync(FileAsset asset, string destination, CancellationToken token)
    {
        var file = new FileInfo(destination);
        if (!file.Exists || file.Length != asset.Size) return false;
        await using var stream = file.OpenRead();
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false))
            .Equals(asset.Hash, StringComparison.OrdinalIgnoreCase);
    }

    static async Task DownloadAsync(FileAsset asset, string destination, Action<long> progress, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            using var response = await Http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length != asset.Size)
                throw new InvalidDataException("Piper asset length mismatch.");
            await using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[65536];
            long received = 0;
            long nextReport = 0;
            await using (var output = File.Create(temporary))
            {
                int count;
                while ((count = await source.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                {
                    received += count;
                    if (received > asset.Size) throw new InvalidDataException("Piper asset is too large.");
                    await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                    hash.AppendData(buffer, 0, count);
                    if (received >= nextReport || received == asset.Size)
                    {
                        progress(received);
                        nextReport = received + 1024 * 1024;
                    }
                }
            }
            if (received != asset.Size ||
                !Convert.ToHexString(hash.GetHashAndReset()).Equals(asset.Hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Piper asset checksum mismatch.");
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }
}

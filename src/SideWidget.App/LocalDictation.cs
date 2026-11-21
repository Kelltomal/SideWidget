using System.Buffers.Binary;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using SideWidget.Core;
using Whisper.net;

namespace SideWidget.App;

sealed class LocalDictation(string directory)
{
    const int ModelSize = 59707625;
    const string ModelHash = "422F1AE452ADE6F30A004D7E5C6A43195E4433BC370BF23FAC9CC591F01A8898";
    const string ModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/f281eb4/ggml-base-q5_1.bin";
    const string InvalidRecording = "Не удалось прочитать запись. Запишите фразу заново.";
    const string DownloadFailed = "Не удалось скачать модель распознавания. Проверьте интернет и свободное место.";
    public const int MaxWavSize = 44 + 30 * 16000 * 2;
    static readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(2) };
    readonly string modelPath = Path.Combine(directory, "speech-models", "ggml-base-q5_1.bin");
    readonly SemaphoreSlim gate = new(1, 1);

    public async Task PrepareAsync(IProgress<int> progress, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        try
        {
            await gate.WaitAsync(deadline.Token).ConfigureAwait(false);
            try
            {
                if (!await IsValidModel(deadline.Token).ConfigureAwait(false))
                    await DownloadAsync(progress, deadline.Token).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
        catch (Exception error) when (error is HttpRequestException or IOException or UnauthorizedAccessException ||
                                      error is OperationCanceledException && !cancellation.IsCancellationRequested)
        {
            throw new CommandFault("local_model", DownloadFailed);
        }
    }

    async Task<bool> IsValidModel(CancellationToken cancellation)
    {
        var model = new FileInfo(modelPath);
        if (!model.Exists || model.Length != ModelSize) return false;
        await using var stream = model.OpenRead();
        var hash = await SHA256.HashDataAsync(stream, cancellation).ConfigureAwait(false);
        return Convert.ToHexString(hash) == ModelHash;
    }

    async Task DownloadAsync(IProgress<int> progress, CancellationToken cancellation)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        var temporary = modelPath + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            using var response = await http.GetAsync(ModelUrl, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length != ModelSize)
                throw new CommandFault("local_model", DownloadFailed);

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var source = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
            var buffer = new byte[65536];
            var received = 0;
            var lastPercent = -1;
            await using (var destination = File.Create(temporary))
            {
                int read;
                while ((read = await source.ReadAsync(buffer, cancellation).ConfigureAwait(false)) != 0)
                {
                    received += read;
                    if (received > ModelSize) throw new CommandFault("local_model", DownloadFailed);
                    hash.AppendData(buffer, 0, read);
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellation).ConfigureAwait(false);
                    var percent = received * 100L / ModelSize;
                    if (percent != lastPercent) progress.Report(lastPercent = (int)percent);
                }
            }
            if (received != ModelSize || Convert.ToHexString(hash.GetHashAndReset()) != ModelHash)
                throw new CommandFault("local_model", DownloadFailed);

            cancellation.ThrowIfCancellationRequested();
            File.Move(temporary, modelPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public async Task<object> TranscribeAsync(string audio, string sourceLanguage, CancellationToken cancellation)
    {
        if (sourceLanguage is not ("auto" or "ru" or "en" or "de" or "fr" or "es" or "zh" or "ja"))
            throw new CommandFault("schema", "Выберите язык записи.");

        using var recording = new MemoryStream(ReadWav(audio), writable: false);
        await gate.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            if (!await IsValidModel(cancellation).ConfigureAwait(false))
                throw new CommandFault("local_model", "Модель распознавания недоступна. Начните запись заново.");
            var transcript = await Task.Run(async () =>
            {
                using var factory = WhisperFactory.FromPath(modelPath);
                await using var processor = factory.CreateBuilder()
                    .WithLanguage(sourceLanguage)
                    .WithThreads(Math.Clamp(Environment.ProcessorCount / 2, 1, 4))
                    .Build();
                var text = new StringBuilder();
                await foreach (var segment in processor.ProcessAsync(recording, cancellation).ConfigureAwait(false))
                    text.Append(segment.Text);
                cancellation.ThrowIfCancellationRequested();
                return text.ToString().Trim();
            }, cancellation).ConfigureAwait(false);
            if (transcript.Length == 0)
                throw new CommandFault("local_speech", "Речь не распознана. Запишите фразу заново.");
            return new { transcript };
        }
        catch (Exception error) when (error is not (CommandFault or OperationCanceledException))
        {
            throw new CommandFault("local_speech", "Не удалось распознать речь на компьютере. Повторите запись.");
        }
        finally
        {
            gate.Release();
        }
    }

    static byte[] ReadWav(string audio)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(audio); }
        catch (FormatException) { throw new CommandFault("schema", InvalidRecording); }

        if (bytes.Length is <= 44 or > MaxWavSize || bytes.Length % 2 != 0 ||
            !bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)) != bytes.Length - 8 ||
            !bytes.AsSpan(8, 8).SequenceEqual("WAVEfmt "u8) ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16)) != 16 ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(20)) != 1 ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(22)) != 1 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24)) != 16000 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28)) != 32000 ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(32)) != 2 ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(34)) != 16 ||
            !bytes.AsSpan(36, 4).SequenceEqual("data"u8) ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(40)) != bytes.Length - 44)
            throw new CommandFault("schema", InvalidRecording);
        return bytes;
    }
}

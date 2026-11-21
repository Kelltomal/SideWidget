using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using SideWidget.Core;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace SideWidget.App;

sealed class TranslationSpeech(string profile)
{
    const string PlaybackError = "Не удалось воспроизвести текст.";
    readonly PiperVoices voices = new(profile);

    public async Task<object> PlayAsync(string text, string language, Action<PiperVoices.DownloadProgress> downloading,
        Action<string> stage, CancellationToken cancellation)
    {
        var wave = Path.Combine(Path.GetTempPath(), $"SideWidget-Piper-{Guid.NewGuid():N}.wav");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMinutes(29));
        var token = deadline.Token;
        try
        {
            await SynthesizeAsync(text, language, wave, downloading, () => stage("preparing"), token);
            var file = await StorageFile.GetFileFromPathAsync(wave).AsTask(token);
            using var audio = await file.OpenAsync(FileAccessMode.Read).AsTask(token);
            using var source = MediaSource.CreateFromStream(audio, "audio/wav");
            using var player = new MediaPlayer();
            player.CommandManager.IsEnabled = false;
            var playback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            player.MediaEnded += (_, _) => playback.TrySetResult();
            player.MediaFailed += (_, _) => playback.TrySetException(new CommandFault("speech_playback", PlaybackError));
            player.Source = source;
            token.ThrowIfCancellationRequested();
            player.Play();
            stage("playing");
            await playback.Task.WaitAsync(token);
            return new { played = true };
        }
        catch (Exception error) when (error is COMException or IOException or ArgumentException or UnauthorizedAccessException or Win32Exception ||
                                      error is OperationCanceledException && !cancellation.IsCancellationRequested)
        {
            throw new CommandFault("speech_playback", PlaybackError);
        }
        finally
        {
            try { File.Delete(wave); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }

    internal async Task SynthesizeAsync(string text, string language, string wave,
        Action<PiperVoices.DownloadProgress> downloading, Action preparing, CancellationToken cancellation)
    {
        text = text.Trim();
        if (text.Length == 0) throw new CommandFault("speech_empty", "Введите текст для озвучивания.");
        if (language == "auto") throw new CommandFault("speech_language", "Выберите исходный язык для озвучивания.");

        var voice = await voices.PrepareAsync(language, downloading, cancellation).ConfigureAwait(false);
        preparing();
        cancellation.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(voice.Executable)
        {
            WorkingDirectory = Path.GetDirectoryName(voice.Executable)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            StandardInputEncoding = new UTF8Encoding(false),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "-m", voice.Model, "-c", voice.Config, "-f", wave, "--json-input", "-q" })
            start.ArgumentList.Add(argument);
        if (voice.OpenJTalkDictionary is not null)
        {
            start.Environment["PIPER_PLUS_OFFLINE_MODE"] = "1";
            start.Environment["PIPER_PLUS_AUTO_DOWNLOAD_DICT"] = "0";
            start.Environment["OPEN_JTALK_DIC_DIR"] = voice.OpenJTalkDictionary;
            start.ArgumentList.Add("-l");
            start.ArgumentList.Add(language);
        }
        using var process = Process.Start(start) ?? throw new Win32Exception("Could not start Piper.");
        var stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
        try
        {
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { text }).AsMemory(), cancellation).ConfigureAwait(false);
            process.StandardInput.Close();
            await process.WaitForExitAsync(cancellation).ConfigureAwait(false);
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            if (process.ExitCode != 0 || !File.Exists(wave) || new FileInfo(wave).Length <= 44)
                throw new CommandFault("speech_playback", PlaybackError);
        }
        finally
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
                catch (Exception error) when (error is InvalidOperationException or Win32Exception or TimeoutException) { }
            }
        }
    }
}

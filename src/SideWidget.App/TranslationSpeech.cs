using System.Runtime.InteropServices;
using SideWidget.Core;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace SideWidget.App;

static class TranslationSpeech
{
    public static async Task<object> PlayAsync(string text, string language, CancellationToken cancellation)
    {
        text = text.Trim();
        if (text.Length == 0) throw new CommandFault("speech_empty", "Введите текст для озвучивания.");
        if (language is not ("auto" or "ru" or "en" or "de" or "fr" or "es" or "zh" or "ja"))
            throw new CommandFault("schema", "Выберите язык озвучивания.");

        const string playbackError = "Не удалось воспроизвести текст.";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMinutes(29));
        try
        {
            var systemVoice = SpeechSynthesizer.DefaultVoice;
            var preferredLanguage = language == "zh" ? "zh-CN" : language;
            var voice = language == "auto" || Matches(systemVoice, language)
                ? systemVoice
                : SpeechSynthesizer.AllVoices
                    .Where(candidate => Matches(candidate, language))
                    .OrderByDescending(candidate => candidate.Language.Equals(preferredLanguage, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
            if (voice is null)
                throw new CommandFault("speech_voice", "Добавьте голос для выбранного языка в настройках речи Windows.");

            using var synthesizer = new SpeechSynthesizer { Voice = voice };
            using var stream = await synthesizer.SynthesizeTextToStreamAsync(text).AsTask(deadline.Token);
            using var source = MediaSource.CreateFromStream(stream, stream.ContentType);
            using var player = new MediaPlayer();
            player.CommandManager.IsEnabled = false;
            var playback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            player.MediaEnded += (_, _) => playback.TrySetResult();
            player.MediaFailed += (_, _) => playback.TrySetException(new CommandFault("speech_playback", playbackError));
            player.Source = source;
            deadline.Token.ThrowIfCancellationRequested();
            player.Play();
            await playback.Task.WaitAsync(deadline.Token);
            return new { played = true };
        }
        catch (Exception error) when (error is COMException or IOException or ArgumentException or UnauthorizedAccessException ||
                                      error is OperationCanceledException && !cancellation.IsCancellationRequested)
        {
            throw new CommandFault("speech_playback", playbackError);
        }
    }

    static bool Matches(VoiceInformation? voice, string language) =>
        voice is not null && (voice.Language.Equals(language, StringComparison.OrdinalIgnoreCase) ||
            voice.Language.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase));
}

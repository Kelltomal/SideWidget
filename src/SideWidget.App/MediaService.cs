using Windows.Media.Control;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace SideWidget.App;

public sealed class MediaService : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArtworkCache> artwork = new(StringComparer.Ordinal);
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private bool disposed;

    public async Task<object> Get()
    {
        await gate.WaitAsync();
        try { return await Snapshot(); }
        finally { gate.Release(); }
    }

    public async Task<object> Command(string id, string command, double? position, Func<bool>? isAuthorized = null)
    {
        if (command is not ("play" or "pause" or "previous" or "next" or "seek")) throw new ArgumentException("Неизвестная команда медиаплеера.");
        if (command == "seek" && (position is null || !double.IsFinite(position.Value) || position < 0))
            throw new ArgumentOutOfRangeException(nameof(position));
        await gate.WaitAsync();
        try
        {
            await Snapshot(); // Drop closed sessions before accepting commands.
            if (isAuthorized is not null && !isAuthorized()) throw new InvalidOperationException("Доступ к пульту отозван или пульт выключен.");
            if (!sessions.TryGetValue(id, out var session)) throw new InvalidOperationException("Медиасессия завершена. Обновите список источников.");
            var controls = session.GetPlaybackInfo().Controls;
            var accepted = command switch
            {
                "play" when controls.IsPlayEnabled => await session.TryPlayAsync(),
                "pause" when controls.IsPauseEnabled => await session.TryPauseAsync(),
                "previous" when controls.IsPreviousEnabled => await session.TrySkipPreviousAsync(),
                "next" when controls.IsNextEnabled => await session.TrySkipNextAsync(),
                "seek" when controls.IsPlaybackPositionEnabled => await session.TryChangePlaybackPositionAsync(SeekTicks(session, position!.Value)),
                _ => false
            };
            if (!accepted) throw new InvalidOperationException("Источник не поддерживает или отклонил команду.");
            return await Snapshot();
        }
        finally { gate.Release(); }
    }

    private static long SeekTicks(GlobalSystemMediaTransportControlsSession session, double position)
    {
        var timeline = session.GetTimelineProperties();
        var requested = timeline.StartTime.TotalSeconds + position;
        var min = Math.Max(timeline.StartTime.TotalSeconds, timeline.MinSeekTime.TotalSeconds);
        var max = Math.Min(timeline.EndTime.TotalSeconds, timeline.MaxSeekTime.TotalSeconds);
        if (max <= min) throw new InvalidOperationException("Источник не передал диапазон перемотки.");
        return TimeSpan.FromSeconds(Math.Clamp(requested, min, max)).Ticks;
    }

    private async Task<object> Snapshot()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        try
        {
            manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var current = manager.GetSessions().ToArray();
            foreach (var key in sessions.Where(p => !current.Contains(p.Value)).Select(p => p.Key).ToArray())
            { sessions.Remove(key); artwork.Remove(key); }
            var result = new List<object>();
            foreach (var session in current)
            {
                var id = sessions.FirstOrDefault(p => Equals(p.Value, session)).Key;
                if (id is null) { id = session.SourceAppUserModelId + ":" + Guid.NewGuid().ToString("N"); sessions[id] = session; }
                try
                {
                    var properties = await session.TryGetMediaPropertiesAsync();
                    var cover = await ReadArtwork(id, properties);
                    var playback = session.GetPlaybackInfo();
                    var controls = playback.Controls;
                    var timeline = session.GetTimelineProperties();
                    var duration = Math.Max(0, (timeline.EndTime - timeline.StartTime).TotalSeconds);
                    var playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                    var position = (timeline.Position - timeline.StartTime).TotalSeconds;
                    if (playing && playback.PlaybackRate is { } rate && timeline.LastUpdatedTime > DateTimeOffset.MinValue)
                        position += Math.Max(0, (DateTimeOffset.UtcNow - timeline.LastUpdatedTime).TotalSeconds) * rate;
                    result.Add(new { id, title = properties.Title, artist = properties.Artist, source = session.SourceAppUserModelId,
                        playing, positionSeconds = duration > 0 ? Math.Clamp(position, 0, duration) : (double?)null,
                        durationSeconds = duration > 0 ? (double?)duration : null,
                        canPlay = controls.IsPlayEnabled, canPause = controls.IsPauseEnabled,
                        canPrevious = controls.IsPreviousEnabled, canNext = controls.IsNextEnabled,
                        canSeek = controls.IsPlaybackPositionEnabled && duration > 0 && timeline.MaxSeekTime > timeline.MinSeekTime,
                        artwork = cover });
                }
                catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException) { sessions.Remove(id); artwork.Remove(id); }
            }
            return new { available = true, sessions = result };
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            sessions.Clear(); artwork.Clear(); manager = null;
            return new { available = false, sessions = Array.Empty<object>(), message = "Системные медиасессии недоступны: " + e.Message };
        }
    }

    private async Task<string?> ReadArtwork(string id, GlobalSystemMediaTransportControlsSessionMediaProperties properties)
    {
        // Cache belongs to a specific live session and track. A new source/track can never reuse its predecessor's cover.
        var key = string.Join('\0', properties.Title, properties.Artist, properties.AlbumTitle, properties.TrackNumber.ToString());
        if (artwork.TryGetValue(id, out var cached) && cached.Key == key && DateTimeOffset.UtcNow < cached.Expires)
            return cached.Data;
        artwork.Remove(id);
        string? data = null;
        try
        {
            if (properties.Thumbnail is not null)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
                using var stream = await properties.Thumbnail.OpenReadAsync().AsTask(timeout.Token);
                // Bound encoded bytes, decoded dimensions, and time independently. No files or remote URLs are used.
                if (stream.Size is > 0 and <= 262144)
                {
                    var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(timeout.Token);
                    if (decoder.PixelWidth is > 0 and <= 2048 && decoder.PixelHeight is > 0 and <= 2048)
                    {
                        using var input = stream.GetInputStreamAt(0);
                        using var reader = new DataReader(input);
                        var bytes = new byte[(int)stream.Size];
                        if (await reader.LoadAsync((uint)bytes.Length).AsTask(timeout.Token) == bytes.Length)
                        {
                            reader.ReadBytes(bytes);
                            // Derive MIME from bytes, never trust a source-supplied SVG or arbitrary data URI.
                            var mime = ImageMime(bytes);
                            if (mime is not null) data = "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
                        }
                    }
                }
            }
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException or OperationCanceledException or ArgumentException or InvalidOperationException) { }
        artwork[id] = new(key, data, DateTimeOffset.UtcNow.AddSeconds(data is null ? 10 : 60));
        return data;
    }

    private static string? ImageMime(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) return "image/jpeg";
        if (bytes.Length >= 12 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP") return "image/webp";
        return null;
    }

    private sealed record ArtworkCache(string Key, string? Data, DateTimeOffset Expires);

    // No event subscriptions or native handles are retained. Do not mutate the session map
    // here: a final asynchronous read may still be completing when the host closes.
    public void Dispose() { disposed = true; }
}

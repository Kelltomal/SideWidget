using System.Text.Json;

namespace SideWidget.Infrastructure;

static class CodexTurnEvents
{
    const int TailLimit = 512 * 1024;

    // Read server lifecycle records from the projected tail, without retaining chat contents.
    public static bool ConfirmsCompletion(string path, long projectedLength, string turnId)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (projectedLength <= 0 || stream.Length != projectedLength) return false;
            var bytes = new byte[(int)Math.Min(projectedLength, TailLimit)];
            stream.Position = projectedLength - bytes.Length;
            var startsAtBeginning = stream.Position == 0;
            stream.ReadExactly(bytes);
            if (stream.Length != projectedLength || bytes[^1] != (byte)'\n') return false;
            for (var end = bytes.Length - 1; end >= 0;)
            {
                var separator = bytes.AsSpan(0, end).LastIndexOf((byte)'\n');
                if (separator < 0 && !startsAtBeginning) break;
                var line = bytes.AsMemory(separator + 1, end - separator - 1);
                end = separator;
                if (line.IsEmpty) continue;
                using var document = JsonDocument.Parse(line);
                var entry = document.RootElement;
                if (entry.ValueKind != JsonValueKind.Object) return false;
                if (!entry.TryGetProperty("type", out var kind) || !kind.ValueEquals("event_msg")
                    || !entry.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object
                    || !payload.TryGetProperty("type", out var type)) continue;
                if (type.ValueEquals("task_started") || type.ValueEquals("turn_aborted")) return false;
                if (!type.ValueEquals("task_complete")) continue;
                return payload.TryGetProperty("turn_id", out var completed) && completed.ValueEquals(turnId)
                    && payload.TryGetProperty("completed_at", out var timestamp)
                    && timestamp.ValueKind == JsonValueKind.Number && timestamp.TryGetInt64(out var seconds)
                    && seconds > 0 && stream.Length == projectedLength;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        {
            return false;
        }
        return false;
    }
}

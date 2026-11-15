using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using SideWidget.Core;

namespace SideWidget.Infrastructure;

sealed record FastTextModel(string[] Labels, Dictionary<string, float[]> Weights)
{
    public (string Label, double Score)? Predict(string text, int minimumFeatures = 14, int minimumWords = 2)
    {
        var scores = new double[Labels.Length];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;
        Add("</s>");
        foreach (var match in Regex.Matches(text.ToLowerInvariant().Replace('ё', 'е'), @"[\p{L}]+").Take(350))
        {
            var word = match.Value;
            if (word.Length is < 2 or > 64) continue;
            seen.Add(word);
            Add("w_" + word);
            var padded = "<" + word + ">";
            for (var size = 3; size <= 4; size++)
                for (var start = 0; start <= padded.Length - size; start++)
                    Add("c_" + padded.Substring(start, size));
        }
        if (count < minimumFeatures || seen.Count < minimumWords) return null;
        var max = scores.Max();
        var best = Array.IndexOf(scores, max);
        return (Labels[best], 1 / scores.Sum(score => Math.Exp((score - max) / count)));

        void Add(string token)
        {
            if (!Weights.TryGetValue(token, out var values)) return;
            for (var index = 0; index < scores.Length; index++) scores[index] += values[index];
            count++;
        }
    }

    public static FastTextModel? Load(string name)
    {
        try
        {
            using var stream = typeof(FastTextModel).Assembly.GetManifestResourceStream(name + ".json.gz");
            if (stream is null) return null;
            using var archive = new GZipStream(stream, CompressionMode.Decompress);
            return JsonSerializer.Deserialize<FastTextModel>(archive, Protocol.Json);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException) { return null; }
    }
}

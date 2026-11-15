using System.Globalization;
using System.IO.Compression;
using System.Text;

using System.Text.RegularExpressions;
using System.Xml;
using SideWidget.Core;

namespace SideWidget.Infrastructure;

sealed class DesktopClassifier
{
    public const int PromptLimit = 2000;
    const int TextLimit = 6000;
    const string Documents = "Документы";
    static readonly Lazy<FastTextModel?> Weights = new(() => FastTextModel.Load("desktop-fasttext"));
    static readonly Dictionary<string, string> Categories = new()
    {
        ["finance"] = "Финансы", ["legal"] = "Договоры и право", ["projects"] = "Проекты",
        ["study"] = "Учёба", ["personal"] = "Личное", ["health"] = "Здоровье",
        ["travel"] = "Поездки", ["auto"] = "Авто"
    };

    sealed record Rule(string Folder, string[] Terms);
    readonly Rule[] rules;

    public DesktopClassifier(string prompt)
    {
        var lines = prompt.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (prompt.Length > PromptLimit || lines.Length > 20) throw InvalidPrompt();
        rules = lines.Select(line =>
        {
            var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !ValidFolder(parts[0])) throw InvalidPrompt();
            var terms = parts[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(Normalize).Where(term => term.Length > 2).ToArray();
            if (terms.Length == 0) throw InvalidPrompt();
            return new Rule(parts[0], terms);
        }).ToArray();
    }

    public bool ModelAvailable => Weights.Value is not null;
    public static string[] Topics => Categories.Values.ToArray();

    public (string Folder, bool Selected) Suggest(FileInfo file, string fallback)
    {
        var name = Normalize(Path.GetFileNameWithoutExtension(file.Name));
        var content = ReadSample(file);
        var haystack = " " + name + " " + Normalize(content) + " ";
        var matching = rules.Where(rule => rule.Terms.Any(term => haystack.Contains(" " + term + " ", StringComparison.Ordinal)))
            .Select(rule => rule.Folder).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (matching.Length == 1) return (matching[0], true);
        if (matching.Length > 1) return (fallback, false);
        if (fallback != Documents || !ModelAvailable) return (fallback, true);
        return PredictTopic(name, content) is { } topic ? topic : (fallback, false);
    }

    public static (string Folder, bool Selected)? PredictTopic(string name, string content)
    {
        name = Normalize(name);
        return Weights.Value?.Predict(name + " " + name + " " + content) is { } prediction &&
            Categories.TryGetValue(prediction.Label, out var folder)
            ? (folder, prediction.Score >= 0.80) : null;
    }
    static string Normalize(string text) => string.Join(' ',
        Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]+").Select(match => match.Value));
    static CommandFault InvalidPrompt() => new("desktop", "Введите по одному правилу на строку. Пример: Финансы: счёт, расходы");

    public static bool ValidFolder(string name)
    {
        if (name.Length is < 1 or > 64 || name != name.Trim() || name.EndsWith('.') ||
            name.Any(character => char.IsControl(character) || "<>:\"/\\|?*".Contains(character))) return false;
        var stem = name.Split('.')[0].TrimEnd();
        return !Regex.IsMatch(stem, @"^(CON|PRN|AUX|NUL|COM[0-9¹²³]|LPT[0-9¹²³]|CONIN\$|CONOUT\$)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static string ReadSample(FileInfo file)
    {
        var extension = file.Extension.ToLowerInvariant();
        var plain = extension is ".txt" or ".md" or ".csv" or ".tsv";
        var office = extension is ".docx" or ".xlsx" or ".pptx" or ".odt" or ".ods" or ".odp";
        if ((!plain && !office) || (file.Attributes & FileAttributes.Offline) != 0) return "";
        try
        {
            using var stream = File.Open(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (plain)
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var buffer = new char[TextLimit];
                return new string(buffer, 0, reader.ReadBlock(buffer));
            }
            if (stream.Length > 25 * 1024 * 1024) return "";
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            var names = extension switch
            {
                ".docx" => new[] { "word/document.xml" },
                ".xlsx" => ["xl/sharedStrings.xml", "xl/worksheets/sheet1.xml"],
                ".pptx" => ["ppt/slides/slide1.xml", "ppt/slides/slide2.xml"],
                _ => ["content.xml"]
            };
            var sample = new StringBuilder();
            foreach (var name in names)
            {
                if (zip.GetEntry(name) is not { Length: <= 1_000_000 } part) continue;
                using var xml = XmlReader.Create(part.Open(), new XmlReaderSettings
                {
                    CloseInput = true, DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = 1_000_000
                });
                while (sample.Length < TextLimit && xml.Read())
                {
                    if (xml.NodeType is not (XmlNodeType.Text or XmlNodeType.CDATA)) continue;
                    var value = xml.Value;
                    sample.Append(value.AsSpan(0, Math.Min(value.Length, TextLimit - sample.Length))).Append(' ');
                }
            }
            return sample.ToString();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or XmlException)
        {
            return "";
        }
    }
}

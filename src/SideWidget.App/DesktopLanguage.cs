using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace SideWidget.App;

internal static class DesktopLanguage
{
    const string ResourcePrefix = "SideWidget.DesktopLocales.";
    static readonly Assembly Resources = typeof(DesktopLanguage).Assembly;
    static readonly Dictionary<string, string> Languages = Resources.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Where(attribute => attribute.Key == "SideWidget.WebLocales" && attribute.Value is not null)
        .SelectMany(attribute => attribute.Value!.Split(';', StringSplitOptions.RemoveEmptyEntries))
        .Select(NormalizeTag)
        .ToDictionary(tag => tag, StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Dictionary<string, string>> Catalogs = LoadCatalogs();
    static readonly Dictionary<string, string> SpecificCultures = new(StringComparer.Ordinal)
    {
        ["en"] = "en-US", ["ja"] = "ja-JP", ["de"] = "de-DE", ["uk"] = "uk-UA",
        ["be"] = "be-BY", ["es"] = "es-ES", ["ru"] = "ru-RU"
    };
    internal static string SystemLanguage { get; } = ResolveSystemLanguage(CultureInfo.CurrentUICulture.Name);
    static string selected = SystemLanguage;

    internal static string Language => Volatile.Read(ref selected);
    internal static CultureInfo Culture => CultureFor(Language);
    internal static bool IsSupported(string? language) => FindLanguage(language) is not null;

    internal static void Select(string? preference) =>
        Volatile.Write(ref selected, FindLanguage(preference) ?? SystemLanguage);

    static string NormalizeTag(string value)
    {
        var tag = value.Replace('_', '-');
        return tag.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : tag;
    }

    static string? FindLanguage(string? value) =>
        value is null ? null : Languages.GetValueOrDefault(NormalizeTag(value));

    internal static string ResolveSystemLanguage(string value)
    {
        var tag = value.Replace('_', '-').ToLowerInvariant();
        for (var candidate = tag; candidate.Length > 0; candidate = candidate[..Math.Max(0, candidate.LastIndexOf('-'))])
            if (FindLanguage(candidate) is { } language)
                return language;

        var parts = tag.Split('-');
        var regionOrScript = parts.Length > 1 ? parts[1] : null;
        return parts[0] switch
        {
            "zh" when regionOrScript is null or "hans" or "cn" or "sg" => "zh-CN",
            "pt" when regionOrScript is null => "pt-BR",
            _ => "en"
        };
    }

    static Dictionary<string, Dictionary<string, string>> LoadCatalogs()
    {
        var catalogs = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in Resources.GetManifestResourceNames())
        {
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal) || !name.EndsWith(".json", StringComparison.Ordinal))
                continue;
            var tag = NormalizeTag(name[ResourcePrefix.Length..^".json".Length]);
            using var stream = Resources.GetManifestResourceStream(name)!;
            catalogs.Add(tag, JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!);
        }
        return catalogs;
    }

    static string? Translate(string language, string source) =>
        Catalogs.GetValueOrDefault(language)?.GetValueOrDefault(source) is { Length: > 0 } value ? value : null;

    static CultureInfo CultureFor(string language)
    {
        try
        {
            return CultureInfo.GetCultureInfo(SpecificCultures.GetValueOrDefault(language, language));
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("en-US");
        }
    }

    internal static string Text(string source, params object?[] args)
    {
        var language = Language;
        var translation = Translate(language, source) ?? (language == "ru" ? null : Translate("en", source))
            ?? Translate("ru", source) ?? source;
        return args.Length == 0 ? translation : string.Format(CultureFor(language), translation, args);
    }

    internal static string HeaderDate(DateTime date, CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.MonthDayPattern.Replace("MMMM", "MMM", StringComparison.Ordinal);
        return date.ToString(pattern, culture);
    }
}

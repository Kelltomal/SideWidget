using System.Globalization;
using System.Threading;

namespace SideWidget.App;

internal static partial class DesktopLanguage
{
    static readonly string[] Languages = ["en", "zh-CN", "ja", "de", "uk", "be", "es", "pt-BR", "ru"];
    static readonly string[] Cultures = ["en-US", "zh-CN", "ja-JP", "de-DE", "uk-UA", "be-BY", "es-ES", "pt-BR", "ru-RU"];
    static readonly string WindowsCulture = CultureInfo.CurrentUICulture.Name;
    internal static string SystemLanguage { get; } = ResolveSystemLanguage(WindowsCulture);
    static int selected = IndexOf(SystemLanguage);

    internal static string Language => Languages[Volatile.Read(ref selected)];
    internal static CultureInfo Culture => CultureInfo.GetCultureInfo(Cultures[Volatile.Read(ref selected)]);

    internal static bool IsSupported(string? language) => IndexOf(language) >= 0;

    internal static void Select(string? preference)
    {
        var index = IndexOf(preference);
        Volatile.Write(ref selected, index >= 0 ? index : IndexOf(SystemLanguage));
    }

    static int IndexOf(string? language) => Array.FindIndex(Languages,
        candidate => string.Equals(candidate, language, StringComparison.OrdinalIgnoreCase));

    static string ResolveSystemLanguage(string windowsCulture)
    {
        var parts = windowsCulture.ToLowerInvariant().Split('-');
        var regionOrScript = parts.Length > 1 ? parts[1] : null;
        return parts[0] switch
        {
            "zh" when regionOrScript is null or "hans" or "cn" or "sg" => "zh-CN",
            "pt" when regionOrScript is null or "br" => "pt-BR",
            var language when IsSupported(language) => language,
            _ => "en"
        };
    }

    internal static string Text(string source, params object?[] args)
    {
        var index = Volatile.Read(ref selected);
        var translation = index < Languages.Length - 1 && Strings.TryGetValue(source, out var entries)
            ? entries[index] : source;
        return args.Length == 0 ? translation : string.Format(CultureInfo.GetCultureInfo(Cultures[index]), translation, args);
    }

    internal static string HeaderDate(DateTime date, CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.MonthDayPattern.Replace("MMMM", "MMM", StringComparison.Ordinal);
        return date.ToString(pattern, culture);
    }
}

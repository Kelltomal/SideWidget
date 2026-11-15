using System.Text.Json;

namespace SideWidget.Core;

public sealed record WebTranslationRequest(string Provider, string Text, string Source, string Target, Uri Uri)
{
    public string Name => Provider == "google" ? "Google" : "Яндекс";
    public static WebTranslationRequest Create(string provider, string text, string source, string target)
    {
        if (provider is not ("google" or "yandex")) throw new CommandFault("provider", "Выберите Google или Яндекс");
        var limit = provider == "google" ? 5000 : 10000;
        if (string.IsNullOrWhiteSpace(text) || text.Length > limit) throw new CommandFault("limit", $"Введите текст до {limit:N0} символов");
        string[] languages = ["ru", "en", "de", "fr", "es", "zh", "ja"];
        if ((source != "auto" && !languages.Contains(source)) || !languages.Contains(target)) throw new CommandFault("language", "Выберите язык перевода");
        var host = provider == "google" ? "translate.google.com" : "translate.yandex.ru";
        var query = provider == "google" ? $"sl={source}&tl={target}&op=translate" : $"source_lang={source}&target_lang={target}";
        return new(provider, text, source, target, new Uri($"https://{host}/?{query}&text={System.Uri.EscapeDataString(text)}"));
    }
    public bool Allows(string address)
    {
        if (!System.Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Port != 443 || uri.UserInfo.Length != 0) return false;
        return Provider == "google" ? uri.Host == "translate.google.com" : uri.Host is "translate.yandex.ru" or "translate.yandex.com";
    }
    public bool AllowsNavigation(string address) => Allows(address) || RequiresVerification(address);
    public bool RequiresVerification(string address)
    {
        if (!System.Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Port != 443 || uri.UserInfo.Length != 0) return false;
        if (Provider == "google")
            return (uri.Host is "translate.google.com" or "www.google.com") && (uri.AbsolutePath == "/sorry" || uri.AbsolutePath.StartsWith("/sorry/", StringComparison.Ordinal));
        return Allows(address) && (uri.AbsolutePath == "/showcaptcha" || uri.AbsolutePath.StartsWith("/showcaptcha/", StringComparison.Ordinal));
    }
    public string? ReadResult(string json)
    {
        if (json.Length > 256000) throw new CommandFault("translation", "Ответ переводчика слишком большой");
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            switch (root.GetProperty("state").GetString())
            {
                case "waiting": return null;
                case "blocked": throw new CommandFault("verification", $"{Name} требует проверку или подтверждение. Попробуйте другой переводчик.");
                case "ready":
                    var text = root.GetProperty("text").GetString();
                    if (string.IsNullOrWhiteSpace(text) || text.Length > 40000) throw new JsonException();
                    return text;
                default: throw new JsonException();
            }
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException)
        { throw new CommandFault("translation", $"Не удалось прочитать результат {Name}. Повторите позже или выберите другой переводчик."); }
    }
}

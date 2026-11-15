using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SideWidget.Core;

namespace SideWidget.Infrastructure;

static class DesktopFolderIcons
{
    public enum Icon
    {
        Document, Finance, Legal, Application, Server, Log, Music, Image, Video, Mixed, Game, Archive
    }
    public enum Style { Classic, Colored, Outline, Modern, Retro, ClosedOutline, ClosedFilled, OpenOutline, OpenFilled }

    public sealed record Request(Icon? Icon);
    public sealed record Settings(byte[]? Contents, FileAttributes Attributes, bool SystemFolder);
    sealed record Asset(byte[] Bytes, string Filename, string DataUrl);
    public static IReadOnlyList<Style> Styles { get; } = Enum.GetValues<Style>();
    public static IReadOnlyDictionary<string, Icon> IconPool { get; } = Enum.GetValues<Icon>().ToDictionary(Label, icon => icon, StringComparer.Ordinal);
    static readonly Lazy<Dictionary<(Icon Icon, Style Style), Asset>> Assets = new(() => Enum.GetValues<Icon>()
        .SelectMany(icon => Styles.Select(style => (Icon: icon, Style: style)))
        .ToDictionary(key => key, key => LoadAsset(key.Icon, key.Style)));

    const string ChangeVerb = @"(?:измени|смени|замени|поменяй|поставь|установи|сделай)";
    const string Please = @"^(?:пожалуйста[ ,]+)?";
    const RegexOptions PromptOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    static readonly Regex SuggestPrompt = new(Please +
        @"(?:" + ChangeVerb + @"|подбери|выбери)\s+(?:значок|значки|иконку|иконки)(?:\s+(?:(?:для|у)\s+)?(?:папки|папок))?(?:\s+на\s+(?:рабочем\s+)?столе)?[.!]?$", PromptOptions);

    static readonly Regex ChangePrompt = new(
        Please + ChangeVerb + @"\s+(?:папке\s+)?(?:значок|иконку)\s+(?:(?:у\s+|для\s+)?(?:выбранной\s+)?папки\s+)?(?:на\s+(?:значок\s+|иконку\s+)?)?(?<icon>.+?)(?:\s+для\s+(?:выбранной\s+)?папки)?[.!]?$", PromptOptions);

    public static Request? ReadRequest(string prompt)
    {
        prompt = prompt.Trim();
        if (DesktopCommandLanguages.ReadIcon(prompt) is { } localized) return localized;
        if (SuggestPrompt.IsMatch(prompt)) return new(Icon: null);
        var match = ChangePrompt.Match(prompt);
        if (!match.Success) return null;
        var name = match.Groups["icon"].Value.Trim(' ', '«', '»', '"', '„', '“', '”').ToLowerInvariant();
        var icon = name switch
        {
            "фото" or "фотографии" or "картинка" or "изображение" or "изображения" => Icon.Image,
            "документ" or "документы" => Icon.Document,
            "финансы" or "счета" => Icon.Finance,
            "право" or "договоры" or "договоры и право" => Icon.Legal,
            "архив" or "архива" or "архивы" or "zip" => Icon.Archive,
            "музыка" or "музыки" or "аудио" => Icon.Music,
            "видео" => Icon.Video,
            "программа" or "программы" or "приложение" or "приложения" => Icon.Application,
            "игра" or "игры" => Icon.Game,
            "сервер" or "серверы" => Icon.Server,
            "лог" or "логи" or "журнал" => Icon.Log,
            _ => throw new CommandFault("desktop", "Выберите значок из списка: фото, документы, финансы, договоры, архив, музыка, видео, программы, игры, сервер или журнал.")
        };
        return new(icon);
    }

    public static string Label(Icon icon) => icon switch
    {
        Icon.Image => "Фото", Icon.Document => "Документы", Icon.Archive => "Архив",
        Icon.Finance => "Финансы", Icon.Legal => "Договоры и право",
        Icon.Music => "Музыка", Icon.Video => "Видео", Icon.Application => "Программы",
        Icon.Server => "Сервер", Icon.Log => "Журнал", Icon.Game => "Игры", _ => "Разные файлы"
    };

    public static Icon Suggest(string folder, IEnumerable<string> files, Func<string, string?> category)
    {
        if (ForCategory(Path.GetFileName(Path.TrimEndingDirectorySeparator(folder))) is { } fromName) return fromName;
        var counts = files.Select(file => Path.GetExtension(file).ToLowerInvariant() is ".mp3" or ".wav" or ".flac" or ".m4a"
                ? Icon.Music : ForCategory(category(file)))
            .OfType<Icon>().GroupBy(icon => icon).Select(group => (Icon: group.Key, Count: group.Count()))
            .OrderByDescending(group => group.Count).Take(2).ToArray();
        return counts.Length == 0 || (counts.Length == 2 && counts[0].Count == counts[1].Count)
            ? Icon.Mixed : counts[0].Icon;
    }

    public static Icon Choose(string folder, string? fileCategory) =>
        ForCategory(folder) ?? ForCategory(fileCategory) ?? Icon.Mixed;

    static Icon? ForCategory(string? category) => category?.ToLowerInvariant() switch
    {
        "документы" or "проекты" or "учёба" => Icon.Document,
        "счета" or "финансы" => Icon.Finance,
        "договоры" or "договоры и право" => Icon.Legal,
        "фото" or "фотографии" or "изображения" or "скриншоты" or "googlephotos" or "google photos" => Icon.Image,
        "музыка" or "аудио" => Icon.Music,
        "видео" or "медиа" => Icon.Video,
        "архив" or "архивы" => Icon.Archive,
        "игры" => Icon.Game,
        "программы" or "скрипты" => Icon.Application,
        "сервер" or "серверы" or "запуски серверов" or "скрипты и серверы" => Icon.Server,
        "логи" => Icon.Log,
        _ => category is null ? null : DesktopCommandLanguages.FolderIconAlias(category)
    };

    public static string StyleLabel(Style style) => style switch
    {
        Style.Classic => "Классическая", Style.Colored => "Цветная", Style.Outline => "Контурная",
        Style.Modern => "Современная", Style.Retro => "Ретро",
        Style.ClosedOutline => "Закрытая контурная", Style.ClosedFilled => "Закрытая залитая",
        Style.OpenOutline => "Открытая контурная", Style.OpenFilled => "Открытая залитая",
        _ => throw new ArgumentOutOfRangeException(nameof(style))
    };

    public static string[] PreviewImages(Icon icon) => Styles.Select(style => Assets.Value[(icon, style)].DataUrl).ToArray();

    public static void Apply(string folder, Icon icon, string profileDirectory, Style style = Style.Classic)
    {
        var settings = Path.Combine(folder, "desktop.ini");
        if (Path.Exists(settings)) return;
        var resource = IconResource(icon, style, profileDirectory);
        using (var stream = new FileStream(settings, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, Encoding.Unicode))
        {
            writer.Write($"[.ShellClassInfo]\r\nIconResource={resource}\r\n");
        }
        File.SetAttributes(settings, File.GetAttributes(settings) | FileAttributes.Hidden | FileAttributes.System);
        RefreshFolder(folder);
    }

    public static Settings ReadSettings(string folder)
    {
        var settings = Path.Combine(folder, "desktop.ini");
        var system = (File.GetAttributes(folder) & FileAttributes.System) != 0;
        if (!Path.Exists(settings)) return new(null, 0, system);
        var attributes = File.GetAttributes(settings);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new IOException("Unsafe desktop.ini");
        return new(File.ReadAllBytes(settings), attributes, system);
    }

    public static Settings PrepareSettings(string folder, Settings original, Icon icon, string profileDirectory, Style style)
    {
        var resource = IconResource(icon, style, profileDirectory);
        var attributes = (original.Attributes & ~FileAttributes.Normal) | FileAttributes.Hidden | FileAttributes.System | FileAttributes.Archive;
        if (original.Contents is null)
            return new([..Encoding.Unicode.GetPreamble(), ..Encoding.Unicode.GetBytes($"[.ShellClassInfo]\r\nIconResource={resource}\r\n")], attributes, true);
        var fallback = CodePagesEncodingProvider.Instance.GetEncoding(0) ?? Encoding.UTF8;
        using var reader = new StreamReader(new MemoryStream(original.Contents), fallback, detectEncodingFromByteOrderMarks: true);
        var staged = Path.Combine(folder, $".sidewidget-{Guid.NewGuid():N}.ini");
        try
        {
            using (var file = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file, Encoding.Unicode)) writer.Write(reader.ReadToEnd());

            SetIconSetting(staged, "IconFile", null);
            SetIconSetting(staged, "IconIndex", null);
            SetIconSetting(staged, "IconResource", resource);
            return new(File.ReadAllBytes(staged), attributes, true);
        }
        finally { File.Delete(staged); }
    }

    public static bool SameContents(Settings first, Settings second) => first.Contents is null
        ? second.Contents is null : second.Contents is not null && first.Contents.AsSpan().SequenceEqual(second.Contents);

    public static bool SameSettings(Settings first, Settings second) => SameContents(first, second) &&
        first.SystemFolder == second.SystemFolder && first.Attributes == second.Attributes;

    public static void WriteSettings(string folder, Settings expected, Settings target)
    {
        if (!SameContents(ReadSettings(folder), expected)) throw new IOException("Folder icon settings changed");
        var path = Path.Combine(folder, "desktop.ini");
        if (target.Contents is null) File.Delete(path);
        else
        {
            var staged = Path.Combine(folder, $".sidewidget-{Guid.NewGuid():N}.ini");
            try
            {
                using (var file = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    file.Write(target.Contents);
                if (expected.Contents is null) File.Move(staged, path);
                else File.Replace(staged, path, null);
                File.SetAttributes(path, target.Attributes);
            }
            finally { File.Delete(staged); }
        }
        RefreshFolder(folder, target.SystemFolder);
    }

    static void SetIconSetting(string path, string key, string? value)
    {
        if (!WritePrivateProfileStringW(".ShellClassInfo", key, value, path))
            throw new IOException("Could not update desktop.ini");
    }

    static byte[] ReadAsset(string filename)
    {
        using var resource = typeof(DesktopFolderIcons).Assembly.GetManifestResourceStream($"FolderIcons/{filename}")
            ?? throw new IOException($"Folder icon {filename} is missing");
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        return buffer.ToArray();
    }

    static Asset LoadAsset(Icon icon, Style style)
    {
        var name = style == Style.Classic ? icon.ToString() : $"{icon}-{style}";
        var bytes = ReadAsset($"{name}.ico");
        var version = Convert.ToHexString(SHA256.HashData(bytes))[..16];
        var image = Convert.ToBase64String(ReadAsset($"{name}.png"));
        return new(bytes, $"{name}-{version}.ico", "data:image/png;base64," + image);
    }

    static string IconResource(Icon icon, Style style, string profileDirectory)
    {
        var asset = Assets.Value[(icon, style)];
        var directory = Directory.CreateDirectory(Path.Combine(profileDirectory, "FolderIcons"));
        var path = Path.Combine(directory.FullName, asset.Filename);
        if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(asset.Bytes))
        {
            var staged = Path.Combine(directory.FullName, $"{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    stream.Write(asset.Bytes);
                File.Move(staged, path, overwrite: true);
            }
            finally { File.Delete(staged); }
        }
        return $"{path},0";
    }

    static void RefreshFolder(string folder, bool system = true)
    {
        var attributes = File.GetAttributes(folder);
        File.SetAttributes(folder, system ? attributes | FileAttributes.System : attributes & ~FileAttributes.System);
        SHChangeNotify(0x2000, 5, folder, IntPtr.Zero); // SHCNE_UPDATEITEM, SHCNF_PATHW
    }

    [DllImport("shell32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    static extern void SHChangeNotify(int eventId, uint flags, string item, IntPtr otherItem);

    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool WritePrivateProfileStringW(string section, string key, string? value, string path);
}

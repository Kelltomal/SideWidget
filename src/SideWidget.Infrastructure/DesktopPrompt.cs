using System.Text.RegularExpressions;
using SideWidget.Core;

namespace SideWidget.Infrastructure;

sealed class DesktopPrompt
{
    const string Move = @"(?:разложи(?:ть)?|разбери|рассортируй|сортируй|сгруппируй|распредели|разнеси|перенеси|перемести|помести(?:ть)?|убери|сложи|закинь|скинь|сложить)";
    const string Keep = @"(?:оставь|оставить|оставляй|не\s+трогай|не\s+переноси|не\s+перемещай)";
    const string Where = @"(?:\s+на\s+(?:рабочем\s+)?столе|\s+на\s+месте)?";
    const string Subject = @"(?<subject>сч[её]т(?:а|ов|ы)?(?:\s+на\s+оплату)?|инвойс(?:ы|ов)?|скрин(?:шот)?(?:ы|ов)?|снимки\s+экрана|документ(?:ы|ов)?|фото(?:графии)?|изображени[яй]|архив(?:ы|ов)?|лог(?:и|ов)?|видео|медиа|игр[ыа]|программ[ыа]|проги|приложени[яе]|батник(?:и|ов)?|скрипт(?:ы|ов)?|запуск[и]?\s+сервер(?:а|ов)|все\s+файлы|файлы)";
    const string Subjects = Subject + @"(?:\s+и\s+" + Subject + ")*";
    static readonly string SubjectPattern = Subject.Replace("(?<subject>", "(?:", StringComparison.Ordinal);
    const string Folder = @"(?<folder>[^«»""<>/\\:|?*]{1,64})";
    const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    static readonly Regex Year = new(@"(?<!\d)(?:19|20)\d{2}(?!\d)");
    static readonly Lazy<FastTextModel?> IntentModel = new(() => FastTextModel.Load("desktop-prompt-intent"));
    static readonly Lazy<FastTextModel?> SubjectModel = new(() => FastTextModel.Load("desktop-prompt-subject"));
    static readonly Regex ExplicitFolder = new(@"\bв\s+(?:папку|папке|каталог|каталоге)\s+(?:«(?<name>[^»]+)»|""(?<name>[^""]+)""|(?<name>[^«»""]+)$)", Options);
    static readonly Regex SeparateFolder = new(@"\b(?:отдельн\p{L}*\s+(?:папк\p{L}*|каталог\p{L}*)|(?:папк\p{L}*|каталог\p{L}*)\s+отдельн\p{L}*)\b", Options);
    static readonly Regex TopicGrouping = new(@"\bпо\s+(?:темам|тематике|смыслу|содержанию)\b", Options);
    static readonly Regex KeepUnclear = new("^(?:" + Keep + @"\s+(?:(?:вс[её]\s+)?непонятное|сомнительные(?:\s+(?:файлы|документы))?)" + Where +
        @"|(?:(?:вс[её]\s+)?непонятное|сомнительные(?:\s+(?:файлы|документы))?)\s+" + Keep + Where + ")$", Options);

    internal sealed record Instruction(string Subject, bool Keep, bool ByYear, string? Folder, bool ByTopic = false, bool ByType = false);
    readonly List<Instruction> instructions = [];
    public bool IncludesLaunchFiles => instructions.Any(rule => rule.Subject is "Игры" or "Программы" or "Скрипты" or "Запуски серверов");

    internal DesktopPrompt(IEnumerable<Instruction> rules) => instructions.AddRange(rules);

    public DesktopPrompt(string text)
    {
        if (DesktopCommandLanguages.ReadSorting(text) is { } translatedRules)
        {
            instructions.AddRange(translatedRules);
            return;
        }
        var quoted = new List<string>();
        var masked = Regex.Replace(text.Trim(), "«[^»]*»|\"[^\"]*\"", match =>
        {
            quoted.Add(match.Value);
            return "\uE000" + (quoted.Count - 1) + "\uE001";
        });
        var clauses = Regex.Split(masked, @"[;.!?\r\n]+\s*|,\s*|\s+(?:а|но|при\s+этом)\s+|\s+и\s+(?=(?:" + Move + "|" + Keep + "|" + SubjectPattern + @"\s+(?:" + Move + "|" + Keep + @")|удали|удалить|переименуй|скопируй|отправь|загрузи)\b)", Options);
        var keepUnclear = false;
        foreach (var part in clauses)
        {
            var clause = Regex.Replace(part.Trim(), @"^(?:а|и|но|пожалуйста)\s+", "", Options);
            for (var index = 0; index < quoted.Count; index++) clause = clause.Replace("\uE000" + index + "\uE001", quoted[index], StringComparison.Ordinal);
            if (clause.Length == 0 || clause.Equals("пожалуйста", StringComparison.OrdinalIgnoreCase)) continue;
            if (KeepUnclear.IsMatch(clause))
            {
                keepUnclear = true;
                continue;
            }
            var group = Regex.Match(clause,
                "^(?:" + Move + @"\s+" + Subjects +
                @"\s+по\s+(?:типам|типу|категориям|видам)(?:\s+файлов)?|" + Move + @"\s+" + Subjects +
                @"\s+по\s+(?<topic>темам|тематике|содержанию)|(?:разложи|разбери|рассортируй|сортируй|сгруппируй)\s+" + Subjects + ")$", Options);
            if (group.Success)
            {
                var topic = group.Groups["topic"].Success;
                var subjectsToGroup = group.Groups["subject"].Captures.Select(capture => SubjectKey(capture.Value)).Distinct().ToArray();
                if (topic && subjectsToGroup.Any(subject => subject != "Документы")) throw Unknown();
                foreach (var subject in subjectsToGroup)
                    instructions.Add(new(subject, false, false, null, topic, !topic));
                continue;
            }
            if (TryUnderstand(clause) is { } learned)
            {
                instructions.Add(learned);
                continue;
            }
            var keep = Regex.Match(clause, "^(?:" + Keep + @"\s+" + Subjects + Where + "|" + Subjects + @"\s+" + Keep + Where + ")$", Options);
            if (keep.Success)
            {
                foreach (Capture subject in keep.Groups["subject"].Captures)
                    instructions.Add(new(SubjectKey(subject.Value), true, false, null));
                continue;
            }
            var move = Regex.Match(clause, "^(?:" + Move + @"\s+" + Subjects + "|" + Subjects + @"(?:\s+" + Move + @")?)\s+(?:по\s+(?<year>годам|году)|в\s+(?:(?<separate>отдельную\s+папку)|(?:папку\s+)?[«""]?" + Folder + "[»\"]?))$", Options);
            if (!move.Success) throw Unknown();
            var destination = move.Groups["folder"].Success ? move.Groups["folder"].Value.Trim() : null;
            var subjects = move.Groups["subject"].Captures.Select(capture => SubjectKey(capture.Value)).Distinct().ToArray();
            if (move.Groups["separate"].Success)
                destination = subjects.Order().SequenceEqual(new[] { "Запуски серверов", "Скрипты" })
                    ? "Скрипты и серверы" : string.Join(" и ", subjects);
            if (destination is not null && !DesktopClassifier.ValidFolder(destination)) throw Unknown();
            foreach (var subject in subjects)
                instructions.Add(new(subject, false, move.Groups["year"].Success, destination));
        }
        if (instructions.Count == 0 || (keepUnclear && !instructions.Any(rule => rule.ByTopic))) throw Unknown();
    }

    static Instruction? TryUnderstand(string clause)
    {
        var folder = ExplicitFolder.Match(clause);
        var destination = folder.Success ? folder.Groups["name"].Value.Trim() : null;
        if (destination is not null && !DesktopClassifier.ValidFolder(destination)) throw Unknown();
        var wording = folder.Success ? clause.Remove(folder.Groups["name"].Index, folder.Groups["name"].Length) : clause;
        if (Regex.IsMatch(wording, @"\b(?:удал\p{L}*|переимен\p{L}*|скопиру\p{L}*|запусти|загрузи|отправь\s+(?:по|на)|не\s+(?:надо|нужно|стоит|хочу|следует|разбира\p{L}*|сортиру\p{L}*|раскладыва\p{L}*|группиру\p{L}*))\b", Options))
            throw Unknown();

        var subjectMatches = Regex.Matches(wording, @"(?<!\p{L})" + Subject + @"(?!\p{L})", Options)
            .Select(match => SubjectKey(match.Groups["subject"].Value)).Distinct().ToArray();
        if (subjectMatches.Length > 1) return null; // The existing parser handles explicit lists.
        if (IntentModel.Value?.Predict(wording) is not { Score: >= .72 } intent ||
            SubjectModel.Value?.Predict(wording) is not { Score: >= .74 } subject || subject.Label == "unknown") return null;

        var label = subject.Label.Replace('_', ' ');
        if (subjectMatches.Length == 1 && subjectMatches[0] != label && subjectMatches[0] != "Файлы") return null;
        var negative = Regex.IsMatch(wording, @"\bне\s+\p{L}+", Options);
        if (intent.Label != "keep" && negative) return null;
        if (TopicGrouping.IsMatch(wording))
            return intent.Label == "topic" && label == "Документы" && destination is null
                ? new(label, false, false, null, ByTopic: true) : null;
        return intent.Label switch
        {
            "keep" when destination is null => new(label, true, false, null),
            "year" when destination is null && !Regex.IsMatch(wording, @"\bотправ\p{L}*\b", Options) => new(label, false, true, null),
            "move" when destination is not null => new(label, false, false, destination),
            "move" when SeparateFolder.IsMatch(wording) => new(label, false, false, label),
            _ => null
        };
    }

    public (string Folder, bool Selected, bool Selectable, string? Reason) Suggest(
        FileInfo file, string fallback, string content)
    {
        var name = Path.GetFileNameWithoutExtension(file.Name);
        var matching = instructions.Where(rule => Matches(rule.Subject, file.Extension, name, content, fallback)).ToArray();
        if (matching.Any(rule => rule.Keep)) return ("На рабочем столе", false, false, "Оставить по запросу");
        if (matching.Length == 0) return ("На рабочем столе", false, false, "Вне запроса");
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in matching)
        {
            if (rule.ByTopic)
            {
                if (DesktopClassifier.PredictTopic(name, content) is not { Selected: true } topic)
                    return ("На рабочем столе", false, false, "Тема не определена уверенно");
                destinations.Add(topic.Folder);
                continue;
            }
            if (rule.ByType)
            {
                destinations.Add(fallback);
                continue;
            }
            if (!rule.ByYear)
            {
                destinations.Add(rule.Folder!);
                continue;
            }
            var years = Years(name);
            if (years.Length == 0) years = Years(content);
            if (years.Length != 1) return ("На рабочем столе", false, false,
                years.Length == 0 ? "Год не найден" : "Найдено несколько лет");
            destinations.Add((rule.Subject is "Счета" or "Скриншоты" or "Видео" ? rule.Subject : fallback) + "/" + years[0]);
        }
        if (destinations.Count != 1) return ("На рабочем столе", false, false, "Подходят разные папки");
        var launchFile = LaunchCategory(file.Name) is not null;
        return (destinations.Single(), !launchFile, true, launchFile ? "Проверьте перед переносом" : null);
    }

    public DesktopCleanup.PromptSummary Summary(IReadOnlyList<DesktopCleanup.FileRow> rows)
    {
        var steps = instructions.Distinct().Select(rule => new DesktopCleanup.PromptStep(rule.Subject,
            rule.Keep ? "keep" : rule.ByTopic ? "topic" : rule.ByType ? "type" : rule.ByYear ? "year" : "move", rule.Folder)).ToArray();
        var selected = rows.Count(row => row.Selected);
        var unclear = rows.Count(row => !row.Selected && row.Reason is "Год не найден" or "Найдено несколько лет" or "Подходят разные папки" or "Проверьте перед переносом" or "Тема не определена уверенно");
        return new(steps, selected, unclear);
    }

    public static string Answer(DesktopCleanup.PromptSummary summary)
    {
        var plan = summary.Steps.Select(step => step.Mode switch
        {
            "keep" => $"{step.Subject} останутся на рабочем столе.",
            "topic" => "Документы разложу по темам. Файлы с неясной темой останутся на столе.",
            "type" => $"{step.Subject} разложу по типам.",
            "year" => $"{step.Subject} разложу по годам.",
            _ => $"{step.Subject} перенесу в «{step.Folder}»."
        });
        return string.Join(' ', plan) + (summary.Selected > 0 ? $" Отмечено для переноса — {summary.Selected}." : " Для переноса пока ничего не отмечено.") +
            (summary.Unclear > 0 ? $" Требуют проверки — {summary.Unclear}." : "");
    }

    static string[] Years(string text) => Year.Matches(text).Select(match => match.Value).Distinct().ToArray();

    static string SubjectKey(string subject)
    {
        subject = subject.ToLowerInvariant().Replace('ё', 'е');
        if (subject.StartsWith("счет") || subject.StartsWith("инвойс")) return "Счета";
        if (subject.StartsWith("скрин") || subject.StartsWith("снимки")) return "Скриншоты";
        if (subject.StartsWith("документ")) return "Документы";
        if (subject.StartsWith("фото") || subject.StartsWith("изображени")) return "Изображения";
        if (subject.StartsWith("архив")) return "Архивы";
        if (subject.StartsWith("лог")) return "Логи";
        if (subject.StartsWith("игр")) return "Игры";
        if (subject.StartsWith("программ") || subject.StartsWith("приложени") || subject == "проги") return "Программы";
        if (subject.StartsWith("батник") || subject.StartsWith("скрипт")) return "Скрипты";
        if (subject.StartsWith("запуск")) return "Запуски серверов";
        return subject switch { "видео" => "Видео", "медиа" => "Медиа", _ => "Файлы" };
    }

    static bool Matches(string subject, string extension, string name, string content, string fallback) => subject switch
    {
        "Файлы" => fallback is not ("Игры" or "Программы" or "Скрипты" or "Запуски серверов"),
        "Счета" => fallback == "Документы" && (Regex.IsMatch(name + " " + content,
            @"(?<!\p{L})(?:сч[её]т(?:а|у|ом|ов|ы)?|invoice(?:s)?|инвойс(?:ы|а)?)(?!\p{L})", Options) ||
            DesktopCommandLanguages.ContainsSubject(subject, name + " " + content)),
        "Скриншоты" => fallback == "Изображения" && (Regex.IsMatch(name,
            @"(?<!\p{L})(?:скриншот(?:ы)?|скрин|снимок[\s_-]+экрана|screen[\s_-]*shot(?:s)?|screen[\s_-]*capture)(?!\p{L})", Options) ||
            DesktopCommandLanguages.ContainsSubject(subject, name)),
        "Видео" => extension.ToLowerInvariant() is ".mp4" or ".mov" or ".mkv" or ".webm",
        _ => subject == fallback
    };

    public static string? LaunchCategory(string name, string? target = null)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension is ".bat" or ".cmd" or ".ps1") return "Скрипты";
        if (extension is not (".lnk" or ".exe" or ".msi")) return null;
        var hints = Path.GetFileNameWithoutExtension(name) + " " + target;
        if (Regex.IsMatch(hints, @"(?<!\p{L})(?:ssh|server|сервер\p{L}*)(?!\p{L})|\.(?:bat|cmd|ps1)(?!\p{L})", Options))
            return "Запуски серверов";
        return Regex.IsMatch(hints, @"(?<!\p{L})(?:game(?:s)?|игр[аы]|steam|steamapps|minecraft|mcskill|roblox|gta|fallout)(?!\p{L})", Options)
            ? "Игры" : "Программы";
    }

    internal static CommandFault Unknown() => new("desktop", "Не удалось разобрать запрос целиком.");
}

using System.Text.RegularExpressions;

namespace SideWidget.Infrastructure;

static partial class DesktopCommandLanguages
{
    const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    static readonly Regex QuotedFolder = new("""«[^»]*»|"[^"]*"|„[^“]*“|“[^”]*”|‘[^’]*’|(?<!\p{L})'[^'\r\n]*'|「[^」]*」|『[^』]*』|《[^》]*》""");
    const string Folder = """(?<folder>\uE000[0-9]+\uE001|[\p{L}\p{M}\p{N}_ -]{1,64})""";
    static readonly string[] Subjects =
        ["Счета", "Скриншоты", "Документы", "Изображения", "Архивы", "Логи", "Видео", "Медиа",
         "Игры", "Программы", "Скрипты", "Запуски серверов", "Файлы"];
    static readonly DesktopFolderIcons.Icon[] Icons =
        [DesktopFolderIcons.Icon.Image, DesktopFolderIcons.Icon.Document, DesktopFolderIcons.Icon.Finance,
         DesktopFolderIcons.Icon.Legal, DesktopFolderIcons.Icon.Archive, DesktopFolderIcons.Icon.Music,
         DesktopFolderIcons.Icon.Video, DesktopFolderIcons.Icon.Application, DesktopFolderIcons.Icon.Game,
         DesktopFolderIcons.Icon.Server, DesktopFolderIcons.Icon.Log, DesktopFolderIcons.Icon.Mixed];
    static readonly Lazy<Grammar[]> Grammars = new(() => Specs().Select(spec => new Grammar(spec)).ToArray());
    static readonly Lazy<Dictionary<string, Regex>> SubjectTerms = new(() =>
        Subjects.Select((subject, index) => (subject, index))
            .Where(item => item.subject is "Счета" or "Скриншоты")
            .ToDictionary(item => item.subject, item => TermPattern(Specs()
                .SelectMany(spec => spec.Subjects.Split('|')[item.index].Split(',')).Distinct())));

    sealed record Spec(string Subjects, string Icons, string Join, string Article, string Please, string Link,
        string Sort, string Keep, string AutoIcon, string ChangeIcon);

    sealed class Grammar
    {
        readonly Dictionary<string, string> subjects;
        readonly Dictionary<string, DesktopFolderIcons.Icon> icons;
        readonly Regex split, sort, keep, autoIcon, changeIcon, politePrefix, joinWord;

        public Grammar(Spec spec)
        {
            subjects = Aliases(spec.Subjects, Subjects);
            icons = Aliases(spec.Icons, Icons);
            var subjectAlternatives = Alternatives(subjects.Keys);
            var subject = $"{spec.Article}(?<subject>{subjectAlternatives})";
            var subjectList = $@"{subject}(?:\s*(?:{spec.Join})\s*{subject})*";
            var icon = $"""["«„“‘「『]?(?<icon>{Alternatives(icons.Keys)})["»“”’」』]?""";
            var link = spec.Link.Replace("{subject-start}", $"(?:{subjectAlternatives})");
            split = new Regex($@"[,.!?;，。！？；、\r\n]+|{link}", Options);
            joinWord = new Regex($@"(?<!\p{{L}})(?:{spec.Join})(?!\p{{L}})", Options);
            politePrefix = new Regex($@"\A(?:{spec.Please})", Options);
            sort = Command(spec.Sort.Replace("{subjects}", subjectList).Replace("{folder}", Folder), spec.Please);
            keep = Command(spec.Keep.Replace("{subjects}", subjectList), spec.Please);
            autoIcon = Command(spec.AutoIcon, spec.Please);
            changeIcon = Command(spec.ChangeIcon.Replace("{icon}", icon), spec.Please);
        }

        public DesktopFolderIcons.Request? ReadIcon(string wording)
        {
            if (autoIcon.IsMatch(wording)) return new(null);
            var match = changeIcon.Match(wording);
            return match.Success ? new(icons[Normalize(match.Groups["icon"].Value)]) : null;
        }

        public DesktopFolderIcons.Icon? IconAlias(string name) =>
            icons.TryGetValue(Normalize(name), out var icon) ? icon : null;

        public (List<DesktopPrompt.Instruction>? Rules, bool Partial) Sort(string text, IReadOnlyDictionary<string, string> quoted)
        {
            var rules = new List<DesktopPrompt.Instruction>();
            var unknown = false;
            foreach (var part in split.Split(politePrefix.Replace(text, "", 1)))
            {
                var clause = part.Trim();
                if (clause.Length == 0) continue;
                var match = keep.Match(clause);
                var leave = match.Success;
                if (!leave) match = sort.Match(clause);
                if (!match.Success)
                {
                    unknown = true;
                    continue;
                }

                var categoryKeys = match.Groups["subject"].Captures.Select(x => subjects[Normalize(x.Value)]).Distinct().ToArray();
                var folder = match.Groups["folder"].Success ? match.Groups["folder"].Value.Trim() : null;
                if (folder?.StartsWith('\uE000') == true)
                    folder = quoted.TryGetValue(folder, out var name) ? name : throw DesktopPrompt.Unknown();
                else if (folder is not null && joinWord.IsMatch(folder)) throw DesktopPrompt.Unknown();
                var topic = match.Groups["topic"].Success;
                if ((folder is not null && !DesktopClassifier.ValidFolder(folder)) ||
                    (topic && categoryKeys.Any(key => key != "Документы"))) throw DesktopPrompt.Unknown();

                foreach (var category in categoryKeys)
                    rules.Add(new(category, leave, match.Groups["year"].Success,
                        match.Groups["separate"].Success ? category : folder, topic, match.Groups["type"].Success));
            }
            return unknown || rules.Count == 0 ? (null, rules.Count > 0) : (rules, false);
        }

        static Regex Command(string pattern, string please) =>
            new($@"\A(?:{please})?(?:{pattern})[.!。！]?\z", Options);
    }

    internal static IReadOnlyList<DesktopPrompt.Instruction>? ReadSorting(string wording)
    {
        var quoted = new Dictionary<string, string>();
        var masked = QuotedFolder.Replace(wording.Trim(), match =>
        {
            var key = $"\uE000{quoted.Count}\uE001";
            quoted.Add(key, match.Value[1..^1]);
            return key;
        });
        var partial = false;
        foreach (var grammar in Grammars.Value)
        {
            var result = grammar.Sort(masked, quoted);
            if (result.Rules is { } rules) return rules;
            partial |= result.Partial;
        }
        return partial ? throw DesktopPrompt.Unknown() : null;
    }

    internal static DesktopFolderIcons.Request? ReadIcon(string wording)
    {
        foreach (var grammar in Grammars.Value)
            if (grammar.ReadIcon(wording) is { } icon) return icon;
        return null;
    }

    internal static DesktopFolderIcons.Icon? FolderIconAlias(string name)
    {
        foreach (var grammar in Grammars.Value)
            if (grammar.IconAlias(name) is { } icon) return icon;
        return null;
    }

    internal static bool ContainsSubject(string name, string text) =>
        SubjectTerms.Value.TryGetValue(name, out var terms) && terms.IsMatch(text.Replace('_', ' ').Replace('-', ' '));

    static Dictionary<string, T> Aliases<T>(string vocabulary, T[] meanings)
    {
        var groups = vocabulary.Split('|');
        if (groups.Length != meanings.Length) throw new InvalidOperationException("Invalid desktop command vocabulary.");
        return groups.SelectMany((group, index) => group.Split(',', StringSplitOptions.TrimEntries)
                .Select(alias => (Alias: Normalize(alias), Meaning: meanings[index])))
            .ToDictionary(alias => alias.Alias, alias => alias.Meaning, StringComparer.OrdinalIgnoreCase);
    }

    static string Normalize(string text) => Regex.Replace(text, @"\s+", " ");
    static string Alternatives(IEnumerable<string> options) => string.Join('|', options.OrderByDescending(x => x.Length)
        .Select(x => Regex.Escape(x).Replace(@"\ ", @"\s+", StringComparison.Ordinal)));

    static Regex TermPattern(IEnumerable<string> terms)
    {
        static bool EastAsian(string term) => term[0] is >= '\u3040' and <= '\u30ff' or >= '\u3400' and <= '\u9fff';
        var eastAsian = terms.Where(EastAsian);
        var other = terms.Where(term => !EastAsian(term));
        return new Regex($@"(?<!\p{{L}})(?:{Alternatives(other)})(?!\p{{L}})|{Alternatives(eastAsian)}", Options);
    }
}

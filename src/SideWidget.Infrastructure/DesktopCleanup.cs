using SideWidget.Core;

namespace SideWidget.Infrastructure;

public sealed class DesktopCleanup(Store store, string desktop, Func<string, string?>? shortcutTarget = null,
    string? commonDesktop = null)
{
    const string UndoKey = "desktopCleanup.lastMove";
    const string IconUndoKey = "desktopCleanup.lastIconChange";
    const int PageSize = 200;
    const FileAttributes Excluded = FileAttributes.Directory | FileAttributes.Hidden |
        FileAttributes.System | FileAttributes.ReparsePoint;
    readonly object gate = new();
    readonly Dictionary<Guid, Plan> previews = new();
    readonly HashSet<Guid> choosingFolders = [];

    public string Folder => Path.Combine(desktop, "Разобранное");
    string DocumentsFolder => Path.Combine(Folder, "Документы");

    public const int PromptLimit = DesktopClassifier.PromptLimit;
    public sealed record PromptStep(string Subject, string Mode, string? Folder);
    public sealed record PromptSummary(PromptStep[] Steps, int Selected, int Unclear);
    public sealed record FileRow(int Id, string Name, string Category, bool Selected, bool Selectable = true, string? Reason = null, bool Shared = false);
    public sealed record PreviewResult(Guid Id, FileRow[] Files, bool CanUndo, bool HasMore, bool HasFolder, bool ModelAvailable, string Answer,
        string[]? Topics = null, string? FolderIcon = null, bool SuggestFolderIcon = false, bool CanUndoIcons = false,
        PromptSummary? Summary = null);
    public sealed record Issue(string Name, string Message);
    public sealed record Result(int Count, Issue[] Issues);
    public sealed record IconResult(int Count, Issue[] Issues, bool CanUndoIcons);
    public sealed record FolderIconRow(int Id, string Name, string Icon);
    public sealed record FolderIconStyle(int Id, string Name, bool LightPreview);
    public sealed record FolderIconPreview(Guid Id, FolderIconRow[] Folders, bool HasMore,
        IReadOnlyDictionary<string, string[]> IconImages, FolderIconStyle[] Styles);
    sealed record Entry(string Name, long Length, DateTime Created, DateTime Modified, string? Folder = null, bool Shared = false, bool ArchivedDocument = false);
    sealed record FolderIconEntry(string Name, DateTime Created, DesktopFolderIcons.Icon Icon);
    sealed record IconChange(string Folder, DateTime Created, DesktopFolderIcons.Settings Before, DesktopFolderIcons.Settings After)
    {
        public string Name => Path.GetFileName(Folder) is { Length: > 0 } name ? name : Folder;
    }
    sealed record IconHistory(IconChange[] Changes, IconChange[]? Previous = null);
    sealed record Plan(Entry[] Files, DesktopFolderIcons.Request? Icon = null, FolderIconEntry[]? FolderIcons = null);
    const string ExcludedFromPreview = "";
    sealed record History(string Desktop, Entry[] Files, Entry[]? Previous = null, string? CommonDesktop = null);

    bool HasCommonDesktop => !string.IsNullOrWhiteSpace(commonDesktop) &&
        !string.Equals(Path.GetFullPath(desktop), Path.GetFullPath(commonDesktop), StringComparison.OrdinalIgnoreCase);

    public PreviewResult Preview(string prompt = "")
    {
        if (prompt.Length > PromptLimit) throw new CommandFault("desktop", "Сократите запрос до 2000 символов.");
        lock (gate)
        {
            if (DesktopFolderIcons.ReadRequest(prompt) is { } icon)
            {
                var iconId = Guid.NewGuid();
                RememberPreview(iconId, new([], icon));
                return new(iconId, [], LastMove().Length > 0, false, Directory.Exists(Folder), true, "",
                    FolderIcon: icon.Icon is { } specified ? DesktopFolderIcons.Label(specified) : null, SuggestFolderIcon: icon.Icon is null,
                    CanUndoIcons: CanUndoFolderIcons);
            }
            if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop))
                throw new CommandFault("desktop", "Папка рабочего стола недоступна.");
            var natural = string.IsNullOrWhiteSpace(prompt) || prompt.Contains(':') ? null : new DesktopPrompt(prompt);
            var classifier = natural is null ? new DesktopClassifier(prompt) : null;
            var personal = new DirectoryInfo(desktop).EnumerateFiles().Select(file => (File: file, Shared: false));
            var shared = natural?.IncludesLaunchFiles == true && HasCommonDesktop && Directory.Exists(commonDesktop)
                ? new DirectoryInfo(commonDesktop!).EnumerateFiles().Select(file => (File: file, Shared: true)) : [];
            var files = personal.Concat(shared).Where(item => IsRegular(item.File))
                .Select(item => (item.File, item.Shared, Category: (item.Shared ? null : Category(item.File.Name)) ??
                    (natural?.IncludesLaunchFiles == true ? DesktopPrompt.LaunchCategory(item.File.Name, ReadShortcutTarget(item.File)) : null)))
                .Where(item => item.Category is not null)
                .OrderBy(item => item.Category, StringComparer.Ordinal)
                .ThenBy(item => item.File.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Shared)
                .Take(PageSize + 1).ToArray();
            var id = Guid.NewGuid();
            var entries = new List<Entry>();
            var rows = new List<FileRow>();
            foreach (var (file, sharedFile, category) in files.Take(PageSize))
            {
                var entry = Capture(file);
                var fallback = category!;
                var suggestion = natural is null
                    ? FromClassifier(classifier!, file, fallback)
                    : natural.Suggest(file, fallback, DesktopClassifier.ReadSample(file));
                var reason = sharedFile ? $"Общий рабочий стол. {suggestion.Reason}".TrimEnd() : suggestion.Reason;
                rows.Add(new(entries.Count, entry.Name, suggestion.Folder, suggestion.Selected && !sharedFile, suggestion.Selectable, reason, sharedFile));
                entries.Add(entry with { Folder = suggestion.Selectable ? suggestion.Folder : ExcludedFromPreview, Shared = sharedFile });
            }
            RememberPreview(id, new([..entries]));
            var summary = natural?.Summary(rows);
            return new(id, [..rows], LastMove().Length > 0, files.Length > PageSize,
                Directory.Exists(Folder), classifier?.ModelAvailable ?? true,
                summary is not null ? DesktopPrompt.Answer(summary) : string.IsNullOrWhiteSpace(prompt) ? "" : $"Применил правила. К переносу предложено файлов — {rows.Count(row => row.Selected)}.",
                CanUndoIcons: CanUndoFolderIcons, Summary: summary);
        }
    }

    public PreviewResult PreviewDocuments()
    {
        lock (gate)
        {
            if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop))
                throw new CommandFault("desktop", "Папка рабочего стола недоступна.");
            FileInfo[] files = [];
            if (Path.Exists(DocumentsFolder))
            {
                CheckDocumentsFolder();
                files = new DirectoryInfo(DocumentsFolder).EnumerateFiles()
                    .Where(file => Category(file.Name) == "Документы" && IsRegular(file))
                    .OrderBy(file => file.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Take(PageSize + 1).ToArray();
            }
            var classifier = new DesktopClassifier("");
            var rows = new List<FileRow>();
            var entries = new List<Entry>();
            foreach (var file in files.Take(PageSize))
            {
                var entry = Capture(file);
                var topic = DesktopClassifier.PredictTopic(Path.GetFileNameWithoutExtension(file.Name), DesktopClassifier.ReadSample(file));
                var destination = topic is { Selected: true } ? topic.Value.Folder : ExcludedFromPreview;
                entries.Add(entry with { Folder = destination, ArchivedDocument = true });
                rows.Add(new(rows.Count, entry.Name, destination, destination.Length > 0, true,
                    destination.Length > 0 ? null : "Выберите тему вручную"));
            }
            var id = Guid.NewGuid();
            RememberPreview(id, new([..entries]));
            return new(id, [..rows], LastMove().Length > 0, files.Length > PageSize, Directory.Exists(Folder),
                classifier.ModelAvailable, "", DesktopClassifier.Topics, CanUndoIcons: CanUndoFolderIcons);
        }
    }

    void RememberPreview(Guid id, Plan plan)
    {
        if (previews.Count >= 16) previews.Remove(previews.Keys.First());
        previews.Add(id, plan);
    }

    public async Task<string?> ChangeFolderIcon(Guid previewId, Func<Task<string?>> pickFolder,
        Func<string, string, Task<bool>> confirm)
    {
        if (!OperatingSystem.IsWindows()) throw new CommandFault("desktop", "Значки папок доступны в Windows.");
        DesktopFolderIcons.Request icon;
        lock (gate)
        {
            icon = IconPlan(previewId);
            if (!choosingFolders.Add(previewId)) throw new CommandFault("desktop", "Завершите выбор папки.");
        }

        try
        {
            var folder = await pickFolder();
            if (string.IsNullOrEmpty(folder)) return null;
            var (created, selected) = await Task.Run(() =>
            {
                var timestamp = FolderCreationTime(folder);
                var selectedIcon = icon.Icon ?? SuggestFolderIcon(folder);
                return (timestamp, selectedIcon);
            });
            if (!await confirm(folder, DesktopFolderIcons.Label(selected))) return null;
            return await Task.Run(() =>
            {
                lock (gate)
                {
                    IconPlan(previewId);
                    if (FolderCreationTime(folder) != created)
                        throw new CommandFault("desktop", "Папка изменилась. Выберите её заново.");
                    var result = ApplyIconChanges([PrepareIconChange(folder, created, selected, DesktopFolderIcons.Style.Classic)], []);
                    if (result.Issues.Length > 0) throw new CommandFault("desktop", result.Issues[0].Message);
                    previews.Remove(previewId);
                    return folder;
                }
            });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new CommandFault("desktop", "Не удалось изменить значок. Проверьте доступ к папке и повторите.");
        }
        finally { lock (gate) choosingFolders.Remove(previewId); }
    }

    public FolderIconPreview PreviewDesktopFolderIcons(Guid previewId)
    {
        lock (gate)
        {
            var requested = IconPlan(previewId);
            CheckDesktopForIcons();
            try
            {
                var folders = new DirectoryInfo(desktop).EnumerateDirectories("*", new EnumerationOptions
                    { AttributesToSkip = FileAttributes.Hidden | FileAttributes.ReparsePoint })
                    .OrderBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Take(PageSize + 1).ToArray();
                var entries = folders.Take(PageSize).Select(folder => new FolderIconEntry(folder.Name, folder.CreationTimeUtc,
                    requested.Icon ?? SuggestFolderIcon(folder.FullName))).ToArray();
                var id = Guid.NewGuid();
                RememberPreview(id, new([], FolderIcons: entries));
                var rows = entries.Select((entry, index) => new FolderIconRow(index, entry.Name, DesktopFolderIcons.Label(entry.Icon))).ToArray();
                var images = DesktopFolderIcons.IconPool.ToDictionary(pair => pair.Key, pair => DesktopFolderIcons.PreviewImages(pair.Value));
                var styles = DesktopFolderIcons.Styles.Select(style => new FolderIconStyle((int)style, DesktopFolderIcons.StyleLabel(style),
                    style is DesktopFolderIcons.Style.ClosedOutline or DesktopFolderIcons.Style.ClosedFilled or
                        DesktopFolderIcons.Style.OpenOutline or DesktopFolderIcons.Style.OpenFilled)).ToArray();
                return new(id, rows, folders.Length > PageSize, images, styles);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                throw new CommandFault("desktop", "Не удалось прочитать папки рабочего стола. Проверьте доступ и повторите.");
            }
        }
    }

    public IconResult ApplyDesktopFolderIcons(Guid previewId, int[] ids, Dictionary<int, int>? styles = null, Dictionary<int, string>? icons = null)
    {
        lock (gate)
        {
            if (!previews.TryGetValue(previewId, out var plan) || plan.FolderIcons is not { } folders)
                throw new CommandFault("desktop", "Предпросмотр устарел. Откройте список папок заново.");
            if (InvalidSelection(ids, folders.Length)) throw new CommandFault("desktop", "Проверьте выбор папок.");
            if (styles?.Any(pair => !ids.Contains(pair.Key) || !Enum.IsDefined((DesktopFolderIcons.Style)pair.Value)) == true)
                throw new CommandFault("desktop", "Выберите доступные стили папок.");
            if (icons?.Any(pair => !ids.Contains(pair.Key) || !DesktopFolderIcons.IconPool.ContainsKey(pair.Value)) == true)
                throw new CommandFault("desktop", "Выберите значки из списка.");
            CheckDesktopForIcons();
            var changes = new List<IconChange>();
            var issues = new List<Issue>();
            foreach (var id in ids)
            {
                var entry = folders[id];
                var folder = Path.Combine(desktop, entry.Name);
                try
                {
                    if (FolderCreationTime(folder) != entry.Created)
                    {
                        issues.Add(new(entry.Name, "Папка изменилась. Откройте список папок заново."));
                        continue;
                    }
                    var style = (DesktopFolderIcons.Style)(styles?.GetValueOrDefault(id) ?? 0);
                    var icon = icons is not null && icons.TryGetValue(id, out var name) ? DesktopFolderIcons.IconPool[name] : entry.Icon;
                    changes.Add(PrepareIconChange(folder, entry.Created, icon, style));
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    issues.Add(new(entry.Name, "Не удалось установить значок. Проверьте доступ к папке."));
                }
            }
            return ApplyIconChanges(changes, issues);
        }
    }

    public bool CanUndoFolderIcons { get { lock (gate) return LastIconChanges().Length > 0; } }

    IconChange PrepareIconChange(string folder, DateTime created, DesktopFolderIcons.Icon icon, DesktopFolderIcons.Style style)
    {
        var before = DesktopFolderIcons.ReadSettings(folder);
        return new(folder, created, before, DesktopFolderIcons.PrepareSettings(folder, before, icon, store.DirectoryPath, style));
    }

    IconResult ApplyIconChanges(List<IconChange> changes, List<Issue> issues)
    {
        var candidates = changes.Where(change => !DesktopFolderIcons.SameSettings(change.Before, change.After)).ToArray();
        var previous = LastIconChanges();
        if (candidates.Length == 0) return new(0, [..issues], previous.Length > 0);
        store.Save(IconUndoKey, new IconHistory(candidates, previous));
        var count = 0;
        var completed = new List<IconChange>();
        foreach (var change in candidates)
        {
            try
            {
                if (FolderCreationTime(change.Folder) != change.Created) throw new IOException("Folder was replaced");
                DesktopFolderIcons.WriteSettings(change.Folder, change.Before, change.After);
                completed.Add(change);
                count++;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (IconChangeWasApplied(change)) completed.Add(change);
                issues.Add(new(change.Name, "Не удалось установить значок. Проверьте доступ к папке."));
            }
        }
        var latest = completed.Count > 0 ? completed.ToArray() : previous;
        store.Save(IconUndoKey, new IconHistory(latest));
        return new(count, [..issues], latest.Length > 0);
    }

    public IconResult UndoFolderIcons()
    {
        lock (gate)
        {
            var completed = 0;
            var pending = new List<IconChange>();
            var issues = new List<Issue>();
            foreach (var change in LastIconChanges())
            {
                try
                {
                    if (FolderCreationTime(change.Folder) != change.Created) throw new IOException("Folder was replaced");
                    var current = DesktopFolderIcons.ReadSettings(change.Folder);
                    if (DesktopFolderIcons.SameSettings(current, change.Before)) continue;
                    if (!DesktopFolderIcons.SameContents(current, change.After))
                    {
                        issues.Add(new(change.Name, "Оформление папки уже изменилось."));
                        pending.Add(change);
                        continue;
                    }
                    DesktopFolderIcons.WriteSettings(change.Folder, change.After, change.Before);
                    completed++;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    issues.Add(new(change.Name, "Не удалось вернуть значок. Проверьте доступ к папке."));
                    pending.Add(change);
                }
            }
            store.Save(IconUndoKey, new IconHistory([..pending]));
            return new(completed, [..issues], pending.Count > 0);
        }
    }

    IconChange[] LastIconChanges()
    {
        var history = store.Read<IconHistory?>(IconUndoKey, null);
        if (history is null) return [];
        if (history.Previous is null) return history.Changes;
        var applied = history.Changes.Where(IconChangeWasApplied).ToArray();
        return applied.Length > 0 ? applied : history.Previous;
    }

    static bool IconChangeWasApplied(IconChange change)
    {
        try
        {
            if (FolderCreationTime(change.Folder) != change.Created) return false;
            var current = DesktopFolderIcons.ReadSettings(change.Folder);
            return DesktopFolderIcons.SameContents(current, change.After) && !DesktopFolderIcons.SameSettings(current, change.Before);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return false; }
    }

    void CheckDesktopForIcons()
    {
        if (!OperatingSystem.IsWindows()) throw new CommandFault("desktop", "Значки папок доступны в Windows.");
        try { SafeDirectory(desktop); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new CommandFault("desktop", "Папка рабочего стола недоступна.");
        }
    }

    static DesktopFolderIcons.Icon SuggestFolderIcon(string folder) => DesktopFolderIcons.Suggest(folder,
        Directory.EnumerateFiles(folder, "*", new EnumerationOptions { AttributesToSkip = Excluded }).Take(PageSize),
        name => Category(name) ?? DesktopPrompt.LaunchCategory(Path.GetFileName(name)));

    DesktopFolderIcons.Request IconPlan(Guid id) => previews.TryGetValue(id, out var plan) && plan.Icon is { } icon
        ? icon : throw new CommandFault("desktop", "Предпросмотр устарел. Примените запрос ещё раз.");

    static DateTime FolderCreationTime(string folder)
    {
        SafeDirectory(folder);
        return Directory.GetCreationTimeUtc(folder);
    }

    void CheckDocumentsFolder()
    {
        SafeDirectory(Folder);
        SafeDirectory(DocumentsFolder);
    }

    string? ReadShortcutTarget(FileInfo file) => file.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) &&
        (file.Attributes & FileAttributes.Offline) == 0 ? shortcutTarget?.Invoke(file.FullName) : null;

    public Result Move(Guid previewId, int[] ids, Dictionary<int, string>? topics = null)
    {
        lock (gate)
        {
            if (!previews.TryGetValue(previewId, out var plan) || plan.Icon is not null || plan.FolderIcons is not null)
                throw new CommandFault("desktop", "Предпросмотр устарел. Обновите список файлов.");
            var preview = plan.Files;
            if (InvalidSelection(ids, preview.Length))
                throw new CommandFault("desktop", "Проверьте выбор файлов.");
            var allowedTopics = DesktopClassifier.Topics;
            if (topics?.Any(pair => !ids.Contains(pair.Key) || !preview[pair.Key].ArchivedDocument ||
                !allowedTopics.Contains(pair.Value, StringComparer.Ordinal)) == true)
                throw new CommandFault("desktop", "Выберите тему из списка.");
            var chosen = ids.Select(id => topics is not null && topics.TryGetValue(id, out var topic)
                ? preview[id] with { Folder = topic } : preview[id]).ToArray();
            if (chosen.Any(entry => entry.Folder == ExcludedFromPreview))
                throw new CommandFault("desktop", "Выберите тему для каждого отмеченного файла.");
            if (chosen.Any(entry => entry.ArchivedDocument)) CheckDocumentsFolder();
            previews.Remove(previewId);

            var candidates = new List<Entry>();
            var issues = new List<Issue>();
            var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in chosen)
            {
                var source = Source(entry);
                var destination = Destination(entry);
                if (!Matches(source, entry)) issues.Add(new(entry.Name, "Файл изменился. Обновите список."));
                else if (Path.Exists(destination) || !destinations.Add(destination))
                    issues.Add(new(entry.Name, "В папке назначения уже есть файл с таким именем или он выбран для переноса."));
                else candidates.Add(entry);
            }
            if (candidates.Count == 0) return new(0, [..issues]);

            var previous = LastMove();
            // Persist before touching files so an interrupted move remains recoverable.
            Save(candidates.ToArray(), previous);
            var moved = new List<Entry>();
            var newFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in candidates)
            {
                try
                {
                    if (entry.ArchivedDocument) CheckDocumentsFolder();
                    var destination = Destination(entry);
                    var source = Source(entry);
                    if (!Matches(source, entry))
                    {
                        issues.Add(new(entry.Name, "Файл изменился. Обновите список."));
                        continue;
                    }
                    SafeDestinationDirectories(entry, newFolders);
                    File.Move(source, destination);
                    moved.Add(entry);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    issues.Add(new(entry.Name, MoveError(entry, error, "переместить")));
                }
            }
            Save(moved.Count > 0 ? [..moved] : previous);
            if (OperatingSystem.IsWindows()) ApplyFolderIcons(moved, newFolders, issues);
            return new(moved.Count, [..issues]);
        }
    }

    static bool InvalidSelection(int[] ids, int count) => ids.Length is < 1 or > PageSize ||
        ids.Distinct().Count() != ids.Length || ids.Any(id => id < 0 || id >= count);

    public Result Undo()
    {
        lock (gate)
        {
            var pending = new List<Entry>();
            var issues = new List<Issue>();
            var restored = 0;
            foreach (var entry in LastMove())
            {
                try
                {
                    if (entry.ArchivedDocument) CheckDocumentsFolder();
                    var source = Destination(entry);
                    SafeDestinationDirectories(entry);
                    var destination = Source(entry);
                    if (Path.Exists(destination))
                    {
                        pending.Add(entry);
                        issues.Add(new(entry.Name, entry.Shared ? "На общем рабочем столе уже есть такой файл." : "На рабочем столе уже есть такой файл."));
                        continue;
                    }
                    if (!Matches(source, entry))
                    {
                        pending.Add(entry);
                        issues.Add(new(entry.Name, "Файл изменился. Проверьте его в папке «Разобранное»."));
                        continue;
                    }
                    File.Move(source, destination);
                    restored++;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    pending.Add(entry);
                    issues.Add(new(entry.Name, MoveError(entry, error, "вернуть")));
                }
            }
            Save([..pending]);
            return new(restored, [..issues]);
        }
    }

    public string ExistingFolder()
    {
        lock (gate)
        {
            if (string.IsNullOrEmpty(desktop) || !Directory.Exists(Folder))
                throw new CommandFault("desktop", "Папка «Разобранное» появится после переноса файлов.");
            SafeDirectory(Folder);
            return Folder;
        }
    }

    Entry[] LastMove()
    {
        var history = store.Read<History?>(UndoKey, null);
        if (history is null || !string.Equals(history.Desktop, desktop, StringComparison.OrdinalIgnoreCase)) return [];
        bool ValidOrigin(Entry entry) => ValidEntry(entry) && (!entry.Shared ||
            (HasCommonDesktop && string.Equals(history.CommonDesktop, commonDesktop, StringComparison.OrdinalIgnoreCase)));
        if (history.Previous is null) return history.Files.Where(ValidOrigin).ToArray();
        var recovered = history.Files.Where(entry => ValidOrigin(entry) &&
            !Matches(Source(entry), entry) && Matches(Destination(entry), entry)).ToArray();
        return recovered.Length > 0 ? recovered : history.Previous.Where(ValidOrigin).ToArray();
    }

    void Save(Entry[] entries, Entry[]? previous = null) => store.Save(UndoKey, new History(desktop, entries, previous, commonDesktop));
    string Source(Entry entry) => Path.Combine(entry.ArchivedDocument ? DocumentsFolder : entry.Shared ? commonDesktop! : desktop, entry.Name);
    string Destination(Entry entry) => Path.Combine(Folder, entry.Folder ?? Category(entry.Name)!, entry.Name);

    static string MoveError(Entry entry, Exception error, string action) => entry.Shared && error is UnauthorizedAccessException
        ? $"Windows не разрешила {action} файл общего рабочего стола. Проверьте права на эту папку."
        : $"Не удалось {action}. Проверьте доступ и закройте файл.";

    void SafeDestinationDirectories(Entry entry, HashSet<string>? newFolders = null)
    {
        var path = Folder;
        SafeDirectory(path, newFolders is not null);
        foreach (var segment in (entry.Folder ?? Category(entry.Name)!).Split('/'))
        {
            path = Path.Combine(path, segment);
            var isNew = newFolders is not null && !Path.Exists(path);
            SafeDirectory(path, newFolders is not null);
            if (isNew) newFolders!.Add(path);
        }
    }

    void ApplyFolderIcons(List<Entry> moved, HashSet<string> newFolders, List<Issue> issues)
    {
        var icons = new Dictionary<string, DesktopFolderIcons.Icon>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in moved)
        {
            var fileCategory = Category(entry.Name) ?? DesktopPrompt.LaunchCategory(entry.Name);
            var segments = (entry.Folder ?? fileCategory!).Split('/');
            var icon = DesktopFolderIcons.Choose(segments[0], fileCategory);
            var path = Folder;
            foreach (var segment in segments)
            {
                path = Path.Combine(path, segment);
                if (!newFolders.Contains(path)) continue;
                icons[path] = icons.TryGetValue(path, out var existing) && existing != icon
                    ? DesktopFolderIcons.Icon.Mixed : icon;
            }
        }

        foreach (var (path, icon) in icons)
        {
            try
            {
                SafeDirectory(Folder);
                var parent = Path.GetDirectoryName(path)!;
                if (parent != Folder) SafeDirectory(parent);
                SafeDirectory(path);
                DesktopFolderIcons.Apply(path, icon, store.DirectoryPath);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                issues.Add(new(Path.GetFileName(path), "Не удалось установить значок папки."));
            }
        }
    }

    static (string Folder, bool Selected, bool Selectable, string? Reason) FromClassifier(DesktopClassifier classifier, FileInfo file, string fallback)
    {
        var (folder, selected) = classifier.Suggest(file, fallback);
        return (folder, selected, true, selected ? null : "Проверьте папку");
    }

    static bool ValidEntry(Entry entry) => entry.Name == Path.GetFileName(entry.Name) &&
        entry.Name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        (!entry.ArchivedDocument || (!entry.Shared && Category(entry.Name) == "Документы" && DesktopClassifier.Topics.Contains(entry.Folder))) &&
        (Category(entry.Name) is not null || (entry.Folder is not null && DesktopPrompt.LaunchCategory(entry.Name) is not null)) &&
        (entry.Folder is null || ValidDestination(entry.Folder));

    static bool ValidDestination(string folder)
    {
        var segments = folder.Split('/');
        return DesktopClassifier.ValidFolder(segments[0]) && (segments.Length == 1 ||
            (segments.Length == 2 && segments[1].Length == 4 && int.TryParse(segments[1], out var year) && year is >= 1900 and <= 2099));
    }

    static void SafeDirectory(string path, bool create = false)
    {
        if (create && !Path.Exists(path)) Directory.CreateDirectory(path);
        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != FileAttributes.Directory)
            throw new IOException("Unsafe desktop cleanup folder");
    }

    static Entry Capture(FileInfo file) => new(file.Name, file.Length, file.CreationTimeUtc, file.LastWriteTimeUtc);
    static bool IsRegular(FileInfo file) => file.Exists && (file.Attributes & Excluded) == 0;
    static bool Matches(string path, Entry entry)
    {
        var file = new FileInfo(path);
        return IsRegular(file) && file.Length == entry.Length && file.CreationTimeUtc == entry.Created &&
            file.LastWriteTimeUtc == entry.Modified;
    }

    static string? Category(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".odt" or ".ods" or ".odp" or
        ".csv" or ".tsv" or ".txt" or ".rtf" or ".md" => "Документы",
        ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".bmp" or ".tif" or ".tiff" or ".heic" => "Изображения",
        ".zip" or ".7z" or ".rar" or ".tar" or ".gz" => "Архивы",
        ".har" or ".log" => "Логи",
        ".mp4" or ".mov" or ".mkv" or ".webm" or ".mp3" or ".wav" or ".flac" or ".m4a" => "Медиа",
        _ => null
    };
}

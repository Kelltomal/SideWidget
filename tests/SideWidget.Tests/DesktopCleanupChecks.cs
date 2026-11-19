using System.Diagnostics;
using System.IO.Compression;
using SideWidget.Core;
using SideWidget.Infrastructure;

static class DesktopCleanupChecks
{
    public static void Run()
    {
        var root = Directory.CreateTempSubdirectory("SideWidget-cleanup-").FullName;
        try
        {
            var desktop = Directory.CreateDirectory(Path.Combine(root, "Desktop")).FullName;
            var store = new Store(Path.Combine(root, "state"));
            var service = new DesktopCleanup(store, desktop);
            foreach (var name in new[] { "Документ.XLSX", "trace.har", "photo.png", "keep.lnk", "keep.bat", "keep.exe", "desktop.ini" })
                File.WriteAllText(Path.Combine(desktop, name), name);
            Directory.CreateDirectory(Path.Combine(desktop, "folder.pdf"));
            File.WriteAllText(Path.Combine(desktop, "folder.pdf", "nested.pdf"), "nested");
            var preview = service.Preview();
            Require(preview.Files.Length == 3 && !preview.CanUndo, "scan scope");
            var selected = preview.Files.Where(file => file.Name != "photo.png").Select(file => file.Id).ToArray();
            Reject(() => service.Move(Guid.NewGuid(), selected));
            Reject(() => service.Move(preview.Id, [-1]));
            File.AppendAllText(Path.Combine(desktop, "trace.har"), "changed after preview");
            var result = service.Move(preview.Id, selected);
            Require(result.Count == 1 && result.Issues.Single().Name == "trace.har", "partial move");
            var document = Path.Combine(desktop, "Документ.XLSX");
            var moved = Path.Combine(service.Folder, "Документы", "Документ.XLSX");
            Require(!File.Exists(document) && File.ReadAllText(moved) == "Документ.XLSX", "move content");
            Require(File.Exists(Path.Combine(desktop, "photo.png")), "unchecked file");
            Require(File.Exists(Path.Combine(desktop, "keep.lnk")), "shortcut preserved");
            Reject(() => service.Move(preview.Id, selected));

            service = new DesktopCleanup(new Store(store.DirectoryPath), desktop);
            Require(service.Preview().CanUndo, "undo survives restart");
            File.WriteAllText(document, "new file");
            preview = service.Preview();
            result = service.Move(preview.Id, [preview.Files.Single(file => file.Name == "Документ.XLSX").Id]);
            Require(result.Count == 0 && result.Issues.Length == 1, "destination collision");
            Require(service.Undo().Count == 0 && File.ReadAllText(document) == "new file", "undo collision");
            File.Move(document, Path.Combine(root, "replacement"));
            Require(service.Undo().Count == 1 && File.ReadAllText(document) == "Документ.XLSX", "undo retry");
            Require(!service.Preview().CanUndo, "undo consumed");
            File.WriteAllText(Path.Combine(desktop, "sample.txt"), "Артикул поставщика и цена за штуку");
            preview = service.Preview("Поставки: артикул поставщика");
            var sample = preview.Files.Single(file => file.Name == "sample.txt");
            Require(sample.Category == "Поставки" && sample.Selected, "prompt matches file content");
            Require(service.Move(preview.Id, [sample.Id]).Count == 1, "prompt destination");
            Require(new DesktopCleanup(store, desktop).Undo().Count == 1, "prompt destination persists for undo");
            Require(!service.Preview().Files.Single(file => file.Name == "sample.txt").Selected, "uncertain file unchecked");
            Reject(() => service.Preview("../outside: артикул"));
            Reject(() => service.Preview("NUL.txt: артикул"));
            preview = service.Preview("Поставки: артикул\nСклад: поставщика");
            Require(!preview.Files.Single(file => file.Name == "sample.txt").Selected, "conflicting prompt rules unchecked");
            File.WriteAllText(Path.Combine(desktop, "Счета_2026.txt"), "");
            File.WriteAllText(Path.Combine(desktop, "Счета_2025.txt"), "");
            preview = service.Preview("Текущий год: счета 2026");
            Require(preview.Files.Single(file => file.Name == "Счета_2026.txt").Category == "Текущий год" &&
                preview.Files.Single(file => file.Name == "Счета_2025.txt").Category != "Текущий год", "prompt matches numeric filename phrase");
            Benchmark(store, Directory.CreateDirectory(Path.Combine(root, "benchmark")).FullName);
            RussianBenchmark(store, Directory.CreateDirectory(Path.Combine(root, "russian")).FullName);
            TopicBenchmark(store, Directory.CreateDirectory(Path.Combine(root, "topics")).FullName);
            NaturalPrompt(store, Directory.CreateDirectory(Path.Combine(root, "natural")).FullName);
            StructuredCommands(store, Directory.CreateDirectory(Path.Combine(root, "commands")).FullName);
            MultilingualPrompts(store, Directory.CreateDirectory(Path.Combine(root, "multilingual")).FullName);
            TopicPrompt(store, Directory.CreateDirectory(Path.Combine(root, "topic-prompt")).FullName);
            AppPrompt(store, Directory.CreateDirectory(Path.Combine(root, "apps")).FullName);
            if (OperatingSystem.IsWindows())
            {
                FolderIcons(store, Directory.CreateDirectory(Path.Combine(root, "icons")).FullName);
                ChangeFolderIcon(store, Directory.CreateDirectory(Path.Combine(root, "change-icon")).FullName);
            }
            SharedDesktop(store, root);
            ArchivedDocuments(store, Directory.CreateDirectory(Path.Combine(root, "archive-desktop")).FullName);
        }
        finally { Directory.Delete(root, recursive: true); }
        Console.WriteLine("PASS: desktop cleanup, partial failures and persistent undo");
    }

    static void StructuredCommands(Store store, string desktop)
    {
        var service = new DesktopCleanup(store, desktop);
        File.WriteAllText(Path.Combine(desktop, "invoice-2025.txt"), "");
        File.WriteAllText(Path.Combine(desktop, "Screenshot.png"), "");
        File.WriteAllText(Path.Combine(desktop, "backup.zip"), "");
        Require(service.GetStatus() is {HasFolder: false, CanUndo: false}, "desktop history before moving");
        var preview = service.PreviewCommand("sort",
            [new("Счета", "year"), new("Скриншоты", "keep"), new("Архивы", "move", "備份 2026")]);
        Require(preview.Files.Single(file => file.Name == "invoice-2025.txt") is {Selected: true, Category: "Счета/2025"}, "structured year rule");
        Require(preview.Files.Single(file => file.Name == "Screenshot.png") is {Selectable: false}, "structured keep rule");
        Require(preview.Files.Single(file => file.Name == "backup.zip") is {Selected: true, Category: "備份 2026"}, "literal structured destination");
        Require(!Directory.Exists(service.Folder), "structured preview changes no files");
        Reject(() => service.PreviewCommand("sort", [new("Архивы", "move", "../outside")]));
        Reject(() => service.PreviewCommand("sort", [new("Архивы", "topic")]));
        Reject(() => service.PreviewCommand("sort", [new("Архивы", "delete")]));
        Reject(() => service.PreviewCommand("sort", []));
        Reject(() => service.PreviewCommand("icons", [new("Архивы", "keep")]));
        Require(service.PreviewCommand("icons", null).SuggestFolderIcon, "structured icon command uses existing icon plan");
        var moved = service.Move(preview.Id, preview.Files.Where(file => file.Selected).Select(file => file.Id).ToArray());
        Require(moved.Count == 2 && File.Exists(Path.Combine(desktop, "Screenshot.png")), "structured plan uses normal move safeguards");
        Require(service.GetStatus() is {HasFolder: true, CanUndo: true}, "desktop history after moving");
        Require(service.Undo().Count == 2, "structured plan is undoable");
    }

    static void ChangeFolderIcon(Store store, string desktop)
    {
        var service = new DesktopCleanup(store, desktop);
        var selected = Directory.CreateDirectory(Path.Combine(desktop, "Материалы")).FullName;
        var settings = Path.Combine(selected, "desktop.ini");
        const string original = "[.ShellClassInfo]\r\nInfoTip=Мои материалы\r\nIconFile=old.ico\r\nIconIndex=0\r\n[ViewState]\r\nMode=Details\r\n";
        File.WriteAllText(settings, original, System.Text.Encoding.Unicode);
        var preview = service.Preview("Поменяй значок папки на «Фото»");
        Require(preview.FolderIcon == "Фото" && preview.Files.Length == 0, "folder icon prompt produces an icon plan");
        Require(File.ReadAllText(settings) == original, "preview preserves the existing icon");
        Reject(() => service.Move(preview.Id, [0]));
        Require(service.Preview("Поставь папке иконку архива").FolderIcon == "Архив", "folder icon request accepts natural wording");
        var noFolder = service.ChangeFolderIcon(preview.Id, () => Task.FromResult<string?>(null),
            (_, _) => throw new InvalidOperationException("No folder to confirm")).GetAwaiter().GetResult();
        Require(noFolder is null && File.ReadAllText(settings) == original, "picker cancellation leaves the file intact");

        var cancelled = service.ChangeFolderIcon(preview.Id, () => Task.FromResult<string?>(selected), (folder, icon) =>
        {
            Require(folder == selected && icon == "Фото", "confirmation identifies the picked folder and icon");
            return Task.FromResult(false);
        }).GetAwaiter().GetResult();
        Require(cancelled is null && File.ReadAllText(settings) == original, "cancelled icon change leaves the file intact");

        var changed = service.ChangeFolderIcon(preview.Id, () => Task.FromResult<string?>(selected), (_, _) => Task.FromResult(true)).GetAwaiter().GetResult();
        var updated = File.ReadAllText(settings);
        Require(changed == selected && updated.Contains("IconResource=") && !updated.Contains("IconFile=") && !updated.Contains("IconIndex="),
            "confirmation replaces the existing icon");
        Require(updated.Contains("InfoTip=Мои материалы") && updated.Contains("[ViewState]\r\nMode=Details"),
            "icon replacement preserves unrelated folder settings");
        const FileAttributes hiddenSystem = FileAttributes.Hidden | FileAttributes.System;
        Require((File.GetAttributes(settings) & hiddenSystem) == hiddenSystem, "replaced desktop.ini is hidden and marked as a system file");
        Require(new DesktopCleanup(store, desktop).UndoFolderIcons().Count == 1 && File.ReadAllText(settings) == original,
            "single icon change restores original desktop.ini after restart");
        Reject(() => service.ChangeFolderIcon(preview.Id, () => throw new InvalidOperationException("Picker should not reopen"),
            (_, _) => Task.FromResult(true)).GetAwaiter().GetResult());
        Reject(() => service.Preview("Поменяй значок папки на «C:\\icon.dll»"));

        var auto = service.Preview("Поменяй значки на рабочем столе");
        Require(auto.SuggestFolderIcon && auto.Files.Length == 0, "generic icon request opens the folder selection flow");
        var photos = Directory.CreateDirectory(Path.Combine(desktop, "Материалы 2026")).FullName;
        File.WriteAllText(Path.Combine(photos, "photo.jpg"), "photo");
        var autoChanged = service.ChangeFolderIcon(auto.Id, () => Task.FromResult<string?>(photos), (folder, icon) =>
        {
            Require(folder == photos && icon == "Фото", "icon is suggested from the picked folder contents");
            Require(!File.Exists(Path.Combine(photos, "desktop.ini")), "icon suggestion leaves the picked folder untouched");
            return Task.FromResult(true);
        }).GetAwaiter().GetResult();
        Require(autoChanged == photos && File.Exists(Path.Combine(photos, "desktop.ini")), "suggested icon is applied after confirmation");

        var desktopPlan = service.Preview("Поменяй значки на рабочем столе");
        var desktopSettings = Path.Combine(desktop, "desktop.ini");
        var games = Directory.CreateDirectory(Path.Combine(desktop, "игры")).FullName;
        var finance = Directory.CreateDirectory(Path.Combine(desktop, "Финансы")).FullName;
        Directory.CreateDirectory(Path.Combine(games, "Вложенная"));
        var hidden = Directory.CreateDirectory(Path.Combine(desktop, "Скрытая"));
        hidden.Attributes |= FileAttributes.Hidden;
        var batch = service.PreviewDesktopFolderIcons(desktopPlan.Id);
        var gamePlan = batch.Folders.Single(row => row.Name == "игры");
        Require(gamePlan.Icon == "Игры" && batch.Folders.Single(row => row.Name == "Финансы").Icon == "Финансы",
            "preview names the icons for the desktop folders");
        Require(!batch.Folders.Any(row => row.Name is "Вложенная" or "Скрытая") &&
            !File.Exists(Path.Combine(games, "desktop.ini")), "preview is limited to visible first-level folders and does not change them");
        var gameStyles = batch.IconImages[gamePlan.Icon];
        Require(batch.Styles.Select(style => style.Name).Skip(3).SequenceEqual(new[] { "Современная", "Ретро",
                "Закрытая контурная", "Закрытая залитая", "Открытая контурная", "Открытая залитая" }) &&
            batch.Styles.Length == 9 && gameStyles.Distinct().Count() == 9, "each category offers all nine distinct folder styles");
        Reject(() => service.ApplyDesktopFolderIcons(batch.Id, [gamePlan.Id], new() { [gamePlan.Id] = 99 }));
        var desktopChanged = service.ApplyDesktopFolderIcons(batch.Id, [gamePlan.Id], new() { [gamePlan.Id] = 8 }, new() { [gamePlan.Id] = "Архив" });
        Require(desktopChanged.Count == 1 && desktopChanged.Issues.Length == 0 && File.Exists(Path.Combine(games, "desktop.ini")) &&
            !File.Exists(Path.Combine(finance, "desktop.ini")) && !File.Exists(desktopSettings),
            "only checked desktop folders are customized");
        const string iconPrefix = "IconResource=";
        var resource = File.ReadLines(Path.Combine(games, "desktop.ini")).Single(line => line.StartsWith(iconPrefix));
        Require(resource.EndsWith(".ico,0", StringComparison.OrdinalIgnoreCase), "Windows uses a custom folder icon resource");
        var icoPath = resource[iconPrefix.Length..^2];
        Require(Path.GetDirectoryName(icoPath) == Path.Combine(store.DirectoryPath, "FolderIcons"),
            "folder icons resolve from the persistent application profile");
        var iconBytes = File.ReadAllBytes(icoPath);
        const string pngPrefix = "data:image/png;base64,";
        var previewBytes = Convert.FromBase64String(batch.IconImages["Архив"][8][pngPrefix.Length..]);
        Require(iconBytes.AsSpan().IndexOf(previewBytes) >= 0, "Windows uses the chosen icon from the pool and the chosen style");
        var repeated = service.ApplyDesktopFolderIcons(batch.Id, [gamePlan.Id], new() { [gamePlan.Id] = 8 }, new() { [gamePlan.Id] = "Архив" });
        Require(repeated.Count == 0 && repeated.CanUndoIcons, "the same preview can be applied again without replacing undo history");
        Require(new DesktopCleanup(store, desktop).UndoFolderIcons().Count == 1 && !File.Exists(Path.Combine(games, "desktop.ini")) &&
            (File.GetAttributes(games) & FileAttributes.System) == 0, "batch undo restores a folder that did not have custom settings");

        var nextBatch = service.PreviewDesktopFolderIcons(desktopPlan.Id);
        var financeId = nextBatch.Folders.Single(row => row.Name == "Финансы").Id;
        Require(service.ApplyDesktopFolderIcons(nextBatch.Id, [financeId]).CanUndoIcons, "new icon change can be undone");
        var financeSettings = Path.Combine(finance, "desktop.ini");
        File.SetAttributes(financeSettings, FileAttributes.Normal);
        File.WriteAllText(financeSettings, "changed by Explorer");
        var conflict = service.UndoFolderIcons();
        Require(conflict.Count == 0 && conflict.Issues.Length == 1 && File.ReadAllText(financeSettings) == "changed by Explorer",
            "undo preserves changes made after SideWidget applied the icon");
    }

    static void FolderIcons(Store store, string desktop)
    {
        var service = new DesktopCleanup(store, desktop);
        var archive = Path.Combine(desktop, "backup.zip");
        var image = Path.Combine(desktop, "photo.png");
        File.WriteAllText(archive, "archive");
        File.WriteAllText(image, "image");
        var preview = service.Preview("Перенеси архивы в папку Резерв, а фото в папку Снимки");
        Require(!Directory.Exists(service.Folder), "icon preview leaves the desktop untouched");
        var result = service.Move(preview.Id, preview.Files.Select(file => file.Id).ToArray());
        Require(result.Count == 2 && result.Issues.Length == 0, "confirmed move into custom folders");

        var archiveFolder = Path.Combine(service.Folder, "Резерв");
        var archiveSettings = Path.Combine(archiveFolder, "desktop.ini");
        var imageSettings = Path.Combine(service.Folder, "Снимки", "desktop.ini");
        Require(File.Exists(archiveSettings) && File.Exists(imageSettings), "new folders get Explorer icon settings");
        var original = File.ReadAllText(archiveSettings);
        Require(original.Contains("IconResource=") && original != File.ReadAllText(imageSettings),
            "icons follow the file categories when folder names are custom");
        const FileAttributes hiddenSystem = FileAttributes.Hidden | FileAttributes.System;
        Require((File.GetAttributes(archiveSettings) & hiddenSystem) == hiddenSystem &&
            (File.GetAttributes(archiveFolder) & (FileAttributes.ReadOnly | FileAttributes.System)) != 0,
            "Explorer can read the hidden folder settings");
        Require(service.Undo().Count == 2, "folder icons do not interfere with undo");

        preview = service.Preview("Перенеси фото в папку Резерв");
        Require(service.Move(preview.Id, [preview.Files.Single(file => file.Selected).Id]).Count == 1 &&
            File.ReadAllText(archiveSettings) == original, "confirmed move preserves an existing folder icon");
    }

    static void ArchivedDocuments(Store store, string desktop)
    {
        var service = new DesktopCleanup(store, desktop);
        Require(service.PreviewDocuments().Files.Length == 0, "missing documents folder is empty");
        var source = Directory.CreateDirectory(Path.Combine(service.Folder, "Документы")).FullName;
        var file = Path.Combine(source, "черновик.txt");
        File.WriteAllText(file, "Второй вариант. Потом уточнить.");
        var plan = service.PreviewDocuments();
        var row = plan.Files.Single();
        Require(!row.Selected && plan.Topics!.Contains("Проекты"), "manual topics available for uncertain file");
        Reject(() => service.Move(plan.Id, [row.Id]));
        Reject(() => service.Move(plan.Id, [row.Id], new() { [row.Id] = "../Проекты" }));
        Require(File.Exists(file), "preview and invalid assignments leave original in place");
        Require(service.Move(plan.Id, [row.Id], new() { [row.Id] = "Проекты" }).Count == 1, "manual assignment moves after confirmation");
        Require(File.Exists(Path.Combine(service.Folder, "Проекты", row.Name)), "manual destination used");
        Require(new DesktopCleanup(store, desktop).Undo().Count == 1 && File.Exists(file), "undo restores archive source");
    }

    static void TopicPrompt(Store store, string desktop)
    {
        File.WriteAllText(Path.Combine(desktop, "budget.txt"), "Сверка платежей, дебиторская задолженность и поступления по банковской выписке.");
        File.WriteAllText(Path.Combine(desktop, "черновик.txt"), "Посмотреть позже. Второй вариант.");
        File.WriteAllText(Path.Combine(desktop, "trace.har"), "");
        var service = new DesktopCleanup(store, desktop);
        foreach (var prompt in new[] { "Разложи документы по темам, а всё непонятное оставь на столе",
            "Сгруппируй офисные документы по смыслу, а сомнительные не трогай" })
        {
            var plan = service.Preview(prompt);
            Require(plan.Answer.Contains("по темам") && plan.Answer.Contains("останутся"), "topic plan and abstention");
            Require(plan.Files.Single(file => file.Selected).Category == "Финансы", "only confident document selected");
            var unclear = plan.Files.Single(file => file.Name == "черновик.txt");
            Require(!unclear.Selectable && unclear.Reason == "Тема не определена уверенно", "unknown document stays");
            Reject(() => service.Move(plan.Id, [unclear.Id]));
        }
        var conflict = service.Preview("Разложи документы по темам, а документы оставь на столе");
        Require(conflict.Files.All(file => !file.Selected), "explicit keep overrides thematic sorting");
        var preview = service.Preview("Рассортируй документы по тематике");
        Require(service.Move(preview.Id, [preview.Files.Single(file => file.Selected).Id]).Count == 1 &&
            File.Exists(Path.Combine(service.Folder, "Финансы", "budget.txt")), "confirmed thematic destination");
        Require(new DesktopCleanup(store, desktop).Undo().Count == 1, "undo thematic move");
    }

    static void TopicBenchmark(Store store, string desktop)
    {
        (string Category, string Name, string Text)[] samples =
        [
            ("Здоровье", "результаты.txt", "Биохимия крови: билирубин, креатинин, показатели выше референса."),
            ("Здоровье", "заключение.txt", "Снимок грудной клетки. Рекомендована консультация пульмонолога."),
            ("Здоровье", "назначения.txt", "Принимать лекарство после еды, повторно явиться на приём через неделю."),
            ("Здоровье", "справка.txt", "Выписной эпикриз после стационара. Жалобы и сопутствующие диагнозы."),
            ("Здоровье", "контроль.txt", "Дневник артериального давления, пульс утром и вечером."),
            ("Поездки", "подтверждение.txt", "Посадочный талон. Регистрация на рейс, терминал вылета, ручная кладь."),
            ("Поездки", "план.txt", "Трансфер от аэропорта, ваучер на проживание, время выезда из гостиницы."),
            ("Поездки", "документ.txt", "Электронный билет РЖД. Станция отправления, вагон, нижнее место."),
            ("Поездки", "памятка.txt", "Виза для туристической поездки, бронь номера и обратный перелёт."),
            ("Поездки", "booking.txt", "Passenger itinerary with connecting flights, departure gates and boarding times."),
            ("Авто", "заказ.txt", "На ТО заменить салонный фильтр и тормозные колодки, проверить уровень масла."),
            ("Авто", "запись.txt", "Пробег машины, шиномонтаж и балансировка колёс перед зимой."),
            ("Авто", "полис.txt", "ОСАГО. Вписать второго водителя, проверить VIN и госномер."),
            ("Авто", "проверка.txt", "Диагностика двигателя, ошибки коробки передач, список запчастей для СТО."),
            ("Авто", "учёт.txt", "Данные автомобиля из ПТС, номер кузова и дата регистрации в ГИБДД."),
            ("Финансы", "таблица.txt", "Сверка взаиморасчётов: погашение задолженности и остатки на счетах."),
            ("Договоры и право", "условия.txt", "Лицензионный договор. Стороны подписали соглашение, определили срок и неустойку."),
            ("Проекты", "встреча.txt", "Команда согласовала критерии приёмки, блокеры интеграции и дату деплоя."),
            ("Учёба", "черновик.txt", "Научрук прислал замечания к курсовой. Исправить список источников к зачёту."),
            ("Личное", "список.txt", "Что купить для семейного ужина: мука, яйца, продукты для пирога."),
            ("", "разное.txt", "Повторить в пятницу, договориться заранее."),
            ("", "заметки.txt", "Версия на проверку, черновой список без темы."),
            ("", "памятка общая.txt", "Бюджет отпуска, договор на машину, выписка врача и задания к экзамену."),
            ("", "пусто.txt", ""),
        ];
        foreach (var sample in samples)
            File.WriteAllText(Path.Combine(desktop, sample.Name), sample.Text);
        var expected = samples.ToDictionary(sample => sample.Name, sample => sample.Category);
        var service = new DesktopCleanup(store, desktop);
        var rows = service.Preview().Files;
        var selected = rows.Where(row => row.Selected).ToArray();
        var correct = selected.Count(row => row.Category == expected[row.Name]);
        var unclear = rows.Count(row => !row.Selected && expected[row.Name] == "");
        var times = Enumerable.Range(0, 21).Select(_ =>
        {
            var start = Stopwatch.GetTimestamp();
            service.Preview();
            return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }).Order().ToArray();
        Console.WriteLine($"TOPICS: {correct}/{selected.Length} checked correct, {correct}/20 clear correctly checked, {unclear}/4 ambiguous unchecked; median {times[10]:F1} ms.");
        Require(correct >= 15 && correct == selected.Length && unclear == 4, "expanded topic classification");
    }

    static void SharedDesktop(Store store, string root)
    {
        var personal = Directory.CreateDirectory(Path.Combine(root, "personal")).FullName;
        var shared = Directory.CreateDirectory(Path.Combine(root, "public")).FullName;
        File.WriteAllText(Path.Combine(personal, "Steam.lnk"), "personal");
        File.WriteAllText(Path.Combine(shared, "Steam.lnk"), "shared");
        File.WriteAllText(Path.Combine(shared, "Edge.lnk"), "shared app");
        File.WriteAllText(Path.Combine(shared, "notes.txt"), "shared document");
        var service = new DesktopCleanup(store, personal, commonDesktop: shared);
        Require(service.Preview().Files.Length == 0, "regular preview excludes shared files");
        var plan = service.Preview("Игры в папку Игры, а программы в папку Программы");
        var sharedRows = plan.Files.Where(file => file.Shared).ToArray();
        Require(sharedRows.Length == 2 && sharedRows.All(file => !file.Selected), "shared shortcuts require selection");
        var edge = sharedRows.Single(file => file.Name == "Edge.lnk");
        var steam = plan.Files.Where(file => file.Name == "Steam.lnk").OrderBy(file => file.Shared).Select(file => file.Id);
        var result = service.Move(plan.Id, [..steam, edge.Id]);
        Require(result.Count == 2 && result.Issues.Single().Name == "Steam.lnk", "collision preserves shared shortcut");
        Require(new DesktopCleanup(store, personal, commonDesktop: shared).Undo().Count == 2, "both origins restored after restart");
        Require(File.ReadAllText(Path.Combine(personal, "Steam.lnk")) == "personal" &&
            File.ReadAllText(Path.Combine(shared, "Edge.lnk")) == "shared app", "original roots restored");
    }

    static void RussianBenchmark(Store store, string desktop)
    {
        (string Category, string Content)[] samples =
        [
            ("Финансы", "Платёжка за хостинг. Проверь, куда ушли деньги, и сверь остатки."),
            ("Финансы", "Закрывашки от поставщика, счёт-фактура и сумма к перечислению."),
            ("Финансы", "Куда потратили выручку: приход, расход, сверка взаиморасчётов."),
            ("Финансы", "Премии сотрудникам и начисления по ведомости за полугодие."),
            ("Договоры и право", "Допник к аренде: продлили срок, подписи обеих сторон на последней странице."),
            ("Договоры и право", "Ходатайство о приобщении доказательств к материалам дела."),
            ("Договоры и право", "ИП выдаёт доверку представителю для подписания документов."),
            ("Договоры и право", "Протокол разногласий по условиям поставки и ответственности контрагента."),
            ("Проекты", "ТЗ на переделку личного кабинета: добавить фильтры и согласовать макеты."),
            ("Проекты", "Дожать баги, собрать билд и выкатить фикс в прод в пятницу."),
            ("Проекты", "Созвон с командой. Разобрали блокеры, распределили таски на итерацию."),
            ("Проекты", "Перед демо проверить интеграцию и собрать фидбек по прототипу."),
            ("Учёба", "Домашка к паре: прорешать задачки и сдать преподу до среды."),
            ("Учёба", "Шпора к зачёту по сопромату, вопросы с последней лекции."),
            ("Учёба", "Курсач отправить научруку, поправить оформление списка литературы."),
            ("Учёба", "Лаба по химии: оформить наблюдения и расчёт погрешности."),
            ("Личное", "Купить гостинцы к празднику и подписать открытки родным."),
            ("Личное", "На дачу взять мангал, уголь и продукты на шашлык."),
            ("Личное", "Кот на осмотре: поставить прививку и забрать ветпаспорт."),
            ("Личное", "Тесто на блины: смешать молоко, муку и яйца, жарить на сковородке."),
            ("", "Второй вариант. Посмотреть и потом дописать."),
            ("", "Забрать в четверг, уточнить место и время."),
            ("", "Выгрузка для проверки, последняя копия."),
            ("", "План бюджета проекта и порядок сдачи курсовой."),
        ];
        for (var index = 0; index < samples.Length; index++)
            File.WriteAllText(Path.Combine(desktop, $"{index}.txt"), samples[index].Content);
        var service = new DesktopCleanup(store, desktop);
        var timer = Stopwatch.StartNew();
        var rows = service.Preview().Files;
        var elapsed = timer.Elapsed.TotalMilliseconds;
        var checkedRows = rows.Where(row => row.Selected).ToArray();
        string Expected(DesktopCleanup.FileRow row) => samples[int.Parse(Path.GetFileNameWithoutExtension(row.Name))].Category;
        var correct = checkedRows.Count(row => row.Category == Expected(row));
        var unclear = rows.Count(row => !row.Selected && Expected(row) == "");
        Console.WriteLine($"RUSSIAN: {correct}/{checkedRows.Length} checked correct, {correct}/20 clear correctly checked, {unclear}/4 ambiguous unchecked; {elapsed:F1} ms.");
        Require(correct >= 18 && correct == checkedRows.Length && unclear == 4, "Russian classification regression");
    }

    static void AppPrompt(Store store, string desktop)
    {
        foreach (var name in new[] { "Game Launcher.lnk", "Puzzle.lnk", "Discord.lnk", "start-server.lnk", "Local site.lnk", "ssh host.bat", "tool.exe", "notes.txt" })
            File.WriteAllText(Path.Combine(desktop, name), "fixture");
        var service = new DesktopCleanup(store, desktop, path => Path.GetFileName(path) switch
        {
            "Puzzle.lnk" => @"C:\Games\Puzzle\app.exe",
            "Local site.lnk" => @"C:\Projects\run.cmd",
            _ => null
        });
        Require(service.Preview().Files.Length == 1, "ordinary cleanup excludes launchers and scripts");
        var preview = service.Preview("Игры помести в папку игры, а программы в папку программы. Батники и запуски серверов в отдельную папку.");
        Require(preview.Files.Single(file => file.Name == "Game Launcher.lnk").Category == "игры", "game folder");
        Require(preview.Files.Single(file => file.Name == "Discord.lnk").Category == "программы", "application folder");
        Require(preview.Files.Single(file => file.Name == "Puzzle.lnk").Category == "игры", "game shortcut target");
        Require(preview.Files.Single(file => file.Name == "Local site.lnk").Category == "Скрипты и серверы", "script shortcut target");
        Require(preview.Files.Where(file => file.Name is "start-server.lnk" or "ssh host.bat").All(file => file.Category == "Скрипты и серверы"), "shared server folder");
        Require(!preview.Files.Single(file => file.Name == "ssh host.bat").Selected, "script needs review");
        Require(!preview.Files.Single(file => file.Name == "tool.exe").Selected, "executable needs review");
        Require(preview.Answer.Contains("Скрипты и серверы"), "custom plan answer");
        Require(service.Move(preview.Id, [preview.Files.Single(file => file.Name == "ssh host.bat").Id]).Count == 1, "explicit script selection");
        Require(new DesktopCleanup(store, desktop).Undo().Count == 1, "script undo survives restart");
    }

    static void NaturalPrompt(Store store, string desktop)
    {
        foreach (var (name, content) in new[] {("Счёт 2024.txt", "Оплата до 2025"),
            ("invoice.txt", "Счёт на оплату от 12.10.2025"), ("счёт.txt", "Оплата"),
            ("Screenshot 2025.png", ""), ("план.txt", "план проекта и задачи")})
            File.WriteAllText(Path.Combine(desktop, name), content);
        var service = new DesktopCleanup(store, desktop);
        var preview = service.Preview("разложи счета по годам, а скриншоты оставь на столе");
        Require(preview.Answer.Contains("по годам") && preview.Answer.Contains("Скриншоты"), "textual answer");
        Require(preview.Files.Single(f => f.Name == "Счёт 2024.txt").Category == "Счета/2024", "filename year takes priority");
        Require(preview.Files.Single(f => f.Name == "invoice.txt").Category == "Счета/2025", "year from file content");
        Require(preview.Files.Count(f => f.Selected) == 2, "only requested and dated files checked");
        var screenshot = preview.Files.Single(f => f.Name.StartsWith("Screenshot"));
        Require(!screenshot.Selectable, "screenshot stays");
        Reject(() => service.Move(preview.Id, [screenshot.Id]));
        Require(service.Move(preview.Id, preview.Files.Where(f => f.Selected).Select(f => f.Id).ToArray()).Count == 2, "move into year folders");
        Require(File.Exists(Path.Combine(service.Folder, "Счета", "2024", "Счёт 2024.txt")), "year directory");
        Require(new DesktopCleanup(store, desktop).Undo().Count == 2, "year-folder undo survives restart");
        Reject(() => service.Preview("разложи счета по годам и удали скриншоты"));
        preview = service.Preview("Оставь скриншоты на рабочем столе, а счета разложи по годам");
        Require(preview.Files.Count(f => f.Selected) == 2, "reversed clauses");
        preview = service.Preview("Разложи счета по годам и скриншоты оставь на столе");
        Require(preview.Files.Count(f => f.Selected) == 2, "conjunction between complete clauses");
        preview = service.Preview("Пожалуйста, разнеси инвойсы по годам, а скрины не трогай");
        Require(preview.Files.Count(f => f.Selected) == 2 && !preview.Files.Single(f => f.Name.StartsWith("Screenshot")).Selectable,
            "colloquial Russian prompt");
        foreach (var prompt in new[] {
            "Сделай разбивку платёжных счетов по годам, а снимки с экрана пускай лежат где лежали",
            "Хочу отдельную папку на каждый год для инвойсов, скриншоты пусть остаются там же",
            "Нужна группировка счетов по году, а скрины пусть будут как есть" })
        {
            preview = service.Preview(prompt);
            Require(preview.Files.Count(f => f.Selected) == 2 &&
                preview.Files.Single(f => f.Name.StartsWith("Screenshot")).Reason == "Оставить по запросу", prompt);
        }
        preview = service.Preview("Собери все счета на оплату в папку «Оплаченные 2025»");
        Require(preview.Files.Count(f => f.Selected && f.Category == "Оплаченные 2025") == 3, "named destination preserved");
        foreach (var prompt in new[] { "Как дела?", "Не надо разбирать счета по годам", "Удали счета и скриншоты",
            "Отправь счета по годам", "Собери счета в папку «../наружу»", "Скопируй счета в папку Резерв",
            "Сделай что-нибудь с ненужными файлами" })
            Reject(() => service.Preview(prompt));
        preview = service.Preview("Сложи инвойсы вместе в папку «Отчёты, 2025»");
        Require(preview.Files.Count(f => f.Selected && f.Category == "Отчёты, 2025") == 3, "punctuation in quoted destination");
        var elapsed = Enumerable.Range(0, 21).Select(_ =>
        {
            var start = Stopwatch.GetTimestamp();
            service.Preview("Сделай разбивку платёжных счетов по годам, а снимки с экрана пускай лежат где лежали");
            return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }).Order().ToArray();
        Console.WriteLine($"PROMPTS: 5 learned variants accepted, 7 unsupported commands rejected; median {elapsed[10]:F1} ms with 5 files.");
    }

    static void MultilingualPrompts(Store store, string desktop)
    {
        var samples = new (string Year, string Move, string Type, string Topic, string Icons, string Photo)[]
        {
            ("Разложи счета по годам, а скриншоты оставь на столе", "Перенеси архивы в папку «Резерв, 2026»",
                "Разложи файлы по типам", "Разложи документы по темам", "Поменяй значки на рабочем столе", "Поменяй значок папки на фото"),
            ("Sort invoices by year and leave screenshots on the desktop", "Move archives into the folder ‘Резерв, 2026’",
                "Sort files by type", "Group documents by topic", "Change desktop folder icons", "Change the folder icon to photos"),
            ("把发票按年份整理，截图留在桌面", "将压缩包移到文件夹「Резерв, 2026」",
                "按类型整理文件", "按主题整理文档", "更改桌面文件夹图标", "将文件夹图标改为照片"),
            ("請求書を年別に整理して、スクリーンショットはデスクトップに残して", "アーカイブをフォルダー「Резерв, 2026」に移動して",
                "ファイルを種類別に整理して", "文書をテーマ別に整理して", "デスクトップのフォルダーアイコンを変更して", "フォルダーアイコンを写真に変更して"),
            ("Sortiere Rechnungen nach Jahren und lass Screenshots auf dem Desktop", "Verschiebe Archive in den Ordner „Резерв, 2026“",
                "Sortiere Dateien nach Typ", "Gruppiere Dokumente nach Thema", "Ändere die Ordnersymbole auf dem Desktop", "Ändere das Ordnersymbol zu Fotos"),
            ("Розклади рахунки за роками, а знімки екрана залиш на робочому столі", "Перенеси архіви в папку «Резерв, 2026»",
                "Розсортуй файли за типами", "Розклади документи за темами", "Зміни значки папок на робочому столі", "Зміни значок папки на фото"),
            ("Раскладзі рахункі па гадах, а здымкі экрана пакінь на працоўным стале", "Перамясці архівы ў папку «Резерв, 2026»",
                "Адсартуй файлы па тыпах", "Раскладзі дакументы па тэмах", "Змяні значкі папак на працоўным стале", "Змяні значок папкі на фота"),
            ("Ordena las facturas por año y deja las capturas de pantalla en el escritorio", "Mueve los archivos comprimidos a la carpeta «Резерв, 2026»",
                "Ordena los archivos por tipo", "Agrupa los documentos por tema", "Cambia los iconos de las carpetas del escritorio", "Cambia el icono de la carpeta a fotos"),
            ("Organize as faturas por ano e deixe as capturas de tela na área de trabalho", "Mova os arquivos compactados para a pasta “Резерв, 2026”",
                "Organize os arquivos por tipo", "Agrupe os documentos por assunto", "Altere os ícones das pastas da área de trabalho", "Troque o ícone da pasta por fotos"),
        };
        File.WriteAllText(Path.Combine(desktop, "invoice 2025.txt"), "Invoice");
        File.WriteAllText(Path.Combine(desktop, "Screenshot.png"), "");
        File.WriteAllText(Path.Combine(desktop, "archive.zip"), "");
        var service = new DesktopCleanup(store, desktop);
        foreach (var sample in samples)
        {
            var preview = service.Preview(sample.Year);
            Require(preview.Files.Single(f => f.Name == "invoice 2025.txt") is { Category: "Счета/2025", Selected: true }, sample.Year);
            Require(preview.Files.Single(f => f.Name == "Screenshot.png") is { Selectable: false, Reason: "Оставить по запросу" }, sample.Year);
            Require(service.Preview(sample.Move).Files.Single(f => f.Name == "archive.zip") is { Category: "Резерв, 2026", Selected: true }, sample.Move);
            Require(service.Preview(sample.Type).Files.Count(f => f.Selected) == 3, sample.Type);
            Require(service.Preview(sample.Topic).Answer.Contains("по темам"), sample.Topic);
            Require(service.Preview(sample.Icons).SuggestFolderIcon, sample.Icons);
            Require(service.Preview(sample.Photo).FolderIcon == "Фото", sample.Photo);
        }
        File.WriteAllText(Path.Combine(desktop, "明細の請求書2023.txt"), "お支払い");
        File.WriteAllText(Path.Combine(desktop, "画面のスクリーンショット.png"), "");
        var chinese = service.Preview("请按年份整理发票，并且别动截图");
        Require(chinese.Files.Single(f => f.Name == "明細の請求書2023.txt") is { Selected: true, Category: "Счета/2023" },
            "recognizes invoices in CJK filenames");
        Require(chinese.Files.Single(f => f.Name == "画面のスクリーンショット.png") is { Selectable: false, Reason: "Оставить по запросу" },
            "recognizes screenshots in CJK filenames independently of command language");
        Require(service.Preview("Please, move archives into 'Research and Development'").Files.Single(f => f.Name == "archive.zip").Category ==
            "Research and Development", "quoted folder preserves capital letters and conjunction");
        Require(service.Preview("Organize my desktop").Summary?.Steps.Single().Mode == "type", "generic sort defaults to category grouping");
        Require(service.Preview("Sort photos and documents by type").Summary?.Steps.Length == 2, "conjoined subjects both reach the plan");
        var japanese = service.Preview("アーカイブをフォルダー「予備」に移動して");
        var archive = japanese.Files.Single(f => f.Name == "archive.zip");
        Require(File.Exists(Path.Combine(desktop, "archive.zip")) && archive.Selected, "localized command only previews before confirmation");
        Require(service.Move(japanese.Id, [archive.Id]).Count == 1 &&
            File.Exists(Path.Combine(service.Folder, "予備", "archive.zip")), "localized plan uses the original move pipeline");
        Require(service.Undo().Count == 1, "localized move is reversible");
        foreach (var invalid in new[] { "Move archives to Backup and delete screenshots", "Move archives to folder \"../outside\"",
            "Don't sort files by type", "不要按类型整理文件", "Ordne Dateien nach Typ und lösche Screenshots",
            "Cambia el icono de la carpeta a fotos y elimina los documentos", "Move archives to Backup and screenshots delete" })
            Reject(() => service.Preview(invalid));
        Console.WriteLine("PASS multilingual cleanup commands across 9 languages");
    }

    static void Benchmark(Store store, string desktop)
    {
        // Independent synthetic files. Generic names exercise content-based sorting.
        (string Folder, string Text)[] samples =
        [
            ("Финансы", "Банковская выписка. Комиссии за операции, дебет и остаток на конец месяца."),
            ("Финансы", "Расходы по подразделениям. Сальдо, сумма поступлений и остаток кассы."),
            ("Финансы", "Invoice for consulting hours. Payment due, amount in euros, bank details."),
            ("Финансы", "Сверка оплаты, итоговая стоимость, сумма НДС за квартал."),
            ("Финансы", "Monthly budget variance and forecast for revenue and operating expenses."),
            ("Договоры и право", "Договор поставки. Стороны установили срок действия, обязательства и порядок расторжения."),
            ("Договоры и право", "Доверенность для представителя. Указаны полномочия и срок действия."),
            ("Договоры и право", "Lease agreement amendment. Tenant and landlord agreed on renewal terms."),
            ("Договоры и право", "Суд принял исковое заявление. Истец представил доказательства и расчёт неустойки."),
            ("Договоры и право", "Nondisclosure agreement covers confidential information and obligations of the parties."),
            ("Проекты", "Портфолио: кейс автоматизации, стек приложения, архитектура и реализация проекта."),
            ("Проекты", "Критерии приёмки релиза, задачи спринта и сценарии интеграции."),
            ("Проекты", "Product roadmap update. Review upcoming milestones, deliverables and blockers."),
            ("Проекты", "Протокол встречи команды. Решения по запуску, ответственные и сроки выполнения."),
            ("Проекты", "QA deployment checklist. Run integration tests and record acceptance criteria."),
            ("Учёба", "Экзаменационные билеты по физике. Темы лекций и вопросы для подготовки."),
            ("Учёба", "Семинар по экономике. Студенты сдают упражнения и домашнее задание."),
            ("Учёба", "Course syllabus for the autumn semester with homework and exam dates."),
            ("Учёба", "Лабораторная по химии. Преподаватель проверяет ход эксперимента и выводы."),
            ("Учёба", "Research thesis bibliography and notes from the academic supervisor."),
            ("Личное", "Рецепт лимонного кекса. Ингредиенты, приготовление теста и температура духовки."),
            ("Поездки", "Маршрут отпуска: авиабилеты, бронирование отеля, время заселения."),
            ("Личное", "Shopping list for dinner with family: onions, pasta and tomatoes."),
            ("Личное", "Приглашение на день рождения, список гостей и семейные фотографии."),
            ("Поездки", "Holiday itinerary: airport transfer, hotel booking and return flight."),
            ("", "Копия файла. Данные, черновик, образец выгрузки."),
            ("", "Проверить изменения. Второй вариант, ещё обсуждается."),
            ("", "Примечание к версии. Посмотреть утром."),
            ("", "Договор аренды, выписка, маршрут, лабораторная работа."),
            ("", "Discussion, rough draft, examples and links for later."),
        ];
        for (var index = 0; index < samples.Length; index++)
        {
            var (folder, content) = samples[index];
            if (index % 3 == 0)
            {
                using var zip = ZipFile.Open(Path.Combine(desktop, $"file-{index}.docx"), ZipArchiveMode.Create);
                using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open());
                writer.Write("<document><text>" + System.Security.SecurityElement.Escape(content) + "</text></document>");
            }
            else File.WriteAllText(Path.Combine(desktop, $"file-{index}.txt"), content);
        }
        var service = new DesktopCleanup(store, desktop);
        var warm = service.Preview();
        Require(warm.ModelAvailable, "bundled fastText weights loaded");
        var selected = warm.Files.Where(file => file.Selected).ToArray();
        string Expected(DesktopCleanup.FileRow file) => samples[int.Parse(Path.GetFileNameWithoutExtension(file.Name)[5..])].Folder;
        var correct = selected.Count(file => Expected(file) == file.Category);
        var unknownUnchecked = warm.Files.Count(file => Expected(file) == "" && !file.Selected);
        var timings = new double[25];
        for (var index = 0; index < timings.Length; index++)
        {
            var timer = Stopwatch.StartNew();
            service.Preview();
            timings[index] = timer.Elapsed.TotalMilliseconds;
        }
        Array.Sort(timings);
        Console.WriteLine($"FASTTEXT: {correct}/{selected.Length} checked correct, {correct}/25 clear examples checked correctly, {unknownUnchecked}/5 ambiguous unchecked; {samples.Length} files median {timings[12]:F1} ms, p95 {timings[23]:F1} ms.");
        Require(correct >= 15 && correct == selected.Length && unknownUnchecked == 5, "benchmark precision and coverage");
    }

    static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Desktop cleanup: " + name);
    }

    static void Reject(Action action)
    {
        try { action(); }
        catch (CommandFault) { return; }
        throw new InvalidOperationException("Desktop cleanup: invalid selection accepted");
    }
}

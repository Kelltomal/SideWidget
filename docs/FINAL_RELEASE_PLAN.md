# Финальный выпуск SideWidget 0.2.0 win-x64

Статус: координатор разрешил автоматические проверки, isolated publish и ZIP кандидата. Они отражаются в CANDIDATE_REPORT.md. Native CAPTCHA smoke остановлен пользователем; GUI/restart и окончательная приёмка требуют продолжения после разрешения. Рабочий release и общий финальный ZIP не заменяются. Git и initial commit выполняет координатор.

## 1. Зафиксировать готовность исходников

- Получить результат последнего motion QA: CompositionTarget.Rendering, fixed-size host reveal, click-release/hold/cancel, drag по верхнему краю, resize, рамка, форма и timer-dismiss. Включить исправления, сделанные после предыдущего publish.
- Подтвердить отсутствие текущих изменений общих файлов и сборок в других чатах. На время финального слота все владельцы прекращают запись исходников и dist. Если исходники меняются после тестов, выпуск останавливается и затронутые проверки повторяются.
- Проверить текущий состав: музыка без заголовка, управление previous/play/next в одной строке; таймер как восьмая плитка «Под рукой»; MyMemory и скрытые Google/Яндекс в своих полях, без видимых сайтов; SellyBoard client и native credential form; актуальная геометрия/жесты.
- Сверить README, PRODUCT, DESIGN, SPEC, FEATURE_MATRIX, SECURITY и TEST_REPORT с кодом. Сейчас FEATURE_MATRIX содержит прежние ограничения календаря/desktop-доступа; владелец интеграции должен отделить реализованный клиент от ещё не подтверждённого production API. Старые performance JSON не выдавать за новые измерения. Не включать прежние числа тестов как результат текущей сборки.
- Данные `%LocalAppData%/SideWidget`, ключи, профили WebView2 и локальные черновики не изменять сборочными командами. Не очищать пользовательский профиль ради smoke.

## 2. Общие автоматические проверки

Из корня проекта, после разрешения координатора:

```powershell
& ./scripts/test-release.ps1
```

Скрипт выполняет Release C# suite, все web-тесты, TypeScript/Vite build и Windows harness. Логи каждого шага и exit codes: `artifacts/verification/<timestamp>/`. Fail-fast при ошибке. Harness читает системные метрики/audio/media; launcher-тест работает с собственными временными данными. Скрипт не запускает SideWidget, не публикует и не упаковывает.

Опциональные реальные публичные запросы — отдельный запуск с `-PublicOnline`, только если нужны свежие сетевые результаты. Он не доказывает живую авторизацию SellyBoard/DeepL и не заменяет GUI-проверку MyMemory. Системные shutdown/restart/sleep/lock не выполнять при smoke.

## 3. Чистый кандидат

В том же эксклюзивном слоте создать уникальный каталог, не переиспользовать release, translation-embedded-check или другие staging:

```powershell
$candidate = Join-Path $PWD ('artifacts/final-release-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
if (Test-Path -LiteralPath $candidate) { throw 'Candidate must be new.' }
& ./.tools/dotnet/dotnet.exe publish src/SideWidget.App -c Release -r win-x64 --self-contained true -o $candidate
if ($LASTEXITCODE) { throw 'Native publish failed.' }
```

Проверить 0 errors, warnings разобрать явно. Каждый файл Web в кандидате должен совпадать с текущим `src/SideWidget.Web/dist` по SHA256; списки assets должны совпадать полностью. Не смешивать DLL/JS из разных staging. Проверить наличие App.xaml-generated metadata по результату native формы, не требовать отдельный App.xbf: он может быть встроенным ресурсом.

## 4. Native smoke именно кандидата

После явной передачи GUI штатно закрыть работающий SideWidget, проверить завершение процесса, запустить `$candidate/SideWidget.App.exe --inspect`. Не использовать Stop-Process. Без новых движений мышью других чатов.

1. Открытие/сворачивание, удержание шапки, отмена жеста, drag, resize, hotkey; отсутствие белой рамки, остающаяся шапка, smooth reveal. Сверить свежий motion QA с кандидатом.
2. Музыка: новая компактная строка, раскрытие/сворачивание, копирование трека. Контроль воспроизведения — только существующая пользовательская сессия и согласованный GUI-сценарий; не запускать новый медиаконтент.
3. Таймер: восьмая плитка, поля/статус, сигнал в свёрнутой шапке, dismiss без непреднамеренного раскрытия. Не сбрасывать активный пользовательский таймер ради теста; при наличии оставить этот сценарий отдельной отмеченной проверкой.
4. Перевод: MyMemory `Hello world`, результат в нашем поле, копирование; Google и Яндекс: реальные переводы в нашем поле, сайт не появляется; отмена загрузки и повторный запрос. Не отправлять пользовательский текст. DeepL форма — пустой Save, Cancel/Escape/reopen, без настоящего ключа.
5. SellyBoard: календарь/форма, masked key, Cancel/Escape и видимая ошибка запрещённого HTTP URL. Реальные credentials/задачи не менять; отличать synthetic UI от production подключения.
6. Калькулятор и история, QR/копирование, единицы, цвет, сохранённые закреплённые приложения и порядок модулей. Пользовательские записи не удалять; тестовые изменения учитывать и откатывать адресно.

Записать фактические результаты и непроверенные сценарии, последние Windows Application Error для нашего процесса; не объявлять отсутствие ошибок по старым логам. Если код исправлен, повторить проверки затронутого пути и собрать новый кандидат.

## 5. Состав поставки и упаковка

В поставку входят только файлы нового publish-каталога плюс актуальная документация:

- SideWidget.App.exe и DLL приложения/Core/Infrastructure; self-contained .NET/Windows App SDK dependencies, ресурсы, конфигурации runtime/deps, WebView2 loader и встроенный read-result.js для скрытых переводчиков.
- Web/index.html, текущие JS/CSS assets, локальные Medeo Sans/Mono WOFF2.
- Assets/Fonts/MedeoSans.ttf, соответствующие OFL и остальные лицензии; Everything64.dll и Everything SDK license.
- Assets/Icons/sidemenu.ico — нативный значок приложения и трея.
- README, PRODUCT, DESIGN, docs/*.md без `*COORDINATION*`; JSON измерений, копируемые package.ps1, явно помеченные как исторические, если не измерены заново.

Исключены `.tools`, исходники/tests, node_modules, docs/archive, reset-backup, screenshots/output, пользовательские DB/WAL/SHM, secrets, WebView2/TranslationWebView2 profiles, логи с пользовательскими данными, старый Python runtime и удалённые серверные модули. PDB из publish допустимы: отдельно проверить отсутствие секретов/данных, не считать их исходниками продукта.

Только после успешного smoke и разрешения на итоговую упаковку:

```powershell
& ./scripts/package.ps1 -ReleaseDirectory $candidate
```

Для отдельного непринятого кандидата разрешён параметр `-OutputArchive artifacts/SideWidget-0.2.0-candidate-<timestamp>-win-x64.zip`; он не перезаписывает общий финальный ZIP. Статус pending указывается в CANDIDATE_REPORT.md внутри поставки.

Результат: `artifacts/SideWidget-0.2.0-win-x64.zip` и `.zip.sha256`. Перед заменой прежнего архива сохранить его и checksum в адресный backup внутри artifacts, если требуется откат. Архив проверять после закрытия ZIP-потока: SHA256 совпадает с sidecar; обязательные файлы и Medeo присутствуют; все бинарные файлы/веб-файлы совпадают с проверенным кандидатом; JS/CSS assets только актуальные; запрещённых путей нет. Не копировать файлы из пользовательского профиля.

После упаковки исходники не изменять без нового цикла. Координатор получает путь, SHA256, размер, текущие числа проверок, Windows smoke, ограничения SellyBoard/DeepL/системных функций и список ещё не реализованного. Только затем координатор проверяет состав и делает initial Git commit.


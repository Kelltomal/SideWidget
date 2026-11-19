# Решения и фактический журнал

- Исходная папка пуста, Git и чужие исходники отсутствовали. Создан один проект на заданной архитектуре; соседние проекты не изменялись.
- SDK: локальный .NET 10.0.403, Windows App SDK 1.8.260921001, self-contained x64. WebView2 Runtime 156.0.4314.58 найден на машине. Никаких установщиков SDK/Runtime не запускалось. Первый запуск dotnet автоматически создал стандартный development certificate; доверие к нему не добавлялось, HTTPS fixture его не использует.
- Механика сначала проверена с двумя пустыми карточками. `--prototype --clip` держит HWND раскрытой высоты и обрезает регион; захват показывает большую прозрачную область. Выбран resize HWND: свёрнутая геометрия действительно 444×52; содержимое WebView2 пересчитывается. Гладкость/FPS по одному MoveAndResize не заявляются. Оба пути доступны для повторной проверки.
- Пользователь уточнил шрифт: Medeo вместо Segoe UI. Использованы локальные файлы `Downloads/Medeo-RC3`, лицензия OFL 1.1 включена. Шрифты встроены в release, системная установка не требуется.
- Изначальный pip 26.1.2 несовместим с pip-tools 7.5.2 (`allow_all_prereleases`). В `.venv` закреплён pip 25.3; lock-файлы получены pip-compile с хешами.
- Уязвимый транзитивный SQLite 2.1.11 заменён на SQLitePCLRaw bundle 3.0.5; Microsoft.Data.Sqlite 10.0.14. Vite/Vitest обновлены по npm audit до 7.3.7/5.1.0; audit после обновления: 0.
- Попытка подключить XamlControlsResources в полностью программном App привела к crash запуска. Удалена; release повторно запущен вне IDE. Нативные диалоги используют штатные стили и явный Medeo FontFamily.
- Независимый review выявил reduced-motion геометрию, утечку JSON-экранированного токена, повторные JSON-поля, неопределённую отмену записи и backoff HTTP429. Исправления внесены; секрет/duplicate/cancel покрыты интеграционными регрессиями.

- Чёрная шапка при первом показе воспроизводилась и без WebView2. Причина — закэшированная non-client геометрия после SetWindowLong. Однократный SetWindowPos с SWP_FRAMECHANGED после изменения стилей устранил дефект; дальнейшая анимация не пересчитывает рамку принудительно. Первый кадр проверен повторными запусками.
- SQLite PRAGMA max_page_count не сохраняется между соединениями. Регрессионный тест сначала упал; лимит перенесён в Open каждого соединения, suite вырос до 50 проверок C#.
- Проверка на ширине 360 обнаружила сохранение прокрутки при навигации. Прокручиваемый main теперь пересоздаётся по page; данные задач остаются в App. UI-review потребовал реальное updatedAt на главной; добавлено и повторно проверено на 444/360.

## Реально применённые навыки

| Навык | Результат |
|---|---|
| caveman | Краткие сообщения; документация и UI написаны обычным русским |
| using-superpowers, brainstorming, writing-plans | Исследование и план по уже заданной архитектуре; без повторного согласования ТЗ |
| executing-plans, test-driven-development | Поэтапная реализация, красные проверки протокола/worker до реализации, интеграционный suite |
| systematic-debugging | SDK, pip-tools, WinRT CreateWithOptionsAsync, crash ресурсов |
| impeccable | Context launcher, craft floor, фактические post-edit hooks, ручной detect без находок |
| design-taste-frontend | App-screen направление true-black-flat; пользовательская палитра и механика имеют приоритет |
| emil-design-eng, animate | Короткое обратимое раскрытие, без анимации клавиатурных действий |
| computer-use | Реальный WinUI/WebView2, POST-подтверждение, Windows picker, предпросмотр изображения |
| requesting-code-review, receiving-code-review | Независимая проверка кода и исправление подтверждённых дефектов |
| verification-before-completion | Финальная сборка и проверки фиксируются в TEST_REPORT |

## Официальные источники

- https://learn.microsoft.com/en-us/windows/apps/develop/ui/windowing-overview — связь Window/AppWindow/HWND.
- https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/webview2 — WinUI WebView2.
- https://github.com/MicrosoftDocs/edge-developer/blob/main/microsoft-edge/webview2/concepts/user-data-folder.md — CreateWithOptionsAsync и профиль в LocalAppData.
- https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security — origin, навигация и сообщения.
- https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput — параллельное чтение потоков.
- https://docs.python.org/3.13/using/windows.html — embeddable runtime, зависимости подготавливаются при сборке.
- https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection — DPAPI CurrentUser.
- https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects — управление процессами, не песочница.
- https://github.com/DNFDEVS/medeo-font — происхождение Medeo; локальная OFL в licenses.


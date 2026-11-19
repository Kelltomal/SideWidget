# Кандидат SideWidget 0.2 — 5 ноября 2026

Это отдельный кандидат, НЕ окончательно принятый релиз. После первоначального Escape пользователь разрешил продолжить GUI. Переводчики проверены в предыдущих staging; итоговый кандидат с последними music/wheel изменениями ожидает запуска после закрытия появившегося стороннего процесса artifacts/release. Общий опубликованный ZIP не перезаписывается.

## Свежая автоматическая проверка

scripts/test-release.ps1, логи artifacts/verification/20261105-035618:

- C#: 932 checks PASS.
- Web: 142 tests PASS в 11 файлах.
- TypeScript/Vite: PASS; актуальные assets index-CmYM7igJ.js, index-C7sgShrV.css.
- Windows harness: 6 PASS / 0 FAIL / 1 SKIP. Системные метрики, Core Audio, GSMTC, Everything, connectivity validation, shortcut/Undo fixture. Публичные сетевые пробы пропущены. Эти проверки не доказывают выполнение команд управления.
- Native self-contained win-x64 publish: PASS, без ошибок/предупреждений, artifacts/candidate-20261105-035803.

Состав и SHA256 ZIP фиксируются отдельно рядом с архивом после упаковки. Исторические performance-reset*.json / startup-reset.json не являются замерами этого кандидата.

## Что удалено

Серверные/GitLab/VPN/CRM/маркетплейсные экраны, API Builder и Python Worker/runtime исключены из активного продукта. Старые данные сохранены; архив прежнего направления не поставляется. Стек C# + WinUI3 + WebView2 и встроенный Medeo сохранены.

## Что реализовано

Локальные калькулятор/история, QR, единицы, цвета/пипетка, закрепление программ; Everything, музыка/звук/показатели, погода/курсы, локальный дневник и таймер. Шторка с неподвижной шапкой, drag/resize/hotkey; трей с перезапуском, новые сгруппированные быстрые действия и подсказки. Календарный клиент SellyBoard. MyMemory и скрытые Google/Яндекс в собственных полях; новая ручная CAPTCHA внутри шторки.

## Что проверено в Windows

Предыдущие отдельные staging: расчёт и clipboard, единицы, сохранение/декодирование QR, picker, поиск Everything, запуск закреплённого Notepad, drag/resize/hotkey, MyMemory и форма ключа DeepL. Финальный motion staging: click-release, отсутствие hover-open, drag, диалог, timer-dismiss. SellyBoard native форма проверена с synthetic key. Полные свидетельства — TEST_REPORT.md, MOTION_VERIFICATION.md, TRANSLATION_UPDATE.md.

Native Google и Яндекс: Hello world = «Привет, мир» в собственном поле, clipboard совпал. Яндекс повторно: Good morning = «Доброе утро», устаревший ответ не вернулся. Кнопка «Отмена» в показанной странице проверки возвращает локальный UI без потери текста. Скриншоты output/playwright/google-hidden-native.png и yandex-hidden-native.png.

Чистый тестовый профиль показал: /showcaptchafast — автоматическая проверка с hidden inputs, затем сайт сам возвращается на /. Это не ручная CAPTCHA. Исправлено её ложное раскрытие и распознавание скрытых CAPTCHA iframe; временная диагностика удалена. Полная ручная CAPTCHA, её успешное решение и Escape при фокусе внутри WebView НЕ подтверждены: сервис больше не запросил ручную проверку. Поддержка реализована, но не объявляется принятой.

Точечный фикс SellyBoard: HTTP200 с пустым data больше не очищает pending создание. Успех требует data.task со строковым id и числовой version; ошибочный envelope/неверный id отклоняются. Regression RED/GREEN; полный DTO/version и исходный client_key сохраняются.

## Ключи и внешние контракты

MyMemory/Google/Яндекс не требуют ключей. DeepL требует личный API-ключ, реальный запрос не проверен. По отчёту владельца API, SellyBoard production https://sellyboard.duckdns.org, source3d25e45e: временный read key дал options/events200, write403, после отзыва401. Личный ключ данного SideWidget не подключён. Broad CRM suite НЕ объявлен green: pre-existing ABC failure, использованы focused проверки; пересекающиеся группы не суммируются. Серверный учёт времени и интеграция дневника требуют дальнейшей работы по контракту.

## Что осталось

Native CAPTCHA/общая приёмка кандидата, mixed DPI/multi-monitor и отдельные команды Windows; полная синхронизация дневника, reminders, полный CRUD задач/серверный timer, GPU/температуры/активность диска, безопасный restart Explorer и прочие пункты FEATURE_MATRIX.md. Неподдерживаемые системные функции не заменены фиктивными переключателями.

Доступность: календарь360/444 без overflow по отдельному headless отчёту; existing secondary #A9B1BA на #24292D имеет WCAG6.77:1, APCA|Lc|56 ниже target60. Полная a11y-приёмка не заявляется. Новая CAPTCHA поверхность визуально не принята. Подпись бинарника не выполнялась.


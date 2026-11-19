# Phone Remote Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Управление музыкой ПК с Android через запоминаемый Wi-Fi-пульт с установкой из Chrome.

**Architecture:** Отдельный Kestrel-хост с узким API и отдельной React-страницей. Существующие MediaService/AudioService выполняют команды; desktop bridge только настраивает сервер и подтверждает устройства. HTTPS с локальным CA обеспечивает secure context для устанавливаемого PWA; HTTP отдаёт только первоначальную инструкцию и публичный сертификат.

**Tech Stack:** .NET 10, ASP.NET Core/Kestrel, WinUI 3, React 19, TypeScript, Vite, Web App Manifest/service worker.

**Spec:** docs/superpowers/specs/2026-11-05-phone-remote-design.md; дополнения пользователя: установка приложения из Chrome и независимость от VPN.

## Global Constraints

- Домашняя сеть; без облака, Bluetooth, системной службы и доступа к общему Controller по HTTP.
- Разрешены музыка и общая громкость выхода; микрофон, файлы, запуск программ и прочие модули закрыты.
- Сохранять существующий дизайн SideWidget и старые данные. Автозапуск использовать существующий StartupService.
- Привязка к выбранному домашнему адаптеру/локальному IPv4; не следовать маршруту VPN. Kill switch, блокирующий LAN, требует разрешения LAN в VPN.
- Для настоящего secure-context PWA использовать HTTPS и публичный CA-сертификат с ручным доверием Android. Ключ CA хранить через DPAPI; на телефоне сверяется SHA-256 fingerprint, видимый в SideWidget.
- Скрытая вкладка Android не гарантирует фоновой работы; восстановление при открытии. IP/порт должны сохраняться; рекомендовать DHCP reservation.

## Review Focus

- Потеря ответа команды не должна повторять next/previous; тест сетевого отказа клиента.
- Отзыв/выключение должен запрещать дальнейшие команды; интеграционные HTTP-тесты.
- Смена VPN не должна выбирать VPN IP; тест фильтра адаптеров и сохранения выбранного ID.
- Service worker не должен кэшировать API, авторизацию и устаревшие управляющие ответы; тест файлов manifest/SW и поведенческая проверка браузером.
- Сеть/порт недоступны после старта: понятное состояние, повторное восстановление, отсутствие дубля сервера; интеграционная проверка stop/restart.

### Task 1: Доверие и сертификаты

**Files:** `src/SideWidget.Remote/RemoteAccess.cs`, `RemoteContracts.cs`, `SideWidget.Remote.csproj`; `src/SideWidget.Infrastructure/RemoteCertificates.cs`; `tests/SideWidget.Remote.Tests/*`.

**Interfaces:** `RemoteAccess.CreateInvite()`, `BeginPair(invite,name)`, `PollPair(id,secret)`, `Decide(id,approved)`, `Authorize(token)`, `Revoke(id)`; `RemoteCertificates.Get(IPAddress[])`, `ExportRoot()`, `Fingerprint`.

- [x] Написать и запустить падающие проверки отсутствующей реализации: приглашение одноразовое/5 минут, подтверждение обязательно, неверный секрет отклонён, доверие сохраняется после перезапуска, отзыв немедленный, лимит 8 устройств.
- [x] Реализовать атомарное хранение хешей ключей, ограниченные ожидающие запросы, криптографическую случайность и проверку в constant time.
- [x] Создать сохраняемый DPAPI CA и короткоживущий TLS leaf с IP SAN/serverAuth; проверить цепочку и повторное использование CA.
- [x] Запустить тесты и записать результат.

### Task 2: Ограниченный HTTP/HTTPS-хост

**Files:** `src/SideWidget.Remote/RemoteServer.cs`, `RemoteNetwork.cs`; тесты в `tests/SideWidget.Remote.Tests/Program.cs`.

**Interfaces:** сервер принимает `RemoteAccess`, allowlist локальных endpoint/subnet, certificate, `Func<Task<object>> snapshot`, `Func<RemoteCommand,Task> command`; `StartAsync`, `StopAsync`. Сеть производит `RemoteAdapter` с ID, адресом, маской, признаком private и фильтрует VPN/virtual/tunnel.

- [x] Сначала интеграционные проверки: чужие Host/Origin, HTTP-команды, неверный токен, лишние поля и неизвестные маршруты запрещены.
- [x] Реализовать HTTPS для API и пульта; HTTP только setup/certificate. Ограничить тело, частоту/параллельность, CORS, локальные подсети и пути статических assets.
- [x] Проверить живым HttpClient: pairing/approval/command/revoke, stop/restart, HTTPS manifest/SW, отсутствие доступа к профилю и desktop UI.

### Task 3: Подключение к Windows и desktop-настройка

**Files:** `src/SideWidget.App/PhoneRemoteService.cs`, `Controller.cs`, `MainWindow.cs`, `SideWidget.App.csproj`; `src/SideWidget.Core/Protocol.cs`; `src/SideWidget.Web/src/PhoneRemoteSettings.tsx`, `WindowsPanels.tsx`.

**Interfaces:** `PhoneRemoteService.Info/Configure/Invite/Decide/Revoke/Dispose`, dispatcher callback из MainWindow. Desktop bridge: `remote.get/configure/invite/decide/revoke` с точными полями, без сетевого доступа к этим действиям.

- [x] Проверить allowlist bridge и выбор физического адаптера; обнаружить existing StartupService и использовать его.
- [x] Встроить жизненный цикл и сериализацию медиакоманд через UI dispatcher; аудио выполнять через существующий сервис. Отдавать только общую громкость, без списка процессов/микрофона.
- [x] Добавить в музыку раскрываемую настройку пульта: включение, адаптер, порты, автозапуск, QR setup и HTTPS, fingerprint, подтверждение кода, список устройств/отзыв, ошибки firewall/VPN/сети.
- [x] Сохранять выбранный адаптер; восстанавливать listener при изменении сети без переезда на VPN. Проверить native сборку.

### Task 4: Мобильный пульт и PWA

**Files:** `src/SideWidget.Web/remote.html`, `vite.config.ts`, `src/remote/*`, `public/remote/*`; тесты клиента и UI.

**Interfaces:** `/api/pair/start`, `/api/pair/poll`, `/api/state`, `/api/command`; manifest `/remote/manifest.webmanifest`, SW `/remote/sw.js`, scope `/` на отдельном сетевом origin.

- [x] Написать падающие проверки клиента: сохранение ключа, 401 сбрасывает доверие, команда не повторяется после таймаута; UI показывает disabled при потере связи.
- [x] Реализовать setup по HTTP: скачать CA, сверить fingerprint на ПК, инструкция установки Android, переход HTTPS с сохранением одноразового приглашения во fragment.
- [x] Реализовать pairing, media/capability controls, commit sliders, общую громкость, reconnect при online/visibility, кнопку установки с beforeinstallprompt и честный fallback инструкции.
- [x] Manifest: name/short_name/id/start_url/scope/display standalone, PNG icons 192/512 и maskable. SW кэширует только оболочку, никогда API; offline экран показывает отсутствие связи, не активные кнопки.
- [x] Проверить мобильные размеры/обе темы, long names, keyboard, setup/error/revoked/offline и отсутствие overflow.

### Task 5: Приёмка, обзор и поставка

**Files:** `docs/PHONE_REMOTE.md`, `README.md`, `docs/FEATURE_MATRIX.md`, `docs/SECURITY.md`, `scripts/test.ps1`.

- [x] Выполнить C#/web suite, HTTP integration, native publish и проверить состав portable; не перезаписывать запущенную сборку. Publish выполнен назначенным combined-владельцем, payload проверен чтением файлов; physicalAndroid/nativeRemoteQR остаются отдельной приёмкой.
- [x] Осмотреть реальный браузер, выполнить focused Impeccable detector; не заявлять автоматические hooks без evidence.
- [x] Отдельный fresh reviewer проверяет сетевую границу, сертификаты/ключи, reconnect/VPN и регрессии. Исправить существенные находки, повторить затронутые проверки.
- [x] Документировать реальный результат и непроверенное на физическом Android. Дать пользователю сборку и короткий путь включения/установки.

## Execution ledger

- Ruling: выполнять в текущем каталоге — repository имеет unborn main и весь продукт untracked; worktree без snapshot потерял бы существующий проект. Не создавать начальный коммит пользовательского продукта как побочный эффект.
- Ruling: пользователь прямо подтвердил реализацию; выполнять inline без дополнительного согласования плана.
- Ruling: новая просьба об установке заменяет HTTP-only пункт утверждённой схемы на HTTPS с локальным CA. CA не устанавливается автоматически на Windows/Android, пользователь доверяет ему сам.
- Pre-flight: Task 1 produces RemoteAccess/certificate, Task 2 consumes them; Task 3 supplies server callbacks/configuration, Task 4 uses Task 2 routes; единые DTO/имена маршрутов в RemoteContracts и клиенте.


Task 1: complete — trust, one-time pairing, encrypted persistent CA, fake-clock expiry checks.
Task 2: complete — live local HTTPS integration41PASS, negative Origin/Host/HTTP/schema tests, delayed-body/queued revoke, artwork versioning.
Task 3: complete (source) — once-only DesktopState remote ownership coordinated with multi-monitor task; native Release buildPASS0warnings0errors. Actual native settings smoke handed to combined owner.
Task 4: complete (source/Chromium) — client/UI checks; normal Chromium installabilityErrors=[]; local fixture pairing/reload/offline/reconnect/revoke; dark/light360/412/768+large text+desktopQR inspected. No physicalAndroid claim.
Task 5: verified1025core+41remote+189web21files, tsc/Vite/nativebuild; code reviewer four P2 fixed and re-review clean. Publish/payload/native smoke assigned explicitly by coordinator to multi-monitor task01a1a97a, no parallel build/publish/GUI. PhysicalAndroid/LAN/VPN/startup/firewall pending. Source frozen, own fixture/dev/Chromium stopped. Global initialcommit/package central; unborn Git repo left intact.
Ruling: true PWA uses HTTPS+manual AndroidCA rather than HTTP shortcut — required by user installation request, no automatic system trust changes.
Ruling: fixedID physical adapter/IP, no default-route/VPN selection — user VPN constraint; kill-switchAllowLAN and stableDHCP remain explicit external prerequisites.
Ruling: no separate nativepublish/GUI — direct coordinator slot assignment verified against user's project-wide coordination instruction; handoff written to UI_COORDINATION and reported centrally.

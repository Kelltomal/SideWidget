# Контракт SellyBoard: календарь, задачи и личный дневник

## Действующий контракт интеграции — 5 ноября 2026

Опубликован на `https://sellyboard.duckdns.org`, source release `3d25e45e`.
В `main` включён тот же код. Отдельный `/api/desktop` вход не используется.

В существующем окне [API SellyBoard](https://sellyboard.duckdns.org/settings/api) создаётся
отдельный календарный ключ с выбранным пользователем текущей компании и сроком действия.
Нужен `calendar:read`; для изменений также `calendar:write`. Смешивание с сервисными scopes
запрещено. Старые company keys не получают личный доступ. Ротация сохраняет пользователя,
компанию и исходный срок. Текущие членство, локальное approval, календарные ACL и тариф
проверяются на сервере при каждом обращении.

| Метод | Маршрут | Данные |
| --- | --- | --- |
| GET | `/api/v1/calendar/options` | `account_id`, `company_id` — десятичные строки; `timezone`, `teams`, `can_write` |
| GET | `/api/v1/calendar/events` | `start`, исключительный `end`, IANA `timezone`; существующие календарные фильтры, период 1–62 дня |
| GET | `/api/v1/calendar/tasks/{id}` | Полная карточка; необязательный `occurrence` |
| POST | `/api/v1/calendar/tasks/save` | JSON полной карточки; стабильный `client_key` при создании; актуальный `version` при обновлении |
| POST | `/api/v1/calendar/tasks/delete` | JSON `id`, `version`, явный `scope` для повторов и `occurrence` |

Transport: `Authorization: Bearer`, JSON, существующий service API envelope `{data,meta}`
или `{error:{code,message,...}}`; cookies/CSRF для этих маршрутов не нужны. ID int64,
включая вложенные ID повторов и участников, передаются строками; bool/float ID отвергаются.
Ошибки 401/403/404/409/429/503 не запускают автоматический повтор записи.

SideWidget реализует календарь, доступные задачи на дату/без даты, полную карточку,
изменение статуса обычной задачи и создание личной all-day задачи. При создании явно
передаются `private`, собственный assignee, пустой marketplace и исключительный end.
Повторяющиеся задачи доступны для чтения; их редактирование остаётся в SB.
Полная карточка хранится нативно и не пересобирается из нескольких UI-полей.
Неизвестный исход создания сохраняет точное тело/client_key в SQLite, привязанные к
server+company+account. После 401 или смены ключа они не теряются. Версионные обновления
после сбоя требуют явного обновления карточки. Серверный учёт времени не добавлялся;
таймер SideWidget и личный черновик дневника остаются отдельными локальными функциями.

Credential вводится нативной PasswordBox и сохраняется DPAPI CurrentUser; в WebView и
SQLite попадает только ссылка на защищённое значение. HTTPS обязателен, кроме точного
loopback при локальной QA; redirects и cookies отключены, BASE_PATH сохраняется.
Настройки порядка/отключения модулей не заменяются при загрузке календаря.

Проверено на настоящем HTTPS временным read-ключом: options/events 200, запись без scope 403,
после отзыва 401. Ключ отозван. Настоящее пользовательское подключение SideWidget не создавалось:
**следующий шаг — ввести свой персональный ключ и базовый URL в нативной форме.**
Нативный UI и ошибки проверены отдельно с синтетическим ключом; это не имитация live sync.

Ниже сохранён контракт исходного веб-календаря и дневника: он полезен для семантики
полей, ACL и повторов, но его session+CSRF transport не применяется SideWidget.

Историческая проверка до реализации интеграции: по локальным исходникам 5 ноября 2026 года. Корень источников: `E:/YandexDisk/Work/Python/ООО Согласие/CRM`. Прочитан `AGENTS.md`; CRM не изменялся. Секреты, `.env`, БД и production не открывались, сетевые запросы не выполнялись. Это проверка исходного контракта, не доказательство версии или работоспособности развёрнутого сервера.

## Исходный веб-контракт

Существующие веб-маршруты session+CSRF сохранены. Актуальная интеграционная авторизация
и новые методы описаны в разделе выше.

## Авторизация и транспорт

| Свойство | Подтверждено исходниками |
| --- | --- |
| Входящий credential | `Cookie: session=…`. `auth()` вызывает `account_registry.get_session(token)`; есть совместимость со старой сессией. В этом пути нет чтения `Authorization: Bearer`. [app.py:10117–10185][auth] |
| Получение сессии | `POST /login`, поля формы `username`, `password`, необязательный `chat_return`; ответ — перенаправление и `Set-Cookie`. Это браузерный вход, не выдача scoped access token. [app.py:17229–17255][login] |
| Срок | Сервер создаёт случайную сессию и CSRF, хранит хеш сессии, срок 12 часов. Cookie: `HttpOnly`, `SameSite=Strict`, `Max-Age=43200`, `Secure` зависит от конфигурации. [account_registry.py:315–340][session], [app.py:17501–17506][cookie] |
| GET без входа | `/api/…` получает HTTP 401, JSON `{ok:false,error:…}`. [app.py:10449–10456][get-auth] |
| POST | Общая проверка origin, затем разбор формы, сессия, поле `csrf`; календарь дополнительно сверяет CSRF. `X-CSRF-Token` не заменяет поле `csrf` для перечисленных ниже маршрутов. [app.py:13244–13340][post-auth], [calendar_web.py:95–116][calendar-post] |
| Тело запроса | `application/x-www-form-urlencoded`; календарный `save` содержит JSON-строку в поле `data`, а не сырое JSON-тело. [app.py:10187–10205][form], [calendar_web.py:103–110][calendar-post] |
| Ответ | Успех `{ok:true,result:…}`, прикладная ошибка `{ok:false,error:string}`; 400, 403, 404, 409. Календарь также явно возвращает 503 при внутреннем сбое. `Cache-Control: no-store`. [calendar_web.py:29–46][calendar-response], [knowledge_web.py:35–37][knowledge-errors] |
| Ограничения | Тарифная блокировка может возвращать 403 `billing_read_only`. Доступ и активное членство проверяются повторно на сервере. [app.py:10130–10145][billing], [calendar_tasks.py:28–47][calendar-access], [knowledge_repository.py:11–29][knowledge-membership] |
| Адрес | Ни домен, ни порт production здесь не установлены. Ниже указаны внутренние пути. Развёртывание может добавлять `BASE_PATH`; `public_path()` сохраняет этот префикс. Нельзя строить рабочий URL только из IP или отбрасывать префикс. [app.py:1283–1293][base-path] |

Проверка `same_origin()` допускает запрос без `Origin`, если `Sec-Fetch-Site` не `cross-site`; это не специальное разрешение для desktop API и не заменяет аутентификацию. Не нужно подделывать браузерный origin или извлекать cookie из чужого профиля. CSRF сейчас встраивается в HTML страницы, отдельный согласованный bootstrap интеграции в изученных обработчиках не найден. [app.py:12478–12507][origin], [app.py:9971–9980][csrf-html]

Состояния и права относятся к аккаунту/команде. Фильтрация в SideWidget не делает общую сессию ограниченным токеном. Наличие токенов у других подсистем SellyBoard не означает, что календарные маршруты принимают их.

## Календарь и задачи: существующие методы

Источники маршрутов: [calendar_web.py:73–78][calendar-get], [calendar_web.py:103–116][calendar-post].

| Метод и путь | Поля | Результат / назначение |
| --- | --- | --- |
| `GET /api/calendar/options` | Нет обязательных query-полей | `teams`, `assignees`, `employees`, `departments`, `account_id`, `timezone`, `timezone_options`, `can_write`, `can_manage_organization`. В SideWidget проецировать только необходимые поля. |
| `GET /api/calendar/events` | `start`, `end` — ISO-даты; необязательные `timezone`, `week_start`, `company_id`, `assignee_id`, `department_id`, `status`, `priority` | `{events,unscheduled,stats,timezone,truncated,can_write}`. `end` исключён; период 1–62 дня. `week_start` должен быть понедельником и пересекать период. |
| `GET /api/calendar/tasks/{id}` | Необязательный `occurrence` | Полная карточка, `version`, `can_write`, конфликты и данные продолжений серии. |
| `POST /api/calendar/tasks/save` | `csrf`, `data` — JSON-объект, описанный ниже | `{task:…}`. Создание и полное сохранение. |
| `POST /api/calendar/tasks/watch` | `csrf`, `id`, `version`, `watching` = `"1"`/`"0"`, `scope`, `occurrence` | Изменение собственной подписки наблюдателя. Не требуется для первой версии SideWidget. |
| `POST /api/calendar/tasks/delete` | `csrf`, `id`, `version`, `scope`, `occurrence` | `{deleted:true,id,version?}`; удаление экземпляра, будущих экземпляров или серии. |

В существующем API также есть фильтр/поле `marketplace`. SideWidget не должен превращать его в функцию продукта. При обновлении существующей задачи сохранить исходное значение в нативной модели; при создании обычной личной задачи использовать пустое значение. Пропуск поля при полном сохранении способен изменить данные, поскольку сервер подставляет значения по умолчанию. [calendar_tasks.py:132–160][calendar-normalize]

### Поля сохранения и чтения

`data` содержит:

- Для создания: стабильный `client_key` длиной до 128 символов; необязательные `id:null`, `version:0`.
- Для обновления: `id`, актуальный `version`, полную редактируемую карточку, а не один изменённый статус.
- `title`: 1–250 символов после trim; `description`, `notes`: до 20000 символов каждое.
- `checklist`: максимум 100 объектов `{text,done}`, текст до 500 символов, `done` — bool.
- `status`: `todo`, `in_progress`, `done`, `cancelled`; `priority`: `low`, `normal`, `high`, `urgent`.
- `visibility`: `team` или `private`, **по умолчанию `team`**. Для личного создания SideWidget обязан явно передавать `private`.
- `assignee_ids`, `observer_ids`: массивы положительных числовых account ID действующих сотрудников. `company_id` необязателен; чужая команда отвергается.
- `start`, `end`: обе даты/времени либо обе пустые; `all_day`: bool; `timezone`: имя IANA; `recurrence`: объект либо `null`.
- `scope`: `one`, `future`, `series`, по умолчанию `series`; для экземпляра серии — `occurrence` или `occurrence_start`.
- При затрагивании продолжений серии: актуальные `continuations:[{id,version,…}]` либо `continuation_versions`, дополнительно выбранные `continuation_ids`. Неполные/устаревшие версии дают 409.

Источники: [calendar_tasks.py:132–160][calendar-normalize], [calendar_tasks.py:397–437][calendar-save], [calendar_recurrence.py:66–87][schedule].

DTO содержит `id`, `company_id`, `created_by`, `updated_by`, `title`, `status`, `priority`, участников, `visibility`, `all_day`, `timezone`, `recurrence`, `version`, `created_at`, `updated_at`, `start`, `end`, `occurrence_start`, `occurrence`, `can_write`. Полная карточка дополнительно содержит `description`, `notes`, `checklist`, `client_key`; события периода содержат счётчики чек-листа без его текста. Временные значения DTO приводятся к UTC ISO; события на весь день остаются датами. [calendar_tasks.py:78–90][calendar-dto], [calendar_tasks.py:282–313][calendar-events]

Календарные JSON ID — числа; ограничение маршрута допускает bigint до 9223372036854775807. Не передавать потенциальный int64 через JavaScript `Number`: нативный C# адаптер читает ID как `long`, в мост SideWidget отдаёт строку и обратно сериализует числом для SB. В базе знаний ID уже переводятся в строки сериализатором `wire()`. [calendar_web.py:49–52][calendar-id], [chat_wire.py:5–14][wire]

### Расписание, личная выборка и повторы

Конечная дата события на весь день исключена. Положительная длительность ограничена 366 днями. Несуществующее локальное время при DST отвергается; неоднозначное время без offset выбирает первый fold. Повторы: `daily`, `weekly`, `monthly`, `dates`; `interval` 1–365, `weekdays` 0–6, `until`, для monthly — `monthdays`, для dates — до 366 дат. [calendar_recurrence.py:18–24][dst], [calendar_recurrence.py:40–87][recurrence]

`assignee_id=currentAccountId` означает «назначено мне», а не «все мои личные события». Общая выборка включает доступные командные задачи, собственные и задачи с участием аккаунта. Отдельного флага «личный календарь = только мои объекты» в изученном query-контракте нет. Для первой версии явно определить UX: «Назначено мне» и отдельная локальная проекция собственных личных задач. Не выдавать клиентскую фильтрацию за серверное ограничение прав. [calendar_tasks.py:50–53][calendar-visible], [calendar_repository.py:115–146][calendar-query]

Запрос ограничен 2000 исходными задачами и отдаёт `truncated`. Нельзя показывать результат как полный при `truncated:true`; нужны меньшие периоды/фильтры, а для гарантированной полноты — расширение серверного контракта. [calendar_repository.py:145–146][calendar-query-limit]

## Дневник и Markdown: существующие методы

Маршруты: [knowledge_web.py:53–65][knowledge-get], [knowledge_web.py:72–95][knowledge-post]. Для SideWidget первоначально использовать только `scope=personal`.

| Метод и путь | Поля | Результат / назначение |
| --- | --- | --- |
| `GET /api/knowledge/daily-options` | Нет | `{today,groups}`; `today` рассчитан в `Europe/Moscow`, не в часовом поясе компьютера. Для SideWidget лучше передавать выбранную дату явно. |
| `GET /api/knowledge/daily` | `day=YYYY-MM-DD`, `scope=personal`; `group_id` отсутствует | `{id}` либо `null`. Чтение не создаёт запись. |
| `POST /api/knowledge/daily` | `csrf`, `day`, `scope=personal` | `{article,created}`. Открывает имеющуюся или создаёт личную запись дня. Это записывающая операция, не фоновое чтение. |
| `GET /api/knowledge/articles/{id}` | Нет | Статья с `body`, `title`, `section_id`, `version`, `daily`, `capabilities`, `can_edit`, `can_delete`. |
| `POST /api/knowledge/articles/{id}` | `csrf`, `title`, `body`, `section_id` либо пустое значение, `expected_version` | Обновлённая статья. Markdown передаётся строкой, без преобразования в HTML. |
| `GET /api/knowledge/articles` | `query` до 120 символов, необязательные `section_id`, `cursor`, `limit` 1–100, по умолчанию 30 | `{rows,next_cursor,can_create,can_manage_sections}`; без полного `body`. Это поиск всех доступных статей, **не только личного дневника**. |
| `POST /api/knowledge/articles` | `csrf`, `title`, `body`, необязательный `section_id` | Создание обычной статьи, HTTP 201. Не использовать вместо `/daily`: нет идентичности дня и create-idempotency key. |
| `POST /api/knowledge/articles/{id}/delete` | `csrf`, `expected_version` | Удаление с проверкой версии и прав. Не нужно для базовой синхронизации дневника. |

Название статьи — до 200 символов, тело — до 100000; исходный Markdown и переносы сохраняются. Приватность по умолчанию — `private`. Редактирование учитывает авторство/доступ, impersonation допускает чтение, но запрещает запись. [knowledge.py:20–25][knowledge-actor], [knowledge.py:76–97][article-save], [knowledge_access.py:19–37][knowledge-access]

Личная запись идентифицируется `(company_id, day, scope='personal', owner_account_id)`, а не заголовком «Дневник …». Переименование не меняет день. `open_daily()` сериализует открытие через блокировку команды, находит уже созданную запись и возвращает её; при создании тело начинается с Markdown-заголовка. Не удалять этот текст и не заменять чужое/свежее содержимое локальным черновиком без сравнения версии. [knowledge_daily.py:16–56][daily]

API статей возвращает также `daily:{date,scope,group_id}` или `null`. Ответы используют строковые ID и ISO-даты благодаря `wire()`. [knowledge_repository.py:83–92][article-read], [employee_chats_web.py:18–19][knowledge-response], [chat_wire.py:5–14][wire]

Обычный поиск статей не имеет фильтра `daily_scope=personal` и не возвращает `daily` в списке. Полноценный поиск только по личному дневнику требует серверного дополнения либо явно ограниченного обхода известных записей. Не загружать все статьи команды ради поиска по дневнику. [knowledge_repository.py:95–116][article-list]

## Версии, повтор запросов и учёт времени

| Операция | Реальная гарантия | Поведение адаптера |
| --- | --- | --- |
| Создание задачи | `client_key`; уникальность `(company_id,created_by,client_key)`. Тот же ключ с другими нормализованными полями даёт 409. [calendar_repository.py:59–64][calendar-insert], [calendar_tasks.py:456–462][calendar-create] | Сохранить ключ и точное тело до отправки. Не генерировать новый ключ после неизвестного исхода. Повтор возможен только с тем же payload и понятным пользователю состоянием. |
| Обновление задачи | Блокировка карточки, обязательный `version`; несовпадение — 409; версии продолжений проверяются отдельно. [calendar_tasks.py:74–75][calendar-version], [calendar_tasks.py:397–415][calendar-save] | После сетевого сбоя перечитать карточку. Не повторять update вслепую и не подменять версию свежей автоматически. |
| Открытие личной записи дня | Проверка существующей идентичности дня под блокировкой команды. [knowledge_daily.py:31–56][daily-open] | После неизвестного исхода сначала `GET /daily`; при отсутствии — явный повтор открытия. Не создавать обычную статью с похожим названием. |
| Сохранение Markdown | `expected_version`, блокировка статьи, HTTP 409 при конфликте. [knowledge.py:76–97][article-save] | Хранить локально исходную версию и черновик. При 409 сохранить оба текста, дать выбор/слияние. Не перезаписывать актуальную статью автоматически. |
| Создание обычной статьи | Нет client key в изученном save-контракте. [knowledge.py:76–97][article-save] | Не применять автоматические повторы после неизвестного исхода. |
| Start / pause / resume / elapsed | В изученных календарных маршрутах и полях хранения отсутствуют. `start/end` — плановый интервал, `in_progress` — статус. [calendar_web.py:73–116][calendar-get], [calendar_repository.py:5–7][calendar-fields] | Не изображать серверный таймер сменой статуса и не сдвигать даты при паузе. Локальный таймер возможен только как явно локальная функция с отдельным хранилищем. |
| «Следующая задача» | Нет серверной очереди и атомарного next-командного контракта. | Локальный выбор следующей задачи не должен завершать предыдущую. Для общей очереди/таймера нужен отдельный контракт. |

Удаление и повторяющиеся задачи имеют дополнительные семантики `one/future/series`; минимальный адаптер не должен молча выбирать `series`. Сначала поддержать обычные задачи и чтение повторов, затем отдельные подтверждённые операции над серией. [calendar_tasks.py:600–629][calendar-delete]

## Исторический эскиз адаптера SideWidget (заменён реализацией выше)

Это проект интерфейса, **не уже существующие новые HTTP-маршруты**. Никаких вымышленных URL или домена в реализации.

```csharp
interface ISellyPersonalAdapter
{
    Task<ConnectionState> GetConnectionState(CancellationToken cancellation);
    Task<CalendarRange> GetCalendar(DateOnly start, DateOnly endExclusive,
        string timeZone, CancellationToken cancellation);
    Task<PersonalTask> GetTask(string id, string? occurrence,
        CancellationToken cancellation);
    Task<PersonalTask> CreateTask(CreatePersonalTask input, string clientKey,
        CancellationToken cancellation);
    Task<PersonalTask> SaveTask(PersonalTask draft, long expectedVersion,
        CancellationToken cancellation);
    Task<DailyNote?> FindDaily(DateOnly day, CancellationToken cancellation);
    Task<DailyNote> OpenDaily(DateOnly day, CancellationToken cancellation);
    Task<DailyNote> SaveDaily(DailyNote draft, long expectedVersion,
        CancellationToken cancellation);
}
```

В `ConnectionState` нужны проверенные `accountId`, `companyId`, возможности чтения/записи, состояние истёкшей сессии и тарифной блокировки. Нативная часть хранит credential защищённо; мост отдаёт только статусы и нужные DTO. ID в мосте — строки. Полное редактируемое состояние задачи хранится в C#, чтобы частичное UI-изменение не стирало описание, наблюдателей или повтор.

Нужный серверный контракт авторизации: выдача и отзыв отдельного desktop-credential с явными правами на календарь и **личный** дневник, привязка к аккаунту/команде, срок действия, понятное повторное подключение. Названия вроде `calendar:read`, `calendar:write`, `diary:personal:read`, `diary:personal:write` — только предложение, не существующие scope. Способ выдачи и transport согласовать отдельно; не реализовывать предполагаемый OAuth-flow без серверной поддержки.

Если пользователь отдельно выберет интеграцию через обычный веб-вход, нужно честно назвать широкие полномочия такой сессии и проверить жизненный цикл login/CSRF/logout, отдельный cookie-контейнер, путь развёртывания и HTTPS. Ограничение исходящих методов адаптера полезно, но не уменьшает серверные права credential.

Сейчас безопасно продолжать независимую работу: typed DTO, парсинг ответов, fake HTTP contract tests, обработка 401/403/409/503, локальные черновики и очередь с неизвестным исходом. Реальное подключение, новые серверные права и таймер — отдельные изменения после решения владельца. Это не разрешение менять CRM или production.

## Границы проверки

- Production API, миграция, ключи/отзыв и серверные аудитории опубликованных патчей проверены.
- SideWidget проверен нативно и синтетическими HTTP/React тестами; постоянный пользовательский ключ не выдавался.
- Учёт времени, queue-next, push/delta sync и синхронизация дневника не входят в эту поставку.
- Подробные доказательства и ограничения: `docs/SELLYBOARD_INTEGRATION_COORDINATION.md`.

[auth]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/app.py:10117>
[login]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/app.py:17229>
[session]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/account_registry.py:315>
[cookie]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/app.py:17501>
[get-auth]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/app.py:10449>
[post-auth]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/app.py:13244>
[form]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/app.py:10187>
[billing]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/app.py:10130>
[base-path]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/app.py:1283>
[origin]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/app.py:12478>
[csrf-html]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/app.py:9971>
[calendar-response]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_web.py:29>
[calendar-id]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_web.py:49>
[calendar-get]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_web.py:73>
[calendar-post]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_web.py:95>
[calendar-access]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_tasks.py:28>
[calendar-visible]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_tasks.py:50>
[calendar-version]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_tasks.py:74>
[calendar-dto]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_tasks.py:78>
[calendar-normalize]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_tasks.py:132>
[calendar-events]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_tasks.py:282>
[calendar-save]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_tasks.py:397>
[calendar-create]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_tasks.py:456>
[calendar-delete]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_tasks.py:600>
[calendar-fields]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_repository.py:5>
[calendar-insert]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_repository.py:59>
[calendar-query]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_repository.py:115>
[calendar-query-limit]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_repository.py:145>
[dst]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_recurrence.py:18>
[recurrence]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_recurrence.py:40>
[schedule]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/calendar_recurrence.py:66>
[knowledge-errors]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/knowledge_web.py:35>
[knowledge-get]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/knowledge_web.py:53>
[knowledge-post]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/knowledge_web.py:72>
[knowledge-actor]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/knowledge.py:20>
[article-save]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/knowledge.py:76>
[knowledge-membership]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/knowledge_repository.py:11>
[article-read]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/knowledge_repository.py:83>
[article-list]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/knowledge_repository.py:95>
[knowledge-access]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/knowledge_access.py:19>
[daily]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/knowledge_daily.py:16>
[daily-open]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/knowledge_daily.py:31>
[knowledge-response]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/employee_chats_web.py:18>
[wire]: <E:/YandexDisk/Work/Python/ООО Согласие/CRM/chat_wire.py:5>

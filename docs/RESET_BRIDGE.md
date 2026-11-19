# Мост WebView2 ↔ C# — текущий контракт 0.2

Источник истины: `SideWidget.Core/Protocol.cs`, `SideWidget.App/Controller.cs`, `WindowServices.cs` и конкретные сервисы. Документ описывает существующие команды; будущий SellyBoard adapter не считается частью моста.

## Envelope, ответы и события

Origin: `https://sidewidget.local`. Ровно четыре поля запроса:

```json
{
  "protocolVersion": 1,
  "requestId": "7eb88c99-555c-4bf3-a748-c6a9890a5f93",
  "actionId": "state.get",
  "params": {}
}
```

UUID новый для каждого самостоятельного действия. Проверяются дубли ключей, глубина JSON до 16, размер до 2 Mi символов, allowlist команд и отсутствие неизвестных имён параметров. Дополнительная проверка типов/диапазонов выполняется обработчиком. Неизвестные/старые API/Python/shell-команды отвергаются.

```ts
type Reply =
  | {protocolVersion:1; requestId:string; status:'result'; data:unknown}
  | {protocolVersion:1; requestId:string; status:'error'; error:{code:string; message:string}};
type Event = {protocolVersion:1; type:string; sequence:number; time:string; data:unknown};
```

События: `panel` → `{expanded,pinned}`, `settings.changed` → `{settings}`, `launcher.changed` → `{}`, `navigate` → `{page}`, `notice` → `{message}`. `time` — ISO datetime. Controller ограничивает одновременную работу 32 командами и помнит последние 2048 requestId. Повтор ID даёт `duplicate`. Web bridge ждёт до 180 секунд, после чего исход нативного действия может оставаться неизвестным; автоматический повтор записи не предусмотрен.

Ошибки прикладного кода — `CommandFault` с кодом/сообщением; непредусмотренные исключения выдаются как `internal` без stack trace. Некорректный envelope может быть отвергнут до извлечения ID, поэтому не всякий malformed input получает коррелированный ответ.

## Snapshot и настройки

`bridge.ready {}` и `state.get {}` возвращают:

```ts
type Snapshot = {
  settings: Settings;
  calculatorHistory: {id:string; expression:string; result:string; time:string}[];
  colorHistory: string[];
  unitFavorites: {from:string; to:string}[];
  apps: {id:string; name:string; path:string; icon:string|null; packaged:boolean}[];
  drafts: {diary:string};
  selly: {configured:false; message:string};
};
type Settings = {
  width:number; height:number; hideDelay:number; openDelay:number;
  hotkey:string; monitor:number; position:number; largeMode:boolean;
  order:string[]|null; disabled:string[]|null;
};
```

По умолчанию: `width=444`, `height=620`, `hideDelay=650`, `openDelay=100`, `hotkey='Ctrl+Alt+Space'`, `monitor=0`, `position=1`, `largeMode=false`; `order/disabled` могут быть `null`, UI нормализует их. Ограничения: width 360–900, height 360–1200 DIP, hideDelay 200–5000, openDelay 0–2000 мс, monitor 0–15, position 0–1, максимум 25 элементов order/disabled.

## Локальное состояние и оболочка

| Команда | Параметры | Ответ |
| --- | --- | --- |
| `settings.save` | Поля Settings | Snapshot; hotkey conflict отклоняет сохранение |
| `history.add` | `expression` ≤500, `result` ≤200 символов | Snapshot; история до 100 записей |
| `history.delete` | `id` ≤64 | Snapshot |
| `history.clear` | `{}` | Snapshot после нативного подтверждения; отмена оставляет историю |
| `color.remember` | `hex` = `#RRGGBB` | Snapshot; canonical uppercase, до 24 |
| `units.favorite` | `from`, `to` ≤32 | Snapshot; toggle пары, до 20 |
| `diary.draft` | `text` ≤80000 | Snapshot; только локальный черновик |
| `shell.pin`, `shell.collapse`, `shell.interactive` | `{}` | `{}`; изменение состояния приходит событием |
| `shell.editing` | `value:boolean` | `{}` |

Калькулятор/единицы/цвета вычисляются в TypeScript; `history.add` лишь сохраняет результат и не запускает выражение в C#. Нативное изменение положения/размера отправляет `settings.changed`; отдельной bridge-команды для pointer-drag нет.

## Clipboard, QR, ссылки и приложения

| Команда | Параметры | Ответ |
| --- | --- | --- |
| `clipboard.text` | `text` ≤100000 | `{copied:true}` |
| `clipboard.image` | `dataUrl` ≤1500000, PNG/JPEG base64 | `{copied:true}` |
| `qr.save` | `format:'svg'|'png'|'jpeg'`, `data` ≤1500000 | `{saved:boolean}`; `false` при отмене picker |
| `links.open` | `url` ≤2048, absolute HTTP/HTTPS без credentials/control chars | `{opened:true}`; системный браузер |
| `apps.add` | `{}` | Snapshot; нативный file picker |
| `apps.pickWindow` | `{}` | Snapshot; выбор настоящего окна и подтверждение закрепления |
| `apps.undo` | `{}` | Snapshot; отмена последнего window-picker toggle, если сохранён undo |
| `apps.launch` | `id` ≤64 | `{launched:true}` |
| `apps.remove` | `id` ≤64 | Snapshot |
| `apps.rename` | `id` ≤64, непустое `name` ≤80 | Snapshot |
| `apps.reorder` | `ids:string[]`, все текущие ID ровно один раз | Snapshot |
| `color.pick` | `{}` | `{hex:string}`; отмена — ошибка `cancelled` |

Для SVG `data` — исходный XML; для PNG/JPEG — data URL. `makeQr()` возвращает именно `{svg:XML,png:dataURL,jpeg:dataURL}`. FileSavePicker выбирает путь; произвольного filename/path от веб-части нет. Native SVG допускает только ограниченную геометрию и запрещает внешние ресурсы/DTD. Количество избранных приложений — до 40. Native drag/drop использует тот же список и событие `launcher.changed`.

## Windows-сервисы

`media.get {}` возвращает:

```ts
{
  available:boolean,
  sessions:{id:string,title:string,artist:string,source:string,playing:boolean,
    positionSeconds:number|null,durationSeconds:number|null,
    canPlay:boolean,canPause:boolean,canPrevious:boolean,canNext:boolean,canSeek:boolean,
    artwork:string|null}[],
  message?:string
}
```

`media.command {id,command,positionSeconds?}` принимает `play|pause|previous|next|seek` и возвращает свежий media snapshot. Seek — секунды относительно начала timeline, доступность и диапазон перепроверяются. Закрытый источник или отказ команды не превращаются в успешный статус.

`audio.get {}` возвращает:

```ts
{
  available:boolean, volume:number|null, muted:boolean|null, microphoneMuted:boolean|null,
  devices:{id:string,name:string,flow:'input'|'output',default:boolean}[],
  sessions:{id:string,name:string,volume:number,muted:boolean}[], message:string|null
}
```

`audio.set {volume?,muted?,microphoneMuted?,sessionId?}` возвращает свежий audio snapshot. Volume 0–1; sessionId выбирает сессию воспроизведения текущего output. Microphone mute относится к default communications input и не совмещается с sessionId. Прямого переключения output/input устройства нет; используется `quick.open {target:'sound'}`.

`search.query {text}` (до 500 символов) → `{available,results:[{path,name,isDirectory}],message?}`; максимум 50 результатов. `search.open {path,reveal?:boolean}` → `{opened:true}`. Controller допускает пути из ограниченного накопленного allowlist результатов текущего процесса; это не строго только последняя выдача. Существование пути проверяется заново, UNC запрещён. UI отдельно отклоняет поздний ответ старого запроса.

`system.get {}` →:

```ts
{
  cpuPercent:number|null, memoryUsed:number|null, memoryTotal:number|null, uptimeSeconds:number,
  network:{name:string,receiveBytesPerSecond:number|null,sendBytesPerSecond:number|null},
  disks:{name:string,total:number,free:number}[],
  battery:{available:boolean,percent:number|null,charging:boolean|null,ac:boolean|null},
  gpuAvailable:false
}
```

Память/диски — байты, сеть — байты/сек. Первый CPU/network sample может быть `null`. Свободное место диска не является его активностью. GPU не измеряется.

## Интернет

`internet.get {}` →:

```ts
{
  available:boolean, connectivity:'internet'|'local'|'limited'|'none'|'unknown',
  source:'Windows', target:string, presets:string[], cooldownSeconds:5,
  lastCheck:null|{method:'icmp'|'https',target:string,checkedAt:string,success:boolean,
    latencyMs:number|null,httpStatus:number|null,status:string,message:string}
}
```

`internet.check {method:'icmp'|'https',target:string}` → тот же snapshot с `lastCheck`. Target до 253 символов в мосте: домен/IP без схемы, порта, пути и credentials. IDN нормализуется. По одному запросу, cooldown 5 секунд, общий timeout 6 секунд. `internet.get` сам сеть не проверяет. ICMP и HTTPS имеют разную семантику latency; `success:false` одной цели не заменяет Windows connectivity.

## Системные команды

`quick.open {target}` принимает `sound|wifi|bluetooth|notifications|power|taskmanager|powershell|cmd` и возвращает `{opened:true}`. Первые пять открывают настройки Windows, остальные — фиксированные системные executable без произвольного command text.

`quick.power {action}` принимает `sleep|restart|shutdown|lock|explorer`. Для реализованных действий требуется нативное подтверждение, **включая lock**. Ответ `{executed:true|false}`; отмена — false. `explorer` сейчас выдаёт `unavailable`, а не перезапускает процессы. Эти действия не выполнялись во время агентских проверок.

## Погода, валюты, перевод

- `currency.get {}` → `{date,source,fetchedAt,stale,rates:[{code,name,nominal,value,rate}],message?}`. `rate=value/nominal`; RUB присутствует с rate 1. `date` — дата котировки, не время запроса. Кэш 30 минут; ошибка с кэшем возвращает stale. Конвертация выполняется локально.
- `weather.get {city}` (до 120 символов) → `{city,temperature,apparent,wind,precipitation,code,time,fetchedAt,source,forecast:[{date,min,max,code}],stale}`. Температура °C, ветер км/ч, осадки мм. Явный запрос запускает geocoding и forecast; минимум 10 секунд между началами weather-запросов в одном service instance. При сетевой/JSON/schema ошибке используется кэш этого города как stale, если он есть.
- `translation.get {}` → `{configured:boolean,provider:'DeepL API',free:boolean}`.
- `translation.configure {}` → та же конфигурация после нативного диалога. Ключ не передаётся через bridge; сама настройка не вызывает сеть.
- `translation.run {text,source,target}` → `{text,provider:'DeepL API'}`. Текст 1–20000 символов, не только whitespace. `source='AUTO'` или пустая строка включает автоопределение; язык нормализуется в uppercase. Региональный код допускается в форме `EN-GB`. Запрос отправляется только после явного действия.
- `translation.cancel {}` → `{cancelled:true}`. Это запрос отмены, не утверждение, что поставщик не получил текст. Поздний ответ отменённого/заменённого запроса не возвращается как актуальный перевод.

`diary.draft` не синхронизирует SellyBoard. Нет bridge-команд записи SB, учёта времени, server reminders, direct radio/DND/device switch или GPU. Их нельзя выводить из наличия страницы интерфейса.

`apps.icon {id}` opens a native PNG/SVG/ICO picker; returns snapshot. `launcherUndoAvailable` is host truth; cancelled selection does not create undo. Ordinary shortcut Identity preserves the resolved EXE while Path keeps the original .lnk and its arguments.

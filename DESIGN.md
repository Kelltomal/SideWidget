---
name: SideWidget
description: Повседневная выдвижная шторка Windows
colors:
  background: "#181a1c"
  surface: "#24292d"
  text: "#f2f4f5"
  muted: "#a9b1ba"
  accent: "#b6c3d0"
  accent-hover: "#d0dae3"
  on-accent: "#16191b"
  line: "#30373d"
  field: "#22272b"
  button: "#2c3339"
  button-hover: "#3a4249"
  button-active: "#454d54"
  selected: "#3b454e"
  tool-hover: "#343c43"
  white: "#fff"
  error-text: "#ffc8c1"
  error-surface: "#352725"
  error-line: "#7d524e"
typography:
  body:
    fontFamily: "Medeo, 'Segoe UI', sans-serif"
    fontSize: "14px"
    fontWeight: 400
  title:
    fontFamily: "Medeo, 'Segoe UI', sans-serif"
    fontSize: "15px"
    fontWeight: 600
  heading:
    fontFamily: "Medeo, 'Segoe UI', sans-serif"
    fontSize: "14px"
    fontWeight: 600
    lineHeight: 1.4
  label:
    fontFamily: "Medeo, 'Segoe UI', sans-serif"
    fontSize: "12px"
    lineHeight: 1.5
  button:
    fontFamily: "Medeo, 'Segoe UI', sans-serif"
    fontSize: "14px"
    fontWeight: 500
    lineHeight: 1.3
  code:
    fontFamily: "'Medeo Mono', Consolas, monospace"
    fontSize: "13px"
  result:
    fontFamily: "'Medeo Mono', monospace"
    fontSize: "25px"
  clock:
    fontFamily: "Medeo Sans"
    fontSize: "22px"
    fontWeight: 600
rounded:
  window: "16px"
  panel: "16px"
  preview: "12px"
  tile: "12px"
  control: "10px"
  swatch: "4px"
  circle: "50%"
spacing:
  xs: "4px"
  sm: "8px"
  home-gap: "10px"
  md: "12px"
  panel-inset: "14px"
  lg: "16px"
  bottom: "20px"
components:
  button-secondary:
    backgroundColor: "{colors.button}"
    textColor: "{colors.text}"
    typography: "{typography.button}"
    rounded: "{rounded.control}"
    padding: "7px 11px"
  button-primary:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.on-accent}"
    typography: "{typography.button}"
    rounded: "{rounded.control}"
    padding: "7px 11px"
  button-primary-hover:
    backgroundColor: "{colors.accent-hover}"
  button-primary-active:
    backgroundColor: "{colors.muted}"
    textColor: "{colors.on-accent}"
  button-quiet:
    backgroundColor: "transparent"
    textColor: "{colors.muted}"
    rounded: "{rounded.control}"
    padding: "3px 4px"
  button-icon:
    backgroundColor: "transparent"
    textColor: "{colors.text}"
    rounded: "{rounded.control}"
    padding: "6px"
    width: "32px"
  field:
    backgroundColor: "{colors.field}"
    textColor: "{colors.text}"
    rounded: "{rounded.control}"
    padding: "9px 11px"
  search:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text}"
    rounded: "{rounded.tile}"
    padding: "0 12px"
  panel:
    backgroundColor: "{colors.surface}"
    rounded: "{rounded.panel}"
    padding: "14px"
  tool-tile:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text}"
    rounded: "{rounded.tile}"
    padding: "11px 3px"
  color-swatch:
    backgroundColor: "{colors.button}"
    textColor: "{colors.text}"
    rounded: "{rounded.control}"
    padding: "6px 8px"
  error:
    backgroundColor: "{colors.error-surface}"
    textColor: "{colors.error-text}"
    rounded: "{rounded.control}"
    padding: "10px 12px"
---

# Design System: SideWidget

## Overview

**Creative North Star: "Повседневная шторка Windows"**

Одна непрозрачная графитовая поверхность с мягкими углами объединяет нативную шапку и инструменты WebView2. Сдержанный светлый акцент отмечает действия, выбор и фокус. Встроенный Medeo сохраняет общий характер на обеих сторонах границы WinUI/React.

Иерархию создают тон поверхности, начертание и отступы. Короткие сводки компактны; формы и результаты получают всю ширину содержимого. Это запись реализованного Product Reset, уточняющая ранее утверждённое направление по коду, а не новая визуальная концепция.

**Key Characteristics:**

- Одна поверхность и неподвижная нативная шапка.
- Спокойная графитовая палитра без декоративного свечения.
- Локальные Medeo Sans и Medeo Mono, ясный клавиатурный фокус.
- Компактные сводки, полноширинные инструменты и текстовые состояния.

Источник токенов: `src/SideWidget.Web/src/style.css`; компоновки: `main.tsx`, `ui.tsx`, `LocalTools.tsx`, `WindowsPanels.tsx`, `PersonalPanels.tsx`; нативной геометрии и движения: `src/SideWidget.App/MainWindow.cs`, `src/SideWidget.Core/PanelMotion.cs`. Направление и режим поверхности записаны в `docs/SURFACE_BRIEF.md` и `.impeccable/surfaces/src-sidewidget-web-src-main-tsx.md`. Архив 0.1 не задаёт текущую систему.

## Colors

Палитра холодных графитовых нейтралей с серебристым акцентом; значения frontmatter нормативны.

### Primary

- **Серебристый акцент** (`accent`, `accent-hover`) — основное действие, воспроизведение, выбранная дата и видимый фокус. Тёмный `on-accent` используется на основной кнопке; воспроизведение и календарь используют `background` для текста.
- **Состояние ошибки** (`error-text`, `error-surface`, `error-line`) — сообщение об операции, никогда декоративная карточка.

### Neutral

- **Графит** (`background`) — общая нативная и веб-поверхность; **приподнятый графит** (`surface`) — панели, поиск, результаты и инструменты.
- **Светлый текст** (`text`) и **вторичный серый** (`muted`) разделяют действие, значение и пояснение. `white` служит выбранным состояниям и непрозрачной подложке QR.
- `field` и `line` обозначают поля и разделители; `button`, `button-hover`, `button-active`, `selected`, `tool-hover` — реальные состояния управления.

**The Text Status Rule.** Цвет сопровождается текстовым состоянием; отсутствие данных не выглядит успешным результатом.

## Typography

Medeo Sans загружается локально: WOFF2 в WebView2, TTF в нативной шапке. Medeo Mono используется для выражений, результатов и кодов; стандартные шрифты остаются запасными. Шрифт интерфейса не загружается из сети.

Роли frontmatter отражают фактическую шкалу: основной текст, заголовок страницы, заголовок секции, подпись, кнопка, код, результат и часы. Нативные день и дата имеют размер 13 DIP; часы используют Semibold. Параграфы имеют межстрочный интервал 1.55; подписи полей — 1.4. Числовые показатели и время используют табличные цифры. В компактной музыке название — 14 px, исполнитель — 12 px; оба сокращаются многоточием.

Крупный режим увеличивает базовый текст до 16 px, пояснения и подписи инструментов до 14 px, заголовки секций до 16 px. Кнопки и поля получают минимум 42 px, плитки инструментов — минимум 80 px. Это явная настройка пользователя, не автоматический брейкпоинт.

## Layout

При первом раскрытии с клавиатуры фокус получает поиск Everything, если он включён. Под поиском по умолчанию одна строка закреплённых инструментов, на узкой ширине и при четырёх кнопках допускается перенос. Пустое состояние музыки — компактная строка с переходом в «Звук», где находится настройка телефонного пульта.

Нативная шапка занимает 52 DIP: день слева, часы по центру, дата справа. Базовая ширина — 444 DIP; минимальная настроенная ширина — 360 DIP. При раскрытии ширина растёт на 40% базовой ширины с каждой стороны, до 180% (444 → 799 DIP с округлением); у края монитора окно сдвигается внутрь рабочей области, на узком мониторе ширина ограничена его рабочей областью. Сохранённая ширина остаётся базовой и не умножается при повторном раскрытии или изменении размера. Верхняя граница и высота шапки сохраняются. Содержимое ниже шапки содержит собственную неподвижную строку навигации и прокручиваемую основную область. Высота по умолчанию — 620 DIP, ограничена рабочей областью монитора.

Основная область имеет отступы 3 px сверху, 16 px по бокам и 20 px снизу, стабильное место под полосу прокрутки. Главная — один вертикальный поток с промежутком 10 px; короткие календарь и погода стоят рядом. Формы используют всю ширину; пары связанных полей могут занимать две равные сжимаемые колонки с промежутком 12 px. Инструменты размещены в четырёх колонках; при ширине до 400 px — в трёх. До 380 px боковые отступы уменьшаются до 12 px, промежуток плиток — до 6 px, число в результате — до 21 px.

**The Fixed Top Rule.** Раскрытие меняет нижнюю границу окна; верхняя граница и нативная шапка остаются на месте.

Визуальная выборка: нативные `docs/screenshots/reset-home-360.png`, `reset-home-444.png`, `reset-settings-444.png`, `reset-calculator.png`, `reset-qr.png`, `reset-colors.png`, `reset-search.png` при 96 DPI. Это свидетельство текущего состояния, не заявление о сверке с недоступным исходным изображением или о проверке всех DPI.

## Elevation & Depth

Веб-поверхности плоские: CSS не задаёт теней или размытия. Глубину создаёт разница между общим фоном, панелью и полем. Декоративные обводки кнопок, полей и поиска убраны; тонкие границы остаются у разделителей и ошибок. Внешняя рамка Windows убрана обработкой WM_NCCALCSIZE: клиентская область занимает всё окно, даже когда WinUI восстанавливает WS_DLGFRAME/WS_EX_WINDOWEDGE при активации. DWMWA_BORDER_COLOR / DWMWA_COLOR_NONE остаётся дополнительным запросом к DWM. Фокус и системные границы forced-colors сохраняются.

**The Quiet Depth Rule.** Разделяйте содержимое тоном и расстоянием; не добавляйте свечение, стекло или декоративные тени.

## Shapes

Мягкие углы образуют согласованную иерархию: окно, панель, превью, плитка, поле. Верхние углы окна прямые; нижние сохраняют радиус 16 DIP. Нативный `CreateRoundRectRgn` использует диаметр 32 DIP и верхнюю границу выше HWND, поэтому верхние дуги не видны. Общие панели используют `panel`, превью и краткие календарь/погода — `preview`, поиск и инструменты — `tile`, кнопки и ввод — `control`. Кнопка воспроизведения круглая.

У обычных полей и кнопок прозрачная граница 1 px сохраняет размеры без видимого контура. Фокус — сплошная обводка 2 px с отступом 3 px; поиск обводит контейнер с отступом 2 px. Текущий день календаря отмечен точкой. В forced-colors элементы управления, панели и плитки получают системные границы, выбор и текущая дата — системную обводку.

## Components

### Buttons

Обычная кнопка спокойная, с минимальной высотой 36 px. Основная использует серебристую заливку; вторичная — нейтральную тёмную. Тихое действие без границы имеет минимум 28 px и текст 12 px. Иконка навигации — 32 px шириной и минимум 30 px высотой. Disabled снижает непрозрачность до 0.5. Наведение меняет заливку только на устройствах с точным указателем. Нажатие и `aria-pressed` имеют отдельные заливки; указатель добавляет короткое сжатие, описанное в Feedback / Motion. У основной кнопки нажатие использует светлый `muted`, сохраняя контраст с `on-accent`; на точном указателе наведение может сохранять более светлый `accent-hover`. Иконки веб-интерфейса — встроенные SVG с линией 1.6 и скруглёнными концами.

### Inputs / Fields

Подпись находится над полем с промежутком 7 px. Поле имеет минимум 40 px высоты; textarea — минимум 90 px, межстрочный интервал 1.6 и вертикальное изменение размера. Поиск объединяет SVG и поле в одном контейнере; минимальная высота ввода — 43 px. Placeholder использует вторичный текст без дополнительного снижения прозрачности. Выделение использует акцент с тёмным текстом.

### Navigation

Строка управления имеет минимум 35 px высоты. На главной — закрепление и редактирование раскладки, без большого названия продукта. Общая громкость находится слева от плеера: вертикальный ползунок (вверх громче), процент под ним, ниже SVG-кнопка звука и кнопка микшера приложений. Микшер раскрывается меню с отдельными вертикальными ползунками и mute для активных аудиосессий, закрывается по Escape, вне меню и кнопкой; вход во все настройки звука и общие настройки панели — в блоке музыки. Если музыка скрыта, общие настройки остаются в верхней строке. Во вложенной странице добавляются возврат и заголовок. Активная настройка и закрепление обозначены `aria-pressed`; иконки имеют доступные имена. Инструменты открываются внутри той же поверхности.

### Cards / Containers

Панель имеет мягкий фон, внутренний отступ 14 px и промежуток 10 px, без собственной тени. Главная использует более компактные варианты: музыка с отступами 10 × 12 px и обложкой 44 × 44 px; краткие календарь/погода с минимумом 70 px. Плитки инструментов имеют минимум 70 px и подпись под SVG. Ответ калькулятора — панель с числом и действием копирования, отступом 18 px и переносом длинного результата.

### Color Swatches / QR

Сохранённый цвет — кнопка с кодом и образцом 20 × 20 px; образец имеет видимую границу и угол `swatch`. Фактический цвет пользователя не является палитрой приложения. QR имеет непрозрачный белый квадрат шириной не более 280 px, сохранённое соотношение сторон и пиксельное отображение. QR генерируется локально; обложки и значки приложений приходят из ОС во время работы. Они не считаются декоративными растровыми ассетами проекта.

### Feedback / Motion

Обычное сообщение использует вторичный текст, размер 13 px и межстрочный интервал 1.5. Ошибка добавляет цветной фон и границу, получает `role="alert"`; обычный результат — `role="status"`. Состояния копирования и сохранения написаны словами.

Нативное раскрытие длится 240 мс, интерполяция — `1 - (1 - t)^3`: быстрый старт, замедление к концу. Высота и горизонтальное расширение используют один прогресс. Разворот начинается с текущей высоты и ширины, без очереди и скачка. Размер HWND и WebView2 постоянен на всём переходе: раскрытие меняет скруглённую область окна, не перераскладывая веб-содержимое. Кадры синхронизированы с `CompositionTarget.Rendering`; фоновый таймер 100 мс проверяет уход указателя. Короткий клик по шапке открывает панель после отпускания. Удержание от 250 мс не раскрывает панель; движение более 5 физических пикселей запускает перетаскивание и исключает клик. Наведение не раскрывает панель. Reduced motion и клавиатурное раскрытие применяют конечное состояние сразу. Перетаскивание и изменение размера следуют вводу напрямую; Escape отменяет операцию.

В WebView2 нажатие указателем использует прерываемый CSS transition 140 мс, `cubic-bezier(.23,1,.32,1)`, масштаб `.97` (иконки и воспроизведение `.93`, чекбокс `.9`). Смена раздела — opacity и translateY 5 px за 180 мс через `@starting-style`; контент по умолчанию видим. Сообщения появляются через opacity. Клавиатурный ввод отключает эти переходы до следующего pointerdown; reduced motion убирает перемещение/масштаб, сохраняет короткий 80-мс переход прозрачности. Ввод, перетаскивание и слайдеры следуют непосредственно действию.

## Do's and Don'ts

Отпускание кнопки возвращает масштаб за 100 мс, быстрее намеренного нажатия за 140 мс.

### Do:

- **Do** сохранять одну поверхность, неподвижную шапку и прокрутку под строкой управления.
- **Do** использовать встроенный Medeo, реальные значения и текстовые состояния.
- **Do** сохранять видимый фокус, подписи полей и перенос длинных значений.
- **Do** проверять главный экран и формы при ширине 360 и 444 DIP, отдельно проверять крупный режим и DPI.

### Don't:

- **Don't** возвращать сетку панели разработчика из архива 0.1.
- **Don't** добавлять отдельные окна-капсулы, декоративное свечение или тяжёлое стекло.
- **Don't** подменять отсутствующие подключения и данные придуманными показателями.
- **Don't** дублировать нативные часы веб-имитацией или менять локальные шрифты на сетевую загрузку.

Техническая область захвата resizeGrip использует графит `#181A1C` с alpha 1/255 для hit testing; это служебная прозрачная поверхность, не дополнительный цвет интерфейса.


### Пипетка экрана

При включении сразу появляется нативная подсказка рядом с указателем: лупа 9× (15×15 физических пикселей без сглаживания), контрастная рамка центрального пикселя, образец цвета и HEX. Движение обновляет подсказку напрямую; таймер 33 мс обновляет цвет и при неподвижном курсоре. Возле границ монитора подсказка меняет сторону. Если курсор попал на прежнее положение подсказки, она скрывается до захвата; собственный интерфейс не попадает в образец. В памяти хранится только текущий фрагмент 15×15; после выбора или отмены GDI-ресурсы освобождаются. Левый клик выбирает цвет, Escape / правый клик отменяют; действует прежний лимит 30 секунд.

### Ручная проверка переводчика

Google и Яндекс обычно скрыты: исходный текст и результат показываются в собственных полях. Только при CAPTCHA тело шторки временно показывает ту же страницу сервиса. Нативная строка Medeo на графите содержит название сервиса, инструкцию пройти проверку и кнопку «Отмена». Шапка остаётся на месте; drag/resize/автосворачивание блокируются как в нативной форме. После результата восстанавливается прежний локальный UI. Это функциональная спецификация нового пути; native визуальная приёмка пока ожидается после остановки GUI пользователем.

Музыкальный блок: компактная версия не имеет фиксированной минимальной высоты. Громкость и медиаконтент образуют две колонки; вертикальный мастер имеет дорожку 64 px, процент под ней и SVG-действия снизу. Обложка/трек и управление воспроизведением остаются в одной строке, название обрезается многоточием. Верхняя строка настроек отсутствует: SVG-кнопки mute, микшера, настроек звука и настроек панели стоят в нижнем правом ряду. Вертикальный мастер в левой колонке 32 px растягивается по высоте блока, процент под дорожкой. Раскрытый микшер сохраняет дорожки 108 px.

По следующему уточнению пользователя обложка выделена в отдельную колонку 82 px между мастером и треком: она растягивается по внутренней высоте обеих строк музыкального блока, включая footer SVG. Нажатие раскрывает музыку на месте. Отсутствующая обложка заменяется тем же полем с SVG ноты, без выдуманной картинки.

Колесо мыши над ползунком или подписью громкости меняет значение на 2 процентных пункта за событие: вверх громче, вниз тише, пределы 0–100%. Страница под громкостью не прокручивается (native wheel listener passive:false). Общий мастер и сессии используют одинаковое поведение; перемотка трека колесом не меняется.

Музыка: полоса позиции, прошедшее/полное время и «Осталось» видны в единственной обычной карточке. Ползунок блокируется без canSeek, показ времени сохраняется; неизвестные значения не выдумываются. Media polling раз в секунду, только пока панель раскрыта. По последнему запросу отдельный полный вид удалён. Одна обычная карточка содержит полосу времени и копирование «Исполнитель — Трек» по нажатию названия. Обложка больше не раскрывает карточку. Общие настройки приложения по последнему запросу возвращены в верхнее меню рядом с закреплением; SVG настроек звука остаётся в музыкальном footer.

## Подсказки под панелью

В Windows все пояснения кнопок выводятся отдельной поверхностью под раскрытой панелью, по её горизонтальному центру. Верх подсказки касается низа панели без зазора. Подсказка использует типографику приложения: основной текст #F2F4F5, пояснение и SVG-превью #A9B1BA, локальный Medeo. Фон #141618 темнее панели, без рамки; верхние углы прямые, нижние сохраняют радиус 16 DIP. Ширина до 340 DIP с ограничением рабочей области монитора. Подсказка не получает фокус, не перехватывает мышь и не занимает место поверх меню. При сворачивании панели скрывается; если под панелью нет места, не переносится поверх управления. В браузерном предпросмотре без Windows-хоста используется нижний центр viewport со сквозными pointer-events.

Последнее уточнение пользователя: превью обложки квадратное, 82×82 px, центрировано по вертикали; больше не растягивается на полную высоту карточки. object-fit:cover сохраняет пропорции изображения.

## Темы приложения — 5 ноября 2026

Настройки → Оформление: «Тёмная», «Светлая», «Как в Windows». Выбор сохраняется сразу, отдельно от остальных полей формы. Существующие настройки без theme остаются тёмными. Системный режим следует именно теме приложений Windows и обновляется без перезапуска: нативный UISettings.ColorValuesChanged и веб prefers-color-scheme; native theme.system синхронизирует обновления.

Светлая палитра сохраняет Medeo, геометрию и спокойные нейтральные поверхности. Основные токены: background #f4f5f6, surface #ffffff, text #20262c, muted #59636e, accent #455d74, on-accent #ffffff, control #e2e7ec, field #e9edf1, panel-field #f0f2f5, selected #d3dee8, line #d4dbe1. Ошибки: text #93372d, surface #fff0ed, line #c27a71. Нативная подсказка: светлая #e5e9ed, тёмная #141618; её верхние прямые углы и расположение ниже окна сохраняются. QR остаётся чёрным на белом, пользовательские образцы цвета и обложки не перекрашиваются.

Цвета меняются за 240 мс: типизированные CSS color tokens и нативные ColorAnimation. Повторный выбор продолжает переход с отображаемого цвета. Первый показ сразу использует сохранённую палитру. prefers-reduced-motion / отключённые анимации Windows применяют тему мгновенно. Цвета полей, выбранных кнопок, подсказок, ошибок, скроллбара и нативной шапки согласованы.

Проверено в отдельном headless Edge: настройки 360/444 px, отсутствие горизонтального overflow, автосмена dark/light, reduced-motion 0s и прерывание перехода. Замеры фона при переходе: rgb(41,43,45), затем rgb(192,193,194), затем rgb(244,245,246). Снимки src/SideWidget.Web/output/playwright/theme-light-{360,444}.png. Нативная сборка проходит; установка и визуальная проверка живого native процесса выполняются общим владельцем GUI, отдельно от этих веб-проверок.

## Компактная полоска поверх развёрнутого окна

Прямое уточнение пользователя: при активном maximized/fullscreen окне на том же мониторе показывается шапка высотой 23 DIP, с временем и датой Medeo 13 DIP. Alpha всей полоски 30% (77/255). Обычная панель остаётся непрозрачной. Полоска сквозная для мыши: первый клик проходит приложению под ней, двойной клик открывает SideWidget, перехватывая вторую пару down/up. Наведение ничего не делает. Двойной клик использует время и прямоугольник Windows. После закрытия панели снова остаётся полоска; восстановление чужого окна возвращает обычную шапку. Новая отдельная Win32 layered поверхность не меняет WinUI/WebView геометрию или визуализатор. Native визуальная приёмка новой версии со сквозным кликом не завершена: пользователь остановил Computer Use физическим Escape.


## Audio visualizer and reference music — 2026-11-05

Latest user references replace the earlier82px miniature direction for music: one player, square album artwork104–190px left, artist above the copyable title, seek/time/remaining and centered playback at right. No full-mode toggle or unsupported queue/favorite/shuffle buttons. Colors and source imagery stay runtime media metadata. Dark and light themes remain supported; the warm-to-purple spectrum follows the supplied waveform reference.

The source is the user's explicit choice: ALL Windows default output audio, obtained locally by WASAPI shared render loopback, bounded4096 FFT and32 logarithmic bands. No microphone, PCM file or raw audio bridge. Data capture25Hz; web bounded25Hz requests only while expanded/visible. UI drawing is display-synced via requestAnimationFrame/CompositionTarget.Rendering with measured-target exponential attack30ms/release80ms, silence40ms; no phase/noise generator. When targets settle, geometry work stops. Reduced motion reduces activity/amplitude.

Normal collapsed52px XAML header paints behind day/clock/date/timers; fullscreen compact23px is a separate layered Win32 HWND with its own lightweight16ms GDI background renderer, sharing the SAME spectrum service and settings. Existing alpha77, pointer transparency, firstclick pass-through and doubleclick reveal remain unchanged. Hidden/disabled UI detaches rendering/timers; disabling capture releases the WASAPI worker asynchronously.

Visualizer settings save immediately: enabled, showInHeader, brightness0..1, sensitivity.25..4, wave/bars. Text contrast caps wave opacity at .35 dark/.15 light before user brightness scaling. Native and web use the same options. Missing/disabled/unavailable/silent data never produces decorative fake activity. Previous single/copy/wheel/timeline behavior stays.


### Spectrum colors (user reference palette)

The supplied waveform reference deliberately adds these signal colors, used only for spectrum rendering: warm `#F6634B` / `rgb(246,99,75)`, middle `#E03E6F` / `rgb(224,62,111)`, cool `#895BE1` / `rgb(137,91,225)`. WinUI opaque calls are `Color.FromArgb(255,246,99,75)`, `Color.FromArgb(255,224,62,111)`, `Color.FromArgb(255,137,91,225)`. The detector may report those four-argument calls as `rgb(255, 246, 99, 75)`, `rgb(255, 224, 62, 111)`, `rgb(255, 137, 91, 225)`; the first255 is alpha, not an extraRGB channel. Playback line accent: dark `#E4D679`, light `#735C16`. These are intentional additions from the user-pinned visual reference, not palette drift.

Windows master volume synchronization uses Core Audio endpoint notifications (`audio.level.changed`), with device reconnect/unregister and4s snapshot polling only as fallback. External updates never echo audio.set; pending reads/write responses cannot overwrite a newer notification. Drag input stays under user control; pointercancel/lostcapture/windowblur restore the latest external value.

### SVG icons and help previews

Shared web icons retain their declared square size inside flex layouts. Native help preserves the same 24×24 SVG coordinate space in a fixed Canvas, scaled uniformly to 28 DIP; individual path bounds are never stretched to fill the preview. Stroke joins and caps are round, matching the web icons. Help uses the first SVG in a button, excluding a separate menu chevron from the native preview.

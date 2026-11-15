# SideWidget

[Русский](../../README.md) · [English](README.en.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [Deutsch](README.de.md) · **Українська** · [Беларуская](README.be.md) · [Español](README.es.md) · [Português (Brasil)](README.pt-BR.md)

SideWidget — компактна панель для повсякденних завдань у Windows. Біля верхнього краю екрана видно годинник. Натисніть на нього, щоб відкрити пошук файлів, закріплені програми та інші інструменти. Типове сполучення клавіш — `Ctrl+Alt+Space`.

SideWidget використовує мову Windows. Якщо її немає в списку, застосунок відкривається англійською. Мову можна змінити в меню **Налаштування → Мова застосунку**.

SideWidget розробляється зі здоровим глуздом і за допомогою GPT-6.1 Sol.

## Знімки екрана

<p align="center">
  <img src="images/sidewidget-home.png" width="760" alt="Головна панель SideWidget із пошуком, програвачем і швидким доступом до інструментів">
</p>

<p align="center">
  <img src="images/sidewidget-commands-uk.png" width="590" alt="Сортування файлів в українській версії SideWidget">
</p>

Програвач знято у Windows під час відтворення музики. На знімках інструментів використано демонстраційні дані.

## Приклади команд

Відкрийте **Файли й папки**, вставте команду й перевірте план перед застосуванням.

- **Сортування.** `Розклади рахунки за роками, а знімки екрана залиш на робочому столі`
- **Значки папок.** `Зміни значки папок на робочому столі`<br>`Зміни значок папки на фото`

[Усі дев’ять мов](../../README.md#примеры-команд)

## Інші інструменти

<details>
<summary><strong>Калькулятор</strong></summary>

Обчислюйте вирази з відсотками й дужками, копіюйте результат і повертайтеся до обчислень з історії.

<img src="images/sidewidget-tool-calculator.png" width="520" alt="Калькулятор">

</details>

<details>
<summary><strong>QR-коди</strong></summary>

Перетворюйте текст або посилання на QR-код і зберігайте його у SVG, PNG чи JPEG.

<img src="images/sidewidget-tool-qr.png" width="520" alt="QR-коди">

</details>

<details>
<summary><strong>Конвертер одиниць</strong></summary>

Перетворюйте відстані, температуру й інші величини. Улюблені пари одиниць можна зберегти.

<img src="images/sidewidget-tool-units.png" width="520" alt="Конвертер одиниць">

</details>

<details>
<summary><strong>Піпетка й кольори</strong></summary>

Виберіть колір з екрана, скопіюйте його у HEX, RGB чи HSL та збережіть у палітрі.

<img src="images/sidewidget-tool-colors.png" width="520" alt="Піпетка й кольори">

</details>

## Можливості

- Калькулятор з історією, QR-коди у форматах SVG, PNG і JPEG, перетворення одиниць, піпетка та перетворення форматів кольорів.
- Пошук файлів через запущений Everything і швидкий запуск закріплених програм.
- **Файли й папки.** Упорядкування файлів на робочому столі, попередній перегляд значків папок, спільний стиль або окремий для кожної папки та скасування останньої зміни значків.
- Керування відтворенням у сумісних програвачах, мікшер гучності й показники системи.
- Календар, локальна чернетка Markdown і переклад через зовнішні сервіси.
- Налаштування розміру, сполучення клавіш і вибір моніторів, на яких відображається панель.

## Встановлення

Для Windows 10 або 11 x64. Відкрийте [Releases](https://github.com/Kelltomal/SideWidget/releases/latest) і виберіть ZIP.

- **Lite** (`-lite-win-x64.zip`) використовує спільні бібліотеки, які достатньо встановити один раз.
- **Повна версія** (`-portable-win-x64.zip`) містить бібліотеки для автономного запуску.

Розпакуйте ZIP в окрему папку. Для Lite запустіть у ній [Install-Dependencies.bat](../../scripts/Install-Dependencies.bat). Скрипт використовує WinGet. Потім відкрийте `SideWidget.exe` поруч із папкою `App`.

### Встановлення залежностей Lite вручну

Встановіть ці пакети x64 один раз. Посилання ведуть до інсталяторів Microsoft.

- [.NET Runtime 10.0 x64](https://aka.ms/dotnet/10.0/dotnet-runtime-win-x64.exe)
- [ASP.NET Core Runtime 10.0 x64](https://aka.ms/dotnet/10.0/aspnetcore-runtime-win-x64.exe)
- [Windows App Runtime 1.8.12 x64](https://aka.ms/windowsappsdk/1.8/1.8.260921001/windowsappruntimeinstall-x64.exe)
- [Microsoft Edge WebView2 Evergreen](https://go.microsoft.com/fwlink/p/?LinkId=2124703)
- [Microsoft Visual C++ Redistributable v14 x64](https://aka.ms/vc14/vc_redist.x64.exe)

SideWidget перевіряє стабільні випуски на GitHub під час запуску. Також можна відкрити **Налаштування → Оновлення**. Lite оновлюється через Lite ZIP. Щоб перейти з повної версії на Lite, закрийте SideWidget у треї та запустіть `SideWidget.exe` з папки Lite. Повна версія теж обере Lite для оновлення, якщо встановлено всі спільні бібліотеки.

[Everything](../../README.md#everything) · [voidtools x64](https://www.voidtools.com/downloads/)

## Документація проєкту

[Збирання та участь у розробці](../../CONTRIBUTING.md) · [Технічна довідка російською](technical.ru.md) · [Повідомити про помилку](https://github.com/Kelltomal/SideWidget/issues/new/choose) · [Повідомити про вразливість](../../SECURITY.md) · [Переклад через Weblate](translating.md)

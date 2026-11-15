# SideWidget

[Русский](../../README.md) · **English** · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [Deutsch](README.de.md) · [Українська](README.uk.md) · [Беларуская](README.be.md) · [Español](README.es.md) · [Português (Brasil)](README.pt-BR.md)

SideWidget is a compact panel for everyday Windows tools. A clock sits at the top edge of the screen. Click it to open file search, pinned apps, and other tools. The default shortcut is `Ctrl+Alt+Space`.

SideWidget follows the Windows display language and falls back to English. You can switch languages under **Settings → App language**.

SideWidget is developed with common sense and help from GPT-6.1 Sol.

## Screenshots

<p align="center">
  <img src="images/sidewidget-home.png" width="760" alt="SideWidget home panel with search, media controls and quick tools">
</p>

<p align="center">
  <img src="images/sidewidget-commands-en.png" width="590" alt="Sorting files in SideWidget in English">
</p>

The player was captured on Windows during music playback. Tool screenshots use sample data.

## Example commands

Open **Files and folders**, paste a command, and review the plan before applying it.

- **Sorting.** `Sort invoices by year and leave screenshots on the desktop`
- **Folder icons.** `Change desktop folder icons`<br>`Change the folder icon to photos`

[All nine languages](../../README.md#примеры-команд)

## More tools

<details>
<summary><strong>Calculator</strong></summary>

Evaluate an expression with percentages and parentheses, copy the result, or recall a calculation from history.

<img src="images/sidewidget-tool-calculator.png" width="520" alt="Calculator">

</details>

<details>
<summary><strong>QR codes</strong></summary>

Turn text or a link into a QR code and save it as SVG, PNG, or JPEG.

<img src="images/sidewidget-tool-qr.png" width="520" alt="QR codes">

</details>

<details>
<summary><strong>Unit converter</strong></summary>

Convert distance, temperature, and other measurements. Save frequently used unit pairs.

<img src="images/sidewidget-tool-units.png" width="520" alt="Unit converter">

</details>

<details>
<summary><strong>Color picker</strong></summary>

Pick a color from the screen, copy it as HEX, RGB, or HSL, and save it to your palette.

<img src="images/sidewidget-tool-colors.png" width="520" alt="Color picker">

</details>

## Features

- Calculator history, QR codes in SVG, PNG, and JPEG, unit conversion, a screen color picker, and color conversion.
- File search through a running Everything instance and quick access to pinned apps.
- **Files and folders.** Organize desktop files, preview folder icons, choose a shared style or set one for each folder, and undo the last icon change.
- Playback controls for compatible players, a volume mixer, and system metrics.
- Calendar with optional SellyBoard integration, a local Markdown draft, and translation through external services.
- Customizable size, keyboard shortcut, and display selection.

## Getting started

SideWidget requires a compatible 64-bit (x64) edition of Windows 10 or 11.

1. Open [Releases](https://github.com/Kelltomal/SideWidget/releases/latest) and download the portable Windows x64 ZIP.
2. Extract the entire archive to a writable local folder.
3. Run `SideWidget.App.exe`. The ZIP includes .NET and WebView2. Keep the extracted folder together when moving SideWidget to another computer.

At startup, SideWidget checks GitHub for a newer stable release. Update now, defer until next launch, or skip that version. To update, SideWidget verifies the ZIP against its SHA-256 checksum, extracts the build, and restarts. You can also check manually under **Settings → Updates**.

## Project documentation

[Build and contribute](../../CONTRIBUTING.md) · [Technical reference in Russian](technical.ru.md) · [Report a bug](https://github.com/Kelltomal/SideWidget/issues/new/choose) · [Report a vulnerability](../../SECURITY.md) · [Translate with Weblate](translating.md)

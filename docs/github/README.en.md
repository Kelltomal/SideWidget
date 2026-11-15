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
- Calendar, a local Markdown draft, and translation through external services.
- Customizable size, keyboard shortcut, and display selection.

## Getting started

For 64-bit (x64) Windows 10 or 11. Open [Releases](https://github.com/Kelltomal/SideWidget/releases/latest) and choose a ZIP.

- **Lite** (`-lite-win-x64.zip`) is recommended. The 0.3.2 ZIP is about 20 MB, and Lite downloads smaller updates. Install the shared runtimes once before the first launch.
- **Full** (`-portable-win-x64.zip`) includes its own runtimes for offline use.

For the full edition, extract the ZIP and open `SideWidget.exe`.

### First launch with Lite

1. Download and extract the entire `-lite-win-x64.zip`. Keep `SideWidget.exe`, `Install-Dependencies.bat`, and the `App` folder together. [Download Lite](https://github.com/Kelltomal/SideWidget/releases/latest)
2. Run `Install-Dependencies.bat` from the extracted folder with an internet connection. It uses WinGet and skips packages already installed. Approve the Windows installation prompt if it appears, then wait for `SideWidget Lite prerequisites are ready`. [View the script](../../scripts/Install-Dependencies.bat)
3. If SideWidget is running, choose **Exit** from its tray menu. Open the Lite copy of `SideWidget.exe`. Your settings are available under the same Windows account. Press `Ctrl+Alt+Space` to open the panel.

If you switched from the full edition and use autostart, enable **Start when signing in to Windows** in Lite settings. Windows will start this copy at the next sign-in.

If WinGet is unavailable or the script stops on a package, use the links below. Then open `SideWidget.exe` again.

### Install Lite dependencies manually

Install these x64 packages once. The links point to Microsoft installers.

- [.NET Runtime 10.0 x64](https://aka.ms/dotnet/10.0/dotnet-runtime-win-x64.exe)
- [ASP.NET Core Runtime 10.0 x64](https://aka.ms/dotnet/10.0/aspnetcore-runtime-win-x64.exe)
- [Windows App Runtime 1.8.12 x64](https://aka.ms/windowsappsdk/1.8/1.8.260921001/windowsappruntimeinstall-x64.exe)
- [Microsoft Edge WebView2 Evergreen](https://go.microsoft.com/fwlink/p/?LinkId=2124703)
- [Microsoft Visual C++ Redistributable v14 x64](https://aka.ms/vc14/vc_redist.x64.exe)

### Update Lite

SideWidget checks for stable releases on startup. To check manually, open **Updates** in settings and select **Check for updates**. **Update now** downloads the Lite ZIP, installs the new version, and restarts the widget. The dependency installer is only needed for initial setup.

When upgrading from 0.3.1, install 0.3.2 manually. Follow the Lite setup above or extract the full edition and open `SideWidget.exe`. Starting with 0.3.2, the full edition selects Lite for updates when the required shared runtimes are already installed.

[Everything](../../README.md#everything) · [voidtools x64](https://www.voidtools.com/downloads/)

## Project documentation

[Build and contribute](../../CONTRIBUTING.md) · [Technical reference in Russian](technical.ru.md) · [Report a bug](https://github.com/Kelltomal/SideWidget/issues/new/choose) · [Report a vulnerability](../../SECURITY.md) · [Translate with Weblate](translating.md)

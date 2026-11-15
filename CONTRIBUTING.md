# Contributing to SideWidget

## Report a problem or propose a change

Search [existing issues](https://github.com/Kelltomal/SideWidget/issues) before starting a new one. Include the SideWidget version and Windows build in bug reports. For display issues, add the number of monitors, their resolutions and scaling. Follow the [security policy](SECURITY.md) for vulnerabilities.

## Build and test

Use Windows x64, PowerShell 7 (`pwsh.exe` on PATH), and Node.js with npm. Windows CI uses Node.js 24. For local builds on Node.js 22, use version 22.12 or later. The bootstrap script installs the pinned .NET SDK locally. The build downloads the fixed x64 WebView2 runtime and publishes the portable app to `artifacts/release`.

From the repository root in PowerShell 7:

```powershell
./scripts/bootstrap.ps1
./scripts/build.ps1
./scripts/test.ps1
./scripts/run.ps1
```

Exit SideWidget from its notification-area menu before rebuilding a running copy in `artifacts/release`. The test script runs the C# and web checks. For changes to WinUI behavior or system integration, also exercise the relevant flow in the Windows app.

## Submit a pull request

Keep the change focused. Explain the user-facing behavior or bug and list the checks you ran. Include a screenshot or recording when it helps reviewers understand a visual change. Use sample data in screenshots, logs, and fixtures. Follow the [code of conduct](CODE_OF_CONDUCT.md).

The [README language selector](README.md) links to all translated overviews. When changing an overview, update the affected translations or note which ones need follow-up.

# Contributing to SideWidget

## Report a problem or propose a change

Search [existing issues](https://github.com/Kelltomal/SideWidget/issues) before starting a new one. Include the SideWidget version and Windows build in bug reports. For display issues, add the number of monitors, their resolutions and scaling. Follow the [security policy](SECURITY.md) for vulnerabilities.

## Build and test

Use Windows x64, PowerShell 7 (`pwsh.exe` on PATH), and Node.js with npm. Windows CI uses Node.js 24. For local builds on Node.js 22, use version 22.12 or later. The bootstrap script installs the pinned .NET SDK locally. The full build bundles its runtimes and publishes to `artifacts/release`. Run `./scripts/build.ps1 -Lite` for a framework-dependent build in `artifacts/release-lite`. [Lite prerequisites](docs/github/README.en.md#getting-started)

From the repository root in PowerShell 7:

```powershell
./scripts/bootstrap.ps1
./scripts/build.ps1
./scripts/test.ps1
./scripts/run.ps1
```

Exit SideWidget from its notification-area menu before rebuilding a running copy in `artifacts/release`. The test script runs the C# and web checks. For changes to WinUI behavior or system integration, also exercise the relevant flow in the Windows app.

## Publish a release on GitHub

Push the matching `vX.Y.Z` tag to run the Windows release workflow. In GitHub's **Assets** block, the first two entries must be `00-SideWidget-X.Y.Z-portable-win-x64.zip` and `01-SideWidget-X.Y.Z-lite-win-x64.zip`, in that order. The workflow makes byte-for-byte copies of the verified archives so GitHub's alphabetical sort places them above the libraries.

Keep the regular Portable and Lite ZIPs, the `SideWidget-X.Y.Z-deps-*` bundles, and their checksum files under their exact names. Older SideWidget versions and the Lite launcher use those names to find updates and dependencies. Verify the published order and check that a previous version can download and validate the new release before announcing it.

## Submit a pull request

Keep the change focused. Explain the user-facing behavior or bug and list the checks you ran. Include a screenshot or recording when it helps reviewers understand a visual change. Use sample data in screenshots, logs, and fixtures. Follow the [code of conduct](CODE_OF_CONDUCT.md).

The [README language selector](README.md) links to all translated overviews. When changing an overview, update the affected translations or note which ones need follow-up.

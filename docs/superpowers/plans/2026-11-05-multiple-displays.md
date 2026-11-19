# Multiple Displays Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans. Track steps below.

**Goal:** Independent curtains on user-selected monitors with shared persisted data.
**Architecture:** A desktop window coordinator owns one shared state/session and creates one window per active selected display. Window-local controllers retain native dialog and shell callbacks. A single primary window owns tray and hotkey registration.
**Tech Stack:** C# WinUI 3, WebView2, React/TypeScript.
**Spec:** ../specs/2026-11-05-multiple-displays-design.md (approved).

## Global Constraints
Preserve existing data and design. No added dependencies. One process, one tray, one hotkey. No synchronized expansion.

## Review Focus
Disconnected selected displays must retain selection and return without duplicates.
Legacy monitor index must migrate without changing which connected screen is shown.
Removing the source display must allow the settings response to complete before closing it.
Background updates must preserve unsaved form fields and diary text.
Exit, hotkey conflict rollback, fullscreen, and mixed DPI must remain window-correct.

### Task 1: Display selection model
Files: Core/Preferences.cs, Core/DisplaySelection.cs, Core/Protocol.cs; tests/SideWidget.Tests/DisplaySelectionChecks.cs.
Interfaces: `DisplayDescriptor(string Id,string Name,bool Primary)`, `DisplaySelection.Resolve(Preferences,IReadOnlyList<DisplayDescriptor>) -> string[]`; preferences add nullable `DisplayIds`.
- [x] Write/run failing checks: old preferences, selected subset, disconnected subset, primary fallback, reconnect, duplicate/empty/oversized IDs, persisted roundtrip and bridge contract.
- [x] Implement model and validation. Run `.tools/dotnet/dotnet.exe run --project tests/SideWidget.Tests`; expect PASS.

### Task 2: Shared desktop session and windows
Files: App/DesktopWindows.cs, App/DisplayCatalog.cs, App/Controller.cs, App/MainWindow.cs, App/WindowServices.cs, App/Program.cs, App/PanelInteraction.cs.
Interfaces: shared session supplies PersonalState, Launcher, Timer, Online, Selly and Spectrum; coordinator supplies available displays, broadcast events/snapshots, refresh, settings validation, hotkey targeting and shutdown.
- [x] Integrate one curtain per resolved ID; reuse the primary window and defer refresh after command completion.
- [x] Keep expansion and pin local; share state changes and timer events; enumerate stable Windows display identities; update on topology/DPI notifications.
- [x] Keep drag within own selected display; preserve single-monitor transfer compatibility.
- [x] Build native app and report hardware limits (see verification report).
- [ ] Coordinated live native acceptance: current hardware windows, independent opening, one tray/hotkey, hotplug, persistence, shutdown.

### Task 3: Settings and synchronized web state
Files: Web/src/DisplaySettings.tsx, Web/src/types.ts, Web/src/main.tsx, Web/src/PersonalPanels.tsx; Web/src/DisplaySettings.test.tsx.
Interfaces: Snapshot adds available displays; Settings adds optional displayIds. `state.changed` carries shared snapshot, `displays.changed` carries current descriptors.
- [x] Write/run failing UI checks for checkboxes, last-selection prevention, disconnected screen, save payload, external snapshot preserving edits.
- [x] Replace monitor-number control with labeled monitor choices; retain all unrelated settings and existing controls.
- [x] Run web tests/build; native build; manual Impeccable detector and rendered settings inspection at 360/444 DIP.
- [x] Update documentation and verification report with evidence and limits.

## Execution ledger
Ruling: repository has no commits and all project files are untracked; linked worktree cannot include this working project. Use orphan `codex/multiple-displays` branch in place, preserve existing files, and do not commit unrelated files.
Ruling: execute approved scope inline under existing authorization; no further process-only approval round.

Task 1: complete — Core/Infrastructure1025 PASS, selection tests first failed before implementation.
Task 2: source complete — actual coordinator18 adapter checks PASS, read-only Windows catalog2displays PASS, native Debug build0warnings0errors. Real window acceptance pending coordinator slot.
Task 3: source complete — web188/188PASS21files, tsc/Vite PASS, 360/444 screenshots inspected, Impeccable[].
Final review: independent reviewer found2drag defects; fixed unavailable selection transfer eligibility and cancelled DisplayId restoration. No deferred minor findings. No initial commit/package/publish: centralized by project coordinator.

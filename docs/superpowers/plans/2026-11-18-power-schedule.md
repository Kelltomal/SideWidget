# Power schedule implementation plan

> **For agentic workers:** Use superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** Разовое выключение/перезагрузка через часы и минуты с отменой и отсчётом.
**Architecture:** Core PowerSchedule stores a deadline and clears before execution;
DesktopState shares it across windows. Controller validates and confirms commands.
React reads native state/events and renders existing timer styles.
**Tech Stack:** C#, WinUI 3, React, TypeScript, Vitest.
**Spec:** docs/superpowers/specs/2026-11-18-power-schedule-design.md

## Global constraints
No forced termination. App must stay running. Delay 1 minute–24 hours.
Hours and minutes can each be zero; both zero shows a warning.
Keep unrelated working changes intact. Publish feature changes to GitHub.

## Review focus
Zero/empty inputs; invalid/fractional values; replacement cancellation;
late or repeated ticks; shared state across windows and bridge reloads.

### Task 1: Scheduler and native commands
- [ ] Add failing PowerScheduleChecks for validation, replacement, cancel,
  overdue execution and at-most-once behavior, including executor failure.
- [ ] Run .tools/dotnet/dotnet.exe run --project tests/SideWidget.Tests.
- [ ] Add Core/PowerSchedule.cs with Schedule(action,hours,minutes,now),
  Cancel(), Tick(now,execute), State and Remaining(now).
- [ ] Wire shared DesktopState, protocol, confirmed Controller commands,
  timer ticks and shared events. Re-run core checks and native build.

### Task 2: Menu, fields and timer display
- [ ] Add failing UI checks for menu entries, zero warning and duration inputs.
- [ ] Add PowerSchedule.tsx hook/form/status, existing timer summary/panel integration.
- [ ] Run npm test and npm run build. Inspect rendered 360/444px views;
  run focused Impeccable detector. Never execute real power commands in checks.

### Task 3: Publish
- [ ] Review diff; commit only feature changes, preserving previous edits.
- [ ] Push feature branch to origin and report link and validation evidence.

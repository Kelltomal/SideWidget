# Compact Strip Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for inline execution.

**Goal:** Compact transparent clock/date strip over maximized applications.
**Architecture:** Pure Core policy owns covering/revealed state and double-click
recognition. A native layered HWND renders the compact header; MainWindow
switches between that surface and its existing WinUI panel.
**Tech Stack:** C#, Win32, WinUI 3; no new dependency.
**Spec:** ../specs/2026-11-05-compact-strip-design.md

## Global Constraints
- Height 23 DIP; opacity 30%; retain time and date.
- Hover/single click must not activate or open; double click opens.
- Preserve other features and the existing working tree.

## Review Focus
- Maximized windows on another monitor must not hide the header.
- Own panel focus must not clear compact mode after double click.
- Leaving/collapsing the panel must restore the strip.
- Separate slow/distant clicks must not open it.
- Native dialogs and capture operations must not disappear.

## Task 1: Policy and native integration
- [x] Add Core checks for covering state, reveal/fold/reset, double-click timing,
  click distance, triple clicks and reset. Run and observe missing-feature failure.
- [x] Implement CompactPanel policy and run the Core suite.
- [x] Implement native layered strip and connect detection, opening and collapse.
- [x] Build the native app, run project tests and focused Impeccable detector.
- [ ] Inspect native strip, single/double click, restore and focus behavior.

Execution is inline in the existing shared workspace. No staging/committing of
the otherwise untracked project. Preserve existing candidate processes until
GUI testing can safely switch to the new candidate.



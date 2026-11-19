# Focus timer implementation plan

> For agentic workers: use superpowers:executing-plans, inline execution.

**Goal:** Native alarm, countdown and manual-stage Pomodoro with a shared React control page.
**Architecture:** Pure Core state machine, persisted through Store; Controller bridge; native header and React page consume the same state.
**Tech Stack:** C#, WinUI 3, React, TypeScript; no new dependencies.
**Spec:** docs/superpowers/specs/2026-11-05-focus-timer-design.md (approved 2026-11-05; Pomodoro 25/5/15, manual next).

## Constraints and review focus
Preserve concurrent work and existing design. Stay in shared checkout for coordinated integration; no broad commits of this untracked repository. Do not overwrite release while another chat owns GUI. Never couple timer state with SellyBoard task tracking. Deadline, pause, midnight, restart and dismiss must be deterministic and tested. A stage must never start on dismiss. Disabled module must not cancel an active session.

## Task 1 — Core state and persistence
- [x] Add FocusTimerChecks.cs and call it from tests/SideWidget.Tests/Program.cs. Assert pause freezes remainder, resume shifts deadline, overdue restored state rings, 4 work sessions lead to long rest, dismiss prepares but never starts next, invalid values fail.
- [x] Run .tools/dotnet/dotnet.exe run --project tests/SideWidget.Tests; observe missing FocusTimer failure.
- [x] Create Core/FocusTimer.cs with FocusTimerState record and FocusTimer methods Start(mode,seconds,alarmTime,now), Pause/Resume/Reset/Dismiss/Tick. Use DateTimeOffset deadlines and immutable snapshots. Add Infrastructure/FocusTimerService.cs for Store persistence and transactional state updates.
- [x] Run Core suite green.

## Task 2 — UI and bridge
- [x] Add timer model tests and TimerPanel interaction tests for start, pause, dismiss, explicit next and invalid input. Observe red.
- [x] Create timer.ts, TimerPanel.tsx, timer.css. Preview persists and computes deadline independently of visible page. Native uses timer.changed events and timer.* commands. Forms use labelled fields and existing tokens; 360/444/799 widths.
- [x] Add narrow Controller/Protocol and main.tsx integrations after coordination; no edits to SellyBoard logic.
- [x] Run npm test and npm run build.

## Task 3 — Native header and verification
- [x] Create FocusTimerHeader.cs; initialise overlay in header, update clock from controller timer each second, compositor opacity breathing on completion and static reduced-motion state. Intercept signal click before header drag; no focus stealing.
- [x] Build native separately; inspect browser captures, run focused detector and independent review.
- [x] Native collapsed countdown/signal/click/restart GUI check after coordinator handoff. Found and fixed hover-open race, re-published separate staging and rechecked.
- [x] Record evidence/limitations in docs and notify coordinating chats.

## Ledger
- User explicitly approved design and manual next stage.
- Ruling: shared checkout retained because peer chats coordinate narrow shared-file integration; no established committed branch exists to isolate cleanly.

- Final verification: Core824 PASS; focused timer41 PASS; web122 PASS; TS/Vite build PASS; native Release build 0 warnings/errors. Focused Impeccable=[].
- Independent review findings addressed with regressions; browser complete four-cycle manual Pomodoro verified at 360/444/799 and reduced motion.
- Native GUI/release handoff recorded docs/UI_COORDINATION.md and docs/TIMER_VERIFICATION.md; no shared process restart or packaging.

- Native QA complete: 45-second real countdown restored 00:24 after restart; expiry kept Notepad foreground; signal pulses; click returns clock with collapsed header. One-minute test Pomodoro work completed, dismiss produced ready break05:00 with null deadline, separate button started04:58. Original timer/preferences restored, staging app left running; GUI explicitly returned to coordinator. Full Core801 / focused41 PASS after hover fix (parallel translation removed/reworked other tests; earlier824 count not reused).

# LAN remote and Codex monitor implementation plan

> **For agentic workers:** Use superpowers:executing-plans or independent bounded workers.

**Goal:** Постоянный музыкальный LAN-пульт и инструмент Codex с лимитами и разовыми сценариями питания.
**Architecture:** Existing LAN server handles same-subnet HTTP music APIs. Read-only Codex readers provide typed snapshots; shared desktop service evaluates tested power policy. Existing PowerSchedule executes cancellable 60-second deadlines.
**Tech Stack:** C#, SQLite, WinUI3, React/TypeScript, existing Codex executable/local state.
**Specs:** ../specs/2026-11-18-lan-music-design.md, ../specs/2026-11-18-codex-monitor-design.md.

## Constraints / review focus
No remote arbitrary commands or WAN access. No Codex DB/auth writes or message reads.
Errors, queued input, stale/unknown data must never trigger shutdown.
No repeated limit trigger within one Windows boot, even after app restart/cancel.
Do not overwrite existing unrelated changes; use current isolated release worktree.

### LAN music
- [x] Add failing real HTTP tests: keyless state/commands, foreign origin/subnet/host rejection.
- [x] Implement permanent URL/QR and HTTP client path, preserving paired HTTPS compatibility.
- [x] Verify client reload/reconnect and mobile/desktop rendered views.

### Codex readers and policy
- [x] Add failing snapshot/limits parsing tests and deterministic shutdown policy tests.
- [x] Implement indexed read-only metadata reader and bounded account limit source.
- [x] Implement selected/all-current completion, boot-scoped one-shot exhaustion, cancel/restart/error behavior.
- [x] Wire shared native commands/events and existing timer cancellation/countdown.

### UI and release
- [x] Add Codex tool tile, chat selection/status, quota windows and power controls; test controls and failed reads.
- [x] Inspect actual renders and run focused design detector.
- [x] Run relevant .NET/web/native checks, fresh review, publish verified release to GitHub.

## Added user scope
- [x] Website monitor: URLs, intervals, persisted bounded history, explicit source limitations.
- [x] Calendar: month/week/list, native persisted CRUD, notes, done state, durable system reminders.
- [x] Fresh review fixes: no rollout-content fallback; relevant-source verification immediately before shutdown.

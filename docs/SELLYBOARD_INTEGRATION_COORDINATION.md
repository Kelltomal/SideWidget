# SellyBoard integration coordination

Owner: integration chat. User approved existing SB API/key integration, main and production.
No production change yet at this checkpoint (2026-11-05, 03:09 Moscow).

## Final contract

Use existing `/api/v1/calendar/options`, `/events`, `/tasks/{id}` and POST `/tasks/save`.
The old draft `/api/desktop` login/session approach was removed before integration.
Calendar-only `calendar:read`/`calendar:write` keys require explicit active user+company binding
and expiry. Ordinary company keys gain no personal access. Options includes decimal-string
account_id AND company_id, timezone and can_write. Standard service data/meta envelope.

Server worktree: C:/Users/Water/.codex/worktrees/calendar-sidewidget-api/CRM, based on main.
New control migration67; task service retains live ACL, billing, recurrence/version semantics.
Production target144.31.201.250 confirmed running existing API portal. Verified PC-only full
backup450404193 bytes plus source/config archive200989125 bytes; full restore and pg_amcheck
passed. Deployment completed; patch notes 309 (general) and 310 (SB administrators) published.

## SideWidget files and behavior

New Infrastructure/SellyBoardClient.cs and SellyBoardConnection.cs; App/SellyBoardTools.cs;
Web/SellyBoardPanel.tsx; narrow selly.* Controller/Protocol insertions, configure delegate in
MainWindow and calendar wrapper in PersonalPanels. Existing timer/translation changes retained.
Module IDs task/calendar and preferences order/disabled unchanged. Diary remains local.

Native key entry and DPAPI storage; no credential in WebView/SQLite. HTTPS except exact loopback,
no redirects/cookies. Full native card cached for versioned status changes. Repeat edits stay
in SB. Private all-day creation, exclusive end, self assignee. Unknown creation retains exact
payload/client_key across401 and reconnect; ownership includes server/company/account.
Updates invalidate cached card before sending and require refresh after uncertain result.

## Verified checkpoint

Native test executable passed806 checks (counts vary as other chats update timer/translation).
Web127 passed before final error-state fix; focused calendar4 passed after fix. New HTTP/client
checks cover IDs, base path, unsafe URL, redirects,409, full card preservation, unknown write,
401 retention and cross-company replay. Native build/publish passed, zero warnings/errors.
Impeccable detector new panel/form: []. Existing global hooks did not run; manual check used.

Actual WinUI648px: calendar opens; native key form hides WebView, password masked; empty submit
disabled; actual address/password input works; Escape and Cancel return to calendar; reopen works.
Found silent connection-error clearing, fixed with failing/passing UI test; final native retry
confirmed the error is visible. Initial GUI clash with motion chat resolved by coordinator. No real key or
user task data was entered. No live production SideWidget connection has been claimed.

Common initial git commit belongs to coordinator (unborn main). Per explicit coordinator:
no git add/commit/reset/clean here. Include all related bridge files with new modules together.
Do not package common release until coordinator's final combined checks.

## Production completion

Release 3d25e45e is running on selly-new. Main contains the same source. Migration 67,
application schema validation and final production control pg_amcheck passed; public HTTPS
health returned 200. Temporary read key: options/events 200, forbidden write 403, revoked 401.
Key revoked; no live user SideWidget connection configured. Published patches 309/310 were
checked through shared history/popup renderers in both themes at desktop/mobile widths.
CRM operations report: docs/operations/2026-11-05-calendar-api-sidewidget-release.md.
SideWidget combined commit/package remains coordinator-owned; no product edits after freeze.

# Codex shortcut and application drag Implementation Plan

**Goal:** Show an optional Codex quota ring beside favorite apps; add Explorer drops and persistent app reordering.
**Architecture:** Keep existing launcher validation and settings persistence. Transfer dropped File objects through WebView2 AdditionalObjects; never accept caller-supplied filesystem paths. Render two duration-based quota rings with an explicit unknown state. Use existing apps.reorder for both shortcut buttons and the full app list, with keyboard alternatives.
**Tech Stack:** React, TypeScript, WinUI 3, WebView2, C#.

- [x] Add failing tests for real quota rings, unavailable data, internal reorder, and external file transfer.
- [x] Add Codex shortcut preference, ring button and panel toggle using existing tokens.
- [x] Add reorder handlers to favorites and app list; preserve hidden apps and avoid launching on drop.
- [x] Add external drop zone and correlated bridge attachments; validate native file objects through the existing launcher.
- [x] Run web, core and launcher tests, locale checks, production build, detector and source review.
- [x] Update installed Lite with backup; inspect native UI and verify drop/reorder without leaving test apps pinned.

Ruling: User confirmed both adding from Explorer and rearranging existing apps. Keep the current design and optional ring shortcut enabled initially. A missing quota never becomes 100% or 0%.

Verification: 284 web tests, 1102 core checks, 10 Windows checks passed (public network probes skipped). Production Lite publish and manifest validated. Focused Impeccable detector returned no findings; independent source review found no actionable bugs. Real WebView2 File objects pinned a local .lnk; UI drag persisted order; ring navigation and preference save succeeded; verification restored original apps and order. Captured and inspected installed home panel.

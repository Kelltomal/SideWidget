# Compact Codex quota ring

User confirmed: show while desktop Codex runs, independently disable in settings,
reuse the existing two nested quota circles. Outer ring is weekly; inner ring is
five-hour. Missing, expired, and stale data stay unknown, represented by dots.

Use the existing CodexMonitorService and official account/rateLimits/read data.
No rollout parsing. Preserve the launcher shortcut and its separate preference.
Add the ring beside the normal collapsed clock and to the native layered strip,
including clock-only mode. Keep timer interactions and existing native motion.

- [x] Regression tests for duration mapping, zero/unknown/expired quotas, visibility and persistence.
- [x] Shared desktop presence detection and read-only access to existing quota snapshot.
- [x] Native clock and layered-strip rings, preserving the existing visual design.
- [x] Persisted clock preference, bridge allowlist, settings UI, nine locales.
- [x] Builds, tests, detector and real Windows/browser visual verification.

Ruling: the repository root is an older dirty branch. Implement in the active
production worktree artifacts/power-schedule-release, where the existing ring
and Codex monitor live; remove only this task's superseded root feature edits.

Verification: 286 web tests, 1111 core checks, native build and Lite publish passed.
Focused detector returned no findings. Real WinUI header and GDI layered strip
were captured and inspected with two fixture quotas; the existing official reader
remains the source in normal use. Settings toggle checked at 360px without overflow.
Installed application was not replaced. Candidate: artifacts/release-lite-codex-ring.
Distribution verification passed: portable dependencies and SHA-256 manifest.

Follow-up: user requested the clock itself stay centered, with the ring to its right.
Removed the combined centered group, balanced native header insets, and positioned
the strip ring from measured clock text while keeping the date at the right edge.
Native geometry: clock center 222.04px in a 444px header; toggle shift 0px.
Captured and inspected both native surfaces. Native build and focused detector passed.
Updated candidate: artifacts/release-lite-codex-centered.

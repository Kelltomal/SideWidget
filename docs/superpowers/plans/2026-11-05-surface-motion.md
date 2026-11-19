# SideWidget surface and expansion implementation plan

**Goal:** Borderless graphite surfaces, tactile feedback, and opening to 180% of collapsed width with a decelerating finish.

**Architecture:** Preserve WinUI/WebView2/Medeo and current actions. Derive horizontal expansion from the existing interruptible height motion; compute monitor-clamped geometry in Core. CSS handles pointer feedback and page transitions; keyboard and reduced-motion paths remain immediate.

**Spec:** User approved graphite surfaces without decorative outlines. Subsequent requests specify 40% expansion on each side and smooth deceleration.

**Constraints:** Shared checkout; coordinate native files and release with the implementation chat. Do not interfere with its GUI session. Keep focus and forced-colors outlines. No animation dependency.

## Tasks

- [x] Add failing Core checks: centered growth, both monitor edges, narrow/negative-coordinate monitors, reverse motion and collapsed restoration.
- [x] Implement pure panel geometry and expansion progress; connect native placement, drag and resize while preserving saved base width.
- [x] Refine existing CSS and add pointer-modality handling, 120–180 ms press/page feedback and reduced-motion support.
- [x] Run focused Core checks and web checks/builds; inspect rendered home/forms at 360, 444 and expanded widths; verify pointer and keyboard paths. Broad Core suite pending concurrent SellyBoardClient completion.
- [x] Run focused Impeccable detector, update DESIGN and coordination evidence, agree native release/restart and verify runtime. Final combined packaging belongs to coordinator.

## Review focus

Monitor clamping must not lose the collapsed anchor. Repeated opens must not compound saved width. Resize must persist base width, not 180% width. Opening reversals must not jump. Pointer cancellation, keyboard input, reduced motion and disabled controls must retain usable states.

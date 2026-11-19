# Surface and motion verification — 2026-11-05

Scope: graphite/Medeo refinement, removal of decorative outlines, pointer feedback and 40% horizontal expansion on each side. Existing surface direction retained; no catalog redraw or replacement design system.

## Verified

- Core: monitor clamping at left/right, negative monitor coordinates, narrow monitor, centered 180% width, inverse drag/resize placement, bounded rapid reversal, height changes without width multiplication, strictly decreasing expansion increments. Test command: `.tools/dotnet/dotnet.exe run --project tests/SideWidget.Tests`; latest observed 748 checks passed (includes concurrently updated online checks).
- Native compile: Release build to separate `artifacts/visual-check`, zero warnings/errors. No restart of another chat's active GUI process.
- Web: final integrated run 114 tests passed across seven files, including concurrent music/layout changes; TypeScript/Vite build passed.
- Edge browser screenshots: `output/playwright/home-360.png`, `home-444.png`, `home-799.png`, `calculator-360.png`, `calculator-444.png`, `settings-360.png`, `settings-799.png`. Checked flat surfaces, label wrapping, hierarchy, full-width fields, no horizontal document overflow, and actual local Medeo load.
- Real pointer hold: computed scale 0.970042, 140 ms transition. Page navigation produced two active transitions at 180 ms. Enter/Escape changed modality to keyboard and computed page transition to 0 s.
- Browser reduced motion: pressed transform `none`, page transform `none`, opacity transition 80 ms. Forced colors restored visible system button borders.
- Impeccable automatic design hook reported no deterministic issues on style.css. Required focused manual detector also returned `[]` for style.css/main.tsx. No hook configuration changed.

## Native verification completed

- Actual window and client area both measure 648 × 620 at 96 DPI, top 0, left 956. Former 3 px non-client bevel is gone in both expanded and collapsed captures. See `native-frame-verified.json` and `screenshots/surface-motion-{expanded,collapsed}.png`.
- Constructor style removal alone was insufficient: activation restored WS_DLGFRAME and WS_EX_WINDOWEDGE. Returning zero for WM_NCCALCSIZE(TRUE) makes the complete window client area. Primary reference: [Microsoft WM_NCCALCSIZE documentation](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-nccalcsize).
- Actual repeated pointer opening grows base 360 to 648 px, centered 144 px either side. Native samples: 420, 534, 584, 619, 636, 645, 648, 648 px. First visible to final sample: 213 ms; nominal curve duration: 240 ms. Consecutive recorded samples are 25–34 ms apart on this machine; this is native frame evidence, not a claim of 60 fps. Timing and positions are saved in `native-motion-verified.json`.
- Removed no-op SetWindowPos at motion start and restarted DispatcherTimer when leaving its idle cadence; this reduced the observed first pointer frame from 592 to 420 px. Width and height share the cubic ease-out, and ending steps diminish to zero.
- DeepL form opens visibly; empty key keeps Save disabled. Header click leaves the native form visible. Keyboard focus visibly cycles key field, plan, Cancel, then key field, skipping disabled Save. Escape and Cancel return to translation. Screenshot: `screenshots/surface-motion-dialog.png`. No key was entered or saved.
- Final focused execution of the existing PanelGeometryChecks against current source: 118/118 passed via `artifacts/motion-check`. Latest shared native publish succeeded and was exercised above.

## Integration limits

The latest broad Core test command is blocked by concurrently added SellyBoardChecks referencing an unfinished SellyBoardClient; those tests were not removed or excluded. The coordinator must repeat the integrated suite once that client is ready. Other concurrent chats own timers and embedded translators; this check covers the coherent published native build, not later source changes or a final combined archive. No archive was created here. Development preview logs show Vite websocket blocked by existing `connect-src 'none'` and missing favicon; production CSP was preserved. Actual multi-monitor DPI and Windows reduced-motion behavior have pure geometry/code coverage, not a claim of hardware testing.

## Motion review (`review-animations`)

| Before | After | Why |
| --- | --- | --- |
| First native animation frame included idle 100 ms interval | Start hover animation with zero elapsed time; pointer opening schedules 16 ms timer immediately | Avoid jumping through most of the curve on the first frame |
| Press and release both 140 ms | Deliberate press 140 ms, release 100 ms | Immediate response while keeping tactile feedback |
| Keyboard shared pointer motion | Capture input modality before action; keyboard has no page/press transition | Preserve fast repeated keyboard use |

Code, browser and native motion verdict: approve within the measured scope above. Native geometry uses real window resize because the requested interaction changes usable window bounds; a CSS transform would scale text and retain incorrect hit testing.

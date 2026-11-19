# Motion native verification — 2026-11-05

Fresh isolated staging: `artifacts/motion-final-check`, fresh web dist. Final running process PID32396; collapsed, no dialog open. No general release packaging or git actions performed.

Verified on actual Windows app at DPI96:

- Hover over collapsed header leaves panel collapsed across several captures.
- Header click opens after release. Root surface remains aligned; no non-client bevel/frame visible in collapsed or expanded captures.
- Collapsed header drag +120px changes visible left1460 from1340 and remains360x52. Reverse drag restores original position exactly. Screen cursor fix prevents accumulating movement from queued events.
- Animation diagnostics: all frames retain HWND648x620, including collapsed and expanded. First two measured openings:70 adjacent samples, median7ms, maximum13ms. This measures application frame/geometry submissions on this machine, not compositor presentation or performance at other DPI.
- Actual3-second timer started via native-connected WebView, expired, collapsed header showed “Время вышло”. Header click dismissed signal and remained360x52; stored state idle, deadline null. No auto-start/transition occurred for this ordinary countdown. Pomodoro phase semantics were not retested.
- Native SellyBoard dialog fully covers WebView, header drag does not move window; no overlay from WebView. Empty form only, no credentials entered/submitted.
- Found/reproduced: after header drag attempt while native dialog open, Escape stopped dismissing until address field was refocused. Narrow fix: ShowNativeDialog exposes temporary focus action; HeaderPressed with dialogCount>0 focuses Cancel and handles press. Fresh isolated publish and same header-drag/Escape sequence now dismisses immediately. Field Escape also passed before fix. Only PanelInteraction.cs and WindowServices.cs changed in this final QA pass.
- Final Escape sequence returned calendar to home, then collapsed panel.

Limits: tool API offers complete click/drag, no separate pointer-down/up. Hold without movement and actual pointer-capture-loss injection were not tested live. HeaderGesture unit checks cover long hold, Cancel followed by release, movement then return to origin, release displacement and vertical movement; all pass. DPI125/150/200, disconnected monitor, reduced-motion setting and compositor/frame presentation recording were not exercised.

Data: backed up only personal.preferences.v2 and personal.focusTimer.v1 to local AppData/SideWidget/motion-qa-restore.json, not repository. Reversed test drag, then restored exact original values with app stopped. Final database checks show both strings equal originals. Other settings/keys unchanged by test code. Original timer idle; restored idle. No secrets read/changed.

Final verification after focus fix: native self-contained publish successful;887 C# checks passed; full web126 tests passed; TypeScript/Vite build passed. Manual global Impeccable detector scanned the two changed native files with regex-only coverage; no findings emitted. No claim of parsed DOM coverage or automatic hook execution.

Files are ready and released; no more source changes planned. GUI EXPLICITLY RETURNED TO COORDINATOR 01a1a8de-e3ce-7ef1-81d8-44c324d641e4. No sky request active; shared mouse free. Coordinator may close PID32396 normally or use this staging. General package remains coordinator-owned.

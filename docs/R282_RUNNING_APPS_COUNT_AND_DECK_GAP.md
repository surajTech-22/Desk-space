# R282 — Running Apps accurate count and non-overlapping Deck placement

Reported screenshot 1 November 2026 shows **Deck · 2 apps**, **Running Apps · 2** and a Deck popup crowding/overlapping the top of the circular weather/clock widget. The user states **three applications** should be represented. This work corrects two credible app discovery/count errors without hard-coding a count, and fixes the actual popup geometry.

## Running Apps
- Previously `SnapshotWindows` discarded every untitled HWND and grouped windows using the display name from `FileDescription`, so **different executables with the same human-readable description** collapsed into one tile. The header then echoed that undercount.
- R282 includes **untitled top-level windows only when they explicitly have `WS_EX_APPWINDOW`** and pass the existing visibility, shell exclusion, DWM cloak and process checks.
- R282 disambiguates only colliding display labels by their actual executable paths, while retaining multiple windows from one executable as one app. `GroupVisible`, the tile grid, hover flyouts, and the Deck header all use those same normalized entries. Three real app groups therefore display **3**, rather than claiming three from hard-coded text.
- Untitled windows retain their actual blank title; their display label is the app name. Actions require the original HWND plus the same process ID and window class; titled-window stale-handle protection remains intact. No process kill or auto-foreground action.

## Popup/clock overlap
- Existing `DeskSpaceMonitorDockV190.Calculate` correctly chooses a side when there is space, but `Side.Contained` and a saved manual offset can push the real Deck popup back into the clock; popup clamping or WPF layout can also move the HWND after calculation.
- R282 limits the Deck **scrollable viewport height** to the actual side space on the widget's assigned monitor when no full-size side placement is available, reserving a visual gap. It prefers a narrower left/right Deck when that side is usable.
- A new `AvoidWidgetV282` checks the Deck vs the actual widget bounds and chooses an unoccupied same-monitor placement. In addition, a post-WPF `HWND` check corrects the **painted** popup location after opening and widget movement, not only the intended callback location. This preserves per-monitor offsets and does not move the other monitor's widget/Deck or staged held files.
- The original hover-only Running Apps interaction, individual multi-window previews, title-matched Close/Minimize/Restore and 3-column order remain.

## Verification and limits
Windows native regression uses synthetic distinct executables with colliding descriptions (three app tiles), same-process multiple windows, an untitled explicit taskbar window, stale PID/class refusal, a physically overlapping popup rectangle, compact scroll near a monitor edge, and a negative-origin secondary display. Existing native WPF and full source-parity tests must stay green.

The screenshot alone does not reveal which third process was missing; the code fixes demonstrable undercount paths but no claim of reproducing the user's exact third app is made without installed-PC evidence. Actual multi-monitor mixed-DPI UI needs physical verification. Do not publish Stable/Tester before the consolidated acceptance gate.

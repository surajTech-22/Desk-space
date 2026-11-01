# R239 — on-demand monitor diagnostics (development)

DeskSpace Workspace **Features → Monitor diagnostics** now offers a read-only Windows device report: connected display count, primary/secondary identity, native pixel display and work-area rectangles (including negative screen origins), Windows-reported effective DPI when available, and each monitor's saved Deck drag offset.

The UI is user-invoked and refreshed only by a button. It does not use a timer, install hooks, inspect workbooks, read clipboard contents, collect application window titles, save state or send data over the network. DPI is advisory and may depend on process awareness and graphics driver, not a claim of physical dual-monitor acceptance.

R238 protects corrupt per-monitor data without overwriting it. R239 reads only that saved map and displays default (0,0) when unavailable.

Testing: strict source guard and native WPF synthetic negative-origin primary/secondary reports, full Windows build, WPF smoke and independently rebuilt canonical sources. Real mixed-DPI and physical cursor/drag input acceptance are separately required. No stable/tester release until all readiness gates pass.

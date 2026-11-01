# DeskSpace — P0/P1 Execution Matrix
**Source:** user-supplied *Complete Feature Research & Expansion Plan*, 31 October 2026.  
**Branch:** `feat/r201-consolidated-roadmap-bundle`, draft PR #79.  
**Rule:** Implemented ≠ native Windows CI tested ≠ physical acceptance ≠ released. **No P2/P3 feature work ahead of P0/P1 unless it blocks a P0/P1 requirement.**

**Latest development evidence:** R280 + R281 combined Windows CI #563 successful. Feature code is not a released update.

## P0 — reliability and correctness (do first)

| Requirement from plan | Actual repository status | Required next evidence |
|---|---|---|
| Independent widgets and their positions | R278 replaced process-local and corrupt-map-overwriting widget position save with monitor-keyed, cross-process mutex, strict JSON validation, atomic writes. Windows CI #530/#540 PASS. R228/R235 already handle independent Deck offsets and shared held-file store | Physical mixed-DPI two-screen hover, widget moving, saved offsets across restart, simultaneous updates |
| Monitor identity, docking, inward popup and reconnect | R152/R153, R190, R214, R231/R236, R247, R264 have native HWND monitor fences, per-monitor deck geometry, resume reconciliation and synthetic topology tests | Real attach/detach and sleep/reconnect; prove separate widgets never attract and popup/flyouts do not cross screens |
| Safe updates, all-process graceful closure and rollback | R219–R230, R249/R250 provide staged download, no-force WM_CLOSE, installer checks, synthetic full rollback and candidate-hash/provenance policies | Installed primary+satellite+tray update with actual old/new app processes, heartbeat/failed-update rollback, preserved data; valid publisher certificate |
| Authoritative source and repeatable Windows build | R267 committed `src/DeskSpace` and requires exact regenerated source SHA parity, independent build and a verified archive. CI #540 PASS | Final signed build and identical Stable/Tester deliverables after acceptance |
| Strict Excel paste transaction | R107/R139–R147, R242/R251/R252/R266/R268 implement full-range gate, formula+format transaction, SaveCopyAs receipt, formula readback/rollback structure | Three real Windows Microsoft Excel workbooks with COM, tables, formulas, dates and deliberate failure/rollback. No Office installation in current GH runner |

## P0 — additional R285 source audit: actual process DPI mode

Microsoft's WPF/Windows guidance distinguishes system-DPI-aware apps from Per-Monitor V2 apps. Prior to R285, `DeskSpace.csproj` had no `ApplicationManifest`, even though the source positions widget/Deck HWNDs in native monitor coordinates.

R285 declares **PerMonitorV2** with a real application manifest and adds `--dpi-audit`, which runs the **actual DeskSpace.exe** without constructing a widget or tray. CI must confirm that Windows reports PMv2 on the built application, not merely on a synthetic geometry test. The existing Displays settings page gains a manual *Save actual monitor / DPI diagnostic to Downloads* button so real mixed-DPI setup can later be audited. This remains **development**, not physical multi-monitor acceptance.

Further app improvement opportunities are tracked in [R285 source audit](R285_PMv2_AND_RESEARCH_AUDIT.md), prioritized as real updater rollback, Excel COM acceptance, mixed-DPI hardware, high-contrast/light-theme consistency, Running Apps physical UI and sustained performance profiling. Defer unrelated P2/P3 additions.

## P1 — finish existing modules before adding utilities

| Requirement from plan | Actual source status | Required next work/evidence |
|---|---|---|
| Captured Sheet + Mappings, separate paste engines | R126 already has exactly two unified tabs and routes Information vs Formats/Formulas separately. This is **implemented code**, not an unstarted feature | Actual Windows Excel UI origin selection, copy/format/formula/table/blank-values, source library and save/delete behavior |
| Running Apps, grouped thumbnails, quick controls | R261 grouped order; R93 stationary hover and monitor-side flyout; R279 added explicit per-window Minimize/Restore controls alongside normal Close and exact stale-HWND title guards. Native CI #540 PASS | Real multi-window Explorer/Office selection, focus/minimize/restore/close and dual-monitor positions |
| Shared Deck/media/files | R210/R211 staged file ZIP integrity and duplication; R243/R272 media Deck controls and reopen | Media session on user's PC, two widgets showing shared held data while deck placement is independent, real Explorer drag |
| Active-app Smart Context Deck | **R280 implemented in the real Deck:** explicit Start/Stop, one event-driven process-category listener, cross-widget shared in-memory state, R258 audit, fixed click-only tool actions. R263 exact Excel suggestions remain separate and intact. Native Windows CI #563 PASS | Verify actual Excel/Explorer/Word/browser switching and privacy on user's installed PC and both screens; never auto-paste |
| Pinned apps recent documents | R262 per-app recent-document choices + click-time revalidation exist | Real Windows Recent/Office/Explorer validation |
| Settings stability and keyboard navigation | R255 guarded Features→Settings, R207 keyboard donut, R246 help, R208 contrast | Real settings failure cases and keyboard/screen-reader navigation for every donut level, multi-DPI |
| Search and indexing | R269 shared progressive multi-drive index, R259 palette facets, R260 index health | Real multi-drive end-to-end latency, cancel/stale searches, optional native Everything IPC |
| Workspace profiles and window layouts | **R281** replaces the lossy profile Save/Import with a cross-process OS mutex, strict existing-file checks, atomic write, and import backup; native Windows CI #563 PASS. R179/R257 already provide separate captured window-layout restore safeguards | Physical saved-widget-profile UI plus multi-monitor window placement, reconnect, and missing-app restore acceptance |

## P1 UI modernization — user-requested design rework (R283–R284)

| Area | Actual committed implementation | Remaining acceptance |
|---|---|---|
| Modern styling system | R283 shared WPF theme palette: Midnight Navy / violet-blue accents, light/dark, native high-contrast, rounded accessible controls, typography, cards and Deck chrome | Physical contrast checks with Windows Narrator/NVDA and 100–200% DPI |
| Main Workspace and Action Palette | R283 real MainWindow.xaml updated to gradient header, modern native-button templates, dark tab navigation, refined status; Action Palette and Feature Hub updated | Exercise every main tab, settings and shortcut without visual clipping |
| Floating clock, Ring and Deck | R283 modern widget face/halo, non-interactive clock ornaments, improved Deck chrome; existing twelve wedge geometries and R282 collision-free Deck placement unchanged | Two physical displays, notifications, weather, parked widget, expanded donut and pointer hit targets |
| Settings and appearance | R283 Modern appearance added for fresh installs; legacy Soft/Material presets and current saved users retained; five Settings Studio pages and pin settings preserved | Live light/dark switching, saved-presets, reset, installed-machine real settings navigation |
| Everyday dialogs, running apps and opt-in Smart Context | R284 actual widget Details, Widget Options (scrollable), Pinned Apps, Running Apps flyouts and opt-in Smart Context cards modernized; keyboard focus/hover and WPF-native controls tested | Real Explorer/Office multi-window, Accessibility keyboard/screen-reader, small-screen visual QA |
| Verified development evidence | [Windows CI #615](https://github.com/surajTech-22/Desk-space/actions/runs/41274378265) SUCCESS, native WPF R201–R284, exact canonical SHA parity, independent build, synthetic updater checks | None of the CI evidence constitutes installed-PC, signed release or real Excel COM acceptance |

**Release gate unchanged:** This is a substantial two-stage code redesign, **not a fully released UI**. Defer further optional P2/P3 tools until P0 real updater/dual monitor/Office acceptance is addressed.

## P2 / P3 deliberately **deferred**
The plan places OCR, extra clipboard tools, batch rename additions, vault expansion, visual workflow builders and optional AAH connectors *after* P0 and the unfinished P1 workflows. R277 already provides durable rename recovery in development; do not continue refining it at the expense of core reliability.

## Release gate
- Synthetic GitHub Windows CI proves a **build** and the tested file/WPF scenarios, not physical dual-monitor devices, installed Microsoft Office, real audio/Explorer, or a production updater.
- Neither a code-signing identity nor physical machine control can be inferred from a successful GitHub Actions run.
- Preserve the **single consolidated release** for Stable and Tester after every required test passes; until then branch/PR-only code with no claimed auto-update delivery.
- When resuming work, advance a P0 or unimplemented P1 item and attach a test to its real module. Do not mark a roadmap item **released** merely because its code exists.
